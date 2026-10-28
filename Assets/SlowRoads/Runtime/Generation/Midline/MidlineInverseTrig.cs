// Adapted from fdlibm / V8 10.2.154.26/src/base/ieee754.cc.
// Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
// Developed at SunSoft, a Sun Microsystems, Inc. business.
// Permission to use, copy, modify, and distribute this software is freely
// granted, provided that this notice is preserved.
// Original code modified by Google Inc.; Copyright 2016 the V8 project authors.
using System;

namespace SlowRoads.Generation.Midline
{
    public static partial class MidlineTrig
    {
        static readonly double[] AtanHigh = {4.63647609000806093515e-1,7.85398163397448278999e-1,
            9.82793723247329054082e-1,1.57079632679489655800};
        static readonly double[] AtanLow = {2.26987774529616870924e-17,3.06161699786838301793e-17,
            1.39033110312309984516e-17,6.12323399573676603587e-17};
        static readonly double[] AtanCoefficients = {3.33333333333329318027e-1,-1.99999999998764832476e-1,
            1.42857142725034663711e-1,-1.11111104054623557880e-1,9.09088713343650656196e-2,
            -7.69187620504482999495e-2,6.66107313738753120669e-2,-5.83357013379057348645e-2,
            4.97687799461593236017e-2,-3.65315727442169155270e-2,1.62858201153657823623e-2};

        static double Atan(double x)
        {
            int hx=High(x),ix=hx&0x7fffffff,id;
            if(ix>=0x44100000)
                return double.IsNaN(x)?x+x:hx>0?AtanHigh[3]+AtanLow[3]:-AtanHigh[3]-AtanLow[3];
            if(ix<0x3FDC0000) { if(ix<0x3E400000) return x; id=-1; }
            else
            {
                x=Math.Abs(x);
                if(ix<0x3FF30000)
                {
                    if(ix<0x3FE60000) { id=0;x=(2*x-1)/(2+x); }
                    else { id=1;x=(x-1)/(x+1); }
                }
                else if(ix<0x40038000) { id=2;x=(x-1.5)/(1+1.5*x); }
                else { id=3;x=-1/x; }
            }
            double z=x*x,w=z*z;
            var a=AtanCoefficients;
            double s1=z*(a[0]+w*(a[2]+w*(a[4]+w*(a[6]+w*(a[8]+w*a[10])))));
            double s2=w*(a[1]+w*(a[3]+w*(a[5]+w*(a[7]+w*a[9]))));
            if(id<0) return x-x*(s1+s2);
            z=AtanHigh[id]-((x*(s1+s2)-AtanLow[id])-x);
            return hx<0?-z:z;
        }
        public static double Atan2(double y,double x)
        {
            if(double.IsNaN(x)||double.IsNaN(y)) return x+y;
            if(x==1) return Atan(y);
            int hx=High(x),hy=High(y),ix=hx&0x7fffffff,iy=hy&0x7fffffff;
            int m=((hy>>31)&1)|((hx>>30)&2);
            if(y==0) return m<2?y:m==2?Math.PI:-Math.PI;
            if(x==0) return hy<0?-Math.PI/2:Math.PI/2;
            if(double.IsInfinity(x))
            {
                if(double.IsInfinity(y))
                    return m==0?Math.PI/4:m==1?-Math.PI/4:m==2?3*(Math.PI/4):-3*(Math.PI/4);
                return m==0?0.0:m==1?-0.0:m==2?Math.PI:-Math.PI;
            }
            if(double.IsInfinity(y)) return hy<0?-Math.PI/2:Math.PI/2;
            const double piLow=1.2246467991473531772e-16;
            int k=(iy-ix)>>20;
            double z;
            if(k>60) { z=Math.PI/2+0.5*piLow;m&=1; }
            else if(hx<0 && k < -60) z=0;
            else z=Atan(Math.Abs(y/x));
            return m==0?z:m==1?-z:m==2?Math.PI-(z-piLow):(z-piLow)-Math.PI;
        }
        public static double Acos(double x)
        {
            int hx=High(x),ix=hx&0x7fffffff;
            const double low=6.12323399573676603587e-17;
            if(Math.Abs(x)>=1 || double.IsNaN(x))
                return x==1?0:x== -1?Math.PI+2*low:double.NaN;
            if(ix<0x3FE00000)
            {
                if(ix<=0x3C600000) return Math.PI/2+low;
                double r=AcosRatio(x*x);
                return Math.PI/2-(x-(low-x*r));
            }
            if(hx<0)
            {
                double z=(1+x)*0.5,s=Math.Sqrt(z),r=AcosRatio(z),w=r*s-low;
                return Math.PI-2*(s+w);
            }
            else
            {
                double z=(1-x)*0.5,s=Math.Sqrt(z);
                double df=BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(s)&unchecked((long)0xffffffff00000000));
                double c=(z-df*df)/(s+df),r=AcosRatio(z),w=r*s+c;
                return 2*(df+w);
            }
        }
        static double AcosRatio(double z)
        {
            double p=z*(1.66666666666666657415e-1+z*(-3.25565818622400915405e-1+
                z*(2.01212532134862925881e-1+z*(-4.00555345006794114027e-2+
                z*(7.91534994289814532176e-4+z*3.47933107596021167570e-5)))));
            double q=1+z*(-2.40339491173441421878+z*(2.02094576023350569471+
                z*(-6.88283971605453293030e-1+z*7.70381505559019352791e-2)));
            return p/q;
        }
    }
}
