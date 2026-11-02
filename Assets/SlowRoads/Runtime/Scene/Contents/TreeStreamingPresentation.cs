using System;
using SlowRoads.Scene.Road;
using SlowRoads.Generation.Contents.Trees;
using SlowRoads.Rendering.Trees;
using UnityEngine;
namespace SlowRoads.Scene.Contents
{
    /// <summary>Scene owns GPU resources; TerrainStreamingSession owns all logical
    /// work. Local origin updates are uniforms, never candidate regeneration.</summary>
    public sealed class TreeStreamingPresentation : MonoBehaviour
    {
        RoadStreamingPresentation road;HillsSceneSettings settings;
        public TreeGpuResources Gpu {get;private set;}
        public TreeStreamingSession Core=>road?.Terrain?.Trees;
        Mesh billboard;
        Material[] near=new Material[8],far=new Material[2];
        readonly Material[] cpu=new Material[32];
        int season=-1;bool disposed;
        public void Initialize(RoadStreamingPresentation road,HillsSceneSettings settings)
        {
            this.road=road;this.settings=settings;
            if(settings.treeMeshes==null||settings.treeMeshes.Length!=8||Array.Exists(settings.treeMeshes,m=>m==null))throw new InvalidOperationException("Tree meshes not configured");
            bool compute=SystemInfo.supportsComputeShaders&&SystemInfo.supportsInstancing&&settings.treeCulling!=null;
            if(!compute&&(!SystemInfo.supportsInstancing||settings.treeFallbackShader==null))throw new NotSupportedException("Tree instancing unavailable");
            billboard=new Mesh{name="Hills original imposter quad"};
            // Hills Os=11, Hs=4; original n4.makeProtoGeo uses the 11m quad.
            billboard.vertices=new[]{new Vector3(-5.5f,0,0),new Vector3(-5.5f,11,0),new Vector3(5.5f,11,0),new Vector3(5.5f,0,0)};
            billboard.uv=new[]{new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),new Vector2(0,0)};
            billboard.triangles=new[]{0,1,2,0,2,3};billboard.RecalculateBounds();
            SelectMaterials(compute);
            Gpu=new(Core.Store,road.Core.Config.TileSize,settings.treeMeshes,(Material[])near.Clone(),billboard,(Material[])far.Clone(),compute);
            Gpu.NearDistance=new[]{13,16,19,22,25,28}[settings.detail]*10;Gpu.FarDistance=road.Core.Config.TileSize*2.5f;
        }
        void SelectMaterials(bool compute)
        {
            season=(int)settings.season;
            for(int i=0;i<8;i++)
            {
                var material=settings.treeMaterials[season*8+i];if(material==null)throw new InvalidOperationException("Tree season material not configured");
                if(compute)near[i]=material;
                else {int index=season*8+i;if(cpu[index]==null){cpu[index]=new Material(material){shader=settings.treeFallbackShader,enableInstancing=true,hideFlags=HideFlags.DontSave};}near[i]=cpu[index];}
            }
            for(int i=0;i<2;i++)far[i]=settings.treeImposterMaterials[season*2+i];
            Gpu?.SetMaterials(near,far);
        }
        void LateUpdate()
        {
            if(disposed||Gpu==null||Core==null)return;
            if(season!=(int)settings.season)SelectMaterials(Gpu.UseCompute);
            Gpu.SetAmbient(RenderSettings.ambientProbe);
            Gpu.Visible=settings.treesVisible;Gpu.CastShadows=settings.treeShadows;Gpu.RenderOrigin=road.RenderOrigin;Gpu.Synchronize();
        }
        public void Release()
        {
            if(disposed)return;disposed=true;Gpu?.Dispose();Gpu=null;
            if(billboard!=null)Destroy(billboard);foreach(var m in cpu)if(m!=null)Destroy(m);
        }
        void OnDestroy()=>Release();
    }
}
