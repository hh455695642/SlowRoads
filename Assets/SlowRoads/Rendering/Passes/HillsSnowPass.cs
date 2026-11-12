using SlowRoads.Rendering.Environment;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
namespace SlowRoads.Rendering.Passes
{
    // After opaque depth, before ordinary transparents. No depth copy, RT, normals or shadow pass.
    public sealed class HillsSnowPass : ScriptableRenderPass
    {
        readonly bool prepare;
        public HillsSnowPass(bool prepare){this.prepare=prepare;renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingTransparents-1);}
        sealed class Data{public HillsSnowResources Resources;public HillsSnowFrame Frame;}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
        {
            var camera=frame.Get<UniversalCameraData>();if(!HillsEnvironmentState.TryGet(camera.camera,out var state)||!state.SnowVisible||state.Snow?.Allocated!=true)return;
            var snow=state.Snow;var r=frame.Get<UniversalResourceData>();
            // RenderGraph target ownership supplies Y flip; the compatibility projection API
            // is not valid here (returned a zero matrix in the actual Unity 6.3 RG camera).
            var projection=GL.GetGPUProjectionMatrix(camera.GetProjectionMatrix(),!r.isActiveTargetBackBuffer||camera.targetTexture!=null);
            var parameters=new HillsSnowFrame(state,camera.GetViewMatrix(),projection,camera.cameraTargetDescriptor.width,camera.cameraTargetDescriptor.height);
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
