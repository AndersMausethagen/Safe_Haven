"""Draws the soft overlays the cinematics lay over their paintings: a glow, drifting mist and a vignette.

  Glow.png      a round light that fades to nothing: firelight, an ember, a lantern, a rune (drawn adding light)
  Mist.png      low banks of mist, thicker towards the bottom; its left and right edges match, so it can slide
  Vignette.png  darkens the corners of the frame

Run from the project root: python Tools/Art/cinematic_overlays.py
"""
import os

import numpy as np
from PIL import Image

OUT = "Assets/_Project/Art/Cinematics/Shared"


def save(name, rgb, alpha):
    h, w = alpha.shape
    colour = np.broadcast_to(np.asarray(rgb, dtype=np.float32), (h, w, 3))
    # A grain of noise finer than one step of the 8 bits: a slow fade drawn adding light otherwise shows as rings.
    grain = np.random.default_rng(11).uniform(-0.5, 0.5, alpha.shape) / 255.0
    rgba = np.dstack([colour, np.clip(alpha + grain * (alpha > 0), 0, 1) * 255]).astype(np.uint8)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(rgba, "RGBA").save(os.path.join(OUT, name))


def glow(size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    r = np.hypot(x - size / 2 + 0.5, y - size / 2 + 0.5) / (size / 2)
    # A bright heart and a long soft skirt, stored as it should look. The screen shows faint light as brighter than
    # its number, so a glow added to black needs its faint end far below what 8 bits can hold: the additive shader
    # (SafeHaven/UI/Additive) raises this to the power that undoes the screen's curve, after reading it.
    alpha = (0.4 * np.exp(-(r / 0.2) ** 2) + 0.6 * np.exp(-(r / 0.5) ** 2)) * np.clip(1.0 - r * r, 0, 1) ** 2
    save("Glow.png", (255, 255, 255), alpha)


def mist(width=1024, height=512, seed=7):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    field = np.zeros((height, width), np.float32)
    # Sums of waves that fit the width a whole number of times, so the left edge meets the right.
    for octave in range(1, 7):
        for _ in range(3):
            kx = int(rng.integers(1, 3 * octave + 1))
            ky = rng.uniform(0.5, 2.0) * octave
            phase_x, phase_y = rng.uniform(0, 2 * np.pi, 2)
            field += np.sin(2 * np.pi * kx * x / width + phase_x) * np.sin(2 * np.pi * ky * y / height + phase_y) / octave
    field = (field - field.min()) / (field.max() - field.min())
    banks = np.clip((field - 0.5) / 0.38, 0, 1) ** 1.4
    # Thick low down, thinning upwards, nothing at the very top.
    up = 1.0 - y / height
    fall = np.clip(1.05 - up * 1.7, 0, 0.85) * np.clip(y / (height * 0.12), 0, 1)
    save("Mist.png", (222, 230, 235), banks * fall * 0.7)


def vignette(width=1024, height=576):
    y, x = np.mgrid[0:height, 0:width].astype(np.float32)
    r = np.hypot((x - width / 2) / (width / 2), (y - height / 2) / (height / 2)) / np.sqrt(2)
    save("Vignette.png", (0, 0, 0), np.clip((r - 0.45) / 0.55, 0, 1) ** 1.8 * 0.7)


if __name__ == "__main__":
    glow()
    mist()
    vignette()
    print("Wrote Glow.png, Mist.png and Vignette.png to", OUT)
