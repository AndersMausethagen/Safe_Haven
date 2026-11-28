"""The Purification Altar's UI shapes (Docs/AltarUXResearch_2026-11-28.md), drawn white on clear so the window tints them:

  Glyph Ring.png  two circles with the 24 runes of the elder futhark between them, softly glowing (turns slowly behind the stage)
  Glow.png        a soft round light (the item's glow, the flash)
  Spark.png       a small four-pointed star with a soft halo (the burst's sparks)
  Shock.png       a thin bright ring fading outward (the commit's shockwave)

Into Assets/_Project/Resources/AltarArt, loaded as sprites by AltarFeel. Run from the project root: python Tools/UI/altar_art.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join("Assets", "_Project", "Resources", "AltarArt")

# The elder futhark as strokes in a 0..1 box (x right, y down): staves, twigs and hooks, close enough to be read as runes.
RUNES = [
    [((0.3, 0), (0.3, 1)), ((0.3, 0.1), (0.75, 0.0)), ((0.3, 0.35), (0.75, 0.25))],  # fehu
    [((0.3, 0), (0.3, 1)), ((0.3, 0), (0.72, 0.35)), ((0.72, 0.35), (0.72, 1))],  # uruz
    [((0.3, 0), (0.3, 1)), ((0.3, 0.3), (0.72, 0.5)), ((0.72, 0.5), (0.3, 0.7))],  # thurisaz
    [((0.3, 0), (0.3, 1)), ((0.3, 0.0), (0.72, 0.22)), ((0.3, 0.3), (0.72, 0.52))],  # ansuz
    [((0.3, 0), (0.3, 1)), ((0.3, 0), (0.72, 0.25)), ((0.72, 0.25), (0.3, 0.5)), ((0.3, 0.5), (0.72, 1))],  # raido
    [((0.7, 0.1), (0.3, 0.5)), ((0.3, 0.5), (0.7, 0.9))],  # kaunan
    [((0.25, 0.1), (0.75, 0.9)), ((0.75, 0.1), (0.25, 0.9))],  # gebo
    [((0.3, 0), (0.3, 1)), ((0.3, 0.05), (0.72, 0.25)), ((0.72, 0.25), (0.3, 0.45))],  # wunjo
    [((0.28, 0), (0.28, 1)), ((0.72, 0), (0.72, 1)), ((0.28, 0.35), (0.72, 0.6))],  # hagalaz
    [((0.5, 0), (0.5, 1)), ((0.3, 0.35), (0.7, 0.6))],  # naudiz
    [((0.5, 0), (0.5, 1))],  # isaz
    [((0.3, 0.2), (0.5, 0.0)), ((0.5, 0.0), (0.7, 0.2)), ((0.3, 0.8), (0.5, 1.0)), ((0.5, 1.0), (0.7, 0.8)), ((0.5, 0), (0.5, 1))],  # jera
    [((0.5, 0), (0.5, 1)), ((0.5, 0.0), (0.72, 0.18)), ((0.5, 1.0), (0.28, 0.82))],  # eihwaz
    [((0.3, 0), (0.3, 1)), ((0.3, 0), (0.7, 0.2)), ((0.3, 1), (0.7, 0.8))],  # perth
    [((0.5, 0.3), (0.5, 1)), ((0.5, 0.3), (0.25, 0.0)), ((0.5, 0.3), (0.75, 0.0))],  # algiz
    [((0.25, 0.1), (0.75, 0.35)), ((0.75, 0.35), (0.25, 0.65)), ((0.25, 0.65), (0.75, 0.9))],  # sowilo
    [((0.5, 0), (0.5, 1)), ((0.5, 0), (0.25, 0.25)), ((0.5, 0), (0.75, 0.25))],  # tiwaz
    [((0.3, 0), (0.3, 1)), ((0.3, 0), (0.7, 0.25)), ((0.7, 0.25), (0.3, 0.5)), ((0.3, 0.5), (0.7, 0.75)), ((0.7, 0.75), (0.3, 1))],  # berkanan
    [((0.28, 0), (0.28, 1)), ((0.72, 0), (0.72, 1)), ((0.28, 0), (0.5, 0.3)), ((0.5, 0.3), (0.72, 0))],  # ehwaz
    [((0.28, 0), (0.28, 1)), ((0.72, 0), (0.72, 1)), ((0.28, 0), (0.72, 0.45)), ((0.72, 0), (0.28, 0.45))],  # mannaz
    [((0.35, 0), (0.35, 1)), ((0.35, 0), (0.7, 0.3))],  # laguz
    [((0.5, 0.1), (0.75, 0.5)), ((0.75, 0.5), (0.5, 0.9)), ((0.5, 0.9), (0.25, 0.5)), ((0.25, 0.5), (0.5, 0.1))],  # ingwaz
    [((0.25, 0.1), (0.75, 0.9)), ((0.75, 0.1), (0.25, 0.9)), ((0.25, 0.1), (0.25, 0.9)), ((0.75, 0.1), (0.75, 0.9))],  # dagaz
    [((0.5, 0.0), (0.75, 0.3)), ((0.75, 0.3), (0.5, 0.6)), ((0.5, 0.6), (0.25, 0.3)), ((0.25, 0.3), (0.5, 0.0)),
     ((0.5, 0.6), (0.25, 1.0)), ((0.5, 0.6), (0.75, 1.0))],  # othala
]


def glyph_ring(size=1024):
    scale = 4
    big = size * scale
    image = Image.new("L", (big, big), 0)
    draw = ImageDraw.Draw(image)
    centre = big / 2
    outer, inner = big * 0.47, big * 0.35
    line = int(big * 0.006)
    draw.ellipse((centre - outer, centre - outer, centre + outer, centre + outer), outline=255, width=line)
    draw.ellipse((centre - inner, centre - inner, centre + inner, centre + inner), outline=200, width=int(line * 0.7))
    mid = (outer + inner) / 2
    height = (outer - inner) * 0.62
    for i, strokes in enumerate(RUNES):
        angle = i / len(RUNES) * math.tau
        # Each rune upright along the ring (its top toward the outside), centred between the circles.
        cx, cy = centre + math.sin(angle) * mid, centre - math.cos(angle) * mid
        up = (math.sin(angle), -math.cos(angle))
        right = (math.cos(angle), math.sin(angle))
        for (x0, y0), (x1, y1) in strokes:
            def at(x, y):
                u, v = (x - 0.5) * height, (0.5 - y) * height
                return cx + right[0] * u + up[0] * v, cy + right[1] * u + up[1] * v
            draw.line([at(x0, y0), at(x1, y1)], fill=255, width=int(line * 0.9))
    image = image.resize((size, size), Image.LANCZOS)
    halo = image.filter(ImageFilter.GaussianBlur(size * 0.012))
    alpha = Image.eval(Image.merge("L", [image]).point(lambda v: v), lambda v: v)
    combined = Image.eval(halo, lambda v: int(v * 0.8))
    combined.paste(255, mask=alpha)
    return white(combined)


def white(alpha):
    out = Image.new("RGBA", alpha.size, (255, 255, 255, 0))
    out.putalpha(alpha)
    return out


def glow(size=256):
    image = Image.new("L", (size, size), 0)
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - size / 2 + 0.5, y - size / 2 + 0.5) / (size / 2)
            pixels[x, y] = int(255 * max(0.0, 1 - d) ** 2.2)
    return white(image)


def spark(size=128):
    image = Image.new("L", (size, size), 0)
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            dx, dy = (x - size / 2 + 0.5) / (size / 2), (y - size / 2 + 0.5) / (size / 2)
            star = max(0.0, 1 - abs(dx) * 7 - abs(dy) * 1.05) + max(0.0, 1 - abs(dy) * 7 - abs(dx) * 1.05)
            halo = max(0.0, 1 - math.hypot(dx, dy)) ** 3 * 0.6
            pixels[x, y] = int(255 * min(1.0, star + halo))
    return white(image)


def shock(size=512):
    image = Image.new("L", (size, size), 0)
    pixels = image.load()
    for y in range(size):
        for x in range(size):
            d = math.hypot(x - size / 2 + 0.5, y - size / 2 + 0.5) / (size / 2)
            ring = math.exp(-((d - 0.86) / 0.05) ** 2) + math.exp(-((d - 0.86) / 0.16) ** 2) * 0.35
            pixels[x, y] = int(255 * min(1.0, ring)) if d < 1 else 0
    return white(image)


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, image in (("Glyph Ring", glyph_ring()), ("Glow", glow()), ("Spark", spark()), ("Shock", shock())):
        image.save(os.path.join(OUT, name + ".png"))
        print("saved", name)


if __name__ == "__main__":
    main()
