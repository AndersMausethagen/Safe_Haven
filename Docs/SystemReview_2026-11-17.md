# System review, 2026-11-17

Read-only review (the user: "have one agent systematically going through systems and data to look for bugs and
inconsistencies, with suggestions on how to fix"). Scope: raids end to end and the map generator (bridges, tolls, fen),
loot and salvage, then data consistency across raids, loot tables, items, monsters, recipes and quests. Boss encounters
(`Raids/Encounters/**`, `AI/BossEncounter.cs`, WhenBoss, the encounter parts of the director/launcher/plan/planner/HUD)
were out of scope, and so was the uncommitted work in progress (the Brukam leaving his bridge in `TrollValley Layout.asset`,
the fen's damp ground in `RaidMap.Dress`, `RaidTimeOfDay.skyCover`, the Misty Woods elites' Trollstone row).

Each finding: id, severity (High: breaks play, loses progress, desyncs co-op, crashes; Medium: wrong numbers, wrong
text, a feature that silently does nothing; Low: edge cases), Confirmed (traced) or Likely (strong evidence, not fully
traced), file:line, problem, failure scenario, suggested fix.

## Summary

No High findings. The new raid code is mostly sound; the problems sit where new content goes through paths built for
something else:

- **Bridge tolls ride the rumour path** (R1): announced at raid start as if they happened, counted as rumours, "close by"
  from anywhere, logged in the report even when never visited; and a co-op friend gets no toll at all (R2).
- **The fen's water isn't known to spawning** (R5): event sites, roamers and event creatures are placed on the NavMesh under
  the Moss Marsh's water sheet (and the valley's river), since nothing asks whether a spot is under water.
- **Caches still apply the 11-02 Smithy cap** (R6), which now removes the loot overhaul's own tier-3 to 5 parts and junk
  (Barrow Goods, Grave Iron, Thick Hide, Offered Blade, Trollstone...) until the forge is upgraded, invisible in the Content
  Editor's grids.
- **Text that the data doesn't bear out** (R8): "Stone trolls stand on the bridges" (the toll is a small moss troll),
  "every way through the woods" in the valley, the Moss Marsh's dusk Nykk that doesn't exist yet.
- Smaller: the toll sits on whichever bank the bend favours (R4), the Tusse fights with the Small Moss Troll's stats asset
  (R9), salvage overflow at a friend's Workshop (R7), a cache saying "Nothing useful inside" after giving a relic (R3), and
  the Moss Marsh borrowing Troll Valley's art and the Woods' loading tips (R10).

Data checks that came out clean (scripted over the YAML, GUIDs resolved through the .meta files): every monster definition
and its prefab share one loot table and one definition; every raid's known enemies are spawned at an offered tier (in Troll
Valley each of the Woods' own creatures, Root Guardian, Moss Shaman, Marsh Light, Bog Spitter, is at most 3% of its creature
list, as decided; Rootlings come only from the Three Wishes event); no regional or elite loot row lies outside its raid's
offered tiers or is blocked by DropCap at every tier it rolls; every raid's likely items have a source in that raid; no
duplicate item ids (eight older items use dotted ids, `find.*` and `enchant.*`, consistently); the raids' unlocking bosses
are the BossDefinitions the encounters mark.

Count: High 0, Medium 5, Low 5.

## Raids

### R1. Bridge tolls are announced as rumours and as events that already happened (Medium, Confirmed)

`Raids/RaidDirector.cs:207-211` stands every toll with `RaidEventDirector.BeginAt`, the path built for a briefing's rumours.
`BeginAt` → `Place` (`Raids/Events/RaidEventDirector.cs:115-158`) then, at the moment the raid begins:

- posts the event's Warning and plays its warning sound once per toll (`:146-154`): "Something sits at the end of the
  bridge, chin on its fists, and watches you come." twice on a valley with two tolls, before anything is in view;
- writes "The Bridge Toll" into the raid's event log (`:156`, `RaidSession.RecordEvent`) and raises `GameEventKind.RaidEvent`
  (`:157`). The report (`UI/RaidReportText.cs:203-206`) and the journal (`Journal/JournalNotices.cs:193-195`) then list the
  toll as something that happened even when the player never went near a bridge;
- puts the site in `pinned`, which every HUD reads as "rumours": `UI/RaidHudView.cs:221-229` counts tolls into "N rumours
  are marked on your map", and `:233-237` posts "The Bridge Toll is close by" for every toll 3 s into the raid wherever the
  player is, because `RaidEventSite.IsFoundFrom` (`Raids/Events/RaidEventSite.cs:35-46`) returns true at once for a site
  whose beacon was never hidden (`beaconShowsWithin < 0`). The world map and minimap draw tolls with the rumour mark and
  label (`UI/WorldMapHud.cs:410-415`, `UI/MinimapHud.cs:409-416`).

Scenario: Troll Valley tier II, a seed with two tolls and one rumour. At start the player gets two identical toll warnings
and two warning sounds from far away, "3 rumours are marked on your map", and "The Bridge Toll is close by" twice while
standing at the valley mouth 150 m from either bridge. Extract without crossing a bridge: the report and the journal list
"The Bridge Toll" twice.

Fix: give standing sites their own list and entry (`RaidEventDirector.Stand(definition, position)`): no Warning, sound,
`RecordEvent` or game event until the player first comes near (or only on resolve, which `RaidEventSite.Choose` already
records), and not in `PinnedSites`, so the rumour hint and "is close by" read only rumours. If tolls should show on the map,
give them their own mark rather than the rumour mark.

### R2. A co-op friend's valley has no tolls (Medium, Confirmed)

`Raids/RaidDirector.cs:208` stands tolls only when `CreatureSource.Remote == null`, i.e. never in a friend's game following
the host. That guard was copied from the set-piece guards above it (creatures, which the host's game makes), but a toll is an
event site, and event sites are personal: `RaidEventDirector.Update` runs random events in every game, and rumours are
placed in every game (`RaidRumourDirector`). Walking through a toll already spawns through `CreatureSource.Remote`
(`SpawnCreaturesOutcome.Apply`, `Raids/Events/RaidEventOutcomes.cs:199-203`).

Scenario: host and friend on a Troll Valley run. The host sees a troll at each bridge and can pay, answer or fight; the
friend sees empty bridges, crosses freely and never gets the reveal, the time or the riddle's find. Troll Valley's one rule
does nothing for friends.

Fix: stand tolls in every game (drop the `CreatureSource.Remote` condition for the tolls only; keep it for the guards).

### R3. A cache says "Nothing useful inside" right after giving a relic or an inscription (Low, Confirmed)

`Raids/LootCache.cs:96-117`: the relic and the inscription are granted first, then `contents`/`resourceContents` are checked
and "Nothing useful inside." is posted when both are empty.

Scenario: a Traveller's Pack whose rows all miss but which rolled a relic: "Found Norn's Bead" then "Nothing useful inside."

Fix: post the line only when the relic and the inscription weren't given either (track a `gaveSomething` flag).

## Map generator

### R4. The toll-taker sits on whichever bank the valley's bend favours, not the near one (Low, Confirmed)

`Raids/Map/RaidMapGenerator.Bridges.cs:87-91` picks the toll's end by `valley.ShareOf(a) <= valley.ShareOf(b)`, "the end
nearer the valley's mouth: the way a raid comes up". But `crossing.Direction` is square to the river's local tangent
(`RaidLayoutPlanner.Valley.cs:258-265`), so both ends sit at almost the same distance up the valley; their difference is
`2·(half+2.5)·Dot(Direction, Axis)`, whose sign is set only by which way the valley happens to bend at that crossing. Which
bank the trail comes from (`ShapeCrossings`' `fromBank`) is never consulted. The site is also turned to a random heading
(`RaidEventDirector.Place`, `:129`), so the troll "sitting across the end of the bridge" may sit with its back to it.

Scenario: on a stretch where the valley bends one way, the toll-taker sits on the far bank; a player coming up the valley
crosses the whole bridge unchallenged and meets the toll after crossing, so "pay, or ford the river elsewhere" means nothing
there.

Fix: store on `RaidLayoutCrossing` the end the trail enters from on the way up (the end on the bank of the trail endpoint
with the lower `ShareOf`), put the toll there, and face the site along the bridge.

### R5. Event sites, roamers and event creatures can be placed under the fen's water (Medium, Likely)

The Moss Marsh's water is one sheet with no collider (`Raids/Map/RaidMapGenerator.Water.cs:72-87`); the NavMesh is built on
the sunken ground beneath it (`RaidMap.RebuildNavMesh`; nothing in the raid code marks water as a NavMesh area). The things
placed during a raid look only for NavMesh, a path and being out of sight: `RaidSpots.TryFindSpot`
(`Raids/RaidSpots.cs:70-101`, used for every event site by `RaidEventDirector.Begin` and for rumours without a set spot),
`RoamingSpawner.TrySpawn` (`Raids/RoamingSpawner.cs:213-260`) and `SpawnCreaturesOutcome` (`RaidEventOutcomes.cs:187-210`).
The fen commit measured 47% of a Moss Marsh map under the sheet, up to 1.2 m deep (`MossMarsh Layout`: aboveWater 0.6,
sink 1.2); the fen tests check only trees, rocks and nodes (`RaidFenTests.TreesAndRocks_KeepOutOfTheWater`). The valley's
river (1.5-2 m deep) has the same gap, on a smaller share of the map.

Scenario: Moss Marsh tier III. The Traders' Wagon event looks for a site 35-70 m away; about half its tries land in open
water, so the wagon and its pillar of light often stand in a pool, and the wolves its choice raises appear and walk about
submerged. Roamers (wolves, bog spitters) likewise spawn and walk under the sheet. Not seen in play: the claim rests on the
NavMesh covering the sunken ground.

Fix: one "is this spot dry enough" check (`WaterSurface.TryFind(point + up, reach, out surface) && surface - point.y > ~0.4`)
in `RaidSpots.TryFindSpot`, `RoamingSpawner.TrySpawn` and `SpawnCreaturesOutcome`; or mark deep water as its own NavMesh area
that spawning excludes (and, later, the Marsh's own water creatures prefer). A fen test like the trees' for event sites.

## Loot and salvage

### R6. Caches drop the loot overhaul's raid parts and junk until the Smithy is upgraded (Medium, Confirmed)

`Raids/LootCache.cs:183-205`: every ladder item (category Material, Equipment, Tool) above `Max(2, SmithyTier + 1)` is
silently removed from a cache's roll, regional loot included. The rule dates from 2026-11-02 (adda2acc), for metal. The
overhaul since made its raid parts and junk finds Materials with tiers 3-5, so they now pass through the Smithy cap too,
which creature drops (`LootOnDeath`, DropCap only), the Content Editor's tier grids (`LootContentSection.Cell/RaidLines`) and
`LootTableTests` don't know about. A base Smithy is level 1 (`Construction.Level = 1`, `SettlementState.BuiltTier`), so the
cap is 2:

- Barrow Chest: Barrow Goods (t3, p0.60, its main row) and Grave Iron (t3) never come until Copper Fire;
- Hunter's Cache: Thick Hide (t3); Seter Chest and Smith's Chest: Broken Sickle (t3); Misty Woods Regional: Wisp Ember,
  Huldra Silk, Elderwood (t3);
- Drowned Hoard and Moss Marsh Regional: Offered Blade and Moonsilver Ore (t4) need Iron Fire, while the tier III briefing
  says "the drowned hoards hold old silver and offered blades";
- Troll Hoard and Troll Valley Regional: Trollstone (t5) needs Moon Fire.

Scenario: Misty Woods tier II with the starting Smithy: a Barrow Chest gives pennies and runestones but never the Barrow
Goods its table (and the Break Down page's own hint, `Durability/SalvageBench.cs:160`) name. Junk meant for the Workshop is
held back by the forge.

Fix: limit the cap to what it was for: `IsAboveThePlayer` only for items that aren't `RaidOnly` and are metal or gear (or
items made at the Smithy), or drop it now that `RaidDifficulty.DropCap` caps every source by tier. Whatever stays, model it
in `LootContentSection` so the grids show what really drops.

### R7. Salvage overflow at a friend's Workshop goes into the stash the game holds there (Low, Likely)

`Durability/Salvage.cs:99` always passes `toStash: true`. `SalvageBench` is `HavenUse.Anyone` ("breaks down the visitor's own
things"), and while visiting with stash rights `GameSession.Stash` holds the host's stash mirror
(`Multiplayer/VisitorAccess.cs:232`, changes collected and sent back at `:150/:205`). Quest rewards already guard this
(`Quests/QuestManager.cs:612`: `atHome = RaidDirector.Current == null && !HavenVisit.IsVisiting`).

Scenario: a guest with a full bag breaks down five Broken Sickles at the host's Workshop: the iron goes into the host's
stash (or, without stash rights, into a stash object the guest can't reach there).

Fix: `toStash: !HavenVisit.IsVisiting` (the ground beyond), as quest rewards do. `Player/PlayerEquipment.cs:378` has the same
pattern.

## Data consistency

### R8. Briefing and toll text that the data doesn't bear out (Medium, Confirmed)

- `Data/Raids/TrollValley.asset` tier III briefing: "Stone trolls stand on the bridges". Nothing puts a stone troll on a
  bridge: the toll site is the Small Moss Troll's visual (`Prefabs/Raids/Events/Sites/Toll Troll.prefab`, child
  `SmallMossTrollVisual`), and walking through it or a wrong riddle answer spawns Small Moss Trolls at every tier
  (`Data/Raids/Events/BridgeToll.asset`, SpawnCreaturesOutcome → `SmallMossTroll.prefab`). With the Brukam's bridge now
  empty, no bridge has a guard. Fix: a tier III+ toll that raises Stone Trolls (a second toll event, or the research's
  price-by-tier toll, `RaidIdentityResearch_2026-11-17.md` §3.1, with its own spawns), or change the line.
- `BridgeToll.asset` "Pay in bones" result: "points out every way through the woods", in a valley. Fix: "up the valley".
- `BridgeToll.asset` riddle answer gives Trollstone (t5) with the pennies; `GiveLootOutcome` drops it through
  `Session.Allows` (DropCap 3 and 4) at tiers II and III, so "tosses you something from its pile" is only pennies there. Fine
  if meant; otherwise a tier-appropriate find.
- `Data/Raids/MossMarsh.asset` Dusk: "A pale horse grazes on the shore, where no horse should be." The Nykk doesn't exist yet
  (a loot table only; no monster, prefab or event), so the briefing promises something the hour never brings. Fix: a dusk line
  about what is there (the dancing mist, the dead, the Toxic Knight) until the Nykk is built.

### R9. The Tusse fights with the Small Moss Troll's stats asset (Low, Likely)

`Data/Monsters/Tusse.asset` `combatStats` and `Prefabs/Enemies/Tusse.prefab` Combatant `baseStats` both point at
`Data/Combat/Stats/SmallMossTrollStats.asset` (84 health). It was made from the forest troll (b7374f6e) and never given its
own. `MonsterRoleTests.EveryMonster_FightsWithItsOwnStats` only checks the definition and prefab agree, so it passes. A small
thief that "fights only when cornered" has a troll's health, and any retune of the Small Moss Troll changes the Tusse too
(the same kind of slip as the four fixed on 11-16). It is the only stats asset two monsters share.

Fix: a `TusseStats` asset (lower health: it runs), and extend the test: no two monster definitions share a stats asset unless
listed as intended.

### R10. The Moss Marsh borrows Troll Valley's art and the Woods' loading tips (Low, Confirmed)

- `Resources/SceneTransitionSettings.asset` has destinations for `Raid_MistyWoods` and `Raid_TrollValley` but not
  `Raid_MossMarsh`, so its loading screen falls back to the generic art and the default tips (`Core/SceneTransitionSettings.cs`
  `ArtFor/TipsFor`), one of which is "The woods shift between visits".
- `Save/SaveSlotInfo.cs:69-83` `PlaceName` knows Troll Valley but not `Raid_MossMarsh` (raw scene name if ever shown;
  `RaidSceneRigTests` checks the valley's only).
- `Data/Raids/MossMarsh.asset:622-628`: `placeArtwork`, `extractedArtwork` and `failedArtwork` are Troll Valley's, so the
  planner shows the valley's painting for the marsh. Possibly waiting for the look pass; listed so it isn't forgotten.

Fix: a destination entry with marsh tips (and art when painted), a `PlaceName` case, own paintings; a convention test that
every raid's scene has a loading destination and a place name.

## Not reached

- Extraction (`ExtractionPoint`, `ExtractionHold`, `ExtractionZoneView`) beyond reading its status logic; it is being edited
  for the encounters.
- Lockpicking (`LockSession`, `PlayerLockpicking`), `RaidReport.Create`, `RaidCompanions` beyond the recent diff,
  `RaidTerrainEvents` beyond the rockfall diff, `ForestAnnoyance`.
- The planner's layout geometry beyond the valley and fen files (`RaidLayoutPlanner.Routes/Height/Rivers/Cliffs`,
  `RaidLayoutField.Ravines/Levels`, `RaidChasm`, `RaidRavineCrags`, `RaidMapSurface`), already pinned by golden tests.
- Recipes and quests as data, beyond checking that no quest names a raid or a raid id that doesn't exist
  (`ContentAudit` already covers broken references, unlearnable recipes and items nothing gives).
- Co-op specifics of loot on a friend's copy (`LootOnDeath`/`EliteCreature` killer on a puppet).
