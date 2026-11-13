// C6 signs + verge-marker surfaces. Uniform controls, instancing only: 2/pass, 4 total.
// Uses URP's existing opaque/depth submission; no extra Feature, RT, blit or shadow request.
Shader "SlowRoads/Hills/Roadside"
{
    Properties
    {
        [Header(Hills Theme Lighting)]
        _ThemeLightingEnabled("Apply theme lighting",Float)=0
        _ThemeSun("Theme sun irradiance (linear)",Vector)=(0,0,0,0)
        _ThemeAmbient("Theme ambient irradiance (linear)",Vector)=(0,0,0,0)
        _ThemeHemisphere("Theme hemisphere irradiance (linear)",Vector)=(0,0,0,0)
        _ThemeReferenceSunIntensity("Theme reference sun intensity",Float)=1

        _BaseMap("Original atlas",2D)="white"{}
        _BaseColor("Original colour",Color)=(1,1,1,1)
        _Cutoff("Original alpha test",Float)=0
        _Emission("Original emission intensity",Float)=0
        _Flat("Original flat shading",Float)=0
        [HideInInspector] _Sign("Original sign lighting",Float)=0
        [HideInInspector] _SunIntensity("Source sun intensity adapter",Float)=0
        [HideInInspector] _SurfaceOnly("Surface evidence",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Back ZWrite On ZTest LEqual
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/Art/Weather/Shaders/EnvironmentSurface.hlsl"
        #include "HillsRoadside.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
#include "../Environment/HillsThemeMaterialFields.hlsl"
        float4 _BaseColor;float _Cutoff,_Emission,_Flat,_SurfaceOnly,_Sign,_SunIntensity;
        CBUFFER_END
#include "../Environment/HillsThemeMaterialFunctions.hlsl"
        struct A {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float3 view:TEXCOORD3;float4 fog:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID};
        V Vert(A i)
        {
            V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
            o.world=TransformObjectToWorld(i.positionOS);o.fog=HillsSurfaceFogVertex(o.world);float4 v=mul(UNITY_MATRIX_MV,float4(i.positionOS,1));o.view=v.xyz;o.positionCS=mul(UNITY_MATRIX_P,v);
            o.normal=TransformObjectToWorldNormal(i.normalOS);o.uv=i.uv;return o;
        }
        float4 Surface(V i){float4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;clip(c.a-_Cutoff);return c;}
        float4 Frag(V i):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);float4 c=Surface(i);if(_SurfaceOnly>.5)return c;
            float3 n=normalize(i.normal);if(_Flat>.5)n=mul(normalize(cross(ddy(i.view),ddx(i.view))),(float3x3)UNITY_MATRIX_V);
            float3 v=GetWorldSpaceNormalizeViewDir(i.world);Light l=GetMainLight();float3 sourceLightDirection=HillsMaterialLightDirection(l.direction),sourceLightColour=HillsMaterialLightColour(l.color);float nl=saturate(dot(n,sourceLightDirection)),nv=saturate(dot(n,v));
            float vh=saturate(dot(v,normalize(sourceLightDirection+v))),f=exp2((-5.55473*vh-6.98316)*vh);
            float specular=(.04*(1-f)+f)*(.5/max(nl+nv,1e-6));
            // Same restricted Three r155 standard BRDF adapter as C3; whole-scene
            // source lighting/fog remains a C7 input, not a new material keyword.
            return float4(HillsSurfaceFog(c.rgb*(HillsMaterialAmbientColour(n,SampleSH(n))+_Emission+_Sign*HillsSignEmission(_ThemeLightingEnabled>.5?_ThemeReferenceSunIntensity:_SunIntensity))+nl*sourceLightColour*l.distanceAttenuation*(c.rgb+specular),i.world,i.fog),1);
        }
        half4 Depth(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);Surface(i);return 0;}
        ENDHLSL
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Depth
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
