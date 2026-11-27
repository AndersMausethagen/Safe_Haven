"""The start menu's living picture (MenuBackdrop): cuts the chosen painting into layers and makes the portal gate's light.

The painting is the user's pick of 2026-11-27 (Z-Image, seed 2711; its prompt in HavenArt/main_menu_prompt.txt), kept as
HavenArt/main_menu_painting.png. Steps:
  1. backstory_layers.split: five layers by depth (sky with the trolls, houses, portal and fire, grass, stones).
  2. The back layer is the painting itself wherever no nearer layer covers it: the depth cut leaves a few spots to no layer, and the
     back layer's fill showed there.
  3. The portal gate: the opening in the portal layer (a flat dark slab in the painting) filled with a soft gold light, brighter
     toward the door, with a faint bloom onto the stones. Drawn additive in the backdrop, breathing (its Flicker).
The backdrop asset (Data/Cinematics/MainMenuBackdrop.asset) places these with glows, mist, smoke and specks; change it there.

Run from this folder with ComfyUI's venv (the depth model): D:\\ComfyUI\\.venv\\Scripts\\python.exe main_menu_layers.py
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
PAINTING = os.path.join(HERE, "HavenArt", "main_menu_painting.png")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "MainMenu"))
NEAR = ["menu_1_far", "menu_2_middle", "menu_3_near", "menu_4_front"]
# Where to look for the portal's opening (pixels at 1920x1088): the arch above the door.
GATE_BOX = (1320, 380, 1640, 560)


def split():
    sys.path.insert(0, HERE)
    import backstory_layers
    return backstory_layers.split(PAINTING, OUT, "menu", 5)


def back_is_the_painting():
    painting = np.asarray(Image.open(PAINTING).convert("RGB")).astype(np.float32)
    back = np.asarray(Image.open(os.path.join(OUT, "menu_0_sky.png")).convert("RGBA")).astype(np.float32)
    covered = np.zeros(painting.shape[:2], np.float32)
    for name in NEAR:
        alpha = np.asarray(Image.open(os.path.join(OUT, name + ".png")).convert("RGBA"))[..., 3].astype(np.float32) / 255
        covered = 1 - (1 - covered) * (1 - alpha)
    keep = np.clip((0.98 - covered) / 0.1, 0, 1)[..., None]
    rgb = painting * keep + back[..., :3] * (1 - keep)
    Image.fromarray(np.dstack([rgb, np.full(covered.shape, 255, np.float32)]).astype(np.uint8), "RGBA").save(os.path.join(OUT, "menu_0_sky.png"))


def gate():
    from scipy import ndimage
    portal = np.asarray(Image.open(os.path.join(OUT, "menu_2_middle.png")).convert("RGBA"))[..., 3].astype(np.float32) / 255
    height, width = portal.shape
    x0, y0, x1, y1 = GATE_BOX
    hole = np.zeros_like(portal, bool)
    hole[y0:y1, x0:x1] = portal[y0:y1, x0:x1] < 0.5
    labels, count = ndimage.label(hole)
    sizes = ndimage.sum(hole, labels, range(1, count + 1))
    opening = labels == (1 + int(np.argmax(sizes)))
    ys, _ = np.nonzero(opening)
    soft = ndimage.gaussian_filter(opening.astype(np.float32), 3)
    rows = np.mgrid[0:height, 0:width][0].astype(np.float32)
    toward_door = np.clip((rows - ys.min()) / (ys.max() - ys.min() + 1), 0, 1)
    alpha = np.clip(soft * (0.45 + 0.55 * toward_door) + ndimage.gaussian_filter(opening.astype(np.float32), 18) * 0.5, 0, 1)
    rgb = np.zeros((height, width, 3), np.float32)
    rgb[...] = (255, 214, 140)
    image = Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), "RGBA")
    image.resize((width // 2, height // 2), Image.LANCZOS).save(os.path.join(OUT, "menu_portal_gate.png"))


if __name__ == "__main__":
    print(split())
    back_is_the_painting()
    gate()
    print("done:", OUT)
