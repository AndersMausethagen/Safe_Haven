"""Item icons with LocalSpawn, in place of the painted placeholders (Editor/Content/PlaceholderIconPainter).

What each icon shows is in item_icons.json (item id -> the object). Every icon is painted in LocalSpawn's
"Safe Haven - Item icon" style at its icon size (512 x 512), its background removed (BiRefNet), and fitted to the frame
with the same margin, so icons of every kind sit alike in an inventory slot. Each item gets its own file: several used
to share one (five meals shared the stew).

  test  [model ...]   A few items with each model named (default: z-image and qwen-image), for choosing a model
                      -> Tools/LocalSpawn/ItemIcons/_test/<model>/<id>.png
  all   --model NAME  Every item in item_icons.json that has no icon yet -> Tools/LocalSpawn/ItemIcons/<id>.png
                      (--force paints them again; item ids are listed to paint only those)

Then in Unity: AgentScripts/BuildItemIcons.cs, entry Apply, puts them in the game: into the item's own icon file where
no other item uses it (so content builders that load that file keep it), a new file where several shared one.
Run with LocalSpawn's Python, with Unity closed (the image model needs about 13 GB of RAM):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/generate_item_icons.py test
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/generate_item_icons.py all --model z-image
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
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
AGENT = r"D:\ComfyUI\agent"
PYTHON = r"D:\ComfyUI\.venv\Scripts\python.exe"
WANTED = os.path.join(HERE, "item_icons.json")
TEST = os.path.join(HERE, "ItemIcons", "_test")
ICONS = os.path.join(HERE, "ItemIcons")  # the finished icons; AgentScripts/BuildItemIcons.cs puts them in the game
STYLE = "Safe Haven - Item icon"
SIZE = 512  # LocalSpawn's "Icon 512x512"
# Of the frame, how much the object may fill at most (its longer side), so icons of every shape sit alike.
FILL = 0.86
TEST_ITEMS = ("copper_ingot", "ironmailhelm", "huntersstew", "wisppendant", "iron_sword", "berries")


def file_name(item_id):
    return re.sub(r"[^a-z0-9_]", "_", item_id.lower()) + ".png"


def seed_of(item_id):
    return zlib.crc32(item_id.encode()) % 100000


def fit(path, target):
    """Trim to what is not transparent and centre it in a square frame with an even margin."""
    image = Image.open(path).convert("RGBA")
    box = image.getchannel("A").point(lambda a: 255 if a > 24 else 0).getbbox()
    if box:
        image = image.crop(box)
    scale = SIZE * FILL / max(image.size)
    image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.LANCZOS)
    frame = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    frame.alpha_composite(image, ((SIZE - image.width) // 2, (SIZE - image.height) // 2))
    os.makedirs(os.path.dirname(target), exist_ok=True)
    frame.save(target)


def paint(item_id, prompt, model, target):
    scratch = os.path.join(HERE, "ItemIcons", "_new")
    shutil.rmtree(scratch, ignore_errors=True)
    os.makedirs(scratch, exist_ok=True)
    prompt_file = os.path.join(scratch, "prompt.txt")
    with open(prompt_file, "w", encoding="utf-8") as stream:
        stream.write(prompt + ".")
    started = time.monotonic()
    draft = model.endswith("-draft")  # e.g. qwen-image-draft: the model's fast sampling
    result = subprocess.run([PYTHON, "generate_image.py", "--prompt-file", prompt_file, "--style", STYLE,
                             "--model", model[:-6] if draft else model] + (["--draft"] if draft else []) + [
                             "--width", str(SIZE), "--height", str(SIZE), "--seed", str(seed_of(item_id)),
                             "--transparent", "--name", file_name(item_id)[:-4], "--out-dir", scratch, "--keep-loaded",
                             "--timeout", "900"],
                            cwd=AGENT, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=1800)
    made = [f for f in os.listdir(scratch) if f.endswith("_transparent.png")]
    if not made:
        raise RuntimeError(f"{item_id}: no icon came back (exit {result.returncode}).\n{(result.stdout + result.stderr)[-1500:]}")
    fit(os.path.join(scratch, made[0]), target)
    shutil.rmtree(scratch, ignore_errors=True)
    return time.monotonic() - started


def stop():
    subprocess.run([PYTHON, "generate_image.py", "--stop"], cwd=AGENT, capture_output=True, timeout=180)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    step = args[0] if args else "test"
    wanted = {k: v for k, v in json.load(open(WANTED, encoding="utf-8")).items() if not k.startswith("_")}
    try:
        if step == "test":
            for model in args[1:] or ["z-image", "qwen-image"]:
                for item_id in TEST_ITEMS:
                    took = paint(item_id, wanted[item_id], model, os.path.join(TEST, model, file_name(item_id)))
                    print(f"{model:11s} {item_id:16s} {took:5.1f} s", flush=True)
        elif step == "all":
            model = sys.argv[sys.argv.index("--model") + 1] if "--model" in sys.argv else "z-image"
            chosen = [i for i in args[1:] if i in wanted] or list(wanted)
            done = 0
            for item_id in chosen:
                target = os.path.join(ICONS, file_name(item_id))
                if os.path.isfile(target) and "--force" not in sys.argv:
                    continue
                took = paint(item_id, wanted[item_id], model, target)
                done += 1
                print(f"[{done}] {item_id:26s} {took:5.1f} s", flush=True)
            print(f"{done} icons painted with {model}.")
    finally:
        stop()


if __name__ == "__main__":
    main()
