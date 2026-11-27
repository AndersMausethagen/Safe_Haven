"""Procedural textures for the skills' effects (the ultimates' 2026-11-27 dressing): white shapes on transparent, coloured by the effect.

  Cracks     glowing cracks running out from a centre, branching: the ground split by an ultimate (a flat quad, additive)
  Beam       a soft vertical column with a bright core: a pillar of light or fire, stretched tall
  Flare      a star with long rays and a hot centre: the moment of impact
  Shockwave  a band with a bright leading edge and a soft trail inward: a wave rolling out over the ground
  Feather    a dark feather's silhouette: the raven's
  Snowflake  a six-armed crystal: the blizzard's

Run with any Python that has numpy and Pillow; writes into Assets/_Project/Art/Effects/Skills/Textures (512 px, RGBA).
  python Tools/Effects/skill_textures.py
"""
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Effects", "Skills", "Textures")
SIZE = 512


def grid():
    y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
    x = (x + 0.5) / SIZE * 2.0 - 1.0
    y = (y + 0.5) / SIZE * 2.0 - 1.0
    return x, y


def save(name, alpha, glow=None):
    """White with this alpha (0-1); glow adds a soft halo under it."""
    a = np.clip(alpha, 0.0, 1.0)
    if glow is not None:
        a = np.clip(np.maximum(a, glow), 0.0, 1.0)
    rgba = np.zeros((SIZE, SIZE, 4), np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (a * 255).astype(np.uint8)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name + ".png"))
    print("wrote", name)


def blur(alpha, radius):
    im = Image.fromarray((np.clip(alpha, 0, 1) * 255).astype(np.uint8), "L").filter(ImageFilter.GaussianBlur(radius))
    return np.asarray(im).astype(np.float32) / 255.0


def cracks(rng):
    """Jagged cracks out from the middle, each branching once or twice, thinner and fainter as they run; a hot core."""
    im = Image.new("L", (SIZE, SIZE), 0)
    draw = ImageDraw.Draw(im)
    c = SIZE / 2

    def crack(x, y, angle, length, width, depth):
        steps = int(length / 9)
        for _ in range(steps):
            angle += rng.uniform(-0.45, 0.45)
            nx = x + math.cos(angle) * 9
            ny = y + math.sin(angle) * 9
            draw.line([(x, y), (nx, ny)], fill=int(255 * min(1.0, 0.35 + width / 6)), width=max(1, int(width)))
            x, y = nx, ny
            width = max(1.0, width * 0.965)
            if depth < 2 and rng.random() < 0.06:
                crack(x, y, angle + rng.choice([-1, 1]) * rng.uniform(0.5, 1.0), length * rng.uniform(0.3, 0.55), width * 0.7, depth + 1)

    for i in range(11):
        a = i / 11 * math.tau + rng.uniform(-0.2, 0.2)
        crack(c, c, a, rng.uniform(170, 245), rng.uniform(8.0, 11.0), 0)
    line = np.asarray(im).astype(np.float32) / 255.0
    x, y = grid()
    r = np.sqrt(x * x + y * y)
    fade = np.clip(1.0 - (r - 0.7) / 0.3, 0.0, 1.0)
    core = np.exp(-(r / 0.12) ** 2) * 0.9
    save("Cracks", line * fade + core, blur(line, 6) * 0.55 * fade)


def beam():
    """A column: bright thin core, soft wide glow, fading out at the top and in at the bottom."""
    x, y = grid()
    across = np.exp(-(x / 0.08) ** 2) + 0.45 * np.exp(-(x / 0.32) ** 2)
    up = (1.0 - y) / 2.0  # 0 at the bottom, 1 at the top (image rows run down)
    along = np.clip(up / 0.08, 0.0, 1.0) * np.clip((1.0 - up) / 0.55, 0.0, 1.0) ** 0.8
    save("Beam", across * along)


def flare():
    """A star: a hot centre, a soft halo and long thin rays (four strong, four faint)."""
    x, y = grid()
    r = np.sqrt(x * x + y * y)
    a = np.arctan2(y, x)
    core = np.exp(-(r / 0.07) ** 2)
    halo = np.exp(-(r / 0.35) ** 2) * 0.45
    rays = (np.abs(np.cos(2 * a)) ** 60 * 0.9 + np.abs(np.cos(2 * a + math.pi / 4)) ** 80 * 0.45) * np.exp(-r / 0.35)
    save("Flare", (core + halo + rays) * np.clip(1.0 - r, 0.0, 1.0))


def shockwave():
    """A ring with a hard bright outer edge and a soft trail inward."""
    x, y = grid()
    r = np.sqrt(x * x + y * y)
    edge = np.exp(-((r - 0.9) / 0.025) ** 2)
    trail = np.clip((r - 0.55) / 0.35, 0.0, 1.0) ** 2.2 * (r < 0.9)
    save("Shockwave", edge + trail * 0.3)


def feather():
    """A feather's silhouette, quill along the diagonal, barbs to both sides with a few splits."""
    x, y = grid()
    u = (x + y) / math.sqrt(2)  # along
    v = (x - y) / math.sqrt(2)  # across
    t = (u + 0.85) / 1.7
    width = 0.22 * np.clip(np.sin(np.clip(t, 0, 1) * math.pi), 0, 1) ** 0.7
    vane = (np.abs(v) < width) & (t > 0.08) & (t < 0.98)
    splits = np.sin(t * 40.0 + np.sign(v) * 3.0) > 0.92
    quill = (np.abs(v) < 0.012) & (t > 0.0) & (t < 1.0)
    alpha = (vane & ~(splits & (np.abs(v) > width * 0.4))).astype(np.float32) * 0.95 + quill.astype(np.float32)
    save("Feather", blur(alpha, 1.0))


def snowflake():
    """A six-armed crystal with side branches."""
    im = Image.new("L", (SIZE, SIZE), 0)
    draw = ImageDraw.Draw(im)
    c = SIZE / 2
    for i in range(6):
        a = i * math.pi / 3
        ex, ey = c + math.cos(a) * 220, c + math.sin(a) * 220
        draw.line([(c, c), (ex, ey)], fill=255, width=14)
        for k, along in enumerate((0.35, 0.55, 0.75)):
            bx, by = c + math.cos(a) * 220 * along, c + math.sin(a) * 220 * along
            length = 80 * (1.0 - along) + 20
            for side in (-1, 1):
                b = a + side * math.pi / 4
                draw.line([(bx, by), (bx + math.cos(b) * length, by + math.sin(b) * length)], fill=255, width=9)
    line = np.asarray(im).astype(np.float32) / 255.0
    save("Snowflake", line, blur(line, 8) * 0.5)


if __name__ == "__main__":
    rng = __import__("random").Random(1127)
    cracks(rng)
    beam()
    flare()
    shockwave()
    feather()
    snowflake()
