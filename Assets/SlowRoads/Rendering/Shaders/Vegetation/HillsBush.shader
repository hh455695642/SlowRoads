Shader "SlowRoads/Hills/Bush"
{
    Properties {
        _BaseMap("Original bush atlas",2D)="white"{} _NoiseMap("Original noise",2D)="gray"{}
        grassVariationMap("Unused",2D)="gray"{} detailFarMap("Unused",2D)="white"{}
        grassColA("Unused",Vector)=(1,1,1,1) grassColB("Unused",Vector)=(1,1,1,1)
        peakColA("Unused",Vector)=(1,1,1,1) peakColB("Unused",Vector)=(1,1,1,1)
        shadowFactor("Shadow",Float)=.9 radiance("Radiance",Float)=0 fresnelIntensity("Unused",Float)=.6 seasonIndex("Season",Float)=0
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="AlphaTest"} Cull Off ZWrite On
        HLSLINCLUDE
        #define HILLS_BUSH
        #include "HillsGroundCover.hlsl"
        ENDHLSL
        Pass {Name "Forward" Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CoverVert
            #pragma fragment CoverForward
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass {Name "DepthNormals" Tags {"LightMode"="DepthNormals"}
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CoverVert
            #pragma fragment CoverDepthNormals
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass {Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CoverVert
            #pragma fragment CoverDepth
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}
