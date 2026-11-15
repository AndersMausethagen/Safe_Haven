"""Decal textures for Safe Haven (Assets/_Project/Art/Decals), made procedurally with numpy so they can be remade:

    python Tools/Decals/decals.py

Each decal is a base map (RGBA, alpha = coverage) and a tangent-space normal map from a height field:
- Blood Atlas: 2x2 splats with satellite droplets and drips, near-white so BloodDecals can tint it per creature.
- Footprints: a left and a right boot print side by side, pressed into the ground.
- Trampled Ground: worn, scuffed earth with ragged edges, for the yards in front of buildings.
- Scorch: ash and char round a fire.
- Wall Grime: damp, moss and dirt at the foot of a wall, strongest at the bottom and fading up.
- Dirt Path and Stone Path: a drawn path's strip (PathPiece), along U and repeating end to end so pieces laid in a line
  join without a seam; ragged, grassy edges across. Packed earth with ruts; flagstones bedded in earth.

Run one group alone (so the others are not redrawn): python Tools/Decals/decals.py paths
"""
import os
import sys

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', '_Project', 'Art', 'Decals')
RNG = np.random.default_rng(7)


def value_noise(size, cells, seed):
    rng = np.random.default_rng(seed)
    grid = rng.random((cells + 1, cells + 1))
    grid[-1, :] = grid[0, :]
    grid[:, -1] = grid[:, 0]
    t = np.linspace(0, cells, size, endpoint=False)
    i = t.astype(int)
    f = t - i
    f = f * f * (3 - 2 * f)
    a = grid[i][:, i]
    b = grid[i][:, i + 1]
    c = grid[i + 1][:, i]
    d = grid[i + 1][:, i + 1]
    fx = f[None, :]
    fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(size, base, octaves, seed):
    total = np.zeros((size, size))
    weight = 0.5
    norm = 0
    for o in range(octaves):
        total += value_noise(size, base * 2 ** o, seed + o * 31) * weight
        norm += weight
        weight *= 0.5
    return total / norm


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def normal_map(height, strength):
    gy, gx = np.gradient(height)
    nx = -gx * strength
    ny = gy * strength
    nz = np.ones_like(height)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    rgb = np.stack([nx / length, ny / length, nz / length], -1) * 0.5 + 0.5
    return (rgb * 255).astype(np.uint8)


def save(name, rgba, height, strength):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), 'RGBA').save(os.path.join(OUT, name + '.png'))
    Image.fromarray(normal_map(height, strength), 'RGB').save(os.path.join(OUT, name + ' Normal.png'))


def disc(size, cx, cy, r, soft=1.5):
    y, x = np.mgrid[0:size, 0:size]
    d = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
    return 1 - smoothstep(r - soft, r + soft, d)


def splat(size, seed):
    rng = np.random.default_rng(seed)
    y, x = np.mgrid[0:size, 0:size] / size - 0.5
    angle = np.arctan2(y, x)
    radius = np.sqrt(x * x + y * y)
    # A ragged main body: a blob of warped noise round the middle, so no two splats share a shape.
    warp_x = (fbm(size, 4, 4, seed + 1) - 0.5) * 0.18
    warp_y = (fbm(size, 4, 4, seed + 2) - 0.5) * 0.18
    wx, wy = x + warp_x, y + warp_y
    stretch = 0.7 + rng.random() * 0.6
    radius_w = np.sqrt((wx * stretch) ** 2 + (wy / stretch) ** 2)
    edge = fbm(size, 10, 4, seed + 3)
    body = 0.17 + 0.07 * (edge - 0.5) * 2
    mask = 1 - smoothstep(body - 0.01, body + 0.004, radius_w)
    height = mask * (0.6 + 0.4 * (1 - radius / 0.25).clip(0, 1))
    # Satellite droplets thrown outward, and a few streaks.
    for _ in range(rng.integers(18, 34)):
        a = rng.random() * 6.28
        d = 0.22 + rng.random() * 0.24
        r = (0.004 + rng.random() * 0.018) * size
        drop = disc(size, (0.5 + np.cos(a) * d) * size, (0.5 + np.sin(a) * d) * size, r)
        mask = np.maximum(mask, drop)
        height = np.maximum(height, drop * 0.8)
    for _ in range(rng.integers(2, 5)):
        a = rng.random() * 6.28
        for s in np.linspace(0.18, 0.18 + rng.random() * 0.2, 14):
            r = (0.012 * (1 - (s - 0.18) * 2.2)).clip(0.003, 1) * size
            drop = disc(size, (0.5 + np.cos(a) * s) * size, (0.5 + np.sin(a) * s) * size, r)
            mask = np.maximum(mask, drop)
            height = np.maximum(height, drop * 0.6)
    shade = 0.75 + 0.25 * fbm(size, 8, 4, seed + 5)
    # Thicker in the middle: darker where it pooled.
    core = 1 - 0.35 * (1 - radius / 0.25).clip(0, 1)
    rgb = np.stack([shade * core] * 3, -1)
    return np.dstack([rgb, mask]), height


def blood():
    tile = 512
    rgba = np.zeros((tile * 2, tile * 2, 4))
    height = np.zeros((tile * 2, tile * 2))
    for i in range(4):
        part, h = splat(tile, 100 + i * 17)
        ty, tx = divmod(i, 2)
        rgba[ty * tile:(ty + 1) * tile, tx * tile:(tx + 1) * tile] = part
        height[ty * tile:(ty + 1) * tile, tx * tile:(tx + 1) * tile] = h
    save('Blood Atlas', rgba, height, 6.0)


def boot(size, flip):
    y, x = np.mgrid[0:size * 2, 0:size] / size
    x = x - 0.5
    if flip:
        x = -x
    # Sole: wide at the ball, narrow at the arch, a rounded toe; heel: its own block behind.
    yy = y - 1.0
    width = 0.19 - 0.07 * np.exp(-((yy - 0.12) / 0.12) ** 2) + 0.02 * np.exp(-((yy + 0.35) / 0.15) ** 2)
    sole = ((x + 0.04 * (yy + 0.2)) / width) ** 2 + ((yy + 0.12) / 0.58) ** 2
    heel = (x / 0.15) ** 2 + ((yy - 0.62) / 0.19) ** 2
    shape = np.maximum(1 - smoothstep(0.8, 1.0, sole), 1 - smoothstep(0.8, 1.0, heel))
    shape *= 0.75 + 0.25 * fbm(size * 2, 12, 3, 5)[:, :size]
    tread = (np.sin(yy * 70) * 0.5 + 0.5) * shape
    depth = shape * (0.7 + 0.3 * tread)
    return shape, depth


def footprints():
    size = 256
    left, dl = boot(size, False)
    right, dr = boot(size, True)
    mask = np.concatenate([left, right], 1)
    depth = np.concatenate([dl, dr], 1)
    grain = fbm(size * 2, 16, 3, 3)[:, :]
    # Pressed wet mud with water standing in it: darker than the soaked ground round it.
    mud = np.stack([0.11 + 0.04 * grain, 0.09 + 0.03 * grain, 0.07 + 0.02 * grain], -1)
    rgba = np.dstack([mud, np.clip(mask * 1.15, 0, 1)])
    save('Footprints', rgba, -depth, 4.0)


def trampled():
    size = 1024
    y, x = np.mgrid[0:size, 0:size] / size - 0.5
    # A rounded-rectangle footprint (buildings are boxes) with a ragged, grassy edge and bare patches inside.
    radius = np.maximum(abs(x), abs(y)) * 0.35 + np.sqrt(x * x + y * y) * 0.65
    rough = fbm(size, 5, 5, 11)
    mask = 1 - smoothstep(0.22, 0.46, radius + (rough - 0.5) * 0.3)
    patchy = smoothstep(0.3, 0.65, fbm(size, 9, 4, 13))
    mask *= 0.6 + 0.4 * patchy
    grit = fbm(size, 40, 3, 17)
    # Packed, damp earth: darker than the meadow's dry soil.
    rgb = np.stack([0.24 + 0.07 * grit, 0.19 + 0.05 * grit, 0.13 + 0.04 * grit], -1)
    height = fbm(size, 24, 4, 19) * mask
    save('Trampled Ground', np.dstack([rgb, mask * 0.95]), height, 3.0)


def scorch():
    size = 512
    y, x = np.mgrid[0:size, 0:size] / size - 0.5
    radius = np.sqrt(x * x + y * y)
    rough = fbm(size, 6, 5, 23)
    mask = 1 - smoothstep(0.22, 0.48, radius + (rough - 0.5) * 0.18)
    char = 1 - smoothstep(0.0, 0.3, radius)
    ash = smoothstep(0.6, 0.75, fbm(size, 30, 3, 29))
    grey = 0.06 + 0.1 * ash + 0.06 * (1 - char)
    rgb = np.stack([grey + 0.01, grey, grey], -1)
    height = fbm(size, 20, 4, 31) * mask * 0.6
    save('Scorch', np.dstack([rgb, mask]), height, 2.0)


def grime():
    w, h = 1024, 256
    y, x = np.mgrid[0:h, 0:w]
    up = y / h  # 0 at the top of the texture
    rough = np.tile(fbm(w, 6, 5, 37), (1, 1))[:h, :]
    # Strongest at the foot (bottom of the texture), ragged edge rising and falling along the wall.
    reach = 0.35 + 0.35 * rough
    mask = smoothstep(1 - reach - 0.1, 1 - reach + 0.25, up)
    moss = smoothstep(0.5, 0.7, np.tile(fbm(w, 14, 4, 41), (1, 1))[:h, :])
    rgb = np.stack([0.12 + 0.04 * moss, 0.14 + 0.1 * moss, 0.07 + 0.02 * moss], -1)
    height = np.tile(fbm(w, 30, 3, 43), (1, 1))[:h, :] * mask
    save('Wall Grime', np.dstack([rgb, mask * 0.9]), height, 2.0)


def path_strip(w, h, seed):
    """Across (v) 0..1 with a ragged edge that repeats along (u): the strip's coverage, and u, v."""
    y, x = np.mgrid[0:h, 0:w]
    u = x / w
    v = y / h
    edge = np.tile(fbm(w, 6, 4, seed), (2, 1))[:h, :]
    # The edge's raggedness from a periodic noise row, so the ends of each piece meet.
    wobble = (edge[0:1, :] - 0.5) * 0.16 + (fbm(w, 18, 3, seed + 3)[:h, :] - 0.5) * 0.08
    across = abs(v - 0.5) + wobble
    return 1 - smoothstep(0.30, 0.47, across), u, v


def dirt_path():
    w, h = 1024, 512
    mask, u, v = path_strip(w, h, 61)
    grit = np.tile(fbm(w, 40, 3, 67), (1, 1))[:h, :]
    # Two wheel and foot ruts, a little darker and lower; a grassy crown between them in places.
    ruts = np.exp(-((v - 0.36) / 0.06) ** 2) + np.exp(-((v - 0.64) / 0.06) ** 2)
    crown = smoothstep(0.55, 0.75, np.tile(fbm(w, 8, 3, 71), (1, 1))[:h, :]) * np.exp(-((v - 0.5) / 0.07) ** 2)
    rgb = np.stack([0.30 + 0.07 * grit - 0.05 * ruts, 0.23 + 0.05 * grit - 0.04 * ruts, 0.15 + 0.04 * grit - 0.03 * ruts], -1)
    alpha = mask * (0.92 - 0.55 * crown)
    height = (np.tile(fbm(w, 30, 4, 73), (1, 1))[:h, :] * 0.6 - ruts * 0.4) * mask
    save('Dirt Path', np.dstack([rgb, alpha]), height, 3.0)


def stone_path():
    w, h = 1024, 512
    mask, u, v = path_strip(w, h, 79)
    # Flagstones: a periodic Voronoi in (u, v) with the u side wrapping, so the stones run on from piece to piece.
    rng = np.random.default_rng(83)
    count = 26
    seeds = np.stack([rng.random(count), 0.18 + 0.64 * rng.random(count)], -1)
    pu = u[..., None]
    pv = v[..., None]
    du = np.abs(pu - seeds[:, 0])
    du = np.minimum(du, 1 - du) * (w / h)
    dv = pv - seeds[:, 1]
    dist = np.sqrt(du * du + dv * dv)
    order = np.argsort(dist, -1)
    which = order[..., 0]
    second = order[..., 1]
    d1 = np.take_along_axis(dist, which[..., None], -1)[..., 0]
    d2 = np.take_along_axis(dist, second[..., None], -1)[..., 0]
    # The true distance to the joint between the two nearest stones (to their bisector), so joints keep one width.
    su = np.abs(seeds[which, 0] - seeds[second, 0])
    su = np.minimum(su, 1 - su) * (w / h)
    apart = np.sqrt(su * su + (seeds[which, 1] - seeds[second, 1]) ** 2)
    gap = (d2 * d2 - d1 * d1) / (2 * np.maximum(apart, 1e-4))
    joint = 1 - smoothstep(0.006, 0.016, gap)
    tone = rng.random(count)[which]
    grit = fbm(w, 48, 3, 89)[:h, :]
    grey = 0.40 + 0.12 * tone + 0.06 * grit
    stone = np.stack([grey * 0.98, grey * 0.96, grey * 0.92], -1)
    earth = np.stack([0.20 + 0.04 * grit, 0.16 + 0.03 * grit, 0.11 + 0.02 * grit], -1)
    rgb = stone * (1 - joint[..., None]) + earth * joint[..., None]
    # Stones are domed a little, joints sunk.
    height = ((1 - joint) * (0.5 + 0.5 * smoothstep(0.0, 0.08, gap)) + grit * 0.15) * mask
    save('Stone Path', np.dstack([rgb, mask * 0.97]), height, 4.0)


if __name__ == '__main__':
    if len(sys.argv) > 1 and sys.argv[1] == 'paths':
        dirt_path()
        stone_path()
        print('path decals in', os.path.abspath(OUT))
        sys.exit(0)
    blood()
    footprints()
    trampled()
    scorch()
    grime()
    dirt_path()
    stone_path()
    print('decals in', os.path.abspath(OUT))
