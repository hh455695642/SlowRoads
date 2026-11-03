using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Walls
{
    public enum HillsWallType {Bridge,Lowland,Barrier,Fence,Drystone}
    public sealed class WallSegment
    {
        public readonly int Start,End;public readonly HillsWallType Type;public double Distance {get;internal set;}
        public bool ContiguousStart {get;internal set;}public bool ContiguousEnd {get;internal set;}
        public WallSegment(int start,int end,HillsWallType type,double distance,bool contiguousStart=false,bool contiguousEnd=false)
        {if(end<start||!double.IsFinite(distance))throw new ArgumentOutOfRangeException();Start=start;End=end;Type=type;Distance=distance;ContiguousStart=contiguousStart;ContiguousEnd=contiguousEnd;}
    }
    /// <summary>Original d3 / $7 / q7 / Z7 / K7, L47521 and L47913–48315.
    /// Uses frozen road support plus the C2 signed overlay. Configuration is Hills' cr/_l result.</summary>
    public sealed class WallSegmentBuilder
    {
        readonly Func<int,CoarseNode> node;readonly Func<int,bool,double> sidewall;
        readonly HillsWallType rule;readonly double width;readonly int maxLength,detail;readonly bool narrow,offRoad;
        readonly WallSegment[] previous=new WallSegment[2];readonly int[] lastExceeded={-100,-100};
        internal sealed class State {internal readonly WallSegment[] Previous;internal readonly int[] Last;internal State(WallSegment[] previous,int[] last){Previous=previous;Last=last;}}
        static WallSegment Copy(WallSegment s)=>s==null?null:new WallSegment(s.Start,s.End,s.Type,s.Distance,s.ContiguousStart,s.ContiguousEnd);
        internal State Capture()=>new(new[]{Copy(previous[0]),Copy(previous[1])},(int[])lastExceeded.Clone());
        internal void Restore(State state,Func<WallSegment,WallSegment> resolve)
        {for(int i=0;i<2;i++){var s=Copy(state.Previous[i]);previous[i]=s==null?null:resolve(s)??s;lastExceeded[i]=state.Last[i];}}
        public WallSegmentBuilder(HillsWallType rule,int initialIndex,Func<int,CoarseNode> node,Func<int,bool,double> sidewall,
            double halfWidth,int maxLength=30,int detail=2,bool narrow=false,bool offRoad=false)
        {
            if(rule==HillsWallType.Fence)throw new ArgumentException("Fence is a Z7 output, not an independent rule");
            if(maxLength<8||maxLength>64||halfWidth<=0)throw new ArgumentOutOfRangeException();this.rule=rule;this.node=node;this.sidewall=sidewall;
            width=halfWidth;this.maxLength=maxLength;this.detail=detail;this.narrow=narrow;this.offRoad=offRoad;
            if(rule!=HillsWallType.Drystone)for(int i=0;i<2;i++)previous[i]=new WallSegment(initialIndex,initialIndex,(HillsWallType)(-1),0);
        }
        CoarseNode N(int index)=>node(index)??throw new InvalidOperationException("Wall interval lacks frozen road support at "+index);
        bool Test(CoarseNode n,int side,bool strict=false)
        {
            if(n==null)throw new InvalidOperationException("Missing wall sentinel");
            if(rule==HillsWallType.Bridge)return n.Bridge;
            if(rule==HillsWallType.Drystone)return sidewall(n.Index,side<0)!=0||!strict&&n.Index==lastExceeded[side<0?0:1]+1;
            if(rule==HillsWallType.Lowland)return !n.Bridge&&(side<0?n.Grade<-.3||n.HeightCorrection< -2:n.Grade>.3||n.HeightCorrection< -2);
            double gradient=narrow?2.5:1.5,turn=narrow?.5:.15;
            return !n.Bridge&&n.Position.Y>4&&n.Position.Y>3&&(side<0?n.Grade< -gradient||n.Grade<.1&&n.HeadingDelta< -turn&&n.HeightCorrection<.6:
                n.Grade>gradient||n.Grade>-.1&&n.HeadingDelta>turn&&n.HeightCorrection<.6);
        }
        bool Start(CoarseNode n,int side,WallSegment prev)
        {
            if(rule==HillsWallType.Bridge)return n.Bridge;
            if(rule==HillsWallType.Drystone)return (prev==null||n.Index>=prev.End)&&Test(n,side);
            if(rule==HillsWallType.Lowland)return Test(n,side)&&!n.Bridge&&!offRoad&&n.Position.Y<4;
            double gradient=narrow?4:2,turn=narrow?1:.3;
            return !n.Bridge&&n.Position.Y>4&&n.Index>=prev.End&&(side<0?n.Grade< -gradient||n.Grade<0&&n.HeadingDelta< -turn&&n.HeightCorrection<.6:
                n.Grade>gradient||n.Grade>0&&n.HeadingDelta>turn&&n.HeightCorrection<.6);
        }
        public WallSegment Generate(int index,bool left)
        {
            int which=left?0:1,side=left?-1:1;var prev=previous[which];var t=N(index);
            if(prev!=null&&index<=prev.End||!Start(t,side,prev))return null;CoarseNode a;
            if(rule==HillsWallType.Drystone)
            {
                a=t;while(a.Previous!=null&&Test(a.Previous,side)&&(prev==null||a.Index>prev.End))a=a.Previous;
                if(prev!=null&&a.Index-prev.End<4)a=N(prev.End);
                while(Test(t,side,true)&&t.Index-a.Index<maxLength)t=t.Next??throw new InvalidOperationException("Wall end not frozen");
                if(Test(t,side,true)&&t.Index-a.Index==maxLength)lastExceeded[which]=t.Index;
            }
            else
            {
                a=rule==HillsWallType.Bridge?t.Previous:t;if(rule==HillsWallType.Bridge)t=t.Next;
                if(a==null||t==null)throw new InvalidOperationException("Bridge wall needs adjacent road nodes");
                int backwards=maxLength/2-4;
                while(Test(a,side)&&a.Previous!=null&&a.Index>prev.End&&t.Index-a.Index<backwards)a=a.Previous;
                if(a.Index-prev.End<4)a=N(prev.End);
                while(Test(t,side)&&t.Index-a.Index<maxLength)t=t.Next??throw new InvalidOperationException("Wall end not frozen");
                if(rule==HillsWallType.Barrier&&t.Index-a.Index<3)return null;
            }
            var type=rule;double distance;
            if(rule==HillsWallType.Drystone)distance=sidewall(a.Index,left);
            else if(rule==HillsWallType.Bridge)distance=width*side;
            else if(rule==HillsWallType.Lowland)distance=(width+(narrow?1.2:.8))*side;
            else{if(offRoad||detail>=0&&(a.Index%2==0||prev.End==a.Index&&prev.Type==HillsWallType.Fence))type=HillsWallType.Fence;distance=(width+(narrow?.8:.4))*side;}
            var result=new WallSegment(a.Index,t.Index,type,distance);
            if(prev!=null&&prev.End==a.Index&&prev.Type==result.Type){prev.ContiguousEnd=true;result.ContiguousStart=true;result.Distance=prev.Distance;}
            previous[which]=result;return result;
        }
    }
}
