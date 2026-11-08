"""Synthesises the fishing fight: the reel turning, the line creaking near its limit, the line snapping, and a fish
thrashing at the surface. Small, close, wooden-and-gut sounds, nothing arcade-like: a hand reel of bone and wood, a gut
line, a pike breaking the water in a cold lake.

- Reel Turn: a short run of ratchet ticks, a wooden pawl over a small cog, played over and over while reeling.
- Line Strain: stick-slip creak of a gut line drawn tight over the rod's tip, with the rod's wood answering.
- Line Snap: a sharp crack, the freed line's twang and a little whip of air.
- Fish Splash: the slap of a tail on the water, the bubbles it drags under (they ring and rise in pitch as they shrink,
  as real bubbles do), and droplets falling back.

Run from the project root: python Tools/Audio/fishing.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Fishing"


def tick(rng, centre):
    """One tooth of the ratchet: a hard little click into a tiny wooden body."""
    click = chip(rng, centre * 0.8, centre * 1.25, 0.03)
    body = strike(wood(rng.uniform(900, 1300), rng, damp=0.012, count=8), 0.05, 0.0003, rng) * 0.35
    return fade_end(click + body[:len(click)] if len(body) >= len(click) else click, 0.3)


def reel_turn(rng):
    seconds = 0.46
    out = blank(seconds)
    # The cog turns a little unevenly under the thumb.
    at = 0.0
    while at < seconds - 0.04:
        place(out, tick(rng, rng.uniform(3200, 4200)), at, rng.uniform(0.6, 1.0))
        at += rng.uniform(0.034, 0.05)
    hum = band(pink(len(out), rng), 300, 900) * 0.04
    return fade_end(out + hum)


def line_strain(rng):
    seconds = 0.55
    t = times(seconds)
    # Stick-slip: the line catches and lets go at an uneven, slowing rate; each release excites a thin resonance.
    out = blank(seconds)
    at = 0.01
    rate = rng.uniform(70, 95)
    while at < seconds - 0.03:
        grain = band(white(int(RATE * 0.006), rng), 1500, 4200) * np.hanning(int(RATE * 0.006))
        place(out, grain, at, rng.uniform(0.4, 1.0))
        at += 1.0 / rate * rng.uniform(0.7, 1.3)
        rate *= rng.uniform(0.985, 1.0)
    creak = sweep(out, 1100 + 250 * np.sin(TAU * 1.7 * t), 9)
    rod = fftconvolve(out, ring(wood(rng.uniform(240, 320), rng, damp=0.04, count=8), 0.12, rng))[:len(t)] * 0.08
    envelope = bump(t, 0.0, 0.12, 0.5)
    return fade_end((creak + rod + out * 0.25) * envelope, 0.25)


def line_snap(rng):
    seconds = 0.7
    t = times(seconds)
    crack = band(white(len(t), rng), 2000, 9000) * decay(t, 0.004)
    twang = pluck(rng.uniform(520, 640), seconds, rng, damping=0.992, brightness=0.8) * decay(t, 0.18) * 0.45
    whip = whoosh(0.3, 600, 2600, 3.0, rng, peak=0.2)
    out = crack + twang
    place(out, whip * 0.5, 0.01)
    return fade_end(out)


def bubble(rng, radius_mm):
    """A bubble of this size (Minnaert): it rings near 3.3 kHz per millimetre of radius, and rises in pitch as it goes."""
    seconds = 0.06 + radius_mm * 0.01
    t = times(seconds)
    f0 = 3300.0 / radius_mm
    pitch = f0 * (1 + rng.uniform(0.6, 1.4) * t / seconds)
    return np.sin(TAU * np.cumsum(pitch) / RATE) * decay(t, seconds * 0.3)


def fish_splash(rng, size):
    seconds = 1.0
    t = times(seconds)
    slap = band(pink(len(t), rng), 250 + 150 * size, 3600) * bump(t, 0.0, 0.004, 0.05 + 0.04 * size)
    body = thump(rng, seconds, 70, 180, 0.06) * 0.35 * size
    out = slap + body
    for _ in range(int(10 + 10 * size)):
        place(out, bubble(rng, rng.uniform(1.5, 6.0)), rng.uniform(0.005, 0.18) ** 1.0, rng.uniform(0.04, 0.12))
    # Droplets falling back a little later.
    drops = scatter(0.5, int(12 + 10 * size), lambda: bubble(rng, rng.uniform(0.8, 2.2)) * 0.6, rng, early=1.3)
    place(out, drops, 0.18, 0.35)
    return fade_end(darken(out, -3.0))


def main():
    for i in range(1, 4):
        name = f"Reel Turn {i}"
        save(OUT, name, space(reel_turn(seeded(name)), FOREST, 0.08), mono=True)
    for i in range(1, 4):
        name = f"Line Strain {i}"
        save(OUT, name, space(line_strain(seeded(name)), FOREST, 0.1), mono=True)
    for i in range(1, 3):
        name = f"Line Snap {i}"
        save(OUT, name, space(line_snap(seeded(name)), FOREST, 0.15), mono=True)
    for i in range(1, 5):
        name = f"Fish Splash {i}"
        save(OUT, name, space(fish_splash(seeded(name), 0.4 + 0.2 * i), FOREST, 0.12), mono=True)


if __name__ == "__main__":
    main()
