using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Meshes for the skill effects, made in code: an ice crystal (a long six-sided point) for the frost spikes, and a jagged rock chunk
/// for the debris a slam throws up.</summary>
public static class SkillMeshes
{
    public const string Folder = "Assets/_Project/Art/Effects/Skills/Meshes";

    /// <summary>A rough rock chunk about 0.3 m across: an octahedron's faces split once, its corners pushed in and out, flat-shaded.</summary>
    public static Mesh Rock()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Art/Effects/Skills", "Meshes");
        }

        string path = Folder + "/Rock Chunk.asset";
        var random = new System.Random(11);
        Vector3[] corners = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        int[,] faces = { { 0, 4, 3 }, { 0, 3, 5 }, { 0, 5, 2 }, { 0, 2, 4 }, { 1, 3, 4 }, { 1, 5, 3 }, { 1, 2, 5 }, { 1, 4, 2 } };
        var jitter = new Dictionary<Vector3, Vector3>();
        Vector3 Bumped(Vector3 v)
        {
            Vector3 key = v.normalized;
            if (!jitter.TryGetValue(key, out Vector3 at))
            {
                at = key * (0.11f + (float)random.NextDouble() * 0.07f);
                at.y *= 0.75f;
                jitter[key] = at;
            }

            return at;
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int f = 0; f < faces.GetLength(0); f++)
        {
            Vector3 a = corners[faces[f, 0]], b = corners[faces[f, 1]], c = corners[faces[f, 2]];
            Vector3 ab = (a + b) * 0.5f, bc = (b + c) * 0.5f, ca = (c + a) * 0.5f;
            foreach (Vector3[] tri in new[] { new[] { a, ab, ca }, new[] { ab, b, bc }, new[] { ca, bc, c }, new[] { ab, bc, ca } })
            {
                int i = vertices.Count;
                vertices.Add(Bumped(tri[0]));
                vertices.Add(Bumped(tri[1]));
                vertices.Add(Bumped(tri[2]));
                triangles.AddRange(new[] { i, i + 1, i + 2 });
            }
        }

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool made = mesh == null;
        if (made)
        {
            mesh = new Mesh { name = "Rock Chunk" };
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (made)
        {
            AssetDatabase.CreateAsset(mesh, path);
        }

        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }

    /// <summary>A crystal one metre tall standing on its base at the origin, flat-shaded (each face its own vertices).</summary>
    public static Mesh Crystal()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Art/Effects/Skills", "Meshes");
        }

        string path = Folder + "/Ice Crystal.asset";
        const int sides = 6;
        // Rings from the base up: a narrow foot, the widest girth low down, a shoulder, then the point.
        float[] heights = { 0f, 0.18f, 0.62f, 1f };
        float[] radii = { 0.09f, 0.16f, 0.12f, 0f };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int ring = 0; ring < heights.Length - 1; ring++)
        {
            for (int s = 0; s < sides; s++)
            {
                float a0 = s * Mathf.PI * 2f / sides, a1 = (s + 1) * Mathf.PI * 2f / sides;
                var p0 = new Vector3(Mathf.Cos(a0) * radii[ring], heights[ring], Mathf.Sin(a0) * radii[ring]);
                var p1 = new Vector3(Mathf.Cos(a1) * radii[ring], heights[ring], Mathf.Sin(a1) * radii[ring]);
                var q0 = new Vector3(Mathf.Cos(a0) * radii[ring + 1], heights[ring + 1], Mathf.Sin(a0) * radii[ring + 1]);
                var q1 = new Vector3(Mathf.Cos(a1) * radii[ring + 1], heights[ring + 1], Mathf.Sin(a1) * radii[ring + 1]);
                int i = vertices.Count;
                if (radii[ring + 1] <= 0f)
                {
                    // Up to the point: one triangle a side (a quad there had a second, empty triangle, and its point no normal).
                    vertices.AddRange(new[] { p0, p1, q0 });
                    triangles.AddRange(new[] { i, i + 2, i + 1 });
                    continue;
                }

                vertices.AddRange(new[] { p0, p1, q1, q0 });
                triangles.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
        }

        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool made = mesh == null;
        if (made)
        {
            mesh = new Mesh { name = "Ice Crystal" };
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (made)
        {
            AssetDatabase.CreateAsset(mesh, path);
        }

        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);
        return mesh;
    }
}
