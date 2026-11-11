"""Paintings for the interface with LocalSpawn: tutorial pictures, creature and resident portraits, raid results.

Each set has a list of what to paint (a JSON file beside this script: key -> prompt, or key -> {"style", "prompt"}) and a
folder the finished paintings go to, which the Unity builders read:

  tutorials   tutorial_art.json        1280x720   -> TutorialArt/<slug>.png         AgentScripts/BuildJournal.cs (Apply)
  creatures   creature_portraits.json  768x768    -> CreaturePortraits/<asset>.png  BuildJournal.cs (PaintedPortraits)
  residents   resident_portraits.json  768x768    -> ResidentPortraits/<asset>.png  AgentScripts/BuildUIArt.cs (Residents)
  raids       raid_art.json            1920x1088  -> RaidArt/<asset>.png            AgentScripts/BuildUIArt.cs (Raids)

  generate_ui_art.py SET [key ...] [--force] [--model NAME]

Paints what has no painting yet (--force paints again; keys listed paint only those). Portraits are scaled to 512.
Run with LocalSpawn's Python (the image model runs on the GPU; Unity can stay open):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/generate_ui_art.py tutorials
"""
import json
import os
import re
import shutil
import subprocess
import sys
import time
import zlib

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
AGENT = r"D:\ComfyUI\agent"
PYTHON = r"D:\ComfyUI\.venv\Scripts\python.exe"
STYLES = {"home": "Safe Haven - Home", "raid": "Safe Haven - Raid"}

# set -> (list, folder, width, height, default style, size it is saved at (None: as painted), slug the key)
SETS = {
    "tutorials": ("tutorial_art.json", "TutorialArt", 1280, 720, "home", None, True),
    "creatures": ("creature_portraits.json", "CreaturePortraits", 768, 768, "raid", 512, False),
    "residents": ("resident_portraits.json", "ResidentPortraits", 768, 768, "home", 512, False),
    "raids": ("raid_art.json", "RaidArt", 1920, 1088, "raid", None, False),
}


def slug(name):
    return re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")


def paint(key, prompt, style, width, height, model, target, size):
    scratch = os.path.join(os.path.dirname(target), "_new")
    shutil.rmtree(scratch, ignore_errors=True)
    os.makedirs(scratch, exist_ok=True)
    prompt_file = os.path.join(scratch, "prompt.txt")
    with open(prompt_file, "w", encoding="utf-8") as stream:
        stream.write(prompt)
    started = time.monotonic()
    draft = model.endswith("-draft")
    result = subprocess.run([PYTHON, "generate_image.py", "--prompt-file", prompt_file, "--style", style,
                             "--model", model[:-6] if draft else model] + (["--draft"] if draft else []) + [
                             "--width", str(width), "--height", str(height), "--seed", str(zlib.crc32(key.encode()) % 100000),
                             "--name", "painting", "--out-dir", scratch, "--keep-loaded", "--timeout", "900"],
                            cwd=AGENT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=1800)
    made = [f for f in os.listdir(scratch) if f.endswith(".png")]
    if not made:
        raise RuntimeError(f"{key}: nothing came back (exit {result.returncode}).\n{(result.stdout + result.stderr)[-1500:]}")
    image = Image.open(os.path.join(scratch, made[0])).convert("RGB")
    if size:
        image = image.resize((size, size), Image.LANCZOS)
    image.save(target)
    shutil.rmtree(scratch, ignore_errors=True)
    return time.monotonic() - started


def main():
    valued = {"--model"}
    args = [a for i, a in enumerate(sys.argv[1:], 1) if not a.startswith("--") and sys.argv[i - 1] not in valued]
    if not args or args[0] not in SETS:
        print(__doc__)
        return
    listed, folder, width, height, default_style, size, slugged = SETS[args[0]]
    model = sys.argv[sys.argv.index("--model") + 1] if "--model" in sys.argv else "qwen-image"
    wanted = {k: v for k, v in json.load(open(os.path.join(HERE, listed), encoding="utf-8")).items() if not k.startswith("_")}
    chosen = [k for k in args[1:] if k in wanted] or list(wanted)
    out = os.path.join(HERE, folder)
    os.makedirs(out, exist_ok=True)
    done = 0
    try:
        for key in chosen:
            target = os.path.join(out, (slug(key) if slugged else key) + ".png")
            if os.path.isfile(target) and "--force" not in sys.argv:
                continue
            entry = wanted[key]
            prompt, style = (entry, default_style) if isinstance(entry, str) else (entry["prompt"], entry.get("style", default_style))
            took = paint(key, prompt, STYLES[style], width, height, model, target, size)
            done += 1
            print(f"[{done}] {key:34s} {took:5.1f} s", flush=True)
        print(f"{done} {args[0]} painted with {model}.")
    finally:
        subprocess.run([PYTHON, "generate_image.py", "--stop"], cwd=AGENT, capture_output=True, timeout=180)


if __name__ == "__main__":
    main()
