"""Rare horse coats from the common one (Art/Wildlife/Horse/HorseCoat.png), for HorseDefinition > Rare Coats.

  HorseCoat Dapple.png  The coat with dapples: lighter rings in a darker net, luminance only (tinted grey in the game).
  HorseCoat Pinto.png   Bay with large white patches, coloured in (the game's tint for it is white).

Run with any Python that has Pillow and numpy:  python Tools/Horses/make_coats.py
"""
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ART = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "_Project", "Art", "Wildlife", "Horse"))
SIZE = 2048


def smooth_noise(size, cells, seed):
    """Value noise: a random grid of cells x cells, smoothly enlarged to size x size, 0-1."""
    rng = np.random.default_rng(seed)
    grid = rng.random((cells, cells)).astype(np.float32)
    image = Image.fromarray((grid * 255).astype(np.uint8)).resize((size, size), Image.BICUBIC)
    return np.asarray(image, dtype=np.float32) / 255.0


def dapples(size, count, seed):
    """Dapples: round, soft light spots scattered close together, the coat darker between them, as on a dapple grey."""
    rng = np.random.default_rng(seed)
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    nearest = np.full((size, size), 1e9, dtype=np.float32)
    for _ in range(count):
        cx, cy = rng.random() * size, rng.random() * size
        nearest = np.minimum(nearest, np.hypot(xs - cx, ys - cy))
    radius = size / np.sqrt(count) * 0.72
    spot = np.clip(1.0 - nearest / radius, 0.0, 1.0)
    # Soft-edged: bright in the middle, fading out before the next spot.
    spot = spot * spot * (3.0 - 2.0 * spot)
    image = Image.fromarray((spot * 255).astype(np.uint8)).filter(__import__("PIL.ImageFilter", fromlist=["GaussianBlur"]).GaussianBlur(3))
    return np.asarray(image, dtype=np.float32) / 255.0


def main():
    coat_path = os.path.join(ART, "HorseCoat.png")
    if not os.path.exists(coat_path):
        sys.exit(f"no {coat_path}")
    coat = np.asarray(Image.open(coat_path).convert("RGB").resize((SIZE, SIZE)), dtype=np.float32) / 255.0
    luminance = coat.mean(axis=2, keepdims=True)

    # Dapple: the coat darkened into a net with light dapples, a little cloud over it so it isn't too even.
    spots = dapples(SIZE, 480, seed=7)[..., None]
    cloud = smooth_noise(SIZE, 12, seed=3)[..., None]
    dapple = coat * (0.66 + 0.34 * spots) * (0.9 + 0.2 * cloud)
    Image.fromarray(np.clip(dapple * 255, 0, 255).astype(np.uint8)).save(os.path.join(ART, "HorseCoat Dapple.png"))

    # Pinto: bay, and large white patches where a slow noise is high.
    bay = np.array([0.42, 0.25, 0.14], dtype=np.float32)
    patches = smooth_noise(SIZE, 7, seed=11)
    white = np.clip((patches - 0.52) / 0.06, 0.0, 1.0)[..., None]
    pinto = luminance * (bay * 1.3) * (1.0 - white) + coat * 1.02 * white
    Image.fromarray(np.clip(pinto * 255, 0, 255).astype(np.uint8)).save(os.path.join(ART, "HorseCoat Pinto.png"))
    print("dapple and pinto coats written to", ART)


if __name__ == "__main__":
    main()
