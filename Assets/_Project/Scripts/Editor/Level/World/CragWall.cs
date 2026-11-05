using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Stands rock-face pieces from a <see cref="CliffKit"/> along a steep face, each fitted exactly: sized to the face's
/// height (stacked in rows on tall faces), its finished side turned outwards, its front moved onto the face (Inset decides
/// how far up the slope) and its top onto the rim, the rest buried in the ground; neighbours overlap. Used by cliffs (one
/// face along the line) and ravines (a face along each side).
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

    public static void Stand(Transform root, Face face)
    {
        var shapes = new Dictionary<GameObject, Vector3[]>();
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
            float size = Mathf.Max(piece.size.y, 0.1f);
            int rows = Mathf.Max(1, Mathf.CeilToInt(faceHeight / (size * 2.5f)));
            float rowHeight = faceHeight / rows;
            float variation = Mathf.Lerp(face.SizeVariation.x, face.SizeVariation.y, (float)face.Random.NextDouble());
            float scale = Mathf.Clamp(rowHeight / size * variation, 0.3f, 4f);
            float width = Mathf.Max(piece.size.x * scale, 0.5f);
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
                float lean = 3f + (float)face.Random.NextDouble() * 5f; // a real crag leans back a little
                instance.transform.SetPositionAndRotation(new Vector3(front.x, rim, front.y),
                    Quaternion.LookRotation(outward) * Quaternion.Euler(-lean, yaw, 0f) * WorldAssets.StandUp(piece));
                instance.transform.localScale = piece.prefab.transform.localScale * rowScale;
                // Fit by the mesh itself: the bulk of its finished side onto the face (a few bumps and overhangs stick
                // out further), its top onto the rim (lower rows lower).
                Extent(instance, piece.prefab, shapes, outward, out float frontmost, out float top);
                float wantedTop = rim + 0.15f - row * rowHeight * 0.9f;
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
                    Quaternion.LookRotation(outward) * Quaternion.Euler(tilt, ((float)face.Random.NextDouble() - 0.5f) * 30f, 0f) * WorldAssets.StandUp(piece));
                ledge.transform.localScale = piece.prefab.transform.localScale * scale * 0.7f;
                Extent(ledge, piece.prefab, shapes, outward, out float ledgeFront, out float ledgeTop);
                // Its top just above the rim, sticking out past the face by about a third of its size.
                float reach = piece.size.x * scale * 0.7f * 0.35f;
                float wantedLedgeFront = Vector3.Dot(new Vector3(front.x, 0f, front.y), outward) + reach;
                ledge.transform.position += outward * (wantedLedgeFront - ledgeFront) + Vector3.up * (rim + 0.25f - ledgeTop);
            }

            along += width * (0.5f - face.Overlap); // to this piece's far end, back by the overlap
        }
    }

    /// <summary>Where the placed piece's finished side stands (the 70th percentile of its points along
    /// <paramref name="outward"/>, so a single overhang does not decide) and how high it reaches.</summary>
    private static void Extent(GameObject instance, GameObject prefab, Dictionary<GameObject, Vector3[]> shapes, Vector3 outward,
                               out float frontmost, out float top)
    {
        if (!shapes.TryGetValue(prefab, out Vector3[] points))
        {
            var list = new List<Vector3>();
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null ||
                    (filter.name.Contains("_LOD") && !filter.name.EndsWith("_LOD0")))
                {
                    continue;
                }

                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    list.Add(toRoot.MultiplyPoint3x4(vertices[i]));
                }
            }

            shapes[prefab] = points = list.ToArray();
        }

        // Points are in the prefab root's space; the instance root has the same hierarchy.
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
        frontmost = along.Length > 0 ? along[Mathf.Min(along.Length - 1, (int)(along.Length * 0.7f))] : 0f;
    }
}
