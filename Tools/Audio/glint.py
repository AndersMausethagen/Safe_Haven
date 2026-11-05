"""Synthesises the tool glint: the small, bright ping that says the raised axe or pick is at its best moment to swing
(release now for a critical). It has to cut through chopping and forest noise without sounding like a combat cue, so it
is high, light and short: a thin piece of metal touched, with a faint shimmer.

Run from the project root: python Tools/Audio/glint.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Gathering"


def glint(rng):
    seconds = 0.45
    t = times(seconds)
    ping = strike(iron(rng.uniform(3100, 3500), rng, hold=0.11, count=8), seconds, 0.0002, rng) * 0.7
    overtone = strike(iron(rng.uniform(5200, 5900), rng, hold=0.05, count=5), seconds, 0.0002, rng) * 0.3
    shimmer = band(white(len(t), rng), 6000, 11000) * bump(t, 0.004, 0.02, 0.09) * 0.06
    return fade_end(ping + overtone + shimmer)


def main():
    for i in range(1, 3):
        name = f"Tool Glint {i}"
        save(OUT, name, space(glint(seeded(name)), FOREST, 0.15), mono=True)


if __name__ == "__main__":
    main()
