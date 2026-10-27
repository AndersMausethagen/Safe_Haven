"""blender -b --factory-startup --python inspect_models.py -- <work dir>: meshes, triangles, size and materials of every
downloaded Poly Haven glTF."""
import glob
import os
import sys

import bpy

work = sys.argv[sys.argv.index("--") + 1]
for path in sorted(glob.glob(os.path.join(work, "*", "*.gltf"))):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    print("==", os.path.basename(path))
    for o in bpy.context.scene.objects:
        if o.type != "MESH":
            continue
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        d = o.dimensions
        mats = [m.name for m in o.data.materials if m]
        print(f"  {o.name}: tris={tris} size=({d.x:.2f}, {d.y:.2f}, {d.z:.2f}) loc={tuple(round(v, 2) for v in o.matrix_world.translation)} parent={o.parent.name if o.parent else None} mats={mats}")
