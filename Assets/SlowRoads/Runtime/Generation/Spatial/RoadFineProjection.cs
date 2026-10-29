using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Spatial
{
    /// <summary>Normals.$u / Settings.m2. Supplementary TQ-L map in TerrainAudit.
    /// Fine distance/Y use t1; Ll controls use coarse t. Never substitute a global nearest segment.</summary>
    public static class RoadFineProjection
    {
        public static bool TryCreate(double x,double z,CoarseNode start,out RoadProjection p,int supportFirst=int.MinValue,int supportLast=int.MaxValue)
        {
            p=default;
            if(start==null||!double.IsFinite(x)||!double.IsFinite(z))return false;
            MidlinePathNode n=start;CoarseNode n0=start;bool fine=false;
            while(n!=null)
            {
                // Validate every visited node, not only the winning projection. Hitting
                // the copied graph edge must not turn an original continued walk into a result.
                int visited=(n as CoarseNode??(n as FineNode)?.PreviousCoarse)?.Index??int.MinValue;
                if(visited<=supportFirst||visited>=supportLast-1)return false;
                double side=n.Normal.X*(z-n.Position.Z)-n.Normal.Z*(x-n.Position.X);
                if(side<=0&&n.NextFine!=null)
                {
                    if(fine)
                    {
                        var b=n.NextFine;
                        if(b.Normal.X*(z-b.Position.Z)-b.Normal.Z*(x-b.Position.X)>0)break;
                        n=b;
                    }
                    else
                    {
                        var c=n as CoarseNode;if(c?.Next==null)return false;
                        var b=c.Next;
                        if(b.Normal.X*(z-b.Position.Z)-b.Normal.Z*(x-b.Position.X)>0){n0=c;fine=true;continue;}
                        n=b;
                    }
                }
                else if(side>0&&n.PreviousFine!=null)
                    n=fine?n.PreviousFine:(n as CoarseNode)?.Previous;
                else {n0=n as CoarseNode??(n as FineNode)?.PreviousCoarse;break;}
            }
            if(n0?.Next==null)return false;
            // Original missing-nextFine can throw or return scratch-dependent tail data.
            // Production contract rejects that incomplete neighbourhood instead.
            if(n0?.Next==null||n?.NextFine==null)return false;
            if(n0.Bridge&&n0.Next.Bridge&&n0.Next.Next==null)return false;
            if(!Parameter(x,z,n0.Position,n0.Next.Position,out double t)||
               !Parameter(x,z,n.Position,n.NextFine.Position,out double t1))return false;
            // Reuse i_'s attribute interpolation at the exact coarse projected point.
            // Do not re-run its walking rules (they differ at side == 0).
            var b0=n0.Next;
            double dx=n.Position.X+t1*(n.NextFine.Position.X-n.Position.X)-x;
            double dz=n.Position.Z+t1*(n.NextFine.Position.Z-n.Position.Z)-z;
            var owner=n as CoarseNode??((FineNode)n).PreviousCoarse;
            p=new RoadProjection{Index=n0.Index,Generation=n0.Handle.Generation,T=t,IsFine=true,
                FineKey=new MidlineSampleKey(owner.Handle,(n as FineNode)?.Ordinal??0),FineCoarseIndex=owner.Index,FineT=t1,
                Distance=Math.Sqrt(dx*dx+dz*dz),Y=n.Position.Y+t1*(n.NextFine.Position.Y-n.Position.Y),
                HeadingDelta=n0.HeadingDelta+t*(b0.HeadingDelta-n0.HeadingDelta),
                Side=dx*n.Normal.X+dz*n.Normal.Z<0?-1:1,Grade=n0.Grade+t*(b0.Grade-n0.Grade),
                HeightCorrection=n0.HeightCorrection+t*(b0.HeightCorrection-n0.HeightCorrection),
                ForwardGrade=n0.GradeForwardAverage+t*(b0.GradeForwardAverage-n0.GradeForwardAverage),
                InterpolatedGrade=n0.Grade+t*(b0.Grade-n0.Grade),Blend=n0.Blend+t*(b0.Blend-n0.Blend),
                Bank=n0.Bank*(1-t)+b0.Bank*t,Bridge=n0.Bridge,NextBridge=b0.Bridge,
                NextNextBridge=b0.Next?.Bridge??false,PreviousBridge=n0.Previous?.Bridge??false};
            return true;
        }
        static bool Parameter(double x,double z,Double3 a,Double3 b,out double t)
        {
            double dx=b.X-a.X,dz=b.Z-a.Z,len=dx*dx+dz*dz;t=0;
            if(len==0||!double.IsFinite(len))return false;
            t=((x-a.X)*dx+(z-a.Z)*dz)/len;t=Math.Max(0,Math.Min(1,t));return double.IsFinite(t);
        }
    }
}
