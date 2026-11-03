"""Synthesises the sounds of people at their work in the Haven, heard across the clearing: the smith's anvil, the carpenter's
mallet, the mason's chisel, a knife on a board, a hoe in the ground, a blade on a hide, glass, a loom and a page.

Each is a real strike or rub, made by what hits and what is hit, as in foley.py. They sit outdoors, and come round again and again,
so they have variants and stay short.

Run from the project root: python Tools/Audio/work.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Work"


def anvil(rng):
    seconds = 1.0
    ping = strike(iron(rng.uniform(1050, 1250), rng, hold=0.32, count=22), seconds, 0.0004, rng) * 0.9
    block = strike(iron(rng.uniform(280, 340), rng, hold=0.09, count=10), seconds, 0.001, rng) * 0.6
    return fade_end(ping + block + thump(rng, seconds, 80, 190, 0.03) * 0.5)


def mallet(rng):
    seconds = 0.3
    plank = strike(wood(rng.uniform(320, 460), rng, damp=0.045), seconds, 0.0012, rng)
    return fade_end(plank + thump(rng, seconds, 90, 210, 0.025) * 0.5)


def chisel(rng):
    seconds = 0.26
    t = times(seconds)
    tap = strike(iron(rng.uniform(2200, 2800), rng, hold=0.035, count=10), seconds, 0.0003, rng) * 0.55
    rock = strike(stone(rng.uniform(600, 950), rng, count=10), seconds, 0.0005, rng)
    grit = band(white(len(t), rng), 2000, 7500) * decay(t, 0.012) * 0.25
    return fade_end(tap + rock + grit)


def chop(rng):
    seconds = 0.16
    t = times(seconds)
    board = strike(wood(rng.uniform(380, 560), rng, damp=0.022), seconds, 0.001, rng)
    edge = band(white(len(t), rng), 2500, 9000) * decay(t, 0.004) * 0.3
    return fade_end(board + edge)


def dig(rng):
    seconds = 0.42
    t = times(seconds)
    bite = thump(rng, seconds, 70, 170, 0.05) * 1.1
    soil = band(pink(len(t), rng), 500, 3400) * bump(t, 0.005, 0.015, 0.09) * 0.8
    return fade_end(bite + soil + scatter(seconds, 12, lambda: chip(rng, 900, 3000), rng, early=2.6) * 0.3)


def hide(rng):
    seconds = 0.46
    t = times(seconds)
    stroke = np.clip(t / 0.05, 0, 1) * np.clip((seconds - t) / 0.16, 0, 1)
    rub = band(pink(len(t), rng), 900, 5200) * stroke
    grain = lowpass(np.abs(white(len(t), rng)) ** 2, 140)
    return fade_end(rub * (0.35 + 2.4 * grain / max(1e-9, grain.max())) * 0.5)


def glass(rng):
    seconds = 0.5
    base = rng.uniform(2300, 3200)
    modes = [(base * r * (1 + rng.normal(0, 0.003)), rng.uniform(0.5, 1) / r, 0.09 / r ** 0.4) for r in (1, 2.32, 4.25, 6.63)]
    out = strike(modes, seconds, 0.0002, rng) * 0.7
    if rng.random() < 0.5:
        place(out, strike(modes, seconds * 0.5, 0.0002, rng) * 0.4, rng.uniform(0.07, 0.13))
    return fade_end(out)


def loom(rng):
    seconds = 0.34
    first = strike(wood(rng.uniform(520, 700), rng, damp=0.03), seconds, 0.0008, rng)
    second = strike(wood(rng.uniform(380, 500), rng, damp=0.03), seconds * 0.6, 0.0008, rng) * 0.7
    place(first, second, rng.uniform(0.09, 0.13))
    return fade_end(first)


def page(rng):
    return cloth(rng.uniform(0.26, 0.38), rng, centre=3600.0)


def sack(rng):
    seconds = 0.36
    out = cloth(seconds, rng, centre=1300.0) * 0.8
    for _ in range(rng.integers(2, 5)):
        knock = strike(wood(rng.uniform(420, 900), rng, damp=0.02, count=8), 0.1, 0.0006, rng) if rng.random() < 0.6 else strike(stone(rng.uniform(800, 1400), rng, count=6), 0.1, 0.0004, rng)
        place(out, knock, rng.uniform(0.02, 0.24), rng.uniform(0.25, 0.6))
    return fade_end(out)


# Stem, recipe, variants, how much of the open air is on it.
SOUNDS = [("Work Anvil", anvil, 4, 0.16), ("Work Mallet", mallet, 4, 0.1), ("Work Chisel", chisel, 4, 0.1), ("Work Chop", chop, 4, 0.06),
          ("Work Dig", dig, 4, 0.05), ("Work Hide", hide, 3, 0.04), ("Work Glass", glass, 4, 0.08), ("Work Loom", loom, 3, 0.07),
          ("Work Page", page, 3, 0.03), ("Sack Rattle", sack, 4, 0.04)]


def main():
    for stem, recipe, count, wet in SOUNDS:
        for i in range(1, count + 1):
            name = f"{stem} {i}"
            save(OUT, name, space(recipe(seeded(name)), FOREST, wet), mono=True)


if __name__ == "__main__":
    main()
