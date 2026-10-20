using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The Connections panel of the Content Editor: what the selected asset points at ("Uses") and what points at it
/// ("Used by"), across data, prefabs and scenes. Each entry opens on its own page, so a designer can walk from an item to
/// the recipes and monsters involving it and back. "Used by" scans the project, so it runs when asked and is remembered
/// until the selection or the project changes.
/// </summary>
public sealed class ContentConnections
{
    private Object usedByFor;
    private readonly List<Object> usedBy = new List<Object>();
    private Object usesFor;
    private readonly List<Object> uses = new List<Object>();

    public void Clear()
    {
        usedByFor = null;
        usesFor = null;
    }

    public void Draw(IContentEditorHost host, Object asset)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (usesFor != asset)
        {
            usesFor = asset;
            uses.Clear();
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
            {
                Object found = Relevant(host, dependency, path);
                if (found != null)
                {
                    uses.Add(found);
                }
            }
        }

        ContentUtility.BeginPanel("Connections", "What this uses and what uses it. Click an entry to open it.");
        EditorGUILayout.LabelField("Uses", EditorStyles.miniBoldLabel);
        DrawEntries(host, uses, "Nothing else.");

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Used by", EditorStyles.miniBoldLabel);
            if (GUILayout.Button(new GUIContent(usedByFor == asset ? "Search Again" : "Find", "Searches every data asset, prefab and scene."),
                    EditorStyles.miniButton, GUILayout.Width(90f)))
            {
                FindUsers(host, asset, path);
            }
        }

        if (usedByFor == asset)
        {
            DrawEntries(host, usedBy, "Nothing uses it. Deleting it wouldn't break anything.");
        }
        else
        {
            EditorGUILayout.LabelField("Press Find to list the recipes, monsters, prefabs and scenes that use this.", ContentUtility.WrappedMiniLabel);
        }

        ContentUtility.EndPanel();
    }

    private void FindUsers(IContentEditorHost host, Object asset, string path)
    {
        usedByFor = asset;
        usedBy.Clear();
        foreach (string user in ContentUtility.FindReferences(asset, null))
        {
            Object found = Relevant(host, user, path);
            if (found != null)
            {
                usedBy.Add(found);
            }
        }

        usedBy.Sort((a, b) => string.Compare(Describe(host, a), Describe(host, b), System.StringComparison.OrdinalIgnoreCase));
    }

    // Content, prefabs and scenes are worth listing; scripts, textures, materials and meshes are noise here.
    private static Object Relevant(IContentEditorHost host, string candidate, string self)
    {
        if (candidate == self || !candidate.StartsWith(ContentUtility.SearchRoot))
        {
            return null;
        }

        Object found = AssetDatabase.LoadMainAssetAtPath(candidate);
        if (found is SceneAsset || found is GameObject)
        {
            return found;
        }

        return found != null && host.FindSectionFor(found) != null ? found : null;
    }

    private static void DrawEntries(IContentEditorHost host, List<Object> entries, string none)
    {
        if (entries.Count == 0)
        {
            EditorGUILayout.LabelField(none, ContentUtility.WrappedMiniLabel);
            return;
        }

        foreach (Object entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            if (GUILayout.Button(Describe(host, entry), EditorStyles.linkLabel))
            {
                ContentSection section = host.FindSectionFor(entry);
                if (section != null)
                {
                    host.Select(section, entry);
                }
                else
                {
                    ContentUtility.Reveal(entry);
                }

                GUIUtility.ExitGUI();
            }
        }
    }

    private static string Describe(IContentEditorHost host, Object entry)
    {
        if (entry is SceneAsset)
        {
            return "Scene: " + entry.name;
        }

        ContentSection section = host.FindSectionFor(entry);
        return section != null ? $"{section.Noun}: {section.Label(entry)}" : entry.name;
    }
}
