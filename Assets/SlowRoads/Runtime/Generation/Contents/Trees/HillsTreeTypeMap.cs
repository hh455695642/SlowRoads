using System;
using System.Collections.Generic;
using System.Globalization;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Contents.Trees
{
    /// <summary>Wa.typeMap: Dev ze L00219–00327, Settings L43785.
    /// World-seeded, non-repeated, 3000-unit tiles. Its layer remains zero in Wa.
    /// Bounded FIFO storage is a cache adaptation only; tile values use the original seed address.</summary>
    public sealed class HillsTreeTypeMap : IDisposable
    {
        readonly string seed;
        readonly Dictionary<(int,int),Double3[][]> tiles=new();
        readonly Queue<(int,int)> order=new();
        bool disposed;
        public int CachedTileCount=>tiles.Count;
        public HillsTreeTypeMap(string seed){this.seed=seed??throw new ArgumentNullException(nameof(seed));}
        Double3[][] Tile(int x,int z)
        {
            var key=(x,z);if(tiles.TryGetValue(key,out var tile))return tile;
            var random=new AleaRandom(seed+"#0#"+x.ToString(CultureInfo.InvariantCulture)+"#"+z.ToString(CultureInfo.InvariantCulture));
            tile=new Double3[4][];int resolution=11;
            for(int depth=0;depth<4;depth++,resolution*=2)
            {
                var layer=new Double3[resolution*resolution];
                for(int i=0;i<resolution;i++)for(int j=0;j<resolution;j++)
                {double a=random.Next()*(Math.PI*2);layer[i*resolution+j]=new Double3(MidlineTrig.Cos(a),MidlineTrig.Sin(a),0);}
                tile[depth]=layer;
            }
            if(tiles.Count==32)tiles.Remove(order.Dequeue());tiles.Add(key,tile);order.Enqueue(key);return tile;
        }
        public double GetXZ(double x,double z)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsTreeTypeMap));
            double tx=x/3000,tz=z/3000;int ox=(int)Math.Floor(tx),oz=(int)Math.Floor(tz);
            double px=tx-ox,pz=tz-oz,h=0;int resolution=11;var tile=Tile(ox,oz);
            for(int depth=0;depth<4;depth++,resolution*=2)
            {
                double sx=px*resolution,sz=pz*resolution;int ix=(int)Math.Floor(sx),iz=(int)Math.Floor(sz);
                double qx=sx-ix,qz=sz-iz;
                double n0=Dot(tile[depth],resolution,ix,iz,sx,sz);
                double n1=ix+1<resolution?Dot(tile[depth],resolution,ix+1,iz,sx,sz):Dot(Tile(ox+1,oz)[depth],resolution,0,iz,sx-resolution,sz);
                double i0=Lerp(n0,n1,qx);
                n0=iz+1<resolution?Dot(tile[depth],resolution,ix,iz+1,sx,sz):Dot(Tile(ox,oz+1)[depth],resolution,ix,0,sx,sz-resolution);
                n1=iz+1<resolution?
                    (ix+1<resolution?Dot(tile[depth],resolution,ix+1,iz+1,sx,sz):Dot(Tile(ox+1,oz)[depth],resolution,0,iz+1,sx-resolution,sz)):
                    (ix+1<resolution?Dot(Tile(ox,oz+1)[depth],resolution,ix+1,0,sx,sz-resolution):Dot(Tile(ox+1,oz+1)[depth],resolution,0,0,sx-resolution,sz-resolution));
                double i1=Lerp(n0,n1,qx);h+=Lerp(i0,i1,qz)*(11.0/resolution)*1;
            }
            return h*1+.45;
        }
        public int GetTypeAt(double x,double z)=>Math.Max(0,Math.Min(1,(int)Math.Floor(GetXZ(x,z)+.5)));
        static double Dot(Double3[] a,int res,int ix,int iz,double x,double z){var g=a[ix*res+iz];return (x-ix)*g.X+(z-iz)*g.Y;}
        static double Lerp(double a,double b,double t)=>(b-a)*((t*(t*6-15)+10)*t*t*t)+a;
        public void Dispose(){if(disposed)return;tiles.Clear();order.Clear();disposed=true;}
    }
}
