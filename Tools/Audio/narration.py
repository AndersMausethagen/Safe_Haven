"""Picks the best take of each narration line and makes it ready for the game.

The lines are spoken by LocalSpawn's Voice generator (Qwen3-TTS), several takes each, as <id>_t<n>.wav beside a
lines.json of {"id", "text"}. Nobody listens here, so a take is judged by what can be measured:

  1. It must say the words: every take is transcribed back (Whisper) and compared with the text.
  2. It must not end mid-word: the last 30 ms must have died away.
  3. Of the takes left, the one closest to the voice's usual pitch wins, then the slower one. A take whose pitch
     strays is the model drifting to another speaker.

The chosen take is trimmed to a short lead and tail, faded at both ends, and set to one loudness, so the lines sit
level in the mix. A report (choices and measurements of every take) is written beside the output.

Run with the Voice generator's Python, which has the speech libraries:
  D:\\ComfyUI\\qwen-tts\\.venv\\Scripts\\python.exe Tools/Audio/narration.py <takes folder> <output folder> [--prefix Backstory_]
"""
import argparse
import glob
import json
import os
import re
import sys
import warnings

import numpy as np

LEAD_S = 0.06
TAIL_S = 0.18
FADE_S = 0.012
TARGET_RMS_DB = -21.0   # of the spoken part
PEAK_LIMIT_DB = -1.5
SILENCE_DB = -42.0


def words(text):
    return re.sub(r"[^a-z ]", "", text.lower().replace("-", " ")).split()


def db(x):
    return 20 * np.log10(max(float(x), 1e-9))


def speech_bounds(x, rate):
    """First and last sample that is not silence."""
    loud = np.abs(x) > 10 ** (SILENCE_DB / 20)
    if not loud.any():
        return 0, len(x)
    return int(np.argmax(loud)), int(len(x) - np.argmax(loud[::-1]))


def measure(path, text, asr):
    import librosa
    import soundfile as sf
    x, rate = sf.read(path, dtype="float32")
    if x.ndim > 1:
        x = x.mean(axis=1)
    first, last = speech_bounds(x, rate)
    spoken = x[first:last]
    heard = asr({"raw": librosa.resample(x, orig_sr=rate, target_sr=16000), "sampling_rate": 16000})["text"].strip()
    f0, _, _ = librosa.pyin(spoken, fmin=60, fmax=400, sr=rate)
    f0 = f0[~np.isnan(f0)]
    end = x[max(0, len(x) - int(0.03 * rate)):]
    seconds = (last - first) / rate
    return {
        "path": path, "heard": heard, "says_the_words": words(heard) == words(text),
        "seconds": round(seconds, 2), "words_per_minute": round(len(words(text)) / max(seconds, 0.1) * 60),
        "pitch_hz": round(float(np.median(f0)), 1) if len(f0) else 0.0,
        "rms_db": round(db(np.sqrt(np.mean(spoken ** 2))), 1), "peak_db": round(db(np.abs(x).max()), 1),
        "lead_s": round(first / rate, 2), "tail_s": round((len(x) - last) / rate, 2),
        "ends_cleanly": bool(db(np.abs(end).max()) < -30.0),
    }


def choose(takes, usual_pitch):
    """The best take, or None when none says the words."""
    good = [t for t in takes if t["says_the_words"] and t["pitch_hz"] > 0]
    clean = [t for t in good if t["ends_cleanly"]] or good
    if not clean:
        return None
    # A semitone or so off the voice's usual pitch is normal expression; beyond that, further is worse.
    def cost(take):
        off = abs(12 * np.log2(take["pitch_hz"] / usual_pitch))
        return (round(max(0.0, off - 1.5), 1), take["words_per_minute"])
    return min(clean, key=cost)


def prepare(path, out_path):
    import soundfile as sf
    x, rate = sf.read(path, dtype="float32")
    if x.ndim > 1:
        x = x.mean(axis=1)
    first, last = speech_bounds(x, rate)
    spoken_rms = np.sqrt(np.mean(x[first:last] ** 2))
    start = max(0, first - int(LEAD_S * rate))
    stop = min(len(x), last + int(TAIL_S * rate))
    clip = x[start:stop].copy()
    # A take that ended sooner than the tail gets silence to make it up, so every line ends the same way.
    short = int(TAIL_S * rate) - (stop - last)
    if short > 0:
        clip = np.concatenate([clip, np.zeros(short, dtype=np.float32)])
    gain = 10 ** (TARGET_RMS_DB / 20) / max(spoken_rms, 1e-6)
    gain = min(gain, 10 ** (PEAK_LIMIT_DB / 20) / max(np.abs(clip).max(), 1e-6))
    clip *= gain
    fade = int(FADE_S * rate)
    clip[:fade] *= np.linspace(0, 1, fade)
    clip[-fade:] *= np.linspace(1, 0, fade)
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    sf.write(out_path, clip, rate, subtype="PCM_16")
    return {"seconds": round(len(clip) / rate, 2), "gain_db": round(db(gain), 1), "sample_rate": rate}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("takes", help="Folder with <id>_t<n>.wav and lines.json")
    parser.add_argument("out", help="Folder for the chosen, prepared lines")
    parser.add_argument("--prefix", default="", help="Put before each id in the file name")
    args = parser.parse_args(argv)

    warnings.filterwarnings("ignore")
    import torch
    from transformers import pipeline
    asr = pipeline("automatic-speech-recognition", model="openai/whisper-small.en",
                   device=0 if torch.cuda.is_available() else -1)
    with open(os.path.join(args.takes, "lines.json"), encoding="utf-8") as stream:
        lines = json.load(stream)
    measured = {}
    for line in lines:
        paths = sorted(glob.glob(os.path.join(args.takes, line["id"] + "_t*.wav")))
        measured[line["id"]] = [measure(path, line["text"], asr) for path in paths]
    pitches = [t["pitch_hz"] for takes in measured.values() for t in takes if t["says_the_words"] and t["pitch_hz"] > 0]
    usual = float(np.median(pitches))
    print(f"Usual pitch of the voice: {usual:.0f} Hz over {len(pitches)} takes that say their words.")

    report, missing = {"usual_pitch_hz": round(usual, 1), "lines": []}, []
    for line in lines:
        takes = measured[line["id"]]
        best = choose(takes, usual)
        entry = {"id": line["id"], "text": line["text"], "takes": takes, "chosen": None}
        if best is None:
            missing.append(line["id"])
            print(f"{line['id']}: NO TAKE SAYS THE WORDS  {[t['heard'] for t in takes]}")
        else:
            out_path = os.path.join(args.out, f"{args.prefix}{line['id']}.wav")
            entry["chosen"] = os.path.basename(best["path"])
            entry["output"] = dict(prepare(best["path"], out_path), path=out_path)
            said = sum(t["says_the_words"] for t in takes)
            print(f"{line['id']}: {entry['chosen']}  {entry['output']['seconds']:5.2f} s  {best['words_per_minute']:3d} wpm  "
                  f"{best['pitch_hz']:5.1f} Hz  ({said}/{len(takes)} takes say the words)  {line['text'][:44]}")
        report["lines"].append(entry)
    with open(os.path.join(args.takes, "narration_report.json"), "w", encoding="utf-8") as stream:
        json.dump(report, stream, indent=2)
    total = sum(e["output"]["seconds"] for e in report["lines"] if e["chosen"])
    print(f"{len(lines) - len(missing)} of {len(lines)} lines ready, {total:.1f} s of speech." +
          (f" Missing: {', '.join(missing)}" if missing else ""))
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
