"""What the kit-built buildings share beyond the bare parts of building_kit.py: a whole log house in one call, doors
and shutters, and the things that stand round a working building (benches, stacks of wood, racks, sacks, a loom).

Everything is drawn in the design's axes (see building_kit.py): x right, y up, the camera's side at -z.
"""
import math
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import (BIRCH, CLOTH, IRON, LOG, PLANKS, SOIL, STONE, WOOD, Roof, board_gable, boulder, box, brace,  # noqa: E402
                          footing, log, log_wall, post_stone, sod_roof)

FOOT = 0.3


def house_roof(half_x, half_z, plate, over_x=0.55, over_z=0.6, pitch=28.0):
    """The roof that fits log walls half_x by half_z with their top at `plate`: it overhangs the gables by over_x and
    the long walls by over_z, at `pitch` degrees."""
    eave_z = half_z + over_z
    drop = eave_z * math.tan(math.radians(pitch))
    return Roof(ridge=plate + 0.17 + drop * half_z / eave_z, eave_z=eave_z, drop=drop, half_x=half_x + over_x)


def log_house(bm, level, rng, half_x, half_z, plate, roof, opening=None, posts=(), apron=0.5, purlins=None, foot=FOOT):
    """A log house: field stones under the walls, four log walls with crossed corners, board gables and the sod roof.
    opening (x0, x1): the stretch of the front wall (-z) left open under the eave; posts stand at its ends and at
    the x in `posts`, on stones, with braces up to the plate log. The earth floor reaches `apron` out at the front."""
    box(bm, (0.0, 0.02, -apron / 2), (half_x * 2 - 0.2, 0.06, half_z * 2 + apron), SOIL, grain='x')
    x0, x1 = opening if opening else (0.0, 0.0)
    left = not opening or x0 > -half_x + 0.6      # a closed stretch of front wall left of the opening
    right = not opening or x1 < half_x - 0.6
    footing(bm, (-half_x - 0.15, half_z), (half_x + 0.15, half_z), level, rng, (0, 1), 2.0, foot)
    for sign in (-1, 1):
        footing(bm, (sign * half_x, -half_z - 0.15), (sign * half_x, half_z - 0.2), level, rng, (sign, 0), 3.0 + sign, foot)
    log_wall(bm, (-half_x, half_z), (half_x, half_z), foot, plate, level, rng)
    for sign in (-1, 1):
        log_wall(bm, (sign * half_x, -half_z), (sign * half_x, half_z), foot, plate, level, rng, half=True)
    if not opening:
        footing(bm, (-half_x + 0.2, -half_z), (half_x - 0.2, -half_z), level, rng, (0, -1), 5.0, foot)
        log_wall(bm, (-half_x, -half_z), (half_x, -half_z), foot, plate, level, rng)
    else:
        if left:
            footing(bm, (-half_x + 0.2, -half_z), (x0 + 0.1, -half_z), level, rng, (0, -1), 5.0, foot)
            log_wall(bm, (-half_x, -half_z), (x0 - 0.08, -half_z), foot, plate, level, rng, over_end=(0.0, 0.0))
        else:
            x0 = -half_x + 0.32
        if right:
            footing(bm, (x1 - 0.1, -half_z), (half_x - 0.2, -half_z), level, rng, (0, -1), 6.0, foot)
            log_wall(bm, (half_x, -half_z), (x1 + 0.08, -half_z), foot, plate, level, rng, over_end=(0.0, 0.0))
        else:
            x1 = half_x - 0.32
        stand = [(x0, left, (1,)), (x1, right, (-1,))] + [(x, False, (-1, 1)) for x in posts]
        for x, on_wall, sides in stand:
            if not on_wall:
                post_stone(bm, x, -half_z, level, rng)
            log(bm, (x, foot + 0.02 if on_wall else 0.22, -half_z), (x, plate - 0.02, -half_z), 0.16, level, rng, wobble=0.006)
            if level["small"]:
                for sign in sides:
                    brace(bm, x, -half_z, sign, plate - 0.8, plate + 0.02, level, rng)
    for z in (-half_z, half_z):
        log(bm, (-half_x - 0.45, plate + 0.13, z), (half_x + 0.45, plate + 0.13, z), 0.15, level, rng)
    for x in (-half_x, half_x):
        board_gable(bm, x, half_z, plate + 0.2, roof, level)
    sod_roof(bm, roof, level, rng, purlins=purlins if purlins is not None else (-half_z * 0.6, half_z * 0.6))


def front(half_z):
    """z of a log wall's outer face on the camera's side."""
    return -half_z - LOG * 0.92


def door(bm, x, half_z, level, rng, width=0.95, height=1.85, foot=FOOT):
    """A plank door on the front wall at x: boards, two iron hinges, a frame and a flat stone for a step."""
    z = front(half_z)
    box(bm, (x, foot + height / 2, z - 0.03), (width, height, 0.07), PLANKS, grain='y')
    if level["small"]:
        for y in (foot + height * 0.25, foot + height * 0.75):
            box(bm, (x - width * 0.12, y, z - 0.075), (width * 0.7, 0.07, 0.02), IRON)
        for sign in (-1, 1):
            box(bm, (x + sign * (width / 2 + 0.06), foot + height / 2, z - 0.04), (0.12, height + 0.12, 0.12), WOOD, grain='y')
        box(bm, (x, foot + height + 0.07, z - 0.04), (width + 0.34, 0.14, 0.14), WOOD, grain='x')
    boulder(bm, (x, 0.05, z - 0.36), (width + 0.1, 0.2, 0.5), level, rng, yaw=0.0, flat=0.6)


def shutters(bm, x, y, half_z, level, width=0.75, height=0.6):
    """A small window on the front wall: two plank shutters folded back beside its dark opening."""
    z = front(half_z)
    box(bm, (x, y, z - 0.01), (width, height, 0.04), IRON)  # the dark of the room
    if level["small"]:
        for sign in (-1, 1):
            box(bm, (x + sign * (width * 0.75 + 0.03), y, z - 0.03), (width / 2, height + 0.06, 0.04), PLANKS, grain='y')
        box(bm, (x, y - height / 2 - 0.05, z - 0.05), (width + 0.16, 0.07, 0.12), WOOD, grain='x')


def bench(bm, x, z, length, level, rng, height=0.42):
    """A bench: half a split log on two stubs, along x."""
    box(bm, (x, height, z), (length, 0.1, 0.34), WOOD, grain='x')
    for sign in (-1, 1):
        log(bm, (x + sign * (length / 2 - 0.2), 0.0, z), (x + sign * (length / 2 - 0.2), height - 0.05, z), 0.12, level, rng, wobble=0.0, sides=6)


def table(bm, x, z, length, depth, level, rng, height=0.85):
    """A workbench or counter: a thick plank top on two trestles, along x."""
    box(bm, (x, height, z), (length, 0.09, depth), PLANKS, grain='x')
    for sign in (-1, 1):
        box(bm, (x + sign * (length / 2 - 0.25), height / 2 - 0.02, z), (0.1, height - 0.05, depth * 0.85), WOOD, grain='y')


def firewood(bm, x0, x1, z, level, rng, rows=3, radius=0.09, depth=0.45):
    """A stack of split firewood against a wall, seen end-on: rows of short logs lying along z."""
    y = radius
    for row in range(rows):
        x = x0 + radius + (radius if row % 2 else 0.0)
        while x < x1 - radius * (1 + row % 2):
            log(bm, (x, y, z - depth / 2), (x, y, z + depth / 2), radius * rng.uniform(0.85, 1.05), level, rng, wobble=0.0,
                sides=5 if level["sides"] > 6 else 4, cap=0.0)
            x += radius * 2.02
        y += radius * 1.75


def log_pile(bm, x, z, length, level, rng, rows=3, radius=0.17):
    """A pile of felled logs lying along x, each row one fewer."""
    for row in range(rows):
        for i in range(rows - row):
            cz = z + (i - (rows - row - 1) / 2) * radius * 2.05
            start = x - length / 2 + rng.uniform(-0.15, 0.15)
            log(bm, (start, radius + row * radius * 1.75, cz), (start + length * rng.uniform(0.9, 1.05), radius + row * radius * 1.75, cz),
                radius * rng.uniform(0.85, 1.1), level, rng, sides=7 if level["sides"] > 6 else 5)


def plank_stack(bm, x, z, size, layers, rng):
    """Sawn planks stacked on two bearers, along x; size is the whole stack's (x, y, z)."""
    lift = 0.08
    for sign in (-1, 1):
        box(bm, (x + sign * size[0] * 0.3, lift / 2, z), (0.1, lift, size[2]), WOOD, grain='z')
    each = size[1] / layers
    for i in range(layers):
        box(bm, (x + rng.uniform(-0.04, 0.04), lift + each * (i + 0.5), z + rng.uniform(-0.02, 0.02)),
            (size[0], each * 0.86, size[2]), PLANKS, grain='x')


def chopping_block(bm, x, z, level, rng, axe=True):
    """A stump with an axe standing in it."""
    log(bm, (x, 0.0, z), (x, 0.45, z), 0.27, level, rng, uneven=0.05, wobble=0.0, cap=0.01)
    if axe and level["small"]:
        log(bm, (x - 0.02, 0.5, z), (x + 0.3, 1.05, z - 0.05), 0.025, level, rng, wobble=0.0, sides=4)
        box(bm, (x - 0.02, 0.5, z), (0.2, 0.14, 0.04), IRON, turn=Matrix.Rotation(0.5, 3, 'Y'))


def sawhorse(bm, x, z, length, level, rng, height=0.7):
    """A trestle with a log lying on it, along x."""
    for sign in (-1, 1):
        for lean in (-1, 1):
            log(bm, (x + sign * length * 0.32, 0.0, z + lean * 0.32), (x + sign * length * 0.32, height, z), 0.05, level, rng,
                wobble=0.0, sides=4)
    log(bm, (x - length / 2, height + 0.14, z), (x + length / 2, height + 0.14, z), 0.15, level, rng, sides=7 if level["sides"] > 6 else 5)


def grindstone(bm, x, z, level, rng):
    """A round grindstone in a wooden frame, its wheel across x."""
    for sign in (-1, 1):
        box(bm, (x + sign * 0.14, 0.38, z), (0.07, 0.76, 0.5), WOOD, grain='y')
    log(bm, (x - 0.07, 0.72, z), (x + 0.07, 0.72, z), 0.36, level, rng, material=STONE, wobble=0.0, uneven=0.0, cap=0.0)


def sack(bm, x, z, level, rng, material=None, size=(0.5, 0.62, 0.42)):
    """A bulging sack standing on the ground."""
    from building_kit import LINEN
    boulder(bm, (x, size[1] / 2 - 0.03, z), size, level, rng, material=material or LINEN)


def pole_rack(bm, x, z, length, level, rng, height=1.7, hang=None, count=6):
    """Two trestles and a pole along x, with strips of something hung over it (hides, fish, cloth: a material)."""
    for sign in (-1, 1):
        for lean in (-1, 1):
            log(bm, (x + sign * length / 2, 0.0, z + lean * 0.35), (x + sign * length / 2, height + 0.08, z), 0.045, level, rng,
                wobble=0.0, sides=4)
    log(bm, (x - length / 2 - 0.15, height, z), (x + length / 2 + 0.15, height, z), 0.04, level, rng, wobble=0.0, sides=5)
    if hang is not None and level["small"]:
        for i in range(count):
            drop = rng.uniform(0.45, 0.8)
            cx = x - length / 2 + length * (i + 0.5) / count
            material = hang[i % len(hang)] if isinstance(hang, (list, tuple)) else hang
            box(bm, (cx, height - drop / 2 - 0.02, z), (length / count * 0.7, drop, 0.03), material)


def hide_frame(bm, x, z, level, rng, width=1.3, height=1.5, lean=0.35):
    """A hide laced into a pole frame that leans back against something (a wall)."""
    from building_kit import HIDE
    for sign in (-1, 1):
        log(bm, (x + sign * width / 2, 0.0, z), (x + sign * width / 2, height, z + lean), 0.04, level, rng, wobble=0.0, sides=4)
    for t in (0.1, 1.0):
        log(bm, (x - width / 2 - 0.08, height * t, z + lean * t), (x + width / 2 + 0.08, height * t, z + lean * t), 0.035, level, rng,
            wobble=0.0, sides=4)
    turn = Matrix.Rotation(-math.atan2(lean, height), 3, 'X')
    box(bm, (x, height * 0.55, z + lean * 0.55 - 0.02), (width * 0.74, height * 0.72, 0.02), HIDE, turn=turn)


def loom(bm, x, z, level, rng, width=1.5, height=1.9, lean=0.4):
    """An upright loom leaning against the wall behind it: two posts, the beam, the cloth and a row of stone weights."""
    from building_kit import LINEN
    for sign in (-1, 1):
        log(bm, (x + sign * width / 2, 0.0, z), (x + sign * width / 2, height, z + lean), 0.055, level, rng, wobble=0.0, sides=5)
    log(bm, (x - width / 2 - 0.15, height - 0.1, z + lean * 0.95), (x + width / 2 + 0.15, height - 0.1, z + lean * 0.95), 0.06, level, rng,
        wobble=0.0, sides=6)
    turn = Matrix.Rotation(-math.atan2(lean, height), 3, 'X')
    box(bm, (x, height * 0.62, z + lean * 0.62 - 0.02), (width * 0.8, height * 0.6, 0.02), LINEN, turn=turn)
    if level["small"]:
        for i in range(6):
            boulder(bm, (x - width * 0.35 + width * 0.7 * i / 5, 0.22, z + 0.06), (0.14, 0.18, 0.12), level, rng)


def awning(bm, x0, x1, z_wall, reach, high, low, level, rng, material=CLOTH):
    """A cloth stretched from the wall at `high` out to two poles at `low`."""
    slope = math.hypot(reach, high - low)
    turn = Matrix.Rotation(-math.atan2(high - low, reach), 3, 'X')
    box(bm, ((x0 + x1) / 2, (high + low) / 2, z_wall - reach / 2), (x1 - x0, 0.025, slope), material, turn=turn)
    for x in (x0 + 0.08, x1 - 0.08):
        log(bm, (x, 0.0, z_wall - reach + 0.05), (x, low + 0.05, z_wall - reach + 0.05), 0.05, level, rng, wobble=0.0, sides=5)


def shield(bm, x, y, z, level, rng, radius=0.42):
    """A round shield leaning against a wall behind it (towards +z), its face to the camera."""
    log(bm, (x, y, z), (x, y + 0.02, z + 0.06), radius, level, rng, material=PLANKS, wobble=0.0, uneven=0.0, cap=0.0,
        sides=8 if level["sides"] > 6 else 6)
    log(bm, (x, y - 0.01, z - 0.05), (x, y, z), radius * 0.22, level, rng, material=IRON, wobble=0.0, uneven=0.0, cap=0.03, sides=6)


def antlers(bm, x, y, z, level, rng):
    """A pair of antlers on the wall."""
    for sign in (-1, 1):
        log(bm, (x, y, z), (x + sign * 0.3, y + 0.25, z - 0.12), 0.025, level, rng, material=BIRCH, wobble=0.0, sides=4)
        log(bm, (x + sign * 0.3, y + 0.25, z - 0.12), (x + sign * 0.42, y + 0.55, z - 0.1), 0.02, level, rng, material=BIRCH, wobble=0.0, sides=4)
        log(bm, (x + sign * 0.2, y + 0.17, z - 0.08), (x + sign * 0.2, y + 0.42, z - 0.16), 0.017, level, rng, material=BIRCH, wobble=0.0, sides=4)
