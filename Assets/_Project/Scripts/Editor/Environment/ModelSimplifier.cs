using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Lightens heavy models for the high top-down camera, where a 100k-triangle tree looks the same as a 5k one
/// (Local Generator trees reach that, and a forest of them is what costs the frame rate). Select a model prefab and open
/// Safe Haven › Art Pipeline › Simplify Model Meshes: each mesh gets a detail level from Unity's mesh LOD generator,
/// baked into "Simplified/&lt;prefab&gt;_&lt;mesh&gt;.asset" beside the prefab, which the prefab's MeshFilters (and
/// MeshColliders using the same mesh) then use. It is one lighter mesh, not an LOD chain.
/// <para>
/// The copy keeps the source's vertices (wind weights, UVs, normals) and only drops triangles, so materials and shaders
/// are unaffected. The source model is never changed; level 0 (Restore) puts the source mesh back. Re-baking keeps the
/// copy's asset, so scene references stay. A "_Wind" prefab variant keeps the override when its GLB reimports.
/// </para>
/// </summary>
public sealed class ModelSimplifier : EditorWindow
{
    public const string FolderName = "Simplified";
    private const int MaxLevels = 9;

    private sealed class Entry
    {
        public Mesh Source;
        public Mesh Levels;
        public int[] Triangles;
        public int Level;
        public int Users;
    }

    private GameObject prefab;
    private readonly List<Entry> entries = new List<Entry>();
    private Vector2 scroll;

    [MenuItem("Safe Haven/Art Pipeline/Simplify Model Meshes")]
    private static void Open()
    {
        var window = GetWindow<ModelSimplifier>("Simplify Model");
        if (Selection.activeGameObject != null && EditorUtility.IsPersistent(Selection.activeGameObject))
            window.Load(Selection.activeGameObject);
    }

    /// <summary>A copy of <paramref name="source"/> with only detail level <paramref name="level"/>'s triangles (0 is
    /// the source's own). <paramref name="levels"/> is a copy of the source that MeshLodUtility.GenerateMeshLods has
    /// run on (see <see cref="GenerateLevels"/>). Vertices, attributes and bounds are the source's.</summary>
    public static Mesh Bake(Mesh source, Mesh levels, int level)
    {
        Mesh result = Instantiate(source);
        result.name = source.name;
        // The generator reorders even level 0's triangles; the source's own order is kept.
        if (level <= 0) return result;
        using (Mesh.MeshDataArray data = Mesh.AcquireReadOnlyMeshData(levels))
        {
            Mesh.MeshData buffers = data[0];
            bool wide = levels.indexFormat == UnityEngine.Rendering.IndexFormat.UInt32;
            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                MeshLodRange[] ranges = levels.GetLods(subMesh);
                MeshLodRange range = ranges[Mathf.Clamp(level, 0, ranges.Length - 1)];
                var descriptor = levels.GetSubMesh(subMesh);
                // Level 0 starts where the sub-mesh starts when the ranges count from the start of the index buffer.
                int start = (int)range.indexStart + (ranges[0].indexStart == descriptor.indexStart ? 0 : descriptor.indexStart);
                var indices = new int[range.indexCount];
                if (wide)
                {
                    var all = buffers.GetIndexData<uint>();
                    for (int i = 0; i < indices.Length; i++) indices[i] = (int)all[start + i];
                }
                else
                {
                    var all = buffers.GetIndexData<ushort>();
                    for (int i = 0; i < indices.Length; i++) indices[i] = all[start + i];
                }

                result.SetIndices(indices, descriptor.topology, subMesh, false, descriptor.baseVertex);
            }
        }

        result.bounds = source.bounds;
        return result;
    }

    /// <summary>A temporary copy of <paramref name="source"/> with Unity's detail levels generated; destroy it after use.</summary>
    public static Mesh GenerateLevels(Mesh source)
    {
        Mesh levels = Instantiate(source);
        levels.hideFlags = HideFlags.HideAndDontSave;
        MeshLodUtility.GenerateMeshLods(levels, MaxLevels);
        return levels;
    }

    /// <summary>The source of a simplified copy made here, or the mesh itself.</summary>
    public static Mesh SourceOf(Mesh mesh)
    {
        string path = AssetDatabase.GetAssetPath(mesh);
        if (!path.Contains("/" + FolderName + "/")) return mesh;
        AssetImporter importer = AssetImporter.GetAtPath(path);
        if (importer != null && GlobalObjectId.TryParse(importer.userData, out GlobalObjectId id) &&
            GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is Mesh source)
            return source;
        return mesh;
    }

    private void OnDisable() => Clear();

    private void Clear()
    {
        foreach (Entry entry in entries)
            if (entry.Levels != null) DestroyImmediate(entry.Levels);
        entries.Clear();
    }

    private void Load(GameObject model)
    {
        Clear();
        prefab = model;
        if (prefab == null) return;

        var bySource = new Dictionary<Mesh, Entry>();
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Mesh source = SourceOf(filter.sharedMesh);
            if (bySource.TryGetValue(source, out Entry known))
            {
                known.Users++;
                continue;
            }

            if (!source.isReadable)
            {
                Debug.LogWarning($"Simplify Model: {source.name} is not readable; turn on Read/Write in its import settings.", source);
                continue;
            }

            var entry = new Entry { Source = source, Levels = GenerateLevels(source), Users = 1 };
            entry.Triangles = new int[entry.Levels.lodCount];
            for (int level = 0; level < entry.Triangles.Length; level++)
                for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
                    entry.Triangles[level] += (int)entry.Levels.GetLods(subMesh)[Mathf.Min(level, entry.Levels.GetLods(subMesh).Length - 1)].indexCount / 3;

            int current = Triangles(filter.sharedMesh);
            entry.Level = System.Array.IndexOf(entry.Triangles, current);
            if (entry.Level < 0) entry.Level = 0;
            bySource.Add(source, entry);
            entries.Add(entry);
        }
    }

    private void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        var picked = (GameObject)EditorGUILayout.ObjectField("Model Prefab", prefab, typeof(GameObject), false);
        if (EditorGUI.EndChangeCheck()) Load(picked);

        if (prefab == null)
        {
            EditorGUILayout.HelpBox("Pick a model prefab (for a generated tree, its \"_Wind\" variant).", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        int before = 0, after = 0;
        foreach (Entry entry in entries)
        {
            EditorGUILayout.LabelField(entry.Users > 1 ? $"{entry.Source.name} (×{entry.Users})" : entry.Source.name, EditorStyles.boldLabel);
            entry.Level = EditorGUILayout.IntSlider("Detail Level", entry.Level, 0, entry.Triangles.Length - 1);
            EditorGUILayout.LabelField(" ", $"{entry.Triangles[entry.Level]:N0} of {entry.Triangles[0]:N0} triangles");
            before += entry.Triangles[0] * entry.Users;
            after += entry.Triangles[entry.Level] * entry.Users;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox($"Per model: {after:N0} triangles (source {before:N0}). Leaf cards fall apart sooner " +
                                "than trunks and rocks; look at the result from the game camera.", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply")) Apply();
            if (GUILayout.Button("Restore Source Meshes"))
            {
                foreach (Entry entry in entries) entry.Level = 0;
                Apply();
            }
        }
    }

    private void Apply()
    {
        var levels = new Dictionary<Mesh, int>();
        foreach (Entry entry in entries) levels[entry.Source] = entry.Level;
        Apply(AssetDatabase.GetAssetPath(prefab), levels);
        Debug.Log($"Simplify Model: {prefab.name} now uses {entries.Count} mesh(es) at the chosen detail levels.", prefab);
    }

    /// <summary>Points the prefab's MeshFilters at the chosen detail level of each source mesh (0 restores the source);
    /// meshes not named keep theirs. Usable from scripts and the CLI as well as the window.</summary>
    public static void Apply(string prefabPath, IReadOnlyDictionary<Mesh, int> levelBySource)
    {
        var targets = new Dictionary<Mesh, Mesh>();
        foreach (KeyValuePair<Mesh, int> pair in levelBySource)
        {
            if (pair.Value <= 0)
            {
                targets[pair.Key] = pair.Key;
                continue;
            }

            Mesh levels = GenerateLevels(pair.Key);
            try
            {
                targets[pair.Key] = Save(prefabPath, pair.Key, Bake(pair.Key, levels, pair.Value));
            }
            finally
            {
                DestroyImmediate(levels);
            }
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            foreach (MeshFilter filter in contents.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || !targets.TryGetValue(SourceOf(filter.sharedMesh), out Mesh target)) continue;
                Mesh old = filter.sharedMesh;
                filter.sharedMesh = target;
                foreach (MeshCollider collider in filter.GetComponents<MeshCollider>())
                    if (collider.sharedMesh == old) collider.sharedMesh = target;
            }

            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
    }

    // Writes the baked level into the prefab's Simplified folder, reusing an existing asset so references keep its GUID.
    private static Mesh Save(string prefabPath, Mesh source, Mesh baked)
    {
        string folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/') + "/" + FolderName;
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), FolderName);

        string path = $"{folder}/{Path.GetFileNameWithoutExtension(prefabPath)}_{source.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(baked, existing);
            DestroyImmediate(baked);
            EditorUtility.SetDirty(existing);
            baked = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(baked, path);
        }

        AssetDatabase.SaveAssets();
        AssetImporter importer = AssetImporter.GetAtPath(path);
        string sourceId = GlobalObjectId.GetGlobalObjectIdSlow(source).ToString();
        if (importer.userData != sourceId)
        {
            importer.userData = sourceId;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Mesh>(path);
    }

    private static int Triangles(Mesh mesh)
    {
        int count = 0;
        for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++) count += (int)mesh.GetIndexCount(subMesh) / 3;
        return count;
    }
}
