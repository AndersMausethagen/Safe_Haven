"""Mane and tail hair cards for a horse breed on the game's horse rig (the Malbers horse skeleton, armature "CG").

A breed is generated without long hair (LocalSpawn draws hair as a solid lump fused to the neck) and gets its hair here:
- **Mane:** a chain of mane bones along the crest ("Mane 1".."Mane N", each a child of the neck or head bone under it,
  pointing down the side the mane falls to), and cards hanging from the crest: two layers, each card's root on the
  neck bone and its length on its mane bone, so the game's HairSway can swing the mane while the clips move the neck.
  A forelock falls forward between the ears.
- **Tail:** cards crossed round the existing tail bones (Tail01..Tail05), widening towards the end, each vertex on the
  tail bone at its place along the chain (the clips already move these; the sway adds to them).
- One object, "<Horse> Hair", skinned to the armature, with the "Hair Card" texture (hair_card_texture.py) tinted in
  the preview; in Unity it is the renderer that wears the horse's mane colour.

The crest is found on the horse's own mesh (the top of the neck per slice), so it fits any breed fitted to the rig.

Use from Blender (the live file, via MCP, or `blender -b <file.blend> --python horse_hair.py -- <horse mesh> [options]`):
    import runpy; m = runpy.run_path(".../horse_hair.py", run_name="horse_hair"); m["build"]("Pony", side=1, mane=0.42)
"""
import math
import random

import bmesh
import bpy
from mathutils import Vector

ARMATURE = "CG"
TEXTURE = r"D:/Safe Haven/Safe Haven/Assets/_Project/Art/Wildlife/Horse/Hair/Hair Card_Albedo.png"
NECK_BONES = ("Spine2", "Neck", "Neck1", "Neck2", "Head")
TAIL_BONES = ("Tail01", "Tail02", "Tail03", "Tail04", "Tail05")


def _world(arm, bone, end="head"):
    b = arm.data.bones[bone]
    return arm.matrix_world @ (b.head_local if end == "head" else b.tail_local)


def crest_line(horse, y_from, y_to, step=0.03):
    """The top of the neck: per slice along y (the horse faces -y), the highest point near the middle line."""
    co = [horse.matrix_world @ v.co for v in horse.data.vertices]
    line = []
    y = y_from
    while y <= y_to + 1e-6:
        s = [c for c in co if abs(c.y - y) < step * 0.6 and abs(c.x) < 0.04]
        if s:
            top = max(s, key=lambda c: c.z)
            line.append(Vector((0.0, y, top.z - 0.012)))
        y += step
    return line


def _nearest_bone(arm, point, names):
    def dist(n):
        a, b = _world(arm, n), _world(arm, n, "tail")
        ab = b - a
        t = max(0.0, min(1.0, (point - a).dot(ab) / max(ab.length_squared, 1e-9)))
        return (a + ab * t - point).length
    return min(names, key=dist)


def add_mane_bones(arm, crest, count, side, length):
    """Mane bones along the crest, each hanging from the crest towards `side` (+1: +x), parented to the neck bone under it."""
    names = []
    bpy.context.view_layer.objects.active = arm
    for o in bpy.context.selected_objects:
        o.select_set(False)
    arm.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    inv = arm.matrix_world.inverted()
    edit = arm.data.edit_bones
    for old in [b for b in edit if b.name.startswith("Mane ")]:
        edit.remove(old)
    for k in range(count):
        i = round(k * (len(crest) - 1) / max(1, count - 1))
        root = crest[i]
        tip = root + Vector((side * length * 0.45, 0.0, -length))
        parent = _nearest_bone(arm, root, NECK_BONES)
        bone = edit.new(f"Mane {k + 1}")
        bone.head = inv @ root
        bone.tail = inv @ tip
        bone.parent = edit[parent]
        bone.use_deform = True
        names.append((bone.name, parent, i))
    # HairSway aims each hair bone at a point: a small end bone where it ends ("Mane N End", "Tail05 End").
    for bone in [b for b in edit if b.name.startswith("Mane ")]:
        end = edit.new(bone.name + " End")
        end.head = bone.tail
        end.tail = bone.tail + (bone.tail - bone.head) * 0.2
        end.parent = bone
        end.use_deform = False
    # Tail05's own tail points sideways in this rig (bone axes from 3ds Max), so its end goes on down the chain.
    if "Tail05" in edit and "Tail04" in edit:
        if "Tail05 End" in edit:
            edit.remove(edit["Tail05 End"])
        t4, t5 = edit["Tail04"], edit["Tail05"]
        end = edit.new("Tail05 End")
        end.head = t5.head + (t5.head - t4.head)
        end.tail = end.head + (t5.head - t4.head) * 0.2
        end.parent = t5
        end.use_deform = False
    bpy.ops.object.mode_set(mode='OBJECT')
    return names


def _card(bm, uv, rows, points, widths, across, u0, u1):
    """A strip through `points` (root first), `widths` wide along `across` at each; returns its vertex rows."""
    made = []
    for r in range(rows + 1):
        p, w = points[r], widths[r]
        a = bm.verts.new(p - across * (w * 0.5))
        b = bm.verts.new(p + across * (w * 0.5))
        made.append((a, b))
    for r in range(rows):
        (a0, b0), (a1, b1) = made[r], made[r + 1]
        f = bm.faces.new((a0, b0, b1, a1))
        for loop, (u, v) in zip(f.loops, ((u0, 1 - r / rows), (u1, 1 - r / rows), (u1, 1 - (r + 1) / rows), (u0, 1 - (r + 1) / rows))):
            loop[uv].uv = (u, v)
    return made


def build(horse_name, side=1, mane=0.42, mane_bones=6, tail_length=None, tint=(0.42, 0.25, 0.12), seed=5):
    """Builds the horse's mane bones and hair. side: which side the mane falls to (+1: +x). mane: its length (m)."""
    rng = random.Random(seed)
    arm = bpy.data.objects[ARMATURE]
    horse = bpy.data.objects[horse_name]
    pose_was = arm.data.pose_position
    arm.data.pose_position = 'REST'
    bpy.context.view_layer.update()

    # The crest from just behind the poll (the head bone's start) back to the withers (over Spine2).
    poll_y = _world(arm, "Head").y + 0.03
    withers_y = _world(arm, "Spine2").y - 0.02
    crest = crest_line(horse, poll_y, withers_y)
    bones = add_mane_bones(arm, crest, mane_bones, side, mane)
    bone_at = [b[2] for b in bones]

    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    deform = bm.verts.layers.deform.verify()
    groups = {}

    def group(name):
        if name not in groups:
            groups[name] = len(groups)
        return groups[name]

    def weigh(rows, weights):
        for (a, b), w in zip(rows, weights):
            for v in (a, b):
                for name, x in w.items():
                    if x > 1e-4:
                        v[deform][group(name)] = x

    rows = 6
    # Where each card's vertices face, set as custom normals at the end. A card's own face normal points one way through
    # it, so the cards on the other side of the neck faced into it and rendered black (Unity's Lit shades both sides of a
    # double-sided card with the same normal). Hair shades as the surface it lies on instead: the mane like the neck under
    # it, the tail out from its middle, the forelock up.
    mane_rows, tail_rows, forelock_rows = [], [], []
    # The neck's surface, to drape the mane over (a card hung straight down from the crest went through the neck).
    from mathutils.bvhtree import BVHTree
    skin = bmesh.new()
    skin.from_mesh(horse.data)
    skin.transform(horse.matrix_world)
    surface = BVHTree.FromBMesh(skin)

    def drape(point, lift):
        """Onto the neck's surface where the point is inside it or near it, `lift` off it along the normal."""
        hit, normal, _, dist = surface.find_nearest(point)
        if hit is None:
            return point
        outside = (point - hit).dot(normal) > 0
        if outside and dist > lift:
            return point
        return hit + normal * lift

    # ----- Mane: two layers of cards draped from the crest down the side it falls to, short ones over the other side.
    for layer, (spacing, length_scale, out_scale, card_side) in enumerate(((0.024, 1.0, 1.0, side), (0.034, 0.8, 0.7, side),
                                                                           (0.05, 0.3, 0.5, -side))):
        y = crest[0].y
        while y <= crest[-1].y:
            i = min(range(len(crest)), key=lambda k: abs(crest[k].y - y))
            root = crest[i] + Vector((card_side * 0.01, 0.0, 0.012))
            # Shorter at the withers and near the poll, longest mid-neck.
            along = i / max(1, len(crest) - 1)
            length = mane * length_scale * (0.65 + 0.35 * math.sin(math.pi * min(1.0, 0.15 + along))) * rng.uniform(0.7, 1.2)
            out = card_side * length * 0.5 * out_scale
            fwd = rng.uniform(-0.03, 0.01)
            points = [drape(root + Vector((out * math.sin(t * math.pi / 2) * (1.0 - 0.25 * t), fwd * t, -length * t * (0.6 + 0.4 * t))),
                            0.012 + 0.035 * t)
                      for t in (r / rows for r in range(rows + 1))]
            widths = [0.11 * (1.0 - 0.3 * r / rows) for r in range(rows + 1)]
            u0 = rng.choice((0.0, 0.25, 0.5))
            made = _card(bm, uv, rows, points, widths, Vector((0, 1, 0)), u0, u0 + 0.5)
            mane_rows.extend(made)
            # The mane bone nearest along the crest; root rows on the neck bone under the crest.
            k = min(range(len(bones)), key=lambda b: abs(bone_at[b] - i))
            neck = bones[k][1]
            weigh(made, [{neck: 1.0 - s, bones[k][0]: s} for s in (min(1.0, (r / rows) * 1.6) for r in range(rows + 1))])
            y += spacing * rng.uniform(0.8, 1.2)
    # A forelock between the ears, falling forward over the brow.
    poll = crest[0] + Vector((0.0, -0.02, 0.01))
    for k in range(4):
        points = [poll + Vector((rng.uniform(-0.02, 0.02) * t, -0.12 * t, -0.2 * t * t)) for t in (r / rows for r in range(rows + 1))]
        made = _card(bm, uv, rows, points, [0.07 * (1 - 0.3 * r / rows) for r in range(rows + 1)],
                     Vector((1, 0, 0)).lerp(Vector((0, 0, 1)), 0.2).normalized(), 0.25 * k % 1.0, 0.25 * k % 1.0 + 0.5)
        weigh(made, [{"Head": 1.0}] * (rows + 1))
        forelock_rows.extend(made)

    # ----- Tail: crossed cards down the tail chain, widening to the end.
    chain = [_world(arm, n) for n in TAIL_BONES]
    end = chain[-1] + (chain[-1] - chain[-2]).normalized() * (tail_length or (chain[-1] - chain[-2]).length)
    chain.append(end)

    def along_chain(t):
        f = t * (len(chain) - 1)
        i = min(int(f), len(chain) - 2)
        return chain[i].lerp(chain[i + 1], f - i), i, f - i

    cards = 12
    for k in range(cards):
        angle = math.pi * k / cards
        across = Vector((math.cos(angle), 0.0, 0.0)) + Vector((0.0, math.sin(angle), 0.0))
        trows = 10
        points, widths, weights = [], [], []
        for r in range(trows + 1):
            t = r / trows
            p, i, f = along_chain(t)
            sway = Vector((rng.uniform(-0.015, 0.015), rng.uniform(-0.01, 0.02), 0.0)) * t
            points.append(p + sway)
            widths.append((0.08 + 0.2 * t) * rng.uniform(0.85, 1.1))
            bone = TAIL_BONES[min(i, len(TAIL_BONES) - 1)]
            weights.append({bone: 1.0} if i == len(TAIL_BONES) - 1 or f < 0.5 else
                           {bone: 1.0 - (f - 0.5), TAIL_BONES[i + 1]: f - 0.5})
        u0 = rng.choice((0.0, 0.25, 0.5))
        made = _card(bm, uv, trows, points, widths, across.normalized(), u0, u0 + 0.5)
        weigh(made, weights)
        tail_rows.extend((a, b, points[r]) for r, (a, b) in enumerate(made))

    facing = {}
    for a, b in mane_rows:
        for v in (a, b):
            facing[v] = surface.find_nearest(v.co)[1] or Vector((0, 0, 1))
    for a, b, mid in tail_rows:
        for v in (a, b):
            out = v.co - mid
            facing[v] = (out.normalized() + Vector((0, 0, 0.4))).normalized() if out.length > 1e-5 else Vector((0, 0, 1))
    for a, b in forelock_rows:
        for v in (a, b):
            facing[v] = Vector((0, -0.5, 1)).normalized()
    bm.verts.index_update()
    normals = [facing.get(v, Vector((0, 0, 1))).normalized() for v in bm.verts]
    skin.free()

    name = horse_name + " Hair"
    old = bpy.data.objects.get(name)
    if old:
        bpy.data.objects.remove(old)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for gname, gi in sorted(groups.items(), key=lambda kv: kv[1]):
        obj.vertex_groups.new(name=gname)
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.normals_split_custom_set_from_vertices([tuple(n) for n in normals])
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = obj.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    obj.data.materials.append(_material(tint))
    arm.data.pose_position = pose_was
    return {"hair": name, "cards_tris": len(mesh.polygons) * 2, "verts": len(mesh.vertices), "mane_bones": [b[:2] for b in bones],
            "crest_points": len(crest)}


def _material(tint):
    mat = bpy.data.materials.get("Hair Card") or bpy.data.materials.new("Hair Card")
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(TEXTURE, check_existing=True)
    mix = tree.nodes.new("ShaderNodeMix")
    mix.data_type = 'RGBA'
    mix.blend_type = 'MULTIPLY'
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs["B"].default_value = (*tint, 1.0)
    tree.links.new(tex.outputs["Color"], mix.inputs["A"])
    tree.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.6
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.blend_method = 'HASHED' if hasattr(mat, "blend_method") else mat.blend_method
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = 'DITHERED'
    mat.use_backface_culling = False
    return mat
