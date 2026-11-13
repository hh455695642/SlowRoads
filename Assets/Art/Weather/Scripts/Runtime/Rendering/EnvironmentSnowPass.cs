using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace SlowRoads.Environment
{
    // After opaque depth, before ordinary transparents. No depth copy, RT, normals or shadow pass.
    public sealed class EnvironmentSnowPass : ScriptableRenderPass
    {
        readonly bool prepare;
        public EnvironmentSnowPass(bool prepare){this.prepare=prepare;renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents-1);}
        sealed class Data{public EnvironmentSnowResources Resources;public EnvironmentSnowFrame Frame;}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
        {
            var camera=frame.Get<UniversalCameraData>();var state=EnvironmentFrameContext.Resolve(frame);if(state?.Snow==null)return;
            var snow=state.Snow;if(prepare){if(!snow.Prepare(state.Frame.SnowCount))return;}else if(!snow.Allocated||snow.Count==0)return;var r=frame.Get<UniversalResourceData>();
            // RenderGraph target ownership supplies Y flip; the compatibility projection API
            // is not valid here (returned a zero matrix in the actual Unity 6.3 RG camera).
            var projection=GL.GetGPUProjectionMatrix(camera.GetProjectionMatrix(),!r.isActiveTargetBackBuffer||camera.targetTexture!=null);
            var parameters=new EnvironmentSnowFrame(state.Frame,state.Camera,camera.GetViewMatrix(),projection,camera.cameraTargetDescriptor.width,camera.cameraTargetDescriptor.height);
            if(prepare)
            {
                using var builder=graph.AddUnsafePass<Data>("Hills snow GPU prepare",out var data);data.Resources=snow;data.Frame=parameters;
                builder.UseBuffer(graph.ImportBuffer(snow.Input),AccessFlags.Read);builder.UseBuffer(graph.ImportBuffer(snow.Prepared),AccessFlags.Write);builder.AllowPassCulling(false);
                builder.SetRenderFunc(static(Data d,UnsafeGraphContext c)=>d.Frame.Record(CommandBufferHelpers.GetNativeCommandBuffer(c.cmd),d.Resources));
            }
            else
            {
                using var builder=graph.AddRasterRenderPass<Data>("Hills snow",out var data);data.Resources=snow;data.Frame=parameters;parameters.Bind(snow.Properties);
                builder.SetRenderAttachment(r.activeColorTexture,0,AccessFlags.ReadWrite);builder.SetRenderAttachmentDepth(r.activeDepthTexture,AccessFlags.ReadWrite);
                builder.UseBuffer(graph.ImportBuffer(snow.Prepared),AccessFlags.Read);builder.UseBuffer(graph.ImportBuffer(snow.Args),AccessFlags.Read);builder.AllowPassCulling(false);
                builder.SetRenderFunc(static(Data d,RasterGraphContext c)=>c.cmd.DrawMeshInstancedIndirect(d.Resources.Quad,0,d.Resources.Material,0,d.Resources.Args,0,d.Resources.Properties));
            }
        }
    }
}
