using SlowRoads.Rendering.Environment;
using SlowRoads.Rendering.Passes;
using UnityEngine.Rendering.Universal;

namespace SlowRoads.Rendering
{
    public sealed class HillsSkyRendererFeature : ScriptableRendererFeature
    {
        HillsSkyPass pass;HillsEnvironmentPass environment,clear;
        public override void Create(){pass=new HillsSkyPass();environment=new HillsEnvironmentPass();clear=new HillsEnvironmentPass(true);}
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            renderer.EnqueuePass(environment);renderer.EnqueuePass(clear); // Also clears globals for Scene/Preview cameras without an owner.
            var camera=data.cameraData.camera;
            if(!HillsEnvironmentState.TryGet(camera,out var state))return;
            if(!(state.SkyVisible&&state.Sky!=null)&&!(state.CloudsVisible&&state.Clouds!=null))return;
            renderer.EnqueuePass(pass);
        }
    }
}
