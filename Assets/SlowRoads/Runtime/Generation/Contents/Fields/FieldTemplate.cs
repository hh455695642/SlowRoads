using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Settings y9/Da L43119–43224. Original edge ordinals are identity, not sorted geometry keys.</summary>
    public sealed class FieldTemplate
    {
        public readonly struct Point {public readonly double X,Z;public Point(double x,double z){X=x;Z=z;}}
        public readonly struct Edge {public readonly int A,B;public readonly double Length;public Edge(int a,int b,double length){A=a;B=b;Length=length;}}
        public sealed class Plot
        {
            public readonly int[] Nodes,Edges;
            public readonly double MinX,MaxX,MinZ,MaxZ,CenterX,CenterZ,Radius,RadiusSquared;
            internal Plot(int[] nodes,int[] edges,double minX,double maxX,double minZ,double maxZ)
            {Nodes=nodes;Edges=edges;MinX=minX;MaxX=maxX;MinZ=minZ;MaxZ=maxZ;CenterX=(minX+maxX)/2;CenterZ=(minZ+maxZ)/2;
                RadiusSquared=((maxX-minX)*(maxX-minX)+(maxZ-minZ)*(maxZ-minZ))/4;Radius=Math.Sqrt(RadiusSquared);}
        }
        public readonly double Scale;
        public readonly Point[] Nodes;
        public readonly Edge[] Edges;
        public readonly Plot[] Plots;
        static readonly int[,] SourceNodes={
            {0,0},{14,0},{28,0},{40,0},{80,0},{100,0},{40,14},{66,14},{80,14},{0,26},{14,26},{28,26},
            {0,40},{14,40},{28,40},{40,40},{66,40},{80,40},{100,40},{40,53},{80,53},{100,53},
            {14,67},{28,67},{40,67},{80,67},{0,80},{14,80},{40,80},{80,80},{100,80},{0,100},{14,100},{40,100},{80,100},{100,100}};
        static readonly int[][] SourcePlots={
            new[]{0,1,10,9},new[]{1,2,11,10},new[]{2,3,6,15,14,11},new[]{3,4,8,7,6},new[]{4,5,18,17,8},new[]{9,10,11,14,13,12},
            new[]{6,7,16,15},new[]{7,8,17,16},new[]{12,13,22,27,26},new[]{13,14,23,22},new[]{14,15,19,24,23},new[]{15,16,17,20,19},
            new[]{17,18,21,20},new[]{19,20,25,24},new[]{20,21,30,29,25},new[]{22,23,24,28,27},new[]{24,25,29,28},
            new[]{26,27,32,31},new[]{27,28,33,32},new[]{28,29,34,33},new[]{29,30,35,34}};
        public FieldTemplate(double scale)
        {
            if(!double.IsFinite(scale)||scale<=0)throw new ArgumentOutOfRangeException(nameof(scale));
            Scale=scale;Nodes=new Point[36];double unit=scale/100;
            for(int i=0;i<Nodes.Length;i++)Nodes[i]=new Point(SourceNodes[i,0]*unit+5,SourceNodes[i,1]*unit+5);
            var edges=new List<Edge>();var lookup=new Dictionary<(int,int),int>();Plots=new Plot[SourcePlots.Length];
            for(int i=0;i<Plots.Length;i++)
            {
                int[] polygon=SourcePlots[i],ids=new int[polygon.Length];double minX=9007199254740991,maxX=-9007199254740991,minZ=minX,maxZ=maxX;
                for(int j=0;j<polygon.Length;j++)
                {
                    int a=polygon[j],b=polygon[(j+1)%polygon.Length];if(a>b)(a,b)=(b,a);
                    // Original !lookup[a][b] also treats edge zero as absent. Do not silently repair its semantics.
                    if(!lookup.TryGetValue((a,b),out int id)||id==0)
                    {id=edges.Count;lookup[(a,b)]=id;double dx=Nodes[b].X-Nodes[a].X,dz=Nodes[b].Z-Nodes[a].Z;edges.Add(new Edge(a,b,Math.Sqrt(dx*dx+dz*dz)));}
                    ids[j]=id;minX=Math.Min(minX,Math.Min(Nodes[a].X,Nodes[b].X));maxX=Math.Max(maxX,Math.Max(Nodes[a].X,Nodes[b].X));
                    minZ=Math.Min(minZ,Math.Min(Nodes[a].Z,Nodes[b].Z));maxZ=Math.Max(maxZ,Math.Max(Nodes[a].Z,Nodes[b].Z));
                }
                Plots[i]=new Plot((int[])polygon.Clone(),ids,minX,maxX,minZ,maxZ);
            }
            Edges=edges.ToArray();
        }
    }
}
