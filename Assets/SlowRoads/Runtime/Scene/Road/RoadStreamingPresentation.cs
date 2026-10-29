using System;
using System.Diagnostics;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Road;
using SlowRoads.Generation.Streaming;
using UnityEngine;

namespace SlowRoads.Scene.Road
{
    /// <summary>Owns exactly three Mesh uploaders. No rendering pipeline pass/RT or runtime material instances.</summary>
    public sealed class RoadStreamingPresentation : MonoBehaviour
    {
        public RoadStreamingSession Core {get;private set;}
        public Double3 RenderOrigin {get;private set;}
        public string Fault {get;private set;}
        public int OriginShiftCount {get;private set;}
        public event Action<Vector3> OriginShifted;
        HillsSceneSettings settings;
        readonly RoadMeshUploader[] uploads=new RoadMeshUploader[3];
        readonly Transform[] targets=new Transform[3];
        readonly MeshRenderer[] renderers=new MeshRenderer[3];
        readonly bool[] publishedVisible=new bool[3];
        readonly Stopwatch watch=new();
        Material lastMaterial;
        bool hasOrigin,disposed;
        public void Initialize(HillsSceneSettings configuration)
        {
            settings=configuration??throw new ArgumentNullException(nameof(configuration));
            Core=new RoadStreamingSession(settings.seed,new RoadStreamingConfig(settings.detail,settings.retainedBehindNodes,settings.LaneStyle));
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Streamed road slot "+i,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);targets[i]=go.transform;
                uploads[i]=new RoadMeshUploader("Hills streamed road "+i);go.GetComponent<MeshFilter>().sharedMesh=uploads[i].Mesh;
                renderers[i]=go.GetComponent<MeshRenderer>();renderers[i].enabled=false;renderers[i].shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderers[i].receiveShadows=false;
            }
            Core.Published+=Publish;Core.LodChanged+=(s,l)=>{if(uploads[s.Id].LiveVertexCount>0)uploads[s.Id].SetLod(l);};
            Core.Invalidated+=()=>{foreach(var r in renderers)r.enabled=false;hasOrigin=false;};
            BindMaterial();
        }
        void BindMaterial()
        {
            // Keep materials tied to the active generation until the selected type is rebuilt.
            var material=settings.ResolveRoadMaterial(Core.Config.Mesh.LaneStyle);
            if(material==lastMaterial)return;foreach(var r in renderers)r.sharedMaterial=material;lastMaterial=material;
        }
        void Publish(StreamingRoadSlot slot)
        {
            var b=slot.Builder.Buffers;
            if(!hasOrigin){RenderOrigin=new Double3(b.Origin.X,0,b.Origin.Z);hasOrigin=true;}
            uploads[slot.Id].Upload(b,Core.Midline.State.Generation,Core.Midline.Revision,targets[slot.Id],RenderOrigin);
            publishedVisible[slot.Id]=b.Visible;
            renderers[slot.Id].enabled=settings.visible&&publishedVisible[slot.Id];
        }
        void Update()
        {
            if(Core==null||disposed||Fault!=null)return;
            try
            {
                BindMaterial();for(int i=0;i<3;i++){uploads[i].Visible=settings.visible;renderers[i].enabled=settings.visible&&Core.Slots[i].Published&&publishedVisible[i];}
                if(!settings.generationEnabled)return;
                // Operation cap determines work; wall time only yields between operations.
                watch.Restart();int remaining=Math.Max(1,settings.operationsPerFrame);
                while(remaining>0)
                {
                    int chunk=Math.Min(16,remaining);remaining-=Core.Advance(chunk);
                    if(settings.cpuBudgetMilliseconds>0&&watch.Elapsed.TotalMilliseconds>=settings.cpuBudgetMilliseconds)break;
                }
                watch.Stop();
            }
            catch(Exception error){Fault=error.Message;UnityEngine.Debug.LogException(error,this);}
        }
        public MidlineDistanceResult TrySample(double distance,out Vector3 position,out Vector3 forward)
        {
            position=default;forward=Vector3.forward;
            if(Fault!=null)return MidlineDistanceResult.Invalidated;
            if(Core==null||!settings.generationEnabled)return MidlineDistanceResult.NotReady;
            var result=Core.TrySample(distance,out var sample);if(result!=MidlineDistanceResult.Ready)return result;
            ShiftOriginIfNeeded(sample.Position);
            var p=sample.Position;position=new Vector3((float)(p.X-RenderOrigin.X),(float)(p.Y+.02),(float)(p.Z-RenderOrigin.Z));
            forward=new Vector3((float)sample.Forward.X,(float)sample.Forward.Y,(float)sample.Forward.Z);
            Core.SetObserverIndex(sample.FromCoarseIndex);return result;
        }
        public void ShiftOriginIfNeeded(Double3 position)
        {
            double x=position.X-RenderOrigin.X,z=position.Z-RenderOrigin.Z;
            if(x*x+z*z<settings.originShiftDistance*settings.originShiftDistance)return;
            var next=new Double3(position.X,0,position.Z);var delta=new Vector3((float)(RenderOrigin.X-next.X),0,(float)(RenderOrigin.Z-next.Z));
            RenderOrigin=next;OriginShiftCount++;
            for(int i=0;i<3;i++)if(Core.Slots[i].Published)
            {var o=uploads[i].Origin;targets[i].localPosition=new Vector3((float)(o.X-next.X),(float)o.Y,(float)(o.Z-next.Z));}
            OriginShifted?.Invoke(delta);
        }
        public void Release()
        {
            if(disposed)return;disposed=true;Core?.Dispose();Core=null;
            foreach(var upload in uploads)upload?.Dispose();
        }
        void OnDestroy()=>Release();
    }
}
