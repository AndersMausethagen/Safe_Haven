"""The first things the player gathers in Safe Haven, modelled and textured in Blender: Small Rocks, Sticks and the Berry Bush.

They replace the sphere and cylinder stand-ins of the Small Rocks, Sticks and Berries nodes (and the dropped Small Rocks and
Sticks), and are meant to sit beside the Poly Haven scans the Haven is dressed with (Field Stone, the fallen logs), so they
are made the same way: real-size shapes whose surfaces come from CC0 scanned textures (Poly Haven lichen_rock and
bark_brown_02, in Tools/Blender/Sources/PolyHaven), baked into each asset's own UVs with ambient occlusion.

- Small Rocks: three clusters of rounded granite field stones with lichen, 3 to 5 stones, about 0.55 x 0.5 m. Sculpted
  high (voronoi facets and noise on a subdivided sphere), baked onto a low mesh of about 250 triangles a stone.
- Sticks: three heaps of fallen, forked branches, 3 to 5 sticks 0.35 to 0.6 m long, with broken, pale ends.
- Berry Bush: two wild currant shrubs, about 1.3 m across and 0.9 m high: arching woody stems, about a thousand leaves
  and clusters of red berries hanging on the outside, where the game's camera above sees them.

Each asset is one FBX holding its variants as separate objects ("<Asset> A", "B", ...), each with its pivot on the ground
under its middle, and three 2048 or 1024 textures: <Asset>_Albedo.png, <Asset>_Normal.png and <Asset>_ORM.png (R
occlusion, G roughness, B metallic: what the game's Layered Lit shader reads). Writes Assets/_Project/Art/Gathering/
<Asset>/, a preview render of all variants to the path given, and Forage.blend beside this script.

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

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else "D:/Safe Haven/Safe Haven"
PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "forage_preview.png")
WANTED = set(args[2:]) or {"rocks", "sticks", "bush"}
SOURCES = os.path.join(ROOT, "Tools", "Blender", "Sources", "PolyHaven")
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Gathering")


# ----- Small helpers ------------------------------------------------------------------------------------------------

def collection(name):
    found = bpy.data.collections.get(name)
    if found is None:
        found = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(found)
    return found


def mesh_object(name, bm, coll, materials=()):
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    for material in materials:
        mesh.materials.append(material)
    return obj


def select_only(objects, active=None):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = active or objects[0]


def join(objects, name):
    select_only(objects)
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    return joined


def image(path, colourspace):
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = colourspace
    return img


def source(asset, kind):
    return os.path.join(SOURCES, asset, f"{asset}_{kind}_2k.jpg")


def node(tree, kind, location, **inputs):
    n = tree.nodes.new(kind)
    n.location = location
    for key, value in inputs.items():
        n.inputs[key].default_value = value
    return n


def ramp(tree, location, stops):
    n = tree.nodes.new("ShaderNodeValToRGB")
    n.location = location
    elements = n.color_ramp.elements
    while len(elements) > len(stops):
        elements.remove(elements[-1])
    while len(elements) < len(stops):
        elements.new(0.5)
    for element, (pos, colour) in zip(elements, stops):
        element.position = pos
        element.color = (*colour, 1.0)
    return n


def mix(tree, location, a, b, factor, blend='MIX'):
    n = tree.nodes.new("ShaderNodeMix")
    n.data_type = 'RGBA'
    n.blend_type = blend
    n.location = location
    tree.links.new(factor, n.inputs["Factor"])
    tree.links.new(a, n.inputs["A"])
    tree.links.new(b, n.inputs["B"])
    return n.outputs["Result"]


def value(tree, location, v):
    n = tree.nodes.new("ShaderNodeValue")
    n.location = location
    n.outputs[0].default_value = v
    return n.outputs[0]


def rgb(tree, location, colour):
    n = tree.nodes.new("ShaderNodeRGB")
    n.location = location
    n.outputs[0].default_value = (*colour, 1.0)
    return n.outputs[0]


def new_material(name):
    material = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    out = node(tree, "ShaderNodeOutputMaterial", (1200, 0))
    bsdf = node(tree, "ShaderNodeBsdfPrincipled", (900, 0))
    tree.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return material, tree, bsdf


# ----- Tubes: sticks and stems --------------------------------------------------------------------------------------

def tube(bm, points, radii, sides, uv, wood_cap=True, jagged=0.0, rng=None, material=0, cap_material=1, tile=0.5):
    """A tube through points (each ring sized by radii) with parallel-transport frames, so it never twists. Its "grain"
    UVs run round it (U, in metres of circumference / tile) and along it (V, metres / tile), so a tiling bark texture
    keeps its real size. The ends are closed with caps of cap_material (pale broken wood), the far end jagged."""
    rng = rng or random.Random(0)
    rings = []
    tangent = (points[1] - points[0]).normalized()
    normal = tangent.orthogonal().normalized()
    length = 0.0
    for i, p in enumerate(points):
        if i > 0:
            length += (p - points[i - 1]).length
            new_tangent = (points[min(i + 1, len(points) - 1)] - points[i - 1]).normalized()
            rotation = tangent.rotation_difference(new_tangent)
            normal = (rotation @ normal).normalized()
            tangent = new_tangent
        binormal = tangent.cross(normal)
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides
            offset = normal * math.cos(a) + binormal * math.sin(a)
            # Bark is never perfectly round.
            wobble = 1.0 + 0.08 * noise.noise(p * 40.0 + offset * 3.0)
            ring.append(bm.verts.new(p + offset * radii[i] * wobble))
        rings.append((ring, length, radii[i]))
    faces = []
    for (ring_a, la, ra), (ring_b, lb, rb) in zip(rings, rings[1:]):
        for s in range(sides):
            quad = (ring_a[s], ring_a[(s + 1) % sides], ring_b[(s + 1) % sides], ring_b[s])
            f = bm.faces.new(quad)
            f.material_index = material
            circumference_a = 2 * math.pi * ra / tile
            circumference_b = 2 * math.pi * rb / tile
            loops = f.loops
            loops[0][uv].uv = (circumference_a * s / sides, la / tile)
            loops[1][uv].uv = (circumference_a * (s + 1) / sides, la / tile)
            loops[2][uv].uv = (circumference_b * (s + 1) / sides, lb / tile)
            loops[3][uv].uv = (circumference_b * s / sides, lb / tile)
            faces.append(f)
    if wood_cap:
        for index, (ring, _, radius) in ((0, rings[0]), (-1, rings[-1])):
            centre_point = points[index]
            direction = (points[-1] - points[-2]).normalized() if index == -1 else (points[0] - points[1]).normalized()
            # A broken end: the cap pulled out into splinters.
            centre = bm.verts.new(centre_point + direction * radius * (0.6 if index == -1 else 0.2) * (1 + jagged))
            for v in ring:
                v.co += direction * radius * rng.uniform(0.0, 0.9) * jagged
            for s in range(sides):
                tri = (ring[s], ring[(s + 1) % sides], centre) if index == -1 else (ring[(s + 1) % sides], ring[s], centre)
                f = bm.faces.new(tri)
                f.material_index = cap_material
                for loop in f.loops:
                    co = loop.vert.co - centre_point
                    loop[uv].uv = (0.5 + co.dot(direction.orthogonal().normalized()) / (radius * 4 + 1e-6),
                                   0.5 + co.dot(direction.cross(direction.orthogonal()).normalized()) / (radius * 4 + 1e-6))
                faces.append(f)
    bmesh.ops.recalc_face_normals(bm, faces=faces)
    return faces


def bent_path(start, direction, length, segments, bend, rng, lift=0.0, droop=0.0):
    """Points along a gently bending line: a branch."""
    points = []
    d = direction.normalized()
    side = Vector((-d.y, d.x, 0.0)).normalized() if abs(d.z) < 0.99 else Vector((1, 0, 0))
    seed = Vector((rng.uniform(0, 100), rng.uniform(0, 100), rng.uniform(0, 100)))
    for i in range(segments + 1):
        t = i / segments
        p = start + d * length * t
        p += side * bend * math.sin(t * math.pi) * length
        p += side * 0.035 * length * noise.noise(seed + Vector((t * 3.0, 0, 0)))
        p.z += lift * math.sin(t * math.pi) - droop * t * t
        points.append(p)
    return points


# ----- Materials for the bake ---------------------------------------------------------------------------------------

def rock_material():
    """Weathered grey field stone (Poly Haven rock_boulder_dry), projected from six sides in object space so it has no
    seams."""
    material, tree, bsdf = new_material("Field Stone (bake)")
    links = tree.links
    coords = node(tree, "ShaderNodeTexCoord", (-900, 0))
    mapping = node(tree, "ShaderNodeMapping", (-700, 0))
    # The texture is about 2 m across; finer reads better on stones this small.
    mapping.inputs["Scale"].default_value = (1.6, 1.6, 1.6)
    links.new(coords.outputs["Object"], mapping.inputs["Vector"])
    maps = {}
    for i, (kind, space) in enumerate((("diff", 'sRGB'), ("rough", 'Non-Color'), ("nor_gl", 'Non-Color'))):
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.location = (-450, 300 - i * 300)
        tex.image = image(source("rock_boulder_dry", kind), space)
        tex.projection = 'BOX'
        tex.projection_blend = 0.35
        links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        maps[kind] = tex
    # A little greyer and darker than the sunlit scan, like the Haven's other field stones.
    grade = node(tree, "ShaderNodeHueSaturation", (-100, 300), Saturation=0.6, Value=0.6)
    links.new(maps["diff"].outputs["Color"], grade.inputs["Color"])
    links.new(grade.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(maps["rough"].outputs["Color"], bsdf.inputs["Roughness"])
    # Box-projected normal maps don't line up with tangents: the relief comes from the sculpt and a bump of the colour.
    bump = node(tree, "ShaderNodeBump", (500, -300), Strength=0.25, Distance=0.004)
    links.new(maps["rough"].outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def bark_material(name="Brown Bark (bake)", tint=(0.78, 0.68, 0.56)):
    """Poly Haven bark_brown_02 on the tubes' grain UVs (real size: 1 m to a tile)."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-900, 0)
    mapping = node(tree, "ShaderNodeMapping", (-700, 0))
    # Grain UVs are in half metres; the texture covers 1 m. Twigs have finer bark: scale it down a little.
    mapping.inputs["Scale"].default_value = (1.4, 1.4, 1.0)
    links.new(uv.outputs["UV"], mapping.inputs["Vector"])
    maps = {}
    for i, (kind, space) in enumerate((("diff", 'sRGB'), ("rough", 'Non-Color'), ("nor_gl", 'Non-Color'))):
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.location = (-450, 300 - i * 300)
        tex.image = image(source("bark_brown_02", kind), space)
        links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        maps[kind] = tex
    tinted = mix(tree, (100, 300), maps["diff"].outputs["Color"], rgb(tree, (-100, 450), tint), value(tree, (-100, 550), 1.0),
                 blend='MULTIPLY')
    # Dead wood weathers grey where it faces the sky.
    up = node(tree, "ShaderNodeNewGeometry", (-450, 700))
    split = node(tree, "ShaderNodeSeparateXYZ", (-250, 700))
    links.new(up.outputs["Normal"], split.inputs["Vector"])
    weather = ramp(tree, (-50, 700), [(0.5, (0, 0, 0)), (1.0, (0.18, 0.18, 0.18))])
    links.new(split.outputs["Z"], weather.inputs["Fac"])
    grey = node(tree, "ShaderNodeRGB", (100, 650))
    grey.outputs[0].default_value = (0.22, 0.2, 0.17, 1)
    coloured = mix(tree, (350, 400), tinted, grey.outputs[0], weather.outputs["Color"])
    links.new(coloured, bsdf.inputs["Base Color"])
    links.new(maps["rough"].outputs["Color"], bsdf.inputs["Roughness"])
    normal_map = node(tree, "ShaderNodeNormalMap", (300, -300), Strength=1.0)
    links.new(maps["nor_gl"].outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def broken_wood_material():
    """The pale, splintered wood of a broken end: fibres along the break, darker towards the bark."""
    material, tree, bsdf = new_material("Broken Wood (bake)")
    links = tree.links
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-800, 0)
    wave = tree.nodes.new("ShaderNodeTexWave")
    wave.location = (-500, 0)
    wave.wave_type = 'RINGS'
    wave.inputs["Scale"].default_value = 9.0
    wave.inputs["Distortion"].default_value = 4.0
    links.new(uv.outputs["UV"], wave.inputs["Vector"])
    colour = ramp(tree, (-200, 0), [(0.2, (0.36, 0.28, 0.18)), (0.8, (0.55, 0.45, 0.31))])
    links.new(wave.outputs["Fac"], colour.inputs["Fac"])
    links.new(colour.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.9
    bump = node(tree, "ShaderNodeBump", (500, -250), Strength=0.4, Distance=0.002)
    links.new(wave.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def leaf_material():
    """Currant leaves: fresh green, lighter along the midrib and veins, darker at the rim; each leaf a slightly different
    green (its "leafvar" face attribute), a few turning yellow. U runs across the leaf (0 to 1), V from stalk to tip."""
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
    attribute.attribute_name = "leafvar"
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
    attribute.attribute_name = "leafvar"
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

def stone(name, size, rng, coll, high_subdiv=6):
    """A rounded field stone: an ellipsoid, flattened under, with broad voronoi facets and fine noise (the high mesh), and
    a decimated copy of it (the low mesh the game uses)."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=high_subdiv, radius=1.0)
    seed = Vector((rng.uniform(0, 500), rng.uniform(0, 500), rng.uniform(0, 500)))
    sx, sy, sz = size[0], size[1], size[2] * 0.8
    planes = []
    for _ in range(rng.randint(5, 7)):
        n = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.1, 1.0))).normalized()
        reach = abs(n.x) * sx + abs(n.y) * sy + abs(n.z) * sz
        planes.append((n, reach * rng.uniform(0.42, 0.62)))
    for v in bm.verts:
        d = v.co.normalized()
        # Broad facets: the stone was split along planes and then worn round.
        facets = 0.0
        bumps = 0.035 * noise.fractal(d * 2.5 + seed, 0.6, 2.0, 4)
        grain = 0.012 * noise.fractal(d * 18.0 + seed, 0.5, 2.0, 3)
        r = 1.0 + facets + bumps + grain
        p = Vector((d.x * sx, d.y * sy, d.z * sz)) * r
        # Split faces: a few planes the stone broke along, worn a little round at the edges.
        for normal, depth in planes:
            over = p.dot(normal) - depth
            if over > 0:
                p -= normal * over
        v.co = p
    # Sits on the ground: the lower part flattened, a little sunk in.
    lowest = min(v.co.z for v in bm.verts)
    floor = lowest + sz * 0.45
    for v in bm.verts:
        if v.co.z < floor:
            v.co.z = floor + (v.co.z - floor) * 0.15
    shift = -min(v.co.z for v in bm.verts) - sz * 0.08
    for v in bm.verts:
        v.co.z += shift
    high = mesh_object(f"{name} High", bm, coll)
    low = high.copy()
    low.data = high.data.copy()
    low.name = name
    low.data.name = name
    coll.objects.link(low)
    decimate = low.modifiers.new("Decimate", 'DECIMATE')
    decimate.ratio = 260.0 / (len(high.data.polygons))
    select_only([low])
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    for poly in high.data.polygons:
        poly.use_smooth = True
    for poly in low.data.polygons:
        poly.use_smooth = True
    return low, high


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


def centre_on_ground(obj):
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    obj.data.transform(Matrix.Translation((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs))))


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
    var = bm.faces.layers.float.new("leafvar")
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


# ----- UVs, baking, export ------------------------------------------------------------------------------------------

def atlas_uvs(objects, margin=0.003):
    """A shared "atlas" UV map over all of an asset's variants, for the bake (the old UVs stay as "grain")."""
    for obj in objects:
        layers = obj.data.uv_layers
        if "atlas" not in layers:
            layers.new(name="atlas")
        layers.active = layers["atlas"]
    select_only(objects)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=margin, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')


def use_gpu(scene):
    scene.render.engine = 'CYCLES'
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = 'OPTIX'
        prefs.get_devices()
        for device in prefs.devices:
            device.use = True
        scene.cycles.device = 'GPU'
    except Exception:
        scene.cycles.device = 'CPU'


def ground_for_ao(coll):
    """A plane under everything, so the occlusion bake darkens what touches the ground."""
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=20.0)
    return mesh_object("AO Ground", bm, coll)


def bake(name, lows, highs, size, ground):
    """Albedo, Normal and ORM (R occlusion, G roughness) for the lows, from the highs when there are any (selected to
    active), else from the lows' own materials."""
    scene = bpy.context.scene
    use_gpu(scene)
    scene.cycles.samples = 16
    folder = os.path.join(ART, name)
    os.makedirs(folder, exist_ok=True)
    images = {}
    for kind, colourspace in (("Albedo", 'sRGB'), ("Normal", 'Non-Color'), ("AO", 'Non-Color'), ("Rough", 'Non-Color')):
        img = bpy.data.images.new(f"{name}_{kind}", size, size, alpha=False)
        img.colorspace_settings.name = colourspace
        images[kind] = img
    if highs:
        target_material, tree, _ = new_material(f"{name} (target)")
        for low in lows:
            low.data.materials.clear()
            low.data.materials.append(target_material)
        targets = [tree.nodes.new("ShaderNodeTexImage")]
        trees = [tree]
    else:
        trees = list({m.node_tree for low in lows for m in low.data.materials})
        targets = [t.nodes.new("ShaderNodeTexImage") for t in trees]
    for tree, target in zip(trees, targets):
        uv = tree.nodes.new("ShaderNodeUVMap")
        uv.uv_map = "atlas"
        tree.links.new(uv.outputs["UV"], target.inputs["Vector"])
        tree.nodes.active = target
    # Bake every variant into the shared images in one go per pass (their atlas islands don't overlap).
    passes = (("Albedo", 'DIFFUSE', {'COLOR'}), ("Normal", 'NORMAL', None), ("AO", 'AO', None), ("Rough", 'ROUGHNESS', None))
    for kind, bake_type, pass_filter in passes:
        for target in targets:
            target.image = images[kind]
        first = True
        pairs = list(zip(lows, highs)) if highs else [(low, None) for low in lows]
        for low, high in pairs:
            if high is not None:
                select_only([high, low], active=low)
            else:
                select_only([low])
            ground.hide_render = kind != "AO"
            kwargs = dict(type=bake_type, use_clear=first, margin=8)
            if pass_filter:
                kwargs["pass_filter"] = pass_filter
            if bake_type == 'NORMAL':
                kwargs["normal_space"] = 'TANGENT'
            if high is not None:
                kwargs.update(use_selected_to_active=True, cage_extrusion=0.02, max_ray_distance=0.05)
            bpy.ops.object.bake(**kwargs)
            first = False
    ground.hide_render = True
    # ORM: occlusion in R, roughness in G, nothing metallic.
    orm = bpy.data.images.new(f"{name}_ORM", size, size, alpha=False)
    orm.colorspace_settings.name = 'Non-Color'
    ao = list(images["AO"].pixels)
    rough = list(images["Rough"].pixels)
    pixels = [0.0] * (size * size * 4)
    pixels[0::4] = ao[0::4]
    pixels[1::4] = rough[0::4]
    pixels[3::4] = [1.0] * (size * size)
    orm.pixels = pixels
    out = {"Albedo": images["Albedo"], "Normal": images["Normal"], "ORM": orm}
    for kind, img in out.items():
        img.filepath_raw = os.path.join(folder, f"{name}_{kind}.png")
        img.file_format = 'PNG'
        img.save()
    return out


def baked_material(name, images):
    material, tree, bsdf = new_material(name)
    links = tree.links
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "UVMap"  # the atlas, renamed by finish()
    uv.location = (-700, 0)
    albedo = node(tree, "ShaderNodeTexImage", (-400, 300))
    albedo.image = images["Albedo"]
    links.new(uv.outputs["UV"], albedo.inputs["Vector"])
    links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
    orm = node(tree, "ShaderNodeTexImage", (-400, 0))
    orm.image = images["ORM"]
    links.new(uv.outputs["UV"], orm.inputs["Vector"])
    split = node(tree, "ShaderNodeSeparateColor", (-100, 0))
    links.new(orm.outputs["Color"], split.inputs["Color"])
    links.new(split.outputs["Green"], bsdf.inputs["Roughness"])
    normal_tex = node(tree, "ShaderNodeTexImage", (-400, -300))
    normal_tex.image = images["Normal"]
    links.new(uv.outputs["UV"], normal_tex.inputs["Vector"])
    normal_map = node(tree, "ShaderNodeNormalMap", (-100, -300))
    normal_map.uv_map = "UVMap"
    links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def finish(objects, material):
    """Only the baked material and the atlas UVs (first, as Unity's UV0) go to the game."""
    for obj in objects:
        obj.data.materials.clear()
        obj.data.materials.append(material)
        layers = obj.data.uv_layers
        for layer in [l for l in layers if l.name != "atlas"]:
            layers.remove(layer)
        layers["atlas"].name = "UVMap"
        if "leafvar" in obj.data.attributes:
            obj.data.attributes.remove(obj.data.attributes["leafvar"])


def export(objects, name):
    select_only(objects)
    path = os.path.join(ART, name, f"{name}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                             axis_up='Y', bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
    return path


def make_asset(name, builder, size, coll):
    built = builder(coll)
    lows, highs = built if isinstance(built, tuple) else (built, [])
    atlas_uvs(lows, margin=0.004 if highs else 0.002)
    ground = ground_for_ao(coll)
    # The variants share the origin: spread them out for the bake so they don't shade each other.
    for i, low in enumerate(lows):
        low.location.x = i * 3.0
        if highs:
            highs[i].location.x = i * 3.0
    images = bake(name, lows, highs, size, ground)
    for low in lows:
        low.location.x = 0.0
    bpy.data.objects.remove(ground)
    for high in highs:
        bpy.data.objects.remove(high)
    material = baked_material(name, images)
    finish(lows, material)
    export(lows, name)
    return lows


# ----- Preview ------------------------------------------------------------------------------------------------------

def stage(rows):
    """The variants in rows on a meadow, under a low sun, seen from above at the game's angle."""
    scene = bpy.context.scene
    for z, objects in enumerate(rows):
        for i, obj in enumerate(objects):
            obj.location = ((i - (len(objects) - 1) / 2) * (1.7 if "Bush" in obj.name else 0.85), -z * 1.25 + 1.1, 0)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=12.0)
    ground = mesh_object("Meadow", bm, collection("Preview"))
    grass, tree, bsdf = new_material("Meadow")
    noise_node = node(tree, "ShaderNodeTexNoise", (-400, 0), Scale=6.0, Detail=8.0)
    colours = ramp(tree, (-150, 0), [(0.3, (0.1, 0.13, 0.04)), (0.7, (0.2, 0.22, 0.07))])
    tree.links.new(noise_node.outputs["Fac"], colours.inputs["Fac"])
    tree.links.new(colours.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    ground.data.materials.append(grass)
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), math.radians(10), math.radians(-40)))
    sun = bpy.context.active_object
    sun.data.energy = 3.5
    sun.data.angle = math.radians(3)
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.65, 0.8, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.9


def camera(location, target, lens=50):
    bpy.ops.object.camera_add(location=location)
    cam = bpy.context.active_object
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    bpy.context.scene.camera = cam
    return cam


def render(path, width, height):
    scene = bpy.context.scene
    use_gpu(scene)
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'AgX'
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rows = []
    if "bush" in WANTED:
        rows.append(make_asset("Berry Bush", build_bush, 2048, collection("Berry Bush")))
    if "rocks" in WANTED:
        rows.append(make_asset("Small Rocks", build_rocks, 1024, collection("Small Rocks")))
    if "sticks" in WANTED:
        rows.append(make_asset("Sticks", build_sticks, 1024, collection("Sticks")))
    stage(rows)
    camera((0.0, -4.6, 4.4), (0.0, -0.2, 0.2), lens=40)
    render(PREVIEW, 1600, 1000)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "Forage.blend"))
    print("FORAGE DONE", ART)


if __name__ == "__main__":
    main()
