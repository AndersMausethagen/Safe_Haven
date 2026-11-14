#ifndef SAFE_HAVEN_WORLD_TERRAIN_ALL_LAYERS_INCLUDED
#define SAFE_HAVEN_WORLD_TERRAIN_ALL_LAYERS_INCLUDED

// ---- Every layer in one pass ----------------------------------------------------------------------------------------
// Unity draws a terrain four layers to a pass. On this shader each pass is tessellated and displaced again, so ground whose
// paint spans three passes cost three times ground that fits in one: the Haven's Misty Woods drew three, its meadows one,
// and a woods frame took twice as long. With _WORLD_ALL_LAYERS (TerrainSurface sets it, and _WorldAllLayers, for a terrain
// painting more than four layers and at most WORLD_SLOTS) the first pass mixes every painted layer itself, reading each one's
// own textures (bound by TerrainSurface, so nothing is copied or resampled) and its settings from a small table, and the
// add passes are culled before tessellation (WorldPatchCulled). Only painted layers take a slot: a shader may read 64
// textures, and the slots' three each, with the pass's own, must fit.
// The mix is the first pass's own, over all layers at once: weights normalised over every layer rather than per group of
// four, which is what the four-per-pass sum approximated.

#define WORLD_SLOTS 14
// Their own samplers: the pass's _Splat0, _Normal0 and _Mask0 (whose samplers the four-per-pass mix borrows) are not read here.
SAMPLER(sampler_trilinear_repeat_aniso8);
SAMPLER(sampler_linear_clamp);
TEXTURE2D(_WorldControl4);
// Per slot, by row: 0 tiling (xy) and offset (zw); 1 diffuse remap (rgb); 2 mask remap scale; 3 mask remap offset;
// 4 normal scale, smoothness, metallic, smoothness source; 5 has a mask map (x), the layer's index on the terrain (y).
// Each slot's settings, six rows of WORLD_SLOTS (TerrainSurface): numbers, not a texture, as this shader may read only 64.
float4 _WorldLayerData[WORLD_SLOTS * 6];
#define WORLD_LAYER_DATA(slot, row) _WorldLayerData[(row) * WORLD_SLOTS + (slot)]
float _WorldSlotCount;

#define WORLD_LAYER_TEXTURES(L) TEXTURE2D(_WorldAlbedo##L); TEXTURE2D(_WorldNormal##L); TEXTURE2D(_WorldMask##L);
WORLD_LAYER_TEXTURES(0) WORLD_LAYER_TEXTURES(1) WORLD_LAYER_TEXTURES(2) WORLD_LAYER_TEXTURES(3) WORLD_LAYER_TEXTURES(4)
WORLD_LAYER_TEXTURES(5) WORLD_LAYER_TEXTURES(6) WORLD_LAYER_TEXTURES(7) WORLD_LAYER_TEXTURES(8) WORLD_LAYER_TEXTURES(9)
WORLD_LAYER_TEXTURES(10) WORLD_LAYER_TEXTURES(11) WORLD_LAYER_TEXTURES(12) WORLD_LAYER_TEXTURES(13)

struct WorldLayerMix
{
    half3 albedo;
    half3 blurred;
    float3 normal;
    half smoothness;
    half metallic;
    half occlusion;
    half total;
};

// One layer, as the four-per-pass mix samples it: anti-tiled, colour-varied, its mask or its constants. Only called where
// the layer has weight; derivatives come in from outside the branch.
void WorldAccumulateLayer(int layer, half weight, float2 uv, float2 dx, float2 dy,
                          TEXTURE2D_PARAM(albedoMap, albedoSampler), TEXTURE2D_PARAM(normalMap, normalSampler),
                          TEXTURE2D_PARAM(maskMap, maskSampler), inout WorldLayerMix mix)
{
    float4 remap = WORLD_LAYER_DATA(layer, 1);
    float4 maskScale = WORLD_LAYER_DATA(layer, 2);
    float4 maskOffset = WORLD_LAYER_DATA(layer, 3);
    float4 settings = WORLD_LAYER_DATA(layer, 4);
    bool hasMask = WORLD_LAYER_DATA(layer, 5).x > 0.5;

    half blend = WorldAntiTileWeight(uv);
    float2x2 turn = WorldAntiTileTurn * _AntiTilingScale;
    float2 turnedUV = WorldAntiTileUV(uv);
    float2 turnedDx = mul(turn, dx), turnedDy = mul(turn, dy);

    half4 albedo = SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, uv, dx, dy);
    [branch] if (blend > 0.001)
        albedo = lerp(albedo, SAMPLE_TEXTURE2D_GRAD(albedoMap, albedoSampler, turnedUV, turnedDx, turnedDy), blend);
    if (_MacroVariation > 0)
        albedo.rgb = saturate(albedo.rgb * WorldMacroShade(uv));

    half smoothness = settings.w < 0.5 ? albedo.a * settings.y : settings.w < 1.5 ? albedo.a : settings.y;
    half4 mask = 0.5h;
#ifdef _MASKMAP
    [branch] if (hasMask)
        mask = SAMPLE_TEXTURE2D_GRAD(maskMap, maskSampler, uv, dx, dy);
#endif
    mask = mask * maskScale + maskOffset;
    half metallic = hasMask ? mask.r : settings.z;
    half occlusion = hasMask ? mask.g : maskScale.g + maskOffset.g;
    smoothness = hasMask ? mask.a : smoothness;

#if defined(_NORMALMAP)
    half3 normal = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, uv, dx, dy), settings.x);
    [branch] if (blend > 0.001)
    {
        half3 turned = UnpackNormalScale(SAMPLE_TEXTURE2D_GRAD(normalMap, normalSampler, turnedUV, turnedDx, turnedDy), settings.x);
        turned.xy = mul(transpose(WorldAntiTileTurn), turned.xy);
        normal = lerp(normal, turned, blend);
    }
    mix.normal += normal * weight;
#endif

    mix.albedo += albedo.rgb * remap.rgb * weight;
    mix.smoothness += smoothness * weight;
    mix.metallic += metallic * weight;
    mix.occlusion += occlusion * weight;
    mix.total += weight;
    [branch] if (_FootprintSettings.w > 0)
        mix.blurred += SAMPLE_TEXTURE2D_LOD(albedoMap, albedoSampler, uv, 5).rgb * remap.rgb * weight;
}

// A slot's weight: its layer's channel in the control map holding it.
half WorldSlotWeight(int slot, half4 controls[5])
{
    int layer = (int)WORLD_LAYER_DATA(slot, 5).y;
    int channel = layer & 3;
    return dot(controls[min(layer >> 2, 4)], half4(channel == 0, channel == 1, channel == 2, channel == 3));
}

#define WORLD_LAYER(L) \
    { \
        half layerWeight = L < (int)_WorldSlotCount ? WorldSlotWeight(L, controls) : 0; \
        [branch] if (layerWeight > 0) \
        { \
            float4 st = WORLD_LAYER_DATA(L, 0); \
            WorldAccumulateLayer(L, layerWeight, uv * st.xy + st.zw, uvDx * st.xy, uvDy * st.xy, \
                TEXTURE2D_ARGS(_WorldAlbedo##L, sampler_trilinear_repeat_aniso8), TEXTURE2D_ARGS(_WorldNormal##L, sampler_trilinear_repeat_aniso8), \
                TEXTURE2D_ARGS(_WorldMask##L, sampler_trilinear_repeat_aniso8), mix); \
        } \
    }

static half3 worldBlurredAlbedo;

// The surface of every layer at this pixel, as the first pass needs it (alpha is the summed weight, about 1).
void WorldAllLayersSurface(float2 uv, out half3 albedo, out half smoothness, out half metallic, out half occlusion,
                           inout half3 normalTS, out half alpha)
{
    float2 uvDx = ddx(uv), uvDy = ddy(uv);
    float2 controlUV = (uv * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 controls[5];
    controls[0] = SAMPLE_TEXTURE2D(_DisplacementControl0, sampler_linear_clamp, controlUV);
    controls[1] = SAMPLE_TEXTURE2D(_DisplacementControl1, sampler_linear_clamp, controlUV);
    controls[2] = SAMPLE_TEXTURE2D(_DisplacementControl2, sampler_linear_clamp, controlUV);
    controls[3] = SAMPLE_TEXTURE2D(_DisplacementControl3, sampler_linear_clamp, controlUV);
    controls[4] = SAMPLE_TEXTURE2D(_WorldControl4, sampler_linear_clamp, controlUV);

    WorldLayerMix mix = (WorldLayerMix)0;
    WORLD_LAYER(0) WORLD_LAYER(1) WORLD_LAYER(2) WORLD_LAYER(3) WORLD_LAYER(4) WORLD_LAYER(5) WORLD_LAYER(6)
    WORLD_LAYER(7) WORLD_LAYER(8) WORLD_LAYER(9) WORLD_LAYER(10) WORLD_LAYER(11) WORLD_LAYER(12) WORLD_LAYER(13)

    half total = max(mix.total, HALF_MIN);
    albedo = mix.albedo / total;
    worldBlurredAlbedo = mix.blurred / total;
    smoothness = mix.smoothness / total;
    metallic = mix.metallic / total;
    occlusion = mix.occlusion / total;
    alpha = saturate(mix.total);
#if defined(_NORMALMAP)
    float3 nrm = mix.normal / total;
    nrm.z += 1e-5f; // never normalise zero
    nrm.xy *= _NormalMapStrength;
    normalTS = normalize(nrm);
#endif
}

#endif
