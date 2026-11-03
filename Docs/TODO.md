# Safe Haven – TODO

Open work and ideas. Tick items off (`[x]`) or delete them when done. Architecture notes for each system live in
[ARCHITECTURE.md](../Assets/_Project/Scripts/ARCHITECTURE.md).

## Combat

- [x] **A finishing blow on staggered enemies**: Draug and Barrow Shieldbearer. More person-shaped creatures are a tick on Can Be Finished.

## Reasons to raid (relics, rumours, people, finds)

- [x] **Authored set-pieces in the generated maps (GDD §9.1, §9.2)**: six so far, on every rumour site and a few camps. Still open: a troll bridge over a ravine and a flooded hollow, which need the ground shaped for them, and rumours that name a place and a direction instead of an exact distance on the HUD.
- [ ] Play them for feel: every number is a first estimate (relic powers, 2 rumours a run, job bonuses, 6% relics in caches).
- [ ] Real art for the rumour sites: they are placeholder shapes (`Prefabs/Raids/Events/Sites`), as is the Elder Tree.
- [ ] Icons for the plans and living things: the plans use the Forge Blueprint's, the others borrow from materials.
- [ ] Give the expedition their own models: everyone is IngridModel, at home and in raids.
- [x] Troll Valley has its own Layout Rules, creatures, tiers and rumour pool, opened by beating the Heartroot. Still the Woods' scene and art: it needs a look, its bridges and its boss (GDD §10.7).
- [ ] A goat (or other animal) for the Haven: bees, seed and the Elder are in, a living animal isn't.
- [ ] Relics in the raid report: they are named in "On the way", but the haul lists the plain item.

## Raid maps (Misty Woods forest terrain)

### Art
- [ ] Replace the placeholder forest kit (spruces, pine, dead spruce, boulders, log in `Assets/_Project/Art/Forest`)
      with real tree and rock art. Swap the prefab mesh/material, then "Raid Map" › Bake Map. No layout changes needed.

### Designer tools
- [ ] Add a "Raid Maps" page to the designer window: preview of the marker image over the height map, legend,
      site counts per group and a Bake button.

### Content
- [ ] More event-site variety (new prefab in `Prefabs/Raids/Events` + an entry in `MistyWoods_Events`).
- [x] Dedicated wolf pack prefab for enemy camps (one howler + one or two packmates), with staggered attacks.

### Polish and fixes
- [ ] Ground event prefab children individually; pieces sit at local y = 0 and may float on slopes.
- [ ] Check that the Campfire in the Abandoned Camp event doesn't carry Safe Haven station/building logic into raids.

### Verification still to do
- [ ] Play-test the 180° and 270° map rotations (only 0° in the editor and 90° in play mode are verified).
- [ ] Play a full raid from the Safe Haven portal through the loading screen into the rotated map and back.

## Art still owed by the new systems (2026-11-03)

- [ ] The Heartroot and its stump: it is the Root Guardian's mannequin at 1.55 times the size. A boss should look like one.
- [ ] Heartroot's Grasp has Frost Rune's icon and effect, with roots on the hit.
- [ ] Heartroot Fight is a synthesised drum loop (`Tools/Audio/boss.py`), 33 s and never heard by whoever made it: listen to it, and a composed track may be better.
- [ ] Haul Sack has the Hide Rucksack's icon.
- [ ] Set-pieces are the forest kit and primitives. The Old Barrow's mound most of all: from the gameplay camera it is a smooth green shape.
- [ ] Finished goods at a workplace, growing as residents make them: tools and sounds are in, the goods are not.

## Play these (2026-11-03)

Everything below was driven by scripts, not played by hand: the numbers are first estimates.

- [ ] The Heartroot's 1100 health, 320 poise and damage, against kits a third raid really has.
- [ ] Companion strength per tier, and whether 45 seconds is fair in a real fight.
- [ ] Guards: two killed a first breach wave with the player standing still.
- [ ] Whether set-piece guards are too much on top of the raid's own camps, as every rumour now has some.
- [ ] Large raids: is 29:10 right for four times the ground? Are 12 set-pieces and 533 resource nodes too much or too little to find, and is the walk to an exit fair?
- [ ] Companion ranks: does +30% health and +20% damage at Sworn take too much of the fight?
- [ ] Whether a rumour's rough direction is enough to find it by, on a normal raid and a large one.
- [ ] A Day raid (11:00, sun 1.36) reads as deep blue night on screen. Kept as it is for now (your call, 3 November); come back to it after playtests. The fog of war is not the cause.
- [ ] Raid HUD: does one way line, the maps and M give enough, or do you miss seeing every exit at a glance? (`RaidHudView`; the old list is in git.)
- [ ] A large raid has six exits, spread out (nearest 283 m from the start on the seed tried). Is the walk fair? `Large Raid › Extra Exits` is a list of names.
- [ ] Long messages moved beside the vitals, pickups stayed by the character (1 Nov playtest fix). Is that the right split?
- [ ] Amnesia: is one memory back every 2 raids right, and does a lost companion turning up on the very next raid make losing them cost too little? `Memory Returns Every`; a wait would be a rule on the rumour.
- [ ] Someone lost stands still until found. An idle or wandering animation, and their own lines (`ResidentDefinition › Returning Lines`, only general ones are written).
- [ ] Heavy bag: 80% speed at a full bag, and whether the Haul Sack's noise is a real price.

## Materials as items (weight and carrying)

- [x] Soft encumbrance: past 80% of the bag's budget speed, stamina and noise ease toward their full-bag values (`PlayerBurden`).
- [x] Haul Sack: more weight, fewer slots, slower and loud. Still open: a pack animal or wheelbarrow (GDD §12.4).
- [ ] Refine at home pays off: tune item value per weight so ingots/planks/blocks are clearly better to carry than raw ore/logs.
- [x] Store all materials in one press at the stash (`StashStorage.StoreMaterials`), with stations still pulling from storage.
- [ ] Decide essence loss on a failed raid (e.g. lose the essences gathered this raid, keep banked ones).
- [ ] Ground drop prefabs for materials (a generic sack/bundle prefab) so dropping a material spawns something to pick up.
- [ ] Per-material stack size tuning after playtest (hides 20, trollstone 10, the rest 50).
- [ ] Show the weight of a full stack in the raid preparation window, so the loadout's weight is visible before leaving.

## Saved games (2026-10-30)

- [ ] Show a picture of the place or the settlement on each save's row.
- [ ] Saves with the same name are allowed; decide if that should stay.
- [ ] Save list: confirm it stays quick with many saves (each row reads its whole file when the list opens).

## Options (2026-10-30)

- [x] Key prompts in text follow a rebind (`GameInput.WithRebinds`).
- [x] Sound captions, rarity letters, damage and timing assists. Still open from GDD §18.5: full controller support for prompts.

## New content (2026-10-30)

Raid events, gear, food, buildings and creatures are in, built by `AgentScripts/BuildGear.cs`, `BuildFood.cs`,
`BuildCreatures.cs` and `BuildRaidEvents.cs` (run them in that order). Open:

- [ ] Balance everything by playing: numbers are first guesses against the existing gear and monsters.
- [ ] Play the raid length: `MistyWoods.asset` is at 1000 s (16:40) while its stages and exits are written for 720.
      Event windows are in seconds, so they fit either way, but fewer events fit in 300 s.
- [ ] Real models and icons: new creatures, buildings and event sites are placeholder shapes.
- [ ] Fit shields and new weapons in the hand (Safe Haven › Characters › Held Item Grips): grips are copied from similar items.
- [ ] Status icons: conditions show as words above the vitals (GDD §10.6 asks for icons).
- [x] Watch Post upgrades name a raid's coming events in the briefing, in place of a separate Scout Post (`RaidForecast`). Decide if the GDD's Scout Post should still be its own building.
- [x] Defender roles for ward breaches: Warden and Hunter stand guard (`ResidentDefender`). Check it doesn't take the fight from the player: two guards killed a first wave unaided.
- [ ] Settlers (Hild, Asa, Gunnar, Yrsa) borrow expedition models: give them their own, and icons for the Boar Pie, Trout in Cream, Haven Feast and Moon Broth, which show the stew's for now.
- [ ] Trollstone armor copies the moonsilver worn look: give it one of its own.
- [ ] Listen to the portal's voice in the game: its takes were measured, never heard. `Tools/Audio/portal.py`, level on Audio/Sounds/Library/Portal Voice.
- [ ] Playtest the base progression pace: raids per tier, Purified Essence against ward, altar and upgrade costs, Trading Post prices.

## Code quality: SOLID refactor (analysis 2026-10-29)

Rules written as special cases instead of following the existing model. Work top to bottom.

1. [x] **Interaction kinds.** `Interactable` gets an explicit Kind: **Use** (completes on the press: talking, opening
       a chest, station, portal, lighting an exit; no player state, animation or work bar) or **Work** (today's timed
       work; duration 0 still means "until Complete()"). Set Use in data (prefabs, scenes, prefab builders), and remove
       the `Instant` flag set in four `Awake`s, the fake 0.2-0.4 s durations and ExtractionPoint's Complete-on-Started.
   - [x] **Talkable**: one shared conversation component (open, camera framing, walk-away close) that gathers pages
         from `IConversationTopics` sources, replacing the copies in QuestGiver, PrologueCompanion and ResidentNpc.
2. [x] **IInteractionName** instead of the type checks in `WorldInteractionIndicator.NameOf`.
3. [x] **Target priority as data** (`Combatant`) instead of `Player.IsPlayer` in MonsterAI target choice; arrow
       recovery as a `Launch` parameter instead of checking whether the owner is the player.
4. [x] **Split large classes**: `Companion` (movement/combat, downed, orders, looks) and the target choice out of
       `MonsterAI`.
5. [x] **Non-blocking lines in combat**: lines without answers during a fight show as subtitles and don't hold attacks,
       so the prologue's wait-for-the-line rule and the Troll Ambush reorder become unnecessary.
6. [x] Smaller: companions stop only when they themselves are talked to (not on any open dialogue); companion grips as
       rig data instead of the player's HeldItemView; one encounter definition per prologue chapter (camp wolves and
       portal enemies the same way).

## Code quality: quests and events (analysis 2026-10-29)

Same review for Quests/ and Events/. Work top to bottom.

1. [x] **One "by the player" rule.** Done: `GameEvent.ForPlayerSide` (quests, kill experience) and the strict
       `GameEvent.ByPlayer` (sounds, the prologue's own fire, gather experience). `GameEvent.ByPlayer` (`Player.IsPlayer`; no actor counts as the player) and
       `ExperienceTracker.ByPlayer` (no actor does not count) disagree. Decide once, on the side (`Faction.IsPlayerSide`
       of the actor's Combatant), so a companion's kill counts for "kill 5 wolves" and a wolf burnt by nobody doesn't.
2. [x] **Objective rules in one place.** Done: every per-type rule is in `QuestObjective` (Progress, Describe,
       FindProblem, Owned, FieldsOf for the drawer). Fixed on the way: the Inspector hid a gather objective's material
       item. Left for when a type needs it: one class per type (`[SerializeReference]`, like `StoryNode`) with an asset
       migration.
3. [x] **Death subject without type checks.** Done: `IEventSubject` (MonsterAI, BuildingObject); the kind comes from
       the Damageable's category. `GameEvents.OnAnyDied` asks for MonsterAI, then BuildingObject; give the
       dying object a small interface for its definition (like `IInteractionName`).
4. [x] **No Portal inside the generic trigger.** Done: `TriggerWhen.PlayerInsideArea` watches any `IArea`
       (HomePortal is one); the SafeHaven scene's Portal Bloom points at the tree. `GameEventTrigger`'s PlayerInsideWard polls `HomePortal.Current`;
       an area interface (or a ward trigger component) keeps the trigger generic.
5. [x] Smaller. Done: `IDisplayName` (ItemDefinition, ResourceDefinition) replaces the type switches in
       QuestObjective, StationMenu and StationWindow. Kept on purpose: `QuestGiver.CanInteract` advancing its waiting
       lines only happens for givers without a Talkable (notice boards), whose prompt is the line; a clean fix needs a
       "focused" hook on Interactable. HomePortal's talked event is raised when its menu (ward, breach) closes, not on
       talk, so it is not Talkable's.

## Code quality: other systems (analysis 2026-10-30)

Same review for the rest of the runtime code (SOLID, Unity practice, no special-case rules). Work top to bottom.

1. [x] **MonsterAI is still 1404 lines.** Done: `MonsterMovement`, `MonsterAttacks` and `MonsterPose` (1063 lines
       left: the state machine with perception and investigation, which drive its states). Was: targeting is out (`MonsterTargeting`); perception and investigation
       (Hear, Sense, TrackTarget, the investigation states, idle gaze, ambush, indicator) and attack actions
       (BeginAttack, UpdateAction, charge, howl, projectile, retreat) are two more plain C# parts, as `Companion` was
       split. Behaviour unchanged; the existing AI tests cover it.
2. [x] **Item or resource is one idea.** Done: `IStackDefinition` (id, name, icon, stack size, slot tint) for
       recipe output, station output and saves. Giving stays with each inventory (bag, essence storage, material pool).
       Was: "Is it an ItemDefinition or a ResourceDefinition?" is asked in
       StationCrafting (deliver output), ResourceSource (harvest), SaveMapper (output id), ItemTooltipView,
       StationWindow (icon, tint, stack) and the quest/station names (now `IDisplayName`). One interface for what both
       share (id, name, icon, tint) and one place that gives an amount of either to someone (bag or material pool).
3. [x] **Monster checks outside AI.** Done: `ICombatSounds` on Monster/BuildingDefinition (reached through
       `IEventSubject`), `IStaggerable` (MonsterAI) for parries, and "a fight is on" from the hit's faction (a Neutral
       creature never fights back). CompanionLeader keeps MonsterAI: companions fight monsters, like the spawners. Was: AudioManager (death sound, "a fight is on"), CombatFeedback (hurt sound),
       PlayerGuard (parry stagger) and CompanionLeader ask for `MonsterAI`. Sounds come from the definition the
       object reports (`IEventSubject` + a small sounds interface, like BuildingObject already has); stagger is an
       `IStaggerable`, so a companion or a future NPC can be staggered too.
4. [x] **UI binds to the player, not to whatever is found.** Done: the windows take `Player.Current` (MessageFeedView
       also rebinds on `Player.Spawned`, like PlayerVitalsView); `PlayerCameraController.Current` replaces the Finds. Was: CharacterWindow, ItemInventoryWindow, MessageFeedView,
       ResourceInventoryPanel and SkillWindow fall back to `FindAnyObjectByType<ItemInventory>()` and the like, which
       can pick a chest's inventory. Bind to `Player.Current` (and rebind when the player arrives). The
       `PlayerCameraController` is looked up with Find in five places (DialogueBox, NokkenEncounter twice,
       ArcheryCameraAim, StoryNodes): one owner exposes it.
5. [x] **One rule for spawned encounters and saving.** Done: SaveableEntity leaves copies without an id unsaved (no
       warning; Level Check reports scene objects missing one); TransientSpawn is gone. Was: `TransientSpawn` strips SaveableEntity from copies at runtime,
       and the prologue rejects saveable enemy prefabs in validation. Let SaveableEntity decide: a copy made while
       playing that wasn't spawned through `SaveableEntity.Spawn` is simply not persistent (no warning, nothing to
       strip).
6. [x] **BuildPlacer (981 lines)** Done (763 left): `BuildGhost` for the preview, and `IBuildArea` replaces the
       nine "prologue or Portal?" branches (HomePortal and PrologueDirector are build areas). Was: holds the build menu pages, the placement rules (grid, snap, lines, CheckSpot)
       and the ghost visuals. Split the menu and the ghost out; the rules become testable without a scene.

Fine as they are: the six `Instance` singletons (AudioManager, CombatFeedback, GameStateManager, UIStateManager,
ExperienceTracker, QuestManager) are scene services with one owner each; spawners configuring `MonsterAI` directly
(NightProwlers, WardBreachDirector, PrologueDirector) are spawning monsters, so knowing the type is their job; raid
generation's Find calls run once per raid build.
