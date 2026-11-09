"""The Life Orb nodes of Safe Haven, modelled and textured in Blender: Life Orb, Life Mote and Radiant Life Orb.

They replace the stand-ins (a grey cylinder with a glowing sphere on it). The orbs are what is left of the forest's old
magic, kept at old places: each is a weathered standing stone, carved with a band that glows the orb's colour, with
roots growing up out of it like fingers to cradle a sphere of light with slow swirls in it.

- Life Orb: a squat stone about 0.55 m high, three roots, a green orb, the whole about 1.25 m.
- Life Mote: smaller and paler: a cairn of three flat stones, two thin roots and a small mote, about 1.1 m.
- Radiant Life Orb: grander: a taller carved stone, five roots and a larger cyan orb, about 1.45 m.

Baked with prop_bake.py (scanned stone and bark: Poly Haven rock_boulder_dry and bark_brown_02) into Albedo, Normal,
ORM, Height (parallax), BlendMask (moss) and Emission. Writes Assets/_Project/Art/Gathering/<Asset>/, a preview render
to the path given, and Orbs.blend beside this script.

Run: blender -b --factory-startup --python Tools/Blender/orb_nodes.py -- <project root> <preview.png> [asset ...]
(assets: orb, mote, radiant; all when none are named).
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

PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "orb_preview.png")
WANTED = set(args[2:]) or {"orb", "mote", "radiant"}


# ----- Materials ----------------------------------------------------------------------------------------------------

def feed(tree, to_socket):
    """The socket feeding to_socket (to insert nodes after it)."""
    return to_socket.links[0].from_socket


def carved_stone(name, glow, band=(0.25, 0.42), lines=3):
    """Old standing stone with a carved band round it: zigzag grooves, cut in (low in the height) and glowing."""
    material = rock_material(name, saturation=0.5, brightness=0.6, scale=1.4)
    tree = material.node_tree
    links = tree.links
    bsdf = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
    height_tex = labelled(tree, "SH Height")
    height_tex.label = ""
    coords = node(tree, "ShaderNodeTexCoord", (-1400, -900))
    split = node(tree, "ShaderNodeSeparateXYZ", (-1200, -900))
    links.new(coords.outputs["Object"], split.inputs["Vector"])
    # Grooves: lines running round the stone, zigzagging with the angle round it.
    angle = node(tree, "ShaderNodeMath", (-1000, -1000))
    angle.operation = 'ARCTAN2'
    links.new(split.outputs["Y"], angle.inputs[0])
    links.new(split.outputs["X"], angle.inputs[1])
    zig = node(tree, "ShaderNodeMath", (-850, -1000))
    zig.operation = 'PINGPONG'
    zig.inputs[1].default_value = math.pi / 9
    links.new(angle.outputs["Value"], zig.inputs[0])
    lifted = node(tree, "ShaderNodeMath", (-700, -950))
    lifted.operation = 'MULTIPLY_ADD'
    lifted.inputs[1].default_value = 0.08
    links.new(zig.outputs["Value"], lifted.inputs[0])
    links.new(split.outputs["Z"], lifted.inputs[2])
    rings = node(tree, "ShaderNodeMath", (-550, -950))
    rings.operation = 'PINGPONG'
    rings.inputs[1].default_value = (band[1] - band[0]) / (2 * lines)
    links.new(lifted.outputs["Value"], rings.inputs[0])
    groove = ramp(tree, (-400, -950), [(0.0, (1, 1, 1)), (0.006, (0, 0, 0))])
    links.new(rings.outputs["Value"], groove.inputs["Fac"])
    in_band = ramp(tree, (-700, -1200), [(band[0] - 0.005, (0, 0, 0)), (band[0], (1, 1, 1)), (band[1], (1, 1, 1)),
                                         (band[1] + 0.005, (0, 0, 0))])
    links.new(split.outputs["Z"], in_band.inputs["Fac"])
    cut = node(tree, "ShaderNodeMath", (-200, -1000))
    cut.operation = 'MULTIPLY'
    links.new(groove.outputs["Color"], cut.inputs[0])
    links.new(in_band.outputs["Color"], cut.inputs[1])
    grooves = cut.outputs["Value"]
    lit = mix(tree, (100, -1000), rgb(tree, (-100, -1100), (0, 0, 0)), rgb(tree, (-100, -1200), glow), grooves)
    links.new(lit, bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    glow_out = tree.nodes.new("NodeReroute")
    links.new(lit, glow_out.inputs[0])
    glow_out.label = "SH Glow"
    colour = mix(tree, (300, 300), feed(tree, bsdf.inputs["Base Color"]), rgb(tree, (100, 200), tuple(c * 0.6 for c in glow)), grooves)
    links.new(colour, bsdf.inputs["Base Color"])
    height = node(tree, "ShaderNodeMath", (200, -600))
    height.operation = 'MULTIPLY_ADD'
    height.inputs[1].default_value = -0.5
    links.new(grooves, height.inputs[0])
    links.new(height_tex.outputs["Color"], height.inputs[2])
    clamp = node(tree, "ShaderNodeClamp", (400, -600))
    links.new(height.outputs["Value"], clamp.inputs["Value"])
    clamp.label = "SH Height"
    return material


def orb_material(name, colour, core):
    """A sphere of light: the orb's colour, brighter in slow swirls; glassy."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    coords = node(tree, "ShaderNodeTexCoord", (-900, 0))
    swirl = node(tree, "ShaderNodeTexNoise", (-700, 0), Scale=4.0, Detail=3.0, Distortion=2.5)
    links.new(coords.outputs["Object"], swirl.inputs["Vector"])
    light = ramp(tree, (-450, 0), [(0.35, colour), (0.7, core)])
    links.new(swirl.outputs["Fac"], light.inputs["Fac"])
    links.new(light.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(light.outputs["Color"], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.0
    bsdf.inputs["Roughness"].default_value = 0.08
    glow = tree.nodes.new("NodeReroute")
    links.new(light.outputs["Color"], glow.inputs[0])
    glow.label = "SH Glow"
    return material


# ----- Shapes -------------------------------------------------------------------------------------------------------

def cradle(bm, uv, rng, base_z, rim, orb_centre, orb_radius, count, thickness):
    """Roots growing up out of the stone's top and curling in round the orb, like fingers holding it."""
    for k in range(count):
        a = 2 * math.pi * k / count + rng.uniform(-0.25, 0.25)
        out = Vector((math.cos(a), math.sin(a), 0))
        start = Vector((out.x * rim * 0.6, out.y * rim * 0.6, base_z - 0.03))
        points = []
        steps = 14
        for i in range(steps + 1):
            t = i / steps
            # Out and up round the orb's side, then over and in above its middle.
            reach = orb_radius * (1.0 + 0.35 * math.sin(math.pi * min(1.0, t * 1.15)))
            angle_up = -math.pi / 2 + t * math.pi * 0.95  # from under the orb to over its top
            p = orb_centre + out * reach * math.cos(angle_up) + Vector((0, 0, reach * math.sin(angle_up)))
            p = start.lerp(p, min(1.0, t * 3.0))
            twist = Vector((-out.y, out.x, 0)) * 0.03 * math.sin(t * 7 + k)
            points.append(p + twist)
        radii = [thickness * (1.0 - 0.75 * i / steps) for i in range(steps + 1)]
        tube(bm, points, radii, 6, uv, wood_cap=True, rng=rng, material=1, cap_material=1, tile=0.25)


def orb_sphere(bm, centre, radius, material_index):
    result = bmesh.ops.create_icosphere(bm, subdivisions=3, radius=radius, matrix=Matrix.Translation(centre))
    for f in {f for v in result["verts"] for f in v.link_faces}:
        f.material_index = material_index
        f.smooth = True


def shrine(name, rng, coll, stone_size, stones, orb_radius, roots, root_thickness, colour, core, glow):
    """Standing stone(s), the roots' cradle and the orb, as a low and a high mesh."""
    stone_material = carved_stone(f"{name} Stone (bake)", glow, band=(stone_size[2] * 0.35, stone_size[2] * 0.62))
    bark = bark_material(f"{name} Root (bake)", tint=(0.55, 0.5, 0.45))
    orb_mat = orb_material(f"{name} Light (bake)", colour, core)
    lows, highs = [], []
    top = 0.0
    for i in range(stones):
        size = tuple(s * (1.0 - 0.18 * i) for s in stone_size)
        low, high = stone(f"{name} Stone {i}", size, rng, coll, high_subdiv=6, low_faces=700, splits=(5, 7),
                          cut=(0.55, 0.75), lumps=0.04)
        lift = Matrix.Translation((rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), top - (0.04 if i else 0.0)))
        turn = Matrix.Rotation(rng.uniform(0, 6.28), 4, 'Z')
        for obj in (low, high):
            obj.data.transform(lift @ turn)
        top = max(v.co.z for v in high.data.vertices) - 0.03
        lows.append(low)
        highs.append(high)
    low = join(lows, name)
    high = join(highs, f"{name} High")
    for obj in (low, high):
        obj.data.materials.append(stone_material)
        obj.data.materials.append(bark)
        obj.data.materials.append(orb_mat)
    orb_centre = Vector((0, 0, top + orb_radius * 1.25))
    seed = rng.random()
    for obj in (low, high):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        uv = bm.loops.layers.uv.get("grain") or bm.loops.layers.uv.new("grain")
        cradle(bm, uv, random.Random(seed), top, stone_size[0], orb_centre, orb_radius, roots, root_thickness)
        orb_sphere(bm, orb_centre, orb_radius, 2)
        bm.to_mesh(obj.data)
        bm.free()
    return low, high


def build_orb(coll):
    low, high = shrine("Life Orb A", random.Random(501), coll, (0.3, 0.27, 0.62), 1, 0.17, 3, 0.03,
                       (0.12, 0.75, 0.32), (0.75, 1.0, 0.7), (0.25, 0.95, 0.45))
    return [low], [high]


def build_mote(coll):
    low, high = shrine("Life Mote A", random.Random(511), coll, (0.26, 0.24, 0.22), 3, 0.1, 2, 0.022,
                       (0.35, 0.85, 0.45), (0.85, 1.0, 0.85), (0.5, 0.95, 0.6))
    return [low], [high]


def build_radiant(coll):
    low, high = shrine("Radiant Life Orb A", random.Random(521), coll, (0.34, 0.3, 0.8), 1, 0.21, 5, 0.034,
                       (0.08, 0.78, 0.7), (0.75, 1.0, 0.95), (0.2, 0.95, 0.85))
    return [low], [high]


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rows = []
    for key, name, builder in (("orb", "Life Orb", build_orb), ("mote", "Life Mote", build_mote),
                               ("radiant", "Radiant Life Orb", build_radiant)):
        if key in WANTED:
            rows.append(make_asset(name, builder, 1024, collection(name), glow=True, cavity_distance=0.06))
    objects = [row[0] for row in rows]
    stage([(objects, 1.3)])
    camera((0.0, -3.4, 2.6), (0.0, 0.0, 0.55), lens=40)
    render(PREVIEW, 1600, 1000)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "Orbs.blend"))
    print("ORBS DONE")


if __name__ == "__main__":
    main()
