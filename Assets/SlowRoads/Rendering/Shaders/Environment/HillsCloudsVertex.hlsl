#ifndef HILLS_CLOUDS_VERTEX_INCLUDED
#define HILLS_CLOUDS_VERTEX_INCLUDED
#include "HillsClouds.hlsl"
float4x4 _HillsCloudModelView;
struct HillsCloudAttributes{float3 positionOS:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct HillsCloudVaryings{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 uv01:TEXCOORD1;float3 uv2Dist:TEXCOORD2;float3 world:TEXCOORD3;float4 fog:TEXCOORD4;UNITY_VERTEX_INPUT_INSTANCE_ID};
HillsCloudVaryings HillsCloudVert(HillsCloudAttributes i)
{
    HillsCloudVaryings o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
    HillsCloudVertexData v=HillsCloudVertex(i.positionOS,i.uv,GetObjectToWorldMatrix(),_WorldSpaceCameraPos);
    o.positionCS=mul(UNITY_MATRIX_P,mul(_HillsCloudModelView,float4(v.transformed,1)));
    o.uv=i.uv;o.uv01=v.uv01;o.uv2Dist=float3(v.uv2,v.dist);o.world=v.world;o.fog=v.fog;return o;
}
#endif
