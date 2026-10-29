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
        int generation;
        bool hasSample;
        IRouteSource route;
        HillsSceneSettings settings;
        public void Initialize(IRouteSource source,HillsSceneSettings config)
        {
            route=source;settings=config;DistanceMetres=requestedDistance=0;
            generation=route.Generation;Move(0);
        }
        void Update()
        {
            if(generation!=route.Generation){generation=route.Generation;DistanceMetres=requestedDistance=0;hasSample=false;}
            if(!hasSample){ApplySample();if(!hasSample)return;}
            if(settings.movementEnabled&&!paused)requestedDistance+=System.Math.Max(0,settings.speed)*Time.deltaTime;
            if(requestedDistance>DistanceMetres)ApplySample();
        }
        public void Move(float metres)
        {
            if(route==null)return;
            requestedDistance+=Mathf.Max(0,metres);ApplySample();
        }
        void ApplySample()
        {
            // Bounded manual progress: never synchronously generate a large request.
            double target=System.Math.Min(requestedDistance,DistanceMetres+10);
            QueryStatus=route.TrySample(target,out var position,out var forward);
            if(QueryStatus!=MidlineDistanceResult.Ready){if(!hasSample)GetComponent<Renderer>().enabled=false;return;}
            DistanceMetres=target;hasSample=true;GetComponent<Renderer>().enabled=true;
            transform.SetPositionAndRotation(position+Vector3.up*.65f,Quaternion.LookRotation(forward,Vector3.up));
        }
    }
}
