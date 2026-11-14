#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;

/// <summary>
/// The minimap's material and Resources/MinimapSettings. Run with
/// unity command run_script --file AgentScripts/BuildMinimap.cs --entry BuildMinimap.All
/// </summary>
public static class BuildMinimap
{
    private const string MaterialPath = "Assets/_Project/Materials/Minimap.mat";
    private const string SettingsPath = "Assets/_Project/Resources/MinimapSettings.asset";

    public static string All()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/UI/Minimap"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        var settings = AssetDatabase.LoadAssetAtPath<MinimapSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<MinimapSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("material").objectReferenceValue = material;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return $"shader {material.shader.name}";
    }
}
