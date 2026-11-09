"""The stone and ore nodes of Safe Haven, modelled and textured in Blender: Granite, Copper Ore, Iron Ore, Moonsilver
Ore, Deepstone and the Rootbound Stone.

Mineable nodes follow one visual language, so the player tells them from plain rock at a glance (see the asset
principles): the metal ores share a dark host rock, and their metal is *exposed*, in clusters that break the boulder's
silhouette on the side the game's camera sees, each metal with its own hue, value and shape (readable without colour):

- Copper Ore: bright warm copper in rounded nugget clusters, green-teal verdigris on them and bleeding into the rock.
- Iron Ore: dark steel-grey angular blocks (magnetite) with rust-red stains running from them.
- Moonsilver Ore: tall pale silver spikes in clusters, glowing faintly blue, with silver veins.
- Granite: pink-grey speckled granite (stone, not ore: no clusters).
- Deepstone: blue-black stone cracked with glowing blue veins.
- Rootbound Stone: a grey stone wrapped in dark cursed roots, the Life Orb glowing green through its cracks.

Each is a split boulder of 2 to 3 m with a smaller stone or two, sculpted high and baked (prop_bake.py) from CC0 scanned
rock (Poly Haven rock_boulder_dry) onto a low mesh, with two levels of detail and a convex collider, and every map:
Albedo, Normal, ORM (with metallic), Height (parallax), BlendMask (moss) and, for the glowing ones, Emission. Writes
Assets/_Project/Art/Gathering/<Asset>/, a preview render to the path given, and Ores.blend beside this script.

Run: blender -b --factory-startup --python Tools/Blender/ore_nodes.py -- <project root> <preview.png> [asset ...]
(assets: granite, copper, iron, moonsilver, deepstone, rootbound; all when none are named).
In an open Blender (the MCP), runpy.run_path(this file, run_name="ore_lab") loads the functions without running main().
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prop_bake import *  # noqa: E402,F401,F403  (helpers, materials, bake)
from prop_bake import ROOT, args  # noqa: E402

PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "ore_preview.png")
WANTED = set(args[2:]) or {"granite", "copper", "iron", "moonsilver", "deepstone", "rootbound"}


# ----- Materials ----------------------------------------------------------------------------------------------------

def feed(tree, to_socket):
    """The socket feeding to_socket (to insert nodes after it)."""
    return to_socket.links[0].from_socket


def veined_rock(name, tint, saturation, brightness, vein=None, stain=None, vein_scale=1.4, vein_width=0.035,
                stain_width=0.16, vein_metal=0.0, vein_rough=0.5, glow=None, raised=0.25, speckle=None):
    """Scanned rock with veins in it: thin, winding lines (contours of a 3D noise field in object space, so they run on
    through the stone, and fade out along part of their length) of vein colour, bleeding a stain around them,
    optionally metallic or glowing. speckle: granite's grains, a list of (colour, share)."""
    material = rock_material(name, saturation=saturation, brightness=brightness, tint=tint, scale=0.9)
    tree = material.node_tree
    links = tree.links
    bsdf = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
    base = feed(tree, bsdf.inputs["Base Color"])
    rough = feed(tree, bsdf.inputs["Roughness"])
    height_tex = labelled(tree, "SH Height")
    height_tex.label = ""
    coords = node(tree, "ShaderNodeTexCoord", (-1400, -900))
    field = node(tree, "ShaderNodeTexNoise", (-1200, -900), Scale=vein_scale, Detail=2.0, Roughness=0.4)
    links.new(coords.outputs["Object"], field.inputs["Vector"])
    centred = node(tree, "ShaderNodeMath", (-1000, -900))
    centred.operation = 'SUBTRACT'
    centred.inputs[1].default_value = 0.5
    links.new(field.outputs["Fac"], centred.inputs[0])
    distance = node(tree, "ShaderNodeMath", (-850, -900))  # how far from the nearest vein, in noise units
    distance.operation = 'ABSOLUTE'
    links.new(centred.outputs["Value"], distance.inputs[0])
    fade_noise = node(tree, "ShaderNodeTexNoise", (-1000, -1150), Scale=0.9, Detail=1.0)
    links.new(coords.outputs["Object"], fade_noise.inputs["Vector"])
    fade = ramp(tree, (-800, -1150), [(0.38, (0, 0, 0)), (0.55, (1, 1, 1))])
    links.new(fade_noise.outputs["Fac"], fade.inputs["Fac"])
    thin = ramp(tree, (-650, -850), [(0.0, (1, 1, 1)), (vein_width * 0.5, (0, 0, 0))])
    links.new(distance.outputs["Value"], thin.inputs["Fac"])
    vein_mask = node(tree, "ShaderNodeMath", (-450, -900))
    vein_mask.operation = 'MULTIPLY'
    links.new(thin.outputs["Color"], vein_mask.inputs[0])
    links.new(fade.outputs["Color"], vein_mask.inputs[1])
    veins = vein_mask.outputs["Value"]
    stain_noise = node(tree, "ShaderNodeTexNoise", (-800, -1300), Scale=6.0, Detail=6.0)
    links.new(coords.outputs["Object"], stain_noise.inputs["Vector"])
    stain_reach = node(tree, "ShaderNodeMath", (-600, -1200))
    stain_reach.operation = 'MULTIPLY_ADD'
    stain_reach.inputs[1].default_value = 0.12
    links.new(stain_noise.outputs["Fac"], stain_reach.inputs[0])
    links.new(distance.outputs["Value"], stain_reach.inputs[2])
    stain_ramp = ramp(tree, (-400, -1200), [(0.02, (0.75, 0.75, 0.75)), (stain_width * 0.5 + 0.03, (0, 0, 0))])
    links.new(stain_reach.outputs["Value"], stain_ramp.inputs["Fac"])
    stain_mask = node(tree, "ShaderNodeMath", (-200, -1200))
    stain_mask.operation = 'MULTIPLY'
    links.new(stain_ramp.outputs["Color"], stain_mask.inputs[0])
    links.new(fade.outputs["Color"], stain_mask.inputs[1])
    stains = stain_mask.outputs["Value"]

    colour = base
    if speckle:
        # Granite: grains of feldspar, quartz and mica, a few millimetres each.
        grains = tree.nodes.new("ShaderNodeTexVoronoi")
        grains.location = (-800, 700)
        grains.inputs["Scale"].default_value = 70.0
        links.new(coords.outputs["Object"], grains.inputs["Vector"])
        threshold = 0.0
        for i, (grain_colour, share) in enumerate(speckle):
            threshold_low, threshold = threshold, threshold + share
            pick = ramp(tree, (-550, 700 - i * 200), [(threshold_low, (0, 0, 0)), (threshold_low + 0.001, (1, 1, 1)),
                                                       (threshold, (1, 1, 1)), (min(threshold + 0.001, 1.0), (0, 0, 0))])
            links.new(grains.outputs["Color"], pick.inputs["Fac"])
            colour = mix(tree, (-200, 700 - i * 200), colour, rgb(tree, (-400, 600 - i * 200), grain_colour),
                         pick.outputs["Color"], blend='OVERLAY' if i else 'MIX')
    if stain:
        colour = mix(tree, (0, 200), colour, rgb(tree, (-200, 100), stain), stains, blend='MULTIPLY')
    if vein:
        colour = mix(tree, (200, 200), colour, rgb(tree, (0, 100), vein), veins)
        rough = mix(tree, (200, -100), rough, value(tree, (0, -150), vein_rough), veins)
    links.new(colour, bsdf.inputs["Base Color"])
    links.new(rough, bsdf.inputs["Roughness"])

    # Height: the scan's relief, veins standing out of the eroded stone.
    height = node(tree, "ShaderNodeMath", (0, -600))
    height.operation = 'MULTIPLY_ADD'
    height.inputs[1].default_value = raised if vein else 0.0
    links.new(veins, height.inputs[0])
    links.new(height_tex.outputs["Color"], height.inputs[2])
    height_clamped = node(tree, "ShaderNodeClamp", (200, -600))
    links.new(height.outputs["Value"], height_clamped.inputs["Value"])
    height_clamped.label = "SH Height"
    if vein and vein_metal > 0:
        metal = node(tree, "ShaderNodeMath", (200, -800))
        metal.operation = 'MULTIPLY'
        metal.inputs[1].default_value = vein_metal
        links.new(veins, metal.inputs[0])
        links.new(metal.outputs["Value"], bsdf.inputs["Metallic"])
        metal.label = "SH Metal"
    if glow:
        lit = mix(tree, (200, -1000), rgb(tree, (0, -1100), (0, 0, 0)), rgb(tree, (0, -1200), glow), veins)
        links.new(lit, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0
        glow_node = tree.nodes.new("NodeReroute")
        glow_node.location = (400, -1000)
        links.new(lit, glow_node.inputs[0])
        glow_node.label = "SH Glow"
    return material


def mineral_material(name, colour, roughness, metallic, glow=None, variation=0.15, patina=None, patina_amount=0.0):
    """Native metal, magnetite or crystal: its colour broken by fine noise; metallic where it is metal, and in patches
    (patina_amount of it) a dull, non-metallic patina (verdigris on copper, rust on iron)."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    coords = node(tree, "ShaderNodeTexCoord", (-900, 0))
    grain = node(tree, "ShaderNodeTexNoise", (-700, 0), Scale=40.0, Detail=8.0)
    links.new(coords.outputs["Object"], grain.inputs["Vector"])
    shade = ramp(tree, (-450, 0), [(0.3, tuple(c * (1 - variation) for c in colour)), (0.7, tuple(min(1.0, c * (1 + variation)) for c in colour))])
    links.new(grain.outputs["Fac"], shade.inputs["Fac"])
    colour_out = shade.outputs["Color"]
    metal_out = value(tree, (-450, -300), metallic)
    rough_out = value(tree, (-450, -350), roughness)
    if patina:
        patches = node(tree, "ShaderNodeTexNoise", (-900, 400), Scale=9.0, Detail=5.0)
        links.new(coords.outputs["Object"], patches.inputs["Vector"])
        cut = 1.0 - patina_amount
        mask = ramp(tree, (-650, 400), [(cut - 0.05, (0, 0, 0)), (cut + 0.05, (1, 1, 1))])
        links.new(patches.outputs["Fac"], mask.inputs["Fac"])
        colour_out = mix(tree, (-200, 200), colour_out, rgb(tree, (-450, 250), patina), mask.outputs["Color"])
        dull = node(tree, "ShaderNodeMath", (-200, -300))
        dull.operation = 'MULTIPLY'
        links.new(metal_out, dull.inputs[0])
        inverse = node(tree, "ShaderNodeMath", (-350, -450))
        inverse.operation = 'SUBTRACT'
        inverse.inputs[0].default_value = 1.0
        links.new(mask.outputs["Color"], inverse.inputs[1])
        links.new(inverse.outputs["Value"], dull.inputs[1])
        metal_out = dull.outputs["Value"]
        rougher = mix(tree, (-200, -500), rough_out, value(tree, (-350, -550), 0.85), mask.outputs["Color"])
        rough_out = rougher
    links.new(colour_out, bsdf.inputs["Base Color"])
    links.new(rough_out, bsdf.inputs["Roughness"])
    links.new(metal_out, bsdf.inputs["Metallic"])
    metal = tree.nodes.new("NodeReroute")
    links.new(metal_out, metal.inputs[0])
    metal.label = "SH Metal"
    height = value(tree, (-450, -400), 0.9)  # standing out of the stone
    height.node.label = "SH Height"
    bump = node(tree, "ShaderNodeBump", (500, -300), Strength=0.3, Distance=0.002)
    links.new(grain.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    if glow:
        bsdf.inputs["Emission Color"].default_value = (*glow, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 1.0
        glow_socket = rgb(tree, (-450, -600), glow)
        glow_socket.node.label = "SH Glow"
    return material


def cursed_root_material():
    """Dark, cursed roots: near-black bark with a sick violet in its furrows."""
    material = bark_material("Cursed Root (bake)", tint=(0.2, 0.15, 0.17))
    tree = material.node_tree
    bsdf = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
    height = labelled(tree, "SH Height")
    furrow = ramp(tree, (300, -900), [(0.0, (0.1, 0.02, 0.12)), (0.25, (0, 0, 0))])
    tree.links.new(height.outputs["Color"], furrow.inputs["Fac"])
    tree.links.new(furrow.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    glow = tree.nodes.new("NodeReroute")
    tree.links.new(furrow.outputs["Color"], glow.inputs[0])
    glow.label = "SH Glow"
    return material


# ----- Shapes -------------------------------------------------------------------------------------------------------

def boulder_group(name, rng, coll, main, sides, material, low_faces=1400):
    """A main boulder (size main) and smaller stones beside it ((x, y, size) each), as one high and one low mesh."""
    lows, highs = [], []
    # Broken along more planes, cut deeper and less lumpy than a field stone: rock split out of the ground.
    low, high = stone(f"{name} Main", main, rng, coll, high_subdiv=7, low_faces=low_faces, splits=(7, 10), cut=(0.4, 0.62),
                      lumps=0.03)
    lows.append(low)
    highs.append(high)
    for i, (x, y, size) in enumerate(sides):
        low, high = stone(f"{name} Side {i}", size, rng, coll, high_subdiv=6, low_faces=low_faces // 4)
        placed = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(rng.uniform(0, 6.28), 4, 'Z')  # once: low and high match
        for obj in (low, high):
            obj.data.transform(placed)
        lows.append(low)
        highs.append(high)
    for obj in lows + highs:
        obj.data.materials.append(material)
    return join(lows, name), join(highs, f"{name} High")


def surface(high):
    """A BVH of the high mesh, to set things on its surface."""
    bm = bmesh.new()
    bm.from_mesh(high.data)
    tree = BVHTree.FromBMesh(bm)
    bm.free()
    return tree


# The game's camera looks down at about 54 degrees from the south (-Y): what faces it is what the player sees.
TOWARDS_CAMERA = Vector((0.0, -0.59, 0.81))


def spots(bvh, rng, count, reach, min_height=0.15, spacing=0.0):
    """Points on the stone (and their normals) the game's camera sees: rays cast along its view, kept where the surface
    faces it, at least spacing apart."""
    found = []
    tries = 0
    while len(found) < count and tries < count * 60:
        tries += 1
        x, z = rng.uniform(-reach, reach), rng.uniform(0.0, reach)
        origin = Vector((x, 0.0, z)) + TOWARDS_CAMERA * 12.0
        hit = bvh.ray_cast(origin, -TOWARDS_CAMERA)
        if hit[0] is None or hit[0].z < min_height or hit[1].dot(TOWARDS_CAMERA) < 0.35:
            continue
        if any((hit[0] - other).length < spacing for other, _ in found):
            continue
        found.append((hit[0], hit[1]))
    return found


def nugget(bm, at, normal, size, rng):
    """A lumpy nugget half sunk in the stone."""
    result = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    seed = Vector((rng.uniform(0, 99), rng.uniform(0, 99), rng.uniform(0, 99)))
    scale = Vector((size * rng.uniform(0.8, 1.3), size * rng.uniform(0.8, 1.3), size * rng.uniform(0.5, 0.8)))
    turn = Vector((0, 0, 1)).rotation_difference(normal).to_matrix().to_4x4()
    for v in result["verts"]:
        d = v.co.normalized()
        r = 1.0 + 0.3 * noise.noise(d * 2.0 + seed)
        local = Vector((d.x * scale.x, d.y * scale.y, d.z * scale.z)) * r
        v.co = (turn @ local) + at - normal * size * 0.35
    return {f for v in result["verts"] for f in v.link_faces}


def block(bm, at, normal, size, rng):
    """An angular block of ore (magnetite grows as cubes), chamfered, tipped at a slant and set a third into the stone.
    Returns its faces."""
    piece = bmesh.new()
    bmesh.ops.create_cube(piece, size=1.0)
    bmesh.ops.bevel(piece, geom=list(piece.edges), offset=0.1, segments=1, affect='EDGES')
    lean = (normal + Vector((rng.uniform(-0.45, 0.45), rng.uniform(-0.45, 0.45), 0.0))).normalized()
    tilt = Vector((0, 0, 1)).rotation_difference(lean).to_matrix().to_4x4()
    spin = Matrix.Rotation(rng.uniform(0, math.pi), 4, 'Z') @ Matrix.Rotation(rng.uniform(0.3, 0.8), 4, 'X')
    scale = Matrix.Diagonal((size * rng.uniform(0.8, 1.2), size * rng.uniform(0.8, 1.2), size * rng.uniform(0.9, 1.4), 1.0))
    piece.transform(Matrix.Translation(at - normal * size * 0.3) @ tilt @ spin @ scale)
    mesh = bpy.data.meshes.new("block")
    piece.to_mesh(mesh)
    piece.free()
    before = set(bm.faces)
    bm.from_mesh(mesh)
    bpy.data.meshes.remove(mesh)
    return [f for f in bm.faces if f not in before]


def crystal(bm, at, normal, length, radius, rng):
    """A six-sided crystal shard with a pointed tip, leaning off the surface."""
    lean = (normal + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(0, 0.4)))).normalized()
    turn = Vector((0, 0, 1)).rotation_difference(lean).to_matrix().to_4x4()
    twist = rng.uniform(0, math.pi)
    base_ring, top_ring = [], []
    for k in range(6):
        a = twist + k * math.pi / 3
        offset = Vector((math.cos(a) * radius, math.sin(a) * radius, 0))
        base_ring.append(bm.verts.new(at + turn @ (offset + Vector((0, 0, -length * 0.25)))))
        top_ring.append(bm.verts.new(at + turn @ (offset * 0.9 + Vector((0, 0, length * 0.75)))))
    tip = bm.verts.new(at + turn @ Vector((rng.uniform(-0.2, 0.2) * radius, rng.uniform(-0.2, 0.2) * radius, length)))
    faces = []
    for k in range(6):
        faces.append(bm.faces.new((base_ring[k], base_ring[(k + 1) % 6], top_ring[(k + 1) % 6], top_ring[k])))
        faces.append(bm.faces.new((top_ring[k], top_ring[(k + 1) % 6], tip)))
    faces.append(bm.faces.new(list(reversed(base_ring))))
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return faces


def add_minerals(low, high, rng, kind, count, size, material_index, per_cluster=(3, 6)):
    """Clusters of ore (nuggets, blocks or crystals) where the game's camera sees the stone, added to both meshes (they
    are low already: the high bakes them as they are)."""
    bvh = surface(high)
    reach = max(high.dimensions.x, high.dimensions.y) * 0.5
    places = spots(bvh, rng, count, reach, spacing=size * 2.2)
    seed = rng.random()  # once: the low and the high get the same minerals
    for obj in (low, high):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        local_rng = random.Random(seed)
        for at, normal in places:
            side = normal.orthogonal().normalized()
            other = normal.cross(side)
            for k in range(local_rng.randint(*per_cluster)):
                a = local_rng.uniform(0, 2 * math.pi)
                spread = size * (0.0 if k == 0 else local_rng.uniform(0.5, 1.1))
                where = at + (side * math.cos(a) + other * math.sin(a)) * spread
                piece_size = size * (1.0 if k == 0 else local_rng.uniform(0.45, 0.8))
                if kind == "crystals":
                    faces = crystal(bm, where, normal, piece_size * local_rng.uniform(1.8, 3.6),
                                    piece_size * local_rng.uniform(0.2, 0.32), local_rng)
                    smooth = False
                elif kind == "blocks":
                    faces = block(bm, where, normal, piece_size, local_rng)
                    smooth = False
                else:
                    faces = nugget(bm, where, normal, piece_size, local_rng)
                    smooth = True
                for f in faces:
                    f.material_index = material_index
                    f.smooth = smooth
        bm.to_mesh(obj.data)
        bm.free()


def roots_over(low, high, rng, count, material, coll):
    """Cursed roots gripping the stone: each comes out of the ground, winds part of the way round it at its own height
    (rising over its shoulder, never all crossing at the top), hugging its surface, and goes back into the ground."""
    bvh = surface(high)
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("grain")
    reach = max(high.dimensions.x, high.dimensions.y) * 0.55
    top = high.dimensions.z
    for r in range(count):
        a = 2 * math.pi * r / count + rng.uniform(-0.4, 0.4)
        sweep = rng.uniform(1.6, 3.2) * rng.choice((-1, 1))  # how far round the stone it winds
        radius = rng.uniform(0.05, 0.085)
        climb = rng.uniform(0.65, 1.05)  # the highest it reaches, as a share of the stone's height
        points = []
        steps = 30
        for i in range(steps + 1):
            t = i / steps
            angle = a + sweep * t
            q = Vector((math.cos(angle) * reach, math.sin(angle) * reach, top * climb * math.sin(math.pi * t) ** 0.7))
            hit = bvh.find_nearest(q)
            p = hit[0] + hit[1] * radius * 0.7 if hit[0] is not None else q
            p.z = max(p.z, radius * 0.6)
            points.append(p)
        # Into the ground at both ends.
        points.insert(0, points[0] + (points[0] - points[1]).normalized() * 0.15 + Vector((0, 0, -0.12)))
        points.append(points[-1] + (points[-1] - points[-2]).normalized() * 0.15 + Vector((0, 0, -0.12)))
        radii = [radius * (1.0 - 0.45 * abs(2 * i / (len(points) - 1) - 1)) for i in range(len(points))]
        tube(bm, points, radii, 7, uv, wood_cap=True, rng=rng, material=0, cap_material=0, tile=0.5)
    roots = mesh_object(f"{low.name} Roots", bm, coll, (material,))
    for poly in roots.data.polygons:
        poly.use_smooth = True
    return roots


# ----- Assets -------------------------------------------------------------------------------------------------------

def ore(name, key, seed, coll, material, mineral=None, kind=None, count=0, size=0.08, main=(1.2, 1.0, 0.95),
        sides=((0.95, 0.55, (0.38, 0.32, 0.34)), (-0.85, -0.7, (0.3, 0.26, 0.28))), per_cluster=(3, 6)):
    rng = random.Random(seed)
    low, high = boulder_group(f"{name} {key}", rng, coll, main, sides, material)
    if mineral is not None:
        for obj in (low, high):
            obj.data.materials.append(mineral)
        add_minerals(low, high, rng, kind, count, size, 1, per_cluster)
    return low, high


def host_rock(name, vein, stain, vein_metal=0.0, vein_rough=0.5, vein_width=0.05, stain_width=0.22, glow=None,
              raised=0.25):
    """The rock every metal ore sits in: darker and cooler than field stone (so ore reads as ore, not as a boulder),
    with wide veins of the metal and its stain bleeding round them."""
    return veined_rock(name, tint=(0.92, 0.92, 0.95), saturation=0.4, brightness=0.3, vein=vein, stain=stain,
                       vein_scale=0.8, vein_width=vein_width, stain_width=stain_width, vein_metal=vein_metal,
                       vein_rough=vein_rough, glow=glow, raised=raised)


def pairs_of(pairs):
    return [p[0] for p in pairs], [p[1] for p in pairs]


def build_granite(coll):
    material = veined_rock("Granite (bake)", tint=(1.0, 0.84, 0.78), saturation=0.55, brightness=0.5,
                           vein=(0.75, 0.72, 0.68), vein_scale=0.8, vein_width=0.015, raised=0.15,
                           speckle=[((0.52, 0.34, 0.3), 0.45), ((0.72, 0.7, 0.66), 0.3), ((0.05, 0.05, 0.05), 0.12)])
    return pairs_of([ore("Granite", "A", 401, coll, material, main=(1.15, 0.95, 1.05)),
                     ore("Granite", "B", 402, coll, material, main=(1.25, 0.85, 0.95),
                         sides=((1.0, 0.4, (0.42, 0.34, 0.28)),))])


def build_copper(coll):
    # Copper: bright warm metal in rounded nuggets, verdigris on them and staining the rock teal round the veins.
    material = host_rock("Copper Rock (bake)", vein=(0.95, 0.42, 0.16), stain=(0.3, 0.8, 0.62), vein_metal=1.0,
                         vein_rough=0.38, vein_width=0.08, stain_width=0.3)
    copper = mineral_material("Native Copper (bake)", (0.95, 0.42, 0.17), 0.38, 1.0, variation=0.2,
                              patina=(0.16, 0.5, 0.4), patina_amount=0.25)
    return pairs_of([ore("Copper Ore", "A", 411, coll, material, copper, "nuggets", 10, 0.17, per_cluster=(4, 7)),
                     ore("Copper Ore", "B", 412, coll, material, copper, "nuggets", 9, 0.18, main=(1.1, 1.0, 1.0),
                         per_cluster=(4, 7))])


def build_iron(coll):
    # Iron: dark steel-grey angular blocks, rust-red stains round them.
    material = host_rock("Iron Rock (bake)", vein=(0.36, 0.11, 0.05), stain=(0.72, 0.3, 0.17), vein_metal=0.0,
                         vein_rough=0.8, vein_width=0.1, stain_width=0.38, raised=0.1)
    magnetite = mineral_material("Magnetite (bake)", (0.16, 0.16, 0.18), 0.48, 0.85, variation=0.25,
                                 patina=(0.36, 0.12, 0.05), patina_amount=0.3)
    return pairs_of([ore("Iron Ore", "A", 421, coll, material, magnetite, "blocks", 8, 0.28, main=(1.25, 1.0, 0.95),
                         per_cluster=(2, 4)),
                     ore("Iron Ore", "B", 422, coll, material, magnetite, "blocks", 7, 0.3, per_cluster=(2, 4))])


def build_moonsilver(coll):
    # Moonsilver: tall pale silver spikes glowing faintly blue, fine silver veins.
    material = host_rock("Moonsilver Rock (bake)", vein=(0.85, 0.9, 0.98), stain=(0.85, 0.92, 1.05), vein_metal=1.0,
                         vein_rough=0.2, vein_width=0.018, stain_width=0.1, glow=(0.2, 0.35, 0.6))
    shards = mineral_material("Moonsilver Crystal (bake)", (0.82, 0.88, 0.96), 0.15, 0.75, glow=(0.3, 0.5, 0.85),
                              variation=0.1)
    return pairs_of([ore("Moonsilver Ore", "A", 431, coll, material, shards, "crystals", 6, 0.2, main=(1.25, 1.0, 1.0)),
                     ore("Moonsilver Ore", "B", 432, coll, material, shards, "crystals", 5, 0.22, main=(1.3, 0.95, 0.95))])


def build_deepstone(coll):
    material = veined_rock("Deepstone (bake)", tint=(0.62, 0.68, 1.0), saturation=0.4, brightness=0.32,
                           vein=(0.3, 0.5, 1.0), stain=(0.6, 0.7, 1.0), vein_scale=1.1, vein_width=0.018,
                           stain_width=0.12, vein_rough=0.3, glow=(0.2, 0.45, 1.0), raised=-0.2)
    return pairs_of([ore("Deepstone", "A", 441, coll, material, main=(1.4, 1.1, 1.25)),
                     ore("Deepstone", "B", 442, coll, material, main=(1.5, 1.05, 1.15),
                         sides=((1.2, 0.6, (0.45, 0.38, 0.3)), (-1.1, -0.6, (0.35, 0.3, 0.24)), (0.2, -1.1, (0.28, 0.24, 0.2))))])


def build_rootbound(coll):
    stone_material = veined_rock("Rootbound Stone (bake)", tint=(0.95, 0.97, 1.0), saturation=0.5, brightness=0.48,
                                 vein=(0.25, 0.85, 0.45), vein_scale=1.4, vein_width=0.022, vein_rough=0.4,
                                 glow=(0.25, 0.95, 0.45), raised=-0.25)
    roots = cursed_root_material()
    rng = random.Random(451)
    low, high = boulder_group("Rootbound Stone A", rng, coll, (0.85, 0.8, 1.1), ((0.75, 0.45, (0.3, 0.26, 0.26)),),
                              stone_material)
    root_mesh = roots_over(low, high, rng, 6, roots, coll)
    high_roots = root_mesh.copy()
    high_roots.data = root_mesh.data.copy()
    coll.objects.link(high_roots)
    low = join([low, root_mesh], "Rootbound Stone A")
    high = join([high, high_roots], "Rootbound Stone A High")
    return [low], [high]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rows = []
    builds = (("granite", "Granite", build_granite, False), ("copper", "Copper Ore", build_copper, False),
              ("iron", "Iron Ore", build_iron, False), ("moonsilver", "Moonsilver Ore", build_moonsilver, True),
              ("deepstone", "Deepstone", build_deepstone, True), ("rootbound", "Rootbound Stone", build_rootbound, True))
    for key, name, builder, glow in builds:
        if key in WANTED:
            rows.append((make_asset(name, builder, 2048, collection(name), glow=glow, cavity_distance=0.15,
                                    collider=True, lods=(0.5, 0.2)), 3.4))
    depth = stage(rows, row_spacing=3.6)
    camera((0.0, -depth - 6.5, 9.0), (0.0, -depth * 0.5 + 0.5, 0.4), lens=35)
    render(PREVIEW, 1600, 1100)
    # A close look at each asset, at the game's angle.
    for objects, _ in rows:
        centre = sum((o.location for o in objects), Vector()) / len(objects)
        camera(centre + Vector((0.0, -5.2, 5.0)), centre + Vector((0, 0, 0.5)), lens=40)
        render(PREVIEW.replace(".png", f" {objects[0].name[:-2]}.png"), 1400, 800)
    if len(args) <= 2:  # a full run: keep the .blend of every asset
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "Ores.blend"))
    print("ORES DONE")


if __name__ == "__main__":
    main()
