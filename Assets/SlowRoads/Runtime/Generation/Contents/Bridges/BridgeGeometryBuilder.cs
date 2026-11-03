using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Bridges
{
    public sealed class BridgeGeometryData
    {
        public readonly float[] Positions,UV,ShadowPositions;public readonly int[] Indices,ShadowIndices;
        public readonly int FarCount,NearStart,NearCount;public readonly BridgeInput Input;
        internal BridgeGeometryData(BridgeInput input,List<double> positions,List<double> uv,List<double> shadows,List<int> deck,List<int> arch0,List<int> pylons,List<int> arch1,List<int> shadowIndices)
        {
            Input=input;Positions=Float(positions);UV=Float(uv);ShadowPositions=Float(shadows);ShadowIndices=shadowIndices.ToArray();
            var all=new List<int>(deck);all.AddRange(arch0);NearStart=all.Count;all.AddRange(pylons);FarCount=all.Count;all.AddRange(arch1);Indices=all.ToArray();NearCount=Indices.Length-NearStart;
        }
        static float[] Float(List<double> a){var f=new float[a.Count];for(int i=0;i<a.Count;i++)f[i]=(float)a[i];return f;}
    }
    /// <summary>Settings J9.generateProtoArchIndex L49044–49059 and ii L49135–49460.
    /// Double geometry; original Float32 conversion is at publication. No random consumption.</summary>
    public sealed class BridgeGeometryBuilder
    {
        readonly BridgeInput input;readonly double width;readonly int segments,gap;readonly bool arches;
        readonly double[] curve;readonly List<int> proto0=new(),proto1=new();
        readonly List<double> positions=new(),uv=new(),shadows=new();
        readonly List<int> deck=new(),arch0=new(),pylons=new(),arch1=new(),shadowIndices=new();
        int cursor,vertex,shadowVertex;bool hasDeck;
        public BridgeGeometryData Data {get;private set;}
        public int ArchGap=>gap;public int Length=>input.Tail.Index-input.Head.Index;
        public readonly double DisplayIndex,UpgradeIndex,DowngradeIndex,RetireIndex;
        static readonly double[][] Curves={new[]{1,.25,0,.25,1},new[]{1,.444,.111,0,.111,.444,1},
            new[]{1,.565,.25,.0625,0,.0625,.25,.565,1},new[]{1,.64,.36,.16,.04,0,.04,.16,.36,.64,1}};
        public BridgeGeometryBuilder(BridgeInput input,double halfWidth,int segments,bool arches=true,int lookahead=160)
        {
            this.input=input??throw new ArgumentNullException(nameof(input));if(!double.IsFinite(halfWidth)||halfWidth<=0||segments!=4&&segments!=6&&segments!=8&&segments!=10)throw new ArgumentOutOfRangeException();
            width=halfWidth;this.segments=segments;this.arches=arches;curve=Curves[(segments-4)/2];gap=Length<4?Length:(int)Math.Ceiling(Length/10.0);
            DisplayIndex=input.Head.Index-lookahead;UpgradeIndex=input.Head.Index-lookahead*.4;DowngradeIndex=input.Tail.Index+lookahead*.1;RetireIndex=DowngradeIndex+lookahead*.2;
            for(int a=0;a<segments;a++)
            {
                int t=a*2;proto1.AddRange(new[]{4+t,5+t,7+t,4+t,7+t,6+t});
                if(a<segments/2)proto1.AddRange(new[]{4+t,6+t,0,5+t,1,7+t});else{if(a==segments/2)proto1.AddRange(new[]{0,4+t,2,1,3,5+t});proto1.AddRange(new[]{4+t,6+t,2,5+t,3,7+t});}
                if(a%2==0)
                {proto0.AddRange(new[]{4+t,5+t,9+t,4+t,9+t,8+t});if(a<segments/2)proto0.AddRange(new[]{4+t,8+t,0,5+t,1,9+t});
                    else{if(a==segments/2)proto0.AddRange(new[]{0,4+t,2,1,3,5+t});proto0.AddRange(new[]{4+t,8+t,2,5+t,3,9+t});}}
                else if(a==segments/2)proto0.AddRange(new[]{0,6+t,2,1,3,7+t});
            }
        }
        void V(double x,double y,double z,double u,double v){positions.Add(x-input.Head.Position.X);positions.Add(y);positions.Add(z-input.Head.Position.Z);uv.Add(u);uv.Add(v);}
        void I(List<int> target,params int[] indices){foreach(int i in indices)target.Add(vertex+i);}
        int Next(int at)=>Length<4?input.Count-1:Math.Min(input.Count-1,at+gap);
        public bool Step()
        {
            if(Data!=null)return true;
            if(!hasDeck)
            {
                Deck(input.Head);
                do
                {
                    int next=Next(cursor);Deck(input[next]);I(deck,0,4,1,1,4,5,1,5,2,2,5,6,2,6,3,3,6,7);vertex+=4;
                    foreach(int i in new[]{0,2,1,1,2,3})shadowIndices.Add(shadowVertex+i);shadowVertex+=2;cursor=next;
                }while(cursor<input.Count-1);
                cursor=0;hasDeck=true;vertex+=4;return false;
            }
            int end=Next(cursor);if(arches)Arch(input[cursor],input[end]);if(end<input.Count-1)Pylon(input[end]);cursor=end;
            if(cursor<input.Count-1)return false;
            Data=new BridgeGeometryData(input,positions,uv,shadows,deck,arch0,pylons,arch1,shadowIndices);return true;
        }
        void Deck(BridgePoint n)
        {
            double nx=width*n.Normal.X,nz=width*n.Normal.Z,u=n.Index-input.Head.Index;
            V(n.Position.X+nx,n.Position.Y+.75,n.Position.Z+nz,u,.8);V(n.Position.X+nx,n.Position.Y,n.Position.Z+nz,u,.6);
            V(n.Position.X-nx,n.Position.Y,n.Position.Z-nz,u,.6);V(n.Position.X-nx,n.Position.Y+.75,n.Position.Z-nz,u,.8);
            shadows.Add(n.Position.X+nx-input.Head.Position.X);shadows.Add(.01);shadows.Add(n.Position.Z+nz-input.Head.Position.Z);
            shadows.Add(n.Position.X-nx-input.Head.Position.X);shadows.Add(.01);shadows.Add(n.Position.Z-nz-input.Head.Position.Z);
        }
        void Pylon(BridgePoint n)
        {
            double nx=(width+.3)*n.Normal.X,nz=(width+.3)*n.Normal.Z,fx=-n.Normal.Z*.6,fz=n.Normal.X*.6,fxf=fx*1.75,fzf=fz*1.75;
            double uvDist=.6*1.75/(width*2+.6),uvWidth=(n.Position.Y- -1)/(width*2+.6)*2;uvWidth=1/uvWidth;uvDist*=2;
            V(n.Position.X+nx*.95+fx,n.Position.Y-.1,n.Position.Z+nz*.95+fz,0,.5);V(n.Position.X+nx+fxf,-1,n.Position.Z+nz+fzf,0,1);
            V(n.Position.X+nx*.95-fx,n.Position.Y-.1,n.Position.Z+nz*.95-fz,uvDist*uvWidth,.5);V(n.Position.X+nx-fxf,-1,n.Position.Z+nz-fzf,uvDist*uvWidth,1);
            V(n.Position.X-nx*.95-fx,n.Position.Y-.1,n.Position.Z-nz*.95-fz,uvWidth,.5);V(n.Position.X-nx-fxf,-1,n.Position.Z-nz-fzf,uvWidth,1);
            V(n.Position.X-nx*.95+fx,n.Position.Y-.1,n.Position.Z-nz*.95+fz,(1-uvDist)*uvWidth,.5);V(n.Position.X-nx+fxf,-1,n.Position.Z-nz+fzf,(1-uvDist)*uvWidth,1);
            I(pylons,0,1,3,0,3,2,2,3,5,2,5,4,4,5,7,4,7,6,6,7,1,6,1,0);vertex+=8;
        }
        void Arch(BridgePoint a,BridgePoint b)
        {
            double nx=width*a.Normal.X,nz=width*a.Normal.Z,sx=b.Position.X-a.Position.X,sy=b.Position.Y-a.Position.Y,sz=b.Position.Z-a.Position.Z;
            V(a.Position.X+nx,a.Position.Y,a.Position.Z+nz,0,.5);V(a.Position.X-nx,a.Position.Y,a.Position.Z-nz,0,-.5);
            V(b.Position.X+nx,b.Position.Y,b.Position.Z+nz,2,.5);V(b.Position.X-nx,b.Position.Y,b.Position.Z-nz,2,-.5);
            for(int i=0;i<=segments;i++)
            {double t=(double)i/segments,x=a.Position.X+sx*t,y=a.Position.Y+sy*t-1-3*curve[i],z=a.Position.Z+sz*t;
                V(x+nx,y,z+nz,t*2,.5-(curve[i]*.25+.15));V(x-nx,y,z-nz,t*2,-.5+(curve[i]*.25+.15));}
            foreach(int i in proto1)arch1.Add(i+vertex);foreach(int i in proto0)arch0.Add(i+vertex);vertex+=(segments+1)*2+4;
        }
    }
}
