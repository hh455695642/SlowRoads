using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SlowRoads.Environment
{
    [DisallowMultipleComponent,RequireComponent(typeof(Camera),typeof(UniversalAdditionalCameraData))]
    public sealed class EnvironmentCameraBinding : MonoBehaviour
    {
        public EnvironmentResources resources;
        public double originX,originZ;
        public bool freezeTime;
        public EnvironmentCameraState State {get;private set;}
        double cloudTime;
        void OnEnable()
        {
            var camera=GetComponent<Camera>();State=new EnvironmentCameraState(camera);
            if(resources!=null){State.SetSkyMaterial(resources.skyMaterial);State.SetCloudMaterial(resources.cloudMaterial);State.SetSnowResources(resources.snowMaterial,resources.snowCulling);}
            camera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
        }
        void LateUpdate()
        {
            if(State==null)return;
            State.SetSkyMaterial(resources!=null?resources.skyMaterial:null);State.SetCloudMaterial(resources!=null?resources.cloudMaterial:null);State.SetSnowResources(resources!=null?resources.snowMaterial:null,resources!=null?resources.snowCulling:null);
            if(!freezeTime&&State.Frame.CloudsEnabled)cloudTime+=Time.deltaTime*State.Frame.CloudGeometry.z;
            State.Input=new EnvironmentFrameInput(transform.position.y,cloudTime,freezeTime?State.Input.SnowTime:Time.timeAsDouble,originX,originZ);
        }
        void OnDisable(){State?.Dispose();State=null;}
    }
}
