# The raid loop: pacing, risk and reward

Research date: 2026-11-22. This is research only. No code, asset, scene or setting was changed, and Unity was not run.

**The problem.** Playtesters leave a 12-minute raid after about 4-5 minutes. That cuts raid income to about a third of the
design, and every economy number rests on raids (`Docs/EconomyAndPhases_2026-11-22.md` §0, §1.1, §1.3). The user said "raid
length: keep as is for now". So this document asks why players leave early, and what would make staying worth it. It does not
just change the timer.

**How sure each part is.**

| Kind | Where it comes from | How sure |
|---|---|---|
| How a raid works | Read from the code (`Scripts/Raids`) and the data (`Data/Raids`, `Resources`, the raid scenes) | Exact, as written. Not watched in Play mode |
| What players did | 17 raids in 6 human playtest logs since 2026-11-14 | Real, but a small sample: probably one player, all Misty Woods tier I, all early game |
| Why they did it | Inferred from the logs and the code | **Hypotheses.** The logs say what and when, not why. §5.4 says how to find out |
| Other games | Web sources, linked in §7 | Facts are cited. Anything marked "unverified" could not be confirmed |

---

## 0. The short answers

### What is going on

1. **The raid's job is done by minute 4.** In 10 of 17 raids the player freed someone to bring home. They left about 2 minutes
   later (median 127 s). The first raid's quest also says "reach the Western Beacon". Rumours are pinned from 0:00, on a map only
   200 m across.
2. **Staying doesn't pay where the money is.** The stage bonus (+25/50/80%) only reaches caches, events and Cursed Essence.
   Ore, wood, fibre, hides and creature parts pay the same at minute 11 as at minute 1. Enemies get +10/20/35% stronger.
3. **The HUD shows the risk every second and the reward once.** "At risk: N items" sits in warning colour all raid. The stage
   name is orange. The "+25%" appears in one message line and is gone.
4. **Players decided before the stage arrived.** In 5 of 7 extractions with a logged reveal, the player was already standing
   at the exit when "The Mist Stirs" began. The Glint cache it reveals (built 2026-11-19 to fix exactly this) came too late.
   Raids since it was built: median 266 s. Before it: 294 s. No change.
5. **Home is the early game's bottleneck.** Home time was 68-89%. The early bill is wood and stone, which come from home. A raid
   brings copper and a person; then the next blocker is back at home.
6. **Ruled out:** a full bag (no item was ever left behind; median haul 41 of 130 weight), nodes cleared (players mined about 2-4
   of ~11 copper nodes and saw about 38% of the map), and travel time (exits were 50-157 m from the start, about 30 s on foot).

### What to do (details in §5)

| # | Change | Effort |
|---|---|---|
| 1 | **Measure why**: new log rows, and a playtest-only "Why home?" picture card after each raid | S |
| 2 | **Rescues stop ending the raid**: a freed person can wait safely in an exit circle; later rescues call out when the mist stirs | M |
| 3 | **Stages pay on the main income**: +15/30/50% to node yields, and the stage bonus on creature drops | S |
| 4 | **Rich veins at the stages**: the first stage reveals 2 veins of the tier's ore, moved from the start layout (same totals) | S-M |
| 5 | **Show the upside**: a gold "×1.25" badge by the clock, the next stage's chest on a clock bar, seen veins on the minimap | S-M |
| 6 | **Late exits are kinder**: the hold shortens each stage, 30 → 24 → 18 → 12 s | S |
| 7 | **Away work pays for time away**: parties, outposts and gifts get up to ×2 for a 9-minute raid; today's yield stays the floor | S |

Not recommended: a longer or shorter clock now, penalties for leaving early (Tarkov's "run-through"), closing the always-open
exit, or a cost to extract early. Each breaks the user's "positive incentives, no hidden penalties" rule, and the evidence
points elsewhere.

---

## 1. How a raid plays now (as built)

All three raids (Misty Woods, Troll Valley, Moss Marsh) share this shape: 720 s, the same stage times, and a 200 m square map
(`RaidLayoutRules` › Map Size 200).

### 1.1 Start to end

| When (clock) | What happens | Where |
|---|---|---|
| **At the portal** | The briefing shows the hour (Day 55%, Dusk 25% with loot ×1.15, Night 20% with loot ×1.4 and 30% more creatures) and 2 rumours (3 with a Tracker). **Well Rested** starts: +10% health and +15% stamina, for 60 s per point of comfort at home | `RaidDefinition`, `RaidTimeOfDay`, `HavenReadiness.GiveRest` |
| **Clock length** | 720 s, +90 s per ward level, +120 s with Sigrid (Leader job). A **large raid**: 400 m map, +750 s (1,470 s) | `RaidDefinition`, `SettlementSettings` › Ward Levels, `Data/Settlement/Leader.asset` |
| **0:00** | The clock starts when loading ends. The layout is seeded: 7-10 creature points, **161-182 resource points**, 1-3 supply caches, 3-4 event sites, guarded set pieces, 1 elite (tier I), maybe one of the Lost. Every list uses distance band "Any": ore is as likely next to the start as at the edge | `RaidGenerator`, `MistyWoods.asset` spawn lists, `RaidElites` |
| **0:00** | Exits: **West Beacon** open all raid; **East Beacon** in 70% of raids, closes at 8:00 and sounds an alarm (2 hunting groups); **Hollow Oak** opens at 9:00 with a 0.6× hold (18 s). A large map adds three | `Raid_MistyWoods.unity`, `ExtractionPoint` |
| **1:00** | Roamers start: one every 20 s while the pacing director builds; at most 2 out at once (+1 per stage, +2 per alarm, cap 8, 12 a raid), scaled by density. After a peak of intensity, no roamers until it falls, then a 15-30 s breather | `RoamingSpawnSettings` (in the raid asset), `RaidIntensity` |
| **4:00** (480 s left) | **The Mist Stirs**: enemies +10% health and power; 2 points of Stirring Prowlers; loot +25%; a **Glint** cache 70-140 m away (×1.5 on top), gold on the minimap | `RaidStage`, `StageReveal` |
| **8:00** (240 s left) | **The Hunt**: +20%; Hunters that hunt the player; 60 s music hush; loot +50%; a **Hoard** (×1.8 on top) | the same |
| **10:00-11:50** | Warnings at 2:00, 1:00, 0:30 and 0:10 left | `RaidDirector.warningTimes` |
| **11:00** (60 s left) | **Collapse**: +35%; Collapse Hunters; loot +80% | the same |
| **12:00** | The raid collapses (a failure, like death), unless the player stands in an open circle: then the clock holds at zero | `RaidDirector.Update`, `RaidSession.Tick` |

**Events cost clock time.** The careful choice at many event sites costs 20-60 s (the Overturned Wagon 30 s, Cries from the
Ditch 45 s, the Blighted Tree 30 s, the Beacon Light 20 s, the Living Stones 45-60 s, the Jaw-keeper 45 s, Bjarne's Forge 45 s,
the Deep Patch 30 s). Stages and exits run on *time left*, so each such choice brings every stage and every exit change earlier by
that much. Which choices players took is not logged.

### 1.2 The pressure curve

| Clock | 0-1:00 | 1:00-4:00 | 4:00-8:00 | 8:00-11:00 | 11:00-12:00 |
|---|---|---|---|---|---|
| Enemy strength (tier I, day) | ×1.00 | ×1.00 | ×1.10 | ×1.20 | ×1.35 |
| Roamers out at once (before density) | 0 | 2 | 3 | 4 | 5 |
| Extra groups woken | – | – | 2 points (wander) | 1-2 points (hunt you) | 2 points (hunt you) |
| Exits open | West (+East) | West (+East) | West (+East) | West, Hollow Oak from 9:00 | West, Hollow Oak |
| Night adds | +10% health, 30% more creatures, all raid | | | | |

### 1.3 The reward curve

| Source | Over time | Scaled by the stage bonus? |
|---|---|---|
| **Ore, wood, fibre, herbs** (the main income) | All placed at 0:00. Nodes **regrow** in the raid as at home: copper and wood after 180 s, iron 360 s, moonsilver 600 s (`Data/Gathering`). Not verified in a launched raid | **No** |
| **Creature drops** (hides, parts, bone) | Roamers add more creatures over time; stage groups too | **No** (tier loot only, `LootOnDeath`) |
| **Cursed Essence** | With kills | Yes (×stage, ×night) |
| **Supply caches** (1-3 a raid) | Fixed at 0:00 | Yes, when opened |
| **Event rewards** (3-4 sites) | Fixed at 0:00 | Yes |
| **Glint** (4:00) and **Hoard** (8:00) | One cache each | Yes: the Glint pays 1.25 × 1.5 ≈ 1.9 times a cache, the Hoard 1.5 × 1.8 = 2.7 times |
| **Elites, rumours, rescues** | All present from 0:00 | – |
| **Raid bonus experience** | Builds as you play; lost if the raid fails | – |

In short: **the danger rises for everything, but the reward rises only for a side slice.** Most of what the economy counts (ore,
hides, wood) pays the same at minute 11 as at minute 1.

### 1.4 Getting out

- **Light, then hold:** one press lights a beacon, then the player holds a 5 m circle for 30 s (Misty Woods). A creature in the
  circle halves the speed. Leaving pauses it. A lit beacon calls creatures within 35 m every 5 s.
- **No cost:** the West Beacon is free and open from 0:00 to the end. Nothing about a later exit is better, except the Hollow
  Oak's 18 s hold in the last 3 minutes.
- **In practice** the 30 s hold took about 55 s (median of 10 extractions, from Position rows: last time more than 12 m from the
  exit to the end), because the beacon draws fights.
- Ward steps "Bound" and "Moonlit" make extraction 20% faster and faster again.

### 1.5 Dying, running out of time, and what is kept

| Kept | Lost |
|---|---|
| Knowledge, recipes, skills, base experience | Everything in the bag: materials, consumables, gear found on the raid |
| The **safe pocket**: 1 slot (one whole stack, so up to 50 ore), +1 at "Steadied", +1 at "Moonlit", +1 for a ring of Secure lands | Unextracted Cursed Essence |
| Gear worn or carried in, at **−25% durability** (4 failures to break it; boss encounters 10%) | The raid's bonus experience ("The raid's extra lessons were lost with it") |
| | A rescued person who fell stays out there, waiting at the same place next raid |

One of the Lost can take the most valuable lost item, to be won back later. Quitting mid-raid counts as a fall. A crash puts the
player back at the portal as they set out.

**Carrying.** The bag has 16 slots and 90 weight; the Hide Rucksack (from the prologue) adds 16 slots and 40 weight: **32 slots,
130 weight**. Copper ore weighs 2 and stacks to 50.

### 1.6 What the HUD says

- The clock, an arrow to the nearest open exit, and the stage's name in orange.
- **"At risk: N items (value) · M materials"** in the warning colour, recounted every second (`RaidHudView.DescribeRisk`).
- The minimap marks exits, rumours, events and the gold reveal. It does **not** mark ore.
- The stage's loot bonus is only in the stage's message ("The woods give more to those who stay: loot +25%"). It is not on the
  HUD (`RaidDirector.StageLoot` is read only by `LootCache`, `RaidEventOutcomes` and `LootOnDeath`).

---

## 2. What the playtests show

### 2.1 Which logs are real

There are 1,783 CSVs. Most are editor or agent probes. A run counts as human here when it:

- lasts 20 minutes or more, over one or more sessions;
- starts a new game in the prologue or the Haven, and doesn't enter a raid in its first minute;
- has no "gear 0" `RaidTier` rows (the probes' mark).

| Run (file) | Length | Raids | Notes |
|---|---|---|---|
| 20261114-122242 | 67 min | 1 | 1 cheat row (not in the raid) |
| 20261114-232848 | 63 min | 2 | |
| 20261115-184337 | 49 min | 2 | 13 cheat rows, none in a raid |
| 20261119-184435 | 84 min | 4 | Position rows from here on |
| 20261120-214517 | 82 min | 3 | |
| 20261121-151353 | 165 min | 5 | Sessions on 11-21 and 11-22. Last written 15:05:29, matching `Player-prev.log` (15:05:33) of the build `Safe Haven Builds/Safe Haven 2026-11-22`: a build run |

**17 raids, all Misty Woods tier I, all before the Smithy and the first ward**, so all on the plain 720 s clock (one was large:
1,470 s). Probably one player. No co-op raids, no Troll Valley or Moss Marsh, no companion along, no Sigrid.

### 2.2 Every raid

"Clock" is the raid's own seconds (`RaidFinished` amount). "At the exit when stage 1 began" uses Position rows (from 11-19).

| # | Run | Clock | Share of clock | Outcome | Hour | Rescue | Copper ore | Haul weight | At the exit when stage 1 began? |
|---|---|---|---|---|---|---|---|---|---|
| 1 | 11-14 | 252 s | 35% | **Died** | Day | Ingrid | 11 | 40 | (no positions) |
| 2 | 11-14 | 294 s | 41% | Extracted | Day | Ingrid | 0 | 13 | |
| 3 | 11-14 | 320 s | 44% | Extracted | Day | – | 14 | 68 | |
| 4 | 11-15 | 201 s | 28% | Extracted | Dusk | Ingrid | 0 | 12 | |
| 5 | 11-15 | 337 s | 47% | Extracted | Day | – | 23 | 83 | |
| 6 | 11-19 | 273 s | 38% | **Died** | Day | Ingrid | 0 | 17 | 18 m from where they fell |
| 7 | 11-19 | 652 s | 44% of 1,470 | Extracted | Day | – (large raid) | 14 | 57 | (no stage reached) |
| 8 | 11-19 | 108 s | 15% | Extracted | Night | Ingrid (again: she fell in #6) | 0 | 3 | |
| 9 | 11-19 | 259 s | 36% | Extracted | Day | – | 14 | 41 | **yes, 11 m** |
| 10 | 11-20 | 274 s | 38% | Extracted | Day | Ingrid | 24 | 69 | **yes, 5 m** |
| 11 | 11-20 | 229 s | 32% | Extracted | Dusk | Bjarne | 0 | 10 | **yes, 3 m** |
| 12 | 11-20 | 244 s | 34% | Extracted | Day | Tor | 0 | 17 | **yes, 3 m** |
| 13 | 11-21 | 259 s | 36% | Extracted | Day | Ingrid | 11 | 41 | **yes, 0 m** |
| 14 | 11-21 | 209 s | 29% | **Died** | Night | – | 14 | 62 | (no reveal logged) |
| 15 | 11-21 | 283 s | 39% | Extracted | Night | – | 13 | 58 | (no reveal logged) |
| 16 | 11-22 | 288 s | 40% | Extracted | Dusk | Bjarne | 13 | 71 | no, 47 m |
| 17 | 11-22 | 409 s | 57% | Extracted | Day | – | 6 | 41 | no, 53 m |

### 2.3 What the numbers say

**When they left.**

- Median clock at the end: **273 s, 38% of the 720**. 15 of 17 ended before 340 s.
- No raid reached The Hunt (8:00). No raid ran out of time.
- **Rescue raids** (10): median 256 s. **Other raids** (7): median 320 s.
- From freeing someone to the end of the raid: median **127 s** (77-241 s, wall time, including the hold).
- In the 7 extractions with a logged reveal, 5 players already stood at their exit (0-11 m) when The Mist Stirs began. Those 5
  ended 19-51 s later, which is about one hold. The other two were about 50 m away and stayed 120-159 s more.
- Whether anyone opened a Glint cache is not logged. It was placed 73-133 m away.
- The October runs, when the clock was 300 s, show the same habit: 10 raids of 85-175 s (median 139 s, 46% of the clock). When
  the clock grew 2.4 times, time spent grew about 2 times. **What is in the raid sets its length, not the clock.**

**How far they went.**

- Exits stood 50-157 m from the start (7 of 9 at 130-157 m). Paths were 245-919 m long (the large raid's 1,451 m).
- Within 30 m of their path (a guess at sight in the mist), players saw **32-48% of the map** before leaving (median 38%).

**What they carried.**

- Copper ore: 0 in 6 raids, **all six of them rescues**; otherwise 6-24 (median 14). Each copper node gave 5-7 here, so that is
  **2-4 nodes**.
- One probe counted **11 copper nodes** standing in Misty Woods I (`RaidNodes`, 2026-11-22). So players mined about **20-35% of
  the copper nodes**, and the nodes regrow after 3 minutes anyway. (The Troll Valley probe's count, 1 copper and 2 iron, looks taken
  before the map was done: it counts 8 s in.)
- Haul weight: 3-83, median 41, of 130. New stacks: 4-21 of 32. **No `LeftBehind` row in any raid**: the bag never refused a
  pickup.
- Pickups went on until the walk to the exit (for example 7, 10, 15, 6 and 11 pickup rows in minutes 1-5 of raid 16). Nothing
  ran dry.

**Danger.**

- Blows that took 5% of health or more: 0-12 a raid. Intensity peaks: 0-3 a raid, the first at about 1-3.5 minutes.
- **3 deaths in 17 raids (18%), all between 209 and 273 s.** Killers: a Bog Spitter, an Old Moss Troll (one 89-damage blow, 60%
  of health) and one not recorded. They lost hauls of 40, 17 and 62 weight, with 11 and 14 copper ore in two of them.
- Well Rested was on at every start and gone at every end (Kit rows, all 12 raids since 11-19). It ran out mid-raid each time.

**Home.**

- Home took 68-89% of play after the prologue (`EconomyAndPhases` §0).
- In the build run (11-21/22): stays at home of 11, 16 and about 68 minutes between raids, spent building the Altar, fences, the Stable
  and the Cauldron. Then two raids 2 minutes apart, when a quest ("Bring Bjarne Home") needed a raid.

### 2.4 Limits of this evidence

- 17 raids, probably one experienced player who knows the game well. Patterns could be that player's habits.
- All early game, tier I, Misty Woods. Nothing here says how mid-game raids go, with iron needs, the ward's longer clock or the
  Keeper's view.
- The logs show what and when. They don't show event choices, what was in the safe pocket, whether a Glint was opened, or why.

---

## 3. Why players leave early

| Candidate cause | Evidence | Verdict |
|---|---|---|
| **The raid's job is done early** (a rescue, a quest's beacon, rumours) | 10 of 17 raids had a rescue; they ended about 2 minutes after freeing (median 127 s); rescue raids were shorter (256 s against 320 s). The first raid's quest "Echoes in the Wilds" ends at the Western Beacon. A freed person who falls stays out there, so an escort *should* be rushed home. Rumours are pinned from 0:00 on a 200 m map | **Main cause** |
| **Staying pays only a side slice** | Stage bonus on caches, events and essence only (§1.3). Ore, wood, hides flat. One reveal cache per stage | **Main cause** (in the design) |
| **The upside is invisible, the downside constant** | "At risk" in warning colour every second, the stage name in orange, the +% in one message. Ore isn't on the map, so after the rumours nothing visible pulls the player on | **Strong contributor** |
| **Danger ramps faster than reward** | +10% at 4:00 against +25% on a slice. All 3 deaths between 3.5 and 4.5 minutes, each losing the bag. Loss aversion makes a lost bag feel worse than an equal gain feels good (§4.3) | **Contributor**: early deaths teach caution |
| **Home needs pull** | Home 68-89%. The early bill is wood and stone, made at home (`EconomyAndPhases` §2.1). Quests are held for homecomings | **Contributor**: in the early game a raid isn't the bottleneck |
| **Extraction is free and always open** | The West Beacon from 0:00, no cost, 30 s | **Enabler**, not a cause. Closing it would punish, not reward |
| **Well Rested runs out** | Gone by the end in 12 of 12. When it ends, max health drops about 9% (148 → 135 in the Kit rows) | **Possible small contributor.** Not proven |
| **Events' time costs** | 20-60 s each for the careful choice, pulling stages earlier | **Unknown**. Choices aren't logged |
| **Away income counts raids, not minutes** | Parties, outposts and homecoming gifts pay the same from 3 minutes on | **Not yet a cause** (none were unlocked). It will remove a reason to stay later |
| Bag full or too heavy | 0 items left behind; median 41 of 130 weight | **Ruled out** for now |
| Nothing left (nodes cleared) | 20-35% of copper nodes mined; 38% of the map seen; pickups to the end; nodes regrow | **Ruled out.** "Nothing *marked* left" is true |
| Travel time | Exits 50-157 m from the start: about 30 s on foot | **Ruled out** |
| Night | 3 raids: 108 s (a rescue), 209 s (died), 283 s | **Unknown** (too few) |
| A companion | Never along; Sigrid not reached | **Not tested** |

**The picture.** The raid gives a clear job up front (a person, a beacon, two rumours), and the job takes about four minutes on
a small map. After that, the HUD shows only a growing loss and a rising danger, while the main income pays no more for staying.
At home a real bottleneck waits. Leaving is the sensible choice, and the player makes it before the first stage begins, so the
Glint never gets a chance.

---

## 4. How other games pace raids

### 4.1 Game by game

| Game | Raid length | Does staying or going deeper pay more? | Getting out | Losing | What it does badly |
|---|---|---|---|---|---|
| **Escape from Tarkov** | 20-50 min by map | Guarded hotspots hold the best loot (Lighthouse's water plant: bosses, guns, mines) | Some exits always open; others need roubles (a car: pay, then about 1 min), keys, power or a partner | All but a 2×2 to 3×4 secure container and insured gear | **"Run Through"**: leave with under about 200 XP or 7 minutes and your loot isn't "found in raid" (guides disagree on the numbers). A hidden penalty players learn from wikis |
| **Hunt: Showdown** | 45 min (cut from 60) | A chain: 3 clues show the boss, kill it, **banish** it (the whole server is told where you are), carry a token that shows on everyone's map | Any exit, any time; stand 30 s with nobody downed; an enemy in the zone resets it | The hunter and all gear; half the match's XP | Banishing paints a target on you: great tension, harsh for new players |
| **Dark and Darker** | Shrinking circle | **Down stairs** lead to deeper floors: "richer rewards and a longer path back". High Roller costs 100 gold for better drops | Escape portals appear later, in sets of 3; one is guaranteed before the last circle | All gear; keep 50% of XP | A guaranteed portal may not fit every teammate; High Roller hides its timer |
| **Arc Raiders** | 30 min max; you can arrive with 15 min left | Night raids give "rare salvage"; storms a storm-only material | An elevator comes about 45 s after you call it; **exits shut down one by one as the round goes on**; a Raider Hatch opens with a single-use key; at time-out the map is bombarded | All but the **Safe Pocket** (1 slot, 2-3 with augments); **XP is always kept**; free loadouts with no cooldown | **Experienced players finished in "a third of a match" and left**; sessions would "pitter out and die after ten minutes". Embark filled them with late joiners, who complained, and changed matchmaking in 2026 |
| **Marauders** | About 25 min (unverified) | – | Two exits in space; your ship has a **storage room you can unload into mid-raid** | No insurance; a free starter kit always | – |
| **The Cycle: Frontier** (closed 2023) | 6-hour servers you drop in and out of | **Loot tiers by zone**: Crescent Falls' easiest zones equal Bright Sands' hardest | Evac ship about 55 s, then 15 s; storms (about 5 min) block evac | Insurance or gear salvage | Shut down |
| **Deep Rock Galactic** (PvE co-op) | Set by the mission | **Secondary objectives** pay extra; Warnings add +15-50% | The drop pod's countdown starts **only when the team calls it**; it leaves after 5 min (3 or 1 in some modes) | **A failed mission still pays 25%**. Each dwarf who reaches the pod adds 150 × hazard bonus. Everyone present gets an equal copy | – |
| **Hades** | 20-40 min a run | **Doors show their reward before you choose.** Heat is opt-in difficulty for bounties | – | The run's gold and boons; **meta currencies are kept** | – |
| **Valheim** | No clock | Each biome is the next tier | Ore can't go through portals, so the trip home is carried | 300 carry (+150 belt); a tombstone; about 5% skill loss | Carrying iron is "a glum chore" |
| **Enshrouded** | The Shroud timer: 5 min, +1 per flame level | – | Fast travel from anywhere outside the Shroud | The backpack drops (default) | – |
| **Palworld** expeditions | 30 min to 2 h | **Surplus power raises the yield** (since v0.6.4) | – | Pals sent are unavailable until they return | – |

### 4.2 What the designers say

- **Arc Raiders** (Virgil Watkins): "approachability became a key design tenet"; free loadouts have "no strings attached";
  Scrappy is there for "trying to soften the blow of defeat"; quests let players "still understand that you've made some
  progress"; "the amount of extraction points reduces as the round goes on".
- **Hades** (Greg Kasavin): "take the pain out of dying"; players must "feel the time you spent wasn't a waste".
- **"A better carrot, a better stick"** (Game Developer): harsh penalties push players to play safe until they "bore themselves
  to death". High-risk options should be optional and pay an emotional high to match.
- **Loss aversion**: a 300-person study (University of Saskatchewan, after "Risking Treasure", CHI PLAY 2020) found a strong bias
  against losses even with virtual gold. Players also hate losing options they may never use.

### 4.3 What Safe Haven should take, and leave

| Pattern | From | For Safe Haven |
|---|---|---|
| **Value rises where the main income is**, by depth or time | The Cycle's zones, Dark and Darker's floors | Safe Haven's maps are small (200 m), so **time** is the depth. Let stages raise ore, not only caches (§5, #3-4) |
| **Content that only exists in the second half** | Arc Raiders' late joiners "profit way more"; Dark and Darker's late portals | Late veins and late rumours (#4, #9) |
| **Objectives that end late, not at minute 4** | Hunt's clues → boss → banish; DRG's objective → call → pod | Rescues later in the raid, and a way to park the rescued (#2) |
| **Pay for staying, not for leaving** | DRG's survival and hazard bonus, Hades' Heat, Palworld's surplus | Stage gather bonus, quicker late holds, more home income for time away (#3, #6, #7) |
| **Show the reward before the choice** | Hades' doors | A clock bar with the next stage's chest and bonus (#5) |
| **Losing costs less, so greed is sensible** | Arc keeps XP and the pocket; DRG pays 25%; Hades keeps meta currency | Safe Haven already keeps base XP, gear and a whole-stack pocket. Make sure players *know* (#5) |
| **Bank mid-raid** | Marauders' ship storage | The outpost keeper's camp (#10) |
| **A full bag is the natural "go home"** | Valheim's carry weight | With regrowing nodes, a 12-minute raid could fill 130 weight: a readable limit |
| **Co-op-safe rewards** | DRG: everyone present gets an equal copy; the countdown starts on the team's call | Every bonus here is per player; quicker holds help the whole team |
| Avoid: **penalties for leaving early** | Tarkov's run-through | Against the user's rules |
| Avoid: **front-loaded objectives** | Arc Raiders' "a third of a match" | This is Safe Haven's problem today |
| Avoid: **exits a team can't share, hidden timers** | Dark and Darker | Keep exits open to all; keep every timer on the HUD |

---

## 5. Recommendations

The rules followed: positive incentives over punishment, no hidden penalties, pictures not text walls, numbers in
ScriptableObject settings, co-op friendly, and the clock stays 12 minutes.

### 5.1 Prioritised

| # | Change | Numbers | Where | Effort | Why |
|---|---|---|---|---|---|
| **1** | **Measure why players leave** | New rows: `StageBegan` (stage, clock, metres to the nearest open exit), `ExitLit` (exit, seconds left, stage), `RevealOpened` (kind, clock), `EventChoice` (event, choice, seconds it cost), `BagAtEnd` (weight %, slots %, what is in the pocket), `NodesMined` (kind, mined, standing at the end), `Rescue` (freed, home). A **playtest-only "Why home?" card** on the results screen: 5 pictures, one tap (bag full / did what I came for / too dangerous / nothing more to find / needed at home). Off in release, like the F1 menu | `PlaytestLog`, `PlaytestLogger`, `StageReveal`, `ExtractionPoint`, `RaidEventDirector`, `RescuableTraveler`, `RaidResultsScreen` | S | 17 raids from one player can't separate the causes. One tap per raid can |
| **2** | **Rescues stop ending the raid** | (a) In any open exit circle, "Wait here": the freed person stays there, can't be hurt, and comes home with the player's extraction from **any** exit. (b) From the second rescue on, the person calls out when **The Mist Stirs** begins, not at 0:00 (their rumour says so in the briefing). The first rescue (Ingrid) stays as it is: it teaches the loop | `RescuableTraveler` (a "waiting at the exit" state), `ExtractionPoint.Inside`; `RaidRumourDefinition` › Appears At Stage, `RaidRumourDirector` | M | 10 of 17 raids; players leave about 2 minutes after freeing someone. GDD §8.4's rule (a person not extracted doesn't arrive) is kept |
| **3** | **Stages pay on the main income** | New `RaidStage` › Gather Bonus: **+15% / +30% / +50%** node yields in The Mist Stirs / The Hunt / Collapse (scale the main yield with `RaidParameters.ScaleAmount`). Creature item drops get the stage's Loot Bonus too. On a full raid mined evenly: about **+17% ore** ((4 + 4×1.15 + 3×1.3 + 1×1.5) / 12) | `RaidStage`, `ResourceSource` (the grant), `LootOnDeath`, the three raid assets | S | Today staying pays only caches, events and essence |
| **4** | **Rich veins at the stages** | New Reveal kind **Vein**: at The Mist Stirs, 2 nodes of the tier's anchor ore (copper I, iron II, moonsilver III-IV), ×1.5 yield, gold on the minimap, 70-140 m away, 50 m from exits. **Move, don't add**: the start layout loses 2 of its ~13 anchor nodes. A full raid's total stays about the same (11 + 2 × 1.5 = 14 node-equivalents, against 13) | `RaidStage.RevealKind`, `StageReveal`, the spawn lists (`Data/Raids/*`) | S-M | Ties the reveal to what the economy counts. Short raids mine 2-4 of ~11 nodes, so moving 2 costs them nothing in practice |
| **5** | **Show the upside** | (a) A gold chest badge beside the clock: "×1.25", pulsing when it rises. (b) A thin bar under the clock with chest marks at each stage and a hover: "+50% in 1:20". (c) Results screen: one row with a chest icon, "Stayed for: +N" (what the stage bonuses added). (d) Ore veins once seen get a small mark on the minimap, like exits. Keep "At risk", but pair it with the gold badge | `RaidHudView`, `MinimapHud`, `RaidReport`, `RaidResultsScreen` | S-M | The HUD shows the loss every second and the reward once (§1.6) |
| **6** | **Late exits are kinder** | `RaidStage` › Extraction Time Scale: **1.0 / 0.8 / 0.6 / 0.4** (30 → 24 → 18 → 12 s in the Misty Woods). Keep the Hollow Oak (×0.6 from 9:00) | `RaidStage`, `ExtractionPoint` (its hold duration), the three raid assets | S | Lowers the end's risk instead of punishing an early exit. Holds took about 55 s in practice; at 12 s the last minute is a sprint, not a siege. Helps every co-op player alike |
| **7** | **Away work pays for time away** | Parties, outposts and homecoming gifts: today's yield at 3 minutes (the floor is unchanged), rising linearly to **×2 at 9 minutes**. The gift's chance: 60%, +5 points a minute over 3, at most 90%. The homecoming card shows each resident's picture and "+4 for 9 minutes out" | `LandParties`, `Outposts`, `HomecomingSettings`; new `TerritorySettings` › Party Full Seconds and Most Bonus | S | Rings already count seconds away. These count raids, so a 3-minute raid pays like a 12-minute one |
| 8 | **Well Rested lasts the raid** | Duration = the raid's clock. Comfort sets its size instead: today's +10% health and +15% stamina at the comfort that now gives 6 minutes, less below that | `HavenReadiness.GiveRest`, `HavenSettings` | S | It ran out mid-raid every time, cutting max health by about 9% near minute 4. Measure first (#1: log Rested seconds at launch) |
| 9 | **Late rumours** | One of the two rumours is a late one: shown in the briefing with a clock icon, it appears at The Mist Stirs or The Hunt (the Rot's Heart, a named elite, a lost cart, a pedlar's camp) | `RaidRumourDefinition` › Appears At Stage, `RaidRumourDirector`, `RaidForecast` | M | Arc Raiders' lesson: objectives done in a third of the match empty it. Hunt's chain keeps players in |
| 10 | **The outpost's raid hook: the keeper's camp** | In the outpost land's raid, the keeper's camp lights at The Mist Stirs: leave up to 4 stacks with the keeper (they come home whatever happens), or extract there at ×0.5 hold | `Outposts`, a set piece, `ExtractionPoint` | M-L | Banking mid-raid (Marauders) makes a longer raid less of a gamble. Gives outposts their missing hook (`Territory.md` "Not yet") |
| 11 | **Confirm node regrowth in raids** | Mine a copper node in a launched raid and wait 180 s. If it regrows, keep it (it makes staying productive) and show it: a sprout icon on a mined vein's mark | a probe; `ResourceNode` | S | The data says it regrows; the economy's per-raid estimates don't count it |
| 12 | Event time costs, later | If #1 shows the careful choices shorten raids, change their cost from clock time to noise (a call like the beacon's) | `Data/Raids/Events` | S | Only after measuring |

### 5.2 What this should do, roughly

These are estimates to check in the next playtest, not promises.

- **Today** (median 273 s): about 14 copper ore when mining at all, often 0 in rescue raids.
- **With #2-#6**: a rescue no longer ends the raid, the first stage arrives with a marked vein and a visible "×1.15" on ore, and
  the end is a quick 12 s hold. If players stay to about **7-8 minutes**, copper per raid should reach about **25-30**: their
  current 2-3 nodes, the 2 veins at ×1.5 × 1.15, and 1-2 more nodes or regrown ones.
- **A full 12-minute raid** gives about +17% ore over today's design (#3). The veins (#4) move ore rather than add it. If the economy
  wants the same raids per tier, trim the anchor ore weights by about 15%, or let the extra pay for the early grind.
- **If players stay long**, the 130 weight becomes the limit at about 50-60 copper ore. That is a readable, positive "time to go".

### 5.3 Forays and outposts (new 2026-11-22)

| System | How it pays | Effect on raid length today | With these changes |
|---|---|---|---|
| **Forays** | Played minutes, at home or raiding (20-35 min a place). Misty Woods I: 4-8 fibre, 2-4 hide (80%), 3-5 copper ore (70%), berries, herbs. About 3 copper ore per 20 minutes | **None.** Time counts anywhere. A foray brings about a fifth of a short raid's copper | Keep as is. They make home time a little more useful, but are a trickle |
| **Outposts** | Per raid of 180 s or more (Greyfell 3-5 iron ore and 2-3 granite; the others 1-3 of their material) | **None, or a slight push towards short raids.** A 4.5-minute raid pays in full. Per real hour: about 3.1 raid cycles (4.5 + 15 min) against 2.7 (12 + 10 min), so short raids earn outposts about 13% more an hour | With #7, a 12-minute raid pays ×2 and a 4.5-minute one ×1.25: about 5.5 against 3.9 units an hour. #10 gives the outpost a reason to *be* in the raid |
| **Hunting and fishing parties** | Per raid of 180 s or more | The same as outposts | #7 |
| **Homecoming gifts** | 60% per raid of 180 s or more | The same | #7 |
| **Rings (felling, quarry)** | Per second away | Already reward longer raids (a 12-minute raid about 27 logs, a 4.5-minute one about 10) | Keep; #7 makes the rest consistent with them |
| **Territory turns** | One per raid that ends, whatever its length | Short raids mean more turns, and so more erosion and pushes, per real hour | Neutral; worth knowing |

**The bigger interaction.** The early game's home bottleneck (wood and stone by hand) is one of the reasons to leave. The Keeper's
view puts residents on plain goods from the third rescue. So, if home needs are a real cause, raids should lengthen on their own
in the mid game. The next playtests should compare raid length before and after the Keeper's view.

### 5.4 What to measure in the next playtest

The log already has `CameHome`, `Eaten`, `HomeStay`, `Stock` and `RaidNodes`. With #1's rows:

| Question | Rows | Target |
|---|---|---|
| How much of the clock is used? | `RaidFinished` | Median at least 60% (7:15 of 12:00) in raids without a rescue |
| When did they decide to go? | `ExitLit`, `StageBegan` | Most exits lit after The Mist Stirs |
| Do they go for the reveal? | `RevealOpened`, Position rows | At least half of the reveals opened |
| How much of the ore did they take? | `NodesMined`, `RaidNodes` | At least half of the anchor-ore nodes |
| Do rescues still end raids? | `Rescue`, `RaidFinished` | Freeing to the end longer than 4 minutes |
| Is the bag the limit? | `BagAtEnd`, `LeftBehind` | Some long raids end near full: the healthy limit |
| Do deaths drive caution? | `PlayerDied`, `Hurt`, the next raid's length | Deaths at or under 20%, and no drop in raid length after a death |
| Why did they go home? | The "Why home?" card | "Did what I came for" and "needed at home" falling; "bag full" rising |
| Is home still the bottleneck? | `HomeStay`, `Stock` | Home share at most 60% early (`EconomyAndPhases` §4.1) |
| What does a raid pay at home? | `CameHome` against `RaidFinished` seconds | Rises with minutes away once #7 is built |
| Copper per raid at tier I | `ItemObtained` in raids | At least 30 (two thirds of the design's 43) |

The Troll Valley `RaidNodes` count should be taken later (20 s in, or when the generator says it's done): its probe count looked
early.

---

## 6. Open questions for the user

| # | Question | Recommended answer |
|---|---|---|
| 1 | **Rescues:** may a freed person wait safely in an exit circle while you go on, and may later rescues call out when the mist stirs instead of from the start? | **Yes to both.** The rule "a person not extracted doesn't arrive" stays. Ingrid's first rescue stays as it is |
| 2 | **Stage bonus on ore:** should the danger stages also raise node yields (+15/30/50%) and creature drops? | **Yes.** It's a pure positive, and the bonus finally touches what the economy counts |
| 3 | **Rich veins:** moved from the start layout (same totals), or added on top (more ore)? | **Moved.** Short raids lose nothing in practice, and the economy's numbers hold |
| 4 | **Time away:** should parties, outposts and homecoming gifts pay more for a longer raid (today's yield as the floor, ×2 at 9 minutes)? | **Yes.** It matches the rings, and is never less than today |
| 5 | **"Why home?" card:** a one-tap picture choice after each raid, in playtest builds only? | **Yes**, off in release like the F1 menu. It is the only way to learn the *why* from a handful of raids |
| 6 | **Regrowing nodes:** copper and wood regrow in a raid after 3 minutes (by the data). Intended? | **Keep it, confirm it in a launched raid, and show it.** It makes staying productive, and the bag becomes the natural limit |

---

## 7. Sources

**Internal** (read, not changed): `Scripts/Raids/RaidDirector.cs`, `RaidSession.cs`, `RaidStage.cs`, `StageReveal.cs`,
`ExtractionPoint.cs`, `ExtractionHold.cs`, `ExtractionWindow.cs`, `RaidParameters.cs`, `RaidIntensity.cs`,
`RoamingSpawnSettings.cs`, `RaidLauncher.cs`, `RaidRisk.cs`, `LootCache.cs`, `Map/RaidMapGenerator.Points.cs`;
`Items/LootOnDeath.cs`, `Items/ItemInventory.cs`, `Player/PlayerSnapshot.cs`, `ResourceInventory/ResourceNode.cs`,
`Settlement/HavenReadiness.cs`, `Settlement/HomecomingSettings.cs`, `Territory/ForaySettings.cs`, `Territory/Outposts.cs`,
`Territory/LandParties.cs`, `Playtest/PlaytestLogger.cs`, `Progression/ExperienceTracker.cs`, `UI/RaidHudView.cs`, `UI/MinimapHud.cs`;
`Data/Raids/MistyWoods.asset`, `TrollValley.asset`, `MossMarsh.asset`, `Data/Raids/Maps/*Layout.asset`, `Data/Raids/Events/*`,
`Data/Gathering/*`, `Data/Items/Materials/*`, `Data/Items/HideRucksack.asset`, `Resources/DurabilitySettings.asset`,
`SettlementSettings.asset`, `TerritorySettings.asset`, `HavenSettings.asset`, `ForaySettings.asset`;
`Scenes/Raids/Raid_MistyWoods.unity` (exit overrides); the git history of `MistyWoods.asset` (the clock: 300 s to 2026-11-01,
1,000 s to 2026-11-14, then 720 s). Docs: `GameDesignDocument.md` §7-8, §18.3; `EconomyAndPhases_2026-11-22.md`; `Territory.md`;
`SystemsGapReview_2026-11-19.md` §4; `GameDesignResearch_2026-11-17.md` §4. Playtest CSVs 20261114-122242, 20261114-232848,
20261115-184337, 20261119-184435, 20261120-214517, 20261121-151353 (and the October runs 20261017-160619, 20261027-204855 as a side
note); probes 20261122-16* for `RaidNodes`.

**External.** Fandom and the Tarkov wiki.gg refused automated reads, so some Tarkov and Hunt facts come from those wikis' search
summaries; they are marked as such.

- Escape from Tarkov:
  [Wikipedia](https://en.wikipedia.org/wiki/Escape_from_Tarkov);
  [exits (GGRecon)](https://cms.ggrecon.com/guides/escape-from-tarkov-how-to-extract/);
  [exits (Pro Game Guides)](https://progameguides.com/escape-from-tarkov/how-to-extract/);
  [Found in raid and run-through (wiki, search summary)](https://escapefromtarkov.fandom.com/wiki/Found_in_raid);
  [run-through thresholds (disputed)](https://www.gameslearningsociety.org/wiki/how-much-xp-do-you-need-to-not-get-a-run-through-tarkov/);
  [Lighthouse loot](https://www.ginx.tv/en/lighthouse-map-extraction-points-best-loot);
  [secure containers](https://afkgaming.com/esports/guide/everything-about-secure-containers-in-escape-from-tarkov)
- Hunt: Showdown:
  [Wikipedia](https://en.wikipedia.org/wiki/Hunt:_Showdown);
  [Update 1.9 notes](https://www.shacknews.com/article/131330/hunt-showdown-update-19-patch-notes);
  [game modes (wiki, search summary)](https://huntshowdown.fandom.com/wiki/Game_Modes);
  [extraction tips (wiki, search summary)](https://huntshowdown.fandom.com/wiki/Tips);
  [what happens on death](https://mein-mmo.de/en/hunt-showdown-what-happens-when-you-die,850013)
- Dark and Darker:
  [Dungeon (wiki)](https://darkanddarker.wiki.spellsandguns.com/Dungeon);
  [Escape Portal (wiki)](https://darkanddarker.wiki.spellsandguns.com/Escape_Portal);
  [Season 11 escape guide](https://www.mmoexp.com/News/dark-and-darker-season-11-escape-guide-portal-spawns-stairs-and-dark-swarm-survival.html);
  [extraction and going down](https://metabot.gg/en/darkanddarker/guides/how-extraction-and-escape-portals-work)
- Arc Raiders:
  [Getting Started (wiki)](https://arcraiders.wiki/wiki/Getting_Started);
  [exits and timers](https://allthings.how/arc-raiders-extraction-elevators-hatches-and-timers-explained/);
  [the time-out](https://allthings.how/how-to-extract-in-arc-raiders-without-losing-your-loot/);
  [Safe Pocket](https://allthings.how/arc-raiders-safe-pockets-increase-slots-and-keep-loot-on-death);
  [Kotaku: Virgil Watkins interview](https://kotaku.com/arc-raiders-embark-extraction-virgil-watkins-interview-2000639879);
  [FRVR: late joining](https://frvr.com/blog/arc-raiders-lead-admits-late-joining-really-sucks-sometimes/);
  [GamingBolt: late joining](https://gamingbolt.com/arc-raiders-design-lead-admits-spawning-late-into-a-session-is-not-great);
  [the matchmaking change](https://comicbook.com/gaming/feature/arc-raiders-finally-addresses-a-major-player-complaint-in-new-update/);
  [map conditions](https://arcraiders.com/en/news/new-map-conditions);
  [GDC 2026: slowing it down](https://opencritic.com/news/27964/the-first-thing-embark-did-after-making-arc-raiders-an-extraction-shooter-was-making-it-slower)
- Marauders: [gameplay](https://www.theloadout.com/marauders/gameplay); [ship storage](https://www.gamepressure.com/newsroom/marauders-ship-and-storage/za4b2b)
- The Cycle: Frontier:
  [Wikipedia](https://en.wikipedia.org/wiki/The_Cycle:_Frontier);
  [storms](https://progameguides.com/the-cycle-frontier/how-long-do-storms-last-in-the-cycle-frontier/);
  [loot by zone](https://ggrecon.com/guides/the-cycle-frontier-map-loot-locations)
- Deep Rock Galactic: [Mission (wiki)](https://deeprockgalactic.wiki.gg/wiki/Mission); [Drop Pod (wiki)](https://deeprockgalactic.wiki.gg/wiki/Drop_Pod)
- Hades:
  [chambers and doors](https://saschb2b.github.io/game-mechanics/games/hades/chamber-and-paths);
  [what carries over](https://www.gamepressure.com/hades/progress-what-gets-carried-forward/z9ebaa);
  [Heat](https://rpgsite.net/feature/10287-hades-pact-of-punishment-heat-modifiers-and-how-to-maximize-your-rewards);
  [Game Developer: Kasavin on death](https://gamedeveloper.com/design/how-supergiant-weaves-narrative-rewards-into-i-hades-i-cycle-of-perpetual-death)
- Valheim: [portals](https://www.gamepressure.com/valheim/portals/zce2d1); [carry weight](https://screenrant.com/valheim-increase-inventory-weight-limit-haldor-megingjord/); [death](https://gamever.io/knowledge-base/what-death-actually-costs-in-valheim-all-six-death-penalty-settings); [PC Gamer: mining iron](https://www.pcgamer.com/uk/everything-about-mining-iron-in-valheim-straight-up-sucks)
- Enshrouded: [the Shroud timer](https://allthings.how/how-the-enshrouded-shroud-timer-works-and-how-to-extend-it/); [fast travel](https://pcgamer.com/enshrouded-fast-travel); [death](https://allthings.how/enshrouded-death-explained-dropped-items-respawn-and-loot-loss-settings/)
- Palworld: [Expedition (wiki)](https://palworld.wiki.gg/wiki/Expedition)
- Design writing:
  [Game Developer: A better carrot, a better stick](https://www.gamedeveloper.com/design/a-better-carrot-a-better-stick-on-risks-and-player-behaviorism);
  [loss aversion thesis (300 participants)](https://harvest.usask.ca/handle/10388/13478);
  [Game Developer: option availability and loss aversion](https://www.gamedeveloper.com/design/analysis-option-availability-and-loss-aversion-in-game-design)
