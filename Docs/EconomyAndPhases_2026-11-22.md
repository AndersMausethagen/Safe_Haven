# The economy and the game's phases: early, mid and end game

Research date: 2026-11-22. This is research and design only: no code, scene, prefab or asset was changed, and Play mode was not
used. The numbers were read from the `.asset` files (a script resolved every GUID to its item), from the code that turns them into
rates, and from the three long playtest logs in `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests`.

The user's concern, condensed:

> "The drops and gathering are balanced around the player doing the farming. That works in the early game until we unlock the
> Keeper's view. Then we get a lot more possibilities to gather and farm resources."

**How sure each number is.**

| Kind | Where it comes from | How sure |
|---|---|---|
| Costs, recipes, yields, settings | Read from the data | Exact |
| Away rates (rings, fields) | Code formulas (`RingWork`, `Fields`, `StationQueue`) | Exact formula; the inputs are assumptions |
| Raid node counts | The spawn lists' targets (161-182 resource points × each entry's weight) | **Estimate.** Not counted in a launched raid (that needs Play mode). The 2026-11-02 count in BaseProgression was "about 10 copper, 6 iron", a little under these targets |
| Player rates | BaseProgression's "13 oak a minute" (designed), and the playtest CSVs (observed) | Two very different numbers; both are given |
| The mid and end game | Nobody has played past the Heartroot | **Calculated only** |

---

## 0. The short answers

### Early, mid and end game as the game stands

| | Early: the Hearth | Mid: the Keeper | End: the Reach |
|---|---|---|---|
| **Starts** | New game | The Road North (the Heartroot falls), which opens the Keeper's view | Portal tier IV "Reclaimed" (Ward III: Smithy III and a boss beaten), which opens tier IV raids |
| **Length** | 4-6 raids. Design: 90-120 minutes. Seen: 3-4 raids in 80-120 minutes, and no playtest reached its end | Rescues 3-11, one at a time: at least 9 raids, more likely 15-25 | Open-ended. Moonsilver and Trollstone: about 18-27 full raids |
| **Raids** | Misty Woods tier I (tier II after Ward I); the Guardian's Hollow | Misty Woods II-III, Troll Valley, Brukam's Bridge, Moss Marsh III | Tier IV in all three raids. There is no tier V raid |
| **People at home** | Ingrid and Bjarne (Tor if he comes before the Heartroot) | Tor to Sigrid, each bringing a building and an order | All 11 |
| **What defines it** | The player is the only gatherer. Copper, then the Heartroot | Residents gather plain goods; iron; land claimed | Moonsilver, Trollstone, the L4-L5 upgrades, Secure lands, the Lost |
| **The bottleneck** | Hand-gathering wood and stone at home (playtests) | Iron ore and hardwood planks, from raids | Moonsilver ore: a steep wall, at 2-3 times the raids per tier of iron |

### Is the user right?

**Partly.** The Keeper's view multiplies **time** more than **materials**. Every resident source makes plain tier I goods or
crops. No resident, ring, field or party makes ore, ingots, granite, creature parts, Cursed Essence or Life Orbs.

| The claim holds for | By how much (estimate per raid cycle) | Why it matters |
|---|---|---|
| **Crops**: flax, barley, healing herb | Farm Plots are cheap (8 Wood, 8 Sticks) and their number is unlimited. About 9-13 net a plot each cycle. Six flax plots make the whole game's linen (about 200 flax) in about 4 cycles | Linen stops being a cost. Draughts still need Orb Tincture (Life Orbs come from raids), so herbs leak nothing |
| **Grey Stone**, once Hild (the 10th rescue) quarries | About 35 away and 25-30 at home a cycle. A ring of regrowing Grey Stone never runs out | It arrives with its own sink: Hild teaches the Stone Wall, and a 30-piece ring costs about 510 Grey Stone |
| **Plain goods late in the game** | Home income rises with every resident, while the plain part of upgrades falls: L2 about 437 wood-equivalent, L3 358, L4 210, L5 144 | Surplus will pool from about the iron tier on. This is the structural form of the user's worry |

| The claim does not hold for | Why |
|---|---|
| **Wood** | Charcoal fuels every ingot (2, 3 and 4 a smelt). The iron tier alone burns about 520 wood, about 45-75 a raid. One felling ring makes about 40-50 a cycle, so it pays the smelter's fuel, not more |
| **Copper, iron and moonsilver ore** | Raids, plus a trickle: Greyfell's tithe (2 copper ore a dawn) and the Trading Post, limited by pennies and stock. No ore node stands in the Haven |
| **Granite, thick hide, silk, deepstone, parts, essence, Life Orbs, trophies** | Raid-bound |

**Two problems the user didn't name are bigger:**

1. **Elderwood (tier III) is a home material already**, through the player's own axe. Haven trees roll better wood with better axes:
   about 9.5% Elderwood with a copper axe and 29.5% with an iron one. A raid gives about 2-3 Elderwood, so home chopping is by far
   the main source of the 76 that L4-L5 and the tier IV-V bows and shields need. The Keeper's view doesn't cause this; felling
   rings use a tier I axe.
2. **Moonsilver is a wall.** At the spawn lists' targets, a full tier III-IV raid gives about 5-7.5 ingots, against about 16 for
   copper and 10-16 for iron. The moonsilver tier needs about 105 ingots, so about 14-21 raids against 5 and 7-12 before.

### What the playtests show (3 long runs, 2026-11-19 to 21)

| Run | Home : raid time (after the prologue) | Raids, and seconds in each | Reached |
|---|---|---|---|
| 11-19 | 49 : 23 minutes (68% home) | 4: 273, 652, 108, 259 | Ingrid; the Altar |
| 11-20 | 57 : 13 minutes (81% home) | 3: 274, 229, 244 | Ingrid, Bjarne, Tor; the Altar; no Smithy |
| 11-21/22 | 106 : 13 minutes (89% home) | 3: 259, 209, 283 | Ingrid; the Altar, paths and fences |

- Raids last **about 4-5 minutes of the 12 on offer**, and bring home 11-24 copper ore.
- At home, players gather about **2-3 Wood and 2-3 Grey Stone a minute** over everything they do there (the design's 13 oak a
  minute is pure chopping).
- The early game is where the grind is today. Away income is counted in seconds away, so 4-minute raids also cut every
  Keeper's-view source by about 2.7 times against the 12-minute figures below.

### Top recommendations (details in §4)

1. **Make "raids per goal" the unit, and keep "home makes the plain, raids make the tier"** (GDD §7.7). Targets: copper 5 raids,
   iron 7, moonsilver 9, Trollstone 6.
2. **Cap wood at home at Hardwood.** Elderwood should come from raids, the Elder Tree (give it a small yield), the Trading Post and
   outposts.
3. **Take down the moonsilver wall.** Aim for about 10-12 ingots a tier III-IV raid, through more nodes, elite ore and Smith's
   Chests, and cut L4/L5 moonsilver about 15%. Gear recipes stay as they are (gear-first rule).
4. **Bound the fields visibly**, and give crops a positive sink:
   - a Farmer sows at most 4 plots, or 6 at Farm Plot II;
   - "Well fed": a resident who ate a Larder meal today works +10%.
5. **Give upgrades L3 and up a growing plain part** (+16, +24 and +32 Fine Planks or Fine Stone Blocks), so rising worker income
   meets rising plain demand.
6. **The end game: outposts at Secure lands.** They cost bulk plain goods. Each gives a capped regional tier material (at most 15%
   of the tier's need) and a hook into the matching raid. A push pauses one; nothing is destroyed.
7. **Measure first** for the early game. Log what home time is spent on, and count the nodes in a launched raid of each tier,
   before any early-game cost changes.

---

## 1. How the economy works now (measured)

### 1.1 Units and assumptions

| Term | Meaning |
|---|---|
| **Full raid** | 720 s (12 min: `RaidDefinition.durationSeconds`), +90 s per ward level, +120 s with Sigrid. A large raid adds 750 s and doubles the width |
| **Observed raid** | About 260 s (median of 10 playtest raids) |
| **Raid cycle** | One raid plus home time. Designed: 12 + 10 minutes. Observed: 4.5 + 15-30 minutes. **A dawn comes every 24 real minutes** (`Safe Haven Day Night` › Day Length 24), so about one dawn a cycle |
| **60% of nodes** | BaseProgression's careful player |
| **Node yield** | Main yield plus burst yield, rolled once per node (`ResourceNode.GrantExtra`): copper 4 + 1.5 = 5.5, iron 3.5 + 1.5 = 5, moonsilver 4 + 1 = 5, Grey Stone 2.5 + 1.5 = 4. Critical swings add 1 each and are not counted |
| **Ingot** | 3 ore plus 2, 3 or 4 Charcoal (copper, iron, moonsilver). Charcoal is 3 Wood for 2 at the Lodge |
| **Raw equivalents** | Rough Plank 1.5 Wood. Fine Plank 2 Wood. Hardwood Plank 2 Hardwood + 0.5 Wood. Rough Stone Block 1.5 Grey Stone. Fine Stone Block 2 Grey Stone. Granite Block 2 Granite + 0.5 Grey Stone. Linen 3 Flax. Nettle Cord 4 Fibre |

### 1.2 Where each material comes from

| Material (tier) | Raid nodes | Raid loot | Home: player | Home: residents | Territory and trade |
|---|---|---|---|---|---|
| Sticks, Small Rocks, Berries (0) | yes | – | forage, 90-240 s regrowth | quarry (Small Rocks) | Ravenwood tithe (berries) |
| Wood (I) | yes | – | trees (3 logs a tree with stone, 4 copper, 6 iron; +2 a tree per Lodge III/V) | felling ring | tithes 2-4 a dawn |
| Grey Stone (I) | – | – | 113 placed nodes, 180 s regrowth; 10 Small Rocks make 1 | quarry ring (regrows) | tithes 3-4 a dawn |
| Nettle Fibre, Barley, Turnip (I) | yes | caches | wild nodes | Farmer (barley, turnip) | – |
| Hardwood (II) | 10 nodes (axe II) | Know Your Enemy | **9.5% of logs (stone axe), 27% (copper), ~35% (iron)** | ring 9.5% (tier I axe) | 2 a dawn (Hollow Pines, Westwood); Trading Post |
| Flax, Healing Herb (II) | 8-9 nodes | caches | – | **Farm Plots, no limit** | Stillwater and Alder Mere (flax), Westwood (herb) |
| Copper Ore (II) | 12-14 nodes (T1-T3) | elites T1, Smith's Chest (ingots) | – | – | **Greyfell 2 a dawn**; Trading Post 6 for 2 pennies, 3 a raid |
| Granite (II) | 5 (Woods), 13-14 (Valley) | Troll Hoard | Great Boulders (finite) | – (quarry pick tier I) | Trading Post |
| Iron Ore (III) | 7 (Woods T2+), 13 (Valley), 19 (Marsh) | elites T2, Smith's Chest, Troll Hoard | – | – | Trading Post 6 for 4, 3 a raid |
| Thick Hide (III) | – | creatures | – (home boars give Rough Hide) | – | Wolfwood 1 a dawn; Trading Post |
| Elderwood (III) | ~1.2 nodes (max 2 a group) | 6% a cache | **9.5% (copper axe), 29.5% (iron axe)** | – | Trading Post 1 for 10 |
| Deepstone, Huldra Silk (III) | ~1.2-1.9 nodes | 5% a cache (silk) | – | – | Trading Post 1 for 10 |
| Moonsilver Ore (IV) | **2.4 (Woods), 2.6 (Valley), 3.7 (Marsh)**, tier III+ | elites T3-T4 2-5; Smith's Chest 20% an ingot | – | – | Trading Post 3 for 10 |
| Trollstone (V) | – | trolls 2-3, elites T4 10-40%, Troll Hoard | – | – | Trading Post 1 for 35 |
| Parts, Cursed Essence, trophies | – | raid only (`ItemDefinition.raidOnly`) | – | – | – |

### 1.3 Raid income per full raid (estimates from the spawn lists)

| Raid and tier | Ore nodes expected (target) | Ore at 60% | Elites and caches | ≈ Ingots a raid |
|---|---|---|---|---|
| Misty Woods I | copper 13.2 | 43 | elite 1 × 3-5 copper ore; Smith's Chest copper ingots | **~16 copper** |
| Misty Woods II | iron 7.4 (copper 12.4) | 22 iron | 1-2 elites × 3-5 iron ore; chests | **~10 iron** (and ~14 copper) |
| Troll Valley II | iron 13.1 | 39 | the same; Troll Hoard 40% iron ingot | **~15-16 iron** |
| Misty Woods III | moonsilver 2.4 (at most 3 a group) | 7 | 2-3 elites × 2-4 moonsilver ore; Smith's Chest 20% an ingot | **~5 moonsilver** |
| Moss Marsh III | moonsilver 3.7, iron 18.7 | 11 | the same | **~6 moonsilver** (and ~19 iron) |
| Woods or Valley IV | moonsilver 2.6-2.8 | 8 | 3-4 elites × 3-5; Trollstone 10% (Woods), 40% (Valley) | **~7.5 moonsilver**; 3-5 Trollstone with a big troll killed, 1-2 without |

Observed in the playtests (Misty Woods I, 4-5 minutes):

- copper ore: 11, 14, 13 and 24, then 0 and 0 in the two raids that rescued Bjarne and Tor;
- 8-21 Rough Hide, 11-28 Cursed Essence and 2-11 Mistwing Feathers a raid.

### 1.4 What each tier costs (data)

**Buildings, every level once** (from `Data/Buildings`; the raw totals use §1.1, charcoal included):

| Level | Buildings | Main items | Raw totals |
|---|---|---|---|
| L1 (48 + 13 decor) | all | 221 Wood, 176 Rough Plank, 112 Grey Stone, 94 Sticks, 54 Nettle Cord, 44 Fine Plank, 39 Rough Stone Block, 28 Fine Stone Block, 20 Hardwood Plank, 12 Copper and 10 Iron Ingots | **~755 Wood, 250 Grey Stone**, 250 Fibre, 48 Hardwood, 36 copper ore, 33 iron ore |
| L2 (17) | the upgrades | 114 Fine Plank, 52 Rough Plank, 48 Grey Stone, 40 Fine Stone Block, **28 Copper Ingot**, 5 Iron Ingot | **437 Wood, 154 Grey Stone**, 84 copper ore, 15 iron ore |
| L3 (10) | the upgrades | **98 Hardwood Plank, 54 Iron Ingot, 36 Granite Block**, 28 Rough Plank, 4 Moonsilver; 32 Purified Essence | 358 Wood, 196 Hardwood, 162 iron ore, 72 Granite |
| L4 (5) | Workshop, Smithy, Tailor, Lodge, Hunter's | **34 Moonsilver Ingot, 24 Elderwood**, 12 Huldra Silk, 12 Linen, 4 Guardian Heartroot, 6 Grave Iron; 32 PE | 210 Wood (charcoal), 102 moonsilver ore |
| L5 (4) | Workshop, Smithy, Lodge, Hunter's | **24 Moonsilver Ingot, 32 Elderwood, 8 Trollstone**, 4 named trophies; 110 PE | 144 Wood (charcoal), 72 moonsilver ore |

**The plain part of a level shrinks after L2**: 437, 358, 210 and 144 wood-equivalent, nearly all of L4-L5 being charcoal. Home
income grows the other way.

**Gear, one kit per tier** (armour, a weapon, the tools, a shield and bow or pack: `Data/Crafting/Recipes`):

| Tier | Kit |
|---|---|
| II copper | 25 Copper Ingot, 10 Rough Hide, 9 Nettle Cord, 6 Rough Plank, 5 Cured Leather |
| III iron | 28 Iron Ingot, 10 Linen, 10 Hardwood Plank, 7 Thick Hide, 6 Cured Leather, 3 Elderwood |
| IV moonsilver | 22 Moonsilver Ingot, 12 Huldra Silk, 8 Elderwood, 2 Deepstone, 1 Fiddle String |
| V Trollstone | 17 Moonsilver Ingot, 11 Trollstone, 9 Elderwood, 1 Fiddle String |

**Each tier's anchor metal in all, and raids needed:**

| Metal | Buildings, portal, modules | Gear | Total | Wood as charcoal | Ingots a full raid | **Full raids** |
|---|---|---|---|---|---|---|
| Copper | ~55 (Kitchen 4, Trading Post 8, L2 28, Steadied 4, modules and carts ~11) | 25 | **~80** | ~240 | ~16 | **~5** |
| Iron | ~87 (L1 10, L2 5, L3 54, Bound 4, modules and bardings ~14) | 28 | **~115** | ~520 | 10-16 | **~7-12** |
| Moonsilver | ~66 (L3 4, L4 34, L5 24, Moonlit 4) | 39 (IV and V) | **~105** | ~630 | 5-7.5 | **~14-21** |
| Trollstone | 8, and 4 named trophies | 11 | **~19** | – | 3-5 with a big troll killed | **~4-6** |

The ladder is smooth until moonsilver, then the raids per tier double. Copper and iron pace about where BaseProgression planned.

**The portal and the ward** (`SettlementSettings`):

| Step | Cost | Also needs | Opens |
|---|---|---|---|
| Ward I | 6 PE | Smithy I | tier II raids |
| Ward II | 12 PE, 4 Life Orbs | Smithy II, Yrsa | tier III |
| Ward III | 20 PE, 6 Life Orbs | Smithy III (Hunter's Lodge and Tailor's Shop, so Gunnar and Asa), a boss | tier IV |
| Steadied | 4 Life Orbs, 4 Copper Ingots, 4 Fine Stone Blocks | – | a second safe pocket |
| Bound | 6 Life Orbs, 4 Iron Ingots, 4 Granite Blocks | – | extraction 20% faster |
| Moonlit | 8 Life Orbs, 10 PE, 4 Moonsilver Ingots, 2 Deepstone | – | a third pocket, faster again |

### 1.5 Home income: the player, and every Keeper's-view source

| Source | Who, and from when | Rule (data and code) | Per full cycle (12 + 10 min) | Per observed cycle (4.5 + 15 min) |
|---|---|---|---|---|
| **Player chopping** | from the start | 3-10 logs a tree by axe and Lodge tier; 13 a minute designed | up to ~130 if they only chop | **~2-3 a minute observed** over home time |
| **Player stone** | from the start | 4 a Grey Stone node; 10 Small Rocks for 1 | – | ~2-3 a minute observed |
| **Felling ring, away** | Tor (3rd rescue), after the view | each idle feller: seconds × 0.4 ÷ (12 + 20 s) trees, 3 logs each (axe I, 9 m tree, 9.5% Hardwood); only while the Lodge's own queue is idle | ~9 trees, **~27 logs** | ~3 trees, ~10 |
| **Felling ring, home** | the same | 12 s ÷ work speed + walk, in working hours 08-18 (42% of the day), while the bench is idle | ~20 logs (estimate) | ~30 (estimate) |
| **Quarry ring, away** | Hild (10th rescue) | seconds × 0.4 ÷ (14 + 20 s) rocks, at most what stood: 4 Grey Stone + 3 Small Rocks each | ~8 rocks, **~34 GS, 25 SR** | ~3 rocks, ~12 GS |
| **Quarry ring, home** | the same | about 22 s a rock measured (probe 2026-11-22); Grey Stone regrows in 180 s, so a 5-rock ring is capped near 6.7 GS a minute | ~25-28 GS | ~35-40 GS |
| **Fields** | Asa (8th rescue): the Farm Plot is hers | a crop every 300-360 s at speed 1 (2 with the Farmer); output holds 10 until carried; water lasts 1200 s | **per plot ~9-13 net** (barley +3, flax +2, herb +2 a crop) | the same per plot |
| **Dock, Beehive** | Gunnar (Dock), a swarm found (Beehive) | Perch 3 per 45 s, Honey 2 per 240 s; output holds 10 | ≤10 each, more if hauled | ≤10 |
| **Hunting or fishing party** | Gunnar (6th rescue), held land | a raid of at least 180 s: 2-4 meat and 1-2 hide, or 2-4 perch and 0-2 trout (+1 a rank); the station stands idle that raid | ~5 items | ~5 items |
| **Tithes** | Secure lands (hold 70+), after The Cold Stones | at each dawn: 1-6 items a land | all 11 Secure: ~9 Wood, 10 Grey Stone, 4 Hardwood, 4 Flax, 3 Cloudberries, 2 copper ore, 1 Thick Hide, ~11 food and hide (**~44 items**) | the same |
| **Homecoming gift** | anyone at home | 60% a raid of at least 180 s: 4 Fine Plank, 3 Fine Stone Block, 6 Charcoal, 2 Linen... | ~5 wood-equivalent | the same |
| **Wish met** | each resident, **once** | the same gift list | – | – |
| **Stations worked** | each trade | **conversion only**: Smith ~5 s an ingot at speed 1.6-1.85; stock targets 30 copper, 20 iron, 16 moonsilver, 40 planks, 40 charcoal | 0 new material; saves the player's time | – |
| **Torches** (sink) | every torch | 1 Charcoal a night | −1.5 Wood a torch a cycle | the same |

What this says:

- One feller makes about 40-50 logs a cycle, against about 45-75 a raid of charcoal in the iron tier. **Wood stays taut.**
- One mason makes about 60 Grey Stone a cycle, against about 25-40 that the mid game's buildings need a cycle. **Stone will
  pile up unless walls are built.** It comes late (rescue 10), together with the Stone Wall.
- **Fields scale with plot count.** Nothing limits it but 8 Wood and 8 Sticks a plot.
- Tithes, parties and gifts together are about 50-55 items a cycle, nearly all plain. They are flavour, not an economy.

### 1.6 Verdict by material

| Material | Whole-game need (est.) | Home share once the view opens | Verdict |
|---|---|---|---|
| Wood | ~2,500, plus torches | high, but charcoal eats it | **Eased, not inflated**, until moonsilver costs stop asking for planks |
| Grey Stone | ~460 + ~510 per stone wall ring | high (quarry, regrowing) | **Inflates late** without walls; bounded by one mason |
| Barley, turnip | meals | very high (plots) | Inflates. Harmless for gates, but meal buffs reach 100% uptime |
| Flax, linen | ~200 flax | very high | **Inflates**: about 4 cycles of six plots cover the game |
| Healing herb | draughts | high | Inflates, but **Orb Tincture (Life Orbs) still gates draughts** |
| Hardwood | ~245 | high, from the player's axe | Already mostly home, before the view. Fine for a tier II common |
| **Elderwood** | ~76 | **very high, from the player's axe** | **A leak through tier III-V**, not from the view |
| Copper, iron, moonsilver ore | 240, 345, 315 | ≤5% (a tithe, and pennies) | **Holds**: raid-bound |
| Granite, thick hide, silk, deepstone | small | ≤10% | Holds |
| Parts, essence, Life Orbs, trophies | – | 0 | Holds |

### 1.7 Data notes found on the way

- The Palisade Gate lists Wood twice (12 and 4). If 4 Rough Plank was meant, the gate costs the wrong thing.
- The farm's Elderwood recipe turns 1 into 1 over 900 s, which gains nothing.
- The ruined Storage Shed is repaired before Tor, so its Lodge requirement only matters for a second one.
- `ARCHITECTURE.md` "Gathering bootstrap" says the Stone Pickaxe keeps strength 2. The data now gives it 1, and copper needs 1.

---

## 2. The phases in detail (as built)

### 2.1 Early: new game to The Road North

| | |
|---|---|
| **Quest spine** | The prologue, then the portal; Ingrid; the Altar; Bjarne; the Smithy; The Weakening Ward; The Breathing Hollow (needs Bjarne); What Breathes Under the Roots (the Heartroot, tier I, about 7 minutes); The Road North (opens the view and Troll Valley) |
| **Buildings known** | Campfire, Workshop, Hut, Small Chest, Well, Torch, Skigard, Hedge, Stable, paths, Notice Board (Homestead); Altar and Cauldron (Ingrid); Smithy, Bellows, Quench Tub (Bjarne); Lodge, Palisade, Log Rack (Tor, if he comes) |
| **Key costs** | Workshop: 24 Wood, 12 Grey Stone, 12 Sticks. Smithy: 24 Grey Stone, 8 Rough Stone Block, 8 Rough Plank (36 GS and 12 Wood raw). Altar: 6 Rough Stone Block and 4 Life Orbs. Hut: 16 Wood, 12 Sticks, 8 Fibre. Smithy II: 12 Copper Ingots, 8 Rough Stone Block, 8 Rough Plank. Workshop II: 16 Rough Plank, 8 Rough Stone Block, 4 Copper |
| **Minimum plain bill** | About 80 Wood and 75 Grey Stone: about 30 minutes at the observed 2-3 a minute, or 12 at the designed 13 |
| **Gathering** | Home: forage, Wood, Grey Stone, Small Rocks, fibre, boars by day. Raids: copper, hides, essence, Life Orbs, fibre, flax and herbs; hardwood and granite once there are copper tools |
| **Workers** | Ingrid purifies; Bjarne smelts. Nobody gathers |

### 2.2 Mid: the Keeper's view to the Reclaimed portal

| | |
|---|---|
| **Rescues, one at a time** | Tor (rings) → Eirik (Watch Post, guard posts) → Yrsa (Kitchen; **Ward II**) → Gunnar (Hunter's Lodge, Dock, parties) → Liv → Asa (Farm Plot, Tailor; so **Smithy III → Ward III**) → Runa (scouting) → Hild (quarry, Cottage, Stone Wall) → Sigrid (Act II's end) |
| **Raids** | Troll Valley (II+), Brukam's Bridge, Moss Marsh (III+, after the Brukam), Misty Woods II-III |
| **Material focus** | Iron (~115 ingots), Hardwood Planks (98 for L3), Granite Blocks (36 for L3), Linen, Thick Hide; the first moonsilver |
| **Territory** | The Cold Stones (after The Road North): claim, hold, push; Secure lands at hold 70; tithes; renown towards Village (180: the Trading Post) and Haven (300) |
| **Keeper's-view sources, as they arrive** | Rings at rescue 3, parties at 6, fields at 8, scouting at 9, quarry at 10. **The "lot more possibilities" arrive one by one across about 9 raids, not all at once** |

### 2.3 End: the Reclaimed portal onwards

| | |
|---|---|
| **Raids** | Tier IV in every raid. Tier V gear exists, but no tier V raid ("tier V gear is what masters tier IV") |
| **Goals** | Moonsilver (~105 ingots), Trollstone (~19) and the four named trophies; L4 and L5; enchanting and relics; the Lost; a ring of Secure lands |
| **What is missing** | A repeatable loop after Act II, beyond the Lost and the pushes. Few plain sinks: L4-L5 ask almost no planks or stone. No use for land beyond tithes and renown |

---

## 3. How other games do it

### 3.1 Game by game

| Game | Phases and gating | Automation | Works well | Players complain about | Sources |
|---|---|---|---|---|---|
| **Valheim** | A biome is a tier: its boss drops the key to the next biome's material (the Elder's key opens the crypts and iron, Moder opens the Blast Furnace...) | None, beyond smelters fed by hand. Ore can't go through portals, so carrying is part of the cost | Every tier is a new place, new enemies and a new tool. Kilns make wood the fuel of metal | Iron in the crypts is "a glum chore"; the bronze-age grind; carrying silver | [PC Gamer: biome order](https://www.pcgamer.com/games/survival-crafting/valheim-biome-order/), [PC Gamer: mining iron](https://www.pcgamer.com/uk/everything-about-mining-iron-in-valheim-straight-up-sucks), [progression guide](https://allthings.how/valheim-progression-guide-every-biome-boss-and-key-drop/) |
| **Enshrouded** | Rescued craftspeople unlock stations and recipes. Flame Altar levels need survivors and materials, widen the build area and open more of the shroud | **None**: survivors craft but don't gather. Players plant and harvest by hand | People are the unlocks, which Safe Haven copied | Chores: placing flax one piece at a time; NPCs "just standing around"; standing requests for workers who gather | [Craftspeople wiki](https://enshrouded.wiki.gg/wiki/Craftspeople), [Flame Altar levels](https://www.sportskeeda.com/mmo/enshrouded-flame-altar-upgrade-levels), [request for gathering NPCs](https://enshrouded.featureupvote.com/suggestions/593103/comment/669858) |
| **Palworld** | Tech levels; materials by region | **Heavy**: mining bases on deposits make ore, coal, sulfur and stone. **Capped at 3 bases** in vanilla | Removes the "chopping 400 trees" chore | At scale the complaint is control and logistics, not scarcity: "industrial output without industrial control". Materials lose their meaning | [PC Gamer: automation](https://www.pcgamer.com/games/survival-crafting/for-years-survival-games-have-convinced-us-that-chopping-down-400-trees-is-somehow-part-of-the-fantasy-palworld-proves-them-wrong/), [multi-base guide](https://allthings.how/palworld-1-0-how-to-farm-infinite-resources-with-multi-base-setups/), [PalMods: automation fixes](https://www.palmods.gg/blog/palworld-base-automation-fixes) |
| **Gord** | Settlement level gates buildings (the Trading Post after lower tiers). **Clay and iron** pay for upgrades | Workers (Clay Workers, Iron Miners) gather **outside the walls** | Scarce upgrade materials come from dangerous places. **Outposts** near veins shorten trips but must be defended from raids. Deposits run out and refill with the month | Scarcity and micromanagement | [Distractify: upgrades](https://www.distractify.com/p/how-to-upgrade-buildings-fast-gord) |
| **V Rising** | Regions and gear level; the castle heart's level sets floor tiles and servant count | **Servants sent on hunts** (2-23 h): longer is safer and pays more; short ones risk injury | Passive income comes from a planned choice, capped by servants and the heart's level | Upkeep (blood essence) and decay when away | [Servant hunts](https://gamerguides.com/v-rising/guide/dominance/servants/how-to-send-servants-out-for-hunts), [castle heart decay](https://www.pcgamesn.com/v-rising/castle-heart-decay), [Gloomrot patch notes](https://primagames.com/tips/v-rising-secrets-of-gloomrot-update-full-patch-notes-listed) |
| **RimWorld** | Tech research | Colonists do everything | **Raid points scale with colony wealth** (items fully, buildings at half), so surplus raises the threat | It punishes building: players manage "wealth" instead of building freely | [Raid points](https://rimworldwiki.com/wiki/Raid_points) |
| **Against the Storm** | Glade danger: **large deposits appear only in dangerous and forbidden glades**; deposits have charges | Workers in buildings | Rich materials sit behind chosen risk | – | [Goods](https://hoodedhorse.com/wiki/Against_the_Storm/Goods), [Building materials](https://wiki.hoodedhorse.com/Against_the_Storm/Building_Materials) |
| **Frostpunk** | Research | Automatons | **Steam cores can't be made**, only found by expeditions, so they cap automation and advanced buildings | Soft-locks when cores run out | [PC Gamer guide](https://www.pcgamer.com/frostpunk-guide/) |
| **Manor Lords** | Development tree | Workers | Small deposits deplete, and deep mines need a tech. That pushes trade and specialisation (an arms town) | Mines lost before the tech | [Deep mining explained](https://james.gamerguides.com/manor-lords/guide/basics/economy/deep-mining-explained) |
| **ARC Raiders** | Workshop upgrades from found items | **Scrappy** brings basic materials **after each raid, survived or not; nothing from idling in the hub** | Passive income counted in raids, which is the rule Safe Haven already follows | – | [Scrappy guide](https://steelseries.com/blog/arc-raiders-scrappy-guide-upgrades) |
| **Escape from Tarkov** | Hideout modules | Bitcoin farm, fuel upkeep | Hideout as a large sink | The farm's income dominates; upkeep is a constant calculation | [AMBCrypto on the farm](https://ambcrypto.com/blog/escape-from-tarkovs-bitcoin-genius-mechanic-or-broken-scam-the-truth-revealed/amp/) |
| **Darkest Dungeon** | Hamlet upgrades paid **only in expedition heirlooms**, a kind for each building (about 2,051 crests in all) | – | The base can't grow without going out | The last levels are a grind | [Heirlooms](https://darkestdungeon.wiki.gg/wiki/Heirlooms), [upgrade order](https://segmentnext.com/darkest-dungeon-building-upgrades) |
| **Soulmask** | Tribe | Tribesmen gather on their own (1.0) | – | A reviewer found the player character "secondary" and, late on, optional | [Fullcleared review](https://fullcleared.com/reviews/soulmask-review-a-tribe-called-quit/), [CGM: 1.0 tribe AI](https://www.cgmagonline.com/articles/soulmask-tribe-management-in-2026/) |
| **Monster Hunter Rise** | Hunts | Meowcenaries gather while you hunt | Passive income counted in quests | – | [TheGamer: Meowcenaries](https://www.thegamer.com/monster-hunter-rise-best-meowcenaries-argosy/) |

**Economy theory.** Daniel Cook's value chains ([Lost Garden](https://lostgarden.com/2021/12/12/value-chains/)):

- pair sources and sinks of the same power (capped with fixed, trickle with repeatable, grind with exponential);
- keep chains taut, with sinks slightly larger than sources;
- cap investment sources;
- use several distinct inputs ("lock-and-key") rather than one currency;
- "don't replace sink power with sink size": huge fixed costs feel grindy.

The faucet-and-sink model: [Unity economy guide](https://unity.com/resources/game-economy-design-guide).

### 3.2 What Safe Haven should take, and leave

| Lesson | From | For Safe Haven |
|---|---|---|
| A tier is a place and a new tool, not only a number | Valheim, Against the Storm | Tier materials stay in raids (and later outposts). Each tier opens a raid, a creature and a station step: keep doing that |
| Automation is welcome when it removes a chore, harmful when it removes a decision | Palworld, Enshrouded, Soulmask | Residents make **plain** goods only. Tier materials and parts never come from residents |
| Cap the automation by headcount or slots, not by penalties | Palworld (3 bases), V Rising (servants, heart level) | One feller, one mason, a visible field limit per Farmer, outposts limited by Secure lands |
| Count passive income in raids, not hours | ARC Raiders, Monster Hunter, V Rising | Already the rule (away work runs on seconds away). Keep it, and remember short raids shrink it |
| A raid-only key caps automation | Frostpunk's cores, Darkest Dungeon's heirlooms | Every L3+ upgrade already asks one raid-only part (Wisp Ember, Widow Silk, Heartroot...). Keep it; give outposts one too |
| Rich deposits behind danger; outposts must be held | Gord, Against the Storm | End-game outposts in Secure lands, with pushes that pause them |
| Scale threat with wealth | RimWorld | **Leave it.** It is a hidden penalty, against the user's rule. Scale sinks with wealth instead (positive sinks) |
| Upkeep that decays the base | V Rising, Tarkov | **Leave it** (positive rule). Torches already burn charcoal; use bonuses for consuming ("Well fed") rather than losses |
| Sinks as big as the sources, in kind | Lost Garden | Plain income meets plain sinks (L3+ planks and blocks, walls, outposts); tier income meets tier costs |

---

## 4. Recommendations

### 4.1 The phases, as Safe Haven should define them

| Phase | Starts | Ends | Identity | Home : raid time target | Raids |
|---|---|---|---|---|---|
| **Early: the Hearth** | New game | The Road North (the Keeper's view) | **You gather.** Copper, two or three people, the first boss | ≤ 60 : 40 | 4-6 |
| **Mid: the Keeper** | The Keeper's view | The Reclaimed portal (tier IV raids open) | **You plan, and they gather the plain.** Iron, nine rescues, land claimed | ≤ 40 : 60 | 12-18 |
| **End: the Reach** | The Reclaimed portal | Open-ended | **You hold land.** Moonsilver, Trollstone, outposts, the Lost, a ring of Secure lands | ≤ 30 : 70 | 20+ |

The rule under all three (GDD §7.7, stated as numbers):

| Material | Home share of the need (player + residents + land) | Raid share |
|---|---|---|
| Plain (tier 0-I) | 80-100% | 0-20% |
| Tier II commons (hardwood, flax, herbs, honey, cured leather) | 50-70% | 30-50% |
| Tier III+ (elderwood, deepstone, silk, ores) | ≤ 15% (tithes, outposts, trade) | ≥ 85% |
| Parts, essence, Life Orbs, trophies | 0% | 100% |

### 4.2 A tier model for availability and cost

1. **Fix raids per tier, then read off costs** (gear-first: the kit's power is tuned already, so costs move, not stats).

   | Tier metal | Target full raids | Ingots a full raid (target) | Tier demand D = R × S | Today |
   |---|---|---|---|---|
   | Copper | 5 | 16 | 80 | ~80 ✔ |
   | Iron | 7 | 15 | 105 | ~115 (≈ fine) |
   | Moonsilver | 9 | 11-12 | ~100 | ~105, but S ≈ 5-7.5 ✘ |
   | Trollstone | 6 | 3-4 | ~20 | ~19 ✔ |

   Raids per tier grow about 1.3 times a tier. That is slow, but every step brings a place, a creature and a station.
2. **Split a tier's demand** about 35-40% gear, 50-55% buildings and portal, 10% field kits and modules. Today: copper 31/55/14,
   iron 24/64/12, moonsilver 37/63/0.
3. **Each upgrade level =** a plain part P(n), the metal of the tier before (BaseProgression's rule), and from L3 up one raid-only
   part.
   - **P(n) should grow**, not shrink: about 1.3-1.5 times a level, as home income does.
   - Suggested plain additions: +16 (L3), +24 (L4) and +32 (L5) Fine Planks or Fine Stone Blocks a station.
   - That is about 410 Fine Plank or Fine Stone Block equivalent in all (about 820 Wood or Grey Stone), about 18 ring-cycles. It
     soaks the residents' plain output as it peaks.
4. **Price node counts per tier, not per map.** Each tier's raid should hold about 8-10 nodes of its anchor ore. Moonsilver has 2.4-3.7
   today, against 13 copper and 7-13 iron.
5. **Pace the plain goods by home minutes:** a tier's new plain buildings should need about 6-8 minutes of home time per raid of that
   tier, counting residents.

### 4.3 Keeping worker income from trivialising costs

| Lever | Proposal | Fits the user's rules? |
|---|---|---|
| **Plain only** | Residents, rings, fields, parties and tithes give tier 0-II goods only (the farm's Elderwood recipe aside). Write it as a test in `ContentAuditTests`: no resident or land source yields tier III or above, except outposts | Yes |
| **Caps by headcount** (visible) | Keep one feller and one mason (the job lists). Fields: **Fields Per Farmer 4, 6 at Farm Plot II**; the Farmer's orders sow no more, and the plot window says so. More plots still grow if the player sows them by hand | Yes: a visible rule, no penalty |
| **Positive sinks** | **Well fed**: a resident who ate a Larder meal that day works +10% (uses the Kitchen and crops: about 11 meals a day with a full Haven, about 25-35 crops). Feast days later. Torches already burn charcoal | Yes: a bonus for using goods |
| **Bigger plain sinks late** | §4.2 point 3; stone wall rings; outposts (§4.4); Grand homes (Home quality is built) costing planks and blocks | Yes |
| **Tier materials stay raid-bound** | Cap home log rolls at Hardwood (`TreeChoppingSettings` › Home Max Wood Tier 2). Elderwood comes from raids, the Elder Tree (give it a queue: 2 Elderwood a cycle), the Trading Post and outposts | Yes; it removes a leak |
| **Measure in raids** | Away work is already per second away. Add a "came home with" log row per source, so the next playtest shows the mid game in numbers | – |
| **Leave out** | RimWorld wealth scaling, upkeep that decays buildings, food that residents starve without | Against the positive rule |

### 4.4 The end game: what would make it exciting (judged by raids)

| Proposal | What it is | What it does for raids | Effort | Verdict |
|---|---|---|---|---|
| **Outposts at Secure lands** | A building at a lit boundary stone. Cost about 40 Fine Planks, 30 Fine Stone Blocks, 8 ingots of the current tier and 1 Pact Rune. Staffed by a resident, who leaves their station as a party does | 1) **A capped regional material** each raid of 180 s or more, at most 15% of the tier's need. 2) **A raid hook**: the land's matching raid gets a forward camp, an extra exit or a rumour. 3) A push **pauses** it until answered on a raid; nothing is destroyed | M-L | **Recommended**: Gord's outposts, the user's own idea, and the reason to hold land |
| Region-locked materials | Through outposts only | Makes the choice of lands a build choice | (with outposts) | Recommended |
| A tier V raid, or an ascension ladder | The Deep Rune and Blood Moon formalised into levels (Hades' Heat): more loot and trophies for harder runs | The core loop keeps a goal after tier IV | M | Recommended next, after outposts |
| A ring of Secure lands pays off | All Secure: the Reclaimed portal's last step, a Haven title, a portal perk (+1 safe pocket, or the stones as waystones) | Gives territory a finish line that helps raids | S-M | Recommended |
| Monuments and Grand homes | Big plain sinks with renown and comfort (Rested) | Rested is a raid buff; otherwise weak | S | Optional, as sinks |
| Patrols and defence | (cut) | Little | – | Leave |

Suggested outpost yields, at most 15% of the tier's need over its raids:

| Land | At Secure, with an outpost | Tier | A raid |
|---|---|---|---|
| Greyfell | Iron Ore, then Moonsilver Ore at portal IV | III, IV | 5 iron, then 2 moonsilver |
| The Wolfwood | Thick Hide | III | 2 |
| The Hollow Pines | Elderwood | III | 1 |
| The Drowning Fen | Bog Venom; Huldra Silk at portal IV | II, III | 2; 1 |
| Ravenwood | Deepstone (the tarn's edge) | III | 1 |
| Stillwater, Alder Mere, Hightarn | Fish and flax (plain; a raid hook only) | I-II | as tithes |

Move today's tier III tithe items (the Wolfwood's Thick Hide) into outposts. Tithes stay the plain flavour of each land.

### 4.5 The early game

The evidence points to too much home gathering, not too little. Before changing anything, measure:

1. Log **home activity** (minutes gathering, building, crafting, walking, idle per stay) and the plain stock at each departure.
2. **Count nodes** in a launched raid of each tier (the memory note's method) to confirm §1.3.
3. If home time stays above 60% with the essential bill (~80 Wood, ~75 Grey Stone) already met, the cause is elsewhere: quests,
   finding people, fences and paths. If it isn't met, choose one:
   - **(a)** Tor's felling ring from the Lodge window before the Keeper's view (the view only draws it);
   - **(b)** take the doubling (BuildBalancePass) back off the early essentials only: Workshop, Smithy, Altar, Hut.

   The doubling was made when a 40-minute save had 50 spare oak. The playtests now show the opposite.
4. Raids of 4-5 minutes cut away income by about 2.7 times against the 12-minute design. Decide whether short raids are the
   intended play; the stage loot bonuses (+25/50/80%) already reward staying.

### 4.6 Prioritised changes

| # | Change | Numbers | Where | Effort | Why first |
|---|---|---|---|---|---|
| 1 | Measure: home activity rows, "came home with" rows, a node count in a launched raid per tier | – | `PlaytestLogger`; an `sh_raid` launch count | S | Every later number rests on it |
| 2 | Cap home log rolls at Hardwood; give the Elder Tree a queue | Home Max Wood Tier 2; Elder Tree 2 Elderwood a cycle | `TreeChoppingSettings`, `ChoppableTree.Roll`, the Elder Tree's building | S | Closes the tier III-V leak |
| 3 | The moonsilver wall | Woods and Valley moonsilver weight to about 4-5 nodes at tier III-IV, Marsh about 5-6; elite moonsilver ore 2-4 → 3-5 (tier III); Smith's Chest moonsilver 20% → 35%; L4/L5 moonsilver −15% (58 → 49). Target: 105 → ~95 ingots at ~11 a raid ≈ 9 raids | `Data/Raids/*` spawn lists, `Data/Loot`, `Data/Buildings` | S | The steepest step in the ladder |
| 4 | Fields Per Farmer | 4, 6 at Farm Plot II; shown in the plot window | `FarmSettings`, `WorkerProduction` | S | Bounds the only uncapped resident source |
| 5 | Growing plain part for L3-L5 | +16 / +24 / +32 Fine Planks or Fine Stone Blocks a station upgrade | `Data/Buildings` | S | Matches rising home income |
| 6 | Well fed | +10% work for a resident who ate a Larder meal that day | `ResidentLife`, Larder | M | A positive sink for crops |
| 7 | Plain-only audit test | No resident or land source above tier II, outposts aside | `ContentAuditTests` | S | Keeps the rule true as content grows |
| 8 | Outposts | §4.4 | Territory, `LandParties`, a building | M-L | The end-game loop, judged by raids |
| 9 | The Secure ring's payoff | A portal perk at all-Secure | `TerritorySettings`, `PortalTiers` | S-M | A finish line for territory |
| 10 | Early-game fix, if 1 shows it | 4.5 (a) or (b) | Lodge window or the four costs | S | Only after measuring |

---

## 5. Open questions for the user

1. **Moonsilver:** should the end game's metal take about 9 raids, like the others, or stay the long tier (about 15-20)?
2. **Elderwood:** a raid material (cap home wood at Hardwood), or a reward for a better axe at home as now?
3. **Fields:** a visible limit per Farmer, a positive sink (Well fed), or both?
4. **Outposts:** yes? Staffed by residents (taking them off their stations, like parties) or by new settlers?
5. **The Trading Post** stays narrow (your 11-17 answer). Should it also *buy* plain surplus (Wood or Stone for pennies) as a sink?
6. **Raid length:** players extract at 4-5 of the 12 minutes. Intended, or should a longer stay pay more?
7. **The early game:** if the measurements confirm too much home gathering, would you rather have Tor's ring before the view, or
   cheaper early essentials?

## 5b. The user's answers, and what was built (2026-11-22)

| # | Answer | Built |
|---|---|---|
| 1 Moonsilver | Stay the long tier, a little lower | Raid node weight 2 → 2.8 and up to 4 a group; tier III elites 3-4 ore; Smith's Chest ingot 30%; tier IV-V upgrades 58 → 51 ingots. About 12 raids, was 14-21 |
| 2 Elderwood | Raid only | Home Max Wood Tier 2 (Hardwood); no outpost gives it |
| 3 Fields | Less yield, more uses, maybe a limit by Haven rank | Farm Plots 2/3/4/6/8 by rank; barley and turnip crops 3 (were 4); residents eat (below); flour → bread; plain part of upgrades III-V with Linen; linen armour padding; Woven Hangings |
| 4 Outposts | Decide | Staffed by residents, out on raids as parties; the land's own material, capped (Docs/Territory.md) |
| 5 Trading Post | Stays narrow | Unchanged |
| 6 Raid length, 7 early game | Keep for now | Unchanged; measured instead (CameHome, Eaten, HomeStay, Stock, RaidNodes rows) |

Also built: the residents eat (the user: any basic food, cooked first, then raw; well fed +10%, hungry -25%, nobody starves), the
plain-only test (`EconomyRulesTests`), and the ring of Secure lands (+1 safe pocket slot, kept).

## 6. Sources

Internal: `Docs/BaseProgression.md`, `Docs/ColonyModeResearch_2026-11-21.md`, `Docs/Territory.md`, `Docs/GearScoreAndRaidTiers.md`,
`Docs/LootTables.md`, `Docs/QuestAndStoryResearch.md`, `Docs/RescueOrder.md`, `Docs/GameDesignDocument.md` §3.3, §5.3, §7.7, §12.5;
`Assets/_Project/Data` and `Assets/_Project/Resources` (read directly); `Settlement/RingWork.cs`, `Crafting/StationQueue.cs`,
`Gathering/TreeChoppingSettings.cs`, `Territory/Territory.cs`, `Settlement/SettlementSystem.cs`; playtest CSVs 20261119-184435,
20261120-214517 and 20261121-151353.

External:

- Valheim: [PC Gamer: biome order](https://www.pcgamer.com/games/survival-crafting/valheim-biome-order/); [PC Gamer: mining iron](https://www.pcgamer.com/uk/everything-about-mining-iron-in-valheim-straight-up-sucks); [progression guide](https://allthings.how/valheim-progression-guide-every-biome-boss-and-key-drop/)
- Enshrouded: [Craftspeople (wiki)](https://enshrouded.wiki.gg/wiki/Craftspeople); [Flame Altar levels](https://www.sportskeeda.com/mmo/enshrouded-flame-altar-upgrade-levels); [request for gathering NPCs](https://enshrouded.featureupvote.com/suggestions/593103/comment/669858)
- Palworld: [PC Gamer: automation](https://www.pcgamer.com/games/survival-crafting/for-years-survival-games-have-convinced-us-that-chopping-down-400-trees-is-somehow-part-of-the-fantasy-palworld-proves-them-wrong/); [multi-base setups](https://allthings.how/palworld-1-0-how-to-farm-infinite-resources-with-multi-base-setups/); [PalMods: automation fixes](https://www.palmods.gg/blog/palworld-base-automation-fixes)
- Gord: [Distractify: upgrades and outposts](https://www.distractify.com/p/how-to-upgrade-buildings-fast-gord)
- V Rising: [servant hunts](https://gamerguides.com/v-rising/guide/dominance/servants/how-to-send-servants-out-for-hunts); [castle heart decay](https://www.pcgamesn.com/v-rising/castle-heart-decay); [Gloomrot patch notes](https://primagames.com/tips/v-rising-secrets-of-gloomrot-update-full-patch-notes-listed)
- RimWorld: [raid points](https://rimworldwiki.com/wiki/Raid_points)
- Against the Storm: [Goods](https://hoodedhorse.com/wiki/Against_the_Storm/Goods); [Building materials](https://wiki.hoodedhorse.com/Against_the_Storm/Building_Materials)
- Frostpunk: [PC Gamer guide](https://www.pcgamer.com/frostpunk-guide/)
- Manor Lords: [deep mining](https://james.gamerguides.com/manor-lords/guide/basics/economy/deep-mining-explained)
- ARC Raiders: [Scrappy](https://steelseries.com/blog/arc-raiders-scrappy-guide-upgrades)
- Escape from Tarkov: [AMBCrypto: the bitcoin farm](https://ambcrypto.com/blog/escape-from-tarkovs-bitcoin-genius-mechanic-or-broken-scam-the-truth-revealed/amp/)
- Darkest Dungeon: [Heirlooms](https://darkestdungeon.wiki.gg/wiki/Heirlooms); [building upgrades](https://segmentnext.com/darkest-dungeon-building-upgrades)
- Soulmask: [Fullcleared review](https://fullcleared.com/reviews/soulmask-review-a-tribe-called-quit/); [CGM: 1.0 tribe AI](https://www.cgmagonline.com/articles/soulmask-tribe-management-in-2026/)
- Monster Hunter Rise: [Meowcenaries](https://www.thegamer.com/monster-hunter-rise-best-meowcenaries-argosy/)
- Economy theory: [Lost Garden: value chains](https://lostgarden.com/2021/12/12/value-chains/); [Unity: economy design guide](https://unity.com/resources/game-economy-design-guide)
