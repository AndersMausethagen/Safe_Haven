#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The firefly lantern and the fireflies to fill it (`unity command script run --file AgentScripts/BuildFireflyLantern.cs --entry BuildFireflyLantern.Apply`):
/// - Firefly (a material, caught at night with Interact) and the Primitive Firefly Lantern (worn in the Light slot,
///   24 in-game hours of pale green light, refilled with a firefly), with their models and icons in Art/Generated/Lantern.
/// - The lantern's hand recipe: 6 Sticks and 1 Firefly, known from the start.
/// - Both firefly profiles: catchable fireflies (Firefly), drawn to lights, and a slightly thinner, smaller swarm.
/// - PlayerLantern on the player.
/// Re-runnable.
/// </summary>
public static class BuildFireflyLantern
{
    private const string Folder = "Assets/_Project/Art/Generated/Lantern";
    private const string FireflyItem = "Assets/_Project/Data/Items/Materials/Firefly.asset";
    private const string LanternItem = "Assets/_Project/Data/Items/PrimitiveFireflyLantern.asset";
    private const string LanternData = "Assets/_Project/Data/Items/Lanterns/Primitive Firefly Lantern.asset";
    private const string Recipe = "Assets/_Project/Data/Crafting/Recipes/PrimitiveFireflyLantern.asset";
    private const string Sticks = "Assets/_Project/Data/Items/Materials/Sticks.asset";
    private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
    private static readonly Color Green = new Color(0.62f, 1f, 0.55f);

    public static string Apply()
    {
        var report = new List<string>();
        System.IO.Directory.CreateDirectory(Folder);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(LanternData));
        Material wood = Lit("Lantern Wood", new Color(0.33f, 0.22f, 0.13f), 0.15f);
        Material glass = Glass();
        Material speck = Speck();
        GameObject lanternModel = LanternModel(wood, glass, speck);
        GameObject fireflyModel = FireflyModel(speck);

        ItemDefinition firefly = Item(FireflyItem, "firefly", "Firefly", "A firefly, cupped in your hands and kept in a twist of cloth. " +
            "It glows on and off, unhurried. One fills a firefly lantern with light for a day.", ItemCategory.Material, true, 20, 0f, 3,
            fireflyModel, null);
        LanternDefinition lantern = Lantern(lanternModel, firefly);
        ItemDefinition lanternItem = Item(LanternItem, "primitive_firefly_lantern", "Primitive Firefly Lantern",
            "Six sticks lashed round a cage of thin bark, and fireflies inside. It hangs from the belt and gives a pale green, " +
            "breathing light for a day, leaving both hands free.", ItemCategory.Equipment, false, 1, 0.6f, 12, lanternModel, lantern);
        report.Add("items: Firefly, Primitive Firefly Lantern");

        RecipeFor(lanternItem, firefly);
        report.Add("recipe: 6 Sticks + 1 Firefly by hand");

        foreach ((string path, int night) in new[] { ("Assets/_Project/Data/World/Fireflies - Haven.asset", 90), ("Assets/_Project/Data/World/Fireflies - Misty Woods.asset", 170) })
        {
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<FireflyProfile>(path));
            so.FindProperty("nightCount").intValue = night;
            so.FindProperty("size").vector2Value = new Vector2(0.18f, 0.33f);
            // A share of the swarm round lights, spread out: with only the player's lantern near, more crowded it round them.
            so.FindProperty("lightShare").floatValue = 0.2f;
            so.FindProperty("lightDistance").vector2Value = new Vector2(1.2f, 5f);
            so.FindProperty("catchable").objectReferenceValue = firefly;
            so.FindProperty("catchableGlow").objectReferenceValue = speck;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add(System.IO.Path.GetFileNameWithoutExtension(path) + ": " + night + " at night, catchable");
        }

        GameObject player = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            if (player.GetComponent<PlayerLantern>() == null)
            {
                player.AddComponent<PlayerLantern>();
                PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefab);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }

        report.Add("PlayerLantern on the player");
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static Material Lit(string name, Color colour, float smoothness)
    {
        string path = $"{Folder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(material);
        return material;
    }

    // The lantern's glass: see-through, glowing from inside while lit (PlayerLantern drives the colour and glow).
    private static Material Glass()
    {
        Material material = Lit("Firefly Lantern Glass", new Color(Green.r * 0.6f, Green.g * 0.6f, Green.b * 0.6f, 0.6f), 0.6f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Green * 2f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(material);
        return material;
    }

    // A firefly's light: a bright unlit speck (its colour is set per firefly, above 1 so it blooms).
    private static Material Speck()
    {
        string path = $"{Folder}/Firefly Glow.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Firefly Glow" };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", Green * 4f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation ?? Quaternion.identity;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    // Hung from its top (the origin): a cord, a wooden cap and base, six sticks round a glowing glass, fireflies inside.
    private static GameObject LanternModel(Material wood, Material glass, Material speck)
    {
        var root = new GameObject("Primitive Firefly Lantern");
        Part(PrimitiveType.Cube, "Cord", root.transform, new Vector3(0f, -0.02f, 0f), new Vector3(0.006f, 0.04f, 0.006f), wood);
        Part(PrimitiveType.Cylinder, "Cap", root.transform, new Vector3(0f, -0.045f, 0f), new Vector3(0.13f, 0.01f, 0.13f), wood);
        Part(PrimitiveType.Cylinder, "Glow", root.transform, new Vector3(0f, -0.12f, 0f), new Vector3(0.1f, 0.068f, 0.1f), glass);
        Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, -0.195f, 0f), new Vector3(0.13f, 0.01f, 0.13f), wood);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            Part(PrimitiveType.Cube, "Stick " + (i + 1), root.transform, new Vector3(Mathf.Cos(angle) * 0.058f, -0.12f, Mathf.Sin(angle) * 0.058f),
                new Vector3(0.011f, 0.16f, 0.011f), wood);
        }

        Vector3[] motes = { new Vector3(0.015f, -0.1f, 0.01f), new Vector3(-0.02f, -0.13f, -0.005f), new Vector3(0.005f, -0.15f, -0.02f) };
        for (int i = 0; i < motes.Length; i++)
        {
            Part(PrimitiveType.Sphere, "Mote " + (i + 1), root.transform, motes[i], Vector3.one * 0.013f, speck);
        }

        var light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0f, -0.12f, 0f);
        light.type = LightType.Point;
        light.color = Green;
        light.range = 7f;
        light.intensity = 1.4f;
        light.shadows = LightShadows.None;
        return Save(root, "Primitive Firefly Lantern");
    }

    // For the icon (and a dropped firefly): a small dark body with a glowing tail and two wings.
    private static GameObject FireflyModel(Material speck)
    {
        var root = new GameObject("Firefly");
        Material body = Lit("Firefly Body", new Color(0.12f, 0.1f, 0.08f), 0.4f);
        Material wing = Lit("Firefly Wing", new Color(0.55f, 0.55f, 0.5f), 0.7f);
        Part(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0f, 0.02f), new Vector3(0.03f, 0.03f, 0.03f), body, Quaternion.Euler(90f, 0f, 0f));
        Part(PrimitiveType.Sphere, "Tail", root.transform, new Vector3(0f, 0f, -0.025f), new Vector3(0.034f, 0.03f, 0.045f), speck);
        Part(PrimitiveType.Cube, "Wing L", root.transform, new Vector3(-0.022f, 0.012f, 0.01f), new Vector3(0.035f, 0.002f, 0.05f), wing, Quaternion.Euler(0f, -18f, 12f));
        Part(PrimitiveType.Cube, "Wing R", root.transform, new Vector3(0.022f, 0.012f, 0.01f), new Vector3(0.035f, 0.002f, 0.05f), wing, Quaternion.Euler(0f, 18f, -12f));
        root.transform.localScale = Vector3.one * 2f;
        return Save(root, "Firefly");
    }

    private static GameObject Save(GameObject root, string name)
    {
        string path = $"{Folder}/{name}.prefab";
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return saved;
    }

    private static LanternDefinition Lantern(GameObject model, ItemDefinition refill)
    {
        var lantern = AssetDatabase.LoadAssetAtPath<LanternDefinition>(LanternData);
        if (lantern == null)
        {
            lantern = ScriptableObject.CreateInstance<LanternDefinition>();
            AssetDatabase.CreateAsset(lantern, LanternData);
        }

        var so = new SerializedObject(lantern);
        so.FindProperty("hours").floatValue = 24f;
        so.FindProperty("refill").objectReferenceValue = refill;
        so.FindProperty("colour").colorValue = Green;
        so.FindProperty("model").objectReferenceValue = model;
        so.ApplyModifiedPropertiesWithoutUndo();
        return lantern;
    }

    private static ItemDefinition Item(string path, string id, string displayName, string description, ItemCategory category,
        bool stackable, int stack, float weight, int value, GameObject model, LanternDefinition lantern)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = displayName;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("category").intValue = (int)category;
        so.FindProperty("tier").intValue = 1;
        so.FindProperty("value").intValue = value;
        so.FindProperty("stackable").boolValue = stackable;
        so.FindProperty("maximumStack").intValue = stack;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("prefab").objectReferenceValue = model;
        so.FindProperty("lantern").objectReferenceValue = lantern;
        if (so.FindProperty("icon").objectReferenceValue == null)
        {
            so.FindProperty("icon").objectReferenceValue = IconRenderer.RenderToSprite(model, $"{Folder}/{displayName} Icon.png");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void RecipeFor(ItemDefinition lantern, ItemDefinition firefly)
    {
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(Recipe);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, Recipe);
        }

        var so = new SerializedObject(recipe);
        so.FindProperty("id").stringValue = "primitive_firefly_lantern";
        so.FindProperty("output").objectReferenceValue = lantern;
        so.FindProperty("outputAmount").intValue = 1;
        SerializedProperty cost = so.FindProperty("itemCost");
        cost.arraySize = 2;
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Sticks);
        cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 6;
        cost.GetArrayElementAtIndex(1).FindPropertyRelative("item").objectReferenceValue = firefly;
        cost.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = 1;
        so.FindProperty("station").intValue = 0;
        so.FindProperty("knownFromStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);

        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        var bookSo = new SerializedObject(book);
        SerializedProperty list = bookSo.FindProperty("recipes");
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == recipe)
            {
                return;
            }
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = recipe;
        bookSo.ApplyModifiedPropertiesWithoutUndo();
    }
}
