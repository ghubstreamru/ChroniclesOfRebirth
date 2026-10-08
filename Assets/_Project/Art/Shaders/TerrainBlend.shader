// Весь рельеф одним материалом: тип земли берётся из карты (пиксель на клетку рельефа), а точка выборки
// сдвигается шумом, привязанным к пиксельной сетке, — границы между травой, тропой, галькой и камнем становятся
// неровными, природными, без квадратных ступенек и без размытия пикселей. Крутые склоны сами получают текстуру
// обрыва (земляной или скальной — по типу земли), спроецированную сбоку: пиксели на стенах не растягиваются.
// Рельеф поверхности (камешки, травинки, комья) считается из яркости текстуры — свет «цепляется» за детали.
Shader "ChroniclesOfRebirth/TerrainBlend"
{
    Properties
    {
        [NoScaleOffset] _GroundTextures("Ground Textures (array: GroundType, then cliff earth, cliff rock)", 2DArray) = "" {}
        [NoScaleOffset] _GroundMap("Ground Type Map (R = type / 16)", 2D) = "black" {}
        _MapRect("Map Rect (minX, minZ, width, depth)", Vector) = (0, 0, 1, 1)
        _MapTexel("Map Texel (1/cols, 1/rows, cols, rows)", Vector) = (1, 1, 1, 1)
        _UnitsPerTile("World Units Per Texture Tile", Float) = 4
        _TexelsPerUnit("Texture Pixels Per Unit", Float) = 32
        _EdgeNoise("Border Jitter (units)", Range(0, 0.6)) = 0.32
        _EdgeDetail("Fine Border Jitter (units)", Range(0, 0.3)) = 0.09
        _BumpStrength("Surface Relief", Range(0, 4)) = 1.6
        _CliffAngle("Cliff From Slope (cos of angle)", Range(0, 1)) = 0.72
        _CliffJitter("Cliff Border Jitter", Range(0, 0.3)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D_ARRAY(_GroundTextures);
        SAMPLER(sampler_GroundTextures);

        CBUFFER_START(UnityPerMaterial)
            float4 _MapRect;
            float4 _MapTexel;
            float _UnitsPerTile;
            float _TexelsPerUnit;
            float _EdgeNoise;
            float _EdgeDetail;
            float _BumpStrength;
            float _CliffAngle;
            float _CliffJitter;
        CBUFFER_END

        #include "GroundCommon.hlsl"

        half3 GroundColor(float2 uv, float index)
        {
            return SAMPLE_TEXTURE2D_ARRAY(_GroundTextures, sampler_GroundTextures, uv / _UnitsPerTile, index).rgb;
        }

        // Как лечь текстуре: сверху (по xz) или сбоку на обрыв (по плоскости, к которой стена ближе всего).
        struct GroundProjection
        {
            float2 uv;      // мировые координаты на плоскости проекции
            float3 axisU;   // мировые направления осей uv — для наклона нормали рельефом
            float3 axisV;
            float index;    // слой массива текстур
        };

        GroundProjection ProjectGround(float3 positionWS, float3 normalWS)
        {
            GroundProjection g;
            float index = GroundIndex(positionWS.xz);
            // Граница «склон / обрыв» неровная: порог крутизны чуть гуляет по пиксельной сетке.
            float2 snapped = SnapToTexel(positionWS.xz + positionWS.y * 0.37);
            float threshold = _CliffAngle + (ValueNoise(snapped * 2.3) - 0.5) * 2 * _CliffJitter;
            if (normalWS.y < threshold)
            {
                bool facesX = abs(normalWS.x) > abs(normalWS.z);
                g.uv = facesX ? positionWS.zy : positionWS.xy;
                g.axisU = facesX ? float3(0, 0, 1) : float3(1, 0, 0);
                g.axisV = float3(0, 1, 0);
                g.index = (index == GROUND_ROCK || index == GROUND_SNOW || index == GROUND_FLAGSTONE)
                    ? GROUND_CLIFF_ROCK : GROUND_CLIFF_EARTH;
            }
            else
            {
                g.uv = positionWS.xz;
                g.axisU = float3(1, 0, 0);
                g.axisV = float3(0, 0, 1);
                g.index = index;
            }
            return g;
        }

        // Рельеф из яркости: разности соседних пикселей текстуры наклоняют нормаль поверхности.
        float3 ReliefNormal(GroundProjection g, float3 normalWS)
        {
            float texel = 1.0 / _TexelsPerUnit;
            float3 lum = float3(0.299, 0.587, 0.114);
            float h = dot(GroundColor(g.uv, g.index), lum);
            float hu = dot(GroundColor(g.uv + float2(texel, 0), g.index), lum);
            float hv = dot(GroundColor(g.uv + float2(0, texel), g.index), lum);
            float3 tilt = g.axisU * (h - hu) + g.axisV * (h - hv);
            // Наклон — только вдоль поверхности, без составляющей по нормали.
            tilt -= normalWS * dot(tilt, normalWS);
            return normalize(normalWS + tilt * _BumpStrength);
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
                float3 n = normalize(i.normalWS);
                GroundProjection g = ProjectGround(i.positionWS, n);
                half3 albedo = GroundColor(g.uv, g.index);
                float3 normalWS = ReliefNormal(g, n);

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
                float3 n = normalize(i.normalWS);
                float3 normalWS = ReliefNormal(ProjectGround(i.positionWS, n), n);
                return half4(NormalizeNormalPerPixel(normalWS), 0);
            }
            ENDHLSL
        }
    }
}
