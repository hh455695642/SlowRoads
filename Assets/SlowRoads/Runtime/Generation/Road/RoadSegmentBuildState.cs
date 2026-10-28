using System;

namespace SlowRoads.Generation.Road
{
    public sealed class RoadSegmentBuildState
    {
        // Nl constraints belong to the segment OBJECT, and survive initGeneration.
        public readonly double?[] PaintConstraints=new double?[5];
        public int? Generation {get;internal set;}
        public double Phase {get;internal set;}
        public int CompletedIntervals {get;internal set;}
        public int RebuildFromCoarseIndex {get;internal set;}=int.MaxValue;
        public RoadBuildResult Result {get;internal set;}=RoadBuildResult.NeedMoreInput;
        public RoadPaintState CapturePaint()=>new RoadPaintState(PaintConstraints);
        public void RestorePaint(RoadPaintState before){if(before==null)throw new ArgumentNullException(nameof(before));before.CopyTo(PaintConstraints);}
    }
    public sealed class RoadPaintState
    {
        readonly double?[] values;
        public RoadPaintState(double?[] values)
        {if(values==null||values.Length!=5)throw new ArgumentException("Five Nl constraints required");this.values=(double?[])values.Clone();}
        public double? Get(int channel)=>values[channel];
        internal void CopyTo(double?[] target)=>Array.Copy(values,target,5);
    }
    public struct RoadPaintNode
    {
        public double Left,Right;
        public bool HasLeft,HasRight,HasMergeLeft,HasMergeRight,MergeLeft,MergeRight;
        public bool HasSolid,HasSolidLeft,HasSolidRight,Solid,SolidLeft,SolidRight;
    }
    /// <summary>Nl's input mutations, isolated from immutable P3. Share one overlay for
    /// ordered segment builds; capture BEFORE Begin for replay. A new revision needs a new overlay.</summary>
    public sealed class RoadAttributeOverlay
    {
        readonly RoadPaintNode[] nodes;
        public readonly RoadPathSnapshot Path;
        public RoadAttributeOverlay(RoadPathSnapshot path)
        {
            Path=path??throw new ArgumentNullException(nameof(path));nodes=new RoadPaintNode[path.CoarseCount];
            for(int i=0;i<nodes.Length;i++)
            {
                var n=path.GetCoarseAt(i);
                nodes[i]=new RoadPaintNode{Left=n.HasLeft?n.Left:double.NaN,Right=n.HasRight?n.Right:double.NaN,
                    HasLeft=n.HasLeft,HasRight=n.HasRight,MergeLeft=n.MergeLeft,MergeRight=n.MergeRight,
                    HasMergeLeft=n.HasMergeLeft,HasMergeRight=n.HasMergeRight};
            }
        }
        public RoadPaintNode Get(int coarseIndex)=>nodes[Path.Offset(coarseIndex)];
        internal void Set(int coarseIndex,RoadPaintNode node)=>nodes[Path.Offset(coarseIndex)]=node;
        public RoadPaintNode[] Capture()=>(RoadPaintNode[])nodes.Clone();
        public void Restore(RoadPaintNode[] before)
        {if(before==null||before.Length!=nodes.Length)throw new ArgumentException("Overlay shape mismatch");Array.Copy(before,nodes,nodes.Length);}
    }
}
