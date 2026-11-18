"""The raid's music cues (Docs/GameDesignResearch_2026-11-17.md T1, Docs/LookAndFeelResearch_2026-11-18.md A5): a stinger as each
danger stage begins, and the held beacon's heartbeat.

- The Mist Stirs: one low horn far off across the valley, answered, so the player hears that something has changed out there.
- The Hunt: two horns nearer and lower, a frame drum under them, the breath of a bowed string sinking away: the forest coming.
- Collapse: three horns at once with a scrape of the bow and the drum hit twice: run.
- Beacon Drum: one frame-drum heartbeat ("lub-dub"), played again and again while the extraction circle is held, quicker as
  it fills (ExtractionPoint); four takes so it never repeats exactly.

The horn is the lip-reed horn of moments.py (breath into a buzz growing brighter as it grows louder). Low notes and membranes only:
no high tones held (no whistles). Run from the project root: python Tools/Audio/raid_music.py
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import foley  # noqa: E402
from moments import blast  # noqa: E402

OUT = "Assets/_Project/Audio/SFX/Safe Haven Raid"


def stirs():
    random = foley.seeded("Stage Stirs")
    seconds = 5.5
    far = blast(3.4, 98.0, random)
    answer = blast(2.6, 110.0, random)
    mix = foley.place(foley.place(foley.blank(seconds), far, 0.0, 0.8), answer, 1.6, 0.45)
    return foley.space(foley.darken(mix, top=-6, weight=5), foley.VALE, 0.8)


def hunt():
    random = foley.seeded("Stage Hunt")
    seconds = 6.0
    near = blast(3.8, 73.42, random)
    second = blast(3.6, 73.8, random)
    drum = foley.drum(62.0, 1.6, random, tension=0.18) * 1.4
    sink = foley.bowed(55.0, 4.5, random, attack=0.9)
    t = foley.times(4.5)
    sink *= np.exp(-t / 2.4)
    mix = foley.blank(seconds)
    foley.place(mix, drum, 0.0, 1.0)
    foley.place(mix, near, 0.05, 1.0)
    foley.place(mix, second, 0.16, 0.65)
    foley.place(mix, sink / max(1e-9, np.abs(sink).max()), 0.4, 0.35)
    return foley.space(foley.darken(mix, top=-5, weight=4), foley.VALE, 0.6)


def collapse():
    random = foley.seeded("Stage Collapse")
    seconds = 5.0
    mix = foley.blank(seconds)
    for i, (pitch, level) in enumerate(((73.42, 1.0), (98.0, 0.7), (110.0, 0.55))):
        foley.place(mix, blast(3.0, pitch, random), i * 0.04, level)
    for at in (0.0, 0.42):
        foley.place(mix, foley.drum(58.0, 1.4, random, tension=0.2) * 1.5, at, 1.0)
    scrape = foley.bowed(65.4, 2.2, random, attack=0.08)
    foley.place(mix, scrape / max(1e-9, np.abs(scrape).max()), 0.02, 0.4)
    return foley.space(foley.darken(foley.drive(mix, 1.4), top=-5, weight=4), foley.VALE, 0.55)


def heartbeat(take):
    random = foley.seeded(f"Beacon Drum {take}")
    seconds = 0.9
    base = 60.0 * random.uniform(0.97, 1.03)
    lub = foley.drum(base, 0.6, random, tension=0.14)
    dub = foley.drum(base * 0.93, 0.55, random, tension=0.1) * random.uniform(0.6, 0.72)
    mix = foley.blank(seconds)
    foley.place(mix, lub, 0.0, 1.0)
    foley.place(mix, dub, random.uniform(0.2, 0.24), 1.0)
    return foley.space(foley.darken(mix, top=-8, weight=6), (0.5, 6, 0.1), 0.12)


def main():
    os.makedirs(OUT, exist_ok=True)
    foley.save(OUT, "Stage Stirs", stirs())
    foley.save(OUT, "Stage Hunt", hunt())
    foley.save(OUT, "Stage Collapse", collapse())
    for take in range(1, 5):
        foley.save(OUT, f"Beacon Drum {take}", heartbeat(take), mono=True)
    print("made the raid's music cues in", OUT)


if __name__ == "__main__":
    main()
