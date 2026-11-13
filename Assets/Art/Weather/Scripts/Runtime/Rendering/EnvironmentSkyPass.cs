using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SlowRoads.Environment
{
    public sealed class EnvironmentSkyPass : ScriptableRenderPass
    {
        // After prepasses, before tree and ordinary opaque colour. No additional RT, blit, depth or colour copy.
        public EnvironmentSkyPass(){renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.BeforeRenderingOpaques-1);}
        sealed class Data{public Mesh Mesh,CloudMesh;public Material Material,CloudMaterial;public MaterialPropertyBlock Properties,CloudProperties;public Matrix4x4 Matrix,CloudMatrix;}
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
        {
            var camera=frame.Get<UniversalCameraData>().camera;
            var state=EnvironmentFrameContext.Resolve(frame);if(state==null)return;
            bool skyVisible=state.Frame.Sky.Enabled&&state.Sky!=null,cloudVisible=state.Frame.CloudsEnabled&&state.Clouds!=null;
            if(!skyVisible&&!cloudVisible)return;
            var resources=frame.Get<UniversalResourceData>();
            using var builder=graph.AddRasterRenderPass<Data>("Hills sky",out var data);
            // RenderGraph pools Data. Explicitly clear optional draws on visibility/revision changes.
            data.Mesh=null;data.CloudMesh=null;
            if(skyVisible){var sky=state.Sky;data.Mesh=sky.Mesh;data.Material=sky.Material;data.Properties=sky.Properties;data.Matrix=sky.Bind(state.Frame,camera);}
            if(cloudVisible){var cloud=state.Clouds;data.CloudMesh=cloud.Mesh;data.CloudMaterial=cloud.Material;data.CloudProperties=cloud.Properties;data.CloudMatrix=cloud.Bind(state.Frame,camera);}
            builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc(static (Data d,RasterGraphContext context)=>{
                if(d.Mesh!=null)context.cmd.DrawMesh(d.Mesh,d.Matrix,d.Material,0,0,d.Properties);
                if(d.CloudMesh!=null)context.cmd.DrawMesh(d.CloudMesh,d.CloudMatrix,d.CloudMaterial,0,0,d.CloudProperties);
            });
        }
    }
}
