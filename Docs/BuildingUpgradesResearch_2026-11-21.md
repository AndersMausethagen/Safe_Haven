# Building upgrades and modules: research and design (2026-11-21)

A read-only study by a systems and progression designer. Nothing in the project was changed except this file, and Unity was not
used.

**The user's words:** "Add an Upgrade tab to the buildings. This is where we can upgrade to the next tier or add modular improvements.
For example, adding a storage unit will increase the number of output slots by +1. Other modular upgrades can be reduced material cost
or even new items completely. [...] Let's start with 5 upgrades max on each building. Some upgrades can be generic and used on all
buildings, like storage unit. Some are unique to that building. Maybe upgrading to next tier will unlock additional modular slots. How
will this affect progression? Must we change some quests? Can quests be that we must find some rare material from a raid which we need
to upgrade or add modular upgrades? Implement something like this and make sure that it fits the quests and general progression."

**Read for this:** all 63 `BuildingDefinition` assets (parsed), the 5 yard roles, `BuildingDefinition.cs`, `Construction.cs`,
`StationExtensions.cs`, `ConstructionUpgradeOffer.cs`, `CraftingStation.cs`, `StationQueue.cs`, `StationCrafting.cs`, `StationWindow.cs`,
`HavenBuildings.cs`, `SettlementState.cs` (UpgradeBonus, NoteBuildings), `WantedAtHome.cs`, `RaidRumourDefinition.cs`, `QuestObjective.cs`,
`QuestDefinition.cs`, the recipes (by station and tier), every material, part, trophy and salvage item with where it drops, the raid loot
tables, spawn tables and set pieces, the 13 jobs, the quest assets of every person's chain, `HavenProgressionTests.NoUpgrade_IsOnlySpeed`,
and the docs `BaseProgression`, `GearScoreAndRaidTiers`, `QuestFlowReview_2026-11-20`, `BuildingSystemResearch` (§3.4.3), `LootTables`,
GDD §5.4, §6.4, §14, §16.5. Web research on twelve games is in §2 with sources.

**How "5" is read here:** at most **5 modules fitted at once on one building** (its slots). Each building has **its own list of at most 5**
(unique modules plus the extension pieces it already has), and the **generic** modules come on top of that list, as the user said
("generic and used on all buildings").

---

## 0. Summary

**The slot rule.** A building that takes modules has **1 slot at tier I and 1 more at each tier, never more than 5**. A Work Yard still
adds 1 (inside the cap of 5). The slot count is already data (`BuildingDefinition › Extension Slots` and `BuildingLevel › Extension Slots`),
so the rule itself is a data change. Buildings with fewer tiers top out lower (the Kitchen at 3, the Hut at 2), and they offer more
modules than slots, so there is always a choice.

**One slot pool, built on the extensions.** The new modules **build on `StationExtensions` and take over its UI role**. The nine placed
extensions (Bellows, Quench Tub, Tool Rack, Chopping Block, Log Rack, Sawhorse, Larder, Smoke Rack, Herb Garden) stay as they are. They
are "placed modules": you still set them down by the station. They now share the building's slots with the new "fitted modules", which are
paid for and hammered in from the Upgrade tab with no placing. The tab is the one place where every slot is seen and filled. Nothing is
removed. Turning the placed nine into fitted ones is left as a later choice for the user (§7).

**Five effect kinds**, which combine well and mostly exist already:

1. **Output slots** (new): one more kind of finished thing can wait. Today the output holds one kind, so a mixed queue stops at the
   first change (`StationQueue.Blocked`). This is the user's storage unit.
2. **Station** (exists): speed, queue, room in the output, and luck. Luck is the chance of one more of a good, or of a Fine piece of
   gear (the gear half is new). A module may never be speed alone.
3. **Opens recipes** (new: `RecipeDefinition › Needs Module`). This covers new items, and cheaper variants that save **home materials
   only** (charcoal, wood, stone, flax, herbs). It never touches raid ore or trophies, which set the pace.
4. **Haven** (exists: every `BuildingLevel` bonus): storage, beds, comfort, light, foretold events, logs, game, Purified Essence, renown.
   Modules never add raid time or rumours. Those stay with the portal, Sigrid and the Watch Post's tiers.
5. **Defence** (new): more health, harder shots from a building that fights, and harm turned back on the striker.

Any module can also be marked **Staffed**: it counts only while someone works there. That is the worker bonus.

**Examples (full catalogue in §3.7):**

| Generic | Unique | Rare-gated |
|---|---|---|
| **Storage Loft**: any station. +1 output slot and +5 room. 8 Rough Plank, 4 Nettle Cord | **Bloomery Hearth** (Smithy): ingots take one Charcoal less. Either it or the Bellows (the "fire" group) | **Nykk-hair Lines** (Dock): 2 Nykk's Mane from the Moss Marsh. Opens deep lines for the big char and pike |
| **Apprentice's Corner**: any workplace. Staffed: +2 queue, 5% luck | **Frame Saw** (Woodcutter's Lodge): Fine Planks from 3 Wood instead of 4 | **Brukam's Horn** (Watch Post): the Brukam's Tusk. Its shots hit 50% harder |
| **Iron Bands**: any building. +50% health in a breach | **Mason's Frame** (Workshop): stone blocks from less grey stone, when Stone Walls make stone a bulk cost | **Jeweller's Bench** (Workshop): 2 Tusse's Trinkets. Opens a Lindorm-scale Ring and a Nykk-hair Charm, the first use for those trophies |

**Progression risks:**

1. Luck on ingots shortens the metal climb. Cap a station's luck from modules and extensions at 15%.
2. Essence yield speeds up portal tiers. Altar modules add +25% at most.
3. Metal spent on modules. Keep the modules of one tier to a third of that tier's upgrade metal.
4. Slot inflation from 2 slots to 5. This is low risk, because station numbers don't set the pace.
5. **Nykk's Mane has no source today.** The Nykk and the Nøkk have loot tables but no creature in any raid.
6. Saves, co-op and raid-time views must all see the modules.

**Quests:**

- **Teach the tab once:** Bjarne's **Copper Fire** (the Smith's Request asset) adds "fit a Storage Loft at the Smithy". It is the moment
  tier II opens the second slot.
- **Small module rewards** for existing quests, at most one per quest: Tor 3 and 4, Ingrid 2 and 3, Eirik 1 and 2, Gunnar 2, Asa 1 and 2,
  Runa 2, Hild 1 and 2, Liv 5.
- **Three quests where a raid's rare material gates a module:**
  - **Bjarne 4: The Iron on the Road** (already planned): Grave Iron, for the Barrow-iron Anvil.
  - **Gunnar 4: Steel in the Water** (new): Nykk's Mane, for the Dock.
  - **Eirik 4: The Valley's Horn** (new): the Brukam's Tusk, for the Watch Post.
- **Three rumours** point to module materials and plans: the old bloomery, a tusse's nest, and pale hair in the reeds.

The rules hold:

- Every quest that asks you to fit a module teaches it when the quest starts.
- Each quest brings one new thing.
- Rescues stay gated as they are.

---

## 1. What exists today

### 1.1 The working buildings

All stations have the default output of 10 and queue of 5. "Slots" are station-extension slots as built (`StationExtensions.Slots`: the
building's own, plus its levels', plus 1 in a Work Yard).

| Building (asset) | Station, worker | Tiers and what each brings (beyond the recipes the tier opens) | Slots today | Extensions today |
|---|---|---|---|---|
| Workshop (ConstructionTable) | Workshop; Mason (Hild) | II Joined: +15% speed, +1 queue, Fine Stone Block. III Iron-Bound: +15%, Field Repair Kit; needs Lodge II. IV Master's: +20%, +1 queue, +5 output, Forward Camp Kit; 2 Guardian Heartroot, 16 Purified Essence; needs Smithy III. V Haven's Own: +25%, +1 queue, Waystone Shard; 2 Trollstone, Moss-troll Tusk, 30 PE; needs Smithy IV | 0 | – |
| Smithy (Forge) | Smithy; Smith (Bjarne) | II Copper Fire: +15%, +1 queue. III Iron Fire: +15%, +1 slot; needs Hunter's Lodge I and Tailor's Shop I. IV Moon Fire: +20%, +1 queue, +5 output; 4 Deepstone, Guardian Heartroot, 6 Grave Iron, 16 PE; needs Altar II. V Trollfire: +25%, +1 queue; 3 Trollstone, Stone-troll Knuckle, 40 PE; needs Workshop IV | 1, then 2 at III | Bellows (+20% speed, 5% luck), Quench Tub (+4 room, 3%), Tool Rack (II: +5%, +2 queue) |
| Woodcutter's Lodge (Sawbench) | Woodcutter's Lodge; Carpenter (Tor) | II Sharpened: +20%, +1 queue. III Treadle: +20%, +1 log per cut at home, +1 slot; 8 Rootling Bark, 2 Shaman's Burl. IV Waterwheel: +20%, +5 output; Guardian Heartroot; needs Smithy III. V Haven's Own: +25%, +1 log; Trollstone, Heartwood of the First Guardian, 20 PE; needs Smithy IV | 1, then 2 at III | Chopping Block (+15%, 5%), Log Rack (+6 room), Sawhorse (II: +10%, +1 queue) |
| Hunter's Lodge (TanningRack) | Hunter's Lodge; Hunter (Gunnar) | II Drying Racks: +20%, +1 queue, +1 game, better leather; 4 Wolf Fang, 2 Boar Tusk. III Smokehouse: +20%; 2 Antler, 4 Thick Hide. IV Moon Racks: +20%, +1 game; 4 Huldra Silk; needs Smithy III. V Troll Hall: +25%; 2 Trollstone, Jutul Heartstone, 20 PE; needs Smithy IV | 0 | – |
| Tailor's Shop (Loom) | Tailor's Shop; Weaver (Asa) | II Cutting Table: +30%. III Pattern Wall: +30%, +2 queue; 3 Widow Silk. IV Silk Frames: +30%; 8 Huldra Silk, 3 Bog-troll Hide; needs Smithy III | 0 | – |
| Kitchen | Kitchen; Cook (Yrsa) | II Stocked Larder: +30%, +1 queue, +1 slot; needs Farm Plot. III The Long Table: +30%, +2 queue, +1 meal at once; needs Hunter's Lodge III | 1, then 2 at II | Larder (+2 queue), Smoke Rack (+4 room, 4%), Herb Garden (+10%, 6%) |
| Cauldron | Cauldron; Rune Scholar (Liv) | II Drying Hooks: +30%; 3 Bog Venom, 4 Troll Moss. III Master's Shelf: +30%, +2 queue, +1 tonic at once; 16 PE; needs Altar II | 0 | – |
| Purification Altar | none (purifies); Alchemist (Ingrid) | II Carved: +25% speed, +25% Purified Essence. III Radiant: the same again; 2 Hag's Knot, 16 PE | 0 | – |
| Watch Post | none; Warden (Eirik) | I teaches the Wayfinder Rune. II Lookout: +1 rumour, +1 foretold, shoots in breaches, teaches 2 runes; needs a breach won. III Beacon: +1 rumour, +2 foretold, Blood Moon Rune; 3 Wisp Ember; needs Smithy II and 2 breaches won | 0 | – |
| Storage Shed | none | II Sturdy: +6 stash slots. III Grand: +10 | 0 | – |
| Farm Plot | Farm; Farmer (Asa) | II Tended: +30%, +1 queue | 0 | – |
| Dock | Dock; Fisher | II Long Pier: +30%, +1 queue, deep lines and pike traps; 2 Bog-troll Hide | 0 | – |
| Wainwright's Yard | Wainwright | II Sturdy: +30%, +1 queue | 0 | – |
| Campfire | Campfire | II Stone Hearth: +25%, +4 m light | 0 | – |
| Hut, Cottage | none | Hut II Thatched: +1 comfort, +1 bed. Cottage II Whitewashed: +2 comfort, +2 beds | 0 | – |
| Small Chest | none | II Bound: +4 stash slots | 0 | – |

**With no tiers:** Stable, Sauna, Well, Trading Post, Notice Board, Beehive (a station), Elder Tree, Runic Ward Totem, Haven Banner. Also
the traps, walls, gates, paths and decorations. The old Furnace is hidden from the menu. The portal has its own tiers and improvements
(Steadied, Bound, Moonlit), and it stays outside this system as the spine.

### 1.2 How it works in code (what the modules can reuse)

- **One sum feeds almost everything.** `Construction.Sum(level => ...)` adds a bonus over the tiers reached. It is read for:
  - a station's speed, queue and room (`CraftingStation`);
  - beds (`BuildingObject`, `SettlementState`);
  - stash slots (`StashChest`);
  - comfort (`HavenReadiness`);
  - renown (`HavenRenown`);
  - the altar's speed and yield (`PurificationAltar`);
  - light (`Construction` itself);
  - extension slots (`StationExtensions`);
  - raid seconds, rumours and foretold events, through `HavenReadiness.BestOfEachKind`.

  Raids, which can't see the scene, read `SettlementState.UpgradeBonus` instead, for game and meal and tonic slots.
  **If fitted modules join this sum, about a dozen systems pick them up with no further change.**
- **Extensions** (`StationExtensions`):
  - A placed extension building counts for the nearest station of its kind within 6 m, or anywhere in the same Work Yard.
  - Each kind counts once, up to the station's slots.
  - Slots go first come, first served.
  - The bonus is `ExtensionBonus`: speed, queue, room, luck.
  - Luck goes to the station's queue (`StationQueue.ExtraChance`), where workers use it during raids too.
- **The output holds one kind.** "The slot holds one kind of thing, up to Capacity. When the result has nowhere to go the station waits
  (Blocked)." Queue a sword and then a pickaxe, and the Smithy stops after the sword until you come back.
- **Upgrades:**
  - The station window's "Upgrade to Tier N…" button and the build menu's Manage › "Upgrade or repair" both open
    `ConstructionUpgradeOffer`, a text page in the action menu.
  - Paying puts the scaffold up. The player hammers it (`Construction.Target`, `Work`, `Paid`), and the work is saved as
    `"Level,Target,Work,Paid"`.
- **Recipes:**
  - The station's tier gates recipes automatically (`RecipeDefinition.RequiredStationTier`: gear at its own tier, materials one tier
    earlier). Locked recipes stay visible with "Needs Smithy Tier II".
  - Costs are paid in `StationCrafting.TryQueue` and given back on cancel.
- **Teaching:**
  - A quest teaches buildings when it starts (`QuestDefinition › Unlocks Buildings`, built for the quest-flow review).
  - Items teach buildings and recipes when read at home (`ItemDefinition › Teaches Buildings/Recipes`).
  - Creatures teach recipes on a kill (the Root Guardian teaches the Iron Hatchet).
- **Rumours** can wait for a building at a tier (`needsBuilding`/`needsTier`) or run while a quest is under way (`whileQuest`). They can
  ask for a set piece and be urgent, and their site can hand out loot (`GiveLootOutcome`).
- **Track:** `WantedAtHome` tracks up to 3 building levels. Their materials are counted on pickups in raids and listed in the briefing.
- **The rule for upgrades** (BaseProgression; `HavenProgressionTests.NoUpgrade_IsOnlySpeed`): every upgrade must open something new to
  make, take a chore off the player, or answer a named raid problem. Speed, queue and output may come with a level, but are never the
  reason to buy it.

### 1.3 What is weak today, and what modules can fix

1. **A mixed queue stops at the first change of kind.** The user's storage unit is exactly the fix.
2. **The extensions became numbers only.** All nine give speed, queue, room or luck. The design in BuildingSystemResearch §3.4.3 asked
   for things that change play:
   - a Bellows so ingots don't wait on charcoal;
   - a Sawhorse that makes planks cheaper;
   - a Smoke Rack so meals last longer;
   - a Quench Tub that makes repairs cheaper.

   Fitted modules can deliver these as the Bloomery Hearth, the Frame Saw and the Jutul Cold Store.
3. **Only three buildings take extensions,** with 2 slots at most (3 in a Work Yard).
4. **The upgrade is a text page.** Nothing shows a building's whole state: its tier, its slots, and what is fitted.
5. **Raid trophies with no use at home:**
   - Lindorm Scale, Nykk's Mane, Brukam's Tusk and Heart of the Forest are used by nothing.
   - Tusse's Trinket is only salvage.
   - Mistwing Feathers only go into arrows, though their own text says "in a quilt they keep the cold out".
   - **Nykk's Mane can't be had at all:** the Nykk and the Nøkk of the Black Pool have loot tables, but no creature prefab exists in
     any raid.
6. **Buildings with no tiers can't grow:** the Stable, Sauna, Well and Trading Post. Their single slot gives them something, through the
   generic modules.

---

## 2. How comparable games do it

Web research, 2026-11-21. The Valheim and V Rising Fandom wikis and the Against the Storm wiki refused automated reads, so guides, other
wikis and patch notes were used. "Unverified" marks a figure seen in only one weak source.

| Game | How upgrades attach | Slots and how they open | Generic or unique | Rare gate | Notes |
|---|---|---|---|---|---|
| **Valheim** [1][2][3] | Pieces built near the station, each a different kind; a second copy adds nothing | Workbench: 4 pieces (levels 1-5). Forge: 6 (1-7). Black Forge: 5. Galdr table: 4. Cauldron: 6 | Unique per station. They give **no speed**: they open recipes and quality levels only | Each piece uses the next biome's materials: Chain (Swamp) for the Bellows, Bloodstone and Morgen Sinew (Ashlands) for the Gem Cutter. The Artisan Press needs 1 boss carapace | Range 2-5 m (unverified). The closest analogue to our extensions |
| **Enshrouded** [4]-[10] | Upgrade stations placed by an NPC's crafter | 20 upgrades gate 574 of 2,542 recipes. The Table Saw alone opens 148 | Unique, and they open recipes in batches | **A quest per upgrade:** find one key part (Circular Sawblades, Scientific Instruments, a Crucible, Bellows), then it can be crafted again. The Flame Altar needs a boss trophy from level 3, and Spark (1, 5, 10 ... 60 a level) | Survivors with beds are required per Flame level |
| **Palworld** [11]-[15] | Better buildings replace the old (Primitive → Improved → Electric Furnace) | Base level 1-35 from "build X" objectives; bases 1→4 | Shared tech tree | Ancient Technology points: 1 per Alpha, 5 per Tower Boss; Ancient Parts 1-20 per Alpha kill; Lifmunk Effigies (world finds) | Effigy upgrades are small %s, up to 20 levels |
| **V Rising** [16]-[19] | Room floors: an enclosed room with one floor gives its stations **+25% speed and 25% lower cost** | Castle Heart levels 1-5 raise the floor area and servant count | Floors are generic per station type | V Blood bosses unlock stations and recipes. Castle Heart III-V need 1 Greater or Primal Blood Essence: the boss unlocks the recipe, then it is crafted from common inputs | A discount by room, not by item |
| **Medieval Dynasty / Going Medieval** [20][21] | Tech points from work, or room types | – | Going Medieval: a Workshop room +20% speed | – | Rooms as modifiers (V Rising's idea again) |
| **Frostpunk 2** [22] | District expansions | Each expansion of 3 tiles opens a building slot, up to 2 a district | – | Research (the Idea Tree) | Slots opened by growth, not by buying |
| **Against the Storm** [23]-[25] | An "upgrade tab" on houses: 2 levels, **pick 1 of 2 at each** | 2 picks | Level 1 generic, level 2 a species perk | Building-material packs | The developers **tried upgrades on most buildings and dropped them as complexity without benefit**. In meta-progression they split "new content" from "power-ups", because the +2% steps "didn't feel like upgrades" |
| **Gord** [26] | Upgrades cost clay and iron from outside the walls | Buildings go to level 3 | – | – | Costs per level unverified |
| **RuneScape: Dragonwilds** [27] | "Improvements" placed within 9 m add an **input slot** to one station | +1 slot each | Unique | Learned the **first time you get** the gating material (Iron Bar, Bronze Bar): no quest, no grind | The nearest station wins |
| **Factorio** [28] | Module items in machine slots | 0, 2, 4 slots by machine tier; up to 8 on late machines | Generic | Higher module tiers need rare intermediates | **Trade-offs:** productivity +4/6/10% costs speed and energy, and is **banned on final products**. Beacons have diminishing returns (÷√n) |
| **Satisfactory** [29] | Power Shards (overclock) and Somersloops | 3 shard slots (+50% each, to 250%); 1, 2 or 4 Somersloop slots by building size, a full set doubles output | Generic | **Finite world finds:** 106 Somersloops; 2,650 shards' worth of Power Slugs | Power cost grows faster than speed: a full amplifier costs ×4 |
| **Kingdom Come 1 (Pribyslavitz)** [30] | Buy a forge (8,000 groschen), then pick **one of two exclusive tool sets** at 2,000 each | 1 pick | Unique: each opens one item (the Tusk, a shield) | A quest brings a skilled labourer | Exclusive choices made concrete |
| **Design writing** [31]-[33] | Larian: "we do big numbers because that's cool", +4.5% fire damage is "not cool". Bycer: scaling upgrades don't change *what* the player does. Sid Meier: an interesting decision is a trade-off that depends on the situation | | | | |

### 2.1 What we take, and what we leave

| Lesson | Where it comes from | In Safe Haven |
|---|---|---|
| Slots are few and open with the building's tier | Factorio (0, 2, 4), Frostpunk 2, Valheim (4-6 pieces) | 1 slot a tier, at most 5 |
| Unique modules carry progression; generic ones carry optimisation | Valheim and Enshrouded (unique, recipe-opening); Factorio and V Rising (generic numbers) | Every building's own list opens something or removes a chore. Generics are few (5) |
| A rare part is needed **once, one or two of it**; bulk comes from common materials | Valheim (1 Carapace, 1 Bloodstone), Enshrouded (one quest part), V Rising (1 Primal Essence) | Every rare gate here is 1-3 of one item |
| A quest finds the key part; after that it can be made again | Enshrouded (Sawblades, Crucible) | The quest teaches the module. The rare part is the cost of each copy |
| Taught when you first have the material | Dragonwilds | Modules that fit are always listed, with "Needs 1 Lindorm Scale" until you have one (our "locked recipes stay visible") |
| Trade-offs make generic upgrades interesting | Factorio, Satisfactory, KCD (exclusive tools), ATS (1 of 2) | Exclusive groups, used sparingly: the Smithy's "fire" (Bellows or Bloomery Hearth) |
| Productivity never on final products | Factorio | Luck never adds more ingots past a cap. Cost variants never touch raid ore |
| A duplicate adds nothing | Valheim | Each kind once per building (already the extension rule) |
| Don't do +2% steps | ATS, Larian | No module is a small percentage alone. Speed is never the reason (the existing test) |
| ATS dropped per-building upgrades as complexity | Eremite | Modules only on buildings that do work (about 15), not on fences, chests or decorations |
| Rooms and floors as the modifier | V Rising, Going Medieval | Not now: our yards already play this role (Work Yard +1 slot, +10% speed) |

---

## 3. Design

### 3.1 Decisions

1. **A slot pool per building**, shared by placed extensions and fitted modules. `StationExtensions.Slots` becomes the building's slots
   for both, capped at 5.
2. **Fitted modules are installed from the Upgrade tab.** They are paid for there and hammered at the building's scaffold, like a tier.
   Placed extensions are still set down by hand. The tab shows them in their slots, and its "Add" on one of them hands over to the
   build placer with the ghost, which already shows the station's slots.
3. **Tiers open; modules shape.** A tier keeps what it gives today and opens one more slot. Modules make the building what the player
   wants it to be.
4. **Modules only on buildings that do work** (§3.7). Fences, walls, gates, paths, traps, chests and decorations get none.
5. **Removing a module never loses it.** It goes to the Haven's store as a spare. Fitting a spare costs only the hammering, and it can
   go on another building of a kind it fits. No refunds to work out, and a rare trophy can't be lost to a misclick (§3.5).

### 3.2 Slots

| Tier | I | II | III | IV | V |
|---|---|---|---|---|---|
| Slots | 1 | 2 | 3 | 4 | 5 |

- **Work Yard:** +1 when the building stands in one, as now, still at most 5.
- **Data:**
  - Set `extensionSlots = 1` on every building that takes modules, and `extensionSlots = 1` on each of its levels.
  - This replaces today's Forge III, Lodge III and Kitchen II single extra slots.
  - The cap is one number (`ModuleSettings.MostSlots = 5`).
- **Where buildings top out:**
  - Workshop, Smithy, Lodge, Hunter's Lodge: 5.
  - Tailor's Shop: 4.
  - Kitchen, Cauldron, Altar, Watch Post, Storage Shed: 3.
  - Farm Plot, Dock, Yard, Campfire, Hut, Cottage: 2.
  - Buildings with no tiers: 1.
- **A locked slot says what opens it:** "Tier III opens this slot", "A Work Yard adds one".

### 3.3 Effect kinds

| # | Kind | Fields | New or existing | Used by |
|---|---|---|---|---|
| 1 | **Output slots** | `extraOutputSlots` | New: `StationQueue` keeps a short list of output slots, each holding one kind up to the station's room | Storage Loft |
| 2 | **Station** | `craftSpeed`, `extraQueue`, `extraOutput` (room), `extraChance` (**luck**) | Existing (`BuildingLevel`, `ExtensionBonus`). Luck on gear means a chance of a Fine piece (new hook in `CraftedWork.QualityChance`) | Apprentice's Corner, Scarecrow, Rune Shelf, Barrow-iron Anvil, Widow-silk Lacing, the placed nine |
| 3 | **Opens recipes** | `RecipeDefinition › Needs Module` (+ `Replaces` for a cheaper variant) | New | New items; thrifty variants (Bloomery Hearth, Frame Saw, Mason's Frame, Spinning Wheel, Herb Rack); pairs (Kit Bench) |
| 4 | **Haven** | Every `BuildingLevel` bonus: `extraStorage`, `beds`, `comfort`, `extraLight`, `foretoldEvents`, `extraLogs`, `extraGame`, `purifyYield`, `extraMealSlots`, `extraTonicSlots`, `renown` | Existing | Loft Shelves, Box Beds, Quilts, Wisp Lantern, Map Table, Elder Graft, Antler Wall, altar modules |
| 5 | **Defence** | `healthShare`, `fightShare` (a building that fights hits harder), `turnBackShare` (part of each blow comes back on the striker) | New, three small hooks: `Damageable` max health, `WatchPostArcher` damage, damage taken | Iron Bands, Hag's Knot, Brukam's Horn |
| + | **Staffed** | `staffed` | New flag: the module counts only while a resident works here | Apprentice's Corner, Runestone Ring |

**Why cost reduction is a recipe variant, not a percentage:**

- **Small recipes:** gear recipes are small (a Copper Sword is 3 ingots), so 20% off rounds to nothing.
- **A refund exploit:** a discount read at the time of paying lets the player queue cheaply, remove the module and cancel for a full
  refund. A variant recipe gives back exactly what it took.
- **Pacing:** ore and trophies set the pace (BaseProgression's ingots per raid), so only home materials get cheaper.
- **How it looks:** the browser shows only the better variant when both are open, and workers use the open one.

**Rules that keep numbers honest:**

- No module is speed alone (`NoUpgrade_IsOnlySpeed` extended to modules).
- A station's luck from modules and extensions is capped at 15% (`ModuleSettings.MostLuck`). Workers' ranks come on top, as now.
- Modules never add raid time or rumours.
- For anything raids read, the best building of a kind counts, as for tiers.

### 3.4 Data model

```
BuildingModuleDefinition : ScriptableObject        (Data/Buildings/Modules/*.asset)
  id, displayName, description, icon
  fits            List<BuildingDefinition>          its own buildings (empty for a generic)
  fitsAny         None | AnyStation | AnyWorkplace | AnyLit | AnyBreakable | AnyBuilding
  tier            1-5                               the building's tier it needs
  group           string                            one module of a group per building ("fire")
  staffed         bool
  bonus           BuildingBonus                     kinds 1, 2, 4 and 5 (the same block a tier uses)
  cost            List<ItemAmount>,  essence  List<ResourceAmount>,  work  float (seconds of hammering)
  requires        List<BuildingRequirement>         rarely: another building first
  knownFromStart  bool
  dressing        string                            a child part on the prefab shown while fitted ("Module Storage Loft"); none: no visual
```

- **`BuildingBonus`** is today's bonus fields of `BuildingLevel` moved into a `[Serializable]` base class, with
  `BuildingLevel : BuildingBonus`.
  - Unity serializes a base class's fields in place, so existing tier data keeps its values. Check one asset with a before/after diff,
    per the standing rule.
  - New fields: `extraChance`, `extraOutputSlots`, `healthShare`, `fightShare`, `turnBackShare`, `unlocksModules`.
  - `Construction.Sum` and `SettlementState.UpgradeBonus` take a `Func<BuildingBonus, …>`, so every existing call compiles as it is.
- **Placed modules** stay `BuildingDefinition`s with `extends`. They gain only `extensionGroup` (so the Bellows can join "fire").
- **Recipes:** `Needs Module` (the station must have it fitted, checked in `StationCrafting.Check` like the tier). A locked recipe
  shows "Needs a Barrow-iron Anvil". `Replaces` points a thrifty variant at the recipe it beats.
- **Teaching:**
  - `QuestDefinition › Unlocks Modules`: learned when the quest starts, the same rule as Unlocks Buildings.
  - `BuildingLevel › Unlocks Modules`: learned when the tier is finished.
  - `ItemDefinition › Teaches Modules`: a plan read at home.
  - What the player knows is kept with the known buildings (`PlayerCrafting`, `CharacterProgress`).
- **A quest objective:** `ObjectiveType.FitModule` (23), for a module, or any, on a building, or any. `Count Already Owned` counts one
  already fitted. It is counted from a new `GameEventKind.ModuleFitted`. `QuestReachability.AskedBeforeTaught` learns that a quest's own
  Unlocks Modules teaches it.

### 3.5 Fitting, removing, saving

- **Fitting** pays the cost (`MaterialPool.For(player)`, from the bag and then the stash, as everything at home). It then puts the
  scaffold up for `work` seconds (3-8), and the player hammers.
  - It is the same job a tier uses: `Construction` gains `Fitting`, the module being hammered.
  - Only one job runs per building at a time: a tier or a module.
  - The station keeps working meanwhile, as during an upgrade.
  - The Construction skill trains on it.
  - In a friend's Haven it waits for the host's yes (`HavenVisit.BuildingNeedsAsking`), as upgrades do.
- **Removing** is instant. The module goes to the Haven's store as a spare (`SettlementState.SpareModules`, id → count), and the tab
  shows "In store: 1 Bellows… Fit for free". A placed module is removed by picking it up or demolishing it, as now.
- **Saving:**
  - `Construction` saves `"Level,Target,Work,Paid,<fitted ids joined by |>,<fitting id>"`. An old save has 4 parts and loads with no
    modules.
  - Spares and known modules are saved with the settlement and the character.
  - The output slots of a `StationQueue` are saved as a list. An old single output becomes slot 1.
- **Raids:** `SettlementState.NoteBuildings` also notes each kind's fitted modules, so `UpgradeBonus` adds them where raids can't see
  the scene.
- **Co-op:** `Construction`'s shared mark includes the modules. A message format bump is checked by `CoopMessageTests`.

### 3.6 The Upgrade tab

**Where it opens:**

- **A station's window gets two tabs, Craft and Upgrade.** It changes with LB and RB through `PadTabs`, as the H window does.
- **Other buildings** open the same panel in its own window:
  - from the build menu's Manage › "Upgrade or repair" (picking a building);
  - from H › Buildings (its "Upgrade…" button).
- The panel replaces `ConstructionUpgradeOffer`'s text page in all three places.
- It is built from the UI kit and its tokens (the Hearthline cards every window uses now).

```
 [ Craft ] [ Upgrade ]                                    Copper Fire Smithy  ·  Tier II of V
 ───────────────────────────────────────────────────────────────────────────────────────────
  NEXT TIER   Iron Fire (III)
   Makes Iron Sword, Iron Mail Hauberk and more. Opens Iron Trap. One more module slot.
   Needs Hunter's Lodge I (standing), Tailor's Shop I (not built)
   Iron Ingot 9/16   Granite Block 12/12   Hardwood Plank 8/8                [Upgrade]  [Track]
 ───────────────────────────────────────────────────────────────────────────────────────────
  MODULES  2 of 2   (Tier III opens a third. A Work Yard adds one.)
  [ Bellows      ] [ Storage Loft ] [  Tier III   ] [  Tier IV  ] [  Tier V  ]
  [ placed       ] [ +1 output    ] [  locked     ] [  locked   ] [  locked  ]
  [ by the forge ] [   (Remove)   ]
 ───────────────────────────────────────────────────────────────────────────────────────────
  FITS HERE
   Apprentice's Corner   While someone works here: +2 queue, 5% luck
                         Fine Plank 10/10  Copper Ingot 3/3  Linen 0/2              [Fit]  [Track]
   Bloomery Hearth       Ingots take one Charcoal less. Instead of the Bellows (fire)    Tier III
   Hag's Knot            +25% health; a quarter of each blow comes back    Needs 1 Hag's Knot (never had one)
   In store: Quench Tub ×1                                                              [Fit, free]
```

- **The top card** is the next tier, ordered as `HavenBuildings.WhatTierBrings` already does: what it makes and opens first, then
  "one more module slot", then its other bonuses. After that come what it needs, and each material against what is home, with the
  missing ones in red.
- **The slot row** has at most 5 cards. A filled card shows the module, a short effect and Remove. A placed card says where it stands.
  A locked card says what opens it.
- **The list below** holds the modules known that fit, with cost and Fit. It also shows:
  - modules that fit and are taught but whose rare material was never held, with "Needs 1 Lindorm Scale";
  - a group conflict ("Instead of the Bellows").

  Modules not yet taught are not shown, so the list grows with the story, and "asked before taught" can't happen.
- **Track** works for a module as for a level (`WantedAtHome`, still 3 at most). Its rare material then shows on pickups in raids and in
  the briefing: "Lindorm Scale 0/1 for the Jeweller's Bench".
- **Controller:** LB and RB switch tabs. The stick moves along the slots and the list. A fits, X removes, Y tracks.

### 3.7 The catalogue

"Rare" is a raid-only part or trophy. "Exists" means the item is in the project today. **NEW** marks content to make. Costs follow the
tier currencies in BaseProgression: tier I is wood, stone and nettle; tier II is copper; tier III is iron; tier IV is moonsilver.

#### Generic (fit many buildings)

| Module | Fits | Tier | Cost (work) | Effect | Rare, and where it comes from | Taught by |
|---|---|---|---|---|---|---|
| **Storage Loft** | any crafting station | I | 8 Rough Plank, 4 Nettle Cord (4 s) | **+1 output slot** (a second kind can wait), +5 room | – | Copper Fire (Bjarne 3) |
| **Apprentice's Corner** | a station that is someone's workplace | II | 10 Fine Plank, 3 Copper Ingot, 2 Linen (5 s) | Staffed: +2 queue, 5% luck | – | Copper Fire |
| **Wisp Lantern** | any building with a light (the wall torches of the big buildings, Campfire, Watch Post) | II | 1 Wisp Ember, 4 Fine Plank, 1 Copper Ingot (4 s) | +5 m light against the night | Wisp Ember (exists): Marsh Light, Misty Woods any tier, 60%; Fog Bank Cache. "A cold spark that won't go out" | The Watch (Eirik 1) |
| **Hag's Knot** | any building | II | 1 Hag's Knot, 2 Nettle Cord (3 s) | Defence: +25% health; a quarter of each blow comes back on the striker | Hag's Knot (exists): Trollkjerring, Troll Valley II+ and the trolls' places in the Misty Woods, 60%. "Hung over a door, it turns harm back the way it came" | Hold the Line (Eirik 2) |
| **Iron Bands** | any building that can be broken in a breach | III | 1 Iron Ingot, 6 Hardwood Plank, 4 Nettle Cord (5 s) | Defence: +50% health | – | Smithy III (Iron Fire) |

#### Placed modules (the extensions as they are, now in the tab)

| Building | Module | Tier | Effect today | Change |
|---|---|---|---|---|
| Smithy | Bellows | I | +20% speed, 5% luck | Joins the group **fire** with the Bloomery Hearth |
| Smithy | Quench Tub | I | +4 room, 3% luck | – |
| Smithy | Tool Rack | II | +5% speed, +2 queue | – |
| Woodcutter's Lodge | Chopping Block, Log Rack, Sawhorse | I, I, II | +15% speed and 5% luck; +6 room; +10% speed and +1 queue | – |
| Kitchen | Larder, Smoke Rack, Herb Garden | I | +2 queue; +4 room and 4% luck; +10% speed and 6% luck | – |

#### Each building's own (fitted)

**Workshop** (5 slots at V; Hild the Mason works here)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Mason's Frame | II | 8 Rough Stone Block, 6 Fine Plank, 2 Copper Ingot | Thrifty blocks: Rough Stone Block from 2 Grey Stone (was 3), Fine Stone Block from 3 (was 4), two a craft | – | Granite (Hild 2), when Stone Walls make stone a bulk cost (8 Fine Stone Block a section) |
| Kit Bench | III | 6 Hardwood Plank, 4 Linen, 1 Iron Ingot | Field Repair Kit and Forward Camp Kit made two at a time for 1.5 times the cost | – | Workshop III (Iron-Bound) |
| Jeweller's Bench | III | 2 Iron Ingot, 4 Hardwood Plank, 2 Tusse's Trinket | Opens **Lindorm-scale Ring** and **Nykk-hair Charm** (NEW) | Tusse's Trinket (exists): Tusse, Misty Woods and Troll Valley, 30%; Troll Hoard and Brukam's Hoard, 2-3 | **Jeweller's Loupe** (NEW plan item) from the rumour "A tusse's nest" (§5.4) |

**Smithy** (5 slots; with the Bellows, Quench Tub and Tool Rack its own list is 5)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Bloomery Hearth | III, group fire | 12 Rough Stone Block, 8 Grey Stone, 4 Copper Ingot | Thrifty ingots: one Charcoal less each (copper 1, iron 2, moonsilver 3). Either this or the Bellows: thriftier or faster | – | **Bloomer's Notes** (NEW plan item) from the rumour "The old bloomery" at the Bog-Iron Bloomery (Moss Marsh) |
| Barrow-iron Anvil | III | 3 Grave Iron, 3 Iron Ingot, 4 Granite Block | Gear made here is Fine 10% more often. Opens **Grave Bodkins** (NEW arrows that chill: 2 Grave Iron, 2 Hardwood Plank, 4 Mistwing Feathers for 10) | Grave Iron (exists): Draug, Misty Woods III+ and Moss Marsh, 70%; Barrow Chest; the Old Barrow event. "It holds an edge and a chill" | **The Iron on the Road** (Bjarne 4) |

**Woodcutter's Lodge** (5 slots; with its three placed pieces its own list is 5)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Frame Saw | II | 6 Fine Plank, 3 Copper Ingot, 2 Nettle Cord | Thrifty Fine Planks: 3 Wood for 2 (was 4) | – | Heavy Loads (Tor 4) |
| Elder Graft | III | 1 Guardian Heartroot, 4 Hardwood Plank | +1 log from each section of a tree cut at home | Guardian Heartroot (exists): Root Guardian, Misty Woods II+, 100% | Lodge III (Treadle) |

**Hunter's Lodge** (5 slots; Gunnar)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Fletcher's Jig | I | 6 Rough Plank, 4 Mistwing Feathers, 2 Nettle Cord | Arrows made here in bigger bundles: Crude Stone and Whistling Arrows, half again as many per craft as by hand | Mistwing Feathers (exists): Mistwing, Misty Woods any tier, 100%; Hunter's Cache | Cured (Gunnar 2) |
| Antler Wall | II | 3 Antler, 4 Fine Plank | +1 game at home, +4 renown | Antler (exists): Stag, at home and in raids, 70% | Hunter's Lodge II (Drying Racks) |

**Tailor's Shop** (4 slots; Asa)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Spinning Wheel | I | 8 Rough Plank, 4 Nettle Cord, 2 Bone | Thrifty Linen: 2 Flax (was 3). Flax is grown at home too | – | Flax and Linen (Asa 2) |
| Widow-silk Lacing | III | 2 Widow Silk, 4 Hardwood Plank | Leather gear made here is Fine 10% more often | Widow Silk (exists): Black Widow, Misty Woods II+, Troll Valley, Moss Marsh, 60% | Tailor's Shop III (Pattern Wall) |

**Kitchen** (3 slots; Yrsa; with its three placed pieces its own list is 4)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Jutul Cold Store | III | 1 Jutul Heartstone, 6 Granite Block, 4 Fine Plank | Opens **Frost-cured Ham** (NEW meal whose bonus lasts twice as long) | Jutul Heartstone (exists): the Berg-jutul, Troll Valley II+ (placed), 100%. "It is cold" | The Jarl's Table (Yrsa 3) |

**Cauldron** (3 slots; Liv, Ingrid's craft)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Herb Rack | I | 6 Rough Plank, 2 Nettle Cord | Thrifty Healing Draught: 1 Healing Herb (was 2) | – | A Draught for the Road (Ingrid 3) |
| Rune Shelf | II | 6 Fine Plank, 4 Bone, 2 Linen | 10% luck: a rune or draught more now and then | – | A Hand for the Shelf (Liv 5) |
| Burl Still | II | 1 Shaman's Burl, 4 Copper Ingot, 2 Fine Plank | Opens **Burl Draught** (NEW: health grows back slowly for two minutes) | Shaman's Burl (exists): Moss Shaman, Misty Woods II+ and Troll Valley, 60% | Cauldron II (Drying Hooks) |

**Purification Altar** (3 slots; Ingrid)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Runestone Ring | II | 1 Runestone Fragment, 8 Granite Block | Staffed: +10% Purified Essence | Runestone Fragment (exists): Barrow Chest, Fog Bank Cache, Supply Cache, the spirits | Cleansing the Curse (Ingrid 2) |
| Living Heart | III | 1 Heart of the Forest | +15% Purified Essence, +1 comfort | Heart of the Forest (exists, unused today): 2% in Misty Woods finds; Troll and Brukam's Hoards. **Optional, a lucky find** | The item itself ("use it at the altar") |

**Watch Post** (3 slots; Eirik)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Map Table | II | 6 Fine Plank, 2 Linen, 1 Runestone Fragment | +1 foretold event in the briefing | As above | Read the Ground (Runa 2) |
| Brukam's Horn | III | Brukam's Tusk, 2 Iron Ingot | Defence: the post's shots hit 50% harder; +6 renown | Brukam's Tusk (exists, unused today): the Brukam, once per kill | **The Valley's Horn** (Eirik 4, new) |

**Storage Shed** (3 slots)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Loft Shelves | I | 12 Rough Plank, 4 Nettle Cord | +6 stash slots | – | A Growing Home (Tor 3) |
| Bog-hide Roof | II | 3 Bog-troll Hide, 6 Fine Plank | +8 stash slots | Bog-troll Hide (exists): Bog Troll, all three raids, 70% | Storage Shed II |

**Homes: Hut and Cottage** (2 slots)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Mistwing Quilts | I | 6 Mistwing Feathers, 2 Linen | +1 comfort (each kind counts once, as now) | Mistwing Feathers; "in a quilt they keep the cold out" | Stone on Stone (Hild 1) |
| Box Beds | II (Cottage) | 8 Fine Plank, 4 Linen | +1 bed | – | Cottage II |

**Dock** (2 slots)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Nykk-hair Lines | II | 2 Nykk's Mane, 4 Fine Plank | Opens **Nykk-hair Deep Lines** (NEW Dock recipe: the big char and pike) | Nykk's Mane (exists, **no source today**): the rumour site in §5.4 gives 2 for the quest; add it to the Moss Marsh's own finds at about 15% | **Steel in the Water** (Gunnar 4, new) |

**Farm Plot** (2 slots)

| Module | Tier | Cost | Effect | Rare and source | Taught by |
|---|---|---|---|---|---|
| Scarecrow | I | 6 Sticks, 2 Linen, 2 Barley | 10% luck: one more of a crop now and then | – | Good Dirt (Asa 1) |

**Generics only:** Campfire, Wainwright's Yard, Beehive, Stable, Sauna, Well, Trading Post. Each has 1-2 slots. Their own modules wait
until their systems want one: the Stable's horses, or the Trading Post's stock. "Nykk's Halter" for the Stable is the obvious next one,
from the folk tale of a nøkk caught with a halter that ploughed all spring.

**Count:** 5 generic, 9 placed (existing), 25 own fitted.

**New content:**

- **5 items, icon-only:** Grave Bodkin, Lindorm-scale Ring, Nykk-hair Charm, Frost-cured Ham, Burl Draught.
- **2 plan items:** Bloomer's Notes, Jeweller's Loupe.
- **About 18 recipes:**
  - the new items above;
  - 8 thrifty variants;
  - 2 kit pairs;
  - 2 arrow bundles;
  - the Nykk-hair Deep Lines.

### 3.8 Rare materials: who uses what

| Material | Used today by | Modules add | Notes |
|---|---|---|---|
| Lindorm Scale | nothing | Lindorm-scale Ring (Jeweller's Bench) | Moss Marsh IV elites, 25% |
| Nykk's Mane | nothing | Nykk-hair Lines, Nykk-hair Charm | **No source. Must get one** |
| Brukam's Tusk | nothing | Brukam's Horn | Once per Brukam kill (rematch after 24 h) |
| Heart of the Forest | nothing | Living Heart | 2%: optional only |
| Tusse's Trinket | salvage | Jeweller's Bench (2) | |
| Mistwing Feathers | 2 arrow recipes | Fletcher's Jig, Quilts, Grave Bodkins | Common, 100% from Mistwings |
| Grave Iron | Smithy IV (6), Grave Blade | Barrow-iron Anvil (3), Grave Bodkins | Some competition, which is intended |
| Widow Silk | Tailor III (3) | Widow-silk Lacing (2) | |
| Hag's Knot | Altar III (2), Ward Totem (1) | Hag's Knot (1 a building) | |
| Runestone Fragment | Ward Totem (1) | Map Table, Runestone Ring (1 each) | Caches only. Kept to two modules |
| Jutul Heartstone | Hunter's V (1) | Jutul Cold Store (1) | 100% from a placed Berg-jutul |
| Shaman's Burl | Lodge III (2), Runic Staff | Burl Still (1) | |
| Guardian Heartroot | Workshop IV (2), Smithy IV, Lodge IV, 2 recipes | Elder Graft (1) | 100% from Root Guardians |

---

## 4. Progression impact

### 4.1 Where modules land

Rescues come about one per raid, so "home N" is about raid N. Raid tiers come from BaseProgression's pace: copper takes about 4 raids,
iron 5-6 more, and moonsilver 5-6 more.

| When | Raids at | What can be fitted | Comes through |
|---|---|---|---|
| Homes 1-4 | Tier I | Storage Loft, Apprentice's Corner; Loft Shelves; Frame Saw; Runestone Ring; Herb Rack; Wisp Lantern. Placed: Bellows, Quench Tub, Chopping Block, Log Rack | Bjarne 3, Tor 3 and 4, Ingrid 2 and 3, Eirik 1 |
| Homes 4-8 | Tier II | Hag's Knot; the Kitchen's placed three; Fletcher's Jig; Antler Wall; Burl Still; Bog-hide Roof; Scarecrow; Spinning Wheel. Placed: Tool Rack, Sawhorse | Eirik 2, Yrsa, Gunnar 2, tiers II, Asa 1 and 2 |
| Homes 8-12 | Tier III | Iron Bands; Kit Bench; Elder Graft; Widow-silk Lacing; Barrow-iron Anvil; Map Table; Quilts; Mason's Frame; Box Beds; Jutul Cold Store; Jeweller's Bench; Bloomery Hearth; Brukam's Horn | Tiers III, Bjarne 4, Runa 2, Hild 1 and 2, Yrsa 3, two rumours, Eirik 4 (after the Brukam) |
| Home 12 on | Tier IV, Moss Marsh | Nykk-hair Lines; the Lindorm-scale Ring and Nykk-hair Charm; Living Heart (luck) | Gunnar 4, Jeweller's Bench, a lucky find |

From the second homecoming on, about one new module arrives each time (two at most, when a tier and a quest finish together), and a
quest or a tier names it. That keeps the pace of "at most 2 new Haven quests per return".

### 4.2 Numbers, checked against BaseProgression and GearScore

- **Metal:**
  - Tier II modules cost 13 Copper Ingots if every one is taken (Apprentice's 3, Wisp 1, Frame Saw 3, Burl Still 4, Mason's 2). That
    is about 1.5 raids of copper at 9 a raid, against 19 copper for the tier II upgrades.
  - Tier III modules cost about 8 iron plus 1 per Iron Bands, so about 13 with five Bands. That is 2 raids at 6 a raid, against 33 iron
    for the tier III upgrades.
  - **Rule:** a tier's modules cost at most a third of that tier's upgrade metal. Most of their cost is home materials.
- **Rare gates:**
  - 1-3 of one item each, from creatures that drop it 60-100% of the time. Lindorm Scale (25% per Moss Marsh IV elite, about one a run)
    and Heart of the Forest (2%, optional) are the exceptions.
  - Nothing the story needs hangs on luck: Nykk's Mane comes from a quest rumour site that gives it (the Moonsilver Folio rule).
- **Luck:**
  - Today a Smithy with Bellows and Quench Tub has 8% luck, plus a Master worker's 12%: 20% more ingots.
  - With the Apprentice's Corner it would be 25%.
  - The 15% cap from modules and extensions keeps the extra metal to about +5 points, so less than one raid saved per tier.
- **Gear:** a Fine piece counts "as if empowered part of the way". +10% Fine chance moves a kit's expected score by well under 1%, far
  inside `RaidTierTests`' 20%. **No tier multiplier needs changing.**
- **Essence:**
  - The altar's tiers give +50%.
  - Modules add +10% while staffed, and +15% with the Living Heart (a lucky find), for +75% at most.
  - That is about 17% more essence than now, so portal tiers and empowering come a little sooner. Watch it in playtests. The cap is
    +25% from modules.
- **Wood and stone** (the home chores): the Frame Saw, Mason's Frame and Bloomery Hearth cut the sinks that the 2026-11-06 balance pass
  doubled (888 Wood to build everything once). They arrive at tier II-III, when the chore has been felt, which is the point.
- **Stash:** +14 slots from modules on top of the shed's +16. This answers the 14 minutes of sorting in the 11-14 run.
- **Comfort, light, foretold:** +1 or +2 comfort (60 s of Well Rested each), +5 m light, +1 foretold event. All are small and visible.

### 4.3 What changes in the existing tiers

1. **Slot data** (pure data): 1 slot at I and +1 at each tier, on the buildings in §3.7. This replaces today's +1 at Forge III, Lodge III
   and Kitchen II.
2. **Tiers teach modules** (`BuildingLevel › Unlocks Modules`), eight of them: Smithy III, Workshop III, Lodge III, Hunter's II, Tailor
   III, Cauldron II, Shed II, Cottage II. Pure data once the field exists.
3. **No bonus moves out of the tiers.** Station numbers don't set the pace, the "never remove functionality" rule holds, and tiers keep
   their reason, which is new recipes and buildings. If stations feel bloated after a playtest, the first thing to trim is the tiers'
   +5 output at IV (Workshop, Smithy, Lodge), as the Storage Loft does that job better.
4. **The Bellows joins the group "fire"**, so the Smithy is either fast or thrifty.

### 4.4 Risks

| Risk | Why | Answer |
|---|---|---|
| Faster metal climb | Luck on ingots | 15% cap. Cost variants never touch ore |
| Faster portal tiers | Essence yield | Module yield at most +25%; one module is optional and lucky |
| Metal drain | Many generics times many buildings | Generics cost 1-3 ingots; a third-of-the-tier rule |
| Choice paralysis | Up to 10 modules could fit a station | The list shows only what is taught. Modules come one a homecoming |
| "+5% boredom" | Numbers-only modules | The extended `NoUpgrade_IsOnlySpeed`. Every own module opens a recipe, removes a chore or answers a named problem |
| An unreachable gate | Nykk's Mane has no source | A canary test: every module's materials have a source. Fix the Nykk's Mane first |
| Raid-time blindness | Raids read `UpgradeBonus`, not the scene | Modules are noted in `NoteBuildings`; tests on the raid view |
| Old saves, co-op | New state | Appended save fields; format bump; round-trip tests |
| Breach balance | +50% health, turned-back harm | Corruption nights scale by portal tier: check one breach after Iron Bands |
| Art | 23 fitted modules with no visuals | Dressing is optional (`Module <id>` parts). Ship without a prop rather than an ugly one (visual-care rule) |

---

## 5. Quests

### 5.1 The rules kept

- **Teach once.** The tab is taught in one quest (Bjarne's Copper Fire). After that, each module is content, not a mechanic. A quest
  teaches at most one.
- **Never ask before teaching.** A quest that asks you to fit a module teaches it when it starts (Unlocks Modules), and
  `QuestReachability` checks it.
- **Rescues stay gated as approved.** Rumours wait for the previous person, and first quests start on rescue. Modules change none of it.
- **Main quests stay events.** Every module quest below is a person's Haven quest with a raid step or a place, never "gather and build"
  alone.
- **Nothing mandatory hangs on luck.** A quest that needs a rare part gets an urgent rumour whose site gives it.

### 5.2 Changes to existing quests (data)

| Quest (asset) | Person, step | Change | Teaches (at start) | Effort |
|---|---|---|---|---|
| **Copper Fire** (SmithsRequest) | Bjarne 3 | After "Upgrade Smithy to tier II", add "Fit a Storage Loft at the Smithy" [owned]. Bjarne's line: "The fire's bigger, and so's the bench. A loft for what's done, and the queue won't stop when the work changes." | Storage Loft, Apprentice's Corner | S data + C1 |
| A Growing Home | Tor 3 | End line points at the shed's loft | Loft Shelves | S |
| Heavy Loads | Tor 4 | End line: "a frame saw wastes less" | Frame Saw | S |
| Cleansing the Curse | Ingrid 2 | – | Runestone Ring | S |
| A Draught for the Road | Ingrid 3 | – | Herb Rack | S |
| The Watch | Eirik 1 | – | Wisp Lantern | S |
| Hold the Line | Eirik 2 | – | Hag's Knot | S |
| Cured | Gunnar 2 | – | Fletcher's Jig | S |
| Good Dirt | Asa 1 | – | Scarecrow | S |
| Flax and Linen | Asa 2 | – | Spinning Wheel | S |
| Read the Ground | Runa 2 | – | Map Table | S |
| Stone on Stone | Hild 1 | – | Mistwing Quilts | S |
| Granite | Hild 2 | – | Mason's Frame | S |
| A Hand for the Shelf | Liv 5 | End line: the shelf at the Cauldron | Rune Shelf | S |
| The Jarl's Table (planned) | Yrsa 3 | – | Jutul Cold Store | S, with the quest |

### 5.3 New and planned quests where a rare raid material gates a module

| Quest | Person, after | Objectives (in order) | Teaches | Effort |
|---|---|---|---|---|
| **The Iron on the Road** (planned in the quest-flow review §5.3) | Bjarne 4, after Copper Fire | Collect Grave Iron ×3; Use Object at an Iron Marker in a raid set piece; Talk to Bjarne; **Fit the Barrow-iron Anvil** | Barrow-iron Anvil | L as planned (set piece). A lighter first version without the marker is M |
| **Steel in the Water** (new) | Gunnar 4, after Never Say Its Name, with the Moss Marsh open | Talk to Gunnar ("Its own hair, and steel in the water: that's how the old ones held a nykk"); Collect Nykk's Mane ×2 (the rumour "Pale hair in the reeds" leads to it); Fit Nykk-hair Lines at the Dock; Catch Fish ×1 at the Dock | Nykk-hair Lines | M (3 lines, a rumour, a site) |
| **The Valley's Horn** (new) | Eirik 4, after Nobody Crosses and Hold the Line | Talk to Eirik; Collect Brukam's Tusk [owned]; Fit Brukam's Horn at the Watch Post; Defend a breach | Brukam's Horn | M (3 lines) |
| The Scale in the Fen (optional Tale) | – | A Moss Marsh IV rumour about something shedding in the deep pools; Reach the Drowning Fen; Collect Lindorm Scale ×1 | – (the ring is at the Jeweller's Bench) | M, later |

Also, the **tiers already gate on trophies**: Smithy V needs the Stone-troll Knuckle, and Hunter's Lodge V the Jutul Heartstone. Hild 3
"The Sleeping Quarry" (planned: the sun-caught troll) can name where stone trolls stand (Troll Valley III). That gives Smithy V's gate a
pointer with one line and no new quest.

### 5.4 Rumours that point to materials and plans (data)

| Rumour | Waits for | Where | Site gives | Urgent |
|---|---|---|---|---|
| "The old bloomery" | Smithy III standing | Bog-Iron Bloomery (Moss Marsh) | Bloomer's Notes (teaches the Bloomery Hearth) | No |
| "A tusse's nest" | Workshop III standing | Ruined Seter (Troll Valley) | Jeweller's Loupe and 2 Tusse's Trinkets | No |
| "Pale hair in the reeds" | While Steel in the Water | The Black Pool Landing (Moss Marsh) | 2 Nykk's Mane (a drowned fisher's creel) | Yes |

Every field exists (`needsBuilding`, `needsTier`, `whileQuest`, `setPiece`, `urgent`, `GiveLootOutcome`), so these are pure data plus
three short site texts.

---

## 6. Implementation plan

Smallest first. **Data** means asset edits only. Each step ends compiled and tested with the affected test classes (targeted tests;
`RunTests -All` before a build).

| # | Step | Kind | Effort | Tests |
|---|---|---|---|---|
| 1 | `BuildingBonus` base class (today's bonus fields moved, `BuildingLevel : BuildingBonus`), new fields unused yet. Diff one asset before and after | Code | S | HavenBuildingTests still pass; an asset diff shows no data change |
| 2 | `BuildingModuleDefinition`, `ModuleSettings` (5 slots, 15% luck cap). `Construction` modules (fit job, remove, save fields, Sum includes modules, the Staffed rule). `StationExtensions.Slots` capped and shared. `SettlementState.SpareModules`; `NoteBuildings` and `UpgradeBonus` include modules; co-op mark | Code | M | ModuleSlotsTests (slots by tier, cap 5, Work Yard, each kind once, a group); save round trip incl. a 4-part old save; raid view sums modules; spares |
| 3 | Slot data on every building in §3.7 (1, +1 a tier) | **Data** | S | Slot counts per building, with a canary that fails if a level lacks its slot |
| 4 | The Upgrade tab: Craft/Upgrade tabs in `StationWindow` (via `StationWindowBuilder`), the same panel in a building window for the rest, replacing `ConstructionUpgradeOffer`'s page; Track for modules (`WantedAtHome` keys); pad | Code | M-L | A UI smoke test: tabs switch, a locked slot says why, Fit pays and starts the job |
| 5 | Teaching: `QuestDefinition`, `BuildingLevel` and `ItemDefinition` module fields; `ObjectiveType.FitModule`, `GameEventKind.ModuleFitted`; `QuestReachability` | Code | S-M | FitModule counts and counts-owned; AskedBeforeTaught fails a quest that asks for a module it doesn't teach |
| 6 | The generic modules Storage Loft (room only until step 7), Apprentice's Corner, Iron Bands; Copper Fire's new step | **Data** + 2 lines | S | `NoUpgrade_IsOnlySpeed` extended to modules |
| 7 | Output slots: `StationQueue` keeps a list; save migration; `StationWindow` output grid shows each kind; workers collect all | Code | M | A mixed queue doesn't block with 2 slots and does with 1; save with an old single output |
| 8 | `RecipeDefinition › Needs Module` and `Replaces`: the check, the lock line, hiding the worse variant, workers using the open variant | Code | S | Can't queue without the module; cancelling a variant refunds what it took; a worker picks the variant |
| 9 | Own modules with existing materials and their variant recipes (Mason's Frame, Frame Saw, Spinning Wheel, Herb Rack, Kit Bench, Fletcher's Jig, Antler Wall, Loft Shelves, Bog-hide Roof, Quilts, Box Beds, Scarecrow, Map Table, Runestone Ring, Elder Graft, Rune Shelf, Wisp Lantern); RecipeBook | **Data** | M | Materials-have-a-source canary; every module fits something |
| 10 | Defence: health, fight and turn-back hooks; Hag's Knot, Brukam's Horn | Code + data | S | Health share applies; turned-back damage hits the striker, never a friend |
| 11 | Gear luck (Fine chance from the station); Barrow-iron Anvil, Widow-silk Lacing | Code + data | S | Quality chance adds up and stays capped |
| 12 | New items and their modules: Grave Bodkin, Lindorm-scale Ring, Nykk-hair Charm, Frost-cured Ham, Burl Draught, Nykk-hair Deep Lines; Jeweller's Bench, Burl Still, Jutul Cold Store, Bloomery Hearth, Nykk-hair Lines; icons through LocalSpawn | Data + content | M | GearScore and RaidTierTests unchanged within tolerance |
| 13 | Quest data (§5.2); Steel in the Water and The Valley's Horn; the three rumours and their sites; **a source for Nykk's Mane** | Data + content | M | Quest reachability; rumour conditions |
| 14 | Dressing parts for fitted modules (`Module <id>`), starting with the Storage Loft, Bloomery Hearth and Wisp Lantern; close-ups and Game View of each seam | Art | M | Visual check in a scene |
| 15 | Docs: BaseProgression (the module rule and slots), GDD §14, the ARCHITECTURE status row, LootTables regenerated | Docs | S | – |

**The first playable slice** is steps 1-4, 6 and 7: the tab, slots, the Storage Loft as the user described it, and Copper Fire teaching
it. Everything after it is data and content riding on the same code.

---

## 7. For the user to decide

1. **The placed extensions.** Keep them placed (this plan), or turn the nine into fitted modules so everything is installed from the
   tab. That would mean hiding them in the build menu and changing a placed one in old saves into a fitted one. That second option
   removes the "set it by the forge" play, so it is the user's call.
2. **Removal into a spare store**, rather than a partial refund. This plan never loses materials. The other choice is to make
   specialisation stick: refund the common materials and keep the rare part in the module.
3. **The fire group** (Bellows or Bloomery Hearth). One exclusive choice on the Smithy is suggested, with none elsewhere for now.
4. **Moving the tiers' +5 output into the Storage Loft** after a playtest, or leaving the tiers alone (recommended for now).
5. **Nykk's Mane:** add the Nykk creature to the Moss Marsh, or make it a find in the Moss Marsh's own loot until the creature exists.

---

## Sources

1. Valheim crafting station levels (timesaver.gg): https://timesaver.gg/blog/valheim-workbench-upgrades-crafting-station-levels
2. Valheim station upgrades (allthings.how): https://allthings.how/valheim-1-0-how-to-get-every-crafting-station-upgrade/
3. Valheim forge upgrades (Game8): https://game8.co/games/Valheim/archives/320284
4. Enshrouded upgrades (pindrop.gg): https://pindrop.gg/enshrouded/crafting/upgrades
5. Enshrouded Table Saw: https://enshrouded.wiki.gg/wiki/Table_Saw
6. Enshrouded Laboratory: https://enshrouded.wiki.gg/wiki/Laboratory
7. Enshrouded Smithing Tools: https://enshrouded.wiki.gg/wiki/Smithing_Tools
8. Enshrouded Blacksmith: https://enshrouded.wiki.gg/wiki/Blacksmith
9. Enshrouded Flame Altar: https://enshrouded.wiki.gg/wiki/Flame_Altar
10. Enshrouded Spark: https://enshrouded.wiki.gg/wiki/Spark
11. Palworld Bases: https://palworld.wiki.gg/wiki/Bases
12. Palworld Technology: https://palworld.wiki.gg/wiki/Technology
13. Palworld Ancient Civilization Parts: https://palworld.wiki.gg/wiki/Ancient_Civilization_Parts
14. Palworld Statue of Power: https://palworld.wiki.gg/wiki/Statue_of_Power
15. Palworld Pal Labor Research Laboratory: https://palworld.wiki.gg/wiki/Pal_Labor_Research_Laboratory
16. V Rising Castle Heart (patch notes; wiki seen in search only): https://primagames.com/tips/v-rising-secrets-of-gloomrot-update-full-patch-notes-listed , https://vrising.fandom.com/wiki/Castle_Heart
17. V Rising blood essence (GamesRadar): https://www.gamesradar.com/v-rising-blood-essence-greater-primal
18. V Rising floors (BisectHosting): https://www.bisecthosting.com/blog/v-rising-flooring-guide-types-bonuses-unlock-requirements-crafting-ingredients
19. V Rising unlocks (TechRaptor): https://techraptor.net/gaming/guides/v-rising-crafting-and-unlocks-guide
20. Medieval Dynasty technology: https://justplaybetter-web.pages.dev/medieval-dynasty/progression/skills-and-technology
21. Going Medieval rooms (search snippet): https://goingmedieval.fandom.com/wiki/Rooms
22. Frostpunk 2 districts: https://www.gamepressure.com/frostpunk-2/building/ze115f1 , https://frostpunk2.game-vault.net/wiki/Industrial_District
23. Against the Storm patch notes (housing upgrades): https://gog.com/forum/against_the_storm/updates_and_changelogs/page2
24. Against the Storm Hearth (may be outdated): https://hoodedhorse.com/wiki/Against_the_Storm/Hearth
25. Eremite Games, progression update: https://eremitegames.com/progression-update/
26. Gord upgrades (unverified costs): https://www.distractify.com/p/how-to-upgrade-buildings-fast-gord , https://techraptor.net/gaming/guides/gord-great-lakes-walkthrough
27. Dragonwilds Improvements: https://dragonwilds.runescape.wiki/w/Loom_Harness , https://dragonwilds.runescape.wiki/w/Tanner%27s_Kit
28. Factorio: https://wiki.factorio.com/Module , https://wiki.factorio.com/Assembling_machine_2 , https://wiki.factorio.com/Assembling_machine_3 , https://wiki.factorio.com/Electromagnetic_plant , https://wiki.factorio.com/Beacon
29. Satisfactory: https://satisfactory.wiki.gg/wiki/Clock_speed , https://satisfactory.wiki.gg/wiki/Power_Shard , https://satisfactory.wiki.gg/wiki/Somersloop , https://satisfactory.wiki.gg/wiki/Production_Amplifier
30. Kingdom Come: Deliverance, Pribyslavitz: https://kingdomcomedeliverance.wiki.gg/wiki/Forge_of_Pribyslavitz , https://kingdomcomedeliverance.wiki.gg/wiki/Pribyslavitz
31. Larian on big numbers (Game Developer): https://gamedeveloper.com/design/baldur-s-gate-3-s-item-balancing-went-big-because-small-stat-changes-are-not-cool-
32. Josh Bycer, power curves (Game Developer): https://gamedeveloper.com/design/the-3-ways-power-curves-impact-progression
33. Sid Meier, GDC 2012 (Game Developer): https://gamedeveloper.com/design/gdc-2012-sid-meier-on-how-to-see-games-as-sets-of-interesting-decisions
34. Folklore of the nøkk and the nykk (the project's own research): Docs/NextRaidResearch_2026-11-17.md
