"""The Woodcutter's Lodge: a log cabin with an open saw shed beside it under one sod roof.

What each tier adds: II a pile of felled logs, III an iron-toothed saw on the bench and a lantern, IV a great wheel
with a moonsilver hub on the gable, V the Haven's banner.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Woodcutters Lodge/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Woodcutters Lodge/Woodcutters Lodge.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/woodcutters_lodge.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Woodcutters Lodge"
HALF_X, HALF_Z, PLATE = 3.0, 1.85, 2.3
ROOF = house_roof(HALF_X, HALF_Z, PLATE)
SHED_X = -0.6      # the cabin's end and the shed's first post
SAW = (1.25, -1.35)


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, opening=(SHED_X, HALF_X), posts=())
    door(bm, -1.85, HALF_Z, level, rng)
    # Split wood stacked at the shed's end, the sawbench with a log on it, sawn planks, and the chopping block.
    firewood(bm, SHED_X + 0.25, SHED_X + 1.15, -1.45, level, rng, rows=5, depth=0.6)
    sawhorse(bm, SAW[0], SAW[1], 2.0, level, rng)
    plank_stack(bm, 1.75, -2.55, (1.7, 0.45, 0.7), 4, rng)
    if level["small"]:
        chopping_block(bm, 0.0, -2.85, level, rng)
        shield(bm, -2.75, 0.5, front(HALF_Z) - 0.14, level, rng)


def logs(bm, level, rng):
    """Tier II: a pile of felled logs in front of the cabin."""
    log_pile(bm, -1.9, -3.05, 2.3, level, rng, rows=3)


def saw(bm, level, rng):
    """Tier III: a two-handed iron saw across the log on the bench, and a lantern on the corner post."""
    box(bm, (SAW[0], 1.2, SAW[1]), (1.5, 0.5, 0.02), IRON)
    for x in (SAW[0] - 0.8, SAW[0] + 0.8):
        log(bm, (x, 0.9, SAW[1]), (x, 1.55, SAW[1]), 0.03, level, rng, wobble=0.0, sides=4)
    lantern(bm, HALF_X - 0.32, 1.7, -HALF_Z - 0.27)


def wheel(bm, level, rng):
    """Tier IV: a great wooden wheel on the right gable, its hub of moonsilver."""
    x = HALF_X + 0.32
    log(bm, (x, 1.1, 0.2), (x + 0.14, 1.1, 0.2), 0.95, level, rng, material=PLANKS, wobble=0.0, uneven=0.0, cap=0.0)
    log(bm, (x + 0.14, 1.1, 0.2), (x + 0.3, 1.1, 0.2), 0.17, level, rng, material=MOONSILVER, wobble=0.0, uneven=0.0, cap=0.02, sides=8)


def flag(bm, level, rng):
    """Tier V: the Haven's banner at the corner."""
    banner(bm, -HALF_X - 0.5, -HALF_Z - 0.6, level, rng)


if __name__ == "__main__":
    build(NAME, shape, {2: logs, 3: saw, 4: wheel, 5: flag}, script="woodcutters_lodge")
