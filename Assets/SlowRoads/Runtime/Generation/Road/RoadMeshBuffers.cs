using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Road
{
    /// <summary>Segment-owned reusable storage. Only live ranges may be consumed after Complete.
    /// No per-interval allocation; unused capacity deliberately retains original typed-array semantics.</summary>
    public sealed class RoadMeshBuffers
    {
        public readonly RoadMeshConfig Config;
        public readonly float[] Position, Normal, Uv, Bridge, Shadow, PaintSolid, PaintSolidLeft, PaintSolidRight, PaintMergeLeft, PaintMergeRight;
        public readonly int[] Indices;
        public readonly RoadIntervalSpan[] Intervals;
        public int VertexCapacity=>Position.Length/3;
        public int VertexCount {get;internal set;}
        public int CoarseCount {get;internal set;}
        public int FineStart=>Config.Intervals*6;
        public int FineCount {get;internal set;}
        public Double3 Origin {get;internal set;}
        public Double3 BoundsMin {get;internal set;}
        public Double3 BoundsMax {get;internal set;}
        public RoadReferenceSphere ReferenceSphere {get;internal set;}
        public bool Visible {get;internal set;}
        public bool Complete {get;internal set;}
        public RoadLod Lod {get;private set;}
        public RoadSegmentRequest Request {get;internal set;}
        public RoadDrawRange DrawRange=>Lod==RoadLod.Fine ? new RoadDrawRange(FineStart,FineCount) : new RoadDrawRange(0,CoarseCount);
        public RoadMeshBuffers(RoadMeshConfig config)
        {
            Config=config??throw new System.ArgumentNullException(nameof(config)); int v=2*(config.Intervals*10+1);
            Position=new float[v*3];Normal=new float[v*3];Uv=new float[v*2];Indices=new int[config.Intervals*66];
            Bridge=new float[v];Shadow=new float[v];PaintSolid=new float[v];PaintSolidLeft=new float[v];PaintSolidRight=new float[v];
            PaintMergeLeft=new float[v];PaintMergeRight=new float[v];Intervals=new RoadIntervalSpan[config.Intervals];
        }
        public void SetLod(RoadLod lod){if(lod!=RoadLod.Coarse&&lod!=RoadLod.Fine)throw new System.ArgumentOutOfRangeException(nameof(lod));Lod=lod;}
    }
}
