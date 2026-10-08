// Объёмная листва: 3D-карточки с пиксельными пучками листьев (кроны лиственных деревьев).
// Вершины — настоящие углы карточек; UV1: xyz — центр карточки, w — сила ветра; UV2 — якорь кроны (центр, радиус).
// Нормаль задана сеткой «почти сферической» — крона освещается как единый объём, а не набор плоскостей.
// Цвет вершины: RGB — разброс оттенка, A — затенение глубины кроны (только для рассеянного света).
Shader "ChroniclesOfRebirth/PixelFoliage"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _Wrap("Wrap Lighting", Range(0, 1)) = 0.15
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 0.85
        _Translucency("Translucency (back light)", Range(0, 2)) = 0.45
        _WindStrength("Wind Strength", Range(0, 0.5)) = 0.06
        _WindSpeed("Wind Speed", Float) = 1.2
        _ShadowOffset("Shadow Lookup Offset To Light", Float) = 0.6

        [Toggle] _CrownFade("Crown Fade Near Player", Float) = 1
        _FadeMode("Fade Mode (0 opaque, 1 faded)", Float) = 0
        _FadeOpacity("Faded Crown Opacity", Range(0, 1)) = 0.3
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1

        // Проход SRPDefaultUnlit — предварительная глубина бледных крон (см. PixelSprite).
        [HideInInspector] _PrePassZTest("Pre Pass ZTest", Float) = 4     // LessEqual
        [HideInInspector] _PrePassZWrite("Pre Pass ZWrite", Float) = 1
        [HideInInspector] _PrePassColorMask("Pre Pass Color Mask", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Cutoff;
            half _Wrap;
            half _AmbientStrength;
            half _Translucency;
            half _WindStrength;
            half _WindSpeed;
            half _ShadowOffset;
            half _CrownFade;
            half _FadeMode;
            half _FadeOpacity;
        CBUFFER_END

        #include "CanopyFade.hlsl"

        struct FoliageAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float4 card : TEXCOORD1;   // xyz — центр карточки, w — сила ветра
            float4 anchor : TEXCOORD2; // xyz — центр кроны, w — её радиус
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float3 CardCenterWS(FoliageAttributes v)
        {
            return TransformObjectToWorld(v.card.xyz);
        }

        // Ветер: карточка качается целиком (медленная волна по лесу + порыв), углы слегка трепещут.
        float3 FoliagePositionWS(FoliageAttributes v)
        {
            float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
            float3 center = CardCenterWS(v);
            float t = _Time.y * _WindSpeed;
            float phase = dot(center.xz, float2(0.31, 0.23));
            float sway = sin(t + phase) * 0.65 + sin(t * 2.7 + phase * 1.9) * 0.35;
            float flutter = sin(t * 6.0 + dot(positionWS, float3(3.1, 2.3, 2.7)));
            float weight = _WindStrength * v.card.w;
            return positionWS + float3(0.8, 0.1, 0.5) * (sway * weight) + float3(0, 1, 0) * (flutter * weight * 0.25);
        }

        half FoliageAlpha(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
                half crownFade : TEXCOORD4;
                half cameraFade : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(FoliageAttributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.cameraFade = CameraFade(CardCenterWS(v));
                o.crownFade = CombinedFade(v.anchor, o.cameraFade);

                float3 positionWS = FoliagePositionWS(v);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(albedo.a - _Cutoff);
                CrownClip(i.crownFade);
                albedo.rgb *= i.color.rgb;

                // Тень ищется чуть ближе к солнцу: внешний слой листвы не затеняет сам себя, внутренние — в тени соседей.
                float3 shadowPosition = i.positionWS + _MainLightPosition.xyz * _ShadowOffset;
                Light light = GetMainLight(TransformWorldToShadowCoord(shadowPosition));
                half3 n = normalize(i.normalWS);
                half shadow = light.shadowAttenuation * light.distanceAttenuation;
                half wrapped = saturate((dot(n, light.direction) + _Wrap) / (1 + _Wrap));
                half3 direct = light.color * (wrapped * shadow);

                // Просвечивание: листья против солнца светятся тёплым, сильнее по краю кроны (там листва тоньше).
                half3 toCamera = normalize(GetCameraPositionWS() - i.positionWS);
                half back = pow(saturate(dot(-toCamera, light.direction)), 4);
                half rim = 1 - saturate(dot(n, toCamera));
                half3 translucent = light.color * (back * _Translucency * lerp(0.3, 1, rim) * lerp(0.25, 1, shadow));

                half3 ambient = SampleSH(n) * (_AmbientStrength * i.color.a);

                half3 color = albedo.rgb * (direct + ambient + translucent);
                color = MixFog(color, i.fogFactor);

                half alpha = _FadeMode > 0.5 ? lerp(1, _FadeOpacity, saturate((i.crownFade - 0.5) * 2)) : 1;
                alpha *= 1 - i.cameraFade;
                return half4(color, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(FoliageAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = FoliagePositionWS(v);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(FoliageAlpha(i.uv) - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half crownFade : TEXCOORD1;
            };

            Varyings Vert(FoliageAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(FoliagePositionWS(v));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.crownFade = CombinedFade(v.anchor, CameraFade(CardCenterWS(v)));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(FoliageAlpha(i.uv) - _Cutoff);
                CrownClip(i.crownFade);
                return 0;
            }
            ENDHLSL
        }

        // Глубина и нормали для SSAO: затенение между гроздьями и у ветвей.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half crownFade : TEXCOORD2;
            };

            Varyings Vert(FoliageAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(FoliagePositionWS(v));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.crownFade = CombinedFade(v.anchor, CameraFade(CardCenterWS(v)));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(FoliageAlpha(i.uv) - _Cutoff);
                CrownClip(i.crownFade);
                float3 normalWS = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
                #else
                return half4(NormalizeNormalPerPixel(normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }

        // Предварительная глубина бледных крон (_FadeMode = 1): пишется только ближайший слой карточек,
        // основной проход рисует один полупрозрачный слой вместо накопления нескольких.
        Pass
        {
            Name "FadePrePass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest [_PrePassZTest]
            ZWrite [_PrePassZWrite]
            ColorMask [_PrePassColorMask]
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half crownFade : TEXCOORD1;
                half cameraFade : TEXCOORD2;
            };

            Varyings Vert(FoliageAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(FoliagePositionWS(v));
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.cameraFade = CameraFade(CardCenterWS(v));
                o.crownFade = CombinedFade(v.anchor, o.cameraFade);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(FoliageAlpha(i.uv) - _Cutoff);
                CrownClip(i.crownFade);
                // Растворяющиеся у камеры карточки не пишут глубину — иначе прятали бы бледный слой за собой.
                clip(0.001 - i.cameraFade);
                return 0;
            }
            ENDHLSL
        }
    }
}
