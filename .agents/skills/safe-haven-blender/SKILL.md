---
name: safe-haven-blender
description: "Make or fix 3D assets for Safe Haven in Blender and bring them into Unity: props, buildings and fences modelled by script, CC0 downloads re-exported (Humanoid rigs, T-pose, UV maps), FBX export settings this project uses, and checking the result in the game. Use when a Safe Haven task needs Blender (modelling, re-rigging, baking, re-exporting a model); the Unity side (import, prefab, scene) still follows the Unity skills."
---

# Safe Haven: Blender to Unity

Blender work for this game ends in Unity, so a model is only done when it looks right in the Haven or a launched raid.
The general Blender skills (`text-to-blender`, `blender-modeling`, `blender-materials`, `blender-export`, ...) cover how to
work in Blender; this skill covers what this project needs from it.

## Two ways to drive Blender

1. **A script, run headless** (preferred for anything that ships). Write `Tools/Blender/<asset>.py`, run it with
   `"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" -b --factory-startup --python Tools/Blender/<asset>.py -- <project root> <preview.png>`.
   It is repeatable, reviewable and rebuilds the asset after a change. `Tools/Blender/skigard.py` is the model to copy:
   a docstring saying what it makes and where it writes, procedural textures baked to PNGs, FBX export, a preview render and
   build-menu icons, and the `.blend` saved beside the script.
2. **The Blender MCP** (official Blender Lab server, `mcp__blender__*` tools) for looking and trying: inspecting a downloaded
   .blend, checking UV maps, a quick viewport screenshot, finding an API with `search_api_docs`. It needs Blender open:
   the **MCP** add-on starts its bridge on its own a moment after Blender opens (Auto Start, port 9876; Preferences ›
   Add-ons › MCP to start or stop it by hand). If the tools fail, Blender is closed or the bridge is off: ask the user to
   open Blender rather than launching a GUI Blender yourself. For a headless session with no user file, the bridge also
   runs in the background: `blender.exe -b --command blender_mcp` (each request must finish before it answers). Once something works, move it into a script.

The MCP runs any Python in the user's open Blender with no sandbox: never point it at a file with unsaved work without asking,
and save to a new path, not over the user's .blend.

Untrusted files (anything downloaded) open only headless with `--factory-startup --disable-autoexec`, so their Python
drivers and scripts don't run.

## FBX for Unity

What the project's own exports use (`skigard.py`), which imports with Unity's defaults (Scale Factor 1, Use File Scale,
no Bake Axis Conversion):

```python
bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                         axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH'},
                         mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
```

- Model in metres at real size (a wall piece is 4 m, the skigard is about 1.3 m high), with the pivot where it should sit on
  the ground; a door or gate leaf as its own object, its pivot on the hinge.
- Textures as separate PNGs next to the FBX (`embed_textures=False`), named `<Asset>_Albedo.png`, `<Asset>_Normal.png`.
- Rigged models: add `'ARMATURE'` to `object_types`, `add_leaf_bones=False`, and bake the animation only when the clips live in
  the file.
- Where things go: `Assets/_Project/Art/<Area>/<Asset>/` (Haven buildings under `Art/Haven`, creatures under
  `Art/Enemies/Downloaded`), building icons in `Assets/_Project/UI/BuildingIcons`.
- Then the Unity side: a builder in `AgentScripts/Build<Asset>.cs` sets up the import, materials and prefab (see
  `BuildSkigard.cs`, `BuildCreatureModels.cs`).

## Downloaded creatures (CC0)

Look at OpenGameArt's Kelgar models by CDmir first; cute packs (googly eyes, chibi) don't fit the dark Nordic folktale look,
whatever their licence. Traps that each cost a round:

- **Humanoid**: feet on separate IK chains, or a rest pose that isn't a T-pose, and Unity won't take it as Humanoid.
  Re-export with the feet parented to the legs and the T-pose applied as the rest pose.
- **Several UV maps**: old .blend files hold more than one, and FBX puts one first, which may be the wrong one. Patchy textures
  or the texture's background showing mean this. Check each map by sampling the image at its UVs and counting corners that land
  on background; the right map can differ per material. Make the right one the active and first map before export.
- Use the model's own eye geometry when it has any.

## Checking it

- Judge it from the gameplay camera, in the Haven and in a launched raid (isolated saves: Safe Haven › Scene › Play Haven
  (Isolated Saves)), not from a Blender render or a preview scene: tints that look fine there read as black silhouettes in
  play. Raids are dark, misty and cold; the Haven is greener and lighter.
- Keep the game's default graphics quality: no cheaper materials or lower-poly stand-ins to save frame time without asking.
- A Blender preview render is for the user's first look and for icons, not proof the asset works.
