using UnityEngine;
using SlowRoads.Scene.Road;

namespace SlowRoads.Debugging
{
    public sealed class HillsDebugSession : MonoBehaviour
    {
        public HillsSceneSettings settings;
        public RouteObserver Observer {get;private set;}
        public RoadStreamingPresentation Streaming {get;private set;}
        GameObject runtimeRoot;
        void Start()=>Rebuild();
        [ContextMenu("重新生成道路")]
        public void Rebuild()
        {
            if(!Application.isPlaying||settings==null)return;
            Streaming?.Release();Streaming=null;
            if(runtimeRoot!=null){runtimeRoot.SetActive(false);Destroy(runtimeRoot);}
            runtimeRoot=new GameObject("Hills Debug Runtime");runtimeRoot.transform.SetParent(transform,false);
            Streaming=runtimeRoot.AddComponent<RoadStreamingPresentation>();Streaming.Initialize(settings);
            var sunObject=new GameObject("Hills Sun",typeof(Light));sunObject.transform.SetParent(runtimeRoot.transform,false);
            sunObject.transform.rotation=Quaternion.Euler(50,-30,0);var sun=sunObject.GetComponent<Light>();
            sun.type=LightType.Directional;sun.intensity=1.2f;sun.shadows=LightShadows.None;
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.name="Route Observer Cube";
            cube.transform.SetParent(runtimeRoot.transform,false);cube.GetComponent<Collider>().enabled=false;Destroy(cube.GetComponent<Collider>());
            if(settings.cubeMaterial!=null)cube.GetComponent<Renderer>().sharedMaterial=settings.cubeMaterial;
            Observer=cube.AddComponent<RouteObserver>();Observer.Initialize(new StreamingRoadRouteSource(Streaming),settings);
            var cameraObject=new GameObject("Hills Camera",typeof(Camera));cameraObject.transform.SetParent(runtimeRoot.transform,false);
            cameraObject.tag="MainCamera";var camera=cameraObject.GetComponent<Camera>();
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.36f,.53f,.65f);camera.farClipPlane=1800;
            cameraObject.AddComponent<ObserverCamera>().Initialize(cube.transform,settings);
            Streaming.OriginShifted+=delta=>{cube.transform.position+=delta;cameraObject.transform.position+=delta;};
        }
        public void TogglePause(){if(Observer!=null)Observer.paused=!Observer.paused;}
        public void Step(){if(Observer==null)return;Observer.paused=true;Observer.Move(Mathf.Max(.01f,settings.stepMetres));}
        void OnDestroy(){Streaming?.Release();if(runtimeRoot!=null)Destroy(runtimeRoot);}
    }
}
