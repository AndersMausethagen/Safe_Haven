# The Haven world: what is there, what is missing, what to build (2026-11-18)

The user: "look at how Safe Haven is built. How can we improve it and give it more variety and be exciting to explore and help
player progress etc?"

**Read and measured:** GDD §4, §6, §7.7, §9.5, §15, §21; `HavenWorldDesign.md`, `Territory.md`, `RescueOrder.md`,
`BaseProgression.md`, `PrologueAndQuestFlow_2026-11-18.md`, `WorldDesignReview_2026-11-17.md` (raids only; it left the Haven
alone, so this is its counterpart), `SystemDesignReview_2026-11-17.md`, `TODO.md`. The recipe (`Data/World/Safe Haven/Haven
World Recipe.asset`), the plan and generator code, the atlas (`Resources/Atlases/SafeHaven.asset`, decoded cell by cell),
`Resources/TerritorySettings.asset`, every quest, request and story asset, the place prefabs, and `SafeHaven.unity` (spawn zones
and resource points assigned to regions through the atlas grid). Pictures: the grass colour map (`Art/Terrain/SafeHaven/Haven
World Terrain_GrassColour.png`), the mist map (`Data/World/Safe Haven/Haven World Mist.png`), today's in-game shots and a render
of the current atlas (scratchpad `hwr/atlas_now.png`, which a reboot can clear). The playtest CSVs since the 11-10 rework.
Nothing in the project was changed.

---

## 0. The short answer

The Haven is well built as **ground**: terraces that read from above, roads in valleys, no dead ends, 99.8% of dry land
reachable, 27 named places, 12 regions, a lake, a bog, two meres, game, mist and a territory loop. What it lacks is the
second half of a world: **things to do at the places, regions that differ, a reason to go out before the third raid, and a
land that changes as home grows.** Five numbers say it:

1. **0 of 15** place prefabs carries any component. **13 of 27** places give nothing but 25 Insight when found.
2. **All six woods are one forest**, the Misty Woods raid's (`recipe.woods = MistyWoods Map`), so the outer Haven is the first
   raid without a clock: same trees, same mist, same creatures.
3. **The region labels contradict the world.** Ravenwood says *Wary* and holds 6 of the 12 deep-woods spawn zones and 101 of 406
   resource points; the *Deadly* Wolfwood has 3 hostile zones, none deep.
4. **The Hollow Pines is 21% of the map with 1 place**; Alder Mere and Hightarn have none; the Wolfwood (8.5%) has 6.
5. **No human run since the rework reached raid 3**, where The Cold Stones opens the land. Their Haven time was spent within
   about 50 m of the portal.

The fixes that matter most reuse what exists: give each kind of place a verb, make lookouts work for a top-down camera, open
the first stone after raid 1, make the region labels true, put soft gates on shortcuts, let rescued residents leave marks on the
land, and let the mist draw back from held land.

---

## 1. How the Haven is built today

### 1.1 Layout

- **A 1 × 1 km terrain centred on the portal.** The yard (55 m) is level; the ward grows from 16 m to 49 m at most. **Haven
  Meadows** (to 170 m, 9 ha) is all buildable: shelves, two grassy knolls 4-5 m high ("a lookout's place", GDD §6.4), a stream
  into a fishing pond 72-95 m out. Then the woods: the edge band from 200 m, the Misty Woods from 330 m, the deep woods
  390-480 m; the corners reach 707 m.
- **Each direction has a job:** Stillwater, the lake, east (300 m, 2-3 islands, a landing); Greyfell, the mountain, north-west
  (from 260 m, 60 m high); the Drowning Fen south-south-west (345 m); Hightarn, a tarn, north; Alder Mere west-south-west.
- **Height as lines:** benches 7 m apart, about three cliffs on a walk out, a ramp wherever a path climbs, a scramble every
  90-150 m, about 20 km of cliff, 21 falls.
- **Ways:** six radial roads in valleys, a ring path at 230 m, loops further out, 30-36 glades (meadow, rocky, lair, wet), 8 in
  10 with a second way out. A cairn at each junction (42 in the scene); a small scene beside a path every 45-70 m (Fallen Giant,
  Log Stack, Hunter's Rest, Waystone).
- **Life:** game (deer, stag, fox, wild boar) in the meadows and open glades by day; hostile zones in the lair glades, refilled
  only by night; wild horses in the ring round the heart; glimpses (the Huldra, a hollowed deer, a small spirit); the Nøkk at
  still water by night.
- **Mist** lies over the woods (0.55) and the bog (1.0), with clear holes over the glades.

The grass colour map shows the whole design at once: green meadows in the middle, the bog's mottled square to the south, three
waters, Greyfell's pale contours in the north-west, and otherwise one even dark wood with terrace lines running round it like
the rings of a tree.

### 1.2 Regions

The woods are wedges between the six roads, named by bearing (`BuildHavenAtlas`). Counted from the atlas and the scene:

| Region | Share | Danger shown | Hostile zones (deep) | Resource points | Places |
|---|---|---|---|---|---|
| The Hollow Pines | 20.8% | Dangerous | 8 (4) | 65 | 1 (Burnt Farmstead, on its edge) |
| Ravenwood | 15.9% | Wary | 8 (6) | **101** | 2 (Old Quarry, Wayfarer's Stones) |
| Westwood | 12.4% | Wary | 4 (1) | 31 | 2 (two Lookout Cairns) |
| Greyfell | 12.1% | Dangerous | **0, and no game** | 66 (47 mountain ore) | 3 |
| Eastwood | 10.4% | Wary | 2 (1) | 55 | 4 |
| Haven Meadows | 9.1% | home | 0 | 30 | 3 |
| The Wolfwood | 8.5% | **Deadly** | 3 (0) | 15 | **6** |
| Stillwater | 3.8% | Calm | 0 | 3 | 3 |
| The Grey Pines | 2.9% | Wary | 0 | 7 | 2 |
| The Drowning Fen | 2.1% | Deadly | 4 (bog) | 33 | 1 |
| Alder Mere | 1.3% | Calm | 0 | 0 | **0** |
| Hightarn | 0.8% | Wary | 0 | 0 | **0** |

### 1.3 What there is to find

| Place kind | Count | What it gives besides 25 Insight on finding |
|---|---|---|
| Wayfarer's Stones (Pilgrim's, Wanderer's, Farewell) | 4: every road end not at water | 3 forage points |
| Old Quarry (Grey Quarry) · Elder Cedar (Grandmother Cedar) | 2 each | 4 rock points, tier 2-3 · 3 forage points |
| Fisher's Rest (Netmender's Rest) · Shepherd's Fold (Ewe Fold) | 2 each | 2 forage points · 2 plant points |
| Mother Maple · Drowned Stones | 1 each | 3 forage · 4 bog plants |
| Lookout Cairn (Raven, Windy) · Woodland Camp (Trapper's, Charcoal) | 3 each | nothing |
| Ruined Watchtower (Broken Tower) | 2 | nothing |
| Standing Stones · Island Stones · Old Barrow · Burnt Farmstead · Wolf Den | 1 each | nothing |

Beside them, the 4 Waystones take an offering once a day (the only roadside piece with a verb), and 11 boundary stones stand at
runtime for the territory. A place is a prop group 8-14 m across plus a radius at which it counts as found.

### 1.4 What pulls the player out, and what changes as home grows

- **Opening (about minutes 13-20):** everything is within 50 m: sticks, rocks, berries, the overgrowth, the Rootbound Stones;
  water at the pond.
- **Until raid 3:** materials by distance (loose stone 80-260 m, stone and copper 250-400, hardwood 300-480, iron 380-480,
  Greyfell's ore up to tier 4, bog plants), hides from game, fishing, wild horses, and Insight for finding places and exploring
  (1 per 150 cells). No quest, request or story asset names a single Haven place (all of `Data/` searched).
- **After raid 3:** The Cold Stones opens the territory: claim a region at its stone, hold it, answer the forest's pushes, do
  what a land asks (hunt, gather or fish N there), draw a tithe, earn renown.
- **As home grows:** buildings, yards, homes, footpaths, smoke and lit windows; the ward from 16 to 49 m; the rank from Overgrown
  Camp to Haven; residents' outings out to 190 m; the seasons (winter ice walks to the islands); lit stones. Past the meadows
  nothing else changes: the woods, their mist and their places look the same on day 1 and day 100.

---

## 2. Where it falls short

**F1. Places are props.** HavenWorldDesign's rule 4 is "every place pays: a landmark with nothing at it teaches the player to
stop looking". Today 13 of 27 places pay only the find, and the names promise what the props lack:
- the Charcoal Camp has no charcoal, and the Old Barrow no barrow-wight and no grave goods;
- the Wolf Den has no wolves of its own: hostile zones sit in lair glades and ignore places;
- the Lookout Cairns and Watchtowers stand high (`high: true`) but don't look out.

The raids already have every verb these need, unused at home: `RaidLookout` (lifts the fog), `LootCache` with loot tables,
`ExperienceStone`, `FieldCamp`. The scene has none of them.

**F2. Regions are names on one forest.** The generator paints the six woods from one `RaidMapDefinition`, the Misty Woods raid's,
with one default mood. TerritorySettings describes six different woods ("tall, dead-straight pines with nothing growing under
them. Sound carries strangely here"; "old pines where the ravens sit and watch the road"), but only the region title says which
one you are in. The GDD wants each region its own and the Haven lighter than the raids. Instead the outer 80% of the Haven *is*
the first raid's biome, with its creatures (Forest-Bitten Wolf, Moss Shaman, Mistwing, Small Moss Troll, Draug, Huldra). That
dulls both: the Haven loses its look, and the first raid loses its otherness.

**F3. The labels misinform, and the world is lopsided.**
- On entering Ravenwood, the richest and most dangerous wood (§1.2), the region title says "Wild · Wary · Softwood, berries,
  game".
- The Wolfwood is shown as Deadly ("few go in, and fewer come out the same") and is one of the safest woods.
- Greyfell is Dangerous with no creatures at all.
- Places follow the plan's luck: one south-west wedge (the Wolfwood with Westwood) holds 8 of 27, the largest region holds one,
  and Alder Mere and Hightarn hold none, so their stones need no find to claim (`Math.Min(placesToClaim, places)`).

**F4. Nothing can be seen from afar, and nothing says where a trail goes.**
- The explore camera (pitch 50°, 14-28 m, FOV 40) shows about 30 × 22 m. Nothing more than about 20 m ahead is in frame,
  however tall it is.
- So in a 1 km world the map does all the seeing, and the map shows only explored ground.
- The 42 junction cairns exist "so the way can be found without the map" (recipe tooltip), but they say nothing.
- The boundary stones use the Ward Totem's model and are "easy to miss in the outer woods' mist" (TODO). The meadows'
  decorative "Boundary Stone" scenes share their name and do nothing.

**F5. The land never shows progress.** The mist is the same over a Secure region as over a wild one, and a rescue changes the
town, not the land. Territory's "Not yet" list (residents working held regions, blight on the map) is exactly this gap.

**F6. Nothing in the land opens.** The planner's own number: 99.8% of dry ground is reachable on foot from minute one. The gates
that exist sit on nodes (tool tiers on ore, hardwood and the big overgrowth) and on the islands (boat, winter ice). There is no
"I'll come back with a copper pick" moment in the land itself, and the one story gate, the cold stones, opens after raid 3.

**F7. The pull comes late.** The three human runs since 11-10 (67, 63 and 49 minutes; 1-2 raids each) never reached raid 3. At
home they did the opening quests, the workshop, the chest and the hut, and everything they gathered there was tier 0-1 (Oak
Wood, Sticks, Small Rocks, Grey Stone, Berries, Nettle Fibre, a little Hardwood). No deer, stag, fox or wild boar was killed at
home in any run since 11-10, tests included. The logger has no position, place-found or region rows, so this shows what they did, not where they walked. It is still
enough to say that before raid 3 the land past the meadows gives nothing a player is asked for.

**F8. Visiting doesn't feed raids.** The colony direction is "base unlocks that feed raids". The land feeds raids only through
territory (Secure land keeps corrupted nights out); nothing seen or done out there changes a briefing.

---

## 3. Proposals, ranked

Effort: S (under half a day), M (1-2 days), L (more). Proposals 1-3 and the quick half of 4 need no regeneration of the world.

### 1. Give every kind of place a verb (S each, M for all)

**The player** finds that each kind of place does one thing, and the same kind always does the same thing, so a name on the map is
a promise:

| Kind | Verb | Built from |
|---|---|---|
| Lookout Cairn, Ruined Watchtower | Look out (proposal 2) | `RaidLookout` |
| Standing Stones, Island Stones | "The stones remember": once, mark one unfound place on the map as *heard of* | `ExperienceStone` + a map pin |
| Old Barrow | A barrow chest (lockpicked, heard) with a Haven loot table; a draug rises there at night | `LootCache`, a night `MobSpawnZone` |
| Wolf Den | The pack lives at the den; beat it for a den hoard (hides, bone, a lost traveller's pack) | `MobSpawnZone`, `LootCache` |
| Burnt Farmstead | Whose farm it was (a page about the old hearth people); relight its hearth: a rest spot that stays | Story page, `FieldCamp` made lasting |
| Woodland, Trapper's, Charcoal Camp | Make the names true: snares (hide), a smoking clamp (Charcoal), a rest by the fire | Nodes, `FieldCamp` |
| Wayfarer's Stones | The offering, plus a carved road line each; the four lines tell where the old road went (it ended at the portal) | `OfferingStone`, story pages |
| Elder Cedar, Mother Maple | Spirit places: the Huldra favours them; seasonal sap and forage | `SpiritBonds`, `GlimpseWatcher` bias |
| Fisher's Rest | A spot where one of the largest fish lives | `FishInTheWater`, FishSizes |
| Old Quarry | A one-time stack of cut Stone Blocks (what the altar asks for) | `LootCache` |
| Shepherd's Fold | Wild horses come here to graze: the place to lasso one | `HorseHerd` spawn bias |
| Drowned Stones | The Nøkk plays here at night | `SpiritBonds` still-water spot |

**Why:** it turns 27 dots on a map into 27 reasons (F1), by editing prefabs, which reaches every instance without regenerating.
**Fits:** GDD §7.7 decides what home may pay (materials and knowledge, never Essence, relics, people or raid plans). Haven loot
tables take no `RaidOnly` item, and `LootOnDeath` already strips those at home. Finds already go to the map and journal; the
portal says one line per kind, the first time.
**Risks:** caches are one-time and personal in co-op, as raid loot is. A night draug at the barrow must keep the ward rule.

### 2. Lookouts that work top-down: climb, look, know where to go (S-M)

**The player** climbs a knoll crown, a Lookout Cairn, a Watchtower or Greyfell's top bench and presses E:
- the camera rises to about 80 m for four seconds, and the fog lifts in a 100 m circle;
- places inside it appear as *seen* pins (found only when walked to), so one walk shows the next two;
- the portal names one: "Smoke over the Hollow Pines. Someone burns charcoal there";
- the first lookout used each turn adds one rumour to the next raid's briefing ("From the Raven Cairn you saw lights moving in
  the Misty Woods"). The briefing is already pinned to the turn.

**Why:** height is the one long sightline a top-down camera can give (F4). A repeatable payoff is a reason to go back to an
explored place, and it feeds raids (F8).
**Fits:** `RaidLookout` (the fog lift), `InGameCinematic`/`CameraShot` for the rise, the raid rumour system (a twin of Runa's
extra rumour). A Watch Post on a knoll could get the same look from home at its Lookout level.
**Risks:** tree crowns between the camera and the view (fade them, as the dialogue camera does); keep the rise short and
skippable; in co-op each player has their own camera and fog.

### 3. Open the land after the first raid, not the third (S)

**The player**, on the first homecoming, gets one optional green quest from the portal: "There is a stone by the water I can't
reach. Go and read it." It sends them to the nearest cold stone (Stillwater, 210 m, past Fisher's Rest and Netmender's Rest),
shows the region on the map and teaches that lands have names, dangers and goods. The stone stays cold; The Cold Stones lights it
after raid 3, as now.
**Why:** F7. Until raid 3 the land past the meadows asks nothing of the player, and no human run has got that far yet.
**Fits:** quest data only. The hold rule (two Haven quests per homecoming) keeps it from crowding; the stone already says which
way its nearest place lies; it pairs with fishing, which the prologue's rod taught.
**Risks:** one more quest in a busy first hour. Keep it held and optional.

### 4. Make the regions honest, then fill the gaps (S, then M)

- **Quick (S, data):** rewrite TerritorySettings to match the world. Ravenwood becomes *Dangerous* with "hardwood, iron, stone";
  the Wolfwood *Wary* until it gets its packs; Greyfell says "no creatures, only the falls and the wind", or gets creatures.
- **Better (M):** creature bands take a region weight, as `bogOnly` takes the bog, so the Wolfwood holds the packs and the deep
  zones spread out. New place rules by region: two or three places in the Hollow Pines (a charcoal pit, a hunters' hide, the
  echoing hollow its description promises), an ice-fishers' hut on Hightarn, a reed-cutters' landing on Alder Mere.

**Why:** F3. A label that lies teaches players to ignore labels, and the map's biggest wedge is its emptiest.
**Fits:** `TerritorySettings`, `HavenCreatureBand`, `HavenPointOfInterest` (a new region or bearing field).
**Risks:** the better half needs a regeneration, and the atlas bake lives in a retired script (`BuildHavenAtlas`), so give it a
non-retired Editor entry first. Place ids are kind plus index (`haven.old_quarry.1`): add new kinds rather than renumber, so
saved finds stay true.

### 5. Gates that teach, on shortcuts and extras (M)

**The player** sees each gate before they can open it, and the map says what opens it:

| Gate | Opened by | Gives |
|---|---|---|
| A rockfall on Greyfell's ramp | A copper pick (Overgrowth's tool tiers), or Hild's stone steps | The short way to the high ore benches and the Windy Cairn |
| A broken footbridge where the ring path crosses the river | Tor at Lodge II, with planks (mended like a broken building) | Cuts the ring walk; before it, the ford is far |
| The bog's sunken causeway | Planks and Tor (the unused `Boardwalk` prefab) | The Drowned Stones without wading past Bog Spitters |
| The Old Barrow's inner door | Liv reads it (after Rubbings) | A barrow room: lore and a one-time chest |
| The old road north: cairns from the ring path to a troll-felled tree | The Road North (Act II) | Seen from day one, the promise PrologueAndQuestFlow §4.3 asks for; opens to a lookout over the way north |

**Why:** F6. A gate seen before its key lets a tool, a rescue or a story beat change the map. Valheim does it with the antler
pickaxe that copper needs, and with swamp crypts that need the Elder's key.
**Fits:** the rescue order (each person's gate comes after their rescue), `Overgrowth`, `BuildingRepairs`, the Upgrade Building
and Repair quest kinds, the portal's voice on each opening, and SystemDesignReview R13's bundles that pay in access. Stand gates
at runtime from data, as `TerritorySpawner` stands the stones, so a regeneration can't wipe them.
**Risks:** never gate the main path or a material the next step needs (the Haven's ore also comes from raids); at most one gate
per region.

### 6. Residents leave marks on the land (M)

**The player** sees each rescue change one place outside the town, and finds that person there on their outings:

| Person | Mark | Where |
|---|---|---|
| Ingrid | A herb bed; healing herbs regrow there | Mother Maple |
| Bjarne | A smoking charcoal clamp; Charcoal to take | Charcoal Camp |
| Tor | Mends the bridge (proposal 5), a woodpile | The ring path's river crossing |
| Eirik | A watch fire, a lit lane at night | A knoll on the ring path |
| Yrsa | A smoking rack, cooked fish | Fisher's Rest |
| Gunnar | A hunter's hide; game near it shows on the minimap | A meadow glade |
| Liv | Carved stones at the junctions (proposal 9) | The ring path's junctions |
| Asa | A field on a shelf | Haven Meadows |
| Runa | Blazes on the trees that glow after dark (her rescue's marks) | The trails into the Wolfwood |
| Hild | Stone steps (proposal 5), a mended dry-stone wall | Greyfell's ramp, Shepherd's Fold |
| Sigrid | Her horn on the cairn; blown at dusk, it calls the residents home | Lookout Cairn |

**Why:** the GDD asks for "a lively fairy-tale village" and §15.6 for a world that answers a rescue. Today it answers only inside
190 m (F5).
**Fits:** `ResidentDefinition` gets a place id, a mark prefab and an outing spot, and `ResidentLife` outings go there by day.
`Footpaths` will wear trails to them by itself. It is the first step of territory's "residents working held regions".
**Risks:** residents far out at dusk meet prowlers, so send them only by day and only along lit lanes or into held land. Each
person is content to make: start with the first three home (Ingrid, Bjarne, Tor).

### 7. The mist draws back from held land (M)

**The player** watches the mist answer the territory. Claiming a region thins its mist, and a Secure region loses most of it,
its lit stone glowing through what is left. A slipping region lets the mist back in; when the forest gathers to push, the mist
rolls in from that side; each Haven rank widens the meadows' clear edge.

**Why:** the land shows progress (F5), and the lore already says it: "inside the ring nobody lost the way home". Enshrouded makes
its Shroud the measure of progress the same way: the stronger the flame, the deeper you can go.
**Fits:** `TerrainMist` reads a 256² region map. Write a runtime copy each turn, scaled by the atlas grid and each region's hold.
It needs no regeneration and costs one small texture a turn.
**Risks:** losing the mood. Keep the night veil, the morning mist, the bog and the deep belt past about 400 m always misty.

### 8. Six woods, not one (M-L)

**The player** knows each wood at screen scale: Ravenwood's old pines with ravens on dead snags; the Hollow Pines' tall straight
trunks over a bare needle floor, with an echo; Eastwood's open spruce and trappers' lines; Westwood's cedar ridges and
watchtowers; the Grey Pines' stumps and fallen stone; the Wolfwood's dense young spruce, bones and clawed bark. The inner woods (170-400 m) are the Haven's own, and lighter. The deep belt keeps the raid's misty cedar, as the border where
home ends.
**Why:** F2. This meets the art direction (each place its own, the Haven lighter than the raids) and gives the first raid back its
strangeness.
**Fits:** the recipe gets a per-region list (bearing range, forest definition or mood, glade landmarks and dressing, ambience),
and the generator's `Woods` pass picks by region. Assets on hand: Norway Spruce A-C, Scots Pine A-C, Dead Cedar, Cedar Giants,
Fallen Giant, Log Stack. `PlaceAcoustics` gives the Hollow Pines its echo.
**Risks:**
- a full regeneration (about 3.5 minutes; the atlas bake as in proposal 4);
- terrain tree prototypes and frame time: measure at fixed spots;
- seams between woods: blend over 30-40 m at the roads, and check close-ups and the game view before committing (the user
  rejected a hard seam once).

### 9. Signposts that name where the trails go (M)

**The player** reads each junction cairn on approach: "Ravenwood · Old Quarry ↑ / Home ↓". Regions are named always, places once
found. Until Liv is home the marks are old runes nobody can read, so her rescue lights up the trails.
**Why:** F4. In a 1 km top-down world the way should be findable from the ground as well as from the map.
**Fits:** the plan's path graph (`HavenWorldPlan.Paths`) knows where each branch leads. Bake the labels into the atlas, use the
Signpost decoration's model, and show the label within about 5 m, like an interaction name.
**Risks:** clutter, so show labels on approach only. The labels must be baked again with the atlas.

### 10. Secrets worth the walk (M)

**The player** finds two or three secrets in each region, off the trails:
- **where:** behind a fall, on a table rock reached only by a scramble, on a ravine floor, on the tarn's island;
- **hints a top-down player can catch:** a raven circling, a glint, a sound from the spot (`PlacedAmbience`), a resident's
  line ("there's something under the falls below the quarry");
- **what they pay:** a lore page about the hearth-keepers, a decoration plan (Rune Stone, Carved Post), a rare fish or herb spot,
  Insight, and one fixed Pact Rune per two regions, so exploring pays into claiming.

**Why:** reasons to leave the trail, and for an explored region to still hold something. Grounded hides its Milk Molars round the
yard, and players comb it because the reward is permanent.
**Fits:** a planner rule for secrets (off-path, on a bench reached by a scramble, near a fall), counted like dull walks;
`LootCache` with a Haven loot table; `ExperienceStone`. Stand them at runtime from data.
**Risks:** the Pact Rune economy (Territory.md counts on about one rune per two raids), so keep these few. Never put anything
from §7.7's raid-only list here.

### 11. The lands' tasks point at places, and the land has goings-on (M)

**The player** gets tasks with a place and a reason: "The boars are rooting up the Burnt Farmstead's graves: drive them off"
instead of "Hunt 6 in the Hollow Pines". Each turn, one or two goings-on appear at known places and leave if ignored: a pedlar
camped at a Wayfarer's Stones, wild horses at a fold, the Huldra at an Elder Cedar, a blight patch to burn, fish running at a
river mouth, a stranger from an earlier waking (approved story).

**Why:** explored places stay alive. Today a found place never changes.
**Fits:** `TerritoryState.TaskOf` (a task kind plus a place from the atlas), the territory's turns, the hold rule,
`QuestWaypoint`, raid event data for the content.
**Risks:** nagging. Keep it to two at a time, never urgent, nothing lost by ignoring one.

### 12. A lit stone is a way home (M)

**The player** steps out at any lit boundary stone from the Lands tab, and from one can "Return to the portal". There is no
stone-to-stone travel, and none during a push or a watch. From the Village rank a Field Camp can stand for good at one Secure
stone: rest, a small chest, basic crafts.
**Why:** a corner is 575 m out, four minutes there and back on foot. Claiming wants a payoff felt every evening, and the stones'
lore already promises "a road home".
**Fits:** `HavenTravel` (already steps the player out beside a building or person), `BoundaryStone`, `FieldCamp`, and the portal:
"A lit stone remembers the way home".
**Risks:** walking loses its worth. Lit stones only, by day, keeps horses and paths useful.

### 13. Night is a different map (S-M)

**The player** finds things only the night shows: lit stones and the ward glow at the minimap's edge, the barrow and the
Drowned Stones show lights and things that rise, Runa's blazes glow, glow-mushroom rings lead to a secret, the Nøkk plays at the
tarn.

**Why:** night already changes the danger, since hostile zones refill only at night, but not what can be found. Night should
pay for its risk.
**Fits:** `VisibilitySource`, the fog's night veil, `SpiritBonds`, `GlimpseWatcher`, spawn-zone hours, `PlacedAmbience` curves.
**Risks:** readability. Keep the pale moonlight rule.

### 14. Measure where players go (S)

Add to `PlaytestLogger` a position row every 5-10 s, plus PlaceFound, RegionEntered, StoneLit and LookoutUsed rows. Draw a heatmap
over the atlas picture in the Playtest Runs window. Today no row says where anyone walked in the Haven (F7); the raid review asked
for the same.

---

## 4. Order of work

1. **No regeneration:** 14, then 1 (starting with Lookouts, Barrow, Den and Wayfarer's Stones), 2, the quick half of 4, and 3.
   Then one hand playtest of the first two hours.
2. **Placed at runtime from data:** 5 (three gates first: the Greyfell stair, Tor's bridge, the bog causeway), 6 (Ingrid, Bjarne,
   Tor first), 7 and 13.
3. **With a regeneration**, once the atlas bake has a non-retired entry and place ids stay stable: the better half of 4, then 8,
   9 and 10.
4. **Last:** 11 and 12, once the territory loop has been played by hand (its balance is still a first guess, per TODO).

**How to know it worked:** every place kind has a verb; no region has fewer than one place per 5% of the map, and its danger
label matches the zones in it; the heatmap shows walks past the meadows before raid 3, and a lookout or gate used in every session.

---

## 5. Decisions for the user

1. **What may exploring the Haven pay?** *Recommended:* knowledge and home: map reveals, shortcuts, lore, decoration plans,
   Insight, renown, spirit favours, a few fixed Pact Runes, one-time material caches. Never Essence, relics, people or raid plans
   (§7.7). *Alternative:* also small boons that carry into the next raid (a blessing from the Elder Cedar). That ties home to
   raids more strongly, but it blurs §7.7.
2. **Should the outer Haven keep the Misty Woods raid's forest?** *Recommended:* no for the inner woods (170-400 m: six woods of
   the Haven's own, lighter), yes for the deep belt past 400 m, as the border where home ends. *Alternative:* one forest, with
   region identity from dressing and places only (proposal 8 shrinks to S-M).
3. **Gates:** *Recommended:* soft gates on shortcuts and extras only, one per region at most, five to start (proposal 5).
   *Alternative:* whole regions closed until a portal tier, as Enshrouded does with its flame level. That gives stronger
   progression but less freedom, and it comes close to breaking "home must not punish going out".
4. **Travel to lit stones?** *Recommended:* yes, home to a lit stone and back, by day, not during a push or a watch.
   *Alternative:* no travel; walking and the horse only.
5. **The first stone walk after raid 1** (proposal 3), with pushes still after raid 3? *Recommended:* yes, optional and held to a
   homecoming. *Alternative:* keep the whole territory line at raid 3 and rely on proposals 1 and 2 to pull players out earlier.

The comparisons with Valheim, Enshrouded and Grounded come from how those games play, not from new research. None of the
proposals depends on them.
