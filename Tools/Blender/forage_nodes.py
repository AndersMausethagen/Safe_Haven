"""The first things the player gathers in Safe Haven, modelled and textured in Blender: Small Rocks and Sticks (the
Berry Bush is in plant_nodes.py with the other plants).

Made like the Poly Haven scans the Haven is dressed with: real-size shapes whose surfaces come from CC0 scanned textures
(rock_boulder_dry, bark_brown_02), baked with prop_bake.py into Albedo, Normal, ORM, Height (parallax) and BlendMask
(where moss grows) maps, with two levels of detail.

- Small Rocks: three clusters of split grey field stones, 3 to 5 stones, about 0.55 x 0.5 m. Sculpted high (split
  planes and noise on a subdivided sphere), baked onto a low mesh of about 250 triangles a stone.
- Sticks: three heaps of fallen, forked branches, 3 to 5 sticks 0.35 to 0.6 m long, with broken, pale ends; half again
  as thick as real twigs, so they read from the game's camera.

Writes Assets/_Project/Art/Gathering/<Asset>/, a preview render of all variants to the path given, and Forage.blend
beside this script.

Run: blender -b --factory-startup --python Tools/Blender/forage_nodes.py -- <project root> <preview.png> [asset ...]
(assets: rocks, sticks; both when none are named).
In an open Blender (the MCP), runpy.run_path(this file, run_name="forage_lab") loads the functions without running main().
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from prop_bake import *  # noqa: E402,F401,F403  (helpers, materials, bake)
from prop_bake import ROOT, args  # noqa: E402

PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "forage_preview.png")
WANTED = set(args[2:]) or {"rocks", "sticks"}


# ----- Small Rocks --------------------------------------------------------------------------------------------------

def rock_cluster(name, layout, seed, coll):
    """Stones placed by layout: (x, y, size x, y, z, turn degrees)."""
    rng = random.Random(seed)
    lows, highs = [], []
    for i, (x, y, sx, sy, sz, turn) in enumerate(layout):
        low, high = stone(f"{name} {i}", (sx, sy, sz), rng, coll)
        placed = Matrix.Translation((x, y, 0)) @ Matrix.Rotation(math.radians(turn), 4, 'Z')
        for obj in (low, high):
            obj.data.transform(placed)
        lows.append(low)
        highs.append(high)
    return join(lows, name), join(highs, f"{name} High")


ROCK_LAYOUTS = {
    "A": [(-0.13, 0.0, 0.11, 0.125, 0.09, 10), (0.01, 0.12, 0.13, 0.12, 0.085, 55), (0.15, -0.01, 0.15, 0.12, 0.1, 100)],
    "B": [(-0.08, -0.04, 0.17, 0.14, 0.12, 30), (0.13, 0.08, 0.12, 0.1, 0.08, 75), (0.1, -0.14, 0.09, 0.08, 0.065, 140),
          (-0.18, 0.15, 0.08, 0.07, 0.06, 200)],
    "C": [(-0.16, -0.06, 0.1, 0.09, 0.075, 15), (0.0, 0.05, 0.12, 0.11, 0.09, 60), (0.16, 0.0, 0.1, 0.085, 0.07, 120),
          (-0.04, -0.16, 0.08, 0.075, 0.06, 170), (0.08, 0.18, 0.075, 0.07, 0.055, 230)],
}


def build_rocks(coll):
    pairs = [rock_cluster(f"Small Rocks {key}", layout, 31 + i, coll) for i, (key, layout) in enumerate(ROCK_LAYOUTS.items())]
    material = rock_material()
    for _, high in pairs:
        high.data.materials.append(material)
    return [low for low, _ in pairs], [high for _, high in pairs]


# ----- Sticks -------------------------------------------------------------------------------------------------------

def stick(bm, uv, start, direction, length, radius, rng, lift, forks):
    """One fallen branch lying on the ground, tapering, with side twigs, broken off at both ends."""
    segments = max(6, int(length / 0.04))
    points = bent_path(start + Vector((0, 0, radius)), direction, length, segments, rng.uniform(-0.08, 0.08), rng, lift=lift)
    radii = [radius * (1.0 - 0.45 * i / segments) for i in range(segments + 1)]
    tube(bm, points, radii, 8, uv, jagged=rng.uniform(0.3, 1.0), rng=rng)
    for _ in range(forks):
        at = rng.randint(segments // 4, segments * 3 // 4)
        base = points[at]
        along = (points[at + 1] - points[at]).normalized()
        side = Vector((-along.y, along.x, 0)).normalized() * rng.choice((-1, 1))
        twig_dir = (along * 0.75 + side * 0.65).normalized()
        twig_len = rng.uniform(0.1, 0.2)
        twig_r = radii[at] * rng.uniform(0.35, 0.5)
        twig_segments = max(4, int(twig_len / 0.03))
        twig = bent_path(base, twig_dir, twig_len, twig_segments, rng.uniform(-0.15, 0.15), rng)
        # The twig rests on the ground too: lower its far end to the ground.
        for i, p in enumerate(twig):
            t = i / twig_segments
            p.z = base.z * (1 - t) + twig_r * t
        twig_radii = [twig_r * (1.0 - 0.6 * i / twig_segments) for i in range(twig_segments + 1)]
        tube(bm, twig, twig_radii, 6, uv, jagged=0.6, rng=rng)


STICK_LAYOUTS = {
    # (start x, y, direction degrees, length, radius, lift, forks)
    "A": [(-0.2, -0.22, 62, 0.46, 0.016, 0.0, 1), (-0.27, 0.05, 15, 0.5, 0.018, 0.025, 0), (0.12, -0.25, 105, 0.42, 0.014, 0.03, 1)],
    "B": [(-0.3, -0.1, 20, 0.6, 0.02, 0.0, 1), (-0.15, -0.3, 75, 0.52, 0.017, 0.035, 1), (0.05, 0.25, -40, 0.44, 0.015, 0.04, 0),
          (0.25, -0.2, 120, 0.36, 0.012, 0.03, 1)],
    "C": [(-0.28, -0.18, 35, 0.55, 0.019, 0.0, 0), (-0.3, 0.0, 10, 0.58, 0.017, 0.03, 1), (-0.25, 0.2, -12, 0.5, 0.016, 0.045, 0),
          (-0.05, -0.3, 80, 0.48, 0.014, 0.05, 1), (0.2, -0.25, 110, 0.4, 0.012, 0.04, 0)],
}


def build_sticks(coll):
    bark = bark_material()
    wood = broken_wood_material()
    objects = []
    for i, (key, layout) in enumerate(STICK_LAYOUTS.items()):
        rng = random.Random(70 + i)
        bm = bmesh.new()
        uv = bm.loops.layers.uv.new("grain")
        for (x, y, degrees, length, radius, lift, forks) in layout:
            d = Vector((math.cos(math.radians(degrees)), math.sin(math.radians(degrees)), 0))
            # Half again as thick as real twigs, so a heap reads from the game's camera 15 m up.
            stick(bm, uv, Vector((x, y, 0)), d, length, radius * 1.5, rng, lift * 1.5, forks)
        obj = mesh_object(f"Sticks {key}", bm, coll, (bark, wood))
        centre_on_ground(obj)
        for poly in obj.data.polygons:
            poly.use_smooth = poly.material_index == 0
        objects.append(obj)
    return objects


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    rows = []
    if "rocks" in WANTED:
        rows.append(make_asset("Small Rocks", build_rocks, 1024, collection("Small Rocks"), cavity_distance=0.03,
                               lods=(0.5, 0.25)))
    if "sticks" in WANTED:
        rows.append(make_asset("Sticks", build_sticks, 1024, collection("Sticks"), cavity_distance=0.03,
                               lods=(0.5, 0.25)))
    depth = stage(rows)
    camera((0.0, -depth - 3.4, 4.4), (0.0, -depth * 0.5, 0.2), lens=40)
    render(PREVIEW, 1600, 1000)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "Forage.blend"))
    print("FORAGE DONE")


if __name__ == "__main__":
    main()
