# Safe Haven – Code Architecture

A map of the scripts in `Assets/_Project/Scripts`: what lives where, how systems talk to each other,
and where to make common changes. Each script's own summary comment explains its details.

The *what and why* of the game lives in the design document,
[Docs/GameDesignDocument.md](../../../Docs/GameDesignDocument.md) (GDD). This file is the *how*.

## Design reference: GDD sections and the systems that implement them

Status: **Done** = playable as designed, **Partial** = a working base that differs from or covers only part
of the GDD, **Not started** = design only. Update this table when a system is added or the GDD changes.

| GDD section | Status | Implemented by | Differences / missing |
|---|---|---|---|
| §5 Gameplay loops | Partial | `RaidPortal` → `RaidLauncher` → `RaidDirector` → `RaidReport`; `SettlementSystem`, `PurificationAltar`, `HeartTree` | The first-90-minutes loop works: home → raid → home, purification and the first ward upgrade. Later loops (seasons, endgame) not built |
| §6 Safe Haven, ward breaches | Partial | `SafeHaven.unity`, `HeartTree`, `SettlementState`, `WardBreachDefinition` + `WardBreachDirector`, `HomeRevival` | Only the first scripted breach (wolves attack only the player, buildings can't be damaged). No later breaches or defender NPCs |
| §7 Raids, timer, planning | Partial | `RaidDefinition`, `RaidSession`, `RaidParameters`, `RaidLauncher` (Portal Sparks), `RaidPreparationWindow` (destinations, Spark stepper, briefing) | 12 min timer and Portal Sparks (+3 min, max 2, paid once) as designed; raid modifiers are crafted runes. Danger stages escalate the raid (`RaidStage`, see "Raid tension"). No objectives |
| §8 Extraction, loss | Partial | `ExtractionPoint` (windows, availability, cost, alarm), `RaidRisk`, `PlayerSnapshot.AfterFailedRaid`, `DurabilitySettings`, safe pocket in `ItemInventory`, `RaidSummaryWindow`, `PlayerArrivalPoint` | Death/time-out rules as designed (deployed gear kept at −25% durability). No companions or carts |
| §9 Raid world, events | Partial | `LootCache`, `ItemDropTable`, `GameEventTrigger`, `RescuableTraveler` | One hand-made map (`Raid_MistyWoods`) with a side path, a traveler and the Old Smithy Chest; no modular assembly or random events |
| §10 Movement, combat, enemies | Partial | `Player*`, `PlayerEquipment`, `PlayerDodge`, `Combat/*`, `MonsterAI`, `MonsterAttack`, `MonsterProjectile`, `MonsterPack`, `MonsterHowlBuff`, `AttackTelegraph` | Composable enemy tactics/temperaments and multiple attacks; small ranged troll, low-hover charging Mistwing, wolf packs/howl. Player weapons still melee only. Dodge is **0.18 s / 1.5 s cooldown** (GDD: ~0.35 s / 2–3 s). No boss |
| §11 Survival, farming | Partial | `Survival/*` (`PlayerNeeds`, `NeedMeter`, `SurvivalSettings`, `WaterSource`), campfire recipes | Hunger and thirst as decided (never lethal). No farming or meal buffs |
| §12 Resources, inventory | Done | Materials are items (`Data/Items/Materials`, each with a weight, bag capped at 150 by `ItemContainer`'s weight budget). Essences only in `ResourceInventory/*` (weightless bars). `GatheringNodeDefinition` + `ResourceSource`/`ResourceNode`, `MaterialPool`, `ItemInventory`, `BackpackCapacity` (slots + carry bonus), `Gathering/*` (axe/pickaxe/sickle timing minigame, raid noise, Forest Annoyance, raid summary stats) | |
| §13 Crafting, refining | Partial | `RecipeDefinition`, `RecipeBook`, `PlayerCrafting` (hand recipes only), `CraftingStation` + `StationQueue`/`WorkshopState`/`StationCrafting`/`StationWindow` (timed station queues; `StationMenu` is the text fallback), `Durability/*` (repairs), `PurificationAltar` | Stone → Copper → Iron ladder, station queues (pay on queue, keep working during raids, resources to storage first, output slot), repairs and purification work. No Essence enhancements |
| §14 Base building | Partial | `BuildPlacer` (G: preview inside the ward radius, instant, move for free; wheel/Shift+wheel rotate, grid + flush-beside-neighbour snapping, Alt = free; entrance yards kept clear; walls dragged in lines), `BuildPlacement` (footprint geometry), `BuildingDefinition`, `BuildingObject`, `SaveableEntity` | No upgrades or building damage |
| §15 NPCs, companions | Partial | `RescuableTraveler` → `ResidentDefinition`, `JobDefinition`, `ResidentNpc`, `ResidentSpawner` | One recruit (Ingrid) and one job (Alchemist). No companions, needs or relationships |
| §16 Skills, levels, bosses | Partial | `SkillDefinition`, `SkillSchool`, `SkillLoadout`, `PlayerSkills`, `SkillWindow` | No levels/talents, Essence enhancements or bosses |
| §17 Quests, trade | Partial | `Quests/*`, `Events/*`; chain "The First Days" + side quest "A Stranger in the Mist" | No trade/bargain or reactive world |
| §18 UI, accessibility | Partial | `UI/*` (TMP), `ActionMenuWindow` (world menus), `UIStateManager`, `UITheme` | No rebinding, text scaling or reduced-shake option |
| §19 Art, audio | Partial | `Audio/*` (a `SceneAudio` "Scene Music" component per scene), volumes `SafeHaven_Volume` / `MistyWoods_Volume`; environment in `Environment/*` (terrain moss, tree wind, `SurfaceLayerController` moss/rust on LocalSpawn models, grass and glowing plants, water and waterfalls, day/night and weather) | Composed music (`Audio/Safe Haven - *.mp3`) set per scene and as game-wide defaults in `AudioSettings`; other sounds and models are placeholders. Day/night and weather are visual only and not saved (§11 "weather influences plans" not built); only SafeHaven has the sky rig, not the raid scene. No rain/thunder sounds |
| §20 Technical design | Done | Definitions vs runtime state vs `Save/*`; rules in settings assets (`GameSettings<T>`) | Raid crash recovery and seeds not built |
| §21 First 90 minutes | Done (prototype) | `SafeHaven.unity`, `Raid_MistyWoods.unity`, quest chain "The First Days"; `Playtest/*` (`PlaytestLogger` writes one CSV per run, `PlaytestSettings` milestones, **Safe Haven ▸ Playtest ▸ Run Comparison**) | Decisions and assumptions are recorded in GDD §26.1. A run = one playthrough (its id is in the save, so loads continue the same log as a new session) |
| §23–24 MVP / milestones | M0–M3 (no boss-earned skill enhancement yet), plus M4's recruit with a job and M5's scripted ward breach | – | Next per GDD: M4 companions (down/revive) and cart transport; M5 boss, environmental event and polish |

## Folders

| Folder | What it holds |
|---|---|
| `Core` | Game-wide plumbing: `GameSession` (in-memory game state), `GameSystems` (starts persistent systems), `SceneLoader` (scene changes; thin front for `SceneTransition`, the async loader with the loading screen and `ISceneSetup`), `GameStateManager` / `UIStateManager` (scene singletons), `GamePause` + `BackHandlers` (pause ownership, Escape routing), `GameMessages` (on-screen text), `GameSettings<T>` (rule assets loaded from `Resources`), `PlayerArrivalPoint` (where the player appears at home), `AmountText`, `Tiers` |
| `Events` | The game-event bus (`GameEvents`, `GameEvent`) and designer-facing pieces: `GameEventDefinition` (Quest Events), `GameEventTrigger` (zones/interactions raise events), `GameEventListener` (events trigger scene changes) |
| `Player` | The player: `Player` (the front door to the player's parts), movement, input, combat, `PlayerEquipment` (worn gear, weapon and tool belt; see "Equipment"), interaction, `PlayerPersistence` + `PlayerSnapshot` (carrying the player across scenes, and what a failed raid keeps) |
| `Combat` | Stats, damage, `Damageable` (health, death, invulnerability, revive), attacks and hit detection, and combat feel: `CombatFeedback` (hit stop, shake), `HitReaction` (flash, punch, knockback), `CombatFeelSettings` |
| `AI` | `MonsterAI`, its animation driver and `AttackTelegraph` (ground warning during a wind-up) |
| `Camera` | `PlayerCameraController` (top-down and over-the-shoulder action style on one Cinemachine camera, framing per game state), `CameraPreferences`/`CameraStyleRules` (which style applies), `CameraPointer` (mouse vs screen-centre aiming) and `CameraShake` (Cinemachine extension) |
| `Items`, `ResourceInventory`, `Crafting`, `Content` | Data definitions (ScriptableObjects) and their runtime containers. `Crafting/MaterialPool` is the one place costs are paid from |
| `Survival` | Hunger and thirst: `PlayerNeeds` (on the player), `NeedMeter` (one meter's rules), `SurvivalSettings`, `WaterSource` (drink and fill waterskins) |
| `Durability` | Wear on deployed equipment: `DurabilitySettings` (loss per failed raid, repair cost share), `Repairs` (cost and repair), `RepairBench` |
| `Building` | `BuildPlacer`: the build menu (G), the placement preview inside the ward radius, moving buildings, snapping, yards and line drawing. `BuildPlacement`: pure footprint/yard/snap/line geometry (tested in `BuildPlacementTests`) |
| `Settlement` | Safe Haven as a place: `SettlementState` (portal, ward level, breach, residents, purification queue), `SettlementSystem` (persistent; ticks purification), `SettlementSettings`, `HeartTree`, `WardBreachDefinition` + `WardBreachDirector`, `HomeRevival`, residents and jobs (`ResidentDefinition`, `JobDefinition`, `ResidentNpc`, `ResidentSpawner`, `RescuableTraveler`), `PurificationAltar` |
| `Interaction` | `Interactable` – anything the player works on with E |
| `Raids` | Raid definitions, modifiers, the `RaidLauncher` (departure, Portal Sparks), the `RaidDirector` that runs a raid scene, extraction, loot caches, the portal. `Raids/Map`: painted raid maps (`RaidMapDefinition`, `RaidMapZone`, `RaidZoneGrid`, `RaidMap`, `MapRotation`, `RaidMapMarkers`; see "Raid maps") |
| `Quests` | Quest data (`QuestDefinition`, `QuestObjective`), per-player progress (`QuestLog`), `QuestManager`, `QuestGiver` (state-aware: marker, start, hand-in), `QuestMarker` + `QuestMarkerSettings` |
| `Audio` | `AudioManager`, `SceneAudio` + `MusicPlaylist`/`MusicRules` (music), `SoundDefinition` + `DeathSound`/`SoundEmitter` (effects). Edited in **Safe Haven > Audio > Sound Editor** |
| `Environment` | `TerrainMoss`: disposable terrain-alphamap-masked PBR moss and optional leafy sprigs; `MossWind`: shared shader wind, driven by a Directional `WindZone` on the same object when present (moss, `TreeWind.shader` trees and particles share it; see [tree wind](../../../Docs/TreeWind.md)); `TerrainMossBloom`: animated moss paint of the Heart Tree ward circle (runtime copy of TerrainData, so the asset is untouched in Play mode); `SurfaceLayerController`: per-object amount (`SetAmount`/`FadeTo` by layer name, via `MaterialPropertyBlock`) of the two live layers of `LayeredLit.shader` ("Safe Haven/Layered Lit"), which grows tileable moss/rust/dirt over a Local Generator model from its exported `_BlendMask` (curvature, edge wear, cavity, coverage). Grass: `Grass.shader` ("Safe Haven/Grass") on instanced terrain detail prototypes, `GrassInteractor` (up to 8 objects that push grass, as shader globals), `GrassTerrainColour` (baked ground colour the grass is tinted with); `Editor/Environment/GrassSetup` builds the assets and prototypes and rebakes on terrain paint (see [grass](../../../Docs/Grass.md)). Water: `Water.shader` ("Safe Haven/Water": depth colour, refraction, caustics, flow, waterfalls, foam), `WaterSurface` (a pond/river interactors look up; switches the simple look for cameras without depth/opaque textures), `WaterInteractor` (player/stone ripples, wake foam, splashes, or constant churn), `WaterRipples` (GPU ripple height field around the player, `Shaders/Resources/WaterRipples.shader`), `Waterfall` (generated falling sheet + plunge); `Editor/Environment/WaterSetup` makes the assets, creates water and carves beds (see [water](../../../Docs/Water.md)). Sky: `DayNightCycle` (+ `DayNightProfile` asset: sun/moon light, ambient probe, fog, reflections, Night/Golden Hour volume weights, `_SafeHavenNight`), `WeatherSystem` (+ `WeatherDefinition`s and a `ClimateDefinition`: wind via `MossWind.SetWeather`, `RainEffect`, Overcast volume, lightning); `Editor/Environment/WeatherSetup` adds the rig, `SkyControllerWindow` previews and drives it (see [weather](../../../Docs/Weather.md)). Grass also has glowing mushrooms/flowers, Nordic plants (fern, blueberry, lingonberry, heather, wild flowers) and `GrassSetup.FillByGround` (grass and plants by ground layer, none under water or objects). `TreeWind.shader` has Fast Leaf Lighting for leaf cards. `GeneratedTexturePostprocessor` gives new generated materials with a blend mask that shader (Assets > Safe Haven > Use Layered Lit converts others). `Editor/Environment` contains setup, `TreeWindMaterials` (Local Generator trees → Tree Wind materials and `_Wind` prefab variants), deterministic `ForestGroundTextures`, native Amplify graph creation (`ForestGroundShaders`) and preview-only `ForestGroundShowcase`. See [terrain moss usage](../../../Docs/TerrainMoss.md) for maps, materials and budgets. No gameplay/save coupling |
| `Save` | The save system, in layers (see below), plus `SaveableEntity` / `ISaveable` for objects in scenes |
| `UI` | Views and windows. They only read game state and send requests; no game rules live here. `ActionMenuWindow` is the shared menu for world objects (Heart Tree, portal, residents, altar, repair bench, build menu). `StationWindow` is the crafting-station window (recipe icon grid with `RecipeTabs` tabs, selected recipe + Queue, queue grid with progress bar, output grid, extension actions as buttons); its prefab is generated by Safe Haven > UI > Build Station Window (`Editor/UI/StationWindowBuilder`). `ItemTooltipView` also describes recipes and bare definitions (`ShowRecipe`, `ShowDefinition`), used for hover tooltips on station cells and on the bag's hand-recipe rows. `UIRoot` (root canvas, UI scale) and `PauseMenu` (Escape menu); see "UI root, pause and the Escape menu" |
| `Editor` | The Content Editor (`Editor/Content`), the Systems Editor (`Editor/Systems`), the UI theme tool (`Editor/UI`), prefab builders, placeholder generators and Edit Mode tests (`Editor/Tests`) |

**Assemblies:** `SafeHaven` (everything outside `Editor`), `SafeHaven.Editor` (`Editor`, editor-only) and `SafeHaven.Tests`
(`Editor/Tests`). Editing editor tools or tests no longer recompiles the game code. Runtime code can't see editor code;
`internal` members are visible to the editor and tests (`Core/AssemblyInfo.cs`, `Editor/AssemblyInfo.cs`). A new
package used by game code must be added to `SafeHaven.asmdef`'s references. A `[SerializeReference]` class
created before the split carries `[MovedFrom(sourceAssembly: "Assembly-CSharp")]`.

## How systems talk to each other

The game uses four communication paths. Picking the right one keeps systems independent.

1. **Game events (`GameEvents.Raise` / `GameEvents.Raised`)** – facts about the game: an item was obtained, a
   monster killed, a raid finished, a quest completed. Anyone can listen without the raiser knowing.
   *Quests count these, the audio manager plays death sounds from them, and `GameEventListener` turns them into scene
   reactions.* Use this when *other systems might care* that something happened.
2. **Per-object C# events** (`Damageable.Died`, `Interactable.Completed`, `ItemContainer.SlotChanged` ...) – for
   components on or next to that object (a health bar watching its own Damageable). Use this when the listener already
   holds a reference to the object.
3. **`GameMessages.Post`** – text for the player to read. Presentation only; never use it as a signal between systems.
4. **Shared state** – `GameSession` (what survives scene changes) and the registries (`Player.Current`,
   `RaidDirector.Current`, `MonsterAI.Active`, `Interactable.Active`, `SaveableEntity.Live`, `BuildingObject.Active`, `UIStateManager.Find<T>(UIWindowId)` for window scripts). Systems read these instead
   of searching the scene.

### Who raises which game event

| Event | Raised by |
|---|---|
| ItemObtained | `ItemInventory.Give` (loot, caches, crafting results, quest rewards). `AudioManager` plays the rarity's pickup sound from it |
| ResourceGathered | `ResourceSource.GrantTo` and loot grants, for essences only (materials are items and raise ItemObtained) |
| Killed / Destroyed | `GameEvents`, translated from every `Damageable`'s death |
| Crafted | `PlayerCrafting.TryCraft` (hand recipes) and `SettlementSystem` when a station finishes a queued craft (no actor) |
| Built | `BuildPlacer` when a building is placed (quests count it; `ResidentNpc` reacts) |
| Consumed | `ItemInventory` when food or drink is used (berries and herbs are items too) |
| RaidFinished | `RaidDirector` when a raid ends |
| QuestStarted / QuestCompleted | `QuestManager` |
| Custom (Quest Events) | `GameEventTrigger`, `GameEventDefinition.Raise`, a quest's completion event, and the Quest Event fields on `RescuableTraveler` (freed/rescued), `HeartTree` (talked to), `RaidPortal` (repaired), `JobDefinition` (job assigned, via `ResidentNpc`) and `WardBreachDefinition` (breach won). The Content Editor's "nothing raises this event" check finds these by the `[RaisedByGame]` attribute on the field; when you add a new Quest Event field that the game raises, mark it `[RaisedByGame]` |

## Lifetimes

| Lives... | Examples | Created by |
|---|---|---|
| For the whole run | `AudioManager`, `QuestManager`, `CombatFeedback`, `SettlementSystem` | `GameSystems` (automatically; no scene setup) |
| For the current game | stash, world state, quest log, settlement (`SettlementState`), active raid | `GameSession` (cleared by New Game, replaced by Load) |
| For one scene | player, `RaidDirector`, `WardBreachDirector`, HUDs, monsters | the scene |
| Forever, as authored data | items, monsters, quests, sounds, raids, residents, jobs | ScriptableObject assets, edited in the Content Editor |
| Forever, as rules | survival, durability, settlement and rarity settings | Settings assets in `Resources`, edited in the Systems Editor |
| On disk | save files | `SaveGameService` → `SaveStorage` |
| As preferences | volume, camera style and per-situation camera overrides (`CameraPreferences`) | `PlayerPrefs` |

Persistent systems implement `IGameSystem.OnSceneEntered`, which `GameSystems` calls once per scene after the scene's
objects have woken up. Use that instead of `SceneManager.sceneLoaded` (it also covers the editor's "no scene reload"
play mode). Use `SceneLoader.Load` for gameplay scene changes so saveable objects are recorded first.

## Scene changes, raid results and raid layouts

- **Async transitions:** `SceneLoader.Load(scene, caption)` → `SceneTransition` fades to a loading screen, records saveable
  objects, loads the scene with `LoadSceneAsync` (activation held until the bar is full and a minimum time has passed),
  then runs every `ISceneSetup` in the new scene (by `SetupOrder`, spread over frames) *after Awake and before Start*,
  and only then fades in and raises `SceneTransition.SceneReady`. Gameplay that must wait for the screen to be visible
  (the raid clock) uses `SceneTransition.WhenReady(callback)`. The first scene in play mode gets the same setup pass.
  Look and timing: `Resources/SceneTransitionSettings` (Systems Editor > Raids & Portal).
- **Results screen:** a raid ends → `RaidDirector` loads the small `RaidResults` scene → `RaidResultsScreen` takes the
  `RaidReport` from `GameSession`, shows artwork (per raid on `RaidDefinition` › Results Screen, else the screen's
  defaults) and the haul, and `SceneTransition.Preload`s Safe Haven while the player reads. Continue activates it.
  Player state and the arrival point stay in `GameSession` across the extra scene. `RaidSummaryWindow` is only the
  fallback when `RaidResults` isn't in Build Settings.
- **Raid layouts:** each raid has a per-raid `RaidSession.Seed`. `RaidGenerator` (an `ISceneSetup` on "Raid Layout") gives
  every `SpawnGroup` its own seed from that seed and the group's path, so editing one group never reshuffles another.
  A group fills *min–max of its hand-placed points* (child transforms) with prefabs rolled from a `SpawnTable`
  (weights, per-group caps, guaranteed minimums so quest resources always exist). Fixed story objects and extraction
  points are ordinary scene objects outside "Raid Layout". Add a point: duplicate a "Point NN" child. Add variety:
  add an entry to the table (`Data/Raids/SpawnTables`). Same seed = same layout (`RaidGenerator.fixedSeed` for testing).
- **Spawn lists (what spawns lives on the raid, the scene only supplies points):** `RaidDefinition.spawnLists` holds
  `RaidSpawnList`s, each with a category (Enemies / Resources / Loot & Events / Escalation, only for the editor tabs),
  the names of the scene's Spawn Groups whose points it may fill, a min–max count over *all* those points, entry band,
  separation, trigger/stage/hunt, and entries (prefab, share % — kept at 100 by `SetShare`/`Normalize` — min, max).
  `RaidGenerator.BindLists` binds each named group to one list (first list wins; a group serves one list) and fills the
  list once from `SeedFor(seed, "List:" + name)`. Groups no list names still roll their own `SpawnTable` (legacy path).
- **Spawn rules:** `RaidDefinition` carries `enemyDensity` (× `RaidGlobalSettings.GlobalEnemyDensity`; multiplies
  min/max of *enemy* lists/groups only, i.e. whose prefabs all carry a `MonsterAI`) and optional own `near/farDistance`
  (`overrideBands`, else the global ones). `RaidGenerator.Rules` builds a `SpawnRules` from them, measured from the
  turned `RaidMap.PlayerStart`; an `entryBand` (Any/Near/Middle/Far) limits a list to points at that distance. Counts
  are capped by the points in band. `SpawnGroup.Plan` rolls a layout without placing it (same draws as spawning).
- **Global raid rules:** `Resources/RaidGlobalSettings` (Systems Editor > Raids & Portal > Global Rules): sleep/wake
  distances, default near/far bands, global enemy density, max creatures alive.
- **Raid systems:** `RaidDefinition.systems` is a `[SerializeReference] List<RaidSystem>`; each system (subclass of
  `RaidSystem`: `DisplayName`, `Description`, `enabled`, `Install(RaidDirector)`) is added per raid in Systems Editor >
  Raids & Portal > Raid Systems (the "+ Add System" menu lists every non-abstract `RaidSystem` type). `RaidDirector`
  installs the enabled ones when the raid starts. New system = new `RaidSystem` subclass with a parameterless
  constructor; no editor code needed. `RoamingSpawnsSystem` wraps the roaming spawns below.
- **Roaming spawns** (on top of the seeded camps): Raid Systems › Roaming Spawns (`RoamingSpawnSettings`: table, first
  delay, interval, alive cap = base + per stage + per alarm (× enemy density, ≤ max), total budget, min/max distance,
  despawn distance, camp clearance, hunt spread). `RoamingSpawner` (auto-added by `RaidDirector`) places a roamer out of
  sight in a ring around the player, grounded by a downward raycast, on the NavMesh, away from other creatures and only
  with a complete path to the player. It walks (`MonsterAI.Roam`) to a reachable spot near the player, attacking what it
  notices; idle roamers re-hunt each interval. Roamers that lose the player (idle, beyond despawn distance, unseen) are
  removed and refunded to the budget. Empty table or alive cap 0 disables it.
- **Sleep:** `CreatureSleep` (auto-added by `RaidDirector`) disables AI, animation driver, Animator and NavMeshAgent of
  idle, non-roaming creatures beyond 85 m and wakes them within 70 m or when hit (the attacker is engaged). Creatures
  are not pooled (low spawn rate, lots of state to reset); see "Pooling".

## Raid maps: painted on the terrain → baked → turned per raid

- **Authoring (in the Scene view, not hand placement):** select the "Raid Map" object; its inspector hosts the **Map
  Painter** (`RaidMapEditor` → `RaidMapPainter`). The layout lives in the map's `RaidMapDefinition` (`Data/Raids/Maps`):
  - **Heights** are the terrain itself (`<Map> Terrain.asset`): **Sculpt Heights** selects the terrain for Unity's own
    Raise/Lower, Smooth and Set Height tools. Bake never touches heights.
  - **Zones** (`RaidMapZone`, byte grids of `zoneResolution`², row 0 = south, cell i at i/res like `GetPixelBilinear`):
    **Paint Zones** brushes the selected zone on the terrain (Shift erases, `[`/`]` resize, Alt orbits) and shows every
    zone as a see-through colour overlay (`RaidMapOverlay.shader`, editor only). Roles: **Path** (soil, no trees),
    **Forest** (tree density, moss), **Clearing** (some soil, no trees) and **Spawn** (see below). Several zones may
    share a role; the strongest wins. Arithmetic in `RaidZoneGrid` (tested).
  - **Markers** (`RaidMapMarker`, 0–1 position + kind + label): coloured handles to drag; click ground to add the chosen
    kind, Delete removes. Kinds/colours: `RaidMapMarkers` (start, exit, enemy camp, resource site, supply cache, event
    site, patrol).
  - The definition also holds terrain layers, weighted tree/rock prefabs, tree spacing and per-site rules (points per
    site, spread). **Top-down View** frames the map from straight above.
- **Spawn zones feed the existing spawn settings:** a Spawn zone scatters candidate points (`pointSpacing`, `maxPoints`,
  `maxSlope`, optional `clearTrees` radius; deterministic per `scatterSeed` + zone name, previewed while painting). Bake
  gives them to every `SpawnGroup` whose **Zone** field has the zone's name ("Ore 3" children, round-robin over
  groups sorted by name). How many are actually used per raid is still the group's count or the raid's
  `RaidSpawnList`, scaled by density through `SpawnRules` — zones only say *where*.
- **Bake (editor):** **Bake Map** (`RaidMapBaker`) writes terrain paint and trees into `<Map> Terrain.asset` (hand-painted
  terrain textures and trees are replaced), fills every `SpawnGroup` with a matching `site` or `zone` with points,
  moves the player start and exits, snaps content to the ground, rebuilds `TerrainMoss` and bakes the NavMesh.
  Groups with neither (`IsBakedFromMap` false) are never touched, so hand-placed points still work.
- **Images (optional):** heights export/import as 16-bit greyscale PNG (exact round trip), zones as 8-bit greyscale PNG,
  for touching up in an image editor (`RaidMapImages`). Maps made with the older three PNGs (height, RGB mask, marker
  dots, still referenced under "Legacy images") convert once with **Import from Images**; Misty Woods is converted
  (heights, zones and 55 markers identical, rebake identical).- **Trees block the NavMesh through "Tree Trunks":** NavMeshSurface ignores terrain trees, so the baker writes one
  invisible combined MeshCollider (`<Map> Trunks.asset`, NavMeshModifier → Not Walkable) under Map Content. It is
  excluded from ground snapping. Rebake if trees change.
- **One layout, four maps:** `RaidMap` (an `ISceneSetup`, order −100, before `RaidGenerator`) turns the map 0/90/180/270°
  from the raid seed (`MapRotation.Pick`): it rotates a *copy* of the terrain data (heights, paint, trees), rotates
  "Map Content" (points, exits, quest zones, story objects, trunks) around the centre, rebuilds moss, updates the
  NavMesh and places the player. `fixedQuarterTurns` forces a turn for testing. Everything that must turn with the
  map goes under Map Content.
- **Forest kit:** placeholder low-poly spruces, pine, dead spruce, boulders and log from **Safe Haven > Raids > Build
  Forest Kit** (`ForestKitBuilder`, `Art/Forest`). Replace a prefab's mesh/material to upgrade the art; rebake.
- **Event sites:** "Event Sites" fills magenta markers from `MistyWoods_Events` (Wolf Den, Spirit Spring, Ore Collapse,
  Abandoned Camp in `Prefabs/Raids/Events`). Add an event: a prefab + a table entry.
- **New map:** create a definition, a scene with a "Raid Map" (`RaidMap`), Bake once (creates the terrain), then **Start Painting**: sculpt, paint zones, place markers, Bake.

## Raid tension: danger stages and extraction choices

- **Danger stages** (GDD §7.3 final danger phase): `RaidDefinition` › Escalation lists `RaidStage`s (name, starts at
  *seconds left*, message, enemy health/power bonus). `RaidDirector` tracks `StageIndex` (0 = calm) through the pure
  `RaidStages.IndexAt`, posts the message, re-applies enemy strength (raid multiplier × (1 + stage bonus); wounded
  enemies only gain the extra maximum; power becomes the `DamageBonus` stat, a straight damage multiplier) and raises `StageChanged`. Thresholds count time *left*, so Portal Sparks add
  calm time instead of secretly scaling the danger. Misty Woods: The Mist Stirs 8:00, The Hunt 4:00, Collapse 1:00.
- **Waves:** a spawn list's (or unbound `SpawnGroup`'s) `trigger` is RaidStart (filled while loading), Stage (filled when stage `stage` begins) or
  Alarm (filled by `RaidDirector.Wake`). `huntPlayer` makes its creatures `PlayerOnly` + `Relentless` and engage the
  player at once. Late groups use the same seed (`RaidGenerator.SpawnNow`/`SpawnStage`) and get the raid's strength.
- **Exits are choices:** `ExtractionPoint` has an open window (`opensAtSecondsLeft` / `closesAtSecondsLeft`, pure
  `ExtractionWindow`), an `availabilityChance` rolled per raid by `RaidGenerator` (`RaidLayout.PickAvailable`, at least
  one exit always stays), a `timeMultiplier`, an optional item cost (checked as an `IInteractionCondition`, paid on
  completion) and an optional `alarm` group woken the first time someone starts extracting there. Misty Woods: West
  Beacon (far, always open), East Beacon (70% of raids, goes dark at 4:00, alarm wakes two hunters), Hollow Oak
  (central, opens at 3:00, extracts in 60% of the time).
- **HUD:** `RaidHudView` shows the stage beside the raid name, the nearest *open* exit with its arrow, every exit with
  distance and state, and "At risk" (`RaidRisk.Of`: what a failed raid would cost, refreshed once a second).
- Ideas this is built for: elites (`SpawnGroup` + stat bonus), event-driven `Wake` (a horn, a broken seal), raid
  modifiers that shift stage thresholds, and exits that cost a crafted rune.

## The save system in layers

`GameSession` (memory) → `SaveMapper` (memory ↔ file format) → `SaveData` (file format: ids and numbers only) →
`SaveStorage` (files on disk). `SaveGameService` drives them; `ContentDatabase` turns saved ids back into assets.
Objects in persistent scenes save themselves through `SaveableEntity` and `ISaveable` components.

What the file holds besides the bag, stash and quests: the settlement (`SaveData.settlement`: portal, ward level,
breach, residents with jobs, purification queue), each item's wear (`SlotSaveData.wear`, so saves from before durability
load undamaged), hunger, thirst and the equipped slot, and the buildings the player knows.

Save ids are automatic: prefabs get their prefab id on import, and every scene gets missing or duplicated object ids
filled in when it is saved (`SaveableEntityTools`). Safe Haven > Save > Assign Missing Save Ids does the same by hand.

## Common changes – where to make them

| I want to... | Do this |
|---|---|
| Add an item, monster, quest... | Content Editor (Safe Haven > Content Editor). No code. |
| Change music, add a sound effect, find unused audio | Sound Editor (Safe Haven > Audio > Sound Editor). Music lives on each scene's "Scene Audio" object (exploring / combat / final-stretch / ambience playlists); empty lists fall back to the defaults in `AudioSettings`. `MusicRules.Choose` decides: final stretch → combat → exploring → default. Object sounds (death, pickup) are `SoundDefinition` assets set on the prefab or its definition. |
| Add a new music situation (e.g. boss) | Add an `AudioMood`, a playlist on `SceneAudio`, and a step in `MusicRules.Choose` |
| Make something react when a quest completes or a zone is entered | Add a `GameEventListener` / `GameEventTrigger` in the scene (Add Component › Safe Haven › Quests › Quest Event Listener / Trigger). No code. |
| Make a one-off world event (e.g. "after the first raid, entering the ward blooms moss") | GameObject › Safe Haven › World Event (Zone + Response) or Ward Moss Bloom. The trigger has **When** (zone / interact / start / `PlayerInsideWard`), **Requires** (a `QuestCondition`: quest Started/Completed) and **Repeat** (`EveryTime` / `OncePerSceneLoad` / `OncePerGame`). OncePerGame events are remembered in `QuestLog.HappenedEventIds` (saved); the listener's **On Load (Already Happened)** response restores the world state, e.g. `TerrainMossBloom.BloomInstantly`. New reactions = a small component with a public method, wired in the listener |
| Place a quest giver, inspect point, quest zone or listener | GameObject › Safe Haven › Quest Giver / Quest Inspect Point / Quest Zone / Quest Event Listener (pre-wired Interactable and trigger). Create a missing Quest Event with **New** next to any Quest Event field |
| Make a quest be handed back to its giver | Quest › **Turn In To Giver** (Content Editor). The quest waits in `ReadyToTurnIn` until `QuestGiver` (or `sh_quest --action turnin`) hands it in; rewards come then |
| Change the floating !, … and ? over quest givers | Systems Editor › World › Quest Markers (`Resources/QuestMarkerSettings`). No code. |
| Find where a quest or Quest Event is used in scenes | Content Editor quest/event page: **In the Scenes** (read from saved scene/prefab files by `QuestSceneIndex`; click to open). Warnings flag givers missing and events nothing raises. **Select All Uses in Open Scenes** selects every object in the loaded scenes that references it (includes unsaved edits) |
| See how all quests connect | **Safe Haven › Quests › Quest Map** (or *Open Quest Map* on a quest page): chains as rows, prerequisite arrows, each awaited Quest Event with what raises it; red = missing. **Drag** one quest onto another to make the second come after the first (loops/duplicates refused), or onto empty space (or use **+ New Quest**) to start a follow-up that copies the prerequisite, chain, start type and hand-in; **right-click** a quest to edit it, add a follow-up, or remove a prerequisite; right-click empty space for a new quest. Layout and link rules are `QuestMapLayout` (pure, tested) |
| Give a Quest Giver varied lines | `QuestGiver` › **Waiting Lines** / **After Lines** (`DialogueLines`: In Order, In Order Then Last, Random without repeats). A new line is picked when the player looks away and back |
| Add a new objective type | `QuestObjective` (see its "Extending" comment) |
| Report something new for quests/audio to react to | Add a `GameEventKind`, call `GameEvents.Raise` where it happens |
| Save a new piece of game-wide state | `SaveGameService` "Extending" comment (4 steps) |
| Save state of objects in a scene | Implement `ISaveable` on a component; add `SaveableEntity` to the object |
| Add a persistent system (achievements, analytics...) | `IGameSystem` + one line in `GameSystems.CreateSystems` |
| Give the player a new part other systems need | Add a property to `Player` |
| Add a Content Editor page | `ContentSection` "Extending" comment |
| Add a raid rule a modifier can change | `RaidStat` + `RaidDefinition.GetBase` + the system that reads it |
| Add a UI window | New `UIWindowId` value + a `UIWindow` on the canvas |
| Change rarity colors, names or pickup sounds | `Resources/RaritySettings` in the Inspector. No code. |
| Make an item rarer or more valuable | The item's **Loot** fields (rarity, value) in the Content Editor. No code. |
| Add a material or change its weight/stack size | New `ItemDefinition` (category Material) in `Data/Items/Materials` via the Content Editor; **Weight** and **Maximum Stack** on the item. Overview in Systems Editor > Carrying |
| Add a gatherable node (tree, ore, herb patch) | Content Editor > Gathering Nodes: a `GatheringNodeDefinition` (tool, tier, seconds, yields, respawn) and its generated prefab |
| Change how many bag slots the player starts with | `ItemInventory.baseUnlockedSlots` on the PlayerArmature prefab; backpacks add `bagSlotBonus` |
| Change the safe pocket size | `ItemInventory.safePocketSlots` on the PlayerArmature prefab |
| Tune hit stop, shake, hit flash, knockback, telegraph colors | `Resources/CombatFeel` in the Inspector. No code. |
| Tune the dodge (distance, speed, i-frames, cooldown) | `PlayerMovementSettings` asset, Dodge header. No code. |
| Give a monster a longer attack warning | Raise its attack's **Hit Delay** (the telegraph lasts exactly that long) |
| Make one target react differently (a boss that can't be pushed) | Add `HitReaction` to its prefab and change its fields |
| React to the player dodging (sound, stamina) | Listen to `PlayerDodge.Dodged` |
| Tune hunger/thirst, carry weight, death and repairs, raid time and Portal Sparks, the ward, purification or breaches | Systems Editor (Safe Haven > Systems Editor). No code. |
| Add a raid destination | New `RaidDefinition` (Content Editor) + add it to the portal's destination list; base rules show in the Systems Editor |
| Reshape a raid map (paths, cliffs, where camps and resources are) | Select "Raid Map" › Map Painter: Sculpt Heights, Paint Zones, Markers, then Bake Map. See "Raid maps" |
| Let ore, herbs or enemies appear in a painted area | Map Painter › Paint Zones › Add Spawn Zone (name it, paint it), set a `SpawnGroup`'s **Zone** to that name, Bake. Amount/density stay in the group or the raid's spawn list |
| Add a buildable structure | `BuildingDefinition` in the Content Editor (cost, **Show In Build Menu**, **Known From Start**) with a prefab holding `BuildingObject` + `SaveableEntity` |
| Keep the ground in front of a building clear, or build it in lines (walls) | The `BuildingDefinition` **Placement** fields: **Yard Depth** (0 = none) and **Yard Side** (Front = local +Z), **Place In Lines** for drag-to-build pieces. No code. |
| Add a resident or a job | Content Editor (Residents, Jobs). A job can teach buildings when assigned |
| Make a new cost (a station, an upgrade) payable from storage at home | Pay it through `MaterialPool.For(player)`; never take from the inventories directly |
| Give a world object a menu of choices (an NPC, a station) | Write a method that builds a `MenuPage` of `MenuAction`s and call `ActionMenuWindow.Current.Open(BuildPage, user, transform)`; the page is rebuilt after every choice (see `HeartTree`). Pass `onClosed` to react when the conversation ends |
| Keep something locked until the story reaches it (the rootbound stones, the portal) | Add `QuestGate` next to its `Interactable` and pick the quest; the prompt shows the locked text |
| Change what the Heart Tree says at each step | Heart Tree in SafeHaven: **Quest Whispers** (one line per active quest) |
| Add a station recipe or tune craft time, output slot and queue size | Recipe: set **Station** and **Craft Seconds** in the Content Editor (Station "By Hand" = instant from the bag). Overview in Systems Editor > Crafting; output slot and queue length per building in Systems Editor > Heart Tree & Building. Station state lives in `SettlementState.Workshops` and is saved |
| Add a choice to a station's menu (like "Repair gear…" or "Plan a raid") | Implement `IStationMenuExtension` on a component next to the `CraftingStation`; its actions show as buttons in `StationWindow` (which closes before the action runs) or as rows in the `StationMenu` fallback |
| Recolor the windows | Safe Haven > UI > Select UI Theme, edit the colors, then Safe Haven > UI > Apply UI Theme |
| Change where monsters, resources or caches can appear in a raid | Raid scene > "Raid Layout" > the group: move/duplicate its "Point NN" children. Which groups a list uses: Content Editor > Raids > the raid > Enemies/Resources/… tab > **Where** |
| Change what a raid spawns, how many, or the % of each | Content Editor > Raids > the raid > Enemies / Resources / Loot & Events / Escalation tab (share %, min, max, count). **Preview** tab simulates 50 raids |
| Change rules for every raid (sleep distance, entry bands, global density) | Systems Editor > Raids & Portal > Global Rules |
| Add an optional system to one raid (e.g. roaming spawns) | Systems Editor > Raids & Portal > Raid Systems > pick the raid > + Add System |
| Set up something in a new scene before the player sees it | Implement `ISceneSetup` on a component in that scene (runs during the loading screen, before `Start`) |
| Tune the action (over-the-shoulder) camera | Systems Editor > Camera > Action camera (distance, pitch limits, FOV, shoulder offset, sensitivity, collision, pointer reach) |
| Make a situation always use one camera style | In game: Options › Camera (Exploration / Combat / Building / Raid). Priority is Building > Combat > Raid > Exploration; rules in `Camera/CameraStyle.cs`, tests in `Editor/Tests/CameraStyleRulesTests.cs` |
| Aim or pick something with the cursor | Use `CameraPointer.TryGetRay` / `TryGetPointOnPlane`, never `Input.mousePosition` directly, so it also works in the action camera (screen centre) |
| Change the raid results artwork | `RaidDefinition` › Results Screen (per raid) or `RaidResultsScreen` in the RaidResults scene (default) |

## Safe Haven: home, survival and durability

- **At home or in a raid?** `RaidDirector.Current == null` means at home. `MaterialPool.For(player)` uses it: in a raid
  only carried things count; at home storage counts too. Costs are paid carried-first and never half paid.
- **Arriving home:** `RaidDirector` sends the player to the `PlayerArrivalPoint` named "Portal" after an extraction and
  "HeartTree" after death or time-out. Dying at home (a ward breach) is handled by `HomeRevival`.
- **Failed raids:** `PlayerSnapshot.AfterFailedRaid` keeps knowledge, condition, the safe pocket and the deployed
  equipment from the loadout captured at departure, worn by `DurabilitySettings.LossOnFailedRaid`. Durability is stored
  as wear on `ItemStack`; items with durability never stack.
- **Tools:** an item's tool tier is the highest resource tier it gathers (Stone 2, Copper 3, Iron 4); tier 1 is gathered
  by hand.
- **Gathering minigame** (`Gathering/*`): `PlayerGathering` (on the player prefab) intercepts work on nodes whose tool
  has a `GatheringToolProfile` in `GatheringMinigameSettings` (axe, pickaxe, sickle: bar speed, zone widths, loudness),
  holds the `Interactable` open and runs a plain-C#
  `GatheringSession` per node (`GatheringNodeDefinition.swings`). Crits call `ResourceSource.GrantBonus` plus
  `CombatFeedback.HitStop`; every swing calls `ResourceNode.NotifySwing`. `ResourceNode` raises `Swung`/`Harvested`/`Regrown`
  and delays hiding its renderers by the break time; `ResourceNodeFeedback` (auto-added) plays them from the node's
  `HarvestFeedbackProfile` (sound, pooled `PooledEffect` particles, node shake, break/regrow pop; per-field fallback to
  `GatheringMinigameSettings.DefaultFeedback`) and exposes UnityEvents for per-prefab one-offs. Glancing blows
  wear the tool by `DurabilitySettings.WearPerGlancingBlow`. `GatheringBarView` is the procedural bar. In a raid each
  swing goes through `GatheringNoise` (wakes `CreatureSleep` within a radius scaled by loudness, feeds annoyance). `ForestAnnoyanceSystem`
  is a raid system (Raid Systems tab, tuned per raid) that `ForestAnnoyanceDriver` runs: Stirring raises `RoamingSpawner` caps/rate,
  Angry calls `HuntNow`, Wrath spawns one wave from its table; `ForestAnnoyanceView` shows the meter. `RaidDirector`
  tallies swings into `RaidSession.Gathering` (`GatheringStats`, plus peak annoyance from the driver), shown in the raid
  summary by `RaidReportText.Gathering`. Tuning: Systems Editor > Player > Gathering Minigame.
- **The Heart Tree** (`HeartTree`) is the hub: talking, starting the pending breach, strengthening the ward (which grows
  `BuildPlacer`'s radius). `WardBreachDirector` spawns the breach's enemies along its marked path and makes buildings
  invulnerable while it runs.
- **Residents:** `RescuableTraveler` follows the player in a raid; extracting with her adds a `ResidentRecord` to
  `SettlementState`. `ResidentSpawner` places residents in Safe Haven; `ResidentNpc` opens their job menu.
- **Purification:** `PurificationAltar` queues batches in `SettlementState`; `SettlementSystem` ticks them everywhere
  (also during raids) while someone holds the Alchemist job.

## Editors for designers

| Editor | For | Menu |
|---|---|---|
| Content Editor | Things: items, resources, recipes, buildings, monsters, quests, raids, residents, jobs, ward breaches. A raid page has tabs: Overview, Rules, Enemies, Resources, Loot & Events, Escalation, Systems, Preview | Safe Haven > Content Editor |
| Systems Editor | Rules and numbers: survival, carrying, death and repair, movement and combat, raids and the portal, the Heart Tree and building, residents and purification, breaches, loot and rarity. Edits the settings assets and shows derived numbers (minutes until hungry, raids until gear breaks, raid length with Sparks) | Safe Haven > Systems Editor |
| UI theme | Window, button and text colors of every UI prefab | Safe Haven > UI |

### CLI commands (`Editor/Cli/`)

Project commands for the Unity CLI, so tools and agents don't need long `eval` scripts for routine checks. Each is a
static `[CliCommand]` method (Pipeline package), tagged `safehaven`, and returns short plain text. List them with
`unity command --query sh_`; run one with `unity command sh_give --format json --no-banner -- --id IronOre --amount 10`.

| Command | Mode | Does |
|---|---|---|
| `sh_content --type --filter --fields --limit` | any | List content (item, recipe, node, building, quest, monster, raid, modifier, essence or a class name). One match shows every field |
| `sh_health --page` | any | Runs every Content Editor page's `FindProblems` |
| `sh_find_refs --asset` | any | Files that reference an asset (by path, GUID, id, display name or file name; every asset sharing the name) |
| `sh_missing_refs --folder` | any | GUIDs in scenes/prefabs/assets that point at deleted assets |
| `sh_player` | play | Scene, raid, HP, needs, stats, bag, pocket, gear, essences, active quests |
| `sh_give --id --amount --to bag\|pocket\|stash` | play | Give items or essences; a negative amount takes them |
| `sh_clear --what`, `sh_player_set --health --hunger --thirst --invulnerable` | play | Test setup |
| `sh_quest --action list\|start\|complete\|progress\|turnin` | play | Quest state |
| `sh_raid --action status\|launch\|extract\|skip\|die` | play | Drive a raid; `skip` stops just short of the end so the director finishes it normally |
| `sh_spawn --id --count`, `sh_save --action save\|load\|info` | play | Spawn monsters on the NavMesh; save/load |
| `sh_sky --hour --weather --transition --speed --lightning` | any | Show or set the time of day, clock speed and weather (`SkyCliCommands`; the Sky Controller window does the same) |

Lookups (`CliLookup`) match an asset by id, display name or asset name, then by a unique partial match, and fail with
the candidates when a key is ambiguous. Add a command by writing another static method in `ContentCliCommands` or
`PlaytestCliCommands`; it registers on the next recompile. New content pages are picked up by `sh_health`
automatically through `ContentEditorWindow.CreateSections()`.

## Combat feel

- `Damageable` raises the game-wide `AnyDamaged` event after every hit. `CombatFeedback` (a persistent system) turns
  it into a `HitReaction` on the target, and, when the player is the attacker or the victim, a hit stop
  (`Time.timeScale` dips for a few real-time milliseconds) and `CameraShake.Add`.
- `CameraShake` is added to the scene's Cinemachine camera automatically. Other systems may call `CameraShake.Add` too.
- `AttackTelegraph` (on monster prefabs) draws the attack's range and arc from its `AttackDefinition` and fills it
  over the wind-up (`HitDelay`); when it is full, the hit lands.
- `PlayerDodge` dashes through `PlayerMovement.Dash`, makes the player's `Damageable` invulnerable for a moment
  (`AddInvulnerability` / `RemoveInvulnerability`, which can stack) and cancels a swing in progress in `PlayerCombat`.
  A hit avoided this way raises `Damageable.Evaded`, shown as "Dodged".

### Creating enemies with multiple attacks

In **Safe Haven > Content Editor > Monsters**, use the **Composable combat** fields:

- **Movement Tactic** is Chase or Keep Distance. Preferred Distance and Distance Tolerance control ranged spacing.
- **Temperament** is a multi-select: Aggressive initiates combat, Cowardly retreats below Flee Health Fraction until
  it gains Flee Distance, and Defensive confines engagement to Defensive Radius around home. Defensive bounds take
  priority over pursuit; cowardly retreat takes priority over attack selection. No flags means retaliation only.
- **Attacks** replaces the legacy single attack only when nonempty. Each `MonsterAttack` has its own definition,
  damage, minimum/maximum range, priority and cooldown. Highest eligible priority wins; list order breaks ties.
  Melee, Projectile, Charge and Howl are supported. Cooldowns start after recovery, including interrupted wind-ups.
- Projectile actions require a prefab with `MonsterProjectile`; they re-aim for each shot in a burst, never home,
  and stop at solid scenery/allies. They use the existing damage calculation and damage-dealt events.
- **Pooling:** `PrefabPool` (Core, one per scene, built on `UnityEngine.Pool.ObjectPool`, ≤ 64 idle per prefab) reuses
  monster projectiles and damage numbers. `PrefabPool.Spawn` instead of `Instantiate`, `PrefabPool.Release` instead of
  `Destroy`; pooled objects must reset themselves in `OnEnable`/their initialize call. Releasing twice is harmless.
- Charges lock direction at wind-up, remain on the NavMesh, stop at an edge or first hit and expose the creature
  for Exposed Seconds after either a hit or miss. Hover Height moves only the Model, not navigation or hit detection.
- Howl applies a timed `DamageBonus` modifier through `MonsterHowlBuff` to living allied monsters in its radius,
  including the caster. Repeated howls replace/refresh the one modifier rather than stack. The buff expires or clears
  on disable. Pack Leader Only limits the wolf howl to the authored leader.
- `MonsterPack` alerts its 2-3 wolves together, reserves staggered attack starts and designates one howler.
  Misty Woods uses a separate `MistyWoods_WolfPacks` table at raid start, preserving single wolves in alarm/stage waves.
  `SpawnGroup.minimumSpawnSeparation` prevents separate packs filling adjacent points of the same camp.

Small Moss Troll has a three-stone burst and weak close-range shove. Mistwing follows at a distance, charges and
lands for two seconds before retreating. Their new spawn groups are under RaidMap Content so rotations include them;
their hand-placed points remain unchanged by map baking (`Site=None`). Visuals are distinct primitive placeholders.
Edit Mode regression tests: `Editor/Tests/MonsterBehaviorTests.cs`. `AgentScripts/VerifyEnemyEncounters.cs` additionally
checks saved assets and runs isolated live combat probes without loading a player save.

## Loot: rarity, carry space and the safe pocket

- **Rarity and value** live on `ItemDefinition` (Loot header). `Rarity` (in `Items/ItemRarity.cs`) turns a rarity into a
  color, name and pickup sound from `RaritySettingsDefinition`. Slots tint by rarity, tooltips and the message feed
  color names, `AudioManager` plays the pickup sound from the `ItemObtained` event, and `RaidReport` sums value and
  lists the rarest finds first.
- **Carry space**: `ItemContainer` has locked slots (past `UnlockedSlots`): they keep items but take no new ones.
  `ItemInventory` unlocks `baseUnlockedSlots`; `BackpackCapacity` adds the best carried backpack's `bagSlotBonus`.
  Losing the backpack in a raid shrinks the bag again. Ctrl+right-click in the bag drops a stack.
- **Safe pocket**: `ItemInventory.SafePocket`, a separate small container. `PlayerSnapshot` carries it, and
  `WithoutBelongings` (a failed raid) keeps it. It is saved as `PlayerSaveData.safePocket`.

## Equipment and the character screen (C)

- **Gear model**: `Items/GearSlot.cs` (the slot enum, plus `GearSlots` for count, labels and `Describe`) and `Items/GearSet.cs`
  (an `ItemContainer` with a `SlotRule`, so each slot only takes items whose `ItemDefinition` gear slot matches; rings fit
  either finger). Slots: 12 armor slots, MainHand, Backpack, and a tool belt of Axe, Pickaxe and Sickle.
- **Held items**: `HeldItemView` (on PlayerArmature) shows the main-hand weapon in the right hand, or, while gathering, the
  belt tool for the job. Models come from `ItemDefinition` › Held In Hand (held model, else the world prefab; position,
  rotation, scale in the grip). Held models are visual only (colliders, rigidbodies and scripts stripped). The grip is a fist
  fitted to the rig's own fingers; tune items in **Safe Haven › Characters › Held Item Grips** (live in Play mode). Stand-in
  models come from the Kevin Iglesias demo props until LocalSpawn makes real ones.
- **`PlayerEquipment`** owns the `GearSet`. It applies each worn item's `StatBonus` list to `CombatStats` and removes it
  again when the item comes off or breaks. It exposes the weapon and `ToolTier`; tools only count while on the tool
  belt. Gear arriving through Give or crafting auto-equips into an empty matching slot.
- **Stats**: `Strength` adds physical power 1:1 (`CombatRules.powerPerStrength`). MaxHealth bonuses scale current
  health proportionally, so swapping gear never heals.
- **UI**: `UI/CharacterWindow.cs` (prefab `Prefabs/UI/CharacterHUD.prefab`, `UIWindowId.Character`, the C key) builds
  slot views at runtime and shows the stat sheet. `UI/ItemDrag.cs` is the shared drag context, so items drag between the
  bag and the character screen; it validates slot rules and swaps. While both windows are open, `ItemInventoryWindow`
  sits right of the character screen and shrinks if the screen is narrow.
- **Saving**: `PlayerSaveData.hasGear` and `gear`. Old saves without gear move equippable items out of the bag
  (`equippedSlot` is legacy). `PlayerSnapshot` carries the gear across scenes. A failed raid keeps gear that was worn
  at raid start, with durability loss, and loses gear looted during the raid.
- Worn gear is repairable at the `RepairBench` and counts toward collect-item quests. Tests: `Editor/Tests/GearTests.cs`.

## UI root, pause and the Escape menu

- **One UI root per scene.** Gameplay scenes hold one `UI Root` prefab instance (`Prefabs/UI/UIRoot.prefab`). It has
  the `UIStateManager`, the `EventSystem`, and `Screen Canvas`: the only root screen canvas and the only `CanvasScaler`.
  Every HUD and window prefab is a nested canvas under `Screen Canvas`. It is stretched to fill, has `overrideSorting`
  on and keeps its own sort order, so a change in one window only rebuilds that canvas. New screen UI goes under
  `Screen Canvas` and needs no `CanvasScaler`. World-space canvases (health bars, prompts) and the runtime loading
  screen stay separate. MainMenu and RaidResults are single-canvas scenes with a `UIRoot` component on their canvas.
- **UI scale:** `UI/UIRoot.cs` sits on the root screen canvas. `UIRoot.Current.Scale` (0.5–2) divides the scaler's
  reference resolution, so one value scales all screen UI. The future options page only needs to set and save it.
- **Pause:** `Core/GamePause.cs` owns pausing. Systems call `Request(owner)` / `Release(owner)`, and the game stays
  paused while any request is held. In single-player a pause freezes time (`Time.timeScale = 0`), which also stops the
  raid timer. For multiplayer, set `GamePause.FreezesTime = false`: the menu still opens and input is still blocked,
  but the world keeps running. `PlayerInputHandler` ignores gameplay input while paused. Hit-stop leaves
  `Time.timeScale` alone while time is frozen.
- **Escape:** `UI/PauseMenu.cs` (inside the UI Root, `UIWindowId.PauseMenu`) is the only Escape listener. It first
  offers the press to `Core/BackHandlers.cs` in priority order: build placement (300), then open windows (200,
  `UIStateManager.CloseAll`), then a timed interaction (100). The menu opens only if none of them used the press. To
  make something cancellable with Escape, register a handler in OnEnable and unregister it in OnDisable. Gamepad back
  (east button) still cancels in `PlayerInputHandler`.
- **Menu:** Continue, Save (disabled with the reason during a raid), Load (asks first), Options (camera style and
  per-situation overrides via `UI/CameraOptionsPanel.cs`; more to come) and Quit (to the main menu or the desktop). Audio is not paused. Tests: `Editor/Tests/PauseTests.cs`.

- **Camera styles:** V toggles between top-down and the action camera at any time. In action style the cursor is
  locked (mouse looks around, wheel zooms, `UI/ActionCrosshair.cs` shows the aim point) and released whenever a window
  or the pause menu is open. If an override forces the current situation, V still changes the choice and a message says
  which situation overrides it.

## Conventions

- **Data vs. behaviour:** authored values live in ScriptableObject definitions, which are never changed while playing.
  Runtime state lives in plain C# classes (`ItemContainer`, `QuestLog`, `RaidSession`) owned by a component or
  `GameSession`, which keeps rules testable without a scene (`Editor/Tests`).
- **Enums that are saved or serialized:** add values at the end and never renumber.
- **Statics:** the project enters Play mode without reloading scripts, so static state must not leak between play
  sessions. Either reset it in a `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method in the same class
  (`GameSession`, `GameEvents`, `Player`), or make it a registry that objects add themselves to in `OnEnable` and
  remove themselves from in `OnDisable` (`MonsterAI.Active`, `Interactable.Active`).
- **Listeners** subscribe in `OnEnable` and unsubscribe in `OnDisable` (or `OnDestroy` for persistent systems).
- **UI text** is TextMesh Pro (part of the uGUI package in Unity 6). Fields are typed `TMP_Text` and components are
  `TextMeshProUGUI`; don't add legacy `UnityEngine.UI.Text`. For an outline or shadow, use the material presets in
  `TextMesh Pro/Resources/Fonts & Materials` instead of the old Outline/Shadow components.
- **Hand-written URP shaders** (`Shaders/*.shader`) must declare the Forward+ reflection-probe keywords
  (`_REFLECTION_PROBE_BLENDING`, `_REFLECTION_PROBE_BOX_PROJECTION`, `_REFLECTION_PROBE_ATLAS`), as `LayeredLit` and
  `TreeWind` do. Without them, a renderer with a `MaterialPropertyBlock` (hit flashes, `SurfaceLayerController`) is
  drawn outside the SRP batcher with no reflections and turns noticeably darker.

## Generated models from LocalSpawn

LocalSpawn (the Local Generator app) exports models as GLBs into `Models/Generated/<asset>/`. `Editor/LocalSpawnMaterials`
makes glTFast import them with the project's shader instead of `Shader Graphs/glTF-pbrMetallicRoughness`:
**Safe Haven/Layered Lit** by default (base colour, normal, glTF ORM map, emission, plus two live layers), or the
shader named in the model's sidecar `<model>.material.json`. glTFast's importer has no setting for this, so the class
wraps glTFast's default material generator (reinstalled before every GLB import). Trees in `Models/Generated/Vegetation`
keep the Tree Wind flow unless they have a sidecar; cut-out or transparent materials keep glTFast's shader.

The sidecar (written by LocalSpawn's 3D export from the material editor's **Unity material** row) holds the shader, its
values (linear colours, floats, keywords), texture paths (the model's `_BlendMask.png`, layer sets in
`Art/Generated/SurfaceLayers/<name>/`) and the layer names (material tags `SurfaceLayer1/2`). Changing the sidecar or
a texture it names reimports the model. `SurfaceLayerController` finds layers by those tags, so
`GetComponent<SurfaceLayerController>().FadeTo("Moss", 1f, 30f)` grows the moss on any exported model.
`Editor/LocalSpawnPreview.Render` renders a model with its real material in a preview scene (LocalSpawn's
**Preview in Unity** calls it through the Unity CLI). Tests: `Editor/Tests/LocalSpawnMaterialsTests`.

GLB is the recommended format. FBX/OBJ exports in `Models/Generated/` get the same sidecar plus their maps as separate
files beside the model (`<model>_BaseColor.png`, `_Normal`, `_ORM_...`/`_MetallicRoughness_...`, `_Emissive`);
`LocalSpawnModelImport.OnPreprocessMaterialDescription` applies it through Unity's model importer and imports those
maps with the right settings (normal map, linear ORM). Multi-material FBX/OBJ keep Unity's own material.

| I want to... | Do this |
|---|---|
| Give an exported model another shader or values | LocalSpawn: Materials editor › Layers › **Unity material**, then 💾 Save + export model. In Unity, editing the model's material is not kept: it is rebuilt on reimport |
| Grow moss or rust on an object from code | Add `SurfaceLayerController` to the object and call `FadeTo("Moss", amount, seconds)` / `SetAmount` |
| Use a new custom shader for models | Put it in `Shaders/` with URP Lit's property names (`_BaseMap`, `_BumpMap`, `_ORMMap` + `_ORMMAP`, `_EmissionMap`, `_EmissionColor`); LocalSpawn lists it automatically |

## Look and lighting

| To change | Edit |
|---|---|
| Color grade, bloom, vignette of Safe Haven / the raid | `Settings/Rendering/Volumes/SafeHaven_Volume` / `MistyWoods_Volume` (each scene's Global Volume uses one) |
| Sun, ambient light, fog | The scene's Lighting window (Environment) and its Directional Light |
| Anti-aliasing, shadow quality, HDR, render scale | `Settings/Rendering/PC_RPAsset` (Mobile_RPAsset for the Mobile quality level) |
| Ambient occlusion | The SSAO feature on `Settings/Rendering/PC_Renderer` |
| A new place's mood | Duplicate a volume profile, assign it to the new scene's Global Volume, set its lighting |
