"""The sounds the opening cinematic, "The Fire at the Stones" (Docs/BackstoryCinematic.md), has no recording for. The fire,
wind, birds, creaks, footsteps and the ward's moments are the game's own; these are made here.

  Opening Stone Hum     a seamless 16 s loop: the stone's low hum, two close tones beating slowly, a soft overtone.
                        Under the fire and the ward ("A low stone hum begins beneath it").
  Opening Stone Pulse   3 s: the hum swells once and a faint ringing overtone answers. The rune's ring of light, the
                        stone answering the log ("The stone answers with a gentle pulse").
  Opening Hum Fades     10 s: the hum pulsing slower and slower until it is gone ("The stone hum slows to silence").
  Opening Ember Hiss    1.6 s: the last ember going out, a small hiss and a tick ("A small hiss from the ember").
  Opening Lantern Out   0.8 s: a lantern blown out, a soft puff and a tiny tick of metal ("Lanterns go out").
  Opening Shadow        4 s: a low rush of air that swells and passes, under the shadow over the morning fire.

Run from the project root: python Tools/Audio/opening_cinematic.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Cinematics"
HUM = 73.4  # D2: low enough to feel, high enough for small speakers' overtones to carry it.


def loop_crossfade(signal, overlap):
    n = int(overlap * RATE)
    head, body, tail = signal[:n], signal[n:-n], signal[-n:]
    ramp = np.linspace(0, np.pi / 2, n)
    shape = (n,) + (1,) * (signal.ndim - 1)
    return np.concatenate([tail * np.cos(ramp).reshape(shape) + head * np.sin(ramp).reshape(shape), body])


def hum_tone(t, rng, depth=1.0):
    """The stone's voice: a fundamental beating against itself, a fifth and an octave over it, and a breath of grit."""
    tone = np.sin(TAU * HUM * t) + 0.8 * np.sin(TAU * HUM * 1.009 * t + 0.4)
    tone += 0.35 * np.sin(TAU * HUM * 1.5 * t + 1.1) + 0.22 * np.sin(TAU * HUM * 2.0 * t + 2.3)
    grit = lowpass(band(pink(len(t), rng), 60, 400), 300)
    return tone / 2.4 + 0.12 * depth * grit / max(1e-9, np.abs(grit).max())


def stone_hum(rng):
    seconds, overlap = 16.0, 3.0
    total = seconds + overlap
    t = times(total)
    swell = 0.7 + 0.3 * np.sin(TAU * 2 * t / seconds)
    out = hum_tone(t, rng) * swell
    wide = np.stack([out, np.roll(out, int(0.011 * RATE))], axis=1)
    return loop_crossfade(space(darken(wide), VALE, 0.25), overlap)


def stone_pulse(rng):
    seconds = 3.0
    t = times(seconds)
    body = hum_tone(t, rng, 0.6) * bump(t, 0.05, 0.6, 0.9)
    ring_ = (np.sin(TAU * HUM * 6 * t) + 0.5 * np.sin(TAU * HUM * 8.01 * t)) * bump(t, 0.25, 0.35, 0.8) * 0.12
    return space(darken(body + ring_), VALE, 0.4)


def hum_fades(rng):
    seconds = 10.0
    t = times(seconds)
    # Each pulse comes later than the last and softer, and the last one doesn't come back.
    starts, gap, level = [], 0.9, 1.0
    at = 0.0
    while at < seconds - 2.5:
        starts.append((at, level))
        at += gap
        gap *= 1.38
        level *= 0.8
    envelope = np.zeros_like(t)
    for start, lvl in starts:
        envelope = np.maximum(envelope, lvl * bump(t, start, 0.35, 0.9))
    envelope *= np.clip((seconds - t) / 3.0, 0, 1)
    return space(darken(hum_tone(t, rng) * envelope), VALE, 0.35)


def ember_hiss(rng):
    seconds = 1.6
    t = times(seconds)
    hiss = band(white(len(t), rng), 2500, 7000) * bump(t, 0.02, 0.06, 0.45) * 0.5
    sizzle = band(white(len(t), rng), 900, 2500) * bump(t, 0.05, 0.1, 0.25) * (0.5 + 0.5 * np.sign(np.sin(TAU * 31 * t)))
    tick = np.zeros_like(t)
    place(tick, chip(rng, 3000, 6000), 0.62, 0.25)
    return space(hiss + 0.25 * sizzle + tick, FOREST, 0.15)


def lantern_out(rng):
    seconds = 0.8
    t = times(seconds)
    puff = sweep(pink(len(t), rng), 900 - 500 * np.clip(t / 0.3, 0, 1), 1.2) * bump(t, 0.0, 0.03, 0.12)
    out = puff / max(1e-9, np.abs(puff).max()) * 0.7
    place(out, chip(rng, 2500, 5500), 0.22, 0.12)
    return space(out, FOREST, 0.2)


def shadow(rng):
    seconds = 4.0
    t = times(seconds)
    air = sweep(pink(len(t), rng), 160 + 260 * bump(t, 0.4, 1.2, 1.4), 0.9)
    air = air / max(1e-9, np.abs(air).max()) * bump(t, 0.3, 1.3, 1.3)
    low = lowpass(pink(len(t), rng), 70) * bump(t, 0.6, 1.2, 1.2)
    wide = np.stack([air, np.roll(air, int(0.02 * RATE))], axis=1) + 0.4 * (low / max(1e-9, np.abs(low).max()))[:, None]
    return space(darken(wide), FOREST, 0.3)


def main():
    save(OUT, "Opening Stone Hum", stone_hum(seeded("opening hum")), peak=0.55, loop=True)
    save(OUT, "Opening Stone Pulse", stone_pulse(seeded("opening pulse")), peak=0.6)
    save(OUT, "Opening Hum Fades", hum_fades(seeded("opening hum fades")), peak=0.55)
    save(OUT, "Opening Ember Hiss", ember_hiss(seeded("opening ember")), peak=0.45, mono=True)
    save(OUT, "Opening Lantern Out", lantern_out(seeded("opening lantern")), peak=0.4, mono=True)
    save(OUT, "Opening Shadow", shadow(seeded("opening shadow")), peak=0.5)
    for name in ("Stone Hum", "Stone Pulse", "Hum Fades", "Ember Hiss", "Lantern Out", "Shadow"):
        print(os.path.join(OUT, f"Opening {name}.wav"))


if __name__ == "__main__":
    main()
