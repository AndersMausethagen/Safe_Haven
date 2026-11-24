"""Synthesises the abilities' sounds (Docs/CombatSkillsResearch_2026-11-24.md: a cast cue and an impact per school), for the Sound picks
page beside MOSS and Stable Audio takes.

Frost is crackle and crunch over a cold rumble, never a chime: ice breaking is many tiny unpitched fractures. Fire is a roar of
low noise that breathes, with sparks. The hidden folk's blessing is wind and leaves rising. Steel and beast skills are air and earth:
whooshes, thumps, dust and stones. Nothing holds a pure tone above a few hundred hertz (the user: no whistles).

Run from the project root: python Tools/Audio/skills.py [out folder] [stem ...]
Writes <stem>_python_v1.wav and _v2.wav (two seeds).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403


def crackle(seconds, rng, count, low, high, early=1.0, level=1.0):
    """Many small fractures or sparks: unpitched ticks scattered over the time."""
    out = blank(seconds)
    for _ in range(count):
        place(out, chip(rng, low, high, rng.uniform(0.015, 0.05)), seconds * rng.random() ** early, rng.uniform(0.2, 1) * level)
    return out


def breath(seconds, rng, low, high, attack, tau):
    """A swell of soft noise: a gust, a rumble, a whoomp."""
    t = times(seconds)
    return band(pink(len(t), rng), low, high) * bump(t, 0, attack, tau)


def frost_cast(rng):
    s = 1.4
    t = times(s)
    rumble = breath(s, rng, 45, 160, 0.6, 0.5) * 1.2
    # Frost creeping: fractures that grow denser as it gathers.
    ice = blank(s)
    for _ in range(70):
        at = s * (1 - rng.random() ** 2.2) * 0.9
        place(ice, chip(rng, 2200, 7500, rng.uniform(0.01, 0.03)), at, rng.uniform(0.2, 0.8) * (0.3 + at / s))
    hiss = band(white(len(t), rng), 3000, 9000) * bump(t, 0.1, 0.8, 0.4) * 0.12
    return rumble + ice * 0.9 + hiss


def frost_release(rng):
    s = 1.6
    out = blank(s)
    for i in range(6):
        at = i * rng.uniform(0.055, 0.075)
        burst = thump(rng, 0.4, 60, 170, 0.07) * 0.8
        place(burst, crackle(0.25, rng, 14, 1500, 6500, early=3.0), 0.0)
        t = times(0.4)
        burst += band(white(len(t), rng), 900, 4200) * decay(t, 0.045) * 0.7
        place(out, burst, at, 1.0 - i * 0.08)
    place(out, crackle(1.0, rng, 40, 2500, 8000, early=2.5, level=0.6), 0.25)
    return out


def ember_cast(rng):
    # A flame catching, not a blast (the user, 2026-11-24: the first sounded "like an explosion"): a soft breath of fire that
    # swells in over a tenth of a second, under a crackle that carries it.
    s = 1.3
    catch = breath(s, rng, 180, 1400, 0.12, 0.3) * 0.55
    sparks = crackle(s, rng, 70, 900, 5200, early=1.1)
    return catch + sparks * 1.0


def ember_release(rng):
    s = 1.8
    t = times(s)
    roar = band(pink(len(t), rng), 90, 2400) * bump(t, 0, 0.09, 0.55)
    # Fire breathes: a slow uneven swell over the roar.
    breathing = 0.75 + 0.25 * lowpass(white(len(t), rng), 9) / max(1e-9, np.abs(lowpass(white(len(t), rng), 9)).max())
    rush = whoosh(0.7, 200, 1400, 1.2, rng, peak=0.3)
    out = roar * breathing * 1.3
    place(out, rush, 0.0, 0.9)
    place(out, crackle(1.6, rng, 70, 700, 5500, early=1.6), 0.05, 0.7)
    return out


def blessing(rng):
    s = 2.0
    t = times(s)
    gust = sweep(pink(len(t), rng), 300 + 900 * bump(t, 0, 0.7, 0.8), 0.9) * bump(t, 0, 0.6, 0.8)
    leaves = blank(s)
    for _ in range(60):
        place(leaves, chip(rng, 1800, 6000, rng.uniform(0.02, 0.06)), rng.uniform(0.15, 1.6), rng.uniform(0.1, 0.4))
    warmth = breath(s, rng, 80, 260, 0.5, 0.7) * 0.5
    return gust * 1.2 + leaves * 0.6 + warmth


def roots(rng):
    s = 1.6
    out = blank(s)
    place(out, thump(rng, 0.6, 50, 140, 0.18), 0.0, 1.0)
    for i in range(4):
        at = 0.05 + i * rng.uniform(0.12, 0.2)
        creak = strike(wood(rng.uniform(70, 130), rng, damp=0.09), 0.5, 0.004, rng)
        place(out, creak, at, 0.8)
        place(out, crackle(0.3, rng, 10, 600, 2800, early=2.0), at, 0.7)
    t = times(s)
    soil = band(pink(len(t), rng), 150, 900) * bump(t, 0.02, 0.1, 0.5) * 0.6
    return out + soil


def wrath_slam(rng):
    s = 2.2
    out = blank(s)
    place(out, thump(rng, 1.4, 30, 120, 0.45, sweep_tau=0.03), 0.0, 1.3)
    place(out, strike(stone(rng.uniform(140, 220), rng), 0.5, 0.003, rng), 0.0, 0.8)
    place(out, crackle(0.5, rng, 25, 700, 3500, early=3.0), 0.0, 0.8)
    place(out, scatter(1.6, 30, lambda: strike(stone(rng.uniform(300, 900), rng, 8), 0.15, 0.002, rng), rng, early=1.6), 0.12, 0.45)
    t = times(s)
    dust = band(pink(len(t), rng), 250, 1800) * bump(t, 0.03, 0.12, 0.7) * 0.4
    return out + dust


def leap_jump(rng):
    s = 1.0
    out = whoosh(0.7, 150, 900, 1.0, rng, peak=0.35) * 1.2
    out = np.concatenate([out, blank(s - 0.7)])
    place(out, cloth(0.3, rng), 0.02, 0.5)
    place(out, thump(rng, 0.2, 80, 200, 0.04), 0.0, 0.5)
    return out


def leap_land(rng):
    s = 1.5
    out = blank(s)
    place(out, thump(rng, 1.0, 40, 140, 0.3), 0.0, 1.2)
    place(out, strike(stone(rng.uniform(160, 260), rng), 0.3, 0.003, rng), 0.0, 0.6)
    t = times(s)
    dust = band(pink(len(t), rng), 300, 2200) * bump(t, 0.01, 0.06, 0.45) * 0.5
    place(out, scatter(1.0, 18, lambda: chip(rng, 600, 3000, 0.05), rng, early=1.8), 0.05, 0.5)
    return out + dust


def whirl(rng):
    s = 1.0
    out = blank(s)
    place(out, whoosh(0.34, 400, 2600, 1.4, rng) + 0.6 * whoosh(0.34, 180, 520, 1.0, rng), 0.0, 0.8)
    place(out, whoosh(0.36, 450, 2900, 1.4, rng) + 0.6 * whoosh(0.36, 200, 560, 1.0, rng), 0.3, 1.0)
    return out


def lunge(rng):
    s = 1.0
    out = blank(s)
    place(out, whoosh(0.45, 220, 1300, 1.0, rng, peak=0.3), 0.0, 1.0)
    for i in range(3):
        t = times(0.12)
        scuff = band(white(len(t), rng), 500, 3000) * bump(t, 0, 0.01, 0.04)
        place(out, scuff, 0.02 + i * 0.09, 0.4)
    return out


def shatter(rng):
    s = 1.5
    out = blank(s)
    t = times(0.2)
    place(out, band(white(len(t), rng), 1500, 7000) * decay(t, 0.012) * 1.2, 0.0)
    place(out, strike(stone(rng.uniform(700, 1100), rng, 24), 0.25, 0.0015, rng), 0.0, 0.6)
    place(out, thump(rng, 0.3, 90, 220, 0.05), 0.0, 0.5)
    place(out, crackle(1.2, rng, 90, 2000, 9000, early=2.6), 0.0, 0.9)
    return out


SOUNDS = [
    ("skill_frost_cast", frost_cast), ("skill_frost_release", frost_release), ("skill_ember_cast", ember_cast),
    ("skill_ember_release", ember_release), ("skill_blessing", blessing), ("skill_roots", roots), ("skill_wrath_slam", wrath_slam),
    ("skill_leap_jump", leap_jump), ("skill_leap_land", leap_land), ("skill_whirl", whirl), ("skill_lunge", lunge),
    ("status_shatter", shatter),
]


def main(folder, only=None):
    for stem, recipe in SOUNDS:
        if only and stem not in only:
            continue
        for take in (1, 2):
            name = f"{stem}_python_v{take}"
            save(folder, name, space(recipe(seeded(name)), FOREST, 0.1), mono=True)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "Tools/Audio/SkillTakes", sys.argv[2:])
