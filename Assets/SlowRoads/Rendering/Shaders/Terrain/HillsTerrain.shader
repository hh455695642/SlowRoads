Shader "SlowRoads/Hills/Terrain" {Properties {grassMap("grassMap",2D)="white"{}
sandMap("sandMap",2D)="white"{}
rockMap("rockMap",2D)="white"{}
rockMapBump("rockMapBump",2D)="white"{}
gravelMap("gravelMap",2D)="white"{}
forestMap("forestMap",2D)="white"{}
heatherMap("heatherMap",2D)="white"{}
detailNearMap("detailNearMap",2D)="white"{}
detailFarMap("detailFarMap",2D)="white"{}
grassVariationMap("grassVariationMap",2D)="white"{}
offroadMap("offroadMap",2D)="white"{}
fadeFine("fadeFine",2D)="white"{}
displacementMap("displacementMap",2D)="white"{}
roadCol("roadCol",Vector)=(1,1,1,1)
grassColA("grassColA",Vector)=(1,1,1,1)
grassColB("grassColB",Vector)=(1,1,1,1)
peakColA("peakColA",Vector)=(1,1,1,1)
peakColB("peakColB",Vector)=(1,1,1,1)
fieldDiscolouration("fieldDiscolouration",Vector)=(1,1,1,1)
 seasonIndex("Season",Float)=0 offroad("Offroad",Float)=0 greenlane("Green lane",Float)=0
 sinkDist("Source sink range",Float)=400 shadowFactor("Shadow strength",Float)=.9 radiance("Radiance",Float)=0 fresnelIntensity("Fresnel",Float)=.6
 _SourceOrigin("Reference texture origin",Vector)=(4020,8240,0,0)
 [HideInInspector] _RenderOrigin("Render origin",Vector)=(0,0,0,0)
 [HideInInspector] vehicleIndex("Observer progress",Float)=0
 [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
 _SurfaceOnly("Surface debug",Float)=0}
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"} Cull [_Cull] ZWrite On
HLSLINCLUDE
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
TEXTURE2D(grassMap); SAMPLER(sampler_grassMap);
TEXTURE2D(sandMap); SAMPLER(sampler_sandMap);
TEXTURE2D(rockMap); SAMPLER(sampler_rockMap);
TEXTURE2D(rockMapBump); SAMPLER(sampler_rockMapBump);
TEXTURE2D(gravelMap); SAMPLER(sampler_gravelMap);
TEXTURE2D(forestMap); SAMPLER(sampler_forestMap);
TEXTURE2D(heatherMap); SAMPLER(sampler_heatherMap);
TEXTURE2D(detailNearMap); SAMPLER(sampler_detailNearMap);
TEXTURE2D(detailFarMap); SAMPLER(sampler_detailFarMap);
TEXTURE2D(grassVariationMap); SAMPLER(sampler_grassVariationMap);
TEXTURE2D(offroadMap); SAMPLER(sampler_offroadMap);
TEXTURE2D(fadeFine); SAMPLER(sampler_fadeFine);
TEXTURE2D(displacementMap); SAMPLER(sampler_displacementMap);
CBUFFER_START(UnityPerMaterial)
float4 roadCol;
float4 grassColA;
float4 grassColB;
float4 peakColA;
float4 peakColB;
float4 fieldDiscolouration;
float4 _SourceOrigin,_RenderOrigin;
float seasonIndex,offroad,greenlane,sinkDist,shadowFactor,radiance,fresnelIntensity,vehicleIndex,_SurfaceOnly,_Cull;
CBUFFER_END
float4 SurfaceSample_grassMap(float2 uv){return SAMPLE_TEXTURE2D(grassMap,sampler_grassMap,uv);}
float4 SurfaceSample_sandMap(float2 uv){return SAMPLE_TEXTURE2D(sandMap,sampler_sandMap,uv);}
float4 SurfaceSample_rockMap(float2 uv){return SAMPLE_TEXTURE2D(rockMap,sampler_rockMap,uv);}
float4 SurfaceSample_rockMapBump(float2 uv){return SAMPLE_TEXTURE2D(rockMapBump,sampler_rockMapBump,uv);}
float4 SurfaceSample_gravelMap(float2 uv){return SAMPLE_TEXTURE2D(gravelMap,sampler_gravelMap,uv);}
float4 SurfaceSample_forestMap(float2 uv){return SAMPLE_TEXTURE2D(forestMap,sampler_forestMap,uv);}
float4 SurfaceSample_heatherMap(float2 uv){return SAMPLE_TEXTURE2D(heatherMap,sampler_heatherMap,uv);}
float4 SurfaceSample_detailNearMap(float2 uv){return SAMPLE_TEXTURE2D(detailNearMap,sampler_detailNearMap,uv);}
float4 SurfaceSample_detailFarMap(float2 uv){return SAMPLE_TEXTURE2D(detailFarMap,sampler_detailFarMap,uv);}
float4 SurfaceSample_grassVariationMap(float2 uv){return SAMPLE_TEXTURE2D(grassVariationMap,sampler_grassVariationMap,uv);}
float4 SurfaceSample_offroadMap(float2 uv){return SAMPLE_TEXTURE2D(offroadMap,sampler_offroadMap,uv);}
float4 SurfaceSample_fadeFine(float2 uv){return SAMPLE_TEXTURE2D(fadeFine,sampler_fadeFine,uv);}
float4 SurfaceSample_displacementMap(float2 uv){return SAMPLE_TEXTURE2D(displacementMap,sampler_displacementMap,uv);}
float4 VertexSample_fadeFine(float2 uv){return SAMPLE_TEXTURE2D_LOD(fadeFine,sampler_fadeFine,uv,0);}
float4 VertexSample_displacementMap(float2 uv){return SAMPLE_TEXTURE2D_LOD(displacementMap,sampler_displacementMap,uv,0);}
float screen(float a,float b){return 1.0-((1.0-a)*(1.0-b));}
float4 terrainBlend(float4 tx1, float w1, float4 tx2, float w2, float depth) {

  // Perform the blending

  float ma = max(tx1.a + w1, tx2.a + w2) - depth;
  float b1 = max(tx1.a + w1 - ma, 0.0);
  float b2 = max(tx2.a + w2 - ma, 0.0);

  return ((tx1 * b1) + (tx2 * b2)) / (b1 + b2);
}


struct Attributes {float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 roadUv:TEXCOORD0;float3 surface:TEXCOORD1;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct Varyings {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normalWS:TEXCOORD1;float4 uv:TEXCOORD2;float4 wuv:TEXCOORD3;float4 surface:TEXCOORD4;float4 weights:TEXCOORD5;float4 extras:TEXCOORD6;float2 roadUv:TEXCOORD7;UNITY_VERTEX_INPUT_INSTANCE_ID};
Varyings Vert(Attributes i){Varyings o=(Varyings)0;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);
float3 transformed=i.positionOS;float roadProximity=i.surface.x,overlap=i.surface.z;
// Geometry-only stage: original tree/field providers are deliberately absent.
float treeMask=0,fieldCol=0;
float height,steepness,roadProx,hasRoadProx,vTreeMask,vLightGrass,vDarkGrass,vField,vHeather,vShadow,vCamDepth;
float2 vUv,vUUv,vWv,vWWv,vRoadUv;float3 vFresnelPos,vFresnelNorm;float wuvSize=500;
float2 roadUv=i.roadUv;


float4 wPos = float4(TransformObjectToWorld(i.positionOS)+_RenderOrigin.xyz,1);

// ORIGIN OFFSET HARD-CODED FOR NOW
// ALSO NEED TO HANDLE CAMERA OFFSET IN FRESNEL FRAG BELOW
// AND THE SAME THING IN GRASS SHADER
wPos.x -= _SourceOrigin.x;
wPos.z -= _SourceOrigin.y;

vFresnelPos = wPos.xyz;
vFresnelNorm = normalize(TransformObjectToWorldNormal(i.normalOS));

height = wPos.y;

// Set UVs from world pos
vUv.x = wPos.x / 10.0;
vUv.y = wPos.z / 10.0;

vUUv.x = vUv.x / 10.0;
vUUv.y = vUv.y / 10.0;

// 0 is perfectly flat, 1 is 90 degrees
steepness = 0.98 - dot(i.normalOS, float3(0,1,0));
steepness = clamp(steepness * 1.4, 0.0, 1.0); // was multiplied by 2 before..?

vWv.x = wPos.x / wuvSize;
vWv.y = wPos.z / wuvSize;

vWWv.x = vWv.x / 8.0;
vWWv.y = vWv.y / 8.0;

vRoadUv = roadUv;

//// GET FADES

float fade0 = VertexSample_fadeFine( vWv).r;
float fade1 = VertexSample_fadeFine( vWWv).r;

// Reduce  vWWv for more utility in the fragment shader
vWWv *= 2.0;
// Same for uvs? Does this create issues for the origin offset?
vUv *= 1.4;

//// HEATHER

vHeather = smoothstep(0.4, 1.0, ((height + fade1 * 350.0) - 60.0) / 500.0);

vHeather *= screen(fade1, fade0);

vHeather = smoothstep(0.3, 0.6, vHeather);


//// GRASSES

float heightVal = min(1.0, max(0.0, (height - (30.0 + fade1 * 40.0))/150.0));

vLightGrass = min(1.0, heightVal * (fade0 + heightVal * 0.5));

// Keep it green close to road?
if(roadProximity > 0.0 && roadProximity < 0.5 + fade0) {

  vHeather *= roadProximity / (0.5 + fade0);

  if(seasonIndex < 3) {
    vLightGrass *= roadProximity / (0.5 + fade0);
  }
}  

vDarkGrass = min(max(0.0,(fade1 - 0.25) * 2.0), 1.0);

vField = fieldCol;

//// ROAD PROX

roadProx = roadProximity;
// Not safe but sets a flag for the frag shader to exit road-relative shading
hasRoadProx = step(0.00001, abs(roadProximity));

// Prevent rocks forming below the road, e.g. at bridges.
float underRoadFactor = 1.0 - step(0.000001, -roadProx);
steepness *= underRoadFactor;
vHeather *= underRoadFactor;

// if(roadProx < 0.0) {
//   steepness = 0.0;
//   vHeather = 0.0;
// }


//// SHADOW

vShadow = min(1.0, treeMask * min(1.0, max(0.0, height / 4.0)));


//// TREE MASK

vTreeMask = vShadow;

if(vTreeMask > 0.1) {
  vTreeMask = max(0.2, vTreeMask);
} else {
  vTreeMask = 0.0;
}

// Road-dependent factors
if(hasRoadProx > 0.5) {

  vTreeMask *= clamp(roadProx / 2.0, 0.0, 1.0);

  float intensity = clamp((16.0 - roadProx) / 6.0, 0.0, 1.0);

  intensity *= 0.8; // Scale it a bit

  transformed += normalize(i.normalOS) * ( (VertexSample_displacementMap( vUUv ).x - 0.4) * min(1.0, steepness * 3.0) ) * intensity;

}

//// MV POSITION

float4 mvPosition = mul(UNITY_MATRIX_V,float4(TransformObjectToWorld(transformed),1));

//// OVERLAP SINK

// TODO Use actual distance probably?

//// Curvature
// float ddist = -mvPosition.z;
// float ssink = ddist / 1000.0;
// ssink *= ssink;
// transformed.y -= ssink * 50.0;
// // Recalc mvPosition
//   mvPosition = mul(UNITY_MATRIX_V,float4(TransformObjectToWorld(transformed),1));

if(overlap > 0.0) {

    float overlapDist = overlap - vehicleIndex;

    if(overlapDist > 0.0) {

      if(overlapDist < sinkDist) {

          float halfSinkDist = sinkDist * 0.75;

          if(overlapDist < halfSinkDist) {
            transformed.y -= 20.0;
          } else {
            transformed.y -= 20.0 * (sinkDist - overlapDist) / (sinkDist - halfSinkDist);
          }
      }

    } else {

      float fSinkDist = -sinkDist/2.0;

      if(overlapDist > fSinkDist) {

        float halfSinkDist = fSinkDist / 2.0;

        if(overlapDist > halfSinkDist) {
          transformed.y -= 20.0;
        } else {
          transformed.y -= 20.0 * (fSinkDist - overlapDist) / halfSinkDist;
        }

      }

    }

    // Recalc mvPosition
    mvPosition = mul(UNITY_MATRIX_V,float4(TransformObjectToWorld(transformed),1));
}

o.positionCS = TransformObjectToHClip(transformed);

vCamDepth = -mvPosition.z;

// vLightGrass = 0.0;
// vDarkGrass = 0.0;

vShadow *= shadowFactor;


o.world=vFresnelPos;o.normalWS=vFresnelNorm;o.uv=float4(vUv,vUUv);o.wuv=float4(vWv,vWWv);
o.surface=float4(height,steepness,roadProx,hasRoadProx);o.weights=float4(vLightGrass,vDarkGrass,vHeather,vCamDepth);
o.extras=float4(vTreeMask,vShadow,vField,0);o.roadUv=vRoadUv;return o;}

half4 Frag(Varyings i):SV_Target {UNITY_SETUP_INSTANCE_ID(i);
float height=i.surface.x,steepness=i.surface.y,roadProx=i.surface.z,hasRoadProx=i.surface.w;
float vLightGrass=i.weights.x,vDarkGrass=i.weights.y,vHeather=i.weights.z,vCamDepth=i.weights.w;
float vTreeMask=i.extras.x,vShadow=i.extras.y,vField=i.extras.z;
float2 vUv=i.uv.xy,vUUv=i.uv.zw,vWv=i.wuv.xy,vWWv=i.wuv.zw,vRoadUv=i.roadUv;
float3 vFresnelPos=i.world,vFresnelNorm=normalize(i.normalWS);
float4 diffuseColor=1,rockTex=0;float3 totalEmissiveRadiance=0;


float fade0 = SurfaceSample_fadeFine( vUv).r;
float fade1 = SurfaceSample_fadeFine( vUUv).r;
float fade2 = SurfaceSample_fadeFine( vWv).r;

//// PREP MAP

float4 texelColor = SurfaceSample_grassMap( vUv ); 

//// DETAIL TEXTURING

float blendVal  = (1.0 - SurfaceSample_detailFarMap( vWWv ).r);
float blendValB = (1.0 - SurfaceSample_detailFarMap( vWv ).r);
float blendValC = (1.0 - SurfaceSample_detailFarMap( vUUv).r);


blendValB = screen(blendValC, blendValB);
blendValB *= min(1.0, blendVal * 10.0);

// Reduce on winter?
if(seasonIndex == 3) {
  blendValB *= 0.7;
}

// Elevation alteration to large-scale blend
blendVal *= (0.6 + 0.4 * vLightGrass);

// Enhance the distant intensity relative to medium/close range
blendVal *= 2.0;

// Blend from large-scale to medium-scale
float depthLerpFar = saturate(vCamDepth / 800.0);
blendVal = blendVal * depthLerpFar + (1.0 - depthLerpFar) * blendValB;

// Large-scale texture toned down for fields
float fieldFactor = ceil(abs(vField));
// blendVal *= 1.0 - fieldFactor * 0.25;

// Depth for far
blendVal *= saturate(0.4 + 0.6 * (vCamDepth / 300.0));

// Dotty noisy...
blendVal += (1.0 - SurfaceSample_detailNearMap( vUUv ).r ) * (0.7 + vLightGrass * 0.3);

//// GRASS COLOUR MIX

// Blend them into two colours

float variationVal = SurfaceSample_grassVariationMap( vWWv).r - 0.5;
float closeVariation = ((1.0 - SurfaceSample_grassVariationMap( vWv).r) - 0.5);

float grassBlend = (screen(fade1, fade0) + vDarkGrass) / 2.0;

// Square for more effect?
grassBlend = grassBlend * grassBlend;

grassBlend = saturate(grassBlend + variationVal + closeVariation);

float lightGrassFactor = saturate(vLightGrass + variationVal - closeVariation);

// Colour the grass tips versus base
lightGrassFactor = saturate(lightGrassFactor * 2.0 * texelColor.r);

float3 grassCol = lerp(
  lerp(grassColA.rgb, grassColB.rgb, grassBlend),
  lerp(peakColA.rgb, peakColB.rgb, grassBlend),
  lightGrassFactor
);

//// APPLY GRASS COLOUR

texelColor.rgb *= grassCol;

//// FIELDS

if(vField != 0.0) {

  // Rough lines using noise
  // Note that we step on vField for simplicity - should consider a separate varying for field mow dir though
  float fadeField = SurfaceSample_fadeFine(
    float2(
      vWv.x * 2.0 * step(0.001, vField),
      vWv.y * 2.0 * (1.0 - step(0.001, vField))
    )
  ).r;

  texelColor.rgb += vField * fieldDiscolouration.rgb * fadeField;

}

//// HEATHER


if(vHeather > 0.0) {

  float4 heatherCol = SurfaceSample_heatherMap( vUv);

  float depthLerp = 0.25 + 0.75 * min(1.0, vCamDepth / 400.0);
  heatherCol = lerp(heatherCol, SurfaceSample_heatherMap( vWv ), depthLerp);
  
  heatherCol = lerp(texelColor, heatherCol, min(1.0, vHeather));
  heatherCol.a = heatherCol.r;
  texelColor.a = texelColor.g;
  texelColor = terrainBlend(
      texelColor,
      1.0 - vHeather,
      heatherCol,
      vHeather,
      0.1
  );
  
}
  
// Apply detail texture
texelColor *= 1.0 - blendVal;


//// TREE MASK

float4 forestCol = SurfaceSample_forestMap( vUv / (1.25 - offroad * 0.25)); // larger scale looks better?
forestCol.a = forestCol.r * 2.0 * fade2;

float fTreeMask = min(1.0, vTreeMask);

if(fTreeMask > 0.0) {

  texelColor = terrainBlend(
    texelColor,
    1.0-fTreeMask,
    forestCol,
    fTreeMask,
    0.1 + (1.0 - fTreeMask) * fade0 * 0.9
  );

  texelColor.a = 1.0;
}


//// SAND BLEND

float vHeight = height + min(1.0, height+0.5) * (fade2 * 2.0 + (fade0 + fade2) * 0.5);

if(vHeight < 4.0 && seasonIndex < 3) {

  float dark = 1.0;

  if(vHeight < 0.0) {
    texelColor = SurfaceSample_sandMap( vUv) * 0.75;
  } else {

    if(vHeight < 2.0) {

      // Darken toward water
      dark = min(dark, 1.0 - (2.0 - vHeight) / 4.0);

    } else if(vHeight > 2.5) {

      // Darken into the grass
      dark = 1.0 - (vHeight-2.5) / 2.0;

    }

    // Lerp from grass to thingy
    float lerp = max(0.0, (vHeight - 1.0) / 3.0);

    float4 sandCol = SurfaceSample_sandMap( vUv);

    texelColor.a = texelColor.r;

    sandCol.a = sandCol.r;

    // Apply shading based on water/grass proximity
    sandCol.rgb *= dark;

    texelColor = terrainBlend(
      texelColor,
      lerp,
      sandCol,
      1.0 - lerp,
      0.05
    );

    // texelColor = lerp(SurfaceSample_sandMap( vUv) * dark, texelColor, height / 4.0);

    // texelColor = terrainBlend(
    //   SurfaceSample_sandMap( vUv) * dark,
    //   dark,
    //   texelColor,
    //   texelColor.g,
    //   height / 4.0
    // );
  }
}

//// ROADSIDE GRAVEL

if((seasonIndex < 3 || offroad > 0.5) && hasRoadProx > 0.5) {

  if(offroad < 0.5 && roadProx < -0.2 && (vCamDepth > 100.0 || seasonIndex == 1)) {

    texelColor.rgb = roadCol.rgb;

  } else if(roadProx + fade2 * 0.1 < 0.5 * fade0) {

    float rp = saturate(1.0 - (roadProx + fade2 * 0.1) / (0.5 * fade0));    

    //// GREENLANE CENTER
    // NOTE - could inject this at attribute level instead?
    
    if(greenlane > 0.5) {
      if(roadProx < -0.7) { // Lower values = narrower strip in middle
        if(roadProx < -1.1) {
          rp = 0.0;
        } else {
          // needs to be 0 when -1.2, 1 when -0.8
          rp = 1.0 - (-roadProx - 0.7) / 0.4;
        }
      }
    } else if(offroad > 0.5) {

      // Off-road a bit noisier along grass edges
      rp = saturate((1.0 * fade0) - (roadProx + fade2 * 0.1) / (1.0 * fade0));    

    }

    // Mix into forest colour if we're under trees
    if(offroad > 0.5) {

      float edgeness = 1.0 - saturate(-roadProx/(2.0 - greenlane));

      float4 gravelCol = SurfaceSample_gravelMap( vUv) * (0.75 + rp * rp * 0.75);

      // texelColor.rgb = float3(rp);
      texelColor = lerp(

      
      lerp(

        gravelCol,
          forestCol,
          min(0.8 - greenlane * 0.2, vShadow * 2.0) * edgeness
        ),
        
        texelColor,

        smoothstep(
          0.0, 
          rp,
          texelColor.g / rp
        )
        

      );

      // OFFROAD TEXTURE
      if((greenlane < 0.5 || seasonIndex == 3) && roadProx < 1.0) {
          float4 roadMaskCol = SurfaceSample_offroadMap( vRoadUv);
          texelColor.rgb *= 1.0 - roadMaskCol.r * 0.5 * ((1.0 - greenlane) + (greenlane * rp));
          
      }  

    } else if(seasonIndex != 1 && vShadow > 0.0) {

      float gravelBrightness = 1.3;
      // if(seasonIndex == 2) {
      //   gravelBrightness = 1.3;
      // }

      texelColor = lerp(

        lerp(
          SurfaceSample_gravelMap(
            vUv
          ),
          forestCol * gravelBrightness,
          min(1.0, vShadow * 2.0)
        ),

        texelColor,

        smoothstep(
          0.0, 
          rp,
          texelColor.g / rp
        )
      );

    } else {

      texelColor = lerp(
        SurfaceSample_gravelMap( vUv) * (0.75 + rp * rp * 0.75),
        texelColor,
        smoothstep(
          0.0, 
          rp,
          texelColor.g / rp
        )
      );      
    }

  }

}

//// STEEPNESS CLIFF

// Note we do a branch here since cliffs take up such a small portion of the space; worthwhile
if(steepness > 0.1) {

  float steepBlend = 1.0 - steepness;
  rockTex = lerp(
    SurfaceSample_rockMap( vUv),
    SurfaceSample_rockMap( vUUv),
    saturate((vCamDepth - 20.0) / 130.0) // Blend by proximity
  );
  
  // Gradual distance-based blend
  rockTex.a = SurfaceSample_rockMapBump( vUv).r * SurfaceSample_rockMapBump( vUUv).r;
  
  // Blend according to steepness etc.
  texelColor.a = 0.2 + fade2 * 0.2;
  
  // Lower steep blend is more snow?
  // float steepBlend = 1.0 - steepness; // Reduce steepess factor to reduce rockiness
  // Reduce the depth value to increase sharp transition from snow to rock
  texelColor = terrainBlend(texelColor, steepBlend, rockTex, (1.0 - steepBlend), 0.08);
  texelColor.a = 1.0;
}

//// PREVENT GLITTERING PIXELS

texelColor.r = saturate(texelColor.r);
texelColor.g = saturate(texelColor.g);
texelColor.b = saturate(texelColor.b);

//// PREP EMISSIVE?

// NOTE this must match the grass sprites. Ideally share a chunk?

totalEmissiveRadiance = texelColor.rgb * radiance;

//// FINAL COMPOSITION

diffuseColor *= texelColor;

// DEBUG COLOURS
// diffuseColor = float4(vHeather, vCurvature, 0.0, 1.0);
// diffuseColor.rgb = vNormal.rgb;
// diffuseColor = float4(float3(vLightGrass), 1.0);

// Clamp to reduce glittering pixels? Needs a better fix though... this is a last resort

/*

This is a commercial project owned and developed solely by Topograph Interactive Ltd.
No derivatives or modifications are permitted 

*/




    // TODO ORIGIN OFFSET USE OFFSET CAM POS
    float3 offsetCameraPosition = float3(
      GetCameraPositionWS().x + _RenderOrigin.x - _SourceOrigin.x,
      GetCameraPositionWS().y + _RenderOrigin.y,
      GetCameraPositionWS().z + _RenderOrigin.z - _SourceOrigin.y
    );
    // REPLACE BELOW WITH cameraPosition WHEN DISABLING ORIGIN OFFSET
    
    float fresnel = 1.0 - max( 0.0, dot( normalize(offsetCameraPosition - vFresnelPos), vFresnelNorm ) );

    float lightFresnel = max(0.0, (fresnel - 0.75)) * 4.0 * (1.0 - blendVal * 0.5);
    lightFresnel *= lightFresnel;
    lightFresnel *= (1.0 - vShadow);
    lightFresnel *= fresnelIntensity;
    
    float darkFresnel = 1.0 - min(1.0, fresnel * 1.3333);
    darkFresnel *= fresnelIntensity;
    darkFresnel = 0.25 + min(0.25, max(0.0, darkFresnel - 0.25) * 0.5);

    diffuseColor.rgb *= 1.0 + lightFresnel - darkFresnel;


Light sun=GetMainLight();half3 direct=sun.color*saturate(dot(vFresnelNorm,sun.direction))*(1.0-min(1.0,vShadow));
half3 ambient=max(0,SampleSH(vFresnelNorm))*(1.75-min(1.0,max(.75,vShadow)));
// URP Lambert/ambient adapter, same scope as the existing road material adapter.
half3 lit=diffuseColor.rgb*(ambient+direct+direct*radiance);
return half4(lerp(lit,diffuseColor.rgb,saturate(_SurfaceOnly)),1);}

ENDHLSL
Pass {Name "Forward" Tags {"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma target 3.5
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile_instancing
ENDHLSL}
Pass {Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask 0
HLSLPROGRAM
#pragma target 3.5
#pragma vertex Vert
#pragma fragment DepthFrag
#pragma multi_compile_instancing
half4 DepthFrag(Varyings i):SV_Target{return 0;}
ENDHLSL}
}}
