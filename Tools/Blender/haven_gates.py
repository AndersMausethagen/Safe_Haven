"""Gates for the Haven's other fences (review 2026-11-16, the user: "yards need a gate" and "more fence kinds"): until now
every yard of stone wall, palisade, hedge or dry-stone wall needed a wooden Skigard Gate. One gate per fence kind, the
same length as its fence piece so a line of them closes the ring (YardSettings › Join Distance):

- Stone Wall Gate (4 m): the Stone Wall's own model with its middle cut out, two dressed stone gate pillars over the cut
  ends, a plank door with iron straps between them.
- Palisade Gate (4 m): the Palisade's logs (stretched 1.39 along the line, as its prefab does) cut the same way, two heavy
  gate logs, a door of upright logs braced across the back.
- Hedge Gate (4.12 m): a gap in the hedge (haven_town_props.py's hedge masses either side) with a little wooden wicket.
- Dry-stone Gap (4.12 m): the dry-stone courses either side of an opening between two upright gate stones; no leaf (the
  user: "a stile or opening").

Each FBX holds "<Asset>", "<Asset> LOD1", "<Asset> LOD2" (the frame) and, for those that swing, "Gate Leaf" with its
pivot on the hinge (Unity +x, as the Skigard Gate's), one level of detail (a few hundred triangles). The cut fence
models keep their own baked material ("Stone Wall", "Palisade"); the rest uses the kit's slots (haven_decor.to_object).
Writes Assets/_Project/Art/Haven/Structures/<Asset>/<Asset>.fbx and Tools/Blender/haven_gates_manifest.json.

Run: blender -b --factory-startup --python Tools/Blender/haven_gates.py -- "<project root, absolute>" [--only "Hedge Gate"]
"""
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import haven_decor as decor  # noqa: E402
from building_kit import BOULDER, IRON, PLANKS, STONE, TURF, WOOD, boulder, box, log, materials  # noqa: E402
from haven_decor import HALF_TURN, LEVELS, R, args, collection, counts, pick, rod, to_object  # noqa: E402

STRUCTURES = os.path.join(decor.ROOT, "Assets", "_Project", "Art", "Haven", "Structures")
MANIFEST = os.path.join(decor.BLENDER_DIR, "haven_gates_manifest.json")
PIECE = 4.12  # the Skigard's, Hedge's and Dry-stone Wall's piece
WALL = 4.0    # the Stone Wall's and (stretched) Palisade's

# Where the gate posts stand (the middle of each, either side), and the leaf between their inner faces.
STONE_POST, STONE_POST_WIDTH = 1.15, 0.42
PALISADE_POST, PALISADE_POST_RADIUS = 1.15, 0.22
HEDGE_POST = 0.7
DRY_POST = 0.78


# ----- The cut fence models ---------------------------------------------------------------------------------------------

def imported_levels(asset, stretch_x=1.0):
    """The fence's three levels of detail as meshes in Blender's axes (the FBX's turn applied), stretched along the line
    as its prefab is. Its collider mesh is left out."""
    path = os.path.join(STRUCTURES, asset, asset + ".fbx")
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    found = {}
    for obj in set(bpy.data.objects) - before:
        if obj.type != 'MESH' or obj.name.endswith("Collider"):
            continue
        obj.data.transform(Matrix.Diagonal((stretch_x, 1.0, 1.0, 1.0)) @ obj.matrix_basis)
        obj.matrix_basis = Matrix.Identity(4)
        lod = 2 if obj.name.endswith("LOD2") else 1 if obj.name.endswith("LOD1") else 0
        found[lod] = obj
    for obj in set(bpy.data.objects) - before:
        if obj not in found.values():
            bpy.data.objects.remove(obj)
    return [found[i] for i in range(3)]


def cut_ends(obj, keep_from):
    """The model with its middle taken out: what lies beyond keep_from either side of its middle (along x) stays."""
    pieces = []
    for side in (1, -1):
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        # clear_inner drops what lies behind the plane's normal: keep the outer end.
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(side * keep_from, 0.0, 0.0), plane_no=(side, 0.0, 0.0), clear_inner=True)
        mesh = bpy.data.meshes.new(obj.name + (" R" if side > 0 else " L"))
        bm.to_mesh(mesh)
        bm.free()
        pieces.append(mesh)
    return pieces


def add_mesh(bm, mesh, material_offset):
    """Appends a mesh (Blender axes) to the bmesh, its material slots after the kit's, its UVs in the kit's own UV layer
    (under another name they would come out as a second UV channel, and the first would be empty for these faces)."""
    for poly in mesh.polygons:
        poly.material_index += material_offset
    kit_uv = bm.loops.layers.uv.verify().name
    if mesh.uv_layers:
        mesh.uv_layers.active.name = kit_uv
        for extra in [layer for layer in mesh.uv_layers if layer.name != kit_uv]:
            mesh.uv_layers.remove(extra)
    bm.from_mesh(mesh)


# ----- The kit parts ----------------------------------------------------------------------------------------------------

def stone_pillars(bm, level):
    """Two dressed stone gate pillars, a little taller than the wall, a cap slab on each."""
    for sx in (-1, 1):
        x = sx * STONE_POST
        box(bm, (x, 1.12, 0.0), (STONE_POST_WIDTH, 2.24, 1.2), STONE)
        box(bm, (x, 2.3, 0.0), (STONE_POST_WIDTH + 0.1, 0.12, 1.3), STONE, grain='x')


def palisade_posts(bm, level):
    """Two heavy gate logs, taller than the palisade, their tops cut square."""
    for sx in (-1, 1):
        log(bm, (sx * PALISADE_POST, -0.15, 0.0), (sx * PALISADE_POST, 2.65, 0.0), PALISADE_POST_RADIUS, level, R(201, sx + 1),
            sides=pick(level, 12, 8, 6))


def hedge_sides(bm, level):
    """The hedge either side of a gap: its leafy masses, as haven_town_props.hedge, from the wicket's posts to the ends,
    and the two wicket posts."""
    half = PIECE / 2
    for sx in (-1, 1):
        start, end = HEDGE_POST + 0.06, half
        count = pick(level, 3, 2, 1)
        width = (end - start) / count * 1.3
        for k in range(count):
            rng = R(301 + (sx + 1), k)
            mid = start + width / 2 + k * ((end - start) - width) / max(1, count - 1) if count > 1 else (start + end) / 2
            boulder(bm, (sx * mid, 0.58, rng.uniform(-0.03, 0.03)), (width, rng.uniform(1.05, 1.18), rng.uniform(0.74, 0.82)),
                    dict(level, div=pick(level, 3, 2, 1)), rng, yaw=rng.uniform(-0.15, 0.15), material=TURF, flat=0.55)
        rod(bm, [(sx * HEDGE_POST, 0.0, 0.0), (sx * HEDGE_POST, 1.08, 0.0)], 0.055, pick(level, 6, 5, 4), WOOD)


def dry_stone_sides(bm, level):
    """The dry-stone courses either side of the opening, as haven_town_props.dry_stone_wall, and an upright gate stone
    standing at each side of it."""
    half = PIECE / 2
    stones = dict(level, div=pick(level, 2, 1, 1))
    courses = [(0.12, 0.62, 0.23), (0.32, 0.56, 0.2), (0.5, 0.5, 0.18)]
    for sx in (-1, 1):
        start = DRY_POST + 0.12
        for row, (y, depth, height) in enumerate(courses):
            x = start + (0.0 if row % 2 == 0 else 0.16)
            k = 0
            while x < half - 0.05:
                rng = R(410 + row + (sx + 1) * 5, k)
                length = min(rng.uniform(0.38, 0.56), half - x)
                if length > 0.12:
                    boulder(bm, (sx * (x + length / 2), y, rng.uniform(-0.02, 0.02)), (length * 0.98, height, depth), stones, rng,
                            yaw=rng.uniform(-0.05, 0.05), flat=0.7)
                x += length
                k += 1
        x, k = start + 0.1, 0
        while x < half - 0.12:
            rng = R(430 + (sx + 1), k)
            boulder(bm, (sx * x, 0.72, 0.0), (0.14, 0.24, 0.42), stones, rng, yaw=rng.uniform(-0.1, 0.1), flat=0.6)
            x += rng.uniform(0.2, 0.27)
            k += 1
        rng = R(440, sx + 1)
        boulder(bm, (sx * DRY_POST, 0.56, 0.0), (0.3, 1.14, 0.46), dict(level, div=pick(level, 2, 2, 1)), rng,
                yaw=rng.uniform(-0.08, 0.08), material=BOULDER, flat=0.75)


# ----- Leaves (drawn closed, the hinge at design x = -width/2: Unity +x) ----------------------------------------------

def plank_door(bm, width, height):
    """A door of upright planks with two iron straps and a brace across the back."""
    box(bm, (0.0, 0.08 + height / 2, 0.0), (width - 0.02, height, 0.07), PLANKS, grain='y')
    for y in (0.42, height - 0.25):
        box(bm, (0.0, 0.08 + y, -0.045), (width - 0.08, 0.08, 0.02), IRON, grain='x')
    box(bm, (0.0, 0.08 + height / 2, 0.05), (math.hypot(width - 0.2, height - 0.6), 0.1, 0.04), PLANKS, grain='x',
        turn=Matrix.Rotation(math.atan2(height - 0.6, width - 0.2), 3, 'Y'))
    rod(bm, [(width / 2 - 0.18, 0.08 + height * 0.5, -0.06), (width / 2 - 0.18, 0.08 + height * 0.5, -0.12)], 0.025, 6, IRON)


def log_door(bm, width, height):
    """Upright logs side by side, bound by two planks across the back."""
    level = LEVELS[1]
    count = 6
    radius = width / count / 2
    for k in range(count):
        x = -width / 2 + radius + k * 2 * radius
        log(bm, (x, 0.06, 0.0), (x, 0.06 + height - (k % 2) * 0.08, 0.0), radius * 0.98, level, R(501, k), sides=7)
    for y in (0.45, height - 0.35):
        box(bm, (0.0, y, radius + 0.03), (width - 0.06, 0.16, 0.05), PLANKS, grain='x')


def wicket(bm, width, height):
    """A little wicket: two stiles, three rails, a brace."""
    for sx in (-1, 1):
        box(bm, (sx * (width / 2 - 0.04), 0.06 + height / 2, 0.0), (0.07, height, 0.06), WOOD)
    for y in (0.22, 0.5, 0.8):
        box(bm, (0.0, 0.06 + y * height / 0.9, 0.0), (width - 0.1, 0.07, 0.04), PLANKS, grain='x')
    box(bm, (0.0, 0.06 + height * 0.5, 0.035), (math.hypot(width - 0.16, height * 0.62), 0.06, 0.03), PLANKS, grain='x',
        turn=Matrix.Rotation(math.atan2(height * 0.62, width - 0.16), 3, 'Y'))


GATES = [
    dict(name="Stone Wall Gate", fence="Stone Wall", stretch=1.0, cut=STONE_POST, draw=stone_pillars,
         leaf=plank_door, leaf_size=(2 * (STONE_POST - STONE_POST_WIDTH / 2) - 0.04, 1.85), length=WALL),
    dict(name="Palisade Gate", fence="Palisade", stretch=1.39, cut=PALISADE_POST, draw=palisade_posts,
         leaf=log_door, leaf_size=(2 * (PALISADE_POST - PALISADE_POST_RADIUS) - 0.04, 2.15), length=WALL),
    dict(name="Hedge Gate", draw=hedge_sides, leaf=wicket, leaf_size=(2 * HEDGE_POST - 0.12, 0.92), length=PIECE),
    dict(name="Dry-stone Gap", draw=dry_stone_sides, leaf=None, length=PIECE),
]


def build_gate(spec, coll, made):
    name = spec["name"]
    fence_levels = imported_levels(spec["fence"], spec["stretch"]) if spec.get("fence") else None
    slots = list(made)
    if fence_levels:
        for material in fence_levels[0].data.materials:
            if material not in slots:
                slots.append(material)
    objects = []
    for i, level in enumerate(LEVELS):
        bm = bmesh.new()
        spec["draw"](bm, level)
        bm.transform(HALF_TURN)
        if fence_levels:
            src = fence_levels[i]
            offset = slots.index(src.data.materials[0]) if src.data.materials else 0
            for mesh in cut_ends(src, spec["cut"]):
                add_mesh(bm, mesh, offset)
        bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
        objects.append(to_object(name if i == 0 else f"{name} LOD{i}", bm, coll, slots))
    leaf = None
    hinge = None
    if spec.get("leaf"):
        width, height = spec["leaf_size"]
        bm = bmesh.new()
        spec["leaf"](bm, width, height)
        bm.transform(HALF_TURN)
        hinge = Vector((-width / 2, 0.0, 0.0))  # design x -width/2: Blender x, which Unity mirrors to +width/2
        bm.transform(Matrix.Translation(-hinge))
        bmesh.ops.triangulate(bm, faces=list(bm.faces), quad_method='BEAUTY', ngon_method='BEAUTY')
        leaf = to_object("Gate Leaf", bm, coll, slots)
        leaf.location = hinge
        objects.append(leaf)
    for obj in fence_levels or []:
        bpy.data.objects.remove(obj)

    folder = os.path.join(STRUCTURES, name)
    os.makedirs(folder, exist_ok=True)
    fbx = os.path.join(folder, f"{name}.fbx")
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z',
                             axis_up='Y', bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
    frame = objects[0]
    lo = Vector(min(v.co[a] for v in frame.data.vertices) for a in range(3))
    hi = Vector(max(v.co[a] for v in frame.data.vertices) for a in range(3))
    entry = dict(
        fbx=f"Assets/_Project/Art/Haven/Structures/{name}/{name}.fbx",
        objects=[obj.name for obj in objects],
        size_m=dict(x=round(hi.x - lo.x, 3), y=round(hi.z - lo.z, 3), z=round(hi.y - lo.y, 3)),
        piece_length=spec["length"],
        triangles=[counts(obj)[0] for obj in objects],
        vertices=[counts(obj)[1] for obj in objects],
        materials=[m.name for m in frame.data.materials],
        hinge_unity=[round(-hinge.x, 3), 0.0, 0.0] if hinge is not None else None,
        leaf_size=list(spec.get("leaf_size", ())),
    )
    print("BUILT", name, entry)
    # Out of the scene again, so the next gate's leaf is "Gate Leaf" too (Blender names must be unique).
    for obj in objects:
        mesh = obj.data
        bpy.data.objects.remove(obj)
        bpy.data.meshes.remove(mesh)
    return entry


def main():
    only = None
    if "--only" in args:
        only = [s.strip() for s in args[args.index("--only") + 1].split(",")]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    made = materials()
    coll = collection("Haven Gates")
    manifest = {}
    if os.path.exists(MANIFEST):
        with open(MANIFEST, encoding="utf-8") as f:
            manifest = json.load(f).get("assets", {})
    for spec in GATES:
        if only is None or spec["name"] in only:
            manifest[spec["name"]] = build_gate(spec, coll, made)
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(dict(note="Made by Tools/Blender/haven_gates.py.", assets=manifest), f, indent=2)
    print("HAVEN GATES DONE", list(manifest))


if __name__ == "__main__":
    main()
