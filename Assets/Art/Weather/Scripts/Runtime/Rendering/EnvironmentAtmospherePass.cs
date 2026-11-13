using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace SlowRoads.Environment
{
    // Parameter-only passes; no attachments, copies or draws.
    public sealed class EnvironmentAtmospherePass : ScriptableRenderPass
    {
        readonly bool clear;
        public EnvironmentAtmospherePass(bool clear=false){this.clear=clear;renderPassEvent=clear?RenderPassEvent.AfterRendering:RenderPassEvent.BeforeRendering;}
        sealed class Data{public EnvironmentSurfaceUniforms Uniforms;}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
        {
            var camera=frame.Get<UniversalCameraData>().camera;EnvironmentCameraState.TryGet(camera,out var state);
            if(!clear)state=EnvironmentFrameContext.Resolve(frame);
            using var builder=graph.AddUnsafePass<Data>(clear?"Hills environment clear":"Hills environment globals",out var data);
            data.Uniforms=!clear&&state!=null?new EnvironmentSurfaceUniforms(state.Frame,state.Revision):default;
            builder.AllowPassCulling(false);builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc(static(Data d,UnsafeGraphContext c)=>{var cmd=CommandBufferHelpers.GetNativeCommandBuffer(c.cmd);d.Uniforms.Record(cmd);});
        }
    }
}
