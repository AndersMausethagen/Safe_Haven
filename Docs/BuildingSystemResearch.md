# Safe Haven building research: why it feels like placing squares, and what to build

Research date: 2026-11-15. This was read-only: no game code, data, scenes or prefabs were changed, and no Unity CLI or Play
mode was used. Paths are relative to `d:\Safe Haven\Safe Haven` unless they are absolute. `file:line` points at the `test`
branch as of commit 6f481dd6.

The user asked: "Right now, I just feel like I'm putting down squares without caring about where to put them. I want the
player to feel like he is building a small, living town, where his decisions shape the look and feel of the settlement.
What are we missing? Is having premade structures the right way? Should we have more modular building... furniture and work
tables in rooms, which, depending on what items you put in the room, turn it into a workshop or kitchen? Look at how
RimWorld does it. Or can we do a hybrid approach...? Which system would fit Safe Haven best?"

---

## 0. Summary

### What the code shows

Safe Haven has a complete premade building system: 44 kinds in the build menu, a ghost with grid and edge snapping, walls
drawn in lines that bend round the portal, scaffolds the player hammers up, five-tier production buildings whose upgrades
switch on modelled "Tier N" parts, decorations, wall torches that light at dusk, chimney smoke and a co-op sync that
already carries every building. The building system is not broken. What is missing is any reason to care **where**
something stands. Six causes, all in the code:

1. **Every number is global and counted per kind.** Comfort counts each kind of building once, anywhere
   (`HavenReadiness.cs:27`). Raid bonuses take "the best building of a kind" (`BuildingDefinition.cs:31-38`). A second
   building of a kind gives half its renown (`HavenSettings.asset:15`, `repeatShare: 0.5`). Beds are a count shared out in
   order of arrival (`SettlementState.cs:243-254`). A resident's +50% work bonus goes to **every** station of their
   workplace's kind, wherever it stands and whether they stand at it or not (`CraftingStation.cs:166-200`). So the best
   Haven is "one of each kind, on any flat ground", and the layout changes no number.
2. **People don't live in what you build.** All residents sleep at **two** fixed scene points
   (`ResidentSpawner.cs:93`: `homeSpots[i % homeSpots.Length]`; the SafeHaven scene lists two, "6" and "Home Spot 2",
   next to the portal). A Hut or Cottage adds to the bed count, but nobody walks into it. Workers stand in the yard of the
   **first** building of their kind (`ResidentNpc.cs:124-150` → `BuildingObject.Find`, `BuildingObject.cs:66-77`: "The
   first active building of the given kind"). Everyone gathers at the **first** Campfire
   (`ResidentLife.cs:300-310`, `SettlementSettings.residentGatheringPlace`). A second campfire, or a cottage built by the
   forge, changes nothing in anyone's day.
3. **Buildings are closed boxes.** Each building root is one box collider plus a carving NavMesh obstacle of the same box
   (`Prefabs/Structures/Hut.prefab`: a 3.15 × 3.44 × 2.66 m box, `m_Carve: 1`). There is no inside to walk into, and no
   room exists anywhere in the game.
4. **Distance costs nothing.** Building, crafting and workers all draw from one stash (`MaterialPool`, `WorkerProduction`),
   and workers carry finished goods back without walking. Nothing is hauled.
5. **The land only says no.** Placement checks are refusals only: inside Haven Meadows, out of the water, at most 0.5 m
   unevenness under the footprint, no overlap, and yards kept clear (`HomePortal.cs:185-210`,
   `BuildPlacer.Spots.cs:70-116`). No spot is better than another. Even the Dock may not stand over water: "Not in the
   water" applies to every building (`HomePortal.cs:200-203`), and `CheckBuilding` returns null for all (`:212`).
6. **Geometry only counts during rare breaches.** Attackers go for the nearest standing building, and wall-breakers for
   walls (`HomeAssault.cs:53-85`). Walls carve the NavMesh (`Construction.cs:361-365`). Lanes start beyond the outermost
   building (`WardBreachDirector.cs:652-682`). Light keeps prowlers out (`VisibilitySource.cs:66-85`). But a breach comes
   about every three successful raids, buildings can never be lost, and all repairs are paid at once at the portal for 30%.

There is also no connective tissue between buildings: no roads or paths (worn paths are an open TODO,
`ARCHITECTURE.md:1808`), no plots and no yards that belong together.

### The recommended system

**A hybrid: premade buildings that people really live and work in, set in yards the player draws, which become workshops,
kitchens, home yards or feast greens from what is put inside them.** In more detail:

- **Production and landmark buildings stay premade** and are run through their own windows, as now. They carry the tier
  I–V ladder, the recipes, the workers and the kit art the user chose.
- **The player makes the places between them.** A skigard, palisade or stone wall drawn into a closed ring with a gate
  becomes a **yard**, the Norwegian *tun*. What stands inside decides its role, as furniture decides a room's role in
  RimWorld or Dragon Quest Builders 2. These are rooms without roofs. They are fully visible from the 48° top-down camera,
  use the line drawing that already exists, and need no interior, no roof cutaway and no new structural rules.
- **Placement pays through people.** Residents sleep in their own house and work at the station nearest their home. A
  house's surroundings (a hearth, water, light, a bench, or a noisy forge next door) set its quality.
- **Production buildings get extensions**, Valheim style: a bellows, quench tub or grindstone placed next to the Smithy
  adds a chosen specialisation, within a few slots per tier.
- **Paths** are drawn like fences, and footpaths wear in where people walk.
- **Fully modular walls and roofs are not recommended now.** Open, furnishable house interiors are the step after, if the
  yards prove the feel and the user accepts larger houses (§3.3, §3.9).

This builds on the work in progress: house tiers and comfort, residents assigned to huts, the NPC window and timed
crafting (§1.8, §3.3).

### Recommendations, ranked by impact per effort

Effort: S = data or a few lines; M = 1–3 days; L = a week or more; XL = several weeks with new art.

1. **Homes people live in** (M): a resident sleeps in their assigned house, walks in at night and out at dawn. The window
   light and smoke show who is home. Workers go to the nearest instance of their workplace, and people gather at the fire
   nearest home.
2. **"Here" readout on the ghost** (S–M): while placing, show what a spot is near (hearth, water, light, the forge, the
   walk to work) and draw the radii that already exist (light, Watch Post 18 m, Ward Totem 7 m).
3. **Home quality from surroundings** (M), feeding the in-progress house tiers and comfort.
4. **Yards as rooms** (L): closed fence rings with a gate get a role from their contents (Home Yard, Work Yard, Kitchen
   Garden, Feast Green, Paddock), an overlay, and small automatic clutter.
5. **Station extensions with slots** (M, plus LocalSpawn props).
6. **Paths and worn footpaths** (M).
7. **Visible stores** (S–M): woodpiles and barrels that grow with the stash.
8. **Residents hammer what you placed while you raid** (M): Carpenter and Mason, as DQB2 residents and Sons of the
   Forest's Kelvin do.
9. **Open, furnished house interiors** (XL): only after a playtest, and only with a decision on house scale.
10. **Drawn log rooms**, with modular walls and an automatic roof (XL): only if 9 is loved.

---

## Part 1: How building works today

### 1.1 From choosing a building to a working building

| Step | Owner | Where | What happens |
|---|---|---|---|
| Open the menu | `BuildPlacer.ToggleMenu` | `Building/BuildPlacer.cs:134-162` | G (pad: the build binding) opens `BuildMenuWindow`, docked along the bottom with tabs Crafting Stations, Walls & Fences, Structures, Decorations and Manage (`BuildCategory.cs`). It only works in an open build area. A visitor needs the host's yes (`HavenVisit.BuildingNeedsAsking`). |
| What can be built | `IBuildArea.Buildable` | `Settlement/HomePortal.cs:146-155` | Known (`PlayerCrafting`), Show In Build Menu, and unlocked (`BuildingUnlocks.IsOpen`: required buildings and tiers, Haven rank). |
| Ghost | `BuildPlacer.BeginPlacing`, `BuildGhost` | `BuildPlacer.cs:231-260` | A see-through copy, green or red. A building with a front yard starts turned to the camera. The yards of placed buildings are drawn. |
| Where it snaps | `ResolveSingle` | `BuildPlacer.Spots.cs:23-67` | A 0.5 m grid (`gridSize`), or flush against the side of a building within 0.75 m, turned to match it. Alt places freely. The wheel turns it 45° (Shift: 15°). |
| Where it may stand | `CheckSpot` + `HomePortal.CheckSpot` | `BuildPlacer.Spots.cs:70-116`; `HomePortal.cs:185-210` | Five ground samples (four corners and the middle) must be in Haven Meadows, out of the water, and within 0.5 m of each other in height. Nothing solid may overlap, no other building's yard may be covered, and its own yard must be clear. |
| Pay and spawn | `BuildPlacer.Build` | `BuildPlacer.cs:464-486` | Pays from the bag first, then storage (`MaterialPool`). `SaveableEntity.Spawn`, so the save system and `HavenSync` carry it. |
| Scaffold | `Construction.BeginBuilding` | `Building/Construction.cs:85-95` | If the kind has Build Work, a scaffold goes up and the station and chest stay off. |
| Hammer | `Construction.Update`, `OnSiteStarted` | `Construction.cs:216-262` | **Only the player** holds E at the site. Speed comes from the Construction skill (`StatType.BuildSpeed`, at least 0.25×) and the Raise in a Day technique. The model rises out of the ground, dust flies. No resident ever builds. |
| Finish | `Construction.Finish` | `Construction.cs:275-299` | Pop, dust, "stands!", recipes taught, and a `Built` or `Upgraded` game event for quests. `Construction.Finished` makes `HavenProgress` note the buildings. |
| Upgrade | `ConstructionUpgradeOffer`, `TryOrderUpgrade` | `Building/ConstructionUpgradeOffer.cs:11-53` | From the station's menu or Manage › Upgrade: pay the next `BuildingLevel`, the scaffold returns, the level's "Tier N" parts switch on (`ConstructionLook.cs:118-140`) or generic dressing is added. |
| Use | `CraftingStation`, `StashChest`, `HorseStable`... | | Station windows with timed queues that keep running during raids. Workers fill the queues from the stash (`WorkerProduction.cs:12-28`). |
| Move | `PickBuilding(false)` | `BuildPlacer.cs:390-428` | Pick up any building and set it down elsewhere, free. |
| Breach damage | `BuildingRepairs` | `Settlement/BuildingRepairs.cs` | Broken, never lost (`HealthFloor`). Everything is mended at once at the portal for a share of its cost. |

### 1.2 What a building is

- **One `BuildingDefinition` asset** (`Scripts/Content/BuildingDefinition.cs:96-223`) holds:
  - cost and Build Work;
  - unlock requirements and required rank;
  - Model Scale and Model Height;
  - yard depth and side;
  - Place In Lines and Decoration;
  - the crafting station kind, queue and output;
  - renown, comfort and beds;
  - the recipes it teaches;
  - its upgrades: a list of `BuildingLevel` (`:11-73`) with craft speed, queue, output, storage, raid seconds,
    rumours, foretold events, comfort, extra logs, recipes, requirements, breaches won, beds, purify yield, meal and
    tonic slots, game and light.
- **One prefab** in `Prefabs/Structures` has a root `BuildingObject`, `Construction`, `Damageable` (decorations too), one
  `BoxCollider` and one carving `NavMeshObstacle`, plus a nested model prefab `Art/Haven/Models/<X> Model.prefab`.
- **Sizes are data.** `ModelSize = (ModelScale, ModelScale × ModelHeight, ModelScale)` (`:198`). The kit buildings are
  0.6 across × 1.55 up; the Stable and Wainwright's Yard are 0.8 × 1.4 (the user, 2026-11-14 and 11-15: "far too big,
  hard to read top-down", then "a bit low now"). Root boxes measured from the prefabs:

| Building | Footprint box (m) | Building | Footprint box (m) |
|---|---|---|---|
| Hut | 3.15 × 2.66 | Trading Post | 4.30 × 3.73 |
| Cottage | 4.05 × 3.16 | Storage Shed | 3.00 × 2.15 |
| Workshop | 3.93 × 3.20 | Stable | 9.64 × 7.35 |
| Smithy | 5.39 × 3.85 | Wainwright's Yard | 4.58 × 3.42 |
| Woodcutter's Lodge | 4.89 × 3.92 | Watch Post | 2.20 × 2.04 |
| Hunter's Lodge | 4.54 × 3.72 | Well | 1.90 × 1.89 |
| Tailor's Shop | 5.37 × 3.59 | Dock | 2.05 × 5.85 |
| Kitchen | 2.24 × 2.40 | Skigard piece | 4.12 × 0.30 |
| Sauna | 2.93 × 2.61 | Log Bench | 1.71 × 0.48 |

- **Tiers you can see.** Nine kit buildings carry "Tier N" children made by `Tools/Blender/<name>.py` on
  `building_kit.py`. Construction switches them on per level. Others get generic dressing: a stone plinth and lanterns,
  then banners and a flower box (`ConstructionLook.cs:4-8`).
- **Life on a building.**
  - `HearthSmoke` puts chimney smoke, a warm window light at night and a washing line ("Lived In") on homes and hearths
    (`Environment/HearthSmoke.cs`).
  - `NightFlame` wall torches light one by one at dusk.
  - `GroundWear` lays trampled earth, damp and moss decals round each standing building (`Rendering/GroundWear.cs`).
  - `RainDrips` drips from roof outlines.
  - `SkyOcclusionMap` keeps rain and sky light out from under roofs, and draws again when the building count changes
    (`Rendering/SkyOcclusionMap.cs:92`).
- **Decorations** (the Decorations tab, 2026-11-14): 13 cheap pieces. Only what people sit or linger at gives comfort, 1
  each, counted once per kind.
- **Lines.** Palisade, Skigard, Skigard Gate and Stone Wall are dragged out end to end (`BuildPlacer.Lines.cs`):
  - up to 20 pieces a drag;
  - angles in 15° steps;
  - ends snap to other ends within about 1 m;
  - scrolling while dragging bends the line round the portal (`BuildPlacement.LayArc`), and a ring closes on itself.
  - The gate (`FenceGate.cs`) swings open for the player, residents and companions, and stays shut to creatures. Its sides
    carve the NavMesh; its leaf doesn't.
  - A broken wall stops carving and opens the way (`Construction.cs:361-365`).
- **Construction** is the player's job only. Tor the Carpenter only halves breach repair costs (`JobDefinition`
  `buildingRepairDiscount`).

### 1.3 The catalogue: 44 placeable kinds

From the 45 assets in `Data/Buildings` (the old Furnace is hidden). "Comfort" and "beds" are base + all upgrades.

| Tab | Buildings | Notes |
|---|---|---|
| **Crafting Stations (12)** | Workshop (I–V), Woodcutter's Lodge (I–V), Smithy (I–V), Hunter's Lodge (I–V), Tailor's Shop (I–IV), Kitchen (I–III, comfort 2), Cauldron (I–III), Farm Plot (I–II), Wainwright's Yard (I–II), Dock (I–II), Beehive, Campfire (I–II, comfort 1) | Each opens recipes by tier (`RecipeDefinition.RequiredStationTier`). Most are taught by a rescued resident ([rescue order](RescueOrder.md)). |
| **Walls & Fences (4)** | Skigard and Skigard Gate (from the start), Palisade (Lodge I), Stone Wall (Workshop III) | Line pieces with health. Wall-breaking trolls take a wall at twice the distance of a house (`HomeAssault.cs:11`). |
| **Structures (15)** | Hut (beds 2+1, comfort 1+1), Cottage (beds 4+2, comfort 4+2), Sauna (comfort 3), Well (comfort 1), Storage Shed (I–III), Small Chest (I–II), Stable, Trading Post (rank Village), Watch Post (I–III), Purification Altar (I–III), Runic Ward Totem, Spike Trap, Iron Trap, Torch, Elder Tree | The homes are the only buildings whose value adds up per copy (beds). |
| **Decorations (13)** | Log Bench, Plank Bench, Trestle Table, Lantern Post, Flower Bed, Herb Planter, Haven Banner, Rune Stone (comfort 1 each); Woodpile, Barrels and Crates, Hay Bales, Signpost, Carved Post (0) | Renown 2–5 each, counted at half for repeats. |

**One of a kind or repeatable?** No code limits any kind (no per-kind cap anywhere in `Building`, `Settlement` or
`Content`). The portal is a scene object, not a building, and a ruined Storage Shed starts in the scene. What decides
whether a second copy is worth anything is the counting rules:

| Worth repeating | Barely worth repeating |
|---|---|
| Homes (beds add up), chests and sheds (slots add up), walls, fences and traps (they cover ground), torches and lantern posts (light covers ground), stables (stalls) | Every station: tier, recipes and worker bonus count per kind. A second Smithy is only a second queue the player runs by hand; the smith never goes there and their bonus is applied to both anyway. Watch Post, Trading Post, Sauna, Kitchen, Well and every decoration: comfort and raid bonuses take the best one of the kind. |

### 1.4 What each building does: jobs, production, storage, unlocks

Jobs and their workplaces, from `Data/Settlement/*.asset`:

| Job (resident) | Workplace | Work orders | What it does for the Haven |
|---|---|---|---|
| Alchemist (Ingrid) | Purification Altar | — | Runs purification (`SettlementSettings.purifierJob`) |
| Smith (Bjarne) | Smithy | 3 | Ingots from the stash; gear repairs half price |
| Carpenter (Tor) | Woodcutter's Lodge | 3 | Planks and charcoal; breach repairs half price |
| Warden (Eirik) | Watch Post | — | The ward holds longer; the Watch Post shoots faster in a breach |
| Hunter (Gunnar) | Hunter's Lodge | 2 | Cures leather |
| Farmer / Weaver (Asa) | Farm Plot / Tailor's Shop | 4 / 1 | Crops / linen |
| Cook (Yrsa) | Kitchen | 2 | Bread and stew |
| Tracker (Runa) | none | — | One more rumour per raid |
| Rune Scholar (Liv) | Cauldron | — | Raid runes; carves enchantments past the player's Runecraft |
| Mason (Hild) | Workshop | 2 | Stone blocks |
| Leader (Sigrid) | Haven Banner | — | 30 s more in every raid |
| Fisher | Dock | 3 | Fish |

- **Production.** A worker keeps their station's queue going from the stash until a stock target, during raids too, and
  carries the output back without walking (`WorkerProduction.cs:4-28`). `QueueOf` takes the **first** queue of the
  station type (`:31-46`).
- **Speed.** A station runs at 1 + upgrade speed + 0.5 if any resident holds its job + 0.25 more if that resident has a bed
  + a working horse's bonus (`CraftingStation.cs:163-180`).
- **Storage.** 16 chest blocks of 60; each chest saves its own block, but everything else reads all of `Items`
  (2026-11-15 storage work).
- **Unlocks.** Each building opens the next (`BuildingDefinition.Requires`), and later tiers ask for other buildings
  (`BuildingLevel.requires`): see [base progression](BaseProgression.md).
- **Raids.** Ward and portal tiers open raid tiers. Comfort gives Well Rested on setting out: +15% stamina and +10% health,
  for 60 s per point (`HavenSettings.restedSecondsPerComfort: 60`). The Watch Post gives rumours and teaches the raid
  runes; Workshop III–V make field kits; the Tailor's Shop makes packs.
- **Comfort ceiling.** All building comfort counted once per kind comes to 23. Each resident with a bed adds 1, so 11
  residents housed add 11, for about 34 points: 34 minutes of Well Rested against a 12-minute raid. After one of each kind,
  comfort is full.

### 1.5 Where placement matters today, and where it doesn't

| System | Reads where things stand? | Evidence |
|---|---|---|
| Comfort, Well Rested | **No.** Each kind once, anywhere | `HavenReadiness.cs:27`: `GroupBy(Building).Sum(Max)` + `Housed` |
| Raid bonuses (time, rumours, foretold events, logs) | **No.** Best of a kind | `BuildingLevel` tooltips (`BuildingDefinition.cs:31-41`); `SettlementState.UpgradeBonus` |
| Renown, Haven rank | **No.** A repeat counts half | `HavenRenown.cs:25-50`; `repeatShare 0.5` |
| Beds | **No.** A count, shared in order of arrival | `SettlementState.cs:243-254, 303-316` |
| Where residents sleep | **No.** Two scene spots | `ResidentSpawner.cs:93`; `ResidentNpc.cs:42-54` |
| Where residents work | Barely: the first building of the kind, in its yard | `ResidentNpc.cs:124-150`; `BuildingObject.cs:66-77` |
| Where residents gather and eat | Barely: the first Campfire, else the portal | `ResidentLife.cs:300-310` |
| Worker speed bonus | **No.** Every station of the kind | `CraftingStation.cs:185-200` |
| Horses at work | **Yes.** A horse speeds stations of its job's kind within 14–16 m | `Horses/HorseJobs.cs:17-41`; `HorseSettings.asset` reach 14–16 |
| Light and night prowlers | **Yes.** Prowlers never step into light; torches push the dark back | `VisibilitySource.cs:66-85`; `NightProwlers.cs` |
| Breach targets | **Yes.** Attackers take the nearest standing building, decorations included; wall-breakers prefer walls ×2 | `HomeAssault.cs:53-85`; decorations carry a `Damageable` |
| Breach lanes | **Yes.** They start beyond the outermost building | `WardBreachDirector.cs:652-682` |
| Walls and gates | **Yes.** They carve the NavMesh; a broken one opens the way | `Construction.cs:361-365`; `FenceGate.cs` |
| Watch Post, Ward Totem, Spike Trap | **Yes.** Reach 18 m, radius 7 m, radius 1.3 m | `WatchPostArcher.cs:16`; `WardTotem.cs:14`; `SpikeTrap.cs:14` |
| Ward (safe at night) | **Yes.** Inside 16 m + 6 m per ward level + 3–15 m by rank (49 m at most) | `HomePortal.cs:79-81`; `SettlementSettings.asset:196-228`; `HavenSettings.asset:43-71` |
| Terrain | Only as a refusal: 0.5 m unevenness, no water | `HomePortal.cs:56-57, 185-210` |
| Roads, paths, plots, adjacency | **Do not exist** | `ARCHITECTURE.md:1808` |
| Facing | Only for the yard and the camera | `BuildPlacer.cs:216-236` |

So placement matters in the dark and in a breach. In the daily life of the Haven, where the user spends most of the time
at home, it doesn't matter at all.

### 1.6 Why it feels like "putting down squares"

1. **No rewards for where, only refusals.** The ghost turns red for bad ground and never says "good spot". The player
   learns "anywhere green".
2. **The value is a list, not a place.** Comfort, renown, bonuses and unlocks live in the H window as totals. The town is
   a container for checkmarks.
3. **The people don't make the places matter.** Pillar D says "the recruited smith should inhabit the forge"
   (GDD §2.4, `Docs/GameDesignDocument.md:118`), and the recruitment loop ends in "assign home and job" (§15.2, `:870`). In
   the code nobody has a home, the smith only stands in the first Smithy's yard, and everyone sleeps at one of two posts by
   the portal. The GDD's own note on the Cottage still reads "Renown for now, with no one living in it yet" (§14.2,
   `:852`).
4. **Squares with no inside and nothing between them.** Every building is a carved box. There are no paths, plots, yards
   or neighbourhoods to tie them into a village.
5. **Every spot is equal.** The 55 m level yard round the portal takes everything, and the meadows' shelves (3.6 m) and
   knolls give no reason to pick one spot over another.
6. **Choices don't change the look between buildings.** Upgrades change a building (good), but nothing changes the space
   around it: no clutter appears where people live, no paths form, no gardens grow behind houses.

### 1.7 Constraints any new system must live with

| Constraint | What it means for a new building system |
|---|---|
| **Co-op** (NGO, host-authoritative, 4 players) | Buildings are `SaveableEntity`s. `HavenSync` diffs every saved object's mark every 0.5 s both ways (`Multiplayer/HavenSync.cs`). A guest needs the host's yes to build (`HavenVisit.BuildingNeedsAsking`), and the settlement is mirrored host → guest every 2 s. A new settlement-changing action is paid locally, then goes through `HavenDecisions.Do` with a `[HavenDecision]` method. Raising the wire format needs a fingerprint in `CoopWireTests`. **Derived state computed the same way on every machine (such as yards from synced fences) needs no wire change.** Hundreds of small pieces would multiply the diff and the world transfer a guest asks for on joining. |
| **Controller** | Building with the sticks works (2026-11-12). In the top-down view the right stick moves a spot up to 14 m out at 9 m/s (`ControllerSettings.cs:41-47`), the bumpers turn, and holding the place button while walking draws a line. The action view aims with the crosshair. **The keyboard and mouse layout must stay identical**: new actions get new bindings only. |
| **Save data** | Placed things are spawned `SaveableEntity`s found by prefab id (`ContentDatabase.FindPrefab`), and `Construction` saves its level and work. Residents are `ResidentRecord`s in the settlement save. Anything new needs an id that survives loads and is guarded against the scene-id trap (memory: `RefreshPrefabId` after a prefab edit). |
| **Terrain** | Building is allowed anywhere in Haven Meadows (`buildReach` 165 m without an atlas). A 55 m level yard surrounds the portal; beyond it are 3.6 m shelves, hollows, knolls with flat crowns (sides under 18°), a stream and a fishing pond. Footprints must be flat within 0.5 m. Regenerating the world keeps buildings but doesn't re-ground them. |
| **NavMesh** | No runtime rebake in the Haven. Buildings carve with `NavMeshObstacle` (Carve Only Stationary), and gates don't carve. Memory: never busy-wait `UpdateNavMesh` in an eval (it froze the Editor). Interiors would need wall-only obstacles instead of one box. |
| **Performance** | Woods frames are 13–18 ms, mostly tessellated terrain and tree shadows. Scripts are about 1 ms. Prop prefabs of 20–33 renderers were merged (49k → 31k objects). The nine kit materials are shared. LOD0 must hold across the whole top-down view (`ScreenBudget`, `ModelBudgetTests`). Never lower default quality. |
| **Raids on home** | Breaches, every three successful raids: 2–3 waves down 2–3 lanes, half for the ward and half for the nearest building. Corruption nights are foretold by portal tier and blight, never by buildings or renown (design decision 8.5/8.6). Buildings are never lost. |
| **Camera** | Two styles (`Camera/CameraStyle.cs`): Top Down (V Rising style: 20 m away, pitch 48°, FOV 40, zoom 14–28 m) and Action (Enshrouded style, 2.5–7 m). A Building context can force either. The camera sees only about 1.8 m in under an eave, so props go at the opening. The base sim is run from H. |
| **Building art** | Buildings are kit-built in Blender from a LocalSpawn concept image (the user, 2026-11-10: "go for the kit with the buildings"). `building_kit.py` already has parametric `log_wall`, `footing`, `board_gable`, `sod_roof`, `chimney`, `lantern`, `banner`, `barrel`, `crate` and `anvil`, plus `log_house` in `building_props.py`. Stations and props come from LocalSpawn (generated models the user prefers to scripted ones). Roof grass is grown in Unity (`RoofGrass`). Every new model is checked against the budgets before import: LOD0 holds across the whole top-down view, about 40 px per visible triangle at 2160p (`Editor/Environment/ScreenBudget.cs`), convex hulls of 64 points or fewer, and `ModelBudgetTests`. |
| **Rules** | Never remove functionality: all new work is additive (keep the home spots as a fallback, keep the Tier N parts, keep "beds never gate"). The bulk builders are retired: targeted edits only. Optimise, don't downgrade. Judge base features by what they do for raids (colony direction, 2026-11-02). |

### 1.8 Work in progress to design around

Another agent is building house tiers and comfort upgrades (bed and kitchen icons), residents assigned to huts with stat
or trait effects, an NPC window like the horse window, and timed inventory crafting. At the time of writing none of it was
committed (`git log` up to 6f481dd6 holds only the combat, gore, cinematic and sound fixes; its progress log lists items 3,
4 and 10). This proposal treats that work as the base it stands on:

| Their system | How this proposal uses it, without competing |
|---|---|
| House tiers and comfort upgrades (bed, kitchen) | The house tier stays the authored material ladder. §3.4.2 adds a **surroundings** part (hearth, water, light, bench, noise) and §3.4.3 a **yard** part, both shown beside the tier in the same breakdown. The bed and kitchen icons can be satisfied by the house's own upgrade or by its yard (a Home Yard with a hearth counts as "kitchen"). |
| Residents assigned to huts, with trait effects | Phase 0 makes the assignment **physical**: the assigned house is where they sleep and what they walk home to. Traits can then read place, for example "likes a quiet home" (no forge within 8 m) or "early riser" (home within 30 m of work). |
| NPC window (like the horse window) | Gains a Home section: house, yard, walk to work in metres, quality breakdown, and "Go there". |
| Timed inventory crafting | Extensions and furniture can later be crafted items set down from the bag, as `ItemDefinition.placesInRaid` kits are today. Version 1 keeps them in the build menu, which already works on a pad. |

---

## Part 2: How other games do it (web research)

Sources were fetched by a research agent on 2026-11-15; numbers marked "community" come from wikis or guides, not from the
developers. Fandom and the Manor Lords wiki blocked automated reading, so some figures come from secondary guides.

### 2.1 Key takeaways

1. **The genre leans hybrid.**
   - Hybrids: DQB2 (blueprints plus room recipes), Medieval Dynasty (premade footprint plus per-segment walls and roofs),
     Sons of the Forest (96 blueprints plus freeform logs), Foundation (required monument parts plus free decoration),
     Fallout 4 (snap pieces plus premade shacks).
   - Fully freeform colony building (RimWorld, Going Medieval) sits on a top-down grid with a cutaway camera.
2. **Placement is made to matter cheaply with "near this" rules, not room solvers.** Examples: Valheim's workbench and
   forge extensions, RimWorld's tool cabinet (+6% each, 2 at most, linked by distance, not by room), Enshrouded's station
   beside a craftsperson's station, Frostpunk's heat circles and Banished's walking distance.
3. **Readable room rules beat clever ones.** "Walls, a door, a light and these items make a Kitchen" (DQB2) reads at a
   glance. RimWorld's four-stat impressiveness is deep but hidden behind an overlay. The lesson from Civ 6's critics: a
   placement bonus nobody can see is no bonus.
4. **NPCs using the space is what makes it alive.** Enshrouded's biggest update (Souls of the Frozen Frontier, Nov 2024)
   was largely survivors who wander, sit, take breaks and sleep in a bed you assign. DQB2's residents cook in the kitchen
   you built and sleep in your bedrooms. Gord's villagers recover sanity at the meadery and bath.
5. **Let the player draw shapes and let the game fill them in.** Manor Lords' drawn plots and Foundation's zones and
   desire paths give an organic town from little input. Tiny Glade and Townscaper add doors, windows, arches and clutter on
   their own: "big shapes predictable, small shapes vary" (Stålberg).
6. **Keep the numbers small and capped.** RimWorld rooms give +2 to +8 mood. Going Medieval's private bedroom is +8
   against +4 shared. Valheim's comfort adds 1 minute of Rested per level, and only the best item per category counts.

### 2.2 RimWorld (the user's reference)

- **Room detection.** A room is any space closed off by walls, doors, vents, coolers or natural rock. A **roof is not
  needed** for a room. Roofing only decides "indoors": 25% or more unroofed uses outdoor temperature, and 300 or more
  unroofed tiles is psychologically outdoors.
- **Room roles from furniture.**
  - Every role scores the room's contents, and "the role with the highest score is the one displayed"; a tie goes to the
    role listed first.
  - Scores: workshop 27 per crafting bench (crafting spots don't count), kitchen 28 per stove (any bench with an edible
    recipe), dining room 12 per table, rec room 7 per joy object, lab 60 per research bench.
  - Bedroom, barracks and hospital score about 100,000 when valid, so a bed overrides everything.
- **Impressiveness.**
  - Four stats, each log-compressed: wealth/1500, beauty/3, space/125 and 1 + cleanliness/2.5.
  - Impressiveness = 65 × their average + 35 × the lowest one, so the weakest stat carries about half the weight.
  - Levels run from awful (under 20) to wondrously impressive (240+). Bedrooms, dining rooms and rec rooms give mood from −2
    (awful) to about +8.
- **Rooms change work.**
  - Darkness under 30% light slows work, down to ×0.8.
  - Cleanliness changes surgery success, research speed and the room's food-poisoning roll (only below −2, capped at 5%;
    the cook's own roll is 5% at skill 0 and 0.1% at 9+).
  - Since 1.6.4518, "Some production buildings now have a work speed penalty if they are not in the correct room": stoves,
    research benches, smithies, tailoring benches and others work at about ×0.8 outside a room with the matching role.
  - The tool cabinet links by distance: +6% per cabinet, at most 2 per bench.
- **Traits and DLC.**
  - Greedy: −8 with no bedroom or an awful one, −4 for a decent one. Jealous: −8 when someone has a clearly better bedroom.
  - Royalty titles ask for bedroom size, impressiveness and named furniture: a knight needs 24 tiles, impressiveness 40, a
    double bed, end table and dresser.
  - Ideology altars have their own room demands.
- **UI.** A room-stats toggle in the corner; Alt shows detail.
- **Designer's frame.** Tynan Sylvester calls RimWorld a story generator. Rooms matter because they feed mood, and mood
  feeds drama.

Sources: [Room](https://rimworldwiki.com/wiki/Room) · [Room stats](https://rimworldwiki.com/wiki/Room_stats) ·
[Tool cabinet](https://rimworldwiki.com/wiki/Tool_cabinet) · [Food poisoning](https://rimworldwiki.com/wiki/Food_poisoning) ·
[Greedy](https://rimworldwiki.com/wiki/Greedy) · [Titles](https://rimworldwiki.com/wiki/Titles) ·
[Light](https://www.rimworldwiki.com/wiki/Light) ·
[Sylvester on RimWorld's design](https://gamedeveloper.com/design/video-how-i-rimworld-i-found-success-through-ridiculous-contrarian-design)

### 2.3 Room recipes and room bonuses

**Dragon Quest Builders 2.**
- **The room rule.** A room is 4–150 squares inside walls 2 blocks high with a door. A "Plaza" uses a doormat instead, and
  a room with a scarecrow becomes a Field that farmers tend.
- **Recipes.** Items inside make the type. A Basic Bedroom is 2 beds and a light. A Simple Kitchen is a chest plus cooking
  fires. A Smithy is a forge, chest, hanging bag and pot. About 123 recipes in all.
- **Adjacency.** A bedroom next to a reception room makes a Hotel.
- **Residents use the rooms.** They sleep there, cook whatever is put in the kitchen chest, serve drinks and bathe.
- **Progression.** Happy residents drop Gratitude, which raises the base level and unlocks recipes.
- **Blueprints.** Residents build from the player's blueprints with materials taken from a chest.

Sources: [room recipes](https://dragon-quest.org/wiki/List_of_room_recipes_in_Dragon_Quest_Builders_2) ·
[Gematsu](https://gematsu.com/2018/12/dragon-quest-builders-2-details-endless-battle-island-setting) ·
[Gratitude](https://twinfinite.net/guides/dragon-quest-builders-2-gratitude-fast-get-how/)

**V Rising** (the camera Safe Haven's top-down view is modelled on).
- **Rooms by floor.** A walled room's floor type sets its specialty: Workshop, Forge, Tailor, Alchemy, Jewelry, Library,
  Prison or Crypt.
- **Bonuses.** Stations inside get about +25% speed and/or −25% cost. One example: Forge floors make a copper ingot cost 15
  ore, not 20.
- **Roofs.** Since Gloomrot, enclosing a room raises its roof automatically, and roofs melt away when the player walks in so
  the top-down camera keeps them in view.
- **Castle Heart.** Its tier caps floor tiles (about 40 / 100 / 180 / 260 / 420; sources differ).

Sources: [Forge room](https://www.gamerguides.com/v-rising/guide/castle/rooms/how-to-create-a-forge-room) ·
[Castle Heart](https://www.gamerguides.com/v-rising/database/structures/castle/castle-heart) ·
[Gloomrot notes](https://primagames.com/tips/v-rising-secrets-of-gloomrot-update-full-patch-notes-listed) ·
[roofs while inside](https://www.gamespot.com/articles/how-to-build-a-roof-in-v-rising/1100-6503780/)

**Valheim.**
- **Comfort.** Comfort counts furniture within about 10 m, the best item per category only (fire, bed, seat, rug...).
  Rested lasts 7 min + 1 min per comfort level and gives +50% health regeneration and +100% stamina regeneration.
- **Shelter** needs a roof, mostly closed walls and a fire.
- **Workbench.** It needs a roof and claims a build radius of about 20 m. It reaches level 5 through extensions placed
  near it: chopping block, tanning rack, adze, tool shelf.
- **Forge.** It reaches level 7 through six extensions: cooler, anvils, smith's anvil, toolrack, bellows, grinding wheel.
- **Structural integrity.** Blue (grounded) through red. Community figures: wood loses 1/8 per piece up and 1/5 out; stone
  holds no overhangs.
- **Controller.** Full support arrived in 0.207.15 (2022).

Sources: [comfort](https://www.corrosionhour.com/valheim-how-does-comfort-work/) ·
[stability](https://gamever.io/knowledge-base/why-your-valheim-build-collapsed-stability-support-and-the-numbers-behind-it) ·
[forge levels](https://www.pcgamer.com/how-to-make-valheim-forge-level-upgrade-bellows) ·
[controller patch](https://wccftech.com/valheim-update-0-207-15-public-test-frost-caves-controller-steam-deck/)

**Enshrouded.**
- **Building.** Voxel building inside a Flame Altar volume, which grows from 40 to 160 m per side.
- **Comfort.** Furniture gives +4 to +10 comfort each, and each level adds about a minute of Rested.
- **Survivors.** 11 craftspeople plus assistants and villagers, rescued and placed in a base. Since Nov 2024 they wander,
  chat, take breaks and sleep in a bed you assign, in a spot checked for a roof and walls; without one they sleep on the
  floor. A roaming range can be set per NPC, and patch 5 had them sit on chairs.
- **Station upgrades.** Placing a second station beside a craftsperson's opens recipes: a Forge by the Blacksmith opens 46.

Sources: [Flame Altar](https://enshrouded.wiki.gg/wiki/Flame_Altar) · [Comfort](https://enshrouded.wiki.gg/wiki/Comfort) ·
[Souls of the Frozen Frontier](https://enshrouded.com/en-US/news/enshrouded-souls-of-the-frozen-frontier-update) ·
[patch 5](https://enshrouded.com/en-US/news/changelog-for-patch-5-v0741) · [upgrades](https://pindrop.gg/enshrouded/crafting/upgrades)

### 2.4 Colony and town builders

**Going Medieval.**
- **Rooms.** Four walls, a door and a roof make a room, and furniture sets its type. An overlay (V) shows rooms.
- **Room types.**
  - The Great Hall needs 50+ tiles, a table with 6 seats, 8 wall decorations and 4 torches.
  - The Infirmary needs an infirmary bed and 2 shelves, and no ordinary beds.
- **Mood.** Impressiveness counts for bedrooms, halls and shrines. Beauty is averaged over a 19-tile radius. A private
  bedroom is +8 against +4 shared (a 2021 player report).
- **Building.** Multi-level, with dug cellars kept at about 2°C.

Sources: [Great Hall](https://www.escapistmagazine.com/going-medieval-great-hall-guide/) ·
[rooms](https://foxyvoxel.io/?p=512) · [bedroom mood](https://steamcommunity.com/app/1029780/discussions/0/5296777170367645509) ·
[cellars](https://gameplay.tips/guides/going-medieval-guide-to-creating-cold-cellars.html)

**Manor Lords.**
- **Plots.** The player draws a burgage plot on gridless land along a road, and its width and depth decide how many
  houses go in and how much backyard is left. Plots snap to roads and buildings, so housing areas look natural.
- **Backyard extensions.** A vegetable garden or orchard yields more on a bigger plot; chickens and goats don't scale.
  Level-2 extensions turn the family into artisans.
- **Upgrades.** Houses go up a level when a church, well, market stalls and goods are near.

Sources: [burgage plots](https://www.pcgamesn.com/manor-lords/burgage-plots-upgrade) ·
[wiki](https://wiki.hoodedhorse.com/Manor_Lords/Burgage_Plot) ·
[extensions](https://videogamer.com/wiki/Manor-Lords/Extensions)

**Foundation.**
- **Zones and paths.** The player paints zones and the AI places houses inside them. Roads form where villagers walk and
  fade when unused.
- **Monuments.** Each is built from required modules (core, door, bell tower) plus free decorative parts, and decorations
  add Splendour for the matching estate.

Sources: [buildings](https://wiki.polymorph.games/foundation/Buildings) ·
[monuments](https://polymorph.games/foundation/modding/monuments) ·
[paths from feet](https://www.pcgamer.com/you-dont-have-to-place-roads-in-this-medieval-city-builder-thanks-to-your-citizens-feet)

**Medieval Dynasty, Bellwright, Gord, Banished, Frostpunk.**
- **Medieval Dynasty** places a premade footprint, then the player hammers in each wall and roof segment, choosing the
  material (wattle, wood or stone; straw, wood or tile) and window or door. It is a premade shell with modular skins.
- **Bellwright's** stations need staff, and goods need a stockpile.
- **Gord's** palisade grows only through upgrades. Villagers lose sanity outside the light and recover it at the Meadery
  or bath. Reviewers call its building "fairly basic".
- **Banished** shows work-radius circles. Its developer says townsfolk choose homes by walking distance, so distance is the
  cost of placement.
- **Frostpunk's** buildings work only inside the generator's heat circles, and cold at home and work makes people sick. Its
  developers: "circles are practically sacred in Frostpunk's design language."

Sources: [Medieval Dynasty](https://www.gamepressure.com/medieval-dynasty/building/zedac6) ·
[Bellwright](https://pindrop.gg/bellwright/guides/beginner-tips) · [Gord](https://en.wikipedia.org/wiki/Gord_(video_game)) ·
[Gord review](https://www.shacknews.com/article/136367/gord-is-the-darkest-dungeon-of-colony-sims) ·
[Banished dev](https://shiningrocksoftware.com/?p=1971) · [Frostpunk heat](https://www.gamepressure.com/frostpunk/heat/z8a12d)

### 2.5 Tiny Glade and Townscaper: little input, much result

- **Townscaper** (Oskar Stålberg) fits hand-made modules on an irregular grid by priority rules. Windows, chimneys,
  stairs and gardens appear on their own. "I want the user to feel like they're in control of the large shapes... larger
  shapes to feel very predictable, but the smaller shapes are allowed to vary quite a bit."
- **Tiny Glade** (Pounce Light) began as a procedural wall generator: "It's just tangibly pleasant to watch something being
  built up in front of your eyes." Dragging a path into a wall makes a door there, arches form by themselves, and clutter
  (washing lines, pots) gathers round doors. "The same feeling that the game pays attention to you and cares about what
  you're doing."

Sources: [How Townscaper works](https://www.gamedeveloper.com/game-platforms/how-townscaper-works-a-story-four-games-in-the-making) ·
[Tiny Glade devs](https://80.lv/articles/exclusive-tiny-glade-developers-discuss-bevy-proceduralism-publishers-cozy-games) ·
[Tiny Glade review](https://www.pcgamer.com/games/city-builder/tiny-glade-review)

### 2.6 Survival builders with snapping and premades

- **Fallout 4.**
  - Each settler's happiness comes from a bed (10, +10 covered), food, water and defence (20 each). Shortfalls cap it:
    defence below population caps everyone at 60.
  - Snap pieces sit beside premade shacks, and a Size bar caps how much each settlement holds.
- **Return to Moria.** Structural support (pillars for upper floors), bedrolls within a hearth's range, and Well-Rested
  from a good bed.
- **Palworld.**
  - The Palbox claims about a 35 m radius, and inside it nothing decays.
  - Working Pals are capped by base level; they pick work by suitability and lose sanity working, recovering at hot springs.
- **RuneScape: Dragonwilds.**
  - Snap pieces with three snap modes cycled on Y. Stability depends on material: height 24/36/48 m for ash, oak and stone.
  - Ghost-mode planning without materials; building takes materials from chests within 40 m.
  - A fully enclosed base gives the Cosiness buff.
  - On a pad, the build menu is on D-pad down.
- **Sons of the Forest.** Two modes: 96 premade blueprints with logs cut to fit, and freeform logs. The companion Kelvin
  finishes every structure in the area on command.

Sources: [Fallout 4 happiness](https://fallout.wiki/wiki/Happiness_(Fallout_4)) ·
[Moria](https://techraptor.net/gaming/guides/lord-of-rings-return-to-moria-building-guide) ·
[Palworld bases](https://palworld.wiki.gg/wiki/Bases) · [Dragonwilds building](https://dragonwilds.runescape.wiki/w/Building) ·
[Kelvin](https://pindrop.gg/sons-of-the-forest/guides/how-to-command-kelvin)

### 2.7 Player psychology and gamepad UX

- **Not everyone wants to build freely.** Quantic Foundry's "Design" motivation (expression, customisation) is its own
  player segment. Freeform building delights that group and is a chore for others. Constraints that create choices ("this
  forge makes the house next to it noisy") are what the rest enjoy.
- **Meaning comes from consequence and use.** RimWorld's rooms matter because colonists feel them. Enshrouded's and DQB2's
  bases feel alive because people use them. Placing a building no one walks to is placing a square.
- **Readability.** PC Gamer on Civ 6's adjacency: "incredibly important, poorly expressed". The bonus must show on the
  ghost before placing.
- **Pad building.**
  - Frostpunk on consoles uses a context radial and a command radial: "One button to always close a window. One button to
    always confirm."
  - Frostpunk 2 pauses time in its radials.
  - Fortnite's Builder Pro gives each piece its own button instead of cycling.
  - Dragonwilds and Enshrouded cycle snap modes on one button and keep a separate bindings tab for building.

  Freeform modular building on a pad needs piece cycling, snap-point cycling and fine rotation. Placing whole buildings
  and drawing lines needs none of that, and Safe Haven already does both.

Sources: [Gamer Motivation Model](https://quanticfoundry.com/wp-content/uploads/2019/04/Gamer-Motivation-Model-Reference.pdf) ·
[Civ 6 critique](https://www.pcgamer.com/14-tiny-things-that-drive-me-crazy-about-civ-6) ·
[Frostpunk on consoles](https://gamedeveloper.com/design/the-simple-most-difficult-challenge-of-bringing-frostpunk-to-consoles) ·
[Frostpunk 2 gamepad](https://news.xbox.com/en-us/2025/09/18/adapting-frostpunk-2s-depth-to-a-gamepad/)

### 2.8 What applies to Safe Haven specifically

| Lesson | Source games | For Safe Haven |
|---|---|---|
| A room doesn't need a roof | RimWorld, DQB2's Plaza and Field | A **fenced yard** can be a room. The skigard already draws closed rings. |
| Items inside decide the role; highest score wins | RimWorld, DQB2, Going Medieval | Yard roles from contents, with a tie-break order in data. |
| "Near this" extensions | Valheim, Enshrouded, RimWorld tool cabinet, Safe Haven's own `HorseJobs` | Smithy extensions within a few metres; slots as choices. |
| Bed as a place, not a number | Enshrouded 2024, DQB2, Fallout 4 | Residents sleep in an assigned house. |
| Circles you can see | Frostpunk, Banished, Palworld | Draw the hearth, well, light and defence radii on the ghost. |
| Draw shapes, the game fills them | Manor Lords, Foundation, Tiny Glade | Yards get clutter by role; footpaths wear in. |
| Small, capped numbers; weakest stat matters | RimWorld, Valheim, Going Medieval | Home quality 0–5, counted once per kind of role; the weakest part shows first. |
| Residents build | DQB2, Sons of the Forest | Carpenter and Mason hammer scaffolds while the player raids. |
| Freeform walls need a grid, a cutaway and a structural model | RimWorld, Going Medieval, V Rising, Valheim | Expensive in 3D on terraces with co-op and a raid-paced game: later, if at all. |

---

## Part 3: Options, recommendation and plan

### 3.1 The options

**(A) Premade buildings made meaningful.** Keep every building as it is. Make placement pay:

- residents live in and walk between buildings;
- radius effects (hearth, water, light, noise);
- extensions placed beside stations;
- paths;
- visible upgrades (they exist);
- a readout on the ghost.

| Advantages | Drawbacks |
|---|---|
| Reuses everything: `BuildingDefinition`, `Construction`, the ghost, lines, `HavenSync`, saves, the pad controls and the kit art. | The town is still a set of premade squares. Expression is in arrangement only. |
| No new art besides extension props, which LocalSpawn makes well. | No rooms or furniture, so it doesn't answer the user's room wish. |
| Fits the camera, NavMesh, raids and co-op as they are. | Adjacency bonuses can feel like a spreadsheet puzzle unless shown clearly. |
| Can be built in small steps, each testable on its own. | |

**(B) Fully modular building.** Foundations, walls, doors, windows and roofs placed piece by piece, with RimWorld or
DQB2 rooms from furniture.

| Advantages | Drawbacks |
|---|---|
| The most expression; the strongest "I made this" for builders. | A new kit of about 40–60 pieces (wall, door, window, corner, T, end, floor, foundation and stilts for the 3.6 m shelves, stairs, roof slope, ridge, hip, valley, gable end), in at least 2–3 materials and the tiers, with LODs, sod roofs and roof grass. LocalSpawn can't make exact tiling pieces, so all of it must be scripted Blender. |
| Rooms emerge: a kitchen is wherever the stove is. | A 3D room solver on uneven ground, structural rules, a roof cutaway for the 48° camera and interior NavMesh. |
| Fits the Valheim and Enshrouded audience. | Hundreds of `SaveableEntity` pieces through `HavenSync`'s 0.5 s diff and the join transfer, plus the save size. |
| | Breaches: `HomeAssault` would chew through single wall pieces, and repairs per piece would need rules. |
| | `RainDrips`, `GroundWear`, `HearthSmoke`, `SkyOcclusionMap` and the minimap all assume one building is one `BuildingObject`. |
| | The tier I–V ladder (`BuildingLevel`) would need a new model for what a "Smithy" is. |
| | Pad UX needs piece and snap cycling. |
| | It competes with raid time: home is the breather between 12-minute raids, and the user wants a Gord-style base run from H. |

**(C) Hybrid variants.**

- **C1. Premade production and landmark buildings, modular houses.** Stations, the Altar, Watch Post, Stable and Trading
  Post stay premade and are run through their windows; homes are built from walls and roofs.
  - For: expression where it is personal (homes), production stays balanced.
  - Against: two building systems; most of B's cost (kit, roofs, cutaway, solver) for the houses alone; premade and
    modular buildings side by side may not match.
- **C2. Premade shells with furnishable interiors.** Kit-built shells in a few sizes with an open interior and a roof that
  fades; the player places furniture inside, and the furniture sets the room's role and quality.
  - For: rooms are closed by construction, so no solver is needed; the exterior keeps the authored look; furniture is
    props (LocalSpawn).
  - Against: the user just made buildings 0.6 wide to read from above. A Hut's 3.15 × 2.66 m box leaves about 2.4 × 2.0 m
    inside, and a bed is 2.0 × 0.9 m. Interiors need about 0.85–1.0 scale. The camera sees only about 1.8 m under an eave,
    so a roof fade and interior lighting are required. Walls need obstacles of their own instead of the one carving box.
    Residents need sit, sleep and cook animations indoors.
- **C3. Manor Lords plots.** The player draws a plot, and the game fills it with a house and backyard extensions.
  - For: an organic town from little input; easy on a pad.
  - Against: built for hundreds of families. Safe Haven has about 11 named residents (13 jobs), so plots would sit empty.
    It needs a plot-filling solver on uneven ground.
- **C4. Yards as rooms (recommended, with A).** A closed ring of fence pieces with a gate is a yard. What stands inside
  decides its role and quality, and the role gives small, capped bonuses and draws residents.
  - For: uses the line drawing, ring closing and gates that exist. No roof, no interior, no cutaway, nothing hidden from
    the top-down camera. Fences are already breach defences, so a well-fenced village is also a defended one. Derived from
    synced pieces, so co-op needs nothing new. It is the Norwegian farmstead (*tun*, ringed by *skigard*), which is the
    game's own look.
  - Against: rooms without roofs are a looser fantasy than RimWorld's indoor rooms, and the user may still want interiors
    later (that is C2, kept as a later phase). A ring detector and a role table must be written and tuned.

### 3.2 Comparison

Rough costs for this codebase. Effort: S = days, M = 1–2 weeks, L = 3–5 weeks, XL = 2+ months. "Risk to today" is the
chance of breaking what works.

| | A: premade made meaningful | B: fully modular | C1: modular houses | C2: furnished shells | C3: plots | **C4 + A: yards as rooms** |
|---|---|---|---|---|---|---|
| Development effort | M | XL | L–XL | L | L | **M–L** |
| Art cost | Low: 6–15 extension props (LocalSpawn) | Very high: 40–60 kit pieces × materials × tiers, LODs | High: house kit, roofs | Medium–high: hollow shells, 15–30 furniture props | Medium: plot dressing, extensions | **Low–medium: extension props, yard clutter, maybe hedge and dry-stone fences** |
| Co-op sync | Unchanged (assignments are Haven decisions) | Heavy: hundreds of entities | Heavy for houses | Moderate: furniture entities | Moderate: plots are entities | **Unchanged: yards derived on each machine** |
| Controller UX | Unchanged | Hard: piece and snap cycling | Hard for houses | Moderate: interior grid with the pad spot | Easy: draw a polygon | **Unchanged: lines already work** |
| NavMesh | Unchanged | Hard: per-piece carving, doors, interiors | Hard for houses | Walls-only obstacles, interior agents | Unchanged | **Unchanged: fences carve, gates don't** |
| Performance | Negligible | Many renderers; needs merging | Many for houses | Moderate: interior props, roof fade | Low | **Negligible: event-driven ring search over tens of pieces** |
| Save data | Home id on the resident record | A new piece graph | A new piece graph for houses | Furniture entities | Plot entities | **Home id; yards not saved** |
| Raids on home | Unchanged | Rework targets, repairs, integrity | Rework for houses | Unchanged | Unchanged | **Stronger: fenced yards are defence** |
| Terraces and slopes | Unchanged (0.5 m rule) | Foundations and stilts needed | Same for houses | Unchanged | Plot solver on slopes | **Fences follow the ground; shelves make natural yards** |
| Camera fit (48° top-down) | Good | Needs cutaway | Needs cutaway | Needs roof fade | Good | **Best: everything outdoors** |
| "Small living town" feel | Medium–high (people make it) | High for builders, low for managers | Medium–high | High indoors | Medium (too few people) | **High: farmsteads, people and roles you can see** |
| Expression | Arrangement | Full | Homes | Interiors | Plot shapes | **Arrangement plus yard shapes and contents** |
| Answers "furniture decides the room" | No | Yes | Yes (homes) | Yes | Partly (extensions) | **Yes, outdoors; indoors later via C2** |
| Risk to today | Low | Very high | High | Medium (scale change) | Medium | **Low** |

### 3.3 Recommendation for Safe Haven

**Build A and C4 together, in small steps: premade buildings that people live and work in, set in yards the player draws,
with extensions and furnishings deciding what each yard is. Keep C2 (open, furnished house interiors) as a decided-later
phase, and don't build B.**

**Why this fits Safe Haven:**

1. **It fixes the actual cause.** The cause is not that buildings are premade. Manor Lords, Frostpunk, Banished and Gord
   all use premade buildings and their towns feel alive. The cause is that nothing reads position and nobody uses the
   places (§1.5). Making people live, work and gather by place, and making yards out of fences, attacks both causes.
2. **It suits the camera.** The Haven is viewed from 20 m at 48°. RimWorld and V Rising need cutaways for indoor rooms.
   Yards are rooms the camera already sees whole.
3. **It suits the pace and the user's direction.** The Haven is a base sim run from H, and home is the breather between
   raids. Drawing a fence ring round a forge and its bellows takes seconds. Laying 60 wall pieces doesn't.
4. **It suits the art pipeline.** The user chose kit-built buildings and generated props. The recommendation needs props
   (extensions, yard clutter), which LocalSpawn makes well, and no exact-fit modular kit, which it can't.
5. **It strengthens the home-defence pillar instead of fighting it.** Fences already carve the NavMesh, and gates already
   keep creatures out. A village of fenced yards is a defended village. Breach lanes, wall-breakers and traps start to
   interact with how the player laid out the town.
6. **It costs co-op and saves almost nothing.** Yards are derived from synced fences on each machine. Home assignment is
   one id on the resident record, sent as a Haven decision.
7. **It answers the user's room question** with the RimWorld and DQB2 mechanic (contents decide the role, highest score
   wins, one quality number), in the place where it reads best. Indoor rooms stay possible later, on the same role table.

**What it reuses:**

| Reused | From |
|---|---|
| Ghost, grid, snapping, yards, line drawing, ring closing, gates | `BuildPlacer`, `BuildPlacement.LayArc`, `FenceGate` |
| Building definitions, upgrades, Tier N parts, scaffolds | `BuildingDefinition`, `Construction`, `ConstructionLook` |
| Proximity precedent | `HorseJobs.NearestStation` (stations within reach) |
| Radii | `VisibilitySource`, `WatchPostArcher.reach`, `WardTotem.radius` |
| Life on buildings | `HearthSmoke` (light, smoke, washing line), `NightFlame`, `GroundWear`, `ResidentLantern` |
| Residents' days | `ResidentRoutine` (pure, tested), `ResidentLife`, `ResidentNpc.TryGetWorkplace` |
| Stable stall pattern (agent off while inside) | `HorseStable`, `HorseMotion.Stall` |
| Co-op | `HavenSync`, `HavenDecisions` |
| In-progress | House tiers and comfort; hut assignment and traits; the NPC window; timed crafting (§1.8) |

**Risks and how to contain them:**

| Risk | Containment |
|---|---|
| Adjacency turns into a spreadsheet puzzle | Few effects (at most 5 per home), shown on the ghost and in the NPC window, no hidden math. Bonuses only, plus at most one penalty (noise), which is the user's call (Q3). |
| Yard detection fails on odd shapes | Rings of line pieces only (fences, palisades, stone walls), endpoints joined by the existing snap. Size limits of 12–900 m². Tests on closed, open, nested and touching rings. |
| Too many bonuses inflate power | Yard roles count once per kind of role, as comfort does today. Effects stay small (§3.4). The user approves numbers. |
| Residents walking home at night get caught outside the light | Breaches already send them to the portal. A lantern after dark exists (`ResidentLantern`). Corruption nights keep to the dark, so homes need light near them: that is a placement choice, which is the point. |
| Removing the old behaviour | Nothing is removed. The two home spots stay for anyone without a house, the first-instance lookups stay as fallbacks, and the Tier N parts stay. |
| Clashing with the other agent's work | Phase 0 waits for or builds on their assignment data; this doc's names are proposals they can rename. |

### 3.4 Design details

All numbers below are first guesses for the user to judge. Every value lives in data (`SettlementSettings`,
`HavenSettings` or a new `YardRoleDefinition`), never in code.

#### 3.4.1 Homes people live in (Phase 0)

- **Bed spots on houses.** Each home prefab gets child transforms "Bed 1".."Bed N" (N = base beds + upgrade beds: Hut 2–3,
  Cottage 4–6), placed just inside the door, plus a "Door" point at the yard edge (`BuildingObject.TryGetYard` already
  gives it).
- **Who sleeps where.** By default the nearest free bed to the resident's workplace; the player overrides it in the NPC
  window (the other agent's assignment). A Haven decision in co-op.
- **Night.**
  - `ResidentActivity.Sleep` walks to the door; on arrival the agent is switched off and the body hidden, the way a
    stabled horse's agent is (`HorseMotion.Stall`).
  - The house's `HearthSmoke` warm light turns on only while someone is home.
  - At wake they come out of the door.
  - No interior and no NavMesh change are needed.
- **Fallback.** No assigned house means today's home spots (unchanged).
- **Work.** `TryGetWorkplace` picks the workplace instance nearest the resident's home (or one the player chose), not
  `BuildingObject.Find`.
  - Two Smithies now mean two smiths can work in two places.
  - `CraftingStation.Worker` should then give the +50% only to the instance the worker stands at.
  - `WorkerProduction.QueueOf` should use that instance's queue.
- **Gathering and meals.**
  - The Fire activity goes to the fire nearest home, or to the Feast Green if one exists (§3.4.4).
  - The Meal activity goes to the nearest Trestle Table or bench within 20 m of home, else the fire.
- **Readout.** The NPC window shows "Lives in the Cottage by the well · 34 m to the Smithy". The ghost of a home shows who
  would move in.

#### 3.4.2 A home's surroundings (Phase 1)

A home's quality is its tier (the other agent's ladder) plus a surroundings part, from 0 to 5, shown as a short list on
the ghost and in the NPC window:

| Effect | Rule (first guess) | Why |
|---|---|---|
| Warm | A Campfire, Kitchen or Sauna within 12 m | Winter already sends residents to fires (`ResidentRoutine.InTheCold`); a warm home keeps afternoon work going in hard cold. |
| Water close | A Well within 20 m | The Well has no reason to stand anywhere today. |
| Lit lane | A Lantern Post, Torch or lit building within 10 m of the door | Prowlers avoid light (`VisibilitySource.IsLit`), so residents walking home at night are safe. |
| A place to sit | A bench within 6 m of the door | Benches today give comfort once, anywhere. |
| Near work | Workplace within 30 m | A short walk; a trait can care. |
| Noisy (optional penalty, Q3) | Smithy, Hunter's Lodge or Sauna's smoke within 8 m | Creates the zoning choice: a craft quarter and a quiet quarter. |

What quality does: a resident in a quality-3+ home gets the bed bonus and, say, a further +10% at work, or whatever the
other agent's trait system defines. Total home comfort for Well Rested keeps its once-per-kind rule, adding at most "best
home quality" once, so raid power can't run away.

#### 3.4.3 Station extensions (Phase 1)

Like Valheim's forge and RimWorld's tool cabinet, and like `HorseJobs` in this codebase:

- Placeable props that count when they stand within 6 m of a production building, or in the same yard.
- Each building tier gives slots: 1 at I, +1 at II and IV, so 3 at most. There are more extensions than slots, so the
  player chooses a specialisation.
- Speed must never be the only reason (`HavenProgressionTests.NoUpgrade_IsOnlySpeed`), so each extension opens or changes
  something.

Examples:

| Building | Extensions (pick by slots) |
|---|---|
| Smithy | Quench Tub (gear repairs −20%), Bellows (smelting +25%, and ingots don't wait on charcoal below 5), Grindstone (Fine quality chance up), Tool Rack (+1 queue) |
| Woodcutter's Lodge | Chopping Block (+1 log per cut at home before tier III), Log Rack (holds charcoal stock for the smith), Sawhorse (planks cost 1 oak less per 4) |
| Kitchen | Smoke Rack (meals last 20% longer), Larder (+1 output), Herb Garden (a herb in each meal) |
| Hunter's Lodge | Drying Rack, Tanning Vat, Antler Wall (trophy renown) |
| Workshop | Vice Bench, Stone Saw (blocks cheaper), Pattern Board |

- **Art.** One LocalSpawn prop each (6–15 in all); several exist as kit parts (`anvil`, `barrel`, `crate`, `round_forge`).
- **Data.** A `BuildingDefinition` with a new "Extends" field (the building kind and slot).
- **Existing parts.** The Tier N parts are kept. Extensions add to them, never replace them.

#### 3.4.4 Yards as rooms (Phase 2)

- **What counts.**
  - Line pieces whose definition is marked "Encloses" (Skigard, Skigard Gate, Palisade, Stone Wall; hedges and dry-stone
    walls later) form a graph.
  - Endpoints within 0.3 m are one node. The existing end-snap already joins them within about 1 m.
  - Each closed face of that graph is a yard if it has at least one gate (DQB2's door rule, Q2) and an area of 12–900 m².
  - Optionally a building's wall can close a ring (Q7). Start with fences only.
- **When it is worked out.** On placing, moving, breaking or removing a line piece or building, through
  `Construction.Finished`, the placer's confirm and `Damageable` breaks. Never per frame.
  - The graph is tens of pieces: a planar face walk plus point-in-polygon for contents is microseconds.
  - Each machine works it out from synced pieces, so it isn't saved and needs no wire change.
  - A broken fence opens the ring until mended, so a breach visibly costs the yard its role.
- **Roles** (a new `YardRoleDefinition`, highest score wins, ties by list order, as RimWorld does):

| Role | Recipe (first guess) | What it gives (once per role kind) | Auto-dressing |
|---|---|---|---|
| Home Yard (*tun*) | A home + a hearth or bench | Residents of homes inside: +1 home quality; satisfies the "kitchen" comfort icon if a hearth is inside | Washing line, firewood stack, a goat or hens |
| Work Yard | A production building + 1 extension | Its extensions count without the 6 m rule; +1 extension slot for one station inside | Sawdust, offcuts, a cart |
| Kitchen Garden | 2 Herb Planters or Farm Plots + a Well or water within 8 m | Farm plots inside: +1 harvest | Scarecrow, baskets |
| Feast Green | Campfire + 2 benches + Trestle Table | Becomes the residents' gathering place and the festival place (`Festivals.Place`); +2 comfort | Bunting, a mead barrel |
| Paddock | A Stable + feed trough + 60 m² | Horses graze in it by day; bond grows faster | Hay, hoof-trodden ground |
| Shrine Yard (optional) | Rune Stone + Carved Post + Flower Bed | Blight eases faster at home | Candles, offerings |

- **Quality.** One number per yard, 0–5:
  - +1 per distinct dressing kind inside (bench, flower bed, lantern, signpost, carved post, woodpile, barrels), up to 3;
  - +1 if lit at night, +1 if a path reaches its gate;
  - the lowest of "lit" and "dressed" shows as the hint for what to add next (RimWorld's weakest-stat idea, kept readable).
- **Overlay.** A toggle in build mode (a new binding; the keyboard layout stays as it is) tints each yard by role and
  prints its name and quality, like Going Medieval's room overlay and RimWorld's room stats.
- **Defence comes free.** Yards are rings of walls with gates creatures must break (`FenceGate`). Breach lanes and
  wall-breaking trolls already react to them.

#### 3.4.5 Paths (Phase 3)

- **Drawn paths.** A "Path" line kind (packed earth, then plank walk, then stone). Each piece is a URP decal strip like
  `GroundWear`'s, so nothing writes into the TerrainData asset.
  - Walking on one is faster: player +10% at home only, residents and carts too.
  - A path to a yard's gate counts toward the yard's quality.
- **Footpaths that wear in.** A low-resolution wear map of where residents walk, drawn as decals or a projected texture
  near the camera. This fills the open TODO at `ARCHITECTURE.md:1808`. Foundation and Tiny Glade show how much this does
  for "it grew by itself".
- **Resident routes.** Residents prefer paths only if that is cheap. Carving can't set NavMesh area costs at runtime, so v1
  leaves routing alone and lets the wear map follow where they actually go.

#### 3.4.6 Later, by decision: open homes (C2) and drawn log rooms (B-lite)

- **Open homes (C2).** Homes get hollow kit interiors, a roof that dithers out when the player is near or furnishing (V
  Rising's approach), wall-only obstacles and a few furniture anchors or a 0.25 m interior grid. Furniture decides the
  room's role with the same role table as yards (Bedroom, Kitchen, Workshop corner, Storeroom).
  - Needs a decision on house scale: interiors need about 0.85–1.0 across against the 0.6 chosen for readability.
  - Needs sit, sleep and cook clips for residents.
- **Drawn log rooms.** Drag a rectangle; log walls rise on footings, with door and window pieces in the line, and a sod
  roof from a few width bands (3–6 m) stretched along the length. This is the existing lines mode plus a roof generator.
  Only if open homes are loved.

### 3.5 What is missing for a living town, beyond the building system

| Missing | Today | Proposal |
|---|---|---|
| People using places | Work in the first building's yard; sleep at two posts; gather at the first fire | §3.4.1 |
| Occupancy you can see | Smoke and window light whenever a building stands (`HearthSmoke`) | Light when someone is home; smoke when a hearth is lit or someone is home |
| Paths wearing in | Not done (`ARCHITECTURE.md:1808`) | §3.4.5 |
| A village centre | Gathering at the first Campfire; festivals at a spawned bonfire (`Festivals`) | Feast Green as the centre; festivals held there |
| Market days | The Trading Post has stock after raids | A trader's cart on the road every few days, stalls on the green |
| Stores you can see | The stash is numbers in a window | Woodpile, ore heap and barrels beside the stores grow with what is in them |
| Residents building | Only the player hammers | Carpenter and Mason hammer placed scaffolds while the player raids (DQB2, Kelvin); the player still can |
| Personal touches | Generic houses | The resident's trade sign or tool by their door; their name on the house's tooltip |
| A look that follows choices | Tier N parts per building | Yard auto-dressing by role, rank dressing (banners at Village, a carved gate at Haven), seasonal garlands |
| Animals in town | Horses (stable, paddock) | Hens and goats in Home Yards, a dog at the Hunter's Lodge |
| Morale view | Planned after the playtest (B7) | Home quality and yard roles are its natural inputs |

### 3.6 Ranked list (impact vs effort)

| # | Item | Impact | Effort | Depends on |
|---|---|---|---|---|
| 1 | Homes people live in (sleep in the assigned house, occupancy light, nearest workplace instance, gathering by home) | Very high | M | The other agent's assignment |
| 2 | "Here" readout and radii on the ghost | High | S–M | — |
| 3 | Home surroundings (warm, water, lit lane, bench, near work, optional noise) into home quality | High | M | 1, house tiers |
| 4 | Yards as rooms (detection, 5 roles, overlay, auto-dressing) | Very high | L | 2 |
| 5 | Station extensions with slots | High | M + 6–15 props | — |
| 6 | Residents hammer placed scaffolds while the player raids | Medium–high | M | 1 |
| 7 | Drawn paths and worn footpaths | Medium–high | M | — |
| 8 | Visible stores | Medium | S–M | — |
| 9 | Feast Green as centre and festival place; market days | Medium | M | 4 |
| 10 | Dock that needs water (allow its far end over water, require the near end on the shore) | Low–medium | S | — |
| 11 | Open, furnished homes (C2) | High | XL | Q1 |
| 12 | Drawn log rooms | Medium–high | XL | 11 |

### 3.7 Phased plan

**Phase 0: "They live here" (3–5 days, no new art). The first small step that proves the feel.**
1. Bed and Door points on the Hut and Cottage prefabs. Edit the prefabs directly with `RefreshPrefabId` after, as the
   memory's trap says.
2. Residents sleep in their assigned house (the in-progress assignment), with the two home spots as fallback. Walk in,
   hide, light on; walk out at dawn.
3. Nearest workplace instance, per-instance worker bonus and queue. Gathering at the fire nearest home.
4. The NPC window's Home line, and on the ghost of a home: who would live here and the walk to their work.
5. **Ask the user to play one evening at home:** place a Cottage by the Smithy, then move it to the far shelf. Does
   watching Bjarne walk home make placement matter?

**Phase 1: "Good spots" (1–2 weeks).**
1. The "Here" readout and radii on every ghost.
2. Home surroundings into home quality, wired into the other agent's tiers and comfort.
3. Extensions: data field, slots, three buildings first (Smithy, Woodcutter's Lodge, Kitchen) with 3–4 props each from
   LocalSpawn (concept first, budgets checked).

**Phase 2: "Yards" (2–3 weeks).**
1. Ring detection over line pieces, with tests.
2. Five roles as data, quality, the overlay.
3. Auto-dressing by role (procedural clutter at the gate and along the inside of the fence: a handful of props per yard,
   within budgets).
4. Feast Green as gathering and festival place.

**Phase 3: "Paths and life" (1–2 weeks).**
1. Drawn paths with speed. The footpath wear map.
2. Residents hammer scaffolds. Visible stores. Market day.

**Phase 4: decide after a playtest.** Open, furnished homes (C2), then perhaps drawn log rooms.

### 3.8 How to verify each step

**Tests (EditMode through `RunTests.ps1`, per the memory rule):**

| Step | Test |
|---|---|
| Homes | `ResidentHomeTests`: nearest free bed chosen; full houses fall back to home spots; moving a house moves its sleepers; removing it falls back. Extend `ResidentRoutine` tests for Sleep at a door. Co-op: the assignment goes through `HavenDecisions` (`CoopHomeTests`). |
| Workplace instances | Two Smithies and two smiths: each at the nearer one, each queue fed, the bonus only where someone works (extend `HavenProgressionTests`). |
| Readout and surroundings | Pure rules class (like `ResidentRoutine`): given positions, the list of effects and the quality; radii from data. |
| Extensions | Slot count per tier; an extension out of reach doesn't count; none is speed-only (`HavenProgressionTests.NoUpgrade_IsOnlySpeed` extended). |
| Yards | `YardDetectionTests`: a closed skigard ring with a gate is one yard; without a gate none; an open ring none; two rings sharing a fence are two yards; a broken piece opens the ring; area limits; role scoring with ties. |
| Co-op | Yards computed the same on host and guest from the same pieces (a deterministic test on piece lists). If the wire format changes, `CoopWireTests` gets the fingerprint. |
| Art | `ModelBudgetTests` for every new prop, `PaintedIconTests` for every new building icon (the memory: new buildings need an icon line and painting). |
| Regressions | `BuildPlacementTests`, `HavenBuildingTests`, `HavenProgressionTests`, `CoopHomeTests`, the breach tests. |

**Play-mode probes (CLI, isolated saves; never `sh_save`):**
- At 22:00 every resident with a house is hidden inside it, and its light is on.
- At 06:00 they come out of the door.
- With two Smithies, two smiths stand at different ones.
- Frame time at home before and after Phase 2 with 5 yards and their clutter (ProfilerRecorder at fixed spots, as in the
  terrain memory).

**Hand-playtest checklist for the user:**
1. Place a Hut, assign someone, wait for night: do they walk home and does the window light up?
2. Move the Hut next to the Smithy: does the ghost say "Noisy" and "Near work"? Does it matter to you?
3. Build a Well between three homes: does it read as the village's middle?
4. Put a Bellows by the Smithy: is the choice between extensions interesting, or a chore?
5. Ring a home and a bench with skigard and a gate: does the yard's name appearing feel rewarding?
6. Make a Feast Green: do people gather there in the evening and at the Harvest Feast?
7. Play a breach with fenced yards: do the yards change where the fight happens?
8. On a controller: draw a yard, place an extension, open the overlay. Is anything awkward?
9. In co-op: a visitor helps draw a fence; does the yard appear for both?
10. After an hour: does the Haven look like your town rather than a list of buildings?

### 3.9 Open questions for the user

1. **Interiors and scale.** Do you want indoor rooms eventually (C2)? If so, would homes grow to about 0.85–1.0 across
   (bigger than the 0.6 you chose for reading from above), or should interiors wait?
2. **Yards need a gate?** DQB2 needs a door. A gate makes yards read as places people enter. Or should any closed ring
   count?
3. **Penalties.** Should a noisy forge lower a nearby home's quality, which creates zoning pressure, or keep everything
   positive in the spirit of "beds never gate"?
4. **Who chooses homes?** Automatic (nearest free bed to work) with an override in the NPC window, or always the player?
5. **Where yard and home bonuses go.** Into resident work speed and traits, Well Rested comfort for raids, renown, or a
   mix? The recommendation is all three, each capped once per kind.
6. **May a house wall close a yard,** so a farmstead can be a house plus three fences?
7. **Freeform walls at all?** Is "premade buildings in yards you draw" Safe Haven's identity, or do you want drawn log
   rooms (Phase 4) on the roadmap?
8. **New fence kinds** for yards: hedges and dry-stone walls (art), or the three existing ones only?
9. **Residents building:** should the Carpenter and Mason finish scaffolds while you raid, or must the player always
   hammer?
10. **Paths:** should a path speed the player up at home (+10%), or only residents and carts?

---

## Appendix: key file references

| Topic | File:line |
|---|---|
| Building data, levels, placement fields, comfort and beds | `Assets/_Project/Scripts/Content/BuildingDefinition.cs:11-73, 96-223` (placement `:132-149`, comfort/beds `:166-169`) |
| First building of a kind | `Assets/_Project/Scripts/Content/BuildingObject.cs:66-77` |
| Placing session, grid, snap, rotation, pad spot | `Assets/_Project/Scripts/Building/BuildPlacer.cs:49-62, 231-260, 278-388, 464-486, 490-514` |
| Spot rules, snapping beside, yards | `Assets/_Project/Scripts/Building/BuildPlacer.Spots.cs:23-67, 70-116` |
| Lines, ring closing, gates joining | `Assets/_Project/Scripts/Building/BuildPlacer.Lines.cs`; `BuildPlacement.cs` (`LayArc`, `LineEnds`) |
| Gate | `Assets/_Project/Scripts/Building/FenceGate.cs:1-30` |
| Build area interface | `Assets/_Project/Scripts/Building/IBuildArea.cs` |
| Haven build land, unevenness, water, ward radius | `Assets/_Project/Scripts/Settlement/HomePortal.cs:55-57, 79-81, 99-115, 185-212` |
| Scaffold, hammering, finish, light radius, broken walls | `Assets/_Project/Scripts/Building/Construction.cs:85-95, 216-299, 330-369` |
| Tier N parts and dressing | `Assets/_Project/Scripts/Building/ConstructionLook.cs:4-8, 118-140` |
| Upgrade offer | `Assets/_Project/Scripts/Building/ConstructionUpgradeOffer.cs` |
| Build menu tabs | `Assets/_Project/Scripts/Building/BuildCategory.cs`; `Assets/_Project/Scripts/UI/BuildMenuWindow.cs` |
| Comfort per kind, Well Rested | `Assets/_Project/Scripts/Settlement/HavenReadiness.cs:26-29, 148-160`; `Resources/HavenSettings.asset:39` |
| Renown repeat share | `Assets/_Project/Scripts/Settlement/HavenRenown.cs:25-50`; `Resources/HavenSettings.asset:15` |
| Beds as a count | `Assets/_Project/Scripts/Settlement/SettlementState.cs:243-254, 303-316` |
| Two home spots | `Assets/_Project/Scripts/Settlement/ResidentSpawner.cs:93`; `Assets/_Project/Scenes/SafeHaven.unity` (ResidentSpawner `homeSpots`, 2 entries) |
| Home and workplace of a resident | `Assets/_Project/Scripts/Settlement/ResidentNpc.cs:42-54, 124-150` |
| Day routine, gathering place | `Assets/_Project/Scripts/Settlement/ResidentRoutine.cs:74-92`; `ResidentLife.cs:225-310`; `SettlementSettings.cs:107, 122` |
| Worker production, first queue | `Assets/_Project/Scripts/Settlement/WorkerProduction.cs:12-46` |
| Station speed, worker and bed bonus | `Assets/_Project/Scripts/Crafting/CraftingStation.cs:163-200` |
| Horses near stations (proximity precedent) | `Assets/_Project/Scripts/Horses/HorseJobs.cs:3-66`; `Resources/HorseSettings.asset` (reach 14–16) |
| Jobs and workplaces | `Assets/_Project/Scripts/Settlement/JobDefinition.cs`; `Assets/_Project/Data/Settlement/*.asset` |
| Light and night | `Assets/_Project/Scripts/Environment/Atmosphere/VisibilitySource.cs:66-85`; `Settlement/NightProwlers.cs` |
| Breach targets and lanes | `Assets/_Project/Scripts/Settlement/HomeAssault.cs:11, 53-85`; `WardBreachDirector.cs:652-682`; `BreachLanes.cs` |
| Defence radii | `WatchPostArcher.cs:16`; `WardTotem.cs:14`; `SpikeTrap.cs:14` |
| Life on buildings | `Environment/HearthSmoke.cs`; `Environment/NightFlame.cs`; `Rendering/GroundWear.cs`; `Environment/RainDrips.cs`; `Rendering/SkyOcclusionMap.cs:92` |
| Stable stalls | `Assets/_Project/Scripts/Horses/HorseStable.cs` |
| Co-op sync | `Assets/_Project/Scripts/Multiplayer/HavenSync.cs` |
| Pad building | `Assets/_Project/Scripts/Core/ControllerSettings.cs:41-62` |
| Camera styles | `Assets/_Project/Scripts/Camera/CameraStyle.cs` |
| Building data and prefabs | `Assets/_Project/Data/Buildings/*.asset` (45); `Assets/_Project/Prefabs/Structures/*.prefab`; `Assets/_Project/Art/Haven/Models/*` |
| Kit modelling | `Tools/Blender/building_kit.py` (`log_wall`, `footing`, `board_gable`, `sod_roof`, `chimney`, `build`); `building_props.py`; one script per building |
| Design docs | `Docs/GameDesignDocument.md:104-120` (§2.1–2.4), `:213-292` (§6), `:824-860` (§14), `:862-901` (§15); `Docs/BaseProgression.md`; `Docs/Territory.md`; `Docs/HavenWorldDesign.md`; `Docs/DesignReview_2026-11-13.md:250-257` (no Common Hall or morale; no people at stake) |
| Status table, base building, lived-in Haven, fences | `Assets/_Project/Scripts/ARCHITECTURE.md:56-82, 525-543, 1007-1012, 1795-1808` |
| Memory notes read | `safe-haven-colony-direction`, `-progression-research`, `-skills`, `-building-scale-decor`, `-buildings-from-concepts`, `-placeholder-models`, `-asset-conventions`, `-asset-budgets`, `-art-direction`, `-haven-terraces`, `-haven-rework`, `-terrain-performance`, `-coop`, `-controller-support`, `-horses`, `-playtest-1115`, `-never-remove-functionality`, `-optimize-not-downgrade`, `-design-decisions-1113` |
