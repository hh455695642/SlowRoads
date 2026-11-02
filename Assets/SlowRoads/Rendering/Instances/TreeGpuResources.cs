using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SlowRoads.Generation.Contents.Trees;
using SlowRoads.Generation.Midline;
using UnityEngine;
using UnityEngine.Rendering;
namespace SlowRoads.Rendering.Trees
{
    [StructLayout(LayoutKind.Sequential)] public struct TreeGpuInstance
    {
        public Vector4 positionScale,parameters;
    }
    public sealed class TreeGpuBatch : IDisposable
    {
        public GraphicsBuffer Input,Visible,Args,ShadowArgs;
        public readonly MaterialPropertyBlock Properties=new();
        public readonly MaterialPropertyBlock ShadowProperties=new();
        public Mesh Mesh;public Material Material;
        public int Count,Type,Variant,Lod;
        public double OriginX,OriginZ;
        public Bounds Bounds;
        public bool HasNear;
        public Matrix4x4[] CpuMatrices;
        public Matrix4x4[][] CpuBatches;
        public void Dispose(){Input?.Dispose();Visible?.Dispose();Args?.Dispose();ShadowArgs?.Dispose();Input=Visible=Args=ShadowArgs=null;CpuMatrices=null;}
    }
    /// <summary>320m spatial clusters, split by original family/variant. Updates
    /// replace only the affected cluster's buffers. Never repacks the whole world.</summary>
    public sealed class TreeGpuResources : IDisposable
    {
        public static readonly HashSet<TreeGpuResources> Active=new();
        readonly Dictionary<TreeClusterKey,List<TreeGpuBatch>> clusters=new();
        readonly List<TreeInstanceCandidate> scratch=new();
        readonly List<TreeGpuInstance>[] groups=new List<TreeGpuInstance>[8];
        readonly TreeCandidateStore store;
        readonly Mesh[] meshes;readonly Material[] materials;
        readonly Mesh billboard;readonly Material[] billboardMaterials;
        readonly int tileSize;
        readonly Vector4[] ambient=new Vector4[7];
        SphericalHarmonicsL2 ambientProbe;bool hasAmbient;
        public bool Visible=true,CastShadows;
        public Double3 RenderOrigin;
        public float NearDistance=190,FarDistance=840;
        public bool UseCompute {get;}
        public int UploadedBytes {get;private set;}
        public long TotalUploadedBytes {get;private set;}
        public int InstanceCount {get;private set;}
        public int BatchCount {get;private set;}
        public IEnumerable<List<TreeGpuBatch>> Clusters=>clusters.Values;
        public TreeGpuResources(TreeCandidateStore store,int size,Mesh[] meshes,Material[] materials,Mesh billboard,Material[] billboardMaterials,bool compute)
        {
            this.store=store;tileSize=size;this.meshes=meshes;this.materials=materials;this.billboard=billboard;this.billboardMaterials=billboardMaterials;
            UseCompute=compute;for(int i=0;i<8;i++)groups[i]=new();SetAmbient(RenderSettings.ambientProbe);Active.Add(this);
        }
        // CommandBuffer indirect draws have no Renderer to supply UnityPerDraw SH.
        // Pack the existing scene probe once on change, never per candidate/frame.
        public void SetAmbient(SphericalHarmonicsL2 probe)
        {
            if(hasAmbient&&ambientProbe.Equals(probe))return;hasAmbient=true;ambientProbe=probe;
            var sh=new SHCoefficients(probe);ambient[0]=sh.SHAr;ambient[1]=sh.SHAg;ambient[2]=sh.SHAb;ambient[3]=sh.SHBr;ambient[4]=sh.SHBg;ambient[5]=sh.SHBb;ambient[6]=sh.SHC;
            foreach(var list in clusters.Values)foreach(var b in list)b.Properties.SetVectorArray("_TreeAmbient",ambient);
        }
        public void Synchronize()
        {
            UploadedBytes=0;
            foreach(var key in store.Dirty)
            {
                if(clusters.Remove(key,out var old))foreach(var batch in old){if(batch.Lod==0)InstanceCount-=batch.Count;BatchCount--;}
                old??=new();var reused=new HashSet<TreeGpuBatch>();
                store.CopyCluster(key,scratch);if(scratch.Count==0){foreach(var b in old)b.Dispose();continue;}
                foreach(var list in groups)list.Clear();double ox=(double)key.X*tileSize+key.CellX*TreeClusterKey.CellsPerCluster*10,oz=(double)key.Z*tileSize+key.CellZ*TreeClusterKey.CellsPerCluster*10;
                foreach(var entry in scratch){var c=entry.Candidate;groups[c.Type*4+c.Variant].Add(new(){
                    positionScale=new((float)(c.X-ox),(float)c.Y,(float)(c.Z-oz),(float)c.Scale),
                    parameters=new((float)c.Rotation,(float)c.Density,c.Variant,entry.Near?1:0)});}
                var batches=new List<TreeGpuBatch>();
                for(int group=0;group<8;group++)if(groups[group].Count>0)for(int lod=0;lod<2;lod++)
                {
                    var data=groups[group];var mesh=lod==0?meshes[group]:billboard;if(mesh==null)continue;
                    var batch=old.Find(b=>b.Type==group/4&&b.Variant==group%4&&b.Lod==lod&&(b.Input==null||b.Input.count>=data.Count));
                    if(batch!=null)reused.Add(batch);else batch=new TreeGpuBatch();
                    batch.Count=data.Count;batch.Mesh=mesh;batch.Material=lod==0?materials[group]:billboardMaterials[group/4];
                    batch.Type=group/4;batch.Variant=group%4;batch.Lod=lod;batch.OriginX=ox;batch.OriginZ=oz;
                    var min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);var max=-min;batch.HasNear=false;
                    foreach(var instance in data){Vector3 p=instance.positionScale;float radius=instance.positionScale.w*18;min=Vector3.Min(min,p-Vector3.one*radius);max=Vector3.Max(max,p+Vector3.one*radius);batch.HasNear|=instance.parameters.w>.5f;}
                    var bounds=new Bounds();bounds.SetMinMax(min,max);batch.Bounds=bounds;
                    if(UseCompute)
                    {
                        batch.Input??=new(GraphicsBuffer.Target.Structured,data.Count,32);batch.Input.SetData(data);
                        batch.Visible??=new(GraphicsBuffer.Target.Append,data.Count,4);batch.Visible.SetCounterValue(0);
                        batch.Args??=new(GraphicsBuffer.Target.IndirectArguments,5,4);
                        batch.Args.SetData(new uint[]{mesh.GetIndexCount(0),0,mesh.GetIndexStart(0),mesh.GetBaseVertex(0),0});
                        batch.ShadowArgs??=new(GraphicsBuffer.Target.IndirectArguments,5,4);batch.ShadowArgs.SetData(new uint[]{mesh.GetIndexCount(0),(uint)data.Count,mesh.GetIndexStart(0),mesh.GetBaseVertex(0),0});
                        batch.Properties.SetBuffer("_Trees",batch.Input);batch.Properties.SetBuffer("_Visible",batch.Visible);batch.Properties.SetFloat("_UseVisible",1);
                        batch.ShadowProperties.SetBuffer("_Trees",batch.Input);batch.ShadowProperties.SetFloat("_UseVisible",0);
                    }
                    else
                    {
                        // Storage-only CPU fallback. Matrices are built on cluster changes,
                        // not each frame; fallback intentionally uses near geometry for all.
                        if(lod==1)continue;
                        batch.CpuMatrices=new Matrix4x4[data.Count];for(int i=0;i<data.Count;i++){var d=data[i];batch.CpuMatrices[i]=Matrix4x4.TRS(d.positionScale,Quaternion.Euler(0,d.parameters.x*Mathf.Rad2Deg,0),Vector3.one*d.positionScale.w);}
                        batch.CpuBatches=new Matrix4x4[(data.Count+1022)/1023][];
                        for(int i=0;i<batch.CpuBatches.Length;i++){int count=Math.Min(1023,data.Count-i*1023);batch.CpuBatches[i]=new Matrix4x4[count];Array.Copy(batch.CpuMatrices,i*1023,batch.CpuBatches[i],0,count);}
                    }
                    batch.Properties.SetFloat("_Family",group/4);batch.Properties.SetFloat("_Billboard",lod);batch.Properties.SetFloat("_Variant",group%4);
                    batch.Properties.SetVectorArray("_TreeAmbient",ambient);
                    batch.ShadowProperties.SetFloat("_Family",group/4);batch.ShadowProperties.SetFloat("_Billboard",lod);batch.ShadowProperties.SetFloat("_Variant",group%4);
                    batches.Add(batch);BatchCount++;if(UseCompute)UploadedBytes+=data.Count*32+40;if(lod==0)InstanceCount+=data.Count;
                }
                foreach(var b in old)if(!reused.Contains(b))b.Dispose();
                clusters.Add(key,batches);
            }
            store.Dirty.Clear();TotalUploadedBytes+=UploadedBytes;
        }
        public Vector3 Origin(TreeGpuBatch batch)=>new((float)(batch.OriginX-RenderOrigin.X),(float)-RenderOrigin.Y,(float)(batch.OriginZ-RenderOrigin.Z));
        public void Bind(TreeGpuBatch b)
        {b.Properties.SetVector("_ClusterOrigin",Origin(b));b.Properties.SetVector("_NoiseOffset",new Vector4((float)(RenderOrigin.X%256),0,(float)(RenderOrigin.Z%256),0));b.Properties.SetFloat("_LodNear",NearDistance);b.Properties.SetFloat("_LodFar",FarDistance);b.Properties.SetFloat("_Lod",b.Lod);}
        public void SetMaterials(Material[] near,Material[] far)
        {foreach(var list in clusters.Values)foreach(var b in list)b.Material=b.Lod==0?near[b.Type*4+b.Variant]:far[b.Type];
            System.Array.Copy(near,materials,8);System.Array.Copy(far,billboardMaterials,2);}
        public void Dispose(){Active.Remove(this);foreach(var list in clusters.Values)foreach(var b in list)b.Dispose();clusters.Clear();InstanceCount=BatchCount=0;}
    }
}
