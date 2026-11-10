# The Haven world: how it is laid out, and why

The Haven's world is generated from a recipe (`Data/World/Safe Haven/Haven World Recipe.asset`) and then refined by
hand. This page is the thinking behind the recipe: what makes a world read and stay interesting from a camera that
looks down on it. Tools and steps are in [level design](LevelDesign.md) › The Haven world.

## What the research said

Read before the 2026-11-10 rework: Nintendo's account of Breath of the Wild's world (via Game Maker's Toolkit), level
designers on elevation, and what top-down and isometric games do (V Rising, Diablo, Don't Starve, Factorio's cliffs).

1. **From above, height must be a line.** A slope is invisible to a camera 20-40 m up; a cliff is an edge with a lit
   top and a dark foot. Top-down games build flat benches with cliffs between them and explicit ramps, not hills.
2. **Rooms and corridors.** Open ground (a glade, a shore, a meadow) is a room; a path, a valley or a ford is a
   corridor. Cliffs and water are the walls. A fight needs a room; a ramp or a ford is a choke point.
3. **Many small draws, not a few big ones.** Nintendo dropped "towers with a route between them" because players
   walked the route like a rope. Many varied places, each visible from a little way off and each worth the walk,
   let a walk chain: reaching one shows two more.
4. **Every place pays.** A landmark with nothing at it teaches the player to stop looking. Resources sit where they
   belong (stone at the quarry, cloudberries in the bog), so the world tells you where to go for what.
5. **Dense spots, quiet stretches, never nothing.** Density varies on purpose, but a walk never goes long with
   nothing on it, and no path ends at nothing.
6. **Each region is its own.** Ground, trees, water, mist and what lives there differ by region, so the player knows
   where they are without the map.
7. **Read first, pretty second.** Where a tree crown hides a cliff or a path, the crown goes.

## How the world applies it

| Rule | In the world | Recipe (Haven World Recipe) |
|---|---|---|
| Height as a line | Outside the Haven the land is **terraces**: flat benches 7 m apart with a cliff between each and the next, climbing to a terraced rim. The lake lies one bench under the Haven, the mountain is a stack of them. | Terraces › Terrace Step, Rise, Wander, Scale |
| | Cliffs wander into bays, headlands and table rocks instead of rings. | Terrace Wander / Scale |
| | No tree stands within 4.5 m of a cliff, so its line shows through the woods. | (generator) |
| Rooms and corridors | **Roads run in valleys**: the land dips one bench along each road out from the Haven, so its walls are cliffs and the benches above are rooms a trail climbs to. | Road Valley, Road Valley Width |
| | **A ramp** wherever a path crosses a cliff, and **scrambles** (a cliff crumbled into a slope, painted as loose stone, kept clear of trees) every 90-150 m along every cliff, so no bench is cut off. The planner reports the share of ground that can be walked to. | Ramp Length, Scramble Spacing |
| | 30-36 **glades**, each on one bench, each joined to the paths by a trail. | Glades |
| Many small draws | 27 **places** of 15 kinds, a glade or place about every 130 m, plus a scene beside the path wherever a walk has passed nothing for 45-70 m. | What Is Found There, Along the Way |
| | Trails **branch**: 8 in 10 glades have a second way out to another glade, and most places have a trail on to the nearest glade, so a walk there carries on somewhere. | Glade Links, Place Links |
| Every place pays | Places list their **rewards**: resource points round the clearing (forage at a camp, stone at the quarry, bog plants at the Drowned Stones). | a place's What It Gives |
| Never nothing | **No road ends at nothing**: one ends at the lake's landing, one in the bog's heart, the rest at Wayfarer's Stones. The planner measures the longest walk with nothing on it and marks any over 90 m, and any path end that leads nowhere. | At Road's End; Longest Dull Walk |
| | The world is square: places, glades, creatures and resources reach into its **corners** (a fifth of the map, empty before). | (plan) |
| Regions | The Haven's **meadows** (groves, ponds, game, the Mother Maple, folds); the **Misty Woods** on their terraces, always in mist; the **lake** with its islands and landing; the **mountain** (ore, rock, the highest ground); the **bog**, a sunken basin of pools and hummocks with a causeway to its heart, its own plants and creatures; a **tarn** in the northern hills and a **mere** in the south-west woods, each with islands. | Rings, Lake, Mountain, Meres and Bog, Mist |
| Water that makes sense | The lake is the low point: two rivers of different widths and five or six streams (out of the meres, the bog and springs) run to it, **falling** wherever they come down to a lower bench, fording at every path, and fanning into the lake over a gravel fan. | Water and Rock |
| Life | **Game** (deer, stag, fox, wild boar: neutral and timid, to hunt and later tame) in the meadows and the open glades, back by day; what hunts you in lairs and the deep woods, back only by night and never near the ward. | creature bands (Peaceful) |

## Numbers worth knowing

Measured by the planner on the shipped recipe (seeds 1-7): 27 places every time, 30-35 glades, about 20 falls, 80-100
scrambles, 99.8% of dry ground reachable on foot, longest walk with nothing on it 52-72 m, no dead ends, under 0.1%
empty ground. About 20 km of cliff in all, of which 3 km (those within 30 m of a path, glade or place) have rock
pieces stood along them: rock pieces are the heaviest thing in the world at 2.5 scene objects a metre, and the rest
are the terrain's own rock. The whole scene holds about as many objects as the old, emptier one (35,000).

## When you change it

- Tune in **Safe Haven › World › Haven World Planner** first: it replans in about a second and shows the problems
  (red hatching: ground nothing climbs to; red lines: dull walks; red crosses: dead ends; pink dots: empty ground).
- More cliffs is not more interest. If the map looks busy, lower Terrace Wander or raise Terrace Scale before
  touching the step.
- A new kind of place needs a prefab and one line in `AgentScripts/BuildHavenWorldRecipe.cs`. Give it a reward.
- Regenerating replaces the terrain and everything the generator made; the portal, buildings, residents, quest and
  story objects stay. Hand edits to the generated world are lost: make lasting changes in the recipe or the builders.
