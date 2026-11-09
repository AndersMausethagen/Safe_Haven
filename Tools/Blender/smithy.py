"""The Smithy, built from the Haven's building kit after its concept image: a long, low Norse log smithy under a sod roof.

This is the kit-built way of making a building (the other is a model generated whole by LocalSpawn, see
Tools/LocalSpawn/structures_manifest.json "Smithy Generated"): the shape is modelled here from a few kinds of part
(logs, boards, boulders, masonry, a sod roof), and every part is mapped at its real size with one of the kit's tiling
materials (Assets/_Project/Art/Haven/Kit/Textures, CC0 scans from Poly Haven; the sod and the soil are the terrain's
own). Nothing is baked: a 7 m building keeps the scans' full sharpness, the mesh is closed and on budget, and every
building made this way shares the same few textures.

The design follows the concept image (Tools/LocalSpawn/Structures/Smithy Generated/A/concept.png), not the stand-in
it replaces: the ridge runs along the building, the long side towards the camera is open between posts with braces,
with a closed store at its left end; log walls with crossed corners stand on a row of field stones; the gables are
upright boards; birch bark shows white under the sod along the eaves and gables; a chimney of stones with a cap
slab stands near the ridge at the right end; a round stone forge, the anvil on its stump and the quench barrel stand
at the opening, where the game's camera sees them under the eave.

The roof is bare sod: the game grows its own grass on it (AgentScripts/BuildStructureModels.cs), which is the
meadow's grass in colour, wind and season.

Game axes: x right, y up, z away from the camera. The open side faces -z, towards the camera at the default view.

Writes Assets/_Project/Art/Haven/Structures/Smithy Kit/Smithy Kit.fbx: "Smithy Kit" (LOD0), "Smithy Kit LOD1" and
"Smithy Kit LOD2", with the material slots in SLOTS (the builder gives them the game's materials), preview renders
and smithy.blend.

Run: blender -b --factory-startup --python Tools/Blender/smithy.py -- <project root> [<preview.png>]
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

NAME = "Smithy Kit"
PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "smithy_preview.png")
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Haven", "Structures", NAME)
KIT = os.path.join(ROOT, "Assets", "_Project", "Art", "Haven", "Kit", "Textures")
TERRAIN = os.path.join(ROOT, "Assets", "_Project", "Art", "Terrain", "World")

# Material slots, and how many metres one repeat of each texture covers (the scans' real size).
WOOD, PLANKS, STONE, IRON, TURF, EMBER, BOULDER, SOIL, BIRCH = range(9)
SLOTS = ["Kit Wood", "Kit Planks", "Kit Stone", "Kit Iron", "Kit Turf", "Kit Ember", "Kit Boulder", "Kit Soil", "Kit Birch"]
TILE = {WOOD: 0.5, PLANKS: 2.0, STONE: 1.8, IRON: 2.2, TURF: 4.0, EMBER: 1.0, BOULDER: 1.6, SOIL: 3.0, BIRCH: 2.0}

# The building, in the game's axes. at() turns them into Blender's.
WALL_X, WALL_Z = 3.4, 2.0        # the walls' centre lines
PLATE = 2.3                      # top of the log walls
RIDGE, EAVE_Z, EAVE_DROP = 3.55, 2.6, 1.4  # the roof deck: from the ridge down to the eave (28 degrees)
ROOF_X = 3.95
LOG = 0.15
FOOT = 0.3                       # the stone footing's height
STORE_X = -1.3                   # the closed store's inner wall, and the first post
POST_X = 0.95
FORGE = (-0.1, -1.15)            # x, z of the round forge
ANVIL = (-1.85, -2.8)
BARREL = (2.45, -2.4)
CHIMNEY = (2.5, 0.8)

# Detail per level: sides of a log, rings along it, a boulder's subdivisions (0: none, the masonry behind stands in),
# the sod grid in metres, and small parts or not.
LEVELS = [dict(sides=10, rings=2, div=2, sod=0.26, small=True),
          dict(sides=6, rings=1, div=1, sod=0.75, small=True),
          dict(sides=4, rings=1, div=0, sod=9.0, small=False)]


def at(x, y, z):
    """A point in the game's axes as Blender's: the FBX export turns Blender's -Y into the game's +Z and mirrors X."""
    return Vector((-x, -z, y))


def deck(z):
    """Height of the roof deck's top at z."""
    return RIDGE - EAVE_DROP * abs(z) / EAVE_Z


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
    """A round log from start to end (game axes): a little crooked and uneven, its end grain slightly domed.
    The bark texture's grain runs along it, wrapped a whole number of times so it has no seam."""
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
    """A board or block: centre and size in game axes (x, y, z), turned by a matrix about its centre (Blender axes).
    Flat faces with hard edges; the texture runs at real size, its grain (V) along the box's 'x', 'y' or 'z'.
    lift(game x, game z of the corner) raises each upper corner by that much (a board cut to the roof's slope)."""
    tile = tile or TILE[material]
    sx, sy, sz = size[0] / 2, size[2] / 2, size[1] / 2  # Blender half sizes: x, y (game z), z (game y)
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
    """Masonry: a block from low to high (game axes), or tapering to the rectangle `top` (x0, z0, x1, z1) at its
    upper end, its faces a grid pushed in and out so the stones stand proud. Corners stay hard, faces are smooth."""
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
    sheets = [  # (a count, b count, point from (a, b), texture coordinates from the point, outward normal in game axes)
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
    turn = Matrix.Rotation(yaw, 3, 'Z')  # Blender's Z is the game's vertical
    seed = Vector((rng.uniform(0, 50), rng.uniform(0, 50), rng.uniform(0, 50)))
    half = Vector((size[0] / 2, size[2] / 2, size[1] / 2))  # Blender: x, y (game z), z (game y)
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


def footing(bm, start, end, level, rng, out, seed):
    """The row of field stones a wall stands on, from start to end (x, z), with the masonry they hide behind them.
    out: the wall's outward direction (x, z); the stones lean out over the masonry's face."""
    (x0, z0), (x1, z1) = start, end
    length = math.hypot(x1 - x0, z1 - z0)
    along = ((x1 - x0) / length, (z1 - z0) / length)
    wide = 0.2 if level["div"] else 0.3
    lo = (min(x0, x1) - abs(out[0]) * wide - abs(along[0]) * 0.0, 0.0, min(z0, z1) - abs(out[1]) * wide)
    hi = (max(x0, x1) + abs(out[0]) * wide, FOOT if level["div"] else FOOT + 0.04, max(z0, z1) + abs(out[1]) * wide)
    mass(bm, lo, hi, level, seed, cell=9.0)
    t = 0.0
    while t < length - 0.15:
        w = min(rng.uniform(0.42, 0.66), length - t + 0.1)
        h = rng.uniform(FOOT + 0.02, FOOT + 0.16)
        d = rng.uniform(0.36, 0.5)
        m = t + w / 2
        centre = (x0 + along[0] * m + out[0] * (0.16 + d * 0.15), h / 2 - 0.05, z0 + along[1] * m + out[1] * (0.16 + d * 0.15))
        boulder(bm, centre, (w * 1.08, h, d), level, rng, yaw=math.atan2(along[1], along[0]) + rng.uniform(-0.2, 0.2))
        t += w


def sod(bm, sign, level, rng):
    """One slope of the roof, on the -z (sign -1) or +z side: boards, birch bark showing white at the edges, and a
    thick lumpy layer of bare sod with a ragged edge, held by a log along the eave."""
    slope = math.hypot(EAVE_Z, EAVE_DROP)
    pitch = math.atan2(EAVE_DROP, EAVE_Z)
    mid_z = sign * EAVE_Z / 2
    turn = Matrix.Rotation(sign * pitch, 3, 'X')  # about Blender's X (the ridge's direction); z is mirrored by at()
    # The deck: boards running down the slope.
    box(bm, (0.0, deck(mid_z) - 0.03, mid_z), (ROOF_X * 2, 0.06, slope), PLANKS, turn=turn, grain='z')
    # Birch bark: a pale sheet a hand wider than the sod, so a white band shows along the eave and the gables.
    box(bm, (0.0, deck(mid_z) + 0.012, mid_z * 1.05), ((ROOF_X + 0.2) * 2, 0.024, slope + 0.3), BIRCH, turn=turn, grain='z')

    cell = level["sod"]
    rough = cell < 5.0
    na, nb = max(1, round(slope / cell)), max(2, round(ROOF_X * 2 / cell))
    thick = 0.4

    def top(a, b):
        """The sod's surface, a from the ridge (0) to the eave (1), b along the building: thinner and ragged
        towards its three open edges, and lumpy. Both slopes get the same heights along the ridge."""
        x = -ROOF_X + 2 * ROOF_X * b
        if rough:
            x += (0.11 * noise.noise(Vector((a * 3.1, sign * 5.0, 1.0)))) * (1.0 if b in (0.0, 1.0) else 0.0)
        z = EAVE_Z * a
        if rough and a == 1.0:
            z += 0.02 + 0.1 * noise.noise(Vector((x * 1.3, sign * 3.0, 4.0))) + 0.04 * noise.noise(Vector((x * 4.1, sign, 9.0)))
        edge = min(1 - a, b, 1 - b) if rough else 1.0
        inside = min(1.0, edge * 5.0)
        height = thick * (0.6 + 0.4 * math.sin(inside * math.pi / 2))
        if rough:
            height += inside * (0.17 * noise.noise(Vector((x * 0.7, z * 0.7, 3.0))) + 0.08 * noise.noise(Vector((x * 2.1, z * 2.1, 9.0))))
            height += 0.03 * noise.noise(Vector((x * 5.0, z * 5.0 * sign, 2.0)))
        return Vector((x, deck(EAVE_Z * a) + max(0.1, height), sign * z))

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
    inside = at(0.0, deck(mid_z), mid_z)
    for edge in edges:
        for (p, vp), (q, vq) in zip(edge, edge[1:]):
            low_p = bm.verts.new(at(p.x, deck(p.z) + 0.02, p.z))
            low_q = bm.verts.new(at(q.x, deck(q.z) + 0.02, q.z))
            quad = [bm.verts.new(vp.co), bm.verts.new(vq.co), low_q, low_p]  # its own corners: a hard edge on the sod
            centre = sum((v.co for v in quad), Vector()) / 4
            if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).dot(centre - inside) < 0:
                quad.reverse()
            face(bm, quad, SOIL, [((v.co.x + v.co.y) / tile, v.co.z / tile) for v in quad], True)
    # The log that holds the sod at the eave, the birch bark rolled round it.
    eave_z = sign * (EAVE_Z + 0.17)
    log(bm, (-ROOF_X - 0.24, deck(EAVE_Z + 0.17) + 0.09, eave_z), (ROOF_X + 0.24, deck(EAVE_Z + 0.17) + 0.09, eave_z), 0.085, level, rng,
        material=BIRCH, wobble=0.004, uneven=0.03)
    if level["small"]:
        # Wind boards on both gables.
        for x in (-ROOF_X - 0.2, ROOF_X + 0.2):
            box(bm, (x, deck(mid_z) - 0.02, mid_z * 1.05), (0.045, 0.3, slope + 0.3), PLANKS, turn=turn, grain='z')


def anvil(bm, level, rng):
    """The anvil on its stump: stump of bark and end grain, the anvil in a few blocks and a horn."""
    x, z = ANVIL
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


def barrel(bm, level):
    """The quench barrel: staves that bulge in the middle, two iron hoops, dark water a little below the rim."""
    x, z = BARREL
    sides = max(6, level["sides"])
    heights = [0.0, 0.14, 0.22, 0.4, 0.58, 0.66, 0.8]
    radii = [0.29, 0.325, 0.335, 0.35, 0.335, 0.325, 0.3]
    bands = {1, 4}  # the rows of faces that are iron
    rows = [[bm.verts.new(at(x + r * math.cos(math.tau * k / sides), h, z + r * math.sin(math.tau * k / sides)))
             for k in range(sides)] for h, r in zip(heights, radii)]
    centre = at(x, 0.4, z)
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
    # The water: a dark disc inside the rim.
    rim = [bm.verts.new(at(x + 0.27 * math.cos(math.tau * k / sides), 0.72, z + 0.27 * math.sin(math.tau * k / sides)))
           for k in range(sides)]
    middle = bm.verts.new(at(x, 0.72, z))
    for k in range(sides):
        tri = [rim[k], rim[(k + 1) % sides], middle]
        if (tri[1].co - tri[0].co).cross(tri[2].co - tri[1].co).z < 0:
            tri.reverse()
        face(bm, tri, IRON, [(v.co.x / TILE[IRON], v.co.y / TILE[IRON]) for v in tri], True)


def forge(bm, level, rng):
    """The round forge: two courses of field stones round a core, and the bed of coals on top."""
    fx, fz = FORGE
    count = 9
    for course, (y, r) in enumerate(((0.15, 0.64), (0.43, 0.6))):
        for k in range(count):
            angle = math.tau * (k + 0.5 * course) / count
            boulder(bm, (fx + r * math.cos(angle), y, fz + r * math.sin(angle)), (0.47, 0.31, 0.36), level, rng,
                    yaw=angle + math.pi / 2 + rng.uniform(-0.15, 0.15))
    sides = 8 if level["div"] else 6
    radius = 0.5 if level["div"] else 0.78
    top_y = 0.52 if level["div"] else 0.58
    low = [bm.verts.new(at(fx + radius * math.cos(math.tau * k / sides), 0.0, fz + radius * math.sin(math.tau * k / sides)))
           for k in range(sides)]
    high = [bm.verts.new(at(fx + radius * math.cos(math.tau * k / sides), top_y, fz + radius * math.sin(math.tau * k / sides)))
            for k in range(sides)]
    centre = at(fx, 0.3, fz)
    tile = TILE[STONE]
    for k in range(sides):
        n = (k + 1) % sides
        quad = [low[k], low[n], high[n], high[k]]
        if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).dot((quad[0].co + quad[1].co) / 2 - centre) < 0:
            quad.reverse()
        face(bm, quad, STONE, [((v.co.x + v.co.y) / tile, v.co.z / tile) for v in quad], True)
    # The coals: a glowing disc, and the stone rim round it.
    ember_r = 0.36
    rim = [bm.verts.new(v.co) for v in high]
    inner = [bm.verts.new(at(fx + ember_r * math.cos(math.tau * k / sides), top_y + 0.02, fz + ember_r * math.sin(math.tau * k / sides)))
             for k in range(sides)]
    glow = [bm.verts.new(v.co) for v in inner]
    middle = bm.verts.new(at(fx, top_y + 0.05, fz))
    for k in range(sides):
        n = (k + 1) % sides
        quad = [rim[k], rim[n], inner[n], inner[k]]
        if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).z < 0:
            quad.reverse()
        face(bm, quad, STONE, [(v.co.x / tile, v.co.y / tile) for v in quad], True)
        tri = [glow[k], glow[n], middle]
        if (tri[1].co - tri[0].co).cross(tri[2].co - tri[1].co).z < 0:
            tri.reverse()
        face(bm, tri, EMBER, [(v.co.x, v.co.y) for v in tri], True)


def chimney(bm, level, rng):
    """The chimney near the ridge at the right end: courses of field stones round a core, under a cap slab."""
    cx, cz = CHIMNEY
    base = deck(cz + 0.45) - 0.25
    top_y = 4.12
    half = 0.42
    core = 0.34 if level["div"] else half + 0.06
    mass(bm, (cx - core, base, cz - core), (cx + core, top_y, cz + core), level, 8.0, cell=9.0, bulge=0.0)
    y = base + 0.14
    course = 0
    while y < top_y - 0.05:
        h = rng.uniform(0.25, 0.31)
        shift = 0.5 if course % 2 else 0.0
        for side in range(4):
            angle = side * math.pi / 2
            ax, az = math.cos(angle), math.sin(angle)      # along this side
            ox, oz = math.sin(angle), -math.cos(angle)     # outward
            for k in range(2):
                t = (k + shift) / 2 * 2 * half - half + half / 2
                centre = (cx + ax * t + ox * half * 0.92, y, cz + az * t + oz * half * 0.92)
                boulder(bm, centre, (rng.uniform(0.44, 0.52), h * 1.08, rng.uniform(0.3, 0.38)), level, rng,
                        yaw=angle + rng.uniform(-0.12, 0.12))
        y += h * 0.93
        course += 1
    if level["div"]:
        boulder(bm, (cx, top_y + 0.05, cz), (1.08, 0.14, 1.02), level, rng, yaw=0.12, flat=0.8)
    else:
        box(bm, (cx, top_y + 0.07, cz), (1.2, 0.16, 1.12), STONE, turn=Matrix.Rotation(0.12, 3, 'Z'))


def build(level_index, coll):
    level = LEVELS[level_index]
    rng = random.Random(7)
    bm = bmesh.new()

    # The floor: packed earth, out to the eave on the open side.
    box(bm, (0.0, 0.02, -0.25), (WALL_X * 2 - 0.2, 0.06, WALL_Z * 2 + 0.5), SOIL, grain='x')

    # Field stones under the walls: the back, both gables and the store's front; one under each post.
    footing(bm, (-WALL_X - 0.15, WALL_Z), (WALL_X + 0.15, WALL_Z), level, rng, (0, 1), 2.0)
    for sign in (-1, 1):
        footing(bm, (sign * WALL_X, -WALL_Z - 0.15), (sign * WALL_X, WALL_Z - 0.2), level, rng, (sign, 0), 3.0 + sign)
    footing(bm, (-WALL_X + 0.2, -WALL_Z), (STORE_X + 0.1, -WALL_Z), level, rng, (0, -1), 5.0)
    for x in (POST_X, WALL_X - 0.32):
        if level["div"]:
            boulder(bm, (x, 0.1, -WALL_Z), (0.6, 0.3, 0.52), level, rng, flat=0.3)
        else:
            mass(bm, (x - 0.28, 0.0, -WALL_Z - 0.25), (x + 0.28, 0.24, -WALL_Z + 0.25), level, 6.0, cell=9.0)

    # Log walls, the courses of one lying half a log above the next one's so the ends cross at the corners.
    course = LOG * 1.8
    rows = int((PLATE - FOOT) / course)
    for i in range(rows):
        y = FOOT + LOG * 0.9 + i * course
        log(bm, (-WALL_X - rng.uniform(0.25, 0.42), y, WALL_Z), (WALL_X + rng.uniform(0.25, 0.42), y, WALL_Z), LOG, level, rng)
        # The store's front, ending against the first post.
        log(bm, (-WALL_X - rng.uniform(0.25, 0.42), y, -WALL_Z), (STORE_X - 0.08, y, -WALL_Z), LOG, level, rng)
        yz = y + course / 2
        if yz + LOG <= PLATE + 0.1:
            for sign in (-1, 1):
                log(bm, (sign * WALL_X, yz, -WALL_Z - rng.uniform(0.25, 0.42)), (sign * WALL_X, yz, WALL_Z + rng.uniform(0.25, 0.42)),
                    LOG, level, rng)
            if level["small"]:
                # The store's inner wall, seen through the opening.
                log(bm, (STORE_X, yz, -WALL_Z + 0.1), (STORE_X, yz, WALL_Z - 0.1), LOG * 0.95, level, rng, sides=6)

    # The open side: posts on their stones, the plate log over them, and braces up to it.
    posts = (STORE_X, POST_X, WALL_X - 0.32)
    for x in posts:
        low = FOOT + 0.02 if x == STORE_X else 0.22
        log(bm, (x, low, -WALL_Z), (x, PLATE - 0.02, -WALL_Z), 0.16, level, rng, wobble=0.006)
    for z in (-WALL_Z, WALL_Z):
        log(bm, (-WALL_X - 0.45, PLATE + 0.13, z), (WALL_X + 0.45, PLATE + 0.13, z), 0.15, level, rng)
    if level["small"]:
        for x, sides_ in ((STORE_X, (1,)), (POST_X, (-1, 1)), (WALL_X - 0.32, (-1,))):
            for sign in sides_:
                log(bm, (x + sign * 0.1, 1.5, -WALL_Z), (x + sign * 0.78, PLATE + 0.02, -WALL_Z), 0.075, level, rng,
                    wobble=0.0, sides=6)

    # Gables of upright boards, cut to the roof.
    for x in (-WALL_X, WALL_X):
        boards = 14 if level["small"] else 4
        width = 2 * WALL_Z / boards
        for k in range(boards):
            z = -WALL_Z + width * (k + 0.5)
            top = deck(z) - 0.05  # at the board's middle; its upper end is cut to the roof's slope
            bottom = PLATE + 0.2
            if top - bottom > 0.06:
                box(bm, (x, (top + bottom) / 2, z), (0.04, top - bottom, width - 0.012), PLANKS, grain='y',
                    lift=lambda corner_x, corner_z, top=top: deck(corner_z) - 0.05 - top)

    # The ridge log and two purlins carry the roof; their ends show under the wind boards.
    log(bm, (-ROOF_X - 0.5, RIDGE - 0.2, 0.0), (ROOF_X + 0.5, RIDGE - 0.2, 0.0), 0.15, level, rng)
    if level["small"]:
        for z in (-1.25, 1.25):
            log(bm, (-ROOF_X + 0.02, deck(z) - 0.17, z), (ROOF_X - 0.02, deck(z) - 0.17, z), 0.11, level, rng)
    for sign in (-1, 1):
        sod(bm, sign, level, rng)

    chimney(bm, level, rng)
    forge(bm, level, rng)
    if level["small"]:
        anvil(bm, level, rng)
        barrel(bm, level)

    name = NAME if level_index == 0 else f"{NAME} LOD{level_index}"
    obj = mesh_object(name, bm, coll, MATERIALS)
    counts = {}
    for polygon in obj.data.polygons:
        counts[SLOTS[polygon.material_index]] = counts.get(SLOTS[polygon.material_index], 0) + len(polygon.vertices) - 2
    print("BUILT", name, sum(counts.values()), "triangles", len(obj.data.vertices), "vertices", counts)
    return obj


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


def materials():
    made = [scanned("Kit Wood", "rough_wood", tint=(1.0, 0.9, 0.78)),
            scanned("Kit Planks", "weathered_planks", tint=(1.0, 0.92, 0.82)),
            scanned("Kit Stone", "rock_wall_10", tint=(0.95, 1.0, 1.08), saturation=0.4),
            scanned("Kit Iron", "rust_coarse_01", tint=(0.42, 0.42, 0.46), saturation=0.3, metallic=0.85),
            flat("Kit Turf", os.path.join(TERRAIN, "Meadow Grass", "Meadow Grass_Color.jpg"), tint=(0.8, 0.68, 0.5))]
    ember, tree, bsdf = new_material("Kit Ember")
    bsdf.inputs["Base Color"].default_value = (0.05, 0.02, 0.01, 1.0)
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.28, 0.05, 1.0)
    bsdf.inputs["Emission Strength"].default_value = 6.0
    made.append(ember)
    made.append(scanned("Kit Boulder", "rock_boulder_dry", tint=(0.95, 0.97, 1.0), saturation=0.7))
    made.append(flat("Kit Soil", os.path.join(TERRAIN, "Dark Humus", "Dark Humus_Color.jpg")))
    made.append(scanned("Kit Birch", "weathered_planks", tint=(2.6, 2.5, 2.3), saturation=0.25))
    return made


MATERIALS = []


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATERIALS.extend(materials())
    coll = collection(NAME)
    levels = [build(i, coll) for i in range(3)]
    os.makedirs(ART, exist_ok=True)
    select_only(levels)
    bpy.ops.export_scene.fbx(filepath=os.path.join(ART, f"{NAME}.fbx"), use_selection=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
    if "--no-preview" not in args:
        stage([(levels, 11.0)])
        camera((-9.0, 19.0, 13.0), (0.0, 0.0, 1.6), lens=40)  # the three levels, from about the game's pitch
        render(PREVIEW.replace(".png", "_lods.png"), 1920, 1080)
        levels[1].hide_render = levels[2].hide_render = True
        x = levels[0].location.x
        # The game's camera looks along +z (Blender's -Y) from 48 degrees up: it stands on Blender's +Y side.
        camera((x, 13.4, 14.9), (x, 0.0, 1.0), lens=40)
        render(PREVIEW, 1920, 1080)
        camera((x + 4.5, 9.0, 2.6), (x - 0.3, 0.0, 1.3), lens=30)  # standing at the open side, as in the concept
        render(PREVIEW.replace(".png", "_front.png"), 1600, 900)
        levels[1].hide_render = levels[2].hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "smithy.blend"))
    print("SMITHY DONE")


if __name__ == "__main__":
    main()
