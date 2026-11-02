// Instancing x2; no optional material keywords. No URP lighting keyword multiplication.
Shader "SlowRoads/Hills/Tree" { Properties {_BaseMap("Albedo",2D)="white"{} _AlphaMap("Alpha",2D)="white"{} _BumpMap("Reference normal channels",2D)="white"{} _NoiseMap("Noise",2D)="white"{} _Cutoff("Alpha cutoff",Float)=.5 _UseAlphaMap("Separate alpha",Float)=0 _Discolouration("Discolouration",Float)=.3 _ShadowFactor("Shadow factor",Float)=.8 _Radiance("Radiance",Float)=0 _HasSnow("Snow",Float)=0} SubShader { Tags {"RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Off ZWrite On
Pass { Name "Forward" Tags {"LightMode"="UniversalForward"} 
HLSLPROGRAM
#pragma target 4.5
#pragma vertex TreeVert
#pragma fragment TreeFrag
#pragma multi_compile_instancing

#include "HillsTreeInstance.hlsl"
ENDHLSL
}
Pass { Name "DepthNormals" Tags {"LightMode"="DepthNormals"} 
HLSLPROGRAM
#pragma target 4.5
#pragma vertex TreeVert
#pragma fragment TreeNormals
#pragma multi_compile_instancing

#include "HillsTreeInstance.hlsl"
ENDHLSL
}
Pass { Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask 0
HLSLPROGRAM
#pragma target 4.5
#pragma vertex TreeVert
#pragma fragment TreeDepth
#pragma multi_compile_instancing

#include "HillsTreeInstance.hlsl"
ENDHLSL
}
Pass { Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ColorMask 0
HLSLPROGRAM
#define TREE_SHADOW 1
#pragma target 4.5
#pragma vertex TreeVert
#pragma fragment TreeDepth
#pragma multi_compile_instancing

#include "HillsTreeInstance.hlsl"
ENDHLSL
}
} }
