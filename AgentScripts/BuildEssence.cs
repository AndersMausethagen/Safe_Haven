#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// What Essence buys (`unity command run_script --file AgentScripts/BuildEssence.cs --entry BuildEssence.Apply`, GDD §13.5):
/// the enchantments and Resources/EssenceSettings, the altar's upgrades that really speed it up, Purified Essence on the
/// greatest building tiers, and the two draughts that keep essence worth having once everything is upgraded. Makes no
/// scene objects. Safe to run again: it writes the same values.
/// </summary>
public static class BuildEssence
{
    private const string Folder = "Assets/_Project/Data/Essence/Enchantments/";
    private const string Definitions = "Assets/_Project/Data/ResourceInventory/Definitions/";
    private const string SettingsPath = "Assets/_Project/Resources/EssenceSettings.asset";
    private const string Food = "Assets/_Project/Data/Items/Food/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";
    private const string Icons = "Assets/_Project/Art/Items/Gear/Icons/";
    private const string Buildings = "Assets/_Project/Data/Buildings/";

    private static ResourceDefinition cursed;
    private static ResourceDefinition purified;

    public static string Apply()
    {
        cursed = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(Definitions + "CursedEssence.asset");
        purified = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(Definitions + "PurifiedEssence.asset");
        PrefabBuildUtility.EnsureFolder(Folder.TrimEnd('/'));
        List<EnchantmentDefinition> enchantments = Enchantments();
        Settings(enchantments);
        string altar = Altar();
        string tiers = GreatTiers();
        int draughts = Draughts();
        AssetDatabase.SaveAssets();
        Refresh();
        AssetDatabase.SaveAssets();
        return $"enchantments {enchantments.Count}; {altar}; {tiers}; draughts {draughts}";
    }

    private static List<EnchantmentDefinition> Enchantments()
    {
        const EnchantTarget weapon = EnchantTarget.Weapon;
        const EnchantTarget armor = EnchantTarget.Armor;
        const EnchantTarget jewelry = EnchantTarget.Jewelry;
        const EnchantTarget tool = EnchantTarget.Tool;
        return new List<EnchantmentDefinition>
        {
            // Purified: protection, recovery and a steady hand. No drawback, and it pays for that.
            Make("Hearthbound", EssenceSchool.Purified, armor, 10, "The warmth of the home fire, worked into the seams.", new StatBonus(StatType.Armor, 8f)),
            Make("Mossgrown", EssenceSchool.Purified, armor | jewelry, 10, "Soft green at the edges, and it closes small wounds.", new StatBonus(StatType.MaxHealth, 20f)),
            Make("Springfed", EssenceSchool.Purified, jewelry | armor, 8, "It remembers running water.", new StatBonus(StatType.MaxStamina, 20f)),
            Make("Frostwarded", EssenceSchool.Purified, armor | jewelry, 8, "The cold finds nothing to hold on to.", new StatBonus(StatType.FrostResistance, 0.15f)),
            Make("Ashwarded", EssenceSchool.Purified, armor | jewelry, 8, "It smells of old smoke and does not burn.", new StatBonus(StatType.FireResistance, 0.15f)),
            Make("Fleetfoot", EssenceSchool.Purified, armor, 12, "Light as a hare on the first snow.", new StatBonus(StatType.MoveSpeed, 0.05f)),
            Make("Sure-Handed", EssenceSchool.Purified, weapon, 12, "It finds the gap in the bark.", new StatBonus(StatType.PhysicalCritChance, 0.06f)),
            Make("Patient", EssenceSchool.Purified, tool, 8, "It takes what is offered and wears slowly.", new StatBonus(StatType.GatherYield, 0.1f), new StatBonus(StatType.ToolCare, 0.2f)),
            // Cursed: burst, drain and risky trades. Used raw and cheap for what they give; each states its price.
            Make("Bloodletting", EssenceSchool.Cursed, weapon, 14, "It cuts whoever holds it, a little, always.", new StatBonus(StatType.DamageBonus, 0.15f), new StatBonus(StatType.MaxHealth, -15f)),
            Make("Hollow", EssenceSchool.Cursed, weapon, 18, "It drinks for you. You feel thinner for it.", new StatBonus(StatType.Lifesteal, 0.06f), new StatBonus(StatType.Armor, -6f)),
            Make("Reaping", EssenceSchool.Cursed, weapon | jewelry, 16, "Whatever the forest gave them, it takes back with interest.", new StatBonus(StatType.EssenceFind, 0.3f), new StatBonus(StatType.MaxHealth, -10f)),
            Make("Witchlit", EssenceSchool.Cursed, jewelry, 14, "A cold little light that wants out.", new StatBonus(StatType.SpellPower, 10f), new StatBonus(StatType.FrostResistance, -0.15f)),
            Make("Gravebound", EssenceSchool.Cursed, armor, 14, "Heavy as a barrow door, and as hard to move.", new StatBonus(StatType.Armor, 16f), new StatBonus(StatType.MoveSpeed, -0.06f)),
            Make("Greedy", EssenceSchool.Cursed, tool, 10, "It tears out more than it should.", new StatBonus(StatType.GatherYield, 0.2f), new StatBonus(StatType.ToolCare, -0.3f)),
        };
    }

    private static EnchantmentDefinition Make(string label, EssenceSchool school, EnchantTarget targets, int price, string text, params StatBonus[] bonuses)
    {
        string file = label.Replace("-", "");
        string path = Folder + file + ".asset";
        var enchantment = AssetDatabase.LoadAssetAtPath<EnchantmentDefinition>(path);
        if (enchantment == null)
        {
            enchantment = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            AssetDatabase.CreateAsset(enchantment, path);
        }

        var so = new SerializedObject(enchantment);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("school").intValue = (int)school;
        so.FindProperty("targets").intValue = (int)targets;
        SetCost(so.FindProperty("cost"), (school == EssenceSchool.Cursed ? cursed : purified, price));
        SetBonuses(so.FindProperty("bonuses"), bonuses);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(enchantment);
        return enchantment;
    }

    private static void Settings(List<EnchantmentDefinition> enchantments)
    {
        var settings = AssetDatabase.LoadAssetAtPath<EssenceSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<EssenceSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("glowMaterial").objectReferenceValue = GlowMaterial();
        SerializedProperty list = so.FindProperty("enchantments");
        list.arraySize = enchantments.Count;
        for (int i = 0; i < enchantments.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = enchantments[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    // Soft additive motes. Shared by all worked gear; the particles carry the colour.
    private static Material GlowMaterial()
    {
        const string path = "Assets/_Project/Data/Essence/Gear Motes.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        EditorUtility.SetDirty(material);
        return material;
    }

    // The GDD promised altar upgrades that improve throughput; they added only renown until now.
    private static string Altar()
    {
        var altar = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + "PurificationAltar.asset");
        var so = new SerializedObject(altar);
        SerializedProperty upgrades = so.FindProperty("upgrades");
        for (int i = 0; i < upgrades.arraySize; i++)
        {
            upgrades.GetArrayElementAtIndex(i).FindPropertyRelative("craftSpeed").floatValue = 0.25f;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(altar);
        return $"altar levels {upgrades.arraySize} at +25% each";
    }

    // Selected tiers only (GDD §13.5): basic structures never need essence.
    private static string GreatTiers()
    {
        var done = new List<string>();
        foreach ((string file, int price) in new[] { ("PurificationAltar", 8), ("Cauldron", 8), ("ConstructionTable", 10) })
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + file + ".asset");
            var so = new SerializedObject(building);
            SerializedProperty upgrades = so.FindProperty("upgrades");
            if (upgrades.arraySize == 0)
            {
                continue;
            }

            SerializedProperty top = upgrades.GetArrayElementAtIndex(upgrades.arraySize - 1);
            SetCost(top.FindPropertyRelative("essence"), (purified, price));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(building);
            done.Add($"{file} {top.FindPropertyRelative("title").stringValue} {price}");
        }

        return string.Join(", ", done);
    }

    private static int Draughts()
    {
        Draught("PurifiedDraught", "Purified Draught", "Clear as meltwater. Your wounds seem further away.", new Color(0.55f, 0.9f, 0.7f), 300f,
            new[] { new StatBonus(StatType.MaxHealth, 40f), new StatBonus(StatType.MaxStamina, 15f) }, (purified, 3), "HealingHerb", 2);
        Draught("CursedDraught", "Cursed Draught", "It burns on the way down and leaves you sharp and thin-skinned.", new Color(0.65f, 0.2f, 0.45f), 180f,
            new[] { new StatBonus(StatType.DamageBonus, 0.2f), new StatBonus(StatType.Armor, -8f) }, (cursed, 5), "BogVenom", 1);
        return 2;
    }

    private static void Draught(string file, string label, string text, Color color, float seconds, StatBonus[] boon, (ResourceDefinition resource, int amount) essence, string herb, int herbs)
    {
        string path = Food + file + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        string iconPath = Icons + file + ".png";
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("category").intValue = (int)ItemCategory.Consumable;
        so.FindProperty("tier").intValue = 2;
        so.FindProperty("rarity").intValue = (int)ItemRarity.Uncommon;
        so.FindProperty("value").intValue = 12;
        so.FindProperty("weight").floatValue = 0.3f;
        so.FindProperty("icon").objectReferenceValue = icon != null ? icon : PlaceholderIconPainter.Paint(IconShape.Potion, color, Color.white, iconPath);
        so.FindProperty("maximumStack").intValue = 10;
        so.FindProperty("boonSeconds").floatValue = seconds;
        so.FindProperty("tonic").boolValue = true;
        SetBonuses(so.FindProperty("boonBonuses"), boon);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);

        string recipePath = Recipes + file + ".asset";
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(recipePath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, recipePath);
        }

        var made = new SerializedObject(recipe);
        made.FindProperty("id").stringValue = file.ToLowerInvariant();
        made.FindProperty("output").objectReferenceValue = item;
        made.FindProperty("outputAmount").intValue = 1;
        made.FindProperty("station").intValue = (int)CraftingStationType.AlchemyTable;
        made.FindProperty("craftSeconds").floatValue = 10f;
        made.FindProperty("knownFromStart").boolValue = true;
        made.FindProperty("sortOrder").intValue = 120;
        SetCost(made.FindProperty("resourceCost"), essence);
        SerializedProperty items = made.FindProperty("itemCost");
        items.arraySize = 1;
        items.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = FindMaterial(herb);
        items.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = herbs;
        made.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static ItemDefinition FindMaterial(string file)
    {
        foreach (string guid in AssetDatabase.FindAssets(file + " t:ItemDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == file)
            {
                return AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            }
        }

        throw new Exception("No item " + file);
    }

    private static void SetCost(SerializedProperty list, params (ResourceDefinition resource, int amount)[] cost)
    {
        list.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("resource").objectReferenceValue = cost[i].resource;
            entry.FindPropertyRelative("amount").intValue = cost[i].amount;
        }
    }

    private static void SetBonuses(SerializedProperty list, StatBonus[] bonuses)
    {
        list.arraySize = bonuses.Length;
        for (int i = 0; i < bonuses.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("stat").intValue = (int)bonuses[i].stat;
            entry.FindPropertyRelative("op").intValue = (int)bonuses[i].op;
            entry.FindPropertyRelative("value").floatValue = bonuses[i].value;
        }
    }

    private static void Refresh()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeBook"))
        {
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(AssetDatabase.GUIDToAssetPath(guid));
            book.CollectAll();
            EditorUtility.SetDirty(book);
        }
    }
}
