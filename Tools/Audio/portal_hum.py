"""Synthesises the portal's hum (the user, 2026-11-23: "add a small sound effect when near the portal"): a quiet, seamless loop heard only
close to the arch.

A low, soft drone on two tones a fifth apart that beat slowly against each other, so the hum breathes; an airy shimmer of high, filtered
breath that swells and sinks; and now and then a faint glassy tone, like light ringing in water, coming and going. It stays under
everything else: small, magical, never a drone you notice. Ten seconds, the tail crossfaded onto the head so it loops without a seam.

Run from the project root: python Tools/Audio/portal_hum.py
Writes Assets/_Project/Audio/SFX/Safe Haven Portal/portal_hum_loop.wav
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Portal"
LOOP = 10.0
TAIL = 1.5


def tone(frequency, seconds, wobble, rate, phase=0.0):
    """A sine that drifts a little in pitch, so it is never a test tone."""
    t = times(seconds)
    drift = 1.0 + wobble * np.sin(2 * np.pi * rate * t + phase)
    return np.sin(2 * np.pi * np.cumsum(frequency * drift) / RATE)


def main():
    rng = seeded("portal_hum_loop")
    seconds = LOOP + TAIL
    t = times(seconds)
    n = len(t)

    # The drone: D2 and A2 with a slow beat, and a soft octave above that comes and goes.
    drone = tone(73.4, seconds, 0.002, 0.09) * 0.55 + tone(110.2, seconds, 0.002, 0.07, 1.1) * 0.4 + tone(110.6, seconds, 0.001, 0.05) * 0.25
    drone += tone(146.8, seconds, 0.003, 0.11, 0.4) * (0.18 + 0.12 * np.sin(2 * np.pi * t / 5.0))
    drone = lowpass(drone, 420)

    # The shimmer: breath through a narrow, slowly wandering band, swelling and sinking.
    air = pink(n, rng)
    shimmer = band(air, 2600, 5200) * (0.5 + 0.5 * np.sin(2 * np.pi * t / 3.3 + 0.7)) ** 2
    shimmer += band(air[::-1], 1400, 2400) * 0.5 * (0.5 + 0.5 * np.sin(2 * np.pi * t / 4.7))
    shimmer *= 0.09

    # Glassy tones: a few soft bell-like notes from a pentatonic set, faint and spread over the loop.
    glints = np.zeros(n)
    notes = [587.3, 659.3, 880.0, 987.8, 1174.7]
    for at in (0.6, 2.9, 4.8, 7.1, 8.7):
        frequency = notes[rng.integers(len(notes))] * rng.uniform(0.997, 1.003)
        note = bell(frequency, 3.5, rng, hold=1.6) * 0.06
        place(glints, note, at)

    mix = drone * 0.5 + shimmer + glints
    mix = space(mix, (1.8, 10, 0.05), 0.35).mean(axis=1)  # the game positions it: mono

    loop_n, tail_n = int(RATE * LOOP), int(RATE * TAIL)
    looped = mix[:loop_n].copy()
    fade = np.linspace(0.0, 1.0, tail_n)
    looped[:tail_n] = looped[:tail_n] * fade + mix[loop_n:loop_n + tail_n] * (1.0 - fade)
    save(OUT, "portal_hum_loop", looped, peak=0.7, mono=True, loop=True)
    print("wrote", os.path.join(OUT, "portal_hum_loop.wav"))


if __name__ == "__main__":
    main()
