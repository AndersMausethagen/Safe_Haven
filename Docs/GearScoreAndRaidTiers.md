# Gear score and raid tiers

How much better gear makes you, what each raid difficulty expects, and how to tune one against the other. The numbers live in assets
and are set by `AgentScripts/BuildRaidTiers.cs`. Designers edit them in Content Editor › Raids › Rules (the tiers), Enemies / Resources /
World (what appears at each tier) and Systems Editor › Gear Score (the score itself). Rule: **a tier's numbers come from the gear that exists,
not the other way round.**

## What we took from other games

- **V Rising:** gear level is the sum of what is worn, and a weapon counts for far more than an armor piece (up to 30 against 9). An empty
  slot adds nothing. It also punishes being under level directly: about 4% less damage dealt and 4% more taken per level below, against
  about 1% the other way (fan wiki figures, not from the developers).
- **World of Warcraft:** difficulty tracks start a fixed distance apart (13 item levels), and the top of one track reaches the start of the
  next. So the last upgrades of one tier carry a player into the next, which is what our +1 to +3 empowering does.
- **Anthem:** a cautionary tale. Its power score averaged over gear in a way that made leaving a slot empty the better choice. Summing
  slots can't go wrong that way, and a two-handed weapon takes the off hand's share so that it isn't behind a sword and shield.
- **What we chose not to copy:** V Rising's hidden damage modifier for being under level. Here the score only reads what gear is, and tiers
  change the creatures' real health and damage, as WoW does. Otherwise a player would be punished twice for the same gap (worse stats and
  a penalty on top), and a designer would have two knobs to balance instead of one.

## The score

`GearScore` adds up what is worn. One piece is worth:

    Points Per Tier (20) x tier x slot's share x rarity factor x (1 + 0.1 per empower level + 0.1 if enchanted)

| Slot | Share | Tier I | Tier III |
|---|---|---|---|
| One-handed weapon | 30% | 6 | 18 |
| Two-handed weapon (takes the off hand's share too) | 38% | 8 | 23 |
| Chest | 20% | 4 | 12 |
| Shield | 8% | 2 | 5 |
| Head | 8% | 2 | 5 |
| Feet | 7% | 1 | 4 |
| Hands, neck | 6% each | 1 | 4 |
| Waist, ring | 5% each | 1 | 3 |

One ring is worn: the second finger is retired (`GearSlots.IsRetired`). The shares were set for two, so a full kit comes to 95% of
Points Per Tier x tier, and the kit a tier is measured against (`GearBalance.KitOf`) wears one.

Rarity adds 5% (Uncommon), 10% (Rare), 20% (Epic), 30% (Legendary). Broken gear counts for nothing, as it gives no stats. Backpacks and tools
count for nothing either. An item's **Gear Score Override** replaces the rules: the torch and the fishing rod count for 1, as nobody's power
comes from them.

## The tiers

A full kit is worth about 20 points per tier. A tier recommends about 10 less than its own full kit, the same step WoW's tracks keep. The floor
("At least") is a little under the tier before's kit: it stops a player stripping down to run for loot they couldn't get otherwise, and
leaves room to go in under-geared on purpose. Tier I has no floor, as nobody can be kept out of their first raid.

| Raid tier | Recommended | At least | Enemy health | Enemy damage | Enemies | Loot | Drops up to |
|---|---|---|---|---|---|---|---|
| I | 10 | 0 | x1 | x1 | x1 | x1 | Tier II |
| II | 30 | 15 | x1.5 | x1.35 | x1.15 | x1.1 | Tier III |
| III | 50 | 30 | x2.3 | x2.6 | x1.3 | x1.2 | Tier IV |
| IV | 70 | 45 | x3 | x3.4 | x1.45 | x1.35 | Tier V |

**A raid of tier N pays for the gear of tier N+1.** This keeps "tiers mean one thing everywhere" (see [BaseProgression](BaseProgression.md)):
wear tier I in a tier I raid, bring home copper, make copper gear, go to tier II for iron.

| Tier | What is new |
|---|---|
| I | Wolves, boars, rootlings, bog spitters, marsh lights, mistwings, small trolls. Copper, hardwood, flax, granite |
| II | Root Guardians and Moss Shamans (at least one). Iron, deepstone, elderwood, silk. Dead spruce among the trees. Copper still there |
| III | Draugs (at least one). Moonsilver |
| IV | No more copper. The great troll's Trollstone can drop. No new creature, but new abilities: see below and "Gaps" |

### What creatures learn

A higher tier changes what creatures do, as well as how hard they hit. Each ability is a row on the monster with a From Tier (Content Editor › Monsters).

| From tier | Creature | Ability | What it does |
|---|---|---|---|
| II | Forest-Bitten Wolf | Pounce | Leaps from up to 10 m at 18 m/s, three times its running speed, after a short wind-up. On the ground for 0.9 s after: the moment to hit it |
| II | Bog Spitter, Marsh Light | Volley | Three shots in a row on a long cooldown, to step out of |
| III | Blight Boar | Trample | A wide stamp when close, on top of its charge and gore |
| III | Root Guardian | Ground Slam | A slow all-round slam with a long reach and a long wind-up |
| III | Small Moss Troll | Troll Rush | A short charge |
| III | Moss Shaman | Volley | Three stones in a row |
| III | Old Moss Troll | Troll Charge | Rushes from up to 11 m, then stands exposed for 2.5 s |
| IV | Draug | War Cry | Nearby creatures hit 15% harder for 10 s. Interruptible |
| IV | Root Guardian | Call Roots | Raises rootlings around it. Interruptible |
| IV | Old Moss Troll | Calls Its Kin | Small trolls come to its side |

**To add one:** add a row to the monster's Attacks with a From Tier. A monster with any actions never uses its plain attack, so one action has to be for every tier
(`EveryCreature_CanStillFightAtTheLowestTier` checks this).

### Who comes for you

The hunters sent late in a raid and on alarms, and the roamers, also follow the tier: wolves, boars, swarms and mistwings at I (Playtests had wolves as 38 of 50 raid kills when they came alone), then boars and Guardians, then Draugs at IV. Tier I's wolf packs have half the share
of the higher tiers. Over 400 simulated seeds, wolves were 47% of tier I's creatures once the stronger ones were gone from it, and 31% now.

| Tier | Creatures at the start of a raid, on average | Of those, wolves |
|---|---|---|
| I | 13.9 | 4.3 |
| II | 16.3 | 6.7 |
| III | 17.1 | 6.4 |
| IV | 18.8 | 7.0 |

### Elites

A few creatures in each run are elites: named, ringed in their affix's colour, stronger, and worth hunting. As in Diablo, each affix is a short word with a mechanic you can learn, and higher
difficulties bring more of them. Unlike Diablo, their name only shows within 16 m, so it can't give away a position through the fog of war. Set up by `AgentScripts/BuildRaidElites.cs`, edited in
Content Editor › Elite Affixes, with counts and loot per tier on Raids › Rules.

| Tier | Elites in a run | Affixes each | Hunters that bring one | What a kill pays, with Cursed Essence | Relic chance |
|---|---|---|---|---|---|
| I | 1 | 1 | never | 3-5 Copper Ore | none |
| II | 1-2 | 1 | never | 3-5 Iron Ore | 5% |
| III | 2-3 | 1 | 15% | 2-4 Moonsilver Ore | 10% |
| IV | 3-4 | 2 | 25% | 3-5 Moonsilver Ore, 25% chance of Trollstone | 15% |

A kill also gives more experience by itself, as that goes by the creature's health. The great troll and rootlings are never elites, and nor is anything a creature summons. None start within 25 m of the player.

| From tier | Affix | What it does | The answer |
|---|---|---|---|
| I | Ironhide | +80% health, +30 armor, bigger, 10% slower | It's slow: keep moving, hit it after it has swung |
| I | Frenzied | +20% health, 35% faster, 35% less waiting between strikes. Wind-ups are unchanged | Dodge on the wind-up, don't trade blows |
| II | Regrowing | +40% health, regrows 3% a second once unhit for 3.5 s | Stay on it |
| II | Frostbound | +40% health, +10% damage, hits chill | Don't get hit twice, cure it, or fight at range |
| III | Vengeful | +30% health, bursts 1.1 s after death in a 3.5 m circle | Kill it, then step out |
| III | Mistcloaked | +40% health, walks wrapped in fog | Back onto open ground and watch for its ring |
| IV | Rallying | +50% health, those within 9 m hit 15% harder, shown by a faint circle | Kill it first |

Affix multipliers multiply the raid's own scaling rather than adding to it, so an Ironhide at tier IV has 3 x 1.8 times a plain creature's health, not 3 + 0.8.

### What stops what a tier shouldn't hold

Three things stop what a tier shouldn't hold, as loot reaches the player many ways:

1. **Spawn lists:** an entry's From and To (`SpawnTable.Entry.AppearsAt`).
2. **Drops:** `RaidDifficulty.Allows` caps gear, tools and materials on monster drops, caches and raid events. Food and potions are never capped.
3. **Nodes:** `ResourceNode` switches itself off at a tier that allows nothing it gives, whatever placed it. The Ore Collapse event holds iron and
   moonsilver veins of its own; a test run of tier I found them there.

## The balance, as measured

Rules tab › What it comes to works this out live from the assets (`GearBalance`). It compares the best kit of a tier to a reference creature
(60 health, 5 armor, hits for 15), using damage, armor and health only. It leaves out attack speed, perks and timing, so read the numbers
against each other, not as a fight's length.

**Skills (2026-11-07).** The kit's damage includes the Melee skill a player is expected to have at the tier: the baseline level (10) at tier I,
whose level bonuses are nothing, then 20, 30 and 40 (+5%, +10%, +15% melee damage; see [Skills](Skills.md)). Enemy health was raised to the
suggested values with that counted (tier II x1.4 to x1.5, III x2.1 to x2.3, IV x2.6 to x3), so a player whose skill keeps pace finds each tier as
the gear alone used to; one ahead in skill finds it easier, one behind harder. Enemy damage is unchanged: skills don't add toughness.

| Tier | Kit score | Damage | Armor | Health | Own kit: hits to kill / die | Kit before: kill / die |
|---|---|---|---|---|---|---|
| I | 18 | 12.8 | 32 | 150 | 4.9 / 13.2 | no gear: can't compare / 6.7 |
| II | 38 | 18.8 | 67 | 160 | 5.0 / 13.2 | 7.4 / 9.8 |
| III | 61 | 29.3 | 118 | 230 | 4.9 / 12.9 | 7.7 / 6.9 |
| IV | 81 | 37.6 | 184 | 240 | 5.0 / 13.4 | 6.5 / 9.8 |

Measured again on 2026-11-10 with one ring (the tool had worn two): own kit moved by 3% at most, so the multipliers stand.

The multipliers are set so that own kit stays level: a new tier feels the same with the right gear. A kit makes a player about **1.2 to 1.5 times
as deadly and 1.3 to 2 times as tough** per tier. A tier up with the old kit costs about 25-50% more hits to kill. Going back a tier feels the
reverse, so the game stays honest about how much the upgrade was worth.

**To tune:** change gear first, read Suggested in the table, then set the tier's Health and Damage to it. `RaidTierTests` fails if own kit strays more
than 20% from tier I, if the kit before stops hurting, or if a higher kit isn't stronger than a lower one.

What this found on day one: **the Moonsilver set was weaker than Iron** (78 armor against 100), and so tier IV gear lowered a player's toughness.
Its armor is now 170, keeping its spell power and resistances.

### Tier I as played: an encounter model (2026-11-06)

The table above uses one reference creature. For tier I the question is what a group leaves you with, so a small model (health left after
killing a group one at a time, two striking at once) was run on the real numbers: creatures at +30% health and +20% damage since the playtest
round, the Hand Axe or Worn Sword (12) for no gear, and the full woven kit (150 health, 32 armor). "Average" lets half the strikes land and
swings 60% of the time; "good" lets a quarter land and swings 70%.

| Health left after three wolves | Day, average | Day, good | Night, average | Night, good | Night's last minute, average |
|---|---|---|---|---|---|
| No armor | dies | 43% | dies | 37% | dies |
| Club and jerkin | 10% | 62% | 1% | 58% | dies |
| Full tier I kit | 36% | 72% | 29% | 70% | 4% |

The day already did what tier I should: hard bare, easier geared, still a fight. **Night was the problem**: at 1.3x health and 1.25x damage,
and +35% more in the last minute, a full tier I kit lost to a pack of three on average, and a run's hour is rolled by chance (55% day, 25% dusk,
20% night). Night creatures now have **1.1x health and hit no harder** (`Resources/NightSettings`, set by `AgentScripts/BuildBalancePass.cs`),
at the user's call: night fights last a little longer instead of ending sooner. Night is still harder (less sight, more creatures, the last stage
on top) and still pays twice the essence. The figures in the table are after the change.

The tier I weapons (Knotted Club, Hand Axe, Worn Sword at 12, the Fire-Hardened Spear at 10.2 for its reach) are also the prologue's starting
choice, so at tier I gear is what keeps you standing, not what kills faster: a kit doubles toughness and adds 4% damage. Copper weapons are the first
real damage step. Wolves caused most of the raid deaths in the playtest logs, and every logged tier run was at gear score 0, so a geared run is still unplayed.

## Gaps

- **Tier V can't be offered yet:** the only tier V gear is the Trollstone Maul, so a full tier V kit doesn't exist. When armor is added, add a row
  and read off its multipliers.
- **Tier IV brings no new creature.** All 12 monsters are in use by tier III. New abilities carry it for now; a creature of its own is the next content gap.
- **Everything here is calculated, not played.** The hit counts are arithmetic, and they leave out the new abilities, which make higher tiers harder than the table says.
  The Run Comparison window (Playtest) now shows extraction rates and gear score by tier: use those to turn the knobs.

## Sources

- [Diablo III elite affixes (Blizzard Watch)](https://blizzardwatch.com/2015/07/11/combat-elite-monster-affixes-diablo-3/)
- [Diablo elites (Diablo Wiki)](https://di.diablowiki.net/Elite)

- [V Rising gear level by tier (BisectHosting)](https://www.bisecthosting.com/it-it/blog/v-rising-gear-level-guide-weapon-armor-jewelry-gear-level-progression)
- [V Rising Wiki: Gear Level](https://vrising.fandom.com/wiki/Gear_Level) (the 1% and 4% figures, quoted from search results; the page wouldn't load directly)
- [World of Warcraft item levels and tracks (WoWUtils)](https://wowutils.com/loot/item-levels-and-crests)
- [Anthem power scaling analysis (MP1st)](https://mp1st.com/news/anthem-power-scaling-broken-down-using-math-proves-loot-doesnt-matter-bioware-says-itll-be-fixed-in-next-patch)
