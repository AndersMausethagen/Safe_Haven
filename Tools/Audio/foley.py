"""The workshop the made sounds share: physical models of struck, plucked, blown and spoken things, and the air around them.

Nothing here plays a tone and calls it an object. A struck thing is a set of resonant modes rung by a short contact, so a
hard hit is bright and a soft one dull for the same reason they are in life. Wood loses its highs at once, iron rings on
and beats, a skin has the ratios of a membrane. Strings are Karplus-Strong, voices a glottal pulse through a vocal tract,
and every sound is finished in a generated room so it sits somewhere instead of on the listener's nose.

Each sound seeds its own generator from its name (seeded), so changing one recipe leaves every other clip identical.
"""
import functools
import os
import zlib

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, fftconvolve, lfilter, sosfilt

RATE = 44100
TAU = 2 * np.pi

# Seconds to fall 60 dB, how much early bounce, how much top end survives in the tail.
FOREST = (0.9, 16, 0.25)
HUT = (0.3, 10, 0.3)
HALL = (1.7, 12, 0.2)
VALE = (4.2, 10, 0.12)


def seeded(name):
    return np.random.default_rng(zlib.crc32(name.encode()))


def times(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def white(n, rng):
    return rng.standard_normal(n)


def pink(n, rng):
    spectrum = np.fft.rfft(rng.standard_normal(n))
    spectrum /= np.sqrt(np.maximum(np.fft.rfftfreq(n, 1 / RATE), 20.0))
    signal = np.fft.irfft(spectrum, n)
    return signal / max(1e-9, signal.std())


def band(signal, low, high, order=2):
    return sosfilt(butter(order, [low, min(high, RATE * 0.45)], btype="bandpass", fs=RATE, output="sos"), signal, axis=0)


def lowpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="lowpass", fs=RATE, output="sos"), signal, axis=0)


def highpass(signal, cutoff, order=2):
    return sosfilt(butter(order, cutoff, btype="highpass", fs=RATE, output="sos"), signal, axis=0)


def _shelf(signal, frequency, db, high):
    a = 10 ** (db / 40)
    w = TAU * frequency / RATE
    cos, alpha = np.cos(w), np.sin(w) / np.sqrt(2)
    root = 2 * np.sqrt(a) * alpha
    sign = -1 if high else 1
    b0 = a * ((a + 1) - sign * (a - 1) * cos + root)
    b1 = 2 * sign * a * ((a - 1) - sign * (a + 1) * cos)
    b2 = a * ((a + 1) - sign * (a - 1) * cos - root)
    a0 = (a + 1) + sign * (a - 1) * cos + root
    a1 = -2 * sign * ((a - 1) + sign * (a + 1) * cos)
    a2 = (a + 1) + sign * (a - 1) * cos - root
    return sosfilt(np.array([[b0 / a0, b1 / a0, b2 / a0, 1, a1 / a0, a2 / a0]]), signal, axis=0)


def low_shelf(signal, frequency, db):
    return _shelf(signal, frequency, db, False)


def high_shelf(signal, frequency, db):
    return _shelf(signal, frequency, db, True)


def darken(signal, top=-5.0, weight=3.0):
    """The game's tone: the air and fizz taken off, a little more chest."""
    return lowpass(high_shelf(low_shelf(signal, 140, weight), 6500, top), 13000)


def sweep(signal, centre, q):
    """A band-pass whose centre moves: wind past an edge, a throat changing shape. A state-variable filter, sample by sample."""
    g = np.tan(np.pi * np.clip(centre, 20, RATE * 0.45) / RATE)
    k = 1 / q
    a1 = 1 / (1 + g * (g + k))
    a2 = g * a1
    a3 = g * a2
    out = np.empty(len(signal))
    low, mid = 0.0, 0.0
    for i, (x, b1, b2, b3) in enumerate(zip(signal.tolist(), a1.tolist(), a2.tolist(), a3.tolist())):
        v3 = x - mid
        v1 = b1 * low + b2 * v3
        v2 = mid + b2 * low + b3 * v3
        low = 2 * v1 - low
        mid = 2 * v2 - mid
        out[i] = v1
    return out * k


def decay(t, tau, start=0.0):
    return np.where(t < start, 0.0, np.exp(-np.clip(t - start, 0, None) / tau))


def bump(t, start, attack, tau):
    """A smooth rise over attack, then an exponential fall."""
    x = t - start
    rise = np.clip(x / attack, 0, 1)
    return np.where(x < 0, 0.0, rise * rise * (3 - 2 * rise) * np.exp(-np.clip(x - attack, 0, None) / tau))


def blank(seconds):
    return np.zeros(int(RATE * seconds))


def place(buffer, signal, seconds, level=1.0):
    """Adds a sound into a buffer from that moment on, cutting what would run past its end."""
    start = int(RATE * seconds)
    length = min(len(signal), len(buffer) - start)
    if 0 < length < len(signal):
        signal = fade_end(signal[:length], 0.15)
    if length > 0:
        buffer[start:start + length] += signal[:length] * level
    return buffer


def fade_end(signal, share=0.2):
    """Lets a sound die away over the last of its length. A ring cut off by the end of its buffer is a click."""
    out = signal.copy()
    length = max(2, int(len(out) * share))
    out[-length:] *= np.linspace(1, 0, length) ** 2
    return out


def drive(signal, amount):
    """Soft saturation, which is most of what makes a thing feel heavy and close."""
    peak = max(1e-9, np.abs(signal).max())
    return np.tanh(signal / peak * amount) / np.tanh(amount) * peak


# ----- Things that are struck -----

def ring(modes, seconds, rng):
    t = times(seconds)
    out = np.zeros(len(t))
    for frequency, level, tau in modes:
        if frequency < RATE * 0.45:
            out += level * np.sin(TAU * frequency * t + rng.uniform(0, TAU)) * np.exp(-t / tau)
    return out


def strike(modes, seconds, contact, rng):
    """Rings the modes with a contact this many seconds long: it is the contact, not a filter, that sets how hard the hit sounds."""
    width = max(3, int(contact * RATE))
    pulse = np.hanning(width + 2)[1:-1] * (1 + 0.25 * white(width, rng))
    return fade_end(fftconvolve(ring(modes, seconds, rng), pulse / pulse.sum())[:int(seconds * RATE)])


def wood(base, rng, damp=0.05, count=12):
    """A beam's first modes plus the irregular ones of a real plank. Highs die almost at once."""
    ratios = [1, 2.76, 5.4, 8.93] + list(rng.uniform(1.4, 11, count - 4))
    return [(base * r * (1 + rng.normal(0, 0.02)), rng.uniform(0.6, 1) / r ** 0.8, damp / r ** 0.9) for r in ratios]


def iron(base, rng, hold=0.8, count=30):
    """Dense inharmonic modes in close pairs, whose beating is what makes metal shimmer rather than chime."""
    modes = []
    ratio = 1.0
    for _ in range(count):
        ratio += rng.uniform(0.1, 0.55)
        frequency = base * ratio
        level = rng.uniform(0.3, 1) / ratio ** 0.7
        tau = hold * rng.uniform(0.6, 1.2) / ratio ** 0.6
        modes.append((frequency, level, tau))
        modes.append((frequency * (1 + rng.uniform(0.0006, 0.0025)), level * rng.uniform(0.5, 1), tau * 0.85))
    return modes


def stone(base, rng, count=18):
    ratios = rng.uniform(1, 7, count)
    return [(base * r, rng.uniform(0.4, 1) / r ** 0.5, 0.022 / r ** 0.5) for r in ratios]


def bell(base, seconds, rng, hold=1.0):
    """A cast bell's partials. The tierce a minor third over the prime is why bells sound grave, and it gets the most room here."""
    partials = [(0.5, 0.55, 2.6), (1.0, 0.8, 1.9), (1.2, 1.0, 1.6), (1.5, 0.4, 1.1), (2.0, 0.7, 0.9), (2.51, 0.24, 0.6), (3.0, 0.3, 0.45), (4.05, 0.14, 0.3)]
    modes = []
    for ratio, level, tau in partials:
        modes.append((base * ratio, level, tau * hold))
        modes.append((base * ratio * 1.0016, level * 0.7, tau * hold * 0.9))
    t = times(seconds)
    clapper = band(white(len(t), rng), 300, 1500) * decay(t, 0.012) * 0.25
    return strike(modes, seconds, 0.0014, rng) + clapper


def drum(base, seconds, rng, tension=0.12):
    """A frame drum: the ratios of a circular membrane, tuned sharp for an instant as the stroke stretches the skin."""
    t = times(seconds)
    bend = 1 + tension * np.exp(-t / 0.035)
    phase = TAU * base * np.cumsum(bend) / RATE
    ratios = [(1.0, 1.0, 0.55), (1.594, 0.55, 0.32), (2.136, 0.35, 0.22), (2.296, 0.25, 0.18), (2.653, 0.18, 0.13), (2.918, 0.12, 0.1), (3.5, 0.08, 0.07)]
    tone = sum(level * np.sin(phase * ratio) * np.exp(-t / tau) for ratio, level, tau in ratios)
    return fade_end(tone + band(white(len(t), rng), 500, 2400) * decay(t, 0.012) * 0.22)


def thump(rng, seconds, low, high, tau, sweep_tau=0.018):
    """A body taking a blow. Mostly a muffled burst of noise: a falling sine alone is a drum machine's kick, and small speakers
    can't play it at all, so it only adds the weight underneath."""
    t = times(seconds)
    pitch = low + (high - low) * np.exp(-t / sweep_tau)
    burst = band(pink(len(t), rng), low * 1.4, high * 5)
    return fade_end((0.4 * np.sin(TAU * np.cumsum(pitch) / RATE) + 0.9 * burst / max(1e-9, np.abs(burst).max())) * np.exp(-t / tau))


def scatter(seconds, count, grain, rng, early=2.0):
    """Many small things falling: grains that bunch towards the start, as debris does."""
    out = blank(seconds)
    for _ in range(count):
        place(out, grain(), seconds * rng.random() ** early, rng.uniform(0.25, 1))
    return out


def chip(rng, low, high, seconds=0.04):
    """One fragment of something breaking: a tick of noise too short to have a pitch. Ringing modes here would plink."""
    t = times(seconds)
    centre = rng.uniform(low, high)
    return fade_end(band(white(len(t), rng), centre * 0.6, centre * 1.7) * decay(t, rng.uniform(0.002, 0.007)), 0.4)


def scrape(seconds, base, rng):
    """Metal dragged along metal: gritty noise feeding the blade's own ringing modes."""
    t = times(seconds)
    grit = lowpass(np.abs(white(len(t), rng)) * white(len(t), rng), 9000)
    modes = iron(base, rng, hold=0.05, count=12)
    return fftconvolve(grit, ring(modes, 0.12, rng))[:len(t)] * np.clip((seconds - t) / (seconds * 0.5), 0, 1)


# ----- Things that move through air -----

def whoosh(seconds, low, high, q, rng, peak=0.45, flutter=0.0, flutter_rate=30.0):
    """Air parting around something swung: speed sets both the pitch and the loudness, and it is fastest late in the arc."""
    t = times(seconds)
    x = np.clip(t / seconds, 1e-6, 1)
    speed = x ** 2 * (1 - x) ** (2 * (1 - peak) / peak)
    speed = speed / speed.max()
    air = sweep(pink(len(t), rng), low + (high - low) * speed, q) * speed ** 1.4
    if flutter > 0:
        air *= 1 + flutter * np.sin(TAU * flutter_rate * t * (0.7 + 0.5 * x))
    return air


def cloth(seconds, rng, centre=1800.0):
    """Fabric snapping: noise opened and shut by irregular little crinkles."""
    t = times(seconds)
    crinkle = lowpass(np.abs(white(len(t), rng)) ** 3, 90, 2)
    crinkle /= max(1e-9, crinkle.max())
    body = bump(t, 0, seconds * 0.15, seconds * 0.35)
    return fade_end(band(pink(len(t), rng), centre * 0.4, centre * 2.6) * crinkle * body, 0.4)


# ----- Things with a voice or a string -----

def voice(pitch, formants, rng, rough=0.0, breath=0.1):
    """A throat: glottal pulses (with period doubling when rough, the growl in a growl) through a vocal tract's resonances."""
    n = len(pitch)
    wander = lowpass(white(n, rng), 24)
    wander /= max(1e-9, np.abs(wander).max())
    phase = np.cumsum(pitch * (1 + 0.02 * wander)) / RATE
    cycle = phase % 1
    glottis = np.where(cycle < 0.6, np.sin(np.pi * cycle / 0.6) ** 2, 0.0)
    source = np.gradient(glottis) * RATE / np.maximum(pitch, 20) / 6
    if rough > 0:
        source *= 1 - rough * 0.75 * (np.floor(phase) % 2)
        flap = lowpass(white(n, rng), 70)
        source *= 1 + rough * flap / max(1e-9, np.abs(flap).max())
    source += white(n, rng) * breath * (0.3 + glottis)
    out = np.zeros(n)
    for centre, q, level in formants:
        out += sweep(source, centre * np.ones(n), q) * level
    return out


def vowel(*tracks):
    """Formants for voice: each (start Hz, end Hz, q, level), gliding over the sound's length."""
    return lambda n: [(np.geomspace(a, b, n), q, level) for a, b, q, level in tracks]


def pluck(frequency, seconds, rng, damping=0.996, brightness=0.5):
    """A gut string (Karplus-Strong): a burst of noise going round a delay line, losing its highs each trip."""
    delay = max(2, int(round(RATE / frequency - 0.5)))
    n = int(seconds * RATE)
    burst = np.zeros(n)
    burst[:delay] = lowpass(white(delay, rng), 600 + 6000 * brightness)
    burst[:delay] -= np.roll(burst[:delay], delay // 7)
    feedback = np.zeros(delay + 2)
    feedback[0] = 1
    feedback[delay] = -damping * 0.5
    feedback[delay + 1] = -damping * 0.5
    return lfilter([1.0], feedback, burst)


def lyre(frequency, seconds, rng, brightness=0.35):
    """A plucked string over a hollow wooden body."""
    string = pluck(frequency, seconds, rng, brightness=brightness)
    return string + 0.5 * band(string, 180, 520) + 0.25 * band(string, 700, 1300)


def bowed(frequency, seconds, rng, attack=0.3):
    """A horsehair bow on a low string, the tagelharpa's kind of sound: a saw wavering as the bow catches, through a wooden body."""
    t = times(seconds)
    catch = lowpass(white(len(t), rng), 14)
    catch /= max(1e-9, np.abs(catch).max())
    phase = np.cumsum(frequency * (1 + 0.0035 * catch + 0.0016 * np.sin(TAU * 4.3 * t))) / RATE
    saw = 2 * (phase % 1) - 1
    rasp = band(white(len(t), rng), 900, 3200) * 0.035 * (0.4 + saw ** 2)
    body = band(saw, 110, 460) + 0.55 * band(saw, 460, 1100) + 0.14 * band(saw, 1100, 2600)
    return (body + rasp) * np.clip(t / attack, 0, 1) ** 1.5 * np.clip((seconds - t) / (seconds * 0.45), 0, 1) ** 1.3


def choir(frequency, seconds, rng, voices=5, attack=0.8):
    """Several low voices holding an open vowel, never quite together."""
    t = times(seconds)
    out = np.zeros(len(t))
    for _ in range(voices):
        rate = rng.uniform(4.2, 5.4)
        pitch = frequency * (1 + rng.uniform(-0.004, 0.004) + 0.004 * np.sin(TAU * rate * t + rng.uniform(0, TAU)))
        out += voice(pitch, [(560.0, 6, 1.0), (980.0, 7, 0.55), (2500.0, 9, 0.12)], rng, breath=0.25)
    return out * np.clip(t / attack, 0, 1) ** 1.5 * np.clip((seconds - t) / (seconds * 0.5), 0, 1) ** 1.5


# ----- The air around it, and the file -----

@functools.lru_cache(maxsize=None)
def _impulse(seconds, early, top):
    rng = seeded(f"room {seconds}")
    n = int(seconds * RATE)
    t = times(seconds)
    channels = []
    for _ in range(2):
        tail = (lowpass(white(n, rng), 500) * np.exp(-t * 6.9 / seconds)
                + band(white(n, rng), 500, 2500) * np.exp(-t * 6.9 / (seconds * 0.6))
                + band(white(n, rng), 2500, 7000) * np.exp(-t * 6.9 / (seconds * 0.25)) * top)
        tail *= np.clip((t - 0.008) / 0.05, 0, 1)
        for _ in range(early):
            moment = int(rng.uniform(0.006, 0.14) * RATE)
            tail[moment] += rng.uniform(-1, 1) * 18 * np.exp(-moment / RATE * 16)
        channels.append(tail / np.sqrt(np.sum(tail ** 2)))
    return np.stack(channels, axis=1)


def space(dry, place_, wet, tail=None):
    """Puts a sound in a place. Returns stereo; save mixes it down for what the game positions in the world."""
    ir = _impulse(*place_)
    length = len(dry) + (int(tail * RATE) if tail else len(ir)) - 1
    out = np.zeros((length, 2))
    out[:len(dry)] = dry[:, None] if dry.ndim == 1 else dry
    for channel in range(2):
        source = dry if dry.ndim == 1 else dry[:, channel]
        out[:, channel] += fftconvolve(source, ir[:, channel])[:length] * wet
    return out


def save(folder, name, signal, peak=0.9, mono=False, loop=False):
    """Writes 16-bit with dither. Mono for sounds the game positions; silence trimmed off the end of anything that isn't a loop."""
    signal = np.asarray(signal, dtype=np.float64)
    if signal.ndim == 2 and mono:
        signal = signal.mean(axis=1)
    if not loop:
        signal = highpass(signal, 28)
        level = np.abs(signal if signal.ndim == 1 else signal.max(axis=1))
        audible = np.nonzero(level > level.max() * 10 ** (-62 / 20))[0]
        signal = signal[:min(len(signal), audible[-1] + int(RATE * 0.02))]
        shape = (len(signal),) + (1,) * (signal.ndim - 1)
        fade = np.ones(len(signal))
        out_length = min(len(signal) // 3, int(RATE * 0.05))
        fade[-out_length:] = np.linspace(1, 0, out_length) ** 2
        fade[:int(RATE * 0.0006)] = np.linspace(0, 1, int(RATE * 0.0006))
        signal = signal * fade.reshape(shape)
    signal = signal / max(1e-9, np.abs(signal).max()) * peak
    rng = seeded(name)
    dither = (rng.random(signal.shape) - rng.random(signal.shape)) / 32768
    os.makedirs(folder, exist_ok=True)
    wavfile.write(os.path.join(folder, name + ".wav"), RATE, (np.clip(signal + dither, -1, 1) * 32767).astype(np.int16))
