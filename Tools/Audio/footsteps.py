"""Synthesises soft footsteps for Safe Haven: grass, forest floor and packed dirt, eight variants each.

A step is a muffled thud (the foot's weight) under filtered noise shaped like a heel then a toe pressing down: grass
is a soft high swish of blades, the forest floor a lower rustle of needles and leaves with the odd tiny crackle, dirt
a short low scuff. Everything is kept quiet and rounded (no sharp transients), as the game wants them barely heard.
"""
import os
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
rng = np.random.default_rng(7)


def band(signal, low, high, order=3):
    sos = butter(order, [low, high], btype="bandpass", fs=RATE, output="sos")
    return sosfilt(sos, signal)


def lowpass(signal, cutoff, order=2):
    sos = butter(order, cutoff, btype="lowpass", fs=RATE, output="sos")
    return sosfilt(sos, signal)


def bump(t, start, attack, decay):
    """A soft envelope: smooth rise over attack, exponential fall."""
    x = t - start
    rise = np.clip(x / attack, 0, 1)
    rise = rise * rise * (3 - 2 * rise)
    fall = np.exp(-np.clip(x - attack, 0, None) / decay)
    return np.where(x < 0, 0, rise * fall)


def step(kind):
    length = {"grass": 0.42, "forest": 0.5, "dirt": 0.32}[kind]
    n = int(RATE * length)
    t = np.arange(n) / RATE
    heel = rng.uniform(0.012, 0.03)
    toe = heel + rng.uniform(0.06, 0.11)
    toe_level = rng.uniform(0.45, 0.75)
    noise = rng.standard_normal(n)
    # Slow random flutter so the swish isn't a flat hiss: blades and leaves brushing past one another.
    flutter = 0.55 + 0.45 * np.abs(lowpass(rng.standard_normal(n), rng.uniform(25, 60)) * 18)
    flutter = np.clip(flutter, 0.2, 1.4)

    if kind == "grass":
        texture = band(noise, rng.uniform(1100, 1600), rng.uniform(4200, 5200)) * flutter
        env = bump(t, heel, 0.018, 0.07) + toe_level * bump(t, toe, 0.025, 0.09)
        thud_freq, thud_level, thud_decay = rng.uniform(70, 95), 0.35, 0.035
        body = texture * env
    elif kind == "forest":
        texture = band(noise, rng.uniform(500, 800), rng.uniform(3000, 3800)) * flutter
        env = bump(t, heel, 0.02, 0.09) + toe_level * bump(t, toe, 0.03, 0.12)
        body = texture * env
        # A few tiny crackles of needles and twigs, softened so they never click.
        for _ in range(rng.integers(1, 4)):
            at = rng.uniform(heel, toe + 0.08)
            grain = band(rng.standard_normal(n), 1800, 4500) * bump(t, at, 0.002, rng.uniform(0.004, 0.009))
            body += grain * rng.uniform(0.25, 0.5)
        thud_freq, thud_level, thud_decay = rng.uniform(60, 85), 0.45, 0.04
    else:  # dirt
        texture = band(noise, rng.uniform(250, 400), rng.uniform(1800, 2600)) * flutter
        env = bump(t, heel, 0.012, 0.045) + 0.6 * toe_level * bump(t, toe, 0.015, 0.05)
        body = texture * env
        thud_freq, thud_level, thud_decay = rng.uniform(65, 90), 0.6, 0.03

    thud = np.sin(2 * np.pi * thud_freq * t) * bump(t, heel, 0.008, thud_decay)
    sound = body / (np.max(np.abs(body)) + 1e-9) + thud * thud_level
    # Round off anything harsh, then a gentle fade so the tail never cuts.
    sound = lowpass(sound, {"grass": 5500, "forest": 4500, "dirt": 3500}[kind])
    fade = np.ones(n)
    tail = int(0.08 * RATE)
    fade[-tail:] = np.linspace(1, 0, tail) ** 2
    sound *= fade
    # Quiet on purpose: about -16 dBFS peak. The game turns them down further.
    sound = sound / (np.max(np.abs(sound)) + 1e-9) * 10 ** (-16 / 20)
    return (sound * 32767).astype(np.int16)


def main(folder):
    os.makedirs(folder, exist_ok=True)
    for kind in ("grass", "forest", "dirt"):
        for i in range(8):
            wavfile.write(os.path.join(folder, f"Footstep {kind.title()} {i + 1}.wav"), RATE, step(kind))
    print("ok", len(os.listdir(folder)))


if __name__ == "__main__":
    main(sys.argv[1])
