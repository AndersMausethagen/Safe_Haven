# Baking the light: research (2026-11-17)

The user asked: "we gotta bake the light at the scenes. So look at how we can do this to optimise quality and if we can tune it to
increase the feeling we want in each raid." Research only: no project file was changed and the Editor was not touched.

Project facts checked in this session: Unity 6000.6.5f1, URP 17.6.0 and core 17.6.0 (`Library/PackageCache`), `PC_RPAsset` with
Light Probe System = Light Probe Groups (`m_LightProbeSystem: 0`), SH bands L1, scenarios and blending off, reflection probe
blending and box projection off, atlas on, GPU Resident Drawer on with occlusion culling, static batching off for Standalone. No
scene has baked lighting today (`m_LightingDataAsset` is the built-in default in `SafeHaven.unity` and `Raid_MistyWoods.unity`).

## Short answer

- **Only the boss arenas can be baked the normal way.** A raid map does not exist until the loading screen builds it, so an
  Editor bake of a raid scene would light a map nobody plays. The Haven is static land, but the sun moves and the player builds on
  it, so baked sunlight would be wrong most of the day.
- **Raids can still be "baked", at load.** Each run has one fixed hour (`RaidSky` sets the hour and stops the clock), so light
  worked out once while the loading screen is up stays right for the whole run. Unity has no runtime baker for this (the APV baker
  is Editor-only and its GPU probe integrator is `internal`), so it has to be our own: a whole-map **ground light field** built
  from data the generator already holds (heights, trees, rocks, ground paint). It grows out of the existing `SkyOcclusionMap`.
- **The Haven** gets the same field, made by the Haven World generator and saved with the world, holding only what does not
  depend on the hour: how much sky each spot sees. The moving sun stays realtime. Felled and planted trees and buildings update it
  locally.
- **Arenas** get Adaptive Probe Volumes (APV) with sky occlusion and one Lighting Scenario per hour or phase, blended at runtime.
  Unity built that for this exact case.
- **Lightmaps:** not needed anywhere. **Enlighten:** no. **Surface Cache GI** (Unity's new realtime GI, already in our URP source
  behind a define): watch for 6.7 LTS, don't adopt in 6.6.
- **The feeling per raid is mostly data and can start now**: per-raid sky cover strength (today one 0.35 for all raids), per-hour
  ambient in each raid's Day Night profile, a per-raid bounce tint once the field exists, and a water material per raid.
- **The biggest trap:** the World terrain shader is at its 64-texture limit. Anything that darkens or tints the ground needs a slot
  freed first. The field needs one; APV needs about six.

## 1. What Unity 6.6 URP offers

| Tool | State in 6000.6 / URP 17.6 | Raids | Haven | Arenas |
|---|---|---|---|---|
| **Lightmaps** (GPU baker) | The Unity 6 GI blog says the GPU Light Baker is out of preview; the CPU lightmapper is called deprecated in the sky-occlusion docs. Static objects only, per-texel, crisp contact shading | No: map built at load | No: sun moves, light would be wrong | Possible, but can't blend phases |
| **APV** | Probes placed automatically in bricks of 4×4×4, spacing 1, 3, 9 or 27 m by default; every pixel samples the 8 nearest probes. L1 or L2. Static data, baked in the Editor in a Baking Set | No | Possible (sky occlusion) | **Yes** |
| **APV Lighting Scenarios + blending** | Several bakes on the same probe positions; `ProbeReferenceVolume.instance.lightingScenario`, `BlendLightingScenario(name, factor)`, `numberOfCellsBlendedPerFrame`. Needs compute shaders and a blending budget. Only indirect light changes; direct light and moved geometry are ours to script | No | Possible, heavy | **Yes**, one per hour or phase |
| **APV sky occlusion** | Bakes how much sky each probe sees (2048 samples, 2 bounces, grey albedo 0.6 by default); the sky colour comes live from the ambient probe. `DayNightCycle` already writes `RenderSettings.ambientProbe` every frame, and URP reads it per camera, so this works with our day cycle as it is. Sky light only: no sun bounce | No | **Yes** | Yes |
| **APV streaming** | GPU streaming (CPU to GPU) and disk streaming; loads cells in the camera's view | – | Needed at 1 km | Not needed |
| **APV tuning** | `ProbeVolumesOptions` volume override: Intensity Multiplier (0-1), Sky Occlusion Intensity Multiplier (0-5), leak reduction (Quality by default). Probe Adjustment Volume: scale probe intensity in a box, fix leaks, override sky direction | – | – | Per-phase and per-area tuning |
| **Light Probe Groups** | Per object, not per pixel. Positions and SH can be rewritten at runtime (`SetPositionsSelf`, `bakedProbes`, `Tetrahedralize`), but only from a probe set baked into the scene; no way to make one from nothing | Weak: terrain gets one value for the whole map | No | No |
| **Reflection probes** | Baked, realtime or custom. Forward+ evaluates them per pixel, at most two per object, up to 64 in view, with an atlas; box projection is a pipeline and per-probe switch. With indirect draws (our GPU Resident Drawer) only Forward+ and Deferred+ get them, which we are | One follow probe (have) | Same | Baked, box-projected, per phase |
| **Enlighten realtime GI** | Unity's Unity 6 GI blog calls Unity 6 "the last supported release"; the 6.6 manual still documents it. Needs a precompute of static geometry. Objects using it leave the GPU Resident Drawer ("Uses only static global illumination, not real time global illumination") | No | No | No |
| **Surface Cache GI** | Present in our URP 17.6 source (`SurfaceCacheGIRendererFeature`, volume override) but compiled only with a `SURFACE_CACHE` scripting define; Unity's forum calls it a preview "not quite ready for release", shipping in 6.7 LTS. Fully dynamic, diffuse only, compute ray tracing (hardware RT on DX12). Replaces lightmaps and APV while on. From the source: it tracks Mesh Renderers and the terrain heightmap, **not terrain trees**, and is incompatible with static batching (ours is off). Custom shaders need `_SCREEN_SPACE_IRRADIANCE` | Later, maybe | Later, maybe | – |
| **Screen-space GI** | URP 17.6 has none besides the Surface Cache's screen resolve | – | – | – |

## 2. The three cases

### 2.1 Generated raids

**What can be baked:** nothing in the Editor. At load, everything: `RaidMap.Generate` already knows the final heights, every
tree (position, kind, so crown size), boulders, set pieces, ponds and rivers, and the ground paint; the hour and weather seed are
known before the scene loads.

**The ground light field.** One texture over the whole map, made once while the loading screen is up:

- **Cover** (what stands overhead): the existing top-down height render of `SkyOcclusionMap`, drawn once over the whole map instead
  of a 160 m square round the player that is redrawn every 12 m or 10 s. Today each redraw costs about 3 ms of CPU, 2 of them the
  terrain's trees. Felling a tree or a rockfall redraws only that tile (the hooks exist: it already redraws for a building).
- **Relief** (how much sky the ground itself hides): horizon tracing over the generated heightmap in 8 to 16 directions, so
  ravines, terrace cliffs, valley walls and hollows go darker. SSAO reaches 0.8 m; this is the 2 to 30 m scale it can't see from a
  camera 20 to 40 m up.
- **Bounce** (light thrown back from sunlit ground near by): ground colour from the paint layers times how much sun reaches that
  ground at the run's hour (heightmap and canopy shadow), blurred over 4 to 8 m, times a per-raid tint.

Shaders already multiply indirect light by `SkyAmbient(positionWS)` (Tree Wind, Grass, Layered Lit, Mesh Surface via SeasonSnow,
both ASE shaders, See-Through Lit). Pointing `SkyOcclusion.hlsl` at the new field updates all of them in one place. The terrain,
water, decals and the mist need it added (see section 4).

**Cost (estimates, to measure):**
- Memory: cover R16 at 1024² for a 200 m map (20 cm texels, finer than today's 31 cm) is 2 MB, 2048² for a 400 m map 8 MB;
  relief and bounce RGBA8 at 512² is 1 MB.
- Load time: one top-down render (about 6 ms once its terrain data is kept, as measured for the sky map) plus horizon tracing of
  about 250,000 cells × 16 directions × 48 steps in Burst jobs, roughly 0.05 to 0.2 s on top of the 4.7 s load.
- Frame time: a few more texture reads in shaders that already read the sky map, and five on the terrain. The repeated sky-map
  renders go away in raids, so CPU time should fall.

**Sun and hours:** the hour is fixed per run, so the bounce channel is right for the whole run. If a story node changes the hour
mid-raid, rebuild that channel (same jobs, under 0.2 s, off the main thread). Clouds already grey the ambient light
(`DayNightCycle.Apply`); bounce is multiplied by the live sun intensity, so it fades with cloud cover by itself.

**What it would show:** the forest floor darker under dense canopy and lighter in clearings, so rooms and corridors read from
above by light as well as by trees; ravines and the Valley's benches shaded by their walls, so height reads (the project's own
rule: "height reads through steps, rock faces and shade"); a coloured lift in shadow next to sunlit clearings, pale rock or green
moss. Interiors are rare in raids (set pieces only); the cover map handles their roofs as it does today.

**Not recommended for raids:** rewriting legacy light probes at runtime (per object only: the terrain, our largest surface, gets
one value), our own GPU probe baker on Unity's public `UnifiedRayTracing` API (possible, size L, but the 2.5D field gets most of the
look from a top-down camera for a fraction of the work), and Surface Cache GI (it doesn't see terrain trees, so the woods' floor
would get open-sky light).

### 2.2 The Haven

**What can be baked:** the land, the cliffs, the World Builder's rocks and the trees, as sky visibility only. Not sunlight: the
sun moves over a 24-minute day. Not buildings: they are placed in play.

**Recommended: the same field, made by the generator.** `HavenWorldGenerator` already works from a plan that knows the heights
and every tree. Its Finish step can write the relief and cover channels into an asset beside the terrain (1 km at 1 m: 1024²
RGBA8, 4 MB). At runtime:
- felling (`TerrainTreeFelling`, `TerrainTreeMemory`) and planting (`PlantedTrees`) clear or add a crown's footprint in the
  cover channel, so a felled tree's dark spot leaves with it;
- buildings keep using the near-player `SkyOcclusionMap`, which stays for roofs, rain and snow;
- no sun bounce is stored; an optional cheap bounce can use the live sun colour times local ground colour times openness.

Cost: no bake step beyond the generator's (which already takes about 70 s); a few texture reads a pixel; 4 MB.

**The APV alternative for the Haven** (a later step if the field falls short): a sky-occlusion bake in a Baking Set. At 1 m minimum
spacing the 1 km ground would need roughly 14 million probes (about 370 MB at 26 bytes each with sky occlusion); at 3 m about
1.6 million (about 40 MB on disk, GPU streaming keeps only what is in view). Bake time: likely tens of minutes on the 4090, and it
must be redone after every world regeneration. Its weak points here: felled trees stay baked dark, and the terrain, trees, grass and
moss shaders all need APV support (section 4). Lighting Scenarios for sun bounce at several hours, blended as the sun moves, are
possible but multiply the bake and memory for a subtle gain in a place meant to be lighter and greener.

**What it would show:** shade under the maple groves and in the bog's hollows at every hour, a darker rim of woods round the bright
home clearing, and night that isn't flat. Interiors of player buildings: the sky map already dims them; real interior shading would
need ambient occlusion baked into each building model (Blender at import), which works anywhere and at any hour.

### 2.3 Boss arenas

Hand-built, about 120 m of terrain, the same ground every try, a fixed hour, and phases that change the place. This is what
Unity's baking is made for.

**What to bake:** APV with sky occlusion over the arena, L1, minimum spacing 1 m (the camera hangs 20 to 40 m up), maximum 9 m.
One Lighting Scenario per lighting state, baked with Probe Positions = Don't Recalculate after the first, so they can blend. The sun
and the fires set to Mixed (Baked Indirect: direct light stays realtime with its shadows, bounce is baked). Box-projected baked
reflection probes for water and wet stone, one cubemap per phase, swapped with the scenario (`customBakedTexture`).

**Phases, using the boss research's sketches:**
- The Heartroot: open hollow under grey sky → canopy closed, the split heart's glow the only light (a scenario with the heart as a
  Mixed point light and the canopy objects in) → the canopy opens to the sun.
- The Brukam's bridge: one scenario for the fight, and an "after" scenario baked with the fallen segments hidden, blended in at
  phase 2 so the gorge brightens where the stone has gone. The falling segments themselves are dynamic and sample the probes.
- The Nøkk: dusk. The rising pool is a moving object; it reads APV and its own water material.
- The Rime-jutul: night blizzard → "dawn reaches the gorge" as a literal scenario blend over the phase's own clock, with the sun
  rotated by script.

**Cost (estimates):** about 0.3 to 0.5 million probes, under 10 MB a scenario plus a few MB shared; a bake of minutes per scenario on
the 4090; at runtime a probe lookup per pixel (Unity notes the default Quality leak reduction adds a slight overhead), and blending
cells only while a blend runs. The panel's Scenario Size shows the real figure.

**Why not lightmaps here:** they give the crispest contact shading on static stone, but they can't blend between phases, and
objects that move (bridge segments, water) would keep their baked light. If an arena has no lighting phases, lightmapping its stone
is an option later.

**What it would show:** real bounce in the gorge and under the bridge, the hollow darkening as the canopy closes, light that pours
in on cue. These are the "own look" the boss research asks for, and they are the most visible baking wins in the game.

## 3. Tuning each raid's feeling

Levers, all data:
1. **Day Night profile per raid** (`Data/Environment/<Raid> Day Night.asset`): Trilight ambient (sky, equator, ground), fog
   colour, reflection, shadow fill, per hour. Each raid has its own already; tune the four raid hours (Dawn, Day 11, Dusk 19.2,
   Night 23.5).
2. **Per-raid cover and relief strength, per hour** (new, in `RaidDefinition` › Look; today `RenderingSettings.skyOcclusionInRaids`
   = 0.35 for all raids). Lower at night so the player stays readable (the reason for 0.35).
3. **Bounce tint and strength per raid** (new, with the field).
4. **Water material per raid** (the layout rules' pond and river materials): Deep and Shallow Colour, Reflection, Sky Reflection
   Colour, Cloud Reflection.
5. **The mist**: `Atmosphere.shader` can read the field so mist under canopy is darker than mist in a clearing. It is a full-screen
   pass, so the terrain's texture limit doesn't apply.
6. The grade (`RaidDefinition` › Grade) stays the top of the chain. Darker indirect light lowers the average, so recheck exposure.

Starting points (to judge in a launched raid at all four hours, never the raid scene):

**Misty Woods: dense, almost suffocating; "keep it as it is", so make the existing feeling stronger, not different.**
- Cover 0.6 by day, 0.4 at night; relief 0.4. The floor under the cedars goes clearly darker than the clearings, which become pools
  of grey-blue sky light. `CharacterFillLight` keeps the player readable.
- Bounce weak and cool green, mostly in clearings: little sun reaches the floor.
- Ambient ground colour a step darker; equator a little bluer.
- Water dark: Reflection about 0.6, low Sky Reflection Colour, and dimmed under canopy by the field (today water has no sky
  occlusion, so a pond under trees reflects open sky).
- Mist darker under trees than over clearings.

**Troll Valley: open, rocky, drier, lighter, "still mysterious with a dangerous edge".**
- Cover 0.25 (few trees); relief 0.65, so the valley walls and benches shade the floor and the valley reads deep from above.
- Bounce warm and fairly strong (pale rock, dry yellow grass): shadowed wall feet glow warm, which is "lighter" without the Haven's
  green. At Dawn and Dusk, the sunlit wall lifts the shaded floor opposite.
- Ambient sky brighter and less blue than the Woods.
- River brighter and clearer: Reflection about 0.85, more sky colour.

**Moss Marsh: green base, thin green ground mist, wet soil, black water.**
- Cover 0.4 (scrub and alder; reeds and grass details don't cast cover); relief low (flat ground).
- Bounce green from moss ground; water counted as near-black ground colour, so banks stay darker and hummocks lighter.
- Ambient equator and ground greener; sky a little yellow-grey.
- Water black-green: Deep Colour near black, low Sky Reflection Colour, Reflection about 0.7, a slight green in the reflection.
  Yellow flower accents stay as they are.
- The green mist can take the bounce tint so it glows faintly over lit moss.

**Arenas:** per phase, a `ProbeVolumesOptions` volume blended with the scenario (Intensity Multiplier, Sky Occlusion Intensity
Multiplier) plus the arena's own grade.

## 4. How it fits, and the traps

- **The sky-occlusion map** becomes the field's cover channel in raids (one render at load) and stays as it is near the player in
  the Haven. The shaders' `SkyAmbient`/`SkyVisibility` entry points stay, so rain and snow under roofs keep working.
- **The terrain's texture limit (64).** "One more turned the terrain grey ('uses 65 texture parameters')". The field needs one slot
  on the World terrain; APV needs about six (three for L1, validity, sky occlusion, sky direction, plus probe occlusion with
  shadowmask). `WORLD_SLOTS` is 14 (three textures each); the notes say the Haven paints 9 layers and raids fewer. If no terrain
  paints more than 12, two fewer slots frees six reads. Change `GenerateWorldTerrainShader.py`, never the generated files, and check
  images match.
- **Our own shaders don't read APV.** The World terrain, See-Through Lit and both ASE shaders include APV variants. Mesh Surface
  uses URP's Lit pass and needs one include. Tree Wind, Grass, Terrain Moss, Layered Lit, Water and `CharacterShading.hlsl` call
  `SampleSH` directly and would stay on the flat ambient. For the arenas, one shared include (APV where baked, else `SampleSH`) used
  by all of them.
- **APV is pipeline-wide.** Switching `PC_RPAsset` to Adaptive Probe Volumes affects every scene. Where nothing is baked
  (`_EnableProbeVolumes` false) shaders fall back to the ambient probe through a uniform branch, so raids and the Haven are unchanged.
  New keywords mean the shader warm-up needs recording again (`RecordShaderWarmup` Merge), and per the notes, check
  `GraphicsOptions` after any pipeline asset change.
- **GPU Resident Drawer:** fine with APV, lightmaps and Blend Probes (it computes per-instance SH). Not with Use Proxy Volume, and
  not with realtime GI (Enlighten). Reflection probes with indirect draws need Forward+, which every renderer already is.
- **MSAA 8x:** APV, lightmaps and the field shade per pixel, so MSAA changes nothing. Surface Cache GI loads a single-sample
  irradiance texture per pixel; edges would share a value.
- **Terrain trees and grass in bakes:** APV placement voxelises terrain trees for probe density. For occlusion in a bake, an old
  Unity bug said painted trees cast only when they have an LOD group (ours do); confirm with a test bake. Grass details never
  contribute.
- **Decals light themselves** in their screen-space pass. If they don't read the same data, blood and footprints under the canopy
  will glow against darker ground. Check after the field lands.
- **Water** reads no sky occlusion today (`Water.shader` has no `SkyAmbient`); add it with the field.
- **Felled trees vs baked data:** the field updates locally; APV can't. Another reason not to put the Haven on APV first.
- **Co-op:** the field comes from the seed and the generated map, so every game builds the same one with nothing sent. Arena
  scenario blends follow the boss phase, which is already the same in every game.
- **Unsaved scenes:** an APV bake writes data next to the scene and a Baking Set asset. Leave arena scenes saved or reverted after
  every bake (the no-save-popups rule).

## 5. Recommendation and build order

| # | Step | Size | Risks | How to verify |
|---|---|---|---|---|
| 1 | **Per-raid light look in data.** Move sky cover strength into `RaidDefinition` › Look per hour; tune each raid's Day Night profile at the four raid hours; a pond and river material per raid | S | Stacking cold shifts turned the Woods flat blue before; change one lever at a time | Fixed shots in a launched raid: 3 raids × 4 hours, same spots, before/after side by side; the player readable at night |
| 2 | **Free terrain texture reads** in the generator (fewer `WORLD_SLOTS` if no terrain paints that many, or pack) | S-M | A terrain painting more layers falls back to multi-pass | Generator reproduces files; terrain pixel diff under 1 per channel; GPU time unchanged; Haven and raid terrains not grey |
| 3 | **The ground light field in raids** (cover rendered once, relief, bounce at the run's hour), read by `SkyOcclusion.hlsl`, the terrain, water and the mist | M | Too dark under canopy at night; seams at the map edge; decals glowing | Same 12 shots; mean brightness in a 64×64 px patch on forest floor vs clearing per raid; `FrameBenchmark` GPU and CPU before/after; load time per step from `RaidMap`'s log |
| 4 | **The field in the Haven**, written by the generator, updated by felling and planting | M | Regeneration must rewrite it; planted trees' growth changes cover slowly | Shots at 6 Haven spots at dawn, noon, dusk, night; fell a tree and plant one, watch the spot; benchmark |
| 5 | **APV for arenas, once:** pipeline asset to APV with scenarios and blending, APV in the custom shaders through one include, the terrain's slots | M | Shader variant growth; warm-up; light leaks through thin stone | A test arena (the template scene): bake, Scenario Size, bake time; frame time; shots per phase; a 3 s blend with no pop |
| 6 | **Each arena's bake and phases** with its content | S-M each | Scenario must be rebaked after any static change | Per phase: shots, blend timing, reflection probe swap |
| 7 | **Watch Surface Cache GI** in 6.7 LTS for the Haven (moving sun, player buildings) | – | Preview; no terrain trees today | Re-check terrain-tree support and cost at 4K with 8x MSAA |

Skip: lightmaps for raids or the Haven, Enlighten, runtime rewriting of legacy light probes, and our own ray-traced probe baker
(revisit only if the field's 2.5D look falls short in arenas-like places such as ravines).

Order rationale: step 1 answers "tune it to the feeling" now with no new systems; step 2 unblocks everything that touches the
ground; step 3 is where the raids, most of the playing time, gain the most; arenas follow their own build.

### Questions for you

1. Is "baked while the raid loads" (our own field) acceptable for raids, given Unity's baking can't see a generated map?
2. How dark under the Woods' canopy at night? The player must stay readable; the fill light helps, but there is a limit.
3. Arenas: one fixed hour each, or hour changes inside the fight (the Rime-jutul's dawn)? Each change is a scenario to bake.
4. Haven: sky light only (recommended), or also sun bounce through blended scenarios (much larger)?

## Sources

Opened and read:
- Unity 6.6 manual, Adaptive Probe Volumes concept: https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/probevolumes/concept
- Lighting Scenarios: https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/probevolumes/probe-volumes-change-lighting-at-runtime/bakedifferentlightingsetups
- Sky occlusion: https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/probevolumes/probe-volumes-change-lighting-at-runtime/skyocclusion
- Use APV: https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/probevolumes/use
- APV streaming: https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/probevolumes/streaming
- APV panel reference (sky occlusion defaults, Probe Positions): https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/probevolumes/lighting-panel-reference
- APV troubleshooting index: https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/probevolumes/fixissues
- Reflection probes in URP: https://docs.unity.com/en-us/engine/6000.6/manual/lighting-overview/lighting/reflection-probes/introduction
- GPU Resident Drawer compatibility (6.6): https://docs.unity.com/en-us/engine/current/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/make-object-compatible-gpu-rendering
- `LightProbes` script reference (6.6): https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/lightprobes
- Enlighten realtime GI (6.6 manual): https://docs.unity.com/en-us/engine/current/manual/lighting-overview/direct-and-indirect-lighting/lightmapping/realtime-gi-using-enlighten/enlighten
- Unity blog, new ways of applying GI in Unity 6: https://unity.com/en/blog/engine-platform/new-ways-of-applying-global-illumination-in-unity-6
- Forum, Surface Cache GI Preview (20 May 2026): https://discussions.unity.com/t/surface-cache-gi-preview/1720494
- Forum, Changes to Unity's Dynamic GI Roadmap (30 June 2025, update 26 May 2026): https://discussions.unity.com/t/feedback-request-changes-to-unitys-dynamic-gi-roadmap/1660897
- Forum, Global illumination in 6.2: https://discussions.unity.com/t/global-illumination-in-6-2/1616453

Seen only as search results (not opened):
- Unity 6.7 Surface Cache GI volume override reference (404 when fetched): https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/lighting/surface-cache-gi-volume-override-reference
- `Terrain.bakeLightProbesForTrees`: https://docs.unity3d.com/kr//ScriptReference/Terrain-bakeLightProbesForTrees.html
- Painted trees casting in bakes only with an LOD group (old issue): https://issuetracker.unity3d.com/issues/lightmap-static-prefabs-painted-as-trees-dont-cast-shadow-on-terrain

Read in the project's own package source (`Library/PackageCache`, URP and core 17.6.0):
- `universal/Runtime/RendererFeatures/SurfaceCacheGIRendererFeature/*.cs`, `Runtime/Overrides/SurfaceCacheGIVolumeOverride.cs`
  (the `SURFACE_CACHE` define, static batching error, what the world adapter tracks)
- `universal/ShaderLibrary/GlobalIllumination.hlsl` (APV fallback to the ambient probe, screen-space irradiance replacing GI)
- `core/Runtime/Lighting/ProbeVolume/ProbeVolume.hlsl`, `ProbeBrickPool.cs` (APV textures and formats), `ProbeReferenceVolume.cs`
  (blending needs compute and a budget), `ProbeVolumesOptions`, `ProbeAdjustmentVolume.cs`
- `core/Runtime/PathTracing/ProbeIntegrator.cs` (internal), `core/Runtime/UnifiedRayTracing/*` (public)
- `core/Runtime/GPUDriven/SceneProcessors/MeshRendererProcessor.cs` (lightmaps and Blend Probes in the Resident Drawer)

Project files read: `Scripts/ARCHITECTURE.md` (World rendering, raid maps, time of day), `Environment/DayNightCycle.cs`,
`Rendering/FollowReflectionProbe.cs`, `Rendering/RenderingSettings.cs`, `Shaders/SkyOcclusion.hlsl`, the custom shaders' GI paths,
`Settings/Rendering/PC_RPAsset.asset`, `Docs/BossEncounterResearch_2026-11-17.md`, and the memory notes on world rendering, terrain
performance, raid look, art direction and the terrain shader generator.
