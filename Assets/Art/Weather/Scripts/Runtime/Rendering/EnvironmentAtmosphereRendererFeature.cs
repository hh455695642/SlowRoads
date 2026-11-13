using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;
namespace SlowRoads.Environment
{
    [MovedFrom(true,"SlowRoads.Rendering","SlowRoads.ContentsRendering","HillsEnvironmentRendererFeature")]
    public sealed class EnvironmentAtmosphereRendererFeature : ScriptableRendererFeature
    {
#if UNITY_EDITOR
        [UnityEngine.Tooltip("Scene View preview uses the same environment resources and render passes. Game cameras keep their own bindings.")]
        public EnvironmentResources sceneViewResources;
        public bool previewInSceneView=true;
        // Editor assembly owns preview-camera lifetimes; no UnityEditor dependency or preview work in players.
        public static event System.Action<EnvironmentAtmosphereRendererFeature,UnityEngine.Camera> SceneViewRendering;
        public static event System.Action<EnvironmentAtmosphereRendererFeature> SceneViewReleased;
        public override void OnCameraPreCull(ScriptableRenderer renderer,in CameraData data)
        {
            // Pre-cull runs before every Feature's AddRenderPasses, independent of Feature order.
            if(data.isSceneViewCamera)SceneViewRendering?.Invoke(this,data.camera);
        }
        protected override void Dispose(bool disposing)=>SceneViewReleased?.Invoke(this);
#endif
        EnvironmentAtmospherePass bind,clear;
        public override void Create(){bind=new EnvironmentAtmospherePass();clear=new EnvironmentAtmospherePass(true);}
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data){renderer.EnqueuePass(bind);renderer.EnqueuePass(clear);}
    }
}
