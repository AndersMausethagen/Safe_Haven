#ifndef SAFE_HAVEN_GROUND_LIGHT
#define SAFE_HAVEN_GROUND_LIGHT
// A generated raid's ground light field (GroundLightMap; Docs/LightingBakeResearch_2026-11-17.md §2.1), worked out once while
// the map loads, since a run's hour never changes: how much the land round a spot hides the sky (hollows, ravines, cliff feet,
// valley walls), how much the canopy shades the ground itself, and light thrown back from sunlit ground near by. Nowhere else
// (_GroundLightRect.w is 0: the Haven, the prologue, painted maps) does any function here change what it is given, so those
// look as they did. One texture array read with URP's shared linear-clamp sampler: the terrain is near its limits of 64
// textures and 16 samplers, and Forward+ already uses that sampler for the reflection probe atlas.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/GlobalSamplers.hlsl"

// Slice 0: rgb the light thrown back (square root), a how much the land's shade leaves (1 open). Slice 1: r how much the
// canopy's shade leaves the ground itself (for the terrain, which can't afford to read the sky map).
TEXTURE2D_ARRAY(_GroundLightField);
float4 _GroundLightRect;   // x min, z min, 1 / size, the dim hour's lift (above 0 while a field is bound; 1 by day)
float4 _GroundLightBounce; // rgb: the raid's bounce tint × strength, times the sun's (or moon's) light as it is now; w its sunflecks
float4 _GroundLightHeights; // slice 1's g and b: x the lowest height, y the range (metres), z 1 while a field with heights is bound, w the water's reach

// Metres above the ground over which the land's shade fades out of the air: mist high over a ravine sees the sky its floor doesn't.
#define GROUND_LIGHT_AIR_FADE 20.0

half4 GroundLightAt(float3 positionWS, uint slice)
{
    float2 uv = saturate((positionWS.xz - _GroundLightRect.xy) * _GroundLightRect.z);
    return SAMPLE_TEXTURE2D_ARRAY_LOD(_GroundLightField, sampler_LinearClamp, uv, slice, 0);
}

// What the land's shade multiplies indirect light by (the dim hour's lift included, so above 1 on open ground at night): 1
// without a field.
half GroundLightRelief(float3 positionWS)
{
    // One result and one return: a value returned from inside a [branch] is "potentially uninitialized" to FXC.
    half relief = 1.0;
    UNITY_BRANCH if (_GroundLightRect.w > 0.0)
    {
        relief = GroundLightAt(positionWS, 0).a * _GroundLightRect.w;
    }

    return relief;
}

// The ground itself (the terrain, the moss on it): the canopy's shade and the land's together, as SkyAmbient gives what stands on
// it, the lift included. 1 without.
half GroundLightOnGround(float3 positionWS)
{
    half light = 1.0;
    UNITY_BRANCH if (_GroundLightRect.w > 0.0)
    {
        light = GroundLightAt(positionWS, 1).r * GroundLightAt(positionWS, 0).a * _GroundLightRect.w;
    }

    return light;
}

// Puts a factor on a surface's indirect light. URP's lit shaders take the lower of the surface's occlusion and SSAO
// (AmbientOcclusion.hlsl, CreateAmbientOcclusionFactor), so a factor above 1 (the night's lift on open ground) would be thrown
// away there and the night would come out darker: the darkening part goes into occlusion, what lies above 1 multiplies the
// indirect diffuse light itself. Call it once bakedGI is sampled, before the bounce is added. A factor of 1 or less (always, without
// a field) is exactly `occlusion *= factor`.
void GroundLightApply(half factor, inout half occlusion, inout half3 bakedGI)
{
    occlusion *= min(factor, 1.0);
    bakedGI *= max(factor, 1.0);
}

// The surface of the water at or near a point (world metres), where a raid's field holds one (slice 1's blue, within Shore Reach of
// an edge and everywhere inside the water): what a warning over a pool lies on (GroundTelegraph.shader). False elsewhere.
bool GroundLightWaterLevel(float3 positionWS, out float level)
{
    level = 0.0;
    bool found = false;
    UNITY_BRANCH if (_GroundLightHeights.z > 0.5)
    {
        half4 ground = GroundLightAt(positionWS, 1);
        if (ground.b > 0.003)
        {
            level = _GroundLightHeights.x + ground.g * _GroundLightHeights.y
                + (ground.b * 255.0 - 1.0) / 254.0 * 2.0 * _GroundLightHeights.w - _GroundLightHeights.w;
            found = true;
        }
    }

    return found;
}

// How wet a shore is at a point (0-1; Docs/LookAndFeelResearch_2026-11-18.md 4.6): a band darker and glossier for a hand's breadth
// above the nearest water, as real banks are. Slice 1's blue holds the water's surface as metres from the cell's ground (its green), ± w
// (0 where none is near); both filter linearly, so their sum is the flat surface between cells too. 0 without a field.
half GroundLightShoreWet(float3 positionWS)
{
    half wet = 0.0;
    UNITY_BRANCH if (_GroundLightHeights.z > 0.5)
    {
        half4 ground = GroundLightAt(positionWS, 1);
        UNITY_BRANCH if (ground.b > 0.003)
        {
            float level = _GroundLightHeights.x + ground.g * _GroundLightHeights.y
                + (ground.b * 255.0 - 1.0) / 254.0 * 2.0 * _GroundLightHeights.w - _GroundLightHeights.w;
            float above = positionWS.y - level;
            // Fading out with distance from the water's edge (slice 1's alpha), so a gentle shore's band ends softly, not in a line.
            wet = (1.0 - smoothstep(0.2, 0.75, above)) * step(-0.6, above) * smoothstep(0.15, 0.65, ground.a);
        }
    }

    return wet;
}

float GroundLightHash(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float GroundLightNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(GroundLightHash(i), GroundLightHash(i + float2(1, 0)), f.x),
                lerp(GroundLightHash(i + float2(0, 1)), GroundLightHash(i + float2(1, 1)), f.x), f.y);
}

#if defined(UNIVERSAL_SHADOWS_INCLUDED)
// Sunflecks (Docs/LookAndFeelResearch_2026-11-18.md L3): spots of sun through gaps in a crown on the ground its shadow falls on,
// drifting a little as the leaves move. A dense crown's shadow is one even shade (tree shadows come from a lighter level of the
// tree), so inside the sun's shadow (one hard tap of its shadow map: the shadow map is bound wherever this is called) the spots
// let some sun through, where the field says a canopy stands toward the sun (slice 1's red, up-sun of the point: a crown's shadow
// falls away from it), not in a cliff's or a building's shadow. The main light's colour (sun or moon, clouds and all) × the raid's
// Sunflecks (_GroundLightBounce.w), to add to a surface's indirect light. Spots are longer along the sun's way across the ground.
// 0 without a field, sunflecks or a sun.
half3 GroundLightSunflecks(float3 positionWS)
{
    half3 light = 0.0;
    UNITY_BRANCH if (_GroundLightRect.w > 0.0 && _GroundLightBounce.w > 0.0 && _MainLightPosition.y > 0.05)
    {
        float2 toSun = _MainLightPosition.xz;
        float across = length(toSun);
        toSun = across > 1e-3 ? toSun / across : float2(1, 0);
        // The crown that shades a point stands up-sun of it: the canopy here or 4 or 9 metres toward the sun.
        half canopy = 1.0 - GroundLightAt(positionWS, 1).r;
        canopy = max(canopy, 1.0 - GroundLightAt(positionWS + float3(toSun.x, 0, toSun.y) * 4.0, 1).r);
        canopy = max(canopy, 1.0 - GroundLightAt(positionWS + float3(toSun.x, 0, toSun.y) * 9.0, 1).r);
        UNITY_BRANCH if (canopy > 0.06)
        {
            float4 coord = TransformWorldToShadowCoord(positionWS);
            half shade = 1.0 - SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_LinearClampCompare, coord.xyz);
            UNITY_BRANCH if (shade > 0.01)
            {
                // Stretched along the sun's way, more the lower it stands.
                float stretch = lerp(1.0, 2.2, saturate(across));
                float2 q = float2(dot(positionWS.xz, toSun) / stretch, dot(positionWS.xz, float2(-toSun.y, toSun.x)));
                float t = _Time.y;
                float2 sway = float2(sin(t * 0.37), cos(t * 0.29)) * 0.22;
                float n = GroundLightNoise(q * 1.25 + sway) * 0.62 + GroundLightNoise(q * 2.9 - sway * 1.6 + 17.0) * 0.38;
                half fleck = smoothstep(0.6, 0.7, n);
                light = _MainLightColor.rgb * (_GroundLightBounce.w * fleck * shade * smoothstep(0.06, 0.3, canopy));
            }
        }
    }

    return light;
}
#endif

// Light thrown back from sunlit ground near by, to add to a surface's indirect diffuse light (bakedGI): most on what faces
// sideways or down, little on what faces the sky (level ground would otherwise be tinted by its own light). It follows the sun or
// moon as it is now, so clouds and night dim it. 0 without.
half3 GroundLightBounce(float3 positionWS, half3 normalWS)
{
    half3 bounce = 0.0;
    UNITY_BRANCH if (_GroundLightRect.w > 0.0)
    {
        half3 thrown = GroundLightAt(positionWS, 0).rgb;
        bounce = thrown * thrown * _GroundLightBounce.rgb * _MainLightColor.rgb * lerp(1.0, 0.1, saturate(normalWS.y));
    }

    return bounce;
}
#endif
