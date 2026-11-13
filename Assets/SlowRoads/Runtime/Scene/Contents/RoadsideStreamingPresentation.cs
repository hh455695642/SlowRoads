using System;
using System.Collections.Generic;
using SlowRoads.Generation.Contents.Roadside;
using SlowRoads.Generation.Midline;
using SlowRoads.Scene.Road;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Scene.Contents
{
    /// <summary>C6 bounded E7 object pools + two x7 quad meshes, submitted through the
    /// existing URP opaque/transparent passes. No per-instance CPU animation or new RT.</summary>
    public sealed class RoadsideStreamingPresentation : MonoBehaviour
    {
        sealed class ObjectSlot {internal GameObject Object;internal MeshFilter Filter;internal Renderer[] Renderers;internal RoadsideObject Data;}
        sealed class EyeSlot {internal GameObject Object;internal Mesh Mesh;internal MeshRenderer Renderer;internal MaterialPropertyBlock Block=new();internal Double3 Origin;internal bool Visible;}
        static readonly Unity.Profiling.ProfilerMarker Generate=new("SlowRoads.C6.Roadside.Advance"),Upload=new("SlowRoads.C6.Mesh.Upload");
        readonly Dictionary<string,ObjectSlot> objects=new();readonly Stack<ObjectSlot> signs=new(),markers=new();readonly EyeSlot[] eyes=new EyeSlot[2];
        RoadStreamingPresentation road;HillsSceneSettings settings;bool disposed;
        public RoadsideStreamingSession Core {get;private set;}
        public int ObjectCount=>objects.Count;public int EyeMeshCount {get;private set;}public long UploadedBytes {get;private set;}public int UploadCount {get;private set;}
        public int PooledObjects=>signs.Count+markers.Count;
        public long EyeCapacityBytes {get;private set;}
        public void Initialize(RoadStreamingPresentation road,HillsSceneSettings settings)
        {
            this.road=road;this.settings=settings;
            if(settings.signMeshes?.Length!=10||settings.signMaterial==null||settings.vergeMarkerModel==null||settings.vergeMarkerMaterials?.Length!=3||settings.catsEyeMaterial==null)throw new InvalidOperationException("Roadside reference assets are not configured");
            foreach(var m in settings.signMeshes)if(m==null)throw new InvalidOperationException("Missing sign variant mesh");
            Core=new(road.Core,road.Terrain.Core,road.Structures.Walls);Core.Committed+=Commit;Core.Released+=Remove;
            Core.EyesUploaded+=UploadEyes;Core.EyesVisibilityChanged+=EyeVisibility;Core.Reset+=ClearEyes;road.OriginShifted+=OriginChanged;
        }
        public int Advance(int budget){using var sample=Generate.Auto();return Core.Advance(budget);}
        ObjectSlot Acquire(RoadsideKind kind)
        {
            var pool=kind==RoadsideKind.Sign?signs:markers;if(pool.Count>0)return pool.Pop();
            GameObject go;MeshFilter filter=null;
            if(kind==RoadsideKind.Sign){go=new GameObject("Hills road sign",typeof(MeshFilter),typeof(MeshRenderer));filter=go.GetComponent<MeshFilter>();go.GetComponent<MeshRenderer>().sharedMaterial=road.ThemeMaterials.Resolve(settings.signMaterial);}
            else
            {
                go=Instantiate(settings.vergeMarkerModel);
                foreach(var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {int material=r.name.Contains("black")?0:r.name.Contains("reflect")?2:1;var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++)mats[i]=road.ThemeMaterials.Resolve(settings.vergeMarkerMaterials[material]);r.sharedMaterials=mats;}
            }
            go.transform.SetParent(transform,false);var renderers=go.GetComponentsInChildren<Renderer>(true);
            foreach(var r in renderers){r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;}
            return new(){Object=go,Filter=filter,Renderers=renderers};
        }
        void Commit(RoadsideObject data)
        {
            if(!objects.TryGetValue(data.Id,out var slot)){slot=Acquire(data.Kind);objects.Add(data.Id,slot);}slot.Data=data;
            slot.Object.name=data.Kind==RoadsideKind.Sign?"Hills sign "+data.Sign+" "+data.Id:"Hills verge marker "+data.Id;
            if(slot.Filter!=null)slot.Filter.sharedMesh=settings.signMeshes[(int)data.Sign];
            Position(slot);slot.Object.transform.localRotation=Quaternion.AngleAxis((float)(data.Rotation*180/Math.PI),Vector3.up);slot.Object.SetActive(true);
        }
        void Position(ObjectSlot slot){var p=slot.Data.Position;var o=road.RenderOrigin;slot.Object.transform.localPosition=new((float)(p.X-o.X),(float)(p.Y-o.Y),(float)(p.Z-o.Z));}
        void Remove(string id)
        {
            if(!objects.Remove(id,out var slot))return;slot.Object.SetActive(false);var pool=slot.Data.Kind==RoadsideKind.Sign?signs:markers;slot.Data=null;
            if(pool.Count<32)pool.Push(slot);else Destroy(slot.Object);
        }
        void UploadEyes(CatsEyeWindow.Slot data)
        {
            using var sample=Upload.Auto();var slot=eyes[data.Id];int count=data.Positions.Length/3;
            if(slot==null)
            {
                var go=new GameObject("Hills cats eyes "+data.Id,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);
                var mesh=new Mesh{name="Hills x7 points as quads "+data.Id};mesh.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=mesh;
                slot=eyes[data.Id]=new(){Object=go,Mesh=mesh,Renderer=go.GetComponent<MeshRenderer>()};slot.Renderer.sharedMaterial=settings.catsEyeMaterial;
                slot.Renderer.shadowCastingMode=ShadowCastingMode.Off;slot.Renderer.receiveShadows=false;EyeMeshCount++;EyeCapacityBytes+=(long)count*(4*32+6*2);
            }
            var positions=new Vector3[count*4];var normals=new Vector3[count*4];var corners=new Vector2[count*4];var indices=new int[count*6];
            Vector2[] quad={new(-1,-1),new(-1,1),new(1,1),new(1,-1)};
            for(int i=0;i<count;i++)
            {
                var p=new Vector3(data.Positions[i*3],data.Positions[i*3+1],data.Positions[i*3+2]);var n=new Vector3(data.Normals[i*3],data.Normals[i*3+1],data.Normals[i*3+2]);
                for(int j=0;j<4;j++){positions[i*4+j]=p;normals[i*4+j]=n;corners[i*4+j]=quad[j];}
                int at=i*6,v=i*4;indices[at]=v;indices[at+1]=v+1;indices[at+2]=v+2;indices[at+3]=v;indices[at+4]=v+2;indices[at+5]=v+3;
            }
            slot.Mesh.Clear();slot.Mesh.vertices=positions;slot.Mesh.normals=normals;slot.Mesh.uv=corners;slot.Mesh.SetIndices(indices,MeshTopology.Triangles,0,true);
            var bounds=slot.Mesh.bounds;bounds.Expand(.2f);slot.Mesh.bounds=bounds;slot.Origin=data.Origin;slot.Visible=data.Visible;
            PositionEyes(slot);UploadedBytes+=(long)count*(4*32+6*2);UploadCount++;
        }
        void EyeVisibility(CatsEyeWindow.Slot s){if(eyes[s.Id]!=null)eyes[s.Id].Visible=s.Visible;}
        void PositionEyes(EyeSlot s){var o=road.RenderOrigin;s.Object.transform.localPosition=new((float)(s.Origin.X-o.X),(float)(s.Origin.Y-o.Y),(float)(s.Origin.Z-o.Z));}
        void OriginChanged(Vector3 _){foreach(var s in objects.Values)Position(s);foreach(var s in eyes)if(s!=null)PositionEyes(s);}
        void LateUpdate()
        {
            if(disposed)return;
            foreach(var s in objects.Values)foreach(var r in s.Renderers)r.enabled=settings.roadsideVisible;
            var p=road.ObserverWorldPosition;var o=road.RenderOrigin;var direction=-road.ObserverForward;
            foreach(var s in eyes)if(s!=null)
            {
                s.Renderer.enabled=s.Visible&&Core.Config.EyesEnabled&&settings.catsEyesVisible&&settings.observerReflectorsEnabled;
                // Explicit observer adapter for X.forwardPosition/forwardDir. It is a
                // reflector input only, not a vehicle/headlight/physics subsystem.
                s.Block.SetVector("_ReflectorPosition",new((float)(p.X-o.X),(float)(p.Y+.65-o.Y),(float)(p.Z-o.Z),1));
                s.Block.SetVector("_ReflectorDirection",direction);s.Block.SetFloat("_ReflectorEnabled",settings.observerReflectorsEnabled?1:0);s.Renderer.SetPropertyBlock(s.Block);
            }
        }
        void ClearEyes(){foreach(var s in eyes)if(s!=null){s.Visible=false;s.Renderer.enabled=false;}}
        public void Release()
        {
            if(disposed)return;disposed=true;road.OriginShifted-=OriginChanged;Core?.Dispose();Core=null;
            foreach(var s in objects.Values)Destroy(s.Object);objects.Clear();foreach(var s in signs)Destroy(s.Object);signs.Clear();foreach(var s in markers)Destroy(s.Object);markers.Clear();
            for(int i=0;i<eyes.Length;i++){var s=eyes[i];if(s==null)continue;Destroy(s.Mesh);Destroy(s.Object);eyes[i]=null;}EyeMeshCount=0;EyeCapacityBytes=0;
        }
        void OnDestroy()=>Release();
    }
}
