"""Synthesises a tree coming down: the trunk starting to give, the fibres tearing, the crown in the air, the landing.

A creak is stick-slip: fibres catching and letting go faster and faster as the load comes on, each slip ringing the
trunk. A crack is a run of those slips that break instead, bunching up towards the moment it goes. The landing is what
a tree really is when it hits: a weight in the ground, a crown of hundreds of small breaks, then what falls after.

Run from the project root: python Tools/Audio/woodcutting.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Woodcutting"


def snap(rng, seconds=0.4, low=600, high=1500):
    t = times(seconds)
    tick = band(white(len(t), rng), 1800, 6500) * decay(t, 0.0025) * 0.7
    fibres = band(pink(len(t), rng), 500, 2600) * decay(t, 0.03) * 0.5
    return strike(wood(rng.uniform(low, high), rng, damp=0.016, count=10), seconds, 0.0003, rng) + tick + fibres


def crinkle(n, rng, rate=60):
    """A gate of irregular little bursts, the way leaves and splinters hiss rather than steam."""
    gate = lowpass(np.abs(white(n, rng)) ** 3, rate, 2)
    return gate / max(1e-9, gate.max())


def creak(rng):
    seconds = rng.uniform(1.6, 2.3)
    t = times(seconds)
    rise = t / seconds
    rate = rng.uniform(18, 28) * (1 + 2.2 * rise ** 2) * (1 + 0.18 * np.sin(TAU * rng.uniform(1.2, 2.4) * t))
    phase = np.cumsum(rate * (1 + 0.25 * lowpass(white(len(t), rng), 30) * 4)) / RATE
    slips = np.diff(np.floor(phase), prepend=0.0) * rng.uniform(0.4, 1.0, len(t))
    body = ring(wood(rng.uniform(170, 240), rng, damp=0.14, count=14), 0.3, rng)
    groan = fftconvolve(slips, body)[:len(t)]
    grain = band(pink(len(t), rng), 300, 1800) * crinkle(len(t), rng, 40) * 0.12
    shape = np.clip(t / (seconds * 0.25), 0, 1) * np.clip((seconds - t) / (seconds * 0.3), 0, 1) ** 1.2
    return darken((groan / max(1e-9, np.abs(groan).max()) + grain) * shape, top=-4, weight=1)


def crack(rng):
    seconds = 2.3
    out = blank(seconds)
    moment = rng.uniform(0.0, 0.05)
    gap = rng.uniform(0.2, 0.28)
    level = 0.45
    while moment < 1.1:
        place(out, snap(rng, 0.45), moment, level * rng.uniform(0.7, 1.0))
        moment += gap * rng.uniform(0.75, 1.25)
        gap *= rng.uniform(0.72, 0.86)
        level = min(1.0, level * 1.09)

    t = times(seconds)
    tear = band(pink(len(t), rng), 250, 1500) * crinkle(len(t), rng, 60) * bump(t, 0.6, 0.3, 0.38) * 0.8
    last = strike(wood(rng.uniform(190, 260), rng, damp=0.1, count=14), 1.0, 0.0009, rng) * 1.6
    place(out, last + thump(rng, 1.0, 55, 150, 0.12) * 1.0, moment)
    place(out, snap(rng, 0.5, 400, 900), moment + 0.02, 1.0)
    return darken(drive(out + tear, 1.6), top=-3, weight=1)


def rush(rng):
    seconds = rng.uniform(3.0, 3.8)
    n = int(seconds * RATE)
    t = times(seconds)
    air = whoosh(seconds, 180, 1500, 0.7, rng, peak=0.86)
    speed = (t / seconds) ** 2.4
    leaves = band(pink(n, rng), 1200, 7000) * crinkle(n, rng, 120) * speed * 1.5
    return fade_end(darken(air + leaves + 0.5 * whoosh(seconds, 80, 320, 0.8, rng, peak=0.86), top=-3, weight=1), 0.14)


def landing(rng):
    seconds = 4.2
    n = int(seconds * RATE)
    t = times(seconds)
    weight = thump(rng, seconds, 34, 120, 0.38, 0.05) * 2.6
    earth = band(pink(n, rng), 60, 420) * bump(t, 0.004, 0.02, 0.55) * 2.6
    crown = band(pink(n, rng), 700, 4500) * crinkle(n, rng, 130) * bump(t, 0.015, 0.05, 0.5) * 0.7
    breaks = scatter(1.5, 70, lambda: chip(rng, 400, 2000), rng, early=1.7) * 0.7
    mix = place(weight + earth + crown, breaks, 0.01)
    place(mix, thump(rng, 1.4, 40, 110, 0.2, 0.04) * 1.1, 0.27)
    for _ in range(9):
        place(mix, snap(rng, 0.4), rng.uniform(0.02, 1.4) ** 1.3, rng.uniform(0.25, 0.7))
    place(mix, scatter(3.0, 50, lambda: chip(rng, 1100, 3400), rng, early=1.2) * 0.3, 0.6)
    return darken(drive(mix, 2.1), top=-7, weight=3)


def branch_snap(rng):
    return darken(snap(rng, 0.45, 550, 1400), top=-3, weight=1)


def limbing(rng):
    seconds = 1.6
    n = int(seconds * RATE)
    t = times(seconds)
    leaves = band(pink(n, rng), 700, 5500) * crinkle(n, rng, 90) * bump(t, 0.0, 0.05, 0.42) * 1.4
    mix = leaves
    for _ in range(6):
        place(mix, snap(rng, 0.35), rng.uniform(0.0, 0.8), rng.uniform(0.4, 1.0))
    return fade_end(darken(mix, top=-3, weight=1), 0.25)


def log_split(rng):
    seconds = 1.0
    wood_bite = strike(wood(rng.uniform(240, 330), rng, damp=0.07, count=14), seconds, 0.001, rng) * 1.3
    chips = scatter(0.3, 22, lambda: chip(rng, 700, 2800), rng) * 1.1
    mix = place(wood_bite + thump(rng, seconds, 60, 150, 0.09) * 0.9, chips, 0.0)
    place(mix, snap(rng, 0.4, 500, 1000), 0.012, 0.8)
    return darken(drive(mix, 1.8), top=-3, weight=1)


def log_knock(rng):
    seconds = 0.5
    hollow = strike(wood(rng.uniform(150, 240), rng, damp=0.08, count=12), seconds, 0.0012, rng)
    return darken(hollow + thump(rng, seconds, 70, 140, 0.04) * 0.5, top=-3, weight=1)


def tree_strike(rng):
    """A trunk swinging into a standing tree: the knock of timber on timber, branches giving, and the crown it shook."""
    seconds = 1.5
    n = int(seconds * RATE)
    t = times(seconds)
    knock = strike(wood(rng.uniform(120, 170), rng, damp=0.16, count=16), seconds, 0.0016, rng) * 2.4
    shaken = band(pink(n, rng), 600, 3500) * crinkle(n, rng, 110) * bump(t, 0.02, 0.1, 0.4) * 0.3
    mix = knock + shaken + thump(rng, seconds, 48, 130, 0.14) * 2.0
    for _ in range(3):
        place(mix, snap(rng, 0.35), rng.uniform(0.01, 0.45), rng.uniform(0.15, 0.4))
    return fade_end(darken(drive(mix, 1.8), top=-6, weight=3), 0.2)


def tree_crush(rng):
    """Something caught under a trunk: the weight, a crack, and a wet give."""
    seconds = 1.1
    n = int(seconds * RATE)
    t = times(seconds)
    weight = thump(rng, seconds, 42, 130, 0.2, 0.04) * 2.0
    crack = strike(wood(rng.uniform(420, 620), rng, damp=0.03, count=12), seconds, 0.0005, rng) * 1.1
    give = band(pink(n, rng), 250, 1300) * decay(t, 0.09) * 2.2
    squelch = band(white(n, rng), 500, 1900) * crinkle(n, rng, 70) * bump(t, 0.01, 0.02, 0.11) * 1.6
    return darken(drive(weight + crack + give + squelch, 2.2), top=-5, weight=2)


# Stem, recipe, variants, how much of the forest is on it.
SOUNDS = [
    ("Tree Creak", creak, 4, 0.14), ("Tree Crack", crack, 3, 0.16), ("Tree Rush", rush, 2, 0.1), ("Tree Landing", landing, 3, 0.2),
    ("Branch Snap", branch_snap, 4, 0.12), ("Limbing", limbing, 3, 0.1), ("Log Split", log_split, 4, 0.1), ("Log Knock", log_knock, 4, 0.08),
    ("Tree Strike", tree_strike, 3, 0.16), ("Tree Crush", tree_crush, 3, 0.12),
]


def main():
    for stem, recipe, count, wet in SOUNDS:
        for i in range(1, count + 1):
            name = f"{stem} {i}"
            save(OUT, name, space(recipe(seeded(name)), FOREST, wet), mono=True)


if __name__ == "__main__":
    main()
