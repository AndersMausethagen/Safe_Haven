"""Synthesises a rock coming apart under the pick: the crack that goes through it, the weight of the halves dropping and the
rubble after them, and the knock of one piece landing and rolling.

Stone has short, bright modes that die almost at once, so a burst is mostly its first few milliseconds. What makes it read as
big is the low thud under that crack and the chips that keep arriving afterwards, bunched towards the start as debris is.

Run from the project root: python Tools/Audio/mining.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Mining"


def rock_burst(rng):
    seconds = 1.3
    t = times(seconds)
    crack = band(white(len(t), rng), 1400, 8000) * decay(t, 0.006) * 1.5
    body = strike(stone(rng.uniform(420, 620), rng, count=20), seconds, 0.0006, rng) * 1.6
    weight = thump(rng, seconds, 52, 150, 0.11) * 2.2
    chips = scatter(seconds, 46, lambda: chip(rng, 1500, 6000), rng, early=2.4) * 0.55
    clacks = blank(seconds)
    for _ in range(9):
        clack = strike(stone(rng.uniform(700, 1500), rng, count=8), 0.14, 0.0004, rng)
        place(clacks, clack, 0.09 + 0.8 * rng.random() ** 1.7, rng.uniform(0.2, 0.7))
    return fade_end(crack + body + weight + chips + clacks * 0.8)


def rock_knock(rng):
    seconds = 0.22
    t = times(seconds)
    tick = band(white(len(t), rng), 1800, 7000) * decay(t, 0.0025) * 0.5
    body = strike(stone(rng.uniform(520, 1100), rng, count=10), seconds, 0.0005, rng)
    return fade_end(tick + body + thump(rng, seconds, 90, 240, 0.03) * 0.55)


# Stem, recipe, variants, how much of the forest is on it.
SOUNDS = [("Rock Burst", rock_burst, 3, 0.14), ("Rock Knock", rock_knock, 5, 0.07)]


def main():
    for stem, recipe, count, wet in SOUNDS:
        for i in range(1, count + 1):
            name = f"{stem} {i}"
            save(OUT, name, space(recipe(seeded(name)), FOREST, wet), mono=True)


if __name__ == "__main__":
    main()
