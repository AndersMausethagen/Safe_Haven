// Safe Haven: the cloud-shadow light cookie (CloudShadows). A tiling pattern of soft cumulus shapes: fractal noise whose
// every octave repeats across the tile, so the cookie wraps without a seam. 1 = full sun, lower = in a cloud's shadow.
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

            float Hash(float2 cell, float period)
            {
                cell = fmod(cell + period * 8.0, period);
                float3 p = frac(float3(cell.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float2 p, float period)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell, period), Hash(cell + float2(1, 0), period), f.x),
                            lerp(Hash(cell + float2(0, 1), period), Hash(cell + float2(1, 1), period), f.x), f.y);
            }

            float Clouds(float2 uv)
            {
                float sum = 0.0, weight = 0.5, total = 0.0;
                float period = 5.0;
                for (int i = 0; i < 6; i++)
                {
                    sum += Noise(uv * period, period) * weight;
                    total += weight;
                    weight *= 0.5;
                    period *= 2.0;
                }

                return sum / total;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float density = Clouds(input.uv);
                // Cover is the share under cloud: the noise's value above which a point is clear.
                float edge = lerp(0.68, 0.38, _CloudCover);
                float cloud = smoothstep(edge - _CloudSoftness * 0.5, edge + _CloudSoftness, density);
                return half4((1.0 - cloud * _CloudDarkness).xxx, 1.0);
            }
            ENDHLSL
        }
    }
}
