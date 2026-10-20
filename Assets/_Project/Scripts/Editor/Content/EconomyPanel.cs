using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The "Where It Comes From" panel for items and resources in the Content Editor: the recipes that make it, the recipes
/// that use it, and the monsters, supply caches and gathering nodes that drop it, each one click away. Content that
/// nothing makes or drops is flagged, since players could never get it. Items also get a shortcut to start a new recipe
/// that makes them.
/// </summary>
public sealed class EconomyPanel
{
    private const double RefreshSeconds = 2.0;

    private readonly List<(Object source, string text)> sources = new List<(Object, string)>();
    private readonly List<(Object source, string text)> uses = new List<(Object, string)>();
    private Object shownFor;
    private double shownAt;

    public void Draw(IContentEditorHost host, Object asset)
    {
        if (!(asset is ItemDefinition) && !(asset is ResourceDefinition))
        {
            return;
        }

        // Recipes and drops change while designing, so the lists are rebuilt now and then rather than every repaint.
        if (shownFor != asset || EditorApplication.timeSinceStartup - shownAt > RefreshSeconds)
        {
            Collect(asset);
        }

        ContentUtility.BeginPanel("Where It Comes From", "How players get this and what it is for. Click an entry to open it.");
        EditorGUILayout.LabelField("Obtained from", EditorStyles.miniBoldLabel);
        if (sources.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing makes, drops or yields this, so players can't get it yet (unless they start with it).", MessageType.Warning);
        }

        DrawEntries(host, sources);

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Used for", EditorStyles.miniBoldLabel);
        if (uses.Count == 0)
        {
            EditorGUILayout.LabelField("No recipe uses it.", ContentUtility.WrappedMiniLabel);
        }

        DrawEntries(host, uses);

        if (asset is ItemDefinition item)
        {
            ContentSection recipes = FindSection<RecipeSection>(host);
            if (recipes != null && GUILayout.Button(new GUIContent("+  New Recipe That Makes This", "Opens a new recipe with this item as its result.")))
            {
                host.CreateNew(recipes, draft =>
                {
                    var data = new SerializedObject(draft);
                    data.FindProperty("output").objectReferenceValue = item;
                    data.ApplyModifiedPropertiesWithoutUndo();
                });
                GUIUtility.ExitGUI();
            }
        }

        ContentUtility.EndPanel();
    }

    private void Collect(Object asset)
    {
        shownFor = asset;
        shownAt = EditorApplication.timeSinceStartup;
        sources.Clear();
        uses.Clear();

        foreach (RecipeDefinition recipe in ResourceCatalogTools.LoadAll<RecipeDefinition>())
        {
            string where = recipe.Station == CraftingStationType.None ? "by hand" : "at " + recipe.Station.WithArticle();
            if (recipe.OutputDefinition == asset)
            {
                sources.Add((recipe, $"Crafted: {recipe.DisplayName} ×{recipe.OutputAmount} {where}"));
            }

            int needed = Needed(recipe, asset);
            if (needed > 0)
            {
                uses.Add((recipe, $"Ingredient: {needed} in {recipe.DisplayName} ({where})"));
            }
        }

        if (asset is ItemDefinition || asset is ResourceDefinition)
        {
            foreach (MonsterDefinition monster in ResourceCatalogTools.LoadAll<MonsterDefinition>())
            {
                AddDrops(monster, "Dropped by " + ContentUtility.DisplayName(monster), monster.Drops, asset);
            }

            foreach (GameObject prefab in Prefabs())
            {
                foreach (LootCache cache in prefab.GetComponentsInChildren<LootCache>(true))
                {
                    AddDrops(prefab, "Found in " + ObjectNames.NicifyVariableName(prefab.name), cache.Drops, asset);
                }
            }
        }

        if (asset is ItemDefinition item && item.IsRaidModifier)
        {
            uses.Add((item.RaidModifier, $"Raid modifier: {item.RaidModifier.DisplayName} ({item.RaidModifier.DescribeEffects()})"));
        }

        foreach (GatheringNodeDefinition node in ResourceCatalogTools.LoadAll<GatheringNodeDefinition>())
        {
            AddDrops(node, "Gathered from " + node.DisplayName, node.Yields, asset);
        }
    }

    private void AddDrops(Object owner, string label, ItemDropTable table, Object dropped)
    {
        foreach (ItemDropEntry entry in table.Entries)
        {
            if (entry != null && entry.Drop == dropped)
            {
                sources.Add((owner, $"{label}: {Quantity(entry.MinQuantity, entry.MaxQuantity, entry.Probability)}"));
            }
        }
    }

    private static int Needed(RecipeDefinition recipe, Object asset)
    {
        int needed = 0;
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            needed += cost.Item == asset ? cost.Amount : 0;
        }

        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            needed += cost.Resource == asset ? cost.Amount : 0;
        }

        return needed;
    }

    private static string Quantity(int min, int max, float chance)
    {
        string amount = min == max ? min.ToString() : $"{min}-{max}";
        return chance >= 1f ? amount : $"{amount} ({Mathf.RoundToInt(chance * 100f)}% chance)";
    }

    private static IEnumerable<GameObject> Prefabs()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null)
            {
                yield return prefab;
            }
        }
    }

    private static void DrawEntries(IContentEditorHost host, List<(Object source, string text)> entries)
    {
        foreach ((Object source, string text) in entries)
        {
            if (source != null && GUILayout.Button(text, EditorStyles.linkLabel))
            {
                ContentSection section = host.FindSectionFor(source);
                if (section != null)
                {
                    host.Select(section, source);
                }
                else
                {
                    ContentUtility.Reveal(source);
                }

                GUIUtility.ExitGUI();
            }
        }
    }

    private static T FindSection<T>(IContentEditorHost host) where T : ContentSection
    {
        foreach (ContentSection section in host.Sections)
        {
            if (section is T match)
            {
                return match;
            }
        }

        return null;
    }
}
