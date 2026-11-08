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
- **Techniques (levels 20 and 40):** one of the two perks teaches a new way of doing something instead of bonuses
  (`PerkDefinition` › Technique, `PerkTechnique`, numbers in `Resources/TechniqueSettings`). Which perk teaches which is one table in
  `AgentScripts/BuildProficiencies.cs` (`TechniquePerks`), so they can be reshuffled without code.

  | Skill | 20 | 40 |
  |---|---|---|
  | Woodcutting | Steady Rhythm: a normal swing keeps the combo | Heartwood Strike: a critical swing counts twice |
  | Mining | Rich Seam: one more ore per critical | Shatter: a critical counts twice |
  | Foraging | Sweep: picking by hand gathers the same plants within 3 m | Forager's Haste: +15% speed for 10 s after a plant |
  | Fishing | Quick Bite: fish bite twice as soon | Double Catch: 25% chance of two fish |
  | Weaponcrafting | Sure Hand: after four plain pieces the next is Fine | Runed Make: a Masterwork comes empowered once |
  | Armorcrafting | Sure Fit | Runed Armour (the same, for armour) |
  | Cooking | Third Plate: one more meal slot | Hearty Meals: a meal heals 25% of health |
  | Alchemy | Potent Remedies: draughts heal 30% more | Spare Dose: 25% chance a draught isn't used up |
  | Construction | Patch Up: breach repairs cost half | Raise in a Day: the hammer works twice as fast |
  | Melee | Riposte: a parry readies a counter | Second Wind: a weapon kill gives back 20 stamina |
  | Archery | Deadly Draw: a perfect shot hits 50% harder | Pinning Shot: a critical arrow roots |
  | Runecraft | Spell Echo: 15% chance of no cooldown | Soul Siphon: a spell kill heals 10% |
  | Sneaking | Silent Takedown: an ambush finishes an ordinary creature with at most 4x the blow's health | Shadowstep: sneaking a third faster |

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
| Fishing | Line control (strain and slack the line takes, how soon fish tire) | 80% | 100% | 130% |
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

The action rate is an assumption, not a measurement. The playtest log now writes each skill's practice experience every played
minute (`SkillXp` rows); Systems Editor › Settings › Skills turns real runs into experience per played hour per skill (median of
runs without cheats) and the hours to levels 10, 20 and 50 that rate comes to. Tighten the curve from those numbers, not from the
table above.

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

## Residents learn, and teach

A resident working through their job's orders gets better at it with every craft (`ResidentRecord.WorkDone`, saved): Novice,
Apprentice (10), Journeyman (30), Expert (70), Master (150) (`SettlementSettings` › Worker Ranks). Each rank makes their station work
10% faster, for the player's orders too, and gives a 3% chance more of one extra of what they make. The Haven window's People page
shows their rank.

Every resident with a job gives the player one lesson in their job's skill between raids ("Ask for a lesson in...", in conversation):
the tier experience of what they know (their rank, or their workplace's tier if higher) times 6, falling off like practice once the
player knows more than they can teach. Smith: Weaponcrafting; Weaver: Armorcrafting; Cook: Cooking; Mason: Construction; Carpenter:
Woodcutting; Farmer: Foraging; Hunter: Archery; Warden: Melee; Tracker: Sneaking; Rune Scholar: Runecraft; Fisher: Fishing. So Melee
and Sneaking, which have no building, have teachers.

## Crafting orders

Once the player's hands are up to it, residents order a piece of good make (a `HavenRequestDefinition` with Quality Fine or
Masterwork). It is asked only once the skill that makes the item has reached the order's From Level, only one order stands at a time
(the raids stay the main reason to go out), and it shows in the raid planner as "Ordered at home" and in the asker's words in passing.
Only a piece of at least that make answers; the plainest that will do is taken, so a Masterwork is kept while a Fine piece answers.
It pays coin, a lesson in the craft given in full (no fall-off: a level's worth of experience at the order's level, one and a half
for a Masterwork) and lasting renown (the Haven window's "Good work for its people", saved).

| Order | Asker | From |
|---|---|---|
| Fine Cured Leather Jack | Asa | Armorcrafting 10 |
| Fine Iron Hatchet, Fine Iron Pickaxe | Tor, Hild | Weaponcrafting 12 |
| Fine Iron Sword, Fine Hunter's Bow | Bjarne, Gunnar | 15 |
| Fine Iron Mail Hauberk | Eirik | Armorcrafting 20 |
| Masterwork Iron Spear, Masterwork Copper Scale Hauberk | Bjarne, Sigrid | 30 |

Content: `AgentScripts/BuildHavenRequests.cs`.

## The Dock and the Fisher

The Dock (12 oak planks, 6 oak wood, 4 nettle cord; upgrades to the Long Pier) is Fishing's mentor building and a station of its
own. Its catches: Set the Nets (1 nettle cord for 3 perch, 45 s), Trout Lines (2 grubs for 2 trout), and from the Long Pier Pike
Traps and Deep Lines for char. What it brings in is caught, not made (`CraftedGoods.Catch`): each catch trains Fishing at 0.75 of
a tier's experience (`LevelingSettings` › Dock Work), under the 2 of a fish landed by hand, and Double Catch works on the nets too.
The Fisher (Gunnar or Yrsa can take it) works the Dock with a rod, keeps nets, lines and traps out while the player is away (stock
20, 15 and 6) and gives lessons in Fishing. The model is generated from primitives for now. Content: `AgentScripts/BuildDock.cs`,
then `BuildProficiencies.cs`.

## Weapon flourishes

From Melee 30 (`TechniqueSettings` › Flourish Level) every Weapon Art gains the flourish of its weapon's kind: one-handed arts
(Flurry) strike 30% harder and a kill with one gives back half its Focus; two-handed arts (Sunder) wear down poise 2.5 times as hard,
so they stagger; polearm arts (Pin) root what they hit. Reaching 30 says so, and the skills window tells of it on Melee
(`Techniques.HasFlourish`, `PlayerCombat.Swings`, `PlayerTechniques`).

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

## How progress feels

- **A level** pops a banner high in the middle of the screen over a soft dark band: it springs in, its title flashes white,
  sparks fly, and a lyre strum climbs to a bowl (Level Up). **A milestone** is bigger in every way: more sparks, the screen's edges
  glow in the accent, and a frame drum, a swelling string and a full strum land on a bell (Milestone, which takes the mix). Both
  draw above any open window, as a craft at a station is where a crafting milestone lands.
- The XP bar eases to its new place, flashes and jumps on a level, starts again from empty; "+12 Woodcutting" pops; the "(N)" of a
  waiting perk breathes.
- In the skills window, waiting milestones breathe in the accent; a chosen perk punches, flashes and sparks to a knocked rune
  (Perk Chosen); Insight going in is heard; a refused choice shakes.
- **Crafting:** whatever is made rises as a card in the right-hand column (top of the screen while a window is open): its icon, its
  name in the colour of its make (Fine in the Rare colour, Masterwork in the Epic one), "x5" for a run, and the experience it
  taught. Good work has its own sound after the smith's taps (Craft Fine: a bowl; Craft Masterwork: a bell and a rising strum) and
  sparks. Queuing at a station shows as queued with a tally knocked into the bench; the recipe's icon punches; a make with nothing
  to make with shakes the button. At the station the progress bar warms towards the accent as the craft nears done, a craft that
  finishes while watching makes the output slot jump with the smith's taps, a full output breathes red.
- **Every button** lifts a little under the pointer, sinks while held and springs back (`UIPress`, added by `UIRole`).

- **Round two:** sparks are soft glowing motes, a few white-hot, drifting down as they fade; a milestone throws slow-turning rays of
  light, and at home out of a fight it slows the world to a third for a moment. Practice flies from the player into the XP bar as a
  mote that lands with a flash. A clean cut on a node floats what it brought ("+1 x3", Rich Seam's extra included) and kicks the
  camera a little more for each in a run. The craft card's icon sits on a light disc tinted by its make, and good work gets a shine
  across it. Hovering a skill shows its next milestone and both of what it offers. The first time a technique comes into play its
  name pops as a banner with what it does (remembered in the save). The portal's planner opens with what the stretch at home came
  to ("Since your last raid: Woodcutting 3 to 7, 2 Fine pieces made").

Code: `UIMotion` (punch, pop, shake, flash, fade, spark bursts, rays, flying motes, breathing; unscaled time), `UISprites` (vignette,
band, glow, ray and disc made in code), `ExperienceHud`, `CraftFeedback`, `SkillsWindow`, `RecipeBrowser`, `StationWindow`. Sounds: `Tools/Audio/progression.py`,
wrapped by `AgentScripts/BuildGameFeel.cs` into `AudioSettings` (Milestone, Perk Chosen, Insight, Craft Fine, Craft Masterwork,
Craft Queued) and `LevelingSettings` › Level Up Sound. The sounds are measured, not yet listened to by a person.

## Mentors and rest

Every skill but Melee and Sneaking has a mentor building at home (`ProficiencyDefinition` › Mentor): each tier it stands at
above I adds 5% to the skill's experience, on raids too (`SettlementState` remembers what stands). Woodcutter's Lodge: Woodcutting;
Smithy: Mining and Weaponcrafting; Farm Plot: Foraging; Tailor's Shop: Armorcrafting; Kitchen: Cooking; Cauldron: Alchemy; Workshop:
Construction; Hunter's Lodge: Archery; Purification Altar: Runecraft; Dock: Fishing. The skills window shows it.

Setting out rested from a comfortable Haven (`HavenSettings` › Rested Bonuses, as long as the comfort at home allows) now also gives
+20% experience, and the Sauna's warmth +10% (the Experience Gain stat). Beds still gate nothing.

## Still to watch

- The pace is worked out, not measured yet. The playtest log writes a `SkillLevel` row for every level (detail `insight` when
  Insight raised it), an `InsightSpent` row and per-minute `SkillXp` rows; Systems Editor › Settings › Skills puts the planned hours
  beside the median played minutes at which real runs (without cheats) reached levels 5 to 50, and the measured experience per hour.
- Crafting orders take a qualifying piece from the bag or stash as soon as it is home, like the raid asks. If players find a Fine
  piece they meant to keep taken, make handing in a choice in conversation instead.
- The raid planner shows the best fighting skill beside the gear score, against the level the tier expects (10, 20, 30, 40).
- The prologue at level 1 took a blow more per fight before the eased curve; with a few wolves down that is mostly gone. Playtest it.
- The Dock can be placed anywhere, not only at the water's edge, and its model is primitives.
