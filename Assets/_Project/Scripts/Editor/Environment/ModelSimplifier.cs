using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Lightens heavy models for the high top-down camera, where a 100k-triangle tree looks the same as a 5k one
/// (Local Generator trees reach that, and a forest of them is what costs the frame rate). Select a model prefab and open
/// Safe Haven › Setup › Art Pipeline › Simplify Model Meshes: each mesh gets a detail level from Unity's mesh LOD generator,
/// baked into "Simplified/&lt;prefab&gt;_&lt;mesh&gt;.asset" beside the prefab, which the prefab's MeshFilters (and
/// MeshColliders using the same mesh) then use. It is one lighter mesh, not an LOD chain.
/// <para>
/// The copy keeps the source's vertex attributes (wind weights, UVs, normals) and drops triangles and the vertices no
/// triangle uses any more (it once kept them all, so every level still drew the source's whole vertex buffer), so materials and shaders
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

    [MenuItem("Safe Haven/Setup/Art Pipeline/Simplify Model Meshes", false, 200)]
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
        return Compact(result);
    }

    /// <summary>Drops the vertices no triangle uses any more, keeping every attribute of the rest (wind weights, UVs,
    /// colours). Dropping only triangles left each level the source's whole vertex buffer: the Lichen Boulder drew 56k
    /// vertices at every level, GreyStone's rock 90k for 7.6k triangles. Skinned and blend-shape meshes are left as
    /// they are.</summary>
    public static Mesh Compact(Mesh mesh)
    {
        if (mesh.blendShapeCount > 0 || mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.BlendIndices)) return mesh;
        int count = mesh.vertexCount;
        // Read through MeshData: a copy of an imported mesh that isn't Read/Write gives empty arrays from mesh.vertices
        // and GetIndices (the Lichen Boulder's levels came out empty that way), but its MeshData is readable here.
        var subIndices = new List<int[]>();
        var topologies = new MeshTopology[mesh.subMeshCount];
        Vector3[] positions, normals;
        Vector4[] tangents;
        Color[] colours;
        var uvs = new List<Vector4[]>();
        var uvDimensions = new int[8];
        using (Mesh.MeshDataArray array = Mesh.AcquireReadOnlyMeshData(mesh))
        {
            Mesh.MeshData data = array[0];
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var descriptor = data.GetSubMesh(s);
                topologies[s] = descriptor.topology;
                using (var indices = new Unity.Collections.NativeArray<int>(descriptor.indexCount, Unity.Collections.Allocator.Temp))
                {
                    data.GetIndices(indices, s, true);
                    subIndices.Add(indices.ToArray());
                }
            }

            T[] Read<T>(System.Action<Unity.Collections.NativeArray<T>> get) where T : struct
            {
                using (var values = new Unity.Collections.NativeArray<T>(count, Unity.Collections.Allocator.Temp))
                {
                    get(values);
                    return values.ToArray();
                }
            }

            positions = Read<Vector3>(data.GetVertices);
            normals = data.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal) ? Read<Vector3>(data.GetNormals) : null;
            tangents = data.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent) ? Read<Vector4>(data.GetTangents) : null;
            colours = data.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color) ? Read<Color>(data.GetColors) : null;
            for (int channel = 0; channel < 8; channel++)
            {
                var attribute = (UnityEngine.Rendering.VertexAttribute)((int)UnityEngine.Rendering.VertexAttribute.TexCoord0 + channel);
                if (!data.HasVertexAttribute(attribute))
                {
                    uvs.Add(null);
                    continue;
                }

                uvDimensions[channel] = data.GetVertexAttributeDimension(attribute);
                int c = channel;
                uvs.Add(Read<Vector4>(values => data.GetUVs(c, values)));
            }
        }

        int indexTotal = subIndices.Sum(i => i.Length);
        if (indexTotal == 0 || positions.Length != count) return mesh; // nothing read: never write an empty mesh

        var remap = new int[count];
        for (int i = 0; i < count; i++) remap[i] = -1;
        var used = new List<int>();
        foreach (int[] indices in subIndices)
        {
            for (int i = 0; i < indices.Length; i++)
            {
                int v = indices[i];
                if (remap[v] < 0)
                {
                    remap[v] = used.Count;
                    used.Add(v);
                }

                indices[i] = remap[v];
            }
        }

        if (used.Count == count) return mesh;

        T[] Pick<T>(T[] all) => all == null || all.Length != count ? null : used.Select(v => all[v]).ToArray();
        Bounds bounds = mesh.bounds;
        string name = mesh.name;
        mesh.Clear();
        mesh.name = name;
        mesh.indexFormat = used.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = Pick(positions);
        if (Pick(normals) is Vector3[] n) mesh.normals = n;
        if (Pick(tangents) is Vector4[] t) mesh.tangents = t;
        if (Pick(colours) is Color[] c2) mesh.colors = c2;
        for (int channel = 0; channel < 8; channel++)
        {
            if (uvDimensions[channel] == 0) continue;
            Vector4[] all = uvs[channel];
            switch (uvDimensions[channel])
            {
                case 2: mesh.SetUVs(channel, used.Select(v => (Vector2)all[v]).ToList()); break;
                case 3: mesh.SetUVs(channel, used.Select(v => (Vector3)all[v]).ToList()); break;
                default: mesh.SetUVs(channel, used.Select(v => all[v]).ToList()); break;
            }
        }

        mesh.subMeshCount = subIndices.Count;
        for (int s = 0; s < subIndices.Count; s++) mesh.SetIndices(subIndices[s], topologies[s], s, false);
        mesh.bounds = bounds;
        return mesh;
    }

    /// <summary>Bakes every simplified copy in the project again from its source at the level it was made at (found by
    /// its triangle count), in place, so references keep its GUID. Used once to compact the copies made before
    /// <see cref="Compact"/>; harmless to run again.</summary>
    public static string RebakeAll(IReadOnlyDictionary<string, int> knownLevels = null)
    {
        var report = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.Contains("/" + FolderName + "/")) continue;
            var copy = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh source = copy != null ? SourceOf(copy) : null;
            if (source == null || source == copy) { report.Add($"{path}: no source"); continue; }
            using var readable = new ReadableSource(source);
            source = readable.Mesh;
            Mesh levels = GenerateLevels(source);
            try
            {
                int level = -1;
                MeshLodRange[] ranges = levels.GetLods(0);
                for (int i = 0; i < ranges.Length; i++)
                    if (ranges[i].indexCount == copy.GetIndexCount(0)) { level = i; break; }
                if (level <= 0 && knownLevels != null && knownLevels.TryGetValue(Path.GetFileNameWithoutExtension(path), out int known)) level = known;
                if (level <= 0) { report.Add($"{path}: level not found ({copy.GetIndexCount(0) / 3} triangles)"); continue; }
                int before = copy.vertexCount;
                Mesh baked = Bake(source, levels, level);
                if (baked.GetIndexCount(0) == 0) { DestroyImmediate(baked); report.Add($"{path}: baked empty, left as it was"); continue; }
                EditorUtility.CopySerialized(baked, copy);
                DestroyImmediate(baked);
                MarkNotReadable(copy);
                EditorUtility.SetDirty(copy);
                report.Add($"{Path.GetFileNameWithoutExtension(path)}: level {level}, {copy.GetIndexCount(0) / 3} triangles, vertices {before} -> {copy.vertexCount}");
            }
            finally
            {
                DestroyImmediate(levels);
            }
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    /// <summary>While open, the source's model imports Read/Write, so a copy of it carries its data: a copy of a mesh
    /// that isn't readable has nothing on the CPU, and saving it wrote an empty mesh (the Lichen Boulder's levels).
    /// Disposing puts the import setting back.</summary>
    private sealed class ReadableSource : System.IDisposable
    {
        private readonly ModelImporter importer;
        public Mesh Mesh { get; }

        public ReadableSource(Mesh source)
        {
            Mesh = source;
            importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as ModelImporter;
            if (source.isReadable || importer == null || importer.isReadable)
            {
                importer = null;
                return;
            }

            GlobalObjectId id = GlobalObjectId.GetGlobalObjectIdSlow(source);
            importer.isReadable = true;
            importer.SaveAndReimport();
            Mesh = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Mesh ?? source;
        }

        public void Dispose()
        {
            if (importer == null) return;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }

    // Saved copies keep no CPU copy in the game (twice the memory otherwise).
    private static void MarkNotReadable(Mesh asset)
    {
        var serialized = new SerializedObject(asset);
        SerializedProperty readable = serialized.FindProperty("m_IsReadable");
        if (readable != null && readable.boolValue)
        {
            readable.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>A lighter copy of <paramref name="source"/> with about <paramref name="share"/> of its triangles (the
    /// generated level nearest it), vertices compacted: an LOD level for a model. Unity's generator reduces meshes
    /// Blender's collapse can't (generated meshes full of tiny holes: the mushroom patch's LOD2 kept 97% of LOD0).
    /// A source that isn't Read/Write is made so while it is read.</summary>
    public static Mesh Lighter(Mesh source, float share)
    {
        using var readable = new ReadableSource(source);
        Mesh levels = GenerateLevels(readable.Mesh);
        try
        {
            MeshLodRange[] ranges = levels.GetLods(0);
            float want = readable.Mesh.GetIndexCount(0) * share;
            int best = 0;
            for (int i = 1; i < ranges.Length; i++)
                if (Mathf.Abs(ranges[i].indexCount - want) < Mathf.Abs(ranges[best].indexCount - want)) best = i;
            Mesh baked = Bake(readable.Mesh, levels, best);
            MarkNotReadable(baked);
            return baked;
        }
        finally
        {
            DestroyImmediate(levels);
        }
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

            using var readable = new ReadableSource(pair.Key);
            Mesh levels = GenerateLevels(readable.Mesh);
            try
            {
                targets[pair.Key] = Save(prefabPath, readable.Mesh, Bake(readable.Mesh, levels, pair.Value));
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

        MarkNotReadable(baked);
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
