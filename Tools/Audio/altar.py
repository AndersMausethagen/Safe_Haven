"""Synthesises the Purification Altar's sounds (Docs/AltarUXResearch_2026-11-28.md): the hold that charges a working, the
let-go that drains it, three commits by how much the working matters, a tick for numbers counting up, a choice laid on the
stone, and essence hissing clean.

The altar is old stone and cold light, so these are the interface's instruments again (bronze bowls, bells, the lyre, a choir
for the great ones), in D, with a rising bowed tone and breath for the charge. A small commit is a bowl answering a knock; a
large one a struck stone, a bell and a rising strum; a milestone adds the frame drum and voices.

Run from the project root: python Tools/Audio/altar.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403
from interface import bowl  # noqa: E402

OUT = "Assets/_Project/Audio/SFX/Safe Haven Interface"
D2, A2, D3, A3, D4, E4, A4, D5 = 73.42, 110.0, 146.83, 220.0, 293.66, 329.63, 440.0, 587.33


def norm(signal):
    return signal / max(1e-9, np.abs(signal).max())


def sweep(start, end, seconds, rate=RATE, harmonics=(1.0, 0.45, 0.2)):
    """A tone gliding from one pitch to another, soft at both ends."""
    t = np.arange(int(seconds * rate)) / rate
    frequency = start * (end / start) ** (t / seconds)
    phase = 2 * np.pi * np.cumsum(frequency) / rate
    tone = sum(level * np.sin(phase * (k + 1)) for k, level in enumerate(harmonics))
    fade = np.minimum(1.0, t / 0.08) * np.minimum(1.0, (seconds - t) / 0.12)
    return norm(tone * fade)


def strum(notes, seconds, rng, gap=0.05, start=0.0, fall=0.06, brightness=0.5):
    out = blank(seconds)
    for i, frequency in enumerate(notes):
        place(out, lyre(frequency, seconds, rng, brightness=brightness), start + i * gap, max(0.3, 1 - i * fall))
    return norm(out)


def charge(rng):
    """Held: a bowed note and breath climbing a fifth as the working gathers (0.9 s, the hold's length)."""
    seconds = 1.0
    rise = sweep(D3, A3, seconds) * 0.5 + sweep(D4, A4, seconds, harmonics=(1.0, 0.2)) * 0.25
    breath = whoosh(seconds, 300, 3200, 1.8, rng, peak=0.85) * 0.4
    under = norm(bowed(D2, seconds, rng, attack=0.6)) * 0.35
    return darken(norm(rise + breath + under), top=-3, weight=1)


def cancel(rng):
    """Let go too soon: the gathered light drains away downward."""
    seconds = 0.45
    fall = sweep(A3, D3 * 0.75, seconds, harmonics=(1.0, 0.3)) * 0.6
    breath = whoosh(seconds, 2200, 300, 2.0, rng, peak=0.2) * 0.3
    return darken(norm(fall + breath), top=-6, weight=2)


def commit_small(rng):
    """A step: a stone knock and a bowl answering it."""
    seconds = 1.4
    knock = strike(stone(rng.uniform(420, 480), rng), seconds, 0.0012, rng)
    ring = place(blank(seconds), norm(bowl(A3, seconds, rng)) + norm(bowl(D4, seconds, rng)) * 0.4, 0.03, 0.8)
    return darken(norm(knock) * 0.7 + ring + thump(rng, seconds, 55, 130, 0.05) * 0.3, top=-2, weight=0)


def commit_large(rng):
    """A working that matters: struck stone, a bell, and a strum rising out of it."""
    seconds = 2.4
    knock = strike(stone(rng.uniform(360, 420), rng), seconds, 0.0015, rng)
    tolled = place(blank(seconds), norm(bell(D4, seconds, rng, hold=0.9)), 0.04, 0.8)
    rising = strum((D3, A3, D4, E4, A4), seconds, rng, gap=0.06, start=0.08) * 0.55
    low = thump(rng, seconds, 40, 90, 0.12, 0.03) * 0.6
    return darken(norm(knock) * 0.6 + tolled + rising + low, top=-1, weight=-1)


def commit_milestone(rng):
    """The great ones: the frame drum, voices swelling, a full strum and a bell to land on."""
    seconds = 3.6
    beat = place(place(blank(seconds), drum(A2, 1.4, rng), 0), drum(D2 * 1.5, 1.2, rng), 0.16, 0.7)
    voices = place(blank(seconds), norm(choir(D3, 3.0, rng, attack=0.4)), 0.1, 0.5)
    full = strum((D2, A2, D3, A3, D4, A4, D5), seconds, rng, gap=0.034, start=0.28, fall=0.05)
    tolled = place(blank(seconds), norm(bell(D4, seconds, rng, hold=1.1)), 0.32, 0.6)
    low = thump(rng, seconds, 36, 70, 0.18, 0.03)
    return darken(norm(beat) * 0.8 + voices * 0.5 + full * 0.8 + tolled + low * 0.7, top=-1, weight=-1)


def tick(rng):
    """A number counting up: the smallest bowl touch."""
    seconds = 0.22
    return darken(norm(bowl(rng.uniform(A4 * 0.98, A4 * 1.02), seconds, rng)) * 0.6, top=-8, weight=2)


def select(rng):
    """A choice laid on the stone: a soft pluck and a breath."""
    seconds = 0.5
    pluck_ = lyre(D4 * rng.uniform(0.99, 1.01), seconds, rng, brightness=0.4)
    breath = whoosh(0.18, 500, 2400, 2.2, rng, peak=0.4) * 0.15
    return darken(norm(pluck_) * 0.7 + place(blank(seconds), breath, 0), top=-6, weight=1)


def purify(rng):
    """Essence hissing clean and a high bowl settling."""
    seconds = 1.5
    hiss = whoosh(0.7, 1800, 6000, 2.4, rng, peak=0.3) * 0.45
    ring = place(blank(seconds), norm(bowl(A4, seconds, rng)) * 0.7 + norm(bowl(D5, seconds, rng)) * 0.3, 0.35, 0.8)
    return darken(place(blank(seconds), hiss, 0) + ring, top=-3, weight=1)


SOUNDS = [
    ("Altar Charge", charge, HALL, 0.25), ("Altar Cancel", cancel, HALL, 0.2), ("Altar Commit Small", commit_small, HALL, 0.25),
    ("Altar Commit Large", commit_large, HALL, 0.3), ("Altar Commit Milestone", commit_milestone, VALE, 0.35),
    ("Altar Tick", tick, HUT, 0.1), ("Altar Select", select, HUT, 0.12), ("Altar Purify", purify, HALL, 0.25),
]


def main():
    for name, recipe, room, wet in SOUNDS:
        signal = space(recipe(seeded(name)), room, wet)
        save(OUT, name, drive(signal, 2.5) if name == "Altar Commit Milestone" else signal)


if __name__ == "__main__":
    main()
