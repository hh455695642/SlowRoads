Shader "SlowRoads/Hills/Water"
{
 Properties {
 _BaseColor("Original base",Color)=(.03,.09,.07,1)
 _BodyColor("Original body",Color)=(.12,.2,.16,1)
 _HighlightColor("Original highlight",Color)=(.3,.4,.3,1)
 _Waves("Original sea_waves red",2D)="gray"{}
 _DepthEnabled("Original WATER_DEPTH",Float)=1
 _WavesEnabled("Original WATER_WAVES",Float)=1
 [HideInInspector] _PhaseA("World/time 200",Vector)=(0,0,0,0)
 [HideInInspector] _PhaseB("World/time 800",Vector)=(0,0,0,0)
 [HideInInspector] _PhaseC("World/time 300",Vector)=(0,0,0,0)
 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Assets/Art/Weather/Shaders/EnvironmentSurface.hlsl"
 TEXTURE2D(_Waves);SAMPLER(sampler_Waves);
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor,_BodyColor,_HighlightColor,_PhaseA,_PhaseB,_PhaseC;
 float _DepthEnabled,_WavesEnabled;
 CBUFFER_END
 struct A {float3 positionOS:POSITION;float depth:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 a:TEXCOORD1;float2 b:TEXCOORD2;float2 c:TEXCOORD3;float depth:TEXCOORD4;float4 fog:TEXCOORD5;UNITY_VERTEX_INPUT_INSTANCE_ID};
 // Ro computes wave UVs per vertex. Preserve that interpolation boundary.
 V Vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);o.positionCS=TransformObjectToHClip(i.positionOS);o.world=TransformObjectToWorld(i.positionOS);o.a=_PhaseA.xy+i.positionOS.xz/200;o.b=_PhaseB.xy+i.positionOS.xz/800;o.c=_PhaseC.xy+i.positionOS.xz/300;o.depth=i.depth;o.fog=HillsSurfaceFogVertex(o.world);return o;}
 void ClipWater(V i){if(_DepthEnabled>.5)clip(10-i.depth);}
 // Original Io/Ro/Eo/To. Unlit opaque; no screen depth, vertex waves, reflection RT or extra render feature.
 float4 Frag(V i):SV_Target {
 UNITY_SETUP_INSTANCE_ID(i);ClipWater(i);
 float3 baseColour=_BaseColor.rgb,bodyColour=_BodyColor.rgb,highlightColour=_HighlightColor.rgb;
 float sea=dot(normalize(_WorldSpaceCameraPos-i.world),float3(0,1,0));
 if(_WavesEnabled>.5||_DepthEnabled>.5){
 float2 a=i.a,b=i.b,c=i.c;
 float w1=SAMPLE_TEXTURE2D(_Waves,sampler_Waves,a).r+.1,w2=SAMPLE_TEXTURE2D(_Waves,sampler_Waves,b).r+.1;
 float w4=SAMPLE_TEXTURE2D(_Waves,sampler_Waves,c).r+.1,w5=SAMPLE_TEXTURE2D(_Waves,sampler_Waves,b-a).r+.1;
 sea=saturate(sea+w4*w5*.5-w1*w2);}
 float factor=1-sea;float4 colour=float4(lerp(baseColour,bodyColour,factor*factor),1);
 if(_DepthEnabled>.5&&i.depth<0)colour*=1-min(1,i.depth/-15)*.3;
 if(_WavesEnabled>.5||_DepthEnabled>.5){if(_DepthEnabled>.5&&i.depth>-5)sea*=.7+.3*(i.depth/-5);float sun=sea<.125?1-sea*8:0;colour.rgb=lerp(colour.rgb,highlightColour,sun*sun);}
 colour.rgb=HillsSurfaceFog(colour.rgb,i.world,i.fog);return colour;}
 float4 Depth(V i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);ClipWater(i);return 0;}
 ENDHLSL
 Pass {
 Name "WaterForward" Tags {"LightMode"="UniversalForwardOnly"}
 Blend Off ZWrite On ZTest LEqual Cull Back
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 ENDHLSL
 }
 Pass {
 Name "DepthOnly" Tags {"LightMode"="DepthOnly"}
 ZWrite On ZTest LEqual Cull Back ColorMask 0
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex Vert
 #pragma fragment Depth
 #pragma multi_compile_instancing
 ENDHLSL
 }
 }
}
