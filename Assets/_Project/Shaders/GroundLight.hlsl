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
float4 _GroundLightRect;   // x min, z min, 1 / size, the night's lift (above 0 while a field is bound)
float4 _GroundLightBounce; // rgb: the raid's bounce tint × strength, times the sun's (or moon's) light as it is now

half4 GroundLightAt(float3 positionWS, uint slice)
{
    float2 uv = saturate((positionWS.xz - _GroundLightRect.xy) * _GroundLightRect.z);
    return SAMPLE_TEXTURE2D_ARRAY_LOD(_GroundLightField, sampler_LinearClamp, uv, slice, 0);
}

// What the land's shade multiplies indirect light by (the night's lift included): 1 without a field.
half GroundLightRelief(float3 positionWS)
{
    UNITY_BRANCH if (_GroundLightRect.w <= 0.0)
    {
        return 1.0;
    }

    return GroundLightAt(positionWS, 0).a * _GroundLightRect.w;
}

// The ground itself (the terrain): the canopy's shade and the land's together, as SkyAmbient gives what stands on it. 1 without.
half GroundLightOnGround(float3 positionWS)
{
    UNITY_BRANCH if (_GroundLightRect.w <= 0.0)
    {
        return 1.0;
    }

    return GroundLightAt(positionWS, 1).r * GroundLightAt(positionWS, 0).a * _GroundLightRect.w;
}

// Light thrown back from sunlit ground near by, to add to a surface's indirect diffuse light (bakedGI): most on what faces
// sideways or down, half on what faces the sky. It follows the sun or moon as it is now, so clouds and night dim it. 0 without.
half3 GroundLightBounce(float3 positionWS, half3 normalWS)
{
    UNITY_BRANCH if (_GroundLightRect.w <= 0.0)
    {
        return 0.0;
    }

    half3 thrown = GroundLightAt(positionWS, 0).rgb;
    return thrown * thrown * _GroundLightBounce.rgb * _MainLightColor.rgb * lerp(1.0, 0.5, saturate(normalWS.y));
}
#endif
