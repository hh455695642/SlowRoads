using System;
using SlowRoads.Generation.Road;

namespace SlowRoads.Generation.Terrain
{
    /// <summary>Hills _l / Ma, Settings L42082–42124. View distance and detail are independent axes.</summary>
    public sealed class TerrainGenerationConfig
    {
        public const string SourceId="SlowRoads-1.0.2-Hills";
        public readonly string Seed;
        public readonly HillsLaneStyle LaneStyle;
        public readonly int ViewDistance,Detail,TileSize,TileCoarseResolution,TileFineResolution,CellResolution,ChunkCapacity;
        public const int CellSize=10;
        public readonly string Identity;
        public double CellStep=> (double)CellSize/CellResolution;
        public TerrainGenerationConfig(string seed,int viewDistance,int detail,HillsLaneStyle lane)
        {
            if(seed==null)throw new ArgumentNullException(nameof(seed));
            if((uint)viewDistance>5||(uint)detail>4||(uint)lane>4)throw new ArgumentOutOfRangeException();
            Seed=seed;ViewDistance=viewDistance;Detail=detail;LaneStyle=lane;
            Identity=FormattableString.Invariant($"{SourceId}:{seed.Length}:{seed}:{viewDistance}:{detail}:{(int)lane}");
            TileSize=new[]{240,480,840,1500,2000,2500}[viewDistance];
            TileCoarseResolution=(int)Math.Floor(TileSize/20.0+.5);TileFineResolution=2*TileCoarseResolution;
            CellResolution=(int)lane<=1?24:new[]{10,16,20,20,20}[detail];
            ChunkCapacity=Math.Min(100,20+TileSize/10);
        }
    }
}
