"""The skigard: the old Norwegian roundpole fence, and a gate for it, modelled and textured in Blender for Safe Haven.

A skigard is split spruce rails laid slanting between pairs of round, young spruce posts, held by bands of softened
spruce twig wound round each pair, about up under a man's arms in height. Here one piece is 4 m long (like the game's
other walls) with four post pairs; its rails lean at 34 degrees and repeat every 4 m, so pieces placed end to end join
into one fence. The end pairs at both ends are the same, so where two pieces meet their posts overlap exactly.

The gate piece has two heavy gate posts with a swinging gate of split rails between them, and short runs of skigard to
the 4 m. Its leaf is its own object with its pivot on the hinge.

The wood is old, dark and worn spruce: grey-brown, deeply cracked, rotting dark and mossy near the ground, with lichen
and bark left on the posts, made procedurally and baked
into one 2048 colour and normal texture (both pieces share it). Writes Assets/_Project/Art/Haven/Skigard/ (Skigard.fbx,
Skigard Gate.fbx, Skigard_Albedo.png, Skigard_Normal.png), icons to Assets/_Project/UI/BuildingIcons, a preview
render to the path given, and the .blend beside this script.

Run: blender -b --factory-startup --python Tools/Blender/skigard.py -- <project root> <preview.png>
"""
import math
import os
import random
import sys

import bpy
from mathutils import Euler, Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else os.getcwd()
PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "skigard_preview.png")
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Haven", "Skigard")
ICONS = os.path.join(ROOT, "Assets", "_Project", "UI", "BuildingIcons")

LENGTH = 4.0
PAIRS = [-2.0, -2.0 / 3.0, 2.0 / 3.0, 2.0]
PAIR_GAP = 0.075          # each post this far either side of the rails
RAIL_ANGLE = math.radians(34)
RAIL_TOP = 1.28
RAIL_STEP = LENGTH / 15.0  # 15 rails per piece, so the pattern repeats every 4 m
BANDS = (0.34, 0.74, 1.12)

rng = random.Random(7)


# ----- Geometry -----------------------------------------------------------------------------------------------------

def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def cylinder(name, start, end, radius, sides, squash=1.0, material=None, collection=None):
    """A cylinder from start to end, squashed across (split rails); its UVs run along its length (V), which the wood's
    grain follows. Placed by transforming the mesh itself, so the object keeps an identity transform."""
    start, end = Vector(start), Vector(end)
    axis = end - start
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=axis.length, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    turn = Vector((0, 0, 1)).rotation_difference(axis.normalized()).to_matrix().to_4x4()
    obj.data.transform(Matrix.Translation((start + end) / 2) @ turn @ Matrix.Diagonal((1.0, squash, 1.0, 1.0)))
    if material:
        obj.data.materials.append(material)
    move(obj, collection)
    return obj


def ring(name, at, material, collection, tilt=0.0, squeeze=(0.5, 1.0), major=0.105, minor=0.013):
    """A twisted twig band: a thin, flattened ring."""
    bpy.ops.mesh.primitive_torus_add(major_radius=major, minor_radius=minor, major_segments=20, minor_segments=6,
                                     location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    turn = Euler((math.radians(tilt), 0, 0)).to_matrix().to_4x4()
    obj.data.transform(Matrix.Translation(Vector(at)) @ turn @ Matrix.Diagonal((squeeze[0], squeeze[1], 1.0, 1.0)))
    obj.data.materials.append(material)
    move(obj, collection)
    return obj


def band(name, x, z, material, collection, tilt):
    """A twisted spruce-twig band round a pair of posts."""
    return ring(name, (x, 0, z), material, collection, tilt)


def move(obj, collection):
    if collection is None:
        return
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)


def post_pair(prefix, x, materials, collection, seed):
    """Two round posts and their bands; the same seed makes the same pair (the shared end pairs)."""
    local = random.Random(seed)
    parts = []
    for side in (-1, 1):
        height = 1.66 + local.uniform(0.0, 0.14)
        lean = Vector((local.uniform(-0.03, 0.03), local.uniform(-0.02, 0.02), 0))
        radius = 0.034 + local.uniform(0.0, 0.008)
        base = Vector((x, side * PAIR_GAP, -0.15))
        parts.append(cylinder(f"{prefix} Post", base, base + lean + Vector((0, 0, height + 0.15)), radius, 10,
                              material=materials["post"], collection=collection))
    for i, z in enumerate(BANDS):
        parts.append(band(f"{prefix} Band {i}", x, z + local.uniform(-0.03, 0.03), materials["band"], collection,
                          local.uniform(-12, 12)))
    return parts


def rails(prefix, x_from, x_to, materials, collection, seed):
    """The slanting split rails, clipped to [x_from, x_to]. Rails start every RAIL_STEP along the whole fence line, so
    pieces placed end to end continue each other's rails."""
    parts = []
    run = RAIL_TOP / math.tan(RAIL_ANGLE)
    first = math.floor((x_from - run) / RAIL_STEP)
    last = math.ceil(x_to / RAIL_STEP)
    for k in range(first, last + 1):
        bottom = Vector((k * RAIL_STEP, 0.0, 0.06))
        top = bottom + Vector((run, 0.0, RAIL_TOP - 0.06))
        # Clip the line to the piece.
        a, b = bottom.copy(), top.copy()
        if b.x <= x_from or a.x >= x_to:
            continue
        if a.x < x_from:
            a = a.lerp(b, (x_from - a.x) / (b.x - a.x))
        if b.x > x_to:
            b = bottom.lerp(top, (x_to - bottom.x) / (top.x - bottom.x))
        if (b - a).length < 0.18:
            continue
        # The rail sits on the one below it, alternating a little to either side in the slot between the posts.
        offset = Vector((0, 0.012 if k % 2 else -0.012, 0))
        # Keyed by the rail's place on the line, so the same rail in two pieces lines up.
        wobble = Vector((0, 0, random.Random(k * 7919 + 3).uniform(-0.015, 0.015)))
        parts.append(cylinder(f"{prefix} Rail {k}", a + offset + wobble, b + offset + wobble, 0.05, 7, squash=0.58,
                              material=materials["rail"], collection=collection))
    return parts


def skigard(materials, collection):
    parts = []
    for i, x in enumerate(PAIRS):
        # The two end pairs are the same pair, so neighbouring pieces' posts overlap exactly.
        seed = 1000 if abs(x) >= 1.99 else 2000 + i
        parts += post_pair(f"Pair {i}", x, materials, collection, seed)
    parts += rails("Skigard", -LENGTH / 2, LENGTH / 2, materials, collection, 11)
    return join(parts, "Skigard")


def gate(materials, frame_collection, leaf_collection):
    hinge, latch = -1.13, 1.13
    frame = []
    for x in (-1.2, 1.2):
        frame.append(cylinder("Gate Post", (x, 0, -0.2), (x + rng.uniform(-0.02, 0.02), 0, 1.92), 0.075, 12,
                              material=materials["post"], collection=frame_collection))
    for i, x in enumerate((-2.0, 2.0)):
        frame += post_pair(f"Gate End {i}", x, materials, frame_collection, 1000)
    frame += rails("Gate Left", -2.0, -1.24, materials, frame_collection, 21)
    frame += rails("Gate Right", 1.24, 2.0, materials, frame_collection, 22)
    # The hinges: twig rings round the gate post and the leaf's hinge stile.
    for z in (0.32, 1.05):
        frame.append(ring("Hinge", (-1.165, 0, z), materials["band"], frame_collection, squeeze=(0.75, 0.62),
                          major=0.11, minor=0.014))
    frame_obj = join(frame, "Gate Frame")

    leaf = []
    width = latch - hinge
    for x in (hinge + 0.05, latch - 0.04):
        leaf.append(cylinder("Stile", (x, 0, 0.12), (x, 0, 1.3), 0.048, 7, squash=0.62, material=materials["rail"],
                             collection=leaf_collection))
    for z in (0.26, 0.6, 0.94, 1.24):
        leaf.append(cylinder("Gate Rail", (hinge + 0.02, 0.03, z), (latch, 0.03, z + rng.uniform(-0.02, 0.02)), 0.042, 7,
                             squash=0.6, material=materials["rail"], collection=leaf_collection))
    leaf.append(cylinder("Brace", (hinge + 0.08, -0.03, 0.22), (latch - 0.06, -0.03, 1.18), 0.042, 7, squash=0.6,
                         material=materials["rail"], collection=leaf_collection))
    leaf_obj = join(leaf, "Gate Leaf")
    # The leaf turns on its hinge.
    set_origin(leaf_obj, Vector((hinge, 0, 0)))
    return frame_obj, leaf_obj, width


def join(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = name
    joined.data.name = name
    return joined


def set_origin(obj, point):
    cursor = bpy.context.scene.cursor
    cursor.location = point
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    cursor.location = (0, 0, 0)


# ----- Materials: weathered spruce, bark, twig bands ----------------------------------------------------------------

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


def mix(tree, location, a, b, factor):
    n = tree.nodes.new("ShaderNodeMix")
    n.data_type = 'RGBA'
    n.location = location
    links = tree.links
    links.new(factor, n.inputs["Factor"])
    links.new(a, n.inputs["A"])
    links.new(b, n.inputs["B"])
    return n.outputs["Result"]


def wood(name, base_stops, bark=0.0, lichen=0.35, grain_scale=(14.0, 2.2, 1.0)):
    """Weathered spruce: fine grain along the length, cracks, lichen spots; bark left in patches on round posts."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    links = tree.links
    out = node(tree, "ShaderNodeOutputMaterial", (1400, 0))
    bsdf = node(tree, "ShaderNodeBsdfPrincipled", (1100, 0), Roughness=0.86)
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-1200, 0)
    mapping = node(tree, "ShaderNodeMapping", (-1000, 0))
    mapping.inputs["Scale"].default_value = grain_scale
    links.new(uv.outputs["UV"], mapping.inputs["Vector"])

    # Grain: long fibres along V, wavering.
    warp = node(tree, "ShaderNodeTexNoise", (-800, 250), Scale=3.0, Detail=4.0)
    links.new(mapping.outputs["Vector"], warp.inputs["Vector"])
    wave = tree.nodes.new("ShaderNodeTexWave")
    wave.location = (-600, 200)
    wave.bands_direction = 'X'
    wave.inputs["Scale"].default_value = 6.0
    wave.inputs["Distortion"].default_value = 7.0
    wave.inputs["Detail"].default_value = 3.0
    links.new(mapping.outputs["Vector"], wave.inputs["Vector"])
    # Cracks: thin dark lines along the length.
    crack_map = node(tree, "ShaderNodeMapping", (-800, -250))
    crack_map.inputs["Scale"].default_value = (3.0, 0.25, 1.0)
    links.new(mapping.outputs["Vector"], crack_map.inputs["Vector"])
    cracks = tree.nodes.new("ShaderNodeTexVoronoi")
    cracks.location = (-600, -250)
    cracks.feature = 'DISTANCE_TO_EDGE'
    cracks.inputs["Scale"].default_value = 13.0
    links.new(crack_map.outputs["Vector"], cracks.inputs["Vector"])
    crack_mask = ramp(tree, (-400, -250), [(0.0, (0, 0, 0)), (0.06, (1, 1, 1))])
    links.new(cracks.outputs["Distance"], crack_mask.inputs["Fac"])

    # Colour: grey weathered wood by the grain, darker in cracks.
    blotch = node(tree, "ShaderNodeTexNoise", (-600, 450), Scale=1.6, Detail=6.0, Roughness=0.6)
    links.new(mapping.outputs["Vector"], blotch.inputs["Vector"])
    shade = node(tree, "ShaderNodeMath", (-400, 350))
    shade.operation = 'MULTIPLY_ADD'
    shade.inputs[1].default_value = 0.45
    links.new(wave.outputs["Fac"], shade.inputs[0])
    links.new(blotch.outputs["Fac"], shade.inputs[2])
    colour = ramp(tree, (-200, 350), base_stops)
    links.new(shade.outputs["Value"], colour.inputs["Fac"])
    cracked = mix(tree, (50, 250), node(tree, "ShaderNodeRGB", (-200, 120)).outputs[0], colour.outputs["Color"],
                  crack_mask.outputs["Color"])
    tree.nodes[-2].outputs[0].default_value = (0.02, 0.017, 0.014, 1.0)  # the crack colour (the RGB node just made)

    result = cracked
    if bark > 0:
        # Bark: rough, darker brown-grey, in patches on the round posts.
        bark_noise = node(tree, "ShaderNodeTexNoise", (-400, 650), Scale=2.2, Detail=5.0)
        links.new(mapping.outputs["Vector"], bark_noise.inputs["Vector"])
        bark_mask = ramp(tree, (-200, 650), [(0.55 - bark * 0.2, (0, 0, 0)), (0.6 - bark * 0.2, (1, 1, 1))])
        links.new(bark_noise.outputs["Fac"], bark_mask.inputs["Fac"])
        bark_tex = node(tree, "ShaderNodeTexNoise", (-400, 850), Scale=40.0, Detail=8.0, Roughness=0.7)
        links.new(mapping.outputs["Vector"], bark_tex.inputs["Vector"])
        bark_colour = ramp(tree, (-200, 850), [(0.3, (0.12, 0.1, 0.08)), (0.7, (0.3, 0.26, 0.21))])
        links.new(bark_tex.outputs["Fac"], bark_colour.inputs["Fac"])
        result = mix(tree, (300, 500), result, bark_colour.outputs["Color"], bark_mask.outputs["Color"])

    if lichen > 0:
        # Lichen: pale grey-green and a little yellow, in spots.
        lichen_noise = node(tree, "ShaderNodeTexNoise", (-400, -550), Scale=9.0, Detail=6.0)
        links.new(mapping.outputs["Vector"], lichen_noise.inputs["Vector"])
        lichen_mask = ramp(tree, (-200, -550), [(0.72 - lichen * 0.15, (0, 0, 0)), (0.76 - lichen * 0.15, (1, 1, 1))])
        links.new(lichen_noise.outputs["Fac"], lichen_mask.inputs["Fac"])
        lichen_colour = ramp(tree, (-200, -750), [(0.0, (0.48, 0.52, 0.4)), (1.0, (0.62, 0.6, 0.34))])
        links.new(lichen_noise.outputs["Fac"], lichen_colour.inputs["Fac"])
        result = mix(tree, (500, 200), result, lichen_colour.outputs["Color"], lichen_mask.outputs["Color"])

    # Old and worn: damp and rotting dark near the ground, with moss creeping up from it.
    position = tree.nodes.new("ShaderNodeNewGeometry")
    position.location = (-600, -950)
    split = tree.nodes.new("ShaderNodeSeparateXYZ")
    split.location = (-400, -950)
    links.new(position.outputs["Position"], split.inputs["Vector"])
    patchy = node(tree, "ShaderNodeTexNoise", (-400, -1150), Scale=4.0, Detail=5.0)
    links.new(position.outputs["Position"], patchy.inputs["Vector"])
    reach = node(tree, "ShaderNodeMath", (-200, -1000))
    reach.operation = 'MULTIPLY_ADD'
    reach.inputs[1].default_value = 0.5
    links.new(patchy.outputs["Fac"], reach.inputs[0])
    links.new(split.outputs["Z"], reach.inputs[2])
    damp = ramp(tree, (0, -1000), [(0.3, (1, 1, 1)), (0.7, (0, 0, 0))])
    links.new(reach.outputs["Value"], damp.inputs["Fac"])
    rot = node(tree, "ShaderNodeRGB", (0, -1200))
    rot.outputs[0].default_value = (0.03, 0.028, 0.02, 1.0)
    result = mix(tree, (300, -900), result, rot.outputs[0], damp.outputs["Color"])
    moss_mask = ramp(tree, (0, -1400), [(0.35, (1, 1, 1)), (0.5, (0, 0, 0))])
    links.new(reach.outputs["Value"], moss_mask.inputs["Fac"])
    moss_noise = node(tree, "ShaderNodeTexNoise", (-200, -1400), Scale=14.0, Detail=6.0)
    links.new(mapping.outputs["Vector"], moss_noise.inputs["Vector"])
    moss_colour = ramp(tree, (0, -1600), [(0.3, (0.05, 0.07, 0.02)), (0.75, (0.11, 0.14, 0.04))])
    links.new(moss_noise.outputs["Fac"], moss_colour.inputs["Fac"])
    moss_cut = node(tree, "ShaderNodeMath", (200, -1500))
    moss_cut.operation = 'MULTIPLY'
    links.new(moss_mask.outputs["Color"], moss_cut.inputs[0])
    links.new(moss_noise.outputs["Fac"], moss_cut.inputs[1])
    result = mix(tree, (500, -1200), result, moss_colour.outputs["Color"], moss_cut.outputs["Value"])

    links.new(result, bsdf.inputs["Base Color"])

    # Relief: the grain, deeper cracks.
    height = node(tree, "ShaderNodeMath", (300, -300))
    height.operation = 'MULTIPLY'
    links.new(wave.outputs["Fac"], height.inputs[0])
    links.new(crack_mask.outputs["Color"], height.inputs[1])
    bump = node(tree, "ShaderNodeBump", (800, -300), Strength=0.85, Distance=0.03)
    links.new(height.outputs["Value"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def withy(name):
    """Bands of softened spruce twig, twisted: brown with a spiral of fibres."""
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    tree = material.node_tree
    tree.nodes.clear()
    links = tree.links
    out = node(tree, "ShaderNodeOutputMaterial", (900, 0))
    bsdf = node(tree, "ShaderNodeBsdfPrincipled", (600, 0), Roughness=0.8)
    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-700, 0)
    mapping = node(tree, "ShaderNodeMapping", (-500, 0))
    mapping.inputs["Scale"].default_value = (30.0, 6.0, 1.0)
    mapping.inputs["Rotation"].default_value = (0, 0, math.radians(35))
    links.new(uv.outputs["UV"], mapping.inputs["Vector"])
    wave = tree.nodes.new("ShaderNodeTexWave")
    wave.location = (-300, 0)
    wave.bands_direction = 'X'
    wave.inputs["Scale"].default_value = 2.0
    wave.inputs["Distortion"].default_value = 2.0
    links.new(mapping.outputs["Vector"], wave.inputs["Vector"])
    colour = ramp(tree, (0, 100), [(0.2, (0.06, 0.045, 0.028)), (0.8, (0.17, 0.12, 0.07))])
    links.new(wave.outputs["Fac"], colour.inputs["Fac"])
    links.new(colour.outputs["Color"], bsdf.inputs["Base Color"])
    bump = node(tree, "ShaderNodeBump", (300, -200), Strength=0.6, Distance=0.01)
    links.new(wave.outputs["Fac"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


# ----- UVs, baking, export ------------------------------------------------------------------------------------------

def atlas_uvs(objects):
    """Keep each part's own UVs as "grain" (the procedural wood follows them) and lay out a shared "atlas" for the bake."""
    for obj in objects:
        layers = obj.data.uv_layers
        layers[0].name = "grain"
        atlas = layers.new(name="atlas")
        layers.active = atlas
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004, scale_to_bounds=False)
    bpy.ops.uv.pack_islands(margin=0.004)
    bpy.ops.object.mode_set(mode='OBJECT')


def bake(objects, materials, size=2048):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 8
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = 'OPTIX'
        prefs.get_devices()
        for device in prefs.devices:
            device.use = True
        scene.cycles.device = 'GPU'
    except Exception:
        scene.cycles.device = 'CPU'
    images = {}
    for kind, colourspace in (("Albedo", 'sRGB'), ("Normal", 'Non-Color')):
        image = bpy.data.images.new(f"Skigard_{kind}", size, size, alpha=False)
        image.colorspace_settings.name = colourspace
        images[kind] = image
    targets = []
    for material in materials:
        tree = material.node_tree
        uv = tree.nodes.new("ShaderNodeUVMap")
        uv.uv_map = "atlas"
        uv.location = (1100, -500)
        target = tree.nodes.new("ShaderNodeTexImage")
        target.location = (1300, -500)
        tree.links.new(uv.outputs["UV"], target.inputs["Vector"])
        tree.nodes.active = target
        targets.append(target)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    scene.render.bake.margin = 12
    for target in targets:
        target.image = images["Albedo"]
    bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, use_clear=True)
    for target in targets:
        target.image = images["Normal"]
    bpy.ops.object.bake(type='NORMAL', normal_space='TANGENT', use_clear=True)
    os.makedirs(ART, exist_ok=True)
    for kind, image in images.items():
        image.filepath_raw = os.path.join(ART, f"Skigard_{kind}.png")
        image.file_format = 'PNG'
        image.save()
    return images


def baked_material(images):
    """What the pieces wear once baked: the two textures on the atlas UVs."""
    material = bpy.data.materials.new("Skigard")
    material.use_nodes = True
    tree = material.node_tree
    bsdf = tree.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = 0.85
    albedo = tree.nodes.new("ShaderNodeTexImage")
    albedo.image = images["Albedo"]
    tree.links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
    normal_tex = tree.nodes.new("ShaderNodeTexImage")
    normal_tex.image = images["Normal"]
    normal_map = tree.nodes.new("ShaderNodeNormalMap")
    tree.links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
    tree.links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def finish(objects, material):
    for obj in objects:
        obj.data.materials.clear()
        obj.data.materials.append(material)
        layers = obj.data.uv_layers
        layers.remove(layers["grain"])
        layers[0].name = "UVMap"
        for poly in obj.data.polygons:
            poly.use_smooth = True
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(50))


def export(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(ART, f"{name}.fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH'},
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)


# ----- Preview and icons --------------------------------------------------------------------------------------------

def stage(fence, frame, leaf):
    """A short run of fence with the gate half open on a meadow, lit by a low sun, seen from above like the game."""
    scene = bpy.context.scene
    copies = []
    for i, x in enumerate((-8.0, -4.0, 4.0, 8.0)):
        copy = fence.copy()
        copy.data = fence.data
        copy.location = (x, 0, 0)
        scene.collection.objects.link(copy)
        copies.append(copy)
    fence.hide_render = True
    leaf.rotation_euler = (0, 0, math.radians(-55))
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
    ground = bpy.context.active_object
    grass = bpy.data.materials.new("Meadow")
    grass.use_nodes = True
    tree = grass.node_tree
    bsdf = tree.nodes["Principled BSDF"]
    noise = tree.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 3.0
    colours = ramp(tree, (0, 0), [(0.3, (0.12, 0.17, 0.06)), (0.7, (0.22, 0.27, 0.1))])
    tree.links.new(noise.outputs["Fac"], colours.inputs["Fac"])
    tree.links.new(colours.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    ground.data.materials.append(grass)
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(52), math.radians(8), math.radians(-35)))
    sun = bpy.context.active_object
    sun.data.energy = 3.0
    sun.data.angle = math.radians(2.5)
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.65, 0.8, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    return copies + [ground, sun]


def camera(location, target, lens=50):
    bpy.ops.object.camera_add(location=location)
    cam = bpy.context.active_object
    direction = Vector(target) - Vector(location)
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    cam.data.lens = lens
    bpy.context.scene.camera = cam
    return cam


def render(path, width, height, transparent=False):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.film_transparent = transparent
    scene.view_settings.view_transform = 'AgX'
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    clear()
    fence_parts = bpy.data.collections.new("Skigard")
    gate_frame = bpy.data.collections.new("Gate")
    for collection in (fence_parts, gate_frame):
        bpy.context.scene.collection.children.link(collection)
    materials = {
        "rail": wood("Split Spruce", [(0.2, (0.055, 0.05, 0.043)), (0.5, (0.105, 0.095, 0.08)), (0.85, (0.17, 0.155, 0.13))],
                     lichen=0.55),
        "post": wood("Spruce Post", [(0.2, (0.04, 0.035, 0.03)), (0.6, (0.085, 0.075, 0.062)), (0.9, (0.14, 0.125, 0.105))],
                     bark=1.0, lichen=0.5, grain_scale=(6.0, 3.0, 1.0)),
        "band": withy("Twig Band"),
    }
    fence = skigard(materials, fence_parts)
    frame, leaf, _ = gate(materials, gate_frame, gate_frame)
    pieces = [fence, frame, leaf]
    atlas_uvs(pieces)
    images = bake(pieces, list(materials.values()))
    finish(pieces, baked_material(images))
    export([fence], "Skigard")
    export([frame, leaf], "Skigard Gate")

    # Icons: each piece alone on transparent, three-quarter view.
    os.makedirs(ICONS, exist_ok=True)
    camera((5.2, -6.5, 4.2), (0, 0, 0.6), lens=55)
    for shown, name in ((fence, "Skigard"), (frame, "Skigard Gate")):
        for obj in pieces:
            obj.hide_render = obj is not shown and not (shown is frame and obj is leaf)
        bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), 0, math.radians(-30)))
        light = bpy.context.active_object
        light.data.energy = 4.0
        render(os.path.join(ICONS, f"{name}.png"), 256, 256, transparent=True)
        bpy.data.objects.remove(light)
    for obj in pieces:
        obj.hide_render = False

    stage(fence, frame, leaf)
    camera((7.5, -11.0, 8.0), (0, 0, 0.5), lens=42)
    render(PREVIEW, 1600, 900)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "Skigard.blend"))
    print("SKIGARD DONE", ART)


main()
