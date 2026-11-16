"""Synthesises the sounds of a lived-in world: an owl at night, the Nokk's song over still water, a deer's alarm
bark, an offering taken, and the hush of something seen and gone. (Ridge wind was removed on 2026-11-16: it whistled.)

An owl is two soft hoots, a low sine with a breathy edge, the second held longer. The Nokk plays a slow fiddle phrase: a bowed sawtooth through body
resonances, a little out of tune and slurred, with a long tail. A deer barks: a short, hoarse burst falling in pitch.
The offering is a soft wooden knock and a rising airy shimmer. A glimpse leaving is a breath of filtered noise with a
faint, falling whisper.

Run from the project root: python Tools/Audio/living.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = "Assets/_Project/Audio/SFX/Safe Haven Living World"
rng = np.random.default_rng(41)


def band(signal, low, high, order=2):
    sos = butter(order, [low, high], btype="bandpass", fs=RATE, output="sos")
    return sosfilt(sos, signal)


def lowpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="lowpass", fs=RATE, output="sos"), signal)


def envelope(length, attack, release):
    env = np.ones(length)
    a, r = max(1, int(RATE * attack)), max(1, int(RATE * release))
    env[:a] = np.linspace(0, 1, a) ** 2
    env[-r:] *= np.linspace(1, 0, r) ** 1.5
    return env


def save(name, signal, peak=0.8):
    signal = signal / max(1e-6, np.abs(signal).max()) * peak
    wavfile.write(os.path.join(OUT, name + ".wav"), RATE, (signal * 32767).astype(np.int16))


def pink(length):
    white = rng.standard_normal(length)
    spectrum = np.fft.rfft(white)
    spectrum /= np.sqrt(np.arange(1, len(spectrum) + 1))
    return np.fft.irfft(spectrum, length)


def hoot(seconds, pitch):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    wobble = 1 + 0.01 * np.sin(2 * np.pi * 5 * t)
    voice = np.sin(2 * np.pi * pitch * wobble * t) + 0.25 * np.sin(4 * np.pi * pitch * t)
    breath = band(rng.standard_normal(length), pitch * 0.8, pitch * 3) * 0.12
    return (voice + breath) * envelope(length, 0.05, seconds * 0.6)


def owl(variant):
    pitch = 380 + variant * 18
    gap = np.zeros(int(RATE * (0.35 + 0.05 * variant)))
    call = np.concatenate([hoot(0.28, pitch), gap, hoot(0.75, pitch * 0.97), np.zeros(int(RATE * 0.4))])
    return lowpass(call, 1800)


# Ridge wind was removed on 2026-11-16: its sliding tone sounded like a wavy whistle (the user: "remove it completely
# from the game").
def bowed(freq, seconds, slide=0.0):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    f = freq * (1 + slide * np.clip(t / 0.25, 0, 1)) * (1 + 0.006 * np.sin(2 * np.pi * 5.2 * t))
    phase = 2 * np.pi * np.cumsum(f) / RATE
    saw = sum(np.sin(k * phase) / k for k in range(1, 9))
    body = band(saw, 300, 1200) * 0.7 + band(saw, 2000, 3500) * 0.25 + band(saw, 150, 400) * 0.3
    rosin = band(rng.standard_normal(length), 2500, 7000) * 0.03
    return (body + rosin) * envelope(length, 0.18, 0.35)


def nokk(variant):
    # A slow minor phrase, a little sharp and slurred, as a tune played across water.
    root = 293.7 * (1 + 0.012 * variant)
    steps = [[0, 3, 7, 5, 3], [0, 2, 3, -2, 0], [7, 5, 3, 2, 0]][variant % 3]
    notes = [bowed(root * 2 ** (s / 12), 0.9 if i < len(steps) - 1 else 1.8, 0.004 * (i % 2)) for i, s in enumerate(steps)]
    phrase = np.concatenate(notes)
    # The lake's echo: a few soft, darkened repeats.
    tail = np.zeros(len(phrase) + int(RATE * 2.5))
    tail[:len(phrase)] += phrase
    for delay, gain in ((0.37, 0.35), (0.81, 0.22), (1.4, 0.12)):
        d = int(RATE * delay)
        tail[d:d + len(phrase)] += lowpass(phrase, 2500) * gain
    return tail


def deer_bark(variant):
    seconds = 0.32
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    pitch = (720 - 380 * t / seconds) * (1 + 0.05 * variant)
    phase = 2 * np.pi * np.cumsum(pitch) / RATE
    voice = np.sign(np.sin(phase)) * 0.3 + np.sin(phase)
    rough = band(rng.standard_normal(length), 600, 3200) * 0.6
    bark = (band(voice, 400, 2600) + rough) * envelope(length, 0.008, 0.18)
    return np.concatenate([bark, np.zeros(int(RATE * 0.25))])


def offering(variant):
    knock_len = int(RATE * 0.12)
    t = np.arange(knock_len) / RATE
    knock = np.sin(2 * np.pi * 210 * t) * np.exp(-t * 40) + band(rng.standard_normal(knock_len), 400, 1800) * np.exp(-t * 60) * 0.4
    seconds = 1.6
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    air = band(rng.standard_normal(length), 1800, 5000 + 2000 * variant / 3) * 0.5
    bells = sum(np.sin(2 * np.pi * f * t) * np.exp(-t * (2.2 + k)) for k, f in enumerate([880, 1318.5, 1760 * (1 + 0.01 * variant)]))
    shimmer = (air * np.sin(np.pi * t / seconds) ** 2 + bells * 0.18) * envelope(length, 0.2, 0.6)
    out = np.zeros(int(RATE * 0.15) + length)
    out[:knock_len] += knock
    out[int(RATE * 0.15):] += shimmer
    return out


def glimpse_gone(variant):
    seconds = 1.4
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    breath = band(rng.standard_normal(length), 300, 2500) * np.sin(np.pi * t / seconds) ** 2
    whisper = band(rng.standard_normal(length), 2000, 6000) * (0.5 + 0.5 * np.sin(2 * np.pi * (7 + variant) * t)) * np.exp(-t * 2.2) * 0.35
    tone = np.sin(2 * np.pi * (520 - 260 * t / seconds) * t) * np.exp(-t * 2.5) * 0.08
    return (breath + whisper + tone) * envelope(length, 0.15, 0.6)


def main():
    os.makedirs(OUT, exist_ok=True)
    for i in range(3):
        save(f"Owl Hoot {i + 1}", owl(i), 0.6)
        save(f"Nokk Song {i + 1}", nokk(i), 0.6)
        save(f"Deer Alarm {i + 1}", deer_bark(i), 0.7)
        save(f"Offering Taken {i + 1}", offering(i), 0.55)
        save(f"Glimpse Gone {i + 1}", glimpse_gone(i), 0.45)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
