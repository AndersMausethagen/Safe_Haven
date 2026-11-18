# Look and feel: where we lack and what to improve (research, 2026-11-18)

The user asked: "research how we can improve the look and feel of the whole game: modern rendering methods, graphic methods we can
enable, more realistic terrain, VFX, SFX, UX etc. Where are we lacking and what can we improve?"

This is research only. I changed no project file, did not touch the Editor and ran no builds or tests. Sizes follow the project's
own convention: **S** under half a day, **M** one to two days, **L** more. GPU and CPU costs are **estimates to measure** unless they
quote a project measurement. Unity publishes almost no per-feature millisecond numbers. I mark claims I could not verify.

**What I read:** `Scripts/ARCHITECTURE.md` (world rendering, the top-down drawing notes, sound, fire, combat feel, options, look and
lighting), `Docs/WorldLookPlan_2026-11-17.md`, `WorldDesignReview_2026-11-17.md` (§6-7), `GameDesignResearch_2026-11-17.md`
(§0, 1, 4), `LightingBakeResearch_2026-11-17.md` (§1), both AgentDecisions logs, `Atmosphere.md`, `Water.md`, `TODO.md` (searched),
`PC_RPAsset`, `PC_Renderer`, the volume list, shader headers (Terrain generator, MeshSurface, Grass, TreeWind, Water, Sky,
Atmosphere, GroundLight), `GameOptions.cs`, the audio scripts, the raid scenes' audio settings, the raid height-field code, and the
screenshots `strata_*`, `rim_*`, `g3_game`, `g7_*`, `marsh_*`, `bb_gorge`, `drips_*` in the session scratchpad.

---

## 0. The short answer

The game already has more rendering machinery than most indie URP games: custom volumetric fog, mist, shafts, fog of war, a
load-time ground light field, tessellated triplanar terrain, cloud-shadow cookies, wetness, decals, footprints, ground impacts, DLSS
4, FSR 2 and STP, and a rich water shader. **Its gaps are not missing big features.** They are seams where those systems meet, plus
two areas that are thin:

1. **Sound is the thinnest layer.** All three raids play **the same ambience loop** (a SeedMesh forest-wind clip, volume 0.55, in
   `Raid_MistyWoods`, `Raid_TrollValley` and `Raid_MossMarsh`). The raid scenes have no `AmbientOneShots` and no `PlacedAmbience`.
   There is no AudioMixer, no reverb and no occlusion anywhere in the code. A biome is half its sound, so this is the cheapest
   large gain.
2. **The ground is too smooth because the generator makes it smooth, not because of resolution.** The raid height field is
   0.39 m per sample (513² over 200 m). The smoothness is authored: banks, pond shores and rims are `Mathf.SmoothStep` profiles
   (`RaidLayoutField.Rivers.cs:125`, `RaidLayoutField.Ground.cs:128-155`). The new layered rock (`WorldStrata`) is colour only,
   so silhouettes and lips stay round. The fix is a shape pass in the height field plus geometric beds in the existing
   tessellation.
3. **Things don't sit in the ground.** Rocks, logs and props meet the terrain at a hard, equally lit seam (`strata_..._game`,
   `rim_game`). Rock-to-terrain colour blending and screen-space contact shadows would make every screen look more grounded from
   the top-down camera.
4. **Half the sun's shadow resolution goes to air.** The cascade splits (0.08 / 0.22 / 0.48 of 50 m: 4, 11, 24 m) put the ground
   the top-down camera sees (about 16-30 m from the lens at the default zoom) in cascades 3 and 4 only. Re-fitting the splits is a
   settings change that should sharpen every shadow on the play area. It may also explain why Ultra needed an 8K map.
5. **Water reads grey from above, which is physically wrong.** At the camera's 50° pitch, water reflects only about 2-6% of the
   sky (Fresnel). What you should see is the bed, the depth colour and the foam. The Valley river's Reflection 0.8 and its
   uniform shore foam make it a grey canal (`strata_..._game`, `g7_top`).
6. **Unity 6.7 brings GTAO, SSR and Surface Cache GI to URP.** It is in beta (6000.7.0b7) with LTS due at the end of 2026. Wait
   for it rather than building them. The groundwork that pays off then is **motion vectors on the grass**, which temporal AO, TAA,
   STP and DLAA all need.

The ranked list is in §11 and the data-only quick wins are in §12.

---

## 1. What already exists (so none of this is re-recommended)

| Area | Built |
|---|---|
| Pipeline | URP 17.6, Forward+, Render Graph; GPU Resident Drawer with GPU occlusion (measured: trims triangles, no frame time); SRP Batcher; LOD cross-fade |
| AA and upscaling | 4x MSAA default; TAA, STP, FSR 1, DLSS 4 (DLAA at 100%), FSR 2 as options; alpha-to-coverage on tree leaves |
| Shadows | 4096 main map (8K on Ultra via options), 4 cascades over 50 m, soft shadows high; `LightShadowBudget` gives the 3 nearest fires soft shadows; tree shadow proxies |
| Indirect light | SSAO (blue noise, full res, depth-normals, 0.8 m); `SkyOcclusionMap`; the raids' load-time **ground light field** (canopy and land shade, ground bounce); follow-the-player realtime reflection probe |
| Atmosphere | `AtmosphereFeature`: half-res volumetric fog with temporal history, sun/moon shafts through the shadow atlas, terrain mist with waves and hollows, mist volumes, torch clearing, fog of war, night veil, per-raid and night volumes |
| Post | Custom tonemapping (Khronos PBR Neutral / AgX), per-raid grade (`RaidGradeVolume`), bloom, vignette, film grain, motion blur, white balance, split toning, channel mixer; option overrides |
| Sky | Gradient `Sky.shader` with sun, moon phase, stars, clouds sharing the cloud-shadow pattern, lightning |
| Terrain | Generated URP Terrain Lit, single pass up to 14 layers, triplanar rock past a slope with anti-tiling, tessellated displacement focused on the player (15-25 m), height blend, macro variation, `WorldStrata` beds (colour), puddles and wetness, footprints, ground impact craters, `TerrainMoss` skin |
| Plants | Detail grass with wind waves, interaction, terrain-colour tint, translucency; Tree Wind with fast leaf lighting and motion vectors; shrub bending |
| Water | Depth colour, clarity, refraction, caustics, mesh-following flow (the flow-map technique), shore, stone, speed and steep foam, GPU ripple sim, rain rings, wind chop, SSR in the shader, waterfalls with plunge, lily pads, per-raid materials |
| VFX | Pooled hit, crit, block, parry, stagger and perfect-dodge sparks; blood and pools; weapon trail; hit flash, punch, knockback; ground impacts (gusts, dust, craters); gathering chips; procedural fire with sparks, embers, heat haze; rain splashes and drips; fireflies per raid; birds; wildlife; leaf fall |
| Feel | Hit stop, trauma shake, rumble, telegraph tiers with parry glint and opening ring, off-screen grace by the real viewport, death advice, see-through occluders, enemy outlines through cover, player light, damage numbers |
| Audio | `AudioManager` (voices, priorities, ears at the player), ducking, explore / combat / final-stretch / event music with memory, weather loops and thunder, nearest-point stream loops, footsteps by surface, creature voices, captions, spoken dialogue |
| Look process | Measured colour targets per raid (WorldLookPlan), a capture that shows the air (B13), first-pass data A2-A18 and generator steps B1-B7, B11 done (commit 2bef4560) |

Still open from earlier plans (not repeated below unless I add something): WorldLookPlan B8-B10, B12, C1-C7; the world review's
L4 canopy dapple cookie, L5 birds per biome, L6 audio landmarks; GameDesignResearch T1 music by danger stage, A3 colour-free
telegraphs, G2 fight camera (the user's decision); the MeshSurface DOTS-instancing step; the Rim Ledge slabs (waiting for the Editor
to close); APV for the arenas.

---

## 2. What the screenshots show

| Shot | What reads wrong |
|---|---|
| `strata_s_0.35_2.2_0.8_40_2.5_game` (latest, top-down Valley) | River: flat grey band with a white foam line along **both** banks; the bank is a smooth curve from meadow to water. Meadow: one even olive-tan with low micro-contrast. **Dark star-shaped plants** (they look near-black even in sun) dot the ground like holes. Rocks sit on the ground with no contact shading or colour blend. The cliff (top right) with beds reads well |
| `strata_..._eye` and `strata_close` (eye level) | The beds work. The rim and the face silhouettes are smooth sweeps; no lip juts out. The sky is saturated cyan with **salmon-pink clouds at a daytime hour**, which looks synthetic |
| `rim_below` | Smooth rounded rim, the beds are faint lines on a smooth wall. Birch foliage lit flat and grey (the fast leaf lighting is per vertex) |
| `g7_edge`, `g7_top` (before the 40° cliff-rock change) | **White glittering bands** on 30-45° slopes and shelf lips: very likely specular aliasing on the pebbly rock layer. The latest strata shots don't show it, so the 40° change may have removed most of it. Check slopes between 30° and 40° |
| `marsh_The_Black_Pool_Landing_game` | Black pools with a **bright milky halo** round every edge, which reads as a glow, not as a shore. Otherwise the marsh's plants and stones read well |
| `bb_gorge` | Bare flat ground, smooth gorge walls, a hard-edged mist curtain (built on purpose, but its sides are a lozenge), lavender-pink sky |
| `drips_game` | The moss skin's bubbly pattern repeats visibly across the frame |
| `drips_dusk_green` | Telegraph wedges are flat opaque green half-discs with no rim or gradient; they read as stickers rather than as something on the ground |

---

## 3. Rendering features in Unity 6 URP

### 3.1 Re-fit the shadow cascades to the top-down camera (quick win)

- **Have:** `PC_RPAsset`: 50 m, 4 cascades, splits 0.08 / 0.22 / 0.48 (cascade ends at 4, 11, 24, 50 m). ARCHITECTURE's "Look and
  lighting" table still says "2 cascades split at 30 m", so the doc is stale.
- **Lacking:** the explore camera (`PlayerCameraSettings`: pitch 50°, FOV 40°, 14-28 m) at its default 21 m sits about 16 m up. The
  ground on screen spans a view depth of about **16 m (bottom edge) to 30 m (top edge)**. So the play area is in cascade 3
  (11-24 m) and cascade 4 (24-50 m). Cascades 1 and 2 (0-11 m) hold only what is within 11 m of the lens: canopy near the camera,
  which `SeeThrough` dissolves around the player anyway. Half the atlas is spent where the player isn't looking. This is likely
  why a 4K map "was too coarse for the ground's own relief" and Ultra needed 8K (`GameOptions.cs` comment, 2026-11-09).
- **Do:** splits of roughly 0.24 / 0.42 / 0.66 (12, 21, 33 m), so the player's half of the screen gets its own cascade. Because
  the action view (V) and cinematics need near cascades, have `GraphicsOptions` (which already works on a runtime copy of the
  asset) set the splits per camera style. Check the result with the Rendering Debugger's shadow-cascade view at zoom 14, 21 and
  28 m.
- **Gain:** an estimated 1.5-3× texel density on the play area, so sharper contact shadows of stones, roots and feet at every zoom.
  It might let Ultra return to a 4K map, saving VRAM and shadow-pass time.
- **Cost:** S (data, plus a few lines for the camera-style switch). GPU: none, possibly a saving.
- **Risk:** low. Watch for a cascade seam across the middle of the screen; tune the splits so the seam falls in the less-watched
  upper half.
- **Top-down fit:** high. It exists for the top-down camera.

### 3.2 Screen-space contact shadows (Bend Studio's method)

- **Have:** shadow maps only. URP has no contact shadows (HDRP and Unreal do).
- **Lacking:** small things (pebbles, grass clumps, the lip of a footprint, a character's feet, a log on the ground) don't darken
  the ground under them crisply. This is the "placed, not grounded" look.
- **Do:** Bend Studio published its screen-space shadow technique, used in Days Gone and other Sony titles, with public code and
  a SIGGRAPH 2023 talk. Unreal ships it as its contact-shadow pass, and Ghost of Tsushima uses the same idea [Bend; Epic;
  WinterCrownWorks]. Implement it as a Render Graph compute pass after the depth prepass (the tessellated terrain already writes
  DepthOnly), multiplied into the main-light shadow term. **Check Bend's code licence before copying it.**
- **Gain:** high and visible on every screen from above. It complements 3.1: maps give the big shapes, screen space gives the last
  few centimetres.
- **Cost:** M. GPU: one short ray march per pixel along the light direction; probably under 1 ms at 4K on a 4090 (**unverified,
  measure**). An option tier (Off / On) under Shadows.
- **Risk:** medium. Screen-space shadows can't see off-screen casters, and thin alpha-tested leaves give noise; restrict the pass
  to short rays (about 0.3-1 m).
- **Top-down fit:** high.

### 3.3 Anti-aliasing: get grass motion vectors, then consider DLAA or TAA for Ultra

- **Have:** 4x MSAA by default; TAA, STP and DLAA as options. ARCHITECTURE: "terrain grass has no motion-vector path and softens
  under them. TAA is an option, not the default." Tree Wind already writes motion vectors.
- **Lacking:** MSAA can't fix alpha-tested foliage (A2C on trees helps), specular shimmer or tessellation shimmer. Unity 6.7's
  GTAO also wants motion vectors for its temporal filter [Unity GTAO preview].
- **Do:**
  1. Find out whether Unity's terrain detail renderer calls a `MotionVectors` pass when the shader has one and the
     `AlwaysRenderMotionVectors` tag, as terrain trees do (**unverified**). If it does, add the pass to `Grass.shader`: previous
     frame's wind bend from the previous time.
  2. If it doesn't, the alternatives are drawing grass through our own instanced path (L), or leaving MSAA as the default.
  3. Once grass has motion vectors, measure **DLAA at 100%** (RTX) and TAA or STP against 4x MSAA at 4K. In Forward+ with HDR,
     MSAA is bandwidth-heavy, so a temporal AA may cost about the same or less (**unverified**). If it looks better, make it the
     Ultra default where DLSS runs. That is a quality increase, not a downgrade.
- **Gain:** medium-high. Stable foliage, no sparkling, cleaner thin branches.
- **Cost:** S-M for the pass, M if grass has to be drawn ourselves.
- **Risk:** low-medium (ghosting on fast plants).
- **Top-down fit:** high. Foliage fills most of the frame.

### 3.4 Geometric specular anti-aliasing in the terrain and MeshSurface

- **Have:** normal-mapped, tessellated rock with smoothness.
- **Lacking:** the white glitter in `g7_*`, which is classic specular aliasing.
- **Do:** add the Kaplanyan / Tokuyoshi variance term (widen roughness by the screen-space derivative of the normal). HDRP's Lit
  does this [Kaplanyan 2016; Tokuyoshi & Kaplanyan 2021]. It is a few ALU in the generator's lighting and in `MeshSurface`.
- **Gain:** removes sparkle at every AA setting. **Cost:** S; GPU negligible. **Risk:** low (slightly duller wet rock: keep the
  term off where `SurfaceWet` raises smoothness, or clamp it).

### 3.5 What's coming in 6.7, and what not to build

| Feature | State | Advice |
|---|---|---|
| **GTAO** in URP | 6.7 alpha/beta; "costs a little more than SSAO"; compute path; temporal filter needs motion vectors [Unity GTAO preview; 6.7 beta notes] | Wait for 6.7 LTS. Grass motion vectors (3.3) are the groundwork |
| **SSR** in URP | Preview targeted at 6.7; on-screen only, falls back to probes; half and quarter res looked blurry on water [Unity SSR preview] | The water already has its own SSR. From 50° pitch water reflects ~2-6% (§5), so URP SSR adds little for the top-down view; maybe for wet stone in the V view |
| **Surface Cache GI** | Preview May 2026, "broadly available in 6.7 LTS"; replaces lightmaps and APV when on; no transparent surfaces [Surface Cache GI preview; docs] | Already judged in LightingBakeResearch: watch for 6.7 LTS. It doesn't track terrain trees (source read earlier), so the ground light field stays relevant |
| **APV** | In 6.6 URP; sky occlusion adds bake time and memory [Unity APV docs] | For the arenas only, as planned |
| **GPU occlusion** | Can cost more where little is occluded [Unity docs] | A top-down camera over open ground is that case: measure on and off in the Valley, as the world review says |
| Virtual texturing | HDRP-only and experimental in Unity (**not re-checked for 6.6**) | Not available in URP. Texture arrays (§4.4) are the URP answer |
| Lightmaps, Enlighten, HDRP switch, hardware RT | Covered in LightingBakeResearch | No |

### 3.6 Smaller rendering items

- **64-bit HDR buffer** (`m_HDRColorBufferPrecision: 0` is 32-bit R11G11B10). If banding shows in dark fog gradients at night,
  try 64-bit on Ultra. Film grain already dithers. Cost: bandwidth and memory. Low priority, only if seen.
- **Legacy Post Processing v2** (`com.unity.postprocessing 3.5.4`) is in the manifest but does nothing in URP. Removing it (after
  checking nothing references it) trims compile time and confusion. Housekeeping, not look.
- **Eye adaptation:** URP has none. A top-down game rarely needs it. Skip.

---

## 4. Terrain realism

### 4.1 Break the SmoothStep: a shape pass in the raid height field (answers "banks and cliff edges look unnaturally smooth")

- **Have:** `RaidLayoutField.*` builds heights from smooth profiles: bank `Lerp(level - waterline, ground, SmoothStep(edge/bank))`
  (Rivers.cs:125); ponds the same (Ground.cs:128-132); ridges and rims `SmoothStep` (Ground.cs:152-155). There is a slope-based
  `Rough` term (Ground.cs:235) and stepped strata bands for **ravines** (Ravines.cs:216-221). Resolution (0.39 m) is not the problem.
- **Do** (each a new field defaulting to today's behaviour, its own random stream, so the Woods' golden layouts stay pinned):
  1. **Banks by river shape.** Use the river centreline's curvature (the planner has it). On outer bends make a *cut bank*: a 0.3-0.8 m
     near-vertical step at the waterline, then the meadow. On inner bends make a *point bar*: a wide, gentle gravel slope. Straight
     reaches get something in between. Domain-warp the bank distance by ±0.5-1.5 m noise so the edge line wanders instead of
     following the centreline at a fixed offset. This matches real river form and gives the gravel layer (B8) and bank stones (B6)
     a place.
  2. **Rim lips.** Replace the rim's SmoothStep with a convex break (flat top, sharp edge, steep face). Add ridged noise along the
     rim at 1-4 m so the lip line is ragged, plus an occasional notch where a gully starts.
  3. **Gullies on walls: the erosion filter.** Rune Skovbo Johansen's "Fast and Gorgeous Erosion Filter" (March 2026) is noise, not
     simulation. It makes branching gullies and ridges aligned with the slope, each point computed on its own, so it is
     parallel-friendly and fits the per-cell field [runevision; 80.lv]. Code is MPL-2.0 (Shadertoy). Apply it only on faces past
     the walkable slope and away from trails and clearings, as `Rough` is masked today. Its limit: gullies don't join into real
     drainage networks [bevy port docs]. That's fine at this scale.
  4. **Talus at cliff feet.** A few iterations of thermal erosion restricted to a band below steep faces. Talus angles are about
     30-40° [World Machine; Unity Terrain Tools]. It gives scree aprons and tells the paint where to put the Scree layer and Scree
     rocks. Keep cliffs intact with a high threshold above and a low one below. A known trick: plain thermal erosion flattens cliffs.
  5. **Valley walls get the ravines' stepped strata** (Ravines.cs already steps heights in bands). Reuse it on the Valley's moods,
     so the beds the shader paints have real ledges under them.
- **Gain:** high. It is exactly what the user named.
- **Cost:** M (1-3, 5), M (4). CPU at load: a 513² field with a few noise octaves plus a few thermal iterations is milliseconds to
  low tens of milliseconds with jobs (**estimate, measure in `RaidMap` load timing**). GPU: none.
- **Risk:** medium. Reachability (`ExitsReachable`), navmesh, fords and bridges, river levels (`RaidValleyTests`), swim depth. Every
  step behind a rules toggle; checked on many seeds with the Raid Generator Report.
- **Top-down fit:** high. From above, the lip line and the bank line are the silhouette.

### 4.2 Geometric beds and lips in the tessellation

- **Have:** `WorldStrata` changes colour only: shade per bed, a darker recess, a lit lip. The terrain tessellates and displaces
  near the player (15-25 m focus).
- **Do:** in `WorldTerrainDisplacement.hlsl` (hand-written, not generated), on faces past Beds From, push the surface along the
  horizontal part of the normal by a stepped function of world height, using the same beds, warp and seam as the colour. Each
  bed's top edge juts 0.2-0.5 m and the recess under it cuts in. The colour already lines up, so the geometry and paint agree.
  Optionally add a little ridged noise to the push, so faces aren't planar between beds.
- **Gain:** high where the player is: real ledges catching low sun, broken silhouettes, shadows inside the recesses (and contact
  shadows, 3.2, make them crisp).
- **Cost:** S-M; a few ALU in the domain shader, no textures. Beyond the focus distance the colour strata carry the read.
- **Risk:** low-medium. Physics ground differs from drawn ground by up to 0.5 m on faces nobody walks on. Watch for cracks at the
  focus fade. Shadows use the same displacement as long as the shadow pass tessellates too (it does today).
- **Top-down fit:** high.

### 4.3 Rocks, logs and props sink into the ground

- **Have:** `MeshSurface` (tessellated rock shader) has no knowledge of the terrain under it. `GrassTerrainColour` already sets a
  **global** ground colour map (`Shader.SetGlobalTexture`), which the grass reads.
- **Do:** in `MeshSurface`, sample that colour map at the pixel's world XZ and blend the bottom 0.2-0.4 m of a rock towards the
  ground colour, with a slightly darker contact band. Get the height above ground from the object's lowest point, or from a terrain
  height texture if one is bound globally. Optionally tint up-facing faces with dust or moss by world normal. The commercial
  terrain packs do the same: the object reads the terrain's splat or colour [Unity forum; GTS].
- **Gain:** high. It removes the "placed prop" seam on hundreds of rocks per map.
- **Cost:** S-M; 1-2 texture taps per rock pixel (MeshSurface is not at the terrain's texture limit).
- **Risk:** low. Do it alongside the planned DOTS-instancing step, so the shader is touched once.
- **Top-down fit:** high.

### 4.4 Terrain texture arrays: the enabler

- **Have:** one pass binds each painted layer's own textures. That is 63 of 64 textures and 14 of 16 samplers, so every new idea
  (another layer, a wet mask, the ground light field) has needed a slot freed first.
- **Do:** let the generator bind layers as three `Texture2DArray`s (albedo, normal, mask), built in the Editor per map palette
  (all project terrain layers are 2K). An array is one texture and one sampler however many slices it has, which is how
  MicroSplat and most custom terrain shaders pass the 4-layer and sampler limits [Unity forum answers; Shader Graph terrain docs].
- **Gain:** about 40 texture slots back. That allows B8's extra layers, a macro albedo or roughness map, a wet-bank mask (4.6),
  hex-tiling (4.5), and more than 14 layers in the Haven. **No direct visual gain on its own.**
- **Cost:** L (generator, `TerrainSurface`, the layer watcher and editor tools, warm-up re-record). GPU: similar or slightly lower.
- **Risk:** medium-high. The terrain is everything. The existing image comparison ("mean difference under 1 per channel") is the
  check. Compile time also matters: the terrain is already 17.7 CPU-hours of variants (TODO).
- **Top-down fit:** neutral; it is plumbing.

### 4.5 Tiling: hex-tiling for the moss skin and large layers

- **Have:** two samples per layer (as is, turned 63°) chosen by slow noise; macro variation.
- **Lacking:** the moss skin repeats visibly (`drips_game`). `TerrainMoss` is its own shader, so it is the first place to try.
- **Do:** Mikkelsen's hex-tiling (JCGT 2022): three samples with per-hex random offsets and a contrast-preserving blend, no lookup
  tables, with a public demo [Mikkelsen 2022; hextile-demo]. Best on normal maps, acceptable on colour.
- **Gain:** medium. **Cost:** S-M; +1 sample per layer where it has weight. The terrain is the frame's main cost, so measure in
  the Woods. **Risk:** low.

### 4.6 A wet band at every shore

- **Have:** puddles and weather wetness. Paint gradients at shores (commit 437addcb). `FenDamp` and B11's wetness floor for the
  marsh.
- **Lacking:** real banks are darker and glossier for a hand's breadth above the water. Ours go from dry meadow straight to water.
- **Do:** the ground light field's slice 1 uses only `.r` (the canopy shade; `GroundLight.hlsl` comment). **Its g, b and a look
  unused (verify).** Store the local water level there, which the generator knows, and in the terrain and MeshSurface darken
  albedo 25-40% and raise smoothness from 0 to about 0.4 m above the water, fading out by 0.8 m. The terrain already reads slice 1,
  so it costs no new texture or sampler.
- **Gain:** medium-high for "the shoreline looks wrong", the user's own complaint of 11-17. **Cost:** S-M. **Risk:** low. Raids
  only; the Haven needs its own path (after 4.4).

### 4.7 Dark plant cards (data check)

- In `strata_..._game`, `g3_game` and `g7_edge`, star-shaped and flat-leaf plants read near-black even in sun. Likely causes: normals
  facing away from the light (the Grass shader's Normal Up), a very dark albedo, or full canopy and SSAO darkening on cards lying
  flat. **Not verified.** Find the rows (likely leaf litter, needle debris, ferns or bushes) with the Rendering Debugger, then fix the
  data: Normal Up higher, translucency, albedo. S, no cost.

### 4.8 Grass at scale

- The current detail grass with clumps, terrain tint and wind waves holds up from above. Ghost of Tsushima's procedural grass
  [GDC 2021] is the reference if detail density ever becomes the bottleneck. It isn't now (plants are capped at the Woods' count).
  **Not recommended now.**

---

## 5. Water

**One physical fact that shapes this section.** Water's base reflectance is about 2%. From the explore camera's 50° pitch, the view
meets the water about 40° from its normal at screen centre (Schlick Fresnel about 2%) and about 60° at the top edge (about 6%).
From the top-down view, water should look like **its bed, its depth colour and its foam**, not like the sky. Reflections only
matter in the V view and cinematics.

| # | Lacking | Do | Gain | Cost | Risk |
|---|---|---|---|---|---|
| W1 | The Valley river is grey and uniform (`strata_..._game`, `g7_top`); A11 *raised* Reflection to 0.8 | Reflection to about 0.25-0.35 in `Water River (Valley)`, clarity up, Shallow colour warmer, so stones and gravel show through. The target art's "shallow and clear" stream | High | S, data | Low |
| W2 | Foam lines both banks evenly | Shore foam near 0 in flowing water; foam from speed, stones (exists) and convergence. Stream lines: advect a foam or streak colour map with the existing flow phases. Vlachos's Portal 2 flow maps also drift debris colour [Vlachos 2010] | High | S | Low |
| W3 | One speed everywhere | Write flow speed into UV2 from local width and slope (narrow or steep: faster, whiter; pools: slower, darker) and from curvature (outer bend faster). The shader already whitens fast water | Medium-high | S-M | Low |
| W4 | Marsh pools have a bright milky halo | A16 already cut Shore Foam to 0.03. If the halo persists, the cause is the depth-fade or refraction edge picking up bright grass. Check with foam at 0 | Medium | S | Low |
| W5 | Nothing floats | Duckweed and scum patches drifting slowly in the fen, a few leaves on the Woods' streams, foam clumps in eddies, all on the flow colour map. Rising bubbles in the marsh pools (also an ambient-life cue) | Medium | S-M | Low |
| W6 | No falls in the Valley | B12 (planned, last, riskiest) | High | L | Medium-high |
| W7 | Bank wetness | §4.6 | – | – | – |

Not needed: an ocean package (Crest and similar), URP SSR for water (it has its own), planar reflections (wasted at 2-6%).

---

## 6. VFX

The combat VFX set is complete (sparks per outcome, blood, trail, flash, impacts, materials). The gaps are in readability of
warnings and in **life and weather between fights**.

| # | Lacking | Do | Gain | Cost | Risk | Top-down |
|---|---|---|---|---|---|---|
| V1 | Telegraphs are flat opaque fans (`drips_dusk_green`); whether they hug slopes or clip into tessellated ground is **unverified**; colour is the only cue | Draw them as decals or a depth-projected shader so they lie on the ground; a bright rim plus a soft inner gradient filling from the attacker; a 1-frame flash at the hit; a pattern per tier (solid, dashed, notched) as A3 asks. Colour must back up shape, never carry it alone [Game Accessibility Guidelines; Xbox XAG 103] | High (fairness) | S-M | Low | Essential |
| V2 | Ambient life is thin outside the Woods (fireflies per raid done; bird species per biome not yet, L5) | **Valley:** a big bird's shadow sliding over the ground now and then (a moving dark blob, cheap, and it gives scale from above), ravens circling the apex, pollen and seed fluff on the wind, dust lifted by gusts on scree. **Marsh:** midge columns over water, dragonflies, bubbles and frog plops in pools. **Woods:** dust motes inside the shafts, falling needles, moths at torches | High (a still frame feels alive) | S-M each | Low | High |
| V3 | Rain from above is mostly splashes (streaks barely read top-down) | Denser ground ripples and splashes near the player and a thin rain sheet near the camera; wet sheen already exists. VFX Graph's depth-buffer collision suits rain splashes and gained URP support in Unity 6 [VFX Graph docs; Unity 6 collision post] | Medium | M | Low-medium | Medium |
| V4 | Wind is visible in grass only | Gust-borne leaves and dust travelling with the grass waves, so wind reads as one event across the frame (Ghost of Tsushima uses wind as a guide [GDC wind talk]) | Medium | S-M | Low | High |
| V5 | Particle System (CPU) for everything; no VFX Graph package | Adopt VFX Graph **only** for high-count effects (rain, midges, motes) if CPU particle cost shows in the profiler. Keep Shuriken for combat (pooled, already tuned) | Perf headroom | M | Medium (a second system) | – |
| V6 | Impact accents on big moments | On a boss kill or a heavy finisher, a longer hit stop on the final blow ("boss stop" [Sakurai]) and a brief radial blur. Keep it under the existing Hit Stop and Screen Flash options | Medium | S | Low | High |

---

## 7. SFX and audio

**Verified:** the three raid scenes' `SceneAudio` ambience is one track, `SeedMesh/.../Forest_Background_wind_1.wav` at 0.55. No raid
scene holds an `AmbientOneShots` or `PlacedAmbience` (component count 0; `MistyWoodsForestTests` asserts "no flies buzzing past"
on purpose). No `AudioMixer`, `AudioReverbZone` or `AudioLowPassFilter` appears in any script. Positional rolloff is linear
(`AudioManager.cs:359`). The final-stretch clip is shared too (GameDesignResearch §4).

| # | Do | Gain | Cost | Risk |
|---|---|---|---|---|
| A1 | **A sound bed per biome, in three layers:** a loop (bed), two or three `AmbientOneShots` sets, and `PlacedAmbience` sources that come from somewhere. **Woods:** a low wind in the spruce canopy, creaks, drips, a distant owl at night. **Valley:** open gusty wind whistling at rims, distant river roar (the stream loops exist), ravens, an eagle's cry, scree trickles under cliffs. **Marsh:** insect hum by day, a frog chorus at dusk and night, a bittern's boom, plops and bubbles, reed rustle. Dawn and night variants by the existing hour curves. Wind, water, insects and frogs synthesise well in `Tools/Audio`; birds are hard to synthesise, so use CC0 field recordings | **Highest per effort**: biomes feel different with eyes closed | S-M per biome; CPU ~1-3 voices | Low |
| A2 | **One AudioMixer** with groups (Music, Ambience, World, Voice, UI) and **snapshots** blended by place: Open, Forest (short early reflections), Gorge (slap echo: Brukam's Bridge), Inside (Haven buildings), Underwater (swimming: low-pass). Driven by Camera Zones, `RaidMapZone` and `PlayerSwimming`. The volume sliders can stay in code or move to exposed parameters | High | M | Low-medium (routing every source) |
| A3 | **Occlusion lite:** for positional sounds beyond about 6 m, one raycast (a budget per frame, staggered) against terrain and buildings sets a low-pass cutoff (22 kHz down to about 2.5 kHz) and -3 to -6 dB. A troll behind the ridge sounds behind the ridge, which also helps stealth | Medium-high | S-M; CPU ~0.05-0.2 ms (**estimate**) | Low |
| A4 | **Air absorption:** a low-pass that closes with distance on positional sounds, so far is dull and near is bright: depth without louder mixing | Medium | S | Low |
| A5 | **Music that follows the raid:** GameDesignResearch T1 (stage layers, stingers, a held-beacon drum, deliberate silence after The Hunt). Vertical layering of synchronised stems is the standard method, often mixed with horizontal sections [Phillips; Sackboy GDC 2021]. Per-raid music is the user's own later plan | High | S-M | Low |
| A6 | **Combat sound audit:** each combat event has at least three variations, ±5% pitch, and a layered hit (transient, body, tail) whose weight matches the hit stop | Medium | S | Low |
| – | Steam Audio (free; occlusion, reflections, baked reverb, HRTF; 2.0 is in beta [Valve docs; releases]) | Overkill for a top-down game today; revisit if A2 and A3 fall short | – | – |

---

## 8. UX and game feel

| # | Lacking | Do | Gain | Cost | Risk |
|---|---|---|---|---|---|
| U1 | The camera pulls in when a fight starts (G2, waiting on the user) | Keep the explore distance in fights, then frame engaged foes (already specified in GameDesignResearch G2) | High | S then M | Medium (feel) |
| U2 | Height reads weakly from above: FOV 40° at 14-28 m is close to telephoto and flattens relief | **Try** FOV 46-50° at a proportionally shorter distance (same ground coverage), so rims, shelves and banks show parallax as the camera moves. A data experiment in `PlayerCameraSettings`; the user's eye decides. Watch edge distortion and canopy near the lens | Medium-high for terrain read | S | Medium (feel, occlusion) |
| U3 | Telegraphs | V1 | – | – | – |
| U4 | No check of value contrast | A grayscale pass over the B13 captures: player, foes and interactables must separate from the ground in luminance, not only in hue. Diablo IV's team says they add or remove detail for readability of the play space [Blizzard 2022 update] | Medium | S (process) | Low |
| U5 | Accessibility gaps | Colour-free telegraph patterns (A3, queued); a high-contrast option for enemy outlines and telegraphs; a **visual-only** fog and mist density slider (the drawn fog only; `AtmosphereConditions` keeps the authored density so monster sight is unchanged). Contrast targets 4.5:1 for text, 3:1 for large [Unity accessibility guide] | Medium | S-M | Low |
| U6 | Hit stop is one curve by weight | Already scaled by `AttackDefinition.Impact`. Add V6's "boss stop" only | Low-medium | S | Low |

---

## 9. Lighting and atmosphere per biome

| # | Lacking | Do | Gain | Cost | Risk |
|---|---|---|---|---|---|
| L1 | The Valley's most common hour is 11:00 (60% of runs, short shadows); the user hasn't decided (AgentDecisions 11-18) | Make about 15:00-16:00 (or 9:00) the main hour. Long low light is what makes the new beds, shelves and lips read. In the shots, shadows fall towards the lower left (sun ahead-right of the camera): a good cross-back light, so keep the azimuth | High for rock | S, data | Low |
| L2 | The sky: saturated cyan with pink clouds in daytime (`strata_..._eye`, `bb_gorge`, `rim_below`); seen in the V view, cinematics and every reflection | Short term: cloud tint by sun elevation (white by day, warm only near the horizon at dusk). Longer term: a physically based sky (Hillaire 2020, used in Unreal, scales to mobile [EGSR 2020]), whose ambient feeds `DayNightCycle`'s ambient probe | Medium (low top-down) | S (data), M (sky) | Low |
| L3 | Canopy dapple | L4 of the world review (a canopy cookie as Texture2D) | Medium | S-M | Low |
| L4 | Bright white mist and fog blobs (the mist wall in `drips_game`, the Brukam curtain) | Softer curtain sides; mist albedo a step darker under canopy shade (the ground light field already reaches the mist through `GroundLightAir`). V Rising also uses volumetric fog as line-of-sight and blends per-area, per-hour volumes [Unity V Rising case study], which the project already does | Low-medium | S | Low |
| L5 | Night: compounds don't stand out by light yet | L10 of the world review (warm `NightFlame` at compounds, cold moon elsewhere) | Medium | S | Low |

---

## 10. Comparable games: what transfers

| Game | What they do (sourced) | Transfers to us |
|---|---|---|
| **Diablo IV** (custom engine, isometric) | Adds or removes detail for camera readability; physically based lighting; rain fills ruts and hoof prints; GDC 2024 talk on level art for the isometric camera [Blizzard 2022; GDC 2024] | Detail where the camera looks (the hero/mid/micro pass); value checks (U4); wet ruts (we have puddles) |
| **V Rising** (Unity HDRP + DOTS, top-down) | Volumetric fog as atmosphere and line-of-sight; volume profiles per area and time of day, blended; custom passes, one darkening objects near the camera [Unity case study] | We match it already; "darken near the camera" is an option for canopy right under the lens, beside SeeThrough |
| **Valheim** (Unity) | Low-res textures, but lighting, fog and weather carry the look (press and players) [ScreenRant; Retroware]; the developer quote I found could not be attributed with certainty | Spend on light, air and weather (L1, A1, V2) before more texture detail |
| **Ghost of Tsushima** | Procedural grass, wind as guidance [GDC 2021 ×2]; screen-space shadows [WinterCrownWorks] | V4, 3.2 |
| **Gord** (Unreal) | A very dark visual and tonal identity [Wikipedia, previews] | Already the reference for the fog of war (Atmosphere.md) |
| **Enshrouded** (own voxel engine) | Lighting is its strength, per reviews; the GI method isn't documented [PCGH; GamerGuides] | Not transferable (voxels) |
| **Tiny Glade, Sons of the Forest, Last Epoch, Grounded, Manor Lords, Dune Awakening** | I could not verify rendering specifics in this pass | – |

---

## 11. Ranked: the best improvements, most visible gain per effort first

| # | Improvement | Section | Effort | Risk |
|---|---|---|---|---|
| 1 | Per-biome ambience beds, one-shots and placed sources (the raids share one loop today) | A1 | S-M per biome | Low |
| 2 | Re-fit shadow cascades to the top-down camera (per camera style) | 3.1 | S | Low |
| 3 | Valley water: reflection down, clarity up, shore foam off, foam where physics puts it | W1, W2 | S | Low |
| 4 | Rocks and props blend into the ground (MeshSurface reads the global ground colour map) | 4.3 | S-M | Low |
| 5 | Height-field shape pass: cut banks and point bars, wandering bank lines, sharp ragged rim lips, the ravines' strata on Valley walls | 4.1 (1, 2, 5) | M | Medium |
| 6 | Geometric beds and lips in the tessellated displacement | 4.2 | S-M | Low-medium |
| 7 | Valley main hour to mid-afternoon (the user's call) | L1 | S | Low |
| 8 | Wet band at every shore via the ground light field's spare channels | 4.6 | S-M | Low |
| 9 | Telegraphs on the ground with rim, gradient and colour-free patterns | V1 | S-M | Low |
| 10 | Screen-space contact shadows (Bend), as an option tier | 3.2 | M | Medium |
| 11 | Ambient life per biome (bird shadow, midges, motes, pollen, gust debris) | V2, V4 | S-M each | Low |
| 12 | AudioMixer with place snapshots (gorge echo, forest, inside, underwater), occlusion lite, air absorption | A2-A4 | M | Low-medium |
| 13 | Erosion filter gullies and talus aprons on walls | 4.1 (3, 4) | M | Medium |
| 14 | Grass motion vectors, then measure DLAA or TAA as the Ultra default | 3.3 | S-M (M+ if grass must be drawn ourselves) | Low-medium |
| 15 | Terrain texture arrays (frees ~40 slots; unlocks more layers, hex-tiling, masks) | 4.4 | L | Medium-high |

Also worth doing when touched: geometric specular AA (3.4, S), hex-tiling the moss (4.5, S-M), the camera FOV experiment (U2,
S), a physically based sky (L2, M), music by danger stage (A5, already specified as T1).

**Budgets to hold** (from the project's rules): 4K default quality at the Woods' 13-18 ms worst case or better; each new GPU pass
measured at the four fixed spots with the same seed and hour (300 frames, noise about 2 ms); new costly things go behind an option
tier, never by lowering a default.

---

## 12. Quick wins (data and settings only)

1. **Shadow cascade splits** about 0.24 / 0.42 / 0.66 on `PC_RPAsset` for the top-down view; check the V view and cinematics.
   (Switching per camera style needs a few lines of code.)
2. **`Water River (Valley)`:** Reflection 0.8 → about 0.3, Clarity up, Shore Foam → about 0.02, Shallow colour a little warmer.
3. **Marsh halo:** set `Water Fen`'s foam to 0 for one launched look; if the ring stays, it is the refraction or depth-fade edge,
   not foam.
4. **Raid ambience:** give each raid a different bed from what exists (`Sounds/Library` Wind Loop and Stream Loop, the OGA water
   loop) and put `AmbientOneShots` with existing SoundDefinitions into the Valley and the Marsh scenes until A1's own sounds exist.
   (Scene edits, saved through the Editor.)
5. **Dark plants:** find the rows that draw near-black from above; raise Normal Up and translucency, or lighten albedo.
6. **Valley main hour** about 15:00 (the user's decision, already queued).
7. **Sky clouds:** less pink by day on the Sky material (warm tint only near the horizon at dusk).
8. **Mist curtain at Brukam's Bridge:** softer sides (edge fade) so it stops reading as a lozenge.
9. **Housekeeping:** remove the unused `com.unity.postprocessing` package after checking nothing references it.

---

## 13. Considered, and not recommended

- **HDRP, Entities or DOTS conversion, Enlighten, lightmaps for raids, hardware ray tracing:** settled in earlier research. The
  frame is the terrain and shadows, not scripts; the maps are built at load.
- **Back-porting GTAO, SSR or Surface Cache GI from 6.7:** wait for 6.7 LTS (end of 2026) and upgrade instead.
- **Planar reflections or an ocean package:** the top-down view sees 2-6% reflection.
- **Voxel or mesh terrain for the generated maps:** heightmap, displacement and ledge meshes cover it at a fraction of the cost.
- **Steam Audio's full simulation now:** too much for a top-down game until the mixer, occlusion lite and biome beds are in.
- **Procedural compute grass:** not until detail density is the measured bottleneck.

---

## Sources

Unity and URP
- Unity Manual, GPU occlusion culling in URP: https://docs.unity3d.com/Manual/urp/gpu-culling.html
- Unity Manual, STP upscaler: https://docs.unity3d.com/Manual/urp/stp/stp-upscaler.html
- Unity forum, Preview of URP GTAO (June 2026, 6.7): https://discussions.unity.com/t/preview-of-urp-ground-truth-ambient-occlusion-gtao/1724061
- Unity forum, Preview of Screen Space Reflections for URP (May 2026, 6.7): https://discussions.unity.com/t/preview-of-screen-space-reflections-for-urp/1721494
- Unity forum, Surface Cache GI preview: https://discussions.unity.com/t/surface-cache-gi-preview/1720494
- Unity docs, Surface Cache GI volume override (6.7): https://docs.unity.com/en-us/engine/6000.7/manual/lighting-overview/lighting/surface-cache-gi-volume-override-reference
- Unity 6.7 beta release notes (6000.7.0b7): https://unity.com/releases/editor/beta/6000.7.0b7
- CG Channel, Unity 6.5 release (6.7 LTS due end of year): https://www.cgchannel.com/2026/06/unity-releases-unity-6-5-discover-5-key-features-for-cg-artists/
- Unity Manual, APV sky occlusion (URP): https://docs.unity3d.com/Manual/urp/probevolumes-skyocclusion.html
- Unity Manual, render pipeline feature comparison: https://docs.unity3d.com/Manual/render-pipelines-feature-comparison.html
- Shader Graph, terrain layers sample (4 layers per pass in URP): https://docs.unity3d.com/Packages/com.unity.shadergraph@17.3/manual/Shader-Graph-Sample-Terrain-Layers.html
- Unity forum, texture samplers limit per pass: https://forum.unity.com/threads/texture-samplers-limit-per-pass.605395/
- Unity forum, blending objects with the ground: https://forum.unity.com/threads/blending-objects-with-ground.1050605/
- GTS terrain shader (mesh-terrain blending, commercial): https://assetstore.unity.com/packages/tools/terrain/gts-glyph-terrain-shader-225272
- VFX Graph, Collide with Depth Buffer: https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@14.0/manual/Block-CollideWithDepthBuffer.html
- Unity forum, Unity 6 collision improvements in VFX Graph: https://discussions.unity.com/t/unity-6-collision-improvements-in-vfx-graph/1557951
- Unity Terrain Tools, thermal erosion: https://docs.unity3d.com/Packages/com.unity.terrain-tools@5.1/manual/erosion-thermal.html
- Unity blog, accessibility basics: https://unity.com/blog/video-game-accessibility-basics
- Unity case study, V Rising: https://unity.com/case-study/v-rising

Techniques
- Bend Studio, Inside Bend screen space shadows: https://www.bendstudio.com/blog/inside-bend-screen-space-shadows/
- Unreal docs, contact shadows (Bend pass): https://dev.epicgames.com/documentation/unreal-engine/contact-shadows-in-unreal-engine
- WinterCrownWORKS, screen-space shadows write-up: https://winter-crown-works.com/en/tech/003
- Kaplanyan et al., specular AA (GDC 2016): https://developer.download.nvidia.com/gameworks/events/GDC2016/akaplanyan_specular_aa.pdf
- Tokuyoshi and Kaplanyan, Stable geometric specular AA (JCGT 2021): https://www.jcgt.org/published/0010/02/02/paper-lowres.pdf
- Rune Skovbo Johansen, Fast and Gorgeous Erosion Filter (March 2026): https://blog.runevision.com/2026/03/
- 80.lv on the erosion filter: https://80.lv/articles/fast-terrain-erosion-filter-that-emulates-erosion-without-simulation
- bevy_erosion_filter (port, limits): https://docs.rs/crate/bevy_erosion_filter/0.1.1
- World Machine, thermal erosion: https://help.world-machine.com/topic/device-thermalerosion/
- Mikkelsen, Practical Real-Time Hex-Tiling (JCGT 2022): https://jcgt.org/published/0011/03/05/paper.pdf ; demo: https://github.com/mmikk/hextile-demo
- Vlachos, Water Flow in Portal 2 (SIGGRAPH 2010): https://www.advances.realtimerendering.com/s2010/Vlachos-Waterflow(SIGGRAPH%202010%20Advanced%20RealTime%20Rendering%20Course).pdf
- Hillaire, A Scalable and Production Ready Sky and Atmosphere Rendering Technique (EGSR 2020): https://diglib.eg.org/handle/10.1111/cgf14050
- Wohllaib, Procedural Grass in Ghost of Tsushima (GDC 2021): https://gdcvault.com/play/1027214
- Blowing from the West: Simulating Wind in Ghost of Tsushima (GDC): https://gdcvault.com/play/1027350/Blowing-from-the-West-Simulating

Audio
- Steam Audio Unity guide: https://valvesoftware.github.io/steam-audio/doc/unity/guide.html ; releases: https://github.com/ValveSoftware/steam-audio/releases/tag/v2.0-beta.18
- Winifred Phillips, music game plan part four (vertical layering): https://www.gamedeveloper.com/audio/music-game-plan-tactics-for-the-video-game-composer-part-four-
- Hybrid horizontal-vertical structure, Sackboy (GDC 2021): https://www.gamedeveloper.com/game-platforms/hybrid-horizontal-vertical-structure-for-game-music-composers-from-spyder-to-sackboy-gdc-2021-

Feel, UX and accessibility
- Sakurai on hitstop (Famitsu column, fan translation): https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/
- Sakurai's hit stop techniques video (coverage): https://www.gonintendo.com/contents/13581-sakurai-s-latest-game-dev-video-features-8-hit-stop-techniques
- Xbox Accessibility Guideline 103: https://devdocs.xbox.com/gaming/accessibility/xbox-accessibility-guidelines/103
- Game Accessibility Guidelines: https://gameaccessibilityguidelines.com/

Comparable games
- Blizzard, Diablo IV quarterly update (March 2022): https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022
- GDC 2024, Diablo IV: The Art of Open World Sanctuary: https://schedule.gdconf.com/session/diablo-iv-the-art-of-open-world-sanctuary/899417
- ScreenRant on Valheim's lighting: https://screenrant.com/valheim-good-graphics-lighting-low-resolution-textures/
- Retroware, What makes Valheim so great: https://articles.retroware.com/2021/02/11/what-makes-valheim-so-great/
- PCGH, Enshrouded 1.0 tech test: https://www.pcgameshardware.de/Enshrouded-Spiel-74267/Specials/1-0-Technik-Test-Benchmarks-Release-1556566/
- GamerGuides, Enshrouded review: https://gamerguides.com/enshrouded/review
- Gord (Wikipedia): https://wikipedia.com/wiki/Gord_(video_game)

**Not verified in this pass:** Bend's code licence; whether Unity's terrain detail renderer runs a grass MotionVectors pass; the
ground light field's slice-1 g/b/a being free; the dark plants' cause; whether telegraph meshes clip on slopes; all millisecond
estimates; virtual texturing's status in 6.6; Tiny Glade's, Sons of the Forest's, Last Epoch's and Grounded's rendering specifics.
