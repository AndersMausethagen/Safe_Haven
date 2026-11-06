using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Puts materials on the See-Through shaders, so they dissolve around the player when they stand between the camera
/// and the player (SeeThrough). URP Lit and Unlit materials swap to the project's See-Through copies, which keep every
/// property and keyword, so they look the same. Tree Wind and Layered Lit materials already dissolve.
/// <list type="bullet">
/// <item>Safe Haven › Setup › Rendering › Make Materials See-Through › Selected: the materials (or the materials of the prefabs
/// and objects) selected.</item>
/// <item>… › Environment: every URP Lit/Unlit material in the environment folders (trees, rocks, resource nodes,
/// structures, forest kit), not characters, creatures, effects or UI.</item>
/// <item>… › Revert Selected: back to URP Lit/Unlit.</item>
/// </list>
/// </summary>
public static class SeeThroughMaterials
{
    private const string Menu = "Safe Haven/Setup/Rendering/Make Materials See-Through/";
    private const string Lit = "Universal Render Pipeline/Lit";
    private const string Unlit = "Universal Render Pipeline/Unlit";
    private const string SeeLit = "Safe Haven/Lit (See-Through)";
    private const string SeeUnlit = "Safe Haven/Unlit (See-Through)";

    /// <summary>Folders whose URP Lit/Unlit materials are environment (things the player walks among).</summary>
    public static readonly string[] EnvironmentFolders =
    {
        "Assets/_Project/Art/Forest",
        "Assets/_Project/Environment/ResourceNodes",
        "Assets/_Project/Environment/Stations",
        "Assets/_Project/Materials/Haven",
        "Assets/_Project/Materials/ResourceNodes",
        "Assets/_Project/Materials/Stations",
        "Assets/_Project/Prefabs/Structures",
    };

    [MenuItem(Menu + "Selected", false, 0)]
    private static void Selected()
    {
        Report(Convert(SelectedMaterials(), true), "selection");
    }

    [MenuItem(Menu + "Environment", false, 1)]
    private static void Environment()
    {
        Report(Convert(EnvironmentMaterials(), true), "environment folders");
    }

    [MenuItem(Menu + "Revert Selected", false, 20)]
    private static void Revert()
    {
        Report(Convert(SelectedMaterials(), false), "selection (reverted)");
    }

    /// <summary>Swaps the materials' shaders; returns how many changed.</summary>
    public static int Convert(IEnumerable<Material> materials, bool seeThrough)
    {
        Shader lit = Shader.Find(seeThrough ? SeeLit : Lit);
        Shader unlit = Shader.Find(seeThrough ? SeeUnlit : Unlit);
        string fromLit = seeThrough ? Lit : SeeLit;
        string fromUnlit = seeThrough ? Unlit : SeeUnlit;
        int changed = 0;
        foreach (Material material in materials)
        {
            if (material == null || !AssetDatabase.Contains(material))
            {
                continue;
            }

            Shader next = material.shader.name == fromLit ? lit : material.shader.name == fromUnlit ? unlit : null;
            if (next == null)
            {
                continue;
            }

            Undo.RecordObject(material, "Make Materials See-Through");
            // Same properties and keywords: only the shader changes, and the render queue the material chose stays.
            int queue = material.renderQueue;
            material.shader = next;
            material.renderQueue = queue;
            EditorUtility.SetDirty(material);
            changed++;
        }

        AssetDatabase.SaveAssets();
        return changed;
    }

    public static List<Material> EnvironmentMaterials()
    {
        var found = new List<Material>();
        var folders = new List<string>();
        foreach (string folder in EnvironmentFolders)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                folders.Add(folder);
            }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Material", folders.ToArray()))
        {
            found.Add(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        // Materials used by those folders' prefabs live elsewhere too (a resource node using a shared prop material).
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            AddRendererMaterials(prefab, found);
        }

        return found;
    }

    private static List<Material> SelectedMaterials()
    {
        var found = new List<Material>();
        foreach (Object selected in Selection.objects)
        {
            if (selected is Material material)
            {
                found.Add(material);
            }
            else if (selected is GameObject go)
            {
                AddRendererMaterials(go, found);
            }
        }

        return found;
    }

    private static void AddRendererMaterials(GameObject root, List<Material> found)
    {
        if (root == null)
        {
            return;
        }

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer)
            {
                continue;
            }

            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && !found.Contains(material))
                {
                    found.Add(material);
                }
            }
        }
    }

    private static void Report(int changed, string what)
    {
        string message = changed == 0
            ? $"No URP Lit/Unlit materials to change in the {what}."
            : $"{changed} material(s) in the {what} now use the See-Through shaders.";
        Debug.Log(message);
        EditorUtility.DisplayDialog("See-Through Materials", message, "OK");
    }
}
