using System;
using System.Collections.Generic;
using SlowRoads.Generation.Terrain;

namespace SlowRoads.Generation.Contents.Grass
{
    public readonly struct GrassWindowConfig
    {
        public readonly double GrassCapacity,BushCapacity;public readonly int Horizon;
        public GrassWindowConfig(double grass,double bush,int horizon){if(grass<0||bush<0||!double.IsFinite(grass+bush)||horizon<1)throw new ArgumentOutOfRangeException();GrassCapacity=grass;BushCapacity=bush;Horizon=horizon;}
        public static GrassWindowConfig From(TerrainGenerationConfig c)
        {
            var g=GrassGenerationConfig.Detail(c.Detail);int horizon=new[]{13,16,19,22,25,28}[c.ViewDistance];
            // Hills _l -> G/C exports. Preserve double capacity: source curIndex uses
            // >= whereas typed-array/draw counts truncate. Do not round both to int.
            double count=c.CellResolution*c.CellResolution*g.Density*horizon*(1+g.Margin/12);
            return new(count,count*.18,horizon);
        }
    }
    public readonly struct RetainedGrassInstance
    {
        public readonly TerrainRegionKey SourceOwner;public readonly GrassInstance Instance;
        internal RetainedGrassInstance(TerrainRegionKey owner,GrassInstance instance){SourceOwner=owner;Instance=instance;}
    }
    /// <summary>Original Et/Lt/K1/K0 retention, independent of T4 Chunk residency and GPU
    /// visibility. S L45813-46092. A completed terrain Chunk may retire before its cards.
    /// This class consumes no random numbers and never samples terrain or a camera.</summary>
    public sealed class GrassInstanceWindow
    {
        public sealed class Slot
        {
            public readonly int Id;public readonly bool Bush;public readonly double Capacity;
            public double RetireIndex {get;internal set;}public double InitIndex {get;internal set;}
            public bool Full {get;internal set;}internal bool Initialized;
            internal readonly List<RetainedGrassInstance> records=new();
            public IReadOnlyList<RetainedGrassInstance> Instances=>records;
            public int DrawCount=>(int)Math.Floor(Math.Min(Capacity,records.Count));
            internal TerrainRegionKey key;internal bool HasKey;internal long revision;
            internal Slot(int id,bool bush,double capacity,double observer){Id=id;Bush=bush;Capacity=capacity;Initialized=!bush;RetireIndex=bush?0:observer;}
        }
        readonly List<Slot> grass=new(),bush=new();readonly HashSet<Slot> changed=new();readonly GrassWindowConfig config;int serial;
        public IReadOnlyList<Slot> Grass=>grass;public IReadOnlyList<Slot> Bushes=>bush;
        public event Action<GrassChunkData> Changed;public event Action<TerrainRegionKey> Released;
        public GrassInstanceWindow(GrassWindowConfig config,double observer=0){this.config=config;Initialize(observer);}
        void Initialize(double observer){serial=0;for(int i=0;i<3;i++)grass.Add(New(false,observer));for(int i=0;i<3;i++)bush.Add(New(true,observer));}
        Slot New(bool isBush,double observer)=>new(serial++,isBush,isBush?config.BushCapacity:config.GrassCapacity,observer);
        void Reset(Slot slot,double observer){slot.records.Clear();slot.Full=false;slot.RetireIndex=observer;changed.Add(slot);}
        public void Add(TerrainRegionKey owner,GrassInstance instance,double observer)
        {
            var c=instance.Candidate;var family=c.Bush?bush:grass;var slot=family[0];if(slot.Capacity<=0)throw new InvalidOperationException("Candidate emitted while original cover density is zero");
            if(!slot.Initialized){slot.Initialized=true;Reset(slot,observer);}
            if(c.Bush&&slot.records.Count==0)slot.InitIndex=c.RetireIndex;
            if(!slot.HasKey){slot.key=new(owner.Generation,"C5.Et/"+owner.Configuration,true,slot.Id,c.Bush?1:0,0);slot.HasKey=true;}
            slot.records.Add(new(owner,instance));slot.RetireIndex=Math.Max(slot.RetireIndex,c.RetireIndex);
            slot.Full=slot.records.Count>=slot.Capacity||(c.Bush&&slot.RetireIndex-slot.InitIndex>config.Horizon);changed.Add(slot);
            if(!slot.Full)return;
            // Et.checkCycleInstances: rotate first, then strict > test; equality permits reuse.
            family.RemoveAt(0);family.Add(slot);slot=family[0];
            if(slot.RetireIndex>observer){slot=New(c.Bush,observer);family.Insert(0,slot);}
            Reset(slot,observer);
        }
        public void Append(GrassChunkData data,double observer)
        {foreach(var instance in data.Instances)Add(data.Owner,instance,observer);Flush(data.Before,data.After);}
        /// <summary>Corresponds to manager.update after a logical chunk upgrade. Full slots
        /// already finalised, plus the current partial/empty slot; temporary upload timing is separate.</summary>
        public void Flush(Fields.FieldRandomState.Stream.Cursor before=default,Fields.FieldRandomState.Stream.Cursor after=default)
        {
            foreach(var slot in changed)
            {
                if(!slot.HasKey)continue;
                int count=slot.DrawCount;if(count==0){Released?.Invoke(slot.key);continue;}
                var copy=new List<GrassInstance>(count);for(int i=0;i<count;i++)copy.Add(slot.records[i].Instance);
                Changed?.Invoke(new GrassChunkData(slot.key,++slot.revision,copy,before,after));
            }
            changed.Clear();
        }
        public void Clear(double observer=0)
        {foreach(var slot in grass)if(slot.HasKey)Released?.Invoke(slot.key);foreach(var slot in bush)if(slot.HasKey)Released?.Invoke(slot.key);grass.Clear();bush.Clear();changed.Clear();Initialize(observer);}
    }
}
