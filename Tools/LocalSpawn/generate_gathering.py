"""Generate Safe Haven's solid gathering models with LocalSpawn and make them game-ready (see gathering_manifest.json).

Phases (each skips what already exists, so it can be stopped and rerun):
  images  concept image per look (LocalSpawn image model, style "3D source (flat lit)")   -> <out>/<asset>/<key>/concept.png
  3d      Hunyuan3D shape + UltraTex texture, game quality, 2048 maps, high normal map  -> <out>/<asset>/<key>/*.glb
  sprites leaf / flower / grass sprites for the leaf-card plants (transparent PNGs)       -> <out>/Sprites/<asset>/<name>.png
          (Tools/Blender/plant_nodes.py lays them out on each plant's sprite sheet)
  ingest  Tools/Blender/ingest_generated.py per asset: atlas, LODs, collider, height/moss masks, glow
          -> Assets/_Project/Art/Gathering/<asset>/ (then run AgentScripts/BuildGatheringModels.cs in Unity)

Run with LocalSpawn's Python (Unity closed: generation needs ~13 GB RAM):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/generate_gathering.py [images|3d|sprites|ingest|all] [asset ...]
"""
import glob
import json
import os
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
AGENT = r"D:\ComfyUI\agent"
PYTHON = r"D:\ComfyUI\.venv\Scripts\python.exe"
BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
OUT = os.path.join(HERE, "Gathering")


def run(cmd, cwd=None, log=None):
    print(">", " ".join(f'"{c}"' if " " in c else c for c in cmd), flush=True)
    with open(log, "a", encoding="utf-8") if log else open(os.devnull, "w") as sink:
        result = subprocess.run(cmd, cwd=cwd, stdout=sink if log else None, stderr=subprocess.STDOUT if log else None)
    if result.returncode != 0:
        print(f"  failed ({result.returncode})" + (f", see {log}" if log else ""), flush=True)
    return result.returncode == 0


def looks(manifest, wanted):
    for asset in manifest["assets"]:
        if wanted and asset["asset"] not in wanted:
            continue
        for look in asset["looks"]:
            folder = os.path.join(OUT, asset["asset"], look["key"])
            os.makedirs(folder, exist_ok=True)
            yield asset, look, folder


def images(manifest, wanted):
    for asset, look, folder in looks(manifest, wanted):
        concept = os.path.join(folder, "concept.png")
        if os.path.exists(concept):
            continue
        prompt = f"{look['prompt']} {manifest['common']}"
        ok = run([PYTHON, "generate_image.py", "--style", "3D source (flat lit)", "--width", "1024", "--height", "1024",
                  "--seed", str(look["seed"]), "--out-dir", folder, "--name", "concept", prompt], cwd=AGENT,
                 log=os.path.join(folder, "image.log"))
        made = sorted(glob.glob(os.path.join(folder, "concept_*.png")))
        if ok and made:
            os.replace(made[-1], concept)


def shapes(manifest, wanted):
    for asset, look, folder in looks(manifest, wanted):
        concept = os.path.join(folder, "concept.png")
        if not os.path.exists(concept) or glob.glob(os.path.join(folder, "**", "*.glb"), recursive=True):
            continue
        run([PYTHON, "generate_3d.py", "--image", concept, "--model", "hunyuan3d", "--quality", "game", "--ultratex",
             "--texture-size", "2048", "--normal-map", "high", "--target", "unity", "--format", "glb", "--out-dir", folder,
             "--name", "model", "--seed", str(look["seed"])], cwd=AGENT, log=os.path.join(folder, "3d.log"))


def sprites(manifest, wanted):
    for group in manifest.get("sprites", []):
        if wanted and group["asset"] not in wanted:
            continue
        folder = os.path.join(OUT, "Sprites", group["asset"])
        os.makedirs(folder, exist_ok=True)
        for sprite in group["sprites"]:
            target = os.path.join(folder, sprite["name"] + ".png")
            if os.path.exists(target):
                continue
            ok = run([PYTHON, "generate_image.py", "--style", "None", "--transparent", "--width", "1024", "--height", "1024",
                      "--seed", str(sprite["seed"]), "--out-dir", folder, "--name", sprite["name"],
                      f"{sprite['prompt']} {manifest['sprite_common']}"], cwd=AGENT, log=os.path.join(folder, "sprites.log"))
            made = sorted(p for p in glob.glob(os.path.join(folder, sprite["name"] + "_*.png")))
            if ok and made:
                os.replace(made[-1], target)


def ingest(manifest, wanted):
    for asset in manifest["assets"]:
        if wanted and asset["asset"] not in wanted:
            continue
        glbs, turns = [], []
        for look in asset["looks"]:
            found = sorted(glob.glob(os.path.join(OUT, asset["asset"], look["key"], "**", "*.glb"), recursive=True))
            if found:
                glbs.append(found[-1])
                turns += ["--turn", str(look.get("turn", 0))]
        if not glbs:
            continue
        glow = asset.get("glow")
        glow_args = ["--glow-json", json.dumps(glow)] if glow else []
        run([BLENDER, "-b", "--factory-startup", "--python", os.path.join(ROOT, "Tools", "Blender", "ingest_generated.py"),
             "--", ROOT, asset["asset"], str(asset["width"])] + glow_args + turns + glbs,
            log=os.path.join(OUT, asset["asset"], "ingest.log"))


def main():
    phase = sys.argv[1] if len(sys.argv) > 1 else "all"
    wanted = set(sys.argv[2:])
    with open(os.path.join(HERE, "gathering_manifest.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    if phase in ("images", "all"):
        images(manifest, wanted)
    if phase in ("3d", "all"):
        shapes(manifest, wanted)
    if phase in ("sprites", "all"):
        sprites(manifest, wanted)
    if phase in ("ingest", "all"):
        ingest(manifest, wanted)
    print("GATHERING DONE", phase, flush=True)


if __name__ == "__main__":
    main()
