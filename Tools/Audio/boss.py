"""Synthesises the Heartroot's fight: a slow war drum over a low drone that loops, and the sound of it waking. No melody: the composed tracks have
those, and a boss should feel like weather. Only the drums, the wood and the ground, as in foley.py.

Run from the project root: python Tools/Audio/boss.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Boss"
BEAT = 60.0 / 58.0
BARS = 8
BEATS_PER_BAR = 4


def heavy_drum(rng, base=58.0):
    seconds = 2.2
    skin = drum(base * rng.uniform(0.98, 1.02), seconds, rng, tension=0.2)
    return fade_end(skin + thump(rng, seconds, 40, 110, 0.14) * 0.9)


def stick(rng):
    seconds = 0.4
    return fade_end(strike(wood(rng.uniform(170, 240), rng, damp=0.05), seconds, 0.0015, rng))


def low_drone(seconds, rng):
    t = times(seconds)
    # Whole numbers of turns over the loop, so it joins without a click.
    tones = [(round(f * seconds) / seconds, level) for f, level in [(36.7, 1.0), (55.1, 0.55), (73.4, 0.25), (37.3, 0.6)]]
    tone = sum(level * np.sin(TAU * f * t + rng.uniform(0, TAU)) for f, level in tones)
    breath = 0.65 + 0.35 * np.sin(TAU * t * round(seconds / 13.0) / seconds)
    air = band(pink(len(t), rng), 70, 260) * 0.6
    fade = int(RATE * 1.0)
    air[:fade] *= np.linspace(0, 1, fade)
    air[-fade:] *= np.linspace(1, 0, fade)
    return (tone * 0.5 + air) * breath


def creak(rng):
    seconds = 1.4
    t = times(seconds)
    rub = band(pink(len(t), rng), 240, 1500) * np.clip(t / 0.3, 0, 1) * np.clip((seconds - t) / 0.5, 0, 1)
    judder = 0.4 + 0.6 * np.abs(np.sin(TAU * np.cumsum(14 + 22 * t / seconds) / RATE))
    return fade_end(rub * judder)


def fight_loop(rng):
    beats = BARS * BEATS_PER_BAR
    seconds = beats * BEAT
    total = int(seconds * RATE)
    music = np.zeros((total, 2))

    def lay(sound, at, level, pan=0.0):
        start = int(at * RATE) % total
        indices = (start + np.arange(len(sound))) % total
        music[indices, 0] += sound * level * (1 - pan)
        music[indices, 1] += sound * level * (1 + pan)

    drone = low_drone(seconds, rng)
    music[:, 0] += drone[:total] * 0.34
    music[:, 1] += drone[:total] * 0.34
    for bar in range(BARS):
        at = bar * BEATS_PER_BAR * BEAT
        lay(heavy_drum(rng), at, 1.0)
        lay(heavy_drum(rng, 58.0), at + 1.5 * BEAT, 0.55, -0.1)
        lay(heavy_drum(rng, 52.0), at + 2 * BEAT, 0.85, 0.1)
        if bar % 2 == 1:
            lay(heavy_drum(rng, 64.0), at + 3 * BEAT, 0.6, -0.15)
            lay(heavy_drum(rng, 64.0), at + 3.5 * BEAT, 0.7, 0.15)
        for step in range(BEATS_PER_BAR * 2):
            if rng.random() < 0.45:
                lay(stick(rng), at + step * BEAT / 2, rng.uniform(0.1, 0.22), rng.uniform(-0.5, 0.5))
        if bar in (2, 6):
            lay(creak(rng), at + rng.uniform(0.2, 2.0) * BEAT, 0.3, rng.uniform(-0.6, 0.6))
    return music


def wakes(rng):
    seconds = 3.4
    t = times(seconds)
    rumble = band(pink(len(t), rng), 30, 180) * np.clip(t / 0.7, 0, 1) * np.clip((seconds - t) / 1.4, 0, 1) * 1.5
    out = rumble + thump(rng, seconds, 30, 95, 0.5) * 1.4
    for _ in range(9):
        crack = strike(wood(rng.uniform(150, 420), rng, damp=0.06), 0.5, 0.002, rng)
        place(out, crack, rng.uniform(0.15, 2.2), rng.uniform(0.25, 0.7))
    place(out, creak(rng), 0.5, 0.7)
    return fade_end(out)


def main():
    save(OUT, "Heartroot Fight", fight_loop(seeded("Heartroot Fight")), loop=True, peak=0.8)
    save(OUT, "Heartroot Wakes", space(wakes(seeded("Heartroot Wakes")), FOREST, 0.2), mono=False)


if __name__ == "__main__":
    main()
