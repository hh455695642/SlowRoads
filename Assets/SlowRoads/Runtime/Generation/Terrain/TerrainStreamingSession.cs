using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Streaming;

namespace SlowRoads.Generation.Terrain
{
    public enum TerrainWorkKind { FarInitialization, NearInitialization, FarRow, NearRow, Upload, Coverage, Retirement }
    /// <summary>Resource adapter around Ma's logical schedule. Reuses T2/T3 builders
    /// and the single P5 producer barrier; never generates roads synchronously.
    /// All callbacks execute on the session thread. Upload must complete before
    /// returning its residency receipt. T4 owns data, presentation owns Meshes.</summary>
    public sealed class TerrainStreamingSession : IDisposable
    {
        sealed class Resident
        {
            public TerrainMeshData Tile;
            public TerrainResidency Receipt;
            public TerrainOverlapState Overlap;
        }
        readonly struct Cover
        {
            public readonly int X,Z,Index;
            public Cover(int x,int z,int index){X=x;Z=z;Index=index;}
        }
        readonly RoadStreamingSession road;
        readonly Func<TerrainRegionState,TerrainMeshData,TerrainResidency> upload;
        readonly Dictionary<TerrainRegionState,Resident> residents=new();
        readonly List<TerrainRegionState> work=new(),release=new();
        readonly Dictionary<(int,int),TerrainRegionState> cells=new();
        readonly Queue<Cover> coverage=new();
        FarTerrainTileBuilder far;
        NearTerrainChunkBuilder near;
        TerrainRegionState building;
        int generation=-1;
        long layer=-1;
        bool disposed;
        public TerrainRegionSchedule Schedule {get;private set;}
        public int ActiveTiles {get;private set;}
        public int ActiveChunks {get;private set;}
        public int UploadedCount {get;private set;}
        public int RetiredCount {get;private set;}
        public bool Building=>building!=null;
        public bool NeedsWork {get;private set;}=true;
        public event Action<TerrainRegionState> Released;
        public event Action Reset;
        public event Action<TerrainRegionState,TerrainOverlapState> CoverageChanged;
        // Optional block measurements. Null subscribers incur no clock reads;
        // the pure generator remains independent of Unity's profiling assembly.
        public event Action<TerrainWorkKind,long> WorkMeasured;
        long BeginMeasure()=>WorkMeasured==null?0:Stopwatch.GetTimestamp();
        void EndMeasure(TerrainWorkKind kind,long start){if(start!=0)WorkMeasured?.Invoke(kind,Stopwatch.GetTimestamp()-start);}
        public TerrainStreamingSession(RoadStreamingSession road,Func<TerrainRegionState,TerrainMeshData,TerrainResidency> upload)
        {this.road=road??throw new ArgumentNullException(nameof(road));this.upload=upload??throw new ArgumentNullException(nameof(upload));}
        Double3? Position(int i)=>i>=road.Midline.State.Head.Index&&i<road.Midline.State.FineTail.Index-2?road.Midline.FindCoarse(i)?.Position:null;
        void Queue(TerrainRegionState r){if(!r.Retired&&!r.Uploaded&&!work.Contains(r)&&r!=building)work.Add(r);}
        void OnEvent(TerrainRegionEvent e)
        {
            TerrainRegionState r=e.Region;
            if(e.Action==TerrainRegionAction.Stage){release.Remove(r);Queue(r);}
            // Native Dt.retire retains height/mesh data for Ca's future road.
            // Only repool releases a tile; chunks release on retire.
            if((e.Chunk&&e.Action==TerrainRegionAction.Retire)||e.Action==TerrainRegionAction.Repool)release.Add(r);
        }
        bool ParentsReady(TerrainRegionState r)
        {
            bool ready=true;
            foreach(var key in r.Layout.ParentTiles)
            {
                if(!Schedule.Tiles.TryGetValue(key,out var t))throw new InvalidOperationException("Ma parent tile not registered");
                if(!residents.TryGetValue(t,out var value)||value.Tile==null||!value.Tile.CanPublish(road)){Queue(t);ready=false;}
            }
            // Collection only reads frozen XZ. Mixed geometry additionally needs
            // B7 attributes and a fine-projection halo; let the single P5 producer
            // advance while that support is incomplete, without staging a stale job.
            int max=int.MinValue;
            foreach(var cell in r.Layout.Cells)for(int i=0;i<cell.CandidateCount;i++)max=Math.Max(max,cell.Candidate(i));
            return ready&&Math.Min(road.Midline.AttributeReadyExclusive,road.Midline.State.FineTail.Index)>max+13;
        }
        void Initialize()
        {
            // P5 does not retain Se's detached head.prev. Trim only the unavailable
            // backwards halo; all subsequent Ma events operate on original indices.
            int first=Math.Max(road.Midline.State.Head.Index+12,road.Midline.State.ObserverIndex- (int)Math.Ceiling(Math.Max(40,Math.Sqrt(road.Config.TileSize)*4)*.4)+1);
            if(!Position(first).HasValue)return;
            generation=road.Midline.State.Generation;
            Schedule=new(road.TerrainConfig,Position,(x,z)=>road.Spatial.GetLastSeenIndexForTile(x,z)??int.MinValue);
            Schedule.Changed+=OnEvent;Schedule.ParentsReady=ParentsReady;
            Schedule.Initialize(first);Schedule.InnerCellRegistered+=(x,z,i)=>coverage.Enqueue(new Cover(x,z,i));
            var p=Position(first).Value;int cx=(int)Math.Floor(p.X/10),cz=(int)Math.Floor(p.Z/10);
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)coverage.Enqueue(new Cover(cx+x,cz+z,first));
        }
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(TerrainStreamingSession));
            if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));if(budget==0)return 0;
            if(!road.Ready||!road.AtTickBoundary)return 0;
            if(generation>=0&&(generation!=road.Midline.State.Generation||(layer>=0&&!road.IsTerrainDataCurrent(generation,0,layer,false))))Clear();
            if(Schedule==null){Initialize();if(Schedule==null)return 0;}
            int used=0;NeedsWork=true;
            while(used<budget)
            {
                if(building!=null)
                {
                    bool isFar=far!=null;long measured=BeginMeasure();
                    if(far!=null)far.Advance(1);else near.Advance(1);used++;
                    EndMeasure(isFar?TerrainWorkKind.FarRow:TerrainWorkKind.NearRow,measured);
                    bool cancelled=far?.Stage==FarTerrainStage.Cancelled||near?.Stage==NearTerrainStage.Cancelled;
                    if(cancelled)throw new InvalidOperationException("Terrain task invalidated while holding producer barrier");
                    var data=far?.Data??near?.Data;
                    if(data!=null)
                    {
                        TerrainResidency receipt=null;
                        try
                        {
                            measured=BeginMeasure();
                            receipt=upload(building,data);
                            if(receipt==null||!receipt.IsCurrent)throw new InvalidOperationException("Terrain upload did not commit");
                            EndMeasure(TerrainWorkKind.Upload,measured);
                        }
                        catch
                        {
                            // A completed builder transferred its arrays to Data. Disposing
                            // only the builder does not invalidate this unpublished product.
                            // Failed transactions must never leave a publishable CPU lease.
                            receipt?.Retire();data.Dispose();
                            far?.Dispose();near?.Dispose();far=null;near=null;building=null;
                            throw;
                        }
                        var resident=new Resident{Receipt=receipt};layer=data.LayerRevision;
                        if(!building.Chunk){resident.Tile=data;resident.Overlap=new(data.Config.TileFineResolution);ActiveTiles++;}
                        else {foreach(var c in data.Cells)cells[(c.X,c.Z)]=building;ActiveChunks++;data.Dispose();}
                        residents.Add(building,resident);building.Residency=receipt;building.Uploaded=true;UploadedCount++;
                        far?.Dispose();near?.Dispose();far=null;near=null;building=null;
                    }
                    continue;
                }
                if(work.Count>0)
                {
                    var r=work[0];work.RemoveAt(0);if(r.Retired||r.Uploaded)continue;
                    if(!r.Chunk){long measured=BeginMeasure();building=r;far=new FarTerrainTileBuilder(road,r.X,r.Z);EndMeasure(TerrainWorkKind.FarInitialization,measured);}
                    else
                    {
                        if(!ParentsReady(r)){work.Add(r);NeedsWork=false;return used;}
                        int min=int.MaxValue,max=int.MinValue;foreach(var c in r.Layout.Cells)for(int i=0;i<c.CandidateCount;i++){min=Math.Min(min,c.Candidate(i));max=Math.Max(max,c.Candidate(i));}
                        int first=Math.Max(road.Midline.State.Head.Index+1,min-64),last=Math.Min(Math.Min(road.Midline.AttributeReadyExclusive,road.Midline.State.FineTail.Index)-1,max+64);
                        if(min<=first||max>=last-2||last-first>=256)throw new InvalidOperationException($"Chunk support outside P5 ready window: {min}..{max}, {first}..{last}");
                        var parents=new List<TerrainMeshData>();foreach(var key in r.Layout.ParentTiles)parents.Add(residents[Schedule.Tiles[key]].Tile);
                        long measured=BeginMeasure();building=r;near=new NearTerrainChunkBuilder(road,r.Layout,first,last,parents);EndMeasure(TerrainWorkKind.NearInitialization,measured);
                    }
                    used++;continue;
                }
                bool step=Schedule.Step(road.Midline.State.ObserverIndex);used++;
                long coverageStart=BeginMeasure();FlushCoverage();EndMeasure(TerrainWorkKind.Coverage,coverageStart);
                long retirementStart=BeginMeasure();ReleaseRetired();EndMeasure(TerrainWorkKind.Retirement,retirementStart);
                if(!step&&work.Count==0){used--;NeedsWork=false;break;}
            }
            return used;
        }
        void FlushCoverage()
        {
            while(coverage.Count>0)
            {
                var c=coverage.Peek();if(!cells.TryGetValue((c.X,c.Z),out var owner)||!owner.Uploaded||!owner.Visible)break;
                coverage.Dequeue();int h=road.Config.TileSize,tx=(int)Math.Floor(c.X*10.0/h),tz=(int)Math.Floor(c.Z*10.0/h);
                int x=c.X-tx*h/10,z=c.Z-tz*h/10,f=h/10;
                Apply(tx,tz,x,z,-1,c.Index);
                if(x==0){Apply(tx-1,tz,x,z,2,c.Index);if(z==0)Apply(tx-1,tz-1,x,z,3,c.Index);else if(z==f)Apply(tx-1,tz+1,x,z,1,c.Index);}
                else if(x==f-1){Apply(tx+1,tz,x,z,6,c.Index);if(z==0)Apply(tx+1,tz-1,x,z,5,c.Index);else if(z==f-1)Apply(tx+1,tz+1,x,z,7,c.Index);}
                if(z==0)Apply(tx,tz-1,x,z,4,c.Index);else if(z==f-1)Apply(tx,tz+1,x,z,0,c.Index);
            }
            foreach(var pair in residents)if(pair.Value.Overlap?.Dirty==true)CoverageChanged?.Invoke(pair.Key,pair.Value.Overlap);
        }
        void Apply(int tx,int tz,int x,int z,int edge,int index)
        {
            if(!Schedule.Tiles.TryGetValue((tx,tz),out var t)||!residents.TryGetValue(t,out var r))return;
            if(edge<0)r.Overlap.Cell(x,z,index);else r.Overlap.Edge(x,z,edge,index);
        }
        void ReleaseRetired()
        {
            for(int i=release.Count-1;i>=0;i--)
            {
                var r=release[i];bool needed=false;
                if(!r.Chunk)foreach(var c in Schedule.Chunks.Values)if(!c.Retired&&c.Layout.ParentTiles.Contains((r.X,r.Z))){needed=true;break;}
                if(needed)continue;
                if(r.Chunk)foreach(var c in r.Layout.Cells)if(cells.TryGetValue((c.X,c.Z),out var owner)&&ReferenceEquals(owner,r))cells.Remove((c.X,c.Z));
                if(residents.Remove(r,out var data))
                {data.Tile?.Dispose();data.Receipt.Retire();if(r.Chunk)ActiveChunks--;else ActiveTiles--;RetiredCount++;r.Uploaded=false;Released?.Invoke(r);}
                release.RemoveAt(i);
            }
        }
        public string ReadinessDescription(Double3 position,int coarseIndex)
        {
            string owner=cells.TryGetValue(((int)Math.Floor(position.X/10),(int)Math.Floor(position.Z/10)),out var r)?$"chunk={r.Id}, visible={r.Visible}, retired={r.Retired}":"cell absent";
            string cover="none";if(coverage.Count>0){var c=coverage.Peek();cover=$"{c.X}/{c.Z}@{c.Index}, "+(cells.TryGetValue((c.X,c.Z),out var o)?$"owner={o.Id}, visible={o.Visible}, uploaded={o.Uploaded}":"owner absent");}
            string pending="";
            if(Schedule!=null)foreach(var c in Schedule.Chunks.Values)if(c.Layout.Sealed&&!c.Uploaded){pending=$" chunk {c.Id}@{c.CreationIndex} parents: ";foreach(var key in c.Layout.ParentTiles){if(Schedule.Tiles.TryGetValue(key,out var t))pending+=$"{key} uploaded={t.Uploaded}, retired={t.Retired}, repooled={t.Repooled}, stage={t.BuildRequested}; ";else pending+=$"{key} MISSING; ";}break;}
            return $"index={coarseIndex}, {owner}; first pending={cover}; l2={Schedule?.L2}; building={Building}; counts={ActiveTiles}/{ActiveChunks}, gen={generation}/{road.Midline.State.Generation}, ahead={Schedule?.LookaheadIndex};{pending}";
        }
        public bool CanTraverse(Double3 position,int coarseIndex)
        {
            if(Schedule==null||ActiveTiles<1||building!=null)return false;
            if(!cells.TryGetValue(((int)Math.Floor(position.X/10),(int)Math.Floor(position.Z/10)),out var r)||!r.Visible||r.Retired)return false;
            // Unready future cells retain the existing far mesh: FlushCoverage is the
            // publication guard. Their queue must not prevent traversing a ready cell.
            return true;
        }
        void Clear()
        {
            // Cancel the completed-but-uncommitted product as well as active work.
            far?.Data?.Dispose();near?.Data?.Dispose();
            far?.Dispose();near?.Dispose();far=null;near=null;building=null;
            foreach(var r in residents.Values){r.Tile?.Dispose();r.Receipt.Retire();}
            residents.Clear();work.Clear();release.Clear();cells.Clear();coverage.Clear();Schedule=null;generation=-1;layer=-1;ActiveTiles=ActiveChunks=0;Reset?.Invoke();
        }
        public void Dispose(){if(disposed)return;Clear();disposed=true;}
    }
}
