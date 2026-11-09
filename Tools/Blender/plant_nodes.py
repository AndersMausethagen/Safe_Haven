"""The plants the player gathers in Safe Haven, modelled and textured in Blender: Healing Herb, Flax, Huldra Silk,
Nettle, Chanterelles, Cloudberries, Wild Barley and Wild Turnips.

They replace the stand-ins (spheres and cylinders, or a borrowed bush). Each is a Nordic plant the item comes from, at
real size or a little over, so it reads from the game's camera:

- Healing Herb: yarrow, feathery leaves and flat heads of small white flowers.
- Flax: a clump of thin stems with narrow leaves, sky-blue five-petalled flowers and seed bolls.
- Huldra Silk: cotton grass: a tussock of grass blades with silky white tufts on thin stems, glowing faintly.
- Nettle: a clump of upright stinging nettles, pairs of heart-shaped, saw-edged leaves.
- Chanterelles: golden funnel-shaped mushrooms with ridges under their wavy caps, in a little moss.
- Cloudberries: a low patch of round, lobed, wrinkled leaves with amber berries made of beads.
- Wild Barley: straw stalks with drooping, bearded golden heads.
- Wild Turnips: purple-shouldered white roots showing at the ground, under a rosette of lobed leaves.

Built from leaf blades (outlines), stems (tubes), petals, grain heads, lathed shapes (caps, roots) and bead berries;
coloured procedurally (each leaf a little different, its "sh_var") and baked with prop_bake.py into Albedo, Normal
(veins, ridges), ORM, Height, BlendMask and, for Huldra Silk, Emission. Leaves face up and out (one-sided in the game);
grass blades and petals have both sides. Writes Assets/_Project/Art/Gathering/<Asset>/, a preview render to the path
given, and Plants.blend beside this script.

Run: blender -b --factory-startup --python Tools/Blender/plant_nodes.py -- <project root> <preview.png> [asset ...]
(assets: herb, flax, silk, nettle, chanterelle, cloudberry, barley, turnip; all when none are named).
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

PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "plant_preview.png")
WANTED = set(args[2:]) or {"herb", "flax", "silk", "nettle", "chanterelle", "cloudberry", "barley", "turnip"}

UP = Vector((0, 0, 1))


# ----- Materials ----------------------------------------------------------------------------------------------------

def varied(name, stops, roughness=0.6, veins=0.0, midrib=0.0, rim_dark=0.0, glow=None, sheen=0.35, strands=False):
    """A plant surface coloured by each part's "sh_var" (stops: a colour ramp over it), with optional veins and midrib
    (on the part's grain UVs: U across, V along) and a darker rim; the veins are the relief the normal map gets."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    attribute = tree.nodes.new("ShaderNodeAttribute")
    attribute.attribute_name = "sh_var"
    attribute.attribute_type = 'GEOMETRY'
    attribute.location = (-900, 400)
    colours = ramp(tree, (-650, 400), stops)
    links.new(attribute.outputs["Fac"], colours.inputs["Fac"])
    colour = colours.outputs["Color"]
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-1200, -100)
    split = node(tree, "ShaderNodeSeparateXYZ", (-1000, -100))
    links.new(uv.outputs["UV"], split.inputs["Vector"])
    centred = node(tree, "ShaderNodeMath", (-850, -100))
    centred.operation = 'SUBTRACT'
    centred.inputs[1].default_value = 0.5
    links.new(split.outputs["X"], centred.inputs[0])
    across = node(tree, "ShaderNodeMath", (-700, -100))
    across.operation = 'ABSOLUTE'
    links.new(centred.outputs["Value"], across.inputs[0])
    relief = value(tree, (-400, -500), 0.0)
    if veins > 0 or midrib > 0:
        slant = node(tree, "ShaderNodeMath", (-700, -300))
        slant.operation = 'MULTIPLY_ADD'
        slant.inputs[1].default_value = -0.9
        links.new(across.outputs["Value"], slant.inputs[0])
        links.new(split.outputs["Y"], slant.inputs[2])
        lines = node(tree, "ShaderNodeMath", (-550, -300))
        lines.operation = 'PINGPONG'
        lines.inputs[1].default_value = 0.07
        links.new(slant.outputs["Value"], lines.inputs[0])
        vein_mask = ramp(tree, (-400, -300), [(0.0, (veins, veins, veins)), (0.014, (0, 0, 0))])
        links.new(lines.outputs["Value"], vein_mask.inputs["Fac"])
        rib = ramp(tree, (-400, -100), [(0.0, (midrib, midrib, midrib)), (0.04, (0, 0, 0))])
        links.new(across.outputs["Value"], rib.inputs["Fac"])
        both = node(tree, "ShaderNodeMath", (-200, -200))
        both.operation = 'MAXIMUM'
        links.new(vein_mask.outputs["Color"], both.inputs[0])
        links.new(rib.outputs["Color"], both.inputs[1])
        relief = both.outputs["Value"]
        colour = mix(tree, (0, 300), colour, rgb(tree, (-200, 250), (1.35, 1.35, 1.15)), relief, blend='MULTIPLY')
    if rim_dark > 0:
        rim = ramp(tree, (-450, 100), [(0.3, (1, 1, 1)), (0.5, (1 - rim_dark, 1 - rim_dark, 1 - rim_dark))])
        links.new(across.outputs["Value"], rim.inputs["Fac"])
        colour = mix(tree, (200, 300), colour, rim.outputs["Color"], value(tree, (0, 150), 1.0), blend='MULTIPLY')
    if strands:
        # Silk: fine fibres running from the stem out (along the grain's V), each a little lighter or darker.
        fibres = node(tree, "ShaderNodeMapping", (-1000, -700))
        fibres.inputs["Scale"].default_value = (90.0, 3.0, 1.0)
        links.new(uv.outputs["UV"], fibres.inputs["Vector"])
        strand = node(tree, "ShaderNodeTexNoise", (-800, -700), Scale=1.0, Detail=6.0, Roughness=0.7)
        links.new(fibres.outputs["Vector"], strand.inputs["Vector"])
        strand_ramp = ramp(tree, (-600, -700), [(0.25, (0, 0, 0)), (0.75, (1, 1, 1))])
        links.new(strand.outputs["Fac"], strand_ramp.inputs["Fac"])
        relief = strand_ramp.outputs["Color"]
        colour = mix(tree, (0, 600), colour, rgb(tree, (-200, 600), (0.82, 0.82, 0.8)), relief, blend='MULTIPLY')
    blotch = node(tree, "ShaderNodeTexNoise", (-650, 700), Scale=30.0, Detail=4.0)
    shade = ramp(tree, (-400, 700), [(0.3, (0.88, 0.88, 0.88)), (0.7, (1.08, 1.08, 1.05))])
    links.new(blotch.outputs["Fac"], shade.inputs["Fac"])
    colour = mix(tree, (400, 400), colour, shade.outputs["Color"], value(tree, (200, 500), 1.0), blend='MULTIPLY')
    links.new(colour, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Specular IOR Level"].default_value = sheen
    bump = node(tree, "ShaderNodeBump", (700, -300), Strength=0.35, Distance=0.001)
    bump.invert = True
    links.new(relief, bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    if glow:
        glow_colour = mix(tree, (600, -700), rgb(tree, (400, -700), (0, 0, 0)), colour, value(tree, (400, -800), glow))
        links.new(glow_colour, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = 1.0
        out = tree.nodes.new("NodeReroute")
        links.new(glow_colour, out.inputs[0])
        out.label = "SH Glow"
    return material


def gills_material(name, stops):
    """Chanterelle: golden, with false gills (ridges) running down under the cap: lines across the grain's U."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    attribute = tree.nodes.new("ShaderNodeAttribute")
    attribute.attribute_name = "sh_var"
    attribute.attribute_type = 'GEOMETRY'
    attribute.location = (-900, 300)
    colours = ramp(tree, (-650, 300), stops)
    links.new(attribute.outputs["Fac"], colours.inputs["Fac"])
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-1100, -200)
    split = node(tree, "ShaderNodeSeparateXYZ", (-900, -200))
    links.new(uv.outputs["UV"], split.inputs["Vector"])
    ridges = node(tree, "ShaderNodeMath", (-700, -200))
    ridges.operation = 'PINGPONG'
    ridges.inputs[1].default_value = 1.0 / 60.0
    links.new(split.outputs["X"], ridges.inputs[0])
    ridge = ramp(tree, (-500, -200), [(0.0, (1, 1, 1)), (1.0 / 60.0, (0, 0, 0))])
    links.new(ridges.outputs["Value"], ridge.inputs["Fac"])
    under = ramp(tree, (-500, -450), [(0.45, (1, 1, 1)), (0.55, (0, 0, 0))])  # V below 0.5 is the underside
    links.new(split.outputs["Y"], under.inputs["Fac"])
    gills = node(tree, "ShaderNodeMath", (-300, -300))
    gills.operation = 'MULTIPLY'
    links.new(ridge.outputs["Color"], gills.inputs[0])
    links.new(under.outputs["Color"], gills.inputs[1])
    colour = mix(tree, (0, 300), colours.outputs["Color"], rgb(tree, (-200, 150), (0.8, 0.72, 0.6)), gills.outputs["Value"],
                 blend='MULTIPLY')
    links.new(colour, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.55
    bump = node(tree, "ShaderNodeBump", (500, -300), Strength=0.5, Distance=0.0015)
    links.new(gills.outputs["Value"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    height = node(tree, "ShaderNodeMath", (300, -600))
    height.operation = 'MULTIPLY_ADD'
    height.inputs[1].default_value = 0.4
    height.inputs[2].default_value = 0.4
    links.new(gills.outputs["Value"], height.inputs[0])
    height.label = "SH Height"
    return material


# ----- Parts --------------------------------------------------------------------------------------------------------

class Kit:
    """One mesh being built, with its grain UVs and per-face variation."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("grain")
        self.var = self.bm.faces.layers.float.new("sh_var")

    def face(self, verts, uvs, material, var, facing=None):
        try:
            f = self.bm.faces.new(verts)
        except ValueError:
            return None
        f.material_index = material
        f[self.var] = var
        for loop, coord in zip(f.loops, uvs):
            loop[self.uv].uv = coord
        f.normal_update()
        if facing is not None and f.normal.dot(facing) < 0:
            f.normal_flip()
        return f

    def blade(self, base, direction, up, length, width, outline, material, var, fold=0.2, curl=0.15, droop=0.0,
              double=False, serrate=0.0):
        """A leaf, petal or grass blade from an outline [(across, along)] (half of it, from stalk to tip), folded a
        little along its middle and curling at the tip; one-sided (facing up) unless double."""
        direction = direction.normalized()
        side = direction.cross(up).normalized()
        normal = side.cross(direction).normalized()
        sides = []
        for sign in (-1, 1):
            row = []
            for i, (across, along) in enumerate(outline):
                if serrate and 0 < i < len(outline) - 1:
                    across *= 1.0 + serrate * (1 if i % 2 else -1)
                a = across * sign
                p = base + direction * along * length + side * a * width
                p += normal * (abs(a) * fold * width - (along ** 2) * curl * length)
                p += UP * (-droop * along ** 2 * length)
                row.append(p)
            sides.append(row)
        for flip in ((False, True) if double else (False,)):
            facing = -normal if flip else normal
            verts = [[self.bm.verts.new(p) for p in row] for row in sides]
            mid = [self.bm.verts.new(base + direction * along * length + normal * (-(along ** 2) * curl * length)
                                     + UP * (-droop * along ** 2 * length)) for _, along in outline]
            for s, row in enumerate(verts):
                sign = -1 if s == 0 else 1
                for i in range(len(outline) - 1):
                    quad = (row[i], row[i + 1], mid[i + 1], mid[i])
                    uvs = [(0.5 + outline[i][0] * sign * 0.5 / 0.5 * 0.5, outline[i][1]),
                           (0.5 + outline[i + 1][0] * sign * 0.5 / 0.5 * 0.5, outline[i + 1][1]),
                           (0.5, outline[i + 1][1]), (0.5, outline[i][1])]
                    if outline[i][0] == 0 and outline[i + 1][0] == 0:
                        continue
                    if outline[i][0] == 0:
                        self.face((row[i + 1], mid[i + 1], mid[i]), uvs[1:], material, var, facing)
                    elif outline[i + 1][0] == 0:
                        self.face((row[i], mid[i + 1], mid[i]), [uvs[0], uvs[2], uvs[3]], material, var, facing)
                    else:
                        self.face(quad, uvs, material, var, facing)

    def stem(self, points, radius, tip_radius, material, var, sides=5):
        start = len(self.bm.faces)
        radii = [radius + (tip_radius - radius) * i / (len(points) - 1) for i in range(len(points))]
        tube(self.bm, points, radii, sides, self.uv, wood_cap=True, material=material, cap_material=material, tile=0.25)
        self.bm.faces.ensure_lookup_table()
        for f in self.bm.faces[start:]:
            f[self.var] = var

    def blob(self, centre, radius, material, var, squash=(1, 1, 1), subdivisions=1, lumpy=0.0, seed=0.0):
        result = bmesh.ops.create_icosphere(self.bm, subdivisions=subdivisions, radius=1.0)
        for v in result["verts"]:
            d = v.co.normalized()
            r = radius * (1.0 + lumpy * noise.noise(d * 2.0 + Vector((seed, seed, seed))))
            v.co = centre + Vector((d.x * squash[0], d.y * squash[1], d.z * squash[2])) * r
        for f in {f for v in result["verts"] for f in v.link_faces}:
            f.material_index = material
            f[self.var] = var
            f.smooth = True
            for loop in f.loops:
                d = (loop.vert.co - centre).normalized()
                loop[self.uv].uv = (0.5 + math.atan2(d.y, d.x) / (2 * math.pi), (d.z + 1) / 2)

    def lathe(self, centre, profile, segments, material, var, wobble=0.0, seed=0.0, axis=UP):
        """A turned shape: profile [(radius, height)] from bottom to top, U round, V along the profile."""
        turn = UP.rotation_difference(axis).to_matrix().to_4x4()
        rings = []
        for j, (r, h) in enumerate(profile):
            ring = []
            for k in range(segments):
                a = 2 * math.pi * k / segments
                rr = r * (1.0 + wobble * noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, seed + j * 0.1))))
                ring.append(self.bm.verts.new(centre + turn @ Vector((math.cos(a) * rr, math.sin(a) * rr, h))))
            rings.append(ring)
        for j in range(len(profile) - 1):
            for k in range(segments):
                k2 = (k + 1) % segments
                uvs = [(k / segments, j / (len(profile) - 1)), (k / segments, (j + 1) / (len(profile) - 1)),
                       ((k + 1) / segments, (j + 1) / (len(profile) - 1)), ((k + 1) / segments, j / (len(profile) - 1))]
                f = self.face((rings[j][k], rings[j + 1][k], rings[j + 1][k2], rings[j][k2]), uvs, material, var)
                if f:
                    f.smooth = True
        return rings

    def object(self, name, coll, materials):
        obj = mesh_object(name, self.bm, coll, materials)
        return obj


def ring_points(base, direction, length, segments, bend=0.0, rng=None, wobble=0.02):
    """A stem's points, bending over towards its tip."""
    direction = direction.normalized()
    side = direction.cross(UP).normalized() if abs(direction.z) < 0.99 else Vector((1, 0, 0))
    points = []
    seed = rng.uniform(0, 99) if rng else 0.0
    for i in range(segments + 1):
        t = i / segments
        p = base + direction * length * t
        p += -UP * bend * length * t * t + side * wobble * length * noise.noise(Vector((seed, t * 3, 0)))
        points.append(p)
    return points


def tuft(rng, count, spread, centre=Vector()):
    """Where the stems of a clump rise from, and which way they lean."""
    out = []
    for _ in range(count):
        a = rng.uniform(0, 2 * math.pi)
        r = spread * math.sqrt(rng.random())
        base = centre + Vector((math.cos(a) * r, math.sin(a) * r, -0.02))
        lean = Vector((math.cos(a) * (r / max(spread, 1e-6)) * 0.35 + rng.uniform(-0.1, 0.1),
                       math.sin(a) * (r / max(spread, 1e-6)) * 0.35 + rng.uniform(-0.1, 0.1), 1.0))
        out.append((base, lean.normalized()))
    return out


# ----- Outlines (half of each, across and along) --------------------------------------------------------------------

OVATE = [(0.0, 0.0), (0.32, 0.22), (0.38, 0.55), (0.2, 0.85), (0.0, 1.0)]
HEART = [(0.0, 0.0), (0.42, 0.12), (0.45, 0.4), (0.3, 0.7), (0.12, 0.9), (0.0, 1.0)]
NARROW = [(0.0, 0.0), (0.25, 0.3), (0.2, 0.75), (0.0, 1.0)]
GRASS = [(0.0, 0.0), (0.5, 0.05), (0.45, 0.6), (0.0, 1.0)]
PETAL = [(0.0, 0.0), (0.28, 0.25), (0.42, 0.7), (0.3, 0.95), (0.0, 1.0)]
ROUND = [(0.0, 0.0), (0.45, 0.15), (0.55, 0.5), (0.4, 0.85), (0.0, 1.0)]
FEATHER = [(0.0, 0.0), (0.3, 0.1), (0.1, 0.18), (0.32, 0.3), (0.1, 0.4), (0.3, 0.52), (0.1, 0.62), (0.26, 0.74),
           (0.08, 0.85), (0.0, 1.0)]
LOBED = [(0.0, 0.0), (0.4, 0.15), (0.25, 0.3), (0.45, 0.45), (0.25, 0.6), (0.38, 0.75), (0.0, 1.0)]

GREEN = [(0.0, (0.05, 0.11, 0.025)), (0.5, (0.09, 0.17, 0.035)), (0.9, (0.14, 0.2, 0.045)), (1.0, (0.3, 0.28, 0.07))]
DARK_GREEN = [(0.0, (0.03, 0.07, 0.02)), (0.5, (0.055, 0.11, 0.03)), (1.0, (0.09, 0.15, 0.04))]
GREY_GREEN = [(0.0, (0.08, 0.13, 0.05)), (0.6, (0.13, 0.18, 0.08)), (1.0, (0.2, 0.22, 0.1))]


# ----- Plants -------------------------------------------------------------------------------------------------------

def flower(kit, centre, facing, petals, length, width, material, var, rng, cup=0.25, double=True):
    side = facing.orthogonal().normalized()
    for k in range(petals):
        a = 2 * math.pi * k / petals + rng.uniform(-0.1, 0.1)
        out = (side * math.cos(a) + facing.cross(side) * math.sin(a)).normalized()
        direction = (out + facing * cup).normalized()
        kit.blade(centre, direction, facing, length, width, PETAL, material, var, fold=0.1, curl=-0.05, double=double)


def healing_herb(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    leaf = varied("Yarrow Leaf (bake)", GREY_GREEN, veins=0.0, midrib=0.6)
    stalk = varied("Yarrow Stem (bake)", [(0.0, (0.12, 0.16, 0.06)), (1.0, (0.18, 0.2, 0.08))], roughness=0.7)
    bloom = varied("Yarrow Flower (bake)", [(0.0, (0.72, 0.7, 0.62)), (0.7, (0.85, 0.83, 0.76)), (1.0, (0.85, 0.7, 0.72))],
                   roughness=0.8)
    # Feathery leaves round the foot.
    for k in range(16):
        a = 2 * math.pi * k / 16 + rng.uniform(-0.2, 0.2)
        out = Vector((math.cos(a), math.sin(a), rng.uniform(0.2, 0.7))).normalized()
        kit.blade(Vector((0, 0, 0.02)), out, UP, rng.uniform(0.16, 0.24), 0.06, FEATHER, 0, rng.random(), fold=0.3,
                  curl=0.25, double=True)
    for base, lean in tuft(rng, 9, 0.1):
        height = rng.uniform(0.38, 0.55)
        points = ring_points(base, lean, height, 6, bend=0.05, rng=rng)
        kit.stem(points, 0.006, 0.004, 1, rng.random())
        for i in (2, 3, 4):
            kit.blade(points[i], (lean + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.3))).normalized(), UP,
                      rng.uniform(0.08, 0.12), 0.04, FEATHER, 0, rng.random(), fold=0.3, curl=0.2, double=True)
        # A flat head of tiny flowers.
        top = points[-1]
        for _ in range(14):
            a, r = rng.uniform(0, 6.28), 0.045 * math.sqrt(rng.random())
            at = top + Vector((math.cos(a) * r, math.sin(a) * r, 0.01 * rng.random()))
            flower(kit, at, UP, 5, 0.012, 0.012, 2, rng.random(), rng, cup=0.1, double=False)
    return kit.object(name, coll, (leaf, stalk, bloom))


def flax(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    leaf = varied("Flax Leaf (bake)", [(0.0, (0.1, 0.17, 0.08)), (1.0, (0.16, 0.24, 0.1))], midrib=0.4)
    stalk = varied("Flax Stem (bake)", [(0.0, (0.14, 0.2, 0.08)), (1.0, (0.22, 0.26, 0.1))], roughness=0.7)
    bloom = varied("Flax Flower (bake)", [(0.0, (0.18, 0.32, 0.75)), (0.7, (0.3, 0.45, 0.88)), (1.0, (0.45, 0.55, 0.92))],
                   roughness=0.6, veins=0.5)
    boll = varied("Flax Boll (bake)", [(0.0, (0.35, 0.33, 0.12)), (1.0, (0.55, 0.5, 0.22))], roughness=0.5)
    for base, lean in tuft(rng, 70, 0.32):
        height = rng.uniform(0.5, 0.75)
        points = ring_points(base, lean, height, 7, bend=rng.uniform(0.0, 0.08), rng=rng)
        kit.stem(points, 0.0035, 0.002, 1, rng.random(), sides=4)
        for i in range(1, 6):
            if rng.random() < 0.7:
                d = (Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0)) + lean * 1.5).normalized()
                kit.blade(points[i], d, UP, rng.uniform(0.03, 0.05), 0.012, NARROW, 0, rng.random(), double=True)
        top = points[-1]
        roll = rng.random()
        if roll < 0.55:
            facing = (lean + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), 0.6))).normalized()
            flower(kit, top, facing, 5, 0.022, 0.022, 2, rng.random(), rng, cup=0.25)
        elif roll < 0.85:
            kit.blob(top + UP * 0.006, 0.0065, 3, rng.random(), subdivisions=0)
    return kit.object(name, coll, (leaf, stalk, bloom, boll))


def huldra_silk(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    grass = varied("Cotton Grass (bake)", [(0.0, (0.08, 0.13, 0.05)), (0.7, (0.14, 0.2, 0.07)), (1.0, (0.35, 0.3, 0.12))],
                   midrib=0.3)
    stalk = varied("Cotton Grass Stem (bake)", [(0.0, (0.15, 0.18, 0.08)), (1.0, (0.25, 0.25, 0.1))], roughness=0.7)
    silk = varied("Huldra Silk (bake)", [(0.0, (0.78, 0.79, 0.76)), (1.0, (0.9, 0.9, 0.86))], roughness=0.95, glow=0.12,
                  strands=True)
    for base, lean in tuft(rng, 90, 0.35):
        d = (lean + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), 0))).normalized()
        kit.blade(base, d, Vector((d.y, -d.x, 0)).normalized() if abs(d.z) < 0.99 else UP, rng.uniform(0.25, 0.45), 0.014,
                  GRASS, 0, rng.random(), fold=0.4, curl=0.0, droop=rng.uniform(0.2, 0.6), double=True)
    for base, lean in tuft(rng, 26, 0.3):
        points = ring_points(base, lean, rng.uniform(0.4, 0.62), 6, bend=0.03, rng=rng)
        kit.stem(points, 0.003, 0.002, 1, rng.random(), sides=4)
        # The silk: a soft tuft of white threads.
        top = points[-1] + UP * 0.02
        kit.blob(top, 0.028, 2, rng.random(), squash=(1.0, 1.0, 1.25), subdivisions=2, lumpy=0.35, seed=rng.random() * 50)
    return kit.object(name, coll, (grass, stalk, silk))


def nettle(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    leaf = varied("Nettle Leaf (bake)", DARK_GREEN, veins=0.7, midrib=0.8, rim_dark=0.1)
    stalk = varied("Nettle Stem (bake)", [(0.0, (0.1, 0.13, 0.05)), (1.0, (0.18, 0.15, 0.07))], roughness=0.75)
    for base, lean in tuft(rng, 16, 0.3):
        height = rng.uniform(0.55, 0.85)
        points = ring_points(base, lean, height, 9, bend=0.04, rng=rng)
        kit.stem(points, 0.006, 0.003, 1, rng.random(), sides=4)
        turn = rng.uniform(0, math.pi)
        for i in range(2, 10):
            # Opposite pairs, each pair turned a quarter from the last, smaller towards the top.
            for sign in (-1, 1):
                a = turn + (i % 2) * math.pi / 2 + (0 if sign > 0 else math.pi)
                d = Vector((math.cos(a), math.sin(a), 0.15 + 0.06 * i)).normalized()
                size = 0.11 * (1.15 - i / 11)
                kit.blade(points[i], d, UP, size, size * 0.75, HEART, 0, rng.random(), fold=0.25, curl=0.3,
                          droop=0.15, serrate=0.18)
    return kit.object(name, coll, (leaf, stalk))


def chanterelles(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    cap = gills_material("Chanterelle (bake)", [(0.0, (0.85, 0.42, 0.03)), (0.6, (0.93, 0.55, 0.05)), (1.0, (0.96, 0.64, 0.1))])
    for k in range(rng.randint(6, 8)):
        a, r = rng.uniform(0, 6.28), 0.22 * math.sqrt(rng.random())
        at = Vector((math.cos(a) * r, math.sin(a) * r, -0.01))
        size = rng.uniform(0.8, 1.5)
        tilt = Vector((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), 1.0)).normalized()
        # Stem into funnel into wavy cap rim, then back under it.
        profile = [(0.012, 0.0), (0.011, 0.02), (0.014, 0.04), (0.024, 0.055), (0.038, 0.065), (0.045, 0.07),
                   (0.042, 0.066), (0.03, 0.058), (0.018, 0.045), (0.012, 0.03)]
        profile = [(rr * size, h * size) for rr, h in profile]
        kit.lathe(at, profile, 16, 0, rng.random(), wobble=0.12, seed=rng.random() * 30, axis=tilt)
    return kit.object(name, coll, (cap,))


def cloudberries(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    leaf = varied("Cloudberry Leaf (bake)", GREEN, veins=0.8, midrib=0.6, rim_dark=0.15)
    stalk = varied("Cloudberry Stem (bake)", [(0.0, (0.12, 0.13, 0.05)), (1.0, (0.2, 0.17, 0.08))], roughness=0.7)
    berry = varied("Cloudberry (bake)", [(0.0, (0.85, 0.42, 0.08)), (0.6, (0.92, 0.55, 0.12)), (1.0, (0.95, 0.68, 0.25))],
                   roughness=0.35, sheen=0.6)
    for base, lean in tuft(rng, 24, 0.26):
        height = rng.uniform(0.08, 0.16)
        points = ring_points(base, lean, height, 3, rng=rng)
        kit.stem(points, 0.004, 0.003, 1, rng.random(), sides=4)
        d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.25)).normalized()
        kit.blade(points[-1] - d * 0.03, d, UP, rng.uniform(0.07, 0.1), 0.09, LOBED, 0, rng.random(), fold=0.15, curl=0.1,
                  serrate=0.15)
    for base, lean in tuft(rng, 9, 0.22):
        top = base + UP * rng.uniform(0.16, 0.22) + lean * 0.02
        kit.stem([base, base.lerp(top, 0.5), top], 0.003, 0.0025, 1, rng.random(), sides=4)
        # An aggregate berry: a dome of beads.
        value_ = rng.random()
        for j in range(14):
            phi = math.acos(1 - 2 * (j + 0.5) / 18)
            theta = j * 2.39996
            d = Vector((math.sin(phi) * math.cos(theta), math.sin(phi) * math.sin(theta), math.cos(phi)))
            kit.blob(top + UP * 0.016 + d * 0.015, 0.0075, 2, value_, subdivisions=0)
    return kit.object(name, coll, (leaf, stalk, berry))


def wild_barley(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    straw = varied("Barley Straw (bake)", [(0.0, (0.48, 0.4, 0.17)), (0.6, (0.62, 0.52, 0.24)), (1.0, (0.55, 0.55, 0.25))],
                   roughness=0.6, midrib=0.3)
    grain = varied("Barley Grain (bake)", [(0.0, (0.58, 0.47, 0.2)), (1.0, (0.75, 0.62, 0.3))], roughness=0.55)
    for base, lean in tuft(rng, 26, 0.18):
        height = rng.uniform(0.6, 0.85)
        nod = rng.uniform(0.12, 0.3)
        points = ring_points(base, lean, height, 8, bend=nod, rng=rng)
        kit.stem(points, 0.004, 0.0025, 0, rng.random(), sides=4)
        for i in (1, 3):
            d = (Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.5))).normalized()
            kit.blade(points[i], d, UP, rng.uniform(0.15, 0.22), 0.016, GRASS, 0, rng.random(), droop=0.5, double=True)
        # The head: two rows of grains along the drooping tip, each with a long awn.
        tip = points[-1]
        along = (points[-1] - points[-2]).normalized()
        side = along.cross(UP).normalized()
        v = rng.random()
        for g in range(8):
            at = tip + along * (0.007 * g)
            for sign in (-1, 1):
                p = at + side * sign * 0.004
                kit.blob(p, 0.005, 1, v, squash=(1.0, 1.0, 1.6), subdivisions=0)
                awn = (along + side * sign * 0.25 + UP * 0.35).normalized()
                kit.stem([p, p + awn * 0.05, p + awn * 0.1 - UP * 0.01], 0.0008, 0.0004, 1, v, sides=3)
    return kit.object(name, coll, (straw, grain))


def wild_turnips(name, seed, coll):
    rng = random.Random(seed)
    kit = Kit()
    leaf = varied("Turnip Leaf (bake)", GREEN, veins=0.7, midrib=0.9)
    root = varied("Turnip Root (bake)", [(0.0, (0.82, 0.78, 0.72)), (1.0, (0.88, 0.85, 0.78))], roughness=0.5)
    shoulder = varied("Turnip Shoulder (bake)", [(0.0, (0.42, 0.12, 0.32)), (1.0, (0.55, 0.2, 0.4))], roughness=0.5)
    for k in range(rng.randint(2, 3)):
        a, r = rng.uniform(0, 6.28), 0.08 * k
        at = Vector((math.cos(a) * r, math.sin(a) * r, -0.05))
        size = rng.uniform(0.8, 1.15)
        white = [(0.004, -0.06), (0.025, -0.04), (0.05, 0.0), (0.06, 0.03)]
        purple = [(0.06, 0.03), (0.058, 0.055), (0.045, 0.075), (0.02, 0.088), (0.006, 0.092)]
        kit.lathe(at, [(rr * size, h * size) for rr, h in white], 14, 1, rng.random(), wobble=0.06, seed=rng.random() * 9)
        kit.lathe(at, [(rr * size, h * size) for rr, h in purple], 14, 2, rng.random(), wobble=0.06, seed=rng.random() * 9)
        crown = at + UP * 0.09 * size
        for j in range(rng.randint(6, 9)):
            b = 2 * math.pi * j / 8 + rng.uniform(-0.3, 0.3)
            d = Vector((math.cos(b), math.sin(b), rng.uniform(0.9, 1.6))).normalized()
            kit.blade(crown, d, UP, rng.uniform(0.2, 0.3), 0.09, LOBED, 0, rng.random(), fold=0.2, curl=0.35, droop=0.25)
    return kit.object(name, coll, (leaf, root, shoulder))


# ----- Assets -------------------------------------------------------------------------------------------------------

PLANTS = (
    ("herb", "Healing Herb", healing_herb, 2, False),
    ("flax", "Flax", flax, 2, False),
    ("silk", "Huldra Silk", huldra_silk, 2, True),
    ("nettle", "Nettle", nettle, 2, False),
    ("chanterelle", "Chanterelles", chanterelles, 2, False),
    ("cloudberry", "Cloudberries", cloudberries, 2, False),
    ("barley", "Wild Barley", wild_barley, 2, False),
    ("turnip", "Wild Turnips", wild_turnips, 2, False),
)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rows = []
    for index, (key, name, maker, count, glow) in enumerate(PLANTS):
        if key not in WANTED:
            continue

        def builder(coll, name=name, maker=maker, count=count, index=index):
            return [maker(f"{name} {chr(65 + i)}", 600 + index * 10 + i, coll) for i in range(count)]
        rows.append((make_asset(name, builder, 2048, collection(name), glow=glow, cavity_distance=0.04), 1.1))
    depth = stage(rows, row_spacing=1.1)
    camera((0.0, -depth - 2.6, 4.8), (0.0, -depth * 0.5 + 0.3, 0.2), lens=35)
    render(PREVIEW, 1600, 1600)
    for objects, _ in rows:
        centre = sum((o.location for o in objects), Vector()) / len(objects)
        camera(centre + Vector((0.0, -1.7, 1.6)), centre + Vector((0, 0, 0.2)), lens=40)
        render(PREVIEW.replace(".png", f" {objects[0].name[:-2]}.png"), 1200, 700)
    if len(args) <= 2:  # a full run: keep the .blend of every plant
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "Plants.blend"))
    print("PLANTS DONE")


if __name__ == "__main__":
    main()
