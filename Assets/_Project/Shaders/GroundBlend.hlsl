#ifndef SAFE_HAVEN_GROUND_BLEND_INCLUDED
#define SAFE_HAVEN_GROUND_BLEND_INCLUDED
// Rocks, logs and props sit in the ground rather than on it (Docs/LookAndFeelResearch_2026-11-18.md 4.3): over the bottom few tens of
// centimetres a surface takes on the colour of the ground under it (GrassTerrainColour's map, which the grass already reads), with a
// darker contact band right at the ground, and a shore's wet band (GroundLight.hlsl). The ground's height is the terrain's own 16-bit
// heightmap (GroundLightMap binds it): the light field's byte heights in coarser cells read a metre off on a bank and blended whole rocks.
// Only in a generated raid; elsewhere nothing changes. Include after GroundLight.hlsl (SkyOcclusion.hlsl brings it).
TEXTURE2D(_GrassTerrainColor); SAMPLER(sampler_GrassTerrainColor);
float4 _GrassTerrainRect;
float _GrassTerrainColorOn;
TEXTURE2D(_GroundBlendHeightmap); SAMPLER(sampler_GroundBlend_linear_clamp);
float4 _GroundBlendPlace; // xy the terrain's corner (x, z), zw uv per metre
float4 _GroundBlendScale; // x the terrain's base height, y metres per heightmap unit, z half a texel, w 1 while bound

// The terrain's height under a point (world metres), between its heightmap's samples as the terrain's own surface lies.
float GroundBlendHeight(float3 positionWS)
{
    float2 uv = (positionWS.xz - _GroundBlendPlace.xy) * _GroundBlendPlace.zw + _GroundBlendScale.z;
    return _GroundBlendScale.x + SAMPLE_TEXTURE2D_LOD(_GroundBlendHeightmap, sampler_GroundBlend_linear_clamp, uv, 0).r * _GroundBlendScale.y;
}

void GroundBlend(inout half3 albedo, inout half smoothness, inout half occlusion, float3 positionWS)
{
    UNITY_BRANCH if (_GroundBlendScale.w < 0.5)
    {
        return;
    }

    float above = positionWS.y - GroundBlendHeight(positionWS);
    // The band grows with how far the thing stands out of the ground (its renderer's bounds), up to 30 cm: a quarter of a stone's
    // height, so a pebble keeps its read (a fixed band hid them) and a boulder sits deep in the soil. Bounds unset: a waist-high rock's.
    float3 middle = (unity_RendererBounds_Min.xyz + unity_RendererBounds_Max.xyz) * 0.5;
    float standing = unity_RendererBounds_Max.y > unity_RendererBounds_Min.y + 0.01 ? unity_RendererBounds_Max.y - GroundBlendHeight(middle) : 1.2;
    float reach = clamp(standing * 0.25, 0.03, 0.3);
    half band = 1.0 - smoothstep(0.0, reach, above);
    half contact = 1.0 - smoothstep(-0.05, reach * 0.35, above);
    UNITY_BRANCH if (_GrassTerrainColorOn > 0.5 && band > 0.001)
    {
        // The ground's colour a little darker: the soil heaped at a stone's foot is damp and in its shadow.
        float2 mapUV = (positionWS.xz - _GrassTerrainRect.xy) * _GrassTerrainRect.zw;
        half3 ground = SAMPLE_TEXTURE2D_LOD(_GrassTerrainColor, sampler_GrassTerrainColor, mapUV, 0).rgb * 0.85;
        albedo = lerp(albedo, ground, band * 0.45);
    }

    albedo *= 1.0 - 0.25 * contact;
    occlusion *= 1.0 - 0.35 * contact;
    half wet = GroundLightShoreWet(positionWS);
    albedo *= 1.0 - 0.3 * wet;
    smoothness = lerp(smoothness, max(smoothness, 0.55), wet);
}
#endif
