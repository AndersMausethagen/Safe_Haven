# World look plan: closing the gap to the concept art (2026-11-17)

The goal (the user): "make each scene and raid look more modern and better ... similar in quality as this, or as close as we can
get with procedural generation." Targets: `Docs/ConceptArt/TrollValley_target.png` and `Docs/ConceptArt/MossMarsh_target.png`.

Looked at: launched-raid shots (`marsh6-8`, `tv_look3-4`, `tv_bridge_sheet`, `tollmeet`, `lf_woodsday`, `lf_on`, `bb4`), the
generator code, the map and layout assets, raid definitions, day-night profiles, raid volumes and the scenes' mist and moss. The
shots and the probes named below (`marsh_thin.cs`, `arena_growth_probe.cs`, `bb_probe_run.cs`, `wlp_stats.py`) are in the session
scratchpad, `C:\Users\ander\AppData\Local\Temp\claude\c228cfdc5752c1eb\d8a62127-68f4-49b0-88cb-7d772495d37f\scratchpad\`, which a
reboot can clear.

**One caveat first.** The capture probes render `Camera.main` into a RenderTexture. Per the 11-10 note, the atmosphere pass skips any
camera with a target texture, so the shots probably show **no AtmosphericFog and no TerrainMist**. Mist can't be judged from them.
Step 1 below fixes the capture before anything else is judged.

## Measured colour, targets against today

Mean RGB, luminance percentiles, mean saturation, a fine-detail measure (mean difference from a 9 px blur) and median hue of the
saturated pixels, on the centre 90% of each image (`wlp_stats.py`; B13 makes it permanent):

| Image | Mean RGB | Lum p5/p50/p95 | Sat | Detail | Hue |
|---|---|---|---|---|---|
| Troll Valley target | 0.32 0.32 0.24 | 0.13/0.30/0.55 | 0.27 | 14.6 | 55° |
| tv_look4 start | 0.24 0.28 0.25 | 0.13/0.24/0.47 | 0.24 | 9.3 | 133° |
| tollmeet | 0.37 0.38 0.35 | 0.18/0.34/0.63 | 0.18 | 13.8 | 127° |
| Moss Marsh target | 0.22 0.27 0.17 | 0.13/0.24/0.40 | 0.37 | 7.5 | 86° |
| marsh8 start | 0.19 0.24 0.20 | 0.07/0.22/0.36 | 0.29 | 8.5 | 140° |
| lf_woodsday (Woods) | 0.12 0.18 0.20 | 0.06/0.17/0.27 | 0.46 | 6.9 | 195° |
| bb4 gate (arena) | 0.41 0.36 0.37 | 0.21/0.33/0.73 | 0.20 | 6.2 | 27° |

What it says: the Valley is a blue-green 130° where the target is a warm yellow-olive 55°; blue sits level with red where the
target's is a quarter lower; it is either a quarter darker in the mid-tones (tv_look4) or brighter but washed out (tollmeet,
saturation 0.18); and it has two thirds of the target's fine detail. The Marsh is too blue and too grey: the target is yellow-green
86° and a quarter more saturated. The Marsh target's detail is *lower* than ours: its fog softens everything, so the Marsh needs
atmosphere more than plants. The arena is a lavender, low-detail plane. The Valley and the Marsh both sit at 130-140° today, so
they read as one biome ("reads as Misty Woods, but with whiter trees").

---

## 1. The gap

### Troll Valley (ranked by how much each closes the gap)

1. **The ground is the Woods' moss.** `RaidMap.ApplyGroundLook` sets `TerrainMoss.MossLayer` to the map's Forest Layer, in the
   Valley *Meadow Grass*, so every meadow wears the Woods' dark bubbly moss skin (tv_look4, tollmeet). The plant rows are the Woods'
   too: Glow Mushrooms and Glow Flowers (the blue dots in every Valley shot), Needle Debris, Dry Leaves, Leaf Litter; no heather,
   berries, flowers or cotton grass. This is most of "reads as Misty Woods with whiter trees".
2. **No rock on the floor.** The target has eight or more outcrops and boulder piles in one view. `Scatter` places rocks only on
   16-38° slopes or where forest > 0.3, so the open floor gets only the planner's great boulders (6-10 clusters of 1-3) and site
   clutter: single stones that read as props, not geology. Nothing dresses the valley walls (`RaidRavineCrags` serves ravines only),
   so from above they are smooth painted slopes.
3. **The river is a canal.** One width, smooth steep banks in the dark cliff paint, a white foam line along both banks
   (`Water River (World)` › Shore Foam 0.3), no stones, rapids or falls (tv_bridge_sheet). The target's stream is shallow and clear,
   foam only round stones that break the surface, grass to the edge, two small falls.
4. **Light is flat and cold.** All four day-night profiles share one sun (yaw 320°, noon 55°, shadow fill 1.35, darkness 0.8).
   The target has a warm low sun whose long soft shadows model every rock.
5. **Trees are spread evenly** on a jittered grid, groves only by a 22 m noise per kind. The target has copses of 3-8 mixed spruce
   and birch with meadow between, and lone trees. Cedars stand in for spruce.
6. **No small detail where ground types meet.** The target puts pebbles, ferns, shrubs or a log at every seam.
7. **Gravel blotches.** `Paint` gives open ground 0.35 soil (`(1 - forest) * 0.35`), so the open valley is half gravel in noisy
   islands (tollmeet). The 14:54 commit softened path edges; open ground still gets the soil.

### Moss Marsh

1. **It is a birch wood.** The tree, rock and undergrowth lists are copies of the Valley's (birch, cedars, cliff boulders, troll
   rocks, cedar saplings); the top view (marsh8) is about half canopy. The target has two dead gnarled trees in view and open fen
   elsewhere. Dead Cedar, the only dead tree, has weight 0.3 and starts at tier 2.
2. **No green fog.** `MossMarsh_Raid_Volume`'s AtmosphericFog is a copy of the Valley's (density 0.008, neutral albedo). The scene's
   TerrainMist is the thinnest of the three (density 0.1, thickness 1.2, base amount 0.5). Green fog is the target's whole mood.
3. **Pools read as lakes.** `Water Fen` › Shore Foam 0.25 draws a white surf line round every black pool. No lily pads, no reeds
   standing in the water.
4. **Reeds as a carpet, not clumps by the pools.** The fen's wetness never drops below 0.25 (`RaidMap.FenDamp`), so every wet-loving
   row grows thicker everywhere, and ten such rows stack. marsh8 thinned them, but they still spread evenly instead of crowding banks.
5. **Colour.** Blue above red; the target is yellow-green with black water and a green haze.
6. **Missing set dressing.** Ruins, mossy boulder piles, a broken boardwalk, dead trees with hanging moss. A `Boardwalk.prefab` and
   a modular pier exist; nothing places them.
7. **Muddy paths look dry.** Nothing keeps `Muddy Bank` wet when it isn't raining.

### Misty Woods

The user said keep it, and it is the most finished of the three: dense, dark, cold, its own. Small gaps: Glow Mushrooms run in
bead-like strings along wet strips (lf_woodsday); the same lack of rock piles and edge detail as the others; trunks between camera
and player fill the frame at sites (lf_on). Nothing here should change its identity.

### Brukam's Bridge arena

The biggest gap per square metre. 1) Bare ground: no grass, plants, trees or stones on either bank (bb4 1, 2, 7). The arena runs
`Dress` with the Valley's map definition; `TerrainGrowth.Fill` returns 0 when the terrain lacks Short Grass, Tall Grass and Bush
prototypes, which is the likely cause (`arena_growth_probe.cs` answers it). 2) The gorge is two flat textured walls and
a flat black water plane, no depth or rock form. 3) A lavender cast (hue 27°) and flat light. 4) The mist curtain is an opaque white
lozenge with hard edges. The bridge itself and the troll are good.

---

## 2. Recommendations

Sizes: S under half a day, M one to two days, L more. Data changes go through the Editor (CLI eval with `SerializedObject`, as
`marsh_thin.cs` does), never YAML.

### (a) Data only, now

**Troll Valley**

| # | Change | Where | Size, risk |
|---|---|---|---|
| A1 | Moss skin off the meadow. The Meadow Grass texture is a yellow-green lawn; what the shots show is the SeedMesh moss skin on top. Try unticking `TerrainMoss` on the Valley terrain, but `RaidMap` calls `Rebuild()` on it directly (line 277), so it may come back: then B1 (a few lines) is the fix | `Raid_TrollValley.unity`, terrain | S, low |
| A2 | Plants: **off** Glow Mushrooms, Glow Flowers, Needle Debris B and C, Leaf Litter. Needle Debris A rarity 0.75. Dry Leaves rarity 0.7. **Add** (detail prototypes from `Art/Vegetation/Safe Haven/Details` added to the Valley's TerrainData first): Heather patch 6, rarity 0.66, thickness 0.6; Blueberry 7 / 0.70 / 0.5, clearing 1; Lingonberry 6 / 0.72 / 0.5; Yellow Flowers 4 / 0.74 / 0.3; White Flowers 4 / 0.72 / 0.3; Cotton Grass A/B 4 / 0.80 / 0.4, love of wet 0.9 | `TrollValley Map` › Plants; `TrollValley Map Terrain` detail prototypes | S, low |
| A3 | Growth, Meadow Grass row: short 0.65 → 0.8, tall 0.38 → 0.45, colour (0.56, 0.60, 0.30) → (0.62, 0.60, 0.32) | `TrollValley Map` › Growth | S, low |
| A4 | Trees: Birch A/B 3, Young Birch A/B 2.2 → 1.4, Cedar Slender 1.4 → 1.2, Cedar Spire 1 → 1.2 (the dark spires stand in for spruce), Cedar A 0.8 → 0.3, Cedar Tall A 0.5 → 0; groves: birches 0.3 → 0.6, spires 0.6 → 0.75. Tree Spacing 3.2 → 3.6. Moods' Forest: Birch Floor 0.95 → 0.7, River Mist 0.8 → 0.6, Troll's Road 0.6 → 0.5 | `TrollValley Map` › Trees; `TrollValley Layout` › Moods | S, low |
| A5 | Undergrowth: Cedar Sapling 2 → 0.6, Cedar Seedling 2 → 0.6, Pebbles A/B 0.6 → 1.2, Scree 0.25 → 0.8, Small Stone 0.5 → 1.0, Mossy Stones 0.4 → 0.8 | `TrollValley Map` › Undergrowth | S, low |
| A6 | Boulders: clusters 6-10 → 10-16, cluster size 1-3 → 3-6, scale 0.6-1.3 → 0.5-1.4 | `TrollValley Layout` | S, low (planner rerolls boulders; reachability is checked) |
| A7 | Sun: noon elevation 55 → 45 (longer shadows), intensity 2 → 2.4, shadow fill 1.35 → 1.15, shadow darkness 0.8 → 0.9 | `Troll Valley Day Night` | S, low |
| A8 | Grade: exposure -0.3 → -0.15, saturation 1.05 → 1.1, filter (1, 0.98, 0.92) → (1.03, 1.0, 0.86). Ground light bounce 1 → 1.2 | `TrollValley.asset` › Look | S, low |
| A9 | Fog (raid volume): density 0.008 → 0.006, albedo → (0.95, 0.93, 0.85), ambient → (0.32, 0.31, 0.27) | `TrollValley_Raid_Volume` › Atmospheric Fog | S, low |
| A10 | Mist in the hollows only: TerrainMist density 0.18 → 0.12, thickness 2.4 → 1.5, tint → (0.90, 0.92, 0.90), coverage 0.78 → 0.5, lowland boost 1 → 2.5, water boost 1.2 → 2, base amount 0 → 0.25 | `Raid_TrollValley.unity` › TerrainMist | S, low |
| A11 | Own river material: copy `Water River (World)` (the Haven's rivers share it) to `Water River (Valley)`: Clarity 1.05 → 1.8, Shore Foam 0.3 → 0.12, Deep (0.03, 0.09, 0.10), Shallow (0.20, 0.32, 0.28), Reflection 0.68 → 0.8 | `Art/Generated/Water`; `TrollValley Layout` › River Material | S, low |

**Moss Marsh**

| # | Change | Where | Size, risk |
|---|---|---|---|
| A12 | Trees: Birch A/B 3 → 0.5, Young Birch A/B 2.2 → 0.8 at scale 0.5-0.8 (stunted), Cedar A/Tall A/Young Cedar → 0, Cedar Slender 1.4 → 0.4, Cedar Spire → 0.3, **Dead Cedar 0.3 → 3, From Tier 2 → 0**. Tree Spacing 3.2 → 4.5. Moods' Forest: Strings and Flarks 0.35 → 0.15, Hummock Fen 0.3 → 0.12, Drowned Wood 0.9 → 0.45, Peat Moor 0.2 → 0.08, Pine Holms 0.5 → 0.3. Aim: under 20% canopy from above | `MossMarsh Map`; `MossMarsh Layout` | S, low |
| A13 | Rocks: Mossy Boulder A/B 0.75 → 2, Mossy Rock Bed 0.5 → 1.5, Mossy Rock and Small → 1; Grey Rock, Grey Boulder, Ground Slab, Cliff Slab → 0.2; both troll rocks → 0 (they are the Valley's). Note: rocks only scatter where forest > 0.3, so A12 thins rocks too until B4 | `MossMarsh Map` › Rocks | S, low |
| A14 | Green ground fog: density 0.008 → 0.025, base height 0.5 → 0.3, height falloff 0.9 → 1.4 (stays knee to waist high), albedo → (0.62, 0.74, 0.52), ambient → (0.20, 0.27, 0.18), noise size 16 → 10, bank strength 0.3 → 0.5. Keep View Clearing on so the player shows | `MossMarsh_Raid_Volume` (and a darker green in `MossMarsh_Raid_Night`) | S, medium (readability of creatures: check) |
| A15 | TerrainMist: density 0.1 → 0.16, thickness 1.2 → 1.6, coverage 0.85 → 0.7, base amount 0.5 → 0.9, lowland boost 1 → 2, water boost 1.5 → 2.5 | `Raid_MossMarsh.unity` | S, low |
| A16 | Black water, no surf: `Water Fen` Shore Foam 0.25 → 0.03, Foam Colour → (0.42, 0.45, 0.30) (scum, not surf), Reflection 0.55 → 0.7, Clarity 0.12 → 0.25, Shallow → (0.08, 0.11, 0.04) | `Art/Generated/Water/Water Fen.mat` | S, low |
| A17 | Grade: saturation 1.08 → 1.2, contrast 1.15 → 1.1, exposure -0.15 → -0.2, filter (0.95, 1, 0.9) → (0.98, 1.0, 0.82). Sun: intensity 2 → 1.7, shadow darkness 0.8 → 0.6 (diffuse). Sky cover by day 0.4 → 0.5 | `MossMarsh.asset`; `Moss Marsh Day Night` | S, low |
| A18 | Growth, Forest Moss row: tall grass 0.6 → 0.4 (the broad blobs in marsh7). Forest Grass rarity 0.5 → 0.6, Ground Weed 0.5 → 0.62, Lake Grass 0.62 → 0.7 | `MossMarsh Map` | S, low |

**Misty Woods:** Glow Mushrooms patch 5 → 2.5 m, thickness 0.22 → 0.3 (small clusters, not strings). Nothing else.

**Brukam's Bridge:** grade filter (1, 0.98, 0.92) → (1.02, 1.0, 0.88) and the arena volume's fog albedo to warm grey, to lose the
lavender; add Short Grass, Tall Grass, Bush and the Valley's plant prototypes to `BrukamsBridge Terrain` so Dress has something to
grow (S, low).

### (b) Small code changes to the generator

Every new field defaults to today's behaviour, so the Misty Woods and `RaidLayoutGoldenTests` stay as they are. New random draws
come from their own streams (`RaidLayout.SeedFor(seed, "...")`) or from noise, never from the scatter stream, so a field at its
default changes nothing.

**B1. Moss where the map says** (S, low). `RaidMapDefinition`: `mossLayer` (empty: the Forest Layer, as today) and a `noMoss`
toggle. `RaidMap.ApplyGroundLook` uses it. Valley: the litter layer (Mossy Forest Floor), so moss lies only under the densest
trees. Replaces A1.

**B2. Open ground's soil as data** (S, low). `RaidMapSurface.Paint` line 54: `(1f - forest) * 0.35f` becomes
`(1f - forest) * definition.OpenSoil` (default 0.35). Valley 0.08: open ground is meadow, soil only on trails and trodden
clearings. Marsh 0.15.

**B3. Tree clumping** (S-M, low). `RaidMapDefinition`: `treeClumping` 0-1 (default 0) and `clumpSize` m (16). In the tree pass,
multiply the acceptance by a copse mask:
`copse = SmoothStep(0.38, 0.62, Perlin(x/clumpSize, y/clumpSize) * 0.75 + Perlin(x/(0.4·clumpSize), ...) * 0.25)`,
`p = forest^1.3 · Lerp(1, 0.12 + 1.6·copse, treeClumping)`.
The 0.12 floor keeps lone trees in the gaps. Apply the same mask at half strength to the undergrowth so shrubs ring the copses.
Valley 0.75 / 14 m, Marsh 0.6 / 9 m (dead trees in twos and threes), Woods 0. Mixed copses come for free: groves pick the kind,
the copse mask picks where.

**B4. Rock piles, rocks in the open, outcrops** (M, low-medium).
- `rocksInOpen` (default 0): a chance per rock-grid spot on open ground (forest < 0.3, off paths). Valley 0.08, Marsh 0.06 (on
  strings and hummocks).
- `pileChance` (default 0): when a rock with room ≥ 1 m lands, add 2-5 satellites from the kinds with room < 1 m (Mossy Rock Small,
  Grey Rock, Pebbles), 0.6-1.8 m from its centre, scale 0.3-0.6, plus one undergrowth plant (fern or shrub) at its foot. Satellites
  get no trunk prism, so the navmesh is unchanged. Valley 0.5, Marsh 0.4, Woods 0.
- Outcrop clusters for the great boulders, behind a rules toggle `boulderOutcrops` (default off, which keeps the golden fingerprints):
  satellites hug the main rock (its half width + 0.3-1.5 m) at scale 0.3-0.55 and sink 30% instead of 20%.
  `RaidLayoutPlanner.Ground.PlaceBoulders`.

**B5. Plants by water height, edges and rocks** (M, medium). The biggest step towards detail where ground types meet.
`TerrainGrowth.Plant` gains:
- `aboveWater` (metres of ground above the local water surface, a range; default unused), from one function in `RaidMap.Dress`:
  the fen's, a pond's or a river's level within reach. Starting bands: Reeds -0.5 to 0.35 (standing in the shallows), Horsetail
  -0.3 to 0.4, Sedge -0.1 to 0.6, Marsh Marigold 0 to 0.3, Cotton Grass 0.05 to 0.8. A band below zero passes the in-water block.
- `atEdges` 0-1: thickens where two paint layers meet (the verge term `4w(1-w)`, per plant). Pebbles, ferns, Ground Weed 0.6.
- `nearRocks` 0-1: thickens within 1.5 m of a rock or boulder (stamped into a coarse grid before `Fill`). Ferns 0.7, Moss Cushion
  0.8, Heather 0.5.
Make `FenDamp` (0.25) a rule (`RaidFenRule.damp`), Marsh 0.1, so wet-lovers crowd the banks instead of the whole fen. Dress logs
its time; measure the extra height sample per cell.

**B6. Stream stones and banks** (M, low-medium). A shore pass with its own list `shoreRocks` (Lake Stone A-C, Lake Stones A/B,
Mossy Rock Small, Pebbles A/B; all exist):
- Bed stones where the water is under 0.8 m deep, 10-30 cm proud of the surface. The water shader already rings stones that reach
  the surface with foam and trails it downstream (Docs/Water.md), so foam lands where the target has it. Valley: one per 3-5 m² of
  shallow bed near fords and the head, fewer in the deep middle.
- Bank stones 0-1.5 m from the edge, clumped by noise, every 1.5-3 m.
As instanced terrain trees like the other rocks; larger stones keep their capsule, so creatures path round them.

**B7. Valley wall crags** (M, low). Dress wall faces steeper than 38° and within 25 m of the floor with pieces from the Boulder
Kit, placed the way `RaidRavineCrags` sets them into a ravine (stood on end, mostly buried, colliders off except the odd big block),
about one per 3 m of wall foot. Wall rocks measured at no frame cost (11.1 ms either way); the Haven's trap was 2.5 objects a metre
over 20 km, so keep the 25 m reach. `RaidMapGenerator.Scenery`, after boulders.

**B8. Two more paint layers** (M, medium). Optional `shoreLayer` (gravel by running water, mud by still water; 0-0.6 m above
water, from the same function) and `variantLayer` (dry grass in the Valley, sphagnum in the Marsh, by broad noise and dryness).
Raids paint 4 of the 14 single-pass slots, so 6 stays single pass. Traps: a new layer draws black until the single-pass table
refreshes; displacement masks need Refresh Displacement.

**B9. Lily pads** (S-M, low). In `PlaceFenWater`/`PlacePonds`: one combined mesh per map of pad quads at the water level, where
the bed is 0.2-0.9 m down within 4 m of a shore, in noise clumps of 5-20, a flower in 1 of 12. One draw call, alpha-cut, no collider.

**B10. Fen boardwalks** (M, medium). Where a fen trail lies within 0.15 m of the water or below it, lay 2 m sections along the
trail: a broken or tilted one in six, a gap one in twelve (only where wadeable). Reuse the crossing records and `PlaceBridges`'
fitting. The deck is walkable navmesh.

**B11. Ground wetness floor per climate** (S, low-medium). `ClimateDefinition.groundWetnessFloor` (default 0); `WeatherSystem`
publishes `max(floor, wetness)`. Marsh 0.35: muddy paths shine, puddles sit in the ruts. Check trees don't go glossy.

**B12. Valley river steps and rapids** (L, medium-high). In the upper half, 2-3 sills dropping 0.8-1.6 m over 1.5 m, 20 m or more
from any crossing; at each a `Waterfall` and plunge (as `AddFalls` does for ravines), a ring of kit boulders and denser bed stones
for 10 m below. The level feeds fords, bridges and swimming: `RaidValleyTests` and the reach check must hold.

**B13. A capture that shows the air** (S, low). `AgentScripts/CaptureRaidLook.cs`: fixed seed and hour per raid, the four spots
(start, two sites, top), `ScreenCapture.CaptureScreenshot` in Play so fog and mist show, and the colour table written beside them.

### (c) New assets

Budgets follow `ScreenBudget` (40 px per visible triangle at 2160p, LOD0 holds across the top-down view), the asset conventions
and `ModelBudgetTests`. Every one gets close-ups of every seam and a game-camera shot before it is committed. Hunyuan3D and UltraTex
outputs go on the prototype-licence list for remaking before release.

| # | Asset | How | Budget |
|---|---|---|---|
| C1 | **Dead gnarled trees with hanging moss**, 3 looks, 6-8 m, for the Marsh (and the Valley's high tiers) | LocalSpawn concept ("bare twisted dead pine, grey wood, no leaves, matte") → Hunyuan3D → `ingest_generated.py`. Hanging moss as alpha cards added in Blender (the hair-card tools), Tree Wind on the cards only | LOD0 5-8k tris trunk + 1-1.5k card tris; 2048 bark atlas, 1024 moss sheet; LOD1 40%, LOD2 15%, shadow proxy; capsule collider; room 1.5 |
| C2 | **Lily pads** atlas: 4 pads + 1 white flower | Image model sprites, cleaned as `marsh_plants.py` cleans sprites | 1024², alpha-cut; 2 tris a pad |
| C3 | **Mossy stone ruins**: a wall fragment, a broken pillar, a fallen lintel, a stone stump | `props_manifest.json` rows, Layered Lit with moss from the baked coverage | 2-4k tris each, 1024-2048 |
| C4 | **Boardwalk kit**: deck 2 m (whole, broken, tilted/sunk), post pairs | Start from `modular_wooden_pier` (CC0) or a Blender script on `building_kit.py`; wet dark Kit Wood | 400-800 tris each |
| C5 | **Juniper** and **dwarf birch** shrubs for the Valley | Leaf cards via `plant_nodes.py`; dwarf birch may be Young Birch at 0.35-0.5 first | 1.5-3k tris, 1024 sheet |
| C6 | **Terrain layers**: River Gravel, Dry Meadow, Sphagnum/Peat | River Gravel and Dry Meadow from Poly Haven / ambientCG CC0 with displacement; sphagnum from `kit_textures.py` (no CC0 one fits) | 2K, height map for tessellation; no new global textures (the terrain is at 63 of 64) |
| C7 | **Spruce**: check before making one | The Poly Haven fir (`Prefabs/Environment/World/Norway Spruce A-C`) and Scots Pine A-C as painted terrain trees: LODGroup, Tree Wind, shadow proxies via `VegetationPackConverter.Tree`. If they pass, they replace the cedar stand-ins in the Valley and stunted pines join the Marsh's holms | Their own; check pixels per triangle at the default view |

---

## 3. Order of work

Most visible gain first. Every check is a launched raid (never the raid scene), same seed and hour before and after, shot with
B13, at the hours the change touches.

1. **Make the check honest** (B13). *Check:* a marsh shot shows TerrainMist where today's shows none; the colour table reproduces.
2. **The Valley's own ground** (A1, or B1 if the moss comes back; A2, A3, A5). *Check:* no moss skin on the meadow, no blue dots,
   heather and flowers in patches; median hue moves from 133° towards 70-90°.
3. **Light and air for every raid** (A7-A10, A14-A17, the arena's grade). *Check:* all four hours. Valley: median luminance
   0.24 → 0.29-0.31, mean red at or above blue, long soft shadows on rocks. Marsh: hue 90-110°, saturation 0.33+, green fog knee to
   waist high, the player readable at night. Arena: no lavender.
4. **Marsh trees and water** (A12, A13, A16, A18; Woods' mushrooms). *Check:* top-view canopy under 20%; dead trees in the shots;
   no white rings on pools.
5. **Clumps, meadow, moss where it belongs** (B1-B3). *Check:* the Valley's top view shows copses, meadow between, lone trees; tree
   count per mood within ±10% (log Scatter's return); Woods' shots unchanged.
6. **Rock** (A6, B4, B7). *Check:* six or more rock groups in the start view; walls read as rock from the game camera;
   `ExitsReachable` passes on the Raid Generator Report's seeds; frame time unchanged at the same spots.
7. **Edge and shore detail** (B5, Fen damp 0.1). *Check:* reeds and horsetail within 2 m of pools and in the shallows, strings
   mossy; pebbles and ferns at rock feet and path verges in the Valley. Detail instances no higher than the Woods' today (sum of
   `GetDetailLayer`, the `arena_grass_probe` pattern).
8. **The stream** (A11, B6). *Check:* foam rings at stones, no surf line; bank stones; wading and swimming work; `RaidValleyTests`.
9. **Brukam's Bridge arena** (hand-built in the Editor): grass prototypes, crags set into both gorge walls, a flowing river strip
   with the Valley material, stones and a fall upstream, spruce and birch on the rims, toll stones with bones and coins, a soft
   tinted mist curtain. *Check:* the seven bb4 shots again; `bb_probe_run.cs` still passes.
10. **Marsh assets** (C1-C4 with B9-B11). *Check per asset:* `ModelBudgetTests`, close-ups, game view; then marsh shots with dead
    trees, lily pads, a boardwalk, a ruin.
11. **More ground** (C5-C7, B8). *Check:* gravel banks in the Valley, sphagnum and mud in the Marsh; still single pass; displacement
    refreshed.
12. **River steps** (B12), last as the riskiest. *Check:* two falls with plunge foam; fords and bridges intact; `RaidValleyTests`
    and the reach check on many seeds.

## Budgets and rules held throughout

- **Quality stays.** Nothing lowers a graphics setting; costly things are made cheaper, not switched off.
- **Trees:** clumping moves trees, it doesn't add them (±10% per mood).
- **Plants:** no raid's detail instances above today's Misty Woods, the measured worst case (13-18 ms).
- **Objects:** crags and boulders as GameObjects only near walkable ground; small rocks, piles and shore stones as instanced terrain
  trees; lily pads one mesh per map.
- **Terrain:** at most 6 painted layers on a raid (single pass holds 14); no new global textures.
- **Biomes stay apart:** the Valley warm, open, rocky, yellow-green, clear water; the Marsh flat, wet, green fog, black water, few
  dead trees; the Woods as it is. Shared assets are fine; shared lists left unedited are not.
