"""The game's own prefabs in Blender: OBJ files and kit.json written by AgentScripts/ExportBiomeKit.cs (each prefab's most
detailed LOD, its materials' textures, colour, alpha cut-off and tiling). load() imports every prefab once, joins its parts into
one object named after the prefab, rebuilds its materials from the game's textures (base colour x tint, normal map, alpha cut
for leaves and grass, two-sided), and keeps the originals hidden for linked copies (place()).
"""
import json
import os

import bpy

_KIT = {}


def _image(path, colour):
    if not path or not os.path.isfile(path):
        return None
    img = bpy.data.images.load(path, check_existing=True)
    if not colour:
        img.colorspace_settings.name = "Non-Color"
    return img


def material(name, spec):
    """A Blender material from one kit.json entry."""
    existing = bpy.data.materials.get("Kit " + name)
    if existing:
        return existing
    mat = bpy.data.materials.new("Kit " + name)
    mat.use_nodes = True
    nt = mat.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    p = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(p.outputs["BSDF"], out.inputs["Surface"])
    shader = spec.get("shader", "")
    # The game's grass shader always cuts at its alpha (no keyword for the exporter to see).
    cutoff = spec.get("cutoff", 0) or (0.4 if "Grass" in shader else 0)
    foliage = cutoff > 0
    p.inputs["Roughness"].default_value = 0.65 if foliage else 0.85
    tc = nt.nodes.new("ShaderNodeTexCoord")
    mp = nt.nodes.new("ShaderNodeMapping")
    tiling = spec.get("tiling", [1, 1, 0, 0])
    mp.inputs["Scale"].default_value = (tiling[0], tiling[1], 1)
    mp.inputs["Location"].default_value = (tiling[2], tiling[3], 0)
    nt.links.new(tc.outputs["UV"], mp.inputs["Vector"])
    base = _image(spec.get("base"), True)
    tint = spec.get("color", [1, 1, 1, 1])
    if base:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = base
        nt.links.new(mp.outputs["Vector"], tex.inputs["Vector"])
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        nt.links.new(tex.outputs["Color"], mul.inputs["A"])
        mul.inputs["B"].default_value = (tint[0], tint[1], tint[2], 1)
        nt.links.new(mul.outputs["Result"], p.inputs["Base Color"])
        if cutoff > 0:
            cut = nt.nodes.new("ShaderNodeMath")
            cut.operation = "GREATER_THAN"
            cut.inputs[1].default_value = cutoff
            nt.links.new(tex.outputs["Alpha"], cut.inputs[0])
            nt.links.new(cut.outputs[0], p.inputs["Alpha"])
    else:
        p.inputs["Base Color"].default_value = (tint[0], tint[1], tint[2], 1)
    normal = _image(spec.get("normal"), False)
    if normal:
        ntex = nt.nodes.new("ShaderNodeTexImage")
        ntex.image = normal
        nt.links.new(mp.outputs["Vector"], ntex.inputs["Vector"])
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], p.inputs["Normal"])
    if foliage:
        # Leaves and blades let a little light through, as the game's do.
        if "Subsurface Weight" in p.inputs:
            p.inputs["Subsurface Weight"].default_value = 0.06
        mat.use_backface_culling = False
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "DITHERED"
        if hasattr(mat, "blend_method"):
            try:
                mat.blend_method = "CLIP"
            except TypeError:
                pass
    return mat


def load(kit_dir, collection):
    """Imports every prefab of the kit into <collection> (hidden from render); returns {name: object}."""
    data = json.load(open(os.path.join(kit_dir, "kit.json"), encoding="utf-8"))
    specs = data["materials"]
    for entry in data["prefabs"]:
        before = set(bpy.data.objects)
        bpy.ops.wm.obj_import(filepath=os.path.join(kit_dir, entry["obj"]), forward_axis="NEGATIVE_Z", up_axis="Y")
        parts = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        if not parts:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        for o in parts:
            o.select_set(True)
        bpy.context.view_layer.objects.active = parts[0]
        if len(parts) > 1:
            bpy.ops.object.join()
        obj = bpy.context.active_object
        obj.name = entry["name"]
        # The importer turns Y-up to Z-up on the object, not the mesh: bake it in, so linked copies stand up.
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        for c in list(obj.users_collection):
            c.objects.unlink(obj)
        collection.objects.link(obj)
        for slot in obj.material_slots:
            if slot.material is None:
                continue
            key = slot.material.name.split(".")[0]
            spec = specs.get(key)
            if spec:
                slot.material = material(key, spec)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        _KIT[entry["name"]] = obj
    return dict(_KIT)


def place(name, location, rotation_z=0.0, scale=1.0, collection=None, tilt=(0.0, 0.0)):
    """A linked copy of a kit prefab, standing at <location>."""
    source = _KIT[name]
    obj = bpy.data.objects.new(name, source.data)
    (collection or bpy.context.scene.collection).objects.link(obj)
    obj.location = location
    obj.rotation_euler = (tilt[0], tilt[1], rotation_z)
    s = scale if isinstance(scale, (tuple, list)) else (scale, scale, scale)
    obj.scale = s
    return obj


def names():
    return list(_KIT)
