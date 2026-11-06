#ifndef SAFE_HAVEN_TERRAIN_DISPLACEMENT_INCLUDED
#define SAFE_HAVEN_TERRAIN_DISPLACEMENT_INCLUDED

TEXTURE2D_ARRAY(_DisplacementMasks); SAMPLER(sampler_DisplacementMasks);
TEXTURE2D(_DisplacementLayers);
TEXTURE2D(_FootprintField); SAMPLER(sampler_FootprintField);
float4 _FootprintArea, _FootprintSettings;
float _FootprintDarkening;
TEXTURE2D(_DisplacementControl0); SAMPLER(sampler_DisplacementControl0);
TEXTURE2D(_DisplacementControl1);
TEXTURE2D(_DisplacementControl2);
TEXTURE2D(_DisplacementControl3);
float4 _DisplacementControl0_TexelSize;
float _DisplacementLayerCount, _DisplacementStrength, _DisplacementNear, _DisplacementFar;
float _TessellationFactor, _TessellationEdgeLength;
float4 _TerrainDetailQuality, _DisplacementTerrainSize;
float _DisplacementDataVersion, _DisplacementMip, _HeightBlendSharpness;
float _DisplacementNormalStrength, _NormalMapStrength, _SurfaceOcclusion, _SurfaceSmoothness;
float _TessellationPixels, _SurfaceDebug;
// Set by TerrainSurface when the first pass draws every layer (WorldTerrainAllLayers.hlsl): the add passes then draw nothing.
float _WorldAllLayers;

// Where the player is (xyz) while a game camera follows one (w = 1; TerrainDetailFocus). The camera hangs 20-40 m above a
// top-down view, so a fade by distance from it tessellated nearly the whole screen; round the player is where relief is seen.
float4 _TerrainDetailFocus;
float _DetailFocusNear, _DetailFocusFar;

float WorldDisplacementFade(float3 positionOS)
{
    float3 world = TransformObjectToWorld(positionOS);
    float distanceToCamera = distance(world, _WorldSpaceCameraPos);
    float range = max(_TerrainDetailQuality.z, 0.001);
    float fade = 1.0 - smoothstep(_DisplacementNear * range, max(_DisplacementFar * range, _DisplacementNear * range + 0.01), distanceToCamera);
    if (_TerrainDetailFocus.w > 0.5 && _DetailFocusFar > 0)
    {
        float toFocus = distance(world.xz, _TerrainDetailFocus.xz);
        fade = min(fade, 1.0 - smoothstep(_DetailFocusNear * range, max(_DetailFocusFar * range, _DetailFocusNear * range + 0.01), toFocus));
    }

    return fade * _TerrainDetailQuality.x;
}

float WorldDisplacementHeightLOD(float2 uv, float mip, out float footprintResponse, out float minimumHeight)
{
    float2 controlUV = (uv * (_DisplacementControl0_TexelSize.zw - 1.0) + 0.5) * _DisplacementControl0_TexelSize.xy;
    float4 controls[4];
    controls[0] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl0, sampler_DisplacementControl0, controlUV, 0);
    controls[1] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl1, sampler_DisplacementControl0, controlUV, 0);
    controls[2] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl2, sampler_DisplacementControl0, controlUV, 0);
    controls[3] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl3, sampler_DisplacementControl0, controlUV, 0);
    float height = 0;
    float weightSum = 0;
    footprintResponse = 0;
    minimumHeight = 0;
    [loop]
    for (uint layer = 0; layer < (uint)_DisplacementLayerCount; layer++)
    {
        float weight = controls[layer >> 2][layer & 3];
        if (weight <= 0.0001) continue;
        float4 tiling = LOAD_TEXTURE2D(_DisplacementLayers, int2(layer, 0));
        float4 remap = LOAD_TEXTURE2D(_DisplacementLayers, int2(layer, 1));
        float midpoint = _DisplacementDataVersion > 0 ? remap.w : 0.5;
        float amplitude = _DisplacementDataVersion > 0 ? remap.z : 1;
        float sampleHeight = SAMPLE_TEXTURE2D_ARRAY_LOD(_DisplacementMasks, sampler_DisplacementMasks, uv * tiling.xy + tiling.zw, layer, mip).r;
        float h = sampleHeight * remap.x + remap.y;
        // Height-aware weighting is shared by ALL geometry passes, never normalized separately per add pass.
        weight *= exp2(clamp((h - 0.5) * _HeightBlendSharpness, -8, 8));
        height += (h - midpoint) * amplitude * weight;
        minimumHeight += min((remap.y - midpoint) * amplitude, (remap.x + remap.y - midpoint) * amplitude) * weight;
        if (_DisplacementDataVersion >= 2)
            footprintResponse += LOAD_TEXTURE2D(_DisplacementLayers, int2(layer, 2)).r * weight;
        weightSum += weight;
    }
    footprintResponse /= max(weightSum, 0.0001);
    minimumHeight /= max(weightSum, 0.0001);
    return height / max(weightSum, 0.0001);
}

float WorldDisplacementHeightLOD(float2 uv, float mip, out float footprintResponse)
{
    float minimumHeight;
    return WorldDisplacementHeightLOD(uv, mip, footprintResponse, minimumHeight);
}

float WorldDisplacementHeightLOD(float2 uv, float mip)
{
    float response;
    return WorldDisplacementHeightLOD(uv, mip, response);
}

float WorldFootprintMask(float2 worldXZ)
{
    if (_FootprintSettings.w <= 0) return 0;
    float2 fieldUV = (worldXZ - _FootprintArea.xy) * _FootprintArea.z;
    if (any(fieldUV < 0) || any(fieldUV > 1)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_FootprintField, sampler_FootprintField, fieldUV, 0).r * _TerrainDetailQuality.x;
}

float WorldSurfaceOffsetLOD(float2 uv, float mip, float2 worldXZ)
{
    float response;
    float minimumHeight;
    float height = WorldDisplacementHeightLOD(uv, mip, response, minimumHeight);
    float mask = WorldFootprintMask(worldXZ);
    float footprint = mask * response;
    float compression = saturate(mask * 4) * response;
    float originalHeight = height * _DisplacementStrength;
    float floorHeight = min(originalHeight, minimumHeight * _DisplacementStrength);
    float pressedHeight = lerp(originalHeight, floorHeight, compression * _FootprintSettings.y) - footprint * _FootprintSettings.x;
    return min(originalHeight, pressedHeight);
}

float WorldDisplacementHeight(float2 uv) { return WorldDisplacementHeightLOD(uv, _DisplacementMip); }

float WorldSlopeFade(float3 normalOS) { return smoothstep(0.35, 0.75, normalize(normalOS).y); }

// ---- Rock on steep ground (shared with the fragment's triplanar rock in WorldTerrainLitPasses) -----------------------
TEXTURE2D(_CliffAlbedo); SAMPLER(sampler_CliffAlbedo);
TEXTURE2D(_CliffNormal);
TEXTURE2D(_CliffMask); SAMPLER(sampler_CliffMask); // its own sampler: the domain stage reads the mask without the colour
float _CliffStrength, _CliffSlopeStart, _CliffSlopeEnd, _CliffBreakup, _CliffTiling, _CliffNormalStrength;
float _CliffDisplacement, _CliffHeightMidpoint;
half4 _CliffTint;
// The steepest ground's rock (Cliff Rock), over the rocky ground above.
TEXTURE2D(_SteepAlbedo);
TEXTURE2D(_SteepNormal);
TEXTURE2D(_SteepMask); SAMPLER(sampler_SteepMask);
float _SteepSlopeStart, _SteepSlopeEnd, _SteepTiling, _SteepNormalStrength, _SteepHeightMidpoint, _RockFromLayers, _RockAlwaysFrom;
half4 _SteepTint;

// How much rock lies here (x) and how much of it is cliff rock rather than rocky ground (y, 0-1). With Rock From Painted
// Layers it is painted: layers marked Rocky or Cliff in the terrain's surface profile (row 2, green, of the layer data)
// are drawn as these rocks, so painting any other ground over them replaces them. Otherwise it follows the slope.
float2 WorldRock(float2 uv, float slope)
{
    if (_CliffStrength <= 0) return 0;
    if (_RockFromLayers > 0.5)
    {
        float2 controlUV = (uv * (_DisplacementControl0_TexelSize.zw - 1.0) + 0.5) * _DisplacementControl0_TexelSize.xy;
        float4 controls[4];
        controls[0] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl0, sampler_DisplacementControl0, controlUV, 0);
        controls[1] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl1, sampler_DisplacementControl0, controlUV, 0);
        controls[2] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl2, sampler_DisplacementControl0, controlUV, 0);
        controls[3] = SAMPLE_TEXTURE2D_LOD(_DisplacementControl3, sampler_DisplacementControl0, controlUV, 0);
        float rocky = 0, cliff = 0;
        [loop]
        for (uint layer = 0; layer < (uint)_DisplacementLayerCount; layer++)
        {
            float kind = LOAD_TEXTURE2D(_DisplacementLayers, int2(layer, 2)).g;
            float weight = controls[layer >> 2][layer & 3];
            rocky += kind > 0.5 && kind < 1.5 ? weight : 0;
            cliff += kind > 1.5 ? weight : 0;
        }
        float amount = saturate(rocky + cliff);
        // Walls are rock whatever is painted: paint is laid out from above, so a near-vertical face has almost no
        // cells of its own and any ground would show stretched down it. What is painted near it still picks which rock
        // (rocky ground where Rocky is painted, cliff rock otherwise).
        float wall = smoothstep(_RockAlwaysFrom, _RockAlwaysFrom + 8.0, slope) * (1.0 - amount);
        return float2(saturate(amount + wall) * _CliffStrength, (cliff + wall) / max(rocky + cliff + wall, 1e-4));
    }

    float rock = smoothstep(_CliffSlopeStart, max(_CliffSlopeEnd, _CliffSlopeStart + 0.1), slope);
    float steep = smoothstep(_SteepSlopeStart, max(_SteepSlopeEnd, _SteepSlopeStart + 0.1), slope);
    return float2(max(rock, steep) * _CliffStrength, steep);
}

// ---- Safe Haven: hiding the rock's repetition -----------------------------------------------------------------------
// The rock is projected from three sides at one size, so the same crack came back every few metres along a cliff. As
// the ground layers do, each projection is sampled twice, as it is and turned 63 degrees and resized, and slow noise in
// the rock's own space picks between them. The colour, normals and the tessellated relief all use the same choice.
float _RockAntiTiling;
static const float2x2 WorldRockTurn = float2x2(0.4536, -0.8912, 0.8912, 0.4536);

float WorldRockHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

float WorldRockNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(lerp(WorldRockHash(i), WorldRockHash(i + float3(1, 0, 0)), f.x),
                     lerp(WorldRockHash(i + float3(0, 1, 0)), WorldRockHash(i + float3(1, 1, 0)), f.x), f.y),
                lerp(lerp(WorldRockHash(i + float3(0, 0, 1)), WorldRockHash(i + float3(1, 0, 1)), f.x),
                     lerp(WorldRockHash(i + float3(0, 1, 1)), WorldRockHash(i + float3(1, 1, 1)), f.x), f.y), f.z);
}

// The second sample's coordinates: turned and a little over half the size, offset so the two never line up.
float2 WorldRockTurnedUV(float2 uv) { return mul(WorldRockTurn, uv) * 0.61 + float2(0.37, 0.71); }

// How much of the turned sample to use at a point of the rock's tiled space (0: none, 1: only it).
float WorldRockBlend(float3 q)
{
    if (_RockAntiTiling <= 0) return 0;
    float n = WorldRockNoise(q * 0.31) * 0.7 + WorldRockNoise(q * 0.83 + 5.1) * 0.3;
    return smoothstep(0.35, 0.65, n) * _RockAntiTiling;
}

float WorldRockHeightAt(TEXTURE2D_PARAM(mask, smp), float2 uv, float b)
{
    float a = SAMPLE_TEXTURE2D_LOD(mask, smp, uv, _DisplacementMip).b;
    [branch] if (b <= 0.001) return a;
    return lerp(a, SAMPLE_TEXTURE2D_LOD(mask, smp, WorldRockTurnedUV(uv), _DisplacementMip).b, b);
}

float WorldTriplanarHeight(TEXTURE2D_PARAM(mask, smp), float3 q, float3 blend)
{
    float b = WorldRockBlend(q);
    return WorldRockHeightAt(TEXTURE2D_ARGS(mask, smp), q.zy, b) * blend.x
         + WorldRockHeightAt(TEXTURE2D_ARGS(mask, smp), q.xz, b) * blend.y
         + WorldRockHeightAt(TEXTURE2D_ARGS(mask, smp), q.xy, b) * blend.z;
}

// The ground's own displacement fades out on steep ground (it only lifts straight up), so the rock is pushed out along
// the slope instead, by its mask's height (blue, as terrain layer masks keep it), projected from three sides like its
// colour so the relief matches what is drawn. Painted rock on level ground keeps the layer displacement instead.
float WorldCliffOffset(float3 positionOS, float3 normalOS, float2 uv)
{
    if (_CliffDisplacement <= 0 || _CliffStrength <= 0) return 0;
    float3 n = normalize(TransformObjectToWorldNormal(normalOS));
    float slope = degrees(acos(saturate(n.y)));
    float2 rock = WorldRock(uv, slope);
    float w = rock.x * (_RockFromLayers > 0.5 ? 1.0 - WorldSlopeFade(normalOS) * 0.75 : 1.0);
    if (w <= 0.001) return 0;
    float3 world = TransformObjectToWorld(positionOS);
    float3 blend = pow(abs(n), 4.0);
    blend /= max(dot(blend, 1.0), 1e-4);
    float h = rock.y < 0.999 ? WorldTriplanarHeight(TEXTURE2D_ARGS(_CliffMask, sampler_CliffMask), world / max(_CliffTiling, 0.01), blend) - _CliffHeightMidpoint : 0;
    if (rock.y > 0.001)
        h = lerp(h, WorldTriplanarHeight(TEXTURE2D_ARGS(_SteepMask, sampler_SteepMask), world / max(_SteepTiling, 0.01), blend) - _SteepHeightMidpoint, rock.y);
    return h * _CliffDisplacement * w;
}

void WorldDisplace(inout float4 positionOS, float3 normalOS, float2 uv)
{
    float fade = WorldDisplacementFade(positionOS.xyz);
    if (fade <= 0) return;
    float3 original = positionOS.xyz;
    if ((_DisplacementStrength > 0 || _FootprintSettings.w > 0) && _DisplacementLayerCount > 0)
    {
        // Vertical detail tapers off on cliffs, where the rock's own relief takes over.
        float slopeFade = WorldSlopeFade(normalOS);
        positionOS.y += WorldSurfaceOffsetLOD(uv, _DisplacementMip, TransformObjectToWorld(original).xz) * fade * slopeFade;
    }

    positionOS.xyz += normalize(normalOS) * WorldCliffOffset(original, normalOS, uv) * fade;
}

// Fragment-only helpers stay separate from the domain displacement so texture derivatives are valid.
float WorldSurfaceMip(float2 uv)
{
    float largest = 0;
    float2 uvDx = ddx(uv);
    float2 uvDy = ddy(uv);
    [loop]
    for (uint i = 0; i < (uint)_DisplacementLayerCount; i++)
    {
        float2 tiling = LOAD_TEXTURE2D(_DisplacementLayers, int2(i, 0)).xy;
        float2 dx = uvDx * tiling;
        float2 dy = uvDy * tiling;
        largest = max(largest, max(dot(dx, dx), dot(dy, dy)));
    }
    uint width, height, slices, levels;
    _DisplacementMasks.GetDimensions(0, width, height, slices, levels);
    return max(_DisplacementMip, 0.5 * log2(max(largest * width * width, 1.0)));
}

float3 WorldSurfaceNormal(float2 uv, float3 positionWS, float3 baseNormalWS, float3 mappedNormalWS)
{
    float footprint = WorldFootprintMask(positionWS.xz);
    if (footprint > 0)
    {
        float response;
        WorldDisplacementHeightLOD(uv, _DisplacementMip, response);
        mappedNormalWS = normalize(lerp(mappedNormalWS, baseNormalWS,
            saturate(saturate(footprint * 4) * response * _FootprintSettings.z)));
    }
    if (_DisplacementNormalStrength <= 0 || _DisplacementLayerCount <= 0 || _TerrainDetailQuality.w <= 0)
        return mappedNormalWS;
    if (_DisplacementStrength <= 0 && _FootprintSettings.w <= 0) return mappedNormalWS;
    float3 positionOS = TransformWorldToObject(positionWS);
    float fade = WorldDisplacementFade(positionOS);
    float3 baseOS = TransformWorldToObjectNormal(baseNormalWS);
    float amount = fade * WorldSlopeFade(baseOS) * _DisplacementNormalStrength;
    if (amount <= 0) return mappedNormalWS;
    float mip = WorldSurfaceMip(uv);
    float stepMetres = max(_DisplacementTerrainSize.z, 0.01) * exp2(mip);
    float2 delta = stepMetres / max(_DisplacementTerrainSize.xy, 0.01);
    float left = WorldSurfaceOffsetLOD(uv - float2(delta.x, 0), mip, positionWS.xz - float2(stepMetres, 0));
    float right = WorldSurfaceOffsetLOD(uv + float2(delta.x, 0), mip, positionWS.xz + float2(stepMetres, 0));
    float back = WorldSurfaceOffsetLOD(uv - float2(0, delta.y), mip, positionWS.xz - float2(0, stepMetres));
    float front = WorldSurfaceOffsetLOD(uv + float2(0, delta.y), mip, positionWS.xz + float2(0, stepMetres));
    float2 gradient = float2(right - left, front - back) * amount / (2 * stepMetres);
    float3 displacedOS = normalize(baseOS - float3(gradient.x, 0, gradient.y) * max(baseOS.y, 0));
    float3 displacedWS = TransformObjectToWorldNormal(displacedOS);
    float3 axis = cross(baseNormalWS, displacedWS);
    float cosine = clamp(dot(baseNormalWS, displacedWS), -0.999, 1);
    return normalize(mappedNormalWS + cross(axis, mappedNormalWS)
        + cross(axis, cross(axis, mappedNormalWS)) / (1 + cosine));
}

struct WorldControlPoint
{
    float4 positionOS : INTERNALTESSPOS;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

WorldControlPoint WorldTessVertex(Attributes v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    TerrainInstancing(v.positionOS, v.normalOS, v.texcoord);
    WorldControlPoint o;
    o.positionOS = v.positionOS;
    o.normalOS = v.normalOS;
    o.texcoord = v.texcoord;
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    return o;
}

struct WorldTessFactors
{
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

float WorldEdgeFactor(float3 a, float3 b)
{
    float3 middle = (a + b) * 0.5;
    float fade = WorldDisplacementFade(middle);
    float edgeMetres = distance(TransformObjectToWorld(a), TransformObjectToWorld(b));
    float distanceToCamera = max(distance(TransformObjectToWorld(middle), _WorldSpaceCameraPos), 0.01);
    float projectedPixels = edgeMetres * abs(UNITY_MATRIX_P._m11) * _ScreenParams.y * 0.5;
    projectedPixels /= unity_OrthoParams.w > 0.5 ? 1 : distanceToCamera;
    float desired = edgeMetres / max(_TessellationEdgeLength, 0.005);
    if (_TessellationPixels > 0) desired = min(desired, projectedPixels / _TessellationPixels);
    float factor = clamp(desired, 1.0, max(_TessellationFactor * _TerrainDetailQuality.y, 1.0));
    float displaces = max(max(step(0.0001, _DisplacementStrength), _FootprintSettings.w) * step(0.5, _DisplacementLayerCount),
                          step(0.0001, _CliffDisplacement * _CliffStrength));
    return lerp(1.0, factor, fade * displaces);
}

// Work nobody sees is not done. A triangle wholly outside the view (the camera's, or a shadow cascade's in the shadow pass)
// is dropped before it is tessellated: the terrain's patches are culled whole on the CPU, so their off-screen triangles still
// reached the hull stage, each split up to 64 times. And an add pass draws only its own four layers, added on top of the
// others: where none of them is painted it would add nothing, so that triangle is dropped too. The 18-layer Haven draws five
// passes; in the woods, culling neither left the GPU tessellating five whole terrains a frame.
float2 WorldSplatUV(float2 uv)
{
    return (uv * (_Control_TexelSize.zw - 1.0) + 0.5) * _Control_TexelSize.xy;
}

bool WorldPatchCulled(InputPatch<WorldControlPoint, 3> patch)
{
    float3 a = TransformObjectToWorld(patch[0].positionOS.xyz);
    float3 b = TransformObjectToWorld(patch[1].positionOS.xyz);
    float3 c = TransformObjectToWorld(patch[2].positionOS.xyz);
    // Displacement and rock relief can lift a point off the flat triangle; keep anything that close to the edge.
    float margin = 2.0 + max(_CliffDisplacement, 0.0);
    float4x4 vp = UNITY_MATRIX_VP;
    float4 planes[4] = { vp[3] + vp[0], vp[3] - vp[0], vp[3] + vp[1], vp[3] - vp[1] };
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float scale = rsqrt(max(dot(planes[i].xyz, planes[i].xyz), 1e-12));
        float da = (dot(planes[i].xyz, a) + planes[i].w) * scale;
        float db = (dot(planes[i].xyz, b) + planes[i].w) * scale;
        float dc = (dot(planes[i].xyz, c) + planes[i].w) * scale;
        if (max(da, max(db, dc)) < -margin) return true;
    }

#if defined(TERRAIN_SPLAT_ADDPASS)
    if (_WorldAllLayers > 0.5) return true;
    float2 ua = patch[0].texcoord, ub = patch[1].texcoord, uc = patch[2].texcoord;
    float2 low = min(ua, min(ub, uc)), high = max(ua, max(ub, uc));
    // A mip whose texels span the whole triangle (averaged, so any paint inside it shows), plus the corners themselves.
    float texels = max(high.x - low.x, high.y - low.y) * _Control_TexelSize.z;
    float mip = ceil(log2(max(texels, 1.0))) + 1.0;
    half4 weights = SAMPLE_TEXTURE2D_LOD(_Control, sampler_Control, WorldSplatUV((ua + ub + uc) / 3.0), mip);
    weights = max(weights, SAMPLE_TEXTURE2D_LOD(_Control, sampler_Control, WorldSplatUV(ua), 0));
    weights = max(weights, SAMPLE_TEXTURE2D_LOD(_Control, sampler_Control, WorldSplatUV(ub), 0));
    weights = max(weights, SAMPLE_TEXTURE2D_LOD(_Control, sampler_Control, WorldSplatUV(uc), 0));
    if (dot(weights, 1.0) <= 0.0) return true;
#endif
    return false;
}

WorldTessFactors WorldPatchConstants(InputPatch<WorldControlPoint, 3> patch)
{
    UNITY_SETUP_INSTANCE_ID(patch[0]);
    WorldTessFactors o;
    if (WorldPatchCulled(patch))
    {
        o.edge[0] = o.edge[1] = o.edge[2] = 0;
        o.inside = 0;
        return o;
    }

    o.edge[0] = WorldEdgeFactor(patch[1].positionOS.xyz, patch[2].positionOS.xyz);
    o.edge[1] = WorldEdgeFactor(patch[2].positionOS.xyz, patch[0].positionOS.xyz);
    o.edge[2] = WorldEdgeFactor(patch[0].positionOS.xyz, patch[1].positionOS.xyz);
    o.inside = (o.edge[0] + o.edge[1] + o.edge[2]) / 3.0;
    return o;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("WorldPatchConstants")]
WorldControlPoint WorldHull(InputPatch<WorldControlPoint, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

#endif
