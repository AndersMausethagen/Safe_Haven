# The next raid: the Moss Marsh (research, 2026-11-17)

You asked: "start researching the next raid". This is research and a proposal, laid out like [Troll Valley's plan](TrollValleyPlan.md).
Nothing in the project was changed.

**How this was made.**
- **The project.** I read the design document, the story research, your novel *The Road That Remembers*, the raid and tier data, the
  generator, and the survival, weather and water code.
- **The web.** I researched Norwegian folklore and history, and how other games build marshes. Every outside claim has a link.
  "(snippet only)" means I could read only a search result, not the page itself.

---

## The short answer

- **Next: the Moss Marsh, a tier III raid** (it offers tiers III and IV).
- **Runner-up: the Frost Peaks.** I would keep them for last, as the region that holds the First Hearth.
- **Why the marsh comes first:**
  - **The design and the story put it there.** The GDD's boss ladder runs forest guardian, troll chieftain, **marsh spirit**, mountain
    ruler (§16.6). The Act III outline puts the finale, the First Hearth, in the Frost Peaks.
  - **It changes play the most.** After a closed forest and a walled valley, the marsh is flat, open and wet. Water becomes the ground
    you cross.
  - **Most of its creatures already exist.**
  - **The Peaks' newest idea, the cold, already reaches the raids in winter.**
- **Where it lies:** on the high moor north of Troll Valley, under the mountains. "North" stays true. Its heart can be the black pool
  from your novel, where the stair down to the First Hearth begins.
- **How it plays:**
  - Strings of firm peat and old duckboards run between pools.
  - Wading is the fast way, and the loud one.
  - Reeds hide you; open bog shows you from far off.
  - A pale horse grazes at the water where no horse should be. Something long moves in the deep pools.
- **What it needs:**
  - a water table under the whole map: one water sheet, with the ground rising through it;
  - boardwalks at crossings, wading noise and a Soaked status;
  - new creatures: the Nykk, the Lindorm as apex, the Nøkk as boss, and two spirits for events;
  - art: reeds, cotton grass, sphagnum ground and black water.
- **The biggest risks:**
  - Swamp levels are among the most hated in games, because they slow you and hurt you at once.
  - It could repeat the Misty Woods: mist, spitters, lights.
  - Creatures and carts behave badly in water.
  - A water surface over the whole map may cost too much frame time.

---

## 1. Which raid comes next

### 1.1 The candidates

| | Moss Marsh | Frost Peaks | Something else |
|---|---|---|---|
| In the GDD | §4.3: "visibility and poisonous creatures challenge preparation" | §4.3: "exposure, rare ores, and ancient shrines demand specialized gear" | Nothing. A coast with the sea-draug, or the novel's Wolf's Jaw pass, would be new regions |
| Boss ladder (§16.6) | "Marsh spirit: status control, antidotes, or a Cursed magic option" | "Mountain ruler: an advanced magical tier, specialized crafting..." | – |
| Story | The Drowning Fen and its marsh guardian (Act III, [QuestAndStoryResearch](QuestAndStoryResearch.md) §3.7.3, §3.8.7) | The First Hearth, the finale (same sections); the novel's snow and ice | – |
| Creatures that exist | Many: see §6 | Few: Berg-jutul, Ice Knight, Draug, wolves | – |
| New play | Water as the ground you cross; sightlines and reeds; spirits you bargain with | Exposure and verticality. Winter raids already bring snow, ice and cold | – |

### 1.2 What the project says

- **GDD §4.3** lists Misty Woods, Moss Marsh, Troll Valley, Frost Peaks, and calls the names, order and count provisional.
- **GDD §16.6**, the boss ladder, runs forest guardian, troll chieftain, marsh spirit, mountain ruler. Now that Troll Valley is the second
  raid, the ladder reads Woods, Valley, Marsh, Peaks.
- **QuestAndStoryResearch, Act III:** "north to the Frost Peaks and the first hearth, where the pact was made", and "a marsh guardian in
  the Drowning Fen". A finale belongs at the end, so the Peaks should come last.
- **Your novel:**
  - Act II passes through snow: the Wolf's Jaw, "A Door in the Snow", and a wall of ice at Troll Valley's mouth.
  - Act III begins at "a pool beneath the mountain" whose black surface shows faces pressing up from below. A stair from it goes down to
    the First Hearth.
  - That pool reads well as the heart of a fen at the mountain's foot.
- **GDD §21.5's "hammer ... past the troll country" no longer points beyond the valley.**
  - [RescueOrder](RescueOrder.md) says that moment is now Sigrid's horn.
  - QuestAndStoryResearch's proposed quest 20 puts her rescue in Troll Valley, past the troll.
  - So the pointer north ends in Troll Valley itself, and it doesn't choose between the marsh and the peaks.
- **Tiers.** Troll Valley starts at tier II and offers II to IV (`Data/Raids/TrollValley.asset`), so the next raid starts at III.
  - Tier V can't be offered yet, because no full tier V kit exists ([GearScoreAndRaidTiers](GearScoreAndRaidTiers.md), Gaps).
  - The same section says "tier IV brings no new creature". This raid's creatures can fill that gap.

### 1.3 Play and variety

- **Each raid should change what the player does, not only the colours:**
  - The Woods are closed and green, with short sight in mist.
  - The Valley is a long corridor between walls, with bridges, stone and a cold palette.
  - The Marsh would be flat, low and wet. Sight is long by day, but reeds and ground mist cut it, and water is the ground you cross.
  - The Peaks would be vertical, white and exposed.
- **The Peaks right after the Valley** would put two rock-and-cliff raids with cold palettes back to back.
- **The cold already exists.** Winter raids bring snow, frozen pools and the Chilled state to the Woods (`ARCHITECTURE.md`, "Raids follow
  the year"; `Survival/PlayerWarmth.cs`). So the Peaks' main idea is partly in the game already.
- **No raid yet makes water the ground.** The Woods only have a few ponds.
- **The catch:** the GDD gives the marsh "visibility and poison", and both already belong to the Woods (mist, the Bog Spitter, the Marsh
  Light). The marsh has to earn its place with water, open ground and its spirits, not with more fog and poison. That is the main rule
  for everything below.

### 1.4 The verdict

**The Moss Marsh, tier III.**
- Keep "Moss Marsh" as the raid's name, and call its heart, the boss's pool, "the Drowning Fen" (a question for you, §14).
- It is unlocked by beating Troll Valley's boss (`RaidDefinition` › Unlocked By), as Troll Valley is by the Heartroot.

**The runner-up is the Frost Peaks.** Choose them instead if you want the novel's order (snow straight after the valley) or want the
cold to be the next new system. Section 15 sketches them.

---

## 2. Creative direction

**The land.** A high moor (*fjellmyr*) in a wide, nearly level basin, with low hills round it and snow-topped peaks far off: the Frost
Peaks, seen on the horizon.
- **Strings** of firmer peat run across the slope, with pools (**flarks**) between them. This is the Norwegian string bog,
  *strengmyr* ([SNL](https://snl.no/strengmyr)).
- **Hummocks** carry heather, crowberry and dwarf birch.
- **Black tarns** hold **holms**: rock islands with old pines.
- A slow, dark stream winds through.
- **Made by people:** old duckboards (*klopper*), the old road's causeway, peat cuttings with their stacks, and a ruined bloomery with
  slag heaps.

**The look.**
- Sphagnum in rust red, ochre and green ([Sphagnum](https://en.wikipedia.org/wiki/Sphagnum): the Acutifolia mosses are orange-red).
- White cotton grass and silver-grey dead pine.
- Black, peat-dark water that mirrors the sky.
- Pale birch on the drier edges, and heather turning purple-brown in autumn.
- The palette is warm colours over dark: rust and gold over black water. That sets it apart from the Woods' blue-green and the Valley's
  slate, and helps the player know where they are (GDD §19.2: "Distinct biome palettes should aid navigation").

**The mood.** Wide, quiet and lonely, and beautiful by day: the first open sky after two closed raids. At dusk it turns uncanny:
- ground mist that seems to dance (Swedish *älvdans*, the elves' dance: [sv.wikipedia](https://sv.wikipedia.org/wiki/Älvdans));
- lights over the water;
- a horse on the shore.

**The weather.** More drizzle than the Woods, calm evenings with ground mist, clear cold days, and storms seldom. A Moss Marsh Climate
asset would set this.

**The hours.**
- Day: long sight.
- Dusk: mist and the Nykk. Folklore says the nøkk is most dangerous after sunset ([SNL](https://snl.no/nøkken)).
- Night: lights and the dead.

**The seasons matter more here than anywhere.** Raids already follow the year:
- **Summer:** cotton grass, ripe cloudberries and swarms of biting flies.
- **Autumn:** the bog turns red.
- **Winter:** the pools freeze. `WinterIce` makes still water walkable, so the whole map opens up.
- **Spring:** high water (`SpringMelt`), so fords must be swum.

---

## 3. Folklore at the core

| Belief or fact | What the sources say | In the raid | Source |
|---|---|---|---|
| **The nøkk as a white horse** (*nykk*, Swedish *bäckahäst*) | Most often a grey-white horse grazing at the water. Whoever sits on its back is carried in and drowned. Saying its name, or throwing steel into the water, breaks its power. The charm: "Nykk! nykk! Nål i vatn ... Du søkk! Eg flyt!" (Nykk, needle in the water ... you sink, I float). In Swedish lore its back grows longer for every child that climbs on. One nøkk was caught with a halter and ploughed all spring | **The Nykk** (a creature) and **the Nøkk** (the boss). Naming it and steel in the water are the counters | [SNL nøkken](https://snl.no/nøkken), [no.wikipedia](https://no.wikipedia.org/wiki/Nøkken), [sv.wikipedia Bäckahästen](https://sv.wikipedia.org/wiki/Bäckahästen), [Faye, *Norske Sagn* 1833](https://www.bokselskap.no/wp-content/themes/bokselskap2/tekster/pdf/norskesagn.pdf) |
| **Lyktemenn** (bog lights) | Flickering lights over marshy ground, said to be unbaptised children who lead people astray. Danish rule: never point at one, and turn your hat inside out. Marsh gas is the usual explanation, though the sources disagree | The **Marsh Light** (exists) and **marsh gas** that flashes when fire meets it (exists) | [SNL lyktemenn](https://snl.no/lyktemenn), [da.wikipedia Lygtemand](https://da.wikipedia.org/wiki/Lygtemand), [Will-o'-the-wisp](https://en.wikipedia.org/wiki/Will-o'-the-wisp) |
| **Deildegast** (the boundary ghost) | The ghost of a man who moved boundary stones. He must lift the stone back; people hear him cheer as he works, then scream when it slips. He rests only if a living person helps him. Most common in Vestlandet, Agder and Telemark. **The "Where shall I put it?" dialogue is North Frisian, not Norwegian** | An event (§9): help him set the stone | [SNL deildegast](https://snl.no/deildegast); Frisian tale: [amerisurv](https://amerisurv.com/2009/04/11/a-ghost-story/) |
| **Utburd** (the crying ghost) | The ghost of a child set out to die. It cries, lures people, rides on their backs and can grow "as big as a house". A naming lays it to rest: "Eg døyper deg på ei von, til Sigrid eller Jon" (I name you on a hope, as Sigrid or Jon). It is found at forest tarns (Båntjern). The Swedish *myling* grows heavier the further it is carried | An event, "The Crying in the Fen". This is already proposed in QuestAndStoryResearch §3.7.4 | [SNL utburd](https://snl.no/utburd), [no.wikipedia](https://no.wikipedia.org/wiki/Utburd), [Myling](https://en.wikipedia.org/wiki/Myling) |
| **Lindorm** (the great serpent) | A long serpent living in lakes, bogs and deep forest, sometimes guarding treasure. Hadeland: only a spear tipped by a smith with steel and copper could kill one. Finnskogen: people threw a burning ball of rags at one. What lies under it grows as it grows. Lake serpents: Mjøsormen, and Seljordsvatnet | **The apex** of the raid | [lokalhistoriewiki Lindorm](https://lokalhistoriewiki.no/wiki/Lindorm), [Lindworm](https://en.wikipedia.org/wiki/Lindworm), [SNL sjøorm](https://snl.no/sjøorm), [Sjøormen i Mjøsa](https://lokalhistoriewiki.no/wiki/Sjøormen_i_Mjøsa) |
| **Offerings in the bog** | Iron Age people sank weapons, boats and jewellery in bogs, often broken first: Illerup (more than 15,000 items), Nydam, Thorsbjerg, Vimose. In Norway, the Kvalsund find: two boats from the 600s, broken and buried in a bog | **The Drowned Hoard** set piece. Giving something back as a bargain | [SNL mosefunn](https://snl.no/mosefunn), [SNL Kvalsundfunnet](https://snl.no/Kvalsundfunnet), [Illerup Ådal](https://en.wikipedia.org/wiki/Illerup_Ådal) |
| **Bog bodies** | About a hundred are known, from Denmark, Germany and the Netherlands. SNL lists none from Norway. Acid bogs keep skin and hair | The **Bog Dead**: a look for the Draug. This is borrowed from Danish finds, so keep it light | [SNL moselik](https://snl.no/moselik) |
| **Fossegrim** | A waterfall spirit that teaches the fiddle for an offering of meat on a Thursday evening | Already a creature. It belongs where the stream falls out of the moor | [SNL fossegrimen](https://snl.no/fossegrimen) |
| **"Mosekonen brygger"** (Danish) | Thick bog fog is the bog-woman brewing | A name for the fog event | [Jammerbugt](https://www.jammerbugt.dk/media/gwxlj2lw/tema-4-vildmosens-mystik.pdf) (snippet only) |
| **Bog iron** (*myrmalm*) and bloomeries (*jernvinne*) | Norway's only iron ore until the mid-1500s. An oily film and orange floc on the water show where it lies, and a bog can be cut again about once a generation. Smelted in clay shaft furnaces on the upland moors. Møsstrond (Hardangervidda) made iron from about AD 500, perhaps 10 tonnes a year in 950–1150 | **Bog-iron nodes** with a visible tell. The **Ruined Bloomery** event | [SNL myrmalm](https://snl.no/myrmalm), [SNL jernvinna](https://snl.no/jernvinna), [Bog iron](https://en.wikipedia.org/wiki/Bog_iron), [Møsstrond (Industriarven)](https://www.industriarven.no/wp-content/uploads/2018/07/8-Mosvatn-og-Mosstrond-1.pdf), [Hurstwic](https://www.hurstwic.com/history/articles/manufacturing/text/bog_iron.htm) |
| **Peat, sphagnum, bog myrtle, cloudberry** | Peat was burned from the Viking Age into the 1950s. Sphagnum was used on wounds through the First World War. Bog myrtle (*pors*) flavoured strong ale before hops (and gave headaches), and was hung against flies. Cloudberries are a prized crop | Resources and recipes (§7) | [lokalhistoriewiki Torv](https://lokalhistoriewiki.no/wiki/Torv), [Jærmuseet](https://www.jaermuseet.no/samlingar/?p=401), [Sphagnum](https://en.wikipedia.org/wiki/Sphagnum), [SNL pors](https://snl.no/pors), [SNL molte](https://snl.no/molte) |
| **Knott** (biting black flies) | Common above the tree line. Mass swarms can kill cattle and horses | A summer hazard that bog myrtle wards off | [SNL knott](https://snl.no/knott) |

The tone rule (GDD §4.2, "creatures should have motivations beyond hostile monster") is well served here:
- The Nykk lures.
- The deildegast wants help.
- The utburd wants a name.
- The Lindorm guards what it has.
- The Nøkk can be bargained with.

---

## 4. The biome

| Part | The real thing | In the game | Art |
|---|---|---|---|
| Ground | Sphagnum (red, ochre, green), dark peat with cut banks, heather on hummocks, wet sedge, black mud, gravel by the stream | Four to six terrain layers: sphagnum red, sphagnum green, peat, mud, sedge, plus gravel from Troll Valley | **Missing:** sphagnum and peat layers |
| Grass and plants | Cotton grass, sedge, reeds, bog myrtle, cloudberry, sundew, bogbean, crowberry | Grass details; reeds as prefabs that hide the player (`Concealment`, as shrubs do) | **Missing:** reeds, cotton grass (Troll Valley needs it too), bog myrtle, sundew. Cloudberry exists as a resource |
| Trees | Dwarf birch, birch on the edges, stunted bog pine on the holms, dead grey pine snags | Few trees: the canopy pass is thin, the undergrowth pass heavy | Birch exists (`Art/Vegetation/Birch`); dead spruce and pines exist; **missing:** stunted bog pine and dwarf birch (could be scaled young birch) |
| Water | Still, black, peat-dark water; pools, tarns, a slow stream | One water sheet under the map (§11), ponds and tarns, a surface river | **Missing:** a black-water material (dark, mirror-like, with a depth tint you can read from above) |
| Rock | Few: holms of bedrock, glacial boulders, boundary stones | Boulder kit (sparse), holms as the rare high ground | Exists: Rune Stone, Rune Cairn, Offering Stone, God Post (`Art/World/Props`) |
| Made by people | Duckboards, a causeway, peat stacks and huts, bloomery ruins, boat remains | Set pieces and boardwalks (§5, §11) | A **modular wooden pier** already exists (`Art/Vegetation/Haven World/Downloaded/modular_wooden_pier`) and could start the boardwalk kit |

---

## 5. Sites and set pieces

| Set piece | What it is | What it does in play |
|---|---|---|
| **Duckboard Crossing** | An old boardwalk over a deep channel | A choke point with guards: the dry way across, watched |
| **The Old Causeway** | The old road on a raised bank | The long dry route (the Spine shape) |
| **Peat Cutters' Hut** | A turf hut, peat stacks, a hearth | Somewhere to get warm and dry, and a cache. Lighting the fire is seen from far off (like the Old Beacon) |
| **Ruined Bloomery** | A clay furnace, slag heaps, ore pits | The bloomery event (§9): smelt bog iron where you stand |
| **Drowned Hoard** | Broken weapons and a boat's ribs in a deep pool | Relics and old silver, in the Lindorm's water. Take, or give back |
| **Boat in the Peat** | A Kvalsund-like boat, half cut out of the peat | A cache and a story find |
| **Boundary Stones** | A row of marker stones, one missing | The deildegast's event |
| **Pine Holm** | A rock island with old pines | High ground: a lookout, moonsilver, an archer's perch |
| **Wisp Hollow** | Gas bubbling up among the reeds, and lights | Marsh gas: a trap for creatures, or for you |
| **The Black Pool** | The fen's heart, a stair under the water | The boss's arena, and the Act III door |

Some existing pieces fit: Standing Stones, Old Barrow (on a holm) and Burnt Farmstead (a drowned farm). The Wolf Den and Ruined Watchtower
don't.

---

## 6. Creatures

### 6.1 Creatures that already exist

| Creature | Its role in the marsh | From tier |
|---|---|---|
| **Bog Troll** ("a long, wet troll of the meres") | The common heavy. Spits venom and shoves. Lives in the water | III |
| **Bog Spitter** | Fewer than in the Woods. Squats on hummocks | III |
| **Black Widow** | In the drowned pines; webs across duckboards | III |
| **Draug**, with a **Bog Dead** look | The fen keeps its dead whole. Rises again (`RisesAgain`). At night and in the hoards | III |
| **Toxic Knight** ("a dead rider out of the bog") | Rare, at dusk and night; a hunter from tier IV | III (rare), IV |
| **Marsh Light** | Lights over the water, with decoys (`MarshLightDecoy`). Fewer than in the Woods: they were the Woods' idea | III |
| **Mistwing** | Sees wide over open bog: the eye in the sky that makes the reeds matter | III |
| **Huldra** | By the tarns; a glimpse and a lure | III |
| **Moss Shaman, Rootlings** | On the drier edges only | III |
| **Blight Boar** | On the moor's dry rim | III |
| **Fossegrim** | Where the stream falls out of the basin | III |
| **Wolves** | Few: packs on the open moor at night | III |

The Stone Troll and Berg-jutul belong to the mountains, and wait for the Peaks.

### 6.2 New creatures

| Creature | What it is | Why it is new play | Body | Size |
|---|---|---|---|---|
| **The Nykk** | By day, a pale horse grazing at the water, like the wild horse you catch at home. Walk up or rope it and it lets you on, then bolts for deep water. At dusk it hunts: charges, stamps, carries off a companion | A creature you might mistake for a prize. **Name it** (one key, learned at the first meeting) and it throws you off and flees. **Steel thrown into the water** (an iron item, or a cheap Steel Needle from the Smithy) keeps it out of that pool for the raid | The horse rig and clips (`Data/Horses`, the Toxic Horse's body), with a pale, wet look and weed in the mane. The drag reuses the fishing Nøkken's struggle (`NokkenEncounter`) | M |
| **The Lindorm** (apex) | One in each raid, in the deep water. It is heard before it is seen: reeds part, a wake, bubbles. It surfaces to strike at waders and at the bank | It makes deep water dangerous and dry ground safe, so the route is the choice. Hunt: Showdown did the same with its Water Devil, which stalks waders ([Crytek](https://crytek.com/news/hunt-showdown-release-update-23-the-water-is-no-longer-safe)) | Seen only in part: a head with an animated jaw, and humps that rise and sink along a curve. Procedural, like the black widow's legs, so it needs no full rig | L |
| **The Nøkk** (boss) | The marsh spirit of the Drowning Fen: §6.3 | GDD §16.6's "marsh spirit" | The Nykk's body for phase 1; a fiddler in the water (the Fossegrim's body, made over) for phase 2 | L |
| **Utburd** | Heard crying, never shown as a child. It rides on the player's back and grows heavier | An event, not a spawn (§9) | A shadow on the back, and a sound | S-M |
| **Deildegast** | A ghost bent under a boundary stone | An event figure (§9) | The Draug's body in a ghost material | S |
| **Knott swarm** | A cloud of black flies over the warm hummocks on summer evenings | A summer hazard: stamina recovers more slowly inside it, and bog myrtle wards it off | Particles and a sound | S |

**Tier IV, the gap.** The new creatures fill it. From tier IV:
- the Nykk roams in pairs and calls the water up (a pull);
- the Lindorm sweeps its coils across a hummock;
- Toxic Knights come as hunters.

These are rows of data on the monsters (`MonsterAttack` › From Tier), as Troll Valley's abilities are.

### 6.3 The apex and the boss

**The Lindorm, the apex.** It plays the role the Old Moss Troll plays in the Woods (GDD §10.5): you are meant to avoid it, not farm it.
- **Where it can go:** it never leaves the water by more than its reach. That is the marsh's promise: on dry ground you are safe from it.
- **Killing it:** possible for a prepared player.
  - A **Wyrm Spear**, tipped with iron and copper at the Smithy (the Hadeland legend), and fire both hurt it more.
  - It is open to blows while it lies on the bank after a strike.
- **Its hoard grows with it.** This is the folklore's rule. Every raid it survives adds to its hoard, up to a cap, and the count is saved.
  Leaving it for later is a real choice.
- **Its drops:** a **Lindorm Scale** (the region's trophy, §7), a relic and its hoard.

**The Nøkk of the Black Pool, the boss.** It is the GDD's marsh spirit, and an Act III quest step.
- **Phase 1, the white horse.** It runs on the hummocks round the pool, charges and rears, and carries a companion off towards the deep.
- **Phase 2, the fiddler.** In the water, its tune draws the player towards the deep (the fishing Nøkken's pull) and charms creatures to
  its side. Steel thrown into each of the arena's pools shuts it out of that pool, so the arena tightens on it.
- **Phase 3, the black pool rises.** Safe ground shrinks. The Hunger's borrowed voices call from under the water, as in your novel.
  - Naming it when it shows its face makes it reel. The game's own rule is "names have power".
- **A bargain instead of a fight.** The player may have listened to its air at home (`SpiritBonds`, the Nøkk "plays to you"). If so, it
  offers a toll: something you would miss, as the guardian asks in your novel. This follows GDD §4.4 ("defeat or negotiate with mythical
  guardians") and Act II's choice between paying the toll and fighting.
- **Its reward:** an ability that controls status, as §16.6 asks. For example, **Nykk's Air**: a short tune that slows the creatures
  near you and draws them in. Or a Cursed option ("Drowning Grip": hold one creature in place and hurt it). Also a **Nykk's Halter**: in
  Faye's tale a haltered nøkk ploughs without rest. It could tame a tireless pale horse at home later.

**One being, two places.** The Nøkk in the Haven's tarn and the boss can be the same being: the bargain then pays off the relationship.
That is a question for you (§14).

---

## 7. Resources and unique drops

The rules come from today's [loot research](LootAndDropsResearch_2026-11-17.md):
- every new drop replaces a generic one rather than adding to the pile;
- it has at least two uses;
- it comes from the raid tier below what it opens;
- if it gates progress, the Trading Post sells a little.

| Drop or resource | From | What it is for | New? |
|---|---|---|---|
| **Iron Ore, as bog iron** | Bog-iron nodes in shallow water, shown by a rainbow film and orange floc | Iron, with a tell you can read. No new item, a new node look | Look only |
| **Moonsilver Ore** | Pine holms, and drowned hoards | The raid's tier IV pay: a tier N raid pays for tier N+1 gear (GearScoreAndRaidTiers) | Exists |
| **Bog-troll Hide** | Bog Troll, replacing its Rune Book | **Bog Waders** at the Tailor: half the wading slowdown. The marsh's gear to prepare (loot research §8) | New |
| **Widow Silk** | Black Widow, keeping its venom | The quiet silk shirt (loot research §8) | New |
| **Nykk's Mane** | The Nykk | Bowstrings for the top bows; the Nykk's Halter | New |
| **Lindorm Scale** | The apex | The region's trophy: one of Smithy V, Workshop V or Tailor IV, and a scale armour piece | New |
| **Bog Myrtle** (*pors*) | Gathered on hummocks | **Gruit Ale** at the Kitchen: hits harder, with a headache (a meal with a price, like Honey Mead). A sprig wards off knott swarms | New |
| **Sphagnum** | Gathered; the Small Moss Troll's Troll Moss from the loot research can be the same item | A Moss Poultice that heals over time; bandages | New, merged with Troll Moss |
| **Cloudberries** | Common here: the bog is their home | Existing recipes | Exists |
| **Offered Blade** (a broken Iron Age sword) | Drowned hoards | Break it down into Grave Iron and pennies, or give it back to the bog (§9) | New (a "barrow goods" kind of item) |
| **Wisp Ember, Bog Venom** | Marsh Lights, spitters, Bog Troll | As now | Exist |

**Chests.** A **Peat Cutter's Box** (sphagnum, bog myrtle, linen, a peat spade) and a **Drowned Hoard** chest (relics, pennies, Offered
Blades, moonsilver). These are kinds of the themed caches the loot research proposes.

**What only this raid gives (GDD §7.7).** Nykk's Air (the boss's ability), the Lindorm's hoard and its relics, and the Halter: a living
thing to bring home later. People can be found here too: the rumour system can stand the Lost in the fen.

---

## 8. Hazards and raid systems

### 8.1 What exists to build on

| System | Where | What it gives the marsh |
|---|---|---|
| Wading and swimming | `Player/PlayerSwimming.cs`, `PlayerMovementSettings.WadeSpeedShare` | Speed falls with depth, to 55% at swimming depth. Swimming and boats exist |
| A water sheet lying under the ground | `WaterSurface` › shoreline ("a bog whose sheet lies under more ground than it wets"), used by the Haven's bog | One water sheet for the whole fen, with the terrain rising through it |
| Ponds and rivers in generated maps | `RaidLayoutRules` › Water, Surface Rivers; `RaidMapGenerator.PlacePonds/PlaceRivers` | Tarns and the stream, drinkable, fishable, with mist over them |
| Wet ground and wet-loving plants | `RaidLayoutPlan.Wetness`, `TerrainGrowth` › Love Of Wet | Plants that thicken towards the water |
| Marsh gas | `GroundHazard` (Gas: "fire sets it off in a burst of flame"), prefabs `ToxicFog`, `GasFlash` | Gas seeps as placed hazards: lure a troll in and shoot a fire arrow |
| Poison and blight | Poison status, Antidote, Blight resistance; `RaidHarms` names them in the briefing | Already readable to the player |
| Fog that hides you | §10.8: in dense fog monsters see half as far; fog banks; Mistcloaked | Ground mist at dusk |
| Shrubs that hide you | `AI/Perception/Concealment` on shrub prefabs | Reeds: the same component |
| Birds that give you away | Birds flush and call an alarm (§10.8) | Ducks on the pools and herons in the reeds: data (`BirdProfile`) |
| Cold and fire | `PlayerWarmth` (season cold), Frost Resistance, Warming Brew, fires that warm | Soaked can feed it (below) |
| Seasons in raids | `Seasons`, `WinterIce`, `SpringMelt`, `WinterTracks` in raid scenes | Frozen pools in winter, high water in spring |
| Rising water | `SpringMelt` (`Season.riverRise`, running water) | The idea of a raised level; the fen's still water needs its own |
| The Nøkken's drag | `NokkenEncounter` (fishing): pulls the player towards the deep until they break free | The Nykk and the boss |
| Horses | `Data/Horses` (Wild Horse, Toxic Horse), the lasso, mounting | The Nykk's body, and its lure |
| Transport | Carts go through the portal; the Rowboat floats on real buoyancy | A boat matters here. Check that a Rowboat kit can be carried into a raid |
| Terrain events | `TerrainEventsSystem` (rockfall, mist thickens) | "The Fen Rises" |

### 8.2 What is new

1. **A water table and depth bands.** The fen is one water level. Ground above it is dry, and ground below it is water. Four bands must
   read at a glance from the camera:

   | Band | Speed | Noise | Other |
   |---|---|---|---|
   | Dry | Normal | Normal | – |
   | Shallow | -15% | ×1.5 | – |
   | Deep | -40% | ×2.5 | No carts |
   | Swim | Swimming | Swimming | Only by boat with cargo |

   Each band has its own tint, foam and reed silhouette. Today's wading speed curve covers the speed; the rest is new.
2. **Wading noise.** `PlayerNoise` makes the same footstep sound in water as on land, and only swimming has its own noise. Footsteps in
   water should carry further with depth, and crouch-wading should be quiet but slow. Small.
3. **Soaked.** Deep water, swimming and rain make the player Soaked. It is a gear state, not damage:
   - stamina recovers 15% more slowly;
   - the bow draws more slowly;
   - the torch goes out;
   - in the cold it doubles `PlayerWarmth`'s chill (this prepares the Frost Peaks);
   - a fire dries it off in about 20 s, and it shows on the HUD.

   Valheim and Don't Starve both use wetness as a penalty to recovery, never as damage ([metabot: Wet](https://metabot.gg/en/valheim/status-effects/Wet),
   [Don't Starve wiki](https://dontstarve.wiki.gg/wiki/Wetness)). Small to medium.
4. **Reeds that hide.** Sight runs far over open bog, and reed beds break it. Reeds are the marsh's way of hiding, where the Woods use
   fog. This is data: `Concealment` on reed prefabs.
5. **Creatures and water.** Deep water gets a high path cost, so creatures keep to dry ground unless they are hunting. The Lindorm, the
   Nykk and the Bog Troll prefer water. Medium.
6. **The Fen Rises.** At a danger stage the water table rises by 0.3–0.5 m: shallows turn deep and fords must be swum. A terrain event.
   Small to medium.
7. **Quaking ground** (*hengemyr*), optional. Floating mats that hold a light player but sink under a heavy bag or a cart. Weight then
   changes the route: the games research calls this "weight changes depth". Medium.
8. **Knott swarms** in summer: §6.2. Small.

**Rules taken from the games research (§10), to be tested:**
- **No ground both slows and hurts.** Water costs speed and noise; anything that deals damage stands on dry or marked ground. Miyazaki
  himself said he "went a little too far" with poison swamps ([VideoGamer](https://www.videogamer.com/news/miyazaki-went-too-far-with-poisonous-swamps-in-elden-ring)).
- **Every exit has a dry route.** It may be 1.3 to 1.6 times longer than the wet short cut, and passes a choke point or open ground.
  Wading is the faster and louder choice, never the only one.
- **Short exposure.** No forced deep crossing longer than about 8 seconds.
- **Water slows top speed, never the controls.** Turning, the parry window and dodge timing stay the same; only top speed and dodge
  distance shrink.
- **Anything that lies in wait shows a tell** (bubbles, a wake) at twice the reach of its strike.

---

## 9. Events

The marsh's own pool, picked like the Woods' (`RaidEventsSystem`: at most four a run, each at most once).

| Event | The warning | The choice | Built on |
|---|---|---|---|
| **The Deildegast** | Cheering, then a scream, over the moor | Carry his stone back to the gap in the boundary row: it is heavy, so you walk slowly and are heard. If you do, he rests and you get a blessing, and the land around is revealed. If not, he follows, screaming, and draws creatures | New. A carried object, like the Lost's following |
| **The White Horse** | A pale horse grazing at a tarn | Mount it, and it bolts for the deep. Name it, and it flees; its tracks lead to a drowned hoard. Or throw steel in the water, and that water is safe for the raid | New. The Nykk |
| **The Crying in the Fen** | A child's crying off the path | Name it on a hope: it rests and blesses the raid. Leave it: it rides you, growing heavier, until you reach a standing stone | QuestAndStoryResearch §3.7.4 |
| **The Drowned Hoard** | A glint of metal in a deep pool | Take it, and the Lindorm stirs and the dead rise. Or give something back, and water creatures leave you be for a while | New. The Old Barrow's pattern |
| **The Ruined Bloomery** | Smoke-black stones and slag | Relight it with charcoal and bog ore, and in 60 s an iron bloom is worth more per weight than the ore. It is loud and smoky, so creatures come | New |
| **The Bog-Wife Brews** | The mist thickens over the pools | A bank of fog with lights and a chest; inside it sound carries less | The existing Thickening Fog, renamed |
| **The Peat Fire** | A hut with dry peat stacked | Light it to dry off and warm up, seen from afar; or pass by | The existing Old Beacon, changed |
| **The Fen Rises** | The water moves | (No choice.) The water level rises at a danger stage | New terrain event |

These carry over as they are: The Toll Troll (a Bog Troll on the causeway), Three Wishes, The Oskorei, and The Living Path (re-dressed as
boundary stones).

---

## 10. How other games do it

| Game | What it does | What to take |
|---|---|---|
| **Valheim, the Swamp** | Constant rain keeps the player Wet (25% less health recovery, 15% less stamina recovery). Water slows you, and leeches hide in deep pools. Iron lies only in crypts behind the last boss's key. Players called it "by far the biggest jump in difficulty" ([Steam](https://steamcommunity.com/app/892970/discussions/0/3731826842448350835), [eip.gg](https://eip.gg/valheim/guides/10-tips-to-survive-the-swamp/), [metabot](https://metabot.gg/en/valheim/status-effects/Wet)) | Wet as a penalty to recovery. A boss that hands you the next region's tool (Bonemass's Wishbone finds silver). **Avoid** its luck: one crypt gave 100 iron, several gave under 40 |
| **Hunt: Showdown, the bayou maps** | Crytek: water "is not really a gameplay space but rather an obstacle". They added walkways and dry routes, and broke up long sightlines. Later they added the Water Devil, which stalks waders, and ducks that call when you come close. "If you hear crows taking off… another player caused it" ([Crytek compounds](https://huntshowdown.com/news/stillwater-and-lawson-details-of-the-compound-changes-coming-with-up), [update 2.3](https://crytek.com/news/hunt-showdown-release-update-23-the-water-is-no-longer-safe), [PlayStation Blog](https://blog.playstation.com/?p=221483)) | Water as the fast, loud, exposed way. A dry route always there. A predator in the water. Birds as alarms |
| **Hunt's Thundershower** | Rain covers footsteps, fog closes in, but weather "must never obscure critical audio cues" ([Crytek](https://huntshowdown.com/news/behind-the-storm-technical-design-of-thundershower-in-hunt-showdown)) | Rain may hide the player's noise (it does already), never a creature's warning |
| **The Witcher 3, Crookback Bog** | The horror is a bargain: the Crones keep the village safe for a price. The consequences come long after the choice ([The Ringer](https://www.theringer.com/2025/05/16/video-games/the-witcher-3-10-year-anniversary-open-world-storytelling-quests-bloody-baron), [Twenty Sided](https://www.shamusyoung.com/twentysidedtale/?p=42809)) | The boss's bargain. The Drowned Hoard's gift that comes due later |
| **Dark Souls' Blighttown, Elden Ring's Lake of Rot** | Slow and poison at once, plus a falling frame rate. "One of the most frustrating in gaming history" ([Blighttown](https://en.wikipedia.org/wiki/Blighttown)). Remembered for the dread and the relief of getting out ([nex-3's ranking of poison swamps](https://nex-3.com/blog/a-definitive-ranking-of-poison-swamps/)) | Never slow and hurt on the same ground. Watch the frame rate in the wettest, foggiest spots |
| **Escape from Tarkov, Shoreline** | Water pockets blend into the ground and are loud and slow ([PlayerAssist](https://playerassist.com/eft-shoreline-map/)). Over 66% of players found heavy-rain sound disruptive ([ConsolePCGaming](https://consolepcgaming.com/escape-from-tarkov-will-adjust-rain-volume-in-upcoming-update)) | Depth must be readable. Keep rain's sound in check |
| **ARC Raiders, map conditions** | One map changes with its condition: Night Raid, Electromagnetic Storm, Cold Snap (frostbite, shelters to fight over, frozen lakes open new routes) ([allthings.how](https://allthings.how/arc-raiders/map/map-conditions/), [FRVR](https://frvr.com/blog/news/arc-raiders-cold-snap-weather-shift-will-kill-you-with-frostbite-and-introduce-new-shelters-to-fight-over), [arcraiders.com](https://arcraiders.com/news/new-map-conditions)) | Raid modifiers for the marsh: **High Water**, **Dry Summer** (mud flats where carts work), **Knott Season**. Winter's frozen pools are already the Cold Snap idea |
| **Monster Hunter World, the Rotten Vale** | Gas drains health, and a torch burns it off for a moment. The armour that resists it comes from the safe level above ([RPG Site](https://www.rpgsite.net/feature/6661-monster-hunter-world-effluvia-how-to-deal-with-the-toxic-gas-in-rotten-vale)) | Marsh gas that fire clears. Gear made from earlier regions answers this one |
| **V Rising** (the same camera) | "Safe and harmful zones had to be clearly defined so this mechanic wouldn't become frustrating" ([Unity case study](https://unity.com/case-study/v-rising)) | Depth bands drawn so clearly that a still picture tells them apart |
| **Death Stranding** | A scanner colours water depth; wade too deep and your cargo floats away ([Deltia's Gaming](https://deltiasgaming.com/?p=267799), snippet only) | Read the depth before you commit, especially with a cart |
| **Design writing** | Water levels feel like "someone just tried to increase the difficulty without giving me any other reward" ([uliwitness](https://uliwitness.github.io/why-underwater-levels-suck-so-often)). "We didn't want traversal to be a guessing game" (Brendon Chung, [Blendo](https://blendogames.com/news/post/2026-06-xx-leveldesign_readability/)). Slow the top speed, not the response to input ([Game Developer](https://gamedeveloper.com/design/maneuverability-in-games-the-importance-of-speed-and-control)) | The rules in §8.2 |

---

## 11. How it would be built

### 11.1 Scene and data, as Troll Valley's

1. **Its own scene, `Raid_MossMarsh`.** Made with the utility Troll Valley's step 1 uses to copy a raid scene, then retargeted: its own map
   definition, terrain, look volumes, climate, birds and sound, with no Woods-only roots.
2. **`MossMarsh.asset`:**
   - tier 3, offering tiers III and IV, the rows copied from Troll Valley's III and IV;
   - Unlocked By: Troll Valley's boss;
   - known enemies, likely items, briefing lines and grade.
3. **`MossMarsh Layout.asset`:** its moods, shapes, quotas, set pieces and water rules.

### 11.2 What the generator supports today, as data only

| Need | Supported now | Where |
|---|---|---|
| Few trees, open ground | Mood › Forest, Tree Spacing, Tree Weights | `RaidMapMood` |
| Low, flat land | Mood › Ridges, Hills; Rules › Ridge Height, Hills, Hill Variance | `RaidLayoutRules` |
| Many tarns | Rules › Ponds (count, radius, depth, stretch), Pond Material, Wet Reach, Damp Ground | `RaidLayoutRules` › Water |
| A stream | Surface Rivers, Ford Depth | `RaidLayoutRules` |
| Mist | Mood › Fog Banks; the scene's fog profiles | Moods, scene |
| Rock holms as lookouts | Lookouts, Landmarks, Boulders | `RaidLayoutRules` |
| Layout shapes | **Ring** (round a great tarn or quaking bog nobody crosses), **Spine** (the old causeway), **Thicket** (a maze of hummocks), **Hub** (all ways meet at the Black Pool) | `RaidLayoutShape` |
| Wet-loving plants | Love Of Wet | `TerrainGrowth` |
| Creatures, caches, events, rumours | Spawn lists with From/To tier, event pool, rumour pool | `RaidDefinition` |
| Weather, grade, sky | Climate asset, `RaidGrade` | `Data/Environment`, `RaidDefinition` |

**Moods to make** (data, once the water table exists):
- **Strings and Flarks**: long dry strings, pools between, the classic string bog;
- **Hummock Fen**: a maze of tussocks and pools;
- **Drowned Wood**: dead pines standing in water, more cover, gloomier;
- **Peat Moor**: drier, cut banks, peat stacks, long sightlines;
- **Tarn Country**: large tarns with holms, where a boat matters;
- **Pine Holms**: rock islands, higher, with moonsilver.

### 11.3 What needs new code

| Piece | What | Size | Depends on |
|---|---|---|---|
| **Fen field mode** | Today the trails are valley floors and the land rises between them (`RaidLayoutField`). A fen turns that over: trails and rooms stand a little above a water table, and the wild ground falls below it. Noise makes hummocks; an optional pattern of bands makes the string bog | M-L | – |
| **The water sheet** | One `WaterSurface` sheet over the map, with its shoreline taken from the field. One sheet instead of hundreds of ponds keeps draw calls down | M | Fen field |
| **Depth tint and bands** | The water shader darkens with depth, so the bands read from the camera; reeds and foam mark the edges | S-M | Water sheet |
| **Boardwalks** | Crossings of deep water get duckboards. Reuses Troll Valley's crossing records and `PlaceBridges` | M | Troll Valley step 4 |
| **Dry-route check** | `ExitsReachable` asks for a path that never goes deeper than shallow water to every exit, and lays the map out again if there isn't one | S-M | Fen field |
| **Water costs for creatures** | Deep water as a costly navmesh area; water creatures ignore the cost | M | – |
| **Wading noise, Soaked** | §8.2 | S, S-M | – |
| **The Fen Rises** | The water sheet's level rises at a stage, and fords and depth are recomputed | S-M | Water sheet |
| **The Nykk** | Horse body, lure, drag, naming and steel | M | Horses, `NokkenEncounter` |
| **The Lindorm** | Water-bound AI on the water network, a procedural body, a saved hoard count | L | Water costs |
| **The Nøkk** | `BossDefinition`, phases, the arena, the bargain | L | The Nykk |
| **Quaking ground** (optional) | Soft-ground zones that sink by carried weight | M | Fen field |

### 11.4 Art that is missing, and what LocalSpawn can make

Most of the look is ground, grass and water, so it is cheap next to creatures.

| Missing | How |
|---|---|
| Sphagnum red, sphagnum green, peat, wet sedge: terrain layers | LocalSpawn textures. Mind the terrain's 14-layer single-pass limit; change `GenerateWorldTerrainShader.py`, never the generated files |
| Reeds, cotton grass, sedge, bog myrtle, sundew, bogbean | Grass details and small prefabs; concept images first. Cotton grass is shared with Troll Valley |
| Stunted bog pine, dwarf birch | LocalSpawn trees (Tree Wind keeps working), or scaled young birch |
| Black water material | A variant of the existing water shader: darker, more mirror, a depth tint |
| Duckboards and causeway | Start from the modular wooden pier in the project, or a Blender script (as Troll Valley's bridge) |
| Peat stacks, turf hut, bloomery ruin, slag heaps, boundary stones, boat ribs, offering pool | LocalSpawn props (the world-props manifest), through the asset budgets and `ModelBudgetTests` |
| The Nykk's look | A pale coat on the horse model; weed in the mane (hair cards) |
| The Lindorm | Head and hump segments from LocalSpawn, with the head rigged. Test one asset at low resolution first |
| The Bog Dead | A material variant for the Draug: dark, leathery, a rope at the neck |

**Sounds** (Python in `Tools/Audio` first, as the project does):
- a calm moor wind;
- water lapping in the reeds, and bubbles;
- moor birds by day: golden plover, snipe, curlew;
- frogs in spring;
- the Nykk's whinny under water;
- the Lindorm's wake;
- the deildegast's cheer and scream;
- the utburd's crying, used sparingly;
- footsteps in three wet bands.

**Music** from LocalSpawn: a sparse fiddle theme for the Nøkk, tuned like a Hardanger fiddle. If the model that makes it is licensed
for prototyping only, list the track for a remake before release.

---

## 12. Order of work and tests

0. **Wait for, or share, Troll Valley's foundations.**
   - What to reuse: its scene-copy utility (step 1), crossings and bridges (step 4), and the RaidSceneRigTests.
   - Baselines first: golden plan hashes for both raids, a generator report, timings.
1. **Prototype the fen field in plain C#, before any scene.**
   - Look at it in Raid Layout Preview and the generator report: depth bands, dry routes, walk times.
   - **Go or no-go:** six seeds as screenshots for you, before art or creatures.
   - `FenLayoutTests`:
     - every exit has a dry route;
     - no forced deep crossing over 8 s;
     - no hazard site in water deeper than shallow;
     - the same seed gives the same map;
     - the Woods' and the Valley's golden hashes stay green.
2. **Split the scene** (`Raid_MossMarsh`), with `MossMarsh.asset` and its layout, climate and grade. RaidSceneRigTests: the same systems,
   no shared assets.
3. **The water sheet, depth tint, reeds that hide, creatures' water costs, wading noise and Soaked.** Tests:
   - wading noise rises with depth, and crouch-wading is quieter;
   - Soaked doubles the chill in `PlayerWarmth.Advance` and dries by a fire;
   - parry and dodge timing are the same in water.
4. **Boardwalks and the dry-route check.** Exits on hummocks with two ways in.
5. **Moods** and the string-bog pattern; a look pass (day, dusk, night, dawn; summer, autumn, winter, spring).
6. **Spawn lists** (existing creatures), set pieces, the marsh's events, drops and caches. All data, checked by the content audit.
7. **The Nykk.** Edit-mode tests for naming and steel, and a probe in a live raid.
8. **The Lindorm.** Until it is ready, an elite Bog Troll or Toxic Knight stands in as apex.
9. **The Nøkk** and its quest step; the reward (ability and halter).
10. **Final art** through `ModelBudgetTests`; sounds; music.
11. **Performance pass:**
    - a water sheet over the whole map, with screen-space reflections;
    - fog over water;
    - reed details;
    - measured with the benchmark build, by day and at dusk, on a large map.

---

## 13. Risks

| Risk | What could go wrong | What keeps it in hand |
|---|---|---|
| **Swamp fatigue** | Slow, wet and hurt at once is the most hated kind of level | The rules in §8.2: no ground both slows and hurts; always a dry route; short exposure; controls never slowed |
| **It repeats the Woods** | Mist, spitters and lights again | Fewer of them; water, sightlines, reeds and new spirits carry its identity; the mist is ground mist at dusk, not all day |
| **Creatures in water** | Creatures walking along the bottom of deep water look wrong; a far pond may be a safe spot | Water costs; only water creatures go deep; tests that creatures reach a player on a holm |
| **Carts and boats** | A cart in deep water, a boat that can't come through the portal, `Transport.GroundMask` skipping water | No carts in deep water, said in the HUD; check the Rowboat kit early |
| **Reachability** | Wet maps fail `ExitsReachable` more often; one long `NavMesh.CalculatePath` gives up on large maps | The dry-route check walks the plan's trails in hops (`RoomsReached`) |
| **Performance** | One big water surface with reflections, fog and reed details; new terrain layers | One sheet, not many ponds; measure at step 11; the 14-layer limit |
| **Readability from above** | A top-down camera flattens depth | The depth tint, foam and reeds, and the V Rising rule: a still picture shows the bands |
| **12 minutes** | Wading slows everything; routes run long | Quotas below the Woods' (as Troll Valley's); walk times measured with the bands in the generator report |
| **Dependence on Troll Valley** | Its steps 1 and 4 are not done yet | Do the plain C# field (step 1 here) in parallel; scene and boardwalks after |
| **Seasons** | Winter freezes the whole fen; spring floods it | Test every season in the look pass; spring's High Water as a named condition |
| **Hard themes** | The utburd is the ghost of a child left to die | Heard, never shown as a child; laid to rest with a name, as the project already plans |
| **Two Nøkks** | The Haven's tarn spirit and the boss could clash | Make them one being and let the bargain pay off, or keep the boss apart (a question) |
| **Licences** | Generated art, sound or music from a model whose licence covers prototyping only | The project's rule: list what it made, and remake it before release |

---

## 14. Questions for you

1. **Marsh next, Peaks last?** Or the Frost Peaks first, in your novel's order?
2. **The name.** "Moss Marsh" for the raid and "the Drowning Fen" for its heart? Or something else?
3. **The boss.** The Nøkk (a spirit you can bargain with, using horse and fishing systems that exist) or the Lindorm (a beast, more new
   work)? I propose the Lindorm as apex and the Nøkk as boss.
4. **The Haven's Nøkk and the boss: one being?** If so, listening to its air at home opens the bargain.
5. **The utburd.** Keep it as it is, heard and named, or soften it?
6. **Soaked.** Add the status, which later feeds the cold of the Peaks, or keep water to speed and noise only?
7. **New drops.** Swap them in for generic drops (Bog Troll's Rune Book becomes Bog-troll Hide), or add them alongside?
8. **The boat.** Should a Rowboat be a real tool here, carried in as a kit?

---

## 15. The runner-up: the Frost Peaks, in brief

- **When:** the fourth and last region, tier IV, offering tiers IV and V once a tier V kit exists. It holds the First Hearth and the
  "mountain ruler".
- **The land:** a high pass and its fells. Cairns (*varder*) mark the way, a seter stands abandoned, and a shrine is cut into the rock.
  The First Hearth lies in the mountain's hollow centre (your novel, chapter 28).
- **Folklore:**
  - the jutul: a giant in the mountain caves who turns to stone in sunlight ([SNL](https://snl.no/jutul));
  - the rimtusser, frost giants born of rime ([SNL](https://snl.no/rimtusser));
  - the hulder at the seter ([SNL hulder](https://snl.no/hulder), [SNL seter](https://snl.no/seter));
  - cairns that are "vital" in poor sight ([SNL](https://snl.no/varde));
  - being taken into the mountain and rung out by bells ([SNL](https://snl.no/bergtaking));
  - the Oskoreia, riding at Christmas ([SNL](https://snl.no/oskoreia)).
- **Creatures:** the Berg-jutul, Stone Troll, Ice Knight, Draug and wolves exist. A Frost Giant and a mountain ruler would be new.
- **Systems:**
  - The cold exists (`PlayerWarmth`, Frost Resistance, Warming Brew, fires), but only from the season. It needs a raid source: the
    raid's own cold, altitude and wind.
  - Snow, ice and tracks exist in raids.
  - Soaked from the marsh makes it bite: wet and cold together freeze you, as in Valheim's Mountains ([eip.gg](https://eip.gg/valheim/guides/10-tips-to-manage-the-mountains/)).
  - Shelters to fight over, as in ARC's Cold Snap ([FRVR](https://frvr.com/blog/news/arc-raiders-cold-snap-weather-shift-will-kill-you-with-frostbite-and-introduce-new-shelters-to-fight-over)).
  - Wind that hides sound and carries scent, as in The Long Dark ([Corrosion Hour](https://www.corrosionhour.com/the-long-dark-10-tips-for-getting-started/)).
- **Generator:**
  - Troll Valley's terraces, ravines, spires and lookouts carry most of it.
  - New: snow cover chosen by height, and a "pass" layout shape.
  - Avalanches as a terrain event, like the rockfall.

---

## Sources

**In the project**
- Design: `Docs/GameDesignDocument.md` (§4.1-4.4, §6.4.2, §7, §9, §10.5-10.8, §11, §12, §16.5-16.6, §19, §21), `Docs/TrollValleyPlan.md`,
  `Docs/QuestAndStoryResearch.md` (§3.7, §3.8), `Docs/RescueOrder.md`, `Docs/GearScoreAndRaidTiers.md`,
  `Docs/LootAndDropsResearch_2026-11-17.md`, `Docs/HavenWorldDesign.md`, `Docs/Territory.md`, `Docs/The Road That Remembers.docx`.
- Architecture: `Assets/_Project/Scripts/ARCHITECTURE.md`: "Raid maps", "Generated raid maps", "World rendering", "Winter, and the year
  in play", "Going along, set-pieces and bosses", "Fishing and the Nøkken", "Transport", "Folk-tale creatures", "Generated models from
  LocalSpawn".
- Data: `Data/Raids/TrollValley.asset`, `Data/Raids/Maps/* Layout.asset`, `Data/Monsters/*`, `Data/Horses/*`,
  `Data/Environment/*Climate.asset`, `Data/World/Safe Haven/Haven World Recipe.asset`.
- Code: `Raids/Map/RaidLayoutRules.cs`, `Raids/RaidHarms.cs`, `Survival/PlayerWarmth.cs`, `Environment/ColdBreath.cs`,
  `Environment/ClimateDefinition.cs`, `Environment/WaterSurface.cs`, `Player/PlayerSwimming.cs`, `Player/PlayerMovementSettings.cs`,
  `Player/PlayerNoise.cs`, `Combat/GroundHazard.cs`, `AI/Perception/Concealment.cs`, `Level/HavenWorldPlan.Water.cs`,
  `Prefabs/Enemies/Hazards`.

**Folklore and history**
- SNL: [nøkken](https://snl.no/nøkken), [fossegrimen](https://snl.no/fossegrimen), [lyktemenn](https://snl.no/lyktemenn),
  [deildegast](https://snl.no/deildegast), [utburd](https://snl.no/utburd), [sjøorm](https://snl.no/sjøorm),
  [moselik](https://snl.no/moselik), [mosefunn](https://snl.no/mosefunn), [Kvalsundfunnet](https://snl.no/Kvalsundfunnet),
  [myr](https://snl.no/myr), [strengmyr](https://snl.no/strengmyr), [pals](https://snl.no/pals), [myrmalm](https://snl.no/myrmalm),
  [jernvinna](https://snl.no/jernvinna), [pors](https://snl.no/pors), [molte](https://snl.no/molte), [knott](https://snl.no/knott),
  [rome](https://snl.no/rome), [jutul](https://snl.no/jutul), [rimtusser](https://snl.no/rimtusser), [hulder](https://snl.no/hulder),
  [seter](https://snl.no/seter), [varde](https://snl.no/varde), [bergtaking](https://snl.no/bergtaking), [oskoreia](https://snl.no/oskoreia)
- [lokalhistoriewiki: Lindorm](https://lokalhistoriewiki.no/wiki/Lindorm), [Sjøormen i Mjøsa](https://lokalhistoriewiki.no/wiki/Sjøormen_i_Mjøsa),
  [Torv](https://lokalhistoriewiki.no/wiki/Torv)
- [Andreas Faye, *Norske Sagn* (1833)](https://www.bokselskap.no/wp-content/themes/bokselskap2/tekster/pdf/norskesagn.pdf)
- [Jærmuseet: peat](https://www.jaermuseet.no/samlingar/?p=401), [Industriarven: Møsvatn and Møsstrond](https://www.industriarven.no/wp-content/uploads/2018/07/8-Mosvatn-og-Mosstrond-1.pdf),
  [Norway's national parks: "Gifts from the bog"](https://designmanual.norgesnasjonalparker.no/uploads/documents/Gavene-fra-myra.pdf)
- [no.wikipedia: Nøkken](https://no.wikipedia.org/wiki/Nøkken), [Utburd](https://no.wikipedia.org/wiki/Utburd), [Lyktemann](https://no.wikipedia.org/wiki/Lyktemann)
- [sv.wikipedia: Bäckahästen](https://sv.wikipedia.org/wiki/Bäckahästen), [Älvdans](https://sv.wikipedia.org/wiki/Älvdans),
  [da.wikipedia: Lygtemand](https://da.wikipedia.org/wiki/Lygtemand)
- English Wikipedia: [Will-o'-the-wisp](https://en.wikipedia.org/wiki/Will-o'-the-wisp), [Myling](https://en.wikipedia.org/wiki/Myling),
  [Lindworm](https://en.wikipedia.org/wiki/Lindworm), [Illerup Ådal](https://en.wikipedia.org/wiki/Illerup_Ådal),
  [Sphagnum](https://en.wikipedia.org/wiki/Sphagnum), [Bog iron](https://en.wikipedia.org/wiki/Bog_iron)
- [Hurstwic: bog iron](https://www.hurstwic.com/history/articles/manufacturing/text/bog_iron.htm),
  [the Frisian boundary-post tale](https://amerisurv.com/2009/04/11/a-ghost-story/)
- Snippet only: [Jammerbugt, "mosekonen brygger"](https://www.jammerbugt.dk/media/gwxlj2lw/tema-4-vildmosens-mystik.pdf)

**Games**
- Valheim: [eip.gg, the Swamp](https://eip.gg/valheim/guides/10-tips-to-survive-the-swamp/), [eip.gg, the Mountains](https://eip.gg/valheim/guides/10-tips-to-manage-the-mountains/),
  [metabot, Wet](https://metabot.gg/en/valheim/status-effects/Wet), [Almar's guides](https://almarsguides.com/Computer/Games/Valheim/Walkthrough/Swamp/),
  [Steam discussion](https://steamcommunity.com/app/892970/discussions/0/3731826842448350835),
  [Shacknews, Bonemass](https://www.shacknews.com/article/122667/how-to-beat-bonemass-valheim)
- Hunt: Showdown: [compound changes](https://huntshowdown.com/news/stillwater-and-lawson-details-of-the-compound-changes-coming-with-up),
  [update 2.3](https://crytek.com/news/hunt-showdown-release-update-23-the-water-is-no-longer-safe),
  [Thundershower](https://huntshowdown.com/news/behind-the-storm-technical-design-of-thundershower-in-hunt-showdown),
  [PlayStation Blog sound interview](https://blog.playstation.com/?p=221483), [CRYENGINE, making Hunt](https://www.cryengine.com/news/view/cryengine-forge-making-hunt-showdown)
- The Witcher 3: [The Ringer](https://www.theringer.com/2025/05/16/video-games/the-witcher-3-10-year-anniversary-open-world-storytelling-quests-bloody-baron),
  [Twenty Sided](https://www.shamusyoung.com/twentysidedtale/?p=42809)
- FromSoftware: [Blighttown](https://en.wikipedia.org/wiki/Blighttown), [nex-3's ranking](https://nex-3.com/blog/a-definitive-ranking-of-poison-swamps/),
  [VideoGamer, Miyazaki](https://www.videogamer.com/news/miyazaki-went-too-far-with-poisonous-swamps-in-elden-ring)
- [Don't Starve wiki: Wetness](https://dontstarve.wiki.gg/wiki/Wetness), [Corrosion Hour: The Long Dark](https://www.corrosionhour.com/the-long-dark-10-tips-for-getting-started/)
- Tarkov: [PlayerAssist, Shoreline](https://playerassist.com/eft-shoreline-map/), [ConsolePCGaming, rain volume](https://consolepcgaming.com/escape-from-tarkov-will-adjust-rain-volume-in-upcoming-update)
- ARC Raiders: [map conditions (Embark)](https://arcraiders.com/news/new-map-conditions), [allthings.how](https://allthings.how/arc-raiders/map/map-conditions/),
  [FRVR, Cold Snap](https://frvr.com/blog/news/arc-raiders-cold-snap-weather-shift-will-kill-you-with-frostbite-and-introduce-new-shelters-to-fight-over),
  [ARC Raiders wiki: Maps](https://arcraiders.wiki/wiki/Maps)
- [RPG Site, the Rotten Vale](https://www.rpgsite.net/feature/6661-monster-hunter-world-effluvia-how-to-deal-with-the-toxic-gas-in-rotten-vale),
  [Unity, V Rising case study](https://unity.com/case-study/v-rising)
- Design writing: [uliwitness, water levels](https://uliwitness.github.io/why-underwater-levels-suck-so-often),
  [Blendo, readability](https://blendogames.com/news/post/2026-06-xx-leveldesign_readability/),
  [Game Developer, speed and control](https://gamedeveloper.com/design/maneuverability-in-games-the-importance-of-speed-and-control)
- Snippet only: [Death Stranding water depth](https://deltiasgaming.com/?p=267799)
