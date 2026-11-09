"""The Workshop: an open-fronted log workshop under a sod roof, where a carpenter and a stonemason work.

What each tier adds: II a rack of tools, III a tool chest and lanterns on the corner posts, IV a moonsilver vice and
moonsilver on the ridge log's ends, V a carved trollstone and the Haven's banner.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Workshop/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Workshop/Workshop.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/workshop.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Workshop"
# The first building a Haven gets, and a small one: about two thirds the size of the lodges that follow it.
HALF_X, HALF_Z, PLATE = 2.25, 1.5, 2.2
ROOF = house_roof(HALF_X, HALF_Z, PLATE, over_x=0.5, over_z=0.55)
BENCH = (-0.95, -0.95)


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, opening=(-HALF_X, HALF_X), posts=(0.1,))
    # The carpenter's bench with its vice, a trestle with a plank on it, the grindstone and a stone being cut.
    table(bm, BENCH[0], BENCH[1], 1.7, 0.7, level, rng)
    if level["small"]:
        box(bm, (BENCH[0] + 0.6, 0.97, BENCH[1] - 0.27), (0.22, 0.16, 0.18), IRON)
        box(bm, (BENCH[0] - 0.4, 0.93, BENCH[1]), (0.5, 0.07, 0.16), WOOD, grain='x')
        sawhorse(bm, 1.15, -0.95, 1.4, level, rng, height=0.62)
        grindstone(bm, 1.75, -2.05, level, rng)
    boulder(bm, (0.6, 0.4, -2.1), (0.58, 0.85, 0.48), level, rng, yaw=0.2, flat=0.45)


def tools(bm, level, rng):
    """Tier II: a rack of tools standing by the left corner post."""
    for x in (-1.9, -0.8):
        log(bm, (x, 0.0, -1.68), (x, 1.75, -1.68), 0.05, level, rng, wobble=0.0, sides=5)
    box(bm, (-1.35, 1.35, -1.68), (1.2, 0.7, 0.04), PLANKS, grain='x')
    for i in range(4):
        box(bm, (-1.78 + i * 0.29, 1.33, -1.72), (0.07, 0.5, 0.03), IRON)


def chest(bm, level, rng):
    """Tier III: a tool chest at the opening and a lantern on each corner post."""
    crate(bm, -1.4, 0.05, -2.25, (0.85, 0.5, 0.5), rng)
    for x in (-HALF_X + 0.32, HALF_X - 0.32):
        lantern(bm, x, 1.7, -HALF_Z - 0.27)


def moonsilver(bm, level, rng):
    """Tier IV: a moonsilver vice on the bench, and bands of it on the ridge log's ends."""
    box(bm, (BENCH[0] + 0.6, 1.0, BENCH[1] - 0.27), (0.3, 0.24, 0.24), MOONSILVER)
    for x in (-ROOF.half_x - 0.4, ROOF.half_x + 0.4):
        box(bm, (x, ROOF.ridge - 0.2, 0.0), (0.1, 0.37, 0.37), MOONSILVER)


def trollstone(bm, level, rng):
    """Tier V: a carved trollstone standing at the opening, and the Haven's banner at the corner."""
    boulder(bm, (-0.3, 0.55, -2.3), (0.55, 1.15, 0.42), level, rng, yaw=-0.15, material=TROLLSTONE, flat=0.5)
    banner(bm, -HALF_X - 0.5, -HALF_Z - 0.6, level, rng)


if __name__ == "__main__":
    build(NAME, shape, {2: tools, 3: chest, 4: moonsilver, 5: trollstone}, script="workshop")
