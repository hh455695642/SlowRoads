using System;
using SlowRoads.Generation.Heightfield;
using SlowRoads.Generation.Road;
using SlowRoads.Generation.Spatial;

namespace SlowRoads.Generation.Terrain
{
    /// <summary>Hills Ll, L06045–L06162. CPU data query; no rendering pass or RT.</summary>
    public sealed class HillsRoadBlend
    {
        readonly RepeatedGradientNoise wl=new(7,2,3,40,.03,0),jt=new(5,3,3,25,.5,0),
            bl=new(5,2,2,200,5,0),ml=new(7,2,3,100,8,1);
        readonly bool offRoad,single;
        readonly double width,verge=0.4,raise,edge,lip;
        public HillsRoadBlend(HillsLaneStyle lane)
        {
            width=new RoadMeshConfig(lane,1).HalfWidth;
            offRoad=lane==HillsLaneStyle.OffRoad||lane==HillsLaneStyle.OffRoadWide;
            single=lane==HillsLaneStyle.OffRoad;
            raise=offRoad?(single?.25:1):3;edge=offRoad?(single?.4:1):2;lip=single?.1:.2;
        }
        public static double RoadLerp(double t)=>t>.3333?1-Math.Pow(1-(t-.3333)/.6667,2)*.5:t*1.5;
        static double Smooth(double t)=>(t*(t*6-15)+10)*t*t*t;
        public double[] SampleNoise(double x,double z)=>new[]{wl.GetXZ(x,z),jt.GetXZ(x,z),bl.GetXZ(x,z),ml.GetXZ(x,z)};

        // Extension point for near-road terrain: caller supplies H (e.g. corner bilerp).
        // Preserve Ll write-back semantics in p, including its perturbed distance and Y.
        public double Evaluate(double h,ref RoadProjection p,double x,double z)
        {
            if(!double.IsFinite(h)||!double.IsFinite(x)||!double.IsFinite(z))throw new ArgumentOutOfRangeException();
            double ga=p.Side==Math.Sign(p.Grade)?Math.Max(0,Math.Abs(p.InterpolatedGrade)-1)/3:0;
            p.OriginalDistance=p.Distance;double widthLerp=1;
            if(offRoad){p.Width=width+(single?verge*2:verge+ga);if(p.Distance<p.Width)widthLerp=p.Distance/p.Width;
                p.Distance+=.4;if(single){p.Distance+=.2;p.Distance+=jt.GetXZ(x,z)*.5;}else p.Distance+=jt.GetXZ(x,z);}
            else p.Width=width+verge+.25+ga;
            p.Y-=.01+.01*p.ForwardGrade;ga=Math.Pow(Math.Min(1,Math.Max(0,ga)),2);p.GradeAdjustment=ga;
            double rm=(20-width)*(.4+.6*p.Blend),l=(p.Distance-p.Width)/16;
            l=Math.Max(0,Math.Min(1,l));l=Math.Pow(1-l,2);
            if(l>0){h+=l*bl.GetXZ(x,z);if(ga>0)h+=ml.GetXZ(x,z)*l*ga;}
            if(p.Distance<p.Width+rm)
            {
                if(!offRoad&&p.Distance<width/2&&!p.Bridge)return p.Y;
                double lt=RoadLerp(Math.Max(0,Math.Min(1,(p.Distance-p.Width)/rm)));
                if(offRoad)
                {
                    double f=1-ga;if(f>0)h+=(p.OriginalDistance<p.Width?raise:Smooth(1-(p.OriginalDistance-p.Width)/rm)*raise)*f;
                    if(p.Distance<p.Width-edge){if(single&&p.Distance<1.7)p.Y+=Smooth(1-p.Distance/1.7)*lip*2;}
                    else if(p.Distance<p.Width){double i=1-(p.Width-p.Distance)/edge;i*=i;p.Y+=i*lip;}
                    else if(p.Distance<p.Width+5)p.Y+=lip*(Math.Cos(Math.PI*(p.Distance-p.Width)/5)+1)/2;
                    p.Y+=wl.GetXZ(x,z);p.Slope=p.Bank;p.HasSlope=true;
                    double side=p.Side;if(p.Slope<0)side*=-1;
                    double factor=(widthLerp*side+1)/2;factor=factor*factor*2-1;
                    p.Y+=Math.Abs(p.Slope)*factor*(single?.5:1);
                }
                if(p.Bridge&&p.NextBridge)
                {
                    if(p.NextNextBridge)lt=p.PreviousBridge?1:Math.Max(p.T,lt);else lt=Math.Max(1-p.T,lt);
                    if(p.Distance<width+verge+4&&h>p.Y){double m=1-Math.Max(0,(width+verge+4-p.Distance)/4),c=lt*.33;lt=(m-c)*(1/(1-c));}
                }
                return h*lt+p.Y*(1-lt);
            }
            return h;
        }
    }
}
