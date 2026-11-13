"""Hearthline 3, the UI kit's art (Assets/_Project/Art/UI/Kit; first made for the horse window), drawn like hearthline_art.py: at four times the
size and scaled down so every edge is smooth, white so UIStyle tints it.

- Hearthline Scroll Corner: the window's gilded corner, scrollwork curling out of the corner along both edges, a leaf
  and a bead at its heart. Placed at the four corners, mirrored.
- Hearthline Card and Card Rim: a rounded card and its rim alone, 9-sliced, for cards, tabs, bars and chips.
- Icons, one flat glyph each, for the stats, the sections and the buttons: Heart, Bolt, Weight, Horseshoe, Bond, Info,
  Stats, Star, Wheat, Brush, Gear, HorseHead, Lead, Stable, Saddle, Hourglass, Pencil, Barding, Bags, Wheel, Stay,
  Graze, Sword, Flame, Snowflake, Sun, Crit, Spark, Axe, Pickaxe, Sickle, Lock, Hammer, Book, Skull, Coin, Eye.
- Kit Icons.png and .json (next to the folder): the icons in one atlas, for TextMesh Pro's <sprite name=...>.

Run from the project root: python Tools/UI/hearthline_kit_art.py
"""
import math
import os

from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Art', 'UI', 'Kit')
SS = 4
ICON = 128
W = (255, 255, 255, 255)
CLEAR = (0, 0, 0, 0)


def canvas(size):
    img = Image.new('RGBA', (size * SS, size * SS), CLEAR)
    return img, ImageDraw.Draw(img)


def save(img, name, size):
    os.makedirs(OUT, exist_ok=True)
    img.resize((size, size), Image.LANCZOS).save(os.path.join(OUT, name + '.png'))


def P(points, s=ICON):
    """Points given on a 0-100 grid, to the drawing's pixels."""
    k = s * SS / 100.0
    return [(x * k, y * k) for x, y in points]


def R(v, s=ICON):
    return v * s * SS / 100.0


def circle(d, cx, cy, r, fill=W, width=0):
    box = [R(cx - r), R(cy - r), R(cx + r), R(cy + r)]
    if width:
        d.ellipse(box, outline=fill, width=int(R(width)))
    else:
        d.ellipse(box, fill=fill)


def line(d, pts, width):
    d.line(P(pts), fill=W, width=int(R(width)), joint='curve')
    for x, y in (pts[0], pts[-1]):
        circle(d, x, y, width / 2)


def heart_shape(d, cx, cy, size, fill=W):
    pts = []
    for i in range(120):
        t = math.tau * i / 120
        x = 16 * math.sin(t) ** 3
        y = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
        pts.append((cx + x * size / 17, cy + y * size / 17))
    d.polygon(P(pts), fill=fill)


def heart():
    img, d = canvas(ICON)
    heart_shape(d, 50, 52, 44)
    save(img, 'Heart', ICON)


def bolt():
    img, d = canvas(ICON)
    d.polygon(P([(58, 4), (20, 56), (46, 56), (36, 96), (80, 38), (53, 38), (66, 4)]), fill=W)
    save(img, 'Bolt', ICON)


def weight():
    img, d = canvas(ICON)
    circle(d, 50, 24, 16, width=8)
    d.polygon(P([(22, 38), (78, 38), (90, 92), (10, 92)]), fill=W)
    d.rounded_rectangle([R(10), R(84), R(90), R(94)], radius=R(4), fill=W)
    save(img, 'Weight', ICON)


def horseshoe():
    img, d = canvas(ICON)
    box = [R(12), R(8), R(88), R(84)]
    d.arc(box, start=150, end=390, fill=W, width=int(R(17)))
    for side in (-1, 1):
        x = 50 + side * 33
        d.rounded_rectangle([R(x - 9), R(62), R(x + 9), R(94)], radius=R(4), fill=W)
    for a in (165, 205, 245, 295, 335, 375):
        t = math.radians(a)
        circle(d, 50 + 29.5 * math.cos(t), 46 + 29.5 * math.sin(t), 3, fill=CLEAR)
    save(img, 'Horseshoe', ICON)


def bond():
    img, d = canvas(ICON)
    heart_shape(d, 40, 50, 34)
    heart_shape(d, 63, 58, 32, fill=CLEAR)
    heart_shape(d, 63, 58, 26)
    save(img, 'Bond', ICON)


def info():
    img, d = canvas(ICON)
    circle(d, 50, 50, 42, width=8)
    circle(d, 50, 30, 6)
    d.rounded_rectangle([R(44), R(42), R(56), R(76)], radius=R(4), fill=W)
    save(img, 'Info', ICON)


def stats():
    img, d = canvas(ICON)
    for x, top in ((14, 56), (40, 30), (66, 10)):
        d.rounded_rectangle([R(x), R(top), R(x + 20), R(90)], radius=R(3), fill=W)
    save(img, 'Stats', ICON)


def star_points(cx, cy, r1, r2, n=5, rot=-90):
    pts = []
    for i in range(n * 2):
        r = r1 if i % 2 == 0 else r2
        t = math.radians(rot + i * 180 / n)
        pts.append((cx + r * math.cos(t), cy + r * math.sin(t)))
    return pts


def star():
    img, d = canvas(ICON)
    d.polygon(P(star_points(50, 54, 46, 19)), fill=W)
    save(img, 'Star', ICON)


def wheat():
    img, d = canvas(ICON)
    line(d, [(50, 96), (50, 20)], 6)
    for i in range(4):
        y = 24 + i * 16
        for side in (-1, 1):
            cx = 50 + side * 11
            box = [R(cx - 8), R(y - 11), R(cx + 8), R(y + 11)]
            grain = Image.new('RGBA', img.size, CLEAR)
            g = ImageDraw.Draw(grain)
            g.ellipse(box, fill=W)
            grain = grain.rotate(side * 30, center=(R(cx), R(y)))
            img.alpha_composite(grain)
    d = ImageDraw.Draw(img)
    circle(d, 50, 12, 7)
    save(img, 'Wheat', ICON)


def brush():
    img, d = canvas(ICON)
    d.rounded_rectangle([R(10), R(22), R(90), R(54)], radius=R(14), fill=W)
    for i in range(8):
        x = 16 + i * 9.6
        d.rounded_rectangle([R(x), R(56), R(x + 5), R(86)], radius=R(2), fill=W)
    d.rounded_rectangle([R(28), R(30), R(72), R(40)], radius=R(4), fill=CLEAR)
    save(img, 'Brush', ICON)


def gear():
    img, d = canvas(ICON)
    teeth = []
    for i in range(16):
        r = 46 if i % 2 == 0 else 34
        for k in (-0.5, 0.5):
            t = math.tau * (i + k * 0.9) / 16
            teeth.append((50 + r * math.cos(t), 50 + r * math.sin(t)))
    d.polygon(P(teeth), fill=W)
    circle(d, 50, 50, 15, fill=CLEAR)
    save(img, 'Gear', ICON)


HORSE_HEAD = [(66, 6), (60, 20), (52, 18), (40, 24), (28, 36), (16, 54), (8, 70), (10, 80), (20, 84), (30, 78),
              (38, 70), (46, 66), (50, 72), (50, 96), (88, 96), (86, 74), (82, 52), (76, 32), (72, 22), (74, 4), (68, 14)]


def horse_head():
    img, d = canvas(ICON)
    d.polygon(P(HORSE_HEAD), fill=W)
    circle(d, 44, 38, 4, fill=CLEAR)
    # The mane's edge, cut into the neck.
    d.line(P([(74, 26), (80, 48), (84, 70), (86, 94)]), fill=CLEAR, width=int(R(3)))
    save(img, 'HorseHead', ICON)


def lead():
    img, d = canvas(ICON)
    d.arc([R(10), R(8), R(66), R(64)], start=0, end=330, fill=W, width=int(R(8)))
    d.arc([R(22), R(20), R(54), R(52)], start=20, end=340, fill=W, width=int(R(6)))
    line(d, [(62, 50), (72, 66), (80, 78), (90, 92)], 9)
    d.rounded_rectangle([R(58), R(42), R(74), R(56)], radius=R(4), fill=W)
    save(img, 'Lead', ICON)


def stable():
    img, d = canvas(ICON)
    d.polygon(P([(50, 6), (96, 44), (88, 44), (88, 94), (12, 94), (12, 44), (4, 44)]), fill=W)
    d.rectangle([R(34), R(54), R(66), R(94)], fill=CLEAR)
    d.line(P([(34, 54), (66, 94)]), fill=W, width=int(R(5)))
    d.line(P([(66, 54), (34, 94)]), fill=W, width=int(R(5)))
    save(img, 'Stable', ICON)


def saddle():
    img, d = canvas(ICON)
    # From the side: the cantle high at the back, the seat dipping, the horn at the front; the skirt under it, a stirrup
    # hanging from its leather.
    seat = [(8, 22), (14, 20), (20, 30), (34, 38), (52, 38), (66, 32), (72, 24), (72, 14), (82, 12), (82, 24), (78, 36)]
    d.polygon(P(seat + [(86, 52), (80, 58), (14, 58), (8, 50), (10, 36)]), fill=W)
    d.line(P([(14, 50), (80, 50)]), fill=CLEAR, width=int(R(3)))
    d.rounded_rectangle([R(43), R(56), R(51), R(76)], radius=R(2), fill=W)
    d.polygon(P([(36, 76), (58, 76), (62, 94), (32, 94)]), fill=W)
    d.polygon(P([(41, 81), (53, 81), (55, 89), (39, 89)]), fill=CLEAR)
    save(img, 'Saddle', ICON)


def hourglass():
    img, d = canvas(ICON)
    d.rounded_rectangle([R(16), R(4), R(84), R(14)], radius=R(3), fill=W)
    d.rounded_rectangle([R(16), R(86), R(84), R(96)], radius=R(3), fill=W)
    d.polygon(P([(24, 14), (76, 14), (54, 50), (76, 86), (24, 86), (46, 50)]), fill=W)
    d.polygon(P([(33, 20), (67, 20), (50, 44)]), fill=CLEAR)
    d.polygon(P([(50, 60), (64, 80), (36, 80)]), fill=CLEAR)
    d.polygon(P([(50, 66), (58, 80), (42, 80)]), fill=W)
    save(img, 'Hourglass', ICON)


def pencil():
    img, d = canvas(ICON)
    body = Image.new('RGBA', img.size, CLEAR)
    b = ImageDraw.Draw(body)
    b.rounded_rectangle([R(40), R(2), R(60), R(70)], radius=R(3), fill=W)
    b.polygon(P([(40, 74), (60, 74), (50, 96)]), fill=W)
    b.rectangle([R(40), R(16), R(60), R(20)], fill=CLEAR)
    body = body.rotate(-40, center=(R(50), R(50)), resample=Image.BICUBIC)
    img.alpha_composite(body)
    save(img, 'Pencil', ICON)


def barding():
    img, d = canvas(ICON)
    d.polygon(P([(50, 4), (90, 16), (86, 56), (50, 96), (14, 56), (10, 16)]), fill=W)
    d.polygon(P([(50, 16), (78, 25), (75, 54), (50, 82), (25, 54), (22, 25)]), fill=CLEAR)
    d.polygon(P([(50, 26), (66, 32), (64, 52), (50, 70), (36, 52), (34, 32)]), fill=W)
    save(img, 'Barding', ICON)


def bags():
    img, d = canvas(ICON)
    d.rounded_rectangle([R(12), R(30), R(88), R(94)], radius=R(12), fill=W)
    d.arc([R(30), R(4), R(70), R(44)], start=180, end=360, fill=W, width=int(R(8)))
    d.rounded_rectangle([R(12), R(30), R(88), R(52)], radius=R(8), fill=CLEAR)
    d.rounded_rectangle([R(14), R(32), R(86), R(50)], radius=R(7), fill=W)
    d.rectangle([R(12), R(50), R(88), R(55)], fill=CLEAR)
    d.rounded_rectangle([R(44), R(46), R(56), R(64)], radius=R(3), fill=W)
    save(img, 'Bags', ICON)


def wheel():
    img, d = canvas(ICON)
    circle(d, 50, 50, 42, width=9)
    circle(d, 50, 50, 10)
    for i in range(8):
        t = math.tau * i / 8
        line(d, [(50, 50), (50 + 38 * math.cos(t), 50 + 38 * math.sin(t))], 6)
    save(img, 'Wheel', ICON)


def stay():
    img, d = canvas(ICON)
    # A tethering post: a stake in the ground, a ring on it.
    d.rounded_rectangle([R(42), R(6), R(58), R(84)], radius=R(4), fill=W)
    d.polygon(P([(42, 84), (58, 84), (50, 98)]), fill=W)
    circle(d, 50, 30, 20, width=7)
    d.rounded_rectangle([R(10), R(88), R(90), R(94)], radius=R(3), fill=W)
    save(img, 'Stay', ICON)


def graze():
    img, d = canvas(ICON)
    for x0, lean, h in ((22, -14, 60), (36, -6, 78), (50, 0, 90), (64, 6, 76), (78, 14, 58)):
        d.polygon(P([(x0 - 6, 96), (x0 + 6, 96), (x0 + lean, 96 - h)]), fill=W)
    save(img, 'Graze', ICON)


def scroll_corner():
    """The gilded corner, top left: two scrolls run out along the edges from a bead in the corner, each ending in a curl,
    with a leaf between them."""
    size = 192
    img, d = canvas(size)
    k = size * SS / 100.0

    def S(v):
        return v * k

    def spiral(cx, cy, r0, turns, start, direction, width):
        pts = []
        steps = int(120 * turns)
        for i in range(steps + 1):
            t = i / steps
            a = start + direction * t * turns * math.tau
            r = r0 * (1 - 0.82 * t)
            pts.append((S(cx + r * math.cos(a)), S(cy + r * math.sin(a))))
        for i in range(len(pts) - 1):
            w = width * (1 - 0.6 * i / len(pts))
            d.line([pts[i], pts[i + 1]], fill=W, width=max(1, int(S(w))))

    def stroke(pts, width_from, width_to):
        n = len(pts) - 1
        for i in range(n):
            w = width_from + (width_to - width_from) * i / n
            d.line([(S(pts[i][0]), S(pts[i][1])), (S(pts[i + 1][0]), S(pts[i + 1][1]))], fill=W, width=max(1, int(S(w))))
            r = S(w) / 2
            d.ellipse([S(pts[i][0]) - r, S(pts[i][1]) - r, S(pts[i][0]) + r, S(pts[i][1]) + r], fill=W)

    # The frame's own two edges, thin, running off the corner.
    stroke([(4, 4), (100, 4)], 1.6, 1.0)
    stroke([(4, 4), (4, 100)], 1.6, 1.0)
    # Along the top: a long sweep out of the corner that rolls into a curl.
    top = [(8, 10)] + [(8 + 50 * t, 10 + 7 * math.sin(t * math.pi)) for t in [i / 20 for i in range(1, 21)]]
    stroke(top, 3.6, 2.0)
    spiral(66, 11, 8, 1.15, math.pi * 0.95, 1, 2.0)
    # Down the side, its mirror.
    side = [(10, 8)] + [(10 + 7 * math.sin(t * math.pi), 8 + 50 * t) for t in [i / 20 for i in range(1, 21)]]
    stroke(side, 3.6, 2.0)
    spiral(11, 66, 8, 1.15, -math.pi * 0.45, -1, 2.0)
    # Two smaller curls branching inwards from the sweeps.
    spiral(34, 22, 6, 1.0, -math.pi * 0.5, -1, 1.6)
    spiral(22, 34, 6, 1.0, math.pi, 1, 1.6)
    # A leaf out of the corner, along the diagonal.
    leaf = []
    for i in range(41):
        t = i / 40
        leaf.append((12 + 26 * t + 5 * math.sin(t * math.pi), 12 + 26 * t - 5 * math.sin(t * math.pi)))
    for i in range(40, -1, -1):
        t = i / 40
        leaf.append((12 + 26 * t - 5 * math.sin(t * math.pi), 12 + 26 * t + 5 * math.sin(t * math.pi)))
    d.polygon([(S(x), S(y)) for x, y in leaf], fill=W)
    d.line([(S(14), S(14)), (S(34), S(34))], fill=CLEAR, width=int(S(0.9)))
    # The bead in the corner.
    r = S(5.2)
    d.ellipse([S(9) - r, S(9) - r, S(9) + r, S(9) + r], fill=W)
    r = S(2.4)
    d.ellipse([S(9) - r, S(9) - r, S(9) + r, S(9) + r], fill=CLEAR)
    save(img, 'Hearthline Scroll Corner', size)


def sword():
    img, d = canvas(ICON)
    blade = Image.new('RGBA', img.size, CLEAR)
    b = ImageDraw.Draw(blade)
    b.polygon(P([(44, 70), (44, 14), (50, 2), (56, 14), (56, 70)]), fill=W)
    b.rounded_rectangle([R(26), R(68), R(74), R(76)], radius=R(3), fill=W)
    b.rounded_rectangle([R(46), R(76), R(54), R(92)], radius=R(2), fill=W)
    b.ellipse([R(44), R(90), R(56), R(100)], fill=W)
    blade = blade.rotate(-45, center=(R(50), R(50)), resample=Image.BICUBIC)
    img.alpha_composite(blade)
    save(img, 'Sword', ICON)


def flame():
    img, d = canvas(ICON)

    def teardrop(cx, cy, w, h, m=1.6, steps=160):
        pts = []
        for i in range(steps):
            t = math.tau * i / steps
            x = math.sin(t) * math.sin(t / 2) ** m
            y = math.cos(t)
            pts.append((cx + x * w, cy - y * h))
        return pts

    d.polygon(P(teardrop(50, 56, 44, 44)), fill=W)
    d.polygon(P(teardrop(50, 70, 20, 24)), fill=CLEAR)
    d.polygon(P(teardrop(50, 76, 10, 14)), fill=W)
    save(img, 'Flame', ICON)


def snowflake():
    img, d = canvas(ICON)
    for k in range(6):
        t = math.tau * k / 6
        ex, ey = 50 + 44 * math.cos(t), 50 + 44 * math.sin(t)
        line(d, [(50, 50), (ex, ey)], 7)
        for f in (0.55, 0.8):
            bx, by = 50 + 44 * f * math.cos(t), 50 + 44 * f * math.sin(t)
            for side in (-1, 1):
                u = t + side * math.radians(45)
                line(d, [(bx, by), (bx + 12 * math.cos(u), by + 12 * math.sin(u))], 5)
    save(img, 'Snowflake', ICON)


def sun():
    img, d = canvas(ICON)
    circle(d, 50, 50, 20)
    for k in range(12):
        t = math.tau * k / 12
        r1, r2 = 28, 44 if k % 2 == 0 else 38
        line(d, [(50 + r1 * math.cos(t), 50 + r1 * math.sin(t)), (50 + r2 * math.cos(t), 50 + r2 * math.sin(t))], 6)
    save(img, 'Sun', ICON)


def crit():
    img, d = canvas(ICON)
    d.polygon(P(star_points(50, 50, 48, 16, n=8, rot=-90)), fill=W)
    circle(d, 50, 50, 9, fill=CLEAR)
    save(img, 'Crit', ICON)


def spark():
    img, d = canvas(ICON)
    d.polygon(P(star_points(50, 50, 46, 9, n=4, rot=-90)), fill=W)
    d.polygon(P(star_points(78, 22, 14, 4, n=4, rot=-90)), fill=W)
    d.polygon(P(star_points(22, 78, 11, 3, n=4, rot=-90)), fill=W)
    save(img, 'Spark', ICON)


def axe():
    img, d = canvas(ICON)
    tool = Image.new('RGBA', img.size, CLEAR)
    t = ImageDraw.Draw(tool)
    t.rounded_rectangle([R(52), R(6), R(60), R(96)], radius=R(3), fill=W)
    t.polygon(P([(54, 12), (30, 4), (18, 18), (16, 34), (24, 48), (54, 38)]), fill=W)
    tool = tool.rotate(-25, center=(R(50), R(50)), resample=Image.BICUBIC)
    img.alpha_composite(tool)
    save(img, 'Axe', ICON)


def pickaxe():
    img, d = canvas(ICON)
    tool = Image.new('RGBA', img.size, CLEAR)
    t = ImageDraw.Draw(tool)
    t.rounded_rectangle([R(46), R(18), R(54), R(96)], radius=R(3), fill=W)
    t.arc([R(8), R(10), R(92), R(60)], start=200, end=340, fill=W, width=int(R(9)))
    tool = tool.rotate(-35, center=(R(50), R(50)), resample=Image.BICUBIC)
    img.alpha_composite(tool)
    save(img, 'Pickaxe', ICON)


def sickle():
    img, d = canvas(ICON)
    blade = Image.new('RGBA', img.size, CLEAR)
    b = ImageDraw.Draw(blade)
    b.ellipse([R(18), R(4), R(90), R(76)], fill=W)
    b.ellipse([R(30), R(0), R(98), R(64)], fill=CLEAR)
    img.alpha_composite(blade)
    d = ImageDraw.Draw(img)
    d.polygon(P([(26, 64), (34, 70), (18, 98), (10, 94)]), fill=W)
    save(img, 'Sickle', ICON)


def card():
    """A rounded card, filled, and its rim alone: 9-sliced (border 20 px), for cards, tabs, bars and chips."""
    size = 64
    img, d = canvas(size)
    d.rounded_rectangle([0, 0, size * SS - 1, size * SS - 1], radius=14 * SS, fill=W)
    save(img, 'Hearthline Card', size)
    img, d = canvas(size)
    d.rounded_rectangle([SS, SS, size * SS - 1 - SS, size * SS - 1 - SS], radius=13 * SS, outline=W, width=int(1.6 * SS))
    save(img, 'Hearthline Card Rim', size)


def lock():
    img, d = canvas(ICON)
    d.arc([R(26), R(6), R(74), R(56)], start=180, end=360, fill=W, width=int(R(9)))
    d.rectangle([R(26), R(30), R(35), R(46)], fill=W)
    d.rectangle([R(65), R(30), R(74), R(46)], fill=W)
    d.rounded_rectangle([R(14), R(42), R(86), R(96)], radius=R(8), fill=W)
    circle(d, 50, 64, 7, fill=CLEAR)
    d.rectangle([R(47), R(66), R(53), R(82)], fill=CLEAR)
    save(img, 'Lock', ICON)


def hammer():
    img, d = canvas(ICON)
    tool = Image.new('RGBA', img.size, CLEAR)
    t = ImageDraw.Draw(tool)
    t.rounded_rectangle([R(45), R(26), R(55), R(98)], radius=R(3), fill=W)
    t.rounded_rectangle([R(18), R(8), R(82), R(32)], radius=R(4), fill=W)
    tool = tool.rotate(35, center=(R(50), R(50)), resample=Image.BICUBIC)
    img.alpha_composite(tool)
    save(img, 'Hammer', ICON)


def book():
    img, d = canvas(ICON)
    d.polygon(P([(50, 20), (8, 10), (8, 84), (50, 94)]), fill=W)
    d.polygon(P([(50, 20), (92, 10), (92, 84), (50, 94)]), fill=W)
    d.line(P([(50, 22), (50, 92)]), fill=CLEAR, width=int(R(3)))
    for y in (32, 46, 60):
        d.line(P([(16, y - 6), (42, y)]), fill=CLEAR, width=int(R(3)))
        d.line(P([(84, y - 6), (58, y)]), fill=CLEAR, width=int(R(3)))
    save(img, 'Book', ICON)


def skull():
    img, d = canvas(ICON)
    d.ellipse([R(14), R(6), R(86), R(74)], fill=W)
    d.rounded_rectangle([R(30), R(58), R(70), R(92)], radius=R(6), fill=W)
    circle(d, 35, 42, 11, fill=CLEAR)
    circle(d, 65, 42, 11, fill=CLEAR)
    d.polygon(P([(50, 54), (44, 66), (56, 66)]), fill=CLEAR)
    for x in (40, 50, 60):
        d.rectangle([R(x - 1.5), R(78), R(x + 1.5), R(92)], fill=CLEAR)
    save(img, 'Skull', ICON)


def coin():
    img, d = canvas(ICON)
    circle(d, 50, 50, 44)
    circle(d, 50, 50, 34, fill=CLEAR)
    circle(d, 50, 50, 28)
    d.polygon(P([(50, 30), (60, 50), (50, 70), (40, 50)]), fill=CLEAR)
    save(img, 'Coin', ICON)


def eye():
    img, d = canvas(ICON)
    top = [(6 + 88 * t, 50 - 34 * math.sin(math.pi * t)) for t in [i / 40 for i in range(41)]]
    bottom = [(94 - 88 * t, 50 + 34 * math.sin(math.pi * t)) for t in [i / 40 for i in range(41)]]
    d.polygon(P(top + bottom), fill=W)
    circle(d, 50, 50, 20, fill=CLEAR)
    circle(d, 50, 50, 12)
    save(img, 'Eye', ICON)


def atlas():
    """Every icon in one atlas for TextMesh Pro (Art/UI/Kit Icons.png and .json), so text can carry them:
    <sprite name="Bags" tint=1>. HearthKitArtInstaller builds the sprite asset from it."""
    import json
    names = sorted(n[:-4] for n in os.listdir(OUT) if n.endswith('.png') and not n.startswith('Hearthline'))
    cell, columns = 128, 8
    rows = (len(names) + columns - 1) // columns
    sheet = Image.new('RGBA', (cell * columns, cell * rows), CLEAR)
    for i, name in enumerate(names):
        icon = Image.open(os.path.join(OUT, name + '.png')).convert('RGBA').resize((cell - 16, cell - 16), Image.LANCZOS)
        sheet.alpha_composite(icon, ((i % columns) * cell + 8, (i // columns) * cell + 8))
    parent = os.path.dirname(OUT)
    sheet.save(os.path.join(parent, 'Kit Icons.png'))
    with open(os.path.join(parent, 'Kit Icons.json'), 'w') as f:
        json.dump({'cell': cell, 'columns': columns, 'names': names}, f)


if __name__ == '__main__':
    for make in (heart, bolt, weight, horseshoe, bond, info, stats, star, wheat, brush, gear, horse_head, lead, stable,
                 saddle, hourglass, pencil, barding, bags, wheel, stay, graze, sword, flame, snowflake, sun, crit, spark, axe,
                 pickaxe, sickle, lock, hammer, book, skull, coin, eye, scroll_corner, card):
        make()
    atlas()
    print('kit art ->', os.path.abspath(OUT))
