"""The raid event sites (Prefabs/Raids/Events/Sites: what a rumour or a rescue sends the player to in a raid), built from
the Haven's building kit (building_kit.py, building_props.py) in place of the shapes of Unity primitives they were
made of. Each site is a function here; build() writes Assets/_Project/Art/Raids/Sites/<Site>/<Site>.fbx with its levels
of detail, and AgentScripts/BuildStructureModels.cs (RaidSites) puts the look in the site prefab's "Model".

A site is seen from every side (the raid's camera turns with the player), so none has a front; it stands on the ground
at its middle, about as wide as the site's 2 m trigger and the stand-in it replaces.

Run: blender -b --factory-startup --python Tools/Blender/raid_sites.py -- "<project root>" [Site ...] [--preview]
"""
import math
import os
import sys

from mathutils import Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from building_kit import *  # noqa: E402,F401,F403
from building_props import *  # noqa: E402,F401,F403
from prop_bake import ROOT, args  # noqa: E402

FOLDER = os.path.join(ROOT, "Assets", "_Project", "Art", "Raids", "Sites")


def night_cairn(bm, level, rng):
    """The Night Cairn: a tall cairn of field stones narrowing to a cap slab, smaller stones and antlers at its foot."""
    y, radius, course = 0.18, 1.15, 0
    while radius > 0.25:
        count = max(3, round(math.tau * radius / 0.62))
        for k in range(count):
            angle = math.tau * (k + 0.5 * (course % 2)) / count + rng.uniform(-0.08, 0.08)
            size = (rng.uniform(0.5, 0.7), rng.uniform(0.32, 0.42), rng.uniform(0.42, 0.55))
            boulder(bm, (radius * math.cos(angle), y, radius * math.sin(angle)), size, level, rng, yaw=angle + math.pi / 2)
        mass(bm, (-radius * 0.8, max(0.0, y - 0.25), -radius * 0.8), (radius * 0.8, y + 0.2, radius * 0.8), level, 3.0 + course,
             material=BOULDER, cell=9.0)
        y += 0.3
        radius -= 0.16
        course += 1
    boulder(bm, (0.0, y + 0.05, 0.0), (0.75, 0.22, 0.6), level, rng, flat=0.7)
    boulder(bm, (0.05, y + 0.4, 0.0), (0.32, 0.55, 0.3), level, rng)
    if level["small"]:
        for k in range(7):
            angle = rng.uniform(0, math.tau)
            r = rng.uniform(1.5, 2.1)
            boulder(bm, (r * math.cos(angle), 0.08, r * math.sin(angle)), (rng.uniform(0.2, 0.4), 0.18, rng.uniform(0.2, 0.35)), level, rng)
        antlers(bm, 0.0, 0.35, -1.42, level, rng)


def livs_circle(bm, level, rng):
    """Liv's Circle: seven standing stones leaning a little, one fallen, a flat stone in the middle."""
    count = 7
    for k in range(count):
        angle = math.tau * k / count + rng.uniform(-0.1, 0.1)
        r = 2.25 + rng.uniform(-0.12, 0.12)
        if k == 4:
            boulder(bm, (r * math.cos(angle), 0.25, r * math.sin(angle)), (1.5, 0.5, 0.6), level, rng, yaw=angle, flat=0.35)
            continue
        height = rng.uniform(1.5, 2.1)
        if not level["div"]:
            # The farthest level has no field stones (boulder draws none): a plain block for each.
            cx, cz = r * math.cos(angle), r * math.sin(angle)
            mass(bm, (cx - 0.25, 0.0, cz - 0.25), (cx + 0.25, height - 0.15, cz + 0.25), level, float(k), material=BOULDER, cell=9.0)
            continue
        before = len(bm.verts)
        boulder(bm, (r * math.cos(angle), height / 2 - 0.12, r * math.sin(angle)), (0.62, height, 0.42), level, rng,
                yaw=-angle + math.pi / 2, flat=0.35)
        # Lean it a little out of true, about its foot.
        bm.verts.ensure_lookup_table()
        lean = Matrix.Rotation(rng.uniform(-0.12, 0.12), 3, 'X') @ Matrix.Rotation(rng.uniform(-0.1, 0.1), 3, 'Y')
        foot = at(r * math.cos(angle), 0.0, r * math.sin(angle))
        for v in bm.verts[before:]:
            v.co = foot + lean @ (v.co - foot)
    boulder(bm, (0.0, 0.1, 0.0), (1.2, 0.26, 0.95), level, rng, flat=0.8)


def tors_shelter(bm, level, rng):
    """Tor's Shelter, built too well: a lean-to on two forked posts, its slope close-boarded and barked, a bed of hides
    under it, split wood stacked neat and a sawhorse with a half-cut log beside it."""
    half, high, back = 1.4, 1.65, 1.0
    for x in (-half, half):
        log(bm, (x, 0.0, -0.55), (x, high + 0.12, -0.55), 0.09, level, rng, wobble=0.004)
    log(bm, (-half - 0.3, high, -0.55), (half + 0.3, high, -0.55), 0.08, level, rng)
    slope = math.hypot(back + 0.55, high)
    pitch = math.atan2(high, back + 0.55)
    turn = Matrix.Rotation(pitch, 3, 'X')  # falling towards +z, as a sod roof's +z slope
    mid_z, mid_y = (-0.55 + back) / 2, high / 2
    for k in range(9):
        x = -half - 0.15 + k * (half * 2 + 0.3) / 8
        log(bm, (x, high + 0.05, -0.62), (x, 0.0, back), 0.05, level, rng, wobble=0.004, sides=6)
    box(bm, (0.0, mid_y + 0.07, mid_z), (half * 2 + 0.5, 0.05, slope), PLANKS, turn=turn, grain='z')
    box(bm, (0.0, mid_y + 0.11, mid_z), (half * 2 + 0.7, 0.025, slope + 0.1), BIRCH, turn=turn, grain='z')
    if level["small"]:
        box(bm, (-0.3, 0.08, 0.15), (1.7, 0.12, 0.85), HIDE, grain='x')
        firewood(bm, half + 0.35, half + 1.25, 0.2, level, rng, rows=4, depth=0.5)
        sawhorse(bm, -0.2, -1.55, 1.3, level, rng)
        chopping_block(bm, 1.25, -1.35, level, rng)


def gunnars_pit(bm, level, rng):
    """Gunnar's Pit: the dark mouth of a hunter's pit in a ring of thrown-up earth, sharpened stakes leaning in round
    it, the branches that hid it fallen through at one side, bones by the rim."""
    radius = 1.25
    # The thrown-up earth: a ring of low lumps round the mouth.
    count = 12
    for k in range(count):
        angle = math.tau * k / count + rng.uniform(-0.1, 0.1)
        r = radius + 0.25
        boulder(bm, (r * math.cos(angle), 0.06, r * math.sin(angle)), (rng.uniform(0.7, 0.95), rng.uniform(0.22, 0.32), 0.6), level, rng,
                material=SOIL, yaw=-angle + math.pi / 2, flat=0.3)
    disc(bm, (0.0, 0.1, 0.0), radius + 0.1, IRON, sides=16)
    count = 10
    for k in range(count):
        angle = math.tau * k / count + rng.uniform(-0.1, 0.1)
        foot = (radius + 0.5) * 1.0
        log(bm, (foot * math.cos(angle), 0.0, foot * math.sin(angle)),
            ((radius + 0.1) * math.cos(angle), rng.uniform(0.8, 1.05), (radius + 0.1) * math.sin(angle)), 0.05, level, rng,
            wobble=0.0, sides=5, cap=0.06)
    if level["small"]:
        for k in range(6):
            a = rng.uniform(-0.5, 0.5)
            log(bm, (-1.2 + k * 0.12, 0.2, -0.9 + a), (0.4 + k * 0.1, 0.05 - k * 0.02, 0.5 + a), 0.03, level, rng, wobble=0.01, sides=4)
        for k in range(4):
            a = rng.uniform(0, math.tau)
            log(bm, (1.7 * math.cos(a), 0.05, 1.7 * math.sin(a)), (1.7 * math.cos(a) + 0.35, 0.06, 1.7 * math.sin(a) + 0.1), 0.025, level, rng,
                material=BIRCH, wobble=0.0, sides=4)


SITES = {
    "Night Cairn": night_cairn,
    "Livs Circle": livs_circle,
    "Tors Shelter": tors_shelter,
    "Gunnars Pit": gunnars_pit,
}

if __name__ == "__main__":
    wanted = [a for a in args[1:] if not a.startswith("--")] or list(SITES)
    if "--preview" not in args:
        args.append("--no-preview")
    for site in wanted:
        build(site, SITES[site], script="raid_site_" + site.lower().replace(" ", "_"), folder=FOLDER, keep_blend=False)
