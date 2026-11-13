"""The raid event sites (Prefabs/Raids/Events/Sites: what a rumour or a rescue sends the player to in a raid), built from
the Haven's building kit (building_kit.py, building_props.py) in place of the shapes of Unity primitives they were
made of. Each site is a function here; build() writes Assets/_Project/Art/Raids/Sites/<Site>/<Site>.fbx with its levels
of detail, and AgentScripts/BuildStructureModels.cs (RaidSites) puts the look in the site prefab's "Model".

A site is seen from every side (the raid's camera turns with the player), so none has a front; it stands on the ground
at its middle, about as wide as the site's 2 m trigger and the stand-in it replaces. Not here: the Crossroads Stranger and
the Toll Troll (characters), the Fog Bank (fog), the Deep Patch and the Elder (plants, for the plant models).

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


# ----- Shared pieces -----------------------------------------------------------------------------------------------------

def stakes(bm, level, rng, radius, count, gap=None, height=(1.0, 1.35), lean=0.25, material=WOOD):
    """A ring of sharpened stakes driven in and leaning outward, with a gap facing -z if `gap` (radians wide)."""
    for k in range(count):
        angle = math.tau * k / count + rng.uniform(-0.06, 0.06)
        if gap and abs(math.atan2(math.sin(angle + math.pi / 2), math.cos(angle + math.pi / 2))) < gap / 2:
            continue
        x, z = radius * math.cos(angle), radius * math.sin(angle)
        h = rng.uniform(*height)
        log(bm, (x, -0.1, z), (x * (1 + lean / radius * h), h, z * (1 + lean / radius * h)), 0.06, level, rng, material=material,
            wobble=0.0, sides=5, cap=0.09)


def trunk(bm, x, z, height, radius, level, rng, material=WOOD, lean=0.12):
    """A tree trunk that tapers: three lengths of log, each narrower and a little off the line of the last, flared roots
    at its foot. Returns its top (x, y, z)."""
    points = [(x, -0.2, z)]
    for k, share in enumerate((0.42, 0.75, 1.0)):
        px, _, pz = points[-1]
        points.append((px + rng.uniform(-lean, lean), height * share, pz + rng.uniform(-lean, lean)))
    for k in range(3):
        log(bm, points[k], points[k + 1], radius * (1.0, 0.78, 0.55)[k], level, rng, material=material, uneven=0.1,
            sides=None if level["small"] else 5)
    if level["small"]:
        for k in range(5):
            angle = math.tau * k / 5 + rng.uniform(-0.3, 0.3)
            log(bm, (x, 0.35, z), (x + (radius + 0.45) * math.cos(angle), -0.08, z + (radius + 0.45) * math.sin(angle)), radius * 0.42,
                level, rng, material=material, wobble=0.0, sides=6)
    return points[-1]


def dead_tree(bm, x, z, height, level, rng, material=IRON, radius=0.3):
    """A dead tree: a tapering bare trunk, long crooked limbs reaching up and out, the stumps of others."""
    tx, ty, tz = trunk(bm, x, z, height, radius, level, rng, material=material)
    if not level["small"]:
        return
    for k in range(6):
        angle = math.tau * k / 6 + rng.uniform(-0.4, 0.4)
        y = height * rng.uniform(0.5, 0.92)
        reach = rng.uniform(1.0, 1.9)
        elbow = (x + reach * 0.5 * math.cos(angle), y + reach * 0.35, z + reach * 0.5 * math.sin(angle))
        tip = (x + reach * math.cos(angle + 0.25), y + reach * rng.uniform(0.55, 1.0), z + reach * math.sin(angle + 0.25))
        log(bm, (x, y, z), elbow, radius * 0.3, level, rng, material=material, wobble=0.0, sides=6)
        log(bm, elbow, tip, radius * 0.17, level, rng, material=material, wobble=0.0, sides=5)


def patch(bm, x, z, radius, level, rng, material=IRON):
    """An irregular stain on the ground (blight, ash): flat overlapping blots, ragged at the edge."""
    for k in range(9 if level["div"] else 1):
        angle = rng.uniform(0, math.tau)
        r = 0.0 if k == 0 else rng.uniform(0.3, 0.7) * radius
        size = radius * (1.5 if k == 0 else rng.uniform(0.6, 0.9))
        boulder(bm, (x + r * math.cos(angle), 0.0, z + r * math.sin(angle)), (size, 0.06, size * rng.uniform(0.7, 1.0)), level, rng,
                material=material, flat=0.95)
    if not level["div"]:
        disc(bm, (x, 0.03, z), radius, material, sides=10)


def cookfire(bm, x, z, level, rng, embers=True):
    """A ring of field stones round a bed of ash (or embers), with a few half-burnt sticks."""
    for k in range(9):
        angle = math.tau * k / 9
        boulder(bm, (x + 0.55 * math.cos(angle), 0.08, z + 0.55 * math.sin(angle)), (0.32, 0.22, 0.26), level, rng, yaw=angle)
    disc(bm, (x, 0.05, z), 0.45, EMBER if embers else IRON, sides=10)
    if level["small"]:
        for k in range(4):
            a = math.tau * k / 4 + 0.4
            log(bm, (x + 0.38 * math.cos(a), 0.06, z + 0.38 * math.sin(a)), (x + 0.05 * math.cos(a), 0.18, z + 0.05 * math.sin(a)), 0.04, level, rng,
                material=IRON, wobble=0.0, sides=5)


def mound(bm, x, z, rx, rz, height, level, rng, material=TURF, opening=None):
    """A low grassed mound: rings of lumps over a core, highest in the middle, of turf (the game grows its grass on it,
    BuildStructureModels.RaidSites). opening (angle): leave a doorway that way."""
    mass(bm, (x - rx * 0.75, 0.0, z - rz * 0.75), (x + rx * 0.75, height * 0.85, z + rz * 0.75), level, 5.0, material=material,
         top=(x - rx * 0.3, z - rz * 0.3, x + rx * 0.3, z + rz * 0.3), bulge=0.08, cell=0.6 if level["div"] else 9.0)
    for ring, scale in ((0, 1.0), (1, 0.7)):
        count = 14 - ring * 4
        for k in range(count):
            angle = math.tau * (k + ring * 0.5) / count
            if opening is not None and abs(math.atan2(math.sin(angle - opening), math.cos(angle - opening))) < 0.35:
                continue
            px, pz = x + rx * scale * 0.85 * math.cos(angle), z + rz * scale * 0.85 * math.sin(angle)
            boulder(bm, (px, height * (0.18 + ring * 0.35), pz), (rx * 0.75, height * 0.55, rz * 0.6), level, rng, material=material,
                    yaw=-angle + math.pi / 2, flat=0.2)


def barrow_door(bm, x, z, level, rng, width=1.6, height=1.5):
    """A doorway of three big stones into a mound, facing -z, the dark beyond it."""
    for sx in (-1, 1):
        boulder(bm, (x + sx * width / 2, height / 2, z), (0.45, height + 0.1, 0.55), level, rng, yaw=0.0, flat=0.5)
    boulder(bm, (x, height + 0.2, z), (width + 0.7, 0.45, 0.65), level, rng, yaw=0.0, flat=0.6)
    box(bm, (x, height / 2, z + 0.25), (width - 0.3, height, 0.1), IRON)


# ----- Sites ---------------------------------------------------------------------------------------------------------------

def eiriks_stand(bm, level, rng):
    """Eirik's Stand: a ring of sharpened stakes round a trampled clearing, a gap where he came and went, a dead fire,
    a shield propped on a stake, a spear in the ground."""
    stakes(bm, level, rng, 2.1, 16, gap=0.8)
    cookfire(bm, 0.2, 0.3, level, rng, embers=False)
    if level["small"]:
        shield(bm, -1.6, 0.55, 1.1, level, rng)
        log(bm, (1.2, -0.2, -0.6), (1.3, 1.9, -0.75), 0.025, level, rng, wobble=0.0, sides=4)
        box(bm, (1.31, 1.95, -0.76), (0.06, 0.3, 0.02), IRON)
        box(bm, (-0.5, 0.06, -0.4), (1.5, 0.08, 0.7), HIDE, grain='x')


def sigrids_horn(bm, level, rng):
    """Sigrid's Horn: the same kind of ring of stakes, and a tall pole with a horn hung from it and a banner rag."""
    stakes(bm, level, rng, 2.0, 14, gap=0.9)
    log(bm, (0.0, -0.3, 0.6), (0.05, 3.0, 0.6), 0.07, level, rng, wobble=0.0)
    if level["small"]:
        log(bm, (0.05, 2.7, 0.6), (-0.55, 2.7, 0.6), 0.03, level, rng, wobble=0.0, sides=5)
        log(bm, (-0.45, 2.45, 0.6), (-0.6, 2.2, 0.62), 0.08, level, rng, material=BIRCH, wobble=0.0, sides=8, cap=0.0)
        log(bm, (-0.6, 2.2, 0.62), (-0.78, 2.1, 0.64), 0.12, level, rng, material=BIRCH, wobble=0.0, sides=8, cap=0.0)
        box(bm, (0.12, 2.35, 0.6), (0.03, 0.8, 0.4), CLOTH)
        cookfire(bm, -0.4, -0.5, level, rng, embers=False)


def runas_marks(bm, level, rng):
    """Runa's Marks: three old trees cut with pale marks, a cord with a knot strung between two of them."""
    for (x, z, h) in ((-1.2, 0.3, 3.4), (0.9, 0.8, 3.0), (0.2, -1.1, 3.6)):
        trunk(bm, x, z, h + 1.5, 0.34, level, rng, lean=0.08)
        if level["small"]:
            for k in range(3):
                y = 1.1 + k * 0.32
                box(bm, (x, y, z - 0.3), (0.22 if k != 1 else 0.06, 0.04 if k != 1 else 0.28, 0.06), BIRCH)
    if level["small"]:
        log(bm, (-1.2, 1.8, 0.3), (0.9, 1.7, 0.8), 0.01, level, rng, material=LINEN, wobble=0.02, sides=3)


def barrow_hoard(bm, level, rng):
    """A barrow open where it wasn't yesterday: an earth mound, a stone doorway, something gold in the dark of it."""
    mound(bm, 0.0, 0.6, 2.3, 2.0, 1.7, level, rng, opening=-math.pi / 2)
    barrow_door(bm, 0.0, -1.2, level, rng, width=1.3, height=1.3)
    for k in range(5):
        box(bm, (rng.uniform(-0.4, 0.4), 0.06 + k * 0.02, -1.3 + rng.uniform(-0.25, 0.1)), (0.12, 0.03, 0.12), COPPER,
            turn=Matrix.Rotation(rng.uniform(0, 3), 3, 'Z'))
    boulder(bm, (1.6, 0.2, -1.4), (0.8, 0.4, 0.6), level, rng)


def open_barrow(bm, level, rng):
    """The Open Barrow: a large mound, its slope slipped at one side, a stone-framed door, silver glinting inside."""
    mound(bm, 0.0, 0.8, 3.4, 3.2, 2.6, level, rng, opening=-math.pi / 2)
    barrow_door(bm, 0.0, -2.0, level, rng)
    for k in range(4):
        box(bm, (rng.uniform(-0.35, 0.35), 0.08, -2.15 + rng.uniform(-0.2, 0.1)), (0.1, 0.03, 0.1), MOONSILVER)
    for k in range(5):
        boulder(bm, (-2.4 + k * 0.35, 0.15, -1.6 - k * 0.2), (0.6, 0.3, 0.5), level, rng, material=SOIL)


def silversmiths_rest(bm, level, rng):
    """The Silversmith's Rest: a small grave mound by the ruin of his bench, his tools rusting, silver in the grass."""
    mound(bm, -0.6, 0.6, 1.6, 1.0, 0.8, level, rng)
    boulder(bm, (-0.6, 0.7, -0.35), (0.5, 1.3, 0.25), level, rng, yaw=0.0, flat=0.5)
    table(bm, 1.3, -0.4, 1.3, 0.6, level, rng, height=0.75)
    if level["small"]:
        anvil(bm, 1.6, 0.8, level, rng)
        for k in range(4):
            box(bm, (1.0 + k * 0.18, 0.82, -0.4), (0.1, 0.02, 0.1), MOONSILVER)


def camp_ruin(bm, level, rng, wheel=True):
    """An abandoned camp: a lean-to fallen in, a tarp half off it, crates and a cold fire."""
    for x in (-1.1, 1.1):
        log(bm, (x, -0.1, 0.5), (x * 0.95, 1.3, 0.5), 0.07, level, rng, wobble=0.0, sides=6)
    log(bm, (-1.3, 1.25, 0.5), (1.4, 0.9, 0.55), 0.06, level, rng, wobble=0.0, sides=6)
    box(bm, (0.1, 0.62, 1.05), (2.3, 0.03, 1.4), HIDE, turn=Matrix.Rotation(0.75, 3, 'X') @ Matrix.Rotation(0.1, 3, 'Y'))
    crate(bm, -1.6, 0.0, -0.6, (0.6, 0.5, 0.5), rng)
    crate(bm, -1.5, 0.5, -0.55, (0.45, 0.35, 0.4), rng)
    cookfire(bm, 0.6, -1.2, level, rng, embers=False)
    if wheel and level["small"]:
        cart_wheel(bm, (1.7, 0.5, -0.2), 0.5, level, rng, axis=(0.97, 0.22, 0.0))


def wainwrights_camp(bm, level, rng):
    """The Wainwright's Camp: his camp fallen in, a wheel against a crate, a small table with his book on it."""
    camp_ruin(bm, level, rng)
    table(bm, -0.4, -0.9, 0.9, 0.5, level, rng, height=0.7)
    if level["small"]:
        box(bm, (-0.4, 0.77, -0.9), (0.3, 0.06, 0.22), HIDE)


def old_bathhouse(bm, level, rng):
    """The Old Bathhouse: a little log house fallen in, its roof gone, the stove's stones in a heap, the door standing
    on its own with the plans cut into it."""
    half_x, half_z = 1.3, 1.0
    for (start, end, top) in (((-half_x, half_z), (half_x, half_z), 1.1), ((-half_x, -half_z), (-half_x, half_z), 0.8),
                              ((half_x, -half_z), (half_x, half_z), 1.4)):
        log_wall(bm, start, end, 0.05, top, level, rng, half=start[0] == end[0])
    log_wall(bm, (-half_x, -half_z), (-0.3, -half_z), 0.05, 0.6, level, rng)
    for k in range(5):
        boulder(bm, (0.5 + rng.uniform(-0.3, 0.3), 0.15 + k * 0.08, 0.3 + rng.uniform(-0.3, 0.3)), (0.4, 0.3, 0.35), level, rng)
    box(bm, (0.45, 0.85, -half_z - 0.05), (0.8, 1.6, 0.07), PLANKS, grain='y', turn=Matrix.Rotation(-0.08, 3, 'Y'))
    if level["small"]:
        for k in range(4):
            box(bm, (0.3 + k * 0.1, 0.7 + (k % 2) * 0.3, -half_z - 0.1), (0.04, 0.35, 0.01), BIRCH)
        log(bm, (-1.0, 0.1, -1.6), (0.6, 0.12, -1.9), 0.13, level, rng)


def wild_swarm(bm, level, rng):
    """The Wild Swarm: a dead tree with a hanging swarm of bees on a limb, comb in a split, a fallen limb."""
    dead_tree(bm, 0.0, 0.0, 3.2, level, rng, material=WOOD, radius=0.32)
    boulder(bm, (0.55, 2.0, 0.0), (0.45, 0.75, 0.42), level, rng, material=HIDE)
    if level["small"]:
        box(bm, (-0.05, 1.2, -0.3), (0.2, 0.35, 0.05), DYE_OCHRE)


def gathering_place(bm, level, rng):
    """The Gathering Place: a post hung with bones and antlers, a ring of stones round it, a heap of picked bones."""
    log(bm, (0.0, -0.3, 0.0), (0.05, 3.0, 0.0), 0.2, level, rng, wobble=0.02)
    log(bm, (-0.9, 2.3, 0.0), (0.95, 2.6, 0.0), 0.06, level, rng, material=BIRCH, wobble=0.02, sides=6)
    log(bm, (0.0, 2.8, -0.6), (0.05, 2.75, 0.6), 0.05, level, rng, material=BIRCH, wobble=0.02, sides=6)
    if level["small"]:
        antlers(bm, 0.05, 2.95, -0.22, level, rng)
        for k in range(5):
            x = -0.8 + k * 0.4
            log(bm, (x, 2.3 + k * 0.08, 0.0), (x + 0.03, 1.8 + k * 0.08, 0.02), 0.012, level, rng, material=LINEN, wobble=0.0, sides=3)
            boulder(bm, (x + 0.03, 1.72 + k * 0.08, 0.02), (0.16, 0.2, 0.14), level, rng, material=BIRCH)
        for k in range(8):
            a = rng.uniform(0, math.tau)
            log(bm, (0.8 * math.cos(a), 0.05, 0.8 * math.sin(a)), (0.8 * math.cos(a) + 0.4, 0.08, 0.8 * math.sin(a) + 0.1), 0.03, level, rng,
                material=BIRCH, wobble=0.0, sides=4)
    for k in range(10):
        angle = math.tau * k / 10
        boulder(bm, (1.9 * math.cos(angle), 0.15, 1.9 * math.sin(angle)), (0.45, 0.3, 0.38), level, rng, yaw=angle)


def asas_farmstead(bm, level, rng):
    """Åsa's Farmstead, burned: the black stumps of the corner posts, a fallen ridge beam, the stone hearth standing,
    a heap of ash, a bundle saved from the fire."""
    for (x, z, h) in ((-1.8, -1.3, 1.6), (1.8, -1.3, 0.9), (-1.8, 1.3, 1.2), (1.8, 1.3, 2.0)):
        log(bm, (x, -0.1, z), (x + rng.uniform(-0.08, 0.08), h, z), 0.16, level, rng, material=IRON, uneven=0.12)
    log(bm, (-1.9, 0.05, 1.0), (1.7, 1.9, 1.3), 0.15, level, rng, material=IRON, uneven=0.1)
    for k in range(3):
        log(bm, (-1.8, 0.12 + k * 0.27, 1.3), (0.2 + rng.uniform(-0.4, 0.4), 0.12 + k * 0.27, 1.3), 0.14, level, rng, material=IRON)
    chimney(bm, 0.9, 0.6, 0.0, 1.6, level, rng, half=0.45, cap=False)
    patch(bm, 0.0, 0.0, 1.5, level, rng)
    if level["small"]:
        disc(bm, (0.9, 0.4, 0.0), 0.25, EMBER, sides=8)
        sack(bm, -0.6, -1.9, level, rng, material=CLOTH, size=(0.6, 0.45, 0.45))


def yrsas_fire(bm, level, rng):
    """Yrsa's Fire: a cookfire with embers still in it, a pot on a tripod, a bedroll and a log to sit on."""
    cookfire(bm, 0.0, 0.0, level, rng, embers=True)
    for k in range(3):
        a = math.tau * k / 3 + 0.3
        log(bm, (0.8 * math.cos(a), 0.0, 0.8 * math.sin(a)), (0.0, 1.25, 0.0), 0.035, level, rng, wobble=0.0, sides=5)
    lifted(bm, 0.4, barrel, 0.0, 0.0, level, height=0.32, radius=0.2, water=False)
    log(bm, (-1.4, 0.18, -0.6), (-0.6, 0.2, -1.3), 0.18, level, rng)
    if level["small"]:
        box(bm, (1.2, 0.06, 0.6), (0.7, 0.1, 1.6), HIDE, grain='z', turn=Matrix.Rotation(0.4, 3, 'Z'))
        sack(bm, 1.5, -0.6, level, rng)


def hilds_wall(bm, level, rng):
    """Hild's Wall, round nothing: a ring of dry-stone wall waist high, a gap to come in by, nothing inside it."""
    count, radius = 16, 2.3
    for k in range(count):
        angle = math.tau * (k + 0.5) / count
        if abs(math.atan2(math.sin(angle + math.pi / 2), math.cos(angle + math.pi / 2))) < 0.3:
            continue
        for course in range(3):
            a = angle + (0.5 / count * math.tau if course % 2 else 0.0)
            boulder(bm, (radius * math.cos(a), 0.18 + course * 0.3, radius * math.sin(a)), (0.95, 0.34, 0.5), level, rng, yaw=-a + math.pi / 2)
        mass(bm, (radius * math.cos(angle) - 0.3, 0.0, radius * math.sin(angle) - 0.3), (radius * math.cos(angle) + 0.3, 0.75,
             radius * math.sin(angle) + 0.3), level, float(k), cell=9.0)


def blighted_tree(bm, level, rng):
    """The Blighted Tree: one tree dead and black in the green, the ground round it burnt dark."""
    dead_tree(bm, 0.0, 0.0, 4.6, level, rng, material=IRON, radius=0.42)
    patch(bm, 0.0, 0.0, 2.6, level, rng)


def living_stones(bm, level, rng):
    """The Living Path: three runestones across the path, one green with moss, one with claw marks, one plain."""
    for k, x in enumerate((-1.8, 0.0, 1.8)):
        boulder(bm, (x, 0.95, 0.0), (0.75, 2.1, 0.4), level, rng, yaw=k * 0.3, flat=0.45)
        if not level["div"]:
            mass(bm, (x - 0.3, 0.0, -0.15), (x + 0.3, 1.9, 0.15), level, float(k), material=BOULDER, cell=9.0)
        if level["small"]:
            for r in range(3):
                box(bm, (x, 0.7 + r * 0.35, -0.21), (0.32, 0.03, 0.01), BIRCH)
    if level["small"]:
        boulder(bm, (-1.8, 1.4, -0.12), (0.7, 1.0, 0.3), level, rng, material=TURF, yaw=0.0, flat=0.6)
        for k in range(3):
            box(bm, (1.7 + k * 0.08, 1.2, -0.22), (0.02, 0.6, 0.01), IRON, turn=Matrix.Rotation(0.3, 3, 'Y'))


def old_beacon(bm, level, rng):
    """The Old Beacon: a round stone hearth on the rise, laid with a pyre of dry split wood, a pole to light it from."""
    count = 12
    for course in range(2):
        for k in range(count):
            angle = math.tau * (k + 0.5 * course) / count
            boulder(bm, (1.2 * math.cos(angle), 0.2 + course * 0.32, 1.2 * math.sin(angle)), (0.6, 0.36, 0.45), level, rng, yaw=angle)
    mass(bm, (-1.0, 0.0, -1.0), (1.0, 0.62, 1.0), level, 2.0, cell=9.0)
    for k in range(8):
        a = math.tau * k / 8
        log(bm, (0.8 * math.cos(a), 0.62, 0.8 * math.sin(a)), (0.08 * math.cos(a), 2.1, 0.08 * math.sin(a)), 0.07, level, rng, wobble=0.0, sides=6)
    if level["small"]:
        log_pile(bm, 1.9, 0.2, 1.3, level, rng, rows=2, radius=0.12)


def trapped_pedlar(bm, level, rng):
    """Cries from the Ditch: a spruce fallen across a ditch, its broken stump, the pedlar's pack just out of reach."""
    log(bm, (-2.0, 0.35, -0.6), (2.0, 0.25, 0.9), 0.3, level, rng, uneven=0.1)
    log(bm, (-2.4, -0.2, -0.75), (-2.35, 0.8, -0.75), 0.33, level, rng, cap=0.15)
    if level["small"]:
        for k in range(6):
            t = 0.2 + k * 0.12
            x, z = -2.0 + 4.0 * t, -0.6 + 1.5 * t
            for side in (-1, 1):
                log(bm, (x, 0.4, z), (x + 0.2, 0.5 + rng.uniform(0, 0.3), z + side * 0.7), 0.03, level, rng, wobble=0.02, sides=4)
    sack(bm, 0.9, -0.9, level, rng, material=HIDE, size=(0.6, 0.55, 0.4))
    patch(bm, 0.0, 0.2, 1.4, level, rng, material=SOIL)


def overturned_wagon(bm, level, rng):
    """The Overturned Wagon: a trader's cart on its side, one wheel in splinters, parcels in the mud."""
    box(bm, (0.0, 0.5, 0.0), (0.06, 1.0, 2.6), PLANKS, grain='z')
    for sz in (-1, 1):
        box(bm, (0.38, 0.5, sz * 1.27), (0.75, 0.9, 0.06), PLANKS, grain='y')
    box(bm, (0.38, 0.95, 0.0), (0.75, 0.06, 2.6), PLANKS, grain='z')
    box(bm, (0.38, 0.05, 0.0), (0.75, 0.06, 2.6), PLANKS, grain='z')
    log(bm, (-0.1, 0.5, -0.4), (-0.4, 0.5, -0.4), 0.05, level, rng, material=IRON, wobble=0.0, sides=6)
    cart_wheel(bm, (-0.42, 0.55, -0.4), 0.52, level, rng, axis=(1.0, 0.0, 0.0))
    if level["small"]:
        for k in range(5):
            a = rng.uniform(-0.8, 0.8)
            log(bm, (-1.3, 0.05, 1.2 + k * 0.1), (-1.3 + 0.5 * math.cos(a), 0.06, 1.2 + k * 0.1 + 0.5 * math.sin(a)), 0.03, level, rng,
                material=PLANKS, wobble=0.0, sides=4)
        crate(bm, 1.4, 0.0, 1.3, (0.5, 0.4, 0.45), rng)
        sack(bm, -1.2, -1.4, level, rng, material=HIDE)
        sack(bm, 1.3, -1.5, level, rng)
        log(bm, (1.2, 0.08, -0.3), (2.6, 0.12, -0.6), 0.05, level, rng, wobble=0.0, sides=6)


def bjarnes_forge(bm, level, rng):
    """Bjarne's Forge: a smith's open-air forge in the woods, the stone hearth and its chimney stump, an anvil, a quench
    barrel, cold."""
    round_forge(bm, 0.0, -0.4, level, rng)
    chimney(bm, 0.0, 0.5, 0.0, 2.2, level, rng, half=0.4)
    anvil(bm, 1.3, 0.3, level, rng)
    barrel(bm, -1.2, 0.4, level, height=0.7, radius=0.3)
    if level["small"]:
        crate(bm, 1.1, 0.0, -1.2, (0.5, 0.4, 0.45), rng)


def heartroot_stump(bm, level, rng):
    """The Heartroot's Hollow: a stump as wide as a house, split down the middle, its heart black as if burnt, its roots
    sprawling, dead trees standing round it."""
    radius = 2.0
    for side in (-1, 1):
        # Each half of the split trunk: a ring of thick staves round half a circle, leaning apart.
        for k in range(9):
            angle = math.pi * k / 8 + (0 if side > 0 else math.pi)
            x, z = radius * math.cos(angle), radius * math.sin(angle)
            if abs(x) < 0.3:
                continue
            h = 2.6 + rng.uniform(-0.6, 0.5) - abs(z) * 0.3
            lean = side * 0.25
            log(bm, (x, -0.2, z), (x + lean, h, z), 0.42, level, rng, uneven=0.06, sides=8 if level["small"] else 5)
    box(bm, (0.0, 1.0, 0.0), (0.5, 2.0, radius * 1.6), IRON)
    mass(bm, (-radius * 0.8, 0.0, -radius * 0.8), (radius * 0.8, 0.5, radius * 0.8), level, 9.0, material=WOOD, cell=9.0)
    for k in range(7):
        angle = math.tau * k / 7 + rng.uniform(-0.2, 0.2)
        reach = rng.uniform(1.8, 2.8)
        log(bm, (radius * 0.9 * math.cos(angle), 0.6, radius * 0.9 * math.sin(angle)),
            ((radius + reach) * math.cos(angle), -0.1, (radius + reach) * math.sin(angle)), 0.3, level, rng, uneven=0.15, sides=7)
    for k in range(5):
        angle = math.tau * k / 5 + 0.4
        dead_tree(bm, 6.5 * math.cos(angle), 6.5 * math.sin(angle), rng.uniform(4.0, 5.5), level, rng, material=WOOD, radius=0.3)


SITES = {
    "Night Cairn": night_cairn,
    "Livs Circle": livs_circle,
    "Tors Shelter": tors_shelter,
    "Gunnars Pit": gunnars_pit,
    "Eiriks Stand": eiriks_stand,
    "Sigrids Horn": sigrids_horn,
    "Runas Marks": runas_marks,
    "Barrow Hoard": barrow_hoard,
    "Open Barrow": open_barrow,
    "Silversmiths Rest": silversmiths_rest,
    "Wainwrights Camp": wainwrights_camp,
    "Old Bathhouse": old_bathhouse,
    "Wild Swarm": wild_swarm,
    "Gathering Place": gathering_place,
    "Asas Farmstead": asas_farmstead,
    "Yrsas Fire": yrsas_fire,
    "Hilds Wall": hilds_wall,
    "Blighted Tree": blighted_tree,
    "Living Stones": living_stones,
    "Old Beacon": old_beacon,
    "Trapped Pedlar": trapped_pedlar,
    "Overturned Wagon": overturned_wagon,
    "Bjarnes Forge": bjarnes_forge,
    "Heartroot Stump": heartroot_stump,
}

if __name__ == "__main__":
    wanted = [a for a in args[1:] if not a.startswith("--")] or list(SITES)
    if "--preview" not in args:
        args.append("--no-preview")
    for site in wanted:
        build(site, SITES[site], script="raid_site_" + site.lower().replace(" ", "_"), folder=FOLDER, keep_blend=False)
