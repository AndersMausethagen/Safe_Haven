"""Synthesises the sounds of Safe Haven's carts and boats: a rolling wheel, wooden creaks, oar strokes and water along a hull.

The rolling wheel is a low rumble of the ground under an iron-shod rim, with a soft knock each turn where the wheel isn't
quite true. It has to loop, so the knocks fall on a whole number of turns and the noise crossfades into its own start.
A creak is what a stick-slip joint does: a run of tiny resonant pulses, their rate wandering, through a narrow wooden band.
An oar stroke is a blade going in, a swish as it pulls and a few drips as it lifts.

Run from the project root: python Tools/Audio/transport.py
"""
import os
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, sosfilt

RATE = 44100
OUT = "Assets/_Project/Audio/SFX/Safe Haven Transport"
rng = np.random.default_rng(11)


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


def seamless(signal, fade):
    """Folds the tail of a signal over its start with an equal-power crossfade, so it loops without a click."""
    body = signal[:-fade].copy()
    t = np.linspace(0, np.pi / 2, fade)
    body[:fade] = body[:fade] * np.sin(t) + signal[-fade:] * np.cos(t)
    return body


def roll_loop():
    seconds = 4.0
    fade = int(RATE * 0.4)
    n = int(RATE * seconds) + fade
    t = np.arange(n) / RATE
    noise = rng.standard_normal(n)
    # Slow unevenness of the ground, so it doesn't sound like a machine.
    ground = 0.65 + 0.35 * np.clip(lowpass(rng.standard_normal(n), 4) * 60, -1, 1)
    rumble = lowpass(noise, 160, 3) * 6 * ground
    grit = band(rng.standard_normal(n), 500, 2400) * 0.1 * ground
    knocks = np.zeros(n)
    turns = 8  # a whole number in the loop, so the last knock leads into the first
    for i in range(turns + 2):
        at = i * seconds / turns + rng.uniform(-0.012, 0.012)
        knocks += np.sin(2 * np.pi * rng.uniform(95, 125) * t) * bump(t, at, 0.004, 0.05) * rng.uniform(0.55, 0.95)
    return seamless(rumble + grit + knocks * 0.6, fade), 0.5


def water_loop():
    seconds = 5.0
    fade = int(RATE * 0.8)
    n = int(RATE * seconds) + fade
    wash = band(rng.standard_normal(n), 240, 1500)
    swell = 0.5 + 0.5 * np.clip(lowpass(rng.standard_normal(n), 1.4) * 140, -1, 1)
    trickle = band(rng.standard_normal(n), 1600, 4200) * np.clip(lowpass(rng.standard_normal(n), 9) * 45, 0, 1) * 0.3
    return seamless(wash * swell + trickle, fade), 0.5


def creak():
    length = rng.uniform(0.4, 0.75)
    n = int(RATE * length)
    t = np.arange(n) / RATE
    pulses = np.zeros(n)
    at = 0.02
    start = rng.uniform(55, 95)
    end = start * rng.uniform(1.3, 2.2)
    while at < length - 0.04:
        rate = start + (end - start) * (at / length)
        index = int(at * RATE)
        pulses[index] = rng.uniform(0.5, 1.0)
        at += (1 / rate) * rng.uniform(0.85, 1.15)
    voice = band(pulses, rng.uniform(420, 620), rng.uniform(1300, 1900), 2)
    body = band(pulses, rng.uniform(190, 260), rng.uniform(380, 470), 2) * 0.6
    envelope = bump(t, 0.0, length * 0.3, length * 0.32)
    return (voice + body) * envelope, 0.7


def oar():
    length = 0.95
    n = int(RATE * length)
    t = np.arange(n) / RATE
    plunge = band(rng.standard_normal(n), 180, 900) * bump(t, 0.02, 0.012, 0.06)
    swish = band(rng.standard_normal(n), rng.uniform(380, 520), rng.uniform(1900, 2500)) * bump(t, 0.1, 0.16, 0.2)
    sound = plunge * 0.9 + swish * 0.65
    for _ in range(rng.integers(3, 6)):
        at = rng.uniform(0.5, 0.82)
        freq = rng.uniform(900, 1900)
        sound += np.sin(2 * np.pi * (freq * t + 220 * (t - at))) * bump(t, at, 0.002, 0.018) * rng.uniform(0.08, 0.2)
    return sound, 0.75


def write(name, signal, peak):
    signal = signal - signal.mean()
    signal = signal / np.max(np.abs(signal)) * peak
    wavfile.write(os.path.join(OUT, name + ".wav"), RATE, (signal * 32767).astype(np.int16))


def main():
    if not os.path.isdir("Assets"):
        sys.exit("Run this from the project root.")

    os.makedirs(OUT, exist_ok=True)
    write("Cart Roll Loop", *roll_loop())
    write("Hull Water Loop", *water_loop())
    for i in range(1, 5):
        write(f"Cart Creak {i}", *creak())
    for i in range(1, 5):
        write(f"Oar Stroke {i}", *oar())


if __name__ == "__main__":
    main()
