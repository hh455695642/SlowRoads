#ifndef HILLS_SURFACE_ENVIRONMENT_INCLUDED
#define HILLS_SURFACE_ENVIRONMENT_INCLUDED
#include "HillsAtmosphere.hlsl"
float4 _HillsSurfaceControl; // fog enabled, source lighting enabled, original sun intensity, revision
float4 _HillsSun,_HillsAmbient,_HillsHemi,_HillsTheme;
float4 _HillsWaterBase,_HillsWaterBody,_HillsWaterHighlight;
float HillsThemeShadow(float fallback){return _HillsSurfaceControl.y>.5?_HillsTheme.x:fallback;}
float HillsThemeRadiance(float fallback){return _HillsSurfaceControl.y>.5?_HillsTheme.y:fallback;}
float HillsThemeFresnel(float fallback){return _HillsSurfaceControl.y>.5?_HillsTheme.z:fallback;}
float HillsThemeTreeDiscolouration(float fallback){return _HillsSurfaceControl.y>.5?_HillsTheme.w:fallback;}
float3 HillsLightDirection(float3 fallback){return _HillsSurfaceControl.y>.5?float3(0,1,0):fallback;}
float3 HillsLightColour(float3 fallback){return _HillsSurfaceControl.y>.5?_HillsSun.rgb:fallback;}
float3 HillsAmbientColour(float3 normal,float3 fallback)
{
    // Original HemisphereLight's ground colour is explicitly black, not a ground tint/SH fit.
    return _HillsSurfaceControl.y>.5?_HillsAmbient.rgb+_HillsHemi.rgb*(normal.y*.5+.5):fallback;
}
float4 HillsSurfaceFogVertex(float3 world)
{
    // Uniform early-out: no noise work when disabled; no season/weather variants.
    return _HillsSurfaceControl.x>.5?HillsFogVertex(world,_WorldSpaceCameraPos,false):0;
}
float3 HillsSurfaceFog(float3 colour,float3 world,float4 fog)
{
    return _HillsSurfaceControl.x>.5?HillsFogFragment(colour,world,_WorldSpaceCameraPos,fog):colour;
}
#endif
