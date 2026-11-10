using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Road;
using SlowRoads.Generation.Spatial;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Terrain;

namespace SlowRoads.Generation.Contents.Walls
{
    public readonly struct WallId : IEquatable<WallId>
    {
        public readonly int Start,End;public readonly HillsWallType Type;public readonly long Offset;
        public WallId(WallSegment s){Start=s.Start;End=s.End;Type=s.Type;Offset=BitConverter.DoubleToInt64Bits(s.Distance);}
        public bool Equals(WallId x)=>Start==x.Start&&End==x.End&&Type==x.Type&&Offset==x.Offset;
        public override bool Equals(object o)=>o is WallId x&&Equals(x);public override int GetHashCode()=>HashCode.Combine(Start,End,Type,Offset);
        public override string ToString()=>$"{Type}:{Start}:{End}:{Offset:x16}";
    }
    public sealed class StreamedWall
    {
        public readonly WallId Id;public WallSegment Segment {get;internal set;}public WallGeometryData Geometry {get;internal set;}
        public int Generation {get;internal set;}public long ContentRevision {get;internal set;}
        internal bool Stale,Seen,Queued,NeedsMesh=true;public bool Visible {get;internal set;}
        internal StreamedWall(WallSegment s,int generation,long revision){Id=new WallId(s);Segment=s;Generation=generation;ContentRevision=revision;}
    }
    /// <summary>G7's ordered rule/geometry queues, with versioned C2 invalidation.
    /// Existing road leases protect staged geometry; C5 reads logical segments independently of GPU visibility.</summary>
    public sealed class WallStreamingSession : IDisposable
    {
        readonly RoadStreamingSession road;readonly ContentAttributeStore attributes;
        readonly Dictionary<WallId,StreamedWall> active=new();readonly Queue<StreamedWall> geometry=new();readonly List<WallId> remove=new();
        readonly SortedDictionary<int,WallSegmentBuilder.State[]> checkpoints=new();
        WallSegmentBuilder[] rules;readonly int maxLength,display,horizon;readonly double retire;
        int generation,scan,dirty=int.MaxValue;bool disposed,revising,initialized;
        StreamedWall building;TerrainBuildContext context;TerrainQueryWindow window;
        Func<bool> buildStep;Func<WallGeometryData> buildData;DrystoneWallRandom random=new();
        readonly HillsRoadBlend blend;
        public event Action<StreamedWall> Committed;public event Action<StreamedWall> Changed;public event Action<WallId> Released;
        public IEnumerable<StreamedWall> Active=>active.Values;public int Count=>active.Count;public bool Building=>building!=null;
        public long Revision {get;private set;}
        public ContentVersion ContentVersion=>attributes.Version;
        public bool QueryReady=>!revising&&dirty==int.MaxValue;
        public WallStreamingSession(RoadStreamingSession road,ContentAttributeStore attributes)
        {
            this.road=road;this.attributes=attributes;maxLength=new[]{20,25,30,30,30,30}[road.Config.ViewDistance];display=new[]{50,80,100,120,140,160}[road.Config.ViewDistance];
            horizon=display+maxLength+(maxLength/2-4)+2;retire=display*.25;blend=new HillsRoadBlend(road.Config.Mesh.LaneStyle);
            generation=road.Midline.State.Generation;InitializeRules((road.Midline.State.Head?.Index??0)+1);attributes.Changed+=AttributesChanged;road.Invalidated+=Reset;
        }
        double Offset(int index,bool left)=>attributes.Read(new ContentAttributeKey(index,0,left?ContentChannel.SidewallLeft:ContentChannel.SidewallRight));
        void InitializeRules(int start)
        {
            scan=start;bool narrow=road.Config.Mesh.LaneStyle==HillsLaneStyle.Single||road.Config.Mesh.LaneStyle==HillsLaneStyle.OffRoad;
            bool singleOffroad=road.Config.Mesh.LaneStyle==HillsLaneStyle.OffRoad;
            rules=new WallSegmentBuilder[4];var types=new[]{HillsWallType.Bridge,HillsWallType.Lowland,HillsWallType.Barrier,HillsWallType.Drystone};
            for(int i=0;i<4;i++)rules[i]=new WallSegmentBuilder(types[i],start,road.Midline.FindCoarse,Offset,road.Config.Mesh.HalfWidth,maxLength,road.TerrainConfig.Detail,narrow,singleOffroad);
            checkpoints.Clear();SaveCheckpoint();
        }
        void SaveCheckpoint(){var state=new WallSegmentBuilder.State[4];for(int i=0;i<4;i++)state[i]=rules[i].Capture();checkpoints[scan]=state;}
        void AttributesChanged(IReadOnlyCollection<ContentAttributeKey> keys)
        {foreach(var k in keys)if((k.Channel==ContentChannel.SidewallLeft||k.Channel==ContentChannel.SidewallRight)&&k.X<=scan)dirty=Math.Min(dirty,k.X);}
        WallSegment Resolve(WallSegment s)
        {
            if(!active.TryGetValue(new WallId(s),out var record))return s;
            record.Segment=s;record.NeedsMesh=true;Queue(record);return s;
        }
        void Rewind()
        {
            int at=Math.Max(road.Midline.State.Head.Index+1,dirty-maxLength-4),selected=int.MinValue;WallSegmentBuilder.State[] state=null;
            foreach(var pair in checkpoints)if(pair.Key<=at&&pair.Key>=road.Midline.State.Head.Index+1){selected=pair.Key;state=pair.Value;}
            if(state==null){InitializeRules(road.Midline.State.Head.Index+1);selected=scan;}
            else{scan=selected;for(int i=0;i<4;i++)rules[i].Restore(state[i],Resolve);var later=new List<int>();foreach(int key in checkpoints.Keys)if(key>scan)later.Add(key);foreach(int key in later)checkpoints.Remove(key);}
            foreach(var record in active.Values)if(record.Segment.End>=selected-4){record.Stale=true;record.Seen=false;}
            revising=true;dirty=int.MaxValue;
        }
        void Queue(StreamedWall wall){if(wall.Queued)return;wall.Queued=true;geometry.Enqueue(wall);}
        void Add(WallSegment segment)
        {
            if(segment==null)return;var id=new WallId(segment);
            if(segment.ContiguousStart)foreach(var previous in active.Values)
                if(previous.Segment.End==segment.Start&&previous.Segment.Type==segment.Type&&previous.Segment.Distance==segment.Distance)
                {previous.NeedsMesh=true;Queue(previous);}
            if(active.TryGetValue(id,out var record)){record.Segment=segment;record.Seen=true;record.Stale=false;record.NeedsMesh=true;record.ContentRevision=attributes.Version.Revision;}
            else{record=new StreamedWall(segment,generation,attributes.Version.Revision){Seen=true};active.Add(id,record);}
            Queue(record);Revision++;
        }
        double Roadside(double x,double z,CoarseNode n)
        {
            if(!RoadProjection.TryCreate(x,z,n,out var p))throw new InvalidOperationException("Wall roadside support unavailable");
            if((!road.Config.Mesh.OffRoad||window.FindCoarse(p.Index).Bridge)&&p.Distance<road.Config.Mesh.HalfWidth)return p.Y;
            return blend.Evaluate(road.ReadTerrainRaw(context,x,z),ref p,x,z);
        }
        bool BeginGeometry()
        {
            while(geometry.Count>0)
            {
                var record=geometry.Peek();
                if(!active.ContainsKey(record.Id)||!record.NeedsMesh){geometry.Dequeue();record.Queued=false;continue;}
                if(record.Segment.Start>road.Midline.State.ObserverIndex+display)return false;
                int first=Math.Max(road.Midline.State.Head.Index,record.Segment.Start-4),last=record.Segment.End+4;
                if(record.Segment.Start<=first||last>=road.Midline.AttributeReadyExclusive||last>=road.Midline.State.FineTail.Index)return false;
                if(road.TryCaptureTerrainWindow(first,last,new[]{record.Segment.Start},out window)!=RoadQueryStatus.Ready)return false;
                if(road.TryBeginTerrainBuild(new[]{new TerrainSampleRequest(window.FindCoarse(record.Segment.Start).Position.X,window.FindCoarse(record.Segment.Start).Position.Z)},0,0,null,out context)!=RoadQueryStatus.Ready)
                {window.Dispose();window=null;return false;}
                // C2's original coarse/fine shadow writes are composed without changing P2's producer nodes.
                for(var n=window.FindCoarse(first);n!=null;n=n.Next)
                {RestoreShadow(n,n.Index,0);for(var f=n.NextFine as FineNode;f!=null;f=f.NextFine as FineNode)RestoreShadow(f,n.Index,f.Ordinal);}
                var input=new WallGeometryInput(record.Segment,window.FindCoarse(record.Segment.Start),record.Segment.Type==HillsWallType.Drystone||road.Config.Mesh.OffRoad?Roadside:null,road.Config.Mesh.LaneStyle==HillsLaneStyle.OffRoad);
                switch(record.Segment.Type)
                {
                    case HillsWallType.Bridge:case HillsWallType.Lowland:var solid=new BridgeSideWallGeometryBuilder(input);buildStep=solid.Step;buildData=()=>solid.Data;break;
                    case HillsWallType.Drystone:var stone=new DrystoneWallGeometryBuilder(input,random);buildStep=stone.Step;buildData=()=>stone.Data;break;
                    case HillsWallType.Barrier:var barrier=new BarrierGeometryBuilder(input,road.TerrainConfig.Detail);buildStep=barrier.Step;buildData=()=>barrier.Data;break;
                    default:var fence=new FenceGeometryBuilder(input,road.TerrainConfig.Seed);buildStep=fence.Step;buildData=()=>fence.Data;break;
                }
                geometry.Dequeue();record.Queued=false;building=record;return true;
            }
            return false;
        }
        void RestoreShadow(MidlinePathNode n,int index,int ordinal)
        {var left=road.ContentShadowProvider?.Invoke(index,ordinal,true);var right=road.ContentShadowProvider?.Invoke(index,ordinal,false);if(left.HasValue)n.ShadowLeft=left.Value;if(right.HasValue)n.ShadowRight=right.Value;}
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(WallStreamingSession));if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));if(budget==0)return 0;
            if(!road.Ready||!road.AtTickBoundary)return 0;if(!initialized||generation!=road.Midline.State.Generation)Reset();int used=0;
            if(building==null&&dirty!=int.MaxValue)Rewind();
            while(used<budget)
            {
                if(building!=null)
                {
                    used++;context.ReportProgress();if(!buildStep())continue;
                    if(!road.IsTerrainBuildCurrent(context))throw new InvalidOperationException("Stale wall publication");
                    building.Geometry=buildData();building.NeedsMesh=false;Revision++;Committed?.Invoke(building);EndGeometry();continue;
                }
                if(scan<road.Midline.State.ObserverIndex+horizon&&scan+maxLength+4<road.Midline.AttributeReadyExclusive)
                {
                    scan++;foreach(var rule in rules)Add(rule.Generate(scan,true));foreach(var rule in rules)Add(rule.Generate(scan,false));SaveCheckpoint();used++;continue;
                }
                if(revising)
                {remove.Clear();foreach(var pair in active)if(pair.Value.Stale&&!pair.Value.Seen)remove.Add(pair.Key);foreach(var id in remove){active.Remove(id);Revision++;Released?.Invoke(id);}revising=false;}
                if(!BeginGeometry())break;
            }
            Retire();return used;
        }
        void EndGeometry(){context?.Dispose();context=null;window?.Dispose();window=null;building=null;buildStep=null;buildData=null;}
        void Retire()
        {
            int observer=road.Midline.State.ObserverIndex;remove.Clear();
            foreach(var pair in active)
            {var w=pair.Value;if(w.Segment.End+retire<observer){remove.Add(pair.Key);continue;}bool visible=w.Segment.Start<=observer+display;
                if(w.Visible!=visible){w.Visible=visible;Changed?.Invoke(w);}}
            foreach(var id in remove){active.Remove(id);Revision++;Released?.Invoke(id);}
            var old=new List<int>();foreach(int i in checkpoints.Keys)if(i<road.Midline.State.Head.Index+1)old.Add(i);foreach(int i in old)checkpoints.Remove(i);
        }
        public WallQuerySnapshot Snapshot()
        {
            if(!QueryReady)throw new InvalidOperationException("C2 wall revision is still rebuilding");var segments=new List<WallSegment>();foreach(var w in active.Values)segments.Add(new WallSegment(w.Segment.Start,w.Segment.End,w.Segment.Type,w.Segment.Distance,w.Segment.ContiguousStart,w.Segment.ContiguousEnd));
            var surfaces=new List<WallSurfaceSnapshot>();foreach(var w in active.Values)if(w.Geometry!=null&&!w.NeedsMesh)surfaces.Add(new WallSurfaceSnapshot(w));
            return new WallQuerySnapshot(generation,attributes.Version,Revision,segments,surfaces);
        }
        /// <summary>G7 L47416–47425. C5's per-cell query avoids cloning every wall mesh.
        /// Call only at the existing content barrier; display visibility is irrelevant.</summary>
        public bool TryGetSides(int index,out double left,out double right,out int side)
        {
            left=right=0;side=0;if(disposed||!QueryReady)return false;
            foreach(var w in active.Values)
            {
                var s=w.Segment;if(index<s.Start||index>=s.End)continue;
                if(s.Distance<0){double d=-s.Distance;if(left==0||left>d)left=d;}
                else if(right==0||right>s.Distance)right=s.Distance;
            }
            side=left!=0?(right!=0?0:1):(right!=0?-1:0);return true;
        }
        void Reset(){EndGeometry();foreach(var id in active.Keys)Released?.Invoke(id);active.Clear();geometry.Clear();Revision++;generation=road.Midline.State.Generation;random=new DrystoneWallRandom();dirty=int.MaxValue;revising=false;InitializeRules((road.Midline.State.Head?.Index??0)+1);initialized=road.Ready;}
        public void Dispose(){if(disposed)return;disposed=true;attributes.Changed-=AttributesChanged;road.Invalidated-=Reset;Reset();}
    }
    public sealed class WallQuerySnapshot
    {
        public readonly int Generation;public readonly ContentVersion Version;public readonly IReadOnlyList<WallSegment> Segments;
        public readonly long Revision;public readonly IReadOnlyList<WallSurfaceSnapshot> Surfaces;
        internal WallQuerySnapshot(int generation,ContentVersion version,long revision,List<WallSegment> segments,List<WallSurfaceSnapshot> surfaces)
        {Generation=generation;Version=version;Revision=revision;Segments=segments.AsReadOnly();Surfaces=surfaces.AsReadOnly();}
        public bool IsCurrent(ContentVersion version)=>Version.Equals(version);
        public bool IsCurrent(WallStreamingSession owner)=>owner.QueryReady&&Revision==owner.Revision&&Version.Equals(owner.ContentVersion);
        // Exact G7.addLeft/Right minimum-offset aggregation. C5 supplies its existing road projection.
        public void Sides(int index,out double left,out double right)
        {left=right=0;foreach(var s in Segments)if(index>=s.Start&&index<s.End){if(s.Distance<0){double d=-s.Distance;if(left==0||left>d)left=d;}else if(right==0||right>s.Distance)right=s.Distance;}}
    }
    /// <summary>C5 can derive the original wall-edge height from these committed CPU values.
    /// Missing future geometry remains absent until built; no GPU readback or guessed constant height.</summary>
    public sealed class WallSurfaceSnapshot
    {
        public readonly WallId Id;public readonly Double3 Origin;
        public readonly IReadOnlyList<float> Positions;public readonly IReadOnlyList<int> Indices;
        internal WallSurfaceSnapshot(StreamedWall wall)
        {Id=wall.Id;Origin=wall.Geometry.Origin;Positions=Array.AsReadOnly((float[])wall.Geometry.Positions.Clone());Indices=Array.AsReadOnly((int[])wall.Geometry.Indices.Clone());}
    }
}
