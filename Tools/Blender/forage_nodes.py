"""The first things the player gathers in Safe Haven, modelled and textured in Blender: Small Rocks, Sticks and the Berry Bush.

They replace the sphere and cylinder stand-ins of the Small Rocks, Sticks and Berries nodes (and the dropped Small Rocks and
Sticks), and are made like the Poly Haven scans the Haven is dressed with: real-size shapes whose surfaces come from CC0
scanned textures (rock_boulder_dry, bark_brown_02), baked with prop_bake.py into Albedo, Normal, ORM, Height (parallax)
and BlendMask (where moss grows) maps.

- Small Rocks: three clusters of split grey field stones, 3 to 5 stones, about 0.55 x 0.5 m. Sculpted high (split
  planes and noise on a subdivided sphere), baked onto a low mesh of about 250 triangles a stone.
- Sticks: three heaps of fallen, forked branches, 3 to 5 sticks 0.35 to 0.6 m long, with broken, pale ends; half again
  as thick as real twigs, so they read from the game's camera.
- Berry Bush: two wild currant shrubs, about 1.3 m across and 0.9 m high: arching woody stems, a leafy crown and strings
  of red berries on its top, where the game's camera sees them.

Writes Assets/_Project/Art/Gathering/<Asset>/, a preview render of all variants to the path given, and Forage.blend
beside this script.

Run: blender -b --factory-startup --python Tools/Blender/forage_nodes.py -- <project root> <preview.png> [asset ...]
(assets: rocks, sticks, bush; all when none are named).
In an open Blender (the MCP), runpy.run_path(this file, run_name="forage_lab") loads the functions without running main().
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prop_bake import *  # noqa: E402,F401,F403  (helpers, materials, bake)
from prop_bake import ROOT, args  # noqa: E402

PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "forage_preview.png")
WANTED = set(args[2:]) or {"rocks", "sticks", "bush"}


# ----- Materials for the bake ---------------------------------------------------------------------------------------

def leaf_material():
    """Currant leaves: fresh green, lighter along the midrib and veins, darker at the rim; each leaf a slightly different
    green (its "sh_leafvar" face attribute), a few turning yellow. U runs across the leaf (0 to 1), V from stalk to tip."""
    material, tree, bsdf = new_material("Currant Leaf (bake)")
    links = tree.links
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-1200, 0)
    split = node(tree, "ShaderNodeSeparateXYZ", (-1000, 0))
    links.new(uv.outputs["UV"], split.inputs["Vector"])
    # Distance from the midrib (0 at the middle, 1 at the rim).
    centred = node(tree, "ShaderNodeMath", (-800, 100))
    centred.operation = 'SUBTRACT'
    centred.inputs[1].default_value = 0.5
    links.new(split.outputs["X"], centred.inputs[0])
    across = node(tree, "ShaderNodeMath", (-600, 100))
    across.operation = 'ABSOLUTE'
    links.new(centred.outputs["Value"], across.inputs[0])
    across2 = node(tree, "ShaderNodeMath", (-450, 100))
    across2.operation = 'MULTIPLY'
    across2.inputs[1].default_value = 2.0
    links.new(across.outputs["Value"], across2.inputs[0])
    # Veins: lines slanting out from the midrib towards the tip.
    slant = node(tree, "ShaderNodeMath", (-600, -150))
    slant.operation = 'MULTIPLY_ADD'
    slant.inputs[1].default_value = -0.9
    links.new(across.outputs["Value"], slant.inputs[0])
    links.new(split.outputs["Y"], slant.inputs[2])
    veins = node(tree, "ShaderNodeMath", (-400, -150))
    veins.operation = 'PINGPONG'
    veins.inputs[1].default_value = 0.06
    links.new(slant.outputs["Value"], veins.inputs[0])
    vein_mask = ramp(tree, (-200, -150), [(0.0, (1, 1, 1)), (0.012, (0, 0, 0))])
    links.new(veins.outputs["Value"], vein_mask.inputs["Fac"])
    midrib = ramp(tree, (-200, 150), [(0.0, (1, 1, 1)), (0.06, (0, 0, 0))])
    links.new(across2.outputs["Value"], midrib.inputs["Fac"])

    attribute = tree.nodes.new("ShaderNodeAttribute")
    attribute.attribute_name = "sh_leafvar"
    attribute.attribute_type = 'GEOMETRY'
    attribute.location = (-800, 500)
    greens = ramp(tree, (-500, 500), [(0.0, (0.05, 0.11, 0.025)), (0.5, (0.09, 0.17, 0.035)), (0.86, (0.15, 0.22, 0.045)),
                                      (0.93, (0.32, 0.3, 0.06)), (1.0, (0.45, 0.26, 0.05))])
    links.new(attribute.outputs["Fac"], greens.inputs["Fac"])
    # Blotchy within a leaf, darker at the rim.
    blotch = node(tree, "ShaderNodeTexNoise", (-500, 800), Scale=12.0, Detail=4.0)
    links.new(uv.outputs["UV"], blotch.inputs["Vector"])
    rim = ramp(tree, (-200, 350), [(0.55, (1, 1, 1)), (1.0, (0.62, 0.62, 0.55))])
    links.new(across2.outputs["Value"], rim.inputs["Fac"])
    base = mix(tree, (0, 500), greens.outputs["Color"], rim.outputs["Color"], value(tree, (-200, 600), 1.0), blend='MULTIPLY')
    shade = ramp(tree, (-200, 800), [(0.3, (0.85, 0.85, 0.85)), (0.7, (1.1, 1.1, 1.0))])
    links.new(blotch.outputs["Fac"], shade.inputs["Fac"])
    base = mix(tree, (200, 600), base, shade.outputs["Color"], value(tree, (0, 750), 1.0), blend='MULTIPLY')
    light = node(tree, "ShaderNodeRGB", (200, 250))
    light.outputs[0].default_value = (0.13, 0.2, 0.05, 1)
    vein_soft = node(tree, "ShaderNodeMath", (200, 150))
    vein_soft.operation = 'MULTIPLY'
    vein_soft.inputs[1].default_value = 0.45
    links.new(vein_mask.outputs["Color"], vein_soft.inputs[0])
    veined = mix(tree, (400, 300), base, light.outputs[0], vein_soft.outputs["Value"])
    ribbed = mix(tree, (600, 300), veined, light.outputs[0], midrib.outputs["Color"])
    links.new(ribbed, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.72
    bsdf.inputs["Specular IOR Level"].default_value = 0.35
    height = node(tree, "ShaderNodeMath", (400, -300))
    height.operation = 'MAXIMUM'
    links.new(vein_mask.outputs["Color"], height.inputs[0])
    links.new(midrib.outputs["Color"], height.inputs[1])
    bump = node(tree, "ShaderNodeBump", (700, -300), Strength=0.3, Distance=0.001)
    bump.invert = True
    links.new(height.outputs["Value"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def berry_material():
    """Ripe red currants: deep, glossy red with a darker end where the flower was."""
    material, tree, bsdf = new_material("Currant (bake)")
    links = tree.links
    attribute = tree.nodes.new("ShaderNodeAttribute")
    attribute.attribute_name = "sh_leafvar"
    attribute.attribute_type = 'GEOMETRY'
    attribute.location = (-600, 0)
    reds = ramp(tree, (-300, 0), [(0.0, (0.42, 0.02, 0.015)), (0.6, (0.62, 0.035, 0.02)), (1.0, (0.75, 0.08, 0.03))])
    links.new(attribute.outputs["Fac"], reds.inputs["Fac"])
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-600, -300)
    split = node(tree, "ShaderNodeSeparateXYZ", (-400, -300))
    links.new(uv.outputs["UV"], split.inputs["Vector"])
    tip = ramp(tree, (-200, -300), [(0.0, (0.35, 0.3, 0.3)), (0.12, (1, 1, 1))])
    links.new(split.outputs["Y"], tip.inputs["Fac"])
    coloured = mix(tree, (100, 0), reds.outputs["Color"], tip.outputs["Color"], value(tree, (-100, 150), 1.0), blend='MULTIPLY')
    links.new(coloured, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.22
    return material


# ----- Small Rocks --------------------------------------------------------------------------------------------------

def rock_cluster(name, layout, seed, coll):
    """Stones placed by layout: (x, y, size x, y, z, turn degrees)."""
    rng = random.Random(seed)
    lows, highs = [], []
    for i, (x, y, sx, sy, sz, turn) in enumerate(layout):
        low, high = stone(f"{name} {i}", (sx, sy, sz), rng, coll)
        placed = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(turn), 4, 'Z')
        for obj in (low, high):
            obj.data.transform(placed)
        lows.append(low)
        highs.append(high)
    return join(lows, name), join(highs, f"{name} High")


ROCK_LAYOUTS = {
    "A": [(-0.13, 0.0, 0.11, 0.125, 0.09, 10), (0.01, 0.12, 0.13, 0.12, 0.085, 55), (0.15, -0.01, 0.15, 0.12, 0.1, 100)],
    "B": [(-0.08, -0.04, 0.17, 0.14, 0.12, 30), (0.13, 0.08, 0.12, 0.1, 0.08, 75), (0.1, -0.14, 0.09, 0.08, 0.065, 140),
          (-0.18, 0.15, 0.08, 0.07, 0.06, 200)],
    "C": [(-0.16, -0.06, 0.1, 0.09, 0.075, 15), (0.0, 0.05, 0.12, 0.11, 0.09, 60), (0.16, 0.0, 0.1, 0.085, 0.07, 120),
          (-0.04, -0.16, 0.08, 0.075, 0.06, 170), (0.08, 0.18, 0.075, 0.07, 0.055, 230)],
}


def build_rocks(coll):
    pairs = [rock_cluster(f"Small Rocks {key}", layout, 31 + i, coll) for i, (key, layout) in enumerate(ROCK_LAYOUTS.items())]
    material = rock_material()
    for _, high in pairs:
        high.data.materials.append(material)
    return [low for low, _ in pairs], [high for _, high in pairs]


# ----- Sticks -------------------------------------------------------------------------------------------------------

def stick(bm, uv, start, direction, length, radius, rng, lift, forks):
    """One fallen branch lying on the ground, tapering, with side twigs, broken off at both ends."""
    segments = max(6, int(length / 0.04))
    points = bent_path(start + Vector((0, 0, radius)), direction, length, segments, rng.uniform(-0.08, 0.08), rng, lift=lift)
    radii = [radius * (1.0 - 0.45 * i / segments) for i in range(segments + 1)]
    tube(bm, points, radii, 8, uv, jagged=rng.uniform(0.3, 1.0), rng=rng)
    for _ in range(forks):
        at = rng.randint(segments // 4, segments * 3 // 4)
        base = points[at]
        along = (points[at + 1] - points[at]).normalized()
        side = Vector((-along.y, along.x, 0)).normalized() * rng.choice((-1, 1))
        twig_dir = (along * 0.75 + side * 0.65).normalized()
        twig_len = rng.uniform(0.1, 0.2)
        twig_r = radii[at] * rng.uniform(0.35, 0.5)
        twig_segments = max(4, int(twig_len / 0.03))
        twig = bent_path(base, twig_dir, twig_len, twig_segments, rng.uniform(-0.15, 0.15), rng)
        # The twig rests on the ground too: lower its far end to the ground.
        for i, p in enumerate(twig):
            t = i / twig_segments
            p.z = base.z * (1 - t) + twig_r * t
        twig_radii = [twig_r * (1.0 - 0.6 * i / twig_segments) for i in range(twig_segments + 1)]
        tube(bm, twig, twig_radii, 6, uv, jagged=0.6, rng=rng)


STICK_LAYOUTS = {
    # (start x, y, direction degrees, length, radius, lift, forks)
    "A": [(-0.2, -0.22, 62, 0.46, 0.016, 0.0, 1), (-0.27, 0.05, 15, 0.5, 0.018, 0.025, 0), (0.12, -0.25, 105, 0.42, 0.014, 0.03, 1)],
    "B": [(-0.3, -0.1, 20, 0.6, 0.02, 0.0, 1), (-0.15, -0.3, 75, 0.52, 0.017, 0.035, 1), (0.05, 0.25, -40, 0.44, 0.015, 0.04, 0),
          (0.25, -0.2, 120, 0.36, 0.012, 0.03, 1)],
    "C": [(-0.28, -0.18, 35, 0.55, 0.019, 0.0, 0), (-0.3, 0.0, 10, 0.58, 0.017, 0.03, 1), (-0.25, 0.2, -12, 0.5, 0.016, 0.045, 0),
          (-0.05, -0.3, 80, 0.48, 0.014, 0.05, 1), (0.2, -0.25, 110, 0.4, 0.012, 0.04, 0)],
}


def build_sticks(coll):
    bark = bark_material()
    wood = broken_wood_material()
    objects = []
    for i, (key, layout) in enumerate(STICK_LAYOUTS.items()):
        rng = random.Random(70 + i)
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("grain")
        for (x, y, degrees, length, radius, lift, forks) in layout:
            d = Vector((math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0))
            # Half again as thick as real twigs, so a heap reads from the game's camera 15 m up.
            stick(bm, uv, Vector((x, y, 0)), d, length, radius * 1.5, rng, lift * 1.5, forks)
        obj = mesh_object(f"Sticks {key}", bm, coll, (bark, wood))
        centre_on_ground(obj)
        for poly in obj.data.polygons:
            poly.use_smooth = poly.material_index == 0
        objects.append(obj)
    return objects


# ----- Berry Bush ---------------------------------------------------------------------------------------------------

LEAF_OUTLINE = [(0.0, 0.0), (0.34, 0.28), (0.36, 0.62), (0.0, 1.0)]  # half a leaf: (across, along)


def leaf(bm, uv, var, base, direction, up, size, value, rng):
    """One leaf: an ovate blade folded a little along its midrib and curling down at the tip."""
    direction = direction.normalized()
    side = direction.cross(up).normalized()
    normal = side.cross(direction).normalized()
    fold = rng.uniform(0.15, 0.35)
    curl = rng.uniform(0.05, 0.2)
    verts = {}
    for sign in (-1, 1):
        for i, (across, along) in enumerate(LEAF_OUTLINE):
            if sign == 1 and (i == 0 or i == len(LEAF_OUTLINE) - 1):
                continue
            a = across * sign
            p = base + direction * along * size + side * a * size
            p += normal * (abs(a) * fold - along * along * curl) * size
            verts[(sign, i)] = bm.verts.new(p)
    mid = bm.verts.new(base + direction * 0.5 * size)
    for i in (0, len(LEAF_OUTLINE) - 1):
        verts[(1, i)] = verts[(-1, i)]
    faces = []
    for sign in (-1, 1):
        for i in range(len(LEAF_OUTLINE) - 1):
            a, b = verts[(sign, i)], verts[(sign, i + 1)]
            tri = (a, b, mid) if sign == 1 else (b, a, mid)
            try:
                f = bm.faces.new(tri)
            except ValueError:
                continue
            f.material_index = 1
            f[var] = value
            f.normal_update()
            # Leaves are one-sided in the game: each faces up and out, towards the camera above.
            if f.normal.dot(normal) < 0:
                f.normal_flip()
            for loop in f.loops:
                if loop.vert is mid:
                    loop[uv].uv = (0.5, 0.5)
                else:
                    key = next(k for k, v in verts.items() if v is loop.vert)
                    across, along = LEAF_OUTLINE[key[1]]
                    loop[uv].uv = (0.5 + across * key[0], along)
            faces.append(f)
    return faces


def berry_cluster(bm, uv, var, at, rng, count):
    """A short hanging string of currants."""
    hang = Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), -0.7)).normalized()
    for k in range(count):
        r = rng.uniform(0.019, 0.023)  # a little large for currants, so they read from the game's camera
        centre = at + hang * (0.015 + k * 0.03) + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) * 0.009
        sphere = bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=r,
                                           matrix=Matrix.Translation(centre))
        value = rng.random()
        faces = {f for v in sphere["verts"] for f in v.link_faces}
        for f in faces:
            f.material_index = 2
            f[var] = value
            for loop in f.loops:
                d = (loop.vert.co - centre).normalized()
                loop[uv].uv = (0.5 + math.atan2(d.y, d.x) / (2 * math.pi), (d.z + 1) / 2)


def bush(name, seed, spread, height, stems, coll, materials, shell_leaves=1100, clusters=40):
    """A wild currant: arching woody stems with side shoots, leafy all over a rounded crown (most leaves on its outer
    shell, where the camera above sees them), and strings of berries resting on the outside."""
    rng = random.Random(seed)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("grain")
    var = bm.faces.layers.float.new("sh_leafvar")
    leaf_points = []
    for s in range(stems):
        turn = 2 * math.pi * (s + rng.uniform(-0.3, 0.3)) / stems
        out = Vector((math.cos(turn), math.sin(turn), 0))
        reach = spread * rng.uniform(0.45, 0.95)
        top = height * rng.uniform(0.7, 1.0)
        # An arching stem: up from the crown, out, and over at the end.
        root = out * rng.uniform(0.0, 0.07)
        points = []
        segments = 10
        for i in range(segments + 1):
            t = i / segments
            p = root + out * reach * t ** 1.3
            p.z = top * math.sin(t * math.pi * 0.62) / math.sin(math.pi * 0.62) * (1 - 0.25 * t * t)
            p += Vector((noise.noise(Vector((s, t * 3, 0))), noise.noise(Vector((t * 3, s, 1))), 0)) * 0.04
            points.append(p)
        radii = [0.013 * (1 - 0.75 * i / segments) for i in range(segments + 1)]
        tube(bm, points, radii, 6, uv, jagged=0.0, rng=rng, material=0, cap_material=0, tile=0.25)
        branch_sets = [points]
        for b in range(rng.randint(4, 7)):
            at = rng.randint(2, segments - 1)
            base = points[at]
            side = Vector((-out.y, out.x, 0)) * rng.choice((-1, 1))
            d = (out * 0.5 + side * 0.7 + Vector((0, 0, rng.uniform(0.1, 0.7)))).normalized()
            length = rng.uniform(0.15, 0.32) * (spread / 0.65)
            branch = bent_path(base, d, length, 5, rng.uniform(-0.2, 0.2), rng, droop=length * 0.25)
            branch_radii = [radii[at] * 0.7 * (1 - 0.7 * i / 5) for i in range(6)]
            tube(bm, branch, branch_radii, 5, uv, jagged=0.0, rng=rng, material=0, cap_material=0, tile=0.25)
            branch_sets.append(branch)
        for path in branch_sets:
            for i in range(len(path) - 1):
                if i / (len(path) - 1) < 0.15:
                    continue
                for _ in range(3):
                    leaf_points.append(path[i].lerp(path[i + 1], rng.random()))
    # The crown: a dome as wide as the stems reach, leaves thickest on its outside and top.
    shell = []
    for _ in range(shell_leaves):
        a = rng.uniform(0, 2 * math.pi)
        # Favour the top: cos of the elevation drawn so the dome is evenly covered, then pushed up.
        up_share = rng.random() ** 0.7
        elevation = math.asin(up_share)
        lumpy = 1.0 + 0.22 * noise.noise(Vector((math.cos(a) * 1.4, math.sin(a) * 1.4, up_share * 1.5 + seed)))             + 0.08 * noise.noise(Vector((math.cos(a) * 5, math.sin(a) * 5, up_share * 4 + seed)))
        depth = 1.0 - rng.random() ** 2 * 0.3
        r = spread * 0.92 * lumpy * depth
        p = Vector((math.cos(a) * math.cos(elevation) * r, math.sin(a) * math.cos(elevation) * r,
                    0.12 + (height - 0.12) * math.sin(elevation) * lumpy * depth))
        shell.append(p)
    leaf_points += shell
    centre = Vector((0, 0, height * 0.3))
    rng.shuffle(leaf_points)
    for p in leaf_points:
        outward = p - centre
        outward.z *= 0.6
        outward = outward.normalized()
        direction = (outward + Vector((rng.uniform(-0.7, 0.7), rng.uniform(-0.7, 0.7), rng.uniform(-0.1, 0.5)))).normalized()
        up = (Vector((0, 0, 1)) * 0.75 + outward * 0.55 + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0))).normalized()
        value = min(0.88, max(0.0, rng.gauss(0.42, 0.2))) if rng.random() > 0.012 else rng.uniform(0.9, 0.96)
        leaf(bm, uv, var, p, direction, up, rng.uniform(0.07, 0.11), value, rng)
    # Berries on the upper outside, where they show from above.
    tops = [p for p in shell if p.z > height * 0.45]
    rng.shuffle(tops)
    for p in tops[:clusters]:
        berry_cluster(bm, uv, var, p + (p - centre).normalized() * 0.05, rng, rng.randint(4, 7))
    obj = mesh_object(name, bm, coll, materials)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def build_bush(coll):
    materials = (bark_material("Currant Bark (bake)", tint=(0.8, 0.72, 0.65)), leaf_material(), berry_material())
    return [bush("Berry Bush A", 5, 0.62, 0.88, 12, coll, materials),
            bush("Berry Bush B", 11, 0.68, 0.8, 11, coll, materials, shell_leaves=1200, clusters=44)]




def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rows = []
    if "bush" in WANTED:
        rows.append((make_asset("Berry Bush", build_bush, 2048, collection("Berry Bush")), 1.7))
    if "rocks" in WANTED:
        rows.append(make_asset("Small Rocks", build_rocks, 1024, collection("Small Rocks"), cavity_distance=0.03))
    if "sticks" in WANTED:
        rows.append(make_asset("Sticks", build_sticks, 1024, collection("Sticks"), cavity_distance=0.03))
    depth = stage(rows)
    camera((0.0, -depth - 3.4, 4.4), (0.0, -depth * 0.5, 0.2), lens=40)
    render(PREVIEW, 1600, 1000)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "Forage.blend"))
    print("FORAGE DONE")


if __name__ == "__main__":
    main()
