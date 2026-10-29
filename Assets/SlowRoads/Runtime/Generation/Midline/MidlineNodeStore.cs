using System;
using System.Collections.Generic;

namespace SlowRoads.Generation.Midline
{
    /// <summary>Generation/version checked coarse slots; B04+ may reuse retired suffixes.</summary>
    public sealed class MidlineNodeStore
    {
        readonly List<CoarseNode> slots = new List<CoarseNode>();
        readonly Dictionary<int,CoarseNode> byIndex=new();
        internal void Register(CoarseNode node)=>byIndex[node.Index]=node;
        internal void Unregister(CoarseNode node){if(byIndex.TryGetValue(node.Index,out var current)&&ReferenceEquals(current,node))byIndex.Remove(node.Index);}
        public CoarseNode Find(int index,int generation)=>byIndex.TryGetValue(index,out var n)&&n.IsActive&&n.Generation==generation&&n.Index==index?n:null;
        readonly Stack<int> free = new Stack<int>();
        readonly List<CoarseNode> deferred=new List<CoarseNode>();
        readonly HashSet<CoarseNode> coarsePins=new HashSet<CoarseNode>(),finePins=new HashSet<CoarseNode>();
        CoarseNode pinnedCoarseHead;
        MidlinePathNode pinnedFineStart;
        internal Func<CoarseNode,bool> RetainReference;
        public int AllocatedCount=>slots.Count;

        public CoarseNode Acquire(int generation)
        {
            Reclaim();
            int slot = free.Count > 0 ? free.Pop() : slots.Count;
            CoarseNode node;
            if (slot == slots.Count)
            {
                node = new CoarseNode {Slot = slot, SlotVersion = 1};
                slots.Add(node);
            }
            else
            {
                node = slots[slot];
                node.SlotVersion++;
            }
            node.Generation = generation;
            node.IsActive = true;
            node.Previous = null;
            node.Next = null;
            node.PreviousFine = node.NextFine = null;
            node.HasBlend = node.HasBridge = node.HasBank = node.HasFineSegments =
                node.HasControlSnapshot = node.HasTargetDot = false;
            node.Blend = node.Bank = 0;
            node.HasShadowLeft=node.HasShadowRight=false;
            node.ShadowLeft=node.ShadowRight=0;
            node.HasPaintSolidLeftWeight=node.HasPaintSolidRightWeight=node.HasPaintMergeLeft=node.HasPaintMergeRight=false;
            node.PaintSolidLeftWeight=node.PaintSolidRightWeight=0;
            node.PaintMergeLeft=node.PaintMergeRight=false;
            node.Bridge = false;
            node.FineSegments = 0;
            return node;
        }

        public bool TryResolve(NodeHandle handle, out CoarseNode node)
        {
            node = null;
            if (handle.Slot < 0 || handle.Slot >= slots.Count) return false;
            var candidate = slots[handle.Slot];
            if (!candidate.IsActive || candidate.Generation != handle.Generation || candidate.SlotVersion != handle.Version)
                return false;
            node = candidate;
            return true;
        }

        public void Release(CoarseNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (node.Slot < 0 || node.Slot >= slots.Count || slots[node.Slot] != node || !node.IsActive)
                throw new ArgumentException("Node does not belong to this store.", nameof(node));
            if(byIndex.TryGetValue(node.Index,out var current)&&ReferenceEquals(node,current))byIndex.Remove(node.Index);
            // Release invalidates outstanding handles immediately, even before slot reuse.
            node.SlotVersion++;
            node.IsActive = false;
            if(IsRetained(node)){deferred.Add(node);return;}
            Recycle(node);
        }
        void Recycle(CoarseNode node)
        {
            node.Previous = node.Next = null;
            node.PreviousFine = node.NextFine = null;
            free.Push(node.Slot);
        }
        internal void Reclaim()
        {
            for(int i=deferred.Count-1;i>=0;i--)
                if(!IsRetained(deferred[i]))
                {var n=deferred[i];deferred.RemoveAt(i);Recycle(n);}
        }
        bool IsRetained(CoarseNode n)=>coarsePins.Contains(n)||finePins.Contains(n)||
            RetainReference!=null && RetainReference(n);
        // Cold restart path. JS keeps detached objects reachable through B7 and fine
        // links; C# pooling must not overwrite them before those source roots advance.
        internal void PinRestartReferences(CoarseNode markingsNode,MidlinePathNode fineStart)
        {
            coarsePins.Clear();finePins.Clear();pinnedCoarseHead=markingsNode;pinnedFineStart=fineStart;
            for(var n=markingsNode;n!=null && coarsePins.Add(n);n=n.Next){}
            for(var n=fineStart;n!=null;n=n.NextFine)
            {
                if(n is CoarseNode c)finePins.Add(c);
                else {var f=(FineNode)n;finePins.Add(f.PreviousCoarse);finePins.Add(f.NextCoarse);}
            }
        }
        internal void AdvancePins(CoarseNode markingsNode,MidlinePathNode fineStart)
        {
            if(pinnedCoarseHead!=null && pinnedCoarseHead!=markingsNode)
            {
                if(!coarsePins.Contains(markingsNode)){coarsePins.Clear();pinnedCoarseHead=null;}
                else while(pinnedCoarseHead!=markingsNode)
                {var old=pinnedCoarseHead;pinnedCoarseHead=old.Next;coarsePins.Remove(old);}
            }
            if(pinnedFineStart!=fineStart){finePins.Clear();pinnedFineStart=null;}
        }
        internal void Clear()
        {
            foreach(var n in slots)
            {n.SlotVersion++;n.IsActive=false;n.Previous=n.Next=null;n.PreviousFine=n.NextFine=null;}
            slots.Clear();byIndex.Clear();free.Clear();deferred.Clear();coarsePins.Clear();finePins.Clear();
            pinnedCoarseHead=null;pinnedFineStart=null;RetainReference=null;
        }
    }
}
