using System;

namespace SlowRoads.Generation.Midline
{
    /// <summary>
    /// Se.extendFine, Dev L01372–L01418. Each Advance unit emits one sample.
    /// The final sample reuses the next coarse endpoint; normals lag one sample.
    /// </summary>
    public sealed class FineMidlineSampler
    {
        readonly CentripetalCurve curve = new CentripetalCurve();
        CoarseNode coarse;
        MidlinePathNode previous;
        int ordinal, segments;
        bool complete=true;
        public bool IsComplete => complete;
        public int Segments => segments;
        public CoarseNode NextCoarse => complete ? coarse?.Next : null;
        internal void Clear(){coarse=null;previous=null;complete=true;}

        public void Begin(CoarseNode start, int minimumSegments, double curvatureHeightFactor)
        {
            if(!complete) throw new InvalidOperationException("Finish the current interval first.");
            if(start?.Next?.Next==null) throw new ArgumentException("Fine sampling requires two coarse successors.");
            if(minimumSegments<1 || minimumSegments>10) throw new ArgumentOutOfRangeException(nameof(minimumSegments));
            coarse=start;
            curve.Set(start.Previous?.Position ?? start.Position,start.Position,start.Next.Position,start.Next.Next.Position);
            Double3 a=WeightedDirection(start.Position,start.Next.Position,curvatureHeightFactor);
            Double3 c=WeightedDirection(start.Next.Position,start.Next.Next.Position,curvatureHeightFactor);
            double minDot;
            if(start.Previous!=null)
            {
                Double3 b=WeightedDirection(start.Previous.Position,start.Position,curvatureHeightFactor);
                minDot=Math.Min(a.Dot(b),a.Dot(c));
                if(minDot>1) minDot=1;
            }
            else minDot=a.Dot(c);
            minDot=(1-Math.Max(minDot,0.99))*100;
            minDot=Math.Sqrt(minDot);
            if(double.IsNaN(minDot)) throw new InvalidOperationException("Original subdivision produced NaN.");
            segments=(int)Math.Floor(minDot*10);
            if(segments>10) segments=10;
            if(segments<minimumSegments) segments=minimumSegments;
            start.HasFineSegments=true;start.FineSegments=segments;
            previous=start;ordinal=1;complete=false;
        }
        static Double3 WeightedDirection(Double3 from,Double3 to,double heightFactor) =>
            new Double3((to.X-from.X)*1,(to.Y-from.Y)*heightFactor,(to.Z-from.Z)*1).Normalize();

        public bool Advance(int budget)
        {
            if(budget<1) throw new ArgumentOutOfRangeException(nameof(budget));
            while(budget-- >0 && !complete)
            {
                MidlinePathNode next;
                if(ordinal<segments)
                {
                    double t=ordinal/(double)segments;
                    next=new FineNode {PreviousCoarse=coarse,NextCoarse=coarse.Next,
                        Ordinal=ordinal,PreviousFine=previous,Normal=new Double3(1,0,0),
                        Position=curve.GetPoint(1.0/3+(1.0/3)*t)};
                }
                else { next=coarse.Next;next.PreviousFine=previous; }
                previous.NextFine=next;
                if(previous.PreviousFine!=null)
                {
                    double x=next.Position.X-previous.PreviousFine.Position.X;
                    double z=next.Position.Z-previous.PreviousFine.Position.Z;
                    double length=Math.Sqrt(x*x+z*z);
                    // The original divides by zero for a degenerate horizontal tangent.
                    previous.Normal=new Double3(-z/length,previous.Normal.Y,x/length);
                }
                previous=next;
                ordinal++;
                if(ordinal>segments) complete=true;
            }
            return complete;
        }
    }
}
