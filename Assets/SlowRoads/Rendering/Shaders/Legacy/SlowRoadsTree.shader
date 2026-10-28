Shader "SlowRoads/Tree"
{
    // =========================================================================
    //  Slow Roads 植被 shader（逆向移植自 Three.js GLSL）
    //  来源：extracted_correct/build/_app/immutable/chunks/tree_04.6b3d8a5d.js
    // =========================================================================
    Properties
    {
        [MainTexture] _BaseMap ("Tree Atlas (Albedo)", 2D) = "white" {}
        [Normal]     _BumpMap ("Normal Map", 2D) = "bump" {}

        [Header(Alpha Test)]
        _AlphaMap ("Alpha Map (G channel)", 2D) = "white" {}
        _UseAlphaMap ("Use Separate Alpha Map", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Fake Shadow)]
        _FakeShadow ("Fake Shadow Strength", Range(0, 1)) = 0.65
        _ShadowFactor ("Shadow Factor", Range(0, 1)) = 0.8

        [Header(Colour)]
        _Discolouration ("Discolouration", Range(0, 2)) = 0.3
        _Radiance ("Leaf Radiance", Range(0, 2)) = 0.5
        _LeafBrightness ("Leaf Brightness", Range(0, 1)) = 1.0

        [Header(Wind)]
        _WindStrength ("Wind Strength", Range(0, 2)) = 0.0
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        LOD 200
        Cull Off
        ZWrite On

        // ============================================================
        //  ForwardLit —— 主光照 pass
        // ============================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
            TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BumpMap_ST;
                float4 _AlphaMap_ST;
                float  _Cutoff;
                float  _UseAlphaMap;
                float  _FakeShadow;
                float  _ShadowFactor;
                float  _Discolouration;
                float  _Radiance;
                float  _LeafBrightness;
                float  _WindStrength;
                float  _WindSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS     : SV_POSITION;
                float2 uv             : TEXCOORD0;
                float3 centerNormalWS : TEXCOORD1;
                float3 positionWS     : TEXCOORD2;
                float  vShadow        : TEXCOORD3;
                float  discolouration : TEXCOORD4;
                float4 shadowCoord    : TEXCOORD5;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 posOS = IN.positionOS.xyz;
                float3 posWS = TransformObjectToWorld(posOS);

                // 风动画（可选，默认关闭）
                if (_WindStrength > 0.001 && IN.uv.y > 0.25)
                {
                    float tt = _Time.y * _WindSpeed;
                    float n = valueNoise(float2(posWS.x * 0.05 + tt, posWS.z * 0.012 + tt * 0.25));
                    float sway = n * _WindStrength;
                    float hf = saturate(posOS.y / 12.0);
                    posWS.x += sin(sway * 6.283) * 0.15 * hf;
                    posWS.z += cos(sway * 6.283) * 0.15 * hf;
                }

                OUT.positionWS = posWS;

                // 中心法线：叶冠(V>0.25)从竖直轴向外，树干从上方点向外
                float3 centerNormalOS;
                if (IN.uv.y > 0.25)
                    centerNormalOS = normalize(posOS - float3(0.0, min(posOS.y, 5.0), 0.0));
                else
                    centerNormalOS = normalize(posOS - float3(0.0, 7.0, 0.0));
                OUT.centerNormalWS = normalize(TransformObjectToWorldNormal(centerNormalOS));

                // 假阴影：到树心竖直轴距离
                float3 axis = float3(0.0, min(posOS.y, 5.0), 0.0);
                float distSq = dot(posOS - axis, posOS - axis);
                float vShadow = 0.0;
                if (distSq < 36.0)
                {
                    if (distSq < 8.0)
                        vShadow = 1.0;
                    else
                        vShadow = 1.0 - (distSq - 8.0) / 28.0;
                }
                vShadow = max(vShadow, 1.0 - saturate(posOS.y / 8.0));
                vShadow *= _ShadowFactor;
                OUT.vShadow = vShadow;

                // 褪色
                float n = valueNoise(posWS.xz / 256.0);
                OUT.discolouration = 1.0 + (n - 0.5) * _Discolouration;

                OUT.uv = IN.uv;
                OUT.shadowCoord = TransformWorldToShadowCoord(posWS);
                OUT.positionCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                // Alpha Test
                float alpha = tex.a;
                if (_UseAlphaMap > 0.5)
                    alpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).g;
                clip(alpha - _Cutoff);

                // 假阴影
                float fShadow = saturate(IN.vShadow);
                fShadow = sqrt(fShadow) * _FakeShadow;

                // 叶片判定：绿通道显著大于蓝通道
                float leafEffect = step(tex.b * 1.5, tex.g) * _LeafBrightness;

                // 褪色只作用于叶片
                tex.rg *= 1.0 - (1.0 - IN.discolouration) * leafEffect;

                // 光照
                Light mainLight = GetMainLight();
                float3 L = mainLight.direction;

                float3 N = normalize(IN.centerNormalWS);

                float d = dot(N, L);
                float squash = min(1.0, max(0.25, d * 2.0));
                float ndl = saturate(d);
                float3 diffuse = tex.rgb * mainLight.color * (ndl * squash);

                // 环境光：中性半球光（对应原版 hemisphereLight），
                // 避免场景「天空盒」环境光偏蓝把树染成蓝紫色。
                float3 skyColor    = float3(0.55, 0.60, 0.62);
                float3 groundColor = float3(0.32, 0.28, 0.24);
                float  hemi        = saturate(N.y * 0.5 + 0.5);
                float3 ambient     = lerp(groundColor, skyColor, hemi) * tex.rgb;

                float shadowMul = 1.0 - fShadow;
                diffuse *= shadowMul;
                ambient  *= 1.0 - max(0.0, fShadow - 0.6);

                // 叶片自发光（蓝分量跟随贴图 tex.b，而非硬编码 0.5，避免暗叶偏蓝紫）
                float3 leafEmissive = float3(tex.rgb * 8.0) * _Radiance * leafEffect;

                float3 color = diffuse + ambient + leafEmissive;
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ============================================================
        //  ShadowCaster —— 阴影投射（保留 alpha 裁剪）
        // ============================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _AlphaMap_ST;
                float  _Cutoff;
                float  _UseAlphaMap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowPassVertex(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(float3(0, 1, 0));
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, _LightDirection));
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 ShadowPassFragment(Varyings IN) : SV_TARGET
            {
                float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                if (_UseAlphaMap > 0.5)
                    alpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).g;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ============================================================
        //  DepthOnly —— 深度预写入
        // ============================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _AlphaMap_ST;
                float  _Cutoff;
                float  _UseAlphaMap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings DepthOnlyVertex(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 DepthOnlyFragment(Varyings IN) : SV_TARGET
            {
                float alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a;
                if (_UseAlphaMap > 0.5)
                    alpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, IN.uv).g;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
