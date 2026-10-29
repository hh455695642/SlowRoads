using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Midline
{
    public readonly struct MidlineSampleKey : IEquatable<MidlineSampleKey>
    {
        public readonly NodeHandle Coarse;
        public readonly int Ordinal;
        public MidlineSampleKey(NodeHandle coarse,int ordinal){Coarse=coarse;Ordinal=ordinal;}
        public bool Equals(MidlineSampleKey b)=>Coarse.Generation==b.Coarse.Generation &&
            Coarse.Slot==b.Coarse.Slot && Coarse.Version==b.Coarse.Version && Ordinal==b.Ordinal;
        public override bool Equals(object obj)=>obj is MidlineSampleKey b && Equals(b);
        public override int GetHashCode()=>unchecked(((Coarse.Generation*397^Coarse.Slot)*397^Coarse.Version)*397^Ordinal);
    }
    public readonly struct MidlineCoarseSample
    {
        public readonly NodeHandle Handle;
        public readonly int Index,FineSegments;
        public readonly Double3 Position,Side;
        public readonly double Blend,Bank,HeightCorrection,Grade,GradeFiltered,GradeForwardAverage,
            ShadowLeft,ShadowRight,PaintLeft,PaintRight;
        public readonly bool Bridge,HasBridge,HasBlend,HasBank,HasFineSegments,HasShadowLeft,HasShadowRight,
            HasPaintLeft,HasPaintRight,HasMergeLeft,HasMergeRight,MergeLeft,MergeRight;
        internal MidlineCoarseSample(CoarseNode n)
        {
            Handle=n.Handle;Index=n.Index;FineSegments=n.FineSegments;Position=n.Position;Side=n.Normal;
            Blend=n.Blend;Bank=n.Bank;HeightCorrection=n.HeightCorrection;Grade=n.Grade;
            GradeFiltered=n.GradeFiltered;GradeForwardAverage=n.GradeForwardAverage;Bridge=n.Bridge;HasBridge=n.HasBridge;
            HasBlend=n.HasBlend;HasBank=n.HasBank;HasFineSegments=n.HasFineSegments;
            ShadowLeft=n.ShadowLeft;ShadowRight=n.ShadowRight;HasShadowLeft=n.HasShadowLeft;HasShadowRight=n.HasShadowRight;
            PaintLeft=n.PaintSolidLeftWeight;PaintRight=n.PaintSolidRightWeight;
            HasPaintLeft=n.HasPaintSolidLeftWeight;HasPaintRight=n.HasPaintSolidRightWeight;
            HasMergeLeft=n.HasPaintMergeLeft;HasMergeRight=n.HasPaintMergeRight;
            MergeLeft=n.PaintMergeLeft;MergeRight=n.PaintMergeRight;
        }
    }
    public readonly struct MidlineFineSample
    {
        public readonly MidlineSampleKey Key;
        public readonly int CoarseIndex,NextCoarseIndex,Ordinal;
        public readonly bool IsCoarse,HasShadowLeft,HasShadowRight;
        public readonly Double3 Position,Side;
        public readonly double ShadowLeft,ShadowRight;
        internal MidlineFineSample(MidlinePathNode n)
        {
            var c=n as CoarseNode;var f=n as FineNode;var owner=c??f.PreviousCoarse;
            CoarseIndex=owner.Index;NextCoarseIndex=c!=null?c.Next?.Index??c.Index:f.NextCoarse.Index;
            Ordinal=f?.Ordinal??0;IsCoarse=c!=null;Key=new MidlineSampleKey(owner.Handle,Ordinal);
            Position=n.Position;Side=n.Normal;ShadowLeft=n.ShadowLeft;ShadowRight=n.ShadowRight;
            HasShadowLeft=n.HasShadowLeft;HasShadowRight=n.HasShadowRight;
        }
    }
    /// <summary>Opt-in immutable P4 input copy; normal Advance allocates no snapshots.
    /// Ready bounds are exclusive coarse indices. B7 right merge can rewrite very old
    /// attributes (Settings L47258–L47260): consumers must check revision/dirty range.</summary>
    public sealed class MidlineReadView
    {
        readonly MidlineCoarseSample[] coarse;
        readonly MidlineFineSample[] fine;
        public int Generation {get;}
        public long Revision {get;}
        public int HeadIndex {get;}
        public int GeometryReadyExclusive {get;}
        public int NormalReadyExclusive {get;}
        public int AttributeReadyExclusive {get;}
        public int GeometryDirtyFrom {get;}
        public int AttributeDirtyFrom {get;}
        public int EarliestReferencedIndex {get;}
        public int CoarseCount=>coarse.Length;
        public int FineCount=>fine.Length;
        public MidlineCoarseSample GetCoarse(int index)=>coarse[index];
        public MidlineFineSample GetFine(int index)=>fine[index];
        internal MidlineReadView(MidlineState state,MidlineMarkingsState markings,BridgeCandidate bridge,
            long revision,int geometryDirty,int attributeDirty,int attributeGuard)
            :this(state,markings,bridge,revision,geometryDirty,attributeDirty,attributeGuard,null,int.MaxValue) { }
        internal MidlineReadView(MidlineState state,MidlineMarkingsState markings,BridgeCandidate bridge,
            long revision,int geometryDirty,int attributeDirty,int attributeGuard,CoarseNode first,int last)
        {
            Generation=state.Generation;Revision=revision;HeadIndex=state.Head.Index;
            GeometryReadyExclusive=NormalReadyExclusive=state.FineTail.Index;
            AttributeReadyExclusive=Math.Max(HeadIndex,Math.Min(NormalReadyExclusive,
                (markings?.Node.Index??HeadIndex)-attributeGuard));
            GeometryDirtyFrom=geometryDirty;AttributeDirtyFrom=attributeDirty;
            var c=new List<MidlineCoarseSample>();
            for(var n=first??state.Head;n!=null&&n.Index<=last;n=n.Next)c.Add(new MidlineCoarseSample(n));
            coarse=c.ToArray();
            var f=new List<MidlineFineSample>();
            for(MidlinePathNode n=first??state.Head;n!=null;n=n.NextFine)
            {var sample=new MidlineFineSample(n);if(sample.CoarseIndex>last)break;f.Add(sample);}
            fine=f.ToArray();
            int earliest=HeadIndex;
            void Keep(CoarseNode n){if(n!=null)earliest=Math.Min(earliest,n.Index);}
            Keep(state.FineTail);Keep(state.SmoothWindowHead);Keep(state.ObserverNode);Keep(bridge.SpawnNode);
            Keep(markings?.Node);Keep(markings?.LeftStart);Keep(markings?.LeftEnd);
            Keep(markings?.RightStart);Keep(markings?.RightEnd);
            foreach(var t in state.Trackers)Keep(t.Node);
            EarliestReferencedIndex=earliest;
        }
    }
}
