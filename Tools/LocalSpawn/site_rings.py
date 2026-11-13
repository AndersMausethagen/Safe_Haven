"""The ground rings that mark special places (AgentScripts/BuildStructureModels.cs, Marks): a soft glowing circle with a
band of faint rune-like ticks inside it, drawn as decal textures with their colour in them (the decal shader has no tint):
gold for raid event sites, the runes' blue for the world's rune props.

Writes Assets/_Project/Art/Decals/Site Ring.png and Rune Ring.png.
Run: D:\\ComfyUI\\.venv\\Scripts\\python.exe Tools/LocalSpawn/site_rings.py
"""
import math
import os
import random

import numpy

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.abspath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Decals"))
SIZE = 512


def ring(colour, seed):
    rng = random.Random(seed)
    y, x = numpy.mgrid[0:SIZE, 0:SIZE].astype(numpy.float32)
    u = (x + 0.5) / SIZE * 2 - 1
    v = (y + 0.5) / SIZE * 2 - 1
    r = numpy.sqrt(u * u + v * v)
    angle = numpy.arctan2(v, u)
    # The ring: a sharp line with a soft glow on both sides, and a fainter second line inside.
    alpha = numpy.exp(-((r - 0.86) / 0.018) ** 2) * 0.95 + numpy.exp(-((r - 0.86) / 0.07) ** 2) * 0.35
    alpha += numpy.exp(-((r - 0.62) / 0.012) ** 2) * 0.45
    # Ticks between the lines, like runes worn faint: short strokes at uneven spacing.
    ticks = numpy.zeros_like(r)
    count = 28
    for k in range(count):
        a = math.tau * k / count + rng.uniform(-0.04, 0.04)
        width = rng.uniform(0.01, 0.03)
        length = rng.uniform(0.05, 0.12)
        centre = rng.uniform(0.7, 0.78)
        da = numpy.abs(numpy.arctan2(numpy.sin(angle - a), numpy.cos(angle - a)))
        ticks = numpy.maximum(ticks, numpy.exp(-(da / width) ** 2) * numpy.exp(-((r - centre) / length) ** 4))
    alpha += ticks * 0.6
    alpha *= numpy.clip((1.0 - r) / 0.04, 0, 1)  # nothing reaches the square's edge
    alpha = numpy.clip(alpha, 0, 1)
    rgb = numpy.ones((SIZE, SIZE, 3), dtype=numpy.float32) * numpy.array(colour, dtype=numpy.float32)
    rgb = rgb * (0.75 + 0.25 * alpha[..., None])  # the brightest parts whiter
    image = numpy.dstack([numpy.clip(rgb, 0, 1), alpha])
    return Image.fromarray((image * 255).astype(numpy.uint8), "RGBA")


def main():
    os.makedirs(OUT, exist_ok=True)
    ring((1.0, 0.82, 0.45), 11).save(os.path.join(OUT, "Site Ring.png"))
    ring((0.55, 0.8, 1.0), 12).save(os.path.join(OUT, "Rune Ring.png"))
    print("RINGS ->", OUT)


if __name__ == "__main__":
    main()
