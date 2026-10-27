# Downloaded models and textures (CC0)

Everything in this folder, and the ground textures in `Art/Terrain/World`, is licensed
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/): free for any use, commercial included, no credit
required. Sources: [Poly Haven](https://polyhaven.com) and [ambientCG](https://ambientcg.com).

## Models (Poly Haven)

| Folder | Prefab(s) | Source |
|---|---|---|
| rock_face_01, rock_face_02 | Rock Face A, B | https://polyhaven.com/a/rock_face_01, https://polyhaven.com/a/rock_face_02 |
| mountainside | Mountainside Crag | https://polyhaven.com/a/mountainside |
| boulder_01 | Lichen Boulder | https://polyhaven.com/a/boulder_01 |
| rock_moss_set_01, rock_moss_set_02 | Mossy Boulder 1-6, Mossy Rock 1-7 | https://polyhaven.com/a/rock_moss_set_01, https://polyhaven.com/a/rock_moss_set_02 |
| rock_07 | Grey Boulder (scaled up to 1.6 m) | https://polyhaven.com/a/rock_07 |
| stone_01 | Field Stone (scaled up to 0.9 m) | https://polyhaven.com/a/stone_01 |
| tree_stump_01, tree_stump_02 | Old Stump A, B | https://polyhaven.com/a/tree_stump_01, https://polyhaven.com/a/tree_stump_02 |
| dead_tree_trunk, dead_tree_trunk_02 | Dead Branch, Rotting Log | https://polyhaven.com/a/dead_tree_trunk, https://polyhaven.com/a/dead_tree_trunk_02 |
| root_cluster_01 | Root Tangle | https://polyhaven.com/a/root_cluster_01 |
| fern_02 | Fern 1-4 | https://polyhaven.com/a/fern_02 |

## Ground textures (`Art/Terrain/World`)

| Layer | Source |
|---|---|
| Pine Needle Floor | Poly Haven forrest_ground_03 |
| Mossy Forest Floor | Poly Haven forest_leaves_02 |
| Muddy Bank | Poly Haven brown_mud_leaves_01 |
| Gravel | Poly Haven rocky_trail |
| Cliff Rock (also the terrain shader's rock) | Poly Haven mossy_rock |
| Meadow Grass, Lush Grass | ambientCG Grass004, Grass001 |
| Trodden Path, Damp Earth | ambientCG Ground030, Ground037 |

## How they were made game-ready

`AgentScripts/PolyHaven`: `fetch_polyhaven.py` downloads (2k maps), `convert_rocks.py` runs in Blender (pivot at the
bottom centre, LOD0 decimated to a budget plus LOD1 at 35 % and LOD2 at 12 %, a convex collider of at most 255
triangles, and a material sidecar: Safe Haven/Layered Lit with moss on upward faces, or Tree Wind with alpha clip for
ferns), `make_terrain_layers.py` builds the terrain layer maps (colour, OpenGL normal, mask: metallic, occlusion,
height, smoothness). Safe Haven › World › Set Up World Assets makes the prefabs, layers and palettes from them.
