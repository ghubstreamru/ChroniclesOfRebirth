// Объёмный травяной покров слоями («мех», shell grass): один и тот же участок поверхности рисуется несколько раз,
// каждый слой чуть выше предыдущего. В каждом слое пиксельная клетка (1/32 юнита) — это срез «травинки»: если травинка
// в этой клетке ниже слоя, пиксель отбрасывается. Получаются настоящие объёмные пиксельные стебли и кочки, которые
// заслоняют друг друга, лежат на склонах и раздвигаются у ног героя.
// Тип земли (трава, сухостой, мох, листья на земле, редкие травинки в гальке) — из той же карты, что у шейдера земли,
// с теми же неровными границами. Номер слоя (_Layer) задаёт материал: слои — разные материалы одного шейдера.
// Цвет вершины: R — множитель высоты травы (низкая у троп и воды, нет на обрывах).
Shader "ChroniclesOfRebirth/PixelGrass"
{
    Properties
    {
        [NoScaleOffset] _GroundMap("Ground Type Map (R = type / 16)", 2D) = "black" {}
        [NoScaleOffset] _GrassRamp("Grass Ramp", 2D) = "white" {}
        [NoScaleOffset] _DryRamp("Dry Grass Ramp", 2D) = "white" {}
        [NoScaleOffset] _MossRamp("Moss Ramp", 2D) = "white" {}
        [NoScaleOffset] _LeafRamp("Fallen Leaves Ramp", 2D) = "white" {}
        _MapRect("Map Rect (minX, minZ, width, depth)", Vector) = (0, 0, 1, 1)
        _MapTexel("Map Texel (1/cols, 1/rows, cols, rows)", Vector) = (1, 1, 1, 1)
        _TexelsPerUnit("Pixels Per Unit", Float) = 32
        _EdgeNoise("Border Jitter (units)", Range(0, 0.6)) = 0.32
        _EdgeDetail("Fine Border Jitter (units)", Range(0, 0.3)) = 0.09

        _Layer("Shell Layer (0..1)", Range(0, 1)) = 0.5
        _GrassHeight("Max Grass Height (units)", Float) = 0.42
        _WindStrength("Wind Strength", Range(0, 1)) = 0.25
        _WindSpeed("Wind Speed", Float) = 1.1
        _PushRadius("Push Radius Around Hero", Float) = 0.75
        _Translucency("Translucency (back light)", Range(0, 2)) = 0.6
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

        TEXTURE2D(_GrassRamp);
        SAMPLER(sampler_GrassRamp);
        TEXTURE2D(_DryRamp);
        TEXTURE2D(_MossRamp);
        TEXTURE2D(_LeafRamp);

        CBUFFER_START(UnityPerMaterial)
            float4 _MapRect;
            float4 _MapTexel;
            float _TexelsPerUnit;
            float _EdgeNoise;
            float _EdgeDetail;
            float _Layer;
            float _GrassHeight;
            half _WindStrength;
            half _WindSpeed;
            half _PushRadius;
            half _Translucency;
        CBUFFER_END

        // Позиция героя от CanopyFadeController (w = 1, если активен).
        float4 _CoR_PlayerPosition;

        #include "GroundCommon.hlsl"

        struct GrassAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        // Слой поднимается над поверхностью; верхние слои гнутся ветром и расходятся в стороны вокруг героя.
        // baseWS — точка поверхности под слоем: по ней читается узор травинок, поэтому стебель гнётся целиком.
        float3 ShellPositionWS(GrassAttributes v, out float3 baseWS, out half3 normalWS)
        {
            baseWS = TransformObjectToWorld(v.positionOS.xyz);
            normalWS = TransformObjectToWorldNormal(v.normalOS);
            float lift = _Layer * _GrassHeight;
            float3 grow = normalize(normalWS + float3(0, 1, 0));
            float3 positionWS = baseWS + grow * lift;

            float bend = _Layer * _Layer;
            float t = _Time.y * _WindSpeed;
            float phase = dot(baseWS.xz, float2(0.35, 0.27));
            float sway = sin(t + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.4;
            positionWS.xz += float2(0.8, 0.5) * (sway * _WindStrength * bend * _GrassHeight);

            if (_CoR_PlayerPosition.w > 0.5)
            {
                float2 away = baseWS.xz - _CoR_PlayerPosition.xz;
                float awayLength = length(away);
                float push = saturate(1 - awayLength / _PushRadius) * step(abs(baseWS.y - _CoR_PlayerPosition.y), 1.5);
                positionWS.xz += away / max(awayLength, 1e-3) * (push * _Layer * _GrassHeight * 1.2);
                positionWS.y -= push * lift * 0.6;
            }
            return positionWS;
        }

        half3 Ramp(TEXTURE2D_PARAM(ramp, rampSampler), float t)
        {
            return SAMPLE_TEXTURE2D_LOD(ramp, rampSampler, float2(clamp(t, 0.02, 0.98), 0.5), 0).rgb;
        }

        // Покров по типу земли: x — густота, y — высота (доля _GrassHeight), z — доля опавших листьев, w — 1 = мох.
        float4 CoverParams(float index)
        {
            if (index == GROUND_GRASS) return float4(0.95, 1.0, 0.0, 0);
            if (index == GROUND_GRASS_LEAVES) return float4(0.85, 0.8, 0.2, 0);
            if (index == GROUND_FOREST_FLOOR) return float4(0.8, 0.4, 0.12, 1);
            if (index == GROUND_PEBBLES) return float4(0.12, 0.65, 0.0, 0);
            if (index == GROUND_DIRT) return float4(0.05, 0.45, 0.03, 0);
            if (index == GROUND_FLAGSTONE) return float4(0.05, 0.4, 0.0, 0);
            if (index == GROUND_ROCK) return float4(0.08, 0.33, 0.0, 1);
            return 0;
        }

        // Есть ли в этом слое травинка (или лист) в пиксельной клетке точки xz; albedo — её цвет, tip — высота среза
        // от корня (0) до кончика (1).
        bool GrassAt(float2 xz, float heightScale, out half3 albedo, out half tip)
        {
            albedo = 0;
            tip = 0;
            float2 texel = floor(xz * _TexelsPerUnit);
            float2 snapped = (texel + 0.5) / _TexelsPerUnit;
            float4 cover = CoverParams(GroundIndex(xz));
            if (cover.x <= 0 && cover.z <= 0)
                return false;

            // Опавшие листья: пятнышки 2×2 пикселя в нижнем слое, поверх мха и травы у земли.
            float2 leafCell = floor(texel * 0.5);
            if (_Layer <= 0.1 && Hash21(leafCell + 7.3) < cover.z)
            {
                albedo = Ramp(TEXTURE2D_ARGS(_LeafRamp, sampler_GrassRamp), 0.3 + Hash21(leafCell + 3.1) * 0.6);
                tip = 0.7;
                return true;
            }

            // Кочки: в центре кочки травинки гуще и выше, между кочками — ниже и реже, видна земля.
            float clump = ValueNoise(snapped * 2.8) * 0.65 + ValueNoise(snapped * 7.5 + 11.0) * 0.35;
            float density = cover.x * saturate(clump * 1.6 - 0.1);
            float height = cover.y * heightScale * saturate(0.25 + clump) * (0.55 + 0.45 * Hash21(texel + 19.1));
            if (Hash21(texel) > density || _Layer > height)
                return false;

            tip = saturate(_Layer / max(height, 1e-3));
            // Сухостой пятнами: в одних местах почти весь луг жёлтый, в других — редкие сухие стебли.
            float dryPatch = ValueNoise(snapped * 0.55 + 4.2);
            bool dry = cover.w < 0.5 && Hash21(texel + 5.7) < lerp(0.08, 0.55, smoothstep(0.35, 0.75, dryPatch));
            float tone = 0.12 + 0.72 * tip + (Hash21(texel + 2.3) - 0.5) * 0.22;
            if (cover.w > 0.5)
                albedo = Ramp(TEXTURE2D_ARGS(_MossRamp, sampler_GrassRamp), tone);
            else if (dry)
                albedo = Ramp(TEXTURE2D_ARGS(_DryRamp, sampler_GrassRamp), tone);
            else
                albedo = Ramp(TEXTURE2D_ARGS(_GrassRamp, sampler_GrassRamp), tone);
            return true;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

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
                float3 baseWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half heightScale : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(GrassAttributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 baseWS;
                half3 normalWS;
                o.positionWS = ShellPositionWS(v, baseWS, normalWS);
                o.baseWS = baseWS;
                o.normalWS = normalWS;
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.heightScale = v.color.r;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo;
                half tip;
                if (!GrassAt(i.baseWS.xz, i.heightScale, albedo, tip))
                    clip(-1);

                // Травинка освещается почти как земля под ней, но чуть «вверх»: покров читается единой массой.
                float3 normalWS = normalize(lerp(normalize(i.normalWS), float3(0, 1, 0), 0.45));

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
                // У корней темнее: стебли затеняют друг друга.
                surface.occlusion = lerp(0.5, 1, tip);
                surface.normalTS = half3(0, 0, 1);

                half4 color = UniversalFragmentPBR(input, surface);

                // Просвечивание: кончики травы против солнца светятся тёплым — контровой свет утра.
                Light mainLight = GetMainLight(input.shadowCoord);
                half back = pow(saturate(dot(-input.viewDirectionWS, mainLight.direction)), 3);
                color.rgb += albedo * mainLight.color * (back * _Translucency * tip * mainLight.shadowAttenuation);

                color.rgb = MixFog(color.rgb, input.fogCoord);
                return half4(color.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 baseWS : TEXCOORD0;
                half heightScale : TEXCOORD1;
            };

            Varyings Vert(GrassAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 baseWS;
                half3 normalWS;
                o.positionCS = TransformWorldToHClip(ShellPositionWS(v, baseWS, normalWS));
                o.baseWS = baseWS;
                o.heightScale = v.color.r;
                return o;
            }

            half Frag(Varyings i) : SV_Target
            {
                half3 albedo;
                half tip;
                if (!GrassAt(i.baseWS.xz, i.heightScale, albedo, tip))
                    clip(-1);
                return 0;
            }
            ENDHLSL
        }

        // Глубина и нормали для SSAO: затенение между кочками и у корней.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 baseWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half heightScale : TEXCOORD2;
            };

            Varyings Vert(GrassAttributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 baseWS;
                half3 normalWS;
                o.positionCS = TransformWorldToHClip(ShellPositionWS(v, baseWS, normalWS));
                o.baseWS = baseWS;
                o.normalWS = normalWS;
                o.heightScale = v.color.r;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo;
                half tip;
                if (!GrassAt(i.baseWS.xz, i.heightScale, albedo, tip))
                    clip(-1);
                float3 normalWS = normalize(lerp(normalize(i.normalWS), float3(0, 1, 0), 0.45));
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
    }
}
