// Fire for torches, campfires, hearths, braziers and burning arrows: flames that lick upward and flicker, white-yellow
// at the heart, orange and then red at the tips, bright enough (HDR) for the bloom to glow round them. Drawn additively,
// so it lights what is behind it and never darkens. No textures: the flame is shaped from scrolling noise.
//
// Two ways to use it:
//  - On a mesh (the default): any rounded mesh, such as the Unity sphere the fires have always used, is drawn as a flame:
//    its top is pulled up and in into tongues that sway, its edge burns away, and its middle is the hottest.
//  - Flame From UV (keyword _FIRE_UV): the flame is shaped in the quad's own UVs (bottom middle the base), for particles
//    and camera-facing quads. Face Camera (_FIRE_BILLBOARD) turns a quad to the camera in the vertex shader.
// Every fire flickers out of step with the others (each is seeded by where it stands), and leans with the wind
// (MossWind's _SafeHavenWind). The light a fire gives is its Light (LightFlicker).
Shader "Safe Haven/Fire"
{
    Properties
    {
        [HDR] _CoreColor ("Heart", Color) = (1.0, 0.92, 0.62, 1)
        [HDR] _MidColor ("Body", Color) = (1.0, 0.62, 0.16, 1)
        [HDR] _EdgeColor ("Tips", Color) = (0.85, 0.2, 0.03, 1)
        _Intensity ("Brightness", Range(0, 12)) = 4
        _Height ("Flame Height (mesh)", Range(0, 3)) = 1.3
        _Taper ("Narrowing to the Tips (mesh)", Range(0, 1)) = 0.7
        _Sway ("Sway", Range(0, 0.5)) = 0.12
        _Speed ("Rise Speed", Range(0, 6)) = 1.6
        _Flicker ("Flicker", Range(0, 1)) = 0.25
        _NoiseScale ("Tongues (noise scale)", Range(0.5, 12)) = 3.2
        _Burn ("Edge Burn-away", Range(0, 1)) = 0.45
        [Toggle(_FIRE_UV)] _FireUV ("Flame From UV (particles, quads)", Float) = 0
        [Toggle(_FIRE_BILLBOARD)] _FireBillboard ("Face Camera (quads)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Sphere" }

        Pass
        {
            Name "Fire"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _FIRE_UV
            #pragma shader_feature_local _FIRE_BILLBOARD
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _MidColor;
                half4 _EdgeColor;
                half _Intensity;
                half _Height;
                half _Taper;
                half _Sway;
                half _Speed;
                half _Flicker;
                half _NoiseScale;
                half _Burn;
            CBUFFER_END
            float4 _SafeHavenWind; // horizontal direction XZ (world), moss displacement, speed

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 flame : TEXCOORD0;   // where in the flame: xz across, y up (0 base .. 1 tip), object scale
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half4 color : COLOR;
                float seed : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float FireHash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float FireNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(FireHash(i), FireHash(i + float3(1, 0, 0)), f.x),
                                 lerp(FireHash(i + float3(0, 1, 0)), FireHash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(FireHash(i + float3(0, 0, 1)), FireHash(i + float3(1, 0, 1)), f.x),
                                 lerp(FireHash(i + float3(0, 1, 1)), FireHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            // Tongues rising: noise scrolling upward, two octaves, the fine one faster.
            float FireTongues(float3 p, float time)
            {
                float rise = time * _Speed;
                float a = FireNoise(p * _NoiseScale - float3(0, rise * 1.0, 0));
                float b = FireNoise(p * _NoiseScale * 2.3 - float3(0, rise * 1.9, 0) + 7.1);
                return a * 0.65 + b * 0.35;
            }

            // How bright the whole fire is now: a slow breath and a quick flutter.
            float FireFlicker(float time, float seed)
            {
                float slow = FireNoise(float3(time * 1.7 + seed * 13.0, seed, 0));
                float quick = FireNoise(float3(time * 9.0 + seed * 7.0, 3.0, seed));
                return 1.0 - _Flicker + _Flicker * 2.0 * (slow * 0.6 + quick * 0.4);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                output.seed = frac(dot(origin, float3(0.1031, 0.1137, 0.0973)) * 43.758);
                float time = _Time.y + output.seed * 31.0;
                float2 wind = _SafeHavenWind.xy * saturate(_SafeHavenWind.z / 0.06);
                float3 positionOS = input.positionOS.xyz;
                float3 positionWS;
            #if defined(_FIRE_UV)
                // The quad's UVs are the flame: x across (-0.5..0.5), y up (0 base, 1 tip).
                output.flame = float3(input.uv.x - 0.5, input.uv.y, 0);
                #if defined(_FIRE_BILLBOARD)
                    float3 right = UNITY_MATRIX_V[0].xyz;
                    float3 up = float3(0, 1, 0);
                    float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), 1);
                    positionWS = origin + right * (positionOS.x * scale.x) + up * (positionOS.y * scale.y);
                #else
                    positionWS = TransformObjectToWorld(positionOS);
                #endif
                float leanUV = saturate(input.uv.y);
                positionWS.xz += (wind * 0.25 + float2(sin(time * 2.3), cos(time * 1.7)) * _Sway * 0.3) * leanUV * leanUV;
                output.normalWS = -GetWorldSpaceNormalizeViewDir(positionWS);
            #else
                // A rounded mesh (a sphere of radius 0.5): its top half rises into the flame and narrows to the tips.
                float up01 = saturate(positionOS.y / 0.5);
                float pull = up01 * up01;
                positionOS.y += pull * _Height * 0.5 * (0.85 + 0.3 * FireNoise(float3(positionOS.xz * 4.0, time * 2.0)));
                positionOS.xz *= 1.0 - _Taper * pull;
                float3 swayOS = float3(sin(time * 2.3 + positionOS.y * 5.0), 0, cos(time * 1.9 + positionOS.y * 4.0)) * _Sway * pull;
                positionOS += swayOS;
                positionWS = TransformObjectToWorld(positionOS);
                positionWS.xz += wind * 0.15 * pull;
                output.flame = float3(positionOS.x, saturate((positionOS.y + 0.5) / (1.0 + _Height * 0.5)), positionOS.z);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            #endif
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float time = _Time.y + input.seed * 31.0;
                float3 p = input.flame;
                float tongues = FireTongues(float3(p.x, p.y, p.z) + input.seed * 5.0, time);
            #if defined(_FIRE_UV)
                // A teardrop: wide and round at the base, narrowing to a wavering tip.
                // Soft all round: a smooth falloff across, rounded at the base and fading out into the tip.
                float width = lerp(0.4, 0.06, pow(saturate(p.y), 0.9));
                float across = abs(p.x + (tongues - 0.5) * 0.2 * p.y) / max(width, 0.01);
                float body = saturate(1.0 - across * across) * smoothstep(0.0, 0.18, p.y) * (1.0 - smoothstep(0.45, 1.0, p.y));
                float heat = body * (1.2 - p.y * 0.7) + (tongues - 0.5) * 0.5 * body;
            #else
                // Hottest where the flame faces the camera, burning away at its edge and towards the tips.
                float facing = saturate(dot(normalize(input.normalWS), GetWorldSpaceNormalizeViewDir(input.positionWS)));
                float heat = facing * facing * (1.15 - p.y * 0.9) + (tongues - 0.5) * (0.85 + p.y * 0.9) - _Burn * p.y * 0.5;
            #endif
                heat *= FireFlicker(time, input.seed);
            #if defined(_FIRE_UV)
                float alpha = smoothstep(0.02, 0.55, heat); // soft edges on particles
            #else
                float alpha = smoothstep(0.06, 0.4, heat);
            #endif
                clip(alpha - 0.002);
                half3 colour = heat > 0.6 ? lerp(_MidColor.rgb, _CoreColor.rgb, saturate((heat - 0.6) / 0.5))
                                          : lerp(_EdgeColor.rgb, _MidColor.rgb, saturate((heat - 0.15) / 0.45));
                colour *= _Intensity * alpha * input.color.rgb * input.color.a;
                colour = MixFogColor(colour, half3(0, 0, 0), input.fog); // additive: fog takes it away rather than tinting it
                return half4(colour, 1);
            }
            ENDHLSL
        }
    }
}
