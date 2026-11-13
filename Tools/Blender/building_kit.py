"""The Haven's building kit: the parts every kit-built building is made of, and the scan materials they share.

A building script (smithy.py is the model to copy) lays out its building from these parts after its concept image:
logs, boards, masonry, single field stones, a sod roof with a birch-bark edge, and a few props. Every part is mapped
at its real size with one of the kit's tiling materials (Assets/_Project/Art/Haven/Kit/Textures, CC0 scans from
Poly Haven; the sod and the soil are the terrain's own), so nothing is baked, a building of any size keeps the
scans' sharpness, and all buildings share the same few textures.

Design axes: x right, y up, z away from the camera. A script is written as the camera sees the building, its
entrance or open side at -z (the side in the concept image's foreground). build() then turns the finished meshes
half a turn, so that side is the building's front in the game (+Z, where its yard lies) and a new piece placed
facing the camera shows it. The ridge of a roof runs along x.

Roofs are bare sod: the game grows its own grass on them (AgentScripts/BuildStructureModels.cs, RoofGrass), which is
the meadow's grass in colour, wind and season.

build() writes Assets/_Project/Art/Haven/Structures/<Name>/<Name>.fbx with "<Name>" (LOD0), "<Name> LOD1",
"<Name> LOD2" and, for a building with tiers, "<Name> Tier 2" ... (the parts a tier adds), all using the material
slots in SLOTS; the Unity builder gives them the game's materials. It also renders previews and saves a .blend.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prop_bake import ROOT, args, camera, collection, image, mesh_object, new_material, node, render, select_only, stage  # noqa: E402

KIT = os.path.join(ROOT, "Assets", "_Project", "Art", "Haven", "Kit", "Textures")
TERRAIN = os.path.join(ROOT, "Assets", "_Project", "Art", "Terrain", "World")
STRUCTURES = os.path.join(ROOT, "Assets", "_Project", "Art", "Haven", "Structures")

# Material slots, and how many metres one repeat of each texture covers (the scans' real size). Those after the
# birch are the plain tinted materials small things and tier parts are made of (the game's "Base ..." tints).
(WOOD, PLANKS, STONE, IRON, TURF, EMBER, BOULDER, SOIL, BIRCH,
 COPPER, MOONSILVER, TROLLSTONE, CLOTH, WATER, LINEN, HIDE, DYE_BLUE, DYE_OCHRE, HAY) = range(19)
SLOTS = ["Kit Wood", "Kit Planks", "Kit Stone", "Kit Iron", "Kit Turf", "Kit Ember", "Kit Boulder", "Kit Soil", "Kit Birch",
         "Kit Copper", "Kit Moonsilver", "Kit Trollstone", "Kit Cloth", "Kit Water", "Kit Linen", "Kit Hide",
         "Kit Dye Blue", "Kit Dye Ochre", "Kit Hay"]
TILE = {WOOD: 0.5, PLANKS: 2.0, STONE: 1.8, IRON: 2.2, TURF: 4.0, EMBER: 1.0, BOULDER: 1.6, SOIL: 3.0, BIRCH: 2.0,
        COPPER: 1.0, MOONSILVER: 1.0, TROLLSTONE: 1.0, CLOTH: 1.0, WATER: 1.0, LINEN: 1.0, HIDE: 1.0, DYE_BLUE: 1.0,
        DYE_OCHRE: 1.0, HAY: 1.0}

LOG = 0.15       # a wall log's radius
COURSE = LOG * 1.8

# Detail per level: sides of a log, rings along it, a boulder's subdivisions (0: none, the masonry behind stands in),
# the sod grid in metres, and small parts or not.
LEVELS = [dict(sides=10, rings=2, div=2, sod=0.26, small=True),
          dict(sides=6, rings=1, div=1, sod=0.75, small=True),
          dict(sides=4, rings=1, div=0, sod=9.0, small=False)]


def at(x, y, z):
    """A point in the design's axes as Blender's (the FBX export turns Blender's -Y into +Z and mirrors X)."""
    return Vector((-x, -z, y))


class Roof:
    """A gable roof whose ridge runs along x: the deck falls from `ridge` by `drop` to the eaves at z = +-eave_z,
    and reaches half_x to each side."""

    def __init__(self, ridge, eave_z, drop, half_x):
        self.ridge, self.eave_z, self.drop, self.half_x = ridge, eave_z, drop, half_x
        self.slope = math.hypot(eave_z, drop)
        self.pitch = math.atan2(drop, eave_z)

    def deck(self, z):
        """Height of the roof deck's top at z."""
        return self.ridge - self.drop * abs(z) / self.eave_z


# ----- Parts ----------------------------------------------------------------------------------------------------------

def face(bm, verts, material, uvs, smooth):
    f = bm.faces.new(verts)
    f.material_index = material
    f.smooth = smooth
    layer = bm.loops.layers.uv.verify()
    for loop, uv in zip(f.loops, uvs):
        loop[layer].uv = uv
    return f


def log(bm, start, end, radius, level, rng, material=WOOD, wobble=0.012, uneven=0.07, cap=0.025, sides=None):
    """A round log from start to end: a little crooked and uneven, its end grain slightly domed. The bark texture's
    grain runs along it, wrapped a whole number of times so it has no seam."""
    a, b = at(*start), at(*end)
    axis = b - a
    length = axis.length
    axis.normalize()
    side = axis.orthogonal().normalized()
    up = axis.cross(side)
    sides, rings = sides or level["sides"], level["rings"]
    wraps = max(1, round(2 * math.pi * radius / TILE[material]))
    shift = rng.random()
    rows = []
    for j in range(rings + 1):
        t = j / rings
        centre = a + axis * (length * t)
        if 0 < j < rings:
            centre += side * rng.uniform(-wobble, wobble) + up * rng.uniform(-wobble, wobble)
        r = radius * (1.0 + rng.uniform(-uneven, uneven))
        rows.append([bm.verts.new(centre + (side * math.cos(2 * math.pi * k / sides) + up * math.sin(2 * math.pi * k / sides)) * r)
                     for k in range(sides)])
    for j in range(rings):
        v0, v1 = shift + length * j / rings / TILE[material], shift + length * (j + 1) / rings / TILE[material]
        for k in range(sides):
            n = (k + 1) % sides
            u0, u1 = wraps * k / sides, wraps * (k + 1) / sides
            face(bm, [rows[j][k], rows[j][n], rows[j + 1][n], rows[j + 1][k]], material,
                 [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], True)
    for row, out, flip in ((rows[0], -axis, True), (rows[-1], axis, False)):
        # End grain: its own vertices (a hard edge against the bark), a fan to a centre pushed out a little.
        middle = sum((v.co for v in row), Vector()) / sides
        rim = [bm.verts.new(v.co) for v in row]
        centre = bm.verts.new(middle + out * cap)
        for k in range(sides):
            n = (k + 1) % sides
            tri = [rim[n], rim[k], centre] if flip else [rim[k], rim[n], centre]
            uv = [((v.co - middle).dot(side) / TILE[material] + 0.5, (v.co - middle).dot(up) / TILE[material] + 0.5) for v in tri]
            face(bm, tri, material, uv, True)


def box(bm, centre, size, material, turn=None, grain='y', tile=None, lift=None):
    """A board or block: centre and size (x, y, z), turned by a matrix about its centre (Blender axes). Flat faces
    with hard edges; the texture runs at real size, its grain (V) along the box's 'x', 'y' or 'z'.
    lift(x, z of the corner) raises each upper corner by that much (a board cut to the roof's slope)."""
    tile = tile or TILE[material]
    sx, sy, sz = size[0] / 2, size[2] / 2, size[1] / 2  # Blender half sizes: x, y (design z), z (design y)
    origin = at(*centre)
    turn = turn or Matrix.Identity(3)
    grain_axis = {'x': 0, 'y': 2, 'z': 1}[grain]
    for axis in range(3):
        for sign in (-1.0, 1.0):
            others = [i for i in range(3) if i != axis]
            # V along the grain where the face has it, else along the face's longer side.
            v_axis = grain_axis if grain_axis in others else max(others, key=lambda i: (sx, sy, sz)[i])
            u_axis = next(i for i in others if i != v_axis)
            corners = []
            for du, dv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = [0.0, 0.0, 0.0]
                p[axis] = sign * (sx, sy, sz)[axis]
                p[u_axis] = du * (sx, sy, sz)[u_axis]
                p[v_axis] = dv * (sx, sy, sz)[v_axis]
                if lift and p[2] > 0:
                    p[2] += lift(centre[0] - p[0], centre[2] - p[1])
                corners.append(Vector(p))
            normal = Vector([sign if i == axis else 0.0 for i in range(3)])
            if (corners[1] - corners[0]).cross(corners[2] - corners[1]).dot(normal) < 0:
                corners.reverse()
            verts = [bm.verts.new(origin + turn @ c) for c in corners]
            off = origin  # the texture is laid in the world, so boards side by side don't repeat each other
            uvs = [((c[u_axis] + off[u_axis]) / tile, (c[v_axis] + off[v_axis]) / tile) for c in corners]
            face(bm, verts, material, uvs, False)


def mass(bm, low, high, level, seed, material=STONE, top=None, bulge=0.035, open_bottom=True, cell=None):
    """Masonry: a block from low to high, or tapering to the rectangle `top` (x0, z0, x1, z1) at its upper end, its
    faces a grid pushed in and out so the stones stand proud. Corners stay hard, faces are smooth."""
    cell = cell or (0.3 if level["div"] == 2 else 0.7 if level["div"] == 1 else 9.0)
    x0, y0, z0 = low
    x1, y1, z1 = high
    tx0, tz0, tx1, tz1 = top or (x0, z0, x1, z1)
    middle = Vector(((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))

    def point(u, v, w):
        """u, v, w in 0..1 across x, y (height), z; the rectangle shrinks towards the top."""
        xa, xb = x0 + (tx0 - x0) * v, x1 + (tx1 - x1) * v
        za, zb = z0 + (tz0 - z0) * v, z1 + (tz1 - z1) * v
        p = Vector((xa + (xb - xa) * u, y0 + (y1 - y0) * v, za + (zb - za) * w))
        out = p - middle
        out.y *= 0.25
        if out.length > 1e-6 and cell < 5.0:
            lump = noise.noise(p * 2.6 + Vector((seed, seed * 0.37, 0.0)))
            p += out.normalized() * bulge * lump * 2.0
        return p

    def steps(length):
        return max(1, round(length / cell))

    nx, ny, nz = steps(x1 - x0), steps(y1 - y0), steps(z1 - z0)
    tile = TILE[material]
    sheets = [  # (a count, b count, point from (a, b), texture coordinates from the point, outward normal)
        (nx, ny, lambda a, b: point(a, b, 1.0), lambda p: (p.x, p.y), Vector((0, 0, 1))),
        (nx, ny, lambda a, b: point(a, b, 0.0), lambda p: (-p.x, p.y), Vector((0, 0, -1))),
        (nz, ny, lambda a, b: point(1.0, b, a), lambda p: (-p.z, p.y), Vector((1, 0, 0))),
        (nz, ny, lambda a, b: point(0.0, b, a), lambda p: (p.z, p.y), Vector((-1, 0, 0))),
        (nx, nz, lambda a, b: point(a, 1.0, b), lambda p: (p.x, p.z), Vector((0, 1, 0))),
    ]
    if not open_bottom:
        sheets.append((nx, nz, lambda a, b: point(a, 0.0, b), lambda p: (p.x, -p.z), Vector((0, -1, 0))))
    for na, nb, where, texture, normal in sheets:
        grid = [[where(i / na, j / nb) for i in range(na + 1)] for j in range(nb + 1)]
        verts = [[bm.verts.new(at(*p)) for p in row] for row in grid]
        want = at(*normal)
        for j in range(nb):
            for i in range(na):
                quad = [verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]]
                points = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
                if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).dot(want) < 0:
                    quad.reverse()
                    points.reverse()
                face(bm, quad, material, [(texture(p)[0] / tile, texture(p)[1] / tile) for p in points], True)


def boulder(bm, centre, size, level, rng, yaw=None, material=BOULDER, flat=0.0):
    """A field stone: a rounded, lumpy block (a cube blown towards a ball, then pushed about), `size` across in
    x, y, z before it is turned by `yaw` about the vertical. flat squares it off (a cap slab). One closed mesh;
    the rock scan is laid on it from its six sides."""
    div = level["div"]
    if div == 0:
        return
    n = div
    yaw = rng.uniform(0, math.tau) if yaw is None else yaw
    turn = Matrix.Rotation(yaw, 3, 'Z')  # Blender's Z is the vertical
    seed = Vector((rng.uniform(0, 50), rng.uniform(0, 50), rng.uniform(0, 50)))
    half = Vector((size[0] / 2, size[2] / 2, size[1] / 2))  # Blender: x, y (design z), z (design y)
    origin = at(*centre)
    made = {}

    def vert(i, j, k):
        key = (i, j, k)
        if key not in made:
            cube = Vector((i / n * 2 - 1, j / n * 2 - 1, k / n * 2 - 1))
            ball = cube.normalized()
            p = ball.lerp(cube, 0.25 + 0.6 * flat)
            p *= 1.0 + (0.16 * (1.0 - flat)) * noise.noise(ball * 1.7 + seed)
            made[key] = (bm.verts.new(origin + turn @ Vector((p.x * half.x, p.y * half.y, p.z * half.z))), cube)
        return made[key]

    tile = TILE[material]
    for axis in range(3):
        u_axis, v_axis = [i for i in range(3) if i != axis]
        for side in (0, n):
            for a in range(n):
                for b in range(n):
                    corners = []
                    for da, db in ((0, 0), (1, 0), (1, 1), (0, 1)):
                        index = [0, 0, 0]
                        index[axis], index[u_axis], index[v_axis] = side, a + da, b + db
                        corners.append(vert(*index))
                    verts = [c[0] for c in corners]
                    normal = Vector([(1.0 if side else -1.0) if i == axis else 0.0 for i in range(3)])
                    if (verts[1].co - verts[0].co).cross(verts[2].co - verts[1].co).dot(turn @ normal) < 0:
                        corners.reverse()
                        verts.reverse()
                    uvs = [((c[1][u_axis] * half[u_axis] + seed.x) / tile, (c[1][v_axis] * half[v_axis] + seed.y) / tile)
                           for c in corners]
                    face(bm, verts, material, uvs, True)


def disc(bm, centre, radius, material, sides=8, up=True):
    """A flat round patch (a bed of coals, water in a barrel), facing up."""
    cx, cy, cz = centre
    rim = [bm.verts.new(at(cx + radius * math.cos(math.tau * k / sides), cy, cz + radius * math.sin(math.tau * k / sides)))
           for k in range(sides)]
    middle = bm.verts.new(at(cx, cy, cz))
    tile = TILE[material]
    for k in range(sides):
        tri = [rim[k], rim[(k + 1) % sides], middle]
        if ((tri[1].co - tri[0].co).cross(tri[2].co - tri[1].co).z < 0) == up:
            tri.reverse()
        face(bm, tri, material, [(v.co.x / tile, v.co.y / tile) for v in tri], True)


def footing(bm, start, end, level, rng, out, seed, height=0.3):
    """The row of field stones a wall stands on, from start to end (x, z), with the masonry they hide behind them.
    out: the wall's outward direction (x, z); the stones lean out over the masonry's face."""
    (x0, z0), (x1, z1) = start, end
    length = math.hypot(x1 - x0, z1 - z0)
    along = ((x1 - x0) / length, (z1 - z0) / length)
    wide = 0.2 if level["div"] else 0.3
    lo = (min(x0, x1) - abs(out[0]) * wide, 0.0, min(z0, z1) - abs(out[1]) * wide)
    hi = (max(x0, x1) + abs(out[0]) * wide, height if level["div"] else height + 0.04, max(z0, z1) + abs(out[1]) * wide)
    mass(bm, lo, hi, level, seed, cell=9.0)
    t = 0.0
    while t < length - 0.15:
        w = min(rng.uniform(0.42, 0.66), length - t + 0.1)
        h = rng.uniform(height + 0.02, height + 0.16)
        d = rng.uniform(0.36, 0.5)
        m = t + w / 2
        centre = (x0 + along[0] * m + out[0] * (0.16 + d * 0.15), h / 2 - 0.05, z0 + along[1] * m + out[1] * (0.16 + d * 0.15))
        boulder(bm, centre, (w * 1.08, h, d), level, rng, yaw=math.atan2(along[1], along[0]) + rng.uniform(-0.2, 0.2))
        t += w


def post_stone(bm, x, z, level, rng):
    """The flat stone a post stands on."""
    if level["div"]:
        boulder(bm, (x, 0.1, z), (0.6, 0.3, 0.52), level, rng, flat=0.3)
    else:
        mass(bm, (x - 0.28, 0.0, z - 0.25), (x + 0.28, 0.24, z + 0.25), level, 6.0, cell=9.0)


def log_wall(bm, start, end, low, high, level, rng, half=False, over=(0.25, 0.42), over_end=None, radius=LOG, sides=None):
    """A wall of lying logs from start to end (x, z) between two heights. `half` lifts its courses half a log, as on
    every second wall of a house, so the ends cross at the corners; over: how far the ends stick out (least, most),
    over_end the same for the far end alone ((0, 0) for an end against a post)."""
    (x0, z0), (x1, z1) = start, end
    length = math.hypot(x1 - x0, z1 - z0)
    ax, az = (x1 - x0) / length, (z1 - z0) / length
    y = low + radius * 0.9 + (COURSE / 2 if half else 0.0)
    while y + radius <= high + 0.1:
        a, b = rng.uniform(*over), rng.uniform(*(over_end or over))
        log(bm, (x0 - ax * a, y, z0 - az * a), (x1 + ax * b, y, z1 + az * b), radius, level, rng, sides=sides)
        y += COURSE


def brace(bm, x, z, sign, low, high, level, rng, reach=0.68):
    """A slanting brace from a post up to the plate log, along x."""
    log(bm, (x + sign * 0.1, low, z), (x + sign * (0.1 + reach), high, z), 0.075, level, rng, wobble=0.0, sides=6)


def board_gable(bm, x, half_z, bottom, roof, level, lean=0.0):
    """A gable of upright boards at x, from `bottom` up to the roof, cut to its slope."""
    boards = max(4, round(half_z * 2 / 0.29)) if level["small"] else 4
    width = 2 * half_z / boards
    for k in range(boards):
        z = -half_z + width * (k + 0.5)
        top = roof.deck(z) - 0.05  # at the board's middle; its upper end is cut to the roof's slope
        if top - bottom > 0.06:
            box(bm, (x, (top + bottom) / 2, z), (0.04, top - bottom, width - 0.012), PLANKS, grain='y',
                lift=lambda corner_x, corner_z, top=top: roof.deck(corner_z) - 0.05 - top)


def sod(bm, roof, sign, level, rng, thick=0.4):
    """One slope of the roof, on the -z (sign -1) or +z side: boards, birch bark showing pale at the edges, and a
    thick lumpy layer of bare sod with a ragged edge, held by a birch-wrapped log along the eave."""
    slope, eave_z, half_x = roof.slope, roof.eave_z, roof.half_x
    mid_z = sign * eave_z / 2
    turn = Matrix.Rotation(sign * roof.pitch, 3, 'X')  # about Blender's X (the ridge's direction); z is mirrored by at()
    # The deck: boards running down the slope.
    box(bm, (0.0, roof.deck(mid_z) - 0.03, mid_z), (half_x * 2, 0.06, slope), PLANKS, turn=turn, grain='z')
    # Birch bark: a pale sheet a hand wider than the sod, so a band of it shows along the eave and the gables.
    box(bm, (0.0, roof.deck(mid_z) + 0.012, mid_z * 1.05), ((half_x + 0.2) * 2, 0.024, slope + 0.3), BIRCH, turn=turn, grain='z')

    cell = level["sod"]
    rough = cell < 5.0
    na, nb = max(1, round(slope / cell)), max(2, round(half_x * 2 / cell))

    def top(a, b):
        """The sod's surface, a from the ridge (0) to the eave (1), b along the building: thinner and ragged
        towards its three open edges, and lumpy. Both slopes get the same heights along the ridge."""
        x = -half_x + 2 * half_x * b
        if rough:
            x += (0.11 * noise.noise(Vector((a * 3.1, sign * 5.0, 1.0)))) * (1.0 if b in (0.0, 1.0) else 0.0)
        z = eave_z * a
        if rough and a == 1.0:
            z += 0.02 + 0.1 * noise.noise(Vector((x * 1.3, sign * 3.0, 4.0))) + 0.04 * noise.noise(Vector((x * 4.1, sign, 9.0)))
        edge = min(1 - a, b, 1 - b) if rough else 1.0
        inside = min(1.0, edge * 5.0)
        height = thick * (0.6 + 0.4 * math.sin(inside * math.pi / 2))
        if rough:
            height += inside * (0.17 * noise.noise(Vector((x * 0.7, z * 0.7, 3.0))) + 0.08 * noise.noise(Vector((x * 2.1, z * 2.1, 9.0))))
            height += 0.03 * noise.noise(Vector((x * 5.0, z * 5.0 * sign, 2.0)))
        return Vector((x, roof.deck(eave_z * a) + max(0.1, height), sign * z))

    grid = [[top(i / na, j / nb) for i in range(na + 1)] for j in range(nb + 1)]
    verts = [[bm.verts.new(at(*p)) for p in row] for row in grid]
    tile = TILE[TURF]
    for j in range(nb):
        for i in range(na):
            quad = [verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]]
            points = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
            if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).z < 0:
                quad.reverse()
                points.reverse()
            face(bm, quad, TURF, [(p.x / tile, p.z / tile) for p in points], True)
    # Its edge, down to the bark: along the eave and up both gables. Bare soil and roots.
    tile = TILE[SOIL]
    edges = [[(grid[j][na], verts[j][na]) for j in range(nb + 1)],
             [(grid[0][i], verts[0][i]) for i in range(na + 1)],
             [(grid[nb][i], verts[nb][i]) for i in range(na + 1)]]
    inside = at(0.0, roof.deck(mid_z), mid_z)
    for edge in edges:
        for (p, vp), (q, vq) in zip(edge, edge[1:]):
            low_p = bm.verts.new(at(p.x, roof.deck(p.z) + 0.02, p.z))
            low_q = bm.verts.new(at(q.x, roof.deck(q.z) + 0.02, q.z))
            quad = [bm.verts.new(vp.co), bm.verts.new(vq.co), low_q, low_p]  # its own corners: a hard edge on the sod
            centre = sum((v.co for v in quad), Vector()) / 4
            if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).dot(centre - inside) < 0:
                quad.reverse()
            face(bm, quad, SOIL, [((v.co.x + v.co.y) / tile, v.co.z / tile) for v in quad], True)
    # The log that holds the sod at the eave, the birch bark rolled round it.
    z = sign * (eave_z + 0.17)
    y = roof.deck(eave_z + 0.17) + 0.09
    log(bm, (-half_x - 0.24, y, z), (half_x + 0.24, y, z), 0.085, level, rng, material=BIRCH, wobble=0.004, uneven=0.03)
    if level["small"]:
        # Wind boards on both gables.
        for x in (-half_x - 0.2, half_x + 0.2):
            box(bm, (x, roof.deck(mid_z) - 0.02, mid_z * 1.05), (0.045, 0.3, slope + 0.3), PLANKS, turn=turn, grain='z')


def sod_roof(bm, roof, level, rng, purlins=(), thick=0.4):
    """The whole roof: the ridge log, purlins at the given z under the deck, and both slopes of sod."""
    log(bm, (-roof.half_x - 0.5, roof.ridge - 0.2, 0.0), (roof.half_x + 0.5, roof.ridge - 0.2, 0.0), 0.15, level, rng)
    if level["small"]:
        for z in purlins:
            log(bm, (-roof.half_x + 0.02, roof.deck(z) - 0.17, z), (roof.half_x - 0.02, roof.deck(z) - 0.17, z), 0.11, level, rng)
    for sign in (-1, 1):
        sod(bm, roof, sign, level, rng, thick)


def anvil(bm, x, z, level, rng):
    """An anvil on its stump: stump of bark and end grain, the anvil in a few blocks and a horn."""
    log(bm, (x, 0.0, z), (x, 0.52, z), 0.3, level, rng, uneven=0.05, wobble=0.0, cap=0.01)
    box(bm, (x, 0.555, z), (0.44, 0.07, 0.27), IRON)
    box(bm, (x, 0.63, z), (0.24, 0.09, 0.17), IRON)
    box(bm, (x - 0.03, 0.715, z), (0.52, 0.09, 0.21), IRON)
    if level["small"]:
        # The horn: a cone off the face's end.
        tip, base, r, n = at(x + 0.5, 0.72, z), at(x + 0.23, 0.715, z), 0.045, 6
        ring = [bm.verts.new(base + Vector((0.0, math.cos(2 * math.pi * k / n) * 0.1, math.sin(2 * math.pi * k / n) * r)))
                for k in range(n)]
        point = bm.verts.new(tip)
        for k in range(n):
            tri = [ring[k], ring[(k + 1) % n], point]
            if (tri[1].co - tri[0].co).cross(tri[2].co - tri[1].co).dot(tri[0].co + tri[1].co - base * 2) < 0:
                tri.reverse()
            face(bm, tri, IRON, [(v.co.x / TILE[IRON], (v.co.y + v.co.z) / TILE[IRON]) for v in tri], True)


def barrel(bm, x, z, level, height=0.8, radius=0.35, water=True):
    """A barrel: staves that bulge in the middle, two iron hoops, and dark water a little below the rim (or a lid)."""
    sides = max(6, level["sides"])
    heights = [h * height for h in (0.0, 0.175, 0.275, 0.5, 0.725, 0.825, 1.0)]
    radii = [r * radius for r in (0.83, 0.93, 0.96, 1.0, 0.96, 0.93, 0.86)]
    bands = {1, 4}  # the rows of faces that are iron
    rows = [[bm.verts.new(at(x + r * math.cos(math.tau * k / sides), h, z + r * math.sin(math.tau * k / sides)))
             for k in range(sides)] for h, r in zip(heights, radii)]
    centre = at(x, height / 2, z)
    for j in range(len(heights) - 1):
        material = IRON if j in bands else PLANKS
        tile = TILE[material]
        for k in range(sides):
            n = (k + 1) % sides
            quad = [rows[j][k], rows[j][n], rows[j + 1][n], rows[j + 1][k]]
            uvs = [(k * 0.21 / tile * 3, heights[j] / tile * 3), ((k + 1) * 0.21 / tile * 3, heights[j] / tile * 3),
                   ((k + 1) * 0.21 / tile * 3, heights[j + 1] / tile * 3), (k * 0.21 / tile * 3, heights[j + 1] / tile * 3)]
            middle = sum((v.co for v in quad), Vector()) / 4
            if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).dot(middle - centre) < 0:
                quad.reverse()
                uvs.reverse()
            face(bm, quad, material, uvs, True)
    disc(bm, (x, height * (0.9 if water else 1.0), z), radius * (0.78 if water else 0.86), IRON if water else PLANKS, sides)


def crate(bm, x, y, z, size, rng):
    """A plank crate standing at (x, z) with its underside at y, a little askew."""
    turn = Matrix.Rotation(rng.uniform(-0.3, 0.3), 3, 'Z')
    box(bm, (x, y + size[1] / 2, z), size, PLANKS, turn=turn, grain='x')


def round_forge(bm, x, z, level, rng):
    """A round forge: two courses of field stones round a core, and the bed of coals on top."""
    count = 9
    for course, (y, r) in enumerate(((0.15, 0.64), (0.43, 0.6))):
        for k in range(count):
            angle = math.tau * (k + 0.5 * course) / count
            boulder(bm, (x + r * math.cos(angle), y, z + r * math.sin(angle)), (0.47, 0.31, 0.36), level, rng,
                    yaw=angle + math.pi / 2 + rng.uniform(-0.15, 0.15))
    sides = 8 if level["div"] else 6
    radius = 0.5 if level["div"] else 0.78
    top_y = 0.52 if level["div"] else 0.58
    low = [bm.verts.new(at(x + radius * math.cos(math.tau * k / sides), 0.0, z + radius * math.sin(math.tau * k / sides)))
           for k in range(sides)]
    high = [bm.verts.new(at(x + radius * math.cos(math.tau * k / sides), top_y, z + radius * math.sin(math.tau * k / sides)))
            for k in range(sides)]
    centre = at(x, 0.3, z)
    tile = TILE[STONE]
    for k in range(sides):
        n = (k + 1) % sides
        quad = [low[k], low[n], high[n], high[k]]
        if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).dot((quad[0].co + quad[1].co) / 2 - centre) < 0:
            quad.reverse()
        face(bm, quad, STONE, [((v.co.x + v.co.y) / tile, v.co.z / tile) for v in quad], True)
    # The stone rim round the coals, and the coals.
    ember_r = 0.36
    rim = [bm.verts.new(v.co) for v in high]
    inner = [bm.verts.new(at(x + ember_r * math.cos(math.tau * k / sides), top_y + 0.02, z + ember_r * math.sin(math.tau * k / sides)))
             for k in range(sides)]
    for k in range(sides):
        n = (k + 1) % sides
        quad = [rim[k], rim[n], inner[n], inner[k]]
        if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).z < 0:
            quad.reverse()
        face(bm, quad, STONE, [(v.co.x / tile, v.co.y / tile) for v in quad], True)
    disc(bm, (x, top_y + 0.03, z), ember_r, EMBER, sides)


def chimney(bm, x, z, base, top, level, rng, half=0.42, cap=True):
    """A chimney: courses of field stones round a core from `base` (inside the roof) up to `top`, under a cap slab."""
    core = half - 0.08 if level["div"] else half + 0.06
    mass(bm, (x - core, base, z - core), (x + core, top, z + core), level, 8.0, cell=9.0, bulge=0.0)
    y = base + 0.14
    course = 0
    while y < top - 0.05:
        h = rng.uniform(0.25, 0.31)
        shift = 0.5 if course % 2 else 0.0
        for side in range(4):
            angle = side * math.pi / 2
            ax, az = math.cos(angle), math.sin(angle)      # along this side
            ox, oz = math.sin(angle), -math.cos(angle)     # outward
            for k in range(2):
                t = (k + shift) / 2 * 2 * half - half + half / 2
                centre = (x + ax * t + ox * half * 0.92, y, z + az * t + oz * half * 0.92)
                boulder(bm, centre, (rng.uniform(0.44, 0.52) * half / 0.42, h * 1.08, rng.uniform(0.3, 0.38)), level, rng,
                        yaw=angle + rng.uniform(-0.12, 0.12))
        y += h * 0.93
        course += 1
    if not cap:
        return
    if level["div"]:
        boulder(bm, (x, top + 0.05, z), (half * 2.57, 0.14, half * 2.43), level, rng, yaw=0.12, flat=0.8)
    else:
        box(bm, (x, top + 0.07, z), (half * 2.85, 0.16, half * 2.67), STONE, turn=Matrix.Rotation(0.12, 3, 'Z'))


def lantern(bm, x, y, z):
    """A small iron lantern with its light, hung at (x, y, z)."""
    box(bm, (x, y + 0.17, z), (0.26, 0.05, 0.26), IRON)
    box(bm, (x, y, z), (0.17, 0.26, 0.17), EMBER)
    box(bm, (x, y - 0.15, z), (0.22, 0.04, 0.22), IRON)


def banner(bm, x, z, level, rng, height=3.3):
    """A pole with a hanging cloth, towards -z."""
    log(bm, (x, 0.0, z), (x, height, z), 0.055, level, rng, wobble=0.0, sides=6)
    log(bm, (x, height - 0.25, z + 0.05), (x, height - 0.25, z - 0.75), 0.03, level, rng, wobble=0.0, sides=4)
    box(bm, (x, height - 0.85, z - 0.42), (0.03, 1.15, 0.6), CLOTH)


# ----- Materials (for the preview: the game's are made by BuildStructureModels.cs) ------------------------------------

def scanned(name, base, tint=(1.0, 1.0, 1.0), saturation=1.0, metallic=None):
    material, tree, bsdf = new_material(name)
    colour = node(tree, "ShaderNodeTexImage", (-700, 200))
    colour.image = image(os.path.join(KIT, f"{base}_diff.jpg"), 'sRGB')
    hsv = node(tree, "ShaderNodeHueSaturation", (-400, 200), Saturation=saturation)
    tree.links.new(colour.outputs["Color"], hsv.inputs["Color"])
    tinted = node(tree, "ShaderNodeMix", (-200, 200))
    tinted.data_type = 'RGBA'
    tinted.blend_type = 'MULTIPLY'
    tinted.inputs["Factor"].default_value = 1.0
    tinted.inputs["B"].default_value = (*tint, 1.0)
    tree.links.new(hsv.outputs["Color"], tinted.inputs["A"])
    tree.links.new(tinted.outputs["Result"], bsdf.inputs["Base Color"])
    arm = node(tree, "ShaderNodeTexImage", (-700, -100))
    arm.image = image(os.path.join(KIT, f"{base}_arm.jpg"), 'Non-Color')
    split = node(tree, "ShaderNodeSeparateColor", (-400, -100))
    tree.links.new(arm.outputs["Color"], split.inputs["Color"])
    tree.links.new(split.outputs["Green"], bsdf.inputs["Roughness"])
    if metallic is not None:
        bsdf.inputs["Metallic"].default_value = metallic
    normal = node(tree, "ShaderNodeTexImage", (-700, -400))
    normal.image = image(os.path.join(KIT, f"{base}_nor_gl.jpg"), 'Non-Color')
    bump = node(tree, "ShaderNodeNormalMap", (-400, -400))
    tree.links.new(normal.outputs["Color"], bump.inputs["Color"])
    tree.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return material


def flat(name, path, tint=(1.0, 1.0, 1.0)):
    material, tree, bsdf = new_material(name)
    colour = node(tree, "ShaderNodeTexImage", (-500, 200))
    colour.image = image(path, 'sRGB')
    tinted = node(tree, "ShaderNodeMix", (-200, 200))
    tinted.data_type = 'RGBA'
    tinted.blend_type = 'MULTIPLY'
    tinted.inputs["Factor"].default_value = 1.0
    tinted.inputs["B"].default_value = (*tint, 1.0)
    tree.links.new(colour.outputs["Color"], tinted.inputs["A"])
    tree.links.new(tinted.outputs["Result"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    return material


def plain(name, colour, glow=None, metallic=0.0):
    material, _tree, bsdf = new_material(name)
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    if glow:
        bsdf.inputs["Emission Color"].default_value = (*glow, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 4.0
    return material


def materials():
    """The preview's materials, in SLOTS order."""
    return [scanned("Kit Wood", "rough_wood", tint=(1.0, 0.9, 0.78)),
            scanned("Kit Planks", "weathered_planks", tint=(1.0, 0.92, 0.82)),
            scanned("Kit Stone", "rock_wall_10", tint=(0.95, 1.0, 1.08), saturation=0.4),
            scanned("Kit Iron", "rust_coarse_01", tint=(0.42, 0.42, 0.46), saturation=0.3, metallic=0.85),
            flat("Kit Turf", os.path.join(TERRAIN, "Meadow Grass", "Meadow Grass_Color.jpg"), tint=(0.8, 0.68, 0.5)),
            plain("Kit Ember", (0.05, 0.02, 0.01), glow=(1.0, 0.28, 0.05)),
            scanned("Kit Boulder", "rock_boulder_dry", tint=(0.72, 0.74, 0.78), saturation=0.7),
            flat("Kit Soil", os.path.join(TERRAIN, "Dark Humus", "Dark Humus_Color.jpg")),
            scanned("Kit Birch", "weathered_planks", tint=(2.6, 2.5, 2.3), saturation=0.25),
            plain("Kit Copper", (0.62, 0.34, 0.17), metallic=0.8),
            plain("Kit Moonsilver", (0.72, 0.78, 0.86), glow=(0.15, 0.2, 0.28), metallic=0.8),
            plain("Kit Trollstone", (0.2, 0.27, 0.2)),
            plain("Kit Cloth", (0.46, 0.14, 0.1)),
            plain("Kit Water", (0.1, 0.18, 0.22)),
            plain("Kit Linen", (0.62, 0.56, 0.44)),
            plain("Kit Hide", (0.4, 0.27, 0.16)),
            plain("Kit Dye Blue", (0.16, 0.24, 0.36)),
            plain("Kit Dye Ochre", (0.6, 0.42, 0.14)),
            scanned("Kit Hay", "hay")]  # made by Tools/LocalSpawn/kit_textures.py (Poly Haven has no hay)


# ----- Building it -----------------------------------------------------------------------------------------------------

def build(name, shape, tiers=None, script="building", folder=None, keep_blend=True):
    """Makes the building: shape(bm, level, rng) draws it once per level of detail, tiers {2: part(bm, level, rng), ...}
    draw what each tier adds. Exports the FBX (to folder/<name>, Art/Haven/Structures by default), renders previews
    (skipped with --no-preview) and saves the .blend (unless keep_blend is off: many small pieces from one script)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    made = materials()
    coll = collection(name)
    half_turn = Matrix.Rotation(math.pi, 4, 'Z')  # the side drawn towards the camera becomes the game's front (+Z)

    def make(object_name, draw, level):
        bm = bmesh.new()
        draw(bm, level, random.Random(7))
        bm.transform(half_turn)
        obj = mesh_object(object_name, bm, coll, made)
        counts = {}
        for polygon in obj.data.polygons:
            slot = SLOTS[polygon.material_index]
            counts[slot] = counts.get(slot, 0) + len(polygon.vertices) - 2
        print("BUILT", object_name, sum(counts.values()), "triangles", len(obj.data.vertices), "vertices", counts)
        return obj

    levels = [make(name if i == 0 else f"{name} LOD{i}", shape, LEVELS[i]) for i in range(3)]
    parts = [make(f"{name} Tier {tier}", draw, LEVELS[0]) for tier, draw in sorted((tiers or {}).items())]
    folder = os.path.join(folder or STRUCTURES, name)
    os.makedirs(folder, exist_ok=True)
    select_only(levels + parts)
    bpy.ops.export_scene.fbx(filepath=os.path.join(folder, f"{name}.fbx"), use_selection=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
    blender = os.path.join(ROOT, "Tools", "Blender")
    if "--no-preview" not in args:
        preview = os.path.join(blender, f"{script}_preview.png")
        width = max(obj.dimensions.x for obj in levels) + 2.5
        stage([(levels, width)])
        for part in parts:  # the tiers' parts stand on the full model, as the top tier looks
            part.location = levels[0].location
        camera((0.0, -19.0, 15.0), (0.0, 0.0, 1.6), lens=40)  # the three levels, from about the game's pitch
        render(preview.replace(".png", "_lods.png"), 1920, 1080)
        levels[1].hide_render = levels[2].hide_render = True
        x = levels[0].location.x
        # After the half turn the front faces Blender's -Y: the game's camera stands there, 48 degrees up.
        camera((x, -13.4, 14.9), (x, 0.0, 1.0), lens=40)
        render(preview, 1920, 1080)
        camera((x - 4.5, -9.0, 2.6), (x + 0.3, 0.0, 1.3), lens=30)  # standing at the front, as in the concept
        render(preview.replace(".png", "_front.png"), 1600, 900)
        levels[1].hide_render = levels[2].hide_render = False
    if keep_blend:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(blender, f"{script}.blend"))
    print(name.upper(), "DONE")
    return levels, parts
