using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Terrain;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>Value retention for Ra's cached i_ anchors after P5 trims its live head.
    /// No spatial search or generation occurs here. Storage is bounded and owned by the shared field registry.</summary>
    internal sealed class FieldRoadHistory
    {
        const int MaxNodes=8192;
        sealed class Frame
        {
            internal readonly MidlineCoarseSample Coarse;internal readonly MidlineFineSample[] Fine;
            internal Frame(CoarseNode n)
            {Coarse=new MidlineCoarseSample(n);var fine=new List<MidlineFineSample>();for(var f=n.NextFine as FineNode;f!=null;f=f.NextFine as FineNode)fine.Add(new MidlineFineSample(f));Fine=fine.ToArray();}
        }
        readonly SortedDictionary<int,Frame> frames=new();
        internal int Count=>frames.Count;
        internal void Capture(TerrainQueryWindow window,int minimum)
        {
            for(var n=window.FindCoarse(window.FirstIndex);n!=null;n=n.Next)frames[n.Index]=new Frame(n);
            var remove=new List<int>();foreach(int i in frames.Keys)if(i<Math.Min(minimum,window.FirstIndex)-4||i>window.LastIndex)remove.Add(i);foreach(int i in remove)frames.Remove(i);
            if(frames.Count>MaxNodes)throw new InvalidOperationException("Field cached-road support exceeds the finite 8192-node retention contract");
        }
        internal Dictionary<int,CoarseNode> Prefix(TerrainQueryWindow current,int oldest)
        {
            int first=oldest-4,last=current.FirstIndex-1;var nodes=new Dictionary<int,CoarseNode>();
            for(int i=first;i<=last;i++)
            {
                if(!frames.TryGetValue(i,out var f)){if(i<oldest)continue;throw new InvalidOperationException("Missing retained field projection support "+i);}
                var a=f.Coarse;nodes.Add(i,new CoarseNode{Index=i,Generation=a.Handle.Generation,Position=a.Position,Normal=a.Side,HeadingDelta=a.HeadingDelta,
                    Grade=a.Grade,GradeForwardAverage=a.GradeForwardAverage,HeightCorrection=a.HeightCorrection,Blend=a.Blend,Bank=a.Bank,Bridge=a.Bridge,
                    HasFineSegments=a.HasFineSegments,FineSegments=a.FineSegments,ShadowLeft=a.ShadowLeft,ShadowRight=a.ShadowRight,HasShadowLeft=a.HasShadowLeft,HasShadowRight=a.HasShadowRight});
            }
            foreach(var pair in nodes)
            {
                var n=pair.Value;var frame=frames[pair.Key];n.Next=nodes.TryGetValue(pair.Key+1,out var next)?next:current.FindCoarse(pair.Key+1);
                // Se detached each retired head's coarse prev. Preserve its forward graph without
                // restoring backwards links that the original no longer retains.
                n.Previous=null;
                if(!n.HasFineSegments)continue;MidlinePathNode previous=n;
                foreach(var a in frame.Fine)
                {
                    if(n.Next==null)throw new InvalidOperationException("Retained field fine interval is incomplete");
                    var fine=new FineNode{PreviousCoarse=n,NextCoarse=n.Next,Ordinal=a.Ordinal,Position=a.Position,Normal=a.Side,
                        ShadowLeft=a.ShadowLeft,ShadowRight=a.ShadowRight,HasShadowLeft=a.HasShadowLeft,HasShadowRight=a.HasShadowRight};
                    previous.NextFine=fine;fine.PreviousFine=previous;previous=fine;
                }
                previous.NextFine=n.Next;
            }
            return nodes;
        }
        internal void Clear()=>frames.Clear();
    }
}
