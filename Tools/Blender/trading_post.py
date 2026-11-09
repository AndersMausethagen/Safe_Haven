"""The Trading Post: a log store with an open counter under a cloth awning, and goods stacked round it.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Trading Post/A/concept.png). Drawn as the camera sees it, the front at -z.

Writes Assets/_Project/Art/Haven/Structures/Trading Post/Trading Post.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/trading_post.py -- <project root> [--no-preview]
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Trading Post"
HALF_X, HALF_Z, PLATE = 2.5, 1.85, 2.3
ROOF = house_roof(HALF_X, HALF_Z, PLATE)
STORE_X = -1.2     # the closed store's end and the counter's first post
COUNTER = (0.65, -2.1)


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, opening=(STORE_X, HALF_X))
    # The counter across the opening, boarded in front, under its awning.
    table(bm, COUNTER[0], COUNTER[1], 2.7, 0.6, level, rng, height=0.95)
    box(bm, (COUNTER[0], 0.45, COUNTER[1] - 0.26), (2.5, 0.8, 0.04), PLANKS, grain='x')
    awning(bm, STORE_X + 0.2, HALF_X - 0.3, -HALF_Z - 0.15, 1.35, PLATE - 0.05, 1.85, level, rng, material=LINEN)
    # Goods: sacks, a barrel and a crate, and a fur on the store's wall.
    sack(bm, -1.75, -2.4, level, rng)
    sack(bm, -1.25, -2.65, level, rng, size=(0.55, 0.5, 0.45))
    barrel(bm, -2.35, -2.45, level, height=0.7, radius=0.3, water=False)
    if level["small"]:
        crate(bm, 2.25, 0.05, -2.75, (0.6, 0.45, 0.5), rng)
        box(bm, (-1.95, 1.45, front(HALF_Z) - 0.03), (0.7, 0.95, 0.03), HIDE)
        box(bm, (COUNTER[0] - 0.7, 1.06, COUNTER[1]), (0.4, 0.13, 0.3), DYE_OCHRE)
        box(bm, (COUNTER[0] + 0.5, 1.08, COUNTER[1]), (0.3, 0.18, 0.3), COPPER)


if __name__ == "__main__":
    build(NAME, shape, script="trading_post")
