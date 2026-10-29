# Safe Haven – TODO

Open work and ideas. Tick items off (`[x]`) or delete them when done. Architecture notes for each system live in
[ARCHITECTURE.md](../Assets/_Project/Scripts/ARCHITECTURE.md).

## Raid maps (Misty Woods forest terrain)

### Art
- [ ] Replace the placeholder forest kit (spruces, pine, dead spruce, boulders, log in `Assets/_Project/Art/Forest`)
      with real tree and rock art. Swap the prefab mesh/material, then "Raid Map" › Bake Map. No layout changes needed.

### Designer tools
- [ ] Add a "Raid Maps" page to the designer window: preview of the marker image over the height map, legend,
      site counts per group and a Bake button.

### Content
- [ ] Paint a second layout for Misty Woods (new Height/Mask/Markers PNGs + `RaidMapDefinition`). With the four
      rotations this gives eight variants.
- [ ] More event-site variety (new prefab in `Prefabs/Raids/Events` + an entry in `MistyWoods_Events`).
- [x] Dedicated wolf pack prefab for enemy camps (one howler + one or two packmates), with staggered attacks.

### Polish and fixes
- [ ] Ground event prefab children individually; pieces sit at local y = 0 and may float on slopes.
- [ ] Check that the Campfire in the Abandoned Camp event doesn't carry Safe Haven station/building logic into raids.

### Verification still to do
- [ ] Play-test the 180° and 270° map rotations (only 0° in the editor and 90° in play mode are verified).
- [ ] Play a full raid from the Safe Haven portal through the loading screen into the rotated map and back.

## Materials as items (weight and carrying)

- [ ] Soft encumbrance: above ~80% carry weight slow movement and raise stamina cost, instead of only a hard cap at 100%.
- [ ] Haul options for raids: a haul sack (extra weight, but slows you or makes noise), and later a pack animal, cart or wheelbarrow (GDD §12.4).
- [ ] Refine at home pays off: tune item value per weight so ingots/planks/blocks are clearly better to carry than raw ore/logs.
- [ ] Quick-deposit: "Deposit all materials" button at the stash, and stations pulling from storage (already via MaterialPool).
- [ ] Decide essence loss on a failed raid (e.g. lose the essences gathered this raid, keep banked ones).
- [ ] Ground drop prefabs for materials (a generic sack/bundle prefab) so dropping a material spawns something to pick up.
- [ ] Per-material stack size tuning after playtest (hides 20, trollstone 10, the rest 50).
- [ ] Show the weight of a full stack in the raid preparation window, so the loadout's weight is visible before leaving.

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

1. [ ] **One "by the player" rule.** `GameEvent.ByPlayer` (`Player.IsPlayer`; no actor counts as the player) and
       `ExperienceTracker.ByPlayer` (no actor does not count) disagree. Decide once, on the side (`Faction.IsPlayerSide`
       of the actor's Combatant), so a companion's kill counts for "kill 5 wolves" and a wolf burnt by nobody doesn't.
2. [ ] **Objective rules in one place.** `ObjectiveType` is switched on in `QuestObjective` (Progress, Describe,
       FindProblem), `QuestObjectiveDrawer` and `QuestManager.CountOwned`. Move CountOwned into the objective first;
       later consider one class per objective type (`[SerializeReference]`, like `StoryNode`) with an asset migration.
3. [ ] **Death subject without type checks.** `GameEvents.OnAnyDied` asks for MonsterAI, then BuildingObject; give the
       dying object a small interface for its definition (like `IInteractionName`).
4. [ ] **No Heart Tree inside the generic trigger.** `GameEventTrigger`'s PlayerInsideWard polls `HeartTree.Current`;
       an area interface (or a ward trigger component) keeps the trigger generic.
5. [ ] Smaller: `QuestObjective.Name` switches on Item/Resource definitions (a shared display-name interface);
       `QuestGiver.CanInteract` advances its waiting lines (a query with a side effect); HeartTree keeps its own talked
       event beside `Talkable`'s.
