"""The sound bed of the first-raid cinematic, "The Misty Woods" (Docs/MistyWoodsCinematic.md), and the sound of arriving.

The user's direction: low ambient drones and distant strings, wind and branches and distant creatures (those are the
game's own recordings, placed in the cinematic); the soundscape grows more uncomfortable when corruption is spoken of;
a faint warm tone when Haven is named; a faint unsettling sound as it fades into the game.

  Misty Woods Drone    a seamless 24 s loop: a sub hum that beats slowly against itself, a low bowed fifth that never
                       settles, and far wind in the canopy. Under every shot.
  Misty Woods Unease   15 s: low strings closing into a cluster (a semitone, then a tritone), a tremolo that quickens,
                       a rumble rising under it. The corruption shot.
  Misty Woods Warmth   12 s: a soft major chord of bowed strings far off, slow to come in. "Return to Haven".
  Misty Woods Arrival  6 s, mono (the game places it at the player): a slow breath through the trees and a far creak.
                       As the picture comes up in the raid.

No pure high tones (2026-11-16): the glassy harmonics over Unease, the halo over Warmth and the thin tone sliding down in
Arrival sounded like a wavy whistle or flute to the user ("that one sounds really weird") and are gone.

Run from the project root: python Tools/Audio/misty_woods_cinematic.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403

OUT = "Assets/_Project/Audio/SFX/Safe Haven Cinematics"


def strings(frequency, seconds, rng, attack, release, tremolo=0.0, tremolo_rate=5.5):
    """A bowed note held steady (foley.bowed's saw and body), with its own swell, release and optional tremolo."""
    t = times(seconds)
    catch = lowpass(white(len(t), rng), 10)
    catch /= max(1e-9, np.abs(catch).max())
    phase = np.cumsum(frequency * (1 + 0.003 * catch + 0.0018 * np.sin(TAU * rng.uniform(3.8, 4.8) * t))) / RATE
    saw = 2 * (phase % 1) - 1
    body = band(saw, 70, 420) + 0.5 * band(saw, 420, 1000) + 0.1 * band(saw, 1000, 2400)
    body += band(white(len(t), rng), 800, 2600) * 0.02 * (0.4 + saw ** 2)
    shape = np.clip(t / attack, 0, 1) ** 1.6 * np.clip((seconds - t) / release, 0, 1) ** 1.4
    if tremolo > 0:
        body *= 1 - tremolo * (0.5 + 0.5 * np.sin(TAU * tremolo_rate * t))
    return body * shape


def loop_crossfade(signal, overlap):
    """The last `overlap` seconds folded over the start (equal power), so the end runs on into the beginning."""
    n = int(overlap * RATE)
    head, body, tail = signal[:n], signal[n:-n], signal[-n:]
    ramp = np.linspace(0, np.pi / 2, n)
    shape = (n,) + (1,) * (signal.ndim - 1)
    joined = tail * np.cos(ramp).reshape(shape) + head * np.sin(ramp).reshape(shape)
    return np.concatenate([joined, body])


def drone(rng):
    seconds, overlap = 24.0, 4.0
    total = seconds + overlap
    t = times(total)
    # Everything moves in whole cycles of the loop, so the fold hides nothing.
    slow = lambda cycles, offset=0.0: 0.5 + 0.5 * np.sin(TAU * cycles * t / seconds + offset)  # noqa: E731
    sub = (np.sin(TAU * 41.2 * t) + 0.8 * np.sin(TAU * 41.2 * 1.0117 * t)) * (0.55 + 0.45 * slow(3))
    sub += 0.25 * np.sin(TAU * 61.7 * t) * slow(2, 1.3)
    fifth = strings(82.4, total, rng, attack=0.01, release=0.01) * (0.35 + 0.65 * slow(2, 0.4))
    fifth += 0.7 * strings(123.5, total, rng, attack=0.01, release=0.01) * (0.2 + 0.8 * slow(3, 2.1))
    fifth = lowpass(fifth, 900)
    wind = lowpass(band(pink(len(t), rng), 120, 900), 700) * (0.25 + 0.75 * slow(4, 0.7) ** 2)
    mono_bed = 0.5 * sub / np.abs(sub).max() + 0.45 * fifth / np.abs(fifth).max() + 0.35 * wind / np.abs(wind).max()
    wide = np.stack([mono_bed, np.roll(mono_bed, int(0.013 * RATE))], axis=1)
    wide[:, 1] += 0.2 * lowpass(band(pink(len(t), rng), 150, 800), 600) * slow(5, 2.0)
    return loop_crossfade(wide, overlap)


def unease(rng):
    seconds = 15.0
    t = times(seconds)
    swell = np.clip(t / 10.0, 0, 1) ** 1.8
    # Low strings: a fifth that a semitone and then a tritone push against.
    low = strings(82.4, seconds, rng, 1.5, 2.5, tremolo=0.15, tremolo_rate=4.5)
    low += 0.8 * strings(87.3, seconds, rng, 4.0, 2.5, tremolo=0.25, tremolo_rate=5.2)
    low += 0.6 * strings(116.5, seconds, rng, 7.0, 2.5, tremolo=0.35, tremolo_rate=6.3)
    # The same cluster an octave up, so it reads on small speakers too.
    low += 0.55 * strings(164.8, seconds, rng, 2.5, 2.5, tremolo=0.2, tremolo_rate=4.9)
    low += 0.5 * strings(174.6, seconds, rng, 5.0, 2.5, tremolo=0.3, tremolo_rate=5.7)
    low += 0.4 * strings(233.1, seconds, rng, 8.0, 2.5, tremolo=0.4, tremolo_rate=6.8)
    # The tremolo quickens as it grows: a second, faster shiver laid in over the last half.
    shiver = 1 - 0.3 * np.clip((t - 6) / 5, 0, 1) * (0.5 + 0.5 * np.sin(TAU * (6 + 4 * np.clip((t - 6) / 6, 0, 1)) * t))
    low *= shiver
    rumble = lowpass(pink(len(t), rng), 90) * np.clip(t / 11, 0, 1) ** 2 * np.clip((seconds - t) / 3, 0, 1)
    dry = (0.7 * low / np.abs(low).max() + 0.22 * rumble / np.abs(rumble).max()) * (0.2 + 0.8 * swell)
    return space(darken(dry), VALE, 0.35)


def warmth(rng):
    seconds = 12.0
    # D major, low and open, each voice in its own time.
    notes = [(146.8, 3.0), (185.0, 3.6), (220.0, 3.3), (293.7, 4.2)]
    out = sum(strings(f, seconds, rng, attack, 4.0) * (1.0 if f < 250 else 0.6) for f, attack in notes)
    out = lowpass(out, 1400)
    return space(out / np.abs(out).max(), VALE, 0.45)


def arrival(rng):
    seconds = 6.0
    t = times(seconds)
    # A long slow breath through the trees: pink noise whose band opens and closes like an exhale.
    breath = sweep(pink(len(t), rng), 320 + 180 * np.sin(TAU * 0.22 * t - 1.2), 1.4) * bump(t, 0.3, 1.6, 2.2)
    # Something far off: a wooden creak, low and stretched.
    creak = strings(55.0, 1.8, rng, 0.4, 0.8)
    creak = band(creak, 180, 1400) * 0.6
    out = blank(seconds)
    out[:] += breath / np.abs(breath).max() * 0.7
    place(out, creak, 2.6, 0.5)
    return space(darken(out), FOREST, 0.4)


def main():
    save(OUT, "Misty Woods Drone", drone(seeded("misty drone")), peak=0.6, loop=True)
    save(OUT, "Misty Woods Unease", unease(seeded("misty unease")), peak=0.7)
    save(OUT, "Misty Woods Warmth", warmth(seeded("misty warmth")), peak=0.5)
    save(OUT, "Misty Woods Arrival", arrival(seeded("misty arrival")), peak=0.5, mono=True)
    for name in ("Drone", "Unease", "Warmth", "Arrival"):
        print(os.path.join(OUT, f"Misty Woods {name}.wav"))


if __name__ == "__main__":
    main()
