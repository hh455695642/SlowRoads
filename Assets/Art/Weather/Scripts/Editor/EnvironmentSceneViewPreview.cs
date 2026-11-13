using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment.Editor
{
    // Editor-only camera owners. Existing Features/Passes still resolve URP's own per-camera VolumeStack.
    [InitializeOnLoad]
    public static class EnvironmentSceneViewPreview
    {
        sealed class Entry
        {
            public EnvironmentAtmosphereRendererFeature Feature;
            public SceneView View;
            public EnvironmentCameraState State;
            double previous,cloudTime,started;
            bool ownsClear;CameraClearFlags originalClear;
            public Entry(EnvironmentAtmosphereRendererFeature feature,SceneView view)
            {
                Feature=feature;View=view;State=new EnvironmentCameraState(view.camera);started=previous=EditorApplication.timeSinceStartup;
                State.PrepareFrame=Prepare;
                State.FilterFrame=f=>f.WithVisibility(View.sceneViewState.showSkybox,View.sceneViewState.showFog);
            }
            public void Sync()
            {
                var r=Feature.sceneViewResources;
                State.SetSkyMaterial(r.skyMaterial);State.SetCloudMaterial(r.cloudMaterial);State.SetSnowResources(r.snowMaterial,r.snowCulling);
            }
            public void PrepareBackground()
            {
                RestoreBackground();
                var stack=VolumeManager.instance.stack;
                bool background=View.sceneViewState.showSkybox&&((State.Sky!=null&&stack.GetComponent<EnvironmentSkyVolume>().enabled.value)||(State.Clouds!=null&&stack.GetComponent<EnvironmentCloudVolume>().enabled.value));
                // URP draws its built-in skybox after opaques. It must not overwrite our 249 background.
                // Only this Scene camera is changed, and only until endCameraRendering.
                if(background){originalClear=View.camera.clearFlags;View.camera.clearFlags=CameraClearFlags.SolidColor;ownsClear=true;}
            }
            public void RestoreBackground()
            {
                if(ownsClear&&State.Camera!=null&&State.Camera.clearFlags==CameraClearFlags.SolidColor)State.Camera.clearFlags=originalClear;
                ownsClear=false;
            }
            void Prepare()
            {
                double now=EditorApplication.timeSinceStartup,delta=now-previous;previous=now;
                var main=Camera.main;
                if(Application.isPlaying&&main!=null&&EnvironmentCameraState.TryGet(main,out var host)&&host!=State)
                {
                    // Read only: scene-camera height/Volume blending remain independent; never tick the host twice.
                    var input=host.Input;State.Input=new(View.camera.transform.position.y,input.CloudTime,input.SnowTime,input.OriginX,input.OriginZ);
                }
                else
                {
                    if(State.Frame.CloudsEnabled)cloudTime+=delta*State.Frame.CloudGeometry.z;
                    State.Input=new(View.camera.transform.position.y,cloudTime,now-started);
                }
            }
        }
        static readonly Dictionary<Camera,Entry> Entries=new();
        static readonly List<Camera> Retired=new();
        static double nextCleanup;
        public static int OwnerCount=>Entries.Count;
        static EnvironmentSceneViewPreview()
        {
            EnvironmentAtmosphereRendererFeature.SceneViewRendering+=Prepare;
            EnvironmentAtmosphereRendererFeature.SceneViewReleased+=Release;
            AssemblyReloadEvents.beforeAssemblyReload+=ReleaseAll;EditorApplication.quitting+=ReleaseAll;
            EditorApplication.playModeStateChanged+=_=>ReleaseAll();EditorApplication.update+=Cleanup;
            RenderPipelineManager.endCameraRendering+=(_,camera)=>{if(Entries.TryGetValue(camera,out var entry))entry.RestoreBackground();};
        }
        public static void Prepare(EnvironmentAtmosphereRendererFeature feature,Camera camera)
        {
            if(camera==null||camera.cameraType!=UnityEngine.CameraType.SceneView)return;
            SceneView view=null;foreach(SceneView candidate in SceneView.sceneViews)if(candidate.camera==camera){view=candidate;break;}
            if(view==null||!feature.isActive||!feature.previewInSceneView||feature.sceneViewResources==null||!view.sceneViewState.showImageEffects){Remove(camera);return;}
            if(Entries.TryGetValue(camera,out var entry)&&entry.Feature!=feature){Remove(camera);entry=null;}
            if(entry==null)
            {
                // An explicitly installed camera owner takes precedence; never dispose an external binding.
                if(EnvironmentCameraState.TryGet(camera,out _))return;
                entry=new Entry(feature,view);Entries.Add(camera,entry);
            }
            entry.Sync();entry.PrepareBackground();
        }
        static void Remove(Camera camera){if(!Entries.Remove(camera,out var entry))return;entry.RestoreBackground();entry.State.Dispose();}
        static void Release(EnvironmentAtmosphereRendererFeature feature)
        {
            Retired.Clear();foreach(var pair in Entries)if(pair.Value.Feature==feature)Retired.Add(pair.Key);
            foreach(var camera in Retired)Remove(camera);Retired.Clear();
        }
        public static void ReleaseAll(){foreach(var entry in Entries.Values){entry.RestoreBackground();entry.State.Dispose();}Entries.Clear();Retired.Clear();}
        static void Cleanup(){if(EditorApplication.timeSinceStartup<nextCleanup)return;nextCleanup=EditorApplication.timeSinceStartup+1;Prune();}
        public static void Prune()
        {
            Retired.Clear();
            foreach(var pair in Entries)
            {
                var e=pair.Value;
                if(pair.Key==null||e.View==null||e.Feature==null||!e.Feature.isActive||!e.Feature.previewInSceneView||e.Feature.sceneViewResources==null||!e.View.sceneViewState.showImageEffects)Retired.Add(pair.Key);
            }
            foreach(var camera in Retired)Remove(camera);Retired.Clear();
        }
    }
}
