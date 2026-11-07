#ifndef SAFE_HAVEN_SURFACE_WETNESS
#define SAFE_HAVEN_SURFACE_WETNESS
// Things getting wet in the rain (WeatherSystem sets _SafeHavenWetness, 0 dry to 1 soaked, rising with the rain and
// drying after it; RainEffect sets _SafeHavenRain while it falls). Wet surfaces darken as their pores fill and turn
// glossy under a film of water, most on whatever faces the sky; while it rains, thin runnels slide down steep faces.
// The terrain has its own wetness and puddles (GenerateWorldTerrainShader.py); this is for everything standing on it.
float _SafeHavenWetness;
float _SafeHavenRain;

float WetnessHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float WetnessNoise(float2 p)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(WetnessHash(cell), WetnessHash(cell + float2(1, 0)), f.x),
                lerp(WetnessHash(cell + float2(0, 1)), WetnessHash(cell + float2(1, 1)), f.x), f.y);
}

// porosity: how much the surface darkens when soaked (stone and wood 1, leaves and metal less).
void SurfaceWet(inout half3 albedo, inout half smoothness, half3 normalWS, float3 positionWS, half porosity)
{
    half wet = (half)_SafeHavenWetness;
    if (wet <= 0.001)
    {
        return;
    }

    half up = saturate(normalWS.y);
    half soak = wet * lerp(0.7, 1.0, up);
    albedo *= lerp(1.0, 0.6, soak * porosity);
    half film = soak * lerp(0.45, 0.8, up);

    // Runnels down the steep faces while it rains: streaks that slide down and break up.
    half steep = saturate(1.0 - abs(normalWS.y) * 1.6);
    if (_SafeHavenRain > 0.01 && steep > 0.01)
    {
        float across = dot(positionWS.xz, float2(0.83, 0.56)) * 9.0;
        float run = WetnessNoise(float2(across, positionWS.y * 1.2 + _Time.y * 2.2));
        film += steep * (half)_SafeHavenRain * smoothstep(0.6, 0.92, run) * 0.35;
    }

    smoothness = lerp(smoothness, max(smoothness, 0.92), saturate(film));
}
#endif
