// Safe Haven: a radial blur out from a great blow (ImpactBlurFeature, Docs/LookAndFeelResearch_2026-11-18.md V6). _ImpactBlur: xy the
// point on the screen (0-1) it streaks from, z how far a pixel's samples reach toward that point (a share of its distance from it).
// Twelve taps; the point itself stays sharp, the screen's edges streak most.
Shader "Hidden/Safe Haven/Impact Blur"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Impact Blur"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _ImpactBlur;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 towards = (_ImpactBlur.xy - uv) * _ImpactBlur.z;
                half4 sum = 0;
                [unroll]
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 11.0;
                    sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + towards * t, 0) * (1.0 - 0.5 * t);
                }

                return sum / 9.0;
            }
            ENDHLSL
        }
    }
}
