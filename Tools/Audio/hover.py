"""The pointer finding a button: a very soft, short tick of fingertip on wood, quieter and higher than the click it may
turn into, so moving over a row of buttons murmurs rather than rattles. Three variants, so a run never repeats.

Run from the project root: python Tools/Audio/hover.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Interface"


def hover(rng):
    seconds = 0.08
    tick = strike(wood(rng.uniform(1700, 2100), rng, damp=0.006, count=6), seconds, 0.0003, rng)
    air = thump(rng, seconds, 220, 320, 0.006) * 0.15
    return darken(tick + air, top=-6, weight=3)


def main():
    for i in range(1, 4):
        name = f"UI Hover {i}"
        save(OUT, name, space(hover(seeded(name)), HUT, 0.06))


if __name__ == "__main__":
    main()
