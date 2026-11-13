"""The raid results paintings, moving: a LocalSpawn video of each (result_motion.json), made into a seamless loop for Unity.

  result_motion.py make KEY [--quality High] [--fast]   paint the clip with MiniMax H3 (start = end = the painting), then process
  result_motion.py process KEY RAW.mp4                  only process a clip already made

Processing: the last frame (the painting again) is dropped and the last half second is blended into the first, so the
loop has no seam; the picture goes to Assets/_Project/Art/UI/Raids/<KEY>_motion.mp4 (H.264, no sound). The model's own
sound, its last half second blended into its start, goes to Tools/LocalSpawn/ResultMotion/<KEY>_h3.wav: it is thin (32 kHz,
little above 1.5 kHz), so Tools/Audio/result_ambience.py builds the place's sound and may lay it underneath. The raid's
RaidDefinition points at the picture and the finished sound (ResultMotion, ResultSound).

Run with LocalSpawn's Python (PyAV, the video model on the GPU; Unity can stay open):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/result_motion.py make MistyWoods_extracted
"""
import argparse
import json
import os
import subprocess
import sys
import wave

import av
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
AGENT = r"D:\ComfyUI\agent"
PYTHON = r"D:\ComfyUI\.venv\Scripts\python.exe"
VIDEO_OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "UI", "Raids")
SOUND_OUT = os.path.join(HERE, "ResultMotion")
# Long enough to hide the colour the model drifts by over a clip (about 7 of 255 at High).
FRAME_BLEND_SECONDS = 0.5
SOUND_BLEND_SECONDS = 0.5
# Ambience level: an even bed under the report, its loudest moments kept under the peak.
TARGET_RMS_DB = -24.0
PEAK_DB = -3.0


def entries():
    with open(os.path.join(HERE, "result_motion.json"), encoding="utf-8") as f:
        return {k: v for k, v in json.load(f).items() if not k.startswith("_")}


def make(key, quality, fast):
    entry = entries()[key]
    image = os.path.join(ROOT, entry["image"])
    out_dir = os.path.join(HERE, "ResultMotion", "raw")
    cmd = [PYTHON, os.path.join(AGENT, "generate_video.py"), image, entry["prompt"], "--model", "minimax-h3",
           "--end-image", image, "--seconds", str(entry.get("seconds", 7)), "--quality", quality,
           "--seed", str(entry.get("seed", 11)), "--out-dir", out_dir]
    if fast:
        cmd.append("--fast")
    run = subprocess.run(cmd, cwd=AGENT, capture_output=True, text=True)
    line = next((l for l in reversed(run.stdout.splitlines()) if l.startswith("{")), None)
    if run.returncode != 0 or line is None:
        sys.exit(f"generate_video.py failed:\n{run.stdout[-2000:]}\n{run.stderr[-2000:]}")
    raw = json.loads(line)["path"]
    process(key, raw)


def read(path):
    frames, chunks, rate, fps = [], [], None, None
    with av.open(path) as c:
        vs = c.streams.video[0]
        fps = float(vs.average_rate)
        frames = [f.to_ndarray(format="rgb24") for f in c.decode(vs)]
    with av.open(path) as c:
        if c.streams.audio:
            a = c.streams.audio[0]
            rate = a.rate
            for f in c.decode(a):
                data = f.to_ndarray()
                chunks.append(data if data.ndim == 2 else data[None, :])
    sound = np.concatenate(chunks, axis=1).T.astype(np.float32) if chunks else None  # (samples, channels)
    return frames, fps, sound, rate


def loop_frames(frames, fps):
    """Drops the closing frame (the painting again) and blends the tail into the head: no seam where it wraps."""
    frames = frames[:-1]
    k = max(1, round(FRAME_BLEND_SECONDS * fps))
    n = len(frames)
    out = list(frames[:n - k])
    # Only the blended frames in floats: a whole clip of them would be gigabytes.
    for i in range(k):
        w = (i + 1) / (k + 1)
        mix = frames[n - k + i].astype(np.float32) * (1 - w) + frames[i].astype(np.float32) * w
        out[i] = np.clip(mix + 0.5, 0, 255).astype(np.uint8)
    return out


def loop_sound(sound, rate):
    """Equal-power blend of the last half second into the first, then levelled; the result wraps without a click."""
    k = int(SOUND_BLEND_SECONDS * rate)
    n = len(sound)
    t = (np.arange(k) + 0.5) / k
    head = np.sin(t * np.pi / 2)[:, None]
    tail = np.cos(t * np.pi / 2)[:, None]
    out = sound[:n - k].copy()
    out[:k] = sound[n - k:] * tail + sound[:k] * head
    rms = float(np.sqrt((out ** 2).mean())) or 1e-6
    gain = 10 ** (TARGET_RMS_DB / 20) / rms
    peak = float(np.abs(out).max()) * gain
    limit = 10 ** (PEAK_DB / 20)
    if peak > limit:
        gain *= limit / peak
    return np.clip(out * gain, -1, 1), 20 * np.log10(gain)


def write_video(path, frames, fps):
    h, w = frames[0].shape[:2]
    with av.open(path, "w") as c:
        vs = c.add_stream("libx264", rate=int(round(fps)))
        vs.width, vs.height, vs.pix_fmt = w - w % 2, h - h % 2, "yuv420p"
        vs.options = {"crf": "17", "preset": "slow"}
        for img in frames:
            frame = av.VideoFrame.from_ndarray(np.ascontiguousarray(img[:vs.height, :vs.width]), format="rgb24")
            for p in vs.encode(frame):
                c.mux(p)
        for p in vs.encode():
            c.mux(p)


def write_wav(path, sound, rate):
    data = (sound * 32767).astype(np.int16)
    with wave.open(path, "wb") as w:
        w.setnchannels(data.shape[1])
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(data.tobytes())


def process(key, raw):
    frames, fps, sound, rate = read(raw)
    looped = loop_frames(frames, fps)
    os.makedirs(VIDEO_OUT, exist_ok=True)
    video = os.path.join(VIDEO_OUT, f"{key}_motion.mp4")
    write_video(video, looped, fps)
    report = {"raw": raw, "video": video, "frames": len(looped), "fps": fps, "seconds": round(len(looped) / fps, 2),
              "size": [int(looped[0].shape[1]), int(looped[0].shape[0])]}
    if sound is not None:
        os.makedirs(SOUND_OUT, exist_ok=True)
        level, gain_db = loop_sound(sound, rate)
        wav = os.path.join(SOUND_OUT, f"{key}_h3.wav")
        write_wav(wav, level, rate)
        report.update(sound=wav, sound_seconds=round(len(level) / rate, 2), rate=rate, gain_db=round(gain_db, 1))
    print(json.dumps(report))


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("make")
    m.add_argument("key")
    m.add_argument("--quality", default="High", choices=["Draft", "Standard", "High"])
    m.add_argument("--fast", action="store_true")
    r = sub.add_parser("process")
    r.add_argument("key")
    r.add_argument("raw")
    a = p.parse_args()
    if a.cmd == "make":
        make(a.key, a.quality, a.fast)
    else:
        process(a.key, a.raw)


if __name__ == "__main__":
    main()
