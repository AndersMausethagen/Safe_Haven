"""Synthesises the Haven's festival music: a lively harvest dance and a slow midwinter air, both seamless loops.

Both are played by a small village band: a fiddle (a bowed sawtooth with vibrato through a few body resonances), a
hurdy-gurdy drone on the tonic and fifth, and a frame drum (a low thump with a skin slap). The harvest tune is a
quick polska in D dorian; the midwinter air is slower, in D minor, with a soft bell on each bar's first beat.

Run from the project root: python Tools/Audio/festival.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = "Assets/_Project/Audio/Music/Safe Haven Festivals"
rng = np.random.default_rng(97)
D4 = 293.66


def band(signal, low, high, order=2):
    return sosfilt(butter(order, [low, high], btype="bandpass", fs=RATE, output="sos"), signal)


def lowpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="lowpass", fs=RATE, output="sos"), signal)


def note(semitones):
    return D4 * 2 ** (semitones / 12)


def fiddle(freq, seconds, loud=1.0):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    vibrato = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * t) * np.clip(t / 0.15, 0, 1)
    phase = 2 * np.pi * np.cumsum(freq * vibrato) / RATE
    saw = 2 * ((phase / (2 * np.pi)) % 1) - 1
    bow = band(rng.standard_normal(length), freq, freq * 6) * 0.04
    tone = saw + bow
    body = band(tone, 280, 3500) + band(tone, 500, 700) * 0.6 + band(tone, 2500, 3200) * 0.4
    env = np.minimum(1, t / 0.03) * np.minimum(1, (seconds - t) / 0.05)
    return body * env * loud


def drone(freqs, seconds):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    out = np.zeros(length)
    for f in freqs:
        phase = 2 * np.pi * f * t + rng.uniform(0, 6)
        out += (2 * ((phase / (2 * np.pi)) % 1) - 1) * 0.5
    buzz = 1 + 0.15 * np.sin(2 * np.pi * 31 * t)
    return band(out * buzz, 120, 2500)


def drum(seconds, low=True):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    if low:
        thump = np.sin(2 * np.pi * (70 + 60 * np.exp(-t / 0.02)) * t) * np.exp(-t / 0.18)
        return thump + band(rng.standard_normal(length), 200, 900) * np.exp(-t / 0.03) * 0.3
    return band(rng.standard_normal(length), 800, 5000) * np.exp(-t / 0.04) * 0.5


def bell(freq, seconds):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    return sum(np.sin(2 * np.pi * freq * r * t) * np.exp(-t / d) * a
               for r, d, a in ((1, 1.8, 1), (2.76, 0.9, 0.4), (5.4, 0.4, 0.2)))


def place(track, sound, at):
    start = int(at * RATE)
    end = min(len(track), start + len(sound))
    track[start:end] += sound[:end - start]
    # What runs past the end wraps round, so the loop has no seam.
    if start + len(sound) > len(track):
        rest = sound[end - start:]
        track[:len(rest)] += rest


def song(melody, beat, drone_notes, drums, bells=False):
    total = sum(length for _, length in melody) * beat
    track = np.zeros(int(total * RATE))
    at = 0.0
    for pitch, length in melody:
        if pitch is not None:
            place(track, fiddle(note(pitch), length * beat * 0.95, 0.8), at)
        at += length * beat
    place(track, drone([note(p) for p in drone_notes], total) * 0.35, 0)
    beats = int(round(total / beat))
    for i in range(beats):
        kind = drums(i)
        if kind:
            place(track, drum(0.5, kind == "low") * 0.7, i * beat)
        if bells and i % 3 == 0:
            place(track, bell(note(12 if (i // 3) % 2 == 0 else 7), 3.0) * 0.18, i * beat)
    return lowpass(track, 9000)


def main():
    os.makedirs(OUT, exist_ok=True)
    # Harvest polska: three beats a bar, D dorian, two phrases repeated.
    a = [(0, 1), (2, 0.5), (3, 0.5), (5, 1), (7, 1), (5, 0.5), (3, 0.5), (2, 1), (0, 1), (-2, 1), (0, 2),
         (3, 1), (5, 0.5), (7, 0.5), (9, 1), (10, 1), (9, 0.5), (7, 0.5), (5, 1), (3, 1), (2, 1), (0, 2)]
    b = [(7, 1), (9, 0.5), (10, 0.5), (12, 1), (10, 1), (9, 1), (7, 1), (5, 1), (7, 0.5), (5, 0.5), (3, 1), (2, 2),
         (0, 1), (2, 0.5), (3, 0.5), (5, 1), (3, 1), (2, 1), (-2, 1), (0, 3)]
    harvest = song(a + a + b + b, 60 / 168, [-12, -5], lambda i: "low" if i % 3 == 0 else ("slap" if i % 3 == 2 else None))
    # Midwinter air: slow, D minor, a bell on each bar.
    c = [(0, 2), (3, 1), (5, 2), (7, 1), (8, 2), (7, 1), (5, 3), (3, 2), (2, 1), (0, 2), (-2, 1), (0, 3),
         (7, 2), (8, 1), (10, 2), (8, 1), (7, 2), (5, 1), (3, 3), (5, 2), (3, 1), (2, 2), (-2, 1), (0, 3)]
    midwinter = song(c + c, 60 / 84, [-12, -5], lambda i: "low" if i % 6 == 0 else None, bells=True)
    for name, track in (("Harvest Polska", harvest), ("Midwinter Air", midwinter)):
        track = track / np.abs(track).max() * 0.7
        wavfile.write(os.path.join(OUT, name + ".wav"), RATE, (track * 32767).astype(np.int16))
        print(name, f"{len(track) / RATE:.1f} s")


if __name__ == "__main__":
    main()
