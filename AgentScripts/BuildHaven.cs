using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Base building content (`unity command run_script --file AgentScripts/BuildHaven.cs --entry BuildHaven.Apply`):
/// ConstructionSettings and HavenSettings (Resources), the materials they use, build work, renown and upgrade levels on
/// every building, three decorations (Lantern Post, Haven Banner, Herb Planter, made with BuildingPrefabBuilder),
/// Construction on every building prefab, the overgrowth nodes (made with ResourceNodePrefabBuilder; they never grow
/// back) and, in Safe Haven, overgrowth scattered over the clearing plus a ruined Storage Shed to repair.
/// Safe to run again (it replaces the overgrowth it placed). Reopens the scene that was open.
/// </summary>
public static class BuildHaven
{
    private const string Haven = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string Materials = "Assets/_Project/Materials/Haven/";
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Buildings = "Assets/_Project/Data/Buildings/";

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        Material blight = Settings();
        string levels = Levels();
        string decor = Decorations();
        string prefabs = AddConstruction();
        Dictionary<string, GameObject> overgrowth = OvergrowthNodes(blight);
        AssetDatabase.SaveAssets();
        string placed = PlaceInHaven(overgrowth);
        if (!string.IsNullOrEmpty(previous) && previous != Haven) EditorSceneManager.OpenScene(previous);
        return $"{levels}; {decor}; {prefabs}; overgrowth kinds {overgrowth.Count}; {placed}";
    }

    // ----- Settings and materials -----

    private static Material Settings()
    {
        Material cloth = MakeMaterial("Banner Cloth", new Color(0.55f, 0.12f, 0.1f), Color.black);
        Material lantern = MakeMaterial("Lantern Glow", new Color(1f, 0.8f, 0.5f), new Color(2.2f, 1.4f, 0.6f));
        Material blight = BlightMaterial();

        var construction = Asset<ConstructionSettings>("Assets/_Project/Resources/ConstructionSettings.asset");
        var so = new SerializedObject(construction);
        so.FindProperty("scaffoldMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        so.FindProperty("stoneMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Data/Prologue/Shelter Stone.mat");
        so.FindProperty("clothMaterial").objectReferenceValue = cloth;
        so.FindProperty("lanternMaterial").objectReferenceValue = lantern;
        so.FindProperty("dustMaterial").objectReferenceValue = DustMaterial();
        so.FindProperty("hammerSound").objectReferenceValue = Sound("Structure Hit");
        so.FindProperty("finishSound").objectReferenceValue = Sound("Built");
        so.ApplyModifiedPropertiesWithoutUndo();

        var haven = Asset<HavenSettings>("Assets/_Project/Resources/HavenSettings.asset");
        var havenData = new SerializedObject(haven);
        havenData.FindProperty("rankSound").objectReferenceValue = Sound("Quest Completed");
        havenData.ApplyModifiedPropertiesWithoutUndo();
        return blight;
    }

    private static Material MakeMaterial(string name, Color colour, Color emission)
    {
        string path = Materials + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", colour);
        material.SetFloat("_Smoothness", 0.2f);
        if (emission.maxColorComponent > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    // A soft round puff for dust and healing motes (particles).
    private static Material DustMaterial()
    {
        string texturePath = Materials + "Dust Puff.png";
        if (!File.Exists(texturePath))
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float alpha = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        string path = Materials + "Dust Puff.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Dark, dead ground under the overgrowth: a soft blotch fading out at the edge.
    private static Material BlightMaterial()
    {
        string texturePath = Materials + "Blight.png";
        if (!File.Exists(texturePath))
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float noise = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.35f;
                    float alpha = Mathf.Clamp01((1f - distance - noise * 0.6f) * 1.6f) * 0.85f;
                    float shade = 0.75f + Mathf.PerlinNoise(x * 0.2f + 7f, y * 0.2f) * 0.25f;
                    texture.SetPixel(x, y, new Color(0.2f * shade, 0.16f * shade, 0.1f * shade, alpha));
                }
            }

            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        string path = Materials + "Blight.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Smoothness", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10;
        EditorUtility.SetDirty(material);
        return material;
    }

    // ----- Levels -----

    private static string Levels()
    {
        int done = 0;
        done += Set("Campfire", 0f, 3, Level("Stone Hearth", "A ring of stones that holds the heat.", 3, Cost(("GreyStone", 6)), speed: 0.25f));
        done += Set("ConstructionTable", 6f, 6,
            Level("Sturdy", "Braced legs and a proper vice.", 4, Cost(("OakPlank", 6), ("GreyStone", 4)), speed: 0.25f, queue: 1),
            Level("Master's", "Every tool has its place on the wall.", 6, Cost(("OakPlank", 10), ("CopperIngot", 2)), speed: 0.25f, queue: 2));
        done += Set("Sawbench", 6f, 6,
            Level("Sturdy", "A heavier blade that bites deeper.", 4, Cost(("OakWood", 8), ("GreyStone", 4)), speed: 0.3f, queue: 1),
            Level("Master's", "A treadle saw that never tires.", 6, Cost(("OakPlank", 8), ("IronIngot", 2)), speed: 0.3f, output: 5));
        done += Set("Furnace", 8f, 8,
            Level("Sturdy", "Thicker walls hold the fire in.", 5, Cost(("GreyStone", 10), ("StoneBlock", 2)), speed: 0.25f),
            Level("Master's", "A bellows that breathes on its own.", 7, Cost(("StoneBlock", 6), ("GraniteBlock", 2)), speed: 0.25f, queue: 2));
        done += Set("Forge", 10f, 10,
            Level("Sturdy", "An anvil that rings true.", 6, Cost(("IronIngot", 3), ("StoneBlock", 6)), speed: 0.25f, queue: 1),
            Level("Master's", "Hammers for every metal.", 8, Cost(("IronIngot", 6), ("GraniteBlock", 4)), speed: 0.25f, output: 5));
        done += Set("Cauldron", 5f, 6,
            Level("Sturdy", "A hook to hang herbs to dry.", 4, Cost(("OakWood", 6), ("GreyStone", 6)), speed: 0.3f),
            Level("Master's", "A cook's shelf of spices from far away.", 6, Cost(("OakPlank", 6), ("CopperIngot", 2)), speed: 0.3f, queue: 2));
        done += Set("Loom", 6f, 6,
            Level("Sturdy", "Tight threads, steady frame.", 4, Cost(("OakPlank", 6), ("NettleCord", 4)), speed: 0.3f),
            Level("Master's", "A loom fit for a jarl's hall.", 6, Cost(("OakPlank", 10), ("Linen", 4)), speed: 0.3f, queue: 2));
        done += Set("StorageShed", 8f, 6,
            Level("Sturdy", "Shelves up to the rafters.", 4, Cost(("OakPlank", 8), ("GreyStone", 6)), storage: 6),
            Level("Grand", "A loft for everything that doesn't fit.", 6, Cost(("OakPlank", 14), ("StoneBlock", 4)), storage: 10));
        done += Set("SmallChest", 2f, 2, Level("Bound", "Iron bands and a sturdier lid.", 2, Cost(("OakWood", 4), ("SmallRocks", 4)), storage: 4));
        done += Set("PurificationAltar", 8f, 10,
            Level("Carved", "Runes cut deep around the basin.", 6, Cost(("GreyStone", 12), ("StoneBlock", 2))),
            Level("Radiant", "It hums softly at night.", 8, Cost(("StoneBlock", 6), ("GraniteBlock", 3))));
        done += Set("Torch", 0f, 1);
        done += Set("Palisade", 0f, 1);
        return $"levels on {done} buildings";
    }

    private static int Set(string file, float work, int renown, params BuildingLevel[] levels)
    {
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + file + ".asset");
        if (building == null) return 0;
        building.ConfigureBuildingUp(work, renown, levels);
        EditorUtility.SetDirty(building);
        return 1;
    }

    private static BuildingLevel Level(string title, string text, int renown, List<ItemAmount> cost, float speed = 0f, int queue = 0, int output = 0, int storage = 0) =>
        new BuildingLevel { title = title, description = text, renown = renown, cost = cost, work = 6f + renown * 0.5f, craftSpeed = speed, extraQueue = queue,
            extraOutput = output, extraStorage = storage };

    private static List<ItemAmount> Cost(params (string item, int amount)[] parts)
    {
        var cost = new List<ItemAmount>();
        foreach ((string item, int amount) in parts)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "Materials/" + item + ".asset")
                             ?? AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + item + ".asset");
            if (definition == null) throw new System.Exception("No item " + item);
            cost.Add(new ItemAmount(definition, amount));
        }

        return cost;
    }

    // ----- Decorations -----

    private static string Decorations()
    {
        var made = new List<string>();
        made.Add(Decoration("LanternPost", "Lantern Post", "A warm light on a post. Lanterns along a path make a village.", 3, 2f,
            Cost(("OakWood", 2), ("Sticks", 2)), LanternModel));
        made.Add(Decoration("HavenBanner", "Haven Banner", "Safe Haven's colours, raised for all the forest to see.", 4, 3f,
            Cost(("OakWood", 2), ("NettleCord", 2)), BannerModel));
        made.Add(Decoration("HerbPlanter", "Herb Planter", "A box of herbs and flowers. Something growing on purpose.", 4, 3f,
            Cost(("OakWood", 3), ("NettleFiber", 2)), PlanterModel));
        return "decorations " + string.Join(", ", made);
    }

    private static string Decoration(string file, string label, string text, int renown, float work, List<ItemAmount> cost, System.Func<GameObject> model)
    {
        string path = Buildings + file + ".asset";
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }

        var so = new SerializedObject(building);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        SerializedProperty items = so.FindProperty("constructionItems");
        items.arraySize = cost.Count;
        for (int i = 0; i < cost.Count; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = cost[i].Item;
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].Amount;
        }

        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("showInBuildMenu").boolValue = true;
        so.FindProperty("sortOrder").intValue = 900;
        so.ApplyModifiedPropertiesWithoutUndo();
        building.ConfigureBuildingUp(work, renown, new BuildingLevel[0]);

        if (building.Prefab == null)
        {
            // The builder wants a model asset: the procedural shape is saved as one first.
            const string models = "Assets/_Project/Art/Haven/Models";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Haven")) AssetDatabase.CreateFolder("Assets/_Project/Art", "Haven");
            if (!AssetDatabase.IsValidFolder(models)) AssetDatabase.CreateFolder("Assets/_Project/Art/Haven", "Models");
            GameObject shape = model();
            try
            {
                GameObject modelAsset = PrefabUtility.SaveAsPrefabAsset(shape, $"{models}/{file} Model.prefab");
                GameObject prefab = BuildingPrefabBuilder.Create(building, modelAsset);
                so.Update();
                so.FindProperty("prefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                Object.DestroyImmediate(shape);
            }
        }

        EditorUtility.SetDirty(building);
        return label;
    }

    private static GameObject LanternModel()
    {
        var root = new GameObject("Lantern Post Model");
        Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(0.14f, 2f, 0.14f), "Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        Piece(root.transform, PrimitiveType.Cube, new Vector3(0.25f, 1.9f, 0f), new Vector3(0.5f, 0.08f, 0.08f), "Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        Transform lamp = Piece(root.transform, PrimitiveType.Cube, new Vector3(0.45f, 1.68f, 0f), new Vector3(0.22f, 0.3f, 0.22f), Materials + "Lantern Glow.mat");
        var light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(lamp, false);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.72f, 0.4f);
        light.intensity = 2f;
        light.range = 7f;
        light.shadows = LightShadows.None;
        light.gameObject.AddComponent<LightFlicker>();
        light.gameObject.AddComponent<VisibilitySource>().Configure(5f, 0.5f, true, 6f);
        return root;
    }

    private static GameObject BannerModel()
    {
        var root = new GameObject("Haven Banner Model");
        Piece(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.1f, 1.6f, 0.1f), "Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 2.9f, 0f), new Vector3(0.9f, 0.06f, 0.06f), "Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 2.15f, 0.02f), new Vector3(0.8f, 1.4f, 0.03f), Materials + "Banner Cloth.mat");
        Piece(root.transform, PrimitiveType.Sphere, new Vector3(0f, 3.25f, 0f), Vector3.one * 0.16f, Materials + "Lantern Glow.mat");
        return root;
    }

    private static GameObject PlanterModel()
    {
        var root = new GameObject("Herb Planter Model");
        Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.4f, 0.6f), "Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f), new Vector3(1.3f, 0.05f, 0.5f), "Assets/_Project/Materials/ResourceNodes/GraniteRock.mat");
        var bush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Generated/Grass/Bush.prefab");
        for (int i = 0; i < 3 && bush != null; i++)
        {
            var plant = (GameObject)PrefabUtility.InstantiatePrefab(bush);
            PrefabUtility.UnpackPrefabInstance(plant, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            plant.transform.SetParent(root.transform, false);
            plant.transform.localPosition = new Vector3(-0.42f + i * 0.42f, 0.4f, 0f);
            plant.transform.localScale = Vector3.one * 0.35f;
            foreach (Collider collider in plant.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        }

        return root;
    }

    private static Transform Piece(Transform parent, PrimitiveType shape, Vector3 position, Vector3 scale, string material)
    {
        GameObject piece = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(material);
        return piece.transform;
    }

    // ----- Construction on every building -----

    private static string AddConstruction()
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingDefinition", new[] { "Assets/_Project/Data" }))
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (building == null || building.Prefab == null || building.Faction != Faction.Player) continue;
            string path = AssetDatabase.GetAssetPath(building.Prefab);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<Construction>() != null || root.GetComponent<BuildingObject>() == null) continue;
                root.AddComponent<Construction>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                added++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return $"Construction added to {added} prefabs";
    }

    // ----- Overgrowth -----

    private static Dictionary<string, GameObject> OvergrowthNodes(Material blight)
    {
        const string dir = "Assets/_Project/Data/Gathering/Overgrowth";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/_Project/Data/Gathering", "Overgrowth");
        const string world = "Assets/_Project/Prefabs/Environment/World/";
        var made = new Dictionary<string, GameObject>
        {
            ["Bramble"] = Node(dir, "Bramble Thicket", ToolType.None, 1, InteractionAnimation.GatherLow, 2.5f, 1, "FibreFeedback",
                new[] { "Assets/_Project/Art/Generated/Grass/Bush.prefab" }, new Vector2(0.9f, 1.3f), 1.4f, blight, ("Sticks", 1, 2), ("NettleFiber", 1, 2)),
            ["Log"] = Node(dir, "Fallen Log", ToolType.Axe, 1, InteractionAnimation.MineGround, 3f, 3, "WoodFeedback",
                new[] { world + "Rotting Log.prefab", "Assets/_Project/Art/Forest/Prefabs/Fallen Log.prefab" }, new Vector2(0.9f, 1.1f), 1.8f, blight, ("OakWood", 2, 3)),
            ["Stump"] = Node(dir, "Old Stump", ToolType.Axe, 3, InteractionAnimation.MineGround, 4f, 4, "WoodFeedback",
                new[] { world + "Old Stump A.prefab", world + "Old Stump B.prefab" }, new Vector2(1.1f, 1.4f), 1.9f, blight, ("OakWood", 3, 5), ("Hardwood", 0, 1)),
            ["Boulder"] = Node(dir, "Boulder", ToolType.Pickaxe, 1, InteractionAnimation.MineGround, 3f, 3, "StoneFeedback",
                new[] { world + "Mossy Boulder 1.prefab", world + "Mossy Boulder 2.prefab", world + "Mossy Boulder 3.prefab" }, new Vector2(0.55f, 0.75f), 1.5f, blight,
                ("GreyStone", 2, 3), ("SmallRocks", 1, 2)),
            ["BigBoulder"] = Node(dir, "Great Boulder", ToolType.Pickaxe, 3, InteractionAnimation.MineGround, 4f, 5, "StoneFeedback",
                new[] { world + "Mossy Boulder 4.prefab", world + "Mossy Boulder 5.prefab", world + "Mossy Boulder 6.prefab" }, new Vector2(1.1f, 1.4f), 2.4f, blight,
                ("GreyStone", 4, 6), ("Granite", 1, 2)),
        };
        return made;
    }

    private static GameObject Node(string dir, string label, ToolType tool, int tier, InteractionAnimation animation, float seconds, int swings, string feedback,
        string[] models, Vector2 scale, float blightRadius, Material blight, params (string item, int min, int max)[] yields)
    {
        string path = $"{dir}/{label.Replace(" ", "")}.asset";
        var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(path);
        if (node == null)
        {
            node = ScriptableObject.CreateInstance<GatheringNodeDefinition>();
            AssetDatabase.CreateAsset(node, path);
        }

        var so = new SerializedObject(node);
        so.FindProperty("id").stringValue = "overgrowth_" + label.Replace(" ", "").ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("tier").intValue = Mathf.Max(1, tier);
        SerializedProperty entries = so.FindProperty("yields").FindPropertyRelative("entries");
        entries.arraySize = yields.Length;
        for (int i = 0; i < yields.Length; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("item").objectReferenceValue = Cost((yields[i].item, 1))[0].Item;
            entry.FindPropertyRelative("resource").objectReferenceValue = null;
            entry.FindPropertyRelative("minQuantity").intValue = yields[i].min;
            entry.FindPropertyRelative("maxQuantity").intValue = yields[i].max;
            entry.FindPropertyRelative("probability").floatValue = 1f;
        }

        so.FindProperty("gatherTool").intValue = (int)tool;
        so.FindProperty("requiredToolTier").intValue = Mathf.Max(1, tier);
        so.FindProperty("gatherAnimation").intValue = (int)animation;
        so.FindProperty("gatherSeconds").floatValue = seconds;
        so.FindProperty("swings").intValue = swings;
        so.FindProperty("gatherRange").floatValue = 1.8f;
        so.FindProperty("depleteOnGather").boolValue = true;
        so.FindProperty("respawnSeconds").floatValue = 0f;
        so.FindProperty("feedback").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"Assets/_Project/Data/Gathering/Feedback/{feedback}.asset");
        SerializedProperty variants = so.FindProperty("modelVariants");
        variants.arraySize = models.Length;
        for (int i = 0; i < models.Length; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(models[i]);
        so.FindProperty("variantScale").vector2Value = scale;
        so.FindProperty("worldModelPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(models[0]);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(node);

        GameObject prefab = node.WorldPrefab;
        if (prefab == null)
        {
            prefab = ResourceNodePrefabBuilder.Create(node, AssetDatabase.LoadAssetAtPath<GameObject>(models[0]));

            so.Update();
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Overgrowth overgrowth = root.GetComponent<Overgrowth>() ?? root.AddComponent<Overgrowth>();
            var data = new SerializedObject(overgrowth);
            data.FindProperty("blightRadius").floatValue = blightRadius;
            data.FindProperty("blightMaterial").objectReferenceValue = blight;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    // ----- Safe Haven -----

    private static string PlaceInHaven(Dictionary<string, GameObject> kinds)
    {
        Scene scene = EditorSceneManager.OpenScene(Haven);
        HomePortal tree = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        Vector3 centre = tree.transform.position;
        GameObject old = GameObject.Find("Overgrowth");
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject("Overgrowth").transform;
        var random = new System.Random(7);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);

        // Keep clear of what stands: the tree, the portal, arrival points, nodes and everything else with a collider.
        var keepClear = new List<(Vector3 at, float radius)> { (centre, 5f) };
        foreach (RaidPortal portal in Object.FindObjectsByType<RaidPortal>(FindObjectsInactive.Include)) keepClear.Add((portal.transform.position, 6f));
        foreach (PlayerArrivalPoint point in Object.FindObjectsByType<PlayerArrivalPoint>(FindObjectsInactive.Include)) keepClear.Add((point.transform.position, 3.5f));
        foreach (ResourceNode node in Object.FindObjectsByType<ResourceNode>(FindObjectsInactive.Include)) keepClear.Add((node.transform.position, 2.5f));
        foreach (WaterSource water in Object.FindObjectsByType<WaterSource>(FindObjectsInactive.Include)) keepClear.Add((water.transform.position, 3f));

        // A ruined shed near the tree, waiting to be repaired.
        GameObject oldRuin = GameObject.Find("Ruined Storage Shed");
        if (oldRuin != null) Object.DestroyImmediate(oldRuin);
        var shed = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + "StorageShed.asset");
        string ruinAt = "none";
        for (int tries = 0; tries < 200; tries++)
        {
            float angle = Range(0f, Mathf.PI * 2f);
            Vector3 at = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(9f, 13f);
            if (!NavMesh.SamplePosition(at + Vector3.up * 3f, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
            at = hit.position;
            if (keepClear.Any(c => Vector3.Distance(Flat(c.at), Flat(at)) < c.radius + 2.5f)) continue;
            var ruin = (GameObject)PrefabUtility.InstantiatePrefab(shed.Prefab, scene);
            ruin.name = "Ruined Storage Shed";
            ruin.transform.SetPositionAndRotation(at, Quaternion.LookRotation(Flat(centre - at)));
            var data = new SerializedObject(ruin.GetComponent<Construction>());
            data.FindProperty("startsRuined").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            ruinAt = at.ToString("F0");
            keepClear.Add((at, 5f));
            break;
        }

        (string kind, int count, float near, float far)[] plan =
        {
            ("Bramble", 14, 6f, 22f), ("Log", 8, 7f, 22f), ("Boulder", 7, 7f, 22f), ("Stump", 5, 9f, 24f), ("BigBoulder", 4, 10f, 26f),
        };
        int placed = 0;
        foreach ((string kind, int count, float near, float far) in plan)
        {
            for (int i = 0, tries = 0; i < count && tries < 400; tries++)
            {
                float angle = Range(0f, Mathf.PI * 2f);
                float distance = Range(near, far);
                Vector3 at = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                if (!NavMesh.SamplePosition(at + Vector3.up * 3f, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
                at = hit.position;
                if (keepClear.Any(c => Vector3.Distance(Flat(c.at), Flat(at)) < c.radius)) continue;
                if (Physics.OverlapSphere(at + Vector3.up * 0.8f, 1.2f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider))) continue;
                var piece = (GameObject)PrefabUtility.InstantiatePrefab(kinds[kind], scene);
                piece.transform.SetParent(group, true);
                piece.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, Range(0f, 360f), 0f));
                keepClear.Add((at, 2.6f));
                placed++;
                i++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"placed {placed} overgrowth, ruin at {ruinAt}";
    }

    private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private static SoundDefinition Sound(string name) => AssetDatabase.LoadAssetAtPath<SoundDefinition>($"Assets/_Project/Audio/Sounds/Library/{name}.asset");

    private static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        EditorUtility.SetDirty(asset);
        return asset;
    }
}
