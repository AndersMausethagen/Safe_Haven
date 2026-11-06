"""Synthesises the calls of geese going over: a skein's honking, far off and high.

A honk is a short, nasal tone (a pulse wave rich in harmonics) dipping in pitch, through a few vocal-tract formants,
with a breathy edge. Each call is two to four honks from different birds, a little apart in pitch and time, softened
and with a touch of distance (high frequencies rolled off, a faint echo).

Run from the project root: python Tools/Audio/seasons.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = "Assets/_Project/Audio/SFX/Safe Haven Living World"
rng = np.random.default_rng(29)


def band(signal, low, high, order=2):
    return sosfilt(butter(order, [low, high], btype="bandpass", fs=RATE, output="sos"), signal)


def lowpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="lowpass", fs=RATE, output="sos"), signal)


def honk(pitch, seconds):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    bend = pitch * (1 + 0.12 * np.exp(-t / 0.05) - 0.08 * t / seconds)
    phase = np.cumsum(bend) / RATE
    pulse = np.where((phase % 1) < 0.28, 1.0, -0.35)
    tone = band(pulse, 450, 1100) * 1.2 + band(pulse, 1500, 2600) * 0.7 + band(pulse, 2800, 3600) * 0.3
    breath = band(rng.standard_normal(length), 800, 4000) * 0.08
    env = np.minimum(1, t / 0.015) * np.clip((seconds - t) / 0.06, 0, 1)
    return (tone + breath) * env


def call():
    seconds = rng.uniform(1.0, 1.6)
    out = np.zeros(int(RATE * seconds))
    for _ in range(rng.integers(2, 5)):
        h = honk(rng.uniform(300, 420), rng.uniform(0.16, 0.26))
        at = int(rng.uniform(0, seconds - 0.3) * RATE)
        out[at:at + len(h)] += h * rng.uniform(0.6, 1.0)
    echo = np.zeros_like(out)
    delay = int(RATE * 0.18)
    echo[delay:] = out[:-delay] * 0.25
    return lowpass(out + echo, 3200)


def main():
    os.makedirs(OUT, exist_ok=True)
    for i in range(1, 4):
        signal = call()
        signal = signal / np.abs(signal).max() * 0.7
        wavfile.write(os.path.join(OUT, f"Geese Call {i}.wav"), RATE, (signal * 32767).astype(np.int16))
    print("wrote geese calls to", OUT)


if __name__ == "__main__":
    main()
