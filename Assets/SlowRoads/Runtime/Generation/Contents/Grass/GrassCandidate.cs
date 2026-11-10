using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Grass
{
    public readonly struct GrassSurfaceSample
    {
        public readonly double Height,RoadProximity,TreeMask;public readonly Double3 Normal;
        public GrassSurfaceSample(double height,double roadProximity,double treeMask,Double3 normal)
        {Height=height;RoadProximity=roadProximity;TreeMask=treeMask;Normal=normal;}
    }
    /// <summary>Frozen original cell channels. Samples are X-major, excluding duplicated end points.</summary>
    public sealed class GrassCellInput
    {
        readonly GrassSurfaceSample[] samples;
        public readonly int Resolution,WallSide;public readonly double RetireIndex;public readonly bool Inner;
        public readonly double WorldX,WorldZ,LeftWall,RightWall;public readonly Double3 RoadPosition,RoadNormal;
        public GrassCellInput(int resolution,bool inner,double worldX,double worldZ,double retireIndex,GrassSurfaceSample[] samples,
            double leftWall=0,double rightWall=0,int wallSide=0,Double3 roadPosition=default,Double3 roadNormal=default)
        {
            if(resolution<=0||samples==null||samples.Length!=resolution*resolution)throw new ArgumentException("Invalid grass cell layout");
            Resolution=resolution;Inner=inner;WorldX=worldX;WorldZ=worldZ;RetireIndex=retireIndex;this.samples=(GrassSurfaceSample[])samples.Clone();
            LeftWall=leftWall;RightWall=rightWall;WallSide=wallSide;RoadPosition=roadPosition;RoadNormal=roadNormal;
        }
        public GrassSurfaceSample At(int x,int z)=>samples[x*Resolution+z];
        internal GrassCellInput WithWalls(double left,double right,int side)=>new(this,left,right,side);
        GrassCellInput(GrassCellInput source,double left,double right,int side)
        {samples=source.samples;Resolution=source.Resolution;RetireIndex=source.RetireIndex;Inner=source.Inner;WorldX=source.WorldX;WorldZ=source.WorldZ;
            LeftWall=left;RightWall=right;WallSide=side;RoadPosition=source.RoadPosition;RoadNormal=source.RoadNormal;}
    }
    public readonly struct GrassGenerationConfig
    {
        public readonly double Density,Margin;public readonly bool OffRoad,WideRoad;
        public GrassGenerationConfig(double density,double margin,bool offRoad=false,bool wideRoad=false)
        {if(density<0||!double.IsFinite(density+margin))throw new ArgumentOutOfRangeException();Density=density;Margin=margin;OffRoad=offRoad;WideRoad=wideRoad;}
        // Hills L01876–01912; manual density overrides belong at the configuration boundary.
        public static GrassGenerationConfig Detail(int detail,bool offRoad=false,bool wideRoad=false)=>detail switch
        {0=>new(0,0,offRoad,wideRoad),1=>new(.5,16,offRoad,wideRoad),2=>new(.65,20,offRoad,wideRoad),3=>new(.75,24,offRoad,wideRoad),4=>new(.85,28,offRoad,wideRoad),_=>throw new ArgumentOutOfRangeException(nameof(detail))};
    }
    public readonly struct GrassCandidate
    {
        public readonly bool Bush;public readonly int X,Z,Variant;public readonly double RetireIndex;
        public readonly Double3 Position,Normal;public readonly double Rotation,Scale,TreeMask,RoadProximity;
        internal GrassCandidate(bool bush,int x,int z,Double3 position,Double3 normal,double rotation,double scale,double treeMask,double proximity,double retireIndex,int variant)
        {Bush=bush;X=x;Z=z;Position=position;Normal=normal;Rotation=rotation;Scale=scale;TreeMask=treeMask;RoadProximity=proximity;RetireIndex=retireIndex;Variant=variant;}
    }
}
