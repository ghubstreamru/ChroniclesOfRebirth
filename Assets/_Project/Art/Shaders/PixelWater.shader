// Вода в духе реалистичного HD-2D: дно видно сквозь воду с преломлением от ряби, цвет поглощается по глубине
// (у берега прозрачная бирюза, вглубь — плотный сине-зелёный), на мелководье по дну бегут каустики,
// под пологим углом вода отражает небо, на гребнях ряби — солнечный блик; у берега — тонкая живая пена.
// Мировые координаты квантуются до пиксельной сетки мира, поэтому вода остаётся пиксельной.
// Нужна Opaque Texture в настройках URP (включает билдер локации).
Shader "ChroniclesOfRebirth/PixelWater"
{
    Properties
    {
        _ShallowColor("Shallow Tint (rgb, a = clarity)", Color) = (0.62, 0.92, 0.86, 1)
        _DeepColor("Deep Color", Color) = (0.05, 0.22, 0.26, 1)
        _DepthRange("Absorption Depth", Float) = 1.8
        _SkyColor("Reflected Sky", Color) = (0.86, 0.82, 0.72, 1)
        _Reflectivity("Reflectivity", Range(0, 1)) = 0.35
        _FoamColor("Foam Color", Color) = (0.93, 0.95, 0.9, 1)
        _FoamWidth("Foam Width", Float) = 0.12
        _RippleScale("Ripple Scale", Float) = 0.7
        _RippleStrength("Ripple Normal Strength", Range(0, 1)) = 0.35
        _Refraction("Refraction (screen units)", Range(0, 0.05)) = 0.012
        _FlowDirection("Flow Direction (xy)", Vector) = (1, 0.1, 0, 0)
        _FlowSpeed("Flow Speed", Float) = 0.5
        _PixelsPerUnit("Pixels Per Unit", Float) = 32
        [HDR] _SunColor("Sun Glint", Color) = (2.4, 2.0, 1.5, 1)
        _Caustics("Caustics Strength", Range(0, 1)) = 0.35
        _ShadowDarkness("Shadow Darkness", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One Zero
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthRange;
                half4 _SkyColor;
                half _Reflectivity;
                half4 _FoamColor;
                float _FoamWidth;
                float _RippleScale;
                half _RippleStrength;
                float _Refraction;
                float4 _FlowDirection;
                float _FlowSpeed;
                float _PixelsPerUnit;
                half4 _SunColor;
                half _Caustics;
                half _ShadowDarkness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), f.x),
                            lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), f.x), f.y);
            }

            // Рябь: вытянутые по течению волны + мелкая встречная.
            float Ripple(float2 p, float2 flow)
            {
                float n1 = ValueNoise(p * _RippleScale * float2(0.35, 2.6) - flow);
                float n2 = ValueNoise(p * _RippleScale * 1.9 + flow * 0.7 + 13.1);
                return n1 * 0.7 + n2 * 0.3;
            }

            // Каустики: светлые сети из двух сдвинутых ячеистых шумов.
            float Caustics(float2 p, float t)
            {
                float a = ValueNoise(p * 2.3 + float2(t * 0.6, t * 0.3));
                float b = ValueNoise(p * 2.9 - float2(t * 0.4, -t * 0.5) + 7.7);
                float web = 1 - abs(a - b) * 4;
                return saturate(web) * saturate(web);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.eyeDepth = -TransformWorldToView(o.positionWS).z;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Пиксельная сетка мира.
                float2 p = (floor(i.positionWS.xz * _PixelsPerUnit) + 0.5) / _PixelsPerUnit;
                float t = _Time.y * _FlowSpeed;
                float2 flow = normalize(_FlowDirection.xy) * t;

                // Нормаль ряби из разностей шума.
                float e = 1.0 / _PixelsPerUnit;
                float h = Ripple(p, flow);
                float hx = Ripple(p + float2(e * 2, 0), flow);
                float hz = Ripple(p + float2(0, e * 2), flow);
                float3 normal = normalize(float3((h - hx) * 6 * _RippleStrength, 1, (h - hz) * 6 * _RippleStrength));

                // Глубина воды под пикселем (по исходной точке, без искажения).
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float depth = max(0, LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - i.eyeDepth);

                // Преломление: сдвиг выборки дна по нормали ряби; если сдвиг попал на что-то над водой — без сдвига.
                float2 offset = normal.xz * _Refraction * saturate(depth * 2);
                float2 refractUV = screenUV + offset;
                float refractDepth = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams) - i.eyeDepth;
                if (refractDepth < 0)
                    refractUV = screenUV;
                half3 bottom = SampleSceneColor(refractUV);

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half lit = lerp(1 - _ShadowDarkness, 1, light.shadowAttenuation);

                // Каустики на мелком дне, гаснут с глубиной и в тени.
                float caustic = Caustics(p, _Time.y) * _Caustics * saturate(1 - depth / (_DepthRange * 0.8)) * light.shadowAttenuation;
                bottom += bottom * caustic * 1.5;

                // Поглощение по глубине: прозрачная бирюза у берега → плотный цвет вглубь.
                float absorb = 1 - exp(-depth / _DepthRange * (1.2 - _ShallowColor.a * 0.6));
                half3 color = lerp(bottom * _ShallowColor.rgb, _DeepColor.rgb * lit, absorb);

                // Отражение неба под пологим углом (Френель) и солнечный блик на гребнях.
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fresnel = pow(1 - saturate(dot(normal, view)), 4);
                color = lerp(color, _SkyColor.rgb * lit, saturate(fresnel * 2 + 0.08) * _Reflectivity);
                float3 halfDir = normalize(light.direction + view);
                float glint = pow(saturate(dot(normal, halfDir)), 220) * light.shadowAttenuation;
                color += _SunColor.rgb * step(0.35, glint) * 0.9;

                // Пена у берега: тонкие рваные линии, бегущие по краю.
                float foamNoise = ValueNoise(p * 3.1 + flow * 1.5);
                float foam = step(depth, _FoamWidth * (0.2 + foamNoise)) * step(0.55, ValueNoise(p * 9 - flow * 2));
                color = lerp(color, _FoamColor.rgb * lit, foam * 0.4);

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
