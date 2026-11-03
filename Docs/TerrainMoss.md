# Terrain moss

Earthy Nordic forest moss for URP (GDD section 19). The terrain renderer needs no additional packages.
The preview adds original procedural PBR textures and two editable Amplify Shader Editor graphs.
These are authored procedural materials, not photogrammetry; scanned textures or authored clump meshes can replace them later.

## Try it

Choose **Safe Haven > Art Pipeline > Open Moss Preview**. This opens a separate, saved demonstration scene with
a moss-covered clearing and a bare winding path. It is not a gameplay scene and is not added to Build Settings.
Enter Play mode to see the wind, or select **Forest Wind** and enable **Preview wind in Scene view**.
The upgraded preview contains moss-covered granite, a leaf-litter path, and four labelled material samples.
It uses **2,957 leafy tufts** with the saved paint; the original ribbon mode is still available.

## Ground materials and Amplify

Open the materials under `Assets/_Project/Art/Moss/Ground`:

- **Damp Leaf Soil**, **Forest Floor Blend**, **Cushion Moss**: soil, a height-sensitive soil/moss blend, and pure moss.
- **Mossy Granite**, **Weathered Granite**: stone with or without upward-facing moss.
- **ForestPreviewVolume**: preview-only ACES and exposure controls.

Adjust **Tint**, **Tiling**, **Relief**, **Ground Normal Strength**, **Moss Normal Strength**, and **Moss Coverage**
in the material Inspector. Ground coverage 0 means soil, 1 means moss. The rock shader also exposes **Slope Threshold**;
coverage and threshold together determine how far moss extends down its sides. **Edge Contrast** changes the height-sensitive boundary.
Relief is a small parallax UV offset, not displacement, tessellation or a changed silhouette.

Open `Assets/_Project/Shaders/Forest Ground ASE.shader` or `Mossy Stone ASE.shader` in Amplify to edit the actual node graphs.
They use Amplify's URP Lit template (created with ASE 1.9.9.14 and URP 17.6). The terrain overlay's
`TerrainMoss.shader` is a separate hand-authored wind/masking shader, not an Amplify graph.
Mesh materials use UV0; terrain textures use metre-based tiling. Moss on rocks is a shaded material, not animated tufts.

Each 1024-square texture set (**Moss**, **Soil**, **Stone**) has:

| Map | Channels | Import |
|---|---|---|
| BaseColor | RGB color, no baked lighting | sRGB |
| Normal | Tangent-space normal | Normal Map |
| Packed | R ambient occlusion, G roughness, B height | Linear |
| TerrainMask | R metallic (0), G occlusion, B height, A smoothness | Linear |

All maps repeat and include mipmaps. Replace them with compatible art without changing gameplay code.
Use **Safe Haven > Art Pipeline > Regenerate Forest Ground Textures** to recreate the original maps;
it confirms before overwriting them. Their deterministic source is `ForestGroundTextures.cs`.
**Create Amplify Forest Shaders** creates missing graphs without overwriting populated graphs or designer edits.
**Upgrade Moss Preview Materials** upgrades the saved preview once; it refuses duplicate showcase roots.
It never applies the showcase to a gameplay scene.

## Add it to a terrain

### Safe Haven scene

Safe Haven now uses **Clearing > Safe Haven Terrain**, replacing the old ground block.
Its 130 x 130 metre footprint and level y=0 collision surface are unchanged so existing trees,
resources, portal, arrival points and building placement remain aligned.

The independent Terrain Data, terrain material, three paint layers and rebaked NavMesh are in
`Assets/_Project/Art/Terrain/SafeHaven`. Paint **Forest Soil**, **Forest Moss**, or **Weathered Stone**
with Terrain's Paint Texture tool. Broad irregular patches, fine edge variation, soil around the portal,
and a soil route toward the portal provide the starting blend; stone is concentrated around existing rootbound stones
and occasional exposed patches. These are saved paint weights, not a runtime generator.

The terrain uses URP Terrain Lit with height blending and the new PBR maps. **Terrain Moss** adds the
paint-masked moss shader and up to 1,800 leafy tufts, driven by **Forest Wind**. The Amplify mesh shaders
are not assigned directly to Terrain: they do not implement Unity's terrain-layer blending.
Preview materials and layers remain separate, so editing this scene's layer tint/tiling does not change MossPreview.
Rebuild moss after repainting; rebake the scene's **NavMesh** if you sculpt the terrain.

### Other terrains

1. Select a Unity **Terrain** object.
2. Choose **Safe Haven > World > Terrain > Add Moss to Selected Terrain**.
3. In Terrain's **Paint Texture** tool, select **Forest Moss** and paint where moss belongs.
   Paint another layer over it to remove moss. Terrain alphamaps are blend weights, not image transparency.
4. Select **Terrain Moss > Rebuild Moss** after painting or sculpting. This redistributes the tufts and refits
   the surface to the heightmap. Save the scene and Terrain Data normally.

The setup tool adds a layer and components; it does not replace the terrain material, repaint existing ground,
or modify gameplay scenes automatically. You can instead assign an existing painted Terrain Layer to **Moss Layer**.
All alphamap channels are supported, including layers beyond the first four. Terrain holes suppress the moss.
Shared Terrain Data means shared paint, just like ordinary Unity Terrain.

## Art controls

- **Shadow Color / Moss Color / Tip Color:** deep moss, main green, and fresh tuft tips.
  Start with warm olive greens at home; try colder, less saturated greens in raids.
- **Pattern Scale:** procedural color frequency in world space, not tuft density.
- **Smoothness:** low values suit soft, matte moss.
- **Base Color Map / Normal Map / Packed Map:** optional PBR maps. Without maps, the original procedural look remains.
  **Texture Size** is metres per repeat; **Texture Influence** blends base color with the procedural palette.
  **Normal Strength** changes relief lighting. When a packed map is supplied, its roughness replaces Smoothness.
  The height channel is used by the Amplify materials, not by the terrain overlay.
- **Coverage Threshold / Edge Softness:** minimum layer weight and irregular boundary width.
- **Surface Resolution:** height-following grid subdivisions per terrain. Increase for steep/sculpted ground;
  lower it for broad, smooth land. **Surface Offset** avoids overlap with the original ground.
  If distant terrain pokes through, reduce the Terrain's **Pixel Error** (LOD simplification), increase the offset
  slightly, or use smaller terrain tiles. The preview uses Pixel Error 1.
- **Tufts Per Square Metre / Maximum Tufts:** sparse density and a hard per-terrain cap.
  Partial paint weights thin the distribution; the cap can reduce density on large terrains.
- **Tuft Height / Width / Maximum Slope:** short fronds and a slope limit for the tufts.
- **Leafy Tufts:** five feathered sprigs per tuft instead of crossed ribbons. This costs 200 vertices / 100 triangles
  per tuft versus 12 vertices / 8 triangles for ribbons. Keep density, cap and draw distance conservative.
- **Draw Distance:** tufts fade out near this camera distance; the flat moss remains.
- **Wind Response:** per-terrain multiplier on the shared wind.
- **Seed:** stable placement across rebuilds and scene loads.

**Forest Wind** controls horizontal direction, strength (maximum tip displacement in metres), speed and gusts.
Keep just one enabled **Moss Wind** in the loaded scenes. No wind source, or strength zero, means still moss.
A Directional **WindZone** on Forest Wind overrides the direction (its arrow), scales strength by **Main** and
replaces gusts with **Pulse Magnitude**, so moss, generated trees ([tree wind](TreeWind.md)) and particles with
External Forces share one wind. Speed stays on Moss Wind. Shaders read `_SafeHavenWind` (XZ direction, moss
displacement, speed), `_SafeHavenWindGusts` and `_SafeHavenTreeWind` (Main).

## Limits and architecture

- Uses a masked mesh skin above the original Terrain rather than replacing URP's Terrain Lit shader.
  Existing terrain layers, physics, navigation, and the terrain basemap remain intact.
- Terrain transform must be unrotated with scale `(1, 1, 1)`. Translation is supported; rebuild after moving it.
- Surface receives URP lighting, shadows, fog and SSAO. Fronds do not cast shadows.
  This is an opaque/cutout effect, not expensive transparent fur or geometry-shader grass.
- Objects, meshes and materials under **Moss (generated)** are temporary and excluded from scene serialization.
  The component holds the shader reference so player builds retain it. It rebuilds when enabled.
- Designer changes request a main-thread rebuild. Painting updates the surface mask immediately; new tuft positions,
  terrain size, layer-list and heightmap changes need a manual rebuild. Runtime terrain sculpting/painting must call
  `TerrainMoss.Rebuild()` when finished.
- This is a bounded prototype renderer, with two mesh renderers per terrain and a tuft cap, not a streamed vegetation
  system. Split very large worlds into terrain tiles before increasing budgets.
- No collision, resource harvesting, dynamic growth or per-tuft save data is introduced.
- The preview's older tiny solid-color texture assets remain as fallbacks. The upgraded terrain uses the PBR maps.
- The preview uses around 591,400 tuft vertices before the surface and rocks. It is a material showcase, not a
  final vegetation performance budget; test on target hardware before using this density across a level.
