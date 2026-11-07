using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Texture streaming (`unity command script run --timeout 2400 -- --file AgentScripts/StreamLargeTextures.cs --entry StreamLargeTextures.Apply`):
/// the large textures the game uses (above 2K, reached from the build scenes and Resources; mostly the SeedMesh forest
/// and VIVID Arts mountain-lake packs at 4K and 8K) get Streaming Mipmaps, and both quality levels stream textures with
/// a generous budget. Unity then keeps only the mip levels the camera needs: from the top-down camera an 8K rock
/// shows its 1K-2K mips, so memory falls (Editor and game) and nothing looks different. Reimports what it changes, so
/// a first run takes a while. Re-runnable: textures already streaming are skipped.
/// </summary>
public static class StreamLargeTextures
{
    private const int LargerThan = 2048;
    // Far more than the visible mips need; below it Unity may drop at most MaxReduction levels from far textures.
    private const int PcBudgetMb = 2048;
    private const int LowBudgetMb = 1024;
    private const int MaxReduction = 2;

    public static string Apply()
    {
        List<string> textures = LargeUsedTextures();
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in textures)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.streamingMipmaps = true;
                importer.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        string quality = Quality();
        AssetDatabase.SaveAssets();
        return $"{textures.Count} large textures now stream; {quality}";
    }

    /// <summary>Imported textures above 2K that the build scenes or Resources reach and that don't stream yet.</summary>
    public static List<string> LargeUsedTextures()
    {
        var roots = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                roots.Add(scene.path);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/_Project/Resources" }))
        {
            roots.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        var found = new List<string>();
        foreach (string path in AssetDatabase.GetDependencies(roots.ToArray(), true))
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.streamingMipmaps || !importer.mipmapEnabled ||
                importer.textureType == TextureImporterType.Sprite || importer.textureType == TextureImporterType.GUI)
            {
                continue;
            }

            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width > LargerThan || height > LargerThan)
            {
                found.Add(path);
            }
        }

        return found;
    }

    private static string Quality()
    {
        Object asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
        var settings = new SerializedObject(asset);
        SerializedProperty levels = settings.FindProperty("m_QualitySettings");
        var report = new List<string>();
        for (int i = 0; i < levels.arraySize; i++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(i);
            string name = level.FindPropertyRelative("name").stringValue;
            int budget = name == "Mobile" ? LowBudgetMb : PcBudgetMb;
            level.FindPropertyRelative("streamingMipmapsActive").boolValue = true;
            level.FindPropertyRelative("streamingMipmapsMemoryBudget").floatValue = budget;
            level.FindPropertyRelative("streamingMipmapsMaxLevelReduction").intValue = MaxReduction;
            level.FindPropertyRelative("streamingMipmapsAddAllCameras").boolValue = true;
            report.Add($"{name} streams ({budget} MB)");
        }

        settings.ApplyModifiedPropertiesWithoutUndo();
        return string.Join(", ", report);
    }
}
