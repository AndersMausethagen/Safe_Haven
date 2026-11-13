"""The hair-card texture for horse manes and tails (Tools/Blender/horse_hair.py lays the cards out): a strip of fine
strands, root at the top, each a little curved, of its own length and brightness, the tips thinning to nothing.
Strands gather in locks with gaps between them. Pale and nearly grey on purpose: the game tints it with the horse's mane colour (Horse.cs, HorseDefinition.Manes).
Colour in RGB, coverage in alpha (alpha-clipped in Unity), plus a strand-direction normal map.

Run: python Tools/Blender/hair_card_texture.py <out folder>   (writes Hair Card_Albedo.png and Hair Card_Normal.png)
"""
import math
import os
import random
import sys

import numpy
from PIL import Image, ImageFilter

W, H = 512, 1024


def make(out):
    rng = random.Random(23)
    colour = numpy.zeros((H, W), numpy.float32)
    alpha = numpy.zeros((H, W), numpy.float32)
    slope = numpy.zeros((H, W), numpy.float32)  # sideways lean of the strands, for the normal map
    ys = numpy.arange(H, dtype=numpy.float32)
    # Locks: strands gather in clumps, each with its own sway, gaps between them, ragged ends.
    locks = [(rng.uniform(0, W), rng.uniform(14, 34), rng.uniform(0.6, 1.0), rng.uniform(4, 18), rng.uniform(0, math.tau),
              rng.uniform(-22, 22), rng.uniform(0.6, 1.0)) for _ in range(16)]
    for _ in range(520):
        cx, spread, lock_len, lock_wave, lock_phase, lock_lean, lock_bright = locks[rng.randrange(len(locks))]
        x0 = cx + rng.gauss(0, spread * 0.5)
        length = min(1.0, lock_len * rng.uniform(0.75, 1.05)) * H
        width = rng.uniform(0.8, 2.2)
        bright = lock_bright * rng.uniform(0.75, 1.0)
        wave, phase, lean = lock_wave * rng.uniform(0.8, 1.2), lock_phase + rng.uniform(-0.3, 0.3), lock_lean + rng.uniform(-4, 4)
        t = ys / H
        xs = x0 + lean * t * t + wave * numpy.sin(t * rng.uniform(2.0, 4.5) + phase) * t
        for y in range(int(length)):
            x = xs[y]
            taper = 1.0 - max(0.0, (y - length * 0.65) / (length * 0.35))  # thins over its last third
            w = width * (0.35 + 0.65 * taper)
            lo, hi = int(math.floor(x - w - 1)), int(math.ceil(x + w + 1))
            for px in range(max(0, lo), min(W, hi + 1)):
                cover = max(0.0, 1.0 - abs(px - x) / max(w, 0.5)) * (0.4 + 0.6 * taper)
                if cover > alpha[y, px]:
                    alpha[y, px] = cover
                    colour[y, px] = bright * (0.85 + 0.15 * math.sin(px * 0.9 + y * 0.03))
                    slope[y, px] = (px - x) / max(w, 0.5)
    alpha = numpy.clip(alpha * 1.6, 0, 1)
    # Darker towards the root (shade under the mane), lighter at the ends.
    shade = (0.75 + 0.25 * (ys / H))[:, None]
    grey = numpy.clip(colour * shade, 0, 1)
    rgb = numpy.dstack([grey * 0.96, grey * 0.93, grey * 0.88])
    os.makedirs(out, exist_ok=True)
    Image.fromarray((numpy.dstack([rgb, alpha]) * 255).astype(numpy.uint8), "RGBA").save(os.path.join(out, "Hair Card_Albedo.png"))
    # Normal: strands are round, so each leans its normal sideways across its width.
    nx = numpy.clip(slope, -1, 1) * 0.6
    nz = numpy.sqrt(numpy.clip(1 - nx * nx, 0, 1))
    normal = numpy.dstack([nx * 0.5 + 0.5, numpy.full_like(nx, 0.5), nz * 0.5 + 0.5])
    Image.fromarray((normal * 255).astype(numpy.uint8), "RGB").filter(ImageFilter.GaussianBlur(0.6)).save(os.path.join(out, "Hair Card_Normal.png"))
    print("HAIR TEXTURE ->", out)


if __name__ == "__main__":
    make(sys.argv[1] if len(sys.argv) > 1 else ".")
