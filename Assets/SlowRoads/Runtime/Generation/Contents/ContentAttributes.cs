using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace SlowRoads.Generation.Contents
{
    public readonly struct ContentVersion : IEquatable<ContentVersion>
    {
        public readonly int Generation;
        public readonly long Road,Layer,Revision;
        public ContentVersion(int generation,long road,long layer,long revision){Generation=generation;Road=road;Layer=layer;Revision=revision;}
        public bool Equals(ContentVersion v)=>Generation==v.Generation&&Road==v.Road&&Layer==v.Layer&&Revision==v.Revision;
        public override bool Equals(object o)=>o is ContentVersion v&&Equals(v);
        public override int GetHashCode()=>HashCode.Combine(Generation,Road,Layer,Revision);
    }
    public enum ContentChannel { TreeShadow,FieldColour,SidewallLeft,SidewallRight,RoadShadowLeft,RoadShadowRight }
    public readonly struct ContentAttributeKey : IEquatable<ContentAttributeKey>
    {
        // Terrain channels use the original 10m world grid; road channels use
        // coarse node index in X; road shadows use Z=fine ordinal (zero is coarse).
        // Signed sidewall offsets use Z=0 and retain their original units.
        public readonly int X,Z;public readonly ContentChannel Channel;
        public ContentAttributeKey(int x,int z,ContentChannel channel){X=x;Z=z;Channel=channel;}
        public bool Equals(ContentAttributeKey k)=>X==k.X&&Z==k.Z&&Channel==k.Channel;
        public override bool Equals(object o)=>o is ContentAttributeKey k&&Equals(k);
        public override int GetHashCode()=>HashCode.Combine(X,Z,Channel);
    }
    /// <summary>Ordered owner contributions, not a mutable P2 node array. Field/wall
    /// consumers share a versioned snapshot. Generation algorithms do not own this storage.</summary>
    public sealed class ContentAttributeStore
    {
        sealed class Contribution {public long Source,Sequence;public Dictionary<ContentAttributeKey,double> Values;}
        readonly Dictionary<long,Contribution> owners=new();
        readonly Dictionary<ContentAttributeKey,double> values=new();
        long sequence;
        int roadShadowKeys;
        public ContentVersion Version {get;private set;}
        public bool HasContributions=>owners.Count>0;
        public bool HasRoadShadows=>roadShadowKeys>0;
        public event Action<IReadOnlyCollection<ContentAttributeKey>> Changed;
        public ContentAttributeStore(ContentVersion version){Version=version;}
        public void Rebase(int generation,long road,long layer)
        {
            if(generation!=Version.Generation||layer!=Version.Layer){Reset(new(generation,road,layer,Version.Revision+1));return;}
            Version=new(generation,road,layer,Version.Revision);
        }
        public double Read(ContentAttributeKey key)=>values.TryGetValue(key,out double v)?v:0;
        public bool TryRead(ContentAttributeKey key,out double value)=>values.TryGetValue(key,out value);
        public IReadOnlyDictionary<ContentAttributeKey,double> Snapshot()=>new ReadOnlyDictionary<ContentAttributeKey,double>(new Dictionary<ContentAttributeKey,double>(values));
        public bool Apply(ContentVersion expected,long source,IReadOnlyDictionary<ContentAttributeKey,double> patch)
        {
            if(!Version.Equals(expected)||source==0||patch==null)return false;
            foreach(var p in patch)if(!double.IsFinite(p.Value)||(uint)p.Key.Channel>5)return false;
            var dirty=new HashSet<ContentAttributeKey>(patch.Keys);
            if(owners.TryGetValue(source,out var old))dirty.UnionWith(old.Values.Keys);
            if(patch.Count==0)owners.Remove(source);else owners[source]=new(){Source=source,Sequence=++sequence,Values=new(patch)};
            var unchanged=new List<ContentAttributeKey>();
            foreach(var key in dirty)
            {
                bool hadValue=values.TryGetValue(key,out double before);
                bool roadShadow=key.Channel==ContentChannel.RoadShadowLeft||key.Channel==ContentChannel.RoadShadowRight;
                if(roadShadow&&values.ContainsKey(key))roadShadowKeys--;
                bool found=false;long last=long.MinValue;double result=0;
                foreach(var owner in owners.Values)if(owner.Values.TryGetValue(key,out double v))
                {if(key.Channel==ContentChannel.TreeShadow){result=Math.Max(result,v);found=true;}
                    else if(owner.Sequence>last){last=owner.Sequence;result=v;found=true;}}
                if(found){values[key]=result;if(roadShadow)roadShadowKeys++;}else values.Remove(key);
                if(hadValue==found&&(!found||before.Equals(result)))unchanged.Add(key);
            }
            foreach(var key in unchanged)dirty.Remove(key);
            Version=new(Version.Generation,Version.Road,Version.Layer,Version.Revision+1);Changed?.Invoke(dirty);return true;
        }
        public void Reset(ContentVersion version){var dirty=new List<ContentAttributeKey>(values.Keys);owners.Clear();values.Clear();sequence=0;roadShadowKeys=0;Version=version;Changed?.Invoke(dirty);}
    }
}
