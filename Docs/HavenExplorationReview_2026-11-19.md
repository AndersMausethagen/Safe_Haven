# Exploring the Haven: what it is like, what is missing, what to build (2026-11-19)

The user: "research and implement the best solutions to making Safe Haven the best possible world in terms of exploration and
design. what are we missing? what do we need in order to make exploring the Haven fun?"

**Read and measured:** `HavenWorldDesign.md`, `HavenWorldDesignReview_2026-11-18.md`, `WorldDesignReview_2026-11-17.md`,
`LevelDesign.md` (the Haven world), `GameDesignResearch_2026-11-17.md` §3, GDD §4, §6, §7.7, §9.5, `Territory.md`, `TODO.md`,
the world, territory, minimap and living-world parts of `ARCHITECTURE.md`. Code: `Territory/*`, `Level/PlaceDiscovery`,
`Level/WorldAtlas`, `Environment/CuriousPlace`, `ExplorationMap`, `MinimapHud`, `WorldMapHud`, `RaidLookout`, `RaidMapLandmark`,
`LootCache`, `ExperienceStone`, `CarvedStone`, `CameraZone`, `PlayerCameraController`, `HavenTravel`, `HorseHerd`,
`PlacedAmbience`, `JournalSettings`, `PlaytestLogger`. Data: the atlas (`Resources/Atlases/SafeHaven.asset`, its 512² region
grid decoded), `Resources/TerritorySettings.asset`, the recipe, `HavenSettings`, the journal pages, loot tables, the camera and
movement settings. The scene (`SafeHaven.unity`) by targeted reads: every prefab instance counted by GUID, the spawn zones and
the 21 waterfalls by position. The playtest CSVs (1,519 runs; 1,133 in November). Web sources at the end ([V] read at the
page, [S] search summary or secondary write-up). Nothing in the project was changed; this file is the only output.

**The 11-18 review (`HavenWorldDesignReview_2026-11-18.md`): none of its 14 proposals is built.** Checked: no `RaidLookout`,
`LootCache`, `ExperienceStone`, `CuriousPlace`, `Breakable` or `FieldCamp` in the scene or on any place prefab; the region labels
in TerritorySettings are unchanged; `PlaytestLogger` still writes Position only in raids; no quest, request or story asset names
a Haven place; its five decisions were never answered. This review keeps its ideas, makes them buildable without regenerating
the world, re-ranks them, and adds what it missed: sound and the minimap as the top-down camera's long sightline, *seen* places,
the falls stairs, camera vistas from `CameraZone`, new places without a regeneration, and ties into renown and Insight.

---

## 0. The short answer

The Haven is well built as ground and badly built as a reason to walk. Six numbers:

1. **0 of 15** place prefabs carries a component. **13 of 27** places give nothing but the find, and the other 14 only a few
   gathering points round them. A far place pays the same 25 Insight as one 118 m from the portal.
2. **The camera sees 14 m ahead.** At the default zoom (21 m, pitch 50°, FOV 40°) nothing past about 14 m in front of the
   player is in frame, however tall it is. The minimap shows no Haven place at all, and the world map only places already found.
   So in this world, "visible from afar" can only mean *heard*, *on the map* or *glowing in the mist*, and none of those is used.
3. **28% of the land past the meadows is more than 150 m (30 s on foot) from any named place**, 13% more than 200 m. On the ring
   path (230 m out, 1,445 m round), the longest stretch between named places is **486 m, 97 s**, from the Burnt Farmstead round
   the south-west to the Trapper's Camp.
4. **The Hollow Pines is 21% of the map with 1 place; Alder Mere and Hightarn have none.** Ravenwood, the richest and most
   dangerous wood, says "Wary"; the safest wood, the Wolfwood, says "Deadly"; Greyfell says "Dangerous" and holds no creature.
5. **The best landmarks are unnamed.** 21 waterfalls stand in four stairs, one climbing 49 m over seven falls, and nothing marks,
   names, sounds or rewards them.
6. **The three human runs since the 11-10 rework spent 23-40 minutes each at home and never left the opening quests.**
   Nobody has played the Haven by hand since 11-15, and the logger can't say where anyone walked there.

The fixes, cheapest pull first: lookouts that open the map and mark what they see; a verb for every kind of place; places you
hear before you see them and a hint on the minimap's rim; one walk out in the first hour; true labels and seven new places in
the empty wedges, at the falls stairs among them; a map that keeps a ledger; secrets off the trails; a story of the last
hearth-keepers told across the land. All of it can be stood at runtime from data, so **nothing here needs the world
regenerated**.

---

## 1. What exploring the Haven is like now

### 1.1 The land, at walking speed

| | Measure | At 5 m/s on foot (`PlayerMovementSettings.moveSpeed`) | On a horse (gallop 10-12.5 m/s) |
|---|---|---|---|
| Portal to the meadows' edge | 170 m | 34 s | 15 s |
| The ring path | 230 m out, 1,445 m round | 4.8 min round | about 2 min |
| Portal to a corner | 707 m | 2.4 min | 1 min |
| The farthest place (Grandmother Cedar) | 575 m | 1.9 min | 50 s |

The player's fog reveal is 20 m (`PlayerArmature` › VisibilitySource › Reveal Radius), a 40 m swath. Walking the whole ring
uncovers about 6% of the map; the whole map would take some 25 km of walking.

### 1.2 What stands in it (counted in `SafeHaven.unity`)

| Kind | Count | Does something? |
|---|---|---|
| Named places (atlas) | 27, of 15 kinds | Found once (25 Insight, a message, a star on the map). 14 (of 7 kinds) have 2-4 gathering points round them; nothing else |
| Junction cairns | 42 | No |
| Wayside scenes (Fallen Giant 9, Log Stack 7, Mossy Rocks 6, Hunter's Rest 4, Boundary Stone 4, Waystone 4, Maple Rest 3, Field Stones 3, Old Stump 2) | 42 | Only the 4 Waystones (an offering a day) |
| Resource points | 406 (iron 64, mountain ore 47, deep forage 47, forage 44, hardwood 40, stone and copper 39, fibre 32, loose stone 23, bog 29, at places 41) | Yes: higher tiers further out |
| Spawn zones | 29 hostile (edge 5, Misty Woods 8, deep 12, bog 4), 28 game (meadow 9, woodland 10, deep 9) | Hostile refill only by night, never within the ward + 120 m |
| Waterfalls | 21, in four stairs (§1.6) | Water sound only, like any river |
| Boundary stones | 11, stood at runtime | The territory loop, opened after raid 3 |
| `RaidLookout`, `LootCache`, `ExperienceStone`, `CarvedStone`, `CuriousPlace`, `Breakable`, `FieldCamp` | **0 each** | The raids' verbs, all usable at home, none placed |

The places, by distance from the portal:

| Ring | Places | Of which pay more than the find |
|---|---|---|
| The meadows (to 170 m) | 3: Ewe Fold 118, Mother Maple 119, Shepherd's Fold 145 | 3 (forage and plant points) |
| 170-300 m | 6: Trapper's Camp 195, Charcoal Camp 217, Netmender's Rest 221, Fisher's Rest 240, Burnt Farmstead 267, Old Quarry 290 | 3 |
| 300-450 m | 11 | 6 (the four Wayfarer's Stones, the Drowned Stones, the Elder Cedar) |
| Past 450 m | 7: Ruined Watchtower, Lookout Cairn, Windy Cairn, Grey Quarry, Broken Tower, Old Barrow, Grandmother Cedar | 2 |

They also bunch: past 300 m nearly all stand in two clusters, the north-east (bearings 48-64: Standing Stones, Elder Cedar,
Woodland Camp, Wayfarer's Stones, Grey Quarry) and the south-west (216-243: Wolf Den, Raven Cairn, Pilgrim's Stones, the two
towers, Lookout Cairn, Old Barrow, Grandmother Cedar, 8 of the 27). The south-east (114-181°) and the north (6-48°) are empty.

### 1.3 The rhythm of a walk

The planner keeps the *dull* walk short (52-72 m with nothing at all), and that holds: a cairn, a fallen giant or a glade is
never far. What is far is anything that *pays or promises*:

- 28% of the outer land is more than 150 m from a named place, 13% more than 200 m, 7% more than 250 m (10 m grid over the
  atlas, the meadows left out).
- Along the ring path, places within 70 m of it, by bearing:

| From | To | Arc | On foot |
|---|---|---|---|
| Old Quarry | Fisher's Rest | 309 m | 62 s |
| Fisher's Rest | Netmender's Rest | 124 m | 25 s |
| Netmender's Rest | Burnt Farmstead | 269 m | 54 s |
| **Burnt Farmstead** | **Trapper's Camp** | **486 m** | **97 s** |
| Trapper's Camp | Charcoal Camp | 76 m | 15 s |
| Charcoal Camp | Old Quarry | 181 m | 36 s |

Of those six, only the Old Quarry (4 rock points) and the two Rests (2 forage points) give anything on arrival.

### 1.4 What the player can see

The explore camera (`Data/Camera/PlayerCameraSettings.asset`: pitch 50°, 14-28 m, vertical FOV 40°) at the default 21 m hangs
16 m up and 13.5 m behind. Its top edge looks down at 30°, so it meets the ground **14 m ahead of the player**; at the widest
zoom, 19 m. A tower 40 m ahead is above the frame, top and bottom. The screen is about 27 × 22 m.

So the long sightlines BotW and Elden Ring build from silhouettes have to come through other channels here:

| Channel | Today in the Haven |
|---|---|
| The minimap | Buildings, carts, horses, drops, companions, quests, creatures seen. **No places**: `RaidMapLandmark` does it in raids (pinned to the rim once found) but the Haven has none |
| The world map (M) | Region names always; a star and name for each place **once walked to**; boundary stones once their region is known. No "seen", no counts, nothing about what a place gives |
| Sound | `PlacedAmbience`: wind on the nearest ridge, creaking dead trees, howls from where spawn zones lie, the Nøkk over still water at night. No place has a sound of its own |
| Light in the mist | Lit stones, campfires and lit windows glow in the volumetric fog (`AtmosphereLight`) |
| The camera | `CameraZone` can pull it out to 1.5× and tilt it 30° (`CameraZone.cs`:18), used once, at the prologue's fire |

### 1.5 Danger and reward by distance

Danger rises outward as it should (edge zones 200-300 m, Misty Woods 300-400, deep 390-480, the bog; night only), and so do
resources (loose stone 80-260 m, stone and copper 250-400, hardwood 300-480, iron 380-480, Greyfell's ore to tier 4). What does
not rise: the find (25 Insight anywhere), and what places give. And the labels are wrong (`TerritorySettings`):

| Region | Label | What is there |
|---|---|---|
| Ravenwood | danger 1 (Wary), "Softwood, berries, game" | 6 of the 12 deep zones, 101 of 406 resource points |
| The Wolfwood | danger 3 (Deadly), "where the cursed packs den" | 3 hostile zones, none deep; the Wolf Den has no pack of its own (nearest zone 43 m off) |
| Greyfell | danger 2 (Dangerous) | No creature and no game |

### 1.6 The falls stairs, the Haven's best landmarks

The 21 waterfalls (`Waterfall Plunge` instances, positions from the scene):

| Stair | Falls | From the foot to the top | Height climbed | Region | Nearest place |
|---|---|---|---|---|---|
| **Seven Steps** (as proposed) | 7 | (-268, 107) to (-329, 151) | 49 m | The Grey Pines into Greyfell | Wanderer's Stones, 86-126 m |
| **Raven Stair** | 4 | (-81, 253) to (-145, 304) | 35 m | Ravenwood | Charcoal Camp, 100-126 m |
| **Hollow Falls** | 3 | (268, -150) to (257, -189) | 13 m | The Hollow Pines | Netmender's Rest, 89-113 m |
| Lower Hollow Falls | 2 | (375, -160), (386, -175) | 7 m | The Hollow Pines | Island Stones, 138 m |
| Single falls | 5 | Meadows (151, -62), Eastwood (181, 36), Wolfwood (-141, -196), by Alder Mere's stone (-235, -86), Greyfell (-202, 307) | | | |

A stair is a climb with sound, a view at the top and hiding places behind the water: everything a landmark needs except a name
and a reason.

### 1.7 Day, night, weather and the year

Built and worth building on: game by day, hostile zones refilled by night, prowlers after the first breach, glimpses (the
Huldra, a hollowed deer, a spirit) rarer near home, the Nøkk at still water by night, owls, catchable fireflies, morning mist,
seasons with leaf fall, winter ice that walks to the islands, `WinterTracks` laying prints from lairs towards home, spring
floods at fords. What night and weather don't change is **what can be found**: no place is open only at night, nothing appears
after rain, and night pays nothing for its risk at home.

### 1.8 Story on the land

The lore is good and none of it is on the land. The journal has the Pact, the Hearth, the Boundary Stones and the Barrow Dead,
opened by quests, and ten "Found in the woods" pages (A Waystone Rubbing, The Hearth Law...) found only by searching raid caches.
At home the places named for the people who kept the hearth a hundred winters ago (the Burnt Farmstead, the Shepherd's Folds,
the Old Quarry, the Wayfarer's Stones where the old road ended, the Old Barrow) say nothing: no thought, no page, no grave good.

### 1.9 What players did (the logs)

- **The three human runs** (11-14 12:22, 11-14 23:28, 11-15 18:43): 35, 40 and 23 minutes at home, the rest in the prologue and
  one or two Misty Woods raids. Their Haven timeline is the opening quests one after another (Awakening, First Camp, the Rootbound
  Stones, the portal, tools, food and water, the workshop, the chest); everything gathered was tier 0-1 (Oak Wood, Sticks, Small
  Rocks, Grey Stone, Berries, Nettle Fibre). None reached raid 3, so the territory never opened for them.
- **Since 11-15** there are only prologue and raid probes (no run past 12 minutes without cheats).
- **Where they walked at home is unknown:** `PlaytestLogger` writes a Position row every 2 s only while `RaidDirector.Current`
  exists (`PlaytestLogger.cs`:199), and finding a place writes no row.

### 1.10 How exploring feeds the rest

Through the territory only (after raid 3): claim, hold, task, tithe, renown. Finding a place gives Insight and 5 hold. Nothing
found or done out there changes the base's renown (`HavenRenown` reads buildings, cleared overgrowth and residents), a raid's
briefing, or what the Haven can build.

---

## 2. What is missing

| # | Missing | Evidence |
|---|---|---|
| M1 | **A reason at each place**, the same for every place of a kind | §1.2; the 11-18 review's F1 |
| M2 | **A long sightline** that works top-down: sound, minimap, map, light, a camera that pulls out on high ground | §1.4 |
| M3 | **Pull in the first hour**, before the territory opens at raid 3 | §1.9 |
| M4 | **Even coverage**: the empty south-east, north and south-west ring | §1.2, §1.3 |
| M5 | **Climbing that pays**: the benches, knolls and falls stairs give no view, no map, no secret | §1.6 |
| M6 | **A map that rewards**: seen places, counts, what is left; Insight that grows with distance | §1.4, §1.5 |
| M7 | **Secrets and story** on the land | §1.8 |
| M8 | **Change**: night, weather and turns that alter what is out there | §1.7 |
| M9 | **Exploring that feeds the base and the raids** before and besides territory | §1.10 |
| M10 | **Measurement** | §1.9 |

---

## 3. What games that do this well teach

| Game | What they do | Rule to take | Source |
|---|---|---|---|
| **Breath of the Wild** | Triangles at three scales: big landmarks to aim at, medium forms that hide what lies behind, small ones for tempo. "Gravity": structures of different visibility pull the player in different directions. Spreading towers evenly with events between "didn't feel appealing" and felt linear; Korok seeds sit in the more interesting variants of a setup | Many uneven attractors, each hiding the next; small rewards where the shape is odd | [1] [S] |
| **Ghost of Tsushima** | Flags (tall, far), short flags (seen only close: "your instincts were right, there is something here"), breadcrumbs (ground marks that work in dense forest). "Every 30 seconds or less" something calls, not that one is bumped into. Trees knocked down where they hid a flag; smoke, birds, leaves and fireflies make landmarks visible from afar, day or night; landmarks change once their content is done | At 5 m/s, something calling every 150 m; a found place looks different once used | [2] [S], [3] |
| **Valheim** | 64 m zones, one location each; each location placed by biome, altitude, min and max distance from the centre, distance from others of its kind, with vegetation cleared round it. The Vegvisir runestone marks a boss's altar on the map; Bonemass's crypts need the Swamp Key | Spacing rules per kind and distance bands; a stone that *tells you where* is a reward; a key seen before it can be used | [4] [V], [5] [S] |
| **Enshrouded** | Climbing an Ancient Spire unlocks fast travel and the Flame at the top "charts the surrounding area" on the map, showing the wells and sanctums nearby; you glide off its balcony | A climb that reveals the next goals on the map | [6] [S] |
| **Elden Ring** | Map fragments stand on steles by roads, crossroads and graces; the map shows a region once its fragment is read. Miyazaki: the aim is the illusion of finding something you weren't meant to find | The map as a found thing; secrets that feel unintended | [7] [S], [8] [S] |
| **RuneScape: Dragonwilds** | Reviews praise journals scattered across Ashenfall for piecing the lore together and each region's own gimmick; they fault chests and camps whose loot was "mostly runes... or redundant crafting scrolls" | Lore on the land works; a chest must hold something wanted | [9]-[11] [S] |
| **Sons of the Forest** | Caves opened by found tools (rope gun, rebreather, shovel); the GPS shows points of interest as green marks | A tool found opens the map; a marked place promises | [12] [S] |
| **Grounded** | Milk Molars hidden round the yard raise stats for good and need a hammer to break out | Permanent rewards make players comb the land | [13] [S] |

**Rules this review holds every proposal to:**
1. **Every kind of place does one thing, always the same thing** (BotW, Dragonwilds' lesson): a name on the map is a promise.
2. **Something calls every 30 s, about 150 m** (Tsushima): a sound, a rim hint, a place or a lookout's mark.
3. **Seeing happens on the map, by ear and in the mist** (the camera's 14 m, §1.4): every landmark gets a sound and a mark.
4. **Height pays** (Enshrouded, BotW): climbing shows the next goals.
5. **Reward rises with distance and risk** (Valheim's bands): what is far or open only at night gives more.
6. **Change when used** (Tsushima): a place shows it has been done.
7. **No regeneration**: everything is stood at runtime from data, as `TerritorySpawner` stands the boundary stones.

---

## 4. The improvements, ranked by fun gained per effort

Effort: S (under half a day), M (1-2 days), L (more). [Quick win] marks what is S and needs no art.

### 0. First: log where players walk at home [Quick win] (S)

- **Problem:** no row says where anyone went in the Haven (§1.9), so nothing below can be judged.
- **Change:** a Position row every 5 s at home (keep 2 s in raids); new rows `PlaceFound` (place id, region, distance from the
  portal), `RegionEntered`, `LookoutUsed`, `CacheOpened`, `SecretFound`.
- **Build:** `Playtest/PlaytestLogger.cs` (the Position block at line 199: drop the `RaidDirector.Current` condition and choose
  the interval by scene), `PlaytestLog` constants, calls from `PlaceDiscovery` and `RegionTitleHud`; later a heatmap over the
  atlas picture in the Playtest Runs window.
- **Risk:** none worth naming (720 rows an hour).
- **Verify:** a 2-minute walk in Play writes about 24 SafeHaven Position rows.

### 1. Lookouts that open the map [Quick win for the first step] (S, then M)

- **Problem:** six places are marked `high: true` (3 Lookout Cairns, 2 Ruined Watchtowers, the Standing Stones) and do
  nothing. Climbing a bench, a knoll or a falls stair shows no more than the meadows.
- **Change:**
  - **Nine lookouts:** the Lookout Cairn (-379, -264), Raven Cairn (-388, -197), Windy Cairn (-428, 196), Ruined Watchtower
    (-283, -357), Broken Tower (-372, -336), Standing Stones (289, 186), the two meadow knolls' crowns (the two highest points
    75-170 m from the portal), and the top of the Seven Steps (about (-329, 151), 49 m above its foot).
  - **Press E, "Look out over the land":** the fog lifts 110 m round (the knolls 80 m), and every place within 180 m becomes
    **seen**: on the world map a hollow star with its name in grey, and on the minimap's rim when within 120 m. It still has to
    be walked to be *found* (and paid).
  - **The view pulls out:** within 6 m of a lookout a `CameraZone` takes the camera to 1.9× its distance and 16° flatter. At the
    default zoom the frame's far edge moves from 14 m to about 55 m ahead (75 m at the widest zoom).
  - **The first use** gives 30 Insight, and the portal names one unfound place it sees, once per lookout ("Smoke has stained
    the pines to the south. Someone burned charcoal there once.").
  - **Later (feeds raids, M9):** the first lookout used each turn adds one line to the next raid's briefing, beside the rumours.
- **Why:** Enshrouded's spires chart the area round them; Valheim's Vegvisir marks the next goal; BotW's chain of one place
  showing the next. For a top-down camera the map *is* the long sightline (rule 3), and height must pay (rule 4).
- **Build:**
  - `HavenLookout` (`Scripts/Level`), RaidLookout's Haven twin: `[InAFriendsHaven(HavenUse.Anyone, "a look with the visitor's own
    fog")]`, reveal and seen radii, remembers its first use in `CharacterProgress.MarkStoneUsed("SafeHaven:lookout:<id>")` (the
    saved set `ExperienceStone` uses).
  - `JournalRecord.seen` (a set like `discovered`) with `SaveMapper` and `SaveCoverageTests`; `WorldMapHud.PlaceAtlas` draws
    seen-not-found places; `MinimapHud.ReadLandmarks` reads them.
  - `CameraZone` and `PlayerCameraController.SetCloseness` both clamp at 1.5 (`CameraZone.cs`:18 and 35,
    `PlayerCameraController.cs`:178): raise both to 2.0.
  - **Stand them at runtime by place kind** (proposal 2's `HavenPlaceVerbs`). The Lookout Cairn prefab is also a Troll Valley
    landmark and the Ruined Watchtower and Standing Stones are raid set pieces, so their prefabs must not carry a Haven verb. The
    knolls and the Seven Steps are positions in the same data.
- **Effort:** S for the reveal and the camera at the six high places; M with seen marks and the other three.
- **Risk:**
  - The woods' mist (0.55) may wash out the vista, and shadows stop at 50 m (the cascades are fitted to 50 m), so the far
    ground is unshadowed. Judge in Play with `ScreenCapture.CaptureScreenshot`; a lookout may need a local mist clearing.
  - Generosity: nine reveals cover about a third of the map. That is the reward; places still need walking.
- **Verify:** in Play at the Raven Cairn, interact. `ExplorationMap.ExploredAt` 100 m away is at least 0.5, the seen set holds
  the Lookout Cairn and Pilgrim's Stones, the screenshot shows the pulled-out view, and a second use gives no Insight.

### 2. Every kind of place does one thing (M in all; S per kind)

- **Problem:** M1. The names promise what the props lack: a Charcoal Camp with no charcoal, a Wolf Den with no wolves, a
  barrow with no grave goods.
- **Change:** one verb per kind, from components that already exist:

| Kind (count) | The verb | Built from | Numbers |
|---|---|---|---|
| **Old Quarry** (2) | A stack of cut stone the old masons left: once, 6 Stone Blocks at the Old Quarry (the purification altar's cost), 4 Granite Blocks at the Grey Quarry | `LootCache` with 0 pins and a fixed table, once per character | Paired with proposal 3's quest |
| **Old Barrow** (1) | A barrow chest, locked (4 pins, heard); its door stands open only from dusk to dawn (proposal 9); a Draug rises there at night | `LootCache` + a Haven copy of `Data/Loot/Caches/Barrow Chest` without raid-only items; a night `MobSpawnZone` of 1 Draug (`ConfigureHome`) | Once; the Draug every night |
| **Wolf Den** (1) | The pack lives at the den: 3 Forest-Bitten Wolves; clear them and the den's hoard opens (hides, bone, a lost traveller's pack) | A `MobSpawnZone` at the den (night refill, 3 nights' rest); its `Cleared` unlocks a `LootCache` (a Haven `Traveller's Pack`) | The hoard once; the pack returns |
| **Burnt Farmstead** (1) | Relight the hearth: a fire that stays lit, warms (`PlayerWarmth`) and keeps prowlers off (`VisibilitySource` Is Light), 267 m out; its page (proposal 7) | The Campfire art + `LightFlicker` + `VisibilitySource`, lit once with 3 Sticks, saved | A safe rest halfway to the bog |
| **Woodland Camp** (3) | Make the names true: the Charcoal Camp's clamp gives 3 Charcoal a day; the Trapper's Camp's snare line 1-2 Rough Hide and a Bone a day; the Woodland Camp's fire to sit at | Resource nodes with a daily refill (`SettlementState.DaysPassed`); the camp fire as at the farmstead | |
| **Standing Stones, Island Stones** (2) | Carved marks: Runecraft practice, a rubbing with a Blank Parchment (`CarvedStone`, tier 1), once a day; and "the stones remember": the first reading marks the two nearest unfound places as seen | `CarvedStone` + a few lines for the seen marks | |
| **Lookout Cairn, Ruined Watchtower** (5) | Look out | Proposal 1 | |
| **Wayfarer's Stones** (4) | An offering (as the Waystones) and one carved line each; the four lines tell where the old road ran and that it ended at the portal | `OfferingStone`, `CuriousPlace`, a journal page each (proposal 7) | |
| **Elder Cedar, Mother Maple** (3) | Spirit places: Huldra glimpses three times as likely within 60 m; her gift (`SpiritBonds`' Huldra Silk) is left only there | A `GlimpseWatcher` entry weight by place; `SpiritBonds` | |
| **Fisher's Rest** (2) | A drying rack (Smoke Rack art) that turns 3 raw fish into trail food overnight; the lake's biggest fish keep near | A station or timed node; `FishInTheWater` size bias within 40 m (M) | |
| **Shepherd's Fold** (2) | Wild horses come to graze: half of `HorseHerd`'s spawns pick a fold when one is out of sight | `HorseHerd` spawn bias | Where to lasso one |
| **Drowned Stones** (1) | The Nøkk plays here every night, not by chance | `SpiritBonds` still-water spot | |

- **Why:** rule 1. Dragonwilds' reviews show what happens when a chest holds nothing wanted.
- **Build:**
  - A `HavenPlaceVerbs` asset (`Data/World/Safe Haven`): per place kind, the prefab to add (a chest, a fire, a node, a lookout,
    a zone) and an offset; per place id, overrides. A static `HavenPlaceVerbs.Begin(scene)` called from `ExperienceTracker` beside
    `TerritorySpawner.Begin` stands them at each atlas place, dropped to the NavMesh. Runtime objects: the scene file never
    changes, and a regeneration can't wipe them.
  - One-time state through `CharacterProgress.MarkStoneUsed("SafeHaven:once:<place id>:<what>")`: personal in co-op, as raid
    loot is. Daily state by `SettlementState.DaysPassed`. A lit hearth is the Haven's: `SettlementState`, synced like the stones.
  - `LootCache` has no save and adds the raid's regional loot: give it a "once for the character" key and check what it rolls
    with no `RaidDirector`.
  - Haven loot tables are copies without raid-only items (23 items are marked `raidOnly`), no Essence (§7.7). Extend the tithe's
    guard test to them.
- **Order:** Old Quarry, Old Barrow, Wolf Den, Burnt Farmstead, the camps, the stones, then the rest.
- **Risk:** the Barrow's and Den's creatures must keep the ward rule (`ConfigureHome`); Pact Runes and relics never here.
- **Verify:** for each kind, a Play probe (cheat teleport) uses it, checks what arrived, and after a reload from a throwaway slot
  the one-time part is refused. Never `sh_save` over the real save.

### 3. Hear it before you see it, and the minimap's rim [Quick win] (S-M)

- **Problem:** M2. The minimap shows no Haven place, and no place sounds.
- **Change:**
  - **A sound at every place kind,** heard 60-90 m off:

| Where | Sound (existing assets unless said) | When |
|---|---|---|
| The four falls stairs | A falls roar, looped (new, synthesised in `Tools/Audio`) | Always |
| Old Barrow, Burnt Farmstead | `Library/Crow Caw`, every 10-25 s | By day |
| Wolf Den | `Creatures/Wolf Howl`, every 30-60 s | Dusk and night |
| The camps, once a fire is lit | `Impacts/Fire Crackle Loop` | Always |
| Lookouts | `Library/Wind Loop`, soft | Always |
| Raven Cairn | `Biomes/Valley Raven` | By day |
| Drowned Stones | `Living/Nokk Song` (as now) | Night |
| Shepherd's Fold | A whinny, when horses graze there | With horses |

  - **On the minimap,** a found place shows as a small mark within the minimap's reach and stays pinned to the rim within
    150 m. An **unfound** place within 70 m shows a faint "?" on the rim in its direction: the top-down short flag. At most two
    rim hints at once, none in a fight.
- **Why:** Tsushima's flags and short flags, carried by smoke, birds and fireflies (rule 2); the raids' landmarks (11-17 review
  G4) already do exactly this.
- **Build:** a `PlaceBeacon` stood by `HavenPlaceVerbs`, or `RaidMapLandmark` with an option to read "found" from
  `JournalRecord.HasDiscovered` instead of its own flag; `MinimapHud.ReadLandmarks` gains the atlas places, checked against the
  fog as ARCHITECTURE "Show something new on the minimap" asks. Fit the rim hints into `HudRules` (the HUD was just decluttered).
- **Risk:** sound clutter (cap two beacons audible at once, duck under combat music); HUD clutter.
- **Verify:** at dusk 60 m from the den the howl plays; walking towards the Burnt Farmstead, a "?" appears at 70 m and becomes
  its mark on the find.

### 4. One walk out in the first hour [Quick win] (S, data)

- **Problem:** M3. Players stay in the meadows for 23-40 minutes, and no quest names a place.
- **Change:** two optional green Haven quests, held to homecomings by Story Pacing (two per homecoming):
  - **"Cut Stone"**, after the first homecoming: the portal, when the altar is first wanted: "The old folk cut their stone in the
    north. Some of it may still be stacked there." Reach Place: the Old Quarry (290 m north, past the Charcoal Camp); take the
    stack (proposal 2: 6 Stone Blocks, the altar's cost).
  - **"A Stone by the Water"** (the 11-18 review's proposal 3), after the second: read the Stillwater boundary stone (210 m east,
    past Fisher's and Netmender's Rest). It stays cold; the region shows on the map, with its name, danger and goods.
- **Why:** the base pulls the player out (rule 5, M9); the walk crosses three places and a lookout's reach.
- **Build:** quest data in the Content Editor (Reach Place with a Quest Target and its Quest Zone; Use Object for the stack);
  `QuestAuditTests` (nothing asked before it is taught: the stack is a bonus, the blocks are still crafted).
- **Risk:** one more quest in a full first hour; the hold rule keeps it to the homecoming.
- **Verify:** a fresh save after raid 1 shows "Cut Stone" waiting; the quest's waypoint leads to the Old Quarry; the blocks arrive.

### 5. True labels, then seven new places in the empty wedges (S, then M)

- **Problem:** M4 and §1.5.
- **Change, quick [Quick win]:** `Resources/TerritorySettings.asset`:
  - Ravenwood: danger 1 → 2, "Hardwood, iron, stone, game".
  - The Wolfwood: danger 3 → 2 and "Fangs, hides; the Wolf Den and the Old Barrow", until proposal 12 makes it deadly again.
  - Greyfell: danger 2 → 1, "Stone, granite, copper and iron ore. Nothing lives up there but the wind."
- **Change, then (M):** seven authored places, each a small group of existing props and one verb:

| Region | Place | At (x, z), then the nearest flat reachable ground within 40 m | Props on hand | What it does |
|---|---|---|---|---|
| The Hollow Pines | **The Hollow Falls** | (262, -170), the 3-fall stair, 312 m | Mossy Rocks, a Log Stack, the falls | A secret behind the top fall (proposal 8); the falls roar |
| The Hollow Pines | **The Felled Ring** | (160, -250), 297 m | Old Stump and Saplings, Log Stack, Sawhorse | An old cutting: a one-time Hardwood stack (6) |
| The Hollow Pines | **The Hunters' Hide** | (95, -480), 489 m (or (276, -380)) | Hunter's Rest, Smoke Rack, Bone Pile | A hunter's cache (a Haven `Hunter's Cache`); game within 40 m shows on the minimap |
| Alder Mere | **The Reed-cutters' Landing** | the mere's shore 15-25 m from its stone (-241, -99), by the outfall at (-235, -86), 269 m | Rowboat, Boardwalk, Peat Stack (set pieces never placed in the Haven) | A reed node; a fishing spot. Fills the ring's 486 m gap |
| Hightarn | **The Tarn Hut** | (175, 295), 343 m | Seter Ruin, Rune Cairn | Night fishing (proposal 9); a winter ice hole |
| Ravenwood | **The Raven Stair** | its top, (-145, 304), 35 m above its foot | the 4 falls, Rune Cairn | A lookout at the top (proposal 1), a secret behind a middle fall |
| The Grey Pines / Greyfell | **The Seven Steps** | foot (-268, 107), top (-329, 151), 49 m up | the 7 falls, Mountain Slab | The ninth lookout; proposal 11's rockfall gate |

  After them: every region has at least one place (the Hollow Pines 4), the outer land more than 150 m from a named place
  drops from 28% to 17%, and the ring's longest gap from 486 m to 309 m.
- **Why:** Valheim's spacing per kind and band; BotW's uneven gravity; rule 2.
- **Build:**
  - `WorldAtlas` gains a second list, `authoredPlaces`, that the atlas bake never writes, and `Places` returns both. Then
    `PlaceDiscovery`, the world map and `Territory.PlacesFound` see them. `WorldAtlasTests.TheAtlas_IsTheWorldThatWasGenerated`
    compares `plan.Pois.Count` with `atlas.Places.Count` (line 48): compare the generated list only. New ids are new kinds
    (`haven.hollow_falls.0`...), so saved finds stay true.
  - Their props through the same runtime data as proposal 2, positions chosen by a one-off Editor eval that samples the
    NavMesh, the slope (under 12°) and keeps 10 m off cliff faces and 8 m off paths; saved as numbers in the data.
  - Alder Mere's and Hightarn's stones will then need a find to claim (`Math.Min(placesToClaim, places)`), as the others do.
- **Risk:** the props must sit right on the ground and read from above: close-ups and the game view of every seam before
  committing (the visual-care rule). No terrain change, no regeneration.
- **Verify:** the coverage numbers above re-measured from the atlas; each new place found in Play, named on the map, in its region.

### 6. The map keeps a ledger, and climbing shows more (S-M) [Quick win for the first two]

- **Problem:** M6. The map can't say what is left, a far find pays as a near one, and climbing reveals nothing more.
- **Change:**
  - **Counts:** on the world map, a region's name shows "2/4" once one of its places is found, and a mark when all are; a found
    place's star fills once its one-time thing is taken.
  - **Insight by distance:** 10 + distance/15 per find instead of a flat 25: 18 at the meadows, 29 at 290 m, 48 at 575 m (all 27:
    913 against 675 today).
  - **Renown for known land:** `HavenRenown` gets a part, "the land known": +1 per place found, at most 30 (ranks are at 25, 90,
    180 and 300; a resident is 8). Exploring now raises the Haven's rank and widens the ward (GDD §4.1, M9).
  - **Height reveals more:** the player's reveal radius goes from 20 m to 20 + 3 m per metre above the mean ground 25 m round,
    at most 45 m: on a bench edge 7 m up, 41 m.
  - **A Places shelf in the journal** (the TODO's own idea): found places by region, what each gives, what is still to take.
- **Why:** Elden Ring's and Enshrouded's maps are found things; rule 5.
- **Build:** `WorldMapHud.PlaceAtlas`; `TerritorySettings.findInsight` becomes a base and a per-metre (`Territory.PlaceFound`);
  `HavenRenown` (one more part shown in `HavenOverview`); the player's `VisibilitySource` reads a height check every 0.5 s (8
  `Terrain.SampleHeight` calls); `JournalContent` a Places shelf.
- **Risk:** renown balance (a cap keeps it small); Insight total +35%.
- **Verify:** the counts change on a find; standing on a bench edge reveals visibly more on the minimap; `HavenOverview` lists
  the new part.

### 7. Found in the Haven: the last hearth-keepers, told across the land (M; mostly writing) [Quick win for the thoughts]

- **Problem:** M7 and §1.8.
- **Change:**
  - **Step 1 (S): the places think aloud.** A `CuriousPlace` at every place kind, two or three lines each, with "Look closer" for
    the next (the Burnt Farmstead: "The hearthstone is cracked right through. Somebody tried to keep this fire going after the
    roof was gone."). No new system.
  - **Step 2 (M): eight pages, "Found in the Haven",** one at each of the Burnt Farmstead, a Shepherd's Fold, the Old Quarry,
    the four Wayfarer's Stones (the four lines of the old road) and the Old Barrow: the last winter the hearth was kept, a
    hundred winters ago, and why it went cold. Within the story rules: dark, nobody named, the hunger.
  - **All eight read:** the portal says where the last keeper hid the hearth's store, a ninth site behind the Raven Stair's top
    fall, with a decoration plan (Rune Stone or Carved Post for the Haven) and one Pact Rune.
- **Why:** GDD §4.5 (environmental clues, recovered objects that unlock small stories); Dragonwilds' journals; the game's own
  premise that a hearth is what the forest cannot forget.
- **Build:** `JournalPage` gains `foundAt` (a place or site id, beside `foundInRaids`); reading the place's Interactable notes it
  found (`JournalRecord`), and `JournalNotices` announces it. Thoughts and pages are data (`Resources/JournalSettings`). Voice
  later, through the Voice Lines window.
- **Risk:** writing load and canon; one writer pass, then the user reads it.
- **Verify:** the journal's new shelf fills one page per place; the ninth site opens only after all eight.

### 8. Secrets off the trail (M)

- **Problem:** an explored region holds nothing more, and nothing rewards leaving a path (M7).
- **Change:** 20 secrets, two a region past the meadows (the Fen one):
  - **Where:** more than 25 m from any path, glade or place; behind or below a fall (the stairs of §1.6), on a table rock
    reached only by a scramble, on a ravine floor (4-6 ravines), on the meres' and the lake's islands (winter ice or a swim).
  - **Hints a top-down player can catch:** a glint that shows within 20 m by day (a small sparkle; a wisp at night), crows
    nearby, a resident's line in their news ("there's something under the falls below the quarry"), and a lookout marks a secret's
    area as a 30 m ring when within 180 m.
  - **Pay:** 40 Insight and a one-time cache (a Haven "Hearth-keeper's cache": materials of the ring's tier, a rare bait, a
    decoration plan); three of the twenty hold a Pact Rune. The ledger counts them ("secrets 1/2").
- **Why:** BotW hides Koroks in the odd variant of a shape; Miyazaki's "something you weren't meant to find"; Grounded's
  permanent rewards.
- **Build:** the same runtime data (kind Secret: position, prop, cache table). Candidates from a one-off Editor eval of
  `HavenWorldPlan.Make(recipe)` (paths, scrambles, ravines) and the scene's falls, then picked by hand and written as numbers.
  Props: Cache Crate, Troll Hoard, Old Bone, Rune Cairn (`Art/World/Props`).
- **Risk:** the Pact Rune economy (`Territory.md` expects about one in two raids): three is the cap. Nothing from §7.7's list.
- **Verify:** each secret reachable on the NavMesh; a probe opens one, reloads, and it stays open.

### 9. Night is a different map (S-M)

- **Problem:** M8. Night raises the danger at home (zones refill, prowlers) but not the reward.
- **Change:**
  - **The barrow's door** opens only from dusk to dawn; its chest (proposal 2) is a night prize, with its Draug.
  - **Wisps** lead from each Elder Cedar and the Mother Maple: a line of 6-8 glowing motes 15 m apart, at night, to a
    night-only secret (two of proposal 8's twenty). They fade at dawn.
  - **Lit stones and lit camps** show on the minimap's rim at night within 200 m: the way home in the dark.
  - **The Tarn Hut's** old fish bite only from 22:00 to 04:00 and run larger; the Nøkk plays at the Drowned Stones every night.
  - **In winter**, one of `WinterTracks`' trails a night ends at a fresh kill with hides: follow the prints.
- **Why:** rule 5; Tsushima's fireflies as night flags.
- **Build:** hours on the `HavenPlaceVerbs` entries; `CatchableFireflies` or the `Small Spirit` prefab for the motes;
  `MinimapHud`; `FishInTheWater`/`FishingSettings` hours for the tarn; a hook in `WinterTracks`.
- **Risk:** readability at night (the pale moonlight rule); prowlers make it fair.
- **Verify:** at 23:00 (cheat the hour) the barrow opens and the wisps stand; at 07:00 both are gone.

### 10. Goings-on at found places (M)

- **Problem:** a found place never changes (Tsushima: landmarks change on completion; M8).
- **Change:** each dawn turn, up to two goings-on at found places, gone after two turns if ignored, never urgent:

| Going-on | Where | What |
|---|---|---|
| A pedlar camped | a Wayfarer's Stones | three things for coin (the Hooded Stranger model, Trading Post rules) |
| Horses at the fold | a Shepherd's Fold | 2-4 wild horses graze, to lasso |
| Boars at the graves | the Burnt Farmstead | 3 Wild Boar root at the graves; drive them off for a grave good |
| Fish running | a river mouth or Fisher's Rest | twice the fish, larger |
| A blight patch | a found place in a held region | dead grass (`WitherSpot`) and three rot nodes to burn: +5 hold |
| A stranger from an earlier waking | a camp | one talk, one gift (the approved strangers story) |

  Told in a resident's news and as a ring on the world map. The lands' tasks name the place ("drive the boars off the Burnt
  Farmstead's graves") instead of "hunt 6 in the Hollow Pines".
- **Build:** a goings-on list (data) picked by `Territory`'s turn, stood by `HavenPlaceVerbs`; `ResidentNpc.News`;
  `TerritoryState.TaskOf` with a place.
- **Risk:** nagging: two at a time, nothing lost by ignoring one.
- **Verify:** across ten cheated dawns, no more than two at once, each where it should be.

### 11. Gates that teach, three to start (M)

- **Problem:** 99.8% of the land is open from minute one; no tool or person changes the map (11-18 review F6).
- **Change:**
  - **A rockfall** on the Seven Steps' top ramp: a copper pick clears it (the overgrowth's tool tiers), opening the short way to
    the lookout and the high ore benches. Until then, the long way round by Greyfell's road.
  - **A broken footbridge** where the ring path crosses a river: mended with 8 planks once Tor is home (`BuildingRepairs`); until
    then the ford, a swim in spring (`SpringMelt`).
  - **The bog's sunken causeway** to the Drowned Stones: a boardwalk (the unused `Boardwalk` set piece) for 10 planks; until then
    wading past the Bog Spitters.
  Each is seen before it opens and says what opens it; at most one per region; never on a road a material needs.
- **Why:** Valheim's swamp key, Sons of the Forest's found tools.
- **Build:** runtime from data (the host's state in `SettlementState`, synced); the portal says a line when one opens.
- **Verify:** each gate blocks the NavMesh before and not after (a probe path query).

### 12. The Old Grey of the Wolfwood (M): make "Deadly" true

- **Problem:** the Wolfwood's label promises the worst wood and it is one of the safest; the danger gradient has no peak at home.
- **Change:** a named pack leader at the Wolf Den on five nights of seven, with three wolves and the pack's howl buff; slain, it
  comes back after five days. It drops thick hides and a wolf-pelt trophy decoration for the Haven, and its first kill opens a
  page. It never leaves the den's 40 m leash and never comes near the ward. Then the Wolfwood is danger 3 again.
- **Build:** a `MonsterDefinition` and a prefab variant of the Forest-Bitten Wolf (×1.3 size, ×3 health), `MonsterHowlBuff`, a
  `LootOnDeath`, a night `MobSpawnZone` with `ConfigureHome`.
- **Risk:** "home must not punish going out": optional, far (383 m), and only for those who seek it.
- **Verify:** it spawns only at night at the den and holds its leash in a probe.

### 13. The mist draws back from held land (M)

The 11-18 review's proposal 7, unchanged in substance: a claimed region's mist × 0.6, a Secure one × 0.3, the bog and the belt
past 400 m always misty; a runtime copy of the 256² region map written each turn (`TerrainMist` › Region Map). The land shows
that it is held, as Enshrouded's Shroud recedes.

### 14. A lit stone is a way home (M)

The 11-18 review's proposal 12: from the Lands tab, step out at a lit boundary stone, and from any lit stone "return to the
portal"; by day, never in a push or a watch (`HavenTravel`, `BoundaryStone`). Lower than before: horses already halve every walk.

**Kept from the 11-18 review, for later:** residents' marks on the land (its 6), six woods of their own (its 8, needs a
regeneration), signposts at the junction cairns (its 9). Its proposals 1, 2, 3, 4, 5, 10, 11, 13 and 14 are folded into 2, 1,
4, 5, 11, 8, 10, 9 and 0 above.

---

## 5. Order of work, and how to know it worked

1. **Day one, no art:** 0, then the quick halves of 1 (the six high places), 5 (labels), 6 (Insight, renown), 7 (thoughts), 4.
2. **The verbs and the channels:** 2 (Old Quarry, Barrow, Den, Farmstead first), 3, the rest of 1 (seen marks, the knolls and
   the Seven Steps).
3. **Coverage:** 5's seven places, 8's secrets, 9.
4. **Story and change:** 7's pages, 10, 11, 12.
5. **Last:** 13 and 14, once the territory has been played by hand.

Then one hand playtest of the first two hours, read through proposal 0.

| Goal | Target |
|---|---|
| Every place pays | Every kind has a verb; no place gives only the find |
| Coverage | Outer land more than 150 m from a named place: at most 17% (28% now), less again with the secrets |
| The ring | Longest gap between named places at most 310 m (486 m now) |
| Regions | At least one place per region and one per 5% of the map; every label matches the zones in it |
| Rhythm | A probe walking each road from the portal to its end meets a place, a sound beacon or a rim hint every 30 s or less |
| Play | The heatmap shows a walk past 200 m before raid 2; a lookout used and a cache opened in every session |

---

## 6. Decisions for the user (recommended first; the agent proceeds on them unless told otherwise)

1. **What may exploring pay?** *Recommended:* knowledge and home: map reveals, lore, decoration plans, Insight, renown, spirit
   favours, one-time material caches, three Pact Runes from secrets and one from the story. Never Essence, relics, people or raid
   plans (§7.7). *Alternative:* small boons carried into the next raid.
2. **Lookouts reveal 110 m and mark places 180 m round.** *Recommended:* yes. *Alternative:* reveal only (no seen marks).
3. **Renown from known land** (+1 a place, at most 30). *Recommended:* yes. *Alternative:* none; exploring pays Insight only.
4. **A named beast at home** (the Old Grey). *Recommended:* yes, optional and leashed. *Alternative:* the Wolfwood stays Wary.
5. **The 11-18 review's open decisions** (six woods, gates, travel to stones, the first stone after raid 1): its recommended
   answers, as carried into 4, 11 and 14 above.

---

## 7. Notes for whoever builds it

- **Don't regenerate the world.** "Everything it made before is replaced" (LevelDesign), and the atlas bake is a retired
  builder. Stand everything at runtime from data, as `TerritorySpawner` does: opening `SafeHaven.unity` already rebuilds the
  scatter into a 280k-line diff (TODO), and scene saves bring "save?" dialogs. Anything that must live in the scene goes under
  **06 Story & Events**, which the generator keeps.
- **Shared prefabs.** Standing Stones, Ruined Watchtower, Old Barrow, Burnt Farmstead and Wolf Den live in
  `Prefabs/Raids/SetPieces` and are used by raid set pieces; the Lookout Cairn is a Troll Valley landmark too. Never give them a
  Haven verb: add it at runtime by place kind. The Haven-only place prefabs (`Prefabs/World/Places/*`, Woodland Camp) could be
  edited, but one runtime route for all is simpler to keep.
- **Saves:** one-time things per character through `CharacterProgress.MarkStoneUsed`; the seen set and found pages in
  `JournalRecord` (with `SaveMapper` and `SaveCoverageTests`); the Haven's world state (a lit hearth, a mended bridge, gates) in
  `SettlementState`, synced to guests by `HavenSync`.
- **Co-op:** every new interactable declares `[InAFriendsHaven(...)]` (`InteractionRuleTests`); caches are personal; gates and
  hearths are the host's.
- **Creatures at home** use `MobSpawnZone.ConfigureHome` (night refill, the ward + 120 m).
- **Loot at home:** Haven copies of loot tables without raid-only items; check what `LootCache` rolls with no raid running.
- **Tests:** `RunTests.ps1` only; never edit .cs during a run. Probes never `sh_save` over the real save. Leave scenes saved or
  reverted. Visual care: close-ups and the game view of every new prop group before committing.
- **HUD:** rim hints go through `HudRules`; the user just asked for a less cluttered HUD.

---

## Sources

1. Breath of the Wild, CEDEC 2017 talk notes [S]: https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399
2. A taxonomy of weenies, the landmarks of Ghost of Tsushima (Game Developer, from the GDC 2021 talk) [S]:
   https://gamedeveloper.com/design/a-taxonomy-of-weenies-the-landmarks-that-define-i-ghost-of-tsushima-i-
3. Exploration in Ghost of Tsushima (GDC Vault listing): https://gdcvault.com/play/1027399/Exploration-in-Ghost-of-Tsushima ;
   Game Informer on rewarding "a minute, or even 30 seconds" of travel:
   https://www.gameinformer.com/2021/05/20/more-than-just-dots-on-a-map-the-challenges-behind-open-world-game-creation
4. Valheim zones and locations (Jotunn modding docs) [V]: https://valheim-modding.github.io/Jotunn/tutorials/zones.html
5. Valheim's Vegvisir and the Swamp Key [S]: https://allthings.how/every-valheim-boss-in-1-0-locations-summons-and-rewards/ ;
   https://gamewith.net/valheim/article/show/28449
6. Enshrouded's Ancient Spires [S]: https://gameleap.com/articles/enshrouded-ancient-spire-fast-travel-locations-map ;
   https://metaforge.app/enshrouded/database/lore/the-ancient-spires-and-fast-travel
7. Elden Ring's map fragments [S]: https://www.windowscentral.com/elden-ring-all-map-fragment-locations
8. Miyazaki on discovery (PC Gamer) [S]:
   https://www.pcgamer.com/two-years-after-release-miyazaki-says-elden-rings-world-still-has-a-small-element-that-i-feel-has-not-yet-been-discovered
9. RuneScape: Dragonwilds, early access impressions (Lords of Gaming) [S]:
   https://lordsofgaming.net/2025/05/runescape-dragonwilds-early-access-impressions
10. RuneScape: Dragonwilds 1.0 review (GFinity) [S]:
    https://www.gfinityesports.com/article/runescape-dragonwilds-v10-review-a-magical-world-punished-by-brutal-solo-survival
11. RuneScape: Dragonwilds reviews (Game Informer, Pocket Tactics) [S]: https://gameinformer.com/review/runescape-dragonwilds/an-adventure-in-busywork ;
    https://www.pockettactics.com/runescape-dragonwilds/review
12. Sons of the Forest's key items and GPS [S]:
    https://gameranx.com/features/id/444335/article/sons-of-the-forest-how-to-get-the-shovel-rebreather-rope-gun-locations-guide/ ;
    https://pcgamer.com/sons-of-the-forest-gps-locators
13. Grounded's Milk Molars (Dot Esports) [S]: https://dotesports.com/general/news/all-milk-molar-locations-in-grounded

**Not verified** (left out of the claims above): density figures per km² for BotW or any other game (the CEDEC notes give none),
Valheim's map reveal radius, and whether Enshrouded's compass shows undiscovered places.
