#ifndef SAFE_HAVEN_SPECULAR_AA_INCLUDED
#define SAFE_HAVEN_SPECULAR_AA_INCLUDED
// Geometric specular anti-aliasing (Docs/LookAndFeelResearch_2026-11-18.md 3.4; Kaplanyan 2016, Tokuyoshi & Kaplanyan 2021, as HDRP's
// Lit does it): where the surface's normal turns quickly from pixel to pixel (a normal-mapped, tessellated rock seen from far) its
// highlight would land on a few pixels and glitter as the camera moves; the roughness is widened by how much the normal varies across
// the pixel, so the highlight spreads over it instead. A few ALU. Apply it before wetness raises smoothness, so wet rock keeps its sheen.
// Used by the terrain (GenerateWorldTerrainShader.py) and Mesh Surface (SeasonSnow.hlsl's wrapper).
#define SPECULAR_AA_VARIANCE 0.1
#define SPECULAR_AA_THRESHOLD 0.2
// 1 turns it off (a debugging switch, a global; unset it is 0: on).
float _SafeHavenSpecularAAOff;

half SpecularAntiAlias(half perceptualSmoothness, float3 normalWS)
{
    float3 du = ddx(normalWS);
    float3 dv = ddy(normalWS);
    float variance = SPECULAR_AA_VARIANCE * (dot(du, du) + dot(dv, dv)) * (1.0 - _SafeHavenSpecularAAOff);
    float roughness = (1.0 - perceptualSmoothness) * (1.0 - perceptualSmoothness);
    float squared = saturate(roughness * roughness + min(2.0 * variance, SPECULAR_AA_THRESHOLD * SPECULAR_AA_THRESHOLD));
    return (half)(1.0 - sqrt(sqrt(squared)));
}
#endif
