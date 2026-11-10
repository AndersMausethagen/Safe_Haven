using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// What keeps converted props cheap, with nothing to see for it (Safe Haven › World › Optimise Props runs it over the
/// packs already converted; <see cref="Prop"/> runs it on every prop it makes from now on). Measured in the Haven on
/// 2026-11-10: 27,700 shadow-casting renderers cost 6 ms of every frame in shadow culling, and 2,353 obstacles 1.8 ms.
/// <list type="bullet">
/// <item><see cref="MergeLodParts"/>: the pieces of each LOD level become one mesh and one renderer. A pile of pebbles was
/// 33 renderers (its stones times its levels) and a crag 24.</item>
/// <item><see cref="TrimShadows"/>: a LOD level that only shows from further away than shadows are ever drawn stops
/// casting them. Worked out for the smallest scale a prop is placed at, the lowest LOD bias the options allow and the
/// longest shadow distance, with a margin, so no shadow that could be seen is lost.</item>
/// <item><see cref="DropStaticObstacle"/>: a prop that can never be mined, felled or moved needs no NavMeshObstacle. With
/// one, the NavMesh bake leaves it out (the bake skips anything carrying an obstacle) and creatures only steer round it
/// frame by frame; without, the bake cuts a hole for its collider and it costs nothing in play.</item>
/// </list>
/// </summary>
public static partial class VegetationPackConverter
{
    // The gameplay camera's field of view, the longest shadow distance of any pipeline asset, the lowest LOD bias the
    // options give (Quality's 2 at View Distance's 0.6), the smallest scale props are placed at, and a margin on top.
    private const float CameraFieldOfView = 40f;
    private const float LongestShadows = 50f;
    private const float LowestLodBias = 1.2f;
    private const float SmallestScale = 0.6f;
    private const float ShadowMargin = 1.25f;

    private static readonly string[] PropFolders =
    {
        "Assets/_Project/Art/Vegetation/Misty Woods", "Assets/_Project/Art/Vegetation/Safe Haven", "Assets/_Project/Art/Vegetation/Haven World",
        "Assets/_Project/Prefabs/Environment/World",
    };

    [MenuItem("Safe Haven/World/Optimise Props")]
    private static void OptimiseMenu() => Debug.Log(OptimiseProps());

    /// <summary>Runs the three over every converted prop prefab (not trees: their levels are trunk, leaves and shadow proxies).</summary>
    public static string OptimiseProps()
    {
        int prefabs = 0, merged = 0, trimmed = 0, obstacles = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", PropFolders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || !IsProp(asset))
            {
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                string folder = Path.GetDirectoryName(path).Replace('\\', '/');
                int a = MergeLodParts(root, folder, Path.GetFileNameWithoutExtension(path));
                int b = TrimShadows(root);
                int c = DropStaticObstacle(root) ? 1 : 0;
                if (a + b + c > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    prefabs++;
                    merged += a;
                    trimmed += b;
                    obstacles += c;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        return $"Optimise Props: {prefabs} prefab(s) changed; {merged} renderer(s) merged away, {trimmed} LOD level renderer(s) no longer cast shadows, {obstacles} obstacle(s) dropped.";
    }

    // A prop on the tessellated mesh shader: every drawn part uses it. Trees (Tree Wind, shadow proxies) are left alone.
    private static bool IsProp(GameObject root)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        return renderers.Length > 0 && root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null
               && renderers.All(r => r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && r.sharedMaterials.All(m => m != null && m.shader != null && m.shader.name == MeshSurface.ShaderName));
    }

    /// <summary>The pieces of each LOD level joined into one mesh (one sub-mesh per material) on one renderer. Returns how many
    /// renderers that saved.</summary>
    public static int MergeLodParts(GameObject root, string folder, string name)
    {
        if (!root.TryGetComponent(out LODGroup group))
        {
            return 0;
        }

        LOD[] lods = group.GetLODs();
        int saved = 0;
        var gone = new List<GameObject>();
        for (int level = 0; level < lods.Length; level++)
        {
            List<MeshRenderer> parts = lods[level].renderers.OfType<MeshRenderer>()
                .Where(r => r != null && r.gameObject != root && r.transform.childCount == 0 && r.TryGetComponent(out MeshFilter f) && f.sharedMesh != null).Distinct().ToList();
            if (parts.Count < 2 || parts.Count != lods[level].renderers.Count(r => r != null))
            {
                continue;
            }

            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (MeshRenderer part in parts)
            {
                Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 toRoot = root.transform.worldToLocalMatrix * part.transform.localToWorldMatrix;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    Material material = part.sharedMaterials[Mathf.Min(s, part.sharedMaterials.Length - 1)];
                    if (!groups.TryGetValue(material, out List<CombineInstance> list)) groups[material] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = s, transform = toRoot });
                }
            }

            // The index format is chosen before anything is joined: changing it afterwards empties the mesh of its triangles.
            int vertices = parts.Sum(part => part.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            IndexFormat format = vertices < 65000 ? IndexFormat.UInt16 : IndexFormat.UInt32;
            var pieces = new List<CombineInstance>();
            foreach (List<CombineInstance> list in groups.Values)
            {
                var joined = new Mesh { indexFormat = format };
                joined.CombineMeshes(list.ToArray(), true, true);
                pieces.Add(new CombineInstance { mesh = joined, transform = Matrix4x4.identity });
            }

            var whole = new Mesh { name = $"{name} LOD{level}", indexFormat = format };
            whole.CombineMeshes(pieces.ToArray(), false, false);
            foreach (CombineInstance piece in pieces) Object.DestroyImmediate(piece.mesh);
            whole.RecalculateBounds();
            long triangles = 0;
            for (int s = 0; s < whole.subMeshCount; s++) triangles += whole.GetIndexCount(s) / 3;
            long expected = parts.Sum(part => (long)part.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
            if (triangles != expected)
            {
                throw new System.InvalidOperationException($"{name} LOD{level}: the joined mesh has {triangles} triangles, its pieces {expected}.");
            }

            whole = SaveMesh(whole, $"{folder}/Meshes/{name} LOD{level}.asset");

            // The first piece carries the level from now on (its settings, its Mesh Surface); the rest go.
            MeshRenderer kept = parts[0];
            kept.name = $"{name}_LOD{level}";
            kept.transform.SetParent(root.transform, false);
            kept.transform.localPosition = Vector3.zero;
            kept.transform.localRotation = Quaternion.identity;
            kept.transform.localScale = Vector3.one;
            // Mesh Surface pads the renderer's bounds from the mesh it found when it was enabled: off while the mesh changes, so it
            // measures the joined mesh and not the one piece it had. (Instances in an open scene keep the old bounds until the
            // scene is opened again.)
            MeshSurface surface = kept.GetComponent<MeshSurface>();
            if (surface != null) surface.enabled = false;
            kept.GetComponent<MeshFilter>().sharedMesh = whole;
            kept.sharedMaterials = groups.Keys.ToArray();
            kept.ResetLocalBounds();
            if (surface == null) surface = kept.gameObject.AddComponent<MeshSurface>();
            surface.enabled = true;
            gone.AddRange(parts.Skip(1).Select(p => p.gameObject));
            lods[level].renderers = new Renderer[] { kept };
            saved += parts.Count - 1;
        }

        if (saved == 0)
        {
            return 0;
        }

        group.SetLODs(lods);
        foreach (GameObject part in gone) Object.DestroyImmediate(part);
        // Holders the pieces sat under, now empty.
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true).Where(t => t != null && t != root.transform).ToList())
        {
            if (child != null && child.childCount == 0 && child.GetComponents<Component>().Length == 1) Object.DestroyImmediate(child.gameObject);
        }

        group.RecalculateBounds();
        return saved;
    }

    /// <summary>Metres from the gameplay camera at which a LOD level begins to show, at the smallest scale and lowest LOD bias.</summary>
    public static float LevelStartsAt(LODGroup group, int level)
    {
        if (level <= 0)
        {
            return 0f;
        }

        float size = group.size * Mathf.Abs(group.transform.lossyScale.y) * SmallestScale;
        float height = Mathf.Max(group.GetLODs()[level - 1].screenRelativeTransitionHeight, 0.0001f);
        return size / (2f * Mathf.Tan(CameraFieldOfView * 0.5f * Mathf.Deg2Rad) * height) * LowestLodBias;
    }

    /// <summary>Stops the LOD levels that only show beyond any shadow from casting. Returns how many renderers that changed.</summary>
    public static int TrimShadows(GameObject root)
    {
        if (!root.TryGetComponent(out LODGroup group))
        {
            return 0;
        }

        LOD[] lods = group.GetLODs();
        int changed = 0;
        for (int level = 1; level < lods.Length; level++)
        {
            if (LevelStartsAt(group, level) < LongestShadows * ShadowMargin)
            {
                continue;
            }

            foreach (Renderer renderer in lods[level].renderers)
            {
                if (renderer != null && renderer.shadowCastingMode == ShadowCastingMode.On)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    changed++;
                }
            }
        }

        return changed;
    }

    /// <summary>Takes the NavMeshObstacle off a prop nothing can mine, fell, break or use: the bake then cuts round its collider.</summary>
    public static bool DropStaticObstacle(GameObject root)
    {
        if (!root.TryGetComponent(out NavMeshObstacle obstacle) || obstacle.carving || root.GetComponent<Collider>() == null
            || root.GetComponent<ResourceNode>() != null || root.GetComponent<ChoppableTree>() != null || root.GetComponent<Interactable>() != null
            || root.GetComponent<Rigidbody>() != null)
        {
            return false;
        }

        Object.DestroyImmediate(obstacle);
        return true;
    }
}
