// Runtime observer contract regression; run through unity script run in Play Mode.
using System;
using System.Reflection;
using UnityEngine;
using SlowRoads.Debugging;
using SlowRoads.Scene.Road;
using SlowRoads.Generation.Midline;
public static class T5ObserverContract
{
    sealed class Source : IRouteSource
    {
        public bool Ready=true; public int Generation {get;set;}=1;
        public MidlineDistanceResult TrySample(double d,out Vector3 p,out Vector3 f)
        {p=new Vector3((float)d,0,0);f=Vector3.right;return Ready?MidlineDistanceResult.Ready:MidlineDistanceResult.NotReady;}
    }
    public static object Run()
    {
        if(!Application.isPlaying)throw new Exception("Run in Play mode");
        var go=new GameObject("T5 observer contract",typeof(MeshRenderer));var config=ScriptableObject.CreateInstance<HillsSceneSettings>();
        try
        {
            var source=new Source();var observer=go.AddComponent<RouteObserver>();observer.Initialize(source,config);observer.paused=true;
            typeof(RouteObserver).GetField("requestedDistance",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(observer,100d);
            go.SendMessage("Update");if(observer.DistanceMetres!=0)throw new Exception("Pause drained automatic backlog");
            source.Ready=false;observer.Move(1);go.SendMessage("Update");if(observer.DistanceMetres!=0)throw new Exception("NotReady committed distance");
            source.Ready=true;go.SendMessage("Update");if(observer.DistanceMetres!=1)throw new Exception("Ready step must commit exactly one metre");
            go.SendMessage("Update");if(observer.DistanceMetres!=1)throw new Exception("Paused step repeated");
            observer.Move(1);if(observer.DistanceMetres!=2)throw new Exception("Second step incorrect");
            source.Generation++;go.SendMessage("Update");if(observer.DistanceMetres!=0)throw new Exception("Generation reset failed");
            return new {pause=true,notReady=true,stepExactlyOnce=true,secondStep=true,generationReset=true};
        }
        finally{UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(config);}
    }
}
