// Safe Haven: the one cloud pattern, shared by the cloud shadows' cookie (CloudCookie.shader) and the sky (Sky.shader),
// so the clouds overhead and their shadows on the ground have the same shapes, cover and drift (CloudLayer).
// Fractal noise whose every octave repeats across one tile (uv 0-1), so it wraps without a seam.
#ifndef SAFE_HAVEN_CLOUD_NOISE_INCLUDED
#define SAFE_HAVEN_CLOUD_NOISE_INCLUDED

float CloudHash(float2 cell, float period)
{
    cell = fmod(cell + period * 8.0, period);
    float3 p = frac(float3(cell.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float CloudValueNoise(float2 p, float period)
{
    float2 cell = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(CloudHash(cell, period), CloudHash(cell + float2(1, 0), period), f.x),
                lerp(CloudHash(cell + float2(0, 1), period), CloudHash(cell + float2(1, 1), period), f.x), f.y);
}

// 0-1 density; octaves 6 for the cookie, fewer where it is drawn per pixel far away.
float CloudDensity(float2 uv, int octaves)
{
    uv = frac(uv);
    float sum = 0.0, weight = 0.5, total = 0.0;
    float period = 5.0;
    for (int i = 0; i < octaves; i++)
    {
        sum += CloudValueNoise(uv * period, period) * weight;
        total += weight;
        weight *= 0.5;
        period *= 2.0;
    }

    return sum / total;
}

// Cover is the share of the sky under cloud: the density above which a point is cloud.
float CloudMask(float density, float cover, float softness)
{
    float edge = lerp(0.68, 0.38, cover);
    return smoothstep(edge - softness * 0.5, edge + softness, density);
}

#endif
