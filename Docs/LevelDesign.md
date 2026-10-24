# Level design tools

Tools for building the home world (and any hand-made scene) in the Scene view. All of them are under
**Safe Haven › Level**, all are undoable, and none add runtime code: what they place are ordinary prefab instances and
terrain paint. Raid maps have their own painted workflow (the Map Painter on "Raid Map"; see ARCHITECTURE "Raid maps").

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
