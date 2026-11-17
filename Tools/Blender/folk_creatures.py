"""The folk-tale creatures (Tools/LocalSpawn/creatures_manifest.json) made ready for Unity after LocalSpawn generated them and
Make-It-Animatable rigged them (52 Mixamo bones, T-pose rest):

- the skin checked: every vertex weighted (an unweighted one would stay behind in the T-pose while the body walks off),
  weights normalised and limited to 4 bones (what Unity skins with), tiny loose bits of mesh removed;
- the height set to the creature's own (CREATURES) and the feet put on the ground at the origin, facing +Y in Blender
  (Unity's +Z);
- the mesh named after the creature, the armature "Armature";
- exported as Assets/_Project/Art/Enemies/Downloaded/<Name>/<Name>.fbx with its BaseColor and Normal maps beside it, and a
  front/side preview render to Tools/Blender/folk_creatures_<Name>.png.

Run from the project root, after rigging (rigged FBX at Tools/LocalSpawn/Creatures/<Name>/A/rigged.fbx, or <asset>/<look>/ for a
creature given as (height, asset, look)):
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup --python Tools/Blender/folk_creatures.py -- . [Name ...]
"""
import glob
import math
import os
import shutil
import sys

import bmesh
import bpy

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["."]
ROOT = os.path.abspath(ARGS[0])
WANTED = ARGS[1:]
SOURCE = os.path.join(ROOT, "Tools/LocalSpawn/Creatures")
OUT = os.path.join(ROOT, "Assets/_Project/Art/Enemies/Downloaded")

# Metres, standing, as the game shows them (the builder scales the body to this too; the file matches it).
CREATURES = {
    "Fossegrim": 2.1,
    "Trollkjerring": 1.9,
    "Bergjutul": 3.6,
    # 2026-11-14: the Rootling's new body and three trolls of different sizes.
    "BarkRootling": 1.0,
    "StoneTroll": 3.2,
    "BogTroll": 2.4,
    "TrollRunt": 1.2,
    # 2026-11-17: Troll Valley's boss and the Moss Marsh's spirits (Docs/TrollValleyPlan.md, Docs/NextRaidResearch_2026-11-17.md).
    "Brukam": 4.6,
    "NokkOfTheBlackPool": 2.3,
    "Deildegast": 1.75,
    # 2026-11-17: the Lost, the nemesis bodies; two looks of each armour grade, so each is (height, source asset, look).
    "LostClothA": (1.8, "LostCloth", "A"),
    "LostClothB": (1.72, "LostCloth", "B"),
    "LostLeatherA": (1.84, "LostLeather", "A"),
    "LostLeatherB": (1.86, "LostLeather", "B"),
    "LostMailA": (1.86, "LostMail", "A"),
    "LostMailB": (1.9, "LostMail", "B"),
}


def spec(name):
    """(height, source asset, look): a plain height means the creature's own folder and look A."""
    entry = CREATURES[name]
    return (entry, name, "A") if isinstance(entry, (int, float)) else entry


def clean_skin(mesh, armature):
    """Every vertex weighted to at least one bone; at most 4 weights, normalised; crumbs of loose mesh gone."""
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(mesh.data)
    # The generated mesh is split along its texture seams; weld it first (UVs live on the corners, so they stay), or every
    # UV island looks like a loose crumb (the first run deleted most of each body).
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.verts.ensure_lookup_table()
    # Loose islands under 0.2% of the vertices: specks the shape model leaves floating.
    seen, islands = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, island = [v], []
        seen.add(v.index)
        while stack:
            cur = stack.pop()
            island.append(cur)
            for e in cur.link_edges:
                o = e.other_vert(cur)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        islands.append(island)
    crumbs = [v for island in islands if len(island) < len(bm.verts) * 0.002 for v in island]
    bmesh.ops.delete(bm, geom=crumbs, context='VERTS')
    # Bridges: where a hand hung against a thigh in the generated pose, the rig's T-pose stretches the faces between them
    # into sheets (the Stone Troll, 2026-11-14). No real face on these bodies has an edge near 18% of the height.
    zs = [v.co.z for v in bm.verts]
    longest = (max(zs) - min(zs)) * 0.18
    bridges = [f for f in bm.faces if any(e.calc_length() > longest for e in f.edges)]
    bmesh.ops.delete(bm, geom=bridges, context='FACES_ONLY')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    print(f"[folk] {len(bridges)} stretched bridge faces removed")
    bmesh.update_edit_mesh(mesh.data)
    bpy.ops.object.mode_set(mode='OBJECT')

    groups = {g.index: g.name for g in mesh.vertex_groups}
    bones = set(b.name for b in armature.data.bones)
    # No weight from a bone far from the vertex (2026-11-14: a few of the Stone Troll's hand vertices carried weight from
    # its spine, a metre off, and stretched into blades whenever the arms moved). Far: 30% of the body's height from the
    # bone's segment. What this leaves unweighted gets its nearest neighbour's weights below.
    from mathutils import Vector
    from mathutils.geometry import intersect_point_line
    corners = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
    reach = (max(c.z for c in corners) - min(c.z for c in corners)) * 0.3
    segments = {b.name: (armature.matrix_world @ b.head_local, armature.matrix_world @ b.tail_local) for b in armature.data.bones}
    far = 0
    for v in mesh.data.vertices:
        p = mesh.matrix_world @ v.co
        for g in list(v.groups):
            name = groups.get(g.group)
            if name not in segments or g.weight <= 0.0:
                continue
            a, b = segments[name]
            q, t = intersect_point_line(p, a, b)
            q = a if t < 0 else b if t > 1 else q
            if (p - q).length > reach:
                mesh.vertex_groups[g.group].remove([v.index])
                far += 1
    print(f"[folk] {mesh.name}: {far} weights from far bones removed")
    unweighted = [v.index for v in mesh.data.vertices if not any(g.weight > 1e-4 and groups.get(g.group) in bones for g in v.groups)]
    if unweighted:
        # Give each the weights of the nearest weighted vertex.
        weighted = [v for v in mesh.data.vertices if v.index not in set(unweighted)]
        from mathutils.kdtree import KDTree
        tree = KDTree(len(weighted))
        for i, v in enumerate(weighted):
            tree.insert(v.co, i)
        tree.balance()
        for index in unweighted:
            v = mesh.data.vertices[index]
            _, i, _ = tree.find(v.co)
            for g in weighted[i].groups:
                mesh.vertex_groups[g.group].add([index], g.weight, 'REPLACE')
    bpy.ops.object.select_all(action='DESELECT')
    mesh.select_set(True)
    bpy.ops.object.vertex_group_limit_total(limit=4)
    bpy.ops.object.vertex_group_normalize_all(lock_active=False)
    return len(crumbs), len(unweighted)


def make(name):
    height, source, look = spec(name)
    rigged = os.path.join(SOURCE, source, look, "rigged.fbx")
    if not os.path.exists(rigged):
        print(f"[folk] {name}: no rig yet at {rigged}")
        return
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=rigged, automatic_bone_orientation=False, ignore_leaf_bones=False)  # the head and toes are leaves: kept
    armature = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    # One body: the rig may come back in parts.
    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    mesh = bpy.context.view_layer.objects.active
    crumbs, fixed = clean_skin(mesh, armature)

    # Height and ground: scale the armature (the mesh rides on it), then lift the lowest point to 0.
    bpy.context.view_layer.update()
    corners = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    low, high = min(c.z for c in corners), max(c.z for c in corners)
    scale = height / max(1e-3, high - low)
    armature.scale = tuple(s * scale for s in armature.scale)
    bpy.context.view_layer.update()
    corners = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
    cx = sum(c.x for c in corners) / len(corners)
    cy = sum(c.y for c in corners) / len(corners)
    armature.location.x -= cx
    armature.location.y -= cy
    armature.location.z -= min(c.z for c in corners)
    bpy.ops.object.select_all(action='DESELECT')
    armature.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mesh.name = name
    mesh.data.name = name
    armature.name = "Armature"

    folder = os.path.join(OUT, name)
    os.makedirs(folder, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(folder, name + ".fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False, object_types={'MESH', 'ARMATURE'},
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', use_mesh_modifiers=False,
                             path_mode='STRIP', embed_textures=False)
    model = os.path.join(SOURCE, source, look, "model")
    for kind in ("BaseColor", "Normal"):
        found = glob.glob(os.path.join(model, f"*_{kind}.png"))
        if found:
            shutil.copyfile(found[0], os.path.join(folder, f"{name}_{kind}.png"))
    print(f"[folk] {name}: {len(mesh.data.polygons)} faces, {len(armature.data.bones)} bones, {height} m, "
          f"{crumbs} loose verts removed, {fixed} unweighted verts weighted")
    preview(name, mesh, folder)


def preview(name, mesh, folder):
    """Front and side, lit, with the base colour, for a first look (Tools/Blender/folk_creatures_<Name>.png)."""
    scene = bpy.context.scene
    found = glob.glob(os.path.join(folder, f"{name}_BaseColor.png"))
    material = bpy.data.materials.new(name + " Preview")
    material.use_nodes = True
    bsdf = material.node_tree.nodes["Principled BSDF"]
    if found:
        tex = material.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(found[0])
        material.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    mesh.data.materials.clear()
    mesh.data.materials.append(material)
    height = spec(name)[0]
    side = bpy.data.objects.new("Side", mesh.data)
    side.matrix_world = mesh.matrix_world.copy()
    scene.collection.objects.link(side)
    side.location.x += height * 0.75
    side.rotation_euler.z += math.radians(90)
    bpy.ops.object.camera_add(location=(height * 0.38, -height * 3.2, height * 0.55), rotation=(math.radians(88), 0, 0))
    scene.camera = bpy.context.object
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), math.radians(10), math.radians(30)))
    bpy.context.object.data.energy = 3.5
    scene.world = bpy.data.worlds.new("Grey")
    scene.world.color = (0.25, 0.27, 0.26)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 900, 900
    scene.render.filepath = os.path.join(ROOT, f"Tools/Blender/folk_creatures_{name}.png")
    bpy.ops.render.render(write_still=True)


for creature in (WANTED or list(CREATURES)):
    make(creature)
