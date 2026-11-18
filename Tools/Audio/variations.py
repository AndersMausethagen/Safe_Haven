"""
More takes of combat sounds that have too few (Docs/LookAndFeelResearch_2026-11-18.md A6: every combat event at least three
variations, so a repeated blow never sounds like a loop). A new take is made from the ones a sound already has, so it keeps its
character: the attack of one take spliced onto the body and tail of another (crossfaded over a few milliseconds where both are
loud), a resample of a few percent (pitch and length together), a gentle tilt of the tone (warmer or brighter by a couple of dB) and
a little drive, then matched in loudness to its sources. No tones are added (no whistles). From one take alone: two resampled,
tilted takes with their tails shaped differently.

Run from the project root:  python Tools/Audio/variations.py
It writes "<name> 3.wav" (and "2.wav" for one-take sounds) beside the sources, and prints what it made; the Unity side
(adding the clips to the SoundDefinitions) is done in the Editor.
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy.signal import butter, resample_poly, sosfilt

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SFX = os.path.join(ROOT, "Assets", "_Project", "Audio", "SFX")

# sound: (folder, source takes, new takes), each new take (how it is made, resample, tilt dB, drive, seed).
WANTED = {
    "Backstab Hit": ("Mixkit Blades", ["Backstab Hit 1.wav", "Backstab Hit 2.wav"],
                     [("Backstab Hit 3.wav", "splice", 0.97, -1.5, 0.15, 3)]),
    "Gore Splash": ("Mixkit Blades", ["Gore Splash.wav"],
                    [("Gore Splash 2.wav", "single", 0.95, -2.0, 0.1, 4), ("Gore Splash 3.wav", "single", 1.04, 1.5, 0.1, 5)]),
    "Perfect Dodge": ("Safe Haven Combat", ["Perfect Dodge 1.wav", "Perfect Dodge 2.wav"],
                      [("Perfect Dodge 3.wav", "splice", 1.03, 1.0, 0.0, 6)]),
    "Ice Crack": ("Safe Haven Winter", ["Ice Crack 1.wav", "Ice Crack 2.wav"],
                  [("Ice Crack 3.wav", "splice", 0.96, -1.0, 0.1, 7)]),
    "Impact Quake": (os.path.join("Safe Haven Impacts", "Remakes"), ["impact_quake_v1_r_moss.wav", "impact_quake_v2_r_moss.wav"],
                     [("impact_quake_v3_r_moss.wav", "splice", 0.97, -1.5, 0.12, 8)]),
    "Shaman Call": ("Safe Haven Creatures", ["Shaman Call 1.wav", "Shaman Call 2.wav"],
                    [("Shaman Call 3.wav", "splice", 0.97, -1.0, 0.05, 9)]),
    "Huldra Call": ("Safe Haven Creatures", ["Huldra Call 1.wav", "Huldra Call 2.wav"],
                    [("Huldra Call 3.wav", "splice", 1.03, 0.5, 0.0, 10)]),
    "Light Splits": ("Safe Haven Creatures", ["Light Splits 1.wav", "Light Splits 2.wav"],
                     [("Light Splits 3.wav", "splice", 0.97, -1.0, 0.0, 11)]),
    "Wing Alarm": ("Safe Haven Creatures", ["Wing Alarm 1.wav", "Wing Alarm 2.wav"],
                   [("Wing Alarm 3.wav", "splice", 1.03, 0.5, 0.0, 12)]),
    "Stone Skin": ("Safe Haven Creatures", ["Stone Skin 1.wav", "Stone Skin 2.wav"],
                   [("Stone Skin 3.wav", "splice", 0.97, -1.5, 0.1, 13)]),
    "Root Regrowth": ("Safe Haven Creatures", ["Root Regrowth 1.wav", "Root Regrowth 2.wav"],
                      [("Root Regrowth 3.wav", "splice", 0.96, -1.0, 0.05, 14)]),
}


def load(path):
    data, rate = sf.read(path, always_2d=True, dtype="float64")
    return data, rate


def loudness(x):
    """RMS over the loud part (above a tenth of the peak), so silence before and after doesn't count."""
    mono = np.abs(x).mean(axis=1)
    peak = mono.max() + 1e-9
    loud = mono[mono > 0.1 * peak]
    return float(np.sqrt(np.mean(loud ** 2))) if loud.size else 1e-9


def resample(x, factor):
    """Pitch and length together: factor below 1 lowers and lengthens."""
    up, down = int(round(1000 / factor)), 1000
    return resample_poly(x, up, down, axis=0)


def tilt(x, rate, db):
    """A gentle shelf: above 0 brighter (highs up, lows down), below warmer, pivoting near 1 kHz."""
    if abs(db) < 0.05:
        return x
    gain = 10 ** (abs(db) / 20) - 1
    sos = butter(1, 1000, "highpass" if db > 0 else "lowpass", fs=rate, output="sos")
    shelf = sosfilt(sos, x, axis=0)
    return (x + shelf * gain) / (1 + gain * 0.5)


def drive(x, amount):
    if amount <= 0:
        return x
    k = 1 + amount * 4
    return np.tanh(x * k) / np.tanh(k)


def onset(x):
    mono = np.abs(x).mean(axis=1)
    peak = mono.max() + 1e-9
    return int(np.argmax(mono > 0.25 * peak))


def splice(a, b, rate, rng):
    """The attack of a onto the body and tail of b, aligned at their onsets, crossfaded 4-12 ms after the attack's peak."""
    oa, ob = onset(a), onset(b)
    lead = max(oa, ob)
    a = np.pad(a, ((lead - oa, 0), (0, 0)))
    b = np.pad(b, ((lead - ob, 0), (0, 0)))
    n = max(len(a), len(b))
    a = np.pad(a, ((0, n - len(a)), (0, 0)))
    b = np.pad(b, ((0, n - len(b)), (0, 0)))
    cut = lead + int(rate * rng.uniform(0.012, 0.03))
    fade = int(rate * rng.uniform(0.004, 0.012))
    w = np.ones(n)
    w[cut:cut + fade] = np.linspace(1, 0, min(fade, n - cut))
    w[cut + fade:] = 0
    return a * w[:, None] + b * (1 - w)[:, None]


def shape_tail(x, rate, rng, longer):
    """A different decay: the tail after the loudest point eased shorter or let ring a little longer."""
    mono = np.abs(x).mean(axis=1)
    peak_at = int(np.argmax(mono))
    n = len(x)
    t = np.arange(n - peak_at) / rate
    rate_k = rng.uniform(0.6, 1.2) * (0.6 if longer else 1.6)
    env = np.exp(-t * rate_k * 1.5) * 0.35 + 0.65 if longer else np.exp(-t * rate_k)
    x = x.copy()
    x[peak_at:] *= env[:, None]
    return x


def make(kind, sources, factor, db, amount, seed):
    rng = np.random.default_rng(seed)
    takes = [load(p) for p in sources]
    rate = takes[0][1]
    channels = max(t[0].shape[1] for t in takes)
    takes = [np.repeat(t[0], channels // t[0].shape[1], axis=1) if t[0].shape[1] != channels else t[0] for t in takes]
    if kind == "splice" and len(takes) >= 2:
        x = splice(takes[1], takes[0], rate, rng)
    else:
        x = shape_tail(takes[0], rate, rng, longer=factor < 1)
    x = resample(x, factor)
    x = tilt(x, rate, db)
    x = drive(x, amount)
    target = np.mean([loudness(t) for t in takes])
    x *= target / loudness(x)
    peak = np.abs(x).max()
    if peak > 0.98:
        x *= 0.98 / peak
    # A 3 ms fade at both ends: no click.
    edge = int(rate * 0.003)
    ramp = np.linspace(0, 1, edge)[:, None]
    x[:edge] *= ramp
    x[-edge:] *= ramp[::-1]
    return x.astype(np.float32), rate


def main():
    made = 0
    for sound, (folder, sources, new) in WANTED.items():
        paths = [os.path.join(SFX, folder, s) for s in sources]
        missing = [p for p in paths if not os.path.exists(p)]
        if missing:
            print(f"{sound}: missing {missing}", file=sys.stderr)
            continue
        for name, kind, factor, db, amount, seed in new:
            out = os.path.join(SFX, folder, name)
            x, rate = make(kind, paths, factor, db, amount, seed)
            sf.write(out, x, rate, subtype="PCM_16")
            made += 1
            print(f"{sound}: {name} ({kind}, x{factor}, {db:+.1f} dB, {len(x) / rate:.2f} s)")
    print(f"made {made}")


if __name__ == "__main__":
    main()
