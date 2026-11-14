#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Rain and snow on the Haven's own things (`unity command script run --file AgentScripts/BuildWeatherSurfaces.cs --entry
/// BuildWeatherSurfaces.Apply`): the plain URP Lit materials in Materials/Haven (building tints, props, crops) move to the
/// see-through Lit copy, which has the same properties and keywords plus the weather (SeasonSnow.hlsl, SurfaceWetness.hlsl),
/// with "Weather Settles On It" on, so they soak in the rain, gather snow in winter and dissolve between the camera and the
/// player like the rest of the buildings. Glowing materials (embers, lantern glass, water, frost, blight) are left alone.
/// Re-runnable.
/// </summary>
public static class BuildWeatherSurfaces
{
    private const string Folder = "Assets/_Project/Materials/Haven";
    private static readonly HashSet<string> Left = new HashSet<string> { "Base Ember", "Lantern Glow", "Base Water", "Base Frost", "Blight" };

    public static string Apply()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Shader weathered = Shader.Find("Safe Haven/Lit (See-Through)");
        var moved = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Folder }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null || material.shader != lit || Left.Contains(material.name))
            {
                continue;
            }

            material.shader = weathered;
            material.SetFloat("_SeasonSnow", 1f);
            material.EnableKeyword("_SEASON_SNOW");
            EditorUtility.SetDirty(material);
            moved.Add(material.name);
        }

        AssetDatabase.SaveAssets();
        return $"{moved.Count} materials weathered";
    }
}
