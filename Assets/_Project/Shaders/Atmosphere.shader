// Volumetric fog and fog of war for AtmosphereFeature (see Docs/Atmosphere.md).
// Pass 0 "March": half resolution. Marches each view ray to the scene depth, gathering in-scattered light (sun or moon
//   through its shadow cascades, AtmosphereLights) and transmittance, with light-only shafts in a separate target.
// Pass 1 "Composite": full resolution. Colour-preserving shaft lighting, scene × transmittance + fog light, then the night veil beyond every visibility
//   source and the dark fog over unexplored ground.
Shader "Hidden/Safe Haven/Atmosphere"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        #define MAX_FOG_LIGHTS 8
        #define MAX_SOURCES 16

        // density, base height, height falloff, anisotropy
        float4 _FogParams0;
        // main light strength, local light strength, 1 / noise size, noise strength
        float4 _FogParams1;
        // max distance, steps, clearing
        float4 _FogParams2;
        // The player (xyz) and how much of the fog between the camera and them is cleared (w): the column the camera looks down.
        float4 _FogViewClear;
        // That column's radius in metres at the player.
        float _FogViewClearRadius;
        // follow terrain, 1 / bank size, bank strength, bank speed
        float4 _FogParams3;
        float4 _FogAlbedo;
        float4 _FogAmbient;
        float4 _FogWind;
        float _FogDriftSpeed;
        float4 _ShaftParams;                         // density, intensity, height, air glow
        float4 _ShaftColor;                          // linear shaft tint
        float _HasFogGround;
        float _AtmosphereShadowCascades;
        #define MAX_TERRAIN_MISTS 4
        int _TerrainMistCount;
        float4 _TerrainMistRects[MAX_TERRAIN_MISTS]; // x, z, width, depth
        float4 _TerrainMistShapes[MAX_TERRAIN_MISTS];// density, bottom, thickness, edge fade
        float4 _TerrainMistClouds[MAX_TERRAIN_MISTS];// inverse size, coverage, erosion, flow stretch
        float4 _TerrainMistWinds[MAX_TERRAIN_MISTS]; // world x/z velocity, integrated time offset, weather agitation
        float4 _TerrainMistTints[MAX_TERRAIN_MISTS]; // linear tint, sky fill
        float4 _TerrainMistWaves[MAX_TERRAIN_MISTS]; // amplitude, wavelength, travel speed
        float4 _TerrainMistPooling[MAX_TERRAIN_MISTS]; // lowland boost, depression depth, water boost
        float4 _TerrainMistClumps[MAX_TERRAIN_MISTS];  // amount, 1 / billow size, height (m), under-shade
        float4 _TerrainMistBillows[MAX_TERRAIN_MISTS]; // creep (share of the drift), thickness (times density), veil (share)
        int _TerrainMistRegionIndex;                 // which mist has a region map (-1: none)
        float _TerrainMistRegionDensity;             // its density where the map is white
        float _TerrainMistRegionTinted;              // 1: the map's G, B, A are the mist's colour there (HavenLand)
        TEXTURE2D(_TerrainMistRegionTex);            // white where its mist always lies
        #define MAX_MIST_VOLUMES 4
        int _MistVolumeCount;
        float4x4 _MistVolumeWorldToLocal[MAX_MIST_VOLUMES];
        float4 _MistVolumeSizes[MAX_MIST_VOLUMES];
        float4 _MistVolumeShapes[MAX_MIST_VOLUMES];
        float4 _MistVolumeClouds[MAX_MIST_VOLUMES];
        float4 _MistVolumeWinds[MAX_MIST_VOLUMES];
        float4 _MistVolumeTints[MAX_MIST_VOLUMES];
        float4 _MistVolumeWaves[MAX_MIST_VOLUMES];
        float4 _MistVolumeClumps[MAX_MIST_VOLUMES];
        float4 _MistVolumeBillows[MAX_MIST_VOLUMES];
        float _HasMistHabitat;
        // The mist's wake field (MistWakes): r parted, gb pushed (metres). Rect: min x, min z, 1 / size. Params: parting, swirl, reach, on.
        TEXTURE2D(_MistWakeTex);
        float4 _MistWakeRect;
        float4 _MistWakeParams;
        float _FogLightCount;
        float4 _FogLightPositions[MAX_FOG_LIGHTS];   // xyz, range
        float4 _FogLightColors[MAX_FOG_LIGHTS];      // linear rgb × intensity
        float4 _FogLightClearings[MAX_FOG_LIGHTS];   // radius, strength, edge softness
        float _SourceCount;
        float4 _Sources[MAX_SOURCES];                // x, z, radius, softness
        #define MAX_FOG_AREAS 8
        float _FogAreaCount;
        float4 _FogAreas[MAX_FOG_AREAS];             // xyz centre, density
        float4 _FogAreaShapes[MAX_FOG_AREAS];        // 1 / radius, 1 / height, softness
        float4 _GroundRect;                          // x min, z min, 1 / width, 1 / depth
        float4 _GroundRange;                         // lowest height, span
        TEXTURE2D(_GroundTex);

        // unexplored strength, night veil strength, veil desaturation, veil darkening
        float4 _WarParams;
        float4 _UnexploredColor;
        float4 _VeilColor;
        float4 _ExploreRect;                         // x min, z min, 1 / width, 1 / depth
        float _WarGround;                            // height of the ground the player stands on
        TEXTURE2D(_ExploreTex);
        // The Haven's lands (HavenLand): each region's colour filter (RGB, half: 0.5 is as painted) and how forgotten it is (A).
        float4 _LandParams;                          // strength, forgotten desaturation, forgotten darkening, unused
        float4 _LandRect;                            // x min, z min, 1 / width, 1 / depth
        float4 _ForgottenColor;
        TEXTURE2D(_LandTex);
        TEXTURE2D(_AtmosphereFog);
        TEXTURE2D(_AtmosphereShafts);
        float _SafeHavenNight;
        // Temporal accumulation (the Resolve pass): last frame's resolved fog and shafts, where the camera saw from then,
        // how much of them to keep, the frame index the march's jitter turns with, and the fog buffers' texel size.
        TEXTURE2D(_AtmosphereFogHistory);
        TEXTURE2D(_AtmosphereShaftsHistory);
        float4x4 _AtmospherePrevViewProj;
        float _AtmosphereHistoryWeight;
        float _AtmosphereFrame;
        float4 _AtmosphereFogTexel;

        float3 WorldPositionAt(float2 uv, out float rawDepth)
        {
            rawDepth = SampleSceneDepth(uv);
            return ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        }

        bool IsFar(float rawDepth)
        {
        #if UNITY_REVERSED_Z
            return rawDepth <= 0.000001;
        #else
            return rawDepth >= 0.999999;
        #endif
        }

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float ValueNoise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                             lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                             lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        float FogNoise(float3 p)
        {
            return ValueNoise(p) * 0.65 + ValueNoise(p * 2.31 + 7.1) * 0.35;
        }

        // 1 inside a visibility source, fading to 0 at its edge; the strongest source wins.
        float Visibility(float2 xz)
        {
            float seen = 0.0;
            int count = (int)_SourceCount;
            for (int i = 0; i < count; i++)
            {
                float4 s = _Sources[i];
                float inner = s.z * (1.0 - s.w);
                seen = max(seen, 1.0 - smoothstep(inner, s.z, distance(xz, s.xy)));
            }
            return seen;
        }
        ENDHLSL

        Pass
        {
            Name "March"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragMarch
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // A raid's ground light (GroundLightAirAbove): mist under the canopy and down in hollows gets less of the sky's light. 1 elsewhere.
            #include "Assets/_Project/Shaders/SkyOcclusion.hlsl"

            // Ground height under a point, from FogGround's baked height field.
            float GroundHeight(float2 xz)
            {
                float2 uv = saturate((xz - _GroundRect.xy) * _GroundRect.zw);
                return _GroundRange.x + SAMPLE_TEXTURE2D_LOD(_GroundTex, sampler_LinearClamp, uv, 0).r * _GroundRange.y;
            }

            // Pockets of extra fog (FogArea): upright ellipsoids fading out over their edge.
            float AreaDensity(float3 p)
            {
                float density = 0.0;
                int count = (int)_FogAreaCount;
                for (int i = 0; i < count; i++)
                {
                    float4 area = _FogAreas[i];
                    float4 shape = _FogAreaShapes[i];
                    float3 local = (p - area.xyz) * float3(shape.x, shape.y, shape.x);
                    float d = length(local);
                    density += area.w * (1.0 - smoothstep(1.0 - shape.z, 1.0, d));
                }
                return density;
            }

            float Density(float3 p)
            {
                // The base lies at a fixed height, or follows the ground as a blanket.
                float baseHeight = _FogParams0.y;
                if (_FogParams3.x > 0.0)
                {
                    baseHeight = lerp(baseHeight, GroundHeight(p.xz) + _FogParams0.y, _FogParams3.x);
                }

                float above = max(0.0, p.y - baseHeight);
                float density = _FogParams0.x * exp(-above * _FogParams0.z);
                float windTime = (_Time.y + _FogWind.w) * _FogDriftSpeed;
                float3 drift = p * _FogParams1.z - windTime * _FogWind.xyz * _FogParams1.z;
                float patch = saturate(FogNoise(drift) * 1.7 - 0.25);
                density *= lerp(1.0, patch, _FogParams1.w);

                // Large banks rolling over the land: thick where the slow noise is high, clear stretches between.
                if (_FogParams3.z > 0.0)
                {
                    float2 bankUV = p.xz * _FogParams3.y - windTime * _FogWind.xz * _FogParams3.w * _FogParams3.y;
                    float bank = smoothstep(0.3, 0.7, ValueNoise(float3(bankUV, windTime * 0.004)));
                    density *= lerp(1.0, bank * 2.0, _FogParams3.z);
                }

                density += AreaDensity(p) * lerp(1.0, patch, _FogParams1.w * 0.5);
                density *= 1.0 - _FogParams2.z * Visibility(p.xz);
                return density;
            }

            float2 PuffHash(float2 cell)
            {
                float2 p = float2(dot(cell, float2(127.1, 311.7)), dot(cell, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 1 at the heart of a billow, falling to 0 between them: cellular noise whose points wander slowly.
            float Puffs(float2 q, float time)
            {
                float2 cell = floor(q);
                float2 local = q - cell;
                float nearest = 8.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 seed = PuffHash(cell + offset);
                        float2 centre = offset + 0.5 + 0.38 * sin(time * 0.25 + 6.2831 * seed);
                        float2 d = centre - local;
                        nearest = min(nearest, dot(d, d));
                    }
                }

                return 1.0 - saturate(sqrt(nearest));
            }

            // A blanket of mist on the ground (TerrainMist › Clumps), not a cloud layer: a thin, wispy veil over most of the ground (Billows.z
            // of a billow's thickness), and out of it soft, broad billows heaped up, joined to one another by the veil rather than standing
            // apart; it thins to the ground only here and there (Coverage). It creeps slowly (Billows.x of the drift; MistWakes' flow moves
            // at the same speed, round what stands in its way), and its tops boil and fray where they stand. Lit at the tops, shaded at the
            // feet (lit: 1 - under-shade at the ground, 1 at a billow's top).
            float ClumpDensity(float2 horizontal, float height, float4 shape, float4 cloud, float4 wind, float4 clumps, float4 billows,
                out float lit)
            {
                lit = 1.0;
                float rel = height - shape.y;
                float tallest = clumps.z;
                if (rel <= 0.0 || rel >= tallest * 1.6)
                    return 0.0;
                float windTime = _Time.y + wind.z;
                float2 q = (horizontal - windTime * wind.xy * billows.x) * clumps.y;
                // Warped, so the billows aren't round cells laid out in a grid.
                float2 warp = float2(ValueNoise(float3(q * 0.43, windTime * 0.03)), ValueNoise(float3(q.yx * 0.43 + 11.3, windTime * 0.03 + 5.1))) - 0.5;
                q += warp * 1.6;
                // Broad billows, slowly boiling in place (a little of a smaller size in them).
                float puff = smoothstep(0.15, 0.95, Puffs(q, windTime * 0.5) * 0.8 + Puffs(q * 2.17 + 7.31, windTime * 0.8) * 0.2);
                // Where the blanket lies deep and where it thins to the ground: most of the ground is covered.
                float lay = ValueNoise(float3(q * 0.17 + 3.1, windTime * 0.008)) * 0.75 + puff * 0.25;
                float cover = smoothstep(0.55 - cloud.y * 0.6, 0.85 - cloud.y * 0.4, lay);
                float rise01 = rel / max(0.2, tallest);
                float fray = (FogNoise(float3(q * 3.1, rise01 * 2.0 + windTime * 0.1)) - 0.5) * tallest * (0.22 + 0.3 * cloud.z);
                // One surface: a low, wispy veil (streaked along the creep) that swells into a heap over each billow, so the billows are
                // joined by it rather than standing apart. The veil is thinner than a heap (Billows.z of its thickness).
                float wisps = FogNoise(float3(q * float2(0.9, 1.7) + 21.7, windTime * 0.05));
                float heap = smoothstep(0.2, 0.85, puff);
                float veilTop = tallest * (0.45 + 0.25 * wisps);
                float heapTop = tallest * (0.45 + 0.55 * puff);
                float top = lerp(veilTop, heapTop, heap) * smoothstep(0.0, 0.35, cover + 0.15);
                float surface = top + fray * (0.3 + rise01);
                float inside = 1.0 - smoothstep(surface - tallest * (0.12 + 0.1 * heap), surface + tallest * 0.05, rel);
                float thickness = lerp(billows.z * (0.6 + 0.4 * wisps), 1.0, heap);
                lit = lerp(1.0 - clumps.w, 1.0, pow(saturate(rel / max(0.15, surface)), 0.7));
                return shape.x * billows.y * inside * thickness * smoothstep(0.0, 0.05, rel);
            }

            float MistSheetDensity(float2 horizontal, float height, float edge,
                float4 shape, float4 cloud, float4 wind, float4 waves, float pooled)
            {
                if (shape.x <= 0.0 || edge <= 0.0 || height <= shape.y)
                    return 0.0;
                float windTime = _Time.y + wind.z;
                float2 velocity = wind.xy;
                float speed = length(velocity);
                float2 along = speed > 0.0001 ? velocity / speed : float2(1.0, 0.0);
                float2 across = float2(-along.y, along.x);
                float agitation = wind.w;
                float2 waveDrift = (horizontal - along * windTime * waves.z) / max(1.0, waves.y);
                float3 waveUV = float3(dot(waveDrift, along), dot(waveDrift, across), windTime * waves.z * 0.025);
                float2 bend = float2(
                    ValueNoise(waveUV * 0.57 + 17.31),
                    ValueNoise(waveUV.yxz * 0.83 + 43.79)) - 0.5;
                // Irregular rolling fronts, not a periodic sine train of evenly spaced patches.
                float3 rolling = waveUV + float3(bend * (1.8 + agitation * 0.45), 0.0);
                float broad = ValueNoise(rolling);
                float crossing = ValueNoise(float3(rolling.x * 0.61 - rolling.y * 0.79,
                    rolling.x * 0.79 + rolling.y * 0.61, rolling.z * 1.37) * 1.73 + 9.41);
                float crest = smoothstep(0.18, 0.82, broad * 0.65 + crossing * 0.35);
                float roll = crest * 2.0 - 1.0;
                float band = (height - shape.y) / (shape.z + waves.x * crest);
                if (band >= 1.0)
                    return 0.0;
                float2 drift = (horizontal - windTime * velocity) * cloud.x;
                drift += (across * roll + along * bend.y) * (waves.x * (3.0 + agitation * 2.0) * cloud.x);
                float stretch = sqrt(max(1.0, cloud.w));
                float3 uv = float3(dot(drift, along) / stretch, dot(drift, across) * stretch, band * 0.7);
                // Advect the broad veil while smaller wisps shear and roll inside it.
                float phase = windTime * speed * cloud.x;
                float evolution = phase * (0.12 + agitation * 0.16);
                float warp = ValueNoise(uv * 0.63 + 13.7 + float3(0.0, evolution, evolution * 0.67)) - 0.5;
                float3 deformed = uv + float3(warp * (1.8 + agitation * 0.6), bend.x, evolution * 0.35);
                float billow = FogNoise(deformed);
                float detail = ValueNoise(deformed * 4.1 + 29.3 + float3(-phase * 0.7, phase * 0.35, phase * 0.4));
                float threshold = lerp(0.78, 0.15, cloud.y);
                float coverage = smoothstep(threshold - 0.04, threshold + 0.22, billow - (1.0 - detail) * cloud.z * (0.25 + agitation * 0.08));
                // Dense close to the ground, rapidly thinning above the flowing core.
                float above = 1.0 - band;
                float vertical = 2.5 * smoothstep(0.0, 0.06, band) * above * above * above;
                float waveDensity = lerp(1.0, 0.45 + 1.25 * crest, saturate(waves.x * 2.0));
                return shape.x * coverage * vertical * waveDensity * pooled * smoothstep(0.0, shape.w, edge);
            }

            float MistCloudDensity(float2 horizontal, float height, float edge,
                float4 shape, float4 cloud, float4 wind, float4 waves, float pooled, float4 clumps, float4 billows, out float lit)
            {
                lit = 1.0;
                if (shape.x <= 0.0 || edge <= 0.0 || height <= shape.y)
                    return 0.0;
                float edgeFade = smoothstep(0.0, shape.w, edge);
                float clumped = 0.0;
                if (clumps.x > 0.0)
                {
                    clumped = ClumpDensity(horizontal, height, shape, cloud, wind, clumps, billows, lit) * pooled * edgeFade;
                    if (clumps.x >= 0.999)
                        return clumped;
                }

                float sheet = MistSheetDensity(horizontal, height, edge, shape, cloud, wind, waves, pooled);
                lit = lerp(1.0, lit, clumps.x);
                return lerp(sheet, clumped, clumps.x);
            }

            // What has moved through the mist here (MistWakes): how much it is parted (x) and how far its pattern is pushed (yz, metres).
            float3 MistWake(float2 xz)
            {
                if (_MistWakeParams.w <= 0.0)
                    return 0.0;
                float2 uv = (xz - _MistWakeRect.xy) * _MistWakeRect.z;
                if (any(uv <= 0.0) || any(uv >= 1.0))
                    return 0.0;
                float4 wake = SAMPLE_TEXTURE2D_LOD(_MistWakeTex, sampler_LinearClamp, uv, 0);
                // Faded out toward the square's edge, so its end is never a line in the mist.
                float edge = saturate(min(min(uv.x, uv.y), min(1.0 - uv.x, 1.0 - uv.y)) * 8.0);
                return float3(wake.r, wake.g, wake.b) * edge;
            }

            float MistDensity(float3 p, out float3 tintDensity, out float3 skyDensity)
            {
                tintDensity = 0.0;
                skyDensity = 0.0;
                float density = 0.0;
                float3 wake = MistWake(p.xz);
                if (_TerrainMistCount > 0)
                {
                    float4 habitat = 0.0;
                    if (_HasMistHabitat > 0.0)
                        habitat = SAMPLE_TEXTURE2D_LOD(_GroundTex, sampler_LinearClamp,
                            saturate((p.xz - _GroundRect.xy) * _GroundRect.zw), 0);
                    float supportHeight = _HasMistHabitat > 0.0
                        ? _GroundRange.x + habitat.g * _GroundRange.y : GroundHeight(p.xz);
                    for (int i = 0; i < _TerrainMistCount; i++)
                    {
                        float4 rect = _TerrainMistRects[i];
                        float2 local = p.xz - rect.xy;
                        float edge = min(min(local.x, local.y), min(rect.z - local.x, rect.w - local.y));
                        float4 pooling = _TerrainMistPooling[i];
                        float pooled = 1.0 + pooling.x * saturate(habitat.a / max(0.1, pooling.y)) + pooling.z * habitat.b;
                        // Where this terrain's region map is white the mist lies whatever the weather.
                        float4 shape = _TerrainMistShapes[i];
                        // A runtime map (HavenLand) carries each place's mist colour in G, B and A.
                        float3 regionTint = float3(1.0, 1.0, 1.0);
                        if (i == _TerrainMistRegionIndex)
                        {
                            float4 region = SAMPLE_TEXTURE2D_LOD(_TerrainMistRegionTex, sampler_LinearClamp, saturate(local / rect.zw), 0);
                            // HavenLand's map (tinted) holds its density at half scale, so a forgotten region can be thicker than painted.
                            shape.x = max(shape.x, _TerrainMistRegionDensity * region.r * (1.0 + _TerrainMistRegionTinted));
                            regionTint = lerp(regionTint, region.gba, _TerrainMistRegionTinted);
                        }
                        // Low over the ground the wake parts the mist and curls its pattern aside; above Reach it drifts on.
                        float low = 1.0 - smoothstep(_MistWakeParams.z * 0.4, _MistWakeParams.z, p.y - supportHeight);
                        float lit;
                        float cloudDensity = MistCloudDensity(p.xz - wake.yz * (_MistWakeParams.y * low), p.y - supportHeight, edge, shape,
                            _TerrainMistClouds[i], _TerrainMistWinds[i], _TerrainMistWaves[i], pooled, _TerrainMistClumps[i], _TerrainMistBillows[i], lit);
                        cloudDensity *= 1.0 - saturate(wake.x * _MistWakeParams.x * low);
                        density += cloudDensity;
                        // A billow's foot is in its own shade: less of the sun, and somewhat less of the sky.
                        tintDensity += _TerrainMistTints[i].rgb * regionTint * (cloudDensity * lit);
                        skyDensity += _TerrainMistTints[i].rgb * regionTint * (_TerrainMistTints[i].w * cloudDensity * lerp(lit, 1.0, 0.4));
                    }
                }
                for (int i = 0; i < _MistVolumeCount; i++)
                {
                    float3 size = _MistVolumeSizes[i].xyz;
                    float3 local = mul(_MistVolumeWorldToLocal[i], float4(p, 1.0)).xyz + float3(size.x, 0.0, size.z) * 0.5;
                    if (local.y >= size.y)
                        continue;
                    float edge = min(min(local.x, local.z), min(size.x - local.x, size.z - local.z));
                    float lowHere = 1.0 - smoothstep(_MistWakeParams.z * 0.4, _MistWakeParams.z, local.y);
                    float2 pushed = mul((float3x3)_MistVolumeWorldToLocal[i], float3(wake.y, 0.0, wake.z)).xz;
                    float lit;
                    float cloudDensity = MistCloudDensity(local.xz - pushed * (_MistWakeParams.y * lowHere), local.y, edge, _MistVolumeShapes[i],
                        _MistVolumeClouds[i], _MistVolumeWinds[i], _MistVolumeWaves[i], 1.0, _MistVolumeClumps[i], _MistVolumeBillows[i], lit);
                    cloudDensity *= 1.0 - saturate(wake.x * _MistWakeParams.x * lowHere);
                    cloudDensity *= smoothstep(0.0, size.y * 0.1, size.y - local.y);
                    density += cloudDensity;
                    tintDensity += _MistVolumeTints[i].rgb * (cloudDensity * lit);
                    skyDensity += _MistVolumeTints[i].rgb * (_MistVolumeTints[i].w * cloudDensity * lerp(lit, 1.0, 0.4));
                }
                return density;
            }

            // Screen-space shadows describe the opaque surface, not a sample floating in the air.
            // Always sample the directional atlas at the raymarch position, including on screen-shadow renderers.
            float AirShadow(float3 p)
            {
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                half cascade = _AtmosphereShadowCascades > 1.0 ? ComputeCascadeIndex(p) : 0;
                float4 coord = float4(mul(_MainLightWorldToShadow[cascade], float4(p, 1.0)).xyz, 1.0);
                half4 parameters = GetMainLightShadowParams();
                parameters.x = 1.0;
                return SampleShadowmap(TEXTURE2D_SHADOW_ARGS(_MainLightShadowmapTexture, sampler_LinearClampCompare),
                    coord, GetMainLightShadowSamplingData(), parameters, false);
            #else
                return 1.0;
            #endif
            }

            // Henyey-Greenstein, scaled so isotropic scattering is 1.
            float Phase(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / pow(max(1e-4, 1.0 + g2 - 2.0 * g * cosTheta), 1.5);
            }

            float3 LocalLights(float3 p)
            {
                float3 light = 0.0;
                int count = (int)_FogLightCount;
                for (int i = 0; i < count; i++)
                {
                    float4 l = _FogLightPositions[i];
                    float3 offset = l.xyz - p;
                    float d2 = dot(offset, offset);
                    float range2 = l.w * l.w;
                    if (d2 < range2)
                    {
                        float fade = 1.0 - d2 / range2;
                        light += _FogLightColors[i].rgb * (fade * fade / (1.0 + d2 * 0.35));
                    }
                }
                return light;
            }

            float LightFogRemaining(float3 p)
            {
                float clearing = 0.0;
                for (int i = 0; i < (int)_FogLightCount; i++)
                {
                    float3 settings = _FogLightClearings[i].xyz;
                    if (settings.x > 0.0 && settings.y > 0.0)
                    {
                        float radius = settings.x;
                        float inner = radius * (1.0 - settings.z);
                        float amount = 1.0 - smoothstep(inner, radius, distance(p, _FogLightPositions[i].xyz));
                        clearing = max(clearing, amount * settings.y);
                    }
                }
                return 1.0 - saturate(clearing);
            }

            // Thinner fog in the column from the camera down to the player (the user, 2026-11-17: "make the fog thinner from the camera
            // down to the player"): full near the line between them, fading out at its radius and just past the player, so the fog
            // beyond and around them stays.
            float ViewColumnRemaining(float3 p)
            {
                if (_FogViewClear.w <= 0.0)
                {
                    return 1.0;
                }

                float3 a = _WorldSpaceCameraPos;
                float3 ab = _FogViewClear.xyz - a;
                float along = dot(p - a, ab) / max(dot(ab, ab), 1e-4);
                float t = saturate(along);
                float radius = _FogViewClearRadius * lerp(0.6, 1.0, t);
                float inside = 1.0 - smoothstep(radius * 0.45, radius, distance(p, a + ab * t));
                inside *= 1.0 - smoothstep(1.0, 1.12, along);
                return 1.0 - inside * _FogViewClear.w;
            }

            struct MarchOutput
            {
                float4 fog : SV_Target0;
                float4 shafts : SV_Target1;
            };

            MarchOutput FragMarch(Varyings input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float rawDepth;
                float3 positionWS = WorldPositionAt(uv, rawDepth);
                float3 origin = _WorldSpaceCameraPos;
                float3 ray = positionWS - origin;
                float sceneDistance = length(ray);
                float3 direction = ray / max(sceneDistance, 1e-4);
                float maxDistance = _FogParams2.x;
                float marchDistance = IsFar(rawDepth) ? maxDistance : min(sceneDistance, maxDistance);

                int steps = max(1, (int)_FogParams2.y);
                float stepLength = marchDistance / steps;
                // Interleaved gradient noise hides the banding of few steps; it turns every frame, and the Resolve pass
                // averages the frames, so the grain melts into smooth fog and shafts.
                float2 grainAt = input.positionCS.xy + _AtmosphereFrame * float2(5.588238, 5.588238);
                float jitter = frac(52.9829189 * frac(dot(grainAt, float2(0.06711056, 0.00583715))));

                Light mainLight = GetMainLight();
                float phase = Phase(dot(direction, mainLight.direction), _FogParams0.w);
                float3 sunLight = mainLight.color * (phase * _FogParams1.x);
                // The gameplay camera looks down, away from the sun: a gentler phase keeps beams readable from above.
                float shaftPhase = Phase(dot(direction, mainLight.direction), _FogParams0.w * 0.3);
                float3 shaftLight = mainLight.color * _ShaftColor.rgb * shaftPhase * _ShaftParams.y;

                float transmittance = 1.0;
                float shaftTransmittance = 1.0;
                float3 gathered = 0.0;
                float3 gatheredShafts = 0.0;
                float underCanopy = 0.0;
                float3 p = origin + direction * (stepLength * jitter);
                for (int i = 0; i < steps; i++)
                {
                    float fogDensity = Density(p);
                    float3 mistTintDensity, mistSkyDensity;
                    float mistDensity = MistDensity(p, mistTintDensity, mistSkyDensity);
                    float shaftDensity = 0.0;
                    if (_ShaftParams.x > 0.0)
                    {
                        float ground = _HasFogGround > 0.0 ? GroundHeight(p.xz) : _FogParams0.y;
                        shaftDensity = _ShaftParams.x * exp(-max(0.0, p.y - ground) / _ShaftParams.z);
                    }
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    shaftDensity *= 1.0 - GetMainLightShadowFade(p);
                #else
                    shaftDensity = 0.0;
                #endif
                    float remaining = LightFogRemaining(p) * ViewColumnRemaining(p);
                    fogDensity *= remaining;
                    mistDensity *= remaining;
                    mistTintDensity *= remaining;
                    mistSkyDensity *= remaining;
                    shaftDensity *= remaining;
                    float density = fogDensity + mistDensity;
                    if (density > 1e-5 || shaftDensity > 1e-5)
                    {
                        float occlusion = AirShadow(p);
                        // Shaft particles gather light, but never replace the scene with a pale fog colour.
                        if (shaftDensity > 1e-5)
                        {
                            float stepShaftTransmittance = exp(-shaftDensity * stepLength);
                            // Only lit air below foliage forms a beam: the ray must already have passed leaf shadow.
                            // Air above the canopy or over open ground stays clear instead of glowing over shadows.
                            gatheredShafts += transmittance * shaftTransmittance * (1.0 - stepShaftTransmittance)
                                * shaftLight * occlusion * underCanopy;
                            shaftTransmittance *= stepShaftTransmittance;
                            underCanopy = max(underCanopy, 1.0 - occlusion);
                        }
                        if (density > 1e-5)
                        {
                            float shadow = lerp(1.0, occlusion, GetMainLightShadowParams().x);
                            float3 directLight = sunLight * shadow + LocalLights(p) * _FogParams1.y;
                            // The land's shade fades out with height over the ground (mist high above a ravine isn't
                            // shaded like its floor); 1 without a field, and no ground sample then.
                            float air = 1.0;
                            UNITY_BRANCH if (_GroundLightRect.w > 0.0)
                            {
                                air = GroundLightAirAbove(p, _HasFogGround > 0.0 ? p.y - GroundHeight(p.xz) : 0.0);
                            }
                            float3 scatter = ((_FogAmbient.rgb * air + directLight) * _FogAlbedo.rgb * fogDensity
                                + mistTintDensity * directLight + mistSkyDensity * air) / density;
                            float stepTransmittance = exp(-density * stepLength);
                            gathered += transmittance * (1.0 - stepTransmittance) * scatter;
                            transmittance *= stepTransmittance;
                        }
                        if (transmittance < 0.01)
                        {
                            break;
                        }
                    }
                    p += direction * stepLength;
                }

                MarchOutput output;
                output.fog = float4(gathered, transmittance);
                output.shafts = float4(gatheredShafts, 0.0);
                return output;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            float4 FragComposite(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                float4 fog = SAMPLE_TEXTURE2D(_AtmosphereFog, sampler_LinearClamp, uv);
                float3 shafts = max(0.0, SAMPLE_TEXTURE2D(_AtmosphereShafts, sampler_LinearClamp, uv).rgb);

                float rawDepth = 0.0;
                float3 positionWS = float3(0.0, 0.0, 0.0);
                if (_WarParams.x > 0.0 || _WarParams.y > 0.0 || _LandParams.x > 0.0)
                {
                    positionWS = WorldPositionAt(uv, rawDepth);
                }

                // The Haven's lands: each region's own colour, and forgotten land grey under the forest's forgetting (HavenLand).
                // On the surface seen, before the mist over it is laid on: the mist and the light shafts keep their own colour, not
                // the grey of whatever lies beyond them (bands at a region's border, darkened mist: the code review).
                if (_LandParams.x > 0.0 && !IsFar(rawDepth))
                {
                    float2 landUV = (positionWS.xz - _LandRect.xy) * _LandRect.zw;
                    if (all(landUV >= 0.0) && all(landUV <= 1.0))
                    {
                        float4 land = SAMPLE_TEXTURE2D_LOD(_LandTex, sampler_LinearClamp, landUV, 0);
                        float3 own = color * (land.rgb * 2.0);
                        float luminance = dot(own, float3(0.299, 0.587, 0.114));
                        float tintLuminance = max(0.05, dot(_ForgottenColor.rgb, float3(0.299, 0.587, 0.114)));
                        float3 grey = luminance * _ForgottenColor.rgb / tintLuminance;
                        float3 forgotten = lerp(own, grey, _LandParams.y) * (1.0 - _LandParams.z);
                        color = lerp(color, lerp(own, forgotten, land.a), _LandParams.x);
                    }
                }

                // Shafts are light in the air only: surfaces keep their own colour (no tint or gain). The beam is a
                // soft, mostly neutral screen blend, masked by scene luminance so it never clips bright areas.
                shafts /= 1.0 + max(max(shafts.r, shafts.g), shafts.b);
                float beamLuma = dot(shafts, float3(0.2126, 0.7152, 0.0722));
                float3 beam = lerp(beamLuma.xxx, shafts, 0.35) * _ShaftParams.w;
                color *= fog.a;
                color += beam * (1.0 - saturate(dot(color, float3(0.2126, 0.7152, 0.0722))));
                color += fog.rgb;

                if (_WarParams.x <= 0.0 && _WarParams.y <= 0.0)
                {
                    return float4(color, 1.0);
                }

                float3 view = positionWS - _WorldSpaceCameraPos;
                float sceneDistance = length(view);
                float3 direction = view / max(sceneDistance, 1e-4);
                // Fog of war belongs to the ground: anything standing above it (a tree crown near the camera) takes the
                // ground behind it along the view ray, so it is never darker than what it stands over.
                float toGround = direction.y < -0.01 ? (_WarGround - _WorldSpaceCameraPos.y) / direction.y : -1.0;
                if (toGround > 0.0 && (IsFar(rawDepth) || sceneDistance < toGround))
                {
                    positionWS = _WorldSpaceCameraPos + direction * toGround;
                }
                else if (IsFar(rawDepth))
                {
                    positionWS = _WorldSpaceCameraPos + direction * 120.0;
                }

                // Night: what lies beyond every source of sight and light turns grey and dim.
                float veil = saturate(_WarParams.y * _SafeHavenNight * (1.0 - Visibility(positionWS.xz)));
                if (veil > 0.0)
                {
                    float luminance = dot(color, float3(0.299, 0.587, 0.114));
                    float tintLuminance = max(0.05, dot(_VeilColor.rgb, float3(0.299, 0.587, 0.114)));
                    float3 grey = luminance * _VeilColor.rgb / tintLuminance;
                    float3 veiled = lerp(color, grey, _WarParams.z) * (1.0 - _WarParams.w) + _VeilColor.rgb * _VeilColor.a;
                    color = lerp(color, veiled, veil);
                }

                // Any hour: ground never seen lies under dark, slowly turning fog.
                if (_WarParams.x > 0.0)
                {
                    float2 exploreUV = (positionWS.xz - _ExploreRect.xy) * _ExploreRect.zw;
                    float inside = step(0.0, exploreUV.x) * step(0.0, exploreUV.y) * step(exploreUV.x, 1.0) * step(exploreUV.y, 1.0);
                    float explored = SAMPLE_TEXTURE2D(_ExploreTex, sampler_LinearClamp, exploreUV).r * inside;
                    float hide = _WarParams.x * (1.0 - smoothstep(0.1, 0.9, explored));
                    if (hide > 0.0)
                    {
                        float swirl = FogNoise(float3(positionWS.x * 0.07, _Time.y * 0.06, positionWS.z * 0.07));
                        float3 dark = _UnexploredColor.rgb * (0.55 + 0.9 * swirl);
                        color = lerp(color, dark, saturate(hide));
                    }
                }

                return float4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Resolve"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragResolve

            struct ResolveOutput
            {
                float4 fog : SV_Target0;
                float4 shafts : SV_Target1;
            };

            // This frame's march blended with last frame's, reprojected to where the camera saw the same air then. Last
            // frame's value is clamped to the range of this frame's neighbours, so moving light or fog never ghosts.
            ResolveOutput FragResolve(Varyings input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 texel = _AtmosphereFogTexel.xy;
                float4 fog = SAMPLE_TEXTURE2D(_AtmosphereFog, sampler_LinearClamp, uv);
                float4 shafts = SAMPLE_TEXTURE2D(_AtmosphereShafts, sampler_LinearClamp, uv);
                float4 fogLow = fog, fogHigh = fog, shaftLow = shafts, shaftHigh = shafts;
                float2 offsets[4] = { float2(texel.x, 0), float2(-texel.x, 0), float2(0, texel.y), float2(0, -texel.y) };
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 f = SAMPLE_TEXTURE2D(_AtmosphereFog, sampler_LinearClamp, uv + offsets[i]);
                    float4 s = SAMPLE_TEXTURE2D(_AtmosphereShafts, sampler_LinearClamp, uv + offsets[i]);
                    fogLow = min(fogLow, f); fogHigh = max(fogHigh, f);
                    shaftLow = min(shaftLow, s); shaftHigh = max(shaftHigh, s);
                }

                float rawDepth;
                float3 positionWS = WorldPositionAt(uv, rawDepth);
                float3 ray = positionWS - _WorldSpaceCameraPos;
                float reach = IsFar(rawDepth) ? _FogParams2.x : min(length(ray), _FogParams2.x);
                positionWS = _WorldSpaceCameraPos + normalize(ray) * reach;
                float2 before = ComputeNormalizedDeviceCoordinates(positionWS, _AtmospherePrevViewProj);
                float keep = _AtmosphereHistoryWeight;
                if (any(before < 0.0) || any(before > 1.0))
                {
                    keep = 0.0;
                }

                float4 oldFog = clamp(SAMPLE_TEXTURE2D(_AtmosphereFogHistory, sampler_LinearClamp, before), fogLow, fogHigh);
                float4 oldShafts = clamp(SAMPLE_TEXTURE2D(_AtmosphereShaftsHistory, sampler_LinearClamp, before), shaftLow, shaftHigh);
                ResolveOutput output;
                output.fog = lerp(fog, oldFog, keep);
                output.shafts = lerp(shafts, oldShafts, keep);
                return output;
            }
            ENDHLSL
        }
    }
}
