using System;
using SlowRoads.Generation.Random;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Heightfield
{
    /// <summary>
    /// Fixed repeated ze profiles (Settings L47084–L47093; Dev L00219–L00327).
    /// One immutable prototype replaces equivalent repeated cache entries. It owns
    /// a private seed-a Alea and never consumes the road's random streams.
    /// </summary>
    public sealed class RepeatedGradientNoise
    {
        readonly Double3[][] gradients;
        readonly int initialResolution,upscale,depthCount;
        readonly double tileSize,scale,offset;
        public RepeatedGradientNoise(int resolution=5,int depth=3,int upscale=3,double tileSize=400,double scale=1,double offset=.5)
        {
            if(resolution<1||depth<1||upscale<1||tileSize<=0)throw new ArgumentOutOfRangeException();
            initialResolution=resolution;this.upscale=upscale;depthCount=depth;this.tileSize=tileSize;this.scale=scale;this.offset=offset;gradients=new Double3[depth][];
            var random=new AleaRandom("a");
            for(int layerIndex=0;layerIndex<depthCount;layerIndex++)
            {
                var layer=new Double3[resolution*resolution];
                for(int x=0;x<resolution;x++) for(int z=0;z<resolution;z++)
                {
                    double angle=random.Next()*(Math.PI*2);
                    layer[x*resolution+z]=new Double3(Math.Cos(angle),Math.Sin(angle),0);
                }
                gradients[layerIndex]=layer;resolution*=upscale;
            }
        }
        public double GetXZ(double x,double z)
        {
            double tx=x/tileSize,tz=z/tileSize;
            double px=tx-Math.Floor(tx),pz=tz-Math.Floor(tz),height=0;
            int resolution=initialResolution;
            for(int depth=0;depth<depthCount;depth++)
            {
                double sx=px*resolution,sz=pz*resolution;
                int ix=(int)Math.Floor(sx),iz=(int)Math.Floor(sz);
                double qx=sx-ix,qz=sz-iz;
                var layer=gradients[depth];
                double n0=Dot(layer,resolution,ix,iz,sx,sz);
                double n1=ix+1<resolution?Dot(layer,resolution,ix+1,iz,sx,sz):
                    Dot(layer,resolution,0,iz,sx-resolution,sz);
                double int0=Lerp(n0,n1,qx);
                n0=iz+1<resolution?Dot(layer,resolution,ix,iz+1,sx,sz):
                    Dot(layer,resolution,ix,0,sx,sz-resolution);
                n1=iz+1<resolution ? (ix+1<resolution?Dot(layer,resolution,ix+1,iz+1,sx,sz):
                    Dot(layer,resolution,0,iz+1,sx-resolution,sz)) :
                    (ix+1<resolution?Dot(layer,resolution,ix+1,0,sx,sz-resolution):
                    Dot(layer,resolution,0,0,sx-resolution,sz-resolution));
                double int1=Lerp(n0,n1,qx);
                height+=Lerp(int0,int1,qz)*((double)initialResolution/resolution)*1;
                resolution*=upscale;
            }
            return height*scale+offset;
        }
        static double Dot(Double3[] layer,int resolution,int ix,int iz,double x,double z)
        { var g=layer[ix*resolution+iz];return (x-ix)*g.X+(z-iz)*g.Y; }
        static double Lerp(double a,double b,double t)=> (b-a)*((t*(t*6-15)+10)*t*t*t)+a;

    }
}
