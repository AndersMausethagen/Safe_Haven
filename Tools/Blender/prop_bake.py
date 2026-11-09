"""Shared Blender code for Safe Haven's baked props: modelling helpers, the scanned-texture bake materials and the bake
itself, so each asset script (forage_nodes.py, ore_nodes.py, ...) holds only its shapes.

Every asset is baked from its own materials (or from a sculpted high mesh onto a low one, selected to active) into one
atlas and written to Assets/_Project/Art/Gathering/<Asset>/ as an FBX of its variants and these textures, which the
game's Layered Lit shader reads:

- <Asset>_Albedo.png: colour, without light or occlusion.
- <Asset>_Normal.png: tangent-space normals (the sculpt's and the scans' relief).
- <Asset>_ORM.png: R ambient occlusion (its own geometry, darker near the ground), G roughness, B metallic (from an
  "SH Metal" node).
- <Asset>_Height.png: height for parallax occlusion (white high, mid-grey where a material has none).
- <Asset>_BlendMask.png: where the shader's live layers (moss, lichen) grow: R curvature (convex), G edge wear,
  B cavity, A coverage (faces turned to the sky, in patches).
- <Asset>_Emission.png: only for glowing assets, from the bake materials' "SH Glow" node.

A bake material marks its height with a node labelled "SH Height" (any node; its first output, 0 to 1) and its glow
with one labelled "SH Glow" (colour). The curvature, cavity and coverage come from the geometry (Cycles' pointiness,
ambient occlusion and the normal), so every material gets them.
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy
from mathutils import Matrix, Vector, noise

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else "D:/Safe Haven/Safe Haven"
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

def rock_material(name="Field Stone (bake)", texture="rock_boulder_dry", scale=1.6, saturation=0.6, brightness=0.6,
                  tint=(1.0, 1.0, 1.0), relief=0.5):
    """Scanned rock (a Poly Haven texture set), projected from six sides in object space so it has no seams. Its
    displacement map is the height (cracks and pits low) and, as a bump, the fine relief the normal map gets."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    coords = node(tree, "ShaderNodeTexCoord", (-900, 0))
    mapping = node(tree, "ShaderNodeMapping", (-700, 0))
    # The textures are about 2 m across; finer reads better on stones this small.
    mapping.inputs["Scale"].default_value = (scale, scale, scale)
    links.new(coords.outputs["Object"], mapping.inputs["Vector"])
    maps = {}
    for i, (kind, space) in enumerate((("diff", 'sRGB'), ("rough", 'Non-Color'), ("disp", 'Non-Color'))):
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.location = (-450, 300 - i * 300)
        tex.image = image(source(texture, kind), space)
        tex.projection = 'BOX'
        tex.projection_blend = 0.35
        links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        maps[kind] = tex
    # A little greyer and darker than the sunlit scan, like the Haven's other field stones.
    grade = node(tree, "ShaderNodeHueSaturation", (-100, 300), Saturation=saturation, Value=brightness)
    links.new(maps["diff"].outputs["Color"], grade.inputs["Color"])
    tinted = mix(tree, (150, 300), grade.outputs["Color"], rgb(tree, (-100, 500), tint), value(tree, (-100, 600), 1.0),
                 blend='MULTIPLY')
    links.new(tinted, bsdf.inputs["Base Color"])
    links.new(maps["rough"].outputs["Color"], bsdf.inputs["Roughness"])
    # Box-projected normal maps don't line up with tangents: the relief comes from the sculpt and a bump of the
    # displacement map.
    bump = node(tree, "ShaderNodeBump", (500, -300), Strength=relief, Distance=0.006)
    links.new(maps["disp"].outputs["Color"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    maps["disp"].label = "SH Height"
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
    for i, (kind, space) in enumerate((("diff", 'sRGB'), ("rough", 'Non-Color'), ("nor_gl", 'Non-Color'),
                                       ("disp", 'Non-Color'))):
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.location = (-450, 300 - i * 300)
        tex.image = image(source("bark_brown_02", kind), space)
        links.new(mapping.outputs["Vector"], tex.inputs["Vector"])
        maps[kind] = tex
    maps["disp"].label = "SH Height"
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
    wave.label = "SH Height"
    return material




# ----- Stones ------------------------------------------------------------------------------------------------------

def stone(name, size, rng, coll, high_subdiv=6, low_faces=260, splits=(5, 7), cut=(0.42, 0.62), lumps=0.035):
    """A split stone: an ellipsoid broken along a few planes, worn round, flattened under and a little sunk in, with
    lumps and fine grain (the high mesh), and a decimated copy of about low_faces faces (the low mesh the game uses)."""
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=high_subdiv, radius=1.0)
    seed = Vector((rng.uniform(0, 500), rng.uniform(0, 500), rng.uniform(0, 500)))
    sx, sy, sz = size[0], size[1], size[2] * 0.8
    planes = []
    for _ in range(rng.randint(*splits)):
        n = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.1, 1.0))).normalized()
        reach = abs(n.x) * sx + abs(n.y) * sy + abs(n.z) * sz
        planes.append((n, reach * rng.uniform(*cut)))
    for v in bm.verts:
        d = v.co.normalized()
        # Broad facets: the stone was split along planes and then worn round.
        facets = 0.0
        bumps = lumps * noise.fractal(d * 2.5 + seed, 0.6, 2.0, 4)
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
    decimate.ratio = min(1.0, low_faces / len(high.data.polygons))
    select_only([low])
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    for poly in high.data.polygons:
        poly.use_smooth = True
    for poly in low.data.polygons:
        poly.use_smooth = True
    return low, high



def centre_on_ground(obj):
    xs = [v.co.x for v in obj.data.vertices]
    ys = [v.co.y for v in obj.data.vertices]
    zs = [v.co.z for v in obj.data.vertices]
    obj.data.transform(Matrix.Translation((-(min(xs) + max(xs)) / 2, -(min(ys) + max(ys)) / 2, -min(zs))))




# ----- UVs, baking, export ------------------------------------------------------------------------------------------

def atlas_uvs(objects, margin=0.004):
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
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40.0)
    return mesh_object("AO Ground", bm, coll)


def labelled(tree, label):
    return next((n for n in tree.nodes if n.label == label), None)


def signals(tree, cavity_distance, dense, occlusion_distance):
    """The geometry signals the mask bakes read, built once into a bake material: curvature, edge wear, cavity and
    coverage from Cycles' pointiness, ambient occlusion and the normal; height and glow from the labelled nodes.
    Pointiness is per vertex: a dense sculpt bends a little at each one, a low mesh a lot, so its ranges differ."""
    links = tree.links
    geometry = node(tree, "ShaderNodeNewGeometry", (-1400, -1600))
    bend, sharp = ((0.5, 0.53), (0.512, 0.55)) if dense else ((0.5, 0.6), (0.535, 0.62))
    curvature = ramp(tree, (-1150, -1500), [(bend[0], (0, 0, 0)), (bend[1], (1, 1, 1))])
    links.new(geometry.outputs["Pointiness"], curvature.inputs["Fac"])
    edge_noise = node(tree, "ShaderNodeTexNoise", (-1400, -1900), Scale=40.0, Detail=6.0)
    edge = ramp(tree, (-1150, -1700), [(sharp[0], (0, 0, 0)), (sharp[1], (1, 1, 1))])
    links.new(geometry.outputs["Pointiness"], edge.inputs["Fac"])
    edge_broken = node(tree, "ShaderNodeMath", (-900, -1750))
    edge_broken.operation = 'MULTIPLY'
    links.new(edge.outputs["Color"], edge_broken.inputs[0])
    links.new(edge_noise.outputs["Fac"], edge_broken.inputs[1])
    ao = tree.nodes.new("ShaderNodeAmbientOcclusion")
    ao.location = (-1400, -2100)
    ao.samples = 16
    ao.only_local = True
    ao.inputs["Distance"].default_value = cavity_distance
    cavity = ramp(tree, (-1150, -2100), [(0.45, (1, 1, 1)), (0.95, (0, 0, 0))])
    links.new(ao.outputs["AO"], cavity.inputs["Fac"])
    # Occlusion: the object's own geometry only (a low mesh lying on the high one's flat faces would block every ray),
    # darkened towards the ground it stands on.
    shade = tree.nodes.new("ShaderNodeAmbientOcclusion")
    shade.location = (-1400, -3300)
    shade.samples = 32
    shade.only_local = True
    shade.inputs["Distance"].default_value = occlusion_distance
    height_split = node(tree, "ShaderNodeSeparateXYZ", (-1400, -3500))
    links.new(geometry.outputs["Position"], height_split.inputs["Vector"])
    grounded = ramp(tree, (-1150, -3500), [(0.0, (0.35, 0.35, 0.35)), (0.25, (1, 1, 1))])
    links.new(height_split.outputs["Z"], grounded.inputs["Fac"])
    occlusion = node(tree, "ShaderNodeMath", (-900, -3400))
    occlusion.operation = 'MULTIPLY'
    links.new(shade.outputs["AO"], occlusion.inputs[0])
    links.new(grounded.outputs["Color"], occlusion.inputs[1])
    split = node(tree, "ShaderNodeSeparateXYZ", (-1150, -2350))
    links.new(geometry.outputs["Normal"], split.inputs["Vector"])
    up = ramp(tree, (-950, -2350), [(0.25, (0, 0, 0)), (0.85, (1, 1, 1))])
    links.new(split.outputs["Z"], up.inputs["Fac"])
    patches = node(tree, "ShaderNodeTexNoise", (-1150, -2600), Scale=6.0, Detail=4.0)
    patch_ramp = ramp(tree, (-950, -2600), [(0.35, (0.25, 0.25, 0.25)), (0.65, (1, 1, 1))])
    links.new(patches.outputs["Fac"], patch_ramp.inputs["Fac"])
    coverage = node(tree, "ShaderNodeMath", (-700, -2450))
    coverage.operation = 'MULTIPLY'
    links.new(up.outputs["Color"], coverage.inputs[0])
    links.new(patch_ramp.outputs["Color"], coverage.inputs[1])
    bsdf = next((n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is not None and bsdf.inputs["Base Color"].links:
        albedo = bsdf.inputs["Base Color"].links[0].from_socket
    else:
        albedo = rgb(tree, (-900, -3100), tuple(bsdf.inputs["Base Color"].default_value[:3]) if bsdf else (0.5, 0.5, 0.5))
    height_node = labelled(tree, "SH Height")
    glow_node = labelled(tree, "SH Glow")
    metal_node = labelled(tree, "SH Metal")
    return {
        "Albedo": albedo,
        "Occlusion": occlusion.outputs["Value"],
        "Curvature": curvature.outputs["Color"],
        "Edge": edge_broken.outputs["Value"],
        "Cavity": cavity.outputs["Color"],
        "Coverage": coverage.outputs["Value"],
        "Height": height_node.outputs[0] if height_node else value(tree, (-900, -2800), 0.5),
        "Glow": glow_node.outputs[0] if glow_node else rgb(tree, (-900, -2900), (0, 0, 0)),
        "Metal": metal_node.outputs[0] if metal_node else value(tree, (-900, -3000), 0.0),
    }


def emit(tree, socket):
    """Show socket as the material's emission for an EMIT bake; returns how to put the material back."""
    out = next(n for n in tree.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output)
    original = out.inputs["Surface"].links[0].from_socket if out.inputs["Surface"].links else None
    emission = tree.nodes.new("ShaderNodeEmission")
    emission.inputs["Strength"].default_value = 1.0
    tree.links.new(socket, emission.inputs["Color"])
    tree.links.new(emission.outputs["Emission"], out.inputs["Surface"])

    def restore():
        tree.nodes.remove(emission)
        if original is not None:
            tree.links.new(original, out.inputs["Surface"])
    return restore


def pixels(img):
    data = numpy.empty(img.size[0] * img.size[1] * 4, dtype=numpy.float32)
    img.pixels.foreach_get(data)
    return data.reshape(-1, 4)


def pack(name, size, channels, colourspace='Non-Color'):
    """A new RGBA image from four single-channel arrays (None leaves 0, alpha 1)."""
    img = bpy.data.images.new(name, size, size, alpha=True)
    img.colorspace_settings.name = colourspace
    data = numpy.zeros((size * size, 4), dtype=numpy.float32)
    data[:, 3] = 1.0
    for i, channel in enumerate(channels):
        if channel is not None:
            data[:, i] = channel
    img.pixels.foreach_set(data.ravel())
    return img


def bake(name, lows, highs, size, ground, glow=False, cavity_distance=0.05):
    """Every map the game reads (see the module's notes) for the lows, from the highs when there are any (selected to
    active), else from the lows' own materials."""
    scene = bpy.context.scene
    use_gpu(scene)
    scene.cycles.samples = 16
    folder = os.path.join(ART, name)
    os.makedirs(folder, exist_ok=True)
    sources = highs or lows
    source_trees = list({m.node_tree for obj in sources for m in obj.data.materials})
    if highs:
        target_material, tree, _ = new_material(f"{name} (target)")
        for low in lows:
            low.data.materials.clear()
            low.data.materials.append(target_material)
        target_trees = [tree]
    else:
        target_trees = source_trees
    targets = []
    for tree in target_trees:
        target = tree.nodes.new("ShaderNodeTexImage")
        uv = tree.nodes.new("ShaderNodeUVMap")
        uv.uv_map = "atlas"
        tree.links.new(uv.outputs["UV"], target.inputs["Vector"])
        tree.nodes.active = target
        targets.append(target)
    occlusion_distance = max(0.15, cavity_distance * 5)
    signal_sockets = {tree: signals(tree, cavity_distance, bool(highs), occlusion_distance) for tree in source_trees}

    def run(kind, bake_type, colourspace='Non-Color', pass_filter=None, signal=None, with_ground=False):
        img = bpy.data.images.new(f"{name}_{kind}", size, size, alpha=False)
        img.colorspace_settings.name = colourspace
        for target in targets:
            target.image = img
        restores = [emit(tree, signal_sockets[tree][signal]) for tree in source_trees] if signal else []
        ground.hide_render = not with_ground
        pairs = list(zip(lows, highs)) if highs else [(low, None) for low in lows]
        for index, (low, high) in enumerate(pairs):
            select_only([high, low] if high else [low], active=low)
            kwargs = dict(type=bake_type, use_clear=index == 0, margin=16)
            if pass_filter:
                kwargs["pass_filter"] = pass_filter
            if bake_type == 'NORMAL':
                kwargs["normal_space"] = 'TANGENT'
            if high is not None:
                kwargs.update(use_selected_to_active=True, cage_extrusion=0.03, max_ray_distance=0.08)
            bpy.ops.object.bake(**kwargs)
        for restore in restores:
            restore()
        ground.hide_render = True
        return img

    # The base colour itself (a diffuse-colour bake would leave metals black).
    albedo = run("Albedo", 'EMIT', 'sRGB', signal="Albedo")
    normal = run("Normal", 'NORMAL')
    ao = pixels(run("AO", 'EMIT', signal="Occlusion"))[:, 0]
    rough = pixels(run("Rough", 'ROUGHNESS'))[:, 0]
    height = pixels(run("HeightRaw", 'EMIT', signal="Height"))[:, 0]
    metal = pixels(run("Metal", 'EMIT', signal="Metal"))[:, 0] if any(labelled(t, "SH Metal") for t in source_trees) else None
    mask = [pixels(run(kind, 'EMIT', signal=kind))[:, 0] for kind in ("Curvature", "Edge", "Cavity", "Coverage")]
    out = {
        "Albedo": albedo,
        "Normal": normal,
        "ORM": pack(f"{name}_ORM", size, (ao, rough, metal)),
        "Height": pack(f"{name}_Height", size, (height, height, height)),
        "BlendMask": pack(f"{name}_BlendMask", size, mask),
    }
    if glow:
        out["Emission"] = run("Emission", 'EMIT', 'sRGB', signal="Glow")
    for kind, img in out.items():
        img.filepath_raw = os.path.join(folder, f"{name}_{kind}.png")
        img.file_format = 'PNG'
        img.save()
    return out


def baked_material(name, images):
    """What the variants wear once baked, for the preview: the maps on the atlas UVs."""
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
    if "Emission" in images:
        glow = node(tree, "ShaderNodeTexImage", (-400, -600))
        glow.image = images["Emission"]
        links.new(uv.outputs["UV"], glow.inputs["Vector"])
        links.new(glow.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 4.0
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
        for attribute in [a.name for a in obj.data.attributes if a.name.startswith("sh_")]:
            obj.data.attributes.remove(obj.data.attributes[attribute])


def export(objects, name):
    select_only(objects)
    path = os.path.join(ART, name, f"{name}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                             axis_up='Y', bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
    return path


def make_asset(name, builder, size, coll, glow=False, cavity_distance=0.05):
    """Build an asset's variants, bake them into one atlas, export them. builder(coll) returns the variants (lows), or
    (lows, highs) to bake sculpted highs onto them."""
    built = builder(coll)
    lows, highs = built if isinstance(built, tuple) else (built, [])
    atlas_uvs(lows, margin=0.006 if highs else 0.003)
    ground = ground_for_ao(coll)
    # The variants share the origin: spread them out for the bake so they don't shade each other.
    for i, low in enumerate(lows):
        low.location.x = i * 5.0
        if highs:
            highs[i].location.x = i * 5.0
    images = bake(name, lows, highs, size, ground, glow=glow, cavity_distance=cavity_distance)
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

def stage(rows, spacing=0.85, row_spacing=1.25):
    """The variants in rows on a meadow, under a low sun. rows: lists of objects, or (objects, spacing) pairs."""
    scene = bpy.context.scene
    y = 0.0
    for row in rows:
        objects, gap = row if isinstance(row, tuple) else (row, spacing)
        for i, obj in enumerate(objects):
            obj.location = ((i - (len(objects) - 1) / 2) * gap, y, 0)
        y -= max(row_spacing, gap * 0.8)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30.0)
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
    return -y


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
