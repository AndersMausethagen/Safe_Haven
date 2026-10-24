# Grass, bushes, Nordic plants and glowing plants (terrain details)

Unity's terrain detail system draws painted grass with GPU instancing. `Safe Haven/Grass` (`Shaders/Grass.shader`)
does the rest:
- bends the blades in the shared Forest Wind (storms included);
- pushes them away from the player;
- tints them with the colour of the ground they grow on;
- hides them under water;
- lets glowing plants glow at night.

Terrain details are culled per patch, so grass outside the camera's view is not drawn.

## Use

1. Select the terrain and run **Safe Haven > Environment > Grass > Fill Selected Terrain With Grass...**.
   - It suggests how much Short Grass, Tall Grass and Bush grows on each ground layer, guessed from the layer's
     colour: green moss is dense, brown soil sparse, grey stone bare. Adjust the amounts and press Fill.
   - It replaces the three grass layers and leaves glowing plants and other details alone.
   - Nothing grows under water or under objects standing on the ground (buildings, rocks).
   - The first run also creates the assets in `Art/Generated/Grass/`, bakes the ground colour and gives
     `PlayerArmature` a `GrassInteractor`.
2. Touch up with the terrain's **Paint Details** tool:
   - grass: Short Grass, Tall Grass, Bush;
   - Nordic forest floor: Fern, Blueberry, Lingonberry, Heather, White Flowers (wood anemone), Yellow Flowers;
   - Glow Mushrooms and Glow Flowers.

   The fill tool's **Plants** amount grows the Nordic plants in natural patches: each kind in its own patches, with a
   few strays. Ferns and berry shrubs clear the grass right around them so they don't drown in the lawn.
   **Add Grass and Bushes to Selected Terrain** only adds the kinds, for painting by hand.
3. Repainting the terrain's textures rebakes the ground colour automatically. If it is ever out of date, run
   **Rebake Ground Colours of Selected Terrain**.
4. Water clears what grows under it. Making or carving water (Water menu) removes the details under it, and the
   ground colour map marks water so grass painted there later stays hidden. **Clear Grass Under Water** does it by hand.
5. To make anything else part the grass (enemies, a cart, a thrown stone), select it and run **Make Selected Push
   Grass**, or add `GrassInteractor` yourself. Up to 8 are used at once; those nearest the camera win.

## Why it looks full from above

- **Camera-facing blades:** every blade turns around its own axis to show its full width to the camera (an axial
  billboard). From a top-down camera, edge-on blades would look like hair. The blade's direction is stored in the
  mesh normal, its half-width in vertex colour A and its side in UV0.x, because terrain details pass only the basic
  vertex channels on to the shader.
- **Many blades per instance:** a clump of 36 short or 24 tall blades. Vertices are cheap; instances and draws are
  what cost.
- **Instance Count scatter mode:** a cell's value (about 25 cm square) is the number of clumps in it, up to 2 for
  Short Grass. Coverage mode treats each clump as about a metre wide and plants very few.
- **Upward normals:** soft, even lighting instead of dark slivers.

## Look (per material, in `Art/Generated/Grass/`)

| Setting | Effect |
|---|---|
| **Bottom / Top Color**, **Dry Color**, **Dry Amount**, **Patch Scale** | The grass's own colours, with dry patches |
| **Ground Color** (0–1) | How much of the ground's colour the grass takes: brown ground → brown grass |
| **Ground Tip**, **Ground Brightness** | How far up the blade the ground colour reaches, and its brightness |
| **Blade Variation** | Per-blade brightness variation |
| **Size**, **Clump Scale / Clump Height** | Overall size, and patches of taller and shorter grass |
| **Wind Strength / Speed**, **Flutter**, **Wind Waves** | Sway, blade flutter and gust waves rolling over the field |
| **Push Strength**, **Flatten** | How far grass bends away from a `GrassInteractor`, and how much it flattens |
| **Translucency** | Sun shining through blades when looking towards it |
| **Blades Face the Camera** | On for the generated grass clumps (see above); off for bushes and glowing plants |
| **Glow Colour**, **Glow Pulse**, **Pulse Speed** | Glowing plants: light-blue glow at night (and faintly in heavy cloud) where vertex colour B is set, pulsing gently; bloom adds the halo |

Blade width/height ranges and density are on the terrain's detail prototype (Edit Details).

## Cost

- One instanced draw per patch and prototype; patches outside the camera are culled.
- Grass beyond **Far Start** (20 m) is thinned to **Far Density** and shrinks away before **Far End** (50 m). Far End
  is kept equal to the terrain's *Detail Distance*, which is 50 m in Safe Haven because the top-down camera sees
  about 35 m.
- Beyond **Animation Distance** the grass stands still (no wind or push maths).
- Grass casts no shadows. Bushes use alpha-clipped leaf cards with coverage-preserving mipmaps.
- The ground colour is one small baked texture (`<TerrainData>_GrassColour.png`), sampled per vertex. Its alpha is
  the water mask.
- Measured in Play mode in SafeHaven, with the editor's own overhead included: the dense fill (about 160k short-grass
  clumps over the 130 m terrain) left the frame time unchanged within noise: 7.9 ms, against 8.5 ms with no grass.

Wind comes from `MossWind` (direction, strength and gusts), the same source as moss and trees. Weather scales it
(see [weather](Weather.md)).
