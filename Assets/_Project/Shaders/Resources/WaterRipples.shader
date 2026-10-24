// One step of the WaterRipples height field (R = height, G = previous height): a damped wave equation, the area
// scrolled by _RippleShift texels when it follows the camera, plus stamps pushing the water down
// (_RippleStamps[i] = uv x, uv y, radius in uv, depth). Run with Graphics.Blit from WaterRipples.
Shader "Hidden/Safe Haven/Water Ripples"
{
    Properties
    {
        // Graphics.Blit hands its source over as the main texture, which needs this property.
        [MainTexture] _MainTex ("Previous Step", 2D) = "black" {}
    }
    SubShader
    {
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Step
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define MAX_STAMPS 16
            TEXTURE2D(_MainTex);
            float4 _RippleTexel; // 1 / width, 1 / height, width, height (set by WaterRipples)
            float2 _RippleShift;
            float _RippleDamping, _RippleWaveSpeed;
            float4 _RippleStamps[MAX_STAMPS];
            float _RippleStampCount;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                // Graphics.Blit draws a 0..1 quad; map it straight to clip space (its matrices do not reach URP's
                // transforms) and flip into render textures, so texel row r always reads row r of the source.
                output.positionCS = float4(input.positionOS.xy * 2.0 - 1.0, 0.5, 1.0);
                if (_ProjectionParams.x < 0.0)
                {
                    output.positionCS.y = -output.positionCS.y;
                }
                output.uv = input.uv;
                return output;
            }

            float2 Read(int2 p)
            {
                int2 size = int2(_RippleTexel.zw);
                if (any(p < 0) || any(p >= size))
                {
                    return 0;
                }
                return LOAD_TEXTURE2D(_MainTex, p).rg;
            }

            float4 Step(Varyings input) : SV_Target
            {
                int2 p = int2(input.uv * _RippleTexel.zw) + int2(_RippleShift);
                float2 centre = Read(p);
                float around = Read(p + int2(1, 0)).r + Read(p - int2(1, 0)).r + Read(p + int2(0, 1)).r + Read(p - int2(0, 1)).r;
                float height = centre.r;
                float next = height + (height - centre.g) * _RippleDamping + _RippleWaveSpeed * (around - 4.0 * height);
                next *= 0.99; // the water level settles back
                int count = (int)min(_RippleStampCount, MAX_STAMPS);
                for (int i = 0; i < count; i++)
                {
                    float4 stamp = _RippleStamps[i];
                    float d = distance(input.uv, stamp.xy) / max(stamp.z, 1e-5);
                    if (d < 1.0)
                    {
                        float w = 1.0 - d * d;
                        next -= stamp.w * w * w;
                    }
                }
                return float4(clamp(next, -4.0, 4.0), height, 0, 0);
            }
            ENDHLSL
        }
    }
}
