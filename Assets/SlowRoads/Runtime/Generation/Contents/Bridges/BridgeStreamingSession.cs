using System;
using System.Collections.Generic;
using SlowRoads.Generation.Streaming;

namespace SlowRoads.Generation.Contents.Bridges
{
    public sealed class StreamedBridge
    {
        public readonly int Id,Generation;public readonly BridgeGeometryBuilder Builder;
        public BridgeGeometryData Geometry=>Builder.Data;public bool Visible {get;internal set;}public bool Near {get;internal set;}
        internal StreamedBridge(BridgeGeometryBuilder builder){Builder=builder;Id=builder.Data.Input.Head.Index;Generation=builder.Data.Input.Generation;}
    }
    /// <summary>J9 L49027–49133. Consumes P2's bridge flag and stable P4 normals; never selects bridge sites.
    /// The finite input is retained until its incremental ii build commits or is cancelled.</summary>
    public sealed class BridgeStreamingSession : IDisposable
    {
        readonly RoadStreamingSession road;readonly Dictionary<int,StreamedBridge> active=new();readonly List<int> retired=new();
        readonly int lookahead,segments;int generation,scan;BridgeGeometryBuilder building;bool disposed,initialized;
        public event Action<StreamedBridge> Committed;public event Action<StreamedBridge> Changed;public event Action<int> Released;
        public IEnumerable<StreamedBridge> Active=>active.Values;public int Count=>active.Count;
        public BridgeStreamingSession(RoadStreamingSession road)
        {
            this.road=road??throw new ArgumentNullException(nameof(road));lookahead=new[]{50,90,160,200,300,300}[road.Config.ViewDistance];segments=new[]{4,6,8,10,10}[road.TerrainConfig.Detail];
            generation=road.Midline.State.Generation;scan=road.Midline.State.Head?.Index??0;road.Invalidated+=Reset;
        }
        bool Prepare(int index)
        {
            var head=road.Midline.FindCoarse(index);if(head==null||!head.Bridge)return false;var tail=head;
            while(tail.Next!=null&&tail.Next.Bridge){tail=tail.Next;if(tail.Index-index>4090)throw new InvalidOperationException("Bridge exceeds bounded support");}
            if(tail.Next==null||tail.Next.Bridge||tail.Next.Index>=road.Midline.AttributeReadyExclusive)return false;
            var points=new BridgePoint[tail.Index-index+2];var node=head;
            for(int i=0;i<points.Length;i++){points[i]=new BridgePoint(node.Index,node.Position,node.Normal,node.Bridge);node=node.Next;}
            var input=new BridgeInput(points,generation,road.Midline.Revision);
            building=new BridgeGeometryBuilder(input,road.Config.Mesh.HalfWidth,segments,true,lookahead);scan=tail.Next.Index;return true;
        }
        public int Advance(int budget)
        {
            if(disposed)throw new ObjectDisposedException(nameof(BridgeStreamingSession));if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));if(budget==0)return 0;
            if(!road.Ready||!road.AtTickBoundary)return 0;if(!initialized||generation!=road.Midline.State.Generation)Reset();int used=0;
            while(used<budget)
            {
                if(building!=null)
                {
                    used++;if(!building.Step())continue;
                    if(building.Data.Input.Generation!=generation)throw new InvalidOperationException("Stale bridge publication");
                    var record=new StreamedBridge(building);building=null;active.Add(record.Id,record);Committed?.Invoke(record);continue;
                }
                if(scan>=road.Midline.State.ObserverIndex+lookahead)break;
                var n=road.Midline.FindCoarse(scan+1);if(n==null||n.Index>=road.Midline.AttributeReadyExclusive)break;
                if(n.Bridge){if(!Prepare(n.Index))break;}else scan=n.Index;used++;
            }
            UpdateVisibility();return used;
        }
        void UpdateVisibility()
        {
            int observer=road.Midline.State.ObserverIndex;retired.Clear();
            foreach(var pair in active)
            {
                var bridge=pair.Value;var builder=bridge.Builder;
                if(observer>=builder.RetireIndex){retired.Add(pair.Key);continue;}
                bool visible=observer>=builder.DisplayIndex,near=observer>=builder.UpgradeIndex&&observer<builder.DowngradeIndex;
                if(bridge.Visible!=visible||bridge.Near!=near){bridge.Visible=visible;bridge.Near=near;Changed?.Invoke(bridge);}
            }
            foreach(int id in retired){active.Remove(id);Released?.Invoke(id);}
        }
        void Reset()
        {building=null;foreach(int id in active.Keys)Released?.Invoke(id);active.Clear();generation=road.Midline.State.Generation;scan=road.Midline.State.Head?.Index??0;initialized=road.Ready;}
        public void Dispose(){if(disposed)return;disposed=true;road.Invalidated-=Reset;Reset();}
    }
}
