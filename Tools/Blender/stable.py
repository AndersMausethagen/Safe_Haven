"""The Stable: a long, low Norse log stable under a sod roof, its whole front open between braced posts, four stalls
inside parted by low log walls and rails, each with a hay rack heaped with hay on the back wall, a log water trough and
straw on the floor.

Built from the Haven's building kit (building_kit.py, building_props.py) after its concept image
(Tools/LocalSpawn/Structures/Stable/A/concept.png). Drawn as the camera sees it, the open front at -z. The stalls are
where the game keeps its horses (BuildHorses: "Stall 0".."Stall 3" at x -3.6, -1.2, 1.2, 3.6 and 0.9 in front of the
middle, facing out), so each horse stands in its own stall with its head at the opening. The paddock's skigard fence
before it is added in Unity (BuildStructureModels).

Writes Assets/_Project/Art/Haven/Structures/Stable/Stable.fbx; AgentScripts/BuildStructureModels.cs puts it in the game.

Run: blender -b --factory-startup --python Tools/Blender/stable.py -- <project root> [--no-preview]
"""
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403

NAME = "Stable"
HALF_X, HALF_Z, PLATE = 5.0, 1.75, 2.5
ROOF = house_roof(HALF_X, HALF_Z, PLATE, over_x=0.5, over_z=0.6)
STALLS = (-3.6, -1.2, 1.2, 3.6)   # the stalls' middles (x), as the game's stall points
PARTS = (-2.4, 0.0, 2.4)          # the partitions between them, and the posts at the front


def shape(bm, level, rng):
    log_house(bm, level, rng, HALF_X, HALF_Z, PLATE, ROOF, opening=(-HALF_X, HALF_X), posts=PARTS, apron=0.7,
              purlins=(-1.05, 1.05))
    for x in PARTS:
        # The partition: lying logs from the back wall half way out, then a rail on to the post.
        log_wall(bm, (x, HALF_Z - 0.1), (x, -0.2), FOOT - 0.25, 1.25, level, rng, over=(0.0, 0.05), radius=0.12, sides=7)
        if level["small"]:
            for y in (0.75, 1.2):
                log(bm, (x, y, -0.2), (x, y, -HALF_Z + 0.12), 0.06, level, rng, wobble=0.004, sides=6)
            log(bm, (x, 0.2, -0.25), (x, 1.3, -0.25), 0.07, level, rng, wobble=0.0, sides=6)
    for x in STALLS:
        # Straw on the floor.
        for k in range(3 if level["small"] else 1):
            boulder(bm, (x + rng.uniform(-0.6, 0.6), 0.04, rng.uniform(-1.2, 0.8)), (rng.uniform(0.9, 1.4), 0.08, rng.uniform(0.7, 1.1)),
                    level, rng, material=HAY, flat=0.85)
        # The water trough: a hollowed log under the hay, across the stall.
        z = HALF_Z - 0.55
        log(bm, (x - 0.55, 0.28, z), (x + 0.55, 0.28, z), 0.22, level, rng, wobble=0.0, uneven=0.03)
        box(bm, (x, 0.47, z), (1.0, 0.02, 0.24), WATER)
        if level["small"]:
            # The hay rack on the back wall: a frame of poles leaning out, slats, hay heaped in it.
            rack_z = HALF_Z - 0.32
            for sx in (-0.6, 0.6):
                log(bm, (x + sx, 1.0, rack_z), (x + sx, 1.75, rack_z - 0.35), 0.035, level, rng, wobble=0.0, sides=5)
            log(bm, (x - 0.65, 1.75, rack_z - 0.35), (x + 0.65, 1.75, rack_z - 0.35), 0.035, level, rng, wobble=0.0, sides=5)
            log(bm, (x - 0.65, 1.0, rack_z), (x + 0.65, 1.0, rack_z), 0.035, level, rng, wobble=0.0, sides=5)
            for k in range(6):
                sx = -0.5 + k * 0.2
                log(bm, (x + sx, 1.0, rack_z), (x + sx, 1.75, rack_z - 0.35), 0.015, level, rng, wobble=0.0, sides=4)
            boulder(bm, (x, 1.55, rack_z - 0.1), (1.15, 0.55, 0.42), level, rng, material=HAY, flat=0.2)
    # Hay heaped by the end wall, and a pitchfork against a post.
    boulder(bm, (HALF_X - 0.55, 0.4, 0.3), (0.9, 0.8, 1.1), level, rng, material=HAY)
    if level["small"]:
        x = PARTS[1] + 0.2
        log(bm, (x, 0.0, -HALF_Z - 0.15), (x + 0.12, 1.45, -HALF_Z + 0.02), 0.022, level, rng, wobble=0.0, sides=4)
        for k in (-1, 0, 1):
            log(bm, (x + k * 0.05, -0.02, -HALF_Z - 0.17), (x + k * 0.06, 0.25, -HALF_Z - 0.14), 0.008, level, rng, material=IRON,
                wobble=0.0, sides=3)
        lifted(bm, 0.0, barrel, -HALF_X - 0.6, -HALF_Z + 0.3, level, height=0.75, radius=0.3)
        sack(bm, -HALF_X - 0.55, -HALF_Z - 0.35, level, rng)
        lantern(bm, PARTS[1], 1.85, -HALF_Z - 0.27)


if __name__ == "__main__":
    build(NAME, shape, script="stable")
