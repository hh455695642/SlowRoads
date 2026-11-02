using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Terrain;
namespace SlowRoads.Generation.Contents.Trees
{
    /// <summary>C1 joins the single T4 producer barrier. Parents stay resident until
    /// near consumers commit; the observer uses T4's existing Ready handshake.
    /// No independent clock, origin, spatial index or full-world regeneration.</summary>
    public sealed class TreeStreamingSession : IDisposable
    {
        readonly RoadStreamingSession road;
        readonly TerrainStreamingSession terrain;
        readonly Dictionary<TerrainRegionState,TreeTileCandidates> staging=new();
        readonly Dictionary<(int,int),TerrainRegionState> cellOwners=new();
        public HillsTreeDensity Density {get;}
        public TreeCandidateGenerator Generator {get;private set;}
        public TreeCandidateStore Store {get;}
        public ContentAttributeStore Attributes {get;}
        public event Action<TerrainRegionState,TreeTileCandidates> TileCommitted;
        readonly int detail;
        bool disposed;
        public TreeStreamingSession(RoadStreamingSession road,TerrainStreamingSession terrain,int? density=null)
        {
            this.road=road;this.terrain=terrain;detail=road.TerrainConfig.Detail;
            Density=new(MidlineConfig.HillsStyle.Hills,road.TerrainConfig.Seed,detail,density);
            Generator=new(Density,detail);Store=new(road.Midline.State.Generation);
            Attributes=new(new(Store.Generation,road.Midline.Revision,0,0));
            terrain.FarNormalSample+=Normal;terrain.RegionCommitted+=Commit;terrain.Released+=Release;terrain.Reset+=Reset;
            terrain.TreeShadowProvider=(x,z)=>Density.GetShadowDensityAt(x,z);
        }
        void Normal(TerrainRegionState r,FarTerrainTileBuilder builder,int x,int z,double normal)
        {
            if(Store.Generation!=builder.Generation)
            {staging.Clear();cellOwners.Clear();Generator=new(Density,detail);Store.Reset(builder.Generation);}
            Density.SetHeadLayers(builder.NegativeXLayer,builder.NonNegativeXLayer);
            if(!staging.TryGetValue(r,out var tile))staging.Add(r,tile=new(r.X,r.Z,r.CreationIndex,road.Config.TileSize,Store.Generation,Generator,detail));
            tile.SampleNormal(x,z,normal,builder.SampleRawForContents);
        }
        void Commit(TerrainRegionState r,TerrainMeshData data)
        {
            Attributes.Rebase(data.Generation,data.Revision,data.LayerRevision);
            if(!r.Chunk)
            {
                if(!staging.Remove(r,out var tile))throw new InvalidOperationException("Missing coarse-normal content transaction");
                Store.Commit(tile);TileCommitted?.Invoke(r,tile);return;
            }
            foreach(var cell in data.Cells)
            {
                int n=(cell.Resolution+1)*(cell.Resolution+1);var h=new float[n];var d=new float[n];var normals=new float[n];
                for(int i=0;i<n;i++){int k=cell.VertexStart+i;h[i]=data.Positions[k*3+1];d[i]=data.RoadProximity[k];normals[i]=data.CoarseNormals[k*3+1];}
                Store.EvaluateCell(new(cell.X,cell.Z,cell.Resolution,h,d,normals));cellOwners[(cell.X,cell.Z)]=r;
            }
        }
        void Release(TerrainRegionState r)
        {
            staging.Remove(r);
            if(!r.Chunk){Store.ReleaseTile(r.X,r.Z);return;}
            foreach(var cell in r.Layout.Cells)if(cellOwners.TryGetValue((cell.X,cell.Z),out var owner)&&ReferenceEquals(owner,r))
            {cellOwners.Remove((cell.X,cell.Z));Store.ReleaseCell(cell.X,cell.Z);}
        }
        void Reset()
        {
            staging.Clear();cellOwners.Clear();int gen=road.Midline.State.Generation;
            if(gen!=Store.Generation)Generator=new(Density,detail); // Fresh world only. Warm layer rebuild retains random tables.
            Store.Reset(gen);Attributes.Reset(new(gen,road.Midline.Revision,0,Attributes.Version.Revision+1));
        }
        public float ShadowAt(double x,double z)=> (float)Math.Max(Density.GetShadowDensityAt(x,z),Attributes.Read(new((int)Math.Floor(x/10+.5),(int)Math.Floor(z/10+.5),ContentChannel.TreeShadow)));
        public float BaseShadowAt(double x,double z)=>(float)Density.GetShadowDensityAt(x,z);
        public float AdditionalShadowAt(double x,double z)=>(float)Attributes.Read(new((int)Math.Floor(x/10+.5),(int)Math.Floor(z/10+.5),ContentChannel.TreeShadow));
        public float FieldAt(double x,double z)=>(float)Attributes.Read(new((int)Math.Floor(x/10+.5),(int)Math.Floor(z/10+.5),ContentChannel.FieldColour));
        public void Dispose()
        {
            if(disposed)return;disposed=true;terrain.FarNormalSample-=Normal;terrain.RegionCommitted-=Commit;terrain.Released-=Release;terrain.Reset-=Reset;terrain.TreeShadowProvider=null;
            staging.Clear();cellOwners.Clear();Store.Reset(Store.Generation+1);
            // Late C2 results retain the previous token; world disposal must reject
            // them and release all source-owned attributes as well as tree entries.
            Attributes.Reset(new(Store.Generation,road.Midline.Revision,0,Attributes.Version.Revision+1));Density.Dispose();
        }
    }
}
