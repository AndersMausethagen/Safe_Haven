"""Turns downloaded Poly Haven rock glTFs into game-ready FBX for Unity:

    blender -b --factory-startup --python convert_rocks.py -- <work dir> <Assets/_Project/Models/PolyHaven> '<json plan>'

plan: {"<asset>": {"lod0": triangles, "height": metres or 0 (keep scan size), "split": bool (one FBX per mesh),
                   "foliage": bool (leaf cards: Safe Haven/Tree Wind with alpha clip and both sides, no collider;
                                    needs textures/<asset>_Color.png with the alpha in it)}}

Per mesh: pivot at the bottom centre, LOD0 decimated to the budget, LOD1 (35 %) and LOD2 (12 %), and a convex
<name>_Collider hull of at most 255 triangles (GeneratedModelPostprocessor turns it into a convex MeshCollider; the
model importer builds the LOD Group from the _LOD names). The asset's maps are copied next to the FBX as
<asset>_Color.jpg, <asset>_Normal.jpg and <asset>_ORM_2k.jpg (Poly Haven's ARM: occlusion, roughness, metallic), and
each FBX gets a material sidecar (LocalSpawnMaterials) asking for Safe Haven/Layered Lit with a moss layer on top.
"""
import glob
import json
import os
import shutil
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
work, out_root, plan = argv[0], argv[1], json.loads(argv[2])

LOD_RATIOS = (0.35, 0.12)
COLLIDER_MAX_TRIS = 255
MOSS = "Assets/_Project/Art/Generated/SurfaceLayers/moss/moss_"


def tris(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def decimate(obj, ratio):
    if ratio >= 1:
        return
    mod = obj.modifiers.new("Decimate", "DECIMATE")
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = bpy.data.meshes.new_from_object(evaluated)
    obj.modifiers.clear()
    old, obj.data = obj.data, mesh
    if old.users == 0:
        bpy.data.meshes.remove(old)


def hull(points, name):
    import bmesh
    bm = bmesh.new()
    for p in points:
        bm.verts.new(p)
    result = bmesh.ops.convex_hull(bm, input=bm.verts)
    bmesh.ops.delete(bm, geom=list(set(result["geom_interior"]) | set(result["geom_unused"])), context="VERTS")
    bmesh.ops.triangulate(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    return mesh


def collider(points, name):
    mesh = hull(points, name)
    for _ in range(6):  # decimating a hull can dent it, so re-hull until it is convex and small enough
        if len(mesh.polygons) <= COLLIDER_MAX_TRIS:
            break
        tmp = bpy.data.objects.new("hull_tmp", mesh)
        bpy.context.scene.collection.objects.link(tmp)
        decimate(tmp, COLLIDER_MAX_TRIS * 0.8 / len(mesh.polygons))
        points = [v.co.copy() for v in tmp.data.vertices]
        bpy.data.objects.remove(tmp)
        mesh = hull(points, name)
    return mesh


def sidecar(asset, foliage=False):
    def tex(name, path):
        return {"name": name, "type": "texture", "texture": path}

    def num(name, value):
        return {"name": name, "type": "float", "value": [value]}

    if foliage:
        return {
            "version": 1,
            "shader": "Safe Haven/Tree Wind",
            "properties": [
                tex("_BaseMap", f"{asset}_Color.png"),
                tex("_BumpMap", f"{asset}_Normal.jpg"),
                tex("_ORMMap", f"{asset}_ORM_2k.jpg"),
                num("_AlphaClip", 1.0),
                {"name": "_ALPHATEST_ON", "type": "keyword", "value": [1.0]},
                num("_Cutoff", 0.45),
                num("_Cull", 0.0),
                num("_Roughness", 1.0),
                num("_WindStrength", 0.35),
                num("_TreeHeight", 1.0),
            ],
        }

    return {
        "version": 1,
        "shader": "Safe Haven/Layered Lit",
        "layers": ["Moss"],
        "properties": [
            tex("_BaseMap", f"{asset}_Color.jpg"),
            tex("_BumpMap", f"{asset}_Normal.jpg"),
            tex("_ORMMap", f"{asset}_ORM_2k.jpg"),
            {"name": "_ORMMAP", "type": "keyword", "value": [1.0]},
            num("_Roughness", 1.0),
            num("_OcclusionStrength", 1.0),
            num("_Metallic", 0.0),
            num("_LayerTriplanar", 1.0),
            {"name": "_LAYERTRIPLANAR_ON", "type": "keyword", "value": [1.0]},
            num("_L1Amount", 0.55),
            num("_L1Source", 4.0),
            num("_L1Upward", 1.0),
            num("_L1Patchiness", 0.75),
            num("_L1Tiling", 1.2),
            tex("_L1Color", MOSS + "Color.png"),
            tex("_L1Normal", MOSS + "Normal.png"),
            tex("_L1MetallicGloss", MOSS + "MetallicGloss.png"),
            tex("_L1Height", MOSS + "Height.png"),
            num("_L1HeightChannel", 0.0),
        ],
    }


def export(obj, name, folder, settings):
    """obj: one mesh with transforms applied. Writes <folder>/<name>.fbx and its sidecar."""
    bpy.ops.object.select_all(action="DESELECT")
    corners = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    height = hi.z - lo.z
    scale = settings.get("height", 0) / height if settings.get("height") else 1.0
    pivot = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
    for v in obj.data.vertices:
        v.co = (obj.matrix_world @ v.co - pivot) * scale
    obj.matrix_world.identity()
    obj.data.update()

    budget = settings.get("lod0", 8000)
    for _ in range(4):  # collapse stops short on scans with many seams, so repeat until within the budget
        if tris(obj) <= budget * 1.05:
            break
        decimate(obj, budget / tris(obj))
    obj.name = f"{name}_LOD0"
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    obj.parent = root
    for level, ratio in enumerate(LOD_RATIOS, 1):
        dup = obj.copy()
        dup.data = obj.data.copy()
        dup.name = f"{name}_LOD{level}"
        bpy.context.scene.collection.objects.link(dup)
        decimate(dup, ratio)
    shell = None
    if not settings.get("foliage"):
        shell = bpy.data.objects.new(f"{name}_Collider", collider([v.co.copy() for v in obj.data.vertices], f"{name}_Collider"))
        bpy.context.scene.collection.objects.link(shell)
        shell.parent = root

    keep = {root, obj} | ({shell} if shell else set()) | {o for o in root.children}
    for o in bpy.context.scene.objects:
        o.select_set(o in keep)
    path = os.path.join(folder, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, path_mode="STRIP",
                             embed_textures=False, mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, bake_space_transform=True)
    with open(os.path.join(folder, name + ".material.json"), "w", encoding="utf-8") as f:
        json.dump(sidecar(settings["asset"], settings.get("foliage", False)), f, indent=2)
    print(f"EXPORTED {name}: lod0={tris(obj)} size=({(hi - lo).x * scale:.2f}, {(hi - lo).y * scale:.2f}, {height * scale:.2f})")
    for o in list(keep):
        bpy.data.objects.remove(o, do_unlink=True)


for asset, settings in plan.items():
    settings["asset"] = asset
    source = os.path.join(work, asset)
    gltf = glob.glob(os.path.join(source, "*.gltf"))[0]
    folder = os.path.join(out_root, asset)
    os.makedirs(folder, exist_ok=True)
    textures = os.path.join(source, "textures")
    colour = ("*_Color.png", "_Color.png") if settings.get("foliage") else ("*_diff_*", "_Color.jpg")
    for pattern, target in (colour, ("*_nor_gl_*", "_Normal.jpg"), ("*_arm_*", "_ORM_2k.jpg")):
        found = glob.glob(os.path.join(textures, pattern))
        shutil.copyfile(found[0], os.path.join(folder, asset + target))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=gltf)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in meshes:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for o in list(bpy.context.scene.objects):
        if o.type == "EMPTY":
            bpy.data.objects.remove(o)

    if settings.get("split") and len(meshes) > 1:
        for i, mesh in enumerate(sorted(meshes, key=lambda m: m.name), 1):
            export(mesh, f"{asset}_{i:02d}", folder, settings)
    else:
        if len(meshes) > 1:
            bpy.ops.object.join()
        export(bpy.context.view_layer.objects.active, asset, folder, settings)
