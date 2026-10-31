"""Synthesises the moments that had no sound: a ward breach from warning to end, a stack dropped, a mouthful eaten.

The toll is a great cast bell heard across a valley. The horn is lip-reed: breath drives a buzz that grows brighter as it
grows louder, as brass and animal horn do, and there are three of them, not quite together, from different distances.
The ward mending is a held fifth in low strings and voices with a lyre across it, relieved rather than triumphant. The
ward falling is stone cracking and the same strings sinking away under it.
A dropped stack is a soft thud with a little rustle. A bite is a few short wet crunches.

Run from the project root: python Tools/Audio/moments.py
"""
import os
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

sys.path.insert(0, os.path.dirname(__file__))
import foley  # noqa: E402

RATE = 44100
OUT = "Assets/_Project/Audio/SFX/Safe Haven Moments"
rng = np.random.default_rng(31)


def band(signal, low, high, order=2):
    sos = butter(order, [low, high], btype="bandpass", fs=RATE, output="sos")
    return sosfilt(sos, signal)


def lowpass(signal, cutoff, order=2):
    sos = butter(order, cutoff, btype="lowpass", fs=RATE, output="sos")
    return sosfilt(sos, signal)


def times(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def save(name, signal, peak=0.85):
    fade = min(len(signal), int(RATE * 0.012))
    signal = signal.copy()
    signal[:fade] *= np.linspace(0, 1, fade)
    signal[-fade:] *= np.linspace(1, 0, fade)
    signal = signal / max(1e-6, np.abs(signal).max()) * peak
    wavfile.write(os.path.join(OUT, name + ".wav"), RATE, (signal * 32767).astype(np.int16))


def toll():
    random = foley.seeded("Ward Toll")
    seconds = 7.0
    tolled = foley.bell(146.83, seconds, random, hold=1.5)
    weight = foley.thump(random, seconds, 36, 80, 0.3, 0.05) * 0.3
    return foley.space(foley.darken(tolled / np.abs(tolled).max() + weight, top=-3, weight=3), foley.VALE, 0.42)


def blast(seconds, note, random):
    t = foley.times(seconds)
    swell = np.clip(t / 0.42, 0, 1) ** 0.8 * np.clip((seconds - t) / 1.1, 0, 1) ** 1.4
    slide = note * (0.84 + 0.16 * np.clip(t / 0.28, 0, 1) ** 0.5)
    tremble = foley.lowpass(foley.white(len(t), random), 9)
    tremble /= max(1e-9, np.abs(tremble).max())
    phase = np.cumsum(slide * (1 + 0.005 * tremble + 0.003 * np.sin(foley.TAU * 5.1 * t) * np.clip((t - 0.7) / 0.6, 0, 1))) / RATE
    buzz = np.tanh((0.8 + 5.5 * swell) * np.sin(foley.TAU * phase) + 0.5 * swell * np.sin(foley.TAU * 2 * phase))
    bore = foley.band(buzz, 110, 680) + 0.6 * foley.band(buzz, 680, 1500) + 0.16 * foley.band(buzz, 1500, 3200)
    breath = foley.band(foley.pink(len(t), random), 500, 2400) * 0.035
    return (bore + breath) * swell


def horn():
    random = foley.seeded("War Horn")
    seconds = 5.0
    near = blast(4.2, 98.0, random)
    second = blast(4.0, 98.4, random)
    far = blast(3.8, 146.8, random)
    mix = foley.place(foley.place(foley.place(foley.blank(seconds), near, 0), second, 0.07, 0.7), far, 0.19, 0.3)
    return foley.space(foley.darken(mix, top=-4, weight=4), foley.VALE, 0.55)


def mended():
    random = foley.seeded("Ward Mended")
    seconds = 5.0
    low, fifth, high = 73.42, 110.0, 146.83
    strings = foley.bowed(low, seconds, random, 0.5) + foley.bowed(fifth, seconds, random, 0.7) * 0.7 + foley.bowed(high, seconds, random, 0.9) * 0.4
    voices = foley.choir(low * 2, seconds, random, attack=1.4) + foley.choir(fifth * 2, seconds, random, attack=1.6) * 0.6
    strum = foley.blank(seconds)
    for i, pitch in enumerate((low, fifth, high, 220.0, 293.66)):
        foley.place(strum, foley.lyre(pitch, seconds, random), 0.5 + i * 0.06, 1 - i * 0.1)
    mix = strings / np.abs(strings).max() * 0.55 + voices / np.abs(voices).max() * 0.4 + strum / np.abs(strum).max() * 0.5
    return foley.space(foley.darken(mix, weight=2), foley.HALL, 0.4)


def falls():
    random = foley.seeded("Ward Falls")
    seconds = 5.5
    t = foley.times(seconds)
    crack = foley.strike(foley.stone(300, random, 40), seconds, 0.0008, random) * 3
    debris = foley.scatter(2.4, 110, lambda: foley.chip(random, 400, 2200), random, early=1.5)
    pitch = 73.42 * np.exp(-t / 3.4)
    phase = np.cumsum(pitch) / RATE
    saw = 2 * (phase % 1) - 1
    sinking = (foley.band(saw, 50, 420) + 0.4 * foley.band(saw, 420, 1000)) * np.clip((seconds - t) / 3.4, 0, 1) ** 1.5 * (0.7 + 0.3 * np.sin(foley.TAU * (6 - t * 0.8) * t))
    low = foley.band(foley.pink(len(t), random), 40, 240) * foley.bump(t, 0.01, 0.04, 1.1)
    mix = foley.place(crack + foley.thump(random, seconds, 32, 100, 0.4, 0.05) * 1.3 + low + sinking * 0.8, debris, 0.02, 0.5)
    return foley.space(foley.darken(foley.drive(mix, 2.0), weight=5), foley.VALE, 0.5)


def thud(weight):
    t = times(0.42)
    thump = np.sin(2 * np.pi * (90 + 60 * np.exp(-t / 0.02)) * t) * np.exp(-t / 0.06) * weight
    rustle = band(rng.standard_normal(len(t)), 900, 5000) * np.exp(-t / 0.09) * 0.32
    return thump + rustle


def bite():
    t = times(0.5)
    total = np.zeros(len(t))
    for _ in range(rng.integers(3, 6)):
        start = rng.uniform(0.0, 0.3)
        hit = band(rng.standard_normal(len(t)), rng.uniform(900, 1500), rng.uniform(3000, 5200))
        total += hit * np.exp(-np.clip(t - start, 0, None) / rng.uniform(0.012, 0.03)) * (t >= start) * rng.uniform(0.5, 1.0)
    return total + lowpass(rng.standard_normal(len(t)), 500) * 0.25 * np.exp(-t / 0.2)


def main():
    os.makedirs(OUT, exist_ok=True)
    foley.save(OUT, "Ward Toll", toll(), 0.85)
    foley.save(OUT, "War Horn", horn(), 0.85)
    foley.save(OUT, "Ward Mended", mended(), 0.8)
    foley.save(OUT, "Ward Falls", falls(), 0.85)
    for i, weight in enumerate((1.0, 1.4, 0.8)):
        save(f"Item Drop {i + 1}", thud(weight), 0.75)
    for i in range(4):
        save(f"Bite {i + 1}", bite(), 0.7)


if __name__ == "__main__":
    main()
