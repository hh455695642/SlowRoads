using System;
using System.Collections.Generic;
using SlowRoads.Generation.Contents.Fields;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Walls
{
    public sealed class FieldWallGeometryData
    {
        public readonly float[] Positions,UV,Shadow;public readonly int[] FarIndices,NearIndices;
        internal FieldWallGeometryData(List<double> p,List<double> uv,List<double> shadow,List<int> far,List<int> near)
        {Positions=Float(p);UV=Float(uv);Shadow=Float(shadow);FarIndices=far.ToArray();NearIndices=new int[far.Count+near.Count];far.CopyTo(NearIndices);near.CopyTo(NearIndices,far.Count);}
        static float[] Float(List<double> a){var result=new float[a.Count];for(int i=0;i<a.Count;i++)result[i]=(float)a[i];return result;}
    }
    /// <summary>Ka.makeWallPoint/Pillar L44529–44660. Pure expansion, no random or clipping.
    /// Float conversion occurs only at the original Float32Array publication boundary.</summary>
    public sealed class FieldWallGeometryBuilder
    {
        readonly List<double> positions=new(),uv=new(),shadow=new();readonly List<int> far=new(),near=new();int vertex;
        void V(double x,double y,double z,double u,double v,double s){positions.Add(x);positions.Add(y);positions.Add(z);uv.Add(u);uv.Add(v);shadow.Add(s);}
        void I(List<int> list,params int[] indices){foreach(int i in indices)list.Add(vertex+i);}
        public FieldWallGeometryData Build(FieldBoundaryRecipe recipe)
        {
            positions.Clear();uv.Clear();shadow.Clear();far.Clear();near.Clear();vertex=0;
            foreach(var e in recipe.Events)
            {
                if(e.Operation==FieldBoundaryOperation.Pillar){Pillar(e.Position,e.Normal,e.Shadow);continue;}
                if(e.Operation==FieldBoundaryOperation.Start){vertex=positions.Count/3;I(near,0,1,2,0,2,3);}
                else{I(far,0,5,1,0,4,5);I(near,1,6,2,1,5,6,2,7,3,2,6,7);vertex+=4;}
                double s=Math.Sqrt(e.Shadow)*1.5;var p=e.Position;var n=e.Normal;
                V(p.X+.38*n.X,p.Y-.12,p.Z+.38*n.Z,e.U,0,s);
                V(p.X+.19*n.X,p.Y-.12+1.12,p.Z+.19*n.Z,e.U,.475,s);
                V(p.X-.19*n.X,p.Y-.12+1.12,p.Z-.19*n.Z,e.U,1-.475,s);
                V(p.X-.38*n.X,p.Y-.12,p.Z-.38*n.Z,e.U,1,s);
                if(e.Operation==FieldBoundaryOperation.End)I(near,0,2,1,0,3,2);
            }
            return new FieldWallGeometryData(positions,uv,shadow,far,near);
        }
        void Pillar(Double3 p,Double3 n,double value)
        {
            double xb=n.X*.38,zb=n.Z*.38,xt=n.X*.19,zt=n.Z*.19,y=p.Y-.12/2,s=Math.Sqrt(value)*1.5;vertex=positions.Count/3;
            V(p.X-xb-zb,y,p.Z-zb+xb,0,0,s);V(p.X-xt-zt,y+1.12,p.Z-zt+xt,0,.475-.1,s);
            V(p.X+xb-zb,y,p.Z+zb+xb,.08,0,s);V(p.X+xt-zt,y+1.12,p.Z+zt+xt,.08,.475-.1,s);
            V(p.X+xb+zb,y,p.Z+zb-xb,0,0,s);V(p.X+xt+zt,y+1.12,p.Z+zt-xt,0,.475-.1,s);
            V(p.X-xb+zb,y,p.Z-zb-xb,.08,0,s);V(p.X-xt+zt,y+1.12,p.Z-zt-xt,.08,.475-.1,s);
            V(p.X-xt+zt,y+1.12,p.Z-zt-xt,0,.475,s);
            I(near,0,3,1,0,2,3,2,5,3,2,4,5,4,7,5,4,6,7,6,1,7,6,0,1,1,3,8,3,5,8);
        }
    }
}
