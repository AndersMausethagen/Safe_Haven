"""Synthesises the voice of the portal: something very old speaking through stone, with no words in it.

A throat far larger than a person's, slow and falling across each phrase, shapes vowels that never quite become speech. An octave
beneath it the stones themselves carry the same line, a low hum runs under all of it without stopping, and a thin breath through the
same vowels gives it the air of a whisper. It sits in a stone chamber with a long dark tail, so it seems to come from inside the arch.
The player reads the words; this only has to say that something is speaking. Each take is its own phrase, so lines don't repeat.

Run from the project root: python Tools/Audio/portal.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Portal"
CHAMBER = (2.4, 14, 0.08)

# First three formants of dark vowels, lowered as the tract of something enormous would lower them.
VOWELS = [(470, 760, 1750), (330, 600, 1650), (250, 520, 1600), (380, 1050, 1800), (520, 860, 1700)]


def phrase_shape(seconds, rng):
    """Syllables: how loud at each moment, and which vowel it is moving towards."""
    n = int(seconds * RATE)
    level = np.zeros(n)
    tracks = [np.zeros(n) for _ in range(3)]
    at = rng.uniform(0.05, 0.15)
    vowel_now = VOWELS[rng.integers(len(VOWELS))]
    start = 0
    while at < seconds - 0.25:
        length = rng.uniform(0.2, 0.46)
        first = int(at * RATE)
        last = min(n, first + int(length * RATE))
        s = np.linspace(0, 1, last - first, endpoint=False)
        level[first:last] = np.maximum(level[first:last], np.sin(np.pi * s) ** 0.7 * rng.uniform(0.62, 1.0))
        for track, target in zip(tracks, vowel_now):
            track[start:last] = target
        start = last
        vowel_now = VOWELS[rng.integers(len(VOWELS))]
        # Words run together, with now and then a breath between them.
        at += length + (rng.uniform(0.2, 0.42) if rng.random() < 0.2 else rng.uniform(0.01, 0.06))

    for track, target in zip(tracks, vowel_now):
        track[start:] = target
    formants = [np.maximum(lowpass(track - track[0], 9) + track[0], 150.0) for track in tracks]
    return lowpass(level, 16), formants


def level_to(signal, rms):
    return signal * rms / max(1e-9, np.sqrt(np.mean(signal ** 2)))


def murmur(seconds, rng, base):
    t = times(seconds)
    level, (first, second, third) = phrase_shape(seconds, rng)
    fall = base * (1.06 - 0.12 * t / seconds)
    lilt = lowpass(white(len(t), rng), 2.2)
    lilt /= max(1e-9, np.abs(lilt).max())
    pitch = fall * (1 + 0.045 * lilt)
    tract = [(first, 6, 1.0), (second, 7, 0.55), (third, 9, 0.14)]

    throat = voice(pitch, tract, rng, rough=0.12, breath=0.3)
    stones = voice(pitch * 0.5, tract, rng, rough=0.3, breath=0.1)
    breath = np.zeros(len(t))
    for centre, q, amount in [(first * 1.1, 4, 0.8), (second * 1.2, 5, 0.5), (third, 6, 0.2)]:
        breath += sweep(white(len(t), rng), centre, q) * amount

    # The hum doesn't stop between words, though it swells under them.
    swell = 0.55 + 0.45 * lowpass(level, 3) / max(1e-9, level.max())
    drift = np.cumsum(fall * 0.5 * (1 + 0.003 * np.sin(TAU * 0.31 * t))) / RATE
    hum = (np.sin(TAU * drift) + 0.45 * np.sin(TAU * drift * 2.003) + 0.2 * np.sin(TAU * drift * 3.01)) * swell

    ends = np.clip(t / 0.35, 0, 1) * np.clip((seconds - t) / 0.7, 0, 1)
    # Balanced by loudness, not by raw level: the voice has to carry on small speakers, with the hum only felt beneath it.
    speech = (level_to(highpass(throat, 110), 1.0) + level_to(highpass(stones, 70), 0.42) + level_to(breath, 0.1)) * level
    dry = (speech + level_to(hum, 0.13)) * ends
    return darken(space(lowpass(dry, 3000), CHAMBER, 0.35, tail=1.8), weight=1.0)


def main():
    # Six takes: long enough for most lines, as a line is read at about the same pace.
    for index, (seconds, base) in enumerate([(3.0, 74), (3.6, 70), (4.2, 76), (2.6, 72), (3.9, 68), (4.6, 73)], start=1):
        rng = seeded(f"portal voice {index}")
        save(OUT, f"portal_voice_{index:02d}", murmur(seconds, rng, base), peak=0.85, mono=True)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    main()
