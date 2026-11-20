// One step of the WaterRipples height field (R = height, G = previous height): a damped wave equation, the area
// scrolled by _RippleShift texels when it follows the camera, plus stamps pushing the water down
// (_RippleStamps[i] = uv x, uv y, radius in uv, depth). Run with Graphics.Blit from WaterRipples.
// BA = drift: how far, in metres (world X, Z), what floats on the water (the leaves and duckweed "Safe Haven/Water" draws) has
// been pushed from where it lay. Each wave carries it the way the wave runs (-dh/dt * grad h points along the wave's travel,
// crest or trough), a stamp shoves it aside from the feet, and it spreads and settles back slowly (the user, 2026-11-20: the
// leaves on the water should move with physics).
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
            float4 _RippleStampMotion[MAX_STAMPS]; // xy: metres what made the stamp moved since its last (x, z)
            float _RippleStampCount;
            float4 _RippleDrift; // wave carry, the field's size in metres (uv to metres), settle per step, spread per step

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

            float4 Read(int2 p)
            {
                int2 size = int2(_RippleTexel.zw);
                if (any(p < 0) || any(p >= size))
                {
                    return 0;
                }
                return LOAD_TEXTURE2D(_MainTex, p);
            }

            float4 Step(Varyings input) : SV_Target
            {
                int2 p = int2(input.uv * _RippleTexel.zw) + int2(_RippleShift);
                float4 centre = Read(p);
                float4 east = Read(p + int2(1, 0)), west = Read(p - int2(1, 0)), north = Read(p + int2(0, 1)), south = Read(p - int2(0, 1));
                float around = east.r + west.r + north.r + south.r;
                float height = centre.r;
                // Drift: carried along the waves, spread a little (no tearing between neighbours), settling back.
                float2 slope = float2(east.r - west.r, north.r - south.r) * 0.5;
                float2 drift = lerp(centre.ba, (east.ba + west.ba + north.ba + south.ba) * 0.25, _RippleDrift.w);
                drift = drift * _RippleDrift.z - (height - centre.g) * slope * _RippleDrift.x;
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
                        // Shoved out from under the feet to just clear of them, no further however long they stand there
                        // (none dead centre, where it has no side). Metres = uv * the field's size (_RippleTexel.z texels).
                        float2 away = input.uv - stamp.xy;
                        float2 out_ = away / max(length(away), 1e-5);
                        float clear = (stamp.z - length(away)) * _RippleDrift.y * 1.2;
                        float pushed = dot(drift, out_);
                        drift += out_ * max(0.0, clear - pushed) * step(1e-4, length(away)) * 0.5;
                        // And dragged along the way it moves, most under its middle: a swimmer's bow pushes the leaves ahead.
                        drift += _RippleStampMotion[i].xy * (w * 0.8);
                    }
                }
                float most = 1.2;
                drift *= min(1.0, most / max(length(drift), 1e-5));
                return float4(clamp(next, -4.0, 4.0), height, drift);
            }
            ENDHLSL
        }
    }
}
