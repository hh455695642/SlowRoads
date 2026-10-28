using System;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Midline
{
    /// <summary>
    /// H7's fixed repeated ze profile (Settings L47084–L47093; Dev L00219–L00327).
    /// One immutable prototype replaces equivalent repeated cache entries. It owns
    /// a private seed-a Alea and never consumes the road's random streams.
    /// </summary>
    public sealed class HillsBlendNoise
    {
        readonly Double3[][] gradients=new Double3[3][];
        public HillsBlendNoise()
        {
            var random=new AleaRandom("a");
            int resolution=5;
            for(int depth=0;depth<3;depth++)
            {
                var layer=new Double3[resolution*resolution];
                for(int x=0;x<resolution;x++) for(int z=0;z<resolution;z++)
                {
                    double angle=random.Next()*(Math.PI*2);
                    layer[x*resolution+z]=new Double3(Math.Cos(angle),Math.Sin(angle),0);
                }
                gradients[depth]=layer;resolution*=3;
            }
        }
        public double GetXZ(double x,double z)
        {
            double tx=x/400,tz=z/400;
            double px=tx-Math.Floor(tx),pz=tz-Math.Floor(tz),height=0;
            int resolution=5;
            for(int depth=0;depth<3;depth++)
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
                height+=Lerp(int0,int1,qz)*(5.0/resolution)*1;
                resolution*=3;
            }
            return height*1+0.5;
        }
        static double Dot(Double3[] layer,int resolution,int ix,int iz,double x,double z)
        { var g=layer[ix*resolution+iz];return (x-ix)*g.X+(z-iz)*g.Y; }
        static double Lerp(double a,double b,double t)=> (b-a)*((t*(t*6-15)+10)*t*t*t)+a;

        public void Apply(CoarseNode node)
        {
            if(node.HasBlend && node.Blend!=0 && !double.IsNaN(node.Blend)) return;
            double clamped=Math.Min(Math.Max(0,GetXZ(node.Position.X,node.Position.Z)),1);
            node.Blend=1-clamped*clamped;
            if(node.Bridge) node.Blend*=0.1;
            if(node.Previous!=null && node.Previous.HasBlend && node.Previous.Blend!=0 && !double.IsNaN(node.Previous.Blend))
                node.Blend=node.Previous.Blend*0.5+node.Blend*0.5;
            node.HasBlend=true;
        }
    }
}
