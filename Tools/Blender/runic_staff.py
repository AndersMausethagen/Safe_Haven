"""The runic staff: the weapon of Runecraft (GDD 10.4, magic and control), modelled low-poly in Blender for Safe Haven.

Made from the concept beside this script (runic_staff_concept.png, LocalSpawn "3D source (flat lit)", seed 47): a straight,
slightly knotted shaft of dark ash, a leather-wrapped grip just above the middle, two iron bands under the head, an iron-shod
foot, and a forked crook of root wood clasping a grey standing stone cut with runes that glow amber; a bone charm hangs from
the crook on a cord. Flat colours, no textures, like the rest of the game's gear (the Kevin Iglesias weapons it was tinted
from): about 500 triangles, so it holds up in the action camera and costs nothing at the top-down one.

The pivot is the middle of the grip and the staff runs up its local +Y in Unity (Blender +Z), head up, as the game's polearm
models do. Writes Assets/_Project/Art/Items/Gear/Models/Source/RunicStaff.fbx, a preview render to the path given, and the
.blend beside this script.

Run: blender -b --factory-startup --python Tools/Blender/runic_staff.py -- <project root> <preview.png>
"""
import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ROOT = args[0] if args else os.getcwd()
PREVIEW = args[1] if len(args) > 1 else os.path.join(ROOT, "Tools", "Blender", "runic_staff_preview.png")
OUT = os.path.join(ROOT, "Assets", "_Project", "Art", "Items", "Gear", "Models", "Source")

FOOT = -1.30      # the iron-shod end, metres below the grip
FORK = 0.11       # where the shaft splits into the crook, just above the grip
STONE_AT = Vector((0.075, 0.0, 0.30))
STONE_SIZE = Vector((0.20, 0.10, 0.27))

random.seed(47)


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, colour, metallic=0.0, roughness=0.8, emission=None, strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    return mat


def tube(name, points, radii, sides, mat, cap_start=True, cap_end=True, twist=0.0):
    """A low-poly tube through the points (a list of Vectors), one ring of `sides` per point."""
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    rings = []
    for i, point in enumerate(points):
        ahead = points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]
        ahead.normalize()
        side = ahead.cross(Vector((0, 1, 0)))
        if side.length < 1e-4:
            side = ahead.cross(Vector((1, 0, 0)))
        side.normalize()
        up = side.cross(ahead)
        ring = []
        for s in range(sides):
            a = 2 * math.pi * s / sides + twist * i
            ring.append(bm.verts.new(point + (side * math.cos(a) + up * math.sin(a)) * radii[i]))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for s in range(sides):
            bm.faces.new((a[s], a[(s + 1) % sides], b[(s + 1) % sides], b[s]))
    if cap_start:
        bm.faces.new(list(reversed(rings[0])))
    if cap_end:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj.data.materials.append(mat)
    return obj


def shaft(mat):
    # Slightly knotted and bent, thicker toward the head.
    points, radii = [], []
    steps = 9
    for i in range(steps + 1):
        t = i / steps
        z = FOOT + (FORK - FOOT) * t
        x = 0.008 * math.sin(t * 5.1) + random.uniform(-0.002, 0.002)
        points.append(Vector((x, random.uniform(-0.002, 0.002), z)))
        radii.append((0.024 + 0.009 * t) * random.uniform(0.94, 1.08))
    return tube("Shaft", points, radii, 7, mat)


def crook(mat):
    # The fork: a long branch up the stone's side, a short one under it.
    long_branch = tube("Crook Long", [Vector((0.0, 0, FORK - 0.03)), Vector((-0.06, 0, FORK + 0.13)), Vector((-0.06, 0.005, FORK + 0.30)),
                                      Vector((-0.035, 0.0, FORK + 0.38))], [0.033, 0.028, 0.021, 0.015], 6, mat, cap_start=False)
    short_branch = tube("Crook Short", [Vector((0.008, 0, FORK - 0.01)), Vector((0.09, 0, FORK + 0.06)), Vector((0.16, 0, FORK + 0.08))],
                        [0.026, 0.018, 0.011], 6, mat, cap_start=False)
    return [long_branch, short_branch]


def stone(mat):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=STONE_AT)
    obj = bpy.context.active_object
    obj.name = "Stone"
    obj.scale = STONE_SIZE
    bpy.ops.object.transform_apply(scale=True)
    bevel = obj.modifiers.new("Bevel", 'BEVEL')
    bevel.width = 0.022
    bevel.segments = 1
    bpy.ops.object.modifier_apply(modifier="Bevel")
    for v in obj.data.vertices:
        v.co += Vector((random.uniform(-0.006, 0.006), random.uniform(-0.003, 0.003), random.uniform(-0.006, 0.006)))
    obj.rotation_euler = (0.0, math.radians(-9), 0.0)
    bpy.ops.object.transform_apply(rotation=True)
    obj.data.materials.append(mat)
    return obj


# Three runes a side, each a few strokes in the stone's local face plane (x across, z up), centred on the stone.
def _x(cx, cz, h=0.017):
    return [((cx - h, cz + h), (cx + h, cz - h)), ((cx - h, cz - h), (cx + h, cz + h))]


def _k(cx, cz, h=0.017):
    return [((cx + h, cz + h), (cx - h, cz)), ((cx - h, cz), (cx + h, cz - h))]


# Two columns of three, as in the concept: gebo and kaun, cut deep.
RUNES = [_k(-0.035, 0.065), _x(-0.035, 0.0), _k(-0.035, -0.065), _x(0.035, 0.065), _k(0.035, 0.0), _x(0.035, -0.065)]


def runes(mat):
    mesh = bpy.data.meshes.new("Runes")
    obj = bpy.data.objects.new("Runes", mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    width = 0.0065
    rot = -math.radians(9)
    cos, sin = math.cos(rot), math.sin(rot)
    for face_y, flip in ((-STONE_SIZE.y / 2 - 0.0025, 1), (STONE_SIZE.y / 2 + 0.0025, -1)):
        for rune in RUNES:
            for (ax, az), (bx, bz) in rune:
                a, b = Vector((ax * flip, 0, az)), Vector((bx * flip, 0, bz))
                along = (b - a).normalized()
                across = Vector((-along.z, 0, along.x)) * width
                corners = [a - across, b - across, b + across, a + across]
                verts = []
                for c in corners:
                    # Turned with the stone, then set on its face.
                    x, z = c.x * cos + c.z * sin, -c.x * sin + c.z * cos
                    verts.append(bm.verts.new(STONE_AT + Vector((x, face_y, z))))
                face = bm.faces.new(verts if flip > 0 else list(reversed(verts)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    # Faces point out of the stone on both sides.
    for poly in mesh.polygons:
        centre = poly.center
        outward = Vector((0, 1 if centre.y > STONE_AT.y else -1, 0))
        if poly.normal.dot(outward) < 0:
            poly.flip()
    obj.data.materials.append(mat)
    return obj


def bands(iron, leather):
    parts = []
    # Iron bands either side of the grip, right under the crook.
    for i, z in enumerate((-0.105, 0.095)):
        parts.append(tube(f"Band {i}", [Vector((0.003, 0, z - 0.012)), Vector((0.003, 0, z + 0.012))], [0.038, 0.038], 8, iron))
    parts.append(tube("Foot", [Vector((0.0, 0, FOOT - 0.01)), Vector((0.0, 0, FOOT + 0.08))], [0.026, 0.029], 8, iron))
    # The grip: leather wound round, a little proud of the wood, in ridges.
    grip = [Vector((0.003, 0, z)) for z in (-0.09, -0.055, -0.02, 0.015, 0.05, 0.085)]
    parts.append(tube("Grip", grip, [0.034, 0.037, 0.034, 0.037, 0.034, 0.036], 7, leather))
    return parts


def charm(cord, iron, bone):
    top = Vector((-0.062, 0.0, FORK + 0.33))
    low = Vector((-0.11, -0.004, FORK + 0.19))
    parts = [tube("Cord", [top, low], [0.0035, 0.0035], 4, cord)]
    # A ring, five segments round, three about.
    mesh = bpy.data.meshes.new("Ring")
    obj = bpy.data.objects.new("Ring", mesh)
    bpy.context.collection.objects.link(obj)
    bm = bmesh.new()
    centre = low + Vector((0, 0, -0.015))
    major, minor, around, about = 0.021, 0.0055, 6, 3
    grid = []
    for i in range(around):
        u = 2 * math.pi * i / around
        rim = centre + Vector((math.cos(u) * major, 0, math.sin(u) * major))
        out = Vector((math.cos(u), 0, math.sin(u)))
        grid.append([bm.verts.new(rim + (out * math.cos(2 * math.pi * j / about) + Vector((0, 1, 0)) * math.sin(2 * math.pi * j / about)) * minor)
                     for j in range(about)])
    for i in range(around):
        for j in range(about):
            a, b = grid[i], grid[(i + 1) % around]
            bm.faces.new((a[j], b[j], b[(j + 1) % about], a[(j + 1) % about]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj.data.materials.append(iron)
    parts.append(obj)
    # The bone: a shaft with knobbed ends.
    b_top = centre + Vector((0, 0, -0.024))
    b_low = b_top + Vector((0.005, 0, -0.085))
    parts.append(tube("Bone", [b_top, b_top + Vector((0.001, 0, -0.016)), b_low + Vector((-0.001, 0, 0.016)), b_low],
                      [0.017, 0.0085, 0.0085, 0.017], 5, bone))
    return parts


def join(objects, name):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = name
    joined.data.name = name
    # Flat shading reads as carved and keeps the vertex count low; Unity imports it as is.
    for poly in joined.data.polygons:
        poly.use_smooth = False
    return joined


def export(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True, object_types={'MESH'},
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)


def preview(obj, path):
    scene = bpy.context.scene
    world = bpy.data.worlds.new("World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.72, 0.74, 0.76, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.35
    scene.view_settings.view_transform = 'Standard'
    scene.world = world
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("Camera", bpy.data.cameras.new("Camera"))
    cam.data.lens = 50
    scene.collection.objects.link(cam)
    target = Vector((0.0, 0.0, -0.45))
    cam.location = target + Vector((0.9, -3.0, 0.5))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 48
    scene.render.resolution_x, scene.render.resolution_y = 640, 1100
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def main():
    clear()
    wood = material("RunicStaff Wood", (0.22, 0.12, 0.065), roughness=0.85)
    leather = material("RunicStaff Leather", (0.33, 0.17, 0.08), roughness=0.7)
    iron = material("RunicStaff Iron", (0.33, 0.34, 0.36), metallic=0.7, roughness=0.45)
    rock = material("RunicStaff Stone", (0.29, 0.32, 0.28), roughness=0.9)
    glow = material("RunicStaff Runes", (1.0, 0.62, 0.22), roughness=0.5, emission=(1.0, 0.55, 0.15), strength=1.2)
    bone = material("RunicStaff Bone", (0.86, 0.81, 0.68), roughness=0.6)
    cord = material("RunicStaff Cord", (0.42, 0.24, 0.12), roughness=0.8)
    parts = [shaft(wood)] + crook(wood) + [stone(rock), runes(glow)] + bands(iron, leather) + charm(cord, iron, bone)
    staff = join(parts, "RunicStaff")
    tris = sum(len(p.vertices) - 2 for p in staff.data.polygons)
    print(f"RUNIC STAFF: {tris} triangles, {len(staff.data.vertices)} vertices, {len(staff.data.materials)} materials")
    export(staff, os.path.join(OUT, "RunicStaff.fbx"))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "Tools", "Blender", "runic_staff.blend"))
    preview(staff, PREVIEW)


main()
