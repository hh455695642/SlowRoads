using UnityEngine.Rendering.Universal;

namespace SlowRoads.Environment
{
    public sealed class EnvironmentSkyRendererFeature : ScriptableRendererFeature
    {
        EnvironmentSkyPass pass;
        public override void Create() => pass = new EnvironmentSkyPass();
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if(data.cameraData.renderType==CameraRenderType.Overlay)return;
            if (!EnvironmentCameraState.TryGet(data.cameraData.camera, out var state)) return;
            if (state.Sky == null && state.Clouds == null) return;
            renderer.EnqueuePass(pass);
        }
    }
}
