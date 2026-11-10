using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stands rock-face pieces from a <see cref="CliffKit"/> along a steep face, each fitted exactly: sized to the face's
/// height (stacked in rows on tall faces), its finished side turned outwards, its front moved onto the face (Inset decides
/// how far up the slope) and its top onto the rim, the rest buried in the ground; neighbours overlap. Used by cliffs (one
/// face along the line) and ravines (a face along each side).
/// <para>
/// A piece is stood by its own shape (<see cref="ShapeOf"/>), whatever the kit says of it. A scan of a rock face is an open
/// sheet with a finished side: it stands as the kit measured it. A boulder is closed, has no finished side and was
/// modelled lying down: stood as modelled and scaled until its small height met the face, it became a huge flat slab
/// sticking out of the cliff. So a boulder is turned thinnest way into the cliff and longest way up, leaned back against
/// the face, and all but its last fifth buried in it: rock that is part of the cliff, not laid at its foot.
/// </para>
/// </summary>
public static class CragWall
{
    /// <summary>Where along the line a distance is (CliffBuilder/RavineBuilder: FeatureBuilder.SegmentAt).</summary>
    public delegate int SegmentFinder(float along, out float fraction);

    public sealed class Face
    {
        public SplineLine Line;
        public CliffKit Kit;
        public System.Random Random;
        public SegmentFinder SegmentAt;
        /// <summary>Flat direction pointing into the high ground at a sample.</summary>
        public System.Func<int, Vector2> HighNormal;
        /// <summary>The face's top edge (flat) at a sample and fraction.</summary>
        public System.Func<int, float, Vector2> Edge;
        /// <summary>How far the edge wanders off its line at a sample and fraction (metres, + into the high ground).</summary>
        public System.Func<int, float, float> Rag;
        /// <summary>The rim's height at a sample, fraction and edge point.</summary>
        public System.Func<int, float, Vector2, float> Rim;
        public float FaceDepth;
        public float Inset;
        public float Overlap;
        public Vector2 SizeVariation;
        /// <summary>Share of pieces that get a second piece leaning out over the rim (an overhang). 0: none.</summary>
        public float Overhangs;
    }

    /// <summary>A piece as it is stood: its points (prefab root space), the turn that stands it, and its size stood up at the
    /// prefab's own scale (x across the face, y height, z depth into the cliff).</summary>
    public sealed class Shape
    {
        public Vector3[] Points;
        public Quaternion Stand;
        public Vector3 Size;
        /// <summary>A closed boulder (stood by its shape), not an open scan of a rock face (stood as the kit measured it).</summary>
        public bool Boulder;
    }

    // An open sheet's faces mostly look one way: the sum of its triangles' area vectors is long. A closed boulder's cancel out.
    private const float OpenSheet = 0.3f;

    /// <summary>How a kit piece stands against a face, worked out from its mesh (see the class summary).</summary>
    public static Shape ShapeOf(CliffKit.Piece piece, Dictionary<GameObject, Shape> shapes)
    {
        if (shapes.TryGetValue(piece.prefab, out Shape shape))
        {
            return shape;
        }

        var points = new List<Vector3>();
        Vector3 facing = Vector3.zero;
        float area = 0f;
        Transform prefab = piece.prefab.transform;
        foreach (MeshFilter filter in piece.prefab.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null ||
                (filter.name.Contains("_LOD") && !filter.name.EndsWith("_LOD0")))
            {
                continue;
            }

            Matrix4x4 toRoot = prefab.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] triangles = filter.sharedMesh.triangles;
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = toRoot.MultiplyPoint3x4(vertices[triangles[t]]);
                Vector3 cross = Vector3.Cross(toRoot.MultiplyPoint3x4(vertices[triangles[t + 1]]) - a, toRoot.MultiplyPoint3x4(vertices[triangles[t + 2]]) - a);
                facing += cross;
                area += cross.magnitude;
            }

            for (int i = 0; i < vertices.Length; i += 3)
            {
                points.Add(toRoot.MultiplyPoint3x4(vertices[i]));
            }
        }

        Quaternion stand = WorldAssets.StandUp(piece);
        Vector3 size = Extents(points, stand);
        bool boulder = area <= 0f || facing.magnitude / area < OpenSheet;
        if (boulder)
        {
            // Thinnest way into the cliff, longest way up, the third along the face.
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int tall = thin != 0 && size.x >= size.y && size.x >= size.z ? 0 : thin != 1 && (size.y >= size.z || thin == 2) ? 1 : 2;
            if (tall == thin)
            {
                tall = (thin + 1) % 3;
            }

            Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            stand = Quaternion.Inverse(Quaternion.LookRotation(Axis(thin), Axis(tall))) * stand;
            size = Extents(points, stand);
        }

        shapes[piece.prefab] = shape = new Shape { Points = points.ToArray(), Stand = stand, Size = size * Mathf.Abs(prefab.localScale.y), Boulder = boulder };
        return shape;
    }

    // How big a set of points is along each axis once turned.
    private static Vector3 Extents(List<Vector3> points, Quaternion turn)
    {
        if (points.Count == 0)
        {
            return Vector3.one;
        }

        Vector3 min = turn * points[0], max = min;
        foreach (Vector3 point in points)
        {
            Vector3 turned = turn * point;
            min = Vector3.Min(min, turned);
            max = Vector3.Max(max, turned);
        }

        return Vector3.Max(max - min, Vector3.one * 0.05f);
    }

    public static void Stand(Transform root, Face face)
    {
        var shapes = new Dictionary<GameObject, Shape>();
        SplineLine line = face.Line;
        float along = 0f;
        int guard = 0;
        while (along < line.Length && guard++ < 4000)
        {
            CliffKit.Piece piece = face.Kit.Pick(face.Random);
            if (piece == null)
            {
                return;
            }

            int s = face.SegmentAt(along, out float f);
            Vector2 high = face.HighNormal(s);
            Vector2 centre = face.Edge(s, f);
            float rag = face.Rag(s, f);
            WorldGround.TryGround(centre + high * (-face.FaceDepth - 0.4f + rag), out Vector3 foot, out _);
            float rim = face.Rim(s, f, centre);
            float faceHeight = Mathf.Max(rim - foot.y, 0.5f);
            Shape shape = ShapeOf(piece, shapes);
            float size = Mathf.Max(shape.Size.y, 0.1f);
            int rows = Mathf.Max(1, Mathf.CeilToInt(faceHeight / (size * 2.5f)));
            float rowHeight = faceHeight / rows;
            float variation = Mathf.Lerp(face.SizeVariation.x, face.SizeVariation.y, (float)face.Random.NextDouble());
            float scale = Mathf.Clamp(rowHeight / size * variation, 0.3f, 4f);
            float width = Mathf.Max(shape.Size.x * scale, 0.5f);
            along = Mathf.Min(along + width * 0.5f, line.Length);
            s = face.SegmentAt(along, out f);
            high = face.HighNormal(s);
            centre = face.Edge(s, f);
            rag = face.Rag(s, f);
            rim = face.Rim(s, f, centre);
            Vector3 outward = new Vector3(-high.x, 0f, -high.y);
            // Inset 0 puts the front just outside the foot of the slope, so the piece covers the whole face.
            Vector2 front = centre + high * (-face.FaceDepth * (1f - face.Inset) - 0.15f + rag);
            for (int row = 0; row < rows; row++)
            {
                float rowScale = row == 0 ? scale : scale * Mathf.Lerp(0.9f, 1.05f, (float)face.Random.NextDouble());
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, root);
                float yaw = ((float)face.Random.NextDouble() - 0.5f) * 14f;
                // A real crag leans back a little; a boulder stood against a face leans with it, and no two stand quite alike.
                float lean = shape.Boulder ? 7f + (float)face.Random.NextDouble() * 8f : 3f + (float)face.Random.NextDouble() * 5f;
                float roll = shape.Boulder ? ((float)face.Random.NextDouble() - 0.5f) * 16f : 0f;
                instance.transform.SetPositionAndRotation(new Vector3(front.x, rim, front.y),
                    Quaternion.LookRotation(outward) * Quaternion.Euler(-lean, yaw, roll) * shape.Stand);
                instance.transform.localScale = piece.prefab.transform.localScale * rowScale;
                // Fit by the mesh itself: the bulk of its finished side onto the face (a few bumps and overhangs stick
                // out further), its top onto the rim (lower rows lower).
                Extent(instance, shape, outward, shape.Boulder ? 0.82f : 0.7f, out float frontmost, out float top);
                // A boulder's top stands a little over or under the rim, so the cliff's edge is broken, not ruled.
                float crown = shape.Boulder ? Mathf.Lerp(-0.45f, 0.35f, (float)face.Random.NextDouble()) : 0.15f;
                float wantedTop = rim + crown - row * rowHeight * 0.9f;
                float stepOut = row * 0.35f * face.FaceDepth; // lower rows stand a little further out
                float wantedFront = Vector3.Dot(new Vector3(front.x, 0f, front.y), outward) + stepOut;
                instance.transform.position += outward * (wantedFront - frontmost) + Vector3.up * (wantedTop - top);
            }

            // Now and then a block leaning out over the rim: the overhang a heightmap can't make. (No random draw when
            // overhangs are off, so faces built without them come out exactly as before.)
            if (face.Overhangs > 0f && face.Random.NextDouble() < face.Overhangs)
            {
                var ledge = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, root);
                float tilt = 14f + (float)face.Random.NextDouble() * 16f;
                ledge.transform.SetPositionAndRotation(new Vector3(front.x, rim, front.y),
                    Quaternion.LookRotation(outward) * Quaternion.Euler(tilt, ((float)face.Random.NextDouble() - 0.5f) * 30f, 0f) * shape.Stand);
                ledge.transform.localScale = piece.prefab.transform.localScale * scale * 0.7f;
                Extent(ledge, shape, outward, 0.7f, out float ledgeFront, out float ledgeTop);
                // Its top just above the rim, sticking out past the face by about a third of its size.
                float reach = shape.Size.x * scale * 0.7f * 0.35f;
                float wantedLedgeFront = Vector3.Dot(new Vector3(front.x, 0f, front.y), outward) + reach;
                ledge.transform.position += outward * (wantedLedgeFront - ledgeFront) + Vector3.up * (rim + 0.25f - ledgeTop);
            }

            along += width * (0.5f - face.Overlap); // to this piece's far end, back by the overlap
        }
    }

    /// <summary>Where the placed piece's finished side stands (a share of the way through its points along
    /// <paramref name="outward"/>, so a single overhang does not decide: the rest stands proud of the face) and how high it reaches.</summary>
    private static void Extent(GameObject instance, Shape shape, Vector3 outward, float share, out float frontmost, out float top)
    {
        // Points are in the prefab root's space; the instance root has the same hierarchy.
        Vector3[] points = shape.Points;
        Matrix4x4 toWorld = instance.transform.localToWorldMatrix;
        var along = new float[points.Length];
        top = float.MinValue;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 world = toWorld.MultiplyPoint3x4(points[i]);
            along[i] = Vector3.Dot(world, outward);
            top = Mathf.Max(top, world.y);
        }

        System.Array.Sort(along);
        frontmost = along.Length > 0 ? along[Mathf.Min(along.Length - 1, (int)(along.Length * share))] : 0f;
    }
}
