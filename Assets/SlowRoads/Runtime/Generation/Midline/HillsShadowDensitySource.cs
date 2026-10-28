using System;
using SlowRoads.Generation.Heightfield;

namespace SlowRoads.Generation.Midline
{
    /// <summary>Wa's road shadow input only, Settings L43767–L43796. No vegetation objects.</summary>
    public sealed class HillsShadowDensitySource : IDisposable
    {
        readonly HillsHeightfield map;
        public double DensityFactor { get; }
        public HillsShadowDensitySource(MidlineConfig.HillsStyle style,string seed,int detail=2,int? treeDensity=null)
        {
            // null denotes original Auto; explicit values use the original numeric option.
            DensityFactor=(treeDensity.HasValue?(treeDensity.Value-1)/3.0:0.5+Math.Min(0.5,detail/5.0))*0.8;
            map=new HillsHeightfield(style==MidlineConfig.HillsStyle.Straight ?
                new HillsHeightfieldConfig(seed,scale:3,offset:1.7,layerResolutions:new[]{21,9,31},
                    squared:true,depthHeightFactor:1.2,temper:false,tileScaleRandom:0.3) :
                new HillsHeightfieldConfig(seed,scale:0.75,offset:2.75,layerResolutions:new[]{3,9,11,29},
                    squared:false,depthHeightFactor:1.8,temper:false,tileScaleRandom:0.25));
        }
        public void SetHeadLayers(int negativeX,int nonNegativeX)=>map.SetLayers(negativeX,nonNegativeX);
        public double GetMapXZ(double x,double z)=>map.GetXZ(x,z);
        public double GetShadowDensityAt(double x,double z)
        {
            double density=(1-Math.Max(Math.Min(1,map.GetXZ(x,z)+(1-DensityFactor)),0))*DensityFactor;
            return density>0.05?Math.Min(1,density*1.5+0.3):0;
        }
        public void Dispose()=>map.Dispose();
    }
}
