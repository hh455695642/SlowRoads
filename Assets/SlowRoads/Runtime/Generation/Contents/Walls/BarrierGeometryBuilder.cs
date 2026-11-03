using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Walls
{
    /// <summary>T0/o5 L47565–47912. Original per-detail cross section and side-specific winding.</summary>
    public sealed class BarrierGeometryBuilder
    {
        readonly WallGeometryInput input;readonly WallGeometryBuffer buffer=new();readonly int level,side;readonly double ox,oz;
        CoarseNode node;int rows;double distance;public WallGeometryData Data {get;private set;}
        public BarrierGeometryBuilder(WallGeometryInput input,int detail)
        {
            this.input=input;if(input.Segment.Type!=HillsWallType.Barrier||detail<0||detail>4)throw new ArgumentException("T0 type/detail mismatch");
            level=Math.Min(3,detail);side=Math.Sign(input.Segment.Distance);node=input.Start;ox=node.Position.X;oz=node.Position.Z;
        }
        void V(double x,double y,double z,double v,Double3 n,double shadow)
        {buffer.Vertex(x,y,z,0,v,shadow);buffer.Normals.Add((float)(n.X*side));buffer.Normals.Add(0);buffer.Normals.Add((float)(n.Z*side));}
        void Row(MidlinePathNode n,CoarseNode reference)
        {
            double x=n.Position.X+n.Normal.X*input.Segment.Distance,z=n.Position.Z+n.Normal.Z*input.Segment.Distance,
                x1=x+n.Normal.X*.1*side,z1=z+n.Normal.Z*.1*side,elev=input.RoadsideHeight==null?n.Position.Y:input.RoadsideHeight(x,z,reference),s=(side<0?n.ShadowLeft:n.ShadowRight)*2;
            V(x1-ox,elev,z1-oz,0,n.Normal,s);
            if(level>2)V(x1-ox,elev+.78*.55,z1-oz,.55,n.Normal,s);
            if(level>1)V(x-ox,elev+.78*.68,z-oz,level==2?.55:.68,n.Normal,s);
            if(level>0)V(x-ox,elev+.78*.88,z-oz,.88,n.Normal,s);
            V(x1-ox,elev+.78,z1-oz,.98,n.Normal,s);rows++;
        }
        public bool Step()
        {
            if(Data!=null)return true;var next=node.Next??throw new InvalidOperationException("T0 next coarse missing");int fine=0,actual=0,first=rows;MidlinePathNode at=node;
            do
            {fine++;actual++;Row(at,next);at=at.NextFine??throw new InvalidOperationException("T0 fine chain missing");
                if(!ReferenceEquals(at,next)&&!(fine==1&&ReferenceEquals(at.NextFine,next))){actual++;at=at.NextFine??throw new InvalidOperationException("T0 fine support truncated");}}
            while(!ReferenceEquals(at,next));
            double stride=6.0/actual;bool subsampled=actual!=fine,even=actual%2==0;int width=level+2;
            for(int i=0;i<fine;i++){for(int j=0;j<width;j++)buffer.UV[(first+i)*width*2+j*2]=(float)distance;distance+=stride;if(subsampled&&(even||i<fine-1))distance+=stride;}
            node=next;if(node.Index<input.Segment.End)return false;Row(node,node);for(int j=0;j<width;j++)buffer.UV[(rows-1)*width*2+j*2]=(float)distance;
            for(int i=0;i<rows-1;i++)buffer.Connect(i*width,width);
            if(side>0)for(int i=0;i<buffer.Indices.Count;i+=3)(buffer.Indices[i+1],buffer.Indices[i+2])=(buffer.Indices[i+2],buffer.Indices[i+1]);
            if(!input.Segment.ContiguousStart)for(int i=0;i<width;i++)buffer.Positions[i*3+1]=(float)(buffer.Positions[i*3+1]-.78);
            if(!input.Segment.ContiguousEnd)for(int i=0;i<width;i++){int atIndex=buffer.Positions.Count-(i*3+2);buffer.Positions[atIndex]=(float)(buffer.Positions[atIndex]-.78);}
            Data=buffer.Finish(new Double3(ox,-.15,oz),true);return true;
        }
    }
}
