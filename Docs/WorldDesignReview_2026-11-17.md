# World design review: from a square to places (2026-11-17)

The user: "In many ways, its just a big square where we can run in any direction without any real identity or variation. how can
we improve that? Research how other games do it, then go through scene after scene ... how we can improve the procedural
generation. Also suggest how we can make the world even more AAA like ... while at the same time keeping performance in check. Do we
have access to more world props that we aren't using ...?"

**Read and measured:** the GDD (§4.3, §7-9, §19), `ARCHITECTURE.md`, today's WorldLookPlan, RaidIdentityResearch and boss
docs, the memory notes on art, budgets and rendering; the planner, field, Layout and set-piece assets; read-only Editor evals that
planned 6 seeds per raid at 200 and 400 m and drew previews (scratchpad `...\scratchpad\wdr\`, which a reboot can clear); and
today's launched-raid shots (`tv_edge2`, `marsh_lily1`, `lf_woodsday`, `bb4`, `gh18`, `siteshots`). Nothing else was changed.

**Status 2026-11-19:** built and on for the Valley and the Marsh since 11-17: G1 districts, G4 node landmarks and short flags, G5 roads,
G8 the Valley's crossing cap, G9 heroes and mids; G3 compounds (Seter Yard, Bloomery Yard) and G7 low shelves since 11-18. On 11-19 the
Misty Woods took G1, G4, G5 and G9 too (golden layouts re-pinned), and **G2 edges were built** (`RaidLayoutPlanner.Barriers`, see
ARCHITECTURE "Edges in the wild ground"): on in the Woods and the Marsh, almost nothing in the Valley (its floor is all within reach of
a trail). Still open: the band's look (it doesn't read as an edge yet), G6 extraction kinds, G10 Marsh shapes, the Valley's bench path
and cirques, and checking §8's targets with the Raid Generator Report.

**Already done:** commit `2bef4560` built the WorldLookPlan's A2-A18, B1-B7, B11 and B13, and lily pads (B9) came later. Still
open: B8 (paint layers), B10 (boardwalks), B12 (river steps), assets C1-C7, and step 9 (the Brukam's Bridge arena). This review is
about **structure**, which that plan didn't cover.

---

## 0. The short answer

**It isn't literally a square; it plays like one.** The generator already builds clearings joined by a spanning tree of trails
with loops and spurs, five shapes, moods, regions on large maps, terraces, ravines and spires (one mood), rivers, ponds, hills,
boulders, lookouts and landmarks. What the player meets is still one evenly spread, walkable carpet, for six measurable reasons:

1. **Trails are paint, not paths.** The wild ground between them is walkable forest: undergrowth has no colliders, trunks stand
   3-4 m apart, ridges rise about 7 m over 16 m. The Ring's briefing says "wild ground nobody crosses"; anyone can.
2. **Everything is spread evenly.** A 200 m Woods map holds 13 clearings, 830 m of trail and **64 sites** (one per 25 × 25 m);
   69% of it lies within 10 m of a trail or clearing. Nothing is busy, nothing is empty, so nothing is memorable: Upton's
   "Trackless Forest".
3. **Places are props.** A set piece is one 9-14 m prefab, 1-2 per map at camps; the Burnt Farmstead is a single timber frame.
4. **Landmarks can't be seen.** At the default zoom (20 m, pitch 48°, fov 40°) the screen covers about 30 × 22 m: some 15 m ahead
   of the player and 7 m behind, and nothing farther ahead is in frame however tall it is. The 1-2 landmarks stand 9 m or more off
   any trail as scenery. "Seen from afar" has to work through the minimap, sound, shadow and light.
5. **One mood per normal map.** `RaidLayoutRules.Regions` gives one region unless the map is scaled up.
6. **Height doesn't stop you or show you anything.** Terraces, ravines and spires are on in The Gorges only; elsewhere 14-50 m of
   relief is walkable slope, which the top-down camera flattens.

**The fix:** districts with different jobs and densities, wild ground as an edge crossed only at gaps, compounds instead of
props, landmarks that work top-down, a trail hierarchy, extraction kinds, and then a hero / mid / micro dressing pass on every
screen. The ten priorities are in §7.

---

## 1. What other games do

[V] = read at a primary source; [S] = secondary press or a search summary only. Full links are under Sources at the end.

| Game or talk | Technique | What to take | Source |
|---|---|---|---|
| **Hunt: Showdown** [V] | Landforms first; compounds "grow up naturally around those features". "Clear lines and clear pathways"; a chimney as the navigation landmark. Boss compounds are "little multiplayer maps within the larger map"; water is "an obstacle" [S] | Water and cliffs as **edges**; a few dense authored compounds with transit ground between; one marker per compound | [4]-[7] |
| **ARC Raiders** [V] | Map marks high-risk, high-loot areas, which draw traffic. Main extracts free and noisy, fewer as the run goes on; keyed hatches quiet, on the margins. Heatmaps; hotspots "embellished" with level design | Extracts are the strongest flow tool; heatmaps | [9] |
| **Escape from Tarkov** [S] | Far-side extracts plus conditional middle ones. Interchange went from 2 to 5 extracts after camping | Too few exits make camping chokepoints | [11][12] |
| **Dark and Darker** [S] | Fixed modules; only their arrangement shuffles | Players learn modules, not maps | [13] |
| **Diablo IV** [V] | "Tile-sets" (kits plus props, interactives, light); transition scenes between tilesets; theme prop layers on or off | Authored kits, a separate **dressing layer**, **transitions** between districts | [14] |
| **Valheim** [V] | 64 m zones, one location each, placed by rules (biome, altitude, distance from centre, flatness, spacing); priority first; `ClearArea` strips vegetation | Landmarks before vegetation, flat ground, cleared silhouettes, spacing per kind | [1] |
| **Spelunky** [S] | 4×4 room grid; a guaranteed solution path; 8-16 templates per room type | Guarantee a spine, then fill from templates | [21] |
| **Unexplored** (Dormans) [V] | A graph with a big loop first; grammar adds cycles, locks, keys, one-way valves; tiles and dressing last | Plan the raid as a graph with one big loop and a valve | [22][23] |
| **Upton on Lynch** (GDC 2007) [V] | Landmarks, paths, edges, nodes, districts. Failure modes: Trackless Forest, Same Damn Corridor, Shotgun Shack, Road to Nowhere | A checklist for every generated map | [24][25] |
| **Ghost of Tsushima** (GDC 2021) [V] | Flags, short flags, breadcrumbs; something calls the player "every 30 seconds or less"; trees blocking a landmark removed; guiding wind and birds | A pacing target and breadcrumbs | [27]-[29] |
| **Breath of the Wild** (CEDEC 2017) [V] | Triangles at three scales (landmark, view-blocker, tempo); "gravity" of uneven attractors replaced an even tower spread | Rock forms in three size classes; weighted attractors | [31][32] |
| **Horizon Zero Dawn** (GDC 2017) [V] | Deterministic GPU placement rules over density maps; moving a road updates everything | Placement rules read the structure (roads, edges) | [33][34] |
| **Far Cry 5, Wildlands** [S] | Houdini roads, settlements, rivers, cliffs | Roads and settlements as **structure**, before scatter | [35]-[38] |
| **Enshrouded, No Rest for the Wicked** [S] | Handcrafted worlds: procedural ones "didn't really feel like a place" (Keen). No Man's Sky's pure noise is the other end [39] | Generate the arrangement, author the places | [17][19] |

**Six principles from this, for a 200-400 m top-down raid:**
1. **Graph first, terrain second, dressing last** (Dormans, Spelunky, Hunt).
2. **Contrast:** dense authored places, sparse transit, and edges you cross only at gaps (Hunt, Upton).
3. **Districts:** each region of a map has a job, a palette and a danger level (Lynch, Diablo IV).
4. **Something calls you every ~30 s.** At 4.8 m/s that is about 140 m: one node or short flag per 100-140 m of trail (Tsushima).
5. **Rock forms in three size classes,** in clusters (BotW, the target art).
6. **Extraction shapes the flow,** and heatmaps check it (ARC, Tarkov).

---

## 2. What the generator actually makes

Measured over 6 seeds (`wdr/plans.cs`). "Open" means on a trail or clearing (`WalkedDistance ≤ 0`).

| Raid, size | Clearings | Trail (m) | Sites | Set pieces | Landmarks | Boulders | Open | ≤10 m from open | Relief (m) |
|---|---|---|---|---|---|---|---|---|---|
| Misty Woods 200 | 13.3 | 831 | 64 | 4.5 | 1.5 | 15 | 32% | 37% | 25 |
| Misty Woods 400 | 46 | 3,273 | 239 | 13 | 5.5 | 65 | 30% | 37% | 30 |
| Troll Valley 200 | 10.5 | 839 | 38 | 1.2 | 1.5 | 82 | 18% | 21% | 50 |
| Troll Valley 400 | 27 | 2,315 | 121 | 3.5 | 5.5 | 393 | 11% | 10% | 53 |
| Moss Marsh 200 | 10.3 | 696 | 43 | 3.5 | 1.5 | 3 | 25% | 36% | 14 |
| Moss Marsh 400 | 32 | 2,628 | 156 | 10 | 5.5 | 11 | 23% | 33% | 13 |

**Scale.** The player runs at about 4.8 m/s, so a 200 m map is about 42 s edge to edge and a 12-minute raid could cross it 17
times. The screen shows about 1.6% of a 200 m map. So:
- **identity has to work at screen scale:** each screen needs a composition you recognise;
- **navigation is the minimap's job:** districts, landmarks and roads must read there.

**The previews** (`wdr/sheet.png`; top row Woods, bottom row Marsh) show the even spread clearly. Clearings and trails cover the
square uniformly, and the coloured site markers are sprinkled like confetti: no cluster, no void, no hierarchy.

**Where the code causes each problem:**

| # | Problem | Where it comes from |
|---|---|---|
| P1 | Wild ground is walkable everywhere | `RaidLayoutField`: "ridges rise with distance from a trail" (Ridge Height 7 m over Ridge Width 16 m). Undergrowth has no colliders. Only valley walls and The Gorges' ravines and terrace cliffs block movement |
| P2 | Even spread | `RaidLayoutPlanner.Rooms.PlaceRooms`: best of 28 candidates, the one farthest from every clearing. The site quotas in the Woods add up to 59 (3 rumours, 5 side, 9 camps, 6 events, 14 resources, 12 caches, 10 patrols), plus start, exits and apex |
| P3 | Small places | `RaidSetPieceDefinition.radius` is 9-14 m. `PlaceSetPieces` uses only rumour sites and 1-2 camps (`campPieces`) |
| P4 | Landmarks unseen | `RaidLayoutPlanner.Landmarks`: `LandmarkFromTrail` 9 m, 1-2 per map, `onlyToSee`. Nothing puts them on the minimap or gives them a sound |
| P5 | One character per map | `RaidLayoutRules.Regions => scaledUp ? largeRegions : Vector2Int.one` |
| P6 | All trails alike | One `trailHalfWidth` (2.6 m × mood). The short and long routes (`EnsureRoutes`) look the same as any spur |
| P7 | Exits are all one kind | Three on the far rim (`ExitRooms`, `ExitSpread` 62°): always open, all lit and held the same way |
| P8 | Height only in one mood | `terraces`, `ravines` and `spires` are 0 in every mood but The Gorges. The Valley and the Marsh have none |

---

## 3. Scene by scene

### 3.1 Misty Woods (keep its identity: dense, dark, cold)

**Works:** the forest itself, The Gorges (the only mood with real structure), and fog banks in The Hollows.

**Missing:** every other mood is the confetti map of P1-P2; screens are a uniform moss-and-fern carpet with no hero object
(`lf_woodsday`); nothing like the GDD §9.2 woodcutter camp exists.

**Changes:**
- **Thickets as edges (G2).** Windthrow walls (fallen cedars stacked as nav obstacles), dense young-spruce stands and bramble in
  the wild ground more than ~10 m from any trail, with deliberate gaps (game trails). Then the Ring's middle *is* ground nobody
  crosses, and a gap becomes a shortcut worth knowing.
- **Two districts per normal map (G1),** for example an Old Growth core round the apex and Windthrow or Open Glades at the start.
  Site density should differ about 3:1 between them.
- **Compounds (G3):** a Charcoal Burner's Camp (clamp mound, sooty ground decal, a hut, smoke column), a Woodcutter's Camp
  (cabin, log stacks, sawhorse, `Log Stack`), and a Hunters' Hide.
- **Giant trees as landmarks (G4):**
  - `Cedar Giant A/B` and the Haven's `Elder Cedar` / `Fallen Giant` at junctions, with clear ground round the trunk (Valheim's
    `ClearArea`);
  - crows circling the apex;
  - glow-mushroom rings as night breadcrumbs.
- **Golden tests:** turning any of this on for the Woods changes its pinned layouts. Re-pin `RaidLayoutGoldenTests` deliberately,
  or try on the Valley and the Marsh first.

### 3.2 Troll Valley (open, rocky, drier, lighter)

**Works:** the strongest macro form in the game: a river spine from mouth to head, benched walls, bridges as pay-or-fight
chokepoints, and since `2bef4560` crags, outcrops, copses, bed stones and a warmer grade.

**Missing:**
- **The plateau is dead space.** In the previews (`valley_maps.png`) the floor takes about 40% of the map's width. The ground
  beyond the walls is flat, empty and unused (78% of a 400 m map is more than 10 m from open ground).
- **The river is too easy to cross.** Each trail crosses at most once, but there are many trails, and the previews show 6-10
  crossings a map. With that many, it stops being an edge.
- **Few vertical breaks on the floor.** The target (`TrollValley_target.png`) is all rock **shelves**: knee-to-head-high steps and
  outcrops with trees on top. The floor today is a slope with boulders on it.
- **The set pieces carry the Valley's story** (Troll Ting, Petrified Trolls, Seter, Lair), but only 1.2 stand on a 200 m map.

**Changes:**
- **Cap crossings and make each one count** (G8, `RaidLayoutRules.Valley.maxCrossings`, default unlimited): two bridges and one
  ford. Hunt's "water is an obstacle" then holds, and the toll matters.
- **Use the benches:** a **high path** along one bench joining two exits gives a loop with height. Put a **seter on a bench**
  (`Ruined Seter` plus the Haven's `Shepherd's Fold`, `Dry-stone Wall`, `Hay Bales`) and lookouts there.
- **Side cirques:** one or two pockets cut into the walls (a Troll Lair cave mouth, a scree fan). Each is a dead-end district with
  its own set piece.
- **Shelves (G7):** terraces at 1.5-3 m in Scree and Stone and Gorge Narrows, dressed with the `Rock Face A/B`, `Mountain Face`
  and `Mountainside Crag` kit. This is BotW's medium triangle and the target's look.
- **Raise set pieces per map to 3-4,** placed rumour-first.
- **River steps (B12)** and two small falls, as the target has.

### 3.3 Moss Marsh (green base, thin green mist, water is the ground)

**Works:** black water without surf, lily pads, plants by height above the water, dead trees in twos and threes, green fog.

**Missing:** the briefings promise "strings of firm peat", "the old causeway", "a black pool in the middle" and "rock islands with
old pines", but the generator builds the same room-and-trail graph on flat ground (14 m relief). No causeway or boardwalk (B10 is
open; `Boardwalk.prefab` is unused), no rock islands, and the Hub's middle isn't guaranteed to be a pool. `Peat Stack`, `Rowboat`
and `Nokk Fiddle` are unused.

**Changes:**
- **The causeway is the spine (G5).** The short route becomes a raised, wider causeway or boardwalk (B10). Side ways are narrow
  strings you wade between.
- **The hub is a pool:** a large pond in the hub room, with the Nøkk/Black Pool set piece at its edge.
- **Pine Holms:**
  - great hills of 2-4 m with outcrops on top and Scots Pine A-C (`Prefabs/Environment/World`);
  - lookouts on top;
  - moonsilver nodes.

  The 2-3 holms per map are the marsh's landmarks and its high ground.
- **Peat cuttings:** straight-edged trenches with `Peat Stack`s, the one man-made geometry in a soft land, so you recognise it at
  once.
- **Districts:** open flark (sightlines, wading), Drowned Wood (cover), Peat Moor (dry, man-made).

### 3.4 Guardian's Hollow (the Heartroot's arena)

**Works:** the fight design, its phases and the sequence of gate, rest stone and hollow.

**Missing:** the floor is a flat, bare plane (`gh18`). The 40 m flat floor is required, because projectiles stop on any collider,
so the detail has to come from everything that isn't a collider on the floor.

**Changes (hand-built, S-M):**
- Ring the rim with `Cedar Giant A/B`, `Root Tangle` and the Haven's `Elder Cedar` and `Fallen Giant`, roots crossing *over* the
  rim edge.
- **Floor decals** (rot, root shadows, leaf drifts, puddles) and colliderless details: moss, mushrooms, SeedMesh bracket fungi
  `Tree_Mushroom_var1-4`.
- Canopy light shafts that die at 33% when the canopy closes.
- `Mist Volume` at the rim only, and glow flowers at the Heartroot's feet.
- Floor detail density can be lower inside the fight ring than on its edge, so telegraphs stay readable.

### 3.5 Brukam's Bridge

**Works:** the bridge and the troll.

**Missing (`bb4`):** bare grass banks, two flat gorge walls, a flat black river and a hard-edged white mist lozenge
(WorldLookPlan step 9, still open).

**Changes (hand-built, M):**
- crags set into both gorge walls (`Rock Face A/B`, `Mountain Face A/B`, `Mountainside Crag`);
- a flowing river strip with `Water River (Valley)`, bed stones and a fall upstream;
- spruce and birch on the rims;
- the troll's camp with `Troll Hoard`, `Old Bone`, the unplaced `Brukam Club`, toll stones with coins;
- Short Grass, Tall Grass and Bush prototypes on `BrukamsBridge Terrain`, so `Dress` grows anything at all;
- a soft, tinted mist curtain.

### 3.6 The Haven and the prologue: the donor

The Haven already has what the raids lack, all scriptless and borrowable:
- authored **Places** (Old Quarry, Lookout Cairn, Wayfarer's Stones, Shepherd's Fold, Drowned Stones, Fisher's Rest, Elder Cedar,
  Woodland Camp, Boat Landing);
- **Along the Way** pieces (Cairn, Boundary Stone, Field Stones, Log Stack, Fallen Giant);
- terraces and paths.

Nothing in this review asks to change the Haven.

---

## 4. Generator changes

**Rules for every item:** new fields default to today's behaviour (golden layouts and paint tests hold); each draws from its own
stream (`RaidLayout.SeedFor(seed, "Districts")`), after the existing steps, so co-op games and retries agree; no Unity API in the
planner, which runs on a worker thread (`RaidLayoutPrefetch`); `ExitsReachable` still checks, and barriers must leave the spine
and both routes open.

| # | Change | Where (classes, fields) | Default | Effort, risk |
|---|---|---|---|---|
| **G1** | **Districts on normal maps, with site density.** Allow 2-3 regions at 200 m. Each region's mood gains a `siteDensity` multiplier per site kind and a `role` (Approach, Wild, Compound, Lair), assigned by distance from the start (danger rises toward the apex, as Valheim's distance from centre). Quotas are shared by role weight, not area | `RaidLayoutRules.normalRegions` (new; `Regions` uses it when not scaled up); `RaidMapMood.siteDensity` (list per `RaidSiteKind`), `role`; `RaidLayoutPlanner.Regions.Share` weights by role; `PlaceSites` picks rooms and trails weighted by region | normalRegions (1,1); siteDensity 1 | M, low |
| **G2** | **Edges in the wild ground.** Beyond `edgeFrom` m from open ground, a mood places barrier bands that leave gaps: windthrow (trunks as nav obstacles), thicket (dense saplings with trunk capsules), bramble, boulder walls. Gaps are drawn where a straight line between two non-adjacent clearings crosses the band (a shortcut; Dormans' valve can be a one-way drop). The Ring's middle becomes truly wild | `RaidMapMood.barrier` (0-1), `barrierKinds` (prefab lists), `barrierGaps`; plan: `RaidLayoutPlan.Barriers` (polylines) from stream "Barriers"; `RaidMapSurface.Scatter` adds solid kinds along them; navmesh rebuild already follows | barrier 0 | M-L, medium (reachability, creature pathing; test with Raid Generator Report) |
| **G3** | **Compounds:** set pieces that own a clearing. A larger footprint (18-30 m), placed as a room in `PlaceRooms` (not a pocket). They flatten ground, stamp a paint layer or decal (yard, ash, trodden earth), and ring the yard with kit walls or fences that have 2-3 openings (chokepoints). Child `RaidMapAnchor` markers become sub-sites (cache, camp, resource) | `RaidSetPieceDefinition`: `ownsClearing`, `footprint`, `groundLayer`, `wallKit`, `openings`, `subSites`; `RaidLayoutPlanner.Rooms` (reserve 1-3 rooms by rule), `.Sites` (fill sub-sites first) | ownsClearing false | M, medium |
| **G4** | **Landmarks for a top-down camera.** 3-5 per map, at **nodes** (trail junctions, compounds), one per district, each a different kind. Each gets: (a) a minimap icon once within `revealRadius` (60-80 m, like `lookoutReveal`), or from the start if `knownAtStart`; (b) an ambient sound audible at 60-100 m (falls, bell, crows, drums); (c) optionally a light or smoke source whose glow lights the mist at the screen edge. Short flags (cairns, carved posts, rune stones) along the two routes every 100-140 m | `RaidLandmarkRule`: `atNodes`, `revealRadius`, `knownAtStart`, `sound`; `RaidLayoutPlanner.Landmarks` (junction candidates first; `LandmarkFromTrail` becomes per rule); new `RaidLayoutRules.shortFlags` (prefab list, spacing); minimap through the existing marker path | atNodes off; shortFlags empty | M, low |
| **G5** | **Trail hierarchy.** The short and long routes become **roads**: wider, a different paint (trodden path or gravel; causeway in the Marsh), ruts and stones at the edges, posts. Side trails stay as today. Game trails through barrier gaps are narrow, litter-coloured and barely visible | `RaidLayoutRules.roadWidth` (multiplier), `roadLayer`; `RaidLayoutField` stamps road distance separately; `RaidMapSurface.Paint` | roadWidth 1, no layer | S-M, low |
| **G6** | **Extraction kinds.** Main exits: loud, lit, always open (today's). **Closing** exits that go dark at a danger stage. **Conditional** exits: the Valley's ferry needs a toll; the Marsh's boat needs a Steel Needle or rope; a hatch needs a found key. **Hidden** exits that a rumour reveals. Place one conditional exit mid-map (Tarkov) | `RaidDefinition` or rules: `exits` list of {kind, closesAt, needsItem, placement}; `ExtractionPoint` gains a condition; the briefing names them | today's 3 main | M, medium (UI, co-op) |
| **G7** | **Shelves and outcrops in more moods.** Low terraces (1.5-3 m, not 7 m) in Cedar Ridge, Scree and Stone and Gorge Narrows, faces dressed by the existing crag pass | Data: `RaidMapMood.terraces` (e.g. 1.5), `terraceHeight` 2-3; check `AssignLevels` handles low steps; crag pass on terrace faces | data only | S data, M if crags need a terrace pass |
| **G8** | **Valley:** cap the crossings; a bench path; side cirques | `RaidLayoutRules.Valley`: `maxCrossings`, `benchTrail`, `cirques`; `RaidLayoutPlanner.Valley`, `ShapeCrossings` | unlimited, off, 0 | M, medium (`RaidValleyTests`) |
| **G9** | **Set-dressing pass: hero, mid, micro.** After scatter, each 30 × 30 m cell of wild ground near a trail gets at most one *hero* (a giant tree, outcrop or ruin fragment), 2-4 *mids* (a boulder cluster, log, shrub clump) and its micro as today. A hero sits 6-15 m off the trail so it enters the frame before you reach it | `RaidMapSurface` (a pass after `Scatter`); `RaidMapDefinition.heroes` / `mids` lists; stream "Dressing" | empty lists | M, low |
| **G10** | **Marsh shapes:** a causeway spine (B10 boardwalk on the short route), a pond in the hub room, holms (great hills + outcrops + pines), peat cuttings | `RaidLayoutRules.Fen`: `hubPool`, `holms`, `cuttings`; `RaidLayoutPlanner.Ground` | off | M, medium |

**Why districts and edges first:** they address P1, P2 and P5, which are the "big square". Compounds and landmarks add the places
to go, and dressing makes each screen recognisable. Two checks:
- **Raid Generator Report** gets new columns: district count, barrier coverage, landmark spacing along routes, and crossings.
- **Heatmaps:** add a position sample every 2 s to the playtest CSV (`PlaytestLogger` has no position rows today), to see where
  players really go, as Embark does.

---

## 5. Assets we have but don't use

The inventory resolved every guid the raid Map, Layout, Raid and scene files reference, against every `.meta`. One limit: the boss
arenas' TerrainData is binary, so their painted trees can't be checked.

**Traps:** SeedMesh renders pink and VIVID cyan until converted through `Editor/Environment/VegetationPackConverter`; use the
art prefabs in `Art/Haven/Structures`, not `Prefabs/Structures`, which carry gameplay components.

| Raid | Asset (path under `Assets/`) | What it adds |
|---|---|---|
| **Woods** | `SeedMesh/.../Mushrooms/Prefabs/Tree_Mushroom_var1-4`, `Rotten_wood_mushroom_var1-5`, `Red_Aged_*`, `SmallOrange_*` (convert) | Bracket fungi on trunks and logs: micro detail at hero trees |
| | `SeedMesh/.../Moss_cover` (4), `Cedrus_tree/Plants_Cover` (5), unconverted `Roldana` (14) and `Oreopanax` (5) | Undergrowth variety; moss cover on rocks |
| | `SeedMesh/.../Dry_cedrus_debris_Var1-7`, `Mossy_rotten_log_var3/4/9`, `Rotten_cedrus_branch` | Windthrow barrier pieces (G2) |
| | `_Project/Prefabs/Environment/World/Norway Spruce A-C`, `Scots Pine A-C` | A second conifer outline; young-spruce thickets |
| | `_Project/Prefabs/World/Places/Elder Cedar`, `Along the Way/Fallen Giant`, `Log Stack`, `Woodland Camp` | Landmarks and compounds |
| | `_Project/Art/Decals/Materials/Footprints.mat`, `Wall Grime.mat`, `Dirt Path.mat`, `Stone Path.mat` (never used in raids) | Trail and yard decals (G3, G5) |
| **Valley** | `_Project/Prefabs/Environment/World/Rock Face A/B`, `Mountainside Crag`, `Grey Boulder`, `Lichen Boulder` (use its `Simplified` LODs) | Shelves and cliff faces (G7): the target's look |
| | `_Project/Art/Vegetation/Haven World/Rocks/Mountain Boulder, Mountain Face A/B, Mountain Slab`; `Safe Haven/Rocks/Lake Boulder A-D, Lake Crag A-D` | Outcrop variety; river banks |
| | `VIVID_Arts/.../MLRT_Rock_Composite_001-010`, `MT_Rock_Small_Composite_01-04`, `MLRT_GroundRock_*` (convert) | The same rock family as the raid rocks: ready-made rock piles |
| | `_Project/Prefabs/World/Places/Old Quarry`, `Lookout Cairn`, `Shepherd's Fold`, `Wayfarer's Stones`; `Along the Way/Cairn`, `Boundary Stone`, `Field Stones` | Compounds on benches; cairns as short flags |
| | `_Project/Art/Haven/Structures/Dry-stone Wall`, `Dry-stone Gap`, `Hay Bales`, `Carved Post`, `Watch Post`, `Woodpile`, `Smoke Rack`; `Art/Haven/Skigard` | Seter compound kit, with walls whose gaps are chokepoints |
| | `_Project/Prefabs/World/Props/Bloomery Ruin`, `Brukam Club` (never placed) | Set dressing for the lair and camp |
| | `_Project/Art/Terrain/World/Pine Needle Floor`, `Damp Earth`, `Forest Litter` (unused layers) | B8 variant layer; road paint |
| **Marsh** | `_Project/Prefabs/World/Props/Boardwalk`, `Peat Stack`, `Rowboat`, `Nokk Fiddle`, `Bloomery Ruin` (made for the Marsh, never placed) | Causeway, peat cuttings, the Black Pool |
| | `_Project/Prefabs/World/Places/Fisher's Rest`, `Drowned Stones`; `Prefabs/World/Boat Landing`; `Art/Haven/Structures/Dock` | Compounds by water; a ferry exit (G6) |
| | `_Project/Art/Vegetation/Safe Haven/Plants/Lake Fern A-C`, `Lake Grass Clump` | Bank plants |
| | `SeedMesh/.../Terrain_Textures/.../Puddles` (convert) | A puddle layer for peat moor |
| | `_Project/Prefabs/World/Living/Small Spirit`, `Huldra Glimpse` (Haven only) | Marsh wisps; living breadcrumbs at night |
| | (missing) a `Data/World/Fireflies - Moss Marsh` and `- Troll Valley` config | Each raid's own night life (GDD §19.2 colours) |
| **Hollow** | `Cedar Giant A/B`, `Elder Cedar`, `Fallen Giant`, `Root Tangle`, `Tree_Mushroom_*`, Glow Mushrooms and Flowers; `Prefabs/Raids/SetPieces/Heartroot Trophy` (no layout uses it) | Rim, floor detail, trophy |
| **Bridge** | Rock Face, Mountain Face, Mountainside Crag; `Troll Hoard`, `Old Bone`, `Brukam Club`, `Sleeping/Striding Troll Rock`; `Water River (Valley).mat` | Gorge walls, camp, river |

**Avoid (style mismatch):** `Art/Forest/Prefabs` (low-poly kit), `Models/Generated/Vegetation/rich_oak*`, `Starter Assets`, and
Kenney or Quaternius packs (low-poly, stylised against the scanned look).

**Make next with LocalSpawn** (`generate_gathering.py --group props`: concept → Hunyuan3D + UltraTex → `ingest_generated.py`;
outputs go on the prototype-licence list), by impact: dead gnarled trees with hanging moss (C1); mossy ruin fragments (C3); a
charcoal clamp and hut; a seter hut and a cairn with a bell (the Peaks too); juniper (C5). Walls, fences and boardwalk kits suit
the scripted Blender tools (`raid_sites.py`, `building_kit.py`) better.

**Free CC0 sources that fit the scanned look:** Poly Haven (rocks, cliffs, trunks, roots, displaced ground textures) and ambientCG
(gravel, peat, moss with height maps). The terrain sits at 63 of 64 global textures: swap layers, don't add.

---

## 6. A more AAA look, with performance in check

### 6.1 Where frames go today

From the memory notes and ARCHITECTURE: the tessellated single-pass terrain and tree shadows are the big costs; the Woods run
13-18 ms at 4K (the plants' worst case); wall rocks and boulders measured free (11.1 ms either way); the sky-occlusion camera costs
about 3 ms CPU, 2 of it terrain trees; scripts about 1 ms.

**How to measure:** isolated Play, fixed spots, 120-300 frames with `ProfilerRecorder` (`FrameBenchmark -sh-benchmark` has raid
offsets), warm-up first, and noise of about 2 ms. Use the same seed and hour before and after.

### 6.2 Steps

Costs are **estimates** to check by measurement; Unity publishes no per-feature numbers for these.

| # | Step | Why it reads as AAA | Expected cost | Measure |
|---|---|---|---|---|
| L1 | **Hero / mid / micro dressing** (G9) and compounds (G3) | Every screen gets a composition; this is the single biggest perceived jump | Mostly instanced; +200-600 draw items per map, culled; ~0-1 ms | Batches and triangles at four spots |
| L2 | **Rock shelves and crags** (G7) using the existing crag pass | The concept art's main read: light catching rock edges | Measured near free for wall rocks | As B7 |
| L3 | **Trail and yard decals** (`Dirt Path`, `Stone Path`, `Footprints`, `Wall Grime`) | Ground tells a story: ruts, wear, ash | Screen-space decals; keep Max Draw Distance ≈ 40 m and one shared instanced material per kind; ~0.2-0.5 ms | GPU frame with decals on/off |
| L4 | **Canopy light shafts and dappled light** (a sun cookie made from the canopy, as Texture2D; URP ignores RenderTexture cookies) | Depth and mood in the Woods and the Hollow | One cookie sample: <0.2 ms | Pixel compare + frame |
| L5 | **Biome life:** fireflies configs for the Valley and the Marsh, birds per biome (ravens and an eagle in the Valley; ducks, herons and midges in the Marsh), `Wildlife` deer and reindeer | Motion is what makes a still frame feel alive | Small CPU; pooled | Script time |
| L6 | **Audio landmarks:** falls, bells, troll drums, crows over the apex, through `AmbientOneShots` and positional sources | Guides like Tsushima's wind without UI; fixes P4 | Negligible | — |
| L7 | **River steps and falls** (B12), flow and foam round stones (the shader already rings stones) | The target's waterfalls | Small | `RaidValleyTests`, frame |
| L8 | **Hanging moss, juniper, dead trees** (C1, C5) | The Marsh's silhouette | Alpha cards: overdraw. Trim cards to the leaf, LOD0 within budget | `ModelBudgetTests` |
| L9 | **Second paint variant per raid** (B8: dry grass / sphagnum) | Macro colour variation, so districts read on the minimap | Single pass holds to 14 layers | Stay single pass |
| L10 | **Night readability:** local warm light at compounds (`NightFlame`), cold moon elsewhere | Districts readable at night by their light | Additional lights in Forward+ are cheap; shadows off on most | Frame at 23.5 h |

**Leave as is (already good):** wind (TreeWind, MossWind), grass interaction (`GrassInteractor`, `TreeBender`), tessellation,
cloud shadows, sky occlusion, the ground light field, far-tree impostors (5 LODs ending in cards), and LOD0 held in the top-down
view.

### 6.3 Entities (ECS/DOTS), the GPU Resident Drawer and BatchRendererGroup

**Short answer:** don't adopt Entities. Do make the GPU Resident Drawer actually apply to your props. Consider a custom BRG only if
profiling later shows terrain-tree culling on the CPU is the limit.

**Entities / Entities Graphics: not worth it now.** Scripts are about 1 ms; the frame is GPU terrain, shadows and plants.
Creatures are tens of skinned meshes, projectiles few and pooled, vegetation already instanced. Co-op is NGO 2.13; NGO and Netcode
for Entities were historically not meant to be installed together, and NGO 3.0 takes Netcode for Entities as a dependency on Unity
6.7 LTS, so revisit then. A hybrid conversion is L+ effort and high risk for no measured gain.

**The GPU Resident Drawer: on, but probably skipping your rocks.** `PC_RPAsset` has GRD and GPU occlusion on, all renderers
Forward+. GRD only takes MeshRenderers whose shader supports DOTS instancing and that use no `MaterialPropertyBlock`. `TreeWind`
and `LayeredLit` reference DOTS instancing; **`MeshSurface`** (the tessellated shader on the rocks, logs, crags and boulders) has
only `multi_compile_instancing`, so those GameObjects likely fall back to plain SRP-batched drawing. Verify in the Rendering
Debugger's GPU Resident Drawer statistics in a launched raid. If confirmed, add DOTS instancing to `MeshSurface` (M, medium risk:
the instance ID must pass through hull and domain stages), which matters more once G2, G3 and G9 add objects. Keep MPBs off static
props.

**GPU occlusion culling:** Unity warns it can *cost* more where little is occluded. A top-down camera over open ground is that case.
Measure on versus off in a launched Valley raid, and keep it per renderer only if it wins.

**Terrain trees** (the raids' trees and small rocks) are drawn by the terrain's own instancing, not GRD (one forum report; verify).
They are fine for one camera. The sky-occlusion camera pays about 2 ms CPU for them. If the extra cameras grow, a small BRG drawer
for scatter would cull once for all cameras. That is a later, measured decision, not now.

**Budget to hold:**
- 4K default quality at the Woods' 13-18 ms worst case or better on the dev PC;
- no raid's plants above the Woods' today;
- at most 6 painted terrain layers;
- crags and boulders as GameObjects only near walkable ground.

---

## 7. The prioritised plan

Impact: H/M/L. Effort: S (under half a day), M (1-2 days), L (more). The ten priorities are 1-10.

| # | What | Kind | Where | Impact | Effort | Risk |
|---|---|---|---|---|---|---|
| 1 | **Districts on normal maps with site density** (G1): 2 regions at 200 m, Approach / Wild / Lair roles, sites weighted by role | Code + data | `RaidLayoutRules.normalRegions`, `RaidMapMood.siteDensity/role`, `.Regions`, `.Sites` | H | M | Low |
| 2 | **Landmarks that work top-down** (G4): 3-5 at nodes, minimap reveal, sound, short flags along the routes every 100-140 m | Code + data | `RaidLandmarkRule`, `.Landmarks`, minimap markers, `AmbientOneShots` | H | M | Low |
| 3 | **Compounds** (G3) with existing pieces: Seter (Valley), Charcoal / Woodcutter's Camp (Woods), Peat Cutters' Camp (Marsh), from Haven Places, Structures art, Skigard | Code + prefabs | `RaidSetPieceDefinition`, `.Rooms`, `.Sites`; new set-piece prefabs | H | M | Medium |
| 4 | **Edges: barriers with gaps** (G2), first in the Valley and the Marsh, then the Woods' Ring and Old Growth (re-pin golden tests) | Code + data | `RaidMapMood.barrier*`, `RaidLayoutPlan.Barriers`, `RaidMapSurface.Scatter` | H | M-L | Medium |
| 5 | **Hero / mid / micro dressing pass** (G9) with SeedMesh fungi and debris, spruce and pine, Haven rocks | Code + converted assets | `RaidMapSurface`, `RaidMapDefinition.heroes/mids`, `VegetationPackConverter` | H | M | Low |
| 6 | **Arena dressing:** Brukam's Bridge (WorldLookPlan step 9) and Guardian's Hollow's rim and floor decals | Scene work | Two encounter scenes | H (per m²) | M | Low |
| 7 | **Trail hierarchy** (G5): roads on the two routes, decals, posts; game trails through gaps | Code + data | `roadWidth/roadLayer`, `RaidLayoutField`, `RaidMapSurface.Paint` | M-H | S-M | Low |
| 8 | **Valley:** cap crossings, bench path, cirques, shelves (G7, G8), river steps (B12) | Code + data | `RaidLayoutRules.Valley`, moods' terraces, `.Valley` | H | M-L | Medium |
| 9 | **Marsh shapes** (G10): causeway or boardwalk (B10), hub pool, holms with pines, peat cuttings, unused Marsh props | Code + data | `RaidLayoutRules.Fen`, `.Ground`, `PlaceBridges` fitting | H | M | Medium |
| 10 | **Extraction kinds** (G6): closing, conditional (toll ferry, boat), hidden; one mid-map | Code + data | `RaidDefinition`, `ExtractionPoint`, briefing | M-H | M | Medium (co-op) |
| 11 | GRD coverage: verify, then DOTS instancing in `MeshSurface`; measure GPU occlusion on/off | Shader | `MeshSurface.shader`, Rendering Debugger | M (perf headroom) | M | Medium |
| 12 | Biome life and audio landmarks (L5, L6): fireflies configs, birds, wildlife, positional loops | Data + small code | `Data/World/Fireflies - *`, bird profiles | M | S | Low |
| 13 | Decals and light (L3, L4) | Data + small code | Decal pool, sun cookie | M | S-M | Low |
| 14 | Heatmaps: position samples in the playtest CSV, plus a Run Comparison view | Code | `PlaytestLogger`, Playtest Runs window | M (tuning) | S | Low |
| 15 | New assets: C1 dead trees, C3 ruins, charcoal clamp, cairn with bell, juniper, gravel / peat layers | LocalSpawn, CC0 | Prototype-licence list | M | M each | Low |

**Order:**
1. Items 1 and 2 together: districts give landmarks somewhere to stand.
2. Then 3 and 5, so each district has places and dressing.
3. Then 4, the riskiest structural one, on the Valley and the Marsh first.
4. The arenas (6) can run in parallel at any time.

**Every step is checked** in a launched raid with the same seed and hour, using the B13 capture, the Raid Generator Report's
reachability on many seeds, and a frame measure at fixed spots.

## 8. How to know it worked

| Check | Target |
|---|---|
| District contrast | Busiest region has ≥3× the sites per hectare of the quietest |
| Something calls you | Along both routes, a node, landmark or short flag every ≤140 m (≈30 s) |
| Edges | ≥30% of wild ground on Woods Ring and Old Growth maps is non-walkable, with ≥2 gaps per band |
| Crossings | Valley: at most 3 per map |
| Places | ≥3 compounds or large set pieces per 200 m map, each with one landmark |
| Screens | At four fixed spots per raid, each B13 shot has one hero object and a recognisable composition (judged by the user) |
| Performance | Frame time at the four spots within 1 ms of today's (noise is about 2 ms, so use 300 frames); no plant count above the Woods' |
| Flow | After playtests, heatmaps show traffic on both routes and at compounds, not uniform coverage |

---

## Sources

1. Valheim zones and locations (Jotunn modding docs): https://valheim-modding.github.io/Jotunn/tutorials/zones.html
4. Hunt dev diary (DualShockers): https://www.dualshockers.com/hunt-showdown-level-design-dev-diary/
5. Hunt 1896 map interview (PC Gamer): https://pcgamer.com/games/this-is-the-most-polished-map-we-ve-ever-shipped-hunt-showdown-1896-director-goes-deep-on-hunt-s-first-new-map-in-3-years
6. CRYENGINE Forge, making Hunt: https://www.cryengine.com/news/view/cryengine-forge-making-hunt-showdown
7. 80.lv on Hunt: https://80.lv/articles/the-development-of-hunt-showdown
9. ARC Raiders, Risk and Reward: https://arcraiders.com/news/risk-and-reward-blog
11. Escape from Tarkov (Wikipedia): https://en.wikipedia.org/wiki/Escape_from_Tarkov
12. Interchange extracts: https://consolepcgaming.com/escape-from-tarkov-rework-interchange-meet-player-demands
13. Dark and Darker modules (guide): https://metabot.gg/en/darkanddarker/guides/map-awareness-and-dungeon-navigation
14. Diablo IV quarterly update, March 2022: https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022
17. Enshrouded Q&A: https://wccftech.com/enshrouded-qa-keen-games-explains-all-about-its-zelda-meets-valheim-game/
19. No Rest for the Wicked: https://www.thestar.com.my/tech/tech-news/2024/05/04/after-two-winsome-ori-games-a-pivot-into-dark-fantasy
21. Spelunky generator analysis (Kazemi): https://tinysubversions.com/spelunkyGen/
22. Dungeon generation in Unexplored (Boris the Brave): https://www.boristhebrave.com/2021/04/10/dungeon-generation-in-unexplored/
23. Dormans on cycles: https://groups.google.com/g/proceduralcontent/c/TnlDXWXMimg
24. Upton, landscaping game worlds (Game Developer): https://gamedeveloper.com/game-platforms/post-gdc-i-rainbow-six-i-s-upton-talks-landscaping-game-worlds
25. The Image of the City: https://en.wikipedia.org/wiki/The_Image_of_the_City
27. A taxonomy of weenies (Game Developer): https://gamedeveloper.com/design/a-taxonomy-of-weenies-the-landmarks-that-define-i-ghost-of-tsushima-i-
28. Exploration in Ghost of Tsushima (GDC Vault): https://gdcvault.com/play/1027399/Exploration-in-Ghost-of-Tsushima
29. Blowing from the West (GDC Vault): https://gdcvault.com/play/1027350/Blowing-from-the-West-Simulating
31. BotW CEDEC 2017 notes: https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399
32. 80.lv, BotW design secrets: https://80.lv/articles/the-design-secrets-of-breath-of-the-wild/
33. Guerrilla, GPU-based procedural placement: https://www.guerrilla-games.com/read/gpu-based-procedural-placement-in-horizon-zero-dawn
34. Its slides: https://www.guerrilla-games.com/media/News/Files/GDC2017_VanMuijden_GPUBasedProceduralPlacementInHorizonZeroDawn.pdf
35. Procedural World Generation of Far Cry 5 (GDC Vault): https://gdcvault.com/play/1025557/Procedural-World-Generation-of-Far
36. SideFX talk: https://www.sidefx.com/learn/talks/procedural-world-generation-far-cry-5/
37. Wildlands procedural world (80.lv): https://80.lv/articles/procedural-world-building-in-ghost-recon-wildlands/
38. Wildlands and Houdini (3DVF): https://3dvf.com/actualite-19811-ghost-recon-wildlands-ubisoft-revient-sur-utilisation-houdini-html/
39. Building Worlds Using Math(s) (GDC Vault): https://gdcvault.com/play/1024514/Building-Worlds-Using
41. Unity GPU Resident Drawer: https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer
42. Making objects GRD-compatible: https://docs.unity.com/en-us/engine/current/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/make-object-compatible-gpu-rendering
43. GPU occlusion culling: https://docs.unity.com/en-us/engine/6000.3/manual/analysis/graphics-performance-profiling/in-urp/gpu-culling
44. Terrain trees and GRD (forum, unverified): https://discussions.unity.com/t/optimized-rendering-of-speedtrees-in-unity/1525063
46. Entities Graphics performance: https://docs.unity3d.com/Packages/com.unity.entities.graphics@1.1/manual/entities-graphics-performance.html
47. Netcode for Entities with Multiplayer Services: https://docs.unity.com/ugs/en-us/manual/mps-sdk/manual/build-with-netcode-for-entities
48. NGO 3.0.0 announcement: https://discussions.unity.com/t/netcode-for-gameobjects-3-0-0-is-now-available/1736792/1
49. URP decal renderer feature: https://docs.unity.com/en-us/engine/6000.0/manual/visual-effects/decals/renderer-feature-decal/reference

**Not verified** (left out of the claims above): the content of Path of Exile's tile talk, Arc Raiders' verticality, Elden Ring
landmark density, and Ghost of Tsushima's grass blade counts.
