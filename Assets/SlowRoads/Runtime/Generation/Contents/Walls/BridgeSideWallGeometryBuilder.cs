using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Walls
{
    /// <summary>S2 L47973–48205. Shared original cross section for bridge and lowland walls.
    /// No normals are written in the original. One original coarse interval per Step.</summary>
    public sealed class BridgeSideWallGeometryBuilder
    {
        readonly WallGeometryInput input;readonly WallGeometryBuffer buffer=new();readonly double ox,oz,offset;readonly int side;
        readonly double[] v;CoarseNode node;double distance;int rows;
        public WallGeometryData Data {get;private set;}
        public BridgeSideWallGeometryBuilder(WallGeometryInput input)
        {
            this.input=input;if(input.Segment.Type!=HillsWallType.Bridge&&input.Segment.Type!=HillsWallType.Lowland)throw new ArgumentException("S2 type mismatch");
            node=input.Start;ox=node.Position.X;oz=node.Position.Z;side=Math.Sign(input.Segment.Distance);offset=side<0?.25:0;
            double a=Math.Atan(1/.15),n=a+.25+a,d=a/n,p=.25/n;v=new[]{2.0,(1-d)*2,(1-d-p)*2,0};
        }
        void Row(MidlinePathNode n)
        {
            double x=n.Position.X+n.Normal.X*(input.Segment.Distance-offset),z=n.Position.Z+n.Normal.Z*(input.Segment.Distance-offset),s=side<0?n.ShadowLeft:n.ShadowRight;
            buffer.Vertex(x-ox-n.Normal.X*.15,n.Position.Y,z-oz-n.Normal.Z*.15,0,v[0],s);
            buffer.Vertex(x-ox,n.Position.Y+1,z-oz,0,v[1],s);
            buffer.Vertex(x+n.Normal.X*.25-ox,n.Position.Y+1,z+n.Normal.Z*.25-oz,0,v[2],s);
            buffer.Vertex(x+n.Normal.X*.25-ox+n.Normal.X*.15,n.Position.Y,z+n.Normal.Z*.25-oz+n.Normal.Z*.15,0,v[3],s);rows++;
        }
        public bool Step()
        {
            if(Data!=null)return true;var next=node.Next??throw new InvalidOperationException("S2 next coarse missing");int fine=0,actual=0,firstRow=rows;MidlinePathNode at=node;
            do
            {
                fine++;actual++;Row(at);at=at.NextFine??throw new InvalidOperationException("S2 fine chain missing");
                if(!ReferenceEquals(at,next)&&!(fine==1&&ReferenceEquals(at.NextFine,next))){actual++;at=at.NextFine??throw new InvalidOperationException("S2 fine chain truncated");}
            }while(!ReferenceEquals(at,next));
            double stride=2.5/actual;bool subsampled=actual!=fine,even=actual%2==0;
            for(int i=0;i<fine;i++){for(int j=0;j<4;j++)buffer.UV[(firstRow+i)*8+j*2]=(float)distance;distance+=stride;if(subsampled&&(even||i<fine-1))distance+=stride;}
            node=next;if(node.Index<input.Segment.End)return false;
            Row(node);for(int j=0;j<4;j++)buffer.UV[(rows-1)*8+j*2]=(float)distance;
            for(int i=0;i<rows-1;i++)buffer.Connect(i*4,4);
            if(!input.Segment.ContiguousStart)for(int i=0;i<4;i++)buffer.Positions[i*3+1]=(float)(buffer.Positions[i*3+1]-1.0);
            if(!input.Segment.ContiguousEnd)for(int i=0;i<4;i++){int p=buffer.Positions.Count-(i*3+2);buffer.Positions[p]=(float)(buffer.Positions[p]-1.0);}
            Data=buffer.Finish(new Double3(ox,-.2,oz));return true;
        }
    }
}
