using System;
using System.Collections.Generic;
using SlowRoads.Generation.Contents.Grass;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Terrain;
using SlowRoads.Rendering.Trees;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Rendering.Grass
{
    /// <summary>Original Et/Lt window-owned 320m clusters using C1's buffers/culler/submission passes.
    /// Upload only on a stable content commit, never a CPU per-frame instance loop.
    /// Unsupported Compute/Instancing explicitly disables cover display, retaining CPU logic.</summary>
    public sealed class GrassGpuResources : HillsGpuSource,IDisposable
    {
        readonly Dictionary<TerrainRegionKey,List<TreeGpuBatch>> chunks=new();
        sealed class Upload
        {
            internal GrassChunkData Data;internal int Copied,Group,Offset;
            internal LinkedListNode<Upload> Node;
            internal readonly Dictionary<(int x,int z,int type),List<GrassCandidate>> Groups=new();
            internal KeyValuePair<(int x,int z,int type),List<GrassCandidate>>[] Ordered;
            internal readonly List<TreeGpuBatch> Batches=new();internal TreeGpuBatch Batch;
            internal TreeGpuInstance[] Values;internal Vector4[] Extras;internal Vector3 Min,Max;
        }
        readonly Dictionary<TerrainRegionKey,Upload> pending=new();readonly LinkedList<Upload> queue=new();Upload current;
        readonly Mesh grass,bush;readonly Material[] materials;
        readonly List<TreeGpuBatch> pool=new();long pooledBytes;
        const long PoolBudget=8*1024*1024;
        static readonly Unity.Profiling.ProfilerMarker UploadMarker=new("SlowRoads.C5.Grass.Upload");
        readonly Vector4[] ambient=new Vector4[7];SphericalHarmonicsL2 lastProbe;bool hasProbe;
        public override bool GroundCover=>true;
        public override bool UseCompute {get;}
        public override int BatchCount {get;protected set;}
        public override IEnumerable<List<TreeGpuBatch>> Clusters=>chunks.Values;
        public int InstanceCount {get;private set;}public long UploadedBytes {get;private set;}
        public bool HasPendingUploads=>current!=null||queue.Count>0;
        public long PooledBytes=>pooledBytes;
        public long CapacityBytes {get{long bytes=pooledBytes+StagingBytes;foreach(var list in chunks.Values)foreach(var b in list)bytes+=Bytes(b);return bytes;}}
        public Vector2 TextureOrigin;
        public string FallbackReason=>UseCompute?null:"Ground cover display disabled: Compute/Instancing unavailable; deterministic CPU candidates retained";
        public GrassGpuResources(Mesh grass,Mesh bush,Material grassMaterial,Material bushMaterial,bool compute=true)
        {
            this.grass=grass??throw new ArgumentNullException(nameof(grass));this.bush=bush??throw new ArgumentNullException(nameof(bush));
            materials=new[]{grassMaterial??throw new ArgumentNullException(nameof(grassMaterial)),bushMaterial??throw new ArgumentNullException(nameof(bushMaterial))};
            NearDistance=FarDistance=200;UseCompute=compute&&SystemInfo.supportsComputeShaders&&SystemInfo.supportsInstancing;
            SetAmbient(RenderSettings.ambientProbe);Active.Add(this);
        }
        public void SetAmbient(SphericalHarmonicsL2 probe)
        {
            if(hasProbe&&lastProbe.Equals(probe))return;hasProbe=true;lastProbe=probe;var sh=new SHCoefficients(probe);
            ambient[0]=sh.SHAr;ambient[1]=sh.SHAg;ambient[2]=sh.SHAb;ambient[3]=sh.SHBr;ambient[4]=sh.SHBg;ambient[5]=sh.SHBb;ambient[6]=sh.SHC;
            foreach(var list in chunks.Values)foreach(var b in list)b.Properties.SetVectorArray("_TreeAmbient",ambient);
        }
        /// <summary>Retain an immutable CPU commit. Visibility changes only after a complete
        /// staged upload; no GPU allocation or per-instance traversal runs in the G4 callback.</summary>
        public void Enqueue(GrassChunkData data)
        {
            if(!UseCompute){Remove(data.Owner);return;}
            Cancel(data.Owner);var job=new Upload{Data=data};pending.Add(data.Owner,job);job.Node=queue.AddLast(job);
        }
        /// <summary>Each unit groups or uploads one instance; spare groups allocate at most once
        /// per call. Finite work replaces the previous one-frame full-chunk allocation/upload.</summary>
        public int Advance(int budget)
        {
            if(budget<0)throw new ArgumentOutOfRangeException(nameof(budget));if(budget==0)return 0;
            using var uploadSample=UploadMarker.Auto();int used=0,allocations=0;
            try
            {
                while(used<budget)
                {
                    if(current==null)
                    {
                        while(queue.Count>0)
                        {var next=queue.First.Value;queue.RemoveFirst();next.Node=null;if(pending.TryGetValue(next.Data.Owner,out var value)&&ReferenceEquals(value,next)){current=next;break;}}
                        if(current==null)break;
                    }
                    var job=current;
                    if(job.Ordered==null)
                    {
                        while(used<budget&&job.Copied<job.Data.Instances.Count)
                        {
                            var c=job.Data.Instances[job.Copied++].Candidate;var key=((int)Math.Floor(c.Position.X/320),(int)Math.Floor(c.Position.Z/320),c.Bush?1:0);
                            if(!job.Groups.TryGetValue(key,out var list)){list=new();job.Groups.Add(key,list);}list.Add(c);used++;
                        }
                        if(job.Copied<job.Data.Instances.Count)break;
                        job.Ordered=new KeyValuePair<(int,int,int),List<GrassCandidate>>[job.Groups.Count];int groupIndex=0;foreach(var group in job.Groups)job.Ordered[groupIndex++]=group;job.Groups.Clear();
                    }
                    if(job.Group<job.Ordered.Length)
                    {
                        var group=job.Ordered[job.Group];var source=group.Value;
                        if(job.Batch==null)
                        {if(allocations++>0)break;Prepare(job,group.Key,source.Count);}
                        var b=job.Batch;int first=job.Offset,count=Math.Min(budget-used,source.Count-first);if(count==0)break;
                        for(int i=first;i<first+count;i++)
                        {
                            var c=source[i];job.Values[i]=new TreeGpuInstance{positionScale=new Vector4((float)(c.Position.X-b.OriginX),(float)c.Position.Y,(float)(c.Position.Z-b.OriginZ),(float)c.Scale),parameters=new Vector4((float)c.Rotation,(float)c.TreeMask,c.Variant,1)};
                            job.Extras[i]=new Vector4((float)c.Normal.X,(float)c.Normal.Y,(float)c.Normal.Z,(float)c.RoadProximity);
                            Vector3 p=job.Values[i].positionScale;float r=(float)c.Scale*b.InstanceRadius;job.Min=Vector3.Min(job.Min,p-Vector3.one*r);job.Max=Vector3.Max(job.Max,p+Vector3.one*r);
                        }
                        b.Input.SetData(job.Values,first,first,count);b.Extra.SetData(job.Extras,first,first,count);UploadedBytes+=count*48;job.Offset+=count;used+=count;
                        if(job.Offset<source.Count)break;b.Bounds.SetMinMax(job.Min,job.Max);job.Batch=null;job.Values=null;job.Extras=null;job.Offset=0;job.Group++;
                        continue;
                    }
                    RemoveCommitted(job.Data.Owner);foreach(var b in job.Batches){b.Material=materials[b.Type];b.Properties.SetVectorArray("_TreeAmbient",ambient);}
                    chunks.Add(job.Data.Owner,job.Batches);BatchCount+=job.Batches.Count;InstanceCount+=job.Data.Instances.Count;
                    pending.Remove(job.Data.Owner);current=null;
                    // Empty input still costs one finite completion unit.
                    if(job.Data.Instances.Count==0)used++;
                }
            }
            catch{if(current!=null)Cancel(current.Data.Owner);throw;}
            return used;
        }
        void Prepare(Upload job,(int x,int z,int type) key,int count)
        {
            var mesh=key.type==0?grass:bush;int reuse=pool.FindIndex(b=>b.Input.count>=count&&b.Input.count<=count*4);
            var b=reuse>=0?pool[reuse]:new TreeGpuBatch();if(reuse>=0){pool.RemoveAt(reuse);pooledBytes-=Bytes(b);}
            // Attach before allocation: cancellation/failure owns every partially prepared resource.
            job.Batches.Add(b);job.Batch=b;job.Values=new TreeGpuInstance[count];job.Extras=new Vector4[count];
            b.Mesh=mesh;b.Material=materials[key.type];b.Count=count;b.Type=key.type;b.Lod=0;b.HasNear=true;b.OriginX=key.x*320.0;b.OriginZ=key.z*320.0;
            b.InstanceRadius=(mesh.bounds.center.magnitude+mesh.bounds.extents.magnitude)*(key.type==0?2:1)+(key.type==0?.2f:.5f);
            job.Min=Vector3.one*float.MaxValue;job.Max=-job.Min;
            b.Input??=new(GraphicsBuffer.Target.Structured,count,32);b.Extra??=new(GraphicsBuffer.Target.Structured,count,16);
            b.Visible??=new(GraphicsBuffer.Target.Append,count,4);b.Visible.SetCounterValue(0);
            b.Args??=new(GraphicsBuffer.Target.IndirectArguments,5,4);b.Args.SetData(new uint[]{mesh.GetIndexCount(0),0,mesh.GetIndexStart(0),mesh.GetBaseVertex(0),0});UploadedBytes+=20;
            b.Properties.SetBuffer("_Trees",b.Input);b.Properties.SetBuffer("_GrassExtra",b.Extra);b.Properties.SetBuffer("_Visible",b.Visible);b.Properties.SetFloat("_UseVisible",1);b.Properties.SetVectorArray("_TreeAmbient",ambient);
        }
        void Cancel(TerrainRegionKey owner)
        {
            if(!pending.Remove(owner,out var job))return;if(ReferenceEquals(current,job))current=null;
            // Remove the queue node too: cancelled data must not stay referenced when
            // every pending owner was released and LateUpdate no longer calls Advance.
            if(job.Node!=null){queue.Remove(job.Node);job.Node=null;}
            foreach(var b in job.Batches)b.Dispose();
        }
        void Retire(TreeGpuBatch b)
        {
            long bytes=Bytes(b);if(pool.Count<32&&pooledBytes+bytes<=PoolBudget){pool.Add(b);pooledBytes+=bytes;}else b.Dispose();
        }
        void RemoveCommitted(TerrainRegionKey owner)
        {
            if(!chunks.Remove(owner,out var list))return;
            foreach(var b in list){InstanceCount-=b.Count;Retire(b);}BatchCount-=list.Count;
        }
        public void Remove(TerrainRegionKey owner)
        {Cancel(owner);RemoveCommitted(owner);}
        public long StagingBytes
        {
            get
            {
                long bytes=0;foreach(var p in pending.Values)foreach(var b in p.Batches)if(b.Input!=null)bytes+=Bytes(b);return bytes;
            }
        }
        static long Bytes(TreeGpuBatch b)=>(long)b.Input.count*52+20;
        public override Vector3 Origin(TreeGpuBatch b)=>new((float)(b.OriginX-RenderOrigin.X),(float)-RenderOrigin.Y,(float)(b.OriginZ-RenderOrigin.Z));
        public void SetMaterials(Material grassMaterial,Material bushMaterial)
        {materials[0]=grassMaterial;materials[1]=bushMaterial;foreach(var list in chunks.Values)foreach(var b in list)b.Material=materials[b.Type];}
        static double Mod(double value,double period)=>value-Math.Floor(value/period)*period;
        public override void Bind(TreeGpuBatch b)
        {
            b.Properties.SetVector("_ClusterOrigin",Origin(b));double period=b.Type==0?4000:512;
            // Wo subtracts originOffset; Yo deliberately samples unshifted wPos.
            double offsetX=b.Type==0?TextureOrigin.x:0,offsetZ=b.Type==0?TextureOrigin.y:0;
            b.Properties.SetVector("_GrassTextureOrigin",new Vector4((float)Mod(b.OriginX-offsetX,period),(float)Mod(b.OriginZ-offsetZ,period),0,0));
        }
        public void Dispose(){Active.Remove(this);foreach(var list in chunks.Values)foreach(var b in list)b.Dispose();foreach(var p in pending.Values)foreach(var b in p.Batches)b.Dispose();pending.Clear();queue.Clear();current=null;foreach(var b in pool)b.Dispose();pool.Clear();pooledBytes=0;chunks.Clear();BatchCount=InstanceCount=0;}
    }
}
