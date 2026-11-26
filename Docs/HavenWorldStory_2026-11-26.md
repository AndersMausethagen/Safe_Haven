# The Haven's world: a reason to walk out, a story to find, a place that lives (2026-11-26)

The user: "the player really hasn't any reason to explore the giant haven map. Right now it's just raid -> haven -> repeat. What
can we do to make the whole world of haven a part of the story? When should we open it up? How can we make it come alive? Also,
how can we make the world more AAA-like in design? Use everything we've learned in creating the raids ... research how other
similar games have designed their world. What can we copy? what can we add and improve? Do a deep analysis and report."

**Read and measured.** Inputs:
- **Earlier documents:** `HavenExplorationReview_2026-11-19.md` (its measurements still hold where nothing has changed since), `HavenWorldDesign.md`, `Territory.md`, GDD §4 and §6, and my notes on the colony direction, the campaign budget and quest pacing.
- **Data:** the atlas (27 places, 12 regions), `HavenPlaceVerbs` and `TerritorySettings`.
- **The mist map**, decoded.
- **Six long play logs since 11-19.**
- **Twelve spots** walked to in Play at noon in clear weather, seen through the game camera (`Docs/Images/HavenWorld_2026-11-26_twelve_spots.jpg`).
- **Two web research passes:** comparable games' home worlds, and AAA world-design craft. Sources are at the end. **[S]** marks a claim from a search summary or a secondary write-up rather than the source itself.

Nothing in the project was changed.

---

## 0. The short answer

The Haven already has the parts of an explorable world: 27 places, a territory loop, lookouts, thoughts at places, waterfalls,
lakes, seasons, night life and a mist. **What it lacks is a reason that comes from the story, and a reason that comes from the
base.** Every system out there is a meter: Insight for a find, hold for a stone, renown for a claim. None of them is a person,
a memory or a thing the Haven needs. The places say a line each, and then nothing more.

The best fit is already in the GDD's own premise: **the forest does not kill the lost, it forgets them.** So make the mist over the
outer Haven *the forgetting itself*. Each region is a part of the old hearth-keepers' village the forest has forgotten:
- the farm that burned the night the hearth went out;
- the quarry where the pact stones were cut;
- the fold, the fishers' rest and the watchtower;
- the stones where the pact was sworn;
- the road the last of them left by.

The player **remembers the land back**, one region at a time. Each region has:
- a short story told in its places;
- a person at home who knew it, or whose craft it was;
- one small task, built from the raids' events: track, carry, hold, escort;
- a payoff the Haven needs: a work site, a resource, a recipe, a road, a resident's passion.

When a region is remembered, the mist draws back from it, colour, birds and game return, and someone from home starts working
there. That gives three answers at once:

- **Story:** the Haven's map becomes Act II's "lost villages" and the answer to "what broke the pact", told at home. The raids keep the
  people and the monsters.
- **When to open it:**
  - one guided walk after the first raid;
  - remembering from raid 3, where the Cold Stones already open;
  - the outer ring after the Heartroot;
  - the last memories in Act II.
- **Alive:** remembered land fills with your own people at work, visitors on the old road, foretold happenings at places, and wildlife
  that comes back.

For the **AAA look**:
- give each region its own identity by restriction (palette, trees, mist colour, sound, life);
- dress every place as a composition you read from above ("what happened here?");
- give the top-down camera things that reach into the frame: smoke, light pillars, birds, sound and wind;
- use the mist on purpose instead of everywhere.

Four of the twelve spots I walked to (Hollow Pines, Ravenwood, Wolfwood, Eastwood) look almost the same.

None of this needs the world regenerated. It is the raids' own toolkit (sites, events, tasks, marks, dressing, grades) pointed
at the Haven.

---

## 1. The Haven now

### 1.1 What stands in it

| | Now | Since the 11-19 review |
|---|---|---|
| Map | 1000 × 1000 m, the portal in the middle; meadows to about 170 m (9% of the map), 11 regions beyond | Unchanged |
| Named places | 27, of 15 kinds (folds, rests, cairns, stones, towers, quarries, camps, cedars, the Barrow, the Den, the Farmstead) | Unchanged |
| What a place does | Found once (Insight, a star on the map). Each kind now has 2 thoughts (`HavenPlaceVerbs`), 3 kinds are lookouts, 4 have a sound, and some hold a find (`PlaceFindTaken`) | **Built 11-19:** thoughts, lookouts, sounds, finds |
| Coverage | 28% of the outer land is more than 150 m (30 s on foot) from a named place. The south-east and north are empty. The Hollow Pines is 21% of the map with 1 place; Alder Mere and Hightarn have none | Unchanged: no place added |
| The territory | 11 boundary stones, claim and hold, pushes, tithes, renown; opened by *The Cold Stones* after raid 3 | Unchanged |
| Life | Game by day, hostile spawn refill by night, prowlers after the first breach, glimpses (Huldra, a hollowed deer), the Nøkk at still water by night, owls, fireflies, seasons, winter ice to the islands | Unchanged |
| Mist | The mist map covers **42% of the land at over half strength and 72% at over a fifth** | Unchanged |

### 1.2 What players did

These are the six long runs since 11-19 (`PlaytestLogger`: `HomeStay`, `RegionEntered`, `PlaceFound`, `LookoutUsed`).

| Run | Played | At home | Home time went to | Regions entered | Places found |
|---|---|---|---|---|---|
| 11-21 15:13 (the long one) | 304 min | 82 min | windows 37, idle 28, **walking 9**, gathering 4, building 4 | 4 (the Meadows, Eastwood, Hightarn, Ravenwood) | 2 |
| 11-23, 11-25 (×2) | 24-33 min | 6-15 min | windows and idle, then walking | 1 (the Meadows) | 0 |
| 11-19, 11-20 | 82-84 min | (before the walk/idle split) | | 1 (the Meadows) | 0 |

In five of six runs the player never left the meadows. In the longest, 9 minutes of walking in 82 at home found 2 of 27 places.
The loop the user describes is what the logs show.

### 1.3 What it looks like at the game camera

![Twelve spots at noon, clear weather](Images/HavenWorld_2026-11-26_twelve_spots.jpg)

Shown in reading order:
1. the portal yard
2. the Shepherd's Fold
3. the Burnt Farmstead
4. the Hollow Pines
5. Ravenwood
6. the Island Stones
7. the Wolfwood
8. the Drowning Fen
9. Eastwood
10. Greyfell
11. the Seven Steps falls
12. the Ruined Watchtower

The black round the frame is the fog of war over ground not yet walked; the player was put there, so little around them is known.

- **The meadows (1, 2) read well.** They are warm, open and have paths. The fold's red stones and the dirt path give the eye somewhere to go.
- **Four woods are one wood (4, 5, 7, 9).** The Hollow Pines, Ravenwood, the Wolfwood and Eastwood all show:
  - tall thin trunks, seen as long diagonal sticks from above;
  - the same white mist billows lying on the ground in clumps;
  - little under them.

  You can't tell which region you're in without the label. Raids got their own identities on 11-17, and these regions never did.
- **The mist hides the ground it covers, everywhere it lies.** The clumps are a good effect, but on 72% of the land they are the same effect,
  which levels the regions into one grey-white texture.
- **The places are single props.** The Burnt Farmstead (3) is a bare log frame on cleared ground. The Ruined Watchtower (12) is a small
  wooden frame. Nothing round them says what happened.
- **The stand-outs are terrain and water:**
  - **Greyfell (10):** sand and rock, a different colour and the clearest region.
  - **The Seven Steps falls (11):** white water down the terrace, the best landmark in the Haven.
  - **The fen's wisps (8):** small lights over the bog.

---

## 2. Why nobody walks out

1. **Nothing out there is needed at home.** The meadows give what the first tiers need, the raids give the rest, and the Keeper's view
   runs the base from anywhere. The territory's tithes are a trickle the player never has to have. ARC Raiders' design director
   says a walkable hub that must be crossed becomes "a chore" [18]. The other side of that lesson: when the hub doesn't need to
   be crossed, it needs a reason to be.
2. **What's out there is a meter, not a story.** Insight, hold and renown are numbers the player already gets elsewhere. The Haven's
   lore ("the Hearth Law", the Barrow Dead, the waystone rubbings) is found in raid caches and read in the journal. It is not
   stood on the land it is about (11-19 review §1.8; still true).
3. **No one points outward.** Eleven written people, each with a craft and a past, and none of them knows a place in the Haven.
   Enshrouded's craftspeople and Spiritfarer's passengers are the reason players head out [3][15].
4. **It opens late and all at once.** Until raid 3 there is no reason at all. Then the Cold Stones open 11 regions at the same moment,
   with meters rather than stories. Valheim, Enshrouded and Dredge open one band at a time, each with its own threat and its own
   reward [1][4][9].
5. **The camera can't see a destination.** At the default zoom the frame meets the ground about 14 m ahead (11-19 review §1.4). A
   tower 40 m off is above the frame, so a landmark must reach the player as sound, smoke, light, birds, the minimap's rim or the
   world map. The Haven uses few of these.
6. **The regions don't look different enough to want to see the next one.** See §1.3.

---

## 3. What other games do

The two research passes, cut to what transfers. Full notes with sources are at the end.

### 3.1 Home worlds that pull you out

| Game | The pull | When it opens | What keeps it alive |
|---|---|---|---|
| **Enshrouded** | The **Shroud recedes** round flame altars. Upgrading the altar grows the safe build area (40, 80, 120, 160 m); strengthening the flame lets you stay longer in the Shroud and opens deadlier zones. These are two separate tracks [1]. Craftspeople rescued from the world each unlock a trade, and their quests point to the next vault [3] | Region by region, paid with things found in the Shroud | People you rescued, working at your base |
| **Valheim** | Each boss's trophy opens the next biome. Dangerous biomes are visible long before you can survive them [4] | Biome by biome | Raids on your base change with what you've beaten |
| **Dredge** | Regions are visible from the start but only survivable with better hull, engine and lights. Each region holds a story relic, and night raises panic [9] | Upgrade by upgrade | The sea's monsters and the night |
| **Stardew Valley** | Finishing a Community Center room repairs the bridge to the quarry, with a short scene. Festivals happen at set places on set days [14] | Bundle by bundle | Townsfolk routines and festivals |
| **Spiritfarer** | Boat upgrades (Icebreaker, Rock Destroyer, Mist Cleaner) open the map. Some need a passenger's favour first [15] | Upgrade and passenger | The passengers' stories |
| **Death Stranding** | Connecting a region changes it: roads and bridges appear and travel gets easier [10] | Connection by connection | Your network |
| **Against the Storm / Gord / Frostpunk** | The land round the settlement is dangerous. Glades and expeditions pay best where it's worst, and one site leads to the next [11][12][13] | Each glade or expedition | Hostility answers how far you push |
| **Hades / Darkest Dungeon** | The hub reacts to every return: new lines, a rebuilt hamlet [16][17] | Every run | Dialogue and visible rebuilding |
| **Ghost of Tsushima / Breath of the Wild / Elden Ring** | The Guiding Wind, golden birds and foxes. Landmarks that hide and reveal (the triangle rule). Few icons, no checklists [20][21][22] | Open early | The world itself moves |

The patterns that keep coming back:
- **a threat you can see that recedes** (Enshrouded, Dredge);
- **raid trophies open home rings** (Valheim);
- **rescued people point outward** (Enshrouded, Spiritfarer);
- **repairing something visibly changes the map** (Stardew, Death Stranding);
- **a few strong finds beat many small ones** (Elden Ring; the checklist fatigue of Palworld's 278 effigies and Ubisoft towers [6][23]).

### 3.2 AAA craft, rules that matter for a top-down camera

These come from the second research pass (talks and developer write-ups). The numbers are the sources' own.

1. **Something to notice about every 40 s** (The Witcher 3 [S]); **a reward every 30 s to 2 min**, never 3 min (Ghost of Tsushima, Game
   Informer). For this camera (a screen is about 27 × 22 m, 5 m/s on foot), that means **a small beat every 1-2 screens and a place
   every 150-250 m**.
2. **The triangle rule at three sizes** (BotW, CEDEC 2017): large shapes are landmarks, medium shapes hide and reveal what's behind,
   small shapes set the pace. From above, the medium masses between screens do the hide-and-reveal.
3. **Landmarks that rise into the frame.** The horizon is off-screen, so use smoke columns, bird flocks and lit shrines. BotW marks camps
   with skull rocks, Ghost thinned trees in front of its landmarks and used drums to mark camps (GMTK, Sucker Punch talk notes [S]).
4. **Guide in the world, not the HUD.** Ghost's wind blows toward one tracked goal; playtests asked for it stronger and more visible.
   **Wind through grass reads very well from above.**
5. **Separate the screen into layers** (Diablo III/IV): dim the background, light the play space, use light to hint at paths, and make the fire-lit refuge
   read by contrast.
6. **Give each biome its identity through restriction.** Ghost limited the plant types per biome and pushed colour. Horizon's ecotopes set assets, tint,
   weather, effects, sound and wildlife together.
7. **Every plant grows somewhere for a reason** (Far Cry 5): grass on exposed hills, forest thicker on shaded slopes, resources in clusters
   with a grammar (mushrooms at the foot of big trees).
8. **Composition rules** (Frozenbyte):
   - primary, secondary and tertiary shapes;
   - no grids and no even random scatter;
   - scale variation of ±20% at most;
   - no long straight lines;
   - "empty areas are as important as detailed areas".

   From above, the ground fills the screen, so terrain tiling is the main giveaway.
9. **Environmental storytelling** (Smith & Worch, GDC 2010): stage props so players ask "what happened here?", pair objects with their
   owner's trade, and let the details add up to a larger picture. Some of it must be missable.
10. **Make everything move** (Ghost): one global wind in every effect, and deer, birds and foxes as guides.
11. **Small and dense, looping back to the hub, with gates in the world** (Far Cry 4 kept Far Cry 3's map size and spent the effort on
    density; Luban's open-world series; Dark Souls' shortcuts). "If you can see it, you can reach it."
12. **Let the hub visibly grow** (Manor Lords' organic plots, Frostpunk's rings round the generator).

---

## 4. What the raids taught us that applies here

The raids have been through the same problem ("why go there?") and got a toolkit for it, all of it data-driven and most of it usable
at home as it is:

| Built for the raids | What it did there | At home |
|---|---|---|
| `RaidLayoutPlanner` sites and quotas (11-26 analysis) | Every pocket gets a site of the right kind; coverage is counted | A **place budget** for the Haven: places per region and the longest gap between places, held by the planner and `HavenWorldPlanTests` |
| Camp dressing (Raider Lean-to, Drying Rack...), event rooms, LocalSpawn props at 5-9k tris | Camps read as camps from the camera | **Dressing kits per place kind**, so a place reads as a scene |
| `RaidEventDefinition`: sites, stakes, paintings, choices with requirements, Defend, Carry, Track and Escort, marks, follow-ups, legends, kinds of day | Events worth the walk | **Haven events**: the same assets, placed at remembered places, peaceful by default and foretold on the homecoming card |
| Guarded stashes, small finds, resource sites on the minimap once seen | Something to find, marked once seen | The same in the mist lands: caches guarded by night, sites marked once seen |
| Walkers on the trails (`RaidGenerator.WalkTheTrails`) | The map has movement | **Travellers on the old road**, and residents walking to their work |
| Per-raid colour grades, skies and decals (11-17) | Each raid is its own place | **Per-region grade volumes** at home |
| Raid edges "walls with gaps", windthrow | Borders that look natural | The **edge of remembered land**: the mist wall, with gaps where the paths go in |
| Interactive mist (billows, wakes) | The mist responds | The player **parting** the mist, and the mist drawing back from remembered land |
| Legends (once per save, a relic, Strange Things in the journal) | Rare, unforgettable moments | **Haven legends**: the Old Grey in the Wolfwood, the lights on Hightarn |
| The Lost, rescue chains, home visits (`HomecomingSettings` › Visits) | People you meet come home | Residents who **point outward** to their region |

---

## 5. The proposal: remembering the Haven

### 5.1 The idea, in the story's own words

The portal says the forest forgets the lost: first the way home, then their names. The Haven's outer lands are the old
hearth-keepers' village, forgotten for a hundred winters. **The mist is the forgetting.** It lies thick where no one remembers and
thin where someone does. The player, who kept the fire, can make the land remembered again, and the place comes back: colour, birds,
paths, and somebody from home working there.

This fits everything already written:
- the hearth feeds the ward;
- the boundary stones are the pact's;
- the places are named for the hearth-keepers' lives (folds, rests, the Farmstead, the Quarry, the Wayfarer's Stones where the old road ended, the Barrow);
- Act II is "investigate the lost villages and learn what fractured the pact".

The Haven *is* the first lost village. The raids hold the lost people; the Haven holds what happened to the place they would come
home to.

### 5.2 How a region is remembered

Each region gets one short arc. It takes 5-15 minutes, fits between raids, and is never upkeep:

1. **A hook from home or a raid.** A resident remembers ("my grandmother spoke of a quarry where they cut the stones that speak"), a raid
   find names a place, or a portal line follows the Heartroot. Held for a homecoming, as the quest pacing rule says (at most 2 new per
   return).
2. **The way there.** Smoke, birds or the wind lead out, and the mist thins along the path as you go (the raids' interactive mist). The
   region's lookout charts it.
3. **The story on the land.** Two or three places dressed as scenes, each with its thoughts. One page for the journal, found at the
   place, not in a raid cache.
4. **One task, from the raids' toolkit:** track a trail, carry the hearthstone home, hold the quarry while the stone is cut, escort a
   lost lamb back to the fold. Often with the resident who knew it along, using the companion system.
5. **Remembered.** The boundary stone lights and the mist draws back from the region over a few seconds, with a short line from the
   portal or the resident. Colour comes back through the region's grade, along with its birds and game.
6. **What the Haven gets**, always something the base needs *now*, so the walk pays at home:
   - a **work site** a resident takes up (the quarry for the Mason, the fold for a herder, the charcoal clamp for the Woodcutter);
   - a **resource** only found there (bog iron, cloudberries, trollstone, alder for the dye vats);
   - a **recipe or plan** (the old fold's fence, the fishers' smokehouse);
   - a **shortcut**: a road or a bridge the player builds, or a lit stone to travel between.

**The territory loop stays, re-framed.**
- *Claiming* a region becomes *remembering* it.
- *Hold* is how well it is remembered.
- The forest's pushes are the forgetting trying to take it back.

Per the campaign budget (11-23: upkeep never blocks the story), **a remembered region never slips while the player is away raiding**. It
only wears where the blight comes from nights the player let get away, which is already the rule (GDD §6.3, 11-14).

### 5.3 The regions, each with its own story

These are suggestions to write from. Ties to people are by what fits their craft or their raid site, and the story beats are
mine to be rewritten.

| Region (share) | Its look (restriction) | What the forest forgot | Places as scenes | Who knows it | Task | Remembered gives |
|---|---|---|---|---|---|---|
| **The Grey Pines** (3%) | Grey pines, charcoal ground, **a smoke column** once relit | The charcoal burners who kept the hearth fed, the last to leave | Charcoal Camp, Trapper's Camp | Tor (carpenter) | Relight the clamp: carry turf, hold it through one burn | Charcoal on site; the clamp as a work site; the smoke is a landmark seen from the meadows |
| **Stillwater** (4%) | Still water, reeds, herons, low dawn mist | The fishers who rowed their dead to the Island Stones | Fisher's Rest, Netmender's Rest, Island Stones (by boat or winter ice) | whoever fishes | Mend the nets; row a lantern to the isle at dusk | A fishing hut site and a smokehouse plan |
| **The Hollow Pines** (21%, now 1 place) | Hollow dead pines, bracket fungus, grey light | **The farm that burned the night the hearth went out.** Its hearthstone is cracked; "somebody tried to keep this fire going after the roof was gone" (its thought today) | Burnt Farmstead; new: the family's trail and a grave in the pines | Asa (her raid site is a farmstead) | Track the family's flight through the pines | An old seed (a heritage crop for the fields); the farm rebuilt as an outpost farm |
| **Ravenwood** (16%, the richest) | Ravens, black birch, the Raven Stair falls | **The quarry where the pact's boundary stones were cut** | Old Quarry, Raven Stair (a lookout at the top) | Gunnar or Bjarne | Hold the quarry while a new boundary stone is cut | The quarry ring for the Mason; stone tithe; Raven Stair lookout |
| **Eastwood** (10%) | Elder cedars, deep moss, standing stones | **Where the pact was sworn** | Standing Stones, Elder Cedar, Woodland Camp, Grey Quarry | Liv (rune scholar) | Read the stones at night (Runecraft); a choice that echoes the pact | The Pact page; a ward rune; the spirits' favour |
| **The Wolfwood** (8%) | Bones, claw marks, cold blue mist | The watch that fled its tower | Wolf Den, Ruined Watchtower, Broken Tower, Pilgrim's Stones | Eirik (soldier) | **The Old Grey**: a named wolf, a Haven legend (once per save) | The watchtower as a Watch Post outpost; the wolves keep away from the road |
| **Westwood** (12%) | Wind, cairns on bare knolls | The cairn-builders who marked the ways for travellers | Raven Cairn, Lookout Cairn | Runa (tracker) | Track the old cairn line | **Travel between lit stones** (the 11-19 review's proposal 14) |
| **The Drowning Fen** (2%) | Green-grey haze, wisps, bog cotton | The drowned road: those who tried to leave by the bog | Drowned Stones; new: a sunken causeway | Yrsa? | Lay a plank path across (Construction) | Bog iron; cloudberries; a way to the Wolfwood |
| **Alder Mere** (1%, no place) | Alders and a spring | A healer's spring (new) | New: the Alder Spring | Hild? | Clear the spring | A healing draught and dye |
| **Greyfell** (12%, already distinct) | Sand, rock, the Seven Steps falls | **The road north: where the last of them left**, and where the troll that broke the stones walked | Farewell Stones, Wanderer's Stones, Windy Cairn, the Seven Steps (to be named) | Sigrid (expedition leader) | Climb the Seven Steps; stand the Farewell Stones | Trollstone; the view of the road north (Act II) |
| **Hightarn** (1%, no place) | The high tarn, cold light, aurora | **The last memory: who let the hearth go cold** | New: the tarn shrine | the portal | Act II's end | The answer to "what broke the pact" |

The Wayfarer's Stones (the old road arriving, north-east) and the Old Barrow (the hearth-keepers' dead, south-west) are two more
scenes: one for travellers to come in by, one for the end of Act II.

### 5.4 When to open it up

| When | What opens | Why then |
|---|---|---|
| Prologue to raid 1 | The meadows only. The mist wall stands visible at about 170 m, with the smoke of the Grey Pines beyond it | Learn home first; show what's out there |
| **After raid 1** (the first homecoming) | **One guided walk out**, under 10 minutes: Tor asks for the charcoal clamp (the Grey Pines). It teaches the lookout, the thoughts, the mist parting and a work site | The 11-19 review's "one walk out in the first hour"; the logs show none happens today |
| **After raid 3** (the Cold Stones, as now) | **Remembering opens**: the near ring (Stillwater, the Hollow Pines, Ravenwood). Each rescued resident brings their region's hook within 1-2 homecomings | The loop is known by then; people come home with stories |
| **After the Heartroot** (end of Act I) | The outer ring: Eastwood, the Wolfwood, Westwood, the Fen, Greyfell's road north | The GDD's "then the road north opens" |
| **Act II** | The Barrow, Hightarn: the last memories | Act II's "learn what fractured the pact" |
| Post-game (11-23 answer) | All remembered: festivals at places, the Haven continues | The user's "the Haven may continue after the story ends" |

**The gates are in the world, not walls** (Luban; Dredge):
- **Thick mist** slows the player and wears them with the raids' Mist-touched. It is walkable, but it says "not yet".
- **Deep water** needs the boat, or winter ice.
- **Cliffs** need a rope ladder Tor builds.
- **The night** brings the prowlers.

Everything stays visible from the start, Valheim-style.

---

## 6. Making it come alive

| Idea | From | How here | Uses what exists |
|---|---|---|---|
| **People at work out there** | Gord, Manor Lords, Stardew | Remembered places become work sites. The Mason walks to the Old Quarry, a herder takes sheep to the Shepherd's Fold, the fisher to the Rest. They walk the roads at their hours and are home at night | Keeper's view jobs, quarry rings, felling rings, outposts (today abstract: make them stand on the land) |
| **Roads you build** | Death Stranding, Stardew's bridge | A path to a remembered place can be laid as a road: carts and horses go faster on it, and residents use it | Building system, carts, horses |
| **Travellers on the old road** | Valheim's traders, Stardew's visitors | From the Wayfarer's Stones down to the portal: the pedlar (his home visit is built), a pilgrim, a tinker, now and then a trader. They walk in, stay a day, and walk out | Home visits (built 11-26), raid walkers |
| **Foretold happenings at places** | ARC Raiders' conditions, Stardew's festivals | One every couple of homecomings, told on the homecoming card: a swarm in the Mother Maple, a storm-felled tree across the road, a run of fish at the mere, a lamb lost from the fold (escort), lights on Hightarn (night). Peaceful by default | `RaidEventDefinition` (sites, choices, tasks, paintings, kinds of day) placed by a Haven director at remembered places |
| **Midsummer and Yule at places** | Stardew's festivals | A bonfire at the Stillwater shore, the Yule log brought from Ravenwood: the residents gather and a song is sung | Seasons, residents' small scenes, voice lines |
| **Wildlife that comes back** | Horizon's ecotopes | Game, birds and fish rise in remembered regions; ravens follow the player in Ravenwood; birds flush ahead along the wind | Game zones, `WildlifeHerd`, birds |
| **The forgetting fights back** | Enshrouded, Against the Storm | Corruption nights and pushes come from unremembered land (already the rule). Make it visible: the mist creeps a little toward the meadows the night before, and blight decals spread on the paths | Territory pushes, corruption nights, interactive mist |
| **Night is a different map** | Cyberpunk's night content, Dredge | Some places only by night (the Nøkk's pool, the wisps' secrets, the tarn's aurora); night pays for its risk at home | Nøkk, wisps, prowlers |
| **The hub answers every return** | Hades | The homecoming card already speaks. Add one resident's line about the region last remembered, and the portal's about the next | Homecoming, voice lines |

---

## 7. The AAA look: what to change

In order of what the camera shows most:

1. **Region identity through restriction.** Today four woods look the same (§1.3). Give each region:
   - 2-3 dominant tree species and an understory;
   - its own ground palette;
   - mist colour and height;
   - a colour grade volume, as the raids got;
   - a sound bed;
   - one bird or beast;
   - one signature prop.

   Use §5.3's "look" column. Judge it with a side-by-side sheet like the one above: you should name the region without its label.
2. **The mist, used on purpose.** Thick and slow at the forgotten border (the gate). Thin wisps in remembered land. Coloured per region (the fen
   green-grey, the Wolfwood cold blue, Greyfell almost none). Today the same billows cover 72% of the land and level every region
   into one grey-white texture. **Its retreat is the most visible reward in the game: make it a moment, with time to watch it.**
3. **Places as compositions.**
   - Every place gets a hero silhouette readable inside one screen, a story ("what happened here?"), dressing from its trade (nets at
     the rests, tools at the quarry) and a clearing shaped so the camera frames it.
   - Use LocalSpawn props as for the raid camps, at 5-9k triangles.
   - The Burnt Farmstead is the first to do: the fallen roof, the cracked hearthstone, a child's toy, scorch on the ground (decals),
     a burnt fence line leading off into the pines (the trail).
4. **Things that reach into the frame and off-screen** (top-down landmarks):
   - **Smoke** from the relit clamp and from work sites.
   - **Light pillars** at lit boundary stones, seen in the mist at night.
   - **Bird flocks** that rise over places as you come near.
   - **Sound beacons:** the quarry's hammering, the fold's bells, the falls.
   - **A place on the minimap's rim** once seen.
   - **The guiding wind:** grass and leaves blow toward the tracked goal, the Ghost of Tsushima way, which reads well from above.
5. **Density by screens.** A small beat every 1-2 screens (a wayside scene, a find, an animal, a sound) and a place every 150-250 m. Today 28%
   of the outer land is more than 150 m from a place. Add places where the map is empty (the south-east, the north, the Hollow
   Pines, Alder Mere, Hightarn), and count coverage in the planner as the raids' layout does.
6. **Trees from above.**
   - At pitch 48-50° tall thin trunks read as long diagonal sticks, and many of them read as clutter (§1.3).
   - Mix in crown-heavy species, more undergrowth and fallen wood, and keep clearings.
   - Thin the trees in front of landmarks and along paths, as Ghost does, and as the 11-06 note found for paths (trunks 4 m off the tread).
7. **Layers and light.** Keep the play space lit and the background dimmer. Light along the paths and at places. The fire-lit settlement should read as
   the refuge by contrast at dusk.
8. **Terrain macro variation and tiling break-up**, from the look-and-feel plan. From above, the ground is most of the screen.
9. **Vistas.** At the top of the falls stairs, the lookouts and Hightarn, a `CameraZone` pulls the camera out and tilts it (built, and used once,
   at the prologue's fire).

---

## 8. Order of work, sizes, and how to know it worked

| Step | What | Size |
|---|---|---|
| 1 | **Region identity pass**: palettes, grades, mist colour and height per region, sound beds, birds. The art fix every later step stands on | M (data and art, no code) |
| 2 | **The remembering**: the mist's strength per region from its remembered state; the draw-back moment; the territory re-framed (names, lines, upkeep only under blight) | M |
| 3 | **The first walk out** (after raid 1): the Grey Pines' clamp with Tor; smoke; the first work site | S-M |
| 4 | **Places as scenes, ring by ring**: dressing kits per place kind (LocalSpawn); the Burnt Farmstead first; 5-7 new places in the empty wedges | M-L, spread out |
| 5 | **Region arcs**: the near ring (Stillwater, the Hollow Pines, Ravenwood) with their residents and tasks, from the raids' events | M per region (mostly writing and data) |
| 6 | **Life**: residents walking to work sites, roads, travellers on the old road, the Haven events director | M-L |
| 7 | The outer ring and Act II's memories (Eastwood, the Wolfwood with the Old Grey, Westwood, the Fen, Greyfell, Hightarn, the Barrow) | L, with Act II's writing |

**Signs that it worked**, read from the play logs that exist today:
- the player leaves the meadows before raid 2 in most sessions;
- walking is over 25% of home time (today 9 of 82 minutes);
- in sessions past raid 3, at least one region is remembered per two homecomings;
- `PlaceFound` reaches half the places by the Heartroot.

A hand playtest of the first two hours, then of a session past raid 5.

---

## 9. Decisions for the user (recommended first)

1. **The mist as the forgetting, and remembering as the Haven's story.** *Recommended: yes.* *Alternative:* keep the territory as it is, and
   add stories to the places only.
2. **Re-frame the territory loop** (claim becomes remember; hold wears only under blight; pushes are the forgetting). *Recommended: yes, keep
   the numbers.* *Alternative:* a separate remembering track beside the territory.
3. **The opening schedule** (a walk after raid 1; remembering from raid 3; the outer ring after the Heartroot; the last memories in Act II).
   *Recommended: as in §5.4.*
4. **Residents point outward** (each written resident gets one region's arc, held for a homecoming). *Recommended: yes.*
5. **Remembered places become work sites with people walking there.** *Recommended: yes*, starting with the quarry, the fold and the clamp.
6. **Haven events**: one foretold happening every couple of homecomings, peaceful by default. *Recommended: yes*, after step 5.
7. **What exploring pays.** *Recommended:* work sites, unique resources, plans, roads and travel, lore, the residents' passions; never
   Essence, relics, people or raid plans (GDD §7.7, the 11-19 review's decision 1). *Alternative:* small boons for the next raid as well.
8. **Where to start.** *Recommended:* step 1 (region identity) and step 3 (the first walk out) together. The first makes the land worth
   seeing; the second puts a player in it.

---

## Sources

**Comparable games (research pass 1).** [S] marks a guide or summary.
- [1] Enshrouded Flame Altar: https://enshrouded.wiki.gg/wiki/Flame_Altar
- [2] Ancient Spires [S]: https://allthings.how/enshrouded-1-0-all-ancient-spire-locations/
- [3] Craftspeople [S]: https://allthings.how/enshrouded-1-0-why-crafting-depends-on-rescuing-craftspeople/
- [4] Valheim biome order: https://www.pcgamer.com/games/survival-crafting/valheim-biome-order/
- [5] Dragonwilds review [S]: https://www.sypnotix.com/reviews/runescape-dragonwilds-review
- [6] Palworld watchtowers: https://palworld.wiki.gg/wiki/Watchtower
- [7] Sons of the Forest caves [S]: https://pindrop.gg/sons-of-the-forest/map/caves
- [9] Dredge: https://www.gamedeveloper.com/production/leveraging-the-unseen-to-turn-players-worst-fears-against-them-in-dredge
- [10] Death Stranding [S]: https://gamespot.com/gallery/death-stranding-multiplayer-guide-explaining-how-t/2900-3159/
- [11] Frostpunk scouting [S]: https://www.dtgre.com/2018/04/frostpunk-scouting-locations.html
- [12] Against the Storm glades [S]: https://techraptor.net/gaming/guides/against-the-storm-glades-guide
- [13] Gord: https://shacknews.com/article/136367/gord-is-the-darkest-dungeon-of-colony-sims
- [14] Stardew bundles and festivals: https://stardewvalleywiki.com/Bundles, https://stardewvalleywiki.com/Festivals
- [15] Spiritfarer boat [S]: https://guidestrats.com/spiritfarer-boat-improvements/
- [16] Hades: https://gamespot.com/articles/hades-changes-what-it-means-to-be-a-roguelike/1100-6483420/
- [17] Darkest Dungeon: https://darkestdungeon.wiki.gg/wiki/Heirlooms
- [18] ARC Raiders' Speranza: https://www.pockettactics.com/arc-raiders/speranza
- [20] Ghost of Tsushima's world: https://www.espn.com/esports/story/_/id/29524519/how-made-world-ghost-tsushima
- [21] BotW triangles: https://kotaku.com/breath-of-the-wilds-biggest-design-secret-lots-of-tria-1819113140
- [22] Elden Ring's map: https://www.inverse.com/gaming/elden-ring-killed-the-map-game-good-riddance
- [23] Towers: https://jboger.substack.com/p/towers

**World-design craft (research pass 2).**
- **Pacing and density:**
  - Game Informer on open-world density (Ghost, Valhalla, Spider-Man): https://www.gameinformer.com/2021/05/20/more-than-just-dots-on-a-map-the-challenges-behind-open-world-game-creation
  - The Witcher 3 "40 seconds" [S, a transcript]: https://voicetube.com/videos/73552
- **Breath of the Wild:**
  - BotW triangle rule (CEDEC 2017, translation): https://gist.github.com/idbrii/e39fe96279aa1670319bfa521d907399
  - GMTK on BotW [S]: https://gmtk.substack.com/p/how-nintendo-solved-zeldas-open-world
- **Ghost of Tsushima:**
  - GDC Vault: https://gdcvault.com/play/1027399/Exploration-in-Ghost-of-Tsushima
  - VFX Voice: https://vfxvoice.com/creating-the-way-of-the-warrior-for-ghost-of-tsushima/
- **Diablo:**
  - Diablo III art direction (GDC 2012): https://gamedeveloper.com/design/gdc-2012-diablo-iii-s-art-director-shows-off-design-process
  - Diablo IV readability: https://news.blizzard.com/en-us/article/23788294/diablo-iv-quarterly-updatemarch-2022
- **Environmental storytelling:** Smith & Worch, GDC 2010: https://www.gdcvault.com/play/1012647
- **Procedural worlds:**
  - Far Cry 5 procedural world: https://blog.playstation.com/2018/03/22/the-procedural-world-generation-of-far-cry-5/
  - Horizon ecotopes: https://www.guerrilla-games.com/read/gpu-based-procedural-placement-in-horizon-zero-dawn
- **Composition:** Frozenbyte: https://wiki.frozenbyte.com/index.php/Level_Art:_Composition
- **Open-world structure:** Luban, open-world level design part 3: https://gamedeveloper.com/design/open-world-level-design-the-full-vision-part-3-5-
- **Hub growth:** Frostpunk radial design: https://news.xbox.com/en-us/2019/09/20/frostpunk-console-edition-radial-driven-design-of-gameplay-and-controls/
