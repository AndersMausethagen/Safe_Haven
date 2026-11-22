"""Synthesises the cauldron brewing (the user, 2026-11-22: "have it create smoke and sound while brewing"): a seamless loop of a pot
on the boil over a small fire, heard while the Cauldron works (StationWorkEffects).

Three layers: a low, rolling boil (pink noise under 220 Hz, slowly swelling), bubbles breaking at the surface (each a short sine
whose pitch rises as the bubble's neck closes, as real bubbles do, in loose clusters), and the fire under it (a soft breathy bed and
sparse crackles). Eight seconds, the tail crossfaded onto the head so it loops without a seam.

Run from the project root: python Tools/Audio/brewing.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = "Assets/_Project/Audio/SFX/Safe Haven Work"
LOOP = 8.0
TAIL = 1.0
rng = np.random.default_rng(822)


def lowpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="lowpass", fs=RATE, output="sos"), signal)


def highpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="highpass", fs=RATE, output="sos"), signal)


def band(signal, low, high, order=2):
    return sosfilt(butter(order, [low, high], btype="bandpass", fs=RATE, output="sos"), signal)


def pink(length):
    spectrum = np.fft.rfft(rng.standard_normal(length))
    spectrum /= np.sqrt(np.arange(1, len(spectrum) + 1))
    return np.fft.irfft(spectrum, length)


def bubble(radius_hz, length_s, rise):
    """One bubble: a sine starting at its resonance and rising as it closes, with a quick decay (Minnaert-style)."""
    n = int(RATE * length_s)
    t = np.arange(n) / RATE
    freq = radius_hz * (1.0 + rise * t / length_s)
    phase = 2 * np.pi * np.cumsum(freq) / RATE
    decay = np.exp(-t / (length_s * 0.35))
    attack = np.minimum(1.0, t / 0.002)
    return np.sin(phase) * decay * attack


def main():
    total = int(RATE * (LOOP + TAIL))
    mix = np.zeros(total)

    # The boil: low rolling noise, swelling slowly.
    boil = lowpass(pink(total), 220, 3)
    swell = 0.75 + 0.25 * np.sin(2 * np.pi * np.arange(total) / RATE / 2.7) * np.sin(2 * np.pi * np.arange(total) / RATE / 1.3 + 1.0)
    mix += boil / np.abs(boil).max() * 0.35 * swell

    # Bubbles, in loose clusters: about seven a second.
    t = 0.0
    while t < LOOP + TAIL - 0.2:
        cluster = rng.integers(1, 4)
        for _ in range(cluster):
            start = int(RATE * (t + rng.uniform(0, 0.08)))
            radius = rng.uniform(260, 900)
            pop = bubble(radius, rng.uniform(0.03, 0.09), rng.uniform(0.4, 1.4)) * rng.uniform(0.15, 0.45)
            end = min(total, start + len(pop))
            mix[start:end] += pop[: end - start]
        t += rng.exponential(0.42)

    # The fire under it: a soft breathy bed and sparse crackles.
    bed = band(rng.standard_normal(total), 250, 1600)
    mix += bed / np.abs(bed).max() * 0.06
    crackles = np.zeros(total)
    for _ in range(int((LOOP + TAIL) * 5)):
        at = rng.integers(0, total - 400)
        length = rng.integers(40, 260)
        crackles[at:at + length] += rng.standard_normal(length) * np.exp(-np.arange(length) / (length * 0.3)) * rng.uniform(0.2, 0.7)
    mix += highpass(crackles, 1800) * 0.35

    # Seamless: the tail fades in over the head.
    loop_n, tail_n = int(RATE * LOOP), int(RATE * TAIL)
    fade = np.linspace(0.0, 1.0, tail_n)
    looped = mix[:loop_n].copy()
    looped[:tail_n] = looped[:tail_n] * fade + mix[loop_n:loop_n + tail_n] * (1.0 - fade)
    looped = looped / max(1e-6, np.abs(looped).max()) * 0.8
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, "cauldron_brew_loop_v1_python.wav")
    wavfile.write(path, RATE, (looped * 32767).astype(np.int16))
    print(path)


if __name__ == "__main__":
    main()
