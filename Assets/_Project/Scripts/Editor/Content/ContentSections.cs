using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Keeps resources and categories listed in the resource catalog the inventory UI reads.</summary>
public static class ResourceCatalogTools
{
    public static List<ResourceCatalog> FindCatalogs()
    {
        var catalogs = new List<ResourceCatalog>();
        foreach (string guid in AssetDatabase.FindAssets("t:ResourceCatalog"))
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(AssetDatabase.GUIDToAssetPath(guid));
            if (catalog != null)
            {
                catalogs.Add(catalog);
            }
        }

        return catalogs;
    }

    public static bool IsCataloged(ResourceDefinition resource)
    {
        foreach (ResourceCatalog catalog in FindCatalogs())
        {
            foreach (ResourceDefinition entry in catalog.Resources)
            {
                if (entry == resource)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsCatalog(string path)
    {
        return AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(ResourceCatalog);
    }

    /// <summary>Adds the resource or category to the project's catalog. With several catalogs, the first one is used.</summary>
    public static void Register(Object entry)
    {
        List<ResourceCatalog> catalogs = FindCatalogs();
        if (catalogs.Count == 0)
        {
            return;
        }

        bool added = entry is ResourceDefinition resource ? catalogs[0].Register(resource)
            : entry is ResourceCategory category && catalogs[0].RegisterCategory(category);
        if (added)
        {
            EditorUtility.SetDirty(catalogs[0]);
            AssetDatabase.SaveAssetIfDirty(catalogs[0]);
        }
    }

    /// <summary>Takes the resource or category out of every catalog, before it is deleted.</summary>
    public static void Remove(Object entry)
    {
        string listName = entry is ResourceCategory ? "categories" : "resources";
        foreach (ResourceCatalog catalog in FindCatalogs())
        {
            var data = new SerializedObject(catalog);
            SerializedProperty list = data.FindProperty(listName);
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == entry)
                {
                    // Clearing first makes DeleteArrayElementAtIndex remove the entry instead of leaving it empty.
                    list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    list.DeleteArrayElementAtIndex(i);
                }
            }

            data.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(catalog);
        }
    }

    public static List<T> LoadAll<T>() where T : Object
    {
        var assets = new List<T>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                assets.Add(asset);
            }
        }

        return assets;
    }
}

/// <summary>Essences: the weightless, metered resources shown as bars (Life Motes, Cursed Essence, Portal Sparks).</summary>
public sealed class ResourceSection : ContentSection
{
    private static readonly Color[] Palette =
    {
        new Color(0.62f, 0.62f, 0.66f), new Color(0.55f, 0.38f, 0.22f), new Color(0.74f, 0.45f, 0.33f),
        new Color(0.85f, 0.70f, 0.25f), new Color(0.35f, 0.70f, 0.35f), new Color(0.30f, 0.60f, 0.90f),
        new Color(0.60f, 0.40f, 0.85f), new Color(0.90f, 0.35f, 0.40f),
    };

    public override string Title => "Essences";
    public override string Noun => "Essence";
    public override string SidebarGroup => "Resources";
    public override Type AssetType => typeof(ResourceDefinition);
    public override string Folder => "Assets/_Project/Data/ResourceInventory/Definitions";
    public override string Help =>
        "Weightless, metered magic resources shown as bars, such as Life Motes, Cursed Essence and Portal Sparks. Each " +
        "belongs to a category (its tab in the essence panel) and a tier. Materials like wood and ore are items now; make " +
        "them on the Items page and say where they grow on the Gathering Nodes page.";

    public override ICollection<string> HiddenFields => new[] { "icon", "color", "sortOrder" };

    public override string ListGroup(Object asset)
    {
        ResourceCategory category = ((ResourceDefinition)asset).Category;
        return category != null ? category.DisplayName : "No category";
    }

    public override Sprite ListIcon(Object asset) => ((ResourceDefinition)asset).Icon;

    public override Color? ListSwatch(Object asset) => ((ResourceDefinition)asset).Color;

    public override int? ListTier(Object asset) => ((ResourceDefinition)asset).Tier;

    public override int Compare(Object a, Object b) => ResourceCatalog.CompareForDisplay((ResourceDefinition)a, (ResourceDefinition)b);

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var resource = (ResourceDefinition)asset;
        if (resource.Category == null)
        {
            problems.Add("No category, so it has no tab in the inventory.");
        }

        if (resource.Icon == null)
        {
            problems.Add("No icon.");
        }

        if (!ResourceCatalogTools.IsCataloged(resource))
        {
            problems.Add("Not in the resource catalog, so the inventory doesn't show it.");
        }

        return problems;
    }

    protected override void InitializeDraft(ScriptableObject draft)
    {
        var categories = ResourceCatalogTools.LoadAll<ResourceCategory>();
        categories.Sort(ResourceCategorySection.CompareCategory);
        if (categories.Count == 0)
        {
            return;
        }

        var data = new SerializedObject(draft);
        data.FindProperty("category").objectReferenceValue = categories[0];
        data.FindProperty("color").colorValue = categories[0].Color;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public override string ValidateCreate(ScriptableObject draft, string name)
    {
        return ((ResourceDefinition)draft).Category == null ? "Choose a category." : base.ValidateCreate(draft, name);
    }

    protected override void AfterCreate(ScriptableObject asset, bool withOptions)
    {
        ResourceCatalogTools.Register(asset);
        base.AfterCreate(asset, withOptions);
    }

    public override void DrawExtras(Object asset)
    {
        DrawInventoryUi((ResourceDefinition)asset);
    }

    public override bool IgnoreReference(string path) => ResourceCatalogTools.IsCatalog(path);

    public override void BeforeDelete(Object asset) => ResourceCatalogTools.Remove(asset);

    private void DrawInventoryUi(ResourceDefinition resource)
    {
        ContentUtility.BeginPanel("Inventory UI", "How this essence looks in the player's essence panel.");
        SerializedObject data = Host.Inspector.Get(resource);
        data.Update();
        SerializedProperty icon = data.FindProperty("icon");
        SerializedProperty color = data.FindProperty("color");
        SerializedProperty sortOrder = data.FindProperty("sortOrder");

        using (new EditorGUILayout.HorizontalScope())
        {
            Rect box = GUILayoutUtility.GetRect(72f, 72f, GUILayout.Width(72f), GUILayout.Height(72f));
            EditorGUI.DrawRect(box, new Color(0f, 0f, 0f, 0.25f));
            ContentUtility.DrawSprite(new Rect(box.x + 4f, box.y + 4f, 64f, 64f), icon.objectReferenceValue as Sprite);

            using (new EditorGUILayout.VerticalScope())
            {
                icon.objectReferenceValue = EditorGUILayout.ObjectField(
                    new GUIContent("Icon", "Small picture shown next to the bar."), icon.objectReferenceValue, typeof(Sprite), false);
                color.colorValue = EditorGUILayout.ColorField(new GUIContent("Bar Color", "Color of the bar's fill."), color.colorValue);
                sortOrder.intValue = EditorGUILayout.IntField(
                    new GUIContent("Sort Order", "Resources with lower values are listed first within the same tier."), sortOrder.intValue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(new GUIContent("Quick Colors", "Click to use. The first matches the category."));
                    if (resource.Category != null)
                    {
                        Swatch(color, resource.Category.Color, "The category's color");
                    }

                    foreach (Color option in Palette)
                    {
                        Swatch(color, option, null);
                    }

                    GUILayout.FlexibleSpace();
                }
            }
        }

        data.ApplyModifiedProperties();
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
        DrawHudPreview(resource);

        if (!ResourceCatalogTools.IsCataloged(resource))
        {
            EditorGUILayout.HelpBox("This resource isn't in the resource catalog, so the inventory doesn't show it.", MessageType.Warning);
            if (GUILayout.Button("Add to Catalog"))
            {
                ResourceCatalogTools.Register(resource);
                Host.RefreshList();
                GUIUtility.ExitGUI();
            }
        }

        ContentUtility.EndPanel();
    }

    private static void Swatch(SerializedProperty property, Color color, string tooltip)
    {
        Rect rect = GUILayoutUtility.GetRect(18f, 18f, GUILayout.Width(18f), GUILayout.Height(18f));
        if (GUI.Button(rect, new GUIContent("", tooltip), GUIStyle.none))
        {
            property.colorValue = color;
        }

        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.6f));
        EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), color);
    }

    // Mirrors the inventory panel: the category tab over the resource's bar with its tier badge.
    private static void DrawHudPreview(ResourceDefinition resource)
    {
        ResourceCategory category = resource.Category;
        Rect heading = GUILayoutUtility.GetRect(0f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(heading, new Color(0.05f, 0.06f, 0.08f, 0.9f));
        EditorGUI.DrawRect(new Rect(heading.x, heading.y, 3f, heading.height), category != null ? category.Color : Color.gray);
        var headingStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
        GUI.Label(new Rect(heading.x + 10f, heading.y + 3f, heading.width - 10f, 16f), category != null ? category.DisplayName : "No category", headingStyle);

        Rect row = GUILayoutUtility.GetRect(0f, 34f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(row, new Color(0.08f, 0.09f, 0.11f, 0.85f));
        var iconRect = new Rect(row.x + 6f, row.y + 4f, 26f, 26f);
        ContentUtility.DrawSprite(iconRect, resource.Icon);

        var tierStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Tiers.ColorOf(resource.Tier) }, alignment = TextAnchor.MiddleCenter };
        var tierRect = new Rect(iconRect.xMax + 4f, row.y + 8f, 26f, 18f);
        GUI.Label(tierRect, Tiers.ToRoman(resource.Tier), tierStyle);

        var textStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.white } };
        var nameRect = new Rect(tierRect.xMax + 4f, row.y + 8f, 110f, 18f);
        GUI.Label(nameRect, resource.DisplayName, textStyle);

        int capacity = ResourceStorage.DefaultBaseCapacity;
        int amount = Mathf.CeilToInt(capacity * 0.6f);
        var amountRect = new Rect(row.xMax - 56f, row.y + 8f, 50f, 18f);
        GUI.Label(amountRect, amount + "/" + capacity, textStyle);

        var bar = new Rect(nameRect.xMax, row.y + 10f, Mathf.Max(20f, amountRect.x - nameRect.xMax - 6f), 14f);
        EditorGUI.DrawRect(bar, new Color(0.02f, 0.02f, 0.03f, 1f));
        EditorGUI.DrawRect(new Rect(bar.x, bar.y, bar.width * amount / capacity, bar.height), resource.Color);
    }
}

/// <summary>Gathering nodes: trees, rocks, ore veins and plants, what tool they need and what they yield.</summary>
public sealed class GatheringNodeSection : PrefabContentSection
{
    public override string Title => "Gathering Nodes";
    public override string Noun => "Gathering Node";
    public override string SidebarGroup => "Resources";
    public override Type AssetType => typeof(GatheringNodeDefinition);
    public override string Folder => "Assets/_Project/Data/Gathering";
    public override string Help =>
        "Things the player gathers in the world: an oak, a copper vein, a flax patch, a Life Mote. Each says which tool it " +
        "needs, how long gathering takes and when it grows back. Its yield table lists the material items (and essences) " +
        "one gather gives. Higher tiers need a better tool.";

    protected override string PrefabField => "worldPrefab";
    protected override PrefabBuildUtility.Placeholder Placeholder => ResourceNodePrefabBuilder.Placeholder;
    protected override string PrefabDescription =>
        "The node placed in the world, such as a tree or an ore vein. The gathering settings and yields above control it.";

    public override ICollection<string> HiddenFields => new[] { "worldPrefab", "worldModelPrefab" };
    protected override IEnumerable<string> CreateHiddenFields => new[] { "worldModelPrefab" };

    public override string ListGroup(Object asset)
    {
        ToolType tool = ((GatheringNodeDefinition)asset).GatherTool;
        return tool == ToolType.None ? "By hand" : tool.ToString();
    }

    public override Sprite ListIcon(Object asset)
    {
        Object yield = ((GatheringNodeDefinition)asset).MainYield;
        return yield is ItemDefinition item ? item.Icon : yield is ResourceDefinition resource ? resource.Icon : null;
    }

    public override int? ListTier(Object asset) => ((GatheringNodeDefinition)asset).Tier;

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var node = (GatheringNodeDefinition)asset;
        if (node.Yields.Entries.Count == 0)
        {
            problems.Add("Yields nothing.");
        }

        foreach (ItemDropEntry entry in node.Yields.Entries)
        {
            if (entry == null || entry.Drop == null)
            {
                problems.Add("A yield line has no item or essence.");
                break;
            }
        }

        if (node.GatherTool != ToolType.None && node.RequiredToolTier > 1 && !ToolExists(node.GatherTool, node.RequiredToolTier))
        {
            problems.Add($"No item is a Tier {Tiers.ToRoman(node.RequiredToolTier)} {node.GatherTool} or better, so nobody can gather it.");
        }

        return problems;
    }

    public override void DrawExtras(Object asset) => DrawPrefabPanel(asset);

    protected override GameObject DefaultModel(Object asset) => ((GatheringNodeDefinition)asset).WorldModelPrefab;

    protected override void OnModelUsed(Object asset, GameObject model)
    {
        if (model != null)
        {
            ContentUtility.SetReference(asset, "worldModelPrefab", model);
        }
    }

    protected override void ValidateModel(GameObject model) => ResourceNodePrefabBuilder.ValidateModel(model);

    protected override GameObject BuildPrefab(Object asset, GameObject model) => ResourceNodePrefabBuilder.Create((GatheringNodeDefinition)asset, model);

    protected override void UpdatePrefab(GameObject prefab, Object asset) => ResourceNodePrefabBuilder.Update(prefab, (GatheringNodeDefinition)asset);

    protected override List<string> PrefabProblems(GameObject prefab, Object asset) =>
        ResourceNodePrefabBuilder.FindProblems(prefab, (GatheringNodeDefinition)asset);

    private static bool ToolExists(ToolType tool, int tier)
    {
        return ResourceCatalogTools.LoadAll<ItemDefinition>().Exists(item => item.ToolType == tool && item.ToolTier >= tier);
    }
}

/// <summary>Shared sounds, particles and shake for gathering families (Wood, Stone, Ore, Fibre).</summary>
public sealed class HarvestFeedbackSection : ContentSection
{
    public override string Title => "Harvest Feedback";
    public override string Noun => "Harvest Feedback";
    public override string SidebarGroup => "Resources";
    public override Type AssetType => typeof(HarvestFeedbackProfile);
    public override string Folder => "Assets/_Project/Data/Gathering/Feedback";
    public override string Help =>
        "How gathering feels for a family of nodes: the sound, particles and node shake of each swing (critical, normal, " +
        "glancing) and the break when a node is gathered. Pick one on each gathering node (Gathering Nodes › Feel). Empty " +
        "sounds and particles use the Default profile in Systems › Gathering Minigame. For a one-off reaction on a single " +
        "prefab (an animation, a falling tree), add a ResourceNodeFeedback to it and use its events.";
}

public sealed class ResourceCategorySection : ContentSection
{
    public override string Title => "Resource Categories";
    public override string Noun => "Resource Category";
    public override string SidebarGroup => "Resources";
    public override Type AssetType => typeof(ResourceCategory);
    public override string Folder => "Assets/_Project/Data/ResourceInventory/Categories";
    public override string Help =>
        "Families of raw resources, such as Wood, Stone, Ore, Essence and Fiber. Each category is a tab in the resource " +
        "inventory, and sets the tool and animation new resources in it start with.";

    public override Color? ListSwatch(Object asset) => ((ResourceCategory)asset).Color;

    public override int Compare(Object a, Object b) => CompareCategory((ResourceCategory)a, (ResourceCategory)b);

    protected override void AfterCreate(ScriptableObject asset, bool withOptions) => ResourceCatalogTools.Register(asset);

    public override bool IgnoreReference(string path) => ResourceCatalogTools.IsCatalog(path);

    public override void BeforeDelete(Object asset) => ResourceCatalogTools.Remove(asset);

    public override string DeleteBlocker(Object asset)
    {
        var users = ResourceCatalogTools.LoadAll<ResourceDefinition>().FindAll(resource => resource.Category == (ResourceCategory)asset);
        return users.Count == 0 ? null
            : $"{users.Count} resource(s) still belong to this category, such as {users[0].DisplayName}. Move them to another category first.";
    }

    internal static int CompareCategory(ResourceCategory a, ResourceCategory b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a == null || b == null)
        {
            return a == null ? 1 : -1;
        }

        return ResourceCatalog.CompareCategories(a, b);
    }
}

public sealed class MonsterSection : PrefabContentSection
{
    private const string DefaultAttackPath = "Assets/_Project/Data/Combat/Attacks/MonsterStrike.asset";

    public override string Title => "Monsters";
    public override string Noun => "Monster";
    public override string SidebarGroup => "Creatures";
    public override Type AssetType => typeof(MonsterDefinition);
    public override string Folder => "Assets/_Project/Data/Monsters";
    public override string Help =>
        "Creatures that fight. Enemy monsters hunt the player, Player monsters fight Enemies and Neutral ones never attack. " +
        "Their prefab comes with everything needed to move, fight, animate, show health and drop loot. " +
        "Composable combat: choose Chase or Keep Distance, combine temperament flags, and add attacks. " +
        "Highest-priority eligible attack wins (list order breaks ties); each has its own range and cooldown. " +
        "An empty attack list uses the legacy single attack. Defensive bounds win over pursuit; cowardly retreat wins over attacks.";

    protected override string PrefabField => "prefab";
    protected override PrefabBuildUtility.Placeholder Placeholder => MonsterPrefabBuilder.Placeholder;
    protected override string PrefabDescription =>
        "The monster placed in the world. It moves on the scene's baked NavMesh, so bake one (AI > Navigation) in new scenes.";

    public override string ListGroup(Object asset) => ((MonsterDefinition)asset).Faction + " monsters";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var monster = (MonsterDefinition)asset;
        if (monster.CombatStats == null)
        {
            problems.Add("No combat stats, so it has 1 health and no armor.");
        }

        if (monster.Attack == null && monster.Attacks.Count == 0 && monster.Faction != Faction.Neutral)
        {
            problems.Add("No attack, so it chases targets but never hits them.");
        }

        foreach (MonsterAttack action in monster.Attacks)
        {
            if (action == null || action.attack == null)
            {
                problems.Add("An attack entry has no Attack Definition.");
                continue;
            }
            if (action.maximumRange < action.minimumRange)
            {
                problems.Add($"{action.attack.DisplayName}: maximum range is below minimum range.");
            }
            if (action.kind == MonsterAttackKind.Projectile
                && (action.projectilePrefab == null || action.projectilePrefab.GetComponent<MonsterProjectile>() == null))
            {
                problems.Add($"{action.attack.DisplayName}: requires a prefab with MonsterProjectile.");
            }
        }

        return problems;
    }

    protected override void InitializeDraft(ScriptableObject draft)
    {
        ContentUtility.SetReference(draft, "attack", AssetDatabase.LoadAssetAtPath<AttackDefinition>(DefaultAttackPath));
        ContentUtility.SetReference(draft, "animatorController", MonsterPrefabBuilder.LoadDefaultController());
    }

    protected override void ValidateModel(GameObject model) => MonsterPrefabBuilder.ValidateModel(model);

    protected override GameObject BuildPrefab(Object asset, GameObject model) => MonsterPrefabBuilder.Create((MonsterDefinition)asset, model);

    protected override void UpdatePrefab(GameObject prefab, Object asset) => MonsterPrefabBuilder.Update(prefab, (MonsterDefinition)asset);

    protected override List<string> PrefabProblems(GameObject prefab, Object asset) =>
        MonsterPrefabBuilder.FindProblems(prefab, (MonsterDefinition)asset);
}

public sealed class BuildingSection : PrefabContentSection
{
    private const string DefaultStatsPath = "Assets/_Project/Data/Combat/Stats/WoodenStructureStats.asset";

    public override string Title => "Buildings";
    public override string Noun => "Building";
    public override string SidebarGroup => "Buildings";
    public override Type AssetType => typeof(BuildingDefinition);
    public override string Folder => "Assets/_Project/Data/Buildings";
    public override string Help =>
        "Structures such as walls and workshops. They have health, can be attacked by the other faction and cost resources to build.";

    protected override string PrefabField => "prefab";
    protected override string IconField => "icon";
    protected override string IconFolder => "Assets/_Project/UI/BuildingIcons";
    protected override PrefabBuildUtility.Placeholder Placeholder => BuildingPrefabBuilder.Placeholder;
    protected override string PrefabDescription =>
        "The building placed in the world, with health, a health bar and an obstacle that monsters walk around.";

    public override string ListGroup(Object asset) => ((BuildingDefinition)asset).Faction + " buildings";

    public override Sprite ListIcon(Object asset) => ((BuildingDefinition)asset).Icon;

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        if (((BuildingDefinition)asset).CombatStats == null)
        {
            problems.Add("No combat stats, so it has 1 health.");
        }

        return problems;
    }

    protected override void InitializeDraft(ScriptableObject draft)
    {
        ContentUtility.SetReference(draft, "combatStats", AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(DefaultStatsPath));
    }

    protected override void ValidateModel(GameObject model) => BuildingPrefabBuilder.ValidateModel(model);

    protected override GameObject BuildPrefab(Object asset, GameObject model) => BuildingPrefabBuilder.Create((BuildingDefinition)asset, model);

    protected override void UpdatePrefab(GameObject prefab, Object asset) => BuildingPrefabBuilder.Update(prefab, (BuildingDefinition)asset);

    protected override List<string> PrefabProblems(GameObject prefab, Object asset) =>
        BuildingPrefabBuilder.FindProblems(prefab, (BuildingDefinition)asset);
}

public sealed class ItemSection : PrefabContentSection
{
    public override string Title => "Items";
    public override string Noun => "Item";
    public override string SidebarGroup => "Items";
    public override Type AssetType => typeof(ItemDefinition);
    public override string Folder => "Assets/_Project/Data/Items";
    public override string Help =>
        "Everything that goes in the bag (B): refined materials such as planks and ingots, tools that unlock higher-tier " +
        "gathering, consumables, backpacks that raise carry capacity, and monster drops such as hides and bones.";

    protected override string PrefabField => "prefab";
    protected override string IconField => "icon";
    protected override string IconFolder => "Assets/_Project/UI/ItemIcons";
    protected override PrefabBuildUtility.Placeholder Placeholder => ItemPrefabBuilder.Placeholder;
    protected override string PrefabDescription => "The item as it lies in the world, for example after being dropped.";

    public override string ListGroup(Object asset) => ObjectNames.NicifyVariableName(((ItemDefinition)asset).Category.ToString());

    public override Sprite ListIcon(Object asset) => ((ItemDefinition)asset).Icon;

    public override int? ListTier(Object asset) => ((ItemDefinition)asset).Tier;

    public override int Compare(Object a, Object b)
    {
        var first = (ItemDefinition)a;
        var second = (ItemDefinition)b;
        int result = first.Category.CompareTo(second.Category);
        if (result == 0)
        {
            result = first.Tier.CompareTo(second.Tier);
        }

        return result != 0 ? result : string.Compare(first.DisplayName, second.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var item = (ItemDefinition)asset;
        if (item.Icon == null)
        {
            problems.Add("No icon.");
        }

        if (item.ToolType != ToolType.None && item.ToolTier == 0)
        {
            problems.Add("It is a tool with Tool Tier 0, so it can't gather anything.");
        }

        if ((item.IsTool || item.IsBackpack) && item.Stackable)
        {
            problems.Add("Tools and backpacks usually shouldn't stack.");
        }

        return problems;
    }

    public override void DrawExtras(Object asset)
    {
        var item = (ItemDefinition)asset;
        DrawIconPanel(asset, PlaceholderIconPainter.Guess(item.DisplayName, item.Category.ToString(), item.ToolType,
            item.IsConsumable || item.IsBlueprint, item.IsBackpack, item.ArmorSlot));
        DrawPrefabPanel(asset);
    }

    protected override void ValidateModel(GameObject model) => ItemPrefabBuilder.ValidateModel(model);

    protected override GameObject BuildPrefab(Object asset, GameObject model) => ItemPrefabBuilder.Create((ItemDefinition)asset, model);

    protected override void UpdatePrefab(GameObject prefab, Object asset) => ItemPrefabBuilder.Update(prefab, (ItemDefinition)asset);

    protected override List<string> PrefabProblems(GameObject prefab, Object asset) =>
        ItemPrefabBuilder.FindProblems(prefab, (ItemDefinition)asset);
}

public sealed class RecipeSection : ContentSection
{
    public override string Title => "Recipes";
    public override string Noun => "Recipe";
    public override string SidebarGroup => "Crafting";
    public override Type AssetType => typeof(RecipeDefinition);
    public override string Folder => "Assets/_Project/Data/Crafting/Recipes";
    public override string Help =>
        "How things are made: an item for the bag, or a refined material for the resource bars (set one result). A recipe " +
        "uses up resources and items (at home also from storage) and is known from the start or learned from a blueprint, " +
        "a resident's job, a quest or a monster. By Hand recipes (keep these few and basic) are made instantly from the bag " +
        "window (B). Every other recipe is queued at its station: materials are paid when queued, the station works for " +
        "Craft Seconds per craft (also while the player is raiding), and the result goes to its output slot (refined " +
        "materials go to storage first).";

    public override string Label(Object asset)
    {
        var recipe = (RecipeDefinition)asset;
        return recipe.OutputAmount > 1 ? $"{recipe.DisplayName} ×{recipe.OutputAmount}" : recipe.DisplayName;
    }

    public override string ListGroup(Object asset) => ((RecipeDefinition)asset).Station.DisplayName();

    public override Sprite ListIcon(Object asset) => ((RecipeDefinition)asset).OutputIcon;

    public override int? ListTier(Object asset) => ((RecipeDefinition)asset).Tier;

    public override int Compare(Object a, Object b) => RecipeBook.CompareForDisplay((RecipeDefinition)a, (RecipeDefinition)b);

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var recipe = (RecipeDefinition)asset;
        if (!recipe.HasOutput)
        {
            problems.Add("No result item or resource, so it makes nothing.");
        }

        if (recipe.ResourceCost.Count == 0 && recipe.ItemCost.Count == 0)
        {
            problems.Add("Costs nothing.");
        }

        if (!recipe.KnownFromStart && TaughtBy(recipe).Count == 0)
        {
            problems.Add("Not known from the start and nothing teaches it (blueprint, job, quest or monster), so nobody can craft it.");
        }

        if (!RecipeBookTools.IsListed(recipe))
        {
            problems.Add("Not in the recipe book, so the crafting list doesn't show it.");
        }

        if (!recipe.IsHandcrafted && recipe.Station != CraftingStationType.Portal
            && !ResourceCatalogTools.LoadAll<BuildingDefinition>().Exists(building => building.Station == recipe.Station))
        {
            problems.Add($"No building is {recipe.Station.WithArticle()}, so nowhere can make it.");
        }

        return problems;
    }

    protected override void InitializeDraft(ScriptableObject draft)
    {
        var data = new SerializedObject(draft);
        data.FindProperty("station").intValue = (int)CraftingStationType.Workbench;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    protected override void AfterCreate(ScriptableObject asset, bool withOptions) => RecipeBookTools.Register();

    public override bool IgnoreReference(string path) => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(RecipeBook);

    public override void DrawExtras(Object asset)
    {
        var recipe = (RecipeDefinition)asset;
        ContentUtility.BeginPanel("Summary", "What the player sees in the crafting list.");
        EditorGUILayout.LabelField("Makes", recipe.HasOutput ? $"{recipe.OutputAmount} × {recipe.OutputName}{(recipe.OutputResource != null ? " (resource)" : "")}" : "nothing");
        EditorGUILayout.LabelField("Uses", Describe(recipe), ContentUtility.WrappedMiniLabel);
        EditorGUILayout.LabelField("Where", recipe.IsHandcrafted
            ? "Anywhere, by hand, instantly (bag window)"
            : $"Queued at {recipe.Station.WithArticle()}, {recipe.CraftSeconds:0.#} s per craft");
        List<string> teachers = TaughtBy(recipe);
        string learned = recipe.KnownFromStart ? "Known from the start" : teachers.Count > 0
            ? "Learned from " + string.Join(", ", teachers)
            : "Not learnable yet: add it to a blueprint, a job, a quest reward or a monster's Unlocks Recipes";
        EditorGUILayout.LabelField("Learned", learned, ContentUtility.WrappedMiniLabel);
        ContentUtility.EndPanel();
    }

    private static string Describe(RecipeDefinition recipe)
    {
        var parts = new List<string>();
        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            if (cost.Resource != null)
            {
                parts.Add($"{cost.Amount} {cost.Resource.DisplayName}");
            }
        }

        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null)
            {
                parts.Add($"{cost.Amount} {cost.Item.DisplayName}");
            }
        }

        return parts.Count > 0 ? string.Join(", ", parts) : "nothing";
    }

    /// <summary>Everything that teaches the recipe, in words: blueprints, jobs, quests and monsters.</summary>
    private static List<string> TaughtBy(RecipeDefinition recipe)
    {
        var teachers = new List<string>();
        foreach (ItemDefinition item in ResourceCatalogTools.LoadAll<ItemDefinition>())
        {
            if (Contains(item.TeachesRecipes, recipe))
            {
                teachers.Add("reading " + item.DisplayName);
            }
        }

        foreach (JobDefinition job in ResourceCatalogTools.LoadAll<JobDefinition>())
        {
            if (Contains(job.TeachesRecipes, recipe))
            {
                teachers.Add("a resident working as " + job.DisplayName);
            }
        }

        foreach (QuestDefinition quest in ResourceCatalogTools.LoadAll<QuestDefinition>())
        {
            if (Contains(quest.RewardRecipes, recipe))
            {
                teachers.Add("the quest " + quest.DisplayName);
            }
        }

        teachers.AddRange(UnlockedBy(recipe).ConvertAll(monster => "defeating " + monster.DisplayName));
        return teachers;
    }

    private static bool Contains<T>(IReadOnlyList<T> list, T wanted) where T : class
    {
        if (list == null)
        {
            return false;
        }

        foreach (T entry in list)
        {
            if (entry == wanted)
            {
                return true;
            }
        }

        return false;
    }

    private static List<MonsterDefinition> UnlockedBy(RecipeDefinition recipe)
    {
        var monsters = ResourceCatalogTools.LoadAll<MonsterDefinition>();
        return monsters.FindAll(monster =>
        {
            foreach (RecipeDefinition unlock in monster.UnlocksRecipes)
            {
                if (unlock == recipe)
                {
                    return true;
                }
            }

            return false;
        });
    }
}

/// <summary>Keeps every recipe listed in the project's recipe book, which the crafting list reads.</summary>
public static class RecipeBookTools
{
    public static void Register()
    {
        foreach (RecipeBook book in ResourceCatalogTools.LoadAll<RecipeBook>())
        {
            book.CollectAll();
            AssetDatabase.SaveAssetIfDirty(book);
        }
    }

    public static bool IsListed(RecipeDefinition recipe)
    {
        foreach (RecipeBook book in ResourceCatalogTools.LoadAll<RecipeBook>())
        {
            foreach (RecipeDefinition entry in book.Recipes)
            {
                if (entry == recipe)
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public sealed class WeaponSection : ContentSection
{
    public override string Title => "Weapons";
    public override string Noun => "Weapon";
    public override string SidebarGroup => "Combat";
    public override Type AssetType => typeof(WeaponDefinition);
    public override string Folder => "Assets/_Project/Data/Combat/Weapons";
    public override string Help =>
        "What the player can hold. A weapon sets the animation stance, its damage and its attacks: the combo chain the attack button " +
        "walks through when pressed in time, the heavy attack a held press performs, the counter after a parry or perfect dodge, and " +
        "how well it guards. Timings follow the animations (Safe Haven › Characters › Set Up Combat Animations).";

    public override string ListGroup(Object asset) => ObjectNames.NicifyVariableName(((WeaponDefinition)asset).Stance.ToString());

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var weapon = (WeaponDefinition)asset;
        if (weapon.BasicAttack == null && weapon.Combo.Count == 0)
        {
            problems.Add("No basic attack, so swinging it does no damage.");
        }

        for (int i = 0; i < weapon.Combo.Count; i++)
        {
            if (weapon.Combo[i] == null)
            {
                problems.Add($"Combo step {i + 1} is empty.");
            }
        }

        foreach (AttackDefinition attack in System.Linq.Enumerable.Concat(weapon.Combo, new[] { weapon.HeavyAttack, weapon.CounterAttack }))
        {
            if (attack != null && string.IsNullOrEmpty(attack.AnimationState))
            {
                problems.Add($"{attack.DisplayName} has no Animation State, so it plays the stance's old attack animation out of step with its timing.");
            }
        }

        return problems;
    }
}

public sealed class AttackSection : ContentSection
{
    public override string Title => "Attacks & Abilities";
    public override string Noun => "Attack";
    public override string SidebarGroup => "Combat";
    public override Type AssetType => typeof(AttackDefinition);
    public override string Folder => "Assets/_Project/Data/Combat/Attacks";
    public override string Help =>
        "One way of dealing damage: a weapon swing, a shield bash, a monster's strike or an ability. It sets the damage, " +
        "how far and wide it reaches and when the hit lands.";

    public override string ListGroup(Object asset) => ((AttackDefinition)asset).DamageType + " damage";
}

public sealed class CombatStatsSection : ContentSection
{
    public override string Title => "Combat Stats";
    public override string Noun => "Combat Stats";
    public override string SidebarGroup => "Combat";
    public override Type AssetType => typeof(CombatStatsDefinition);
    public override string Folder => "Assets/_Project/Data/Combat/Stats";
    public override string Help =>
        "Health, power, armor and resistances. The player, each monster and each building points at one of these, and " +
        "several can share the same stats.";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        if (((CombatStatsDefinition)asset).GetValue(StatType.MaxHealth) <= 0f)
        {
            problems.Add("Max Health is 0, so anything using these stats has 1 health.");
        }

        return problems;
    }

    protected override void InitializeDraft(ScriptableObject draft)
    {
        var data = new SerializedObject(draft);
        SerializedProperty stats = data.FindProperty("stats");
        SetStat(stats, StatType.MaxHealth, 100f);
        SetStat(stats, StatType.PhysicalCritDamage, 0.5f);
        SetStat(stats, StatType.SpellCritDamage, 0.5f);
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // CreateInstance may already have run the asset's Reset, which lists every stat, so update a row before adding one.
    private static void SetStat(SerializedProperty stats, StatType stat, float value)
    {
        SerializedProperty entry = null;
        for (int i = 0; i < stats.arraySize; i++)
        {
            SerializedProperty candidate = stats.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("stat").intValue == (int)stat)
            {
                entry = candidate;
                break;
            }
        }

        if (entry == null)
        {
            stats.arraySize++;
            entry = stats.GetArrayElementAtIndex(stats.arraySize - 1);
            entry.FindPropertyRelative("stat").intValue = (int)stat;
        }

        entry.FindPropertyRelative("value").floatValue = value;
    }
}

public sealed class PrefabBrowserSection : ContentSection
{
    private const string PrefabRoot = "Assets/_Project/Prefabs";
    private PrefabTools tools;
    private GameObject sourcesFor;
    private readonly List<KeyValuePair<ContentSection, Object>> sources = new List<KeyValuePair<ContentSection, Object>>();

    public override string Title => "All Prefabs";
    public override string Noun => "Prefab";
    public override string SidebarGroup => "Prefabs";
    public override Type AssetType => typeof(GameObject);
    public override bool UsesDataInspector => false;
    public override string Help =>
        "Every prefab in the project's Prefabs folder. Open one, place it in the scene, adjust its components or delete it. " +
        "Prefabs made from data are best changed from that data's page, linked at the top.";

    private PrefabTools Tools => tools ??= new PrefabTools(() => Host?.Repaint());

    public override List<Object> Load()
    {
        var assets = new List<Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null)
            {
                assets.Add(prefab);
            }
        }

        return assets;
    }

    public override string Label(Object asset) => ObjectNames.NicifyVariableName(asset.name);

    public override string ListGroup(Object asset)
    {
        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)).Replace('\\', '/');
        return folder.Length > PrefabRoot.Length ? folder.Substring(PrefabRoot.Length + 1) : "Prefabs";
    }

    public override void DrawExtras(Object asset)
    {
        var prefab = (GameObject)asset;
        if (sourcesFor != prefab)
        {
            FindSources(prefab);
        }

        ContentUtility.BeginPanel("Prefab");
        using (new EditorGUILayout.HorizontalScope())
        {
            Tools.DrawPreview(prefab, 128f);
            using (new EditorGUILayout.VerticalScope())
            {
                foreach (KeyValuePair<ContentSection, Object> source in sources)
                {
                    if (GUILayout.Button($"Made from {source.Key.Noun}: {source.Key.Label(source.Value)}  →"))
                    {
                        Host.Select(source.Key, source.Value);
                        GUIUtility.ExitGUI();
                    }
                }

                if (GUILayout.Button("Open Prefab"))
                {
                    AssetDatabase.OpenAsset(prefab);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button("Place in Scene"))
                {
                    PrefabTools.PlaceInScene(prefab);
                    GUIUtility.ExitGUI();
                }

                if (GUILayout.Button(new GUIContent("Paint with Scatter Brush", "Paint many of it over the ground in the Scene view")))
                {
                    ScatterBrush.OpenWith(prefab);
                    GUIUtility.ExitGUI();
                }
            }
        }

        EditorGUILayout.Space(4f);
        Tools.DrawComponents(prefab, "Changes are saved into the prefab.");
        ContentUtility.EndPanel();
    }

    public override void Dispose()
    {
        tools?.Dispose();
    }

    private void FindSources(GameObject prefab)
    {
        sourcesFor = prefab;
        sources.Clear();
        foreach (ContentSection section in Host.Sections)
        {
            if (!(section is PrefabContentSection))
            {
                continue;
            }

            foreach (Object asset in section.Load())
            {
                if (section.LinkedPrefab(asset) == prefab)
                {
                    sources.Add(new KeyValuePair<ContentSection, Object>(section, asset));
                }
            }
        }
    }
}
