using System;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Contents.Fields;

namespace SlowRoads.Generation.Contents.Walls
{
    /// <summary>k2 L48317–48588. Module-scoped Y9/C4 must be shared in segment generation order.</summary>
    public sealed class DrystoneWallRandom
    {internal readonly FieldRandomState.Stream Jitter=new("wall",31,.2,.1),Shape=new("wall");}
    public sealed class DrystoneWallGeometryBuilder
    {
        readonly WallGeometryInput input;readonly DrystoneWallRandom random;readonly WallGeometryBuffer buffer=new();
        readonly double ox,oz,offset;readonly int side;CoarseNode node;int interval,rows;double distance;
        public WallGeometryData Data {get;private set;}
        public DrystoneWallGeometryBuilder(WallGeometryInput input,DrystoneWallRandom random)
        {this.input=input;this.random=random??throw new ArgumentNullException(nameof(random));if(input.Segment.Type!=HillsWallType.Drystone||input.RoadsideHeight==null)throw new ArgumentException("k2 requires C2 offset and frozen roadside height");
            node=input.Start;ox=node.Position.X;oz=node.Position.Z;side=Math.Sign(input.Segment.Distance);offset=side<0?.38:0;}
        void Row(MidlinePathNode n,CoarseNode baseNode,bool last)
        {
            double dist=input.Segment.Distance,height=1.12-.1,peak=.1;
            if(!last&&interval>0){dist+=random.Jitter.Next();height=1.12+random.Jitter.Next();}
            double x=n.Position.X+n.Normal.X*(dist-offset),z=n.Position.Z+n.Normal.Z*(dist-offset),xw=x+n.Normal.X*.38,zw=z+n.Normal.Z*.38,
                mx=(x+xw)/2,mz=(z+zw)/2,elev=input.RoadsideHeight(x,z,baseNode),s=Math.Sqrt(side<0?n.ShadowLeft:n.ShadowRight)*1.25;
            double mmx=mx,mmz=mz,nmx=mx,nmz=mz,peakA=1.12,peakB=1.12;
            if(!last)
            {double w=.06+random.Shape.Next()*.05;mmx-=n.Normal.X*w;mmz-=n.Normal.Z*w;w=.06+random.Shape.Next()*.05;nmx+=n.Normal.X*w;nmz+=n.Normal.Z*w;
                peakA=height+peak+.03-random.Shape.Next()*.06;peakB=height+peak+.03-random.Shape.Next()*.06;}
            buffer.Vertex(x-ox-n.Normal.X*.19,elev,z-oz-n.Normal.Z*.19,0,1,s);buffer.Vertex(x-ox,elev+height,z-oz,0,.56,s);
            buffer.Vertex(mmx-ox,elev+peakA,mmz-oz,0,.52,s);buffer.Vertex(nmx-ox,elev+peakB,nmz-oz,0,.48,s);
            buffer.Vertex(xw-ox,elev+height,zw-oz,0,.44,s);buffer.Vertex(xw-ox+n.Normal.X*.19,elev,zw-oz+n.Normal.Z*.19,0,0,s);rows++;
        }
        public bool Step()
        {
            if(Data!=null)return true;var next=node.Next??throw new InvalidOperationException("k2 next coarse missing");int fine=0,actual=0,first=rows;MidlinePathNode at=node;
            do
            {fine++;actual++;Row(at,node,false);at=at.NextFine??throw new InvalidOperationException("k2 fine chain missing");
                if(!ReferenceEquals(at,next)&&!(fine==1&&ReferenceEquals(at.NextFine,next))){actual++;at=at.NextFine??throw new InvalidOperationException("k2 fine chain truncated");}}
            while(!ReferenceEquals(at,next));
            double stride=2.0/actual;bool subsampled=actual!=fine,even=actual%2==0;
            for(int i=0;i<fine;i++){for(int j=0;j<6;j++)buffer.UV[(first+i)*12+j*2]=(float)distance;distance+=stride;if(subsampled&&(even||i<fine-1))distance+=stride;}
            interval++;node=next;if(node.Index<input.Segment.End)return false;Row(node,node,true);
            for(int j=0;j<6;j++)buffer.UV[(rows-1)*12+j*2]=(float)distance;
            for(int i=0;i<rows-1;i++)buffer.Connect(i*6,6);
            Data=buffer.Finish(new Double3(ox,-.12,oz));return true;
        }
    }
}
