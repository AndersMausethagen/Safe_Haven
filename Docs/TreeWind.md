# Tree wind (Local Generator trees)

Trees exported by the Local Generator's Vegetation tab (`Assets/_Project/Models/Generated/Vegetation/<tree>/`)
carry baked wind weights:
- uv1 = (sway 0 ground → 1 crown, flex 0 trunk → 1 twig tip);
- uv2 = (per-branch phase, leaf flutter).

`Safe Haven/Tree Wind` (`Shaders/TreeWind.shader`) bends them with the same `TreeWind.hlsl` formula that the
generator's 3D preview uses, so a tree sways in Unity as it did in the app. Bark, leaves and vines all use the
shader, because the sway bends the whole tree and leaves would otherwise detach from their branches.

## Use

1. Export the tree from the generator. On import, `TreeWindMaterials` creates:
   - `<tree>/Materials/<Bark|Leaves|Vines>.mat` (Tree Wind, copied from the glTF materials);
   - a `<glb name>_Wind.prefab` variant next to each GLB.
   glTFast ignores material remaps, so **place the `_Wind` prefab**, not the GLB.
2. If a GLB instance is already in a scene, select it and run **Safe Haven > Environment > Apply Tree Wind
   Materials**. Running it on a GLB or folder in the Project window rebuilds that tree's materials and variants.
3. Wind comes from the scene's **Forest Wind** (Moss Wind + Directional **WindZone**):
   - **WindZone arrow** sets the direction.
   - **Main** scales tree sway (0 = still) and moss strength.
   - **Pulse Magnitude** sets the moss gusts.
   - **Turbulence** only affects particles.

   The trees' own gusts come from the shader.

## Per-material settings

- **Wind Strength** (0–2): per-material multiplier. Keep bark and leaves equal so they stay attached.
- **Tree Height**: the tallest mesh in the tree folder, in object units. It is filled in automatically; the sway
  amplitude scales with it, as in the preview.
- **Alpha Clip / Cutoff / Cull**: copied from the glTF leaf material (clipped at 0.5, two-sided).
- **ORM map**: R = AO, G = roughness, B = metallic, matching the generator's packed map.

Existing materials are never overwritten on reimport, so tuning is kept. Delete a material to regenerate it.

## Prologue's procedural conifers

The prologue's existing spruce/pine/dead-spruce models were not generator exports and had no wind UVs.
Their scene-specific copies in `Data/Prologue/Vegetation` contain baked height/radial sway and flex weights
in UV1, with branch phase and flutter in UV2, and use the same **Safe Haven/Tree Wind** shader. Wind materials
preserve the original colour/texture look, with matching strength for bark and needles; needles use fast leaf
lighting. Mesh bounds include a sway margin to avoid edge-of-screen culling.

Only the prologue TerrainData references these five wind prefabs. Original raid prefabs/materials are unchanged;
boulders and fallen logs retain their original static prototypes. Adjust the prologue's Forest Wind and its
storm multiplier, or the copied materials' Wind Strength, to tune movement without changing the raid.

## Performance

- **Fast Leaf Lighting** is on for alpha-clipped materials (leaves, vines).
  - Per vertex: sun and ambient, darker inside the canopy (using the baked flex).
  - Per pixel: the leaf texture, one hardware shadow compare (no soft-shadow taps) and a cheap Lambert for point
    lights.
  - Skipped: the normal map, specular, reflection probes and soft-shadow filtering.

  Bark keeps full PBR. Measured on a tree-filled 1080p view of SafeHaven, the frame went from 3.40 to 3.16 ms with
  vertex-only shadows; with the per-pixel shadow added back the gain is within noise on this GPU. It matters more on
  weaker GPUs and with many trees.
- **Depth priming** (the PC renderer's Depth Priming Mode, now Disabled) shades each leaf pixel only once instead of
  once per overlapping card. In the same view it cut the frame from about 3.2 ms to 2.7–3.0 ms. It is a project-wide
  renderer setting, so it was measured but not changed.
