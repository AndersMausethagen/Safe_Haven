"""Turns a Poly Haven conifer (millions of needle cards) into game trees with branch cards:

    blender -b --factory-startup --python bake_conifer.py -- <tree.gltf> <out dir> '<json options>'

options: {"prefix": "Norway Spruce", "cells": 30 (voxels up the height for LOD0), "tiles": 8, "tile": 1024,
          "trunk": 3000 (bark triangles at LOD0), "seed": 3, "moss": 0.0 (bark moss layer amount),
          "tint": [1, 1, 1] (needle colour tint), "slice": 0.3 (atlas depth slice, thinner shows more gaps)}

For each tree in the file (Poly Haven ships a, b and c):
- the needle cards are grouped into voxels; each group becomes one card, laid in the group's own plane (the two widest
  directions of its needles, by principal components), sized to it and facing outwards; coarser voxels give the fewer,
  bigger cards of LOD1 and LOD2;
- the cards show a needle atlas rendered from the tree itself: eight typical groups seen straight on (Eevee, unlit
  colour with the needles' alpha), faded to a ragged round edge, darkened where the needles are dense (self-shadow),
  with a normal map raised from the needles themselves, so the cards look like the real branches;
- the bark (trunk and branches) is kept as a decimated mesh with its own UVs;
- wind data for Safe Haven/Tree Wind goes in the second and third UV sets (sway 0, branch flex growing outwards, a
  phase per card, a little flutter), so branches move and the trunk stands still.
Writes <prefix> A/B/C.fbx (LOD0-2), the atlas (<prefix> Needles_Color.png with alpha, _Normal.png), the bark maps and a
material sidecar per FBX (Layered Lit for the bark, moss layer off unless "moss" is set, Tree Wind with alpha clip for the needles).
"""
import json
import math
import os
import shutil
import sys

import bpy
import numpy as np
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT, OPTS = argv[0], argv[1], json.loads(argv[2]) if len(argv) > 2 else {}
PREFIX = OPTS.get("prefix", "Spruce")
CELLS = OPTS.get("cells", 30)
TILES = OPTS.get("tiles", 8)
TILE = OPTS.get("tile", 1024)  # tile width; tiles are 2:1, so 8 tiles make a 2048 atlas
TRUNK_TRIS = OPTS.get("trunk", 3000)
MOSS = OPTS.get("moss", 0.0)    # Layer 1 (moss) amount on the bark
TINT = OPTS.get("tint", [1.0, 1.0, 1.0])
SLICE = OPTS.get("slice", 0.3)  # half depth of the needles each atlas tile sees, times the cell: thinner is sparser
RNG = np.random.default_rng(OPTS.get("seed", 3))
ASSET = os.path.basename(SRC).split("_1k")[0]
MAPS = os.path.join(os.path.dirname(SRC), "maps")
TEXTURES = os.path.join(os.path.dirname(SRC), "textures")
LOD_CELL = (1.0, 1.8, 3.2)      # voxel size per LOD, times the LOD0 size
LOD_TRUNK = (1.0, 0.4, 0.15)    # bark triangles per LOD, times the LOD0 budget
TRUNK_SHARE, TRUNK_MIN = 0.2, 150  # the trunk's own share of the bark budget, and its floor
LOD_TWIGS = (0.5, 1.5, 4.0)     # loose bark pieces smaller than this (metres) are dropped: hidden in the needles
os.makedirs(OUT, exist_ok=True)


def log(*parts):
    print("CONIFER", *parts, flush=True)


# ---------------------------------------------------------------------------------------------- reading the tree

def numpy_of(collection, attribute, count, width, dtype=np.float32):
    data = np.empty(count * width, dtype=dtype)
    collection.foreach_get(attribute, data)
    return data.reshape(count, width) if width > 1 else data


def split_tree(obj):
    """(bark mesh data, needle polygon centres and areas, tree height) of one imported tree, moved so its trunk
    stands at the origin."""
    me = obj.data
    me.transform(obj.matrix_world)
    obj.matrix_world = Matrix.Identity(4)
    names = [m.name if m else "" for m in me.materials]
    twig = [i for i, n in enumerate(names) if "twig" in n]
    polygons = len(me.polygons)
    material = numpy_of(me.polygons, "material_index", polygons, 1, np.int32)
    is_twig = np.isin(material, twig)
    co = numpy_of(me.vertices, "co", len(me.vertices), 3)

    # Bark: the polygons that are not needles, rebuilt as a small mesh (with their UVs and materials).
    start = numpy_of(me.polygons, "loop_start", polygons, 1, np.int32)[~is_twig]
    total = numpy_of(me.polygons, "loop_total", polygons, 1, np.int32)[~is_twig]
    loop_vertex = numpy_of(me.loops, "vertex_index", len(me.loops), 1, np.int32)
    uv = numpy_of(me.uv_layers[0].data, "uv", len(me.loops), 2)
    loops = np.repeat(start - np.concatenate(([0], np.cumsum(total)[:-1])), total) + np.arange(total.sum())
    used, remap = np.unique(loop_vertex[loops], return_inverse=True)
    kept = [i for i in range(len(names)) if i not in twig]  # the bark's own materials, renumbered
    renumber = np.full(len(names), -1, dtype=np.int32)
    renumber[kept] = np.arange(len(kept), dtype=np.int32)
    bark = {"co": co[used], "faces": np.split(remap, np.cumsum(total)[:-1]), "uv": uv[loops],
            "material": renumber[material[~is_twig]], "names": [names[i] for i in kept]}

    centres = numpy_of(me.polygons, "center", polygons, 3)[is_twig]
    areas = numpy_of(me.polygons, "area", polygons, 1)[is_twig]

    # Stand the trunk on the origin: the middle of the bark near the ground.
    base_z = bark["co"][:, 2].min()
    foot = bark["co"][bark["co"][:, 2] < base_z + 1.5]
    origin = np.array([foot[:, 0].mean(), foot[:, 1].mean(), base_z], dtype=np.float32)
    bark["co"] = bark["co"] - origin
    centres = centres - origin
    height = float(max(bark["co"][:, 2].max(), centres[:, 2].max()))
    return bark, centres, areas, height, origin


# ---------------------------------------------------------------------------------------------- clustering

def cluster(centres, areas, cell):
    """Groups needle polygons into voxels: per group its centre, axes (u, v widest; n the plane's normal, facing out
    and up) and half size."""
    keys = np.floor(centres / cell).astype(np.int64) + 4096
    key = (keys[:, 0] * 8192 + keys[:, 1]) * 8192 + keys[:, 2]
    _, group = np.unique(key, return_inverse=True)
    weight = np.bincount(group, weights=areas)
    keep = weight > np.median(weight) * 0.15
    mean = np.stack([np.bincount(group, weights=areas * centres[:, k]) for k in range(3)], axis=1) / weight[:, None]
    cov = np.empty((len(weight), 3, 3))
    for a in range(3):
        for b in range(a, 3):
            m = np.bincount(group, weights=areas * centres[:, a] * centres[:, b]) / weight - mean[:, a] * mean[:, b]
            cov[:, a, b] = cov[:, b, a] = m
    values, vectors = np.linalg.eigh(cov[keep])  # ascending: n, v, u
    mean, weight = mean[keep], weight[keep]
    n = vectors[:, :, 0]
    v = vectors[:, :, 1]
    u = vectors[:, :, 2]
    radial = mean.copy()
    radial[:, 2] = 0.0
    radial /= np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), 1e-4)
    up = np.array([0.0, 0.0, 1.0])
    outward = radial + up * 0.6
    flip = np.sum(n * outward, axis=1) < 0
    n[flip] *= -1

    def along_branch(normal):
        """The outward direction laid in a plane: the branch runs from the trunk side (-u) to its tip (+u)."""
        axis = radial - normal * np.sum(radial * normal, axis=1, keepdims=True)
        bad = np.linalg.norm(axis, axis=1) < 0.2  # plane facing the trunk: fall back to its widest direction
        axis[bad] = u[bad]
        return axis / np.linalg.norm(axis, axis=1, keepdims=True)

    # The atlas sees each group straight on through its own plane (the clearest view of the spray); the card itself
    # leans halfway towards a drooping, outward-and-up plane so the layered branches still show from the ground.
    view_u = along_branch(n)
    card_n = n * 0.5 + (radial * 0.75 + up * 0.65) * 0.5
    card_n /= np.linalg.norm(card_n, axis=1, keepdims=True)
    card_u = along_branch(card_n)
    spread = np.sqrt(np.maximum(values[:, 1:], 1e-8)) * 2.1
    half = np.clip(spread[:, 1] * 1.1, cell * 0.6, cell * 1.3)  # half the branch's length; its width is half that
    return {"centre": mean, "u": card_u, "v": np.cross(card_n, card_u), "n": card_n, "half": half, "weight": weight,
            "view_u": view_u, "view_v": np.cross(n, view_u), "view_n": n}


# ---------------------------------------------------------------------------------------------- the needle atlas

def emission_material(name, colour_path, alpha_path, normals):
    """Unlit needles for the atlas: their colour, or (normals) their normal in camera space; alpha from the mask."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    emit = nodes.new("ShaderNodeEmission")
    clear = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    alpha = nodes.new("ShaderNodeTexImage")
    alpha.image = bpy.data.images.load(alpha_path, check_existing=True)
    alpha.image.colorspace_settings.name = "Non-Color"
    if normals:
        geometry = nodes.new("ShaderNodeNewGeometry")
        flip = nodes.new("ShaderNodeVectorMath")
        flip.operation = "SCALE"
        sign = nodes.new("ShaderNodeMath")
        sign.operation = "MULTIPLY_ADD"  # backfacing (0/1) * -2 + 1
        sign.inputs[1].default_value = -2.0
        sign.inputs[2].default_value = 1.0
        links.new(geometry.outputs["Backfacing"], sign.inputs[0])
        links.new(geometry.outputs["Normal"], flip.inputs[0])
        links.new(sign.outputs[0], flip.inputs["Scale"])
        to_camera = nodes.new("ShaderNodeVectorTransform")
        to_camera.vector_type = "NORMAL"
        to_camera.convert_from = "WORLD"
        to_camera.convert_to = "CAMERA"
        links.new(flip.outputs[0], to_camera.inputs[0])
        pack = nodes.new("ShaderNodeVectorMath")
        pack.operation = "MULTIPLY_ADD"
        pack.inputs[1].default_value = (0.5, 0.5, -0.5)  # Blender's camera looks down -Z: out of the card is +Z
        pack.inputs[2].default_value = (0.5, 0.5, 0.5)
        links.new(to_camera.outputs[0], pack.inputs[0])
        links.new(pack.outputs[0], emit.inputs["Color"])
    else:
        colour = nodes.new("ShaderNodeTexImage")
        colour.image = bpy.data.images.load(colour_path, check_existing=True)
        links.new(colour.outputs["Color"], emit.inputs["Color"])
    links.new(alpha.outputs["Color"], mix.inputs["Fac"])
    links.new(clear.outputs[0], mix.inputs[1])
    links.new(emit.outputs[0], mix.inputs[2])
    links.new(mix.outputs[0], out.inputs["Surface"])
    return mat


def render_atlas(obj, cards, cell):
    """Renders TILES typical needle groups of the tree straight on into a colour atlas (with alpha) and a normal atlas."""
    scene = bpy.context.scene
    engines = bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items.keys()
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in engines else "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 32
    scene.render.film_transparent = True
    scene.render.resolution_x = TILE
    scene.render.resolution_y = TILE // 2
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    names = [m.name if m else "" for m in obj.data.materials]
    colour = os.path.join(TEXTURES, f"{ASSET}_twig_diff_1k.jpg")
    alpha = os.path.join(MAPS, f"{ASSET}_twig_alpha_1k.png")
    looks = {"colour": emission_material("atlas colour", colour, alpha, False)}
    hidden = bpy.data.materials.new("atlas hidden")
    hidden.use_nodes = True
    hidden.node_tree.nodes.clear()
    hidden_out = hidden.node_tree.nodes.new("ShaderNodeOutputMaterial")
    hidden.node_tree.links.new(hidden.node_tree.nodes.new("ShaderNodeBsdfTransparent").outputs[0], hidden_out.inputs["Surface"])

    camera_data = bpy.data.cameras.new("atlas camera")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("atlas camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera

    big = np.argsort(cards["weight"])[-max(TILES * 4, 1):]
    picks = big[np.argsort(cards["centre"][big, 2])][np.linspace(0, len(big) - 1, TILES).astype(int)]
    columns, rows = 2, math.ceil(TILES / 2)
    h = TILE // 2
    atlas = {kind: np.zeros((rows * h, columns * TILE, 4), dtype=np.float32) for kind in looks}
    for t, i in enumerate(picks):
        c, u, v, n = (Vector(cards[k][i]) for k in ("centre", "view_u", "view_v", "view_n"))
        distance = cell * 4.0
        camera.matrix_world = Matrix((
            (u.x, v.x, n.x, (c + n * distance).x),
            (u.y, v.y, n.y, (c + n * distance).y),
            (u.z, v.z, n.z, (c + n * distance).z),
            (0, 0, 0, 1)))
        camera_data.ortho_scale = float(cards["half"][i]) * 2.0  # the wider (branch) side; the tile is 2:1
        # A thin slice: one layer of twigs, so the card shows gaps and a jagged outline instead of a solid clump.
        camera_data.clip_start = distance - cell * SLICE
        camera_data.clip_end = distance + cell * SLICE
        for kind, material in looks.items():
            for slot, name in enumerate(names):
                obj.material_slots[slot].material = material if "twig" in name else hidden
            scene.view_settings.view_transform = "Standard" if kind == "colour" else "Raw"
            path = os.path.join(OUT, f"_tile_{kind}_{t}.png")
            scene.render.filepath = path
            bpy.ops.render.render(write_still=True)
            image = bpy.data.images.load(path)
            pixels = np.array(image.pixels[:], dtype=np.float32).reshape(h, TILE, 4)
            bpy.data.images.remove(image)
            os.remove(path)
            row, col = t // columns, t % columns
            atlas[kind][row * h:(row + 1) * h, col * TILE:(col + 1) * TILE] = pixels
        log("tile", t, "of", TILES)

    scene.collection.objects.unlink(camera)
    colour_atlas = atlas["colour"]
    for t in range(TILES):
        row, col = t // columns, t % columns
        tile = colour_atlas[row * h:(row + 1) * h, col * TILE:(col + 1) * TILE]
        tile[..., 3] *= edge_mask(t)
        # Self-shadow: where the needles are dense the light gets in less.
        density = blur(tile[..., 3], TILE // 48)
        tile[..., :3] *= (1.0 - 0.3 * np.clip(density, 0.0, 1.0))[..., None]
    save_png(dilate(colour_atlas), os.path.join(OUT, f"{PREFIX} Needles_Color.png"))
    save_png(normal_from_alpha(colour_atlas[..., 3]), os.path.join(OUT, f"{PREFIX} Needles_Normal.png"))
    return columns, rows


def edge_mask(seed):
    """A branch spray's outline on a 2:1 tile (trunk side left, tip right): narrow at the stem, widest past the
    middle, tapering to the tip, with a few lobes for the side twigs. The edge is steep, so the needles themselves
    draw the silhouette rather than a soft round fade."""
    h = TILE // 2
    y, x = np.mgrid[0:h, 0:TILE].astype(np.float32)
    x /= TILE - 1
    y = (y / (h - 1) - 0.5) * 2.0
    rng = np.random.default_rng(seed + 11)
    width = np.clip(np.sin(np.pi * np.clip(x, 0.0, 1.0) ** 0.75), 0.0, 1.0) ** 0.7
    width *= np.clip(x / 0.25, 0.25, 1.0)  # a narrow stem at the trunk end
    width *= 1.0 + sum(rng.uniform(0.04, 0.09) * np.sin(x * k * np.pi + rng.uniform(0, 6.28)) for k in (3, 5, 8))
    inside = 1.0 - np.abs(y + rng.uniform(-0.08, 0.08) * x) / np.maximum(width * 0.95, 1e-3)
    return np.clip(inside / 0.12, 0.0, 1.0)


def blur(values, radius):
    """Box blur (three passes ~ gaussian) of a 2D array."""
    out = values.astype(np.float32)
    for _ in range(3):
        for axis in (0, 1):
            cumulative = np.cumsum(np.pad(out, [(radius + 1, radius) if a == axis else (0, 0) for a in range(2)], mode="edge"), axis=axis)
            upper = np.take(cumulative, np.arange(2 * radius + 1, cumulative.shape[axis]), axis=axis)
            lower = np.take(cumulative, np.arange(0, cumulative.shape[axis] - 2 * radius - 1), axis=axis)
            out = (upper - lower) / (2 * radius + 1)
    return out


def normal_from_alpha(alpha, strength=2.5):
    """A tangent-space normal map (OpenGL, +Z out of the card) raised from the needles: each needle a small ridge."""
    height = blur(alpha, 1)
    dy, dx = np.gradient(height)
    n = np.stack([-dx * strength, -dy * strength, np.ones_like(height)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    out = np.ones(alpha.shape + (4,), dtype=np.float32)
    out[..., :3] = n * 0.5 + 0.5
    return out


def dilate(image, passes=12):
    """Spreads the colour of the needles into the see-through pixels around them, so mipmaps do not darken the edges."""
    rgb, alpha = image[..., :3].copy(), image[..., 3]
    filled = alpha > 0.02
    for _ in range(passes):
        total = np.zeros_like(rgb)
        count = np.zeros(alpha.shape, dtype=np.float32)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            shifted = np.roll(np.roll(rgb, dy, 0), dx, 1)
            mask = np.roll(np.roll(filled, dy, 0), dx, 1)
            total += shifted * mask[..., None]
            count += mask
        grow = (~filled) & (count > 0)
        rgb[grow] = total[grow] / count[grow, None]
        filled = filled | grow
    out = image.copy()
    out[..., :3] = rgb
    return out


def save_png(pixels, path):
    height, width = pixels.shape[:2]
    image = bpy.data.images.new(os.path.basename(path), width, height, alpha=True)
    image.pixels.foreach_set(np.ascontiguousarray(pixels).ravel())
    image.filepath_raw = path
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


# ---------------------------------------------------------------------------------------------- building a LOD

def drop_small_parts(bm, smallest):
    """Deletes loose pieces whose size (bounding diagonal) is under <smallest> metres."""
    seen = set()
    doomed = []
    for start in bm.verts:
        if start.index in seen:
            continue
        part, stack = [], [start]
        seen.add(start.index)
        while stack:
            vertex = stack.pop()
            part.append(vertex)
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other.index not in seen:
                    seen.add(other.index)
                    stack.append(other)
        xs = [v.co.x for v in part]
        ys = [v.co.y for v in part]
        zs = [v.co.z for v in part]
        size = math.dist((min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs)))
        if size < smallest:
            doomed.extend(part)
    import bmesh
    bmesh.ops.delete(bm, geom=doomed, context="VERTS")


def bark_part(bark, keep, name, smallest):
    """The bark faces flagged in <keep> as one welded object, loose pieces under <smallest> metres dropped;
    None when nothing is left."""
    total = np.array([len(f) for f in bark["faces"]])
    me = bpy.data.meshes.new(name)
    me.from_pydata(bark["co"].tolist(), [], [f.tolist() for f, k in zip(bark["faces"], keep) if k])
    layer = me.uv_layers.new(name="UVMap")
    layer.data.foreach_set("uv", bark["uv"][np.repeat(keep, total)].ravel())
    for wind in ("Wind", "WindPhase"):
        me.uv_layers.new(name=wind)  # zero: the trunk stands still
    for material_name in bark["names"]:
        me.materials.append(bpy.data.materials.get(material_name))
    me.polygons.foreach_set("material_index", bark["material"][keep])
    me.update()
    # glTF splits vertices along every seam, which leaves the decimator nothing to collapse: weld them first
    # (the UVs are per corner, so they survive).
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    bm.verts.index_update()
    drop_small_parts(bm, smallest)
    bm.to_mesh(me)
    bm.free()
    if not me.polygons:
        bpy.data.meshes.remove(me)
        return None
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def decimate(obj, target):
    for _ in range(4):
        triangles = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        if triangles <= target * 1.05:
            break
        mod = obj.modifiers.new("Decimate", "DECIMATE")
        mod.ratio = target / triangles
        mod.use_collapse_triangulate = True
        bpy.context.view_layer.update()  # a new object: the depsgraph must know it before it can be evaluated
        evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = bpy.data.meshes.new_from_object(evaluated)
        obj.modifiers.clear()
        old, obj.data = obj.data, mesh
        bpy.data.meshes.remove(old)
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def bark_object(bark, ratio, name, smallest):
    """The bark of one LOD. The trunk is decimated on its own budget: in one pass with the branches its long,
    flat cylinder is the cheapest to collapse and vanished first (a floating crown at LOD0)."""
    target = TRUNK_TRIS * ratio
    is_trunk = np.array(["trunk" in n for n in bark["names"]] + [False])[bark["material"]]
    parts = []
    trunk = bark_part(bark, is_trunk, name + "_trunk", 0.0)
    if trunk:
        target -= decimate(trunk, max(TRUNK_MIN, target * TRUNK_SHARE))
        parts.append(trunk)
    branches = bark_part(bark, ~is_trunk, name + "_branches", smallest)
    if branches:
        decimate(branches, max(target, TRUNK_MIN))
        parts.append(branches)
    return join(parts, name) if len(parts) > 1 else parts[0]

def card_object(cards, layout, needles, height, name):
    columns, rows = layout
    count = len(cards["half"])
    tile = RNG.integers(0, TILES, count)
    corners = np.array([[-1, -1], [1, -1], [1, 1], [-1, 1]], dtype=np.float32)
    co = np.empty((count * 4, 3), dtype=np.float32)
    uv = np.empty((count * 4, 2), dtype=np.float32)
    wind = np.zeros((count * 4, 2), dtype=np.float32)
    phase = np.zeros((count * 4, 2), dtype=np.float32)
    normals = np.empty((count * 4, 3), dtype=np.float32)
    reach = max(float(np.linalg.norm(cards["centre"][:, :2], axis=1).max()), 0.5)
    half = cards["half"][:, None]
    for k, (su, sv) in enumerate(corners):
        index = np.arange(count) * 4 + k
        # A branch-shaped card: its length along u (out from the trunk), half as wide; its stem end sits a little
        # inside the group so neighbouring sprays overlap at the branch.
        co[index] = cards["centre"] + cards["u"] * (su + 0.25) * half + cards["v"] * sv * half * 0.5
        row, col = tile // columns, tile % columns
        uv[index, 0] = (col + (su + 1) * 0.5 * 0.996 + 0.002) / columns
        uv[index, 1] = (row + (sv + 1) * 0.5 * 0.99 + 0.005) / rows
        # Branch flex grows with the distance from the trunk; the inner edge of each card moves less.
        distance = np.linalg.norm(co[index, :2], axis=1)
        wind[index, 1] = np.clip(distance / reach, 0.05, 1.0)
        phase[index, 0] = (np.arange(count) * 0.618034) % 1.0
        phase[index, 1] = 0.3
        radial = co[index].copy()
        radial[:, 2] = 0.0
        radial /= np.maximum(np.linalg.norm(radial, axis=1, keepdims=True), 1e-4)
        # Rounded lighting: the cards' normals lean out from the trunk and up, like a crown seen as one volume.
        bent = cards["n"] * 0.35 + radial * 0.65 + np.array([0.0, 0.0, 0.25])
        normals[index] = bent / np.linalg.norm(bent, axis=1, keepdims=True)
    me = bpy.data.meshes.new(name)
    me.from_pydata(co.tolist(), [], np.arange(count * 4).reshape(count, 4).tolist())
    for layer_name, data in (("UVMap", uv), ("Wind", wind), ("WindPhase", phase)):
        layer = me.uv_layers.new(name=layer_name)
        layer.data.foreach_set("uv", data.ravel())
    me.materials.append(needles)
    me.normals_split_custom_set_from_vertices(normals.tolist())
    me.update()
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def join(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = name
    joined.data.name = name
    return joined


# ---------------------------------------------------------------------------------------------- materials and sidecar

def unity_st(material_name, gltf):
    """The base map's tiling and offset for Unity from the glTF's texture transform (V is flipped between them)."""
    for m in gltf["materials"]:
        if m["name"] != material_name:
            continue
        transform = m.get("pbrMetallicRoughness", {}).get("baseColorTexture", {}).get("extensions", {}).get("KHR_texture_transform")
        if not transform:
            return None
        sx, sy = transform.get("scale", [1, 1])
        ox, oy = transform.get("offset", [0, 0])
        return [sx, sy, ox, 1 - sy - oy]
    return None


def bark_key(material_name):
    key = material_name[len(ASSET) + 1:] if material_name.startswith(ASSET + "_") else material_name
    return key if os.path.exists(os.path.join(MAPS, f"{ASSET}_{key}_diff_2k.jpg")) else "bark"


def copy_bark_maps(keys):
    for key in keys:
        for kind, suffix in (("diff", "_Color.jpg"), ("nor_gl", "_Normal.jpg"), ("arm", "_ORM_2k.jpg")):
            source = os.path.join(MAPS, f"{ASSET}_{key}_{kind}_2k.jpg")
            if os.path.exists(source):
                shutil.copyfile(source, os.path.join(OUT, f"{ASSET}_{key}{suffix}"))


def sidecar(bark_names, gltf, height):
    def tex(name, path):
        return {"name": name, "type": "texture", "texture": path}

    def num(name, value):
        return {"name": name, "type": "float", "value": [value]}

    moss = "Assets/_Project/Art/Generated/SurfaceLayers/moss/moss_"
    sections = []
    for name in bark_names:
        key = bark_key(name)
        properties = [
            tex("_BaseMap", f"{ASSET}_{key}_Color.jpg"), tex("_BumpMap", f"{ASSET}_{key}_Normal.jpg"),
            tex("_ORMMap", f"{ASSET}_{key}_ORM_2k.jpg"), {"name": "_ORMMAP", "type": "keyword", "value": [1.0]},
            num("_Roughness", 1.0), num("_OcclusionStrength", 1.0), num("_Metallic", 0.0),
            num("_LayerTriplanar", 1.0), {"name": "_LAYERTRIPLANAR_ON", "type": "keyword", "value": [1.0]},
            # Moss layer set up but off by default: the generic bright moss painted the trunks in green stripes.
            num("_L1Amount", MOSS), num("_L1Source", 4.0), num("_L1Upward", 0.0), num("_L1Patchiness", 0.75),
            num("_L1PatchScale", 0.6), num("_L1Tiling", 1.4),
            tex("_L1Color", moss + "Color.png"), tex("_L1Normal", moss + "Normal.png"),
            tex("_L1MetallicGloss", moss + "MetallicGloss.png"), tex("_L1Height", moss + "Height.png"),
            num("_L1HeightChannel", 0.0),
        ]
        st = unity_st(name, gltf)
        if st:
            properties.append({"name": "_BaseMap_ST", "type": "vector", "value": st})
        sections.append({"match": name, "shader": "Safe Haven/Layered Lit", "layers": ["Moss"], "properties": properties})
    sections.append({"match": f"{PREFIX} Needles", "shader": "Safe Haven/Tree Wind", "properties": [
        tex("_BaseMap", f"{PREFIX} Needles_Color.png"), tex("_BumpMap", f"{PREFIX} Needles_Normal.png"),
        {"name": "_BaseColor", "type": "color", "value": list(TINT) + [1.0]},
        num("_AlphaClip", 1.0), {"name": "_ALPHATEST_ON", "type": "keyword", "value": [1.0]}, num("_Cutoff", 0.4),
        num("_Cull", 0.0), num("_Roughness", 0.85), num("_FastLeafLighting", 1.0),
        {"name": "_FAST_LEAF_LIGHTING", "type": "keyword", "value": [1.0]}, num("_Translucency", 0.45),
        num("_WindStrength", 1.0), num("_TreeHeight", height),
    ]})
    return {"version": 1, "shader": "Safe Haven/Layered Lit", "materials": sections}


# ---------------------------------------------------------------------------------------------- main

def preview_material(needles):
    """Lit needles for the preview render: the atlas colour, alpha-clipped."""
    needles.use_nodes = True
    nodes, links = needles.node_tree.nodes, needles.node_tree.links
    nodes.clear()
    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    colour = nodes.new("ShaderNodeTexImage")
    colour.image = bpy.data.images.load(os.path.join(OUT, f"{PREFIX} Needles_Color.png"), check_existing=True)
    clip = nodes.new("ShaderNodeMath")
    clip.operation = "GREATER_THAN"
    clip.inputs[1].default_value = 0.4
    links.new(colour.outputs["Color"], bsdf.inputs["Base Color"])
    links.new(colour.outputs["Alpha"], clip.inputs[0])
    links.new(clip.outputs[0], bsdf.inputs["Alpha"])
    bsdf.inputs["Roughness"].default_value = 0.8
    links.new(bsdf.outputs[0], out.inputs["Surface"])


def render_preview(objects, path):
    """All LOD0 trees in a row and the first tree's LOD1 and LOD2 beside them, in sunlight, seen from the side."""
    scene = bpy.context.scene
    scene.view_settings.view_transform = "Standard"
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("preview world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.55, 0.65, 0.8, 1.0)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.8
    scene.world = world
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(30))
    scene.collection.objects.link(sun)
    x = 0.0
    tallest = 0.0
    for obj in objects:
        width = max(obj.dimensions.x, obj.dimensions.y, 4.0)
        obj.location = (x + width * 0.5, 0.0, 0.0)
        x += width + 1.0
        tallest = max(tallest, obj.dimensions.z)
    camera_data = bpy.data.cameras.new("preview camera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = max(x, tallest * 1.9)
    camera = bpy.data.objects.new("preview camera", camera_data)
    scene.collection.objects.link(camera)
    camera.location = (x * 0.5, -60.0, tallest * 0.5)
    camera.rotation_euler = (math.radians(90), 0, 0)
    scene.camera = camera
    scene.render.resolution_x = 1800
    scene.render.resolution_y = int(1800 * min(tallest * 1.1 / camera_data.ortho_scale, 1.0)) or 900
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    log("preview", path)


def export(objects, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={"MESH", "EMPTY"}, path_mode="STRIP",
                             embed_textures=False, mesh_smooth_type="OFF", add_leaf_bones=False, bake_anim=False,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, bake_space_transform=True)
    return path


def main():
    with open(SRC, encoding="utf-8") as f:
        gltf = json.load(f)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC)
    trees = sorted([o for o in bpy.context.scene.objects if o.type == "MESH"], key=lambda o: o.name)
    log("imported", [o.name for o in trees])
    needles = bpy.data.materials.new(f"{PREFIX} Needles")
    layout = None
    bark_names = set()
    preview = []
    for index, obj in enumerate(trees):
        letter = chr(ord("A") + index)
        bark, centres, areas, height, origin = split_tree(obj)
        cell = height / CELLS
        log(letter, "height", round(height, 2), "needle polygons", len(areas), "cell", round(cell, 3))
        if layout is None:
            cards = cluster(centres, areas, cell)
            obj.location = Vector((-float(origin[0]), -float(origin[1]), -float(origin[2])))  # render around the origin
            bpy.context.view_layer.update()
            for other in trees:
                other.hide_render = other is not obj
            layout = render_atlas(obj, cards, cell)
        bark_names.update(bark["names"])
        bpy.data.objects.remove(obj, do_unlink=True)

        root = bpy.data.objects.new(f"{PREFIX} {letter}", None)
        bpy.context.scene.collection.objects.link(root)
        parts = [root]
        for level, (scale, trunk, twigs) in enumerate(zip(LOD_CELL, LOD_TRUNK, LOD_TWIGS)):
            cards = cluster(centres, areas, cell * scale)
            lod_name = f"{PREFIX} {letter}_LOD{level}"
            trunk_object = bark_object(bark, trunk, lod_name + "_bark", twigs)
            card_obj = card_object(cards, layout, needles, height, lod_name + "_cards")
            lod = join([trunk_object, card_obj], lod_name)
            lod.parent = root
            parts.append(lod)
            log(letter, "LOD", level, "cards", len(cards["half"]), "triangles", sum(len(p.vertices) - 2 for p in lod.data.polygons))
        export(parts, f"{PREFIX} {letter}")
        with open(os.path.join(OUT, f"{PREFIX} {letter}.material.json"), "w", encoding="utf-8") as f:
            json.dump(sidecar(sorted(bark_names), gltf, height), f, indent=2)
        for o in parts:
            if o.type == "MESH" and (o.name.endswith("_LOD0") or (index == 0 and o.type == "MESH")):
                o.parent = None
                preview.append(o)
            else:
                bpy.data.objects.remove(o, do_unlink=True)
    if OPTS.get("preview", True):
        preview_material(needles)
        render_preview(preview, os.path.join(os.path.dirname(OUT.rstrip("/\\")), f"{PREFIX} preview.png"))
    copy_bark_maps(sorted({bark_key(n) for n in bark_names}))
    log("done")


main()
