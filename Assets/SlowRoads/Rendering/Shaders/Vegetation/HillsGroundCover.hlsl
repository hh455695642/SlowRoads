#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
// Original Bo/Wo/Xo/Zo and Qo/Yo/Jo. Grass and bush use separate shaders, no combinatorial keyword.
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
TEXTURE2D(_NoiseMap);SAMPLER(sampler_NoiseMap);
TEXTURE2D(grassVariationMap);SAMPLER(sampler_grassVariationMap);
TEXTURE2D(detailFarMap);SAMPLER(sampler_detailFarMap);
CBUFFER_START(UnityPerMaterial)
float4 _ClusterOrigin,_GrassTextureOrigin;
float4 grassColA,grassColB,peakColA,peakColB;
float4 _TreeAmbient[7];
float shadowFactor,radiance,fresnelIntensity,seasonIndex,_UseVisible;
CBUFFER_END
struct GroundInstance {float4 positionScale;float4 parameters;};
StructuredBuffer<GroundInstance> _Trees;
StructuredBuffer<float4> _GrassExtra;
StructuredBuffer<uint> _Visible;
struct CoverAttributes {float3 positionOS:POSITION;float2 uv:TEXCOORD0;uint instanceID:SV_InstanceID;};
struct CoverVaryings
{
    float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;
    float3 ground:TEXCOORD2;float4 mapUV:TEXCOORD3;float4 colourUV:TEXCOORD4;
    float4 factors:TEXCOORD5;float4 probe:TEXCOORD6;UNITY_VERTEX_INPUT_INSTANCE_ID
};
float3 RotateCover(float3 p,float rotation,float3 normal)
{
    float sn,cs;sincos(rotation,sn,cs);
    // E1.rotation.order="XZY" (S L45938): apply Y, then Z, then X.
    p=float3(p.x*cs+p.z*sn,p.y,-p.x*sn+p.z*cs);
    #ifndef HILLS_BUSH
    float sx=normal.z,cx=sqrt(max(0,1-sx*sx)),sz=-normal.x,cz=sqrt(max(0,1-sz*sz));
    p=float3(p.x*cz-p.y*sz,p.x*sz+p.y*cz,p.z);
    #endif
    #ifndef HILLS_BUSH
    p=float3(p.x,p.y*cx-p.z*sx,p.y*sx+p.z*cx);
    #endif
    return p;
}
float ScreenCover(float a,float b){return 1-(1-a)*(1-b);}
CoverVaryings CoverVert(CoverAttributes i)
{
    CoverVaryings o=(CoverVaryings)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
    uint index=_UseVisible>.5?_Visible[i.instanceID]:i.instanceID;
    GroundInstance data=_Trees[index];float4 extra=_GrassExtra[index];float3 normal=extra.xyz;
    float rotation=data.parameters.x,scale=data.positionScale.w,tm=data.parameters.y,rp=extra.w;
    float3 center=data.positionScale.xyz+_ClusterOrigin.xyz;
    float3 probe=center+RotateCover(float3(1,1,1),rotation,normal)*scale;
    float3 position=i.positionOS;float2 uv=i.uv;
    float dist=dot(center.xz-_WorldSpaceCameraPos.xz,center.xz-_WorldSpaceCameraPos.xz);
    float shadow=min(1,tm*min(1,max(0,probe.y/4))),variant=data.parameters.z;
    #ifdef HILLS_BUSH
    float2 wv=(_GrassTextureOrigin.xy+data.positionScale.xz+RotateCover(float3(1,1,1),rotation,normal).xz*scale)/512;
    float fade=SAMPLE_TEXTURE2D_LOD(_NoiseMap,sampler_NoiseMap,wv,0).r;
    if(variant==0){if(shadow>.1){if(fade>.41)variant=fade>.52?2:1;}else if(fade>.46)variant=fade>.54?1:2;}
    if(variant==2)position.y-=.1;
    float shrink=saturate((dist-10000)/30000);position*=1-shrink*.5;position.y-=shrink*.5;
    o.mapUV=float4(wv,0,0);o.factors=float4(shadow*shadowFactor,saturate(i.positionOS.y*2),dist>40000?1:0,0);
    #else
    float2 absolute=_GrassTextureOrigin.xy+data.positionScale.xz;
    float2 wv=absolute/500,wwv=wv/8,gv=absolute/10,ggv=gv/16;
    float fade0=SAMPLE_TEXTURE2D_LOD(_NoiseMap,sampler_NoiseMap,wv,0).r,fade1=SAMPLE_TEXTURE2D_LOD(_NoiseMap,sampler_NoiseMap,wwv,0).r;
    float v=SAMPLE_TEXTURE2D_LOD(_NoiseMap,sampler_NoiseMap,ggv,0).r;
    float heather=smoothstep(.4,1,((center.y+fade1*350)-60)/500)*ScreenCover(fade1,fade0);
    variant=1;
    if(shadow<.1&&heather>.38+v/6){variant=3;position.y-=v*.2*(rp>1?1:rp);}
    if(rp<v*2+.2)variant=0;
    else if((variant<3&&(center.y>100&&v+fade0>1.75-(center.y-50)/250))||shadow>v*2)variant=2;
    else if(variant==3)position*=.5+fade0*1.5;
    if(rp<-.5)variant=1;
    float lightGrass=0,darkGrass=0,doColour=variant<3?1:0;
    if(doColour>.5)
    {
        float h=saturate((center.y-(30+fade1*40))/150);lightGrass=min(1,h*(fade0+h*.5));darkGrass=saturate((fade1-.25)*2);
        if(rp<.5+fade1)lightGrass*=max(.5,rp/(.5+fade1));
    }
    position.y-=saturate((dist-10000)/30000)*.5;position*=1-step(40000,dist);
    if(seasonIndex<3){float crop=1-.5*step(.5,abs(variant-2));position.y*=crop;uv.y*=crop;}
    else{float keep=step(1.5,variant);position.xz*=keep;position.y*=keep*.5;uv.y*=keep*.5;}
    o.mapUV=float4(wv,wwv*2);o.colourUV=float4(gv*1.4,ggv);o.factors=float4(shadow*shadowFactor,lightGrass,darkGrass,doColour);
    #endif
    o.world=center+RotateCover(position,rotation,normal)*scale;
    o.positionCS=TransformWorldToHClip(o.world);o.ground=normal;o.uv=uv+float2(variant*.25,0);
    o.probe=float4(probe-float3(0,1,0),dist);return o;
}
float4 CoverSurface(CoverVaryings i,out float3 emission)
{
    float4 texel=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
    emission=0;
    #ifdef HILLS_BUSH
    if(i.factors.z>0)discard;
    // Original fixed material alphaTest=.5 still applies after the dynamic map-fragment test.
    clip(texel.a-.5);
    emission=texel.rgb*radiance*i.factors.y;
    #else
    clip(texel.a-.25);
    if(i.factors.w>.5)
    {
        float fade0=SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,i.colourUV.xy).r,fade1=SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,i.colourUV.zw).r;
        float variation=SAMPLE_TEXTURE2D(grassVariationMap,sampler_grassVariationMap,i.mapUV.zw).r-.5;
        float closeVariation=(1-SAMPLE_TEXTURE2D(grassVariationMap,sampler_grassVariationMap,i.mapUV.xy).r)-.5;
        float blend=(ScreenCover(fade0,fade1)+i.factors.z)/2;blend=saturate(blend*blend+variation+closeVariation);
        float lightFactor=saturate(saturate(i.factors.y+variation-closeVariation)*2*texel.r);
        float3 colour=lerp(lerp(grassColA.rgb,grassColB.rgb,blend),lerp(peakColA.rgb,peakColB.rgb,blend),lightFactor);
        texel.rgb*=colour*(1-(1-SAMPLE_TEXTURE2D(detailFarMap,sampler_detailFarMap,i.mapUV.xy).r)*.7);
    }
    else{clip(texel.a-.98);texel.rgb*=1+(SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,i.colourUV.xy).r-.6);}
    emission=texel.rgb*radiance;
    float fresnel=1-max(0,dot(normalize(_WorldSpaceCameraPos-i.probe.xyz),i.ground));
    float lightFresnel=max(0,fresnel-.75)*4*clamp((i.uv.y*3),.7,.9);
    lightFresnel*=lightFresnel;lightFresnel*=(1-i.factors.x)*min(1,i.probe.w/100)*fresnelIntensity*max(i.factors.w,.5);
    float darkFresnel=(1-min(1,fresnel*1.3333))*fresnelIntensity*.5;darkFresnel=.25+min(.25,max(0,darkFresnel-.25)*.5);
    texel.rgb*=1+lightFresnel-darkFresnel;
    #endif
    return texel;
}
float3 CoverAmbient(float3 n)
{
    float4 v=float4(n,1);float3 a=float3(dot(_TreeAmbient[0],v),dot(_TreeAmbient[1],v),dot(_TreeAmbient[2],v));
    float4 b=n.xyzz*n.yzzx;a+=float3(dot(_TreeAmbient[3],b),dot(_TreeAmbient[4],b),dot(_TreeAmbient[5],b));a+=_TreeAmbient[6].rgb*(n.x*n.x-n.y*n.y);return max(0,a);
}
float4 CoverForward(CoverVaryings i):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);float3 emission;float4 c=CoverSurface(i,emission);float3 n=normalize(i.ground);Light light=GetMainLight();
    float shadow=i.factors.x;float3 direct=saturate(dot(n,light.direction))*light.color*light.distanceAttenuation*(1-min(1,shadow));
    float3 indirect=CoverAmbient(n)*(1.75-min(1,max(.75,shadow)));
    return float4(c.rgb*(direct+indirect+direct*emission),1);
}
float4 CoverDepth(CoverVaryings i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);float3 emission;CoverSurface(i,emission);return 0;}
float4 CoverDepthNormals(CoverVaryings i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);float3 emission;CoverSurface(i,emission);return float4(normalize(i.ground),0);}
