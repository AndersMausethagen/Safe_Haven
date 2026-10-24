using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Gives Local Generator trees (GLBs in Models/Generated/Vegetation with baked wind in uv1/uv2) the
/// Safe Haven/Tree Wind shader. One material per glTF material name lives in the tree folder's Materials/ and is
/// shared by every GLB of that tree (tree, _Trunk, _Leaves), so trunk and leaves always bend together.
/// glTFast's importer ignores external material remaps, so each GLB gets a "&lt;name&gt;_Wind" prefab variant that
/// swaps the materials; place that variant (or run the menu on existing scene instances).</summary>
public sealed class TreeWindMaterials : AssetPostprocessor
{
    public const string VegetationRoot = "Assets/_Project/Models/Generated/Vegetation/";
    public const string ShaderName = "Safe Haven/Tree Wind";
    public const string VariantSuffix = "_Wind";
    private static readonly HashSet<string> Pending = new HashSet<string>();

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (IsTreeGlb(path) && Pending.Add(path))
            {
                // Creating assets is not allowed inside an import callback.
                EditorApplication.delayCall += () =>
                {
                    Pending.Remove(path);
                    if (IsTreeGlb(path) && HasWindData(path)) Apply(path);
                };
            }
        }
    }

    [MenuItem("Safe Haven/Environment/Apply Tree Wind Materials")]
    public static void ApplyToSelection()
    {
        int assets = 0, instances = 0;
        foreach (Object selected in Selection.objects)
        {
            if (selected is GameObject go && !EditorUtility.IsPersistent(go))
            {
                instances += ApplyToInstance(go);
                continue;
            }

            string path = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(path)) continue;
            IEnumerable<string> glbs = AssetDatabase.IsValidFolder(path)
                ? Directory.GetFiles(path, "*.glb", SearchOption.AllDirectories)
                : new[] { path };
            foreach (string glb in glbs)
            {
                string assetPath = glb.Replace('\\', '/');
                if (assetPath.EndsWith(".glb") && HasWindData(assetPath) && Apply(assetPath) != null) assets++;
            }
        }

        Debug.Log($"Tree Wind: updated {assets} tree GLB(s) and {instances} scene renderer(s).");
    }

    [MenuItem("Safe Haven/Environment/Apply Tree Wind Materials", true)]
    private static bool CanApplyToSelection() => Selection.objects.Length > 0;

    public static bool IsTreeGlb(string path) =>
        path.StartsWith(VegetationRoot) && path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the GLB carries the generator's wind weights (a second and third UV set).</summary>
    public static bool HasWindData(string path)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Mesh mesh && mesh.HasVertexAttribute(VertexAttribute.TexCoord2)) return true;
        }

        return false;
    }

    /// <summary>Creates the tree folder's wind materials (existing ones keep their tuning) and the GLB's
    /// "_Wind" prefab variant. Returns the variant path, or null when the shader is missing.</summary>
    public static string Apply(string glbPath)
    {
        Dictionary<string, Material> materials = EnsureMaterials(glbPath);
        if (materials == null) return null;

        string variantPath = VariantPath(glbPath);
        if (File.Exists(variantPath))
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(variantPath);
            if (SwapMaterials(contents, materials, false) > 0) PrefabUtility.SaveAsPrefabAsset(contents, variantPath);
            PrefabUtility.UnloadPrefabContents(contents);
        }
        else
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            SwapMaterials(instance, materials, false);
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);
        }

        return variantPath;
    }

    /// <summary>Swaps the materials of an existing scene instance of a generated tree (undoable).</summary>
    public static int ApplyToInstance(GameObject instance)
    {
        Object source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(instance);
        string glbPath = source != null ? AssetDatabase.GetAssetPath(source) : null;
        if (string.IsNullOrEmpty(glbPath) || !glbPath.EndsWith(".glb") || !HasWindData(glbPath)) return 0;
        Dictionary<string, Material> materials = EnsureMaterials(glbPath);
        return materials == null ? 0 : SwapMaterials(instance, materials, true);
    }

    public static string VariantPath(string glbPath) =>
        glbPath.Substring(0, glbPath.Length - ".glb".Length) + VariantSuffix + ".prefab";

    private static Dictionary<string, Material> EnsureMaterials(string glbPath)
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            Debug.LogError($"Tree Wind: shader '{ShaderName}' not found.");
            return null;
        }

        string folder = Path.GetDirectoryName(glbPath).Replace('\\', '/');
        string materialsFolder = folder + "/Materials";
        if (!AssetDatabase.IsValidFolder(materialsFolder)) AssetDatabase.CreateFolder(folder, "Materials");

        float height = TreeHeight(folder);
        var result = new Dictionary<string, Material>();
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(glbPath))
        {
            if (!(asset is Material source) || result.ContainsKey(source.name)) continue;
            string materialPath = $"{materialsFolder}/{source.name}.mat";
            Material wind = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (wind == null)
            {
                wind = new Material(shader) { name = source.name };
                CopyGltf(source, wind);
                wind.SetFloat("_TreeHeight", height);
                AssetDatabase.CreateAsset(wind, materialPath);
            }
            else if (wind.shader == shader && !Mathf.Approximately(wind.GetFloat("_TreeHeight"), height))
            {
                wind.SetFloat("_TreeHeight", height);
                EditorUtility.SetDirty(wind);
            }

            result[source.name] = wind;
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static int SwapMaterials(GameObject root, Dictionary<string, Material> materials, bool undo)
    {
        int changed = 0;
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] shared = renderer.sharedMaterials;
            bool dirty = false;
            for (int i = 0; i < shared.Length; i++)
            {
                if (shared[i] != null && materials.TryGetValue(shared[i].name, out Material wind) && shared[i] != wind)
                {
                    shared[i] = wind;
                    dirty = true;
                }
            }

            if (!dirty) continue;
            if (undo) Undo.RecordObject(renderer, "Apply Tree Wind Materials");
            renderer.sharedMaterials = shared;
            if (undo) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            changed++;
        }

        return changed;
    }

    /// <summary>Copies glTFast's glTF-pbrMetallicRoughness values to a Tree Wind material.</summary>
    public static void CopyGltf(Material source, Material target)
    {
        Texture orm = source.GetTexture("metallicRoughnessTexture");
        Texture occlusion = source.GetTexture("occlusionTexture");
        target.SetTexture("_BaseMap", source.GetTexture("baseColorTexture"));
        target.SetColor("_BaseColor", source.GetColor("baseColorFactor"));
        target.SetTexture("_BumpMap", source.GetTexture("normalTexture"));
        target.SetFloat("_BumpScale", source.GetFloat("normalTexture_scale"));
        target.SetTexture("_ORMMap", orm != null ? orm : occlusion);
        // The generator packs AO into the metallic-roughness image (ORM); a separate AO map is not supported.
        target.SetFloat("_OcclusionStrength", occlusion != null && occlusion == orm ? source.GetFloat("occlusionTexture_strength") : 0f);
        target.SetFloat("_Roughness", source.GetFloat("roughnessFactor"));
        target.SetFloat("_Metallic", source.GetFloat("metallicFactor"));
        bool clip = source.IsKeywordEnabled("_ALPHATEST_ON");
        target.SetFloat("_AlphaClip", clip ? 1f : 0f);
        if (clip) target.EnableKeyword("_ALPHATEST_ON");
        else target.DisableKeyword("_ALPHATEST_ON");
        target.SetFloat("_Cutoff", source.GetFloat("alphaCutoff"));
        // Leaf cards (alpha clipped) light per vertex: their overlapping pixels are most of a tree's cost.
        target.SetFloat("_FastLeafLighting", clip ? 1f : 0f);
        if (clip) target.EnableKeyword("_FAST_LEAF_LIGHTING");
        else target.DisableKeyword("_FAST_LEAF_LIGHTING");
        target.SetFloat("_Cull", source.GetFloat("_Cull"));
        target.renderQueue = clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
        target.SetOverrideTag("RenderType", clip ? "TransparentCutout" : "Opaque");
        target.enableInstancing = true;
    }

    /// <summary>Top of the tallest mesh of any GLB in the folder (object units); the wind's sway scales with it.</summary>
    public static float TreeHeight(string folder)
    {
        float height = 0f;
        foreach (string file in Directory.GetFiles(folder, "*.glb"))
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(file.Replace('\\', '/')))
            {
                if (asset is Mesh mesh) height = Mathf.Max(height, mesh.bounds.max.y);
            }
        }

        return height > 0f ? height : 10f;
    }
}
