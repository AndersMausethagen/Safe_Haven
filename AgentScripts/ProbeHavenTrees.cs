using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Lists trees in the open scene without colliders: placed tree objects (by prefab) and terrain tree prototypes
/// (`unity command run_script --file AgentScripts/ProbeHavenTrees.cs --entry ProbeHavenTrees.Report`).</summary>
public static class ProbeHavenTrees
{
    public static string Open(string path)
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        EditorSceneManager.OpenScene(path);
        return "opened " + path;
    }

    public static string Report()
    {
        var lines = new List<string>();
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include))
        {
            TerrainData data = terrain.terrainData;
            var collider = terrain.GetComponent<TerrainCollider>();
            lines.Add($"terrain {terrain.name}: {data.treeInstanceCount} trees, tree colliders {(collider != null ? "terrain collider" : "no collider")}");
            for (int i = 0; i < data.treePrototypes.Length; i++)
            {
                GameObject prefab = data.treePrototypes[i].prefab;
                int count = data.treeInstances.Count(t => t.prototypeIndex == i);
                bool hasCollider = prefab != null && prefab.GetComponentInChildren<Collider>(true) != null;
                lines.Add($"  prototype {i} {(prefab != null ? AssetDatabase.GetAssetPath(prefab) : "none")} x{count} collider={hasCollider}");
            }
        }

        var byPrefab = new Dictionary<string, (int total, int bare)>();
        foreach (GameObject go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Select(t => t.gameObject))
        {
            if (!PrefabUtility.IsOutermostPrefabInstanceRoot(go))
            {
                continue;
            }

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!path.ToLowerInvariant().Contains("tree") && !path.Contains("Spruce") && !path.Contains("Pine") && !path.Contains("Birch") && !path.Contains("Oak"))
            {
                continue;
            }

            bool bare = go.GetComponentInChildren<Collider>() == null;
            byPrefab.TryGetValue(path, out var counts);
            byPrefab[path] = (counts.total + 1, counts.bare + (bare ? 1 : 0));
        }

        foreach (var pair in byPrefab.OrderByDescending(p => p.Value.bare))
        {
            lines.Add($"placed {pair.Key}: {pair.Value.total}, without collider {pair.Value.bare}");
        }

        return string.Join("\n", lines);
    }
}
