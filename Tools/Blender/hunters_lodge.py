"""The Hunter's Lodge: a closed log lodge under a sod roof, antlers over its door, a hide on a frame and a
drying rack outside.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Hunters Lodge/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Hunters Lodge/Hunters Lodge.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/hunters_lodge.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Hunters Lodge"
HALF_X, HALF_Z, PLATE = 2.7, 1.8, 2.25
ROOF = house_roof(HALF_X, HALF_Z, PLATE)


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF)
    door(bm, -0.35, HALF_Z, level, rng)
    shutters(bm, -2.0, 1.4, HALF_Z, level)
    hide_frame(bm, 0.9, front(HALF_Z) - 0.42, level, rng)
    pole_rack(bm, 1.55, -2.85, 1.7, level, rng, hang=[HIDE, CLOTH, LINEN], count=7)
    if level["small"]:
        antlers(bm, -1.2, 1.55, front(HALF_Z) - 0.05, level, rng)
        chopping_block(bm, -1.6, -2.75, level, rng, axe=False)


if __name__ == "__main__":
    build(NAME, shape, script="hunters_lodge")
