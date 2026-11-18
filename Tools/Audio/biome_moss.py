"""Safe Haven's biome beds, one-shots and foley from LocalSpawn's MOSS-SoundEffect v2 (Docs/LookAndFeelResearch_2026-11-18.md A1;
the user, 2026-11-18: "create folley and ambience with the localspawns model that best fits the task").

MOSS v2 (48 kHz mono, up to 30 s, Apache-2.0) is the better of LocalSpawn's two foley models. A bed is two takes of one prompt,
one to the left and one to the right (a natural stereo field, as two microphones apart), cut to loop with a long crossfade.
A one-shot is the event cut out of a take; footsteps and rustles are cut step by step out of a walking take.

Two phases:
  1. generate (LocalSpawn's Python, the Editor closed: the model takes ~11 GB to load):
       D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/Audio/biome_moss.py generate [name ...]
  2. process (any Python with numpy and scipy), from the project root:
       python Tools/Audio/biome_moss.py process [name ...]
Raw takes stay in D:/ComfyUI/output/agent/audio/biomes; the cut clips go to Assets/_Project/Audio/Ambience/<Biome>.
Agents can't listen: process prints loudness, length and the whistle check (no pure high tones, the user's rule of 2026-11-16).
"""
import json
import os
import sys

RAW = "D:/ComfyUI/output/agent/audio/biomes"
OUT = "Assets/_Project/Audio/Ambience"
RATE = 48000
NEGATIVE = "music, melody, singing, speech, voices, whistle, flute, synthesizer, beeps, electronic, distortion, clipping"

# name: (folder, kind, seconds, takes, prompt). kind: bed (stereo loop), shot (the event cut out), steps (each step cut out).
SOUNDS = {
    # The Misty Woods: a dense, damp spruce forest.
    "woods_bed_day": ("Misty Woods", "bed", 30, 2,
                      "Ambience of a dense old spruce forest on a cold damp day: a low steady wind high in the needle canopy, soft "
                      "rustling of branches far above, very quiet and close, no birds, no water, no animals, field recording"),
    "woods_bed_night": ("Misty Woods", "bed", 30, 2,
                        "Ambience of a dense spruce forest at night: still cold air, the faintest low breath of wind in the trees, "
                        "occasional tiny drip of water on moss, deep quiet, no insects, no birds, field recording"),
    "woods_creak": ("Misty Woods", "shot", 7, 3,
                    "A tall old spruce trunk slowly creaking and groaning as it sways in the wind, deep woody creak, close, forest"),
    "woods_drips": ("Misty Woods", "shot", 6, 3,
                    "Large drops of water falling from tree branches onto wet moss and leaves, a few slow drips, quiet forest"),
    "woods_owl": ("Misty Woods", "shot", 6, 3,
                  "A tawny owl hooting once in the distance in a dark forest at night, low soft hoots, reverberant woods"),
    "woods_woodpecker": ("Misty Woods", "shot", 5, 3,
                         "A woodpecker drumming on a dead tree trunk somewhere far away in a large forest, short fast burst"),
    # Troll Valley: open, rocky, a river below.
    "valley_bed_day": ("Troll Valley", "bed", 30, 2,
                       "Ambience of a wide open rocky mountain valley: cold breeze over grass and stones, the wind rising and "
                       "falling softly, a large river rushing far away below, open air, no birds, field recording"),
    "valley_bed_night": ("Troll Valley", "bed", 30, 2,
                         "Ambience of a mountain valley at night: cold calm air over rocks, a distant river murmuring far below, "
                         "very quiet and wide, no insects, no birds, field recording"),
    "valley_raven": ("Troll Valley", "shot", 6, 3,
                     "A raven croaking twice, deep hoarse calls echoing off a rocky mountain valley wall, distant"),
    "valley_scree": ("Troll Valley", "shot", 6, 3,
                     "Small loose stones and gravel trickling and tumbling down a scree slope under a cliff, a short rockfall, "
                     "dry clatter, outdoors"),
    # The Moss Marsh: wet, flat, alive.
    "marsh_bed_day": ("Moss Marsh", "bed", 30, 2,
                      "Ambience of a northern bog and marsh on a summer day: a soft hum of midges and flies over still water, "
                      "light wind through reeds and sedge, gentle water lapping, no birds singing, field recording"),
    "marsh_bed_night": ("Moss Marsh", "bed", 30, 2,
                        "Ambience of a marsh at dusk: many frogs croaking in a chorus over still water, toads in the distance, "
                        "quiet reeds, humid night air, field recording"),
    "marsh_bittern": ("Moss Marsh", "shot", 7, 3,
                      "A Eurasian bittern booming across a reed marsh, very deep low hollow boom, three times, distant"),
    "marsh_plop": ("Moss Marsh", "shot", 3, 3,
                   "A frog jumping into still pond water, one small wet plop and splash, close"),
    "marsh_bubbles": ("Moss Marsh", "shot", 5, 3,
                      "Marsh gas bubbles rising and popping slowly at the surface of a muddy bog pool, wet gurgles, close"),
    # Foley: steps the ground had none of, and plants against the legs.
    "steps_mud": ("Foley", "steps", 14, 2,
                  "Footsteps of one person walking slowly through deep wet mud and peat bog, squelching steps, boots, close"),
    "steps_gravel": ("Foley", "steps", 14, 2,
                     "Footsteps of one person walking on loose gravel and scree stones, crunching steps, boots, close, outdoors"),
    "rustle_reeds": ("Foley", "steps", 12, 2,
                     "A person pushing slowly through tall dry reeds and sedge, stems swishing and brushing against legs with "
                     "each step, close"),
}


def generate(names):
    sys.path.insert(0, "D:/ComfyUI/agent")
    import moss_sfx  # LocalSpawn's MOSS runner (its own Python, one process per job)

    items = []
    for name in names:
        folder, kind, seconds, takes, prompt = SOUNDS[name]
        for take in range(takes):
            out = os.path.join(RAW, f"{name}_v{take + 1}.wav")
            if os.path.exists(out):
                continue
            items.append({"prompt": prompt, "seconds": float(seconds), "seed": 7000 + take * 131 + len(items) * 7,
                          "negative": NEGATIVE, "output": out})
    os.makedirs(RAW, exist_ok=True)
    if not items:
        print("all takes already made")
        return
    print(f"making {len(items)} takes")
    result = moss_sfx.run(items, timeout=7200)
    print(json.dumps({k: result.get(k) for k in ("ok", "seconds", "load_seconds", "peak_vram_gb")}))


# ----------------------------------------------------------------------------------------------------------------- process

def _load(path):
    from scipy.io import wavfile
    import numpy as np
    rate, data = wavfile.read(path)
    integer = data.dtype.kind == "i"
    data = data.astype(np.float64)
    if integer:
        data = data / 32768.0
    if data.ndim > 1:
        data = data.mean(axis=1)
    if rate != RATE:
        from scipy.signal import resample_poly
        data = resample_poly(data, RATE, rate)
    return data


def _highpass(signal, cutoff=45.0):
    from scipy.signal import butter, sosfiltfilt
    sos = butter(2, cutoff, btype="highpass", fs=RATE, output="sos")
    return sosfiltfilt(sos, signal, axis=0)


def _rms_db(signal):
    import numpy as np
    return 20 * np.log10(max(1e-9, float(np.sqrt(np.mean(np.square(signal))))))


def _whistle(signal):
    """The largest STFT peak above 400 Hz over the band's median (the user's no-whistle rule: over ~2000 is a whistle)."""
    import numpy as np
    from scipy.signal import stft
    mono = signal if signal.ndim == 1 else signal.mean(axis=1)
    f, _, z = stft(mono, fs=RATE, nperseg=4096)
    mag = np.abs(z)[f > 400]
    return float(mag.max() / max(1e-12, np.median(mag)))


def _fade(signal, seconds):
    import numpy as np
    n = min(len(signal) // 2, int(RATE * seconds))
    if n <= 0:
        return signal
    ramp = np.linspace(0, 1, n)
    shape = (n,) + (1,) * (signal.ndim - 1)
    signal[:n] *= ramp.reshape(shape)
    signal[-n:] *= ramp[::-1].reshape(shape)
    return signal


def _loop(signal, overlap):
    """A seamless loop: the last `overlap` seconds crossfaded (equal power) into the first, and dropped from the end."""
    import numpy as np
    n = int(RATE * overlap)
    head, tail = signal[:n], signal[-n:]
    t = np.linspace(0, np.pi / 2, n).reshape((n,) + (1,) * (signal.ndim - 1))
    joined = head * np.sin(t) + tail * np.cos(t)
    return np.concatenate([joined, signal[n:-n]], axis=0)


def _envelope(signal, window=0.02):
    import numpy as np
    n = max(1, int(RATE * window))
    power = np.convolve(np.square(signal), np.ones(n) / n, mode="same")
    return 10 * np.log10(power + 1e-12)


def _event(signal, pad=0.15, floor_db=-38.0, longest=6.0):
    """The take's main event: from just before it rises above `floor_db` (relative to its peak) to where it has died away."""
    import numpy as np
    env = _envelope(signal)
    peak = env.max()
    above = np.where(env > peak + floor_db)[0]
    start = max(0, above[0] - int(RATE * pad))
    end = min(len(signal), above[-1] + int(RATE * pad * 2))
    end = min(end, start + int(RATE * longest))
    return signal[start:end].copy()


def _steps(signal, length=0.42, spacing=0.32, keep=8):
    """Each step cut out of a walking take: onsets on the envelope, at least `spacing` apart, the `keep` most alike in loudness."""
    import numpy as np
    env = _envelope(signal, 0.01)
    rise = np.diff(env, prepend=env[0])
    gap = int(RATE * spacing)
    peaks, last = [], -gap
    threshold = env.max() - 26.0
    for i in np.argsort(-rise):
        if rise[i] < 1.5:
            break
        if env[min(len(env) - 1, i + int(RATE * 0.03))] < threshold:
            continue
        if all(abs(i - p) >= gap for p in peaks):
            peaks.append(i)
        if len(peaks) >= keep * 3:
            break
    peaks.sort()
    cuts = []
    for p in peaks:
        start = max(0, p - int(RATE * 0.02))
        cut = signal[start:start + int(RATE * length)].copy()
        if len(cut) >= int(RATE * length * 0.8):
            cuts.append(cut)
    if not cuts:
        return []
    levels = np.array([_rms_db(c) for c in cuts])
    middle = np.median(levels)
    order = np.argsort(np.abs(levels - middle))
    return [cuts[i] for i in sorted(order[:keep])]


def _write(path, signal, peak=None, rms_db=None):
    import numpy as np
    from scipy.io import wavfile
    if rms_db is not None:
        signal = signal * (10 ** ((rms_db - _rms_db(signal)) / 20))
    if peak is not None:
        signal = signal / max(1e-9, np.abs(signal).max()) * peak
    top = np.abs(signal).max()
    if top > 0.98:
        signal = signal / top * 0.98
    os.makedirs(os.path.dirname(path), exist_ok=True)
    wavfile.write(path, RATE, (signal * 32767).astype(np.int16))
    return signal


def process(names):
    import numpy as np
    report = []
    for name in names:
        folder, kind, seconds, takes, prompt = SOUNDS[name]
        paths = [os.path.join(RAW, f"{name}_v{t + 1}.wav") for t in range(takes)]
        paths = [p for p in paths if os.path.exists(p)]
        if not paths:
            report.append(f"{name}: no takes")
            continue
        target = os.path.join(OUT, folder)
        if kind == "bed":
            left, right = (_highpass(_load(p)) for p in (paths + paths)[:2])
            n = min(len(left), len(right))
            stereo = np.stack([left[:n], right[:n]], axis=1)
            # The first second of a take can still be settling: dropped before the loop is made.
            stereo = _loop(stereo[int(RATE * 1.0):], 4.0)
            written = _write(os.path.join(target, f"{name}.wav"), stereo, rms_db=-26.0)
            report.append(f"{name}: bed {len(written) / RATE:.1f} s, rms {_rms_db(written):.1f} dB, whistle x{_whistle(written):.0f}")
        elif kind == "shot":
            for t, p in enumerate(paths):
                cut = _fade(_event(_highpass(_load(p))), 0.03)
                written = _write(os.path.join(target, f"{name}_{t + 1}.wav"), cut, peak=0.7)
                report.append(f"{name}_{t + 1}: {len(written) / RATE:.2f} s, rms {_rms_db(written):.1f} dB, whistle x{_whistle(written):.0f}")
        else:
            index = 0
            for p in paths:
                for cut in _steps(_highpass(_load(p), 60.0)):
                    index += 1
                    written = _write(os.path.join(target, f"{name}_{index}.wav"), _fade(cut, 0.012), peak=0.6)
            report.append(f"{name}: {index} cuts")
    print("\n".join(report))


if __name__ == "__main__":
    if len(sys.argv) < 2 or sys.argv[1] not in ("generate", "process"):
        print(__doc__)
        sys.exit(1)
    wanted = sys.argv[2:] or list(SOUNDS)
    unknown = [n for n in wanted if n not in SOUNDS]
    if unknown:
        sys.exit(f"unknown: {unknown}")
    (generate if sys.argv[1] == "generate" else process)(wanted)
