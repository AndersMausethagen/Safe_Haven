"""Hearthline art made in numpy (Assets/_Project/Art/UI): drawn at four times the size and scaled down, so every edge is
smooth.

- Hearthline Divider: the rule under a window's title. A hairline that fades out at both ends, a lozenge knot in the
  middle with a dot either side, white so UIStyle tints it.
- Hearthline Cursor: the pointer. An amber-edged dark arrowhead with a carved notch, its tip at the top left.
- Hearthline Glow: a soft round glow, for behind the one button to press and lit item icons.

Run from the project root: python Tools/UI/hearthline_art.py
"""
import os

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Art', 'UI')
SS = 4


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def save(name, rgba):
    os.makedirs(OUT, exist_ok=True)
    h, w = rgba.shape[:2]
    img = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), 'RGBA')
    img = img.resize((w // SS, h // SS), Image.LANCZOS)
    img.save(os.path.join(OUT, name + '.png'))


def divider():
    w, h = 512 * SS, 40 * SS
    y, x = np.mgrid[0:h, 0:w].astype(float)
    cx, cy = w / 2, h / 2
    dx, dy = (x - cx) / SS, (y - cy) / SS
    # The hairline, fading towards both ends.
    fade = 1 - smoothstep(60, 250, np.abs(dx))
    line = (1 - smoothstep(0.6, 1.4, np.abs(dy))) * fade
    # A gap round the knot.
    line *= smoothstep(16, 22, np.abs(dx))
    # The lozenge: an outlined diamond with a filled heart.
    d = np.abs(dx) / 11 + np.abs(dy) / 7
    lozenge = (1 - smoothstep(0.86, 1.0, d)) * smoothstep(0.62, 0.74, d) + (1 - smoothstep(0.32, 0.42, d))
    # A dot either side.
    dots = np.zeros_like(dx)
    for side in (-1, 1):
        r = np.sqrt((dx - side * 19) ** 2 + dy ** 2)
        dots = np.maximum(dots, 1 - smoothstep(1.4, 2.2, r))
    alpha = np.clip(np.maximum(np.maximum(line, lozenge), dots), 0, 1)
    save('Hearthline Divider', np.dstack([np.ones_like(alpha)] * 3 + [alpha]))


def cursor():
    s = 64 * SS
    y, x = np.mgrid[0:s, 0:s].astype(float) / SS
    # An arrowhead from the tip at (2, 2): a long edge down, a short edge across, a notched tail.
    def inside(px, py, grow=0.0):
        u, v = px - 2, py - 2
        a = u >= 0.04 * v - grow             # the left edge, nearly upright
        b = v >= 1.2 * u - grow              # the right edge, at about fifty degrees
        c = u * 0.3 + v <= 28 + grow         # the tail, cut across
        return a & b & c
    body = inside(x, y).astype(float)
    edge = inside(x, y, 2.2).astype(float) - body
    notch = (np.abs((x - 2) * 2.6 - (y - 2)) < 1.4) & ((y - 2) > 8) & ((y - 2) < 20)
    dark = np.array([0.09, 0.08, 0.07])
    amber = np.array([0.9, 0.7, 0.38])
    rgb = body[..., None] * dark + edge[..., None] * amber
    rgb[notch & (body > 0)] = amber * 0.75
    alpha = np.clip(body + edge, 0, 1)
    # A soft shadow under it, down and right.
    shadow = inside(x - 2, y - 3, 1.5).astype(float) * 0.35
    alpha_all = np.maximum(alpha, shadow)
    rgb = np.where(alpha[..., None] > 0, rgb, 0)
    save('Hearthline Cursor', np.dstack([rgb, alpha_all]))


def glow():
    s = 128 * SS
    y, x = np.mgrid[0:s, 0:s].astype(float)
    r = np.sqrt((x - s / 2) ** 2 + (y - s / 2) ** 2) / (s / 2)
    alpha = np.clip(1 - r, 0, 1) ** 2.2
    save('Hearthline Glow', np.dstack([np.ones_like(alpha)] * 3 + [alpha]))


if __name__ == '__main__':
    divider()
    cursor()
    glow()
    print('art in', os.path.abspath(OUT))
