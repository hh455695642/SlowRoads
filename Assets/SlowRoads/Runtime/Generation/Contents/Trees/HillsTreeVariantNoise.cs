using System;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Contents.Trees
{
    /// <summary>Original h5(4,4), Settings L43305–43363/L43757. Cubic interpolation,
    /// seam duplicates and the original amplitude expression; not the repeated ze profile.</summary>
    public sealed class HillsTreeVariantNoise
    {
        readonly Double3[][] layers=new Double3[4][];
        readonly double amplitude;
        public HillsTreeVariantNoise()
        {
            var random=new AleaRandom("perlin");int resolution=4;
            for(int depth=0;depth<4;depth++,resolution*=2)
            {
                int stride=resolution+1;var points=new Double3[stride*stride];
                for(int x=0;x<resolution;x++)
                {
                    for(int z=0;z<resolution;z++)
                    {double angle=random.Next()*(Math.PI*2);points[x*stride+z]=new Double3(MidlineTrig.Cos(angle),MidlineTrig.Sin(angle),0);}
                    points[x*stride+resolution]=points[x*stride];
                }
                Array.Copy(points,0,points,resolution*stride,stride);layers[depth]=points;
            }
            double sum=0,value=1;for(int depth=0;depth<4;depth++){sum+=value;value/=2;}
            amplitude=.5+(1/sum)*.5;
        }
        public double Get(double x,double z)
        {
            x-=Math.Floor(x);z-=Math.Floor(z);double height=0,a=amplitude;int resolution=4;
            foreach(var layer in layers)
            {
                double sx=x*resolution,sz=z*resolution;int ix=(int)Math.Floor(sx),iz=(int)Math.Floor(sz),stride=resolution+1;
                double qx=sx-ix,qz=sz-iz;
                double n0=Dot(layer[ix*stride+iz],ix,iz,sx,sz),n1=Dot(layer[(ix+1)*stride+iz],ix+1,iz,sx,sz);
                double n2=Dot(layer[ix*stride+iz+1],ix,iz+1,sx,sz),n3=Dot(layer[(ix+1)*stride+iz+1],ix+1,iz+1,sx,sz);
                height+=Lerp(Lerp(n0,n1,qx),Lerp(n2,n3,qx),qz)*a;a/=2;resolution*=2;
            }
            return height+.5;
        }
        static double Dot(Double3 g,int x,int z,double sx,double sz)=>(sx-x)*g.X+(sz-z)*g.Y;
        static double Lerp(double a,double b,double t)=>a+(b-a)*(t*t*(3-2*t));
    }
}
