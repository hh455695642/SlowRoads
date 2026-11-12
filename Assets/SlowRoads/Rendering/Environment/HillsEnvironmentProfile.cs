using System;
using UnityEngine;

namespace SlowRoads.Rendering.Environment
{
    public enum HillsEnvironmentTime { [InspectorName("早晨")] Morning, [InspectorName("白天")] Day, [InspectorName("傍晚")] Evening }
    public enum HillsEnvironmentWeather { [InspectorName("晴天")] Clear, [InspectorName("阴天")] Overcast }

    // Immutable source values. This table is not a second editable scene configuration.
    public readonly struct HillsEnvironmentFog
    {
        public readonly int A,B,C;
        public readonly double Near,Far,Height,Intensity;
        public HillsEnvironmentFog(int a,int b,int c,double near,double far,double height,double intensity)
        { A=a;B=b;C=c;Near=near;Far=far;Height=height;Intensity=intensity; }
    }
    public readonly struct HillsEnvironmentClouds
    {
        public readonly bool HasClouds;
        public readonly int Highlight,Lowlight,Mode;
        public readonly float Shelf0,Shelf1,Scale0,Scale1;
        public HillsEnvironmentClouds(bool has,int highlight,int lowlight,float shelf0,float shelf1,float scale0,float scale1,int mode)
        {HasClouds=has;Highlight=highlight;Lowlight=lowlight;Shelf0=shelf0;Shelf1=shelf1;Scale0=scale0;Scale1=scale1;Mode=mode;}
    }
    public readonly struct HillsEnvironmentLights
    {
        public readonly int Sun,Ambient,AmbientShadow;
        public readonly float SunIntensity,AmbientIntensity,Radiance,Fresnel;
        public HillsEnvironmentLights(int sun,float sunIntensity,int ambient,float ambientIntensity,int shadow,float radiance,float fresnel)
        {Sun=sun;SunIntensity=sunIntensity;Ambient=ambient;AmbientIntensity=ambientIntensity;AmbientShadow=shadow;Radiance=radiance;Fresnel=fresnel;}
    }
    public readonly struct HillsEnvironmentSnow
    {
        public readonly bool Configured;
        public readonly int Colour;
        public readonly float Density,Speed;
        public HillsEnvironmentSnow(bool configured,float density,int colour,float speed)
        {Configured=configured;Density=density;Colour=colour;Speed=speed;}
        public int PointCount=>!Configured||Density==0?0:Density<=.5f?5476:48841;
    }
    public sealed partial class HillsEnvironmentProfile
    {
        public int Season {get;}
        public HillsEnvironmentTime Time {get;}
        public HillsEnvironmentWeather Weather {get;}
        public HillsEnvironmentFog Fog {get;}
        public HillsEnvironmentClouds Clouds {get;}
        public HillsEnvironmentLights Lights {get;}
        public HillsEnvironmentSnow Snow {get;}
        public int WaterBase {get;}
        public int WaterBody {get;}
        public int WaterHighlight {get;}
        public float ShadowFactor {get;}
        public float TreeDiscolouration {get;}
        HillsEnvironmentProfile(int season,HillsEnvironmentTime time,HillsEnvironmentWeather weather,HillsEnvironmentFog fog,
            HillsEnvironmentClouds clouds,HillsEnvironmentLights lights,HillsEnvironmentSnow snow,int waterBase,int waterBody,int waterHighlight,float shadow,float treeDiscolouration)
        {Season=season;Time=time;Weather=weather;Fog=fog;Clouds=clouds;Lights=lights;Snow=snow;WaterBase=waterBase;WaterBody=waterBody;WaterHighlight=waterHighlight;ShadowFactor=shadow;TreeDiscolouration=treeDiscolouration;}
        public static HillsEnvironmentProfile Resolve(int unitySeason,HillsEnvironmentTime time,HillsEnvironmentWeather weather)
        {
            if((uint)unitySeason>=4)throw new ArgumentOutOfRangeException(nameof(unitySeason));
            if((uint)time>=3)throw new ArgumentOutOfRangeException(nameof(time));
            if((uint)weather>=2)throw new ArgumentOutOfRangeException(nameof(weather));
            return Profiles[unitySeason*6+(int)time*2+(int)weather];
        }
        // Original Three Color.setHex uses the sRGB transfer, unlike Hills XE's pow(2.2) surface helper.
        public static Color LinearColour(int hex)
        {
            static float Convert(float c)=>c<.04045f?c*.0773993808f:Mathf.Pow(c*.9478672986f+.0521327014f,2.4f);
            return new Color(Convert(((hex>>16)&255)/255f),Convert(((hex>>8)&255)/255f),Convert((hex&255)/255f),1);
        }
    }
}
