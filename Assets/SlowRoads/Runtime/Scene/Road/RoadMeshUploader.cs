using System;
using System.Runtime.InteropServices;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Road;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Scene.Road
{
    /// <summary>URP built-in opaque Mesh submission boundary; no custom render pass.
    /// Owns exactly one Mesh, no material instances. P5 owns segment GameObjects and scheduling.
    /// Call on Unity's main thread and check producer revision before uploading a completed buffer.</summary>
    public sealed class RoadMeshUploader : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        struct HillsChannels
        {
            // TEXCOORD1 = bridge, shadow, solid, solidLeft; TEXCOORD2 = solidRight, mergeLeft, mergeRight.
            public float bridge,shadow,solid,left,right,mergeLeft,mergeRight;
        }
        static readonly VertexAttributeDescriptor[] Layout={
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),
            new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3,1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,2),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4,3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,3,3)};
        const MeshUpdateFlags Flags=MeshUpdateFlags.DontRecalculateBounds;
        HillsChannels[] channels;
        float[] baseShadow;
        int[] contentOwner,contentOrdinal;
        bool contentBound;
        ushort[] indices;
        int vertexCount,coarseCount,fineStart,fineCount;
        bool sourceVisible;
        RoadLod lod;
        bool disposed,enabled=true,visible=true;
        public Mesh Mesh {get;private set;}
        public bool Enabled
        {
            get=>enabled;
            set {ThrowIfDisposed();enabled=value;ApplyVisibility();}
        }
        public bool Visible
        {
            get=>visible;
            set {ThrowIfDisposed();visible=value;ApplyVisibility();}
        }
        public bool DiagnosticsEnabled {get;set;}
        public int LastUploadedBytes {get;private set;}
        public int UploadCount {get;private set;}
        public long ContentUploadedBytes {get;private set;}
        public int LiveVertexCount=>vertexCount;
        /// <summary>C2 composition touches only the existing interleaved content stream.
        /// P4's validated position/normal/UV buffers and source shadow history remain immutable.</summary>
        public void UpdateContentShadows(Func<int,int,bool,double?> provider)
        {
            ThrowIfDisposed();if(vertexCount==0)return;if(!contentBound)throw new InvalidOperationException("Upload must bind its finite content path");
            for(int row=0;row<vertexCount/2;row++)
            {
                int at=row*2;
                channels[at].shadow=(float)(provider?.Invoke(contentOwner[row],contentOrdinal[row],true)??baseShadow[at]);
                channels[at+1].shadow=(float)(provider?.Invoke(contentOwner[row],contentOrdinal[row],false)??baseShadow[at+1]);
            }
            Mesh.SetVertexBufferData(channels,0,0,vertexCount,3,Flags);
            ContentUploadedBytes+=(long)vertexCount*28;
        }
        public Double3 Origin {get;private set;}
        public RoadSegmentRequest SubmittedRequest {get;private set;}
        public RoadMeshUploader(string name="Hills road segment")
        {
            Mesh=new Mesh{name=name};Mesh.MarkDynamic();
        }
        public bool Upload(RoadMeshBuffers source,int currentGeneration,long currentRevision,Transform target=null,Double3 renderOrigin=default,RoadPathSnapshot path=null)
        {
            ThrowIfDisposed();
            if(!enabled)return false;
            if(source==null||!source.Complete)throw new InvalidOperationException("Upload requires a completed, current segment");
            if(source.Request.Generation!=currentGeneration||source.Request.Revision!=currentRevision)
                throw new InvalidOperationException("Producer revision changed before upload; rebuild from saved before-state");
            Validate(source);
            int first=path?.FineOffset(source.Request.HeadCoarseIndex)??-1;
            if(path!=null&&(first<0||first+source.VertexCount/2>path.FineCount))throw new InvalidOperationException("Road content binding does not match the uploaded fine chain");
            if(channels==null||channels.Length!=source.VertexCapacity)
            {
                Mesh.Clear(false);
                channels=new HillsChannels[source.VertexCapacity];indices=new ushort[source.Indices.Length];
                baseShadow=new float[source.VertexCapacity];contentOwner=new int[source.VertexCapacity/2];contentOrdinal=new int[source.VertexCapacity/2];
                Mesh.SetVertexBufferParams(source.VertexCapacity,Layout);
                Mesh.SetIndexBufferParams(indices.Length,IndexFormat.UInt16);Mesh.subMeshCount=1;
            }
            for(int i=0;i<source.VertexCount;i++)channels[i]=new HillsChannels{
                bridge=source.Bridge[i],shadow=source.Shadow[i],solid=source.PaintSolid[i],left=source.PaintSolidLeft[i],
                right=source.PaintSolidRight[i],mergeLeft=source.PaintMergeLeft[i],mergeRight=source.PaintMergeRight[i]};
            // The P5 producer immediately reuses its builder on slot rotation. Keep only
            // the small value mapping and baseline needed by later C2 content patches.
            Array.Copy(source.Shadow,baseShadow,source.VertexCount);contentBound=path!=null;
            if(contentBound)for(int row=0;row<source.VertexCount/2;row++)
            {var n=path.GetFine(first+row);contentOwner[row]=n.Owner;contentOrdinal[row]=n.IsCoarse?0:first+row-path.FineOffset(n.Owner);}
            int liveIndexEnd=source.FineStart+source.FineCount;
            for(int i=0;i<liveIndexEnd;i++)indices[i]=checked((ushort)source.Indices[i]);
            // Performance: retain GPU/CPU capacity; upload only live data, never recalculate normals/tangents.
            Mesh.SetVertexBufferData(source.Position,0,0,source.VertexCount*3,0,Flags);
            Mesh.SetVertexBufferData(source.Normal,0,0,source.VertexCount*3,1,Flags);
            Mesh.SetVertexBufferData(source.Uv,0,0,source.VertexCount*2,2,Flags);
            Mesh.SetVertexBufferData(channels,0,0,source.VertexCount,3,Flags);
            Mesh.SetIndexBufferData(indices,0,0,liveIndexEnd,Flags);
            // Freeze publication metadata: the producer may reuse its staging buffers after this call.
            vertexCount=source.VertexCount;coarseCount=source.CoarseCount;fineStart=source.FineStart;fineCount=source.FineCount;
            sourceVisible=source.Visible;lod=source.Lod;SubmittedRequest=source.Request;Origin=source.Origin;
            var min=ToVector(source.BoundsMin);var max=ToVector(source.BoundsMax);
            // Rounding center/extents can otherwise shrink the exact Float32 min/max.
            var pad=new Vector3(Mathf.Max(1,Mathf.Max(Mathf.Abs(min.x),Mathf.Abs(max.x))),
                Mathf.Max(1,Mathf.Max(Mathf.Abs(min.y),Mathf.Abs(max.y))),Mathf.Max(1,Mathf.Max(Mathf.Abs(min.z),Mathf.Abs(max.z))))*1e-6f;
            var bounds=new Bounds();bounds.SetMinMax(min-pad,max+pad);Mesh.bounds=bounds;
            if(target!=null)target.position=ToVector(new Double3(Origin.X-renderOrigin.X,Origin.Y-renderOrigin.Y,Origin.Z-renderOrigin.Z));
            ApplyVisibility();UploadCount++;
            LastUploadedBytes=DiagnosticsEnabled?source.VertexCount*60+liveIndexEnd*2:0;
            return true;
        }
        public void SetLod(RoadLod lod)
        {
            ThrowIfDisposed();if(vertexCount==0)throw new InvalidOperationException("No submitted segment");
            if(lod!=RoadLod.Coarse&&lod!=RoadLod.Fine)throw new ArgumentOutOfRangeException(nameof(lod));
            this.lod=lod;ApplyVisibility(); // changes one index range; uploads zero vertex/index bytes.
        }
        void ApplyVisibility()
        {
            if(Mesh==null||vertexCount==0)return;
            var range=lod==RoadLod.Fine?new RoadDrawRange(fineStart,fineCount):new RoadDrawRange(0,coarseCount);
            Mesh.SetSubMesh(0,new SubMeshDescriptor(range.Start,enabled&&visible&&sourceVisible?range.Count:0,MeshTopology.Triangles){
                firstVertex=0,vertexCount=vertexCount,bounds=Mesh.bounds},Flags);
        }
        static Vector3 ToVector(Double3 v)=>new Vector3((float)v.X,(float)v.Y,(float)v.Z);
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        static void Validate(RoadMeshBuffers b)
        {
            if(b.VertexCount<4||b.VertexCount>b.VertexCapacity||b.CoarseCount!=b.Config.Intervals*6||b.FineCount<=0)
                throw new ArgumentException("Invalid live counts");
            for(int i=0;i<b.VertexCount*3;i++)if(!Finite(b.Position[i])||!Finite(b.Normal[i]))throw new ArgumentException("Non-finite geometry");
            for(int i=0;i<b.VertexCount*2;i++)if(!Finite(b.Uv[i]))throw new ArgumentException("Non-finite UV");
            for(int i=0;i<b.VertexCount;i++)if(!Finite(b.Bridge[i])||!Finite(b.Shadow[i])||!Finite(b.PaintSolid[i])||!Finite(b.PaintSolidLeft[i])||
                !Finite(b.PaintSolidRight[i])||!Finite(b.PaintMergeLeft[i])||!Finite(b.PaintMergeRight[i]))throw new ArgumentException("Non-finite Nl channel");
            for(int i=0;i<b.FineStart+b.FineCount;i++)if(b.Indices[i]<0||b.Indices[i]>=b.VertexCount)throw new ArgumentException("Index outside live vertex range");
        }
        void ThrowIfDisposed(){if(disposed)throw new ObjectDisposedException(nameof(RoadMeshUploader));}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            if(Mesh!=null)
            {
                if(Application.isPlaying)UnityEngine.Object.Destroy(Mesh);else UnityEngine.Object.DestroyImmediate(Mesh);
                Mesh=null;
            }
            channels=null;indices=null;baseShadow=null;contentOwner=contentOrdinal=null;contentBound=false;vertexCount=0;
        }
    }
}
