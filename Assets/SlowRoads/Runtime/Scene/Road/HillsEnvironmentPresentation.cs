using System;
using UnityEngine;
using SlowRoads.Rendering.Environment;
using SlowRoads.Generation.Midline;
using SlowRoads.Generation.Spatial;

namespace SlowRoads.Scene.Road
{
    public sealed class HillsEnvironmentPresentation : MonoBehaviour
    {
        HillsSceneSettings settings;
        RoadStreamingPresentation streaming;
        SlowRoads.Environment.EnvironmentResources resources;
        float initialFar,ownedFar;Light sun;Color initialColour,ownedColour;float initialIntensity,ownedIntensity;Quaternion initialRotation,ownedRotation;bool ownsLight;
        public HillsEnvironmentState State {get;private set;}
        public void Initialize(HillsSceneSettings config,Camera camera,RoadStreamingPresentation source, SlowRoads.Environment.EnvironmentResources environmentResources=null)
        {
            Release();settings=config??throw new ArgumentNullException(nameof(config));streaming=source;resources=environmentResources;
            sun=source!=null?source.Sun:null;if(sun!=null){initialColour=sun.color;initialIntensity=sun.intensity;initialRotation=sun.transform.rotation;}
            initialFar=camera.farClipPlane;ownedFar=initialFar;State=new HillsEnvironmentState(camera);
            try{Apply();}catch{Release();throw;}
        }
        void Apply()
        {
            State.ApplyTheme(HillsEnvironmentProfile.Resolve((int)settings.season,settings.environmentTime,settings.environmentWeather),settings.environmentLightingEnabled);
            streaming?.ThemeMaterials?.Apply(State.Profile,State.ObserverShadow,State.LightingEnabled);
            State.SetSkyMaterial(resources!=null?resources.skyMaterial:null);
            State.SetCloudMaterial(resources!=null?resources.cloudMaterial:null);
            State.SetSnowResources(resources!=null?resources.snowMaterial:null,resources!=null?resources.snowCulling:null);
            ApplySun();
            // View quality owns clipping. Local/cloud Volume blends must not change vegetation culling.
            if(resources!=null&&(resources.skyMaterial!=null||resources.cloudMaterial!=null)){ownedFar=settings.detail switch{0=>1200,1=>3600,2=>7000,3=>11000,4=>15000,_=>22000};State.Camera.farClipPlane=ownedFar;}
        }
        void LateUpdate()
        {
            if(State==null)return;
            Apply();var origin=streaming!=null?streaming.RenderOrigin:default;
            State.Tick(Time.deltaTime,origin.X,origin.Z);State.SetSnowTime(Time.timeAsDouble);State.SetSurfaceInput(ReadObserverShadow());
            streaming?.ThemeMaterials?.Apply(State.Profile,State.ObserverShadow,State.LightingEnabled);
        }
        public void ApplyThemeNow()
        {
            if(State==null)return;
            Apply();streaming?.ThemeMaterials?.Reapply();
        }
        void ApplySun()
        {
            if(sun==null)return;
            if(!State.LightingEnabled){RestoreSun();return;}
            var p=State.Profile.Lights;int c=p.Sun;
            ownedColour=new Color(((c>>16)&255)/255f,((c>>8)&255)/255f,(c&255)/255f);ownedIntensity=p.SunIntensity;ownedRotation=Quaternion.Euler(90,0,0);
            if(sun.color!=ownedColour)sun.color=ownedColour;if(sun.intensity!=ownedIntensity)sun.intensity=ownedIntensity;if(sun.transform.rotation!=ownedRotation)sun.transform.rotation=ownedRotation;ownsLight=true;
        }
        void RestoreSun()
        {
            if(!ownsLight||sun==null)return;
            if(sun.color==ownedColour)sun.color=initialColour;if(sun.intensity==ownedIntensity)sun.intensity=initialIntensity;if(sun.transform.rotation==ownedRotation)sun.transform.rotation=initialRotation;ownsLight=false;
        }
        // Read-only source m2 + current C2 road-shadow overlay; never advance generation or RNG.
        double ReadObserverShadow()
        {
            if(streaming?.Core?.Midline==null||streaming.Trees?.Core==null)return 0;
            var at=streaming.ObserverWorldPosition;var start=streaming.Core.Midline.FindCoarse((int)Math.Floor(streaming.ObserverProgress));
            if(start==null||!RoadFineProjection.TryCreate(at.X,at.Z,start,out var p))return State.ObserverShadow;
            double shadow;
            if(p.Distance<settings.HalfWidth+10)
            {
                MidlinePathNode n=streaming.Core.Midline.FindCoarse(p.FineCoarseIndex);int ordinal=0;
                while(n!=null&&ordinal<p.FineKey.Ordinal){n=n.NextFine;ordinal++;}
                if(n?.NextFine==null)return State.ObserverShadow;
                double Read(MidlinePathNode node,bool left){var c=node as CoarseNode;var f=node as FineNode;return streaming.Core.ContentShadowProvider?.Invoke(c?.Index??f.PreviousCoarse.Index,c!=null?0:f.Ordinal,left)??(left?node.ShadowLeft:node.ShadowRight);}
                double sl=Read(n,true)*(1-p.FineT)+Read(n.NextFine,true)*p.FineT,sr=Read(n,false)*(1-p.FineT)+Read(n.NextFine,false)*p.FineT;
                shadow=ObserverShadow(sl,sr,p.Distance,p.Side,settings.HalfWidth,at.Y);
            }
            else return State.ObserverShadow; // P5 observer is on the published road. Hold if its neighbourhood is not current; no approximate off-road/vehicle branch.
            return shadow;
        }
        public static double ObserverShadow(double left,double right,double distance,double side,double width,double height)
        {
            double d=Math.Min(width,distance*1.1),blend=d/width*side/2+.5,result=left*blend+right*(1-blend);
            if(height<4)result=height<0?Math.Min(1,height/-2):Math.Min(result,height/4);
            return result<1e-5?0:result;
        }
        public void Release()
        {
            if(State?.Camera!=null&&Mathf.Approximately(State.Camera.farClipPlane,ownedFar))State.Camera.farClipPlane=initialFar;
            RestoreSun();sun=null;State?.Dispose();State=null;streaming=null;
        }
        void OnDestroy()=>Release();
    }
}
