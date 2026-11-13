Shader "SlowRoads/Environment Surface Example"
{
    Properties{_BaseColor("Base colour",Color)=(.3,.5,.2,1)}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
        Pass
        {
            Name "Forward" Tags{"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "EnvironmentSurface.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
            struct A{float3 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;half3 n:TEXCOORD1;float4 fog:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
            V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);o.w=TransformObjectToWorld(i.p);o.p=TransformWorldToHClip(o.w);o.n=TransformObjectToWorldNormal(i.n);o.fog=HillsSurfaceFogVertex(o.w);return o;}
            half4 Frag(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);half3 n=normalize(i.n);half3 lighting=SampleSH(n)+GetMainLight().color*saturate(dot(n,GetMainLight().direction));return half4(HillsSurfaceFog(_BaseColor.rgb*lighting,i.w,i.fog),1);}
            ENDHLSL
        }
    }
}
