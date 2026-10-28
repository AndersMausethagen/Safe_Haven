using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Tall things (over 3 m) in the open scene without any collider, grouped by prefab or name
/// (`unity command run_script --file AgentScripts/ProbeTall.cs --entry ProbeTall.Report`).</summary>
public static class ProbeTall
{
    public static string Report()
    {
        var groups = new Dictionary<string, int>();
        foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (renderer.bounds.size.y < 3f || renderer is ParticleSystemRenderer || renderer is LineRenderer)
            {
                continue;
            }

            GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(renderer.gameObject) ?? renderer.transform.root.gameObject;
            if (root.GetComponentInChildren<Collider>() != null || root.GetComponent<Terrain>() != null)
            {
                continue;
            }

            string key = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            key = string.IsNullOrEmpty(key) ? "(no prefab) " + root.name : key;
            groups.TryGetValue(key, out int n);
            groups[key] = n + 1;
        }

        return string.Join("\n", groups.OrderByDescending(g => g.Value).Select(g => $"{g.Value} x {g.Key}"));
    }
}
