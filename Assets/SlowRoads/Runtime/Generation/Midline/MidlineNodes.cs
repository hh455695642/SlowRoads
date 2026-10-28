namespace SlowRoads.Generation.Midline
{
    /// <summary>Coarse endpoints and interior samples share one fine-chain identity.</summary>
    public abstract class MidlinePathNode
    {
        public Double3 Position, Normal;
        public MidlinePathNode PreviousFine, NextFine;
        public bool HasShadowLeft, HasShadowRight;
        public double ShadowLeft, ShadowRight;
    }

    public sealed class FineNode : MidlinePathNode
    {
        public CoarseNode PreviousCoarse, NextCoarse;
        public int Ordinal;
    }

    /// <summary>Dev Se coarse node, L01115–L01145; later fields stay absent until written.</summary>
    public sealed class CoarseNode : MidlinePathNode
    {
        internal int Slot, SlotVersion, Generation;
        internal bool IsActive;
        public int Index;
        public double Heading, HeadingDelta, Turn, Grade, GradeFiltered, GradeForwardAverage, HeightCorrection;
        public CoarseNode Previous, Next;
        public bool HasBlend;
        public double Blend;
        public bool HasBridge;
        public bool Bridge;
        public bool HasBank;
        public double Bank;
        public bool HasFineSegments;
        public int FineSegments;
        public bool HasPaintSolidLeftWeight,HasPaintSolidRightWeight,HasPaintMergeLeft,HasPaintMergeRight;
        public double PaintSolidLeftWeight,PaintSolidRightWeight;
        public bool PaintMergeLeft,PaintMergeRight;
        // Source's optional `o` is a control snapshot, not a default zero vector.
        public bool HasControlSnapshot;
        public double ControlSnapshot;
        public bool HasTargetDot;
        public double TargetDot;

        public NodeHandle Handle => new NodeHandle(Generation, Slot, SlotVersion);
    }

    public readonly struct NodeHandle
    {
        public readonly int Generation, Slot, Version;
        public NodeHandle(int generation, int slot, int version)
        {
            Generation = generation; Slot = slot; Version = version;
        }
    }
}
