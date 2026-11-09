# Base progression

How Safe Haven grows, and why. The numbers live in the assets and are set by `AgentScripts/BuildBaseTiers.cs`,
`AgentScripts/BuildFoundation.cs`, `AgentScripts/BuildHavenProgression.cs` and `AgentScripts/BuildBalancePass.cs`, then `AgentScripts/BuildRescueOrder.cs`
for who opens what; running them again, in that order, restores them. Rule: **the base shows progress, and everything it gives helps you in the woods.**

## The rules

1. **Tiers mean one thing everywhere.** I Stone and Wood, II Copper, III Iron, IV Moonsilver, V Trollstone. A material's tier is the
   tier of the gear made from it, and a building's tier is the best gear it can make.
2. **Gear is made at its own tier; what gets used up comes one tier earlier** (`RecipeDefinition.RequiredStationTier`). A tier I Smithy
   smelts the copper that pays for tier II, where copper gear is made. Nothing has to be authored per recipe.
3. **Each building opens the next** (`BuildingDefinition.Requires`, read by `BuildingUnlocks`).
4. **Each ore needs the pickaxe of the tier below it**, so every metal has to be used before the next can be reached.
5. **Locked recipes stay visible** at the station, with "Needs Smithy Tier II", so the player can see what to aim for.

## The rule for upgrades

**Every upgrade must open something new to make, take a chore off the player, or answer a named raid problem.** Speed, queue and output can come with
a level, but they are never the reason to buy it: stations keep working during raids, and a full Smithy queue of the longest recipe takes 3.5 minutes
against a raid of 16:40. `HavenProgressionTests.NoUpgrade_IsOnlySpeed` holds the game to this.

## The chain

```
Campfire, Torch, Lantern Post        open from the start
Workshop I                           basic gear, utilities, stone work, repairs
├─ Hut                               the first beds
├─ Smithy I                          smelts copper (Bjarne)
├─ Cauldron I                        healing and tonics (Ingrid)
├─ Woodcutter's Lodge I              planks, shields (Tor)
│  │  └─ Kitchen                     meals with lasting bonuses
│  ├─ Cauldron, Storage Shed, Palisade, Watch Post
├─ Hunter's Lodge I                  cures hides; bows and more game at higher tiers
│  └─ Tailor's Shop I                linen; leather gear and packs at higher tiers
├─ Small Chest, Farm Plot, Well, Spike Trap, Banner, Planter
└─ Workshop II ─ Cottage             four beds and the first real comfort

Later buildings, so the chain goes on past copper:
Stone Wall       Workshop III        three times a palisade's health
Iron Trap        Smithy III          bites far harder than a stake
Runic Ward Totem Watch Post III and Purification Altar   slows and wears down what comes near it in a breach
Trading Post     Workshop II and the rank of Village     sells valuables, buys what luck left short
```

**People open their workplaces** ([rescue order](RescueOrder.md)): a building someone works at is not known from the start. Bringing that person
home teaches it, as does giving them the job: the Altar and Cauldron (Ingrid), Smithy (Bjarne), Lodge and Wainwright's Yard (Tor, the Yard at Lodge II),
Watch Post (Eirik), Hunter's Lodge and Dock (Gunnar), Farm Plot and Tailor's Shop (Asa), Kitchen (Yrsa), Cottage and Stone Wall (Hild), Haven Banner
(Sigrid). The chain above still sets what has to stand first. Beehive, Sauna and Elder Tree come from things found in raids, and the Wainwright's
Ledger is a shortcut to the Yard.

**Later tiers ask for other buildings** (`BuildingLevel.requires`), so the whole haven climbs together: Smithy III needs the Hunter's Lodge and Tailor's
Shop (iron gear is made with their leather and linen), Smithy IV needs Altar II, Smithy V needs Workshop IV, Workshop III needs Lodge II, Workshop IV and
Lodge IV need Smithy III, Workshop V and Lodge V need Smithy IV, Kitchen II needs a Farm Plot, Kitchen III needs Hunter's Lodge III, Cauldron III needs
Altar II, Watch Post III needs Smithy II. A level that isn't open says what it waits for, in H and at the station.

| Tier | Workshop | Woodcutter's Lodge | Smithy | Hunter's Lodge | Tailor's Shop |
|---|---|---|---|---|---|
| I | Knotted Club, Fire-Hardened Spear, Wicker Shield, Bandages, woven hide gear, stone and granite blocks | Oak and hardwood planks, Round Shield | Smelts copper | Cures leather, makes fine cord work | Linen |
| II | Fang and tusk jewellery, Venom Fang | Copper-Bound Shield | Copper tools and gear, smelts iron | Hunter's Bow, more leather from a hide, more game at home | Cured leather set, Hide Rucksack |
| III | Wisp Pendant, Field Repair Kit | Iron Kite Shield, +1 log per cut at home | Iron tools and gear, smelts moonsilver | Elderwood Longbow, more leather from thick hide | Traveller's Pack |
| IV | Forward Camp Kit | Moonwood Shield | Moonsilver gear, greatsword and spear | Moonstrung Bow, more game | Wayfarer's Frame |
| V | Waystone Shard | Trollwall Shield, +1 log per cut | Trollstone Maul, Blade and full set | Trollhorn Bow | |

| Other | What its tiers open |
|---|---|
| Kitchen | I: 8 meals. II: Boar Pie, Trout in Cream, with bonuses that scale with you. III: Haven Feast, Moon Broth, and one more meal in you at once |
| Cauldron | I: healing and tonics. II: Greater Healing, Swiftfoot. III: Radiant Draught, Ironbark, Frostward |
| Altar | I: purifies, empowers, enchants, deepens skills. II and III: 25% more Purified Essence each, from the same curse |
| Campfire | I: basic meals. II: 4 m more light against the night |
| Hut, Cottage | 2 and 4 beds, 3 and 6 upgraded |

There is no raid tier V yet: tier V gear is what masters tier IV. A raid of tier V would need creatures of its own (see [gear score and raid tiers](GearScoreAndRaidTiers.md)).

## What the base does for raids (the Raids tab in H)

| | Source | Effect |
|---|---|---|
| Depth | Portal ward levels | Ward I opens raid tier II, II opens III, III opens IV (`RaidDifficulty.wardLevelNeeded`), on top of the gear floor. It costs Purified Essence, so the essence loop is part of going deeper |
| Reach | Ward levels; Sigrid | 90 s more on every raid, per level; 2 minutes more with a Leader |
| The way home | Portal improvements, made at the portal | Steadied: a second safe pocket. Bound: extraction 20% faster. Moonlit: a third pocket, and 20% faster again |
| Endurance | Comfort: Cottage 4 (+2 upgraded), Sauna 3, Kitchen 2, Hut 1 (+1), Well, Campfire, Lantern Post, Banner, Planter 1 each, and 1 per resident with a bed | Well Rested on setting out: +15% stamina, +10% health, for 60 s per point. Percentages, so it keeps its worth as gear improves. Each kind counts once |
| Knowledge | Watch Post tiers | Rumours before a raid, and it teaches the raid runes: Wayfinder (I), Rune of Plenty and Norn's Hourglass (II), Blood Moon, the harder raid for more loot (III) |
| Field kits | Workshop III to V | Repair kit, a forward camp to rest at, a Waystone Shard that opens a way home where you stand |
| Carrying | Tailor's Shop | Each pack brings more home: Rucksack 40 weight and 16 slots, Traveller's Pack 60 and 20, Wayfarer's Frame 90 and 24 |

## What the base does at home

**People.** The seven of the expedition are found through rumours. Beyond them, each rank draws a settler who comes with a job: Hild the Mason (Homestead) keeps blocks in the stash, Asa the Farmer or Weaver (Hamlet) keeps crops growing or flax turning into linen, Gunnar the Hunter (Village) cures leather, Yrsa the Cook (Haven) keeps bread and stew in. Their models are stand-ins for now, borrowed from the expedition.

**Beds never gate anyone.** A resident comes home, takes a job and works whether or not there is a bed. Someone with one works 25% faster on top of the 50% a worker gives, and adds a point of comfort. Beds are shared in order of arrival (`SettlementState.HasBed`), and the People tab says who has one.

**Ranks land through the game**: Homestead 25, Hamlet 90, Village 180, Haven 300. Before, about 75 renown was buildable before the first raid and the top rank came around copper.

**Defence climbs too.** Stone Walls and Iron Traps follow the Workshop and Smithy. From Tier II the Watch Post shoots the nearest attacker in a breach (harder per tier, faster with a Warden). It keeps to breaches, so the game hunted by day stays the player's. A Runic Ward Totem chills and wears down what comes near it.

**The Trading Post** turns the valuables raids hand over into Old Silver Pennies, and pennies into a little of what luck can leave a player short of: ore, thick hide, deepstone, silk, elderwood, a Trollstone. Stock returns after each raid survived, and nothing is offered ahead of the Smithy's tier (`TradeSettings`).

**Nothing mandatory hangs on luck.** The Moonsilver Folio teaches the ingot recipe, so its rumour is urgent as soon as Smithy III stands (`RaidRumourDefinition.needsTier`). It used to average 8 raids, 18 or more for one player in ten. Liv keeps runes nobody else can teach: Hush, for a quieter road with poorer caches, and the Deep Rune, for far fiercer creatures and far richer caches.

## Tiers I and II: the foundation

**Before the first raid** there is something to do at home:

- The Workshop makes a club, a spear, a wicker shield and bandages from wood, sticks and nettle.
- Barley, turnips and chanterelles grow just beyond the ward, so the Campfire and Farm Plot work from day one.
- Blight Boars roam outside the light by day (`HomeHunting`) and give meat, hide and bone. They never enter light, leave at nightfall and carry no Cursed Essence.

**People come when there is work for them.** Once the Woodcutter's Lodge stands, the rumour leading to Tor comes up ahead of any chosen at random; once the Smithy stands, the same goes for Bjarne (`RaidRumourDefinition.needsBuilding`, `urgent`).

**They work on their own** (`WorkerProduction`, `JobDefinition.workOrders`). Given their job, Tor keeps making planks and Bjarne ingots from what is in the stash, during raids too, and carries the results back. They leave some of each input and stop at a target, and the People tab shows what they are doing and can pause them. A worker also makes the station 50% faster. Coming home, a message says what they made.

## The balance pass

Numbers measured by launching real raids, as a raid's spawn lists decide what it holds and they replace the scene's own groups.

| Problem found | Change |
|---|---|
| The raid list held no iron, granite, hardwood, flax, moonsilver, deepstone, elderwood or silk: only events and caches gave them | All are in the list. A raid holds about 10 copper, 6 iron, 8 hardwood, 6 flax and 4 granite nodes, and a few rare ones |
| About 23 copper nodes a raid: a whole tier of metal in one trip | About 10 |
| A stone pickaxe could mine iron, so copper tools were never needed | Tool tiers are Stone I, Copper II, Iron III; copper ore needs I, iron II, moonsilver III |
| Iron ore and ingots were tier II, moonsilver III, one behind their gear | Iron III, moonsilver IV, trollstone V |
| The Forge cost Trollstone and iron, yet was the only place to make copper tools | The Smithy costs stone and planks, and comes third in the chain |
| Caches gave about 1 copper and 0.5 iron ingot each from the first raid, and moonsilver now and then | Fewer, and a cache holds no gear or material more than a tier past the Smithy (at least tier II) |
| Moonsilver smelted with Elderwood, itself rare | Hardwood fuels both iron and moonsilver (since 2026-11-06, charcoal fuels every ingot: see below) |
| Grey stone gave 1-2 a node, and everything needs it | 2-3 |
| Copper swords came from the Construction Table | Metal at the Smithy, wood at the Lodge, the rest at the Workshop |
| Bjarne could not be found at all, so nobody could be the Smith | A rumour and a site of his own |
| Runes could only be learned from Liv, found by chance | The Watch Post teaches them too |
| 20 of 26 buildings were open from the first minute | 4 are; the rest open through the chain |

**Since raid tiers ([gear score and raid tiers](GearScoreAndRaidTiers.md)):** the counts below were measured before raids had difficulty tiers, when one raid held every ore. Each ore now comes from its tier: copper from tier I, iron from II, moonsilver from III. The node counts per raid stay about the same, but the goals below now need the right tier as well.

### Expected pace (calculated from real counts, not yet playtested)

Three ore make an ingot, and what limits a trip is time and the weight of the bag (150, and ore weighs 2). A careful player reaching about 60% of the nodes:

| Per raid | Ore | Ingots |
|---|---|---|
| Copper | 6 nodes x 4 = 24, plus 1-2 ingots from caches | about 9 |
| Iron, with a copper pickaxe | 4 nodes x 3.5 = 14, plus 1-2 from caches | about 6 |
| Moonsilver, with an iron pickaxe | up to 3 nodes x 4 = 12 | up to 4 |

| Goal | Ingots | Raids |
|---|---|---|
| Copper pickaxe, Smithy II and Workshop II: enough to reach iron | about 19 copper | 2 |
| Full copper armor and a weapon | 15 copper | 2 more |
| Every tier III upgrade and an iron pickaxe | about 33 iron | 5-6 |
| Every tier IV upgrade | about 22 moonsilver | 5-6 |

These were 11, 18 and 12 ingots (1-2, 3-4 and 3-4 raids) before the balance pass below doubled building costs. Gear recipes kept their ingots.

If this feels fast or slow, change the weights of the Resources list in `BuildFoundation.RaidResources` first; the bag and the clock keep a trip honest either way.

### Wood and stone at home (balance pass, 2026-11-06, `BuildBalancePass`)

Measured, not played: a small tree takes 4 swings to fell and 2 per section, and gives about 3 oak and 2 sticks with a stone axe, so about
**13 oak a minute** with the walking. Building everything once took 444 oak (a plank is 2 oak), and a Cottage under two minutes of chopping.
The real save at 40 minutes held 50 spare oak and 60 sticks with nothing to spend them on.

**Every building and level now costs twice its original amount**, in every material: wood, stone, fiber, ingots, hide, linen and essence.
Everything once takes **888 oak**. Only trophies from named creatures keep their count (Guardian Heartroot, Trollstone, Runestone Fragment), as
doubling them would double the kills. A Light in the Wood gathers what the doubled Workshop costs (24 oak, 12 grey stone, 12 sticks).
Gifts of the Clearing is unchanged: it gathers for the stone tools, which are recipes, not buildings.

| Time to gather, at 13 oak a minute | Buildings |
|---|---|
| About a minute | Lantern Post, Banner, Planter, Small Chest, Farm Plot, Spike Trap, a Palisade section (12 oak), Hut (16), Woodcutter's Lodge (20) |
| 2 minutes | Workshop (24 oak, 12 grey stone, 12 sticks), Hunter's Lodge (24), Watch Post (28), Smithy (16 oak and 24 grey stone) |
| 3-5 minutes | Cottage (48 oak and 12 stone blocks), Storage Shed (48), Sauna (40), Kitchen (40), Tailor's Shop (48), Wainwright's Yard (56) |

The Lodge's +1 log per cut at home (tier III and V) and a better axe's extra log per section are what speed this up later.

### Charcoal: the wood sink

Valheim and Enshrouded both make wood the fuel of metal: a kiln burns it to coal, and the smelter wants coal. Here the **Woodcutter's Lodge
burns Charcoal** (3 oak for 2, or 10 sticks for 1, which gives the stick pile a use), and **every ingot takes charcoal** instead of a log: 2 for
copper, 3 for iron, 4 for moonsilver (were 1 oak, 1 hardwood, 1 hardwood). So wood use grows with the metal: a raid's 9 copper ingots burn
about 27 oak, and the ingots in every building upgrade about 770. Hardwood no longer fuels the Smithy; it still makes Hardwood Planks. Tor keeps
40 charcoal in the stash after his planks, so Bjarne's smelting doesn't wait on the player. The item's icon is a placeholder lump.

## Where the ideas come from

- **Valheim:** stations gate recipes and gear by level, and upgrades use materials from further out; gear belongs to its biome and is meant to be replaced.
- **Enshrouded:** a base upgrade extends the time you can survive outside, and rescued craftspeople unlock whole crafting tiers.
- **Gord:** you build inside the walls, resource buildings open new ways of working, and later upgrades call for clay and metal.
- **Darkest Dungeon:** expedition-only loot pays for the hamlet, so the base can't grow without going out.
- **Valheim's Rested and Hades' Heat:** comfort at home as a buff; choosing a harder run for more reward.
- **Rend and players' critiques of Raft:** cut refined materials and tiers that only add time, which is why each tier here must bring something new.
- **Enshrouded, again:** Flame levels ask for survivors with an assigned bed, and each level opens the next region. Here beds help without being asked for, and the ward opens the next raid tier.
- **Darkest Dungeon, again:** every building answers one problem, and upgrades improve cost, capacity or ceiling, which is the shape of the Hunter's Lodge yields and the Altar. The Nomad Wagon is the Trading Post's cousin.
- **Tarkov's hideout:** modules that need other modules keep unlocks coming. Its fuel and upkeep are left out.
- **Arc Raiders' Utility station:** stations that make raid tools (a zipline, a smoke grenade) are where the field kits come from.
- **V Rising:** a named creature unlocking a station gives a hunting goal without luck, as the Root Guardian does for iron tools.
- **Valheim's Rested:** comfort lengthens it, but its bonuses are percentages, which is why ours became percentages too.
- **Gord:** settlement level gates the Trading Post.
