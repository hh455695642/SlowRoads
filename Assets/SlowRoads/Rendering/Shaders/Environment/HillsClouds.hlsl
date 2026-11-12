#ifndef HILLS_CLOUDS_INCLUDED
#define HILLS_CLOUDS_INCLUDED
#include "HillsAtmosphere.hlsl"
TEXTURE2D(_CloudMap);
#if defined(HILLS_CLOUD_ORIGINAL_SAMPLER) && !defined(SHADER_API_GLES3) && !defined(SHADER_API_GLES)
// Fixed source sampler: project ForceEnable otherwise promotes texture aniso4 to >=9.
SAMPLER(sampler_Trilinear_Repeat_Aniso4);
#define HILLS_CLOUD_SAMPLER sampler_Trilinear_Repeat_Aniso4
#else
// GLES uses the texture's bounded Trilinear/Repeat/aniso4 fallback. No extra runtime variant.
SAMPLER(sampler_CloudMap);
#define HILLS_CLOUD_SAMPLER sampler_CloudMap
#endif
float4 HillsCloudTexture(float2 uv)
{
// Preserve original implicit texture() sampling, without URP global mip bias.
    return PLATFORM_SAMPLE_TEXTURE2D(_CloudMap,HILLS_CLOUD_SAMPLER,uv);
}
float4 _HillsCloudShelf; // shelf0, shelf1, scale0, scale1
float4 _HillsCloudParameters; // altitude, mode, original uniform time, reserved
float4 _HillsCloudHighlight,_HillsCloudLowlight;
struct HillsCloudVertexData
{
    float3 transformed;
    float3 world;
    float4 uv01;
    float2 uv2;
    float dist;
    float4 fog;
};
HillsCloudVertexData HillsCloudVertex(float3 position,float2 uv,float4x4 model,float3 camera)
{
    HillsCloudVertexData o;
    float3 w=mul(model,float4(position,1)).xyz+_HillsLogicalOrigin.xyz;
    float3 cam=camera+_HillsLogicalOrigin.xyz;
    float h0=_HillsCloudShelf.x-cam.y,h1=_HillsCloudShelf.y-cam.y;
    float altitude=_HillsCloudParameters.x,time=_HillsCloudParameters.z;
    o.uv01.xy=(w.xz+time)/(_HillsCloudShelf.z*altitude);
    float scale1=_HillsCloudShelf.w*altitude;
    float lowerScale=scale1*h1/h0,motionScale=(h1-h0)/h0;
    o.uv01.zw=(w.xz+cam.xz*motionScale)/lowerScale+time/scale1;
    o.uv2=o.uv01.zw+float2(.001,-.001);
    float2 v=(uv-.5)*2;float dist=max(0,1-sqrt(v.x*v.x+v.y*v.y));o.dist=dist;
    o.transformed=position;float bend=(1-o.dist)*(1-o.dist)*altitude*.5;o.transformed.z+=bend;
    o.world=mul(model,float4(o.transformed,1)).xyz;
    float near=_HillsFogRange.x,far=_HillsFogRange.y,blend=1,avg=(far+near)/2;
    if(avg<500)blend=avg<50?0:(avg-50)/450;
    if(_HillsHaze.x<0)blend*=1-_HillsHaze.y;
    float depth=far;
    if(_HillsFogRange.w>.5)
    {
        depth=max(0,(depth-near)*(far/(far-near)));
        depth=1-exp(-_HillsFogRange.z*_HillsFogRange.z*depth*depth);
    }
    else depth=smoothstep(near,far,depth);
    // Original So leaves vDesat unwritten. Explicit zero matches the captured desktop driver;
    // this is a documented defined-value adapter, not an assertion about undefined GLSL.
    o.fog=float4(depth,0,0,blend);return o;
}
float3 HillsCloudBlend(float3 colour,float4 c1,float4 c2,float4 c3,float dist,float fogBlend)
{
    float mode=_HillsCloudParameters.y;float3 low=_HillsCloudLowlight.rgb,high=_HillsCloudHighlight.rgb;
    float alpha;float3 cloud;
    if(mode==0)
    {
        float ic=1-(1-c1.a)*(1-c2.a);ic*=c3.a;
        cloud=lerp(low,high,c3.a*ic);colour=lerp(colour,cloud,ic*min(1,fogBlend)*dist);
    }
    else if(mode==1)
    {
        alpha=1-(1-c1.a)*(1-c2.a);cloud=lerp(low,high,c1.a*alpha*c3.a);
        colour=lerp(colour,cloud,alpha*dist*dist);
    }
    else if(mode==2)
    {
        c2.a=.25+c2.a*.75;c3.a*=.5;c2.a*=c1.a;c3.a*=c1.a;
        alpha=1-(1-c1.a)*(1-c2.a);cloud=lerp(low,high*1.5,c3.a*(dist*2));
        colour=lerp(colour,cloud,alpha*min(1,fogBlend)*dist);
    }
    else if(mode==3)
    {
        cloud=lerp(low,high,min(c2.a,c3.a));alpha=max(c2.a,c1.a*c3.a);
        colour=lerp(colour,cloud,alpha*dist*dist);
    }
    // Original mode4/5 preserve the fogged base; hasClouds is a separate scene condition.
    return colour;
}
float4 HillsCloudFragmentFromBase(float3 base,float4 uv01,float2 uv2,float dist,float3 world,float3 camera,float4 fog)
{
    float3 colour=HillsFogFragment(base,world,camera,fog);
    float4 c1=HillsCloudTexture(uv01.xy);
    float4 c2=HillsCloudTexture(uv01.zw);
    float4 c3=HillsCloudTexture(uv2);
    float blend=max((world.y-camera.y)*2/distance(world,camera),0)*fog.w;
    return float4(HillsCloudBlend(colour,c1,c2,c3,dist,blend),1);
}
float4 HillsCloudFragment(float2 uv,float4 uv01,float2 uv2,float dist,float3 world,float3 camera,float4 fog)
{
    return HillsCloudFragmentFromBase(HillsCloudTexture(uv).rgb,uv01,uv2,dist,world,camera,fog);
}
#endif
