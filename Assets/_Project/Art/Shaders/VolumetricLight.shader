// Объёмный свет солнца: луч от камеры до поверхности проходит сквозь дымку, и в каждой точке
// проверяется карта теней солнца. Где солнце проходит сквозь просветы в кронах — дымка светится,
// в тени — нет. Так лучи появляются сами, из настоящей геометрии, без расставленных вручную плоскостей.
// Подключается через Full Screen Pass Renderer Feature перед tilt-shift.
Shader "ChroniclesOfRebirth/VolumetricLight"
{
    Properties
    {
        _Intensity("Intensity", Range(0, 8)) = 0.9
        _Anisotropy("Forward Scattering (g)", Range(0, 0.95)) = 0.45
        _Density("Haze Density", Range(0, 0.2)) = 0.014
        _HeightFalloff("Height Falloff (1/units)", Range(0, 1)) = 0.18
        _BaseHeight("Haze Base Height", Float) = 0
        _MaxDistance("Max Ray Distance", Float) = 45
        _Steps("Ray Steps", Range(8, 64)) = 32
        _Extinction("Extinction Of Scene Color", Range(0, 1)) = 0.2
        _HazeTint("Haze Tint", Color) = (1, 0.88, 0.7, 1)
        // Воздушная перспектива: дымка, освещённая небом, а не солнцем — холодная, видна и в тени.
        _AmbientHaze("Ambient Haze (rgb color, a strength)", Color) = (0.55, 0.65, 0.78, 0.25)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "VolumetricLight"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // Каскады теней включаются ключевым словом материала: глобальные ключевые слова теней
            // к моменту полноэкранного прохода уже могут быть сброшены.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _Anisotropy;
            float _Density;
            float _HeightFalloff;
            float _BaseHeight;
            float _MaxDistance;
            float _Steps;
            float _Extinction;
            half4 _HazeTint;
            half4 _AmbientHaze;

            // Фазовая функция Хеньи — Гринстейна: дымка ярче, когда смотришь в сторону солнца (контровой свет).
            float PhaseHG(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1 - g2) / (4 * PI * pow(abs(1 + g2 - 2 * g * cosTheta), 1.5));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 scene = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float rawDepth = SampleSceneDepth(uv);
                float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 cameraWS = GetCameraPositionWS();
                float3 ray = positionWS - cameraWS;
                float rayLength = min(length(ray), _MaxDistance);
                float3 direction = normalize(ray);

                int steps = (int)_Steps;
                float stepLength = rayLength / steps;
                // Сдвиг начала по шуму: вместо полос от шагов — мелкое зерно, которое сливается с плёночным.
                float jitter = InterleavedGradientNoise(uv * _ScreenParams.xy, 0);

                Light sun = GetMainLight();
                float cosTheta = dot(direction, sun.direction);
                float phase = lerp(1.0 / (4 * PI), PhaseHG(cosTheta, _Anisotropy), 0.85);

                float inscatter = 0;
                float transmittance = 1;
                for (int i = 0; i < steps; i++)
                {
                    float3 p = cameraWS + direction * ((i + jitter) * stepLength);
                    // Утренняя дымка гуще в низинах и тоньше наверху.
                    float density = _Density * exp(-max(p.y - _BaseHeight, 0) * _HeightFalloff);
                    half lit = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                    inscatter += transmittance * density * lit * stepLength;
                    transmittance *= exp(-density * stepLength);
                }

                half3 light = inscatter * phase * sun.color * _HazeTint.rgb * _Intensity * 4 * PI;
                half3 ambient = (1 - transmittance) * _AmbientHaze.rgb * _AmbientHaze.a;
                half3 color = scene.rgb * lerp(1, transmittance, _Extinction) + light + ambient;
                return half4(color, scene.a);
            }
            ENDHLSL
        }
    }
}
