"""blender -b --factory-startup --python preview_models.py -- <gltf> <out.png>: one image with the model seen from
+Y, -Y, +X, -X and above (Blender axes, Z up), to find which side of a scanned rock face is finished."""
import math
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
corners = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
hi = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
center = (lo + hi) / 2
radius = (hi - lo).length

scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "TEXTURE"
scene.render.resolution_x = 360
scene.render.resolution_y = 360
cam_data = bpy.data.cameras.new("cam")
cam = bpy.data.objects.new("cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

views = {"+Y": Vector((0, 1, 0.25)), "-Y": Vector((0, -1, 0.25)), "+X": Vector((1, 0, 0.25)),
         "-X": Vector((-1, 0, 0.25)), "top": Vector((0.01, 0.01, 1))}
paths = []
for name, direction in views.items():
    cam.location = center + direction.normalized() * radius * 1.4
    cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
    path = out.replace(".png", f"_{name.replace('+', 'p').replace('-', 'm')}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    paths.append(path)
print("PREVIEWS", paths)
