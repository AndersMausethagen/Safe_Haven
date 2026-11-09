"""The Hut: a low log sleeping hut under a sod roof, with a door that shuts, a smoke vent, a bench and a bucket.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Hut/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Hut/Hut.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/hut.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Hut"
HALF_X, HALF_Z, PLATE = 1.65, 1.25, 1.8
ROOF = house_roof(HALF_X, HALF_Z, PLATE, over_x=0.45, over_z=0.5)
CHIMNEY = (-0.8, 0.35)
CHIMNEY_TOP = ROOF.ridge + 0.75


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, apron=0.3, purlins=())
    door(bm, 0.45, HALF_Z, level, rng, width=0.85, height=1.5)
    chimney(bm, CHIMNEY[0], CHIMNEY[1], ROOF.deck(CHIMNEY[1] + 0.3) - 0.2, CHIMNEY_TOP, level, rng, half=0.27)
    if level["small"]:
        z = front(HALF_Z)
        bench(bm, -0.8, z - 0.3, 1.1, level, rng, height=0.38)
        barrel(bm, 1.35, z - 0.3, level, height=0.34, radius=0.18)


if __name__ == "__main__":
    build(NAME, shape, script="hut")
