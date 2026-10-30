using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Terrain
{
    /// <summary>Original f0.getIndexesForRes, Settings L41131–41190.
    /// The coarse perimeter retains fine edge vertices; it is not a regular decimated grid.</summary>
    public sealed class TerrainGridTopology
    {
        readonly int[] coarse,fine;
        public int Resolution {get;}
        public int CoarseCount=>coarse.Length;
        public int FineCount=>fine.Length;
        public int GetCoarse(int index)=>coarse[index];
        public int GetFine(int index)=>fine[index];
        // Legal Hills resolutions only. No unbounded static cache or mutable shared arrays.
        public TerrainGridTopology(int t)
        {
            if(Array.IndexOf(new[]{10,16,20,24,48,84,150,200,250},t)<0)throw new ArgumentOutOfRangeException(nameof(t));
            Resolution=t;int d=t+1,p=t,u=t/2,m,w,n,o,v,y;
            var f=new List<int>();var a=new List<int>();
            void F(params int[] values)=>f.AddRange(values);
            for(int j=0;j<=t;j++)for(int x=0;x<=t;x++)
            {
                m=j*d+x;w=m+1;n=(j+1)*d+x;o=n+1;
                if(j<u&&x<u)
                {
                    bool edgeX=j==0||j==u-1,edgeZ=x==0||x==u-1;
                    if(edgeX||edgeZ)
                    {
                        if(edgeZ)
                        {
                            if(!edgeX)
                            {
                                if(x==0)F(m*2,w*2,m*2+d,w*2,o*2,m*2+d,o*2,n*2,m*2+d);
                                else F(w*2,w*2+d,m*2,m*2,w*2+d,n*2,n*2,w*2+d,o*2);
                            }
                        }
                        else if(j==0)F(m*2,m*2+1,n*2,n*2,m*2+1,o*2,o*2,m*2+1,w*2);
                        else F(m*2,n*2+1,n*2,w*2,n*2+1,m*2,o*2,n*2+1,w*2);
                    }
                    else F(m*2,w*2,n*2,w*2,o*2,n*2);
                }
                if(j<p&&x<p){a.Add(m);a.Add(w);a.Add(n);a.Add(w);a.Add(o);a.Add(n);}
            }
            F(0,1,d,1,2,2*d+2,d,1,2*d+2,d,2*d+2,2*d);
            m=p-2;w=m+1;n=w+1;o=d+p;v=2*d+p-2;y=v+2;F(m,w,v,w,n,o,v,o,y,v,w,o);
            m=d*(p-2);w=m+2;n=d*(p-1);o=d*p;v=o+1;y=v+1;F(m,w,n,n,v,o,v,w,y,n,w,v);
            m=(d+1)*(p-2);w=m+2;n=w+d;o=m+2*d;v=o+1;y=v+1;F(m,w,n,m,v,o,v,n,y,m,n,v);
            coarse=f.ToArray();fine=a.ToArray();
        }
    }
}
