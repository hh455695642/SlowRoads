using System;
using System.Collections.Generic;
using SlowRoads.Generation.Contents.Bridges;
using SlowRoads.Generation.Contents.Fields;
using SlowRoads.Generation.Contents.Walls;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Terrain;
using SlowRoads.Scene.Road;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

namespace SlowRoads.Scene.Contents
{
    /// <summary>One main-scene owner for C2/C3 Mesh resources. Ordinary URP opaque/depth submission;
    /// no extra RendererFeature, RT, blit or second vegetation renderer. Generation remains CPU pure data.</summary>
    public sealed class StructureStreamingPresentation : MonoBehaviour
    {
        // CPU attribution only. Profiler scopes allocate no per-frame managed objects;
        // generation remains Unity-free and rendering still uses the ordinary URP queues.
        static readonly ProfilerMarker WallWork=new("SlowRoads.C3.Walls.Advance"),BridgeWork=new("SlowRoads.C3.Bridges.Advance"),
            MeshUpload=new("SlowRoads.C3.Mesh.Upload"),FieldGeometry=new("SlowRoads.C2.FieldWall.Geometry"),Presentation=new("SlowRoads.C3.Presentation");
        enum Kind {Field,Bridge,Shadow,Wall}
        sealed class Slot : IDisposable
        {
            internal GameObject Object;internal Mesh Mesh;internal MeshRenderer Renderer;internal Double3 Origin;internal Kind Kind;
            internal TerrainRegionState Region;internal StreamedBridge Bridge;internal StreamedWall Wall;
            internal int Capacity,IndexCapacity,FarCount,NearStart,NearCount;internal bool IsNear;internal IndexFormat Format;
            internal float[] Up,EmptyUV,EmptyShadow;internal ushort[] ShortIndices;
            internal Material Material;
            public void Dispose(){if(Mesh!=null)UnityEngine.Object.Destroy(Mesh);if(Object!=null)UnityEngine.Object.Destroy(Object);Mesh=null;Object=null;}
        }
        static readonly VertexAttributeDescriptor[] Layout={new(VertexAttribute.Position,VertexAttributeFormat.Float32,3,0),new(VertexAttribute.Normal,VertexAttributeFormat.Float32,3,1),
            new(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,2,2),new(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,1,3)};
        readonly Dictionary<object,Slot> active=new();readonly Stack<Slot> pool=new();
        // Native Unity resources must be created on the main thread, not during deserialization.
        RoadStreamingPresentation road;HillsSceneSettings settings;bool disposed;
        public BridgeStreamingSession Bridges {get;private set;}public WallStreamingSession Walls {get;private set;}
        public bool Building=>Walls?.Building??false;public int NativeMeshCount=>active.Count+pool.Count;
        public long UploadedBytes {get;private set;}public int UploadCount {get;private set;}
        public long GpuCapacityBytes {get{long bytes=0;foreach(var s in active.Values)bytes+=(long)s.Capacity*36+(long)s.IndexCapacity*(s.Format==IndexFormat.UInt16?2:4);foreach(var s in pool)bytes+=(long)s.Capacity*36+(long)s.IndexCapacity*(s.Format==IndexFormat.UInt16?2:4);return bytes;}}
        public void Initialize(RoadStreamingPresentation road,HillsSceneSettings settings)
        {
            this.road=road;this.settings=settings;Bridges=new BridgeStreamingSession(road.Core);Walls=new WallStreamingSession(road.Core,road.Terrain.Trees.Attributes);
            Bridges.Committed+=BridgeCommitted;Bridges.Released+=BridgeReleased;Walls.Committed+=WallCommitted;Walls.Released+=WallReleased;
            road.Terrain.Fields.Committed+=FieldCommitted;road.Terrain.Fields.Released+=FieldReleased;road.OriginShifted+=OriginChanged;
        }
        public int Advance(int budget)
        {
            int used;using(WallWork.Auto())used=Walls.Advance(budget);
            if(used<budget&&!road.Core.TerrainBuildPending){using(BridgeWork.Auto())used+=Bridges.Advance(budget-used);}return used;
        }
        Slot Acquire(string name)
        {
            Slot slot;
            if(pool.Count>0)slot=pool.Pop();else
            {var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);var mesh=new Mesh{name="Hills structure"};mesh.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=mesh;
                slot=new Slot{Object=go,Mesh=mesh,Renderer=go.GetComponent<MeshRenderer>()};slot.Renderer.shadowCastingMode=ShadowCastingMode.Off;slot.Renderer.receiveShadows=false;}
            slot.Object.name=name;slot.Object.SetActive(true);slot.Renderer.enabled=false;return slot;
        }
        void Upload(object key,Kind kind,float[] positions,float[] uv,float[] shadow,float[] normals,int[] indices,Double3 origin,int farCount,int nearStart,int nearCount,
            TerrainRegionState region=null,StreamedBridge bridge=null,StreamedWall wall=null)
        {
            using var uploadSample=MeshUpload.Auto();
            if(positions.Length==0||indices.Length==0){Remove(key);return;}int count=positions.Length/3;var slot=Acquire("Hills "+kind+" "+key);
            try
            {
                foreach(float value in positions)if(!float.IsFinite(value))throw new InvalidOperationException("Non-finite structure vertex");
                var format=count<=65535?IndexFormat.UInt16:IndexFormat.UInt32;int capacity=Mathf.NextPowerOfTwo(count),indexCapacity=Mathf.NextPowerOfTwo(indices.Length);
                if(slot.Capacity<capacity||slot.IndexCapacity<indexCapacity||slot.Format!=format)
                {
                    slot.Capacity=Math.Max(capacity,slot.Capacity);slot.IndexCapacity=Math.Max(indexCapacity,slot.IndexCapacity);slot.Format=format;slot.Mesh.Clear(false);
                    slot.Mesh.SetVertexBufferParams(slot.Capacity,Layout);slot.Mesh.SetIndexBufferParams(slot.IndexCapacity,format);slot.Mesh.subMeshCount=1;
                    slot.Up=new float[slot.Capacity*3];for(int i=0;i<slot.Capacity;i++)slot.Up[i*3+1]=1;
                    slot.EmptyUV=new float[slot.Capacity*2];slot.EmptyShadow=new float[slot.Capacity];slot.ShortIndices=new ushort[slot.IndexCapacity];
                }
                var flags=MeshUpdateFlags.DontRecalculateBounds;
                slot.Mesh.SetVertexBufferData(positions,0,0,positions.Length,0,flags);slot.Mesh.SetVertexBufferData(normals??slot.Up,0,0,count*3,1,flags);
                slot.Mesh.SetVertexBufferData(uv!=null&&uv.Length>=count*2?uv:slot.EmptyUV,0,0,count*2,2,flags);
                slot.Mesh.SetVertexBufferData(shadow!=null&&shadow.Length>=count?shadow:slot.EmptyShadow,0,0,count,3,flags);
                if(format==IndexFormat.UInt16){for(int i=0;i<indices.Length;i++)slot.ShortIndices[i]=checked((ushort)indices[i]);slot.Mesh.SetIndexBufferData(slot.ShortIndices,0,0,indices.Length,flags);}
                else slot.Mesh.SetIndexBufferData(indices,0,0,indices.Length,flags);
                var min=new Vector3(positions[0],positions[1],positions[2]);var max=min;
                for(int i=3;i<positions.Length;i+=3){var p=new Vector3(positions[i],positions[i+1],positions[i+2]);min=Vector3.Min(min,p);max=Vector3.Max(max,p);}
                slot.Mesh.bounds=new Bounds((min+max)*.5f,max-min);slot.Kind=kind;slot.Region=region;slot.Bridge=bridge;slot.Wall=wall;slot.Origin=origin;
                slot.FarCount=farCount;slot.NearStart=nearStart;slot.NearCount=nearCount;slot.IsNear=false;slot.Mesh.SetSubMesh(0,new SubMeshDescriptor(0,farCount,MeshTopology.Triangles){bounds=slot.Mesh.bounds,vertexCount=count},flags);
                Position(slot);slot.Material=null;
                // Publish only after the replacement has a complete native Mesh; keep the old view on upload failure.
                Remove(key);active.Add(key,slot);UploadedBytes+=(long)count*36+(long)indices.Length*(format==IndexFormat.UInt16?2:4);UploadCount++;
            }
            catch{slot.Renderer.enabled=false;slot.Object.SetActive(false);pool.Push(slot);throw;}
        }
        void FieldCommitted(TerrainRegionState r,FieldBoundaryRecipe recipe)
        {
            FieldWallGeometryData data;using(FieldGeometry.Auto())data=new FieldWallGeometryBuilder().Build(recipe);
            Upload(r,Kind.Field,data.Positions,data.UV,data.Shadow,null,data.NearIndices,new Double3(recipe.WorldX,0,recipe.WorldZ),data.FarIndices.Length,0,data.NearIndices.Length,region:r);
        }
        void BridgeCommitted(StreamedBridge bridge)
        {
            var d=bridge.Geometry;var o=new Double3(d.Input.Head.Position.X,0,d.Input.Head.Position.Z);
            Upload((Kind.Bridge,bridge.Id),Kind.Bridge,d.Positions,d.UV,null,null,d.Indices,o,d.FarCount,d.NearStart,d.NearCount,bridge:bridge);
            Upload((Kind.Shadow,bridge.Id),Kind.Shadow,d.ShadowPositions,null,null,null,d.ShadowIndices,o,d.ShadowIndices.Length,0,d.ShadowIndices.Length,bridge:bridge);
        }
        void WallCommitted(StreamedWall wall)
        {var d=wall.Geometry;Upload(wall.Id,Kind.Wall,d.Positions,d.UV,d.Shadow,d.Normals,d.Indices,d.Origin,d.Indices.Length,0,d.Indices.Length,wall:wall);}
        void FieldReleased(TerrainRegionState r)=>Remove(r);
        void BridgeReleased(int id){Remove((Kind.Bridge,id));Remove((Kind.Shadow,id));}
        void WallReleased(WallId id)=>Remove(id);
        void Remove(object key)
        {
            if(!active.Remove(key,out var slot))return;slot.Renderer.enabled=false;slot.Object.SetActive(false);slot.Region=null;slot.Bridge=null;slot.Wall=null;
            if(pool.Count<64)pool.Push(slot);else slot.Dispose();
        }
        void Position(Slot s){var o=road.RenderOrigin;s.Object.transform.localPosition=new Vector3((float)(s.Origin.X-o.X),(float)(s.Origin.Y-o.Y),(float)(s.Origin.Z-o.Z));}
        void OriginChanged(Vector3 _){foreach(var slot in active.Values)Position(slot);}
        void LateUpdate()
        {
            using var presentationSample=Presentation.Auto();
            if(disposed||road==null)return;var materials=settings.ResolveStructureMaterials();
            foreach(var s in active.Values)
            {
                bool near=s.Kind==Kind.Field?s.Region.TargetLod==1:s.Kind==Kind.Bridge&&s.Bridge.Near;
                if(s.IsNear!=near){s.IsNear=near;var sub=s.Mesh.GetSubMesh(0);sub.indexStart=near?s.NearStart:0;sub.indexCount=near?s.NearCount:s.FarCount;s.Mesh.SetSubMesh(0,sub,MeshUpdateFlags.DontRecalculateBounds);}
                Material material=s.Kind switch {Kind.Field=>near?materials.drystone:materials.fieldFar,Kind.Bridge=>materials.bridge,Kind.Shadow=>settings.bridgeProjectedShadow,
                    _=>s.Wall.Segment.Type switch {HillsWallType.Bridge or HillsWallType.Lowland=>materials.solidWall,HillsWallType.Barrier=>materials.barrier,HillsWallType.Fence=>materials.fence,_=>materials.drystone}};
                if(material==null||material.shader==null)throw new InvalidOperationException("Missing formal structure material");
                material=road.ThemeMaterials.Resolve(material);
                if(s.Material!=material){s.Renderer.sharedMaterial=material;s.Material=material;}
                s.Renderer.enabled=s.Kind switch {Kind.Field=>settings.fieldWallsVisible&&s.Region.Visible&&!s.Region.Retired,
                    Kind.Bridge=>settings.bridgesVisible&&s.Bridge.Visible,Kind.Shadow=>settings.bridgesVisible&&s.Bridge.Visible&&s.Bridge.Near,_=>settings.wallsVisible&&s.Wall.Visible};
            }
        }
        public void Release()
        {
            if(disposed)return;disposed=true;if(road!=null){road.OriginShifted-=OriginChanged;road.Terrain.Fields.Committed-=FieldCommitted;road.Terrain.Fields.Released-=FieldReleased;}
            Bridges?.Dispose();Walls?.Dispose();foreach(var slot in active.Values)slot.Dispose();active.Clear();foreach(var slot in pool)slot.Dispose();pool.Clear();
        }
        void OnDestroy()=>Release();
    }
}
