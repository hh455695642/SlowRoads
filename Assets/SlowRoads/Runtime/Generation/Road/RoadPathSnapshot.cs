using System;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Road
{
    public readonly struct RoadCoarseSample
    {
        public readonly int Index;
        public readonly Double3 Position;
        public readonly bool Bridge, HasLeft, HasRight, HasMergeLeft, HasMergeRight, MergeLeft, MergeRight;
        public readonly double Left, Right, Blend, Bank, HeightCorrection, Grade, GradeFiltered, GradeForwardAverage;
        public readonly int FineSegments;
        public readonly bool HasFineSegments,HasBlend,HasBank,HasBridge;
        public RoadCoarseSample(int index,Double3 position,bool bridge,double left,double right,
            bool hasLeft,bool hasRight,bool mergeLeft,bool mergeRight,bool hasMergeLeft,bool hasMergeRight,
            double blend=0,double bank=0,double heightCorrection=0,double grade=0,double gradeFiltered=0,double gradeForwardAverage=0,
            int fineSegments=0,bool hasFineSegments=false,bool hasBlend=false,bool hasBank=false,bool hasBridge=false)
        {
            Index=index;Position=position;Bridge=bridge;Left=left;Right=right;HasLeft=hasLeft;HasRight=hasRight;
            MergeLeft=mergeLeft;MergeRight=mergeRight;HasMergeLeft=hasMergeLeft;HasMergeRight=hasMergeRight;
            Blend=blend;Bank=bank;HeightCorrection=heightCorrection;Grade=grade;GradeFiltered=gradeFiltered;GradeForwardAverage=gradeForwardAverage;
            FineSegments=fineSegments;HasFineSegments=hasFineSegments;HasBlend=hasBlend;HasBank=hasBank;HasBridge=hasBridge;
        }
    }
    public readonly struct RoadFineSample
    {
        public readonly int Owner, PreviousCoarse, NextCoarse;
        public readonly bool IsCoarse, HasShadowLeft, HasShadowRight;
        public readonly Double3 Position, Side;
        public readonly double ShadowLeft, ShadowRight;
        public RoadFineSample(int owner,int previous,int next,bool isCoarse,Double3 position,Double3 side,
            double shadowLeft,double shadowRight,bool hasShadowLeft=true,bool hasShadowRight=true)
        {Owner=owner;PreviousCoarse=previous;NextCoarse=next;IsCoarse=isCoarse;Position=position;Side=side;
            ShadowLeft=shadowLeft;ShadowRight=shadowRight;HasShadowLeft=hasShadowLeft;HasShadowRight=hasShadowRight;}
    }

    /// <summary>Immutable P4 copy. All ready/dirty bounds use exclusive/coarse indices,
    /// never fine-array offsets. No references to mutable Se/B7 nodes survive here.</summary>
    public sealed class RoadPathSnapshot
    {
        readonly RoadCoarseSample[] coarse;
        readonly RoadFineSample[] fine;
        readonly int[] coarseToFine;
        public int Generation {get;}
        public long Revision {get;}
        public int HeadIndex=>coarse.Length==0 ? 0 : coarse[0].Index;
        public int CoarseCount=>coarse.Length;
        public int FineCount=>fine.Length;
        public int GeometryReadyExclusive {get;}
        public int NormalReadyExclusive {get;}
        public int AttributeReadyExclusive {get;}
        public int GeometryDirtyFrom {get;}
        public int AttributeDirtyFrom {get;}
        public RoadPathSnapshot(int generation,long revision,RoadCoarseSample[] coarse,RoadFineSample[] fine,
            int geometryReadyExclusive,int normalReadyExclusive,int attributeReadyExclusive,
            int geometryDirtyFrom=int.MaxValue,int attributeDirtyFrom=int.MaxValue)
            :this(generation,revision,coarse,fine,geometryReadyExclusive,normalReadyExclusive,attributeReadyExclusive,geometryDirtyFrom,attributeDirtyFrom,false){}
        RoadPathSnapshot(int generation,long revision,RoadCoarseSample[] coarse,RoadFineSample[] fine,
            int geometryReadyExclusive,int normalReadyExclusive,int attributeReadyExclusive,
            int geometryDirtyFrom,int attributeDirtyFrom,bool takeOwnership)
        {
            Generation=generation;Revision=revision;
            if(coarse==null||fine==null)throw new ArgumentNullException();
            this.coarse=takeOwnership?coarse:(RoadCoarseSample[])coarse.Clone();
            this.fine=takeOwnership?fine:(RoadFineSample[])fine.Clone();
            GeometryReadyExclusive=geometryReadyExclusive;NormalReadyExclusive=normalReadyExclusive;
            AttributeReadyExclusive=attributeReadyExclusive;GeometryDirtyFrom=geometryDirtyFrom;AttributeDirtyFrom=attributeDirtyFrom;
            coarseToFine=new int[coarse.Length];
            for(int i=0;i<coarse.Length;i++)
            { if(coarse[i].Index!=(long)HeadIndex+i)throw new ArgumentException("Coarse indices must be contiguous"); coarseToFine[i]=-1; }
            int last=-1;
            for(int i=0;i<fine.Length;i++) if(fine[i].IsCoarse)
            {
                int c=Offset(fine[i].Owner);
                if(c<0 || c<=last)throw new ArgumentException("Fine chain must have ordered, unique coarse anchors");
                coarseToFine[c]=i;last=c;
            }
        }
        public RoadCoarseSample GetCoarseAt(int offset)=>coarse[offset];
        public RoadFineSample GetFine(int offset)=>fine[offset];
        public int Offset(int index){long o=(long)index-HeadIndex;return o>=0&&o<coarse.Length?(int)o:-1;}
        public int FineOffset(int coarseIndex){int c=Offset(coarseIndex);return c<0?-1:coarseToFine[c];}
        public bool IsBridge(int coarseIndex){int c=Offset(coarseIndex);return c>=0&&coarse[c].Bridge;}

        // Extension point: P5 owns snapshot publication and retirement; P4 never invokes Advance.
        public static RoadPathSnapshot FromMidline(MidlineReadView view)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
            var c=new RoadCoarseSample[view.CoarseCount];
            for(int i=0;i<c.Length;i++)
            {
                var n=view.GetCoarse(i);
                c[i]=new RoadCoarseSample(n.Index,n.Position,n.Bridge,n.PaintLeft,n.PaintRight,n.HasPaintLeft,n.HasPaintRight,
                    n.MergeLeft,n.MergeRight,n.HasMergeLeft,n.HasMergeRight,n.Blend,n.Bank,n.HeightCorrection,n.Grade,n.GradeFiltered,n.GradeForwardAverage,
                    n.FineSegments,n.HasFineSegments,n.HasBlend,n.HasBank,n.HasBridge);
            }
            var f=new RoadFineSample[view.FineCount];
            for(int i=0;i<f.Length;i++)
            {
                var n=view.GetFine(i);
                int previous=n.IsCoarse ? (n.CoarseIndex==view.HeadIndex?int.MinValue:n.CoarseIndex-1) : n.CoarseIndex;
                int next=n.NextCoarseIndex==n.CoarseIndex ? int.MinValue : n.NextCoarseIndex;
                f[i]=new RoadFineSample(n.CoarseIndex,previous,next,n.IsCoarse,n.Position,n.Side,
                    n.ShadowLeft,n.ShadowRight,n.HasShadowLeft,n.HasShadowRight);
            }
            return new RoadPathSnapshot(view.Generation,view.Revision,c,f,view.GeometryReadyExclusive,view.NormalReadyExclusive,
                view.AttributeReadyExclusive,view.GeometryDirtyFrom,view.AttributeDirtyFrom,true);
        }
    }
}
