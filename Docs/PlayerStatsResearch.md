# Player stats research: what the numbers do, whether the game needs more, and build choice without a new bar

Research date: 2026-11-15, after the melee rework (`Docs/CombatResearch.md`), the enemy work (`Docs/EnemyCombatResearch.md`) and the
ranged and stealth study (`Docs/RangedStealthResearch.md`). Read-only: no Unity, no CLI, no project edits apart from this file. Paths
are relative to `d:\Safe Haven\Safe Haven`, scripts under `Assets/_Project/Scripts/`, and `file:line` is the `test` branch at
`03a675af`. Combat files are being edited at the same time, so line numbers in `Player/PlayerCombat*` may drift.

**The question (the user):** "look at player stats and how this affects gameplay and combat. Right now it's very basic. Look for how we
can implement stats without overpowering the player or adding another player progression bar. Do we need player stats? What are the
benefits of having it, versus not having it? How could this smoothly integrate into the other systems?"

## Status (2026-11-15): built

The user: "do all suggestions." All nine recommendations were built the same day on `test`. They were checked by Edit Mode tests:
`RaidTierTests` (a fully stacked kit stays within a reach), `PlayerStatsTests` (new) and `ContentExpansionTests` (the dodge classes).
Not played by hand.

The choices made where the report left them open:

- **#1, counting every source.**
  - `GearBalance.StackedKitOf` adds to the tier's kit:
    - every stat on the gear;
    - the Melee perks, and the perks of the best crafting or gathering skill, taking the stronger side of each milestone up to the
      tier's expected level;
    - the best meals and tonics of the tier, as many as the slots hold;
    - Well Rested.
  - The raid tier window gains a "Fully stacked: kill / die" column.
  - **Empowering is left out.** It raises the gear score, so an empowered player already counts toward the next tier.
  - **The reach is toughness ×1.5 and damage ×1.35** against the plain kit (`RaidTierTests.StackedToughnessReach` and
    `StackedDamageReach`). The fully stacked kit now comes to:

    | Tier | Damage | Toughness |
    |---|---|---|
    | I | ×1.03 | ×1.21 |
    | II | ×1.30 | ×1.27 |
    | III | ×1.29 | ×1.28 |
    | IV | ×1.26 | ×1.44 |

    At the start it reached up to ×2.04 toughness. The changes that brought it inside the reach:
    - **The perks (#7).**
    - **Health meals give a share of health instead of flat health:**
      - Hunter's Stew: +25 → +10%.
      - Purified Draught: +40 → +10%.
      - Smoked Boar: +10 → +4%.
      - Haven Feast: +12% → +10%.
- **#2, the Fight card.**
  - The character window's Offense card is now **Fight**, with these rows:
    - **Damage:** one share of the weapon's damage, and the blow it makes.
    - **Blows:** blows to fell, and blows to take, against the tiers' reference creature at the tier ahead (the raid being fought in,
      or the one the gear is ready for).
    - **Stamina:** stamina as dodges or swings.
    - **Dodge:** the dodge class, the load, and how far it is to the next class.
    - **Staff and abilities:** spell power and spell critical chance.
  - `FightMath` is the one formula behind both the Fight card and `GearBalance`.
  - Item tooltips:
    - an arrow for each stat against the piece worn in that slot;
    - "(+X% damage)" beside Might;
    - the meal family beside a meal or tonic.
- **#3, the playtest log.** Two new kinds of row:
  - **Kit,** at the start and end of each raid: max health, armour, the damage share, toughness and active boons.
  - **Hurt,** for every blow that takes at least 5% of health: what struck, how hard, and with what.
- **#4, the tonic slot.**
  - One tonic at a time (Survival Settings › Tonic Slots); a new tonic pushes out the one closest to ending.
  - A second slot comes from the Cauldron's Master's Shelf (Building Level › Extra Tonic Slots) and from the perk **Second Vial**.
    Second Vial is the old Rune Steeping perk; its id is kept.
- **#5, honest stats.**
  - Strength and Physical Power both read as **Might**. Holy Resistance reads as **Blight Resistance**; the enum is unchanged.
  - Spell Power is marked "(staff and abilities)".
  - **The off hand already counted in the load.** §1.5 was wrong: `PlayerEquipment.EquipLoad` adds it after the loop.
  - **Spell Power is gone from seven melee pieces.** The report named five of them; it missed the Trollstone Helm and Gauntlets.
    - Moonsilver Circlet and Gloves, Trollstone Helm and Gauntlets: +8 Max Stamina.
    - Moonsilver and Trollstone Blades: +0.1 Physical Crit Damage.
    - Moonsilver Ring: +5% Move Speed.
    - The Wisp Pendant keeps its Spell Power.
- **#6, weight.**
  - A bow in hand counts at 40% of its weight in the load (`PlayerEquipment.BowLoadShare`). Half weight would still have made the
    Trollhorn Bow (2.6) roll in leather. Leather (4.9) with any bow is now a quickstep, and the gear noise follows the load.
  - Heavy Load is now 13.5 (it was 13). Copper Scale with a sword (13.0) rolls, and with a shield it is a heavy roll.
- **#7, the perks.** Gathering and crafting perks give no more fight numbers:
  - **Armour and weapon crafting** perks give finish and quality.
  - **Hearthkeeper:** build speed.
  - **Stone Skin:** refining yield.
  - **Strong Stomach:** meals last longer.
  - **Sea Legs, Wildheart and Iron Arms:** yields and tool care.
  - **Axe Fighter:** chop speed.

  The two combat perks became trade-offs:
  - **Reaver:** +10% melee damage, and 10% more stamina a swing.
  - **Warden:** +15 health and +5 armour, and 3% slower.
- **#8 and #9, meal families and harms.**
  - Meals and tonics are tagged Hearty, Swift, Fierce or Warding.
  - The raid planner reads the harms off the raid's data (`RaidHarms`): fire, frost and blight from the creatures that can be met at
    the tier, and the season's cold. It names them under the known dangers, and says that Warding food or the matching resistance
    answers them. A run of plain blows suggests Hearty or Fierce food.
  - **The rumour texts were not changed.** A rumour leads to a place, not to the creatures there, so the harm line comes from the run
    instead.
  - Misty Woods now reads: Frost (Draug) and Blight (Blight Boar, Bog Spitter).
  - Nothing deals fire yet. The planner will name fire once a creature does.

## 0. Summary

**The three biggest findings:**
1. **The game already has player stats: 59 of them, from nine sources, mostly hidden.**
   - `StatType` has 59 entries (`Combat/StatType.cs:5-123`).
   - They come from base values, gear, quality and empowering, enchantments and relics, 14 skills' level bonuses, 112 perks of plain
     numbers, meals, tonics and Well Rested.
   - The character sheet shows 13 of them.
   - Nothing shows stamina, move speed, the damage shares, Life Drain, or the equipment load. The load decides whether the dodge is a
     quickstep, a roll or a heavy roll.
   - The system underneath isn't basic; it is invisible.
2. **The balance model counts four of them, and the rest can break the tiers.**
   - `GearBalance` counts gear Armour, Health, Strength, Crit and the Melee level (`Editor/Raids/RaidTierEditor.cs:85-121`). Perks,
     meals, tonics, empowering, enchantments and Well Rested are left out.
   - Tonics have no slot limit: every different one stacks (`Survival/PlayerBoons.cs:77-91`).
   - A plausible tier III player is about **1.2 times as deadly and 1.7 times as tough** as the kit the tier was tuned to. Hits to die
     go from 12.9 to 21.7, far past the 20% band `RaidTierTests` guards (arithmetic, §1.6).
   - Nobody has met this yet: every logged raid was tier I at gear score 0-14, and no stat food was eaten.
3. **Several shown stats mislead or do nothing.**
   - Strength and Physical Power are one number shown twice (+3% physical damage a point, nothing else).
   - "Holy" resistance only stops Poison.
   - Spell Power on Moonsilver and Trollstone melee gear is dead weight for a sword user.
   - Leather is meant for moving and shooting, but leather with a bow rolls instead of quickstepping.
   - The shield's weight is never counted.

**Do we need player stats? No new attributes, points or levels.** The benefits (planning a build, comparing gear, understanding a death)
come from surfacing what exists:
- in blows and dodges rather than raw numbers;
- with a ceiling on stacking so the tiers hold;
- with builds from choices the game already offers: armour weight, meals, perks, runes, relics.

Comparable games are moving the same way: No Rest for the Wicked 1.0 drops its attributes (announced October 2026), Dragonwilds cut its
armour-driven health layer, and Diablo IV cut its affixes (§2).

**Co-op:** each player's own game works out their blows and the blows they take, so stats never cross the wire, and readouts and caps
need no sync. Creatures gain health per friend (`Multiplayer/CreatureHost.cs:241`), which assumes friends of similar power. Stacking
with no ceiling breaks that.

**Ranked recommendations** (detail in §3.3):

| # | Recommendation | Impact | Effort | Changes balance? |
|---|---|---|---|---|
| 1 | Count every source: `GearBalance` and `RaidTierTests` learn perks, meals, tonics, empowering and Well Rested, with a "full stack" column and test | High (guards the tiers) | S-M | No (it measures) |
| 2 | A **Fight** card on the character window in blows and dodges (for example "fells in 5 blows, falls in 13"), and tooltips that compare stat by stat | High | S-M | No |
| 3 | Kit and Hurt rows in the playtest log, at raid entry and end | High (enables tuning) | S | No |
| 4 | One tonic at a time: a tonic slot like the meal slots | High | S | Yes, down from today's worst case; sign-off |
| 5 | Make every shown stat true: one Might row, Holy shown as Blight, spell power marked for the staff, the shield in the load | Med-High | S | Labels no; the rest slight, sign-off |
| 6 | Weight as the build stat: show the dodge class and load, keep leather and a bow light | High | S-M | Slight, sign-off |
| 7 | Perks as choices, not stat sticks: no fight numbers from gathering and crafting skills; combat pairs as trade-offs or verbs | Med-High | M | Yes (down), sign-off |
| 8 | Meals as the per-raid build: three named families, suggested by the raid planner | Med | S (data, text) | Slight |
| 9 | Resistances that answer places: rumours name the harm, so a resist piece is a packing choice | Med | S-M | No |

Not recommended: attributes with points, a level, or derived attributes computed from skills (§3.2).

## 1. How it works today

### 1.1 One sheet, one formula

- A combatant's `CombatStatsDefinition` is copied into a `StatSheet`, and modifiers carry their source so gear, a meal or a perk can be
  taken off again. A stat is (base + flat) × (1 + sum of percents) (`Combat/StatSheet.cs:53`).
- The player starts with 100 health, 5% physical and spell crit and +50% crit damage (`Data/Combat/Stats/PlayerStats.asset`).
  Stamina is 100, refilling at 26 a second; a swing costs 12 and a dodge 25 (`Data/Player/PlayerStaminaSettings.asset`).
- **Outgoing damage** (`Combat/DamageCalculator.cs:49-66`): (base + weapon × ratio) × (1 + power × 0.03) × (1 + Damage Bonus), × (1 +
  crit damage) on a crit. Power is Physical Power plus Strength for physical blows, Spell Power otherwise (`Combat/CombatRules.cs:44-48`).
- Then the player's own shares apply: Melee Damage on swings, before backstabs and counters so their order holds
  (`Player/PlayerCombat.Swings.cs:587`); Arrow Damage (`PlayerArchery.cs:436`); Spell Damage on abilities only (`Skills/PlayerSkills.cs:153`).
- **Incoming damage** (`DamageCalculator.cs:74-94`): armour ÷ (armour + 100) off physical blows, then the type's resistance, then Blunt,
  Slash or Stab, each capped at 90%. A blow always does at least 1.

### 1.2 Where each fight stat is read

| Stat | Read at | What it does in play |
|---|---|---|
| Max Health | `Combat/Damageable.cs:38` | Health bar length |
| Strength, Physical Power | `CombatRules.cs:47` | +3% physical damage per point, arrows at 0.8. Nothing else: no carrying, no poise |
| Spell Power, Spell Crit | `DamageType.cs:28-38` | Frost and fire blows: staff bolts (`MonsterProjectile` builds with the player's stats, `AI/MonsterProjectile.cs:74`) and abilities |
| Crit chance and damage | `DamageCalculator.cs:59-64` | Random crits; counters, staggered backstabs and perfect releases are sure crits (`:21`) |
| Armour, resistances | `DamageCalculator.cs:84-90` | See §1.1. Frost Resistance also slows the winter chill (`Survival/PlayerWarmth.cs:103`) |
| Damage Bonus | `DamageCalculator.cs:52` | Empowered weapons, two bows, meals, enchantments; also how raids and night scale creatures |
| Max Stamina, Move Speed, Draw Speed | `Player/PlayerStamina.cs:144`, `PlayerMovement.cs:367`, `PlayerArchery.cs:121` | Bar length, speed, bow draw time |
| Stamina costs, Life Drain, Structure Damage | `PlayerCombat.Swings.cs:284`, `PlayerGuard.cs:186`, `Combatant.cs:72`, `DamageCalculator.cs:56` | Skills and a few perks and enchantments only |
| Equipment load (not a stat) | `PlayerEquipment.cs:49-61`, `PlayerMovementSettings.cs:52-56, 76` | Up to 6, a quickstep: 0.6 stamina, 0.8 cooldown, 105% move speed. From 13, a heavy roll that barges: 1.4 stamina, 1.25 cooldown, 90% move speed. Between them, a roll |

No player item gives Blunt, Slash or Stab resistance.

### 1.3 What changes the player, and how far it can stack

| Source | Fight stats it gives | Limit | Counted by `GearBalance`? | Seen where |
|---|---|---|---|---|
| Gear (`Items/GearSet.cs:207-228`) | Armour, health, Strength, crit, resistances, stamina, speed, draw, spell power | 7 armour slots, 1 ring, weapon, off hand | Armour, health, Strength, crit only | Tooltip "+N" lines; the sheet |
| Quality and empowering (`Essence/GearPower.cs`) | +20% of a piece's positive stats per level (3 levels), +6% weapon damage per level (`Resources/EssenceSettings.asset:23-24`); Fine = ½ level, Masterwork = 1 | 3 levels | No | Tooltip, gear score |
| Enchantments (22, `Data/Essence/Enchantments`) | 14 stat ones, several with a cost (Bloodletting +15% damage, -15 health; Gravebound +16 armour, -6% speed), and 8 relics that change play (Wolf-Fed, Twice-Buried, Harefoot...) | One per piece | No | Tooltip |
| Skill level bonuses (14 skills, `Progression/PlayerProficiencies.cs`) | Melee, Archery and Runecraft each -12% to +20% damage, stamina costs, draw speed, sneak | Level 50 | Melee damage at the expected level (10/20/30/40) | Skills window "Now / At 50" |
| Perks (140: 112 numbers, 28 techniques) | See below | One of two per milestone, 5 milestones a skill | No | Skills window |
| Meals (`Survival/PlayerBoons.cs:131-160`) | Strength, health, armour, stamina, speed, Damage Bonus, frost | 2 slots (`Resources/SurvivalSettings.asset:27`), +1 Kitchen III, +1 Third Plate | No | Conditions list over the vitals |
| Tonics (8 with bonuses, `PlayerEating.cs:164`) | Barkskin +14 armour, Ironbark +32 armour and 5% physical, Purified +40 health, Cursed Draught +20% damage and -8 armour, Swiftfoot +12% speed... | **None: every different tonic stacks** | No | Conditions list |
| Well Rested (`Docs/BaseProgression.md` › Endurance) | +15% stamina, +10% health, 60 s per comfort point | Comfort | No | Conditions list |

**Perks are where the hidden power sits.**
- The old talents became perks, so most are small numbers (`Data/Proficiencies/Perks`).
- 27 of the 90 perks in the nine gathering and crafting skills give fight numbers. Some examples:
  - Mining 50 Iron Arms and Weaponcrafting 40 Hammer Arm: +3 Strength each, so +9% damage each. Melee 50's Reaver gives +10%.
  - Health: Fishing 50 Sea Legs +15, Foraging 50 Wildheart +20, Cooking 40 Strong Stomach +15.
  - Armour: Construction 50 Stone Skin +8, Armorcrafting 50 Second Skin +10 armour and +10 health.
- Taking every health perk on offer adds 120 health, nearly the 140 a whole tier IV kit adds. Armour perks add 37, and Strength perks add
  18% damage, more than the Melee skill's +15% at the level expected for tier IV.

**Techniques are the good kind.** Each of the 28 teaches a verb: Riposte, Second Wind, Pinning Shot, Silent Takedown, Third Plate.
Art runes give a weapon kind a second art (`Progression/ArtRunes.cs`). Relics change how the wearer plays (`Essence/RelicEffect.cs`).
These are build choices that are not numbers, and they already exist.

### 1.4 What the player can see

- **Character window (C)** (`UI/CharacterWindow.Sheet.cs:74-101, 118-155`): gear score; Health, Strength, Armour (with "% less");
  Physical Power, Spell Power, Physical and Spell Crit, weapon damage; four resistances; tool tiers.
  - That is 13 stat types. "Physical power" is Strength plus Physical Power, so it shows the same number twice for almost every player.
- **Item tooltip** (`UI/ItemTooltipView.cs:428-480`): "+2 Strength" lines, and an arrow up or down by **gear score only**.
  - It never says what a stat does or how this piece compares stat by stat.
  - A ring of +8 armour and a ring of +2 Strength compare by score, not by what they do.
- **Skills window (N):** each skill's bonuses now and at 50, and each perk's numbers.
- **HUD:** health, stamina and Focus bars, condition icons (`UI/PlayerConditionsView.cs`), and the experience bar along the bottom
  (`UI/ExperienceHud.cs`). That bar is the game's one progression bar.
- **Nowhere:** total damage share (gear + skill + perks + meals), stamina as a number, move speed, equipment load and dodge class,
  Life Drain, Ambush Damage, sneak visibility.

### 1.5 Stats that mislead or do nothing

- **Strength:** shown with a weight icon, which hints at carrying, but it is only power. A tier I Roast Boar (+3) is +9% damage, a third
  of a weapon tier (Worn Sword 12 to Copper Sword 15 is +25%).
- **Holy resistance:** the only Holy source is Poison (`Data/Combat/Status/Poison.asset:22`, damage type 3). Two perks, two hauberks
  and two draughts give it under a sun icon.
- **Fire resistance:** only undead knights' fire, burning and fire hazards (`Combat/Burning.cs:94`, `GroundHazard.cs:234`).
- **Spell Power on melee gear:** Moonsilver Circlet and Gloves +6 each, Moonsilver and Trollstone Blades +8, Moonsilver Ring +10. All of
  it is dead weight for a sword user. Runecraft's Spell Damage misses staff bolts while Spell Power hits them (`RangedStealthResearch.md` §1.2).
- **Equipment load** (`PlayerEquipment.cs:54`) sums Head to MainHand. OffHand is slot 17, so a 6-weight Iron Kite Shield adds nothing.
  - The leather set is 4.9; the test checks it without a weapon (`Editor/Tests/ContentExpansionTests.cs:398-403`). With a bow (2.2-2.6)
    it is 7.1-7.5, a roll. Only a dagger, club or fire-hardened spear keeps it a quickstep.
  - The Copper Scale set (10.0) plus a copper sword (3) lands exactly on 13: a heavy roll.
- **Courage** (named in the brief) does not exist in code.

### 1.6 The balance model and what it leaves out

`GearBalance` takes the best one-hander, shield, six armour slots and one ring of a tier and works out damage, armour and health. It
compares them to a reference creature (60 health, 5 armour, hits for 15) at the tier's multipliers. `RaidTierTests` requires own-kit
hits to stay within 20% of tier I's (`Editor/Tests/RaidTierTests.cs:136-155`).

| Tier | Damage | Armour | Health | Own kit: hits to kill / die |
|---|---|---|---|---|
| I | 12.8 | 32 | 150 | 4.9 / 13.2 |
| II | 18.8 | 67 | 160 | 5.0 / 13.2 |
| III | 29.3 | 118 | 230 | 4.9 / 12.9 |
| IV | 37.6 | 184 | 240 | 5.0 / 13.4 |

**Stack example, tier III** (arithmetic with the same formulas; the choice of extras is mine).
- Extras, all of them reachable by then:
  - Perks: Melee 10 Thick Skin (+15 health) and 20 Shield Arm (+8 armour); Armorcrafting 10 Padded Lining (+10) and 20 Fitted Plates
    (+6 armour); Weaponcrafting 20 Balanced Blades (+3% crit) and 30 Know Your Steel (+4% melee).
  - Empowering: one level on the hauberk and the sword.
  - Two meals: Boar Pie (+8% health) and Trout in Cream (+8% damage).
  - Two tonics: Barkskin (+14 armour) and Purified Draught (+40 health).
- Results:
  - Health goes from 230 to 329 and armour from 118 to 157, so toughness is ×1.69 and **hits to die go from 12.9 to 21.7**.
  - Damage goes from 29.3 to 35.1, ×1.20, so **hits to kill go from 4.9 to 4.1**.
  - Add Cursed Draught and damage is ×1.41, so 3.5 hits.
  - Well Rested would add another 10% health on top.
- Toughness is the bigger leak, because armour and health come from everywhere.

### 1.7 Real play

The playtest CSVs (`%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests`, 1,222 files, most of them probes):
- All 29 `RaidTier` rows are tier I, at gear score 0 (24 rows), 8 (3) or 14 (2).
- The two longest recent cheat-free runs (2026-11-14, 63 and 67 played minutes) reached Melee 12 and 5. What was eaten was bandages,
  healing draughts and stewed berries: no boon meal or tonic.
- The longest old run (90 minutes) died three times, all to wolves.

So stat stacking has never been played, and new players live below the skill baseline (level 10), where penalties apply. The log has no
row for health, armour, boons or perks at raid entry, so the arithmetic above can't yet be checked against play.

### 1.8 Co-op

A friend's game works out its own blows and sends them as `CreatureHit` (`Multiplayer/CoopCreatures.cs`). Creature strikes land on a
friend in the friend's own game (`FriendHitbox.cs`), against the friend's stats. Health goes out as a byte (`PlayerLookState.cs:74`).
- Stats need no sync, and the host trusts a friend's numbers (fine for friends-only play).
- Party scaling adds creature health per friend (`CreatureHost.cs:241`). That assumes friends of similar power; stacking with no ceiling
  breaks it.

## 2. What other games do (web research)

Facts from official or press sources unless marked. The searches ran on 2026-11-15.

| Game | What it does with stats | Takeaway |
|---|---|---|
| **No Rest for the Wicked** | Early access: 8 attributes and an equip load that picks quickstep, roll or heavy roll ([Fextralife](https://norestforthewicked.wiki.fextralife.com/Stats), fan wiki). **1.0 drops attributes**: "attributes are gone entirely"; the weapon sets the class (24); six trait slots take traits from any class; health and stamina come from swappable cores with a capacity, and "every point over makes you take 10% more damage". The reason: progression is "often just about making some numbers go up" ([Wccftech, 2026-10-16](https://wccftech.com/no-rest-for-the-wicked-1-0-scraps-attributes-24-classes-stealth-swappable-serum-cores/); not shipped yet) | The game the combat rework copied is dropping attributes for weapon identity and a budget |
| **Valheim** | No attributes. Health and stamina come from three foods at once, split into health, stamina and balanced kinds; the first split was rebalanced within days ([PC Gamer](https://www.pcgamer.com/valheims-food-rebalance-has-already-been-rebalanced)). Skills by use, a share lost on death (unverified, community) | Food as the per-trip build. Keep the all-round meal good |
| **RuneScape: Dragonwilds** | Skills by use, no attributes. June 2026: Wardshield removed because it "placed too much weight on the armour you were wearing"; health now grows from shrines found exploring ([Jagex](https://dragonwilds.runescape.com/news/health-and-wardstones)) | The game Safe Haven's skills follow took a layer away |
| **Diablo IV** | Season 4 cut affixes and removed conditional "+% damage to X" so upgrades read at a glance ([Blizzard](https://news.blizzard.com/en-us/article/24077223/4)); 2.0 cut the level cap from 100 to 60 ([Kotaku](https://kotaku.com/diablo-4-big-changes-update-2-0-ptr-patch-notes-dlc-pc-1851636919)); 2.5 added one **Toughness** number with a recommended value per tier ([Blizzard Watch](https://blizzardwatch.com/2025/10/22/diablo-4-patch-2-5-0-changes/)) | One survival number per difficulty tier |
| **Path of Exile 2** | Attributes gate armour bases; reviewers call the tree "a star map of confusion" ([OpenCritic](https://opencritic.com/overslept/review/17827/path-of-exile-2)) | Gates turn into a points tax |
| **Elden Ring / Dark Souls** | Soft caps (Strength 18/50/60/80, [Fextralife](https://eldenring.wiki.fextralife.com/stats)); scaling letters hide the numbers; equip load bands 30/70/100% set the roll ([wiki.gg](https://eldenring.wiki.gg/wiki/Equipment_Load)) | Soft caps fix what free points cause. Weight is a build axis you feel |
| **Hades / Hades II** | No character stats; Mirror of Night pairs, one side at a time ([Fextralife](https://hades.wiki.fextralife.com/Mirror+of+the+Night)); Hades II Arcana spend a Grasp budget (numbers unverified, community) | Sidegrades or a budget; run power resets |
| **Monster Hunter** | No character stats; named, levelled gear skills and meals that last a hunt ([MH wiki](https://www.monsterhunterwiki.org/wiki/MHWilds/Skills)) | Power you can name, on the gear |
| **Zelda BotW / TotK** | Hearts and stamina only; one meal effect at a time ([Fextralife](https://zeldatearsofthekingdom.wiki.fextralife.com/Bonuses+and+Effects)); set bonuses are verbs such as no fall damage ([Zelda Wiki](https://www.zeldawiki.wiki/wiki/Set_Bonus)) | Buffs that answer a place |
| **Deep Rock Galactic** | No stats; a few perk slots; overclocks labelled Clean, Balanced or Unstable, each with its stated cost ([wiki.gg](https://deeprockgalactic.wiki.gg/wiki/Overclocks)) | A labelled trade-off makes power a choice |
| **Grim Dawn / Last Epoch** | Points per level; players settle on "enough for your gear, the rest in Physique" ([guide](https://www.grimdawn.com/guide/character/character-basics/); the rule is community, unverified); Last Epoch players call Dexterity not worth it ([forum](https://forum.lastepoch.com/t/current-state-of-attributes/17422)) | Free points collapse into one stat |
| **Designer writing** | Josh Sawyer, GDC 2016 "Gods and Dumps": shrink the gap between viable and optimal builds ([GDC Vault](https://gdcvault.com/play/1023481/Gods-and-Dumps-Attribute-Tuning)). D&D 5e bounded accuracy caps each source's bonus ([summary](https://dungeonsdragons.fandom.com/wiki/Bounded_accuracy); the original article is offline). Sea of Thieves: "a cutlass is always a cutlass" keeps mixed groups level ([Digital Trends](https://www.digitaltrends.com/gaming/sea-of-thieves-return-to-form-for-rare-studi/)) | Bound each source; keep co-op gaps small |

**Takeaways:**
1. **Don't add attributes.** The genre is removing them, and a level reads as power, which fights a game tuned on hits.
2. **Show survival against the tier,** as Diablo IV's Toughness does. Saying it in blows is clearer still.
3. **Cap each source** (one effect, a few food slots), so the worst stack stays inside a tested window.
4. **Make choices sidegrades with the cost on the label.** Enchantments such as Bloodletting and Gravebound already do this.
5. **Let weight and food carry the build.** Weight changes the dodge, and food resets each outing.

## 3. Recommendations

### 3.1 Visible stats: benefits against costs

| Benefit of visible stats | Its cost | How to get the benefit without the cost |
|---|---|---|
| Plan a build, compare gear | Spreadsheet play; "+2 Strength" means nothing | Compare stat by stat, each said as an effect ("+6% damage", "a heavier dodge") |
| Understand a death or a slow kill | A sheet of 59 numbers | One card in blows and dodges against the tier's creatures |
| Feel growth | Another bar; creep against the tiers | Skills (with their bar), gear tiers and techniques already do this |
| Build identity | False choices: everyone dumps into one stat | Trade-offs: weight class, meal family, perk pair, rune, relic |
| Co-op | Gaps between friends | Bounded stacking |

Hidden stats have a cost of their own: untested power, as the perks and tonics show. Stats should be **visible on demand** (window,
tooltip), **never on the HUD**, and **counted by the balance model**. The GDD already sets this bar for a new meter: "Test whether simple
health costs and debuffs create enough risk before adding another player-facing system" (`Docs/GameDesignDocument.md:899`).

### 3.2 The options

| Option | Depth and build choice | Creep risk | New bar? | UI cost | Effort | Verdict |
|---|---|---|---|---|---|---|
| A. No new stats: surface and count what exists | Low on its own | Lowest once counted | No | One card, tooltips | S-M | **Yes, the base** |
| B. Stats only on gear and food, curated and capped | Medium | Low with slots | No | Tooltip tags | S | **Yes** |
| C. Derived attributes fed by skills ("Might" from Melee and Mining) | None: no choice, it repeats the skills' bonuses | Medium | Reads like one | A panel | M | No; rollups for display only (#2) |
| D. Traits with a capacity budget (NRftW 1.0, Hades Grasp) | High | Low if the budget is fixed | No | A window | L | Not now: techniques, runes and relics fill the role. Revisit if builds converge |
| E. Attributes with points per level | Looks high, collapses to one stat | High | **Yes** | High | L | Rejected: it needs a level |

### 3.3 Details

| # | Where in the code | UI cost | Co-op |
|---|---|---|---|
| 1 Count every source | `GearBalance.KitOf`/`Measure` (`Editor/Raids/RaidTierEditor.cs:41-121`); a new `RaidTierTests` case | A third column in Raids › Rules › What it comes to (editor only) | None |
| 2 Fight card | `CharacterWindow.Sheet.cs` (a `HearthKit.Card` of `KitStatRow`s in place of the doubled power row); the kit formula moved from `GearBalance` to runtime code; `ItemTooltipView.Compared` and `StatNames` | One card; tooltip lines. Nothing on the HUD | Each player's own; no sync |
| 3 Kit and Hurt rows | `Playtest/PlaytestLogger.cs:146`, beside `RaidTier` | None | Each game logs its own player |
| 4 Tonic slot | `PlayerBoons.MakeRoomForMeal` (`:131-160`) copied for tonics; `SurvivalSettings` › Tonic Slots | The message meals already post | Local; boons already travel in `PlayerSnapshot` |
| 5 Stats made true | `StatNames.Of` (`Items/GearSlot.cs:247-310`), the sheet, `PlayerEquipment.EquipLoad` (`:54`), Moonsilver and Trollstone item assets | Labels and icons | None |
| 6 Weight as the build stat | `PlayerMovementSettings` › Light/Heavy Load, item weights, the Fight card | One row; a tooltip line | The dodge already plays through the pose stream |
| 7 Perks as choices | `Data/Proficiencies/Perks` assets, edited directly (targeted edits; perk ids kept, so saves keep their choices) | Perk text | None |
| 8 Meal families | Food item descriptions; the raid planner's rumour line | A tag on the tooltip | None |
| 9 Resistances by place | `RaidRumourDefinition` text, region pages | Rumour text | None |

**#1.** The "everything stacked" kit adds:
- the stronger fight perk at each milestone reachable at `ExpectedLevel(tier)`, in Melee and one crafting skill;
- the tier's two best meals and best tonics;
- +1 empowering;
- Well Rested.

Test it with `AFullyStackedKit_StaysWithinTheTiersReach`. The band is the user's choice, for example ×1.35 toughness and ×1.25 damage.
Count the bows' Damage Bonus too, with `RangedStealthResearch.md` #5. Expect the test to fail today: that failure is the case for #4 and #7.

**#2.** The card gives:
- blows to fell and blows you can take against the next raid's tier;
- damage as one share, with its sources on hover;
- stamina as "4 dodges or 8 swings";
- the dodge class with its load ("Roll, load 8.6: quickstep at 6").

The tooltip says each stat as an effect ("+3 Strength (+9% damage)") with an arrow per stat beside the gear-score arrow.

**#4.** One tonic at a time. A second slot comes from an Alchemy milestone or Cauldron III, mirroring Third Plate and Kitchen III, and a
new tonic pushes out the one nearest its end. Boons aren't saved, so nothing migrates. Setting Tonic Slots high restores today's
behaviour.

**#5.** These change labels only:
- one Might row;
- "Holy" shown as "Blight Resistance" with a drop icon (the enum stays);
- "(staff and abilities)" on spell power.

These need sign-off:
- **The off hand in the load.** Moonsilver with an Iron Kite Shield comes to 14.6, a heavy roll.
- **Spell Power on Moonsilver and Trollstone melee pieces,** swapped for stats a sword user gets something from. Keep it on the Wisp
  Pendant and staff gear.

**#6.** Quickstep against heavy roll is already a real trade: 0.6 against 1.4 stamina a dodge, and 105% against 90% move speed. Only the
feet show it. Two fixes:
- **Leather with a bow.** Leather is the archer's set (+10% draw, +5% speed), but it rolls with a bow. Count a bow at half weight, or
  raise Light Load to 8.
- **Copper Scale with a sword** sits exactly on 13. Decide which side it belongs.

Light then means quickstep and sneaking, and heavy means barging and armour. Weapon damage is untouched, so the families' 6% band holds.

**#7.** Gathering and crafting perks give verbs or help their own craft rather than fight numbers: Iron Arms carries more ore, Sea Legs
swims faster. Combat pairs offer one side as a labelled trade-off, Deep Rock Galactic-style; for example, Reaver gives +10% damage and
costs +10% swing stamina. Shares multiply every family alike, so the 6% band holds. Crit damage perks favour sure crits (counters, the
bow's perfect release), which `RangedStealthResearch.md` is retuning.

**#8.** The tooltip tags meals Hearty (health, armour), Swift (stamina, speed) or Fierce (damage at an armour cost, like Honey Mead).
The planner suggests one per rumour. Keep the all-round meal good: that was Valheim's lesson.

**#9.** Rumours name the harm (fire knights, bog poison, winter cold), so a resistance piece or Fish Soup becomes a packing choice.

### 3.4 Phased plan

| Phase | Items | Balance | Done when |
|---|---|---|---|
| A. Measure and read | #1, #3, #2, the label parts of #5 | None | The stacked column exists; the Fight card matches `GearBalance` within 5% for a tier kit; Kit rows in a playtest |
| B. Identity | #6 (display), #8, #9 | Slight | Load class visible; meal families tagged; rumours name harms |
| C. For sign-off | #4, #6 thresholds, the sign-off parts of #5, #7 | Yes | The user picks the stacked band; `RaidTierTests` passes with it |

### 3.5 How to verify

- **Edit Mode tests** through `RunTests.ps1`:
  - the stacked-kit test;
  - a tonic past the slot replaces one;
  - the Fight card's arithmetic in a plain C# class;
  - `HeavyArmourMakesAHeavyDodge...` with a weapon in hand.
- **`RaidTierTests` and the Rules tab** after any perk, food or weight change. Change gear first, then read off the multipliers.
- **Playtest:** Kit and Hurt rows over a tier I-II run, against §1.6.
- **Co-op:** one check that a friend's card and tonic slot work in their own game.

### 3.6 Constraints honoured and overlaps

- **No bar, level or XP track.** The experience bar stays the only one.
- **Never remove functionality.** Every change is a label, a readout or data. Slots and thresholds keep today's values until signed off,
  and perk ids are kept.
- **Small HUD.** Everything new lives in the character window and tooltips.
- **Weapon families.** No flat per-hit damage, which would favour fast families; damage stats stay shares.
- **Overlaps.** `RangedStealthResearch.md` #5 (bows in `GearBalance`) and #8 (the staff and Spell Damage) fit #1 and #5.

## 4. Unsure or unverified

- **The tier III stack (§1.6) is arithmetic.** The choice of extras is mine, and the model ignores attack speed, timing and creature
  abilities.
- **Nobody has been seen stacking tonics or meals:** no logged run ate a boon item. Some playtest CSVs are agent probes, so the counts
  in §1.7 are rough.
- **The leather-with-a-bow and Copper-Scale-with-a-sword loads are sums of asset weights,** not seen in play. I did not find the Iron
  Sword's weight.
- **Excluding the off hand from `EquipLoad` may be on purpose:** the loop stops at MainHand.
- **Holy resistance may be kept for future holy damage.** If so, rename later rather than now.
- **A friend's damage is trusted by the host;** I read the summaries in `CoopCreatures`, not every message.
- **Web sources:**
  - No Rest for the Wicked 1.0 is announced, not released, and could change.
  - Valheim's skill loss, Hades II's Grasp numbers and the Grim Dawn and Last Epoch player claims are community sources.
  - The Dragonwilds page header says 2026-06-10, but its metadata says 2026-11-10.
