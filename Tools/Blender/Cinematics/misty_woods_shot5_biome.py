"""The Misty Woods cinematic, shot 5, in 3D with the game's own Misty Woods assets, laid out after its painting
(Tools/LocalSpawn/MistyWoods/mw5_avoid.png; the user, 2026-11-21: "use as many assets from the misty wood biome as needed and make
the scenes as close as possible to the actual pictures used in the cinematic").

The painting: night under a full moon (upper left), mountains and a far line of conifers through the gap; a hooded traveller with
a pack crouched on a long mossy fallen log at the bottom left, big ferns in both lower corners; carved rune stones left and right,
mossy rocks and a fallen branch in a clearing filled with fog; huge trunks framing it, two on the right; behind them, vast in the
fog, the giant. The giant is the troll of the user's reference (a mossy stone troll with a long beard and a tree for a club), made
in misty_woods_shot5.py; everything else is the game's: cedars, boulders, logs, stumps, ferns, grasses, clover, moss and the rune
stone, exported from Unity by AgentScripts/ExportBiomeKit.cs into a kit folder, and the Misty Woods terrain layers on the ground.

Moving: fog banks, a veil over the giant and the ground mist drifting; grass, ferns and the crowns in the wind; clouds passing the
moon; motes in the moonlight; the giant breathing and turning its head; a slow push-in.

  blender.exe -b --factory-startup --python Tools/Blender/Cinematics/misty_woods_shot5_biome.py -- <kit folder> still <frame> <scale%> <out.png>
  ... -- <kit folder> anim <scale%> <frames folder>
  ... -- <kit folder> blend <out.blend>
"""
import math
import os
import random
import sys

import bpy
from mathutils import Vector, noise

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import biome_kit as kit  # noqa: E402
import misty_woods_shot5 as base  # noqa: E402

FPS = base.FPS
FRAMES = base.FRAMES
SEEDMESH = "D:/Safe Haven/Safe Haven/Assets/SeedMesh/Mossy_Cedrus_Forest/Environment"
# Where the painting has the moon: a little left of the view's centre, high in the frame.
MOON_AZIMUTH = math.radians(-8.0)
MOON_ELEVATION = math.radians(18.0)
CAMERA_FROM = Vector((-0.6, -2.6, 1.85))
CAMERA_TO = Vector((-0.5, -1.6, 1.8))
CAMERA_PITCH = math.radians(92.7)
# The traveller's log: centre and heading (its thick part under the traveller, at the left third of the frame).
LOG_AT = (-0.6, 2.6)
LOG_HEADING = math.radians(-4)

base.TROLL_AT = Vector((5.0, 46.0, -0.8))
base.TROLL_SCALE = 1.85


def moon_direction():
    return Vector((math.sin(MOON_AZIMUTH) * math.cos(MOON_ELEVATION), math.cos(MOON_AZIMUTH) * math.cos(MOON_ELEVATION),
                   math.sin(MOON_ELEVATION)))


# ----------------------------------------------------------------------------- the ground

_GROUND = []


def ground_z(x, y):
    """The ground's height at (x, y), so nothing floats or sinks where the ground rises and falls."""
    if not _GROUND:
        return 0.0
    g = _GROUND[0]
    dg = bpy.context.evaluated_depsgraph_get()
    inv = g.matrix_world.inverted()
    hit, loc, _, _ = g.evaluated_get(dg).ray_cast(inv @ Vector((x, y, 50.0)), inv.to_3x3() @ Vector((0, 0, -1)))
    return (g.matrix_world @ loc).z if hit else 0.0


def top_of(obj, x, y):
    """The highest surface of <obj> at (x, y) (where the traveller sits on the log)."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    inv = obj.matrix_world.inverted()
    hit, loc, _, _ = obj.evaluated_get(dg).ray_cast(inv @ Vector((x, y, 50.0)), (inv.to_3x3() @ Vector((0, 0, -1))).normalized())
    return (obj.matrix_world @ loc).z if hit else None


def ground_material():
    """The Misty Woods terrain's own layers: moss and leaf litter over forest soil, broken up by noise."""
    mat = bpy.data.materials.new("Misty Woods Floor")
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    p.inputs["Roughness"].default_value = 0.92
    nt.links.new(p.outputs["BSDF"], out.inputs["Surface"])
    tc = nt.nodes.new("ShaderNodeTexCoord")

    def layer(path, normal_path, tile):
        # The game's terrain layer tile size, in metres.
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (1.0 / tile, 1.0 / tile, 1.0 / tile)
        nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(path, check_existing=True)
        nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
        ntex = nt.nodes.new("ShaderNodeTexImage")
        ntex.image = bpy.data.images.load(normal_path, check_existing=True)
        ntex.image.colorspace_settings.name = "Non-Color"
        nt.links.new(mp.outputs["Vector"], ntex.inputs["Vector"])
        return tex, ntex

    soil, soil_n = layer(f"{SEEDMESH}/Terrain_Textures/Dirt/Dirt_forest_basecolor.png", f"{SEEDMESH}/Terrain_Textures/Dirt/Dirt_forest_normal.png", 2.0)
    litter, litter_n = layer(f"{SEEDMESH}/Terrain_Textures/Dense_Fallen_leaves_1/Dense_fallen_Leaves_forest_basecolor.png",
                             f"{SEEDMESH}/Terrain_Textures/Dense_Fallen_leaves_1/Dense_fallen_Leaves_forest_normal.png", 2.0)
    moss, moss_n = layer(f"{SEEDMESH}/Global_textures/Moss/SeedMesh_Moss_surface_basecolor.png",
                         f"{SEEDMESH}/Global_textures/Moss/SeedMesh_Moss_surface_normal.png", 3.0)

    def noise(scale):
        n = nt.nodes.new("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = 4.0
        nt.links.new(tc.outputs["Object"], n.inputs["Vector"])
        r = nt.nodes.new("ShaderNodeMapRange")
        r.inputs["From Min"].default_value = 0.45
        r.inputs["From Max"].default_value = 0.58
        nt.links.new(n.outputs["Fac"], r.inputs["Value"])
        return r.outputs["Result"]

    def mix(a, b, fac):
        m = nt.nodes.new("ShaderNodeMix")
        m.data_type = "RGBA"
        nt.links.new(fac, m.inputs["Factor"])
        nt.links.new(a, m.inputs["A"])
        nt.links.new(b, m.inputs["B"])
        return m.outputs["Result"]

    f1, f2 = noise(0.08), noise(0.21)
    colour = mix(mix(soil.outputs["Color"], litter.outputs["Color"], f1), moss.outputs["Color"], f2)
    nrm = mix(mix(soil_n.outputs["Color"], litter_n.outputs["Color"], f1), moss_n.outputs["Color"], f2)
    nt.links.new(colour, p.inputs["Base Color"])
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(nrm, nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], p.inputs["Normal"])
    return mat


def _smooth(a, b, v):
    t = min(1.0, max(0.0, (v - a) / (b - a)))
    return t * t * (3 - 2 * t)


def terrain(x, y):
    """The painting's ground, in metres: the clearing dips in the middle, banks rise to both sides (higher on the left), the log
    lies on a low bank in front, a mossy mound at the bottom right and the left tree's root bank, then the ground climbs behind
    the giant into forested hills that rise towards the sides like a valley."""
    h = -0.9 * math.exp(-((x - 1.0) / 10.0) ** 2 - ((y - 17.0) / 11.0) ** 2)
    if x < 0:
        h += _smooth(3.0, 13.0, -x) * 2.6 * (1.0 - _smooth(30.0, 60.0, y) * 0.4) * _smooth(2.0, 6.0, y)
    else:
        h += _smooth(7.0, 24.0, x) * 2.4 * (1.0 - _smooth(40.0, 70.0, y) * 0.5)
    h += 0.35 * math.exp(-((y - 2.5) / 2.5) ** 2)
    h += 1.2 * math.exp(-((x - 4.0) / 2.0) ** 2 - ((y - 4.8) / 1.8) ** 2)
    h += 1.1 * math.exp(-((x + 6.5) / 3.0) ** 2 - ((y - 5.5) / 3.0) ** 2)
    # Low in the middle, where the painting looks out to the mountains; climbing steeply to both sides.
    rise = _smooth(52.0, 170.0, y)
    h += 6.0 * rise + rise * 0.0028 * x * x
    h += _smooth(60.0, 170.0, y) * 4.0 * noise.noise(Vector((x * 0.015, y * 0.015, 0.3)))
    return h


def heightfield(name, x0, x1, y0, y1, step, coll):
    """A grid over [x0, x1] x [y0, y1] at <step> metres, its heights from terrain()."""
    nx, ny = int((x1 - x0) / step), int((y1 - y0) / step)
    verts = [(x0 + i * step, y0 + j * step, terrain(x0 + i * step, y0 + j * step)) for j in range(ny + 1) for i in range(nx + 1)]
    faces = [(j * (nx + 1) + i, j * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i) for j in range(ny) for i in range(nx)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    obj = bpy.data.objects.new(name, me)
    coll.objects.link(obj)
    return obj


def ground(coll):
    g = heightfield("Ground", -170.0, 170.0, -20.0, 280.0, 1.25, coll)
    # Small rolls and hummocks over the shape.
    base.displace(g, "CLOUDS", 7.0, 0.8).direction = "Z"
    base.give(g, ground_material())
    base.smooth(g)
    _GROUND[:] = [g]
    bpy.context.view_layer.update()
    return g


# ----------------------------------------------------------------------------- the cover: grass, ferns, clover, moss

COVER = [("Forest Grass", 6), ("Short Grass", 8), ("Tall Grass", 3), ("Wild Fern", 2), ("Wild Fern B", 2), ("Common Fern", 1),
         ("Small Fern", 3), ("Forest Clover", 4), ("Moss Cushion", 4), ("Moss Cushion B", 3), ("Leaf Litter", 2), ("Needle Debris A", 2),
         ("Brake Fern", 1)]


def cover(g, coll):
    """The biome's ground cover scattered over the clearing and bending in the wind (geometry nodes, picking from a collection)."""
    pool = base.collection("Cover Pool (hidden)")
    for name, weight in COVER:
        for _ in range(weight):
            kit.place(name, (0, 0, 0), collection=pool)
    pool.hide_render = True
    pool.hide_viewport = True

    # The same shape and the same hummocks as the ground under it.
    patch = heightfield("Cover", -30.0, 30.0, 6.0, 45.0, 0.75, coll)
    m = base.displace(patch, "CLOUDS", 7.0, 0.8)
    m.texture = g.modifiers["Displace"].texture
    m.direction = "Z"

    ng = bpy.data.node_groups.new("Wind Cover", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    n, l = ng.nodes, ng.links.new
    gi, go = n.new("NodeGroupInput"), n.new("NodeGroupOutput")
    dist = n.new("GeometryNodeDistributePointsOnFaces")
    dist.distribute_method = "POISSON"
    dist.inputs["Distance Min"].default_value = 0.3
    dist.inputs["Density Max"].default_value = 8.0
    info = n.new("GeometryNodeCollectionInfo")
    info.inputs["Collection"].default_value = pool
    info.inputs["Separate Children"].default_value = True
    info.inputs["Reset Children"].default_value = True
    inst = n.new("GeometryNodeInstanceOnPoints")
    inst.inputs["Pick Instance"].default_value = True
    pick = n.new("FunctionNodeRandomValue")
    pick.data_type = "INT"
    pick.inputs["Min"].default_value = 0
    pick.inputs["Max"].default_value = sum(w for _, w in COVER) - 1
    scale = n.new("FunctionNodeRandomValue")
    scale.data_type = "FLOAT"
    scale.inputs["Min"].default_value = 0.8
    scale.inputs["Max"].default_value = 1.6
    yaw = n.new("FunctionNodeRandomValue")
    yaw.data_type = "FLOAT_VECTOR"
    yaw.inputs["Min"].default_value = (0, 0, 0)
    yaw.inputs["Max"].default_value = (0, 0, 6.283)
    l(gi.outputs[0], dist.inputs["Mesh"])
    l(dist.outputs["Points"], inst.inputs["Points"])
    l(info.outputs[0], inst.inputs["Instance"])
    l(pick.outputs["Value"], inst.inputs["Instance Index"])
    l(scale.outputs["Value"], inst.inputs["Scale"])
    l(yaw.outputs["Value"], inst.inputs["Rotation"])
    # The wind: moving noise turns each at its root.
    pos = n.new("GeometryNodeInputPosition")
    time = n.new("GeometryNodeInputSceneTime")
    drift = n.new("ShaderNodeVectorMath")
    drift.operation = "SCALE"
    way = n.new("ShaderNodeCombineXYZ")
    way.inputs["X"].default_value = 0.9
    way.inputs["Y"].default_value = 0.35
    l(way.outputs[0], drift.inputs[0])
    l(time.outputs["Seconds"], drift.inputs["Scale"])
    sp = n.new("ShaderNodeVectorMath")
    sp.operation = "SCALE"
    sp.inputs["Scale"].default_value = 0.22
    l(pos.outputs["Position"], sp.inputs[0])
    add = n.new("ShaderNodeVectorMath")
    add.operation = "ADD"
    l(sp.outputs[0], add.inputs[0])
    l(drift.outputs[0], add.inputs[1])
    wind = n.new("ShaderNodeTexNoise")
    wind.inputs["Scale"].default_value = 1.3
    l(add.outputs[0], wind.inputs["Vector"])
    centre = n.new("ShaderNodeVectorMath")
    centre.operation = "SUBTRACT"
    centre.inputs[1].default_value = (0.5, 0.5, 0.5)
    l(wind.outputs["Color"], centre.inputs[0])
    amount = n.new("ShaderNodeVectorMath")
    amount.operation = "MULTIPLY"
    amount.inputs[1].default_value = (0.45, 0.35, 0.0)
    l(centre.outputs[0], amount.inputs[0])
    rot = n.new("GeometryNodeRotateInstances")
    l(inst.outputs["Instances"], rot.inputs["Instances"])
    e2r = n.new("FunctionNodeEulerToRotation")
    l(amount.outputs[0], e2r.inputs[0])
    l(e2r.outputs[0], rot.inputs["Rotation"])
    rot.inputs["Local Space"].default_value = False
    l(rot.outputs["Instances"], go.inputs[0])
    mod = patch.modifiers.new("Wind Cover", "NODES")
    mod.node_group = ng
    patch.data.materials.clear()
    return patch


def mossy(obj, darken=0.55):
    """Moss over the upward faces of <obj>, its own copies of its materials: the bark darkened for the night, the game's
    moss texture blended in where the surface faces up, broken up by noise."""
    moss_path = f"{SEEDMESH}/Global_textures/Moss/SeedMesh_Moss_surface_basecolor.png"
    obj.data = obj.data.copy()
    for slot in range(len(obj.data.materials)):
        old = obj.data.materials[slot]
        if old is None:
            continue
        mat = old.copy()
        mat.name = old.name + " Mossy"
        obj.data.materials[slot] = mat
        nt = mat.node_tree
        p = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if p is None or not p.inputs["Base Color"].links:
            continue
        bark = p.inputs["Base Color"].links[0].from_socket
        dark = nt.nodes.new("ShaderNodeMix")
        dark.data_type = "RGBA"
        dark.blend_type = "MULTIPLY"
        dark.inputs["Factor"].default_value = 1.0
        dark.inputs["B"].default_value = (darken, darken * 0.95, darken * 0.9, 1)
        nt.links.new(bark, dark.inputs["A"])
        tc = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping")
        mp.inputs["Scale"].default_value = (0.8, 0.8, 0.8)
        nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(moss_path, check_existing=True)
        tex.projection = "BOX"
        tex.projection_blend = 0.3
        nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Normal"], sep.inputs[0])
        n = nt.nodes.new("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = 1.6
        n.inputs["Detail"].default_value = 5.0
        nt.links.new(tc.outputs["Object"], n.inputs["Vector"])
        up = nt.nodes.new("ShaderNodeMath")
        up.operation = "MULTIPLY_ADD"
        nt.links.new(n.outputs["Fac"], up.inputs[0])
        up.inputs[1].default_value = 1.1
        nt.links.new(sep.outputs["Z"], up.inputs[2])
        mask = nt.nodes.new("ShaderNodeMapRange")
        mask.interpolation_type = "SMOOTHSTEP"
        mask.inputs["From Min"].default_value = 1.35
        mask.inputs["From Max"].default_value = 1.6
        nt.links.new(up.outputs[0], mask.inputs["Value"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        nt.links.new(mask.outputs["Result"], mix.inputs["Factor"])
        nt.links.new(dark.outputs["Result"], mix.inputs["A"])
        nt.links.new(tex.outputs["Color"], mix.inputs["B"])
        nt.links.new(mix.outputs["Result"], p.inputs["Base Color"])
    return obj


def put(name, x, y, rotation_z=0.0, scale=1.0, coll=None, sink=0.05):
    """A kit prefab standing on the ground at (x, y)."""
    return kit.place(name, (x, y, ground_z(x, y) - sink), rotation_z=rotation_z, scale=scale, collection=coll)


# ----------------------------------------------------------------------------- the set

def trees(coll):
    """Huge trunks framing the clearing (the painting's left tree and the two on the right), and the forest beyond."""
    big = [
        ("Cedar Giant A", (-5.4, 5.2), 2.0, 0.4), ("Cedar Giant B", (5.6, 21.0), 1.9, 2.1), ("Cedar Giant A", (5.8, 9.6), 1.9, 4.0),
        ("Cedar Tall A", (-12.5, 14.0), 1.5, 1.0), ("Cedar Tall B", (-13.0, 22.0), 1.5, 5.2), ("Cedar Giant B", (14.0, 19.0), 1.6, 3.3),
        ("Cedar Tall B", (-16.0, 5.0), 1.4, 2.4), ("Cedar Tall A", (12.0, 30.0), 1.5, 0.7), ("Cedar Tall A", (-15.0, 30.0), 1.3, 0.8),
    ]
    placed = [put(name, x, y, r, s, coll, sink=0.3) for name, (x, y), s, r in big]
    rng = random.Random(21)
    kinds = ["Cedar A", "Cedar B", "Cedar C", "Cedar D", "Cedar Tall A", "Cedar Tall B", "Cedar Giant A", "Cedar Giant B"]
    for _ in range(320):
        x, y = rng.uniform(-110, 110), rng.uniform(34, 230)
        # The gap through which the giant, the far forest and the mountains show stays open: no tall trees in that direction
        # (the far treeline is the forested ridge beyond the ground).
        bearing = math.degrees(math.atan2(x - CAMERA_FROM.x, y - CAMERA_FROM.y))
        if -21.0 < bearing < 26.0:
            continue
        placed.append(put(rng.choice(kinds), x, y, rng.uniform(0, math.tau), rng.uniform(1.3, 2.1), coll, sink=0.3))
    # Boughs reaching in over the top of the frame from the framing trunks, as the painting's leaves frame the moon.
    for name, root, yaw, lift, scale in [("Young Cedar Bushy", (-4.8, 5.4, 6.8), 25.0, 5.0, 1.0),
                                         ("Young Cedar Bushy", (5.0, 9.6, 8.3), 175.0, 6.0, 1.2),
                                         ("Young Cedar Bushy", (7.2, 4.6, 6.0), 160.0, 4.0, 1.1),
                                         ("Young Cedar Bushy", (5.4, 21.0, 13.6), 192.0, 3.0, 1.6)]:
        bough = kit.place(name, root, scale=scale, collection=coll)
        bough.rotation_euler = (0, math.radians(90.0 - lift), math.radians(yaw))
        placed.append(bough)
    for i, tree in enumerate(placed):
        base.sway(tree, 0.004, 3.5 + (i % 5) * 0.5, seed=i)
    return placed


def clearing(coll):
    """The painting's clearing: rune stones left and right, mossy rocks, a fallen branch, stumps and shrubs."""
    # The rune stone is thin along x: turned about a quarter, its face looks back at the traveller.
    put("Rune Stone", -6.0, 9.6, math.radians(118), 0.62, coll, sink=0.2)
    put("Rune Stone", 3.4, 9.6, math.radians(80), 1.45, coll, sink=0.4)
    stone = bpy.data.materials.get("Kit Rune_Stone")
    if stone is not None:
        for node in stone.node_tree.nodes:
            if node.type == "MIX" and node.blend_type == "MULTIPLY":
                node.inputs["B"].default_value = (0.5, 0.53, 0.5, 1)
    for name, (x, y), sc, r in [("Wild Fern", (2.4, 8.9), 2.2, 0.6), ("Eagle Fern", (4.6, 8.6), 2.1, 2.2), ("Common Fern", (1.6, 10.6), 1.8, 4.0)]:
        put(name, x, y, r, sc, coll, sink=0.0)
    items = [("Mossy Boulder A", (1.2, 12.0), 1.2, 0.3), ("Mossy Rock", (-1.4, 14.5), 1.1, 1.2), ("Mossy Boulder B", (2.8, 16.5), 1.3, 2.0),
             ("Mossy Rock Small", (0.8, 8.0), 1.2, 0.7), ("Mossy Stones", (-2.6, 9.0), 1.4, 2.2), ("Grey Boulder", (-10.0, 26.0), 0.9, 0.4),
             ("Rotten Branch", (1.6, 9.8), 1.5, 0.15), ("Lichen Branch", (2.6, 9.4), 1.3, 2.9), ("Mossy Rock Bed", (-1.8, 7.0), 1.2, 0.5),
             ("Stump A", (-9.5, 12.5), 1.3, 0.0), ("Snag Stump", (8.6, 18.0), 1.4, 1.0), ("Forest Shrub B", (9.6, 7.5), 1.1, 0.8),
             ("Forest Shrub A", (-10.0, 9.0), 1.2, 2.0), ("Young Cedar Bushy", (10.5, 14.0), 1.2, 1.5), ("Mossy Branch Log", (-6.0, 15.5), 1.3, 1.9),
             ("Branch Pile", (5.4, 8.0), 1.2, 0.6), ("Twig Pile", (-0.4, 6.0), 1.3, 0.0), ("Fallen Trunk", (-3.5, 21.0), 1.2, 0.35),
             ("Mossy Log", (6.2, 6.0), 1.3, 2.6), ("Small Stone", (1.0, 6.4), 1.5, 0.4), ("Rotten Log A", (2.2, 9.6), 1.0, 0.1),
             ("Fallen Cedar A", (-2.0, 17.0), 1.1, 2.8)]
    for name, (x, y), s, r in items:
        put(name, x, y, r, s, coll)


def foreground(mats, coll):
    """The big mossy log across the bottom of the frame, the traveller crouched on its far top at the left third, big ferns over
    it in both corners and a mossy mound at the bottom right."""
    # The game's fallen trunk, longer and much thicker (length x1.6, girth x2.5): the painting's log is a big old trunk.
    log = mossy(put("Fallen Trunk", LOG_AT[0], LOG_AT[1], LOG_HEADING, (1.6, 2.5, 2.5), coll, sink=0.25))
    mossy(put("Rotten Log B", -6.8, 1.6, math.radians(28), 1.3, coll, sink=0.15))
    # The traveller: the highest point of the log's far top, at the left third of the frame.
    best = None
    for i in range(9):
        x = -2.7 + i * 0.1
        for dy in (0.0, 0.1, 0.2, 0.3, 0.4):
            y = LOG_AT[1] + (x - LOG_AT[0]) * math.tan(LOG_HEADING) + dy
            z = top_of(log, x, y)
            if z is not None and (best is None or z > best[2]):
                best = (x, y, z)
    seat = best or (-2.3, 2.9, 0.9)
    traveller(coll, Vector((seat[0], seat[1], seat[2] - 0.05)))
    # Moss along the log's top, as the painting's, kept off the traveller's seat.
    rng = random.Random(5)
    for i in range(26):
        x = -6.0 + i * 0.45 + rng.uniform(-0.12, 0.12)
        y = LOG_AT[1] + (x - LOG_AT[0]) * math.tan(LOG_HEADING) + rng.uniform(-0.35, 0.25)
        z = top_of(log, x, y)
        if z is not None and abs(x - seat[0]) > 0.55:
            kit.place(rng.choice(["Moss Cushion", "Moss Cushion B", "Short Grass", "Moss Cushion"]), (x, y, z - 0.04),
                      rotation_z=rng.uniform(0, math.tau), scale=rng.uniform(2.0, 3.2), collection=coll)
    # The mossy mound at the bottom right.
    for i in range(14):
        x, y = 4.0 + rng.uniform(-1.6, 1.6), 4.8 + rng.uniform(-1.3, 1.3)
        kit.place(rng.choice(["Moss Cushion", "Moss Cushion B"]), (x, y, ground_z(x, y) - 0.03), rotation_z=rng.uniform(0, math.tau),
                  scale=rng.uniform(3.0, 4.5), collection=coll)
    for name, (x, y), sc, r in [
            # Bottom left: big ferns rising over the log's near end.
            ("Eagle Fern B", (-3.0, 0.6), 2.1, 0.9), ("Common Fern", (-4.2, 1.2), 2.0, 2.6), ("Wild Fern", (-2.4, 0.1), 1.7, 4.4),
            ("Eagle Fern", (-5.4, 3.4), 1.9, 1.6), ("Common Fern", (-4.4, 4.8), 1.7, 5.0),
            # Bottom right: ferns behind and over the log, and round the mound.
            ("Eagle Fern", (1.0, 3.9), 2.4, 3.6), ("Eagle Fern B", (2.7, 1.5), 2.8, 1.4), ("Common Fern", (3.0, 4.4), 2.6, 0.4),
            ("Common Fern", (2.0, 5.6), 2.2, 2.2), ("Wild Fern", (5.0, 6.2), 2.2, 3.1), ("Eagle Fern B", (4.4, 2.6), 2.6, 5.5),
            # Behind the traveller, low.
            ("Wild Fern B", (-3.6, 6.2), 1.8, 0.2), ("Small Fern", (-1.4, 4.4), 2.0, 1.0), ("Brake Fern", (0.2, 4.2), 1.8, 2.0)]:
        fern = put(name, x, y, r, sc, coll, sink=0.0)
        base.sway(fern, 0.03, 2.2 + r * 0.3, seed=int(r * 10), axes=(0, 1))
    return log


def traveller(coll, at):
    """The hooded traveller crouched on the log, back to the camera, a pack on the back: as the painting has them."""
    t = bpy.data.objects.new("Traveller", None)
    coll.objects.link(t)
    t.location = at
    t.rotation_euler = (0, 0, math.radians(12))
    t.scale = (1.22, 1.12, 1.12)
    cloak_mat = base.mat_surface("Wool Cloak", 22.0, [(0.3, (0.025, 0.03, 0.018)), (0.7, (0.06, 0.068, 0.042))], rough=0.95, bump_strength=0.12)
    p = cloak_mat.node_tree.nodes.get("Principled BSDF")
    if p is not None and "Sheen Weight" in p.inputs:
        p.inputs["Sheen Weight"].default_value = 0.0
    pack_mat = base.mat_surface("Leather Pack", 4.0, [(0.3, (0.05, 0.028, 0.014)), (0.75, (0.13, 0.07, 0.033))], rough=0.6, bump_strength=0.15)
    # Crouched, facing into the clearing (+y), back to the camera: a body from a skeleton of points (the Skin modifier, as
    # the troll), the cloak over it pooling on the log, the head bowed in a hood with a slack peak, the pack high on the back
    # with a bedroll on top, the right knee and boot out to the side.
    pts = {
        "seat": ((0, 0.02, 0.08), 0.2), "low": ((0, 0.03, 0.28), 0.25), "mid": ((0, 0.05, 0.48), 0.24), "chest": ((0, 0.1, 0.62), 0.23),
        "neck": ((0, 0.17, 0.72), 0.1), "head": ((0, 0.21, 0.8), 0.12),
        "kneeR": ((0.2, 0.32, 0.36), 0.11), "shinR": ((0.23, 0.36, 0.08), 0.075), "footR": ((0.24, 0.46, 0.03), 0.065),
        "kneeL": ((-0.17, 0.3, 0.3), 0.1), "shinL": ((-0.2, 0.3, 0.05), 0.07),
        "armR": ((0.21, 0.14, 0.6), 0.085), "elbowR": ((0.25, 0.3, 0.42), 0.07), "handR": ((0.12, 0.4, 0.4), 0.055),
        "armL": ((-0.21, 0.14, 0.6), 0.085), "elbowL": ((-0.23, 0.3, 0.4), 0.07), "handL": ((-0.08, 0.4, 0.38), 0.055),
    }
    edges = [("seat", "low"), ("low", "mid"), ("mid", "chest"), ("chest", "neck"), ("neck", "head"),
             ("low", "kneeR"), ("kneeR", "shinR"), ("shinR", "footR"), ("low", "kneeL"), ("kneeL", "shinL"),
             ("chest", "armR"), ("armR", "elbowR"), ("elbowR", "handR"), ("chest", "armL"), ("armL", "elbowL"), ("elbowL", "handL")]
    names = list(pts)
    me = bpy.data.meshes.new("Traveller Body")
    me.from_pydata([pts[k][0] for k in names], [(names.index(x), names.index(y)) for x, y in edges], [])
    body = bpy.data.objects.new("Traveller Body", me)
    coll.objects.link(body)
    skin = body.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    for i, k in enumerate(names):
        r = pts[k][1]
        # Broader than deep: shoulders and back under the cloak.
        me.skin_vertices[0].data[i].radius = (r * 1.12, r * 0.9)
    me.skin_vertices[0].data[names.index("seat")].use_root = True
    base.subdivide(body, 2)
    body.modifiers.new("Smooth", "SMOOTH").iterations = 4
    # Folds in the cloak: noise stretched down its length.
    folds = bpy.data.objects.new("Cloak Folds", None)
    coll.objects.link(folds)
    folds.parent = t
    folds.scale = (0.06, 0.06, 0.6)
    base.displace(body, "CLOUDS", 0.6, 0.035, coords_object=folds)
    hem = base.add_cone("Cloak Hem", 0.24, 0.2, 0.14, (0, 0.06, 0.07), vertices=40, coll=coll)
    hem.scale = (1.15, 0.9, 1.0)
    base.displace(hem, "CLOUDS", 0.6, 0.04, coords_object=folds)
    hood = base.add_sphere("Hood", 0.15, (0, 0.18, 0.84), scale=(0.95, 1.12, 1.08), coll=coll)
    cowl = base.add_sphere("Cowl", 0.19, (0, 0.13, 0.72), scale=(1.15, 1.0, 0.5), coll=coll)
    peak = base.add_cone("Hood Peak", 0.085, 0.012, 0.17, (0, 0.08, 0.95), rotation=(math.radians(-48), 0, 0), vertices=14, coll=coll)
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.01, -0.13, 0.55))
    pack = bpy.context.active_object
    pack.name = "Pack"
    pack.scale = (0.34, 0.17, 0.38)
    base.move_to(pack, coll)
    pack.modifiers.new("Round", "BEVEL").width = 0.07
    pack.modifiers["Round"].segments = 5
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0.01, -0.2, 0.68))
    flap = bpy.context.active_object
    flap.name = "Pack Flap"
    flap.scale = (0.35, 0.05, 0.17)
    flap.rotation_euler = (math.radians(8), 0, 0)
    base.move_to(flap, coll)
    flap.modifiers.new("Round", "BEVEL").width = 0.02
    roll = base.add_cone("Bedroll", 0.075, 0.075, 0.46, (0.01, -0.12, 0.8), rotation=(0, math.radians(90), 0), vertices=20, coll=coll)
    for part, mat in [(body, cloak_mat), (hem, cloak_mat), (hood, cloak_mat), (cowl, cloak_mat), (peak, cloak_mat),
                      (pack, pack_mat), (flap, pack_mat), (roll, cloak_mat)]:
        part.parent = t
        base.give(part, mat)
        base.smooth(part)
    # Breathing.
    t.keyframe_insert("scale", frame=1)
    for fc in base.fcurves(t.animation_data.action):
        if fc.data_path == "scale" and fc.array_index == 2:
            wave = fc.modifiers.new("FNGENERATOR")
            wave.function_type = "SIN"
            wave.amplitude = 0.008
            wave.phase_multiplier = 2 * math.pi / (2.6 * FPS)
            wave.use_additive = True
    return t


# ----------------------------------------------------------------------------- the fog

def fog_material(name, density, colour, size, fade, drift, anisotropy=0.3):
    """Fog in world metres: patches about <size> m across, full density at fade[0] m above the ground thinning smoothly to
    nothing at fade[1] m, drifting <drift> metres over the shot while its shape slowly changes."""
    mat = bpy.data.materials.new(name)
    nt, out = base.nodes_of(mat)
    vol = nt.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (*colour, 1)
    vol.inputs["Anisotropy"].default_value = anisotropy
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    mp = nt.nodes.new("ShaderNodeMapping")
    nt.links.new(geo.outputs["Position"], mp.inputs["Vector"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    n.inputs["Scale"].default_value = 1.0 / size
    n.inputs["Detail"].default_value = 4.0
    n.inputs["Roughness"].default_value = 0.6
    nt.links.new(mp.outputs["Vector"], n.inputs["Vector"])
    shape = nt.nodes.new("ShaderNodeMapRange")
    shape.interpolation_type = "SMOOTHSTEP"
    shape.inputs["From Min"].default_value = 0.36
    shape.inputs["From Max"].default_value = 0.66
    shape.inputs["To Min"].default_value = 0.2
    nt.links.new(n.outputs["Fac"], shape.inputs["Value"])
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
    # The top is broken up too, so it never reads as a lid.
    lift = nt.nodes.new("ShaderNodeMath")
    lift.operation = "MULTIPLY_ADD"
    nt.links.new(n.outputs["Fac"], lift.inputs[0])
    lift.inputs[1].default_value = -(fade[1] - fade[0]) * 0.6
    nt.links.new(sep.outputs["Z"], lift.inputs[2])
    height = nt.nodes.new("ShaderNodeMapRange")
    height.interpolation_type = "SMOOTHSTEP"
    height.inputs["From Min"].default_value = fade[0] - (fade[1] - fade[0]) * 0.3
    height.inputs["From Max"].default_value = fade[1]
    height.inputs["To Min"].default_value = 1.0
    height.inputs["To Max"].default_value = 0.0
    nt.links.new(lift.outputs[0], height.inputs["Value"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    nt.links.new(shape.outputs["Result"], mul.inputs[0])
    nt.links.new(height.outputs["Result"], mul.inputs[1])
    amount = nt.nodes.new("ShaderNodeMath")
    amount.operation = "MULTIPLY"
    amount.inputs[1].default_value = density
    nt.links.new(mul.outputs[0], amount.inputs[0])
    nt.links.new(amount.outputs[0], vol.inputs["Density"])
    nt.links.new(vol.outputs["Volume"], out.inputs["Volume"])
    mp.inputs["Location"].keyframe_insert("default_value", frame=1)
    mp.inputs["Location"].default_value = drift
    mp.inputs["Location"].keyframe_insert("default_value", frame=FRAMES)
    n.inputs["W"].keyframe_insert("default_value", frame=1)
    n.inputs["W"].default_value = 0.5
    n.inputs["W"].keyframe_insert("default_value", frame=FRAMES)
    base.linear(mat.node_tree.animation_data)
    return mat


def fog(coll):
    """The painting's fog: a thick luminous mist lying in the clearing and round the giant's legs, thin haze up through the
    trunks, and a veil in front of the giant so it stays a shape in the fog."""
    def box(name, centre, size, mat):
        bpy.ops.mesh.primitive_cube_add(size=1, location=centre)
        b = bpy.context.active_object
        b.name = name
        b.scale = size
        base.move_to(b, coll)
        base.give(b, mat)
        return b

    box("Ground Mist", (0, 45, 3.5), (120, 66, 7.0),
        fog_material("Ground Mist", 0.24, (0.82, 0.87, 0.9), 13.0, (0.3, 4.2), (5.0, 1.5, 0.0), anisotropy=0.5))
    box("Fog Banks", (0, 100, 20.0), (260, 230, 40.0),
        fog_material("Fog Banks", 0.009, (0.52, 0.58, 0.64), 30.0, (0.0, 32.0), (9.0, 3.0, 0.0), anisotropy=0.12))
    t = base.TROLL_AT
    box("Fog Veil", (t.x, t.y - 6.0, 10.0), (48, 10, 20.0),
        fog_material("Fog Veil", 0.09, (0.55, 0.61, 0.67), 9.0, (0.0, 19.0), (6.0, 0.0, 1.0), anisotropy=0.4))


# ----------------------------------------------------------------------------- the distance and the sky

def emit_gradient(name, bottom, top, strength, height):
    """An unlit colour fading from <bottom> to <top> over <height> metres up the object (far hills: aerial haze, no shading)."""
    mat = bpy.data.materials.new(name)
    nt, out = base.nodes_of(mat)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    up = nt.nodes.new("ShaderNodeMapRange")
    up.inputs["From Min"].default_value = 0.0
    up.inputs["From Max"].default_value = height
    nt.links.new(sep.outputs["Z"], up.inputs["Value"])
    col = base.ramp(nt, up.outputs["Result"], [(0.0, bottom), (0.7, top)])
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Strength"].default_value = strength
    nt.links.new(col.outputs["Color"], e.inputs["Color"])
    nt.links.new(e.outputs["Emission"], out.inputs["Surface"])
    return mat


def mountain_range(name, y, depth, width, height, size, body, haze, seed, treeline=0.0, tilt=0.0):
    """A range of ridged mountains (ridged multifractal noise) along y, the ridge in the middle of a <depth> band; unlit, its
    colour hazing from <haze> at the foot to <body> up the slopes. <treeline> adds a ragged edge of tree tops (metres)."""
    nx, ny = 700, 16
    raw = []
    for j in range(ny + 1):
        v = j / ny
        yy = y + (v - 0.5) * depth
        envelope = math.sin(math.pi * v) ** 0.6
        for i in range(nx + 1):
            xx = (i / nx - 0.5) * width
            r = noise.ridged_multi_fractal(Vector((xx / size + seed, yy / size, seed)), 1.15, 2.0, 4, 1.0, 2.0)
            r = max(0.0, r) ** 0.75
            # Higher to the left of the view (x < 0), lower to the right.
            lean = 1.0 + tilt * max(-1.0, min(1.0, -xx / (y * 0.6)))
            raw.append((xx, yy, max(0.0, r) * envelope * lean))
    top = max(r for _, _, r in raw) or 1.0
    verts = []
    for xx, yy, r in raw:
        z = height * r / top - 12.0
        if treeline:
            z += treeline * abs(noise.noise(Vector((xx * 0.35, yy * 0.35, seed))))
        verts.append((xx, yy, z))
    faces = [(j * (nx + 1) + i, j * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i + 1, (j + 1) * (nx + 1) + i) for j in range(ny) for i in range(nx)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    obj = bpy.data.objects.new(name, me)
    obj.location = (0, 0, 0)
    base.link(obj)
    base.give(obj, emit_gradient(name, haze, body, 1.0, height))
    return obj


def far_hills(coll):
    """The painting's distance: a dark forested ridge, then mountains, then far peaks paling into the haze under the moon."""
    for obj in (
            mountain_range("Far Peaks", 1500.0, 300.0, 3600.0, 330.0, 520.0, (0.1, 0.118, 0.145), (0.13, 0.145, 0.17), 3.1, tilt=0.35),
            mountain_range("Mountains", 820.0, 200.0, 2200.0, 150.0, 330.0, (0.062, 0.076, 0.098), (0.095, 0.11, 0.13), 7.7, tilt=0.25),
            mountain_range("Forest Ridge", 430.0, 120.0, 1200.0, 50.0, 160.0, (0.028, 0.038, 0.046), (0.07, 0.085, 0.1), 11.3, treeline=5.0)):
        base.move_to(obj, coll)


def moon(coll):
    """The full moon where the painting has it, its face mottled."""
    at = moon_direction() * 2200
    disc = base.add_sphere("Moon", 74.0, at, segments=48, rings=24, coll=coll)
    mat = bpy.data.materials.new("Moon")
    nt, out = base.nodes_of(mat)
    n = base.noise(nt, 0.05, detail=5.0, coords="Object")
    col = base.ramp(nt, n.outputs["Fac"], [(0.42, (0.55, 0.58, 0.58)), (0.52, (0.82, 0.84, 0.8)), (0.62, (0.95, 0.96, 0.92))])
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Strength"].default_value = 5.0
    nt.links.new(col.outputs["Color"], e.inputs["Color"])
    nt.links.new(e.outputs["Emission"], out.inputs["Surface"])
    base.give(disc, mat)
    # The glow round it: a camera-facing sheet, its light falling off from the moon's rim (added over what is behind).
    d = moon_direction()
    bpy.ops.mesh.primitive_plane_add(size=1, location=d * 2100)
    halo = bpy.context.active_object
    halo.name = "Moon Halo"
    halo.rotation_euler = (-d).to_track_quat("Z", "Y").to_euler()
    halo.scale = (900, 900, 1)
    base.move_to(halo, coll)
    hm = bpy.data.materials.new("Moon Halo")
    hnt, hout = base.nodes_of(hm)
    tc = hnt.nodes.new("ShaderNodeTexCoord")
    length = hnt.nodes.new("ShaderNodeVectorMath")
    length.operation = "LENGTH"
    hnt.links.new(tc.outputs["Object"], length.inputs[0])
    fall = hnt.nodes.new("ShaderNodeMapRange")
    fall.interpolation_type = "SMOOTHERSTEP"
    fall.inputs["From Min"].default_value = 0.04
    fall.inputs["From Max"].default_value = 0.5
    fall.inputs["To Min"].default_value = 1.0
    fall.inputs["To Max"].default_value = 0.0
    hnt.links.new(length.outputs["Value"], fall.inputs["Value"])
    power = hnt.nodes.new("ShaderNodeMath")
    power.operation = "POWER"
    power.inputs[1].default_value = 2.6
    hnt.links.new(fall.outputs["Result"], power.inputs[0])
    glow = hnt.nodes.new("ShaderNodeEmission")
    glow.inputs["Color"].default_value = (0.55, 0.62, 0.7, 1)
    strength = hnt.nodes.new("ShaderNodeMath")
    strength.operation = "MULTIPLY"
    strength.inputs[1].default_value = 0.9
    hnt.links.new(power.outputs[0], strength.inputs[0])
    hnt.links.new(strength.outputs[0], glow.inputs["Strength"])
    add = hnt.nodes.new("ShaderNodeAddShader")
    hnt.links.new(glow.outputs["Emission"], add.inputs[0])
    hnt.links.new(hnt.nodes.new("ShaderNodeBsdfTransparent").outputs[0], add.inputs[1])
    hnt.links.new(add.outputs[0], hout.inputs["Surface"])
    if hasattr(hm, "surface_render_method"):
        hm.surface_render_method = "BLENDED"
    base.give(halo, hm)
    return disc


def clouds(coll):
    """Thin clouds drifting across the moon, lit where they pass it; clear sky shows between them."""
    d = moon_direction()
    bpy.ops.mesh.primitive_plane_add(size=1, location=d * 700)
    sheet = bpy.context.active_object
    sheet.name = "Clouds"
    sheet.rotation_euler = (-d).to_track_quat("Z", "Y").to_euler()
    sheet.scale = (1300, 520, 1)
    base.move_to(sheet, coll)
    mat = bpy.data.materials.new("Clouds")
    nt, out = base.nodes_of(mat)
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    mp.inputs["Scale"].default_value = (2.5, 6.0, 1.0)
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    n.inputs["Scale"].default_value = 2.2
    n.inputs["Detail"].default_value = 8.0
    n.inputs["Roughness"].default_value = 0.6
    nt.links.new(mp.outputs["Vector"], n.inputs["Vector"])
    alpha = nt.nodes.new("ShaderNodeMapRange")
    alpha.inputs["From Min"].default_value = 0.44
    alpha.inputs["From Max"].default_value = 0.72
    alpha.inputs["To Max"].default_value = 0.85
    nt.links.new(n.outputs["Fac"], alpha.inputs["Value"])
    # Brighter where they pass the moon.
    dist = nt.nodes.new("ShaderNodeVectorMath")
    dist.operation = "LENGTH"
    nt.links.new(tc.outputs["Object"], dist.inputs[0])
    glow = nt.nodes.new("ShaderNodeMapRange")
    glow.inputs["From Min"].default_value = 0.0
    glow.inputs["From Max"].default_value = 0.12
    glow.inputs["To Min"].default_value = 3.0
    glow.inputs["To Max"].default_value = 0.55
    nt.links.new(dist.outputs["Value"], glow.inputs["Value"])
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = (0.2, 0.23, 0.28, 1)
    nt.links.new(glow.outputs["Result"], e.inputs["Strength"])
    clear = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(alpha.outputs["Result"], mix.inputs["Fac"])
    nt.links.new(clear.outputs[0], mix.inputs[1])
    nt.links.new(e.outputs["Emission"], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    if hasattr(mat, "surface_render_method"):
        mat.surface_render_method = "BLENDED"
    mp.inputs["Location"].keyframe_insert("default_value", frame=1)
    mp.inputs["Location"].default_value = (0.06, 0.0, 0.0)
    mp.inputs["Location"].keyframe_insert("default_value", frame=FRAMES)
    n.inputs["W"].keyframe_insert("default_value", frame=1)
    n.inputs["W"].default_value = 0.2
    n.inputs["W"].keyframe_insert("default_value", frame=FRAMES)
    base.linear(mat.node_tree.animation_data)
    base.give(sheet, mat)
    return sheet


def moonlight(scene):
    """A night sky glowing round the moon, moonlight from it (the giant and the trunks against it), a soft sky fill on the near side."""
    world = bpy.data.worlds.new("Misty Woods Night")
    scene.world = world
    world.use_nodes = True
    nt = world.node_tree
    bg = nt.nodes["Background"]
    tc = nt.nodes.new("ShaderNodeTexCoord")
    dot = nt.nodes.new("ShaderNodeVectorMath")
    dot.operation = "DOT_PRODUCT"
    dot.inputs[1].default_value = moon_direction()
    nt.links.new(tc.outputs["Generated"], dot.inputs[0])
    sky = base.ramp(nt, dot.outputs["Value"], [(0.5, (0.012, 0.019, 0.034)), (0.9, (0.03, 0.045, 0.07)), (0.975, (0.07, 0.09, 0.125)),
                                               (0.994, (0.2, 0.23, 0.28)), (0.9985, (0.5, 0.54, 0.58))])
    nt.links.new(sky.outputs["Color"], bg.inputs["Color"])
    bg.inputs["Strength"].default_value = 1.0

    data = bpy.data.lights.new("Moon Light", "SUN")
    data.energy = 2.6
    data.color = (0.66, 0.75, 0.92)
    data.angle = math.radians(1.0)
    sun = bpy.data.objects.new("Moon Light", data)
    base.link(sun)
    sun.rotation_euler = (-moon_direction()).to_track_quat("-Z", "Y").to_euler()
    # The sky behind the camera: soft, cold, no hard shadow; it lets the ferns, log and cloak read as in the painting.
    sky_data = bpy.data.lights.new("Sky Fill", "SUN")
    sky_data.energy = 2.0
    sky_data.color = (0.6, 0.72, 0.72)
    sky_data.angle = math.radians(40.0)
    sky_fill = bpy.data.objects.new("Sky Fill", sky_data)
    base.link(sky_fill)
    sky_fill.rotation_euler = (math.radians(55), 0, math.radians(15))
    rim = bpy.data.lights.new("Rim", "AREA")
    rim.energy = 30000
    rim.size = 30
    rim.color = (0.5, 0.6, 0.78)
    r = bpy.data.objects.new("Rim", rim)
    base.link(r)
    r.location = (28, 62, 26)
    r.rotation_euler = (math.radians(65), 0, math.radians(120))


def camera(scene):
    data = bpy.data.cameras.new("Camera")
    data.lens = 26
    data.clip_end = 4000.0
    data.dof.use_dof = True
    data.dof.focus_distance = 9.0
    data.dof.aperture_fstop = 5.6
    cam = bpy.data.objects.new("Camera", data)
    base.link(cam)
    scene.camera = cam
    # Behind and a little above the traveller on the log, looking into the clearing; it creeps in.
    cam.location = CAMERA_FROM
    cam.rotation_euler = (CAMERA_PITCH, 0, math.radians(-3.0))
    cam.keyframe_insert("location", frame=1)
    cam.location = CAMERA_TO
    cam.keyframe_insert("location", frame=FRAMES)
    for fc in base.fcurves(cam.animation_data.action):
        for k in fc.keyframe_points:
            k.interpolation = "SINE"
            k.easing = "EASE_IN_OUT"
    cam.keyframe_insert("rotation_euler", frame=1)
    for fc in base.fcurves(cam.animation_data.action):
        if fc.data_path == "rotation_euler":
            m = fc.modifiers.new("NOISE")
            m.scale = 2.4 * FPS
            m.strength = 0.005
            m.phase = fc.array_index * 4.0
    return cam


def build(kit_dir, percent):
    scene = base.reset()
    kit_coll = base.collection("Kit (hidden)")
    kit.load(kit_dir, kit_coll)
    kit_coll.hide_render = True
    kit_coll.hide_viewport = True
    # The motes' source sphere lives here (misty_woods_shot5.spores looks for this collection).
    motes = base.collection("Pine Variants (hidden)")
    motes.hide_render = True
    motes.hide_viewport = True
    mats = base.materials()
    land, forest, set_, giant, near, air = (base.collection(n) for n in ("Land", "Forest", "Clearing", "Troll", "Foreground", "Air"))
    g = ground(land)
    base.TROLL_AT = Vector((base.TROLL_AT.x, base.TROLL_AT.y, ground_z(base.TROLL_AT.x, base.TROLL_AT.y) - 0.8))
    cover(g, land)
    trees(forest)
    clearing(set_)
    base.troll(mats, giant)
    foreground(mats, near)
    fog(air)
    far_hills(air)
    moon(air)
    clouds(air)
    base.spores(mats, air)
    moonlight(scene)
    camera(scene)
    base.render_settings(scene, percent)
    scene.eevee.volumetric_end = 260.0
    if hasattr(scene.eevee, "shadow_pool_size"):
        scene.eevee.shadow_pool_size = "1024"
    scene.view_settings.exposure = 1.0
    return scene


def main():
    a = sys.argv[sys.argv.index("--") + 1:]
    kit_dir, mode = a[0], a[1]
    if mode == "still":
        scene = build(kit_dir, a[3])
        for f in range(1, int(a[2]) + 1):
            scene.frame_set(f)
        scene.render.filepath = a[4]
        bpy.ops.render.render(write_still=True)
    elif mode == "anim":
        scene = build(kit_dir, a[2])
        scene.render.filepath = a[3].rstrip("/\\") + "/frame_"
        bpy.ops.render.render(animation=True)
    elif mode == "blend":
        build(kit_dir, 100)
        bpy.ops.wm.save_as_mainfile(filepath=a[2])


if __name__ == "__main__":
    main()
