using System;
using SlowRoads.Generation.Contents.Grass;
using SlowRoads.Rendering.Grass;
using SlowRoads.Scene.Road;
using UnityEngine;

namespace SlowRoads.Scene.Contents
{
    /// <summary>Main-scene display owner. C5 CPU jobs remain inside the P5/T4 producer barrier;
    /// this component only handles stable GPU uploads, uniforms and disposal.</summary>
    public sealed class GrassStreamingPresentation : MonoBehaviour
    {
        RoadStreamingPresentation road;HillsSceneSettings settings;int season=-1;bool disposed;
        public GrassStreamingSession Core {get;private set;}public GrassGpuResources Gpu {get;private set;}
        public GrassInstanceWindow Window {get;private set;}
        public void Initialize(RoadStreamingPresentation road,HillsSceneSettings settings,SlowRoads.Generation.Contents.Fields.FieldRandomState.Stream moduleDensity=null)
        {
            this.road=road;this.settings=settings;
            if(settings.grassMesh==null||settings.bushMesh==null||settings.grassMaterials?.Length!=4||settings.bushMaterials?.Length!=4)throw new InvalidOperationException("Ground cover reference assets are not configured");
            Core=new GrassStreamingSession(road.Core,road.Terrain.Core,road.Terrain.Fields,road.Structures.Walls,moduleDensity);
            Gpu=new GrassGpuResources(settings.grassMesh,settings.bushMesh,road.ThemeMaterials.Resolve(settings.grassMaterials[(int)settings.season]),road.ThemeMaterials.Resolve(settings.bushMaterials[(int)settings.season]));
            Window=new GrassInstanceWindow(GrassWindowConfig.From(road.Core.TerrainConfig),road.Core.Midline.State.ObserverIndex);
            Core.Committed+=Retain;Core.Cleared+=ClearWindow;Window.Changed+=Gpu.Enqueue;Window.Released+=Gpu.Remove;SelectMaterials();
        }
        void Retain(GrassChunkData data)=>Window.Append(data,data.ObserverIndex);
        void ClearWindow()=>Window.Clear(road.Core.Midline.State.ObserverIndex);
        void SelectMaterials()
        {
            season=(int)settings.season;Gpu.SetMaterials(road.ThemeMaterials.Resolve(settings.grassMaterials[season]),road.ThemeMaterials.Resolve(settings.bushMaterials[season]));
            var origin=settings.ResolveTerrainMaterial().GetVector("_SourceOrigin");Gpu.TextureOrigin=new Vector2(origin.x,origin.y);
        }
        void LateUpdate()
        {
            if(disposed||Gpu==null)return;if(season!=(int)settings.season)SelectMaterials();
            Gpu.Visible=settings.grassVisible&&Gpu.UseCompute;Gpu.RenderOrigin=road.RenderOrigin;Gpu.SetAmbient(RenderSettings.ambientProbe);
            if(Gpu.HasPendingUploads)Gpu.Advance(Math.Max(1,settings.grassUploadInstancesPerFrame));
        }
        public void Release()
        {
            if(disposed)return;disposed=true;
            if(Core!=null){Core.Committed-=Retain;Core.Cleared-=ClearWindow;}
            Core?.Dispose();Core=null;if(Window!=null){Window.Changed-=Gpu.Enqueue;Window.Released-=Gpu.Remove;Window.Clear();Window=null;}Gpu?.Dispose();Gpu=null;
        }
        void OnDestroy()=>Release();
    }
}
