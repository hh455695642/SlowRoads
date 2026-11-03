using System;
using System.Collections.Generic;
using System.Globalization;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Random;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Ta: Dev ze defaults, Settings L43118. Fixed seed "a", one layer, resolution 2.
    /// Independent bounded cache; does not reseed Wa or alter the height field.</summary>
    public sealed class FieldQualityNoise
    {
        readonly Dictionary<(int,int),Double3[]> tiles=new();readonly Queue<(int,int)> order=new();
        Double3[] Tile(int x,int z)
        {
            if(tiles.TryGetValue((x,z),out var tile))return tile;
            var rng=new AleaRandom("a#0#"+x.ToString(CultureInfo.InvariantCulture)+"#"+z.ToString(CultureInfo.InvariantCulture));tile=new Double3[4];
            for(int i=0;i<4;i++){double angle=rng.Next()*(Math.PI*2);tile[i]=new Double3(MidlineTrig.Cos(angle),MidlineTrig.Sin(angle),0);}
            if(tiles.Count==32)tiles.Remove(order.Dequeue());tiles.Add((x,z),tile);order.Enqueue((x,z));return tile;
        }
        double Dot(int ox,int oz,int ix,int iz,double x,double z)
        {if(ix==2){ox++;ix=0;x-=2;}if(iz==2){oz++;iz=0;z-=2;}var g=Tile(ox,oz)[ix*2+iz];return (x-ix)*g.X+(z-iz)*g.Y;}
        static double Lerp(double a,double b,double t)=>(b-a)*((t*(t*6-15)+10)*t*t*t)+a;
        public double GetXZ(double x,double z)
        {
            double tx=x/3000,tz=z/3000;int ox=(int)Math.Floor(tx),oz=(int)Math.Floor(tz);double sx=(tx-ox)*2,sz=(tz-oz)*2;
            int ix=(int)Math.Floor(sx),iz=(int)Math.Floor(sz);double qx=sx-ix,qz=sz-iz;
            double a=Lerp(Dot(ox,oz,ix,iz,sx,sz),Dot(ox,oz,ix+1,iz,sx,sz),qx),
                b=Lerp(Dot(ox,oz,ix,iz+1,sx,sz),Dot(ox,oz,ix+1,iz+1,sx,sz),qx);
            return Lerp(a,b,qz)+.5;
        }
    }
}
