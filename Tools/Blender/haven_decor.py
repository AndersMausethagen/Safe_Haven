"""Haven decorations and the wall torch: small built things the player places in the Haven, made from the Haven's
building kit (building_kit.py, building_props.py) so they share the kit buildings' tiling scanned materials.

Assets (each its own folder under Assets/_Project/Art/Haven/Structures/<Asset>/<Asset>.fbx, holding "<Asset>",
"<Asset> LOD1" and "<Asset> LOD2"): Log Bench, Plank Bench, Trestle Table, Woodpile, Barrels and Crates, Carved Post,
Flower Bed, Signpost, Rune Stone, Hay Bales, Wall Torch. The Wall Torch's FBX also carries two empties: "Mount" (the
back plate's centre, where it touches the wall: the model's origin) and "Flame" (the top of the torch head, where the
game puts its flame and light). AgentScripts/BuildStructureModels.cs maps the "Kit ..." material names to the game's.

Drawn in the kit's design axes (x right, y up, the camera's side at -z), pivot at the ground centre; the meshes are
turned half a turn at the end, as building_kit.build() does, so the drawn front is the game's front (+Z). Every level
is drawn from the same layout (each piece has its own random stream), so the levels of detail line up.

Also writes Tools/Blender/haven_decor_manifest.json (sizes, triangles, budgets, suggested costs), previews in
Tools/Blender/decor_previews/ (game-pitch three-quarter view, top view, the three levels) and haven_decor.blend.

Run: blender -b --factory-startup --python Tools/Blender/haven_decor.py -- "<project root>"
         [--only "Log Bench,Wall Torch"] [--res 900] [--no-preview] [--no-blend]
"""
import json
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import (BIRCH, BOULDER, CLOTH, DYE_BLUE, DYE_OCHRE, HAY, HIDE, IRON, LINEN, PLANKS, SLOTS,  # noqa: E402
                          SOIL, STRUCTURES, TILE, TROLLSTONE, WOOD, at, barrel, boulder, box, face, log, materials)
from prop_bake import ROOT, args, collection, stage  # noqa: E402

BLENDER_DIR = os.path.join(ROOT, "Tools", "Blender")
PREVIEWS = os.path.join(BLENDER_DIR, "decor_previews")
MANIFEST = os.path.join(BLENDER_DIR, "haven_decor_manifest.json")

# Detail per level. The kit's own LEVELS drop field stones at LOD2 (div 0: the masonry behind them stands in), and a
# decoration has no masonry, so its stones keep one subdivision.
LEVELS = [dict(sides=8, rings=1, div=2, sod=0.5, small=True, lod=0),
          dict(sides=6, rings=1, div=1, sod=0.75, small=True, lod=1),
          dict(sides=4, rings=1, div=1, sod=9.0, small=False, lod=2)]

M = Matrix(((-1.0, 0.0, 0.0), (0.0, 0.0, -1.0), (0.0, 1.0, 0.0)))  # design -> Blender, as at()
MI = M.inverted()
HALF_TURN = Matrix.Rotation(math.pi, 4, 'Z')
TAU = math.tau


def V(*p):
    return Vector(p[0] if len(p) == 1 else p)


def R(seed, i=0):
    """A piece's own random stream: the same at every level, whatever the levels skip."""
    return random.Random(seed * 7919 + i * 104729 + 17)


def pick(level, *values):
    """values[0] at LOD0, [1] at LOD1, [2] at LOD2 (the last repeats)."""
    return values[min(level["lod"], len(values) - 1)]


def rot(axis, degrees):
    """A rotation about a design axis ('X', 'Y' up, 'Z')."""
    return Matrix.Rotation(math.radians(degrees), 3, axis)


# ----- Faces ------------------------------------------------------------------------------------------------------------

def newell(cos):
    n = Vector()
    for a, b in zip(cos, cos[1:] + cos[:1]):
        n.x += (a.y - b.y) * (a.z + b.z)
        n.y += (a.z - b.z) * (a.x + b.x)
        n.z += (a.x - b.x) * (a.y + b.y)
    return n


def poly(bm, verts, material, uvs, smooth, want):
    """A face of BMVerts, wound so its normal points along `want` (a design-space direction)."""
    if newell([v.co for v in verts]).dot(M @ V(want)) < 0:
        verts, uvs = verts[::-1], uvs[::-1]
    return face(bm, verts, material, uvs, smooth)


def new(bm, p):
    return bm.verts.new(at(*p))


def turned(bm, pivot, rotation, draw, *a, **k):
    """Draws a part with draw(bm, ...) and turns what it made by a design-space rotation about `pivot` (a barrel on
    its side, a leaning wheel, a tilted board)."""
    before = len(bm.verts)
    draw(bm, *a, **k)
    bm.verts.ensure_lookup_table()
    turn = M @ rotation @ MI
    p = at(*pivot)
    for v in bm.verts[before:]:
        v.co = p + turn @ (v.co - p)


def moved(bm, offset, draw, *a, **k):
    before = len(bm.verts)
    draw(bm, *a, **k)
    bm.verts.ensure_lookup_table()
    d = M @ V(offset)
    for v in bm.verts[before:]:
        v.co += d


# ----- Parts ------------------------------------------------------------------------------------------------------------

def frames(points, closed):
    """A tangent, side and up at each point of a path, carried along it without twisting."""
    n = len(points)
    tangents = []
    for i in range(n):
        if closed:
            t = points[(i + 1) % n] - points[i - 1]
        else:
            t = points[min(i + 1, n - 1)] - points[max(i - 1, 0)]
        tangents.append(t.normalized())
    ref = Vector((0, 1, 0)) if abs(tangents[0].y) < 0.9 else Vector((0, 0, 1))
    side = ref.cross(tangents[0]).normalized()
    out = []
    for t in tangents:
        side = side - t * side.dot(t)
        side = side.normalized() if side.length > 1e-6 else t.orthogonal().normalized()
        out.append((t, side, t.cross(side)))
    return out


def rod(bm, points, radii, sides, material, caps=(True, True), closed=False, smooth=True, mats=None, twist=0.0,
        phase=0.0, dome=0.0, tile=None, cap_material=None):
    """A tube through design points, `radii` one or one a point; mats: a material for each side (a wheel rim with its
    iron tyre outside). Caps are flat ngons, or fans pushed out by `dome`."""
    pts = [V(p) for p in points]
    n = len(pts)
    radii = radii if isinstance(radii, (list, tuple)) else [radii] * n
    tile = tile or TILE[material]
    fr = frames(pts, closed)
    rings = []
    for i, (c, (t, s, u)) in enumerate(zip(pts, fr)):
        ring = []
        for k in range(sides):
            a = phase + TAU * k / sides + twist * i
            ring.append(new(bm, c + (s * math.cos(a) + u * math.sin(a)) * radii[i]))
        rings.append(ring)
    mean_r = sum(radii) / n
    wraps = max(1, round(TAU * mean_r / tile))
    along = [0.0]
    for a, b in zip(pts, pts[1:]):
        along.append(along[-1] + (b - a).length)
    segments = n if closed else n - 1
    for i in range(segments):
        j = (i + 1) % n
        vj = along[i] + (pts[j] - pts[i]).length
        for k in range(sides):
            m = (k + 1) % sides
            quad = [rings[i][k], rings[i][m], rings[j][m], rings[j][k]]
            mid = (pts[i] + pts[j]) / 2
            want = sum((MI @ v.co for v in quad), Vector()) / 4 - MI @ at(*mid)
            u0, u1 = (wraps * k / sides, wraps * (k + 1) / sides) if smooth else (k * 0.2, (k + 1) * 0.2)
            uv = [(u0, along[i] / tile), (u1, along[i] / tile), (u1, vj / tile), (u0, vj / tile)]
            poly(bm, quad, mats[k] if mats else material, uv, smooth, want)
    if closed:
        return
    for end, (i, sign) in enumerate(((0, -1.0), (n - 1, 1.0))):
        if not caps[end]:
            continue
        t, s, u = fr[i]
        rim = [new(bm, MI @ v.co) for v in rings[i]]
        mat = cap_material if cap_material is not None else material
        ctile = TILE[mat]
        uv = [((MI @ v.co - pts[i]).dot(s) / ctile + 0.5, (MI @ v.co - pts[i]).dot(u) / ctile + 0.5) for v in rim]
        if dome:
            centre = new(bm, pts[i] + t * sign * dome)
            for k in range(sides):
                m = (k + 1) % sides
                poly(bm, [rim[k], rim[m], centre], mat, [uv[k], uv[m], (0.5, 0.5)], True, t * sign)
        else:
            poly(bm, rim, mat, uv, False, t * sign)


def lathe(bm, centre, profile, sides, material, mats=None, phase=0.0, smooth=True, cap=False, planar=False):
    """A turned shape round a vertical axis at centre: profile [(radius, height), ...] from the bottom up (it may fold
    back down inside, as a bowl does); a radius of 0 closes it to a point. mats: a material for each band; planar:
    the texture laid from above (a low mound), not wrapped round."""
    cx, cy, cz = centre
    tile = TILE[material]
    rings = []
    for r, y in profile:
        if r < 1e-6:
            rings.append([new(bm, (cx, cy + y, cz))] * sides)
        else:
            rings.append([new(bm, (cx + r * math.cos(phase + TAU * k / sides), cy + y, cz + r * math.sin(phase + TAU * k / sides)))
                          for k in range(sides)])
    for i in range(len(profile) - 1):
        (r0, y0), (r1, y1) = profile[i], profile[i + 1]
        dr, dy = r1 - r0, y1 - y0
        mat = mats[i] if mats else material
        for k in range(sides):
            m = (k + 1) % sides
            a = phase + TAU * (k + 0.5) / sides
            want = V(math.cos(a) * dy, -dr, math.sin(a) * dy)
            verts = [rings[i][k], rings[i][m], rings[i + 1][m], rings[i + 1][k]]
            uv = [(k / sides * 2, y0 / tile * 3), ((k + 1) / sides * 2, y0 / tile * 3),
                  ((k + 1) / sides * 2, y1 / tile * 3), (k / sides * 2, y1 / tile * 3)]
            if planar:
                uv = [((MI @ v.co).x / tile, (MI @ v.co).z / tile) for v in verts]
            if r0 < 1e-6:
                verts, uv = verts[1:], uv[1:]
            elif r1 < 1e-6:
                verts, uv = verts[:3], uv[:3]
            poly(bm, verts, mat, uv, smooth, want)
    if cap and profile[-1][0] > 1e-6:
        rim = [new(bm, MI @ v.co) for v in rings[-1]]
        poly(bm, rim, material, [((MI @ v.co).x / tile, (MI @ v.co).z / tile) for v in rim], False, (0, 1, 0))


def sector(bm, start, end, radius, a0, a1, segs, bark=WOOD, split=PLANKS, ends=WOOD, ref=(0, 1, 0)):
    """A split piece of log from start to end: its section the slice of a circle from angle a0 to a1 round the axis
    (pi: a half log; ~1.8 rad: a split billet), the bark round the arc, the split faces flat, end grain at the ends."""
    a, b = V(start), V(end)
    t = (b - a).normalized()
    up = V(ref) - t * V(ref).dot(t)
    up = up.normalized() if up.length > 1e-6 else t.orthogonal().normalized()
    side = up.cross(t).normalized()

    def d(angle):
        return side * math.cos(angle) + up * math.sin(angle)

    half = abs(a1 - a0) >= math.pi - 1e-4  # the flat face a chord (a half log, or less than half sawn off)
    segs = max(segs, 2) if half else segs  # a chord's end needs three corners
    angles = [a0 + (a1 - a0) * k / segs for k in range(segs + 1)]
    length = (b - a).length
    rows = [[new(bm, c + d(g) * radius) for g in angles] for c in (a, b)]
    tile = TILE[bark]
    for k in range(segs):
        want = d((angles[k] + angles[k + 1]) / 2)
        s0, s1 = radius * (angles[k] - a0) / tile, radius * (angles[k + 1] - a0) / tile
        poly(bm, [rows[0][k], rows[0][k + 1], rows[1][k + 1], rows[1][k]], bark,
             [(s0, 0), (s1, 0), (s1, length / tile), (s0, length / tile)], True, want)
    st = TILE[split]
    if half:
        quad = [new(bm, a + d(a0) * radius), new(bm, a + d(a1) * radius), new(bm, b + d(a1) * radius), new(bm, b + d(a0) * radius)]
        poly(bm, quad, split, [(0, 0), (2 * radius / st, 0), (2 * radius / st, length / st), (0, length / st)], False, -d((a0 + a1) / 2))
    else:
        for g, turn in ((a0, -math.pi / 2), (a1, math.pi / 2)):
            quad = [new(bm, a), new(bm, a + d(g) * radius), new(bm, b + d(g) * radius), new(bm, b)]
            poly(bm, quad, split, [(0, 0), (radius / st, 0), (radius / st, length / st), (0, length / st)], False, d(g + turn))
    et = TILE[ends]
    for c, sign in ((a, -1.0), (b, 1.0)):
        ring = ([] if half else [c]) + [c + d(g) * radius for g in angles]
        verts = [new(bm, p) for p in ring]
        uv = [((p - c).dot(side) / et + 0.5, (p - c).dot(up) / et + 0.5) for p in ring]
        poly(bm, verts, ends, uv, False, t * sign)


def prism(bm, outline, origin, U, V_, depth, material, cap_material=None):
    """A board cut to a convex outline [(u, v), ...] in the plane through origin spanned by U and V_ (design vectors),
    `depth` thick along U x V_."""
    o, U, V_ = V(origin), V(U).normalized(), V(V_).normalized()
    N = U.cross(V_).normalized()
    tile = TILE[material]
    pts = [o + U * u + V_ * v for u, v in outline]
    back = [p + N * depth for p in pts]
    area = sum(u0 * v1 - u1 * v0 for (u0, v0), (u1, v1) in zip(outline, outline[1:] + outline[:1]))
    turn = 1.0 if area > 0 else -1.0  # outward edge normals: (dv, -du) for a counter-clockwise outline
    for ring, want in ((pts, -N), (back, N)):
        mat = cap_material if cap_material is not None else material
        poly(bm, [new(bm, p) for p in ring], mat, [(u / TILE[mat], v / TILE[mat]) for u, v in outline], False, want)
    s = 0.0
    for i in range(len(pts)):
        j = (i + 1) % len(pts)
        edge = (pts[j] - pts[i]).length
        (u0, v0), (u1, v1) = outline[i], outline[j]
        want = (U * (v1 - v0) - V_ * (u1 - u0)) * turn
        poly(bm, [new(bm, pts[i]), new(bm, pts[j]), new(bm, back[j]), new(bm, back[i])], material,
             [(s / tile, 0), ((s + edge) / tile, 0), ((s + edge) / tile, depth / tile), (s / tile, depth / tile)], False, want)
        s += edge


def ribbon(bm, points, normals, width, material, lift=0.004):
    """A flat strip along points (design) lying on a surface whose normal at each point is in `normals`."""
    pts, ns = [V(p) for p in points], [V(n).normalized() for n in normals]
    tile = TILE[material]
    left, right = [], []
    for i, (p, n) in enumerate(zip(pts, ns)):
        t = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        w = n.cross(t).normalized() * (width / 2)
        left.append(new(bm, p + w + n * lift))
        right.append(new(bm, p - w + n * lift))
    s = 0.0
    for i in range(len(pts) - 1):
        e = (pts[i + 1] - pts[i]).length
        poly(bm, [left[i], right[i], right[i + 1], left[i + 1]], material,
             [(0, s / tile), (width / tile, s / tile), (width / tile, (s + e) / tile), (0, (s + e) / tile)], True,
             ns[i] + ns[i + 1])
        s += e


def rosette(bm, centre, radius, height, leaves, material, rng, inner=0.42):
    """A low clump of leaves seen from above: a star-shaped dome, its points the leaf tips."""
    cx, cy, cz = centre
    tile = TILE[material]
    top = new(bm, (cx, cy + height, cz))
    spin = rng.uniform(0, TAU)
    tips, notches = [], []
    for k in range(leaves):
        a = spin + TAU * k / leaves
        r = radius * rng.uniform(0.82, 1.12)
        tips.append(new(bm, (cx + r * math.cos(a), cy + height * 0.2, cz + r * math.sin(a))))
        b = a + math.pi / leaves
        notches.append(new(bm, (cx + radius * inner * math.cos(b), cy + height * 0.62, cz + radius * inner * math.sin(b))))
    for k in range(leaves):
        for tri in ((top, tips[k], notches[k]), (top, notches[k], tips[(k + 1) % leaves])):
            poly(bm, list(tri), material, [((MI @ v.co).x / tile, (MI @ v.co).z / tile) for v in tri], True, (0, 1, 0))


def flower(bm, centre, radius, material, petals=5, cup=0.012, tilt=None):
    """A flower head facing the sky: a shallow cup of `petals` points."""
    cx, cy, cz = centre
    tilt = tilt or (0.0, 0.0)
    mid = new(bm, (cx, cy - cup * 0.3, cz))
    rim = []
    for k in range(petals):
        a = TAU * k / petals
        x, z = radius * math.cos(a), radius * math.sin(a)
        rim.append(new(bm, (cx + x, cy + cup + x * tilt[0] + z * tilt[1], cz + z)))
    if petals == 3:
        poly(bm, rim, material, [(0, 0), (1, 0), (0.5, 1)], True, (0, 1, 0))
        return
    for k in range(petals):
        tri = [mid, rim[k], rim[(k + 1) % petals]]
        poly(bm, tri, material, [(0.5, 0.5), (0, 0), (1, 0)], True, (0, 1, 0))


# ----- Assets -----------------------------------------------------------------------------------------------------------
# ASSETS-BEGIN

def log_bench(bm, level):
    """A split half log, flat side up, on two stumps, pegged through into them."""
    seat_y, length, radius, cut = 0.37, 1.7, 0.21, 0.1  # the log's axis; its top sawn flat `cut` above it
    for i, x in enumerate((-0.55, 0.55)):
        rng = R(1, i)
        x, z, top, spin = x + rng.uniform(-0.03, 0.03), rng.uniform(-0.03, 0.03), seat_y - 0.17 + rng.uniform(-0.01, 0.01), rng.uniform(0, TAU)
        rod(bm, [(x, 0.0, z), (x, 0.1, z), (x, top, z)], [0.215, 0.178, 0.166], pick(level, 10, 7, 5), WOOD, caps=(False, True), phase=spin)
        if level["lod"] == 0:
            rod(bm, [(x * 0.98, seat_y, z * 0.5), (x * 0.98, seat_y + cut + 0.012, z * 0.5)], 0.024, 6, WOOD, caps=(False, True))
    yaw = R(1, 5).uniform(-2.5, 2.5)
    a = math.asin(cut / radius)
    turned(bm, (0.0, seat_y, 0.0), rot('Y', yaw), sector, (-length / 2, seat_y, 0.0), (length / 2, seat_y, 0.0), radius,
           math.pi - a, TAU + a, pick(level, 8, 6, 4), bark=WOOD, split=WOOD, ends=WOOD)


def profile_board(bm, x0, x1, cols, bottom, top, z, thick, material):
    """A board standing in the x-y plane, its front face at z and `thick` towards +z, its top edge cut to top(x)."""
    xs = [x0 + (x1 - x0) * i / cols for i in range(cols + 1)]
    tile = TILE[material]
    for zf, want in ((z, (0, 0, -1)), (z + thick, (0, 0, 1))):
        low = [new(bm, (x, bottom, zf)) for x in xs]
        high = [new(bm, (x, top(x), zf)) for x in xs]
        for i in range(cols):
            a, b = xs[i], xs[i + 1]
            poly(bm, [low[i], low[i + 1], high[i + 1], high[i]], material,
                 [(a / tile, bottom / tile), (b / tile, bottom / tile), (b / tile, top(b) / tile), (a / tile, top(a) / tile)], False, want)
    for i in range(cols):
        a, b = xs[i], xs[i + 1]
        quad = [new(bm, (a, top(a), z)), new(bm, (b, top(b), z)), new(bm, (b, top(b), z + thick)), new(bm, (a, top(a), z + thick))]
        poly(bm, quad, material, [(a / tile, 0), (b / tile, 0), (b / tile, thick / tile), (a / tile, thick / tile)], False,
             (top(a) - top(b), b - a, 0.0))
    for x, sign in ((x0, -1.0), (x1, 1.0)):
        quad = [new(bm, (x, bottom, z)), new(bm, (x, bottom, z + thick)), new(bm, (x, top(x), z + thick)), new(bm, (x, top(x), z))]
        poly(bm, quad, material, [(0, 0), (thick / tile, 0), (thick / tile, 0.2 / tile), (0, 0.2 / tile)], False, (sign, 0, 0))


def plank_bench(bm, level):
    """A bench of two planks on side frames whose back posts rise, leaning back, to a backrest board with a carved
    top edge (a crown in the middle, a row of lobes) and turned knobs."""
    seat_y, lean = 0.45, rot('X', 9)
    for z in (-0.1, 0.1):
        box(bm, (0.0, seat_y - 0.025, z), (1.6, 0.05, 0.19), PLANKS, grain='x')
    for sx in (-1, 1):
        x = sx * 0.62
        box(bm, (x, (seat_y - 0.05) / 2, -0.15), (0.065, seat_y - 0.05, 0.065), WOOD)
        box(bm, (x, seat_y - 0.08, 0.0), (0.055, 0.06, 0.36), WOOD, grain='z')
        turned(bm, (x, 0.0, 0.16), lean, box, (x, 0.5, 0.16), (0.065, 1.0, 0.065), WOOD)
        if level["small"]:
            turned(bm, (x, 0.0, 0.16), lean, lathe, (x, 1.0, 0.16), [(0.038, 0.0), (0.05, 0.03), (0.042, 0.06), (0.0, 0.08)],
                   pick(level, 8, 6), WOOD)
    box(bm, (0.0, 0.14, 0.0), (1.24, 0.05, 0.05), WOOD, grain='x')
    half = 0.74

    def top(x):
        u = x / half
        return 0.8 + 0.075 * (1.0 - u * u) + 0.024 * abs(math.sin(math.pi * x / 0.185))

    turned(bm, (0.0, 0.0, 0.16), lean, profile_board, -half, half, pick(level, 32, 16, 8), 0.64, top, 0.16 - 0.0325 - 0.035, 0.035, PLANKS)
    if level["small"]:
        turned(bm, (0.0, 0.0, 0.16), lean, box, (0.0, 0.57, 0.16 - 0.05), (1.36, 0.06, 0.03), PLANKS, grain='x')


def stool(bm, x, z, level, rng, height=0.44):
    """A three-legged stool: a thick round seat on splayed legs."""
    spin = rng.uniform(0, TAU)
    lathe(bm, (x, height - 0.055, z), [(0.145, 0.0), (0.16, 0.025), (0.155, 0.055)], pick(level, 10, 7, 5), WOOD, cap=True, phase=spin)
    for k in range(3):
        a = spin + TAU * (k + 0.5) / 3
        rod(bm, [(x + 0.15 * math.cos(a), 0.0, z + 0.15 * math.sin(a)), (x + 0.075 * math.cos(a), height - 0.05, z + 0.075 * math.sin(a))],
            0.022, pick(level, 5, 4, 3), WOOD, caps=(False, False))


def trestle_table(bm, level):
    """A table of three planks on two trestles, a stretcher between them, a stool at each side, a bowl and a
    tankard on the top."""
    top_y, length = 0.8, 1.9
    for k in range(3):
        rng = R(3, k)
        box(bm, (rng.uniform(-0.025, 0.025), top_y - 0.03, (k - 1) * 0.268), (length + rng.uniform(-0.05, 0.03), 0.06, 0.26), PLANKS, grain='x')
    sides = pick(level, 6, 5, 4)
    for sx in (-1, 1):
        x = sx * 0.66
        box(bm, (x, top_y - 0.1, 0.0), (0.09, 0.08, 0.72), WOOD, grain='z')
        for sz in (-1, 1):
            rod(bm, [(x, 0.0, sz * 0.36), (x, top_y - 0.13, sz * 0.09)], 0.04, sides, WOOD, caps=(False, False))
        rod(bm, [(x, 0.3, -0.3), (x, 0.3, 0.3)], 0.03, sides, WOOD)
    rod(bm, [(-0.76, 0.35, 0.0), (0.76, 0.35, 0.0)], 0.035, sides, WOOD)
    stool(bm, -0.42, -0.72, level, R(3, 10))
    stool(bm, 0.55, 0.74, level, R(3, 11))
    if level["small"]:
        lathe(bm, (0.38, top_y, -0.06), [(0.07, 0.0), (0.12, 0.035), (0.145, 0.075), (0.128, 0.075), (0.105, 0.045), (0.0, 0.035)],
              pick(level, 10, 7), WOOD)
        lathe(bm, (-0.52, top_y, 0.12), [(0.047, 0.0), (0.048, 0.02), (0.048, 0.035), (0.048, 0.1), (0.048, 0.12), (0.039, 0.12), (0.0, 0.1)],
              pick(level, 8, 6), WOOD, mats=[WOOD, IRON, WOOD, IRON, WOOD, WOOD])


def woodpile(bm, level):
    """Split firewood stacked end-on to the camera between two posts, on two bearers, under a cover of three
    slabs, bark up."""
    lod = level["lod"]
    half_x, depth, radius, rows = 0.84, 0.5, 0.125, 6
    for z in (-0.15, 0.15):
        rod(bm, [(-0.98, 0.055, z), (0.98, 0.055, z)], 0.055, pick(level, 6, 5, 4), WOOD, phase=0.3)
    for sx in (-1, 1):
        x = sx * (half_x + 0.1)
        rod(bm, [(x, 0.0, 0.0), (x - sx * 0.02, 1.3, 0.0)], 0.06, pick(level, 7, 5, 4), WOOD, caps=(False, True))
    # The stack's body, behind the billets' ends, so the gaps between them read as shadow (far off, at LOD2, it is
    # the stack, under its top row and cover).
    body = rows * radius * 1.25
    box(bm, (0.0, 0.11 + body / 2, 0.0), (half_x * 2 - 0.04, body, depth - (0.06 if lod == 2 else 0.22)), WOOD, grain='z')
    y = 0.11 + radius * 0.62
    for row in range(rows):
        x, k = -half_x + 0.08 + (0.05 if row % 2 else 0.0), 0
        while x < half_x - 0.07:
            rng = R(40 + row, k)
            # Halves and plump quarters, bark up, down or to a side, as split wood is stacked.
            spin = rng.choice((math.pi / 2, -math.pi / 2, 0.0, math.pi)) + rng.uniform(-0.45, 0.45)
            width, r = rng.choice((math.pi, 2.3, 1.95)), radius * rng.uniform(0.9, 1.08)
            z0, z1 = -depth / 2 + rng.uniform(-0.05, 0.03), depth / 2 + rng.uniform(-0.03, 0.04)
            dy = rng.uniform(-0.012, 0.012)
            if lod < 2 or row == rows - 1:
                pull = 2 * r * math.sin(width / 2) / (3 * width / 2)  # from the apex to the billet's middle
                cx, cy = x - math.cos(spin) * pull, y + dy - math.sin(spin) * pull
                sector(bm, (cx, cy, z0), (cx, cy, z1), r, spin - width / 2, spin + width / 2, pick(level, 3, 1, 1),
                       bark=PLANKS, split=WOOD, ends=WOOD)  # pale cut ends, the darker weathered scan as bark
            x += r * 1.55
            k += 1
        y += radius * 1.25
    # The cover: three slabs (the outsides of a log, sawn off) along the top, bark up, each a little higher towards the back so they show to the camera.
    for k, z in enumerate((-0.17, 0.0, 0.17)):
        rng = R(60, k)
        a = math.asin(0.1)
        cy = y - radius * 0.35 + 0.02 * k
        turned(bm, (0.0, cy, z), rot('Y', rng.uniform(-3, 3)), sector, (-half_x - 0.12, cy - 0.1 * 0.12, z), (half_x + 0.12, cy - 0.1 * 0.12, z),
               0.12, a, math.pi - a, pick(level, 5, 3, 2), bark=WOOD, split=PLANKS, ends=WOOD)


def crate(bm, centre, size, level):
    """A plank crate: its body, the boards round its lid and its corner posts, a brace across the front."""
    x, y0, z = centre
    sx, sy, sz = size
    box(bm, (x, y0 + sy / 2, z), (sx - 0.02, sy - 0.02, sz - 0.02), PLANKS, grain='x')
    if level["lod"] == 2:
        return
    t = 0.045
    for dz in (-1, 1):
        box(bm, (x, y0 + sy - t / 2, z + dz * (sz / 2 - t / 2)), (sx, t, t), WOOD, grain='x')
    for dx in (-1, 1):
        box(bm, (x + dx * (sx / 2 - t / 2), y0 + sy - t / 2, z), (t, t, sz - 2 * t), WOOD, grain='z')
        for dz in (-1, 1):
            box(bm, (x + dx * (sx / 2 - t / 2), y0 + (sy - t) / 2, z + dz * (sz / 2 - t / 2)), (t, sy - t, t), WOOD)
    if level["lod"] == 0:
        w, h = sx - 2 * t, sy - 2 * t
        turned(bm, (x, y0 + sy / 2, z - sz / 2), rot('Z', math.degrees(math.atan2(h, w))), box,
               (x, y0 + sy / 2, z - sz / 2 - 0.006), (math.hypot(w, h), 0.05, 0.02), WOOD, grain='x')


def barrels_and_crates(bm, level):
    """Two lidded barrels, a crate with a smaller one on it, and a tied sack."""
    staves = dict(level, sides=pick(level, 12, 8, 6))
    barrel(bm, -0.42, 0.08, staves, height=0.82, radius=0.3, water=False)
    barrel(bm, 0.2, 0.34, staves, height=0.64, radius=0.24, water=False)
    turned(bm, (0.5, 0.0, -0.2), rot('Y', 14), crate, (0.5, 0.0, -0.2), (0.6, 0.46, 0.48), level)
    if level["small"]:
        turned(bm, (0.46, 0.0, -0.22), rot('Y', -9), crate, (0.46, 0.46, -0.22), (0.36, 0.26, 0.3), level)
    sx, sz = -0.12, -0.42
    turned(bm, (sx, 0.0, sz), rot('Z', -7), boulder, (sx, 0.29, sz), (0.4, 0.62, 0.32), dict(level, div=pick(level, 3, 2, 1)),
           R(5, 2), material=LINEN, yaw=0.3, flat=0.4)
    turned(bm, (sx, 0.0, sz), rot('Z', -7), lathe, (sx, 0.56, sz), [(0.07, 0.0), (0.042, 0.05), (0.058, 0.09), (0.0, 0.115)],
           pick(level, 7, 5, 4), LINEN)


def facet_phase(sides):
    """The phase that puts a flat facet of an n-sided rod or lathe straight to the front (-z)."""
    return {8: math.pi / 8, 6: 0.0}[sides]


def carved_post(bm, level):
    """A guardian post: an eight-sided shaft with two knot bands painted red, a broad head with a bold face carved on
    its front (a frowning brow painted ochre, inlaid iron eyes, a long nose, a drooping ochre moustache, a pointed beard running down onto
    the shaft), a cone cap whose wide brim is painted red, and field stones wedged round its foot."""
    lod = level["lod"]
    sides = pick(level, 8, 8, 6)
    ph = facet_phase(sides)
    rod(bm, [(0, 0, 0), (0, 0.3, 0), (0, 1.4, 0)], [0.175, 0.155, 0.145], sides, WOOD, caps=(False, False), phase=ph)
    rod(bm, [(0, 1.36, 0), (0, 1.5, 0), (0, 1.92, 0), (0, 1.98, 0)], [0.145, 0.225, 0.22, 0.2], sides, WOOD,
        caps=(False, False), phase=ph)
    lathe(bm, (0, 1.97, 0), [(0.2, 0.0), (0.265, 0.03), (0.26, 0.06), (0.13, 0.16), (0.0, 0.26)], sides, WOOD,
          mats=[CLOTH, CLOTH, WOOD, WOOD], phase=ph)
    band = pick(level, 16, 8, 8)
    for y in (0.62, 1.1):
        rod(bm, [(0, y - 0.07, 0), (0, y, 0), (0, y + 0.07, 0)], [0.145, 0.185, 0.145], band, CLOTH, caps=(False, False),
            twist=math.pi / band)
    front = -0.22 * math.cos(math.pi / 8)  # the head's front facet
    for sx in (-1, 1):  # the brow, frowning: two ridges meeting over the nose
        turned(bm, (sx * 0.065, 1.83, front), rot('Z', sx * 14), box, (sx * 0.065, 1.83, front - 0.03), (0.15, 0.055, 0.08), DYE_OCHRE, grain='x')
        box(bm, (sx * 0.062, 1.775, front - 0.006), (0.075, 0.042, 0.03), IRON)
    prism(bm, [(0.0, 1.83), (0.09, 1.66), (0.0, 1.64)], (-0.035, 0.0, front + 0.01), (0, 0, -1), (0, 1, 0), 0.07, WOOD)
    if lod < 2:
        for sx in (-1, 1):
            turned(bm, (sx * 0.05, 1.625, front), rot('Z', sx * -22), box, (sx * 0.06, 1.625, front - 0.03), (0.12, 0.035, 0.05), DYE_OCHRE, grain='x')
    prism(bm, [(-0.1, 1.62), (0.1, 1.62), (0.055, 1.38), (0.0, 1.28), (-0.055, 1.38)], (0.0, 0.0, front - 0.055), (1, 0, 0), (0, 1, 0),
          0.11, WOOD)
    stones = dict(level, div=pick(level, 2, 1, 1))
    for k, a in enumerate((0.35, 2.5, 4.3)):
        rng = R(6, k)
        w, h, d = rng.uniform(0.24, 0.3), rng.uniform(0.15, 0.2), rng.uniform(0.18, 0.22)
        boulder(bm, (0.25 * math.cos(a), h * 0.36, 0.25 * math.sin(a)), (w, h, d), stones, rng, yaw=a - math.pi / 2)


def flower_bed(bm, level):
    """A ring of field stones round a mound of dark earth, low clumps of herbs in it, flowering white, yellow, red,
    and a few blue spikes (lupins)."""
    stones = dict(level, div=pick(level, 2, 1, 1))
    count, ring = 11, 0.62
    for k in range(count):
        rng = R(7, k)
        a = TAU * k / count + rng.uniform(-0.08, 0.08)
        w, h, d = rng.uniform(0.27, 0.34), rng.uniform(0.15, 0.22), rng.uniform(0.2, 0.25)
        yaw = a - math.pi / 2 + rng.uniform(-0.25, 0.25)
        boulder(bm, (ring * math.cos(a), h * 0.36, ring * math.sin(a)), (w, h, d), stones, rng, yaw=yaw)
    soil = [(0.6, 0.0), (0.56, 0.085), (0.36, 0.125), (0.0, 0.14)]
    lathe(bm, (0.0, 0.0, 0.0), soil, pick(level, 14, 10, 7), SOIL, planar=True)

    def ground(r):
        for (r0, y0), (r1, y1) in zip(soil, soil[1:]):
            if r1 <= r <= r0:
                return y0 + (y1 - y0) * (r0 - r) / (r0 - r1)
        return soil[-1][1]

    colours = [LINEN, DYE_OCHRE, CLOTH, DYE_BLUE, LINEN, DYE_OCHRE, CLOTH]
    for k, colour in enumerate(colours):
        place = R(8, k)
        rr, a = 0.43 * math.sqrt((k + 0.5) / len(colours)), k * 2.39996 + 0.4
        x, z = rr * math.cos(a), rr * math.sin(a)
        size, height = place.uniform(0.12, 0.17), place.uniform(0.11, 0.18)
        base = ground(rr) - 0.02
        blooms = [(place.uniform(0, TAU), place.uniform(0.02, size * 0.7), place.uniform(0.75, 1.05), place.uniform(0.045, 0.062),
                   (place.uniform(-0.3, 0.3), place.uniform(-0.3, 0.3))) for _ in range(4)]
        rosette(bm, (x, base, z), size, height, pick(level, 7, 5, 4), TROLLSTONE, R(9, k))
        for fa, fr, fh, radius, tilt in blooms:
            at_x, at_z = x + fr * math.cos(fa), z + fr * math.sin(fa)
            if colour == DYE_BLUE:  # lupins: spikes standing out of the clump
                lathe(bm, (at_x, base + height * 0.5, at_z), [(0.032, 0.0), (0.029, 0.1), (0.0, 0.23 * fh)], pick(level, 5, 4, 3), colour)
            else:
                flower(bm, (at_x, base + height * fh + 0.02, at_z), radius, colour, petals=pick(level, 5, 5, 3), tilt=tilt)


def arrow(bm, y, paint, level):
    """A plank arrow along +x from the post, its head painted."""
    box(bm, (0.29, y, 0.0), (0.48, 0.15, 0.035), PLANKS, grain='x')
    prism(bm, [(0.53, y - 0.1), (0.69, y), (0.53, y + 0.1)], (0.0, 0.0, -0.0175), (1, 0, 0), (0, 1, 0), 0.035, paint)


def signpost(bm, level):
    """A post with a little pyramid roof and three plank arrows pointing three ways, their heads painted red, ochre
    and blue, field stones round its foot."""
    rod(bm, [(0, 0, 0), (0, 2.1, 0)], [0.075, 0.065], pick(level, 8, 6, 5), WOOD, caps=(False, True))
    lathe(bm, (0, 2.1, 0), [(0.105, 0.0), (0.105, 0.025), (0.0, 0.1)], 4, PLANKS, phase=math.pi / 4)
    # Their faces kept towards the front (the camera): an arrow pointing to the back would be seen edge-on.
    for y, yaw, paint in ((1.86, 20, CLOTH), (1.62, 160, DYE_OCHRE), (1.38, -30, DYE_BLUE)):
        turned(bm, (0, y, 0), rot('Y', yaw), arrow, y, paint, level)
    stones = dict(level, div=pick(level, 2, 1, 1))
    for k, a in enumerate((0.3, 1.9, 3.4, 4.8)):
        rng = R(11, k)
        w, h, d = rng.uniform(0.2, 0.26), rng.uniform(0.13, 0.17), rng.uniform(0.16, 0.2)
        if level["lod"] == 2 and k % 2:
            continue
        boulder(bm, (0.17 * math.cos(a), h * 0.36, 0.17 * math.sin(a)), (w, h, d), stones, rng, yaw=a - math.pi / 2)


def resample(points, spacing):
    pts = [V(p) for p in points]
    out = [pts[0]]
    carry = 0.0
    for a, b in zip(pts, pts[1:]):
        seg = (b - a).length
        s = spacing - carry
        while s < seg:
            out.append(a.lerp(b, s / seg))
            s += spacing
        carry = seg - (s - spacing)
    if (out[-1] - pts[-1]).length > spacing * 0.3:
        out.append(pts[-1])
    else:
        out[-1] = pts[-1]
    return out


# Runes as strokes (along, across the band), the stave across it: shapes after the elder futhark, not words.
RUNES = [
    [(0, -0.035, 0, 0.035), (0, 0.0, 0.026, 0.024), (0, -0.02, 0.026, 0.004)],
    [(0, -0.035, 0, 0.035)],
    [(0, -0.035, 0, 0.035), (0, 0.035, 0.022, 0.012), (0, 0.035, -0.022, 0.012)],
    [(0, -0.035, 0, 0.035), (-0.018, 0.012, 0.018, -0.012)],
    [(0, -0.035, 0, 0.035), (0, 0.035, 0.026, 0.01), (0.026, 0.01, 0.026, -0.035)],
    [(0, -0.035, 0, 0.035), (0, 0.035, 0.022, 0.012), (0.022, 0.012, 0, 0.0), (0, 0.0, 0.024, -0.035)],
    [(0, -0.035, 0, 0.035), (0, 0.0, -0.02, 0.025), (0, 0.0, 0.02, 0.025)],
]


def rune_stone(bm, level):
    """A standing stone, tapering, with a band painted red running up one side, over the top and down the other (a
    serpent's body, its head at the left foot), runes cut through the paint along it, small stones at its foot."""
    lod = level["lod"]
    before = len(bm.verts)
    boulder(bm, (0.0, 0.66, 0.0), (0.8, 1.5, 0.36), dict(level, div=pick(level, 4, 3, 2)), R(12, 0), yaw=math.pi,
            material=BOULDER, flat=0.55)
    bm.verts.ensure_lookup_table()
    for v in bm.verts[before:]:
        d = MI @ v.co
        t = max(0.0, min(1.0, (d.y + 0.09) / 1.5))
        v.co = at(d.x * (1 - 0.22 * t), d.y, d.z * (1 - 0.15 * t))
    bm.normal_update()  # the tree's hit normals are the faces' stored ones
    tree = BVHTree.FromBMesh(bm)

    def onto(x, y):
        loc, normal, _i, _d = tree.ray_cast(at(x, y, -1.0), M @ Vector((0.0, 0.0, 1.0)))
        return MI @ loc, (MI @ normal).normalized()

    path = [(-0.25, 0.12), (-0.25, 0.8)] + [(-0.25 * math.cos(math.pi * k / 12), 0.8 + 0.36 * math.sin(math.pi * k / 12))
                                             for k in range(1, 12)] + [(0.25, 0.8), (0.25, 0.12)]
    line = resample([(x, y, 0.0) for x, y in path], pick(level, 0.06, 0.11, 0.2))
    hits = [onto(p.x, p.y) for p in line]
    ribbon(bm, [h[0] for h in hits], [h[1] for h in hits], 0.1, CLOTH, lift=0.006)
    head = [onto(-0.25, y) for y in (0.06, 0.1, 0.15)]
    if lod < 2:  # the serpent's head, wider than its body, at the left foot
        ribbon(bm, [h[0] for h in head], [h[1] for h in head], 0.15, CLOTH, lift=0.007)
        flat_line = resample([(x, y, 0.0) for x, y in path], 0.13)[1:-1]
        for k, p in enumerate(flat_line):
            i = min(range(len(line) - 1), key=lambda j: (line[j] - p).length)
            t = (line[i + 1] - line[i]).normalized()
            across = Vector((-t.y, t.x, 0.0))
            for stroke in RUNES[(k * 3) % len(RUNES)][:pick(level, 9, 1)]:
                a0, b0, a1, b1 = stroke
                ends = [onto(*(p + t * a + across * b).xy) for a, b in ((a0, b0), (a1, b1))]
                ribbon(bm, [e[0] for e in ends], [e[1] for e in ends], 0.016, BOULDER, lift=0.011)
    stones = dict(level, div=pick(level, 2, 1, 1))
    for k, (x, z) in enumerate(((-0.42, -0.12), (0.4, -0.16), (0.12, 0.3))):
        rng = R(12, k + 1)
        w, h, d = rng.uniform(0.2, 0.28), rng.uniform(0.12, 0.17), rng.uniform(0.16, 0.22)
        boulder(bm, (x, h * 0.35, z), (w, h, d), stones, rng)


def wheel(bm, centre, radius, level):
    """A cart wheel, its axle along x: a rim of felloes with an iron tyre (the rim's outer face), hub and spokes."""
    cx, cy, cz = centre
    n = pick(level, 16, 10, 8)
    rim = [(cx, cy + radius * math.cos(TAU * k / n), cz + radius * math.sin(TAU * k / n)) for k in range(n)]
    rod(bm, rim, 0.036, 4, PLANKS, closed=True, mats=[IRON, PLANKS, PLANKS, PLANKS], phase=math.pi / 4)
    rod(bm, [(cx - 0.09, cy, cz), (cx + 0.09, cy, cz)], 0.07, pick(level, 8, 6, 5), PLANKS)
    spokes = pick(level, 8, 6, 4)
    for k in range(spokes):
        a = TAU * (k + 0.5) / spokes
        rod(bm, [(cx, cy + 0.06 * math.cos(a), cz + 0.06 * math.sin(a)), (cx, cy + (radius - 0.03) * math.cos(a), cz + (radius - 0.03) * math.sin(a))],
            0.018, pick(level, 4, 3, 3), PLANKS, caps=(False, False))


def hay_bales(bm, level):
    """Three bales, two on the ground and one across them, each tied twice with twine, on a mat of spilt hay, and an
    old cart wheel leaning on the stack's right end."""
    lod = level["lod"]
    size = (0.92, 0.47, 0.5)
    for i, (c, yaw) in enumerate((((-0.48, 0.235, 0.0), 0.06), ((0.47, 0.235, 0.05), -0.08), ((0.0, 0.69, 0.02), 0.12))):
        boulder(bm, c, size, dict(level, div=pick(level, 3, 2, 1)), R(10, i), yaw=yaw, material=HAY, flat=0.78)
        if lod == 0:
            turn = rot('Y', -math.degrees(yaw))
            hy, hz = size[1] / 2 + 0.012, size[2] / 2 + 0.012
            loop = [(-hz, -hy * 0.7), (-hz, hy * 0.55), (-hz * 0.7, hy), (hz * 0.7, hy), (hz, hy * 0.55), (hz, -hy * 0.7)]
            for lx in (-0.24, 0.24):
                pts = [V(c) + turn @ V(lx, y, z) for z, y in loop]
                ns = [turn @ V(0.0, y / hy, z / hz) for z, y in loop]
                ribbon(bm, pts, ns, 0.03, LINEN, lift=0.0)
    if lod < 2:
        rng = R(10, 9)
        n = 14
        mid = new(bm, (0.05, 0.012, 0.04))
        rim = [new(bm, (0.05 + 1.15 * r * math.cos(TAU * k / n), 0.008, 0.04 + 0.55 * r * math.sin(TAU * k / n)))
               for k, r in enumerate(rng.uniform(0.82, 1.12) for _ in range(n))]
        for k in range(n):
            tri = [mid, rim[k], rim[(k + 1) % n]]
            poly(bm, tri, HAY, [((MI @ v.co).x, (MI @ v.co).z) for v in tri], True, (0, 1, 0))
    turned(bm, (1.1, 0.0, -0.34), rot('Y', 58) @ rot('Z', 14), wheel, (1.1, 0.43, -0.34), 0.43, level)


# The wall torch, drawn against a wall in the plane z = 0 (the torch stands out towards the camera, -z), its back
# plate's centre at the origin: the handle's butt sits in a hook at the plate's foot, the handle leans out about 20
# degrees through a ring on an arm, and the wrapped head is charred at the top.
TORCH_BUTT = Vector((0.0, -0.275, -0.04))
TORCH_DIR = Vector((0.0, 0.33, -0.12)).normalized()
TORCH_LENGTH = 0.6
TORCH_DOME = 0.022
TORCH_RING_T = (0.05 - TORCH_BUTT.y) / TORCH_DIR.y


def torch_at(t):
    return TORCH_BUTT + TORCH_DIR * t


FLAME = torch_at(TORCH_LENGTH) + TORCH_DIR * TORCH_DOME


def wall_torch(bm, level):
    """Sized for its size on screen (ScreenBudget: ~140 triangles for a 0.6 m thing at the default view)."""
    lod = level["lod"]
    # Back plate: a strap with a pointed head, nailed flat to the wall.
    prism(bm, [(-0.04, -0.31), (0.04, -0.31), (0.04, 0.1), (0.0, 0.14), (-0.04, 0.1)], (0.0, 0.0, -0.012),
          (1, 0, 0), (0, 1, 0), 0.012, IRON)
    ring_c = torch_at(TORCH_RING_T)
    ring_r = 0.031
    _t, side, up = frames([torch_at(0.0), torch_at(1.0)], False)[0]
    # The arm out to the ring (far off, to the handle itself), and a brace under it.
    arm_end = Vector((0.0, ring_c.y, ring_c.z + (ring_r if lod < 2 else 0.02)))
    rod(bm, [(0.0, ring_c.y, -0.012), arm_end], 0.008, pick(level, 4, 4, 3), IRON, caps=(False, False), phase=math.pi / 4)
    if lod == 0:
        rod(bm, [(0.0, -0.08, -0.012), (0.0, ring_c.y - 0.008, (arm_end.z - 0.012) * 0.75)], 0.0065, 3, IRON, caps=(False, False))
        rod(bm, [(0.0, -0.295, -0.012), (0.0, -0.3, -0.064), (0.0, -0.262, -0.07)], 0.007, 3, IRON, caps=(False, False))
    if lod < 2:  # the ring round the handle
        n = pick(level, 6, 4)
        rod(bm, [ring_c + (side * math.cos(TAU * k / n) + up * math.sin(TAU * k / n)) * ring_r for k in range(n)],
            0.0065, 3, IRON, closed=True, smooth=True)
    # Handle, then the head: a bundle of hide strips wound on, the top of it soaked pitch, charred.
    rod(bm, [torch_at(0.0), torch_at(0.43)], 0.02, pick(level, 5, 4, 3), WOOD, caps=(True, False))
    head = pick(level, 7, 5, 4)
    wrap = [torch_at(0.41), torch_at(0.455), torch_at(0.515)] if lod < 2 else [torch_at(0.41), torch_at(0.515)]
    rod(bm, wrap, [0.025, 0.043, 0.047][-len(wrap):] if lod < 2 else [0.03, 0.047], head, HIDE, caps=(False, False),
        twist=0.45 if lod < 2 else 0.0)
    rod(bm, [torch_at(0.515), torch_at(TORCH_LENGTH)], [0.047, 0.04], head, SOIL, caps=(False, True), dome=TORCH_DOME,
        phase=0.9)


ASSETS = [
    dict(name="Log Bench", draw=log_bench,
         cost={"OakWood": 4}, comfort=1,
         notes="A log sawn flat on top, on two stumps. Seat height 0.47 m."),
    dict(name="Plank Bench", draw=plank_bench,
         cost={"OakPlank": 4, "OakWood": 2}, comfort=1,
         notes="Seat height 0.45 m; the backrest leans back 9 degrees, its back side is +Z's opposite (the bench faces +Z)."),
    dict(name="Trestle Table", draw=trestle_table,
         cost={"OakPlank": 6, "OakWood": 4}, comfort=2,
         notes="Table top 0.8 m; a stool at each long side (one in front, one behind), a bowl and a tankard on the top."),
    dict(name="Woodpile", draw=woodpile,
         cost={"OakWood": 8}, comfort=1,
         notes="Split billets end-on to the front between two posts, under three bark slabs."),
    dict(name="Barrels and Crates", draw=barrels_and_crates,
         cost={"OakPlank": 6, "IronIngot": 1, "Linen": 1}, comfort=0,
         notes="Set dressing; could double as a small store if wanted."),
    dict(name="Carved Post", draw=carved_post,
         cost={"OakWood": 6, "Berries": 2}, comfort=1,
         collider=dict(type="capsule", center=[0.0, 1.12, 0.0], radius=0.22, height=2.23, direction="y"),
         notes="2.2 m guardian post; the face is on the front (+Z). Berries stand for the red paint (optional)."),
    dict(name="Flower Bed", draw=flower_bed,
         cost={"GreyStone": 6, "HealingHerb": 2}, comfort=2,
         collider=dict(type="box", center=[0.0, 0.1, 0.0], size=[1.45, 0.2, 1.45]),
         notes="Low (0.3 m): a walk-over collider or none may suit it better than a solid one."),
    dict(name="Signpost", draw=signpost,
         cost={"OakWood": 2, "OakPlank": 3}, comfort=0,
         collider=dict(type="capsule", center=[0.0, 1.05, 0.0], radius=0.2, height=2.2, direction="y"),
         notes="No text: the arrow heads are painted red, ochre and blue; arrows at 1.86, 1.62, 1.38 m."),
    dict(name="Rune Stone", draw=rune_stone,
         cost={"GreyStone": 10, "Berries": 2}, comfort=2,
         notes="1.4 m; the painted band and runes are on the front (+Z)."),
    dict(name="Hay Bales", draw=hay_bales,
         cost={"Barley": 6, "NettleCord": 1, "OakPlank": 2}, comfort=0,
         notes="Three bales and a leaning cart wheel on a mat of spilt hay."),
    dict(name="Wall Torch", draw=wall_torch, marks={"Mount": (0.0, 0.0, 0.0), "Flame": tuple(FLAME)},
         cost={"Sticks": 2, "NettleFiber": 1, "IronIngot": 1}, comfort=0, collider=None,
         notes=("Origin = Mount (the back plate's centre, its back face on the wall); the torch stands out along +Z. "
                "The charred tip is its own slot (Kit Soil) so the game can swap it for an ember material when lit.")),
]

# ASSETS-END


# ----- Building them ----------------------------------------------------------------------------------------------------

def to_object(name, bm, coll, made):
    """The mesh with only the material slots it uses (named as the kit's, which the Unity builder maps)."""
    used = sorted({f.material_index for f in bm.faces})
    remap = {old: i for i, old in enumerate(used)}
    for f in bm.faces:
        f.material_index = remap[f.material_index]
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for old in used:
        mesh.materials.append(made[old])
    obj = bpy.data.objects.new(name, mesh)
    coll.objects.link(obj)
    return obj


def counts(obj):
    """Triangles, and vertices as an engine counts them (split where UVs or flat normals differ)."""
    me = obj.data
    triangles = sum(len(p.vertices) - 2 for p in me.polygons)
    uv = me.uv_layers.active.data
    keys = set()
    for p in me.polygons:
        flat = None if p.use_smooth else tuple(round(c, 3) for c in p.normal)
        for li in p.loop_indices:
            u = uv[li].uv
            keys.add((me.loops[li].vertex_index, round(u.x, 4), round(u.y, 4), flat))
    return triangles, len(keys)


def screen_budget(sx, sy, sz):
    """ScreenBudget.cs and ModelBudgetTests for these bounds (Unity x, y up, z): pixels at the default view, the
    triangles that draw 40 (the builder's aim) and 25 (the test's floor) pixels each, and the test's LOD0 cap."""
    pitch = math.radians(48.0)
    across, deep = max(sx, sz), min(sx, sz)
    area = 0.6 * 147.0 * 147.0 * across * (sy * math.cos(pitch) + deep * math.sin(pitch))
    size = max(sx, sy, sz)
    return dict(default_view_pixels=round(area), aim_40px=round(area / 20.0), floor_25px=round(area / 12.5),
                test_cap=2500 if size < 1 else 6000 if size < 3 else 8000)


def unity(b):
    """A point in the exported model's space as Unity reads it (Blender -Y forward, X mirrored)."""
    return [round(-b.x, 4), round(b.z, 4), round(-b.y, 4)]


def build_asset(spec, coll, made):
    name = spec["name"]
    levels = []
    for i, level in enumerate(LEVELS):
        bm = bmesh.new()
        spec["draw"](bm, level)
        bm.transform(HALF_TURN)
        bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
        levels.append(to_object(name if i == 0 else f"{name} LOD{i}", bm, coll, made))
    marks = []
    for mark, p in spec.get("marks", {}).items():
        empty = bpy.data.objects.new(mark, None)
        empty.empty_display_size = 0.05
        empty.location = HALF_TURN.to_3x3() @ at(*p)
        coll.objects.link(empty)
        marks.append(empty)
    folder = os.path.join(STRUCTURES, name)
    os.makedirs(folder, exist_ok=True)
    fbx = os.path.join(folder, f"{name}.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    for obj in levels + marks:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = levels[0]
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                             axis_up='Y', bake_space_transform=True,
                             object_types={'MESH', 'EMPTY'} if marks else {'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
    dims = levels[0].dimensions
    lo = Vector(min(v.co[a] for v in levels[0].data.vertices) for a in range(3))
    hi = Vector(max(v.co[a] for v in levels[0].data.vertices) for a in range(3))
    stats = [counts(obj) for obj in levels]
    entry = dict(
        folder=f"Assets/_Project/Art/Haven/Structures/{name}",
        fbx=f"Assets/_Project/Art/Haven/Structures/{name}/{name}.fbx",
        objects=[obj.name for obj in levels + marks],
        size_m=dict(x=round(dims.x, 3), z=round(dims.y, 3), y=round(dims.z, 3)),
        bounds_unity=dict(min=unity(Vector((hi.x, hi.y, lo.z))), max=unity(Vector((lo.x, lo.y, hi.z)))),
        triangles=[s[0] for s in stats],
        vertices=[s[1] for s in stats],
        vertices_per_triangle=round(stats[0][1] / max(1, stats[0][0]), 2),
        lod_share=[round(s[0] / max(1, stats[0][0]), 2) for s in stats],
        budget=screen_budget(dims.x, dims.z, dims.y),
        materials=[m.name for m in levels[0].data.materials],
    )
    entry.update({k: v for k, v in spec.items() if k not in ("name", "draw", "marks")})
    if "collider" not in spec:  # the bounds, as a box
        mn, mx = entry["bounds_unity"]["min"], entry["bounds_unity"]["max"]
        entry["collider"] = dict(type="box", center=[round((a + b) / 2, 3) for a, b in zip(mn, mx)],
                                 size=[round(b - a, 3) for a, b in zip(mn, mx)])
    if marks:
        entry["marks_unity"] = {e.name: unity(e.location) for e in marks}
    print("BUILT", name, entry["triangles"], "tris", entry["vertices"], "verts", entry["size_m"], entry["budget"])
    return levels, marks, entry


def check_marks(spec, entry):
    """Reads the exported FBX back and checks the empties came through where they were put."""
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT, entry["fbx"]))
    found = {}
    for obj in set(bpy.data.objects) - before:
        if obj.type == 'EMPTY' and obj.name.split(".")[0] in spec["marks"]:
            found[obj.name.split(".")[0]] = obj.matrix_world.translation.copy()
    for obj in set(bpy.data.objects) - before:
        bpy.data.objects.remove(obj)
    for mark, p in spec["marks"].items():
        want = HALF_TURN.to_3x3() @ at(*p)
        got = found.get(mark)
        ok = got is not None and (got - want).length < 1e-3
        print("MARK", mark, "ok" if ok else "MISMATCH", tuple(round(c, 4) for c in want), got and tuple(round(c, 4) for c in got))
        entry.setdefault("marks_checked", {})[mark] = bool(ok)


# ----- Previews ---------------------------------------------------------------------------------------------------------

def preview_cameras():
    persp = bpy.data.objects.new("Preview Camera", bpy.data.cameras.new("Preview Camera"))
    persp.data.lens = 50
    ortho = bpy.data.objects.new("Top Camera", bpy.data.cameras.new("Top Camera"))
    ortho.data.type = 'ORTHO'
    for cam in (persp, ortho):
        collection("Preview").objects.link(cam)
    return persp, ortho


def aim(cam, location, target):
    cam.location = location
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()


def bounds(objs):
    bpy.context.view_layer.update()
    lo = Vector((1e9, 1e9, 1e9))
    hi = -lo
    for obj in objs:
        for corner in obj.bound_box:
            w = obj.matrix_world @ Vector(corner)
            lo = Vector(map(min, lo, w))
            hi = Vector(map(max, hi, w))
    return lo, hi


def shoot(cam, path, res):
    scene = bpy.context.scene
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def previews(built, res):
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.eevee.taa_render_samples = 32
    scene.view_settings.view_transform = 'AgX'
    scene.render.film_transparent = False
    os.makedirs(PREVIEWS, exist_ok=True)
    stage([])
    persp, ortho = preview_cameras()
    fov = 2 * math.atan(18.0 / persp.data.lens)
    everything = [obj for levels, _marks in built.values() for obj in levels]
    pitch, yaw = math.radians(48.0), math.radians(28.0)
    # A plank wall for what hangs on one (the Wall Torch: its back on Blender's y = 0 after the half turn).
    wall_bm = bmesh.new()
    box(wall_bm, (0.0, 0.0, 0.11), (1.6, 1.6, 0.2), PLANKS)
    wall_bm.transform(HALF_TURN)
    wall = to_object("Preview Wall", wall_bm, collection("Preview"), [bpy.data.materials[n] for n in SLOTS])
    for name, (levels, marks) in built.items():
        slug = name.lower().replace(" ", "_")
        for obj in everything:
            obj.hide_render = obj is not levels[0]
        wall.hide_render = not marks
        lift = max(0.0, -bounds([levels[0]])[0].z) + (0.6 if marks else 0.0)  # what hangs on a wall, off the ground
        for obj in levels:
            obj.location.z = lift
        wall.location.z = lift
        lo, hi = bounds([levels[0]])
        centre = (lo + hi) / 2
        reach = (hi - lo).length / 2
        dist = reach / math.tan(fov / 2) * 1.25
        direction = Vector((-math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
        aim(persp, centre + direction * dist, centre)
        shoot(persp, os.path.join(PREVIEWS, f"{slug}_view.png"), (res, round(res * 0.75)))
        ortho.data.ortho_scale = max(hi.x - lo.x, hi.y - lo.y) * 1.3
        ortho.location = (centre.x, centre.y, hi.z + 5.0)
        ortho.rotation_euler = (0.0, 0.0, 0.0)
        shoot(ortho, os.path.join(PREVIEWS, f"{slug}_top.png"), (round(res * 0.75), round(res * 0.75)))
        # The three levels side by side, from the game's pitch straight from the front.
        gap = (hi.x - lo.x) + 0.35
        for i, obj in enumerate(levels):
            obj.hide_render = False
            obj.location.x = (i - 1) * gap
        lo3, hi3 = bounds(levels)
        centre3 = (lo3 + hi3) / 2
        dist3 = max((hi3 - lo3).x * 0.62, (hi3 - lo3).length / 2) / math.tan(fov / 2) * 1.1
        aim(persp, centre3 + Vector((0.0, -math.cos(pitch), math.sin(pitch))) * dist3, centre3)
        shoot(persp, os.path.join(PREVIEWS, f"{slug}_lods.png"), (res, round(res * 0.5)))
        for obj in levels:
            obj.location.x = obj.location.z = 0.0


def main():
    only = None
    if "--only" in args:
        only = [s.strip() for s in args[args.index("--only") + 1].split(",")]
    res = int(args[args.index("--res") + 1]) if "--res" in args else 900
    specs = [s for s in ASSETS if only is None or s["name"] in only]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    made = materials()
    for material in made:  # the preview's plain tints matte, as the game's are (the default gloss read them white)
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        if bsdf and not any(n.type == 'TEX_IMAGE' for n in material.node_tree.nodes):
            bsdf.inputs["Roughness"].default_value = 0.85
    coll = collection("Haven Decor")
    manifest = {}
    if os.path.exists(MANIFEST):
        with open(MANIFEST, encoding="utf-8") as f:
            manifest = json.load(f).get("assets", {})
    built = {}
    for spec in specs:
        levels, marks, entry = build_asset(spec, coll, made)
        if marks:
            check_marks(spec, entry)
        built[spec["name"]] = (levels, marks)
        manifest[spec["name"]] = entry
    order = [s["name"] for s in ASSETS]
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(dict(
            note=("Made by Tools/Blender/haven_decor.py. size_m: x across, z deep (the front is +Z), y up, in metres. "
                  "triangles/vertices per level (LOD0, LOD1, LOD2; vertices as the engine splits them). budget: "
                  "ScreenBudget.cs numbers for these bounds. cost: suggested construction items (asset names under "
                  "Data/Items/Materials); comfort: suggested BuildingDefinition.comfort. collider: a suggested primitive "
                  "in the model's space (Unity axes). marks_unity: the Wall Torch's empties in the model's space."),
            assets={k: manifest[k] for k in sorted(manifest, key=lambda k: order.index(k) if k in order else 99)}),
            f, indent=2)
    if "--no-preview" not in args:
        previews(built, res)
    if "--no-blend" not in args:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLENDER_DIR, "haven_decor.blend"))
    print("HAVEN DECOR DONE", list(built))


if __name__ == "__main__":
    main()
