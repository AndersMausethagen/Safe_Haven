"""Tiling textures for the building kit that Poly Haven has no scan of, made with LocalSpawn's image model and turned into
a scan-like set beside the others in Assets/_Project/Art/Haven/Kit/Textures (<name>_diff.jpg, _nor_gl.jpg, _arm.jpg,
_disp.png), which building_kit.py and AgentScripts/BuildStructureModels.cs (KitMaterials) use like the scans.

- hay: loose dry hay and straw, for stalls, racks, heaps and barns (the Stable).

The image is made seamless by cross-fading it with itself shifted half a tile (a soft cross mask hides the seam), then
the maps are read off it: height from brightness (straws in the light are high), the normal from the height's slopes,
occlusion from how far a pixel sits below its neighbourhood, roughness high, no metal.

Run with LocalSpawn's Python (Unity may stay open: the image model is VRAM only):
  D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/kit_textures.py [name ...] [--force]
"""
import glob
import os
import subprocess
import sys

import numpy
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
AGENT = r"D:\ComfyUI\agent"
PYTHON = r"D:\ComfyUI\.venv\Scripts\python.exe"
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Haven", "Kit", "Textures")
WORK = os.path.join(HERE, "KitTextures")
SIZE = 1024

TEXTURES = {
    "hay": dict(seed=9101, prompt="Seamless tileable texture seen straight down from above and filling the whole frame edge to edge: a "
                                  "thick even layer of loose dry golden hay and pale straw, thin stalks lying crossed in every direction, "
                                  "a few darker brown dried grass blades among them, evenly lit, photographic, high detail. No ground "
                                  "showing, no shadows from outside, no objects, no border.", strength=6.0),
}


def generate(name, spec):
    os.makedirs(WORK, exist_ok=True)
    source = os.path.join(WORK, f"{name}.png")
    if os.path.exists(source) and "--force" not in sys.argv:
        return source
    subprocess.run([PYTHON, "generate_image.py", "--style", "None", "--width", str(SIZE), "--height", str(SIZE), "--seed", str(spec["seed"]),
                    "--out-dir", WORK, "--name", name, spec["prompt"]], cwd=AGENT, check=True)
    made = sorted(glob.glob(os.path.join(WORK, f"{name}_*.png")))
    os.replace(made[-1], source)
    subprocess.run([PYTHON, "generate_image.py", "--stop"], cwd=AGENT)
    return source


def seamless(image):
    """The image cross-faded with itself shifted half a tile: the shifted copy covers the edges (where the original's
    seam would be), the original covers the middle (where the shifted copy's seam is)."""
    a = numpy.asarray(image, dtype=numpy.float32)
    h, w = a.shape[:2]
    shifted = numpy.roll(numpy.roll(a, h // 2, axis=0), w // 2, axis=1)
    y = numpy.abs(numpy.linspace(-1.0, 1.0, h))[:, None]
    x = numpy.abs(numpy.linspace(-1.0, 1.0, w))[None, :]
    edge = numpy.clip((numpy.maximum(x, y) - 0.55) / 0.35, 0.0, 1.0)  # 0 in the middle, 1 at the border
    edge = edge * edge * (3 - 2 * edge)
    mixed = a * (1 - edge[..., None]) + shifted * edge[..., None]
    return Image.fromarray(numpy.clip(mixed, 0, 255).astype(numpy.uint8))


def maps(name, colour, strength):
    rgb = numpy.asarray(colour, dtype=numpy.float32) / 255.0
    height = rgb @ numpy.array([0.3, 0.59, 0.11], dtype=numpy.float32)
    height = (height - height.min()) / max(1e-6, height.max() - height.min())
    # Slopes, wrapping round the edges so the normal map tiles too.
    dx = (numpy.roll(height, -1, axis=1) - numpy.roll(height, 1, axis=1)) * 0.5 * strength
    dy = (numpy.roll(height, -1, axis=0) - numpy.roll(height, 1, axis=0)) * 0.5 * strength
    normal = numpy.dstack([-dx, dy, numpy.ones_like(height)])  # OpenGL: green up
    normal /= numpy.linalg.norm(normal, axis=2, keepdims=True)
    nor = ((normal * 0.5 + 0.5) * 255).astype(numpy.uint8)
    blurred = numpy.asarray(Image.fromarray((height * 255).astype(numpy.uint8)).filter(ImageFilter.GaussianBlur(6)), dtype=numpy.float32) / 255.0
    occlusion = numpy.clip(1.0 - (blurred - height) * 2.2, 0.35, 1.0)
    roughness = numpy.clip(0.82 + (1 - height) * 0.12, 0.0, 1.0)
    arm = numpy.dstack([occlusion, roughness, numpy.zeros_like(height)])
    colour.save(os.path.join(OUT, f"{name}_diff.jpg"), quality=92)
    Image.fromarray(nor).save(os.path.join(OUT, f"{name}_nor_gl.jpg"), quality=95)
    Image.fromarray((arm * 255).astype(numpy.uint8)).save(os.path.join(OUT, f"{name}_arm.jpg"), quality=95)
    Image.fromarray((height * 65535).astype(numpy.uint16)).save(os.path.join(OUT, f"{name}_disp.png"))
    print("KIT TEXTURE", name, "->", OUT)


def main():
    wanted = [a for a in sys.argv[1:] if not a.startswith("--")] or list(TEXTURES)
    for name in wanted:
        spec = TEXTURES[name]
        colour = seamless(Image.open(generate(name, spec)).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS))
        maps(name, colour, spec["strength"])


if __name__ == "__main__":
    main()
