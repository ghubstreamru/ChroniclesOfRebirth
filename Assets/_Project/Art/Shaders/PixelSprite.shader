// Пиксельный спрайт-билборд в 3D-мире (персонажи, листва, трава).
// Меш хранит pivot в позиции вершины и смещение угла (в юнитах) в UV1 — разворот делается здесь.
// В проходе теней матрица вида — это вид источника света, поэтому силуэт сам разворачивается к солнцу.
Shader "ChroniclesOfRebirth/PixelSprite"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _FrameRect("Frame Rect (xy offset, zw size)", Vector) = (0, 0, 1, 1)
        _Upright("Upright (0 = face camera, 1 = vertical)", Range(0, 1)) = 1
        _NormalBend("Normal Bend To Camera", Range(0, 1)) = 0.5
        _Wrap("Wrap Lighting", Range(0, 1)) = 0.5
        _AmbientStrength("Ambient Strength", Range(0, 2)) = 1
        _WindStrength("Wind Strength", Range(0, 0.5)) = 0
        _WindSpeed("Wind Speed", Float) = 1.5
        _ShadowOffset("Shadow Lookup Offset To Light", Float) = 0.3
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 0)
        _XRayColor("X-Ray Ghost Tint (a = opacity)", Color) = (0.85, 0.93, 1, 0.6)

        // Прозрачность крон рядом с героем. Материал листвы рисует меш дважды:
        // режим 0 — непрозрачные кроны (бледнеющие пропускаются), режим 1 — только бледнеющие, полупрозрачно.
        [Toggle] _CrownFade("Crown Fade Near Player", Float) = 0
        _FadeMode("Fade Mode (0 opaque, 1 faded)", Float) = 0
        _FadeOpacity("Faded Crown Opacity", Range(0, 1)) = 0.3
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1

        // Проход SRPDefaultUnlit: у персонажей — «призрак» сквозь препятствия, у бледных крон — предварительная глубина.
        [HideInInspector] _PrePassZTest("Pre Pass ZTest", Float) = 5     // Greater
        [HideInInspector] _PrePassZWrite("Pre Pass ZWrite", Float) = 0
        [HideInInspector] _PrePassColorMask("Pre Pass Color Mask", Float) = 15
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
            float4 _FrameRect;
            half _Upright;
            half _NormalBend;
            half _Wrap;
            half _AmbientStrength;
            half _WindStrength;
            half _WindSpeed;
            half _ShadowOffset;
            half4 _EmissionColor;
            half4 _XRayColor;
            half _CrownFade;
            half _FadeMode;
            half _FadeOpacity;
        CBUFFER_END

        #include "CanopyFade.hlsl"

        struct SpriteAttributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            float2 corner : TEXCOORD1;
            float4 anchor : TEXCOORD2; // xyz — центр кроны, w — её радиус (одинаковы для всех клубков дерева)
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Растворение у камеры — по pivot клубка (вершины билборда лежат в нём).
        half CameraFade(SpriteAttributes v)
        {
            return CameraFade(TransformObjectToWorld(v.positionOS.xyz));
        }

        half CombinedFade(SpriteAttributes v, half cameraFade)
        {
            return CombinedFade(v.anchor, cameraFade);
        }

        // Базис билборда из текущей матрицы вида (камера или источник света).
        void BillboardBasis(out float3 right, out float3 up)
        {
            right = normalize(UNITY_MATRIX_V[0].xyz);
            up = normalize(lerp(UNITY_MATRIX_V[1].xyz, float3(0, 1, 0), _Upright));
        }

        float3 BillboardPositionWS(SpriteAttributes v)
        {
            float3 pivotWS = TransformObjectToWorld(v.positionOS.xyz);
            float3 right, up;
            BillboardBasis(right, up);
            float sway = sin(_Time.y * _WindSpeed + pivotWS.x * 0.7 + pivotWS.z * 0.45) * _WindStrength * max(v.corner.y, 0);
            return pivotWS + right * (v.corner.x + sway) + up * v.corner.y;
        }

        float2 SpriteUV(float2 uv)
        {
            return _FrameRect.xy + uv * _FrameRect.zw;
        }

        half SpriteAlpha(float2 uv, half vertexAlpha)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a * vertexAlpha;
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

            Varyings Vert(SpriteAttributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.cameraFade = CameraFade(v);
                o.crownFade = CombinedFade(v, o.cameraFade);

                float3 positionWS = BillboardPositionWS(v);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);

                // Нормаль между «вверх» и «к камере»: спрайт освещается как объёмный, а не как плоская карточка.
                float3 toCamera = normalize(GetCameraPositionWS() - positionWS);
                o.normalWS = normalize(lerp(float3(0, 1, 0), toCamera, _NormalBend));
                o.uv = SpriteUV(v.uv);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor * i.color;
                clip(albedo.a - _Cutoff);
                CrownClip(i.crownFade);

                // Тень ищется чуть ближе к солнцу: клубок не затеняет сам себя, но получает тень от соседних.
                float3 shadowPosition = i.positionWS + _MainLightPosition.xyz * _ShadowOffset;
                Light light = GetMainLight(TransformWorldToShadowCoord(shadowPosition));
                half3 n = normalize(i.normalWS);
                half wrapped = saturate((dot(n, light.direction) + _Wrap) / (1 + _Wrap));
                half3 direct = light.color * (wrapped * light.shadowAttenuation * light.distanceAttenuation);
                half3 ambient = SampleSH(n) * _AmbientStrength;

                half3 color = albedo.rgb * (direct + ambient) + _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);

                // В бледном режиме прозрачность плавно растёт от 0.5 до 1 по силе бледнения.
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
                half alpha : TEXCOORD1;
            };

            Varyings Vert(SpriteAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = BillboardPositionWS(v);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirection = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, lightDirection, lightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                o.positionCS = positionCS;
                o.uv = SpriteUV(v.uv);
                o.alpha = v.color.a;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(SpriteAlpha(i.uv, i.alpha) - _Cutoff);
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
                half alpha : TEXCOORD1;
                half crownFade : TEXCOORD2;
            };

            Varyings Vert(SpriteAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(BillboardPositionWS(v));
                o.uv = SpriteUV(v.uv);
                o.alpha = v.color.a;
                o.crownFade = CombinedFade(v, CameraFade(v));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                clip(SpriteAlpha(i.uv, i.alpha) - _Cutoff);
                CrownClip(i.crownFade);
                return 0;
            }
            ENDHLSL
        }

        // Проход, который URP рисует перед основным (SRPDefaultUnlit). Два применения, включается только где нужен:
        // • персонажи — «призрак» там, где героя закрывает геометрия: цвета спрайта, осветлённые к _XRayColor
        //   (ZTest Greater, цвет пишется);
        // • бледные кроны (_FadeMode = 1) — предварительная глубина: записывается только ближайший слой клубков,
        //   и основной проход рисует один полупрозрачный слой вместо накопления 4–5 слоёв (ZTest LEqual, ZWrite On, без цвета).
        Pass
        {
            Name "XRayOrPrePass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest [_PrePassZTest]
            ZWrite [_PrePassZWrite]
            ColorMask [_PrePassColorMask]
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : TEXCOORD1;
                half crownFade : TEXCOORD2;
                half cameraFade : TEXCOORD3;
            };

            Varyings Vert(SpriteAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformWorldToHClip(BillboardPositionWS(v));
                o.uv = SpriteUV(v.uv);
                o.alpha = v.color.a;
                o.cameraFade = CameraFade(v);
                o.crownFade = CombinedFade(v, o.cameraFade);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(albedo.a * i.alpha - _Cutoff);
                if (_FadeMode > 0.5)
                {
                    CrownClip(i.crownFade);
                    // Растворяющиеся у камеры клубки не пишут глубину — иначе прятали бы бледный слой за собой.
                    clip(0.001 - i.cameraFade);
                    return 0;
                }
                return half4(lerp(albedo.rgb, _XRayColor.rgb, 0.4), _XRayColor.a);
            }
            ENDHLSL
        }
    }
}
