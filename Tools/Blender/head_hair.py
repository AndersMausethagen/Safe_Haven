"""Hair for a generated creature head, built in Blender instead of generated: scalp hair cards and braids.

Long hair on a generated head came out as flat, foil-like facets at game budgets (the Trollkjerring parts test,
2026-11-16; the user: "remove hair in an edit, and add it in blender or unity"). So the head is generated bald
(LocalSpawn split_parts.py, a "head_bald" part) and the hair is made here:

- Scalp cards: strips laid over the scalp from roots on its top, sides and back. Each falls along the surface towards
  the braid on its own side (hair parted in the middle and gathered into the braids), a little above the skin, in two
  layers. Their normals are the scalp's: Unity's URP Lit shades both sides of a cull-off card with one normal, and the
  horse manes rendered black where cards faced into the neck (horse_hair.py).
- Braids: three interwoven strands round a path that starts behind an ear and drapes over the body (kept outside it),
  a leather tie near the end and a short tuft of cards below it.

The card texture is the horse one (pale strands, alpha coverage), tinted by the material. Run inside Blender (the
MCP, or exec this file) once the head and body are in place, Blender coordinates (Z up, the creature facing -Y):

    build_hair(head, body, name="Trollkjerring", tint=(0.86, 0.86, 0.84))
"""
import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

TEXTURE = r"D:/Safe Haven/Safe Haven/Assets/_Project/Art/Wildlife/Horse/Hair/Hair Card_Albedo.png"


def world_bvh(obj):
    """BVH of the object's mesh in world space (FromObject works in object space)."""
    mw = obj.matrix_world
    verts = [mw @ v.co for v in obj.data.vertices]
    return BVHTree.FromPolygons(verts, [tuple(p.vertices) for p in obj.data.polygons])


def world_bounds(obj):
    pts = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def keep_outside(p, trees, lift):
    """Push p out of (or off) the surfaces to `lift` above them. Returns (point, normal of the nearest surface)."""
    normal = Vector((0, 0, 1))
    best = None
    for tree in trees:
        loc, nrm, _, dist = tree.find_nearest(p)
        if loc is None:
            continue
        if best is None or dist < best[2]:
            best = (loc, nrm, dist)
        if (p - loc).dot(nrm) < lift:
            p = loc + nrm * lift
    if best is not None:
        normal = best[1]
    return p, normal


def _material(name, tint, cards=True, rough=0.75):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    tree.nodes.clear()
    out = tree.nodes.new("ShaderNodeOutputMaterial")
    bsdf = tree.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = rough  # matte: shiny white hair read as foil
    if tint is not None and len(tint) == 3 and cards is not None:
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(TEXTURE, check_existing=True)
        # The card texture is pale grey strands (~0.48, tinted in Unity) and BLACK where its alpha is clear: opaque hair
        # (cap, braids) showed those gaps, and the strands read dark. So: strands x tint x 1.8, and for opaque hair the
        # strands laid over a base of the tint by the texture's alpha.
        def mix_node(blend, fac=1.0):
            m = tree.nodes.new("ShaderNodeMix")
            m.data_type, m.blend_type = "RGBA", blend
            # By identifier: the Mix node has float, vector and colour sockets all named "A"/"B"/"Result", and
            # inputs["A"] is the float one (the first try rendered black: the texture never reached the colour).
            sockets = {s.identifier: s for s in m.inputs}
            sockets["Factor_Float"].default_value = fac
            return m, sockets, next(s for s in m.outputs if s.identifier == "Result_Color")
        strands, s_in, s_out = mix_node("MULTIPLY")
        s_in["B_Color"].default_value = (*(c * 3.5 for c in tint), 1.0)  # strands ~0.2 linear: x3.5 is ~0.6-0.7
        tree.links.new(tex.outputs["Color"], s_in["A_Color"])
        colour = s_out
        if not cards:
            base, b_in, b_out = mix_node("MIX")
            b_in["A_Color"].default_value = (*(c * 0.62 for c in tint), 1.0)
            tree.links.new(tex.outputs["Alpha"], b_in["Factor_Float"])
            tree.links.new(s_out, b_in["B_Color"])
            colour = b_out
        tree.links.new(colour, bsdf.inputs["Base Color"])
        if cards:
            tree.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
            if hasattr(mat, "surface_render_method"):
                mat.surface_render_method = "DITHERED"
            mat.use_backface_culling = False
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def _solid_material(name, colour, rough=0.8):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    return mat


def head_frame(head):
    """Landmarks measured on the mesh: top of the skull, the ears' height (where the head is widest: the ears stick
    out) and the centre line. A generated head may carry a neck or shoulders, so its box centre is no landmark."""
    mw = head.matrix_world
    pts = [mw @ v.co for v in head.data.vertices]
    top = max(p.z for p in pts)
    cx = sum(p.x for p in pts) / len(pts)
    upper = [p for p in pts if p.z > top - 0.6 * (top - min(q.z for q in pts))]
    ear = max(upper, key=lambda p: abs(p.x - cx))
    skull = [p for p in pts if p.z > ear.z - 0.1 * (top - ear.z)]
    cy = sum(p.y for p in skull) / len(skull)
    half_width = max(abs(p.x - cx) for p in skull if abs(p.z - ear.z) > 0.25 * (top - ear.z)) or 0.1
    return dict(top=top, ear_z=ear.z, cx=cx, cy=cy, skull=top - ear.z, half_width=half_width)


def braid_anchors(head):
    """Behind each ear, a little below it: where the braids start (left = -x, right = +x)."""
    f = head_frame(head)
    tree = world_bvh(head)
    anchors = {}
    for side in (-1, 1):
        guess = Vector((f["cx"] + side * f["half_width"] * 0.75, f["cy"] + f["half_width"] * 0.55,
                        f["ear_z"] - 0.25 * f["skull"]))
        loc, nrm, _, _ = tree.find_nearest(guess)
        anchors[side] = loc + nrm * f["skull"] * 0.03
    return anchors


def _in_scalp(centre, n, f):
    """Scalp: above the ears all round, but on the face side only above the hairline; not the ears themselves."""
    up = (centre.z - f["ear_z"]) / f["skull"]
    if abs(centre.x - f["cx"]) > f["half_width"] * 1.05:  # the ears stick out further than the skull
        return False
    if centre.y < f["cy"]:  # the face half: only above the hairline (cards grew over the cheeks before)
        return up > 0.62 and n.y > -0.92
    if centre.y > f["cy"] + 0.25 * f["half_width"]:  # the back of the head: down to the nape
        return up > -0.9
    return up > -0.1  # the sides, above the ears


def scalp_roots(head, count, seed):
    """Points on the scalp (see _in_scalp), area-weighted."""
    rng = random.Random(seed)
    f = head_frame(head)
    mw, nmat = head.matrix_world, head.matrix_world.to_3x3().inverted().transposed()
    polys = []
    for p in head.data.polygons:
        centre = mw @ p.center
        n = (nmat @ p.normal).normalized()
        if _in_scalp(centre, n, f):
            polys.append((centre, n, p.area))
    total = sum(a for _, _, a in polys)
    roots = []
    for _ in range(count):
        r, acc = rng.uniform(0, total), 0.0
        for centre, n, a in polys:
            acc += a
            if acc >= r:
                roots.append((centre, n))
                break
    return roots


def _strip(bm, uv, points, normals, width, u0, u1, normal_store):
    """A card through `points` (root first), lying on the surface (across = normal x direction)."""
    rows = len(points) - 1
    made = []
    for r, (p, n) in enumerate(zip(points, normals)):
        d = (points[min(r + 1, rows)] - points[max(r - 1, 0)]).normalized()
        across = n.cross(d).normalized()
        w = width * (1.0 - 0.55 * r / rows)  # thins to the tip
        a, b = bm.verts.new(p - across * w * 0.5), bm.verts.new(p + across * w * 0.5)
        normal_store[a] = normal_store[b] = n
        made.append((a, b))
    for r in range(rows):
        (a0, b0), (a1, b1) = made[r], made[r + 1]
        f = bm.faces.new((a0, b0, b1, a1))
        for loop, (u, v) in zip(f.loops, ((u0, 1 - r / rows), (u1, 1 - r / rows),
                                          (u1, 1 - (r + 1) / rows), (u0, 1 - (r + 1) / rows))):
            loop[uv].uv = (u, v)


def _finish(bm, name, material, parent, normal_store):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    order = list(bm.verts)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    (parent.users_collection[0] if parent else bpy.context.scene.collection).objects.link(obj)
    mesh.materials.append(material)
    for p in mesh.polygons:
        p.use_smooth = True
    if normal_store:
        loops = [normal_store.get(order[l.vertex_index], Vector((0, 0, 1))) for l in mesh.loops]
        mesh.normals_split_custom_set(loops)
    if parent:
        obj.parent = parent
        obj.matrix_parent_inverse = parent.matrix_world.inverted()
    return obj


def scalp_cards(head, body, anchors, name, tint, count=80, seed=11, rows=9):
    f = head_frame(head)
    skull = f["skull"]
    trees = [world_bvh(head)] + ([world_bvh(body)] if body else [])
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new()
    normals = {}
    for i, (root, n0) in enumerate(scalp_roots(head, count, seed)):
        layer = i % 2
        lift = skull * (0.15 + 0.12 * layer)  # above the cap (0.1 x skull)
        side = -1 if root.x < f["cx"] else 1
        target = anchors[side]
        length = (target - root).length * rng.uniform(1.0, 1.15)
        step = length / rows
        p, n = keep_outside(root + n0 * lift, trees, lift)
        pts, nrms = [p], [n]
        for _ in range(rows):
            to = target - p
            gravity = Vector((0, 0.25, -1)).normalized()
            d = (to.normalized() * 0.75 + gravity * 0.25)
            d = (d - n * d.dot(n)).normalized()  # along the surface
            p, n = keep_outside(p + d * step, trees, lift)
            pts.append(p)
            nrms.append(n)
        u0 = rng.choice((0.0, 0.25, 0.5, 0.75))
        _strip(bm, uv, pts, nrms, skull * rng.uniform(0.35, 0.55), u0, u0 + 0.25, normals)
    return _finish(bm, f"{name} Hair Cards", _material("Head Hair Cards", tint), head, normals)


def _frames(path):
    """Parallel-transport frames (tangent, normal, binormal) along a polyline."""
    t0 = (path[1] - path[0]).normalized()
    n = t0.cross(Vector((1, 0, 0)))
    if n.length < 1e-4:
        n = t0.cross(Vector((0, 1, 0)))
    n.normalize()
    out = []
    for i in range(len(path)):
        t = (path[min(i + 1, len(path) - 1)] - path[max(i - 1, 0)]).normalized()
        n = (n - t * n.dot(t)).normalized()
        out.append((t, n, t.cross(n)))
    return out


def braid_path(anchor, side, head, body, length, radius, samples=60):
    """From behind the ear, forwards over the shoulder and down the chest, kept outside head and body."""
    trees = [world_bvh(head)] + ([world_bvh(body)] if body else [])
    step = length / samples
    p = anchor.copy()
    pts = [p]
    for i in range(samples):
        t = i / samples
        # first forwards (-y) and a little outwards round the shoulder, then mostly down
        d = Vector((side * 0.15 * (1 - t), -0.9 * (1 - t) ** 2 - 0.05, -1.0)).normalized()
        p, _ = keep_outside(p + d * step, trees, radius * 1.05)
        pts.append(p)
    return pts


def braid(head, body, anchor, side, name, tint, length, radius, twists=7.0):
    path = braid_path(anchor, side, head, body, length, radius)
    frames = _frames(path)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new()
    sides = 8
    tie_at = 0.86
    for strand in range(3):
        rings = []
        for i, (p, (t, n, b)) in enumerate(zip(path, frames)):
            s = i / (len(path) - 1)
            if s > tie_at:
                break
            phase = s * twists * math.tau + strand * math.tau / 3
            taper = 1.0 - 0.45 * max(0.0, (s - 0.6) / (tie_at - 0.6))
            centre = p + (n * math.cos(phase) + b * 0.55 * math.sin(phase)) * radius * 0.42 * taper
            r = radius * 0.5 * taper
            ring = [bm.verts.new(centre + (n * math.cos(a) + b * math.sin(a)) * r)
                    for a in (k * math.tau / sides for k in range(sides))]
            rings.append(ring)
        for i in range(len(rings) - 1):
            for k in range(sides):
                f = bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
                for loop, (u, v) in zip(f.loops, ((k / sides, i * 0.08), ((k + 1) / sides, i * 0.08),
                                                  ((k + 1) / sides, (i + 1) * 0.08), (k / sides, (i + 1) * 0.08))):
                    loop[uv].uv = (u * 0.25, v)
    braid_obj = _finish(bm, f"{name} Braid {'L' if side < 0 else 'R'}", _material("Braid Hair", tint, cards=False), head, None)
    # the tie: a short leather band round the path at tie_at
    i = int(tie_at * (len(path) - 1))
    p, (t, n, b) = path[i], frames[i]
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius * 0.62, depth=radius * 0.9, location=p)
    tie = bpy.context.active_object
    tie.name = f"{name} Braid Tie {'L' if side < 0 else 'R'}"
    tie.rotation_euler = Matrix((n, b, t)).transposed().to_euler()
    tie.data.materials.append(_solid_material("Braid Tie", (0.22, 0.13, 0.07)))
    tie.parent = head
    tie.matrix_parent_inverse = head.matrix_world.inverted()
    # the tuft below the tie: a few short cards
    bm = bmesh.new()
    uvl = bm.loops.layers.uv.new()
    normals = {}
    end = path[-1]
    for k in range(5):
        a = k * math.tau / 5
        root = p + (n * math.cos(a) + b * math.sin(a)) * radius * 0.35
        tip = end + (n * math.cos(a) + b * math.sin(a)) * radius * 0.9 + t * radius * 0.6
        pts = [root.lerp(tip, s / 4) for s in range(5)]
        facing = (n * math.cos(a) + b * math.sin(a)).normalized()
        _strip(bm, uvl, pts, [facing] * 5, radius * 1.1, 0.25 * (k % 4), 0.25 * (k % 4) + 0.25, normals)
    tuft = _finish(bm, f"{name} Braid Tuft {'L' if side < 0 else 'R'}", _material("Head Hair Cards", tint), head, normals)
    return braid_obj, tie, tuft


def hair_cap(head, name, tint, lift=0.012):
    """The scalp copied a little off the skin, the strand texture on it (opaque): full cover under the cards, as game
    hair does, so no bald patches show between them. Strands run down: u goes round the head, v down from the top."""
    f = head_frame(head)
    mw, nmat = head.matrix_world, head.matrix_world.to_3x3().inverted().transposed()
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new()
    verts = {}
    for p in head.data.polygons:
        centre = mw @ p.center
        if not _in_scalp(centre, (nmat @ p.normal).normalized(), f):
            continue
        ring = []
        for vi in p.vertices:
            if vi not in verts:
                v = head.data.vertices[vi]
                n = (nmat @ v.normal).normalized()
                verts[vi] = bm.verts.new(mw @ v.co + n * f["skull"] * lift / 0.12)
            ring.append(verts[vi])
        try:
            face = bm.faces.new(ring)
        except ValueError:
            continue
        for loop in face.loops:
            co = loop.vert.co
            u = (math.atan2(co.x - f["cx"], -(co.y - f["cy"])) / math.tau + 0.5) * 6.0
            v = (f["top"] - co.z) / (f["skull"] * 1.4)
            loop[uv].uv = (u, 1.0 - v)
    return _finish(bm, f"{name} Hair Cap", _material("Hair Cap", tint, cards=False, rough=0.85), head, None)


def build_hair(head, body, name, tint=(0.86, 0.86, 0.84), cards=140, braid_length=None, braid_radius=None, seed=11):
    f = head_frame(head)
    anchors = braid_anchors(head)
    made = [hair_cap(head, name, tint), scalp_cards(head, body, anchors, name, tint, count=cards, seed=seed)]
    for side, anchor in anchors.items():
        made += braid(head, body, anchor, side, name, tint, braid_length or f["skull"] * 4.2,
                      braid_radius or f["skull"] * 0.2)
    return made


def clear_hair(name):
    for o in [o for o in bpy.data.objects if o.name.startswith(name + " Hair") or o.name.startswith(name + " Braid")]:
        bpy.data.objects.remove(o, do_unlink=True)
