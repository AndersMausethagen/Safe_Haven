"""The Smithy: a long, low Norse log smithy under a sod roof, built from the Haven's building kit (building_kit.py).

The design follows its concept image (Tools/LocalSpawn/Structures/Smithy/concept.png): the ridge runs along the
building, the long side towards the camera is open between posts with braces, with a closed store at its left end;
log walls with crossed corners stand on a row of field stones; the gables are upright boards; birch bark shows pale
under the sod along the eaves and gables; a chimney of stones with a cap slab stands near the ridge at the right end;
a round stone forge, the anvil on its stump and the quench barrel stand at the opening, where the game's camera sees
them under the eave.

What each tier adds (the game switches these on as the Smithy is upgraded): II copper on the anvil and a crate of
ingots, III a quench trough, an iron cap over the chimney and a lantern, IV moonsilver in the coals and on the ridge
log's ends, V a trollstone anvil and a banner.

Writes Assets/_Project/Art/Haven/Structures/Smithy/Smithy.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/smithy.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import (COPPER, IRON, LOG, MOONSILVER, SOIL, TROLLSTONE, WATER, WOOD, Roof, anvil, banner, barrel,  # noqa: E402
                          board_gable, boulder, box, brace, build, chimney, crate, disc, footing, lantern, log, log_wall,
                          post_stone, round_forge, sod_roof)

NAME = "Smithy"
WALL_X, WALL_Z = 3.4, 2.0        # the walls' centre lines
PLATE = 2.3                      # top of the log walls
ROOF = Roof(ridge=3.55, eave_z=2.6, drop=1.4, half_x=3.95)  # 28 degrees
FOOT = 0.3                       # the stone footing's height
STORE_X = -1.3                   # the closed store's inner wall, and the first post
POST_X = 0.95
CORNER_X = WALL_X - 0.32
FORGE = (-0.1, -1.15)
ANVIL = (-1.85, -2.8)
BARREL = (2.45, -2.4)
CHIMNEY = (2.5, 0.8)
CHIMNEY_TOP = 4.12


def shape(bm, level, rng):
    # The floor: packed earth, out to the eave on the open side.
    box(bm, (0.0, 0.02, -0.25), (WALL_X * 2 - 0.2, 0.06, WALL_Z * 2 + 0.5), SOIL, grain='x')

    # Field stones under the walls: the back, both gables and the store's front; one under each post.
    footing(bm, (-WALL_X - 0.15, WALL_Z), (WALL_X + 0.15, WALL_Z), level, rng, (0, 1), 2.0, FOOT)
    for sign in (-1, 1):
        footing(bm, (sign * WALL_X, -WALL_Z - 0.15), (sign * WALL_X, WALL_Z - 0.2), level, rng, (sign, 0), 3.0 + sign, FOOT)
    footing(bm, (-WALL_X + 0.2, -WALL_Z), (STORE_X + 0.1, -WALL_Z), level, rng, (0, -1), 5.0, FOOT)
    for x in (POST_X, CORNER_X):
        post_stone(bm, x, -WALL_Z, level, rng)

    # Log walls, the gables' courses lying half a log above the long walls' so the ends cross at the corners.
    log_wall(bm, (-WALL_X, WALL_Z), (WALL_X, WALL_Z), FOOT, PLATE, level, rng)
    log_wall(bm, (-WALL_X, -WALL_Z), (STORE_X - 0.08, -WALL_Z), FOOT, PLATE, level, rng, over_end=(0.0, 0.0))
    for sign in (-1, 1):
        log_wall(bm, (sign * WALL_X, -WALL_Z), (sign * WALL_X, WALL_Z), FOOT, PLATE, level, rng, half=True)
    if level["small"]:
        # The store's inner wall, seen through the opening.
        log_wall(bm, (STORE_X, -WALL_Z + 0.1), (STORE_X, WALL_Z - 0.1), FOOT, PLATE, level, rng, half=True,
                 over=(0.0, 0.0), radius=LOG * 0.95, sides=6)

    # The open side: posts on their stones, the plate logs, and braces up to the front one.
    for x in (STORE_X, POST_X, CORNER_X):
        low = FOOT + 0.02 if x == STORE_X else 0.22
        log(bm, (x, low, -WALL_Z), (x, PLATE - 0.02, -WALL_Z), 0.16, level, rng, wobble=0.006)
    for z in (-WALL_Z, WALL_Z):
        log(bm, (-WALL_X - 0.45, PLATE + 0.13, z), (WALL_X + 0.45, PLATE + 0.13, z), 0.15, level, rng)
    if level["small"]:
        for x, signs in ((STORE_X, (1,)), (POST_X, (-1, 1)), (CORNER_X, (-1,))):
            for sign in signs:
                brace(bm, x, -WALL_Z, sign, 1.5, PLATE + 0.02, level, rng)

    for x in (-WALL_X, WALL_X):
        board_gable(bm, x, WALL_Z, PLATE + 0.2, ROOF, level)
    sod_roof(bm, ROOF, level, rng, purlins=(-1.25, 1.25))

    chimney(bm, CHIMNEY[0], CHIMNEY[1], ROOF.deck(CHIMNEY[1] + 0.45) - 0.25, CHIMNEY_TOP, level, rng)
    round_forge(bm, FORGE[0], FORGE[1], level, rng)
    if level["small"]:
        anvil(bm, ANVIL[0], ANVIL[1], level, rng)
        barrel(bm, BARREL[0], BARREL[1], level)


def copper(bm, level, rng):
    """Tier II: a copper face on the anvil, and a crate of copper ingots inside the opening."""
    box(bm, (ANVIL[0] - 0.03, 0.775, ANVIL[1]), (0.36, 0.03, 0.2), COPPER)
    crate(bm, -0.95, 0.05, -1.6, (0.72, 0.42, 0.5), rng)
    for k in range(3):
        box(bm, (-0.95 + (k - 1) * 0.18, 0.51, -1.6), (0.13, 0.07, 0.32), COPPER)


def iron(bm, level, rng):
    """Tier III: a quench trough beside the barrel, an iron cap over the chimney and a lantern on the middle post."""
    box(bm, (1.4, 0.24, -2.75), (1.3, 0.48, 0.5), WOOD, grain='x')
    box(bm, (1.4, 0.49, -2.75), (1.14, 0.02, 0.36), WATER)
    cx, cz = CHIMNEY
    box(bm, (cx, CHIMNEY_TOP + 0.34, cz), (1.0, 0.06, 0.95), IRON)
    for sx in (-1, 1):
        for sz in (-1, 1):
            box(bm, (cx + sx * 0.4, CHIMNEY_TOP + 0.22, cz + sz * 0.38), (0.06, 0.22, 0.06), IRON)
    lantern(bm, POST_X, 1.72, -WALL_Z - 0.27)


def moonsilver(bm, level, rng):
    """Tier IV: moonsilver in the coals, and bands of it on the ridge log's ends."""
    disc(bm, (FORGE[0], 0.58, FORGE[1]), 0.28, MOONSILVER)
    for x in (-ROOF.half_x - 0.4, ROOF.half_x + 0.4):
        box(bm, (x, ROOF.ridge - 0.2, 0.0), (0.1, 0.37, 0.37), MOONSILVER)


def trollstone(bm, level, rng):
    """Tier V: a trollstone anvil on a field stone beside the first, and the smith's banner at the corner."""
    x, z = ANVIL[0] + 1.05, ANVIL[1] + 0.05
    boulder(bm, (x, 0.2, z), (0.72, 0.46, 0.58), level, rng, flat=0.4)
    box(bm, (x, 0.5, z), (0.4, 0.14, 0.26), TROLLSTONE)
    box(bm, (x, 0.64, z), (0.66, 0.16, 0.32), TROLLSTONE)
    banner(bm, -WALL_X - 0.5, -WALL_Z - 0.6, level, rng)


if __name__ == "__main__":
    build(NAME, shape, {2: copper, 3: iron, 4: moonsilver, 5: trollstone}, script="smithy")
