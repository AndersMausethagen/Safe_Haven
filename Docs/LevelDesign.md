# Level design tools

All the editor tools at a glance, by what you want to do: [Editor tools](EditorTools.md).

Tools for building the home world, raids and any hand-made scene in the Scene view. They are all in one window,
**World Tools** (Safe Haven › World Tools, or the Scene view's **Safe Haven World** panel), and in the menus
**Safe Haven › World** (place, terrain, water, grass and plants, sky and weather) and **Safe Haven › Scene** (make
playable, Level Check). All are undoable, and none add runtime code: what they place are ordinary prefab instances and
terrain paint. Raid maps have their own painted workflow (the Map Painter on "Raid Map"; see ARCHITECTURE "Raid maps").

## World Tools

| Tab | What is there |
|---|---|
| **Place** | Every level prefab as a picture, in groups (Rocks & Cliffs, Trees, Forest Floor, Plants & Grass, Resources, Buildings, Raid, Creatures, Other) and by set (**Forest Lake**, **Mossy Forest**, **Original**; see "The two looks" below), with a search box. Click one, then click in the Scene view: a preview follows the mouse and sits on the ground (or on a rock); each click places one, with a new random turn and size. **R** turns the next one (Shift+R back), **[ ]** size, **Esc** or right-click stops. Or drag a picture into the Scene view. Placed objects go under "Level Props" (in a raid map, under its Map Content, so they turn with the map). Also the Scatter Brush and Drop to Ground |
| **Terrain** | Sculpt & Paint (selects the terrain in view, whose Inspector has Unity's brushes), the Terrain Toolbox, Gentle Relief, the World Builder's rivers, paths, cliffs and forests, Build Now, the Dark Forest (Mossy Forest) or Haven (Forest Lake) look, Auto-Paint, moss, **Paint ground (tessellated)**, Rebake NavMesh |
| **Water & Plants** | Ponds, straight rivers, waterfalls, making a mesh water, carving a bed; grass and plants, and **Add Forest Lake / Mossy Forest Plants** (the look's grass, moss, ferns and flowers as terrain details for Paint Details) |
| **Sky & Weather** | The Sky Controller, the day/night and weather rig, shared wind, fog set-up, fog presets and fog areas |
| **Scene** | Make Playable (as Safe Haven or as a Raid) and what is missing |

The **Safe Haven World** panel in the Scene view (shown by default; the Scene view's overlay menu hides or moves it) has
Place…, Tools…, **Stick to ground**, Drop / Drop + Align, Sculpt, Build World and Check Scene, so the tools are at hand
while you work.

### Stick to ground

While **Stick to ground** is on (the panel or the Place tab), prefab objects dragged into the Scene view from the Project
window (or the Place tab), duplicated, or moved sideways with the Move tool settle on the ground under them: by the
bottom of their meshes, sunk by **Sink**, tilted with the slope if **Align to slope** is on. The settling is part of the
same undo step. Lifting or lowering an object by hand is left alone, as are cameras, lights, volumes, UI, terrains,
whole maps and what the World Builder and Scatter Brush place themselves. The settings are yours (kept per user, not in
the project).

### Make a scene playable

World Tools › Scene (or Safe Haven › Scene › Make Playable as Safe Haven / as Raid) turns any saved scene with ground
into one you can press Play in:

- It copies what every gameplay scene needs from the real scene of that kind (SafeHaven.unity or Raid_MistyWoods.unity),
  so it is always the current setup: the player rig (player, cameras, HUD and windows, game state, sun, sky, weather and
  fog, copied together so they stay connected, replacing the scene's own camera and sun), the NavMesh surface, save keys
  and scene audio. A home also gets its wind, persistence, the raid portal and a wake-up point (Player Arrival Point
  "HomePortal"); a raid gets its director (with a raid to test with), a Raid Layout with the Raid Generator (put spawn
  groups under it) and an extraction point 30 m ahead (move it where it belongs).
- It stands the player on the ground at the wake-up point or the middle of the Scene view, bakes the NavMesh and adds the
  scene to Build Settings. One undo step; save the scene to keep it.
- **What is missing** lists errors (it will not play), warnings (it will play badly) and notes, most with a button that
  fixes them: no player, camera, game state or HUD, nothing to stand on, an unbaked NavMesh or a player off it, not in
  Build Settings, two suns or audio listeners; for a raid no director, raid, generator, exit or spawn groups, or no raid
  that uses the scene (**Make a Raid for This Scene** copies one and points it here); for a home no persistence,
  wake-up point, portal or Portal. It also counts what Level Check finds.

A home scene saves into the save file like the real Haven does when you play it.

### Scene hierarchy (folders)

Every gameplay scene keeps its top level in numbered folders: **00 Systems** (game state, save keys, persistence, scene
audio, NavMesh, view bounds), **01 Player & Cameras**, **02 Lighting & Atmosphere** (sun, volumes, Sky and Weather,
Atmosphere, wind), **03 UI**, **04 World** (terrain, the World Builder's **World**, water, raid map, hand-placed trees,
overgrowth), **05 Gameplay** (portal, gathering, forage, residents, raids, spawns, test areas), **06 Story & Events**
(story scripts, moments, event reactions) and **07 Ambience** (fireflies, birds, canopy debris, insect sounds). Folders
are empty objects at the origin, so filing an object changes nothing in play. **Safe Haven › Scene › Tidy Hierarchy**
files loose objects (and re-files misplaced ones) by what they are; Make Playable does it too, and the World Builder,
Scatter Brush, Place tool, sky and atmosphere set-up put new top-level objects in the right folder and find theirs
inside one (`SceneFolders`). A Prologue-style NavMesh that bakes only its children holds the level, so it is filed under
World with its children. Scenes of five objects or fewer (main menu, results) are left as they are; GameplayTestScene
keeps its own numbered layout.

## Mob Spawn Zones and Random Resource Nodes

Both are in World Tools › Place (**Mob Spawn Zone** under Raid, **Random Resource Node** under Resources) and work in
any scene.

**Mob Spawn Zone** (`MobSpawnZone`): an area drawn on the ground where creatures appear. In the Scene view drag its
corners (they follow the ground), Shift-click near an edge to add a corner, Ctrl-click a corner to remove one; Drop
Corners to Ground in the Inspector settles them all. It spawns Count creatures at random spots inside it on walkable
ground, Separation apart:

- **What:** one entry in Creatures for a specific creature, several for a weighted random pick (Min/Max Per Group cap
  each), or a shared Spawn Table asset. Use the creature prefab, not its model (the Inspector warns).
- **When:** On Start; Player Near (within Wake Distance of the zone's edge); or Manual (only `Spawn()`, from a quest or
  an event). Respawn fills it again Respawn Seconds after its creatures are dead, never in front of the player.
- **Hunt Player** sends them straight at the player. In a raid they get the raid's strength.
- **On Cleared** (and the `Cleared` event) fires when everything it spawned is dead: what a "kill them all" step uses.
  In Play mode the Inspector has Spawn Now and Despawn.

**Random Resource Node** (`RandomResourceNode`): becomes a gathering node when the scene starts, picked from every node
of its Kind (Wood: axe, Rock: pickaxe, stone and ore; Plant: sickle; Forage: by hand; or Any) within its Tier range,
or from its Only list. Stable Pick makes a spot always become the same node (home); otherwise each visit rolls again
(raids). Its gizmo is coloured by kind. What it becomes is not saved.

**Look variants:** a gathering node can carry other looks (GatheringNodeDefinition › Model Variants, Variant Scale):
each placed node picks its own model or a variant (the same one every time for the same spot), fitted to the original's
size, at a random size and facing. Grey Stone has three (Field Stone, Grey Boulder, Lichen Boulder).

## World Builder (rivers, paths, cliffs, forests)

Draw the world with splines and let the World Builder shape, paint and fill the terrain. Everything it does is
**non-destructive**: move a knot and the river digs a new bed while the old one fills back in, delete a path and the
grass grows back, change a setting and it rebuilds. Hand sculpting and painting with Unity's terrain tools keep working
alongside it.

### Making a feature

**World Tools › Build** (pick the look in its tabs: Haven · Forest Lake or Dark Forest · Mossy Forest), or
**GameObject › Safe Haven › World** (or right-click in the Hierarchy), which uses the look of the terrain in view. Click
points on the ground; nothing is built until you press Enter (a cliff or ravine shows a see-through preview of its face,
height and depth meanwhile, following the Inspector):

| Menu | Makes |
|---|---|
| **River** | A river with a rocky bed and earthy banks, stones in the water and ferns on the banks: the Mossy Forest's (cedar rock and soil, mossy stones) or the Forest Lake's (lake rock and banks, lake stones and ferns) |
| **Path** | A trodden path (bare cedar soil in the forest, the Haven's path ground) |
| **Cliff** | A cliff or crag: the shaped terrain in tessellated Cliff Rock with rubble at its foot (set a Kit, e.g. the look's Crags, to stand rock pieces along it) |
| **Ravine** | A cut with steep walls on both sides, optional ledges, moss on the shaded wall and rubble on the floor |
| **Forest Area** | The Mossy Forest: dense, clumped cedars, young cedars, mossy logs and stumps, ferns and shrubs, mossy boulders, on the Misty Woods floor biome (the raids' forest). The Forest Lake: open groves of sugar maples and a few cedars, saplings, logs and stumps, lake boulders, ferns by the water (the light, green Haven) |
| **Biome Area** | A loop painted with a Biome preset: its ground, plants (terrain grass) and props, faded in from the edge |

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
- **Kit** (a Cliff Kit: *Mossy Forest Crags* or *Haven Lake Crags*; the older *Rock Faces* of scanned rock faces still
  works) stands pieces along the face. Each is
  sized to the face's height (stacked in rows on tall faces), turned with its finished side out and stood upright
  (scans often lean), its top set on the rim and the bulk of its face on the cliff face; the rest is buried. **Inset**
  moves the face back into the slope (0 covers the whole face), **Overlap** closes the gaps, **Size Variation** varies them.
- **Rubble** (default: the look's *Rubble*) gathers at the foot; **Rock Layer** (default: the look's rock ground) paints the scree.
- A Cliff Kit (Create › Safe Haven › World › Cliff Kit) lists pieces with a weight. **Measure Pieces** works out each
  piece's facing, lean and size from its mesh; correct **Face Yaw** / **Face Tilt** by hand if a piece faces wrong.

### Areas (forests, groves)

A closed loop filled by **layers**, each a palette at a **Density** (per 100 m²), placed only where its rules allow:
**Slope**, **Height**, **On Ground** (terrain layers and how much of them), **Away From Water / Paths / Cliffs**
(negative water distance: only *near* water, for reeds and ferns), with **Clumping** into groves and clearings of
**Clump Size**, thinning out over **Edge Fade**. The palette's spacing keeps objects apart, and **Room Around Objects**
keeps them off anything already standing there with a collider (buildings, nodes, rocks). The same seed always gives the
same forest; it regrows when a river, path or cliff changes under it. Saveable objects (resource nodes) get save ids.

### Ground Paint (a look for an area)

**GameObject › Safe Haven › World › Ground Paint › Haven Meadow / Haven Old-Growth / Raid Dark Forest** (or World Tools › Build › Ground Paint ▾): draw a closed
loop and the ground inside it is painted with that look's **Paint Rules** (the same rules the terrain's auto-paint
uses: steepness, height, hollows, patches), fading in over **Edge Fade** metres with a frayed edge, at **Strength**.
It builds before paths and rivers (order 5), so they paint over it, and a forest area grows on top of it. Point
**Rules** at any Paint Rules asset (Create › Safe Haven › World › Terrain Paint Rules) for your own look.

- *Haven Meadow* (*Haven Lake Paint - Haven Meadow*): Haven meadow, lush moss patches, the Haven path in hollows.
- *Old-Growth Forest* (*Haven Lake Paint - Old-Growth Forest*, round the Haven): old moss, banks and litter in patches,
  humus in hollows. Under Safe Haven's groves.
- *Dark Forest* (*Mossy Forest Paint - Dark Forest*, raids): cedar litter, cedar moss patches, cedar soil in hollows.

The menus use the looks' rule sets; the original sets (*Paint Rules - …* in Data/World, on the PolyHaven ground layers)
are still there for scenes that use them.

The forest floor layers come with every map: colour, OpenGL normal, a mask (occlusion, height, smoothness) and the
height (displacement), occlusion and roughness on their own. The terrain blends layers by height (Enable Height-based
Blend on the World Terrain material), so moss fills the soil's hollows rather than cross-fading.

### The terrain

The first feature over a terrain adds a **World Terrain** component (and a stash asset next to the TerrainData that
keeps the ground under the features). Its Inspector:

- **Build Automatically** (on) / **Build Now** / **Rebuild Everything**.
- **Keep Objects On Ground**: props, nodes and buildings standing on the ground move up or down with it when a feature
  reshapes the ground under them.
- **Rebake NavMesh After Build** (off: slow) or **Rebake NavMesh** when you are done.
- **Dark Forest Look** / **Haven Look**: the Mossy Forest or Forest Lake look. The terrain gets its own copy of the
  look's tessellated material (its rock on steep ground, anti-tiling, colour variation and displacement settings, via
  Enable or Refresh Displacement), the look's paint-rule grounds are added to its layers, and its **Paint Rules** are
  set. The TerrainData must be saved as an asset.
- **Auto-Paint with Rules** paints the whole terrain by its shape from **Paint Rules** (*Mossy Forest Paint - Dark
  Forest*: cedar litter, moss patches, soil in hollows; *Haven Lake Paint - Haven Meadow*: meadow, lush moss patches,
  path ground in hollows). Each rule paints a layer by steepness, height, hollow or ridge, broken into patches; later rules
  paint over earlier ones. Features paint over the result; hand painting afterwards is kept.
- A feature that needs more room (a deep bed, a tall cliff) moves and stretches the terrain to fit, without moving the
  ground.

How it works: each build starts from the ground as it was before any feature (kept per cell in the stash, with hand
edits since the last build added in), applies the features in order, puts the ground back where no feature reaches any
more, then regenerates the features' objects, keeping every object that came out the same (`GeneratedObjects`: a prefab instance with the same prefab and overrides, or a container of such): unchanged objects keep their file ids and save ids, so saving the scene after a build only writes what changed (a rebuild of the whole Haven kept 26,013 of 26,015 objects; the two rivers' water strips are always made again). A build joins the undo step of the edit that caused it: one **Ctrl+Z**
takes back both. Opening a scene never rebuilds or dirties it.

### Terrain shader (rock on steep ground)

*Safe Haven/Terrain (World)* is URP's Terrain Lit plus **rock on steep ground**: past **Rock Starts At** degrees the
ground turns to a rock texture projected from three sides (no stretching on cliffs or steep banks), fully rock at
**Fully Rock At**, with a **Ragged Edge**. Rock Colour / Normal / Mask (default: Cliff Rock, mossy lichen rock), Size,
Tint and Normal Strength are on the material (*World Terrain (Dark Forest)* and *(Haven)* in Art/Terrain/World). It is
generated from URP's shader by `AgentScripts/GenerateWorldTerrainShader.py`: run it again after a URP upgrade (give it
a folder to write a copy there instead, to compare with the shader in use). The generator also writes the layers'
**Hide Repetition** (each layer sampled a second time, turned and scaled, picked by slow noise), **Large Colour
Variation** (kept at or below white: brighter albedo made the lighting blow up into flashing black and white blocks
under bloom) and the skipping of layers with no weight at a pixel. `WorldTerrainDisplacement.hlsl` and
`WorldTerrainDomain.hlsl` are written by hand. It works
best with up to 4 ground layers per terrain (above 4 URP draws the terrain twice and the rock blends less).

The rock is **tessellated** too: **Rock Relief** pushes it out along the slope by the rock
mask's height (blue channel), projected from three sides like its colour, so cliffs and steep banks have real relief
where the ground's own displacement fades out (calibrated to span 8 cm, see below). **Rock Relief Midpoint** is the rock's average height, measured from the
mask by Enable or Refresh Displacement.

There are two rocks: **rocky ground** (the Rock textures: the look's Haven Rock or Cedar Rock) on steep slopes, and
**Cliff Rock** (the Cliff Rock textures) on the steepest drops. With **Rock From Painted Layers** on (Haven and the
Prologue), they are *paint*: wherever a ground marked **Rocky** or **Cliff** in the terrain's Surface Profile is painted,
that rock is drawn from three sides with its relief, and painting any other ground over it replaces it. World Tools ›
Terrain › **Rock on Steep Ground** (and the Look buttons) paints them by slope over what is there: rocky ground past the
material's **Rock Starts At** (Haven 42°), Cliff Rock past **Cliff Rock Starts At** (56°), with ragged edges. Paint
Haven Rock / Cliff Rock (Paint ground › Forest Lake / Original) to add rock by hand. Faces steeper than **Always Rock
Above** (50°) are rock whatever is painted: paint is laid from above, so past about 50° any ground stretches (by
1/cos of the slope), and a short wall only one height cell wide never reads steeper than about 60° to the shader (its
normals are averaged with the flat ground above and below), so a higher threshold missed exactly those walls. What is
painted near such a face still picks Haven Rock or Cliff Rock; with nothing rocky painted it is Cliff Rock. Without
painted rock (generated raid maps), both rocks follow the slope.

#### Where the relief comes from, and how big it is

Each ground's displacement is its height map: the Surface Profile's own height texture for a layer, or else the blue
channel of the layer's mask. Ground converted from the packs (`VegetationPackConverter.Ground`) gets the pack's real
height map in that blue channel, found by the colour texture's name (Mountain Lake keeps its height maps apart from its
terrain layers), read straight from the file (packs import height maps as sRGB colour, and that gamma curve flattens
hollows and sharpens peaks), uncompressed (block compression steps the height). The packs' mask height remap is HDRP's
height *amplitude and base* for blending between layers (HDRP terrain never displaces), not a range to stretch heights
to: reading it as one clipped 43% of Haven Rock flat at the top, which is what made it spiky.

Heights come normalised to 0-1 over whatever range each scan had, so one amplitude cannot suit them all. **Safe Haven ›
World › Terrain › Calibrate Ground Relief** (`GroundRelief`) sets, for every ground of every terrain profile, the
midpoint to the height map's own average (the ground stays level on average) and the amplitude so its 1st-99th
percentile spans a real height: **2 cm** for moss, grass and litter, **3 cm** for soil, paths and banks, **6 cm** for
stones and rock; and the steep rock's **Rock Relief** so it spans **8 cm**, all at **Relief ×1**. **World Tools ›
Terrain › Ground relief › Relief ×** is the live dial: it scales every ground and the steep rock together, keeping
their proportions, by setting the material's displacement strength (×1 = strength 0.1, `GroundRelief.ReferenceStrength`)
and rock relief; the line under it shows the resulting centimetres. All terrains start at ×3. It rebuilds every
terrain's displacement on a stand-in terrain, so no open scene changes. **Recalibrate Grounds** (or the menu item) after
adding a ground or changing a height map; it keeps each terrain's Relief ×.

#### True terrain displacement

The world shader now tessellates actual terrain triangles (not parallax). Select a terrain using this shader and run
**Safe Haven > World > Terrain > Enable or Refresh Displacement**, then save the scene. The command creates a dedicated
material and persistent height-mask array beside its TerrainData; the painted layers, holes and cliff settings are kept.
Up to 16 layers are supported. Each layer's mask-map **blue** channel, height remap, tile size and offset drive the
displacement, weighted by the original terrain paint textures. Missing height maps contribute a neutral height.
All layer passes use the same height resources, including layers 5-16, so their geometry stays aligned.

On the material, **Displacement Height Range** defaults to 0.10 metres (approximately +/-5 cm for a 0-1 height map).
**Full Detail Distance** / **Detail Fade Distance** default to 8 / 35 metres; **Maximum Tessellation** defaults to 8,
with a target triangle edge length of 8 cm, and no triangle smaller than **4 pixels** on screen (Target Edge Length in
pixels; 0 would allow 1-pixel triangles, which cost the GPU most of its frame, see "Static flags and heavy models").
Detail fades smoothly with distance and on steep cliffs.
Forward, deferred, shadow, depth, depth-normal, selection and distant base-map passes share the displaced surface.
The existing mesh-only *Forest Ground ASE* shader is not used for this: it does not preserve the terrain's painted layers.

Painting existing layers updates displacement immediately. Adding, removing or reordering layers (Unity's Paint
Texture tool, a layer palette, World Tools) rebuilds the displacement data by itself a moment later
(`TerrainLayerWatcher`, for terrains already set up); run the command again after changing tiling,
height remaps or source mask textures. A new layer takes its height settings from a terrain where it is already tuned
(the Haven's first). Refresh keeps the material's
displacement tuning. Height copies now use linear **RHalf** textures (16-bit floating point), at 512, 1024 or 2048 per layer;
source textures and TerrainData are not modified. Higher precision preserves available input detail; it cannot recover
detail already lost in a compressed/low-resolution source map.
Hardware tessellation (shader model 4.6, e.g. desktop D3D11) is required; this shader is not a mobile/WebGL fallback.
Displacement is visual geometry only: the TerrainCollider, NavMesh and placement sampling still use the original
heightfield. Keep the amplitude small for walkable ground. Adjacent terrain tiles need matching layers, UV phase and
displacement settings to avoid seams. Refreshing does not save unrelated scene changes.

#### Designer workflow: Terrain Surface

Open **Safe Haven > World > Terrain Surface**. This single window contains:

- The target terrain and its assigned material. **Set Up / Rebuild Height Resources** safely accepts the world shader,
  URP Terrain Lit, or an unassigned terrain; other custom shaders are rejected rather than silently losing features.
- Geometry height, distance falloff, subdivision cap (up to 64), target edge size, height mip smoothing and height-aware
  displacement blending. **Balanced** and **Maximum** density presets keep authored heights and layer settings.
- Height-derived normal strength, normal-map multiplier, mask AO strength and smoothness multiplier.
  Height normals are sampled from the same layered height field as displacement and composed with existing mapped
  normals in Forward, Deferred and DepthNormals passes. Low quality skips height-normal sampling.
- Forward-only diagnostic previews: lit, blended height and final world-space normals.
- A persistent **Terrain Surface Profile** beside TerrainData, containing height-array resolution and per-layer source
  override, channel, amplitude, midpoint and contrast. Setup discovers matching sibling `LayerName_Height.png` maps;
  otherwise it reads the original mask blue channel. Explicit overrides must have sRGB disabled.
- Per-layer albedo/normal/mask references, normal strength, UV size/offset and mask remaps, plus all cliff controls.
  Terrain Layer assets are shared: editing these affects every terrain using them. Profile height shaping is local to
  that profile and does not alter source maps.

Material lighting/geometry controls update immediately. Profile/source/tiling changes require **Rebuild**; zero height
is preserved on refresh. Existing paint updates the bound control maps. The profile only defines bake inputs; a player
does not rebake them. Save Surface Assets saves the relevant material, profile and terrain layers only. Saving the scene
is a separate explicit action, and includes any other work in that scene.

`TerrainSurface.SetQuality(DetailQuality)` is the runtime hook for the future graphics options menu:
**Off**, **Low**, **Balanced**, **Ultra**. Ultra uses the full authored density/range; Balanced scales density to 65% and
range to 75%; Low uses 35%/45% and skips height-derived normals. Off removes displacement and height-derived normals,
but is not a shader-model fallback (the hull/domain pipeline still requires tessellation hardware).

#### Recovering terrain footprints

The **Terrain Surface > Recovering Footprints** controls set the footprint lifetime (default **30 game-time seconds**),
depth (default **2.5 cm**), height-detail flattening, normal-map flattening, compressed-ground darkening,
grass flattening, boot width/length, and optional sole mask. Darkening can be set to zero; it makes tread readable among noisy textures.
An empty sole mask draws a procedural boot with heel, rounded toe and tread grooves; replace it with a linear
greyscale texture for another sole. White presses down; black does not. These controls update at runtime without
rebaking. Changing field resolution/area resets the local field on the next step.
SafeHaven uses realistic **0.13 x 0.30 m** bootprints, darkening **0.8**, and full grass/height/normal
flattening. Width and length remain adjustable independently of visibility and recovery.

Each profile layer has **Footprint Response**: use 1 for soft moss/mud, a smaller value for firm earth or gravel, and
0 for hard rock. **Rebuild Height Resources** after changing the response. Painted layer weights blend the response;
the cliff slope fade also suppresses impressions on steep rock. Setup assigns `TerrainFootprints` and its stamp shader.
`PlayerAudio` automatically adds `PlayerTerrainFootprints` in every scene: it listens to `PlayerAudio.FootPlanted`, sharing actual
foot-plant timing from animation events/bones, with alternating contacts for non-humanoid rigs. It raycasts the
contact, so floors/bridges block stamping the ground below, and water contacts are excluded.
Terrain setup disables **Draw Instanced**: the tessellation domain passes require non-instanced terrain geometry.
Keep this disabled after setup. Per-layer amplitude/height edits need **Rebuild Height Resources**; changing only
the profile does not update the baked height metadata. This applies to Prologue/MistyWoods as well as SafeHaven.
Prologue/MistyWoods uses a 64-subdivision cap and 0.02-metre target edges to resolve small boot tread.
Its moss response is 1, soil response 0.6 and stone response 0; footprints use full compression and 0.8 darkening,
with realistic 0.13 x 0.30-metre soles and 30-second recovery. Its dark storm lighting can conceal relief and
darkened tracks from the game camera; use a close view and the Tracks preview to distinguish lighting from missing stamps.

Footprints press down the real rendered geometry rather than adding a floating decal. The whole sole compresses
height detail toward the painted layers' minimum height, including detail below the terrain origin, without raising
valleys. Tread adds secondary depressions. Height-detail flattening also softens fine albedo detail inside the sole,
while normal-map flattening removes the uncompressed moss lighting; shallow grooves still compress the whole sole.
Preview **3 Tracks** displays the field on the actual terrain (Forward rendering) for checking contact
placement. Footprint lookup uses actual world positions, not reconstructed terrain UVs, so patch draw transforms
cannot offset the trail. All displacement geometry passes share the field, including shadows and depth. Recovery is gradual;
pausing freezes recovery. Repeated steps refresh the pressed area. The field is local to each terrain (default
24 m square, 2048 pixels); it recentres with new steps, preserving overlapping texels and discarding distant tracks.
It is a bounded visual effect, not a persistent trail system: leaving its area, disabling the component or leaving the
scene removes tracks. TerrainData, shared material/profile assets, colliders and NavMesh are never changed by steps.
Painted **Safe Haven/Grass** plants also bend down in the recovering field, revealing the surface trail rather than
immediately springing upright behind the player. Their roots descend toward the profile's lowest compressed layer
so flattened grass does not float above and hide the tread. Up to four nearby terrain fields feed the grass shader without
changing shared plant materials or detail instances; trees are unaffected. Off quality hides displacement and trail bending;
Low retains pressed geometry but omits expensive height-derived lighting normals.
Quality is set per terrain through a MaterialPropertyBlock, preserving shared material assets and unrelated properties.
The component rebinds paint textures when terrain painting replaces them, reapplies patch-bounds expansion on load,
and restores its bounds/quality override on disable. The future options menu must call it for each terrain; no player
settings UI or persistence is added here.

Maximum is a near-camera quality option, not a measured frame-rate guarantee. Use a short detailed range and profile
the target GPU before shipping. Larger height ranges do not solve flat lighting, and neither tessellation nor map
baking can create roots/rocks missing from the source height field. Overhangs require real meshes/vector displacement,
not this vertical terrain heightfield. Blender baking is only useful when a corresponding high-detail source exists;
the existing authored height maps do not require a Blender round trip.

### Tessellation on meshes

Use **Safe Haven > World > Mesh Surface** with a selected static mesh. Choose its material slot and
**Create Tessellated Copy and Assign** for a basic opaque URP Lit material. The original material is preserved.
The tool adds `MeshSurface` for expanded displacement culling bounds. Save the scene/prefab after assignment.
No existing scene objects are converted automatically.

For custom/Amplify materials, create a material with **Safe Haven/Mesh Surface (Tessellated)**, assign its
maps manually, then assign it to the mesh and use **Add Displacement Bounds Support** in the window.
The reusable **Mesh Surface Moss** material in `Assets/_Project/Art/Terrain/SafeHaven` provides a configured example.
This is a hand-written URP shader, not an editable Amplify graph; existing ASE graphs remain untouched.

All controls are in the Mesh Surface window or material Inspector:

- **Height Map (Linear)**: use a real height texture with sRGB disabled, not an albedo/normal map.
  **Height Channel** `(1,0,0,0)` selects red; `(0,0,1,0)` selects blue for packed terrain masks.
- **Height Range (metres)** controls physical displacement independently of object scale.
  Start around `0.02` to `0.05`. **Height Midpoint** selects the zero-height level.
- **Height Normal Strength**, **Normal Strength**, **Height Smoothing** tune lighting and fine noise.
- **Maximum Subdivision**, **Target Edge Length**, **Full Detail Distance**, **Fade Distance**
  control adaptive density and distance fade. Shared edges use the same factors to avoid tessellation cracks.
- **Base Map** tiling/offset applies to every map. Metallic uses R/smoothness A; occlusion uses G.
  Alpha clipping is supported, transparent blending is not. Click **Save Material** to persist tuning.

Requires tessellation-capable hardware; unsupported platforms fall back to ordinary URP Lit without displacement.
Intended for UV-mapped MeshRenderers with normals and tangents, not skinned characters, terrain or grass blades.
Hard edges, split normals and UV seams can open under displacement: use continuous height and smooth welded
surfaces. Disable static batching for displaced objects. Each LOD needs the intended material/bounds component.
The collider, NavMesh and baked mesh silhouette are unchanged; light baking uses an undisplaced Meta surface.
No motion-vector pass is supplied (avoid relying on this surface for animated displacement/TAA motion vectors).
Increasing subdivision has a GPU cost; use short detail distances and reserve high settings for close-up assets.

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
  the dark raid forests; Meadow Grass, Lush Grass, Trodden Path and Damp Earth for the Haven; Forest Moss (ambientCG
  Moss004), Dark Humus (Poly Haven mud_forest) and Forest Litter (forest_ground_06) for the old-growth floor.
  `AgentScripts/PolyHaven/make_terrain_layers.py <work> <out> [layer ...]` rebuilds named layers only.
- **Palettes** (Data/Level/Palettes): River Stones, Riverbank, Cliff Rubble, Dark Forest Trees, Forest Floor Debris,
  Ferns, Haven Trees, Mossy Boulders. They work with the Scatter Brush too.
- **Data/World**: the Rock Faces cliff kit and the two paint rule sets.

Safe Haven › World › **Set Up World Assets** remakes anything missing (existing assets are kept).

### The two looks (converted packs)

The world is now built from two packs converted to the project's shaders by `VegetationPackConverter` (trees on Tree
Wind and fellable, rocks, logs and stumps on the tessellated Mesh Surface with their height maps, ground layers with
height in the mask). `WorldLook` describes each, and World Tools uses them everywhere:

| | **Forest Lake** (Mountain Lake: the Haven, the Prologue meadow) | **Mossy Forest** (Mossy Cedrus: the raids, the Prologue forest) |
|---|---|---|
| Prefabs | Art/Vegetation/Safe Haven (sugar maples and saplings, lake boulders, stones and crags, ferns, grass and flowers) | Art/Vegetation/Misty Woods (cedars, young cedars, cliff boulders, slabs, outcrops, mossy logs and stumps, shrubs, ferns, moss, grass) |
| Ground layers | 9 in Art/Vegetation/Safe Haven/Ground (Haven Meadow, Lush Moss, Path, Mossy Ground, Rock, Old Moss, Bank, Litter, Humus) | 4 in Art/Vegetation/Misty Woods/Ground (Cedar Litter, Moss, Soil, Rock) |
| Data (Data/World/…) | Safe Haven: *Haven Lake* Trees, Young Trees, Forest Floor, Boulders, Ferns, River Stones, Riverbank, Rubble, Crags, two paint rule sets | Mossy Forest: *Mossy Forest* Trees, Young Trees, Forest Floor, Undergrowth, Boulders, River Stones, Riverbank, Rubble, Crags, Dark Forest paint rules |
| Terrain material | Haven World Terrain Displacement | MistyWoods Map Terrain Displacement |
| Layer palette | *Forest Lake Ground* | *Mossy Forest Ground* |

Set Up World Assets makes the Mossy Forest's data where missing (its palettes start as copies of the Prologue forest's).
The raw packs (Assets/SeedMesh, Assets/VIVID_Arts) are not offered: their own shaders don't match the game's look.

#### Painting the ground (tessellated)

World Tools › Terrain › **Paint ground (tessellated)** shows every ground of a set as a picture. Click one: it joins
the terrain in view if missing (up to 16), the terrain's displacement is rebuilt so the new ground has its relief at
once, and Unity's Paint Texture brush opens on it (best effort: if Unity changes its terrain inspector, pick the brush
by hand). **Refresh Relief** rebuilds the displacement by hand; **Layer Palettes** makes a Terrain Tools layer palette
per look, so Paint Texture › Layer Palette adds a whole look at once. Terrains not yet on the tessellated shader get it
from a Look first.

**Safe Haven** is made with it too (`AgentScripts/BuildHavenWorld.cs`): one 400 m terrain round the portal in the
Haven look, a flat clearing, hills rolling outward and the land rising north to a crag (*North Crag*), a Haven river
past the east of the base (the drinking spot on its bank), two Haven paths (up the slope from the portal, east over a
ford) and four Haven groves round the clearing. Edit them under **World** in the scene. After changing the ground,
bake the NavMesh with its Bake button (a script uses `HavenNavMesh.Bake`, which keeps the data in its own asset:
NavMesh data inside a scene makes Unity save the whole scene as binary).

A demo: **Scenes/Demo/WorldBuilderDemo** (a river falling over a cliff, a ford, an outcrop and a dark forest).
`AgentScripts/WorldDemo.cs` rebuilds it.

## Scatter Brush

**Safe Haven › World › Place › Scatter Brush** (or World Tools › Place). Pick a palette, press **Paint**, then click or drag over the ground.

| Input | Does |
|---|---|
| Click / drag | Paints objects from the palette inside the circle |
| Shift + click / drag | Erases this palette's objects inside the circle |
| `[` / `]` | Smaller / larger brush |
| Esc | Stops painting |

- A **palette** (`ScatterPalette`, Create › Safe Haven › World › Scatter Palette) is a list of prefabs with a weight,
  a random scale range, how much each tilts with the slope (0 upright, 1 fully), how deep it sinks into the ground and
  whether it turns randomly. Its **spacing** is the smallest distance between two painted objects, so painting over
  the same spot again only fills the gaps.
- Defaults in `Data/Level/Palettes` (made by Safe Haven › Setup › World › Create Default Scatter Palettes, kept if they exist):
  **Forest Trees** (spruces, pine, the odd dead spruce, wind oak), **Rocks and Logs**, **Wood and Stone Nodes**
  (oak, grey stone, hardwood, granite) and **Forage** (berries, healing herb, flax, nettle).
- Objects go under **Level Scatter › &lt;palette name&gt;** in the scene, so one palette's objects can be hidden,
  selected or deleted together. Save ids are given to painted saveable objects (resource nodes) right after a stroke.
- Objects are set down by the bottom of their meshes, not their pivot.
- **One prefab:** Content Editor › Prefabs › a prefab › **Paint with Scatter Brush** opens the brush with just that
  prefab (spacing from its size; the palette is not saved).

## Drop to Ground

**Safe Haven › World › Place › Drop Selection to Ground** (End) and **Drop and Align to Slope** (Shift+End) set the selected
objects on whatever is below them (colliders first, then the terrain) by the bottom of their meshes. The Content
Editor's **Place in Scene** does the same.

## Static flags and heavy models

Leave placed scenery **non-static**; the project runs on the SRP Batcher without the GPU Resident Drawer, and the
camera only ever sees a small patch of the world.

- **Measured in SafeHaven (2026-10-30):** marking the 1,735 decor rock and crag renderers Batching Static changed draw
  calls from about 972 to 973 and CPU time not at all, but cost about 233 MB of graphics memory (each instance's
  geometry is copied into the combined mesh).
- **Never static batch anything using Tree Wind** (trees, ferns, plants). The wind works in object space, so batching
  breaks it.
- **Never static batch a gathering node, a choppable tree or a building.** They are rotated, scaled, swapped or felled
  at runtime (`ResourceNode` picks a model variant in `Awake`).
- **Terrains** are static (they are), but their **Draw Instanced** stays off. Terrain displacement needs it off
  (`TerrainDisplacementEditor`).
- **GPU Resident Drawer is on** (`PC_RPAsset`; static batching is off, as it requires). Identical mesh objects are
  drawn instanced from data kept on the GPU, which scales with scene size where per-object drawing doesn't.
  - Measured in the Editor (2026-10-30): draw calls halved (about 988 to 458 at the spawn, 527 to 268 in a dense
    grove), URP's CPU time about the same (2.0 to 2.4 ms and 1.8 to 1.7 ms) and no visible change.
  - **A shader only gets it with DOTS instancing:** put `#include_with_pragmas` URP's `ShaderLibrary/DOTS.hlsl` after
    `#pragma multi_compile_instancing` in every pass, and go through the transform macros, not `unity_ObjectToWorld`
    directly. Without it, objects silently fall back to normal drawing.
  - Shaders with it: Layered Lit, Tree Wind, Lit/Unlit (See-Through), Shader Graph and URP's own. Not needed: terrain
    (never drawn this way) and Grass (terrain details). Not done: Water and Mesh Surface (Tessellated).
  - **A renderer made at runtime whose non-DOTS shader is animated through a `MaterialPropertyBlock` is not drawn at
    all.** This hit the slash arcs. Draw such effects with `Graphics.RenderMesh` (as `SlashEffect` does), or give the
    shader DOTS instancing. Effects on URP's own shaders (attack telegraphs, the build ghost, the extraction zone) and
    the interaction outlines were checked and draw fine.
  - With it on, `FrameTimingManager` reports no GPU time; measure the GPU with the Profiler's GPU module instead.
- **The GPU's biggest cost was the terrain's tessellation** (`Safe Haven/Terrain (World)`, displacement within about
  35 m of the camera). Its 2 cm edge length meant 1-pixel triangles at the game camera's distance, drawn again in
  every tessellated pass: forward, both shadow cascades, depth and the normals SSAO reads. Measured at 1920x1080
  (2026-10-30), the terrain was 23 of the grove's 24.8 ms.
  - The fix is **Target Edge Length (pixels) = 4** (`_TessellationPixels`): no triangle smaller than about 4 pixels.
    It is the shaders' default now, and the Terrain Surface window's presets set it.
  - GPU time: about 24.7 to 7.2 ms in the grove, 9.0 to 4.3 ms at the spawn.
  - No visible change from the game camera or from 3 m away. Close up the limit lets the tessellation back in, since
    nearer triangles cover more pixels.
  - Measure GPU time with the Profiler's GPU module (`RawFrameDataView.frameGpuTimeMs`) at 1080p. A small Game view
    hides most of the cost.
- **What does cost the frame is triangles.** Check a new model with Level Check's census and the visible-triangle
  count, then lighten it with **Safe Haven › Setup › Art Pipeline › Simplify Model Meshes** (`ModelSimplifier`). Make sure a
  model's LOD levels really differ: `boulder_01.fbx` shipped three identical ones.

## Level Check

**Safe Haven › Scene › Level Check** (or `unity command sh_level`, `-- --fix true` to apply every fix) lists what the
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
