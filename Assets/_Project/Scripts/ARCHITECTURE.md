# Safe Haven – Code Architecture

A map of the scripts in `Assets/_Project/Scripts`: what lives where, how systems talk to each other,
and where to make common changes. Each script's own summary comment explains its details.

The *what and why* of the game lives in the design document,
[Docs/GameDesignDocument.md](../../../Docs/GameDesignDocument.md) (GDD). This file is the *how*.

Scene environment defaults: new scenes come from the **Safe Haven Scene** template (`Scenes/Templates`, pinned in
File › New Scene); its `SafeHavenScenePipeline` calls `SceneEnvironmentDefaults.AddTo`, which wires the scene through
`WeatherSetup` and `AtmosphereSetup` to the weather/day-night systems, shaft rendering and ground sampler. Scenes made by
code (tests, tools) are left as made; Edit Mode tests that need a scene use `TestScenes.StartClean`.
`TerrainMist` has calm rolling wave crests and stronger density in hollows and over `WaterSurface` footprints.
`FogGround` bakes RGBA terrain height, mist-support height, water mask and local depression depth;
water searches happen at bake time, not per raymarch sample. Global fog stays stationary and uses terrain height only.
Terrains added to weather scenes with a ground sampler receive weather/golden-hour-driven `TerrainMist`; existing
authored components are preserved. Private transient volume profiles are persisted beside the scene on first save.
`WeatherSystem.Weather Only Atmosphere` gates fog fully by weather and suppresses baseline/golden-hour terrain mist,
allowing random Mist/Fog weather with fully clear periods while shaft parameters remain independent.
`WorldToolsWindow` separates continuous fog setup from light-shaft setup; `ScenePlayability` completes atmosphere
after copying game rigs. See [Atmosphere](../../../Docs/Atmosphere.md).

Terrain geometry detail: `Editor/Environment/TerrainDisplacementEditor` configures per-terrain persistent height arrays,
layer UV/remap data and a dedicated world material. `TerrainSurfaceProfile` owns authored height sources and shape
settings; `TerrainSurface` owns per-terrain runtime quality overrides through MaterialPropertyBlock (future graphics
options hook). `Editor/Environment/TerrainSurfaceWindow` is the unified setup/tuning UI. The generated
`Shaders/Terrain/WorldTerrain*` passes share distance-adaptive tessellation/displacement and height-derived lighting
normals across all painted layers; colliders and NavMesh are unchanged.
`TerrainFootprints` owns a bounded, rolling RFloat GPU impression field per terrain; its stamp shader draws recovering
boot tread without changing source assets. `PlayerAudio.FootPlanted` shares animation/bone-based contacts with
`PlayerTerrainFootprints`, which raycasts the actual surface before stamping (not through bridges or water).
`PlayerAudio` ensures that contact routing exists on every runtime player, including players in other scenes.
Terrain displacement setup disables instanced terrain patches, which the current domain passes do not support.
Profile controls own lifetime, sole shape, depth and flattening; baked per-layer response excludes hard surfaces.
All terrain geometry passes and height-derived normals sample the same footprint field.
The four nearest active footprint fields also feed `Safe Haven/Grass`, so trampled plants recover with the ground
instead of hiding it immediately after the player passes. Globals are cleared on scene teardown/subsystem reset.
See [terrain displacement](../../../Docs/LevelDesign.md#true-terrain-displacement).

Mesh geometry detail: `Shaders/MeshSurface.shader` is an opt-in URP Lit surface with adaptive hardware
tessellation and world-metre height displacement. Forward-only lighting (also in Deferred), shadows, depth and
depth normals share `MeshSurfaceTessellation.hlsl`; the Meta pass exports the undisplaced surface for baking.
`Environment/MeshSurface` expands renderer-local culling bounds without modifying shared meshes or colliders.
`Editor/Environment/MeshSurfaceWindow` creates dedicated material copies and exposes all material controls.
Existing terrain and mesh materials are never globally replaced.

## Design reference: GDD sections and the systems that implement them

Status: **Done** = playable as designed, **Partial** = a working base that differs from or covers only part
of the GDD, **Not started** = design only. Update this table when a system is added or the GDD changes.

| GDD section | Status | Implemented by | Differences / missing |
|---|---|---|---|
| §5 Gameplay loops | Partial | `RaidPortal` → `RaidLauncher` → `RaidDirector` → `RaidReport`; `SettlementSystem`, `PurificationAltar`, `HeartTree` | The first-90-minutes loop works: home → raid → home, purification and the first ward upgrade. Later loops (seasons, endgame) not built |
| §6 Safe Haven, ward breaches | Partial | `SafeHaven.unity`, `HeartTree`, `SettlementState`, `WardBreachDefinition` + `WardBreachDirector`, `HomeRevival`, `NightProwlers` | Only the first scripted breach (wolves attack only the player, buildings can't be damaged). No later breaches or defender NPCs. After it is won, weak wolves prowl outside the light at night (§6.3, see [atmosphere](../../../Docs/Atmosphere.md)) |
| §7 Raids, timer, planning | Partial | `RaidDefinition`, `RaidSession`, `RaidParameters`, `RaidLauncher` (Portal Sparks), `RaidPreparationWindow` (destinations, Spark stepper, briefing) | 12 min timer and Portal Sparks (+3 min, max 2, paid once) as designed; raid modifiers are crafted runes. Danger stages escalate the raid (`RaidStage`, see "Raid tension"). No objectives |
| §8 Extraction, loss | Partial | `ExtractionPoint` (lit once, then held: `ExtractionHold`, `ExtractionZoneView`; windows, availability, cost, alarm), `RaidRisk`, `PlayerSnapshot.AfterFailedRaid`, `DurabilitySettings`, safe pocket in `ItemInventory`, `RaidSummaryWindow`, `PlayerArrivalPoint` | Death/time-out rules as designed (deployed gear kept at −25% durability). No companions or carts |
| §9 Raid world, events | Partial | `LootCache`, `ItemDropTable`, `GameEventTrigger`, `RescuableTraveler` | One hand-made map (`Raid_MistyWoods`) with a side path, a traveler and the Old Smithy Chest; no modular assembly or random events |
| §10 Movement, combat, enemies | Partial | `Player*`, `PlayerEquipment`, `PlayerDodge`, `PlayerGuard`, `Combat/*` (`ComboRules`, `GuardRules`, `PoiseMeter`, `CombatTechniqueSettings`), `MonsterAI`, `MonsterAttack`, `MonsterProjectile`, `MonsterPack`, `MonsterHowlBuff`, `AttackTelegraph` | Slow, deliberate melee: timed combo chains with finishers, heavy attack, guard and parry, flinch, monster poise and stagger, perfect dodge and counters (see "Combat technique"). Composable enemy tactics/temperaments and multiple attacks; small ranged troll, low-hover charging Mistwing, wolf packs/howl. Monsters notice through senses: a sight cone blocked by cover, a close sense, hearing; the player can sneak (see "Senses and stealth"). Apex: the Old Moss Troll walks a round (`MonsterPatrol`) and is heard coming (`CreatureFootfalls`). Player weapons: melee and a bow (`PlayerArchery`, see "Combat technique" › Bow). Dodge is **0.18 s / 2 s cooldown** (GDD: ~0.35 s / 2–3 s). No boss |
| §11 Survival, farming | Partial | `Survival/*` (`PlayerNeeds`, `NeedMeter`, `SurvivalSettings`, `WaterSource`), campfire recipes | Hunger and thirst as decided (never lethal). No farming or meal buffs |
| §12 Resources, inventory | Done | Materials are items (`Data/Items/Materials`, each with a weight, bag capped at 150 by `ItemContainer`'s weight budget). Essences only in `ResourceInventory/*` (weightless bars). `GatheringNodeDefinition` + `ResourceSource`/`ResourceNode`, `MaterialPool`, `ItemInventory`, `BackpackCapacity` (slots + carry bonus), `Gathering/*` (axe/pickaxe/sickle timing minigame, raid noise, Forest Annoyance, raid summary stats). Every tree can be felled with an axe: tree prefabs carry the shared Tree node and `ChoppableTree` (logs by axe tier and tree size, a chance of Hardwood/Elderwood: `TreeChoppingSettings`, Systems Editor › Tree Chopping); painted terrain trees get invisible stand-ins near the player and shrink away when felled (`TerrainTreeFelling`, added to terrains by itself, on a runtime copy of the terrain data). Oak Wood is shown as Wood | |
| §13 Crafting, refining | Partial | `RecipeDefinition`, `RecipeBook`, `PlayerCrafting` (hand recipes only), `CraftingStation` + `StationQueue`/`WorkshopState`/`StationCrafting`/`StationWindow` (timed station queues; `StationMenu` is the text fallback), `Durability/*` (repairs), `PurificationAltar` | Stone → Copper → Iron ladder, station queues (pay on queue, keep working during raids, resources to storage first, output slot), repairs and purification work. No Essence enhancements |
| §14 Base building | Partial | Torch (light against the night: `VisibilitySource`, `AtmosphereLight`), `BuildPlacer` (G: preview inside the ward radius; buildings with Build Work become a `Construction` scaffold the player hammers up (E), the rest stand at once; "Upgrade or repair a building"; move for free; wheel/Shift+wheel rotate, grid + flush-beside-neighbour snapping, Alt = free; entrance yards kept clear; walls dragged in lines), `BuildPlacement` (footprint geometry), `BuildingDefinition` (Build Work, Renown, Upgrades: `BuildingLevel` cost/work/craft speed/queue/output/storage/renown), `BuildingObject`, `Construction` (level, scaffold, rising model, generated level dressing, saved), `SaveableEntity`; Safe Haven starts overgrown (`Overgrowth` on never-regrowing gathering nodes) with a ruined shed; `HavenRenown`/`HavenSettings`/`HavenProgress` (renown, ranks that widen the ward), `HavenHud`, the Heart Tree's Haven page (H); decorations Lantern Post, Haven Banner, Herb Planter | Building damage; real art for level dressing (generated from primitives now) |
| §15 NPCs, companions | Partial | `RescuableTraveler` → `ResidentDefinition`, `JobDefinition`, `ResidentNpc`, `ResidentSpawner`; `Companions/*` (`Companion`, `CompanionDefinition`, `CompanionOrder`, `CompanionTactics`) | One recruit (Ingrid) and one job (Alchemist). Companions fight beside the player with orders (follow, stay, defend, retreat, attack my target) given in conversation; the prologue expedition uses them. Knocked down, they get up after a while: no player revive, permanent death or raid deployment yet. No needs or relationships |
| §16 Skills, levels, bosses | Partial | `SkillDefinition`, `SkillSchool`, `SkillLoadout`, `PlayerSkills`, `SkillWindow`; levels and talents in `Progression/*` (`CharacterProgress` in `GameSession.Progress`, `ExperienceTracker`, `PlayerTalents`, `TalentDefinition` × 20 in four free-mix trees, `LevelingSettings`, `ExperienceStone`), `TalentWindow` (N), `ExperienceHud`, the Rune Book | Essence enhancements and bosses |
| §17 Quests, trade | Partial | `Quests/*`, `Events/*`, story graphs (`Story/*`: `StoryScript` in the scene with `[SerializeReference]` `StoryNode`s and links, edited in `Editor/Story/StoryGraphWindow` (GraphView); see [story graphs](../../../Docs/StoryGraphs.md)); chain "The First Days" + side quest "A Stranger in the Mist" | No trade/bargain or reactive world |
| §18 UI, accessibility | Partial | `UI/*` (TMP), `DialogueBox` (conversations at the bottom of the screen: live `DialoguePortrait` of the speaker, name, typed-out line, answers 1-4; QuestGiver, companions and story graphs talk through it), `ActionMenuWindow` (world menus), `UIStateManager`, `UITheme` | No rebinding, text scaling or reduced-shake option |
| §19 Art, audio | Partial | `Audio/*` (a `SceneAudio` "Scene Music" component per scene), volumes `SafeHaven_Volume` / `MistyWoods_Volume`; environment in `Environment/*` (terrain moss, tree wind, `SurfaceLayerController` moss/rust on LocalSpawn models, grass and glowing plants, water and waterfalls, day/night and weather) | Composed music (`Audio/Safe Haven - *.mp3`) set per scene and as game-wide defaults in `AudioSettings`; effects from a CC0 library (`Audio/SFX`, credits in `Audio/SFX/CREDITS.md`): combat, footsteps by ground layer (`PlayerAudio`), quests, crafting, building, interface, rain, wind, thunder and stream loops (`WorldAudio`), gathering. Models are placeholders. Day/night and weather are visual only; the Haven's hour is saved (`DayNightCycle` is an `ISaveable` on a SaveableEntity, and time spent on a raid moves the clock on), the weather is not (§11 "weather influences plans" not built); only SafeHaven has the sky rig, not the raid scene. Volumetric fog, fog of war (unexplored ground, the night veil) and fire in the fog: `Environment/Atmosphere`, see [atmosphere](../../../Docs/Atmosphere.md) |
| §20 Technical design | Done | Definitions vs runtime state vs `Save/*`; rules in settings assets (`GameSettings<T>`) | Raid crash recovery and seeds not built |
| §21 First 90 minutes | Done (prototype), prologue added | `Prologue_MistyWoods.unity`, `Prologue/*`, `SafeHaven.unity`, `Raid_MistyWoods.unity`; quest chains "Prologue - The Unquiet Forest", "The Lost Expedition", "The First Days"; `Playtest/*` | Prologue uses a separate authored scene, not a raid. Three story escorts (Sigrid, Eirik, Runa) with camp topics; Bjarne, Tor, Liv and Ingrid are left out to be rescued later (Ingrid already has a rescue; the others are not authored yet). Weapon choice, cooking, troll ambush and the old troll are story graphs. Shelter and waystones are blockout art. |
| §23–24 MVP / milestones | M0–M3 (no boss-earned skill enhancement yet), plus M4's recruit with a job and M5's scripted ward breach | – | Next per GDD: M4 companions (down/revive) and cart transport; M5 boss, environmental event and polish |

## Folders

| Folder | What it holds |
|---|---|
| `Core` | Game-wide plumbing: `GameSession` (in-memory game state), `GameSystems` (starts persistent systems), `SceneLoader` (scene changes; thin front for `SceneTransition`, the async loader with the loading screen and `ISceneSetup`), `GameStateManager` / `UIStateManager` (scene singletons), `GamePause` + `BackHandlers` (pause ownership, Escape routing), `GameMessages` (on-screen text), `GameSettings<T>` (rule assets loaded from `Resources`), `PlayerArrivalPoint` (where the player appears at home), `AmountText`, `Tiers` |
| `Events` | The game-event bus (`GameEvents`, `GameEvent`) and designer-facing pieces: `GameEventDefinition` (Quest Events), `GameEventTrigger` (zones/interactions raise events), `GameEventListener` (events trigger scene changes) |
| `Player` | The player: `Player` (the front door to the player's parts), movement, input, combat, `PlayerEquipment` (worn gear, weapon and tool belt; see "Equipment"), interaction, `PlayerPersistence` + `PlayerSnapshot` (carrying the player across scenes, and what a failed raid keeps) |
| `Combat` | Stats, damage, `Damageable` (health, death, invulnerability, revive), attacks and hit detection, and combat feel: `CombatFeedback` (hit stop, shake), `HitReaction` (flash, punch, knockback), `CombatFeelSettings`. `Faction` (Player, Enemy, Neutral, Ally): the player and Ally (companions, travellers) are one side with no friendly fire (`FactionRelations.AreAllies`); Enemy AI hunts both (`WillAttack`). Kills carry the fallen side (`GameEvent.KilledFaction`), so a quest's "kill 5 enemies" (`QuestObjective` › Faction, used when no monster is set) never counts the player or a companion falling |
| `Companions` | People who fight beside the player. `Companion` (on the NPC: carries out its order, walks, is knocked down and gets up, takes orders in conversation through `IConversationTopics`; its plain C# parts are `CompanionLeader` (whom it follows and what they last hit), `CompanionWeapon` (swings and arrows at the strike delay) and `CompanionBody` (Animator and the weapon in its hand)), `CompanionDefinition` (a kind of companion: attack or arrow, order ranges, the orders it takes and its answers; `Data/Companions`), `CompanionOrder` and `CompanionTactics` (plain C#, tested in `CompanionTests`: which enemy, where to stand, per order). Monsters go for a priority target first while they can reach it (`Combatant` › Priority Target, set on the player prefab; `TargetPriority`, `MonsterTargeting.CanReach`); a companion only when the player is out of reach. A definition also gives the weapon in their hand (`HeldItemView.CreateModel` at the definition's grip for their humanoid rig; a bow in the left hand), optional animations (Archer: `ArcherAnimations` override whose Attack is a bow draw) and the strike delay. Orders in conversation can be switched off per companion (the prologue expedition follows the story). Content: `AgentScripts/BuildCompanions.cs`. A new order = a `CompanionOrder` value at the end + its case in `CompanionTactics` + an answer line in the definitions |
| `AI` | `MonsterAI` (the state machine: idle, investigating, chasing, attacking, staggered, returning; leash and home) and its plain C# parts: `MonsterTargeting` (whom it may hunt, Player Only, avoided ground, reachability, a better target in sight), `MonsterMovement` (the NavMesh: walking, stopping, turning, charging), `MonsterAttacks` (carrying out a strike and its actions: projectiles, charges, howls, cooldowns) and `MonsterPose` (body language of wind-ups, staggers and hovering); its animation driver and `AttackTelegraph` (ground warning during a wind-up). `AI/Perception`: how monsters notice things (`MonsterSenses`, `Awareness`, `PerceptionRules`, `WorldNoise`, `AwarenessIndicator`, `PerceptionSettings`; see "Senses and stealth"). `MonsterPatrol` (walks a route of waypoints through `MonsterAI.Roam`, keeps its monster awake), `CreatureFootfalls` (step sounds and camera shake of heavy creatures) |
| `Camera` | `PlayerCameraController` (top-down and over-the-shoulder action style on one Cinemachine camera, framing per game state), `CameraPreferences`/`CameraStyleRules` (which style applies), `CameraPointer` (mouse vs screen-centre aiming) and `CameraShake` (Cinemachine extension) |
| `Items`, `ResourceInventory`, `Crafting`, `Content` | Data definitions (ScriptableObjects) and their runtime containers. `Crafting/MaterialPool` is the one place costs are paid from |
| `Survival` | Hunger and thirst: `PlayerNeeds` (on the player), `NeedMeter` (one meter's rules), `SurvivalSettings`, `WaterSource` (drink and fill waterskins) |
| `Durability` | Wear on deployed equipment: `DurabilitySettings` (loss per failed raid, repair cost share), `Repairs` (cost and repair), `RepairBench` |
| `Building` | `BuildPlacer`: the build menu (G), placing, moving buildings, snapping, yards and line drawing. `BuildGhost` (plain C#): the see-through preview and the yard markers. `IBuildArea` / `BuildArea.Current`: where and what may be built (the `HeartTree`'s ward, the `PrologueDirector`'s camp: its guidance, ground, spot rules and buildable list), so the placer knows neither. `BuildPlacement`: pure footprint/yard/snap/line geometry (tested in `BuildPlacementTests`) |
| `Settlement` | Safe Haven as a place: `SettlementState` (portal, ward level, breach, residents, purification queue), `SettlementSystem` (persistent; ticks purification), `SettlementSettings`, `HeartTree`, `WardBreachDefinition` + `WardBreachDirector`, `HomeRevival`, residents and jobs (`ResidentDefinition`, `JobDefinition`, `ResidentNpc`, `ResidentSpawner`, `RescuableTraveler`), `PurificationAltar` |
| `Interaction` | `Interactable` – anything the player interacts with on E. Its **Kind** says how it runs (`InteractionKind`): **Work** over Duration with a work bar and animation (gathering, repairing; Duration 0 = until `Complete()`), or **Use**, done on the press with no player state, animation or bar (talking, chests, stations, portal, altar, lighting an exit). Set it in data; `BuildingPrefabBuilder` makes stations Use. Side concerns are small interfaces: `IInteractionCondition`, `IInteractionVerb`, `IInteractionNote`, `IInteractionName` (the prompt's name). `Talkable` is the one way to talk to someone: on Use it opens a DialogueBox conversation gathered from every `IConversationTopics` on the object in component order (the first with a line speaks it; each adds answers: `QuestGiver` offers, `PrologueCompanion` camp topics, `Companion` orders, `ResidentNpc` jobs), and after an answer opens again with its follow-up line. It holds the speaker's name, portrait, camera distance and an optional talked event. A `QuestGiver` without a Talkable starts or hands in on the press (notice boards) |
| `Raids` | Raid definitions, modifiers, the `RaidLauncher` (departure, Portal Sparks), the `RaidDirector` that runs a raid scene, extraction, loot caches, the portal. `Raids/Map`: painted raid maps (`RaidMapDefinition`, `RaidMapZone`, `RaidZoneGrid`, `RaidMap`, `MapRotation`, `RaidMapMarkers`; see "Raid maps") |
| `Progression` | Levels, experience and talents (see "Levels and talents") |
| `Quests` | Quest data (`QuestDefinition`, `QuestObjective`), per-player progress (`QuestLog`), `QuestManager`, `QuestGiver` (state-aware: marker, start, hand-in), `QuestMarker` + `QuestMarkerSettings` |
| `Audio` | `AudioManager`, `SceneAudio` + `MusicPlaylist`/`MusicRules` (music), `SoundDefinition` + `DeathSound`/`SoundEmitter` (effects), `PlayerAudio` (footsteps, landing), `WorldAudio` (weather and water loops, thunder, interface sounds). Edited in **Safe Haven > Audio > Sound Editor**; the free library is set up by **Safe Haven > Audio > Set Up Free Sound Library** |
| `Environment` | `TerrainMoss`: disposable terrain-alphamap-masked PBR moss and optional leafy sprigs; `MossWind`: shared shader wind, driven by a Directional `WindZone` on the same object when present (moss, `TreeWind.shader` trees and particles share it; see [tree wind](../../../Docs/TreeWind.md)); `TerrainMossBloom`: animated moss paint of the Heart Tree ward circle (runtime copy of TerrainData, so the asset is untouched in Play mode); `SurfaceLayerController`: per-object amount (`SetAmount`/`FadeTo` by layer name, via `MaterialPropertyBlock`) of the two live layers of `LayeredLit.shader` ("Safe Haven/Layered Lit"), which grows tileable moss/rust/dirt over a Local Generator model from its exported `_BlendMask` (curvature, edge wear, cavity, coverage). Grass: `Grass.shader` ("Safe Haven/Grass") on instanced terrain detail prototypes, `GrassInteractor` (up to 8 objects that push grass, as shader globals), `GrassTerrainColour` (baked ground colour the grass is tinted with); `Editor/Environment/GrassSetup` builds the assets and prototypes and rebakes on terrain paint (see [grass](../../../Docs/Grass.md)). Water: `Water.shader` ("Safe Haven/Water": depth colour, refraction, caustics, flow, waterfalls, foam), `WaterSurface` (a pond/river interactors look up; switches the simple look for cameras without depth/opaque textures), `WaterInteractor` (player/stone ripples, wake foam, splashes, or constant churn), `WaterRipples` (GPU ripple height field around the player, `Shaders/Resources/WaterRipples.shader`), `Waterfall` (generated falling sheet + plunge); `Editor/Environment/WaterSetup` makes the assets, creates water and carves beds (see [water](../../../Docs/Water.md)). Sky: `DayNightCycle` (+ `DayNightProfile` asset: sun/moon light, ambient probe, fog, reflections, Night/Golden Hour volume weights, `_SafeHavenNight`), `WeatherSystem` (+ `WeatherDefinition`s and a `ClimateDefinition`: wind via `MossWind.SetWeather`, `RainEffect`, Overcast volume, lightning); `Editor/Environment/WeatherSetup` adds the rig, `SkyControllerWindow` previews and drives it (see [weather](../../../Docs/Weather.md)). Grass also has glowing mushrooms/flowers, Nordic plants (fern, blueberry, lingonberry, heather, wild flowers) and `GrassSetup.FillByGround` (grass and plants by ground layer, none under water or objects). `TreeWind.shader` has Fast Leaf Lighting for leaf cards. `GeneratedTexturePostprocessor` gives new generated materials with a blend mask that shader (Assets > Safe Haven > Use Layered Lit converts others). `Editor/Environment` contains setup, `TreeWindMaterials` (Local Generator trees → Tree Wind materials and `_Wind` prefab variants), deterministic `ForestGroundTextures`, native Amplify graph creation (`ForestGroundShaders`) and preview-only `ForestGroundShowcase`. See [terrain moss usage](../../../Docs/TerrainMoss.md) for maps, materials and budgets. No gameplay/save coupling |
| `Save` | The save system, in layers (see below), plus `SaveableEntity` / `ISaveable` for objects in scenes |
| `Environment/Atmosphere` | The look of the world in the way of Gord ([atmosphere](../../../Docs/Atmosphere.md)): `AtmosphereFeature` (URP Render Graph feature on PC_Renderer, `Shaders/Atmosphere.shader`: half-resolution volumetric fog ray march with shadows and fire light, full-resolution composite with the fog of war), the volume overrides `AtmosphericFog` and `FogOfWar`, `VisibilitySource` (sight and light: lifts the night veil, thins fog, reveals ground, `IsLit` for night creatures), `AtmosphereLight` (a Light that glows in the fog), `ExplorationMap` + `ExplorationGrid` (explored ground, saved at home), `FogGround` (baked ground heights: fog that follows the terrain), `FogArea` (pockets of extra fog), `AtmosphereConditions` (weather fog, fog density for monster sight). `Environment/LightFlicker` for fire. `Environment/SeeThrough` + `Shaders/SeeThrough/` (Gord-style dithered see-through of whatever stands between camera and player; in Tree Wind, Layered Lit and the See-Through copies of URP Lit/Unlit). Designer tools: `Editor/Environment/AtmosphereSetup` (Set Up Open Scene, presets, Fog Area), `SeeThroughMaterials` |
| `UI` | Views and windows. They only read game state and send requests; no game rules live here. `ActionMenuWindow` is the shared menu for world objects (Heart Tree, portal, residents, altar, repair bench, build menu). `StationWindow` is the crafting-station window (recipe icon grid with `RecipeTabs` tabs, selected recipe + Queue, queue grid with progress bar, output grid, extension actions as buttons); its prefab is generated by Safe Haven > UI > Build Station Window (`Editor/UI/StationWindowBuilder`). `ItemTooltipView` also describes recipes and bare definitions (`ShowRecipe`, `ShowDefinition`), used for hover tooltips on station cells and on the bag's hand-recipe rows. `UIRoot` (root canvas, UI scale) and `PauseMenu` (Escape menu); see "UI root, pause and the Escape menu" |
| `Editor` | The Content Editor (`Editor/Content`), the Systems Editor (`Editor/Systems`), the level tools (`Editor/Level`), the UI theme tool (`Editor/UI`), prefab builders, placeholder generators and Edit Mode tests (`Editor/Tests`) |

**Assemblies:** `SafeHaven` (everything outside `Editor`), `SafeHaven.Editor` (`Editor`, editor-only) and `SafeHaven.Tests`
(`Editor/Tests`). Editing editor tools or tests no longer recompiles the game code. Runtime code can't see editor code;
`internal` members are visible to the editor and tests (`Core/AssemblyInfo.cs`, `Editor/AssemblyInfo.cs`). A new
package used by game code must be added to `SafeHaven.asmdef`'s references. A `[SerializeReference]` class
created before the split carries `[MovedFrom(sourceAssembly: "Assembly-CSharp")]`.

## How systems talk to each other

### Designer-authored prologue

`Data/Prologue/ForestPrologue.asset` owns the ordered `PrologueChapter` list: quest, behaviour, automatic-entry
messages, HUD text, duration and initial enemy count. Sigrid's ordinary `QuestGiver` component owns the ordered
**Offers** for peaceful chapters: quest reference, briefing text, acceptance label and waiting text.
Her `Talkable` makes the offers a conversation (close-camera framing, walk-away close); reading or closing it does not
accept anything. Set a chapter quest's Start to **By Quest Giver**, then author its offer on the giver.
Acceptance uses `QuestGiver → QuestManager → QuestLog`; there is no director acceptance API or leader flag.
The director does not run an unaccepted chapter's timer/encounter.
Automatically-started emergency chapters need no conversation; a fighting chapter (camp defence, portal hold) says
its arrival line and starts at once: in a fight a line without answers is a subtitle that leaves the controls to the
player (`DialogueBox`), so nothing waits for it, and story graphs can Spawn Enemies before Say (Troll Ambush). Add or reorder chapters together with their quest prerequisites in
the existing Quest Map; chapter list order does not rewrite the prerequisite graph. A chapter waits for its quest
to complete. Reusable behaviour presets handle shelter, fuel, fire, rest, camp defence, travel, activation and the
portal hold; new kinds of scene mechanics still require code. The same asset holds the field building, building radius,
starting armor, dialogue camera distance scale, waiting-for-leader text, and each fighting chapter's own
Enemies (prefab, weight, from wave: wolves at the camp; wolves, small moss trolls and from the second wave mistwings at
the portal; ExpeditionSmallMossTroll/ExpeditionMistwing are unsaved copies made by `AgentScripts/BuildPrologueFinale.cs`,
which also sets the one-minute hold), wave interval, first-wave size, wave growth/caps, rumble, ending/recovery/homecoming text and transition timing. It is never mutated at runtime.

Select an expedition NPC in `Prologue_MistyWoods` to edit its `PrologueCompanion`: resident identity, forest/camp/
portal dialogue, camp topics (questions the player can ask around the camp, from gathering fuel until the wolves) and
the alchemist's healing amount/interval; the speaker's name, camera distance and TalkedTo event are on its `Talkable`. Walking and fighting are its `Companion`
(formation offset, Fighter or Archer definition): the director's rally points become Stay orders, no rally is Follow,
and it stands still until the expedition sets out.
When an NPC also has a `QuestGiver`, keep it above the `PrologueCompanion`: the first part with a line speaks it (the
offer before the camp chatter), and every part adds its answers (`IConversationTopics`).
The intro's other moments are story graphs under **Intro Moments** in the scene (`Docs/StoryGraphs.md`): the choice of
bow or axe, Eirik's waterskin for the cooking chapter (**Berries for the Pot**, started automatically after the fire),
the small trolls' ambush in the berry clearing, and the old troll walking towards the fire when the wolves are beaten.
Escorts stay put until the first briefing is accepted, and each stops walking while the player talks to them (`Talkable.IsTalking`).
Hand-mounted `ExpeditionTorch` objects keep their authored lit models upright during animation; their Light and
LightFlicker components are scene-editable. Scene `GameEventTrigger`s control discovery zones;
`PrologueInteraction` handles portal activation, with editable before/after-step prompts. `PrologueDirector` derives its
chapter from `GameSession.Quests`, owns temporary enemies/timers, and raises encounter/story facts. The camp quest
counts the cleared encounter, not player kill credit. No `RaidSession`, regular raid loot loss, result screen or
durability penalty is involved. Supplies deliberately reset to home starting contents.

`BuildPlacer` retains its ordinary placement, footprint checks, material payment and Built event. In the forest
the `PrologueDirector` is the build area (`IBuildArea`), which exposes only the prologue definition's Campfire, the same `Data/Buildings/Campfire.asset` the player builds at home
(Sticks and Small Rocks; upgrades only at home), only during the accepted building chapter, and only inside the
shelter's build radius. Keep the gathering objectives aligned with its Construction Items when tuning costs
(PrologueTests checks this). The fire quest counts **Build Structure**, not a use-fire event.
Its ground is the director's TerrainCollider (height resampled after snapping), and it rejects footprints whose
corners differ by more than 0.2 m from the base; the Heart Tree's ward is a flat plane.
`PersistentScene` and the campfire's registered prefab preserve the player's placement and resource depletion.
The expedition wolf prefab has no SaveableEntity: encounters intentionally restart at checkpoints.
Off-hand torch and armor are granted and auto-equipped once from the prologue definition, before the saved
Started event is raised; loading never grants duplicates. The forest player's inventory defaults are empty.
The definition's Starting Weapon is empty: the player picks the one-handed Woodsman's Axe or the Crude Wooden Bow (with
arrows) in the **Bow or Axe** story. L (`PlayerEquipment.ToggleTorch`) swaps the torch and a two-handed weapon. Torches are normal
`WeaponDefinition` / `ItemDefinition` / `RecipeDefinition` assets, crafted by hand for two Sticks. `WeaponKind.Torch`
allows the appended `GearSlot.OffHand` (17); existing saved slot indices stay unchanged. `GearSet` enforces hand
occupancy in its container rule as well as equip commands, so drag/drop and auto-equip cannot pair an occupied off
hand with a TwoHanded/Polearm weapon. Conflicting equips are rejected, not silently unequipped.
`HeldItemView` renders both hands, preserves only the torch's Light/LightFlicker alongside normal visual components,
and stows the off-hand light during gathering. **Held Item Grips** fits both palms and previews torches in the left hand.
Dialogue camera framing uses `PlayerCameraController`'s owned
BeginDialogue/EndDialogue override without changing saved zoom or camera style. The menu's close callback releases
it on close, replacement, walk-away or disable; combat closes conversations.

Safe chapter entries auto-save. Manual saves are blocked during combat/transition; loading restarts the chapter.
Early death revives at the chapter rally point; death during the hold and surviving its countdown both reach the
narrative homecoming. `PrologueHomecoming` starts the home Awakening event (also for legacy/direct-home starts),
then raises the homecoming event; the expedition-search quest uses ordinary **On Quest Event** start configuration.
It saves the first arrival for prologue survivors only. Portal inspection
is a normal quest event; its repair objective can count an already-raised event, preventing early repair softlocks.
The home portal allows inspection immediately; `RaidPortal.repairRequires` retains the existing repair quest gate
on the repair action itself, with designer-editable locked text.
`MainMenuView` keeps new-game destination and missing-save-scene fallback separate.

For safe designer playtesting use **Safe Haven > Quests > Play Prologue (Isolated Saves)**. It starts only the
prologue, preserves open scene setup, and redirects checkpoints to Unity's temporary cache before scene loading,
including across domain reloads. Stopping restores the previous start scene, background setting and normal storage.
Ordinary Editor Play is blocked when the prologue is loaded alongside other scenes without an explicit Play Mode
start scene: duplicate players and raid setup otherwise compete for the global player and spawn position.
Open the prologue alone for ordinary Play, or use the isolated preview while keeping other scenes open.

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

The quest scene index reads real `QuestGiver` references, including ordered Offers, in saved scenes and
prefabs. Sigrid is indexed exactly like any other giver; the index has no prologue binding rules.
A **By Quest Giver** quest without a placed giver still warns. Ordered offers select the first unfinished
quest without skipping locked prerequisites or required turn-in; old single-quest givers retain their
immediate-interaction behavior. **Select All Uses in Open Scenes** finds the same serialized references,
including unsaved edits. `QuestLog` owns progress; `QuestManager` alone dispatches lifecycle and authored
start/completion events through `GameEvents`, feeding them back into the log and designer reactions.

### Authoring dynamic quest reactions

1. Open the intended gameplay scene. In **Safe Haven > Content Editor**, select a quest (or select its node in
   **Quest Map** and open the Content Editor). Its **Scene Reactions** panel offers **On Quest Started** and
   **On Quest Completed**. Quest Event pages offer **When Event Is Raised**.
2. The button creates an undoable, always-active `GameEventListener` in that scene. Edit its ordered **Actions**
   directly in the same panel: **Play Music**, **Silence Music**, **Resume Music** (automatic scene rules),
   **Change Weather** (definition + transition seconds), **Set Object Active**, or **Parameters**.
3. **Parameters** uses UnityEvent's standard target/property/method picker, not reflection/property-path strings.
   Examples: Light intensity, AudioSource volume, component enabled, Animator trigger, and exposed gameplay setters.
   Unity's supported single float/int/bool/string/Object arguments are available; vector/multi-argument operations
   need a small explicit public adapter method. Optional **Response** remains compatible with existing listeners.
4. Save the **scene**, not just the quest asset. Shared quests/events never store scene object references. Keep the
   listener active; disable target objects, not its host. Missing action references report runtime errors explicitly.
5. To signal other quests or several scenes, add named events to a quest's **On Started** / **On Completed** lists.
   Legacy **Completion Event** still fires first on completion. The event index/Used By panel recognises both lists.
   **Play Test > Raise event now** on the event page exercises its listeners in Play Mode.
6. **Respond If Already Happened** restores state from saved quest/event history on scene load. Uncheck it for
   one-shot sounds/cinematics. Put persistent state and one-shots on separate listeners. For multiple historical
   reactions writing the same property, author an explicit final-state restore response/condition; listener order
   is not a timeline. **Already Happened Response**, when configured, replaces the optional Response only;
   typed Actions still restore. Changes to live weather/music are restored by listeners, not serialized as assets.

`SceneAudio.Start Silent` (and Silence Music) suppresses automatic/default/final-stretch music, not ambience; a fight
still brings combat music (the prologue's wolves and trolls), and event music beats both. Event music
holds priority until Silence/Resume/another Play Music action or leaving the scene. The forest starts silent and its
Hold Portal **Quest Started** listener plays the authored defence playlist, including checkpoint replay. Designers
can replace that clip or add weather, lights and object actions in the quest's Scene Reactions panel.

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
| Custom (Quest Events) | `GameEventTrigger`, `GameEventDefinition.Raise`, a quest's completion event and On Started/On Completed lists, and the Quest Event fields on `RescuableTraveler` (freed/rescued), `HeartTree` (talked to), `RaidPortal` (repaired), `JobDefinition` (job assigned, via `ResidentNpc`) and `WardBreachDefinition` (breach won). The Content Editor's "nothing raises this event" check finds these by the `[RaisedByGame]` attribute on the field; when you add a new Quest Event field that the game raises, mark it `[RaisedByGame]` |

### Base building: clearing, construction, upgrades, renown

Safe Haven starts overgrown: brambles, fallen logs, old stumps and boulders are gathering nodes (`Data/Gathering/Overgrowth`,
Respawn Seconds 0, so they never grow back) with an `Overgrowth` beside them (dark ground that heals when cleared). They
block building until cleared; the big stumps and boulders need tier 3 tools. A ruined Storage Shed (`Construction.Starts
Ruined`) waits to be repaired (hold E with its cost at hand). Placing a building with Build Work from the build menu
(`BuildPlacer`, existing placement rules) puts up a scaffold: holding E hammers it (dust, sound, a little shake), the
model rises out of the ground, and when done it pops, raises dust and the Built game event. Upgrades are ordered from the
build menu ("Upgrade or repair a building"): the next `BuildingLevel` is paid and the scaffold goes up again; each level
adds generated dressing (stone plinth and lanterns, then banners and a flower box) and its bonuses, read by
`CraftingStation` (Speed, QueueLength, OutputCapacity; a resident whose job's workplace is the building adds 50%) and
`StashChest` (slots). Looks and sounds: `Resources/ConstructionSettings`. `HavenRenown` adds up renown (every standing
building and its levels, a second of a kind counts half, cleared overgrowth, residents); `HavenProgress` (added to the
Heart Tree) celebrates each `HavenSettings` rank once and records it in `SettlementState.HavenRank`, which widens the
ward (`HeartTree.WardRadius`; the moss spreads out to the new edge). Content is built by `AgentScripts/BuildHaven.cs`.

### Fishing and the Nøkken

A Fishing Rod is a weapon of kind Fishing Rod (`WeaponDefinition.TakesAttackInput`, like the bow), so `PlayerFishing`
owns the attack and guard buttons while it is held: hold to swing back, let go to cast at the cursor (one bait from the
bag, best first: Grubs from rotten logs and stumps, or Berries), wait for the float to go under, strike, then the
reeling game (`ReelSession`, plain C#: keep the catch zone on the darting fish until the catch fills). Which fish bite
where (`WaterSurface.IsStill`: the Safe Haven tarn; rivers flow), bait, timings and the Nøkken are in
`Resources/FishingSettings`. In still water a struck bite can be the Nøkken (`NokkenEncounter`): it rises at the water's
edge, the camera closes in, it drags the player towards the deep and hurts them until Space is hammered enough (or it
wins and takes a third of their health); the first meeting raises Met The Nøkken. `FishingHud` draws it all. Content:
`AgentScripts/BuildFishing.cs`. Editor scripts that play a scene without opening it use `TemporaryPlayScene.Play`, which
clears Unity's play mode start scene again whenever Play mode ends.

### Levels and talents

`GameSession.Progress` (`CharacterProgress`, plain C#) owns the level, experience towards the next level, talent ranks
by id, the items crafted once and the experience stones touched; `SaveMapper` writes it to `SaveData.progress` (talents
that no longer exist give their points back). `ExperienceTracker` (a game system) turns game events into experience
with the amounts in `Resources/LevelingSettings`: monsters the player kills (`MonsterDefinition.Experience`, or from
health), completed quests (`QuestDefinition.Experience`, or the default), finished raids (more for extracting), the
first craft of each item, gathered nodes (`ResourceSource.Harvested`), explored ground (`ExplorationMap.Explored`),
read Rune Books (`ItemDefinition.Experience`) and `ExperienceStone`s (once per game each, remembered by scene and
place, so raids can't farm them). Each level gives a talent point. The four trees (Ranger, Soldier, Runemaster,
Tinkerer) are disciplines to mix, not locked classes: any point can go in any tree; row N opens after N × Points Per
Row in that tree. `PlayerTalents` on the player turns ranks into `StatModifier`s on its `Combatant`. Talent stats
beyond combat (`MaxStamina`, `MoveSpeed`, `DrawSpeed`, `GatherYield`, `ToolCare`) are read by `PlayerStamina`,
`PlayerMovement`, `PlayerArchery`, `ResourceSource` and `PlayerGathering`. Talents are reset for free beside the
Heart Tree. To add a talent: create one (Create > Safe Haven > Progression > Talent), give it a unique id, tree, row
and per-rank bonuses, and add it to LevelingSettings' Talents.

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
- **Exits are held, not waited at** (GDD §8.1): one press lights an `ExtractionPoint` (an instant interaction; the
  cost is paid and the `alarm` woken then; `IInteractionVerb` labels the prompt "[E] Light the way home"). From then
  `ExtractionHold` (plain C#, tested) fills only while the player stands inside `holdRadius` and no creature that would
  attack the player does: outside = Waiting, a creature inside = Contested; both pause, neither resets. Full = Done:
  `RaidDirector.Extract`. Every `callInterval` a lit beacon emits a `WorldNoise` of `callRadius` (35 m), so idle
  creatures come to look and the forest converges on the circle. A beacon whose window closes while lit gutters out and
  loses what was held. Hold time = the raid's Extraction Time × `timeMultiplier` (Misty Woods 30 s, Troll Valley 35 s).
  `ExtractionZoneView` (added automatically) draws the circle on the ground with the telegraph material, filling
  clockwise, coloured by state (dim unlit, gold waiting, green holding, red contested), plus a beacon light.
- **Exits are choices:** `ExtractionPoint` has an open window (`opensAtSecondsLeft` / `closesAtSecondsLeft`, pure
  `ExtractionWindow`), an `availabilityChance` rolled per raid by `RaidGenerator` (`RaidLayout.PickAvailable`, at least
  one exit always stays), a `timeMultiplier`, an optional item cost (checked as an `IInteractionCondition`, paid on
  lighting) and an optional `alarm` group woken when it is lit. Misty Woods: West Beacon (far, always open), East
  Beacon (70% of raids, goes dark at 4:00, alarm wakes two hunters), Hollow Oak (central, opens at 3:00, held in 60%
  of the time, inside the Old Moss Troll's round).
- **Time of day:** `RaidDefinition.TimesOfDay` (`RaidTimeOfDay`: name, hour, chance, loot multiplier, briefing line).
  `RaidTimeOfDay.For(raid, seed)` rolls one from the run's seed (tested); `RaidPreparationWindow` rolls the seed when it
  opens, shows the hour and passes the seed to `RaidLauncher.TryLaunch`, so the briefing is what the raid gets. The
  loot multiplier enters `RaidParameters.Calculate` like a modifier. `RaidSky` (an `ISceneSetup` on the raid's sky rig)
  sets the `DayNightCycle` hour and stops its clock. Night shortens monster sight (`AtmosphereConditions.Night`,
  `PerceptionSettings.SightAtNight`).
- **The apex:** the Old Moss Troll (Content Editor › Monsters; built by `AgentScripts/BuildOldMossTroll.cs`) stands under
  Map Content › Apex in Misty Woods with a `MonsterPatrol` round of four waypoints looping around the Hollow Oak. Weak
  eyes (9 m, 100°), a 6 m nose (close sense) and keen ears (×1.8); 1400 health, poise 450, a 1 s sweep and a 1.3 s
  360° stomp; it drops 2–3 Trollstone. `CreatureFootfalls` thuds every stride (`Audio/Sounds/Library/TrollStep`, heard
  55 m) and shakes the camera within 28 m. Patrols walk at `walkSpeedScale` (`MonsterAI.RoamSpeedScale`) and set
  `MonsterAI.KeepAwake`, so `CreatureSleep` leaves them walking far from the player.
- **Turned maps:** `RaidMap` turns Map Content a frame after placed creatures have started, so it calls
  `MonsterAI.ResetHome` on every monster under Map Content afterwards (their homes turn with them).
- **HUD:** `RaidHudView` shows the stage beside the raid name, the nearest *open* exit with its arrow (or the lit one,
  with "Holding… %", "contested!" or "Back to the circle"), every exit with distance, state and how much is held, and "At risk" (`RaidRisk.Of`: what a failed raid would cost, refreshed once a second).
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
| Make a one-off world event (e.g. "after the first raid, entering the ward blooms moss") | GameObject › Safe Haven › World Event (Zone + Response) or Ward Moss Bloom. The trigger has **When** (zone / interact / manually / `PlayerInsideArea`: inside any `IArea` set as its **Area**, such as the Heart Tree's ward), **Requires** (a `QuestCondition`: quest Started/Completed) and **Repeat** (`EveryTime` / `OncePerSceneLoad` / `OncePerGame`). OncePerGame events are remembered in `QuestLog.HappenedEventIds` (saved); the listener's **On Load (Already Happened)** response restores the world state, e.g. `TerrainMossBloom.BloomInstantly`. New reactions = a small component with a public method, wired in the listener |
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
| Tune hit stop, shake, hit flash, knockback, telegraph colors, effects, slash arcs, the hurt vignette | `Resources/CombatFeel` (Systems Editor › Movement & Combat). No code. |
| Tune combo timing, parry window, guard, flinch, poise and stagger | `Resources/CombatTechniqueSettings` (Systems Editor › Movement & Combat). No code. |
| Give a weapon a longer chain, a heavy attack or a counter | Content Editor › Weapons: **Combo**, **Heavy Attack**, **Counter Attack**. Each attack names its **Animation State**; rerun Safe Haven › Characters › Set Up Combat Animations to retime the built-in ones from their clips |
| Make a monster harder to stagger | Content Editor › Monsters › **Poise** (0 = the game-wide default) |
| Change how far or wide a monster sees, how well it hears, how quickly it spots you | Content Editor › Monsters › **Detection Range** (sight) and **Senses** (Sight Angle, Close Sense Range, Hearing, Alertness). All monsters side by side: Systems Editor › World › Senses & Stealth |
| Tune noticing, searching, sneaking, the player's noise, the ?/! marks and their sounds | `Resources/PerceptionSettings` (Systems Editor › World › Senses & Stealth). No code. |
| Make something new heard by monsters (a horn, a falling tree, a cart) | `WorldNoise.Emit(position, radius)` |
| Change a combat, footstep, world or weather sound | Sound Editor › Sound Effects › Game-wide effects (or the weapon's, attack's or monster's own sound fields) |
| Tune the dodge (distance, speed, i-frames, cooldown) | `PlayerMovementSettings` asset, Dodge header. No code. |
| Give a monster a longer attack warning | Raise its attack's **Hit Delay** (the telegraph lasts exactly that long) |
| Make one target react differently (a boss that can't be pushed) | Add `HitReaction` to its prefab and change its fields |
| React to the player dodging (sound, stamina) | Listen to `PlayerDodge.Dodged` |
| Tune hunger/thirst, carry weight, death and repairs, raid time and Portal Sparks, the ward, purification or breaches | Systems Editor (Safe Haven > Systems Editor). No code. |
| Add a raid destination | New `RaidDefinition` (Content Editor) + add it to the portal's destination list; base rules show in the Systems Editor |
| Change when a raid takes place (day/dusk/night chances, night's reward) | Content Editor › Raids › the raid › Rules › **Time of day** |
| Change how long an exit is held, its circle or how loudly a lit beacon calls | The raid's **Extraction Seconds** (Content Editor › Raids), and on the scene's `ExtractionPoint`: **Time Multiplier**, **Hold Radius**, **Call Radius** / **Call Interval** |
| Give a new map fog, fog of war and night | Safe Haven › World › Sky and Weather › Fog › Set Up Open Scene, then › Apply Preset; pockets with GameObject › Safe Haven › World › Fog Area. See [atmosphere](../../../Docs/Atmosphere.md) |
| Make a tree, roof or prop dissolve when it hides the player | Safe Haven › Rendering › Make Materials See-Through › Selected (URP Lit/Unlit → See-Through copies; Tree Wind and Layered Lit already do). Size of the circle: `SeeThrough` on PlayerArmature |
| Change the mist, the night veil or how dark unexplored ground is | The scene's volumes: **Atmospheric Fog** / **Fog of War** overrides (SafeHaven_Volume, SafeHaven_Night, MistyWoods_Volume). Independent sun/moon shafts: Atmospheric Fog's **Shaft Density / Intensity / Height**. Drifting terrain clouds: select a Terrain, **Safe Haven > World > Sky and Weather > Fog > Add Drifting Mist to Selected Terrain**, then adjust **TerrainMist** (density, finite height band, cloud size/coverage/erosion, wind, edge fade). Uses FogGround heights and the existing raymarch, up to four nearest tiles; visual only. No code. See [atmosphere](../../../Docs/Atmosphere.md) |
| Make something light the night (fog glow, veil lifted, prowlers kept off) | `Light` + `AtmosphereLight` (+ `LightFlicker`), and a `VisibilitySource` with **Is Light** |
| Make a torch push back physical mist | `AtmosphereLight` on a point Light: **Clearing Radius / Strength / Softness**, default 4 m / 1 / 0.6. Removes base fog, FogArea, TerrainMist and shaft particles in a soft 3D sphere; limited by Light range, follows enabled/intensity. HeldItemView preserves/adds it for equipped torches; ExpeditionTorch attaches it to expedition point lights in Awake. No fog-of-war reveal or gameplay sight changes. |
| Tie fog/mist to existing weather and golden hour | `WeatherDefinition.Mist` blends in `WeatherState`; existing `WeatherSystem` publishes fog/wind/mist through `AtmosphereConditions`, `DayNightCycle` publishes its existing cloud-reduced golden-hour curve. `TerrainMist` combines independent Base/Weather/Golden Hour amounts. `AtmosphereFeature` integrates weather-scaled drift once per game-time interval for fog clumps, banks and terrain clouds, avoiding time-times-speed phase jumps. No second weather/clock simulation. |
| Tune night creatures at home | `NightProwlers` on "Night Prowlers" in SafeHaven (prefabs, how many, interval, distance, light margin) |
| Spawn something that must never be saved | Plain `Instantiate`: a copy made while playing has no save id, so it isn't saved. Only `SaveableEntity.Spawn` makes a saved copy (placed buildings) |
| Make a monster walk a round | Add `MonsterPatrol` to it in the scene and give it a **Route** (an object whose children are the waypoints, under Map Content in raids). Heavy footsteps: `CreatureFootfalls` on the prefab |
| Say what pressing E does on an object | Implement `IInteractionVerb` next to its `Interactable` |
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
| Fill an area with trees, rocks or resource nodes by hand | Safe Haven › World › Place › Scatter Brush with a palette (or Content Editor › Prefabs › **Paint with Scatter Brush** for one prefab). New palette: Create › Safe Haven › World › Scatter Palette. Painted objects go under "Level Scatter" in the scene. Raid maps are painted with the Map Painter instead |
| Lay a path or a trodden clearing into the terrain | GameObject › Safe Haven › World › Path (a spline you can move later; the old path grows back), or Safe Haven › World › Terrain › Path Painter to paint once |
| Place rocks, trees or other prefabs on the ground | World Tools (Safe Haven › World Tools, or the Scene view's Safe Haven World panel) › Place: click a picture, click in the Scene view. With **Stick to ground** on, anything dragged in, duplicated or moved settles on the ground |
| Make a scene playable as a home or a raid | World Tools › Scene › Make Playable (or Safe Haven › Scene › Make Playable as …); the list below it says what is still missing, with fixes |
| Add a river, cliff or forest | GameObject › Safe Haven › World › River / Cliff / Dark Forest Area / Haven Grove Area, then click points on the ground. It builds itself whenever you change it; see [level design](../../../Docs/LevelDesign.md) "World Builder" |
| Paint a whole terrain by its shape | The terrain's World Terrain › Dark Forest Look or Haven Look, then Auto-Paint with Rules (rules: Data/World/Paint Rules - …) |
| Check a level before committing it | Safe Haven › Scene › Level Check, or `unity command sh_level` |
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
  `SettlementState`. `ResidentSpawner` places residents in Safe Haven; `ResidentNpc` offers their jobs in conversation (`Talkable`).
- **Purification:** `PurificationAltar` queues batches in `SettlementState`; `SettlementSystem` ticks them everywhere
  (also during raids) while someone holds the Alchemist job.

## Gathering bootstrap and tiers

`Tiers.Min` is 0; Tier 0 is shown as `0`, not an absent tier. Sticks, Small Rocks, and Berries are hand-picked Tier 0 items/nodes. SafeHaven contains four Sticks and four Small Rocks nodes under **Tier 0 Forage**, with separate save ids and 90-second regrowth. The new items have simple placeholder models, pickup prefabs, and rendered icons.

`ItemInventory.GatheringTier` returns 0 without a tool. Players with `PlayerEquipment` must equip a matching, unbroken tool; inventory-only users use their best carried tool. `ResourceNode.CanInteract` enforces the requirement before preview and interaction start. `GatherTool.None` is explicitly hand-pickable; Tier I tool nodes no longer accept bare hands.

The three first stone-tool recipes consume Sticks and Small Rocks, not Oak Wood/Grey Stone. Stone Hatchet has tool strength 1; Stone Pickaxe intentionally keeps strength 2 to bootstrap copper. Item tier and tool strength are not interchangeable. Tests in `BootstrapProgressionTests` and `InteractionTests` (category `GatheringProgression`) cover the assets, hand-picked yields, crafting, and tool gating.

`Interactable` ignores inactive visual colliders when measuring reach (including the repaired portal's hidden model); without a live solid collider it uses the interaction origin. `PlayerInteractionOutline` on the player prefab outlines nearby interactables or the first unobstructed collider under `CameraPointer` (mouse or action-camera crosshair). It uses disposable mesh/skinned-renderer children (`OutlineCopies`, also used by the bow's red `ArcheryTargetOutline`) and shared `InteractionOutline`/`InteractionOutlineMask` materials, never changes original materials, hides depleted/disabled visuals and while windows/pause are open, and casts no shadows. The URP shaders draw a depth-tested, three-pixel amber silhouette, not an x-ray or a promise that tool requirements pass. A colourless mask reserves stencil bit 0 for the original silhouette; the expanded double-sided hull draws only outside it, so thin ground-level sticks do not lose their contour to buried backfaces. They run at queues 2998/2999, after opaque rendering, so URP Deferred does not override their stencil states. Tests: `InteractionReadability` (both orthographic and perspective rendered-pixel checks).

The eight starter forage nodes have slightly larger model silhouettes and 1.1-metre local clearings in SafeHavenTerrain's detail layers; vegetation elsewhere is unchanged. Light equipment still uses Quickstep, now with `Slide_Start` instead of a landing-only clip; distance, stamina, invulnerability, load thresholds and cooldown are unchanged.

## Editors for designers

| Editor | For | Menu |
|---|---|---|
| Content Editor | Things: items, resources, recipes, buildings, monsters, quests, raids, residents, jobs, ward breaches. A raid page has tabs: Overview, Rules, Enemies, Resources, Loot & Events, Escalation, Systems, Preview | Safe Haven > Content Editor |
| Systems Editor | Rules and numbers: survival, carrying, death and repair, movement and combat, raids and the portal, the Heart Tree and building, residents and purification, breaches, loot and rarity. Edits the settings assets and shows derived numbers (minutes until hungry, raids until gear breaks, raid length with Sparks) | Safe Haven > Systems Editor |
| UI theme | Window, button and text colors of every UI prefab | Safe Haven > UI |
| Level tools (`Editor/Level`) | Building the world by hand: **Scatter Brush** paints weighted `ScatterPalette`s (`Data/Level/Palettes`: forest trees, rocks and logs, wood and stone nodes, forage) on the ground with spacing, slope tilt and sink (Shift erases); **Path Painter** paints a ground layer along a curve with a frayed edge, clears grass and smooths the ground; **Drop to Ground** (End / Shift+End aligns to the slope) sets objects down by the bottom of their meshes; **Level Check** finds floating, buried, unreachable, under-water, crowded and off-terrain gameplay objects, save id problems, missing scripts and leftovers, with fixes, and counts the level's contents. See [level design](../../../Docs/LevelDesign.md) | Safe Haven > World, Safe Haven > Scene |
| World Tools (`Editor/Level/Tools`) | The level designer's hub. `WorldToolsWindow` (tabs Place, Terrain, Water & Plants, Sky & Weather, Scene; its buttons run the Safe Haven › World / Scene menu items, listed in `MenuPaths` and checked by `WorldToolsTests`), `WorldToolsOverlay` (Scene view panel), `PrefabCatalog` (level prefabs by folder and name into designer groups), `PrefabPlacer` (click-to-place with a drawn preview, never a scene object; parents under Level Props or a raid map's content), `GroundStick` + `PlacementSettings` (EditorPrefs; drag-in, duplicate and Move-tool drops settle on the ground via `GroundPlacement`, joining the move's undo step), `ScenePlayability` (Make Playable copies the gameplay roots from SafeHaven.unity or Raid_MistyWoods.unity with `Unsupported.DuplicateGameObjectsUsingPasteboard` so references between them hold, stands the player, adds the kind's pieces, bakes the NavMesh, adds to Build Settings; `Check` lists errors, warnings and notes with fixes) | Safe Haven > World Tools, Safe Haven > Scene |
| Level spawners (`Level/MobSpawnZone`, `Level/ZonePolygon`, `ResourceInventory/RandomResourceNode`) | `MobSpawnZone`: a drawn polygon that spawns creatures from its list or a Spawn Table on start, when the player comes near or on `Spawn()`, refills on a timer, raises `Cleared` (editor: `MobSpawnZoneEditor` handles). `RandomResourceNode`: becomes a gathering node of a kind and tier range from `ContentDatabase.GatheringNodes`. `ResourceNode` applies `GatheringNodeDefinition.ModelVariants`/`VariantScale` per spot. See [level design](../../../Docs/LevelDesign.md) | World Tools › Place |
| World Builder (`Level/World`, `Editor/Level/World`) | Non-destructive, spline-based world building. Runtime assembly holds only data: `WorldFeature` (a `SplineContainer` plus settings; `RiverFeature`, `PathFeature`, `CliffFeature`, `BiomeArea`), `KnotValues` (per-knot multipliers kept with their knots), `WorldTerrain` (on a terrain: settings and what was built, by hash), `WorldTerrainStash` (asset: the ground before any feature, per touched height/paint/detail cell, and removed trees), `CliffKit`, `TerrainPaintRules`, and `ScatterPalette` (moved here so scene components can use palettes). Editor: `WorldBuilder` watches feature edits, spline changes, deletions and hand sculpting, and rebuilds the affected terrains after the mouse is released (each build joins the triggering edit's undo step); a build reads the heights (absorbing hand edits: base += current − last), runs each `FeatureBuilder` (River/Path/Cliff/Area) in order over `HeightLayer`/`PaintLayer`/`ClearLayer` working sets built from `LineField` distance fields of the sampled `SplineLine`, makes room in the terrain if needed, restores cells no feature touches any more, clears and restores grass and terrain trees, moves grounded objects with the ground (`GroundGlue`) and regenerates each feature's `Generated` objects (river water mesh with per-vertex flow and a `WaterSurface` centre line, waterfall plunges, cliff pieces fitted to the face, scatter). `WorldAssets` makes the CC0 prefabs, palettes, ground layers, cliff kit, paint rules and materials; `WorldPaint` auto-paints by slope/height/hollowness; `WorldEditors` has the draw mode and knot handles; `WorldMenus` the GameObject menu. `Shaders/Terrain/WorldTerrainLit` is URP Terrain Lit plus triplanar rock on steep ground, generated by `AgentScripts/GenerateWorldTerrainShader.py`. Tests: `WorldBuilderTests`. See [level design](../../../Docs/LevelDesign.md) | GameObject > Safe Haven > World, Safe Haven > World |
| Sky Controller | Time of day, clock speed and weather, in edit and Play mode | Safe Haven > World > Sky and Weather > Sky Controller |
| Environment HUD | Read-only top-centred clock/phase timeline and vector weather symbol, bound to the scene sky rig; no separate clock or saved state (see [weather](../../../Docs/Weather.md)) | Safe Haven > UI > Add Environment HUD to Scene |
| Planner import (`Editor/Planner`) | Reads `manifest.json` from the external Progression Planner app (`D:\Safe Haven\ProgressionPlanner`, outside this repo). Shows New / Changed / Error entries with old → new values and writes only after confirmation. Never deletes or overwrites; matches existing assets by GUID, then id. Kinds: items, essences, buildings, recipes, gathering nodes and weapons (`WeaponDefinition`, keyed by asset name; edited, never created: new weapons come from the Content Editor). Items carry their weapon reference and armor stats (`stats` lines). CLI: `sh_planner_preview`, `sh_planner_apply` | Safe Haven > Planner > Import Planner Data |

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
| `sh_level --fix --census` | edit | The Level Check on the open scene: contents and problems; `--fix true` applies every fix it can (undoable, not saved) |

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
  (`AddInvulnerability` / `RemoveInvulnerability`, which can stack). A hit avoided this way raises `Damageable.Evaded`.
- Effects, sounds and the rest of the feedback are listed under "Combat technique" below; see [combat](../../../Docs/Combat.md).

## Combat technique: slow, deliberate, dangerous

The player is no super soldier: combat is about reading the enemy and committing, not clicking. Rules are in
`Resources/CombatTechniqueSettings` (Systems Editor › Player › Movement & Combat); the design is in [combat](../../../Docs/Combat.md).

- **Chains** (`PlayerCombat`, rules in `ComboRules`, tested): a weapon's `combo` list is walked by presses *in time*
  after each swing; a press before the swing lands (mashing) fumbles the chain back to the first swing; a press right
  as a swing recovers builds a small rhythm bonus. The last step is the finisher (more damage and poise). Holding the
  button is the **heavy attack** (`heavyAttack`); a ready counter uses `counterAttack`.
- **Commitment and flinch:** a swing can't be dodged out of until it lands (the dodge is buffered); the player moves
  slowly while swinging; a hit that gets through while winding up loses the swing and staggers the player (`Flinched`).
- **Guard and parry** (`PlayerGuard`, an `IHitGuard` that `Damageable` asks before applying a hit; `GuardRules`):
  right mouse / left trigger. Frontal hits are blocked (part gets through, stamina pays); without stamina the guard
  breaks and the player reels; raised within the parry window before a hit it parries: no damage, the attacker
  staggers (`MonsterAI.Stagger`) and a counter is ready. Attack while guarding = shield bash with a shield, else it drops
  the guard. Events: `Damageable.Guarded` / `AnyGuarded`, `DamageResult.Guard`.
- **Poise** (`PoiseMeter` on every `MonsterAI`; `MonsterDefinition.Poise`, attacks' `Poise Damage`): at zero the monster
  enters `State.Staggered` (attack cancelled, extra damage taken, `MonsterAI.AnyStaggered`). `AttackTelegraph` turns
  the parry colour and glints for the last parry window of the wind-up.
- **Perfect dodge:** a hit evaded during the dodge's invulnerability readies a counter (`PlayerCombat.PerfectDodge`).
- **Bow** (`WeaponKind.Bow`, `WeaponStance.Bow`; design in [combat](../../../Docs/Combat.md) › The bow): `PlayerArchery`
  takes the attack button while the main hand holds a bow (`PlayerCombat` ignores it then): hold to draw, let go to
  loose the first `ItemDefinition.Arrow` stack in the bag, then reload. The bow's numbers are on `WeaponDefinition`
  (Bow header: draw/reload seconds, arrow speed, spread, minimum draw, draw move speed, `shotAttack`); the arrow's on
  `ArrowDefinition` (damage, speed multiplier, gravity scale, `ArrowProjectile` prefab, hit sound, impact noise).
  `ArcheryRules` (pure, tested) holds the draw curves, spread and the launch angle that lands on the aim point.
  `PlayerArchery.Target` is the hostile monster nearest the cursor/crosshair in line of sight; `ArcheryTargetOutline`
  outlines it red (`OutlineCopies`, shared with `PlayerInteractionOutline`; `TargetOutline.mat`). `ArrowProjectile`
  (pooled) flies a swept arc, damages through `DamageCalculator` + `Combatant.ReportDamageDealt`, sticks in scenery and
  emits `WorldNoise`. Presentation: `PlayerArcheryAnimation` (the animator's "Bow" layer, under "Upper Body"; it also
  turns the spine after animation so the draw line points at the aim, since the archer clips stand sideways) and
  `HeldBowView` (string between the bow rig's tips, pulled to the right hand, and the nocked arrow),
  `ArcheryCameraAim` (`PlayerCameraController.SetAiming`), `ArcheryTrajectoryView` (arc preview for long shots) and
  `QuiverHud` (made by `CombatFeedback`: arrows, draw bar, quick shot). Technique: perfect release
  (`ArcheryRules.IsPerfectRelease`, forced crit), guard button aims with a bow (`PlayerGuard` ignores it then),
  the counter a perfect dodge readies looses an instant shot (`PlayerCombat.SpendCounter`), Z cycles arrow kinds
  (`PlayerStateMachine.ArrowCycleRequested`). Fire arrows add `Burning` (Fire ticks marked `HitTechnique.OverTime`,
  which `CombatFeedback` keeps quiet); stuck arrows may be `Recoverable` (walk over them). Content:
  `AgentScripts/BuildArchery.cs`; the layer: `CombatAnimationSetup.BuildBowLayer` (part of Set Up Combat Animations).
  Tests: `ArcheryTests`.
- **Focus** (`PlayerFocus`): earned from `SwingLanded`, parries, perfect dodges and blocks, drains after a hold; skills
  with a `SkillDefinition.FocusCost` need it (`SkillCastResult.NoFocus`). Shown as the gold bar in `PlayerVitalsView`
  (added by Safe Haven › UI › Add Focus Bar to Vitals HUD).
- **Dodge by gear weight:** `PlayerEquipment.EquipLoad` (armour, rings, weapon) picks a `DodgeStyle` from
  `PlayerMovementSettings` (Quickstep / Roll / Heavy Roll, each a `DodgeShape` of multipliers, move speed and animation);
  `PlayerDodge.DodgeStarted` plays it; the heavy roll ends in the `shoulderBarge` attack. While the dodge cools down the
  stamina bar glows red around its edge (`PlayerVitalsView`, from `PlayerDodge.CooldownFraction`); the glow going out
  means it is ready.
- **Committed eating** (`PlayerEating`, Survival): `ItemInventory.Use` sends food and drink there; 1.3 s, slowed, no
  attacking/guarding/skills, interrupted (food kept) by a hit or dodge; healing arrives over time.
- **Monster body language:** `AttackPose` (pure, tested) coils a monster's Model through the wind-up, lunges at the hit
  and sways it while staggered; `MonsterAI.LateUpdate` applies it with the hover.
- **Hits carry** `DamageInfo.Poise` and `Technique` (`HitTechnique`: Heavy, Finisher, Counter, Rhythm, ShieldBash);
  `AttackResolver.Execute` takes the swing's damage and poise multipliers.
- **Animation:** swings are played by crossfading into the attack's **Animation State** by name
  (`PlayerAnimationController.OnSwingStarted`, `ActionSpeed` parameter), so the animation starts with the gameplay swing.
  Standing swings and dodges play on the **Combat Full Body** layer (legs included), running swings on Attack Arms; the
  wind-up plays at Animation Speed and, from the hit, the strike at Follow Through Speed.
  The player's body is Kevin Iglesias' HumanM (`PlayerBodySetup`, Safe Haven › Characters › Use Human Body for Player).
  States and timings come from **Safe Haven › Characters › Set Up Combat Animations** (`CombatAnimationSetup`: imports
  the Quaternius UAL1/UAL2 clips, adds the states to `PlayerAnimator` and a Stagger state to `MonsterHumanoid`, measures
  each clip's contact moment from the weapon hand's speed and writes Hit Delay / Recovery, builds the weapons' chains).
- **Feedback** (`CombatFeedback`): pooled spark effects for hits, critical hits, blocks, parries, staggers and perfect
  dodges, a `SlashEffect` arc along each swing ("Safe Haven/Slash"), short hitches on parries and perfect dodges, the
  `CombatScreenEffects` vignette (hurt flash, low-health pulse), an optional `ComboHud`, and sounds from Audio Settings
  (attack, then weapon, then game-wide). Effects are built by **Safe Haven › Combat › Build Combat Effects**
  (`CombatEffectsBuilder`, Prefabs/Effects/Combat, "Safe Haven/Effect Particle"). Text callouts are empty by default.

## Senses and stealth: monsters notice, the player hides

Monsters no longer know about anything inside a radius; they see, hear and lose track (GDD §10.8). Rules are in
`Resources/PerceptionSettings` (Systems Editor › World › Senses & Stealth); each monster's eyes and ears are on its
`MonsterDefinition` (Detection Range = sight range, plus Senses: Sight Angle, Close Sense Range, Hearing, Alertness).

- **Awareness** (`Awareness`, plain C#, tested): a level 0–1 and the place of the last thing noticed. Suspicious from
  `SuspiciousAt`, Alerted (spotted) at 1, sticky until the monster loses track or calms down. `MonsterSenses` (owned by
  `MonsterAI`) fills it: an `OverlapSphere`, then per candidate `PerceptionRules.NoticeRate` (close range from any
  direction; otherwise only inside the cone, slower with distance; scaled by visibility × alertness) and a line of
  sight raycast to the chest or the head (`HeadAboveTarget`, so a bump in the ground hides nobody) against `SightBlockers` (terrain, terrain trees, the baked "Tree Trunks", rocks, buildings; characters
  with a NavMeshAgent or CharacterController never block). Seeing nothing drains it.
- **Hearing:** `WorldNoise.Emit(position, radius)` first raises `WorldNoise.Emitted` (`CreatureSleep` wakes sleepers
  inside it), then calls `MonsterAI.Hear` on every awake monster: `PerceptionRules.HeardLevel` lifts awareness to at
  least suspicious but never to spotted, so a sound only brings them to look. Sources: `PlayerStealth` (footsteps,
  dodges, hits landed or taken), `GatheringNoise` (each swing), a monster that spots its target (a call), and
  `GatheringNoise.Investigate` (Forest Annoyance "Angry").
- **`MonsterAI.State.Investigating`** (added at the end of the enum): idle, roaming or searching monsters that are
  suspicious walk (at `InvestigateSpeedScale`) to the stimulus, clamped inside their leash and defensive radius, then
  look around for `SearchSeconds` and walk home (roamers resume their roam). A new glimpse sends them there instead.
  Spotting, a hit or a pack alert turns it into a hunt where it stands.
- **Hunting and losing track:** `SetTarget` alerts awareness and, when it was not already alerted, shows the "!", plays
  `SpottedSound` for the player (once per pack) and emits the call. While hunting, `TrackTarget` checks `CanSee`
  (chase range, no cone, line of sight, or close range); out of sight for `LoseTrackSeconds` or beyond chase range, the
  monster `LoseTrack`s: it investigates the last known position at `LostTrackAwareness` (a pale "?"). `Relentless`
  monsters (ward-breach attackers, hunt waves, alarms) never lose track; `Engage` still targets at once.
- **Idle gaze:** idle monsters glance up to `IdleGlanceAngle` either side of the way they first faced, every
  `IdleGlanceSeconds`, so a sneaking player has to time their approach.
- **Ambush:** an unaware monster has `Damageable.DamageTakenMultiplier = AmbushDamageTaken` (`MonsterAI.RefreshAmbush`;
  a stagger still sets its own multiplier).
- **The player** (`PlayerStealth` on PlayerArmature, `Player.Stealth`): hold Left Ctrl / left bumper to sneak
  (`SneakSpeedScale` through `PlayerMovement.SetSpeedScale`). `Visibility`: fighting (for `FightingSeconds` after a hit
  landed or taken) > moving > still > sneaking; sight range scales with √visibility between `Min/MaxRangeScale`.
  Other targets (the traveller, residents) are always fully visible.
- **Presentation:** `AwarenessIndicator` (created on a monster the first time it notices anything) floats a "?" that
  brightens with awareness (amber; pale while searching for a lost target) and a red "!" for `SpottedShowSeconds`, in
  the quest markers' font. `SpottedSound` is `Audio/Sounds/Library/Spotted` (the heavy bells pitched down);
  `SuspiciousSound` is empty until a creature sound exists.
- **Gizmos:** a selected monster draws its sight cone (yellow), close sense (orange), chase range and leash.
- Night, fog and light will plug in through visibility and sight range, the only inputs `PerceptionRules` needs.
- Tests: `Editor/Tests/PerceptionTests.cs` (rates, cone, sneaking range, hearing, stages, line of sight).

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
  (an `ItemContainer` with a `SlotRule`, so each slot only takes items whose `ItemDefinition` gear slot matches; there is
  one ring). Slots: 7 armor slots (Head, Body, Hands, Feet, Belt, Amulet, Ring; Shoulders, Back, Wrists, Legs and the second ring are retired: `GearSlots.IsRetired`, numbers kept for saves, gear found there moves to the bag on load), MainHand, Off Hand, Backpack, and a tool belt of Axe, Pickaxe and Sickle.
- **Held items**: `HeldItemView` (on PlayerArmature) shows the main-hand weapon in the right hand (a bow in the left), or,
  while gathering, the belt tool for the job. Models come from `ItemDefinition` › Held In Hand (held model, else the world prefab; position,
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
  Scalers use Expand, so the whole 1920x1080 layout stays on screen at any aspect (on ultra-wide the height sets the
  size). Screen canvases made in code (HUDs, dialogue box, loading screen) call `UIRoot.ConfigureScaler` instead of
  setting up their own `CanvasScaler`.
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
  which situation overrides it. The top-down cursor lean is measured from the ground under the screen centre, not the
  player, so the view holds still while the cursor does; aiming a bow leans 1.5x further and blocks right-mouse orbit.

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
| Anti-aliasing, shadow quality, HDR, render scale | `Settings/Rendering/PC_RPAsset` (Mobile_RPAsset for the Mobile quality level). Shadows are fitted to the top-down camera (14–28 m away): 2 cascades split at 30 m, 50 m distance. Lowering quality for frame rate belongs in player options, not these defaults |
| A heavy model's triangle count (Local Generator trees reach 100k) | **Safe Haven › Art Pipeline › Simplify Model Meshes** (`Editor/Environment/ModelSimplifier`): picks a Unity mesh-LOD level per mesh, bakes it to `Simplified/` beside the prefab and points the prefab at it (vertices and wind data kept; level 0 restores). Trunks and rocks simplify well; leaf cards lose whole cards, so leave leaves alone. The oak trunks round the Haven use it |
| Ambient occlusion | The SSAO feature on `Settings/Rendering/PC_Renderer` |
| A new place's mood | Duplicate a volume profile, assign it to the new scene's Global Volume, set its lighting |
