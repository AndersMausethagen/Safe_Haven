using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Meshes for the skill effects, made in code: an ice crystal (a long six-sided point) for the frost spikes.</summary>
public static class SkillMeshes
{
    public const string Folder = "Assets/_Project/Art/Effects/Skills/Meshes";

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
