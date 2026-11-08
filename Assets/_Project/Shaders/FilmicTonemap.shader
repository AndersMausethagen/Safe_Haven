// Safe Haven: the curves of FilmicTonemapping (TonemapFeature). Scene-linear light in, linear light in the screen's range
// (0-1) out, before URP's post-processing. Pass 0: Khronos PBR Neutral. Pass 1: AgX. Pass 2: AgX Punchy.
// AgX after Benjamin Wrensch's fit of Troy Sobotka's curve (sRGB primaries); PBR Neutral after the Khronos reference.
Shader "Hidden/Safe Haven/Filmic Tonemap"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _TonemapExposure;
        float _TonemapSaturation;
        float _TonemapContrast;
        float4 _TonemapFilter;

        float3 Saturate(float3 color, float amount)
        {
            float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
            return max(0.0, luma + (color - luma) * amount);
        }

        float3 PbrNeutral(float3 color)
        {
            const float startCompression = 0.8 - 0.04;
            const float desaturation = 0.15;
            float x = min(color.r, min(color.g, color.b));
            float offset = x < 0.08 ? x - 6.25 * x * x : 0.04;
            color -= offset;
            float peak = max(color.r, max(color.g, color.b));
            if (peak < startCompression)
            {
                return color;
            }

            const float d = 1.0 - startCompression;
            float newPeak = 1.0 - d * d / (peak + d - startCompression);
            color *= newPeak / peak;
            float g = 1.0 - 1.0 / (desaturation * (peak - newPeak) + 1.0);
            return lerp(color, newPeak.xxx, g);
        }

        float3 AgXContrast(float3 x)
        {
            float3 x2 = x * x;
            float3 x4 = x2 * x2;
            return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
        }

        // Rows are the reference's columns: mul(v, M) is its M * v.
        static const float3x3 AgXIn = float3x3(
            0.842479062253094, 0.0423282422610123, 0.0423756549057051,
            0.0784335999999992, 0.878468636469772, 0.0784336,
            0.0792237451477643, 0.0791661274605434, 0.879142973793104);
        static const float3x3 AgXOut = float3x3(
            1.19687900512017, -0.0528968517574562, -0.0529716355144438,
            -0.0980208811401368, 1.15190312990417, -0.0980434501171241,
            -0.0990297440797205, -0.0989611768448433, 1.15107367264116);

        float3 AgX(float3 color, bool punchy)
        {
            const float minEv = -12.47393;
            const float maxEv = 4.026069;
            color = mul(max(color, 1e-10), AgXIn);
            color = clamp(log2(color), minEv, maxEv);
            color = (color - minEv) / (maxEv - minEv);
            color = AgXContrast(color);
            if (punchy)
            {
                // The reference's Punchy look, in the curve's display space.
                color = pow(max(color, 0.0), 1.35);
                color = Saturate(color, 1.4);
            }

            color = mul(color, AgXOut);
            return pow(saturate(color), 2.2);
        }

        float3 Source(float2 uv)
        {
            float3 color = max(0.0, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv).rgb * _TonemapExposure * _TonemapFilter.rgb);
            // Contrast round middle grey, in stops (a power curve keeps black black and grey grey).
            return 0.18 * pow(color / 0.18, _TonemapContrast);
        }
        ENDHLSL

        Pass
        {
            Name "Neutral"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(Saturate(PbrNeutral(Source(input.texcoord)), _TonemapSaturation), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "AgX"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(Saturate(AgX(Source(input.texcoord), false), _TonemapSaturation), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "AgX Punchy"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                return half4(Saturate(AgX(Source(input.texcoord), true), _TonemapSaturation), 1.0);
            }
            ENDHLSL
        }
    }
}
