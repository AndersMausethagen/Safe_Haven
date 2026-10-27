using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the Torch structure and makes campfires light the night (run with `unity command run_script --file
/// AgentScripts/BuildTorch.cs --entry BuildTorch.All`): a BuildingDefinition (2 Sticks, 1 Nettle Fiber, known from the
/// start), a placeholder model of a post with a burning head, its prefab through BuildingPrefabBuilder with a flickering
/// light that glows in the fog and holds back the night (VisibilitySource), and an icon. Re-running updates in place.
/// </summary>
public static class BuildTorch
{
    private const string Definition = "Assets/_Project/Data/Buildings/Torch.asset";
    private const string Campfire = "Assets/_Project/Prefabs/Structures/Campfire.prefab";
    private const string ModelPath = "Assets/_Project/Environment/Stations/TorchModel.prefab";

    public static string All()
    {
        var log = new List<string>();
        var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Definition);
        if (definition == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Buildings/Palisade.asset", Definition);
            definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Definition);
            log.Add("created definition");
        }

        var sticks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Sticks.asset");
        var fiber = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/NettleFiber.asset");
        var so = new SerializedObject(definition);
        so.FindProperty("id").stringValue = "torch";
        so.FindProperty("displayName").stringValue = "Torch";
        so.FindProperty("description").stringValue =
            "A burning torch on a post. It lights the ground around it at night, holds back the grey dark and keeps night " +
            "creatures at bay.";
        so.FindProperty("constructionCost").ClearArray();
        SerializedProperty items = so.FindProperty("constructionItems");
        items.ClearArray();
        AddItem(items, sticks, 2);
        AddItem(items, fiber, 1);
        so.FindProperty("showInBuildMenu").boolValue = true;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("sortOrder").intValue = 9;
        so.FindProperty("yardDepth").floatValue = 0f;
        so.FindProperty("placeInLines").boolValue = false;
        so.FindProperty("prefab").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        // The builder takes a model asset, like the Content Editor's model field.
        GameObject sceneModel = BuildModel();
        GameObject model;
        try
        {
            model = PrefabUtility.SaveAsPrefabAsset(sceneModel, ModelPath);
        }
        finally
        {
            Object.DestroyImmediate(sceneModel);
        }

        string existing = BuildingPrefabBuilder.PrefabFolder + "/Torch.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(existing) != null)
        {
            AssetDatabase.DeleteAsset(existing);
            log.Add("rebuilt prefab");
        }

        GameObject prefab = BuildingPrefabBuilder.Create(definition, model);

        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var lightObject = new GameObject("Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            var fire = lightObject.AddComponent<Light>();
            fire.type = LightType.Point;
            fire.color = new Color(1f, 0.62f, 0.3f);
            fire.intensity = 6f;
            fire.range = 9f;
            fire.shadows = LightShadows.None;
            lightObject.AddComponent<LightFlicker>();
            lightObject.AddComponent<AtmosphereLight>();
            Sight(root, 9f, 12f);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        so = new SerializedObject(definition);
        so.FindProperty("prefab").objectReferenceValue = prefab;
        Sprite icon = IconRenderer.RenderToSprite(prefab, "Assets/_Project/UI/BuildingIcons/Torch.png");
        if (icon != null)
        {
            so.FindProperty("icon").objectReferenceValue = icon;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        prefab.GetComponent<SaveableEntity>().RefreshPrefabId();
        log.Add(CampfireLight());
        AssetDatabase.SaveAssets();

        List<string> problems = BuildingPrefabBuilder.FindProblems(prefab, definition);
        log.Add($"torch prefab {prefabPath}; icon={(icon != null)}; problems={(problems.Count == 0 ? "none" : string.Join(", ", problems))}");
        return string.Join("; ", log);
    }

    // A campfire lights the night like a torch, a little further.
    private static string CampfireLight()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Campfire);
        try
        {
            Light fire = root.GetComponentInChildren<Light>(true);
            if (fire.GetComponent<AtmosphereLight>() == null)
            {
                fire.gameObject.AddComponent<AtmosphereLight>();
            }

            if (fire.GetComponent<LightFlicker>() == null)
            {
                fire.gameObject.AddComponent<LightFlicker>();
            }

            Sight(root, 10f, 12f);
            PrefabUtility.SaveAsPrefabAsset(root, Campfire);
            return "campfire lights the night";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Sight(GameObject root, float radius, float reveal)
    {
        var sight = root.GetComponent<VisibilitySource>() ?? root.AddComponent<VisibilitySource>();
        var data = new SerializedObject(sight);
        data.FindProperty("radius").floatValue = radius;
        data.FindProperty("softness").floatValue = 0.4f;
        data.FindProperty("reveals").boolValue = true;
        data.FindProperty("revealRadius").floatValue = reveal;
        data.FindProperty("followsWard").boolValue = false;
        data.FindProperty("isLight").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    // A wooden post with a pitch-soaked head and a flame, from primitives and the stations' materials.
    private static GameObject BuildModel()
    {
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Stations/StationWood.mat");
        var flame = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Stations/StationFire.mat");
        var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Stations/StationStone.mat");
        var model = new GameObject("TorchModel");
        Part(model, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(0.14f, 0.85f, 0.14f), wood);
        Part(model, PrimitiveType.Cylinder, new Vector3(0f, 1.78f, 0f), new Vector3(0.22f, 0.1f, 0.22f), wood);
        Part(model, PrimitiveType.Sphere, new Vector3(0f, 1.92f, 0f), new Vector3(0.26f, 0.42f, 0.26f), flame);
        Part(model, PrimitiveType.Sphere, new Vector3(0.08f, 0.05f, 0.05f), new Vector3(0.3f, 0.18f, 0.28f), stone);
        Part(model, PrimitiveType.Sphere, new Vector3(-0.1f, 0.05f, -0.04f), new Vector3(0.26f, 0.16f, 0.3f), stone);
        return model;
    }

    private static void Part(GameObject model, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(shape);
        part.name = shape.ToString();
        Object.DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(model.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static void AddItem(SerializedProperty items, ItemDefinition item, int amount)
    {
        items.InsertArrayElementAtIndex(items.arraySize);
        SerializedProperty entry = items.GetArrayElementAtIndex(items.arraySize - 1);
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("amount").intValue = amount;
    }
}
