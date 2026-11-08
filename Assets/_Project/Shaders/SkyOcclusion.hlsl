#ifndef SAFE_HAVEN_SKY_OCCLUSION
#define SAFE_HAVEN_SKY_OCCLUSION
// How much open sky a point sees (SkyOcclusionMap): a top-down height map of what stands round the player (roofs,
// canopies, bushes; the terrain is left out) tells whether something stands above the point. Under a roof or a tree the
// sky's ambient light and reflections dim, and rain, puddles and snow stay out. Soft: five taps a metre apart, and a
// cover that grows over the first two metres of height above the point. 1 everywhere outside the map, or with it off.
TEXTURE2D(_SkyOcclusionMap);
SAMPLER(sampler_SkyOcclusionMap);
float4 _SkyOcclusionRect; // x min, z min, 1 / size, on (1) or off (0)

half SkyVisibility(float3 positionWS)
{
    if (_SkyOcclusionRect.w < 0.5)
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

    // Fade out towards the map's edge, so its border never shows.
    float edge = saturate(min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y)) * 12.0);
    return 1.0 - cover * 0.2 * edge;
}

// What indirect light (ambient and reflections) is multiplied by: a little always comes in round the edges.
half SkyAmbient(float3 positionWS)
{
    return lerp(0.35, 1.0, SkyVisibility(positionWS));
}
#endif
