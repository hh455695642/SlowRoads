using System;
using System.Collections.Generic;
using SlowRoads.Generation.Contents.Trees;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Spatial;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Terrain;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Serial Ka jobs after C1 far-candidate commit, before the next terrain/road work unit.
    /// Holds the existing producer barrier; no independent road index, clock, tree store or GPU renderer.</summary>
    public sealed class FieldStreamingSession : IDisposable
    {
        sealed class Region
        {
            internal TerrainRegionState Terrain;internal long Source;internal FieldGenerationJob Job;
            internal readonly List<TreeId> Trees=new();internal Dictionary<ContentAttributeKey,double> Patch=new();
            internal int? Tail;internal bool Queued;
        }
        readonly RoadStreamingSession road;readonly TerrainStreamingSession terrain;readonly TreeStreamingSession trees;
        readonly Dictionary<TerrainRegionState,Region> regions=new();readonly Queue<Region> pending=new();
        readonly List<(long source,int last)> retiredRoad=new();
        readonly Queue<Region> revisit=new();
        FieldTileRegistry registry;FieldQualityNoise quality=new();FieldRandomState random;long sequence;int owner;
        FieldExecutionState execution=new();
        readonly int detail,density;bool disposed;
        Region current;TerrainBuildContext context;TerrainQueryWindow window;FieldContentTransaction transaction;World world;
        public event Action<TerrainRegionState,FieldBoundaryRecipe> Committed;
        public event Action<TerrainRegionState> Released;
        public FieldRandomState Random=>random;
        public int ResidentRegions=>regions.Count;public int SharedFieldTiles=>registry.LiveCount;
        public int RetainedRoadNodes=>registry.RoadHistory.Count;
        public bool HasWork=>current!=null||pending.Count>0||revisit.Count>0;
        public long CompletedCount {get;private set;}
        public bool TrySnapshot(out FieldQuerySnapshot snapshot)
        {
            snapshot=null;if(disposed||HasWork||!road.AtTickBoundary)return false;
            var plots=new List<FieldPlotSnapshot>();var boundaries=new List<FieldBoundaryEvent>();var extras=new List<FieldTree>();
            foreach(var r in regions.Values)if(r.Job?.Complete==true)
            {foreach(var c in r.Job.Candidates)plots.Add(new FieldPlotSnapshot(c,registry.Template));boundaries.AddRange(r.Job.Recipe.Events);extras.AddRange(r.Job.Recipe.Trees);}
            snapshot=new FieldQuerySnapshot(trees.Attributes.Version,plots,boundaries,extras,trees.Attributes.Snapshot());return true;
        }
        public FieldStreamingSession(RoadStreamingSession road,TerrainStreamingSession terrain,TreeStreamingSession trees,int density)
        {
            this.road=road;this.terrain=terrain;this.trees=trees;this.density=density;detail=road.TerrainConfig.Detail;
            registry=new FieldTileRegistry(new[]{700,800,1000,1000,1000,1000}[road.Config.ViewDistance],road.Midline.State.Generation);random=new FieldRandomState(road.TerrainConfig.Seed);
            trees.TileCommitted+=Enqueue;terrain.Released+=Release;terrain.Reset+=Reset;terrain.Lifecycle+=Lifecycle;terrain.ContentWorkPending=()=>HasWork;
            trees.Attributes.Changed+=AttributesChanged;
            road.ContentShadowProvider=(index,ordinal,left)=>trees.Attributes.TryRead(new ContentAttributeKey(index,ordinal,left?ContentChannel.RoadShadowLeft:ContentChannel.RoadShadowRight),out double value)?value:(double?)null;
            road.ContentSidewallProvider=(index,left)=>trees.Attributes.Read(new ContentAttributeKey(index,0,left?ContentChannel.SidewallLeft:ContentChannel.SidewallRight));
        }
        void AttributesChanged(IReadOnlyCollection<ContentAttributeKey> keys)
        {
            int first=int.MaxValue,last=int.MinValue;
            foreach(var k in keys)if(k.Channel>=ContentChannel.SidewallLeft){first=Math.Min(first,k.X);last=Math.Max(last,k.X);}
            if(first<=last)terrain.InvalidateContent(first,last);
        }
        void Enqueue(TerrainRegionState terrainRegion,TreeTileCandidates _)
        {
            if(registry.Generation!=road.Midline.State.Generation)Reset();
            var r=new Region{Terrain=terrainRegion,Source=0x2000000000000000L+ ++sequence,Queued=true};regions.Add(terrainRegion,r);pending.Enqueue(r);
        }
        int? Tail(TerrainRegionState r)
        {var segments=road.Spatial?.GetSegmentsForTile(r.X,r.Z);return segments!=null&&segments.Count>0?segments[0].Tail:null;}
        // Called only at the existing tick boundary, in terrain creation order. Displays never enqueue work.
        public void CheckReclips()
        {
            if(disposed||!road.AtTickBoundary||current!=null||pending.Count>0||terrain.Building)return;
            for(int i=retiredRoad.Count-1;i>=0;i--)if(retiredRoad[i].last<road.Midline.State.Head.Index)
            {trees.Attributes.Apply(trees.Attributes.Version,retiredRoad[i].source,new Dictionary<ContentAttributeKey,double>());retiredRoad.RemoveAt(i);}
            while(revisit.Count>0){var r=revisit.Dequeue();if(regions.ContainsKey(r.Terrain)&&!r.Terrain.Retired&&!r.Queued&&r.Job!=null&&Tail(r.Terrain)!=r.Tail)
                {r.Queued=true;pending.Enqueue(r);}}
        }
        void Lifecycle(TerrainRegionEvent e)
        {
            // i7.customDisplay/customUpgrade -> Ka.checkRegenerate. A changed Ca tail by itself
            // does not schedule a reclip; per-tick polling would consume extra random values.
            if(!e.Chunk&&(e.Action==TerrainRegionAction.Display||e.Action==TerrainRegionAction.Fine)&&regions.TryGetValue(e.Region,out var r))revisit.Enqueue(r);
        }
        bool Begin()
        {
            while(pending.Count>0)
            {
                current=pending.Dequeue();if(!regions.ContainsKey(current.Terrain)||current.Terrain.Retired){current=null;continue;}
                if(detail==0){current.Queued=false;current.Tail=Tail(current.Terrain);current=null;continue;}
                if(road.TryCaptureContentWindow(out window)!=RoadQueryStatus.Ready){pending.Enqueue(current);current=null;return false;}
                if(road.TryBeginTerrainBuild(new[]{new TerrainSampleRequest(current.Terrain.X*(double)road.Config.TileSize,current.Terrain.Z*(double)road.Config.TileSize)},0,0,null,out context)!=RoadQueryStatus.Ready)
                {window.Dispose();window=null;pending.Enqueue(current);current=null;return false;}
                trees.Density.SetHeadLayers(context.NegativeXLayer,context.NonNegativeXLayer);
                registry.RoadHistory.Capture(window,registry.MinimumRoadAnchor);
                transaction=new FieldContentTransaction(trees.Store,trees.Attributes,current.Source,current.Trees);transaction.RetainRoadPatch(current.Patch);
                world=new World(this,transaction,window,context);
                int originalOwner=owner++;
                if(current.Job==null)
                {if(owner>1000)owner=0;current.Job=new FieldGenerationJob(registry,random,quality,world,current.Terrain.X,current.Terrain.Z,originalOwner,road.Config.TileSize,road.Config.Mesh.HalfWidth,detail,density,detail>=2,execution);}
                else current.Job.Reclip(world,originalOwner);
                current.Tail=Tail(current.Terrain);return true;
            }
            return false;
        }
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(FieldStreamingSession));if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));if(budget==0)return 0;
            if(!road.AtTickBoundary||terrain.Building)return 0;int used=0;
            while(used<budget)
            {
                if(current==null&&!Begin())break;
                int step=current.Job.Advance(1);used+=step;context.ReportProgress();
                if(!current.Job.Complete)continue;
                if(!road.IsTerrainBuildCurrent(context)||!transaction.Commit())throw new InvalidOperationException("Field publication lost its content/road token");
                current.Trees.Clear();foreach(var candidate in transaction.Additions)current.Trees.Add(candidate.Id);
                current.Patch=new Dictionary<ContentAttributeKey,double>(transaction.Patch);current.Job.MarkPublished();current.Queued=false;CompletedCount++;
                var region=current.Terrain;var recipe=current.Job.Recipe;EndContext();current=null;Committed?.Invoke(region,recipe);
            }
            return used;
        }
        void EndContext(){context?.Dispose();context=null;window?.Dispose();window=null;transaction=null;world=null;}
        public bool CanTraverse(Double3 position)
        {
            if(detail==0)return true;int x=(int)Math.Floor(position.X/road.Config.TileSize),z=(int)Math.Floor(position.Z/road.Config.TileSize);
            foreach(var r in regions.Values)if(r.Terrain.X==x&&r.Terrain.Z==z&&!r.Terrain.Retired)return r.Job!=null&&r.Job.Complete&&!r.Queued;
            return false;
        }
        void Release(TerrainRegionState region)
        {
            if(!regions.Remove(region,out var r))return;
            if(ReferenceEquals(r,current)){transaction?.Cancel();EndContext();current=null;}
            r.Job?.Dispose();trees.Store.ApplyExternal(trees.Store.Generation,trees.Store.Revision,r.Source,Array.Empty<TreeCandidate>(),r.Trees);
            // Terrain colours disappear with their region. Signed road markings retain their source lifetime
            // until the road no longer references this interval; Reset releases the complete generation.
            var roadPatch=new Dictionary<ContentAttributeKey,double>();foreach(var p in r.Patch)if(p.Key.Channel>=ContentChannel.SidewallLeft)roadPatch.Add(p.Key,p.Value);
            trees.Attributes.Apply(trees.Attributes.Version,r.Source,roadPatch);Released?.Invoke(region);
            if(roadPatch.Count>0){int last=int.MinValue;foreach(var key in roadPatch.Keys)last=Math.Max(last,key.X);retiredRoad.Add((r.Source,last));}
        }
        void Reset()
        {
            bool fresh=registry.Generation!=road.Midline.State.Generation;
            transaction?.Cancel();EndContext();current=null;
            foreach(var r in regions.Values)
            {
                r.Job?.Dispose();trees.Store.ApplyExternal(trees.Store.Generation,trees.Store.Revision,r.Source,Array.Empty<TreeCandidate>(),r.Trees);
                trees.Attributes.Apply(trees.Attributes.Version,r.Source,new Dictionary<ContentAttributeKey,double>());Released?.Invoke(r.Terrain);
            }
            foreach(var old in retiredRoad)trees.Attributes.Apply(trees.Attributes.Version,old.source,new Dictionary<ContentAttributeKey,double>());
            regions.Clear();pending.Clear();revisit.Clear();retiredRoad.Clear();registry.Dispose();
            registry=new FieldTileRegistry(new[]{700,800,1000,1000,1000,1000}[road.Config.ViewDistance],road.Midline.State.Generation);quality=new FieldQualityNoise();
            // Fresh/warm reset follows the current content session contract. C5 shares this stream instance.
            if(fresh){random=new FieldRandomState(road.TerrainConfig.Seed);execution=new FieldExecutionState();sequence=0;owner=0;}
        }
        public void Dispose()
        {if(disposed)return;disposed=true;trees.TileCommitted-=Enqueue;trees.Attributes.Changed-=AttributesChanged;terrain.Released-=Release;terrain.Reset-=Reset;terrain.Lifecycle-=Lifecycle;terrain.ContentWorkPending=null;road.ContentShadowProvider=null;road.ContentSidewallProvider=null;Reset();registry.Dispose();}
        sealed class World : IFieldWorld
        {
            readonly FieldStreamingSession session;readonly FieldContentTransaction transaction;readonly TerrainQueryWindow window;readonly TerrainBuildContext context;readonly HillsRoadBlend blend;
            Dictionary<int,CoarseNode> oldNodes;
            internal World(FieldStreamingSession session,FieldContentTransaction transaction,TerrainQueryWindow window,TerrainBuildContext context)
            {
                this.session=session;this.transaction=transaction;this.window=window;this.context=context;blend=new HillsRoadBlend(session.road.Config.Mesh.LaneStyle);
                for(var c=window.FindCoarse(window.FirstIndex);c!=null;c=c.Next)
                {
                    Restore(c,c.Index,0);MidlinePathNode n=c.NextFine;
                    while(n!=null&&!ReferenceEquals(n,c.Next)){var f=(FineNode)n;Restore(n,c.Index,f.Ordinal);n=n.NextFine;}
                }
            }
            void Restore(MidlinePathNode n,int index,int ordinal)
            {if(session.trees.Attributes.TryRead(new ContentAttributeKey(index,ordinal,ContentChannel.RoadShadowLeft),out double left)){n.ShadowLeft=left;n.HasShadowLeft=true;}
                if(session.trees.Attributes.TryRead(new ContentAttributeKey(index,ordinal,ContentChannel.RoadShadowRight),out double right)){n.ShadowRight=right;n.HasShadowRight=true;}}
            public CoarseNode ClosestNode(int tx,int tz,double x,double z,double radius)
            {var status=session.road.Spatial.GetClosestNodeForTile(tx,tz,x,z,out int index,out _,radius);return status==RoadQueryStatus.Ready?(Node(index)??throw new InvalidOperationException("Ca selected outside bounded field support: "+index)):null;}
            public bool CloseToRoad(int tx,int tz,double x,double z,double radius)=>session.road.Spatial.IsCloseToRoad(tx,tz,x,z,radius);
            public CoarseNode Node(int index)
            {
                var node=window.FindCoarse(index);if(node!=null)return node;
                if(index>=window.FirstIndex)return null;
                if(oldNodes==null)
                {
                    oldNodes=session.registry.RoadHistory.Prefix(window,Math.Min(index,session.registry.MinimumRoadAnchor));
                    foreach(var pair in oldNodes){Restore(pair.Value,pair.Key,0);for(var f=pair.Value.NextFine as FineNode;f!=null;f=f.NextFine as FineNode)Restore(f,pair.Key,f.Ordinal);}
                }
                return oldNodes.TryGetValue(index,out node)?node:null;
            }
            public double RawHeight(double x,double z)=>session.road.ReadTerrainRaw(context,x,z);
            public double RoadsideHeight(double x,double z,CoarseNode n)
            {
                if(!RoadProjection.TryCreate(x,z,n,out var p))throw new InvalidOperationException("Ka roadside projection lacks support");
                if((!session.road.Config.Mesh.OffRoad||Node(p.Index).Bridge)&&p.Distance<session.road.Config.Mesh.HalfWidth)return p.Y;
                // Hills Pa (Settings u3) is true: negative raw heights are passed through to Ll.
                return blend.Evaluate(RawHeight(x,z),ref p,x,z);
            }
            public double Density(double x,double z)=>session.trees.Density.GetDensityAt(x,z);
            public double Sidewall(int index,bool left)=>transaction.ReadSidewall(index,left);
            public void WriteSidewall(FieldId field,int index,bool left,double offset)=>transaction.Sidewall(index,left,offset);
            public bool AddTree(in FieldTree tree)=>transaction.Add(tree,session.road.Config.TileSize);
            public void WriteColour(FieldId field,double x,double z,double colour)=>transaction.Colour(x,z,colour);
            public void WriteShadow(FieldId field,CoarseNode n,double side,double value)
            {
                bool left=side>0;double current=left?(n.HasShadowLeft?n.ShadowLeft:0):(n.HasShadowRight?n.ShadowRight:0);double shadow=Math.Max(current,value);Set(n,left,shadow);
                if(n.Index>session.road.Midline.State.FineTail.Index)return;
                if(n.Previous!=null&&n.Previous.HasFineSegments)Interpolate(n.Previous,n,left);
                if(n.HasFineSegments&&n.Next!=null)Interpolate(n,n.Next,left);
            }
            void Set(MidlinePathNode n,bool left,double value)
            {if(left){n.ShadowLeft=value;n.HasShadowLeft=true;}else{n.ShadowRight=value;n.HasShadowRight=true;}
                if(n is CoarseNode c)transaction.SetRoadShadow(c.Index,0,left,value);else{var f=(FineNode)n;transaction.SetRoadShadow(f.PreviousCoarse.Index,f.Ordinal,left,value);}}
            void Interpolate(CoarseNode a,CoarseNode b,bool left)
            {
                MidlinePathNode n=a.NextFine;int count=a.FineSegments+1;
                for(int i=1;i<count;i++){if(n==null)throw new InvalidOperationException("Ka shadow fine support missing");double t=(double)i/count;
                    double end=left?(b.HasShadowLeft?b.ShadowLeft:double.NaN):(b.HasShadowRight?b.ShadowRight:double.NaN);
                    double start=left?(a.HasShadowLeft?a.ShadowLeft:double.NaN):(a.HasShadowRight?a.ShadowRight:double.NaN);
                    Set(n,left,end*t+start*(1-t));n=n.NextFine;}
            }
        }
    }
}
