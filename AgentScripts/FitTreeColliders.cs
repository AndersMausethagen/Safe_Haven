using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Gives the generated vegetation trees a trunk collider so the player can't walk through them
/// (`unity command run_script --file AgentScripts/FitTreeColliders.cs --entry FitTreeColliders.Prefabs`, then
/// `.SceneCopies` with the scene open). The capsule stands on the trunk's foot, as wide as the trunk 0.5-2 m up (the 75th
/// percentile of the vertices' distance from the trunk's middle there, so a branch or root doesn't widen it).
/// </summary>
public static class FitTreeColliders
{
    private const string Folder = "Assets/_Project/Models/Generated/Vegetation";

    public static string Prefabs()
    {
        var lines = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentInChildren<Collider>(true) != null || !Fit(root, out string how))
                {
                    continue;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                lines.Add($"{path}: {how}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return lines.Count == 0 ? "no prefabs needed one" : string.Join("\n", lines);
    }

    /// <summary>Trees in the open scene placed straight from a model (not a prefab with a collider) get one too.</summary>
    public static string SceneCopies()
    {
        var lines = new List<string>();
        foreach (GameObject go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Select(t => t.gameObject))
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go) || !PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go).StartsWith(Folder)
                || go.GetComponentInChildren<Collider>(true) != null)
            {
                continue;
            }

            if (Fit(go, out string how))
            {
                lines.Add($"{go.name}: {how}");
            }
        }

        if (lines.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        return lines.Count == 0 ? "none" : string.Join("\n", lines);
    }

    // Only whole trees with a "Trunk" part (not the separate Leaves/Trunk part prefabs); only the trunk's mesh counts.
    private static bool Fit(GameObject tree, out string how)
    {
        how = null;
        MeshFilter trunk = tree.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.name == "Trunk");
        if (trunk == null || trunk.sharedMesh == null || trunk.transform == tree.transform)
        {
            return false;
        }

        Matrix4x4 local = tree.transform.worldToLocalMatrix * trunk.transform.localToWorldMatrix;
        List<Vector3> points = trunk.sharedMesh.vertices.Select(v => local.MultiplyPoint3x4(v)).ToList();
        float foot = points.Min(p => p.y);
        float top = points.Max(p => p.y);
        float tall = top - foot;
        // Above the root flare, below the first branches.
        List<Vector3> band = points.Where(p => p.y >= foot + tall * 0.06f && p.y <= foot + tall * 0.22f).ToList();
        if (band.Count < 8)
        {
            return false;
        }

        var middle = new Vector2(band.Average(p => p.x), band.Average(p => p.z));
        List<float> distances = band.Select(p => Vector2.Distance(new Vector2(p.x, p.z), middle)).OrderBy(d => d).ToList();
        float radius = distances[Mathf.Clamp(Mathf.RoundToInt(distances.Count * 0.75f), 0, distances.Count - 1)];

        var capsule = tree.AddComponent<CapsuleCollider>();
        capsule.direction = 1;
        capsule.radius = radius;
        capsule.height = Mathf.Max(tall * 0.6f, radius * 2f);
        capsule.center = new Vector3(middle.x, foot + capsule.height * 0.5f, middle.y);
        float scale = tree.transform.lossyScale.y;
        how = $"trunk {tall:F1} tall, radius {radius:F2} (x{scale:F2} = {radius * scale:F2} m in place)";
        return true;
    }
}
