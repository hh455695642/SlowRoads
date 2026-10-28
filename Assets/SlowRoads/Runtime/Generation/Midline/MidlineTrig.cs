// Adapted from fdlibm, as distributed in V8 10.2.154.26/src/base/ieee754.cc.
// Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
// Developed at SunSoft, a Sun Microsystems, Inc. business.
// Permission to use, copy, modify, and distribute this software is freely
// granted, provided that this notice is preserved.
// Original code modified by Google Inc.; Copyright 2016 the V8 project authors.
using System;

namespace SlowRoads.Generation.Midline
{
    /// <summary>
    /// Scalar fdlibm sin/cos for deterministic Se direction feedback. Mono's CRT
    /// differs from the reference V8 by 1 ULP even at ordinary heading angles.
    /// No allocation per sample. Keep arithmetic order intact.
    /// Supports bounded generator angles, not arbitrary large-argument reduction.
    /// </summary>
    public static partial class MidlineTrig
    {
        static int High(double x) => (int)(BitConverter.DoubleToInt64Bits(x) >> 32);
        static readonly int[] Multiples = {
            0x3FF921FB,0x400921FB,0x4012D97C,0x401921FB,0x401F6A7A,0x4022D97C,
            0x4025FDBB,0x402921FB,0x402C463A,0x402F6A7A,0x4031475C,0x4032D97C,
            0x40346B9C,0x4035FDBB,0x40378FDB,0x403921FB,0x403AB41B,0x403C463A,
            0x403DD85A,0x403F6A7A,0x40407E4C,0x4041475C,0x4042106C,0x4042D97C,
            0x4043A28C,0x40446B9C,0x404534AC,0x4045FDBB,0x4046C6CB,0x40478FDB,
            0x404858EB,0x404921FB };

        public static double Sin(double x)
        {
            int ix = High(x) & 0x7fffffff;
            if (ix <= 0x3FE921FB) return KernelSin(x, 0, false);
            if (ix >= 0x7ff00000) return double.NaN;
            int n = Reduce(x, out double a, out double b);
            switch (n & 3)
            {
                case 0: return KernelSin(a,b,true);
                case 1: return KernelCos(a,b);
                case 2: return -KernelSin(a,b,true);
                default: return -KernelCos(a,b);
            }
        }
        public static double Cos(double x)
        {
            int ix = High(x) & 0x7fffffff;
            if (ix <= 0x3FE921FB) return KernelCos(x,0);
            if (ix >= 0x7ff00000) return double.NaN;
            int n = Reduce(x, out double a, out double b);
            switch (n & 3)
            {
                case 0: return KernelCos(a,b);
                case 1: return -KernelSin(a,b,true);
                case 2: return -KernelCos(a,b);
                default: return KernelSin(a,b,true);
            }
        }
        static double KernelSin(double x, double y, bool tail)
        {
            if ((High(x)&0x7fffffff) < 0x3E400000) return x;
            const double s1=-1.66666666666666324348e-1, s2=8.33333333332248946124e-3,
                s3=-1.98412698298579493134e-4, s4=2.75573137070700676789e-6,
                s5=-2.50507602534068634195e-8, s6=1.58969099521155010221e-10;
            double z=x*x, v=z*x, r=s2+z*(s3+z*(s4+z*(s5+z*s6)));
            return tail ? x-((z*(0.5*y-v*r)-y)-v*s1) : x+v*(s1+z*r);
        }
        static double KernelCos(double x,double y)
        {
            int ix=High(x)&0x7fffffff;
            if(ix<0x3E400000) return 1;
            const double c1=4.16666666666666019037e-2,c2=-1.38888888888741095749e-3,
                c3=2.48015872894767294178e-5,c4=-2.75573143513906633035e-7,
                c5=2.08757232129817482790e-9,c6=-1.13596475577881948265e-11;
            double z=x*x, r=z*(c1+z*(c2+z*(c3+z*(c4+z*(c5+z*c6)))));
            if(ix<0x3FD33333) return 1-(0.5*z-(z*r-x*y));
            double q=ix>0x3FE90000 ? 0.28125 :
                BitConverter.Int64BitsToDouble((long)(ix-0x00200000)<<32);
            double iz=0.5*z-q, a=1-q;
            return a-(iz-(z*r-x*y));
        }
        static int Reduce(double x,out double a,out double b)
        {
            const double p1=1.57079632673412561417, p1t=6.07710050650619224932e-11,
                p2=6.07710050630396597660e-11,p2t=2.02226624879595063154e-21,
                p3=2.02226624871116645580e-21,p3t=8.47842766036889956997e-32;
            int hx=High(x), ix=hx&0x7fffffff;
            if(ix<0x4002D97C)
            {
                double z=x+(hx>0?-p1:p1);
                if(ix==0x3FF921FB)
                {
                    z+=hx>0?-p2:p2; a=z+(hx>0?-p2t:p2t);
                    b=(z-a)+(hx>0?-p2t:p2t);
                }
                else { a=z+(hx>0?-p1t:p1t); b=(z-a)+(hx>0?-p1t:p1t); }
                return hx>0?1:-1;
            }
            if(ix>0x413921FB)
                throw new ArgumentOutOfRangeException(nameof(x),"Generator angle exceeds fdlibm medium reduction domain.");
            double t=Math.Abs(x);
            int n=(int)(t*6.36619772367581382433e-1+0.5);
            double fn=n,r=t-fn*p1,w=fn*p1t;
            a=r-w;
            if(n>=32 || ix==Multiples[n-1])
            {
                int j=ix>>20, loss=j-((High(a)>>20)&0x7ff);
                if(loss>16)
                {
                    t=r; w=fn*p2; r=t-w; w=fn*p2t-((t-r)-w); a=r-w;
                    loss=j-((High(a)>>20)&0x7ff);
                    if(loss>49) { t=r;w=fn*p3;r=t-w;w=fn*p3t-((t-r)-w);a=r-w; }
                }
            }
            b=(r-a)-w;
            if(hx<0) { a=-a;b=-b;return -n; }
            return n;
        }
    }
}
