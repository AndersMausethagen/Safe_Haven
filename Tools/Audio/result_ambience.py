"""Synthesises the sound of the raid results paintings' places, looped under the report while the painting moves
(Tools/LocalSpawn/result_motion.py makes the moving picture).

Getting out alive, at dusk by the portal: a soft breeze in pines (noise in bands whose gusts repeat exactly over the
loop, as world.py's wind, with more of the needles' hiss on top), a few small birds far off with the forest's echo, and
the portal's warm hum: a low chord that breathes, with the video model's own hum (MiniMax H3, thin but warm) under it
where result_motion.py left it. Falling, at night: a cold wind moaning low through the trunks with a thin whistle in it,
wood creaking now and then, a deep drone that beats slowly, and one low growl far off in the dark.

Everything that happens once (a bird, a creak, the growl) is placed with its echo wrapped round the loop's end, so the
loop meets itself with no seam and no click. Levelled for the ambience channel: an even bed, not a scene.

Run from the project root: python Tools/Audio/result_ambience.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
from foley import *  # noqa: E402,F403
from world import wind  # noqa: E402
import creatures  # noqa: E402

from scipy.io import wavfile  # noqa: E402
from scipy.signal import resample_poly  # noqa: E402

OUT = "Assets/_Project/Audio/Ambience"
H3 = "Tools/LocalSpawn/ResultMotion"
TARGET_RMS_DB = -24.0
PEAK_DB = -4.0

# The pine breeze at dusk: mostly needles and leaves, little low end.
PINES = [(90, 0.5, 0.3, 0.4), (320, 0.5, 0.5, 0.6), (1100, 0.45, 0.6, 0.8), (3200, 0.4, 0.5, 1.0), (6500, 0.35, 0.2, 1.2)]
# The cold night wind: a moan through dead trunks, as the raids' wind (world.WILDS), a little thinner.
COLD = [(48, 0.5, 0.9, 0.5), (110, 0.4, 1.0, 0.7), (230, 0.3, 0.9, 0.9), (340, 0.1, 0.5, 1.3), (470, 0.08, 0.34, 1.4),
        (800, 0.45, 0.45, 1.0), (2200, 0.5, 0.12, 1.1)]


def wrap(buffer, signal, seconds, level=1.0):
    """Adds a sound from that moment, what runs past the end coming round to the start: a loop with no seam."""
    if signal.ndim == 1:
        signal = np.stack([signal, signal], axis=1)
    start = int(RATE * seconds) % len(buffer)
    for offset in range(0, len(signal), len(buffer)):
        piece = signal[offset:offset + len(buffer)]
        first = min(len(piece), len(buffer) - start)
        buffer[start:start + first] += piece[:first] * level
        buffer[:len(piece) - first] += piece[first:] * level
    return buffer


def pan(signal, side):
    """Mono to stereo, -1 left to 1 right, equal power."""
    angle = (side + 1) * np.pi / 4
    return np.stack([signal * np.cos(angle), signal * np.sin(angle)], axis=1)


def rms(signal):
    return float(np.sqrt(np.mean(signal ** 2))) or 1e-9


def at_level(signal, db):
    return signal * (10 ** (db / 20) / rms(signal))


def breathing_chord(seconds, notes, rng, breaths=3):
    """Soft sines that swell and fall a whole number of times over the loop."""
    t = np.arange(int(RATE * seconds)) / RATE
    out = np.zeros(len(t))
    for i, (frequency, level) in enumerate(notes):
        # Each note a whole number of cycles over the loop, so it meets itself.
        f = round(frequency * seconds) / seconds
        swell = 0.6 + 0.4 * np.sin(TAU * (breaths + i) * t / seconds + rng.uniform(0, TAU))
        out += np.sin(TAU * f * t) * level * swell
    return out


def bird(rng):
    """A small bird far off: three to six quick rising or falling notes."""
    pieces = []
    for _ in range(int(rng.integers(3, 7))):
        duration = rng.uniform(0.05, 0.12)
        t = times(duration)
        a, b = rng.uniform(2600, 5200, 2)
        pitch = np.linspace(a, b, len(t)) * (1 + 0.03 * np.sin(TAU * 40 * t))
        phase = TAU * np.cumsum(pitch) / RATE
        pieces.append((np.sin(phase) + 0.2 * np.sin(2 * phase)) * np.sin(np.pi * t / duration) ** 2)
        pieces.append(blank(rng.uniform(0.03, 0.1)))
    return lowpass(np.concatenate(pieces), 6000)


def creak(rng):
    """Wood bending: a slow train of ticks that speeds up, through the timber's resonance."""
    seconds = rng.uniform(0.8, 1.4)
    t = times(seconds)
    rate = np.linspace(rng.uniform(12, 18), rng.uniform(30, 45), len(t))
    ticks = (np.diff(np.floor(np.cumsum(rate) / RATE), prepend=0) > 0).astype(float)
    body = band(ticks, rng.uniform(280, 380), rng.uniform(700, 950), order=3) * 6
    return fade_end(body * np.sin(np.pi * t / seconds) ** 1.5, 0.3)


def dusk(seconds, rng):
    out = np.zeros((int(RATE * seconds), 2))
    out += at_level(wind(seconds, PINES, "result dusk pines"), TARGET_RMS_DB - 1)
    hum = breathing_chord(seconds, [(98, 1.0), (147, 0.6), (196, 0.35)], rng)
    out += pan(at_level(lowpass(hum, 900), TARGET_RMS_DB - 13), 0.0)
    layer = h3_layer("MistyWoods_extracted", seconds)
    if layer is not None:
        out += at_level(layer, TARGET_RMS_DB - 12)
    for at in rng.uniform(0.5, seconds - 1.0, 5):
        call = space(bird(rng), FOREST, 0.35)
        out = wrap(out, call * 10 ** ((TARGET_RMS_DB + 3) / 20) / max(1e-9, np.abs(call).max()), at)
    return out


def night(seconds, rng):
    out = np.zeros((int(RATE * seconds), 2))
    out += at_level(wind(seconds, COLD, "result night wind"), TARGET_RMS_DB - 1)
    # A thin whistle in it: one narrow band of the same loop-exact wind, gusting harder.
    whistle = wind(seconds, [(1050, 0.04, 1.0, 1.4)], "result night whistle")
    out += at_level(whistle, TARGET_RMS_DB - 16)
    drone = breathing_chord(seconds, [(41, 1.0), (61.5, 0.7), (82, 0.25)], rng, breaths=2)
    out += pan(at_level(drone, TARGET_RMS_DB - 7), 0.0)
    # Levelled by their loudness while they sound, not their peak: a creak is sparse ticks, its peak far above its body.
    # Spread over the loop (a fifth, a half and four fifths in), away from the growl at three fifths.
    for share, side in zip((0.17, 0.45, 0.8), (-0.6, 0.5, -0.2)):
        dry = creak(rng)
        sound = space(pan(dry, side), FOREST, 0.4)
        out = wrap(out, sound * 10 ** ((TARGET_RMS_DB - 4) / 20) / rms(sound[:len(dry)]), seconds * share)
    growl = lowpass(creatures.wolf_snarl(rng), 1200)
    growl = np.concatenate([growl, growl[: len(growl) // 2] * 0.6])
    sound = space(growl, VALE, 0.8)
    out = wrap(out, sound * 10 ** ((TARGET_RMS_DB - 3) / 20) / rms(sound[:len(growl)]), seconds * 0.6)
    return out


def h3_layer(key, seconds):
    """The video model's own sound for the place (result_motion.py), resampled and tiled over the loop, or None."""
    path = os.path.join(H3, key + "_h3.wav")
    if not os.path.exists(path):
        return None
    rate, data = wavfile.read(path)
    data = data.astype(np.float64) / 32768
    if data.ndim == 1:
        data = np.stack([data, data], axis=1)
    data = resample_poly(data, RATE, rate, axis=0)
    # Its loop is about 6.8 s: tiled, it repeats within ours. Our loop is made a whole number of its lengths.
    reps = int(np.ceil(seconds * RATE / len(data)))
    return np.tile(data, (reps, 1))[: int(seconds * RATE)]


def finish(name, signal):
    # Filtered as if it ran round twice and the second time kept: a filter's start on a loop is a seam.
    signal = highpass(np.concatenate([signal, signal]), 30)[len(signal):]
    gain = 10 ** (TARGET_RMS_DB / 20) / rms(signal)
    signal = signal * gain
    peak = float(np.abs(signal).max())
    limit = 10 ** (PEAK_DB / 20)
    if peak > limit:
        signal *= limit / peak
        peak = limit
    save(OUT, name, signal, peak=peak, loop=True)
    print(f"{name}: {len(signal) / RATE:.2f} s, rms {20 * np.log10(rms(signal)):.1f} dBFS, peak {20 * np.log10(peak):.1f} dBFS")


def main():
    h3 = os.path.join(H3, "MistyWoods_extracted_h3.wav")
    # Three of the model's loops long when its hum is laid under, so that layer meets itself too.
    if os.path.exists(h3):
        rate, data = wavfile.read(h3)
        dusk_seconds = 3 * len(data) / rate
    else:
        dusk_seconds = 20.0
    finish("MistyWoods_extracted", dusk(dusk_seconds, seeded("result dusk")))
    finish("MistyWoods_failed", night(20.0, seeded("result night")))


if __name__ == "__main__":
    main()
