using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Road;
using SlowRoads.Generation.Spatial;

namespace SlowRoads.Generation.Terrain
{
    public enum TerrainProjectionMode { Coarse, Fine }

    /// <summary>U.getCompleteHeight inputs. Local coordinates are grid indices, not metres.
    /// Corner values belong to the caller's cell revision. Coordinates outside the cell are legal for normals.</summary>
    public readonly struct TerrainHeightInput
    {
        public readonly double X,Z,LocalX,LocalZ,TopLeft,TopRight,BottomLeft,BottomRight;
        public readonly int CellResolution;
        public readonly bool Bilinear;
        public readonly TerrainProjectionMode ProjectionMode;
        public TerrainHeightInput(double x,double z,double localX,double localZ,int resolution,
            double tl,double tr,double bl,double br,bool bilinear,TerrainProjectionMode mode)
        {X=x;Z=z;LocalX=localX;LocalZ=localZ;CellResolution=resolution;
            TopLeft=tl;TopRight=tr;BottomLeft=bl;BottomRight=br;Bilinear=bilinear;ProjectionMode=mode;}
    }
    public readonly struct TerrainHeightResult
    {
        public readonly double BaseHeight,Height;
        public readonly RoadProjection BeforeBlend,Projection;
        public readonly RoadBlendTrace Trace;
        public TerrainHeightResult(double h,double height,RoadProjection before,RoadProjection after,RoadBlendTrace trace)
        {BaseHeight=h;Height=height;BeforeBlend=before;Projection=after;Trace=trace;}
    }

    /// <summary>Pure U height composition. No generation, mesh, global state or node ownership.
    /// Caller supplies ordered candidates and verifies their residency before invoking.</summary>
    public static class TerrainHeightQuery
    {
        public static RoadQueryStatus TryEvaluate(in TerrainHeightInput input,IReadOnlyList<CoarseNode> candidates,
            Func<double,double,double> height,HillsRoadBlend blend,out TerrainHeightResult result,
            int supportFirst=int.MinValue,int supportLast=int.MaxValue)
        {
            result=default;
            if(input.CellResolution<1||!double.IsFinite(input.X)||!double.IsFinite(input.Z)||
               !double.IsFinite(input.LocalX)||!double.IsFinite(input.LocalZ)||
               !double.IsFinite(input.TopLeft)||!double.IsFinite(input.TopRight)||
               !double.IsFinite(input.BottomLeft)||!double.IsFinite(input.BottomRight)||
               (uint)input.ProjectionMode>1)throw new ArgumentOutOfRangeException(nameof(input));
            if(height==null||blend==null)throw new ArgumentNullException();
            if(candidates==null||candidates.Count==0)return RoadQueryStatus.NoRoad;
            RoadProjection selected=default;
            // Preserve array order and strict '<'. An unavailable candidate may win:
            // never skip it and silently select a farther mature road.
            for(int i=0;i<candidates.Count;i++)
            {
                RoadProjection p;
                bool ok=input.ProjectionMode==TerrainProjectionMode.Fine?
                    RoadFineProjection.TryCreate(input.X,input.Z,candidates[i],out p,supportFirst,supportLast):
                    RoadProjection.TryCreate(input.X,input.Z,candidates[i],out p,true,supportFirst,supportLast);
                if(!ok)return RoadQueryStatus.NotResident;
                if(p.Index<=supportFirst||(long)p.Index+2>=supportLast)return RoadQueryStatus.NotResident;
                if(i==0||p.Distance<selected.Distance)selected=p;
            }
            double h;double lx=input.LocalX,lz=input.LocalZ;int res=input.CellResolution;
            if(input.Bilinear)
            {
                double u=lx/res,v=lz/res;
                double h1=input.TopLeft*(1-u)+input.TopRight*u;
                double h2=input.BottomLeft*(1-u)+input.BottomRight*u;
                h=h1*(1-v)+h2*v;
            }
            else if(lx==0&&lz==0)h=input.TopLeft;
            else if(lx==0&&lz==res)h=input.BottomLeft;
            else if(lx==res&&lz==0)h=input.TopRight;
            else if(lx==res&&lz==res)h=input.BottomRight;
            else h=height(input.X,input.Z);
            var before=selected;double output=blend.Evaluate(h,ref selected,input.X,input.Z,out var trace);
            result=new TerrainHeightResult(h,output,before,selected,trace);return RoadQueryStatus.Ready;
        }
    }
}
