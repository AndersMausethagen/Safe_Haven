// Safe Haven: the mist's fluid (MistWakes): Stable Fluids (Stam 1999; keijiro/StableFluids as the user's guide) on a top-down square that
// follows the player. The mist creeps through it on a slow background flow (_WakeCreep), round whatever stands in its way (trees, rocks,
// walls: solid cells) and stirred by every body moving through it. Six passes a frame:
//   5 Solid: what stands in the mist's way, from the obstacle list (1 inside, soft at the rim).
//   0 Velocity: carried along by itself, relaxing back to the creep, pushed by the stirring bodies, nothing inside a solid (m/s).
//   1 Divergence, 2 Pressure (Jacobi, many times; mirrored at a solid's face), 3 Project: the velocity made free of divergence, so a push
//     turns into swirls round the body and the creep parts round an obstacle rather than running into it.
//   4 State: r = how much the mist is parted (carried by the flow, filling back in; parted behind an obstacle as the creep leaves its lee),
//     gb = how far the mist's pattern has been carried off its creep (metres, the noise is read that much upstream), relaxing back so the
//     mist heals into its own shapes.
// Each pass reads last frame shifted with the square (_MistWakeShift) where it reads its own field. The fields a pass reads are globals
// (_MistWake…), set between passes in MistWakes' one command buffer. Outside the square the air creeps on
// undisturbed: velocity = the creep, nothing parted.
Shader "Hidden/Safe Haven/Mist Wake"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #define MAX_STAMPS 64
        #define MAX_OBSTACLES 96

        // Inline sampler states (named for their filter and wrap), so nothing depends on which pipeline include declares the shared ones.
        SamplerState sampler_wake_linear_clamp;
        SamplerState sampler_wake_point_clamp;

        TEXTURE2D(_MistWakeVelocity);
        TEXTURE2D(_MistWakePressure);
        TEXTURE2D(_MistWakeDivergence);
        TEXTURE2D(_MistWakeState);
        TEXTURE2D(_MistWakeSolid);
        float4 _WakeTexel;                  // 1 / width, 1 / height, metres a texel
        float4 _MistWakeShift;                  // xy: how far the square moved, in uv (applied where a pass reads its own last frame)
        float4 _WakeSim;                    // dt, fill rate, settle rate, velocity damping
        float4 _WakeRect;                   // min x, min z, size, stir strength
        float4 _WakeCreep;                  // the background flow (world x, z, m/s), how much an obstacle parts the mist, 0
        float _StampCount;                  // a float: Material.SetFloat (an int uniform read a float's bits)
        float4 _Stamps[MAX_STAMPS];         // x, z, radius, strength
        float4 _StampVelocities[MAX_STAMPS];// world x, z velocity
        float _ObstacleCount;
        float4 _Obstacles[MAX_OBSTACLES];      // x, z, half width along its own x, half width along its own z (metres)
        float4 _ObstacleShapes[MAX_OBSTACLES]; // cos, sin of its turn; roundness (2: an ellipse, higher: a box); 0

        struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }

        bool Inside(float2 uv) { return all(uv > 0.0) && all(uv < 1.0); }

        float4 Read(TEXTURE2D_PARAM(tex, samp), float2 uv)
        {
            return Inside(uv) ? SAMPLE_TEXTURE2D_LOD(tex, samp, uv, 0) : 0.0;
        }

        // Beyond the square the air creeps on undisturbed.
        float2 ReadVelocity(float2 uv)
        {
            return Inside(uv) ? SAMPLE_TEXTURE2D_LOD(_MistWakeVelocity, sampler_wake_linear_clamp, uv, 0).xy : _WakeCreep.xy;
        }

        #define VELOCITY(uv) ReadVelocity(uv)
        #define PRESSURE(uv) Read(TEXTURE2D_ARGS(_MistWakePressure, sampler_wake_point_clamp), uv).x
        #define STATE(uv) Read(TEXTURE2D_ARGS(_MistWakeState, sampler_wake_linear_clamp), uv)
        #define SOLID(uv) Read(TEXTURE2D_ARGS(_MistWakeSolid, sampler_wake_point_clamp), uv).x

        float2 World(float2 uv) { return _WakeRect.xy + uv * _WakeRect.z; }

        float StampWeight(int s, float2 world, out float2 outward)
        {
            float4 stamp = _Stamps[s];
            float2 offset = world - stamp.xy;
            float radius = max(0.05, stamp.z);
            float distance = length(offset);
            outward = distance > 0.001 ? offset / distance : 0.0;
            return exp(-dot(offset, offset) / (radius * radius)) * stamp.w;
        }

        // A neighbour's pressure (read at pressureUv: last frame's, shifted, on a frame's first step), or this cell's own where the
        // neighbour is solid this frame (no flow through a solid's face).
        float PressureBeside(float2 solidUv, float2 pressureUv, float own)
        {
            return SOLID(solidUv) > 0.5 ? own : PRESSURE(pressureUv);
        }
        ENDHLSL

        // 0: velocity, carried by itself, relaxing back to the creep, stirred; still inside a solid.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                float dt = _WakeSim.x;
                float2 uv = input.uv + _MistWakeShift.xy;
                float2 here = VELOCITY(uv);
                float2 carried = VELOCITY(uv - here * dt / _WakeRect.z);
                float2 velocity = _WakeCreep.xy + (carried - _WakeCreep.xy) * exp(-dt * _WakeSim.w);
                float2 world = World(input.uv);
                int count = min((int)_StampCount, MAX_STAMPS);
                for (int s = 0; s < count; s++)
                {
                    float2 outward;
                    float weight = StampWeight(s, world, outward);
                    float radius = max(0.05, _Stamps[s].z);
                    // Dragged along the way the body goes, and pushed aside from it.
                    velocity += (_StampVelocities[s].xy * 1.6 + outward * radius * 3.0) * (weight * _WakeRect.w * dt * 6.0);
                }

                float speed = length(velocity);
                if (speed > 16.0)
                    velocity *= 16.0 / speed;
                velocity *= 1.0 - SOLID(input.uv);
                return half4(velocity, 0.0, 0.0);
            }
            ENDHLSL
        }

        // 1: divergence of the velocity.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                float2 t = _WakeTexel.xy;
                float right = VELOCITY(input.uv + float2(t.x, 0.0)).x;
                float left = VELOCITY(input.uv - float2(t.x, 0.0)).x;
                float up = VELOCITY(input.uv + float2(0.0, t.y)).y;
                float down = VELOCITY(input.uv - float2(0.0, t.y)).y;
                return half4((right - left + up - down) / (2.0 * _WakeTexel.z), 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }

        // 2: one Jacobi step of the pressure (the first of a frame reads last frame's, shifted with the square, as its start).
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                float2 t = _WakeTexel.xy;
                float2 uv = input.uv + _MistWakeShift.xy;
                float own = PRESSURE(uv);
                float2 here = input.uv;
                float sum = PressureBeside(here + float2(t.x, 0.0), uv + float2(t.x, 0.0), own) + PressureBeside(here - float2(t.x, 0.0), uv - float2(t.x, 0.0), own)
                    + PressureBeside(here + float2(0.0, t.y), uv + float2(0.0, t.y), own) + PressureBeside(here - float2(0.0, t.y), uv - float2(0.0, t.y), own);
                float divergence = SAMPLE_TEXTURE2D_LOD(_MistWakeDivergence, sampler_wake_point_clamp, input.uv, 0).x;
                return half4((sum - divergence * _WakeTexel.z * _WakeTexel.z) * 0.25, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }

        // 3: the velocity less the pressure's gradient: free of divergence, it swirls, and goes round a solid.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                float2 t = _WakeTexel.xy;
                float own = PRESSURE(input.uv);
                float2 gradient = float2(
                    PressureBeside(input.uv + float2(t.x, 0.0), input.uv + float2(t.x, 0.0), own) - PressureBeside(input.uv - float2(t.x, 0.0), input.uv - float2(t.x, 0.0), own),
                    PressureBeside(input.uv + float2(0.0, t.y), input.uv + float2(0.0, t.y), own) - PressureBeside(input.uv - float2(0.0, t.y), input.uv - float2(0.0, t.y), own)) / (2.0 * _WakeTexel.z);
                float2 velocity = SAMPLE_TEXTURE2D_LOD(_MistWakeVelocity, sampler_wake_point_clamp, input.uv, 0).xy - gradient;
                velocity *= 1.0 - SOLID(input.uv);
                return half4(velocity, 0.0, 0.0);
            }
            ENDHLSL
        }

        // 4: the mist's state, carried by the flow: parted (r), and how far its pattern was carried off its creep (gb, metres).
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                float dt = _WakeSim.x;
                float2 velocity = SAMPLE_TEXTURE2D_LOD(_MistWakeVelocity, sampler_wake_linear_clamp, input.uv, 0).xy;
                float2 from = input.uv + _MistWakeShift.xy - velocity * dt / _WakeRect.z;
                float4 state = STATE(from);
                state.r *= exp(-dt * _WakeSim.y);
                float solid = SOLID(input.uv);
                // The mist here came from upstream: its pattern is read that much further back (as far as the flow strayed from the creep
                // the pattern already drifts with), then heals toward its own place.
                state.gb = (state.gb + (velocity - _WakeCreep.xy) * (dt * (1.0 - solid))) * exp(-dt * _WakeSim.z);
                float carried = length(state.gb);
                if (carried > 6.0)
                    state.gb *= 6.0 / carried;

                // Nothing lies inside a solid; the creep carries that gap on into the obstacle's lee, where it fills back in.
                state.r = max(state.r, solid * _WakeCreep.z);
                float2 world = World(input.uv);
                int count = min((int)_StampCount, MAX_STAMPS);
                for (int s = 0; s < count; s++)
                {
                    float2 outward;
                    state.r = max(state.r, StampWeight(s, world, outward));
                }

                state.a = 1.0;
                return state;
            }
            ENDHLSL
        }

        // 5: what stands in the mist's way: trees, rocks, walls (MistWakes' obstacle list).
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            half4 Frag(Varyings input) : SV_Target
            {
                float2 world = World(input.uv);
                float solid = 0.0;
                int count = min((int)_ObstacleCount, MAX_OBSTACLES);
                for (int o = 0; o < count; o++)
                {
                    float4 obstacle = _Obstacles[o];
                    float4 shape = _ObstacleShapes[o];
                    float2 offset = world - obstacle.xy;
                    // Into the obstacle's own axes, then a superellipse: 2 an ellipse, higher a box with rounded corners.
                    float2 local = abs(float2(offset.x * shape.x + offset.y * shape.y, -offset.x * shape.y + offset.y * shape.x))
                        / max(obstacle.zw, 0.05);
                    float roundness = max(1.0, shape.z);
                    float reach = pow(pow(local.x, roundness) + pow(local.y, roundness), 1.0 / roundness);
                    solid = max(solid, 1.0 - smoothstep(0.8, 1.0, reach));
                }

                return half4(solid, 0.0, 0.0, 0.0);
            }
            ENDHLSL
        }
    }
}
