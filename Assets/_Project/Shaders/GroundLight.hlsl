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
float4 _GroundLightBounce; // rgb: the raid's bounce tint × strength, times the sun's (or moon's) light as it is now

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
