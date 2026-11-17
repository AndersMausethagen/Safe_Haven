# Troll Valley: its own world (plan, 2026-11-16)

The user: "create the world generator for the troll valley raid. you decide the creative direction, how its build,
whats in the biome and what creatures. but the name should give you a hint". Not built yet; this is the plan.

## Progress

- **Step 0, done (2026-11-17):** `RaidLayoutGoldenTests` pins the Misty Woods' plans and heights for seeds 1-5 and a large map.
- **Step 1, done:** `Raid_TrollValley.unity` with its own map, terrain, mist, climate, day-night, birds and navmesh; the Woods'
  quests and story stay in the Woods; a Berg-jutul walks the apex round (`RaidSceneRigTests`).
- **Step 3, built (not yet seen in a launched raid):** the Valley shape (ARCHITECTURE › Generated raid maps), Troll Valley's rules
  set to it with five valley moods (Birch Floor, Scree and Stone, The Troll's Road, River Mist, Gorge Narrows), 6-8 stops and about
  two thirds of the Woods' sites; `RaidValleyTests`. Bridges are off (every crossing a ford) until the bridge model is in (step 4).
- **Art in progress:** birches (`Art/Vegetation/Birch`) for the look pass; LocalSpawn models for the Brukam, his bridge, club, hoard
  and bones, a seter ruin and a striding troll stone (props/creatures manifests).

## Creative direction

A long glacial **U-valley** of old Norway. A meandering river with gravel bars, cotton grass and wet meadow runs along the
floor; steep **scree** and stepped slopes climb to cliff rims on both sides (the floor and lower slopes are the play
space, the rims are walls); side gullies drop waterfalls into it. Subalpine **birch** groves and scattered **pine**,
juniper, heather, lichen-covered rock. Cold slate and grey-green with rust heather and pale birch, low mist over the
river, overcast or a low sun.

Folklore at its core: **trolls turn to stone at dawn**, so the valley's boulders and crags are petrified trolls (some
with faces and arms, and some wake). **Old stone bridges** cross the river as chokepoints, each guarded by a troll
("the Troll's Road"), building up to the Brukam boss bridge (GDD §10.7). Sites: troll lairs (overhangs with bone
piles and hoards), ruined summer farms (seter) the trolls wrecked, a **troll ting** stone circle, waystones.

Creatures: StoneTroll and SmallMossTroll common; TrollRunt and Tusse thieves; Trollkjerring at lairs; BogTroll in the
river marsh; Bergjutul rare from tier III; few wolves; bridge guards as set-piece guards. Resources: stone, iron in the
scree, birch timber, troll hoards.

## How it is built (from the code survey)

1. **Its own scene, `Raid_TrollValley`.** The map definition, terrain (and its plant types), navmesh, look volumes,
   climate, birds and audio all live in the scene, and the Misty Woods scene carries Woods quests (Echo Zone, Misty
   Hollow, beacons) and the Old Moss Troll apex. A per-raid override would leave those in the valley. A one-off
   editor utility copies `Raid_MistyWoods` and its map/terrain/volume/climate assets under `TrollValley *` names,
   retargets the copy, bakes a navmesh, drops the Woods-only roots, adds the scene to Build Settings, and points
   `TrollValley.asset` at it (+ SaveSlotInfo, SceneTransitionSettings tips). Builders stay retired: before/after report.
2. **A `Valley` layout shape** (`RaidLayoutShape.Valley`, a `RaidValleyRule` on the rules): a meandering centreline on
   one of the map's axes, `WallRise` (benched walls, tilt along the valley, gullies cutting in), `Walkable`, and the
   river along the centre from edge to edge. It plugs into the planner (rooms: start at the mouth, main exit at the
   head, stops alternating banks so the two banks are the two routes), the height/ground fields, zones (floor forest,
   gravel banks) and `PlaceRivers`. Plain C#, deterministic, own random streams, worker-thread safe.
3. **Crossings and bridges**: every trail-river crossing recorded; bridges (own stream, short route first) keep the
   river deep under them; `PlaceBridges` after the rivers (not `PlacePieces`, which snaps children to the terrain), with a
   set-piece site so guards come out. Gullies with waterfalls (the falls code pulled into a helper) and line mist.
4. **Data**: `TrollValley Map` (Meadow Grass / Gravel / Cliff Rock / Pine Needle layers, heather, lake grass, moss,
   rocks gathered at the wall feet, a "Troll Valley Stones" boulder kit with Sleeping Troll Rock), `TrollValley Layout`
   (moods Birch Floor, Scree and Stone, The Troll's Road, River Mist, Gorge Narrows; quotas ~40% under the Woods'),
   set pieces (Troll Bridge new, Petrified Trolls, Ruined Seter, Troll Ting, Troll Lair), troll spawn lists, a cool
   grade, blue-grey low fog, more overcast and drizzle, ravens, river and wind ambience.
5. **Cheap hooks**: bridge guards; "The Sleepers" event (troll rocks that wake); a Dawn time of day with fewer stone
   trolls (no petrification system exists yet).

Art the project lacks: birch, juniper, cotton grass, a stone bridge (modelled in Blender by script, deck in segments for
the Brukam bridge later). Stand-ins until then: the cedars and pines that exist.

## Tests and order of work

0. Baselines first: Misty Woods golden plan hashes for seeds 1-5, a generator report and timings.
1. Split the scene (same rules, same plan); fix RaidLookTests (all maps), BirdTests, PresentationPolishTests,
   PlaytestFixesTests, BossTests (scenes differ); new RaidSceneRigTests (same systems, no shared assets, no Woods roots).
2. Look pass (day, dusk, night, dawn screenshots). 3. Valley shape and river + RaidValleyTests (walkable rooms and
   sites, river edge to edge and downhill, walls high on both sides, same seed same map; golden test stays green).
4. Crossings and the bridge. 5. Gullies, falls, mist. 6. Set pieces, spawns, events, Dawn, apex (Bergjutul until
   Brukam). 7. Final art through ModelBudgetTests. 8. Performance pass (taller terrain shadows at a low sun, meadow
   grass, birch cards).

Risks: walls on the camera side hiding the player (keep walls 18-24 m, the walkable band clear of them); gully ramps at
or under 28 deg for the exits check; half the map walkable, so shorter quotas and route lengths checked against the
12-minute clock.
