// A firefly: a hot point in a soft halo, in the particle's colour, its alpha how lit it is. No texture to load; Intensity
// goes above 1 so bloom picks it up.
Shader "Safe Haven/Firefly"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "Firefly"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half distanceFromMiddle = saturate(length(input.uv - 0.5) * 2.0);
                half falloff = 1.0 - distanceFromMiddle;
                half glow = falloff * falloff * 0.35 + pow(falloff, 6.0) * 1.6;
                return half4(input.color.rgb * input.color.a * glow * _Intensity, 0.0);
            }
            ENDHLSL
        }
    }
}
