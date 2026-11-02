using System;
namespace SlowRoads.Generation.Contents.Trees
{
    public readonly struct TreeNearResult
    {
        public readonly bool Visible;
        public readonly double Height;
        public TreeNearResult(bool visible,double height){Visible=visible;Height=height;}
    }
    public static class TreeNearResolver
    {
        // s0.loadTrees L46249–46315. Inputs have already passed through original
        // Float32 mesh storage. In particular float(.7) is below double .7.
        public static TreeNearResult Evaluate(TreeCandidate c,double height,double road,double normal)
        {
            if(c.Id.Source!=0)return new(true,c.Y); // Original extra trees bypass normal candidate rejection.
            bool visible=c.FarValid&&height>=2&&road>=1.8&&normal>=.7;
            double groupScale=c.Type*.5+1.08; // Three-tree scale bonus is supplied by caller overload.
            return Evaluate(c,height,road,normal,groupScale,visible);
        }
        public static TreeNearResult Evaluate(TreeCandidate c,double height,double road,double normal,double groupScale,bool initial=true)
        {
            if(c.Id.Source!=0)return new(true,c.Y);
            bool visible=initial&&c.FarValid&&height>=2&&road>=1.8&&normal>=.7&&!(c.Type==1&&c.Variant!=2&&road<1.8+2*groupScale);
            return new(visible,road>4?height-(road-4)/6:height);
        }
    }
    /// <summary>Compact owned near mesh channels; no references to disposed T3 arrays.</summary>
    public sealed class TreeTerrainCell
    {
        public readonly int X,Z,Resolution;
        readonly float[] heights,roads,normals;
        public TreeTerrainCell(int x,int z,int resolution,float[] heights,float[] roads,float[] normals)
        {
            int size=(resolution+1)*(resolution+1);
            if(heights.Length!=size||roads.Length!=size||normals.Length!=size)throw new ArgumentException("Invalid cell channels");
            X=x;Z=z;Resolution=resolution;this.heights=(float[])heights.Clone();this.roads=(float[])roads.Clone();this.normals=(float[])normals.Clone();
        }
        public TreeNearResult Evaluate(TreeCandidate c,double groupScale)
        {
            double x=(c.X/10-Math.Floor(c.X/10))*Resolution,z=(c.Z/10-Math.Floor(c.Z/10))*Resolution;
            int ix=Math.Min(Resolution-1,(int)Math.Floor(x)),iz=Math.Min(Resolution-1,(int)Math.Floor(z)),stride=Resolution+1;
            double dx=x-ix,dz=z-iz;int tl=ix*stride+iz,tr=tl+1,bl=tl+stride,br=bl+1;
            double h=(heights[tl]*(1-dz)+heights[tr]*dz)*(1-dx)+(heights[bl]*(1-dz)+heights[br]*dz)*dx;
            double n=normals[tl];if(n==0){n=normals[tr];if(n==0){n=normals[bl];if(n==0)n=normals[br];}}
            return TreeNearResolver.Evaluate(c,h,roads[tl],n,groupScale);
        }
    }
}
