using System;
using System.Collections.Generic;
using SlowRoads.Generation.Contents.Trees;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Contents.Fields
{
    /// <summary>C2's ordered side effects, staged until a complete tile/reclip commits.
    /// Uses the existing C1 store and R4 attribute owner. Never edits ordinary tree candidates or P2 nodes.</summary>
    public sealed class FieldContentTransaction
    {
        readonly TreeCandidateStore trees;readonly ContentAttributeStore attributes;
        readonly ContentVersion version;readonly long treeRevision,source;
        readonly List<TreeCandidate> additions=new();readonly List<TreeId> removals=new();
        readonly Dictionary<ContentAttributeKey,double> patch=new();readonly Dictionary<(int,int),int> tileCounts=new();
        bool ended;
        public IReadOnlyList<TreeCandidate> Additions=>additions;
        public IReadOnlyDictionary<ContentAttributeKey,double> Patch=>patch;
        public bool Current=>!ended&&attributes.Version.Equals(version)&&trees.Generation==version.Generation&&trees.Revision==treeRevision;
        public FieldContentTransaction(TreeCandidateStore trees,ContentAttributeStore attributes,long source,IReadOnlyList<TreeId> replace=null)
        {
            if(source==0)throw new ArgumentOutOfRangeException(nameof(source));this.trees=trees;this.attributes=attributes;this.source=source;version=attributes.Version;treeRevision=trees.Revision;
            if(replace!=null)foreach(var id in replace){if(id.Source!=source)throw new ArgumentException("Field may withdraw only its own trees");removals.Add(id);}
        }
        public bool Add(in FieldTree tree,int tileSize)
        {
            if(ended)throw new InvalidOperationException("Ended field transaction");
            int tx=(int)Math.Floor(tree.Position.X/tileSize),tz=(int)Math.Floor(tree.Position.Z/tileSize);
            if(!trees.TryGetTile(tx,tz,out var tile))return false; // Original p0 routes only existing terrain tiles.
            var cell=((int)Math.Floor((tree.Position.X-tx*(double)tileSize)/10),(int)Math.Floor((tree.Position.Z-tz*(double)tileSize)/10));
            var key=(tx,tz);tileCounts.TryGetValue(key,out int count);tileCounts[key]=count+1;
            var id=new TreeId(tx,tz,tile.Event,cell.Item1,cell.Item2,tree.Ordinal,source);
            additions.Add(new TreeCandidate(id,tree.Position.X,tree.Position.Y,tree.Position.Z,tree.Rotation,tree.Scale,tree.Type,tree.Variant,tree.Density,trees.NextRawIndex(tx,tz)+count));return true;
        }
        public double ReadSidewall(int index,bool left)
        {var key=new ContentAttributeKey(index,0,left?ContentChannel.SidewallLeft:ContentChannel.SidewallRight);return patch.TryGetValue(key,out double value)?value:attributes.Read(key);}
        public void Sidewall(int index,bool left,double offset)
        {
            var key=new ContentAttributeKey(index,0,left?ContentChannel.SidewallLeft:ContentChannel.SidewallRight);
            // Ka first-truthy-write, including the sign. The generic attribute store does not implement this rule.
            if(ReadSidewall(index,left)==0)patch[key]=offset;
        }
        public void Colour(double x,double z,double value)
            =>patch[new ContentAttributeKey((int)Math.Floor(x/10+.5),(int)Math.Floor(z/10+.5),ContentChannel.FieldColour)]=value;
        public void Shadow(int index,bool left,double value)
        {
            var key=new ContentAttributeKey(index,0,left?ContentChannel.RoadShadowLeft:ContentChannel.RoadShadowRight);
            double old=patch.TryGetValue(key,out double prior)?prior:attributes.Read(key);patch[key]=Math.Max(old,value);
        }
        internal void SetRoadShadow(int index,int ordinal,bool left,double value)
        {
            var key=new ContentAttributeKey(index,ordinal,left?ContentChannel.RoadShadowLeft:ContentChannel.RoadShadowRight);
            // Ka.makeNodeShadow (Settings L44067–44092) can interpolate an undefined
            // endpoint ahead of B7's attribute frontier. Keep that NaN in the private
            // source graph, but withdraw this owner's overlay at the publication boundary.
            // Consumers then use the ready B7 value, not a fabricated zero or a stale
            // retained field value. Other channels and infinities still fail validation.
            if(double.IsNaN(value))patch.Remove(key);else patch[key]=value;
        }
        public bool Commit()
        {
            if(!Current)return false;
            foreach(var p in patch)if(!double.IsFinite(p.Value))throw new InvalidOperationException($"Non-finite field attribute {p.Key.Channel} at {p.Key.X}:{p.Key.Z}: {p.Value}");
            // Both stores are session-thread owned. Validate every attribute before the first mutation;
            // no callbacks or producer work intervene between these two commits.
            if(!trees.ApplyExternal(version.Generation,treeRevision,source,additions,removals))return false;
            if(!attributes.Apply(version,source,patch))throw new InvalidOperationException("Content owner changed inside atomic field publication");
            ended=true;return true;
        }
        public void Cancel(){ended=true;additions.Clear();patch.Clear();}
        internal void RetainRoadPatch(IReadOnlyDictionary<ContentAttributeKey,double> previous)
        {foreach(var p in previous)if(p.Key.Channel>=ContentChannel.SidewallLeft)patch[p.Key]=p.Value;}
    }
}
