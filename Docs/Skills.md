# Skills

The player gets better at what they do. Felling trees trains Woodcutting, swinging a sword trains Melee, creeping past a wolf
trains Sneaking. Thirteen skills: Woodcutting, Mining, Foraging and Fishing (gathering); Weaponcrafting, Armorcrafting, Cooking,
Alchemy and Construction (crafting); Melee, Archery and Runecraft (combat); Sneaking (stealth). Skills replaced the character level and the four talent trees on 2026-11-07, modelled on RuneScape: Dragonwilds,
where every activity is a skill with small bonuses each level and something new at set levels.

Code: `Progression/` (`ProficiencyDefinition`, `PerkDefinition`, `CharacterProgress`, `ExperienceTracker`, `PlayerTraining`,
`PlayerProficiencies`, `LevelingSettings`), the window `UI/SkillsWindow` (N). Content: `AgentScripts/BuildProficiencies.cs`, assets in
`Data/Proficiencies`. In code a skill is a *proficiency*, because `SkillDefinition` is a spell; in the game spells are now called
abilities.

## What a skill gives

- **Every level:** a little more of a few stats (`ProficiencyDefinition` › Level Bonuses). Each is **0 at the baseline level (10)**,
  the player the game was tuned for. Below it the player is a beginner and worse than that; above it, better.
- **Every 10 levels (10 to 50):** a milestone with two perks; the player keeps one. The old talents live on as perks. The first choice
  can be made anywhere; changing it, or choosing all again, only at home beside the portal, for free.
- **Insight:** experience that isn't practice (quests, raids, first crafts, buildings, catches, new ground, rune books, experience stones,
  blows no skill trains yet). The player puts it into any skill from the skills window. An old save's character experience becomes
  Insight (`SaveMapper.LegacyExperience`); its talents are gone.

## Making the start weaker

The tuning the game had becomes level 10, reached around the first tier I raids. A new player starts below it:

| Skill | Stat | Level 1 | Level 10 | Level 50 |
|---|---|---|---|---|
| Woodcutting / Mining | Tool rises on the timing bar | 90% speed | 100% | 125% |
| Foraging | Picking by hand and the sickle | 85% speed | 100% | 130% |
| | Extra plant chance | 0 | 0 | +20% |
| Fishing | Catch zone on the reel | 80% | 100% | 130% |
| | Critical zone width | 70% | 100% | 140% |
| | Extra yield chance | 0 | 0 | +15% |
| Melee | Damage of every swing | 88% | 100% | 120% |
| | Stamina per swing | 125% | 100% | 80% |
| | Stamina per blocked blow | 125% | 100% | 75% |
| Archery | Arrow damage | 88% | 100% | 120% |
| | Draw speed | 85% | 100% | 125% |
| Runecraft | Spell damage | 88% | 100% | 120% |
| | Stamina per spell | 125% | 100% | 80% |
| Sneaking | Sneaking footsteps and lock noise | 130% | 100% | 60% |
| | Visibility while sneaking (time to be spotted) | 118% (85%) | 100% | 71% (140%) |
| | Lockpicking zone width | 80% | 100% | 130% |
| Weaponcrafting / Armorcrafting | Condition new gear starts in | 75% | 100% | 100% |
| | Chance of a better make (a quarter of it Masterwork) | 0 | 0 | 60% |
| Cooking / Alchemy | How long meals / tonics last | 80% | 100% | 150% |
| | Chance of one more portion / draught | 0 | 0 | +25% |
| Construction | Hammering speed | 80% | 100% | 150% |
| | Chance of one more plank, ingot or block | 0 | 0 | +20% |

The penalties fade eased, not evenly (`LevelingSettings` › Beginner Ease 2.5): two thirds of them are gone by level 4 and three
quarters by level 5, so the first half hour feels like quick growth and the beginner is soon only a little behind.

Movement, dodge and parry windows, invulnerability and attack cadence are never touched: a slow-feeling character is not a weak one.
Melee damage scales every swing before backstabs and counters, so their order (plain 19, mid-strike backstab 26, counter 44, staggered
backstab 50 on a Draug) keeps its proportions at every level.

The prologue's fights were tuned to the old player, now level 10. At level 1 they take one more blow (wolf and small troll 7 to 8 with
sword or club, 9 to 10 with the spear) and 25% more stamina per swing; companions fight alongside. Not yet playtested.

## Pace

Each level takes 1.105 times the one before, from 50 experience. An action is worth its tier's experience (10, 16, 25, 40, 60 for
tiers I to V: a node's tier, or the raid's tier for anything done there; I at home), 60% when one tier below the skill's band (levels
1-10 are tier I's band, 11-20 tier II's...) and 25% two or more below, and x1.5 on a raid. With about 180 actions an hour, a quarter of
them for one skill (`ProgressionTests.AuthoredCurve_...`):

| Level | Total experience | Normal mixed play |
|---|---|---|
| 10 (baseline) | ~700 | ~1.5 h |
| 20 | ~2,700 | ~4.5 h |
| 30 | ~8,100 | ~9.5 h |
| 50 | ~63,000 | ~33 h |

The action rate is an assumption, not a measurement: count real actions per raid from the playtest logs before tightening it.

## What trains what

| Skill | Practice | Where |
|---|---|---|
| Woodcutting | a node gathered with an axe, at its tier | `ExperienceTracker.OnHarvested` |
| Mining | a node gathered with a pickaxe | same |
| Foraging | a node picked by hand or cut with a sickle | same |
| Fishing | a fish landed, 2 x its tier's experience | `PlayerFishing.Land` |
| Archery | each arrow that hurts a monster, as Melee's blows | `PlayerTraining` (`HitTechnique.Shot`) |
| Runecraft | each spell that hurts a monster, as Melee's blows; every spell cast, 3 tier I experience | `PlayerTraining` (`HitTechnique.Spell`, `PlayerSkills.SkillCast`) |
| Melee | each weapon swing that hurts a monster: the share of the monster's worth (its Experience, or 0.25 per point of health) it took off its health; parries and perfect dodges, 3 tier I experience, five times per monster | `PlayerTraining` |
| Sneaking | first blow on an unaware monster (8), sneaking for 6 s within a monster's sight before it spots you (4, three times per monster), a monster that hunted you losing track (10), a lock pin set silently (2) | `PlayerTraining` |
| Weaponcrafting | a weapon (3), tool (2) or batch of arrows (0.5) made, in its item tier's experience | `ExperienceTracker.PractiseCraft` |
| Armorcrafting | armour, a shield or a pack (3) | same |
| Cooking | a meal (1) | same |
| Alchemy | a tonic, cure or remedy (1.5) | same |
| Construction | a building hammered up (4) or upgraded (6), in the tier experience of its level; a batch of planks, ingots, blocks or leather refined (0.5) | `OnConstructionFinished`, `PractiseCraft` |

Crafting counts what the player makes by hand and what they order at a station, when it finishes; workers' orders train nobody,
and the raid bonus never applies (an order that finishes while the player is away was made at home). Growing at the Farm Plot
and the Beehive trains no craft (`CraftedWork.Of(recipe)`).

## Better makes

Gear the player makes rolls its quality when it is made by hand, or when they collect it from a station: **Fine** or **Masterwork**
(`ItemStack.Quality`, saved and shared in co-op). A Fine piece is worth half a level of empowering at the altar (+10% of its stats,
+3% weapon damage, +5% gear score), a Masterwork a whole level (`LevelingSettings` › Fine and Masterwork Strength); it shows as
"Fine Iron Sword" and in green in the tooltip. Raid tiers are tuned to plain gear, so a well made kit is a head start, not a
requirement.

Blows of other kinds (traps) give Insight. The Ambush Damage of a perk counts for arrows as well as swings.

## Raid tiers

Raid tiers are tuned to gear kits (`GearBalance`, [GearScoreAndRaidTiers](GearScoreAndRaidTiers.md)). The kit's damage now includes the
Melee skill expected at the tier (`LevelingSettings.ExpectedLevel`: 10, 20, 30, 40, so +0%, +5%, +10%, +15%), and enemy health was raised
to the suggested values (II x1.5, III x2.3, IV x3). A player whose skill keeps pace finds each tier as gear alone used to.

## Raid lessons wait for the way home

On a raid, practice pays its usual experience at once and the raid's bonus (the extra half of x1.5) is held: it comes home with an
extraction and is lost on a death or when time runs out (`ExperienceTracker.SettleRaid`, called by `RaidDirector`). Nothing earned is
ever taken away; only the bonus is at risk, like the loot. The HUD shows "+N if you get out" beside the skill, and the raid summary
lists what each skill learned and whether the bonus came home.

## Insight

Insight goes into a skill in full below level 20 and at half its worth from there on (`LevelingSettings` › Insight Full Below,
Insight Share Above), so it catches a neglected skill up but practice always leads.

## Seeing progress

The bar along the bottom follows the skill trained last, with its next milestone ("perk at 20"); a level shows a banner high in the
middle of the screen for a few seconds, a milestone for longer, with the perk to choose.

## Playing together

A friend's practice within 20 m in the same place teaches the player a quarter of it (`CoopLessons`, the `SafeHaven.Lesson` message,
sent every two seconds per skill and passed on by the host). What is learned by watching is kept at once and never sent on, so it
can't echo between players. Not yet tried with two players.

## Specials, signatures and titles

- **Specials:** a few crafts need a skill as well as their station (`RecipeDefinition` › Required Skill): the tier V Trollstone set and
  shield (Armorcrafting 30), the Trollstone blade, maul and Trollhorn bow (Weaponcrafting 30), the Haven Feast (Cooking 25) and the
  Radiant Draught (Alchemy 25). The tier kits from copper to moonsilver never wait on a skill: raid tiers need them.
- **Signatures:** a Fine or Masterwork piece carries its maker's name ("Made by Astrid", saved and shared). The Trading Post buys
  them from the bag: a Fine piece for a tenth of its value in coin, a Masterwork for a quarter (`TradeSettings`).
- **Total level:** every skill's level added up gives a title (Newcomer, Settler, Woodsfolk, Haven-Keeper, Elder of the Haven,
  Legend of the Forest), shown in the skills window, and one point of Haven renown for every 20 levels above the start.

## Mentors and rest

Every skill but Fishing, Melee and Sneaking has a mentor building at home (`ProficiencyDefinition` › Mentor): each tier it stands at
above I adds 5% to the skill's experience, on raids too (`SettlementState` remembers what stands). Woodcutter's Lodge: Woodcutting;
Smithy: Mining and Weaponcrafting; Farm Plot: Foraging; Tailor's Shop: Armorcrafting; Kitchen: Cooking; Cauldron: Alchemy; Workshop:
Construction; Hunter's Lodge: Archery; Purification Altar: Runecraft. The skills window shows it.

Setting out rested from a comfortable Haven (`HavenSettings` › Rested Bonuses, as long as the comfort at home allows) now also gives
+20% experience, and the Sauna's warmth +10% (the Experience Gain stat). Beds still gate nothing.

## Still to watch

- The pace is worked out, not measured yet. The playtest log now writes a `SkillLevel` row for every level (detail `insight` when
  Insight raised it) and an `InsightSpent` row; Systems Editor › Settings › Skills puts the planned hours beside the median played
  minutes at which real runs (without cheats) reached levels 5 to 50.
- The raid planner shows the best fighting skill beside the gear score, against the level the tier expects (10, 20, 30, 40).
- The prologue at level 1 took a blow more per fight before the eased curve; with a few wolves down that is mostly gone. Playtest it.
- Fishing, Melee and Sneaking have no mentor yet (a dock, a training yard).
