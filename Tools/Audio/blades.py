"""Makes the blades' hit sounds from Mixkit's sword recordings: cuts into flesh, with a body's thump under the heavy ones.

Mixkit Sound Effects Free License (https://mixkit.co/license/#sfxFree): free for commercial use, video games named, no attribution asked.
Not to be redistributed on their own or claimed as ours: these files go out inside the game and no other way, so if this repository is
ever made public, take Tools/Audio/Recorded/Mixkit and Assets/_Project/Audio/SFX/Mixkit Blades out of it first.

Run from the project root: python Tools/Audio/blades.py
"""
import os
import sys
import urllib.request

sys.path.insert(0, os.path.dirname(__file__))
from combat import blank, drive, place, thud  # noqa: E402
from foley import FOREST, RATE, np, save, seeded, space, wavfile  # noqa: E402

OUT = "Assets/_Project/Audio/SFX/Mixkit Blades"
SOURCES = os.path.join(os.path.dirname(__file__), "Recorded", "Mixkit")

# Mixkit's numbers for the recordings used.
RECORDINGS = {
    "cutting_flesh": 2788, "cutting_killing": 2168, "gore_slash_hit": 2796, "slides_body": 2791, "knife_fast_hit": 2184, "gore_splash": 263,
}


def fetch(name):
    path = os.path.join(SOURCES, name + ".wav")
    if not os.path.exists(path):
        os.makedirs(SOURCES, exist_ok=True)
        number = RECORDINGS[name]
        request = urllib.request.Request(f"https://assets.mixkit.co/active_storage/sfx/{number}/{number}.wav", headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(request) as response, open(path, "wb") as out:
            out.write(response.read())
    return path


def recording(name, pitch=1.0, seconds=None):
    """The recording as mono, its leading silence gone (they start up to a quarter second late, which a hit can't afford), at full level."""
    rate, data = wavfile.read(fetch(name))
    signal = data.astype(np.float64)
    if data.dtype.kind == "i":
        signal /= float(-np.iinfo(data.dtype).min)
    signal = signal.mean(axis=1) if signal.ndim == 2 else signal
    window = int(0.002 * rate)
    level = np.sqrt(np.convolve(signal ** 2, np.ones(window) / window, "same"))
    signal = signal[max(0, np.argmax(level > level.max() * 0.04) - int(0.001 * rate)):]
    signal = np.interp(np.arange(0, len(signal) - 1, rate / RATE * pitch), np.arange(len(signal)), signal)
    if seconds is not None:
        signal = signal[:int(seconds * RATE)]
        fade = min(len(signal) // 2, int(0.05 * RATE))
        signal[-fade:] *= np.linspace(1, 0, fade) ** 2
    return signal / max(1e-9, np.abs(signal).max())


def with_weight(cut, rng, weight, seconds):
    """A heavy weapon's cut: the same cut, with the body it went into under it."""
    mix = place(place(blank(seconds), cut, 0, 1), thud(rng, True), 0.002, weight)
    return drive(mix / max(1e-9, np.abs(mix).max()), 1.4)


# Name -> what it is made from. Every variant is a different recording, or the same one pitched well apart.
BLADE = [("cutting_flesh", 1.0, 0.5), ("gore_slash_hit", 1.0, 0.55), ("slides_body", 1.0, 0.45), ("knife_fast_hit", 0.94, 0.3)]
CLEAVE = [("cutting_killing", 0.92, 0.8), ("gore_slash_hit", 0.84, 0.7), ("cutting_flesh", 0.8, 0.6), ("slides_body", 0.82, 0.55)]
PIERCE = [("knife_fast_hit", 1.0, 0.3), ("slides_body", 1.12, 0.3), ("cutting_flesh", 1.16, 0.26)]
BACKSTAB = [("cutting_killing", 0.74, 0.9), ("gore_slash_hit", 0.7, 0.8)]


def main():
    for i, (name, pitch, seconds) in enumerate(BLADE, 1):
        save(OUT, f"Blade Hit {i}", space(recording(name, pitch, seconds), FOREST, 0.08), mono=True)

    for i, (name, pitch, seconds) in enumerate(CLEAVE, 1):
        mix = with_weight(recording(name, pitch, seconds), seeded(f"Cleave Hit {i}"), 0.55, seconds)
        save(OUT, f"Cleave Hit {i}", space(mix, FOREST, 0.08), mono=True)

    for i, (name, pitch, seconds) in enumerate(PIERCE, 1):
        save(OUT, f"Pierce Hit {i}", space(recording(name, pitch, seconds), FOREST, 0.08), mono=True)

    save(OUT, "Gore Splash", space(recording("gore_splash", 1.0, 0.8), FOREST, 0.08), mono=True)

    # Heard over the weapon's own hit: pitched down and weighted, so a backstab sounds like nothing else.
    for i, (name, pitch, seconds) in enumerate(BACKSTAB, 1):
        mix = with_weight(recording(name, pitch, seconds), seeded(f"Backstab Hit {i}"), 0.8, seconds)
        save(OUT, f"Backstab Hit {i}", space(mix, FOREST, 0.08), mono=True)


if __name__ == "__main__":
    main()
