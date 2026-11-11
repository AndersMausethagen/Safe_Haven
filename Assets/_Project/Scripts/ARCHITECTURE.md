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
`MistVolume` and the **Mist Volume** prefab provide movable, rotatable local clouds without terrain or a ground bake;
the nearest four share the terrain cloud kernel, with independent density, bounds, flow and optional weather control.
Shaft lighting has a separate half-resolution target and bounded chromatic composition; only actual fog/mist
adds extinction. This preserves saturated surfaces and darks under golden sunlight or cool moonlight.
`FogGround` bakes RGBA terrain height, mist-support height, water mask and local depression depth (once in play, at Start: the ground once per cell, still water only over its bounds, rivers stamped by centreline segment via `WaterSurface.WorldCentreline`; testing every water at every cell of the 1 km Haven took 70-150 s on entering play, now under a second);
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
The Haven's terrain is the standard for footprints and ground shape: a new `TerrainSurfaceProfile` starts from its values and `TerrainDisplacementEditor.UseStandardFootprints` brings an existing terrain in line.
A layer added to a terrain takes its height settings from any profile where it is already tuned (the Haven's first), and `TerrainLayerWatcher` rebuilds a tessellated terrain's displacement when its layer list changes in the Editor (`Editor/Level/Tools/GroundLayerPainter`). The generated shader's steep-ground rock is displaced too (`WorldCliffOffset` in the hand-written `WorldTerrainDisplacement.hlsl`): along the normal, by the rock mask's blue height, triplanar, around `_CliffHeightMidpoint` measured at setup. Rock comes in two kinds (rocky ground, `_Cliff*`; Cliff Rock on the steepest, `_Steep*`), placed by `WorldRock`: by slope, or with `_RockFromLayers` by the painted weight of layers whose profile entry is marked Rocky/Cliff (`TerrainSurfaceProfile.RockKind`, layer data row 2 green), so painting replaces it; `Editor/Environment/TerrainRock` paints them by slope; faces past `_RockAlwaysFrom` (50°) are rock regardless (paint from above stretches there). Ground relief is calibrated in metres by `Editor/Environment/GroundRelief` (midpoint = height map mean, amplitude from its 1st-99th percentile spread); `VegetationPackConverter` reads pack height maps raw (not through their sRGB import) and never through the HDRP mask height remap, which is blend amplitude/base, not a 0-1 range. Layer albedo with Large Colour Variation is clamped to white: above it the lighting produced values bloom and motion blur spread into flashing black and white blocks.
Mined nodes crack apart: a `GatheringNodeDefinition` with Break Apart pieces has its models cut ahead of time by `Editor/Content/MeshCracker` (`NodeFractureBuilder`, Safe Haven › Setup › Resources › Crack Mined Nodes; run it after changing its models or pieces) into a `NodeFracture`: solid pieces in the model's own space with rough broken faces in a second submesh. When gathered, `RockChunk`s take the model's place exactly and fly apart, and its Burst Yields are granted on top of the yield, with no second experience. How they fly and the inside's material are on the family's `HarvestFeedbackProfile`; sounds come from `Tools/Audio/mining.py`.
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
| §5 Gameplay loops | Partial | `RaidPortal` → `RaidLauncher` → `RaidDirector` → `RaidReport`; `SettlementSystem`, `PurificationAltar`, `HomePortal` | The first-90-minutes loop works: home → raid → home, purification and the first ward upgrade. Later loops (seasons, endgame) not built |
| §6 Safe Haven, ward breaches | Partial | `WardBreachDirector`, `WardBreachDefinition`, `WardBreachSettings`, `BreachLanes`, `BuildingRepairs`, `BreachHud`: warning, preparation, defense, recovery, see "Ward breaches". Missing from the design: runic ward totems, resident guards, watch post arrows. After the first is won, weak wolves prowl outside the light at night (§6.3, see [atmosphere](../../../Docs/Atmosphere.md)) |, `Territory/*` (regions claimed at boundary stones and held against the forest's pushes: Docs/Territory.md)
| §7 Raids, timer, planning | Partial | `RaidDefinition`, `RaidSession`, `RaidParameters`, `RaidLauncher` (Portal Sparks), `RaidPreparationWindow` (built in code: destination tabs, tier picker, Spark stepper, modifiers; the briefing at a glance with its details on hover) | 12 min timer and Portal Sparks (+3 min, max 2, paid once) as designed, but `MistyWoods.asset` is at 300 s for now while its stages and exits are written for 720 (see the TODO); raid modifiers are crafted runes. Danger stages escalate the raid (`RaidStage`, see "Raid tension"). Each run's reason is a rumour (`RaidRumourDefinition`, see "What only raids give"); no other objectives |
| §8 Extraction, loss | Partial | `ExtractionPoint` (lit once, then held: `ExtractionHold`, `ExtractionZoneView`; windows, availability, cost, alarm), `RaidRisk`, `PlayerSnapshot.AfterFailedRaid`, `DurabilitySettings`, safe pocket in `ItemInventory`, `RaidSummaryWindow`, `PlayerArrivalPoint` | Death/time-out rules as designed (deployed gear kept at −25% durability). No companions in raids. Carts come through the portal and home again: see "Transport" |
| §9 Raid world, events | Partial | `LootCache`, `ItemDropTable`, `GameEventTrigger`, `RescuableTraveler` | Misty Woods is generated anew each run from rules (`RaidLayoutRules`, see "Generated raid maps"), not from modules; a raid without rules plays the painted map. Random events: `Raids/Events/*` (ten for Misty Woods, see "Raid events") |
| §10 Movement, combat, enemies | Partial | `Player*`, `PlayerEquipment`, `PlayerDodge`, `PlayerGuard`, `Combat/*` (`ComboRules`, `GuardRules`, `PoiseMeter`, `CombatTechniqueSettings`), `MonsterAI`, `MonsterAttack`, `MonsterProjectile`, `MonsterPack`, `MonsterHowlBuff`, `AttackTelegraph` | Slow, deliberate melee: timed combo chains with finishers, heavy attack, guard and parry, flinch, monster poise and stagger, perfect dodge and counters (see "Combat technique"). Composable enemy tactics/temperaments and multiple attacks; small ranged troll, low-hover charging Mistwing, wolf packs/howl. Monsters notice through senses: a sight cone blocked by cover, a close sense, hearing; the player can sneak (see "Senses and stealth"). Apex: the Old Moss Troll walks a round (`MonsterPatrol`) and is heard coming (`CreatureFootfalls`). More creatures, built by `AgentScripts/BuildCreatures.cs`: Marsh Light, Draug, Bog Spitter, Blight Boar, Rootling swarms and the Moss Shaman (a `MonsterAttackKind.Summon` action). Status effects: `StatusEffectDefinition` + `StatusEffects` (Poison, Chill, Roots; `AttackDefinition` › Applies Status, cured by `ItemDefinition` › Cures). Shields: `WeaponKind.Shield` in the off hand. Player weapons: melee and a bow (`PlayerArchery`, see "Combat technique" › Bow). Wading and swimming in ponds and rivers (`PlayerSwimming`: swimming forward costs stamina, empty stamina swims slower, never drowns; held items are put away; no diving or swimming clips yet). Dodge is **0.18 s / 2 s cooldown** (GDD: ~0.35 s / 2–3 s). No boss |
| §11 Survival, farming | Partial | `Survival/*` (`PlayerNeeds`, `NeedMeter`, `SurvivalSettings`, `WaterSource`), campfire recipes | Hunger and thirst as decided (never lethal). Meals with timed bonuses and tonics: `PlayerBoons`, `ItemDefinition` › Boon Bonuses (two meal slots, `SurvivalSettings` › Meal Slots; carried through the portal, not written to the save). Farming: a Farm Plot is a station whose recipes are crops, so they grow during raids |
| §12 Resources, inventory | Done | Materials are items (`Data/Items/Materials`, each with a weight, bag capped at 150 by `ItemContainer`'s weight budget). Essences only in `ResourceInventory/*` (weightless bars). `GatheringNodeDefinition` + `ResourceSource`/`ResourceNode`, `MaterialPool`, `ItemInventory`, `BackpackCapacity` (slots + carry bonus), `Gathering/*` (axe/pickaxe/sickle timing minigame, raid noise, Forest Annoyance, raid summary stats). Every tree can be felled with an axe: tree prefabs carry the shared Tree node and `ChoppableTree` (logs by axe tier and tree size, a chance of Hardwood/Elderwood: `TreeChoppingSettings`, Systems Editor › Tree Chopping); painted terrain trees get invisible stand-ins near the player and shrink away when felled (`TerrainTreeFelling`, added to terrains by itself, on a runtime copy of the terrain data). Oak Wood is shown as Wood | |
| §13 Crafting, refining | Partial | `RecipeDefinition`, `RecipeBook`, `PlayerCrafting` (hand recipes only), `CraftingStation` + `StationQueue`/`WorkshopState`/`StationCrafting`/`StationWindow` (timed station queues; `StationMenu` is the text fallback), `Durability/*` (repairs), `PurificationAltar` | Stone → Copper → Iron ladder, station queues (pay on queue, keep working during raids, resources to storage first, output slot), repairs and purification work. Gear runs Stone → Copper → Iron → Moonsilver, with jewellery, shields and meals (`AgentScripts/BuildGear.cs`, `BuildFood.cs`). New stations: Kitchen, Tanning Rack, Farm Plot and Beehive; a crop is a recipe with long Craft Seconds. Essence uses: see "Essence" |
| §14 Base building | Partial | Progression: [base progression](../../../Docs/BaseProgression.md): the Workshop, Woodcutter's Lodge and Smithy (assets ConstructionTable, Sawbench, Forge; ids kept for saves) have tiers I to V; `BuildingUnlocks` reads `BuildingDefinition.Requires`, `RecipeDefinition.RequiredStationTier` and `StationQueue.Tier` gate recipes, `Construction` switches on a model's `Tier N` parts, `HavenReadiness` adds up what the base gives raids. `WorkerProduction` runs residents' `JobDefinition` work orders from the stash, `HomeHunting` keeps game outside the light by day, and `SettlementState.NoteBuildings` remembers what stands for raids, which can't see it. Content: `AgentScripts/BuildBaseTiers.cs` then `BuildFoundation.cs`, which run after `BuildHaven`/`BuildFood` and override their numbers. Torch (light against the night: `VisibilitySource`, `AtmosphereLight`), `BuildPlacer` (G: preview inside the ward radius; buildings with Build Work become a `Construction` scaffold the player hammers up (E), the rest stand at once; "Upgrade or repair a building"; move for free; wheel/Shift+wheel rotate, grid + flush-beside-neighbour snapping, Alt = free; entrance yards kept clear; walls dragged in lines), `BuildPlacement` (footprint geometry), `BuildingDefinition` (Build Work, Renown, Upgrades: `BuildingLevel` cost/work/craft speed/queue/output/storage/renown), `BuildingObject`, `Construction` (level, scaffold, rising model, generated level dressing, saved), `SaveableEntity`; Safe Haven starts overgrown (`Overgrowth` on never-regrowing gathering nodes) with a ruined shed; `HavenRenown`/`HavenSettings`/`HavenProgress` (renown, ranks that widen the ward), `HavenHud`, the Haven window (`HavenWindow`; H, or ask the portal), where the base is run from: the Overview tab draws a `HavenOverview`, and Stores, Buildings and People are `HavenListPage`s drawing `HavenEntry` lists read by `HavenStores`, `HavenBuildings` and `HavenPeople`, with buttons to build, order an upgrade, give a job (`ResidentNpc.GiveJob`) and go straight to a building or person (`HavenTravel`); a Raids tab shows `HavenReadiness`; decorations Lantern Post, Haven Banner, Herb Planter | Building damage; real art for level dressing. Nine buildings are kit-built models (Workshop, both lodges, Smithy, Tailor's Shop, Trading Post, Cottage, Hut, Sauna: a Blender script each on `Tools/Blender/building_kit.py`, after a concept image, put in by `AgentScripts/BuildStructureModels.cs` with the meadow's grass on their sod roofs, `Grass.shader` `_BAKED_ROOTS`; a building's front is +Z and a new piece starts turned to the camera); the stations and the rest are still primitives. Added by `AgentScripts/BuildFood.cs`: Kitchen, Tanning Rack, Farm Plot, Beehive (stations), Well (`WaterSource`), Sauna (`BoonSource`), Watch Post, Spike Trap (`SpikeTrap`), Cottage |
| §15 NPCs, companions | Partial | `RescuableTraveler` → `ResidentDefinition`, `JobDefinition`, `ResidentNpc`, `ResidentSpawner`; `Companions/*` (`Companion`, `CompanionDefinition`, `CompanionOrder`, `CompanionTactics`, `CompanionBarks`); `ResidentRoutine`, `ResidentLife` | One recruit (Ingrid) and one job (Alchemist). Companions fight beside the player with orders (follow, stay, defend, retreat, attack my target) given in conversation; the prologue expedition uses them. Knocked down, they get up after a while: no player revive, permanent death or raid deployment yet. Residents keep a daily routine and companions fight with some sense (see "The living world"). No needs or relationships. People are modular humans with emotes and talking gestures, and the player is made on a character creation screen (see "Characters") |
| §16 Skills, levels, bosses | Partial | `SkillDefinition`, `SkillSchool`, `SkillLoadout`, `PlayerSkills`, `SkillWindow`; skills that level by doing in `Progression/*` (`ProficiencyDefinition` × 13: Woodcutting, Mining, Foraging, Fishing, Weaponcrafting, Armorcrafting, Cooking, Alchemy, Construction, Melee, Archery, Runecraft, Sneaking; `PerkDefinition` × 130 at their milestones; `CraftedWork` (goods, quality, condition); `CharacterProgress` in `GameSession.Progress`, `ExperienceTracker`, `PlayerTraining`, `PlayerProficiencies`, `LevelingSettings`, `ExperienceStone`), `SkillsWindow` (N), `ExperienceHud`, the Rune Book; see "Skills"; skill ranks bought with Purified Essence (see "Essence") | Bosses |
| §17 Quests, trade | Partial | `Quests/*`, `Events/*`, story graphs (`Story/*`: `StoryScript` in the scene with `[SerializeReference]` `StoryNode`s and links, edited in `Editor/Story/StoryGraphWindow` (GraphView); see [story graphs](../../../Docs/StoryGraphs.md)); chains "The First Days" and "The Lost Expedition" (why the haven is built: GDD §4.1; rebuilt by `AgentScripts/BuildHomeStory.cs`, then `BuildOpeningOrder.cs` for the order of the first days, moments under Home Moments in SafeHaven; who is rescued when and what each opens: [rescue order](../../../Docs/RescueOrder.md), `BuildRescueOrder.cs`) | No trade/bargain or reactive world |
| §18 UI, accessibility | Partial | `UI/*` (TMP), `DialogueBox` (conversations at the bottom of the screen: live `DialoguePortrait` of the speaker, name, typed-out line, answers 1-4; QuestGiver, companions and story graphs talk through it), `ActionMenuWindow` (world menus), `UIStateManager`, `UITheme`, `TutorialWindow` + `TutorialNoticeHud` (see "Journal") | Options screen with key rebinding, UI and text size, reduced shake and flashes (see "Options"). Tutorials open the first time a mechanic is tried. Status icons beside the vitals (`PlayerConditionsView`). Colour-independent signals and audio cues are still open |
| §19 Art, audio | Partial | `Audio/*` (a `SceneAudio` "Scene Music" component per scene), volumes `SafeHaven_Volume` / `MistyWoods_Volume`; environment in `Environment/*` (terrain moss, tree wind, `SurfaceLayerController` moss/rust on LocalSpawn models, grass and glowing plants, water and waterfalls, day/night and weather) | Dialogue is spoken: 412 lines in twelve generated voices (`VoiceBank`, see "Spoken dialogue"), unheard. Composed music (`Audio/Safe Haven - *.mp3`) set per scene and as game-wide defaults in `AudioSettings`; effects from a CC0 library (`Audio/SFX`, credits in `Audio/SFX/CREDITS.md`), most of it made for the game by `Tools/Audio` because the free packs' interface blips and cartoon impacts read as arcade: combat, footsteps by ground layer (`PlayerAudio`), quests, crafting, building, interface, rain, wind, thunder and stream loops (`WorldAudio`), gathering. Models are placeholders. Day/night and weather are visual only; the Haven's hour is saved (`DayNightCycle` is an `ISaveable` on a SaveableEntity, and time spent on a raid moves the clock on), the weather is not (§11 "weather influences plans" not built); the raid has the same sky rig and stops the clock at the rolled hour (`RaidSky`). Volumetric fog, fog of war (unexplored ground, the night veil) and fire in the fog: `Environment/Atmosphere`, see [atmosphere](../../../Docs/Atmosphere.md) |
| §20 Technical design | Done | Definitions vs runtime state vs `Save/*`, with any number of saved games (see "The save system in layers"); rules in settings assets (`GameSettings<T>`) | Raid crash recovery and seeds not built |
| §21 First 90 minutes | Done (prototype), prologue added | `Prologue_MistyWoods.unity`, `Prologue/*`, `SafeHaven.unity`, `Raid_MistyWoods.unity`; quest chains "Prologue - The Unquiet Forest", "The Hearth and the Door" (the story at home), "The Lost Expedition" (rescues), "The First Days" and "Hands at Home" (Haven quests); `Playtest/*` | Prologue uses a separate authored scene, not a raid. Three story escorts (Sigrid, Eirik, Runa) with camp topics; Bjarne, Tor, Liv and Ingrid are left out to be rescued later: Ingrid in the raid scene, the others through rumours (`Data/Raids/Rumours`). Weapon choice, cooking, troll ambush and the old troll are story graphs. Shelter and waystones are blockout art. |
| §23–24 MVP / milestones | M0–M3 (no boss-earned skill enhancement yet), plus M4's recruit with a job and M5's scripted ward breach | – | Next per GDD: M4 companions (down/revive); M5 boss, environmental event and polish |

## Folders

| Folder | What it holds |
|---|---|
| `Core` | Game-wide plumbing: `GameInput` (the game's input: the project-wide Input Actions asset `Input/InputSystem_Actions.inputactions`, maps Player, Camera, Build, System and UI, control schemes Keyboard&Mouse and Gamepad; every action named once here, bindings only in the asset; listeners subscribe in OnEnable and unsubscribe in OnDisable, and PlayerInputHandler turns the Player map off while paused. Active Input Handling is the Input System only. Tested on virtual devices in `InputActionsTests`), `GameSession` (in-memory game state), `GameSystems` (starts persistent systems), `Options/*` (the player's settings, see "Options"), `SceneLoader` (scene changes; thin front for `SceneTransition`, the async loader with the loading screen and `ISceneSetup`), `GameStateManager` / `UIStateManager` (scene singletons), `GamePause` + `BackHandlers` (pause ownership, Escape routing), `GameMessages` (on-screen text), `GameSettings<T>` (rule assets loaded from `Resources`), `PlayerArrivalPoint` (where the player appears at home), `AmountText`, `Tiers` |
| `Events` | The game-event bus (`GameEvents`, `GameEvent`) and designer-facing pieces: `GameEventDefinition` (Quest Events), `GameEventTrigger` (zones/interactions raise events), `GameEventListener` (events trigger scene changes) |
| `Player` | The player: `Player` (the front door to the player's parts), movement, input, combat, `PlayerSwimming` (wading slows the player the deeper the water; from Swim Depth they float and swim, no fighting, dodging or interacting: `PlayerStateMachine.IsSwimming`; numbers in `PlayerMovementSettings` › Water) and `PlayerSwimAnimation` (procedural treading and breaststroke, no clips), `PlayerEquipment` (worn gear, weapon and tool belt; see "Equipment"), interaction, `PlayerPersistence` + `PlayerSnapshot` (carrying the player across scenes, and what a failed raid keeps) |
| `Combat` | Stats, damage, status effects (`StatusEffectDefinition`, `StatusEffects`: poison, chill, roots), `Damageable` (health, death, invulnerability, revive), attacks and hit detection, and combat feel: `CombatFeedback` (hit stop, shake), `HitReaction` (flash, punch, knockback), `CombatFeelSettings`. `Faction` (Player, Enemy, Neutral, Ally): the player and Ally (companions, travellers) are one side with no friendly fire (`FactionRelations.AreAllies`); Enemy AI hunts both (`WillAttack`). Kills carry the fallen side (`GameEvent.KilledFaction`), so a quest's "kill 5 enemies" (`QuestObjective` › Faction, used when no monster is set) never counts the player or a companion falling |
| `Companions` | People who fight beside the player. `Companion` (on the NPC: carries out its order, walks, is knocked down and gets up, takes orders in conversation through `IConversationTopics`; its plain C# parts are `CompanionLeader` (whom it follows and what they last hit), `CompanionWeapon` (swings and arrows at the strike delay) and `CompanionBody` (Animator and the weapon in its hand)), `CompanionDefinition` (a kind of companion: attack or arrow, order ranges, the orders it takes and its answers; `Data/Companions`), `CompanionOrder` and `CompanionTactics` (plain C#, tested in `CompanionTests`: which enemy, where to stand, per order). Monsters go for a priority target first while they can reach it (`Combatant` › Priority Target, set on the player prefab; `TargetPriority`, `MonsterTargeting.CanReach`); a companion only when the player is out of reach. A definition also gives the weapon in their hand (`HeldItemView.CreateModel` at the definition's grip for their humanoid rig; a bow in the left hand), optional animations (Archer: `ArcherAnimations` override whose Attack is a bow draw) and the strike delay. Orders in conversation can be switched off per companion (the prologue expedition follows the story). Content: `AgentScripts/BuildCompanions.cs`. A new order = a `CompanionOrder` value at the end + its case in `CompanionTactics` + an answer line in the definitions |
| `AI` | `MonsterAI` (the state machine: idle, investigating, chasing, attacking, staggered, returning; leash and home) and its plain C# parts: `MonsterTargeting` (whom it may hunt, Player Only, avoided ground, reachability, a better target in sight), `MonsterMovement` (the NavMesh: walking, stopping, turning, charging), `MonsterAttacks` (carrying out a strike and its actions: projectiles, charges, howls, cooldowns) and `MonsterPose` (body language of wind-ups, staggers and hovering); its animation driver and `AttackTelegraph` (ground warning during a wind-up). `AI/Perception`: how monsters notice things (`MonsterSenses`, `Awareness`, `PerceptionRules`, `WorldNoise`, `AwarenessIndicator`, `PerceptionSettings`; see "Senses and stealth"). `MonsterPatrol` (walks a route of waypoints through `MonsterAI.Roam`, keeps its monster awake), `CreatureFootfalls` (step sounds and camera shake of heavy creatures), `MonsterIdleLife` and `MonsterIdleDriver` (plain C#: wander, graze, sleep; see "The living world"), `MonsterPositioning` (plain C#: flanking, finding a line for a shot, a target's speed), `MonsterPack` (a group that can break and run). What needs the state machine is in partial files of `MonsterAI`: `.Investigation`, `.Backstabs`, `.Gizmos` |
| `Camera` | `PlayerCameraController` (top-down and over-the-shoulder action style on one Cinemachine camera, framing per game state), `CameraPreferences`/`CameraStyleRules` (which style applies), `CameraPointer` (mouse vs screen-centre aiming) and `CameraShake` (Cinemachine extension) |
| `Items`, `ResourceInventory`, `Crafting`, `Content` | Data definitions (ScriptableObjects) and their runtime containers. `Crafting/MaterialPool` is the one place costs are paid from |
| `Survival` | Meals, tonics and other timed bonuses: `PlayerBoons` (two meal slots), `BoonSource` (the Sauna), `PlayerEating`. Hunger and thirst: `PlayerNeeds` (on the player), `NeedMeter` (one meter's rules), `SurvivalSettings`, `WaterSource` (drink and fill waterskins) |
| `Durability` | Wear on deployed equipment: `DurabilitySettings` (loss per failed raid, repair cost share), `Repairs` (cost and repair), `RepairBench` |
| `Building` | `BuildPlacer`: placing, moving buildings, snapping, yards and line drawing; G toggles `UI/BuildMenuWindow` (code-built picture-card grid with tabs by `BuildCategory`, a detail pane with have/need costs, and Manage cards for upgrade/move), which calls back `BuildPlacer.Place` / `PickBuilding`. `BuildGhost` (plain C#): the see-through preview and the yard markers. `IBuildArea` / `BuildArea.Current`: where and what may be built (the `HomePortal`'s ward, the `PrologueDirector`'s camp: its guidance, ground, spot rules and buildable list), so the placer knows neither. `BuildPlacement`: pure footprint/yard/snap/line geometry (tested in `BuildPlacementTests`) |
| `Settlement` | Safe Haven as a place: `SettlementState` (portal, ward level, breach, residents, purification queue), `SettlementSystem` (persistent; ticks purification), `SettlementSettings`, `HomePortal`, `WardBreachDefinition` + `WardBreachDirector`, `HomeRevival`, `SpikeTrap`, residents and jobs (`ResidentDefinition`, `JobDefinition`, `ResidentNpc`, `ResidentSpawner`, `RescuableTraveler`), `ResidentRoutine` + `ResidentLife` (their day), `PurificationAltar` |
| `Interaction` | `Interactable` – anything the player interacts with on E. Its **Kind** says how it runs (`InteractionKind`): **Work** over Duration with a work bar and animation (gathering, repairing; Duration 0 = until `Complete()`), or **Use**, done on the press with no player state, animation or bar (talking, chests, stations, portal, altar, lighting an exit). Set it in data; `BuildingPrefabBuilder` makes stations Use. Side concerns are small interfaces: `IInteractionCondition`, `IInteractionVerb`, `IInteractionNote`, `IInteractionName` (the prompt's name). `Talkable` is the one way to talk to someone: on Use it opens a DialogueBox conversation gathered from every `IConversationTopics` on the object in component order (the first with a line speaks it; each adds answers: `QuestGiver` offers, `PrologueCompanion` camp topics, `Companion` orders, `ResidentNpc` jobs), and after an answer opens again with its follow-up line. It holds the speaker's name, portrait, camera distance and an optional talked event. A `QuestGiver` without a Talkable starts or hands in on the press (notice boards) |
| `Raids` | Raid definitions, modifiers, the `RaidLauncher` (departure, Portal Sparks), the `RaidDirector` that runs a raid scene, extraction, loot caches, the portal, `RaidSpots` (finding a place out of sight). `Raids/Events`: raid events (see "Raid events"). `Raids/Map`: painted raid maps (`RaidMapDefinition`, `RaidMapZone`, `RaidZoneGrid`, `RaidMap`, `MapRotation`, `RaidMapMarkers`; see "Raid maps") |
| `Progression` | Skills, their experience, perks and Insight (see "Skills") |
| `Quests` | Quest data (`QuestDefinition`, `QuestObjective`), per-player progress (`QuestLog`), `QuestManager`, `QuestGiver` (state-aware: marker, start, hand-in), `QuestMarker` + `QuestMarkerSettings` |
| `Audio` | `AudioManager`, `SceneAudio` + `MusicPlaylist`/`MusicRules` (music), `SoundDefinition` + `DeathSound`/`SoundEmitter` (effects), `PlayerAudio` (footsteps, landing), `WorldAudio` (weather and water loops, thunder, interface sounds), `VoiceBank` + `VoicePlayer` (recorded lines, found by speaker and words; see "Spoken dialogue"). Edited in **Safe Haven > Sound Editor**; the library is set up by **Safe Haven > Setup > Audio > Set Up Free Sound Library**, then `AgentScripts/BuildAudio.cs` for what lives outside it |
| `Cinematics` | Narrated sequences of layered paintings: `CinematicDefinition` (the data), `CinematicTimeline` and `CinematicCamera` (timing and parallax, plain C#), `CinematicScreen` (plays one over any scene), `CinematicMotesGraphic` (specks). Two exist: the opening, and "The Catch" as the prologue ends. Written by scripts with `Editor/Cinematics/CinematicBuilder`. See "Cinematics" |
| `Environment` | `TerrainMoss`: disposable terrain-alphamap-masked PBR moss and optional leafy sprigs; `MossWind`: shared shader wind, driven by a Directional `WindZone` on the same object when present (moss, `TreeWind.shader` trees and particles share it; see [tree wind](../../../Docs/TreeWind.md)); `TerrainMossBloom`: animated moss paint of the portal ward circle (runtime copy of TerrainData, so the asset is untouched in Play mode); `SurfaceLayerController`: per-object amount (`SetAmount`/`FadeTo` by layer name, via `MaterialPropertyBlock`) of the two live layers of `LayeredLit.shader` ("Safe Haven/Layered Lit"), which grows tileable moss/rust/dirt over a Local Generator model from its exported `_BlendMask` (curvature, edge wear, cavity, coverage). Grass: `Grass.shader` ("Safe Haven/Grass") on instanced terrain detail prototypes, `GrassInteractor` (up to 8 objects that push grass, as shader globals), `GrassTerrainColour` (baked ground colour the grass is tinted with); `Editor/Environment/GrassSetup` builds the assets and prototypes and rebakes on terrain paint (see [grass](../../../Docs/Grass.md)). Water: `Water.shader` ("Safe Haven/Water": depth colour, refraction, caustics, flow, waterfalls, foam), `WaterSurface` (a pond/river interactors look up; switches the simple look for cameras without depth/opaque textures), `WaterInteractor` (player/stone ripples, wake foam, splashes, or constant churn), `WaterRipples` (GPU ripple height field around the player, `Shaders/Resources/WaterRipples.shader`), `Waterfall` (generated falling sheet + plunge); `Editor/Environment/WaterSetup` makes the assets, creates water and carves beds (see [water](../../../Docs/Water.md)). Sky: `DayNightCycle` (+ `DayNightProfile` asset: sun/moon light, ambient probe, fog, reflections, Night/Golden Hour volume weights, `_SafeHavenNight`), `WeatherSystem` (+ `WeatherDefinition`s and a `ClimateDefinition`: wind via `MossWind.SetWeather`, `RainEffect`, Overcast volume, lightning); `Editor/Environment/WeatherSetup` adds the rig, `SkyControllerWindow` previews and drives it (see [weather](../../../Docs/Weather.md)). Grass also has glowing mushrooms/flowers, Nordic plants (fern, blueberry, lingonberry, heather, wild flowers) and `GrassSetup.FillByGround` (grass and plants by ground layer, none under water or objects). `TreeWind.shader` has Fast Leaf Lighting for leaf cards. `GeneratedTexturePostprocessor` gives new generated materials with a blend mask that shader (Assets > Safe Haven > Use Layered Lit converts others). `Editor/Environment` contains setup, `TreeWindMaterials` (Local Generator trees → Tree Wind materials and `_Wind` prefab variants), deterministic `ForestGroundTextures`, native Amplify graph creation (`ForestGroundShaders`) and preview-only `ForestGroundShowcase`. See [terrain moss usage](../../../Docs/TerrainMoss.md) for maps, materials and budgets. No gameplay/save coupling |
| `Save` | The save system, in layers (see below), with any number of saved games (`SaveSlotInfo`, `SaveGameService.CurrentSlot`), plus `SaveableEntity` / `ISaveable` for objects in scenes |
| `Environment` (raids) | The Misty Woods have their own weather and light: `Misty Woods Climate` (every weather in it has mist), `Misty Woods Day Night`, and the `MistyWoods_*` volumes (cold and darker than Haven, greens pulled toward teal), set by `AgentScripts/BuildPlaytestFixes.cs`. Generated raids keep the world's look: `TerrainGrowth` grows grass and plants by ground layer for both the editor's Grass and Plants tool and `RaidMap.Dress`, which also bakes the ground colour for grass and refreshes `TerrainSurface` on the map that was kept. `RaidMapDefinition` › Growth sets how much grows on each layer. The look is set up by `AgentScripts/BuildRaidLook.cs` (tessellated material, wind trees, mist, moss, footprints, Forest Wind); `RaidLookTests` guards it |
| `Environment/Atmosphere` | The look of the world in the way of Gord ([atmosphere](../../../Docs/Atmosphere.md)): `AtmosphereFeature` (URP Render Graph feature on PC_Renderer, `Shaders/Atmosphere.shader`: half-resolution volumetric fog ray march with shadows and fire light, full-resolution composite with the fog of war), the volume overrides `AtmosphericFog` and `FogOfWar`, `VisibilitySource` (sight and light: lifts the night veil, thins fog, reveals ground, `IsLit` for night creatures), `AtmosphereLight` (a Light that glows in the fog), `ExplorationMap` + `ExplorationGrid` (explored ground, saved at home), `FogGround` (baked ground heights: fog that follows the terrain), `FogArea` (pockets of extra fog), `AtmosphereConditions` (weather fog, fog density for monster sight). `Environment/LightFlicker` for fire. `Camera/StrayCameras`: the game's camera switches off any other base camera drawing to its display on Start (a bare test "Main Camera" left in the open scene once took over the game view). `Environment/SeeThrough` + `Shaders/SeeThrough/` (Gord-style dithered see-through of whatever stands between camera and player; in Tree Wind, Layered Lit and the See-Through copies of URP Lit/Unlit). Designer tools: `Editor/Environment/AtmosphereSetup` (Set Up Open Scene, presets, Fog Area), `SeeThroughMaterials` |
| `UI` | Views and windows. They only read game state and send requests; no game rules live here. `UI/Options`: the Options and Saved Games screens, built in code from `OptionsWidgets`. `PlayerConditionsView` lists boons and status effects above the vitals. `ActionMenuWindow` is the shared menu for world objects (Portal, portal, residents, altar, repair bench, building upgrades). `MessageFeedView` (the message feed, in `ItemHUD.prefab`) follows the player on screen so pickups and notices appear beside them, falling back to the lower right when the player is off screen. `DialogueBox.ClaimsClick` keeps a left-click that continues dialogue from also drawing the bow. `StationWindow` is the crafting-station window (recipe icon grid with `RecipeTabs` tabs, selected recipe + Queue, queue grid with progress bar, output grid, extension actions as buttons); its prefab is generated by Safe Haven > UI > Build Station Window (`Editor/UI/StationWindowBuilder`). `ItemTooltipView` also describes recipes and bare definitions (`ShowRecipe`, `ShowDefinition`), used for hover tooltips on station cells and on the bag's hand-recipe rows. `UIRoot` (root canvas, UI scale) and `PauseMenu` (Escape menu); see "UI root, pause and the Escape menu" |
| `Editor` | The Content Editor (`Editor/Content`), the Systems Editor (`Editor/Systems`), the level tools (`Editor/Level`), the UI theme tool (`Editor/UI`), prefab builders, placeholder generators and Edit Mode tests (`Editor/Tests`) |

**Assemblies:** `SafeHaven` (everything outside `Editor`), `SafeHaven.Editor` (`Editor`, editor-only) and `SafeHaven.Tests`
(`Editor/Tests`). Editing editor tools or tests no longer recompiles the game code. Runtime code can't see editor code;
`internal` members are visible to the editor and tests (`Core/AssemblyInfo.cs`, `Editor/AssemblyInfo.cs`). A new
package used by game code must be added to `SafeHaven.asmdef`'s references. A `[SerializeReference]` class
created before the split carries `[MovedFrom(sourceAssembly: "Assembly-CSharp")]`.

## How systems talk to each other

### Designer-authored prologue

The game starts in the sun: a meadow west of the walled forest (second terrain, *Meadow Terrain*, sharing the forest's west
edge), then the walk down the old road into the forest. Built by `AgentScripts/BuildPrologueMeadow.cs` (`Data`, `Land`,
`Moments`), but made entirely of the designer pieces below, so all of it can be edited in the Content Editor, Quest Map and
Story Graph. A chapter's whole setup is on its quest's Content Editor page (**Story Chapter** panel, `PrologueChapterPanel`).

`Data/Prologue/ForestPrologue.asset` owns the ordered `PrologueChapter` list: quest, behaviour, automatic-entry
messages, HUD text, duration and initial enemy count. A chapter with behaviour **Story** has no mechanic of its own: story
graphs and scene reactions make its moments, and the chapter only says where it is (**Site**: where a checkpoint goes back to,
what the companions talk about), what the companions do (**Stand Still**, **Wait At Site**, **Follow**, **Story Leads**) and
whether there is **Time To Talk** (their topics). A new step of the story that needs no new mechanic is a Story chapter. Sigrid's ordinary `QuestGiver` component owns the ordered
**Offers** for briefings and return objectives: quest reference, briefing text, acceptance label and waiting text.
Her `Talkable` makes the offers a conversation (close-camera framing, walk-away close); reading or closing it does not
accept anything. Set a chapter quest's Start to **By Quest Giver**, then author its offer on the giver.
Before another briefing, the previous quest uses **Turn In To Giver** so "Return to Sigrid" stays tracked.
The standard ordered-offer handoff accepts the next briefing and hands in the previous quest together.
Acceptance uses `QuestGiver → QuestManager → QuestLog`; there is no director acceptance API or leader flag.
The director does not run an unaccepted chapter's timer/encounter.
Automatically-started emergency chapters need no conversation; once its quest starts (it waits its turn, see "Story
pacing"), a fighting chapter (camp defence, portal hold) says its arrival line and starts at once: in a fight a line
without answers is a subtitle that leaves the controls to the player (`DialogueBox`), so nothing waits for it, and story
graphs can Spawn Enemies before Say (Troll Ambush). Add or reorder chapters together with their quest prerequisites in
the existing Quest Map; chapter list order does not rewrite the prerequisite graph. A chapter waits for its quest
to complete, or with **Ends At Objective** for the part of it up to that objective: then the next chapter takes the same quest on
(Shelter from the Storm is the shelter, then the fire), its line is said in passing, and `PrologueDefinition.OpensItsQuest`
tells the parts apart. A camp defence can come in **Waves** (the next when the last has fallen, with an optional **Next Wave
Text**). Reusable behaviour presets handle shelter, fuel, fire, rest, camp defence, travel, activation and the
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
The morning's moments are story graphs under **Prologue Meadow › Morning Moments**, the forest's under **Intro Moments** (both
in `Docs/StoryGraphs.md`): Eirik's waterskin for the cooking chapter (**Berries for the Pot**, started automatically after the fire),
the small trolls' ambush in the berry clearing, and the old troll walking towards the fire when the wolves are beaten.
By the morning fire the companions stand still (chapter **Stand Still**, `PrologueDirector.CompanionsHeld`). All companions stop and stow their held weapon during peaceful
conversations, restoring its prior visibility afterward; combat retains armed behaviour. Separate torch props remain lit.
Hand-mounted `ExpeditionTorch` objects keep their authored lit models upright during animation; their Light and
LightFlicker components are scene-editable. Scene `GameEventTrigger`s control discovery zones;
`PrologueInteraction` handles portal activation, with editable before/after-step prompts. `PrologueDirector` derives its
chapter from `GameSession.Quests`, owns temporary enemies/timers, and raises encounter/story facts. The camp quest
counts the cleared encounter, not player kill credit. No `RaidSession`, regular raid loot loss, result screen or
durability penalty is involved. What the player wears at the end (armour, weapon, belt tools, backpack, lantern) and their
arrows come home, by the rule of a failed raid (`PlayerSnapshot.WhatWasWorn`, handed over in `PrologueDirector.EndSequence`;
the definition's **Worn Gear Comes Home** turns it off). Everything carried stays behind, and the Haven's own starting contents,
recipes, hunger and thirst stand.

`BuildPlacer` retains its ordinary placement, footprint checks, material payment and Built event. In the forest
the `PrologueDirector` enters its `PrologueBuildArea` as the build area (`IBuildArea`), which exposes only the prologue definition's Campfire, the same `Data/Buildings/Campfire.asset` the player builds at home
(Sticks and Small Rocks; upgrades only at home), only during the accepted building chapter, and only inside the
shelter's build radius. Keep the gathering objectives aligned with its Construction Items when tuning costs
(PrologueTests checks this). The fire quest counts **Build Structure**, not a use-fire event.
Its ground is the director's TerrainCollider (height resampled after snapping), and it rejects footprints whose
corners differ by more than 0.2 m from the base; the portal's ward is a flat plane.
`PersistentScene` and the campfire's registered prefab preserve the player's placement and resource depletion.
The expedition wolf prefab has no SaveableEntity: encounters intentionally restart at checkpoints.
Armor is granted and auto-equipped once from the prologue definition (Starting Torch is empty: the story hands one out on the road), before the saved
Started event is raised; loading never grants duplicates. The forest player's inventory defaults are empty.
The definition's Starting Weapon is empty: at the tree line the player picks a Tier I sword, axe, spear, club or bow (with
arrows) in the **Choose a Weapon** story. L (`PlayerEquipment.ToggleTorch`) swaps the torch and a two-handed weapon. Z (`PlayerEquipment.SwapWeapon`) swaps the weapon in hand for the one held before it (or puts the only one away). Torches are normal
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

For safe designer playtesting use **Safe Haven > Scene > Play Prologue (Isolated Saves)**. It starts only the
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

### Quest handoffs, tracking and countdowns

These are shared quest-system features, not prologue rules:

- **Closed By** (`QuestDefinition`) makes a quest optional for as long as a later one hasn't begun. Once that quest starts it
  is no longer offered; if it is under way it lapses (`QuestStatus.Lapsed`: off the tracker, no rewards, saved) and if it only
  waits to be handed in it is handed in by itself (`QuestManager.CloseQuestsEndedBy`).
- **Side Offers** (`QuestGiver`) are optional quests beside the ordered **Offers**: each is its own answer in conversation
  (**Ask Text**, **Thanks Text**), in any order and at the same time as the others, and doesn't hold up the main sequence. The
  floating mark shows the most pressing one. They need a `Talkable`.
- **Rank** (`QuestDefinition`), the kind of quest: Main Story (gold), Rescue (teal: one of the lost expedition), Haven (green:
  building and learning at home), Side Quest (silver, shown as "Tale") and Optional (bronze, "Errand"). The colours are in
  `QuestMarkerSettings` (Systems Editor) and reach the giver's mark, the tracker, the waypoint arrow, both maps, dialogue answers
  and the Quest Map's strip. The enum's numbers are the order the kinds were added in (saves keep them); lists use
  `QuestMarkerSettings.RankOrder`: story, rescues, haven, tales, errands. What belongs where is in
  [QuestRedesign.md](../../../Docs/QuestRedesign.md): something happens in every Main Story quest, and nothing but the story
  and rescues may stand before a Main Story or Rescue quest (`QuestCategoryTests`).
- Answers that do something to a quest say so in its rank's colour (`DialogueChoice.QuestTag`: "Starts errand: ...",
  "Hands in: ..."). Quest givers set it through `MenuAction`; a story's Choice sets it when an answer leads straight to Start Quest.
- **Catch Fish** objectives count `GameEventKind.FishCaught`, raised by `PlayerFishing` for a landed fish: of one kind, or any
  when the item is empty.

- **Turn In To Giver** retains the current quest after its task is done. If the next ordered offer directly depends on
  it and its other prerequisites are met, the giver presents the next briefing; accepting it hands in the old quest
  and starts the next. Reading, closing or replaying an obsolete choice cannot accept another quest.
- **Available Objective** is optional tracker text shown once prerequisites are complete, before acceptance. Use it
  for instructions such as "Speak to the Smith". `QuestWaypoint` can point to an **Available**, **Active** or
  **Ready To Turn In** quest, so task locations and return-to-giver locations are separate authored waypoints.
- **Announce Start** and **Announce Completion** control messages and quest sounds. Intermediate chain stages can
  be silent while preserving their progress, rewards, lifecycle events, scene reactions and story pacing.
- `QuestTimers.Set/Clear` raises `Changed`, so registering a clock after a quest starts updates the tracker immediately.
  **Show Timer In Hud** also uses `RaidHudView`'s existing countdown display, with Large Timer and urgency colours.
  Raids take priority; otherwise the soonest active prominent quest timer is shown. Keep the HUD GameObject active
  in gameplay scenes: the view hides its Canvas, rather than disabling itself, when there is no clock.

`MoveObjectNode` has an **Animation Speed** multiplier for a slower or faster gait without changing its travel duration.
It restores playback speed when movement ends or is cancelled. `MonsterAnimationDriver` yields locomotion control while
AI is disabled, and `CreatureFootfalls` counts real horizontal movement, including story-driven motion, ignoring warps.
Each authored stride produces the same sound and distance-attenuated camera feedback; no timed imitation stomps are needed.

### Walking the party, and a scene with two looks

**Walk The Party** (`WalkPartyNode`, moved by plain C# `PartyWalk`) walks companions along a route (an object whose children
are its points) at one pace: they keep their formation, turned with the road, and stride towards a spot a little ahead so they
don't catch up and stop. The walk waits for a conversation, a fight, a companion left behind, or a player too far behind.
**Wait Until The Walk Reaches** and **Change Time of Day** (Follow Walk) follow `PartyWalk.Current`, so talk and sunset belong
to places on the road and wait along with the party. A load restarts the walk from the chapter's site (**Also After Loading**).

`SceneLook` (on Sky and Weather) gives one scene a second look: pairs of volumes, and optionally a second `DayNightProfile`. The
sky rig goes on driving the first volume of each pair and `SceneLook` splits that weight by how far the fade has come, so the
second look follows the hour and the weather as well. The prologue's first look is the Haven's, its second the raid's own
(`MistyWoods_Raid_*`, Misty Woods Day Night). Change it with the story's **Change Look** or the scene reaction **Change Look**.
Scene reactions also have **Change Time of Day**. A reaction replayed on loading skips its transition (`SceneEventAction.Execute`
`atOnce`), so what stays changed belongs in a reaction with **Respond If Already Happened**, and the moment itself in a story.

The forest's second day and night profile is `Data/Prologue/Prologue Forest Night` (more moon and night ambient than the raid's, which it
copies), and its global volume is `Data/Prologue/Prologue Forest Look` (the raid's, with a thinner night veil): change the forest's moonlight
there, as the raid's own assets are left alone. A `CompanionTopic` can raise a Quest Event when asked.
A chapter's companions can **Watch Only**: they wait at the site and leave enemies to the player (`Companion.HoldFire`). An attack that starts a
fighting chapter closes any conversation (`PrologueDirector.RefreshStep`).

The player stands still in a conversation (`PlayerInputHandler.IsListening`; in a fight the feet stay free). The camera glides between
speakers and leans at most **Dialogue Max Lean** towards someone far off (`PlayerCameraSettings`). The view follows the player to
the edge of the map; the Boundary walls keep the player well inside its terrains (a view limit there overrode the prologue's
opening framing and was removed: the maps are to grow past what the camera can see). With a fish on the line the view comes
down close behind the player (`BeginCloseUp`), and while a float is out it leans to keep it in view (`KeepInView`).

Any `WaterSurface` can be **Drinkable** (a World Builder river always is): it makes a `WaterSource` reached across
its whole edge (`WaterShore`) while playing. **Oval** makes a disc mesh's outline the oval, not its bounding box.

Scenes can have several terrains: `Terrains.Under(point)` is the one holding a point (`Terrain.activeTerrain` is only one of
them), and the World Builder gives terrains that touch one height along their shared edge (`WorldBuilder.ShareEdges`). A terrain
may sit below y = 0 once the builder has made room for a river bed: compare terrains in world heights.

### Hearthline: one style for all the UI

`UI/UIStyle` (`Resources/UIStyle`, Systems Editor › Settings › UI Style) holds the fonts and colours. Prefabs and scenes carry a
`UIRole` (window, inset, slot, button, primary button), which colours them from it on waking, so a colour changed there reaches the
whole game with nothing to re-run; code-built UI reads the style directly. `UIStyle.OnWorld` sets up text that sits on the world, with
its shadowed material. `HearthlineArt` adds non-interactive carved corners behind window contents and places the hearthmark in the
home badge, map and loading screen. `UIRole` adds the corners when a window first appears, without affecting layout; the action-camera
aiming indicator is not a window and must not receive a decorative frame. `UIStyle.AccentFor`
and `RimFor` use colder decorative ink on a raid; primary controls and warning colours retain their usual meanings. The minimap and world
map share separate exit, quest, event and rumour sprites; the minimap also has a companion mark. World-map labels state quest rank and exit status in words.
The player interaction prompt has a nearly opaque always-on-top slate and lightened status text for contrast over bright scenery.

**Safe Haven › Setup › UI › Regenerate Hearthline Artwork** (`HearthlineArtworkBuilder`) redraws the original sprites under `UI/Hearthline`,
assigns them to `UIStyle`, and updates the main-menu mark and player-prompt prefab. It overwrites those generated sprites and refuses
to run while an open scene is dirty. `EnvironmentHudView` enlarges existing clock labels at runtime, and `EnvironmentHudSetup` gives
new clocks the same sizes. **Safe Haven › Setup › UI › Apply Hearthline** (`HearthlineSetup`) is idempotent: it puts roles on new UI prefabs and
lays out the frameless HUD. Keep window colours solid: this project blends in Linear, so a faint light overlay on a dark panel comes
out far brighter than its alpha. Fonts are in `UI/Fonts` with their licences (OFL), which must ship with the game. Text that must draw
over the world needs a material from its own font: TextMesh Pro silently drops one made for another atlas. Every `UIWindow` fades in as it
opens (`UIStyle` › Window Fade Seconds, `CanvasGroup` on its content, added if it has none) and closes at once, so a window's own open
animation should only move things, not touch the alpha. The fade advances by a capped step, so a slow first frame can't use it up, and a window
that reopens within a tenth of a second (the next line of a conversation) doesn't fade again.

**Hearthline 2** (`AgentScripts/BuildHearthline2.cs`; art from `Tools/UI/hearthline_art.py`, the hover tick from
`Tools/Audio/hover.py`). A full-screen window (a canvas sorted at 100 or more: the Haven, skills, journal, raid planner, pause)
lays a blurred, dimmed picture of the game under itself: `UIWindow` tells `UIBackdrop`, which shows `UIBlurFeature.Output` (a
quarter-resolution blur of the finished frame, made only while wanted) on its own overlay canvas one below the window. A `UIRole`
window gets the `UI/Hearthline Panel` material (lift, inner shadow, grain; solid) when its image has no sprite, and its own `Title`
label (searched three levels down; tooltips excluded) is carved in the display face (`UIStyle.Display`: Cinzel, a shadowed title
material made from Cinzel's own atlas, a vertex gradient) with `HearthlineArt.AddTitleRule` under it, placed from the text's last
baseline because the shadow pads the text bounds. `UIStyle.Title` stays the serif for names and headings inside windows: the Haven
overview's camp name uses it. A primary button gets a glow sibling (`HearthlineArt.AddGlow`, `UIGlowFollow`, which copies the
button's real rectangle, not its `sizeDelta`, as a stretched button's isn't its size). `ItemSlotView` lights rare things from
behind the icon and adds `SlotShimmer` to the best; `ItemTooltipView` tints a header band by rarity (a solid mix, never a wash),
glows rare names (a cached `GLOW_ON` copy of the title material per rarity) and compares gear score with what is worn in the same
slot. `UIPress` plays `AudioSettings` › Hover; `UICursor` sets the Hearthline pointer.

### Story pacing: room between beats

Quests that start by themselves (**Automatically**, **On Quest Event**) don't start on the spot. `QuestManager.Begin` asks
`StoryPacing` (plain C#, tested in `QuestPacingTests`, numbers in `Resources/StoryPacingSettings`, Systems Editor › World ›
Story Pacing) and keeps the quest waiting until: `BetweenQuestsSeconds` after the last quest completed (a quest's own
**Start Delay** replaces this), no monster hunting the player's side (`MonsterAI.AnyHuntingPlayerSide`) for
`AfterFightSeconds`, and nobody talking (`DialogueBox.IsSpeaking`) for `AfterDialogueSeconds`. `LongestHoldSeconds` caps
the waiting for a fight or a conversation, so a monster nobody can reach can't stop the story. Someone asking (a Quest
Giver, a story's Start Quest, Start Now in the Content Editor) starts a quest at once. Because stories and prologue
chapters react to a quest starting, delaying the quest delays them too. Say and Choice also wait out the pause after a
fight themselves (**After Fight Pause**, on by default), for lines that follow a fight directly; a line said during a
fight stays a subtitle. **Wait For Quiet** does the same for steps without a line.

Waiting quests aren't saved. Automatic ones are found again by `StartAvailableQuests` on entering a scene, and On Quest Event
ones by `StartQuestsWhoseEventHappened`, since their event won't come again (the homecoming saves right after raising its
event, so without this the search quest would be lost on reload). That also means an On Quest Event quest whose event came
before its prerequisites were done now starts on the next scene entry once they are.

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

### Quest deeds: who or what, not an event per person

An objective names its target as an asset, so one objective type serves every person, place and thing, and a designer
assigns the target instead of making an event:

- **Talk To** a person (their `ResidentDefinition`) or a thing (a `QuestTargetDefinition`, such as the Portal).
- **Reach Place** / **Use Object**: a `QuestTargetDefinition` ("Quest Target", `Data/Quests/Targets`) that a scene's Quest Zone
  or Inspect Point (`GameEventTrigger.Target`) is marked with. A target says how "use" reads for it (Inspect, Light, Touch).
- **Rescue Person** (found and freed, or brought home), **Assign Job**, **Defend Breach**, **Reach Haven Rank**; and as before
  Collect, Gather, Kill, Craft, Build, Catch Fish, Complete Raids.
- **Store Item** (put something, or one kind, in the stash at home), **Repair Item** (at the Workshop or with a kit) and
  **Upgrade Building** (raise a building, or any, to a tier; Count Already Owned counts one standing there): quests teach an
  answer by doing it (A Safe Place, A Smith's Hearth, Heavy Loads).
- **Story Event** (the old Quest Event objective): one-off moments only: a lesson learnt, an answer chosen.
- **Kill Monster** with **Count Already Owned** counts a boss whose defeat is saved (its prefab's `BossEncounter`), as it won't
  come again. Other monsters never count that way.

Who someone is comes from the object: `Talkable` asks any `IQuestPerson` beside it (`ResidentNpc`, `PrologueCompanion`,
`Companion`) for its resident record, so talking to an expedition member or resident needs no setup at all. Deeds are
remembered in the quest log's saved set (`QuestDeeds` keys, e.g. `talked:expedition.sigrid`), so Count Already Owned, a
quest's **When Player Does** start (`QuestDefinition.StartWhen`, any objective as the condition) and once-per-game triggers
know a deed done earlier; old saves' per-person event ids map to the deeds (`QuestDeeds.FromLegacy`). Stories use the same
vocabulary: the trigger **When Player: …** (`WhenPlayerDid`, in `Story/StoryTriggers.cs`) and the step **Count As
Done** (`CountAsDoneNode`, in `Story/StoryQuestNodes.cs`).

For designers (Content Editor › Quests):

- **New Quest** shows only the essentials (name, chain, rank, what comes first, how it starts, objectives, hand-in, rewards)
  with a first Talk To objective to fill in and **Given By**: someone in the open scenes, or a new Quest Giver at the Scene
  view. Other settings are on the quest once it exists. Fields that a quest's choices make meaningless are hidden
  (`ContentSection.HiddenFieldsFor`): the start event unless it starts on one, Start When unless When Player Does...
- **Make It Work** (`QuestChecklist`), at the top of every quest: how it starts (who offers it), each objective's person,
  place or thing in the scenes (`QuestSceneIndex.MarkedAs`, the `[QuestIdentity]` fields), how it is handed in, its
  rewards; each missing piece has buttons: give it to someone in the open scenes (or the selected object, or a new giver),
  create a Quest Zone or Inspect Point for the target at the Scene view, make the selected object the target, add a first
  objective. A Quest Target field has **New** beside it (`QuestTargetTools`). It also flags (2026-11-11 audit): a Talk To,
  Reach or Use that counts deeds done before the quest when an earlier quest asks the same (it is done the moment it
  starts: fix, **Count Only After It Starts**; for a conversation raise a Story Event at its end), someone talked to or given
  work before the rescue that brings them home, a building nothing teaches, an item no recipe makes.
- **The whole game's loose ends** (`QuestAudit`, `QuestAuditTests`): a quest that can never start or waits for one that
  can't, something a story or gate waits for that nothing raises (`WaitForEventNode` counts as waiting, not raising), a
  portal whisper's moment no story plays (the click is swallowed), a rescue no raid places, and every quest's Make It Work.
  Reads saved files only. Run it after any quest, story or scene change.
- **Story builders and saves.** A story's save id and its triggers' ids are random (`SaveableEntity.AssignNewId`,
  `StoryNode.id`): a builder that destroys and remakes a story makes every saved game play it again. Keep the object and
  the trigger ids (`BuildHavensReach.Fresh`/`KeepTriggerIds`), or patch a story in place (`FixQuestAudit`).

### Quests in co-op: decided in one place

A quest needs no multiplayer code. Quests are each player's own (the character's part of the save), and while a player
visits a friend's Haven (`HavenVisit.InHostsHaven`) one rule decides what moves their quests on, per objective type
(`QuestObjective.InAFriendsHaven`, applied in `Progress` and `Owned` and to what the log remembers, `QuestLog.Handle`):

| Counts in a friend's Haven | Only in one's own Haven |
|---|---|
| Collect, Gather, Kill, Craft, Raids, Fish, Rescue (the traveller goes to their own Haven), Reach Place, Repair | Build, Upgrade, Destroy, Store, Assign Job, Haven Rank, Defend Breach, Talk To (the friend's people and portal), Use Object (the friend's things) |

A Story Event decides for itself (Quest Event › **Home Only**: the boundary stones' events, the portal repaired). A Home
Only event raised in a friend's Haven is not remembered either, so it can't count once home, and starts no quest. A
friend's people and notice board offer a visitor nothing and take nothing in (`QuestGiver.StateFor`); stories don't play
for visitors (`StoryScript`); the portal keeps its story moments and Talk To for the player's own (`HomePortal`). On a raid
followed with the host everything counts. Arriving anywhere recounts Count Already Owned objectives (`QuestManager.RecountOwned`,
only ever adding), so a quest begun on a raid or in a friend's Haven sees the player's own buildings once home. A new
objective type must be given a rule (`QuestCoopTests`).

**Beyond quests: a guest's settlement is a mirror.** In a guest's game `GameSession.Settlement` is the host's, taken on every
couple of seconds, so a change made to it there is quietly undone. Anything a visitor (or a follower on the host's raid)
earns or does must go where it belongs (2026-11-11 sweep, `CoopHomeTests`):
- **Theirs: to their own Haven**, the game kept as home (`HavenVisit.ChangeHome`, which reads it into a `SettlementState`, runs
  the change by the usual rules and writes it back; also `RescuedHome`, `SpiritGiftHome`, `BossBeatenHome`): an extraction
  (their Haven's breaches come by extractions), the Huldra's respect, a rescue, the Nøkk's air, a boss.
- **The host's: to the host's game**, which applies it and sends the result back with the settlement: the lands
  (`Territory.ForHost`/`FromFriend`), a breach source struck and one of the host's lost led home (`HavenHelp`), both carried by
  `CoopLands`, once a run where every player has the same rumour.
- **Neither: refused** (`HavenVisit.LeftToHost`): jobs, trades, the portal and ward, lighting stones.

A new system that changes the settlement from something a guest can do needs one of the three. If one is missed, the change says so: every
`SettlementState` and `TerritoryState` change made to a guest's borrowed copy logs an error naming the change and the three
ways out (`SettlementState.IsBorrowed`, `CoopHomeTests`), and `CoopLiveTests` plays a host and a friend over 127.0.0.1.
A changed co-op message fails `CoopWireTests` until `MultiplayerSettings.WireFormat` is raised and its fingerprint noted.

### Safeguards against loose ends (2026-11-12)

- **Audits as tests, and before every build.** `QuestAuditTests` (quests, stories, gates, portal moments, rescues) and
  `ContentAuditTests` (`ContentAudit`: references to deleted assets or objects, recipes and buildings nobody can learn, items
  asked for that nothing gives, monsters nothing spawns). `SafeHavenBuild.BuildWindows` runs both and refuses to build on any
  finding (`SafeHavenBuild.SkipAudits` or `SAFEHAVEN_SKIP_AUDITS=1` for an emergency). A new field pointing at items,
  recipes, buildings or monsters fails `EveryContentField_IsSorted` until ContentAudit is told whether it gives, uses or teaches.
- **Story builders share `StoryBuilder`** (Editor/Story): `Remember` before clearing, `Fresh`, `Link`, `Finish` before saving.
  A story rebuilt keeps its save id and its triggers' ids, so saved games don't play it again; new ones get ids from their name.
- **Builds that remove quest targets say so** (`QuestTargetGuard`, in the World Builder and the Haven World Generator).
- **No story step waits for ever**: each gives up (`IStoryWait`), waits for the player (`[WaitsForThePlayer]`) or doesn't wait
  (`StoryWaitTests`).
- **The World Builder saves the terrain's assets after a build** (heights, paint, ground stash) and remembers the heights it
  built (`WorldTerrain.BuiltHeights`): unsaved, every Editor start loaded old heights and rebuilt the Haven; a heightmap
  "change" that leaves the heights as built no longer rebuilds.

### Who raises which game event

| Event | Raised by |
|---|---|
| ItemObtained | `ItemInventory.Give` (loot, caches, crafting results, quest rewards). `AudioManager` plays the rarity's pickup sound from it |
| ResourceGathered | `ResourceSource.GrantTo` and loot grants, for essences only (materials are items and raise ItemObtained) |
| Killed / Destroyed | `GameEvents`, translated from every `Damageable`'s death |
| Crafted | `PlayerCrafting.TryCraft` (hand recipes) and `SettlementSystem` when a station finishes a queued craft (no actor) |
| Built | `BuildPlacer` when a building is placed (quests count it; `ResidentNpc` reacts) |
| Consumed | `ItemInventory` when food or drink is used (berries and herbs are items too) |
| RaidEvent | `RaidEventDirector` when an event begins (Amount 0) and `RaidEventSite` when the player acts on it (Amount: the choice, from 1). The playtest log's record of event participation |
| RaidFinished | `RaidDirector` when a raid ends |
| QuestStarted / QuestCompleted | `QuestManager` |
| Talked / Reached / Used | Deeds by target (`QuestDeeds.Raise`): `Talkable` when talked to (Subject: who it is: an `IQuestPerson`'s resident record, or its Quest Target), `HomePortal` when a talk ends (its Quest Target), and `GameEventTrigger` with a Target (a zone or area: Reached; an interaction or Raise: Used). Stories raise them with **Count As Done** |
| PersonFreed / PersonRescued | `RescuableTraveler` when found and freed, and when they come home (Subject: their resident) |
| JobAssigned | `ResidentNpc.GiveJob` (Subject: the job; Detail: the person) |
| BreachRepelled | `WardBreachDirector` when a breach is beaten back (Subject: the breach) |
| HavenRankReached | `HavenProgress` each time it counts the ranks (Amount: the rank, from 1) |
| LeftBehind | Loot that didn't fit and is gone, through `LootGrant.LeftBehind`: `ResourceSource` (gathering), `LootOnDeath`, `EliteCreature`, `GiveLootOutcome`. `RaidDirector` counts the player's into the session for the summary |
| Stored | `StashStorage.Store`, `StoreMaterials` and `StoreResources`: the stash window and a cart's Send to Storage |
| Repaired | `Repairs.TryRepair` (at the Workshop) and a repair kit (`ItemInventory`), one per piece |
| Upgraded | `Construction.Finish` when an upgrade is done (Amount: the tier it stands at now; building it the first time raises Built) |
| Custom (Story Events) | One-off moments only: `GameEventTrigger`'s Quest Event, `GameEventDefinition.Raise`, a quest's completion event and On Started/On Completed lists, story nodes, `RaidPortal` (repaired), the prologue's chapter events, and the optional Story Event fields on `Talkable`, `HomePortal`, `RescuableTraveler`, `JobDefinition` and `WardBreachDefinition`. Talking to someone, reaching a place, using a thing, rescuing someone, hiring, defending and Haven ranks are deeds above: a Quest Event made per person or place is not needed (and the shared traveller and companion prefabs used to raise one person's event for everyone). The Content Editor's "nothing raises this event" check finds raisers by `[RaisedByGame]` on the field; fields naming who or what an object is for quests carry `[QuestIdentity]` |

### Base building: clearing, construction, upgrades, renown

Safe Haven starts overgrown: brambles, fallen logs, old stumps and boulders are gathering nodes (`Data/Gathering/Overgrowth`,
Respawn Seconds 0, so they never grow back) with an `Overgrowth` beside them (dark ground that heals when cleared). They
block building until cleared; the big stumps and boulders need tier 3 tools. A ruined Storage Shed (`Construction.Starts
Ruined`) waits to be repaired (hold E with its cost at hand). Placing a building with Build Work from the build menu
(`BuildPlacer`, existing placement rules) puts up a scaffold: holding E hammers it (dust, sound, a little shake), the
model rises out of the ground, and when done it pops, raises dust and the Built game event. Upgrades are ordered from the
build menu (Manage › "Upgrade or repair"): the next `BuildingLevel` is paid and the scaffold goes up again; each level
adds generated dressing (stone plinth and lanterns, then banners and a flower box) and its bonuses, read by
`CraftingStation` (Speed, QueueLength, OutputCapacity; a resident whose job's workplace is the building adds 50%) and
`StashChest` (slots). Looks and sounds: `Resources/ConstructionSettings`. `HavenRenown` adds up renown (every standing
building and its levels, a second of a kind counts half, cleared overgrowth, residents); `HavenProgress` (added to the
Portal) celebrates each `HavenSettings` rank once and records it in `SettlementState.HavenRank`, which widens the
ward (`HomePortal.WardRadius`; the moss spreads out to the new edge). Content is built by `AgentScripts/BuildHaven.cs`. A level can ask for other buildings (`BuildingLevel.requires`), a building for a rank (`requiredRank`); `BuildingUnlocks` answers both. Ranks can bring a settler (`HavenSettings.Rank.settler`). Beds are counted in `SettlementState.NoteBuildings` and never gate anyone. What every reached upgrade adds, where raids can't look at the scene, comes from `SettlementState.UpgradeBonus`. The Trading Post is `TradingPost` + `TradeSettings`; `WatchPostArcher` and `WardTotem` act only in a breach; `FieldCamp` and any other `ItemDefinition.placesInRaid` kit are set down by `ItemInventory`. See [base progression](../../../Docs/BaseProgression.md).

### Lanterns and fireflies

A lantern is an item with a `LanternDefinition` (its light, colour, flicker, hours and refill). It is worn in the Light gear
slot (`GearSlot.Light`, appended at 18, so saves keep their indices; the character screen's right column) and keeps the
light it has left as its condition (`ItemStack.LightHours`, like a waterskin's water). `PlayerLantern` hangs it from the
left hip on a short swing, lights it (L, the light key: the lantern first, else the off-hand torch), breathes its light and
glass, and burns it down in in-game hours (from the `DayNightCycle`'s clock), writing what is left back every 3 in-game
minutes. Right-clicking a lantern that isn't full with its refill in the bag refills it (`ItemInventory.Refill`); empty,
L refills it the same way. The Primitive Firefly Lantern (6 Sticks and a Firefly, by hand) is what the prologue hands out
on the road instead of a torch. Fireflies (`Fireflies`, a particle swarm per place, `FireflyProfile`) send a share of new
ones to the fires and lamps near the view (`WorldLights.Current`) and the player's lantern; `CatchableFireflies` keeps a few
real ones about at night (by lights, or near the player), pooled and moved by one Update, each an Interactable on a still
anchor ("Catch Firefly") that gives a Firefly. Content: `AgentScripts/BuildFireflyLantern.cs`.

### Fishing and the Nøkken

A Fishing Rod is a weapon of kind Fishing Rod (`WeaponDefinition.TakesAttackInput`, like the bow), so `PlayerFishing`
owns the attack and guard buttons while it is held: hold to raise the rod and swing it back, let go to cast at the
cursor (the float leaves at the throw's flick; one bait from the bag, best first: Grubs from rotten logs and stumps, or
Berries), wait for the float to go under, strike, then the fight (`FishFight`, plain C#): the fish rests, warns and runs
to a side or straight out; hold attack to reel, let go to give line, lean the rod against a run with the movement keys
(`PlayerMovement.MoveKeys`); too much tension for too long snaps the line, slack lets a fresh fish throw the hook, steady
pressure tires it, and a tired fish at the bank is landed (swung out, or held up when heavy). The Fishing skill's Line
Control (`StatType.ReelZone`) stretches the line's patience and tires fish sooner. The poses are the Interaction layer's
fish states (`PlayerAnimationController.Fishing`: `FishingPose`, `FishSpeed`); the view closes in on the player and the
float (`PlayerCameraController.BeginCloseUp`); `FishingLine` draws the float, the line and the landed fish. Which fish bite
where (`WaterSurface.IsStill`: the Safe Haven tarn; rivers flow), bait, timings and the Nøkken are in
`Resources/FishingSettings`. In still water a struck bite can be the Nøkken (`NokkenEncounter`): it rises at the water's
edge, the camera closes in, it drags the player towards the deep and hurts them until Space is hammered enough (or it
wins and takes a third of their health); the first meeting raises Met The Nøkken. `FishingHud` draws it all. Content:
`AgentScripts/BuildFishing.cs`. Editor scripts that play a scene without opening it use `TemporaryPlayScene.Play`, which
clears Unity's play mode start scene again whenever Play mode ends.

### Skills

What the player gets better at by doing (design and numbers: [Skills](../../../Docs/Skills.md)). They replaced the character level and
talents on 2026-11-07. `ProficiencyDefinition` (a skill; "proficiency" in code because `SkillDefinition` is a spell, which the game now
calls an ability) holds Level Bonuses, stats that are 0 at `LevelingSettings` › Baseline Level (10, the player the game was tuned for),
At First Level at 1 (a beginner's penalties) and At Last Level at 50, and Milestones every ten levels offering two `PerkDefinition`s
(the talents' old script, GUID kept). `GameSession.Progress` (`CharacterProgress`, plain C#) owns total experience per skill (levels
follow from `LevelingSettings`), the chosen perks, unspent Insight, the altar's spell ranks, items crafted once and experience stones
touched; `SaveMapper` writes it to `SaveData.progress` and turns an old save's character experience into Insight
(`LegacyExperience`).

`ExperienceTracker` (a game system) turns practice into experience: `Practise(skill, tier, amount)` scales by `LevelingSettings.Multiplier`
(less below the skill's tier band, x1.5 on a raid) and keeps fractions; a node gathered trains the skill whose Gather Tool it needs
(`ResourceSource.Harvested` now carries the node); everything that isn't practice (quests, raids, first crafts, buildings, catches,
exploration, rune books, stones, `GrantInsight`) is Insight, put into a skill in the window. `PlayerTraining` (added by `Player`) reports
the player's own practice: weapon swings (`HitTechnique.Melee`), arrows (`Shot`) and spells (`Spell`) that hurt monsters, for Melee,
Archery and Runecraft; parries and perfect dodges for Melee; every spell cast for Runecraft; ambushes
(`MonsterAI.LastHitAmbushed`), sneaking within a monster's sight, `MonsterAI.LostTrackOfPlayer` and silent lock pins
(`PlayerLockpicking.PinPressed`) for Sneaking; per-monster limits live as long as the player object. `PlayerProficiencies` (on the
player prefab, once `PlayerTalents`) turns levels and perks into `StatModifier`s, again only when a level or perk changes. The skill stats
are read by `PlayerGathering` (Chop/Mine Speed and Sweet Spot; Forage Speed for the sickle and picking by hand), `ResourceSource`
(Chop/Mine/Forage Yield), `PlayerFishing` (Reel Zone; a catch trains Fishing), `PlayerArchery` (Arrow Damage, Draw Speed),
`PlayerSkills` (Spell Damage and Stamina Cost), `ArrowProjectile` (Ambush Damage), `PlayerCombat.Strike` (Melee
Damage, Ambush Damage) and `CostOf` (Melee Stamina Cost), `PlayerGuard` (Block Stamina Cost), `PlayerNoise` and `PlayerStealth`
(Sneak Noise and Visibility) and `PlayerLockpicking` (Lockpick Zone). `GearBalance` counts the Melee skill expected at each raid tier
(`LevelingSettings.ExpectedLevel`), and the raid planner shows the best fighting skill against it. `PlaytestLogger` writes
`SkillLevel` and `InsightSpent` rows; `PlaytestLog.SkillPace` and the Systems Editor's Skills page (`Editor/Systems/SkillsPage`)
measure the pace from them. `CoopLessons` (an `ICoopPart`) shares a quarter of each player's practice with friends nearby
(`ExperienceTracker.Practised`, `LearnByWatching`). `RecipeDefinition.RequiredSkill` gates a few special crafts; `ItemStack.Maker`
signs a better make; `TradeSettings.CoinsFor` prices it at the Trading Post; `LevelingSettings` gives the total level a title and
`HavenRenown` a share of renown. Residents learn too: `SettlementSystem` counts a worker's crafts (`ResidentRecord.WorkDone`) for
their rank (`SettlementSettings.RankFor`), which `CraftingStation` adds to its speed (`SettlementState.WorkerSpeed`) and which may
put an extra in the output slot; `JobDefinition.Teaches` is the skill `ResidentNpc` offers a lesson in (`ExperienceTracker.TakeLesson`,
once between raids: `TaughtSinceRaid`, cleared by `RecordExtraction`). A perk may teach a technique (`PerkDefinition.Technique`, `PerkTechnique`): `Techniques.Has` is asked by the system it changes
(gathering, fishing, `CraftedWork`, `PlayerBoons`, `PlayerEating`, `BuildingRepairs`, `Construction`, `PlayerArchery`, `PlayerSkills`,
`PlayerCombat`, `PlayerStealth`), and `PlayerTechniques` (added by `Player`) carries out the ones that answer events (Riposte,
Second Wind, Soul Siphon, Pinning Shot, Sweep via `ResourceNode.GatherBy`, Forager's Haste); numbers in `TechniqueSettings`.
From Melee 30 Weapon Arts have their weapon kind's flourish (`Techniques.HasFlourish`/`FlourishOf`, read by `PlayerCombat.Swings` for
damage and poise and by `PlayerTechniques` for Flurry's Focus refund and Pin's roots). Perks are chosen again for free beside the Portal. Feedback: `UIMotion` (UI) runs small unscaled motions (punch, pop, shake, flash,
fade, spark bursts) on one hidden runner; `UIPress` (added to every painted button by `UIRole`) lifts and sinks buttons;
`ExperienceHud` pops the level and milestone banner on override-sorted canvases above windows; `CraftFeedback` shows what
`CraftedWork.Made` announces (`ExperienceHud` also flies practice motes into the bar, `ExperienceTracker.SinceLastRaid` tells the
planner what the stretch at home came to, `Techniques.FirstUsed` pops a technique's banner once); sounds are `AudioSettingsDefinition` › Milestone, Perk Chosen, Insight, Craft Fine/Masterwork/Queued. To add a skill: create one (Create > Safe Haven >
Progression > Skill) with a unique id, its Training and (for gathering) Gather Tool or (for crafting) Crafts, level bonuses and
milestones, add it to LevelingSettings' Skills, and report its practice to `ExperienceTracker.Practise`.

Crafting skills: `CraftedWork` (Crafting) sorts what a recipe makes into `CraftedGoods` (weapons, tools, arrows, armour, meals, tonics,
materials; catches at the Dock, which train Fishing; nothing at the Farm Plot, Beehive or portal) and turns out gear as its maker would: quality rolled from Weaponcraft or
Armorcraft Quality (`ItemQuality` on `ItemStack`, saved in `SlotSaveData.quality`, shared by `StashShare`, worth part of an empowering
in `GearPower.QualityBonuses` and `GearScore`) and the condition it starts in (Finish). `PlayerCrafting.TryCraft` and
`StationCrafting.Collect` give gear through it (`ItemInventory.GiveMade`); `ExperienceTracker.PractiseCraft(recipe)` is called by hand
crafts and by `SettlementSystem` for the player's own station orders (not workers'), which may also put an extra portion in the output
slot (`StationQueue.AddExtra`). `PlayerEating` stretches meals and tonics by Meal and Tonic Duration, `Construction` hammers faster by
Build Speed. `ExperienceTracker.Boost` multiplies all practice by the skill's mentor building's tiers above I
(`ProficiencyDefinition.Mentor`, `LevelingSettings.MentorBonus`) and the player's Experience Gain (in `HavenSettings` › Rested Bonuses
and the Sauna's boon). Crafting orders are `HavenRequestDefinition`s with a Quality: `CanAsk` waits for the making skill's From Level,
`HavenRequestSettings.Choose` lets one stand at a time, `HavenRequestBoard` counts and takes pieces of at least that make
(`ItemContainer.Count/Remove(item, least)`) and pays a lesson (`ExperienceTracker.Teach`, in full) and renown
(`SettlementState.OrderRenown`, saved, a part of `HavenRenown`). `PlaytestLogger` writes per-minute `SkillXp` rows that
`PlaytestLog.SkillRate` and the Skills page turn into experience per hour. Content: `AgentScripts/BuildProficiencies.cs`,
`BuildHavenRequests.cs` (orders), `BuildDock.cs` (the Dock and the Fisher).

## Lifetimes

| Lives... | Examples | Created by |
|---|---|---|
| For the whole run | `AudioManager`, `QuestManager`, `CombatFeedback`, `SettlementSystem` | `GameSystems` (automatically; no scene setup) |
| For the current game | stash, world state, quest log, settlement (`SettlementState`), active raid | `GameSession` (cleared by New Game, replaced by Load) |
| For one scene | player, `RaidDirector`, `WardBreachDirector`, HUDs, monsters | the scene |
| Forever, as authored data | items, monsters, quests, sounds, raids, residents, jobs | ScriptableObject assets, edited in the Content Editor |
| Forever, as rules | survival, durability, settlement and rarity settings | Settings assets in `Resources`, edited in the Systems Editor |
| On disk | save files | `SaveGameService` → `SaveStorage` |
| As preferences | everything on the Options screen: `GameOptions`, volumes (`AudioManager`), camera style and per-situation overrides (`CameraPreferences`), the window (`DisplayOptions`), key bindings (`InputRebinds`) | `PlayerPrefs` |

Persistent systems implement `IGameSystem.OnSceneEntered`, which `GameSystems` calls once per scene after the scene's
objects have woken up. Use that instead of `SceneManager.sceneLoaded` (it also covers the editor's "no scene reload"
play mode). Use `SceneLoader.Load` for gameplay scene changes so saveable objects are recorded first.

## Conventions: reaching a system, and keeping classes to one job

**Reaching a system.** Every scene or game singleton is reached the same way: `public static X Current { get; private
set; }`, set when it wakes or is enabled, cleared when it goes (only if it is still the current one), and reset by a
`[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` hook, because domain reload is off.
No system is reached through an `Instance` (a private copy made on first use, as `BloodEffects` and `PrefabPool`
keep, is their own business). Settings assets are `XSettings.Current` too. A static list, set or table is cleared by
the same hook (by name: `ConventionTests` reads the hook, and the methods it calls, for `Current = null` and each
`.Clear()`; `MeshSurface` keeps its edit-mode registry and is the one listed exception); a static event isn't (Editor tools listen to some and wouldn't subscribe again), so whatever subscribes
to one in the game unsubscribes. `ConventionTests` checks all of these. `Player.Current` is the local player; in co-op a
friend is a puppet, so code acting for whoever did something uses the object it was given (the `user` of an
interaction, a hit's `Source`) rather than `Player.Current`.

- A MonoBehaviour reads `X.Current` where it needs it, or keeps it from `OnEnable` when it subscribes to its events.
- A plain C# part is given what it needs in its constructor (`new RaidCompanions(raid)`, `new ConstructionLook(this,
  building)`) and reads nothing global it could be given.
- Where a lower system must not know a higher one, it asks through an interface: `IRaidCreatureSource`
  (`CreatureSource.Remote`, so AI doesn't know co-op), `IBuildArea` (so `BuildPlacer` knows neither the Haven nor the
  prologue), `IQuestTarget` and `IQuestPerson` (so quests don't know who people are).

**A class that grows a second job** gives it to a class of its own, usually plain C# owned by the component. Callers use
that part directly (`director.Creatures.Wake(...)`, `director.Companions.Companion`), not forwarding methods.

| Owner | Its parts |
|---|---|
| `RaidDirector` (the raid's life: clock, stages, ending) | `RaidCreatures` (strength, hunting, alarms), `RaidCompanions` (who came along, the lost one) |
| `Construction` (level, work, payment, saving) | `ConstructionLook` (rising, scaffold, dressing, dust), `ConstructionUpgradeOffer` |
| `HomePortal` (the ward and the voice) | `HomePortalMenu` (what using it offers) |
| `PlayerCombat` (the swing state machine) | `MeleeTargeting` (aim assist, what can be finished), `PlayerCombatMode` (Combat and Exploration) |
| `RaidPreparationWindow` (the planner's view) | `RaidPlan` (the choices, their rules and launching) |
| `PlayerFishing` (casting, biting, reeling) | `FishingLine` (the float and the line) |
| `PrologueDirector` (the chapters) | `PrologueBuildArea` (where the campfire may go) |

**A state machine** whose behaviour all reads and writes the same state stays one class and is split into partial files
by concern: `X.cs` holds the state (fields, properties, events) and the lifecycle; each `X.Concern.cs` holds behaviour,
and its summary ends "Part of X, in its own file". `MonsterAI` (Awareness, Investigation, Targets, Fighting, Home,
Reactions, Backstabs, Remote, Gizmos), `PlayerCombat` (Input, Swings, Finishers, Reactions), `PlayerAnimationController`
(Locomotion, Combat, Work, Debug), `RaidPreparationWindow` (Briefing, Preparation, Building), `BuildPlacer` (Spots,
Lines) and `RaidLayoutPlanner` (Rooms, Sites, Ground, Cliffs, Regions, Routes, Height, Rivers, Landmarks, each with its own tuning
constants; the map's data is `RaidLayoutPlan`), `RaidLayoutField` (Levels, Ravines, Rivers, Ground), `RaidMapGenerator` (Points, Scenery, Water, Reach) and `HavenWorldPlan` (Ground, Water, Paths, Woodland, Rock, Resources, AlongTheWay). A raid map step draws random numbers in a fixed order: moving code
between parts changes nothing, but reordering draws within a step changes every map a seed makes.

**Files and serialized types.** A MonoBehaviour or ScriptableObject stays in the file named after it (moving it
changes its script GUID and breaks every reference); partial files beside it are free. Plain `[Serializable]` types
kept by `[SerializeReference]` (story nodes, raid outcomes) are found by class name, so they may move between files, but
renaming one needs `[MovedFrom]`. Story nodes live in one file per graph menu category (`Story/StoryTriggers.cs`,
`StoryTalkNodes.cs`, `StoryFlowNodes.cs`, `StoryQuestNodes.cs`, `StoryWorldNodes.cs`, `StoryCameraSoundNodes.cs`).

## Scene changes, raid results and raid layouts

- **What the loading screen shows:** `SceneTransitionSettings` › Destinations gives a scene its own picture and tips (`Art/UI/Loading`; `AgentScripts/BuildLoadingScreens.cs`); what a destination leaves empty comes from the general ones. Keys in tips follow a rebind.
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

## Raid events: warnings, sites and choices

GDD §9.3. A raid opts in with the **Raid Events** system (`RaidEventsSystem`, Systems Editor › Raids & Portal › Raid Systems):
a pool of `RaidEventDefinition`s, first delay, interval, a budget per raid and a limit on open sites (§9.4).

- **Timing:** `RaidEventDirector` (added by the system) picks with `RaidEventPicker` (plain C#, tested): an event that may
  happen now (`CanHappen`: earliest second, least time left, minimum stage) and hasn't happened this raid, weighted, from a
  random stream of the raid's seed. Windows are in seconds, so they hold for any raid length.
- **Place:** `RaidSpots.TryFindSpot` finds ground out of sight with a path to the player. An event without a site prefab
  happens around the player at once (the Oskorei).
- **Warning and finding it:** the warning goes to the message feed, and `RaidHudView` lists open sites under the exits with
  distance and which way they lie as the player sees it. Sites carry a pale beacon of light.
- **Choices:** `RaidEventSite` opens them in the `ActionMenuWindow`. The clock keeps running while the player thinks. A choice
  has a label, a plainly stated price, an item cost, a result line and `RaidEventOutcome`s. Walking away leaves the site open.
- **Outcomes** (`[SerializeReference]`, `RaidEventOutcomes.cs`): Give Loot, Heal or Wound (a wound never kills), Change Raid Time
  (`RaidSession.AddTime`; it can't end the raid on the spot), Give Boon or Curse, Apply Status, Spawn Creatures, Make Noise,
  Reveal Map, Tell the Player, Chance (two lists). `RaidEventOutcomeDrawer` gives the lists their kind menu.
- **Report:** `RaidSession.EventLog` reaches the raid summary ("On the way"), and each event raises `GameEventKind.RaidEvent`
  for the playtest log.

Content: `AgentScripts/BuildRaidEvents.cs` (sites are placeholder shapes in `Prefabs/Raids/Events/Sites`). The older prefabs
in `Prefabs/Raids/Events` are static scenery for the Event Sites spawn list; they are not raid events.

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

## Generated raid maps: a new forest every run

GDD §9.1. A raid with **Raid Definition › Layout Rules** gets a new map from its seed while the loading screen is up; without them it plays its painted map, turned. Three steps, each testable alone:

- **`RaidLayoutPlanner`** (plain C#): seed and `RaidLayoutRules` → `RaidLayoutPlan`: rooms (the start, exits, apex, a hub), links, wandering trails, side-trail spurs and sites. The shape and mood are rolled by `Choose` from their own stream, so the briefing can name them cheaply and a retry keeps them. Sites go in the order of the rules' list, so what comes first gets the dead ends.
- **`RaidLayoutField`** (plain C#): plan → grids of distance to open ground, and from them heights (`HeightAt`) and the three painted zones. Ridges rise with distance from a trail, so corridors are always valley floors.
- **`RaidMapGenerator`**: rewrites a runtime copy of the terrain data, on a runtime copy of the map definition (`RaidMapDefinition.UseGenerated`), so the painted map's assets never change. `RaidMapSurface` (shared with the editor's Bake) does the paint, trees and trunks. It then moves the start, exits and alarm groups, gives every Spawn Group with a Site new points (`RaidLayoutRules.Groups` names those placed by hand on the painted map) and moves whatever carries a **`RaidMapAnchor`**.

`RaidMap.Generate` drives it, rebuilds the navmesh and checks a path to every exit (`ExitsReachable`), laying the map out again if there is none. It costs about 2 s in the editor (ponds about 30 ms of that), then `Dress` about 0.5 s. Spawn lists then fill the points as on any map, so what spawns stays the raid's.

- **Ponds (`RaidLayoutRules` › Water, `RaidLayoutPond`):** the planner places them last, from their own random stream, so water never moves a site. Each lies beside a trail or at the edge of a clearing, off every trail and site. `RaidLayoutField` opens the ground around one, sets its level just under the lowest ground around it (so water can't stand above its banks) and sinks a bed; the terrain draws the waterline. `RaidMapGenerator.PlacePonds` lays a runtime disc mesh with an ordinary still `WaterSurface`, so wading, swimming, fishing, footsteps, the minimap and mist over water all just work. Each also gets a `WaterSource` on a child "Drinking Place" (Rules › Pond Drink, Pond Fill Item). It has no collider: a pond's reach is measured to its shore through `IInteractionShape` (`RaidPondShore`), as a solid collider would stop the player and enter the navmesh. Creatures can wade: a navmesh hole would make the middle of a pond a safe spot.
- **Cliffs and ravines (`RaidMapMood` › Cliffs and Ravines; the mood "The Gorges", `AgentScripts/BuildRaidGorges.cs`):** all off by default, so other moods are unchanged. **Terraces:** `RaidLayoutPlanner.AssignLevels` gives each clearing a level from broad noise, then lowers ends until no trail climbs more than one level or more steeply than a walkable ramp (about 28°), and ties to one level what stands too close for a cliff between (a trail passing a clearing, two trails side by side). `RaidLayoutField.BuildLevels` gives trails (ramps between their ends), spurs, clearings and pockets a height and the wild ground the nearest's, blending between two near the same height and keeping a hard step (a cliff) where they differ by a level. Sites and their level pockets keep off ramps (`OnRamp`, `PocketFits`). **Ravines** (`RaidLayoutRavine`, own stream after the ponds) grow from a spot both ways and stop before a clearing, site, pond, other ravine or the edge, or a trail they don't cross squarely; a crossing keeps a neck of solid ground (`Cut` blends by trail distance). The floor only falls along the line (river flows first point to last) and lies at least Depth below the rim; walls are ragged, of varying width, here and there stepped in strata, with rubble at their foot; ends narrow. A map with ravines is lifted (`Lift`) and its terrain copy grows taller (`RaidMapGenerator.ShapeGround`). `RaidMapGenerator.PlaceRavines` lays a flowing river (`Rules › River Material`, a World Builder style mesh with flow in uv2 and a `WaterSurface` centre line, running on under the end walls), copies of the scene's fog bank along the floor, rock from `Rules › Ravine Crags` set into both walls (`RaidRavineCrags`: stood on end, placed where a ray from the ravine's middle meets the terrain and mostly buried, random spacing and height; colliders and nav obstacles off, except the odd great block standing on the rim), a waterfall over the closed head of a river ravine and foam at the foot of every cascade (the floor keeps a drop of 2.5 m or more where the land falls by a cliff; `Rules › Waterfall Material/Plunge`, the runtime `Waterfall`) and a **`RaidChasm`**, which kills the player 5 m below the rim (a neck is solid ground; dodge invulnerability only delays it; the invincible cheat puts them back where they last stood) and topples a creature that a blow would push over an edge (`Damageable.AnyDamaged`, the CombatFeel knockback × `HitReaction.KnockbackScale`, 0 holds): it falls and dies, credited to the attacker, its drops at the bottom. **Spires** (`RaidLayoutSpire`): sheer pillars in the wild ground. `RaidLayoutField.KeepsClear` keeps trees, rocks and resource nodes out of ravines and off spires. Floors and spire tops are navmesh islands with no path, so roaming spawns and events (which need one) never use them. `RaidMapMood.largeByDefault` makes the raid planner offer the run large first.
- **Relief on every map (`RaidLayoutRules` › Hills, Boulders and Lookouts; set by `BuildRaidGorges`):** each run rolls a **hilliness** from its seed (`RaidLayoutPlanner.Hilliness`, Hill Variance) that multiplies the rolling ground and the **great hills** (`RaidLayoutHill`, smooth rises with their tops off trails and clearings, never steeper than 1.5 × 0.35 so trails can cross their flanks); the mood's Hills multiplies them. **Great boulders** (`RaidLayoutBoulder`, clusters from `Boulder Kit`, at most 9 m across, solid and in the navmesh) lie in the wild ground, the mood's Boulders times. **Lookouts** (`RaidLookout`): a rock at the edge of each of the highest plain clearings; looking out reveals `Lookout Reveal` metres of the map. Terrain copies grow taller for any map whose ground needs it. Why this way: top-down cameras flatten gentle slopes, so height reads through steps, rock faces and shade (terraces, cliffs, boulders), not long smooth rises.
- **Variety (`RaidLayoutPlanner.Regions`, `.Routes`, `.Height`, `.Rivers`, `.Landmarks`; rules set by `BuildRaidGorges`):** each piece from its own random stream. **Regions** on large maps (`Large Regions`, `RaidLayoutRegion`): the start's region has the run's mood (and reaches furthest), the others their own; `RaidLayoutPlan.MoodAt`/`Blend` give a point's mood (borders warped and eased over `Region Blend`), which terraces, hills, boulders, ravines, spires, rivers, ridges, forest density and tree species (`RaidMapSurface.Scatter`'s weight per spot) follow; the briefing names the others ("Further off: ..."). **Two ways out** (`Two Routes`): `EnsureRoutes` finds the short way to the exit ahead and a second avoiding it, adding a bypass trail when there is none; camps take the short way first. **Height**: the apex moves to the highest far clearing, exits lie low, quotas can `Prefer Height` (caches, rumours) or `Overlook Ramps` (camps). **A way down** (`RaidMapMood.ravineDescents`): one end of a long ravine slopes to its floor from a spur trail, with `Descent Sites` at the bottom; there only a real fall kills (`RaidChasm.FallingFrom`). **Surface rivers** (`RaidLayoutRiver`, `Surface Rivers`, mood `Rivers`): channels cut from a falling surface, fords where trails cross (`Ford Depth`), drinkable running water (`RaidMapGenerator.PlaceRivers`, the shared `WaterStrip`). **Landmarks** (`RaidLandmarkRule`): hill tops first, stripped to scenery. Runs don't repeat: the planner re-rolls a seed with the last run's mood and shape (`RaidLauncher.LastRun`).
- **Terrain events** (`TerrainEventsSystem`, a Raid System on both raids): at a danger stage rocks close a neck whose trail isn't the last way to an exit (carved out of the navmesh), and later the ravine mist thickens.
- **Generation speed:** the raid planner lays the run out ahead on a worker thread (`RaidLayoutPrefetch`, keyed by rules, seed, size; `Rules › Map Size` must match the map); `RaidMap` does the plan, field and heights off the main thread otherwise (heights row by row on every core), retries a failed reachability check by clearing solid rocks off the blocked trails before laying out anew, and logs each step's time. Terrace levels stamp 26 m and spread the nearest two outward. Measured on a large Gorges map: 8.3 s to 6.2 s; wall rocks and boulders cost no measurable frame time (11.1 ms either way), so they are left as objects. **Safe Haven › Content › Raid Generator Report** (`RaidGeneratorReport`, `RaidMapMeasure`) lays out many runs offline and sums them up per mood (routes, height, steepest trail, ravines, rivers, timings; CSV).
- **High ground:** arrows and creatures' missiles hit harder from above (`CombatFeelSettings` › High Ground: from 1.5 m, +3 %/m, at most +25 %), for the player and creatures alike.
- **What grows (`RaidMapDefinition` › Growth, Plants):** `TerrainGrowth.Plant` is one plant's patches as data, including Love Of Wet, which spreads and thickens them where `RaidLayoutPlan.Wetness` is high (near water, and in the forest's damp patches). The prototypes themselves live on the map's TerrainData.
- **Where things stand (`RaidScatterHabitat`):** every tree, undergrowth and rock entry has a habitat (slope and forest-density ranges, wet/dry, forest edge or deep forest, near trunks or in gaps, groves, room). `RaidMapSurface.Scatter` runs three passes (canopy, then undergrowth: saplings, shrubs and clutter without colliders, then rocks and logs) and picks each spot's kind by weight × suitability among those with room to stand (trunks keep the mean of both rooms apart). Paint takes the plan's wetness (moss takes wet ground) and an optional fourth **litter** layer under the densest dry canopy; borders are broken up by fine noise. Tune it all in the World tab ("Where it belongs", Undergrowth, Litter).
- **The Misty Woods forest** is the Mossy Cedrus Forest (most of it) and the Mountain Lake Forest Pack, converted by `Editor/Environment/VegetationPackConverter` (trees → Tree Wind with baked wind UVs and LOD kept longer, leaf mips keep their coverage; rocks/logs → tessellated Mesh Surface with trunk capsules; plants → instanced Grass details; ground → terrain layers whose mask carries a softened height) and wired by `AgentScripts/BuildMistyWoodsForest.cs`. The vendor folders (`Assets/SeedMesh`, `Assets/VIVID_Arts`) are only sources; their HDRP/Shader Graph materials are never used. `MistyWoodsForestTests` guards it. Nothing in the Misty Woods reaches the old forest kit (`Art/Forest/Prefabs`): the set pieces' boulders, logs and dead trees and the painted map's tree kinds were swapped for the new ones; non-tree resource nodes (stones, boulders, stumps, logs, brambles) and the blue **Glow Mushrooms** (`Art/Generated/Grass`) stay. Young trees in the undergrowth list (young cedars, maple saplings) are solid and fellable; shrubs and clutter are not. **Mineable rocks:** a painted kind whose prefab has a `ResourceNode` and no `ChoppableTree` is gathered through **`TerrainNodeHarvesting`** (added to every terrain with tree prototypes): near the player a live copy of the prefab stands undrawn in the painted rock's pose, and takes its place once struck, so it shakes, breaks and grows back like any node. The converter gives the smaller rocks the Boulder or Great Boulder node's gathering (`Prop(..., node:)`), without its `SaveableEntity`/`Overgrowth`; cliff boulders and outcrops stay scenery. **Atmosphere** (also set up by `BuildMistyWoodsForest`, in the raid scene): **`CanopyDebris`** (Environment) sheds the pack's needle and twig meshes from the crowns of tall painted trees near the camera and lets them drift with MossWind's wind; **`AmbientOneShots`** (Audio) plays a SoundDefinition now and then near the listener (`Forest Fly`); the scene's Scene Music ambience is the pack's recorded forest wind. Painted mineable rocks set `ResourceNode` › Keep Own Model, so the live copy never takes a model variant. Trunk capsules come from the pack's own trunk colliders, so stumps match the tree. **`TreeLeafLook`** (on a tree prefab) names its own leaf-fall effect and colour; cedars use `Needle Fall`. Rerun after converter changes with `--entry BuildMistyWoodsForest.Rebuild`: it remakes prefabs and materials in place (same GUIDs), unlike deleting the output folders, which breaks every reference to them.
- **The Safe Haven forest** is the Mountain Lake Forest Pack (most of it) plus Cedrus conifers, mosses and berries, converted by the same `VegetationPackConverter` into `Art/Vegetation/Safe Haven` and wired by `AgentScripts/BuildSafeHavenForest.cs`. The Haven is a World Builder scene, so the script swaps its 9 ground layers and 11 grass slots **in place** (the `WorldTerrainStash` is indexed by layer and slot), points its groves, paint areas, paths, river and cliff at Haven-only copies of the palettes, cliff kit and paint rules (`Data/World/Safe Haven`, because the Prologue and the demo scene share the originals), rebuilds the world, regrows grass, moss and displacement, and rebakes the navmesh. Trees are the pack's Sugar Maples (gameplay copied from the oak, scaled to its height) and the raid's cedars; resource nodes are unchanged. `MergeNestedLods` folds a model's nested LOD groups (maple trunk + crown, rock composites) into one before converting. Rerun with `--entry BuildSafeHavenForest.Rebuild`. `--entry BuildSafeHavenForest.Alive` adds the groves' **Young trees** (maple saplings, young cedars) and **Forest floor** (logs, stumps, branch piles) layers, the raid's **Canopy Debris** (it sheds from standing scene trees too, through `ChoppableTree.Enabled`), **Forest Flies**, and the lake's wind recording as a second ambience track. The lake maples have roots modelled below their pivot while the World Builder rests objects on their lowest point, so tree palette entries sink by that depth (`SinkRoots`).
- **The prologue** (`AgentScripts/BuildPrologueForest.cs`): its meadow is the Haven's biome (Haven grounds, grass and `Haven Lake *` palettes), its dark forest the raid's (raid grounds and grass; the painted forest's eight kinds swapped in place for the raid painted map's, in the same order; the raid's undergrowth added by the raid's habitats, kept off paths, riverbeds and cells left without grass, which is how the story route, the camp and the portal stay clear). The forest-edge groves use `Data/World/Prologue` palettes of raid content; both terrains share one ground map so the road and river stay one ground across the seam. The forest's trunk collider (`Prologue Forest Trunks`) is rebaked from the painted trees.
- **Performance, without lowering quality** (graphics settings, LODs, shadows and ambient occlusion stay as authored; every change is measured and pixel-compared): **`MeshSurface`** (on thousands of rocks and logs) has no per-frame callback: its bounds padding depends only on scale, so one static ticker rechecks a slice each frame (`Application.onBeforeRender`) and re-pads only those whose scale changed. This was 10.7 ms of LateUpdate and 440 KB of garbage a frame in the Haven. **`InteractableGrid`** (Interaction) files every Interactable by 8 m cells, re-filing a slice each frame; the player's nearest-target search and the outline scan ask it instead of walking all of them (1224 in the Haven). Outline copies are made for the nearest level of detail only and destroyed after 10 s out of reach. **`CanopyDebris`** emits only from crowns inside the camera's view, in proportion, so what is seen falls as before, and reads the painted trees once per map. The terrain shader skips the texture reads and noise of layers with no weight at a pixel (explicit gradients, so filtering is unchanged). GPU occlusion culling is on in `PC_RPAsset` (GPU Resident Drawer); it hides only what is behind other geometry. Wind trees' mesh bounds are padded for sway sideways and up only, never below the roots. The terrain stays non-instanced: displacement needs it. **`NodeStreaming`** (Gathering) takes the placed trees of each loaded scene (a grove's, or hand-placed) that have nothing of their own to save and no model variants: once the scene has woken, each is stripped to its meshes, LODs, collider and nav obstacle, and a full live copy (from a per-kind template kept under an inactive holder) takes its place within 10 m of the player, while hovered (`PlayerInteractionOutline` wakes it via `Wake`/`KeepAwake`) or when a falling tree is about to reach it (`TreeImpacts` calls `WakeNear`; `FelledTree` treats a stripped tree's collider as a tree). The copy goes back past 14 m once standing, still and not in use. `CanopyDebris` counts stripped crowns from `NodeStreaming.Active`. Not yet measured; alternatives (switching components off instead, or a pool of live copies) are in Docs/TODO.md.
- **Materials:** a generated map takes its ground material and three layers from the definition each run (`RaidMap.ApplyGroundLook`), so changing them needs no rebake. Displacement masks are baked per layer: after swapping one, run Enable or Refresh Displacement on the raid terrain.
- **Mist:** the raid has its own fog profiles (`MistyWoods_Raid_Volume`, `MistyWoods_Raid_Night`) because the prologue and the story demo share the originals. The fog follows the terrain fully and thins fast; `FogGround`'s grid is 1 m here so thin mist doesn't float off steep ridges.
- **Where to edit all of this:** Content Editor › Raids › **World** tab (`RaidWorldEditor`): layout, materials, trees and rocks, plants, water and mist under a live strip of maps. It finds the raid's map definition and terrain through its scene's dependencies, so only Mist needs the scene loaded.
- **New mood or shape weight:** the World tab's Layout page, or `Data/Raids/Maps/MistyWoods Layout.asset`. No code.
- **See what the rules make:** the World tab's strip, or Safe Haven › Content › Raid Layout Preview for six maps at a time.
- **Something new standing on the map:** spawn groups and exits need nothing. A story object needs a `RaidMapAnchor`; make sure the rules have enough sites of its kind.
- **The painted map:** still bakes as before, and is what `rotate` applies to. Rebaking it now would change it: the terrain asset doesn't match what the definition bakes to, and hasn't since before generated maps.

**Raid tiers and gear score:** a raid's `RaidDifficulty` rows are the tiers the portal offers; the planner starts on the highest one `GearScore` is ready for, and `RaidLauncher.CheckGear` refuses anyone under its floor. `RaidSession.Tier` and `Difficulty` carry the choice into the run. The tier's multipliers go through `RaidParameters` like any modifier, spawn entries and map trees have From and To (`RaidSpawnList.EntriesFor`, `RaidMapDefinition.UseGenerated`), and `RaidDifficulty.Allows` caps drops and switches off `ResourceNode`s. `GearBalance` measures a kit against a tier. Higher tiers also change what creatures do: a `MonsterAttack` with From Tier is only chosen in raids of that tier or above, so a new ability is a row of data on the monster (`AgentScripts/BuildRaidAbilities.cs`, run after `BuildRaidTiers`). A monster with any actions never uses its plain attack, so one action has to be for every tier. `MonsterAttacks.HoldsGround` decides when one stops rather than closes in. **Elites** (`Raids/Elites`): `RaidElites` promotes creatures from the raid's seed once the raid begins, and rolls a chance on hunters sent later; `EliteCreature` applies an `EliteAffixDefinition`'s numbers and `EliteBehaviour`s, marks the creature with a ring and a name, and pays the tier's Elite Loot on a player's kill. A new affix with existing behaviours is only an asset; a new kind of behaviour is a `[Serializable]` subclass. The playtest log writes a `RaidTier` row before each `RaidFinished`, and the Run Comparison window adds them up per tier. Why these numbers, and how to tune them: [gear score and raid tiers](../../../Docs/GearScoreAndRaidTiers.md). Set up by `AgentScripts/BuildRaidTiers.cs`.

Content: `AgentScripts/BuildRaidLayout.cs`, then `AgentScripts/BuildRaidWater.cs` (ponds, plants, mist; run it after `BuildRaidLook`, which takes the prologue's mist back).

## World rendering: light, weather on surfaces, decals

Content: `AgentScripts/BuildRendering.cs`, `BuildWeatherSurfaces.cs`, `BuildDecals.cs` (textures from `Tools/Decals/decals.py`);
numbers in `Resources/RenderingSettings`. `WorldRendering` (an `IGameSystem`) owns the runtime parts and re-binds them per scene.
- **Tonemapping.** `FilmicTonemapping` (a volume override) with `TonemapFeature` after the Atmosphere feature: Khronos PBR Neutral
  or AgX before URP's post-processing, so a volume using it sets URP's Tonemapping to None and Bloom's threshold is in screen range.
  The Haven's base volume uses Neutral. Each raid's `RaidDefinition` › Look › Grade (`RaidGrade`) is applied by `RaidGradeVolume`, a
  global volume above the scene's own that sets only the tonemapping (so the raid's night and weather volumes still blend under
  it). Neutral alone turned the raid mist milky lavender (its volumes were graded under ACES): the grade adds `FilmicTonemapping`
  › Contrast and › Filter, at -0.9 stops, contrast 1.35, a faint teal filter.
- **Sky.** `SkyDome` puts `Shaders/Sky.shader` (RenderingSettings › Sky Material) on every scene with a `DayNightCycle` and feeds it
  where the sun and moon really stand (`DayNightProfile.SkyAt`: the light's own angle is clamped, so the sky can't use it), the moon's
  phase (Settlement days) and lightning. Its clouds are `CloudLayer`'s: one cover and drift, and one pattern (`CloudNoise.hlsl`)
  shared with the cloud-shadow cookie. Seldom seen from above, it is what every reflection shows.
- **Light.** `FollowReflectionProbe` (realtime, a face a frame, when the player moves, the hour turns, or every 6 s for the clouds),
  `CloudShadows` (a tiling cookie on the sun; URP takes only a Texture2D as a sun cookie, so the drawn pattern is copied on the GPU
  into one; softened by night, when the moon is all the light), four shadow cascades (the Shadows option's High keeps all four).
  `LightShadowBudget` gives the three fires and lamps nearest the view soft shadows at dusk and night (the nearest at the medium
  tier, so all fit the 2048 atlas). Every renderer on the pipeline's list must be Forward+: one on Forward switches the GPU Resident
  Drawer off for all cameras. SSAO reaches 0.8 m: from the camera's height a smaller reach is a few pixels. `WorldLights` finds
  the world's fires and lamps every 3 s (lights reaching 5 m or more that nobody carries) for the shadow budget and gives any
  without fog glow an `AtmosphereLight`. Measured: the Resident Drawer's occlusion culling trims triangles but no frame time, as
  the frame waits on the terrain's tessellation, not on draw calls. `SkyOcclusionMap` draws a top-down height map round the player through the Sky Occlusion Renderer
  (a Render Objects override pass, pipeline renderer #1) and `Shaders/SkyOcclusion.hlsl` dims indirect light under cover and keeps
  rain and snow off what stands under a roof. Not in the generated terrain: one more texture took it over its limit. In raids at
  0.35 strength (`_SkyOcclusionRect.w`): full strength under the forest canopy hid the player at night. A render costs about
  3 ms of CPU, 2 of them the terrain's trees whatever their distance, so it redraws on need (12 m moved, a building up or down,
  every 10 s), not on a timer; drawing it a quarter a frame cost four times as much.
- **Weather on surfaces.** `Shaders/SurfaceWetness.hlsl` soaks whatever has "Weather Settles On It" (the old snow toggle) plus trees
  and grass; the terrain's puddles are its own (`GenerateWorldTerrainShader.py`). Include SurfaceWetness before SeasonSnow, and in
  ASE shaders after URP's Lighting.hlsl (it needs `_Time`). Characters (URP Lit) soak per character instead (`CharacterWetness`,
  added by `WetCharacters`): on copies of their materials, never destroyed, since a death ragdoll may still draw them. Rain splashes
  are placed one by one with a ray (`RainEffect`), and `RainDrips` drips off building outlines, sloping edges and tree crowns.
- **Decals.** URP's Decal feature, screen space (the terrain's DBuffer variant turned it grey, and with decal layers on nothing
  lands on the terrain, whose depth pass writes no rendering layers; so no angle fade: keep decal boxes thin). `DecalPool` (blood
  via `BloodDecals`, tinted per colour from one atlas, and `FootprintDecals`), `GroundWear` (trampled earth, scorch, wall grime on
  standing buildings, as children of them).
- **Fog, water, fire.** The atmosphere accumulates over frames (`AtmosphereFeature` › History Weight, a Resolve pass with
  reprojection and neighbourhood clamping); water adds screen-space reflection (loads, not samples, inside the march loop: a
  gradient instruction there is a compiler warning `WaterTests` catches); `LightFlicker` gives fires heat haze and embers that
  light; `EffectParticle` fades softly into what it meets.

- **Characters.** Character materials wear Lit (See-Through) with `_CHARACTER_SKIN` or `_CHARACTER_CLOTH`
  (`AgentScripts/BuildCharacterLook.cs`, on the assets: a build keeps only the keyword sets its materials use, so runtime
  copies with new keywords would lose them). `CharacterShading.hlsl` adds a sky-and-sun rim and light through skin, with global
  strengths (URP's Lit keeps its material buffer fixed); those variants are never see-through. `CharacterLook` sets the
  strengths and gives characters `CharacterWetness` in rain.
- **Temporal AA.** Tree Wind writes motion vectors (`AlwaysRenderMotionVectors`, painted trees set to per-object motion), so
  TAA and STP follow its sway; terrain grass has no motion-vector path and softens under them. TAA is an option, not the default.
- **Shader warm-up.** `ShaderWarmup` compiles `Settings/Rendering/Warmup.shadervariants` behind the first loading screen (builds
  only). Record with `AgentScripts/RecordShaderWarmup.cs`: Begin, tour, Save (or Merge to add to it). The Editor logs a variant
  only when first made, so shaders drawn before Begin are missed (AddMaterials fills in from materials), and saving clears it.
- **Measuring.** A development build from `AgentScripts/BuildBenchmark.cs` (Build/WindowsDev) run with `-sh-benchmark`
  (`FrameBenchmark`) loads a new game in a scratch save folder, times four places by day and dusk with the CPU/GPU split, and
  writes benchmark.txt to the persistent data folder.

## Drawing a large world from a top-down camera

Measured in the 1 km Haven (editor, 1080p). Far from home, frames took 28–31 ms against 13 ms at home. Almost all of the difference was the tessellated, displaced terrain and the trees' shadows; scripts cost about 1 ms. What each fix does:
- **One terrain pass** (`Shaders/Terrain/WorldTerrainAllLayers.hlsl`, `_WORLD_ALL_LAYERS`). Unity draws a terrain four layers to a pass, and on `Terrain (World)` every pass is tessellated and displaced again, so the woods (paint in three passes) cost about three times the meadows (one). `TerrainSurface` binds every *painted* layer's own textures (nothing copied or resampled) plus a small settings table to the terrain's property block, and sets the keyword on its material. The first pass then mixes all of them, and the add passes are culled in the hull (`WorldPatchCulled`). At most 14 painted layers fit, because a shader reads at most 64 textures; a terrain painting more keeps four to a pass. Images match the four-per-pass drawing (mean difference under 1 per channel where no trees move).
- **Relief round the player** (`TerrainDetailFocus`, set by `PlayerCameraController`). Tessellation fades by distance from the player (`_DetailFocusNear`/`_DetailFocusFar`, 15–25 m), not only from the camera, which hangs 20–40 m up. Mesh Surface props follow the same focus. Edit mode keeps the camera fade.
- **Nothing tessellated off screen** (`WorldPatchCulled`). Triangles wholly outside the view (the camera's, or a shadow cascade's) get a tessellation factor of 0. So do add-pass triangles where that pass's layers have no paint.
- **Tree shadows from a lighter level** (`Editor/Environment/TreeShadowProxies`, run by `VegetationPackConverter.Tree`). The two finest LODs stop casting and carry a shadows-only copy of LOD2. The terrain draws these to the shadow map only (checked by tinting them red: no red pixels on screen). `TreeParts` and the interaction outline skip them.
- **Tessellation target 6 px** (was 4) on the World terrain materials.

Result: the woods went from 28–31 ms to 13–18 ms, and home from 13.3 ms to 12.3 ms.

Profiled again on 2026-11-10 after the Haven's rework (Editor profiler, main thread; read the hierarchy, not summed marker recorders: those add in worker threads' job time, which made shadow culling look like 6 ms of the frame when it was none of the main thread's):
- **Props are one renderer a LOD level** (`VegetationPackConverter.Optimise`, run by `Prop` and by **Safe Haven › World › Optimise Props**): `MergeLodParts` joins a level's pieces into one mesh (a pile of pebbles was 33 renderers, a crag 24; the scene went from 49,000 objects to 31,300), `TrimShadows` stops levels only seen past any shadow from casting, and `DropStaticObstacle` takes the NavMeshObstacle off what can never be mined or felled. Unity's bake leaves out anything carrying an obstacle, so those rocks were only steered round frame by frame; without one the bake cuts round their colliders (2,353 obstacles to 811, the NavMesh's update 1.8 ms to 0.6 ms). Traps: set a joined mesh's index format before combining (changing it after empties the mesh), and switch `MeshSurface` off while a renderer's mesh changes, or it pads the old piece's bounds and the LOD group is measured from them.
- **The sky-occlusion camera's terrain data is kept** (`SkyOcclusionMap`: `Terrain.keepUnusedRenderingResources`). The camera draws only when the player has moved 12 m, and a terrain throws away what it built for a camera that has not drawn for a few frames: with 130,000 trees each draw rebuilt it, a 160 ms frame every couple of seconds of walking. Kept, the draw is 6 ms.
- **A river answers "is this point in me" from the stretches near it** (`WaterSurface.NearestInReach`): its centreline in runs of 12 points with a box each. It used to walk the whole line (1,400-1,800 points) for every `WaterInteractor` every frame, 2.1 ms.
- Left as found: the terrain's mask maps stay uncompressed (block compression steps the height, which tessellation shows); triangles in the woods follow the tessellation target and the screen's size, not the LOD bias; about 2 KB of garbage a frame (`ExperienceHud.LateUpdate` 768 B of it).

Also: `TerrainDisplacementEditor.Apply` displaces the first 16 layers of a terrain with more, instead of refusing it. The refusal had left the Haven's relief tiled for its old 400 m size at 1 km.

## The Haven world: generated as a baseline

**Safe Haven › World › Generate Haven World** (`Editor/Level/HavenWorld/HavenWorldGenerator`) builds the Haven's world from `Data/World/Safe Haven/Haven World Recipe.asset` (`HavenWorldRecipe`, set up by `AgentScripts/BuildHavenWorldRecipe.cs`; the older `BuildHavenWorld.cs` built the previous 400 m Haven by hand-coded features) through a `HavenWorldPlan` (runtime, plain C#, seeded: `HavenWorldPlan.Make(recipe, seed)`, so a new game could later roll its own Haven). Rings from the portal: a gentle buildable middle (the ward reaches about 50 m), the Haven's meadows and maple groves, then the Misty Woods; a lake with islands and a mountain in their own directions; a rim at the edge. It reuses the World Builder for everything it has a feature for: rivers, paths (radials, a ring, spurs to places), outcrops and mountain cliffs, ravines and groves are ordinary features, each copied from the scene's own tuned feature of its kind (`EditorUtility.CopySerialized`, per-knot values cleared) and built by `WorldBuilder.BuildChanged`. Around them it lays the land's shape, the ground (the Haven's and the Dark Forest's paint rules blended by distance, via `WorldPaint.RuleWeight`; layers matched by name), the lake and ponds (`RaidLayoutPond` shapes, still `WaterSurface`), the woods as terrain trees (`RaidMapSurface.Scatter` with the raids' map definition, so trees are felled and rocks mined as in raids), grass (`GrassSetup.FillByGround`), points of interest, creature spawn zones (`MobSpawnZone`, by distance band) and resource points (`RandomResourceNode`, Stable Pick, higher tiers further out). Three steps, `Prepare`, `Build`, `Finish`, each runnable alone; it keeps 05 Gameplay, 06 Story & Events and the overgrowth, and never saves the scene. Measured on 1 km: Prepare 21 s, Build 26 s, Finish 24 s (navmesh), all on the main thread. `WorldBuilder.ClearTrees` buckets the trees in a 32 m grid and visits only the cells along each open line (or a loop's box): 150 000 trees, 46 features, 177 s to 3.3 s.

Reworked 2026-11-10 for a camera that looks down (the thinking: [the Haven world's design](../../../Docs/HavenWorldDesign.md)):
- **Terraces** (`HavenWorldPlan.Ground`, `.Terraces`). `Land` is the smooth land (a climb out from the Haven that swells and sinks, the mountain, the rim, with the lake's valley, each mere's shelf, the bog's basin and a valley along each road pressed in); `Terraced` steps it into benches `terraceStep` apart with a face `riserWidth` wide whatever the land's own slope; `Tier`/`OneBench` say which bench a point is on. The cliff lines are traced from `Land` by marching squares (`Contours`). `BuildRamps` (once every path is laid) stamps a ramp field (`RampAt`: the face drawn out to `rampLength` along paths), places scrambles along each cliff and keeps what is left as `Cliffs`, only where a cliff is walked beside (`cliffRockNear`): the generator makes those `CliffFeature`s with Shaping Cut (the plan has already stepped the ground, so Cut only sharpens, paints and stands rock), and the rest are terrain alone. `FindReachable` flood-fills what can be walked to; nodes, zones and scenes are only placed there.
- **Water** (`.Water`): the lake one bench under the Haven so everything runs to it; `Meres` (smaller lakes, `HavenMere`) with islands; the bog (`BogWeight`, a basin one bench under the lowest land round it, hummocks and pools from noise under one level water sheet); rivers and streams by index (`RiverWidths`, a stream is a narrow `RiverFeature`), `Meander`, `PlaceFalls` (knots just above and below each cliff a river comes down), `PlaceMouths` (a scoured fan in the bed where a river runs into still water, painted with the river's gravel).
- **River mouths.** `WaterSurface.SetShoreline` gives still water its true outline; `RiverBuilder` runs rivers in to that instead of the mesh's bounding box (a turned lake's box reached 30 m past the water, and rivers stopped there, on dry land). A river's water now runs 3.5 m out over the pond's and fades (vertex alpha, the material's Edge Fade from Vertex Alpha), and the generator's still-water meshes fade over their last 2 m, so no edge lies across a mouth.
- **Somewhere to go** (`.Paths`, `.AlongTheWay`): a road each to the lake, the mountain and the bog (and on into it by a causeway); `RoadEnds` take the places marked At Road's End; `LinkPlaces` and `gladeLinks` give places and glades a second way out. `Interesting` says whether a walker has something to come across; scenes are set beside a path wherever one has not for `trailSceneSpacing`. `MeasureWays` reports `LongestDullWalk`, `DullWalks`, `DeadEnds`, `EmptyGround`; `HavenWorldPlanTests` holds them.
- **Places** may stand at a road's end, on a shore, in the bog or in the Haven's meadows (`HavenPointOfInterest`), are levelled (`Poi.Land`, so no cliff runs through one) and give resource points round their clearing (`rewards`). `Around` samples out to the corners of the square world.
- **Game** (`HavenCreatureBand.peaceful`): zones in the meadows and the open glades that fill by day too; `MonsterDefinition` › Game › Timid and `MonsterAI.Timid` make a creature run when struck or when it sees the player (Deer, Stag, Fox, Wild Boar: `AgentScripts/BuildGame.cs`, Faction Neutral). A glade with game gets no `WildlifeHerd`.
- **Mist where the woods are** (`HavenWorldPlan.MistAt` → `HavenWorldGenerator.BakeMist` → `TerrainMist` › Region Map/Amount → `Atmosphere.shader` `_TerrainMistRegionTex`): the mist's density is at least Region Amount where the map is white, whatever the weather.
- **The planner** (`Editor/Level/HavenWorld/HavenWorldPlannerWindow`, `HavenWorldPreview`; `AgentScripts/ProbeHavenPlan.cs` for a picture from the command line): draws a plan as a map and counts it, in about a second, without touching the scene.
- New features start as copies of the scene's own (the template), so the generator sets every setting that differs by kind (river or stream, cliff or outcrop) rather than trusting what the template happened to be.

What the Haven world keeps safe and remembers:
- **Spawn zones near home** (`MobSpawnZone.ConfigureHome`): the generator's zones refill only at night (`NightSettings.IsDark`) and never while the ward's edge (`HomePortal.WardRadius`) plus 120 m reaches them, so the ward growing over a zone quietly turns it off.
- **Felled woods** (`TerrainTreeMemory`, an `ISaveable` on the terrain's `SaveableEntity`): saves `TerrainTreeFelling.FelledNow()` (tree index and seconds left to regrow) and restores it with `RestoreFelled`; a save from a different tree count (a regenerated world) is ignored rather than felling the wrong trees. With **Keep Felled** (the Haven) nothing comes back by itself.
- **Small trees and saplings**: a painted tree shorter than `TreeChoppingSettings` › Small Tree Height (6 m) gets the Small Tree Proxy instead of the felling one: the Young Tree node, broken down by hand ("Break down", `InteractionVerb`) for sticks and a Cedar Sapling, gone for good at home. Felling a tree also gives a sapling (the Tree node's yields). A sapling (`SaplingDefinition` on the item: what it grows into, days, room) is planted at home by right-click (`ItemInventory.Plant`): `PlantedTrees` (ISaveable, on the Haven terrain) adds it to the terrain's runtime copy after the trees it was made with, so the terrain draws it and the felling stand-ins work on it (small: broken down; grown: felled). Growth is a scale from the in-game day (`SettlementState.DaysPassed` and the hour), set for all planted trees every 4 s, no distance checks; `TerrainTreeMemory` keeps only the trees the terrain was made with (`PlantedTrees.BaseCount`), and a planted tree that comes down leaves the list. Content: `AgentScripts/BuildSaplings.cs`.
- **Edge of the world:** the rim rises into a broken, ridged crest (`HavenWorldPlan.Ground`), steep enough to paint as rock, not a smooth wall.

The woods are laid out as rooms and corridors, read from the top-down camera:
- **Paths by kind** (`HavenWorldPlan.PathKind`, parallel to `Paths`): main roads, the ring, spurs to places, outer loops between neighbouring roads (`PlaceOuterLoops`) and woodland trails. Each kind has its own width (`roadWidth` … `trailWidth`). `TrailClear` checks a trail every 4 m against water, ravines, outcrops, places, the mountain's steeps and the Haven's ring.
- **Glades** (`Glade`: Meadow, Rocky, Lair, Wet; ragged rim via `Open`) are open rooms joined to the network by a trail, sometimes two (`gladeLinks`). Spawn zones take lair glades first, then any dry one (`PlaceZones`). Half the resource clusters go to a glade that suits them (`TryGladeCluster`). Wet glades hold a pool, and some hold a lone landmark tree (`gladeLandmarks`). They are painted with the Haven's meadow ground, so they show as bright rooms.
- **Corridors:** the generator's `Corridors` stamps a tree-free verge beside each tread (scatter clearing 0.45: no trees, undergrowth kept, because cedar crowns spread 4–6 m) and thins the woods for `pathClearing` metres past it.
- **Paths look worn, not ruled** (`PathFeature` natural look, off by default): `widthWander`, `patchiness`, `shoulder` (a lip that catches the light), `raggedGrass`, the Mossy Forest's `Path Edge` palette, and `gradeSmoothing`. A graded path (20–30 m) cuts into rises and fills dips, with banks of 1.6 m per metre of cut (capped at `MaxBank`, which is added to `Reach` so partial rebuilds never clip them).
- **Rivers run downhill in valleys:** `LevelRivers` gives each knot the lowest ground met so far, never under the lake. `RiverValley` (in `Height`) brings the ground down to it, with sides that widen with the cut. The generator puts the river's knots at those levels in `SplineHeight` mode. With `FollowGround`, a running minimum of the shaped terrain had dragged one river 3 m under its valley for the rest of its course. Every path crossing gets a knot of its own (`Fords`), shallow and wide.
- **Readable relief:** the woods' ridges have rounded crests. Their sharp V-crests left 17% of the woods steeper than the trees' 36°, and those bare pale ribbons read as paths; now it is 2.7%. The generator also mosses steep woods slopes instead of leaving them to tan litter.

Measured on seed 1: paths over 20° fell from 15% to 4.5% of samples, and over 30° from 6% to under 1% (what is left is mostly the mountain road). Cedars within 2–4 m of a path's centre are down to 0.14 of the deep woods' density.

Read from the top-down camera (measured at 12 spots: tree crowns covered 62–82% of the screen on woods trails, ravine edges and the Woodland Camp):
- **Canopy cutaway** (`SeeThroughCanopyClip` in `Shaders/SeeThrough/SeeThrough.hlsl`, used by Tree Wind's leaf cards; set by `SeeThrough`): a leaf above the crown start (2.5 m over the player's feet) stipples away when the camera's line through it lands within 14 m of the player. Bark, undergrowth and shadows stay.
- **Ravine rims:** bare at the lip, no trees for `rimClearing` metres beyond it, and stones (`rimStones`) every `rimStoneSpacing` metres along it, found by walking out from the floor to where the ground stops falling.
- **Places** keep `crownMargin` metres more free of trees.
- **Along the way** (`HavenWorldPlan.PlaceJunctions`, `PlaceTrailScenes`, `PlaceGladeDressing`; planned last, so nothing else moves; prefabs from `AgentScripts/BuildHavenWayScenes.cs`): a cairn where each trail leaves another path, a small scene every 60–90 m beside woods paths (none mineable), and each glade dressed for its kind. Landmark trees stand in 70% of glades.
- **Character fill light** (`CharacterFillLight`, on the player): a soft point light on rendering layer 7 that only the player and creatures (`MonsterAI.Awake` marks them) receive, so figures read against the dark woods' floor.

CC0 models for it (Poly Haven, `Art/Vegetation/Haven World/Downloaded`, see its LICENSE.txt) are converted by `AgentScripts/BuildHavenWorldProps.cs`: each download gets a source material and a mask the mesh shader reads (made from Poly Haven's ARM map), is wrapped in an identity-root source prefab (a single-mesh FBX keeps its ×100 scale and 270° turn on its root, which `VegetationPackConverter.Prop` resets) and converted. The results: four mountain rocks (`HavenWorldRecipe.mountainRocks`, strewn by `HavenWorldPlan.PlaceMountainRocks`, planned last so they move nothing else; rock faces turn their open side downhill by their area-weighted normal, and the terrain trees under them are cleared), the Boat Landing (`Prefabs/World`, the pier's straight sections and mooring gantry lowered to a 0.55 m deck, placed by `PlaceLandings` at the lake path's shore and the island with a place) and the Woodland Camp point of interest (a stone fire pit with the Misty Woods' logs, a stump, kindling and a mineable boulder).

## What only raids give

GDD §7.7. **Relics** are enchantments of the `Relic` school (`RelicSettings`, `Data/Essence/Relics`), so a relic keeps its name, tooltip, save and empowering from the enchantment system. What makes them different is `EnchantmentDefinition.Effects`: `RelicEffect`s, which are stateless because one object serves every wearer. `Attach` starts one on a wearer (`PlayerRelics`, added by `PlayerEquipment`) and returns what ends it, and anything it remembers lives in that closure. The altar skips relics (`AltarWork.Enchanting`). `RelicSettings.Grant` is the way in: rumour outcomes (`GiveRelicOutcome`), `LootCache` (`CacheChance`) and `LootOnDeath.relicChance` (the Old Moss Troll).

**Rumours** are `RaidRumourDefinition`s in the raid's Raid Rumours system: a briefing line, conditions (`CanComeUp`) and a `RaidEventDefinition`, so a rumour's site, choices and outcomes are an ordinary raid event with weight 0. `RaidRumours.For` picks them from the seed, so the briefing and `RaidRumourDirector` agree without storing anything. `RaidEventDirector.BeginAt` stands each at a `RaidMap.RumourSpots` place; pinned sites don't count against the random events. A rumour's reward is an outcome: `GiveRelicOutcome`, `RescueResidentOutcome`, `WeakenBreachOutcome`, or Give Loot for a found item.

**People** are the expedition's residents, with jobs that are plain numbers on `JobDefinition` (`ExtraRumours`, `GearRepairDiscount`, `BuildingRepairDiscount`, `WardHealth`, `RaidSeconds`), read through `SettlementState.JobBonus` by the system each one concerns. **Knowledge and living things** are blueprint items with **Learned At Home** set: `ItemInventory` refuses them during a raid, so they are at risk until extracted. **Blows against a breach** are `SettlementState.BreachWeakening`, saved, read by `WardBreachDirector` and cleared when a breach is won. **Essence** stays a raid's: `NightProwlers` sets `LootOnDeath.WithholdsEssence`.

Content: `AgentScripts/BuildRaidReasons.cs` (after BuildRaidEvents and BuildRaidLayout).

## Raids with a reason from home

- **The Haven's asks** (`HavenRequestDefinition` in `Data/Settlement/Requests`, `Resources/HavenRequestSettings`; content by `AgentScripts/BuildHavenRequests.cs`): residents (or the Haven as a whole) ask for what only raids give, for example Bjarne asks for iron ore and Ingrid for bog venom. Each ask has a reward (an item, coin, experience, the blight eased). Up to three stand at once, never two from one person, and only from people who live there and in the ask's season (`HavenRequestSettings.Choose`, seeded by the day, so a reload asks the same). They are saved as `SettlementState.Requests`.
- **Where they show:** in the raid planner's briefing, under the rumours, with how much is home already (`HavenRequestBoard.BriefingLines`), and in the asker's words in passing (`ResidentAsides`).
- **Handing in:** `HavenRequestBoard` (SafeHaven › 05 Gameplay) hands an ask in as soon as what it wants is home, in the bag or the stash. New asks come with the next homecoming.
- **Staying on pays:** each danger stage has a **Loot Bonus** (`RaidStage`, Misty Woods +25/50/80%), and its message says so. `RaidDirector.StageLoot` multiplies what caches and events hold (`LootCache`, `RaidEventOutcomes`) and the essence creatures drop (`LootOnDeath`).

## Fences: the skigard

The first fence, built by the player from the start (Sticks and Oak Wood, no workshop): the old Norwegian roundpole fence and its gate, 100 health each (`Data/Combat/Stats/SkigardStats`). Modelled and textured in Blender by `Tools/Blender/skigard.py`: split spruce rails slanting between post pairs, twig bands, dark worn wood baked to one texture (`Art/Haven/Skigard`). The prefabs and buildings are made by `AgentScripts/BuildSkigard.cs`, from the Palisade's with their own save identity. Each piece is 4 m, and its rails repeat every 4 m, so pieces placed end to end make one fence.
- **The gate** (`FenceGate`): it swings open away from the player, residents and companions as they come up, and shuts behind them. Creatures have to break it. Its sides carve the navmesh; its leaf doesn't, so residents still plan through it.
- **Bending round the ward:** while dragging a line, scroll up to bend it round the portal (`BuildPlacement.LayArc`). Each piece is a chord of the circle through the line's start, spread evenly so the curve ends exactly where dragged (or on a fence end it joins), and a ring closes on itself. Line pieces of the same length join each other's ends (`BuildPlacer.Joins`), so a gate carries on from a skigard.

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
  sets the `DayNightCycle` hour and stops its clock, and seeds the `WeatherSystem` from the run's seed
  (`SetRandomSeed`, `RaidLayout.SeedFor(seed, "Weather")`), so every game on one run (co-op) has the same weather; lightning
  draws from its own random, as it draws every frame and frame rates differ. Night shortens monster sight (`AtmosphereConditions.Night`,
  `PerceptionSettings.SightAtNight`).
- **The apex:** the Old Moss Troll (Content Editor › Monsters; built by `AgentScripts/BuildOldMossTroll.cs`) stands under
  Map Content › Apex in Misty Woods with a `MonsterPatrol` round of four waypoints looping around the Hollow Oak. Weak
  eyes (9 m, 100°), a 6 m nose (close sense) and keen ears (×1.8); 1400 health, poise 450, a 1 s sweep and a 1.3 s
  360° stomp; it drops 2–3 Trollstone. `CreatureFootfalls` thuds every stride (`Audio/Sounds/Library/TrollStep`, heard
  55 m) and shakes the camera within 28 m. Patrols walk at `walkSpeedScale` (`MonsterAI.RoamSpeedScale`) and set
  `MonsterAI.KeepAwake`, so `CreatureSleep` leaves them walking far from the player.
- **Turned maps:** `RaidMap` turns Map Content a frame after placed creatures have started, so it calls
  `MonsterAI.ResetHome` on every monster under Map Content afterwards (their homes turn with them).
- **HUD:** text says the time and the way out, and the maps say where everything else is. `RaidHudView` shows the raid's name for its first 8 s and at a new stage, then only the stage; the nearest *open* exit with its arrow (or the lit one, with "Holding… %", "contested!" or "Back to the circle"), adding "closes in" under a minute and what it costs; and one line of news at a time (`RaidAlertLine`), only for what the message feed doesn't say: where an event opened, a rumour's site found, and the countdown of a downed companion, which takes the line over. Above the vitals: the companion and "At risk" (`RaidRisk.Of`: what a failed raid would cost, refreshed once a second, empty when nothing is). `QuestTrackerView` shows only what can be done on this raid (`QuestObjective.CanProgressOnARaid`, or a Quest Event with a waypoint here) and counts the rest as "at home". `ItemHUD` has two `MessageFeedView`s: Pickups stay beside the character, and what is posted goes in Notices over the vitals. Every exit, rumour and open event is pinned on `MinimapHud` and labelled on `WorldMapHud`; a rumour's mark is rounded (`RumourWay.RoughSpot`) until the player is close enough to see its beacon. The wording is in `RaidHudText`.
- Ideas this is built for: elites (`SpawnGroup` + stat bonus), event-driven `Wake` (a horn, a broken seal), raid
  modifiers that shift stage thresholds, and exits that cost a crafted rune.

## The save system in layers

`GameSession` (memory) → `SaveMapper` (memory ↔ file format) → `SaveData` (file format: ids and numbers only) →
`SaveStorage` (files on disk). `SaveGameService` drives them; `ContentDatabase` turns saved ids back into assets.
Objects in persistent scenes save themselves through `SaveableEntity` and `ISaveable` components.

**Saved games:** a game can have any number of saves, each its own file (a slot; `SaveStorage.ListSlots` finds them, so a
copied-in `.json` shows up too). The game being played writes to `SaveGameService.CurrentSlot`: the slot it was loaded from, or
a new one (`journey_<date>`, named "Journey N", or "<Name>'s Journey" when the character was given a name) the first time it saves, so a new game never touches another. Save, F5 and the
prologue's checkpoints all go there. `SaveData.displayName` is the name; `SaveSlotInfo` is one row of a list, read from the file
without loading it, and a file that can't be read still gets a row so it can be deleted. `UI/Options/SavesScreen.cs` lists them,
from the main menu and the Escape menu: load, overwrite and Save as New while playing, rename, delete. The old `save.json` is
just the first slot ("Journey 1"). F6 loads the current save, or the newest. Continue on the main menu loads the newest.

**Autosave and crashes (GDD §20.6):** `SaveGameService.SaveBeforeRaid` saves when a raid sets out, with the sparks and runes paid and `SaveData.raidUnderWay` set, and `SaveOnceHome` saves once the player stands at home again, past the results screen. Nothing is saved in a raid, so a raid that never comes home simply didn't happen: its haul is gone and nothing can be doubled. Loading a save with the mark tells the player so (`NoticeFor`), then saves it away. Quitting a raid to the menu goes the same way. It costs the raid's haul and spares a failed raid's wear, a fair price for a crash and little gain from pulling the plug. An Editor session playing a scene with no game loaded doesn't autosave, so it leaves nothing for Continue to find.

What the file holds besides the bag, stash and quests: the settlement (`SaveData.settlement`: portal, ward level,
breach, residents with jobs, purification queue), each item's wear (`SlotSaveData.wear`, so saves from before durability
load undamaged), hunger, thirst and the equipped slot, and the buildings the player knows.

Save ids are automatic: prefabs get their prefab id on import, and every scene gets missing or duplicated object ids
filled in when it is saved (`SaveableEntityTools`). Safe Haven > Scene > Assign Missing Save Ids does the same by hand.

## Common changes – where to make them

| I want to... | Do this |
|---|---|
| Add an item, monster, quest... | Content Editor (Safe Haven > Content Editor). No code. |
| Change music, add a sound effect, find unused audio | Sound Editor (Safe Haven > Sound Editor). Music lives on each scene's "Scene Audio" object (exploring / combat / final-stretch / ambience playlists); empty lists fall back to the defaults in `AudioSettings`. `MusicRules.Choose` decides: final stretch → combat → exploring → default. Object sounds (death, pickup) are `SoundDefinition` assets set on the prefab or its definition. |
| Add a new music situation (e.g. boss) | Add an `AudioMood`, a playlist on `SceneAudio`, and a step in `MusicRules.Choose` |
| Make something react when a quest completes or a zone is entered | Add a `GameEventListener` / `GameEventTrigger` in the scene (Add Component › Safe Haven › Quests › Quest Event Listener / Trigger). No code. |
| Make a one-off world event (e.g. "after the first raid, entering the ward blooms moss") | GameObject › Safe Haven › World Event (Zone + Response) or Ward Moss Bloom. The trigger has **When** (zone / interact / manually / `PlayerInsideArea`: inside any `IArea` set as its **Area**, such as the portal's ward), **Requires** (a `QuestCondition`: quest Started/Completed) and **Repeat** (`EveryTime` / `OncePerSceneLoad` / `OncePerGame`). OncePerGame events are remembered in `QuestLog.HappenedEventIds` (saved); the listener's **On Load (Already Happened)** response restores the world state, e.g. `TerrainMossBloom.BloomInstantly`. New reactions = a small component with a public method, wired in the listener |
| Place a quest giver, inspect point, quest zone or listener | GameObject › Safe Haven › Quest Giver / Quest Inspect Point / Quest Zone / Quest Event Listener (pre-wired Interactable and trigger). Create a missing Quest Event with **New** next to any Quest Event field |
| Give an NPC optional quests, offered side by side | `QuestGiver` › **Side Offers** (Ask Text, Text, Accept Text, Waiting Text, Thanks Text); needs a `Talkable`. End them when the story moves on: quest › **Closed By**. No code. |
| Add a step to the prologue | New quest in the chain (prerequisite: the step before), then its page's **Story Chapter** panel: **Add As The Last Chapter**, **Earlier** / **Later**. Behaviour **Story** needs no code: moments are story graphs and scene reactions. No code. |
| Make the party walk somewhere | An empty object with the route's points as children, then Story Graph › **Walk The Party**. Talk on the way: **Wait Until The Walk Reaches** › **Say** with **Aside** on. No code. |
| Turn day to night, or change a scene's whole look, as the story moves on | Story Graph › **Change Time of Day**, **Change Look** (a scene's `SceneLook`), **Change Weather**; for what must stay after loading, the same in the quest or event's **Scene Reactions**. No code. |
| Make a quest be handed back to its giver | Quest › **Turn In To Giver** (Content Editor). The quest waits in `ReadyToTurnIn` until `QuestGiver` (or `sh_quest --action turnin`) hands it in; rewards come then |
| Change the floating !, … and ? over quest givers | Systems Editor › World › Quest Markers (`Resources/QuestMarkerSettings`). No code. |
| Find where a quest or Quest Event is used in scenes | Content Editor quest/event page: **In the Scenes** (read from saved scene/prefab files by `QuestSceneIndex`; click to open). Warnings flag givers missing and events nothing raises. **Select All Uses in Open Scenes** selects every object in the loaded scenes that references it (includes unsaved edits) |
| See how all quests connect | **Safe Haven › Quest Map** (or *Open Quest Map* on a quest page): chains as rows, prerequisite arrows, each awaited Quest Event with what raises it; red = missing. **Drag** one quest onto another to make the second come after the first (loops/duplicates refused), or onto empty space (or use **+ New Quest**) to start a follow-up that copies the prerequisite, chain, start type and hand-in; **right-click** a quest to edit it, add a follow-up, or remove a prerequisite; right-click empty space for a new quest. Layout and link rules are `QuestMapLayout` (pure, tested) |
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
| Tune hit stop, shake, hit flash, knockback, telegraph colors, effects, the weapon trail, blood, corpses, the hurt vignette | `Resources/CombatFeel` (Systems Editor › Movement & Combat). No code. |
| Tune combo timing, charging, focus on an enemy, backstabs, parry window, guard, flinch, poise and stagger | `Resources/CombatTechniqueSettings` (Systems Editor › Movement & Combat). No code. |
| Give a weapon a longer chain, a heavy attack, a counter or a weapon art | Content Editor › Weapons: **Combo**, **Heavy Attack**, **Counter Attack**, **Weapon Art**. Each attack names its **Animation State**; rerun Safe Haven › Setup › Characters › Set Up Combat Animations to retime the built-in ones from their clips |
| Give a weapon family a different swing | Its `Swing(...)` line in `Editor/Characters/CombatMovesets.cs` (add a clip to `StylizedStates` if it is new), then Set Up Combat Animations. Its damage and cadence come from the attack in `From`, so they don't move |
| Change what a monster bleeds, or stop it ragdolling | Content Editor › Monsters › Body |
| Make a monster harder to stagger | Content Editor › Monsters › **Poise** (0 = the game-wide default) |
| Change how far or wide a monster sees, how well it hears, how quickly it spots you | Content Editor › Monsters › **Detection Range** (sight) and **Senses** (Sight Angle, Close Sense Range, Hearing, Alertness). All monsters side by side: Systems Editor › World › Senses & Stealth |
| Change how long the game waits between quests and after fights | `Resources/StoryPacingSettings` (Systems Editor › World › Story Pacing); one quest: its **Start Delay**. No code. |
| Tune noticing, searching, sneaking, the player's noise, the ?/! marks and their sounds | `Resources/PerceptionSettings` (Systems Editor › World › Senses & Stealth). No code. |
| Make something new heard by monsters (a horn, a falling tree, a cart) | `WorldNoise.Emit(position, radius)`; a sound the player makes goes through `Player.Noise.Make`, so it shows on the meter |
| Tune lockpicking: the bar, slips, each press's noise and sounds | `Resources/LockpickingSettings` (Systems Editor › World › Lockpicking). Pins are on each Loot Cache. No code. |
| Change a combat, footstep, world or weather sound | Sound Editor › Sound Effects › Game-wide effects (or the weapon's, attack's or monster's own sound fields) |
| Remake a made sound, or make a new one | Its recipe in `Tools/Audio` (`combat.py`, `interface.py`, `world.py`, `moments.py`) on the models in `foley.py`; run the script from the project root. Each sound seeds itself from its name, so the others stay identical. The import normalizes every clip's peak, so judge loudness at the Sound Definition's volume. Synthesis first; the local Stable Audio 3 model only for what it can't do |
| Tune the dodge (distance, speed, i-frames, cooldown) | `PlayerMovementSettings` asset, Dodge header. How it goes around an enemy: Combat Technique › Dodging around an enemy. No code. |
| Give a monster a longer attack warning | Raise its attack's **Hit Delay** (the telegraph lasts exactly that long) |
| Make one target react differently (a boss that can't be pushed) | Add `HitReaction` to its prefab and change its fields |
| React to the player dodging (sound, stamina) | Listen to `PlayerDodge.Dodged` |
| Tune hunger/thirst, carry weight, death and repairs, raid time and Portal Sparks, the ward, purification or breaches | Systems Editor (Safe Haven > Systems Editor). No code. |
| Add a setting to the Options screen | A `GameOption` in `Core/Options/GameOptions.cs`, in its tab's list. Read it where it matters; if it must act at once, handle it in `OptionsSystem`. See "Options" |
| Let a new action be rebound | A row in `InputRebinds.Rows` |
| Add a raid event | Content Editor › Raids › Raid Events: warning, site prefab (`RaidEventSite` on its root, an Interactable with Kind Use when it has choices) and choices; add it to the raid's pool in Systems Editor › Raids & Portal › Raid Systems › Raid Events. A new kind of result is a new `RaidEventOutcome` class; the editor's menu finds it. See "Raid events" |
| Give a food a lasting bonus, or make a tonic | The item's **Boon Bonuses** and **Boon Seconds** in the Content Editor; **Tonic** takes no meal slot; **Cures** lists status effects it clears. No code |
| Add a status effect | New `StatusEffectDefinition` (Create › Safe Haven › Combat › Status Effect) and set it as an attack's **Applies Status**. No code |
| Add a crop, or anything that takes time at home | A recipe at the Farm Plot or Beehive with long Craft Seconds: stations work during raids. No code |
| Add a rumour | A `RaidRumourDefinition` in the raid's Raid Rumours system (Systems Editor › Raids & Portal › Raid Systems), with a raid event at weight 0 for what is found. See "What only raids give" |
| Add a relic power | An `EnchantmentDefinition` of school Relic in `Data/Essence/Relics`, listed in Systems Editor › Relics. A new behaviour is a new `RelicEffect` class; the inspector's menu finds it |
| Change what a generated map can look like | `Data/Raids/Maps/MistyWoods Layout.asset`; check with Safe Haven › Content › Raid Layout Preview |
| Give a job something to do for the haven | A number on `JobDefinition`, read through `SettlementState.JobBonus` by the system it concerns |
| Add a raid destination | New `RaidDefinition` (Content Editor) + add it to the portal's destination list; base rules show in the Systems Editor |
| Change when a raid takes place (day/dusk/night chances, night's reward) | Content Editor › Raids › the raid › Rules › **Time of day** |
| Change how long an exit is held, its circle or how loudly a lit beacon calls | The raid's **Extraction Seconds** (Content Editor › Raids), and on the scene's `ExtractionPoint`: **Time Multiplier**, **Hold Radius**, **Call Radius** / **Call Interval** |
| Give a new map fog, fog of war and night | Safe Haven › World › Sky and Weather › Fog › Set Up Open Scene, then › Apply Preset; pockets with GameObject › Safe Haven › World › Fog Area. See [atmosphere](../../../Docs/Atmosphere.md) |
| Make a tree, roof or prop dissolve when it hides the player | Safe Haven › Setup › Rendering › Make Materials See-Through › Selected (URP Lit/Unlit → See-Through copies; Tree Wind and Layered Lit already do). Size of the circle: `SeeThrough` on PlayerArmature |
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
| Make a sound dip the music while it plays | Tick **Takes The Mix** on its `SoundDefinition`, for what the player must not miss. Anyone talking does it by itself; how far and how fast is Audio Settings › Making Room (`AudioDucking`) |
| Give a speaker a voice | Spoken lines: add the name to the cast at the top of `Tools/Audio/dialogue_voices.py` with a description, then Safe Haven › Voice Lines › Speak new lines (see "Spoken dialogue"). A sound instead of speech, for lines without a recording: set **Voice** on their `StorySpeaker` in a story graph, or pass it to `DialogueSpeaker` in code; **Voice From** says where it comes from when they have no model. The portal's is `AgentScripts/BuildPortalVoice.cs` |
| Voice a new or changed line | Write the line where it belongs, then Safe Haven › Voice Lines › **Speak new lines**. To change how one is said, select it there, set Mood / Feeling / Take, **Speak this line again** |
| Give an item, building, skill, perk, essence or status a painted icon | Add its id and what its icon shows (one object, by its looks) to its list in `Tools/LocalSpawn/` (`item_icons.json`, `building_icons.json`, `skill_icons.json` for combat skills, `proficiency_icons.json`, `perk_icons.json`), run `Tools/LocalSpawn/generate_item_icons.py all --model qwen-image-draft --set <items|buildings|skills|proficiencies|perks> <id>` (LocalSpawn, "Safe Haven - Item icon" style, Icon 512x512, background removed, fitted to an even margin; it runs on the GPU, Unity can stay open), then `AgentScripts/BuildIcons.cs` (entry Items, Buildings, Skills, Proficiencies, Perks or All). A definition alone on its icon file gets the painting in that same file, so content builders that load the file keep it; shared files are split. A builder that renders or paints its icon every run (BuildDock, BuildTorch, BuildTransport, some in BuildHavenProgression) puts it back: `ItemIconTests` / `PaintedIconTests` fail, run BuildIcons again. Effects (motion, glow, sound) must be solid painted shapes: background removal strips thin ones. All 204 items were painted 2026-11-11; Qwen-Image (draft) was chosen over Z-Image because its icons read at slot size and follow the prompt |
| Paint a portrait, a raid's pictures, a raid event's scene, a journal page, the Haven's camp at a rank, a surface in the world or a tutorial picture | Write what it shows in its list in `Tools/LocalSpawn/` (`creature_portraits.json`, `resident_portraits.json`, `raid_art.json`, `event_art.json`, `journal_art.json`, `haven_art.json`, `surface_art.json`, `tutorial_art.json`), run `Tools/LocalSpawn/generate_ui_art.py <creatures|residents|raids|events|journal|haven|surfaces|tutorials> <key>` (Safe Haven Home or Raid style, Qwen-Image), then `AgentScripts/BuildJournal.cs` (Apply for tutorial pictures, PaintedPortraits for creatures; `Portrait` keeps a painting over the render) or `AgentScripts/BuildUIArt.cs` (Residents: the People page and their conversations; Raids: the planner's header and the results screen; Events: above an event's choices, via `MenuPage.Picture`; Haven: `HavenSettings` › Ranks › Picture, the overview's header; Surfaces: the banner's cloth and the notice board's note, which needs SafeHaven open). Journal pages: `BuildJournal.Apply` (a person's page shows their portrait). Essence, status and boon icons are `generate_item_icons.py --set resources|conditions|boons`, then `BuildIcons` (Essences, Conditions) and `BuildUIArt.Boons` (`Resources/ConditionIconSettings`, by the start of a boon's id; a meal shows its item's icon) |
| Add a resident or a job | Content Editor (Residents, Jobs). A job can teach buildings when assigned |
| Make a new cost (a station, an upgrade) payable from storage at home | Pay it through `MaterialPool.For(player)`; never take from the inventories directly |
| Give a world object a menu of choices (an NPC, a station) | Write a method that builds a `MenuPage` of `MenuAction`s and call `ActionMenuWindow.Current.Open(BuildPage, user, transform)`; the page is rebuilt after every choice (see `HomePortal`). Pass `onClosed` to react when the conversation ends |
| Keep something locked until the story reaches it (the rootbound stones, the portal) | Add `QuestGate` next to its `Interactable` and pick the quest; the prompt shows the locked text |
| Change what the portal says at each step | Portal in SafeHaven: **Quest Whispers** (one line per active quest). The last active one in the list speaks first, so the main story goes last; **Listen** goes through the lines of the other open quests. `AgentScripts/BuildQuestCategories.cs` sorts the list by kind |
| Have a conversation with the portal at a step of the story | Give that step's Quest Whisper a **Moment** (a Quest Event) and make a Story Script that starts on it (**When Quest Event**). It replaces the tree's menu once; end the story with Raise Quest Event if a quest waits for the talk |
| Make a quest wait for a Haven rank | A **Reach Haven Rank** objective (its rank as in Systems Editor › World › Haven); Count Already Owned counts a rank already reached. A story starts on it with **When Player: Reach Haven Rank** |
| Make a quest ask the player to talk to someone, reach a place or use a thing | **Talk To** (choose the person), **Reach Place** / **Use Object** (choose or make a Quest Target with **New**). The quest's **Make It Work** list then offers to put a Quest Zone or Inspect Point for it at the Scene view, or to make the selected object it. No Quest Event is needed |
| Start a quest when the player does something | **Start** › **When Player Does**, then **Start When**: any objective, e.g. Reach Place: the Wilds Echo |
| Start a story when the player talks to someone or reaches a place | Story Graph › **When Player: …** with the deed; **Count As Done** in a story counts a deed done (e.g. the talk once its lines are said) |
| Start a story after the player's Nth raid | Story Graph › **When N Raids Are Done** (`WhenRaidsDone`): checked as the story wakes (home from a raid, a game loaded), any way the raids ended; the portal's call about the boundary stones uses it (`BuildHavensReach`) |
| Add or change an enchantment | Create > Safe Haven > Essence > Enchantment, then add it to `Resources/EssenceSettings` › **Enchantments** (Systems Editor › Essence). A Cursed one must state its drawback as a negative bonus (`EssenceTests`) |
| Change what empowering or skill ranks cost and give | `Resources/EssenceSettings` (Systems Editor › Essence) |
| Add a kind of cart or boat | A `TransportDefinition` in `Data/Transport`, a prefab with `Transport`, `SaveableEntity`, an `Interactable` and a driver (`Haulable` or `Rowboat`) with every collider on Ignore Raycast, and a kit item whose Transport points back at it. See `AgentScripts/BuildTransport.cs`, and call `SaveableEntity.RefreshPrefabId` on a new prefab or it can't be recreated from a save |
| Change what a transport carries and how it slows the player | Its `TransportDefinition` (Systems Editor › Transport) |
| Change how much worse, and richer, the night is | `Resources/NightSettings` (Systems Editor › Loot & Rarity); how many more creatures a raid holds at an hour is that hour's **Enemy Density** in the raid's Time of Day list |
| Show something new on the minimap | Add it in `MinimapHud.ReadMarkers`, from a list the system already keeps; check `Found` so nothing shows through the fog of war |
| Add a ward breach | A `WardBreachDefinition` (Content Editor › Ward Breaches) with **Threatens Home**, **Lane Count** and **Waves**, then add it to `Resources/WardBreachSettings` › **Breaches** (Systems Editor › Ward Breaches). The last in the list is the one that repeats |
| Change who the Haven window counts as the lost expedition | `Resources/HavenSettings` › **Expedition** |
| Change how fast the ground soaks and dries | The scene's Climate: **Soak Seconds**, **Dry Seconds**. The puddles themselves: `WorldWetness` in `AgentScripts/GenerateWorldTerrainShader.py`, then run it (see [Weather](../../../Docs/Weather.md)) |
| Tune felling: what each stage pays, how many sections, how far it is heard | `Resources/TreeChoppingSettings`. Felling Share is how much of the old yield comes from bringing the tree down; swings per section are on `Data/Gathering/FelledTree`. No code |
| Tune what a falling tree does to things: damage, who it spares, how hard it knocks trees | `Resources/TreeChoppingSettings` › What It Hits On The Way Down. No code |
| Change which noises send birds up out of the trees | `Flush Noise` and `Flush Flocks` on the place's `BirdProfile`. Anything through `WorldNoise.Emit` counts. No code |
| Change the birds, or add them to a place | A `BirdProfile` in `Data/World`, and the Birds prefab in the scene with that profile. `AgentScripts/BuildBirds.cs` draws the sprite sheet and makes the sounds' assets; `Tools/Audio/birds.py` makes the sounds. Nothing in the game hears or sees them; they go up when a creature comes near (`Birds.MonsterNear`) |
| Change how long the seasons last or how they look | `Data/World/Season Cycle` (days, autumn, leaf fall, frost, snows, lying snow, climate per season; turning days). Which trees turn: **Deciduous** on their Tree Wind material. No code |
| Add or tune wild animals | A Wildlife prefab in `Prefabs/World/Living` (shy distance, speeds), and `HavenWorldRecipe` › Wildlife for where herds go; regenerate the world's Finish step to place them |
| Change what residents say in passing | `Resources/SettlementSettings` › Asides (homecoming, morning, day, evening lines; Aside Every) |
| Change fireflies, or add them to a place | A `FireflyProfile` in `Data/World`, and the Fireflies prefab in the scene (see [Weather](../../../Docs/Weather.md)) |
| Change how crafting looks in the bag and at stations | Both are one `RecipeBrowser` (`UI/RecipeBrowser`, built by Safe Haven › Setup › UI › Build Recipe Browsers). It reads and makes through an `IRecipeMaker`: `HandMaker` or `StationMaker` |
| Add a station recipe or tune craft time, output slot and queue size | Recipe: set **Station** and **Craft Seconds** in the Content Editor (Station "By Hand" = instant from the bag). Overview in Systems Editor > Crafting; output slot and queue length per building in Systems Editor > Portal & Building. Station state lives in `SettlementState.Workshops` and is saved |
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
| Lay a path or a trodden clearing into the terrain | GameObject › Safe Haven › World › Path, or World Tools › Build › Path (a spline you can move later; the old ground grows back) |
| Place rocks, trees or other prefabs on the ground | World Tools (Safe Haven › World Tools, or the Scene view's Safe Haven World panel) › Place: click a picture, click in the Scene view. With **Stick to ground** on, anything dragged in, duplicated or moved settles on the ground |
| Make a scene playable as a home or a raid | World Tools › Scene › Make Playable (or Safe Haven › Scene › Make Playable as …); the list below it says what is still missing, with fixes |
| Add a river, cliff, ravine, forest or biome | World Tools › Build (by look) or GameObject › Safe Haven › World › River / Path / Cliff / Ravine / Forest Area / Biome Area, then click points on the ground and press Enter. It builds itself whenever you change it; see [level design](../../../Docs/LevelDesign.md) "World Builder" |
| Paint a whole terrain by its shape | The terrain's World Terrain › Dark Forest Look or Haven Look, then Auto-Paint with Rules (rules: Data/World/Paint Rules - …) |
| Check a level before committing it | Safe Haven › Scene › Level Check, or `unity command sh_level` |
| Change the raid results artwork | `RaidDefinition` › Results Screen (per raid) or `RaidResultsScreen` in the RaidResults scene (default) |

## Safe Haven: home, survival and durability

- **At home or in a raid?** `RaidDirector.Current == null` means at home. `MaterialPool.For(player)` uses it: in a raid
  only carried things count; at home storage counts too. Costs are paid carried-first and never half paid.
- **Arriving home:** `RaidDirector` sends the player to the `PlayerArrivalPoint` named "Portal" after an extraction and
  "HomePortal" after death or time-out. Dying at home (a ward breach) is handled by `HomeRevival`.
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
  `GatheringMinigameSettings.DefaultFeedback`) and exposes UnityEvents for per-prefab one-offs. The marker rises once per
  swing and, with no press, the tool comes down as a normal swing; only a press well before the zones is glancing. Glancing blows
  wear the tool by `DurabilitySettings.WearPerGlancingBlow`. `TimingBarView` is the procedural bar. In a raid each
  swing goes through `GatheringNoise` (wakes `CreatureSleep` within a radius scaled by loudness, feeds annoyance). `ForestAnnoyanceSystem`
  is a raid system (Raid Systems tab, tuned per raid) that `ForestAnnoyanceDriver` runs: Stirring raises `RoamingSpawner` caps/rate,
  Angry calls `HuntNow`, Wrath spawns one wave from its table; `ForestAnnoyanceView` shows the meter. `RaidDirector`
  tallies swings into `RaidSession.Gathering` (`GatheringStats`, plus peak annoyance from the driver), shown in the raid
  summary by `RaidReportText.Gathering`. Tuning: Systems Editor > Player > Gathering Minigame.
- **The swing:** the result of a gathering swing is settled by the press, but the blow is not. On the Mine Wall and Mine Ground
  states `WorkSpeed` holds the clip raised `SwingWindUp` seconds before contact (measured from the clips into
  `GatheringMinigameSettings` by `BuildWoodcutting`, using `CombatAnimationSetup.ContactTimes`); the press releases it, and
  `PlayerGathering.LandBlow` runs when it lands, so sound, chips, noise and completing the node all arrive with the tool.
  A node that is more than its bounds implements `IHarvestBody`: where the tool lands, and its size for
  `AudioManager.Play(sound, position, size)`, which pitches and levels one recording for a sapling or an old oak.
- **Felling** (`Gathering/*`, all numbers in `Resources/TreeChoppingSettings`): `ChoppableTree` opens a `TreeNotch`, shakes leaves
  down and creaks on each swing, and on the last one reads its model into a `TreeParts` (plain data; a scene tree and a painted terrain tree,
  through `TerrainTreeFelling`'s stand-in, read the same way) and starts a `FelledTree` from the Felled Tree prefab. That is a
  real rigid body on a hinge at the cut: gravity does the fall, the hinge gives at 62 degrees so the butt kicks, and the
  logs of the felling (`FellingLogs`, a share of the old yield) are given when it lands. `FellDirection` picks open ground away
  from the player. Only the trunk collides, and only with a `TreeGroundPatch` copied from the terrain's heights on a layer
  of its own: the crown just sinks in, and `Physics.IgnoreCollision` on a TerrainCollider doesn't reach its painted trees,
  which would hang the trunk. So a tree passes through a wall if it falls into one; the direction it picks avoids that. Once still, `FelledLog` takes over through the Felled Tree node, so the swing minigame,
  bonus logs and noise work as usual: the first run limbs it (sticks, and the model is swapped for a clean `LogMesh` trunk),
  each later one cuts a section from the butt (logs, and a `TimberPiece` rolls free). Only the next section can be reached.
  On the way down `TreeImpacts` crushes what is under it, by how fast that part of the tree is moving: creatures are the
  feller's kills, allies are spared, the player takes a share (`CrushShare`). Standing trees it meets are lines to it
  (`TreeContact`), as painted ones have no colliders of their own: each is knocked (`TreeSway`, a damped `TreeSpring`), keeps a
  little of the lean, sheds leaves and takes speed out of the fall. A painted tree can't lean, so `TerrainTreeFelling.Knocked`
  stands a copy in for it while it sways. The stump is left until the tree grows back. The fallen tree is not saved: a save and load clears it, and it clears
  itself after `FallenTreeSeconds`. Sounds: `Tools/Audio/woodcutting.py`; textures: `Tools/Art/timber_textures.py`;
  prefabs, profiles and particles: `AgentScripts/BuildWoodcutting.cs`.
- **The portal** (`HomePortal`) is the hub: talking, starting the pending breach, strengthening the ward (which grows
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
| Systems Editor | Rules and numbers: survival, carrying, death and repair, movement and combat, raids and the portal, the portal and building, residents and purification, breaches, loot and rarity. Edits the settings assets and shows derived numbers (minutes until hungry, raids until gear breaks, raid length with Sparks) | Safe Haven > Systems Editor |
| UI theme | Window, button and text colors of every UI prefab | Safe Haven > UI |
| Level tools (`Editor/Level`) | Building the world by hand: **Scatter Brush** paints weighted `ScatterPalette`s (`Data/Level/Palettes`: forest trees, rocks and logs, wood and stone nodes, forage) on the ground with spacing, slope tilt and sink (Shift erases); **Drop to Ground** (End / Shift+End aligns to the slope) sets objects down by the bottom of their meshes; **Level Check** finds floating, buried, unreachable, under-water, crowded and off-terrain gameplay objects, save id problems, missing scripts and leftovers, with fixes, and counts the level's contents. See [level design](../../../Docs/LevelDesign.md) | Safe Haven > World, Safe Haven > Scene |
| World Tools (`Editor/Level/Tools`) | The level designer's hub. `WorldToolsWindow` (tabs Place, Terrain, Water & Plants, Sky & Weather, Scene; its buttons run the Safe Haven › World / Scene menu items, listed in `MenuPaths` and checked by `WorldToolsTests`), `WorldToolsOverlay` (Scene view panel), `PrefabCatalog` (level prefabs by folder and name into designer groups, and by set: the two `WorldLook` packs or Original), `GroundLayerPainter` (Terrain › Paint ground: adds a layer to the terrain, rebuilds its displacement and opens Unity's Paint Texture brush on it by reflection, best effort; Terrain Tools layer palettes per look; a look's plants as detail prototypes) and `TerrainLayerWatcher`, `SceneFolders` (the numbered top-level folders 00 Systems … 07 Ambience, Safe Haven › Scene › Tidy Hierarchy; tools that look for a named top-level object use `SceneFolders.Find`/`IsUnder`/`Place`, so it may sit inside a folder), `PrefabPlacer` (click-to-place with a drawn preview, never a scene object; parents under Level Props or a raid map's content), `GroundStick` + `PlacementSettings` (EditorPrefs; drag-in, duplicate and Move-tool drops settle on the ground via `GroundPlacement`, joining the move's undo step), `ScenePlayability` (Make Playable copies the gameplay roots from SafeHaven.unity or Raid_MistyWoods.unity with `Unsupported.DuplicateGameObjectsUsingPasteboard` so references between them hold, stands the player, adds the kind's pieces, bakes the NavMesh, adds to Build Settings; `Check` lists errors, warnings and notes with fixes) | Safe Haven > World Tools, Safe Haven > Scene |
| Level spawners (`Level/MobSpawnZone`, `Level/ZonePolygon`, `ResourceInventory/RandomResourceNode`) | `MobSpawnZone`: a drawn polygon that spawns creatures from its list or a Spawn Table on start, when the player comes near or on `Spawn()`, refills on a timer, raises `Cleared` (editor: `MobSpawnZoneEditor` handles). `RandomResourceNode`: becomes a gathering node of a kind and tier range from `ContentDatabase.GatheringNodes`. `ResourceNode` applies `GatheringNodeDefinition.ModelVariants`/`VariantScale` per spot. See [level design](../../../Docs/LevelDesign.md) | World Tools › Place |
| World Builder (`Level/World`, `Editor/Level/World`) | Non-destructive, spline-based world building. Runtime assembly holds only data: `WorldFeature` (a `SplineContainer` plus settings; `RiverFeature`, `PathFeature`, `CliffFeature`, `BiomeArea`), `KnotValues` (per-knot multipliers kept with their knots), `WorldTerrain` (on a terrain: settings and what was built, by hash), `WorldTerrainStash` (asset: the ground before any feature, per touched height/paint/detail cell, and removed trees), `CliffKit`, `TerrainPaintRules`, and `ScatterPalette` (moved here so scene components can use palettes). Editor: `WorldBuilder` watches feature edits, spline changes, deletions and hand sculpting, and rebuilds the affected terrains after the mouse is released (each build joins the triggering edit's undo step); a build reads the heights (absorbing hand edits: base += current − last), runs each `FeatureBuilder` (River/Path/Cliff/Area) in order over `HeightLayer`/`PaintLayer`/`ClearLayer` working sets built from `LineField` distance fields of the sampled `SplineLine`, makes room in the terrain if needed, restores cells no feature touches any more, clears and restores grass and terrain trees, moves grounded objects with the ground (`GroundGlue`) and regenerates each feature's `Generated` objects (river water mesh with per-vertex flow and a `WaterSurface` centre line, waterfall plunges, cliff pieces fitted to the face, scatter). `WorldAssets` makes the CC0 prefabs, palettes, ground layers, cliff kit, paint rules and materials; `WorldLook` describes the two converted packs the menus now build with (Forest Lake for the Haven, Mossy Forest for the raids: prefabs, grounds, palettes, crags, paint rules, the terrain material template) and makes the Mossy Forest's data; `WorldPaint` auto-paints by slope/height/hollowness; `WorldEditors` has the draw mode and knot handles (and `UseLook`, which gives a terrain its own copy of a look's tessellated material and grounds); `WorldMenus` the GameObject menu. `Shaders/Terrain/WorldTerrainLit` is URP Terrain Lit plus triplanar rock on steep ground, anti-tiling and colour variation, generated by `AgentScripts/GenerateWorldTerrainShader.py`. Tests: `WorldBuilderTests`. See [level design](../../../Docs/LevelDesign.md) | GameObject > Safe Haven > World, Safe Haven > World |
| Haven World Planner (`Editor/Level/HavenWorld`) | The Haven world's recipe drawn as a map and counted before anything is generated: seeds, layers, problems (unreachable ground, dull walks, dead ends, empty ground), a readout under the pointer. See [level design](../../../Docs/LevelDesign.md) › The Haven world | Safe Haven > World > Haven World Planner |
| Sky Controller | Time of day, clock speed and weather, in edit and Play mode | Safe Haven > World > Sky and Weather > Sky Controller |
| Environment HUD | Read-only top-centred clock/phase timeline and vector weather symbol, bound to the scene sky rig; no separate clock or saved state (see [weather](../../../Docs/Weather.md)) | Safe Haven > Setup > UI > Add Environment HUD to Scene |
| Voice Lines (`Editor/Audio/VoiceLinesWindow`, `DialogueVoices`) | Every line a character says, whether it is recorded, and the buttons that record it: **Speak new lines** finds the lines, has LocalSpawn speak the ones without a recording and adds them to the game; a selected line can be heard, given a mood, a feeling in words, a strength or another take, and spoken again. See "Spoken dialogue" | Safe Haven > Voice Lines |
| Planner import (`Editor/Planner`) | Reads `manifest.json` from the external Progression Planner app (`D:\Safe Haven\ProgressionPlanner`, outside this repo). Shows New / Changed / Error entries with old → new values and writes only after confirmation. Never deletes or overwrites; matches existing assets by GUID, then id. Kinds: items, essences, buildings, recipes, gathering nodes and weapons (`WeaponDefinition`, keyed by asset name; edited, never created: new weapons come from the Content Editor). Items carry their weapon reference and armor stats (`stats` lines). CLI: `sh_planner_preview`, `sh_planner_apply` | Safe Haven > Content > Import Planner Data |

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

## Cheat menu (F1)

For playtesting in the Editor and in builds: switches that stay on across scenes (invincible, one-hit kills, endless
stamina, no hunger or thirst, move fast, free costs, no requirements) and one-off actions on five tabs (player, items,
raid, spawn, world).

- `Cheats` holds it all as plain static calls with no UI, and `sh_spawn` shares its `Spawn`. Switches live for the session
  and are never saved. Each use raises `Cheats.Used`, which `PlaytestLogger` writes as a `Cheat` row, so a helped run shows
  in its CSV.
- The game asks `Cheats` in only three places: `MaterialPool` (free costs), `BuildingUnlocks` and `StationCrafting` (no
  requirements), and `RaidLauncher` (no requirements opens a locked raid).
- `CheatSystem` (persistent, from `GameSystems`) listens to System/CheatMenu, gives each new player the switches that are
  on, and ticks those that keep working.
- `CheatWindow` is built in code in the scene the first time it is asked for, and goes with the scene. It holds a `GamePause`
  while open, so the raid clock stops and typing in its search box can't reach the Player map.
- The Raid tab starts a raid of a chosen layout mood ("type"), shape and size (the type's own, Normal or Large): `Cheats.StartRaid` finds a seed that rolls them (`RaidLayoutPlanner.FindSeed`), so the run is an ordinary one. The state line names the running raid's mood, shape, size and seed.
- `Resources/CheatSettings` lists creatures, raids, weather and essences, as a build can't search the project. Refill it with
  `AgentScripts/BuildCheatMenu.cs` after adding one; `CheatTests` fails until then. **`inReleaseBuilds` is on so builds can be
  playtested: turn it off before release.** The Editor and development builds can always open the menu.

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
  after each swing; a press before the swing lands is buffered and only misses the rhythm bonus (`EarlyPressFumbles`
  makes it fumble the chain instead); a press right as a swing recovers builds a small rhythm bonus. The last step is
  the finisher (more damage and poise). The light swing starts on the press; held for `HeavyHoldSeconds` while it still
  winds up, it turns into the **heavy attack** (`heavyAttack`, paying only the stamina difference); a ready counter
  uses `counterAttack`.
- **Poise bar:** `WorldHealthBar` copies its own parts into a thin strip under a monster's health at runtime and reads `MonsterAI.Poise`
  (Options › Gameplay › Poise Bars), so no prefab needs it.
- **Landing a swing** (`PlayerCombat`, arithmetic in `StrikeRules`, tested): `AttackDefinition` plays a build-up (`AnimationSpeed`), a
  strike (`StrikeSpeed` for the last `StrikeLead` seconds) and a follow-through; `PlayerAnimationController.UpdateSwingSpeed` drives them
  through `ActionSpeed`. The lunge (`StepIntoBlow`) runs in the last `LungeSeconds`, ending at contact. A held heavy swing waits at the top
  of its wind-up (`IsCharging`, `Charge`, `ChargeFull`) with its clock stopped by `chargeHeld`. `LandSwing` finds targets, orders them by
  `StrikeRules.SweepPosition` and queues each as a `PendingHit`, applied through `AttackResolver.Apply`, so a sweep strikes in the order the
  blade reaches them. `AttackDefinition.Impact` scales hit stop, shake, blood and the corpse's throw; it changes no damage. Monsters still
  use `AttackResolver.Execute`, one frame for all.
- **Dodging around** (`PlayerDodge.SteerAround`, rules in `DodgeRules`, tested): with `PlayerFocusTarget.HasFocus`, steered toward or past the target,
  the dash turns each frame (`PlayerMovement.SteerDash`) toward `DodgeRules.SpotBehind`, along the circle while the straight way is blocked, and ends on
  arrival. `CanBeCutShort` holds a rush back until then. Any dodge ignores collision with nearby monsters' bodies while it lasts (`PassThroughBodies`).
  `MonsterAI.IsOpenToBackstab` is staggering, or `State.Attacking` for a definition with `OpenWhileStriking` that isn't `IsElite`. `PlayerDodge` refunds
  part of the dodge on `PlayerCombat.Backstabbed`.
- **Enemies answering backstabs** (all in `MonsterAI`, numbers in Combat Technique): `OnDamaged` sees `HitTechnique.Backstab`, breaks off the strike and sets
  `answerAt`; `AnswerBackstab` starts a strike (`MonsterAttacks.ChooseAnswer`, ignores cooldowns) or a hop (`State.Hopping`, invulnerable). `BackstabMemory`
  (plain, tested) is per instance; those of its side nearby learn too. While striking, `SeesBackstabComing` polls the player's wind-up, one roll per swing.
- **`MonsterAI` is an `IHitGuard`** on its own `Damageable`: `parryUntil` for a parry it saw coming (`PlayerCombat.Rebuff` throws the player back), and the
  definition's shield (`GuardRules.ResolveShield`) while `Chasing`. A blocked blow still pays poise through `OnDamaged`. New shield-bearers need a
  `ShieldArc`, a slow `turnSpeed` and a `MonsterShieldPose` on the prefab, which raises the arm over the animation.
- **Taking turns at the player:** `MonsterAI.HoldsBack` counts others with `MonsterAttacks.IsClosingIn` each time one is ready to strike (no tokens to leak),
  and `AttackTurns` (plain, tested) says strike, stand ready or circle. Monsters targeting anything but the player don't take part.
- **Combos and held blows** are data on `MonsterAttack` (`followUps`, `holdChance`), melee only. `MonsterAttacks.Blow` moves on and `MonsterAI.UpdateAttack`
  raises `AttackStarted` again, so the animation, pose and telegraph treat each blow as a strike. A hold stops the strike's clock: `IsHolding` is what
  `AttackTelegraph`, `MonsterPose` and `MonsterAnimationDriver` also stop for. New follow-ups on a humanoid need a line in `CombatMovesets.MonsterAttackStates`.
- **Heavy swings that push through:** `WeaponDefinition.HeavySwingArmor`, read by `PlayerCombat.IsPushingThrough`, which sets the player's `DamageTakenMultiplier`
  and skips the flinch. The player's multiplier belongs to `PlayerCombat`.
- **Archery feel:** `ArrowProjectile.Strike` parents the arrow to the nearest bone or rigid part (`NearestBodyPart`, found before the blow, so a kill's corpse
  takes it along) and `ReturnToPool` takes it back out; its trail is made in code. Body sounds come from `CombatFeedback` (the bow's Hit Sound), `arrow.HitSound` is
  scenery only, and `HeardAt` brings the player's far blows within earshot. `PlayerFocus` earns from `Combatant.DamageDealt` on the bow's shot.
- **Rush attacks** (`SwingKind.Rush`, `WeaponDefinition.RushAttack`): `PlayerCombat.WantsRush` turns a press during or just after a dodge, or on
  the run (`StrikeRules.IsRunningAttack`), into a rush; a queued rush begins once `PlayerDodge.CanBeCutShort`, and `CutShort` ends the dash. Its lunge
  runs for the whole wind-up. **The fight:** `StrikeRules.FightIsOver` keeps `GameState.Combat` while `IsHunted` (a monster within `FightHoldRange` is
  `IsEngaging` the player); being struck, blocking or evading enters it.
- **Planted heavy swings** (`PlayerAnimationController.plantedSwing`): a heavy swing plays on Upper Body until its strike, then `StrikeWithWholeBody`
  crossfades the same clip on Combat Full Body at the same clip time. **Left hand:** `TwoHandGrip` (`[DefaultExecutionOrder(100)]`, after the animator)
  solves the left arm with `ArmSolver` onto a point along `HeldWeaponShape`'s handle.
- **Focus on an enemy** (`PlayerFocusTarget`, rules in `TargetRules`, tested): reads `MonsterAI.Active` five times a second while in
  `GameState.Combat` with a melee weapon, and calls `PlayerMovement.SetFocusDirection`. `AimDirection` then means "toward the focused enemy",
  so swings, skills and facing follow it with no change of their own; `RawAimDirection` is still the cursor. It stands aside while the aim
  points past `FocusFreeAimAngle`. `PlayerCombat.TrackFocus` keeps a winding swing turning toward it. Ring: `Graphics.RenderMesh`.
- **Weapon arts and backstabs:** `WeaponDefinition.WeaponArt` / `WeaponArtFocus`, `SwingKind.Art`, `HitTechnique.WeaponArt`. Input is the
  CycleArrows action (the bow ignores melee, and melee ignores the bow) or attacking while guarding without a shield. Focus pays and `CostOf`
  charges no stamina; arts earn no Focus. A hit on a `MonsterAI.IsOpenToBackstab` from within `BackstabArc` behind it is `HitTechnique.Backstab`
  (`PlayerCombat.IsBackstab`); `BackstabMarker` draws the wedge, added by `CombatFeedback` on `AnyStaggered`, warming with `BackstabReadChance`.
- **Blood and corpses** (made in `CombatFeedback.OnAnyDamaged`): `BloodEffects` is one scene-wide prefab of three world-space particle systems
  that emits each particle itself with its own velocity (`StrikeRules.HitForce`, `SprayDirection`); stains raycast the ground and lie along its
  normal. `DeathRagdoll` takes the monster's `Model` (or, without one, its non-UI children) out of it into a new corpse object at the
  moment of the blow, so `Damageable`'s own death, loot and removal carry on untouched; `MonsterPose` notices its visual has left and stops.
  Colliders are on Ignore Raycast and ignore the player's controller. `MonsterDefinition`: `Bleeds`, `BloodColor`, `RagdollOnDeath`. The corpse
  pumps `BloodEffects.Spurt` from the struck body and starts `PoolUnder` once its heaviest body is still; pools are a fourth particle system whose
  Size Over Lifetime is the spreading (built by Build Blood and Markers).
- **Weapon trail** (`WeaponTrail`): a ribbon between the held model's tip and a point along its longest axis, recorded in `LateUpdate`, smoothed
  with Catmull-Rom and drawn with the slash material. `CombatFeel.SlashArcs` keeps the old flat arc available.
- **Movesets** (`CombatMovesets.cs`, part of Set Up Combat Animations): `AttackSpec.From` names the attack a swing takes its numbers and cadence
  from, so swapping a clip changes no damage per second. `CombatMovesetTests` guards states and shape. Humanoid monsters: `MonsterAnimationDriver`
  crossfades `AttackDefinition.AnimationState` at `AnimationSpeed` (fitted to `HitDelay`) and flinches through a `Hit` state.
- **Getting out and flinch:** a dodge cuts a swing short, during the wind-up too (`DodgeCancelsWindup`; the swing is
  lost); the player moves slowly while swinging; a hit that gets through while winding up loses the swing and staggers
  the player (`Flinched`).
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
  outlines it red (`OutlineCopies`, shared with `PlayerInteractionOutline`; `TargetOutline.mat`). `EnemyVisibility`
  keeps every nearby hostile monster outlined through trees and walls: white, or red while `MonsterAI.IsHuntingPlayerSide`
  (it has aggro on the player or a companion); the bow's target shows its own outline instead. `ArrowProjectile`
  (pooled) flies a swept arc, damages through `DamageCalculator` + `Combatant.ReportDamageDealt`, sticks in scenery and
  emits `WorldNoise`. Presentation: `PlayerArcheryAnimation` (the animator's "Bow" layer, under "Upper Body"; it also
  turns the spine after animation so the draw line points at the aim, since the archer clips stand sideways) and
  `HeldBowView` (string between the bow rig's tips, pulled to the right hand, and the nocked arrow),
  `ArcheryCameraAim` (`PlayerCameraController.SetAiming`, `SetBowInHand`: a bow holds the framing still, and the right button turns the view out of a fight), `ArcheryTrajectoryView` (arc preview for long shots, stray included: it is rolled as the draw starts) and
  `QuiverHud` (made by `CombatFeedback`: arrows, draw bar, quick shot). Technique: perfect release
  (`ArcheryRules.IsPerfectRelease`, forced crit), guard button aims with a bow (`PlayerGuard` ignores it then),
  the counter a perfect dodge readies looses an instant shot (`PlayerCombat.SpendCounter`), Z cycles arrow kinds
  (`PlayerStateMachine.ArrowCycleRequested`). Fire arrows add `Burning` (Fire ticks marked `HitTechnique.OverTime`,
  which `CombatFeedback` keeps quiet); stuck arrows may be `Recoverable` (walk over them). Content:
  `AgentScripts/BuildArchery.cs`; the layer: `CombatAnimationSetup.BuildBowLayer` (part of Set Up Combat Animations).
  Tests: `ArcheryTests`.
- **Focus** (`PlayerFocus`): earned from `SwingLanded`, parries, perfect dodges and blocks, drains after a hold; skills
  with a `SkillDefinition.FocusCost` need it (`SkillCastResult.NoFocus`). Shown as the gold bar in `PlayerVitalsView`
  (added by Safe Haven › Setup › UI › Add Focus Bar to Vitals HUD).
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
  Swings and dodges play on the **Combat Full Body** layer (legs included): a swing plants the feet from its start until
  the blow lands (`WindupMoveSpeed`, 0; `PlayerCombat.ReleaseFeet`) and `PlayerMovement.FacingLocked` holds the body on the
  blow, so only the attack's Lunge moves it. Once it has landed the player moves at `SwingMoveSpeed` / `HeavyMoveSpeed`
  (both 0: planted and facing the blow until the swing ends, since arms swinging over running legs looked wrong). Above 0,
  walking off hands the rest of the swing to Attack Arms from the same moment (`PlayerAnimationController.WalkOutOfSwing`).
  The swing's timer runs on, so walking off doesn't bring the next attack sooner; every way out goes through `EndSwing`,
  which frees the feet. The wind-up plays at Animation Speed and, from the hit, the strike at Follow Through Speed.
  The player's body is the N-hance modular human (`ModularCharacter` + `PlayerLook`, see "Characters"); the clips the
  states play are swapped for the pack's where it has them (`CharacterAnimatorBuilder`).
  Locomotion and Combat Locomotion pick a run set by `WeaponStance` (unarmed `Run_*`, `Run_1H_*`, `Run_2H_*`,
  `Run_2HL_*` + `Walk_2HL_*` for polearms, `Run_Bow_*`), so a held weapon is carried rather than pumped with the arms.
  Thresholds are the clips' measured ground speeds (walk 1.6, run 4.8, backward runs 2.1 m/s); `MotionSpeed` scales the
  clip to the actual speed (capped at Max Motion Speed) so the feet don't slide. Rebuild with **Safe Haven › Characters ›
  Build Player Locomotion**. Moving away from the facing direction (cursor-facing in combat, or a locked facing) costs
  speed: `PlayerMovementSettings` › Backpedal Speed (0.7 = 70% straight backwards).
  States and timings come from **Safe Haven › Setup › Characters › Set Up Combat Animations** (`CombatAnimationSetup`: imports
  the Quaternius UAL1/UAL2 clips, adds the states to `PlayerAnimator` and a Stagger state to `MonsterHumanoid`, measures
  each clip's contact moment from the weapon hand's speed and writes Hit Delay / Recovery, builds the weapons' chains).
- **Feedback** (`CombatFeedback`): pooled spark effects for hits, critical hits, blocks, parries, staggers and perfect
  dodges, a `SlashEffect` arc along each swing ("Safe Haven/Slash"), short hitches on parries and perfect dodges, the
  `CombatScreenEffects` vignette (hurt flash, low-health pulse), an optional `ComboHud`, and sounds from Audio Settings
  (attack, then weapon, then game-wide). Effects are built by **Safe Haven › Setup › Combat › Build Combat Effects**
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
  least suspicious but never to spotted, so a sound only brings them to look. Cover between the sound and the monster
  (`MonsterSenses.HasClearPathTo`, the same `SightBlockers`) cuts its reach to `HeardThroughCover`, and the monster places
  it up to `HeardSpreadAtEdge` metres off, less the nearer it is (`HeardError`), so a far sound brings it to the area
  and a near one to you. Sources: `PlayerNoise` (see below), a monster that spots its target (a call), an arrow
  striking (a distraction: it stays off the player's meter), and `GatheringNoise.Investigate` (Forest Annoyance "Angry").
- **The player's noise** (`PlayerNoise` on PlayerArmature, `Player.Noise`): every sound the player makes goes through
  `Make(position, radius)`: its own footsteps (`SneakingFootstepNoise` while sneaking), swimming strokes, hard landings,
  dodges and hits landed or taken, plus those handed to it by `GatheringNoise`, `PlayerArchery` and `PlayerLockpicking`.
  Footsteps, strokes and landings are louder in heavy gear (`GearNoiseScale`, up to `HeavyGearNoise` at the Heavy Load),
  and rain hides the player's noise (`AtmosphereConditions.WeatherRain`, `NoiseInRain`). `NoiseMeter` (plain C#, tested)
  adds a share of each sound to a build-up the next ones carry, so a long fight or a string of slips reaches further and
  further (`NoiseBuild*`); walking stays below its decay and doesn't add up. `Level` holds the last sound for
  `NoiseHoldSeconds` and then falls; `NoiseMeterView` shows it (made by `PlayerNoise`, shown in raids and near monsters),
  with the band's word (`NoiseBands`) so it doesn't rest on color alone.
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
  Other targets (the traveller, residents) are always fully visible. Sneaking crouches the body: the animator's "Sneak"
  layer sits right above Base, so Base keeps running under it and every action layer plays over it; the `Sneaking`
  parameter (off in the air, in water and in death) fades it in and out over a blend of the UAL1 crouch clips on Speed.
  Safe Haven › Setup › Characters › Set Up Sneak Animation rebuilds it.
- **Presentation:** `AwarenessIndicator` (created on a monster the first time it notices anything) floats a "?" that
  brightens with awareness (amber; pale while searching for a lost target) and a red "!" for `SpottedShowSeconds`, in
  the quest markers' font. `SpottedSound` is `Audio/Sounds/Library/Spotted` (a low iron stroke over a drum);
  `SuspiciousSound` is `Audio/Sounds/Library/Suspicious` (a creature's sniff and low grunt), both made by `Tools/Audio/world.py`.
- **Gizmos:** a selected monster draws its sight cone (yellow), close sense (orange), chase range and leash.
- Night, fog and light will plug in through visibility and sight range, the only inputs `PerceptionRules` needs.
- Tests: `Editor/Tests/PerceptionTests.cs` (rates, cone, sneaking range, hearing, noise build-up, stages, line of sight).

### Lockpicking: chests are heard

A `LootCache` with `lockPins` (3 by default; 0 is a pack with no lock) must be picked before it can be searched. Its
Interactable has to be Work, not Use. `PlayerLockpicking` on PlayerArmature holds the interaction open (like
`PlayerGathering`) and runs a `LockSession` (plain C#, tested): green sets a pin silently, yellow with a click, anywhere
else slips and drops a pin; each pin set narrows the zones; `SlipsAllowed` slips force the lock, so a chest always opens,
just loudly. The marker never times out, since the raid's clock is the pressure. Every press goes through `PlayerNoise`,
and on a picked lock the lid is heard as well (`LootCache.LiftLid`). Auto-gather (Options) picks by itself, never in the
green. Rules, noise and sounds: `Resources/LockpickingSettings` (Systems Editor › World › Lockpicking). The bar is
`TimingBarView`, shared with gathering through `ITimingBar`.

### Creating enemies with multiple attacks

In **Safe Haven > Content Editor > Monsters**, use the **Composable combat** fields:

- **Movement Tactic** is Chase or Keep Distance. Preferred Distance and Distance Tolerance control ranged spacing.
- **Temperament** is a multi-select: Aggressive initiates combat, Cowardly retreats below Flee Health Fraction until
  it gains Flee Distance, and Defensive confines engagement to Defensive Radius around home. Defensive bounds take
  priority over pursuit; cowardly retreat takes priority over attack selection. No flags means retaliation only.
- **Attacks** replaces the legacy single attack only when nonempty. Each `MonsterAttack` has its own definition,
  damage, minimum/maximum range, priority and cooldown. Highest eligible priority wins; list order breaks ties.
  Melee, Projectile, Charge, Howl and Summon are supported. Cooldowns start after recovery, including interrupted wind-ups.
  Summon calls up Summon Count of a prefab around the monster, up to Summon Limit alive at once, hunting the player.
  An attack's **Applies Status** leaves a status effect behind on a hit that gets through.
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

## The living world: how creatures and people behave

Rules are plain C# and tested in `Editor/Tests/LivingWorldTests.cs`; every number and gesture is on a data asset. Not behaviour trees: `MonsterAI` stays the one state machine and these are parts it asks. `AgentScripts/BuildBetterAI.cs` set the first values.

**Monsters** (`MonsterDefinition` › Idle Life, In a Fight):
- **Idle life:** `MonsterIdleLife` picks what a calm creature does between pauses: stand, walk, graze or sleep. `MonsterPose.Rest` shows it. It only runs while idle, and a sleeper's attention is scaled by Idle Sleep Attention through `MonsterSenses.Attention`, so it is easier to sneak past.
- **Surrounding:** Flank Spread makes those on one target take different sides (`AttackTurns.FlankSpot`).
- **Shooting:** a shot needs a clear line (`MonsterAI.CanShoot`). Without one the shooter moves to find a line (`FindALine`). `MonsterAttack.leadTarget` aims ahead of a moving target; 0 aims straight at them, and only a few attacks have it.
- **No way there:** `MonsterMovement.IsCutOff` notices a target it can't walk to. After `PerceptionSettings.CutOffSeconds` a melee creature goes home and ignores targets for Cut Off Forget Seconds; one that can shoot stays. Careful when testing: the prologue lake has NavMesh under the water, so it doesn't cut anyone off.
- **Answering a hit:** whatever is struck hunts back past its usual ranges for `PerceptionSettings.ProvokedSeconds` (`IsProvoked`).
- **Packs:** `MonsterPack` › Breaks With Its Leader, Breaks At Losses. `MonsterPack.Breaks` decides, and `MonsterAI.Rout` sends the rest running for a while.

**Companions** (`CompanionDefinition` › Fighting well, Keeping up; rules in `CompanionTactics`):
- **Formation:** `FormationPoint` turns with the leader's heading, so they stay behind whichever way the player walks.
- **Choosing a target:** `CompanionThreat.TakenBy` and `TakenPenalty` spread them over the enemies.
- **Archers:** they need a clear shot (`CompanionWeapon.HasClearShot`), back away (`ArcherStep`), and their arrows pass through friends (`ArrowProjectile`).
- **Dodging:** on a wind-up aimed at them they may step out of the way (`InTheWay`, `StepOutOfTheWay`).
- **Stealth:** while the player sneaks they keep low (`IsKeepingLow`), and monsters don't sense them unless they fight (`IsHidden`).
- **Catching up:** `HowToCatchUp` makes them run when far behind and warp when very far or stuck.
- **Callouts:** `CompanionBarks` picks spotted, behind-you, hurt and won lines. The lines are `FightLines` on the `CompanionDefinition`, or on the `ResidentDefinition` for someone with a name.

**Residents** (`SettlementSettings` › Daily Life; `JobDefinition.workGesture`):
- `ResidentRoutine.Decide` turns the hour, whether they have a workplace, and danger into `Work`, `Meal`, `Fire`, `Sleep` or `Shelter`. `ResidentLife`, added by `ResidentNpc`, walks them there on the NavMesh and plays the gesture.
- The work spot is outside the walls: the yard, else just off the side (`ResidentNpc.TryGetWorkplace`). They eat and sit in a ring round the Gathering Place, or the portal if there is none, and they sleep at their own spot.
- On arriving in a scene they are already in place, so a whole haven doesn't set off at once on every load. They stop to talk and carry on after. Stuck for 4 s, they go straight there.
- Danger is a ward breach, or a monster hunting the player's side within Danger Radius of that person. They run for the portal.
- To see it: **Safe Haven › Scene › Play Haven (Isolated Saves)** plays the Haven without touching the real save.

## The Haven, lived in: animals, spirits, smoke, sounds and seasons

Set up by `AgentScripts/BuildLivingWorld.cs` (entry `BuildLivingWorld.Apply`). It makes the sound assets (`Audio/Sounds/Living`, synthesised by `Tools/Audio/living.py`), the prefabs (`Prefabs/World/Living`), the season cycle and the scene's `07 Ambience/Living World`. Everything runs near the camera only and stops when no one is there.
- **Wildlife** (`Environment/Wildlife`, `WildlifeHerd`; models are Quaternius CC0 deer, stag and fox in `Art/Wildlife`, see CREDITS.md): a herd spawns its animals when the player comes within 55 m, never in sight closer than 18 m, at its hours (`byHour`), and clears them past 85 m. Animals graze and wander until the player comes within shy distance plus heard noise, or a monster within 14 m. Then they bolt and are gone; the herd stays empty for 300 s. The generator's `PlaceWildlife` puts grazers in meadow and wet glades and at grove centres (dawn and dusk), and a fox in half the other glades (night) (`HavenWorldRecipe.grazers`, `foxes`).
- **Warnings:** settled birds go up when a monster comes within 9 m (`Birds.MonsterNear`). `Aftermath` tramples the grass where a creature died (a `GrassInteractor` for 150 s) and often sends crows to land there afterwards (`Birds.LandAt`).
- **Glimpses** (`GlimpseWatcher` → `Glimpse`): now and then something watches from ahead of the camera: a Hollowed Deer, the Huldra, a small spirit. Further from the portal, rarer ones. It sinks away when approached or after a few seconds. Never while something hunts the player.
- **Hearths** (`HearthSmoke` on the Cottage, Hut, Kitchen, Smithy, Sauna, Trading Post and Furnace prefabs): chimney smoke that leans with the wind, a warm window light after dark, and a washing line on homes (the `livedIn` object). Only while the building stands.
- **Residents:** `ResidentLantern` (a lantern light after dark, out while asleep) and `ResidentAsides` (walk past and they speak: the news once per homecoming, `ResidentNpc.News()`, then a line for the time of day; lines on `SettlementSettings` › Asides) are added in `ResidentNpc.Bind`.
- **Offerings** (`Survival/OfferingStone` on the Waystone): leave the least filling food in the bag for The Forest's Favour (+10% gathering, +5% speed, 8 min); the stone rests 20 min.
- **Morning** (`AtmosphereConditions.MorningAt`): mist rises before dawn and burns off by mid-morning (`TerrainMist.morningAmount`, less in rain). A dawn chorus and owls at night (`AmbientOneShots.byHour`).
- **Sounds from places** (`Audio/PlacedAmbience`): wind from the ridge above you, creaking from the nearest dead trees, howls from the way the creature spawns lie, the Nøkk's song over still water at night.
- **Seasons** (`Seasons` + `SeasonCycle` asset `Data/World/Season Cycle`): each dawn counts a day (`SettlementState.DaysPassed`, saved). The day sets `_SafeHavenSeason` (autumn, leaf fall, frost). Tree Wind turns, then thins, leaves on materials marked **Deciduous** (maple, broadleaf, birch, forest shrubs; never cedar) and frosts all leaves. `Grass.shader` yellows and frosts. The year: summer 10 days, autumn 6, late autumn 4, winter 6, spring 6, each turning into the next over 2 days. A season may bring its own `ClimateDefinition` (`WeatherSystem.Active.SetClimate`). Raids keep the summer look (`Seasons` only runs in the Haven).
- **Frost and snow on the ground** (`WorldFrost`, `WorldSnow` in the terrain passes, written by `AgentScripts/GenerateWorldTerrainShader.py`: change the generator and run it, never the generated files): hoarfrost is `_SafeHavenSeason.w` (`Seasons.GroundFrost`: the season's frost by the hour, thickest with the morning mist, lingering in shade). A season marked **Snows** (winter) has `WeatherSystem.SetSnowfall` turn rain into snowflakes (`RainEffect`'s Snowflakes, from `WeatherSetup`; no rain sound, splashes or rings) and keeps its **Lying Snow** on the ground. `WeatherSystem.AdvanceSnow` builds it up while snow falls and lets it linger, and the snow (`_SafeHavenSnow`) whitens level ground (footprints show through), sinks and whitens the grass, and settles on whatever faces up in Tree Wind. Snow doesn't soak the ground, so there are no puddles; out of winter it thaws and wets the ground a little.
- Not done: worn paths where people walk. Painting the terrain at runtime would write into the TerrainData asset in the Editor.

### Winter, and the year in play

Set up by `AgentScripts/BuildWinter.cs` (sounds from `Tools/Audio/winter.py`) and `AgentScripts/BuildSeasonalLife.cs` (music from `Tools/Audio/festival.py`, geese from `Tools/Audio/seasons.py`). Both are re-runnable.
- **Snow on things** (`Shaders/SeasonSnow.hlsl`): whatever faces up whitens, in clumps. Materials opt in with **Snow Settles On It** (keyword `_SEASON_SNOW`) on Mesh Surface, Layered Lit, Mossy Stone and the See-Through Lit/Unlit copies, so floors stay clear (BuildWinter turns it on by material name). URP's own lit pass gets it by renaming its `UniversalFragmentPBR` call (`SEASON_SNOW_WRAP_PBR`). Thatch and shingle roofs moved to See-Through Lit to take it.
- **Ice** (`WinterIce`, `IceSheet`): still water freezes once snow lies in a snowing season. It forms over a minute and waits until nobody is swimming in it. It gets a walkable collider at the surface, the `_IceAmount` look in `Water.shader` (cracks, drifted snow), and `WaterSurface.IsFrozen` (no wading, swimming, fishing, drinking or water sound). Rivers keep running. Wells (`WaterSource`) freeze each night: the first use of the day breaks the ice.
- **Steps and tracks:** steps crunch in snow and knock on ice (`AudioSettings` › footstepSnow, footstepIce). Prints last up to five times as long in lying snow, and falling snow fills them in (`TerrainFootprints.SnowLifetime`). Animals and creatures leave prints too (`WalkerFootprints.AddTo(go, step, spacing)`). `WinterTracks` lays a trail of prints from each lair and herd towards home, near the player only.
- **Cold** (`PlayerWarmth`, `ColdBreath`): breath shows in the cold. Out in it the player chills over about four minutes (slower with Frost Resistance gear, faster at night) and becomes **Chilled** (-8% speed, -15% stamina). Any fire in the world (`LightFlicker`, not one carried) or a burning hearth (`HearthSmoke.Burning`) warms them. In hard cold residents leave afternoon work for the fire (`ResidentRoutine.InTheCold`).
- **Seasonal gathering:** `GatheringNodeDefinition` › In Seasons (empty means all year). At home, random resource spots pick only what is in season (`Seasons.Today`). If nothing of a kind is in season, a spot keeps its usual pick, so it is never empty.
- **Festivals** (`Festivals`, `FestivalDefinition` in `Data/World/Festivals`): the Harvest Feast (day 15, evening) and the Midwinter Bonfire (day 23). A bonfire is lit (`Prefabs/World/Living/Festival Bonfire`) with its music, residents gather round it and say its lines, and the player who joins gets its boon once.
- **The year's creatures:** fireflies by season (`Season.fireflies`). Geese fly over in a V in autumn and spring (`MigratingGeese`, drawn with the birds' sheet). Winter coats: the fox goes white, deer greyer (`Wildlife` › Winter Coat).
- **Blight** (`SettlementState.Blight`, saved): a corrupted night whose creatures get away spreads it, beating them back eases it, and each day eases it a little. As it spreads, hollowed deer come more often and nearer home (`GlimpseWatcher.Entry.moreWithBlight`), and herds shrink and may hold a hollowed one (`WildlifeHerd` › Hollowed).
- **Residents go out** (`ResidentActivity.Outing`): about one day in three each, for an hour or so between ten and two, to a waystone or the lakeshore away from lairs (`ResidentRoutine.Outing`, `ResidentLife.GoingOut`).
- **Spirits that remember** (`SpiritBonds`, saved on `SettlementState`): stand at still open water at night and the Nøkk plays to you. Stay for its whole air (about 28 s) and fish bite half again as fast, and the Nøkken leaves you be (`PlayerFishing`). Let the Huldra go in her own time twice and she leaves Huldra Silk where she stood. Chase her and she stays away three days.
- **Raids follow the year:** `Raid_MistyWoods` has the same Seasons (same cycle and start day, so the same date), WinterIce, WinterTracks and SpringMelt. A winter raid is snowy and its marsh pools freeze into ways across. In spring, running water stands `Season.riverRise` higher (0.45 m), so fords are swum (`SpringMelt`).

## Going along, set-pieces and bosses

**Raid companions (GDD §8.4, §15.5).** `ResidentDefinition` › Raid Companion says how someone fights; the raid planner offers everyone who has one and lives in the Haven, one per raid (`RaidLauncher.CanGoAlong`, `RaidSession.Companion`). `RaidDirector.BringCompanion` spawns them beside the player from `SettlementSettings` › Raid Companion Prefab with their own model (`Companion.SpawnAs`), scaled by the raid's multipliers the other way round: enemy power makes them tougher and enemy health makes them hit harder, so they stay as much help at any tier. On a raid they don't get up by themselves: `CompanionDefinition` › Bleed Out Seconds (45) to help them up, or they are gone. Down when the raid ends counts the same, because nobody went back. Alive but elsewhere, they find their own way home. `SettlementState.LoseResident` takes them off the list: raids don't seek them as a new face (`HasResident`). A `FallenCairn` goes up near the portal for as long as they are out there, `ResidentNpc` speaks of it for `GriefRaids` raids, and their job goes on `VacantJobs`, open to anyone (`JobsOpenTo`), so a loss never blocks crafting (GDD §7). A crash brings them back, like everything else in a raid that never came home.

**The lost are found again (GDD §8.4).** Nobody dies for good. `RaidRumourDefinition` › Seeks Lost (`Data/Raids/Rumours/TheLost`, urgent, in every raid's pool) comes up while `SettlementState.LostToFind` has someone, longest lost first, and its event stands them at its site: `StandTheLostOutcome` → `RaidDirector.StandLostAt` spawns them as a `Companion`, `StandLost` (waiting, untouchable, not on the minimap). The choice (`LeadTheLostOutcome`) sets them following. `RaidDirector.SettleTheLost` brings them home only if the player extracts with them alive, up and within `Found Comes Home Within`, because they don't know the way; otherwise nothing changes and the rumour comes up again. `FindAgain` makes a new `ResidentRecord`: no bond, no job, `IsStranger` for their first words (`Stranger Lines`). What the forest took is kept, not lost: `ResidentRecord.Remembers` collects what happens, `LoseResident` moves it to `FallenRecord.Forgotten` (`CompanionBond.WhatIsForgotten`, biggest last), and one comes back every `Memory Returns Every` raids together, said once in `ResidentNpc.Remembered` (`ResidentDefinition` › Returning Lines, else `SettlementSettings`), the last with `All Back Line`. The words and the rumour: `AgentScripts/BuildCompanionAmnesia.cs`.

**Bonds.** Raids together make a rank (`CompanionBond`, `SettlementSettings` › Bond Ranks: Trusted at 3, Sworn at 8) that adds to their health and damage on a raid. Each raid leaves one memory, the biggest thing that happened (`Remember`), which they bring up once when next spoken to: their own line from `ResidentDefinition` › Memory Lines, else the general one. `ResidentRecord` keeps Raids Along, Kills Along and Times Helped Up, saved, and a cairn says how long they were at the player's side.

**What the boss changes.** `BossDefinition` also holds its waking (`Wake Seconds`, sound, a title card from `BossBar`, untouchable and harmless meanwhile), its `Music`, played through `SceneAudio.PlayEventMusic` until it falls, its `Trophy` and `Home Lines`. `RaidDefinition` › Unlocked By opens a locked raid once that boss is beaten. Troll Valley is `AgentScripts/BuildBossTurningPoint.cs`: the Misty Woods scene with its own Layout Rules (steeper, thinner forest), creatures, tiers from II and rumours. It has no art of its own yet.

**Large raids.** Raid Definition › Large Raid: width (2), extra seconds (750) and Extra Exits, by name: `RaidMapGenerator.AddExits` copies the raid's own (`ExtractionPoint.CopyAs`: its hours and cost, no alarm, never a quest's) and `SpreadOver` keeps every exit as far apart as the sites allow, with West and East still at the ends. The planner offers Normal or Large on any raid with generated maps; `RaidSession.IsLarge` carries it. `RaidMap` grows a runtime copy of the terrain at the same detail per metre (`GrowTerrain`), moves the walls (`FitBoundary`), and works from `RaidLayoutRules.ScaledBy` and `RaidMapDefinition.ScaledBy`, copies that never touch the assets: four times the clearings and sites, rumour sites and rumours only doubled. `SpawnRules.AreaScale` multiplies spawn lists, except Escalations, which come where the player is. Stages and exits count seconds left, so more time is only more calm first. Anything that reads the terrain's size before `RaidMap.Setup` has to be told: `ExplorationMap.Refit`. **One long `NavMesh.CalculatePath` on a large map runs out of nodes and reports a partial path even where the way is open**, so ask in hops: `RaidMapGenerator.RoomsReached` walks the plan's trails. Measured in the Editor: 4.7 s to load a normal raid at 90 fps, 10.7 s for a large one at 87 to 90 fps, with 533 resource nodes and 93 creatures, 31 awake.

**Rumours point roughly.** The HUD gives a rumour's place and a rough way (`RumourWay`: "at the Old Barrow, far off, south-east"), and its beacon stays out until 40 m off (`RaidEventSite.HideBeaconBeyond`). Exits and random events still say metres. **Foretelling:** `BuildingLevel` › Foretold Events (Watch Post upgrades) names that many of a raid's events in the briefing (`RaidForecast`, from the seed), and `RaidEventDirector` draws those first so it comes true.

**Guards.** `JobDefinition` › Defends As: through a ward breach that resident is on guard (`ResidentDefender`, held by `ResidentLife`): they step out and a Companion with their model holds a post, the Watch Post if that is their workplace, else a ring round the portal. A guard gets up by itself, so nobody dies of a breach. The rest shelter. Warden and Hunter do this now.

**Set-pieces (GDD §9.2).** `RaidSetPieceDefinition` is an authored place: a prefab built round a clear middle, the room it needs, the moods it suits, its guards by tier. `RaidLayoutPlanner.PlaceSetPieces` gives one to every rumour site and to a few camps, from its own random stream so it never moves a site, and adds a pocket of level ground. `RaidMapGenerator.PlacePieces` stands them, each direct child on the ground by itself. Guards come out in `RaidDirector.BeginRaid` (`RaidSetPieceSite.BringGuards`), strengthened like any spawn. A rumour can name the place it wants (`RaidRumourDefinition` › Set Piece, `RaidRumourDirector.TakeSpot`); if the map lacks it, it takes what is there. A new place is an asset and a prefab in Layout Rules › Set Pieces: `AgentScripts/BuildSetPieces.cs` makes the first six from the forest kit.

**Bosses (GDD §16.5).** `BossDefinition` holds the name, phases and reward; `BossEncounter` on the prefab moves the fight on as health falls, opens attacks by `MonsterAttack` › From Phase, and shows `BossBar`. Each phase can make it reel, and hit harder. The reward is learned at once and kept even if the player falls before getting home (`SettlementState.MarkBossDefeated`, given once); drops are ordinary drops and have to be carried out. The Heartroot (`AgentScripts/BuildFirstBoss.cs`) is reached through the rumour system: `TheHeartroot` is urgent from the third raid and stops once it is beaten, the HUD points to it, and the player chooses to wake it at a stump in Guardian's Hollow. That set-piece is Always There, on the rumour site farthest from the apex, so the great troll's round doesn't cross the fight. A phase gate has two sides: `SelectAttack` for what it may use and `HoldsGround` for where it stops, or it waits at the range of an attack it hasn't learned.

**Finishing blow.** A press close behind a staggered creature that Can Be Finished (person-shaped: Draug, Barrow Shieldbearer) locks the two bodies for the stab (`PlayerCombat.BeginFinish`, `StrikeRules.CanFinish`, `CombatTechniqueSettings` › Finishing Blow). The numbers and timing are the weapon's own rush, and only the look is `Finish Stab`, a clip whose reach peaks at the 0.28 s where every rush lands, so it can't change the balance. `MonsterAI.HoldForFinish` keeps it reeling and stands it up to take the blow.

**A heavy bag (GDD §12.4).** `PlayerBurden`, on the player, reads the bag's weight: past Player Movement Settings › Carrying › Heavy From Share (80%) speed, stamina costs and footstep noise ease toward their Full Bag values (rule: `CarryLoad`). 100% is still a hard cap. A backpack can have a cost of its own: `ItemDefinition` › Carry Noise, Carry Speed Scale, Carry Sound. The Haul Sack carries 90 to the Hide Rucksack's 40, with half its slots, 8% slower, and every step 5 m louder.

**Work.** `JobDefinition` › Work Tool, Work Tool Scale, Work Sound, Work Sound Delay: `ResidentLife` shows the tool only while working. A repeated gesture sounds at its moment of contact (read off the clip by `AgentScripts/BuildHavenWork.cs`), and a held one every so often.

## Loot: rarity, carry space and the safe pocket

- **Rarity and value** live on `ItemDefinition` (Loot header). `Rarity` (in `Items/ItemRarity.cs`) turns a rarity into a
  color, name and pickup sound from `RaritySettingsDefinition`. Slots tint by rarity, tooltips and the message feed
  color names, `AudioManager` plays the pickup sound from the `ItemObtained` event, and `RaidReport` sums value and
  lists the rarest finds first.
- **Who collects a kill**: `LootOnDeath` gives a creature's drop to whoever landed the last blow, if they carry a bag.
  A blow from the player's side that carries none (a companion, a spike trap, a watch post's archer) pays the player
  (`LootOnDeath.TakerOf`, used by `EliteCreature` too): a companion finishing a creature must not cost the drop. Nobody's
  kill (a fall, a creature's) drops nothing.
- **Carry space**: `ItemContainer` has locked slots (past `UnlockedSlots`): they keep items but take no new ones.
  `ItemInventory` unlocks `baseUnlockedSlots`; `BackpackCapacity` adds the best carried backpack's `bagSlotBonus`.
  Losing the backpack in a raid shrinks the bag again.
- **Dropping**: Ctrl+right-click in the bag calls `ItemInventory.Drop`, and the stack lies on the ground as a `DroppedItem`
  until E picks it up, keeping its wear and essence work. One prefab serves every item (`Resources/DroppedItemSettings`, built
  by `AgentScripts/BuildDroppedItem.cs`): it copies the item's meshes, or shows a sack, as most items have no model. Saved at
  home; raid scenes aren't persistent, so a drop stays behind with the raid. Picking up goes through
  `ItemContainer.TransferTo`, not `Give`: raising `ItemObtained` again would let a drop fill a collect quest.
- **Safe pocket**: `ItemInventory.SafePocket`, a separate small container. `PlayerSnapshot` carries it, and
  `WithoutBelongings` (a failed raid) keeps it. It is saved as `PlayerSaveData.safePocket`.

## Equipment and the character screen (C)

- **Gear model**: `Items/GearSlot.cs` (the slot enum, plus `GearSlots` for count, labels and `Describe`) and `Items/GearSet.cs`
  (an `ItemContainer` with a `SlotRule`, so each slot only takes items whose `ItemDefinition` gear slot matches; there is
  one ring). Slots: 7 armor slots (Head, Body, Hands, Feet, Belt, Amulet, Ring; Shoulders, Back, Wrists, Legs and the second ring are retired: `GearSlots.IsRetired`, numbers kept for saves, gear found there moves to the bag on load), MainHand, Off Hand, Backpack, and a tool belt of Axe, Pickaxe and Sickle.
- **Held items**: `HeldItemView` (on PlayerArmature) shows the main-hand weapon in the right hand (a bow in the left), or,
  while gathering, the belt tool for the job. Models come from `ItemDefinition` › Held In Hand (held model, else the world prefab; position,
  rotation, scale in the grip). Held models are visual only (colliders, rigidbodies and scripts stripped). The grip is a fist
  fitted to the rig's own fingers; tune items in **Safe Haven › Content › Held Item Grips** (live in Play mode). Stand-in
  models come from the Kevin Iglesias demo props until LocalSpawn makes real ones.
- **`PlayerEquipment`** owns the `GearSet`. It applies each worn item's `StatBonus` list to `CombatStats` and removes it
  again when the item comes off or breaks. It exposes the weapon and `ToolTier`; tools only count while on the tool
  belt. Gear arriving through Give or crafting auto-equips into an empty matching slot.
- **Shields**: a weapon of `WeaponKind.Shield` goes in the Off Hand, never the Main Hand. `PlayerEquipment` hands it to
  `PlayerCombat.Shield`; its Guard Let Through decides blocks, and attacking while guarding bashes. L stows it for the torch.
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

## Characters: modular humans, character creation and emotes

People are built from the N-hance Stylized Modular Human packs (`Assets/StylizedCharacter`; runtime code never references
the pack's scripts, only `SafeHaven.Editor` does).

- **Data**: `CharacterBodyDefinition` (one per gender, `Data/Characters`: model, avatar, skins, faces, eyes, underclothes,
  hair/brows/beards in each hair colour, the female clip swaps), `GearLookDefinition` (worn meshes and skin overlays per
  gender, and the body parts they hide), `EmoteDefinition` (clip, optional begin/end, held or one-shot, on the wheel) and
  `CharacterCatalog` (`Resources/CharacterCatalog`: both bodies, the starting look, the name dice lists, the emotes and
  the speaking / quest-taken / sit-at-fire gestures, the plain clothes and shoes). `CharacterAppearance` is the plain,
  saved choice of indices plus the name (`GameSession.Appearance`, `SaveData`).
- **`ModularCharacter`** builds a body from an appearance and an outfit (a list of gear looks), rebinding meshes to one
  skeleton and swapping in the female clips; it only rebuilds what changed. `PlayerLook` dresses the player from
  `GameSession.Appearance` and the worn gear's `ItemDefinition` › Worn Look; with nothing in Body or Feet the chosen plain
  clothes / plain shoes are worn. NPCs keep the outfit authored on their prefab.
- **Character creation**: New Game opens `UI/Options/CharacterCreationScreen` (name with a random-name dice, body, skin,
  face, hair, hair colour, brows, beard, eyes, clothes; Randomise) over a `CharacterPreviewStage` (a RenderTexture studio
  far below the scene, drag to turn). Confirm calls `SaveGameService.TryStartNewGame(scene, look)`. The name is used in
  dialogue (`DialogueSpeaker.Player`) and save names.
- **Emotes**: `CharacterEmotes` plays them on the controllers' **Emote** layer (weight only while one plays; held poses
  end when the character moves). The command wheel (Alt) has an Emotes slot: hover it to open the emote page.
  `CharacterGestures` adds the incidental ones: talking gestures on each dialogue line, a gesture from giver and player
  when a quest is taken, and sitting down when cooking at a campfire.
- **Builders** (Safe Haven › Characters): `CharacterLibraryBuilder` (bodies, emotes, catalog, armour and plain-clothes
  looks), `CharacterAnimatorBuilder` (player and Villager controllers, clip swaps, Emote layers), `PlayerBodySetup`
  (player prefab, grips via `HeldItemGrips`), `PeopleSetup` (people prefabs, residents, the traveler and the prologue
  expedition, including their expedition torches).

## UI root, pause and the Escape menu

- **One UI root per scene.** Gameplay scenes hold one `UI Root` prefab instance (`Prefabs/UI/UIRoot.prefab`). It has
  the `UIStateManager`, the `EventSystem`, and `Screen Canvas`: the only root screen canvas and the only `CanvasScaler`.
  Every HUD and window prefab is a nested canvas under `Screen Canvas`. It is stretched to fill, has `overrideSorting`
  on and keeps its own sort order, so a change in one window only rebuilds that canvas. New screen UI goes under
  `Screen Canvas` and needs no `CanvasScaler`. World-space canvases (health bars, prompts) and the runtime loading
  screen stay separate. MainMenu and RaidResults are single-canvas scenes with a `UIRoot` component on their canvas.
- **UI scale:** `UI/UIRoot.cs` sits on the root screen canvas. `UIRoot.Current.Scale` (0.5–2) divides the scaler's
  reference resolution, so one value scales all screen UI. The player's UI and Text Size setting multiplies it.
  Scalers use Expand, so the whole 1920x1080 layout stays on screen at any aspect (on ultra-wide the height sets the
  size). Screen canvases made in code (HUDs, dialogue box, loading screen) call `UIRoot.ConfigureScaler` instead of
  setting up their own `CanvasScaler`.
- **Pause:** `Core/GamePause.cs` owns pausing. Systems call `Request(owner)` / `Release(owner)`, and the game stays
  paused while any request is held. In single-player a pause freezes time (`Time.timeScale = 0`), which also stops the
  raid timer. For multiplayer, set `GamePause.FreezesTime = false`: the menu still opens and input is still blocked,
  but the world keeps running. `PlayerInputHandler` ignores gameplay input while paused. Hit-stop leaves
  `Time.timeScale` alone while time is frozen, and while it is shared (`GamePause.TimeIsShared`: hosting friends).
- **Escape:** `UI/PauseMenu.cs` (inside the UI Root, `UIWindowId.PauseMenu`) is the only Escape listener. It first
  offers the press to `Core/BackHandlers.cs` in priority order: build placement (300), then open windows (200,
  `UIStateManager.CloseAll`), then a timed interaction (100). The menu opens only if none of them used the press. To
  make something cancellable with Escape, register a handler in OnEnable and unregister it in OnDisable. Gamepad back
  (east button) still cancels in `PlayerInputHandler`.
- **Menu:** Continue, Save and Saved Games (to the save being played, and opening `SavesScreen`; both disabled with the reason during a raid),
  Options (opens `OptionsScreen`, see "Options") and Quit (to the main menu or the desktop). Audio is not paused. Tests: `Editor/Tests/PauseTests.cs`.

- **Camera styles:** V toggles between top-down and the action camera at any time. In action style the cursor is
  locked (mouse looks around, wheel zooms, `UI/ActionCrosshair.cs` shows the aim point) and released whenever a window
  or the pause menu is open. If an override forces the current situation, V still changes the choice and a message says
  which situation overrides it. The top-down cursor lean is measured from the ground under the screen centre, not the
  player, so the view holds still while the cursor does; aiming a bow leans 1.5x further and blocks right-mouse orbit.

## Transport

Carts, barrows and boats (GDD §8.5). They are real rigidbodies; the rules read the same weight the physics feels.

- **Definitions:** `TransportDefinition` (capacity, overload limit, the three points of the speed curve, mass, pull force), `TransportSettings` (extraction range, noise, strain). Built by `AgentScripts/BuildTransport.cs`, with the kits, recipes and the Wainwright's Yard (`CraftingStationType.Wainwright`).
- **`Transport`** is the object in the world: a Rigidbody with an `ItemContainer` of cargo whose weight limit is the overload limit. Weight sets `Body.mass`. Left alone it rests and goes kinematic. It is an `ISaveable` and spawned through `SaveableEntity`, so position and cargo persist, and it catches its own fall (`CatchFall`) so physics can't lose someone's haul.
- **`Haulable`:** the player is a CharacterController, which no joint can hold, so the hands are a spring at the handle with the force capped at `PullForce`. Wheels are frictionless spheres and the script supplies side grip and rolling drag. Pulled and pushed are different on purpose: pushing is unstable twice over (a gripping wheel ahead is a reversing trailer, and a push at the handle is behind the centre of mass), so a barrow in the hands has no side grip, takes the shove at its centre of mass and is steered by torque.
- **`Rowboat`:** float points pushed up by how deep they are, so draught follows the load. Steering reads `PlayerMovement.MovementInput`. The player is parked with `PlayerMovement.SitAt`, which turns the CharacterController off.
- **`PlayerTransport`** owns what the player is using, the speed scale and everything that lets go. `PlayerStateMachine.IsAboard` gates fighting like swimming does. `PlayerTransportPose` bends bones after the Animator, as there are no animations for this.
- **Between scenes:** `GameSession.CarriedTransport` holds a definition and its saved cargo. `PlayerTransport.Start` waits until the player stands on the ground before setting it down: set down earlier it falls through a scene still waking.
- **`TransportAudio`** reads everything from the physics: the loop follows real speed and load. The clips are made by `Tools/Audio/transport.py`, and the sounds are set in `TransportSettings`.
- **`CargoPile`** shows the load on the cart itself: pieces grow in as weight rises and heap over the rim when overfilled.
- **`CartScavenging`** is the risk of letting go in a raid: creatures nearby catch the scent (`MonsterAI.Investigate`, as a smell isn't a noise) and take cargo a bite at a time. `RaidRisk.WithCargo` and `RaidReport` put what is at stake in the briefing and the results.
- **Rivers:** `WaterSurface.CurrentAt` gives the flow at a point, and `Rowboat` takes its drag relative to that water, so a boat left alone drifts downstream.
- **Hauling is work:** rowing, climbing and pulling an overfilled load cost stamina, and costs paid through `MaterialPool` can draw on the cargo of a transport standing near the player.
- **Colliders are on Ignore Raycast** so a wheel's ground ray doesn't find its own cart; `Transport.GroundMask` also skips water.

## Ward breaches

Attacks on the home (GDD §6.6–6.9): warning → preparation → defense → recovery. Bounded on purpose: nothing the player built is ever lost, and a lost defense mends by itself.

- **Pacing:** `WardBreachSettings` lists the breaches in order; the last comes back stronger once they run out. `SettlementState` holds `BreachesWon`, `NextBreachAt` and `BreachPendingSince`. A breach only comes due arriving home after a successful raid. It waits for the player at the portal, but after `RaidsPutOff` more raids the ward gives way at the next nightfall.
- **The defense:** `WardBreachDirector`. The first breach keeps its gentle rules (`ThreatensHome` off): one marked path, enemies hunt only the player, buildings shielded, falling ends it. Later ones come in waves down `BreachLanes`, found on the NavMesh beyond the ward from a seed and drawn with copies of the scene's path marker. Each attacker is re-aimed once a second: every other one at the ward while a road to it is open, the rest at the nearest building; anyone close is fought first, and a chase over `ChaseLimit` is given up. Breach enemies carry no loot, as the breach has its own reward.
- **The ward** is a `Ward Anchor` prefab raised at the tree only while a breach runs. If its `Damageable` falls the breach is lost: the attackers leave, `WardStrain` draws the ward in for a few raids, and the same breach waits to be tried again. The player falling doesn't end a real breach, as `HomeRevival` wakes them at the tree.
- **Buildings break, they aren't lost:** `BuildingObject` sets `Damageable.HealthFloor`, so `destroyOnDeath` on a building can't fire. At the floor it is `IsBroken`: `Construction` darkens it and opens the NavMesh under a wall, and `CraftingStation.Usable` turns false. `BuildingRepairs` mends everything at once from the portal or the recovery page, for `RepairShare` of what the damage cost to build.

## Night

Anywhere the sky is dark, `Resources/NightSettings` applies, scaled by `AtmosphereConditions.Night`, so dusk counts for a little.

- **Danger:** `RaidDirector` multiplies its enemy health and power by the hour; `NightProwlers` calls `NightSettings.Strengthen` on what it spawns and lets more prowl with every breach won (`ProwlersAlive`, `DeepProwler`). They still never step into light. How many creatures a raid holds is the hour's own: `RaidTimeOfDay.enemyDensity`, used by `RaidDefinition.GetSpawnRules`.
- **Reward:** `LootOnDeath.Reaped` multiplies Cursed Essence by the night's share and the killer's Essence Find. Breach attackers carry no loot, so this can't turn a defense into a farm.

## Cinematics: the backstory that opens a new game

A new game starts with "The Old Story" ([script](../../../Docs/BackstoryCinematic.md)): twelve painted shots, the portal's
narration with subtitles, sounds and the game's own music, about two minutes, skippable by holding Esc, Space or Enter.
`MainMenuView` › **Opening Cinematic** plays it after character creation and starts the game when it ends or is skipped;
empty goes straight to the first scene. **Load During Cinematic** loads that scene in the background meanwhile
(`SceneTransition.Preload`), so the wait afterwards is a few seconds.

- **A cinematic is data.** `CinematicDefinition` (`Data/Cinematics/Backstory.asset`) holds shots; a shot holds layers back to
  front, a camera move, the lines spoken and the sounds heard. Nothing changes the asset at runtime.
- **Depth is arithmetic, not a 3D scene.** Every layer is a UI image in a 16:9 frame. `CinematicCamera.Place` gives a layer at
  depth d (0 = the subject, 4 = a far forest, -0.45 = a fern in front) its scale and slide for a camera pose, so near
  layers move more than far ones. It draws over any scene, at any aspect (`ArtCover`: a wider screen crops at most a
  fifth of the height, then black at the sides), with no camera, render feature or layer of its own.
- **The voice sets the time.** A shot lasts its pauses, its lines' clips and the hold after them (`CinematicShot.Seconds`), and
  `CinematicTimeline` adds the shots up: a line spoken again never breaks the timing. A layer's **Opacity** curve runs over
  the shot (left edge its start), which is how the ember dies, the seasons change and the lanterns go out.
- **Moving details are layers too:** a round additive glow with **Flicker** (firelight), a sliding **Drift** (mist), and
  **Motes** (`CinematicMotesGraphic`: embers, snow, pollen, fireflies, placed from the speck's number and the time, so a
  shot looks the same every time). Additive layers use `Resources/Cinematics/Cinematic Additive` (`SafeHaven/UI/Additive`).
  That shader multiplies the light by its strength itself and blends One One, and reads the texture's alpha as
  perceived brightness (raised to 2.2): with the strength left to the blend (SrcAlpha One) a glow on black came out in
  rings with a hard rim, as its faint end is smaller than 8 bits can hold.
- **While it plays** the game is paused (`GamePause`), the scene's music and ambience are held back
  (`AudioManager.HoldMusic`/`ReleaseMusic`) and its own sources ignore the pause. Narration follows the new **Voice**
  volume (`AudioChannel.Voice`, Options › Audio); music dips under each line (`Music While Speaking`).
- **A stalled frame counts for at most 0.1 s** (`CinematicScreen`): the background scene load can hold a frame for seconds,
  and jumping ahead by that much skipped the first line.
- **Dissolves fade the new shot's layers in one by one**, not as one picture, so a near layer shows a little of its own
  background through it halfway. Not noticeable at these lengths; a render texture per shot would cure it.

**Changing it.** The asset is written by `AgentScripts/BuildBackstoryCinematic.cs`: shots, camera moves, pauses, glows and
music are there, and re-running it rewrites the asset, so change it there and not in the Inspector. The parts it reads:

| Part | Made by | Kept in |
|---|---|---|
| Paintings | `Tools/LocalSpawn/generate_backstory.py paintings` (LocalSpawn's Safe Haven styles; prompts in the script) | `Tools/LocalSpawn/Backstory/` |
| Layers | `... generate_backstory.py layers` (`backstory_layers.py`: a depth estimate splits the painting, what a nearer layer hid is filled with its surroundings mirrored) | `Art/Cinematics/Backstory/` |
| Glow, mist, vignette | `Tools/Art/cinematic_overlays.py` | `Art/Cinematics/Shared/` |
| Narration | LocalSpawn's Voice tab or `Tools/Audio/backstory_voice.py` (Qwen3-TTS, voice `portal_f_british_northern`, kept in `Tools/Audio/Voices/`), then `Tools/Audio/narration.py` picks the take of each line that says its words and sets the level | `Audio/Voice/Backstory/` |

Traps: the image model keeps a fire it is redrawing however cold the words are (paint the cold hearth afresh, without the
word fire); it writes letters on any signpost; and it adds a wanderer to an empty place unless told nobody is there. The
voice model has no speed control and ignores a slower reference clip. The builder saves only its own asset: saving all
assets also wrote terrain data the World Builder had changed in memory. `CinematicTests` checks the arithmetic, that
every line has a voice, that no painting's edge ever comes into frame, and that the script keeps the first act's secrets.

### A second cinematic: "The Catch", as the prologue ends

When the countdown at the waystones runs out (or the player falls in that last defence), `PrologueDirector.EndSequence`
plays `PrologueDefinition` › **Ending Cinematic** before it asks for home, which loads behind it (**Load Home During
Cinematic**). Nine shots ([script](../../../Docs/PrologueEndCinematic.md)): the hand on the stone, the old troll's blow,
the stones breaking, everyone thrown, the portal catching one, and what there is to do. Empty goes straight home.

It is made like the opening, from `AgentScripts/BuildPrologueEndCinematic.cs` (asset `Data/Cinematics/TheCatch.asset`),
`Tools/LocalSpawn/generate_prologue_end.py` (which only names its paintings and reuses `generate_backstory.py`) and
`Tools/Audio/prologue_end_voice.py` (the portal's voice through LocalSpawn's acting model, one clip a sentence).

**Writing another one.** The parts a shot is made of are in `Editor/Cinematics/CinematicBuilder`: `Use(artFolder,
voicePrefix)` names the cinematic's files and sets their import settings, then `Shot`, `Painting`, `Glow`, `Specks`,
`Mist`, `Snow`, `Vignette`, `Bed`, `Keys` and `Save`. A builder script is then only what is in each shot. Where a glow
sits (u, v from the painting's bottom left) is read off the painting; both scripts keep those numbers together.

## Spoken dialogue: a recording for a line, found by who says it and what they say

Characters speak their lines aloud. Nothing in the dialogue data points at a recording: whoever shows a line asks the
`VoiceBank` (`Resources/VoiceBank.asset`) for one by **speaker and words**, so every system that puts a line on screen
is voiced the same way, and a builder that rewrites a story keeps its voices as long as the words stay the same.

- **Where it hooks in.** `DialogueBox.Speak` (every `Say`, `Ask` and `Aside`: story graphs, quest givers, companions'
  barks, residents) and `HomePortal.Speak` (the portal's menu and whispers) call `VoicePlayer.Say(speaker, line)`. With
  a recording it plays on the **Voice** volume, one voice at a time, heard while the game is held still. Without one
  the speaker's own sound plays as before, and `DialogueBox.Unvoiced` is raised.
- **Finding a line.** `VoiceBank.Key` is the speaker's name and `VoiceBank.Plain(text)`: no rich-text tags, no key hints
  (`(G)`, `[E]`: the player can rebind them and it is still the same line), no case, single spaces. `VoiceBank.Id` is a
  short hash of that, and the recording's file name: `Audio/Voice/Dialogue/<speaker>/<id>.wav`.
- **A subtitle waits for its recording**: an aside stays up until the clip ends plus `VoiceBank.SubtitleTail`.
- **Hand Placed** on the bank holds recordings put on a line by hand (an actor's take). They win over generated ones
  and are kept when the list is rebuilt.
- **Lines the harvest can't see** (text put together while playing) are written to
  `Tools/Audio/Voices/dialogue_wanted.tsv` by `VoiceLinesWanted` when the game says them unvoiced, and picked up by the
  next harvest.

**Making the recordings** is three steps, all behind **Safe Haven › Voice Lines**:

| Step | Does | Code |
|---|---|---|
| Find lines | Reads every line a named character says from residents, the prologue, prefabs and the two story scenes (opened beside the open one, never saved) into `Tools/Audio/Voices/dialogue_lines.json` | `DialogueVoices.Harvest` |
| Speak | Gives each line a voice (the cast at the top of the script) and a mood, and has LocalSpawn speak the lines without a clip | `Tools/Audio/dialogue_voices.py new` (or `again <id>` for chosen lines) |
| Add to the game | Imports the clips (mono, Vorbis, compressed in memory) and writes the bank | `DialogueVoices.Apply` |

How a line is said, first that applies: what the Voice Lines window set for it (`dialogue_direction.json`), the
`DIRECTION` table in the script (written by reading the scenes: a mood, sometimes with its own mix of feelings), the
kind of line (a bark in a fight is shouted, the portal's whispers are calm), then plainly.

**The voices** are designed in LocalSpawn (Qwen3-TTS's voice designer) from the descriptions in the cast at the top of
`Tools/Audio/dialogue_voices.py`, and kept in `Tools/Audio/Voices/<name>/`. Each reference clip is long (15 to 25 s,
written in the character's manner with a question, an exclamation and a quiet close): every line is cloned from it,
and the first one- or two-sentence clips gave flatter lines. `Tools/Audio/redesign_voices.py` designs a voice again
with several seeds and keeps the one most like the voice it replaces (a speaker-recognition model), of the right sex
and saying all its words.

**Who speaks the lines** (the user's choice by ear, 2026-11-10): the characters by LocalSpawn's acting model
(IndexTTS-2.5), which plays the feeling as it speaks; the portal by Qwen3-TTS, plainly (better for her longer, quieter
reads; her sentences are put on lines of their own, which gives a pause between them). **Nobody is slowed**: slowed
lines sounded "drunk and slow in the head". And Qwen's moods made afterwards on the sound are not used: they sounded
bad. IndexTTS's licence is bilibili's own: read `D:\ComfyUI\agent\integrations\index-tts\README-WINDOWS.md` before
shipping these recordings.

Traps: a script run from the command line once wrote its clips beside the wrong folder (relative paths are now always
beside the script file); the clip in a voice's own folder is the *designer's* sample, not a line spoken by the acting
model; and LocalSpawn's raw recordings (`raw/` beside the clips) are not the game's and are not committed.
`VoiceBankTests` checks how lines are found and that every line the bank lists has its recording.

## Journal

The journal (J, `Journal/JournalWindow`, or the Escape menu's Journal) has five tabs: Backstory, Quests, Creatures, Mechanics and Raids. A dot marks a row not read, and each tab counts its own. `JournalContent` turns the game into `HavenShelf` lists and the window draws them with `HavenListPage`, with a wider reading column (and, for creatures, a bigger picture) than the Haven window's.

- **Text is data.** Backstory and Mechanics are `JournalPage`s in `Resources/JournalSettings`; a page can wait for a quest to start, a creature to be met, a resident to come home or to be found in a raid. `QuestDefinition` › Journal Notes and `MonsterDefinition` › Journal Lore, Advice, Habitat, Kills To Know and Portrait hold the rest, and Resident Notes are what people at home say of a creature. `AgentScripts/BuildJournal.cs` writes them all and renders the portraits: run it after changing a body. Mechanics pages are written by hand from `Docs/Combat.md`, so a changed rule has to be changed there too. Keys written as (N) or [E] follow a rebind.
- **What is shown comes from the game where it can.** A quest's objectives, status and rewards are read from the `QuestLog`; a creature's health, attacks and traits from its definition, so those lines can't go stale.
- **The record:** `CreatureRecords` notes a creature as met when it spots the player or a blow passes between them, and slain when the player's side kills it. It lives in `CharacterProgress`, is saved in `ProgressSaveData.creatures`, and a failed raid doesn't take it back. A creature not met shows only as "???".
- **A creature gives itself up in steps** (`JournalContent.KnowledgeOf`): how it fights and what it leaves after the first kill, how to live through it after Kills To Know (or being killed by one), and what is told of it after twice that. What is only ever fought once or twice has Kills To Know 1.
- **`JournalRecord`** (in `CharacterProgress`, saved in `ProgressSaveData.journal`) holds pages read, announced and found, the skills tried, what has killed the player, the quest pinned to the top of the tracker and the last 60 raids. `JournalNotices` announces new pages, rolls `LootCache` searches for the next lost page and writes each raid down in `GameSession.EndRaid`. The window only reads what these leave behind, so all of it goes on with the window closed.
- **Tutorials are the Mechanics pages.** A mechanics page has a Summary (what it is) above its body (how it works), an Art painting (LocalSpawn: `Tools/LocalSpawn/tutorial_art.json`, `generate_ui_art.py tutorials`, copied in by `BuildJournal`) and may wait for a skill to be tried (`JournalPage.unlockedBySkill`, `BuildJournal.MechanicsTriedBy`). `JournalNotices.Tried(skill)` marks it on first practice (`ExperienceTracker.Practised`) and where a system starts one before any experience is earned: a swing at a node (`PlayerGathering`), a cast (`PlayerFishing.Throw`), a crouch (`PlayerInputHandler.OnSneak`). A save with experience in a skill counts it as tried. A mechanics page that opens is not announced in words but queued in `Tutorials` (this session only; cleared by `GameSession.StartNewGame`/`LoadGame`): `TutorialNoticeHud` shows a card at the right edge with the newest one's picture until all are read, and `TutorialWindow` (T, `UIWindowId.Tutorial`, or a click on the card) shows them large, oldest first, with Previous/Next; a page shown counts as read. The journal's Mechanics tab shows each page's picture and opens it in the tutorial window. To add one: a page in `BuildJournal.Mechanics` (with its skill in `MechanicsTriedBy`), its picture in `tutorial_art.json`, paint, `BuildJournal.Apply`. `TutorialTests`.

## Creature bodies

Bodies come from CC0 models in `Art/Enemies/Downloaded` (`CREDITS.md` says whose). `AgentScripts/BuildCreatureModels.cs` imports them and replaces each creature's `Model` child, and nothing else on the prefab, including the prologue's own copies in `Data/Prologue`.

- **Person-shaped** creatures are Humanoid and keep `MonsterHumanoid.controller`, so strikes, staggers, finishers and the shield pose all work unchanged. The downloads had their feet on separate IK chains and weren't in a T-pose, which Humanoid needs, so each FBX was re-exported from Blender with the feet parented to the legs, the arms and legs straightened and that pose made the rest pose, which dropped their own animations.
- **Clips of their own** go over the shared ones in an `AnimatorOverrideController`, but never over a strike: strike speeds are fitted to each attack's Hit Delay, so a clip of another length would change the DPS and the parry. Treants get their idle, run, Taunt (as Roar) and Death from `Treant1Clips.fbx`/`Treant2Clips.fbx`, the full file imported as Humanoid with the limbs straightened in its description. The Draug shambles on the Universal Animation Library's zombie clips. Bodies of one model share a controller.
- **Falling:** a body with Falls By Animation that has a Death state plays it instead of going limp (`DeathRagdoll`). The rest ragdoll, or bake if they are animals.
- **Fitted:** each body's capsule, agent and `MonsterAnimationDriver` Stride Speed are set from its height. Stride keeps a beast's feet to the ground at its real speed. A creature at rest plays a `Resting` state when its controller has one, instead of `MonsterPose` tipping and pressing it down.
- **Telling one from the next** (`AI/CreatureLook`): Size and Shade Variation give each spawn its own, an elite is tinted with its colour and its eyes take it, and each stage of a boss sets `BossPhase.glow` on its veins (`Tree02 Veins.png`, the treant's crevices) and eyes. These change the body's own copies of its materials, as a hit's flash lays a property block over them and clears it again.
- **Eyes** are anything whose material is named `Eye ...`, Unlit and HDR so they show in the dark: the model's own where it has them (wolf, goblin), and small spheres on the head bone where it doesn't. Spheres are placed in the standing pose, as the file's own pose can be far from it, and their offsets were found by rendering each head from three sides. A corpse's go dark.
- **Gear** (`Downloaded/Gear`, modelled in Blender) is rigid and hangs on bones: the Draug's mail and helm, and the Huldra's hollow back, sized from bone positions. `BuildCreatureModels.Refit` re-does only the prefabs, for tuning this without a re-import.
- **Animals** are Generic, with a small controller in `Animation/Controllers/Beasts` on the same parameters. `DeathRagdoll` bakes a skinned animal's last pose into one rigid body, as it has no ragdoll rig.
- **Old Blender files often hold several UV maps, and FBX and Unity read the first.** If a texture looks patched or shows its background colour, the wrong map is first: measure which one lands on painted texels before blaming the texture.

## Minimap

`MinimapHud` in the corner, made for every scene with a player (`ExperienceTracker`), when the scene has terrain.

- **The ground** is painted once by `MinimapMap`, from the terrain's layers and heights, the water meshes and the NavMesh (what can't be walked is darker). Painting rather than a camera keeps it the same at noon and midnight, free of the fog and veil, and nothing renders after that. It is spread over frames, as it takes a noticeable moment on a 400 m terrain.
- **`Shaders/Minimap.shader`** does the rest: the round shape, scrolling and turning, the ward's edge, and `ExplorationMap.Texture` as the fog of war. Moving the map is a few material values.
- **Markers** are read four times a second and placed every frame, from the lists the game already keeps (`BuildingObject.Active`, `Transport.All`, `DroppedItem.All`, `ExtractionPoint.Active`, `Companion.Active`, `QuestWaypoint.Active`, `WardBreachDirector`). What hasn't been explored isn't marked. Quests and breach lanes stay pinned to the rim when out of reach.
- **`MinimapHud.Project`** is the one place a world offset becomes a spot on the map; the shader does the same turn the other way round. Up is where the camera looks.
- **M** (`GameInput.WorldMap`) opens the full explored world map, and M or Esc closes it. It shows at once, reusing the minimap's painted ground (`MinimapHud.PaintedMap`) instead of painting its own. The corner minimap keeps its close-range markers and hides while the world map is open.
  Sizes and reaches: `Resources/MinimapSettings`.
- **Regions and named places (the Haven):** `WorldAtlas` (`Resources/Atlases/<scene name>`, so a scene without one has none: raids
  are laid out anew each time) holds a 512² grid of region indices over the terrain, each region's name and where to write it,
  and the named places. The world map draws the borders as one texture (a line where a cell's region differs from its east or
  north neighbour), the regions' names (smaller for small regions), and a star and name for each place found. `PlaceDiscovery`
  (made by `ExperienceTracker` when the scene has an atlas) finds a place when the player comes within its radius: once, told
  in words, kept in `JournalRecord.Discovered` and saved. `AgentScripts/BuildHavenAtlas.cs` bakes the atlas from the same
  `HavenWorldPlan` the world was generated from: the lake and meres (with a shore strip), the bog, the mountain, the meadows inside
  the recipe's Haven ring, and the Misty Woods split into one wood between each pair of roads, its borders following the roads,
  each named by the way it lies; the 27 points of interest are the places, a kind that stands more than once given a name per
  copy (`Names`). **Bake it again whenever the world is generated again**; `WorldAtlasTests.TheAtlas_IsTheWorldThatWasGenerated`
  says when it is stale.
- **Holding the land (the Haven's Reach, [Territory](../../../Docs/Territory.md)):** each region but home has a boundary stone
  (`WorldAtlas.Region.stoneAt`, beside a path near its middle, off water; `TerritorySpawner` stands them at runtime). Lighting one
  claims the region (the first free, then a Pact Rune each), and `TerritoryState` (on `SettlementState`, saved) keeps its hold,
  0-100, worn every turn (a dawn at home, `Seasons`, or a raid ending, `GameEvents.RaidFinished`), faster under blight. The forest
  gathers against the weakest held region and pushes after two turns unless a watch is held at its stone (`StoneDefence`) or rot's
  hearts are burned on raids (rumour `RotsHeart`, only while a push is coming: `RaidRumourDefinition` › Forest Pushing,
  `WeakenIncursionOutcome`). Held regions ask a task (hunt, gather, fish there; counted from `Killed` and `ExperienceTracker.Practised`
  at the player's region), bring renown (`HavenRenown`), and Secure ones a dawn tithe into the stash; corrupted nights come from the
  pushed or weakest land (`Territory.NightBearing` in `CorruptionNights`). What each region is like: `Resources/TerritorySettings`,
  written by `AgentScripts/BuildTerritory.cs`. Shown in the Haven window's Lands tab (`HavenLands`), on the world map, on entering a
  region and above the minimap (`RegionTitleHud`). In co-op the lands are the host's: a guest's copy comes with the settlement
  (`HavenSync.ApplySettlement`, then `Territory.MirrorArrived` redraws the stones), no turn passes in a guest's game, and what a
  guest does for them (a rune offered, task work, a place found, a heart burned) goes to the host's game (`Territory.ForHost`,
  `CoopLands`, `Territory.FromFriend`); a heart counts once a run, however many players burn it. Lighting stones and standing watch
  are the host's (`Territory.CanClaim`, `CanStandWatch`); the watch's creatures are shared as any of the host's are. The stones stay
  cold until the quest line opens them (`TerritorySettings` › Unlocked By, `Territory.Unlocked`): after the third raid the portal calls
  (`Story - The Cold Stones`, `WhenRaidsDone`) and starts **The Cold Stones** (hear it out, light a stone; every unlit stone is a
  `QuestWaypoint` for it), then **What the Land Asks** (a task, a rune, Secure) and **The Forest Answers** (started by the first
  push's warning); the land tells them through quest events on `TerritorySettings` › The story. `AgentScripts/BuildHavensReach.cs`
  makes the quests, events and the portal's stories and whispers in SafeHaven. `TerritoryTests`.

## Sound

`AudioManager` owns all of it, across scenes. Gameplay refers to `SoundDefinition` assets and calls `AudioManager.Play`.

- **The ears** are an `AudioListener` of the manager's, at the player's head and turned with the camera. The top-down camera is about 19 m from the player, so with the scene's listener on it a footstep (audible to 10 m) was silent. `AudioManager.UseEars` turns the camera's listener off whenever there is a player, on every scene change, and back on without one: there must only ever be one. Use `AudioManager.EarPosition`, never the camera, for anything about what can be heard. `AudioSettings › World Sounds Level` sits under everything positional, against the music and interface.
- **Voices:** `MaxSimultaneousEffects` is the limit. A sound nobody can hear takes no voice, nor does a repeat within `SoundDefinition.MinimumGap`. When they run out the lowest `Priority` gives way, the oldest first; `Background` (birds, footsteps) never takes a voice from anything, and `Important` (being hurt, a parry, warnings) only from its own kind.
- **Music:** `MusicMemory` keeps where a playlist was left for two minutes, so a short fight doesn't send it back to the first bar. Fights change the music in `CombatFadeSeconds`, the rest in `CrossfadeSeconds`, and crossfades are equal-power.
- **Pausing:** `AudioListener.pause` follows `GamePause`. `Configure` makes positional sounds obey it; music, ambience, weather, water and 2D sounds ignore it, so menus still click.
- **Creatures:** each `MonsterDefinition` has an Attack Sound (played by `MonsterAI.AnnounceStrike` as every blow winds up: a warning, so `Important`, with a caption), a Hurt Sound (not on the killing blow) and a Death Sound. `MonsterAttack` › Sound replaces the Attack Sound for one action (a howl, a call to the ground). Size is pitch: kin share clips from `Tools/Audio/creatures.py` (`Audio/SFX/Safe Haven Creatures`), and `AgentScripts/BuildCreatureVoices.cs` sets them up, with a `CreatureFootfalls` on what has a tread. `PresentationPolishTests` holds a new creature to all of this.
- **Importing:** clips of 60 s or more stream, those of 8 s or more stay compressed in memory (streaming can gap at a loop), the rest decompress. `AgentScripts/BuildAudio.cs` applies it and `AudioSystemTests` holds it. A new loop that follows the sliders should use `AudioManager.SteadyVolume`.

## Essence

What Cursed and Purified Essence buy (GDD §13.5, §16.4). The design rule is that everything is deterministic: an offer says what it costs and what it does, and it never fails or wastes essence. A risk is a stated price, written as a negative `StatBonus`.

- **Definitions:** `EnchantmentDefinition` (school, what it fits, cost, bonuses) and `EssenceSettings` (empower levels, skill ranks, the list of enchantments; saved by id through it). Built by `AgentScripts/BuildEssence.cs`, which also sets the altar's upgrades, the great building tiers (`BuildingLevel.essence`) and the two draughts.
- **Runtime state:** `ItemStack.Empower` and `.Enchantment` belong to the one item (gear never stacks) and go wherever durability goes, so a new way of copying a stack has to keep them. Skill ranks are in `CharacterProgress`, keyed by `SkillDefinition.Id`, and aren't touched by a talent reset. `SettlementState.PurificationBonus` holds the altar's speed, because `SettlementSystem` works the queue during raids when the altar isn't loaded; the altar sets it when it loads and when a building finishes.
- **Rules:** `GearPower` (bonuses, costs, Essence Find) and `AltarWork` (what can be done now and doing it) are pure enough to test; `GearSet` applies `GearPower.BonusesOf`. `Combatant` drains life from `StatType.Lifesteal`; `LootOnDeath` adds `StatType.EssenceFind` to Cursed Essence.
- **UI:** `AltarWindow`, built in code the first time an altar is used. Empower, Enchant and Skills are one flow over `AltarSubject` and `AltarOffer`, so a new use is a new list of offers, not a new window. `CodeUI` holds the parts it shares with `HavenWindow`. `GearGlow` shows the work from outside: the colour of an item's frame in a slot and the glow on a weapon in the hand.

## Options

The Options screen (`UI/Options/OptionsScreen.cs`, built in code from `OptionsWidgets`) opens from the Escape menu and the main
menu. Tabs: Gameplay, Graphics, Audio, Controls, Accessibility. It only draws settings and writes them back.

- **A setting** is a `GameOption` in `Core/Options/GameOptions.cs` (toggle, slider or choice, with a label, a help line and a
  default), kept in PlayerPrefs because it is a preference and applies to every save. Adding one to its tab's list is enough
  for the screen to draw it; whatever it affects reads `Value`, `On` or `Index`.
- **Taking effect:** most are read where they matter (`CameraShake`, `CombatFeedback`, `PlayerInputHandler`, `DialogueBox`...).
  What has to be pushed is in `OptionsSystem`, a persistent system: at start, on scene changes and on `GameOption.AnyChanged`.
- **Graphics** (`GraphicsOptions`) work on a copy of the pipeline asset made at start; the project's assets are not changed and
  ambient occlusion on the shared renderer is put back when the game ends. The defaults are the Ultra preset, which is the
  game as authored: render scale 100%, 4x MSAA, 4096 shadows, Ultra terrain. Lower quality is only ever the player's choice.
- **Look** (bloom, motion blur, film grain, brightness) is a global override volume at priority 1000, so scene profiles stay
  as authored. Brightness uses Lift Gamma Gain, which no scene profile sets.
- **Display** (`DisplayOptions`) stores the size, not a list index. A change must be kept within ten seconds or it goes back.
  The Editor's Game view keeps its own size, so none of that shows there.
- **Controls** (`InputRebinds`): `Rows` lists what can be rebound; overrides go on the Input Actions asset and into PlayerPrefs,
  so `GameInput.KeyLabel` follows them. A key in use swaps with the other row. Escape is the game's own. While waiting for a
  key every map but UI is off. Text is written with the default keys ("[E]", "(N)", quests' "(B)"), and `GameInput.WithRebinds` puts in the
  new key wherever it is shown (quest tracker, dialogue, messages, prompts). It costs nothing without a rebind.
- **Accessibility:** Sound Captions (`SoundCaptions`, a persistent system: `SoundDefinition` › Caption, with the side it came from), Rarity
  Letters on item slots, Damage Taken and Parry and Dodge Timing (`GameOptions.DamageTakenShare`, `TimingShare`). An assist in use is written to
  the playtest log as an `Assist` row, so a helped run can be told from another.
- **Not in here:** audio volumes stay in `AudioManager`, camera styles in `CameraPreferences`; the screen uses them directly.

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

## Fire

Flames are the **Fire** shader (`Shaders/Fire.shader`, set up by `AgentScripts/BuildFire.cs`). It is additive, procedural (no textures) and HDR, so the bloom glows round it. The flames lick upward and flicker, yellow at the heart, orange and then red at the tips, and lean with the wind; each fire is out of step with the others. It has two modes:
- **On a mesh** (the default): the round mesh the fires have always used (a Unity sphere) is pulled up into tongues. `StationFire.mat` uses it, so every torch, campfire, furnace, fire arrow and the prologue's fires burn with it.
- **Flame From UV** (`_FIRE_UV`): the flame is shaped in each particle's or quad's UVs. The festival bonfire's flame particles use it (upright, low and dense: `BuildFire.Upright`). Add **Face Camera** (`_FIRE_BILLBOARD`) for a lone quad.

The light is the fire's `LightFlicker`: warm yellow (1, 0.8, 0.46), sliding towards a deeper orange as it dims. It wanders a few centimetres so shadows move (only when the light has its own child object). With **Sparks** set, motes rise from it (`Resources/Fire Sparks`); the campfire, standing torch, hand torch, camp and bonfire have them.

## Co-op (multiplayer)

Up to four players play together; one hosts (`MultiplayerSettings`, Resources; Systems Editor › Co-op). Built on **Netcode for GameObjects** (host-authoritative listen server). With the Steam client running, sessions go through **Steam**: `SteamPlatform` starts Steamworks.NET (app 480 through the SteamAppId environment variable), `SteamLobby` opens a friends-only lobby holding the host's Steam id and protocol version, friends join from an overlay invite, "Join Game" in their friends list or a `+connect_lobby` launch, and traffic goes over Valve's relay (SteamNetworkingSockets transport: no IPs, no ports). Without Steam, sessions fall back to **Unity Transport** by IP/LAN. Steam code compiles only while the Steamworks.NET package is present (`SAFEHAVEN_STEAM`). Decided with the user: visitors may help in a Haven with the host's approval, raid loot is personal, Steam app 480 until the game has its own.

**The approach: friends beside you, not a networked player.** The player you control stays the ordinary single-player `Player` (all 60 components, `Player.Current`), run entirely on your machine. Co-op adds friends beside it:
- `CoopSession` (a game system) is the connection only: it owns the NetworkManager, made in code with Netcode's scene management **off**, so each game changes scenes on its own, and runs the **parts** of co-op through one life (`ICoopPart`: `Begin(CoopLink)` when a session starts, host or guest; `Tick` every frame while connected, each part pacing itself; `FriendLeft` on the host; `End`). The parts: `HavenShare` (the world for visitors, its changes, the settlement and the hour, and when a visit ends), `VisitorAccess`, `RaidsTogether` (following the host, raid news, extracting together), `CoopCreatures` (with `CreatureHost` and `CreaturePuppets`, one role per side), `CoopDowned`, `CoopPings`, `CoopLessons`, `CoopLands` (what a guest does for the host's lands, Territory), `CoopDrops` and `CoopHud`; a part failing in `Tick` is logged and the others go on. `CoopLink` holds the named messages every part uses (to the host, to a guest, to every guest but one, or passed on by the host for a guest), all on one reliable ordered channel unless a part asks for unreliable; `CoopPlace` is where a player is (scene, and in a raid its run). `Host`, `Join(address)`, `Leave`; joining needs the same `ProtocolVersion` and a free place. While a session runs, `GamePause.FreezesTime` is off: the Escape menu blocks your input but the world keeps running. The Escape menu's Co-op button opens your game to friends, closes it, or leaves.
- `NetworkPlayerAvatar` (the Network Player prefab, one per connection, lives across scene loads): on its owner's machine it publishes a `PlayerLookState` (name, character-creation body, gear by slot, held item, scene — ids only, a network variable) and sends `PoseSnapshot`s (position, facing, `AnimatorFrame`) 20 times a second, unreliable. Elsewhere it draws a `RemotePlayerBody` when the friend is in the same scene. A fishing line is a third value, `FishingLineState` (phase, where it was cast from and landed, the server time it was cast, the reel's progress in 32 steps; one byte when no line is out), written when it changes; friends draw it with the same `FishingLine` view on the friend's body (`RemoteFishing`), so the float arcs, bobs, dips at a bite and is reeled in for everyone. A raid companion travels the same way: `CompanionLookState` (who, and whether they are down) with their own poses beside the player's; friends draw them from the resident's own model with their animations and weapon (`RemoteCompanion`, built through `CompanionBody` as in the owner's game), and "[E] Help up" on a friend's downed companion asks the owner's game to get them up (`HelpCompanionUpRpc`; `FriendRevive` takes what helping does).
- `RemotePlayerBody`: a modular body built in code like the character preview (Animator with the player's controller, `ModularCharacter`, no player components, clip events off), dressed by `PlayerLook.Outfit` (the same rules as your own outfit), holding what they hold, with their name over them.
- `PoseBuffer` draws friends `InterpolationDelay` behind the shared server clock, between the two poses around that moment; a big gap is a teleport. `AnimatorMirror` sets the friend's parameters and blends any layer into the state their game is in, which covers triggers and code-driven cross-fades.

**Visiting a friend's Haven** (`HavenVisit`). A save divides into the *world* (`world`, `settlement`, `stash`: the host's) and the *character* (`player`, `progress`, `appearance`, `quests`, `happenedEvents`, `playtest`: yours). Once a guest is connected they ask for the Haven (`SafeHaven.WorldPlease`); the host sends `HavenVisit.WorldOf(SaveGameService.Capture())` (no character of theirs), gzipped and in 4 KB pieces (`WorldTransfer`, fragmented reliable messages). The guest keeps their own game in memory as *home* and plays `Combine(hostWorld, home)` through `SaveGameService.TryPlay`, beside the host (or at the Haven's arrival point when the host is on a raid). Saving while visiting writes `KeepHome(home, now)`: the guest's own journey with their character as it is now, never the host's world. Leaving, or the host closing, plays `KeepHome` again: home with what the character gained. The host's hour follows every 5 s (`SafeHaven.Clock`). Raids, ward breaches and loading a save are refused while visiting (`HavenVisit.Refusal`); a guest's own game doesn't run the host's settlement (`SettlementSystem` waits) and a guest crafts and builds only from what they carry (`MaterialPool` leaves out the stash and carts).

**Keeping the Haven in step** (`HavenSync`, `SafeHaven.Entities`). Every half second each side compares its loaded `SaveableEntity`s' `SharedMark` with the last one seen and sends the ones that changed, were created by play (`SaveableEntity.Created`), or are gone, in their save format (`SaveMapper.ToData`/`ReadInto`), gzipped and halved until each message is under 4 KB. The host's game has the last word: it applies a guest's changes without noting them as seen, so its next look sends the result to everyone, the sender included, and two players changing the same thing at once end with the host's. The receiver writes them into its `GameSession.World` (also when that place isn't loaded, say the host is on a raid) and onto the loaded objects (`SaveableEntity.ApplyShared`, `PersistentScene.Recreate`), noting their marks so nothing echoes back; the host passes a guest's changes on to the other guests. Components say how they are shared with `ISharedState`: nodes compare only gathered-or-not, felled trees which trees are down, buildings level, payment and whole seconds of work, health its whole points; the explored map, the clock and story scripts stay each player's own. Restores run again on live objects, so `ResourceNode` grows back and dropped items and carts replace what they hold; felled trees add up between games (each grows its own back on its timers), so a friend's list never stands a tree felled here back up. The host moves anything saved with its position; guests move only what was placed while playing. Whoever changed something last wins. All co-op messages share one reliable sequenced channel, so changes never arrive before the Haven they belong to.

**The settlement is the host's, mirrored** (`SafeHaven.Settlement`). Every 2 s the host compares its settlement as save data (purifying and workshop progress left out, so ticking alone sends nothing) and sends it when it changed; a guest takes it on in place (`SettlementState.TakeOn`, a reflective field copy that keeps the object, so `Changed` listeners such as residents, the portal and the ward marker stay subscribed and redraw). The guest's game doesn't run the host's settlement: `SettlementSystem`, `HavenProgress` (renown and rank), `HavenRequestBoard` and `CorruptionNights` wait while visiting. Decisions that would change it are the host's and are refused before anything is paid (`HavenVisit.LeftToHost`): residents' jobs, repairing the portal, strengthening the ward, the altar, trades and workshop queues.

**Raids together: friends follow the host** (`RaidInvite`, `SafeHaven.RaidOpen`/`RaidClosed`). A guest can't open the portal themselves (`HavenVisit.RaidRefusal`). When the host goes through, `RaidLauncher.Launched` hands `CoopSession` a `RaidInvite` (raid, seed, tier, modifier ids, sparks, size, extra seconds, extraction cut, foretold count, length), sent to the guests; the portal then offers "Follow (host) to (raid)" while the host's run has a minute or more left. `RaidLauncher.TryFollow` builds the same run (`Build`/`FromInvite`, shared with launching), so the seed generates the same map, with the follower's own gear and nothing paid; their clock starts where the host's stands. Loot caches roll their own (personal loot). A follower returns to the host's Haven, and their save on the way in keeps `raidUnderWay`. Friends see each other in a raid only on the same run (the avatar's place is scene#seed).

**Creatures in a shared raid, and in the host's Haven during a visit, are the host's** (`CoopCreatures`, `MonsterAI.Remote`, `FriendHitbox`). In the Haven the host's game runs its mob zones' creatures whenever friends stand there; a visitor's game makes none (its `MobSpawnZone`s hold off, so their emptiness isn't a clearing) and takes the host's over 2 s after the host leaves (quietly), giving them back when the host returns. The host numbers each creature and tells each friend on the same run (avatar place scene#seed) of those within 90 m, by prefab id (`SaveableEntity.PrefabId`; every raid creature has one, the prologue's Expedition ones don't), then ten times a second their position, facing, speed, state, health and whom it is after (unreliable, 54 to a message), each strike with whom it is at, and each death. In the host's game a friend's body has a `FriendHitbox` (player layer, a priority target) so creatures notice, chase and strike friends; it takes no damage there. A friend's game removes its own creatures and stops roaming spawns (`CoopCreatures.FromHost`) and builds the host's as puppets (`MonsterAI.MakeRemote`: no thinking, no path finding, follows poses). A strike plays out in the friend's game against where they really are, so it lands or misses fairly. For that the puppet has to be the host's creature in everything a strike reads: it is readied with the raid's and the stage's strength as it is built (`RaidCreatures.Prepare`, or a tier IV creature hits a friend as a tier I one does), a charge travels over the NavMesh without its agent (`MonsterMovement.Detached`), a strike at someone else goes where the host's went (the strike carries where it is aimed, and the puppet lets go of any target of its own), summons are left to the host's game, and whether it is unaware is read from the host's state and whom it hunts (`MonsterAI.IsRemoteUnaware`: its own senses never look, so by them every blow was an ambush). A friend's blows show at once but can't kill (`Damageable.HealthFloor` 1); they go to the host (`CreatureHit`, the friend's own and their raid companion's), are applied there past the creature's guard, which the friend's game has already asked (`Damageable.ApplyJudged`), and provoke the creature onto the friend (`MonsterAI.ProvokedBy`). Only a blow that kills carries the host's name there, when the host had struck it too. A stagger is the host's: its creature's poise is what blows wear down, and the Staggered state shows on every puppet; a stagger begun in a friend's game (their parry, a finishing blow holding it) is sent (`CreatureStagger`, `MonsterAI.RemoteStaggered`) and stands there meanwhile. A death on the host finishes the puppet (`Damageable.Finish`, which takes what health is left past any defense or invulnerability, so the number shown is what it had), with the friend as killer when they had a hand in it, so the kill, experience and loot (given straight to the killer's bag by `LootOnDeath`) are theirs; the host likewise gets the kill when a friend's blow finishes what the host had hit. If the host leaves the run first, the puppets become the friend's own creatures (`MonsterAI.MakeLocal`). Puppets are drawn on the session clock 0.15 s behind (`PoseBuffer`, snapshot interpolation; a pose after a quiet spell starts from where the creature stood). Each strike carries its time and where it began: the puppet steps there before striking, and a strike more than 0.6 s late is dropped. A friend's game makes no creatures of its own while the host sends them (`MonsterAI.SpawningRemote`/`SuppressedByHost`, removed as they wake). Poses are 19 bytes (id, exact position, facing, speed, state, health and whom it is after in a byte each: nobody, this friend, or someone else on the players' side, `MonsterAI.RemoteHunt`, so a puppet's `IsEngaging(player)` and `IsHuntingPlayerSide` drive the friend's outline, combat music, overhead bars and parry and backstab rules as the host's own creatures do) and sent only for creatures that moved or changed, at least once a second each, and three times a second beyond 40 m: about 0.4 KB/s for a dozen and a half creatures where it was 4 KB/s. A friend's game makes no raid creatures at all, not even for a moment: `CreatureSource.Remote` (an `IRaidCreatureSource`, set by `CreaturePuppets`) makes `SpawnGroup.PlaceAt` skip creatures after the same rolls (the rest of the layout lands where it does in the host's game), set-piece guards and roaming spawns stay in the host's game, and what the friend wakes is asked of the host: an extraction beacon's alarm by its group's hierarchy path (`CreatureWake`, `RaidGenerator.FindGroup`), an event's creatures by prefab id (`CreatureCall`, within 60 m of the friend). The host fills them there and sends them after the friend (`RaidDirector.Wake`/`PrepareSpawned` with a `prey`). While hosting friends, hit-stop and slow motion leave time alone (`GamePause.TimeIsShared`, set by `CoopSession`): the host's time runs the creatures friends fight. A creature removed without dying (a roamer left behind) is sent as gone (`CreatureGone`), and a friend whose place changes (died, went home, came back) is told of everything again. An elite's affixes travel with it by name, and the friend's game makes the same elite of its puppet (`EliteCreature.Make`, scaled by its own raid's multipliers): same title, ring, size and behaviours, which act in the friend's game as strikes do. A boss comes as a puppet of its prefab with its own `BossEncounter`, so the bar, music and phases follow its mirrored health; its defeat counts in the friend's own Haven too (`HavenVisit.BossBeatenHome`), with its line and skill on their own first win.

**Wire format and cost.** `MultiplayerSettings.ProtocolVersion` is the asset's version plus `WireFormat` (raise it whenever a message changes), so mismatched builds are turned away cleanly. Friends' poses carry animation weights and parameters at half precision (bools already as bits) and their arrays come from `FramePool`, handed back as the pose buffer drops frames, so receiving makes no garbage. Places are compared as fixed strings, worked out once per scene or run (`CoopCreatures.PlaceKey`). The world for a joining friend is packed on the main thread on purpose: it and the changes after it go out in order on one channel, and packing it elsewhere would let changes overtake it.

**Downed and helped up** (`CoopDowned`, `FriendRevive`, `SafeHaven.HelpUp`). While a friend who is standing is on the same run, the player's health is held at 1 (`Damageable.HoldFloor`: several holders, the highest counts, so a Deathless relic and this rule never undo each other). Reaching it knocks them down at the next look (after a relic has had its say): invulnerable, no input, lying as the dead lie (`PlayerAnimationController.SetDead`, mirrored to friends), left alone by creatures (`MonsterTargeting.CanTarget`, `FriendHitbox.IsDown`), and their look says `Downed`. Friends see "Help up" on their body and hold E (`MultiplayerSettings.HelpUpSeconds`); the help goes to them (through the host for a guest) and they get up with `HelpedUpHealth`. They die after `BleedOutSeconds`, or 3 s after no friend is standing on the run, and the raid ends for them as any death does. Alone, a killing blow kills as always.

**Joining late and leaving mid-raid.** The host keeps its run's `RaidInvite` while the run lasts; a friend who joins while the host is out gets it right after the Haven (`TellOfRaidUnderWay`, with the seconds already gone), so the portal still offers to follow. A session that ends during a friend's raid (they leave, the host closes or quits, the connection drops) doesn't cut the run short: their creatures become their own (`CoopCreatures.Reset` hands puppets back) and the trip home waits until they are back from the raid (`CoopSession.EndVisit`).

**Playing together** (`CoopPings`, `PartyHud`, raid news). Each friend on the run adds `MultiplayerSettings.CreatureHealthPerFriend` (60%) to every creature's health in the host's game (`CoopCreatures.ScaleForParty`, a stat modifier; health keeps its share). The Ping key (Player/Ping: Y, or the mouse's forward button) marks what is under the crosshair, an enemy or a spot, for everyone in the same place: a label in the world and a mark on the minimap for 8 s, one per player. Friends show on the minimap (pinned at the edge when far, red when down) and in the party panel at the left edge (name, health from their look, distance, "Down!" or "Elsewhere"). Each fall in the same raid leaves `BleedOutPerFall` (70%) of the last bleed-out, down to `ShortestBleedOut`. Friends hear how each other's raids ended (`SafeHaven.RaidNews`). The host can take back every yes from the Escape menu (`VisitorAccess.RevokeAll`); a guest with the stash open sends what they just changed and the stash closes.

**Down, watching, handing over, getting out together.** Down, a player crouches and crawls at `CrawlSpeed` (the input handler is off, which turns the Player map off, so only Move is switched back on; nothing while paused) and sees the seconds left and any friend helping, with their progress (`DownedHud`; `FriendRevive` sends `SafeHaven.Helping` as E is held or let go). Going down is decided the frame after the blow (a Deathless relic heals within it), before regeneration can lift health off the floor. Friends see "Name · Down!" or "· Fallen" over them; creatures leave the down and the dead alone. A death with a friend still standing on the run hands the raid's finish to `CoopSpectate`: the camera follows a friend (`PlayerCameraController.Watch`), Dodge for the next, Interact to head home, and it ends by itself when nobody is left standing or the session ends. What a player drops on a raid lies in every friend's game on the run (`CoopDrops`): a pickup asks the host, who lets one player at a time take it, and what is left or that it is gone goes to everyone; a friend who leaves mid-pickup frees it. Extracting beside a friend, or within the minute after them from the same raid, gives `TogetherExperience` for each (up to three); the party panel shows "Got out", "Fell" or "Caught" for friends whose raids ended.

**What a visitor wins stays theirs.** Quests count a visitor's own deeds, not the host's Haven: a build objective that counts standing buildings counts none while visiting (what they build there still counts). A traveller a guest rescues on the host's raid joins the guest's own Haven (`HavenVisit.RescuedHome`, into the home game that saving and going home write), and a boss they help bring down counts as beaten there too (`BossBeatenHome`); its defeat line and skill come on their own first win, whatever the host's Haven has seen.

**Building and the stash take the host's yes** (`VisitorAccess`). A guest opening the build menu or a stash chest asks (`SafeHaven.Ask`); the host is told and answers from the Escape menu ("A Friend Asks", on the confirm page), and a yes lasts while the guest stays (`HavenVisit.MayBuild`, `MayUseStash`). Building needs nothing more: it reaches the host through `HavenSync`, paid from the guest's own materials. The stash goes through `StashShare`: the host's is the real one and is sent whole (`SafeHaven.Stash`) when a guest is let in and whenever it changes; a guest sends only what they put in or took out, by item kind and condition (`SafeHaven.StashChange`), so the host's workshops keep filling it meanwhile. Changes are numbered, and a guest takes on the host's stash only once it includes their latest, or a stack just taken could show again for a moment and be taken twice. Steam is off in the Editor by default (`MultiplayerSettings.SteamInEditor`): while on, Steam treats the Editor as the running game and closes it when Steam shuts down.

Testing: `CoopTests` (edit mode). Live, one Editor can host and run a second NetworkManager as a guest over 127.0.0.1 (Netcode allows several in one process). Multiplayer Play Mode instances tagged "Host"/"Guest" start a session on their own once they have a player and always save to a throwaway folder; each instance is a whole Editor, so check free RAM first.

**Not yet:** a real join through Steam, and a visit between two machines, are untested here (they need a second account or PC; the parts were tested in one Editor). Guests can't yet ask for settlement decisions (a job for a resident, a trade); the host makes them. A friend's companion is never a creature's target in the host's game (only caught by what strikes near it), and a boss fight hasn't been played through together yet. The puppet's side of a fight (charges, strikes at others, staggers) was checked in one Editor by driving a creature as a puppet, not yet between two games. Next: a playtest on two machines (Steam join, a visit, a raid together).

## Editor memory

The forest and mountain packs bring hundreds of 4K/8K textures, and the Editor held 10+ GB. Three things keep it down:
- **Editor texture cap** (`EditorTextureCap`, Safe Haven › Editor › Light Textures in the Editor): Unity's import override caps the Editor's own copies at 2048 (local Library only). `SafeHavenBuild.BuildWindows` lifts it for the build; any other build with the cap on stops with an explanation.
- **Texture streaming** (`AgentScripts/StreamLargeTextures.cs`): the large textures the game uses stream their mips (both quality levels, 2 GB budget on PC), so the game and the GPU keep only what the camera needs.
- **After Play** (`PlayModeMemory`): with no domain reload a session's assets stayed loaded (about 2 GB per Play); they are unloaded when Play ends.

Import workers are limited in the local Editor user settings (2 at most, none kept on standby).

## Look and lighting

| To change | Edit |
|---|---|
| Color grade, bloom, vignette of Safe Haven / the raid | `Settings/Rendering/Volumes/SafeHaven_Volume` / `MistyWoods_Volume` (each scene's Global Volume uses one) |
| Sun, ambient light, fog | The scene's Lighting window (Environment) and its Directional Light |
| Anti-aliasing, shadow quality, HDR, render scale | `Settings/Rendering/PC_RPAsset` (Mobile_RPAsset for the Mobile quality level). Shadows are fitted to the top-down camera (14–28 m away): 2 cascades split at 30 m, 50 m distance. Lowering quality for frame rate belongs in player options, not these defaults |
| A heavy model's triangle count (Local Generator trees reach 100k) | **Safe Haven › Setup › Art Pipeline › Simplify Model Meshes** (`Editor/Environment/ModelSimplifier`): picks a Unity mesh-LOD level per mesh, bakes it to `Simplified/` beside the prefab and points the prefab at it (vertices and wind data kept; level 0 restores). Trunks and rocks simplify well; leaf cards lose whole cards, so leave leaves alone. The oak trunks round the Haven use it |
| A model's budget (generated models arrive at ~10k triangles whatever their size) | Top-down rule (the user, 2026-11-09): nothing in the top-down view draws a lower LOD; LOD0 is sized for that view and the lighter levels only serve the action camera's horizon (`Editor/Environment/ScreenBudget`: 147 px a metre at 2160p at the default zoom, 40 px per visible triangle, LOD0 down to the farthest ground at full zoom-out). `Tools/Blender/ingest_generated.py` decimates each generated look as far as its shape allows, measured in a fraction of a second without baking (95% of the surface within 2% of its size, silhouette from the game camera 95% the same), and writes the result to `<Asset>_Budget.json`; LOD1/LOD2 come from Blender or, where its collapse fails, Unity's mesh LOD generator (`ModelSimplifier.Lighter`). `Editor/Tests/ModelBudgetTests` checks every gathering node: LOD0 in the whole top-down view, pixels per triangle at the default view (shape-limited looks reported, leaf cards excluded: their cost is overdraw), LOD0 cap by size, vertices per triangle, hull size, readable meshes, texture size. Known debt listed there: the scanned FallenLog, GreatBoulder and OldStump are denser than the view needs |
| Ambient occlusion | The SSAO feature on `Settings/Rendering/PC_Renderer` |
| A new place's mood | Duplicate a volume profile, assign it to the new scene's Global Volume, set its lighting |
