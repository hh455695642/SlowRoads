using System.Collections.Generic;
using SlowRoads.Rendering.Trees;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
namespace SlowRoads.Rendering
{
    /// <summary>Dedicated vegetation feature, no RT allocation or full-screen copy.
    /// Compute precedes existing prepass consumers; tree normals are appended before
    /// SSAO (+1). Forward writes the active camera depth independently of its copy.</summary>
    public sealed class HillsTreeRendererFeature : ScriptableRendererFeature
    {
        public ComputeShader culling;
        TreeCullPass cull;
        TreeDrawPass depth,forward;
        public override void Create(){cull=new();depth=new(true);forward=new(false);}
        public override void OnCameraPreCull(ScriptableRenderer renderer,in CameraData cameraData)
        {
            // Submit shadow-only indirect renderers BEFORE URP culling, so the built-in
            // light shadow passes own targets/cascades. No camera-visible duplicate.
            if(cameraData.cameraType==CameraType.Preview)return;
            foreach(var o in TreeGpuResources.Active)if(o.Visible&&o.CastShadows&&o.UseCompute)foreach(var list in o.Clusters)foreach(var b in list)if(b.Lod==0&&b.HasNear)
            {
                Vector3 delta=b.Bounds.center+o.Origin(b)-cameraData.camera.transform.position;
                if(new Vector2(delta.x,delta.z).magnitude>o.NearDistance+b.Bounds.extents.magnitude)continue;
                b.ShadowProperties.SetVector("_ClusterOrigin",o.Origin(b));b.ShadowProperties.SetFloat("_LodNear",o.NearDistance);b.ShadowProperties.SetFloat("_LodFar",o.FarDistance);
                b.ShadowProperties.SetVector("_NoiseOffset",new Vector4((float)(o.RenderOrigin.X%256),0,(float)(o.RenderOrigin.Z%256),0));
                var bounds=b.Bounds;bounds.center+=o.Origin(b);
                Graphics.DrawMeshInstancedIndirect(b.Mesh,0,b.Material,bounds,b.ShadowArgs,0,b.ShadowProperties,ShadowCastingMode.ShadowsOnly,false,0,cameraData.camera);
            }
        }
        static bool VisibleCluster(TreeGpuResources owner,TreeGpuBatch batch,Camera camera,Plane[] planes)
        {
            if(owner.UseCompute&&batch.Lod==0&&!batch.HasNear)return false;
            var bounds=batch.Bounds;bounds.center+=owner.Origin(batch);
            float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            Vector3 d=bounds.center-camera.transform.position;float limit=batch.Lod==0?owner.NearDistance*Mathf.Sqrt(1+tan*tan*(1+camera.aspect*camera.aspect)):owner.FarDistance;
            if(new Vector2(d.x,d.z).sqrMagnitude>(limit+bounds.extents.magnitude)*(limit+bounds.extents.magnitude))return false;
            return GeometryUtility.TestPlanesAABB(planes,bounds);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data)
        {
            if(data.cameraData.cameraType==CameraType.Preview||TreeGpuResources.Active.Count==0)return;
            cull.shader=culling;renderer.EnqueuePass(cull);renderer.EnqueuePass(depth);renderer.EnqueuePass(forward);
        }
        sealed class CullData {public ComputeShader Shader;public int Kernel;public Camera Camera;public readonly List<(TreeGpuResources owner,TreeGpuBatch batch)> Batches=new();}
        sealed class TreeCullPass : ScriptableRenderPass
        {
            public ComputeShader shader;
            public TreeCullPass(){renderPassEvent=RenderPassEvent.BeforeRenderingPrePasses;}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                if(shader==null)return;
                bool any=false;foreach(var owner in TreeGpuResources.Active)if(owner.Visible&&owner.UseCompute&&owner.BatchCount>0){any=true;break;}if(!any)return;
                using var builder=graph.AddComputePass<CullData>("Hills trees GPU culling",out var data);
                // RenderGraph pools pass data across frames. Never retain disposed
                // region buffers or append last frame's draw list to this frame.
                data.Batches.Clear();
                data.Shader=shader;data.Kernel=shader.FindKernel("Cull");data.Camera=frame.Get<UniversalCameraData>().camera;
                var frustum=GeometryUtility.CalculateFrustumPlanes(data.Camera);
                foreach(var owner in TreeGpuResources.Active)if(owner.Visible&&owner.UseCompute)foreach(var list in owner.Clusters)foreach(var b in list)
                {
                    if(!VisibleCluster(owner,b,data.Camera,frustum))continue;
                    data.Batches.Add((owner,b));builder.UseBuffer(graph.ImportBuffer(b.Input),AccessFlags.Read);
                    builder.UseBuffer(graph.ImportBuffer(b.Visible),AccessFlags.Write);builder.UseBuffer(graph.ImportBuffer(b.Args),AccessFlags.ReadWrite);
                }
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (CullData d,ComputeGraphContext context)=>{
                    var planes=GeometryUtility.CalculateFrustumPlanes(d.Camera);var vectors=new Vector4[6];for(int i=0;i<6;i++)vectors[i]=new(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
                    context.cmd.SetComputeVectorArrayParam(d.Shader,"_Planes",vectors);context.cmd.SetComputeVectorParam(d.Shader,"_Camera",d.Camera.transform.position);
                    context.cmd.SetComputeMatrixParam(d.Shader,"_View",d.Camera.worldToCameraMatrix);
                    foreach(var pair in d.Batches){var o=pair.owner;var b=pair.batch;
                        context.cmd.SetBufferCounterValue(b.Visible,0);context.cmd.SetComputeBufferParam(d.Shader,d.Kernel,"_Trees",b.Input);
                        context.cmd.SetComputeBufferParam(d.Shader,d.Kernel,"_Visible",b.Visible);
                        context.cmd.SetComputeIntParam(d.Shader,"_Count",b.Count);context.cmd.SetComputeIntParam(d.Shader,"_Lod",b.Lod);
                        context.cmd.SetComputeFloatParam(d.Shader,"_Near",o.NearDistance);context.cmd.SetComputeFloatParam(d.Shader,"_Far",o.FarDistance);
                        context.cmd.SetComputeVectorParam(d.Shader,"_Origin",o.Origin(b));
                        context.cmd.DispatchCompute(d.Shader,d.Kernel,(b.Count+63)/64,1,1);context.cmd.CopyCounterValue(b.Visible,b.Args,4);
                    }
                });
            }
        }
        sealed class DrawData {public bool Depth;public readonly List<(TreeGpuResources owner,TreeGpuBatch batch)> Batches=new();}
        sealed class TreeDrawPass : ScriptableRenderPass
        {
            readonly bool depth;
            public TreeDrawPass(bool depth){this.depth=depth;renderPassEvent=depth?RenderPassEvent.AfterRenderingPrePasses:RenderPassEvent.BeforeRenderingOpaques;}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                bool any=false;foreach(var owner in TreeGpuResources.Active)if(owner.Visible&&owner.BatchCount>0){any=true;break;}if(!any)return;
                var r=frame.Get<UniversalResourceData>();
                if(depth&&(!r.cameraNormalsTexture.IsValid()||!r.cameraDepthTexture.IsValid()))return;
                using var builder=graph.AddRasterRenderPass<DrawData>(depth?"Hills tree depth normals":"Hills tree forward",out var data);
                data.Batches.Clear();
                data.Depth=depth;builder.SetRenderAttachment(depth?r.cameraNormalsTexture:r.activeColorTexture,0,AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(depth?r.cameraDepthTexture:r.activeDepthTexture,AccessFlags.ReadWrite);
                // These shaders sample material atlases only. Importing all global
                // textures would also read the normals attachment being written.
                builder.AllowPassCulling(false);
                var camera=frame.Get<UniversalCameraData>().camera;var frustum=GeometryUtility.CalculateFrustumPlanes(camera);
                foreach(var owner in TreeGpuResources.Active)if(owner.Visible)foreach(var list in owner.Clusters)foreach(var b in list)
                {
                    if(!VisibleCluster(owner,b,camera,frustum))continue;
                    if(b.Material==null)continue;owner.Bind(b);data.Batches.Add((owner,b));
                    if(owner.UseCompute){builder.UseBuffer(graph.ImportBuffer(b.Input),AccessFlags.Read);builder.UseBuffer(graph.ImportBuffer(b.Visible),AccessFlags.Read);builder.UseBuffer(graph.ImportBuffer(b.Args),AccessFlags.Read);}
                }
                builder.SetRenderFunc(static (DrawData d,RasterGraphContext context)=>{
                    foreach(var pair in d.Batches){var b=pair.batch;int pass=b.Material.FindPass(d.Depth?"DepthNormals":"Forward");if(pass<0)continue;
                        if(pair.owner.UseCompute)context.cmd.DrawMeshInstancedIndirect(b.Mesh,0,b.Material,pass,b.Args,0,b.Properties);
                        else if(b.CpuBatches!=null)foreach(var matrices in b.CpuBatches)context.cmd.DrawMeshInstanced(b.Mesh,0,b.Material,pass,matrices,matrices.Length,b.Properties);
                    }
                });
            }
        }
    }
}
