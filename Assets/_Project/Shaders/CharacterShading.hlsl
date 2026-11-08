// Safe Haven: people and creatures standing out from the ground from a camera 20-30 m up. Characters wear a variant of
// Lit (See-Through) (CharacterShading.cs swaps it in at runtime): _CHARACTER_CLOTH gets a soft rim, lit by the sky and the
// sun from behind the edge, so a figure's outline reads against the litter; _CHARACTER_SKIN also lets light into the
// skin past the terminator, warm, as it does through a cheek or an ear. Strengths are global (RenderingSettings ›
// Characters) since URP's Lit keeps its material buffer fixed. Characters never dissolve in the see-through circle.
#ifndef SAFE_HAVEN_CHARACTER_SHADING_INCLUDED
#define SAFE_HAVEN_CHARACTER_SHADING_INCLUDED

float4 _SafeHavenCharacterRim;    // x strength, y sharpness (power), z extra at night, w sun share
float4 _SafeHavenSkinScatter;     // rgb tint, w strength
float _SafeHavenNight;

#if defined(_CHARACTER_CLOTH) || defined(_CHARACTER_SKIN)
    #define SAFE_HAVEN_CHARACTER
#endif

#if defined(SAFE_HAVEN_CHARACTER)
half3 CharacterLight(InputData inputData, SurfaceData surfaceData)
{
    half3 normal = inputData.normalWS;
    half3 view = inputData.viewDirectionWS;
    Light sun = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    half facing = saturate(dot(normal, view));
    half rim = pow(1.0 - facing, max(_SafeHavenCharacterRim.y, 0.5));
    // The edge catches what lights the scene: the sky all round, and the sun where it comes from behind or the side.
    half3 sky = max(SampleSH(normal), 0.0);
    half3 sunEdge = sun.color * sun.distanceAttenuation * lerp(0.35, 1.0, sun.shadowAttenuation)
                  * saturate(dot(normal, sun.direction) + 0.35);
    half strength = _SafeHavenCharacterRim.x * (1.0 + _SafeHavenCharacterRim.z * _SafeHavenNight);
    half3 light = rim * strength * (sky + sunEdge * _SafeHavenCharacterRim.w) * lerp(half3(1, 1, 1), surfaceData.albedo * 1.6 + 0.2, 0.5);

    #if defined(_CHARACTER_SKIN)
        // Wrap lighting: light reaching a little past where the surface turns away, tinted by what it passed through.
        half nl = dot(normal, sun.direction);
        half wrapped = saturate((nl + 0.5) / 1.5) - saturate(nl);
        light += wrapped * sun.color * sun.distanceAttenuation * lerp(0.4, 1.0, sun.shadowAttenuation)
               * _SafeHavenSkinScatter.rgb * surfaceData.albedo * _SafeHavenSkinScatter.w;
    #endif
    return light;
}
#endif
#endif
