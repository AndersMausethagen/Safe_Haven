# The Heartroot, first hand-played try: what the log says, and what it is missing (2026-11-23)

The user (2026-11-23): "I tried the first boss yesterday. I barely got him down 30%. I think he might be missing a 'vulnerability'
stage or some tool or mechanic the player can use to add some serious damage. Analyse my playlog, look through the mechanic and
suggest improvements. Research how other similar games create interesting and fun boss mechanics."

This is analysis and proposals. Nothing is built. It follows `Docs/HeartrootBossReview_2026-11-17.md` ("the review"), whose
changes C1 to C10 are all in the game now. The numbers come from the user's run log `Playtests/20261121-151353.csv`, session 7,
2026-11-22 22:00 local (rows 7334 to 7655), and from the live assets.

## Status: built 2026-11-23 (the user: "do p0 and p5 and make killing the saplings be a key to a topple", plus a fire recipe)

- **P0, boss rows:** `BossTryLog` and `BossTryTally`. A `Boss` row at its waking, each stage and each topple (with its cause), and
  one `BossTry` row as a try ends (won, died, left). The row has the health left, the seconds, the stage, topples by cause, heart
  strikes, interrupts, calls, summons killed, the player's damage by kind of blow, draughts drunk and the last blow on the player.
- **P5, no root lock:** Status Effect › Refreshes, Ends On Dodge, Rest Seconds and Takes Hold Line (`StatusTiming`), on a status of
  their own (EruptionRoots) for the Heartroot's and Root Guardian's eruptions and grasps.
  - These roots no longer restart on a volley's later rings, a roll tears free, they rest 1.5 s after they end, and the player is told
    "Rooted! Roll to tear free."
  - The shared Roots (the widow's web, the bog troll's drag, the root enchantments) still hold through a roll.
- **P1, Rootlings as the key:** Monster Attack › Bond Poise Share 0.3, Bond Topple Seconds 4, Bond Within 12 on the Heartroot's
  Call Roots (`BoundSummons`, `SummonBondLedger`).
  - Sap lines tie each Rootling to its chest.
  - Each Rootling killed takes 30% of its poise.
  - A whole call killed within 12 s topples it for 4 s.
  - A Rootling killed by its own slam counts.
  - Not built: the lines on a friend's copy in co-op (summons are made in the host's game).
- **The fire weakness, as a recipe the portal teaches (the user's design):**
  - **The errand:** "What the Old Wood Fears", bronze, from the portal (a whisper after What Breathes'). It starts with The
    Breathing Hollow done, and closes when The Road North starts.
  - **What it asks:** it teaches Ember Pitch as it starts (Quest › Teaches At Start, new). Then it asks for 2 Cursed Sap and one
    Ember Pitch, and rewards a second pitch.
  - **Cursed Sap:** half the felled Tree, Oak and Hardwood trees in the Misty Woods give one (Drop Entry › Only In Raid, new).
  - **Ember Pitch:** 2 Cursed Sap and a Hearth Ember at a campfire. A coating (Item › Coating): its own slot, one at a time, used as
    a tonic for 5 minutes. Every landed swing on a creature adds 25% of itself as Fire (`CoatedFire`), and 5% set it alight
    (Burning, as fire arrows do). Flames on the blade smoulder at rest and roar through a swing (`CoatedBladeView`, Coated Flames).
  - **Counted:** in `FightMath` and the stacked kit, under a coating budget of its own (×1.35, PowerCurveSettings). See
    Docs/BuffBalance_2026-11-23.md.
  - **Borrowed icons:** Ember Pitch uses the Hearth Ember's, and Cursed Sap uses Rootling Bark's.
  - **Not voiced yet:** the portal's new line.
- **Not played by hand.** P2 (bigger topples), P3 (falls at the stage marks) and P4 (braziers) are not built.

## The short answer

- **You were right: the fight has no vulnerability stage you can see or reach.** Its only big opening is the topple. A topple comes
  from poise, and poise comes mostly from parries. You parried twice in four minutes. Even a topple is worth about 100 health, which
  is 4% of the bar. A window that small doesn't read as a vulnerability stage, even when it happens.
- **The clock needs almost twice the damage you dealt.** You did about 3.1 damage a second to the Heartroot. To beat the rage at 7:30
  you need 5.8, every second of the fight. At your pace a kill takes about 14 minutes. Even a club that never stops swinging does
  about 11 a second on its bark, so the fight only works if openings bring about a third of the damage. In your try they brought
  close to none.
- **The Rootlings ate your damage.** Twelve died: you killed 11, and the Heartroot's own blow finished the twelfth. They took 35 of
  your hits and 393 of your damage: half as much as reached the boss. They gave you nothing back.
- **You died to a root lock.** Three Root Eruption rings hit you in about a second (28 + 28 + 33 = 89, half your health). The first
  ring roots you, so you stand in the next two.
- **The fix:** give it openings that come by three routes, not one, and make each worth 8 to 10% of its health. Rootlings become the
  key to a topple instead of a tax (as Rennala's students are). Each stage mark becomes a long fall (as the False Knight's is).
  The hollow gets a fire tool (as Valheim's Elder has). And the log must record the fight, because today it can't tell us whether
  it ever toppled.

---

## 1. What the log shows

### 1.1 The try

| | |
|---|---|
| Length | 4 min 10 s from the gate to your death (238.11 to 242.28 played minutes) |
| Your kit | 179 health, armour 32, damage ×1.16, toughness 237, Well Rested; **Knotted Club** (damage 12) |
| Gear | "gear 18, recommended 10": the game said you were well over the mark |
| The Heartroot | 2,600 health, armour 35, poise 450, fire −40%, rage at 450 s |
| Stage reached | Stage 1 only. Every blow that hurt you is a stage 1 move: no Uproot, Hammerfall or Ground Slam ever came (they start at 66%). |
| Its health left | About 70% (your estimate; the log keeps no boss rows, see §1.4) |

### 1.2 What you did

| Swings | Defence | Techniques |
|---|---|---|
| 82 light, 30 rush, 6 heavy | 36 dodges (4 perfect), 5 blocks | 2 parries, 4 counters, 5 backstabs, 7 interrupts |
| **0 charged heavies, 0 weapon arts** | 1 Greater Healing Draught | 103 blows landed, 8 whiffed |

- **About 68 blows reached the Heartroot** (103 landed, less 35 on Rootlings). At about 11.5 each that is about 780 damage: your 30%.
- **Your Focus was full for the weapon art many times over.** Focus comes at 6 a hit and 35 a parry, and the Chop Art costs 30. The
  art does about 2.9× a first chop, and 45 poise.
- **The charged heavy was never used.** On the club it does about 5× a first chop's damage and 105 poise, which is ten light chops'
  worth.

### 1.3 What hurt you

The log keeps blows worth 5% of your health or more: 22 of them, 569 damage.

| Move | Blows | Damage |
|---|---|---|
| Root Eruption | 7 | 192 |
| Warden's Slam | 5 | 140 |
| Chop (the flail's second blow) | 4 | 104 |
| Bough Flail sweep | 3 | 78 |
| Bark Burst | 3 | 55 |

- **Root Eruption hurt most, in clusters:** two rings in 0.6 s at 239.30, and the three that killed you at 242.26 to 242.28. Roots
  holds you for 1.6 s and the rings come 0.55 s apart, so the first ring holds you in place for the other two. The roll probably
  still moves you out (`PlayerDodge` moves the body itself and ignores the speed Roots sets), but nothing shows that it does.
- **The encounter design's budget for a winning try is 5 to 8 Healing Draughts** and about 600 damage taken. You took more than
  569 and drank one.

### 1.4 What the log can't tell

There is no row for the boss at all: not the damage you dealt it, its health when you died, topples, heart strikes, broken calls,
or the stage reached. The review's step 7 (`BossTry` and `BossPhase` rows) was never built. **So we don't know if it toppled once,
twice, or never.** From the poise numbers, once or twice is possible, but your sense of "no vulnerability stage" says that if it
happened, it didn't register.

---

## 2. Why it plays like this

### 2.1 The clock

| | Damage a second on its bark |
|---|---|
| Needed, on average, over the whole 7:30 | **5.8** |
| A club chaining A, B, C without a pause (34.6 damage every 3.1 s) | about 11 |
| The same with realistic uptime around its strings, about 50% | 5 to 6 |
| Your try | **3.1** |

**Bark alone barely meets the clock even for a player who never touches an add, never misses and never heals.** The review planned
for this: about 35% of its health was to come from topples, interrupts and stage reels. That only happens for a player who parries
well.

### 2.2 The only opening is the hardest one

- **Poise 450.** A light chain (A, B, C) takes 44, so about ten chains in a row, about 31 s of chaining, with no break of 5 s.
- **After 5 s without a blow it recovers 15 a second.** Every trip to a Rootling (6 to 18 s in your try) gave most of it back.
- **A parry takes 180 (40%),** so two parries and a few chains topple it. That is the intended route, and it is the hardest skill in
  the game.
- **A topple lasts 3 s.** With a club that is a backstab (about 30), the heart strike (52) and part of a chain: **about 100 health,
  4% of the bar.**

**For comparison:** in the games below, the window is where most of the damage happens, not a bonus on top.

### 2.3 Adds that take and never give

Call Roots comes every 22 s from 85% health. If you break it (blue, 1.4 s), it reels for 2.5 s. If it finishes, two Rootlings come
and it "lies open" for 3 s, while you are turning to the Rootlings. Neither outcome changes much, so the Rootlings are a tax: 36
health each, about 3 blows each, and nothing gained against the boss.

---

## 3. How other games do it

The review of 11-17 covered Sekiro, Elden Ring's stance, Diablo IV's stagger bar and Ashava's blades, Monster Hunter's topples,
the Curse-Rotted Greatwood, Lost Ark's counters, V Rising and Hollow Knight's rage. Below is what adds to it on this question: **how
does a player cause a big opening, besides by parrying?**

1. **Adds as the key: Elden Ring, Rennala (phase 1).** A barrier blocks all damage. Hitting three glowing students breaks it, she
   crashes to the floor and lies open for "a fairly generous" time, then the barrier comes back and the cycle repeats
   [1, 2]. *The adds are how you hurt her, not a delay.* The Greatwood is the same idea on a tree: popping its sacs stuns it (the
   review's [6]).
2. **Damage tears the armour off: Hollow Knight, the False Knight.** The first real boss of the game. Enough blows on its armour and
   it collapses, its head opens, and blows there are what kill it. Then it gets up and the cycle repeats (from memory, not
   re-checked this time). *A guaranteed vulnerability stage that comes from plain damage, built for a first boss.*
3. **The arena as a weapon: Monster Hunter's Diablos.** Bait its charge into a stone pillar and its horns stick in the rock. It is
   "unable to attack for several seconds", and hits on its head can break a horn [3, 4]. *Position, not timing, makes the window.*
4. **Tools you bring or find: Monster Hunter's traps and the world.** A pitfall or shock trap holds a large monster "for a short time,
   setting it up for capture or an opportunity to go on the offensive" [5]. Falling boulders in the map deal very large damage to
   monsters standing under them [6]. *Preparation and the arena add damage, not only survival.*
5. **The nearest neighbour: Valheim's Elder.** A tree boss with 2,500 health (ours has 2,600). It is weak to fire (one guide says
   double damage), so fire arrows are the usual answer; fire pits built around the altar keep it burning; pillars block its vines
   and break when hit too much [7, 8, 9]. *The answer to a tree is fire and cover: both are things the player brings or uses.*
6. **Fast blows fill a meter too: God of War.** Quick bare-handed blows fill an enemy's stun meter fastest; a full one opens a
   finishing move. In Ragnarök every enemy has a stun bar [10, 11]. *Light weapons get their own route to an opening.*
7. **Windows as the fight's rhythm.** A combat designer calls the vulnerability window a "hold": a short time the boss gives "so that
   the player can safely get their licks in", like Devil May Cry 4's frog boss, whose tongue (its weak point) only comes out in a
   timed window [12]. Titan Souls: brief windows, then survival until the next [13].
8. **The warning: "the Zelda pattern".** Use the dungeon's item on the boss, it is stunned, hit it. Critics call it rigid: one
   process, no choice [14]. *Use a scripted window to teach once, but never make it the only way in.*

**What this says for the Heartroot:**
- **Good bosses give three routes to a window:** skill (parry, stance: Sekiro, Elden Ring), targets (students, sacs, armour: Rennala,
  the Greatwood, the False Knight) and the world or tools (pillars, traps, fire: Diablos, Monster Hunter, Valheim). The Heartroot
  has only skill, the hardest one.
- **A window is worth a big share,** often the bulk of the fight's damage. Ours is 4%.
- **The window is something the player can see coming and aim for:** a glowing student, a pillar, a trap. Our poise strip is the
  only cue.

---

## 4. Proposals, ranked by impact for the effort

All of them keep the 11-17 rules: parry, then backstab stays the best blow; the colour language stays (blue breaks, purple
unblockable, red-edge heavy, pale green open); no flee; the rage at 7:30; 2,600 health until the new rows say otherwise.

### P0. Log the fight (S, code): before any tuning

- **One `BossTry` row when it ends:** health left, seconds, stage reached, topples by cause, heart strikes, calls broken and
  finished, Rootlings killed, damage to the boss by technique (light, rush, heavy, charged, art, backstab, heart strike), draughts
  drunk, the killing move.
- **A `Boss` row at each topple and each stage.**
- This is the review's step 7, which was never built. Without it every proposal below is tuned blind.

### P1. Sap-bound Rootlings: kill them to topple it (M, code and data)

This is the Rennala and Greatwood idea in our own systems. It turns the 393 damage you spent on Rootlings into the fight's main
loop, and it uses what you already did by instinct.

- **Call Roots draws Rootlings out of its own roots.** A glowing sap line ties each Rootling to the trunk, readable from above.
- **Each tied Rootling that dies sends a shock up the line:** 30% of its poise (a parry is 40%) and a flinch.
- **If the whole call dies within 12 s, the roots tear out of it: a 4 s topple, its heart open.** A Rootling crushed by its own
  slam counts too (that happened once in your try). Baiting the slam onto them becomes a trick worth learning.
- **Call Roots becomes a choice:** break it for a 2.5 s reel and no adds, or let it finish and earn the bigger opening.
- **Its own 3 s "lies open" after a finished call** was the review's "backwards" rule. With P1 it has a reason to go, but that is
  your call (it removes something), see Q1.
- **In your try:** 6 finished calls means up to 6 openings.
- **Code:**
  - a summon remembers its summoner;
  - on its death, the summoner takes the poise share or topples (`MonsterAI.Topple` exists);
  - the tie as an Unlit line.
- **Co-op:** the topple already comes back from the host. The line on a friend's copy needs the summoner, which may need a new wire
  format (`CoopWireTests`).

### P2. Bigger openings, fewer of them (S, data and a little code)

- **Topple Seconds 3 → 5.**
- **Topple Strike 2% → 5%** (130 health). Doubled when the first blow in the topple is a charged heavy or a weapon art. Your two
  unused big blows then have a reason to exist.
- **Poise Growth 0.3 stays,** so topples can't come back to back.
- **One topple is then worth about 8 to 10% of its health,** against 4% today.
- **A small picture of the heavy button at the open heart** while it lies down: a picture, not a line of text.

### P3. Each stage mark is a fall: the vulnerability stage (S, data and a little code)

This is the False Knight's idea. At 66% and 33%:
- the 2.5 s reel becomes a **6 s fall with the heart open**;
- it plays Knockdown, Down and Get Up through the topple path;
- the heart glows and the stage line shows;
- then Uproot or Heartburst comes, as now.

It is guaranteed, it reads as "the vulnerability stage", and it rewards reaching the mark. It doesn't grow its poise.

### P4. Fire from the hollow (M, an arena prop): the tool you asked for

This is Valheim's Elder answer, in the arena. It uses its fire weakness, which only matters today if you packed Fire Arrows or the
Hand Torch.

- **Four pitch braziers on the hollow's rim.** A blow tips one over and it spills burning pitch in a 3 m patch for 10 s.
- **The Heartroot standing in it burns:** fire damage over time at −40%, and its poise wears twice as fast while it burns.
- **The braziers relight after 45 s,** and they light the dark of stage 3 as well.
- **It is a lure, as Diablos's pillar is:** stand by a brazier, tip it as it comes.
- **Reuse:** hazards × fire are built (the world reactions work of 11-14).
- **Variant, or a later addition: jam stones (Diablos).** A Warden's Slam or Hammerfall that strikes one of three standing stones
  jams the bough for 4 s, heart open, one bark chip. Each stone cracks after two jams. Build one of the two in the first pass, not
  both (the campaign budget).

### P5. No root lock (S, data or a little code)

- **Only the first ring of a volley roots.** Being rooted gives 2 s without Roots.
- **Show that a roll tears you free.** If the roll already does, the Roots icon shakes. If it doesn't, make it, then show it.
- **Check in Play first** whether the roll moves you out today.

### P6. Light blows get their route (S, data)

- **Its poise doesn't recover while it is attacking or while its Rootlings live.** Today the adds hand its poise back.
- **Poise Recovery Delay 5 → 7 s.**
- With P1 this matters less. Without P1 it is the cheapest help for a light-weapon player.

---

## 5. What it should feel like after P0 to P3 and P5 (estimates)

- **A topple with P2:** a rush backstab about 30, the heart strike 130, a chain at ×1.2 about 40: **about 200 (8%)**.
- **Your 11-22 try, played the same way:**
  - 6 call topples, about 1,200;
  - bark, 780;
  - the 66% fall, about 200.
  - **That is about 2,200: into stage 3 at 4:10**, and alive, with P5.
- **That is likely too generous,** so expect the first pass to take Call Roots' cooldown 22 → 30 s or the call topple 4 → 3 s.
- **The tuning target, kept from the review:** an average first win in 5:30 to 6:30, about half its damage from openings. Your
  unchanged play should reach stage 3 before the rage. A player who parries well wins with 60 to 90 s to spare.
- **Tune in this order:** Call Roots' cooldown, then the call topple's length, then the heart strike, and health last.

## 6. Build order, with the check for each

1. **P0.** *Check:* a probe fight writes one `BossTry` row with every column, and `Boss` rows at topples and stage marks.
2. **P5.** *Check in Play:* caught by the first ring, a roll clears the next two, and the second ring doesn't root.
3. **P2, P3.** *Check:* a topple lasts 5 s; a charged heavy as the first blow doubles the heart strike; set its health to 0.65 in an
   eval and it falls for 6 s, then Uproot comes.
4. **P1.** *Check:*
   - the lines show from the camera's height;
   - killing both Rootlings within 12 s topples it for 4 s;
   - one killed by its own slam counts;
   - a broken call makes no Rootlings and reels it 2.5 s.
   - EditMode test: the poise share and the topple, with a timer that can fail.
5. **P4** (or the jam stones). *Check:* a tipped brazier burns it, its poise wears double, and the brazier relights after 45 s.
6. **Then:** targeted tests (`BossTests`, `BossToppleTests`), a review subagent, and your playtest. Retune from the new rows.

## 7. Questions for you

1. **Rootlings as the key (P1)?** And may the 3 s "lies open" after a finished call go, now that killing the Rootlings is the
   opening? *Recommended: yes to both.*
2. **Bigger openings (P2) and falls at the stage marks (P3)?** *Recommended: yes.* These are the vulnerability stage you felt was
   missing.
3. **The arena tool:**
   - fire braziers (P4);
   - jam stones;
   - both later.

   *Recommended: braziers first.* The tree's own weakness, and they light stage 3.
4. **P0 and P5 are fixes, not design.** *Recommended: build them first in any case.*

---

## Sources

Read for this document (search summaries, with the pages listed):
1. [Elden Ring wiki: Rennala, Queen of the Full Moon](https://eldenring.wiki.gg/wiki/Queen_Rennala). Community wiki.
2. [GameSpot: How to beat Rennala](https://www.gamespot.com/articles/elden-ring-how-to-beat-rennala-queen-of-the-full-moon/1100-6501316/).
3. [Kotaku: the Monster Hunter beast that almost broke me (Diablos)](https://kotaku.com/the-monster-hunter-beast-that-almost-broke-me-1822737249).
4. [SuperCheats: The Horned Dragon (Monster Hunter Tri)](https://supercheats.com/guides/monster-hunter-tri/the-horned-dragon).
5. [Fextralife MHW wiki: Pitfall Trap](https://monsterhunterworld.wiki.fextralife.com/Pitfall+trap). Community wiki.
6. [Game8: Monster Hunter Wilds environmental traps](https://game8.co/games/Monster-Hunter-Wilds/archives/483298) (the boulder
   damage; the page is for Wilds).
7. [GameSpot: Valheim Elder boss guide](https://www.gamespot.com/articles/valheim-guide-how-to-find-and-defeat-the-elder-and-what-you-get-from-it/1100-6487903/).
8. [Valheim wiki: Boss strategies](https://valheim.fandom.com/wiki/Boss_strategies). Community wiki; pages from 2021 to 2023.
9. [AllThings.how: How to beat the Elder](https://allthings.how/valheim-how-to-beat-the-elder/).
10. [Digital Trends: God of War combat guide](https://www.digitaltrends.com/gaming/god-of-war-combat-guide/).
11. [Sportskeeda: staggering enemies in God of War Ragnarök](https://sportskeeda.com/esports/god-war-ragnarok-staggering-enemies).
12. [Game Developer: Boss design, tips from a combat designer](https://gamedeveloper.com/design/opinion-boss-design---tips-from-a-combat-designer).
13. [Game Developer: one-hit kills in Titan Souls](https://gamedeveloper.com/design/game-design-deep-dive-one-hit-kills-in-i-titan-souls-i-).
14. [Zelda Dungeon: "The Zelda Pattern" in boss design](https://www.zeldadungeon.net/?p=178302).

From memory, not re-checked: Hollow Knight's False Knight (its armour breaks, its head opens).

In the project:
- the user's run log (session 7, 2026-11-22);
- `TheHeartroot.asset`, `TheHeartrootStats.asset`, `Boss The Heartroot.asset` and its 18 attack entries;
- `KnottedClub.asset` and its Chop attacks, `CombatTechniqueSettings.asset`, `Roots.asset`;
- `MonsterAI.Answers` (poise, topples), `MonsterAI.Reactions` (the parry share), `PoiseMeter`, `PlayerCombat.Swings`,
  `PlayerDodge`, `StatusEffects`, `BossEncounter`;
- `Docs/HeartrootBossReview_2026-11-17.md` and `Docs/HeartrootEncounterDesign_2026-11-17.md`.
