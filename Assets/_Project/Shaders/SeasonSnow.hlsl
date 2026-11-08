#ifndef SAFE_HAVEN_SEASON_SNOW
#define SAFE_HAVEN_SEASON_SNOW
// Snow settling on things in winter (WeatherSystem sets _SafeHavenSnow: 0 none, 1 a full layer). Whatever faces up
// whitens, in clumps that follow the surface's own normal map, so it gathers on the tops of rocks, logs, roofs and
// bushes and stays out of their undersides and steep faces. Materials opt in with the "Weather Settles On It" toggle
// (keyword _SEASON_SNOW), so floors and other indoor surfaces stay clear: the same toggle lets the rain soak them when
// the shader includes SurfaceWetness.hlsl first.
float _SafeHavenSnow;

float SeasonSnowHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SeasonSnowNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(SeasonSnowHash(cell), SeasonSnowHash(cell + float2(1, 0)), f.x),
                lerp(SeasonSnowHash(cell + float2(0, 1)), SeasonSnowHash(cell + float2(1, 1)), f.x), f.y);
}

// How much snow covers a point facing normalWS (0-1).
half SeasonSnowCover(half3 normalWS, float3 positionWS)
{
    if (_SafeHavenSnow <= 0.001)
    {
        return 0;
    }

    float clumps = SeasonSnowNoise(positionWS.xz * 2.3 + positionWS.y * 0.7) * 0.6 + SeasonSnowNoise(positionWS.xz * 7.9) * 0.4;
    #if defined(SAFE_HAVEN_SKY_OCCLUSION)
        clumps -= (1.0 - SkyVisibility(positionWS)) * 2.0; // no snow under a roof
    #endif
    half reach = normalWS.y + (clumps - 0.5) * 0.45 - (1.0 - _SafeHavenSnow) * 0.55;
    return smoothstep(0.3, 0.7, reach) * saturate(_SafeHavenSnow * 1.6);
}

void SeasonSnow(inout half3 albedo, inout half smoothness, half3 normalWS, float3 positionWS)
{
    half cover = SeasonSnowCover(normalWS, positionWS);
    if (cover <= 0.001)
    {
        return;
    }

    half sparkle = SeasonSnowNoise(positionWS.xz * 31.0);
    albedo = lerp(albedo, half3(0.82, 0.86, 0.92) * (0.92 + 0.12 * sparkle), cover * 0.92);
    smoothness = lerp(smoothness, 0.3, cover);
}

// For URP's standard lit passes: include after Lighting.hlsl and before the pass, and the pass's call to
// UniversalFragmentPBR(inputData, surfaceData) gets snow first.
#if defined(SEASON_SNOW_WRAP_PBR)
half4 SeasonSnowFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
    #if defined(SAFE_HAVEN_SKY_OCCLUSION)
        surfaceData.occlusion *= SkyAmbient(inputData.positionWS); // under roofs and canopies, less sky light
    #endif
    #if defined(_SEASON_SNOW)
        SeasonSnow(surfaceData.albedo, surfaceData.smoothness, inputData.normalWS, inputData.positionWS);
        #if defined(SAFE_HAVEN_SURFACE_WETNESS)
            SurfaceWet(surfaceData.albedo, surfaceData.smoothness, inputData.normalWS, inputData.positionWS, 1.0 - surfaceData.metallic * 0.8);
        #endif
    #endif
    return UniversalFragmentPBR(inputData, surfaceData);
}
#define UniversalFragmentPBR SeasonSnowFragmentPBR
#endif
#endif
