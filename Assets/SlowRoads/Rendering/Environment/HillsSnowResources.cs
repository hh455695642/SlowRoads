using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace SlowRoads.Rendering.Environment
{
    // One bounded camera-relative cluster. Simulation/culling run on GPU, source order retained.
    public sealed class HillsSnowResources : IDisposable
    {
        public const int FullCount=48841,HalfCount=5476;
        public const uint LayoutSeed=0x053A07E5;
        public readonly Material Material;
        public readonly ComputeShader Compute;
        public readonly MaterialPropertyBlock Properties=new();
        public GraphicsBuffer Full {get;private set;}
        public GraphicsBuffer Half {get;private set;}
        public GraphicsBuffer Prepared {get;private set;}
        public GraphicsBuffer Args {get;private set;}
        public Mesh Quad {get;private set;}
        public int AllocationCount {get;private set;}
        public int ArgumentUploads {get;private set;}
        public int Count {get;private set;}
        public bool Supported=>SystemInfo.supportsComputeShaders&&SystemInfo.supportsInstancing&&SystemInfo.supportsIndirectArgumentsBuffer&&SystemInfo.graphicsShaderLevel>=45&&Material!=null&&Material.shader.isSupported&&Compute!=null;
        public bool Allocated=>Prepared!=null;
        public long BufferBytes=>Allocated?(long)(FullCount+HalfCount)*12+(long)FullCount*32+20:0;
        public int Kernel {get;private set;}
        public GraphicsBuffer Input=>Count==HalfCount?Half:Full;
        public HillsSnowResources(Material material,ComputeShader compute){Material=material;Compute=compute;}
        public static (Vector3[] full,Vector3[] half) CreatePoints(Func<double> random)
        {
            if(random==null)throw new ArgumentNullException(nameof(random));
            var full=new Vector3[FullCount];var half=new Vector3[HalfCount];int f=0,h=0;
            // Original gi: half consumes a SECOND random number, interleaved with the full grid.
            for(int x=0;x<=220;x++)for(int z=0;z<=220;z++)
            {
                float px=(float)(x/220d*30),pz=(float)(z/220d*30);
                full[f++]=new Vector3(px,(float)(random()*30),pz);
                if(x%3==0&&z%3==0)half[h++]=new Vector3(px,(float)(random()*30),pz);
            }
            return (full,half);
        }
        void Allocate()
        {
            uint seed=LayoutSeed;double Next(){seed=unchecked(seed*1664525u+1013904223u);return seed/4294967296d;}
            var points=CreatePoints(Next);
            try{
                Full=new GraphicsBuffer(GraphicsBuffer.Target.Structured,FullCount,12);Full.SetData(points.full);
                Half=new GraphicsBuffer(GraphicsBuffer.Target.Structured,HalfCount,12);Half.SetData(points.half);
                Prepared=new GraphicsBuffer(GraphicsBuffer.Target.Structured,FullCount,32);
                Args=new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,5,4);
                Quad=new Mesh{name="Hills snow unit quad",hideFlags=HideFlags.DontSave};
                Quad.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(-1,1,0),new Vector3(1,1,0)};Quad.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one};Quad.triangles=new[]{0,2,1,1,2,3};Quad.RecalculateBounds();
                Kernel=Compute.FindKernel("PrepareSnow");Properties.SetBuffer("_SnowPrepared",Prepared);AllocationCount++;
            }catch{Dispose();throw;}
        }
        public bool Prepare(int count)
        {
            if(count!=0&&count!=HalfCount&&count!=FullCount)throw new ArgumentOutOfRangeException(nameof(count));
            if(count==0||!Supported){Count=0;return false;}
            if(!Allocated)Allocate();
            if(Count!=count){Count=count;Args.SetData(new uint[]{6,(uint)count,0,0,0});ArgumentUploads++;}return true;
        }
        public void Dispose()
        {
            Full?.Dispose();Half?.Dispose();Prepared?.Dispose();Args?.Dispose();Full=Half=Prepared=Args=null;Count=0;
            if(Quad!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(Quad);else UnityEngine.Object.DestroyImmediate(Quad);Quad=null;}
        }
    }
    // Captured by value for RenderGraph; no mutable per-frame material/global parameters.
    public readonly struct HillsSnowFrame
    {
        public readonly Matrix4x4 ModelView,Projection;
        public readonly Vector4 Offset,Parameters,Screen,Colour;
        public HillsSnowFrame(HillsEnvironmentState state,Matrix4x4 view,Matrix4x4 projection,int width,int height)
        {
            var camera=state.Camera.transform;var forward=camera.forward;forward.y=0;forward.Normalize();var local=camera.position+forward*15;
            ModelView=view*Matrix4x4.Translate(local-Vector3.one*15);Projection=projection;
            Offset=new Vector4((float)(state.OriginX+local.x),local.y,(float)(state.OriginZ+local.z),0);
            Parameters=new Vector4((float)state.SnowTime,state.Profile.Snow.Speed,.3f,height*.5f);
            Screen=new Vector4(width,height,1,1024);Colour=(Vector4)HillsEnvironmentProfile.LinearColour(state.Profile.Snow.Colour);
        }
        public void Bind(MaterialPropertyBlock p){p.SetVector("_SnowColour",Colour);p.SetVector("_SnowScreen",Screen);}
        public void Record(CommandBuffer cmd,HillsSnowResources resource)
        {
            var c=resource.Compute;int k=resource.Kernel;
            cmd.SetComputeMatrixParam(c,"_SnowModelView",ModelView);cmd.SetComputeMatrixParam(c,"_SnowProjection",Projection);
            cmd.SetComputeVectorParam(c,"_SnowOffset",Offset);cmd.SetComputeVectorParam(c,"_SnowParameters",Parameters);cmd.SetComputeVectorParam(c,"_SnowScreen",Screen);
            cmd.SetComputeTextureParam(c,k,"_SnowNoise",resource.Material.GetTexture("_SnowNoise"));
            cmd.SetComputeBufferParam(c,k,"_SnowPositions",resource.Input);cmd.SetComputeBufferParam(c,k,"_SnowPrepared",resource.Prepared);cmd.SetComputeIntParam(c,"_SnowCount",resource.Count);cmd.DispatchCompute(c,k,(resource.Count+127)/128,1,1);
        }
    }
}
