using System;
using System.Collections.Generic;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Terrain;
using SlowRoads.Generation.Contents.Fields;
using SlowRoads.Generation.Contents.Trees;
using SlowRoads.Generation.Contents.Walls;

namespace SlowRoads.Generation.Contents.Grass
{
    public readonly struct GrassInstance
    {
        public readonly int CellX,CellZ;public readonly GrassCandidate Candidate;
        internal GrassInstance(int x,int z,GrassCandidate candidate){CellX=x;CellZ=z;Candidate=candidate;}
        // Logical identity is (chunk Owner, CellX, CellZ, Candidate.X, Candidate.Z).
        // Bush priority permits at most one logical instance at a sampled point.
    }
    public sealed class GrassChunkData
    {
        public readonly TerrainRegionKey Owner;public readonly long Revision;public readonly FieldRandomState.Stream.Cursor Before,After;
        public readonly double ObserverIndex;
        public IReadOnlyList<GrassInstance> Instances {get;}
        internal GrassChunkData(TerrainRegionKey owner,long revision,List<GrassInstance> instances,FieldRandomState.Stream.Cursor before,FieldRandomState.Stream.Cursor after,double observerIndex=0)
        {Owner=owner;Revision=revision;Instances=instances.AsReadOnly();Before=before;After=after;ObserverIndex=observerIndex;}
    }
    /// <summary>s0.load1 ownership on T4's existing FIFO. Geometry copies before CPU release,
    /// while G4 consumption waits for PrepareFine AND the original base-chunk completion.
    /// The producer barrier remains held for an entire grass job, including all its cells.</summary>
    public sealed class GrassStreamingSession : IDisposable
    {
        sealed class Job
        {
            internal TerrainRegionState Region;internal GrassSurfaceSnapshot[] Cells;internal long Order;internal double Observer;
            internal int Cell;internal readonly List<GrassInstance> Instances=new();
        }
        readonly RoadStreamingSession road;readonly TerrainStreamingSession terrain;readonly FieldStreamingSession fields;readonly WallStreamingSession walls;
        readonly Dictionary<TerrainRegionState,Job> geometry=new();readonly Dictionary<TerrainRegionState,GrassChunkData> active=new();
        readonly HashSet<TerrainRegionState> requested=new();readonly Queue<Job> queue=new();
        readonly Func<bool> previousPending,ownPending;
        readonly GrassGenerationConfig config;GrassRandomState random;int generation;bool disposed;long queuedCells;
        Job current;GrassCandidateBuilder builder;FieldRandomState.Stream.Lease lease;FieldRandomState.Stream.Cursor[] checkpoint;
        public event Action<GrassChunkData> Committed;public event Action<TerrainRegionKey> Released;
        public event Action Cleared;
        public IEnumerable<GrassChunkData> Active=>active.Values;public GrassRandomState Random=>random;
        public bool Building=>current!=null;public bool HasWork=>current!=null||queue.Count>0;
        public bool PriorityTurn=>current!=null||queue.Count>0&&queue.Peek().Order<terrain.NextWorkOrder;
        public long CompletedCount {get;private set;}
        public long PendingCellCount=>queuedCells;
        public GrassStreamingSession(RoadStreamingSession road,TerrainStreamingSession terrain,FieldStreamingSession fields,WallStreamingSession walls,FieldRandomState.Stream moduleDensity=null)
        {
            this.road=road;this.terrain=terrain;this.fields=fields;this.walls=walls;
            var lane=road.Config.Mesh.LaneStyle;config=GrassGenerationConfig.Detail(road.TerrainConfig.Detail,lane==Road.HillsLaneStyle.OffRoad,road.Config.Mesh.OffRoad);
            generation=road.Midline.State.Generation;random=new(fields.Random.Shared,road.TerrainConfig.CellStep,moduleDensity);
            terrain.RegionCommitted+=Capture;terrain.Lifecycle+=Lifecycle;terrain.Released+=Release;terrain.Reset+=Reset;
            previousPending=terrain.ContentWorkPending;ownPending=()=>previousPending?.Invoke()==true||PriorityTurn;terrain.ContentWorkPending=ownPending;
        }
        void Enqueue(Job job)
        {
            if(job.Order!=0||!requested.Contains(job.Region)||job.Region.Retired)return;
            // U.initUpgrade defers addJob while base LOD0 is unfinished. Reserve NOW, not at an early request.
            job.Order=terrain.ReserveContentOrder();queue.Enqueue(job);
        }
        void Capture(TerrainRegionState region,TerrainMeshData data)
        {
            if(!region.Chunk)return;
            // Ka.checkRegenerate/reinit queues only the field job (S L43963-43988).
            // U.load1 guards hasLod1 (L41716); completed native candidates survive that update.
            // Verified by Fixtures/Grass/lifecycle.json. Arbitrary wall/config generations
            // still require their own explicit invalidation contract, never an implicit extra G4 draw.
            if(active.ContainsKey(region))return;
            var cells=new GrassSurfaceSnapshot[data.Cells.Count];
            for(int i=0;i<cells.Length;i++)
            {
                var cell=data.Cells[i];var closest=road.Midline.FindCoarse(cell.Candidates[0]);
                if(!GrassSurfaceSnapshot.TryCapture(road,fields,data,region.Residency,cell,closest,walls,region.LastSeen-terrain.Schedule.S5,out cells[i],true))
                    throw new InvalidOperationException("Grass cell source is not stable at the T4 commit barrier");
            }
            if(geometry.TryGetValue(region,out var previous))queuedCells-=previous.Cells.Length;
            var job=new Job{Region=region,Cells=cells};geometry[region]=job;queuedCells+=cells.Length;Enqueue(job);
        }
        void Lifecycle(TerrainRegionEvent e)
        {
            if(!e.Chunk||e.Action!=TerrainRegionAction.PrepareFine)return;
            requested.Add(e.Region);if(geometry.TryGetValue(e.Region,out var job))Enqueue(job);
        }
        bool Current(Job job)=>!job.Region.Retired&&geometry.TryGetValue(job.Region,out var latest)&&ReferenceEquals(latest,job)&&job.Cells[0].ParentCurrent;
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(GrassStreamingSession));if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));
            if(fields.HasQueuedWork||!road.AtTickBoundary||terrain.Building||!walls.QueryReady)return 0;int used=0;
            while(used<budget)
            {
                if(current==null)
                {
                    while(queue.Count>0&&!Current(queue.Peek()))queue.Dequeue();
                    if(!PriorityTurn)break;current=queue.Dequeue();current.Observer=road.Midline.State.ObserverIndex;lease=random.Shared.Claim(this);checkpoint=random.Capture();
                }
                if(!Current(current)){Cancel();continue;}
                if(builder==null)
                {
                    if(!current.Cells[current.Cell].TryBindWalls(walls,out var bound)){Cancel();continue;}
                    builder=new GrassCandidateBuilder(bound.Input,config,random,this);
                }
                used+=builder.Advance(1);if(!builder.Complete)continue;
                var cell=current.Cells[current.Cell].Input;
                foreach(var candidate in builder.Candidates)current.Instances.Add(new((int)Math.Floor(cell.WorldX/10),(int)Math.Floor(cell.WorldZ/10),candidate));
                builder.Dispose();builder=null;current.Cell++;
                if(current.Cell<current.Cells.Length)continue;
                var result=new GrassChunkData(current.Cells[0].Owner,current.Cells[0].TerrainRevision,current.Instances,checkpoint[0],random.Shared.Capture(),current.Observer);
                active[current.Region]=result;geometry.Remove(current.Region);queuedCells-=current.Cells.Length;CompletedCount++;current=null;checkpoint=null;lease.Dispose();lease=null;Committed?.Invoke(result);
            }
            return used;
        }
        void Cancel()
        {
            builder?.Dispose();builder=null;if(checkpoint!=null)random.Restore(checkpoint);checkpoint=null;
            lease?.Dispose();lease=null;current=null;
        }
        void Release(TerrainRegionState region)
        {
            if(ReferenceEquals(current?.Region,region))Cancel();if(geometry.Remove(region,out var job))queuedCells-=job.Cells.Length;requested.Remove(region);
            if(active.Remove(region,out var data))Released?.Invoke(data.Owner);
        }
        void Reset()
        {
            Cancel();foreach(var value in active.Values)Released?.Invoke(value.Owner);active.Clear();geometry.Clear();queuedCells=0;requested.Clear();queue.Clear();
            if(generation!=road.Midline.State.Generation||!ReferenceEquals(random.Shared,fields.Random.Shared))
            {generation=road.Midline.State.Generation;random=new(fields.Random.Shared,road.TerrainConfig.CellStep);}
            Cleared?.Invoke();
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;terrain.RegionCommitted-=Capture;terrain.Lifecycle-=Lifecycle;terrain.Released-=Release;terrain.Reset-=Reset;
            if(ReferenceEquals(terrain.ContentWorkPending,ownPending))terrain.ContentWorkPending=previousPending;Reset();
        }
    }
}
