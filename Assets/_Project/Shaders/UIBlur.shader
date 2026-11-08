// Safe Haven: the blurred picture of the game behind open windows (UIBlurFeature, UIBackdrop). Pass 0 takes the camera's
// finished image down with a 13-tap filter; passes 1 and 2 blur it across and down with a 9-tap Gaussian.
Shader "Hidden/Safe Haven/UI Blur"
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
        float4 _BlurTexel; // 1 / source width, 1 / source height

        half3 Tap(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0).rgb; }

        half3 Gauss(float2 uv, float2 step)
        {
            half3 sum = Tap(uv) * 0.2270270;
            sum += (Tap(uv + step * 1.3846153) + Tap(uv - step * 1.3846153)) * 0.3162162;
            sum += (Tap(uv + step * 3.2307692) + Tap(uv - step * 3.2307692)) * 0.0702702;
            return sum;
        }
        ENDHLSL

        Pass
        {
            Name "Down"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 t = _BlurTexel.xy;
                half3 sum = Tap(uv) * 0.125;
                sum += (Tap(uv + t * float2(-1, -1)) + Tap(uv + t * float2(1, -1)) + Tap(uv + t * float2(-1, 1)) + Tap(uv + t * float2(1, 1))) * 0.125;
                sum += (Tap(uv + t * float2(-2, -2)) + Tap(uv + t * float2(2, -2)) + Tap(uv + t * float2(-2, 2)) + Tap(uv + t * float2(2, 2))) * 0.03125;
                sum += (Tap(uv + t * float2(-2, 0)) + Tap(uv + t * float2(2, 0)) + Tap(uv + t * float2(0, -2)) + Tap(uv + t * float2(0, 2))) * 0.0625;
                return half4(sum, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Across"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target { return half4(Gauss(input.texcoord, float2(_BlurTexel.x, 0)), 1); }
            ENDHLSL
        }

        Pass
        {
            Name "Up"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target { return half4(Gauss(input.texcoord, float2(0, _BlurTexel.y)), 1); }
            ENDHLSL
        }
    }
}
