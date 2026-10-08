// Экранный tilt-shift в духе HD-2D: резкая горизонтальная полоса вокруг героя,
// плавно нарастающее размытие к верху и низу кадра. Подключается через Full Screen Pass Renderer Feature.
Shader "ChroniclesOfRebirth/TiltShift"
{
    Properties
    {
        _FocusCenter("Focus Center (0..1)", Range(0, 1)) = 0.5
        _FocusWidth("Sharp Band Half Width", Range(0, 0.5)) = 0.1
        _Falloff("Falloff", Range(0.01, 1)) = 0.32
        _MaxRadius("Max Blur Radius (px at 1080p)", Range(0, 20)) = 8
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
            Name "TiltShift"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _FocusCenter;
            float _FocusWidth;
            float _Falloff;
            float _MaxRadius;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float distanceFromFocus = abs(uv.y - _FocusCenter);
                float t = saturate((distanceFromFocus - _FocusWidth) / _Falloff);
                t = t * t * (3 - 2 * t);
                float radius = _MaxRadius * t * (_ScreenParams.y / 1080.0);
                if (radius < 0.5)
                    return center;

                // Диск из 24 точек по золотому углу — мягкое «боке» без полос.
                float2 texel = 1.0 / _ScreenParams.xy;
                half3 sum = center.rgb;
                [unroll]
                for (int k = 1; k < 24; k++)
                {
                    float r = sqrt(k / 23.0) * radius;
                    float a = k * 2.39996;
                    float2 offset = float2(cos(a), sin(a)) * r * texel;
                    sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, 0).rgb;
                }

                return half4(sum / 24.0, center.a);
            }
            ENDHLSL
        }
    }
}
