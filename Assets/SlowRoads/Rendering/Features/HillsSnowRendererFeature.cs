using SlowRoads.Rendering.Environment;
using SlowRoads.Rendering.Passes;
using UnityEngine.Rendering.Universal;
namespace SlowRoads.Rendering
{
    public sealed class HillsSnowRendererFeature : ScriptableRendererFeature
    {
        HillsSnowPass prepare,draw;
        public override void Create(){prepare=new HillsSnowPass(true);draw=new HillsSnowPass(false);}
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(!HillsEnvironmentState.TryGet(data.cameraData.camera,out var state)||!state.SnowVisible||state.Snow==null||!state.Snow.Prepare(state.Profile.Snow.PointCount))return;
            renderer.EnqueuePass(prepare);renderer.EnqueuePass(draw);
        }
    }
}
