"""Synthesises the sounds of getting better: a level, a milestone, a perk taken, Insight put in, and the crafts that came
out well.

They have to feel like a reward without sounding like a slot machine. So they are the interface's instruments, the lyre,
bronze bowls and bells, frame drum, in D with its open fifths: a level is a quick strum climbing an octave and a half, a
milestone a drum, a swelling string and a full strum that lands on a bell. Good work at the anvil keeps the smith's last
taps and lets a bowl (Fine) or a bell and a rising strum (Masterwork) ring out after them.

Run from the project root: python Tools/Audio/progression.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403
from interface import bowl, crafted  # noqa: E402

OUT = "Assets/_Project/Audio/SFX/Safe Haven Interface"
D2, A2, D3, E3, A3, D4, E4, A4, D5 = 73.42, 110.0, 146.83, 164.81, 220.0, 293.66, 329.63, 440.0, 587.33


def norm(signal):
    return signal / max(1e-9, np.abs(signal).max())


def strum(notes, seconds, rng, gap=0.045, start=0.0, fall=0.08, brightness=0.45):
    out = blank(seconds)
    for i, frequency in enumerate(notes):
        place(out, lyre(frequency, seconds, rng, brightness=brightness), start + i * gap, max(0.3, 1 - i * fall))
    return norm(out)


def level_up(rng):
    """A quick strum climbing from D to the A above the octave, a bowl under its top note."""
    seconds = 1.8
    climb = strum((D3, A3, D4, E4, A4), seconds, rng, gap=0.055, fall=0.05, brightness=0.55)
    ring = place(blank(seconds), norm(bowl(A3 * 2, seconds, rng)), 0.22, 0.32)
    return darken(climb + ring + thump(rng, seconds, 60, 140, 0.05) * 0.25, top=-2, weight=0)


def milestone(rng):
    """The big one: a frame drum, a string swelling under it, a full strum and a bell to land on."""
    seconds = 3.4
    beat = place(place(blank(seconds), drum(A2, 1.4, rng), 0), drum(D2 * 1.5, 1.2, rng), 0.16, 0.7)
    low = place(place(blank(seconds), thump(rng, 1.4, 38, 70, 0.16, 0.03), 0.0), thump(rng, 1.2, 40, 76, 0.12, 0.03), 0.16, 0.6)
    swell = norm(bowed(D2, seconds, rng, attack=0.5) + bowed(A2, seconds, rng, attack=0.5) * 0.7 + bowed(D3, seconds, rng, attack=0.6) * 0.5)
    full = strum((D2, A2, D3, A3, D4, A4, D5), seconds, rng, gap=0.032, start=0.3, fall=0.06, brightness=0.5)
    tolled = place(blank(seconds), norm(bell(D4, seconds, rng, hold=0.9)), 0.36, 0.55)
    mix = norm(beat) * 0.8 + low * 0.8 + swell * 0.35 + full * 0.8 + tolled
    return darken(mix, top=-1, weight=-1)


def perk_chosen(rng):
    """A rune knocked into stone, and the stone answering."""
    seconds = 1.5
    knock = strike(stone(rng.uniform(380, 460), rng), seconds, 0.0012, rng)
    answer = place(blank(seconds), norm(bowl(A3, seconds, rng)), 0.05, 0.6)
    pluck_ = place(blank(seconds), lyre(D4, seconds, rng, brightness=0.5), 0.09, 0.5)
    return darken(norm(knock) * 0.9 + answer + pluck_ + thump(rng, seconds, 50, 120, 0.06) * 0.4, top=-2, weight=1)


def insight(rng):
    """A thought settling: breath and a high bowl."""
    seconds = 1.3
    breath = whoosh(0.5, 400, 2600, 2.5, rng, peak=0.5) * 0.35
    ring = norm(bowl(D4, seconds, rng)) * 0.7 + norm(bowl(A4, seconds, rng)) * 0.3
    return darken(place(blank(seconds), breath, 0) + place(blank(seconds), ring, 0.12), top=-3, weight=1)


def craft_fine(rng):
    """The smith's taps, and a bowl answering them: this one came out well."""
    seconds = 2.2
    taps = crafted(rng)
    ring = place(blank(seconds), norm(bowl(A3, seconds, rng)) + norm(bowl(A3 * 1.5, seconds, rng)) * 0.45, 0.34, 0.7)
    return darken(place(blank(seconds), taps, 0) + ring, top=-2, weight=0)


def craft_masterwork(rng):
    """The taps, then a bell and a strum rising out of the work."""
    seconds = 3.6
    taps = crafted(rng)
    tolled = place(blank(seconds), norm(bell(D4, seconds, rng, hold=1.0)), 0.34, 0.8)
    rising = strum((D3, A3, D4, A4, D5), seconds, rng, gap=0.07, start=0.42, fall=0.04, brightness=0.55) * 0.6
    under = place(blank(seconds), norm(bowl(A3, seconds, rng)), 0.34, 0.4)
    voices = place(blank(seconds), norm(choir(D3, 2.6, rng, attack=0.6)), 0.4, 0.18)
    return darken(place(blank(seconds), taps, 0) + tolled + rising + under + voices, top=-1, weight=-1)


def craft_queued(rng):
    """An order given: a hand laid on the bench and a tally knocked into it."""
    seconds = 0.4
    hand = cloth(0.12, rng, 900) * 0.6
    tally = strike(wood(rng.uniform(560, 680), rng, damp=0.02, count=8), seconds, 0.0006, rng)
    return darken(place(place(blank(seconds), hand, 0), tally, 0.05, 0.8), top=-3, weight=2)


SOUNDS = [
    ("Level Up", level_up, 1, HALL, 0.3), ("Milestone", milestone, 1, VALE, 0.35), ("Perk Chosen", perk_chosen, 1, HALL, 0.25),
    ("Insight", insight, 1, HALL, 0.25), ("Craft Fine", craft_fine, 1, HUT, 0.22), ("Craft Masterwork", craft_masterwork, 1, HALL, 0.35),
    ("Craft Queued", craft_queued, 2, HUT, 0.1),
]

# A milestone has to carry over the music at full volume: saturation lifts what follows its opening drum.
FORWARD = {"Milestone": 2.5}


def main():
    for stem, recipe, count, room, wet in SOUNDS:
        for i in range(1, count + 1):
            name = stem if count == 1 else f"{stem} {i}"
            signal = space(recipe(seeded(name)), room, wet)
            save(OUT, name, drive(signal, FORWARD[stem]) if stem in FORWARD else signal)


if __name__ == "__main__":
    main()
