using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Walls
{
    public sealed class WallGeometryData
    {
        public readonly float[] Positions,Normals,UV,Shadow;public readonly int[] Indices;public readonly Double3 Origin;
        internal WallGeometryData(List<float> p,List<float> uv,List<float> shadow,List<int> index,Double3 origin,List<float> normals=null)
        {Positions=p.ToArray();UV=uv.ToArray();Shadow=shadow.ToArray();Indices=index.ToArray();Origin=origin;Normals=normals?.ToArray();}
    }
    // Storage follows the original typed attributes: each write rounds to Float32, including later endcap edits.
    internal sealed class WallGeometryBuffer
    {
        internal readonly List<float> Positions=new(),UV=new(),Shadow=new(),Normals=new();internal readonly List<int> Indices=new();
        internal void Vertex(double x,double y,double z,double u,double v,double shadow)
        {Positions.Add((float)x);Positions.Add((float)y);Positions.Add((float)z);UV.Add((float)u);UV.Add((float)v);Shadow.Add((float)shadow);}
        internal void Connect(int previous,int width)
        {
            for(int i=0;i<width-1;i++)Indices.AddRange(new[]{previous+i,previous+i+1,previous+width+i,previous+i+1,previous+width+i+1,previous+width+i});
        }
        internal WallGeometryData Finish(Double3 origin,bool normals=false)=>new(Positions,UV,Shadow,Indices,origin,normals?Normals:null);
    }
    /// <summary>Finite immutable support supplied by the content coordinator, never the live producer graph.</summary>
    public sealed class WallGeometryInput
    {
        public readonly WallSegment Segment;public readonly CoarseNode Start;
        public readonly Func<double,double,CoarseNode,double> RoadsideHeight;
        public WallGeometryInput(WallSegment segment,CoarseNode start,Func<double,double,CoarseNode,double> roadsideHeight=null)
        {
            Segment=segment??throw new ArgumentNullException(nameof(segment));Start=start??throw new ArgumentNullException(nameof(start));RoadsideHeight=roadsideHeight;
            if(start.Index!=segment.Start||segment.End<=segment.Start||segment.End-segment.Start>128)throw new ArgumentOutOfRangeException();
            var n=start;for(int i=segment.Start;i<=segment.End;i++){if(n==null||n.Index!=i)throw new ArgumentException("Wall interval lacks support");if(i<segment.End)n=n.Next;}
        }
    }
}
