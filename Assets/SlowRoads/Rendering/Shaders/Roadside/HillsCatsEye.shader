// x7 n7/d7. URP adapter replaces nonportable hardware point sprites with bounded
// camera-facing quads. One transparent pass, instancing only (2 variants), no RT/depth prepass.
Shader "SlowRoads/Hills/CatsEye"
{
    Properties {_BaseColor("Original colour",Color)=(1,1,1,1) _PointSize("Original point size",Float)=.06}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-10"}
        Pass
        {
            Name "Reflector" Tags {"LightMode"="UniversalForward"}
            // Three Material defaults retain depthWrite=true even for this transparent
            // PointsMaterial. Preserve that behavior, including zero-brightness points.
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha ZWrite On ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "HillsCatsEye.hlsl"
            #include "../Environment/HillsSurfaceEnvironment.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _PointSize;
            CBUFFER_END
            float4 _ReflectorPosition,_ReflectorDirection;float _ReflectorEnabled;
            struct A {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 corner:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float brightness:TEXCOORD0;float3 world:TEXCOORD1;float4 fog:TEXCOORD2;UNITY_VERTEX_INPUT_INSTANCE_ID};
            V Vert(A i)
            {
                V o=(V)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);float3 world=TransformObjectToWorld(i.positionOS);
                o.positionCS=TransformWorldToHClip(world);o.world=world;o.fog=HillsSurfaceFogVertex(world);
                // Three PointMaterial sizeAttenuation: size * (height/2) / -viewZ.
                // Clip w cancels depth; x includes viewport aspect for square pixels.
                o.positionCS.xy+=i.corner*(_PointSize*.5)*float2(_ScreenParams.y/_ScreenParams.x,1);
                o.brightness=HillsReflectorBrightness(world,TransformObjectToWorldNormal(i.normalOS),_ReflectorPosition.xyz,_ReflectorDirection.xyz)*_ReflectorEnabled;return o;
            }
            float4 Frag(V i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);return float4(HillsSurfaceFog(_BaseColor.rgb,i.world,i.fog),i.brightness);}
            ENDHLSL
        }
    }
}
