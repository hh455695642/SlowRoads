using System;
using System.Collections.Generic;
namespace SlowRoads.Generation.Contents.Trees
{
    public readonly struct TreeClusterKey : IEquatable<TreeClusterKey>
    {
        public const int CellsPerCluster=32; // 320m display cluster; logical candidates remain 10m-addressed.
        public readonly int X,Z,Event,CellX,CellZ;
        public TreeClusterKey(TreeId id){X=id.TileX;Z=id.TileZ;Event=id.Event;CellX=(int)Math.Floor(id.CellX/(double)CellsPerCluster);CellZ=(int)Math.Floor(id.CellZ/(double)CellsPerCluster);}
        public bool Equals(TreeClusterKey k)=>X==k.X&&Z==k.Z&&Event==k.Event&&CellX==k.CellX&&CellZ==k.CellZ;
        public override bool Equals(object o)=>o is TreeClusterKey k&&Equals(k);
        public override int GetHashCode()=>HashCode.Combine(X,Z,Event,CellX,CellZ);
    }
    public readonly struct TreeInstanceCandidate
    {
        public readonly TreeCandidate Candidate;
        public readonly bool Near;
        public TreeInstanceCandidate(TreeCandidate c,bool near){Candidate=c;Near=near;}
    }
    /// <summary>One formal store for raw candidates, evaluated candidates and external
    /// contributions. Region changes touch only associated bounded clusters; GPU visibility
    /// is owned separately by rendering. All mutations occur on the session thread.</summary>
    public sealed class TreeCandidateStore
    {
        sealed class Entry {public TreeCandidate Raw;public double GroupScale;public TreeNearResult Near;public bool Evaluated,NearActive;}
        readonly Dictionary<TreeId,Entry> entries=new();
        readonly Dictionary<TreeClusterKey,HashSet<TreeId>> clusters=new();
        readonly Dictionary<(int,int),TreeTileCandidates> tiles=new();
        readonly Dictionary<(int,int),TreeTerrainCell> cells=new();
        readonly Dictionary<(int,int),HashSet<TreeId>> worldCells=new();
        public readonly HashSet<TreeClusterKey> Dirty=new();
        public int Generation {get;private set;}
        public long Revision {get;private set;}
        public int Count=>entries.Count;
        public event Action<TreeTileCandidates> Committed;
        public TreeCandidateStore(int generation){Generation=generation;}
        public IEnumerable<TreeClusterKey> ClusterKeys=>clusters.Keys;
        public bool TryGetTile(int x,int z,out TreeTileCandidates tile)=>tiles.TryGetValue((x,z),out tile);
        public bool TryGetRaw(TreeId id,out TreeCandidate candidate)
        {if(entries.TryGetValue(id,out var e)){candidate=e.Raw;return true;}candidate=default;return false;}
        public bool TryGetResolved(TreeId id,out TreeInstanceCandidate candidate)
        {
            candidate=default;if(!entries.TryGetValue(id,out var e)||!e.Raw.FarValid||(e.Evaluated&&!e.Near.Visible))return false;
            candidate=new(e.Evaluated?e.Raw.AtHeight(e.Near.Height):e.Raw,e.NearActive);return true;
        }
        void Put(TreeCandidate c,double groupScale)
        {
            var e=new Entry{Raw=c,GroupScale=groupScale};entries[c.Id]=e;
            var key=new TreeClusterKey(c.Id);if(!clusters.TryGetValue(key,out var list))clusters.Add(key,list=new());list.Add(c.Id);Dirty.Add(key);
            var cell=((int)Math.Floor(c.X/10),(int)Math.Floor(c.Z/10));if(!worldCells.TryGetValue(cell,out var members))worldCells.Add(cell,members=new());members.Add(c.Id);
            Evaluate(e);
        }
        void Evaluate(Entry e)
        {
            var c=e.Raw;e.Evaluated=cells.TryGetValue(((int)Math.Floor(c.X/10),(int)Math.Floor(c.Z/10)),out var cell);
            e.NearActive=e.Evaluated;
            if(e.Evaluated)e.Near=cell.Evaluate(c,e.GroupScale);
        }
        public void Commit(TreeTileCandidates tile)
        {
            if(tile.Generation!=Generation)throw new InvalidOperationException("Stale tree generation");
            if(tiles.ContainsKey((tile.TileX,tile.TileZ)))throw new InvalidOperationException("Tile already committed");
            tiles.Add((tile.TileX,tile.TileZ),tile);
            foreach(var c in tile.Candidates)
            {
                // Recover group scale from original pose, including the count==3 bonus.
                // Store it explicitly in later external records rather than derive from GPU matrices.
                Put(c,c.GroupScale);
            }
            tile.Revision=++Revision;Committed?.Invoke(tile);
        }
        // Candidate already stores absolute scale; group rejection needs only the
        // original family/count scale. Read it from the generation record.
        public void EvaluateCell(TreeTerrainCell cell)
        {
            cells[(cell.X,cell.Z)]=cell;
            if(worldCells.TryGetValue((cell.X,cell.Z),out var ids))foreach(var id in ids){Evaluate(entries[id]);Dirty.Add(new(id));}
            Revision++;
        }
        public void ReleaseCell(int x,int z)
        {
            if(!cells.Remove((x,z)))return;
            // Original prepCellTree keeps the corrected height/hidden decision when
            // a near chunk retires; only the near representation is relinquished.
            if(worldCells.TryGetValue((x,z),out var ids))foreach(var id in ids){entries[id].NearActive=false;Dirty.Add(new(id));}
            Revision++;
        }
        public void CopyCluster(TreeClusterKey key,List<TreeInstanceCandidate> output)
        {
            output.Clear();if(!clusters.TryGetValue(key,out var ids))return;
            foreach(var id in ids){var e=entries[id];if(!e.Raw.FarValid)continue;
                if(e.Evaluated&&!e.Near.Visible)continue;
                output.Add(new(e.Evaluated?e.Raw.AtHeight(e.Near.Height):e.Raw,e.NearActive));}
            output.Sort((a,b)=>{int c=a.Candidate.RawIndex.CompareTo(b.Candidate.RawIndex);if(c!=0)return c;var x=a.Candidate.Id;var y=b.Candidate.Id;
                c=x.Source.CompareTo(y.Source);if(c!=0)return c;c=x.CellX.CompareTo(y.CellX);if(c!=0)return c;c=x.CellZ.CompareTo(y.CellZ);return c!=0?c:x.Slot.CompareTo(y.Slot);});
        }
        void Remove(TreeId id)
        {if(!entries.Remove(id,out var e))return;var cell=((int)Math.Floor(e.Raw.X/10),(int)Math.Floor(e.Raw.Z/10));worldCells[cell].Remove(id);if(worldCells[cell].Count==0)worldCells.Remove(cell);
            var k=new TreeClusterKey(id);clusters[k].Remove(id);if(clusters[k].Count==0)clusters.Remove(k);Dirty.Add(k);}
        public bool ApplyExternal(int generation,long expectedRevision,long source,IReadOnlyList<TreeCandidate> upsert,IReadOnlyList<TreeId> remove)
        {
            if(generation!=Generation||expectedRevision!=Revision||source==0||upsert==null||remove==null)return false;
            var seen=new HashSet<TreeId>();
            foreach(var c in upsert)if(c.Id.Source!=source||!seen.Add(c.Id)||!tiles.TryGetValue((c.Id.TileX,c.Id.TileZ),out var tile)||tile.Event!=c.Id.Event||
                (int)Math.Floor(c.X/tile.Size)!=tile.TileX||(int)Math.Floor(c.Z/tile.Size)!=tile.TileZ||
                !double.IsFinite(c.X+c.Y+c.Z+c.Scale+c.Rotation)||c.Scale<=0||c.Type<0||c.Type>1||c.Variant<0||c.Variant>3)return false;
            foreach(var id in remove)if(id.Source!=source||!seen.Add(id))return false;
            foreach(var id in remove)Remove(id);
            foreach(var c in upsert){Remove(c.Id);Put(c,1);}
            Revision++;return true;
        }
        public void ReleaseTile(int x,int z)
        {
            if(!tiles.Remove((x,z)))return;
            var remove=new List<TreeId>();foreach(var id in entries.Keys)if(id.TileX==x&&id.TileZ==z)remove.Add(id);
            foreach(var id in remove)Remove(id);Revision++;
        }
        public void Reset(int generation){foreach(var k in clusters.Keys)Dirty.Add(k);entries.Clear();clusters.Clear();tiles.Clear();cells.Clear();worldCells.Clear();Generation=generation;Revision++;}
    }
}
