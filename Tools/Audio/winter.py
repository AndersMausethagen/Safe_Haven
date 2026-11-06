"""Synthesises winter's sounds: steps crunching in snow, steps on ice, ice creaking under weight and cracking as it
sets or goes.

A snow step is a quick burst of tiny grains breaking: hundreds of short clicks through a bright band, packed into the
first tenth of a second and thinning after, over a soft low thud. An ice step is a hard, short knock with a faint
glassy ring. A creak is the low groan of a sheet flexing: a slowly bent tone with a rough edge, rising and falling.
A crack is a sharp snap followed by the long, falling "pew" of a sound running through a frozen lake (a dispersive
chirp: high notes arrive first).

Run from the project root: python Tools/Audio/winter.py
"""
import os

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = "Assets/_Project/Audio/SFX/Safe Haven Winter"
rng = np.random.default_rng(73)


def band(signal, low, high, order=2):
    sos = butter(order, [low, high], btype="bandpass", fs=RATE, output="sos")
    return sosfilt(sos, signal)


def lowpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="lowpass", fs=RATE, output="sos"), signal)


def save(name, signal, peak=0.8):
    signal = signal / max(1e-6, np.abs(signal).max()) * peak
    fade = min(len(signal), int(RATE * 0.01))
    signal[-fade:] *= np.linspace(1, 0, fade)
    wavfile.write(os.path.join(OUT, name + ".wav"), RATE, (signal * 32767).astype(np.int16))


def snow_step(seconds=0.32):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    grains = np.zeros(length)
    # Grains break thickest just after the foot lands, then fewer as it settles.
    count = rng.integers(260, 380)
    when = (rng.beta(1.3, 4.0, count) * seconds * 0.85 * RATE).astype(int)
    for at in when:
        size = rng.integers(8, 40)
        if at + size >= length:
            continue
        grains[at:at + size] += rng.standard_normal(size) * np.exp(-np.arange(size) / (size * 0.3)) * rng.uniform(0.3, 1.0)
    crunch = band(grains, 900, 6500) + band(grains, 300, 1200) * 0.5
    thud = lowpass(rng.standard_normal(length), 180) * np.exp(-t / 0.04) * 2.5
    return crunch + thud


def ice_step(seconds=0.35):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    knock = band(rng.standard_normal(length), 600, 3500) * np.exp(-t / 0.012)
    ring = sum(np.sin(2 * np.pi * f * t) * np.exp(-t / d) * a
               for f, d, a in ((rng.uniform(1700, 2100), 0.09, 0.08), (rng.uniform(3100, 3600), 0.05, 0.05)))
    thud = lowpass(rng.standard_normal(length), 200) * np.exp(-t / 0.03) * 1.5
    return knock + ring + thud


def creak(seconds):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    pitch = 70 + 35 * np.sin(np.pi * t / seconds) + 6 * np.sin(2 * np.pi * 3.1 * t)
    phase = 2 * np.pi * np.cumsum(pitch) / RATE
    tone = np.sign(np.sin(phase)) * 0.4 + np.sin(phase * 2.01) * 0.3
    rough = 1 + 0.6 * np.maximum(0, np.sin(2 * np.pi * 23 * t + rng.uniform(0, 6)))
    swell = np.sin(np.pi * t / seconds) ** 1.5
    return band(tone * rough, 90, 1400) * swell


def crack(seconds=2.6):
    length = int(RATE * seconds)
    t = np.arange(length) / RATE
    snap = band(rng.standard_normal(length), 800, 9000) * np.exp(-t / 0.008) * 1.6
    # The "pew": a chirp falling from high to low, the high part arriving first, with a long tail.
    pitch = 3200 * np.exp(-t / 0.35) + 180
    phase = 2 * np.pi * np.cumsum(pitch) / RATE
    pew = np.sin(phase) * np.exp(-t / 0.9) * 0.45
    echo = np.zeros(length)
    delay = int(RATE * 0.23)
    echo[delay:] = pew[:-delay] * 0.35
    rumble = lowpass(rng.standard_normal(length), 120) * np.exp(-t / 0.6) * 0.6
    return snap + pew + echo + rumble


def main():
    os.makedirs(OUT, exist_ok=True)
    for i in range(1, 6):
        save(f"Snow Step {i}", snow_step(rng.uniform(0.26, 0.36)), 0.7)
    for i in range(1, 5):
        save(f"Ice Step {i}", ice_step(), 0.7)
    for i in range(1, 4):
        save(f"Ice Creak {i}", creak(rng.uniform(1.0, 1.8)), 0.6)
    for i in range(1, 3):
        save(f"Ice Crack {i}", crack(rng.uniform(2.2, 3.0)), 0.8)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
