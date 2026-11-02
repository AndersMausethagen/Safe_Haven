"""Synthesises the sounds that talk to the player: the menus, quests, loot and warnings of the body.

Nothing here is a tone a computer would make. The menus are wood, leather and a laid-down hand. Quests are a frame drum and
a tagelharpa's open fifth, and finishing one adds a lyre, because a fifth is settled without being cheerful. Loot climbs
by depth rather than by rising notes: a rare find is a struck bowl, a legendary one a bell in a valley with voices under it.

Run from the project root: python Tools/Audio/interface.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Interface"
D1, D2, A2, D3, A3, D4 = 36.71, 73.42, 110.0, 146.83, 220.0, 293.66


def click(rng):
    seconds = 0.12
    tap = strike(wood(rng.uniform(1000, 1300), rng, damp=0.009, count=8), seconds, 0.0005, rng)
    body = thump(rng, seconds, 48, 76, 0.028) * 0.5
    return darken(tap + body + thump(rng, seconds, 130, 210, 0.012) * 0.3, top=-3, weight=2)


def window_open(rng):
    """A leather flap thrown back onto a table."""
    seconds = 0.4
    flap = cloth(0.2, rng, 900)
    knock = strike(wood(rng.uniform(320, 400), rng, damp=0.04), seconds, 0.0014, rng)
    return darken(place(place(blank(seconds), flap, 0, 1.1), knock, 0.13, 0.55), weight=2)


def window_close(rng):
    """The flap let fall, softly and lower."""
    seconds = 0.36
    flap = cloth(0.14, rng, 650)
    knock = strike(wood(rng.uniform(210, 280), rng, damp=0.05), seconds, 0.002, rng)
    return darken(place(place(blank(seconds), flap, 0, 0.8), knock + thump(rng, seconds, 80, 140, 0.03) * 0.5, 0.06, 0.7), weight=3)


def counter_ready(rng):
    """A blade turned in the hand: bright enough to catch in a fight, short enough not to get in its way."""
    seconds = 0.7
    glint = strike(iron(rng.uniform(1150, 1350), rng, hold=0.28), seconds, 0.00035, rng)
    return darken(glint + place(blank(seconds), scrape(0.09, 2100, rng), 0, 0.5), top=-2, weight=0)


def out_of_stamina(rng):
    """The breath running out, with the heart under it."""
    seconds = 0.95
    n = int(seconds * RATE)
    t = times(seconds)
    throat = pink(n, rng)
    breath = sweep(throat, np.geomspace(1000, 620, n), 4) + 0.5 * sweep(throat, np.geomspace(1900, 1250, n), 6)
    breath = band(breath, 300, 3000) * bump(t, 0.04, 0.09, 0.2) * 0.85
    heart = place(place(blank(seconds), thump(rng, 0.3, 44, 78, 0.06, 0.03), 0.0), thump(rng, 0.3, 40, 70, 0.07, 0.03), 0.2, 0.7)
    return darken(breath + heart, weight=2)


def quest_started(rng):
    seconds = 2.6
    string = bowed(D1, seconds, rng) * 0.22 + bowed(D2, seconds, rng) * 0.8 + bowed(A2, seconds, rng) + bowed(D3, seconds, rng) * 0.8 + bowed(A3, seconds, rng) * 0.4
    beat = place(place(blank(seconds), drum(A2, 1.4, rng), 0), drum(A2, 1.4, rng), 0.21, 0.6)
    low_beat = place(place(blank(seconds), thump(rng, 1.4, 42, 72, 0.12, 0.025), 0.02), thump(rng, 1.4, 42, 72, 0.12, 0.025), 0.21, 0.55)
    return darken(place(beat * 0.6 + low_beat, string / np.abs(string).max(), 0, 0.9), weight=2)


def quest_completed(rng):
    seconds = 3.6
    strum = blank(seconds)
    for i, frequency in enumerate((D2, A2, D3, A3, D4)):
        place(strum, lyre(frequency, seconds, rng), 0.04 + i * 0.038, 1 - i * 0.1)
    held = bowed(D2, seconds, rng) + bowed(A2, seconds, rng) * 0.6
    tolled = bell(D3, seconds, rng, hold=0.8)
    mix = place(place(strum / np.abs(strum).max(), held / np.abs(held).max(), 0, 0.3), drum(D2, 1.6, rng), 0, 1.0)
    mix += thump(rng, seconds, 42, 70, 0.14, 0.025) * 0.3
    return darken(place(mix, tolled / np.abs(tolled).max(), 0.05, 0.28), weight=-2)


def crafted(rng):
    """A smith's last two taps, and the work set down."""
    seconds = 0.9
    first = strike(iron(rng.uniform(720, 840), rng, hold=0.42), seconds, 0.0003, rng)
    second = strike(iron(rng.uniform(820, 940), rng, hold=0.3), seconds, 0.0003, rng)
    setdown = strike(wood(rng.uniform(170, 230), rng, damp=0.06), seconds, 0.002, rng)
    mix = place(place(first + thump(rng, seconds, 80, 170, 0.03) * 0.5, second, 0.17, 0.55), setdown, 0.42, 0.5)
    return darken(mix, top=-4, weight=2)


def bowl(frequency, seconds, rng):
    """A bronze bowl struck with something soft. Its modes are nearly harmonic, which is why it hums rather than clangs."""
    modes = []
    for ratio, level, tau in ((1, 0.8, 1.1), (2.71, 0.75, 0.8), (3.9, 0.3, 0.55), (5.15, 0.45, 0.45), (6.8, 0.2, 0.3), (8.3, 0.2, 0.22)):
        modes += [(frequency * ratio, level, tau), (frequency * ratio * 1.0022, level * 0.7, tau * 0.9)]
    return strike(modes, seconds, 0.0011, rng)


def loot_rare(rng):
    seconds = 2.8
    return darken(bowl(A3, seconds, rng) + bowl(A3 * 1.5, seconds, rng) * 0.6 + thump(rng, seconds, 50, 120, 0.08) * 0.2, weight=0)


def loot_epic(rng):
    seconds = 3.0
    tolled = bell(D3, seconds, rng, hold=0.75)
    mix = bowl(A3, seconds, rng) * 0.6 + tolled / np.abs(tolled).max()
    return darken(place(mix, lyre(D2, seconds, rng) + lyre(A2, seconds, rng), 0.05, 0.3) + thump(rng, seconds, 42, 110, 0.14) * 0.25, weight=1)


def loot_legendary(rng):
    seconds = 4.6
    tolled = bell(D3 * 0.75, seconds, rng, hold=1.15)
    voices = choir(D2 * 1.5, seconds, rng) + choir(D3, seconds, rng) * 0.6 + choir(A2 * 1.5, seconds, rng) * 0.35
    strum = blank(seconds)
    for i, frequency in enumerate((D2, A2, D3, A3)):
        place(strum, lyre(frequency, seconds, rng), 0.1 + i * 0.05, 0.7)
    mix = tolled / np.abs(tolled).max() + voices / np.abs(voices).max() * 0.45 + strum / np.abs(strum).max() * 0.35
    return darken(mix + thump(rng, seconds, 36, 90, 0.26, 0.04) * 0.35, weight=1)


# Stem, recipe, variants, the place it rings in, how much of it.
SOUNDS = [
    ("Click", click, 3, HUT, 0.06), ("Window Open", window_open, 2, HUT, 0.1), ("Window Close", window_close, 2, HUT, 0.1),
    ("Counter Ready", counter_ready, 2, HUT, 0.12), ("Out of Stamina", out_of_stamina, 2, HUT, 0.1), ("Crafted", crafted, 3, HUT, 0.18),
    ("Quest Started", quest_started, 1, HALL, 0.3), ("Quest Completed", quest_completed, 1, HALL, 0.3),
    ("Loot Rare", loot_rare, 1, HALL, 0.3), ("Loot Epic", loot_epic, 1, HALL, 0.4), ("Loot Legendary", loot_legendary, 1, VALE, 0.5),
]


# Quests have to carry over the music at full volume, and their opening drum spends all the headroom: saturating lifts what follows it.
FORWARD = {"Quest Started": 3.0, "Quest Completed": 3.0}


def main():
    for stem, recipe, count, room, wet in SOUNDS:
        for i in range(1, count + 1):
            name = stem if count == 1 else f"{stem} {i}"
            signal = space(recipe(seeded(name)), room, wet)
            save(OUT, name, drive(signal, FORWARD[stem]) if stem in FORWARD else signal)


if __name__ == "__main__":
    main()
