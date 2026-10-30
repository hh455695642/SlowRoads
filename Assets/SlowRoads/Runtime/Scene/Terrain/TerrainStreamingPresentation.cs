using System;
using System.Collections.Generic;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Terrain;
using SlowRoads.Scene.Road;
using UnityEngine;

namespace SlowRoads.Scene.Terrain
{
    /// <summary>Only the presentation owner touches native Mesh/Renderer objects.
    /// Generation, coverage and progress are owned by the existing road/terrain sessions.
    /// Shared season materials, no per-tile material instances, RTs or custom passes.</summary>
    public sealed class TerrainStreamingPresentation : MonoBehaviour
    {
        sealed class Slot
        {
            public GameObject Object;
            public MeshRenderer Renderer;
            public TerrainMeshUploader Upload;
        }
        readonly Dictionary<TerrainRegionState,Slot> active=new();
        readonly Stack<Slot> pool=new();
        HillsSceneSettings settings;
        RoadStreamingPresentation road;
        bool disposed;
        Material runtimeMaterial,sourceMaterial;
        public TerrainStreamingSession Core {get;private set;}
        public int NativeMeshCount=>active.Count+pool.Count;
        public void Initialize(RoadStreamingPresentation road,HillsSceneSettings settings)
        {
            this.road=road;this.settings=settings;Core=new TerrainStreamingSession(road.Core,Upload);
            Core.Released+=ReleaseRegion;Core.Reset+=ReleaseAll;
            Core.CoverageChanged+=(r,c)=>{if(active.TryGetValue(r,out var slot))slot.Upload.UpdateOverlap(c);};
            road.OriginShifted+=OriginChanged;
        }
        Material ResolveMaterial()
        {
            var source=settings.ResolveTerrainMaterial();
            if(runtimeMaterial==null)runtimeMaterial=new Material(source){name="Hills terrain runtime surface",hideFlags=HideFlags.DontSave};
            if(source!=sourceMaterial){runtimeMaterial.CopyPropertiesFromMaterial(source);sourceMaterial=source;}
            return runtimeMaterial;
        }
        TerrainResidency Upload(TerrainRegionState region,TerrainMeshData data)
        {
            Slot slot;
            if(pool.Count>0)slot=pool.Pop();
            else
            {
                var go=new GameObject("Streamed terrain",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
                slot=new Slot{Object=go,Renderer=go.GetComponent<MeshRenderer>(),Upload=new TerrainMeshUploader()};
                go.GetComponent<MeshFilter>().sharedMesh=slot.Upload.Mesh;slot.Renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                slot.Renderer.receiveShadows=false;
            }
            try
            {
                slot.Object.name=region.Chunk?"Near terrain chunk "+region.Id:$"Far terrain {region.X},{region.Z}";
                slot.Object.SetActive(true);slot.Renderer.enabled=false;
                slot.Renderer.sharedMaterial=ResolveMaterial();
                slot.Upload.Upload(data,road.Core,slot.Object.transform,road.RenderOrigin);slot.Upload.SetFine(region.TargetLod==1);
                active.Add(region,slot);return slot.Upload.Residency;
            }
            catch{slot.Upload.Dispose();Destroy(slot.Object);throw;}
        }
        public int Coordinate(int budget)
        {
            if(road.Core.Ready&&road.Core.AtTickBoundary)
            {
                int used=Core.Advance(budget);
                if(used>=budget||Core.Building||Core.NeedsWork)return used;
                return used+road.Core.Advance(budget-used);
            }
            return road.Core.Advance(budget);
        }
        void LateUpdate()
        {
            if(disposed||road==null||road.Core==null)return;
            var material=ResolveMaterial();
            var origin=road.RenderOrigin;material.SetVector("_RenderOrigin",new Vector4((float)origin.X,(float)origin.Y,(float)origin.Z,0));
            material.SetFloat("vehicleIndex",(float)road.ObserverProgress);
            material.SetFloat("sinkDist",Core.Schedule!=null?(float)Core.Schedule.L2:400);
            material.SetFloat("offroad",road.Core.Config.Mesh.OffRoad?1:0);
            material.SetFloat("greenlane",road.Core.Config.Mesh.LaneStyle==SlowRoads.Generation.Road.HillsLaneStyle.OffRoad?1:0);
            foreach(var pair in active)
            {
                var r=pair.Key;var slot=pair.Value;slot.Renderer.sharedMaterial=material;
                slot.Renderer.enabled=settings.terrainVisible&&r.Visible&&!r.Retired&&slot.Upload.Residency.IsCurrent;
                slot.Upload.SetFine(r.TargetLod==1);
            }
        }
        void OriginChanged(Vector3 delta)
        {foreach(var slot in active.Values)slot.Upload.SetRenderOrigin(slot.Object.transform,road.RenderOrigin);}
        void ReleaseRegion(TerrainRegionState r)
        {
            if(!active.Remove(r,out var slot))return;
            slot.Renderer.enabled=false;slot.Object.SetActive(false);slot.Upload.Residency?.Retire();pool.Push(slot);
        }
        void ReleaseAll()
        {foreach(var slot in active.Values){slot.Renderer.enabled=false;slot.Object.SetActive(false);slot.Upload.Residency?.Retire();pool.Push(slot);}active.Clear();}
        void OnDrawGizmos()
        {
            if(settings==null||!settings.terrainDebug||disposed)return;
            foreach(var pair in active)
            {
                var r=pair.Key;var slot=pair.Value;var b=slot.Upload.Mesh.bounds;
                Gizmos.color=r.Retired?Color.gray:!r.Visible?Color.red:r.Chunk?(r.TargetLod==1?Color.yellow:new Color(1,.5f,0)):(r.TargetLod==1?Color.green:Color.cyan);
                Gizmos.DrawWireCube(b.center+slot.Object.transform.position,b.size);
                if(r.Chunk&&r.Visible&&r.Layout.Cells!=null)
                {
                    Gizmos.color=new Color(.5f,1,.2f);var origin=road.RenderOrigin;
                    foreach(var cell in r.Layout.Cells)if(cell.Inner)Gizmos.DrawWireCube(new Vector3((float)(cell.X*10+5-origin.X),b.center.y,(float)(cell.Z*10+5-origin.Z)),new Vector3(10,.1f,10));
                }
            }
        }
        public void Release()
        {
            if(disposed)return;disposed=true;if(road!=null)road.OriginShifted-=OriginChanged;Core?.Dispose();ReleaseAll();
            foreach(var slot in pool){slot.Upload.Dispose();if(slot.Object!=null)Destroy(slot.Object);}pool.Clear();if(runtimeMaterial!=null)Destroy(runtimeMaterial);runtimeMaterial=null;sourceMaterial=null;
        }
        void OnDestroy()=>Release();
    }
}
