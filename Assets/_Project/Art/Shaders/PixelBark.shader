// Пиксельная кора без развёртки: рисунок считается в локальной системе ветви — поперёк оси от центра кольца,
// вдоль оси — по длине ветви (данные колец из MeshBuilder.AddTube: UV1 — ось и длина, UV2 — центр кольца и сдвиг рисунка).
// Швов нет ни по окружности, ни через развилку (ствол и главный рог — одна трубка), а рисунок не «плывёт»
// от того, где дерево стоит в мире. Пиксели привязаны к мировой сетке (_TexelsPerUnit) — того же размера, что у всего мира.
// Узор — продольные волокна и сеть борозд вдоль ветви; цвета — из палитры (_BarkRamp, по пикселю на цвет) с дизерингом.
// Рельеф борозд наклоняет нормаль — свет цепляется за кору, как с картой нормалей.
// Меши без данных оси (стволы елей) получают вертикальные волокна по мировым координатам.
Shader "ChroniclesOfRebirth/PixelBark"
{
    Properties
    {
        [NoScaleOffset] _BarkRamp("Bark Palette (1 px per color, dark to light)", 2D) = "gray" {}
        _RampSize("Palette Size", Float) = 8
        _TexelsPerUnit("Pixels Per Unit", Float) = 32
        _BumpStrength("Furrow Relief", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BarkRamp);
        SAMPLER(sampler_BarkRamp);

        CBUFFER_START(UnityPerMaterial)
            float _RampSize;
            float _TexelsPerUnit;
            float _BumpStrength;
        CBUFFER_END

        struct BarkAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 axis : TEXCOORD1; // xyz — направление ветви, w — длина вдоль неё
            float4 ring : TEXCOORD2; // xyz — центр кольца, w — сдвиг рисунка
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Целочисленный хэш (PCG3D): одинаково точен на любых координатах.
        float Hash31(float3 p)
        {
            uint3 v = uint3(int3(floor(p)));
            v = v * 1664525u + 1013904223u;
            v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
            v ^= v >> 16u;
            v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
            return v.x * (1.0 / 4294967295.0);
        }

        float ValueNoise3(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3 - 2 * f);
            float a = lerp(Hash31(i), Hash31(i + float3(1, 0, 0)), f.x);
            float b = lerp(Hash31(i + float3(0, 1, 0)), Hash31(i + float3(1, 1, 0)), f.x);
            float c = lerp(Hash31(i + float3(0, 0, 1)), Hash31(i + float3(1, 0, 1)), f.x);
            float d = lerp(Hash31(i + float3(0, 1, 1)), Hash31(i + float3(1, 1, 1)), f.x);
            return lerp(lerp(a, b, f.y), lerp(c, d, f.y), f.z);
        }

        // Локальная система ветви в точке поверхности (данные интерполированы между кольцами).
        struct BarkFrame
        {
            float3 positionWS;
            float3 axis;
            float arc;
            float3 center;
            float seed;
            bool hasAxis;
        };

        BarkFrame MakeFrame(float3 positionWS, float4 axisData, float4 ringData)
        {
            BarkFrame f;
            f.positionWS = positionWS;
            f.hasAxis = dot(axisData.xyz, axisData.xyz) > 0.01;
            f.axis = f.hasAxis ? normalize(axisData.xyz) : float3(0, 1, 0);
            f.arc = axisData.w;
            f.center = ringData.xyz;
            f.seed = ringData.w;
            return f;
        }

        float3 CellCenterWS(float3 p)
        {
            return (floor(p * _TexelsPerUnit) + 0.5) / _TexelsPerUnit;
        }

        // Тон коры (0 — тёмный, 1 — светлый) в пикселе с точкой p. Координаты — в пикселях:
        // поперёк оси — смещение от центра кольца, вдоль — длина по ветви. Поперёк волокна по 8 px, вдоль — по 32 px;
        // борозды — линии уровня шума, растянутого вдоль оси: длинные трещины расходятся и сходятся, между ними — пластины.
        float BarkTone(BarkFrame f, float3 p)
        {
            float3 cell = CellCenterWS(p);
            float3 radial = (cell - f.center) * _TexelsPerUnit;
            float3 across = radial - f.axis * dot(radial, f.axis) + f.seed * 37.0;
            float along = (f.hasAxis ? f.arc + dot(cell - f.positionWS, f.axis) : cell.y) * _TexelsPerUnit;

            float t = 0.3 + (ValueNoise3(across / 8 + f.axis * (along / 32)) - 0.5) * 0.45
                    + (Hash31(floor(across / 2) + f.axis * floor(along / 4)) - 0.5) * 0.12;

            float plates = ValueNoise3(across / 7 + f.axis * (along / 56) + 17.3);
            float wiggle = ValueNoise3(across / 3 + f.axis * (along / 20) + 5.1) - 0.5;
            float furrow = abs(plates - 0.5 + wiggle * 0.06);
            if (furrow < 0.03)
                t = 0.04;                    // дно борозды
            else if (furrow < 0.06)
                t -= 0.14;                   // склон борозды
            else
                t += saturate(furrow - 0.06) * 0.35; // середина пластины — светлее
            return t;
        }

        static const float BayerMatrix[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        // Порог упорядоченного дизеринга 4×4 в [-0.5, 0.5) по пикселю мировой сетки.
        float Bayer4(float3 p)
        {
            float3 cell = floor(p * _TexelsPerUnit);
            int x = (int)(cell.x + cell.z) & 3;
            int y = (int)cell.y & 3;
            return (BayerMatrix[y * 4 + x] + 0.5) / 16 - 0.5;
        }

        half3 BarkColor(BarkFrame f)
        {
            float t = BarkTone(f, f.positionWS) + Bayer4(f.positionWS) * 0.5 / _RampSize;
            float index = clamp(floor(saturate(t) * _RampSize), 0, _RampSize - 1);
            return SAMPLE_TEXTURE2D_LOD(_BarkRamp, sampler_BarkRamp, float2((index + 0.5) / _RampSize, 0.5), 0).rgb;
        }

        // Рельеф: тон соседних пикселей поперёк и вдоль ветви наклоняет нормаль (борозды — впадины).
        // Соседи берутся от центра текущего пикселя — нормаль постоянна в пределах пикселя.
        float3 BarkNormal(BarkFrame f, float3 normalWS)
        {
            float3 around = normalize(cross(f.axis, normalWS) + 1e-5);
            float3 along = normalize(cross(normalWS, around));
            float texel = 1.0 / _TexelsPerUnit;
            float3 center = CellCenterWS(f.positionWS);
            float h = BarkTone(f, center);
            float ha = BarkTone(f, center + around * texel);
            float hv = BarkTone(f, center + along * texel);
            return normalize(normalWS + (around * (h - ha) + along * (h - hv)) * _BumpStrength);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 axis : TEXCOORD2;
                float4 ring : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(BarkAttributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.axis = float4(TransformObjectToWorldDir(v.axis.xyz, false), v.axis.w);
                o.ring = float4(TransformObjectToWorld(v.ring.xyz), v.ring.w);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                BarkFrame frame = MakeFrame(i.positionWS, i.axis, i.ring);
                float3 normalWS = BarkNormal(frame, normalize(i.normalWS));

                InputData input = (InputData)0;
                input.positionWS = i.positionWS;
                input.positionCS = i.positionCS;
                input.normalWS = normalWS;
                input.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                input.fogCoord = i.fogFactor;
                input.bakedGI = SampleSH(normalWS);
                input.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = BarkColor(frame);
                surface.alpha = 1;
                surface.metallic = 0;
                surface.smoothness = 0.05;
                surface.occlusion = 1;
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(input, surface);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return half4(color.rgb, 1);
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

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 Vert(BarkAttributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
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
                return positionCS;
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 Vert(BarkAttributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // Нормали для SSAO: затенение в развилках, у корней и в бороздах.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 axis : TEXCOORD2;
                float4 ring : TEXCOORD3;
            };

            Varyings Vert(BarkAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.axis = float4(TransformObjectToWorldDir(v.axis.xyz, false), v.axis.w);
                o.ring = float4(TransformObjectToWorld(v.ring.xyz), v.ring.w);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                BarkFrame frame = MakeFrame(i.positionWS, i.axis, i.ring);
                return half4(NormalizeNormalPerPixel(BarkNormal(frame, normalize(i.normalWS))), 0);
            }
            ENDHLSL
        }
    }
}
