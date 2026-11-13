using System;
using UnityEngine;
using UnityEngine.Rendering;
using SlowRoads.Rendering.Environment;

namespace SlowRoads.Environment
{
    public enum WeatherSeason { Summer=0, Spring=1, Autumn=2, Winter=3 }

    [Serializable]
    public struct WeatherParameters : IEquatable<WeatherParameters>
    {
        [InspectorName("天气季节")] public WeatherSeason season;
        [InspectorName("天气时段")] public HillsEnvironmentTime time;
        [InspectorName("天气")] public HillsEnvironmentWeather weather;
        [InspectorName("显示天空")] public bool sky;
        [InspectorName("显示云层")] public bool clouds;
        [InspectorName("显示地景雾")] public bool fog;
        [InspectorName("显示飘雪")] public bool snow;
        [InspectorName("天气范围档位"),Range(0,5)] public int view;
        [InspectorName("天气细节档位"),Range(0,4)] public int detail;
        public bool Equals(WeatherParameters p) => season==p.season && time==p.time && weather==p.weather && sky==p.sky && clouds==p.clouds && fog==p.fog && snow==p.snow && view==p.view && detail==p.detail;
        public static WeatherParameters Default => new() { season=WeatherSeason.Summer,time=HillsEnvironmentTime.Day,weather=HillsEnvironmentWeather.Clear,sky=true,clouds=true,fog=true,snow=true,view=2,detail=2 };
    }

    // Sole weather preset writer. It never modifies Lighting, other overrides, clocks or generation.
    public static class WeatherProfileWriter
    {
        public static bool IsWeather(VolumeComponent c) => c is EnvironmentSkyVolume or EnvironmentCloudVolume or EnvironmentFogVolume or EnvironmentSnowVolume;
        public static bool IsComplete(VolumeProfile p) => p!=null && p.Has<EnvironmentSkyVolume>() && p.Has<EnvironmentCloudVolume>() && p.Has<EnvironmentFogVolume>() && p.Has<EnvironmentSnowVolume>();
        public static void Write(VolumeProfile target, in WeatherParameters input)
        {
            if (!IsComplete(target)) throw new ArgumentException("Weather Profile requires Sky, Clouds, Ground Fog and Snow components",nameof(target));
            if ((uint)input.view>=6 || (uint)input.detail>=5) throw new ArgumentOutOfRangeException(nameof(input));
            var p=HillsEnvironmentProfile.Resolve((int)input.season,input.time,input.weather);
            target.TryGet<EnvironmentSkyVolume>(out var sky);target.TryGet<EnvironmentFogVolume>(out var fog);
            target.TryGet<EnvironmentCloudVolume>(out var cloud);target.TryGet<EnvironmentSnowVolume>(out var snow);
            double range=input.view switch {0=>360,1=>960,2=>1680,3=>3000,4=>4000,_=>5000};
            double haze=p.Fog.Intensity;if(haze<=0 && range<2000) haze=Math.Max(-1,(1+haze)*(range/2000d)-1);
            void Atmosphere(EnvironmentAtmosphereVolume v,bool enabled,int detail)
            {
                Own(v);v.enabled.value=enabled;
                v.colourA.value=HillsEnvironmentProfile.LinearColour(p.Fog.A);v.colourB.value=HillsEnvironmentProfile.LinearColour(p.Fog.B);v.colourC.value=HillsEnvironmentProfile.LinearColour(p.Fog.C);
                v.near.value=range*p.Fog.Near;v.far.value=range*p.Fog.Far;
                v.mode.value=detail>2?EnvironmentFogMode.ExponentialSquared:EnvironmentFogMode.Linear;
                v.densityFromFar.value=true;v.density.value=0;v.hazeHeight.value=p.Fog.Height;v.hazeIntensity.value=haze;v.cameraHeightCorrection.value=true;
            }
            Atmosphere(sky,input.sky,input.detail);sky.backgroundDistance.value=range;Atmosphere(fog,input.fog,input.detail);
            Own(cloud);var c=p.Clouds;cloud.enabled.value=input.clouds && c.HasClouds && input.detail>0;
            cloud.highlight.value=HillsEnvironmentProfile.LinearColour(c.Highlight);cloud.lowlight.value=HillsEnvironmentProfile.LinearColour(c.Lowlight);
            cloud.shelf0.value=c.Shelf0;cloud.shelf1.value=c.Shelf1;cloud.scale0.value=c.Scale0;cloud.scale1.value=c.Scale1;cloud.mode.value=c.Mode;
            cloud.altitude.value=input.view switch {0=>400,1=>500,2=>600,3=>600,4=>700,_=>800};
            cloud.worldScale.value=input.view switch {0=>1200,1=>3600,2=>7000,3=>11000,4=>15000,_=>22000};
            cloud.segments.value=(int)Mathf.Max(20,cloud.worldScale.value/400);cloud.animationSpeed.value=2;
            Own(snow);snow.enabled.value=input.snow;snow.quality.value=p.Snow.PointCount==48841?EnvironmentSnowQuality.Full:p.Snow.PointCount==5476?EnvironmentSnowQuality.Half:EnvironmentSnowQuality.Off;
            snow.colour.value=HillsEnvironmentProfile.LinearColour(p.Snow.Colour);snow.speed.value=p.Snow.Speed;
        }
        static void Own(VolumeComponent c){c.active=true;foreach(var p in c.parameters)p.overrideState=true;}
    }
}
