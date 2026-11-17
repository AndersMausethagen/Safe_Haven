"""Lily pads and white water lilies for the Moss Marsh's still water, modelled and textured in Blender for Safe Haven.

Nymphaea, the white water lily of Nordic tarns and bog pools: round floating leaves cut by a narrow V from the rim to the
stalk, slightly cupped with a wavy edge, the odd one rolling its rim up to show the purple-red underside, the old ones
yellowed, spotted brown and torn; a white flower of layered pointed petals round a golden crown of stamens, sitting on a
pad; and a closed bud, green and bronze with a white tip, standing on its stalk out of the water. Stylised low-poly
(60-200 triangles a piece), real metres, each piece's pivot at the waterline (the pad's centre, where its stalk meets it;
Y up is the water's surface). The game lays them in clumps as one mesh per map (RaidLilyPads), so nothing here is a
card: the outline is the geometry and the textures are opaque.

- Lily Pad A: small (0.30 m), fresh, flat with a slight cup.
- Lily Pad B: middling (0.44 m), fresh and darker, a wavy edge.
- Lily Pad C: large (0.54 m), its far rim rolled up (the purple-red underside on its own faces), yellowing at the edge.
- Lily Pad D: old (0.50 m): sagging and crinkled, yellowed, brown spots, a torn slit and a bitten rim gone brown.
- Water Lily: the flower, 0.13 m across: four sepals, eight outer and eight inner petals, the stamen crown.
- Lily Bud: a closed bud leaning on its stalk, 9 cm out of the water.

Textures are worked out in numpy (radiating, forking veins and their fine net; a darker margin round the rim and down the
notch; sun-bleached yellowing; brown spots) into one shared 1024 atlas: LilyPads_Albedo.png (sRGB, no alpha),
LilyPads_Normal.png (the veins as grooves, OpenGL/Unity convention) and LilyPads_ORM.png (512; occlusion, roughness,
metallic: waxy pads, matte petals; the wet look is the shader's smoothness, never painted into the colour).

Writes Assets/_Project/Art/Vegetation/Moss Marsh/Lily Pads/ (Lily Pads.fbx and the textures), a preview render to the path
given (four panels: the pieces close, a clump from the action camera, at eye level, and from the default game camera) with
each panel beside it, and the .blend beside this script.

Run: blender -b --factory-startup --python Tools/Blender/lily_pads.py -- <project root> <preview.png>
"""
import math
import os
import random
import sys

import bpy
import numpy
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else os.getcwd()
PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "lily_pads_preview.png")
ART = os.path.join(ROOT, "Assets", "_Project", "Art", "Vegetation", "Moss Marsh", "Lily Pads")
ASSET = "LilyPads"
S = 1024            # atlas size in pixels
DISC = 0.46         # a pad's rim lies this share of its cell from the cell's centre
UP = Vector((0.0, 0.0, 1.0))

# Atlas cells in pixels: (column from, row from, column to, row to), rows counted from the bottom as Blender stores them.
CELLS = {
    "A": (0, 0, 448, 448),
    "B": (448, 0, 896, 448),
    "C": (0, 448, 448, 896),
    "D": (448, 448, 896, 896),
    "petal": (896, 0, 1024, 256),
    "sepal": (896, 256, 1024, 512),
    "bud": (896, 512, 1024, 768),
    "stalk": (896, 768, 1024, 896),
    "stamens": (0, 896, 128, 1024),
    "stigma": (128, 896, 256, 1024),
    "underside": (256, 896, 384, 1024),
}


def srgb(*c):
    return numpy.array(c, dtype=numpy.float32)


# ----- Pads: the shapes the geometry and the textures share ---------------------------------------------------------

class PadSpec:
    """One pad: size, outline and shape. Angles in degrees; 0 is the notch, which the texture knows too."""

    def __init__(self, key, radius, segments, rings, notch, cup, wave, waves, seed, curl=None, slit=None, bite=None,
                 colours=None, yellowing=0.2, spots=0, sag=0.0):
        self.key, self.radius, self.segments, self.rings = key, radius, segments, rings
        self.notch, self.cup, self.wave, self.waves, self.seed = notch, cup, wave, waves, seed
        self.curl = curl      # (centre angle, half width, start share of the radius, most turn in degrees)
        self.slit = slit      # (angle, inner end as share of the radius, half gap at the rim in degrees)
        self.bite = bite      # (from angle, to angle, depth as share of the radius)
        self.colours = colours
        self.yellowing, self.spots, self.sag = yellowing, spots, sag


FRESH = {"dark": srgb(0.12, 0.21, 0.07), "mid": srgb(0.2, 0.31, 0.105), "light": srgb(0.26, 0.37, 0.13),
         "vein": srgb(0.29, 0.39, 0.15), "rim": srgb(0.16, 0.13, 0.065), "yellow": srgb(0.45, 0.43, 0.16)}
DEEP = {"dark": srgb(0.1, 0.18, 0.07), "mid": srgb(0.16, 0.27, 0.1), "light": srgb(0.21, 0.32, 0.12),
        "vein": srgb(0.24, 0.34, 0.14), "rim": srgb(0.17, 0.11, 0.07), "yellow": srgb(0.42, 0.41, 0.16)}
OLD = {"dark": srgb(0.16, 0.2, 0.07), "mid": srgb(0.25, 0.3, 0.1), "light": srgb(0.32, 0.36, 0.13),
       "vein": srgb(0.34, 0.38, 0.15), "rim": srgb(0.2, 0.13, 0.06), "yellow": srgb(0.47, 0.42, 0.15)}

PADS = [
    PadSpec("A", 0.15, 18, [0.5, 1.0], notch=9, cup=0.07, wave=0.035, waves=5, seed=11, colours=FRESH, yellowing=0.12),
    PadSpec("B", 0.22, 22, [0.42, 0.78, 1.0], notch=7, cup=0.06, wave=0.07, waves=7, seed=23, colours=DEEP,
            yellowing=0.18),
    PadSpec("C", 0.27, 20, [0.45, 0.7, 0.86, 1.0], notch=8, cup=0.05, wave=0.04, waves=6, seed=37, colours=FRESH,
            curl=(160.0, 62.0, 0.7, 85.0), yellowing=0.45),
    PadSpec("D", 0.25, 20, [0.45, 0.78, 1.0], notch=11, cup=0.04, wave=0.09, waves=9, seed=41, colours=OLD,
            curl=(295.0, 32.0, 0.76, 45.0), slit=(215.0, 0.5, 1.6), bite=(80.0, 118.0, 0.16), yellowing=0.7,
            spots=9, sag=0.012),
]


def wrap(a):
    return (a + math.pi) % (2.0 * math.pi) - math.pi


def bell(x):
    """1 at 0 falling smoothly to 0 at |x| = 1."""
    x = min(1.0, abs(x))
    return 0.5 + 0.5 * math.cos(math.pi * x)


def rim_shape(spec, theta):
    """The rim's distance from the centre at an angle (radians), as a share of the radius: a slight irregular wobble, and
    the old pad's bite taken out of it."""
    rng = random.Random(spec.seed)
    wobble = sum(rng.uniform(0.006, 0.014) * math.sin(k * theta + rng.uniform(0, 6.28)) for k in (2, 3, 5, 8))
    r = 1.0 + wobble
    if spec.bite:
        a0, a1, depth = math.radians(spec.bite[0]), math.radians(spec.bite[1]), spec.bite[2]
        mid, half = (a0 + a1) * 0.5, (a1 - a0) * 0.5
        t = abs(wrap(theta - mid)) / half
        if t < 1.0:
            jag = 0.75 + 0.5 * random.Random(int(theta * 1000) + spec.seed).random()
            r -= depth * bell(t) ** 0.6 * jag
    return r


# ----- Textures -----------------------------------------------------------------------------------------------------

def value_noise(shape, scale, seed, octaves=4, gain=0.5):
    """Smooth value noise in [0, 1] with features about scale pixels across, fractal over octaves."""
    h, w = shape
    rng = numpy.random.default_rng(seed)
    total = numpy.zeros(shape, dtype=numpy.float32)
    amp, norm = 1.0, 0.0
    for octave in range(octaves):
        s = max(1.5, scale / (2 ** octave))
        grid = rng.random((int(h / s) + 3, int(w / s) + 3)).astype(numpy.float32)
        ys, xs = numpy.arange(h) / s, numpy.arange(w) / s
        y0, x0 = ys.astype(int), xs.astype(int)
        ty, tx = ys - y0, xs - x0
        ty, tx = (ty * ty * (3 - 2 * ty))[:, None], (tx * tx * (3 - 2 * tx))[None, :]
        rows0, rows1 = grid[y0], grid[y0 + 1]
        a, b, c, d = rows0[:, x0], rows0[:, x0 + 1], rows1[:, x0], rows1[:, x0 + 1]
        total += ((a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty) * amp
        norm += amp
        amp *= gain
    return total / norm


def smooth(a, b, x):
    t = numpy.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def mix(a, b, t):
    return a + (b - a) * t[..., None]


def disc(cell):
    c0, r0, c1, r1 = cell
    w, h = c1 - c0, r1 - r0
    xs = (numpy.arange(w) + 0.5 - w / 2) / (w * DISC)
    ys = (numpy.arange(h) + 0.5 - h / 2) / (h * DISC)
    x, y = numpy.meshgrid(xs.astype(numpy.float32), ys.astype(numpy.float32))
    return numpy.hypot(x, y), numpy.arctan2(y, x), w * DISC


def radial_veins(r, theta, rpx, rng, count, width=2.4):
    """Veins radiating from the centre, curving a little and forking towards the rim; 1 on a vein."""
    veins = numpy.zeros_like(r)
    rr = numpy.minimum(r, 1.0)
    w = width * (1.0 - 0.55 * rr) + 0.7
    start = rng.uniform(0, 2 * math.pi)
    gap = 2 * math.pi / count
    for i in range(count):
        angle = start + gap * i + rng.uniform(-0.12, 0.12) * gap
        curve = rng.uniform(-0.06, 0.06)
        # Each vein forks in two towards the rim, the pair parting by under half the gap to the next vein.
        fork, spread = rng.uniform(0.45, 0.72), rng.uniform(0.18, 0.3) * gap
        phi = angle + curve * rr * rr
        beyond = numpy.clip(rr - fork, 0.0, None) / (1.0 - fork)
        part = smooth(0.0, 1.0, beyond) * spread
        for side in (-1.0, 1.0):
            branch = phi + side * part
            d = numpy.abs((theta - branch + math.pi) % (2 * math.pi) - math.pi) * r * rpx
            veins = numpy.maximum(veins, numpy.exp(-(d / (w * (1.0 - 0.25 * smooth(0.0, 0.3, beyond)))) ** 2))
    return veins * (1.0 - 0.45 * rr)


def net(shape, seed, scale=10.0):
    """The fine net of veinlets between the veins: thin ridges of noise."""
    n = value_noise(shape, scale, seed, octaves=2, gain=0.4)
    return (1.0 - numpy.abs(2.0 * n - 1.0)) ** 7


def pad_texture(spec):
    cell = CELLS[spec.key]
    r, theta, rpx = disc(cell)
    shape = r.shape
    rng = numpy.random.default_rng(spec.seed)
    col = spec.colours
    rr = numpy.minimum(r, 1.0)
    blade = value_noise(shape, rpx * 0.5, spec.seed + 1)
    albedo = mix(col["dark"], col["mid"], smooth(0.2, 0.6, blade))
    albedo = mix(albedo, col["light"], smooth(0.55, 0.9, blade) * 0.6)
    veins = radial_veins(r, theta, rpx, rng, 22 if spec.radius > 0.2 else 18)
    fine = net(shape, spec.seed + 2) * (1.0 - veins)
    albedo = mix(albedo, col["vein"], veins * 0.55 + fine * 0.12)
    # A paler hub where the stalk meets the leaf.
    albedo = mix(albedo, col["light"], smooth(0.07, 0.0, r) * 0.6)
    # Sun-bleached yellowing in patches, most of it out towards the rim.
    patches = value_noise(shape, rpx * 0.45, spec.seed + 3)
    yellow = smooth(0.62 - spec.yellowing * 0.3, 0.8 - spec.yellowing * 0.25, patches) * (0.25 + 0.75 * rr ** 1.5)
    yellow = numpy.clip(yellow * (0.35 + spec.yellowing), 0.0, 1.0)
    albedo = mix(albedo, col["yellow"], yellow * (0.65 + 0.35 * veins))
    # A darker margin round the rim and down both sides of the notch (past the rim the margin's colour carries on, so
    # the cut edge always lands in it).
    notch = math.radians(spec.notch)
    beside_notch = r * numpy.sin(numpy.clip(numpy.abs(theta) - notch, 0.0, math.pi / 2))
    margin = numpy.maximum(smooth(0.9, 0.985, r), smooth(0.035, 0.006, beside_notch) * smooth(0.03, 0.1, r))
    rough = numpy.full(shape, 0.38, dtype=numpy.float32)
    height = -0.8 * veins - 0.25 * fine + 0.5 * (value_noise(shape, rpx * 0.18, spec.seed + 4) - 0.5)
    # Torn and bitten: browned and dried at the new edges.
    dead = numpy.zeros(shape, dtype=numpy.float32)
    if spec.slit:
        angle, inner, _ = spec.slit
        d = numpy.abs((theta - math.radians(angle) + math.pi) % (2 * math.pi) - math.pi) * r
        dead = numpy.maximum(dead, smooth(0.085, 0.012, d) * smooth(inner - 0.1, inner + 0.05, r))
    if spec.bite:
        a0, a1, depth = spec.bite
        mid, half = math.radians((a0 + a1) * 0.5), math.radians((a1 - a0) * 0.5)
        t = numpy.abs((theta - mid + math.pi) % (2 * math.pi) - math.pi) / (half * 1.25)
        dead = numpy.maximum(dead, (0.5 + 0.5 * numpy.cos(math.pi * numpy.clip(t, 0, 1))) *
                             smooth(1.0 - depth - 0.16, 1.0 - depth + 0.02, r))
    if dead.any():
        ragged = value_noise(shape, rpx * 0.08, spec.seed + 5)
        dead = numpy.clip(dead * (0.75 + 0.6 * ragged), 0.0, 1.0)
        albedo = mix(albedo, srgb(0.27, 0.18, 0.08), smooth(0.15, 0.5, dead))
        albedo = mix(albedo, srgb(0.13, 0.085, 0.04), smooth(0.6, 0.95, dead))
        rough = numpy.maximum(rough, 0.4 + 0.35 * dead)
        height += 0.4 * (ragged - 0.5) * dead
    albedo = mix(albedo, col["rim"], margin * 0.85)
    rough = numpy.maximum(rough, 0.38 + 0.1 * margin + 0.18 * yellow)
    # Brown spots on the old pad: a dried patch inside a darker ring.
    xs = r * numpy.cos(theta)
    ys = r * numpy.sin(theta)
    for _ in range(spec.spots):
        while True:
            pr, pa = math.sqrt(rng.uniform(0.04, 0.8)), rng.uniform(0, 2 * math.pi)
            if abs(wrap(pa)) > notch + 0.25:
                break
        size = rng.uniform(0.018, 0.06)
        d = numpy.hypot(xs - pr * math.cos(pa), ys - pr * math.sin(pa)) / size
        d = d * (0.85 + 0.3 * value_noise(shape, rpx * 0.05, int(rng.integers(1 << 30)), octaves=2))
        albedo = mix(albedo, srgb(0.33, 0.22, 0.1), smooth(1.05, 0.7, d))
        albedo = mix(albedo, srgb(0.15, 0.1, 0.05), smooth(0.35, 0.0, numpy.abs(d - 1.0)) * 0.85)
        rough = numpy.maximum(rough, 0.72 * smooth(1.1, 0.8, d))
        height += 0.35 * smooth(0.3, 0.0, numpy.abs(d - 1.0))
    # A few faint dark specks (algae, dried droplets) on every pad.
    specks = value_noise(shape, 2.5, spec.seed + 6, octaves=1)
    albedo *= (1.0 - 0.18 * smooth(0.86, 0.95, specks))[..., None]
    return albedo, height, rough


def underside_texture():
    r, theta, rpx = disc(CELLS["underside"])
    rng = numpy.random.default_rng(5)
    base = value_noise(r.shape, rpx * 0.4, 51)
    albedo = mix(srgb(0.22, 0.085, 0.09), srgb(0.31, 0.12, 0.12), base)
    veins = radial_veins(r, theta, rpx, rng, 16, width=1.6)
    albedo = mix(albedo, srgb(0.4, 0.21, 0.17), veins * 0.7)
    albedo = mix(albedo, srgb(0.15, 0.06, 0.06), smooth(0.9, 1.0, r) * 0.7)
    return albedo, 1.0 * veins, numpy.full(r.shape, 0.55, dtype=numpy.float32)


def grid(cell):
    c0, r0, c1, r1 = cell
    w, h = c1 - c0, r1 - r0
    u, v = numpy.meshgrid((numpy.arange(w) + 0.5) / w, (numpy.arange(h) + 0.5) / h)
    return u.astype(numpy.float32), v.astype(numpy.float32)


def petal_texture(cell, seed, base_colour, body, tip=None, margin=None):
    """A petal or sepal along v (0 at its base): its body colour, a tint at the base, fine veins fanning from the base,
    and optionally a darker margin and tip."""
    u, v = grid(cell)
    albedo = numpy.broadcast_to(body, u.shape + (3,)).copy()
    albedo = mix(albedo, base_colour, smooth(0.42, 0.0, v))
    lines = numpy.zeros_like(u)
    for k in range(-5, 6):
        centre = 0.5 + k * 0.085 * (0.25 + 0.75 * v)
        lines = numpy.maximum(lines, numpy.exp(-((u - centre) * cell_width(cell) / 1.3) ** 2))
    lines *= 0.55 + 0.45 * value_noise(u.shape, 12, seed)
    albedo *= (1.0 - 0.07 * lines)[..., None]
    if margin is not None:
        edge = numpy.abs(u - 0.5) * 2.0
        albedo = mix(albedo, margin, smooth(0.45, 0.95, edge + 0.25 * v))
    if tip is not None:
        albedo = mix(albedo, tip, smooth(0.78, 1.0, v))
    albedo *= (0.94 + 0.08 * value_noise(u.shape, 20, seed + 1))[..., None]
    return albedo, -0.5 * lines, None


def cell_width(cell):
    return cell[2] - cell[0]


def bud_texture():
    """Around the bud (u, four sepals) from its foot to its tip (v): green-bronze sepals that part near the top to show
    the white petals."""
    u, v = grid(CELLS["bud"])
    local = (u * 4.0) % 1.0 - 0.5
    half = 0.5 * (1.0 - smooth(0.5, 1.0, v) ** 0.8) + 0.02
    sepal = smooth(half + 0.03, half - 0.03, numpy.abs(local))
    green = mix(srgb(0.15, 0.24, 0.085), srgb(0.22, 0.31, 0.11), value_noise(u.shape, 16, 61))
    green = mix(green, srgb(0.3, 0.17, 0.1), smooth(half - 0.18, half - 0.02, numpy.abs(local)) * 0.8)
    petal = mix(srgb(0.62, 0.64, 0.5), srgb(0.8, 0.79, 0.72), smooth(0.55, 0.9, v))
    albedo = mix(petal, green, sepal)
    ridge = numpy.exp(-(local * 28.0) ** 2)
    return albedo, 0.4 * ridge - 0.6 * (1.0 - sepal), None


def stalk_texture():
    u, v = grid(CELLS["stalk"])
    stripes = 0.5 + 0.5 * numpy.sin(u * math.pi * 2 * 6 + 2.0 * value_noise(u.shape, 30, 71))
    albedo = mix(srgb(0.2, 0.15, 0.09), srgb(0.29, 0.22, 0.12), stripes)
    return albedo, 0.3 * stripes, None


def stamen_texture():
    u, v = grid(CELLS["stamens"])
    filaments = 0.5 + 0.5 * numpy.sin(u * math.pi * 2 * 10)
    albedo = mix(srgb(0.74, 0.4, 0.07), srgb(0.88, 0.64, 0.13), filaments)
    albedo = mix(albedo, srgb(0.93, 0.78, 0.3), smooth(0.6, 1.0, v) * 0.7)
    albedo = mix(albedo, srgb(0.5, 0.42, 0.12), smooth(0.25, 0.0, v) * 0.6)
    return albedo, 0.6 * filaments, None


def stigma_texture():
    r, theta, rpx = disc(CELLS["stigma"])
    rays = 0.5 + 0.5 * numpy.cos(theta * 12)
    albedo = mix(srgb(0.8, 0.66, 0.22), srgb(0.9, 0.62, 0.14), rays * smooth(0.15, 0.5, r))
    albedo = mix(albedo, srgb(0.62, 0.5, 0.16), smooth(0.2, 0.0, r))
    return albedo, 0.5 * rays * smooth(0.15, 0.5, r), None


def normals_from(height, strength):
    dy, dx = numpy.gradient(height.astype(numpy.float32))
    n = numpy.dstack((-dx * strength, -dy * strength, numpy.ones_like(height)))
    n /= numpy.linalg.norm(n, axis=2, keepdims=True)
    return n * 0.5 + 0.5


def bake_textures():
    """The atlas: albedo, normal and ORM arrays filled cell by cell, saved as PNGs; returns the Blender images."""
    albedo = numpy.zeros((S, S, 3), dtype=numpy.float32)
    albedo[:] = srgb(0.17, 0.25, 0.09)
    normal = numpy.zeros((S, S, 3), dtype=numpy.float32)
    normal[:] = (0.5, 0.5, 1.0)
    rough = numpy.full((S, S), 0.5, dtype=numpy.float32)
    occlusion = numpy.ones((S, S), dtype=numpy.float32)

    def put(key, result, strength, roughness=None, ao=None):
        colour, height, rgh = result
        c0, r0, c1, r1 = CELLS[key]
        albedo[r0:r1, c0:c1] = numpy.clip(colour, 0.0, 1.0)
        normal[r0:r1, c0:c1] = normals_from(height, strength)
        rough[r0:r1, c0:c1] = rgh if rgh is not None else roughness
        if ao is not None:
            occlusion[r0:r1, c0:c1] = ao

    for spec in PADS:
        put(spec.key, pad_texture(spec), 1.6)
    put("underside", underside_texture(), 1.4)
    petal = petal_texture(CELLS["petal"], 81, srgb(0.6, 0.64, 0.44), srgb(0.84, 0.83, 0.78), tip=srgb(0.86, 0.84, 0.76))
    _, pv = grid(CELLS["petal"])
    put("petal", petal, 1.0, roughness=0.62, ao=0.75 + 0.25 * smooth(0.0, 0.35, pv))
    sepal = petal_texture(CELLS["sepal"], 91, srgb(0.3, 0.38, 0.17), srgb(0.6, 0.64, 0.47), tip=srgb(0.27, 0.2, 0.1),
                          margin=srgb(0.22, 0.32, 0.12))
    put("sepal", sepal, 1.0, roughness=0.55)
    put("bud", bud_texture(), 1.2, roughness=0.5)
    put("stalk", stalk_texture(), 1.0, roughness=0.5)
    _, sv = grid(CELLS["stamens"])
    put("stamens", stamen_texture(), 1.4, roughness=0.72, ao=0.6 + 0.4 * smooth(0.0, 0.7, sv))
    put("stigma", stigma_texture(), 1.2, roughness=0.7, ao=0.85)

    os.makedirs(ART, exist_ok=True)
    images = {}
    orm = numpy.dstack((occlusion, rough, numpy.zeros_like(rough)))
    orm = orm.reshape(S // 2, 2, S // 2, 2, 3).mean(axis=(1, 3))  # half size, as the project's other ORMs
    for kind, data, colourspace in (("Albedo", albedo, 'sRGB'), ("Normal", normal, 'Non-Color'), ("ORM", orm, 'Non-Color')):
        size = data.shape[0]
        image = bpy.data.images.new(f"{ASSET}_{kind}", size, size, alpha=False)
        image.colorspace_settings.name = colourspace
        pixels = numpy.ones((size, size, 4), dtype=numpy.float32)
        pixels[:, :, :3] = data
        image.pixels.foreach_set(pixels.ravel())
        image.filepath_raw = os.path.join(ART, f"{ASSET}_{kind}.png")
        image.file_format = 'PNG'
        image.save()
        images[kind] = image
    return images


# ----- Geometry -----------------------------------------------------------------------------------------------------

class Builder:
    """Vertices, faces and per-corner UVs, made into a mesh at the end. Faces can be turned to face a wanted way."""

    def __init__(self):
        self.verts, self.faces, self.uvs = [], [], []

    def vert(self, co):
        self.verts.append(Vector(co))
        return len(self.verts) - 1

    def face(self, idx, uv, want=None):
        if want is not None:
            a, b, c = (self.verts[i] for i in idx[:3])
            if (b - a).cross(c - a).dot(want) < 0.0:
                idx, uv = list(reversed(idx)), list(reversed(uv))
        self.faces.append(list(idx))
        self.uvs.append(list(uv))

    def mesh(self, name):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata([tuple(v) for v in self.verts], [], self.faces)
        layer = mesh.uv_layers.new(name="UVMap")
        for poly in mesh.polygons:
            for k, loop in enumerate(poly.loop_indices):
                layer.data[loop].uv = self.uvs[poly.index][k]
        for poly in mesh.polygons:
            poly.use_smooth = True
        mesh.validate()
        mesh.update()
        return mesh


def cell_uv(key, x, y, radius=DISC):
    """A point on a disc cell, x and y in -1..1 of the disc (1 at the pad's rest rim)."""
    c0, r0, c1, r1 = CELLS[key]
    cu, cv = (c0 + c1) * 0.5 / S, (r0 + r1) * 0.5 / S
    return (cu + x * radius * (c1 - c0) / S, cv + y * radius * (r1 - r0) / S)


def rect_uv(key, u, v):
    c0, r0, c1, r1 = CELLS[key]
    return ((c0 + u * (c1 - c0)) / S, (r0 + v * (r1 - r0)) / S)


def pad(spec):
    """A pad: a fan of rings round the centre, open across the notch; cupped, waved, and curled where the spec says, the
    curl's outer face carrying the underside's colour on faces of its own."""
    R = spec.radius
    rng = random.Random(spec.seed)
    notch = math.radians(spec.notch)
    columns = [notch + (2 * math.pi - 2 * notch) * j / spec.segments for j in range(spec.segments + 1)]
    slit_at = None
    if spec.slit:
        angle = math.radians(spec.slit[0])
        j = min(range(len(columns)), key=lambda k: abs(columns[k] - angle))
        columns[j] = angle
        columns.insert(j + 1, angle)
        slit_at = j
    rings = spec.rings
    phase = rng.uniform(0, 6.28)
    curl = spec.curl

    def curl_weight(theta):
        if not curl:
            return 0.0
        return bell(abs(wrap(theta - math.radians(curl[0]))) / math.radians(curl[1]))

    def place(theta, f, k):
        """Where the point at angle theta, share f of the radius, lands: in the plane, then lifted and rolled."""
        z = R * (spec.cup * f ** 4 + spec.wave * f ** 3 * (0.5 + 0.5 * math.sin(spec.waves * theta + phase)))
        z += spec.sag * (0.5 + 0.5 * math.sin(3.0 * theta + phase)) * f ** 2  # an old pad's crinkle (never under the pivot)
        radial = Vector((math.cos(theta), math.sin(theta), 0.0))
        w = curl_weight(theta)
        if w > 0.0 and f > curl[2]:
            # Rolled up over the arc beyond the curl's start, keeping its length (the texture is not stretched).
            s = (f - curl[2]) * R
            kappa = math.radians(curl[3]) * w / ((1.0 - curl[2]) * R)
            out = math.sin(kappa * s) / kappa if kappa > 1e-6 else s
            lift = (1.0 - math.cos(kappa * s)) / kappa if kappa > 1e-6 else 0.0
            base = curl[2] * R
            z0 = R * (spec.cup * curl[2] ** 4)
            return radial * (base + out) + UP * (z0 + lift + (z - R * spec.cup * curl[2] ** 4) * (1.0 - w))
        return radial * (f * R) + UP * z

    b = Builder()
    centre = b.vert((0.0, 0.0, 0.0))
    centre_uv = cell_uv(spec.key, 0.0, 0.0)
    ring_verts, rest = [], {}
    for k, ring in enumerate(rings):
        row = []
        for j, theta in enumerate(columns):
            split = slit_at is not None and j in (slit_at, slit_at + 1) and ring > spec.slit[1] - 0.05
            if slit_at is not None and j == slit_at + 1 and not split:
                row.append(row[-1])  # inside the slit's inner end its two sides are one leaf: one vertex
                continue
            if split:
                # The slit's two sides part a little more towards the rim, one side a little higher.
                side = -1 if j == slit_at else 1
                theta = theta + side * math.radians(spec.slit[2]) * ((ring - spec.slit[1] + 0.05) / (1.05 - spec.slit[1]))
            f = ring * (rim_shape(spec, theta) if k == len(rings) - 1 else
                        1.0 + (rim_shape(spec, theta) - 1.0) * 0.5 * (k == len(rings) - 2 and spec.bite is not None))
            p = place(theta, f, k)
            if split and j == slit_at + 1:
                p += UP * 0.004 * max(0.0, ring - spec.slit[1]) / (1.0 - spec.slit[1])
            index = b.vert(p)
            rest[index] = (f * math.cos(theta), f * math.sin(theta), curl_weight(theta) if f > (curl[2] if curl else 9) else 0.0)
            row.append(index)
        ring_verts.append(row)

    def uv_of(index):
        x, y, _ = rest[index]
        return cell_uv(spec.key, x, y)

    # Faces in the flat leaf's order: angles rise counter-clockwise seen from above, so every face looks up before the
    # rim is rolled (a rolled face may look down, and must keep its neighbours' winding). The slit's column pair has no
    # faces: open from the rim down to its inner end, a single vertex inside that. Quads are split here, the same way for
    # the leaf and its underside, so no importer picks the other diagonal and lets one surface poke through the other.
    top = []
    for j in range(len(columns) - 1):
        if slit_at is not None and j == slit_at:
            continue
        a, c = ring_verts[0][j], ring_verts[0][j + 1]
        b.face([centre, a, c], [centre_uv, uv_of(a), uv_of(c)])
        for k in range(len(rings) - 1):
            q = [ring_verts[k][j], ring_verts[k + 1][j], ring_verts[k + 1][j + 1], ring_verts[k][j + 1]]
            for tri in ([q[0], q[1], q[2]], [q[0], q[2], q[3]]):
                b.face(tri, [uv_of(i) for i in tri])
                top.append(tri)

    # The curl's underside: its own faces just outside the rolled leaf, in the underside's colour.
    curled = [q for q in top if any(rest[i][2] > 0.02 for i in q)]
    if curled:
        mesh = b.mesh(f"{spec.key} probe")
        normals = {v.index: Vector(v.normal) for v in mesh.vertices}
        bpy.data.meshes.remove(mesh)
        under = {}
        for q in curled:
            for i in q:
                if i not in under:
                    under[i] = b.vert(b.verts[i] - normals[i] * 0.0025)
        for q in curled:
            idx = [under[i] for i in reversed(q)]
            uvs = [cell_uv("underside", -rest[i][0], rest[i][1]) for i in reversed(q)]
            b.faces.append(idx)
            b.uvs.append(uvs)
    return b


def petal(b, key, base, alpha, elevation, length, width, cup, droop, twist=0.0):
    """A pointed petal or sepal from base, heading alpha (radians round the flower), rising at elevation: base, two rows of
    three and the tip; its sides cupped up by cup, its length bent down by droop (both as shares of its size). Single
    sided: the material draws both sides."""
    d = Vector((math.cos(alpha) * math.cos(elevation), math.sin(alpha) * math.cos(elevation), math.sin(elevation)))
    s = Vector((-math.sin(alpha), math.cos(alpha), 0.0))
    s = (Matrix.Rotation(twist, 3, d) @ s).normalized()
    n = d.cross(s).normalized()
    rows = [(0.0, 0.08), (0.42, 0.5), (0.8, 0.36), (1.0, 0.0)]
    grid_rows = []
    for t, half in rows:
        row = []
        for x in ((-1.0, 0.0, 1.0) if half > 0.1 else (0.0,)):
            p = base + d * (t * length) + s * (x * half * width) + n * (cup * abs(x) ** 2 * half * width - droop * t * t * length)
            row.append((b.vert(p), rect_uv(key, 0.5 + 0.5 * x * half / 0.5 * 0.92, t)))
        grid_rows.append(row)
    (b0, u0), = grid_rows[0][1:2] if len(grid_rows[0]) == 3 else grid_rows[0]
    r1, r2 = grid_rows[1], grid_rows[2]
    (tp, ut), = grid_rows[3]
    b.face([b0, r1[0][0], r1[1][0]], [u0, r1[0][1], r1[1][1]], want=n)
    b.face([b0, r1[1][0], r1[2][0]], [u0, r1[1][1], r1[2][1]], want=n)
    for k in range(2):
        q = [r1[k][0], r2[k][0], r2[k + 1][0], r1[k + 1][0]]
        b.face(q, [r1[k][1], r2[k][1], r2[k + 1][1], r1[k + 1][1]], want=n)
    b.face([r2[0][0], tp, r2[1][0]], [r2[0][1], ut, r2[1][1]], want=n)
    b.face([r2[1][0], tp, r2[2][0]], [r2[1][1], ut, r2[2][1]], want=n)


def water_lily():
    """The flower: four sepals lying out, eight outer petals rising gently and eight inner ones steeper between them,
    round a crown of stamens with the stigma's dish in its middle."""
    b = Builder()
    rng = random.Random(53)
    for k in range(4):
        a = math.radians(45 + 90 * k + rng.uniform(-6, 6))
        petal(b, "sepal", Vector((math.cos(a) * 0.009, math.sin(a) * 0.009, 0.006)), a, math.radians(8), 0.058 * rng.uniform(0.95, 1.05),
              0.024, cup=0.25, droop=0.08)
    for k in range(8):
        a = math.radians(22.5 + 45 * k + rng.uniform(-5, 5))
        petal(b, "petal", Vector((math.cos(a) * 0.01, math.sin(a) * 0.01, 0.009)), a, math.radians(rng.uniform(20, 28)),
              0.057 * rng.uniform(0.93, 1.06), 0.024, cup=0.35, droop=0.06, twist=rng.uniform(-0.15, 0.15))
    for k in range(8):
        a = math.radians(45 * k + rng.uniform(-6, 6))
        petal(b, "petal", Vector((math.cos(a) * 0.009, math.sin(a) * 0.009, 0.013)), a, math.radians(rng.uniform(46, 56)),
              0.045 * rng.uniform(0.92, 1.06), 0.02, cup=0.45, droop=-0.04, twist=rng.uniform(-0.2, 0.2))
    # The crown: a short flaring ring of stamens, its rim notched, and the stigma's dish inside it.
    sides = 10
    bottom, top = [], []
    for j in range(sides + 1):
        a = 2 * math.pi * j / sides
        tall = 0.026 if j % 2 == 0 else 0.022
        bottom.append(b.vert((math.cos(a) * 0.0105, math.sin(a) * 0.0105, 0.012)))
        top.append(b.vert((math.cos(a) * 0.0155, math.sin(a) * 0.0155, tall)))
    for j in range(sides):
        a = 2 * math.pi * (j + 0.5) / sides
        out = Vector((math.cos(a), math.sin(a), 0.0))
        q = [bottom[j], bottom[j + 1], top[j + 1], top[j]]
        b.face(q, [rect_uv("stamens", j / sides, 0.0), rect_uv("stamens", (j + 1) / sides, 0.0),
                   rect_uv("stamens", (j + 1) / sides, 1.0), rect_uv("stamens", j / sides, 1.0)], want=out)
    dish = b.vert((0.0, 0.0, 0.019))
    for j in range(sides):
        a0, a1 = 2 * math.pi * j / sides, 2 * math.pi * (j + 1) / sides
        b.face([dish, top[j], top[j + 1]],
               [cell_uv("stigma", 0.0, 0.0), cell_uv("stigma", math.cos(a0), math.sin(a0)),
                cell_uv("stigma", math.cos(a1), math.sin(a1))], want=UP)
    # Laid out at 12 cm, grown to the white water lily's usual 14-15 cm (it read as a speck from the game camera).
    b.verts = [v * 1.2 for v in b.verts]
    return b


def lathe(b, key, profile, sides, frame, v_range=(0.0, 1.0), tip=None):
    """Rings round the frame's Z through the profile [(radius, height)], u round (with a seam column), v along; closed by
    a tip vertex when given."""
    rows = []
    heights = [h for _, h in profile] + ([tip] if tip is not None else [])
    low, high = min(heights), max(heights)
    for radius, h in profile:
        v = v_range[0] + (v_range[1] - v_range[0]) * (h - low) / (high - low)
        row = []
        for j in range(sides + 1):
            a = 2 * math.pi * j / sides
            row.append((b.vert(frame @ Vector((math.cos(a) * radius, math.sin(a) * radius, h))), rect_uv(key, j / sides, v)))
        rows.append(row)
    axis = (frame.to_3x3() @ UP).normalized()
    for k in range(len(rows) - 1):
        for j in range(sides):
            a = 2 * math.pi * (j + 0.5) / sides
            out = frame.to_3x3() @ Vector((math.cos(a), math.sin(a), 0.0))
            q = [rows[k][j], rows[k][j + 1], rows[k + 1][j + 1], rows[k + 1][j]]
            b.face([p for p, _ in q], [uv for _, uv in q], want=out)
    if tip is not None:
        apex = b.vert(frame @ Vector((0.0, 0.0, tip)))
        for j in range(sides):
            last = rows[-1]
            b.face([last[j][0], last[j + 1][0], apex],
                   [last[j][1], last[j + 1][1], rect_uv(key, (j + 0.5) / sides, v_range[1])], want=axis)


def lily_bud():
    """A closed bud on its stalk, leaning, 9 cm out of the water; the stalk runs on under the surface."""
    b = Builder()
    lean = Matrix.Rotation(math.radians(13), 4, 'X') @ Matrix.Rotation(math.radians(20), 4, 'Z')
    lathe(b, "stalk", [(0.0045, -0.04), (0.0045, 0.0), (0.0042, 0.032)], 6, lean)
    lathe(b, "bud", [(0.0042, 0.03), (0.011, 0.037), (0.0165, 0.05), (0.0152, 0.066), (0.0085, 0.08)], 8, lean,
          v_range=(0.0, 0.93), tip=0.093)
    return b


# ----- Materials, export, measuring ---------------------------------------------------------------------------------

def baked_material(images):
    material = bpy.data.materials.new("Lily Pads")
    material.use_nodes = True
    tree = material.node_tree
    bsdf = tree.nodes["Principled BSDF"]
    albedo = tree.nodes.new("ShaderNodeTexImage")
    albedo.image = images["Albedo"]
    tree.links.new(albedo.outputs["Color"], bsdf.inputs["Base Color"])
    normal_tex = tree.nodes.new("ShaderNodeTexImage")
    normal_tex.image = images["Normal"]
    normal_map = tree.nodes.new("ShaderNodeNormalMap")
    tree.links.new(normal_tex.outputs["Color"], normal_map.inputs["Color"])
    tree.links.new(normal_map.outputs["Normal"], bsdf.inputs["Normal"])
    orm = tree.nodes.new("ShaderNodeTexImage")
    orm.image = images["ORM"]
    split = tree.nodes.new("ShaderNodeSeparateColor")
    tree.links.new(orm.outputs["Color"], split.inputs["Color"])
    tree.links.new(split.outputs["Green"], bsdf.inputs["Roughness"])
    bsdf.inputs["Subsurface Weight"].default_value = 0.0
    return material


def make_object(name, builder, material, collection):
    mesh = builder.mesh(name)
    mesh.materials.append(material)
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def measure(obj):
    """Triangles, vertices as Unity will count them (split where position, normal or UV differ), size in metres."""
    mesh = obj.data
    mesh.calc_loop_triangles()
    keys = set()
    layer = mesh.uv_layers[0].data
    normals = mesh.corner_normals
    for loop in mesh.loops:
        keys.add((loop.vertex_index, tuple(round(c, 3) for c in normals[loop.index].vector),
                  tuple(round(c, 4) for c in layer[loop.index].uv)))
    corners = [Vector(c) for c in obj.bound_box]
    size = [max(c[k] for c in corners) - min(c[k] for c in corners) for k in range(3)]
    return len(mesh.loop_triangles), len(keys), size


def export(objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(ART, "Lily Pads.fbx"), use_selection=True,
                             apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             object_types={'MESH'}, mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='STRIP',
                             embed_textures=False)


# ----- Preview ------------------------------------------------------------------------------------------------------

def use_gpu(scene):
    scene.render.engine = 'CYCLES'
    try:
        prefs = bpy.context.preferences.addons["cycles"].preferences
        prefs.compute_device_type = 'OPTIX'
        prefs.get_devices()
        for device in prefs.devices:
            device.use = True
        scene.cycles.device = 'GPU'
    except Exception:
        scene.cycles.device = 'CPU'


def marsh_water(collection):
    """Black, still marsh water with a muddy bank beyond it, a soft sun under thin cloud."""
    scene = bpy.context.scene
    mesh = bpy.data.meshes.new("Water")
    mesh.from_pydata([(-40, -40, 0), (40, -40, 0), (40, 40, 0), (-40, 40, 0)], [], [(0, 1, 2, 3)])
    water = bpy.data.objects.new("Water", mesh)
    collection.objects.link(water)
    material = bpy.data.materials.new("Marsh Water")
    material.use_nodes = True
    bsdf = material.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.006, 0.009, 0.006, 1)
    bsdf.inputs["Roughness"].default_value = 0.06
    mesh.materials.append(material)
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(50), math.radians(10), math.radians(-35)))
    sun = bpy.context.active_object
    sun.data.energy = 2.6
    sun.data.angle = math.radians(8)
    sun.data.color = (1.0, 0.96, 0.88)
    world = bpy.data.worlds.new("Sky")
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.46, 0.55, 0.58, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7


def camera(location, target, lens=50, fov=None):
    cam_data = bpy.data.cameras.new("Camera")
    cam = bpy.data.objects.new("Camera", cam_data)
    bpy.context.scene.collection.objects.link(cam)
    cam.location = location
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat('-Z', 'Y').to_euler()
    if fov:
        cam_data.sensor_fit = 'VERTICAL'
        cam_data.angle_y = math.radians(fov)
    else:
        cam_data.lens = lens
    cam_data.clip_start = 0.02
    bpy.context.scene.camera = cam
    return cam


def render(path, width, height):
    scene = bpy.context.scene
    use_gpu(scene)
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    scene.render.resolution_x, scene.render.resolution_y = width, height
    scene.render.film_transparent = False
    scene.view_settings.view_transform = 'AgX'
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def game_view(target, distance, pitch, yaw, path, width, height, fov=40.0):
    p, y = math.radians(pitch), math.radians(yaw)
    back = Vector((-math.sin(y) * math.cos(p), -math.cos(y) * math.cos(p), math.sin(p)))
    cam = camera(Vector(target) + back * distance, target, fov=fov)
    render(path, width, height)
    bpy.data.objects.remove(cam)
    return path


def copy_of(source, location, turn, scale, collection):
    copy = source.copy()
    copy.location = location
    copy.rotation_euler = (0.0, 0.0, turn)
    copy.scale = (scale, scale, scale)
    copy.hide_render = False  # the sources are hidden while their copies are shown
    collection.objects.link(copy)
    return copy


def clump(pieces, centre, count, rng, collection, flower=True, bud=True):
    """Pads round a point, none overlapping, a flower on one and a bud beside another: as RaidLilyPads lays them."""
    pads = [pieces[k] for k in ("Lily Pad A", "Lily Pad B", "Lily Pad C", "Lily Pad D")]
    placed = []
    radii = {"Lily Pad A": 0.15, "Lily Pad B": 0.22, "Lily Pad C": 0.27, "Lily Pad D": 0.25}
    tries = 0
    while len(placed) < count and tries < 400:
        tries += 1
        source = rng.choices(pads, weights=(3, 3, 1.5, 1))[0]
        scale = rng.uniform(0.85, 1.2)
        r = radii[source.name] * scale
        spread = 0.26 * math.sqrt(count)
        at = Vector((centre[0] + rng.gauss(0, spread), centre[1] + rng.gauss(0, spread), 0.0))
        if any((at - p).length < (r + q) * 0.9 for p, q, _ in placed):
            continue
        lift = 0.003 + (0.004 * len(placed)) % 0.012  # staggered, so touching pads never share a height
        copy_of(source, at + Vector((0, 0, lift)), rng.uniform(0, 6.28), scale, collection)
        placed.append((at, r, lift))
    if flower and placed:
        at, _, lift = placed[len(placed) // 2]
        copy_of(pieces["Water Lily"], at + Vector((0, 0, lift + 0.002)), rng.uniform(0, 6.28), 1.0, collection)
    if bud and len(placed) > 2:
        at, r, _ = placed[1]
        angle = rng.uniform(0, 6.28)
        beside = at + Vector((math.cos(angle), math.sin(angle), 0.0)) * (r + 0.05)
        copy_of(pieces["Lily Bud"], beside, rng.uniform(0, 6.28), 1.0, collection)
    return placed


def tile(paths, columns, out, cell=(960, 540)):
    rows = (len(paths) + columns - 1) // columns
    w, h = cell
    canvas = numpy.zeros((rows * h, columns * w, 4), dtype=numpy.float32)
    canvas[:, :, 3] = 1.0
    for i, path in enumerate(paths):
        img = bpy.data.images.load(path, check_existing=False)
        img.scale(w, h)
        px = numpy.empty(w * h * 4, dtype=numpy.float32)
        img.pixels.foreach_get(px)
        bpy.data.images.remove(img)
        r, c = i // columns, i % columns
        canvas[(rows - 1 - r) * h:(rows - r) * h, c * w:(c + 1) * w] = px.reshape(h, w, 4)
    result = bpy.data.images.new(os.path.basename(out), columns * w, rows * h, alpha=True)
    result.pixels.foreach_set(canvas.ravel())
    result.filepath_raw = out
    result.file_format = 'PNG'
    result.save()
    return out


def preview(pieces):
    shown = bpy.data.collections.new("Preview")
    bpy.context.scene.collection.children.link(shown)
    marsh_water(shown)
    for obj in pieces.values():
        obj.hide_render = True
    stem = os.path.splitext(PREVIEW)[0]
    # 1. The pieces close, as the action camera sees them.
    row = [("Lily Pad A", (-0.95, 0.1)), ("Lily Pad B", (-0.42, 0.1)), ("Lily Pad C", (0.25, 0.1)), ("Lily Pad D", (0.95, 0.1)),
           ("Water Lily", (-0.3, -0.45)), ("Lily Bud", (0.3, -0.45))]
    lineup = []
    for name, (x, y) in row:
        # Pad C turned so its rolled rim faces the camera (its underside shows); the flower sits on a smaller pad B.
        turn = math.radians(110 if name == "Lily Pad C" else -90)
        lineup.append(copy_of(pieces[name], (x, y, 0.005 if name == "Water Lily" else 0.003), turn, 1.0, shown))
    lineup.append(copy_of(pieces["Lily Pad B"], (-0.3, -0.45, 0.003), 0.7, 0.75, shown))
    panels = [game_view(Vector((0.0, -0.1, 0.0)), 2.6, 48.0, 0.0, stem + "_close.png", 1920, 1080)]
    # 2. Eye level: the curl's underside, the flower's profile, the bud.
    cam = camera(Vector((0.35, -1.75, 0.22)), Vector((0.1, -0.1, 0.05)), lens=55)
    panels.append(render(stem + "_eye.png", 1920, 1080))
    bpy.data.objects.remove(cam)
    for obj in lineup:
        bpy.data.objects.remove(obj)
    # 3 and 4. Clumps along a bank, from the action camera (6 m) and from the default game camera (20 m, 1080p).
    rng = random.Random(9)
    for centre, count in (((0.0, 0.0), 14), ((3.4, 1.8), 8), ((-3.2, 2.6), 18), ((-1.2, -3.0), 6), ((4.0, -2.4), 11)):
        clump(pieces, centre, count, rng, shown)
    panels.append(game_view(Vector((0.0, 0.0, 0.0)), 6.0, 42.0, 0.0, stem + "_action.png", 1920, 1080))
    panels.append(game_view(Vector((0.0, 0.0, 0.0)), 20.0, 48.0, 0.0, stem + "_game.png", 1920, 1080))
    tile(panels, 2, PREVIEW)
    print("PREVIEW", PREVIEW)


# ----- Main ---------------------------------------------------------------------------------------------------------

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    images = bake_textures()
    material = baked_material(images)
    assets = bpy.data.collections.new("Lily Pads")
    bpy.context.scene.collection.children.link(assets)
    pieces = {}
    for spec in PADS:
        pieces[f"Lily Pad {spec.key}"] = make_object(f"Lily Pad {spec.key}", pad(spec), material, assets)
    pieces["Water Lily"] = make_object("Water Lily", water_lily(), material, assets)
    pieces["Lily Bud"] = make_object("Lily Bud", lily_bud(), material, assets)
    bpy.context.view_layer.update()
    for obj in pieces.values():
        tris, verts, size = measure(obj)
        low = min((obj.matrix_world @ Vector(c)).z for c in obj.bound_box)
        print(f"BUDGET {obj.name}: {tris} triangles, {verts} vertices, {size[0]:.3f} x {size[1]:.3f} m, "
              f"{size[2]:.3f} m tall, lowest {low:.4f} m")
    export(list(pieces.values()))
    preview(pieces)
    for obj in pieces.values():
        obj.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "LilyPads.blend"))
    print("LILY PADS DONE", ART)


main()
