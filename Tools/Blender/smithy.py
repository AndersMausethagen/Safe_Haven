"""The Smithy, built from the Haven's building kit: a Norse log smithy with an open front and a turf roof.

This is the kit-built way of making a building (the other is a model generated whole by LocalSpawn, see
Tools/LocalSpawn/structures_manifest.json "Smithy Generated"): the shape is modelled here from a few kinds of part
(logs, boards, stone masses, a turf roof), and every part is mapped at its real size with one of the kit's tiling
materials (Assets/_Project/Art/Haven/Kit/Textures, CC0 scans from Poly Haven; the turf is the terrain's own meadow
grass). Nothing is baked: a 6 m building keeps the scans' full sharpness, the mesh is closed and exactly on budget, and
every building made this way shares the same few textures.

The layout is the placeholder's (Art/Haven/Models/Smithy Model.prefab), so the upgrade tiers' parts still sit on it:
6.2 x 5.0 m, open front on +Z (Unity), the ridge along Z at 3.8 m, the forge and its chimney in the back left corner
(chimney top at 4.4 m), the anvil on its stump at (0.9, 0.2).

Writes Assets/_Project/Art/Haven/Structures/Smithy Kit/Smithy Kit.fbx: "Smithy Kit" (LOD0), "Smithy Kit LOD1" and
"Smithy Kit LOD2", with the material slots Kit Wood, Kit Planks, Kit Stone, Kit Iron, Kit Turf and Kit Ember
(AgentScripts/BuildStructureModels.cs gives them the game's materials), a preview render and smithy.blend.

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
TURF = os.path.join(ROOT, "Assets", "_Project", "Art", "Terrain", "World", "Meadow Grass")

# Material slots, and how many metres one repeat of each texture covers (the scans' real size).
WOOD, PLANKS, STONE, IRON, TURF_M, EMBER = range(6)
SLOTS = ["Kit Wood", "Kit Planks", "Kit Stone", "Kit Iron", "Kit Turf", "Kit Ember"]
TILE = {WOOD: 0.5, PLANKS: 2.0, STONE: 1.8, IRON: 2.2, TURF_M: 2.5, EMBER: 1.0}

# The building, in the game's axes (x right, y up, z towards the open front). at() turns them into Blender's.
WALL_X, WALL_Z = 3.0, 2.4       # the walls' centre lines
WALL_TOP = 2.5
RIDGE, EAVE_X, EAVE_DROP = 3.75, 3.55, 1.45  # the roof deck: from the ridge down to the eave (22 degrees)
ROOF_Z = 2.75
LOG = 0.15

# Detail per level: sides of a log, rings along it, the stone grid and turf grid in metres, and small parts or not.
LEVELS = [dict(sides=12, rings=3, stone=0.3, turf=0.25, small=True),
          dict(sides=7, rings=1, stone=0.7, turf=0.7, small=True),
          dict(sides=5, rings=1, stone=9.0, turf=9.0, small=False)]


def at(x, y, z):
    """A point in the game's axes as Blender's: the FBX export turns Blender's -Y into the game's +Z and mirrors X."""
    return Vector((-x, -z, y))


def deck(x):
    """Height of the roof deck's top at x."""
    return RIDGE - EAVE_DROP * abs(x) / EAVE_X


# ----- Parts ----------------------------------------------------------------------------------------------------------

def face(bm, verts, material, uvs, smooth):
    f = bm.faces.new(verts)
    f.material_index = material
    f.smooth = smooth
    layer = bm.loops.layers.uv.verify()
    for loop, uv in zip(f.loops, uvs):
        loop[layer].uv = uv
    return f


def log(bm, start, end, radius, level, rng, material=WOOD, wobble=0.012, uneven=0.07, cap=0.025):
    """A round log from start to end (game axes): a little crooked and uneven, its end grain slightly domed.
    The bark texture's grain runs along it, wrapped a whole number of times so it has no seam."""
    a, b = at(*start), at(*end)
    axis = b - a
    length = axis.length
    axis.normalize()
    side = axis.orthogonal().normalized()
    up = axis.cross(side)
    sides, rings = level["sides"], level["rings"]
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
    lift(game x of the corner) raises each upper corner by that much (a board cut to the roof's slope)."""
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
                    p[2] += lift(centre[0] - p[0])
                corners.append(Vector(p))
            normal = Vector([sign if i == axis else 0.0 for i in range(3)])
            if (corners[1] - corners[0]).cross(corners[2] - corners[1]).dot(normal) < 0:
                corners.reverse()
            verts = [bm.verts.new(origin + turn @ c) for c in corners]
            off = origin  # the texture is laid in the world, so boards side by side don't repeat each other
            uvs = [((c[u_axis] + off[u_axis]) / tile, (c[v_axis] + off[v_axis]) / tile) for c in corners]
            face(bm, verts, material, uvs, False)


def mass(bm, low, high, level, seed, material=STONE, top=None, bulge=0.035, open_bottom=True):
    """Masonry: a block from low to high (game axes), or tapering to the rectangle `top` (x0, z0, x1, z1) at its
    upper end, its faces a grid pushed in and out so the stones stand proud. Corners stay hard, faces are smooth."""
    cell = level["stone"]
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


def turf(bm, sign, level, rng):
    """One slope of the roof: boards underneath, sod on top (uneven, sagging over the eave), from the ridge to the eave."""
    slope = math.hypot(EAVE_X, EAVE_DROP)
    pitch = math.atan2(EAVE_DROP, EAVE_X)
    # The deck: boards running down the slope.
    mid_x = sign * EAVE_X / 2
    turn = Matrix.Rotation(-sign * pitch, 3, 'Y')  # about Blender's Y (the ridge's direction), x mirrored by at()
    box(bm, (mid_x, deck(mid_x) - 0.03, 0.0), (slope, 0.06, ROOF_Z * 2), PLANKS, turn=turn, grain='x')
    # The sod.
    cell = level["turf"]
    na, nb = max(1, round(slope / cell)), max(1, round(ROOF_Z * 2 / cell))
    tile = TILE[TURF_M]
    thick = 0.26

    def top(a, b):
        """The sod's surface, a from the ridge (0) to the eave (1), b from the back gable to the front: thinner
        towards its three open edges, and lumpy. Both slopes get the same heights along the ridge."""
        x = sign * EAVE_X * a * 1.012
        z = (-ROOF_Z + 2 * ROOF_Z * b) * 1.012
        inside = min(1.0, min(1 - a, b, 1 - b) * 6.0) if cell < 5.0 else 1.0
        height = thick * (0.5 + 0.5 * inside)
        if cell < 5.0:
            height += inside * (0.13 * noise.noise(Vector((x * 0.8, z * 0.8, 3.0))) + 0.05 * noise.noise(Vector((x * 2.3, z * 2.3, 9.0))))
        return Vector((x, deck(x / 1.012) + max(0.06, height), z))

    grid = [[top(i / na, j / nb) for i in range(na + 1)] for j in range(nb + 1)]
    verts = [[bm.verts.new(at(*p)) for p in row] for row in grid]
    for j in range(nb):
        for i in range(na):
            quad = [verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]]
            points = [grid[j][i], grid[j][i + 1], grid[j + 1][i + 1], grid[j + 1][i]]
            if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).z < 0:
                quad.reverse()
                points.reverse()
            face(bm, quad, TURF_M, [(p.x / tile, p.z / tile) for p in points], True)
    # Its edge, down to the deck: along the eave and up both gables.
    rim = [(grid[j][na], j) for j in range(nb + 1)]
    rim += [(grid[nb][i], nb + na - i) for i in range(na - 1, -1, -1)]
    back = [(grid[0][i], i) for i in range(na + 1)]
    for edge in (rim, back):
        for (p, k), (q, _) in zip(edge, edge[1:]):
            low_p = Vector((p.x, deck(p.x / 1.012) - 0.02, p.z))
            low_q = Vector((q.x, deck(q.x / 1.012) - 0.02, q.z))
            quad = [bm.verts.new(at(*v)) for v in (p, q, low_q, low_p)]
            outward = at(*(((p + q) / 2) - Vector((sign * EAVE_X * 0.5, (p.y + q.y) / 2, 0.0))))
            if (quad[1].co - quad[0].co).cross(quad[2].co - quad[1].co).dot(outward) < 0:
                quad.reverse()
            # The same sod, seen from the side: a thin strip of the texture.
            face(bm, quad, TURF_M, [((v.co.x + v.co.y) / tile, v.co.z / tile) for v in quad], True)
    # The log that holds the sod at the eave, on a few wooden hooks.
    eave_x = sign * (EAVE_X + 0.06)
    log(bm, (eave_x, deck(EAVE_X) + 0.07, -ROOF_Z - 0.12), (eave_x, deck(EAVE_X) + 0.07, ROOF_Z + 0.12), 0.085, level, rng)
    if level["small"]:
        # Wind boards on both gables, crossing at the ridge.
        for z in (-ROOF_Z - 0.05, ROOF_Z + 0.05):
            box(bm, (mid_x * 1.03, deck(mid_x) + 0.07, z), (slope + 0.25, 0.26, 0.04), PLANKS, turn=turn, grain='x')


def anvil(bm, level, rng):
    """The anvil on its stump: stump of bark and end grain, the anvil in a few blocks and a horn."""
    x, z = 0.9, 0.2
    log(bm, (x, 0.1, z), (x, 0.58, z), 0.3, level, rng, uneven=0.03, wobble=0.0, cap=0.01)
    box(bm, (x, 0.615, z), (0.44, 0.07, 0.27), IRON)
    box(bm, (x, 0.69, z), (0.24, 0.09, 0.17), IRON)
    box(bm, (x - 0.03, 0.775, z), (0.52, 0.09, 0.21), IRON)
    if level["small"]:
        # The horn: a cone off the face's end.
        tip, base, r, n = at(x + 0.5, 0.78, z), at(x + 0.23, 0.775, z), 0.045, 6
        ring = [bm.verts.new(base + Vector((0.0, math.cos(2 * math.pi * k / n) * 0.1, math.sin(2 * math.pi * k / n) * r)))
                for k in range(n)]
        point = bm.verts.new(tip)
        for k in range(n):
            tri = [ring[k], ring[(k + 1) % n], point]
            if (tri[1].co - tri[0].co).cross(tri[2].co - tri[1].co).dot(tri[0].co + tri[1].co - base * 2) < 0:
                tri.reverse()
            face(bm, tri, IRON, [(v.co.x / TILE[IRON], (v.co.y + v.co.z) / TILE[IRON]) for v in tri], True)


def build(level_index, coll):
    level = LEVELS[level_index]
    rng = random.Random(7)
    bm = bmesh.new()

    # The ground it stands on: a flagged floor, and a fieldstone footing under the three walls.
    mass(bm, (-3.1, 0.0, -2.5), (3.1, 0.12, 2.5), level, 1.0, bulge=0.012)
    foot = 0.4
    mass(bm, (-3.32, 0.0, -2.68), (3.32, foot, -2.12), level, 2.0)
    for sign in (-1, 1):
        x0, x1 = sorted((sign * 2.72, sign * 3.32))
        mass(bm, (x0, 0.0, -2.12), (x1, foot, 2.68), level, 3.0 + sign)

    # Log walls: the back and the two sides, the courses of one lying half a log above the other's so the ends lock.
    course = LOG * 1.8
    for i in range(8):
        y = foot + LOG * 0.9 + i * course
        if y + LOG > WALL_TOP + 0.08:
            break
        out = rng.uniform(0.22, 0.4)
        log(bm, (-WALL_X - out, y, -WALL_Z), (WALL_X + rng.uniform(0.22, 0.4), y, -WALL_Z), LOG, level, rng)
    for sign in (-1, 1):
        for i in range(8):
            y = foot + LOG * 0.9 + (i + 0.5) * course
            if y + LOG > deck(WALL_X) - 0.02:
                break
            log(bm, (sign * WALL_X, y, -WALL_Z - rng.uniform(0.22, 0.4)), (sign * WALL_X, y, WALL_Z + rng.uniform(0.12, 0.3)),
                LOG, level, rng)
        # The open front: a post at each corner, on a stone.
        log(bm, (sign * WALL_X, 0.1, WALL_Z + 0.36), (sign * WALL_X, 2.24, WALL_Z + 0.36), 0.17, level, rng, wobble=0.006)
    log(bm, (-WALL_X - 0.4, 2.38, WALL_Z + 0.36), (WALL_X + 0.4, 2.38, WALL_Z + 0.36), 0.16, level, rng)

    # Gables of upright boards, front and back, under the roof.
    for z in (-WALL_Z, WALL_Z + 0.36):
        boards = 12 if level["small"] else 4
        width = 2 * WALL_X / boards
        for k in range(boards):
            x = -WALL_X + width * (k + 0.5)
            top = deck(x) - 0.05  # at the board's middle; its upper end is cut to the roof's slope
            bottom = 2.5
            if top - bottom > 0.08:
                box(bm, (x, (top + bottom) / 2, z), (width - 0.012, top - bottom, 0.035), PLANKS, grain='y',
                    lift=lambda corner_x, top=top: deck(corner_x) - 0.05 - top)

    # Purlins from gable to gable carry the roof; their ends show under the wind boards.
    for x in (0.0, -1.25, 1.25, -2.45, 2.45):
        if x == 0.0 or level["small"]:
            log(bm, (x, deck(x) - 0.06 - 0.11, -ROOF_Z + 0.02), (x, deck(x) - 0.06 - 0.11, ROOF_Z + 0.25), 0.11, level, rng)
    for sign in (-1, 1):
        turf(bm, sign, level, rng)

    # The forge in the back left corner: a stone hearth with its bed of coals, a hood over it and the chimney.
    mass(bm, (-2.5, 0.1, -2.2), (-0.7, 1.0, -1.0), level, 5.0)
    for a, b in (((-2.5, 1.0, -1.16), (-0.7, 1.12, -1.0)), ((-2.5, 1.0, -1.5), (-2.34, 1.12, -1.16)),
                 ((-0.86, 1.0, -1.5), (-0.7, 1.12, -1.16))):
        mass(bm, a, b, level, 6.0, bulge=0.012)
    box(bm, (-1.6, 1.035, -1.34), (1.48, 0.07, 0.36), EMBER)
    mass(bm, (-2.4, 1.0, -2.2), (-0.8, 1.9, -1.5), level, 7.0, top=(-2.15, -2.2, -1.05, -1.5))
    mass(bm, (-2.15, 1.9, -2.2), (-1.05, 4.25, -1.5), level, 8.0, top=(-2.08, -2.2, -1.12, -1.55))
    mass(bm, (-2.17, 4.25, -2.26), (-1.03, 4.4, -1.46), level, 9.0, bulge=0.015, open_bottom=False)

    anvil(bm, level, rng)

    name = NAME if level_index == 0 else f"{NAME} LOD{level_index}"
    obj = mesh_object(name, bm, coll, MATERIALS)
    print("BUILT", name, sum(len(p.vertices) - 2 for p in obj.data.polygons), "triangles", len(obj.data.vertices), "vertices")
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


def materials():
    made = [scanned("Kit Wood", "rough_wood", tint=(0.8, 0.72, 0.62)),
            scanned("Kit Planks", "weathered_planks", tint=(0.9, 0.82, 0.74)),
            scanned("Kit Stone", "rock_wall_10", tint=(0.85, 0.87, 0.9), saturation=0.35),
            scanned("Kit Iron", "rust_coarse_01", tint=(0.42, 0.42, 0.46), saturation=0.3, metallic=0.85)]
    grass, tree, bsdf = new_material("Kit Turf")
    colour = node(tree, "ShaderNodeTexImage", (-400, 200))
    colour.image = image(os.path.join(TURF, "Meadow Grass_Color.jpg"), 'sRGB')
    tree.links.new(colour.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.95
    made.append(grass)
    ember, tree, bsdf = new_material("Kit Ember")
    bsdf.inputs["Base Color"].default_value = (0.05, 0.02, 0.01, 1.0)
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.28, 0.05, 1.0)
    bsdf.inputs["Emission Strength"].default_value = 6.0
    made.append(ember)
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
    stage([(levels, 10.0)])
    camera((-9.0, -19.0, 13.0), (0.0, 0.0, 1.6), lens=40)  # the three levels, from about the game's pitch
    render(PREVIEW.replace(".png", "_lods.png"), 1920, 1080)
    levels[1].hide_render = levels[2].hide_render = True
    x = levels[0].location.x
    camera((x - 5.5, -11.5, 12.0), (x, 0.0, 1.5), lens=40)  # the game's view: 48 degrees down, from the front left
    render(PREVIEW, 1920, 1080)
    camera((x - 1.5, -8.5, 2.2), (x + 0.4, 0.0, 1.5), lens=28)  # standing at the open front, looking in
    render(PREVIEW.replace(".png", "_front.png"), 1600, 900)
    levels[1].hide_render = levels[2].hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "smithy.blend"))
    print("SMITHY DONE")


if __name__ == "__main__":
    main()
