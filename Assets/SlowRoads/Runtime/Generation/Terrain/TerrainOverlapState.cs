using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Terrain
{
    /// <summary>Dt.registerOverlap/registerEdgeOverlap, Settings L42780–42818.
    /// Native byte results remain queryable for parity. The user authorized (2026-10-30)
    /// complete float indices for the Unity display to avoid Int8 overflow. Logical indices also identify the
    /// dependent chunk and must never be reconstructed from a wrapped byte.</summary>
    public sealed class TerrainOverlapState
    {
        public readonly int Resolution,Side;
        
        readonly int[] logical;
        public bool Dirty {get;private set;}
        public TerrainOverlapState(int resolution){Resolution=resolution;Side=resolution+1;logical=new int[Side*Side];}
        void Set(int x,int z,int index)
        {
            int at=x*Side+z;if((uint)at>=logical.Length)return; // original TypedArray ignores out-of-range writes
            index=Math.Max(1,index);logical[at]=index;Dirty=true;
        }
        public void Cell(int x,int z,int index){Set(x,z,index);Set(x,z+1,index);Set(x+1,z,index);Set(x+1,z+1,index);}
        public void Edge(int x,int z,int edge,int index)
        {
            int f=Resolution;
            switch(edge)
            {
                case 0:Set(x,0,index);Set(x+1,0,index);break;
                case 1:Set(f,0,index);break;
                case 2:Set(f,z,index);Set(f,z+1,index);break;
                case 3:Set(f,f,index);break;
                case 4:Set(x,f,index);Set(x+1,f,index);break;
                case 5:Set(0,f,index);break;
                case 6:Set(0,z,index);Set(0,z+1,index);break;
                default:Set(0,0,index);break;
            }
        }
        public sbyte Native(int vertex)=>unchecked((sbyte)logical[vertex]);
        public int Logical(int vertex)=>logical[vertex];
        public void CopyTo(float[] destination)
        {if(destination.Length!=logical.Length)throw new ArgumentException();for(int i=0;i<logical.Length;i++)destination[i]=logical[i];Dirty=false;}
    }
}
