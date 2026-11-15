"""The Haven town's built pieces (Docs/BuildingSystemResearch.md §3.4, 2026-11-15): station extensions set beside the
Smithy, the Woodcutter's Lodge and the Kitchen, and two more fences that close yards. Made from the Haven's building kit
and haven_decor.py's helpers, so they share the kit buildings' tiling scanned materials, levels of detail and export.

Assets (Assets/_Project/Art/Haven/Structures/<Asset>/<Asset>.fbx with "<Asset>", "<Asset> LOD1", "<Asset> LOD2"):
- Smithy: Bellows, Quench Tub, Tool Rack.
- Woodcutter's Lodge: Chopping Block, Log Rack, Sawhorse.
- Kitchen: Smoke Rack, Larder, Herb Garden.
- Fences, laid in lines like the Skigard (4.12 m a piece, along x): Hedge, Dry-stone Wall.

Drawn in the kit's design axes (x right, y up, the camera's side at -z), pivot at the ground centre, as haven_decor.py.
Writes Tools/Blender/haven_town_manifest.json (sizes, triangles, budgets) and previews in Tools/Blender/decor_previews/.

Run: blender -b --factory-startup --python Tools/Blender/haven_town_props.py -- "<project root, absolute>"
         [--only "Bellows,Hedge"] [--res 700] [--no-preview]
"""
import json
import math
import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import haven_decor as decor  # noqa: E402
from building_kit import (BOULDER, CLOTH, DYE_OCHRE, HIDE, IRON, LINEN, PLANKS, SOIL, TROLLSTONE, TURF, WOOD,  # noqa: E402
                          barrel, boulder, box, log, materials)
from haven_decor import R, args, build_asset, collection, flower, lathe, pick, rod, rosette, rot, turned  # noqa: E402

MANIFEST = os.path.join(decor.BLENDER_DIR, "haven_town_manifest.json")
PIECE = 4.12  # a Skigard piece's length, so a gate joins any of these fences


# ----- The Smithy's -----------------------------------------------------------------------------------------------------

def bellows(bm, level):
    """A great leather bellows on a low trestle: two wedge boards, the hide between them, an iron nozzle to the front
    left (the forge's side), a handle rising at the back."""
    for sx in (-1, 1):
        x = sx * 0.42
        rod(bm, [(x, 0.0, -0.2), (x, 0.52, 0.0)], 0.04, pick(level, 6, 5, 4), WOOD)
        rod(bm, [(x, 0.0, 0.2), (x, 0.52, 0.0)], 0.04, pick(level, 6, 5, 4), WOOD)
    box(bm, (0.0, 0.54, 0.0), (1.0, 0.05, 0.22), PLANKS, grain='x')
    box(bm, (0.0, 0.6, 0.0), (0.92, 0.04, 0.52), PLANKS, grain='x')
    # The hide: fuller at the back where the boards open, pinched at the nozzle.
    turned(bm, (0.0, 0.66, 0.0), rot('Z', 6), box, (-0.02, 0.69, 0.0), (0.8, 0.14, 0.46), HIDE)
    turned(bm, (0.0, 0.76, 0.0), rot('Z', 11), box, (0.0, 0.78, 0.0), (0.94, 0.04, 0.52), PLANKS, grain='x')
    rod(bm, [(0.46, 0.66, 0.0), (0.82, 0.6, 0.0)], [0.06, 0.035], pick(level, 8, 6, 4), IRON)
    rod(bm, [(-0.42, 0.84, 0.0), (-0.66, 1.08, 0.0)], 0.03, pick(level, 6, 4, 4), WOOD)
    if level["small"]:
        box(bm, (-0.7, 1.1, 0.0), (0.06, 0.06, 0.34), WOOD, grain='z')


def quench_tub(bm, level):
    """A broad, low tub of water, banded with iron, a pair of tongs leaning on its rim and a stone to stand on."""
    staves = dict(level, sides=pick(level, 14, 10, 7))
    barrel(bm, 0.0, 0.0, staves, height=0.5, radius=0.45, water=True)
    if level["small"]:
        for dx in (-0.025, 0.025):
            rod(bm, [(0.3 + dx, 0.02, -0.5), (0.2 + dx, 0.62, -0.22), (0.12 + dx, 0.56, 0.0)], 0.014, 4, IRON)
    boulder(bm, (-0.55, 0.07, -0.38), (0.36, 0.14, 0.28), dict(level, div=pick(level, 2, 1, 1)), R(31, 1), material=BOULDER, flat=0.6)


def tool_rack(bm, level):
    """An upright rack of planks: two posts on feet, two rails, hammers, tongs and a file hanging from pegs."""
    for sx in (-1, 1):
        box(bm, (sx * 0.56, 0.75, 0.0), (0.08, 1.5, 0.08), WOOD)
        box(bm, (sx * 0.56, 0.04, 0.0), (0.1, 0.08, 0.5), WOOD, grain='z')
    for y in (0.55, 1.3):
        box(bm, (0.0, y, 0.0), (1.22, 0.1, 0.04), PLANKS, grain='x')
    box(bm, (0.0, 0.92, 0.03), (1.04, 0.64, 0.02), PLANKS, grain='x')
    if not level["small"]:
        return
    for k, x in enumerate((-0.38, -0.12, 0.14, 0.4)):
        rod(bm, [(x, 1.24, -0.03), (x, 1.24, -0.1)], 0.012, 4, WOOD)
        if k % 2 == 0:  # a hammer: handle down, head at the peg
            rod(bm, [(x, 1.2, -0.08), (x, 0.82, -0.08)], 0.02, 5, WOOD)
            box(bm, (x, 1.2, -0.08), (0.16, 0.06, 0.06), IRON, grain='x')
        else:  # tongs, open a little
            rod(bm, [(x - 0.02, 1.22, -0.08), (x - 0.05, 0.78, -0.08)], 0.012, 4, IRON)
            rod(bm, [(x + 0.02, 1.22, -0.08), (x + 0.05, 0.78, -0.08)], 0.012, 4, IRON)
    box(bm, (0.0, 0.42, -0.07), (0.5, 0.04, 0.04), IRON, grain='x')


# ----- The Woodcutter's Lodge's ----------------------------------------------------------------------------------------

def chopping_block(bm, level):
    """A broad stump of a block, its top scarred, an axe bitten into it, chips scattered round."""
    log(bm, (0.0, 0.0, 0.0), (0.0, 0.5, 0.0), 0.32, level, R(41, 0), uneven=0.05, cap=0.01, sides=pick(level, 12, 8, 6))
    rod(bm, [(0.06, 0.52, 0.0), (0.36, 0.96, -0.08)], 0.025, pick(level, 6, 5, 4), WOOD)
    box(bm, (0.04, 0.53, 0.0), (0.2, 0.1, 0.03), IRON, turn=rot('Z', 30), grain='x')
    if level["lod"] == 0:
        for k in range(9):
            rng = R(42, k)
            a, r = rng.uniform(0, math.tau), rng.uniform(0.4, 0.75)
            box(bm, (r * math.cos(a), 0.012, r * math.sin(a)), (rng.uniform(0.06, 0.12), 0.02, rng.uniform(0.03, 0.06)), PLANKS,
                turn=rot('Y', math.degrees(a)), grain='x')


def log_rack(bm, level):
    """Logs laid lengthwise between two pairs of posts, three rows, on two bearers: the charcoal and plank stock."""
    for z in (-0.25, 0.25):
        box(bm, (0.0, 0.05, z), (1.9, 0.1, 0.12), WOOD, grain='x')
    for sx in (-1, 1):
        for z in (-0.38, 0.38):
            rod(bm, [(sx * 0.85, 0.0, z), (sx * 0.85, 1.0, z)], 0.05, pick(level, 6, 5, 4), WOOD)
    rows = [(5, 0.2), (4, 0.43), (3, 0.66)]
    for row, (count, y) in enumerate(rows):
        for k in range(count):
            z = (k - (count - 1) / 2) * 0.24
            if level["lod"] == 2 and row < 2 and 0 < k < count - 1:
                continue  # the middle of the lower rows is hidden behind the ends
            log(bm, (-0.8, y, z), (0.8, y, z), 0.11, level, R(50 + row, k), sides=pick(level, 8, 6, 5))


def sawhorse(bm, level):
    """A sawhorse of crossed legs with a log across it, a bow saw leaning on its end."""
    for sx in (-1, 1):
        x = sx * 0.42
        rod(bm, [(x, 0.0, -0.32), (x, 0.82, 0.12)], 0.04, pick(level, 6, 5, 4), WOOD)
        rod(bm, [(x, 0.0, 0.32), (x, 0.82, -0.12)], 0.04, pick(level, 6, 5, 4), WOOD)
    box(bm, (0.0, 0.64, 0.0), (1.0, 0.06, 0.06), PLANKS, grain='x')
    log(bm, (-0.75, 0.9, 0.0), (0.7, 0.86, 0.0), 0.13, level, R(61, 0), sides=pick(level, 9, 6, 5))
    if level["small"]:
        # The bow saw: a bent wooden frame and the thin blade along its foot.
        rod(bm, [(0.62, 0.0, -0.3), (0.66, 0.45, -0.32), (0.72, 0.62, -0.3)], 0.016, 4, WOOD)
        rod(bm, [(0.98, 0.0, -0.3), (0.94, 0.45, -0.32), (0.86, 0.62, -0.3)], 0.016, 4, WOOD)
        rod(bm, [(0.72, 0.62, -0.3), (0.86, 0.62, -0.3)], 0.012, 4, WOOD)
        box(bm, (0.8, 0.05, -0.3), (0.36, 0.05, 0.006), IRON, grain='x')


# ----- The Kitchen's ---------------------------------------------------------------------------------------------------

def smoke_rack(bm, level):
    """A rack of poles over a smouldering pit: split fish and strips of meat hang from the crossbars."""
    for sx in (-1, 1):
        for sz in (-1, 1):
            rod(bm, [(sx * 0.55, 0.0, sz * 0.32), (sx * 0.5, 1.45, sz * 0.28)], 0.035, pick(level, 6, 5, 4), WOOD)
    for z in (-0.28, 0.28):
        rod(bm, [(-0.62, 1.38, z), (0.62, 1.38, z)], 0.03, pick(level, 6, 5, 4), WOOD)
    stones = dict(level, div=pick(level, 2, 1, 1))
    for k in range(7):
        rng = R(71, k)
        a = math.tau * k / 7
        boulder(bm, (0.32 * math.cos(a), 0.07, 0.22 * math.sin(a)), (0.17, 0.13, 0.15), stones, rng, flat=0.3)
    lathe(bm, (0.0, 0.0, 0.0), [(0.28, 0.0), (0.22, 0.05), (0.0, 0.06)], pick(level, 9, 7, 5), SOIL, planar=True)
    for k in range(6 if level["small"] else 3):
        rng = R(72, k)
        x = -0.45 + k * (0.9 / 5 if level["small"] else 0.45)
        z = -0.28 if k % 2 else 0.28
        length = rng.uniform(0.32, 0.48)
        colour = HIDE if k % 3 else DYE_OCHRE
        box(bm, (x, 1.34 - length / 2, z), (0.1, length, 0.03), colour, turn=rot('Y', rng.uniform(-20, 20)))


def larder(bm, level):
    """A cool larder: a plank store on stone feet with a sloping lid, a crock and a sack beside it."""
    for sx in (-1, 1):
        for sz in (-1, 1):
            boulder(bm, (sx * 0.36, 0.08, sz * 0.26), (0.2, 0.16, 0.2), dict(level, div=pick(level, 2, 1, 1)), R(81, sx + 2 * sz + 3), flat=0.5)
    box(bm, (0.0, 0.52, 0.0), (0.92, 0.66, 0.7), PLANKS, grain='y')
    turned(bm, (0.0, 0.9, 0.0), rot('X', -14), box, (0.0, 0.9, 0.0), (1.02, 0.06, 0.82), PLANKS, grain='x')
    if level["small"]:
        box(bm, (0.0, 0.52, -0.355), (0.5, 0.5, 0.02), WOOD)
        box(bm, (0.2, 0.52, -0.37), (0.04, 0.12, 0.02), IRON)
        lathe(bm, (0.66, 0.0, -0.18), [(0.12, 0.0), (0.16, 0.12), (0.13, 0.26), (0.09, 0.3), (0.0, 0.3)], pick(level, 8, 6, 5), CLOTH)
        boulder(bm, (-0.68, 0.18, -0.12), (0.3, 0.36, 0.26), dict(level, div=pick(level, 2, 2, 1)), R(82, 0), material=LINEN, flat=0.35)


def herb_garden(bm, level):
    """A raised bed of planks full of dark earth and herbs: a kitchen's own garden."""
    sx, sz, h = 0.72, 0.46, 0.26
    for dz in (-1, 1):
        box(bm, (0.0, h / 2, dz * sz), (sx * 2 + 0.06, h, 0.05), PLANKS, grain='x')
    for dx in (-1, 1):
        box(bm, (dx * sx, h / 2, 0.0), (0.05, h, sz * 2 - 0.05), PLANKS, grain='z')
    box(bm, (0.0, h - 0.04, 0.0), (sx * 2 - 0.05, 0.06, sz * 2 - 0.05), SOIL, grain='x')
    count = 8 if level["small"] else 4
    for k in range(count):
        rng = R(91, k)
        x = -sx + 0.2 + (k % 4) * (sx * 2 - 0.4) / 3
        z = -0.2 if k < 4 else 0.2
        if count == 4:
            z = 0.0
        rosette(bm, (x, h - 0.02, z), rng.uniform(0.13, 0.17), rng.uniform(0.1, 0.16), pick(level, 7, 5, 4), TROLLSTONE, rng)
        if level["lod"] == 0 and k % 3 == 0:
            flower(bm, (x + 0.04, h + 0.14, z), 0.04, LINEN, petals=5)


# ----- Fences ----------------------------------------------------------------------------------------------------------

def hedge(bm, level):
    """A clipped hedge a piece long: a row of leafy masses grown into one, a little lumpy on top, woody stems at its
    foot. Turf-green so it reads as leaves from above."""
    half = PIECE / 2
    count = pick(level, 7, 5, 3)
    width = PIECE / count * 1.3
    for k in range(count):
        rng = R(101, k)
        # Ends flush with the piece's ends, so pieces meet at the posts' spacing (and a Skigard Gate fits between).
        x = -half + width / 2 + k * (PIECE - width) / max(1, count - 1)
        boulder(bm, (x, 0.58, rng.uniform(-0.03, 0.03)), (width, rng.uniform(1.05, 1.18), rng.uniform(0.74, 0.82)),
                dict(level, div=pick(level, 3, 2, 1)), rng, yaw=rng.uniform(-0.15, 0.15), material=TURF, flat=0.55)
    if level["small"]:
        for k in range(6):
            rng = R(102, k)
            x = -half + 0.3 + k * (PIECE - 0.6) / 5
            rod(bm, [(x, 0.0, -0.34), (x + rng.uniform(-0.05, 0.05), 0.22, -0.3)], 0.03, 4, WOOD)


def dry_stone_wall(bm, level):
    """A dry-stone wall a piece long: flat field stones in courses, broken joints, wider at the foot, a row of stones on
    edge along the top."""
    half = PIECE / 2
    stones = dict(level, div=pick(level, 2, 1, 1))
    courses = [(0.12, 0.62, 0.23), (0.32, 0.56, 0.2), (0.5, 0.5, 0.18)]
    for row, (y, depth, height) in enumerate(courses):
        x = -half + (0.0 if row % 2 == 0 else 0.22)
        k = 0
        while x < half - 0.05:
            rng = R(110 + row, k)
            length = min(rng.uniform(0.38, 0.56), half - x)
            if length > 0.12:
                boulder(bm, (x + length / 2, y, rng.uniform(-0.02, 0.02)), (length * 0.98, height, depth), stones, rng,
                        yaw=rng.uniform(-0.05, 0.05), flat=0.7)
            x += length
            k += 1
    # Cope stones on edge, a little apart, along the top.
    x, k = -half + 0.12, 0
    while x < half - 0.12:
        rng = R(120, k)
        boulder(bm, (x, 0.72, 0.0), (0.14, 0.24, 0.42), stones, rng, yaw=rng.uniform(-0.1, 0.1), flat=0.6)
        x += rng.uniform(0.2, 0.27)
        k += 1


ASSETS = [
    dict(name="Bellows", draw=bellows, extends="Forge", notes="Beside the Smithy's forge; the nozzle points +x."),
    dict(name="Quench Tub", draw=quench_tub, extends="Forge", notes="Water-filled tub with tongs."),
    dict(name="Tool Rack", draw=tool_rack, extends="Forge", notes="Upright rack, faces the front (+Z)."),
    dict(name="Chopping Block", draw=chopping_block, extends="Sawbench", notes="Stump with an axe in it."),
    dict(name="Log Rack", draw=log_rack, extends="Sawbench", notes="Three rows of logs between posts."),
    dict(name="Sawhorse", draw=sawhorse, extends="Sawbench", notes="Crossed legs, a log across, a bow saw."),
    dict(name="Smoke Rack", draw=smoke_rack, extends="Kitchen", notes="Poles over a smouldering pit, fish and meat hanging."),
    dict(name="Larder", draw=larder, extends="Kitchen", notes="Plank store on stone feet."),
    dict(name="Herb Garden", draw=herb_garden, extends="Kitchen", notes="Raised plank bed of herbs."),
    dict(name="Hedge", draw=hedge, fence=True, notes="A line piece 4.12 m along x, like the Skigard."),
    dict(name="Dry-stone Wall", draw=dry_stone_wall, fence=True, notes="A line piece 4.12 m along x, like the Skigard."),
]


def main():
    only = None
    if "--only" in args:
        only = [s.strip() for s in args[args.index("--only") + 1].split(",")]
    res = int(args[args.index("--res") + 1]) if "--res" in args else 700
    specs = [s for s in ASSETS if only is None or s["name"] in only]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    made = materials()
    for material in made:
        bsdf = material.node_tree.nodes.get("Principled BSDF")
        if bsdf and not any(n.type == 'TEX_IMAGE' for n in material.node_tree.nodes):
            bsdf.inputs["Roughness"].default_value = 0.85
    coll = collection("Haven Town")
    manifest = {}
    if os.path.exists(MANIFEST):
        with open(MANIFEST, encoding="utf-8") as f:
            manifest = json.load(f).get("assets", {})
    built = {}
    for spec in specs:
        levels, marks, entry = build_asset(spec, coll, made)
        built[spec["name"]] = (levels, marks)
        manifest[spec["name"]] = entry
    order = [s["name"] for s in ASSETS]
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(dict(note="Made by Tools/Blender/haven_town_props.py, as haven_decor_manifest.json.",
                       assets={k: manifest[k] for k in sorted(manifest, key=lambda k: order.index(k) if k in order else 99)}), f, indent=2)
    if "--no-preview" not in args:
        decor.previews(built, res)
    print("HAVEN TOWN DONE", list(built))


if __name__ == "__main__":
    main()
