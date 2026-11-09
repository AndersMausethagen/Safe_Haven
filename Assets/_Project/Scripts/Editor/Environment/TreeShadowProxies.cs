using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Trees cast their shadow from a lighter copy of themselves. From the top-down camera most trees on screen sit at their two
/// most detailed levels, and the shadow map draws the level the camera sees, twice (two cascades): in the Misty Woods 20-40
/// million triangles a frame went into shadows alone. Here those two levels stop casting and carry a shadows-only copy of the
/// third level's meshes instead, so what is seen is unchanged and the shadow is the same crown, a little coarser in its leaves.
/// Run by VegetationPackConverter.Tree, and over existing trees from Safe Haven › World › Add Tree Shadow Proxies.
/// </summary>
public static class TreeShadowProxies
{
    private const string ProxyName = "Shadow Proxy";
    private const int ShadowLevel = 2;

    [MenuItem("Safe Haven/World/Add Tree Shadow Proxies")]
    private static void AddToTerrainTrees()
    {
        int changed = 0, checkedTrees = 0;
        var prefabs = new HashSet<GameObject>();
        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>())
        {
            foreach (TreePrototype prototype in terrain.terrainData.treePrototypes)
            {
                if (prototype.prefab != null)
                {
                    prefabs.Add(prototype.prefab);
                }
            }
        }

        foreach (GameObject prefab in prefabs)
        {
            checkedTrees++;
            if (AddToPrefab(prefab))
            {
                changed++;
            }
        }

        foreach (Terrain terrain in Object.FindObjectsByType<Terrain>())
        {
            terrain.terrainData.RefreshPrototypes();
            terrain.Flush();
        }

        Debug.Log($"Tree shadow proxies: {changed} of {checkedTrees} tree prefabs now cast from LOD{ShadowLevel}.");
    }

    /// <summary>Adds (or refreshes) the proxies on a prefab asset; false when it is not a tree with enough levels.</summary>
    public static bool AddToPrefab(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path) || !IsTree(prefab))
        {
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!Add(root))
            {
                return false;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Trees only: the wind shader's, with a third level to cast from. Rocks and logs keep their own shadows.
    private static bool IsTree(GameObject prefab) =>
        prefab.TryGetComponent(out LODGroup group) && group.lodCount > ShadowLevel
        && prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Any(m => m != null && m.shader != null && m.shader.name == "Safe Haven/Tree Wind");

    /// <summary>On an open root: the two finest levels stop casting and share a shadows-only copy of the third.</summary>
    public static bool Add(GameObject root)
    {
        if (!root.TryGetComponent(out LODGroup group) || group.lodCount <= ShadowLevel)
        {
            return false;
        }

        LOD[] lods = group.GetLODs();
        foreach (Transform old in root.transform.Cast<Transform>().Where(t => t.name.StartsWith(ProxyName)).ToList())
        {
            Object.DestroyImmediate(old.gameObject);
        }

        for (int level = 0; level < lods.Length; level++)
        {
            lods[level].renderers = lods[level].renderers.Where(r => r != null).ToArray();
        }

        var proxies = new List<Renderer>();
        foreach (Renderer source in lods[ShadowLevel].renderers)
        {
            if (!(source is MeshRenderer) || !source.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
            {
                continue;
            }

            var proxy = new GameObject($"{ProxyName} {proxies.Count}");
            proxy.transform.SetParent(root.transform, false);
            proxy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            proxy.transform.localScale = Vector3.Scale(source.transform.lossyScale, new Vector3(1f / root.transform.lossyScale.x, 1f / root.transform.lossyScale.y, 1f / root.transform.lossyScale.z));
            proxy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var drawn = proxy.AddComponent<MeshRenderer>();
            drawn.sharedMaterials = source.sharedMaterials;
            drawn.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            drawn.receiveShadows = false;
            proxies.Add(drawn);
        }

        if (proxies.Count == 0)
        {
            return false;
        }

        for (int level = 0; level < ShadowLevel; level++)
        {
            foreach (Renderer renderer in lods[level].renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            lods[level].renderers = lods[level].renderers.Concat(proxies).ToArray();
        }

        group.SetLODs(lods);
        return true;
    }
}
