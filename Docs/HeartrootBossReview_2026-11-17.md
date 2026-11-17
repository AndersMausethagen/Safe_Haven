# The Heartroot as a boss fight: review and fixes (2026-11-17)

The user: "Analyse the boss mechanic. How does the boss fight feel and work in Safe Haven now? Is it just a boring damage sponge that
we must hit for 10 minutes before we win? How can we make it more exciting? [...] Focus on the first boss fight for now, and let's
make that one as good as possible."

This is a review and a design, and nothing is built. It reads today's assets and code, and critiques
`Docs/HeartrootEncounterDesign_2026-11-17.md` ("the encounter design" below), keeping most of it. The times are estimates. The
playtest rows must confirm them.

## The short answer

- **Today it is not a ten-minute sponge.** It is a fight of about four minutes. The first two thirds are a metronome: one slam
  every 3.1 s, and the boss stands idle for over half of that. The last third is a wall: a knockdown slam every 1.45 s.
- **The real sponge risk is the plan to double its health to 2,900** without changing what the player does second to second. That
  would mean about 370 club blows on bark with nothing changing between them.
- **The fix is structure, not health.** Give it more verbs, each with its own answer. Make good play turn into big openings: a
  visible poise bar, a long topple, a heart strike, and bark that breaks off. Make interrupts pay. Give each stage an opening move
  and a change of behaviour, then add the arena. Most of this is data. The code is four small additions.

**The top changes, ranked by impact per effort:** C1 a real first-stage moveset (data, S); C2 topples (S to M); C3 interrupts that
pay (S); C4 length from structure (data, S); C5 a set piece at each phase change (data, S).

---

## 1. How it plays now

### 1.1 What it has

`TheHeartroot.asset`, `Boss The Heartroot.asset` and `TheHeartrootStats.asset`:
- 1,430 health, 35 armour (×0.74 physical), 320 poise, fire −25%.
- Stages at 66% (+10% damage, a 1.8 s reel) and 33% (+20% more, a 2.2 s reel).
- No weights, follow-ups, holds, unblockables, When Foe, Below Health or Hurt Mood. All of those exist in the code, and no boss
  uses them.

Damage is shown raw, then as what reaches the tier I entry kit: 150 health, ×0.76 after 32 armour, and the stage's bonus.

| # | Entry | Clip | From | Wind-up and shape | Damage | Cooldown, priority |
|---|---|---|---|---|---|---|
| 1 | Guardian Slam | Strike Chop (UAL1) | Stage 1 | 0.6 s, 150° by 2.4 m | 48 → 36 (24%) | 1.8 s, 10 |
| 2 | Root Eruption: 3 lobs that root | Strike Overhead (UAL1) | Stage 1, but **only 3 to 18 m away** | Rings fill over 1.0 s | 42 → 32 (21%) | 11 s, 20 |
| 3 | Ground Slam | Strike Overhead, the same clip at 0.37 speed | Stage 2 | 1.15 s, all round, 4.5 m, red edge | 64.8 → 54 (36%: a knockdown) | 7 s, 15 |
| 4 | Call Roots: 2 Rootlings, then Open After 4 s | Roar (the treant's Taunt) | Stage 2, **only 2 m or more away** | 0.9 s, blue | none | 16 s, 22 |
| 5 | "Fast slam" | Strike Chop: **the same attack asset and wind-up as #1** | Stage 3 | 0.6 s | 55.2 → 54.5 (36%: a knockdown) | 1.1 s, 12 |
| 6 | Regrowth: mends 12% over 6 s | Roar | Stage 3 | 0.9 s, blue | none | 30 s, 30 |

### 1.2 Second to second

**Stage 1 has one move for a melee player.** The lob needs 3 m, so in club reach it only slams: a 0.6 s fill, the blow, recovery
to 1.3 s, then it stands and turns through its 1.8 s cooldown (`MonsterAttacks.Finish` allows a strike 0.15 s later, but nothing
else is ready). A 3.1 s cycle: winding up 19% of the time, recovering 23%, idle 58%. A tier I Root Guardian has exactly this
moveset (`RootGuardian.asset`). **The first third of the first boss is a common enemy with more health.**

**Stage 2** adds a heavy ring about every 9 s, and Rootlings if you stand back.

**Stage 3 is a cliff.** The "fast slam" is the same 0.6 s wind-up and clip on a 1.1 s cooldown: a slam every ~1.45 s, each a
knockdown, openings down from ~2.5 s to ~0.85 s. Regrowth's 0.9 s blue fill mends 172 health (36% of the stage) unless a blow
lands in time.

**Length:** about 65 s, 75 s and 95 s or more: about four minutes. **Playtests:** the logs hold 13 Heartroot kills, all probes
(killed 0.1 to 0.6 minutes after the event). Nobody has fought it by hand.

### 1.3 Does the player's toolkit matter?

| Tool | Today |
|---|---|
| **Parry** | Optional. Light blows break its poise anyway (a club chain: ~44 in 2.3 s). A parry takes 128 and gives a 1.3 s stagger, about one chain. With 58% idle, parrying is style. |
| **Dodge** | One slam, always on the same beat. |
| **Backstab** | Bosses open only while staggered: 1.3 s, one blow if you are already behind. |
| **Positioning** | Doesn't matter in melee: the lob and calls only come if you stand back. It must turn before it strikes (`Engage`), and nothing punishes its flank, so circling it likely gets free hits (check in Play). |
| **Interrupt** | Regrowth only, in stage 3, and the window is 0.9 s. |
| **Flasks** | A 1.3 s drink fits in any idle gap in stages 1 and 2. Nothing needs a cure. Stage 3 leaves no gap. |
| **Stamina** | Rarely: one roll per 3 s. |

**Verdict.** The danger comes from damage per hit, not from decisions. It is "don't get hit by the one move", then "don't get hit
by the one move twice as often".

### 1.4 Look and feedback

- **Three attack clips for six entries.** Ground Slam and Root Eruption both play Strike Overhead (slowed to 0.37, floaty); Call
  Roots and Regrowth both play Roar. Two of the three are humanoid UAL1 clips on a treant.
- **The treant's own Attack_1 (40 frames), Attack2 (25) and Attack3 (30)**, and Death2 and Death3, are imported as Humanoid in
  `Art/Enemies/Downloaded/Treant2/Treant2Clips.fbx` and unused. `Treant2.overrideController` (only on the Heartroot) overrides
  Idle, Run, Death1 and Taunt (as Roar).
- **The boss bar shows only health:** no poise, no phase marks. The system that turns skill into openings can't be seen.
- **It never flinches:** that needs a blow worth 4% of its health (57); a club blow does 8 to 12.
- **A phase change is a line, a glow, a shake and more damage.** Nothing in the arena or its behaviour changes.

### 1.5 The encounter design: what to keep and what to fix

**Keep:** the lesson table (one deciding move per skill), Grasping Roots, Hammerfall with its hold, Bough Flail, weighted bands,
deterministic arena changes by phase (they run in co-op), the root walls and Thornwave, Heartrot with the Antidote, the rest stone,
the gate, the rage clock, and its catch of the "Frost Rune" slip in `HeartrootGrasp.asset`.

**Fix:**
1. **2,900 health makes the length out of bark.** Nothing pays more for playing well than for playing safe (C2, C4).
2. **Regrowth, a 2.4 s fill that mends 12% (348),** is broken by anyone in melee, so it only taxes a player drinking or rolling
   away, and one slip costs ~50 s of work. Breaking it should be a reward (C3).
3. **The stage 3 "fast slam" is spam, not speed.** Remove it (C5).
4. **Stage 3 piles everything on:** about 13 moves in the dark, plus drips. Retire moves by stage; drips only at the rage (C5, C8).
5. **Two blue moves in stage 2.** Teach the interrupt in stage 1 (Call Roots), test it in stage 2 (Regrowth).
6. **Nothing in it is about poise, animation or sound** (C2, C6, C9).

---

## 2. What makes boss fights exciting, briefly

1. **Tells that mean one thing, in colour and shape.** The same cue must always mean the same outcome: a red-glowing hammer is
   always unblockable [1]; in Lost Ark blue means a counter can stop it [8]. From above: one shape per threat type, 3 to 5
   families per boss, 650 to 900 ms wind-ups for medium blows and 900 ms or more for heavy ones, no shake during wind-ups, and a
   muted-replay test [11]. *Safe Haven already has this colour language; the Heartroot just doesn't use it.*
2. **Pacing by patterns.** Players get bored when they decode patterns faster than new ones arrive; change timings only once the
   old ones are learned; **windows in proportion to the wait** [1]. Psychonauts 2 builds every move as tell, attack, recovery,
   chained and tuned per phase [2].
3. **A meter that turns good play into a big opening.** Sekiro's posture break opens a deathblow [5]. Elden Ring's stance break
   opens a riposte, "a large chunk", and a boss's stance waits about 6 to 9 s before it recovers, so poise builds across exchanges
   [4]. Diablo IV has a boss stagger bar [3]; Monster Hunter's flinches and topples are "a good time to attack" [7].
4. **Breaking things changes the fight.** Staggering Diablo IV's Ashava breaks its arm blades, and "areas of the battlefield that
   were dangerous, become safer" [3]. Dark Souls III's Curse-Rotted Greatwood, a tree, shrugs off blows to its bark, but each sac
   popped stuns it, and the last one collapses the floor [6].
5. **Phases change behaviour and the arena.** False Knight rages and brings the ceiling down [10]. V Rising's Willfred turns to a
   feeding frenzy, Ziva changes weapons, Talzur sets the ground ablaze [9].
6. **Preparation is an answer.** The Greatwood is weak to fire [6]; V Rising suggests a brew for Raziel [9]; Monster Hunter's rage
   brings new attacks [7].

---

## 3. Changes, ranked by impact per effort

**Phase numbering.** "From Phase 0" is stage 1 (100 to 66%), 1 is stage 2 (66 to 33%), and 2 is stage 3. Keep the 0.66 and 0.33
marks.

**Copies.** Guardian Slam, Ground Slam, Call Roots and Root Eruption are shared with the Root Guardian, so make Heartroot copies
of the `AttackDefinition`s. A new array element copies the one before it, so reset every field.

### C1. A real stage 1 moveset (data, S): the biggest gain for the least work

Six moves in five families, all with flags the code already has. Each new move first appears on its own, held back with **Below
Health** (0.97 means "once 3% is gone"), so the player learns one tell at a time. Remove the fast slam (#5 above): pressure comes
from strings. The cooldowns give a rhythm (a string, its opening, another string, a breath of 2 to 3 s) and an uptime of about 55%
in stage 1, against ~80% today. **Every opening is earned and marked** by the green ring.

| Move | Asset and clip | Wind-up and shape | Damage | Then | Choice |
|---|---|---|---|---|---|
| **Warden's Slam**: parry it | HeartrootSlam (Strike Chop) | 0.7 s, 150° by 2.6 m | 44 → 33 (22%) | Recovery 1.5 (0.8 open) | Weight 3, cooldown 3.0 |
| **Bough Flail**, sweep then chop: parry both (2 parries take 80% of its poise) | New HeartrootSweep (Strike Wide), follow-up HeartrootChop | 0.75 s, 170° by 3.0 m; gap 0.3; 0.55 s, 60° by 2.6 m | 30 → 23, then 38 → 29 | Chop recovery 1.6 (1.05 open) | Weight 2.5, cooldown 5, Combo Chance 1 |
| **Grasping Roots**: dodge it | New HeartrootGraspingRoots | Purple, 1.1 s, 14° by 9 m, Unblockable, Pull 3, Roots | 25 → 19 (13%), rooted 1.6 s (your guard still works) | Recovery 2.0 | Weight 2, cooldown 9, 3.5 to 9 m, When Foe Backing Off or Shooting, Below Health 0.95 |
| **Root Eruption**: keep moving | HeartrootEruption (Call clip, not Strike Overhead) | Lob ×3, 2 m rings, 1.0 s, 0.55 s apart, Roots | 34 → 26 (17%) | | Weight 1.5, cooldown 11, 4 to 18 m, When Foe Backing Off or Shooting, Below Health 0.97 |
| **Bark Burst**: don't loiter at its back | New (Shove, or the treant's Attack3) | 0.8 s, all round, 3.2 m, Pull −2.5 | 30 → 23 | Recovery 1.4 | Weight 0.6, cooldown 6, **When Foe Behind** (×3) |
| **Call Roots**: break it | HeartrootCallRoots (Call clip) | Blue, 1.4 s | Rootlings, at most 3 | Open After 3 if it finishes; C3 if broken | Weightless, priority 22, cooldown 22, minimum range **0**, Below Health 0.85 |

### C2. Topple: make the poise break the payoff (code S to M, plus data)

This is the Sekiro, Elden Ring and Monster Hunter loop in our own systems, and the main cure for the sponge. Parries and heavy
blows fill a bar you can see. A full bar floors the tree for 3 s, its heart opens, and bark breaks away.

**Data:**
- Poise 320 → **450**.
- **Topple Seconds 3.0** (an ordinary stagger is 1.3).
- **Poise Recovery Delay 5 s** (2 s for everything else).
- **Poise Growth 0.30**: each topple needs 30% more, and it resets at each phase, so a topple can't be chained into another.
- **Topple Strike 0.02**: the first backstab in a topple deals 2% of its full health on top (52 at 2,600 health). It respects
  "parry, roll behind, strike is the best blow".
- **Bark:** Armour Chips 4, Armour Per Chip 0.1, broken by topples.

**Code:**
- `MonsterDefinition`: the five fields above; 0 keeps today's behaviour for every other creature.
- `MonsterAI.Answers.TakeBlowOnPoise` and the parry's PoiseDamage branch in `MonsterAI.Reactions`: a heavyweight with Topple
  Seconds takes the existing felled path, at its own length.
- `MonsterAnimationDriver`: let Knockdown, Down and Get Up play for it (`!ai.IsHeavyweight` blocks that today).
- `PoiseMeter`: a delay per creature (set where `MonsterAI` builds it) and a growable maximum.
- `CreatureWounds`: a chip on a topple. Today a chip needs a blow worth 7% of health, which never happens to a boss.
- The topple strike, where backstab damage is scaled (`DamageCalculator`).
- `BossBar`: a thin amber poise strip fed by `BossEncounter` from `MonsterAI.Poise` (pulsing below 30%, as `ReelingTell` does),
  and marks at 66% and 33%.
- **Co-op:** the stagger already comes back from the host. If the felled flag isn't in the pose message, add it, raise
  `MultiplayerSettings.WireFormat` and update `CoopWireTests`.

**What it gives (estimates).** One club topple is worth about 100 health (a rush backstab ~20, the strike 52, a chain at ×1.2). An
average first win topples it about 3 times a stage: ~35% of its health from earned openings. A perfect parrier gets about 4.

### C3. Interrupts that pay (code S, plus data)

**Code:**
- `MonsterAttack` › **Reel When Broken** (seconds).
- In `MonsterAI.Reactions.OnDamaged`, read `attacks.Action` before `TryInterrupt`. If the strike was broken, call
  `StaggerAtLeast(reelWhenBroken)`.
- Today a broken call only waits 0.5 s, and Open After runs only when the call *finishes*: it opens itself after healing, which is
  backwards.

**Data:**
- **Regrowth** (stage 2 on, edited in place): blue, Hit Delay **1.8**, mends **8%** over 6 s, cooldown 35, Reel When Broken
  **3.0**, Open After 0.
- **Call Roots:** Reel When Broken 2.5.

Breaking a blue move becomes the best moment of the fight, as a Lost Ark counter is [8], instead of a tax.

### C4. Length from structure, not health (data, S)

- **Health 2,600 at tier I** (×1.82), not 2,900. **The rage at 7:30** (Enrage Seconds 450).
- **Estimates:** skilled and prepared, 4:00 to 4:30; an average first win (meal and tonic, some parries, one Regrowth missed),
  5:30 to 6:30; unprepared and passive, it meets the rage with ~15% left. About 220 blows land on bark instead of 370; the rest
  comes from topples, interrupts and phase reels.
- **Tuning rule:** an average clean, prepared try ends 60 to 90 s before the rage. Add **topples** and **interrupts** columns to
  the encounter design's `BossTry` row.
- Healing Draughts stack and are limited only by the bag. That is fine: the rage turns extra draughts into survival, not time.

### C5. A set piece at each phase change, and each stage its own (data S; optional code S)

A one-time **opener** is a strike entry with priority 40, cooldown 9999, its From Phase and no weight. It comes as soon as the
phase reel ends (`SelectAttack` already puts weightless high priority first). Raise the reels to **2.5 s** (both phases), so
there is room for a backstab.

| Stage | Opener | Then adds | Retires |
|---|---|---|---|
| **2**, "roots tear free" | **Uproot**: all round, 6 m, 1.6 s, heavy (dark edge and swell), 56 → 47 (31%), Open After 2.5. It lands as the root walls rise (C7) | **Hammerfall** (0.9 to 1.0 s overhead, heavy, Hold Chance 0.4, 56 → 47, Open After 1.2, When Foe Guarding, weight 1.5, cooldown 7). **Bough Flail II**: sweep, chop, Hammerfall at Combo Chance 0.7, so 30% of the time only the sweep comes. **Ground Slam** (Heartroot copy, 60 raw, Open After 1.5, cooldown 12). **Thornwave** (as the encounter design). **Regrowth** (C3). A second Warden's Slam entry with Hold Chance 0.35 | |
| **3**, "the old heart splits" | **Heartburst**: Heart Spray once, 6 rings, Scatter 4, and the canopy closes | **Heart Spray**: Throw, 4 rings of 1.6 m, 1.2 s, 22 Blight → 29 (19%), plus **Heartrot** at 3 a second for 15 s (58, 39%), cured by the Antidote. Pools last 8 s, cooldown 14. **Bough Flail III**: sweep, chop, sweep, Hammerfall at 0.5. Regrowth's cooldown 35 → 25 (a second entry) | Call Roots and Thornwave: the walls sink and no Rootlings come in the dark |

- **Retiring moves needs `MonsterAttack` › Until Phase** (S): in `IsAvailable`, `HoldsGround` and `ChooseAnswer`. Without it,
  keep them, but stage 3 is busier.
- **Heartrot is softened** from the encounter design's 83 (55%) to 58 (39%). It is a first boss, and its harm can't be blocked.

### C6. Each move its own body and sound (M: animation, no new clips needed)

- **What the Heartroot can use.** `MonsterHumanoid` already has Strike Wide, Strike Claw, Strike Lunge, Throw, Shove, Cast with
  Cast Release, Call with Call Release, and Knockdown, Down and Get Up. Its own Attack_1, Attack2 and Attack3 are unused.
- **How to give it its own strikes.** Override in `Treant2.overrideController`, which affects nothing else:
  - Strike Lunge's clip → **Attack_1** for Hammerfall;
  - Strike Claw → **Attack3** for Grasping Roots or Bark Burst;
  - Strike Wide stays UAL1, unless Attack2 reads better as the sweep.
- **Fitting each one.** `CombatClipKit` (a monster state, and the attack fitted to its clip). Judge each on a Play-mode contact
  sheet at contact. Mind the memory trap: drive a PlayableGraph, not `camera.Render()` after posing bones.
- **Moves that move to other clips.** Ground Slam keeps Strike Overhead. Eruption and Call Roots move to Call. Regrowth keeps Roar.
  Thornwave uses Cast. Heart Spray uses Throw.
- **The topple:** try Knockdown, then Down, then Get Up on the treant. If it reads badly, play **Death2** cut at its kneel as Down.
- **Sound:** one wind-up sound per family through `MonsterAttack` › Sound, made in `Tools/Audio` with Python first (the project's
  rule): a creak (slam), a whoosh of leaves (flail), an underground rumble (roots), a deep groan (heavies), a rising sap-draw
  (Regrowth), a hiss (Heart Spray), a crack and groan (topple).

### C7. Stage 2 arena: root walls and Thornwave (M: kept from the encounter design)

Build `RisingGround`, the Thornwave projectile and the walls as the encounter design's §2.2 and §3.3 describe. Uproot (C5) is the
beat that raises them. Walls stop waves but not grabs or eruptions: a positioning rule that reads from above.

### C8. Stage 3: the split heart (M)

Keep the deep dusk, `PlayerGlow`, the heart's light (`PhaseLight`) and Heart Spray with Heartrot. Change two things: **drips only
at the rage** (the encounter design's optional row), so one hazard family comes at a time; and **the heart glows** (`BossPhase` ›
Glow), so the topple's opening shows in the dark.

### C9. Feedback and the camera (S to M)

- **The topple:** a double hit-stop on the breaking blow, the backstab's slow beat, a 0.6 shake, the crack, bark particles at the
  chest, the heart pulsing (`CreatureLook.Glow`), the green ring for 3 s.
- **Music:** a stinger and a second layer at each phase, through the story graph's Music node (built).
- **The camera:** `CameraZone` at about 32 m; no shake during any wind-up. The player must stay visible behind the 4.9 m trunk,
  where every topple backstab happens: check the game view, and add an outline if hidden (S).

### C10. The finale: the Hollow's Hold at 12% (M)

As the encounter design's §2.2 and §3.4 describe: rings of eruptions from the rim inward, and its body is the safe ground. End it in
a **5 s topple**: the kill window, and the moment players remember. Build it last in the first pass, not after the playtest (Q4).

### Considered, and not recommended

- **Weak points on the body:** `MeleeTargeting` aims at the creature, not at a knot on a 4.9 m trunk seen from above.
- **Anchor roots in the arena:** a new immobile creature, a link to the boss and co-op sync, M to L for what topples already give.
- **More health:** it only makes more bark.

---

## 4. Build order, with how to check each step in Play

The boss work doesn't need the arena. Do steps 1 to 6 on the Heartroot in the Woods: its set piece, or `sh_spawn`. Use an
invulnerable player and an in-game coroutine probe, and never `sh_save` (memory: raid probes, test saves).

1. **Measure today (S).** A probe logs each strike's start and end, each opening and stagger, to a file. *Check:* stage 1 shows a
   slam every ~3.1 s and ~58% idle (§1.2). Keep it as the baseline.
2. **C1.** *Check:* over 3 minutes every stage 1 move appears, in order as health falls; none comes three times running; openings
   show the ring; standing behind it draws Bark Burst within ~3 s; Grasping Roots pulls and roots; a parried flail breaks off with
   no NullReferenceException (the 2026-11-15 trap). EditMode test on `MonsterAttacks.SelectAttack`: no Grasping Roots at full
   health, and Call Roots first when ready.
3. **C2.** Tests: `PoiseMeter` growth and delay, topples only with Topple Seconds, a chip per topple. *Check:* two parries and two
   chains floor it for 3 s (Knockdown, Down, Get Up); the first backstab shows the strike; poise waits 5 s; the second topple needs
   more; the strip moves.
4. **C3.** *Check:* a blow in a blue fill makes it reel 3 s (Regrowth) or 2.5 s (Call Roots); a finished Regrowth mends 8%.
5. **C5 and C8's data.** *Check:* set its health to 0.65, then 0.32 (an eval): a 2.5 s reel, the opener once, Hammerfall's hold
   catches an early roll, Heartrot ticks, the Antidote cures it; with Until Phase, no Call Roots or Thornwave in stage 3.
6. **C6.** *Check:* contact sheets at contact; in the game view at camera-zone distance each move is told apart muted, and by
   sound alone.
7. **C4:** health 2,600, the rage at 450 s, `BossTry` and `BossPhase` rows with topples and interrupts. A probe checks pace only.
8. **The arena** (C7, C8's scene): the encounter design's build steps 2, 5 and 6, with its Play checks (its §5, step 8).
9. **C9, then C10.** *Check:* a full timed try with the entry kit; the finale's rings leave the 6 m around it clean.
10. **Finish:** `RunTests.ps1 -All`, a review subagent on the new code, the user's playtest. Tune health, then poise growth, then the
    topple strike, then wind-ups.

---

## 5. Questions for the user

1. **Length.**
   - 2,600 health with topples: a first win in about 5:30 to 6:30, the rage at 7:30.
   - Or the encounter design's 2,900 and 8:30, with no topples?
   - **Recommended: 2,600 with topples.** The time it takes is decided by how well you play, not by bark.
2. **The topple payoff:** a heart strike (2% of its health on the first backstab of a topple), and bark that breaks off and lowers
   its armour. This is a rule for bosses only. **Recommended: yes.** It is what makes parrying the boss worth it.
3. **Preparing with fire:**
   - raise its fire weakness from −25% to −40%, so Fire Arrows and the Hand Torch are a choice when packing;
   - add no weakness to a kind of blow, so the entry club is never the wrong weapon.
   - **Recommended: yes.**
4. **The finale (Hollow's Hold):** in the first pass, built last, or after the first playtest? **Recommended: the first pass.**
   "Epic" lives in the last 30 seconds.

---

## Sources

Opened and read:
1. Alex Kubodera (Death's Gambit),
   [Designing for difficulty readability in ARPGs](https://www.gamedeveloper.com/game-platforms/designing-for-difficulty-readability-in-arpgs),
   Game Developer.
2. Rebecca Vessal, GDC 2023 (Psychonauts 2),
   [a modular system of maneuvers for boss fights](https://www.gamedeveloper.com/marketing/using-a-modular-system-of-maneuvers-to-design-i-psychonauts-2-i-s-boss-fights-in-a-hurry),
   Game Developer.
3. [BlizzCon 2019, Diablo IV systems panel transcript](https://diablo.blizzplanet.com/blog/comments/blizzcon-2019-diablo-iv-systems-and-features-panel-transcript/5):
   the stagger bar and Ashava's blades.
4. [Elden Ring wiki: Stance](https://eldenring.wiki.gg/wiki/Stance). Community wiki.
5. [Activision support: Sekiro game mechanics](https://support.activision.com/sekiro/articles/sekiro-shadows-die-twice-game-mechanics).
6. [Dark Souls III wiki: Curse-Rotted Greatwood](https://darksouls3.wiki.fextralife.com/Curse-Rotten+Greatwood). Community wiki,
   with some contradictions on the page.
7. [Kiranico: Understanding Monster (MH World)](https://mhworld.kiranico.com/guide/understanding-monster).
8. [MeinMMO: Lost Ark, countering bosses](https://mein-mmo.de/en/lost-ark-how-to-counter-attacks-from-bosses,787486).
9. Martin Lövgren, [V Rising boss encounters](https://blog.playstation.com/?p=392590), PlayStation Blog.
10. [Nintendo Life: Hollow Knight boss moves](https://www.nintendolife.com/guides/hollow-knight-boss-locations-and-moves-list).
11. [GamineAI: telegraph shape language for top-down boss fights](https://gamineai.com/blog/enemy-telegraph-shape-language-top-down-boss-fights-fast-visual-consistency-audit-2026).
    One author's guidelines, not a standard.

From search summaries only, not opened: Hades' Extreme Measures and the Furies (the wiki pages refused to load). Valheim's bosses
are covered in `Docs/BossEncounterResearch_2026-11-17.md`'s sources.

In the project: the Heartroot's and Root Guardian's data, attack assets and stats; `KnottedClub` and its Chop attacks;
`CombatTechniqueSettings.asset`; `Treant2.overrideController`, `Treant2Clips.fbx.meta`, `MonsterHumanoid.controller`;
`BossDefinition`, `BossEncounter`, `BossBar`, `MonsterAttack(s)`, `MonsterAI` (Fighting, Reactions, Answers), `PoiseMeter`,
`CreatureWounds`, `MeleeTargeting`, `PlayerEating`; `Docs/Combat.md`; the playtest logs.
