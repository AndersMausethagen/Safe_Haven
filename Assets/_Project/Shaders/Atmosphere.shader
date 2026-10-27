// Volumetric fog and fog of war for AtmosphereFeature (see Docs/Atmosphere.md).
// Pass 0 "March": half resolution. Marches each view ray to the scene depth, gathering in-scattered light (sun or moon
//   through its shadow cascades, AtmosphereLights) and transmittance. Output: rgb = light, a = transmittance.
// Pass 1 "Composite": full resolution. Scene × transmittance + fog light, then the night veil beyond every visibility
//   source and the dark fog over unexplored ground.
Shader "Hidden/Safe Haven/Atmosphere"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #define MAX_FOG_LIGHTS 8
        #define MAX_SOURCES 16

        // density, base height, height falloff, anisotropy
        float4 _FogParams0;
        // main light strength, local light strength, 1 / noise size, noise strength
        float4 _FogParams1;
        // max distance, steps, clearing
        float4 _FogParams2;
        // follow terrain, 1 / bank size, bank strength, bank speed
        float4 _FogParams3;
        float4 _FogAlbedo;
        float4 _FogAmbient;
        float4 _FogWind;
        float _FogLightCount;
        float4 _FogLightPositions[MAX_FOG_LIGHTS];   // xyz, range
        float4 _FogLightColors[MAX_FOG_LIGHTS];      // linear rgb × intensity
        float _SourceCount;
        float4 _Sources[MAX_SOURCES];                // x, z, radius, softness
        #define MAX_FOG_AREAS 8
        float _FogAreaCount;
        float4 _FogAreas[MAX_FOG_AREAS];             // xyz centre, density
        float4 _FogAreaShapes[MAX_FOG_AREAS];        // 1 / radius, 1 / height, softness
        float4 _GroundRect;                          // x min, z min, 1 / width, 1 / depth
        float4 _GroundRange;                         // lowest height, span
        TEXTURE2D(_GroundTex);

        // unexplored strength, night veil strength, veil desaturation, veil darkening
        float4 _WarParams;
        float4 _UnexploredColor;
        float4 _VeilColor;
        float4 _ExploreRect;                         // x min, z min, 1 / width, 1 / depth
        float _WarGround;                            // height of the ground the player stands on
        TEXTURE2D(_ExploreTex);
        TEXTURE2D(_AtmosphereFog);
        float _SafeHavenNight;

        float3 WorldPositionAt(float2 uv, out float rawDepth)
        {
            rawDepth = SampleSceneDepth(uv);
            return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        }

        bool IsFar(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return rawDepth <= 0.000001;
        #else
            return rawDepth >= 0.999999;
        #endif
        }

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float ValueNoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                             lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                             lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        float FogNoise(float3 p)
        {
            return ValueNoise(p) * 0.65 + ValueNoise(p * 2.31 + 7.1) * 0.35;
        }

        // 1 inside a visibility source, fading to 0 at its edge; the strongest source wins.
        float Visibility(float2 xz)
        {
            float seen = 0.0;
            int count = (int)_SourceCount;
            for (int i = 0; i < count; i++)
            {
                float4 s = _Sources[i];
                float inner = s.z * (1.0 - s.w);
                seen = max(seen, 1.0 - smoothstep(inner, s.z, distance(xz, s.xy)));
            }
            return seen;
        }
        ENDHLSL

        Pass
        {
            Name "March"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMarch
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Ground height under a point, from FogGround's baked height field.
            float GroundHeight(float2 xz)
            {
                float2 uv = saturate((xz - _GroundRect.xy) * _GroundRect.zw);
                return _GroundRange.x + SAMPLE_TEXTURE2D_LOD(_GroundTex, sampler_LinearClamp, uv, 0).r * _GroundRange.y;
            }

            // Pockets of extra fog (FogArea): upright ellipsoids fading out over their edge.
            float AreaDensity(float3 p)
            {
                float density = 0.0;
                int count = (int)_FogAreaCount;
                for (int i = 0; i < count; i++)
                {
                    float4 area = _FogAreas[i];
                    float4 shape = _FogAreaShapes[i];
                    float3 local = (p - area.xyz) * float3(shape.x, shape.y, shape.x);
                    float d = length(local);
                    density += area.w * (1.0 - smoothstep(1.0 - shape.z, 1.0, d));
                }
                return density;
            }

            float Density(float3 p)
            {
                // The base lies at a fixed height, or follows the ground as a blanket.
                float baseHeight = _FogParams0.y;
                if (_FogParams3.x > 0.0)
                {
                    baseHeight = lerp(baseHeight, GroundHeight(p.xz) + _FogParams0.y, _FogParams3.x);
                }

                float above = max(0.0, p.y - baseHeight);
                float density = _FogParams0.x * exp(-above * _FogParams0.z);
                float3 drift = p * _FogParams1.z - _Time.y * _FogWind.xyz * _FogParams1.z;
                float patch = saturate(FogNoise(drift) * 1.7 - 0.25);
                density *= lerp(1.0, patch, _FogParams1.w);

                // Large banks rolling over the land: thick where the slow noise is high, clear stretches between.
                if (_FogParams3.z > 0.0)
                {
                    float2 bankUV = p.xz * _FogParams3.y - _Time.y * _FogWind.xz * _FogParams3.w * _FogParams3.y;
                    float bank = smoothstep(0.3, 0.7, ValueNoise(float3(bankUV, _Time.y * 0.004)));
                    density *= lerp(1.0, bank * 2.0, _FogParams3.z);
                }

                density += AreaDensity(p) * lerp(1.0, patch, _FogParams1.w * 0.5);
                density *= 1.0 - _FogParams2.z * Visibility(p.xz);
                return density;
            }

            // Henyey-Greenstein, scaled so isotropic scattering is 1.
            float Phase(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / pow(max(1e-4, 1.0 + g2 - 2.0 * g * cosTheta), 1.5);
            }

            float3 LocalLights(float3 p)
            {
                float3 light = 0.0;
                int count = (int)_FogLightCount;
                for (int i = 0; i < count; i++)
                {
                    float4 l = _FogLightPositions[i];
                    float3 offset = l.xyz - p;
                    float d2 = dot(offset, offset);
                    float range2 = l.w * l.w;
                    if (d2 < range2)
                    {
                        float fade = 1.0 - d2 / range2;
                        light += _FogLightColors[i].rgb * (fade * fade / (1.0 + d2 * 0.35));
                    }
                }
                return light;
            }

            float4 FragMarch(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float rawDepth;
                float3 positionWS = WorldPositionAt(uv, rawDepth);
                float3 origin = _WorldSpaceCameraPos;
                float3 ray = positionWS - origin;
                float sceneDistance = length(ray);
                float3 direction = ray / max(sceneDistance, 1e-4);
                float maxDistance = _FogParams2.x;
                float marchDistance = IsFar(rawDepth) ? maxDistance : min(sceneDistance, maxDistance);

                int steps = max(1, (int)_FogParams2.y);
                float stepLength = marchDistance / steps;
                // Interleaved gradient noise hides the banding of few steps as a fine, stable grain.
                float jitter = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));

                Light mainLight = GetMainLight();
                float phase = Phase(dot(direction, mainLight.direction), _FogParams0.w);
                float3 sunLight = mainLight.color * (phase * _FogParams1.x);

                float transmittance = 1.0;
                float3 gathered = 0.0;
                float3 p = origin + direction * (stepLength * jitter);
                for (int i = 0; i < steps; i++)
                {
                    float density = Density(p);
                    if (density > 1e-5)
                    {
                        float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                        float3 scatter = (_FogAmbient.rgb + sunLight * shadow + LocalLights(p) * _FogParams1.y) * _FogAlbedo.rgb;
                        float stepTransmittance = exp(-density * stepLength);
                        gathered += transmittance * (1.0 - stepTransmittance) * scatter;
                        transmittance *= stepTransmittance;
                        if (transmittance < 0.01)
                        {
                            break;
                        }
                    }
                    p += direction * stepLength;
                }

                return float4(gathered, transmittance);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            float4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float4 fog = SAMPLE_TEXTURE2D(_AtmosphereFog, sampler_LinearClamp, uv);
                color = color * fog.a + fog.rgb;

                if (_WarParams.x <= 0.0 && _WarParams.y <= 0.0)
                {
                    return float4(color, 1.0);
                }

                float rawDepth;
                float3 positionWS = WorldPositionAt(uv, rawDepth);
                float3 view = positionWS - _WorldSpaceCameraPos;
                float sceneDistance = length(view);
                float3 direction = view / max(sceneDistance, 1e-4);
                // Fog of war belongs to the ground: anything standing above it (a tree crown near the camera) takes the
                // ground behind it along the view ray, so it is never darker than what it stands over.
                float toGround = direction.y < -0.01 ? (_WarGround - _WorldSpaceCameraPos.y) / direction.y : -1.0;
                if (toGround > 0.0 && (IsFar(rawDepth) || sceneDistance < toGround))
                {
                    positionWS = _WorldSpaceCameraPos + direction * toGround;
                }
                else if (IsFar(rawDepth))
                {
                    positionWS = _WorldSpaceCameraPos + direction * 120.0;
                }

                // Night: what lies beyond every source of sight and light turns grey and dim.
                float veil = saturate(_WarParams.y * _SafeHavenNight * (1.0 - Visibility(positionWS.xz)));
                if (veil > 0.0)
                {
                    float luminance = dot(color, float3(0.299, 0.587, 0.114));
                    float tintLuminance = max(0.05, dot(_VeilColor.rgb, float3(0.299, 0.587, 0.114)));
                    float3 grey = luminance * _VeilColor.rgb / tintLuminance;
                    float3 veiled = lerp(color, grey, _WarParams.z) * (1.0 - _WarParams.w) + _VeilColor.rgb * _VeilColor.a;
                    color = lerp(color, veiled, veil);
                }

                // Any hour: ground never seen lies under dark, slowly turning fog.
                if (_WarParams.x > 0.0)
                {
                    float2 exploreUV = (positionWS.xz - _ExploreRect.xy) * _ExploreRect.zw;
                    float inside = step(0.0, exploreUV.x) * step(0.0, exploreUV.y) * step(exploreUV.x, 1.0) * step(exploreUV.y, 1.0);
                    float explored = SAMPLE_TEXTURE2D(_ExploreTex, sampler_LinearClamp, exploreUV).r * inside;
                    float hide = _WarParams.x * (1.0 - smoothstep(0.1, 0.9, explored));
                    if (hide > 0.0)
                    {
                        float swirl = FogNoise(float3(positionWS.x * 0.07, _Time.y * 0.06, positionWS.z * 0.07));
                        float3 dark = _UnexploredColor.rgb * (0.55 + 0.9 * swirl);
                        color = lerp(color, dark, saturate(hide));
                    }
                }

                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
