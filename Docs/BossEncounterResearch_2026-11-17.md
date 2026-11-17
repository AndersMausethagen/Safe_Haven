# Boss encounters: a second kind of raid (research, 2026-11-17)

The user: "I am considering having different types of raid, Like expedition (for getting resources and finding npc) and Boss
encounter. In Boss encounter there is only a very small map of the biome. Designed around a boss encounter. Think about it like
walking through the mist in a Dark souls game. So limited space, different rules. Kill the boss to end. In order to get acess to the
boss, you must have gotten to a certain point in the quest line. This unlocks the boss encounter. Killing the boss will unlock next
raid and progress the story. [...] Research how this could fit and how this can be implemented."

This is research only. Nothing has been built.

## Decisions (the user, 2026-11-17)

**Go for option C.** The answers to §4's questions:

1. **Name:** "Boss encounter" for now, a prototype name ("Holmgang is not something people know").
2. **The Heartroot gets its own arena too**, and "an epic fight, having the player use everything he has learned: parry, dodge,
   using flasks etc."
3. **The Brukam moves** out of Troll Valley's expeditions into his encounter.
4. **All in:** the only way out is winning or dying.
5. **Wear on a failed encounter: 10%.** "Rules should stay consistent ... if that feels too unfair, we might lower the global cost."
6. **Companions can come.**
7. **A friend whose story isn't there gets only the drops**, "but it helps their quest as well if he is also on it".
8. **Rematches at higher tiers, but only 24 hours after a completed kill**, so it can't be spammed.
9. **Yes:** the Rime-jutul comes between "The Door in the Snow" and "The First Hearth".

And, changing §2's hidden limit: "Failing can be a bit softer, but should still be punishing. The player must feel the need to come
prepared and use every available power increase available. We could use a shown timer, a DPS check, where the boss will enrage after
a certain time. I think we should design the boss encounter to last around 10 minutes."

## The short answer

- **It fits the game, and most of it is already built.** A boss encounter can be an ordinary raid underneath. It uses the same
  portal, launch, save before leaving, crash recovery, loss rules, results screen and co-op follow. It plays in a small hand-built
  scene with some rules switched off: no clock, no rumours or events, no hunt for an exit. The new parts are:
  - a fog gate;
  - a way home that opens when the boss falls;
  - a story trigger, so that each phase of the fight can change the arena;
  - a page in the planner for encounters.
- **Two kinds of raid.**
  - **Expeditions** are raids as they are now: gather, rescue, follow rumours, and find the way to the next boss.
  - **Encounters** have one boss in one arena, and the run ends when it dies.
  - In short: expeditions find the boss, and encounters are where you fight it.
- **The quest line opens an encounter, and the kill opens the next region.** Both gates already exist as data:
  - `QuestCondition` checks whether a quest has started or is done.
  - `RaidDefinition › Unlocked By` checks whether a boss has been beaten.
  - Unlock the next raid when the *quest* is done, not when the boss is killed. That way, paying the Brukam's toll counts as well.
- **Failing costs what a failed raid costs.** Since a boss is meant to be retried, three things are softer:
  - gear wears less (10%, not 25%);
  - the next try starts at a rest stone beside the gate;
  - nothing is spent until you step through the gate.
- **Recommendation: Option C** (§3.2).
  - The encounter stays a `RaidDefinition`, so it is still a destination in the planner.
  - A new `BossEncounterDefinition` beside it holds the boss, what opens the encounter and the gate's price.
  - A `BossEncounterSystem` and three small scene parts run it, in one hand-built arena scene per boss.
  - Sizes: the framework is M; each arena is M to L (scene, story graph and art).
- **Build the Brukam's encounter first.** The bridge that breaks under him (GDD §10.7) is the fight a generated map can't stage.
  Leave the Heartroot in the Misty Woods as it is for now: it works, and it teaches the first boss inside a raid the player already
  knows. Move it into an arena later if the Brukam's encounter plays well.

---

## What other games do, and what to take

| Game | What it does | What to take |
|---|---|---|
| **Elden Ring / Dark Souls** | Bosses are "beyond a fog barrier". Summoning pools are marked near bosses. In co-op, beating a boss or mini-boss ends the session, and the host can have up to two co-op guests ([Bandai Namco](https://en.bandainamcoent.eu/node/7918), official). The Stakes of Marika are checkpoints at the arena door. They make the walk back to the boss after a death shorter, and let players change their build before trying again (Kitao, via [GamerBraves](https://gamerbraves.com/?p=108158); *search summary only*). Elden Ring also has field bosses that stand in the open world, with no fog | The gate, and a rest stone at the gate for later tries. Friends join before the gate. Field bosses in expeditions, story bosses behind the gate |
| **Valheim** | You call each boss at its altar with offerings from its own biome: 2 deer trophies, 3 ancient seeds, 10 withered bones. What it drops and the power it gives open the next biome ([PC Gamer](https://www.pcgamer.com/au/valheim-boss-fights-guide), [Windows Central](https://www.windowscentral.com/valheim-boss-guide-how-find-summon-and-defeat-every-boss); *secondary guides*). You can call a boss again with new offerings | An offering at the gate, gathered on expeditions, sets the pace of rematches. No lockout is needed |
| **Monster Hunter** | Each monster is its own quest. A quest has a 50-minute limit and fails at the third faint ([community wiki](https://monsterhunterwiki.org/wiki/MHWorld/Quests/Optional_Quests)). Each faint cuts the reward ([Game8](https://game8.co/games/Monster-Hunter-Wilds/archives/498751); *search summary*). Arena quests are fought in a small fixed arena with gear set by the game ([PC Gamer](https://www.pcgamer.com/games/rpg/monster-hunter-wilds-first-title-update-is-overflowing-with-new-stuff-a-long-awaited-grand-hub-arch-tempered-monsters-arena-quests-and-most-importantly-fashion/); *search summary*) | A long time limit as a safety net, not a clock the player watches. In co-op, our downs and help-ups already play the part of faints |
| **Hunt: Showdown** | Boss lairs sit on the normal extraction map. Killing the boss is half the job: you hold the spot while it is banished, then get out ([fan wikis](https://bloxodes.com/wiki/hunt-showdown); *search summary*). Crytek's rules: "the gear you need will be around the boss area", and lairs usually don't change shape ([Crytek](https://huntshowdown.com/news/meet-scrapbeak-our-new-boss), official) | The Heartroot in the Woods is already this kind of boss. Arenas should have healing nearby, and our arenas *do* change: that is what they are for |
| **V Rising** | About 40 V Blood bosses carry the whole progression, and each opens abilities and recipes. A tracker leads you to them ([guides](https://progameguides.com/v-rising/all-v-blood-boss-locations-in-v-rising/); *search summary*) | Once the quest reaches a boss, the portal can "sense" it: the encounter appears in the planner |
| **Returnal** | Beating a biome's boss opens the way on for good ([guides](https://www.powerpyx.com/returnal-permanent-unlocks-explained/); *search summary*) | The kill unlocks the next raid for good. This is already our rule |
| **Hades** | The same bosses come back every run. Under the higher-difficulty pact (Extreme Measures) their fights change, not only their numbers ([Supergiant patch notes](https://www.supergiantgames.com/blog/hades-welcome-to-hell-update-patch-notes/); *search summary*) | A rematch at a higher tier gets new moves (`MonsterAttack › From Tier`). GDD §5.4: "a novel choice, not only higher numbers" |
| **WoW / Destiny raids** | A raid is a chain of boss encounters, each built around its mechanics. Progress is checkpointed per boss. WoW gives a boss's loot only once per lockout ([Blizzard support](https://support.blizzard.com/article/000269958); *search summary*). Destiny's checkpoints reset each week ([guide](https://www.gamerevolution.com/guides/435063-destiny-2-reset-raid-checkpoint-progress); *search summary*) | One encounter is one boss. Weekly lockouts don't fit a single-player game |
| **ARC Raiders / Dark and Darker** | ARC Raiders' big bosses (the Matriarch, the Queen) appear only under a map condition on the normal maps ([PC Gamer](https://www.pcgamer.com/games/third-person-shooter/arc-raiders-matriarch); *search summary*). Dark and Darker's bosses sit deeper in the dungeon, and you still leave through an ordinary escape portal ([wiki](https://darkanddarker.wiki.spellsandguns.com/Escape_Portal)). I found no boss-only mode in either | Extraction games keep their bosses inside the run. Doing both kinds, as Elden Ring does, is what would make Safe Haven different |

**A name, if wanted.** In Old Norse custom, a *holmgang* was a duel fought on a *holme*: "a real island or a marked-off place". It was
seen as a kind of trial before the gods ([SNL](https://snl.no/holmgang)). It fits "a small map, different rules" well. "Encounter"
is used below as the working name.

---

## 1. How it fits the game

### 1.1 Two kinds of raid

| | Expedition (as now) | Encounter (new) |
|---|---|---|
| What it is for | Resources, people, rumours, story finds, and the way to the next boss | One boss: the act's turning point |
| Map | Generated each run, 200 m | Hand-built and the same every time, about 100 m: arrival, approach, rest stone, gate, arena |
| Clock | 12 minutes, danger stages, exits that open and close | None shown. A hidden limit (30 min) ends a stuck run as a time-out |
| Rumours, events, elites, roaming spawns | Yes | None. GDD §7.4 already says "a boss raid can be more structured so random events do not compromise encounter fairness" |
| What the portal offers | Tier, Portal Sparks, map size, runes, companion | The boss's page. Tiers only for rematches. No Sparks, map size or runes. Companions are a question for you (§4) |
| Horse and cart | Come through | Not taken (blocked at the portal) |
| Leaving | Light an exit and hold it | Before the gate, the arrival stone takes you home with nothing lost. After the gate, only by winning: the way home opens where the boss falls |
| Dying | You lose what you carry. Gear is kept, at −25% durability | The same rules, at −10%. An offering laid at the gate is gone |
| Trying again | Plan a new run | Start at the rest stone by the gate, as often as you like |
| Rewards | What you carry out | First kill: skill, story step, trophy at home, next raid. Drops, carried a few metres to the way home. Rematches: drops by tier |

### 1.2 The way in: the quest line opens it, expeditions find it

A step of the main quest opens the encounter. Its first objective should be on an expedition: find his tracks, reach the Troll's
Road, reach the Black Pool (a Reach Place objective). Then the portal "has found" the place, and the encounter appears in the
planner. This is the tracker from V Rising, the clues from Hunt and the Vegvisir from Valheim. It keeps expeditions as the way to
the boss.

| Boss | Its encounter opens when | Played from | Winning it (or the bargain) opens |
|---|---|---|---|
| The Heartroot | Stays in the Woods for now: its rumour after two extractions, then the Hearth Ember (built) | Misty Woods | Troll Valley (built: `Unlocked By`) |
| The Brukam | "The Toll" starts (Act II #19, after "The Man in Grey" and Hild home) | Troll Valley | The Moss Marsh, once "The Toll" is done, by fight or toll |
| The Nøkk of the Black Pool | "Steel in the Water" starts (after "The Black Pool") | Moss Marsh | The Frost Peaks, once that quest is done, by fight or bargain |
| The Rime-jutul | No quest step yet. The Peaks chain (RaidIdentityResearch §6.4) has none: one is needed before "The First Hearth" | Frost Peaks | The First Hearth (finale, to agree) |

- **Before** its quest starts, the encounter doesn't show at the portal, so nothing is spoiled.
- **Once open,** it has its own tab with the boss's name card.
- **Once beaten,** the tab stays as a rematch, marked as won.

### 1.3 What it costs, and what failing means

- **Nothing is spent at the portal.** The approach is safe. The arrival stone is an exit, and leaving through it ends the run as
  extracted, with nothing gained and nothing lost.
- **At the gate,** you pay the boss's offering if it has one: the Heartroot's Hearth Ember already works this way. The prompt then
  says plainly what is at stake. `RaidRisk.Of` already works this out, for example: "If you fall here you lose: 3 Healing Draughts,
  2 Trail Rations. Your gear stays, worn."
- **Dying follows the raid's rules (GDD §8.3),** so there is nothing new to learn. In an arena you carry little but potions and
  food, so you lose what you brought to use up.
- **Wear has to be softer.** At 25% per failed raid, new gear breaks after four failures (the Systems page says so). A Souls-style
  run of retries would break the kit in an evening. Give encounters their own failure wear, 10% suggested.
- **The next try starts at the rest stone** by the gate: Elden Ring's answer to the walk back. You play the approach only the first
  time. A small painted map loads in a few seconds: the Woods' 200 m generated map took 4.7 s, and an arena skips the generation.
- **After the gate there is no way out but winning or dying.** The mist closes behind you. That rule is what makes it a boss fight.
- **No clock is shown.** A hidden 30-minute limit, Monster Hunter's safety net, ends a stuck run as a time-out. One warning comes
  two minutes before: "The mist is closing in."

### 1.4 The fog-gate moment, from above

A Souls fog wall is a third-person moment. Under our top-down camera it is built from parts that mostly exist already:
1. A wall of mist lies across the only way on. "[E] Step into the mist" opens a confirmation that shows the stake.
2. The mist closes behind the player: a collider, and a block in the navmesh.
3. A `CameraZone` pulls the camera out over the arena, so a 4.6 m troll and his boulders stay on screen.
4. An `InGameCinematic` of 3 to 6 s shows the boss.
5. `BossBar.ShowCard` shows the name card, the boss's `Music` starts and `BossEncounter.Wake` raises it. All three exist.

At the Brukam, the gate is also the toll: pay, or fight.

### 1.5 Co-op

- **Following.** Friends follow the host as they do now. Today the portal offers to follow while the host's raid has a minute or
  more left, but an encounter has no clock. It should offer to follow until the gate closes.
- **Going through together.** Everyone alive on the run gathers at the gate. It shows "2 of 3 here", and all go in when the host
  steps through. A friend who arrives later is told "The fight has begun".
- **What already works.** The boss is run by the host's game. Each friend adds 60% to its health. Friends who go down can be helped
  up (Monster Hunter's faints). Phases follow the boss's health, which friends already receive (`BossEncounter`'s friend's-copy
  rule). So arena changes driven by phase run in every game. Anything random about them, such as which stones fall, must come from
  the run's seed.
- **Who gets the win.** Today, a guest's first win counts as the boss beaten in their own Haven (`BossBeatenHome`). For a boss that
  ends an act, that lets a guest skip their own story. Suggested rule: a guest whose quest line hasn't reached the encounter keeps
  the drops and the bond with their companion, but the boss stays unbeaten in their own Haven. This is a question for you (§4).
- **Not yet played together.** ARCHITECTURE says that "a boss fight hasn't been played through together yet". That has to be done
  before encounters ship in co-op.

### 1.6 Tiers, the first kill and rematches

- **The first kill is at one fixed tier,** tuned to the kit the player is expected to have when the quest gets there: Heartroot I,
  Brukam II, Nøkk III, Rime-jutul IV. The story fight has no tier picker.
- **After that, the encounter stays at the portal as a rematch,** at its raid's tiers (the Brukam at II to IV).
  - The tier's multipliers already strengthen the boss at the start of the raid (`RaidCreatures.StrengthenAll`). A boss raised by
    the gate must be prepared the same way, as spawned creatures are.
  - `MonsterAttack › From Tier` gives it new moves at higher tiers, as Hades' Extreme Measures does.
- **A rematch pays the boss's drops at that tier:** Brukam's Tusk, Trollstone, the Lindorm Scale and so on, which
  RaidIdentityResearch §4 makes tier V materials. The skill is given once only, and that is already how it works. Nothing like a
  weekly lockout is needed: the offering paces rematches.
- **A rematch is only worth playing if its parts are needed.** RaidIdentityResearch found that three trophies lead nowhere yet, and
  that has to be fixed first.

### 1.7 What stays in expeditions

Elden Ring splits its bosses: field bosses stand in the open, story bosses wait behind fog. Safe Haven can do the same.

- **The Brukam.** Today he stands on the head bridge in every Troll Valley raid from tier II, with his bar, his music and Mountain
  Leap.
  - **Recommended: move him into his encounter,** in one step.
  - The expedition's head bridge becomes a toll bridge, like the others. His camp and hoard stand across the river.
  - Expeditions then hint at him: his footfalls, and a "boulders thrown from the heights" event.
  - After the kill, the head bridge lies broken in expeditions, because he broke it.
  - Keeping him in both places would split his tuning and spoil the first-kill moment.
- **The Heartroot.** Recommended: **keep it as it is for now,** reached by its rumour and woken by the ember in the Woods.
  - It works, it is tested, and its quest is wired.
  - A boss inside an expedition is a good first boss: Valheim teaches its bosses with the first biome's, and Hunt keeps its bosses on
    the normal map.
  - It also gives the game both kinds of boss.
  - To move it later, put the Guardian's Hollow prefab and the ember choice in an arena, and let the rumour become the "find it"
    step.
- **Expeditions only:** rescues, rumours, events, caches and the quests' raid steps.

---

## 2. Encounter design

### 2.1 What a small arena gives that a generated map can't

- **Ground you can learn.** Generated maps change every run, and a boss fight is learned by trying again on the same ground.
- **Phases that change the place:** stones fall, water rises, fires go out, walls of roots rise. These are pieces placed by hand and
  switched on by phase.
- **Clear lines of sight for a top-down camera.** Walls and trees stay off the camera's side (a risk the Troll Valley plan already
  names). A camera zone gives a wider view for big bosses.
- **An approach** of 40 to 90 seconds through mist: lore, the boss heard before it is seen, and a rest stone to eat and drink a
  tonic before the fight.
- **A point of no return** and a title card.
- **Fairness:** no random events, elites, roaming creatures or waves at each danger stage.
- **Its own look:** light, colour grade, weather, music, cinematic and results art (`RaidDefinition › Results Screen`). Because the
  arena is small, polishing it by hand is affordable. It must still match the Haven's graphics ("raid look").

### 2.2 The shape every encounter shares

**Arrival stone** (an exit home) → **approach** (40 to 90 s, the first time only) → **rest stone** (later tries start here) →
**gate** (offering, stake, confirmation) → **arena** (about 40 to 60 m across) → **the way home** (opens where the boss falls; a
short hold, no alarm).

A run takes 8 to 15 minutes, loading included.

### 2.3 Four sketches

**The Heartroot: Guardian's Hollow** (if it moves; it stays in the Woods for now)
- **Approach.** A sunken path between dead trees in thick mist. Its breathing is heard before the stump is seen, and old offerings
  from the pact lie along the way.
- **Gate.** Laying the Hearth Ember in the split stump is the gate: the existing choice. The ring of dead trees closes behind as
  roots rise.
- **Phases.**
  1. Roots and slams in the open hollow.
  2. At 66% ("roots begin to tear free"), walls of roots rise and split the hollow into lanes, and it reaches through them.
  3. At 33% ("the old heart splits"), the canopy closes. The split heart's glow is the only light, so you see its blows best when
     you stand near it.
- **After.** "Hearth-keeper." The canopy opens to the sun, the act's own answer. Rewards: Heartroot's Grasp and the Guardian
  Heartroot.

**The Brukam: Brukam's Bridge** (GDD §10.7; build this one first)
- **Approach.** The Troll's Road climbs a narrow gorge. Toll stones carry coins and bones, the river roars below, and his hoard sits
  on a ledge across the gorge.
- **Gate: the toll post** at the near end of the bridge.
  - "Pay the toll" (a Hearth Ember and Life Orbs, as decided) ends it in peace: the Marsh opens, but there is no Mountain Leap.
  - "Fight", and he rises at the far end.
- **Arena.** One stone bridge, about 45 m long and 8 m wide, built in six to eight segments. His camp stands on the far bank. The
  hoard is on a ledge below one side.
- **Phases.**
  1. From full health to 66%: wide club sweep, slam and thrown boulders, over the whole bridge.
  2. At 66% ("tears a stone from his own bridge"), segments behind and beside him fall in a fixed order, with cracks and dust 2 s
     before each. Safe ground shrinks, but gaps open behind him: GDD §10.7's "openings behind the boss". He throws the stones he
     tears out.
  3. At 33% ("hide greys to stone: bring a hammer"), the side cache comes into play. A narrow stair from the middle of the bridge
     leads down to the hoard, and taking it means leaving safe ground while he throws. If he breaks the middle span, the hoard goes
     into the river. The GDD's "as time runs low" becomes "as the bridge runs out".
- **Falling is not death.** The river carries you to the near bank with heavy damage, and you climb back. A deadly fall is hard to
  judge from above (the raid rule today: only a real fall kills).
- **After.** "The valley goes quiet", and the way home opens on his camp. Rewards: Mountain Leap, Brukam's Tusk and Trollstone.

**The Nøkk of the Black Pool** (NextRaidResearch §6.3)
- **Approach.** Duckboards into the Drowning Fen at dusk. A fiddle plays, and faces in the water call in borrowed voices.
- **Gate.** The last duckboard. If "The Nøkk's Tune" is done, the bargain is offered here.
- **Phases.**
  1. A white horse charges over the hummocks and carries a companion off towards the deep.
  2. As the fiddler in the water, it pulls you towards the deep and charms creatures to its side. A steel needle thrown into each
     side pool shuts it out of that pool, so the arena tightens on it.
  3. The black pool rises (the water surface lifts), and the safe hummocks shrink. Naming it makes it reel.
- **Reward.** Nykk's Air (or Drowning Grip) and Nykk's Mane. Winning opens the Frost Peaks.

**The Rime-jutul: the Jutul's Cut** (RaidIdentityResearch §3.3)
- **Approach.** A line of cairns climbs a gorge in blowing snow. The cold takes your warmth, and a bell rings.
- **Gate.** Light the first cairn-fire, which costs firewood. Snow then closes the mouth of the gorge.
- **Phases.**
  1. It hurls boulders at your fires, not at you: keep them lit.
  2. A blizzard. Three cairn-fires keep you warm, and each lit fire thaws its rime armour so it can be hurt.
  3. Dawn reaches the gorge, and it starts to turn to stone. Win before it sets and the Rimeheart is yours. If it sets, the fight is
     won but you get only Trollstone. The fight's own clock, on purpose.
- **Reward.** Hearthguard and the Rimeheart.

---

## 3. Implementation in this codebase

### 3.1 What is reused

| Needed | Already there |
|---|---|
| A boss with phases, a bar, a name card, music, a wake, a reward given once, a trophy and home lines | `BossDefinition`, `BossEncounter`, `BossBar`, `SettlementState.MarkBossDefeated` |
| Unlock by boss / by quest | `RaidDefinition › Unlocked By`; `QuestCondition` (Started/Completed), already used by `RaidPortal` repairs and `GameEventTrigger` |
| A quest step done by the kill, counting a kill made earlier | Kill Monster objective with Count Already Owned |
| Launch, the save before leaving, crash recovery, the loadout, the report, wear, companions, experience, the journal, results | `RaidLauncher`, `SaveGameService`, `RaidDirector.Finish`, `RaidResultsScreen` |
| A painted map that is not turned | `RaidMap` without Layout Rules, with `rotate` off |
| Strength by tier | `RaidDifficulty`, `RaidCreatures.StrengthenAll`, `MonsterAttack › From Tier` |
| An offering or toll with an item cost, and the boss raised by it | `RaidEventDefinition` choices and costs, `SpawnCreaturesOutcome` (the Heartroot today), the story graph's `ChoiceNode` |
| Framing and cinematics | `InGameCinematic`, `CameraShot`, `CameraZone`, `RaidDefinition › First Visit Cinematic` |
| Arena changes, made by designers | Story graph nodes: Set Active, Play Animation, Move Object, Shake Camera, Change Weather / Time Of Day, Spawn Enemies, Play Cinematic, Music |
| The way home | `ExtractionPoint` (hold, cost), `RaidDirector.Extract` |
| What's at stake | `RaidRisk.Of` |
| Co-op | `RaidInvite`, `TryFollow`, `CoopCreatures` (the boss's copy follows the host's health), `CoopDowned`, `CoopSpectate` |

### 3.2 Three ways to model it

| | A. `RaidKind` on RaidDefinition | B. Its own definition and pipeline | **C. A destination, plus an encounter block (recommended)** |
|---|---|---|---|
| What | `enum RaidKind { Expedition, Encounter }`. The encounter's fields (boss, quest condition, gate cost, wear) go inline on `RaidDefinition` | A `BossEncounterDefinition` with its own `EncounterLauncher`, `EncounterSession` and `EncounterDirector` | `RaidDefinition` stays the destination. A reference to a new `BossEncounterDefinition` asset makes it an encounter (`IsEncounter`). The behaviour lives in a `BossEncounterSystem : RaidSystem` and three scene parts |
| For | Fewest files. Everything reused | Clean data: no expedition fields on a boss fight | Everything reused (save, crash, co-op, results, quests, playtest log, cheats). The boss fight's data is in one asset designers can find. It follows the project's way of adding raid behaviour ("a new system is a new `RaidSystem` subclass") |
| Against | `RaidDefinition` already has about 60 fields. More `if (kind == …)` checks spread through it | It duplicates the riskiest code: `RaidLauncher.Go`, `RaidDirector.Finish`, `raidUnderWay` crash saves, `RaidInvite`, quest `RaidFinished` hooks, and every reader of `RaidDirector.Current` (creature strength, sleep, companions) | Still needs three small branches in `RaidDirector` and a page in the planner |
| Size | M | L to XL, high risk | M |

**Why C.** It costs about the same as A, keeps the encounter's data separate as B does, and doesn't copy B's risks. The few rules
an expedition has and an encounter doesn't are mostly data already:
- an encounter has empty stage and system lists;
- it has no rumour pool or events in its scene;
- it has no Layout Rules.

So the director needs only a handful of new branches.

### 3.3 The arena: hand-built scenes or a generated "Arena" shape

| | Hand-built scene per boss (recommended) | A tiny generated "Arena" layout shape |
|---|---|---|
| For | The ground is the same every try. Phase pieces, cinematics and the camera are placed exactly. A small terrain loads fast. The painted-map path exists already ("a raid without layout rules plays its painted scene") | It reuses each raid's look and generator |
| Against | One scene per boss to build in the Editor, and its look to match | Every run would differ, which is wrong for a fight you learn. The generator can't place a breaking bridge, rising water or cairn-fires exactly. Terrain copies only grow today (`RaidSizeOption` scales up) |
| Size | M per scene | L, and less good |

Build each arena from an **arena template scene**, made the way Troll Valley's scene was split off:
- the raid rig that `RaidSceneRigTests` checks (`RaidMap`, `RaidGenerator`, `RaidDirector`, weather, day and night, `SceneAudio`,
  terrain surface, moss and mist, `GameStateManager`);
- a painted terrain of about 120 m, not turned;
- no rumour or event directors;
- the raid's own terrain layers, grade and weather assets, so the biome matches its expeditions.

### 3.4 Option C, file by file

| Area | Change | Files | Size |
|---|---|---|---|
| Data | New `BossEncounterDefinition`: the boss, Opens When (`QuestCondition`), Gate Cost (items), Wear On Failure, Time Limit, gate and stake text, story tier. `RaidDefinition` gets `encounter` and `IsEncounter`. `IsLocked` also checks the encounter's condition. A new optional `unlockRequires` (`QuestCondition`) on the next raid, so that a paid toll or bargain unlocks it too | new `Scripts/Raids/Encounters/BossEncounterDefinition.cs`; `Raids/RaidDefinition.cs` | S |
| Launch | Refuse Sparks, a large map and runes for an encounter. Leave the horse and cart at home | `Raids/RaidLauncher.cs`, `Transport/HorseTravel` call site | S |
| Director | For an encounter: no warnings and no "reach an extraction within…" line, but "Beyond the mist: the Brukam" instead. It ticks only the hidden limit, installs the encounter system, and uses the encounter's wear in `Finish` | `Raids/RaidDirector.cs` | S |
| System | `BossEncounterSystem`: starts at the rest stone once the gate has been reached, opens the way home when the boss falls, writes a playtest row for each try | new `Scripts/Raids/Encounters/BossEncounterSystem.cs` | M |
| Scene parts | `EncounterGate`: prompt, the stake from `RaidRisk`, pays the offering, counts friends at the gate in co-op, seals the mist, plays the cinematic, raises or wakes the boss and strengthens it as spawned creatures are. `EncounterRestStone`. `ExtractionPoint › Opens When The Boss Falls` | new `Scripts/Raids/Encounters/*`; `Raids/ExtractionPoint.cs` | M |
| Boss hooks | `BossEncounter.PhaseEntered` and `Fell` events. A new story trigger, **When Boss** (reaches phase N, or falls), so each arena's changes are a story graph made by designers | `AI/BossEncounter.cs`, `Story/StoryTriggers.cs` | S |
| HUD and results | No timer in an encounter. One line: "Bring down the Brukam", then "The way home is open". The results screen's title: "The Brukam has fallen" | `UI/RaidHudView.cs`, `UI/RaidResultsScreen.cs` | S |
| Planner | The encounter's page: name and title, painting, the stake, the gate's price; tiers only for rematches; no Sparks, map size, hour or rumours. Its tab is hidden until it is open and marked once beaten | `UI/RaidPreparationWindow*.cs`, `Raids/RaidPlan.cs`; the portal's destinations (data) | M |
| Quests | Data only: the quest step starting opens the encounter; the quest being done (kill or toll) opens the next raid | `Data/Quests/*`, `Data/Raids/*.asset` | S |
| Saves | "Reached the gate" for each encounter, kept beside `raidsVisited` | `Progression/CharacterProgress.cs`, `Save/SaveMapper.cs`, `SaveData` | S |
| Co-op | Following is offered until the gate seals. A "ready at the gate" message. The rule for a guest's win | `Multiplayer/RaidsTogether.cs`, a `CoopLink` message, `WireFormat` and `CoopWireTests` | S to M |
| Tests | Unlocking; the planner hides Sparks; no clock; the phase trigger fires once per phase, on the host and on a friend's copy; the gate seals; the rest-stone start; `RaidSceneRigTests` over the arena scenes | `Editor/Tests/*` | M |
| Content, per arena | A scene from the template; approach dressing; the gate's mist; phase pieces; a story graph; a cinematic; a camera zone; music; results art | `Scenes/Raids/Encounter_*.unity`, `Data/Raids/*`, prefabs | M to L each |

### 3.5 Recommended build order

1. **Framework.** Data, the director's branches, the gate, the way home on a fall, the When Boss trigger, the planner page, the HUD,
   saves and the co-op follow rule, with their tests.
2. **The arena template scene,** built in the Editor through the CLI. The bulk scene builders are retired, so this is done by hand.
3. **Brukam's Bridge.**
   - The gate is the toll.
   - The bridge in segments: its LocalSpawn model has a single deck today, and the Troll Valley plan already meant to cut it into
     segments.
   - The hoard ledge, his phases in a story graph, a camera zone and a cinematic.
   - Playtest it.
4. **Move the Brukam.**
   - In Valley › Boss Bridge, the head bridge becomes a toll bridge with his camp across the river.
   - "The Toll" opens the encounter, and "The Toll" done opens the Moss Marsh.
5. **Tune from the playtest rows** (one per boss try: tier, phase reached, seconds, the outcome): the wear, the rest stone and the
   boss's health.
6. **Then decide the Heartroot,** and build the Nøkk's and the Rime-jutul's arenas as their raids are built.

---

## 4. Risks and questions

**Risks**
- **Readability from above.** A big boss, thrown boulders and a bridge that falls apart must all read from a top-down camera. That
  needs a wider view, cracks shown well before a segment falls, and no walls on the camera's side.
- **Each arena is handmade:** a scene, a story graph, a cinematic and art. That is M to L of content each, on top of its raid.
- **Co-op.** Arena changes must happen the same way in every game. Driving them by phase, with anything random taken from the seed,
  covers it, but no boss has been fought together yet.
- **The move between versions.** If the Brukam stands both on the expedition's bridge and in his encounter for a while, his tuning
  and his first-kill moment split. Move him in one step.
- **Little to lose, so little tension?** An encounter has nothing to carry out, so dying stings less than in an expedition. That is
  the point (it tests skill, not greed), but the softer wear and the rest stone must not make retries feel free of risk.
- **The story in co-op.** Unless the guest-win rule changes, a guest can beat an act's boss before their own quests reach it.

**Questions for you**
1. **Name:** "Encounter", or something with more flavour? *Holmgang* (Old Norse: a duel on a marked-off island) fits "a small map,
   different rules".
2. **The Heartroot:** keep it in the Misty Woods as it is (recommended for now), or give it an arena too?
3. **The Brukam:** take him out of Troll Valley's expeditions and into his encounter (recommended), or keep him on the bridge until
   "The Toll" starts?
4. **After the gate:** no way out but winning or dying (recommended), or an item that lets you flee as a failure?
5. **Gear wear on a failed encounter:** 10% (recommended), or the raid's 25%?
6. **Companions:** may they come? If one falls in an encounter, is it lost like on a raid, or carried home by the mist (recommended)?
7. **A friend helping with a boss their own story hasn't reached:** does it count in their Haven (today's rule), or do they get only
   the drops (recommended)?
8. **Rematches** at higher tiers, for the boss's parts (recommended), or one fight only?
9. **The Rime-jutul** needs a quest step. Should it come between "The Door in the Snow" and "The First Hearth"?

---

## Sources

Official or primary:
- [Bandai Namco: Elden Ring, introduction to the multiplayer system](https://en.bandainamcoent.eu/node/7918): fog barriers,
  summoning pools near bosses, a boss's defeat ends the co-op session, up to two guests.
- [Crytek: Meet Scrapbeak](https://huntshowdown.com/news/meet-scrapbeak-our-new-boss): the rules of boss lairs, and how Scrapbeak
  breaks them.
- [Store norske leksikon: holmgang](https://snl.no/holmgang).

From search summaries (not opened or checked in full):
- [Supergiant: Hades patch notes](https://www.supergiantgames.com/blog/hades-welcome-to-hell-update-patch-notes/): bosses changed
  under Extreme Measures.
- [Blizzard support: loot lockouts](https://support.blizzard.com/article/000269958).
- [GamerBraves: Kitao on the Stakes of Marika](https://gamerbraves.com/?p=108158). The page refused to load (403), so the summary
  was not checked.

Secondary (guides and wikis):
- Valheim: [PC Gamer](https://www.pcgamer.com/au/valheim-boss-fights-guide),
  [Windows Central](https://www.windowscentral.com/valheim-boss-guide-how-find-summon-and-defeat-every-boss).
- Monster Hunter: [MH wiki, MHWorld optional quests](https://monsterhunterwiki.org/wiki/MHWorld/Quests/Optional_Quests),
  [Game8, fainting in Wilds](https://game8.co/games/Monster-Hunter-Wilds/archives/498751),
  [PC Gamer on Wilds' arena quests](https://www.pcgamer.com/games/rpg/monster-hunter-wilds-first-title-update-is-overflowing-with-new-stuff-a-long-awaited-grand-hub-arch-tempered-monsters-arena-quests-and-most-importantly-fashion/).
- Hunt: Showdown: [Bloxodes wiki](https://bloxodes.com/wiki/hunt-showdown).
- V Rising: [Pro Game Guides](https://progameguides.com/v-rising/all-v-blood-boss-locations-in-v-rising/).
- Returnal: [PowerPyx](https://www.powerpyx.com/returnal-permanent-unlocks-explained/).
- Destiny: [GameRevolution](https://www.gamerevolution.com/guides/435063-destiny-2-reset-raid-checkpoint-progress).
- ARC Raiders: [PC Gamer](https://www.pcgamer.com/games/third-person-shooter/arc-raiders-matriarch).
- Dark and Darker: [wiki, Escape Portal](https://darkanddarker.wiki.spellsandguns.com/Escape_Portal).

In the project:
- GDD §2.3, §4.4, §7.4, §8.3, §10.7 and §16.5 to 16.6.
- ARCHITECTURE: "Going along, set-pieces and bosses", "Generated raid maps", "Raid tension", "Co-op", "In-game cinematics",
  "Camera zones".
- QuestRedesign.md; QuestAndStoryResearch.md §3.7.2 and §3.7.3; TrollValleyPlan.md; NextRaidResearch_2026-11-17.md §6.3;
  RaidIdentityResearch_2026-11-17.md §3.3, §6 and §7.
- `BossDefinition.cs`, `BossEncounter.cs`, `RaidDefinition.cs`, `RaidLauncher.cs`, `RaidSession.cs`, `RaidDirector.cs`,
  `RaidMapGenerator.Bridges.cs`, `QuestCondition.cs`, `StoryTriggers.cs`.
- `Boss Brukam.asset`, `Boss The Heartroot.asset`, `Rumours/TheHeartroot.asset` and its event, `Brukam's Bridge.asset`,
  `TrollValley.asset`.
