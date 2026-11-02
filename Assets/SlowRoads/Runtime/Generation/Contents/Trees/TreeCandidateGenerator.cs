using System;
namespace SlowRoads.Generation.Contents.Trees
{
    /// <summary>Wa.getTreesAt, Settings L43802–43834. Session owns the random tables;
    /// visibility, near correction and GPU allocation never invoke this generator.</summary>
    public sealed class TreeCandidateGenerator
    {
        public readonly TreeRandomState Random;
        readonly HillsTreeVariantNoise noise=new();
        readonly Func<double,double,double> density;
        readonly Func<double,double,int> type;
        readonly int detail;
        public TreeCandidateGenerator(HillsTreeDensity density,int detail,TreeRandomState random=null)
            :this(density.GetDensityAt,density.GetTypeAt,detail,random){}
        public TreeCandidateGenerator(Func<double,double,double> density,Func<double,double,int> type,int detail,TreeRandomState random=null)
        {this.density=density??throw new ArgumentNullException(nameof(density));this.type=type??throw new ArgumentNullException(nameof(type));
            if((uint)detail>4)throw new ArgumentOutOfRangeException(nameof(detail));this.detail=detail;Random=random??new();}
        public TreeGroup Generate(double x,double z)
        {
            double d=density(x,z),nd=detail>2?(density(x+10,z+10)+density(x+10,z-10)+density(x-10,z-10)+density(x-10,z+10))/4:0;
            double dd=(d-.001)*3;int count=(int)Math.Floor(dd);
            if(Random.NextCount()<dd%1)count++; // JS signed remainder, including negative values.
            if(count<=0)return new(0,0,0,0,0,d,nd);
            int arrangement=Random.NextArrangement(),family=type(x,z);
            double v=noise.Get(x/(family==0?1000:2000),z/(family==0?1000:2000));
            if(family==1&&count<2&&detail>0)count=2;
            int band=v>.46?(v<.58?1:v<.7?2:3):0;
            return new(count,arrangement,family,band,Random.NextVariant(),d,nd);
        }
    }
}
