using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Trees
{
    /// <summary>Wa's read-only density/type basis. No candidate RNG is consumed by queries.
    /// Owns separate height-layer state; never changes the road's shadow source.</summary>
    public sealed class HillsTreeDensity : IDisposable
    {
        readonly HillsShadowDensitySource density;
        readonly HillsTreeTypeMap types;
        bool disposed;
        public double DensityFactor=>density.DensityFactor;
        public HillsTreeDensity(MidlineConfig.HillsStyle style,string seed,int detail=2,int? treeDensity=null)
        {
            if(detail<0||detail>4)throw new ArgumentOutOfRangeException(nameof(detail));
            if(treeDensity.HasValue&&(treeDensity<1||treeDensity>4))throw new ArgumentOutOfRangeException(nameof(treeDensity));
            density=new HillsShadowDensitySource(style,seed,detail,treeDensity);types=new HillsTreeTypeMap(seed);
        }
        void Check(){if(disposed)throw new ObjectDisposedException(nameof(HillsTreeDensity));}
        public void SetHeadLayers(int negativeX,int nonNegativeX){Check();density.SetHeadLayers(negativeX,nonNegativeX);}
        public double GetMapXZ(double x,double z){Check();return density.GetMapXZ(x,z);}
        public double GetDensityAt(double x,double z){Check();return density.GetDensityAt(x,z);}
        public double GetShadowDensityAt(double x,double z){Check();return density.GetShadowDensityAt(x,z);}
        public double GetTypeNoiseAt(double x,double z){Check();return types.GetXZ(x,z);}
        public int GetTypeAt(double x,double z){Check();return types.GetTypeAt(x,z);}
        public void Dispose(){if(disposed)return;density.Dispose();types.Dispose();disposed=true;}
    }
}
