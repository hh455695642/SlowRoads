using System;
using System.Globalization;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Contents.Walls
{
    /// <summary>K4 L48605–49014. User-authorized adaptation (2026-11-02): only the two
    /// Math.random disturbances use an independent per-wall Alea input. Geometry formulas stay original.</summary>
    public sealed class FenceGeometryBuilder
    {
        readonly WallGeometryInput input;readonly WallGeometryBuffer fence=new(),posts=new();readonly Func<double> next;
        readonly double ox,oz;readonly int side;CoarseNode node;int interval,rows;
        public WallGeometryData Data {get;private set;}
        public static string RandomAddress(string seed,WallSegment s)=>seed.Length.ToString(CultureInfo.InvariantCulture)+":"+seed+"|K4|"+
            s.Start.ToString(CultureInfo.InvariantCulture)+"|"+s.End.ToString(CultureInfo.InvariantCulture)+"|"+BitConverter.DoubleToInt64Bits(s.Distance).ToString("x16",CultureInfo.InvariantCulture);
        public FenceGeometryBuilder(WallGeometryInput input,string seed,Func<double> replay=null)
        {
            this.input=input;if(input.Segment.Type!=HillsWallType.Fence)throw new ArgumentException("K4 type mismatch");
            var rng=new AleaRandom(RandomAddress(seed??throw new ArgumentNullException(nameof(seed)),input.Segment));next=replay??rng.Next;
            node=input.Start;ox=node.Position.X;oz=node.Position.Z;side=Math.Sign(input.Segment.Distance);
        }
        void Row(Double3 p,Double3 n,double shadow,bool last,bool post)
        {
            double x=p.X+n.X*input.Segment.Distance-ox,z=p.Z+n.Z*input.Segment.Distance-oz,
                x1=x+n.X*(last?.1:.08)*side,z1=z+n.Z*(last?.1:.08)*side,elev=p.Y;
            if(input.RoadsideHeight!=null)elev=input.RoadsideHeight(x+ox,z+oz,node);shadow*=2.5;
            double a=last?0:next()*.06-.03,b=last?0:next()*.06-.03;
            V(x,elev+a,z,0,n,shadow);V(x,elev+.56+a,z,.52,n,shadow);V(x1,elev+.56+a,z1,.56,n,shadow);
            V(x,elev+.8+b,z,.8,n,shadow);V(x,elev+1+b,z,.956,n,shadow);V(x1,elev+1+b,z1,1,n,shadow);
            if(post)Post(x,z,elev,n,shadow);rows++;
        }
        void V(double x,double y,double z,double v,Double3 n,double shadow)
        {fence.Vertex(x,y,z,rows,v*.4,shadow);fence.Normals.Add((float)(n.X*-side));fence.Normals.Add(0);fence.Normals.Add((float)(n.Z*-side));}
        void Post(double x,double z,double y,Double3 n,double shadow)
        {
            int vertex=posts.Positions.Count/3;
            double xa=x-n.X*.02*side+-n.Z*.06,za=z-n.Z*.02*side+n.X*.06,
                xb=x+n.X*.11*side+-n.Z*.06,zb=z+n.Z*.11*side+n.X*.06,
                xc=x+n.X*.11*side- -n.Z*.06,zc=z+n.Z*.11*side-n.X*.06,
                xd=x-n.X*.02*side- -n.Z*.06,zd=z-n.Z*.02*side-n.X*.06;
            P(xa,y,za,.25,1,shadow);P(xa,y+1+.03,za,.25,.5,shadow);
            P(xb,y,zb,.5,1,shadow);P(xb,y+1+.03,zb,.5,.5,shadow);
            P(xc,y,zc,.75,1,shadow);P(xc,y+1+.03,zc,.75,.5,shadow);
            P(xd,y,zd,0,1,shadow);P(xd,y+1+.03,zd,0,.5,shadow);P(xd,y+1+.03,zd,.5,.42,shadow);
            foreach(int i in new[]{0,3,1,0,2,3,2,5,3,2,4,5,4,7,5,4,6,7,6,1,7,6,0,1,1,3,8,3,5,8})posts.Indices.Add(vertex+i);
        }
        void P(double x,double y,double z,double u,double v,double shadow)
        {posts.Vertex(x,y,z,u,v,shadow);posts.Normals.Add(0);posts.Normals.Add(1);posts.Normals.Add(0);}
        public bool Step()
        {
            if(Data!=null)return true;var nextNode=node.Next??throw new InvalidOperationException("K4 next sentinel missing");Double3 midpoint,midnormal;
            if(node.FineSegments<2)
            {midpoint=new Double3((node.Position.X+nextNode.Position.X)/2,(node.Position.Y+nextNode.Position.Y)/2,(node.Position.Z+nextNode.Position.Z)/2);
                midnormal=new Double3(-(nextNode.Position.Z-node.Position.Z),0,nextNode.Position.X-node.Position.X).Normalize();}
            else if(node.FineSegments==2){midpoint=node.NextFine.Position;midnormal=node.NextFine.Normal;}
            else
            {
                MidlinePathNode fn=node;for(int i=0;i<node.FineSegments/2.0;i++)fn=fn.NextFine??throw new InvalidOperationException("K4 fine support missing");
                midpoint=fn.Position;
                if(node.FineSegments%2!=0)midpoint=new Double3((fn.Position.X+fn.PreviousFine.Position.X)/2,(fn.Position.Y+fn.PreviousFine.Position.Y)/2,(fn.Position.Z+fn.PreviousFine.Position.Z)/2);
                midnormal=fn.Normal;
            }
            double shadow=side<0?node.ShadowLeft:node.ShadowRight;
            Row(node.Position,node.Normal,shadow,false,interval>0||input.Segment.ContiguousStart);
            Row(midpoint,midnormal,shadow,false,true);interval++;node=nextNode;
            if(node.Index<input.Segment.End)return false;
            Row(node.Position,node.Normal,side<0?node.ShadowLeft:node.ShadowRight,true,false);
            for(int i=0;i<rows-1;i++)fence.Connect(i*6,6);
            if(!input.Segment.ContiguousStart)for(int i=0;i<6;i++)fence.Positions[i*3+1]=(float)(fence.Positions[i*3+1]-1.0);
            if(!input.Segment.ContiguousEnd)for(int i=0;i<6;i++){int at=fence.Positions.Count-(i*3+2);fence.Positions[at]=(float)(fence.Positions[at]-1.0);}
            int postStart=fence.Positions.Count/3;fence.Positions.AddRange(posts.Positions);fence.Normals.AddRange(posts.Normals);fence.UV.AddRange(posts.UV);fence.Shadow.AddRange(posts.Shadow);
            foreach(int i in posts.Indices)fence.Indices.Add(postStart+i);Data=fence.Finish(new Double3(ox,-.15,oz),true);return true;
        }
    }
}
