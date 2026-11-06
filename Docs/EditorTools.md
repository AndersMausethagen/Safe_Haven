# Editor tools: where everything is

Safe Haven's tools for adding content and building the world, by what you want to do. Details are in the linked docs
and in each tool's tooltips.

## The menu

| Safe Haven › | For |
|---|---|
| **World Tools** | Building the world: placing prefabs, the World Builder, ground, water, plants, sky, making a scene playable. Also the **Safe Haven World** panel in the Scene view. |
| **Content Editor** | Making and changing content: items, weapons, resources, monsters, buildings, recipes, raids, residents, quests. **Needs Attention** lists every problem it finds. |
| **Systems Editor** | Balancing how systems behave (survival, carrying, gathering, combat, raids, crafting, gear score...) with the numbers they produce. |
| **Sound Editor** | Music per scene and every sound effect, with previews and where each is used. |
| **Quest Map** · **Story Graph** | Every quest and its links on one canvas; a scene's story as nodes ([story graphs](StoryGraphs.md)). |
| **Playtest Runs** | Real playtest runs side by side against the progression targets. |
| **Content ›** | Shortcuts into the Content Editor (Items, Monsters, Buildings, Resources, Prefab Browser), plus Held Item Grips, Raid Layout Preview and Import Planner Data. |
| **Scene ›** | Level Check, Make Playable as Safe Haven / Raid, Tidy Hierarchy, Assign Missing Save Ids, Play Haven / Prologue with a test save (your real save is never touched). |
| **World ›** | Everything World Tools runs, as menu items (and the End / Shift+End / Ctrl+End drop shortcuts). |
| **Setup ›** | Run-once and rebuild steps: art pipeline, character and combat builds, UI, audio libraries, catalogues, scene template. You rarely need these; they are kept for rebuilding generated assets. |

**GameObject › Safe Haven** (or right-click in the Hierarchy) makes scene objects: World Builder features (River, Path,
Cliff, Ravine, Forest Area, Biome Area, Ground Paint, in the look of the terrain in view), quest objects (Quest Giver,
Inspect Point, Zone, Waypoint, World Event...), Fog Area, Mist Volume and Story Script.

## World Tools, tab by tab

| Tab | What is there |
|---|---|
| **Place** | Every level prefab as a picture: click one and click in the Scene view (R turns, Shift+R back), or drag it in. Stick to Ground, Align to Slope, random turn and size. Scatter Brush; Drop, Drop + Align, Physics Drop. |
| **Build** | The World Builder by look (**Haven · Forest Lake** or **Dark Forest · Mossy Forest**): draw a River, Path, Cliff, Ravine, Grove/Forest or Ground Paint (click points, Enter builds; cliffs and ravines show a preview first). The look's **Biomes**: Draw Area (a loop the World Builder keeps painted) or Brush (paint by hand). Build Now, Rebuild Everything, Build as I edit, hand paint handed back. Every feature in the scene, to select or switch off. |
| **Ground** | Sculpt & Paint, Terrain Toolbox, Gentle Relief; give the terrain a look, Auto-Paint, rock on steep ground, moss; the look's grounds to paint with; relief (all grounds, per ground, on meshes); NavMesh. |
| **Water & Plants** | Pond, Straight River, Waterfall; with something selected: make it still or flowing water, carve a bed, make it ripple water or bend grass. Add a look's plants to the terrain, fill it with grass, clear grass under water. |
| **Sky** | The Sky Controller (time of day, weather, lightning, previewed in edit mode); fog presets and Fog Areas; the scene's atmosphere volumes and terrain mist. **Scene setup** (folded) has the once-per-scene steps and says what is missing. |
| **Scene** | Make the scene playable as a home or a raid, what is missing (with fixes), Level Check, save ids, tidy the hierarchy, play with a test save. |

## How do I...

| Do this | With |
|---|---|
| Put down a rock, tree or building | World Tools › Place: click it, click in the Scene view |
| Fill a hillside with trees and rocks | Build › Forest (a loop), or Place › Scatter Brush |
| Make a meadow, forest floor or riverbank | Build › Biomes › Draw Area (stays editable) or Brush (by hand) |
| Add a river, path, cliff or ravine | Build › River / Path / Cliff / Ravine, click along it, Enter |
| Paint a ground by hand | Ground › Paint ground: click a ground, then paint (your paint wins over the World Builder's) |
| Set something on the ground | Select it, End (Shift+End tilts it with the slope, Ctrl+End lets it fall with physics) |
| Make an item, monster or building | Content Editor (or Safe Haven › Content › ...) |
| Tune hunger, carrying, combat, raids | Systems Editor |
| Add a quest or story beat | Content Editor › Quests, Quest Map, Story Graph; GameObject › Safe Haven › Quest ... objects |
| Check a level before playing | World Tools › Scene, or Safe Haven › Scene › Level Check |
| Play without touching my save | World Tools › Scene › Play the Haven / Play the Prologue |

More: [level design](LevelDesign.md), [atmosphere](Atmosphere.md), [weather](Weather.md), [water](Water.md),
[grass](Grass.md), [story graphs](StoryGraphs.md).
