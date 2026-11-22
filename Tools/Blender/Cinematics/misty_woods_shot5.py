"""The Misty Woods cinematic, shot 5, as a 3D demo (the user, 2026-11-21: "Do a demo shot ... of frame 5 ... not ... any of the
trolls in-game ... like the one in the picture ... mostly a silhouette and foggy ... fog, trees, grass, clouds etc needs to move").

Shot 5 of Docs/MistyWoodsCinematic.md: "A traveller crouched behind a log; something vast moves behind the trunks."
Everything is made here from primitives and procedural textures (no game models, no downloads):

- a fallen log and a hooded traveller crouched behind it, back to the camera, in the foreground;
- pine trunks with crowns of stacked boughs, two of them framing a gap;
- a mountain troll about 11 m tall behind them: a hunched Skin-modifier body with a rocky, mossy hide, a head with a heavy brow
  and hanging nose, a long stringy beard, dim eyes, and a tree trunk held across it as a club. It breathes and turns its head;
- fog banks and a ground mist drifting (animated 4D noise in volumes), grass bending in the wind (geometry nodes), crowns
  swaying, spores drifting in the light, clouds moving behind the canopy, a slow push-in with a little handheld drift.

Run headless (EEVEE on the GPU):
  "C:/Program Files/Blender Foundation/Blender 5.2/blender.exe" -b --factory-startup --python Tools/Blender/Cinematics/misty_woods_shot5.py -- still <frame> <scale%> <out.png>
  ... -- anim <scale%> <frames folder>     (all frames as PNGs; ffmpeg joins them)
  ... -- blend <out.blend>                 (the scene saved, to open and look at)
"""
import math
import random
import sys

import bpy
from mathutils import Euler, Vector

FPS = 24
SECONDS = 8
FRAMES = FPS * SECONDS
TROLL_AT = Vector((3.2, 37.0, -0.6))
TROLL_SCALE = 1.4


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    return a or ["still", "96", "50", "//shot5.png"]


# ----------------------------------------------------------------------------- scene

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.frame_start = 1
    scene.frame_end = FRAMES
    scene.render.fps = FPS
    return scene


def link(obj, collection=None):
    (collection or bpy.context.scene.collection).objects.link(obj)
    return obj


def collection(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


# ----------------------------------------------------------------------------- materials

def nodes_of(mat):
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    return nt, out


def principled(nt):
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    return p


def noise(nt, scale, detail=6.0, rough=0.6, coords="Object", dims="3D"):
    tc = nt.nodes.new("ShaderNodeTexCoord")
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = dims
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    nt.links.new(tc.outputs[coords], n.inputs["Vector"])
    return n


def ramp(nt, source, stops):
    r = nt.nodes.new("ShaderNodeValToRGB")
    els = r.color_ramp.elements
    els[0].position, els[0].color = stops[0][0], (*stops[0][1], 1)
    els[1].position, els[1].color = stops[-1][0], (*stops[-1][1], 1)
    for pos, col in stops[1:-1]:
        e = els.new(pos)
        e.color = (*col, 1)
    nt.links.new(source, r.inputs["Fac"])
    return r


def bump(nt, height, strength, distance=0.1):
    b = nt.nodes.new("ShaderNodeBump")
    b.inputs["Strength"].default_value = strength
    b.inputs["Distance"].default_value = distance
    nt.links.new(height, b.inputs["Height"])
    return b


def mat_surface(name, scale, stops, rough=0.9, bump_strength=0.6, coords="Object", moss=None):
    """A rough procedural surface: a colour ramp over noise, bumped by the same noise; moss on the tops when asked."""
    mat = bpy.data.materials.new(name)
    nt, out = nodes_of(mat)
    p = principled(nt)
    p.inputs["Roughness"].default_value = rough
    n = noise(nt, scale, coords=coords)
    col = ramp(nt, n.outputs["Fac"], stops)
    base = col.outputs["Color"]
    if moss is not None:
        # Moss where the surface faces up, broken by a second noise.
        geo = nt.nodes.new("ShaderNodeNewGeometry")
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(geo.outputs["Normal"], sep.inputs["Vector"])
        n2 = noise(nt, scale * 0.6, coords=coords)
        add = nt.nodes.new("ShaderNodeMath")
        add.operation = "ADD"
        nt.links.new(sep.outputs["Z"], add.inputs[0])
        nt.links.new(n2.outputs["Fac"], add.inputs[1])
        mask = nt.nodes.new("ShaderNodeMapRange")
        mask.inputs["From Min"].default_value = moss[0]
        mask.inputs["From Max"].default_value = moss[1]
        nt.links.new(add.outputs[0], mask.inputs["Value"])
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type = "RGBA"
        nt.links.new(mask.outputs["Result"], mix.inputs["Factor"])
        nt.links.new(base, mix.inputs["A"])
        mix.inputs["B"].default_value = (*moss[2], 1)
        base = mix.outputs["Result"]
    nt.links.new(base, p.inputs["Base Color"])
    b = bump(nt, n.outputs["Fac"], bump_strength)
    nt.links.new(b.outputs["Normal"], p.inputs["Normal"])
    nt.links.new(p.outputs["BSDF"], out.inputs["Surface"])
    return mat


def mat_emit(name, colour, strength):
    mat = bpy.data.materials.new(name)
    nt, out = nodes_of(mat)
    e = nt.nodes.new("ShaderNodeEmission")
    e.inputs["Color"].default_value = (*colour, 1)
    e.inputs["Strength"].default_value = strength
    nt.links.new(e.outputs["Emission"], out.inputs["Surface"])
    return mat


def mat_fog(name, density, colour, scale, height_fade, drift, anisotropy=0.35):
    """Drifting fog: 4D noise moved along over the shot, denser near the ground."""
    mat = bpy.data.materials.new(name)
    nt, out = nodes_of(mat)
    vol = nt.nodes.new("ShaderNodeVolumePrincipled")
    vol.inputs["Color"].default_value = (*colour, 1)
    vol.inputs["Anisotropy"].default_value = anisotropy
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    nt.links.new(tc.outputs["Generated" if False else "Object"], mp.inputs["Vector"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = 3.0
    n.inputs["Roughness"].default_value = 0.55
    nt.links.new(mp.outputs["Vector"], n.inputs["Vector"])
    # Banks: the noise pushed into patches.
    shape = nt.nodes.new("ShaderNodeMapRange")
    shape.inputs["From Min"].default_value = 0.38
    shape.inputs["From Max"].default_value = 0.68
    nt.links.new(n.outputs["Fac"], shape.inputs["Value"])
    # Thinner with height.
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs["Vector"])
    fade = nt.nodes.new("ShaderNodeMapRange")
    fade.inputs["From Min"].default_value = height_fade[0]
    fade.inputs["From Max"].default_value = height_fade[1]
    fade.inputs["To Min"].default_value = 1.0
    fade.inputs["To Max"].default_value = 0.15
    nt.links.new(sep.outputs["Z"], fade.inputs["Value"])
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = "MULTIPLY"
    nt.links.new(shape.outputs["Result"], mul.inputs[0])
    nt.links.new(fade.outputs["Result"], mul.inputs[1])
    floor = nt.nodes.new("ShaderNodeMath")
    floor.operation = "MULTIPLY_ADD"
    nt.links.new(mul.outputs[0], floor.inputs[0])
    floor.inputs[1].default_value = density
    floor.inputs[2].default_value = density * 0.25
    nt.links.new(floor.outputs[0], vol.inputs["Density"])
    nt.links.new(vol.outputs["Volume"], out.inputs["Volume"])
    # The drift and the slow change of shape over the shot.
    mp.inputs["Location"].default_value = (0, 0, 0)
    mp.inputs["Location"].keyframe_insert("default_value", frame=1)
    mp.inputs["Location"].default_value = drift
    mp.inputs["Location"].keyframe_insert("default_value", frame=FRAMES)
    n.inputs["W"].default_value = 0.0
    n.inputs["W"].keyframe_insert("default_value", frame=1)
    n.inputs["W"].default_value = 0.6
    n.inputs["W"].keyframe_insert("default_value", frame=FRAMES)
    linear(mat.node_tree.animation_data)
    return mat


def linear(anim):
    if anim and anim.action:
        for fc in fcurves(anim.action):
            for k in fc.keyframe_points:
                k.interpolation = "LINEAR"


def fcurves(action):
    """F-curves of an action, whether layered (Blender 4.4+) or not."""
    if hasattr(action, "fcurves") and len(getattr(action, "fcurves", [])) > 0:
        return list(action.fcurves)
    found = []
    for layer in getattr(action, "layers", []):
        for strip in layer.strips:
            for bag in getattr(strip, "channelbags", []):
                found.extend(bag.fcurves)
    return found


# ----------------------------------------------------------------------------- meshes

def displace(obj, kind, size, strength, coords_object=None, mid=0.5):
    tex = bpy.data.textures.new(f"{obj.name} {kind}", kind)
    if hasattr(tex, "noise_scale"):
        tex.noise_scale = size
    if kind == "VORONOI":
        tex.distance_metric = "DISTANCE"
    m = obj.modifiers.new("Displace", "DISPLACE")
    m.texture = tex
    m.strength = strength
    m.mid_level = mid
    if coords_object is not None:
        m.texture_coords = "OBJECT"
        m.texture_coords_object = coords_object
    else:
        m.texture_coords = "GLOBAL"
    return m


def subdivide(obj, levels):
    m = obj.modifiers.new("Subdivision", "SUBSURF")
    m.levels = levels
    m.render_levels = levels
    return m


def smooth(obj):
    for p in obj.data.polygons:
        p.use_smooth = True


def add_cone(name, r1, r2, depth, location, rotation=(0, 0, 0), vertices=16, coll=None):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r1, radius2=r2, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    move_to(obj, coll)
    return obj


def add_sphere(name, radius, location, scale=(1, 1, 1), segments=32, rings=16, coll=None):
    bpy.ops.mesh.primitive_uv_sphere_add(radius=radius, location=location, segments=segments, ring_count=rings)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    move_to(obj, coll)
    return obj


def move_to(obj, coll):
    if coll is None:
        return
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    coll.objects.link(obj)


def give(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


# ----------------------------------------------------------------------------- the ground

def ground(mats, coll):
    bpy.ops.mesh.primitive_plane_add(size=240, location=(0, 60, 0))
    g = bpy.context.active_object
    g.name = "Ground"
    move_to(g, coll)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.subdivide(number_cuts=120)
    bpy.ops.object.mode_set(mode="OBJECT")
    displace(g, "CLOUDS", 6.0, 0.9)
    give(g, mats["ground"])
    smooth(g)
    return g


def blade_mesh(mat):
    """One blade of grass, 0.5 m, a tapering strip bent a little forward; its pivot at the root."""
    h, w = 0.42, 0.018
    verts, faces = [], []
    rows = 4
    for i in range(rows + 1):
        t = i / rows
        half = w * (1 - t) + 0.002
        bend = 0.16 * t * t
        verts += [(-half, bend, h * t), (half, bend, h * t)]
    for i in range(rows):
        a = i * 2
        faces.append((a, a + 1, a + 3, a + 2))
    me = bpy.data.meshes.new("Blade")
    me.from_pydata(verts, [], faces)
    me.materials.append(mat)
    obj = bpy.data.objects.new("Blade", me)
    return obj


def grass(g, mats, coll):
    """Grass over the near ground, bending in the wind: geometry nodes, each blade turned at its root by moving noise."""
    blade = blade_mesh(mats["grass"])
    hidden = collection("Grass Blade (hidden)")
    hidden.objects.link(blade)
    hidden.hide_render = True
    hidden.hide_viewport = True

    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 16.5, 0))
    patch = bpy.context.active_object
    patch.name = "Grass"
    patch.scale = (44, 27, 1)
    move_to(patch, coll)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.subdivide(number_cuts=60)
    bpy.ops.object.mode_set(mode="OBJECT")
    # The same rise as the ground under it.
    displace(patch, "CLOUDS", 6.0, 0.9).texture = g.modifiers["Displace"].texture
    give(patch, mats["ground"])

    ng = bpy.data.node_groups.new("Wind Grass", "GeometryNodeTree")
    ng.interface.new_socket("Geometry", in_out="INPUT", socket_type="NodeSocketGeometry")
    ng.interface.new_socket("Geometry", in_out="OUTPUT", socket_type="NodeSocketGeometry")
    n = ng.nodes
    gi = n.new("NodeGroupInput")
    go = n.new("NodeGroupOutput")
    dist = n.new("GeometryNodeDistributePointsOnFaces")
    dist.inputs["Density"].default_value = 130.0
    info = n.new("GeometryNodeObjectInfo")
    info.inputs["Object"].default_value = blade
    inst = n.new("GeometryNodeInstanceOnPoints")
    rnd_scale = n.new("FunctionNodeRandomValue")
    rnd_scale.data_type = "FLOAT"
    rnd_scale.inputs["Min"].default_value = 0.35
    rnd_scale.inputs["Max"].default_value = 0.9
    rnd_rot = n.new("FunctionNodeRandomValue")
    rnd_rot.data_type = "FLOAT_VECTOR"
    rnd_rot.inputs["Min"].default_value = (-0.25, -0.25, 0.0)
    rnd_rot.inputs["Max"].default_value = (0.25, 0.25, 6.283)
    l = ng.links.new
    l(gi.outputs[0], dist.inputs["Mesh"])
    l(dist.outputs["Points"], inst.inputs["Points"])
    l(info.outputs["Geometry"], inst.inputs["Instance"])
    l(rnd_rot.outputs["Value"], inst.inputs["Rotation"])
    l(rnd_scale.outputs["Value"], inst.inputs["Scale"])

    # Wind: noise moving across the field turns each blade at its root.
    pos = n.new("GeometryNodeInputPosition")
    time = n.new("GeometryNodeInputSceneTime")
    tmul = n.new("ShaderNodeVectorMath")
    tmul.operation = "SCALE"
    comb = n.new("ShaderNodeCombineXYZ")
    comb.inputs["X"].default_value = 1.0
    comb.inputs["Y"].default_value = 0.4
    l(comb.outputs[0], tmul.inputs[0])
    l(time.outputs["Seconds"], tmul.inputs["Scale"])
    addv = n.new("ShaderNodeVectorMath")
    addv.operation = "ADD"
    scl = n.new("ShaderNodeVectorMath")
    scl.operation = "SCALE"
    scl.inputs["Scale"].default_value = 0.25
    l(pos.outputs["Position"], scl.inputs[0])
    l(scl.outputs[0], addv.inputs[0])
    l(tmul.outputs[0], addv.inputs[1])
    wind = n.new("ShaderNodeTexNoise")
    wind.inputs["Scale"].default_value = 1.2
    wind.inputs["Detail"].default_value = 2.0
    l(addv.outputs[0], wind.inputs["Vector"])
    centre = n.new("ShaderNodeVectorMath")
    centre.operation = "SUBTRACT"
    centre.inputs[1].default_value = (0.5, 0.5, 0.5)
    l(wind.outputs["Color"], centre.inputs[0])
    amount = n.new("ShaderNodeVectorMath")
    amount.operation = "MULTIPLY"
    amount.inputs[1].default_value = (1.1, 0.9, 0.0)
    l(centre.outputs[0], amount.inputs[0])
    rot = n.new("GeometryNodeRotateInstances")
    l(inst.outputs["Instances"], rot.inputs["Instances"])
    if "FunctionNodeEulerToRotation" in dir(bpy.types) or True:
        try:
            e2r = n.new("FunctionNodeEulerToRotation")
            l(amount.outputs[0], e2r.inputs[0])
            l(e2r.outputs[0], rot.inputs["Rotation"])
        except Exception:
            l(amount.outputs[0], rot.inputs["Rotation"])
    rot.inputs["Local Space"].default_value = False
    realize = n.new("GeometryNodeRealizeInstances")
    l(rot.outputs["Instances"], realize.inputs[0])
    l(realize.outputs[0], go.inputs[0])

    mod = patch.modifiers.new("Wind Grass", "NODES")
    mod.node_group = ng
    return patch


# ----------------------------------------------------------------------------- the trees

def pine_variant(index, mats, coll):
    """One pine: a tapering trunk with dead stubs low down and a crown of drooping boughs; kept hidden, copied about."""
    rng = random.Random(100 + index)
    height = rng.uniform(26, 34)
    radius = rng.uniform(0.42, 0.6)
    parts = []
    trunk = add_cone(f"Pine {index} Trunk", radius, radius * 0.18, height, (0, 0, height / 2), vertices=20, coll=coll)
    displace(trunk, "STUCCI", 0.35, 0.06)
    subdivide(trunk, 1)
    give(trunk, mats["bark"])
    smooth(trunk)
    parts.append(trunk)
    # Dead branches low on the trunk: the forest's spikes against the fog.
    for s in range(rng.randint(6, 11)):
        z = rng.uniform(2.0, height * 0.6)
        yaw = rng.uniform(0, math.tau)
        length = rng.uniform(0.5, 1.6)
        stub = add_cone(f"Pine {index} Stub {s}", 0.06, 0.008, length, (0, 0, 0), vertices=6, coll=coll)
        stub.rotation_euler = Euler((math.radians(rng.uniform(95, 135)), 0, yaw), "XYZ")
        direction = Vector((0, 0, 1))
        direction.rotate(stub.rotation_euler)
        stub.location = Vector((0, 0, z)) + direction * (length / 2 + radius * 0.6)
        give(stub, mats["bark"])
        parts.append(stub)
    # Crown: many narrow drooping tiers from a third of the height up, each a ragged skirt round the trunk.
    layers = rng.randint(10, 13)
    for k in range(layers):
        t = k / (layers - 1)
        z = height * (0.58 + 0.4 * t)
        r = (1 - t) * rng.uniform(2.3, 2.9) + 0.35
        depth = 1.6 + (1 - t) * 1.4
        bough = add_cone(f"Pine {index} Bough {k}", r, 0.12, depth, (0, 0, z + depth * 0.5), vertices=12, coll=coll)
        bough.rotation_euler = (rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), rng.uniform(0, math.tau))
        displace(bough, "VORONOI", 0.45, 0.4)
        subdivide(bough, 1)
        give(bough, mats["needles"])
        parts.append(bough)
    # One object to copy: joined.
    bpy.ops.object.select_all(action="DESELECT")
    for p in parts:
        p.select_set(True)
    bpy.context.view_layer.objects.active = trunk
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    tree = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    tree.name = f"Pine Variant {index}"
    return tree


def forest(mats, coll):
    hidden = collection("Pine Variants (hidden)")
    variants = [pine_variant(i, mats, hidden) for i in range(3)]
    hidden.hide_render = True
    hidden.hide_viewport = True
    rng = random.Random(7)
    spots = [(-1.6, 19.0, 1.15), (6.4, 23.5, 1.3), (-6.5, 12.0, 1.0), (10.5, 15.0, 1.05), (-12.0, 26.0, 1.0)]
    while len(spots) < 70:
        x, y = rng.uniform(-45, 45), rng.uniform(8, 110)
        # A clear lane from the camera to the troll, so it shows between the trunks.
        lane = abs(x - TROLL_AT.x * (y / TROLL_AT.y)) < 4.0 and y < TROLL_AT.y + 6
        if lane or abs(x) < 3 and y < 10:
            continue
        spots.append((x, y, rng.uniform(0.8, 1.25)))
    crowns = []
    for i, (x, y, s) in enumerate(spots):
        source = variants[i % len(variants)]
        tree = bpy.data.objects.new(f"Pine {i}", source.data)
        coll.objects.link(tree)
        tree.location = (x, y, -0.2)
        tree.scale = (s, s, s * rng.uniform(0.92, 1.08))
        tree.rotation_euler = (0, 0, rng.uniform(0, math.tau))
        crowns.append(tree)
    # A slow sway: the whole tree leans and comes back, each its own way.
    for i, tree in enumerate(crowns):
        sway(tree, 0.006, 3.0 + (i % 5) * 0.4, seed=i)
    return crowns


def sway(obj, amount, period, seed=0, axes=(0, 1)):
    """A gentle rocking on the given rotation axes: noise on the rotation's F-curves."""
    obj.keyframe_insert("rotation_euler", frame=1)
    for fc in fcurves(obj.animation_data.action):
        if fc.data_path == "rotation_euler" and fc.array_index in axes:
            m = fc.modifiers.new("NOISE")
            m.scale = period * FPS
            m.strength = amount * 2
            m.phase = seed * 7.3 + fc.array_index * 3.1


# ----------------------------------------------------------------------------- the troll

def troll(mats, coll):
    """A hunched mountain troll about 11 m tall, the club of a whole tree held across it: a silhouette in the fog."""
    root = bpy.data.objects.new("Troll", None)
    coll.objects.link(root)
    root.location = TROLL_AT
    root.rotation_euler = (0, 0, math.radians(-14))
    root.scale = (TROLL_SCALE,) * 3

    # The body from a skeleton of points: the Skin modifier wraps flesh round it, radius by radius.
    pts = {
        "pelvis": ((0, 0.4, 4.9), 1.9), "belly": ((0, -0.6, 6.0), 2.45), "chest": ((0, -0.9, 7.6), 2.3),
        "hump": ((0, -0.4, 8.7), 1.9), "neck": ((0, -1.9, 8.5), 1.2),
        "hipL": ((-1.05, 0.2, 4.6), 1.2), "kneeL": ((-1.35, -0.55, 2.5), 0.95), "ankleL": ((-1.45, -0.3, 0.55), 0.85),
        "footL": ((-1.5, -1.2, 0.35), 0.7),
        "hipR": ((1.05, 0.2, 4.6), 1.2), "kneeR": ((1.4, -0.45, 2.5), 0.95), "ankleR": ((1.55, -0.2, 0.55), 0.85),
        "footR": ((1.6, -1.1, 0.35), 0.7),
        "shoulderL": ((-2.7, -0.6, 8.6), 1.3), "elbowL": ((-3.3, -1.7, 6.0), 0.9), "handL": ((-2.0, -2.9, 4.6), 0.8),
        "shoulderR": ((2.7, -0.6, 8.6), 1.3), "elbowR": ((3.35, -1.5, 6.2), 0.9), "handR": ((1.7, -2.95, 5.2), 0.8),
    }
    edges = [("pelvis", "belly"), ("belly", "chest"), ("chest", "hump"), ("hump", "neck"),
             ("pelvis", "hipL"), ("hipL", "kneeL"), ("kneeL", "ankleL"), ("ankleL", "footL"),
             ("pelvis", "hipR"), ("hipR", "kneeR"), ("kneeR", "ankleR"), ("ankleR", "footR"),
             ("hump", "shoulderL"), ("shoulderL", "elbowL"), ("elbowL", "handL"),
             ("hump", "shoulderR"), ("shoulderR", "elbowR"), ("elbowR", "handR")]
    names = list(pts)
    me = bpy.data.meshes.new("Troll Body")
    me.from_pydata([pts[k][0] for k in names], [(names.index(a), names.index(b)) for a, b in edges], [])
    body = bpy.data.objects.new("Troll Body", me)
    coll.objects.link(body)
    body.parent = root
    skin = body.modifiers.new("Skin", "SKIN")
    skin.use_smooth_shade = True
    for i, k in enumerate(names):
        r = pts[k][1]
        me.skin_vertices[0].data[i].radius = (r, r * 0.92)
    me.skin_vertices[0].data[names.index("pelvis")].use_root = True
    subdivide(body, 3)
    body.modifiers.new("Smooth", "SMOOTH").iterations = 8
    # A hide of rock and moss: lumps over lumps.
    displace(body, "VORONOI", 1.3, 0.75)
    displace(body, "CLOUDS", 0.4, 0.22)
    give(body, mats["troll"])

    # The head, apart so it can turn: brow, nose, ears, beard and two dull eyes.
    head = bpy.data.objects.new("Troll Head", None)
    coll.objects.link(head)
    head.parent = root
    head.location = (0, -2.5, 8.75)
    skull = add_sphere("Troll Skull", 1.45, (0, 0, 0.1), scale=(1.0, 1.05, 0.95), coll=coll)
    brow = add_sphere("Troll Brow", 1.1, (0, -0.95, 0.4), scale=(1.1, 0.45, 0.35), coll=coll)
    nose = add_sphere("Troll Nose", 0.62, (0, -1.5, -0.3), scale=(0.8, 1.0, 1.2), coll=coll)
    nose.rotation_euler = (math.radians(-35), 0, 0)
    ear_l = add_sphere("Troll Ear L", 0.4, (-1.05, -0.1, 0.1), scale=(0.35, 0.8, 1.0), coll=coll)
    ear_r = add_sphere("Troll Ear R", 0.4, (1.05, -0.1, 0.1), scale=(0.35, 0.8, 1.0), coll=coll)
    for part in (skull, brow, nose, ear_l, ear_r):
        part.parent = head
        displace(part, "VORONOI", 0.6, 0.18)
        give(part, mats["troll"])
        smooth(part)
    # The beard: a long hanging cone pulled into strings (noise stretched down its length).
    stretch = bpy.data.objects.new("Beard Strings", None)
    coll.objects.link(stretch)
    stretch.parent = head
    stretch.scale = (0.12, 0.12, 2.5)
    beard = add_cone("Troll Beard", 1.35, 0.55, 5.0, (0, -0.75, -2.9), vertices=64, coll=coll)
    beard.scale = (1.25, 0.5, 1.0)
    beard.rotation_euler = (math.radians(10), 0, 0)
    beard.parent = head
    bpy.context.view_layer.objects.active = beard
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.subdivide(number_cuts=10)
    bpy.ops.object.mode_set(mode="OBJECT")
    displace(beard, "CLOUDS", 0.35, 0.5, coords_object=stretch)
    displace(beard, "STUCCI", 0.25, 0.15)
    give(beard, mats["beard"])
    smooth(beard)
    # Eyes: barely lit, under the brow.
    for side in (-1, 1):
        eye = add_sphere(f"Troll Eye {side}", 0.085, (0.5 * side, -1.35, 0.12), coll=coll)
        eye.parent = head
        give(eye, mats["eye"])

    # The club: a stripped trunk with its broken crown at the far end, held across the body.
    club = add_cone("Troll Club", 0.62, 0.38, 8.2, (0, 0, 0), vertices=18, coll=coll)
    club.parent = root
    a, b = Vector(pts["handL"][0]), Vector(pts["handR"][0])
    mid = (a + b) / 2
    axis = (b - a).normalized()
    club.location = mid + axis * 1.3 + Vector((0, -0.3, 0))
    club.rotation_euler = axis.to_track_quat("Z", "Y").to_euler()
    club.rotation_euler.x += math.radians(8)
    displace(club, "STUCCI", 0.4, 0.12)
    subdivide(club, 1)
    give(club, mats["bark"])
    smooth(club)
    twig_rng = random.Random(3)
    for t in range(16):
        twig = add_cone(f"Club Twig {t}", 0.09, 0.005, twig_rng.uniform(1.0, 2.6), (0, 0, 0), vertices=5, coll=coll)
        twig.parent = club
        twig.location = (twig_rng.uniform(-0.3, 0.3), twig_rng.uniform(-0.3, 0.3), 4.1 + twig_rng.uniform(-0.6, 0.4))
        twig.rotation_euler = (twig_rng.uniform(-1.2, 1.2), twig_rng.uniform(-1.2, 1.2), twig_rng.uniform(0, math.tau))
        give(twig, mats["bark"])

    # Alive, barely: it breathes, sways a little, and turns its head slowly toward the log.
    body.keyframe_insert("scale", frame=1)
    for fc in fcurves(body.animation_data.action):
        if fc.data_path == "scale" and fc.array_index in (0, 1):
            m = fc.modifiers.new("GENERATOR")
            m.mode = "POLYNOMIAL"
            m.coefficients = (1.0, 0.0)
            wave = fc.modifiers.new("FNGENERATOR")
            wave.function_type = "SIN"
            wave.amplitude = 0.012
            wave.phase_multiplier = 2 * math.pi / (4.2 * FPS)
            wave.use_additive = True
    sway(root, 0.01, 5.0, seed=11, axes=(1,))
    head.rotation_euler = (math.radians(4), 0, math.radians(-6))
    head.keyframe_insert("rotation_euler", frame=1)
    head.rotation_euler = (math.radians(9), 0, math.radians(14))
    head.keyframe_insert("rotation_euler", frame=FRAMES)
    return root


# ----------------------------------------------------------------------------- the foreground

def log_and_traveller(mats, coll):
    log = add_cone("Fallen Log", 0.55, 0.45, 6.5, (-0.6, 4.2, 0.4), rotation=(0, math.radians(90), math.radians(8)), vertices=32, coll=coll)
    bpy.context.view_layer.objects.active = log
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.subdivide(number_cuts=6)
    bpy.ops.object.mode_set(mode="OBJECT")
    displace(log, "STUCCI", 0.3, 0.06)
    give(log, mats["log"])
    smooth(log)

    # The traveller, crouched behind it, seen from behind: a hooded cloak over drawn-up knees.
    t = bpy.data.objects.new("Traveller", None)
    coll.objects.link(t)
    t.location = (-1.6, 3.35, 0.0)
    t.rotation_euler = (0, 0, math.radians(18))
    cloak = add_cone("Cloak", 0.6, 0.24, 0.78, (0, 0, 0.39), vertices=24, coll=coll)
    hood = add_sphere("Hood", 0.21, (0, 0.06, 0.95), scale=(0.9, 1.1, 1.05), coll=coll)
    peak = add_cone("Hood Peak", 0.12, 0.01, 0.22, (0, -0.12, 1.06), rotation=(math.radians(-60), 0, 0), vertices=10, coll=coll)
    shoulder = add_sphere("Shoulders", 0.3, (0, 0.05, 0.76), scale=(1.15, 0.8, 0.5), coll=coll)
    arm = add_cone("Arm", 0.08, 0.06, 0.5, (0.26, 0.3, 0.72), rotation=(math.radians(-75), 0, math.radians(-10)), vertices=8, coll=coll)
    for part in (cloak, hood, peak, shoulder, arm):
        part.parent = t
        displace(part, "CLOUDS", 0.25, 0.04)
        give(part, mats["cloth"])
        smooth(part)
    # Breathing, held.
    t.keyframe_insert("scale", frame=1)
    for fc in fcurves(t.animation_data.action):
        if fc.data_path == "scale" and fc.array_index == 2:
            wave = fc.modifiers.new("FNGENERATOR")
            wave.function_type = "SIN"
            wave.amplitude = 0.008
            wave.phase_multiplier = 2 * math.pi / (2.6 * FPS)
            wave.use_additive = True
    # Rocks round about.
    rng = random.Random(5)
    for r in range(14):
        rock = add_sphere(f"Rock {r}", rng.uniform(0.2, 0.9), (rng.uniform(-14, 14), rng.uniform(5, 30), 0), segments=16, rings=8, coll=coll)
        rock.scale = (1, rng.uniform(0.7, 1.3), rng.uniform(0.4, 0.7))
        displace(rock, "VORONOI", 0.5, 0.25)
        give(rock, mats["rock"])
    return t


# ----------------------------------------------------------------------------- air

def fog(mats, coll):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 50, 9))
    banks = bpy.context.active_object
    banks.name = "Fog Banks"
    banks.scale = (140, 110, 18)
    move_to(banks, coll)
    give(banks, mats["fog"])
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 36, 0.8))
    mist = bpy.context.active_object
    mist.name = "Ground Mist"
    mist.scale = (90, 60, 2.0)
    move_to(mist, coll)
    give(mist, mats["mist"])
    bpy.ops.mesh.primitive_cube_add(size=1, location=(TROLL_AT.x, TROLL_AT.y - 7.0, 8.0))
    veil = bpy.context.active_object
    veil.name = "Fog Veil"
    veil.scale = (40, 8, 18)
    move_to(veil, coll)
    give(veil, mats["veil"])
    return banks, mist


def sky(mats, coll):
    """Clouds moving behind the crowns: an emissive sheet of noise, drifting."""
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, 140, 40), rotation=(math.radians(80), 0, 0))
    sheet = bpy.context.active_object
    sheet.name = "Clouds"
    sheet.scale = (400, 160, 1)
    move_to(sheet, coll)
    mat = bpy.data.materials.new("Clouds")
    nt, out = nodes_of(mat)
    e = nt.nodes.new("ShaderNodeEmission")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    nt.links.new(tc.outputs["Object"], mp.inputs["Vector"])
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.noise_dimensions = "4D"
    n.inputs["Scale"].default_value = 3.0
    n.inputs["Detail"].default_value = 8.0
    nt.links.new(mp.outputs["Vector"], n.inputs["Vector"])
    col = ramp(nt, n.outputs["Fac"], [(0.35, (0.05, 0.065, 0.075)), (0.7, (0.26, 0.29, 0.31))])
    nt.links.new(col.outputs["Color"], e.inputs["Color"])
    e.inputs["Strength"].default_value = 1.0
    nt.links.new(e.outputs["Emission"], out.inputs["Surface"])
    mp.inputs["Location"].keyframe_insert("default_value", frame=1)
    mp.inputs["Location"].default_value = (0.05, 0.0, 0.0)
    mp.inputs["Location"].keyframe_insert("default_value", frame=FRAMES)
    n.inputs["W"].keyframe_insert("default_value", frame=1)
    n.inputs["W"].default_value = 0.25
    n.inputs["W"].keyframe_insert("default_value", frame=FRAMES)
    linear(mat.node_tree.animation_data)
    give(sheet, mat)
    return sheet


def spores(mats, coll):
    """Spores and dust drifting slowly down through the light."""
    mote = add_sphere("Mote", 0.012, (0, -50, -50), segments=6, rings=4)
    give(mote, mats["mote"])
    hidden = bpy.data.collections.get("Pine Variants (hidden)")
    move_to(mote, hidden)
    # Filled from the first frame (a volume, all at once), so any frame shows them without a run-up.
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 12, 4))
    emitter = bpy.context.active_object
    emitter.name = "Spore Emitter"
    emitter.scale = (26, 22, 8)
    move_to(emitter, coll)
    ps = emitter.modifiers.new("Spores", "PARTICLE_SYSTEM").particle_system
    s = ps.settings
    s.count = 1200
    s.frame_start = 1
    s.frame_end = 1
    s.lifetime = 1000
    s.emit_from = "VOLUME"
    s.distribution = "RAND"
    s.normal_factor = 0.0
    s.render_type = "OBJECT"
    s.instance_object = mote
    s.particle_size = 1.0
    s.size_random = 0.7
    s.brownian_factor = 0.12
    s.effector_weights.gravity = 0.004
    s.use_rotations = False
    emitter.show_instancer_for_render = False
    return emitter


# ----------------------------------------------------------------------------- light, camera, render

def light_and_world(scene):
    world = bpy.data.worlds.new("Misty Woods")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.07, 0.085, 0.095, 1)
    bg.inputs["Strength"].default_value = 1.0
    # A low cold sun from behind the troll: shafts through the crowns, the giant against the light.
    sun_data = bpy.data.lights.new("Sun", "SUN")
    sun_data.energy = 3.2
    sun_data.color = (0.78, 0.86, 0.95)
    sun_data.angle = math.radians(2.5)
    sun = bpy.data.objects.new("Sun", sun_data)
    link(sun)
    sun.rotation_euler = (math.radians(75), 0, math.radians(176))
    # A faint cold rim from the side, so the hide catches a little light.
    rim_data = bpy.data.lights.new("Rim", "AREA")
    rim_data.energy = 45000
    rim_data.size = 20
    rim_data.color = (0.6, 0.7, 0.8)
    rim = bpy.data.objects.new("Rim", rim_data)
    link(rim)
    rim.location = (22, 42, 16)
    rim.rotation_euler = (math.radians(70), 0, math.radians(110))
    fill_data = bpy.data.lights.new("Fill", "AREA")
    fill_data.energy = 350
    fill_data.size = 8
    fill_data.color = (0.55, 0.62, 0.7)
    fill = bpy.data.objects.new("Fill", fill_data)
    link(fill)
    fill.location = (2.5, -6.0, 4.5)
    fill.rotation_euler = (math.radians(-70), 0, math.radians(180))


def camera(scene):
    cam_data = bpy.data.cameras.new("Camera")
    cam_data.lens = 30
    cam_data.dof.use_dof = True
    cam_data.dof.focus_distance = 22.0
    cam_data.dof.aperture_fstop = 4.0
    cam = bpy.data.objects.new("Camera", cam_data)
    link(cam)
    scene.camera = cam
    # Low behind the traveller, looking up past the trunks at the giant; it creeps in.
    cam.location = (-0.9, -1.8, 1.35)
    cam.rotation_euler = (math.radians(94.0), 0, math.radians(-3))
    cam.keyframe_insert("location", frame=1)
    cam.location = (-0.75, -0.2, 1.45)
    cam.keyframe_insert("location", frame=FRAMES)
    for fc in fcurves(cam.animation_data.action):
        for k in fc.keyframe_points:
            k.interpolation = "SINE"
            k.easing = "EASE_IN_OUT"
    cam.keyframe_insert("rotation_euler", frame=1)
    # A little held-camera drift.
    for fc in fcurves(cam.animation_data.action):
        if fc.data_path == "rotation_euler":
            m = fc.modifiers.new("NOISE")
            m.scale = 2.2 * FPS
            m.strength = 0.006
            m.phase = fc.array_index * 5.0
    return cam


def render_settings(scene, percent):
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    r = scene.render
    r.resolution_x, r.resolution_y = 1920, 1080
    r.resolution_percentage = int(percent)
    r.use_motion_blur = True
    r.image_settings.file_format = "PNG"
    e = scene.eevee
    for name, value in (("taa_render_samples", 48), ("volumetric_tile_size", "4"), ("volumetric_samples", 96),
                        ("volumetric_start", 0.5), ("volumetric_end", 220.0), ("volumetric_shadow_samples", 24),
                        ("use_volumetric_shadows", True), ("volumetric_light_clamp", 0.0), ("use_shadows", True),
                        ("shadow_ray_count", 2), ("shadow_step_count", 8), ("use_raytracing", True), ("use_gtao", True)):
        if hasattr(e, name):
            try:
                setattr(e, name, value)
            except Exception:
                pass
    vs = scene.view_settings
    vs.view_transform = "AgX"
    for look in ("AgX - Medium High Contrast", "Medium High Contrast"):
        try:
            vs.look = look
            break
        except TypeError:
            continue
    vs.exposure = 0.7


# ----------------------------------------------------------------------------- build

def materials():
    return {
        "ground": mat_surface("Forest Floor", 2.5, [(0.3, (0.025, 0.028, 0.02)), (0.55, (0.05, 0.055, 0.035)), (0.8, (0.07, 0.065, 0.045))], bump_strength=0.8),
        "grass": mat_surface("Grass", 0.8, [(0.25, (0.05, 0.065, 0.03)), (0.6, (0.1, 0.11, 0.05)), (0.85, (0.16, 0.15, 0.08))], rough=0.8, bump_strength=0.1, coords="Generated"),
        "bark": mat_surface("Bark", 6.0, [(0.3, (0.025, 0.022, 0.02)), (0.6, (0.06, 0.055, 0.05)), (0.85, (0.1, 0.1, 0.09))], bump_strength=1.0,
                            moss=(0.75, 1.3, (0.05, 0.07, 0.03))),
        "needles": mat_surface("Needles", 9.0, [(0.3, (0.012, 0.022, 0.016)), (0.7, (0.03, 0.045, 0.03))], rough=0.85, bump_strength=0.6),
        "troll": mat_surface("Troll Hide", 1.8, [(0.25, (0.04, 0.042, 0.04)), (0.55, (0.09, 0.09, 0.085)), (0.8, (0.14, 0.14, 0.13))],
                             bump_strength=1.2, moss=(0.55, 1.2, (0.05, 0.075, 0.03))),
        "beard": mat_surface("Troll Beard", 3.5, [(0.25, (0.06, 0.065, 0.055)), (0.6, (0.15, 0.16, 0.13)), (0.85, (0.24, 0.25, 0.21))], bump_strength=1.6),
        "log": mat_surface("Fallen Log", 5.0, [(0.3, (0.03, 0.025, 0.02)), (0.7, (0.08, 0.07, 0.05))], bump_strength=1.0,
                           moss=(0.6, 1.25, (0.06, 0.09, 0.03))),
        "cloth": mat_surface("Cloak", 12.0, [(0.3, (0.025, 0.025, 0.028)), (0.7, (0.06, 0.055, 0.05))], rough=0.95, bump_strength=0.3),
        "rock": mat_surface("Rock", 2.0, [(0.3, (0.05, 0.05, 0.05)), (0.7, (0.12, 0.12, 0.115))], bump_strength=1.0,
                            moss=(0.7, 1.3, (0.05, 0.075, 0.03))),
        "eye": mat_emit("Troll Eye", (0.85, 0.68, 0.3), 0.75),
        "mote": mat_emit("Mote", (0.75, 0.8, 0.78), 0.18),
        "fog": mat_fog("Fog Banks", 0.034, (0.62, 0.68, 0.72), 0.035, (2.0, 24.0), (7.0, 3.0, 0.4)),
        "veil": mat_fog("Fog Veil", 0.075, (0.6, 0.66, 0.7), 0.09, (0.0, 20.0), (14.0, 0.0, 1.0), anisotropy=0.4),
        "mist": mat_fog("Ground Mist", 0.12, (0.7, 0.75, 0.78), 0.09, (-0.5, 1.6), (11.0, -2.0, 0.0), anisotropy=0.2),
    }


def build(percent):
    scene = reset()
    mats = materials()
    land = collection("Land")
    trees = collection("Trees")
    giant = collection("Troll")
    near = collection("Foreground")
    air = collection("Air")
    g = ground(mats, land)
    grass(g, mats, land)
    forest(mats, trees)
    troll(mats, giant)
    log_and_traveller(mats, near)
    fog(mats, air)
    sky(mats, air)
    spores(mats, air)
    light_and_world(scene)
    camera(scene)
    render_settings(scene, percent)
    return scene


def main():
    a = args()
    mode = a[0]
    if mode == "still":
        scene = build(a[2])
        # Stepped up to the frame, so the spores drift there as they would in the shot.
        for f in range(1, int(a[1]) + 1, 1):
            scene.frame_set(f)
        scene.render.filepath = a[3]
        bpy.ops.render.render(write_still=True)
    elif mode == "anim":
        scene = build(a[1])
        scene.render.filepath = a[2].rstrip("/\\") + "/frame_"
        bpy.ops.render.render(animation=True)
    elif mode == "blend":
        build(100)
        bpy.ops.wm.save_as_mainfile(filepath=a[1])


if __name__ == "__main__":
    main()
