using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Spatial;

namespace SlowRoads.Generation.Terrain
{
    /// <summary>Bounded consumer snapshot, copied once per cell/build batch, never per vertex.
    /// No producer nodes escape. Dispose releases the private graph; the session validates publication.</summary>
    internal sealed class TerrainQueryWindow : IDisposable
    {
        internal readonly object Owner;
        internal readonly long Residency;
        internal CoarseNode[] Candidates;
        CoarseNode[] nodes;
        readonly Dictionary<MidlineSampleKey,(MidlineFineSample current,MidlineFineSample next)> attributes=new();
        public bool TryGetFineAttributes(MidlineSampleKey key,out MidlineFineSample current,out MidlineFineSample next)
        {
            current=next=default;
            if(IsDisposed||!attributes.TryGetValue(key,out var pair))return false;
            current=pair.current;next=pair.next;return true;
        }
        public int Generation {get;}
        public long Revision {get;}
        public int FirstIndex {get;}
        public int LastIndex {get;}
        public bool IsDisposed=>Candidates==null;
        internal TerrainQueryWindow(object owner,long residency,MidlineReadView view,int[] orderedCandidates)
        {
            Owner=owner;Residency=residency;Generation=view.Generation;Revision=view.Revision;
            FirstIndex=view.GetCoarse(0).Index;LastIndex=view.GetCoarse(view.CoarseCount-1).Index;
            nodes=new CoarseNode[view.CoarseCount];
            for(int i=0;i<nodes.Length;i++)
            {
                var a=view.GetCoarse(i);
                nodes[i]=new CoarseNode{Index=a.Index,Generation=a.Handle.Generation,Slot=a.Handle.Slot,SlotVersion=a.Handle.Version,
                    Position=a.Position,Normal=a.Side,HeadingDelta=a.HeadingDelta,Grade=a.Grade,HeightCorrection=a.HeightCorrection,
                    GradeForwardAverage=a.GradeForwardAverage,Blend=a.Blend,Bank=a.Bank,Bridge=a.Bridge};
                if(i>0){nodes[i].Previous=nodes[i-1];nodes[i-1].Next=nodes[i];}
            }
            MidlinePathNode previous=null;
            for(int i=0;i<view.FineCount;i++)
            {
                var a=view.GetFine(i);int at=a.CoarseIndex-FirstIndex;
                if(at<0||at>=nodes.Length)continue;
                MidlinePathNode n;
                if(a.IsCoarse)n=nodes[at];
                else
                {
                    if(at+1>=nodes.Length)break;
                    n=new FineNode{PreviousCoarse=nodes[at],NextCoarse=nodes[at+1],Ordinal=a.Ordinal,Position=a.Position,Normal=a.Side};
                }
                if(previous!=null){previous.NextFine=n;n.PreviousFine=previous;}previous=n;
                n.ShadowLeft=a.ShadowLeft;n.ShadowRight=a.ShadowRight;n.HasShadowLeft=a.HasShadowLeft;n.HasShadowRight=a.HasShadowRight;
                if(i+1<view.FineCount&&view.GetFine(i+1).CoarseIndex<=LastIndex)
                    attributes.Add(a.Key,(a,view.GetFine(i+1)));
            }
            SetCandidates(orderedCandidates);
        }
        internal void SetCandidates(int[] orderedCandidates)
        {
            if(nodes==null)throw new ObjectDisposedException(nameof(TerrainQueryWindow));
            if(orderedCandidates==null||orderedCandidates.Length==0||orderedCandidates.Length>128)throw new ArgumentOutOfRangeException(nameof(orderedCandidates));
            foreach(int i in orderedCandidates)if(i<=FirstIndex||i>=LastIndex-2)throw new ArgumentOutOfRangeException(nameof(orderedCandidates),"Cell requires support outside the frozen window");
            Candidates=new CoarseNode[orderedCandidates.Length];
            for(int i=0;i<Candidates.Length;i++)Candidates[i]=nodes[orderedCandidates[i]-FirstIndex];
        }
        public void Dispose(){Candidates=null;nodes=null;attributes.Clear();}
    }
}
