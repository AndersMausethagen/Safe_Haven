"""Synthesises Safe Haven's birds: wings going up, crows calling, small birds singing.

Wings are bursts of airy noise, one soft thump to each beat, slowing and fading as the flock gets away.
A caw is a harsh, buzzing voice: a sawtooth falling in pitch, roughened by a fast tremble, through two narrow mouth
resonances. A chirp is a clean whistle that swoops, a few notes to a phrase.

Run from the project root: python Tools/Audio/birds.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = "Assets/_Project/Audio/SFX/Safe Haven Birds"
rng = np.random.default_rng(23)


def band(signal, low, high, order=2):
    sos = butter(order, [low, high], btype="bandpass", fs=RATE, output="sos")
    return sosfilt(sos, signal)


def save(name, signal, peak=0.85):
    fade = min(len(signal), int(RATE * 0.01))
    signal = signal.copy()
    signal[:fade] *= np.linspace(0, 1, fade)
    signal[-fade:] *= np.linspace(1, 0, fade)
    signal = signal / max(1e-6, np.abs(signal).max()) * peak
    wavfile.write(os.path.join(OUT, name + ".wav"), RATE, (signal * 32767).astype(np.int16))


def flutter(seconds, beats_per_second, birds):
    """Several sets of wings at slightly different rates, starting one after another."""
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    total = np.zeros(length)
    for bird in range(birds):
        start = rng.uniform(0, 0.18)
        rate = beats_per_second * rng.uniform(0.85, 1.15)
        # The beat slows a little as it climbs away.
        phase = 2 * np.pi * rate * (t - start) * (1 - 0.12 * np.clip(t / seconds, 0, 1))
        beat = np.clip(np.sin(phase), 0, 1) ** 2
        fade = np.exp(-np.clip(t - start, 0, None) / (seconds * 0.38)) * (t >= start)
        air = band(rng.standard_normal(length), 500, 3200) * beat * fade
        thump = band(rng.standard_normal(length), 90, 320) * beat * fade * 1.2
        total += air + thump
    return total


def caw(duration, start_pitch, end_pitch):
    length = int(RATE * duration)
    t = np.arange(length) / RATE
    pitch = np.linspace(start_pitch, end_pitch, length) * (1 + 0.02 * np.sin(2 * np.pi * 7 * t))
    phase = np.cumsum(pitch) / RATE
    saw = 2 * (phase % 1) - 1
    rough = 1 + 0.55 * np.sign(np.sin(2 * np.pi * 63 * t))
    voice = saw * rough
    mouth = band(voice, 780, 1250) * 1.0 + band(voice, 1700, 2500) * 0.7 + band(voice, 300, 600) * 0.3
    rise = np.clip(t / 0.025, 0, 1)
    fall = np.clip((duration - t) / 0.07, 0, 1)
    return mouth * rise * fall


def caws(count):
    pieces = []
    for _ in range(count):
        pieces.append(caw(rng.uniform(0.26, 0.4), rng.uniform(430, 500), rng.uniform(300, 350)))
        pieces.append(np.zeros(int(RATE * rng.uniform(0.09, 0.2))))
    return np.concatenate(pieces + [np.zeros(int(RATE * 0.05))])


def chirp(notes, low, high):
    pieces = []
    for _ in range(notes):
        duration = rng.uniform(0.05, 0.13)
        length = int(RATE * duration)
        t = np.arange(length) / RATE
        a, b = rng.uniform(low, high, 2)
        pitch = np.linspace(a, b, length) * (1 + 0.03 * np.sin(2 * np.pi * 40 * t))
        tone = np.sin(2 * np.pi * np.cumsum(pitch) / RATE) + 0.25 * np.sin(4 * np.pi * np.cumsum(pitch) / RATE)
        pieces.append(tone * np.sin(np.pi * t / duration) ** 2)
        pieces.append(np.zeros(int(RATE * rng.uniform(0.02, 0.09))))
    return np.concatenate(pieces + [np.zeros(int(RATE * 0.03))])


def main():
    os.makedirs(OUT, exist_ok=True)
    for i in range(3):
        save(f"Small Wings {i + 1}", flutter(0.9, 15, 5), 0.8)
        save(f"Crow Wings {i + 1}", flutter(1.2, 7, 4), 0.85)
    for i, count in enumerate((1, 2, 3, 2)):
        save(f"Crow Caw {i + 1}", caws(count), 0.8)
    for i, (notes, low, high) in enumerate(((3, 3200, 5200), (5, 3800, 6200), (2, 2600, 4200), (6, 4200, 6800))):
        save(f"Bird Chirp {i + 1}", chirp(notes, low, high), 0.7)


if __name__ == "__main__":
    main()
