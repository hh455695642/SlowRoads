using System;

namespace SlowRoads.Generation.Midline
{
    /// <summary>
    /// Archived OE/yd open four-point centripetal curve, Normals L10970–L11001.
    /// Preserves Math.pow(distanceSquared,.25), knot fallback and polynomial order.
    /// </summary>
    public sealed class CentripetalCurve
    {
        readonly Double3[] points = new Double3[4];
        public void Set(Double3 p0,Double3 p1,Double3 p2,Double3 p3)
        { points[0]=p0;points[1]=p1;points[2]=p2;points[3]=p3; }

        public Double3 GetPoint(double parameter)
        {
            if(parameter<0 || parameter>1) throw new ArgumentOutOfRangeException(nameof(parameter));
            double scaled=3*parameter;
            int segment=(int)Math.Floor(scaled);
            double weight=scaled-segment;
            if(weight==0 && segment==3) { segment=2;weight=1; }
            Double3 p1=points[segment],p2=points[segment+1];
            Double3 p0=segment>0?points[segment-1]:Extrapolate(points[0],points[1]);
            Double3 p3=segment+2<4?points[segment+2]:Extrapolate(points[3],points[2]);
            double k0=Math.Pow(DistanceSquared(p0,p1),0.25);
            double k1=Math.Pow(DistanceSquared(p1,p2),0.25);
            double k2=Math.Pow(DistanceSquared(p2,p3),0.25);
            if(k1<1e-4) k1=1;
            if(k0<1e-4) k0=k1;
            if(k2<1e-4) k2=k1;
            return new Double3(Evaluate(p0.X,p1.X,p2.X,p3.X,k0,k1,k2,weight),
                Evaluate(p0.Y,p1.Y,p2.Y,p3.Y,k0,k1,k2,weight),
                Evaluate(p0.Z,p1.Z,p2.Z,p3.Z,k0,k1,k2,weight));
        }
        static Double3 Extrapolate(Double3 a,Double3 b) => new Double3(a.X-b.X+a.X,a.Y-b.Y+a.Y,a.Z-b.Z+a.Z);
        static double DistanceSquared(Double3 a,Double3 b)
        { double x=a.X-b.X,y=a.Y-b.Y,z=a.Z-b.Z;return x*x+y*y+z*z; }
        static double Evaluate(double p0,double p1,double p2,double p3,double k0,double k1,double k2,double t)
        {
            double m0=(p1-p0)/k0-(p2-p0)/(k0+k1)+(p2-p1)/k1;
            double m1=(p2-p1)/k1-(p3-p1)/(k1+k2)+(p3-p2)/k2;
            m0*=k1;m1*=k1;
            double a=p1,b=m0,c=-3*p1+3*p2-2*m0-m1,d=2*p1-2*p2+m0+m1;
            double t2=t*t,t3=t2*t;
            return a+b*t+c*t2+d*t3;
        }
    }
}
