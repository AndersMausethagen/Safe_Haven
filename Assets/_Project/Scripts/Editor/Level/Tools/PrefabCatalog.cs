using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>The prefabs the World Tools' Place tab offers, sorted into the groups a level designer looks for (rocks,
/// trees, forest floor, plants, resources, buildings, raid, creatures) and by set (the Forest Lake and Mossy Forest packs,
/// see <see cref="WorldLook"/>, or the original assets). Found by folder and name, so a new prefab in one of these
/// folders shows up by itself.</summary>
public static class PrefabCatalog
{
    public sealed class Entry
    {
        public GameObject Prefab;
        public string Name;
        public string Category;
        public string Set;
        public string Path;
    }

    public static readonly string[] Categories =
    {
        "Rocks & Cliffs", "Trees", "Forest Floor", "Plants & Grass", "Resources", "Buildings", "Raid", "Creatures", "Other",
    };

    public const string OriginalSet = "Original";

    public static readonly string[] Sets = WorldLook.All.Select(l => l.Name).Append(OriginalSet).ToArray();

    private static readonly string[] Folders =
    {
        "Assets/_Project/Prefabs", "Assets/_Project/Art/Forest/Prefabs", "Assets/_Project/Data/Prologue/Vegetation",
        "Assets/_Project/Models/Generated/Vegetation", "Assets/_Project/Art/Vegetation",
    };

    private static List<Entry> entries;

    public static IReadOnlyList<Entry> All => entries ??= Load();

    /// <summary>Forgets the list (a prefab was added or removed).</summary>
    public static void Refresh() => entries = null;

    /// <summary>The set a prefab comes from: a look's pack, or the original assets.</summary>
    public static string SetOf(string path)
    {
        string folder = path.Replace('\\', '/');
        WorldLook look = WorldLook.All.FirstOrDefault(l => folder.StartsWith(l.ArtFolder + "/"));
        return look != null ? look.Name : OriginalSet;
    }

    /// <summary>The group a prefab belongs in, from its folder and name.</summary>
    public static string CategoryOf(string path, string name)
    {
        string folder = path.Replace('\\', '/');
        string n = name.ToLowerInvariant();
        if (folder.Contains("/Art/Vegetation/"))
        {
            return PackCategory(folder, n);
        }

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

    // The converted packs are sorted by folder (Trees, Rocks and Logs, Undergrowth, Details, Plants, Rocks), then by name.
    private static string PackCategory(string folder, string n)
    {
        bool young = n.Contains("sapling") || n.Contains("seedling") || n.StartsWith("young");
        if (folder.Contains("/Trees/") || young)
        {
            return "Trees";
        }

        if (folder.Contains("/Details/") || folder.Contains("/Plants/") || n.Contains("shrub") || n.Contains("fern"))
        {
            return "Plants & Grass";
        }

        if (n.Contains("rock") || n.Contains("boulder") || n.Contains("stone") || n.Contains("crag") || n.Contains("cliff") ||
            n.Contains("slab") || n.Contains("outcrop") || n.Contains("scree") || n.Contains("pebble"))
        {
            return "Rocks & Cliffs";
        }

        return "Forest Floor";
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
                path.Contains("/Prefabs/Effects/") || path.Contains("/Prefabs/Items/") || path.Contains("/Art/Vegetation/") && path.Contains("/Effects/"))
            {
                continue; // not things a level is built from
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            string name = ObjectNames.NicifyVariableName(file.Replace("_Wind", "").Replace('_', ' '));
            string category = CategoryOf(path, file);
            // Pack saplings and young trees that can't be felled are planting, not trees.
            if (category == "Trees" && path.Contains("/Art/Vegetation/") && prefab.GetComponent<ChoppableTree>() == null && prefab.GetComponent<ResourceNode>() == null)
            {
                category = "Plants & Grass";
            }

            list.Add(new Entry { Prefab = prefab, Name = name, Category = category, Set = SetOf(path), Path = path });
        }

        return list.OrderBy(e => e.Name).ToList();
    }
}
