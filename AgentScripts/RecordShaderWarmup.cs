using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Records the shader variants play uses, for ShaderWarmup. `Begin` (before entering Play) forgets what the Editor has
/// tracked so far; play through the moments that matter (day, dusk, night, rain, storm, fog, fires, a raid by day and by
/// night, the windows); `Save` writes Settings/Rendering/Warmup.shadervariants and puts it on RenderingSettings.
/// `unity command script run --file AgentScripts/RecordShaderWarmup.cs --entry RecordShaderWarmup.Begin` (then `.Save`).
/// </summary>
public static class RecordShaderWarmup
{
    private const string Path = "Assets/_Project/Settings/Rendering/Warmup.shadervariants";
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;

    public static string Begin()
    {
        typeof(ShaderUtil).GetMethod("ClearCurrentShaderVariantCollection", Hidden).Invoke(null, null);
        return "tracking from now: " + Count();
    }

    private static int AddFromMaterials(ShaderVariantCollection collection)
    {
        var so = new SerializedObject(collection);
        SerializedProperty shaders = so.FindProperty("m_Shaders");
        var recorded = new HashSet<Shader>();
        for (int i = 0; i < shaders.arraySize; i++)
        {
            if (shaders.GetArrayElementAtIndex(i).FindPropertyRelative("first").objectReferenceValue is Shader shader)
            {
                recorded.Add(shader);
            }
        }

        int added = 0;
        var seen = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            Shader shader = material != null ? material.shader : null;
            if (shader == null || recorded.Contains(shader) || !AssetDatabase.GetAssetPath(shader).StartsWith("Assets/_Project")
                || ShaderUtil.ShaderHasError(shader))
            {
                continue;
            }

            string[] keywords = material.shaderKeywords;
            foreach (string[] set in new[] { keywords, keywords.Concat(new[] { "FOG_EXP" }).ToArray() })
            {
                string key = shader.name + "|" + string.Join(" ", set.OrderBy(k => k));
                if (!seen.Add(key))
                {
                    continue;
                }

                foreach (UnityEngine.Rendering.PassType pass in new[] { UnityEngine.Rendering.PassType.ScriptableRenderPipeline,
                             UnityEngine.Rendering.PassType.ScriptableRenderPipelineDefaultUnlit })
                {
                    try
                    {
                        if (collection.Add(new ShaderVariantCollection.ShaderVariant(shader, pass, set)))
                        {
                            added++;
                        }
                    }
                    catch (System.ArgumentException)
                    {
                        // No such pass or keyword in this shader.
                    }
                }
            }
        }

        return added;
    }

    public static string Count()
    {
        object shaders = typeof(ShaderUtil).GetMethod("GetCurrentShaderVariantCollectionShaderCount", Hidden).Invoke(null, null);
        object variants = typeof(ShaderUtil).GetMethod("GetCurrentShaderVariantCollectionVariantCount", Hidden).Invoke(null, null);
        return $"{shaders} shaders, {variants} variants";
    }

    /// <summary>Adds the material-based variants to the saved collection without recording again.</summary>
    public static string AddMaterials()
    {
        var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(Path);
        int added = AddFromMaterials(collection);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssetIfDirty(collection);
        return $"added {added}; {collection.shaderCount} shaders, {collection.variantCount} variants";
    }

    public static string Save()
    {
        string count = Count();
        if (count.StartsWith("0 shaders"))
        {
            return "nothing recorded (saving clears the tracking): not overwriting " + Path;
        }

        typeof(ShaderUtil).GetMethod("SaveCurrentShaderVariantCollection", Hidden).Invoke(null, new object[] { Path });
        AssetDatabase.ImportAsset(Path);
        var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(Path);
        int added = AddFromMaterials(collection);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssetIfDirty(collection);
        var settings = AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        var so = new SerializedObject(settings);
        so.FindProperty("warmupVariants").objectReferenceValue = collection;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        return $"recorded {count}, added {added} from materials; {Path} holds {collection.shaderCount} shaders, {collection.variantCount} variants";
    }
}
