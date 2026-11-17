# Raid identity: what makes each raid its own (research, 2026-11-17)

You asked: "Research how to make the new raids and loot drops unique and implement suggested solutions. Add new necessary items, quests and
story lines as needed." Earlier you said each raid should feel unique "like how Valheim and Arc raider does it. Some things can be global,
but some need to be unique", that tiers should be "somewhat unique, with higher tier loot and higher quantity of drops", and that
materials should be "enough so it feels varied, but everything should have some purpose".

This is the research and the proposal. Nothing in the project was changed except this file.

**How this was made.**
- **The project.** I read the design document (§4, §7-§10, §13, §16, §17), [Troll Valley's plan](TrollValleyPlan.md), the
  [Moss Marsh research](NextRaidResearch_2026-11-17.md) and its decisions, the [loot research](LootAndDropsResearch_2026-11-17.md),
  [LootTables.md](LootTables.md), the quest and story docs, ARCHITECTURE.md, and the raid, rumour, event, quest, item and building data.
- **The novel.** *The Road That Remembers*, all 31 chapters, and the review of it.
- **The web.** Game wikis and developer posts, and the Norwegian encyclopaedia (SNL) for the folklore. "(snippet only)" means I could
  read only a search result, not the page.

---

## Decisions (the user, 2026-11-17)

Answers to §8's first five questions (6 to 9 are still open):

1. **The Brukam is the old troll**, and Act II's "The Toll" is his fight or payment at his bridge: "we can start with that".
2. **Troll Valley's Woods creatures and events:** "not sure, you decide". Decided: keep them, rare (as built: under 5% of the
   creature lists, none of the Woods-only events or rumours, the Huldra Silk and Elderwood nodes at weight 0).
3. **Trollstone is mainly the Valley's, at the high tier**: the Woods keep only the Old Moss Troll's, and their elites' row goes.
4. **Yes:** the Rime-jutul is the Frost Peaks' "mountain ruler", and the antlered Jaw-keeper a toll-taker who is never fought.
5. **Yes:** the Jaw-keeper's toll is a companion's memory, which comes back at the First Hearth.

- **Q6–9 (the user, 2026-11-17 evening):** 6. a simpler apex for the Peaks first (The Ride later); 7. yes, Sigrun is rescued in the
  Peaks by the red thread and teaches the fur gear; 8. yes, the Peaks at tiers IV and V once the Trollstone kit is measured; 9. yes,
  one knight per raid: Fire in the Valley, Toxic in the Marsh, Ice in the Peaks.

## The short answer

- **Troll Valley is still mostly the Misty Woods in its data.** The scene, valley shape, themed caches and the Brukam's table are its own,
  but its events, rumours, known enemies, likely finds, tier briefings, stage messages, resource nodes and half its creatures are the
  Woods'. Fixing that is data only, and it is the biggest gain for the least work.
- **Three trophies lead nowhere.** No recipe or building level asks for Brukam's Tusk, the Lindorm Scale or Nykk's Mane yet.
- **One rule per raid, which changes how you play it:**
  - Misty Woods: the forest is annoyed by gathering, and the mist hides (built).
  - **Troll Valley: pay or fight.** Trolls hold the crossings and ask a toll; noise wakes the troll-stones; at dawn the trolls are stone.
  - **Moss Marsh: water is the ground.** Wading is fast and loud, the deep belongs to the Lindorm, and you get Soaked (your decisions).
  - **Frost Peaks: the mountain takes your warmth.** You move from fire to fire along the cairns, and turning back in time is the skill.
- **Global and unique loot, the Darkest Dungeon way.** Metals, pennies, essence, hides, inscriptions and relics stay global. Each raid
  gives one gatherable, two or three creature parts, a trophy, one or two cache kinds and one piece of knowledge that nowhere else gives.
- **Tiers stay one ladder** (copper, iron, moonsilver, trollstone), but each raid's top tier pays something of its own for tier V:
  Trollstone from the Valley, the Lindorm Scale from the Marsh, pelts and rime cores from the Peaks.
- **Each raid prepares you for the next** (Valheim's lesson): the Valley's birch bark tars the Marsh's waders, the Marsh's Lindorm Scale
  keeps you dry in the Peaks' cold.
- **About ten new items in all**, most of them parts that replace something generic.
- **Quests:** Act II (already outlined) runs through the Valley, and short chains of three or four quests send you to the Marsh and the
  Peaks for reasons from home: Gunnar's Nøkk, Eirik's sister, the man in grey and the First Hearth.
- **Frost Peaks, in one line:** the high pass of the novel (the Wolf's Jaw with its bell, the Door in the Snow, the First Hearth), cold
  as its rule, the Oskorei as its apex, a rime-jutul as its "mountain ruler", and ice that gives back what the mountain kept.

---

## 1. What strong games do, and what to take

| Game | What makes a place its own | What to take |
|---|---|---|
| **Valheim** | The Mountains freeze you: health and stamina recovery stop. A frost-resistance mead (from earlier biomes) is the first answer, a wolf cape (from the Mountains' own wolves and silver) the lasting one. Being wet cancels frost resistance. Silver is hidden until the Swamp boss's Wishbone shows it (snippets: [Prima Games](https://primagames.com/gaming/valheim-silver-wolf-armor-frost-resistance-guide), [eip.gg](https://eip.gg/valheim/guides/10-tips-to-manage-the-mountains/)) | One hazard per place, answered first by gear from the place before and then by gear from the place itself. A boss hands you the key to the next place |
| **ARC Raiders** | One map changes with its condition: Night Raid, Electromagnetic Storm, Hidden Bunker, Locked Gate (four key cards). "Minor conditions add events that can increase particular types of loot"; major ones raise difficulty and loot ([arcraiders.wiki Maps](https://arcraiders.wiki/wiki/Maps), [Embark](https://arcraiders.com/news/new-map-conditions)) | Conditions rolled per run that change a map's rules and its loot. Keys that open a locked place |
| **Darkest Dungeon** | Four heirloom kinds. "Each location tends to drop one type more than others": busts in the Ruins, portraits in the Warrens, deeds in the Weald, crests in the Cove, and every building upgrade needs crests plus a regional kind ([wiki.gg](https://darkestdungeon.wiki.gg/wiki/Heirlooms)). Each region has its own curios and the provision that opens them: holy water in the Ruins (snippet only, [GameSkinny](https://www.gameskinny.com/tips/darkest-dungeon-curios-guide/)) | **The model for global vs unique:** one global currency every upgrade wants, plus one regional kind per upgrade line. A region's things open with a region's tool |
| **Deep Rock Galactic** | The Glacial Strata need "temperature management to prevent freezing": warm vents, cold vents, blizzards with a warning 15 s ahead, slippery ice. Each biome has one crafting mineral in plenty and one scarce, while mission ores depend on the mission, not the biome ([wiki.gg](https://deeprockgalactic.wiki.gg/wiki/Glacial_Strata)) | Warm and cold spots on the map, a weather warning before a storm, a resource tilt per place, and some things global |
| **Monster Hunter World** | Hoarfrost Reach drains stamina in the cold. A Hot Drink, brewed from peppers that grow there, answers it, and hot springs warm you (snippets: [fextralife](https://monsterhunterworld.wiki.fextralife.com/Hot+Drink), [monsterhunterwiki](https://www.monsterhunterwiki.org/wiki/Hoarfrost_Reach)) | The answer to a place grows in that place |
| **Hunt: Showdown** | The rule is that "the gear you need will be around the boss area"; Scrapbeak breaks it by carrying the supplies on his back ([Crytek](https://huntshowdown.com/news/meet-scrapbeak-our-new-boss)) | A boss arena with its own rule, and one boss that breaks it on purpose |
| **Escape from Tarkov** | Labs needs a keycard found in other maps; locked rooms need map keys (snippets: [Insider Gaming](https://insider-gaming.com/how-to-get-into-the-lab-escape-from-tarkov-keycard/), [Dot Esports](https://dotesports.com/escape-from-tarkov/news/how-to-get-and-use-the-red-keycard-in-escape-from-tarkov)) | A find from one raid that opens something in another |
| **Dark and Darker** | The frost floors have their own creatures (a yeti, ice hounds), falling ice and a Frost Wyvern whose claws are a crafting part (snippets: [wiki](https://darkanddarker.wiki.spellsandguns.com/Frost_Wyvern)) | Frost variants of known creatures plus a few of its own |

**Six rules, taken from these:**
1. **One rule per raid**, readable in the first minute, with a counter you can prepare.
2. **The first answer comes from the raid before; the lasting answer comes from the raid itself.**
3. **One global kind, one regional kind** in every upgrade line (Darkest Dungeon's crests and heirlooms).
4. **The boss opens the next raid**, and its trophy is spent on something that matters at home.
5. **A tilt, not a wall:** a raid has more of some global things and less of others, and a few things nowhere else.
6. **Conditions vary a raid** from run to run (ARC). Time of day already does this; region conditions can come later.

---

## 2. Where the raids stand today

| Raid | What is its own | What is not yet |
|---|---|---|
| **Misty Woods** (I-IV) | Mist, the forest's annoyance, the Old Moss Troll, the Heartroot, Woods finds, Fog Bank, Old Smithy | Nothing to fix here |
| **Troll Valley** (II-IV) | Scene, valley shape, river, bridges (being built), birches, Troll Hoard, Seter Chest, Brukam's Hoard, Valley finds, troll-folk parts, the Brukam (monster and table) | Still the Woods' (`Data/Raids/TrollValley.asset`): all 10 events (including Cursed Tree, Thickening Fog, Beacon Light); all 17 rumours (including Rot's Heart); known enemies (Small Moss Troll, Draug, Shieldbearer, Root Guardian, Old Moss Troll); likely items (Healing Herb, Copper Ore, Oak Wood, Berries, Nettle Fiber); the three tier briefings ("Root Guardians wake, Moss Shamans call them on", "The woods at their worst"); stage messages ("The woods give more…"); every resource node, including Huldra Silk and Elderwood; and its creature list holds Rootlings, Root Guardians, Moss Shamans, Marsh Lights and Bog Spitters. Its elites give exactly the Woods' rows. No Dawn yet. The Brukam has no `BossDefinition` |
| **Moss Marsh** (III-IV) | Full research and your decisions; loot tables ready (Nykk, Lindorm, Nøkk, Drowned Hoard, Peat Cutter's Box, Deildegast, Utburd, Marsh finds) | No raid asset or scene, no quests, and its two new parts lead nowhere |
| **Frost Peaks** (IV-V) | Cold exists, but only from the season (`PlayerWarmth`); Frostward Draught, Warming Brew, Ice Knight, Frostbound, Rime-Touched | Everything else |

**Contradictions to fix:**
- **Trollstone.** Troll Valley's description says "Trollstone is found only here", but the Woods' tier IV elites (25%) and the Old
  Moss Troll give it too.
- **Trophies with no use:** Brukam's Tusk, Lindorm Scale and Nykk's Mane. No recipe or building level asks for them. The Jutul
  Heartstone (Tanning Rack), Stone-troll Knuckle (Forge) and Moss-troll Tusk (Construction Table) are used.

---

## 3. Each raid's identity

### 3.1 Troll Valley: what to add (the plan stands)

| Part | Planned already | Add |
|---|---|---|
| **The rule** | Bridges as choke points with guards; "The Sleepers"; a Dawn hour | **Pay or fight.** Each bridge's guard sits across the deck and asks a toll. **Pay**: the price is shown and grows by tier (II: 3 pennies or 2 bones; III: 6 pennies or a Tusse's Trinket; IV: a Gilded Troll Idol). You cross, and it tells you one thing (Reveal Map). **Riddle**, once a raid, as the Toll Troll does. **Fight**: the guard and the rocks beside it get up. **Or ford** elsewhere: slow, and loud once wading noise exists. The Brukam is the great toll at the last bridge. This gives pennies, trinkets and bones a use on the road, and makes "what's in my purse" part of the loadout. **Noise wakes stone:** troll-rocks near a fight or a mining strike get up (The Sleepers as a standing rule, not only an event). **Dawn:** the trolls are stone; the raid is quieter and pays less |
| **Only here** | Trollstone, stone, iron in the scree, birch timber, troll hoards | **Birch Bark** from the valley's birches (new; §5). Trollstone mostly from here (§8 Q3). The Brukam's **Mountain Leap** (GDD §10.7). The Stone-troll Knuckle and Brukam's Tusk as the valley's trophies |
| **Caches** | Troll Hoard, Seter Chest, Brukam's Hoard | No more: the Smith's Chest suits the iron in the scree, and the Herb-wife's Basket is global |
| **Creatures** | Stone Troll and Small Moss Troll common; Troll Runt and Tusse thieves; Trollkjerring at lairs; Bog Troll in the river marsh; Berg-jutul from III; few wolves | Take the Woods' own out: Rootlings, Root Guardians, Moss Shamans and Marsh Lights (or keep them under 5%). The **Fire Knight** becomes the valley's knight (each raid one knight: §4) |
| **Apex and boss** | Berg-jutul as apex; the Brukam at the bridge | The Brukam's `BossDefinition` and phases (GDD §10.7), reached by an urgent rumour as the Heartroot is. **The Brukam is the old troll** of Act I that broke the stones, and Act II's "The Toll" is the fight or the payment at its bridge (§8 Q1) |
| **Set pieces** | Troll Bridge, Petrified Trolls, Ruined Seter, Troll Ting, Troll Lair | From the novel (chapters 24-25), for Act II: **The Narrow Road** (a ravine where voices of your own people call) and **The Room Beneath the Road** (a stone arch with a brass button stamped with a bear, a child's wooden horse, a blue door: the man in grey's past) |
| **Events** | The Sleepers | A Valley pool: Toll Troll (on a bridge), The Sleepers, **Borrowed Voices** (a companion's voice calls from a ravine: follow it and find an ambush, or answer with what only they would say and it falls silent), **The Troll Ting** (trolls gather at the stones at night; from tier IV), **Rockfall** (exists), and the global ones (Pedlar, Wagon, Three Wishes, Living Stones, Old Barrow, Oskorei in winter). Out: Cursed Tree, Thickening Fog, Beacon Light |
| **Stages** | – | "Stones Stir" 8:00, "The Trolls Wake" 4:00, "Rockfall" 1:00, in the valley's words |
| **Tiers** | – | **II:** runts and tusse at the bridges, stone trolls few, iron in the scree, seter chests. **III:** stone trolls at the bridges, Trollkjerring lairs with hoards holding moonsilver, a Sleeper wakes, Berg-jutul rare, the Brukam offered. **IV:** the Troll Ting gathers, Sleepers wake in pairs, the Berg-jutul walks the apex round, hoards hold Trollstone, two affixes on elites. **Elites:** iron (II), moonsilver (III), Trollstone at 40% (IV) and Granite, so the valley's top tier is the place for Trollstone |

### 3.2 Moss Marsh: what to add (the research stands)

The research covers the rule (water bands, wading noise, Soaked, reeds that hide, the Fen Rises), the creatures (the Nykk, the
Lindorm, the Nøkk of the Black Pool, the utburd, the deildegast), the sites and the events. What is missing or weak:

1. **No story or quests send you there.** §6 adds a short chain: the Black Pool, Gunnar's Nøkk, the boss's bargain.
2. **Its parts lead nowhere.** Nykk's Mane and the Lindorm Scale need uses (§5).
3. **The Waders need a second material.** Birch Bark from the Valley tars them, so the Valley prepares you for the Marsh.
4. **It overlaps the Woods.** Marsh Lights and Bog Spitters are the Woods' creatures. Keep them low and let the water creatures carry
   it; the **Toxic Knight** ("a dead rider out of the bog") becomes the marsh's knight.
5. **Its breach.** "Bog and Bone" is a breach the Haven already knows. Let its source gather in the fen (§7, item 8).
6. **What only it gives at the top.** Tier IV: the Lindorm Scale for a tier V piece of its own, the Scale Cloak (§5).

### 3.3 Frost Peaks: a full proposal

**Where and when.** The fourth and last raid, tier IV and V. In the novel, the stair under the black pool leads to the First Hearth in
the mountain's hollow centre (chapters 27-28). In the game, that stair comes up in the snow at **the Wolf's Jaw**, the pass under the
peaks (chapter 19). So beating, or bargaining with, the Nøkk opens the Peaks, as the Heartroot opened the Valley.

**The land.** A high pass above the tree line: bare fell, boulder fields, snow on the high ground, a frozen tarn. Places:
- **cairns** (*varder*) marking the old road;
- a **stone shelter** at the top;
- an **ice patch** that has drawn back from the old pass;
- a **high seter** with smoke rising from it;
- **reindeer pits** dug long ago;
- **shrines** cut into the rock (GDD §4.3);
- the **Jutul's Cut**, a gorge.

It is modelled on the real Lendbreen pass in Jotunheimen: about four miles above 6,000 feet, "marked only by ancient cairns", with
the foundations of a stone shelter. As the ice melted it gave up more than 800 things, among them shoes, arrows, a rune stick, a mitten,
a sled and a snowshoe for a horse ([Scientific American](https://www.scientificamerican.com/article/melting-ice-reveals-a-lost-viking-era-pass-in-norways-mountains/)).
"The ice gives back what the mountain kept" is the forgetting run backwards, which fits the book's title.

**Folklore at its core.**

| Belief | What the sources say | In the raid |
|---|---|---|
| **Jutul** | Lives alone in a cave inside a mountain, strides valleys, hurls rocks at churches, turns to stone when the sun shines on it; the word may come from *eta*, "to eat" ([SNL](https://snl.no/jutul)). Rendalsjutulen cut a gorge to steal a river ([SNL Jutulhogget](https://snl.no/Jutulhogget)) | **The boss: the Rime-jutul**, "the eater", the hunger's giant. Its arena is the Jutul's Cut |
| **Rimtusser** | The giants born of melted rime ([SNL](https://snl.no/rimtusser)) | Its rime armour, and the rime-touched dead from the ice |
| **Oskoreia** | A noisy ride of the restless dead at Yule. It carries people off and rides horses to exhaustion overnight. People hung steel over doors, and if you met it outdoors you threw yourself flat on the ground in a cross ([SNL](https://snl.no/oskoreia)) | **The apex: the Ride.** Dead riders sweep the cairn road at dusk and night. Leave the road, lie low, or stand in a shelter with steel over its door |
| **Hulder at the seter** | Herders asked the hidden folk's leave to use a seter; dairymaids spilt a little milk for them and those who did were rewarded with butter and cheese; she lures with a *lokk* ([SNL](https://snl.no/hulder)) | **The Hulder's Seter** event. The Huldra and its lure attack exist |
| **Bergtaking** | People taken into the mountain could be "rung out with church bells", and must not eat while inside ([SNL](https://snl.no/bergtaking)) | **"Rung Out of the Mountain"**, a Tale, with the novel's bells |
| **Varde** | Cairns can be "vital for safe travel" when sight fails. A *varde* was also a beacon lit to signal ([SNL](https://snl.no/varde)) | Cairn-beacons you light: warmth, a waymark in a whiteout, seen from afar |
| **The mountain code** | Rule 8: "Vend i tide, det er ingen skam å snu" (turn back in time; there is no shame in turning) ([SNL fjellvettreglene](https://snl.no/fjellvettreglene)) | The briefing's line, and exits that close from the top down |
| **Wild reindeer and pits** | Norway keeps Europe's last wild mountain reindeer. More than 50,000 pit traps are known, up to 1,200 on Dovrefjell (snippet only, [SNL fangstgraver](https://snl.no/fangstgraver)) | Herds that bolt and give you away; old pits as traps |

**The rule: the mountain takes your warmth.**
- **Cold from the place.** On the Peaks, cold comes from the height and the wind as well as the season:
  - mild on the low ground;
  - strong on the fell;
  - full on the high pass, a quarter more in the open wind;
  - more again in a blizzard.

  `PlayerWarmth` already does the rest: exposure builds, and Chilled slows you and cuts stamina.
- **Warmth comes from fire and stone:**
  - cairn-beacons you light (20 s to light; seen from 60 m, so creatures come);
  - stone shelters;
  - Warming Brew (cures Chill) and the Frostward Draught (resistance), which exist;
  - fur gear (§5).
- **Soaked doubles the cold** (as the Marsh research planned). It is the Valheim trap made readable: dry off at a fire before you climb.
- **It drains; it never hurts.** The Marsh research's rule holds: no ground both slows and wounds. Only in a tier V blizzard does a second
  step, Freezing, stop health from coming back (Valheim's Freezing).
- **Turn back in time.** The high exits close first when the weather turns; the Jaw, the low exit, stays open.

**What only it gives.**
- **Reindeer Pelt** and **Rime Core** (parts).
- The **Horse Snowshoe** (knowledge: the Sled).
- The **Rimeheart** (the boss's trophy) and the boss's ability.
- Ice-patch relics.
- The First Hearth.

**Caches.**
- **Ice Patch:** what the ice gave back, such as barrow goods, old arrows, pennies, a relic chance and, rarely, the Horse Snowshoe.
- **Cairn Cache:** a traveller's cache under a cairn, with a waterskin, a Warming Brew and pennies.
- **Shrine Offering:** take the inscription, or leave an offering for a blessing on the raid.

The Traveller's Pack and Hunter's Cache stay global.

**Creatures.**

| Creature | Role | Body |
|---|---|---|
| **Wild reindeer** | A herd that bolts and alarms like the birds; prey for pelts, meat and antler | The Deer and Stag rig with a reindeer look (LocalSpawn) |
| **Wolves, pack leaders** | Packs on the open fell; the leader carries a Reindeer Pelt | Exist |
| **Ice Knight** | The Peaks' knight (exists); drops a Rime Core | Exists |
| **Rime-touched Draug** | The dead the ice gives back: slow, cold to touch, rising from the ice patch at tier V | Draug, frost look |
| **Stone Troll, Berg-jutul** | Rare, on the boulder fields | Exist |
| **The Ride (apex)** | §3.3 folklore. A fast round along the cairn road at dusk and night | Undead Knights on the Toxic Horse's body. Effort L. Until it exists, a frost-dressed Berg-jutul can walk the round |
| **The Rime-jutul (boss)** | The "mountain ruler" (GDD §16.6) | The Berg-jutul's body, larger, with a rime look |
| **The Jaw-keeper** | Never a fight: the antlered stone guardian of the novel, the mountain's keeper of the pact, who takes a toll at the Jaw (§6) | New; a story figure, not a spawn |

**The boss, the Rime-jutul in the Jutul's Cut.**
- **Phase 1:** it hurls boulders at your fires, not at you, as the novel's guardian attacks the fire (chapter 19).
- **Phase 2:** the blizzard. Three cairn-fires round the arena keep you warm, and each lit fire thaws its rime armour so it can be hurt.
- **Phase 3:** dawn breaks through the gorge and it begins to turn to stone. Win before it sets, and its heart is yours. If it sets,
  the fight is won but the Rimeheart is locked in the rock; mining it gives Trollstone only. That is a race against the clock with a
  choice, which is Hunt's rule, bent.
- **Reward:** the boss sigil **Hearthguard**: plant a small ward-fire anywhere. It warms, steadies, and keeps the hunger's borrowed
  voices off for a while. That is the hearth-keeper's power, and the "advanced magical tier" GDD §16.6 asks for. Its **Rimeheart**
  opens the Purification Altar's top tier.

**Set pieces.** The Wolf's Jaw (cairn, bronze bell, strips of cloth with names), Cairn Line, Stone Shelter, Ice Patch, Reindeer Pits,
the Hulder's Seter, the Shrine in the Rock, the Door in the Snow (story), the Jutul's Cut (boss), the First Hearth (finale, fixed).

**Events.**
- **Whiteout:** at a danger stage, sight drops and the lit cairns show the way.
- **Avalanche:** a terrain event, like the rockfall.
- **The Ride:** where the apex doesn't exist yet, the Oskorei event re-dressed.
- **The Hulder's Herd:** ask leave and spill milk, and she gives butter and cheese; or the herd tramples through.
- **Thaw:** the ice patch gives up a find, but the ice is unsafe.
- **Rung Out of the Mountain:** the Tale.
- **The Snow Falls Upward:** the Door in the Snow is near, and the hunger speaks.

**Tiers.**
- **IV:**
  - cold only on the high ground;
  - the Ride by night only;
  - Ice Knights rare;
  - ice patches give barrow goods and arrows.
- **V:**
  - a blizzard at The Hunt stage;
  - the Ride at dusk too;
  - rime draugs rise;
  - Rime Cores common;
  - great inscriptions at the shrines;
  - the Rime-jutul offered;
  - more relics.

  A tier V raid needs a measured tier V kit first. The Trollstone armour now exists (helm, hauberk, gauntlets, girdle, boots), so read
  the tier's multipliers off it as GearScoreAndRaidTiers says.

**How it would be built.** As Troll Valley's: its own scene copied from the Valley's, plus:
- a **Pass** layout shape (a climbing route between walls, with shelves and a summit);
- snow cover chosen by height;
- avalanches as a terrain event;
- a `MountainColdSystem` (a new `RaidSystem` that gives `PlayerWarmth` a cold floor from height and wind);
- art from LocalSpawn: reindeer, cairn with bell, shelter, shrine, ice patch, the rime-jutul's look.

---

## 4. Global and unique loot

| | **Global** (any raid) | **Misty Woods** | **Troll Valley** | **Moss Marsh** | **Frost Peaks** |
|---|---|---|---|---|---|
| **Gathered** | Ore by tier (copper I, iron II, moonsilver III+), stone, herbs, Life Orbs | Elderwood, Huldra Silk, flax | Birch Bark, Granite in the scree, iron in the scree | Bog Myrtle, sphagnum (Troll Moss), cloudberries, bog-iron look | Moonsilver in the ice, frozen tarn fish |
| **Creature parts** | Hides, bone, meat, Wolf Fang, Grave Iron, Barrow Goods, pennies | Rootling Bark, Shaman's Burl, Wisp Ember, Mistwing Feathers | Hag's Knot, Tusse's Trinket | Bog-troll Hide, Widow Silk, Nykk's Mane, Bog Venom | Reindeer Pelt, Rime Core |
| **Trophies** | – | Guardian Heartroot, Moss-troll Tusk, Heartwood | Stone-troll Knuckle, Jutul Heartstone, Brukam's Tusk | Lindorm Scale | Rimeheart |
| **Caches** | Traveller's Pack, Hunter's Cache, Herb-wife's Basket, Smith's Chest, Barrow Chest | Fog Bank, Old Smithy | Troll Hoard, Seter Chest, Brukam's Hoard | Peat Cutter's Box, Drowned Hoard | Ice Patch, Cairn Cache, Shrine Offering |
| **Knowledge, abilities** | Inscriptions, relics, raid runes, rescues, the Lost, breach sources | Moonsilver Folio, Heartroot's Grasp, bees, seed, Elder sapling | Mountain Leap | Nykk's Air, the Halter | Horse Snowshoe (the Sled), Hearthguard |
| **Knight** | – | (a mix) | Fire Knight | Toxic Knight | Ice Knight |

**What decides global or unique:** anything every tier needs to move up (metal, essence, hides, pennies) stays global, so no raid
is a chore. Anything that answers a place, or marks having been there, is unique.

**The tier ladder per raid.** A raid of tier N still pays for tier N+1 gear in metal (GearScoreAndRaidTiers). What each raid adds:

| Raid tier | Misty Woods | Troll Valley | Moss Marsh | Frost Peaks |
|---|---|---|---|---|
| I → II gear | Copper | – | – | – |
| II → III | Iron; Rootling Bark, Burl | Iron; Hag's Knot (Ward Totem), Birch Bark | – | – |
| III → IV | Moonsilver; Guardian Heartroot | Moonsilver; Stone-troll Knuckle (Smithy V) | Moonsilver; Bog-troll Hide (Waders), Widow Silk | – |
| IV → V | Moonsilver; the Old Moss Troll's Trollstone and tusk | **Trollstone, the main source**; Jutul Heartstone; Brukam's Tusk | Moonsilver; **Lindorm Scale** (Scale Cloak) | **Reindeer Pelt, Rime Core** (the cold kit, Frostward Draught II) |
| V | – | – | – | Mastery: empowering +3 with Rime Cores, great inscriptions, relics, the Rimeheart (altar's top tier) |

"Higher tier, more and better" already holds: loot ×1.25 / 1.5 / 1.8 by tier, rows that drop only from a tier, more elites and
higher relic chances. The tables above add *what is new* at each tier, not only *how much*.

---

## 5. New items, and preparing for a raid

**New items (ten, including three the Marsh research already proposed).** Each has two uses or a building tier, and says what it replaces.

| Item | Raid | From | Uses | Replaces or merges |
|---|---|---|---|---|
| **Birch Bark** | Valley | Birch trees (a second yield, like a log), seter chests | **Tar Torch** at the Workshop (stays lit in rain and when Soaked); **Bog Waders** (with Bog-troll Hide); rowboat caulking at the Dock | Huldra Silk and Elderwood nodes in the Valley (they stay the Woods') |
| **Bog Myrtle** | Marsh | Hummocks | **Gruit Ale** at the Kitchen (hits harder, with a headache); a sprig wards off knott swarms | (Marsh research) |
| **Steel Needle** | Home | Smithy I: 1 Iron Ingot → 4 | Thrown into a pool, the Nykk can't use it; the boss arena's pools | (Marsh research) |
| **Bog Waders** | Home | Tailor III: 3 Bog-troll Hide, 2 Birch Bark, 2 Linen | Half the wading slowdown, quieter wading | (Marsh research; adds the bark) |
| **Scale Cloak** | Home | Tailor IV: 2 Lindorm Scale, 2 Widow Silk | Never Soaked, so frost resistance holds in the wet: the Peaks' first answer | Gives the Lindorm Scale its use, with a scale armour piece later |
| **Reindeer Pelt** | Peaks | Wild reindeer, pack-leader wolves | **Fur Cloak** and **Fur Hood** at the Tailor (frost resistance); fur bedding in homes (comfort) | The loot research's "Pack Leader's Pelt" |
| **Rime Core** | Peaks | Ice Knight (beside its drops), rime draugs, frost elites | **Frostward Draught II**; the Frostbound and Frostwarded enchantments' cost; empowering Rime-Touched | The loot research's elemental cores (Smoulder Ember and Bog Rot can follow for the other knights) |
| **Horse Snowshoe** | Peaks | Ice patch (rare; a rumour guarantees it) | Learned At Home: teaches the **Sled** at the Wainwright's Yard (fast on snow, slow on bare ground) | – |
| **Rimeheart** | Peaks | The Rime-jutul, once a win | The Purification Altar's top tier; mounted for renown | – |
| **Seter Cheese** (optional) | Valley | Seter chests | Food, and the Valley's Tale (§6) | – |

**Uses for the existing dead ends:**
- **Brukam's Tusk:** the top tier of a defence (GDD §16.6: the troll chieftain "unlocks … defensive ward upgrades"). The Watch Post's or
  the Ward Totem's last level, plus a mount for renown.
- **Nykk's Mane:** a bowstring for the top bows (beside the Fiddle String), and **Nykk's Halter** later (a tireless pale horse, Horses).
- **Lindorm Scale:** the Scale Cloak above, and a tier V scale shirt later.

**Preparing makes going a decision.**

| Raid | What to bring | Why it is a choice |
|---|---|---|
| **Troll Valley** | A blunt weapon (stone trolls are weak to blunt: the strike kinds exist); a toll purse of pennies, trinkets or bones | The purse is carry space; the hammer is not your best blade |
| **Moss Marsh** | Bog Waders; Steel Needles; Antidote; Tar Torches; a rowboat kit if carried in | Waders give up an armour slot; the boat takes cargo room |
| **Frost Peaks** | Frostward Draught or Warming Brew; Scale Cloak or fur; Tar Torches to light cairns faster; a Sled instead of the handcart | Cold gear over battle gear; the sled is poor on bare ground |

---

## 6. Quests and story lines

### 6.1 What the quest system offers (as built)

- **Kinds:** Main Story (gold), Rescue (teal), Haven (green), Tale (silver), Errand (bronze). Something happens in every Main quest.
- **Objectives:** deeds with assigned targets (Talk To, Reach Place, Use Object with a Quest Target), Kill Monster (a saved boss counts
  with Count Already Owned), Complete Raids *in one place*, Collect, Craft, Build, Upgrade Building, Store Item, Rescue Person, Learn
  Enchantment, Give Inscription, and Story Event for one-off moments.
- **Starts:** Automatically, On Quest Event, When Player Does, or offered by a Quest Giver (Side Offers in any order). A quest can be
  **Closed By** a later one.
- **From a raid:** a **rumour** (briefing line, site, set piece, conditions: After Home, Needs Tier, Needs Building, Until Beaten,
  urgent, hour, Seeks Lost, breach coming). **Events** offer choices with item costs and outcomes. **Haven asks** come from residents,
  are posted once a **Notice Board** stands, and can depend on the season. Bosses are reached through urgent rumours.
- **Pacing rules to keep:**
  - Haven, Tale and Errand quests are held for homecomings, at most two new per return.
  - Main and Rescue quests are never held.
  - The tracker shows one quest per colour.
  - Nothing is asked before it is taught (`QuestAuditTests`).
  - The man in grey stays unnamed, and so does his family.

### 6.2 Troll Valley: Act II, as outlined, with three changes

Act II, "The Doors" (QuestAndStoryResearch §3.7.2, quests 16 to 21), already runs through the Valley:
1. Where the Old One Walks.
2. A Door Broken.
3. The Man in Grey.
4. The Toll.
5. The Horn at Dusk (Sigrid).
6. What Sigrid Knows.

Keep it, with these changes:
1. **The Toll is the Brukam.** One troll, not two: the old troll that broke the stones is the Brukam at the great bridge. Fight it, or
   pay a Hearth Ember and Life Orbs, as you decided. Either opens the Marsh.
2. **The Man in Grey** follows his trail through the novel's places: the Narrow Road, where voices of your own people call, and the
   Room Beneath the Road (the wooden horse, the blue door), before his Cold Camp. Runa's line meets him there, as planned.
3. **One Tale and three asks.**
   - **Tale, "The Boy Who Ate With the Troll"** (Asbjørnsen and Moe: Askeladden squeezes a cheese and calls it a stone, then wins an
     eating match with a hidden sack ([SNL](https://snl.no/askeladden), snippet only)). It starts When Player Does: Reach Place
     *Troll Ting*. Choices:
     - squeeze a Seter Cheese: the troll gives up its hoard (Troll Hoard table);
     - take the eating match with a backpack in the bag: the same, and it bursts;
     - refuse: it fights.

     It uses an event's choices with item costs. Optional: it needs the cheese.
   - **Haven asks:**
     - Hild: 6 Granite ("stone that has stood");
     - Tor: 6 Birch Bark ("the boats leak");
     - Yrsa: an Old Churn ("butter, at last").

### 6.3 Moss Marsh: four quests (Act III begins)

| Quest | Kind, giver | Starts | Objectives (existing types) | What happens, reward |
|---|---|---|---|---|
| **The Black Pool** | Main, the portal | After What Sigrid Knows | Complete Raids *Moss Marsh*; Reach Place *The Black Pool* (urgent rumour); Use Object *The Black Pool* | Faces in the water call in borrowed voices (novel chapter 27). The portal: the pact's road north went down a stair here, and something will not let anyone pass |
| **The Nøkk's Tune** | Haven, Gunnar | After The Black Pool (held for a homecoming) | Talk To *the Haven's Nøkk* (a target at the tarn) at dusk; Catch Fish there | Gunnar has heard the tarn's Nøkk play. Listening opens the boss's bargain (the research's `SpiritBonds` idea) |
| **The Crying in the Fen** | Tale | When Player Does: Use Object at the utburd's site | Story Event: named | Naming it on a hope lays it to rest. It teaches that names have power, which the Nykk and the boss's third phase use |
| **Steel in the Water** | Main, the portal | After The Black Pool | Kill Monster *The Nøkk of the Black Pool* [owned] **or** Story Event *The Nøkk's Price* (its bargain, if The Nøkk's Tune is done) | Fight or bargain, as in Act II. The stair opens and the Frost Peaks unlock. Reward: Nykk's Air (boss sigil) |

**Haven asks:** Ingrid, 6 Troll Moss (poultices); Gunnar, 2 Nykk's Mane ("a line that won't part"); Asa, 3 Widow Silk. **Breach
source:** "Something gathers in the fen" for Bog and Bone.

### 6.4 Frost Peaks: five quests (Act III ends)

| Quest | Kind, giver | Starts | Objectives | What happens, reward |
|---|---|---|---|---|
| **The Wolf's Jaw** | Main, the portal | After Steel in the Water | Complete Raids *Frost Peaks*; Reach Place *The Wolf's Jaw*; Story Event *Toll answered* | The cairn, the bell, the strips of cloth with names. The **Jaw-keeper** asks "one memory, freely given". **Pay**: the companion with you forgets one memory (`ResidentRecord.Remembers` → Forgotten, the system the Lost use), and it comes back at the First Hearth. **Refuse**: it attacks your fire, not you; keep the cairn-fire lit for 60 s and it steps back with "Debt" (novel chapter 19). Either way the pass opens |
| **The Red Thread** | Rescue, Eirik | Rumour "A red thread on the snow" (After Home: Eirik; urgent) | Rescue Person *Sigrun* [home] | Eirik's sister, the twelfth person you approved, in a camp half-buried in snow (chapter 21). She is quick with a needle, so she teaches the fur gear at the Tailor: the rescue answers the cold just felt (the RescueOrder rule) |
| **Rung Out of the Mountain** | Tale | When Player Does: Reach Place *a rock face where a bell rings* | Use Object *the cairn's bell* | Bergtaking: ring the bell and someone taken into the mountain walks out, through the existing Lost flow or as a stranger. It gives the novel's bells a rule: a bell rings when a lost name is said aloud (the novel review's C9) |
| **The Door in the Snow** | Main, the portal | After The Wolf's Jaw | Reach Place *The Door in the Snow*; Story Event *the man in grey answered* | Snow falls upward round a stone door; the man in grey kneels at it, and a child's voice calls (chapter 20). Stop him, or let him try and see what comes through. It points to the First Hearth |
| **The First Hearth** | Main | After The Door in the Snow, every resident home | Reach Place *The First Hearth*; Craft *Hearth Ember*; Use Object *the basin* | The finale (chapters 28-30): seven doors, the hunger, the names spoken, the new pact. **Outline only:** to agree before writing (QuestAndStoryResearch §3.7.3) |

**Haven asks:** Sigrun, 4 Reindeer Pelts; Bjarne, 2 Rime Cores ("steel quenched in it holds an edge in the cold"); Liv, an Inscription
from a shrine.

**Continuity with the book.**
- The game's order is Valley, Marsh, Peaks, while the book's is Jaw, Door, Valley, pool, Hearth. The stair under the pool coming up at
  the Jaw joins the two.
- The book's antlered guardian (the Jaw-keeper) stays the mountain's keeper and is never fought, as in chapter 31 ("visited once each
  winter to count the fires").
- The Brukam carries the mountain troll's line that the review found dropped (C7).
- Nothing names the man in grey or his family.

---

## 7. Implementation list, ranked

| # | What | Value | Effort | Data or code | Files and systems |
|---|---|---|---|---|---|
| 1 | **Troll Valley's own data:** events pool (Valley pool, §3.1), rumour pool (drop Rot's Heart and the Woods' finds, keep rescues, the Lost, breach, barrow), known enemies, likely items, three tier briefings, stage names and messages, resource nodes (drop Huldra Silk and Elderwood), creature list (Woods-only creatures out or under 5%), elites (Trollstone 40% at IV, Granite), description ("most of the trollstone") | Very high | S | Data | `Data/Raids/TrollValley.asset`, `Data/Raids/SpawnTables`, `Data/Loot/Raids/TrollValley*`. Run RaidSceneRigTests and the loot overview |
| 2 | **Uses for the dead-end trophies, and the unlock chain** (Brukam's Tusk, Nykk's Mane, Lindorm Scale; Marsh Unlocked By the Brukam, Peaks by the Nøkk) | High | S | Data, plus a small code change if a paid toll must also unlock | `Data/Buildings/*.asset`, recipes, `RaidDefinition.unlockedBy` |
| 3 | **Haven asks per raid** (9 rows, §6) | High | S | Data | `Data/Settlement/Requests`; QuestAuditTests (nothing asked before taught) |
| 4 | **One knight per raid** (spawn shares) and the Valley's **Dawn** hour | Medium | S | Data | `RaidDefinition.TimesOfDay`, spawn lists |
| 5 | **The Valley's rule: bridge tolls** (pay, riddle, fight) and the Sleepers on noise | High | M | Data, with small code for a site per bridge and a price by tier | Troll Bridge set piece, `RaidEventDefinition`, `RaidSetPieceSite.BringGuards`, `WorldNoise`; plan steps 4 and 6 |
| 6 | **Birch Bark, Tar Torch, Bog Waders, Steel Needle, Bog Myrtle** | Medium-high | S-M | Data, and icons from LocalSpawn | `Data/Items`, recipes, a node on the birches |
| 7 | **Act II through the Valley** (§6.2) with the Brukam's `BossDefinition` | Very high | L | Data, scenes, stories, voice | Quests, Quest Targets, set pieces, `BossDefinition`; QuestAndStoryResearch §3.7.2 |
| 8 | **Breach sources by region** (a rumour shows up only in the raid where that breach gathers) | Medium | S | Code (one field) | `RaidRumourDefinition`, `BreachSource` |
| 9 | **The Moss Marsh**, built as its research §12 orders it, plus its four quests (§6.3) | High | L | Both | Its own scene and data, as the Valley's |
| 10 | **Mountain cold as a raid system**, with Soaked doubling it | High (the Peaks need it) | S-M | Code | New `MountainColdSystem : RaidSystem`; `PlayerWarmth.ColdNow` takes a raid floor; tests beside the warmth tests |
| 11 | **The Frost Peaks:** scene, Pass shape, set pieces, reindeer, rime draugs, the Rime-jutul, Reindeer Pelt, Rime Core, Horse Snowshoe and Sled | High | L | Both | As Troll Valley's plan; ModelBudgetTests for the art |
| 12 | **The Ride as apex** | Medium-high | L | Code and art | Riders on the horse body, `MonsterPatrol` at speed, shelters with steel |
| 13 | **Sigrun's rescue and the Peaks quests**, then the First Hearth | Very high | L | Data, stories, voice | `ResidentDefinition`, rumour, quests; needs your sign-off on the finale |
| 14 | **Region conditions** (ARC): High Water, Whiteout, Troll Ting night, rolled with the hour | Medium | M | Code | `RaidDefinition` beside `TimesOfDay`, `RaidSky`, the briefing |

Items 1 to 4 are a day of data work and can be playtested before any art exists. To measure them, add playtest rows (TollPaid, PartUsed)
as each lands.

---

## 8. Questions for you

1. **Is the Brukam the old troll** that broke the stones, so that Act II's "The Toll" is the Brukam's fight or payment at its bridge?
2. **Troll Valley's Woods creatures and events:** take the Woods-only ones out (Rootlings, Root Guardians, Moss Shamans, Marsh Lights,
   Cursed Tree, Thickening Fog, Huldra Silk and Elderwood nodes), or keep them rare?
3. **Trollstone:** mainly the Valley's (the Woods keeps only the Old Moss Troll's, and its tier IV elites lose the 25% row), or leave
   both as now and fix the description?
4. **The Frost Peaks' boss and keeper:** the Rime-jutul as "mountain ruler", and the antlered Jaw-keeper as a toll-taker who is never
   fought?
5. **The Jaw-keeper's toll:** a companion's memory that comes back at the First Hearth. Is that right, too sad, or should it be
   something else?
6. **The Ride as the Peaks' apex** (dead riders on horses: large work), or a simpler apex first?
7. **Sigrun** rescued in the Peaks by the red thread, teaching the fur gear?
8. **Tier V:** offer the Peaks at IV and V once the Trollstone kit is measured?
9. **One knight per raid:** Fire in the Valley, Toxic in the Marsh, Ice in the Peaks?

---

## Sources

**In the project:** `Docs/GameDesignDocument.md`, `Docs/TrollValleyPlan.md`, `Docs/NextRaidResearch_2026-11-17.md`,
`Docs/LootAndDropsResearch_2026-11-17.md`, `Docs/LootTables.md`, `Docs/QuestAndStoryResearch.md`, `Docs/QuestRedesign.md`,
`Docs/RescueOrder.md`, `Docs/GearScoreAndRaidTiers.md`, `Docs/BaseProgression.md`, `Docs/NovelReview_TheRoadThatRemembers_2026-11-17.md`,
`Docs/The Road That Remembers.docx`, `Assets/_Project/Scripts/ARCHITECTURE.md`, `Data/Raids/TrollValley.asset`, `Data/Raids/Rumours`,
`Data/Raids/Events`, `Data/Quests`, `Data/Items`, `Data/Settlement/Requests`, `Scripts/Survival/PlayerWarmth.cs`,
`Scripts/Quests/QuestObjective.cs`.

**Games:**
- ARC Raiders: [Maps](https://arcraiders.wiki/wiki/Maps), [New Map Conditions](https://arcraiders.com/news/new-map-conditions)
- Darkest Dungeon: [Heirlooms](https://darkestdungeon.wiki.gg/wiki/Heirlooms); curios (snippet only):
  [GameSkinny](https://www.gameskinny.com/tips/darkest-dungeon-curios-guide/)
- Deep Rock Galactic: [Glacial Strata](https://deeprockgalactic.wiki.gg/wiki/Glacial_Strata)
- Hunt: Showdown: [Meet Scrapbeak](https://huntshowdown.com/news/meet-scrapbeak-our-new-boss)
- Valheim (snippet only): [Prima Games](https://primagames.com/gaming/valheim-silver-wolf-armor-frost-resistance-guide),
  [eip.gg](https://eip.gg/valheim/guides/10-tips-to-manage-the-mountains/)
- Monster Hunter World (snippet only): [Hot Drink](https://monsterhunterworld.wiki.fextralife.com/Hot+Drink),
  [Hoarfrost Reach](https://www.monsterhunterwiki.org/wiki/Hoarfrost_Reach)
- Escape from Tarkov (snippet only): [Insider Gaming](https://insider-gaming.com/how-to-get-into-the-lab-escape-from-tarkov-keycard/),
  [Dot Esports](https://dotesports.com/escape-from-tarkov/news/how-to-get-and-use-the-red-keycard-in-escape-from-tarkov)
- Dark and Darker (snippet only): [Frost Wyvern](https://darkanddarker.wiki.spellsandguns.com/Frost_Wyvern)

**Folklore and history:**
- SNL: [jutul](https://snl.no/jutul), [Jutulhogget](https://snl.no/Jutulhogget), [rimtusser](https://snl.no/rimtusser),
  [oskoreia](https://snl.no/oskoreia), [hulder](https://snl.no/hulder), [bergtaking](https://snl.no/bergtaking),
  [varde](https://snl.no/varde), [fjellvettreglene](https://snl.no/fjellvettreglene)
- Snippet only: [SNL fangstgraver](https://snl.no/fangstgraver), [SNL Askeladden](https://snl.no/askeladden),
  [Boots Who Ate a Match With the Troll](https://en.wikipedia.org/wiki/Boots_Who_Ate_a_Match_With_the_Troll)
- Lendbreen: [Scientific American](https://www.scientificamerican.com/article/melting-ice-reveals-a-lost-viking-era-pass-in-norways-mountains/)
