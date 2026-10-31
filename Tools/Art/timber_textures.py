"""Draws the textures of a felled tree: the sawn face of a log, falling leaves and a soft puff of dust.

Run from the project root: python Tools/Art/timber_textures.py
"""
import os

import numpy as np
from PIL import Image

OUT = "Assets/_Project/Art/Forest/Textures"
rng = np.random.default_rng(11)


def grid(size):
    axis = (np.arange(size) + 0.5) / size * 2 - 1
    return np.meshgrid(axis, axis)


def smooth_noise(size, cells):
    """Soft blobs, by enlarging a small field of random values."""
    field = Image.fromarray((rng.random((cells, cells)) * 255).astype(np.uint8))
    return np.asarray(field.resize((size, size), Image.BICUBIC), dtype=np.float32) / 255


def save(name, rgba):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))


def cut_face(size=512):
    """Year rings around an off-centre heart, wandering a little, with the checks that open as wood dries."""
    x, y = grid(size)
    x = x - 0.06
    y = y + 0.04
    angle = np.arctan2(y, x)
    radius = np.hypot(x, y) * (1 + 0.025 * np.sin(angle * 3 + 0.7) + 0.012 * np.sin(angle * 7)) + (smooth_noise(size, 14) - 0.5) * 0.015
    # Latewood is a thin dark line after each wide pale band, and the years are not all alike.
    years = radius * 34 + (smooth_noise(size, 5) - 0.5) * 1.4 + np.sin(radius * 11) * 0.7
    rings = 1 - np.clip((years % 1) * 1.15 - 0.15, 0, 1) ** 5
    grain = (smooth_noise(size, 120) - 0.5) * 0.14
    light = np.array([0.72, 0.56, 0.38])
    dark = np.array([0.47, 0.31, 0.19])
    colour = dark + (light - dark) * rings[..., None]
    colour = colour * (1 + grain[..., None])
    heart = np.clip(1 - radius / 0.32, 0, 1)[..., None]
    colour = colour * (1 - heart * 0.32) + np.array([0.30, 0.17, 0.09]) * heart * 0.32
    for check in rng.uniform(-np.pi, np.pi, 4):
        width = 0.002 + 0.006 * radius
        split = np.exp(-(((angle - check + np.pi) % (2 * np.pi) - np.pi) * np.maximum(radius, 0.02) / width) ** 2) * np.clip(radius - 0.1, 0, 0.5) * 2
        colour = colour * (1 - np.clip(split, 0, 0.5)[..., None])
    rim = np.clip((radius - 0.86) / 0.08, 0, 1)[..., None]
    colour = colour * (1 - rim) + np.array([0.20, 0.13, 0.09]) * rim
    save("LogCutFace", np.concatenate([colour, np.ones((size, size, 1))], axis=2))


def leaf(size, width, tip, curve):
    x, y = grid(size)
    y = -y
    x = x - curve * (y ** 2 - 0.5)
    t = np.clip((y + 0.95) / 1.9, 0, 1)
    edge = width * np.sin(np.pi * t ** tip)
    alpha = np.clip((edge - np.abs(x)) * size * 0.25, 0, 1) * (np.abs(y) < 0.95)
    rib = np.exp(-(x / 0.03) ** 2) * 0.28
    veins = (0.5 + 0.5 * np.sin((y - np.abs(x) * 1.4) * 34)) * 0.12
    value = 0.92 - rib - veins + (t - 0.5) * 0.12
    return np.dstack([value, value, value, alpha])


def falling_leaves(size=256):
    """Two by two: three broad leaves and one long needle sprig. White, as each tree tints them."""
    half = size // 2
    atlas = np.zeros((size, size, 4))
    atlas[:half, :half] = leaf(half, 0.5, 0.8, 0.12)
    atlas[:half, half:] = leaf(half, 0.38, 0.65, -0.18)
    atlas[half:, :half] = leaf(half, 0.6, 1.0, 0.06)
    atlas[half:, half:] = leaf(half, 0.13, 1.0, 0.1)
    save("FallingLeaves", atlas)


def puff(size=128):
    """A soft, lumpy ball that fades to nothing before its edges, so a square never shows."""
    x, y = grid(size)
    fade = np.clip(1 - np.hypot(x, y), 0, 1) ** 1.6
    lumps = 0.55 + 0.45 * smooth_noise(size, 7)
    alpha = np.clip(fade * lumps * 1.5, 0, 1)
    save("DustPuff", np.dstack([np.ones((size, size)), np.ones((size, size)), np.ones((size, size)), alpha]))


def main():
    cut_face()
    falling_leaves()
    puff()


if __name__ == "__main__":
    main()
