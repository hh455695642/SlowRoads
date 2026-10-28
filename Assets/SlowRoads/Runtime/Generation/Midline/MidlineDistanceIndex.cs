using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Midline
{
    public enum MidlineDistanceResult { Ready, NotReady, Expired, Invalidated }
    public readonly struct MidlineDistanceSample
    {
        public readonly Double3 Position,Forward;
        public readonly MidlineSampleKey From,To;
        public MidlineDistanceSample(Double3 p,Double3 forward,MidlineSampleKey from,MidlineSampleKey to)
        {Position=p;Forward=forward;From=from;To=to;}
    }
    /// <summary>Unity extension: sequential double chord-length accumulation, not D's UV.
    /// Update is explicit; sampling uses binary search and allocates nothing.</summary>
    public sealed class MidlineDistanceIndex
    {
        struct Point {public MidlineFineSample Sample;public double Distance;}
        readonly List<Point> points=new List<Point>();
        int generation=-1;
        int first;
        bool invalidated;
        public int Count=>points.Count-first;
        public int Generation=>generation;
        public double MinimumDistance=>Count==0?0:points[first].Distance;
        public double MaximumDistance=>points.Count==0?0:points[points.Count-1].Distance;
        public void Clear(){points.Clear();first=0;generation=-1;invalidated=false;}
        public MidlineDistanceResult Update(MidlineReadView view)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
            if(generation!=view.Generation){Clear();generation=view.Generation;}
            if(invalidated)return MidlineDistanceResult.Invalidated;
            int live=first;
            while(live<points.Count && points[live].Sample.CoarseIndex<view.HeadIndex)live++;
            if(live>0 && live==points.Count){invalidated=true;return MidlineDistanceResult.Invalidated;}
            first=live; // Retire without shifting the full window every update.
            if(first>=1024 && first>points.Count/2){points.RemoveRange(0,first);first=0;}
            if(points.Count>0 && view.GeometryDirtyFrom<=points[points.Count-1].Sample.CoarseIndex)
            {
                int cut=first;
                while(cut<points.Count && points[cut].Sample.CoarseIndex<view.GeometryDirtyFrom)cut++;
                if(cut==first){invalidated=true;return MidlineDistanceResult.Invalidated;}
                points.RemoveRange(cut,points.Count-cut);
            }
            int start=0;
            if(points.Count>0)
            {
                var last=points[points.Count-1].Sample;
                start=LowerBound(view,last.CoarseIndex,last.Ordinal);
                if(start>=view.FineCount || !view.GetFine(start).Key.Equals(last.Key))
                {invalidated=true;return MidlineDistanceResult.Invalidated;}
                start++;
            }
            for(int i=start;i<view.FineCount;i++)
            {
                var sample=view.GetFine(i);
                if(sample.CoarseIndex>=view.GeometryReadyExclusive)break;
                if(double.IsNaN(sample.Position.X)||double.IsNaN(sample.Position.Y)||double.IsNaN(sample.Position.Z)||
                    double.IsInfinity(sample.Position.X)||double.IsInfinity(sample.Position.Y)||double.IsInfinity(sample.Position.Z))
                {invalidated=true;return MidlineDistanceResult.Invalidated;}
                double distance=0;
                if(points.Count>0)
                {
                    var previous=points[points.Count-1];
                    double x=sample.Position.X-previous.Sample.Position.X,
                        y=sample.Position.Y-previous.Sample.Position.Y,z=sample.Position.Z-previous.Sample.Position.Z;
                    distance=previous.Distance+Math.Sqrt(x*x+y*y+z*z);
                }
                points.Add(new Point {Sample=sample,Distance=distance});
            }
            return Count>1?MidlineDistanceResult.Ready:MidlineDistanceResult.NotReady;
        }
        static int LowerBound(MidlineReadView view,int coarse,int ordinal)
        {
            int lo=0,hi=view.FineCount;
            while(lo<hi)
            {
                int m=lo+(hi-lo)/2;var s=view.GetFine(m);
                if(s.CoarseIndex<coarse || s.CoarseIndex==coarse && s.Ordinal<ordinal)lo=m+1;
                else hi=m;
            }
            return lo;
        }
        public MidlineDistanceResult TrySample(double distance,out MidlineDistanceSample sample)
        {
            sample=default;
            if(double.IsNaN(distance)||double.IsInfinity(distance))throw new ArgumentOutOfRangeException(nameof(distance));
            if(invalidated)return MidlineDistanceResult.Invalidated;
            if(Count<2)return MidlineDistanceResult.NotReady;
            if(distance<MinimumDistance)return MidlineDistanceResult.Expired;
            if(distance>MaximumDistance)return MidlineDistanceResult.NotReady;
            int lo=first,hi=points.Count-1;
            while(lo<hi)
            {
                int m=lo+(hi-lo)/2;
                if(points[m].Distance<=distance)lo=m+1;else hi=m;
            }
            int end=Math.Max(first+1,lo),begin=end-1;
            // Duplicate points do not supply a direction. Skip only zero-length chords.
            while(end<points.Count && points[end].Distance==points[begin].Distance)end++;
            if(end==points.Count)
            {
                end=points.Count-1;begin=end-1;
                while(begin>=first && points[begin].Distance==points[end].Distance)begin--;
                if(begin<first)return MidlineDistanceResult.NotReady;
            }
            var a=points[begin];var b=points[end];double length=b.Distance-a.Distance;
            double t=(distance-a.Distance)/length;
            var delta=new Double3(b.Sample.Position.X-a.Sample.Position.X,b.Sample.Position.Y-a.Sample.Position.Y,
                b.Sample.Position.Z-a.Sample.Position.Z);
            sample=new MidlineDistanceSample(a.Sample.Position.Lerp(b.Sample.Position,t),delta.Normalize(),a.Sample.Key,b.Sample.Key);
            return MidlineDistanceResult.Ready;
        }
    }
}
