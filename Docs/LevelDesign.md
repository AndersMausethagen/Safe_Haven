# Level design tools

Tools for building the home world (and any hand-made scene) in the Scene view. All of them are under
**Safe Haven › Level** and **Safe Haven › World**, all are undoable, and none add runtime code: what they place are
ordinary prefab instances and terrain paint. Raid maps have their own painted workflow (the Map Painter on "Raid Map";
see ARCHITECTURE "Raid maps").

## World Builder (rivers, paths, cliffs, forests)

Draw the world with splines and let the World Builder shape, paint and fill the terrain. Everything it does is
**non-destructive**: move a knot and the river digs a new bed while the old one fills back in, delete a path and the
grass grows back, change a setting and it rebuilds. Hand sculpting and painting with Unity's terrain tools keep working
alongside it.

### Making a feature

**GameObject › Safe Haven › World** (or right-click in the Hierarchy):

| Menu | Makes |
|---|---|
| **River** / **River (Haven)** | A river with a gravel bed and muddy (or, in the Haven, earthy) banks, stones in the water and ferns on the banks |
| **Path (Forest)** / **Path (Haven)** | A trodden path (mud in the forest, a grassy track in the Haven) |
| **Cliff** | A cliff or crag of scanned rock faces with rubble at its foot |
| **Dark Forest Area** | Dense, clumped spruce with dead trees, stumps, logs, ferns and mossy boulders (the Gord-like raid forest) |
| **Haven Grove Area** | Open oak groves, a few pines, mossy boulders and ferns by the water (the light, green Haven) |

The new feature goes under a **World** object in the scene and starts in **Draw** mode: click points on the ground,
**Backspace** removes the last, **Enter** or **Esc** finishes. Areas close their loop when you finish.

- **Edit Knots** (Inspector) switches to Unity's spline tools: drag knots, add one by clicking a segment, and drag
  tangents for sharper or softer bends.
- The **white dots** beside each knot of a river or path set the width there; the dots above each knot of a cliff set
  its height there. Values stay with their knots when knots are added or removed.
- **Reverse** swaps the direction (a river runs from the first knot to the last; a cliff's high side swaps).
- **Close Loop** (cliffs) makes an outcrop (high inside) or, with the high side on the right, a pit.
- **Build Now** builds immediately; normally it builds by itself a moment after you let go of the mouse.
- **Freeze Objects** keeps the generated rocks, water and plants as they are, so you can move single pieces by hand;
  the terrain still follows the spline. Turn it off to generate them again.
- **Seed** gives another version of the same shape (other rocks, other frayed edges).
- **Order**: features build lowest first (cliffs 0, paths 10, rivers 20, areas 30), so a river cuts through a path
  (a ford) and over a cliff (a waterfall), and a forest grows around both.

### River

- The water lies a little below the ground along the line (**Surface Below Ground**) and **never runs uphill**: where
  the ground rises, the river cuts through it; where it drops steeply, the water falls. Drops of at least
  **Waterfall Drop** get foam, mist and spray at their foot (the Waterfall Plunge). **Spline Height** uses the knots'
  heights instead (lift and lower them).
- **Width**, **Depth** and **Bank Width** shape the bed: a rounded channel with banks sloping up to the ground around.
- The water mesh follows the bends and its ripples and foam flow along them (Water River (World): Flow Follows the
  Mesh). Narrow stretches run faster. `WaterSurface` knows the river's real outline and surface height, so wading,
  splashes, footsteps and the river sound work along the whole river.
- **Bed Layer** / **Bank Layer** / **Bank Paint** paint the bed and banks; grass and terrain trees in the water and on
  the banks' edge are cleared. **Stones** go in the water (they make foam where they break the surface), **Bank
  Scatter** along the banks.

### Path

**Width** (the fully painted middle) and **Soft Edge** (a frayed fade). **Flatten** evens out the ground across the path
(following the ground smoothed along it), **Sink** presses it in like a trodden track. **Layer** and **Paint
Strength** are its ground; grass and trees on it are cleared. **Edge Scatter** lines it with stones or plants.

### Cliff

Draw where the face should stand; the **high side** is on the left of the drawing direction (inside, for a loop).

- **Raise** lifts the high side by **Height** (a ledge or plateau, out to **Plateau** metres, then blending back);
  **Cut** digs the low side down (a ravine or quarry wall); **Rocks Only** leaves the terrain and only stands rocks.
  A wide plateau makes a terrace; a narrow one a ridge (a river cuts through a ridge but falls over a terrace).
- **Face Depth** is the horizontal depth of the face (smaller is steeper); **Raggedness** breaks its line.
- **Kit** (a Cliff Kit, default *Rock Faces*: scanned rock faces and a crag) stands pieces along the face. Each is
  sized to the face's height (stacked in rows on tall faces), turned with its finished side out and stood upright
  (scans often lean), its top set on the rim and the bulk of its face on the cliff face; the rest is buried. **Inset**
  moves the face back into the slope (0 covers the whole face), **Overlap** closes the gaps, **Size Variation** varies them.
- **Rubble** (default *Cliff Rubble*) gathers at the foot; **Rock Layer** (default *Gravel*) paints the scree.
- A Cliff Kit (Create › Safe Haven › World › Cliff Kit) lists pieces with a weight. **Measure Pieces** works out each
  piece's facing, lean and size from its mesh; correct **Face Yaw** / **Face Tilt** by hand if a piece faces wrong.

### Areas (forests, groves)

A closed loop filled by **layers**, each a palette at a **Density** (per 100 m²), placed only where its rules allow:
**Slope**, **Height**, **On Ground** (terrain layers and how much of them), **Away From Water / Paths / Cliffs**
(negative water distance: only *near* water, for reeds and ferns), with **Clumping** into groves and clearings of
**Clump Size**, thinning out over **Edge Fade**. The palette's spacing keeps objects apart, and **Room Around Objects**
keeps them off anything already standing there with a collider (buildings, nodes, rocks). The same seed always gives the
same forest; it regrows when a river, path or cliff changes under it. Saveable objects (resource nodes) get save ids.

### The terrain

The first feature over a terrain adds a **World Terrain** component (and a stash asset next to the TerrainData that
keeps the ground under the features). Its Inspector:

- **Build Automatically** (on) / **Build Now** / **Rebuild Everything**.
- **Keep Objects On Ground**: props, nodes and buildings standing on the ground move up or down with it when a feature
  reshapes the ground under them.
- **Rebake NavMesh After Build** (off: slow) or **Rebake NavMesh** when you are done.
- **Dark Forest Look** / **Haven Look**: the world terrain material and the matching paint rules.
- **Auto-Paint with Rules** paints the whole terrain by its shape from **Paint Rules** (*Paint Rules - Dark Forest*:
  pine-needle floor, moss patches, mud in hollows; *Paint Rules - Haven Meadow*: meadow grass, lush patches, trodden
  earth in hollows). Each rule paints a layer by steepness, height, hollow or ridge, broken into patches; later rules
  paint over earlier ones. Features paint over the result; hand painting afterwards is kept.
- A feature that needs more room (a deep bed, a tall cliff) moves and stretches the terrain to fit, without moving the
  ground.

How it works: each build starts from the ground as it was before any feature (kept per cell in the stash, with hand
edits since the last build added in), applies the features in order, puts the ground back where no feature reaches any
more, then regenerates the features' objects. A build joins the undo step of the edit that caused it: one **Ctrl+Z**
takes back both. Opening a scene never rebuilds or dirties it.

### Terrain shader (rock on steep ground)

*Safe Haven/Terrain (World)* is URP's Terrain Lit plus **rock on steep ground**: past **Rock Starts At** degrees the
ground turns to a rock texture projected from three sides (no stretching on cliffs or steep banks), fully rock at
**Fully Rock At**, with a **Ragged Edge**. Rock Colour / Normal / Mask (default: Cliff Rock, mossy lichen rock), Size,
Tint and Normal Strength are on the material (*World Terrain (Dark Forest)* and *(Haven)* in Art/Terrain/World). It is
generated from URP's shader by `AgentScripts/GenerateWorldTerrainShader.py`: run it again after a URP upgrade. It works
best with up to 4 ground layers per terrain (above 4 URP draws the terrain twice and the rock blends less).

### Assets

All CC0 (free for any use, no credit needed; see `Models/PolyHaven/LICENSE.md`), made game-ready by the scripts in
`AgentScripts/PolyHaven` (download, Blender conversion with LODs and colliders, terrain layer maps):

- **Prefabs/Environment/World**: Rock Face A and B, Mountainside Crag, Lichen Boulder, Grey Boulder, Field Stone,
  Mossy Boulder 1-6, Mossy Rock 1-7, Old Stump A and B, Rotting Log, Dead Branch, Root Tangle, Fern 1-4. Rocks use
  Layered Lit with moss on their tops; ferns use Tree Wind (they sway).
- **Trees**: Norway Spruce A-C and Scots Pine A-C, realistic Nordic conifers baked from Poly Haven scans by
  `AgentScripts/PolyHaven/bake_conifer.py` (Blender): the scans' millions of needle cards become a few hundred
  elongated branch-spray cards (pointing out from the trunk and drooping) showing a 2:1 needle atlas rendered from the
  tree itself, tinted per species. The bark is kept as a decimated mesh (Layered Lit, moss layer present but off: the
  generic moss painted the trunks green); the trunk has its own triangle budget so it survives decimation. Three LODs (the farther, the fewer and bigger the cards, and only the trunk and main limbs), branches
  sway in the wind (Tree Wind, wind data in the second and third UV sets; the trunk stands still) and a capsule round the
  trunk. The dark forest's *Dark Forest Trees* palette uses them; the Haven's *Haven Trees* mixes a few in with its oaks.
- **Ground layers** (Art/Terrain/World): Pine Needle Floor, Mossy Forest Floor, Muddy Bank, Gravel and Cliff Rock for
  the dark raid forests; Meadow Grass, Lush Grass, Trodden Path and Damp Earth for the Haven.
- **Palettes** (Data/Level/Palettes): River Stones, Riverbank, Cliff Rubble, Dark Forest Trees, Forest Floor Debris,
  Ferns, Haven Trees, Mossy Boulders. They work with the Scatter Brush too.
- **Data/World**: the Rock Faces cliff kit and the two paint rule sets.

Safe Haven › World › **Set Up World Assets** remakes anything missing (existing assets are kept).

A demo: **Scenes/Demo/WorldBuilderDemo** (a river falling over a cliff, a ford, an outcrop and a dark forest).
`AgentScripts/WorldDemo.cs` rebuilds it.

## Scatter Brush

**Safe Haven › Level › Scatter Brush.** Pick a palette, press **Paint**, then click or drag over the ground.

| Input | Does |
|---|---|
| Click / drag | Paints objects from the palette inside the circle |
| Shift + click / drag | Erases this palette's objects inside the circle |
| `[` / `]` | Smaller / larger brush |
| Esc | Stops painting |

- A **palette** (`ScatterPalette`, Create › Safe Haven › Level › Scatter Palette) is a list of prefabs with a weight,
  a random scale range, how much each tilts with the slope (0 upright, 1 fully), how deep it sinks into the ground and
  whether it turns randomly. Its **spacing** is the smallest distance between two painted objects, so painting over
  the same spot again only fills the gaps.
- Defaults in `Data/Level/Palettes` (made by Safe Haven › Level › Create Default Scatter Palettes, kept if they exist):
  **Forest Trees** (spruces, pine, the odd dead spruce, wind oak), **Rocks and Logs**, **Wood and Stone Nodes**
  (oak, grey stone, hardwood, granite) and **Forage** (berries, healing herb, flax, nettle).
- Objects go under **Level Scatter › &lt;palette name&gt;** in the scene, so one palette's objects can be hidden,
  selected or deleted together. Save ids are given to painted saveable objects (resource nodes) right after a stroke.
- Objects are set down by the bottom of their meshes, not their pivot.
- **One prefab:** Content Editor › Prefabs › a prefab › **Paint with Scatter Brush** opens the brush with just that
  prefab (spacing from its size; the palette is not saved).

## Path Painter

For paths you may want to move later, use a World Builder **Path** instead: this one paints once.

**Safe Haven › Level › Path Painter.** Press **Draw**, click points along the path in the Scene view (drag a point to
move it, Backspace removes the last, Esc stops drawing), then **Paint Path**.

- The path follows a smooth curve through the points. **Width** is the fully painted middle; **Soft edge** is how far
  it fades out, frayed with noise so it does not look ruled.
- **Ground layer** is any of the terrain's layers (a trodden path, gravel, a mossy strip).
- **Clear grass and plants** removes terrain details along the middle; **Smooth the ground** evens out bumps under it.
- The grass ground colour is rebaked afterwards, so grass beside the path takes its colour.

## Drop to Ground

**Safe Haven › Level › Drop Selection to Ground** (End) and **Drop and Align to Slope** (Shift+End) set the selected
objects on whatever is below them (colliders first, then the terrain) by the bottom of their meshes. The Content
Editor's **Place in Scene** does the same.

## Level Check

**Safe Haven › Level › Level Check** (or `unity command sh_level`, `-- --fix true` to apply every fix) lists what the
open scene contains (resource nodes per kind, interactables, monsters, spawn groups, saveable objects, lights, water,
scattered objects per palette) and what looks wrong:

| Problem | Means | Fix |
|---|---|---|
| Floating / Buried | A resource node or interactable more than 0.25 m above, or 0.6 m into, the ground | Drop to ground |
| Unreachable | More than 2 m from the NavMesh: residents and monsters cannot walk to it | Move it, or rebake the NavMesh |
| Under water | A gameplay object below a water surface (water sources excepted) | Move it |
| Crowded | Two resource nodes less than 1 m apart | Move or delete one |
| Off the terrain | Outside every terrain | Move it |
| Save id | Missing or shared save id: its state would not save | Assign ids |
| Missing script | A component whose script was deleted | Remove |
| Leftover | A terrain without data, or an empty Cube/Sphere/GameObject at the world origin | Delete |

Select frames the object; **Fix All Fixable** applies every fix as one undo step. Save the scene to keep the fixes.
Run it before committing a level change.
