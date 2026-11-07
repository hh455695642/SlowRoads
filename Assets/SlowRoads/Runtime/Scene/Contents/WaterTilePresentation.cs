using System;
using System.Collections.Generic;
using SlowRoads.Generation.Contents.Water;
using SlowRoads.Generation.Terrain;
using SlowRoads.Scene.Road;
using UnityEngine;
using UnityEngine.Rendering;
namespace SlowRoads.Scene.Contents
{
    /// <summary>Mesh ownership only; T4 owns all demand/jobs. Shared material, no extra render pass/RT.</summary>
    public sealed class WaterTilePresentation : MonoBehaviour
    {
        sealed class Slot {internal GameObject Object;internal Mesh Mesh;internal MeshRenderer Renderer;internal WaterTileData Data;}
        readonly Dictionary<TerrainRegionState,Slot> active=new();readonly Stack<Slot> pool=new();
        MaterialPropertyBlock block;RoadStreamingPresentation road;HillsSceneSettings settings;bool disposed;double time;
        public int NativeMeshCount=>active.Count+pool.Count;
        public int ActiveWaterTiles=>active.Count;
        public long UploadedBytes {get;private set;}public int UploadCount {get;private set;}
        public event Action<long> UploadMeasured;
        void Awake()=>block=new MaterialPropertyBlock();
        public void Initialize(RoadStreamingPresentation owner,HillsSceneSettings config)
        {road=owner;settings=config;road.Terrain.Core.WaterCommitted+=Commit;road.Terrain.Core.Released+=Remove;road.Terrain.Core.Reset+=Reset;road.OriginShifted+=Shift;}
        void Commit(TerrainRegionState region,WaterTileData data)
        {
            if(region.Residency==null||!region.Residency.IsCurrent||!region.Residency.Key.Equals(data.Key)||region.Residency.Revision!=data.TerrainRevision||region.Residency.LayerRevision!=data.LayerRevision)throw new InvalidOperationException("Stale water upload");
            if(!data.HasSea){Remove(region);return;}
            long start=UploadMeasured!=null?System.Diagnostics.Stopwatch.GetTimestamp():0;
            Slot s;if(pool.Count>0)s=pool.Pop();else
            {var go=new GameObject("Hills Water",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);var mesh=new Mesh{name="Hills Water Tile"};go.GetComponent<MeshFilter>().sharedMesh=mesh;s=new(){Object=go,Mesh=mesh,Renderer=go.GetComponent<MeshRenderer>()};s.Renderer.shadowCastingMode=ShadowCastingMode.Off;s.Renderer.receiveShadows=false;}
            try{
                var p=new Vector3[data.Positions.Length/3];var normals=new Vector3[p.Length];var uv=new Vector2[p.Length];var depth=new Vector2[p.Length];
                for(int i=0;i<p.Length;i++){p[i]=new(data.Positions[i*3],data.Positions[i*3+1],data.Positions[i*3+2]);normals[i]=Vector3.up;uv[i]=new(data.UV[i*2],data.UV[i*2+1]);depth[i]=new(data.OriginalDepth[i],0);}
                s.Mesh.Clear();s.Mesh.indexFormat=p.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16;s.Mesh.vertices=p;s.Mesh.normals=normals;s.Mesh.uv=uv;s.Mesh.uv2=depth;s.Mesh.triangles=data.Indices.ToArray();
                s.Mesh.bounds=new Bounds(new Vector3(data.Size*.5f,0,data.Size*.5f),new Vector3(data.Size,.02f,data.Size));s.Data=data;s.Object.SetActive(true);s.Renderer.enabled=false;Remove(region);active.Add(region,s);Position(s);
                UploadedBytes+=(long)p.Length*40+(long)data.Indices.Length*(p.Length>65535?4:2);UploadCount++;
                if(UploadMeasured!=null)UploadMeasured(System.Diagnostics.Stopwatch.GetTimestamp()-start);
            }catch{s.Object.SetActive(false);pool.Push(s);throw;}
        }
        void Position(Slot s){var o=road.RenderOrigin;s.Object.transform.localPosition=new Vector3((float)(s.Data.OriginX-o.X),(float)-o.Y,(float)(s.Data.OriginZ-o.Z));}
        void Shift(Vector3 _){foreach(var s in active.Values)Position(s);}
        static float Phase(double value,double period)=>(float)((value%period)/period);
        void LateUpdate()
        {
            if(disposed||road==null)return;if(settings.generationEnabled)time+=Time.deltaTime;
            var material=settings.waterMaterials[(int)settings.season];if(material==null)throw new InvalidOperationException("Missing Hills water material");int detail=road.Core.TerrainConfig.Detail;
            foreach(var pair in active){var r=pair.Key;var s=pair.Value;var d=s.Data;s.Renderer.sharedMaterial=material;
                s.Renderer.enabled=settings.waterVisible&&r.Visible&&!r.Retired&&r.Residency!=null&&r.Residency.IsCurrent&&r.Residency.Key.Equals(d.Key)&&r.Residency.Revision==d.TerrainRevision&&r.Residency.LayerRevision==d.LayerRevision;
                block.Clear();block.SetFloat("_DepthEnabled",detail>=2?1:0);block.SetFloat("_WavesEnabled",detail>=1?1:0);
                block.SetVector("_PhaseA",new Vector4(Phase(d.OriginX+time,200),Phase(d.OriginZ+time,200),0,0));
                block.SetVector("_PhaseB",new Vector4(Phase(d.OriginX-time,800),Phase(d.OriginZ-time,800),0,0));
                block.SetVector("_PhaseC",new Vector4(Phase(d.OriginX-time,300),Phase(d.OriginZ-time,300),0,0));s.Renderer.SetPropertyBlock(block);}
        }
        void Remove(TerrainRegionState r){if(!active.Remove(r,out var s))return;s.Renderer.enabled=false;s.Object.SetActive(false);s.Data=null;if(pool.Count<32)pool.Push(s);else{Destroy(s.Mesh);Destroy(s.Object);}}
        void Reset(){foreach(var s in active.Values){s.Renderer.enabled=false;s.Object.SetActive(false);s.Data=null;pool.Push(s);}active.Clear();while(pool.Count>32){var s=pool.Pop();Destroy(s.Mesh);Destroy(s.Object);}time=0;}
        public void Release(){if(disposed)return;disposed=true;if(road!=null){road.Terrain.Core.WaterCommitted-=Commit;road.Terrain.Core.Released-=Remove;road.Terrain.Core.Reset-=Reset;road.OriginShifted-=Shift;}Reset();foreach(var s in pool){Destroy(s.Mesh);Destroy(s.Object);}pool.Clear();}
        void OnDestroy()=>Release();
    }
}
