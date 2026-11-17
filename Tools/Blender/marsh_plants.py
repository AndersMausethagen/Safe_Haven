"""The Moss Marsh's terrain details: the plants that grow round its water, each a small clump the terrain draws by the
thousand, modelled and textured in Blender the way plant_nodes.py makes the gathering plants.

- Horsetail ("ormegress", Equisetum): jointed green stems with dark rings at the joints (geometry), a whorl of thin
  needle branches at each joint (a shallow V-shaped card through the stem showing the whorl seen from above), and a few
  thinner stems as crossed cards.
- Sedge Tussock (Carex): a dense round tussock of thin arching blades, green with straw-yellow dead ones, round a low
  hummock of old leaves.
- Reeds: tall strap leaves of cattail and reed for the water's edge (cards), and a few stalks with brown cattail heads
  and their thin spikes (geometry).
- Cotton Grass (Eriophorum): a tuft of thin grass blades, thin stalks and white cotton tufts.
- Marsh Marigold (Caltha palustris): a low clump of round glossy leaves on curving stalks, golden flowers above them.

Built with plant_nodes.py's parts (Kit, the leaf-card sprite sheet, the wind the Tree Wind shader reads: uv1 = sway, flex;
uv2 = phase, flutter) and prop_bake.make_asset (one 1024 atlas, two LODs that drop whole cards), with these differences
for terrain details:
- Budgets like the Misty Woods and Haven details (Lake Fern 482 triangles, Yellow Flowers 209): 150-400 at LOD0.
- A sheet cell holds only its sprite's opaque box, so no texture is spent on empty margins; a card keeps the box's
  proportions (never stretched) and follows a curve of its real length (a drooping card is not squashed).
- The sprites were cut out of a white background, and their half-transparent edges still held some of it (a pale rim
  round every blade once alpha-tested). Edge pixels take the colour of the leaf next to them before the bake.
- Vertex colours for the Safe Haven/Grass shader the terrain details are drawn with: R the bend weight (0 at the ground,
  1 at the top, the wind and the player's push), G a random value per stem (its brightness, and which stems thin out far
  away: a stem and its leaves go together), B 0 (no night glow), A 0. Written linear, so the shader reads them as set.

Writes <art>/<Plant>/<Plant>.fbx (variants "<Plant> A" and "B", each with "LOD1" and "LOD2"), <Plant>_Albedo.png (alpha =
shape), _Normal.png and _ORM.png (half size), and previews: marsh_plants_<Plant>.png (the game's slant, straight down,
eye level) and marsh_plants_contact.png (every plant, and a field of them from the game's default camera).

Run: blender -b --factory-startup --python Tools/Blender/marsh_plants.py -- <project root> [--size 1024] [--art <folder>]
     [--preview <folder>] [--no-field] [plant ...]    (plants: horsetail sedge reeds cotton marigold; all when none named)
<art> defaults to Assets/_Project/Art/Vegetation/Moss Marsh, <preview> to this script's folder.
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
import prop_bake  # noqa: E402
import plant_nodes as pn  # noqa: E402
from prop_bake import (ROOT, args, collection, make_asset, mix, new_material, node, ramp, rgb, select_only,  # noqa: E402
                       tube, use_gpu, value)


def option(name, default=None, flag=False):
    if name not in args:
        return default
    at = args.index(name)
    if flag:
        del args[at]
        return True
    found = args[at + 1]
    del args[at:at + 2]
    return found


SIZE = int(option("--size", "1024"))
ART = option("--art", os.path.join(ROOT, "Assets", "_Project", "Art", "Vegetation", "Moss Marsh"))
PREVIEWS = option("--preview", os.path.dirname(os.path.abspath(__file__)))
FIELD = not option("--no-field", flag=True)
WANTED = set(args[1:]) or {"horsetail", "sedge", "reeds", "cotton", "marigold"}
prop_bake.ART = ART  # bake() and export() write there

UP = Vector((0, 0, 1))
SPRITES = pn.SPRITES
SOLID_REGION = (0.0, 0.79, 1.0, 1.0)  # stems and heads; the sprite cells below
ASPECT = {}  # sheet cell -> width / length of the sprite box it holds
STALK = {}  # sheet cell -> share of the box's height, from its foot, that is only a thin stalk


# ----- Sprites ------------------------------------------------------------------------------------------------------

def push_pull(colour, weight):
    """Fill colour where weight is 0 from the weighted pixels round it (averages over ever larger blocks)."""
    levels = []
    c, w = colour * weight[..., None], weight.copy()
    while True:
        levels.append((c, w))
        if c.shape[0] <= 1 and c.shape[1] <= 1:
            break
        h, wd = c.shape[0] + c.shape[0] % 2, c.shape[1] + c.shape[1] % 2
        cp = numpy.zeros((h, wd, 3), dtype=numpy.float32)
        wp = numpy.zeros((h, wd), dtype=numpy.float32)
        cp[:c.shape[0], :c.shape[1]] = c
        wp[:w.shape[0], :w.shape[1]] = w
        c = cp[0::2, 0::2] + cp[1::2, 0::2] + cp[0::2, 1::2] + cp[1::2, 1::2]
        w = wp[0::2, 0::2] + wp[1::2, 0::2] + wp[0::2, 1::2] + wp[1::2, 1::2]
    fill = levels[-1][0] / numpy.maximum(levels[-1][1], 1e-6)[..., None]
    for c, w in reversed(levels[:-1]):
        up = numpy.repeat(numpy.repeat(fill, 2, axis=0), 2, axis=1)[:c.shape[0], :c.shape[1]]
        own = c / numpy.maximum(w, 1e-6)[..., None]
        t = numpy.clip(w, 0.0, 1.0)[..., None]
        fill = own * t + up * (1.0 - t)
    return fill


def clean_sprite(path, name, tint=(1.0, 1.0, 1.0)):
    """A generated sprite as a new image whose half-transparent edge pixels (and the empty ones round them) take the
    colour of the solid leaf next to them: no trace of the white it was cut from, and no dark or pale halo in the mips."""
    source = bpy.data.images.load(path, check_existing=False)
    w, h = source.size
    px = numpy.empty(w * h * 4, dtype=numpy.float32)
    source.pixels.foreach_get(px)
    bpy.data.images.remove(source)
    px = px.reshape(h, w, 4)
    alpha = px[:, :, 3]
    solid = (alpha > 0.93).astype(numpy.float32)
    if solid.sum() < 50:
        solid = (alpha > 0.6).astype(numpy.float32)
    fill = push_pull(px[:, :, :3], solid)
    keep = solid[..., None]
    px[:, :, :3] = (px[:, :, :3] * keep + fill * (1.0 - keep)) * numpy.array(tint, dtype=numpy.float32)
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = 'sRGB'
    # Channel packed: Cycles would otherwise premultiply it and the filled colour round the leaf would bake black.
    img.alpha_mode = 'CHANNEL_PACKED'
    img.pixels.foreach_set(numpy.clip(px, 0.0, 1.0).ravel())
    return img


def sprite_material(name, img, roughness=0.65):
    """A sprite on the sheet: its colour, its alpha the shape ("SH Solid": the coverage bake), its shading the relief."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-900, 0)
    tex = node(tree, "ShaderNodeTexImage", (-650, 0))
    tex.image = img
    tex.interpolation = 'Cubic'
    tex.extension = 'CLIP'
    links.new(uv.outputs["UV"], tex.inputs["Vector"])
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = roughness
    solid = tree.nodes.new("NodeReroute")
    links.new(tex.outputs["Alpha"], solid.inputs[0])
    solid.label = "SH Solid"
    grey = node(tree, "ShaderNodeRGBToBW", (-300, -200))
    links.new(tex.outputs["Color"], grey.inputs["Color"])
    bump = node(tree, "ShaderNodeBump", (0, -200), Strength=0.35, Distance=0.002)
    links.new(grey.outputs["Val"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def grid(cols, rows, region=(0.0, 0.0, 1.0, 0.78), pad=0.006):
    u0, v0, u1, v1 = region
    cw, ch = (u1 - u0) / cols, (v1 - v0) / rows
    return [(u0 + c * cw + pad, v0 + r * ch + pad, u0 + (c + 1) * cw - pad, v0 + (r + 1) * ch - pad)
            for r in range(rows) for c in range(cols)]


class Sheet(pn.Sheet):
    """plant_nodes' sprite sheet, its cells given as rects; each holds its sprite's opaque box only."""

    def __init__(self, cells):
        self.kit = pn.Kit(cards=False)
        self.cells = cells

    def rect(self, index, pad=0.0):
        return self.cells[index]

    def sprite(self, index, material, img):
        rect = self.cells[index]
        x0, y0, x1, y1 = pn.opaque_box(img)
        u0, v0, u1, v1 = rect
        vs = [self.kit.bm.verts.new(Vector(p)) for p in ((u0, v0, 0), (u1, v0, 0), (u1, v1, 0), (u0, v1, 0))]
        self.kit.face(vs, [(x0, y0), (x1, y0), (x1, y1), (x0, y1)], material, 0.5, facing=UP)
        ASPECT[rect] = (x1 - x0) / max(y1 - y0, 1e-3)
        STALK[rect] = stalk_share(img, (x0, y0, x1, y1))
        print(f"SPRITE {img.name}: box {x0:.2f}-{x1:.2f} x {y0:.2f}-{y1:.2f}, aspect {ASPECT[rect]:.2f}, "
              f"stalk {STALK[rect]:.2f}")


def stalk_share(img, box, thin=0.12):
    """How much of the box, from its foot up, is only a thin stalk (rows whose opaque width is under thin of the box's)."""
    w, h = img.size
    px = numpy.empty(w * h * 4, dtype=numpy.float32)
    img.pixels.foreach_get(px)
    alpha = px.reshape(h, w, 4)[:, :, 3] > 0.35
    x0, y0, x1, y1 = (int(box[0] * w), int(box[1] * h), int(box[2] * w), int(box[3] * h))
    for row in range(y0, y1):
        if alpha[row, x0:x1].sum() > thin * (x1 - x0):
            return max(0.0, (row - y0) / max(y1 - y0, 1))
    return 0.0


def sprite_sheet(asset, names, cells, first_material, tints=None):
    """The asset's sprites (Tools/LocalSpawn/Gathering/Sprites/<asset>/<name>.png) on a sheet, as materials from index
    first_material on; returns (sheet, materials, {name: cell})."""
    sheet = Sheet(cells)
    materials, by_name = [], {}
    for i, name in enumerate(names):
        path = os.path.join(SPRITES, asset, f"{name}.png")
        if not os.path.exists(path):
            raise FileNotFoundError(f"{path}: run Tools/LocalSpawn/generate_gathering.py sprites \"{asset}\" first")
        img = clean_sprite(path, f"{asset} {name}", (tints or {}).get(name, (1.0, 1.0, 1.0)))
        material = sprite_material(f"{asset} Sprite {name} (bake)", img)
        materials.append(material)
        sheet.sprite(i, first_material + i, img)
        by_name[name] = cells[i]
    return sheet, tuple(materials), by_name


# ----- Parts --------------------------------------------------------------------------------------------------------

def card(kit, base, direction, side, length, rect, phase, curl=0.0, segments=2, flex=1.0, width=None, twist=0.0,
         stalk=0.0):
    """A leaf card from base along direction, its sprite's width across side, curling over by curl (radians, over its
    length: positive bends the tip down) in segments of equal length, so the sprite keeps its proportions. Two-sided.
    stalk (metres): the sprite's thin stalk drawn out to this length first (one straight segment; a stalk stretched
    along its length looks the same), and length is then the rest of the sprite: a leaf held up on a long stalk."""
    d = direction.normalized()
    side = (side - d * side.dot(d)).normalized()
    share = STALK.get(tuple(rect), 0.0) if stalk > 0 else 0.0
    if width is None:
        width = length / max(1.0 - share, 0.2) * ASPECT.get(tuple(rect), 1.0)
    axis = d.cross(UP)
    axis = axis.normalized() if axis.length > 1e-4 else side.copy()
    step = length / segments
    rows, p = [], base.copy()
    if share > 0:
        rows.append((0.0, kit.bm.verts.new(p - side * width * 0.5), kit.bm.verts.new(p + side * width * 0.5)))
        p = p + d * stalk
    for j in range(segments + 1):
        t = j / segments
        turn = Matrix.Rotation(-curl * t, 3, axis) @ Matrix.Rotation(twist * t, 3, d)
        s = (turn @ side).normalized()
        rows.append((share + (1.0 - share) * t, kit.bm.verts.new(p - s * width * 0.5),
                     kit.bm.verts.new(p + s * width * 0.5)))
        if j < segments:
            p = p + (Matrix.Rotation(-curl * (j + 0.5) / segments, 3, axis) @ d) * step
    return _card_faces(kit, rows, rect, phase, flex, lambda t: t)


def whorl(kit, centre, heading, size, rect, phase, lift=0.35, flex=1.0):
    """A whorl seen from above, as a shallow V through its stem: the card's middle at centre, both ends raised by lift
    (radians), so it reads as a star from above and as rising branches from the side."""
    h = Vector((heading.x, heading.y, 0.0)).normalized()
    side = UP.cross(h).normalized()
    width = size * ASPECT.get(tuple(rect), 1.0)
    arm = (math.cos(lift) * size * 0.5, math.sin(lift) * size * 0.5)
    points = [centre - h * arm[0] + UP * arm[1], centre, centre + h * arm[0] + UP * arm[1]]
    rows = [(t, kit.bm.verts.new(p - side * width * 0.5), kit.bm.verts.new(p + side * width * 0.5))
            for t, p in zip((0.0, 0.5, 1.0), points)]
    return _card_faces(kit, rows, rect, phase, flex, lambda t: abs(t - 0.5) * 2.0)


def _card_faces(kit, rows, rect, phase, flex, flutter):
    u0, v0, u1, v1 = rect
    faces = []
    for (ta, a0, a1), (tb, b0, b1) in zip(rows, rows[1:]):
        f = kit.bm.faces.new((a0, a1, b1, b0))
        f.normal_update()
        f[kit.card_flag] = 1
        f.smooth = True
        for loop in f.loops:
            v = loop.vert
            t = ta if v in (a0, a1) else tb
            across = 0.0 if v in (a0, b0) else 1.0
            loop[kit.atlas].uv = (u0 + (u1 - u0) * across, v0 + (v1 - v0) * t)
            loop[kit.wind].uv = (0.0, flex)
            loop[kit.phase].uv = (phase, flutter(t))
        faces.append(f)
    return faces


def stem(kit, points, radius, tip, material, var, phase, sides=3, tile=0.25, flex0=0.0):
    """A thin open tube (no caps: its ends are in the ground or under a head), flexing more towards its tip. Its grain
    V runs in metres / tile along it (a jointed stem sets tile to its joint spacing: one unit a joint)."""
    before = set(kit.bm.faces)
    radii = [radius + (tip - radius) * i / (len(points) - 1) for i in range(len(points))]
    tube(kit.bm, points, radii, sides, kit.uv, wood_cap=False, material=material, cap_material=material, tile=tile)
    new = [f for f in kit.bm.faces if f not in before]
    base = points[0]
    span = max(sum((b - a).length for a, b in zip(points, points[1:])), 1e-4)
    for f in new:
        f[kit.var] = var
        f.smooth = True
    kit.wind_on(new, lambda p: flex0 + (1.0 - flex0) * min(1.0, (p - base).length / span), phase)
    return new


def along(points, s):
    """The point a share s of the way along a polyline, and the direction there."""
    lengths = [(b - a).length for a, b in zip(points, points[1:])]
    goal = s * sum(lengths)
    for i, length in enumerate(lengths):
        if goal <= length or i == len(lengths) - 1:
            a, b = points[i], points[i + 1]
            return a.lerp(b, min(1.0, goal / max(length, 1e-6))), (b - a).normalized()
        goal -= length


def outward_side(d, out):
    """The side vector for a blade leaning along d: across, so its face looks out (out: the way from the plant's middle)."""
    side = d.cross(out if out.length > 1e-4 else Vector((1, 0, 0)))
    if side.length < 1e-4:
        side = d.orthogonal()
    return side.normalized()


def jointed_material(name):
    """Horsetail stem: green, a thin dark sheath ring at every joint (one joint per grain V unit), browner at the foot."""
    material, tree, bsdf = new_material(name)
    links = tree.links
    attribute = tree.nodes.new("ShaderNodeAttribute")
    attribute.attribute_name = "sh_var"
    attribute.attribute_type = 'GEOMETRY'
    attribute.location = (-900, 400)
    colours = ramp(tree, (-650, 400), [(0.0, (0.085, 0.15, 0.045)), (0.6, (0.11, 0.18, 0.05)), (1.0, (0.14, 0.21, 0.07))])
    links.new(attribute.outputs["Fac"], colours.inputs["Fac"])
    uv = tree.nodes.new("ShaderNodeUVMap")
    uv.uv_map = "grain"
    uv.location = (-1200, -100)
    split = node(tree, "ShaderNodeSeparateXYZ", (-1000, -100))
    links.new(uv.outputs["UV"], split.inputs["Vector"])
    joint = node(tree, "ShaderNodeMath", (-800, -100))
    joint.operation = 'FRACT'
    links.new(split.outputs["Y"], joint.inputs[0])
    # The sheath just above each joint: dark teeth at its top, a paler band below them.
    ring = ramp(tree, (-600, -100), [(0.0, (0.55, 0.55, 0.5)), (0.05, (0.55, 0.55, 0.5)), (0.065, (0.12, 0.1, 0.08)),
                                     (0.1, (0.12, 0.1, 0.08)), (0.12, (1, 1, 1))])
    links.new(joint.outputs["Value"], ring.inputs["Fac"])
    colour = mix(tree, (-200, 300), colours.outputs["Color"], ring.outputs["Color"], value(tree, (-400, 200), 1.0),
                 blend='MULTIPLY')
    foot = ramp(tree, (-600, -400), [(0.0, (0.6, 0.45, 0.3)), (0.9, (1, 1, 1))])
    links.new(split.outputs["Y"], foot.inputs["Fac"])
    colour = mix(tree, (0, 300), colour, foot.outputs["Color"], value(tree, (-200, 100), 1.0), blend='MULTIPLY')
    # Fine ridges along the stem (horsetails are grooved).
    grooves = node(tree, "ShaderNodeMath", (-800, -700))
    grooves.operation = 'PINGPONG'
    grooves.inputs[1].default_value = 0.012
    links.new(split.outputs["X"], grooves.inputs[0])
    links.new(colour, bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.5
    relief = node(tree, "ShaderNodeMath", (-500, -700))
    relief.operation = 'MAXIMUM'
    links.new(grooves.outputs["Value"], relief.inputs[0])
    sheath = ramp(tree, (-650, -900), [(0.0, (0, 0, 0)), (0.05, (0, 0, 0)), (0.07, (1, 1, 1)), (0.11, (0, 0, 0))])
    links.new(joint.outputs["Value"], sheath.inputs["Fac"])
    links.new(sheath.outputs["Color"], relief.inputs[1])
    bump = node(tree, "ShaderNodeBump", (500, -300), Strength=0.4, Distance=0.001)
    links.new(relief.outputs["Value"], bump.inputs["Height"])
    links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


# ----- Plants -------------------------------------------------------------------------------------------------------

def horsetail(coll):
    """Horsetail: jointed stems (dark sheath rings) with a V-card whorl at each joint, and thinner card stems."""
    stem_material = jointed_material("Horsetail Stem (bake)")
    materials = (stem_material,)
    cells = grid(2, 2)
    sheet, sprites, cell = sprite_sheet("Horsetail", ["stem_a", "stem_b", "whorl_a", "whorl_b"], cells, len(materials))
    materials = materials + sprites
    variants = []
    for key, seed, stems, extras in (("A", 7111, 6, 3), ("B", 7112, 5, 3)):
        rng = random.Random(seed)
        solids, cards = pn.Kit(), pn.Kit(cards=True)
        height = 0.6
        for base, lean in pn.tuft(rng, stems, 0.13):
            tall = rng.uniform(0.34, 0.6)
            joint = rng.uniform(0.06, 0.08)
            points = pn.ring_points(base, lean, tall, 4, bend=rng.uniform(0.0, 0.04), rng=rng, wobble=0.012)
            phase = rng.random()
            stem(solids, points, 0.0034, 0.0012, 0, rng.random(), phase, sides=3, tile=joint)
            heading = rng.uniform(0, 2 * math.pi)
            joints = int(tall / joint)
            for j in range(2, joints):
                s = j * joint / tall
                if s > 0.86:
                    break
                at, _ = along(points, s)
                size = rng.uniform(0.13, 0.16) * (1.0 - 0.6 * s)
                heading += rng.uniform(1.2, 2.0)
                whorl(cards, at, Vector((math.cos(heading), math.sin(heading), 0)), size,
                      cell["whorl_a" if rng.random() < 0.75 else "whorl_b"], phase, lift=rng.uniform(0.6, 0.85),
                      flex=0.3 + 0.7 * s)
        # Thinner stems behind them: a whole stem with its whorls on two crossed cards.
        for base, lean in pn.tuft(rng, extras, 0.17):
            phase = rng.random()
            rect = cell["stem_a" if rng.random() < 0.8 else "stem_b"]
            length = rng.uniform(0.3, 0.48)
            turn = rng.uniform(0, math.pi)
            for k in range(2):
                a = turn + k * math.pi / 2
                card(cards, base, lean, Vector((math.cos(a), math.sin(a), 0)), length, rect, phase,
                     curl=rng.uniform(0.0, 0.15), segments=2)
        variants.append(pn.plant(f"Horsetail {key}", height, solids, cards, sheet, coll, materials))
    return pn.plant_asset(variants, sheet.object("Horsetail Sheet", coll, materials))


def sedge_tussock(coll):
    """Sedge tussock: a low hummock of old leaves under a dense fountain of thin blades, upright in the middle and
    arching over at the rim, a quarter of them dead and straw-yellow."""
    litter = pn.varied("Sedge Litter (bake)", [(0.0, (0.06, 0.05, 0.028)), (0.6, (0.1, 0.085, 0.04)),
                                               (1.0, (0.15, 0.12, 0.055))], roughness=0.85)
    materials = (litter,)
    sheet, sprites, cell = sprite_sheet("Sedge Tussock", ["blades_a", "blades_b", "dead_a", "blades_c"], grid(2, 2),
                                        len(materials), tints={"dead_a": (0.8, 0.78, 0.68)})
    materials = materials + sprites
    variants = []
    for key, seed, radius, height, blades in (("A", 7211, 0.17, 0.55, 46), ("B", 7212, 0.14, 0.45, 40)):
        rng = random.Random(seed)
        solids, cards = pn.Kit(), pn.Kit(cards=True)
        solids.lathe(Vector((0, 0, -0.03)), [(radius, 0.0), (radius * 0.85, 0.045), (radius * 0.5, 0.085),
                                             (radius * 0.15, 0.1)], 7, 0, rng.random(), wobble=0.12, seed=rng.random() * 9)
        for k in range(blades):
            a = 2 * math.pi * (k + rng.uniform(-0.4, 0.4)) / blades
            r = radius * 0.75 * math.sqrt(rng.random())
            out = r / (radius * 0.75)
            base = Vector((math.cos(a) * r, math.sin(a) * r, 0.045 * (1.0 - out * out) + 0.01))
            elevation = math.radians(85 - 28 * out + rng.uniform(-6, 6))
            d = Vector((math.cos(a) * math.cos(elevation), math.sin(a) * math.cos(elevation), math.sin(elevation)))
            radial = Vector((math.cos(a), math.sin(a), 0))
            side = Vector((-math.sin(a), math.cos(a), 0))
            side = (Matrix.Rotation(rng.uniform(-0.6, 0.6), 3, d) @ side)
            roll = rng.random()
            name = "dead_a" if roll < 0.12 else ("blades_a", "blades_b", "blades_c")[int(roll * 10) % 3]
            length = height * rng.uniform(0.7, 1.0) * (1.05 if name == "dead_a" else 1.0)
            card(cards, base, d, side, length, cell[name], rng.random(), curl=math.radians(8 + 40 * out + rng.uniform(0, 12)),
                 segments=3 if out > 0.45 else 2, twist=rng.uniform(-0.3, 0.3))
        variants.append(pn.plant(f"Sedge Tussock {key}", height, solids, cards, sheet, coll, materials))
    return pn.plant_asset(variants, sheet.object("Sedge Tussock Sheet", coll, materials))


def reeds(coll):
    """Reeds at the water's edge: tall strap leaves (cards) and a few stalks with cattail heads and spikes (geometry)."""
    stalk = pn.varied("Reed Stalk (bake)", [(0.0, (0.09, 0.14, 0.045)), (1.0, (0.15, 0.18, 0.065))], roughness=0.55)
    head = pn.varied("Cattail Head (bake)", [(0.0, (0.07, 0.035, 0.018)), (0.6, (0.1, 0.055, 0.026)),
                                             (1.0, (0.13, 0.075, 0.035))], roughness=0.9, strands=True)
    spike = pn.varied("Cattail Spike (bake)", [(0.0, (0.3, 0.24, 0.11)), (1.0, (0.42, 0.34, 0.16))], roughness=0.7)
    materials = (stalk, head, spike)
    cells = grid(4, 1)
    sheet, sprites, cell = sprite_sheet("Reeds", ["leaves_a", "leaves_b", "cattail_a", "cattail_b"], cells,
                                        len(materials))
    materials = materials + sprites
    variants = []
    for key, seed, leaves, cattails in (("A", 7311, 20, 3), ("B", 7312, 17, 2)):
        rng = random.Random(seed)
        solids, cards = pn.Kit(), pn.Kit(cards=True)
        height = 1.6
        for base, lean in pn.tuft(rng, leaves, 0.2):
            phase = rng.random()
            d = (Vector((lean.x, lean.y, 0)) * 0.55 + UP + Vector((rng.uniform(-0.1, 0.1), rng.uniform(-0.1, 0.1), 0))).normalized()
            out = Vector((d.x, d.y, 0))
            side = outward_side(d, out)
            side = Matrix.Rotation(rng.uniform(-0.8, 0.8), 3, d) @ side
            name = "leaves_a" if rng.random() < 0.55 else "leaves_b"
            card(cards, base, d, side, rng.uniform(0.8, 1.4), cell[name], phase, curl=math.radians(rng.uniform(5, 35)),
                 segments=3, twist=rng.uniform(-0.5, 0.5))
        for base, lean in pn.tuft(rng, cattails, 0.12):
            phase = rng.random()
            tall = rng.uniform(1.15, 1.5)
            points = pn.ring_points(base, (lean + UP * 2).normalized(), tall, 4, bend=0.0, rng=rng, wobble=0.01)
            stem(solids, points, 0.005, 0.0035, 0, rng.random(), phase, sides=3)
            top, direction = points[-1], (points[-1] - points[-2]).normalized()
            size = rng.uniform(0.85, 1.15)
            profile = [(0.0045, -0.16), (0.011, -0.15), (0.0125, -0.1), (0.012, -0.04), (0.009, -0.015), (0.004, -0.008)]
            before = set(solids.bm.faces)
            solids.lathe(top, [(r * size, h * size) for r, h in profile], 6, 1, rng.random(), wobble=0.05,
                         seed=rng.random() * 9, axis=direction)
            solids.wind_on([f for f in solids.bm.faces if f not in before], 1.0, phase)
            tip = top + direction * rng.uniform(0.05, 0.09)
            stem(solids, [top - direction * 0.01, top.lerp(tip, 0.5), tip], 0.0028, 0.0008, 2, rng.random(), phase,
                 sides=3, flex0=1.0)
        variants.append(pn.plant(f"Reeds {key}", height, solids, cards, sheet, coll, materials))
    return pn.plant_asset(variants, sheet.object("Reeds Sheet", coll, materials))


def cotton_grass(coll):
    """Cotton grass: a tuft of thin grass blades, thin stalks, and a white cotton tuft on each: two crossed cards and
    one lying flat, so it stays a soft round tuft from the game's camera and from above."""
    stalk = pn.varied("Cotton Grass Stalk (bake)", [(0.0, (0.1, 0.13, 0.05)), (1.0, (0.17, 0.17, 0.075))], roughness=0.7)
    materials = (stalk,)
    sheet, sprites, cell = sprite_sheet("Cotton Grass", ["grass_a", "grass_b", "tuft_a", "tuft_b"], grid(2, 2),
                                        len(materials))
    materials = materials + sprites
    variants = []
    for key, seed, blades, heads in (("A", 7411, 14, 7), ("B", 7412, 12, 6)):
        rng = random.Random(seed)
        solids, cards = pn.Kit(), pn.Kit(cards=True)
        height = 0.42
        for base, lean in pn.tuft(rng, blades, 0.12):
            out = Vector((lean.x, lean.y, 0))
            elevation = math.radians(rng.uniform(45, 80))
            heading = math.atan2(out.y, out.x) if out.length > 1e-3 else rng.uniform(0, 6.28)
            d = Vector((math.cos(heading) * math.cos(elevation), math.sin(heading) * math.cos(elevation),
                        math.sin(elevation)))
            side = Matrix.Rotation(rng.uniform(-0.5, 0.5), 3, d) @ Vector((-math.sin(heading), math.cos(heading), 0))
            card(cards, base, d, side, rng.uniform(0.17, 0.27), cell["grass_a" if rng.random() < 0.5 else "grass_b"],
                 rng.random(), curl=math.radians(rng.uniform(15, 50)), segments=2)
        for base, lean in pn.tuft(rng, heads, 0.13):
            phase = rng.random()
            points = pn.ring_points(base, lean, rng.uniform(0.26, 0.4), 2, bend=rng.uniform(0.0, 0.05), rng=rng)
            stem(solids, points, 0.0018, 0.0013, 0, rng.random(), phase, sides=3)
            top = points[-1]
            turn = rng.uniform(0, math.pi)
            if rng.random() < 0.35:
                # A few nodding heads on their own thin stalks (the sprite shows them from the side): crossed cards.
                rect = cell["tuft_b"]
                size = rng.uniform(0.1, 0.13)
                for k in range(2):
                    a = turn + k * math.pi / 2
                    card(cards, top - UP * size * 0.15, UP, Vector((math.cos(a), math.sin(a), 0)), size, rect, phase,
                         segments=1)
                continue
            # One upright tuft: two crossed cards (its sprite's stalk squeezed into the stalk's top) and one lying flat
            # that shows only the cotton, so it stays a round tuft from above.
            rect = cell["tuft_a"]
            size = rng.uniform(0.045, 0.06)
            for k in range(2):
                a = turn + k * math.pi / 2
                card(cards, top - UP * 0.012, UP, Vector((math.cos(a), math.sin(a), 0)), size, rect, phase,
                     segments=1, stalk=0.012)
            share = STALK.get(tuple(rect), 0.0)
            u0, v0, u1, v1 = rect
            cotton = (u0, v0 + (v1 - v0) * share, u1, v1)
            heading = Vector((math.cos(turn), math.sin(turn), 0))
            centre = top + UP * size * 0.3
            card(cards, centre - heading * size * 0.35, heading, UP.cross(heading), size * 0.7, cotton, phase, segments=1,
                 width=size * 0.7 * ASPECT.get(tuple(rect), 1.0) / max(1.0 - share, 0.2))
        variants.append(pn.plant(f"Cotton Grass {key}", height, solids, cards, sheet, coll, materials))
    return pn.plant_asset(variants, sheet.object("Cotton Grass Sheet", coll, materials))


def marsh_marigold(coll):
    """Marsh marigold: round glossy leaves on stalks that rise and curve over, so the blades lie open to the sky, and
    golden flowers on their own stalks just above them."""
    stalk = pn.varied("Marigold Stalk (bake)", [(0.0, (0.06, 0.11, 0.03)), (1.0, (0.1, 0.15, 0.04))], roughness=0.45,
                      sheen=0.5)
    materials = (stalk,)
    sheet, sprites, cell = sprite_sheet("Marsh Marigold", ["leaf_a", "leaf_b", "flower_a", "flower_b"], grid(2, 2),
                                        len(materials), tints={"leaf_a": (0.72, 0.78, 0.7), "leaf_b": (0.75, 0.8, 0.72)})
    materials = materials + sprites
    variants = []
    for key, seed, leaves, flowers in (("A", 7511, 17, 6), ("B", 7512, 15, 5)):
        rng = random.Random(seed)
        solids, cards = pn.Kit(), pn.Kit(cards=True)
        height = 0.28
        for k in range(leaves):
            a = 2 * math.pi * (k + rng.uniform(-0.35, 0.35)) / leaves
            r = rng.uniform(0.0, 0.04)
            base = Vector((math.cos(a) * r, math.sin(a) * r, -0.01))
            # Outer leaves on longer, more leaning stalks: a low mound, the blades tilted out to the light.
            outer = rng.random()
            elevation = math.radians(80 - 25 * outer + rng.uniform(-5, 5))
            d = Vector((math.cos(a) * math.cos(elevation), math.sin(a) * math.cos(elevation), math.sin(elevation)))
            side = Vector((-math.sin(a), math.cos(a), 0))
            tilt = math.radians(rng.uniform(5, 35))
            card(cards, base, d, side, rng.uniform(0.075, 0.11), cell["leaf_a" if rng.random() < 0.6 else "leaf_b"],
                 rng.random(), curl=max(0.0, elevation - tilt) / 0.75, segments=2, twist=rng.uniform(-0.2, 0.2),
                 stalk=rng.uniform(0.07, 0.16) * (0.8 + 0.4 * outer))
        for base, lean in pn.tuft(rng, flowers, 0.1):
            phase = rng.random()
            points = pn.ring_points(base, lean, rng.uniform(0.17, 0.27), 2, bend=0.02, rng=rng)
            stem(solids, points, 0.0028, 0.0022, 0, rng.random(), phase, sides=3)
            top = points[-1]
            if rng.random() < 0.3:
                # A spray of three flowers and a bud on its own stalks (the sprite shows them from the side): a card
                # rising from the stalk's top, leaning out.
                out = Vector((lean.x, lean.y, 0))
                out = out.normalized() if out.length > 1e-3 else Vector((1, 0, 0))
                elevation = math.radians(rng.uniform(50, 70))
                d = (out * math.cos(elevation) + UP * math.sin(elevation)).normalized()
                card(cards, top - d * 0.01, d, UP.cross(out), rng.uniform(0.09, 0.11), cell["flower_b"], phase,
                     curl=0.2, segments=2)
                continue
            size = rng.uniform(0.045, 0.06)
            facing = (lean + Vector((rng.uniform(-0.3, 0.3), rng.uniform(-0.3, 0.3), 2.0))).normalized()
            heading = facing.orthogonal().normalized()
            card(cards, top - heading * size * 0.5, heading, facing.cross(heading), size, cell["flower_a"], phase,
                 segments=1)
        variants.append(pn.plant(f"Marsh Marigold {key}", height, solids, cards, sheet, coll, materials))
    return pn.plant_asset(variants, sheet.object("Marsh Marigold Sheet", coll, materials))


# key, asset, builder, preview spacing between A and B
PLANTS = (
    ("horsetail", "Horsetail", horsetail, 0.7),
    ("sedge", "Sedge Tussock", sedge_tussock, 0.9),
    ("reeds", "Reeds", reeds, 1.1),
    ("cotton", "Cotton Grass", cotton_grass, 0.6),
    ("marigold", "Marsh Marigold", marsh_marigold, 0.6),
)


# ----- Vertex colours and export ------------------------------------------------------------------------------------

def grass_colours(variants):
    """Vertex colours for the Safe Haven/Grass shader from the wind data: R = sway (bend weight), G = the stem's phase
    (a random value shared by a stem and its leaves), B = A = 0."""
    for obj in variants:
        mesh = obj.data
        wind, phase = mesh.uv_layers["wind"].data, mesh.uv_layers["phase"].data
        colour = mesh.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
        values = numpy.zeros((len(mesh.loops), 4), dtype=numpy.float32)
        sway = numpy.empty(len(mesh.loops) * 2, dtype=numpy.float32)
        wind.foreach_get("uv", sway)
        stems = numpy.empty(len(mesh.loops) * 2, dtype=numpy.float32)
        phase.foreach_get("uv", stems)
        values[:, 0] = numpy.clip(sway[0::2], 0.0, 1.0)
        values[:, 1] = numpy.clip(stems[0::2], 0.0, 1.0)
        colour.data.foreach_set("color", values.ravel())
        mesh.color_attributes.active_color = colour


def export(objects, name):
    """prop_bake.export, with the vertex colours written linear (the Grass shader reads them as data)."""
    select_only(objects)
    path = os.path.join(prop_bake.ART, name, f"{name}.fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                             axis_up='Y', bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False, colors_type='LINEAR')
    return path


prop_bake.export = export


def measure(obj):
    """Triangles, vertices as Unity will count them (split where position, normal, a UV or the colour differ), size."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    tris = len(mesh.loop_triangles)
    keys = set()
    layers = [layer.data for layer in mesh.uv_layers]
    normals = mesh.corner_normals
    for loop in mesh.loops:
        keys.add((loop.vertex_index, tuple(round(c, 3) for c in normals[loop.index].vector),
                  tuple(tuple(round(c, 4) for c in layer[loop.index].uv) for layer in layers)))
    corners = [Vector(c) for c in obj.bound_box]
    size = [max(c[k] for c in corners) - min(c[k] for c in corners) for k in range(3)]
    return tris, len(keys), size


# ----- Preview ------------------------------------------------------------------------------------------------------

def marsh_ground():
    """Wet marsh soil with moss, a strip of dark water, a soft sun under thin cloud."""
    scene = bpy.context.scene
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=40.0)
    ground = pn.mesh_object("Marsh", bm, collection("Preview"))
    material, tree, bsdf = new_material("Marsh Ground")
    texture = node(tree, "ShaderNodeTexCoord", (-900, 0))
    patches = node(tree, "ShaderNodeTexNoise", (-650, 100), Scale=0.6, Detail=6.0, Roughness=0.6)
    tree.links.new(texture.outputs["Object"], patches.inputs["Vector"])
    colours = ramp(tree, (-400, 100), [(0.38, (0.03, 0.026, 0.018)), (0.55, (0.045, 0.06, 0.02)),
                                       (0.7, (0.06, 0.09, 0.025))])
    tree.links.new(patches.outputs["Fac"], colours.inputs["Fac"])
    moss = node(tree, "ShaderNodeTexNoise", (-650, -400), Scale=40.0, Detail=8.0, Roughness=0.7)
    tree.links.new(texture.outputs["Object"], moss.inputs["Vector"])
    fine = ramp(tree, (-400, -400), [(0.35, (0.65, 0.65, 0.6)), (0.65, (1.25, 1.3, 1.1))])
    tree.links.new(moss.outputs["Fac"], fine.inputs["Fac"])
    ground_colour = mix(tree, (-150, 100), colours.outputs["Color"], fine.outputs["Color"], value(tree, (-300, 0), 1.0),
                        blend='MULTIPLY')
    tree.links.new(ground_colour, bsdf.inputs["Base Color"])
    bump = node(tree, "ShaderNodeBump", (-150, -400), Strength=0.4, Distance=0.01)
    tree.links.new(moss.outputs["Fac"], bump.inputs["Height"])
    tree.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    wet = ramp(tree, (-400, -200), [(0.4, (0.25, 0.25, 0.25)), (0.6, (0.75, 0.75, 0.75))])
    tree.links.new(patches.outputs["Fac"], wet.inputs["Fac"])
    tree.links.new(wet.outputs["Color"], bsdf.inputs["Roughness"])
    ground.data.materials.append(material)
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=1.0)
    water = pn.mesh_object("Water", bm, collection("Preview"))
    water.scale = (24.0, 1.6, 1.0)
    water.location = (0.0, 4.0, 0.015)
    material, tree, bsdf = new_material("Marsh Water")
    bsdf.inputs["Base Color"].default_value = (0.012, 0.018, 0.012, 1)
    bsdf.inputs["Roughness"].default_value = 0.04
    water.data.materials.append(material)
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(48), math.radians(8), math.radians(-35)))
    sun = bpy.context.active_object
    sun.data.energy = 3.0
    sun.data.angle = math.radians(8)
    sun.data.color = (1.0, 0.97, 0.9)
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.6, 0.66, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8


def alpha_tested(objects, cutoff=0.35):
    """The preview draws the cards as the game does: cut out where the albedo's alpha is under the cutoff."""
    for material in {m for o in objects for m in o.data.materials if m}:
        tree = material.node_tree
        bsdf = next(n for n in tree.nodes if n.type == 'BSDF_PRINCIPLED')
        if not bsdf.inputs["Alpha"].links:
            continue
        source = bsdf.inputs["Alpha"].links[0].from_socket
        test = node(tree, "ShaderNodeMath", (200, -500))
        test.operation = 'GREATER_THAN'
        test.inputs[1].default_value = cutoff
        tree.links.new(source, test.inputs[0])
        tree.links.new(test.outputs["Value"], bsdf.inputs["Alpha"])


def look(location, target, path, width, height, lens=50, ortho=None):
    cam = prop_bake.camera(location, target, lens)
    if ortho:
        cam.data.type = 'ORTHO'
        cam.data.ortho_scale = ortho
    scene = bpy.context.scene
    use_gpu(scene)
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.cycles.transparent_max_bounces = 48  # layers of cut-out cards
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'AgX'
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    return path


def game_camera(target, distance, width, height, path, pitch=48.0, yaw=0.0, fov=40.0):
    """The game's top-down camera: pitch degrees down, a vertical field of view of fov."""
    p, y = math.radians(pitch), math.radians(yaw)
    back = Vector((-math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
    cam = prop_bake.camera(target + back * distance, target, 50)
    cam.data.sensor_fit = 'VERTICAL'
    cam.data.angle_y = math.radians(fov)
    scene = bpy.context.scene
    use_gpu(scene)
    scene.cycles.samples = 64
    scene.cycles.use_denoising = True
    scene.cycles.transparent_max_bounces = 48
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.view_settings.view_transform = 'AgX'
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    return path


def tile(paths, columns, out, cell=(960, 540)):
    """The renders in a grid, each scaled to cell."""
    rows = (len(paths) + columns - 1) // columns
    w, h = cell
    canvas = numpy.zeros((rows * h, columns * w, 4), dtype=numpy.float32)
    canvas[:, :, 3] = 1.0
    for i, path in enumerate(paths):
        img = bpy.data.images.load(path, check_existing=False)
        img.scale(w, h)
        px = numpy.empty(w * h * 4, dtype=numpy.float32)
        img.pixels.foreach_get(px)
        bpy.data.images.remove(img)
        r, c = i // columns, i % columns
        canvas[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = px.reshape(h, w, 4)
    result = bpy.data.images.new(os.path.basename(out), columns * w, rows * h, alpha=True)
    result.colorspace_settings.name = 'sRGB'
    result.pixels.foreach_set(canvas.ravel())
    result.filepath_raw = out
    result.file_format = 'PNG'
    result.save()
    bpy.data.images.remove(result)
    return out


def stack(paths, width, out):
    """The images one above the other, each scaled to width (its proportions kept)."""
    parts = []
    for path in paths:
        img = bpy.data.images.load(path, check_existing=False)
        w, h = width, round(img.size[1] * width / img.size[0])
        img.scale(w, h)
        px = numpy.empty(w * h * 4, dtype=numpy.float32)
        img.pixels.foreach_get(px)
        bpy.data.images.remove(img)
        parts.append(px.reshape(h, w, 4))
    canvas = numpy.concatenate(list(reversed(parts)), axis=0)  # Blender's rows run bottom up
    result = bpy.data.images.new(os.path.basename(out), width, canvas.shape[0], alpha=True)
    result.colorspace_settings.name = 'sRGB'
    result.pixels.foreach_set(canvas.ravel())
    result.filepath_raw = out
    result.file_format = 'PNG'
    result.save()
    bpy.data.images.remove(result)
    return out


def preview(made):
    """Per plant: A and B from the game's slant (close, as the action camera sees them), straight down and at eye
    level; then every plant together, and a field of them from the default camera (20 m, 1080p)."""
    marsh_ground()
    panels, closeups = {}, []
    for name, objects, gap in made:
        for i, obj in enumerate(objects):
            obj.location = ((i - 0.5) * gap, 0.0, 0.0)
            obj.hide_render = False
        others = [o for _, objs, _ in made for o in objs if o not in objects]
        for o in others:
            o.hide_render = True
        tall = max(o.dimensions.z for o in objects)
        target = Vector((0.0, 0.0, tall * 0.35))
        prefix = os.path.join(PREVIEWS, f"marsh_plants_{name.replace(' ', '_')}")
        shots = [
            game_camera(target, gap * 1.7 + tall * 1.2, 960, 640, prefix + "_game.png", pitch=42.0),
            look(Vector((0, 0, 6.0)), Vector((0, 0, 0)), prefix + "_top.png", 960, 640, ortho=gap * 2.3),
            look(Vector((0.0, -(gap * 2.2 + tall * 1.4), tall * 0.45)), Vector((0, 0, tall * 0.4)),
                 prefix + "_side.png", 960, 640, lens=50),
        ]
        panels[name] = tile(shots, 3, prefix + ".png", cell=(960, 640))
        closeups.append(shots[0])
        for path in shots:
            os.remove(path)
    contact = [panels[name] for name, _, _ in made]
    if FIELD and len(made) > 1:
        contact.append(field(made))
    sheet = stack(contact, 1920, os.path.join(PREVIEWS, "marsh_plants_contact.png"))
    print("PREVIEW", sheet)


def field(made):
    """Every plant scattered over a strip of marsh the way the terrain would paint them (random turn and size), seen
    from the game's default camera: 20 m away, 48 degrees down, 1080p."""
    rng = random.Random(3)
    copies = []
    # name: (y from, y to, spread, count); the water lies at y 3.2 to 4.8
    zones = {"Reeds": (2.9, 3.6, 0.6, 60), "Horsetail": (1.2, 2.9, 1.5, 70), "Sedge Tussock": (-1.5, 1.8, 2.5, 55),
             "Cotton Grass": (-4.0, 0.0, 2.5, 80), "Marsh Marigold": (1.8, 3.1, 2.0, 60)}
    for name, objects, _ in made:
        for o in objects:
            o.hide_render = True
        y0, y1, spread, count = zones.get(name, (-2.0, 2.0, 2.0, 25))
        for k in range(count):
            source = objects[k % len(objects)]
            copy = source.copy()
            bpy.context.scene.collection.objects.link(copy)
            copy.hide_render = False
            copy.location = (rng.uniform(-10.0, 10.0), rng.uniform(y0, y1) + rng.gauss(0, spread * 0.2), 0.0)
            copy.rotation_euler = (0, 0, rng.uniform(0, 2 * math.pi))
            copy.scale = [rng.uniform(0.8, 1.2)] * 3
            copies.append(copy)
    path = os.path.join(PREVIEWS, "marsh_plants_field.png")
    game_camera(Vector((0.0, 1.0, 0.0)), 20.0, 1920, 1080, path)
    return path


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    made, report = [], []
    for key, name, builder, gap in PLANTS:
        if key not in WANTED:
            continue
        variants = make_asset(name, builder, SIZE, collection(name), masks=False, alpha=True, lods=(0.6, 0.3),
                              lod_mode='thin', solid_region=SOLID_REGION, after_bake=grass_colours)
        for variant in variants:
            for obj in [variant] + [o for o in bpy.data.objects if o.name.startswith(variant.name + " LOD")]:
                tris, verts, size = measure(obj)
                report.append(f"{obj.name}: {tris} triangles, {verts} vertices, "
                              f"{size[0]:.2f} x {size[1]:.2f} m, {size[2]:.2f} m tall")
        alpha_tested(variants)
        made.append((name, variants, gap))
    for line in report:
        print("BUDGET", line)
    preview(made)
    print("MARSH PLANTS DONE")


if __name__ == "__main__":
    main()
