#ifndef SAFE_HAVEN_SKY_OCCLUSION
#define SAFE_HAVEN_SKY_OCCLUSION
// How much open sky a point sees (SkyOcclusionMap): a top-down height map of what stands round the player (roofs,
// canopies, bushes; the terrain is left out) tells whether something stands above the point. Under a roof or a tree the
// sky's ambient light and reflections dim, and rain, puddles and snow stay out. Soft: five taps a metre apart, and a
// cover that grows over the first two metres of height above the point. 1 everywhere outside the map, or with it off.
// On a generated raid map the height map covers the whole map, drawn once, and the ground light field (GroundLight.hlsl)
// adds the land's own shade; elsewhere these functions give exactly what they gave before it.
#include "Assets/_Project/Shaders/GroundLight.hlsl"
TEXTURE2D(_SkyOcclusionMap);
SAMPLER(sampler_SkyOcclusionMap);
float4 _SkyOcclusionRect; // x min, z min, 1 / size, how much cover dims indirect light (0 = the map is off)

half SkyVisibility(float3 positionWS)
{
    if (_SkyOcclusionRect.w <= 0.0)
    {
        return 1.0;
    }

    float2 uv = (positionWS.xz - _SkyOcclusionRect.xy) * _SkyOcclusionRect.z;
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return 1.0;
    }

    float tap = 1.0 * _SkyOcclusionRect.z;
    float2 taps[5] = { float2(0, 0), float2(tap, 0), float2(-tap, 0), float2(0, tap), float2(0, -tap) };
    half cover = 0.0;
    [unroll] for (int i = 0; i < 5; i++)
    {
        float above = SAMPLE_TEXTURE2D_LOD(_SkyOcclusionMap, sampler_SkyOcclusionMap, uv + taps[i], 0).r - positionWS.y;
        cover += smoothstep(0.4, 2.0, above);
    }

    // Fade out towards the map's edge, so its border never shows (a raid's whole-map cover reaches past the map, so the fade
    // lies beyond it).
    float edge = saturate(min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * 12.0);
    return 1.0 - cover * 0.2 * edge;
}

// What the cover alone does to indirect light: a little always comes in round the edges.
half SkyCoverAmbient(float3 positionWS)
{
    // At full strength a third gets in under full cover; the raids use less, so the forest floor stays readable at night.
    return lerp(1.0 - 0.65 * saturate(_SkyOcclusionRect.w), 1.0, SkyVisibility(positionWS));
}

// What indirect light (ambient and reflections) is multiplied by: the cover, and on a raid map with a field the land's shade.
half SkyAmbient(float3 positionWS)
{
    return SkyCoverAmbient(positionWS) * GroundLightRelief(positionWS);
}

// For characters: the field only ever lifts them (the night's lift), never darkens them below the cover alone, so the player
// stays as readable at night as before the field. Above 1 on open ground at night: put it on with GroundLightApply.
half SkyAmbientAtLeastCover(float3 positionWS)
{
    return SkyCoverAmbient(positionWS) * max(1.0, GroundLightRelief(positionWS));
}

// Water and mist, which read no sky map elsewhere: on a raid map with a field, the canopy over the point (one tap: the mist
// marches many points a pixel) and the land's shade, which fades out over GROUND_LIGHT_AIR_FADE metres above the ground
// (aboveGround, in metres). The dim hour's lift only gives back what they lose there and never makes them lighter than
// before the field: lit air reads as milky at night. 1 without a field, so the Haven's water and mist are as they were.
half GroundLightAirAbove(float3 positionWS, float aboveGround)
{
    UNITY_BRANCH if (_GroundLightRect.w <= 0.0)
    {
        return 1.0;
    }

    half cover = 1.0;
    if (_SkyOcclusionRect.w > 0.0)
    {
        float2 uv = saturate((positionWS.xz - _SkyOcclusionRect.xy) * _SkyOcclusionRect.z);
        float above = SAMPLE_TEXTURE2D_LOD(_SkyOcclusionMap, sampler_LinearClamp, uv, 0).r - positionWS.y;
        cover = lerp(1.0 - 0.65 * saturate(_SkyOcclusionRect.w), 1.0, 1.0 - smoothstep(0.4, 2.0, above));
    }

    half relief = lerp(GroundLightAt(positionWS, 0).a, 1.0, saturate(aboveGround / GROUND_LIGHT_AIR_FADE));
    return min(cover * relief * _GroundLightRect.w, 1.0);
}

// The same at the ground (a water surface).
half GroundLightAir(float3 positionWS)
{
    return GroundLightAirAbove(positionWS, 0.0);
}
#endif
