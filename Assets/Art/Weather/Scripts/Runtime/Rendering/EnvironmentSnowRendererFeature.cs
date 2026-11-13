using UnityEngine.Rendering.Universal;
namespace SlowRoads.Environment
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true,"SlowRoads.Rendering","SlowRoads.ContentsRendering","HillsSnowRendererFeature")]
    public sealed class EnvironmentSnowRendererFeature : ScriptableRendererFeature
    {
        EnvironmentSnowPass prepare,draw;
        public override void Create(){prepare=new EnvironmentSnowPass(true);draw=new EnvironmentSnowPass(false);}
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(data.cameraData.renderType==CameraRenderType.Overlay)return;
            if(!EnvironmentCameraState.TryGet(data.cameraData.camera,out var state)||state.Snow==null)return;
            renderer.EnqueuePass(prepare);renderer.EnqueuePass(draw);
        }
    }
}
