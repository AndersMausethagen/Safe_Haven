"""The Wainwright's Yard: an open Norse work shelter on four braced posts under a sod roof, a half-built cart body on two
trestles beneath it with a mallet and a chisel on its bed, a finished spoked wheel leaning on a post, a stack of planks, a
tub of water to swell the wheels' wood and a chopping block with its axe.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Wainwrights Yard/A/concept.png). Drawn as the camera sees it, the front at -z.

Tier II (Sturdy): a workbench with a grindstone at the back, a lantern on a post, and a wheel being made on a stump.

Writes Assets/_Project/Art/Haven/Structures/Wainwrights Yard/Wainwrights Yard.fbx; AgentScripts/BuildStructureModels.cs
puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/wainwrights_yard.py -- <project root> [--no-preview]
"""
import math
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Wainwrights Yard"
HALF_X, HALF_Z, PLATE = 1.75, 1.15, 2.2
ROOF = house_roof(HALF_X, HALF_Z, PLATE, over_x=0.45, over_z=0.5)
BED = (0.05, -0.1)        # the cart body's middle
BED_Y = 0.78


def trestle(bm, x, z, depth, level, rng, height=0.66):
    """A trestle across z: a beam on two pairs of splayed legs."""
    box(bm, (x, height, z), (0.12, 0.1, depth), WOOD, grain='z')
    for sz in (-1, 1):
        for sx in (-1, 1):
            log(bm, (x + sx * 0.22, 0.0, z + sz * (depth / 2 - 0.12)), (x + sx * 0.04, height - 0.04, z + sz * (depth / 2 - 0.12)), 0.035,
                level, rng, wobble=0.0, sides=5)


def shape(bm, level, rng):
    box(bm, (0.0, 0.02, -0.15), (HALF_X * 2 + 0.6, 0.06, HALF_Z * 2 + 0.9), SOIL, grain='x')

    # The shelter: four posts on stones, plate logs along the long sides, tie logs across the gables, braces.
    for x in (-HALF_X, HALF_X):
        for z in (-HALF_Z, HALF_Z):
            post_stone(bm, x, z, level, rng)
            log(bm, (x, 0.22, z), (x, PLATE - 0.02, z), 0.15, level, rng, wobble=0.006)
    for z in (-HALF_Z, HALF_Z):
        log(bm, (-HALF_X - 0.45, PLATE + 0.13, z), (HALF_X + 0.45, PLATE + 0.13, z), 0.14, level, rng)
    for x in (-HALF_X, HALF_X):
        log(bm, (x, PLATE + 0.05, -HALF_Z - 0.35), (x, PLATE + 0.05, HALF_Z + 0.35), 0.11, level, rng)
        log(bm, (x, PLATE + 0.12, 0.0), (x, ROOF.ridge - 0.25, 0.0), 0.09, level, rng, wobble=0.0)
        if level["small"]:
            inward = 1 if x < 0 else -1
            for z in (-HALF_Z, HALF_Z):
                brace(bm, x, z, inward, 1.45, PLATE + 0.02, level, rng)
            # The gable's rafters, open: a pair from the king post down to the eaves.
            for sign in (-1, 1):
                log(bm, (x, PLATE + 0.12, sign * 0.1), (x, ROOF.deck(sign * HALF_Z) - 0.12, sign * (HALF_Z + 0.3)), 0.07, level, rng,
                    wobble=0.0, sides=6)
    sod_roof(bm, ROOF, level, rng, purlins=(-0.75, 0.75))

    # The cart body on its trestles, half built: the bed, low sides, one shaft fitted, the other on the ground.
    bx, bz = BED
    for x in (bx - 0.55, bx + 0.55):
        trestle(bm, x, bz, 1.0, level, rng)
    box(bm, (bx, BED_Y, bz), (1.7, 0.06, 0.95), PLANKS, grain='x')
    for sz in (-1, 1):
        box(bm, (bx, BED_Y + 0.17, bz + sz * 0.45), (1.7, 0.28, 0.05), PLANKS, grain='x')
    box(bm, (bx - 0.83, BED_Y + 0.17, bz), (0.05, 0.28, 0.95), PLANKS, grain='z')
    log(bm, (bx - 0.6, BED_Y - 0.07, bz + 0.3), (bx + 2.0, BED_Y - 0.2, bz + 0.25), 0.045, level, rng, wobble=0.003, sides=6)
    if level["small"]:
        log(bm, (bx - 0.3, 0.05, bz - 0.75), (bx + 1.9, 0.05, bz - 0.9), 0.045, level, rng, wobble=0.003, sides=6)
        # A mallet and a chisel on the bed.
        log(bm, (bx + 0.2, BED_Y + 0.08, bz - 0.1), (bx + 0.2, BED_Y + 0.08, bz + 0.12), 0.06, level, rng, wobble=0.0, sides=6)
        log(bm, (bx + 0.2, BED_Y + 0.08, bz - 0.1), (bx + 0.25, BED_Y + 0.06, bz - 0.42), 0.015, level, rng, wobble=0.0, sides=4)
        box(bm, (bx - 0.25, BED_Y + 0.045, bz + 0.05), (0.22, 0.025, 0.035), IRON)

    # A finished wheel leaning on the front left post, the tub, planks at the back, the chopping block.
    cart_wheel(bm, (-HALF_X - 0.12, 0.55, -HALF_Z - 0.3), 0.52, level, rng, axis=(0.08, 0.22, -0.97))
    barrel(bm, HALF_X - 0.35, -HALF_Z + 0.15, level, height=0.45, radius=0.33)
    plank_stack(bm, -0.2, HALF_Z - 0.3, (2.4, 0.32, 0.55), 4, rng)
    if level["small"]:
        chopping_block(bm, HALF_X + 0.55, -HALF_Z - 0.55, level, rng)


def sturdy(bm, level, rng):
    """Tier II (Sturdy): a workbench and a grindstone at the back, a lantern on the front post, a wheel on a stump."""
    table(bm, 0.9, HALF_Z - 0.25, 1.3, 0.55, level, rng)
    grindstone(bm, -HALF_X + 0.45, HALF_Z - 0.35, level, rng)
    lantern(bm, HALF_X, 1.7, -HALF_Z - 0.2)
    x, z = -HALF_X - 0.75, 0.35
    log(bm, (x, 0.0, z), (x, 0.42, z), 0.26, level, rng, uneven=0.05, wobble=0.0, cap=0.01)
    cart_wheel(bm, (x, 0.48, z), 0.48, level, rng, axis=(0.0, 1.0, 0.0), spokes=6)


if __name__ == "__main__":
    build(NAME, shape, {2: sturdy}, script="wainwrights_yard")
