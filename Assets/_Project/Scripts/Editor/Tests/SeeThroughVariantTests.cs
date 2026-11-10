using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What SeeThroughVariantStripper takes for granted stays true: no cross-fading LOD group draws a see-through material,
/// and no light in a prefab or build scene casts hard shadows. Read from the asset text, so nothing is loaded.
/// </summary>
public sealed class SeeThroughVariantTests
{
    private static readonly Regex CrossFading = new Regex(@"m_FadeMode: [12]\b");
    private static readonly Regex HardShadows = new Regex(@"m_Shadows:\s*\r?\n\s*m_Type: 1\b");

    [Test]
    public void NoCrossFadingLodGroup_UsesASeeThroughMaterial()
    {
        Shader shader = Shader.Find(SeeThroughVariantStripper.ShaderName);
        Assert.IsNotNull(shader, SeeThroughVariantStripper.ShaderName);
        string shaderGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(shader));
        HashSet<string> materials = AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
            .Where(guid => AssetDatabase.GUIDToAssetPath(guid) is string path && path.EndsWith(".mat")
                           && File.ReadAllText(path).Contains("guid: " + shaderGuid))
            .ToHashSet();
        Assert.Greater(materials.Count, 0);

        var offenders = new List<string>();
        foreach (string path in Sources())
        {
            string text = File.ReadAllText(path);
            if (CrossFading.IsMatch(text) && materials.Any(text.Contains))
            {
                offenders.Add(path);
            }
        }

        Assert.IsEmpty(offenders, "a cross-fading LOD group with a see-through material: SeeThroughVariantStripper drops LOD_FADE_CROSSFADE");
    }

    [Test]
    public void NoLight_CastsHardShadows()
    {
        List<string> offenders = Sources().Where(path => HardShadows.IsMatch(File.ReadAllText(path))).ToList();
        Assert.IsEmpty(offenders, "a hard-shadow light: SeeThroughVariantStripper drops hard-shadow variants (make it soft)");
    }

    private static IEnumerable<string> Sources()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            yield return AssetDatabase.GUIDToAssetPath(guid);
        }

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                yield return scene.path;
            }
        }
    }
}
