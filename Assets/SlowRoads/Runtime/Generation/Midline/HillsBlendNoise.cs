using System;
using SlowRoads.Generation.Heightfield;
namespace SlowRoads.Generation.Midline
{
    public sealed class HillsBlendNoise
    {
        readonly RepeatedGradientNoise noise=new RepeatedGradientNoise();
        public double GetXZ(double x,double z)=>noise.GetXZ(x,z);
        public void Apply(CoarseNode node)
        {
            if(node.HasBlend && node.Blend!=0 && !double.IsNaN(node.Blend)) return;
            double clamped=Math.Min(Math.Max(0,GetXZ(node.Position.X,node.Position.Z)),1);
            node.Blend=1-clamped*clamped;
            if(node.Bridge) node.Blend*=0.1;
            if(node.Previous!=null && node.Previous.HasBlend && node.Previous.Blend!=0 && !double.IsNaN(node.Previous.Blend))
                node.Blend=node.Previous.Blend*0.5+node.Blend*0.5;
            node.HasBlend=true;
        }
    }
}
