#ifndef SAFE_HAVEN_HEX_TILING_INCLUDED
#define SAFE_HAVEN_HEX_TILING_INCLUDED
// Hex-tiling (Mikkelsen, "Practical Real-Time Hex-Tiling", JCGT 2022; Docs/LookAndFeelResearch_2026-11-18.md 4.5): a texture tiled
// over a big surface (the moss skin) repeats visibly from above. Here the plane is cut into a triangle grid; each corner gets its
// own random offset and turn, a point samples the three corners of its triangle and blends them, the brighter sample winning a little
// more (contrast-preserving), so the repeat breaks up without the samples washing grey. Gradients are passed per sample, so mips
// stay seamless. Build the frame once (HexFrame) and sample any number of maps with it.

struct HexTiles
{
    float2 uv1, uv2, uv3;
    float2x2 turn1, turn2, turn3;
    float2 dx, dy;
    float3 weights;
};

float2 HexHash(float2 p)
{
    float2 r = mul(float2x2(127.1, 311.7, 269.5, 183.3), p);
    return frac(sin(r) * 43758.5453);
}

float2x2 HexTurn(int2 id, float strength)
{
    float angle = abs(id.x * id.y) + abs(id.x + id.y) + PI;
    angle = fmod(angle, 2.0 * PI);
    angle = angle > PI ? angle - 2.0 * PI : angle;
    angle *= strength;
    float s, c;
    sincos(angle, s, c);
    return float2x2(c, -s, s, c);
}

float2 HexCentre(int2 corner)
{
    return mul(float2x2(1.0, 0.5, 0.0, 1.0 / 1.15470054), (float2)corner) / (2.0 * sqrt(3.0));
}

HexTiles HexFrame(float2 uv, float turnStrength)
{
    HexTiles t;
    t.dx = ddx(uv);
    t.dy = ddy(uv);
    float2 skewed = mul(float2x2(1.0, 0.0, -0.57735027, 1.15470054), uv * 2.0 * sqrt(3.0));
    int2 base = int2(floor(skewed));
    float3 f = float3(frac(skewed), 0.0);
    f.z = 1.0 - f.x - f.y;
    float s = step(0.0, -f.z);
    float s2 = 2.0 * s - 1.0;
    t.weights = float3(-f.z * s2, s - f.y * s2, s - f.x * s2);
    int2 v1 = base + int2(s, s);
    int2 v2 = base + int2(s, 1 - s);
    int2 v3 = base + int2(1 - s, s);
    t.turn1 = HexTurn(v1, turnStrength);
    t.turn2 = HexTurn(v2, turnStrength);
    t.turn3 = HexTurn(v3, turnStrength);
    float2 c1 = HexCentre(v1), c2 = HexCentre(v2), c3 = HexCentre(v3);
    t.uv1 = mul(uv - c1, t.turn1) + c1 + HexHash(v1);
    t.uv2 = mul(uv - c2, t.turn2) + c2 + HexHash(v2);
    t.uv3 = mul(uv - c3, t.turn3) + c3 + HexHash(v3);
    return t;
}

// The blend's weights once the base colour's three samples are known: each corner's share by its triangle weight, tilted toward the
// brighter sample (contrast 0.6) and sharpened (power 7), so the seams between corners are short and nothing turns grey.
float3 HexWeights(HexTiles t, half3 c1, half3 c2, half3 c3)
{
    float3 luma = float3(dot(c1, half3(0.299, 0.587, 0.114)), dot(c2, half3(0.299, 0.587, 0.114)), dot(c3, half3(0.299, 0.587, 0.114)));
    float3 w = t.weights * lerp(1.0, luma, 0.6);
    w = pow(max(w, 1e-5), 7.0);
    return w / dot(w, 1.0);
}

#define HEX_SAMPLE(tex, samp, t, uvN, turnN) SAMPLE_TEXTURE2D_GRAD(tex, samp, t.uvN, mul(t.dx, t.turnN), mul(t.dy, t.turnN))

// A tangent-space normal map hex-tiled: each sample's xy turned back by its corner's turn before the blend.
half3 HexNormal(TEXTURE2D_PARAM(tex, samp), HexTiles t, float3 w, half strength)
{
    half3 n1 = UnpackNormalScale(HEX_SAMPLE(tex, samp, t, uv1, turn1), strength);
    half3 n2 = UnpackNormalScale(HEX_SAMPLE(tex, samp, t, uv2, turn2), strength);
    half3 n3 = UnpackNormalScale(HEX_SAMPLE(tex, samp, t, uv3, turn3), strength);
    n1.xy = mul(t.turn1, n1.xy);
    n2.xy = mul(t.turn2, n2.xy);
    n3.xy = mul(t.turn3, n3.xy);
    return normalize(n1 * w.x + n2 * w.y + n3 * w.z);
}
#endif
