"""The Dock: a short Norse jetty of weathered planks on log posts, with rails, a fishing net hung on a drying rack, a coil
of rope, a barrel and a lantern on a tall post at the far end.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Dock/A/concept.png). Drawn as the camera sees it: the land end, where the yard is, at -z,
the far end (over the water) at +z.

Tier II (the Long Pier): the deck runs on past the lantern post, with two pike traps and a rod against the rail.

Writes Assets/_Project/Art/Haven/Structures/Dock/Dock.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/dock.py -- <project root> [--no-preview]
"""
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Dock"
HALF_W = 0.95                  # the deck's half width (x)
NEAR, FAR = -1.9, 1.9          # the deck's ends (z)
DECK = 0.5                     # the deck's top
POST_X = 0.8
LANTERN = (0.8, 1.85)
RACK_Z = 1.2


def deck(bm, level, rng, z0, z1, posts):
    """Planks across two log beams on rows of posts, from z0 to z1."""
    for x in (-POST_X + 0.08, POST_X - 0.08):
        log(bm, (x, DECK - 0.15, z0 - 0.05), (x, DECK - 0.15, z1 + 0.05), 0.09, level, rng, wobble=0.004)
    for z in posts:
        for x in (-POST_X, POST_X):
            log(bm, (x, -0.45, z), (x, DECK - 0.06, z), 0.13, level, rng, wobble=0.004, uneven=0.05)
        log(bm, (-POST_X - 0.12, DECK - 0.28, z), (POST_X + 0.12, DECK - 0.28, z), 0.08, level, rng, wobble=0.0)
    z = z0 + 0.14
    while z < z1 - 0.1:
        width = rng.uniform(0.24, 0.3)
        shift = rng.uniform(-0.06, 0.06)
        turn = Matrix.Rotation(rng.uniform(-0.02, 0.02), 3, 'Z')
        box(bm, (shift, DECK - 0.03 + rng.uniform(-0.01, 0.01), z), (HALF_W * 2 + rng.uniform(-0.08, 0.1), 0.06, width - 0.02),
            PLANKS, turn=turn, grain='x')
        z += width


def rail(bm, level, rng, x, z0, z1):
    """A rail along one side: uprights and two poles."""
    stops = [z0, (z0 + z1) / 2, z1] if z1 - z0 > 1.6 else [z0, z1]
    for z in stops:
        log(bm, (x, DECK - 0.05, z), (x, DECK + 0.95, z), 0.05, level, rng, wobble=0.0, sides=6)
    for y in (DECK + 0.5, DECK + 0.9):
        log(bm, (x, y, z0 - 0.08), (x, y, z1 + 0.08), 0.04, level, rng, wobble=0.004, sides=6)


def shape(bm, level, rng):
    deck(bm, level, rng, NEAR, FAR, (NEAR + 0.2, 0.0, FAR - 0.2))

    # The lantern on its post at the far corner.
    x, z = LANTERN
    log(bm, (x + 0.05, -0.45, z), (x + 0.05, DECK + 1.95, z), 0.07, level, rng, wobble=0.0)
    if level["small"]:
        log(bm, (x + 0.05, DECK + 1.82, z), (x - 0.42, DECK + 1.82, z), 0.035, level, rng, wobble=0.0, sides=5)
        log(bm, (x + 0.05, DECK + 1.4, z), (x - 0.22, DECK + 1.8, z), 0.025, level, rng, wobble=0.0, sides=4)
        log(bm, (x - 0.36, DECK + 1.8, z), (x - 0.36, DECK + 1.62, z), 0.008, level, rng, wobble=0.0, sides=3)
        lantern(bm, x - 0.36, DECK + 1.43, z)

        rail(bm, level, rng, -HALF_W + 0.06, -1.0, FAR - 0.1)
        rail(bm, level, rng, HALF_W - 0.06, 0.6, FAR - 0.45)

        # The drying rack across the deck near the far end, a net hung over its pole.
        for rx in (-0.72, 0.42):
            for lean in (-1, 1):
                log(bm, (rx, DECK, RACK_Z + lean * 0.32), (rx, DECK + 1.62, RACK_Z), 0.04, level, rng, wobble=0.0, sides=5)
        log(bm, (-0.86, DECK + 1.56, RACK_Z), (0.56, DECK + 1.56, RACK_Z), 0.035, level, rng, wobble=0.003, sides=5)
        net(bm, -0.66, 0.36, DECK + 1.53, RACK_Z, 0.85, level, rng)

        coil(bm, -0.3, DECK, 0.35, level, rng)
    lifted(bm, DECK, barrel, 0.42, 0.55, level, height=0.62, radius=0.24, water=False)


def long_pier(bm, level, rng):
    """Tier II, the Long Pier: the deck on past the far end, two pike traps on it and a rod against the rail."""
    deck(bm, level, rng, FAR - 0.02, FAR + 1.9, (FAR + 0.9, FAR + 1.7))
    if level["small"]:
        rail(bm, level, rng, -HALF_W + 0.06, FAR + 0.1, FAR + 1.8)
        for (x, z, yaw) in ((0.35, FAR + 1.0, 0.3), (0.1, FAR + 1.55, -0.2)):
            # A pike trap: a long wicker basket lying on its side, a rope to its mouth.
            log(bm, (x - 0.35, DECK + 0.2, z), (x + 0.35, DECK + 0.2, z + yaw * 0.6), 0.18, level, rng, material=PLANKS, wobble=0.0,
                uneven=0.04, cap=0.0, sides=8)
        log(bm, (-HALF_W + 0.15, DECK, FAR + 0.6), (-HALF_W + 0.05, DECK + 2.1, FAR + 1.1), 0.02, level, rng, wobble=0.0, sides=4)


if __name__ == "__main__":
    build(NAME, shape, {2: long_pier}, script="dock")
