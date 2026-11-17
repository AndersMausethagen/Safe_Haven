# Loot and drops: what there is, what is missing, and what to borrow (2026-11-17)

You asked: are all drops useful, which are not and what could they be used for, do we need more unique drops from chests
and monsters, do we need more to make, how does ARC Raiders make drops interesting, could that work here for crafting,
building and upgrading, and is essence a good resource.

**How this was made.** This is research only: nothing in the project was changed.
- **A script read every data asset, prefab and scene** (1,909 files). It found 1,784 places that give or ask for one of the
  231 items and bar resources, and sorted them into sources and uses the same way `ContentAudit` does.
- **Real play.** I read the playtest logs. The useful ones are three real runs from 2026-11-14 and 11-15, with five tier I raids
  between them. That is small evidence, so I treat it as a hint, not a measurement.
- **Web research.** I read about ARC Raiders and about ten other games. Sources are at the end. Where I could only read a search
  summary and not the page itself, the source list says so.

---

## The short answer

1. **Almost everything has a use.** The audit's rule holds: nothing is asked for that nothing gives, apart from Water, which comes
   from water on purpose. But about a dozen drops are weak:
   - **Sell-only:** the Gilded Troll Idol and the Heart of the Forest can only be sold at the Trading Post.
   - **One or two uses, and they pile up:** Grave Iron, Rootling Bark, Boar Tusk, the Runestone Fragment and the Radiant Life Orb.
   - **A wasted drop:** Grey Stone from three troll-folk. It is a home staple, so it is pointless as raid loot.
   - **No way out:** found gear the player has outgrown, and relics they don't want. They can't be broken down, and plain gear can't
     be sold.
2. **Monsters don't feel distinct in what they give.** Of about 30 creatures that drop anything, fewer than ten have a part of their
   own.
   Many of the newer folklore creatures reuse older drop tables:
   - The Moss Shaman, Bog Troll, Fossegrim and Trollkjerring drop exactly the same things.
   - So do the three great trolls.
   - So do the Small Moss Troll, Troll Runt and Tusse.
   - The Black Widow drops what the Bog Spitter drops.
   - The Heartroot boss drops what a Root Guardian drops.
3. **Chests don't feel distinct either.** One Supply Cache table of 29 entries serves every chest in both raids. Troll Valley still
   uses the Misty Woods' resource and cache lists, which the plan expects at this stage.
4. **Recipes are deep at the Smithy and the Cauldron and thin elsewhere.**
   - The Tailor's Shop, Hunter's Lodge, Woodcutter's Lodge and Wainwright's Yard make one thing or none per tier past tier II.
   - Creature parts are almost never used by the Tailor, the Lodge, the Kitchen, or in building upgrades.
5. **An ARC Raiders approach fits partly.** Several parts would work well here:
   - Station tiers that ask for a named part from a named creature.
   - Breaking found gear down into materials at home, with the result shown before you confirm.
   - A "used for" line on every item, and marking the items you want.
   - Chests themed by what they hold.

   ARC's coin economy and its junk-everything loop don't fit. Neither do wipes or stash-space pressure. These fight the GDD's rule
   that "materials alone aren't a reason to go".
6. **Essence: keep it, but change how it is used.** It is the game's clearest raid-only currency, and choosing between purifying it
   and spending it raw is a good decision. But:
   - It sits on every drop table, so kills feel alike.
   - It weighs nothing, so it never enters the decision about what to carry.
   - It alone pays for ward levels, great building tiers, empowering, enchanting and skill ranks.

   The fix is to pair the big essence costs with a named creature part, so essence decides how much and a part decides which one.
   Also trim the minor light bars rather than adding more.
7. **Bugs found on the way:**
   - The Blight Boar never drops the Bone its journal entry and BaseProgression promise.
   - Woven Hide Leggings are filed as a Valuable.
   - Hostile creatures in the Haven world's outer bands very likely give Cursed Essence at home. This comes from reading the code
     and needs one check in Play. The GDD says only raids give it.

The ranked list of suggestions is in [section 10](#10-suggestions-ranked-by-value-for-effort).

---

## 1. What there is today

### 1.1 In numbers

| What | Count | Notes |
|---|---|---|
| Items | 224 | 93 equipment, 52 consumables, 54 materials, 10 tools, 9 quest and knowledge items, 6 valuables |
| Bar resources | 7 | Life Mote, Life Orb, Radiant Life Orb, Orb Tincture, Portal Spark, Cursed Essence, Purified Essence |
| Recipes | 185 | At 14 stations (see section 4) |
| Buildings | 63 | Stations, houses, walls, decorations |
| Creatures with a drop table | About 30 | 30 monster definitions; the prologue wolf and the Barrow Shieldbearer are prefab variants |
| Chest tables | 1 main | Supply Cache, plus small Barrel and Supply Crate breakables and three cache variants |
| Raid events and rumours with loot | 14 | Old Barrow, Trader's Wagon, Cursed Tree, Toll Troll, the knowledge finds and others |
| Enchantments | 57 | 49 enchantments and 8 relic powers |
| Haven asks | 21 | Residents asking for raid things |

### 1.2 Materials: where they come from and what uses them

"Uses" counts the distinct recipes, buildings and asks that want the item.

**Home basics** are all well used: Sticks (34 uses), Small Rocks (8), Wood (44), Grey Stone (22), Nettle Fiber (12), and the two
refined goods most things need, Nettle Cord (50) and Oak Plank (47). The same is true of Berries, Barley, Turnips, Chanterelles,
Grubs, Fireflies, the four fish and Charcoal. Nothing needs doing here.

**Raid materials:**

| Item | Tier | Comes from | Used by | Verdict |
|---|---|---|---|---|
| Copper Ore | II | Ore nodes in tier I-III raids; tier I elites; Trading Post | Copper Ingot (27 uses); one ask | Fine |
| Iron Ore | III | Nodes from tier II; tier II elites; Ore Collapse; Trading Post | Iron Ingot (41 uses); old Furnace; ask | Fine |
| Moonsilver Ore | IV | Nodes from tier III; tier III-IV elites; Trading Post | Moonsilver Ingot (29 uses) | Fine |
| Granite | II | Nodes; Trading Post | Granite Block (9 uses); 2 draughts | Fine |
| Deepstone | III | Rare node from tier II; Trading Post | 9 uses: Ward Totem, Smithy IV, Moonlit portal, gear | Fine |
| Hardwood | II | Nodes; Root Guardians; Trading Post | Hardwood Plank (24 uses); Elder Tree | Fine (a step on the way to planks) |
| Elderwood | III | Rare node from tier II; Cursed Tree; Elder farm | 14 uses | Fine |
| Flax | II | Nodes; farm | Linen (16 uses); Fire Arrow | Fine |
| Huldra Silk | III | Rare node; Huldra (30%); a spirit's gift; Trading Post | 13 uses | Fine |
| Healing Herb, Cloudberries | II | Nodes; caches; farm | 8 and 4 uses | Fine |

**Creature parts.** This is where the weak spots are.

| Item | Tier | Comes from | Used by | Verdict |
|---|---|---|---|---|
| Rough Hide | I | Wolves, boars, deer, fox, stag | 20 uses | Fine |
| Thick Hide | III | Root Guardian, Stag, caches, Trading Post | 15 uses | Fine |
| Bone | I | Wolves, deer, fox, stag, Mistwing | 19 uses: the attunement and raid runes, arrows, staff | Fine |
| Boar Meat | I | Boars | 5 meals | Fine |
| **Boar Tusk** | II | Boars (35-40%) | Tusk Ring; one ask | **Thin** |
| Wolf Fang | II | Wolves (25%) | Two raid runes, Venom Fang, Fang Necklace; ask | Thin, but fine |
| Bog Venom | II | Bog Spitter, Black Widow (70%) | Antidote, Cursed Draught, Venom Fang; ask | Thin, but fine |
| **Rootling Bark** | I | Rootlings (80%, and they come in swarms) | Barkskin and Ironbark draughts; one ask | **Thin, and it piles up.** In real play, 12-17 were picked up over two raids |
| Wisp Ember | III | Marsh Light (60%); Fog Bank cache | Forward Camp Kit, Moon Broth, Swiftfoot Tonic, Wisp Pendant; ask | Fine |
| **Grave Iron** | III | Draug, Barrow Shieldbearer, the Undead Knights (70-90%); Old Barrow | Grave Blade only; one ask | **Thin, and it piles up** |
| Guardian Heartroot | III | Root Guardian, the Heartroot | Workshop IV, Smithy IV, Lodge IV, Heartroot Amulet, Hide Rucksack | **Good:** this is the trophy pattern working |
| Trollstone | V | The three great trolls; tier IV elites (25%); Toll Troll; Trading Post (35 coins) | 14 uses | Good |
| **Grey Stone, as a troll drop** | I | Small Moss Troll, Troll Runt, Tusse | A home staple | **Wasted as a raid drop:** it weighs 1.5 and the clearing is full of it |

### 1.3 Valuables, finds and knowledge

| Item | Comes from | What it is for | Verdict |
|---|---|---|---|
| Old Silver Penny | Caches (55%); draug, knights and four troll-folk; most events | The Trading Post's coin; one Old Barrow choice | A currency, but there is little to spend it on |
| Runestone Fragment | Caches (18%); events; reward for the trollstone ask | Sells for 6 coins; one per Ward Totem | Thin |
| **Gilded Troll Idol** | Caches (6%); Old Barrow (15%); Three Wishes | Sells for 15 coins | **Sell-only** |
| **Heart of the Forest** | Caches (1.5%) | Sells for 40 coins | **Sell-only.** Its own text says "The forest wants it back", which asks for a use |
| Pact Rune | Caches (5%); quests; Rot's Heart | Claims a region | Good: one clear use |
| **Woven Hide Leggings** | The starting kit | Sells for 1 coin | **A leftover** from when legs had a slot, and filed as a Valuable |
| Rune Book | Caches (12%); four troll-folk (15%) | Read for 120 experience | Fine, but the same everywhere |
| Knowledge finds (Moonsilver Folio, Wainwright's Ledger, Bathhouse Plans, Pouch of Wild Seed, Swarm in a Skep, Elder Sapling, Trollsmith's Plans) | One rumour or event each | Teach recipes or buildings, read at home | **The best drops in the game:** one of a kind, at risk until extracted, and guaranteed through rumours |
| Inscriptions | Carved stones; elites (10-30% by tier); bosses; caches (3%) | Learn an enchantment | Good. This is the game's equivalent of ARC's blueprints |
| Relics | Great trolls (always); elites (0-15%); caches (6%); rumours | Gear with a power | Good, but an unwanted one has no use |
| Found gear (Bone Charm, Copper Band, Cured Leather Cap, Round Shield; knights' horse bardings at 20%) | Caches, Undead Knights | Wear it, or nothing | **No way out:** plain gear can't be sold (`TradeSettings.CoinsFor`) or broken down |

### 1.4 The bars

| Bar | Comes from | Spent on |
|---|---|---|
| Life Mote | Life Mote nodes | Cauldron (8), Healing Salve, Orb Tincture |
| Life Orb | Nodes; Rootbound Stones; caches; breaches won | Portal repair, ward and portal levels, Portal Sparks, Orb Tincture, Waystone Shard, Altar |
| Radiant Life Orb | A rare node | Greater Healing and Radiant Draught only |
| Orb Tincture | Made from orbs; caches | 16 recipes: the runes, healing draughts, Runic Staff |
| Portal Spark | Made at the portal | 3 minutes more raid time each |
| Cursed Essence | Nearly every hostile creature; elites; caches | Cursed enchantments; Cursed Draught; purifying |
| Purified Essence | The altar (2 Cursed for 1); one quest; the Cursed Tree | Ward and portal levels, great building tiers, empowering, enchanting, skill ranks, Purified Draught |

### 1.5 What real play shows

Three real runs with five tier I raids between them:
- **What was picked up in raids:** over two raids, 11-23 copper ore, 10-18 rough hide, 12-17 rootling bark, 10-11 bone, 6 pennies
  and 3-5 wolf fangs. One run found a Heart of the Forest, and one a Swarm in a Skep.
- **Essence:** 28-33 Cursed Essence over two raids, about 15 a raid. The prologue gave about 56.
- **Crafting:** nothing was crafted from a creature part. Every craft was stone tools, food and bandages. The runs ended before the
  Lodge or the Smithy stood, so this doesn't prove the sinks fail. It does show that parts pile up from the first raid.

---

## 2. Are all drops useful?

### 2.1 Dead ends: no use except selling

| Drop | Problem | What it could be for |
|---|---|---|
| Heart of the Forest | Sells for 40 coins, nothing else | **A choice.** Sell it, or give it back to the forest: offer it at the Elder Tree or the portal for something lasting (ward radius, renown, a relic, or easing the blight). The item text already says the forest wants it |
| Gilded Troll Idol | Sells for 15 coins | Melt it at the Smithy into gilt for Masterwork finishes and gilded decorations (a banner, a carved post) that give renown. Or a troll accepts it as a toll instead of a fight |
| Woven Hide Leggings | Starting-kit leftover filed as a Valuable | Make it a Material ("old hide") that breaks down into 2 Rough Hide, or drop it from the kit |
| Found plain gear and unwanted relics | No sale, no salvage | Break it down at home into a share of its recipe (section 7) |

### 2.2 Thin uses: one or two sinks, and they pile up

| Drop | Uses today | Proposed extra uses (no new items needed) |
|---|---|---|
| Grave Iron | Grave Blade; one ask | Grave-iron bodkin arrows (good against armour and draugs); Iron Trap II teeth; a Smithy IV upgrade cost; a grave-iron helm or axe with a chilling hit |
| Rootling Bark | Two draughts; one ask | A Woodcutter's Lodge III cost; a "barkbound" shield; mulch for the Farm Plot or Herb Garden (crops grow faster); a smoke pot (section 9) |
| Boar Tusk | Tusk Ring; one ask | A Hunter's Lodge II cost; tusk-spiked Spike Trap II; tool handles; a tusk-hilted hunting knife |
| Runestone Fragment | Ward Totem; sells | Ward Totem levels; the Rune Stone decoration; give three to Liv to rebuild an inscription |
| Radiant Life Orb | Two draughts | Portal tier IV or ward III; the Elder Tree's second level; empowering to +3 |
| Old Silver Penny | Trading Post | More to buy: a horse trader, rumours from a pedlar, a paid ask on the Notice Board |

### 2.3 Bugs and odd data

1. **The Blight Boar never drops Bone.**
   - Its `MonsterDefinition` (which the journal shows) has Bone 1-2 at 60%.
   - The `LootOnDeath` on `Prefabs/Enemies/BlightBoar.prefab` doesn't, and that table is what actually drops.
   - BaseProgression says "meat, hide and bone". This is the only mismatch between the two tables.
2. **Woven Hide Leggings are filed as a Valuable** (`category: 5`).
3. **Cursed Essence probably drops at home.**
   - `LootOnDeath` keeps Cursed Essence back only when `WithholdsEssence` is set, and only `HomeHunting` and `NightProwlers` set it.
   - The Haven world's hostile bands (Edge of the woods, Misty Woods, Deep woods, Bog in `Haven World Recipe`) spawn wolves,
     boars, spitters, shamans, mistwings, small trolls, draugs, marsh lights and the huldra through `MobSpawnZone`. Nothing withholds
     their essence.
   - One real run logged 2 Cursed Essence gathered in SafeHaven. That run also used the cheat menu, so this is a hint, not proof.
   - GDD §7.7 says only raids give it. Check this once in Play. The same bands also make most "raid" creature parts huntable at home,
     which matters if station tiers start asking for parts.
4. **Item Value is not on one scale.**
   - Ore and its ingot have the same value: copper 3 and 3, iron 3 and 3, moonsilver 8 and 8.
   - A Trollstone is worth 8 but costs 35 coins, which is 350 in value terms, since a coin is worth about 10.
   - The Idol shows "Value 150" and sells for 15 coins.
   - This is harmless today, because Value only feeds the raid report and the tooltip. It matters the moment anything sells or breaks
     down by value.
5. **Troll Valley copies the Misty Woods' lists.** Its Resources and Supply Caches lists are the Woods' own, and only the creature
   weights differ. The plan (step 6) brings troll hoards and the valley's own spawns later.

---

## 3. Do monsters and chests feel distinct?

### 3.1 Creatures that share one table

These are the tables that actually drop (`LootOnDeath` on the prefabs):

| What drops | Creatures that drop exactly this |
|---|---|
| 1-2 Grey Stone, 1-2 essence | Small Moss Troll, Troll Runt, Tusse. The Tusse also drops what it stole |
| Rune Book (15%), 1-2 pennies (50%), 3-4 essence | Moss Shaman, Bog Troll, Fossegrim, Trollkjerring |
| 2-3 Trollstone, 9-12 essence, a relic, its own inscription | Old Moss Troll, Stone Troll, Berg-jutul |
| Bog Venom (70%), 1-2 essence | Bog Spitter, Black Widow |
| Guardian Heartroot, 2-4 Hardwood, Thick Hide (50%), 3-4 essence | Root Guardian, and **the Heartroot boss**. The boss also teaches an ability and its inscription, but its material drop is a guardian's |
| Grave Iron, pennies, essence | Draug, Barrow Shieldbearer. The Undead Knights add a horse barding at 20% |
| Hide, bone, Wolf Fang (25%), essence | Forest-Bitten Wolf, the prologue wolf |
| Bone, essence | Mistwing |

**Creatures with a part of their own:**
- Rootling (bark)
- Marsh Light (wisp ember)
- Root Guardian (heartroot)
- Huldra (silk)
- Boars (meat and tusk)
- Stag (thick hide)
- The Undead Knights (barding)
- The great trolls (trollstone, shared among the three)

That is fewer than ten of about 30. The folklore creatures added on 2026-11-13 and 11-14 have bodies, moves and roles of their own but no loot
of their own: the Tusse, Fossegrim, Trollkjerring, Bog Troll, Black Widow, Troll Runt, Berg-jutul and Stone Troll.

### 3.2 Chests

- **One table everywhere.** The one Supply Cache table (29 entries) is the same in every raid, tier and place. It holds:
  - ingots, draughts, herbs and planks;
  - pennies, essence and the runes;
  - four low-tier gear pieces;
  - the Idol and the Heart of the Forest.
- **Variants change little.** The Fog Bank, Abandoned Camp and Wolf Den variants override a few entries.
- **Themed loot comes from events, and they work.** The Old Barrow gives grave iron and pennies, the Trader's Wagon gives ingots and
  linen, the Cursed Tree gives purified essence and herbs, and the Toll Troll gives trollstone. A chest tells you nothing about
  where it stands.

### 3.3 Places and tiers

- **Tiers differ in ore only.** What changes is copper, then iron, then moonsilver, then trollstone, plus the tier cap on drops
  (`RaidDifficulty.Allows`).
- **Elites pay ore too:** copper, iron, moonsilver, and a chance of trollstone at tier IV. Their bonus is generic.
- **The two raids give the same things for now.** Troll Valley's own identity in loot ("troll hoards" in the plan) isn't built.

---

## 4. Recipes and stations: what can be made, and the gaps

The tier of a recipe here is the tier of what it makes.

| Station | Recipes | I | II | III | IV | V | Gap |
|---|---|---|---|---|---|---|---|
| Smithy | 52 | - | 14 | 18 | 12 | 8 | None: the backbone |
| Cauldron | 26 | 1 | 19 | 3 | 3 | - | 12 of the 19 at tier II are runes. Thin after tier II |
| Workshop | 24 | 15 | 5 | 2 | 1 | 1 | One field kit per tier past II |
| By hand | 17 | 16 | 1 | - | - | - | Fine |
| Kitchen | 12 | 2 | 6 | 2 | 2 | - | Fine |
| Campfire | 10 | 9 (+1 at tier 0) | - | - | - | - | Fine |
| Tailor's Shop | 10 | - | 8 | 1 | 1 | - | **One item each at III and IV**, though it has four levels and level IV costs 8 Huldra Silk |
| Hunter's Lodge | 9 | - | 5 | 2 | 1 | 1 | **4 of the 5 at tier II are the same Cured Leather.** One bow per tier, no arrows, traps or trophies |
| Woodcutter's Lodge | 9 | 4 | 2 | 1 | 1 | 1 | **One shield per tier** |
| Farm Plot | 7 | 3 | 3 | 1 | - | - | Fine |
| Dock | 4 | 4 fish lines (tier 0) | | | | | Fine for a side station |
| Wainwright's Yard | 3 | 1 | 2 | - | - | - | **Nothing past tier II** |
| Beehive, Portal | 1 each | | | | | | By design |

**Where the parts are not used.** Creature parts go to the Smithy, Cauldron and Workshop. Almost none go to the Tailor's Shop
(only Huldra Silk), the Woodcutter's Lodge (none), the Kitchen (boar meat only), or building upgrades. Only Heartroot, Trollstone,
Huldra Silk, Thick Hide, Deepstone and Elderwood appear in upgrades, which otherwise cost planks, blocks and ingots.

The upgrade rule in BaseProgression ("every upgrade must open something new to make...") is met by station levels. But at the
Tailor, Hunter's Lodge, Woodcutter's Lodge and Yard, "something new" is one item.

---

## 5. Essence: is it a good resource?

### 5.1 How it is earned

| Source | Cursed Essence each |
|---|---|
| Wolves, spitters, mistwings, marsh lights, the huldra, small trolls, runts, the Tusse | 1-2 |
| Rootlings, Blight Boars | 1, at 30-50% |
| Draug, Moss Shaman, Fossegrim, Trollkjerring, Bog Troll, Root Guardian, the Heartroot | 2-4 |
| Undead Knights | 4-6 |
| Great trolls (Old Moss, Stone, Berg-jutul) | 9-12, plus a relic |
| Elites by tier, I to IV | 2-3, 3-4, 4-6, 6-8 |
| Supply caches | 1-3, at 45% |
| Multipliers | Danger stage +25/50/80%; night; the Reaping enchantment and Essence Find |

Purified Essence comes only from the altar (2 Cursed for 1), plus 15 from one quest and 2-3 from the Cursed Tree event. Measured:
about 15 Cursed a tier I raid, so about 7-8 Purified. Higher tiers should pay several times that, but that is not measured.

### 5.2 How it is spent

| Sink | Purified Essence | Notes |
|---|---|---|
| Ward I to III | 6 + 12 + 20 = 38 | Plus Life Orbs; these open raid tiers II to IV |
| Portal: Moonlit | 10 | |
| Great building tiers | 174 | Workshop IV-V (16, 30), Smithy IV-V (16, 40), both lodges V (20 each), Cauldron III (16), Altar III (16) |
| Skill ranks I-III | 65 per ability × 7 = 455 | The abilities are named for creatures: Wolf's Lunge, Huldra's Blessing, Heartroot's Grasp, Trollsbane Wrath... |
| Empowering, +1 to +3 | About 40-70 per piece | 6, 12 and 24, plus 2 per level for each tier above I, and a share of the recipe's materials |
| Enchanting | 8-22 per enchantment | One per item; 9 cursed enchantments cost 10-22 raw Cursed |
| Draughts | 3 Purified or 5 Cursed each | Repeatable: the "forever" sink |

**All of it comes to about 1,400 Purified, which is about 2,800 Cursed: tens of raids.** As a long-term currency that is fine. The
choice is real early on: a ward level, or a building tier, or a first empower. Later it risks feeling like grinding one number.

### 5.3 Verdict: keep it, change how it works with drops

**What is good:**
- It is truly from raids. It carries the theme (cursed against purified).
- Purifying it slowly at 2:1 or spending it raw now for a stated price is a good, honest choice.
- It has deep sinks.

**What is weak:**
1. **It is on every table.** The most common reward for killing anything is the same bar going up a little. That flattens the
   creatures more than any other single thing.
2. **It never enters the decision about what to carry.** It weighs nothing, so it is only ever "at risk". The core extraction
   decision in GDD §12.1 ("what do I leave behind?") never touches it.
3. **One number gates every magical system.** GDD §13.5 already says important tier transitions should also need "a boss-specific
   token". That exists only where Heartroot and Trollstone appear in building tiers.
4. **Too many light bars.** Seven bar resources. The design review of 2026-11-13 already counted Life Orb, Life Mote, Radiant Life
   Orb, Orb Tincture, Portal Spark, Cursed and Purified among too many currencies. The Radiant Life Orb has two uses and the Life
   Mote three small ones.
5. **It probably drops at home**, from the Haven world's hostile bands (section 2.3).

**Recommendations:**
- **Keep Cursed and Purified Essence as they are.** Don't add new essence types.
- **Pair essence with parts.**
  - Skill rank III needs the namesake creature's part: Wolf's Lunge III needs wolf fangs, Huldra's Blessing III huldra silk,
    Heartroot's Grasp III a Heartroot trophy.
  - Empowering to +3 needs a part that matches the item's kind: hide for leather, grave iron for iron, wisp ember for jewellery.
  - Each great building tier needs a region's trophy.

  Essence then says how much, and the part says which creature to go after. This is the Monster Hunter and Valheim lesson (section 6)
  applied to a system that already exists.
- **Optional: one physical essence item.** Great trolls and Undead Knights could also drop a heavy "Blighted Heart" (weight 2-3)
  that the altar renders into 10 Cursed Essence. Big payloads then do enter the bag decision, and it works like ARC's recyclables:
  an item worth more once you have carried it home.
- **Fold the minor bars.**
  - Either let 5 Life Motes become 1 Life Orb by themselves, or give motes one clear job.
  - Give the Radiant Life Orb a real sink: portal tier IV, ward III or empowering to +3.
- **Check and close the home-essence leak**, if it is one.

---

## 6. What other games do

### 6.1 ARC Raiders (Embark, October 2025)

These points are checked on the ARC Raiders wiki and guides unless marked.
- **Every item answers "what is this for?" on the item itself.** It shows rarity, category, sell price, what it recycles into, and
  where it is used (workshop, quests, projects).
- **The categories each have a job:**
  - Basic Materials (Metal Parts, Fabric, Chemicals) are the crafting base.
  - Topside Materials (ARC Alloy, Wires, Steel Spring) are mid-tier parts found in raids.
  - Recyclables are junk worth breaking down. An Alarm Clock gives a Processor and 6 Plastic Parts; Rusted Tools give a Steel Spring
    and 8 Metal Parts. Parts dropped by ARC machines (Wasp Driver, Tick Pod) are recyclables too.
  - Trinkets are mainly for selling.
  - Nature items (Apricot, Agave) are partly used in upgrades.
- **Recycle or salvage.** Recycling at home gives the full yield, and the panel shows exactly what you will get before you commit.
  Salvaging in a raid gives about half. Junk keeps about its value when recycled, refined parts lose value, and trinkets should be
  sold.
- **Bench levels ask for named parts.**
  - Level 1 costs bulk basics: Gunsmith level 1 is 20 Metal Parts and 30 Rubber Parts.
  - Later levels ask for a junk item, a refined part and a part from a specific enemy: Gunsmith level 2 is 3 Rusted Tools,
    5 Mechanical Components and 8 Wasp Drivers. Refiner level 2 is 3 Toasters, 5 ARC Motion Cores and 8 Fireball Burners.

  That is what sends players after particular enemies.
- **Track.** You can mark a missing material in a bench's level-up tab. Tracked items then show an eye icon in the inventory and in
  raids.
- **Blueprints drop from themed containers:**
  - Industrial containers: explosives.
  - Medical containers: meds.
  - Raider containers: weapons.
  - Security containers: augments.
  - Electrical containers: utilities.

  Learning one uses it up. Drop rates were changed several times after launch, which players noticed loudly (search summaries only).
- **Quests and projects ask for hand-ins.** A trophy project wants 20 Wasp Drivers, 15 Tick Pods and 5 Surveyor Vaults, which keeps
  parts wanted after the benches are done.
- **Scrappy, the rooster,** gathers basic materials while you raid, in proportion to your time in the round.
- **Lessons from what went wrong:**
  - Embark tried an auction house and dropped it, because it "turned the game into just being about coins", with "very little care
    about going in, exploring the correct location, and searching the right containers" (Virgil Watkins).
  - The first Expedition rewarded stash value, so players hoarded. Watkins called that "a bit of a dark pattern".
  - Kotaku notes the game doesn't protect items you still need, so players recycle them by mistake.
  - Stash space and clutter are the commonest complaints (search summaries only).
  - Free Loadouts were added to ease "gear fear".

### 6.2 Other games

| Game | What it does | What to take here |
|---|---|---|
| Escape from Tarkov | Each hideout level needs specific items, other modules, and skill or trader levels. Medstation level 2: 2 bloodsets, 6 saline, 10 tourniquets, 3 medical tools, Generator 1, Health skill 2 | Station tiers that need named raid parts alongside other buildings (BaseProgression already credits Tarkov for "modules that need other modules") |
| Valheim | Each boss drops the key to the next tier: Hard Antler for the pickaxe that mines copper, the Swamp Key for the iron crypts, Dragon Tears for the Artisan Table. Hung trophies give powers | Every great creature's trophy should open one named thing. The Root Guardian already teaches iron tools; give the trolls, the Berg-jutul and the Heartroot the same |
| Monster Hunter | Each monster has its own parts that make its own gear; rare parts are rare. Breaking a part (head, tail) changes what you get. A wishlist tells you when you have the parts | Creature-themed gear lines. "How you killed it" decides the part: the game already has sever, smash and backstab. A wishlist is what ARC's Track is |
| Darkest Dungeon | Heirlooms come only from expeditions, each dungeon favouring one kind, and are spent only on the hamlet. Every upgrade needs Crests plus one other kind. Trading between kinds always loses value | Region-typed trophies for building tiers. A lossy exchange at the Trading Post as a safety valve for bad luck |
| Diablo III, Loot 2.0 | Fewer, better drops, made to suit the finder | Cut the filler (Grey Stone, the same Rune Book everywhere) rather than add more |
| Path of Exile | The currency items are crafting items, so loot is the economy | Essence already works this way: it is spent, not hoarded |
| Grounded | Analysing a new resource unlocks the recipes that use it | "The first time you bring X home, someone at home learns a recipe with it": discovery without luck |
| Subnautica | Scanning a few fragments of a machine teaches its blueprint | Inscriptions and rubbings already do this |
| Hades | Many currencies, but each has one clear sink: Keys unlock, Darkness buys the Mirror, Gemstones renovate the House, Nectar buys favour | If the light bars stay, each needs one place to go; otherwise merge them |
| Dark and Darker | Merchants' quests ask for specific looted items, and finishing them raises affinity, which opens more stock | The Haven's asks already do this; tie a resident's favour or rank to their asks |
| Deep Rock Galactic | Overclock cores from deep dives and events, crafted with credits and rare minerals | Close to inscriptions plus essence today |

**From design writing.** Chris Grey (Game Developer) argues that flat drop chances leave a long tail of unlucky players, and that
"dry spells leave bigger scars than lucky breaks feel good". The answer is chances that rise after each failure, or guaranteed
routes. The project's rule "nothing mandatory hangs on luck" (rumours guarantee the Folio) is this. Keep it for anything new that
gates progress.

### 6.3 Lessons in one list

1. Every drop needs a "for what", shown on the item.
2. A creature's own part gives a reason to hunt that creature. Generic loot and a generic currency don't.
3. Upgrades that ask for a named part make goals without luck, provided the part is common enough or has a fallback.
4. Junk that breaks into parts makes carrying a decision. Too much of it makes clutter.
5. Fewer, better drops beat many weak ones.
6. Don't let coins swallow the loop, and don't reward hoarding.
7. Protect what the player needs: mark wanted items, and warn before breaking something you still need.

---

## 7. Can an ARC Raiders approach work here?

### 7.1 What already matches

- **A safe pocket.** One slot, up to three with portal levels.
- **Weight and slots, and packs that add both.**
- **At-risk knowledge.** Learned At Home finds are blueprints that have to survive the way back. Inscriptions are blueprints that
  drop from themed sources (elites, carved stones, bosses).
- **Hand-ins.** The Haven's asks work like ARC quests: residents ask for bog venom, wolf fangs or grave iron, for a reward.
- **A trader.** The Trading Post, priced in pennies.
- **A Scrappy.** Residents' work orders: Tor's planks, Bjarne's ingots.
- **Station levels that already ask for a trophy:** Heartroot and Trollstone.

### 7.2 What fits and would help

| ARC idea | Here | What it touches |
|---|---|---|
| "Used in" and sell price on every item | A "Used for" line on items, worked out from recipes, building levels, asks and the Trading Post. Essences already show an authored "Used for" (`ResourceDefinition.Uses`) | Code, small: `ItemTooltipView` plus a lookup built from the recipe book and the buildings |
| Track and wishlist | Pin a building level or recipe; its missing items get a mark in the bag and a "wanted" line in raid pickups. Asks mark their items the same way | Code, small: a pinned list in `SettlementState`, the tooltip, the pickup message |
| Bench levels needing a named enemy part | Station levels II-IV each ask for one creature part from creatures of the raid tier below, with Trading Post stock as the fallback | Data only: `BuildingDefinition` upgrade costs, `TradeSettings` offers |
| Recycling, with the result shown first | "Break down" at the Workshop (and the Smithy for metal): a piece of gear gives a share of its recipe, scaled by wear. That gives a way out for found gear, unwanted relics and outgrown kit | Code, small to medium: reuse `Repairs.RecipeFor` and the share logic in `Repairs.Cost`. Add the new output field to `ContentAudit.ItemSources` |
| Containers with themed pools | Split the Supply Cache into 4-5 kinds: hunter's cache, smith's chest, herb-wife's basket, barrow chest, traveller's pack. Add troll hoards in Troll Valley | Data and prefab variants (the Fog Bank, Camp and Den variants show how); layout rules for which kind stands where |
| Projects that consume surplus | One long, staged Haven project (a longhouse, or rebuilding the old stone bridge) that eats region trophies and surplus parts for renown and a lasting bonus | Code, medium: a staged-building type, or `BuildingLevel` chains with `requires` |

### 7.3 What doesn't fit

- **Coins as the centre.** Here coins are a safety valve against bad luck. Widening the Trading Post into a general market would
  repeat what Embark removed.
- **Junk as the main reason to raid.** GDD §7.7: "Materials alone aren't a reason to go." Salvage should clear clutter and add
  carry decisions, not replace people, knowledge, relics and essence as the reasons to raid.
- **Wipes and Expeditions.** Safe Haven is about building a home that lasts. Nothing here should reset.
- **Stash pressure as tension.** Sixteen chests of 60 slots is right for a base game. Don't make storage scarce to force selling.
- **Luck-only blueprints for anything needed.** Keep rumour-guaranteed knowledge. Random teachings belong to optional things
  (enchantments, decorations, variant recipes).
- **Dozens of junk items.** Each needs an icon and a description, and adds to the 54 materials. A handful is enough.

### 7.4 A staged way to try it

| Stage | What | Effort | How to tell it worked |
|---|---|---|---|
| 0 | Fix the Blight Boar bone and the Leggings category; check the home-essence leak. Add one named creature part to five station levels, each with a Trading Post fallback | An afternoon, data | Playtest: do players hunt wolves and boars on purpose for the Hunter's Lodge II? |
| 1 | Automatic "Used for" line; pin an upgrade and mark wanted items | Small code | Fewer thin-use parts left in the stash after five raids |
| 2 | Break down gear at the Workshop or Smithy, with the result shown first | Small to medium code | Found gear and relics stop piling up |
| 3 | 8-10 creature parts of their own (section 8); 4-5 cache kinds; troll hoards | Medium: data, icons from LocalSpawn | Players can name what each creature gives |
| 4 | Recipe lines that use the new parts (section 9) | Medium: data, some small code | Each station adds at least two things per tier |
| 5 | A Haven project; put Value on one scale before anything sells or breaks down by value | Later | |

Add playtest rows as each stage lands (Salvaged, WantedPicked, PartUsed), so "carried and never used" can be measured instead of
guessed.

---

## 8. New drops: one part per creature that lacks one

**Rules for every new drop:**
- It replaces a generic entry rather than adding one.
- It has at least two uses in different places, or one use in a building tier.
- It comes from the raid tier below what it opens.
- If it gates progress, the Trading Post also sells a little.

Swapping a current drop is a change to existing data, so each swap is your call.

| Creature | Gives today | New part | Used for |
|---|---|---|---|
| Small Moss Troll | Grey Stone | **Troll Moss** (light) | Moss was stuffed between the logs of old Norwegian houses: Cottage and Sauna upgrades (more comfort). A Moss Poultice that heals over time. Mulch for the Herb Garden |
| Troll Runt | Grey Stone | **Stolen goods**, data only: pennies, plus a chance of Linen, Nettle Cord or a Copper Ingot | Gives the thief an identity; nothing new to make |
| Tusse | Grey Stone (and what it stole) | **Old Silver Pennies** (1-3), plus a rare **Tusse's Trinket** (a valuable) | The Smithy melts it into silver for jewellery, or the Trading Post buys it |
| Moss Shaman | Rune Book, pennies | **Shaman's Burl** (living wood) | The staff line (Runic Staff, a tier IV staff); a Woodcutter's Lodge III cost |
| Trollkjerring | Rune Book, pennies | **Hag's Knot** (a "trollknute", a knot-mark against harm) | Ward Totem and its levels; a charm against curses; a chance of an inscription |
| Bog Troll | Rune Book, pennies | **Bog-troll Hide** (slick, waterproof) | Tailor: bog waders (no slowing in wet ground), a Dock upgrade, rowboat caulking |
| Fossegrim | Rune Book, pennies | **Fiddle String** | Strings for the top bows; a fiddle for the Haven (comfort and festivals). In the old tale the fossegrim teaches the fiddle for an offering of meat, which could be a raid event |
| Black Widow | Bog Venom | **Widow Silk**, keeping the venom | Tailor: a quiet silk shirt (less noise, for the stealth game). Dock deep lines. A Lasso upgrade |
| Mistwing | Bone | **Mistwing Feathers** | Fletching for every tier of arrows at the Hunter's Lodge; a down quilt for beds (comfort) |
| The Heartroot (boss) | A Root Guardian's drop | **Heartwood of the First Guardian**: unique, once per win | The Elder Tree's next level; Woodcutter's Lodge V; a Heartroot Amulet upgrade |
| Old Moss Troll, Stone Troll, Berg-jutul | The same Trollstone table | A **trophy each**: Moss-troll Tusk, Stone-troll Knuckle, Jutul Heartstone, keeping Trollstone | The boss tokens GDD §13.5 asks for: portal tier IV, Workshop V, Smithy V, skill rank III. Mounted in the Haven for renown |
| Draug | Grave Iron, pennies | Add **Barrow Goods** (a brooch, a grave-ring) | Breaks down into pennies and grave iron, or sells: an ARC recyclable in Nordic dress |
| Undead Knights | Grave Iron, pennies, barding | Add an **elemental core**: Smoulder Ember, Rime Core, Bog Rot | Fire Arrows II, Frostward Draught, a better Antidote, resistance enchantments of their element |
| Stag | Hides, bone | Add **Antler** | Tool handles, a Hunter's Lodge trophy mount, an antler decoration |
| Elite wolves | As wolves | **Pack Leader's Pelt** | A winter cloak at the Tailor (cold) |

**Do these first:**
- the three Grey Stone swaps;
- the four Rune Book creatures;
- the Black Widow and the Mistwing;
- the Heartroot;
- the three troll trophies.

Those 12 creatures account for most of the "they all give the same" problem.

**How you kill it (optional, needs code).** The game already knows the strike that killed a creature (blunt, slash or stab) and
whether it severed or smashed:
- a Stone Troll killed with a hammer cracks out a **Granite Hide** for the Stone Wall II;
- a deer taken unseen gives an unmarked hide worth more to the Tailor;
- severing a troll's tusk guarantees its trophy.

This is Monster Hunter's part-break, on systems that exist. It means conditional entries on `ItemDropTable`.

**Chests and places:**
- **Hunter's cache:** hides, arrows, bait, a trap.
- **Smith's chest:** ingots, a tool, a repair kit, and junk that breaks into metal (a broken sickle, a bent horseshoe).
- **Herb-wife's basket:** herbs, draughts, honey, seeds.
- **Barrow chest:** barrow goods, pennies, inscriptions, grave iron.
- **Traveller's pack:** linen, food, pennies, a knowledge find.
- **Troll hoard** (Troll Valley, behind its guard): trollstone, the Idol, stolen gear, Heart of the Forest at a better chance.
- **Ruined seter** (Troll Valley): a cheese mould, an old churn, linen, a broken scythe that gives iron.

Move the four low-tier gear pieces out of the general cache into the kinds they suit.

---

## 9. More things to make

Every new recipe below uses a part that today has few sinks, or one proposed in section 8. "Data" means a recipe asset and an item;
"code" means a new behaviour.

| Station | New recipes | Tier | Uses | Touches |
|---|---|---|---|---|
| Hunter's Lodge | **Arrows by tier:** fletched (Mistwing feathers), barbed fang arrows (bleed), venom arrows (Bog Venom), grave-iron bodkins (armour, draugs), moonsilver arrows (the dead, at night), trollstone-tipped (stagger) | II-V | Feathers, Wolf Fang, Bog Venom, Grave Iron | Data; most effects already exist as status effects |
| Hunter's Lodge | **Snares** at home (game caught while you raid); **scent musk** (creatures notice you later); **trophy mounts** (stag head, troll skull: renown) | II-III | Boar Tusk, Antler, trophies | Data and small code (musk, snares) |
| Tailor's Shop | **Bog waders**, **widow-silk shirt** (quiet), **pack-leader cloak** (cold), a **quiver** (more arrows) | III-IV | Bog-troll Hide, Widow Silk, Pelt | Data (stats exist: noise, cold, speed) |
| Woodcutter's Lodge | **Barkbound Shield** (Rootling Bark); **shaman's staff** (Burl); **Elderwood furniture** that gives comfort | II-IV | Rootling Bark, Burl, Elderwood | Data |
| Workshop | **Smoke pot** (bark and charcoal: breaks sight, like ARC's smoke grenade), **caltrops** (tusk and grave iron: slows pursuers), **bait bundle** (meat and honey: draws creatures away), **wisp lantern** (needs no fireflies) | II-IV | Rootling Bark, Boar Tusk, Grave Iron, Wisp Ember | Code, small to medium for smoke and caltrops; data for the lantern |
| Smithy | **Grave-iron helm and axe** (chilling hit); **silver band** (melted pennies or Tusse trinkets); **gilt** from the Idol | III-IV | Grave Iron, pennies, Idol | Data |
| Cauldron | **Moss Poultice**, **knot draught** (curse resistance), **ember, rime and rot draughts** (resistances from the knights' cores); a third Radiant draught | II-IV | Troll Moss, Hag's Knot, cores, Radiant Life Orb | Data |
| Wainwright's Yard | **Iron-shod handcart** (more cargo), a **sled** for winter, a **horse cart** | III-IV | Iron, Hardwood, Bog-troll Hide | Data and some code for new transport |

**Station levels that ask for a named part.** All data; each part comes from the raid tier below.

| Level | Costs today | Add | Fallback |
|---|---|---|---|
| Hunter's Lodge II | 16 Oak Plank, 8 Rough Hide, 4 Copper Ingot | 4 Wolf Fangs, 2 Boar Tusks | Trading Post: fangs |
| Cauldron II | 12 Wood, 12 Grey Stone | 3 Bog Venom, 2 Wisp Embers | |
| Woodcutter's Lodge III | 12 Hardwood Plank, 6 Iron Ingot, 8 Nettle Cord | 8 Rootling Bark | Haven asks also want bark |
| Tailor's Shop III | 12 Hardwood Plank, 8 Linen, 4 Thick Hide | 3 Widow Silk, or Huldra Silk until the widow has silk | Trading Post: silk |
| Watch Post III | 12 Hardwood Plank, 6 Iron Ingot | 3 Wisp Embers ("a light that won't go out") | |
| Smithy IV | Moonsilver, Granite Block, Deepstone, 1 Heartroot | 6 Grave Iron | Old Barrow event |
| Workshop V, Smithy V | Trollstone | The matching troll trophy | Any great troll's trophy counts, so it isn't one kill on luck |

---

## 10. Suggestions ranked by value for effort

| # | Suggestion | Value | Effort | Touches |
|---|---|---|---|---|
| 1 | **Fix the bugs:** Blight Boar bone; Leggings category; check and close Cursed Essence at home | Medium | Very small | Data: BlightBoar prefab, the Leggings asset. One line in `MobSpawnZone` (set `WithholdsEssence` at home), if confirmed |
| 2 | **One named creature part in 5-7 station levels**, each with a fallback (section 9) | High | Small | Data: `Data/Buildings/*.asset`, `TradeSettings`. Builders are retired, so edit the assets directly |
| 3 | **Give the thin-use parts a second and third use:** Grave Iron, Rootling Bark, Boar Tusk, Runestone Fragment, Radiant Life Orb | High | Small | Data: recipe costs and new recipes |
| 4 | **"Used for" on every item, and wanted items marked** (asks and a pinned upgrade) | High | Small | Code: `ItemTooltipView`, `SettlementState`, the pickup message |
| 5 | **A part of their own for the 12 creatures that share tables** (section 8) | High | Medium | Data: about 10 new items, icons from LocalSpawn, `LootOnDeath` on prefabs and the matching `MonsterDefinition` drops (keep the two equal, or the journal lies, as with the boar) |
| 6 | **Themed caches and troll hoards** | High | Medium | Data and prefab variants of `SupplyCache`; Troll Valley layout (plan step 6) |
| 7 | **Break gear down at home**, shown before confirming | Medium-high | Small to medium | Code: reuse `Repairs`; add the field to `ContentAudit.ItemSources`; it needs an `ItemDelivery` route |
| 8 | **Valuables with a second use:** return the Heart, melt the Idol, Runestones to Liv | Medium | Small to medium | Data, plus a small "offer" interaction |
| 9 | **Pair essence with parts:** skill rank III, empower +3, great tiers | Medium-high | Small to medium | Code: per-skill and per-kind item costs beside `EssenceSettings` (buildings are data). Then fold or give jobs to the minor bars |
| 10 | **New recipe lines** at the Hunter's Lodge, Tailor, Lodge and Yard (section 9) | Medium-high | Medium | Mostly data; smoke and caltrops are small code |
| 11 | **How you kill it decides the part** | Medium | Medium | Code: conditional `ItemDropTable` entries read from the killing blow |
| 12 | **A long Haven project**, and Value on one scale | Medium | Medium-large | Code; do this last |

Items 1-4 can be tried in a day or two and tested in one playtest, before any art is made. Items 5-6 give the biggest change in
how drops feel.

---

## 11. Questions for you

1. **Should creature parts stay raid-only?** The Haven world's outer bands make most of them huntable at home today. That softens
   luck, but weakens "only raids give". The same question applies to Cursed Essence there.
2. **How many materials are too many?** There are 54 today. Section 8 adds about 10. I'd hold the line by swapping generic drops
   rather than adding to them.
3. **Breaking down: gear only, or a handful of junk finds too?** Junk finds would be broken sickles and barrow goods replacing raw
   ingots in caches.
4. **Swapping current drops** (Grey Stone off the troll-folk, the Rune Book off four creatures) changes existing data. Do you want
   that, or should the new parts be added beside the old ones?
5. **Should the Trading Post stay narrow?** It buys only four valuables and Fine or Masterwork gear, and sells a little stock of
   materials. Or should it also buy surplus parts at a low rate? ARC's lesson leans narrow.

---

## Sources

**ARC Raiders**
- ARC Raiders Wiki, Loot: https://arcraiders.wiki/wiki/Loot (read)
- ARC Raiders Wiki, Workshop: https://arcraiders.wiki/wiki/Workshop (read)
- ARC Raiders Wiki, Blueprints: https://arcraiders.wiki/wiki/Blueprints
- ARC Raiders Wiki, Traders: https://arcraiders.wiki/wiki/Traders
- ARC Raiders Wiki, Projects: https://arcraiders.wiki/wiki/Projects
- ARC Raiders Wiki, Loadout: https://arcraiders.wiki/wiki/Loadout
- Recycling, salvaging and selling: https://allthings.how/arc-raiders-recycling-recycle-salvage-and-sell-items-efficiently/
- Workshop upgrades and tracking: https://allthings.how/arc-raiders-workshop-upgrades-steps-tracking-and-materials/
- Kotaku, inventory and stash cheat sheet: https://kotaku.com/arc-raiders-inventory-full-stash-loadout-crafting-cheat-sheet-2000643629
- Vice, why the auction house was scrapped: https://www.vice.com/en/article/arc-raiders-devs-experimented-with-auction-house-style-trading-here-is-why-they-scrapped-it/
- FRVR, the first Expedition's "dark pattern": https://frvr.com/blog/arc-raiders-dev-says-the-games-first-expedition-created-a-dark-pattern-as-players-hoarded-wealth-but-this-system-will-change
- Gfinity, Free Loadouts and "gear fear": https://www.gfinityesports.com/article/arc-raiders-free-loadouts-could-be-nerfed-in-future-updates
- Embark, The Fourth Expedition: https://arcraiders.com/news/the-fourth-expedition
- Embark, Live Update 2.3.0: https://arcraiders.com/news/live-update-2-3-0
- Embark FAQ, the Expedition project: https://id.embark.games/arc-raiders/support/faq/157-expedition-project
- Shacknews, Expedition requirements: https://www.shacknews.com/article/146727/all-expedition-requirements-in-arc-raiders
- Insider Gaming, Expeditions paused until 2027: https://insider-gaming.com/arc-raiders-cancels-expeditions-until-early-2027/
- PC Gamer, blueprint drop rates up after Cold Snap (search summary only): https://www.pcgamer.com/games/third-person-shooter/arc-raiders-players-say-blueprint-drop-rates-have-exploded-after-cold-snap-update-blueprints-everywhere
- PC Gamer, blueprint drop rates brought back down (search summary only): https://www.pcgamer.com/games/third-person-shooter/arc-raiders-patch-brings-blueprint-drop-rates-back-down-to-earth-but-embark-says-theyre-still-higher-overall-than-before
- GamesRadar, the stash debate (search summary only): https://www.gamesradar.com/games/third-person-shooter/arc-raiders-players-were-already-grumbling-and-after-another-patch-of-nerfs-a-fierce-debate-brews-crafting-ui-is-definitely-a-mess-but-should-the-stash-actually-get-buffed
- Rock Paper Shotgun, review (search summary only): https://www.rockpapershotgun.com/arc-raiders-review

**Other games**
- Tarkov Forge, Medstation requirements (read): https://tarkovforge.com/hideout/medstation
- Valheim bosses, summons and rewards (read): https://allthings.how/every-valheim-boss-in-1-0-locations-summons-and-rewards/
- Screen Rant, Valheim's Artisan Table (search summary only): https://screenrant.com/valheim-craft-artisan-table-uses/
- Darkest Dungeon Wiki, Heirlooms (read): https://darkestdungeon.wiki.gg/wiki/Heirlooms
- Monster Hunter Wiki, MHWorld materials (search summary only): https://www.monsterhunterwiki.org/wiki/MHWorld/Materials
- Monster Hunter World weapon and armour upgrades, including the wishlist (search summary only): https://www.accelerated-ideas.com/news/monster-hunter-world-weapon-and-armour-upgrade-guide.aspx
- Engadget, Diablo III Loot 2.0 (search summary only): https://www.engadget.com/2014-02-26-diablo-iiis-loot-2-0-patch-tweaks-difficulty-drops-and-more.html
- Diablo Wiki, Loot 2.0 (search summary only): https://di.diablowiki.net/Loot_2.0
- PoE Wiki, Currency (search summary only): https://www.poewiki.net/wiki/Currency
- Gamepressure, ExileCon interview with Grinding Gear Games (search summary only): https://www.gamepressure.com/editorials/exilecon-interview-with-path-of-exile-2-developers-grinding-gear/z2675
- Twinfinite, Grounded's resource analyzer (search summary only): https://twinfinite.net/guides/grounded-resource-analyzer-charges-how-get/
- Shacknews, Grounded raw science (search summary only): https://shacknews.com/article/119528/how-to-get-more-raw-science-grounded
- Gamer Tweak, Subnautica scanner room fragments (search summary only): https://gamertweak.com/scanner-room-fragment-subnautica/amp/
- Prima Games, Hades items (search summary only): https://primagames.com/gaming/hades-items-guide-nectars-keys-ambrosia-titan-blood-and-more/2
- Dark and Darker Wiki, Quest Item (search summary only): https://darkanddarker.wiki.spellsandguns.com/Quest_Item
- Dark and Darker Wiki, Trading (search summary only): https://darkanddarker.wiki.spellsandguns.com/Trading
- Deep Rock Galactic Wiki, The Forge (search summary only): https://deeprockgalactic.wiki.gg/wiki/The_Forge

**Design writing**
- Chris Grey, "Emotions and randomness: loot drops", Game Developer (read): https://www.gamedeveloper.com/design/emotions-and-randomness-loot-drops
- PC Gamer, "I am begging you to stop adding loot to games that don't need it" (search summary only): https://www.pcgamer.com/i-am-begging-you-to-stop-adding-loot-to-games-that-dont-need-it/

**In the project**
- Design docs: `Docs/GameDesignDocument.md` (§5, §7.7, §12, §13.5, §13.6, §14), `Docs/BaseProgression.md`,
  `Docs/GearScoreAndRaidTiers.md`, `Docs/EnchantingResearch.md` §1.7 and §1.10, `Docs/DesignReview_2026-11-13.md`,
  `Docs/TrollValleyPlan.md`.
- Architecture: `Assets/_Project/Scripts/ARCHITECTURE.md`: "Loot", "Essence", "What only raids give", "Raids with a reason from
  home", "Base building", "Storage".
- Data: `Data/Items`, `Data/ResourceInventory`, `Data/Crafting/Recipes`, `Data/Buildings`, `Data/Monsters`, `Data/Raids` (both raids,
  `SpawnTables`, events), `Data/Settlement/Requests`, `Data/World/Safe Haven/Haven World Recipe.asset`, `Resources/TradeSettings`,
  `Resources/EssenceSettings`, `Resources/SettlementSettings`.
- Prefabs: `Prefabs/Enemies/*` (`LootOnDeath`) and the `SupplyCache` prefab.
- Code: `Items/LootOnDeath.cs`, `Settlement/TradeSettings.cs`, `Settlement/HomeHunting.cs`, `Level/MobSpawnZone.cs`,
  `Durability/Repairs.cs`, `UI/ItemTooltipView.cs`, `Editor/Content/ContentAudit.cs`.
- Playtest logs: `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Safe Haven\Playtests`. The real runs are 20261114-122242,
  20261114-232848 and 20261115-184337.
