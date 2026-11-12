using SlowRoads.Rendering.Environment;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace SlowRoads.Rendering.Passes
{
    // Before all depth/colour draws, independently of sky visibility. No attachments, copies or draws.
    public sealed class HillsEnvironmentPass : ScriptableRenderPass
    {
        readonly bool clear;
        public HillsEnvironmentPass(bool clear=false){this.clear=clear;renderPassEvent=clear?RenderPassEvent.AfterRendering:RenderPassEvent.BeforeRendering;}
        sealed class Data{public HillsEnvironmentUniforms Uniforms;}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
        {
            var camera=frame.Get<UniversalCameraData>().camera;HillsEnvironmentState.TryGet(camera,out var state);
            using var builder=graph.AddUnsafePass<Data>(clear?"Hills environment clear":"Hills environment globals",out var data);
            if(!clear&&state!=null)state.SetSurfaceInput(state.ObserverShadow); // Final camera pose, after all LateUpdate followers.
            data.Uniforms=new HillsEnvironmentUniforms(clear?null:state);builder.AllowPassCulling(false);builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static(Data d,UnsafeGraphContext context)=>d.Uniforms.Record(CommandBufferHelpers.GetNativeCommandBuffer(context.cmd)));
        }
    }
}
