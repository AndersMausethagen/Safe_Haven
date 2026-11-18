// Safe Haven: screen-space contact shadows (Docs/LookAndFeelResearch_2026-11-18.md 3.2; ContactShadowsFeature). The sun's shadow map
// can't see pebbles, grass clumps, a foot or a log's underside from 20-30 m up: here each sunlit pixel marches the depth buffer a short
// way toward the sun, and if something stands in the way the pixel darkens by Strength, multiplied onto the lit scene after opaques
// (before water, fog and particles). Written from the long-known technique (a depth-buffer ray march toward the light), not from any
// published code. _ContactShadowParams: x strength, y metres marched, z thickness (metres behind a surface still counted as solid),
// w the camera distance beyond which it fades out.
Shader "Hidden/Safe Haven/Contact Shadows"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend DstColor Zero

        Pass
        {
            Name "Contact Shadows"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define CONTACT_STEPS 14

            float4 _ContactShadowParams;
            // The terrain under the view (GroundMarks.BindGround): only what lies on the ground takes a contact shadow (the ground, a
            // pebble, a foot, the base of a clump); leaves and blades higher up would shade themselves and darken every plant.
            TEXTURE2D(_GroundMarkHeightmap); SAMPLER(sampler_GroundMark_linear_clamp);
            float4 _GroundMarkPlace;
            float4 _GroundMarkScale;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 1e-6) return 1;
                #else
                    if (raw >= 1.0 - 1e-6) return 1;
                    raw = lerp(UNITY_NEAR_CLIP_VALUE, 1, raw);
                #endif
                float3 world = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float3 toSun = _MainLightPosition.xyz;
                float eye = LinearEyeDepth(raw, _ZBufferParams);
                half fade = 1.0 - smoothstep(_ContactShadowParams.w * 0.7, _ContactShadowParams.w, eye);
                if (toSun.y < 0.05 || fade <= 0.0) return 1;

                UNITY_BRANCH if (_GroundMarkScale.w > 0.5)
                {
                    float2 heightUV = (world.xz - _GroundMarkPlace.xy) * _GroundMarkPlace.zw + _GroundMarkScale.z;
                    if (all(heightUV > 0.0) && all(heightUV < 1.0))
                    {
                        float ground = _GroundMarkScale.x + SAMPLE_TEXTURE2D_LOD(_GroundMarkHeightmap, sampler_GroundMark_linear_clamp, heightUV, 0).r * _GroundMarkScale.y;
                        fade *= 1.0 - smoothstep(0.08, 0.2, world.y - ground);
                        if (fade <= 0.0) return 1;
                    }
                }

                // Only where the sun reaches: the shadow map already shades the rest.
                half lit = MainLightRealtimeShadow(TransformWorldToShadowCoord(world));
                if (lit < 0.05) return 1;

                // Steps grow toward the end (fine near the surface, where contact matters most); the depth's own error grows with distance.
                float bias = 0.02 + eye * 0.0015;
                half occluded = 0;
                [loop] for (int s = 1; s <= CONTACT_STEPS; s++)
                {
                    float share = s / (float)CONTACT_STEPS;
                    float3 p = world + toSun * (0.04 + share * share * _ContactShadowParams.y);
                    float4 clip = TransformWorldToHClip(p);
                    float2 at = clip.xy / clip.w * 0.5 + 0.5;
                    #if UNITY_UV_STARTS_AT_TOP
                        at.y = 1.0 - at.y;
                    #endif
                    if (any(at < 0.0) || any(at > 1.0))
                    {
                        break;
                    }

                    float sceneEye = LinearEyeDepth(SampleSceneDepth(at), _ZBufferParams);
                    float behind = clip.w - sceneEye;
                    if (behind > bias && behind < _ContactShadowParams.z)
                    {
                        // Nearer occluders shade harder: a pebble's contact more than a stone a metre off.
                        occluded = 1.0 - share * 0.5;
                        break;
                    }
                }

                half shade = 1.0 - _ContactShadowParams.x * occluded * lit * fade;
                return half4(shade, shade, shade, 1);
            }
            ENDHLSL
        }
    }
}
