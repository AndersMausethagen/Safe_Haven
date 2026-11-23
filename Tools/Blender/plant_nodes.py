"""The plants the player gathers in Safe Haven, modelled and textured in Blender: the Berry Bush, Healing Herb, Flax,
Huldra Silk, Nettle, Chanterelles, Cloudberries, Wild Barley and Wild Turnips.

Each is a Nordic plant the item comes from, a little over real size so it reads from the game's camera, and built the way
the game's other plants are (see the asset principles): woody or juicy parts (stems, berries, roots, mushrooms) as
geometry, and leaves, flower heads, grass blades and ears of barley as two-sided leaf CARDS cut out of a small sprite
sheet, with the wind data the game's Tree Wind shader reads (uv1 = sway, flex; uv2 = phase, flutter), so they sway
and bend away from the player like the ferns and shrubs. Two levels of detail drop cards at random (the silhouette stays).

- Berry Bush: wild currant, arching woody stems, sprays of saw-edged leaves, strings of red berries on its top.
- Healing Herb: yarrow, feathery leaves round its foot, flat heads of small white flowers.
- Flax: thin stems, narrow leaves, sky-blue five-petalled flowers and seed bolls.
- Huldra Silk: cotton grass, a tussock of grass blades with silky white tufts on thin stems, glowing faintly.
- Nettle: upright stinging nettles, pairs of heart-shaped, saw-edged leaves.
- Chanterelles: golden funnel-shaped mushrooms with ridges under their wavy caps (solid: no cards, Layered Lit).
- Cloudberries: a low patch of round, lobed leaves with amber berries made of beads.
- Wild Barley: straw stalks, grass blades, drooping bearded ears.
- Wild Turnips: purple-shouldered white roots at the ground under a rosette of lobed leaves.
- Ramsons (2026-11-23): wild garlic, broad glossy leaves and white star-flower umbels.
- Angelica: a tall purple-flushed stalk with ball-shaped green-white umbels over a rosette of divided leaves.
- Lingonberries: a low mat of small glossy dark leaves with clusters of red berries.
- Bog Myrtle: a knee-high shrub of reddish twigs, narrow grey-green leaves and brown catkins.

One 1024 texture set each: the sprites in its bottom three quarters (their alpha is the leaf shape), the solid parts'
own baked islands in the top quarter. Albedo (with alpha), Normal (veins, ridges) and ORM; Emission for Huldra Silk;
Height and BlendMask only for the Chanterelles. Writes Assets/_Project/Art/Gathering/<Asset>/, a preview render to the
path given, and Plants.blend beside this script.

Run: blender -b --factory-startup --python Tools/Blender/plant_nodes.py -- <project root> <preview.png> [asset ...]
(assets: bush, herb, flax, silk, nettle, chanterelle, cloudberry, barley, turnip, ramsons, angelica,
lingonberry, myrtle; all when none are named).
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prop_bake import *  # noqa: E402,F401,F403  (helpers, materials, bake)
from prop_bake import ROOT, args  # noqa: E402

PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "plant_preview.png")
WANTED = set(args[2:]) or {"bush", "herb", "flax", "silk", "nettle", "cloudberry", "barley"}

UP = Vector((0, 0, 1))
SPRITES = os.path.join(ROOT, "Tools", "LocalSpawn", "Gathering", "Sprites")
# Sheet cells showing a generated sprite -> the part of the sprite that isn't transparent (x0, y0, x1, y1 in 0-1 of the
# sprite). Cards show only that part: alpha-tested pixels still cost shading, and the square sprites are only 24% leaf
# (40% of their opaque bounding box), so a card cropped to the box draws 42% fewer wasted pixels.
IMAGE_CELLS = {}


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


# ----- Building blocks ----------------------------------------------------------------------------------------------

class Kit:
    """One mesh being built: grain UVs (what the procedural surfaces follow), per-part variation (sh_var) and the wind
    the game's Tree Wind shader reads: "wind" = (sway, flex), "phase" = (phase, flutter). Sway (0 at the ground, 1 at
    the top) is filled in from the height at the end; flex (0 at a stem's foot, 1 at its tip), phase (per stem) and
    flutter (0 at a leaf's base, 1 at its tip) are set as parts are made. Cards (sh_card = 1) are leaf cards: their
    "atlas" UVs point into the sprite sheet and they aren't baked themselves."""

    def __init__(self, cards=False):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("grain")
        self.wind = self.bm.loops.layers.uv.new("wind")
        self.phase = self.bm.loops.layers.uv.new("phase")
        self.atlas = self.bm.loops.layers.uv.new("atlas") if cards else None
        self.var = self.bm.faces.layers.float.new("sh_var")
        self.card_flag = self.bm.faces.layers.int.new("sh_card")

    def wind_on(self, faces, flex, phase, flutter=None):
        """flex and flutter: a number, or a function of the vertex position."""
        for f in faces:
            for loop in f.loops:
                p = loop.vert.co
                loop[self.wind].uv = (0.0, flex(p) if callable(flex) else flex)
                loop[self.phase].uv = (phase, flutter(p) if callable(flutter) else (flutter or 0.0))

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

    def stem(self, points, radius, tip_radius, material, var, phase, sides=5, flex0=0.0, flex1=1.0):
        before = set(self.bm.faces)
        radii = [radius + (tip_radius - radius) * i / (len(points) - 1) for i in range(len(points))]
        tube(self.bm, points, radii, sides, self.uv, wood_cap=True, material=material, cap_material=material, tile=0.25)
        new = [f for f in self.bm.faces if f not in before]
        base, tip = points[0], points[-1]
        span = max((tip - base).length, 1e-4)
        for f in new:
            f[self.var] = var
            f.smooth = True
        self.wind_on(new, lambda p: flex0 + (flex1 - flex0) * min(1.0, (p - base).length / span), phase)
        return new

    def blob(self, centre, radius, material, var, phase, flex=0.0, squash=(1, 1, 1), subdivisions=1, lumpy=0.0, seed=0.0):
        result = bmesh.ops.create_icosphere(self.bm, subdivisions=subdivisions, radius=1.0)
        faces = {f for v in result["verts"] for f in v.link_faces}
        for v in result["verts"]:
            d = v.co.normalized()
            r = radius * (1.0 + lumpy * noise.noise(d * 2.0 + Vector((seed, seed, seed))))
            v.co = centre + Vector((d.x * squash[0], d.y * squash[1], d.z * squash[2])) * r
        for f in faces:
            f.material_index = material
            f[self.var] = var
            f.smooth = True
            for loop in f.loops:
                d = (loop.vert.co - centre).normalized()
                loop[self.uv].uv = (0.5 + math.atan2(d.y, d.x) / (2 * math.pi), (d.z + 1) / 2)
        self.wind_on(faces, flex, phase)
        return faces

    def lathe(self, centre, profile, segments, material, var, wobble=0.0, seed=0.0, axis=UP):
        """A turned shape: profile [(radius, height)] from bottom to top, U round, V along the profile. Rigid."""
        turn = UP.rotation_difference(axis).to_matrix().to_4x4()
        rings = []
        for j, (r, h) in enumerate(profile):
            ring = []
            for k in range(segments):
                a = 2 * math.pi * k / segments
                rr = r * (1.0 + wobble * noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, seed + j * 0.1))))
                ring.append(self.bm.verts.new(centre + turn @ Vector((math.cos(a) * rr, math.sin(a) * rr, h))))
            rings.append(ring)
        faces = []
        for j in range(len(profile) - 1):
            for k in range(segments):
                k2 = (k + 1) % segments
                uvs = [(k / segments, j / (len(profile) - 1)), (k / segments, (j + 1) / (len(profile) - 1)),
                       ((k + 1) / segments, (j + 1) / (len(profile) - 1)), ((k + 1) / segments, j / (len(profile) - 1))]
                f = self.face((rings[j][k], rings[j + 1][k], rings[j + 1][k2], rings[j][k2]), uvs, material, var)
                if f:
                    f.smooth = True
                    faces.append(f)
        self.wind_on(faces, 0.0, 0.0)
        return rings

    def card(self, base, direction, up, length, width, rect, phase, bend=0.12, droop=0.0, flex=1.0):
        """A leaf card: a quad bent once along its length, base at base, pointing along direction, facing up; its atlas
        UVs cover rect (u0, v0, u1, v1) of the sprite sheet (the sprite's foot at v0). Two-sided in the game."""
        direction = direction.normalized()
        side = direction.cross(up).normalized()
        normal = side.cross(direction).normalized()
        crop = IMAGE_CELLS.get(tuple(rect))
        if crop is not None:
            # The sprite is square with its stalk at the bottom middle: the card covers only its opaque box, at the same
            # size in the world and with the stalk where it was.
            x0, y0, x1, y1 = crop
            base = base + direction * length * y0 + side * length * ((x0 + x1) * 0.5 - 0.5)
            width = length * (x1 - x0)
            length = length * (y1 - y0)
            u0, v0, u1, v1 = rect
            rect = (u0 + (u1 - u0) * x0, v0 + (v1 - v0) * y0, u0 + (u1 - u0) * x1, v0 + (v1 - v0) * y1)
        rows = []
        for j, t in enumerate((0.0, 0.5, 1.0)):
            centre = base + direction * length * t - normal * bend * length * t * t - UP * droop * length * t * t
            rows.append([self.bm.verts.new(centre - side * width * 0.5), self.bm.verts.new(centre + side * width * 0.5)])
        u0, v0, u1, v1 = rect
        faces = []
        for j in range(2):
            ta, tb = j * 0.5, (j + 1) * 0.5
            verts = (rows[j][0], rows[j][1], rows[j + 1][1], rows[j + 1][0])
            f = self.bm.faces.new(verts)
            f.normal_update()
            if f.normal.dot(normal) < 0:
                f.normal_flip()
            f[self.card_flag] = 1
            f.smooth = True
            for loop in f.loops:
                v = loop.vert
                across = 0.0 if v in (rows[0][0], rows[1][0], rows[2][0]) else 1.0
                along = next(t for (t, row) in zip((0.0, 0.5, 1.0), rows) if v in row)
                loop[self.atlas].uv = (u0 + (u1 - u0) * across, v0 + (v1 - v0) * along)
                loop[self.wind].uv = (0.0, flex)
                loop[self.phase].uv = (phase, along)
            faces.append(f)
        return faces

    def object(self, name, coll, materials, height):
        """The mesh, with sway filled in from each vertex's height over the plant's."""
        for f in self.bm.faces:
            for loop in f.loops:
                w = loop[self.wind].uv
                loop[self.wind].uv = (max(0.0, min(1.0, loop.vert.co.z / max(height, 0.05))), w.y)
        return mesh_object(name, self.bm, coll, materials)


def sprite_material(name, path, fallback):
    """A generated sprite (transparent PNG) for the sheet: its colour where it is opaque (a dark plant colour behind,
    so mipmaps don't get light halos), its alpha as the leaf's shape (labelled "SH Solid": the coverage bake), its
    shading as relief (the normal map)."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-900, 0)
    tex = node(tree, "ShaderNodeTexImage", (-650, 0))
    tex.image = image(path, 'sRGB')
    tex.interpolation = 'Cubic'
    tex.extension = 'CLIP'
    links.new(uv.outputs["UV"], tex.inputs["Vector"])
    colour = mix(tree, (-300, 100), rgb(tree, (-500, 250), fallback), tex.outputs["Color"], tex.outputs["Alpha"])
    links.new(colour, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.65
    solid = tree.nodes.new("NodeReroute")
    links.new(tex.outputs["Alpha"], solid.inputs[0])
    solid.label = "SH Solid"
    grey = node(tree, "ShaderNodeRGBToBW", (-300, -200))
    links.new(tex.outputs["Color"], grey.inputs["Color"])
    bump = node(tree, "ShaderNodeBump", (0, -200), Strength=0.35, Distance=0.002)
    links.new(grey.outputs["Val"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def opaque_box(img, threshold=0.16, pad=0.015):
    """The box round a sprite's opaque pixels (alpha over threshold), padded, as (x0, y0, x1, y1) in 0-1, y up."""
    w, h = img.size
    px = numpy.empty(w * h * 4, dtype=numpy.float32)
    img.pixels.foreach_get(px)
    alpha = px.reshape(h, w, 4)[:, :, 3] > threshold
    rows, cols = numpy.nonzero(alpha.any(axis=1))[0], numpy.nonzero(alpha.any(axis=0))[0]
    if len(rows) == 0:
        return (0.0, 0.0, 1.0, 1.0)
    return (max(0.0, cols[0] / w - pad), max(0.0, rows[0] / h - pad),
            min(1.0, (cols[-1] + 1) / w + pad), min(1.0, (rows[-1] + 1) / h + pad))


def sprite_set(asset, names, fallback=(0.08, 0.13, 0.03)):
    """The generated sprites for an asset, as materials, or None when any is missing (the drawn leaves are used)."""
    paths = [os.path.join(SPRITES, asset, f"{n}.png") for n in names]
    if not all(os.path.exists(p) for p in paths):
        print("SPRITES missing for", asset, "- drawing leaves instead")
        return None
    return [sprite_material(f"{asset} Sprite {n} (bake)", p, fallback) for n, p in zip(names, paths)]


class Sheet:
    """The sprite sheet the leaf cards show: flat drawings of leaves (and flower heads, grass tufts, ears of barley),
    one per cell of a grid in region (u0, v0, u1, v1) of the texture, built as flat geometry lying on the texture
    itself (its XY is its atlas UV), baked with the plant's own materials. Each cell's foot is at its bottom."""

    def __init__(self, cols, rows, region=(0.0, 0.0, 1.0, 0.75)):
        self.kit = Kit(cards=False)
        self.cols, self.rows, self.region = cols, rows, region

    def rect(self, index, pad=0.01):
        u0, v0, u1, v1 = self.region
        cw, ch = (u1 - u0) / self.cols, (v1 - v0) / self.rows
        c, r = index % self.cols, index // self.cols
        return (u0 + c * cw + pad, v0 + r * ch + pad, u0 + (c + 1) * cw - pad, v0 + (r + 1) * ch - pad)

    def to_sheet(self, rect, x, y):
        u0, v0, u1, v1 = rect
        return Vector((u0 + (u1 - u0) * x, v0 + (v1 - v0) * y, 0.0))

    def leaf(self, rect, base, angle, length, width, outline, material, var, serrate=0.0):
        """A leaf drawn in the cell: base (x, y in 0-1), pointing at angle (radians from straight up), length and width
        as fractions of the cell. Its grain UVs are U across, V from stalk to tip (the veins follow them)."""
        d = Vector((math.sin(angle), math.cos(angle), 0.0))
        side = Vector((d.y, -d.x, 0.0))
        b = Vector((base[0], base[1], 0.0))
        verts, uvs = [], []
        half = []
        for sign in (-1, 1):
            row = []
            for i, (across, along) in enumerate(outline):
                if serrate and 0 < i < len(outline) - 1:
                    across *= 1.0 + serrate * (1 if i % 2 else -1)
                p = b + d * along * length + side * across * sign * width
                row.append((p, (0.5 + across * sign, along)))
            half.append(row)
        mid = [(b + d * along * length, (0.5, along)) for _, along in outline]
        for row in half:
            for i in range(len(outline) - 1):
                pts = [row[i], row[i + 1], mid[i + 1], mid[i]]
                if outline[i][0] == 0 and outline[i + 1][0] == 0:
                    continue
                if outline[i][0] == 0:
                    pts = [row[i + 1], mid[i + 1], mid[i]]
                elif outline[i + 1][0] == 0:
                    pts = [row[i], mid[i + 1], mid[i]]
                vs = [self.kit.bm.verts.new(self.to_sheet(rect, p.x, p.y)) for p, _ in pts]
                f = self.kit.face(vs, [g for _, g in pts], material, var, facing=UP)

    def line(self, rect, points, width, material, var):
        """A thin stalk or twig drawn in the cell along points (x, y)."""
        for a, b in zip(points, points[1:]):
            a, b = Vector((a[0], a[1], 0)), Vector((b[0], b[1], 0))
            side = Vector(((b - a).y, -(b - a).x, 0)).normalized() * width * 0.5
            vs = [self.kit.bm.verts.new(self.to_sheet(rect, p.x, p.y)) for p in (a - side, a + side, b + side, b - side)]
            self.kit.face(vs, [(0, 0), (1, 0), (1, 1), (0, 1)], material, var, facing=UP)

    def disc(self, rect, centre, radius, petals, material, var, rng, petal_outline=None):
        """A flower seen from above: petals round a centre."""
        for k in range(petals):
            a = 2 * math.pi * k / petals + rng.uniform(-0.15, 0.15)
            self.leaf(rect, centre, a, radius, radius * 0.75, petal_outline or PETAL, material, var)

    def image(self, index, material, sprite=None):
        """Cell index shows a generated sprite: one quad over the cell, its grain UVs the image's."""
        rect = self.rect(index)
        u0, v0, u1, v1 = rect
        vs = [self.kit.bm.verts.new(Vector(p)) for p in ((u0, v0, 0), (u1, v0, 0), (u1, v1, 0), (u0, v1, 0))]
        self.kit.face(vs, [(0, 0), (1, 0), (1, 1), (0, 1)], material, 0.5, facing=UP)
        picture = next((n.image for n in sprite.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image), None) if sprite else None
        IMAGE_CELLS[tuple(rect)] = opaque_box(picture) if picture else (0.0, 0.0, 1.0, 1.0)

    def object(self, name, coll, materials):
        obj = mesh_object(name, self.kit.bm, coll, materials)
        mesh = obj.data
        atlas = mesh.uv_layers.new(name="atlas")
        for loop in mesh.loops:
            co = mesh.vertices[loop.vertex_index].co
            atlas.data[loop.index].uv = (co.x, co.y)
        mesh.uv_layers.active = atlas
        atlas.active_render = True
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


def facing_out(p, centre, rng, lift=0.5):
    """A leaf's direction and the way its face turns: out from the plant's middle and up, a little at random."""
    out = p - centre
    out.z *= 0.5
    out = out.normalized() if out.length > 1e-5 else Vector((1, 0, 0))
    direction = (out + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), rng.uniform(0.0, lift)))).normalized()
    up = (UP * 0.8 + out * 0.4 + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 0))).normalized()
    return direction, up


# ----- Outlines (half of each, across and along) --------------------------------------------------------------------

OVATE = [(0.0, 0.0), (0.32, 0.22), (0.38, 0.55), (0.2, 0.85), (0.0, 1.0)]
HEART = [(0.0, 0.0), (0.42, 0.12), (0.45, 0.4), (0.3, 0.7), (0.12, 0.9), (0.0, 1.0)]
NARROW = [(0.0, 0.0), (0.25, 0.3), (0.2, 0.75), (0.0, 1.0)]
GRASS = [(0.0, 0.0), (0.5, 0.05), (0.45, 0.6), (0.0, 1.0)]
PETAL = [(0.0, 0.0), (0.28, 0.25), (0.42, 0.7), (0.3, 0.95), (0.0, 1.0)]
FEATHER = [(0.0, 0.0), (0.3, 0.1), (0.1, 0.18), (0.32, 0.3), (0.1, 0.4), (0.3, 0.52), (0.1, 0.62), (0.26, 0.74),
           (0.08, 0.85), (0.0, 1.0)]
LOBED = [(0.0, 0.0), (0.4, 0.15), (0.25, 0.3), (0.45, 0.45), (0.25, 0.6), (0.38, 0.75), (0.0, 1.0)]

GREEN = [(0.0, (0.07, 0.13, 0.03)), (0.5, (0.11, 0.19, 0.04)), (0.9, (0.16, 0.22, 0.05)), (1.0, (0.3, 0.28, 0.07))]
DARK_GREEN = [(0.0, (0.04, 0.08, 0.02)), (0.5, (0.065, 0.12, 0.03)), (1.0, (0.1, 0.16, 0.04))]
GREY_GREEN = [(0.0, (0.09, 0.14, 0.05)), (0.6, (0.14, 0.19, 0.08)), (1.0, (0.2, 0.22, 0.1))]
HAVEN_GREEN = [(0.0, (0.09, 0.15, 0.03)), (0.5, (0.14, 0.21, 0.04)), (0.9, (0.2, 0.25, 0.05)), (1.0, (0.32, 0.29, 0.07))]

# The sheet: cards in the bottom three quarters, everything solid (stems, berries, roots) baked in the top quarter.
SOLID_REGION = (0.0, 0.76, 1.0, 1.0)


def plant(name, height, solids, cards, sheet, coll, materials):
    """A plant's solid parts (with their own atlas islands) and its cards (pointing into the sheet), as the dict
    prop_bake.make_asset takes."""
    low = solids.object(name, coll, materials, height)
    card_obj = cards.object(f"{name} Cards", coll, materials, height) if cards is not None else None
    return low, card_obj


def plant_asset(variants, sheet_obj):
    return {"lows": [v[0] for v in variants], "cards": [v[1] for v in variants], "sheet": sheet_obj}


# ----- Plants -------------------------------------------------------------------------------------------------------

def berry_bush(coll):
    """Wild currant: arching woody stems, a crown of leaf-spray cards and strings of red berries on its top."""
    bark = bark_material("Currant Bark (bake)", tint=(0.6, 0.52, 0.45))
    leaf = varied("Currant Leaf (bake)", HAVEN_GREEN, veins=0.6, midrib=0.8, rim_dark=0.15, roughness=0.7)
    twig = varied("Currant Twig (bake)", [(0.0, (0.2, 0.15, 0.1)), (1.0, (0.28, 0.2, 0.12))], roughness=0.8)
    berry = varied("Currant (bake)", [(0.0, (0.5, 0.025, 0.02)), (0.6, (0.68, 0.04, 0.025)), (1.0, (0.8, 0.1, 0.04))],
                   roughness=0.25, sheen=0.6)
    materials = (bark, leaf, twig, berry)
    sheet = Sheet(2, 2)
    sprites = sprite_set("Berry Bush", ['spray_a', 'spray_b', 'spray_c', 'spray_d'], (0.08, 0.13, 0.03))
    if sprites:
        materials = materials + tuple(sprites)
        for cell, _ in enumerate(sprites):
            sheet.image(cell, len(materials) - len(sprites) + cell, sprites[cell])
    else:
        rng = random.Random(5)
        for cell in range(4):
            # A spray: a twig with five to seven leaves along it, the youngest at the tip.
            rect = sheet.rect(cell)
            sheet.line(rect, [(0.5, 0.0), (0.5 + rng.uniform(-0.05, 0.05), 0.55), (0.5, 0.95)], 0.025, 2, rng.random())
            for k in range(rng.randint(5, 7)):
                t = 0.12 + 0.8 * k / 6
                side = -1 if k % 2 else 1
                sheet.leaf(rect, (0.5, t * 0.9), side * rng.uniform(0.5, 1.0), rng.uniform(0.32, 0.42) * (1.1 - t * 0.4),
                           0.85, HEART, 1, min(0.95, rng.gauss(0.45 + 0.1 * cell, 0.15)), serrate=0.12)
            sheet.leaf(rect, (0.5, 0.88), rng.uniform(-0.2, 0.2), 0.12, 0.8, HEART, 1, 0.6, serrate=0.12)
    variants = []
    for key, seed, spread, height, stems in (("A", 5, 0.62, 0.9, 10), ("B", 11, 0.7, 0.82, 9)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        tips = []
        for s in range(stems):
            turn = 2 * math.pi * (s + rng.uniform(-0.3, 0.3)) / stems
            out = Vector((math.cos(turn), math.sin(turn), 0))
            reach, top = spread * rng.uniform(0.45, 0.95), height * rng.uniform(0.7, 1.0)
            phase = rng.random()
            points = []
            for i in range(9):
                t = i / 8
                p = out * rng.uniform(0.0, 0.05) + out * reach * t ** 1.3
                p.z = top * math.sin(t * math.pi * 0.62) / math.sin(math.pi * 0.62) * (1 - 0.25 * t * t)
                points.append(p)
            solids.stem(points, 0.014, 0.004, 0, rng.random(), phase, sides=5)
            for i in range(3, 9):
                tips.append((points[i], phase))
            for b in range(rng.randint(2, 4)):
                at = rng.randint(3, 7)
                d = (out * 0.5 + Vector((-out.y, out.x, 0)) * rng.choice((-0.7, 0.7)) + UP * rng.uniform(0.1, 0.6)).normalized()
                length = rng.uniform(0.15, 0.3)
                branch = bent_path(points[at], d, length, 4, rng.uniform(-0.2, 0.2), rng, droop=length * 0.25)
                solids.stem(branch, 0.007, 0.003, 0, rng.random(), phase, sides=4, flex0=0.4)
                tips.extend((p, phase) for p in branch[1:])
        centre = Vector((0, 0, height * 0.3))
        # The crown: leaf sprays on the twigs and over the dome the stems make.
        dome = []
        for _ in range(170):
            a = rng.uniform(0, 2 * math.pi)
            up_share = rng.random() ** 0.7
            elevation = math.asin(up_share)
            r = spread * 0.9 * (1.0 + 0.2 * noise.noise(Vector((math.cos(a) * 1.4, math.sin(a) * 1.4, up_share * 1.5 + seed))))
            dome.append((Vector((math.cos(a) * math.cos(elevation) * r, math.sin(a) * math.cos(elevation) * r,
                                 0.12 + (height - 0.12) * math.sin(elevation))), rng.random()))
        for p, phase in tips + dome:
            direction, up = facing_out(p, centre, rng)
            length = rng.uniform(0.22, 0.3)
            cards.card(p - direction * length * 0.25, direction, up, length, length * 0.95, sheet.rect(rng.randrange(4)),
                       phase, bend=0.15, droop=0.1)
        # Strings of berries on the top of the crown, where the camera sees them.
        tops = [p for p, _ in dome if p.z > height * 0.5]
        rng.shuffle(tops)
        # Strings of currants on the top of the crown: 26 strings of 3-5 (38 of 4-6 made the berries 3,800 of the bush's
        # 7,400 triangles, for dots a few pixels wide from the game's camera).
        for p in tops[:26]:
            hang = Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), -0.7)).normalized()
            at = p + (p - centre).normalized() * 0.04
            value = rng.random()
            for k in range(rng.randint(3, 5)):
                solids.blob(at + hang * (0.015 + k * 0.03), rng.uniform(0.018, 0.022), 3, value, 0.0, flex=0.8,
                            subdivisions=1)
        variants.append(plant(f"Berry Bush {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Berry Bush Sheet", coll, materials))


def healing_herb(coll):
    """Yarrow: feathery leaves round the foot, stems with flat heads of small white flowers (cards seen from above)."""
    leaf = varied("Yarrow Leaf (bake)", GREY_GREEN, midrib=0.6, roughness=0.75)
    stalk = varied("Yarrow Stem (bake)", [(0.0, (0.12, 0.16, 0.06)), (1.0, (0.18, 0.2, 0.08))], roughness=0.7)
    bloom = varied("Yarrow Flower (bake)", [(0.0, (0.72, 0.7, 0.62)), (0.7, (0.85, 0.83, 0.76)), (1.0, (0.85, 0.7, 0.72))],
                   roughness=0.8)
    materials = (leaf, stalk, bloom)
    sheet = Sheet(2, 2)
    sprites = sprite_set("Healing Herb", ['leaf_a', 'leaf_b', 'flower_a', 'flower_b'], (0.08, 0.13, 0.03))
    if sprites:
        materials = materials + tuple(sprites)
        for cell, _ in enumerate(sprites):
            sheet.image(cell, len(materials) - len(sprites) + cell, sprites[cell])
    else:
        rng = random.Random(1)
        for cell in (0, 1):
            sheet.leaf(sheet.rect(cell), (0.5, 0.02), 0.0, 0.95, 0.75, FEATHER, 0, rng.random())
        for cell in (2, 3):
            rect = sheet.rect(cell)
            for _ in range(26):
                a, r = rng.uniform(0, 6.28), 0.4 * math.sqrt(rng.random())
                sheet.disc(rect, (0.5 + math.cos(a) * r, 0.5 + math.sin(a) * r), 0.07, 5, 2, rng.random(), rng)
    variants = []
    for key, seed in (("A", 601), ("B", 602)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        for k in range(14):
            a = 2 * math.pi * k / 14 + rng.uniform(-0.2, 0.2)
            out = Vector((math.cos(a), math.sin(a), rng.uniform(0.3, 0.8))).normalized()
            cards.card(Vector((0, 0, 0.02)), out, UP, rng.uniform(0.18, 0.26), 0.13, sheet.rect(rng.randrange(2)),
                       rng.random(), bend=0.25, droop=0.2, flex=0.6)
        height = 0.55
        for base, lean in tuft(rng, 9, 0.1):
            points = ring_points(base, lean, rng.uniform(0.38, 0.55), 5, bend=0.05, rng=rng)
            phase = rng.random()
            solids.stem(points, 0.006, 0.004, 1, rng.random(), phase, sides=4)
            for i in (2, 3):
                d = (lean + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.3))).normalized()
                cards.card(points[i], d, UP, rng.uniform(0.09, 0.13), 0.06, sheet.rect(rng.randrange(2)), phase, bend=0.2)
            # The flower head: a card lying flat on the stem's top.
            top = points[-1] + UP * 0.005
            size = rng.uniform(0.1, 0.13)
            cards.card(top - Vector((0, size * 0.5, 0)), Vector((0, 1, 0.02)), UP, size, size,
                       sheet.rect(2 + rng.randrange(2)), phase, bend=-0.05)
        variants.append(plant(f"Healing Herb {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Healing Herb Sheet", coll, materials))


def flax(coll):
    """Flax: thin stems, narrow leaves, sky-blue flowers (cards facing up and out) and seed bolls."""
    leaf = varied("Flax Leaf (bake)", [(0.0, (0.1, 0.17, 0.08)), (1.0, (0.16, 0.24, 0.1))], midrib=0.4)
    stalk = varied("Flax Stem (bake)", [(0.0, (0.14, 0.2, 0.08)), (1.0, (0.22, 0.26, 0.1))], roughness=0.7)
    bloom = varied("Flax Flower (bake)", [(0.0, (0.18, 0.32, 0.75)), (0.7, (0.3, 0.45, 0.88)), (1.0, (0.45, 0.55, 0.92))],
                   roughness=0.6, veins=0.5)
    boll = varied("Flax Boll (bake)", [(0.0, (0.35, 0.33, 0.12)), (1.0, (0.55, 0.5, 0.22))], roughness=0.5)
    materials = (leaf, stalk, bloom, boll)
    sheet = Sheet(2, 2)
    sprites = sprite_set("Flax", ['leaves_a', 'leaves_b', 'flower_a', 'flower_b'], (0.08, 0.13, 0.03))
    if sprites:
        materials = materials + tuple(sprites)
        for cell, _ in enumerate(sprites):
            sheet.image(cell, len(materials) - len(sprites) + cell, sprites[cell])
    else:
        rng = random.Random(2)
        for cell in (0, 1):
            rect = sheet.rect(cell)
            for j in range(4):
                sheet.leaf(rect, (0.2 + 0.2 * j, 0.02), rng.uniform(-0.25, 0.25), rng.uniform(0.7, 0.95), 0.12, NARROW, 0,
                           rng.random())
        for cell in (2, 3):
            sheet.disc(sheet.rect(cell), (0.5, 0.5), 0.46, 5, 2, rng.random(), rng)
    variants = []
    for key, seed in (("A", 611), ("B", 612)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.72
        for base, lean in tuft(rng, 55, 0.3):
            points = ring_points(base, lean, rng.uniform(0.5, 0.72), 5, bend=rng.uniform(0.0, 0.08), rng=rng)
            phase = rng.random()
            solids.stem(points, 0.004, 0.0025, 1, rng.random(), phase, sides=3)
            if rng.random() < 0.6:
                d = (lean + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0))).normalized()
                cards.card(points[1], d, UP, rng.uniform(0.12, 0.18), 0.08, sheet.rect(rng.randrange(2)), phase, bend=0.1)
            top, roll = points[-1], rng.random()
            if roll < 0.6:
                facing = (lean + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), 0.6))).normalized()
                size = rng.uniform(0.045, 0.055)
                side = facing.orthogonal().normalized()
                cards.card(top - side * size * 0.5, side, facing, size, size, sheet.rect(2 + rng.randrange(2)), phase,
                           bend=-0.15)
            elif roll < 0.85:
                solids.blob(top + UP * 0.006, 0.0075, 3, rng.random(), phase, flex=1.0, subdivisions=0)
        variants.append(plant(f"Flax {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Flax Sheet", coll, materials))


def huldra_silk(coll):
    """Cotton grass: a tussock of grass-blade cards, thin stems with silky white tufts that glow a little at night."""
    grass = varied("Cotton Grass (bake)", [(0.0, (0.08, 0.13, 0.05)), (0.7, (0.14, 0.2, 0.07)), (1.0, (0.35, 0.3, 0.12))],
                   midrib=0.3)
    stalk = varied("Cotton Grass Stem (bake)", [(0.0, (0.15, 0.18, 0.08)), (1.0, (0.25, 0.25, 0.1))], roughness=0.7)
    silk = varied("Huldra Silk (bake)", [(0.0, (0.78, 0.79, 0.76)), (1.0, (0.9, 0.9, 0.86))], roughness=0.95, glow=0.12,
                  strands=True)
    materials = (grass, stalk, silk)
    sheet = Sheet(2, 2)
    sprites = sprite_set("Huldra Silk", ['grass_a', 'grass_b', 'grass_c', 'grass_d'], (0.08, 0.13, 0.03))
    if sprites:
        materials = materials + tuple(sprites)
        for cell, _ in enumerate(sprites):
            sheet.image(cell, len(materials) - len(sprites) + cell, sprites[cell])
    else:
        rng = random.Random(3)
        for cell in range(4):
            rect = sheet.rect(cell)
            for j in range(6):
                sheet.leaf(rect, (0.25 + 0.1 * j, 0.0), rng.uniform(-0.4, 0.4), rng.uniform(0.6, 0.98), 0.08, GRASS, 0,
                           rng.random())
    variants = []
    for key, seed in (("A", 621), ("B", 622)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.62
        for base, lean in tuft(rng, 26, 0.32):
            d = (lean + Vector((rng.uniform(-0.4, 0.4), rng.uniform(-0.4, 0.4), 0))).normalized()
            up = Vector((d.y, -d.x, 0)).normalized() if abs(d.z) < 0.99 else Vector((1, 0, 0))
            length = rng.uniform(0.28, 0.42)
            cards.card(base, d, up, length, length * 0.6, sheet.rect(rng.randrange(4)), rng.random(), bend=0.2,
                       droop=rng.uniform(0.1, 0.3))
        for base, lean in tuft(rng, 22, 0.28):
            points = ring_points(base, lean, rng.uniform(0.4, 0.6), 5, bend=0.03, rng=rng)
            phase = rng.random()
            solids.stem(points, 0.003, 0.002, 1, rng.random(), phase, sides=3)
            # A silky head: a soft, slightly uneven ball with a smaller puff off one side (strong lumps on a coarse
            # sphere read as a faceted pebble, a white hexagon in game).
            top = points[-1] + UP * 0.025
            solids.blob(top, 0.024, 2, rng.random(), phase, flex=1.0, squash=(1.0, 1.0, 1.3),
                        subdivisions=2, lumpy=0.12, seed=rng.random() * 50)
            puff = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(0.2, 0.8))).normalized()
            solids.blob(top + puff * 0.016, 0.014, 2, rng.random(), phase, flex=1.0, subdivisions=1, lumpy=0.1,
                        seed=rng.random() * 50)
        variants.append(plant(f"Huldra Silk {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Huldra Silk Sheet", coll, materials))


def nettle(coll):
    """Stinging nettles: upright stems with pairs of heart-shaped, saw-edged leaves, each pair a quarter turn on."""
    leaf = varied("Nettle Leaf (bake)", DARK_GREEN, veins=0.7, midrib=0.8, rim_dark=0.1)
    stalk = varied("Nettle Stem (bake)", [(0.0, (0.1, 0.13, 0.05)), (1.0, (0.18, 0.15, 0.07))], roughness=0.75)
    materials = (leaf, stalk)
    sheet = Sheet(2, 2)
    sprites = sprite_set("Nettle", ['leaf_a', 'leaf_b', 'leaf_c', 'leaf_d'], (0.08, 0.13, 0.03))
    if sprites:
        materials = materials + tuple(sprites)
        for cell, _ in enumerate(sprites):
            sheet.image(cell, len(materials) - len(sprites) + cell, sprites[cell])
    else:
        rng = random.Random(4)
        for cell in range(4):
            sheet.leaf(sheet.rect(cell), (0.5, 0.02), 0.0, 0.96, 0.85, HEART, 0, rng.random(), serrate=0.2)
    variants = []
    for key, seed in (("A", 631), ("B", 632)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.85
        for base, lean in tuft(rng, 14, 0.3):
            points = ring_points(base, lean, rng.uniform(0.55, 0.85), 8, bend=0.04, rng=rng)
            phase = rng.random()
            solids.stem(points, 0.006, 0.003, 1, rng.random(), phase, sides=4)
            turn = rng.uniform(0, math.pi)
            for i in range(2, 9):
                for sign in (-1, 1):
                    a = turn + (i % 2) * math.pi / 2 + (0 if sign > 0 else math.pi)
                    d = Vector((math.cos(a), math.sin(a), 0.2 + 0.05 * i)).normalized()
                    size = 0.12 * (1.15 - i / 11)
                    cards.card(points[i], d, UP, size, size * 0.8, sheet.rect(rng.randrange(4)), phase, bend=0.3,
                               droop=0.15)
        variants.append(plant(f"Nettle {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Nettle Sheet", coll, materials))


def cloudberries(coll):
    """A low patch of round, lobed, wrinkled leaves with amber berries made of beads on short stems."""
    leaf = varied("Cloudberry Leaf (bake)", GREEN, veins=0.8, midrib=0.6, rim_dark=0.15)
    stalk = varied("Cloudberry Stem (bake)", [(0.0, (0.12, 0.13, 0.05)), (1.0, (0.2, 0.17, 0.08))], roughness=0.7)
    berry = varied("Cloudberry (bake)", [(0.0, (0.85, 0.42, 0.08)), (0.6, (0.92, 0.55, 0.12)), (1.0, (0.95, 0.68, 0.25))],
                   roughness=0.35, sheen=0.6)
    materials = (leaf, stalk, berry)
    sheet = Sheet(2, 2)
    sprites = sprite_set("Cloudberry", ['leaf_a', 'leaf_b', 'leaf_c', 'leaf_d'], (0.08, 0.13, 0.03))
    if sprites:
        materials = materials + tuple(sprites)
        for cell, _ in enumerate(sprites):
            sheet.image(cell, len(materials) - len(sprites) + cell, sprites[cell])
    else:
        rng = random.Random(6)
        for cell in range(4):
            sheet.leaf(sheet.rect(cell), (0.5, 0.02), 0.0, 0.96, 0.95, LOBED, 0, rng.random(), serrate=0.15)
    variants = []
    for key, seed in (("A", 641), ("B", 642)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.24
        for base, lean in tuft(rng, 22, 0.26):
            points = ring_points(base, lean, rng.uniform(0.08, 0.16), 2, rng=rng)
            phase = rng.random()
            solids.stem(points, 0.004, 0.003, 1, rng.random(), phase, sides=3)
            d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.25)).normalized()
            size = rng.uniform(0.09, 0.13)
            cards.card(points[-1] - d * size * 0.3, d, UP, size, size, sheet.rect(rng.randrange(4)), phase, bend=0.12)
        for base, lean in tuft(rng, 9, 0.22):
            top = base + UP * rng.uniform(0.16, 0.22) + lean * 0.02
            phase = rng.random()
            solids.stem([base, base.lerp(top, 0.5), top], 0.003, 0.0025, 1, rng.random(), phase, sides=3)
            value = rng.random()
            # One bumpy ball a berry (it was 14 beads, 2,500 of the patch's 3,100 triangles for a berry 2 cm across).
            solids.blob(top + UP * 0.02, 0.019, 2, value, phase, flex=1.0, squash=(1.0, 1.0, 0.9), subdivisions=2,
                        lumpy=0.22, seed=rng.random() * 50)
        variants.append(plant(f"Cloudberries {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Cloudberries Sheet", coll, materials))


def wild_barley(coll):
    """Barley: straw stalks, grass-blade cards, and bearded ears as crossed cards on the drooping tips."""
    straw = varied("Barley Straw (bake)", [(0.0, (0.48, 0.4, 0.17)), (0.6, (0.62, 0.52, 0.24)), (1.0, (0.55, 0.55, 0.25))],
                   roughness=0.6, midrib=0.3)
    grain = varied("Barley Grain (bake)", [(0.0, (0.58, 0.47, 0.2)), (1.0, (0.75, 0.62, 0.3))], roughness=0.55)
    materials = (straw, grain)
    sheet = Sheet(2, 2)
    sprites = sprite_set("Wild Barley", ['blade_a', 'blade_b', 'ear_a', 'ear_b'], (0.45, 0.38, 0.16))
    if sprites:
        materials = materials + tuple(sprites)
        for cell, _ in enumerate(sprites):
            sheet.image(cell, len(materials) - len(sprites) + cell, sprites[cell])
    else:
        rng = random.Random(7)
        for cell in (0, 1):
            rect = sheet.rect(cell)
            for j in range(3):
                sheet.leaf(rect, (0.3 + 0.2 * j, 0.0), rng.uniform(-0.3, 0.3), rng.uniform(0.7, 0.98), 0.1, GRASS, 0,
                           rng.random())
        for cell in (2, 3):
            # An ear: two rows of grains up a short stalk, each with a long awn.
            rect = sheet.rect(cell)
            sheet.line(rect, [(0.5, 0.0), (0.5, 0.55)], 0.03, 0, 0.5)
            for g in range(9):
                y = 0.08 + g * 0.055
                for sign in (-1, 1):
                    sheet.leaf(rect, (0.5 + sign * 0.03, y), sign * 0.35, 0.09, 0.6, OVATE, 1, rng.random())
                    sheet.line(rect, [(0.5 + sign * 0.05, y + 0.08), (0.5 + sign * 0.16, y + 0.45)], 0.008, 1, 0.7)
    variants = []
    for key, seed in (("A", 661), ("B", 662)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.85
        for base, lean in tuft(rng, 26, 0.18):
            points = ring_points(base, lean, rng.uniform(0.6, 0.85), 6, bend=rng.uniform(0.12, 0.3), rng=rng)
            phase = rng.random()
            solids.stem(points, 0.004, 0.0025, 0, rng.random(), phase, sides=3)
            for i in (1, 3):
                d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.5)).normalized()
                up = Vector((d.y, -d.x, 0)).normalized()
                cards.card(points[i], d, up, rng.uniform(0.18, 0.25), 0.05, sheet.rect(rng.randrange(2)), phase, droop=0.4)
            along = (points[-1] - points[-2]).normalized()
            side = along.cross(UP).normalized()
            rect = sheet.rect(2 + rng.randrange(2))
            for turn in (side, along.cross(side).normalized()):
                cards.card(points[-1] - along * 0.01, along, turn, 0.16, 0.09, rect, phase, bend=0.05)
        variants.append(plant(f"Wild Barley {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Wild Barley Sheet", coll, materials))


def wild_turnips(coll):
    """Wild turnips: purple-shouldered white roots showing at the ground, under a rosette of lobed leaf cards."""
    leaf = varied("Turnip Leaf (bake)", GREEN, veins=0.7, midrib=0.9)
    root = varied("Turnip Root (bake)", [(0.0, (0.82, 0.78, 0.72)), (1.0, (0.88, 0.85, 0.78))], roughness=0.5)
    shoulder = varied("Turnip Shoulder (bake)", [(0.0, (0.42, 0.12, 0.32)), (1.0, (0.55, 0.2, 0.4))], roughness=0.5)
    materials = (leaf, root, shoulder)
    sheet = Sheet(2, 2)
    rng = random.Random(8)
    for cell in range(4):
        sheet.leaf(sheet.rect(cell), (0.5, 0.02), 0.0, 0.96, 0.8, LOBED, 0, rng.random())
    variants = []
    for key, seed in (("A", 671), ("B", 672)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.35
        for k in range(rng.randint(2, 3)):
            a, r = rng.uniform(0, 6.28), 0.08 * k
            at = Vector((math.cos(a) * r, math.sin(a) * r, -0.05))
            size = rng.uniform(0.8, 1.15)
            white = [(0.004, -0.06), (0.025, -0.04), (0.05, 0.0), (0.06, 0.03)]
            purple = [(0.06, 0.03), (0.058, 0.055), (0.045, 0.075), (0.02, 0.088), (0.006, 0.092)]
            solids.lathe(at, [(rr * size, h * size) for rr, h in white], 12, 1, rng.random(), wobble=0.06,
                         seed=rng.random() * 9)
            solids.lathe(at, [(rr * size, h * size) for rr, h in purple], 12, 2, rng.random(), wobble=0.06,
                         seed=rng.random() * 9)
            crown = at + UP * 0.09 * size
            phase = rng.random()
            for j in range(rng.randint(6, 9)):
                b = 2 * math.pi * j / 8 + rng.uniform(-0.3, 0.3)
                d = Vector((math.cos(b), math.sin(b), rng.uniform(0.9, 1.6))).normalized()
                length = rng.uniform(0.22, 0.32)
                cards.card(crown, d, UP, length, length * 0.5, sheet.rect(rng.randrange(4)), phase, bend=0.3, droop=0.25)
        variants.append(plant(f"Wild Turnips {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Wild Turnips Sheet", coll, materials))


def chanterelles(coll):
    """Golden funnel-shaped mushrooms with ridges under their wavy caps: solid, no cards (they don't sway)."""
    cap = gills_material("Chanterelle (bake)", [(0.0, (0.85, 0.42, 0.03)), (0.6, (0.93, 0.55, 0.05)), (1.0, (0.96, 0.64, 0.1))])
    lows = []
    for key, seed in (("A", 651), ("B", 652)):
        rng = random.Random(seed)
        kit = Kit()
        for k in range(rng.randint(6, 8)):
            a, r = rng.uniform(0, 6.28), 0.22 * math.sqrt(rng.random())
            at = Vector((math.cos(a) * r, math.sin(a) * r, -0.01))
            size = rng.uniform(0.8, 1.5)
            tilt = Vector((rng.uniform(-0.25, 0.25), rng.uniform(-0.25, 0.25), 1.0)).normalized()
            profile = [(0.012, 0.0), (0.011, 0.02), (0.014, 0.04), (0.024, 0.055), (0.038, 0.065), (0.045, 0.07),
                       (0.042, 0.066), (0.03, 0.058), (0.018, 0.045), (0.012, 0.03)]
            kit.lathe(at, [(rr * size, h * size) for rr, h in profile], 16, 0, rng.random(), wobble=0.12,
                      seed=rng.random() * 30, axis=tilt)
        lows.append(kit.object(f"Chanterelles {key}", coll, (cap,), 0.12))
    return lows


def ramsons(coll):
    """Wild garlic (ramsons): a clump of broad, glossy, upright-arching leaves, and thin stems above them with round heads of
    white star-shaped flowers (cards facing up and out)."""
    leaf = varied("Ramsons Leaf (bake)", [(0.0, (0.06, 0.15, 0.03)), (0.5, (0.1, 0.24, 0.05)), (1.0, (0.16, 0.3, 0.07))],
                  midrib=0.7, roughness=0.35, sheen=0.5)
    stalk = varied("Ramsons Stem (bake)", [(0.0, (0.14, 0.22, 0.08)), (1.0, (0.2, 0.28, 0.1))], roughness=0.6)
    bloom = varied("Ramsons Flower (bake)", [(0.0, (0.8, 0.82, 0.78)), (0.8, (0.92, 0.93, 0.9)), (1.0, (0.85, 0.9, 0.75))],
                   roughness=0.7)
    materials = (leaf, stalk, bloom)
    sheet = Sheet(2, 2)
    rng = random.Random(9)
    for cell in (0, 1):
        sheet.leaf(sheet.rect(cell), (0.5, 0.02), 0.0, 0.96, 0.55, OVATE, 0, rng.random())
    for cell in (2, 3):
        # An umbel from above: a dozen small six-pointed stars round its middle.
        rect = sheet.rect(cell)
        for _ in range(12):
            a, r = rng.uniform(0, 6.28), 0.3 * math.sqrt(rng.random())
            sheet.disc(rect, (0.5 + math.cos(a) * r, 0.5 + math.sin(a) * r), 0.11, 6, 2, rng.random(), rng)
    variants = []
    for key, seed in (("A", 681), ("B", 682)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.42
        for base, lean in tuft(rng, 16, 0.22):
            # A broad leaf rising from the ground on a short stalk, arching out.
            d = (lean + Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), 0.9))).normalized()
            side = d.cross(UP)
            up = side.cross(d).normalized() if side.length > 1e-4 else Vector((1, 0, 0))
            length = rng.uniform(0.24, 0.32)
            cards.card(base, d, up, length, length * 0.42, sheet.rect(rng.randrange(2)), rng.random(), bend=0.2,
                       droop=0.22, flex=0.7)
        for base, lean in tuft(rng, 6, 0.16):
            points = ring_points(base, lean, rng.uniform(0.32, 0.42), 4, bend=0.02, rng=rng)
            phase = rng.random()
            solids.stem(points, 0.003, 0.0025, 1, rng.random(), phase, sides=3)
            top = points[-1] + UP * 0.004
            size = rng.uniform(0.07, 0.09)
            facing = (lean + UP).normalized()
            side = facing.orthogonal().normalized()
            cards.card(top - side * size * 0.5, side, facing, size, size, sheet.rect(2 + rng.randrange(2)), phase, bend=-0.1)
        variants.append(plant(f"Ramsons {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Ramsons Sheet", coll, materials))


def angelica(coll):
    """Angelica: a tall, hollow, purple-flushed stalk from a rosette of big divided leaves, branching at its top into round,
    ball-shaped umbels of tiny green-white flowers (cards round a ball, so they read as globes from any side)."""
    # Green all over: GREEN's last stop is a straw yellow that showed as pale wedges on the big leaflets.
    leaf = varied("Angelica Leaf (bake)", [(0.0, (0.06, 0.13, 0.03)), (0.6, (0.1, 0.19, 0.05)), (1.0, (0.14, 0.23, 0.06))],
                  veins=0.6, midrib=0.8, rim_dark=0.1)
    stalk = varied("Angelica Stalk (bake)", [(0.0, (0.26, 0.12, 0.16)), (0.5, (0.2, 0.24, 0.1)), (1.0, (0.24, 0.3, 0.12))],
                   roughness=0.5, sheen=0.4)
    bloom = varied("Angelica Umbel (bake)", [(0.0, (0.42, 0.5, 0.26)), (0.7, (0.6, 0.66, 0.42)), (1.0, (0.78, 0.8, 0.62))],
                   roughness=0.8)
    materials = (leaf, stalk, bloom)
    sheet = Sheet(2, 2)
    rng = random.Random(10)
    for cell in (0, 1):
        # A divided leaf: a stalk with three pairs of saw-edged leaflets and one at the tip.
        rect = sheet.rect(cell)
        sheet.line(rect, [(0.5, 0.0), (0.5, 0.9)], 0.03, 0, 0.3)
        for k, t in enumerate((0.3, 0.52, 0.74)):
            for sign in (-1, 1):
                sheet.leaf(rect, (0.5, t), sign * rng.uniform(0.9, 1.2), 0.3 - 0.04 * k, 0.45, OVATE, 0, rng.random(),
                           serrate=0.15)
        sheet.leaf(rect, (0.5, 0.86), 0.0, 0.14, 0.5, OVATE, 0, rng.random(), serrate=0.15)
    for cell in (2, 3):
        rect = sheet.rect(cell)
        for _ in range(40):
            a, r = rng.uniform(0, 6.28), 0.42 * math.sqrt(rng.random())
            sheet.disc(rect, (0.5 + math.cos(a) * r, 0.5 + math.sin(a) * r), 0.05, 5, 2, rng.random(), rng)
    variants = []
    for key, seed in (("A", 691), ("B", 692)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 1.35
        for k in range(9):
            a = 2 * math.pi * k / 9 + rng.uniform(-0.25, 0.25)
            out = Vector((math.cos(a), math.sin(a), rng.uniform(0.5, 1.0))).normalized()
            length = rng.uniform(0.38, 0.5)
            cards.card(Vector((0, 0, 0.03)), out, UP, length, length * 0.8, sheet.rect(rng.randrange(2)), rng.random(),
                       bend=0.2, droop=0.3, flex=0.5)
        for s in range(rng.randint(1, 2)):
            base = Vector((rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05), -0.02))
            lean = Vector((rng.uniform(-0.12, 0.12), rng.uniform(-0.12, 0.12), 1.0)).normalized()
            tall = rng.uniform(1.05, 1.3) * (1.0 if s == 0 else 0.8)
            points = ring_points(base, lean, tall, 8, bend=0.02, rng=rng)
            phase = rng.random()
            solids.stem(points, 0.016, 0.009, 1, rng.random(), phase, sides=6, flex0=0.0, flex1=0.6)
            # Spokes at the top, each holding a ball of flowers.
            top = points[-1]
            for b in range(rng.randint(4, 6)):
                ang = rng.uniform(0, 6.28)
                d = Vector((math.cos(ang) * 0.6, math.sin(ang) * 0.6, 1.0)).normalized()
                spoke = [top, top + d * 0.06, top + d * 0.12]
                solids.stem(spoke, 0.004, 0.003, 1, rng.random(), phase, sides=3, flex0=0.6, flex1=1.0)
                ball, radius = spoke[-1] + d * 0.045, rng.uniform(0.045, 0.06)
                for c in range(8):
                    n = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-0.2, 1))).normalized()
                    side = n.orthogonal().normalized()
                    cards.card(ball + n * radius * 0.8 - side * radius, side, n, radius * 2, radius * 2,
                               sheet.rect(2 + rng.randrange(2)), phase, bend=-0.15)
            for i in (3, 5):
                d = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.6)).normalized()
                length = rng.uniform(0.22, 0.3)
                cards.card(points[i], d, UP, length, length * 0.8, sheet.rect(rng.randrange(2)), phase, bend=0.2, droop=0.25)
        variants.append(plant(f"Angelica {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Angelica Sheet", coll, materials))


def lingonberries(coll):
    """Lingonberry: a low, dense mat of short woody stems with small glossy dark oval leaves, and clusters of bright red
    berries nodding at the stem tips."""
    leaf = varied("Lingonberry Leaf (bake)", DARK_GREEN, midrib=0.5, roughness=0.3, sheen=0.6)
    twig = varied("Lingonberry Twig (bake)", [(0.0, (0.2, 0.13, 0.08)), (1.0, (0.3, 0.2, 0.1))], roughness=0.8)
    berry = varied("Lingonberry (bake)", [(0.0, (0.55, 0.02, 0.03)), (0.6, (0.75, 0.05, 0.05)), (1.0, (0.88, 0.14, 0.08))],
                   roughness=0.2, sheen=0.7)
    materials = (leaf, twig, berry)
    sheet = Sheet(2, 2)
    rng = random.Random(11)
    for cell in range(4):
        # A shoot: a twig with small oval leaves all along it.
        rect = sheet.rect(cell)
        sheet.line(rect, [(0.5, 0.0), (0.5 + rng.uniform(-0.05, 0.05), 0.9)], 0.03, 1, rng.random())
        for k in range(8):
            t = 0.1 + 0.8 * k / 7
            side = -1 if k % 2 else 1
            sheet.leaf(rect, (0.5, t * 0.92), side * rng.uniform(0.6, 1.1), 0.22 * (1.1 - t * 0.3), 0.7, OVATE, 0,
                       min(0.95, rng.gauss(0.5, 0.15)))
    variants = []
    for key, seed in (("A", 701), ("B", 702)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        height = 0.26
        # Many short shoots (a lingonberry leaf is about a centimetre and a half: shoots of 6-10 cm read as a mat of them). A
        # berry's faces each bake to their own island (three vertices a triangle): the mat's many cards keep the whole within the
        # foliage budget (ModelBudgetTests), with eight clusters of three or four berries.
        for base, lean in tuft(rng, 180, 0.32):
            length = rng.uniform(0.06, 0.1)
            d = (lean + Vector((rng.uniform(-0.5, 0.5), rng.uniform(-0.5, 0.5), 0.4))).normalized()
            side = d.cross(UP)
            up = side.cross(d).normalized() if side.length > 1e-4 else Vector((1, 0, 0))
            cards.card(base, d, up, length * 1.3, length * 0.9, sheet.rect(rng.randrange(4)), rng.random(), bend=0.1,
                       droop=0.1, flex=0.6)
        for base, lean in tuft(rng, 8, 0.26):
            # Low, among the leaves (a lingonberry's clusters nod at its shoot tips, not on stalks above the mat).
            top = base + UP * rng.uniform(0.06, 0.1) + lean * 0.03
            phase = rng.random()
            solids.stem([base, base.lerp(top, 0.5), top], 0.003, 0.002, 1, rng.random(), phase, sides=3)
            hang = Vector((rng.uniform(-0.6, 0.6), rng.uniform(-0.6, 0.6), -0.5)).normalized()
            # Berries a little over life size (1 cm), so they read from the game's camera.
            for k in range(rng.randint(3, 4)):
                at = top + hang * 0.012 + Vector((rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.005)))
                solids.blob(at, rng.uniform(0.011, 0.014), 2, rng.random(), phase, flex=0.9, subdivisions=1)
        variants.append(plant(f"Lingonberries {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Lingonberries Sheet", coll, materials))


def bog_myrtle(coll):
    """Bog myrtle (sweet gale): a knee-high shrub of upright, reddish-brown twigs with narrow, grey-green, toothed leaves
    towards their tips and short brown catkins on the bare wood."""
    bark = bark_material("Myrtle Bark (bake)", tint=(0.55, 0.35, 0.28))
    leaf = varied("Myrtle Leaf (bake)", GREY_GREEN, midrib=0.6, rim_dark=0.1, roughness=0.65)
    catkin = varied("Myrtle Catkin (bake)", [(0.0, (0.3, 0.17, 0.1)), (1.0, (0.45, 0.28, 0.14))], roughness=0.85)
    materials = (bark, leaf, catkin)
    sheet = Sheet(2, 2)
    rng = random.Random(12)
    for cell in range(4):
        rect = sheet.rect(cell)
        sheet.line(rect, [(0.5, 0.0), (0.5 + rng.uniform(-0.04, 0.04), 0.92)], 0.025, 0, rng.random())
        for k in range(9):
            t = 0.12 + 0.82 * k / 8
            side = -1 if k % 2 else 1
            sheet.leaf(rect, (0.5, t * 0.9), side * rng.uniform(0.35, 0.7), 0.32 * (1.15 - t * 0.5), 0.35, NARROW, 1,
                       min(0.95, rng.gauss(0.5, 0.15)), serrate=0.1)
    variants = []
    for key, seed, height, stems in (("A", 711, 0.7, 14), ("B", 712, 0.62, 12)):
        rng = random.Random(seed)
        solids, cards = Kit(), Kit(cards=True)
        for base, lean in tuft(rng, stems, 0.2):
            tall = height * rng.uniform(0.7, 1.0)
            points = ring_points(base, (lean + Vector((0, 0, 1.5))).normalized(), tall, 6, bend=0.03, rng=rng)
            phase = rng.random()
            solids.stem(points, 0.008, 0.003, 0, rng.random(), phase, sides=5, flex0=0.0, flex1=0.8)
            # Twigs off the upper half, each tipped with a spray of leaves; catkins on the bare wood between.
            for i in range(2, 7):
                if rng.random() < 0.85:
                    d = (lean + Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.9))).normalized()
                    twig = [points[i], points[i] + d * 0.08, points[i] + d * 0.15]
                    solids.stem(twig, 0.004, 0.002, 0, rng.random(), phase, sides=4, flex0=0.6)
                    up = (UP - d * d.z).normalized() if abs(d.z) < 0.99 else Vector((1, 0, 0))
                    cards.card(twig[0] + d * 0.02, d, up, rng.uniform(0.2, 0.26), 0.16, sheet.rect(rng.randrange(4)), phase,
                               bend=0.1, droop=0.05)
                    side = d.cross(UP).normalized() if abs(d.z) < 0.99 else Vector((1, 0, 0))
                    cards.card(twig[1], (d + side * 0.8).normalized(), up, rng.uniform(0.16, 0.2), 0.13,
                               sheet.rect(rng.randrange(4)), phase, bend=0.1, droop=0.05)
                elif rng.random() < 0.7:
                    out = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), 0.8)).normalized()
                    solids.blob(points[i] + out * 0.012, 0.008, 2, rng.random(), phase, flex=0.8, squash=(0.8, 0.8, 2.0),
                                subdivisions=1, lumpy=0.2, seed=rng.random() * 40)
            tip = (points[-1] - points[-2]).normalized()
            cards.card(points[-1] - tip * 0.06, tip, Vector((1, 0, 0)), rng.uniform(0.18, 0.24), 0.14,
                       sheet.rect(rng.randrange(4)), phase, bend=0.1)
        variants.append(plant(f"Bog Myrtle {key}", height, solids, cards, sheet, coll, materials))
    return plant_asset(variants, sheet.object("Bog Myrtle Sheet", coll, materials))


# key, asset, builder, cards (Tree Wind) or solid (Layered Lit), glow
PLANTS = (
    ("bush", "Berry Bush", berry_bush, True, False),
    ("herb", "Healing Herb", healing_herb, True, False),
    ("flax", "Flax", flax, True, False),
    ("silk", "Huldra Silk", huldra_silk, True, True),
    ("nettle", "Nettle", nettle, True, False),
    ("cloudberry", "Cloudberries", cloudberries, True, False),
    ("barley", "Wild Barley", wild_barley, True, False),
    ("ramsons", "Ramsons", ramsons, True, False),
    ("angelica", "Angelica", angelica, True, False),
    ("lingonberry", "Lingonberries", lingonberries, True, False),
    ("myrtle", "Bog Myrtle", bog_myrtle, True, False),
)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rows = []
    for key, name, builder, cards, glow in PLANTS:
        if key not in WANTED:
            continue
        if cards:
            made = make_asset(name, builder, 1024, collection(name), glow=glow, masks=False, alpha=True,
                              lods=(0.6, 0.3), lod_mode='thin', solid_region=SOLID_REGION)
        else:
            made = make_asset(name, builder, 1024, collection(name), cavity_distance=0.04, lods=(0.35, 0.1))
        rows.append((made, 1.6 if name == "Berry Bush" else 1.1))
    depth = stage(rows, row_spacing=1.2)
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
