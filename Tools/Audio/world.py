"""Synthesises what happens out in the world: creatures dying and scenting you, timber and stone giving way, a troll's tread, the wind.

A creature's throat is modelled long, far longer than a person's, so its vowels sit low. Period doubling gives the growl
and it ends in breath and the body hitting the ground. A break is hundreds of small failures rather than one bang, which
is why it is a scatter of cracks over a low thump. The wind is noise cut into bands in the frequency domain, each with
its own gusts that repeat exactly over the loop, so it has no seam to hide: it is written over the clips the scenes use.

Run from the project root: python Tools/Audio/world.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven World"
WIND = "Assets/_Project/Audio/Placeholder"


def creature_dies(rng):
    seconds = 1.5
    length = rng.uniform(0.75, 1.0)
    n = int(length * RATE)
    t = times(length)
    start = rng.uniform(110, 165)
    pitch = start * (1 + 0.35 * np.sin(np.pi * np.clip(t / 0.22, 0, 1))) * np.exp(-t / (length * 0.55))
    shape = vowel((rng.uniform(420, 520), 230, 4, 1.0), (rng.uniform(760, 900), 420, 5, 1.1), (1500, 980, 6, 0.7), (2600, 1800, 7, 0.25))(n)
    throat = voice(pitch, shape, rng, rough=0.95, breath=0.45) + voice(pitch * 0.5, shape, rng, rough=1.0, breath=0.2) * 0.35
    cry = fade_end(drive(throat, 2.2) * bump(t, 0, 0.03, length * 0.34), 0.35)
    last_breath = sweep(pink(n, rng), np.geomspace(800, 300, n), 3) * bump(t, length * 0.5, 0.08, 0.16) * 0.2
    fall = thump(rng, 0.6, 48, 120, 0.09) + band(pink(int(0.6 * RATE), rng), 300, 1800) * decay(times(0.6), 0.05) * 0.3
    mix = place(blank(seconds), fade_end(cry / np.abs(cry).max() + last_breath, 0.3), 0)
    return darken(place(mix, fall, length * rng.uniform(0.72, 0.88), 0.45), weight=1)


def suspicious(rng):
    """A breath drawn through a wet nose and a low questioning grunt: it has noticed something, and you should stop moving."""
    seconds = 0.8
    n = int(seconds * RATE)
    t = times(seconds)
    sniff = band(sweep(pink(n, rng), np.geomspace(900, 1700, n), 3), 500, 3200) * bump(t, 0.0, 0.05, 0.05) * 0.5
    grunt_length = rng.uniform(0.3, 0.4)
    g = times(grunt_length)
    pitch = rng.uniform(58, 72) * (1 + 0.28 * (g / grunt_length) ** 2)
    shape = vowel((340, 420, 4, 1.0), (620, 760, 5, 0.5), (1300, 1500, 6, 0.2))(len(g))
    grunt = fade_end(voice(pitch, shape, rng, rough=0.8, breath=0.3) * bump(g, 0, 0.05, grunt_length * 0.4), 0.45)
    return darken(place(sniff, grunt / np.abs(grunt).max(), 0.14, 0.9), weight=1)


def spotted(rng):
    """The moment you are seen: a low iron stroke with a drum under it, on the beat and with no warning."""
    seconds = 3.0
    tolled = bell(rng.uniform(205, 225), seconds, rng, hold=0.62)
    return darken(place(tolled / np.abs(tolled).max() + thump(rng, seconds, 40, 100, 0.2, 0.04) * 0.3, drum(98, 1.6, rng), 0, 0.3), weight=0)


def troll_step(rng):
    seconds = 1.5
    t = times(seconds)
    tread = thump(rng, seconds, 30, 72, 0.22, 0.05) * 1.2 + thump(rng, seconds, 48, 110, 0.08) * 0.5
    earth = band(pink(len(t), rng), 70, 420) * bump(t, 0.003, 0.008, 0.16) * 0.8
    patter = scatter(1.1, 34, lambda: chip(rng, 500, 1800), rng, early=1.5) * 0.28
    return darken(drive(place(tread + earth, patter, 0.08), 1.8), weight=3)


def splinters(rng, seconds, count, low=700, high=2600):
    return scatter(seconds, count, lambda: chip(rng, low, high), rng)


def wood_breaks(rng):
    seconds = 1.4
    n = int(seconds * RATE)
    t = times(seconds)
    groan = strike(wood(rng.uniform(120, 170), rng, damp=0.16, count=16), seconds, 0.0016, rng)
    tear = band(pink(n, rng), 500, 3000) * bump(t, 0.0, 0.01, 0.16) * 0.3
    mix = groan + tear + place(blank(seconds), splinters(rng, 0.9, 42), 0, 0.8) + thump(rng, seconds, 48, 130, 0.13) * 0.9
    return darken(drive(mix, 2.2), weight=2)


def stone_breaks(rng):
    seconds = 1.6
    n = int(seconds * RATE)
    t = times(seconds)
    crack = strike(stone(rng.uniform(280, 380), rng, 30), seconds, 0.0007, rng) * 2.5
    falling = scatter(1.3, 90, lambda: chip(rng, 500, 2600), rng, early=1.6) * 1.2
    rumble = band(pink(n, rng), 50, 300) * bump(t, 0.004, 0.03, 0.34) * 0.9
    return darken(drive(place(crack + rumble + thump(rng, seconds, 40, 110, 0.15), falling, 0.01), 2.2), weight=2)


def structure_breaks(rng):
    """A whole building going: beams, then its weight landing, then what comes down after."""
    seconds = 2.6
    n = int(seconds * RATE)
    t = times(seconds)
    beams = place(blank(seconds), wood_breaks(rng), 0.0, 0.8)
    place(beams, wood_breaks(rng), rng.uniform(0.22, 0.34), 0.7)
    landing = place(blank(seconds), thump(rng, 1.6, 32, 95, 0.32, 0.05) * 1.3, rng.uniform(0.32, 0.44))
    dust = band(pink(n, rng), 90, 800) * bump(t, 0.3, 0.12, 0.6) * 0.32
    return darken(drive(beams + landing * 0.8 + dust + place(blank(seconds), splinters(rng, 1.6, 36, 400, 1500), 0.4, 0.5), 1.7), weight=2)


def wind(seconds, bands, seed):
    """Bands of noise that gust on their own. Everything is whole cycles of the loop, so it meets itself exactly."""
    rng = seeded(seed)
    n = int(seconds * RATE)
    frequencies = np.fft.rfftfreq(n, 1 / RATE)
    t = np.arange(n) / n
    out = np.zeros((n, 2))
    for centre, width, level, restless in bands:
        mask = np.exp(-0.5 * (np.log(np.maximum(frequencies, 1) / centre) / width) ** 2)
        mask /= np.sqrt(np.sum(mask ** 2))
        cycles = rng.integers(1, 4, 4) * np.array([1, 2, 3, 5])
        phases = rng.uniform(0, TAU, 4)
        for channel, lean in enumerate((-0.35, 0.35)):
            noise = np.fft.irfft(np.fft.rfft(white(n, rng)) * mask, n)
            gust = sum(np.sin(TAU * c * t + p + lean * (i + 1)) / (i + 1) for i, (c, p) in enumerate(zip(cycles, phases)))
            out[:, channel] += noise * level * np.clip(1 + restless * gust / 1.6, 0.05, None)
    return out


# A wind in the raids that moans low through dead trunks, and a breeze at home that is mostly leaves.
WILDS = [(48, 0.5, 1.0, 0.5), (110, 0.4, 1.0, 0.7), (230, 0.3, 0.9, 0.9), (340, 0.1, 0.5, 1.3), (470, 0.08, 0.34, 1.4), (800, 0.45, 0.5, 1.0), (2200, 0.5, 0.14, 1.1)]
HAVEN = [(70, 0.5, 0.45, 0.4), (260, 0.5, 0.6, 0.6), (900, 0.45, 0.7, 0.8), (2600, 0.4, 0.75, 1.0), (5200, 0.3, 0.3, 1.2)]

# Stem, recipe, variants, how much of the forest is on it. Variants are made per stem so they stay apart.
SOUNDS = [
    ("Creature Dies", creature_dies, 4, 0.2), ("Suspicious", suspicious, 3, 0.14), ("Spotted", spotted, 3, 0.2), ("Troll Step", troll_step, 5, 0.12),
    ("Wood Breaks", wood_breaks, 3, 0.14), ("Stone Breaks", stone_breaks, 3, 0.14), ("Structure Breaks", structure_breaks, 3, 0.2),
]


def main():
    for stem, recipe, count, wet in SOUNDS:
        for i in range(1, count + 1):
            name = f"{stem} {i}"
            save(OUT, name, space(recipe(seeded(name)), FOREST, wet), mono=True)
    save(WIND, "Ambience_WildsWind", wind(24, WILDS, "wilds wind"), peak=0.7, loop=True)
    save(WIND, "Ambience_HavenBreeze", wind(24, HAVEN, "haven breeze"), peak=0.7, loop=True)


if __name__ == "__main__":
    main()
