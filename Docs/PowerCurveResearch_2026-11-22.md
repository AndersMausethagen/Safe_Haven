# The player's power curve: every source at once

Research date: 2026-11-22. Research only. No code, asset, scene or prefab was changed, and Unity was not run. The numbers come from the
`.asset` files (read with a script that resolves every GUID), from the code's own formulas, and from the playtest CSVs in
`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests`.

**The question.** Power sources were built and tuned on different days. Do they stack into runaway combinations, or leave tiers where
nothing new matters?

**How sure each number is.**

| Kind | Where it comes from | How sure |
|---|---|---|
| Item, food, perk, enchantment, raid and creature numbers | Read from the data | Exact |
| Kits, stacks and hits | A Python copy of `GearBalance` and `FightMath` run on the data | Checked: it reproduces the tier table in `GearScoreAndRaidTiers.md` (scores 18/38/61/81, damage 12.8/18.8/29.3/37.6) and the K3 table in `AgentDecisions_2026-11-18.md` (every slot at tier IV: ×1.84 / ×1.45) to the digit |
| Seconds to kill, damage per second | The same, with swing timings from the attack assets | Arithmetic. Leaves out dodging, positioning and misses |
| What players do | 3 human runs, 12 raids, all at tier I | Small sample, one tier only |
| Tiers II to V in play | Nobody has played them | **Calculated only** |

---

## 0. The short answers

**The tier multipliers are sound at tier entry.** Each tier's entry kit (its gear, plus the perks and enchantments kept from the tier
before) stays within 1% of tier I's fight, in hits to kill and hits to die. The ladder is "hard at entry, mastered by the end": mastering a
tier makes it 30-40% easier, and the next tier is 1.8-3.0 times harder for a mastered player.

**But the guard rails have four holes, and the top of the curve has none.**

| # | Finding | Size | Why it matters |
|---|---|---|---|
| 1 | **The balance model counts Might at a third of its real worth.** `GearBalance` uses `CombatRules.Default` (+1% damage a point). The player prefab uses `Data/Combat/CombatRules.asset` (+3% a point). The Kit rows in the logs confirm 3%: a Roast Boar (+3 Might) took one player's damage share from ×1.23 to ×1.33 | At the game's value, the stack test's own case breaks the damage reach at tier III (×1.37) and IV (×1.45). The test passes only because it reads the wrong rules | The ×1.35 damage guarantee is not true in the game |
| 2 | **Extra food slots are not bounded** (review item K3, still open). The game allows 4 meals and 3 tonics; the test counts 2 and 1 | Tier IV toughness ×1.84 of the kit (bound ×1.5) | Food is the biggest source a player stacks before every raid |
| 3 | **Empowering and better makes are outside every bound.** Masterwork +3 adds 80% of a piece's stats and 24% weapon damage | A maxed iron kit equals plain moonsilver in damage and is ×1.53 as tough. A maxed moonsilver kit is ×1.72 as tough as a plain Trollstone kit | "Change gear first" stops being true: an old maxed kit beats the next tier's new one |
| 4 | **Nothing is tuned for the top.** Trollstone (tier V) gear exists; no raid has a tier V. Everything stacked on Trollstone gear makes tier IV about **12 times** easier than its entry (work ×0.08) | A dead tier and a runaway at once | The end game has nothing to spend its power on |
| 5 | **Co-op party health adds to the tier instead of multiplying it.** +60% creature health a friend is added to the tier's +220% | Four players at tier IV face ×1.56 a solo creature; at tier I ×2.8 | Groups get relatively stronger the higher they go |
| 6 | **Mounted damage multiplies everything and is in no bound** | Up to ×1.98 on every swing and arrow from a galloping horse | Horses go through the portal now |
| 7 | **Heals are flat while health grows** | A draught heals 20% of health at tier I and 68% at tier IV (88% with Potent Remedies) | Sustain climbs a tier's ladder faster than the danger |

**Dead or flat stretches.** Tier I has almost no damage step (every tier I weapon is 12; mastered adds ×1.06 damage). Tier V has gear but
no raid. Meal-duration perks do little on raids that last 4-5 minutes. Horse trample (flat 24-39) is worthless past tier II.

**Mandatory sources.** Each tier's gear (by design; copper to iron is the steepest wall: ×2.97 the work). Perks and enchantments: tiers are
tuned to a kit with them, so a player without them finds every tier 21-26% harder. The tier III entry kit counts the Grave Blade (25); with
the Iron Sword (20) tier III takes ×1.24 the hits to kill, outside the test's ±20%.

**What the logs say.** Only tier I has been played: 12 raids, 2 deaths, gear 14-16 against 10 recommended. Players eat almost nothing
before raids (one meal in 12 raids). Wolf bites take ~10% of health, as the model says. A landed blow averages 12.5 damage, about 40%
above a plain swing: technique (backstabs, heavies, rushes) is a large power source the model leaves out.

**Top recommendations** (details and effort in §5):

1. Let `GearBalance` read the game's own `CombatRules` asset (S), then fix the data the test then fails on.
2. Settle K3 with a **one-boon-per-family rule** (Hearty, Fierce, Swift, Warding): extra slots add breadth, not depth (M).
3. Halve empowering's stat share (Essence Settings › Stat Share 0.2 → 0.1), so stats rise as the score does (S).
4. Add a tier V row tuned to the Trollstone entry kit (suggested health ×4.2, damage ×5.1) (M).
5. Multiply co-op party health by the tier's health scale, as elites already do (S).
6. One `PowerCurveTests` class that checks every stack against bounds kept in a settings asset (M).

---

## 1. Every power source

### 1.1 The player's side

"Bound" says whether a test or a runtime cap limits it. "Stacks" says how it combines with the rest.

| Source | What it gives | Most it gives | Arrives | Bound | Stacks |
|---|---|---|---|---|---|
| **Weapon** | Damage of every blow | One-handed 12 / 16 / 25 / 30 / 37.5 by tier I-V; two-handed 16 / 34 / 45 / 50 | Tier N gear from tier N−1 raids | Drop caps by tier | The base every multiplier works on |
| **Armour** | Damage taken × (1 − A / (A + 100)) | Kit armour 32 / 67 / 118 / 184 / 230 (65-70% cut at IV-V) | Same | Diminishing; a blow always does at least 1 | Toughness grows in a straight line with armour (health × (100 + A) / 100) |
| **Health on gear** | Flat health | Kit health 150 / 160 / 230 / 240 / 268 | Same | None | Flat, then health shares multiply it |
| **Might on gear** | Strength or Physical Power: +3% damage a point in play | 4 (I), 6 (II), 4 (III), 0 (IV), 4 (V Maul) | Same | None | One additive bucket with Might from food and perks |
| **Gear score** | Nothing by itself: reads the gear | 18 / 38 / 61 / 81 / 109 | – | – | Picks the portal's default tier and floor |
| **Fine / Masterwork** | Half / one level of empowering (+10% / +20% of a piece's positive stats; weapons +3% / +6% damage) | Up to 60% chance at Weaponcrafting or Armorcrafting 50, a quarter of it Masterwork. Runed Make / Runed Armour (40): a Masterwork comes empowered once | From the first craft; worthwhile from skill ~20 | **None** | Adds to empowering levels |
| **Empowering** (altar) | +20% of positive stats a level; weapons +6% Damage Bonus a level | 3 levels. With Masterwork: +80% of stats, +24% damage | Once the Purification Altar stands (reached in every long playtest) | **None.** Left out of the stack test on purpose: "it raises the score, so the planner counts it as the next tier's" | Damage Bonus joins one additive bucket |
| **Enchantments** (Inscriptions) | One per piece; a kind counts once (`GearPower.CountsEnchantment`). Stat ones: +4% health, +4-6 armour, +6% crit, +15% crit damage, +10% damage for −15 health; great ones are verbs | Budget ×1.12 toughness, ×1.08 damage | Known 8 from the start; the rest from raid inscriptions, carved at Runecraft 5-30 | **Yes:** `RaidTierTests.Enchantments_AddAtMostTheirBudget` | Into the same stat buckets |
| **Relics** | A power on found gear, up to one tier above the raid. Moonbound: +30% damage in the dark, −15% by day. Wolf-Fed: a kill heals 6%. Trollblooded: regrows 2.5% a second when calm. Twice Buried: 30% to rise again | Elites 5 / 10 / 15% at II-IV; caches 6% | Tier II on | **None.** The stack test skips the Relic school | Moonbound joins the Damage Bonus bucket |
| **Melee level** | Swing damage −12% (level 1) → 0 (10) → +20% (50); stamina a swing 125% → 80%; a block 125% → 75% | +5 / 10 / 15% at the expected 20 / 30 / 40 | Level 10 reached at 48-55 played minutes in the logs | Counted in the tiers (`LevelingSettings.ExpectedLevel`) | Its own multiplier on every swing |
| **Melee perks** | 10: Thick Skin +15 health / Weapon Drill +3 Might. 20: Shield Arm +8 armour / Riposte. 30: Endurance / Hunter's Patience +20% crit damage. 40: Unbroken +3% resist / Second Wind. 50: Warden / Reaver +10% melee | About +15% toughness, +3-9% damage | One choice each 10 levels | Counted in the stack test | Into the stat buckets |
| **Weapon flourish** | From Melee 30: one-handed arts +30% and half Focus back on a kill; two-handed arts stagger (poise ×2.5) | – | Melee 30 | Not counted (a verb) | Multiplies art blows |
| **Sneaking** | Ambush damage +25% (20), +35% (40), +30% (50): **+90%** on a sure critical first blow; Silent Takedown kills an ordinary creature with up to 4× the blow's health | One opening blow a fight | Sneaking 20-50 | Not counted | Multiplies the opening blow |
| **Horsemanship** | Mounted damage −10% → +20% (level), Lancer +18% (40), Rider of the North +20% (50); ×1.25 more at a gallop | **×1.98** on every swing and arrow from the saddle | Horses come through the portal (`HorseTravel`) | **None** | A separate multiplier on the blow |
| **Horse trample** | Flat 24 (wild horse ×1.5) or 30 (Trample technique), × mounted damage | 24-39 a creature | Same | – | Flat: half a wolf at tier I, 3% of a Draug at IV |
| **Gathering and crafting perks** | No fight numbers since 2026-11-15, but small resistances and stamina | – | – | Counted (adds nothing) | – |
| **Cooking** | Meals last −20% → +50% (level), perks up to +75%; Third Plate (20): +1 meal slot; Hearty Meals (40): a meal heals 25% | Haven Feast 720 s → 27 min | Cooking 10-50 | **Slots not bounded** (K3) | Duration only, but the slot is depth |
| **Alchemy** | Tonics last −20% → +50%, perks up to +60%; Potent Remedies (20): draughts heal +30%; Second Vial (30): +1 tonic slot; Spare Dose (40): 25% a draught isn't used | Ironbark 240 s → 8 min | Alchemy 10-50 | **Slots not bounded** (K3) | Same |
| **Meals** (2 slots) | Haven Feast +10% health +12 armour; Hunter's Stew +10% health; Roast Boar +3 Might; Smoked Boar +2 Might +4% health; Honey Mead +10% damage −5 armour; Trout in Cream +8% damage | 4 slots: base 2, Kitchen III (needs moonsilver), Third Plate | Tier I meals at once; best ones at II-IV | 2 counted, 4 allowed | Health shares and Damage Bonus are additive buckets; armour flat |
| **Tonics** (1 slot) | Ironbark +32 armour +5% resist; Cursed +20% damage −8 armour; Purified +10% health; Barkskin +14 armour | 3 slots: base 1, Cauldron III (needs iron), Second Vial | **None at tier I**; from tier II | 1 counted, 3 allowed | Same |
| **Heals** | Healing Salve 30, Healing Draught 60, Greater 120, Radiant 180, Mending 30 at once + 40 over time; Bandage 15 | Flat | II-IV for draughts (Orb Tincture gates them) | None | Flat against a growing health pool (§2.7) |
| **Well Rested** | +10% health, +15% stamina, +20% experience | Comfort × 60 s | **Given in full at every raid launch** (`HavenReadiness.GiveRest`) | Counted only in "mastered" | So it is part of every raid's start, not a mastery reward |
| **Regain** | 40% of a blow can be won back by striking within 3 s | – | Always | – | Sustain, scales with the blow |
| **Technique** | Backstab ×1.75 on a staggered foe (×1.35 mid-strike), counter ×1.3, staggered foes take ×1.2, charged heavy ×1.4, rhythm up to +10%, arts 1.9-2.6× a swing | In the logs a landed blow averages ~40% above a plain swing | Always | Not counted (skill, not stats) | Multiplies the blow |
| **Mentors** | +5% experience a building tier | – | – | – | Pace, not power |
| **Insight** | Experience into any skill, full below 20, half above | – | – | – | Catches a lagging Melee up |

### 1.2 Home bonuses that reach raids

Few do, and that is good. `healthShare`, `fightShare` and `turnBackShare` (Iron Bands, Brukam's Horn, Hag's Knot) only change walls and the
Watch Post at home. Haven rank's ward bonus is the home's light. What does cross the portal:

| Source | What it gives raids | Kind |
|---|---|---|
| Kitchen III (The Long Table) | +1 meal slot | Power (food depth) |
| Cauldron III (Master's Shelf) | +1 tonic slot | Power |
| Comfort | Length of Well Rested | Power, but given at every launch |
| Ward levels I-III | +90 s a level; open tiers II-IV | Time, access |
| Sigrid at home | +120 s | Time |
| Watch Post II-III, Map Table | Rumours and foretold events | Knowledge |
| Portal: Steadied, Moonlit, the Secure ring | Safe pocket slots | Risk, not power |
| Portal: Bound | Extraction 20% faster | Risk |

### 1.3 Companions, horses and co-op

| Source | What it gives | Scales with the tier? | Observed |
|---|---|---|---|
| **Raid companion** (Fighter) | 9 damage every 1.6 s, about **60%** of the tier I entry player's swing damage a second | **Yes**: health × the raid's enemy power, damage × its enemy health (`RaidCompanions.Toughen`), so a constant share at every tier | 14% of all damage on creatures killed in raids (1,427 of 10,461) |
| Bond | Trusted (3 raids) +15% health / +10% damage; Sworn (8) +30% / +20% | On top | – |
| **Horse** | Mounted multiplier and trample (§1.1); a second body monsters attack | Health does not scale | Unplayed |
| **Co-op friends** | Creature health +60% a friend (`MultiplayerSettings.creatureHealthPerFriend`) | **Added** to the tier's share (§2.6) | Unplayed with two people |

### 1.4 The enemy side

| Lever | I | II | III | IV | V |
|---|---|---|---|---|---|
| Health × (Misty Woods, Guardian's Hollow) | 1 | 1.5 | 2.5 | 3.2 | – |
| Health × (Troll Valley, Moss Marsh, Brukam's Bridge) | – | 1.4 | 2.3 | 2.8 | – |
| Damage × (all raids) | 1 | 1.6 | 2.85 | 4.05 | – |
| Creatures × | 1 | 1.15 | 1.3 | 1.45 | – |
| Elites a run / affixes each | 1 / 1 | 1-2 / 1 | 2-3 / 1 | 3-4 / 2 | – |
| New creatures | Wolves, boars, rootlings, spitters, lights, mistwings, small trolls | Root Guardians, Moss Shamans | Draugs | None | – |
| New abilities | – | Pounce, Volley | Trample, Ground Slam, Troll Rush, Troll Charge, Volley | War Cry (+15%), Call Roots, Calls Its Kin | – |

Multiplied on top: the raid's danger stages (+10% / +20% / +35% health and damage at 480 / 240 / 60 s left), night (×1.1 health), Blood
Moon (×1.5) and Deep Woods (×2). Elite affixes multiply each other and the tier: Ironhide ×1.8 health +30 armour, Frenzied ×1.2,
Regrowing / Frostbound / Mistcloaked ×1.4 (Frostbound ×1.1 damage), Vengeful ×1.3, Rallying ×1.5. Bosses add +10% / +20% damage
by phase. All of these multiply correctly; only co-op adds.

**Signature creatures grow too.** A tier's new creatures carry far more base health: wolf 58.5, Blight Boar 101.5, Draug 286, Root Guardian
468. The tier multiplier lands on top of that. The balance model's reference creature (60 or 100 health) does not see the shift (§2.5).

---

## 2. The model: entry and mastered, tiers I to V

### 2.1 How it is computed

The code's own formulas (`GearBalance`, `FightMath`, `DamageCalculator`):

- **Damage of a swing** = weapon × (1 + Might × per point) × (1 + Damage Bonus) × (1 + Melee) × (1 + crit chance × crit damage).
  Crit starts at 5% and +50%.
- **Toughness** = health × (1 + health shares) / ((1 − armour / (armour + 100)) × (1 − resist)).
- **Hits to kill** = creature health × tier health / (damage × (1 − its armour cut)). **Hits to die** = toughness / (blow × tier damage).
- **Work** = hits to kill / hits to die. Lower is easier. Tier I's entry kit is 1.00.
- **Entry kit** (`EntryKitOf`): the tier's gear, the Melee level expected there, the perks and enchantments reached by the tier before. No food,
  no rest. **Mastered** (`MasteredKitOf`): perks and enchantments of the tier itself, 2 meals, 1 tonic, Well Rested.
- **Damage a second** = damage × Player Damage 0.7 (`CombatTechniqueSettings`) × the weapon's combo rate (a sword's four swings deal
  4.34 weapon-damages in 4.04 s: ×1.075 a second; an axe ×1.088; a greatsword ×0.716), against the creature's armour. This is nonstop
  swinging; real fights swing perhaps 60% of the time.

### 2.2 The kits

| Tier | Weapon | Score | Damage a swing | Armour | Health | Toughness | Step from the tier before (damage / toughness) |
|---|---|---|---|---|---|---|---|
| I | Knotted Club 12 | 18 | 12.8 | 32 | 150 | 198 | – |
| II | Copper Hand Axe 16 | 38 | 18.8 | 67 | 160 | 267 | ×1.47 / ×1.35 |
| III | Grave Blade 25 | 61 | 29.3 | 118 | 230 | 501 | ×1.56 / **×1.88** |
| IV | Moonsilver Blade 30 | 81 | 37.6 | 184 | 240 | 682 | ×1.28 / ×1.36 |
| V | Trollstone Blade 37.5 | 109 | 49.0 | 230 | 268 | 884 | ×1.30 / ×1.30 |

"Damage a swing" is the kit's expected blow: the weapon, Might, crits at their chance, and the Melee level the tier expects (+0 / 5 / 10 /
15 / 20%), at the model's 1% a Might point. Iron is the biggest step. Moonsilver and Trollstone are smaller ones, which matters for
empowering (§2.6).

### 2.3 Entry and mastered, as the tests see them

Misty Woods multipliers; the tests' creature (100 health, 10 armour, blows of 15). Ratios are against tier I's entry kit.

| Tier | Kit | Damage | Toughness | Hits to kill | Hits to die | Work |
|---|---|---|---|---|---|---|
| I | Entry (plain) | 12.8 | 198 | 8.6 (×1.00) | 13.2 (×1.00) | 1.00 |
| I | Mastered | 13.5 | 265 | 8.1 (×0.94) | 17.7 (×1.34) | 0.70 |
| II | Entry | 19.3 | 320 | 8.5 (×0.99) | 13.4 (×1.01) | 0.98 |
| II | Mastered | 25.1 | 373 | 6.6 (×0.76) | 15.6 (×1.18) | 0.65 |
| III | Entry | 32.2 | 565 | 8.5 (×0.99) | 13.2 (×1.00) | 0.99 |
| III | Mastered | 39.4 | 693 | 7.0 (×0.81) | 16.2 (×1.23) | 0.66 |
| IV | Entry | 41.1 | 806 | 8.6 (×1.00) | 13.3 (×1.01) | 0.99 |
| IV | Mastered | 49.3 | 1,087 | 7.1 (×0.83) | 17.9 (×1.36) | 0.61 |

The next tier for a mastered player: ×2.40 the work (I → II), **×2.97** (II → III), ×1.82 (III → IV). The II → III wall is the steepest
because iron is the biggest gear step: it is the clearest "get the new gear first" moment in the game.

Tier I's mastered damage is only ×1.06: there are no tonics at tier I and every tier I weapon hits for 12. At tier I gear and food keep
you standing; nothing makes you kill faster but skill.

### 2.4 What each source adds

Stepwise, at the model's rules (0.01 a Might point). "×" is against the first row.

**Tier II**

| Step | Damage | × | Toughness | × | Work |
|---|---|---|---|---|---|
| Gear, Melee at 10 | 17.9 | 1.00 | 267 | 1.00 | 1.27 |
| + Melee level 20 | 18.8 | 1.05 | 267 | 1.00 | 1.21 |
| + perks | 18.8 | 1.05 | 306 | 1.15 | 1.06 |
| + enchantments | 19.3 | 1.08 | 337 | 1.26 | 0.93 |
| + 2 meals, 1 tonic, rested (mastered) | 25.1 | 1.40 | 373 | 1.40 | 0.65 |
| + every food slot | 25.6 | 1.43 | 435 | 1.63 | 0.55 |
| … at the game's 0.03 a Might point | 31.5 | 1.58 | 415 | 1.55 | 0.46 |
| + Masterwork and +3 on every piece | 42.3 | 2.12 | 694 | 2.60 | 0.21 |

**Tier III**

| Step | Damage | × | Toughness | × | Work |
|---|---|---|---|---|---|
| Gear, Melee at 10 | 26.6 | 1.00 | 501 | 1.00 | 1.35 |
| + Melee level 30 | 29.3 | 1.10 | 501 | 1.00 | 1.23 |
| + perks | 29.6 | 1.11 | 554 | 1.10 | 1.10 |
| + enchantments | 30.8 | 1.16 | 602 | 1.20 | 0.97 |
| + 2 meals, 1 tonic, rested (mastered) | 39.4 | 1.48 | 693 | 1.38 | 0.66 |
| + every food slot | 42.5 | 1.60 | 826 | 1.65 | 0.51 |
| … at the game's 0.03 | 51.6 | 1.80 | 770 | 1.53 | 0.45 |
| + Masterwork and +3 | 62.1 | 2.16 | 1,655 | 3.30 | 0.18 |

**Tier IV**

| Step | Damage | × | Toughness | × | Work |
|---|---|---|---|---|---|
| Gear, Melee at 10 | 33.2 | 1.00 | 682 | 1.00 | 1.45 |
| + Melee level 40 | 38.2 | 1.15 | 682 | 1.00 | 1.26 |
| + perks | 39.5 | 1.19 | 768 | 1.13 | 1.08 |
| + enchantments | 41.1 | 1.24 | 832 | 1.22 | 0.96 |
| + 2 meals, 1 tonic, rested (mastered) | 49.3 | 1.48 | 1,087 | 1.59 | 0.61 |
| + every food slot | 56.8 | 1.71 | 1,385 | 2.03 | 0.42 |
| … at the game's 0.03 | 61.5 | 1.85 | 1,340 | 1.97 | 0.40 |
| + Masterwork and +3 | 80.3 | 2.41 | 2,881 | 4.23 | 0.14 |

Reading it: **food is the largest source a player controls each raid; empowering is the largest permanent one.** Skill levels add
5-15% damage; perks and enchantments together about a quarter of toughness. Together, a fully prepared tier IV player does the tier's
work in 14% of the effort its multipliers were tuned for.

### 2.5 Real seconds and blows

Entry kit, nonstop swinging, Misty Woods multipliers. A companion would cut "time to kill" by about 14% (logs) to 37% (if it always fights).

| Tier | Against | Kit | Damage a swing | Damage a second | Its health | Time to kill | Toughness | Its blow lands for | Blows to die |
|---|---|---|---|---|---|---|---|---|---|
| I | Wolf | Entry | 12.8 | 9.3 | 58 | 6.3 s | 198 | 13.6 | 11.0 |
| I | Wolf | Mastered | 13.5 | 9.8 | 58 | 6.0 s | 265 | 12.8 | 14.7 |
| II | Wolf | Entry | 19.3 | 14.0 | 88 | 6.3 s | 320 | 16.4 | 11.1 |
| II | Blight Boar | Entry | 19.3 | 13.6 | 152 | 11.2 s | 320 | 21.8 | 8.3 |
| II | Blight Boar | Mastered | 25.1 | 17.7 | 152 | 8.6 s | 373 | 22.3 | 9.7 |
| III | Draug | Entry | 32.2 | 19.4 | 715 | **36.8 s** | 565 | 34.7 | 6.9 |
| III | Draug | Mastered | 39.4 | 23.7 | 715 | 30.1 s | 693 | 36.0 | 8.4 |
| IV | Draug | Entry | 41.1 | 24.8 | 915 | **37.0 s** | 806 | 38.4 | 6.9 |
| IV | Draug | Mastered, every slot | 56.8 | 34.2 | 915 | 26.8 s | 1,385 | 30.9 | 11.9 |
| IV | Root Guardian | Entry | 41.1 | 22.9 | 1,498 | **65 s** | 806 | 60.7 | 4.4 |

**An elite at tier IV.** A Draug that is Ironhide and Frostbound: 286 × 3.2 × 1.8 × 1.4 = 2,306 health and 55 armour. The entry kit needs
**116 s** of nonstop swinging (late stage at night: 172 s); the most prepared kit 84 s. Its blow lands for 42, so 6.3 blows kill the
entry player.

The reference-creature model keeps a wolf-sized fight level across tiers. The fights players will actually meet grow from 6 s (a wolf at
tier I) to 37 s (a Draug at III-IV) and 65 s (a Guardian at IV), because a tier's own creatures start bigger and the tier multiplies them.
Whether that is the intended feel is open question 5.

### 2.6 What no test sees

Toughness and damage are against the tier's plain kit, without enchantments, as `RaidTierTests` measures the reach. Work includes
enchantments.

| Case | Toughness | Damage | Work against the tier's entry | Bound today |
|---|---|---|---|---|
| Stack test, base slots, model rules (what passes) | T3 ×1.28, T4 ×1.48 | T3 ×1.29, T4 ×1.26 | – | ×1.5 / ×1.35: passes |
| Same, **at the game's 0.03 a Might point** | T3 ×1.24, T4 ×1.33 | **T3 ×1.37, T4 ×1.45** | – | **Fails** |
| Every slot (4 meals, 3 tonics), model rules | T3 ×1.53, **T4 ×1.84** | T3 ×1.39, T4 ×1.45 | T4 0.42 | **Fails**, untested |
| Every slot, game rules | T3 ×1.43, T4 ×1.78 | T3 ×1.57, T4 ×1.57 | T4 0.40 | **Fails**, untested |
| Masterwork +3 on the tier's plain kit, against the next tier's plain kit | T3 ×1.53, T4 ×1.72 | T3 ×1.00, T4 ×1.04 | – | None |
| Plain Trollstone kit in a tier IV raid | – | – | 0.76 | None |
| Trollstone, mastered at level 50 | – | – | 0.34 | None |
| Trollstone, every slot, game rules, Masterwork +3 | 3,859 | 113 | **0.08** | None |
| Mounted at a gallop, Horsemanship 50 | – | ×1.98 on top of anything | – | None |

The Might mismatch does **not** break the tier multipliers: tier I's kit gains from the extra Might too, and entry kits stay within 5% of
tier I's fight either way. It breaks the stack bound, where Might-heavy food (Roast Boar +3, Smoked Boar +2) and Weapon Drill count
three times what the model thinks. The game's own Fight card already uses the 0.03 rules, so the card and the raid tier window disagree.

**Co-op dilution.** Party health is a percent modifier added to the tier's percent (`CreatureHost` line 264), while elite affixes are
multiplied by the tier's scale (`EliteCreature.Rescale`).

| Players | Creature health against solo, tier I | II | III | IV | Share of a creature's health each player must deal, IV |
|---|---|---|---|---|---|
| 2 | ×1.6 | ×1.4 | ×1.24 | ×1.19 | 0.59 (tier I: 0.80) |
| 3 | ×2.2 | ×1.8 | ×1.48 | ×1.38 | 0.46 (tier I: 0.73) |
| 4 | ×2.8 | ×2.2 | ×1.72 | ×1.56 | **0.39** (tier I: 0.70) |

Enemy damage does not scale with the party, and each creature splits its attention. So a group of four at tier IV kills each creature in
less than half the per-player effort it needs at tier I.

### 2.7 Runaway stacks

1. **Empowering and better makes.** No bound. +80% of every piece's stats is larger than any other single source at tiers III-IV, and it
   lets an old kit out-tough the next tier's (§2.6). Of the sources the user said should be shown and earned, it is the only uncapped one.
2. **Food slots.** ×1.84 toughness at tier IV with every slot. The K3 decision has waited since 2026-11-18.
3. **The Damage Bonus bucket.** Everything that says "+% damage" adds in one bucket: Cursed Draught 0.20, Honey Mead 0.10, Trout in Cream
   0.08, Bloodletting 0.10, empowering up to 0.24, Moonbound up to 0.30 at night. **+1.02 at most, ×2.02.** Additive buckets are the safe
   kind (PoE's "increased"), but nothing bounds the bucket's total, and relics are outside the budget.
4. **Separate multipliers on a blow.** Melee level (×1.20), mounted (×1.98), Ambush (×1.90 on the opener), backstab, counter. Each is
   "more" in PoE's sense. Mounted damage is the only one that applies to every blow and is in no bound.
5. **Trollstone in tier IV** (×0.08 work at the extreme).
6. **Co-op at high tiers** (§2.6).
7. **Sustain.** Heals are flat and grow by tier faster than health:

| Tier | Best heal | Heals | Entry health | Share of health | With Potent Remedies | Entry blow taken (signature creature) | Blows a draught undoes |
|---|---|---|---|---|---|---|---|
| I | Healing Salve | 30 | 150 | 20% | 26% | 13.6 (wolf) | 2.2 |
| II | Healing Draught | 60 | 182 | 33% | 43% | 16.4 (wolf) | 3.7 |
| III | Greater Healing Draught | 120 | 239 | 50% | 65% | 34.7 (Draug) | 3.5 |
| IV | Radiant Draught | 180 | 265 | 68% | **88%** | 38.4 (Draug) | 4.7 (6.1) |

A tier IV draught is worth three tier I draughts against the same share of danger. Life Orbs gate how many there are, which is why this
is a slope, not a break.

### 2.8 Dead and flat stretches

| Where | What is flat | Size |
|---|---|---|
| Tier I damage | All tier I one-handers deal 12; no tonics exist; mastered is ×1.06 damage | Only Melee level and technique raise damage. The logs show players are fine with this |
| Melee levels between milestones | +0.5% damage a level | By design (Dragonwilds); the milestones carry the feel |
| Tier V | Trollstone gear (score 109), no tier V raid | A whole tier of gear with nothing to measure it against. A maxed moonsilver kit is ×1.72 as tough as plain Trollstone, so the Trollstone set is not even an upgrade for a maxed player |
| Moonsilver step | ×1.28 damage, ×1.36 toughness (iron was ×1.56 / ×1.88) | The tier with the longest grind (about 12 raids) has the smallest gear step |
| Meal duration perks on raids | Haven Feast lasts 12 min at Cooking 10 (about 10 min at Cooking 2, where players are); raids last 4-5 min (logs) | Slow Simmer, Spice Lore, Strong Stomach and Hearth Master add little to a raid. Tonic duration still matters (120-300 s) |
| Horse trample past tier II | Flat 24-39 | 3% of a tier IV Draug |
| Hunter's Patience | +20% crit damage at 11% crit chance | About +2% damage; Endurance is the real choice |

### 2.9 Mandatory and worthless

**Mandatory.**
- **The tier's gear.** By design. Entering tier III in copper is ×2.97 the work.
- **Perks and enchantments.** The entry kit counts both. Without them a tier is 21-26% more work (the "gear, Melee at expected level" rows
  in §2.4: 1.21 / 1.23 / 1.26). A player who never visits the altar is quietly behind at every tier.
- **The Melee level the tier expects** (20 / 30 / 40). A player still at 10 when they enter tier IV deals 15% less (work 1.45).
- **The Grave Blade at tier III.** `KitOf` picks the strongest one-hander, the Grave Blade (25, Grave Iron from barrows and Draugs). With the
  Iron Sword (20), tier III's entry takes ×1.24 the hits to kill, outside the test's ±20%.

**Worthless or close to it** (for raid power): meal-duration perks, trample past tier II, Trollstone gear for a maxed moonsilver player,
rarity's score bonus (score only), and the Tusk Ring against the Copper Band in a fight (stamina against Might).

---

## 3. What the playtest logs show

### 3.1 Which runs are real

- 1,783 CSVs. 21 run 20 played minutes or more. Most are editor or agent probes: short, or starting in a raid scene.
- **Kept:** `20261119-184435` (84 min), `20261120-214517` (82 min), `20261121-151353` (165 min, ended 2026-11-22 15:05). All start with
  "new run" and the prologue, have Kit and Hurt rows, and have no Cheat rows. The last one's last write is 4 s before `Player-prev.log`'s,
  so it is the built game. The other two are the long runs the economy research identified on 11-19 and 11-20.
- **Left out:** `20261115-184337` (49 min) has 13 Cheat rows.
- **Sample:** probably one player, 12 raids, **all Misty Woods tier I**. Nothing here says anything about tiers II-V.

### 3.2 Raids and kits

| | Value |
|---|---|
| Raids | 12: 10 extracted, 2 died |
| Deaths | A Bog Spitter in daylight at 273 s (93 damage in big blows; the rest poison and small blows); the Old Moss Troll at night, one blow of 89 (60% of 148 health) |
| Raid length | 108-652 s, median about 260 s |
| Gear score | 14-16 against 10 recommended (a full tier I kit is 18) |
| Kit at the start | 148 health (135 + Well Rested), armour 28-30, damage share ×1.02 → ×1.33, toughness 190-219 |
| Boons at the start | Well Rested every raid; **one meal in 12 raids** (Roast Boar) |
| What was eaten (all places) | Stewed Berries 17, Healing Draught 6, Bandage 5, Roast Boar 3, Mushroom Skewer 2, Antidote 1 |

The Roast Boar raid is the cleanest evidence for finding 1: damage share went ×1.23 → ×1.33. Three Might at 3% a point on a first factor of
1.12 gives exactly ×1.08; at 1% it would have been ×1.03.

### 3.3 Blows taken and given

**Hurt rows** (blows of 5% of health or more), raids, prologue and home together:

| Attacker | Blows | Median | Most | Share of 148 health |
|---|---|---|---|---|
| Wolf | 137 | 14-15 | 16 | ~10% (the model: 13.6, 11 blows to die) |
| Blight Boar | 9 | 11 | 19 | 7-13% |
| Mistwing, Marsh Light, Bog Troll, Black Widow | 22 | 9-11 | 11 | ~7% |
| Draug (in a tier I raid, late in the 11-21 run) | 5 | 24 | 24 | 16% |
| Old Moss Troll | 1 | 89 | 89 | 60% |

**Kill rows in raids** (137 kills): the player dealt 86% of the damage, the companion (Ingrid) 14%.

| Creature | Kills | Player's blows (median) | Its health | Seconds |
|---|---|---|---|---|
| Wolf | 53 | 5 | 64 | 4.8 |
| Mistwing | 27 | 4 | 58 | 4.7 |
| Rootling | 26 | 3 | 36 | 2.0 |
| Blight Boar | 8 | 9 | 156 | 7.6 |
| Marsh Light | 8 | 5 | 52 | 6.0 |
| Black Widow | 4 | 17 | 352 | 21.8 |
| Bog Troll | 2 | 16 | 210 | 30.3 |

A landed blow averaged **12.5 damage** (724 blows), 15% of them critical. A plain tier I swing in the model is about 9. Technique adds
about 40%: in raids the player made 59 backstabs, 102 heavies and 140 rushes against 737 light swings, but only 3 parries, 3 perfect
dodges and **no weapon arts**. 18% of swings missed.

The model's 6.3 s for a wolf (nonstop swinging) against 4.8 s observed: real play is faster at tier I, because of technique and the
companion. Big creatures already take 20-30 s at tier I (Black Widow, Bog Troll), which supports §2.5's point that fights lengthen.

### 3.4 Skills

| Skill | Highest reached (three runs) |
|---|---|
| Melee | 12, 15, 14 (level 10 at 31-56 played minutes; some levels from Insight) |
| Foraging | 11, 10, 13 |
| Woodcutting | 5, 8, 8 |
| Construction | 3, 4, 9 |
| Mining, Sneaking, Cooking | 3-5, 4, 2 |

Melee is on pace for the 20 that tier II expects. Cooking and Alchemy sit at 1-2, so a real player's meals last 20% shorter than the
model assumes and the extra slots from Third Plate and Second Vial are far away. That is fine at tier I; the slots matter at III-IV.

---

## 4. How other games shape a power curve

| Game | How power grows | What it does well | What goes wrong | What Safe Haven can take |
|---|---|---|---|---|
| **V Rising** | Gear level is the sum of worn gear; enemies have a level; ±1% / −4% damage dealt and taken per level of difference ([wiki](https://vrising.fandom.com/wiki/Gear_Level), [g-portal](https://g-portal.com/en/blog/vrising-gear-level-guide-en)) | One readable number; under-levelling hurts at once | A hidden modifier on top of real stats: punished twice | Already taken (sum of slots) and already refused (the hidden modifier). Keep both |
| **Valheim** | Biome tiers gated by bosses; food sets most of your health and stamina; three different dishes at once ([PC Gamer](https://www.pcgamer.com/games/survival-crafting/valheim-biome-order/), [eip.gg](https://eip.gg/valheim/guides/how-food-health-and-stamina-work-valheim/)) | Food is a choice before every trip; "different dishes" stops one stat stacking | Food is so central that unfed play feels broken | The **no-duplicates rule is the model for K3**: extra slots should add variety, not more of the same |
| **Enshrouded** | Flame Level gates crafted tiers (item levels 10-45); runes upgrade gear a level at a time; uniques re-craft at your current level ([allthings.how](https://allthings.how/enshrouded-1-0-craftable-armor-sets-and-the-flame-level-that-unlocks-each-one/), [upgrades](https://allthings.how/enshrouded-1-0-how-to-upgrade-weapons/)) | Upgrading keeps a favourite piece alive into the next band | Upgrade reach past the cap (55 against 50) blurs the bands | Empowering should **reach** the next tier, not pass it |
| **RuneScape: Dragonwilds** | Power level read from gear tier; skills with small per-level bonuses and set milestones ([allthings.how](https://allthings.how/how-progression-works-in-runescape-dragonwilds-full-guide/), [gear guide](https://xgamingserver.com/blog/runescape-dragonwilds-gear-guide/)) | Gear gates, skills colour | – | Already the model for Skills. Keep levels small and perks the punctuation |
| **Path of Exile** | "Increased" modifiers add, "more" modifiers multiply; resistances cap at 75% (90% hard), physical reduction at 90% ([poewiki](https://pathofexile.gamepedia.com/Increase), [maxroll](https://maxroll.gg/poe2/getting-started/damage-scaling), [resistance cap](https://www.poe2wiki.net/wiki/Maximum_Resistances)) | Players can reason about stacking; caps protect the defence side | "More" multipliers compound into extreme builds | Name the buckets. Safe Haven's Damage Bonus is an "increased" bucket (safe); mounted, Melee level and Ambush are "more" multipliers and need bounds |
| **Diablo III / IV** | D3 set bonuses multiplied into thousands of percent; Greater Rifts scale about 17% a level, so power chased power ([Blizzard Watch](https://blizzardwatch.com/2017/10/03/diablo-ptr-nerfs/), [dev insights](https://www.icy-veins.com/forums/topic/48105-developer-insights-balancing-class-set-design/)). D4's Loot Reborn cut affixes and made damage reduction rarer to rein it in ([Blizzard](https://news.blizzard.com/en-gb/article/24077223/galvanize-your-legend-in-season-4-loot-reborn), [PC Gamer](https://www.pcgamer.com/games/action/5-changes-diablo-4-season-4-rework)) | An endless ladder gives the top something to do | Multiplicative stacking forced an exponential difficulty curve | Keep multipliers few; if the top needs a ladder, make it a **tier V row**, not uncapped stacking |
| **Monster Hunter World** | Tempered monsters: same moves, more health and damage; armour upgraded with spheres ([RPG Site](https://www.rpgsite.net/feature/6775-monster-hunter-world-tempered-monsters-augmentation-decoration-farming-and-the-endgame-explained)) | Gear check and skill check together; the monster teaches the same moves | Defence upgrades become a chore | Tier ladders work best when the creature's moves grow with it (Safe Haven's From Tier abilities do this) |
| **Hades** | Heat: the player chooses conditions (e.g. +30% enemy health) for better rewards ([RPG Site](https://rpgsite.net/feature/10287-hades-pact-of-punishment-heat-modifiers-and-how-to-maximize-your-rewards), [wiki](https://hades.fandom.com/wiki/Heat)) | The player sets their own challenge, and it pays | – | Blood Moon and Deep Woods are this already. The end game's answer to surplus power can be **chosen heat**, not more grind |
| **Deep Rock Galactic** | Hazard raises enemy damage and damage resistance; player count multiplies resistance and numbers ([wiki](https://deeprockgalactic.wiki.gg/wiki/Hazard_Level)) | Party size and difficulty multiply each other | Exact numbers hard to see | Co-op scaling should **multiply** with the tier (finding 5) |
| **Palworld** | Many power sources and fast levelling; players call the default too easy and the end game a repeated preparation loop ([mein-mmo](https://mein-mmo.de/en/many-fans-find-palworld-too-easy-and-fast-revealing-their-attitude-towards-the-perfectly-balanced-gaming-experience,1577529/), [PalMods](https://www.palmods.gg/blog/palworld-1-0-endgame-problem)) | Generous, positive growth | Power outruns content | The tier V gap is this risk in small |

**The pattern.** Games that keep a curve readable do three things. Power is gated in bands (Valheim, Enshrouded, V Rising). Within a band,
stacking is mostly additive, or bounded by a "no duplicates" rule (PoE's increased, Valheim's dishes). Surplus power at the top is spent on
chosen difficulty (Hades heat, Greater Rifts). Games that fail let multipliers compound (Diablo III) or let power outrun content (Palworld).

---

## 5. Recommendations

The user's rules applied throughout: tiers come from measured kits (change gear first, read off multipliers, no hidden under-gear penalty);
slow but not grindy; positive incentives; numbers in ScriptableObject settings; pictures with names, words on hover.

### 5.1 Prioritised

| # | Change | Numbers | Where | Effort | Why |
|---|---|---|---|---|---|
| 1 | **Count Might as the game does.** `GearBalance` reads `Data/Combat/CombatRules.asset`, not `CombatRules.Default` | Then the stack test fails at T3 (×1.37) and T4 (×1.45) damage; fix with #2 and the data trims below | `Editor/Raids/RaidTierEditor.cs` (`Measure`, `StackedKitOf`); `FightMath` callers | S | The guarantee should be about the game the player plays. The Fight card already uses the asset |
| 2 | **Decide K3: one boon per family.** At most one Hearty, one Fierce, one Swift and one Warding meal, and the same for tonics. Extra slots add breadth (stamina, speed, warding), not more armour | Every slot today T4 ×1.84 / ×1.45. With the family rule ×1.58 / ×1.36 (model rules), ×1.56 / ×1.45 (game rules). Then trim the tier IV levers until the test passes: start with Ironbark Draught +32 → +24 armour and Cursed Draught +20% → +15% | `PlayerBoons.MakeRoomForMeal/Tonic` (a same-family boon replaces the old one); a family field on `ItemDefinition` (move the rule in `ItemTooltipView.MealFamily` into data); `GearBalance.StackedKitOf` with `FoodSlots.Every` honouring it | M | Valheim's rule. Readable as one family picture per slot. Keeps every slot worth having. The 11-18 lean (b, half-strength extras) is the fallback: ×1.65 / ×1.35 |
| 3 | **Empowering grows stats as fast as the score.** Essence Settings › Stat Share 0.2 → **0.1** (Weapon Damage stays 0.06) | A maxed kit (Masterwork +3) against the next tier's plain kit: toughness T3 ×1.53 → ×1.10, T4 ×1.72 → ×1.20; damage ×0.98-1.00. Stats then rise +10% a level, as the gear score already does | `Resources/EssenceSettings.asset`; the altar's tooltip numbers follow | S | A kept-up kit reaches the next tier (the WoW-track idea in GearScoreAndRaidTiers) but does not pass it, so new gear still matters |
| 4 | **Bound co-op by the tier.** Party health = per friend × friends × the raid's health scale, as elites do | 4 players: ×2.8 a solo creature at every tier (today ×1.56 at IV) | `Multiplayer/CreatureHost.cs` line 264 | S | Groups should feel the same at every tier |
| 5 | **Put every raid under the tier test** | Troll Valley, Moss Marsh, Brukam's Bridge use health 1.4 / 2.3 / 2.8; entry kill hits ×0.87-0.93. Either tune them to Misty Woods' 1.5 / 2.5 / 3.2, or record why they differ (a per-raid tolerance) | `RaidTierTests` (loop over every `RaidDefinition`) | S | Today only Misty Woods is checked |
| 6 | **Tune tier III to the weapon a player has.** Let `KitOf` pick the best weapon made of the tier's anchor metal, or raise the Iron Sword 20 → 24 | With the Iron Sword, tier III entry takes ×1.24 the hits to kill (band ±20%) | `GearBalance.KitOf` or `Data/Combat/Weapons/IronSword` | S | The Grave Blade needs Grave Iron; a player arriving at tier III may not have it |
| 7 | **Add tier V** to Misty Woods, Troll Valley and Moss Marsh, read off the Trollstone entry kit | Suggested health ×4.2, damage ×5.1 (game rules; ×4.2 / ×5.4 model rules), creatures ×1.6, elites 4-5 with 2 affixes, a ward or boss gate. Give it new abilities (one per signature creature) | `Data/Raids/*.asset` (Rules tab); `RaidTierTests` already loops over tiers | M | Trollstone gear needs a place to be tested; tier IV stays a fight for a tier IV kit. Alternative: declare Trollstone tier IV's mastery reward and accept ×0.34 |
| 8 | **Bound mounted damage.** Horsemanship's level bonus +20% → +10%, Lancer +18% → +10%, Rider +20% → +10%; the gallop's ×1.25 only on the charge's first blow | Most ×1.98 → ×1.30 (×1.63 on a charge) | `Data/Proficiencies/Horsemanship`, its perks; `PlayerRiding.MountedScale` | S-M | Every swing and arrow from the saddle is multiplied today |
| 9 | **Heals as a share of health.** Salve 20%, Healing 30%, Greater 35%, Radiant 40% of maximum health (Potent Remedies stays ×1.3) | Today 20% → 68% (88%) | A heal-share field on `ItemDefinition` (or reinterpret `healAmount`), `PlayerEating` | S-M | Sustain stays level with the tier's danger, as hits do |
| 10 | **Relics and the Damage Bonus bucket in the budget.** Count relic powers with the enchantment budget (a relic takes a piece's enchantment place); add one bound on the whole Damage Bonus bucket | Moonbound alone is +30% at night | `GearBalance.StackedKitOf` (stop skipping the Relic school for stat powers; count Moonbound at full dark) | S | The bucket reaches ×2.0 today |
| 11 | **Count Well Rested in the entry kit**, since every raid launches rested | Entry toughness +10% | `GearBalance.EntryKitOf` | S | The tuning and the game should start from the same player |
| 12 | **Show readiness as pictures.** In the planner, beside the gear score: the tier's expected kit as five small cards (gear, Melee level, perks, inscriptions, food families), each ticked or not, words on hover ("This tier is tuned for a player with 3 perks and 3 inscriptions. You have 2.") | – | `RaidPreparationWindow`, `FightCard`, `KitPictureCard` | M | Makes the hidden "mandatory" sources (perks, enchantments, skill) visible without walls of text |
| 13 | **Move the bounds into data.** A `PowerCurveSettings` asset (Systems Editor) holding the reaches: stack ×1.5 / ×1.35, enchantments ×1.12 / ×1.08, maxed-kit reach ×1.2 / ×1.05, co-op share | – | New `GameSettings<PowerCurveSettings>`; `RaidTierTests` constants read from it; the raid tier window prints them | S | The user asked for data-driven tuning; the bounds are design numbers |

### 5.2 A test that guards the curve

One EditMode class, beside `RaidTierTests`, using the game's own rules and the bounds from #13. It loops over every raid and every
tier, so a new tier V row or a new raid is checked the day it is added.

```csharp
public sealed class PowerCurveTests
{
    // Bounds live in PowerCurveSettings (Systems Editor), not here.
    private static PowerCurveSettings Reach => PowerCurveSettings.Current;
    private static CombatRules Rules => AssetDatabase.LoadAssetAtPath<CombatRules>("Assets/_Project/Data/Combat/CombatRules.asset");

    [Test] // Every way a player can stack (every food slot under the family rule, perks, rest; enchantments and relics in their own budget)
    public void EveryStack_StaysWithinItsReach_AtTheGamesOwnRules()
    {
        foreach (RaidDefinition raid in AllRaids())
        foreach (RaidDifficulty tier in raid.Difficulties)
        {
            GearBalance.Kit plain = GearBalance.KitOf(tier.tier, Gear, Rules);
            GearBalance.Kit stacked = GearBalance.StackedKitOf(tier.tier, Gear, enchanted: false, tier.tier, true, FoodSlots.Every, Rules);
            Assert.LessOrEqual(stacked.Toughness / plain.Toughness, Reach.StackedToughness, $"{raid.name} {tier.tier}: {Describe(stacked)}");
            Assert.LessOrEqual(stacked.Damage / plain.Damage, Reach.StackedDamage, $"{raid.name} {tier.tier}: {Describe(stacked)}");
        }
    }

    [Test] // A kept-up kit reaches the next tier but does not pass it, so new gear still matters
    public void AMaxedKit_ReachesTheNextTier_ButNotPast() { /* Masterwork +3 on every piece of tier N against KitOf(N+1) */ }

    [Test] // Gear of every tier has a raid tier to be measured in
    public void TheHighestGearTier_HasARaidTier() { /* max ItemDefinition.Tier of counted gear <= max RaidDifficulty.tier */ }

    [Test] // Party health keeps the same share per player at every tier
    public void APartysShare_IsTheSameAtEveryTier() { /* CreatureHost's health share at tier I and IV, 4 players */ }

    [Test] // A draught undoes the same share of health at every tier
    public void AHeal_IsTheSameShareOfHealthAtEveryTier() { /* best heal of tier N / EntryKitOf(N).Health within ±10% */ }

    [Test] // Mounted at a gallop, with the Horsemanship the tier expects, stays inside the damage reach
    public void FromTheSaddle_StaysWithinTheDamageReach() { }
}
```

Each assertion's message names the pieces, food and perks it counted, so a failure says what to tune (`RaidTierTests` already does this
for enchantments). Canary: a fixture that adds a fake meal of +100% armour must make the first test fail (the project's test-quality rule:
tests must be able to fail).

### 5.3 What not to change

- **The tier multipliers.** Entry kits are level to within 1%. Leave them, and fix the stacks around them.
- **The skills' small level bonuses.** Flat between milestones is the Dragonwilds feel the user chose.
- **The enchantment budget.** It works and is tested.
- **Raid modifiers and elite affixes.** They multiply with the tier correctly.

---

## 6. Open questions for the user

1. **Food (K3, open since 11-18).** Extra meal and tonic slots break the bound at tier IV (×1.84 toughness). One boon per family, half
   strength for extra slots, or one extra of each?
   **Recommended:** one per family. It reads as a picture per slot and keeps extra slots useful without stacking armour.
2. **Empowering.** Should a maxed kit (Masterwork, +3) beat the next tier's fresh gear? Today a maxed iron kit is ×1.53 as tough as plain
   moonsilver.
   **Recommended:** no. Reach it (at most ×1.2 tougher): Stat Share 0.2 → 0.1.
3. **Tier V.** Add a tier V raid row for the Trollstone set, or keep Trollstone as tier IV's reward for mastering it?
   **Recommended:** add tier V (suggested ×4.2 health, ×5.1 damage), so the top of the game has a fight and tier IV stays one.
4. **Might.** The game gives 3% damage a point; the balance tool counts 1%. Tune the tool to the game, or the game to the tool?
   **Recommended:** tune the tool to the game. Players already see the 3% on their Fight card and in the Kit rows.
5. **Big creature fights.** With the entry kit a Draug takes about 37 s of nonstop swinging at tiers III-IV (about a minute in real play),
   an elite Draug at tier IV about two minutes, a wolf at tier I 6 s. Is that the feel you want?
   **Recommended:** aim for about 25 s for a Draug at entry (its base health 286 → about 200), and keep elites at about one minute.

Also recommended without a question (small, clearly bugs or drift): co-op party health multiplied by the tier (#4), all raids under the
test (#5), Well Rested in the entry kit (#11).

---

## Sources

Internal: `Docs/GearScoreAndRaidTiers.md`, `Docs/PlayerStatsResearch.md`, `Docs/EnchantingResearch.md`, `Docs/Skills.md`,
`Docs/EconomyAndPhases_2026-11-22.md`, `Docs/SystemDesignReview_2026-11-17.md` (K3), `Docs/AgentDecisions_2026-11-17.md`,
`Docs/AgentDecisions_2026-11-18.md`. Code: `Editor/Raids/RaidTierEditor.cs` (`GearBalance`), `Editor/Tests/RaidTierTests.cs`,
`Combat/FightMath.cs`, `Combat/DamageCalculator.cs`, `Combat/CombatRules.cs`, `Combat/StatSheet.cs`, `Essence/GearPower.cs`,
`Essence/RelicEffect.cs`, `Survival/PlayerBoons.cs`, `Settlement/HavenReadiness.cs`, `Raids/RaidParameters.cs`, `Raids/RaidCreatures.cs`,
`Raids/RaidCompanions.cs`, `Raids/Elites/EliteCreature.cs`, `Multiplayer/CreatureHost.cs`, `Player/PlayerCombat.Swings.cs`,
`Horses/PlayerRiding.cs`, `Horses/HorseMotion.cs`, `Progression/LevelingSettings.cs`, `Progression/ProficiencyDefinition.cs`. Data:
`Data/Items`, `Data/Combat` (CombatRules, Weapons, Attacks, Stats), `Data/Monsters`, `Data/Raids` (tiers, elites, modifiers),
`Data/Proficiencies`, `Data/Buildings`, `Data/Companions`, `Resources/*Settings.asset`. Playtest CSVs `20261119-184435`,
`20261120-214517`, `20261121-151353` (and `20261115-184337`, excluded for cheats).

External:

- V Rising: [Gear Level (wiki)](https://vrising.fandom.com/wiki/Gear_Level); [g-portal gear level guide](https://g-portal.com/en/blog/vrising-gear-level-guide-en)
- Valheim: [PC Gamer: biome order](https://www.pcgamer.com/games/survival-crafting/valheim-biome-order/); [eip.gg: how food, health and stamina work](https://eip.gg/valheim/guides/how-food-health-and-stamina-work-valheim/); [PC Gamer: food guide](https://www.pcgamer.com/best-valheim-food-recipes-combo-list)
- Enshrouded: [armour sets by Flame Level](https://allthings.how/enshrouded-1-0-craftable-armor-sets-and-the-flame-level-that-unlocks-each-one/); [upgrading weapons](https://allthings.how/enshrouded-1-0-how-to-upgrade-weapons/); [gear level 55](https://www.theclick.gg/?p=91477)
- RuneScape: Dragonwilds: [how progression works](https://allthings.how/how-progression-works-in-runescape-dragonwilds-full-guide/); [gear guide](https://xgamingserver.com/blog/runescape-dragonwilds-gear-guide/)
- Path of Exile: [Increase (wiki)](https://pathofexile.gamepedia.com/Increase); [maxroll: damage scaling](https://maxroll.gg/poe2/getting-started/damage-scaling); [Maximum Resistances (wiki)](https://www.poe2wiki.net/wiki/Maximum_Resistances); [Resistance (wiki)](https://pathofexile.fandom.com/wiki/Resistance)
- Diablo: [Blizzard Watch: D3 PTR nerfs](https://blizzardwatch.com/2017/10/03/diablo-ptr-nerfs/); [Developer insights: class set design](https://www.icy-veins.com/forums/topic/48105-developer-insights-balancing-class-set-design/); [Blizzard: Season 4 Loot Reborn](https://news.blizzard.com/en-gb/article/24077223/galvanize-your-legend-in-season-4-loot-reborn); [PC Gamer: Season 4 changes](https://www.pcgamer.com/games/action/5-changes-diablo-4-season-4-rework)
- Monster Hunter World: [RPG Site: tempered monsters and the end game](https://www.rpgsite.net/feature/6775-monster-hunter-world-tempered-monsters-augmentation-decoration-farming-and-the-endgame-explained)
- Hades: [RPG Site: Pact of Punishment](https://rpgsite.net/feature/10287-hades-pact-of-punishment-heat-modifiers-and-how-to-maximize-your-rewards); [Heat (wiki)](https://hades.fandom.com/wiki/Heat)
- Deep Rock Galactic: [Hazard Level / difficulty scaling (wiki)](https://deeprockgalactic.wiki.gg/wiki/Hazard_Level)
- Palworld: [mein-mmo: fans find it too easy](https://mein-mmo.de/en/many-fans-find-palworld-too-easy-and-fast-revealing-their-attitude-towards-the-perfectly-balanced-gaming-experience,1577529/); [PalMods: the 1.0 end-game problem](https://www.palmods.gg/blog/palworld-1-0-endgame-problem)
