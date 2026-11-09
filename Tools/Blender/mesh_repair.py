"""Close the small holes and clear the debris that generated meshes arrive with, before they are decimated and baked.

LocalSpawn's Hunyuan3D meshes are closed only when the shape is a plain convex lump: of the 22 gathering looks, 17 had
between 32 and 393 open borders (its own report: "holes", "largest_hole"). Most are pin-pricks in creases, left where
the inner shell was cut away; a few are whole open sheets (a mushroom cap one face thick, the open underside). In game
a hole shows the ground through the model (back faces are culled), costs vertices (every border is a seam) and gives
the decimation a border to tear along.

repair(obj) works on a welded mesh:
- loose vertices and edges, zero-area faces and specks (islands a fraction of the model's size) go;
- every hole no wider than MAX_HOLE of the model's diagonal is filled, and longer ones that are only a crack (a tear
  down a chest's front: long, but a hair wide). Borders are walked round each vertex's own fan
  of faces, so two holes that touch at a vertex (half of them do: a missing pair of triangles) come apart and both
  close. The patch takes the texture coordinates of its rim, so the rim's colour runs across it; where the rim sits on
  more than one texture island it takes one rim point's colour instead of a smear across the atlas;
- the outline of an open sheet is not a hole (its faces lie inside the loop, not round it): closing it would put a
  lid on a mushroom cap. Those and wider borders are left alone and reported, so the asset's budget file says what is
  still open. Only the generator can close them (a signed remesh, or LocalSpawn's --watertight).

Used by ingest_generated.py on the generated mesh, before its copy is decimated, so the bake's source and the game's
mesh have the same patches. On its own it reports what it would do to GLBs, changing nothing on disk:
  blender -b --factory-startup --python Tools/Blender/mesh_repair.py -- <model.glb> [<model.glb> ...]
"""
import math
import sys

import bmesh
import bpy

# A border is filled when its loop is at most this wide (of the mesh's bounding diagonal) and this many edges long.
MAX_HOLE = 0.06
MAX_HOLE_EDGES = 400
# A crack: a longer border (up to MAX_CRACK of the diagonal) whose mean width is at most CRACK_WIDTH of it.
MAX_CRACK, CRACK_WIDTH = 0.3, 0.008
# The rim's texture coordinates run across the patch when they span at most this much of the atlas (one island).
UV_SPAN = 0.08
# An island is a speck when it is at most this wide (of the diagonal) and has few faces.
SPECK, SPECK_FACES = 0.012, 24
WELD = 1e-4


def _diagonal(bm):
    lo = [min(v.co[i] for v in bm.verts) for i in range(3)]
    hi = [max(v.co[i] for v in bm.verts) for i in range(3)]
    return max(1e-9, sum((hi[i] - lo[i]) ** 2 for i in range(3)) ** 0.5)


def _extent(verts):
    lo = [min(v.co[i] for v in verts) for i in range(3)]
    hi = [max(v.co[i] for v in verts) for i in range(3)]
    return sum((hi[i] - lo[i]) ** 2 for i in range(3)) ** 0.5


def borders(bm):
    """The open borders: each a list of boundary edges joined at their vertices, and whether it is one simple loop."""
    seen = set()
    found = []
    for start in bm.edges:
        if not start.is_boundary or start in seen:
            continue
        edges, stack, simple = [], [start], True
        seen.add(start)
        while stack:
            edge = stack.pop()
            edges.append(edge)
            for vert in edge.verts:
                around = [e for e in vert.link_edges if e.is_boundary]
                if len(around) != 2:
                    simple = False  # borders meet here (a figure of eight), or a non-manifold fan
                for other in around:
                    if other not in seen:
                        seen.add(other)
                        stack.append(other)
        found.append((edges, simple))
    return found


def _islands(bm):
    seen = set()
    for start in bm.faces:
        if start in seen:
            continue
        island, stack = [], [start]
        seen.add(start)
        while stack:
            face = stack.pop()
            island.append(face)
            for edge in face.edges:
                for other in edge.link_faces:
                    if other not in seen:
                        seen.add(other)
                        stack.append(other)
        yield island


def _holes(bm):
    """Each hole as the face corners round it, in order. A border is followed from one boundary edge to the next round
    the vertex's fan of faces, so borders that only touch at a vertex are separate holes; a walk that comes back to a
    vertex is cut there into its own loops."""
    seen = set()
    for edge in bm.edges:
        if not edge.is_boundary or edge in seen:
            continue
        first = edge.link_loops[0]
        ring, loop, whole = [], first, True
        while whole:
            seen.add(loop.edge)
            ring.append(loop)
            step = loop.link_loop_next  # the next edge of the same face, from the far vertex of this one
            for _ in range(256):
                if step.edge.is_boundary:
                    break
                across = step.link_loop_radial_next
                if len(step.edge.link_faces) != 2 or across.vert == step.vert:
                    whole = False  # more than two faces on the edge, or the neighbour is wound the other way
                    break
                step = across.link_loop_next
            else:
                whole = False
            if not whole or step.edge == first.edge:
                break
            if step.edge in seen or len(ring) > 100000:
                whole = False
                break
            loop = step
        if not whole:
            continue
        stack, at = [], {}
        for loop in ring:
            if loop.vert in at:
                cut = at[loop.vert]
                cycle = stack[cut:]
                del stack[cut:]
                for l in cycle:
                    at.pop(l.vert, None)
                if len(cycle) >= 3:
                    yield cycle
            at[loop.vert] = len(stack)
            stack.append(loop)
        if len(stack) >= 3:
            yield stack


def _width(cycle):
    """The loop's mean width: twice its area over its length round (a strip's width, a disc's radius)."""
    points = [l.vert.co for l in cycle]
    area, around = points[0] * 0.0, 0.0
    for a, b in zip(points, points[1:] + points[:1]):
        area += a.cross(b)
        around += (b - a).length
    return area.length / max(around, 1e-12)


def _is_outline(cycle):
    """True when the loop runs round its own faces (the edge of an open sheet) instead of round a gap in them."""
    n = len(cycle)
    centre = sum((l.vert.co for l in cycle), cycle[0].vert.co * 0.0) / n
    inward = 0.0
    for loop in cycle:
        middle = (loop.vert.co + loop.link_loop_next.vert.co) * 0.5
        to_face = loop.face.calc_center_median() - middle
        to_centre = centre - middle
        if to_face.length > 1e-12 and to_centre.length > 1e-12:
            inward += to_face.normalized().dot(to_centre.normalized())
    return inward / n > 0.3


def _fill(bm, cycle):
    """A patch over the hole, wound like the faces round it, its texture coordinates from the rim; 1 when it was made."""
    verts = [l.vert for l in reversed(cycle)]
    layers = []  # per texture layer: the rim's coordinates, and whether they lie on one island
    for layer in bm.loops.layers.uv.values():
        rim = {l.vert: l[layer].uv.copy() for l in cycle}
        us, vs = [uv.x for uv in rim.values()], [uv.y for uv in rim.values()]
        layers.append((layer, rim, max(us) - min(us) <= UV_SPAN and max(vs) - min(vs) <= UV_SPAN))

    def map_face(face):
        face.smooth = True
        for layer, rim, one_island in layers:
            anchor = rim[verts[0]]
            for k, corner in enumerate(face.loops):
                if one_island:
                    corner[layer].uv = rim[corner.vert] if corner.vert in rim else sum(rim.values(), anchor * 0.0) / len(rim)
                else:
                    # One rim point's texel, on a triangle just large enough to have a tangent (for the normal map).
                    turn = 6.2831853 * k / len(face.loops)
                    corner[layer].uv = (anchor.x + 2e-4 * math.cos(turn), anchor.y + 2e-4 * math.sin(turn))

    try:
        face = bm.faces.new(verts, cycle[0].face)
    except ValueError:  # the face is already there (a doubled triangle)
        return 0
    map_face(face)
    faces = bmesh.ops.triangulate(bm, faces=[face])["faces"] if len(verts) > 3 else [face]
    if not any(len(e.link_faces) > 2 for f in faces for e in f.edges):
        return 1
    # Two rim vertices were already joined by a closed edge, and the patch hung a third face on it (long cracks do
    # this: their rim runs along whole triangles). A fan from a new point in the middle only makes new edges.
    bmesh.ops.delete(bm, geom=faces, context='FACES')
    middle = bm.verts.new(sum((v.co for v in verts), verts[0].co * 0.0) / len(verts))
    for a, b in zip(verts, verts[1:] + verts[:1]):
        map_face(bm.faces.new((a, b, middle), cycle[0].face))
    return 1


def measure(bm, diagonal=None):
    """What is open: the number of borders, the widest (of the diagonal), and edges shared by more than two faces."""
    diagonal = diagonal or _diagonal(bm)
    open_borders = borders(bm)
    widest = max((_extent({v for e in edges for v in e.verts}) for edges, _ in open_borders), default=0.0)
    return {"holes": len(open_borders), "largest_hole": round(widest / diagonal, 3),
            "non_manifold_edges": sum(1 for e in bm.edges if len(e.link_faces) > 2)}


def repair_bmesh(bm, max_hole=MAX_HOLE):
    """Weld, clean and fill bm in place; returns what it found and did."""
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=WELD)
    diagonal = _diagonal(bm)
    before = measure(bm, diagonal)

    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-7)
    specks = [f for island in list(_islands(bm)) if len(island) <= SPECK_FACES
              and _extent({v for f in island for v in f.verts}) <= SPECK * diagonal for f in island]
    if specks:
        bmesh.ops.delete(bm, geom=specks, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context='VERTS')

    filled = 0
    for cycle in list(_holes(bm)):
        extent = _extent({l.vert for l in cycle})
        crack = extent <= MAX_CRACK * diagonal and _width(cycle) <= CRACK_WIDTH * diagonal
        if len(cycle) > MAX_HOLE_EDGES or not (extent <= max_hole * diagonal or crack) or _is_outline(cycle):
            continue
        filled += _fill(bm, cycle)
    bm.normal_update()
    after = measure(bm, diagonal)
    return {"holes_before": before["holes"], "largest_before": before["largest_hole"], "filled": filled,
            "specks_removed": len(specks), "loose_removed": len(loose), **after}


def repair(obj, max_hole=MAX_HOLE):
    """Repair obj's mesh in place (see the module's note); returns the report."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    report = repair_bmesh(bm, max_hole)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return report


def measure_object(obj):
    """holes / largest_hole / non_manifold_edges of obj as it is (welded copy; obj is not changed)."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=WELD)
    report = measure(bm)
    bm.free()
    return report


def _report(paths):
    for path in paths:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=path)
        meshes = [o for o in bpy.data.objects if o.type == 'MESH']
        for obj in meshes:
            triangles, vertices = len(obj.data.polygons), len(obj.data.vertices)
            report = repair(obj)
            print("REPAIR", path, "|", triangles, "tris", vertices, "verts ->", len(obj.data.polygons), "tris",
                  len(obj.data.vertices), "verts |", report, flush=True)


if __name__ == "__main__":
    _report(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
