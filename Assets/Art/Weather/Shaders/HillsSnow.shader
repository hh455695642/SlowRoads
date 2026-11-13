Shader "SlowRoads/HillsSnow"
{
 Properties {_SnowMap("Original snow particle",2D)="white"{} _SnowNoise("Original linear noise",2D)="gray"{}}
 SubShader {Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout"}
 Pass {
 Name "Snow"
 // Original PointsMaterial is opaque alpha-test, fog=false, depthTest/write=true.
 Blend Off ZWrite On ZTest LEqual Cull Off
 HLSLPROGRAM
 #pragma target 4.5
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "HillsSnow.hlsl"
 TEXTURE2D(_SnowMap);SAMPLER(sampler_Trilinear_Repeat);
 StructuredBuffer<HillsSnowPrepared> _SnowPrepared;
 struct A{float3 position:POSITION;float2 uv:TEXCOORD0;uint instance:SV_InstanceID;};
 struct V{float4 position:SV_POSITION;float2 uv:TEXCOORD0;};
 V Vert(A i)
 {
    HillsSnowPrepared p=_SnowPrepared[i.instance];V o;o.uv=i.uv;
    o.position=p.clip;o.position.xy+=i.position.xy*p.info.x/_SnowScreen.xy*p.clip.w;
    if(p.info.y<.5)o.position=float4(2,2,2,1);return o;
 }
 float4 Frag(V i):SV_Target {float alpha=PLATFORM_SAMPLE_TEXTURE2D(_SnowMap,sampler_Trilinear_Repeat,i.uv).a;clip(alpha-.1);return float4(_SnowColour.rgb,1);}
 ENDHLSL
 }}
}
