"""Textures for the skill effects (Docs/CombatSkillsResearch_2026-11-24.md section 6), drawn procedurally so they stay ours.

Writes white-on-transparent RGBA PNGs (the effect's colour comes from the graph), 512 px, into
Assets/_Project/Art/Effects/Skills/Textures:
  Glow.png         a soft round glow with a bright core
  Ring.png         a thin soft ring (shockwaves, cast rings)
  Shard.png        a long thin diamond (ice shards, splinters)
  Rune Circle.png  a double ring carrying runes of the elder futhark (the cast circle under the feet)
  Ember.png        a small hot ember with a faint tail
  Slash.png        a crescent swept by a blade: bright at its leading edge, thin at the tips (physical arts)
  Streak.png       a long soft line, for speed lines along a dash
Run: python Tools/Effects/skill_textures.py
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

SIZE = 512
OUT = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "_Project", "Art", "Effects", "Skills", "Textures")


def blank():
    return Image.new("L", (SIZE, SIZE), 0)


def save(alpha, name):
    rgba = Image.new("RGBA", (SIZE, SIZE), (255, 255, 255, 0))
    rgba.putalpha(alpha)
    os.makedirs(OUT, exist_ok=True)
    rgba.save(os.path.join(OUT, name))


def glow():
    a = blank()
    px = a.load()
    c = SIZE / 2
    for y in range(SIZE):
        for x in range(SIZE):
            d = math.hypot(x - c, y - c) / c
            v = max(0.0, 1 - d) ** 2.2 + 0.6 * max(0.0, 1 - d * 4) ** 2
            px[x, y] = int(min(1.0, v) * 255)
    save(a, "Glow.png")


def ring():
    a = blank()
    px = a.load()
    c = SIZE / 2
    for y in range(SIZE):
        for x in range(SIZE):
            d = math.hypot(x - c, y - c) / c
            v = math.exp(-((d - 0.86) / 0.045) ** 2) + 0.25 * math.exp(-((d - 0.8) / 0.12) ** 2)
            px[x, y] = int(min(1.0, v) * 255)
    save(a, "Ring.png")


def shard():
    a = blank()
    d = ImageDraw.Draw(a)
    c = SIZE / 2
    d.polygon([(c, 8), (c + 46, c - 20), (c, SIZE - 8), (c - 46, c + 20)], fill=150)
    d.polygon([(c, 30), (c + 14, c), (c, SIZE - 40), (c - 14, c)], fill=255)
    save(a.filter(ImageFilter.GaussianBlur(3)), "Shard.png")


# Elder futhark runes as strokes on a unit box (0..1), drawn upright and laid round the ring.
RUNES = {
    "fehu": [((0.3, 0), (0.3, 1)), ((0.3, 0.55), (0.75, 0.25)), ((0.3, 0.8), (0.75, 0.5))],
    "uruz": [((0.3, 1), (0.3, 0)), ((0.3, 0), (0.75, 0.35)), ((0.75, 0.35), (0.75, 1))],
    "thurisaz": [((0.3, 0), (0.3, 1)), ((0.3, 0.3), (0.7, 0.5)), ((0.7, 0.5), (0.3, 0.7))],
    "isa": [((0.5, 0), (0.5, 1))],
    "kenaz": [((0.7, 0.15), (0.3, 0.5)), ((0.3, 0.5), (0.7, 0.85))],
    "algiz": [((0.5, 1), (0.5, 0)), ((0.5, 0.4), (0.2, 0.05)), ((0.5, 0.4), (0.8, 0.05))],
    "tiwaz": [((0.5, 1), (0.5, 0)), ((0.5, 0), (0.2, 0.3)), ((0.5, 0), (0.8, 0.3))],
    "sowilo": [((0.65, 0), (0.35, 0.4)), ((0.35, 0.4), (0.65, 0.6)), ((0.65, 0.6), (0.35, 1))],
}


def rune_circle():
    big = SIZE * 2
    a = Image.new("L", (big, big), 0)
    d = ImageDraw.Draw(a)
    c = big / 2
    for r, w in ((0.97, 10), (0.86, 6), (0.62, 5)):
        rr = r * c
        d.ellipse([c - rr, c - rr, c + rr, c + rr], outline=255, width=w)
    names = list(RUNES)
    random.seed(7)
    count = 16
    for i in range(count):
        ang = i / count * math.tau
        glyph = RUNES[names[i % len(names)]]
        mid = 0.74 * c
        h = 0.085 * c * 2
        cx, cy = c + math.cos(ang) * mid, c + math.sin(ang) * mid
        # Upright along the radius: the glyph's up points outward.
        up = (math.cos(ang), math.sin(ang))
        side = (-up[1], up[0])
        for (x0, y0), (x1, y1) in glyph:
            p0 = (cx + side[0] * (x0 - 0.5) * h + up[0] * (0.5 - y0) * h, cy + side[1] * (x0 - 0.5) * h + up[1] * (0.5 - y0) * h)
            p1 = (cx + side[0] * (x1 - 0.5) * h + up[0] * (0.5 - y1) * h, cy + side[1] * (x1 - 0.5) * h + up[1] * (0.5 - y1) * h)
            d.line([p0, p1], fill=255, width=9)
    # Ticks on the inner ring.
    for i in range(48):
        ang = i / 48 * math.tau
        r0, r1 = 0.62 * c, (0.58 if i % 4 else 0.54) * c
        d.line([(c + math.cos(ang) * r0, c + math.sin(ang) * r0), (c + math.cos(ang) * r1, c + math.sin(ang) * r1)], fill=255, width=5)
    a = a.resize((SIZE, SIZE), Image.LANCZOS)
    soft = a.filter(ImageFilter.GaussianBlur(5))
    save(Image.eval(Image.merge("L", [Image.blend(soft, a, 0.7)]), lambda v: v), "Rune Circle.png")


def ember():
    a = blank()
    px = a.load()
    c = SIZE / 2
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = (x - c) / c, (y - c) / c
            tail = math.exp(-(dx / 0.12) ** 2) * max(0.0, 1 - abs(dy)) * (0.35 if dy > 0 else 0.0)
            core = max(0.0, 1 - math.hypot(dx, dy) * 2.2) ** 1.5
            px[x, y] = int(min(1.0, core + tail) * 255)
    save(a, "Ember.png")


def slash():
    a = blank()
    px = a.load()
    c = SIZE / 2
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = (x - c) / c, (y - c) / c
            r = math.hypot(dx, dy)
            ang = math.atan2(dy, dx)  # the crescent spans the upper half, its middle at the top
            along = (ang + math.pi) / math.pi - 1.0  # -1..1 over the upper half
            if dy > 0.05:
                continue
            span = max(0.0, 1 - abs(along + 0.5) / 0.5) if -1 <= along <= 0 else 0.0
            width = 0.07 + 0.11 * span
            band = math.exp(-((r - 0.8) / width) ** 2)
            edge = math.exp(-((r - 0.86) / 0.025) ** 2) * 0.8
            px[x, y] = int(min(1.0, (band * 0.7 + edge) * span ** 0.6) * 255)
    save(a, "Slash.png")


def streak():
    a = blank()
    px = a.load()
    c = SIZE / 2
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = (x - c) / c, (y - c) / c
            v = math.exp(-(dx / 0.06) ** 2) * max(0.0, 1 - abs(dy)) ** 1.5
            px[x, y] = int(min(1.0, v) * 255)
    save(a, "Streak.png")


if __name__ == "__main__":
    glow()
    ring()
    shard()
    rune_circle()
    ember()
    slash()
    streak()
    print("written to", os.path.abspath(OUT))
