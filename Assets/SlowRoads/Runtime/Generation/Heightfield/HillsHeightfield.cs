using System;
using System.Collections.Generic;
using System.Globalization;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Heightfield
{
    /// <summary>
    /// Hills Pl height query and oe cache. Source: Dev L00025-L00133; Hills L06516-L06829.
    /// X/Z and height stay double until the later rendering boundary.
    /// </summary>
    public sealed class HillsHeightfield : IDisposable
    {
        const double TileSize = 3000.0;
        const int CacheOffset = 100; // Original oe stores tile coordinates as object keys +100.
        readonly HillsHeightfieldConfig config;
        readonly JsKeys<JsKeys<CacheEntry>> cache = new();
        readonly int[] currentLayer = new int[2];
        Tile protoTile;
        int cacheCount;
        bool disposed;

        public HillsHeightfield(HillsHeightfieldConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public int CacheCount => cacheCount;
        public int NegativeXLayer => currentLayer[0];
        public int NonNegativeXLayer => currentLayer[1];

        public double GetMidlineXZ(double x, double z) => GetXZ(x, z);

        // Extension point: the streaming scheduler can prewarm a tile without sampling a height.
        public void WarmTile(int tileX, int tileZ)
        {
            ThrowIfDisposed();
            GetTile(tileX, tileZ);
        }

        public double GetXZ(double x, double z, int depth = 0)
        {
            ThrowIfDisposed();
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(z) || double.IsInfinity(z))
                throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be finite.");

            // JS l = l || config.depth: zero means all layers.
            int count = depth == 0 ? config.Depth : Math.Min(depth, config.Depth);
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(depth));
            double worldX = x / TileSize, worldZ = z / TileSize;
            int tileX = checked((int)Math.Floor(worldX));
            int tileZ = checked((int)Math.Floor(worldZ));
            ValidateTile(tileX, tileZ);
            double localX = worldX - tileX, localZ = worldZ - tileZ;
            Tile tile = GetTile(tileX, tileZ);
            double height = 0;
            for (int layer = 0; layer < count; layer++)
            {
                int resolution = config.LayerResolution(layer);
                int ix = (int)Math.Floor(localX * resolution - 0.5);
                int iz = (int)Math.Floor(localZ * resolution - 0.5);
                double contribution = 0;
                // Preserve the original x-major, z-minor accumulation order.
                contribution += Contribution(tile, tileX, tileZ, layer, resolution, ix, iz, localX, localZ);
                contribution += Contribution(tile, tileX, tileZ, layer, resolution, ix, iz + 1, localX, localZ);
                contribution += Contribution(tile, tileX, tileZ, layer, resolution, ix + 1, iz, localX, localZ);
                contribution += Contribution(tile, tileX, tileZ, layer, resolution, ix + 1, iz + 1, localX, localZ);

                double temper = 1;
                if (config.Temper && layer > 0)
                {
                    double factor = (height + config.Offset + config.TemperBase) / config.TemperBelow;
                    temper = Math.Min(Math.Max(config.TemperMin, factor), 1);
                }
                height += contribution * temper;
            }
            return height + config.Offset;
        }

        public void IncrementLayer(int side, bool force = false)
        {
            ThrowIfDisposed();
            if (side < 0 || side > 1) throw new ArgumentOutOfRangeException(nameof(side));
            if (!config.Spiralise && !force) return;
            currentLayer[side]++;
            PruneCache(true);
        }

        public void SetLayers(int negativeX, int nonNegativeX, bool force = false)
        {
            ThrowIfDisposed();
            if (!config.Spiralise && !force) return;
            currentLayer[0] = negativeX;
            currentLayer[1] = nonNegativeX;
            PruneCache(true);
        }

        // Explicit maintenance for streaming; original pruneCache(force) is not a hard capacity limit.
        public void PruneCache(bool force = false)
        {
            ThrowIfDisposed();
            if (!force && cacheCount <= config.MaxCached) return;
            for (int xi = 0; xi < cache.Count;)
            {
                int x = cache.KeyAt(xi);
                var row = cache[x];
                for (int zi = 0; zi < row.Count;)
                {
                    int z = row.KeyAt(zi);
                    var entry = row[z];
                    int expectedLayer = currentLayer[x < CacheOffset ? 0 : 1];
                    if (entry.Access < 0 || entry.Layer != expectedLayer)
                    {
                        row.Remove(z);
                        cacheCount--;
                        if (!force && cacheCount <= config.MaxCached) break;
                    }
                    else { entry.Access--; zi++; }
                }
                if (row.Count == 0) cache.Remove(x);
                else xi++;
                if (!force && cacheCount <= config.MaxCached) break;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            cache.Clear();
            protoTile = null;
            cacheCount = 0;
            disposed = true;
        }

        void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(HillsHeightfield));
        }

        double Contribution(Tile current, int tileX, int tileZ, int layerIndex, int resolution,
            int pointX, int pointZ, double localX, double localZ)
        {
            int neighborX = pointX < 0 ? -1 : pointX >= resolution ? 1 : 0;
            int neighborZ = pointZ < 0 ? -1 : pointZ >= resolution ? 1 : 0;
            int cellX = pointX < 0 ? resolution - 1 : pointX >= resolution ? 0 : pointX;
            int cellZ = pointZ < 0 ? resolution - 1 : pointZ >= resolution ? 0 : pointZ;
            Tile tile = neighborX == 0 && neighborZ == 0
                ? current : GetTile(tileX + neighborX, tileZ + neighborZ);
            ControlPoint point = tile.Layers[layerIndex].Points[cellX * resolution + cellZ];
            double dx = point.X - (localX - neighborX);
            double dz = point.Z - (localZ - neighborZ);
            return Kernel(dx, dz, point);
        }

        // Shared radial kernel; explicit value input also supports offline boundary validation.
        public static double SampleKernel(double x, double z, ControlPoint point) =>
            Kernel(point.X - x, point.Z - z, point);

        static double Kernel(double dx, double dz, ControlPoint point)
        {
            double radius = dx * dx + dz * dz;
            if (radius >= point.RadiusSquared) return 0;
            double t = radius / point.RadiusSquared;
            double smooth = -((t * (t * 6 - 15) + 10) * t * t * t) + 1;
            return smooth * point.Y;
        }

        Tile GetTile(int tileX, int tileZ)
        {
            ValidateTile(tileX, tileZ);
            int x = checked(tileX + CacheOffset), z = checked(tileZ + CacheOffset);
            if (cache.TryGetValue(x, out var row) && row.TryGetValue(z, out var hit))
            {
                hit.Access = 2;
                return hit.Tile;
            }
            cacheCount++; // oe increments before pruning and generation.
            PruneCache();
            Tile result = GenerateTile(tileX, tileZ);
            cache.TryGetValue(x, out row); // Pruning may have removed the old row.
            if (row == null)
            {
                row = new JsKeys<CacheEntry>();
                cache.Add(x, row);
            }
            row.Add(z, new CacheEntry(result, currentLayer[tileX < 0 ? 0 : 1]));
            return result;
        }

        Tile GenerateTile(int tileX, int tileZ)
        {
            ValidateTile(tileX, tileZ);
            if (config.Repeated && protoTile != null) return protoTile;
            // JS number-to-string uses invariant ASCII digits/signs; device locale must not change the stream.
            string seed = config.Repeated ? config.Seed :
                config.Seed + "#" + currentLayer[tileX < 0 ? 0 : 1].ToString(CultureInfo.InvariantCulture) +
                "#" + tileX.ToString(CultureInfo.InvariantCulture) +
                "#" + tileZ.ToString(CultureInfo.InvariantCulture);
            var random = new AleaRandom(seed);
            // The random call is mandatory even when TileScaleRandom is zero.
            double amplitude = 1 + random.Next() * config.TileScaleRandom - config.TileScaleRandom / 2;
            var layers = new Layer[config.Depth];
            double depthFactor = config.DepthHeightFactor;
            for (int depth = 0; depth < layers.Length; depth++)
            {
                int resolution = config.LayerResolution(depth);
                double factor = config.Resolution / (double)resolution * depthFactor;
                depthFactor *= config.DepthHeightFactor;
                var points = new ControlPoint[resolution * resolution];
                double radius = 1.0 / resolution;
                for (int x = 0; x < resolution; x++)
                for (int z = 0; z < resolution; z++)
                {
                    double h = random.Next() * 2 - 1;
                    if (config.Squared)
                        h = h < 0 ? h * (-(h * (1 + h)) * 4) : h * (h * (1 - h) * 4);
                    h *= factor;
                    points[x * resolution + z] = new ControlPoint(
                        (x + 0.5) / resolution, h * config.Scale * amplitude,
                        (z + 0.5) / resolution, radius * radius);
                }
                layers[depth] = new Layer(points);
            }
            var tile = new Tile(layers);
            if (config.Repeated) protoTile = tile;
            return tile;
        }

        // Test/debug access does not mutate the cache and only exposes a value copy.
        public ControlPoint GenerateControlPoint(int tileX, int tileZ, int depth, int x, int z)
        {
            ThrowIfDisposed();
            int resolution = config.LayerResolution(depth);
            if ((uint)x >= resolution || (uint)z >= resolution) throw new ArgumentOutOfRangeException();
            return GenerateTile(tileX, tileZ).Layers[depth].Points[x * resolution + z];
        }

        // Opt-in diagnostic copies only; normal sampling/streaming never allocates these.
        public ControlPoint[] CopyTileControlPoints(int tileX, int tileZ, int depth)
        {
            ThrowIfDisposed();
            return (ControlPoint[])GenerateTile(tileX, tileZ).Layers[depth].Points.Clone();
        }

        public CacheState[] CopyCacheState()
        {
            ThrowIfDisposed();
            var result = new CacheState[cacheCount];
            int index = 0;
            for (int xi = 0; xi < cache.Count; xi++)
            {
                int x = cache.KeyAt(xi);
                var row = cache[x];
                for (int zi = 0; zi < row.Count; zi++)
                {
                    int z = row.KeyAt(zi);
                    var entry = row[z];
                    result[index++] = new CacheState(x - CacheOffset, z - CacheOffset, entry.Access, entry.Layer);
                }
            }
            return result;
        }

        public readonly struct CacheState
        {
            public readonly int X, Z, Access, Layer;
            public CacheState(int x, int z, int access, int layer)
            { X = x; Z = z; Access = access; Layer = layer; }
        }

        static void ValidateTile(int x, int z)
        {
            // Reserve one neighboring tile on each side and the +100 JS cache-key offset.
            // Wider world coordinates require a deliberate key/origin migration in streaming.
            if (x <= int.MinValue || z <= int.MinValue ||
                x >= int.MaxValue - CacheOffset || z >= int.MaxValue - CacheOffset)
                throw new ArgumentOutOfRangeException(nameof(x), "Tile coordinate exceeds the supported Int32 cache domain.");
        }

        public readonly struct ControlPoint
        {
            public readonly double X, Y, Z, RadiusSquared;
            public ControlPoint(double x, double y, double z, double radiusSquared)
            {
                X = x; Y = y; Z = z; RadiusSquared = radiusSquared;
            }
        }

        sealed class Layer
        {
            public readonly ControlPoint[] Points;
            public Layer(ControlPoint[] points) { Points = points; }
        }

        sealed class Tile
        {
            public readonly Layer[] Layers;
            public Tile(Layer[] layers) { Layers = layers; }
        }

        sealed class CacheEntry
        {
            public readonly Tile Tile;
            public readonly int Layer;
            public int Access = 2;
            public CacheEntry(Tile tile, int layer) { Tile = tile; Layer = layer; }
        }

        // JS object enumeration: nonnegative integer keys first in ascending order;
        // negative string keys then follow insertion order. Deletion/reinsertion goes last.
        // Keys are Int32 here, so every nonnegative key is a valid JS array-index key.
        sealed class JsKeys<T>
        {
            readonly Dictionary<int, T> values = new();
            readonly List<int> keys = new();
            int numericCount;
            public int Count => keys.Count;
            public T this[int key] => values[key];
            public int KeyAt(int index) => keys[index];
            public bool TryGetValue(int key, out T value) => values.TryGetValue(key, out value);
            public void Add(int key, T value)
            {
                values.Add(key, value);
                if (key < 0) keys.Add(key);
                else
                {
                    int index = keys.BinarySearch(0, numericCount, key, Comparer<int>.Default);
                    keys.Insert(~index, key);
                    numericCount++;
                }
            }
            public void Remove(int key)
            {
                if (!values.Remove(key)) return;
                keys.Remove(key);
                if (key >= 0) numericCount--;
            }
            public void Clear() { values.Clear(); keys.Clear(); numericCount = 0; }
        }
    }
}
