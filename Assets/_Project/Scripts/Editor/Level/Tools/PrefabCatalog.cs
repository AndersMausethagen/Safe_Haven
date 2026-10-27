using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>The prefabs the World Tools' Place tab offers, sorted into the groups a level designer looks for (rocks,
/// trees, forest floor, resources, buildings, raid, creatures). Found by folder and name, so a new prefab in one of these
/// folders shows up by itself.</summary>
public static class PrefabCatalog
{
    public sealed class Entry
    {
        public GameObject Prefab;
        public string Name;
        public string Category;
        public string Path;
    }

    public static readonly string[] Categories =
    {
        "Rocks & Cliffs", "Trees", "Forest Floor", "Resources", "Buildings", "Raid", "Creatures", "Other",
    };

    private static readonly string[] Folders =
    {
        "Assets/_Project/Prefabs", "Assets/_Project/Art/Forest/Prefabs", "Assets/_Project/Data/Prologue/Vegetation",
        "Assets/_Project/Models/Generated/Vegetation",
    };

    private static List<Entry> entries;

    public static IReadOnlyList<Entry> All => entries ??= Load();

    /// <summary>Forgets the list (a prefab was added or removed).</summary>
    public static void Refresh() => entries = null;

    /// <summary>The group a prefab belongs in, from its folder and name.</summary>
    public static string CategoryOf(string path, string name)
    {
        string folder = path.Replace('\\', '/');
        string n = name.ToLowerInvariant();
        if (folder.Contains("/Prefabs/Resources/"))
        {
            return "Resources";
        }

        if (folder.Contains("/Prefabs/Structures/"))
        {
            return "Buildings";
        }

        if (folder.Contains("/Prefabs/Raids/"))
        {
            return "Raid";
        }

        if (folder.Contains("/Prefabs/Enemies/"))
        {
            return "Creatures";
        }

        if (n.Contains("spruce") || n.Contains("pine") || n.Contains("oak") || n.Contains("tree") && !n.Contains("stump"))
        {
            return "Trees";
        }

        if (n.Contains("rock") || n.Contains("boulder") || n.Contains("stone") || n.Contains("crag") || n.Contains("cliff"))
        {
            return "Rocks & Cliffs";
        }

        if (n.Contains("stump") || n.Contains("log") || n.Contains("branch") || n.Contains("root") || n.Contains("fern") ||
            n.Contains("bush") || n.Contains("moss"))
        {
            return "Forest Floor";
        }

        return "Other";
    }

    private static List<Entry> Load()
    {
        var list = new List<Entry>();
        string[] folders = Folders.Where(AssetDatabase.IsValidFolder).ToArray();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            // Generated vegetation comes as a model, its parts and a wind variant: only the whole tree with wind.
            if (path.Contains("/Models/Generated/Vegetation/") && (!file.EndsWith("_Wind") || file.EndsWith("_Leaves_Wind") || file.EndsWith("_Trunk_Wind")))
            {
                continue;
            }

            if (path.Contains("/Prefabs/UI/") || path.Contains("/Prefabs/Systems/") || path.Contains("/Prefabs/Characters/") ||
                path.Contains("/Prefabs/Effects/") || path.Contains("/Prefabs/Items/"))
            {
                continue; // not things a level is built from
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            string name = ObjectNames.NicifyVariableName(file.Replace("_Wind", "").Replace('_', ' '));
            list.Add(new Entry { Prefab = prefab, Name = name, Category = CategoryOf(path, file), Path = path });
        }

        return list.OrderBy(e => e.Name).ToList();
    }
}
