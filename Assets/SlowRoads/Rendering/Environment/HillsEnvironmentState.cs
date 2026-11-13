using System;
using System.Collections.Generic;
using UnityEngine;
using SlowRoads.Environment;
namespace SlowRoads.Rendering.Environment
{
    // Hills-only theme adapter. Weather is authored separately; this adapter owns only Hills lighting and host camera inputs.
    public sealed class HillsEnvironmentState : IDisposable
    {
        static readonly Dictionary<Camera,HillsEnvironmentState> Active=new();
        public Camera Camera {get;}
        public HillsEnvironmentProfile Profile {get;private set;}
        public int Revision {get;private set;}
        public double ObserverShadow {get;private set;}
        public double CloudTime {get;private set;}
        public double SnowTime {get;private set;}
        public double OriginX {get;private set;}
        public double OriginZ {get;private set;}
        public EnvironmentCameraState Background {get;}
        public EnvironmentSkyResources Sky=>Background.Sky;
        public EnvironmentCloudResources Clouds=>Background.Clouds;
        public EnvironmentSnowResources Snow=>Background.Snow;
        public HillsThemeLighting Lighting {get;private set;}
        bool disposed,lightInput;
        public bool SkyVisible=>Background.Frame.Sky.Enabled;
        public bool CloudsVisible=>Background.Frame.CloudsEnabled;
        public bool AtmosphereVisible=>Background.Frame.Fog.Enabled;
        public bool LightingEnabled=>Lighting.Enabled;
        public bool SnowVisible=>Background.Frame.SnowCount>0;
        public float FogNear=>Background.Frame.Fog.Range.x;
        public float FogFar=>Background.Frame.Fog.Range.y;
        public float FogDensity=>Background.Frame.Fog.Range.z;
        public float HazeHeight=>Background.Frame.Fog.Haze.x;
        public float HazeIntensity=>Background.Frame.Fog.Haze.y;
        public bool ExponentialFog=>Background.Frame.Fog.Range.w>.5f;
        public float CloudAltitude=>Background.Frame.CloudGeometry.x;
        public float CloudScale=>Background.Frame.CloudGeometry.y;
        public int CloudSegments=>Background.Frame.CloudSegments;
        public HillsEnvironmentState(Camera camera)
        {
            if(camera==null)throw new ArgumentNullException(nameof(camera));
            if(Active.ContainsKey(camera))throw new InvalidOperationException("Camera already has an environment owner");
            Camera=camera;Background=new EnvironmentCameraState(camera);
            Active.Add(camera,this);
        }
        public static bool TryGet(Camera camera,out HillsEnvironmentState state)=>Active.TryGetValue(camera,out state);
        public void ApplyTheme(HillsEnvironmentProfile profile,bool lighting=true)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            if(ReferenceEquals(Profile,profile)&&lightInput==lighting)return;
            Profile=profile;lightInput=lighting;
            MapLighting();RefreshBase();Revision++;
        }
        void MapLighting()
        {
            if(Profile==null)return;
            Lighting=HillsThemeLighting.Evaluate(Profile,ObserverShadow,lightInput);
        }
        void RefreshBase()
        {
            if(Profile==null)return;
            var input=new EnvironmentFrameInput(Camera.transform.position.y,CloudTime,SnowTime,OriginX,OriginZ);Background.Input=input;
            // Weather alone resolves the native stack; theme lighting is a material value snapshot.
            // Never manufacture a weather frame or overwrite an already resolved camera snapshot.
        }
        public void SetSurfaceInput(double observerShadow)
        {
            if(!double.IsFinite(observerShadow))throw new ArgumentOutOfRangeException(nameof(observerShadow));
            ObserverShadow=observerShadow;MapLighting();RefreshBase();
        }
        public void Tick(double seconds,double originX,double originZ)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            OriginX=originX;OriginZ=originZ;if(CloudsVisible)CloudTime+=seconds*Background.Frame.CloudGeometry.z;RefreshBase();
        }
        public void SetSnowTime(double time){if(!double.IsFinite(time)||time<0)throw new ArgumentOutOfRangeException(nameof(time));SnowTime=time;RefreshBase();}
        public void SetSkyMaterial(Material material){if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));Background.SetSkyMaterial(material);}
        public void SetCloudMaterial(Material material){if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));Background.SetCloudMaterial(material);}
        public void SetSnowResources(Material material,ComputeShader compute){if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));Background.SetSnowResources(material,compute);}
        public void Dispose()
        {
            if(disposed)return;disposed=true;Background.Dispose();Active.Remove(Camera);if(Active.Count==0)EnvironmentSurfaceUniforms.Clear();
        }
    }
}
