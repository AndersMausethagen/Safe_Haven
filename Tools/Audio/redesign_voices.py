"""Designs the cast's voices again from their long reference texts (CAST in dialogue_voices.py), keeping each one as
close as it can to the voice it replaces.

  design   For each voice, one design per seed in its `seeds`, with LocalSpawn's voice designer (Qwen3-TTS
           VoiceDesign) -> Tools/Audio/Voices/raw/candidates/<voice>/<seed>.wav. Nothing is replaced yet.
  pick     Each candidate is written down again by a speech recogniser (it must say all its words: the portal's lines
           are cloned from the clip and its text together), its pitch is measured (it must be a woman's or a man's as
           the cast says), and it is compared by a speaker-recognition model with the voice it replaces (the one first
           chosen by ear). The closest that passes is kept -> Tools/Audio/Voices/raw/candidates/choices.json.
           Runs in the qwen-tts Python (it has the models); `all` starts it there by itself.
  install  The kept candidates become the voices, in LocalSpawn and in Tools/Audio/Voices/<voice>/. The voice each
           replaces is kept in Tools/Audio/Voices/raw/previous/<voice>/ (not committed; git has it too).
  all      The three in order.

Then speak the lines again: `dialogue_voices.py redo`, and in Unity BuildDialogueVoices.Apply (or Safe Haven >
Voice Lines). Run with LocalSpawn's Python:
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/Audio/redesign_voices.py all [voice ...]
"""
import json
import os
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import dialogue_voices as dv  # noqa: E402

AGENT = dv.AGENT
QWEN_PYTHON = r"D:\ComfyUI\qwen-tts\.venv\Scripts\python.exe"
QWEN_DIR = r"D:\ComfyUI\qwen-tts"
CANDIDATES = os.path.join(dv.RAW, "candidates")
PREVIOUS = os.path.join(dv.RAW, "previous")
CHOICES = os.path.join(CANDIDATES, "choices.json")
LOCALSPAWN_VOICES = r"D:\ComfyUI\output\agent\voices"


def roles(only):
    return [(speaker, role) for speaker, role in dv.CAST.items() if not only or role["voice"] in only]


def design(only):
    sys.path.insert(0, AGENT)
    import generate_voice as gv
    items = []
    for _, role in roles(only):
        for seed in role["seeds"]:
            if os.path.isfile(os.path.join(CANDIDATES, role["voice"], f"{seed}.wav")):
                continue  # designed before: a seed always gives the same clip
            items.append({"description": role["description"], "text": role["reference"], "seed": seed,
                          "output": os.path.join(CANDIDATES, role["voice"], f"{seed}.wav")})
    if not items:
        print("Every candidate is designed already.")
        return
    print(f"Designing {len(items)} candidates.", flush=True)
    result = gv.run_worker({"mode": "design", "language": "English", "items": items}, timeout=3600,
                           progress=lambda fraction, message: print(f"[{fraction:4.0%}] {message}", flush=True))
    for made in result["outputs"]:
        print(f"  {os.path.relpath(made['path'], CANDIDATES):24s} {made['seconds']:5.1f} s")


def compared_with(role):
    """The voice a design replaces: the one kept before (raw/previous) or the one in place now."""
    old = role.get("was") or role["voice"]
    for folder in (os.path.join(PREVIOUS, old), os.path.join(dv.VOICES, old)):
        if os.path.isfile(os.path.join(folder, "reference.wav")):
            return os.path.join(folder, "reference.wav")
    return None


def pick(only):
    """In the qwen-tts Python: words, pitch and likeness of every candidate; the best of each voice."""
    sys.path.insert(0, QWEN_DIR)
    import librosa
    import soundfile
    import torch
    import speak as common
    from transformers import AutoFeatureExtractor, WavLMForXVector
    extractor = AutoFeatureExtractor.from_pretrained("microsoft/wavlm-base-plus-sv")
    model = WavLMForXVector.from_pretrained("microsoft/wavlm-base-plus-sv").cuda().eval()
    listener = common.Listener("English")

    def embed(path):
        wav, _ = librosa.load(path, sr=16000)
        inputs = extractor([wav], sampling_rate=16000, return_tensors="pt", padding=True)
        with torch.no_grad():
            e = model(**{k: v.cuda() for k, v in inputs.items()}).embeddings
        return torch.nn.functional.normalize(e, dim=-1)[0].cpu()

    choices = json.load(open(CHOICES, encoding="utf-8")) if os.path.isfile(CHOICES) else {}
    for speaker, role in roles(only):
        old = compared_with(role)
        target = embed(old) if old else None
        rows = []
        for seed in role["seeds"]:
            path = os.path.join(CANDIDATES, role["voice"], f"{seed}.wav")
            if not os.path.isfile(path):
                continue
            wav, rate = soundfile.read(path, dtype="float32")
            heard = listener.hear(wav, rate) or ""
            pitch = dv.pitch_of(path)
            rows.append(dict(seed=seed, path=path, seconds=round(len(wav) / rate, 2), pitch=round(pitch),
                             says=common.says(heard, role["reference"]), heard=heard,
                             right=pitch > 0 and (pitch >= dv.SPLIT_HZ) == role["woman"],
                             like=round(float(embed(path) @ target), 3) if target is not None else 0.0))
        passing = [r for r in rows if r["says"] and r["right"]] or [r for r in rows if r["right"]] or rows
        best = max(passing, key=lambda r: r["like"])
        best["passed"] = bool(best["says"] and best["right"])
        choices[role["voice"]] = dict(best, candidates=rows, speaker=speaker)
        print(f"{role['voice']:22s} kept seed {best['seed']} ({best['seconds']} s, {best['pitch']} Hz, like the old "
              f"{best['like']:.2f}{'' if best['passed'] else ', FAILED A CHECK'}); others: "
              + ", ".join(f"{r['seed']} {r['like']:.2f}{'' if r['says'] else ' words'}{'' if r['right'] else ' pitch'}"
                          for r in rows if r is not best), flush=True)
    os.makedirs(CANDIDATES, exist_ok=True)
    json.dump(choices, open(CHOICES, "w", encoding="utf-8"), indent=1, ensure_ascii=False)


def install(only):
    import time
    choices = json.load(open(CHOICES, encoding="utf-8"))
    for _, role in roles(only):
        chosen = choices.get(role["voice"])
        if not chosen:
            print(f"{role['voice']}: nothing chosen, left as it is")
            continue
        for place in (os.path.join(LOCALSPAWN_VOICES, role["voice"]), os.path.join(dv.VOICES, role["voice"])):
            old_clip = os.path.join(place, "reference.wav")
            keep = os.path.join(PREVIOUS, role["voice"])
            if os.path.isfile(old_clip) and not os.path.isdir(keep):
                shutil.copytree(place, keep)
            os.makedirs(place, exist_ok=True)
            shutil.copyfile(chosen["path"], old_clip)
            record = {"name": role["voice"], "description": role["description"], "reference_text": role["reference"],
                      "language": "English", "seed": chosen["seed"], "source": "designed",
                      "model": "Qwen3-TTS-12Hz-1.7B-VoiceDesign", "created": time.strftime("%Y-%m-%d %H:%M:%S"),
                      "seconds": chosen["seconds"], "sample_rate": 24000}
            with open(os.path.join(place, "voice.json"), "w", encoding="utf-8") as stream:
                json.dump(record, stream, indent=2, ensure_ascii=False)
        print(f"{role['voice']}: now seed {chosen['seed']}, {chosen['seconds']} s")


if __name__ == "__main__":
    step = sys.argv[1] if len(sys.argv) > 1 else "all"
    only = [a for a in sys.argv[2:] if not a.startswith("--")]
    if step in ("design", "all"):
        design(only)
    if step == "pick":
        pick(only)
    elif step == "all":
        env = dict(os.environ, PYTHONIOENCODING="utf-8", HF_HUB_OFFLINE="1", TRANSFORMERS_OFFLINE="1")
        subprocess.run([QWEN_PYTHON, os.path.abspath(__file__), "pick"] + only, check=True, env=env)
    if step in ("install", "all"):
        install(only)
