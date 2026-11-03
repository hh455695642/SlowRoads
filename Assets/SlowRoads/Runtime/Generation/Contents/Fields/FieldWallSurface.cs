using System;
using SlowRoads.Generation.Contents.Trees;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>s0.customCellLoad/Elev L46195–46217. Original wall band after Ll;
    /// Pl, road blending and base normals retain their original semantics.</summary>
    public sealed class FieldWallSurface
    {
        readonly HillsTreeVariantNoise noise=new(3,3);
        public static double Weight(double distance,double side,double left,double right)
        {
            double offset=side>0&&left!=0?-left:side<0&&right!=0?right:0;
            return offset!=0&&distance<offset+1&&distance>offset-1?Math.Min(1,(1-Math.Abs(offset-distance))*1.1):0;
        }
        public double Elevation(double x,double z,double weight)=>weight>0?weight*(.2+noise.Get(x,z)*.2):0;
    }
}
