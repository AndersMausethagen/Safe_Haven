// Safe Haven: the cloud-shadow light cookie (CloudShadows). The tiling cloud pattern of CloudNoise.hlsl, the same the sky
// draws, so the cookie wraps without a seam and matches the clouds overhead. 1 = full sun, lower = in a cloud's shadow.
Shader "Hidden/Safe Haven/Cloud Cookie"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CloudNoise.hlsl"

            float _CloudCover;
            float _CloudSoftness;
            float _CloudDarkness;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float cloud = CloudMask(CloudDensity(input.uv, 6), _CloudCover, _CloudSoftness);
                return half4((1.0 - cloud * _CloudDarkness).xxx, 1.0);
            }
            ENDHLSL
        }
    }
}
