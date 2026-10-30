using UnityEngine;
using SlowRoads.Scene.Road;
using SlowRoads.Generation.Midline;

namespace SlowRoads.Debugging
{
    // Observer commits distance only after the streaming source returns Ready.
    public sealed class RouteObserver : MonoBehaviour
    {
        public bool paused;
        public double DistanceMetres {get;private set;}
        public MidlineDistanceResult QueryStatus {get;private set;}
        double requestedDistance;
        double manualTarget;
        bool manualPending;
        int generation;
        bool hasSample;
        IRouteSource route;
        HillsSceneSettings settings;
        public void Initialize(IRouteSource source,HillsSceneSettings config)
        {
            route=source;settings=config;DistanceMetres=requestedDistance=manualTarget=0;manualPending=false;
            generation=route.Generation;Move(0);
        }
        void Update()
        {
            if(generation!=route.Generation){generation=route.Generation;DistanceMetres=requestedDistance=manualTarget=0;manualPending=false;hasSample=false;}
            if(!hasSample){ApplySample();if(!hasSample)return;}
            if(paused||!settings.movementEnabled)
            {
                // Backpressure can accumulate automatic demand. Pause freezes the
                // committed position immediately; only an explicit step may retry.
                if(manualPending)ApplySample();else requestedDistance=DistanceMetres;
                return;
            }
            requestedDistance+=System.Math.Max(0,settings.speed)*Time.deltaTime;
            if(requestedDistance>DistanceMetres)ApplySample();
        }
        public void Move(float metres)
        {
            if(route==null)return;
            if(!manualPending)requestedDistance=DistanceMetres;
            requestedDistance+=Mathf.Max(0,metres);manualTarget=requestedDistance;manualPending=true;ApplySample();
        }
        void ApplySample()
        {
            // Bounded manual progress: never synchronously generate a large request.
            double target=System.Math.Min(requestedDistance,DistanceMetres+10);
            QueryStatus=route.TrySample(target,out var position,out var forward);
            if(QueryStatus!=MidlineDistanceResult.Ready){if(!hasSample)GetComponent<Renderer>().enabled=false;return;}
            DistanceMetres=target;hasSample=true;GetComponent<Renderer>().enabled=true;
            if(manualPending&&DistanceMetres>=manualTarget)manualPending=false;
            transform.SetPositionAndRotation(position+Vector3.up*.65f,Quaternion.LookRotation(forward,Vector3.up));
        }
    }
}
