"""Synthesises the moments that had no sound: a ward breach from warning to end, a stack dropped, a mouthful eaten.

The toll is a low bell: inharmonic partials that ring and die at their own rates.
The horn is a blown animal horn: a buzzing sawtooth swelling through a soft resonance, sliding up to its note.
The ward mending is a warm chord rising out of a low hum. The ward falling is that hum breaking and sinking.
A dropped stack is a soft thud with a little rustle. A bite is a few short wet crunches.

Run from the project root: python Tools/Audio/moments.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

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
    t = times(4.5)
    partials = [(0.56, 1.0, 2.6), (1.0, 0.8, 1.9), (1.19, 0.55, 1.5), (1.5, 0.4, 1.1), (2.0, 0.3, 0.8), (2.74, 0.18, 0.5), (3.76, 0.1, 0.3)]
    base = 196.0
    bell = sum(level * np.sin(2 * np.pi * base * ratio * t) * np.exp(-t / decay) for ratio, level, decay in partials)
    strike = band(rng.standard_normal(len(t)), 300, 1800) * np.exp(-t / 0.018) * 0.5
    return bell + strike


def horn(seconds=2.6, note=146.0):
    t = times(seconds)
    slide = note * (0.84 + 0.16 * np.clip(t / 0.22, 0, 1) ** 0.5)
    wobble = 1 + 0.006 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.5) / 0.5, 0, 1)
    phase = np.cumsum(slide * wobble) / RATE
    saw = 2 * (phase % 1) - 1
    body = band(saw, 180, 900) + 0.5 * band(saw, 1000, 2200)
    breath = band(rng.standard_normal(len(t)), 800, 3000) * 0.04
    swell = np.clip(t / 0.3, 0, 1) * np.clip((seconds - t) / 0.7, 0, 1) ** 1.5
    return (body + breath) * swell


def mended():
    t = times(3.6)
    chord = [130.8, 196.0, 261.6, 329.6, 392.0]
    total = np.zeros(len(t))
    for i, pitch in enumerate(chord):
        rise = np.clip((t - i * 0.16) / 0.4, 0, 1)
        fall = np.exp(-np.clip(t - 1.1 - i * 0.16, 0, None) / 1.0)
        tone = np.sin(2 * np.pi * pitch * t) + 0.3 * np.sin(4 * np.pi * pitch * t) + 0.3 * np.sin(2 * np.pi * pitch * 1.003 * t)
        total += tone * rise * fall
    return total


def falls():
    t = times(4.0)
    pitch = 110.0 * np.exp(-t / 2.4)
    drone = np.sin(2 * np.pi * np.cumsum(pitch) / RATE) + 0.6 * np.sin(2 * np.pi * np.cumsum(pitch * 1.5) / RATE)
    tremble = 1 + 0.5 * np.sin(2 * np.pi * (9 - t * 1.6) * t)
    crack = band(rng.standard_normal(len(t)), 90, 500) * np.exp(-t / 0.9) * 1.6
    fade = np.clip(t / 0.05, 0, 1) * np.clip((4.0 - t) / 2.2, 0, 1)
    return (drone * tremble + crack) * fade


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
    save("Ward Toll", toll(), 0.8)
    save("War Horn", horn(), 0.8)
    save("Ward Mended", mended(), 0.7)
    save("Ward Falls", falls(), 0.8)
    for i, weight in enumerate((1.0, 1.4, 0.8)):
        save(f"Item Drop {i + 1}", thud(weight), 0.75)
    for i in range(4):
        save(f"Bite {i + 1}", bite(), 0.7)


if __name__ == "__main__":
    main()
