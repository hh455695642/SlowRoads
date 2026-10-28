using System;

namespace SlowRoads.Generation.Heightfield
{
    /// <summary>Immutable snapshot of Pl configuration; Hills.source.txt L06516-L06543.</summary>
    public sealed class HillsHeightfieldConfig
    {
        readonly int[] layerResolutions;
        public string Seed { get; }
        public double Scale { get; }
        public double Offset { get; }
        public int Resolution { get; }
        public int UpresFactor { get; }
        public int Depth => layerResolutions.Length;
        public double DepthHeightFactor { get; }
        public bool Squared { get; }
        public bool Temper { get; }
        public double TemperBase { get; }
        public double TemperBelow { get; }
        public double TemperMin { get; }
        public bool Spiralise { get; }
        public int MaxCached { get; }
        public double TileScaleRandom { get; }
        public bool Repeated { get; }
        public int LayerResolution(int depth) => layerResolutions[depth];
        public int[] CopyLayerResolutions() => (int[])layerResolutions.Clone();

        public HillsHeightfieldConfig(
            string seed = "a", double scale = 1, double offset = 0,
            int resolution = 5, int upresFactor = 2, int depth = 3,
            int[] layerResolutions = null, double depthHeightFactor = 1,
            bool squared = false, bool temper = false, double temperBase = 0,
            double temperBelow = 100, double temperMin = 0.25,
            bool spiralise = true, int maxCached = 32,
            double tileScaleRandom = 0, bool repeated = false)
        {
            Seed = seed ?? throw new ArgumentNullException(nameof(seed));
            Scale = scale; Offset = offset; Resolution = resolution; UpresFactor = upresFactor;
            DepthHeightFactor = depthHeightFactor; Squared = squared; Temper = temper;
            TemperBase = temperBase; TemperBelow = temperBelow; TemperMin = temperMin;
            Spiralise = spiralise; MaxCached = maxCached;
            TileScaleRandom = tileScaleRandom; Repeated = repeated;
            // Pl defaults to [3,6,9]. Its empty-array fallback calls an uninitialised
            // field in v1.0.2, so reject that input instead of silently changing output.
            if (layerResolutions != null && layerResolutions.Length == 0)
                throw new ArgumentException("The archived Pl cannot generate an empty layerResolutions array.", nameof(layerResolutions));
            this.layerResolutions = layerResolutions == null ? new[] { 3, 6, 9 } : (int[])layerResolutions.Clone();
            for (int i = 0; i < this.layerResolutions.Length; i++)
                if (this.layerResolutions[i] <= 0) throw new ArgumentOutOfRangeException(nameof(layerResolutions));
            if (MaxCached < 1) throw new ArgumentOutOfRangeException(nameof(maxCached));
            if (Temper && TemperBelow == 0) throw new ArgumentOutOfRangeException(nameof(temperBelow));
        }
    }
}
