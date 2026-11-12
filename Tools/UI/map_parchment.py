"""The world map's paper (Assets/_Project/Art/UI/Map): parchment the map is drawn on and the grain its charcoal takes.

- Map Parchment.png: tan parchment, tileable. Mottled with age, fine fibres laid one way, a few water stains and foxing
  spots, all from noise filtered in frequency space, which makes every layer wrap round by itself.
- Map Grain.png: data for the World Map shader, tileable, linear. R the paper's tooth (where charcoal catches and where
  it skips), G a smudge noise for soft charcoal shading, B a slow noise for ragged edges (the explored edge, the sheet's
  deckle), A fibres.

Run from the project root: python Tools/UI/map_parchment.py
"""
import os

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Art', 'UI', 'Map')
RNG = np.random.default_rng(1847)


def noise(size, low, high, slope=1.0, stretch=(1.0, 1.0)):
    """Tileable noise: white noise kept between two frequencies (cycles per tile), falling off as 1/f^slope, 0..1."""
    fy = np.fft.fftfreq(size)[:, None] * size / stretch[1]
    fx = np.fft.rfftfreq(size)[None, :] * size / stretch[0]
    f = np.sqrt(fx * fx + fy * fy)
    band = ((f >= low) & (f <= high)).astype(float) / np.maximum(f, 1.0) ** slope
    spectrum = (RNG.normal(size=(size, size // 2 + 1)) + 1j * RNG.normal(size=(size, size // 2 + 1))) * band
    field = np.fft.irfft2(spectrum, s=(size, size))
    field -= field.min()
    return field / max(field.max(), 1e-9)


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def parchment(size=1024):
    base = np.array([0.80, 0.69, 0.50])
    light = np.array([0.90, 0.82, 0.64])
    dark = np.array([0.62, 0.50, 0.33])

    mottle = noise(size, 1, 24, slope=1.1)
    cloud = noise(size, 2, 60, slope=0.9)
    fibres = noise(size, 30, 260, slope=0.6, stretch=(6.0, 1.0))
    tooth = noise(size, 120, 512, slope=0.3)
    # A few stains, each with its tide line: both from the same field, the line just outside the stain.
    blots = noise(size, 2, 9, slope=1.6)
    stains = smoothstep(0.84, 0.97, blots)
    rims = np.clip(smoothstep(0.80, 0.84, blots) - smoothstep(0.84, 0.90, blots), 0, 1)
    foxing = smoothstep(0.93, 0.985, noise(size, 40, 90, slope=0.2)) * smoothstep(0.4, 0.8, cloud)

    # Age as a gentle wash, not patches: the tone moves the colour a little either way of the base.
    tone = (0.6 * mottle + 0.4 * cloud - 0.5) * 2.0
    up, down = np.clip(tone, 0, 1), np.clip(-tone, 0, 1)
    rgb = base + (light - base) * (0.55 * up)[..., None] - (base - dark) * (0.45 * down)[..., None]
    rgb *= (0.94 + 0.10 * fibres)[..., None]
    rgb *= (0.97 + 0.05 * tooth)[..., None]
    # A water stain is a little darker and browner inside, with a darker tide line round it.
    brown = np.array([0.52, 0.38, 0.22])
    rgb = rgb * (1 - 0.12 * stains[..., None]) + brown * 0.12 * stains[..., None]
    rgb = rgb * (1 - 0.09 * rims[..., None]) + brown * 0.09 * rims[..., None]
    rgb = rgb * (1 - 0.35 * foxing[..., None]) + np.array([0.45, 0.30, 0.16]) * 0.35 * foxing[..., None]
    return np.clip(rgb, 0, 1)


def grain(size=512):
    tooth = noise(size, 60, 256, slope=0.2)
    smudge = noise(size, 4, 40, slope=1.0)
    slow = noise(size, 1, 10, slope=1.2)
    fibres = noise(size, 16, 160, slope=0.5, stretch=(6.0, 1.0))
    # The tooth is pushed apart, so charcoal reads as grains caught on the paper and gaps where it skipped.
    tooth = smoothstep(0.25, 0.75, tooth)
    return np.dstack([tooth, smudge, slow, fibres])


def save(name, array, mode):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((np.clip(array, 0, 1) * 255).astype(np.uint8), mode).save(os.path.join(OUT, name + '.png'))


def main():
    save('Map Parchment', parchment(), 'RGB')
    save('Map Grain', grain(), 'RGBA')
    print('written to', os.path.normpath(OUT))


if __name__ == '__main__':
    main()
