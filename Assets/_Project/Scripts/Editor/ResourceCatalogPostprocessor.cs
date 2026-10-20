using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the resource catalog, the recipe book and the save system's content database in step with the project: when a
/// Resource Definition, Resource Category, Recipe or Item is created or imported it is added, and entries whose asset
/// was deleted are removed. Anything watching the catalog (the resource HUD, even while playing) then refreshes by itself.
/// Safe Haven > Resources > Refresh Catalog runs the same scan by hand. With more than one resource catalog in the project
/// no resources are added automatically, since it can't tell which one is meant.
/// </summary>
public sealed class ResourceCatalogPostprocessor : AssetPostprocessor
{
    private static bool syncScheduled;

    [MenuItem("Safe Haven/Resources/Refresh Catalog")]
    public static void RefreshCatalogs()
    {
        int added = Sync();
        Debug.Log(added == 0 ? "Resource catalog is already up to date." : $"Added {added} entr{(added == 1 ? "y" : "ies")} to the catalog.");
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        bool relevant = deleted.Length > 0;
        foreach (string path in imported)
        {
            if (path.EndsWith(".asset") && IsCatalogEntry(AssetDatabase.GetMainAssetTypeAtPath(path)))
            {
                relevant = true;
                break;
            }
        }

        if (relevant && !syncScheduled)
        {
            // Assets can't be modified from inside the import callback, so the sync runs right after it.
            syncScheduled = true;
            EditorApplication.delayCall += () =>
            {
                syncScheduled = false;
                Sync();
            };
        }
    }

    private static bool IsCatalogEntry(System.Type type)
    {
        return type == typeof(ResourceDefinition) || type == typeof(ResourceCategory) || type == typeof(RecipeDefinition)
            || type == typeof(ItemDefinition) || type == typeof(QuestDefinition);
    }

    private static int Sync()
    {
        // Items go in every content database (the save system's id lookup); there is normally exactly one.
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (database != null)
            {
                database.CollectAll();
                AssetDatabase.SaveAssetIfDirty(database);
            }
        }

        // Recipes go in every recipe book; there is normally exactly one.
        foreach (string guid in AssetDatabase.FindAssets("t:RecipeBook"))
        {
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(AssetDatabase.GUIDToAssetPath(guid));
            if (book != null)
            {
                book.CollectAll();
                AssetDatabase.SaveAssetIfDirty(book);
            }
        }

        var catalogs = new List<ResourceCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:ResourceCatalog"))
        {
            catalogs.Add(AssetDatabase.LoadAssetAtPath<ResourceCatalog>(AssetDatabase.GUIDToAssetPath(guid)));
        }

        if (catalogs.Count != 1)
        {
            return 0;
        }

        int added = catalogs[0].CollectAllDefinitions();
        AssetDatabase.SaveAssetIfDirty(catalogs[0]);
        return added;
    }
}
