using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Spatial
{
    // Value copy of normals.i_ (Settings import Z4). d is lateral metres, not Ca's d².
    // Ll writes its derived values into this copy; no mutable producer node escapes.
    public struct RoadProjection
    {
        internal object Owner;
        public int Index,Generation;
        public long Revision;
        public double Distance,Y,HeadingDelta,Side,Grade,T;
        public double HeightCorrection,ForwardGrade,InterpolatedGrade,Blend,Bank;
        public bool Bridge,NextBridge,NextNextBridge,PreviousBridge;
        public double GradeAdjustment,OriginalDistance,Width,Slope;
        public bool HasSlope;

        public static bool TryCreate(double x,double z,CoarseNode start,out RoadProjection p)
        {
            p=default;if(start==null)return false;
            var n=start;
            // This is the original side-of-normal walk, not a global closest-segment search.
            while(n!=null)
            {
                double side=n.Normal.X*(z-n.Position.Z)-n.Normal.Z*(x-n.Position.X);
                if(side<0&&n.Next!=null){var b=n.Next;double s=b.Normal.X*(z-b.Position.Z)-b.Normal.Z*(x-b.Position.X);if(s>0)break;n=b;}
                else if(side>0&&n.Previous!=null)n=n.Previous;else break;
            }
            if(n?.Next==null)n=start.Previous?.Previous;
            if(n?.Next==null)return false;
            var next=n.Next;
            if(n.Bridge&&next.Bridge&&next.Next==null)return false;
            double dx=next.Position.X-n.Position.X,dz=next.Position.Z-n.Position.Z;
            double len=dx*dx+dz*dz;if(len==0)return false;
            double t=((x-n.Position.X)*dx+(z-n.Position.Z)*dz)/len;t=Math.Max(0,Math.Min(1,t));
            double px=n.Position.X+t*dx-x,pz=n.Position.Z+t*dz-z;
            p=new RoadProjection{Index=n.Index,Generation=n.Handle.Generation,T=t,
                Distance=Math.Sqrt(px*px+pz*pz),Y=n.Position.Y+t*(next.Position.Y-n.Position.Y),
                HeadingDelta=n.HeadingDelta+t*(next.HeadingDelta-n.HeadingDelta),Side=px*n.Normal.X+pz*n.Normal.Z<0?-1:1,
                Grade=n.Grade+t*(next.Grade-n.Grade),
                HeightCorrection=n.HeightCorrection+t*(next.HeightCorrection-n.HeightCorrection),
                ForwardGrade=n.GradeForwardAverage+t*(next.GradeForwardAverage-n.GradeForwardAverage),
                InterpolatedGrade=n.Grade+t*(next.Grade-n.Grade),Blend=n.Blend+t*(next.Blend-n.Blend),
                Bank=n.Bank*(1-t)+next.Bank*t,Bridge=n.Bridge,NextBridge=next.Bridge,
                NextNextBridge=next.Next?.Bridge??false,PreviousBridge=n.Previous?.Bridge??false};
            return true;
        }
    }
}
