#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "../Environment/HillsSurfaceEnvironment.hlsl"
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
TEXTURE2D(_AlphaMap);SAMPLER(sampler_AlphaMap);
TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
TEXTURE2D(_NoiseMap);SAMPLER(sampler_NoiseMap);
CBUFFER_START(UnityPerMaterial)
float4 _ClusterOrigin;
float4 _NoiseOffset;
half _Cutoff,_UseAlphaMap,_Family,_Billboard,_Variant,_Lod,_UseVisible;
float _LodNear,_LodFar;
half _Discolouration,_ShadowFactor,_Radiance,_HasSnow;
float4 _TreeAmbient[7];
CBUFFER_END
#ifndef TREE_CPU
struct TreeData {float4 positionScale;float4 parameters;};
StructuredBuffer<TreeData> _Trees;
StructuredBuffer<uint> _Visible;
#endif
struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;uint instanceID:SV_InstanceID;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;half3 normalWS:TEXCOORD1;float3 world:TEXCOORD2;half2 shadowDensity:TEXCOORD3;half dissolve:TEXCOORD4;float3 angleBlend:TEXCOORD5;float2 rotation:TEXCOORD6;half4 sourceSurface:TEXCOORD7;half3 centerNormal:TEXCOORD8;float2 normalUv:TEXCOORD9;float4 fog:TEXCOORD10;float3 fogWorld:TEXCOORD11;UNITY_VERTEX_INPUT_INSTANCE_ID};
Varyings TreeVert(Attributes i)
{
    Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
    float3 p=i.positionOS;float3 sourceFogPosition=i.positionOS;float rotation=0,scale=1,density=0,nearActive=1;float3 center=0;
    #ifndef TREE_CPU
    TreeData t=_Trees[_UseVisible>.5?_Visible[i.instanceID]:i.instanceID];center=t.positionScale.xyz;scale=t.positionScale.w;rotation=t.parameters.x;density=t.parameters.y;nearActive=t.parameters.w;
    #endif
    float sn,cs;sincos(rotation,sn,cs);
    float3 centerNormal;
    float radius=length(p.xz),h=p.y/12;
    centerNormal=radius<.0001?float3(p.x,1,p.z):float3(p.x,p.y<0?0:max(.5,h*1.5)*radius,p.z);
    if(_Family<.5)centerNormal=p-float3(0,i.uv.y>.25?min(p.y,5):7,0);
    centerNormal=normalize(float3(centerNormal.x*cs+centerNormal.z*sn,centerNormal.y,-centerNormal.x*sn+centerNormal.z*cs));
    float3 surfaceNormal=_Family>.5?centerNormal:normalize(float3(i.normalOS.x*cs+i.normalOS.z*sn,i.normalOS.y,-i.normalOS.x*sn+i.normalOS.z*cs));
    o.normalUv=i.uv;
    o.rotation=float2(cs,sn);
    if(_Billboard>.5)
    {
        // Original Hills Ho: sixteen azimuths horizontally, four variants vertically.
        float3 direction=(_ClusterOrigin.xyz+center)-_WorldSpaceCameraPos;
        surfaceNormal=normalize(float3(-direction.x,0,-direction.z));
        o.normalUv=i.uv*float2(1.0/16,.25-.002);
        float angle=atan2(direction.x,direction.z),ca=cos(angle),sa=sin(angle);
        o.rotation=float2(ca,sa);
        sourceFogPosition.xz=float2(sourceFogPosition.x*ca,-sourceFogPosition.x*sa);
        float crop=(.8-_Family*.1)*(1-step(.5,_Family)*i.uv.y*(.25-.002)*4*.6);
        p.xz=float2(p.x*ca,-p.x*sa)*crop;
        float normalized=angle-rotation;if(normalized>PI)normalized-=TWO_PI;else if(normalized< -PI)normalized+=TWO_PI;
        float tile=(normalized+PI)/(TWO_PI/16);
        o.uv=float2((i.uv.x*crop+(1-crop)/2+floor(tile))/16,i.uv.y*(.25-.002)+_Variant/4);
        float depth=-TransformWorldToView(_ClusterOrigin.xyz+center).z;
        o.angleBlend=float3(frac(o.uv.x+1.0/16),o.uv.y,frac(tile)*step(depth,500));
        float inflate=saturate((depth-200)/800);p*=1+inflate*.3;p.y-=inflate*4;
        p=p*scale+center;
    }
    else {p=float3(p.x*cs+p.z*sn,p.y,-p.x*sn+p.z*cs)*scale+center;o.uv=i.uv;}
    #ifdef TREE_CPU
    p=TransformObjectToWorld(p);centerNormal=TransformObjectToWorldNormal(centerNormal);surfaceNormal=TransformObjectToWorldNormal(surfaceNormal);
    #endif
    if(_Billboard<.5)sourceFogPosition=float3(sourceFogPosition.x*cs+sourceFogPosition.z*sn,sourceFogPosition.y,-sourceFogPosition.x*sn+sourceFogPosition.z*cs);
    sourceFogPosition=sourceFogPosition*scale+center;
    #ifdef TREE_CPU
    sourceFogPosition=TransformObjectToWorld(sourceFogPosition);
    #endif
    o.fogWorld=sourceFogPosition+_ClusterOrigin.xyz;o.fog=HillsSurfaceFogVertex(o.fogWorld);
    o.world=p+_ClusterOrigin.xyz;o.positionCS=TransformWorldToHClip(o.world);o.normalWS=surfaceNormal;o.centerNormal=centerNormal*scale;
    half larch=step(.59,o.uv.x)*(1-step(.77,o.uv.x));half cypress=step(.77,o.uv.x);
    half shadow=((2.5+larch+density)*max(.1,1-min(1,h))-radius)*(1-max(larch,cypress)*.25);
    if(o.uv.x<.125)shadow=.7+.3*density;
    if(_Family<.5){float3 delta=i.positionOS-float3(0,min(i.positionOS.y,5+density*HillsThemeShadow(_ShadowFactor)),0);float d=dot(delta,delta);
        shadow=d<36?(d<8?1:1-(d-8)/28):0;shadow=max(shadow,1-saturate(i.positionOS.y/8));}
    o.shadowDensity=half2(shadow*HillsThemeShadow(_ShadowFactor),density);
    float distance=-TransformWorldToView(_ClusterOrigin.xyz+center).z;
    if(_Billboard<.5)distance=-TransformWorldToView(o.world).z;
    half brightness=1-density*HillsThemeShadow(_ShadowFactor)*saturate((10-i.positionOS.y)/3);
    half radianceBlend=1-saturate((distance-50)/200)*.33;
    if(_Billboard>.5){brightness=1-.25*HillsThemeShadow(_ShadowFactor)*saturate((.25-i.uv.y*(.25-.002))/.08);radianceBlend*=max(.5,brightness);}
    half alphaTest=_Cutoff;
    if(_Family<.5&&_Billboard<.5&&distance<(_LodNear>200?_LodNear:200))alphaTest=max(.3,_Cutoff*(1-distance/(_LodNear>200?_LodNear*1.3:300)));
    half discolor=1+(SAMPLE_TEXTURE2D_LOD(_NoiseMap,sampler_NoiseMap,(_ClusterOrigin.xz+center.xz+_NoiseOffset.xz)/256,0).r-.5)*HillsThemeTreeDiscolouration(_Discolouration);
    if(_Family>.5&&_Billboard<.5&&o.uv.x>.76)discolor=discolor/2+.5;
    o.sourceSurface=half4(brightness,radianceBlend,alphaTest,discolor);
    // Hills ds: mesh 0.70F..F; imposter 0.58F..0.88F, not symmetric fades.
    o.dissolve=_Billboard>.5?(nearActive>.5?1-saturate((distance-_LodNear*.58)/max(1,_LodNear*.3)):0):saturate((distance-_LodNear*.7)/max(1,_LodNear*.3));
    #ifdef TREE_SHADOW
    o.dissolve=nearActive>.5?saturate((length((_ClusterOrigin.xyz+center-_WorldSpaceCameraPos).xz)-_LodNear*.7)/max(1,_LodNear*.3)):1;
    #endif
    return o;
}
// Original Hills Fo/zo: blend/dissolve noise uses the uncropped local quad UV,
// not the atlas tile/variant UV. normalUv already holds (u/16, v*(1/4-.002)).
// Reuse the existing interpolator: no new texture read, varying or keyword.
float2 TreeNoiseUv(Varyings i) { return _Billboard>.5?i.normalUv*float2(21.32,4):i.uv; }
half4 TreeSurface(Varyings i,out half3 emissive)
{
    emissive=0;
    half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
    half alpha=_UseAlphaMap>.5?c.a*SAMPLE_TEXTURE2D(_AlphaMap,sampler_AlphaMap,i.uv).g:c.a;
    // Hills zo uses a noise-selected azimuth, not a linear blend between trees.
    half noise=SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,TreeNoiseUv(i)).r;
    if(_Billboard>.5){half4 next=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.angleBlend.xy);c=lerp(c,next,step(noise,i.angleBlend.z));alpha=c.a;}
    clip(alpha-i.sourceSurface.z);
    if(_Billboard<.5||i.angleBlend.z>0)clip(noise-i.dissolve);clip(.999-i.dissolve);
    half discolor=i.sourceSurface.w;
    if(_Family<.5){half leaf=_Billboard>.5?i.sourceSurface.x:step(c.b*1.5,c.g)*i.sourceSurface.x;c.rg*=_Billboard>.5?discolor:1-(1-discolor)*leaf;emissive=half3(c.rg*8,.5)*(HillsThemeRadiance(_Radiance*2)*.5)*leaf*i.sourceSurface.y;c.rgb*=i.sourceSurface.y;}
    else {if(_Billboard>.5||i.uv.x>.125)c.g*=discolor;if(_Billboard<.5)c.rgb*=1-sqrt(saturate(i.shadowDensity.x))*.65;}
    return c;
}
half4 TreeSurface(Varyings i){half3 emissive;return TreeSurface(i,emissive);}
half3 TreeNormal(Varyings i)
{
    if(_Family>.5&&_Billboard<.5)return normalize(i.normalWS);
    half3 n=SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv).rgb*2-1;
    if(_Billboard>.5){half3 next=SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.angleBlend.xy).rgb*2-1;half noise=SAMPLE_TEXTURE2D(_NoiseMap,sampler_NoiseMap,TreeNoiseUv(i)).r;n=lerp(n,next,step(noise,i.angleBlend.z));}
    // normals.dk -> Er=0 -> normalMapTangentSpace. Preserve the source channels;
    // use Three's derivative tangent frame, without Unity NormalMap repacking.
    float3 N=normalize(i.normalWS),q0=ddx(i.world),q1=ddy(i.world);
    float2 st0=ddx(i.normalUv),st1=ddy(i.normalUv);
    float3 q1perp=cross(q1,N),q0perp=cross(N,q0);
    float3 T=q1perp*st0.x+q0perp*st1.x,B=q1perp*st0.y+q0perp*st1.y;
    float det=max(dot(T,T),dot(B,B));float s=det==0?0:rsqrt(det);
    return normalize(T*s*n.x+B*s*n.y+N*n.z);
}
half4 TreeAlbedo(Varyings i,out half3 emissive,out half3 n)
{
    half4 c=TreeSurface(i,emissive);n=TreeNormal(i);
    if(_HasSnow>.5&&_Family<.5){if(_Billboard>.5&&n.y>0)c.rgb=lerp(c.rgb,1.0.xxx,min(1,n.y*8));else if(_Billboard<.5&&n.y>.1)c.rgb=lerp(c.rgb,min(1,.5+n.y).xxx,min(1,(n.y-.1)*8));}
    return c;
}
float4 TreeFrag(Varyings i):SV_Target
{
    half3 emissive,n;half4 c=TreeAlbedo(i,emissive,n);Light light=GetMainLight();float3 sourceLightDirection=HillsLightDirection(light.direction),sourceLightColour=HillsLightColour(light.color);
    half fShadow=sqrt(saturate(i.shadowDensity.x))*.65;
    half centerDot=dot(i.centerNormal,sourceLightDirection);
    half squash=_Billboard>.5?1:(_Family>.5?saturate(centerDot):min(1,max(.25,centerDot*2))*(i.uv.y>.25?1-i.shadowDensity.x*.5:1));
    float3 direct=c.rgb*sourceLightColour*saturate(dot(n,sourceLightDirection))*squash;
    half ambientShadow=_Billboard>.5?1:(_Family>.5?1-max(0,fShadow-.6):(i.shadowDensity.x>.5?1.5-i.shadowDensity.x:1));
    float3 ambient=max(0,HillsAmbientColour(n,SampleSH9(_TreeAmbient,n)))*c.rgb*ambientShadow;
    return float4(HillsSurfaceFog(direct+ambient+direct*emissive,i.fogWorld,i.fog),1);
}
half4 TreeDepth(Varyings i):SV_Target {TreeSurface(i);return 0;}
half4 TreeNormals(Varyings i):SV_Target {TreeSurface(i);return half4(TreeNormal(i),0);}
