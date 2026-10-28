using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Road
{
    // Normals Wm / l_ exports (ay / ax); Hills xl, not D's fallback width.
    public enum HillsLaneStyle { OffRoadWide, OffRoad, Single, SingleWide, Standard, TwoLane, ThreeLane, FourLane }
    public enum RoadBuildResult { NeedMoreInput, Building, Complete, Invalidated }
    public enum RoadLod { Coarse = 0, Fine = 1 }

    public sealed class RoadMeshConfig
    {
        public readonly HillsLaneStyle LaneStyle;
        public readonly int Intervals;
        public readonly bool BaseOnly, OffRoad;
        public readonly double HalfWidth, UvStep;
        public readonly string Id;
        public RoadMeshConfig(HillsLaneStyle laneStyle, int intervals, bool baseOnly = false)
        {
            if ((int)laneStyle < 0 || (int)laneStyle > 7) throw new ArgumentOutOfRangeException(nameof(laneStyle));
            // UInt16 capacity: 2 * (10*A+1) <= 65535. No silently wrapped indices.
            if (intervals < 1 || intervals > 3276) throw new ArgumentOutOfRangeException(nameof(intervals));
            LaneStyle=laneStyle; Intervals=intervals; BaseOnly=baseOnly;
            int lanes=Math.Max(0,(int)laneStyle-3);
            double width=lanes==0 ? 3.6 : 3.4;
            if (laneStyle==HillsLaneStyle.OffRoadWide || laneStyle==HillsLaneStyle.SingleWide) width=lanes==0 ? 7.2 : 4;
            double half;
            if(lanes==0) half=width/2;
            else { half=.1; for(int i=0;i<lanes;i++) half+=width; half+=.2; }
            HalfWidth=baseOnly ? 3.2 : half; UvStep=10/(HalfWidth*2);
            OffRoad=!baseOnly && (laneStyle==HillsLaneStyle.OffRoad || laneStyle==HillsLaneStyle.OffRoadWide);
            Id=((int)laneStyle)+":"+intervals+":"+(baseOnly?"D":"Nl");
        }
    }

    public readonly struct RoadSegmentRequest
    {
        public readonly int SegmentId, Generation, HeadCoarseIndex, ObserverIndex;
        public readonly long Revision;
        public readonly double InitialPhase;
        public readonly string ConfigurationId;
        public RoadSegmentRequest(int segmentId,int generation,long revision,int headCoarseIndex,
            string configurationId,double initialPhase,int observerIndex=0)
        {
            if(double.IsNaN(initialPhase)||double.IsInfinity(initialPhase)) throw new ArgumentOutOfRangeException(nameof(initialPhase));
            SegmentId=segmentId; Generation=generation; Revision=revision; HeadCoarseIndex=headCoarseIndex;
            ConfigurationId=configurationId; InitialPhase=initialPhase; ObserverIndex=observerIndex;
        }
    }

    public readonly struct RoadDrawRange
    {
        public readonly int Start, Count;
        public RoadDrawRange(int start,int count){Start=start;Count=count;}
    }
    public readonly struct RoadIntervalSpan
    {
        public readonly int CoarseIndex, FirstVertex, LastVertex;
        public RoadIntervalSpan(int coarseIndex,int firstVertex,int lastVertex)
        {CoarseIndex=coarseIndex;FirstVertex=firstVertex;LastVertex=lastVertex;}
    }
    public readonly struct RoadReferenceSphere
    {
        public readonly Double3 Center;
        public readonly double Radius;
        public RoadReferenceSphere(Double3 center,double radius){Center=center;Radius=radius;}
    }
}
