// S1 URP lighting adapter for the original S2/k2/T0/K4/ii surfaces.
// Keywords: instancing only (2 variants/pass). Type, alpha clip and object-space normals are uniforms.
// Original procedural meshes do not cast realtime shadows; Forward and DepthOnly have identical clip rules.
Shader "SlowRoads/Hills/Structure"
{
    Properties
    {
        _BaseMap("Reference diffuse",2D)="white"{}
        _ObjectNormal("Reference object-space normal",2D)="bump"{}
        _BaseColor("Reference colour",Color)=(1,1,1,1)
        _Cutoff("Reference alpha test",Float)=0
        _ObjectNormalEnabled("Object-space normal",Float)=0
        _Flat("Flat shading",Float)=1
        _ShadowEnabled("Original Ce shadow patch",Float)=1
        _ShadowFactor("Original shadow factor",Float)=.9
        [HideInInspector] _SurfaceOnly("Surface validation",Float)=0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull [_Cull] ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
        TEXTURE2D(_ObjectNormal);SAMPLER(sampler_ObjectNormal);
        CBUFFER_START(UnityPerMaterial)
        // Preserve source highp colour and BRDF intermediates; final depth uses half.
        float4 _BaseMap_ST,_BaseColor;
        float _Cutoff,_ObjectNormalEnabled,_Flat,_ShadowEnabled,_ShadowFactor,_Cull,_SurfaceOnly;
        CBUFFER_END
        struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float shadow:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;float shadow:TEXCOORD3;float3 positionVS:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
        Varyings Vert(Attributes i)
        {
            Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS=TransformObjectToWorld(i.positionOS);
            // Match the source's projection * modelView evaluation order. Combining
            // VP first shifts high-frequency repeated UVs near texel boundaries.
            float4 viewPosition=mul(UNITY_MATRIX_MV,float4(i.positionOS,1));
            float4 projectedPosition=mul(UNITY_MATRIX_P,viewPosition);
            o.positionVS=viewPosition.xyz;o.positionCS=projectedPosition;
            o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.uv=TRANSFORM_TEX(i.uv,_BaseMap);o.shadow=i.shadow*_ShadowFactor*_ShadowEnabled;return o;
        }
        float4 Surface(Varyings i){float4 sampled=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);float alpha=sampled.a*_BaseColor.a;clip(alpha-_Cutoff);return float4(sampled.rgb*_BaseColor.rgb,alpha);}
        float4 Forward(Varyings i,FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);float4 c=Surface(i);
            float3 normal=normalize(i.normalWS);
            // Three normal_fragment_begin: flat derivatives already face the viewer.
            // Only smooth/object-space map normals receive the double-sided sign.
            if(_Flat>.5)normal=mul(normalize(cross(ddy(i.positionVS),ddx(i.positionVS))),(float3x3)UNITY_MATRIX_V);
            else normal*=IS_FRONT_VFACE(face,1,-1);
            if(_ObjectNormalEnabled>.5)normal=TransformObjectToWorldNormal(SAMPLE_TEXTURE2D(_ObjectNormal,sampler_ObjectNormal,i.uv).xyz*2-1)*IS_FRONT_VFACE(face,1,-1);
            float3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);
            Light light=GetMainLight();
            float3 lightDirection=light.direction,lightColour=light.color*light.distanceAttenuation;
            float nl=saturate(dot(normal,lightDirection)),nv=saturate(dot(normal,view));
            float vh=saturate(dot(view,normalize(lightDirection+view)));
            float direct=1-min(1,i.shadow),ambient=lerp(1,1.75-min(1,max(.75,i.shadow)),_ShadowEnabled);
            float fresnel=exp2((-5.55473*vh-6.98316)*vh);
            float specular=(.04*(1-fresnel)+fresnel)*(.5/max(nl+nv,1e-6));
            if(_SurfaceOnly>14.5)return float4(c.rgb,1);
            if(_SurfaceOnly>13.5)return float4(0,0,0,1);
            if(_SurfaceOnly>12.5)return float4(c.rgb*SampleSH(normal)*ambient,1);
            if(_SurfaceOnly>11.5)return float4(nl*lightColour*direct*specular,1);
            if(_SurfaceOnly>10.5)return float4(nl*lightColour*direct*c.rgb,1);
            if(_SurfaceOnly>.5){if(_SurfaceOnly>9.5)return float4(.04,1,1,1);if(_SurfaceOnly>8.5)return float4(nl,nv,vh,1);if(_SurfaceOnly>7.5)return float4(view,1);if(_SurfaceOnly>6.5)return float4(IS_FRONT_VFACE(face,1,0),0,0,1);if(_SurfaceOnly>5.5)return float4(normal,1);if(_SurfaceOnly>4.5)return float4(i.uv,0,1);if(_SurfaceOnly>3.5)return SAMPLE_TEXTURE2D(_ObjectNormal,sampler_ObjectNormal,i.uv);if(_SurfaceOnly<1.5)return half4(c.rgb,1);if(_SurfaceOnly<2.5)return half4(normal*.5+.5,1);return half4(direct,ambient,c.a,1);}
            // Archived Three r155: metalness=0, roughness=1. In this restricted
            // material domain D_GGX=1/pi and V=.5/max(NL+NV,EPSILON).
            // Three keeps the full dielectric diffuse; URP's .96 attenuation and
            // roughness approximation are not equivalent. URP light/SH inputs
            // carry the 1/pi normalization here. No source environment map exists.
            float3 colour=c.rgb*SampleSH(normal)*ambient+nl*lightColour*direct*(c.rgb+specular);
            return float4(colour,1);
        }
        half4 Depth(Varyings i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);Surface(i);return 0;}
        ENDHLSL
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Forward
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
