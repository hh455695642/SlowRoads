using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Roadside
{
    public enum HillsRoadSign {Left,Right,LeftDouble,RightDouble,Uphill15,Uphill20,Downhill15,Downhill20,ChevronLeft,ChevronRight}
    // Native _9 pools h4 objects. O7/P7 can still refer to a recycled h4: retain
    // that source alias in CPU logic, independently of Unity's presentation pool.
    internal sealed class RoadSignHandle {internal RoadSignRecord Current;internal bool InPool;}
    public sealed class RoadSignRecord
    {
        public readonly int Id,NodeIndex,CreatedIndex;public readonly double RetireIndex,Sink;public readonly Double3 Position;public readonly double Rotation;
        public HillsRoadSign Type {get;internal set;}public int Revision {get;internal set;}
        internal readonly RoadSignHandle Handle;
        internal RoadSignRecord(int id,HillsRoadSign type,int node,int created,double retire,Double3 position,double rotation,double sink=0,RoadSignHandle handle=null)
        {Id=id;Type=type;NodeIndex=node;CreatedIndex=created;RetireIndex=retire;Position=position;Rotation=rotation;Sink=sink;Handle=handle??new();Handle.Current=this;}
    }
    /// <summary>_9/O7/P7/F7 S L46637–47075. Consumes the existing coarse/fine graph.
    /// Each work unit advances one original root; no random numbers, scene objects or vehicle inputs.</summary>
    public sealed class RoadSignGenerator
    {
        readonly double width,retire;readonly int horizon;readonly Func<double,double,CoarseNode,double> roadside;
        readonly List<RoadSignRecord> records=new();CoarseNode root,lastSignNode;bool canMerge=true;int observer,serial;
        readonly Stack<RoadSignHandle> pool=new();readonly Dictionary<int,RoadSignHandle> live=new();
        CoarseNode turnHead,turnTail,hillHead,hillTail,chevHead,chevTail;
        double turnSum,turnThreshold=1.2,discard=.4,chevSum,chevThreshold=.28,chevNext=.12,chevPrior=.5;
        int turnHorizon=5,turnSpacing=10,turnMargin=3,lastTurnIndex=10,lastTurnDirection;bool lastTurnSingle;
        RoadSignRecord lastTurn,lastHill;bool onHill;int hillDir,hillSteep,lastHillIndex=-20,lastChev=-5;
        public IReadOnlyList<RoadSignRecord> Records=>records;
        public event Action<RoadSignRecord,bool> Changed;
        public int RootIndex=>root.Index;
        public RoadSignGenerator(CoarseNode initial,int style,double halfWidth,int lookahead,double retireMargin,
            Func<double,double,CoarseNode,double> roadsideHeight=null,bool deferInitialLookahead=false,Action<RoadSignRecord,bool> initialListener=null)
        {
            if(initial==null||lookahead<0||lookahead>200||retireMargin<0||halfWidth<=0)throw new ArgumentOutOfRangeException();
            width=halfWidth;horizon=lookahead;retire=retireMargin;roadside=roadsideHeight;root=initial;observer=initial.Index;
            Changed+=initialListener;
            turnHead=turnTail=initial;turnSum=initial.HeadingDelta;
            while(turnTail.Index<turnHead.Index+turnHorizon)Turn(initial);
            // Original O7 applies style overrides AFTER the initialization loop.
            if(style==1){turnThreshold=.5;turnHorizon=10;turnSpacing=20;turnMargin=6;}
            else if(style==3){turnThreshold=1.6;turnMargin=4;}
            hillHead=hillTail=initial;for(int i=0;i<10;i++)hillTail=Next(hillTail);
            chevHead=chevTail=initial;chevSum=Math.Abs(initial.HeadingDelta);
            if(style==1){chevThreshold=.2;chevNext=.1;chevPrior=.8;}
            else if(style==3){chevThreshold=.34;chevNext=.15;chevPrior=.6;}
            while(chevTail.Index<chevHead.Index+4)Chevron(initial);
            // Finite original initialization horizon; later catch-up uses explicit budgets.
            if(!deferInitialLookahead)Advance(observer,horizon);
        }
        static CoarseNode Next(CoarseNode n)=>n.Next??throw new InvalidOperationException("Road sign support is incomplete");
        public int Advance(int observerIndex,int budget)
        {
            if(budget<0||observerIndex<observer)throw new ArgumentOutOfRangeException();observer=observerIndex;int used=0;
            while(used<budget&&root.Index<observer+horizon){root=Next(root);Turn(root);Hill(root);Chevron(root);used++;}return used;
        }
        // Runtime history is captured by Changed before retirement; tests may retain the full list.
        public void DiscardHistoryThrough(double observerIndex)=>records.RemoveAll(r=>r.RetireIndex<=observerIndex);
        public void Retire(int id){if(live.Remove(id,out var handle)&&!handle.InPool){handle.InPool=true;pool.Push(handle);}}
        RoadSignRecord Place(MidlinePathNode node,HillsRoadSign type,bool merge=true,int side=-1,double sink=0,double offset=.6)
        {
            var coarse=node as CoarseNode;Double3 p=node.Position;
            if(lastSignNode!=null)
            {
                if(merge&&canMerge&&coarse!=null&&coarse.Index-lastSignNode.Index<3){node=lastSignNode;coarse=lastSignNode;p=node.Position;p=new(p.X,p.Y-.7,p.Z);}
                else if(coarse!=null&&coarse.Index==lastSignNode.Index){node=Next(coarse);coarse=(CoarseNode)node;p=node.Position;}
            }
            var owner=coarse??((FineNode)node).NextCoarse.Previous;
            double x=p.X+side*node.Normal.X*(width+offset),z=p.Z+side*node.Normal.Z*(width+offset);
            double y=roadside!=null?roadside(x,z,owner):p.Y;
            var handle=pool.Count>0?pool.Pop():new RoadSignHandle();handle.InPool=false;
            var record=new RoadSignRecord(serial++,type,owner.Index,observer,owner.Index+retire,new Double3(x,y-sink,z),-Math.PI/2-owner.Heading,sink,handle);live.Add(record.Id,handle);
            // P5 pools retired nodes. Keep the last placement's values rather than a stale
            // pooled reference; Next is only consulted for a recent same-node placement.
            records.Add(record);lastSignNode=new CoarseNode{Index=owner.Index,Position=owner.Position,Normal=owner.Normal,Heading=owner.Heading,Next=owner.Next};canMerge=merge;Changed?.Invoke(record,true);return record;
        }
        void Variant(RoadSignRecord reference,HillsRoadSign type){var r=reference.Handle.Current;r.Type=type;r.Revision++;Changed?.Invoke(r,false);}
        void Turn(CoarseNode n)
        {
            if(n.Index>turnHead.Index){turnSum-=turnHead.HeadingDelta;turnHead=Next(turnHead);}
            if(turnTail.Index>=turnHead.Index+turnHorizon)return;
            turnTail=Next(turnTail);turnSum+=turnTail.HeadingDelta;
            int direction=turnSum<0?1:-1;if(!(turnSum< -turnThreshold||turnSum>turnThreshold))return;
            if(turnTail.Index-lastTurnIndex>=turnSpacing)
            {
                var place=turnHead;double da=place.HeadingDelta;
                for(int i=0;i<turnMargin;i++){place=place.Previous??throw new InvalidOperationException("Road sign previous support missing");da+=place.HeadingDelta;}
                if(Math.Sign(da)==direction&&Math.Abs(da)>discard)return;
                lastTurn=Place(place,direction<0?HillsRoadSign.Left:HillsRoadSign.Right);lastTurnIndex=turnTail.Index;lastTurnDirection=direction;lastTurnSingle=true;
            }
            else if(lastTurnSingle&&lastTurnDirection!=direction){Variant(lastTurn,lastTurnDirection>0?HillsRoadSign.RightDouble:HillsRoadSign.LeftDouble);lastTurnSingle=false;}
        }
        void Hill(CoarseNode n)
        {
            if(n.Index==hillHead.Index)return;hillHead=Next(hillHead);hillTail=Next(hillTail);
            double dh=hillTail.Position.Y-hillHead.Position.Y;bool climbing=dh>0;
            double gradient=Math.Abs(dh)/100;int steep=gradient>.15?(gradient>.2?2:1):0;
            double tailDh=hillTail.Position.Y-hillTail.Previous.Position.Y;
            if(onHill)
            {
                // Preserve the original climbing?1:0 state, including its unusual downhill exit condition.
                if((hillDir>0&&tailDh<0)||(hillDir<0&&tailDh>0))onHill=false;
                else if(steep>hillSteep)Variant(lastHill,hillDir>0?HillsRoadSign.Uphill20:HillsRoadSign.Downhill20);
            }
            else if(steep>0&&hillTail.Index-lastHillIndex>20)
            {
                onHill=true;hillDir=climbing?1:0;hillSteep=steep;lastHillIndex=hillTail.Index;
                var p=hillHead.Previous?.Previous?.Previous??hillHead;
                lastHill=Place(p,steep==2?(climbing?HillsRoadSign.Uphill20:HillsRoadSign.Downhill20):(climbing?HillsRoadSign.Uphill15:HillsRoadSign.Downhill15));
            }
        }
        void Chevron(CoarseNode n)
        {
            if(n.Index>chevHead.Index){chevSum-=Math.Abs(chevHead.HeadingDelta);chevHead=Next(chevHead);}
            if(chevTail.Index>=chevHead.Index+4)return;chevTail=Next(chevTail);
            if(n.Index-lastChev>10&&chevSum<chevPrior&&Math.Abs(chevTail.HeadingDelta)>chevThreshold&&
                Math.Abs(Next(chevTail).HeadingDelta)>chevNext&&Math.Sign(chevTail.HeadingDelta)==Math.Sign(Next(chevTail).HeadingDelta))
            {
                int direction=chevTail.HeadingDelta<0?1:-1;var place=chevTail.Previous;
                MidlinePathNode fine=place,second=null,third=null;int count=0;
                while(!ReferenceEquals(fine,chevTail))
                {fine=fine.NextFine??throw new InvalidOperationException("Chevron fine support missing");count++;if(count==4)second=fine;else if(count==8)third=fine;}
                if(third!=null)
                {lastChev=n.Index;var type=direction<0?HillsRoadSign.ChevronLeft:HillsRoadSign.ChevronRight;Place(place,type,false,-direction,1,1);Place(second,type,false,-direction,1,1);Place(third,type,false,-direction,1,1);}
            }
            chevSum+=Math.Abs(chevTail.HeadingDelta);
        }
    }
}
