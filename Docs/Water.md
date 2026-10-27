# Water (ponds, streams, waterfalls)

`Safe Haven/Water` (`Shaders/Water.shader`) is one transparent surface shader for still water, flowing water and
waterfalls:

- **Depth colour:** the camera depth under the surface gives the water depth. Water shades from Shallow to Deep
  Colour, and you see into it as far as **Clarity**. The bed is refracted and lit by moving caustics.
- **Flow:** flat water scrolls its ripples and foam along **Flow Direction**. That direction is in object space, so
  rotating the object turns the river. **Flow Speed** is in m/s.
- **Steep faces (waterfalls):** they run down the mesh UV at **Fall Speed**. Faster and steeper water turns whiter,
  murkier and foamier.
- **Foam:**
  - a broken line at the shore;
  - rings around stones that reach the surface, trailing downstream in flowing water;
  - on fast or falling water;
  - around churning spots (waterfall plunge pools).
- **Reacts to the world:**
  - the player and dropped stones make ripples and wake foam (`WaterInteractor`);
  - rain makes rings (`_SafeHavenRain`);
  - wind makes the surface choppier (the scene's **Moss Wind**).

## Use

All menus are under **Safe Haven > Environment > Water**:

| Menu | Does |
|---|---|
| **Create Pond** / **Create River Section** | Adds a water plane at the Scene view's centre. The river flows along its local X (red arrow) |
| **Create Waterfall...** | Asks for width, height, lip length and throw speed, then adds the falling sheet, its plunge (foam, ripples, mist, spray) and a pool. Its blue Z arrow points the way the water falls; put the lip at the top of the cliff |
| **Make Selected Still Water** / **Make Selected Flowing Water** | Gives any mesh (a plane, a box, your own river mesh) the water material and a `WaterSurface` |
| **Carve Terrain Under Selected Water...** | Digs a bed with sloping banks into the terrain under the selected water, so depth colour, the visible bed and shore foam show. Undoable. If the bed would go below the terrain's lowest height, the terrain is lowered and its heights raised by the same amount, so the ground stays put |
| **Make Selected Disturb Water** | Adds a `WaterInteractor`, so a thrown stone, a boat or a monster makes ripples and splashes |

For a winding river, use the World Builder's **River** (GameObject › Safe Haven › World › River; see
[level design](LevelDesign.md)): it digs the bed, lays a water mesh along the spline and turns steep drops into
waterfalls. Its material (*Water River (World)*) has **Flow Follows the Mesh** on: each vertex carries its own flow
direction and speed (UV2), and the ripples and foam cross-fade two phases so the flow can bend without stretching them.
Its `WaterSurface` gets the river's centre line (surface height and half width along it), so "in the water" and the
surface height are right along the whole river, not just inside the mesh's box.

The create and make menus also give `PlayerArmature` a `WaterInteractor`. Making or carving water also removes the
grass and other terrain details under it (and grass painted there later stays hidden; see [grass](Grass.md)).

Water needs a bed below it: depth is what gives it colour. Water lying flat on the ground looks like a thin film
with foam all over.

## Look (per material, in `Art/Generated/Water/`)

| Setting | Effect |
|---|---|
| **Shallow / Deep Colour**, **Deep At** | Colour by depth |
| **Clarity** | How many metres you see into the water |
| **White Water Colour**, **Fast Water Is Murky** | Fast and falling water turns white and less clear |
| **Ripple Size / Strength**, **Wind Makes It Choppy** | Surface detail; wind (Moss Wind) roughens it |
| **Smoothness**, **Reflection Strength**, **Refraction** | Sun glints, sky reflection, how much the bed bends |
| **Flow Direction**, **Flow Speed** | River direction (object X, Z) and speed. Still water drifts gently with the wind |
| **Fall Speed**, **Fall Streak Size** | Waterfalls (steep faces; UV in metres, v downhill) |
| **Edge Fade from Vertex Alpha**, **Cull** | Waterfall sheets: faded edges, both sides visible |
| **Shore and Stone Foam**, **Foam Trail Behind Stones**, **Foam from Speed**, **Foam on Steep Water** | Where foam appears |
| **Caustics Strength / Size** | Light patterns on the bed |
| **Player and Stone Ripples**, **Rain Ripples** | Strength of interaction ripples and rain rings |

`Waterfall` (on the waterfall object) has width, height, lip, throw speed (how far it arcs out), spread and edge
fade. The mesh rebuilds as you edit them, and the plunge moves to where the water lands. `WaterInteractor` has two
modes:
- **Moving:** ripples, wake foam when running, and a splash prefab when it drops in faster than *Splash Speed*.
- **Constant:** always churns, as at a plunge pool or rapids.

## Cost

- Each water surface is one transparent draw. It needs the camera depth and opaque textures (on in `PC_RPAsset`).
  Cameras without them (the Mobile renderer) get a simpler alpha-blended look automatically: no refraction, depth
  colour or shore foam.
- Ripples are one 256×256 two-channel texture covering 24 m around the player, simulated on the GPU 30 times a
  second, only in Play mode.
- Foam spots are at most 8; the shader loops over them.
- Stone trails take 3 extra depth samples per pixel, only on flowing water.
- Mist and spray are a few dozen particles per waterfall.
