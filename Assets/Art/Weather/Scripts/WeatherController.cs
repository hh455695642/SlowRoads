using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SlowRoads.Environment
{
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Volume))]
    public sealed class WeatherController : MonoBehaviour
    {
        [Tooltip("天气季节独立于HillsDebugSession的地表季节；两者手动同步。")]
        public WeatherParameters weather=WeatherParameters.Default;
        public EnvironmentResources resources;
        public Volume Volume => GetComponent<Volume>();
        public int Applications {get;private set;}
        public string Error {get;private set;}
        WeatherParameters applied;
        VolumeProfile author,instance;
        bool pending=true,hasApplied;
#if UNITY_EDITOR
        public static readonly System.Collections.Generic.HashSet<WeatherController> Editors=new();
        public static event Action<WeatherController> Applied;
#endif
        public bool NeedsRefresh => pending || !hasApplied || !applied.Equals(weather) || author!=Volume.sharedProfile || (Volume.HasInstantiatedProfile() && instance!=Volume.profile);
        void OnEnable(){pending=true;
#if UNITY_EDITOR
            Editors.Add(this);
#endif
        }
        void OnDisable(){
#if UNITY_EDITOR
            Editors.Remove(this);
#endif
        }
        // Validation can be called during import; only schedule main-thread work here.
        void OnValidate()=>pending=true;
        void Update(){if(Application.isPlaying && NeedsRefresh)ApplyWeather();}
        public void RequestRefresh()=>pending=true;
        public void AcceptCurrentParameters(){applied=weather;author=Volume.sharedProfile;instance=Volume.HasInstantiatedProfile()?Volume.profile:null;pending=false;hasApplied=true;}
        public bool ApplyWeather()
        {
            var v=Volume;var p=v.sharedProfile;
            var runtime=v.HasInstantiatedProfile()?v.profile:null;
            if(!WeatherProfileWriter.IsComplete(p) || (runtime!=null && !WeatherProfileWriter.IsComplete(runtime)))
                return Reject("请给Volume指定含Sky、Clouds、Ground Fog和Snow的Profile。点击应用可在编辑器补齐组件。");
            try
            {
                WeatherProfileWriter.Write(p,weather);
                if(runtime!=null && runtime!=p)WeatherProfileWriter.Write(runtime,weather);
            }
            catch(ArgumentException e){return Reject(e.Message);}
            AcceptCurrentParameters();Error=null;Applications++;
#if UNITY_EDITOR
            Applied?.Invoke(this);
#endif
            return true;
        }
        bool Reject(string error){if(Error!=error)Debug.LogWarning(error,this);Error=error;pending=false;applied=weather;author=Volume.sharedProfile;instance=Volume.HasInstantiatedProfile()?Volume.profile:null;hasApplied=true;return false;}
    }
}
