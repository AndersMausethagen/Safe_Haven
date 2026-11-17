# The Heartroot's encounter: Guardian's Hollow (design, 2026-11-17)

The user (2026-11-17): "Hearthroot will also get its own arena, and we are gonna design an epic fight! Having the player use
everything he has learned: parry, dodge, using flasks etc." The rules for encounters come from `Docs/BossEncounterResearch_2026-11-17.md`
› Decisions: no way out but winning or dying, a shown enrage clock that checks damage, about ten minutes, and failing softer than a
raid but still punishing.

This is a design only, and nothing is built. The numbers are from today's assets. The time budget (§3.1) is an estimate, which the
playtest rows tune (§5, step 9).

---

## 1. The fight in one paragraph

You walk a sunken path through thick mist, hearing breathing before you see anything. You eat and drink at a rest stone with a lit
campfire, then carry a Hearth Ember through the only gap in a ring of dead trees. The mist closes behind you, the ember flies into
the split stump, and the Heartroot stands up. In the open bowl of stage 1 (100 to 66%) it teaches its everyday blows: a slam to
parry, a flail of boughs that drains a player who blocks and pays one who parries, a root grab that only a dodge escapes, and
eruptions thrown at your feet. At 66% four walls of root rise like spokes and split the hollow into lanes. It throws waves of thorn
that the walls stop, reaches under the walls with its grab, calls Rootlings, and draws sap to heal unless something strikes it. At
33% the walls sink and the canopy closes, leaving the split heart's glow and your own light. Black sap drips on the rim, and the
heart spits a rot that only an Antidote cures or a draught outlasts. The HUD counts down to its rage at 8:30. With the tier I entry
kit, a meal and a tonic, a clean try wins in about seven minutes. Without them, or after letting it heal, you finish against a raging
tree. It is the first encounter a player meets: tier I, the Misty Woods, 90 to 120 minutes in (QuestRedesign). So every answer has a
fair tell, and each is something the game has already taught.

**Each lesson, and the move where it decides the fight**

| Learned | Deciding move | Tell | Why it is the answer |
|---|---|---|---|
| Parry | Bough Flail | The usual fill, which turns pale with a glint at the parry moment | A parry is free and gives 20 stamina back. Each takes 40% of its 320 poise, so the third in a row staggers it (1.3 s) |
| Dodge | Grasping Roots | A purple (unblockable) fan, 9 m long, over 1.1 s | It can't be blocked or parried. A hit pulls you in, roots you and the slam follows |
| Openings, backstab | Ground Slam (Open After 1.5 s), Call Roots (4 s), stage reels, parry staggers | The pale green ring; "its heart lies open!" | A boss is open only while staggered: a backstab then does ×1.75 with a sure crit, on top of ×1.2 |
| Interrupt | Regrowth | A blue fill over 2.4 s, with its line | Any blow breaks it, an arrow or a thrown club too. If not, it mends 12%: about 50 s of your work |
| Stamina | Bough Flail into Hammerfall; Hammerfall against a raised guard | The hammer's red edge | Blocking all five blows costs about 115 stamina, so the guard breaks on the hammer even from a full bar. Rolling through costs 85+ |
| Positioning | Thornwave; the light and the rot | Cast, then waves; drips warn 2 s ahead | Walls stop waves but not grabs or eruptions. In stage 3 the clean ground is the lit middle |
| Flasks, tonics | The length | Your bars | A tonic lasts 180 s, the fight about 420 s. Healing comes over 6 s, and the 1.3 s drink needs a window |
| Cures | Heart Spray: Heartrot | Lob rings over 1.2 s; the heart spits | 83 damage over 16 s unless cured. The Antidote cures it |
| The clock | Enrage at 8:30 | HUD timer (built) | +50% damage, then +15% every 30 s |

---

## 2. The arena

### 2.1 Scene and layout

`Scenes/Raids/Encounter_GuardiansHollow.unity` is made from `Raid_MistyWoods`, the way `Encounter_BrukamsBridge` was made from
Troll Valley's scene. That keeps three things:

- the rig that `RaidSceneRigTests` checks;
- the Woods' terrain layers, grade and mist;
- the day-night profile.

It has `RaidMap` › Rotate off, a painted terrain of 128 m (`Data/Raids/Maps/Guardian's Hollow Terrain.asset`) and its own navmesh.
The hour is fixed in late afternoon.

```
             tall dead trees on the rim (N, E, W)
         .-----------------------------------.
        /  outer ring r 16-20 (stage 3 rot)   \
       |   mid ring  r 8-16  (always clean)    |
       |   NW wall     [STUMP]      NE wall    |   stage 2: four spoke walls, r 6-19,
       |   SW wall    heart ring    SE wall    |   each with a 3 m gap at r 11-14
        \            r 3-8                    /
         '--- broken stumps --- OFFERING GAP -'    the gate: mist wall, Beyond 4 m inside
                     rest stone + campfire          15 m south of the gap
                     sunken path, ~90 m, one bend
                     arrival stone                  the south edge
```

- **Arrival stone.** An `ExtractionPoint` with Turns Back. Leaving here before the gate costs nothing.
- **Approach.** A sunken path about 90 m long, between banks 2 to 3 m high.
  - It bends once, so the hollow can't be seen from the stone.
  - Dead trees stand in thick `TerrainMist`.
  - Three old offerings from the pact are Inspect Points with Say Aside lines: a bowl of ash, a carved hearth-stone, a child's shoe.
  - The stump's breathing is heard from about 60 m.
  - No creatures. The first walk takes 30 to 45 s.
- **Rest stone.** It stands 15 m before the gate, and later tries start here (`EncounterArena` › Rest Stone).
  - Beside it is a lit campfire, so a forgotten Hearth Ember can be made from 3 Sticks.
  - This is where the meal and the first tonic go down.
- **Gate: the Offering Gap.** The only gap in the dead ring, hung with old offerings.
  - `EncounterGate` asks **Hearth Ember ×1** (Gate Items). This is the existing rule, "only a hearth's fire wakes it", moved from
    the rumour's choice to the gate.
  - Seal: a mist wall. Beyond: 4 m inside.
- **The hollow.** A bowl 40 m across with a flat floor, which `MonsterProjectile` needs: it stops on any collider, ground too.
  - Bare dark earth, as "nothing grows inside" says.
  - The stump is 6 m wide.
  - Tall trees stand on the north, east and west. On the south rim, the camera's side, only broken stumps, none over 3 m.
- **Way home.** On the stump, held shut until the fall (`ExtractionPoint.HoldShut`, built).
- **Camera.** A `CameraZone` pulls out to about 32 m: room for the 4.9 m boss and a thorn wave's reach.

### 2.2 What changes, and when

| When | What changes | How |
|---|---|---|
| Gate | The mist seals. The ember arcs into the split, the stump opens and the Heartroot rises: its name card, untouchable for 3.2 s, its music | `EncounterArena.Enter` (built); `InGameCinematic` (4 s); `BossDefinition` › Wake |
| 66% | Four walls of root rise on the diagonals, 1.8 m high (1.4 m on the southern two). Cracks and dust warn 2.5 s ahead. Anyone on a line is nudged aside, never hurt or trapped. Each wall carves the navmesh, so creatures use the gaps | **New `RisingGround` (S)**, the mirror of `CollapsingGround` |
| 33% | The walls sink. Branches close the canopy at the rim, never over the floor, and the light falls to deep dusk, so `PlayerGlow` comes on. The heart's light reaches about 10 m. Sap starts to drip on the outer ring | Story graph, When Boss › phase 2: Show Object, Change Look, Change Time of Day (check that it reaches `AtmosphereConditions.Night`; if not, an override, S). **New `PhaseLight` (S)**, **new `ArenaHazardDrops` (S)** |
| 12%, optional | "It roots itself and calls the whole hollow up": three rings of eruptions from the rim inward, 1.2 s apart, each warned 1.5 s ahead. The only safe ground is within 6 m of it. Then it reels for 5 s | A 4th `BossPhase`, a Take Root action and waves in `ArenaHazardDrops` (**M**, §6 question 5) |
| 8:30 | Its eyes burn and its line is posted. Optional: drips spread onto the mid ring | Built. Optional: a When Boss "Enrages" moment (S) |
| Fall | Rootlings wither and the canopy opens. Low sun falls into the hollow (a 3 s shot), and the way home opens | Story graph, When Boss › falls: Remove Enemies, Hide Object, Change Time of Day, Play Cinematic |

Every piece follows the phase by the boss's `BossDefinition` (`BossEncounter.AnyPhaseEntered`), as `CollapsingGround` does, so it
also runs on a friend's game. Nothing is random: the walls, drips and waves keep a fixed order.

### 2.3 Safe ground, hazards and flasks

**Safe ground**
- **Stage 1:** everywhere outside a telegraph.
- **Stage 2:** behind a wall from Thornwave, and nowhere from grabs or eruptions. Roots go under walls and lobs fall from above:
  neither `HitDetector` nor `LobStrike` checks for walls.
- **Stage 3:** the mid ring never gets placed rot. The outer ring gets one 2.2 m pool every 8 s, in a fixed order: twelve pools,
  about two fifths of it. Heart Spray's pools land anywhere and last 8 s.

**Flasks**
- **The meal must outlast the fight.** Roast Boar (300 s) doesn't. Smoked Boar (600 s) and Hunter's Stew (480 s) do.
- **A tonic lasts 180 s,** so you drink again twice. Each drink takes 1.3 s, slowed, and a hit cancels it: a real trade against a
  swing.
- **Healing arrives over 6 s.** Drink in a window:
  - the 4 s after Call Roots;
  - the Ground Slam's 1.5 s;
  - behind a wall after a wave.
  - Never during the flail.
- **Heartrot:** the Antidote cures it, and a Healing Draught outlasts most of it.
- **What you lose.** Everything brought is lost on a fall, and the gate's menu lists it (`RaidRisk`, built).
- **A winning try with the entry kit uses about:** 5 to 8 Healing Draughts, 1 or 2 Antidotes, a meal and 2 or 3 tonic doses. That
  covers about 600 damage taken, less Regain.

**Readable from above**
- **The existing colours:** purple unblockable, blue interruptible, red edge heavy, pale green opening.
- **Rot is brown-black with a yellow sheen,** never green or purple, and faintly emissive.
- **Telegraphs read in the dark:** `CombatEffect.mat` is URP Particles/Unlit.
- **What the dark hides:** the body and the rim, never the rules. Rootlings show by their eyes.
- **Darkness:** at the Woods' night after the 112e39ba moon change.

---

## 3. Phases and moves

### 3.1 The budget

| Fact | Value |
|---|---|
| Tier I kit (`GearBalance.EntryKitOf(1)`) | Knotted Club (12, blunt) and Woven Hide set: 150 health, 32 armour (×0.76 physical), a roll. Woods tier I is ×1 health, ×1 damage |
| Its damage to the Heartroot | 15.5 DPS while swinging, which is 11.5 after its 35 armour; 12.4 with Roast Boar; about 15 with a Cursed Draught too |
| The Heartroot today | 1,430 health, 320 poise, −25% fire. Stages at 66% (+10%, 1.8 s reel) and 33% (+20% more, 2.2 s reel) |
| Today's length | 124 s of pure swings, or 3.5 to 5 minutes in a real fight: too short |

**Proposed: 2,900 health at tier I (×2.03), enrage at 510 s.**

| Stage | Health | Average DPS (swing × uptime × openings) | Time |
|---|---|---|---|
| 1 | 986 | 13.5 × 50% × 1.15 = 7.8 | ~2:05 |
| 2 | 957, +348 per finished Regrowth | 13.5 × 40% × 1.15 = 6.2 | ~2:35 |
| 3 | 957, +348 per finished Regrowth | 13.5 × 42% × 1.2 = 6.8 | ~2:20 |
| All | 2,900 | | **~7:00** (8:15 without meal or tonic) |

- **The run.** With the approach, the gate and the way home, a run takes 8.5 to 10 minutes.
- **What the clock does to each kind of try:**
  - unprepared: it ends at the wire;
  - unprepared with one Regrowth missed: about 45 s of rage;
  - two missed: a raging stage 3, which is meant to kill.
- **Rage at stage 3:** its slam takes 44% of the kit's health, and the fast slam 50%.
- **Tuning rule:** a clean, prepared try ends 60 to 90 s before the rage. The knob is the boss's health, not the clock.
- **The 2026-11-16 rule** ("tune Player Damage, not enemy health") is about the player's power across the game. This is one boss
  given the length its fight is designed for. It is still question 1.

**How to read the tables**
- **Damage** is raw, then what reaches the tier I kit after 32 armour and the stage's bonus, as a share of its 150 health.
- **The wind-up** is the attack's Hit Delay: how long its telegraph takes to fill.
- **The ring** is the green opening (`MonsterAttacks.TryGetOpening`).

### 3.2 Stage 1, full health: "the old wood wakes"

| Move | You see | Do | Damage | Then | System |
|---|---|---|---|---|---|
| **Warden's Slam** (Guardian Slam) | Plain fill, 150° by 2.4 m, 0.6 s | Parry; or dodge; or block (25 stamina, 22% through the shield) | 48, 36 (24%) | Ring 0.7 s | Existing, cooldown 1.8 |
| **Bough Flail**, 2 blows | Sweep then chop, 0.65 s and 0.6 s, 0.3 s apart | Parry both: that takes 256 of 320 poise, so a few hits stagger it. Or roll out and give up the opening | 36, 27 (18%) each | | **New** attacks (data): Follow Ups, Combo Chance 1, Combo Gap 0.3; clips Strike Wide and Strike Chop |
| **Grasping Roots** | Purple fan, 9 m by 14°, 1.1 s | Dodge across it or through it (a perfect dodge earns a counter). Rooted? Your guard still works: parry the slam that follows | 30, 23 (15%), pulled 3 m, Roots 1.6 s, then the slam | 2 s recovery after a miss | **New** (data), built as Bogtroll Drag is: Unblockable, Pull 3, Roots; range 4 to 9 m, cooldown 9 |
| **Root Eruption** | Three r 2 m rings at your feet, 1.0 s each, aimed ahead | Keep moving. A root from one sets up the next | 42, 32 (21%), Roots | | Existing lob, 3 to 18 m, cooldown 11 |
| **Bark Burst** | Fill all round it, 3 m, 0.7 s | Don't loiter behind it unless it is staggered | 34, 26 (17%), thrown 2.5 m | Ring 0.6 s | **New** (data): Arc 360, Pull −2.5, When Foe Behind |

### 3.3 Stage 2, from 66%: "roots begin to tear free"

It reels for 1.8 s, a backstab window, and hits 10% harder. The walls rise. Stage 1's moves stay, and Grasping Roots now goes under
the walls.

| Move | You see | Do | Damage | Then | System |
|---|---|---|---|---|---|
| **Bough Flail**, 4 blows, then **Hammerfall** | Four fills, 0.6 s each. Then an overhead with a red edge and a swell, 0.9 s, which it holds 0.45 s longer 40% of the time | Parry: the third parry staggers it, then roll behind. Blocking it all breaks the guard on the hammer (1.1 s stun). Rolling it all costs 85+ stamina. The hold catches a roll made on reflex | 39.6, 30 (20%) each; the hammer 66, 50 (33%) | A 1.3 s stagger | **New** (data). Hammerfall: Hold Chance 0.4, Heavy. It also comes alone, three times as often while you hold your guard (When Foe Guarding) |
| **Ground Slam** | Fill all round it, 4.5 m, 1.15 s, red edge | Roll out, then back in behind it while its arms are in the ground | 71, 54 (36%: a knockdown) | **Open After 1.5 s**: a backstab | Existing; Open After set on `MonsterAttack` (data) |
| **Thornwave** | It casts for 1.2 s, then three rolling thorns (r 0.9 m, 9 m/s), each aimed anew | Put a wall between you and it, or step aside between them. Rootlings stop them too | 41.8, 32 (21%) each | 1.5 s | **New** (data): Projectile, Burst 3 at 0.6 s, Lead 0.15, 6 to 25 m, cooldown 10; **new** projectile prefab (S) |
| **Call Roots** | Blue fill, 1.4 s | Strike it in the wind-up. If it finishes: kill the 2 Rootlings (36 health each) or use the opening | Rootling 9.6, 7 (5%), 30% Roots | **Open After 4 s** | Existing, with a **Heartroot copy** (the Root Guardian shares the asset): Hit Delay 1.4, at most 4 Rootlings |
| **Regrowth** | Blue fill, **2.4 s**, arms up; "Strike it to break the draw!" | Any blow before the fill ends: walk 12 m, throw the club art 12 m, or shoot | It mends 348 (12%) over 6 s | 0.5 s after a break | Existing Howl (heal 0.12, Interruptible). From Phase 2 → **1**, Hit Delay 0.9 → **2.4**, cooldown 30 |

### 3.4 Stage 3, from 33%: "the old heart splits"

It reels for 2.2 s, and its bonus reaches +30% in all. The walls sink, the canopy closes and the drips start. Everything else stays.
The fast slam already exists: 55.2, cooldown 1.1, gaining Hold Chance 0.25. Regrowth's cooldown falls to 22 s through a second entry.

| Move | You see | Do | Damage | Then | System |
|---|---|---|---|---|---|
| **Heart Spray** | The heart spits (Throw). Four r 1.6 m rings scattered round you, 1.2 s each | Walk out. If hit, an **Antidote** cures it at once; a Healing Draught outlasts most of it. Stay out of its pools | 22 Blight, 29 (19%; armour doesn't help), plus **Heartrot**: 4/s × 1.3 for 16 s = **83 (55%)** | 1.0 s | **New** (data): Lob, Burst 4, Scatter 3, Hazard "Heartrot Pool", cooldown 14. **New** status Heartrot; Antidote Cures += Heartrot |
| **Rot drips** (the arena) | A dark spot grows and a strand falls, 2 s, then a pool | Keep the fight in the mid ring and the light | Heartrot | | **New `ArenaHazardDrops` (S)**, using `GroundHazard.Spawn(prefab, at, boss)` so the drips hit as hard as its stage and rage |
| **The Hollow's Hold** (12%, once, optional) | Rings of eruptions from the rim inward, each warned 1.5 s ahead | Get within 6 m of it: its body is the safe place | 50, 49 (33%) per ring, Roots | **Open After 5 s**: the kill window | Take Root: Howl, Self Only, Below Health 0.12, Hit Delay 4.5. **M** |

### 3.5 How it chooses, and what is shared

- **Weights.** Weighted bands (`MonsterAttack` › Weight, built, but no boss uses them yet), so a seven-minute fight never runs in the
  same order:
  - **priority 30,** weightless: Regrowth and Take Root;
  - **priority 20:** Grasping Roots 2, Thornwave 1.5, Root Eruption 1.5, Heart Spray 2, Call Roots 1;
  - **priority 10:** Warden's Slam 3, Bough Flail 2, Ground Slam 1.5, Hammerfall 1, Bark Burst 1.
  - From Phase and `HoldsGround` keep it at a range it can use.
- **Copies.** Guardian Slam, Ground Slam, Call Roots and Root Eruption are shared with the Root Guardian. A changed
  `AttackDefinition` value needs a Heartroot copy. Open After, cooldowns and weights live on `MonsterAttack`, so they are its own.
- **Found while reading:** `Data/Combat/Attacks/HeartrootGrasp.asset` (the reward skill's attack) is named "Frost Rune" and deals
  Frost damage, but the skill says "roots break from the ground". Check it.

---

## 4. Rewards and story

**Kept**
- **The skill:** Heartroot's Grasp, learned at once and kept even if you fall after the kill. Grasping Roots, the move that taught
  you to dodge, becomes yours.
- **At home:** the Heartroot Trophy by the portal, the home lines, and "Hearth-keeper".
- **The drops** (`Data/Loot/Creatures/TheHeartroot.asset`):
  - Heartwood of the First Guardian, once;
  - Guardian Heartroot;
  - 3 to 5 Hardwood;
  - 1 to 2 Thick Hide, half the time;
  - 8 to 12 Cursed Essence;
  - the recipes: the iron Hatchet, Pickaxe and Sickle, and the Hide Rucksack.
  - They are carried a few metres to the way home.

**The way in: an expedition finds it**
- **A new Main Story step, "The Breathing Hollow"** (data):
  - It comes after The Weakening Ward and Bring Bjarne Home.
  - Its objective is a Reach Place on a new Quest Target, Guardian's Hollow: a Quest Zone in the expedition's set piece, with Must
    Extract. That is the quest-deeds rule, not a Quest Event per place.
  - The urgent rumour (after two extractions) still points the way.
- **What Breathes Under the Roots** takes it as a prerequisite. Its start opens the encounter (Opens When › Started).
  - Its objectives stay: the ember, then the kill. The kill is a Kill Monster with Count Already Owned, so it counts anywhere.
  - Its journal notes change.
- **Saves:**
  - a player already on that quest finds the encounter open;
  - a player who beat the Heartroot in the Woods has a rematch.

**What the kill opens**
- What Breathes completes, The Road North starts, and Troll Valley opens. Its Unlocked By is Boss The Heartroot, unchanged.
- Brukam's Bridge opens on the same kill.

**The Woods afterwards**
- **Before the kill:** Guardian's Hollow stays Always There. The stump breathes and can't be woken. The rumour's choice becomes "Lay
  your hand on the split": a Tell with no spawn. Two Rootlings guard it.
- **After the kill:** a quiet sunlit glade, with the stump dead and no guards: "the road north is open and lit once it falls". A
  small `ShowWhenBossBeaten` (S) on the prefab swaps the look. The rumour already stops (Until Beaten).
- **Rematches:**
  - 24 h after a kill (built), at the Woods' tiers II to IV.
  - From Tier gives it more: from II the flail always ends in Hammerfall; from III, five eruptions and a second grab.
  - From tier II a rematch drops a second Guardian Heartroot, which the Forge, Sawbench and Construction Table already ask for.
- **Co-op:**
  - Each friend adds 60% health (built).
  - The pieces follow the phase on every game.
  - A friend without the story gets only the drops (built).
  - Companions come, and rot and eruptions hit them too.

---

## 5. Build list

**1. Decisions (§6).**

**2. The encounter's data (S)**
- `Data/Raids/GuardiansHollow.asset`:
  - land Woods, tier 1, locked;
  - the Woods' difficulties I to IV, with no elites;
  - Known Enemies: the Heartroot and the Rootling, so the planner shows Blight.
- `Data/Raids/Encounters/Guardian's Hollow Encounter.asset`:
  - the boss, First Tier 1, Rematch Hours 24;
  - Opens When: What Breathes › Started;
  - the gate: "Carry the ember in", Hearth Ember ×1;
  - Enrage Seconds 510, at 0.5 / 0.15 / 30;
  - Wear 0.1.
- Add it to the portal's destinations.

**3. The boss's data (S to M)**
- Max Health 2,900.
- Eight new attacks: Bough Flail ×3, Hammerfall, Grasping Roots, Bark Burst, Thornwave, Heart Spray.
- A Heartroot copy of Call Roots. Regrowth is edited in place.
- The attacks list rebuilt with weights. A new array element copies the one before it, so reset every field.
- The Heartrot status, three hazard prefabs (Heartrot Pool, Rot Drip, Hollow Eruption, copied from BlightTrail) and the Thornwave
  projectile.
- The Antidote's cures, and the Journal Advice.
- Check the "Frost Rune" slip.

**4. Code (S each, M together)**
- **`RisingGround`:** rise or sink by phase, with a warning, a nudge and a carving obstacle.
- **`ArenaHazardDrops`:** points in order, with the boss as maker.
- **`PhaseLight`:** reads its own `PhaseEntered`, so it works on a friend's copy.
- **`ShowWhenBossBeaten`.**
- **Playtest rows:**
  - `BossPhase`: the phase, and the seconds since the gate;
  - `BossTry`: the outcome, the seconds, the phase reached, whether it raged, and the heals and cures used.
- **Optional:** the When Boss "Enrages" moment.

**5. The scene (M to L).** By hand in the Editor through the CLI, since the builders are retired.
- Copy, strip, paint and bake.
- Place:
  - the stones and the path's dressing;
  - the gate, its Seal and Beyond;
  - the held boss, and the way home;
  - the walls and the drip points;
  - the canopy, the `CameraZone` and two cinematics;
  - the story graph.
- Visual care: close-ups and the game view of every seam. Match the Haven's graphics.

**6. The quest (S, data).** The target and its zone; "The Breathing Hollow"; the prerequisite; the rumour's Tell; the glade's look.

**7. Tests, with `RunTests.ps1 -All`**
- `EncounterTests`: the wiring.
- `RaidSceneRigTests`: the new scene.
- `BossTests`: update it, since it expects the Woods spawn.
- New tests:
  - the pieces start once, keep their order and follow the definition on a copy;
  - no drip lands on the mid ring;
  - the Antidote cures Heartrot;
  - each deciding move keeps its flag: Unblockable, Interruptible, Open After, Heartrot.

**8. Play checks.** CLI probes first (invulnerable for the walk-throughs), then by hand.
- The gate's price, seal and wake.
- Each piece at its mark, and no wall traps anyone.
- A thorn wave stops at a wall, and the grab goes under it.
- In the dark: telegraphs, pools and eyes are seen, and `PlayerGlow` is on.
- A thrown club breaks Regrowth.
- A two-copy co-op run.
- A timed try with the entry kit.

**9. Tune from the rows.** First the health, against the 60 to 90 s margin. Then the wind-ups, if tier I finds them unfair. Then
Heartrot.

**10. After the first playtest:** the Hollow's Hold (M), and drips spreading at the rage (S).

---

## 6. Open questions

1. **Length and health.** About seven minutes (eight unprepared) inside a run of 8.5 to 10, with health raised from 1,430 to 2,900
   and the rage at 8:30? Or a ten-minute fight, at about 4,100 health and the rage at 11:30? **Recommended: seven.** Ten minutes of
   one tier I boss is long for a first boss, and the approach and the way home already make the run about ten.
2. **Leave the Woods, with a find step?** Move the Heartroot out of the Woods' expeditions in one step, with "The Breathing Hollow"
   found on an expedition before the portal opens the encounter? Or open it as soon as What Breathes starts? **Recommended: the
   find step.** Expeditions find the boss and encounters are where you fight it, as the research advised.
3. **Heartrot.** A new status (16 s at 4 Blight per second, cured by the Antidote), or Poison made stronger? Poison is 21 damage
   over 7 s, too weak to decide anything. **Recommended: the new status.** Poison stays as it is for wolves and widows, and the
   Antidote cures both.
4. **How dark is stage 3?** Deep dusk, as the Woods at night, with your light and the heart's? Or nearly black? **Recommended: deep
   dusk.** You asked for "not too dark", and the rules must read from above.
5. **The last stand at 12%:** build it in the first pass (M), or after the first playtest? **Recommended: after.** The three stages
   already test everything on the list. The finale is polish once the core feels right.
