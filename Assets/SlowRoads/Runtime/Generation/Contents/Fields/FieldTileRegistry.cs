using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Ra/Ea L43227–43298. Shared field tiles are distinct from terrain tiles.</summary>
    public sealed class FieldTileRegistry : IDisposable
    {
        public sealed class Tile
        {
            public readonly int X,Z,Generation;
            public readonly double WorldX,WorldZ;
            public int Members {get;internal set;}
            internal readonly bool[] DrawnFields=new bool[21];
            internal readonly int?[] DrawnWalls;
            internal readonly double[] Heights=new double[36],Densities=new double[36];
            internal readonly FieldNodeProjection[] Projections=new FieldNodeProjection[36];
            internal Tile(int x,int z,int generation,FieldTemplate template)
            {X=x;Z=z;Generation=generation;WorldX=x*template.Scale;WorldZ=z*template.Scale;DrawnWalls=new int?[template.Edges.Length];}
            public bool IsFieldClaimed(int index)=>DrawnFields[index];
            public double CachedHeight(int index)=>Heights[index];
            public double CachedTreeDensity(int index)=>Densities[index];
        }
        readonly Dictionary<(int,int),Tile> tiles=new();
        internal readonly FieldRoadHistory RoadHistory=new();
        internal int MinimumRoadAnchor
        {get{int min=int.MaxValue;foreach(var tile in tiles.Values)foreach(var p in tile.Projections)if(p!=null)min=Math.Min(min,p.Index);return min;}}
        public FieldTemplate Template {get;}
        public int Generation {get;}
        bool disposed;
        public int LiveCount=>tiles.Count;
        internal sealed class Checkpoint
        {
            internal readonly Tile[] Tiles;internal readonly bool[][] Fields;internal readonly int?[][] Walls;
            internal readonly double[][] Heights,Densities;internal readonly FieldNodeProjection[][] Projections;
            internal Checkpoint(Tile[] tiles)
            {
                Tiles=tiles;Fields=new bool[tiles.Length][];Walls=new int?[tiles.Length][];Heights=new double[tiles.Length][];Densities=new double[tiles.Length][];Projections=new FieldNodeProjection[tiles.Length][];
                for(int i=0;i<tiles.Length;i++)
                {
                    var t=tiles[i];Fields[i]=(bool[])t.DrawnFields.Clone();Walls[i]=(int?[])t.DrawnWalls.Clone();Heights[i]=(double[])t.Heights.Clone();Densities[i]=(double[])t.Densities.Clone();Projections[i]=new FieldNodeProjection[36];
                    for(int n=0;n<36;n++){var p=t.Projections[n];if(p!=null)Projections[i][n]=new FieldNodeProjection{Index=p.Index,Side=p.Side,Distance=p.Distance,Drawn=p.Drawn};}
                }
            }
        }
        internal Checkpoint Capture(Tile[] lease)=>new(lease);
        internal void Restore(Checkpoint state)
        {
            for(int i=0;i<state.Tiles.Length;i++)
            {var t=state.Tiles[i];if(disposed||!tiles.TryGetValue((t.X,t.Z),out var current)||!ReferenceEquals(t,current))continue;
                Array.Copy(state.Fields[i],t.DrawnFields,21);Array.Copy(state.Walls[i],t.DrawnWalls,t.DrawnWalls.Length);Array.Copy(state.Heights[i],t.Heights,36);Array.Copy(state.Densities[i],t.Densities,36);Array.Copy(state.Projections[i],t.Projections,36);}
        }
        public FieldTileRegistry(double scale,int generation){Template=new FieldTemplate(scale);Generation=generation;}
        public Tile[] Acquire(double worldX,double worldZ,double terrainTileSize)
        {
            if(disposed)throw new ObjectDisposedException(nameof(FieldTileRegistry));
            if(!double.IsFinite(worldX)||!double.IsFinite(worldZ)||!double.IsFinite(terrainTileSize)||terrainTileSize<=0)throw new ArgumentOutOfRangeException();
            double scale=Template.Scale;
            int x1=checked((int)Math.Floor(worldX/scale)),z1=checked((int)Math.Floor(worldZ/scale));
            double ex=(worldX+terrainTileSize)/scale,ez=(worldZ+terrainTileSize)/scale;
            int x2=checked((int)Math.Floor(ex)),z2=checked((int)Math.Floor(ez));
            if(ex-x2<10/scale)x2--;if(ez-z2<10/scale)z2--;
            var result=new List<Tile>();
            // Preserve Ra's axis-equality branches, including its narrow-tile edge behavior.
            if(x1!=x2){for(int x=x1;x<=x2;x++){if(z1==z2)result.Add(Get(x,z1));else for(int z=z1;z<=z2;z++)result.Add(Get(x,z));}}
            else if(z1!=z2){for(int z=z1;z<=z2;z++)result.Add(Get(x1,z));}else result.Add(Get(x1,z1));
            return result.ToArray();
        }
        Tile Get(int x,int z)
        {if(!tiles.TryGetValue((x,z),out var tile)){tile=new Tile(x,z,Generation,Template);tiles.Add((x,z),tile);}tile.Members++;return tile;}
        public void Release(IReadOnlyList<Tile> lease)
        {
            if(disposed)return;
            foreach(var tile in lease)
            {
                if(tile.Generation!=Generation||!tiles.TryGetValue((tile.X,tile.Z),out var current)||!ReferenceEquals(tile,current))return;
                if(--tile.Members<=0)tiles.Remove((tile.X,tile.Z));
            }
        }
        public void Dispose(){tiles.Clear();RoadHistory.Clear();disposed=true;}
    }
}
