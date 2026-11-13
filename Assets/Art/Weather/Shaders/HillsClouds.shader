Shader "SlowRoads/HillsClouds"
{
    Properties{_CloudMap("Original cloud map (linear)",2D)="white"{}}
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Background"}
        Pass
        {
            Name "Clouds"
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HillsCloudVert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            // Two instancing variants only; original cloud modes and weather are uniforms.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define HILLS_CLOUD_ORIGINAL_SAMPLER 1
            #include "HillsCloudsVertex.hlsl"
            float4 Frag(HillsCloudVaryings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return HillsCloudFragment(i.uv,i.uv01,i.uv2Dist.xy,i.uv2Dist.z,i.world,_WorldSpaceCameraPos,i.fog);
            }
            ENDHLSL
        }
    }
}
