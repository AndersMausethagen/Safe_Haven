"""The Tailor's Shop: a log house with an open porch under its sod roof, where the loom stands and dyed cloth
hangs to dry.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Tailors Shop/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Tailors Shop/Tailors Shop.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/tailors_shop.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Tailors Shop"
HALF_X, HALF_Z, PLATE = 3.4, 1.85, 2.3
ROOF = house_roof(HALF_X, HALF_Z, PLATE)
PORCH_X = -1.3     # the closed room's end and the porch's first post


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, opening=(PORCH_X, HALF_X), posts=(1.05,))
    door(bm, -2.4, HALF_Z, level, rng)
    loom(bm, -0.25, -1.5, level, rng)
    # Dyed cloth on a line between the posts.
    log(bm, (PORCH_X, 2.0, -HALF_Z - 0.2), (HALF_X - 0.32, 2.0, -HALF_Z - 0.2), 0.02, level, rng, wobble=0.0, sides=4)
    if level["small"]:
        for i, dye in enumerate((CLOTH, DYE_BLUE, LINEN, DYE_OCHRE)):
            box(bm, (1.35 + i * 0.46, 1.62 - 0.05 * (i % 2), -HALF_Z - 0.2), (0.4, 0.74 + 0.1 * (i % 2), 0.02), dye)
        bench(bm, 2.25, -2.4, 1.4, level, rng)
        barrel(bm, 0.75, -2.45, level, height=0.34, radius=0.27, water=False)   # a basket of wool
        sack(bm, 0.2, -2.55, level, rng, size=(0.45, 0.5, 0.4))


if __name__ == "__main__":
    build(NAME, shape, script="tailors_shop")
