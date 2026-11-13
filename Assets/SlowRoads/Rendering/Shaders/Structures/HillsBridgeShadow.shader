// Original Hills us: black, opacity .4, transparent projected bridge geometry at Y=.01.
Shader "SlowRoads/Hills/BridgeShadow"
{
    Properties {_Opacity("Original opacity",Float)=.4}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Name "BridgeProjection" Tags {"LightMode"="UniversalForward"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Art/Weather/Shaders/EnvironmentSurface.hlsl"
            CBUFFER_START(UnityPerMaterial) half _Opacity; CBUFFER_END
            struct Attributes {float3 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float4 fog:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            Varyings Vert(Attributes i){Varyings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);o.positionCS=TransformObjectToHClip(i.positionOS);o.world=TransformObjectToWorld(i.positionOS);o.fog=HillsSurfaceFogVertex(o.world);return o;}
            half4 Frag(Varyings i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);return half4(HillsSurfaceFog(float3(0,0,0),i.world,i.fog),_Opacity);}
            ENDHLSL
        }
    }
}
