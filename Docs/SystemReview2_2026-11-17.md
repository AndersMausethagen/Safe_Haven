# System review 2, 2026-11-17

Read-only review (the user: "send one agent to check code for bugs in the same method as earlier"). Scope, in order:
what the first pass (`Docs/SystemReview_2026-11-17.md`) didn't reach (extraction, lockpicking, the raid report and results
screen, forest annoyance, recipes and quests beyond references, loot on a co-op friend's copy), then today's unreviewed code
(arena fall, collapsing ground, the turn-back, standing tolls, `RaidSpots.IsDryEnough`, `PlayerGlow`, the fog's view column,
path feather/fray and the verge), then data consistency. The ground light field was out of scope (another agent).

Each finding: id, severity (High: breaks play, loses progress, desyncs co-op, crashes; Medium: wrong numbers, wrong text, a
feature that silently does nothing; Low: edge cases), Confirmed (traced) or Likely (strong evidence, not fully traced),
file:line, problem, failure scenario, suggested fix.

## Summary

No High findings. The extraction, lockpicking, report and results-screen code held up on a full read; most of what turned up sits where
a friend's game (co-op) meets systems written for one player, plus a few small slips in today's code:

- **A co-op friend's raid is quieter than the host's** (S1, S8): the forest's wrath wave is made and destroyed in the same frame on a
  friend's game (it never asks the host, as every other raid spawn does), its hunts move no creature, and a friend's lit beacon calls
  none of the host's creatures, because only the player's own noise (`PlayerNoise`) is sent to the host.
- **A friend's own matters read the host's Haven on a followed raid** (S5): the raid summary's "who at home could use this", the caches'
  forge cap and the skills' mentor boost use `GameSession.Here`, which on a followed raid is still the host's.
- **Today's path edges** (S4): the grassy verge peaks wherever the path layer is half the paint, which with the new 2 m feather and the
  leaf drift is also the middle of side paths and of leafy stretches of trail (worked out from the data, not seen).
- Smaller: a wrath is followed at once by "The forest is angry" and a second hunt (S2); a falling span's dust would appear at the world's
  origin once dust is assigned (S3); a companion who never walked in is reported as home (S6); the HUD shows no progress while the
  arrival stone is held (S7).

Reviewed and found sound: `ExtractionPoint`/`ExtractionHold`/`ExtractionWindow` (windows, dousing, contested holds, sheltering at zero,
copies for large maps, `ChooseExtractions`), `RaidDirector.TurnBack` (guards, save, co-op, no report by design; encounters bring no horse
or cart), `LockSession`/`PlayerLockpicking`, `RaidReport.Create` and the results screen, `ArenaFall`, the standing tolls
(`Stand`/`MeetStanding`, the maps' marks), `RaidSpots.IsDryEnough` (depth window, decks, rivers' centre lines), `PlayerGlow`, the fog's
view column, loot on a friend's copy (`CreaturePuppets.OnDied`'s killer, `CreatureHost.OnHit`, elites), and the data checks below.

Count: High 0, Medium 3 (S1, S5, S8), Low 5 (S2, S3, S4, S6, S7).

## Forest annoyance

### S1. The forest's wrath and its hunts do nothing in a co-op friend's game (Medium, Confirmed)

`Gathering/ForestAnnoyanceDriver.cs:110-121` (`OnWrath`) calls `RoamingSpawner.SpawnWave`, which goes straight to `TrySpawn` →
`Instantiate(prefab, ...)` (`Raids/RoamingSpawner.cs:115-131`, `:244`). Unlike the spawner's own `Update` (`:61`, returns while
`CreatureSource.Remote != null`) and every other raid spawn path (`SpawnCreaturesOutcome` at `Events/RaidEventOutcomes.cs:197-200`,
`RaidCreatures.Wake` at `RaidCreatures.cs:37-40`), the wave never asks the host. On a friend's game following the host, any creature
made locally is destroyed in its own `Awake` (`AI/MonsterAI.cs:371-374` → `MonsterAI.Remote.cs:73-82`, `SuppressedByHost`). The Angry
hunts (`Hunt`, `:125-148`) only wake and send this game's creatures (`CreatureSleep.WakeWithin`, `GatheringNoise.Investigate`,
`RoamingSpawner.HuntNow` over its own `roots`), which on a friend's game are the host's puppets or nothing.

Scenario: host and friend in the Misty Woods; the friend chops and mines loudly. Their meter climbs through Stirring and Angry
("Something is hunting you") to "The forest's wrath is upon you!", and nothing comes: the wave's creatures are made and destroyed in
the same frame, and the hunts move no creature. Gathering costs a friend nothing that it costs the host. (The wave's empty roots also
stay in `roots` and `AliveCount`, harmless only because `Update` returns early there.)

Fix: in `TrySpawn`, when `CreatureSource.Remote != null`, choose the spot as now and ask the host
(`CreatureSource.Remote.Spawn(prefab, point, yaw, hunt: true)`) instead of instantiating, as `SpawnCreaturesOutcome` does; for the
Angry hunts, a remote "hunt round this player" request (or let the host's game send its roamers when a friend's annoyance is Angry).
A `CopyRuleTests`-style test: a wrath raised with a remote source asks it for `WrathGroups` packs.

### S2. A wrath is followed at once by "The forest is angry" and a second hunt (Low, Confirmed)

`Gathering/ForestAnnoyance.cs:126-145`: on reaching the wrath threshold, `Add` sets `Value = AngryAt`, raises `LevelChanged(Wrath)`,
then `Wrath`, then `UpdateLevel()` raises `LevelChanged(Angry)`. The driver's `OnLevelChanged(Angry)` (`ForestAnnoyanceDriver.cs:100-104`)
posts "The forest is angry. Something is hunting you." and sets `nextHunt = Time.time`, so the next `Update` hunts again right after
`OnWrath`'s own `Hunt()`.

Scenario: a player already Angry lands the swing that tips it over: "The forest's wrath is upon you!" and, in the same moment, "The
forest is angry. Something is hunting you." (a step down read as a new warning), and every roamer is re-sent twice in two frames.

Fix: in `OnLevelChanged`, skip the Angry message and the hunt reset when the previous level was Wrath (keep the last level in the
driver), or have `Add` set `Level = Angry` silently after a wrath.

## Boss encounters (today's parts)

### S3. A falling piece's dust lands at the world's origin (Low, Likely)

`Raids/Encounters/CollapsingGround.cs:132-155`: `Fall` disables every collider of the piece (`:136-139`), then calls `Burst(piece)`
(`:150`), which places the dust at `BoundsOf(piece).center` (`:170-176`). `BoundsOf` reads `collider.bounds` first (`:185-197`), and a
disabled collider's bounds are empty at (0, 0, 0) (Unity: "an empty bounding box if the collider is disabled"), so the encapsulated
bounds are the origin. The crack's burst (`Crack`, `:116`) runs before the colliders go off and is placed right.

Scenario: once `dust` is assigned (it is empty in `Encounter_BrukamsBridge.unity` today, so nothing shows yet), each span's fall puts its
dust cloud at the world's corner (0, 0, 0), far off the bridge at x 80, z 62-102, instead of where the span drops.

Fix: keep the `Bounds` computed at the top of `Fall` and pass its centre to `Burst` (`Burst(Vector3 at)`), or call `Burst` before the
colliders are disabled.

## Map surface and growth (today's path edges)

### S4. The grassy verge grows down the middle of side paths and of leaf-strewn stretches of trail (Low, Likely)

`Environment/TerrainGrowth.cs:142-148` adds `verge · 4w(1−w)` for a layer's weight `w`, peaking at w = 0.5 wherever that layer is half
of the paint, whatever the other half is. `Raids/Map/RaidMapSurface.cs` makes the path layer half of the paint in two places that are
not its edge:

- Leaf drift (`:89-101`): on every trail, in 3.5 m patches, `weights[..,1] = rest·soil·(1−drift)` with drift up to `PathLitter` 0.5, the
  rest going to the litter layer. A trail's centre with drift 0.4 is path w ≈ 0.6.
- The wider feather (`:60`, `PathFeather` 2 m on the Woods and the valley since 41ba40aa, 2.2 m on the marsh): two box blurs of ~2 m on
  a path strength that already ramps over ±1.5 m round the edge (`RaidLayoutField.Ground.cs:230`, `SoilEdge` 1.5). A spur is
  `TrailHalfWidth 2.6 × SpurWidth 0.6 × mood 0.85-1.3` = 1.3-2.0 m half-wide (`RaidLayoutField.cs:18,80-88`): its centre comes out
  at soil ≈ 0.6 (about 0.8 with the old 0.9 m feather).

Worked through for the Woods and the valley (path row: verge 0.6, bareAt 0.8; `BareFrom` 0.7 → room falls only from w 0.56 to 0.8):
at w = 0.6 the verge adds 0.6·4·0.6·0.4 = 0.58 short grass (and 0.35 tall) with room ≈ 0.93, about as much as the forest floor's own
0.6 (Woods) / 0.65 (valley). So side paths read as grassy strips along their whole width, and main trails carry grass tufts in their
middle wherever leaves drift; the marsh less (bareAt 0.7 removes more). Not seen in a launched raid: arithmetic from the data.

Fix: make the verge an edge of the path against the floor only, e.g. `verge · 4 · w_path · w_floor / (w_path + w_floor)²` with the
forest (and litter-off-path) layer as the floor, or compute it in `RaidMapSurface` from the soil before drift (a verge mask passed to
growth, or a separate "verge" weight); and keep a spur's centre bare (scale the feather by the trail's half width, or floor the blurred
soil at `soil_raw · 0.85` inside the trail). Then look at a Woods spur and a leafy trail in a launched raid.

## Raid's end and loot in co-op

### S5. On a raid followed with the host, a guest's own summary, forge cap and skill boost read the host's Haven (Medium, Confirmed)

While a guest follows the host, the visit goes on through the raid: `HavenVisit.IsVisiting` stays true (`Multiplayer/HavenVisit.cs:29-32`,
`InHostsHaven` is false only because a raid is active; `EncounterArena.FollowsAHost` relies on it), so `GameSession.Here` is still the
host's copy and the guest's own Haven is `GameSession.Home` (`HavenVisit.OwnHaven`, `Core/GameSession.cs:56-70`). Three reads that are
about the guest's own Haven use `Here`:

- `Raids/RaidDirector.cs:533-534`: `HavenAnswers.Fill(Report, ..., GameSession.Here)`. Every answer in the guest's raid summary is worked
  out against the host's Haven (`Settlement/HavenAnswers.cs:118-267`: `StationKnown`, `Available` by `BuiltTier`, `PersonIsHome` by
  `settlement.Find`, `IsFallen`, the Mender's `JobBonus`).
- `Raids/LootCache.cs:187`: the forge cap `IsAboveThePlayer(drop.Item, GameSession.Here.SmithyTier)`, which its doc calls "the player's"
  Smithy.
- `Progression/ExperienceTracker.cs:197`: `Boost` reads the mentor building's tier from `Here` ("its mentor building's tiers at home,
  SettlementState remembers them on raids too"), so every skill's experience on the raid is boosted by the host's buildings.

Scenario: a guest whose own Haven has no Smithy and no Bjarne follows a host who has both into Troll Valley and extracts with iron ore.
Their summary says nothing about the ore (the host's Smithy "uses" it), where it should say "Bring Bjarne home: the Smith knows how";
a worn axe shows no Mender line because the host has one at work. Caches give the guest gear a tier above what their own forge allows,
and their Mining experience is boosted by the host's mine. A guest further along than the host gets the reverse.

Fix: `GameSession.Home` in all three (`HavenAnswers.Fill(..., GameSession.Home)`, `GameSession.Home.SmithyTier`, `GameSession.Home.BuiltTier`
in `Boost`; Home is a `SettlementState`, an `IHavenView`). Whether the mentor boost should follow the Haven one stands in while visiting
the host's Haven itself is a design call; on a raid it is one's own. A `CoopHomeTests` case: a raid followed as a guest fills the
summary from the guest's Haven.

### S6. A companion who never walked in is reported as having "made it home" (Low, Confirmed)

`Raids/RaidCompanions.cs:29-50`: `BringCompanion` returns without a companion when there is no NavMesh within 8 m of the spot beside
the player ("Nowhere for X to stand; they stayed at home.", a log warning only), or when the raid ends before `BeginRaid` ran. `Settle`
(`:87-108`) still sets `report.Companion = session.Companion` and, with `Companion == null`, neither loses nor counts them;
`UI/RaidReportText.cs:36-41` then prints "X made it home." for someone who never left.

Scenario: the player sets out with Gunnar from a start point whose NavMesh sample fails (a set-piece start on a rock, a narrow ledge). No
Gunnar appears and the player is told nothing; the summary says "Gunnar made it home."

Fix: post the "stayed at home" line to the player (GameMessages) when he doesn't walk in, and in `Settle` set `report.Companion` only when
`Companion != null` (or give the report a "stayed at home" line).

## Boss encounters: the HUD

### S7. Holding the arrival stone to turn back shows no progress in the HUD (Low, Confirmed)

`UI/RaidHudView.cs:148-149`: in an encounter the way-out line is `DescribeEncounter(arena)` whenever the boss hasn't fallen, and the arrow
target stays null, so `DescribeExtraction` (the only place that writes "Holding X... 40% (5 s)", `:347-371`) never runs before the gate.
`DescribeEncounter` (`:331-340`) shows the arrival line ("Beyond the mist: the Brukam's bridge.") before the gate.

Scenario: at Brukam's Bridge the player decides not to fight and lights the Arrival Stone: the HUD keeps saying "Beyond the mist: the
Brukam's bridge." for the whole hold; only the ring on the ground and the "Back into the circle!" warnings say anything is happening.

Fix: in `Update`, describe a lit extraction first (`ExtractionPoint.Active` has one `IsLit`) before falling back to `DescribeEncounter`,
e.g. `arena != null && !arena.BossFallen && !AnyLit() ? DescribeEncounter(arena) : DescribeExtraction(out target)`.

## Extraction in co-op

### S8. A friend's lit beacon calls none of the host's creatures (Medium, Confirmed)

`Raids/ExtractionPoint.cs:206-210`: while lit, the beacon calls every `callInterval` with `WorldNoise.Emit(transform.position, callRadius)`
("The forest will come"). `WorldNoise.Emit` (`AI/Perception/WorldNoise.cs:16-29`) only reaches this game's `MonsterAI.Active`. The one way a
sound goes from a friend's game to the host's creatures is `PlayerNoise.Made` → `NetworkPlayerAvatar.OnOwnNoise` → `NoiseRpc`
(`Multiplayer/NetworkPlayerAvatar.cs:270-322`, `Player/PlayerNoise.cs:93-99`); the beacon's call doesn't go through `PlayerNoise`, and nothing
else forwards `WorldNoise`. On a friend's game following the host every creature is the host's puppet, run by the host's game, so the call
moves none of them. (The beacon's alarm group is forwarded: `RaidCreatures.Wake` → `CreatureSource.Remote.Wake`.)

Scenario: host and friend each light a beacon in the Misty Woods, far apart. Creatures within 35 m of the host's circle come to look every
5 s; the friend's circle draws nothing but what happens to walk by, so a friend's extraction is a quiet wait where the host's is a fight.
The same holds for the forest's hunts (S1) and an event's "Make Noise" outcome (`Raids/Events/RaidEventOutcomes.cs:243`) in a friend's game.

Fix: a world sound this game makes on a friend's run should also go to the host: e.g. a `CoopNoise.Emit(position, radius)` used by the
beacon and `MakeNoiseOutcome`, which calls `WorldNoise.Emit` and, when `CreatureSource.Remote != null`, sends it through the same
`NoiseRpc` (it already rate-limits and checks the place). Not `PlayerNoise.Make`: that would fill the player's stealth meter.

## Data consistency (brief)

Scripted over the YAML, GUIDs resolved through the .meta files. Clean:

- Monsters: all 31 definitions have their own stats asset (no two share one; the Tusse has `TusseStats`), and each prefab's Combatant
  `baseStats`, LootOnDeath `table` and MonsterAI `definition` match its definition (variants read through their overrides:
  BarrowShieldbearer on the Draug prefab, the Boar's ExpeditionBoar).
- Brukam's Bridge: unlocked by the Heartroot as Troll Valley is, and its boss unlocks the Moss Marsh (whose locked text names the Brukam);
  the Brukam's table matches its tier briefings (iron at II only, moonsilver from III, Trollstone from IV, each within that tier's DropCap
  3/4/5); `layoutRules` is empty, so no Large option is offered; its `largeRaid.extraExits` and `roaming` are unused (no systems).
- Troll Valley no longer lists the Brukam (known enemies, likely items) and its tier III briefing points to his own bridge.
- Every rumour's event has a site prefab (`RaidRumourDirector` takes the last pinned site as the one just placed).
- Recipes: no zero amounts; the only output-in-its-own-cost recipes are the seven Farm recipes (seed to crop, by design); no item that
  breaks down is also crafted.
- Quests: every objective with a target names an existing asset; descriptions agree with their targets. The AssignJob objectives name a
  person in their text but count the job only (`person` empty), which matters only if another resident can take that job.

## Not reached

- `RaidTerrainEvents` beyond the rockfall, `RaidCompanions` beyond its report line (S6), the planner's geometry files.
- Whether the Brukam's Bridge terrain leaves a strip of river outside the `River Fall` trigger (x 42-118, z 63-105, y 17-25; the river
  sheet spans z 60-108): a player who walks down a bank there instead of falling from the bridge may not be caught. Needs the terrain's
  heights or a look in the scene.
- A guest who steps through their own gate after the host's fight has already dropped the spans lands on the near bank with the bridge
  gone (each game collapses from its copy's phases): fine if the river catch covers it (above), not checked in play.
- The fog's view column in cinematics and on other cameras (it always aims at `Player.Current`); looked plausible, not seen.
- Recipes and quests as balance (costs, rewards, pacing): only structure was checked.
