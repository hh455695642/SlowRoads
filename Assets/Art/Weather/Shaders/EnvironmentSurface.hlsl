#ifndef ENVIRONMENT_SURFACE_INCLUDED
#define ENVIRONMENT_SURFACE_INCLUDED
#include "HillsAtmosphere.hlsl"
float4 _HillsSurfaceControl;
float4 HillsSurfaceFogVertex(float3 world){return _HillsSurfaceControl.x>.5?HillsFogVertex(world,_WorldSpaceCameraPos,false):0;}
float3 HillsSurfaceFog(float3 colour,float3 world,float4 fog){return _HillsSurfaceControl.x>.5?HillsFogFragment(colour,world,_WorldSpaceCameraPos,fog):colour;}
#endif
