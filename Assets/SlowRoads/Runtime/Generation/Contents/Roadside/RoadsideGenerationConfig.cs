using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Road;
namespace SlowRoads.Generation.Contents.Roadside
{
    /// <summary>Hills _l/cr H L5898-5998; S x7 L45274-45446. No quality-dependent guesses.</summary>
    public readonly struct RoadsideGenerationConfig
    {
        public readonly int SignLookahead;public readonly double SignRetire,EyeSpacing;public readonly bool EyesEnabled;
        public RoadsideGenerationConfig(RoadStreamingConfig c)
        {int h=new[]{13,16,19,22,25,28}[c.ViewDistance];SignLookahead=h*2;SignRetire=h/2.0;EyeSpacing=c.Mesh.HalfWidth*2;EyesEnabled=c.Mesh.LaneStyle==HillsLaneStyle.Standard;}
    }
}
