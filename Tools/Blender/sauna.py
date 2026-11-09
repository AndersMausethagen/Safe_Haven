"""The Sauna: a small log bathhouse under a sod roof, with a stone stove's chimney, a bench, a bucket and a
water barrel.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Sauna/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Sauna/Sauna.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/sauna.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Sauna"
HALF_X, HALF_Z, PLATE = 1.45, 1.15, 1.95
ROOF = house_roof(HALF_X, HALF_Z, PLATE, over_x=0.45, over_z=0.5)
CHIMNEY = (0.8, 0.3)
CHIMNEY_TOP = ROOF.ridge + 0.9


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, apron=0.3, purlins=())
    door(bm, -0.4, HALF_Z, level, rng, width=0.85, height=1.6)
    chimney(bm, CHIMNEY[0], CHIMNEY[1], ROOF.deck(CHIMNEY[1] + 0.33) - 0.2, CHIMNEY_TOP, level, rng, half=0.3)
    z = front(HALF_Z)
    barrel(bm, -1.55, z - 0.35, level, height=0.8, radius=0.33)
    if level["small"]:
        bench(bm, 0.75, z - 0.3, 1.0, level, rng, height=0.4)
        barrel(bm, 0.2, z - 0.28, level, height=0.3, radius=0.16)


if __name__ == "__main__":
    build(NAME, shape, script="sauna")
