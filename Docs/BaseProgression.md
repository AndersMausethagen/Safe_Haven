# Base progression

How Safe Haven grows, and why. The numbers live in the assets and are set by `AgentScripts/BuildBaseTiers.cs`
and `AgentScripts/BuildFoundation.cs`; running them again, in that order, restores them. Rule: **the base shows progress, and everything it gives helps you in the woods.**

## The rules

1. **Tiers mean one thing everywhere.** I Stone and Wood, II Copper, III Iron, IV Moonsilver, V Trollstone. A material's tier is the
   tier of the gear made from it, and a building's tier is the best gear it can make.
2. **Gear is made at its own tier; what gets used up comes one tier earlier** (`RecipeDefinition.RequiredStationTier`). A tier I Smithy
   smelts the copper that pays for tier II, where copper gear is made. Nothing has to be authored per recipe.
3. **Each building opens the next** (`BuildingDefinition.Requires`, read by `BuildingUnlocks`).
4. **Each ore needs the pickaxe of the tier below it**, so every metal has to be used before the next can be reached.
5. **Locked recipes stay visible** at the station, with "Needs Smithy Tier II", so the player can see what to aim for.

## The chain

```
Campfire, Torch, Lantern Post        open from the start
Workshop I                           basic gear, utilities, stone work, repairs
├─ Woodcutter's Lodge I              planks, shields, bows
│  ├─ Smithy I                       smelts copper
│  │  └─ Kitchen                     meals with lasting bonuses
│  ├─ Cauldron, Storage Shed, Palisade, Watch Post
├─ Small Chest, Farm Plot, Well, Tanning Rack (then Loom), Spike Trap, Banner, Planter
└─ Workshop II ─ Cottage             the first real comfort
```

Beehive, Sauna, Wainwright's Yard and Elder Tree still come from things found in raids; the Purification Altar from giving Ingrid a job.

| Tier | Workshop | Woodcutter's Lodge | Smithy |
|---|---|---|---|
| I | Knotted Club, Fire-Hardened Spear, Wicker Shield, Bandages (all from home materials), woven hide gear, stone and granite blocks | Oak and hardwood planks, Round Shield | Smelts copper |
| II | Fang and tusk jewellery, Fang Dagger | Hunter's Bow | Copper tools and gear, smelts iron |
| III | Wisp Pendant | Elderwood Longbow, +1 log per cut at home | Iron tools and gear, smelts moonsilver |
| IV | Faster, bigger queue and output | Faster, bigger output | Moonsilver gear |
| V | Fastest | +1 log per cut at home | Trollstone Maul |

Tier IV and V upgrades also need things only raids give (Guardian Heartroot, Trollstone, Purified Essence). There is little tier V gear
yet: the Workshop and Lodge have speed and yield there, not new things to make. That is the next content gap.

## What the base does for raids (the Raids tab in H)

| | Source | Effect |
|---|---|---|
| Reach | Heart Tree ward levels | 90 s more on every raid, per level |
| Endurance | Comfort: Cottage 4 (+2 upgraded), Sauna 3, Kitchen 2, Well, Campfire, Lantern Post, Banner, Planter 1 each | Well Rested on setting out: +20 stamina, +15 health, for 60 s per point. Each kind counts once |
| Knowledge | Watch Post tiers | Rumours before a raid, and it teaches the raid runes: Wayfinder (I), Rune of Plenty and Norn's Hourglass (II), Blood Moon, the harder raid for more loot (III) |

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
| Moonsilver smelted with Elderwood, itself rare | Hardwood fuels both iron and moonsilver |
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
| Copper pickaxe, Smithy II and Workshop II: enough to reach iron | 11 copper | 1-2 |
| Full copper armor and a weapon | 15 copper | 2 more |
| Every tier III upgrade and an iron pickaxe | 18 iron | 3-4 |
| Every tier IV upgrade | 12 moonsilver | 3-4 |

If this feels fast or slow, change the weights of the Resources list in `BuildFoundation.RaidResources` first; the bag and the clock keep a trip honest either way.

## Where the ideas come from

- **Valheim:** stations gate recipes and gear by level, and upgrades use materials from further out; gear belongs to its biome and is meant to be replaced.
- **Enshrouded:** a base upgrade extends the time you can survive outside, and rescued craftspeople unlock whole crafting tiers.
- **Gord:** you build inside the walls, resource buildings open new ways of working, and later upgrades call for clay and metal.
- **Darkest Dungeon:** expedition-only loot pays for the hamlet, so the base can't grow without going out.
- **Valheim's Rested and Hades' Heat:** comfort at home as a buff; choosing a harder run for more reward.
- **Rend and players' critiques of Raft:** cut refined materials and tiers that only add time, which is why each tier here must bring something new.
