// Верхние грани рельефа одним материалом: тип земли берётся из карты (пиксель на клетку рельефа), а точка выборки
// сдвигается шумом, привязанным к пиксельной сетке, — границы между травой, тропой, галькой и камнем становятся
// неровными, природными, без квадратных ступенек и без размытия пикселей. Рельеф поверхности (камешки, травинки,
// комья) считается из яркости текстуры — свет «цепляется» за детали, как с картой нормалей.
Shader "ChroniclesOfRebirth/TerrainBlend"
{
    Properties
    {
        [NoScaleOffset] _GroundTextures("Ground Textures (array, index = GroundType)", 2DArray) = "" {}
        [NoScaleOffset] _GroundMap("Ground Type Map (R = type / 16)", 2D) = "black" {}
        _MapRect("Map Rect (minX, minZ, width, depth)", Vector) = (0, 0, 1, 1)
        _MapTexel("Map Texel (1/cols, 1/rows, cols, rows)", Vector) = (1, 1, 1, 1)
        _UnitsPerTile("World Units Per Texture Tile", Float) = 4
        _TexelsPerUnit("Texture Pixels Per Unit", Float) = 32
        _EdgeNoise("Border Jitter (units)", Range(0, 0.6)) = 0.32
        _EdgeDetail("Fine Border Jitter (units)", Range(0, 0.3)) = 0.09
        _BumpStrength("Surface Relief", Range(0, 4)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D_ARRAY(_GroundTextures);
        SAMPLER(sampler_GroundTextures);
        TEXTURE2D(_GroundMap);
        SAMPLER(sampler_GroundMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _MapRect;
            float4 _MapTexel;
            float _UnitsPerTile;
            float _TexelsPerUnit;
            float _EdgeNoise;
            float _EdgeDetail;
            float _BumpStrength;
        CBUFFER_END

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
            float a = Hash21(i), b = Hash21(i + float2(1, 0)), c = Hash21(i + float2(0, 1)), d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        // Тип земли в точке: позиция привязывается к пиксельной сетке текстур, затем сдвигается шумом
        // (крупные «языки» + мелкая рваность) и по ней читается карта клеток.
        float GroundIndex(float2 xz)
        {
            float2 snapped = (floor(xz * _TexelsPerUnit) + 0.5) / _TexelsPerUnit;
            float2 jitter = float2(ValueNoise(snapped * 1.3), ValueNoise(snapped * 1.3 + 17.7)) - 0.5;
            float2 fine = float2(ValueNoise(snapped * 7.0 + 3.1), ValueNoise(snapped * 7.0 + 9.4)) - 0.5;
            float2 p = snapped + jitter * 2 * _EdgeNoise + fine * 2 * _EdgeDetail;
            float2 uv = (p - _MapRect.xy) / _MapRect.zw;
            uv = (floor(uv * _MapTexel.zw) + 0.5) * _MapTexel.xy;
            return floor(SAMPLE_TEXTURE2D_LOD(_GroundMap, sampler_GroundMap, uv, 0).r * 16 + 0.5);
        }

        half3 GroundColor(float2 xz, float index)
        {
            return SAMPLE_TEXTURE2D_ARRAY(_GroundTextures, sampler_GroundTextures, xz / _UnitsPerTile, index).rgb;
        }

        // Рельеф из яркости: разности соседних пикселей текстуры дают наклон поверхности.
        float3 ReliefNormal(float2 xz, float index)
        {
            float texel = 1.0 / _TexelsPerUnit;
            float3 lum = float3(0.299, 0.587, 0.114);
            float h = dot(GroundColor(xz, index), lum);
            float hx = dot(GroundColor(xz + float2(texel, 0), index), lum);
            float hz = dot(GroundColor(xz + float2(0, texel), index), lum);
            return normalize(float3((h - hx) * _BumpStrength, 1, (h - hz) * _BumpStrength));
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float index = GroundIndex(xz);
                half3 albedo = GroundColor(xz, index);

                // Рельеф поверхности наклоняет нормаль верхней грани (на склонах-тропах — вместе с ней).
                float3 relief = ReliefNormal(xz, index);
                float3 n = normalize(i.normalWS);
                float3 normalWS = normalize(n + float3(relief.x, 0, relief.z));

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
                surface.albedo = albedo;
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes v) : SV_POSITION
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // Нормали для SSAO (затенение щелей) — с рельефом поверхности, чтобы AO ложилось и в мелкие детали.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float index = GroundIndex(i.positionWS.xz);
                float3 relief = ReliefNormal(i.positionWS.xz, index);
                float3 normalWS = normalize(normalize(i.normalWS) + float3(relief.x, 0, relief.z));
                return half4(NormalizeNormalPerPixel(normalWS), 0);
            }
            ENDHLSL
        }
    }
}
