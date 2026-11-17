"""The Lost (the nemesis bodies, Tools/LocalSpawn/creatures_manifest.json: LostCloth, LostLeather, LostMail, looks A and B) made
ready for Unity, with their garments as meshes of their own (the user, 2026-11-17: "separate the armor or cloth only, not the limbs.
and give it realistic physics"):

- in: the whole take (<look>/model/*.glb), the garments assembled onto it by LocalSpawn's parts tools
  (<look>/assembled/assembled.glb: "body" with the faces under each garment removed, and "skirt"/"garment"/"helm"), and the whole
  take rigged by Make-It-Animatable (<look>/rigged.fbx: the same vertices in the same order, reposed to its T-pose rest);
- the assembled meshes are carried into the rig's pose: each is bound to the whole take (Surface Deform) and the whole take's
  vertices are moved to the rigged ones (vertex i is vertex i: checked on the legs, which the T-pose leaves where they were);
- weights copied from the rigged body (nearest face, interpolated); a helmet is the head's alone, rigid;
- skin cleaned as Tools/Blender/folk_creatures.py does (welded, crumbs removed, at most 4 weights, normalised), the height set and
  the feet put on the ground at the origin;
- exported as Assets/_Project/Art/Enemies/Downloaded/<Name>/<Name>.fbx: meshes <Name> (the body) and <Name>_<Part>, each with its
  own material of the same name and its BaseColor and Normal maps beside the FBX; a preview to Tools/Blender/lost_ones_<Name>.png.

In Unity the hanging garments get Cloth (pinned at the belt, free at the hem, colliding with the legs).

Run from the project root, after assembling and rigging:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup --python Tools/Blender/lost_ones.py -- . [Name ...]
"""
import glob
import math
import os
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["."]
ROOT = os.path.abspath(ARGS[0])
WANTED = ARGS[1:]
SOURCE = os.path.join(ROOT, "Tools/LocalSpawn/Creatures")
OUT = os.path.join(ROOT, "Assets/_Project/Art/Enemies/Downloaded")

# Name: (asset, look, height in metres).
LOOKS = {
    "LostClothA": ("LostCloth", "A", 1.8),
    "LostClothB": ("LostCloth", "B", 1.72),
    "LostLeatherA": ("LostLeather", "A", 1.84),
    "LostLeatherB": ("LostLeather", "B", 1.86),
    "LostMailA": ("LostMail", "A", 1.86),
    "LostMailB": ("LostMail", "B", 1.9),
}
PARTS = ("skirt", "garment", "helm")
HEAD = "mixamorig:Head"


def imported(load):
    before = set(bpy.data.objects)
    load()
    return [o for o in bpy.data.objects if o not in before]


def world(obj):
    m = obj.matrix_world
    return np.array([tuple(m @ v.co) for v in obj.data.vertices])


def similarity(src, dst):
    """Scale, rotation and translation taking src onto dst (Umeyama), and the median distance left."""
    mu_s, mu_d = src.mean(0), dst.mean(0)
    a, b = src - mu_s, dst - mu_d
    u, d, vt = np.linalg.svd(b.T @ a / len(src))
    s = np.eye(3)
    if np.linalg.det(u) * np.linalg.det(vt) < 0:
        s[2, 2] = -1
    r = u @ s @ vt
    scale = np.trace(np.diag(d) @ s) / (a ** 2).sum(1).mean()
    t = mu_d - scale * r @ mu_s
    m = np.eye(4)
    m[:3, :3] = scale * r
    m[:3, 3] = t
    left = np.median(np.linalg.norm((scale * (r @ src.T)).T + t - dst, axis=1))
    return Matrix(m.tolist()), left


def apply_transform(objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)


def weld_and_clean(mesh):
    """Welded along its texture seams, specks under 0.2% of its vertices removed. Returns how many vertices went."""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    bm = bmesh.from_edit_mesh(mesh.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.verts.ensure_lookup_table()
    seen, islands = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack, island = [v], []
        seen.add(v.index)
        while stack:
            x = stack.pop()
            island.append(x)
            for e in x.link_edges:
                y = e.other_vert(x)
                if y.index not in seen:
                    seen.add(y.index)
                    stack.append(y)
        islands.append(island)
    least = max(8, int(len(bm.verts) * 0.002))
    crumbs = [v for island in islands if len(island) < least for v in island]
    if crumbs:
        bmesh.ops.delete(bm, geom=crumbs, context='VERTS')
    bmesh.update_edit_mesh(mesh.data)
    bpy.ops.object.mode_set(mode='OBJECT')
    return len(crumbs)


def cut_slivers(mesh, before, tall, most=2.5):
    """Faces whose longest edge grew more than <most> times (and past 4% of the height) between the two poses. Returns how many went."""
    after = world(mesh)
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    bm.verts.ensure_lookup_table()
    torn = []
    for f in bm.faces:
        idx = [v.index for v in f.verts]
        grew = 0.0
        longest = 0.0
        for a, b in zip(idx, idx[1:] + idx[:1]):
            was = np.linalg.norm(before[a] - before[b])
            now = np.linalg.norm(after[a] - after[b])
            longest = max(longest, now)
            grew = max(grew, now / max(was, 1e-5))
        if grew > most and longest > tall * 0.04:
            torn.append(f)
    bmesh.ops.delete(bm, geom=torn, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bm.to_mesh(mesh.data)
    bm.free()
    mesh.data.update()
    return len(torn)


def open_hem(mesh):
    """A generated garment is a closed shell: the cap across its hem (faces looking down, in its lowest fifth) goes, so it hangs
    open over the legs as cloth does. Returns how many faces went."""
    m = mesh.matrix_world
    low = min((m @ v.co).z for v in mesh.data.vertices)
    high = max((m @ v.co).z for v in mesh.data.vertices)
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    rotation = m.to_3x3().normalized()
    cap = [f for f in bm.faces if (rotation @ f.normal).z < -0.6 and (m @ f.calc_center_median()).z < low + (high - low) * 0.2]
    bmesh.ops.delete(bm, geom=cap, context='FACES')
    bm.to_mesh(mesh.data)
    bm.free()
    mesh.data.update()
    return len(cap)


def save_maps(mesh, folder, name):
    """The mesh's base colour and normal images as <name>_BaseColor.png and <name>_Normal.png."""
    made = []
    for slot in mesh.material_slots:
        material = slot.material
        if material is None or not material.use_nodes:
            continue
        for node in material.node_tree.nodes:
            if node.type != 'TEX_IMAGE' or node.image is None:
                continue
            linked = [link.to_node for link in node.outputs["Color"].links]
            kind = "Normal" if any(n.type == 'NORMAL_MAP' for n in linked) else "BaseColor" if any(n.type == 'BSDF_PRINCIPLED' for n in linked) else None
            if kind is None or kind in made:
                continue
            path = os.path.join(folder, f"{name}_{kind}.png")
            image = node.image
            image.filepath_raw = path
            image.file_format = 'PNG'
            image.save()
            made.append(kind)
        material.name = name
        break
    return made


def make(name):
    asset, look, height = LOOKS[name]
    folder = os.path.join(SOURCE, asset, look)
    whole_path = (glob.glob(os.path.join(folder, "model", "*.glb")) or [None])[0]
    assembled_path = os.path.join(folder, "assembled", "assembled.glb")
    rigged_path = os.path.join(folder, "rigged.fbx")
    for path in (whole_path, assembled_path, rigged_path):
        if path is None or not os.path.exists(path):
            print(f"[lost] {name}: missing {path}")
            return

    bpy.ops.wm.read_factory_settings(use_empty=True)
    whole = next(o for o in imported(lambda: bpy.ops.import_scene.gltf(filepath=whole_path)) if o.type == 'MESH')
    pieces = [o for o in imported(lambda: bpy.ops.import_scene.gltf(filepath=assembled_path)) if o.type == 'MESH']
    rig = imported(lambda: bpy.ops.import_scene.fbx(filepath=rigged_path, automatic_bone_orientation=False, ignore_leaf_bones=False))
    armature = next(o for o in rig if o.type == 'ARMATURE')
    rigged = next(o for o in rig if o.type == 'MESH')
    if len(whole.data.vertices) != len(rigged.data.vertices):
        print(f"[lost] {name}: the rig has {len(rigged.data.vertices)} vertices, the whole take {len(whole.data.vertices)}: not the same mesh")
        return

    # Into the rig's frame, matched on the legs (the lowest third), which its T-pose leaves where they were.
    w, r = world(whole), world(rigged)
    low = w[:, 2] < w[:, 2].min() + (w[:, 2].max() - w[:, 2].min()) * 0.33
    move, left = similarity(w[low], r[low])
    tall = r[:, 2].max() - r[:, 2].min()
    print(f"[lost] {name}: legs matched to {left:.4f} of {tall:.3f} (vertex order kept)")
    if left > tall * 0.03:
        print(f"[lost] {name}: the legs don't match: vertex order not kept, stopping")
        return
    for o in [whole] + pieces:
        placed = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = move @ placed
    apply_transform([whole] + pieces)

    # Below the hips (all of a skirt; a dress's or coat's skirt, but not its sleeves) a garment never follows the arms: in the
    # concept's A-pose the hands hang beside it, and bound to them it was dragged up into strings when the arms rose.
    arm_groups = {g.index for g in rigged.vertex_groups if any(k in g.name for k in ("Arm", "Hand", "Shoulder"))}
    arm_weight = np.zeros(len(rigged.data.vertices))
    for v in rigged.data.vertices:
        arm_weight[v.index] = sum(g.weight for g in v.groups if g.group in arm_groups)
    free = np.where(arm_weight < 0.2)[0]
    hips_bone = armature.data.bones.get("mixamorig:Hips")
    hips_z = (armature.matrix_world @ hips_bone.head_local).z if hips_bone else r[:, 2].min() + tall * 0.53
    band = free[np.abs(r[free, 2] - hips_z) < tall * 0.05]
    centre_x = r[band, 0].mean() if len(band) else r[:, 0].mean()
    hip_half = np.abs(r[band, 0] - centre_x).max() if len(band) else tall * 0.12
    from mathutils.kdtree import KDTree
    posed = world(whole)
    clear = KDTree(len(free))
    for k, i in enumerate(free):
        clear.insert(Vector(posed[i]), k)
    clear.balance()
    lower = {}
    for piece in pieces:
        kind = piece.name.split(".")[0]
        if kind not in ("skirt", "garment"):
            continue
        cos = world(piece)
        picked = [i for i, co in enumerate(cos) if kind == "skirt" or (co[2] < hips_z and abs(co[0] - centre_x) < hip_half * 1.25)]
        lower[piece] = picked
        group = piece.vertex_groups.new(name="Upper")
        group.add([i for i in range(len(cos)) if i not in set(picked)], 1.0, 'REPLACE')
        # By group, not index: cutting slivers renumbers the vertices.
        piece.vertex_groups.new(name="Lower").add(picked, 1.0, 'REPLACE')

    concept_pose = {p: world(p) for p in pieces}

    # Each piece follows the whole take into the T-pose: bound to it (Surface Deform), or where the generated surface won't bind
    # (edges shared by three faces), moved as its nearest vertex of the whole take moves.
    unbound = []
    for piece in pieces:
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = piece
        piece.select_set(True)
        deform = piece.modifiers.new("Follow", 'SURFACE_DEFORM')
        deform.target = whole
        if piece in lower:
            deform.vertex_group = "Upper"
        bpy.ops.object.surfacedeform_bind(modifier=deform.name)
        if not deform.is_bound:
            piece.modifiers.remove(deform)
            unbound.append(piece)
    before = world(whole)
    if unbound:
        from mathutils.kdtree import KDTree
        tree = KDTree(len(before))
        for i, co in enumerate(before):
            tree.insert(Vector(co), i)
        tree.balance()
        for piece in unbound:
            for v in piece.data.vertices:
                _, i, _ = tree.find(v.co)
                v.co += Vector(r[i] - before[i])
            piece.data.update()
        print(f"[lost] {name}: moved by nearest vertex (no surface bind): {[p.name for p in unbound]}")
    # Below the hips: moved as the nearest vertex of the body that no arm moves (the legs and hips barely move into the T-pose).
    for piece, picked in lower.items():
        for i in picked:
            v = piece.data.vertices[i]
            _, k, _ = clear.find(v.co)
            j = free[k]
            v.co += Vector(r[j] - before[j])
        piece.data.update()
    for i, v in enumerate(whole.data.vertices):
        v.co = Vector(r[i])
    whole.data.update()
    for piece in pieces:
        if piece in unbound:
            continue
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = piece
        piece.select_set(True)
        bpy.ops.object.modifier_apply(modifier="Follow")

    # Faces the generator stretched across a gap in the concept's pose (an arm to the waist, a sleeve to the skirt) became long slivers
    # when the arms rose: nothing a body or a garment wears stretches like that, so they go.
    torn = sum(cut_slivers(p, concept_pose[p], tall) for p in pieces)

    # Weights from the rigged body; a helmet rides the head alone.
    soft = [p for p in pieces if p.name.split(".")[0] != "helm"]
    bpy.ops.object.select_all(action='DESELECT')
    for p in soft:
        p.select_set(True)
    rigged.select_set(True)
    bpy.context.view_layer.objects.active = rigged
    bpy.ops.object.data_transfer(use_reverse_transfer=False, data_type='VGROUP_WEIGHTS', vert_mapping='POLYINTERP_NEAREST',
                                 layers_select_src='ALL', layers_select_dst='NAME')
    # Below the hips, the weights of the nearest body vertex no arm moves, not of a hand that hung beside it.
    rest = KDTree(len(free))
    for k, i in enumerate(free):
        rest.insert(Vector(r[i]), k)
    rest.balance()
    names = {g.index: g.name for g in rigged.vertex_groups}
    for piece in lower:
        mark = piece.vertex_groups["Lower"].index
        picked = [v.index for v in piece.data.vertices if any(g.group == mark and g.weight > 0.5 for g in v.groups)]
        piece.vertex_groups.remove(piece.vertex_groups["Upper"])
        piece.vertex_groups.remove(piece.vertex_groups["Lower"])
        for i in picked:
            v = piece.data.vertices[i]
            for g in list(v.groups):
                piece.vertex_groups[g.group].remove([i])
            _, k, _ = rest.find(v.co)
            for g in rigged.data.vertices[free[k]].groups:
                target = piece.vertex_groups.get(names[g.group]) or piece.vertex_groups.new(name=names[g.group])
                target.add([i], g.weight, 'REPLACE')
    for p in pieces:
        if p in soft:
            continue
        p.vertex_groups.clear()
        group = p.vertex_groups.new(name=HEAD)
        group.add(range(len(p.data.vertices)), 1.0, 'REPLACE')

    bpy.data.objects.remove(whole, do_unlink=True)
    bpy.data.objects.remove(rigged, do_unlink=True)
    opened = sum(open_hem(p) for p in pieces if p.name.split(".")[0] in ("skirt", "garment"))
    crumbs = 0
    for p in pieces:
        crumbs += weld_and_clean(p)
        p.parent = armature
        p.matrix_parent_inverse = armature.matrix_world.inverted()
        bind = p.modifiers.new("Armature", 'ARMATURE')
        bind.object = armature
        bpy.ops.object.select_all(action='DESELECT')
        bpy.context.view_layer.objects.active = p
        p.select_set(True)
        bpy.ops.object.vertex_group_limit_total(limit=4)
        bpy.ops.object.vertex_group_normalize_all(lock_active=False)

    # Height and ground, as the folk creatures.
    bpy.context.view_layer.update()
    corners = np.vstack([world(p) for p in pieces])
    scale = height / max(1e-3, corners[:, 2].max() - corners[:, 2].min())
    armature.scale = tuple(s * scale for s in armature.scale)
    bpy.context.view_layer.update()
    corners = np.vstack([world(p) for p in pieces])
    armature.location.x -= corners[:, 0].mean()
    armature.location.y -= corners[:, 1].mean()
    armature.location.z -= corners[:, 2].min()
    bpy.ops.object.select_all(action='DESELECT')
    armature.select_set(True)
    for p in pieces:
        p.select_set(True)
    bpy.context.view_layer.objects.active = armature
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    out = os.path.join(OUT, name)
    os.makedirs(out, exist_ok=True)
    report = []
    for p in pieces:
        part = p.name.split(".")[0]
        mesh_name = name if part == "body" else f"{name}_{part.capitalize()}"
        p.name = mesh_name
        p.data.name = mesh_name
        maps = save_maps(p, out, mesh_name)
        report.append(f"{mesh_name} {len(p.data.polygons)} faces {'+'.join(maps)}")
    armature.name = "Armature"
    bpy.ops.object.select_all(action='DESELECT')
    armature.select_set(True)
    for p in pieces:
        p.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, name + ".fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False, object_types={'MESH', 'ARMATURE'},
                             add_leaf_bones=False, bake_anim=False, mesh_smooth_type='FACE', use_mesh_modifiers=False,
                             path_mode='STRIP', embed_textures=False)
    print(f"[lost] {name}: {height} m, {len(armature.data.bones)} bones, {crumbs} loose verts removed, {opened} hem faces opened, {torn} stretched faces cut; " + "; ".join(report))
    preview(name, pieces, height)


def preview(name, pieces, height):
    """Front and side, lit, for a first look."""
    scene = bpy.context.scene
    for p in pieces:
        side = bpy.data.objects.new(p.name + " Side", p.data)
        side.matrix_world = Matrix.Translation((height * 0.75, 0, 0)) @ Matrix.Rotation(math.radians(90), 4, 'Z') @ p.matrix_world
        scene.collection.objects.link(side)
    bpy.ops.object.camera_add(location=(height * 0.38, -height * 3.2, height * 0.55), rotation=(math.radians(88), 0, 0))
    scene.camera = bpy.context.object
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), math.radians(10), math.radians(30)))
    bpy.context.object.data.energy = 3.5
    scene.world = bpy.data.worlds.new("Grey")
    scene.world.color = (0.25, 0.27, 0.26)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 900, 900
    scene.render.filepath = os.path.join(ROOT, f"Tools/Blender/lost_ones_{name}.png")
    bpy.ops.render.render(write_still=True)


for look in (WANTED or list(LOOKS)):
    make(look)
