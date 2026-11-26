# Raid layout analysis: places, nodes, enemies (2026-11-26)

The user: "analyser raids og hvordan de er satt opp mtp points of interest, noder, fiender, layout etc. ser du noen forbedringer vi
kan gjøre der?" (analyse the raids and how they are set up for points of interest, nodes, enemies and layout; can we improve
them?)

**Read only.** Nothing in the game was changed. The evidence comes from three places:
- the code: the planner, the generator's point placement, spawn lists, events and rumours;
- the data of the three generated raids (`Data/Raids/MistyWoods`, `TrollValley`, `MossMarsh`) and their scenes' spawn groups and anchors;
- measurements: Editor evals that planned **24 seeds per raid** at the normal size (200 m), plus the user's playtest log
  `20261121-151353.csv` (12 Misty Woods raids, 2026-11-21 to 11-25, played in the build and the Editor).

Scripts and preview sheets are in the scratchpad (`...\scratchpad\raids\`), which a reboot can clear.

What came before:
- `WorldDesignReview_2026-11-17` (G1-G10) gave the maps districts, edges, compounds, node landmarks, roads and dressing.
- `RaidLoopResearch_2026-11-22` covered pacing, risk and reward.

This document is about what stands **on** the map: places, nodes and creatures, and whether the player can read them. It does not
repeat those two.

## Status (2026-11-26, the user: "gjør alle dine anbefalinger")

Built as recommended, including the recommended answers to §5: every pocket holds something (Q1), and the Woods' Marsh Lights go down
while the widows stay (Q2). R7 and R8 wait for a playtest (Q3, and the order in §4). Nothing here has been played by hand.

| # | As built |
|---|---|
| R1 | Four resource lists, one per group (Ore Veins, Timber, Herbs and Fibres, Life Wells), each filling all of its group's points. `SpawnGroup` › Site Share gives each group its share of the sites by what its kinds weighed in the old list at tier I, so totals hold (Woods 0.67 / 1.37 / 1.09 / 0.87). Ore Veins › Farthest Sites takes the far sites (`RaidMapMarkers.ShareOut(distances, weights, farthest)`). Spirit Spring joins the Life Wells and Ore Collapse the Ore Veins (one each at most) |
| R2 | Caches: 5 pockets in the Woods (was 12), 4 in the Valley and Marsh (was 8). The Supply Caches list fills every pocket: real caches about 2 in 5 (Woods) or 1 in 2, and the rest **small finds**: Dropped Sack, Old Offering, Wanderer's Remains (`Prefabs/Raids/Caches`, about half a cache's table, `LootCache` › Small Find: no lock, regional loot, relic or inscription), plus the Abandoned Camp at most once. The Valley and the Marsh have the Woods' crates and barrels about their sites |
| R3 | `RaidEventDirector.Begin` takes a free event room at the event's distance, then up to 1.6 × its far distance, then any spot out of sight (`RaidSpots.TryTakeRoom`). The Event Sites list is empty; its Wolf Den went to the Creatures list |
| R4 | Camps: 7 in the Woods (was 9). Creatures lists place a pack of 1-2 at each camp, scaled by density (2-3 at day density 1.5, more at night); great creatures and pack prefabs are `SpawnTable.Entry` › Alone. A **Camp Stash** stands beside every camp (`SpawnGroup` › Every Site, the Camp Stashes list), locked to search until the creatures that stood round it are dead (`GuardedCache`) |
| R5 | Troll Valley: pockets 4-8 m off the trail with 60% of the open ground, 8 resource sites. All three raids' plans meet at least 90% of every quota (test) |
| R6 | Every node landmark has a sound (crows, raven, woodpecker, howl, bittern). Lookouts already scale with a large map's area |
| R9 | `CameUpon` playtest rows (`RaidPlacesSeen`): kind and group, seconds into the raid, metres from the start |
| R10 | A Trail Walkers list (1-2, from patrol spots) whose creatures walk a round through the nearest camps and patrol spots (`RaidSpawnList` › Walks The Trails, `MonsterPatrol`) |
| Review | A stash with nobody at its camp stays open: only a friend's copy looks for guards again, and a creature walking a round never guards. `CameUpon` reads the places once the raid runs. A walker's round hangs under the map, not its spawn group, whose children are points. The wider room search starts past the event's own reach. A capped entry in a pack list must stand alone (test) |
| Q2 | Woods Creatures: Marsh Light 12 → 3 (3% of the list); Small Moss Troll 16 → 22 and Black Widow 12 → 15 keep tier I from being mostly wolves |

**Checked live** (Misty Woods, Play mode, three seeds): the log reads Creatures 13 of 21 points (7 camps), Herbs 48, Life Wells 36, Ore 24,
Timber 60 (168 nodes, as before), Supply Caches 5 of 5, Camp Stashes 7 of 7, Trail Walkers 2. Every stash said "Guarded: N of the camp
still stand". The walkers moved between camps. An event stood 0 m from an event room (before the stretch, one landed 40 m off: the rooms
lay 80-134 m from the start). An Old Offering read from the game camera with its name and glint. `CameUpon` rows were written.

**Round 2 (same day, the user: "sett i gang med 1-4 ... Bruk gjerne localspawn ... lag gjerne flere, spennende og unike events"):**

| # | As built |
|---|---|
| 1 | Resource sites on the minimap: a site whose group has a Map Icon (`SpawnGroup` › Map Icon: copper ore, oak, healing herb, life orb) is marked once seen through the fog of war, until its nodes are gathered (`MinimapHud.ReadResourceSites`, `RaidPlace.Icon`) |
| 2 | Camps look like camps: `RaidLayoutRules` › Camp Dressing (a Raider Lean-to, a Drying Rack, the Bone Pile), 2-3 a camp, 6-8 m out, facing in (`RaidMapGenerator.PlaceCampDressing`, own stream). The Camp Stash has its own look (a hide-wrapped bundle on stones) in place of the supply crate |
| 3 | Event rooms checked in a launched Troll Valley: the Troll's Cookpot stood 0 m from its room, no clutter within 3 m |
| 4 | Co-op: trail walkers made on a friend's copy after the host leaves get their round (`CreaturePuppets.RefillTheRaid`) |
| Events | Seven new ones, each raid's own. **Troll Valley:** Rockfall on the Ledge (dig a buried pack out of fresh scree, or grab and risk the slope), The Troll's Cookpot (fish out the trolls' bones and loot and have two runts come back for you, or taste the broth: +30 health or poison), The Herder's Lur (blow it hard: the valley and its exits revealed and everything hears; or the herders' call: faster feet). **Moss Marsh:** The Bog Body (take the ring and maybe wake a draug, or lay them back: armour and purified essence), Lights on the Water (follow them to a drowned man's purse or into marsh lights; or light the lantern and fight what comes), The Peat Cutter's Hut (rest, search, or pay two pennies). **Misty Woods:** The Charcoal Clamp (tend it for charcoal, or break it open hot). Pools: Woods 11, Valley 10, Marsh 8 |
| Art | LocalSpawn (Hunyuan3D + UltraTex, `props_manifest.json`): Raider Lean-to, Drying Rack, Camp Stash, Scree Slide, Troll Cookpot, Herder's Lur, Bog Body, Marsh Lantern, Peat Hut, Charcoal Clamp, 1,700-8,000 LOD0 triangles; event paintings in `event_art.json`. UltraTex and Hunyuan3D licences: re-texture/check before a release, like the others |

Tests: `RaidCampsAndEventsTests1126`. Checked live in Troll Valley (the cookpot in its room, lean-tos at the camps, iron ore sites as one cluster, icons on the minimap).

**Tune after a playtest:** camp stash loot (`Data/Loot/Caches/Camp Stash`, 0.6 × a Hunter's Cache, 7 a map); pack size; small-find
tables. Golden layouts re-pinned (5 caches, 7 camps); `RaidWorldDesignTests` keeps its old pins by restoring the old counts.

---

## 0. The short answer

**The maps are well shaped now, but most of the places on them are empty, and the full ones all look alike.** A 200 m Misty Woods map
plans **63 sites**. About **37% of them hold nothing** at the start of a raid:
- 10 of the 12 cache pockets;
- 2-3 of the 6 event rooms;
- the 10 patrol spots, which only fill at stages the player never reaches.

Each empty cache pocket has its own side trail leading to it. The full places are uniform:
- every resource site holds the same mix of 23 kinds of node;
- every camp holds one spawn;
- the raid's own events never use the rooms set aside for them.

So the map *says* "there is something down every path" and then mostly isn't true. The player learns that quickly, which fits what
the logs show: they visit 16-38% of the map and leave after about five and a half minutes.

| # | Finding | Where it comes from |
|---|---|---|
| F1 | **Empty places with paths to them.** Woods: about 10 empty cache pockets a map, Marsh about 6, Valley about 4. In the Woods they hold only breakable crates; in the Marsh and the Valley there is no clutter list, so nothing at all | Cache quota 12/8/8 sites, but the Supply Caches list fills 1-3 (`RaidSpawnList`); `PlaceClutter` dresses every cache, camp and event site whether filled or not |
| F2 | **Every resource site is the same soup.** The four groups (Ore Veins, Timber, Herbs and Fibres, Life Wells) are filled from **one** list of 23 kinds, so an "ore vein" holds oak, nettles and life orbs. There are about 170 nodes, evenly spread, so nothing is worth seeking | One `Resources` list names all four groups; each site's 12 points belong to one group (`RaidMapMarkers.ShareOut`), but every group draws from the same mix |
| F3 | **Troll Valley doesn't fit its quotas.** Only 4.2 of 9 resource sites fit, 4.3 of 6 camps, and 1.4 of 2 rumour sites. That gives about **50 resource nodes** (Woods 169, Marsh 108), **4-5 creatures** at the start (Woods 9), and a second rumour that lands on random ground in 3 runs of 10 | Beside-path pockets need room the valley floor lacks; the plateau beyond the walls (about 60% of the map) is unused |
| F4 | **Events ignore the event rooms.** The Trader's Wagon, the Cursed Tree and the Living Stones stand on random ground 'out of sight' around the player. The 6 planned event rooms get static props instead: a Wolf Den creature, two resource nodes and a camp station | `RaidEventDirector.Begin` → `RaidSpots.TryFindSpot` (a random angle and distance from the player) |
| F5 | **No quiet ground, no busy ground.** The median distance from a site to its nearest neighbour is 10-13 m, and a site stands within 25 m of 94-99% of the short route. Together with F1, the map is evenly sprinkled, mostly with nothing | Quotas sum to about 60 sites on 200 m; separations of 16-28 m |
| F6 | **A camp is one spawn with no reason to clear it.** Each camp's three points lie within 8 m and the list keeps spawns 12 m apart, so a camp gets one creature or one pack. Night (+30%) and tier density change nothing at the start: the number of camps sets the count. No camp guards anything | `RaidMapDefinition` › EnemyCamp 3 points, spread 4 m; Creatures list separation 12 m |
| F7 | **Every exit is on the far rim** (96% in the far third, 129-206 m from the start). Every run flows start → far side | `ExitRooms`; G6 extraction kinds not built |
| F8 | **Landmarks are silent.** The G4 fields (sound, reveal) are built, but no landmark has a sound, and each map has one lookout | `RaidLayoutRules` › Landmarks: `sound` empty in all three raids |

The ten recommendations, ranked, are in §4. Two of them are **data only** and fix the largest problems (F1, F2). Three questions for
you are in §5.

---

## 1. What a raid holds now (measured)

### 1.1 Sites planned per 200 m map (mean of 24 seeds)

| Site | Woods | Valley | Marsh | What fills it at the start | Mean distance from the start (Woods) |
|---|---|---|---|---|---|
| Camp (in a clearing) | 9.0 | 4.3 (of 6) | 6.0 | Creatures list: **one spawn each** | 129 m (54% in the far third) |
| Resource site (a pocket beside a trail) | 14.1 | 4.2 (of 9) | 9.0 | Resources list: 12 nodes each, a mix of 23 kinds | 101 m |
| Cache (a pocket, prefers height) | 12.1 | 6.4 (of 8) | 8.0 | Supply Caches: **1-3 of them** | 100 m |
| Event room (a clearing) | 6.0 | 4.0 | 4.0 | Event Sites list: 3-4 static props | 96 m |
| Patrol (on a trail) | 10.0 | 6.0 | 6.0 | Only the stage and alarm groups (stage 1 at 4:00) | 124 m |
| Side (a dead end) | 5.0 | 2.2 (of 3) | 3.0 | Story objects (`RaidMapAnchor`) | 113 m |
| Rumour (a dead end, prefers height) | 3.0 | 1.4 (of 2) | 2.0 | The briefing's 2 rumours (3 with a Tracker) | 114 m |
| Apex | 1 | 1 | 1 | The raid's great danger | 162 m |
| Exits | 3 | 3 | 3 | Always | 160 m (Valley 137 m) |

Other things on each map: set pieces 4.5 / 1.2 / 3.8, landmarks 4.3 / 2.9 / 4.2, one lookout, short flags every 100-140 m on the
roads.

**Empty at the start (Woods):** about 10 caches, 2.5 event rooms, 1 rumour dead end without a Tracker, and 10 patrol spots: about 23
of 63. The patrol spots carry no clutter and no side path, so they cost nothing. The cache pockets and dead ends each have a side
trail, which is the "Road to Nowhere" in Upton's list (`WorldDesignReview` §1).

### 1.2 What the player did (12 raids, 2026-11-21 to 11-25, all Misty Woods)

| | Range | Median |
|---|---|---|
| Raid clock at the end | 184-465 s | **333 s** (the 11-22 study's 17 raids: 273 s; the first five here overlap it) |
| Distance walked | 358-994 m | 716 m |
| Share of the map's 25 m squares entered | 10-24 of 64 (16-38%) | 15 |
| Farthest from the start | 109-350 m | 150 m |
| Kills | 6-21 | 10 (of 143: wolves 34%, Rootlings 25%, Mistwings 13%) |
| Time in fights | 4-31% | 16% |
| Deaths | 3 of 12 (the last two to a Root Guardian and a Marsh Light at tier II) | |

- **Something happens often**, mostly pickups. Four raids had no gap of 30 s or more without one; the others had 1-4 gaps of 30-65 s
  (longer ones were pauses). The "something every 30 s" target (`WorldDesignReview` §1) is met by gathering, not by places.
- **Nodes mined against nodes standing** (the three raids with `NodesMined` rows): copper 3 of 15, trees 2-3 of 11-17, oak 1 of 33,
  life orbs 1-8 of 11-19. One of each of many kinds.
- **The bag ended at 83% of its slots in all three raids with a `BagAtEnd` row** (weight 27-65). With 23 kinds of node, a slot fills
  per kind, not per load. This may be a reason players go home: F2 is costing slots.
- **The always-open West Beacon was the exit in all three raids that logged it** (`ExitLit`).

---

## 2. The findings in detail

### F1. Empty places with paths to them

The planner places sites to a quota, but whether one is filled is the spawn list's choice. `PlaceClutter` then puts 2-5 breakables
at every camp, cache and event site, filled or not. In the Woods a cache pocket is therefore a side trail to a few crates. In the
Valley and the Marsh, whose rules have no clutter list, it is a side trail to nothing.

Other games use chance containers too: Tarkov fills a container now and then. The difference is that a container is *something to
open*; an empty pocket is just ground.

**Effect:** side paths stop being worth checking. Walking straight to the exit and gathering what lies on the way is the best play,
which is what the logs show.

### F2. The resource soup

Each of the 14 resource sites gives its 12 points to one of the four groups. All four groups are filled by the single `Resources` list:
- oak 30%, nettle 16%, sticks 12%, small rocks 12%, life orbs 13%, copper 9% and so on;
- so a typical site holds 3-4 oaks, about 1 copper, 2 nettles, 1-2 life orbs and the rest one each.

What this costs:
- **Nothing to look for:** copper is everywhere a little, so no place says "ore here".
- **Nothing to remember:** the four group names (Ore Veins, Timber, ...) do nothing.
- **Slots:** a visit to one site yields 6-8 kinds (see the bag above).
- **Distance means nothing:** every list uses the band "Any", so a 60 m site is as rich as a 160 m one. This was already noted in
  `RaidLoopResearch` §1.1 and is still true.

The stage veins (2026-11-22) are the one place where ore is concentrated, and they appear after most players have left.

### F3. Troll Valley doesn't fit its quotas

The Valley has no human runs yet, so this is from the plans:

| | Quota | Placed (mean) | Result |
|---|---|---|---|
| Resource sites | 9 | 4.2 | ~50 nodes; the Woods have 169 |
| Camps | 6 | 4.3 | ~4-5 creatures at the start; the Woods have 9 |
| Caches | 8 | 6.4 | |
| Rumour dead ends | 2 | 1.4 | In 3 runs of 10 the second rumour falls back to a random spot 60+ m off (`RaidRumourDirector`), with no dead end or set piece |
| Side | 3 | 2.2 | A story object can share a site (`PlaceAnchors` takes `index % count`) |

The valley floor leaves no room for beside-path pockets 9-15 m off a trail. Meanwhile the plateau beyond the walls is about 60% of the
map and stays empty. The bench path and the cirques (G8) were never built.

**Effect:** the Valley runs at tiers II-IV, where iron is the metal that matters, yet it is the poorest map: about 3 iron nodes at tier II (9% of the tier's weights × ~50).

### F4. Events ignore the event rooms

`RaidEventDirector.Begin` places each event at a random angle and distance from the player, on ground out of sight with a path back.
It never looks at the map's event rooms. Those rooms get the older static props from the Event Sites list:
- Wolf Den (a creature);
- Spirit Spring and Ore Collapse (resource nodes);
- Abandoned Camp (a crafting station).

So the best written content in a raid (choices with a price) lands wherever chance puts it: in a barrier gap, beside a pond, on a slope.
Meanwhile the clearings planned and dressed for it hold a prop.

### F5. Evenly sprinkled

The median nearest-neighbour distance between sites is 11 m (Woods), 10 m (Valley) and 13 m (Marsh), and 57-74% of sites have
another within 12 m. Along the short route, a site stands within 25 m of 94-99% of its length.

The districts (G1) change *which* kinds go where, but not how dense the map is: it is equally full everywhere. That leaves:
- no quiet ground to cross between places;
- no busy place to aim for;
- nothing a minimap shows as "over there".

### F6. Camps

A camp's three points (one at the centre and two 4 m out) all lie within 8 m. The Creatures list keeps spawns 12 m apart, so each
camp gets one spawn: a single creature or one pack prefab.

The list asks for 7-10 × density 1.5, which is 10-15 spawns. Night (×1.3) and the tier's density raise that further. But every one of
the 9 camps (6 in the Marsh, 4-5 in the Valley) is already full at 10, so **night and density add no creatures at the start**. They
still raise the roamer cap. That differs from what the GDD says: "a night raid holds 30% more creatures".

A camp also guards nothing. The caches are in their own pockets, and nothing about a camp says "clear me for X". The things worth
fighting for are set pieces, rumours and the apex. A camp is just an obstacle.

### F7. Exits on the far rim

All three exits are on the far rim, 129-206 m from the start (Valley 101-174 m). Nothing pulls the player sideways or keeps them in the
middle.

The extraction kinds of G6 are still unbuilt:
- a closing exit;
- a conditional exit (the Valley's ferry for a toll);
- a hidden exit from a rumour;
- one exit in the middle of the map.

### F8. Silent landmarks

G4 built sound, reveal and node placement. In the data, no landmark has a sound, `knownAtStart` is off everywhere, and each map has one
lookout.

The camera sees about 15 m ahead (`WorldDesignReview` §0.4), so sound is the only thing that can call the player from 60-100 m. The
code is ready and only the sounds are missing. Crows at the Elder Cedar, water at a fall, or wind in the stones would each do, and
`Tools/Audio` can make them.

### Also noticed (smaller)

- **Creature mix in the Woods at tier I:** wolves 17%, small moss trolls 33%, **Marsh Lights 25%** and **Black Widows 25%** (each 12%
  weight with no tier limit). A Marsh Light killed the player in the Woods. The Valley's foreign creatures were kept under 5%
  (`RaidIdentityResearch`, decision 2). Whether the Woods should be a quarter Marsh Lights is your call (Q2).
- **The plateau:** the Valley's ground beyond the walls is still unused (`WorldDesignReview` §3.2). It is the cause of F3.
- **No log row says which places the player reached.** `Reached` is quest-only. A "came upon" row (site kind and distance) would show
  which places draw players and which are passed by. This is the heatmap `WorldDesignReview` §4 asked for, aimed at places.

---

## 3. What other games do (from the earlier research, not new)

- **Hunt: Showdown:** a few dense compounds, transit between them, one mark per compound (`WorldDesignReview` [4]-[7]).
- **Ghost of Tsushima:** something calls every ~30 s (about 140 m at our run speed), and that something is a *place*, not a pickup [27]-[29].
- **ARC Raiders:** high-loot areas marked on the map and drawing traffic; extracts as the strongest flow tool [9].
- **Valheim:** each location placed by rules, priority first, with its own clear ground [1].
- **Darkest Dungeon** (`RaidIdentityResearch` §1): each region tilts toward one kind, so the place tells you what you will find.

Taken together: fewer places, each full, each readable as a kind of place, with the richest farther out.

---

## 4. Recommendations, ranked

| # | Change | Fixes | Effort |
|---|---|---|---|
| **R1** | **Themed resource sites.** Split `Resources` into four lists, one per group: Ore Veins (ore and stone, 6-8 nodes, band Middle/Far), Timber (trees, sticks), Herbs and Fibres (plants, berries), Life Wells (orbs, motes). Each site then reads as one kind. Keep totals about equal, so the economy holds | F2, slots, distance | **S, data only** (the group binding already gives each site one group) |
| **R2** | **Fewer, fuller pockets.** Cut the cache quota to about 5 (Woods) or 4 (Valley, Marsh), and fill 2-4 of them. Give every other pocket a **small find**: a fallen traveller's bag, a herb patch, a lore stone, a small vein. No beside-path site stays empty. Give the Valley and the Marsh a clutter list of their own (peat stacks, seter barrels) | F1, F5 | **S-M**, mostly data; small finds are new prefabs on `LootCache` |
| **R3** | **Events take event rooms.** `RaidEventDirector.Begin` first tries an unused event room within the event's Site Distance and out of sight, and falls back to random ground. The static props move to clutter or side sites | F4 | **M**, one method plus a test |
| **R4** | **Camps worth clearing.** Each camp gets a stash (a cache or a small vein at its centre) and 2-3 creatures of one family. A list of "camp stash" spawns that shares the camp's points needs a group that takes *every* site of its kind instead of a share. Night and density then add creatures to a camp, as the GDD says | F6 | **M**, small code plus data |
| **R5** | **Fit the Valley.** Let beside-path sites use the benches and the plateau's edge, or a valley rule with shorter pocket reaches. Add a test that each raid's plans meet at least 90% of every quota over 24 seeds, so this can't drift unseen again | F3 | **M** |
| **R6** | **Landmark sounds** (data plus `Tools/Audio`): crows, water, wind in the stones, a bell. Two lookouts on large maps | F8 | **S** |
| **R7** | **Busy and quiet ground.** After R2, let a district's role set density: Approach sparse, Lair dense. Join camps, caches and veins into 3-5 clustered places, with transit between | F5 | **M** (the G1 weights exist; this adds a density per role) |
| **R8** | **One exit in the middle** (G6): a closing exit or a conditional one (a toll, a key from a rumour) | F7 | **M-L**, touches UI and co-op. **Ask first** (Q3) |
| **R9** | **A "came upon" log row** for sites, set pieces and landmarks | Evidence | **S** |
| **R10** | **Patrols at the start:** one or two small groups that walk between camps on the trails, instead of empty patrol spots until stage 1 | F6 | **M**, needs a walking behaviour; after R4 |

**Order:** R1 and R6 first (data, no risk). Then R2 and R9. Then R3 and R4. R5 before the Valley is played. R7 and R8 after a
playtest shows what R1-R4 did.

**How to tell it worked:**
- The squares entered go from 16-38% to over 40%.
- The clock at the end goes past stage 1 (over 240 s *after* the reveal).
- `NodesMined` shows several of one kind from one site.
- The bag's slots stop filling before its weight does.
- The new R9 rows show players turning down side paths.

---

## 5. Questions for you

1. **Fewer, fuller places (R2):** should every pocket hold *something*, even a small find? Or do you want some kept empty, so a side
   path stays a gamble? I recommend that every pocket holds something.
2. **Marsh Lights and Black Widows in the Misty Woods** (12% weight each, from tier I; together half of the tier I creature picks):
   keep, or lower to the Valley's under-5% rule? I recommend lowering the Marsh Lights and keeping the widows, since the Woods' spiders
   are newer and meant to be there.
3. **An exit in the middle (R8):** build now, or after a playtest of R1-R4? I recommend after.

---

## Appendix: how this was measured

- **Plans:** `RaidLayoutPlanner.Plan(rules, 200, seed × 7919)` for seeds 1-24 per raid. Sites by kind and distance from the start.
  Nearest neighbour between sites. A 2 m walk of the short route for the share with no site within 25 m. No terrain was built.
- **Fill:** each raid's `SpawnLists` (counts, groups, bands, entries) with the scenes' Spawn Groups (`site`, `trigger`, read from
  YAML) and `RaidMapDefinition` › Site Rules (camp 3 points spread 4 m; resources 12 points spread 8 m).
- **Logs:** `Position` rows every 2 s (path, 25 m squares, farthest point); `ItemObtained`, `ResourceGathered`, `Killed`, `RaidEvent`
  and `EventChoice` as moments; fights as combat rows clustered within 6 s. Wall time includes pauses, so long gaps over a minute
  were left out.
- **Preview sheets** (`sheet_MistyWoods.png`, `sheet_TrollValley.png`, `sheet_MossMarsh.png`) were drawn with
  `RaidLayoutPreview.WriteSheet`.
