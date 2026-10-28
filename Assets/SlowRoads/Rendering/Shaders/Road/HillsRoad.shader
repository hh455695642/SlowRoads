Shader "SlowRoads/Hills/Road"
{
    Properties
    {
        _BaseMap("Base",2D)="white"{} _DashedMap("Dashed",2D)="black"{}
        _SolidMap("Solid",2D)="black"{} _SolidLeftMap("Solid left",2D)="black"{} _SolidRightMap("Solid right",2D)="black"{}
        _MergeLeftMap("Merge left",2D)="black"{} _MergeRightMap("Merge right",2D)="black"{} _OverlayMap("Season overlay",2D)="black"{}
        _BaseColor("Linear tint",Color)=(1,1,1,1)
        _TextureStretch("Base and overlay stretch",Range(1,2))=1
        _HasPaint("Paint",Float)=1 _HasOverlay("Overlay",Float)=0
        _ShadowFactor("Source shadow factor",Float)=1 _FresnelIntensity("Source fresnel",Float)=1
        _Radiance("Source radiance",Float)=0 _SurfaceOnly("Debug surface only",Float)=0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest"}
        Cull [_Cull] ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_DashedMap); SAMPLER(sampler_DashedMap);
        TEXTURE2D(_SolidMap); TEXTURE2D(_SolidLeftMap); TEXTURE2D(_SolidRightMap);
        TEXTURE2D(_MergeLeftMap); TEXTURE2D(_MergeRightMap); TEXTURE2D(_OverlayMap); SAMPLER(sampler_OverlayMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _TextureStretch,_HasPaint,_HasOverlay,_ShadowFactor,_FresnelIntensity,_Radiance,_SurfaceOnly,_Cull;
        CBUFFER_END
        struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 a:TEXCOORD1;float3 b:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 a:TEXCOORD1;float3 b:TEXCOORD2;
            float3 positionWS:TEXCOORD3;half3 normalWS:TEXCOORD4;float overlay:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
        Varyings Vert(Attributes v)
        {
            Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.positionWS=TransformObjectToWorld(v.positionOS);o.positionCS=TransformWorldToHClip(o.positionWS);
            o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=v.uv;o.a=v.a;o.b=v.b;
            o.a.y=v.a.y*_ShadowFactor;
            // Source saturates PER VERTEX, before interpolation (ue.vert).
            o.overlay=saturate(o.a.y*3.0);return o;
        }
        float2 RoadUv(float2 uv){return float2(uv.x,uv.y/_TextureStretch);}
        half4 Surface(Varyings i,out half3 emission)
        {
            half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,RoadUv(i.uv));base.a=max(i.a.x,base.a);
            // Alpha cutout is original MeshLambertMaterial alphaTest=.75, never transparent blending.
            clip(base.a-.75);emission=base.rgb*_Radiance;
            if(_HasPaint>.5)
            {
                half4 paint=SAMPLE_TEXTURE2D_BIAS(_DashedMap,sampler_DashedMap,i.uv,-.3);
                float s=smoothstep(.49,.51,i.a.z),l=smoothstep(.49,.51,i.a.w),r=smoothstep(.49,.51,i.b.x);
                float allow=1.0-ceil(max(l,r));
                float ml=smoothstep(.49,.51,i.b.y)*allow,mr=smoothstep(.49,.51,i.b.z)*allow;
                // Performance: ordinary dashed regions sample only base + dashed. Preserve source branch order.
                if(ml+mr>0){paint=lerp(paint,SAMPLE_TEXTURE2D_BIAS(_MergeLeftMap,sampler_DashedMap,i.uv,-.3),ml);
                    paint=lerp(paint,SAMPLE_TEXTURE2D_BIAS(_MergeRightMap,sampler_DashedMap,i.uv,-.3),mr);}
                if(r+l>0){paint=lerp(paint,SAMPLE_TEXTURE2D_BIAS(_SolidRightMap,sampler_DashedMap,i.uv,-.3),r);
                    paint=lerp(paint,SAMPLE_TEXTURE2D_BIAS(_SolidLeftMap,sampler_DashedMap,i.uv,-.3),l);
                    paint=lerp(paint,SAMPLE_TEXTURE2D_BIAS(_SolidMap,sampler_DashedMap,i.uv,-.3),s);}
                base.rgb=lerp(base.rgb,paint.rgb,paint.a);
            }
            if(_HasOverlay>.5&&i.overlay>0){half4 overlay=SAMPLE_TEXTURE2D(_OverlayMap,sampler_OverlayMap,RoadUv(i.uv));base.rgb=lerp(base.rgb,overlay.rgb,i.overlay*overlay.a);}
            return base*_BaseColor;
        }
        half4 Frag(Varyings i):SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(i);half3 emission;half4 surface=Surface(i,emission);
            if(_SurfaceOnly>.5)return surface;
            half3 normal=normalize(i.normalWS);float f=1.0-max(0.0,dot(normalize(GetCameraPositionWS()-i.positionWS),normal));
            float light=max(0.0,f-.75)*4.0;light=light*light*(1.0-i.a.y)*_FresnelIntensity;
            float dark=(1.0-min(1.0,f*1.3333))*_FresnelIntensity;dark=.25+min(.25,max(0.0,dark-.25)*.5);
            surface.rgb*=1.0+light-dark;
            // URP adapter for the source Lambert sun/ambient. No extra light/shadow variants or shadow RT reads.
            Light sun=GetMainLight();half3 direct=sun.color*saturate(dot(normal,sun.direction))*(1.0-min(1.0,i.a.y));
            half3 ambient=max(0,SampleSH(normal))*(1.75-min(1.0,max(.75,i.a.y)));
            return half4(surface.rgb*(direct*(1.0+emission)+ambient),surface.a);
        }
        half4 DepthFrag(Varyings i):SV_Target
        {UNITY_SETUP_INSTANCE_ID(i);half a=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,RoadUv(i.uv)).a;clip(max(i.a.x,a)-.75);return 0;}
        ENDHLSL
        Pass
        {
            Name "Forward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
