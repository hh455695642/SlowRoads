Shader "SlowRoads/Hills/Grass"
{
    Properties {
        [Header(Hills Theme Lighting)]
        _ThemeLightingEnabled("Apply theme lighting",Float)=0
        _ThemeSun("Theme sun irradiance (linear)",Vector)=(0,0,0,0)
        _ThemeAmbient("Theme ambient irradiance (linear)",Vector)=(0,0,0,0)
        _ThemeHemisphere("Theme hemisphere irradiance (linear)",Vector)=(0,0,0,0)
        _ThemeReferenceSunIntensity("Theme reference sun intensity",Float)=1

        _BaseMap("Original grass atlas",2D)="white"{} _NoiseMap("Original noise",2D)="gray"{}
        grassVariationMap("Variation",2D)="gray"{} detailFarMap("Detail",2D)="white"{}
        grassColA("Grass A",Vector)=(1,1,1,1) grassColB("Grass B",Vector)=(1,1,1,1)
        peakColA("Peak A",Vector)=(1,1,1,1) peakColB("Peak B",Vector)=(1,1,1,1)
        shadowFactor("Shadow",Float)=.9 radiance("Radiance",Float)=0 fresnelIntensity("Fresnel",Float)=.6 seasonIndex("Season",Float)=0
    }
    SubShader {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="AlphaTest"} Cull Off ZWrite On
        HLSLINCLUDE
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
