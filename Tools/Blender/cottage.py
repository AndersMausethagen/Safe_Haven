"""The Cottage: a family's log house under a sod roof, a porch over its door, a window, a stone chimney, a bench
and the winter's firewood.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Cottage/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Cottage/Cottage.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/cottage.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Cottage"
HALF_X, HALF_Z, PLATE = 2.3, 1.55, 2.2
ROOF = house_roof(HALF_X, HALF_Z, PLATE)
DOOR_X = -0.55
CHIMNEY = (1.25, 0.55)
CHIMNEY_TOP = ROOF.ridge + 0.7


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, apron=0.3)
    door(bm, DOOR_X, HALF_Z, level, rng)
    shutters(bm, 1.15, 1.4, HALF_Z, level)
    chimney(bm, CHIMNEY[0], CHIMNEY[1], ROOF.deck(CHIMNEY[1] + 0.4) - 0.25, CHIMNEY_TOP, level, rng, half=0.36)
    # The porch: a board roof over the door on two posts.
    z = front(HALF_Z)
    awning(bm, DOOR_X - 0.75, DOOR_X + 0.75, z, 0.95, 2.12, 1.9, level, rng, material=PLANKS)
    if level["small"]:
        bench(bm, 1.15, z - 0.32, 1.25, level, rng)
        firewood(bm, -2.1, -1.45, z - 0.28, level, rng, rows=4)


if __name__ == "__main__":
    build(NAME, shape, script="cottage")
