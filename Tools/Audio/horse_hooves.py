"""Hoofbeats on other ground, made from the Horse AnimSet Pro pack's own steps (its dirt and grass hooves):

  Horse Hoof Stone 1-4.wav  harder and brighter: a sharp click on the strike and a short ring, for rock, gravel and paths
  Horse Hoof Wood 1-4.wav   lower and hollow: a resonant knock, for planks, bridges and floors
  Horse Hoof Water 1-4.wav  the step dulled and a splash over it, for shallow water

Written to Assets/_Project/Audio/SFX/Horse/. Run with any Python that has numpy and scipy:
  python Tools/Audio/horse_hooves.py
"""
import os
import sys
import wave

import numpy as np
from scipy import signal

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Malbers Animations", "Horse AnimSet Pro", "6 - Audio", "Horse")
OUT = os.path.join(ROOT, "Assets", "_Project", "Audio", "SFX", "Horse")
RATE = 44100


def read(path):
    with wave.open(path, "rb") as w:
        rate, channels, width, frames = w.getframerate(), w.getnchannels(), w.getsampwidth(), w.getnframes()
        data = w.readframes(frames)
    if width == 2:
        audio = np.frombuffer(data, dtype=np.int16).astype(np.float32) / 32768.0
    elif width == 4:
        audio = np.frombuffer(data, dtype=np.float32).astype(np.float32)
    else:
        sys.exit(f"{path}: {width * 8}-bit audio isn't read here")
    if channels > 1:
        audio = audio.reshape(-1, channels).mean(axis=1)
    if rate != RATE:
        audio = signal.resample(audio, int(len(audio) * RATE / rate)).astype(np.float32)
    return audio


def write(path, audio):
    peak = float(np.max(np.abs(audio))) or 1.0
    audio = np.clip(audio / peak * 0.85, -1.0, 1.0)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((audio * 32767).astype(np.int16).tobytes())


def pitch(audio, factor):
    """Plays faster (higher, shorter) or slower (lower, longer) by resampling."""
    return signal.resample(audio, max(16, int(len(audio) / factor))).astype(np.float32)


def envelope(length, attack, decay):
    t = np.arange(length) / RATE
    return np.minimum(1.0, t / max(attack, 1e-4)) * np.exp(-t / decay)


def stone(step, rng):
    hard = pitch(step, 1.18 + rng.uniform(-0.04, 0.04))
    high = signal.sosfilt(signal.butter(2, 1800, "highpass", fs=RATE, output="sos"), hard)
    click = rng.normal(0, 1, int(0.012 * RATE)) * envelope(int(0.012 * RATE), 0.0004, 0.003)
    click = signal.sosfilt(signal.butter(2, 3500, "highpass", fs=RATE, output="sos"), click)
    ring_t = np.arange(int(0.09 * RATE)) / RATE
    ring = np.sin(2 * np.pi * rng.uniform(2300, 2900) * ring_t) * np.exp(-ring_t / 0.02) * 0.12
    out = hard * 0.7 + high * 0.6
    out[:len(click)] += click * 0.5
    out[:len(ring)] += ring
    return out


def wood(step, rng):
    low = pitch(step, 0.86 + rng.uniform(-0.04, 0.04))
    body = signal.sosfilt(signal.butter(2, [120, 900], "bandpass", fs=RATE, output="sos"), low)
    # A hollow board: a comb filter (a short echo inside the plank) and a damped low knock.
    delay = int(RATE / rng.uniform(170, 210))
    comb = np.copy(body)
    for i in range(delay, len(comb)):
        comb[i] += comb[i - delay] * 0.55
    knock_t = np.arange(int(0.12 * RATE)) / RATE
    knock = np.sin(2 * np.pi * rng.uniform(140, 180) * knock_t) * np.exp(-knock_t / 0.035) * 0.35
    out = comb * 0.6 + low * 0.3
    out[:len(knock)] += knock
    return out


def water(step, rng):
    dull = signal.sosfilt(signal.butter(2, 700, "lowpass", fs=RATE, output="sos"), step) * 0.6
    length = int(rng.uniform(0.28, 0.38) * RATE)
    noise = rng.normal(0, 1, length)
    splash = signal.sosfilt(signal.butter(2, [600, 5000], "bandpass", fs=RATE, output="sos"), noise)
    splash *= envelope(length, 0.006, 0.08)
    # Droplets after the splash.
    for _ in range(rng.integers(3, 7)):
        at = int(rng.uniform(0.05, 0.25) * RATE)
        drop_t = np.arange(int(0.03 * RATE)) / RATE
        drop = np.sin(2 * np.pi * rng.uniform(1200, 2600) * drop_t * (1 + drop_t * 8)) * np.exp(-drop_t / 0.008) * 0.25
        end = min(length, at + len(drop))
        splash[at:end] += drop[:end - at]
    out = np.zeros(max(len(dull), length), dtype=np.float32)
    out[:len(dull)] += dull
    out[:length] += splash * 0.8
    return out


def main():
    steps = [read(os.path.join(SOURCE, f"Horse Step_0{i}.wav")) for i in range(1, 6)]
    os.makedirs(OUT, exist_ok=True)
    rng = np.random.default_rng(17)
    for kind, make in (("Stone", stone), ("Wood", wood), ("Water", water)):
        for i in range(4):
            write(os.path.join(OUT, f"Horse Hoof {kind} {i + 1}.wav"), make(steps[i % len(steps)], rng))
    print("hoofbeats written to", OUT)


if __name__ == "__main__":
    main()
