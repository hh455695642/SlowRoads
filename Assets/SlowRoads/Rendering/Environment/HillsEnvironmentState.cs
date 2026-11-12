using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlowRoads.Rendering.Environment
{
    // Per-camera presentation state. Rendering never references the RoadPresentation assembly.
    public sealed class HillsEnvironmentState : IDisposable
    {
        static readonly Dictionary<Camera,HillsEnvironmentState> Active=new();
        public Camera Camera {get;}
        public HillsEnvironmentProfile Profile {get;private set;}
        public int Revision {get;private set;}
        public int View {get;private set;}=-1;
        public int Detail {get;private set;}=-1;
        public bool SkyVisible {get;private set;}
        public bool CloudsVisible {get;private set;}
        public bool AtmosphereVisible {get;private set;}
        public bool SnowVisible {get;private set;}
        public bool LightingEnabled {get;private set;}
        public double ObserverShadow {get;private set;}
        public float HazeHeight {get;private set;}
        public double CloudTime {get;private set;}
        public double SnowTime {get;private set;}
        public HillsSnowResources Snow {get;private set;}
        public double OriginX {get;private set;}
        public double OriginZ {get;private set;}
        public float FogNear {get;private set;}
        public float FogFar {get;private set;}
        public float FogDensity {get;private set;}
        public float HazeIntensity {get;private set;}
        public bool ExponentialFog=>Detail>2;
        public float CloudAltitude {get;private set;}
        public float CloudScale {get;private set;}
        public HillsSkyResources Sky {get;private set;}
        public HillsCloudResources Clouds {get;private set;}
        public int CloudSegments=>(int)Mathf.Max(20,CloudScale/400); // Original PlaneGeometry floors the requested segment count.
        bool disposed;
        public HillsEnvironmentState(Camera camera)
        {
            if(camera==null)throw new ArgumentNullException(nameof(camera));
            if(Active.ContainsKey(camera))throw new InvalidOperationException("Camera already has an environment owner");
            Camera=camera;Active.Add(camera,this);
        }
        public static bool TryGet(Camera camera,out HillsEnvironmentState state)=>Active.TryGetValue(camera,out state);
        public void Apply(HillsEnvironmentProfile profile,int view,int detail,bool sky,bool clouds,bool atmosphere,bool snow,bool lighting=true)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));
            if(profile==null)throw new ArgumentNullException(nameof(profile));
            if((uint)view>=6)throw new ArgumentOutOfRangeException(nameof(view));
            if((uint)detail>=5)throw new ArgumentOutOfRangeException(nameof(detail));
            bool showClouds=clouds&&profile.Clouds.HasClouds&&detail>0,showSnow=snow&&profile.Snow.PointCount>0;
            if(ReferenceEquals(Profile,profile)&&View==view&&Detail==detail&&SkyVisible==sky&&CloudsVisible==showClouds&&AtmosphereVisible==atmosphere&&SnowVisible==showSnow&&LightingEnabled==lighting)return;
            Profile=profile;View=view;Detail=detail;SkyVisible=sky;CloudsVisible=showClouds;AtmosphereVisible=atmosphere;SnowVisible=showSnow;LightingEnabled=lighting;HazeHeight=(float)profile.Fog.Height;
            float range=view switch{0=>360,1=>960,2=>1680,3=>3000,4=>4000,_=>5000};
            // Original CPU expressions use JS double; convert once at the GPU boundary.
            double near=range*profile.Fog.Near,far=range*profile.Fog.Far,haze=profile.Fog.Intensity;
            FogNear=(float)near;FogFar=(float)far;FogDensity=(float)Math.Sqrt(5/(far*far));
            if(haze<=0&&range<2000)haze=Math.Max(-1,(1+haze)*(range/2000d)-1);
            HazeIntensity=(float)haze;
            CloudAltitude=view switch{0=>400,1=>500,2=>600,3=>600,4=>700,_=>800};
            CloudScale=view switch{0=>1200,1=>3600,2=>7000,3=>11000,4=>15000,_=>22000};
            Clouds?.Configure(this);if(!SnowVisible)Snow?.Prepare(0);
            Revision++;
        }
        // Ni.updateAlways / updateHDR inputs; presentation reads existing observer/road data only.
        public void SetSurfaceInput(double observerShadow)
        {
            if(!double.IsFinite(observerShadow))throw new ArgumentOutOfRangeException(nameof(observerShadow));
            ObserverShadow=observerShadow;
            double height=Profile.Fog.Height,cam=Camera.transform.position.y;
            if(height>0&&cam<height+10){double n=Math.Clamp(1-(cam-10)/height,0,1);height*=1+2*n*n;}
            HazeHeight=(float)height;
        }
        public void Tick(double seconds,double originX,double originZ)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));
            OriginX=originX;OriginZ=originZ;
            // Original Ni advances this clock only while its cloud is visible.
            if(CloudsVisible)CloudTime+=seconds*2;
        }
        public void SetSnowTime(double time){if(!double.IsFinite(time)||time<0)throw new ArgumentOutOfRangeException(nameof(time));SnowTime=time;}
        public void SetSnowResources(Material material,ComputeShader compute)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));
            if(Snow?.Material==material&&Snow?.Compute==compute)return;
            Snow?.Dispose();Snow=material!=null?new HillsSnowResources(material,compute):null;
        }
        public void SetSkyMaterial(Material material)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));
            if(Sky?.Material==material)return;
            Sky?.Dispose();Sky=material!=null?new HillsSkyResources(material):null;
        }
        public void SetCloudMaterial(Material material)
        {
            if(disposed)throw new ObjectDisposedException(nameof(HillsEnvironmentState));
            if(Clouds?.Material==material)return;
            Clouds?.Dispose();Clouds=material!=null?new HillsCloudResources(material):null;
            if(Profile!=null)Clouds?.Configure(this);
        }
        public void Dispose(){if(disposed)return;disposed=true;Snow?.Dispose();Snow=null;Clouds?.Dispose();Clouds=null;Sky?.Dispose();Sky=null;Active.Remove(Camera);if(Active.Count==0)HillsEnvironmentUniforms.Clear();}
    }
}
