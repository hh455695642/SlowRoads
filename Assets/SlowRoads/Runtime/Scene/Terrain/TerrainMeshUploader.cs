using System;
using System.Runtime.InteropServices;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Streaming;
using SlowRoads.Generation.Terrain;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Scene.Terrain
{
    /// <summary>Terrain-specific buffer layout; shares P4's direct Mesh upload approach.
    /// Owns one native Mesh, no materials or scene objects. T4 owns scheduling and overlap.</summary>
    public sealed class TerrainMeshUploader : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct SurfaceChannels {public float roadProximity,curvature,overlap;}
        static readonly VertexAttributeDescriptor[] Layout={
            new(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),
            new(VertexAttribute.Normal,VertexAttributeFormat.Float32,3,1),
            new(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,2),
            new(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,3,3)};
        const MeshUpdateFlags Flags=MeshUpdateFlags.DontRecalculateBounds;
        ushort[] indices;
        float[] positions,uv,coarseNormals,fineNormals;
        SurfaceChannels[] channels;
        int coarseCount,fineStart,fineCount,vertices;
        bool fine=true,enabled=true;
        public Mesh Mesh {get;private set;}=new Mesh{name="Hills terrain"};
        public Double3 Origin {get;private set;}
        public int LastUploadedBytes {get;private set;}
        public int UploadCount {get;private set;}
        public TerrainResidency Residency {get;private set;}
        public bool Enabled {get=>enabled;set{Check();enabled=value;ApplyRange();}}
        void Check(){if(Mesh==null)throw new ObjectDisposedException(nameof(TerrainMeshUploader));}
        public bool Upload(TerrainMeshData data,RoadStreamingSession owner,Transform target=null,Double3 renderOrigin=default)
        {
            Check();if(!enabled)return false;
            if(data==null||!data.CanPublish(owner))throw new InvalidOperationException("Terrain generation or revision is stale");
            int count=data.VertexCount;
            if(count==0||count>65535)throw new ArgumentOutOfRangeException(nameof(data));
            for(int i=0;i<data.Positions.Length;i++)if(!float.IsFinite(data.Positions[i])||!float.IsFinite(data.Normals[i]))throw new ArgumentException("Non-finite terrain geometry");
            foreach(int i in data.Indices)if(i<0||i>=count)throw new ArgumentException("Invalid terrain index");
            if(vertices!=count||indices==null||indices.Length!=data.Indices.Length)
            {
                Mesh.Clear(false);Mesh.SetVertexBufferParams(count,Layout);
                indices=new ushort[data.Indices.Length];uv=new float[count*2];positions=new float[count*3];
                coarseNormals=new float[count*3];fineNormals=new float[count*3];
                channels=new SurfaceChannels[count];
                Mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt16);Mesh.subMeshCount=1;
            }
            for(int i=0;i<indices.Length;i++)indices[i]=checked((ushort)data.Indices[i]);
            data.CoarseNormals.CopyTo(coarseNormals);data.Normals.CopyTo(fineNormals);data.Positions.CopyTo(positions);
            if(data.RoadUv!=null)data.RoadUv.CopyTo(uv);else Array.Clear(uv,0,uv.Length);
            Mesh.SetVertexBufferData(positions,0,0,count*3,0,Flags);
            Mesh.SetVertexBufferData(fine?fineNormals:coarseNormals,0,0,count*3,1,Flags);
            Mesh.SetVertexBufferData(uv,0,0,count*2,2,Flags);
            for(int i=0;i<count;i++)channels[i]=new SurfaceChannels{roadProximity=data.RoadProximity?[i]??0,curvature=data.Curvature[i],overlap=data.Overlap?[i]??0};
            Mesh.SetVertexBufferData(channels,0,0,count,3,Flags);
            Mesh.SetIndexBufferData(indices,0,0,indices.Length,Flags);
            vertices=count;coarseCount=data.CoarseCount;fineStart=data.FineStart;fineCount=data.FineCount;
            Origin=new Double3(data.OriginX,0,data.OriginZ);
            var min=new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);var max=-min;
            for(int i=0;i<count;i++){var p=new Vector3(data.Positions[i*3],data.Positions[i*3+1],data.Positions[i*3+2]);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
            var pad=new Vector3(Mathf.Max(1,Mathf.Max(Mathf.Abs(min.x),Mathf.Abs(max.x))),Mathf.Max(1,Mathf.Max(Mathf.Abs(min.y),Mathf.Abs(max.y))),Mathf.Max(1,Mathf.Max(Mathf.Abs(min.z),Mathf.Abs(max.z))))*1e-6f;
            var bounds=new Bounds();bounds.SetMinMax(min-pad,max+pad);Mesh.bounds=bounds;
            SetRenderOrigin(target,renderOrigin);ApplyRange();
            var receipt=data.CommitUpload(owner);Residency?.Retire();Residency=receipt;UploadCount++;LastUploadedBytes=count*44+indices.Length*2;return true;
        }
        public void SetRenderOrigin(Transform target,Double3 renderOrigin)
        {Check();if(target!=null)target.position=new Vector3((float)(Origin.X-renderOrigin.X),(float)-renderOrigin.Y,(float)(Origin.Z-renderOrigin.Z));}
        public void SetFine(bool value)
        {
            Check();if(fine==value)return;fine=value;
            if(vertices>0){Mesh.SetVertexBufferData(fine?fineNormals:coarseNormals,0,0,vertices*3,1,Flags);ApplyRange();}
        }
        void ApplyRange()
        {if(vertices>0)Mesh.SetSubMesh(0,new SubMeshDescriptor(fine?fineStart:0,enabled?(fine?fineCount:coarseCount):0){firstVertex=0,vertexCount=vertices,bounds=Mesh.bounds},Flags);}
        public void Dispose()
        {
            if(Mesh!=null){if(Application.isPlaying)UnityEngine.Object.Destroy(Mesh);else UnityEngine.Object.DestroyImmediate(Mesh);Mesh=null;}
            Residency?.Retire();Residency=null;indices=null;channels=null;positions=uv=coarseNormals=fineNormals=null;vertices=0;
        }
    }
}
