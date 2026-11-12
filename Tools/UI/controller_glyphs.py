"""Controller button glyphs for on-screen prompts (Assets/_Project/Art/UI/Controller Glyphs.png + .json), drawn in numpy
like hearthline_art.py: at four times the size and scaled down, so every edge is smooth.

One 128 px cell per glyph, named <family>_<control> after the Input System's Gamepad controls ("xbox_buttonSouth",
"playstation_dpadUp"), for Xbox, PlayStation and Switch pads. Hearthline look: a dark solid body with a light rim, as the
HUD's type is light with a dark shadow; face buttons keep the colours players know (Xbox letters, PlayStation shapes).
Safe Haven > UI > Build Controller Glyphs makes the TextMesh Pro sprite asset from the two files.

Run from the project root: python Tools/UI/controller_glyphs.py
"""
import json
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
OUT = os.path.join(ROOT, 'Assets', '_Project', 'Art', 'UI')
FONT = os.path.join(ROOT, 'Assets', '_Project', 'UI', 'Fonts', 'FiraSans-SemiBold.ttf')
SS = 4
CELL = 128
COLUMNS = 8

BODY = (0.10, 0.09, 0.075)
RIM = (0.925, 0.894, 0.827)
MUTED = (0.66, 0.62, 0.555)
RIM_WIDTH = 4.5

XBOX = {'A': (0.45, 0.74, 0.36), 'B': (0.89, 0.38, 0.32), 'X': (0.36, 0.60, 0.92), 'Y': (0.95, 0.78, 0.30)}
PS = {'cross': (0.55, 0.68, 0.95), 'circle': (0.93, 0.45, 0.45), 'square': (0.88, 0.58, 0.85), 'triangle': (0.38, 0.80, 0.68)}

Y, X = (np.mgrid[0:CELL * SS, 0:CELL * SS].astype(float) + 0.5) / SS


def cover(d):
    """Coverage of a signed distance (in final pixels, negative inside), one supersampled pixel of soft edge."""
    return np.clip(0.5 - d * SS, 0, 1)


def circle(cx, cy, r):
    return np.hypot(X - cx, Y - cy) - r


def box(cx, cy, hw, hh, r):
    qx = np.abs(X - cx) - (hw - r)
    qy = np.abs(Y - cy) - (hh - r)
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - r


def segment(ax, ay, bx, by, width):
    px, py = X - ax, Y - ay
    vx, vy = bx - ax, by - ay
    h = np.clip((px * vx + py * vy) / (vx * vx + vy * vy), 0, 1)
    return np.hypot(px - vx * h, py - vy * h) - width / 2


def triangle(cx, cy, r):
    """An equilateral triangle pointing up (Inigo Quilez's distance)."""
    k = np.sqrt(3.0)
    px = np.abs(X - cx) - r
    py = -(Y - cy) + r / k
    flip = px + k * py > 0
    nx = np.where(flip, (px - k * py) / 2, px)
    ny = np.where(flip, (-k * px - py) / 2, py)
    nx = nx - np.clip(nx, -2 * r, 0)
    return -np.hypot(nx, ny) * np.sign(ny)


def ring(d, width):
    return np.abs(d) - width / 2


class Glyph:
    def __init__(self):
        self.rgb = np.zeros(X.shape + (3,))
        self.a = np.zeros(X.shape)

    def paint(self, colour, alpha):
        alpha = np.clip(alpha, 0, 1)
        out = alpha + self.a * (1 - alpha)
        safe = np.maximum(out, 1e-6)[..., None]
        self.rgb = (np.array(colour) * alpha[..., None] + self.rgb * (self.a * (1 - alpha))[..., None]) / safe
        self.a = out

    def body(self, d):
        """The button's body: a soft shadow under it, the dark fill and the light rim."""
        self.paint((0, 0, 0), 0.45 * np.clip(1 - np.maximum(d, 0) / 5, 0, 1) * (d > -1))
        self.paint(BODY, 0.95 * cover(d))
        self.paint(RIM, cover(ring(d + RIM_WIDTH / 2, RIM_WIDTH)))

    def text(self, label, size, colour, cy=64.0):
        mask = Image.new('L', (CELL * SS, CELL * SS), 0)
        font = ImageFont.truetype(FONT, int(size * SS))
        # Capitals centred on their cap height (0.69 em in Fira Sans), not on the font's line.
        ImageDraw.Draw(mask).text((64 * SS, (cy + 0.345 * size) * SS), label, font=font, fill=255, anchor='ms')
        self.paint(colour, np.asarray(mask, dtype=float) / 255)

    def image(self):
        rgba = np.dstack([self.rgb, self.a])
        img = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), 'RGBA')
        return img.resize((CELL, CELL), Image.LANCZOS)


def face(label=None, colour=RIM, shape=None):
    g = Glyph()
    g.body(circle(64, 64, 50))
    if shape == 'cross':
        d = np.minimum(segment(50, 50, 78, 78, 7.5), segment(78, 50, 50, 78, 7.5))
        g.paint(colour, cover(d))
    elif shape == 'circle':
        g.paint(colour, cover(ring(circle(64, 64, 18), 7.5)))
    elif shape == 'square':
        g.paint(colour, cover(ring(box(64, 64, 17, 17, 2), 7.5)))
    elif shape == 'triangle':
        g.paint(colour, cover(ring(triangle(64, 68, 22), 7)))
    else:
        g.text(label, 62, colour)
    return g


def shoulder(label):
    g = Glyph()
    g.body(box(64, 64, 57, 30, 15))
    g.text(label, 40, RIM)
    return g


def trigger(label):
    g = Glyph()
    g.body(np.minimum(box(64, 80, 46, 32, 12), circle(64, 64, 46)))
    g.text(label, 40, RIM, cy=72)
    return g


def stick(label, pressed):
    g = Glyph()
    g.body(circle(64, 64, 50))
    if pressed:
        g.paint(RIM, cover(circle(64, 64, 34)))
        g.text(label, 40 if len(label) > 1 else 46, BODY)
    else:
        g.paint(MUTED, 0.6 * cover(ring(circle(64, 64, 34), 3)))
        g.text(label, 40 if len(label) > 1 else 46, RIM)
    return g


def dpad(lit=None):
    g = Glyph()
    plus = np.minimum(box(64, 64, 18, 54, 7), box(64, 64, 54, 18, 7))
    g.body(plus)
    inside = cover(plus + RIM_WIDTH)
    arms = {'Up': -(Y - 64), 'Down': Y - 64, 'Left': -(X - 64), 'Right': X - 64}
    for name, along in arms.items():
        if name == lit:
            g.paint(RIM, inside * np.clip((along - 20) * SS, 0, 1))
    # A small arrow at each arm's end, dark on the lit arm.
    tips = {'Up': (64, 22, 0, -1), 'Down': (64, 106, 0, 1), 'Left': (22, 64, -1, 0), 'Right': (106, 64, 1, 0)}
    for name, (tx, ty, dx, dy) in tips.items():
        # The point at the arm's end, the base 10 px back and 8 px either side.
        bx, by = tx - dx * 10, ty - dy * 10
        g.paint(BODY if name == lit else MUTED, 0.9 * in_triangle((tx, ty), (bx - dy * 8, by + dx * 8), (bx + dy * 8, by - dx * 8)))
    return g


def in_triangle(p0, p1, p2):
    def edge(a, b):
        return (X - a[0]) * (b[1] - a[1]) - (Y - a[1]) * (b[0] - a[0])

    e0, e1, e2 = edge(p0, p1), edge(p1, p2), edge(p2, p0)
    inside = ((e0 >= 0) & (e1 >= 0) & (e2 >= 0)) | ((e0 <= 0) & (e1 <= 0) & (e2 <= 0))
    return inside.astype(float)


def small(shape_d, draw):
    g = Glyph()
    g.body(shape_d)
    draw(g)
    return g


def lines(g, width=16):
    for y in (53, 64, 75):
        g.paint(RIM, cover(box(64, y, width, 2.6, 1.3)))


def view(g):
    g.paint(RIM, cover(ring(box(58, 58, 13, 10, 3), 4)))
    g.paint(RIM, cover(box(70, 70, 13, 10, 3)))


def share(g):
    d = np.minimum(segment(64, 78, 64, 50, 5), np.minimum(segment(54, 60, 64, 50, 5), segment(74, 60, 64, 50, 5)))
    g.paint(RIM, cover(d))


def plus(g):
    g.paint(RIM, cover(np.minimum(box(64, 64, 17, 4, 2), box(64, 64, 4, 17, 2))))


def minus(g):
    g.paint(RIM, cover(box(64, 64, 17, 4, 2)))


def family(name):
    if name == 'xbox':
        faces = {'buttonSouth': face('A', XBOX['A']), 'buttonEast': face('B', XBOX['B']),
                 'buttonWest': face('X', XBOX['X']), 'buttonNorth': face('Y', XBOX['Y'])}
        shoulders, triggers, sticks, presses = ('LB', 'RB'), ('LT', 'RT'), ('LS', 'RS'), ('LS', 'RS')
        start = small(circle(64, 64, 40), lines)
        select = small(circle(64, 64, 40), view)
    elif name == 'playstation':
        faces = {'buttonSouth': face(colour=PS['cross'], shape='cross'), 'buttonEast': face(colour=PS['circle'], shape='circle'),
                 'buttonWest': face(colour=PS['square'], shape='square'), 'buttonNorth': face(colour=PS['triangle'], shape='triangle')}
        shoulders, triggers, sticks, presses = ('L1', 'R1'), ('L2', 'R2'), ('L', 'R'), ('L3', 'R3')
        start = small(box(64, 64, 25, 42, 25), lambda g: lines(g, 11))
        select = small(box(64, 64, 25, 42, 25), share)
    else:
        # Nintendo's letters sit the other way round: the bottom button is B, the right one A.
        faces = {'buttonSouth': face('B'), 'buttonEast': face('A'), 'buttonWest': face('Y'), 'buttonNorth': face('X')}
        shoulders, triggers, sticks, presses = ('L', 'R'), ('ZL', 'ZR'), ('L', 'R'), ('L', 'R')
        start = small(circle(64, 64, 40), plus)
        select = small(circle(64, 64, 40), minus)

    glyphs = dict(faces)
    glyphs['leftShoulder'], glyphs['rightShoulder'] = shoulder(shoulders[0]), shoulder(shoulders[1])
    glyphs['leftTrigger'], glyphs['rightTrigger'] = trigger(triggers[0]), trigger(triggers[1])
    glyphs['start'], glyphs['select'] = start, select
    glyphs['leftStick'], glyphs['rightStick'] = stick(sticks[0], False), stick(sticks[1], False)
    glyphs['leftStickPress'], glyphs['rightStickPress'] = stick(presses[0], True), stick(presses[1], True)
    glyphs['dpad'] = dpad()
    for direction in ('Up', 'Down', 'Left', 'Right'):
        glyphs['dpad' + direction] = dpad(direction)
    return glyphs


def main():
    names, images = [], []
    for fam in ('xbox', 'playstation', 'switch'):
        for control, glyph in family(fam).items():
            names.append(f'{fam}_{control}')
            images.append(glyph.image())

    rows = (len(images) + COLUMNS - 1) // COLUMNS
    size = 1
    while size < max(rows, COLUMNS) * CELL:
        size *= 2
    atlas = Image.new('RGBA', (COLUMNS * CELL, size), (0, 0, 0, 0))
    for i, img in enumerate(images):
        atlas.paste(img, ((i % COLUMNS) * CELL, (i // COLUMNS) * CELL))

    os.makedirs(OUT, exist_ok=True)
    atlas.save(os.path.join(OUT, 'Controller Glyphs.png'))
    with open(os.path.join(OUT, 'Controller Glyphs.json'), 'w', encoding='utf-8') as f:
        json.dump({'cell': CELL, 'columns': COLUMNS, 'names': names}, f, indent=2)
    print(f'{len(names)} glyphs, atlas {atlas.size}')


if __name__ == '__main__':
    main()
