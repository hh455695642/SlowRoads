using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Generation.Terrain
{
    public enum TerrainRegionAction { Init, Stage, Display, PrepareFine, Fine, Coarse, Retire, Repool }
    public readonly struct TerrainRegionEvent
    {
        public readonly bool Chunk;
        public readonly TerrainRegionAction Action;
        public readonly int X,Z,Index,Id;
        public readonly TerrainRegionState Region;
        public TerrainRegionEvent(bool chunk,TerrainRegionAction action,int x,int z,int index,int id=0,TerrainRegionState region=null)
        {Chunk=chunk;Action=action;X=x;Z=z;Index=index;Id=id;Region=region;}
    }
    public sealed class TerrainRegionState
    {
        public readonly bool Chunk;
        public readonly int X,Z,Id,CreationIndex;
        public int LastSeen {get;internal set;}
        public int TargetLod {get;internal set;}
        public bool BuildRequested {get;internal set;}
        public bool Uploaded {get;internal set;}
        public bool Visible {get;internal set;}
        public bool Retired {get;internal set;}
        public bool Repooled {get;internal set;}
        public NearTerrainChunkLayout Layout {get;internal set;}
        public TerrainResidency Residency {get;internal set;}
        public TerrainRegionState(bool chunk,int x,int z,int id,int creation)
        {Chunk=chunk;X=x;Z=z;Id=id;CreationIndex=creation;LastSeen=creation;}
    }
    /// <summary>Ma's logical event order, Settings L42082–42554. No Mesh/height work here.
    /// Readiness delays completion only; the caller feeds stable coarse nodes in order.
    /// Extension: content hooks must not change cell registration or random consumption.</summary>
    public sealed class TerrainRegionSchedule
    {
        sealed class TilePlan
        {
            public int X,Z,Stage,Spawn;
            public readonly List<TerrainRegionState> Add=new(),Up=new(),Down=new(),Remove=new();
        }
        readonly TerrainGenerationConfig config;
        readonly Func<int,Double3?> position;
        readonly Func<int,int,int> roadLastSeen;
        public readonly Dictionary<(int x,int z),TerrainRegionState> Tiles=new();
        public readonly Dictionary<int,TerrainRegionState> Chunks=new();
        readonly List<TilePlan> stageTiles=new();
        readonly Queue<TilePlan> spawnTiles=new();
        readonly Queue<TerrainRegionState> stage=new(),display=new(),prepare=new(),fine=new(),coarse=new(),retire=new();
        public readonly double Q2,L2,H4,S5,W4,Lookahead;
        public readonly int B4,MT;
        readonly double collapse;
        NearTerrainCorridorCollector collector;
        int cx,cz,seen;
        public int LookaheadIndex {get;private set;}
        public int LastStagedIndex {get;private set;}=-100;
        public event Action<TerrainRegionEvent> Changed;
        public event Action<int,int,int> InnerCellRegistered;
        public Func<TerrainRegionState,bool> ParentsReady;
        public bool Initialized=>collector!=null;
        public NearTerrainChunkLayout OpenChunk=>collector?.Current;
        public TerrainRegionSchedule(TerrainGenerationConfig config,Func<int,Double3?> source,Func<int,int,int> roadLastSeen=null)
        {
            this.config=config;position=source;this.roadLastSeen=roadLastSeen??((x,z)=>int.MinValue);
            double h=config.TileSize;L2=Math.Max(40,Math.Sqrt(h)*4);H4=L2*1.25;
            B4=new[]{13,16,19,22,25,28}[config.ViewDistance];MT=B4*4;S5=Math.Min(-6,B4*-.4);W4=Math.Min(-12,L2*-.4);
            Q2=Math.Max(Math.Max(25,Math.Sqrt(h)),H4-h/10*1.5+5);Lookahead=H4+config.ChunkCapacity/5.0+10;collapse=5+h/100;
        }
        void Emit(TerrainRegionState r,TerrainRegionAction action,int index)
        {Changed?.Invoke(new(r.Chunk,action,r.X,r.Z,index,r.Id,r));}
        TerrainRegionState NewTile(int x,int z,int index)
        {
            var r=new TerrainRegionState(false,x,z,0,index);Tiles.Add((x,z),r);Emit(r,TerrainRegionAction.Init,index);return r;
        }
        void NewChunk(NearTerrainChunkLayout layout,int observer)
        {
            var r=new TerrainRegionState(true,layout.StartIndex,0,layout.Id,layout.StartIndex){Layout=layout};Chunks.Add(r.Id,r);Emit(r,TerrainRegionAction.Init,observer);
        }
        public void Initialize(int first)
        {
            if(Initialized)throw new InvalidOperationException("Already initialized");
            var p=position(first)??throw new InvalidOperationException("Initial node unavailable");LookaheadIndex=seen=first;
            cx=(int)Math.Floor(p.X/config.TileSize);cz=(int)Math.Floor(p.Z/config.TileSize);
            collector=new(config,(int)Math.Floor(p.X/10),(int)Math.Floor(p.Z/10),first);
            NewChunk(collector.Previous,first);
            for(int x=-2;x<=2;x++)for(int z=-2;z<=2;z++)
            {var t=NewTile(cx+x,cz+z,1);t.BuildRequested=true;t.Visible=true;t.TargetLod=Math.Abs(x)<2&&Math.Abs(z)<2?1:0;Emit(t,TerrainRegionAction.Stage,first);}
            NewChunk(collector.Current,first);collector.Started+=layout=>NewChunk(layout,observerForEvent);
        }
        int observerForEvent;
        TerrainRegionState Get(int x,int z)=>Tiles.TryGetValue((x,z),out var t)?t:null;
        void AddStrip(TilePlan a,int x,int z,int rx,int rz,int ux,int uz,int dx,int dz,bool inner)
        {
            var tile=Get(x,z)??NewTile(x,z,LookaheadIndex);tile.LastSeen=LookaheadIndex;a.Add.Add(tile);
            a.Remove.Add(Get(rx,rz));if(inner){a.Up.Add(Get(ux,uz));a.Down.Add(Get(dx,dz));}
        }
        public bool Step(int observer)
        {
            if(!Initialized)throw new InvalidOperationException("Initialize first");observerForEvent=observer;bool did=false;
            var next=position(LookaheadIndex+1);
            if(next.HasValue&&LookaheadIndex<observer+Lookahead)
            {
                did=true;LookaheadIndex++;var p=next.Value;
                int nx=(int)Math.Floor(p.X/config.TileSize),nz=(int)Math.Floor(p.Z/config.TileSize),dx=nx-cx,dz=nz-cz;
                if((dx!=0||dz!=0)&&LookaheadIndex-LastStagedIndex<collapse&&stageTiles.Count>0)
                {
                    var a=stageTiles[stageTiles.Count-1];
                    if(a.X==nx&&a.Z==nz){stageTiles.RemoveAt(stageTiles.Count-1);if(stageTiles.Count>0){a=stageTiles[stageTiles.Count-1];cx=a.X;cz=a.Z;dx=nx-cx;dz=nz-cz;LastStagedIndex=a.Spawn;}}
                }
                if(dx!=0||dz!=0)
                {
                    var a=new TilePlan{X=nx,Z=nz,Stage=(int)Math.Ceiling(LookaheadIndex-Q2),Spawn=LookaheadIndex-1};LastStagedIndex=LookaheadIndex;
                    for(int n=-2;n<=2;n++)
                    {
                        if(dx!=0)AddStrip(a,nx+dx*2,nz+n,cx-dx*2,cz+n,nx+dx,nz+n,cx-dx,cz+n,n> -2&&n<2);
                        if(dz!=0)AddStrip(a,nx+n,nz+dz*2,cx+n,cz-dz*2,nx+n,nz+dz,cx+n,cz-dz,n> -2&&n<2);
                    }
                    stageTiles.Add(a);
                }
                cx=nx;cz=nz;
                int ox=(int)Math.Floor(position(LookaheadIndex-1).Value.X/10),oz=(int)Math.Floor(position(LookaheadIndex-1).Value.Z/10);
                int cellX=(int)Math.Floor(p.X/10),cellZ=(int)Math.Floor(p.Z/10),ddx=cellX-ox,ddz=cellZ-oz;
                collector.AddNode(p.X,p.Z,LookaheadIndex+1);
                for(int n=-1;n<=1;n++){if(ddx!=0)InnerCellRegistered?.Invoke(cellX+ddx,cellZ+n,LookaheadIndex);if(ddz!=0)InnerCellRegistered?.Invoke(cellX+n,cellZ+ddz,LookaheadIndex);}
                while(collector.Completed.Count>0)
                {var layout=collector.Completed.Dequeue();var r=Chunks[layout.Id];r.LastSeen=layout.LastSeenIndex;stage.Enqueue(r);}
            }
            if(observer>seen)
            {
                did=true;
                if(stageTiles.Count>0&&stageTiles[0].Stage<=observer)
                {
                    var a=stageTiles[0];stageTiles.RemoveAt(0);
                    foreach(var t in a.Add){t.Retired=false;t.BuildRequested=true;Emit(t,TerrainRegionAction.Stage,observer);}
                    foreach(var t in a.Up)if(t!=null)Emit(t,TerrainRegionAction.PrepareFine,observer);spawnTiles.Enqueue(a);
                }
                if(spawnTiles.Count>0&&spawnTiles.Peek().Spawn<=observer)
                {
                    var a=spawnTiles.Dequeue();
                    foreach(var t in a.Add){t.Retired=false;t.Visible=true;Emit(t,TerrainRegionAction.Display,observer);}
                    foreach(var t in a.Up)if(t!=null){t.TargetLod=1;Emit(t,TerrainRegionAction.Fine,observer);}
                    foreach(var t in a.Down)if(t!=null){t.TargetLod=0;Emit(t,TerrainRegionAction.Coarse,observer);}
                    foreach(var t in a.Remove)if(t!=null&&!t.Repooled)
                    {
                        t.Visible=false;t.Retired=true;t.TargetLod=0;
                        bool keep=t.LastSeen>observer||roadLastSeen(t.X,t.Z)>observer;
                        t.Repooled=!keep;Emit(t,keep?TerrainRegionAction.Retire:TerrainRegionAction.Repool,observer);if(!keep)Tiles.Remove((t.X,t.Z));
                    }
                }
                seen++;
            }
            // Ma performs at most one chunk queue transition per update, in this priority.
            if(stage.Count>0&&stage.Peek().CreationIndex-H4<=observer&&(ParentsReady?.Invoke(stage.Peek())??true))
            {var r=stage.Dequeue();r.BuildRequested=true;Emit(r,TerrainRegionAction.Stage,observer);display.Enqueue(r);did=true;}
            else if(display.Count>0&&display.Peek().CreationIndex-L2<=observer&&display.Peek().Uploaded)
            {var r=display.Dequeue();r.Visible=true;Emit(r,TerrainRegionAction.Display,observer);prepare.Enqueue(r);did=true;}
            else if(prepare.Count>0&&prepare.Peek().CreationIndex-MT<=observer)
            {var r=prepare.Dequeue();Emit(r,TerrainRegionAction.PrepareFine,observer);fine.Enqueue(r);did=true;}
            else if(fine.Count>0&&fine.Peek().CreationIndex-B4<=observer)
            {var r=fine.Dequeue();r.TargetLod=1;Emit(r,TerrainRegionAction.Fine,observer);coarse.Enqueue(r);did=true;}
            else if(coarse.Count>0&&coarse.Peek().LastSeen-S5<=observer)
            {var r=coarse.Dequeue();r.TargetLod=0;Emit(r,TerrainRegionAction.Coarse,observer);retire.Enqueue(r);did=true;}
            else if(retire.Count>0&&retire.Peek().LastSeen-W4<=observer)
            {var r=retire.Dequeue();r.Visible=false;r.Retired=true;Emit(r,TerrainRegionAction.Retire,observer);Chunks.Remove(r.Id);did=true;}
            return did;
        }
    }
}
