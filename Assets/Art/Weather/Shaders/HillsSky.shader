Shader "SlowRoads/HillsSky"
{
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Background"}
        Pass
        {
            Name "Sky"
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // Only instancing variants; environment choices are uniforms. No shadow/depth passes.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HillsAtmosphere.hlsl"
            struct A{float3 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float4 fog:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
            V Vert(A i)
            {
                V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
                o.world=TransformObjectToWorld(i.positionOS);o.positionCS=TransformWorldToHClip(o.world);
                o.fog=HillsFogVertex(o.world,_WorldSpaceCameraPos,false);return o;
            }
            float4 Frag(V i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return float4(HillsFogFragment(float3(1,1,1),i.world,_WorldSpaceCameraPos,i.fog),1);
            }
            ENDHLSL
        }
    }
}
