"""The giant black widow's body (2026-11-14), modelled by script: the image model would not leave the legs out, and the legs
are made and walked procedurally in Unity (SpiderLegs), so only the body is made here.

A glossy black abdomen, egg-shaped, with the red hourglass on its underside and a row of small red spots on top; a narrow
waist; the cephalothorax with eight dark-red eyes in two rows, two thick chelicerae with curved black fangs, and the
spinnerets at the back. Three materials (Chitin, Hourglass, Eyes) that Unity maps to its own. Faces +Y in Blender (Unity's
+Z), the origin at the waist; SpiderLegs lifts the body to its height. About 2.4 m long.

Run from the project root:
  "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup --python Tools/Blender/black_widow.py -- .
Writes Assets/_Project/Art/Enemies/Downloaded/BlackWidow/BlackWidow.fbx and Tools/Blender/black_widow_preview.png.
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else ["."]
ROOT = os.path.abspath(ARGS[0])
OUT = os.path.join(ROOT, "Assets/_Project/Art/Enemies/Downloaded/BlackWidow")


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def material(name, colour, roughness, emission=None):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*colour, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Coat Weight"].default_value = 0.6 if name == "Chitin" else 0.0
    if emission:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = 0.6
    return m


def ellipsoid(name, centre, radii, segments=48, rings=28, egg=0.0):
    """A UV sphere scaled to radii; egg > 0 narrows the +Y end (the abdomen's front)."""
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=1.0, location=(0, 0, 0))
    obj = bpy.context.object
    obj.name = name
    for v in obj.data.vertices:
        y = v.co.y
        squeeze = 1.0 - egg * max(0.0, y)
        v.co = Vector((v.co.x * radii[0] * squeeze, y * radii[1], v.co.z * radii[2] * squeeze))
    obj.location = centre
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    return obj


def cone(name, start, end, r0, r1, sides=12):
    """A tapered tube from start to end (a fang, a chelicera, a spinneret)."""
    start, end = Vector(start), Vector(end)
    depth = (end - start).length
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r0, radius2=r1, depth=depth, location=(start + end) / 2)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference((end - start).normalized())
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=False)
    return obj


def build():
    chitin = material("Chitin", (0.012, 0.012, 0.014), 0.22)
    red = material("Hourglass", (0.75, 0.02, 0.02), 0.32)
    eyes = material("Eyes", (0.05, 0.0, 0.0), 0.05, emission=(0.25, 0.0, 0.0))

    parts = []
    abdomen = ellipsoid("Abdomen", (0, -0.86, 0.06), (0.66, 0.86, 0.6), segments=96, rings=56, egg=0.18)
    parts.append(abdomen)
    head = ellipsoid("Cephalothorax", (0, 0.38, -0.04), (0.4, 0.5, 0.27), segments=40, rings=22)
    parts.append(head)
    parts.append(cone("Waist", (0, -0.05, -0.02), (0, 0.06, -0.03), 0.16, 0.13))
    # Chelicerae and their fangs, curving down and back.
    for side in (-1, 1):
        parts.append(cone(f"Chelicera {side}", (side * 0.11, 0.78, -0.08), (side * 0.12, 0.93, -0.22), 0.085, 0.06))
        parts.append(cone(f"Fang {side}", (side * 0.12, 0.93, -0.22), (side * 0.08, 0.88, -0.36), 0.035, 0.004, sides=8))
        # Pedipalps: short feelers beside the jaws.
        parts.append(cone(f"Pedipalp {side}", (side * 0.2, 0.74, -0.1), (side * 0.32, 0.98, -0.2), 0.045, 0.03))
    parts.append(cone("Spinnerets", (0, -1.66, 0.02), (0, -1.78, -0.04), 0.09, 0.03))
    eye_objects = []
    for row, (y, z, spread, size) in enumerate(((0.86, 0.09, 0.06, 0.032), (0.8, 0.16, 0.1, 0.026))):
        for side in (-1, 1):
            for k in (0, 1):
                x = side * (spread * 0.5 + k * spread)
                bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=size - k * 0.006, location=(x, y - k * 0.03, z))
                e = bpy.context.object
                e.name = f"Eye {row}{side}{k}"
                eye_objects.append(e)

    # One mesh, its materials by part.
    for obj in parts + eye_objects:
        obj.data.materials.clear()
        obj.data.materials.append(eyes if obj in eye_objects else chitin)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts + eye_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = abdomen
    bpy.ops.object.join()
    body = bpy.context.object
    body.name = "BlackWidow"
    body.data.name = "BlackWidow"
    if red.name not in [m.name for m in body.data.materials]:
        body.data.materials.append(red)
    red_index = [m.name for m in body.data.materials].index("Hourglass")

    # The hourglass under the abdomen and a smaller one on its back: faces of the abdomen by where they face and lie.
    centre, radii = Vector((0, -0.86, 0.06)), Vector((0.66, 0.86, 0.6))
    bm = bmesh.new()
    bm.from_mesh(body.data)
    for f in bm.faces:
        c = f.calc_center_median()
        local = Vector(((c.x - centre.x) / radii.x, (c.y - centre.y) / radii.y, (c.z - centre.z) / radii.z))
        if local.length > 1.15 or c.y > 0.0:
            continue
        n = f.normal
        if n.z < -0.55:
            # Two triangles tip to tip along the belly.
            yy = abs(local.y + 0.05)
            if yy < 0.55 and abs(local.x) < 0.04 + 0.42 * yy:
                f.material_index = red_index
        elif n.z > 0.55:
            # A smaller one on its back too: the game looks down from above, and the belly is never seen there.
            yy = abs(local.y + 0.1)
            if yy < 0.42 and abs(local.x) < 0.03 + 0.3 * yy:
                f.material_index = red_index
    # A little unevenness: no chitin is a perfect egg.
    for v in bm.verts:
        v.co += v.normal * (math.sin(v.co.x * 23) * math.sin(v.co.y * 19) * math.sin(v.co.z * 29)) * 0.006
    bm.to_mesh(body.data)
    bm.free()
    for poly in body.data.polygons:
        poly.use_smooth = True
    return body


def export(body):
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    body.select_set(True)
    bpy.context.view_layer.objects.active = body
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "BlackWidow.fbx"), use_selection=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=False, object_types={'MESH'},
                             mesh_smooth_type='FACE', use_mesh_modifiers=True, path_mode='STRIP', embed_textures=False)
    print(f"[black widow] {len(body.data.polygons)} faces, {len(body.data.materials)} materials, "
          f"size {tuple(round(d, 2) for d in body.dimensions)}")


def preview(body):
    scene = bpy.context.scene
    body.location.z = 0.95
    bpy.ops.mesh.primitive_plane_add(size=8, location=(0, 0, 0))
    bpy.ops.object.camera_add(location=(3.4, 3.0, 2.4))
    cam = bpy.context.object
    cam.rotation_euler = (Vector((0, 0, 0.8)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.camera = cam
    bpy.ops.object.light_add(type='SUN', rotation=(math.radians(45), math.radians(15), math.radians(40)))
    bpy.context.object.data.energy = 3.5
    bpy.ops.object.light_add(type='AREA', location=(-2, -2, 3))
    bpy.context.object.data.energy = 300
    scene.world = bpy.data.worlds.new("Grey")
    scene.world.color = (0.3, 0.32, 0.31)
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x, scene.render.resolution_y = 900, 700
    scene.render.filepath = os.path.join(ROOT, "Tools/Blender/black_widow_preview.png")
    bpy.ops.render.render(write_still=True)
    # Underside, for the hourglass.
    cam.location = (0.4, -0.6, -1.6)
    cam.rotation_euler = (Vector((0, -0.7, 0.95)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.data.objects["Plane"].hide_render = True
    scene.render.filepath = os.path.join(ROOT, "Tools/Blender/black_widow_preview_under.png")
    bpy.ops.render.render(write_still=True)


clear()
spider = build()
export(spider)
preview(spider)
