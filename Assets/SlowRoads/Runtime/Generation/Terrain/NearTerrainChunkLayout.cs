using System;
using System.Collections.Generic;
using System.Linq;

namespace SlowRoads.Generation.Terrain
{
    public sealed class NearTerrainCell
    {
        public readonly int X,Z;
        internal readonly List<int> candidates=new();
        public bool Inner {get;internal set;}
        public int CandidateCount=>candidates.Count;
        public int Candidate(int index)=>candidates[index];
        internal NearTerrainCell(int x,int z,int candidate){X=x;Z=z;candidates.Add(candidate);}
        internal int[] Candidates()=>candidates.ToArray();
    }
    /// <summary>U.addCell/registerInnerCell, Settings L41335–41434. Cell order is
    /// JS integer-key order after +10000, then insertion order for negative keys.</summary>
    public sealed class NearTerrainChunkLayout
    {
        readonly Dictionary<int,Dictionary<int,NearTerrainCell>> map=new();
        readonly HashSet<(int x,int z)> parents=new();
        readonly TerrainGenerationConfig config;
        public readonly int Id;
        public readonly long CollectionEpoch;
        public TerrainGenerationConfig Config=>config;
        public int Count {get;private set;}
        public bool Sealed {get;private set;}
        public IReadOnlyList<NearTerrainCell> Cells {get;private set;}
        public IReadOnlyCollection<(int x,int z)> ParentTiles=>parents;
        public int LastSeenIndex {get;private set;}=int.MinValue;
        public NearTerrainChunkLayout(TerrainGenerationConfig config,int id,long collectionEpoch=0){this.config=config??throw new ArgumentNullException(nameof(config));Id=id;CollectionEpoch=collectionEpoch;}
        public bool HasCell(int x,int z)=>map.TryGetValue(x,out var row)&&row.ContainsKey(z);
        internal NearTerrainCell Get(int x,int z)=>map.TryGetValue(x,out var row)&&row.TryGetValue(z,out var cell)?cell:null;
        void Mutable(){if(Sealed)throw new InvalidOperationException("Chunk collection is sealed");}
        public void AddCell(int x,int z,int candidate,NearTerrainChunkLayout previous=null)
        {
            Mutable();parents.Add(((int)Math.Floor(x*10.0/config.TileSize),(int)Math.Floor(z*10.0/config.TileSize)));
            LastSeenIndex=Math.Max(LastSeenIndex,candidate);
            if(!map.TryGetValue(x,out var row)){row=new();map.Add(x,row);}
            if(row.TryGetValue(z,out var current)){current.candidates.Add(candidate);return;}
            var old=previous?.Get(x,z);if(old!=null){previous.Mutable();old.candidates.Add(candidate);return;}
            if(Count>=config.ChunkCapacity)throw new InvalidOperationException("Chunk capacity exceeded");
            row.Add(z,new NearTerrainCell(x,z,candidate));Count++;
        }
        public void RegisterInner(int x,int z,NearTerrainChunkLayout previous=null)
        {
            Mutable();var cell=Get(x,z);if(cell!=null)cell.Inner=true;
            else if(previous?.Get(x,z) is NearTerrainCell old){previous.Mutable();old.Inner=true;}
        }
        static IEnumerable<int> Ordered(IEnumerable<int> keys)=>keys.Where(k=>k>=-10000).OrderBy(k=>k).Concat(keys.Where(k=>k< -10000));
        public void Seal()
        {
            Mutable();var cells=new List<NearTerrainCell>(Count);
            foreach(int x in Ordered(map.Keys))foreach(int z in Ordered(map[x].Keys))cells.Add(map[x][z]);
            if(cells.Count==0)throw new InvalidOperationException("Empty chunk");
            Cells=cells.AsReadOnly();Sealed=true;
        }
    }
    /// <summary>Finite Ma collection only, L42405–42436. No T4 spawn/overlap scheduler.
    /// A chunk is sealed one rotation later because current cells can append to prev.</summary>
    public sealed class NearTerrainCorridorCollector
    {
        readonly TerrainGenerationConfig config;
        int x,z,id;
        bool finished;
        NearTerrainChunkLayout previous,current;
        public readonly Queue<NearTerrainChunkLayout> Completed=new();
        public NearTerrainCorridorCollector(TerrainGenerationConfig config,int cellX,int cellZ,int candidate)
        {
            this.config=config;x=cellX;z=cellZ;current=new(config,id++);
            for(int i=-2;i<=2;i++)for(int j=-2;j<=2;j++)current.AddCell(x+i,z+j,candidate);
            for(int i=-1;i<=1;i++)for(int j=-1;j<=1;j++)current.RegisterInner(x+i,z+j);
            Rotate();
        }
        void Rotate(){if(previous!=null){previous.Seal();Completed.Enqueue(previous);}previous=current;current=new(config,id++);}
        public void AddNode(double worldX,double worldZ,int nextCandidate)
        {
            if(finished)throw new InvalidOperationException("Corridor collection has finished");
            if(!double.IsFinite(worldX)||!double.IsFinite(worldZ))throw new ArgumentOutOfRangeException();
            int nx=(int)Math.Floor(worldX/10),nz=(int)Math.Floor(worldZ/10),dx=nx-x,dz=nz-z;
            if(dx!=0&&dz!=0&&config.ChunkCapacity-current.Count<5)Rotate();
            if(dx!=0||dz!=0)for(int a=-2;a<=2;a++)
            {
                if(dx!=0){int sx=nx+dx*2,sz=nz+a;current.AddCell(sx,sz,nextCandidate,previous);
                    if(a> -2&&a<2)current.RegisterInner(sx-dx,sz,previous);if(current.Count>=config.ChunkCapacity)Rotate();}
                if(dz!=0){int sx=nx+a,sz=nz+dz*2;current.AddCell(sx,sz,nextCandidate,previous);
                    if(a> -2&&a<2)current.RegisterInner(sx,sz-dz,previous);if(current.Count>=config.ChunkCapacity)Rotate();}
            }
            x=nx;z=nz;
        }
        public void Finish(){if(finished)return;finished=true;if(previous!=null){previous.Seal();Completed.Enqueue(previous);previous=null;}if(current.Count>0){current.Seal();Completed.Enqueue(current);} }
    }
}
