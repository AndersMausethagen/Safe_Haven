# Downloaded textures (CC0)

The textures in `Textures` are from [Poly Haven](https://polyhaven.com), licensed
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/): free for any use, commercial included, no credit
required. They are the building kit's tiling materials: a wall, post, roof or forge built in Blender
(`Tools/Blender/smithy.py`) is mapped with them at their real size, so every building made from the kit shares them.

| Files | Used for | Real size of one tile | Source |
|---|---|---|---|
| rough_wood_* | Logs, posts, beams | 0.5 m | https://polyhaven.com/a/rough_wood |
| weathered_planks_* | Plank walls, doors, shutters | 2.0 m | https://polyhaven.com/a/weathered_planks |
| rock_wall_10_* | Fieldstone plinths, chimneys, forges | 1.8 m | https://polyhaven.com/a/rock_wall_10 |
| rust_coarse_01_* | Iron: anvil, bands, hinges | 2.2 m | https://polyhaven.com/a/rust_coarse_01 |
| rock_boulder_dry_* | Field stones one by one: footings, chimneys, forges | 1.6 m | https://polyhaven.com/a/rock_boulder_dry |

Each has four maps (2K, the height map 1K and 8-bit): `_diff` colour, `_nor_gl` normal (OpenGL, as Unity reads it),
`_arm` occlusion / roughness / metallic in red / green / blue (the game's ORM order), `_disp` height.
The boulder's `_arm` is made from its roughness map (no occlusion, no metal): Poly Haven has none for it.
