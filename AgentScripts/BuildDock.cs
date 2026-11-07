using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The Dock and the Fisher (`unity command script run --file AgentScripts/BuildDock.cs --entry BuildDock.Apply`): a pier
/// with nets, lines and traps (CraftingStationType.Dock) whose catches train Fishing (CraftedGoods.Catch) and which is
/// Fishing's mentor building, its four catch recipes, and the Fisher job that keeps the nets out while the player is away
/// and gives lessons in Fishing; Gunnar and Yrsa can take it. The model is generated from primitives. Re-runnable; run
/// BuildProficiencies afterwards so Fishing knows its mentor and the Fisher teaches. Touches no scene.
/// </summary>
public static class BuildDock
{
    private const string Buildings = "Assets/_Project/Data/Buildings/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Models = "Assets/_Project/Art/Haven/Models/";
    private const string Icons = "Assets/_Project/Art/Haven/Icons/";
    private const string Tints = "Assets/_Project/Materials/Haven/";
    private const string Jobs = "Assets/_Project/Data/Settlement/";

    private static Material plank;
    private static Material darkPlank;
    private static Material iron;
    private static Material cord;

    public static string Apply()
    {
        plank = Tint("Cart Plank", new Color(0.46f, 0.31f, 0.18f));
        darkPlank = Tint("Cart Plank Dark", new Color(0.32f, 0.21f, 0.12f));
        iron = Tint("Dark Iron", new Color(0.2f, 0.21f, 0.24f));
        cord = Tint("Net Cord", new Color(0.62f, 0.56f, 0.42f));

        // What the Dock brings in: a net of perch for cord, trout on grub-baited lines, and from the Long Pier pike traps
        // and deep lines for char.
        var nets = Recipe("DockNets", "Set the Nets", "Perch", 3, 45f, 1, new[] { ("Materials/NettleCord", 1) }, 1);
        var lines = Recipe("DockTroutLines", "Trout Lines", "BrookTrout", 2, 60f, 1, new[] { ("Materials/Grubs", 2) }, 2);
        var traps = Recipe("DockPikeTraps", "Pike Traps", "Pike", 1, 90f, 2, new[] { ("Materials/OakPlank", 1), ("Materials/Grubs", 1) }, 3);
        var deep = Recipe("DockDeepLines", "Deep Lines", "ArcticChar", 1, 120f, 2, new[] { ("Materials/NettleCord", 1), ("Materials/Grubs", 3) }, 4);
        BuildingDefinition dock = Dock();
        JobDefinition fisher = Fisher(dock, nets, lines, traps);
        string residents = OfferTo(fisher, "Gunnar", "Yrsa");
        AssetDatabase.SaveAssets();
        Refresh();
        AssetDatabase.SaveAssets();
        return $"dock, 4 catches ({nets.name}, {lines.name}, {traps.name}, {deep.name}), fisher; {residents}";
    }

    private static RecipeDefinition Recipe(string file, string label, string fish, int amount, float seconds, int tier,
        (string item, int amount)[] cost, int order)
    {
        string path = Recipes + file + ".asset";
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        var so = new SerializedObject(recipe);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("output").objectReferenceValue = Item("Fish/" + fish);
        so.FindProperty("outputAmount").intValue = amount;
        so.FindProperty("station").intValue = (int)CraftingStationType.Dock;
        so.FindProperty("craftSeconds").floatValue = seconds;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("minimumStationTier").intValue = tier;
        so.FindProperty("sortOrder").intValue = order;
        SerializedProperty items = so.FindProperty("itemCost");
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Item(cost[i].item);
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
        return recipe;
    }

    private static BuildingDefinition Dock()
    {
        string path = Buildings + "Dock.asset";
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }

        var so = new SerializedObject(building);
        so.FindProperty("id").stringValue = "dock";
        so.FindProperty("displayName").stringValue = "Dock";
        so.FindProperty("description").stringValue = "A short pier, a drying rack and a coil of nets. Set at the water's edge, it brings in fish while " +
                                                       "you are about other things, and anyone who fishes learns faster near it.";
        so.FindProperty("combatStats").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>("Assets/_Project/Data/Combat/Stats/WoodenStructureStats.asset");
        SerializedProperty items = so.FindProperty("constructionItems");
        (string item, int amount)[] cost = { ("Materials/OakPlank", 12), ("Materials/OakWood", 6), ("Materials/NettleCord", 4) };
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Item(cost[i].item);
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.FindProperty("station").intValue = (int)CraftingStationType.Dock;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("showInBuildMenu").boolValue = true;
        so.FindProperty("sortOrder").intValue = 19;
        so.FindProperty("placeInLines").boolValue = false;
        if (so.FindProperty("icon").objectReferenceValue == null)
        {
            so.FindProperty("icon").objectReferenceValue = IconRenderer.RenderToSprite(Model(), Icons + "Dock Icon.png");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        var level = new BuildingLevel
        {
            title = "Long Pier",
            description = "Out past the reeds: pike traps and deep lines for char, and the nets come in sooner.",
            renown = 5,
            work = 8f,
            craftSpeed = 0.3f,
            extraQueue = 1,
            cost = new List<ItemAmount> { new ItemAmount(Item("Materials/OakPlank"), 10), new ItemAmount(Item("Materials/IronIngot"), 1) },
        };
        building.ConfigureBuildingUp(9f, 8, new[] { level });
        EditorUtility.SetDirty(building);
        if (building.Prefab == null)
        {
            GameObject prefab = BuildingPrefabBuilder.Create(building, Model());
            so.Update();
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        string prefabPath = AssetDatabase.GetAssetPath(building.Prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (root.GetComponent<Construction>() == null)
            {
                root.AddComponent<Construction>();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return building;
    }

    // A plank pier on posts, a drying rack with a net hung over it, a coil of rope, a barrel and a lantern post.
    private static GameObject Model()
    {
        string path = Models + "Dock Model.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject("Dock Model");
        Transform t = root.transform;
        for (int i = 0; i < 9; i++)
        {
            Piece(t, PrimitiveType.Cube, new Vector3(0f, 0.42f, -1.6f + i * 0.4f), new Vector3(1.8f, 0.07f, 0.36f), i % 2 == 0 ? plank : darkPlank);
        }

        foreach (float x in new[] { -0.85f, 0.85f })
        {
            foreach (float z in new[] { -1.6f, 0f, 1.6f })
            {
                Piece(t, PrimitiveType.Cylinder, new Vector3(x, 0.15f, z), new Vector3(0.16f, 0.45f, 0.16f), darkPlank);
            }

            Piece(t, PrimitiveType.Cube, new Vector3(x, 0.34f, 0f), new Vector3(0.1f, 0.1f, 3.4f), darkPlank);
        }

        // The drying rack: two uprights, a pole, and a net draped over it.
        foreach (float z in new[] { -0.9f, 0.5f })
        {
            Piece(t, PrimitiveType.Cube, new Vector3(-0.6f, 1.05f, z), new Vector3(0.08f, 1.25f, 0.08f), plank);
        }

        Piece(t, PrimitiveType.Cylinder, new Vector3(-0.6f, 1.66f, -0.2f), new Vector3(0.06f, 0.75f, 0.06f), plank).localRotation = Quaternion.Euler(90f, 0f, 0f);
        Piece(t, PrimitiveType.Cube, new Vector3(-0.66f, 1.3f, -0.2f), new Vector3(0.02f, 0.7f, 1.3f), cord).localRotation = Quaternion.Euler(0f, 0f, 8f);
        Piece(t, PrimitiveType.Cube, new Vector3(-0.54f, 1.32f, -0.2f), new Vector3(0.02f, 0.66f, 1.25f), cord).localRotation = Quaternion.Euler(0f, 0f, -8f);

        // A coil of rope, a barrel, and a lantern post at the far end.
        Piece(t, PrimitiveType.Cylinder, new Vector3(0.45f, 0.5f, -1.1f), new Vector3(0.55f, 0.06f, 0.55f), cord);
        Piece(t, PrimitiveType.Cylinder, new Vector3(0.5f, 0.75f, 0.6f), new Vector3(0.5f, 0.32f, 0.5f), darkPlank);
        Piece(t, PrimitiveType.Cylinder, new Vector3(0.5f, 0.75f, 0.6f), new Vector3(0.52f, 0.03f, 0.52f), iron);
        Piece(t, PrimitiveType.Cube, new Vector3(0.75f, 1.1f, 1.65f), new Vector3(0.1f, 1.4f, 0.1f), darkPlank);
        Piece(t, PrimitiveType.Cube, new Vector3(0.6f, 1.75f, 1.65f), new Vector3(0.35f, 0.06f, 0.06f), darkPlank);
        Piece(t, PrimitiveType.Cube, new Vector3(0.45f, 1.6f, 1.65f), new Vector3(0.14f, 0.2f, 0.14f), iron);

        PrefabBuildUtility.EnsureFolder(Models.TrimEnd('/'));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static JobDefinition Fisher(BuildingDefinition dock, RecipeDefinition nets, RecipeDefinition lines, RecipeDefinition traps)
    {
        string path = Jobs + "Fisher.asset";
        var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
        if (job == null)
        {
            job = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(job, path);
        }

        var so = new SerializedObject(job);
        so.FindProperty("id").stringValue = "job.fisher";
        so.FindProperty("displayName").stringValue = "Fisher";
        so.FindProperty("description").stringValue = "Works the Dock: keeps the nets and lines out while you are away, so there is fish in the stash, " +
                                                       "and gives lessons in Fishing.";
        so.FindProperty("acceptLine").stringValue = "\"The water's patient. So am I.\"";
        so.FindProperty("workplace").objectReferenceValue = dock;
        so.FindProperty("workGesture").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<EmoteDefinition>("Assets/_Project/Data/Characters/Emotes/Fishing.asset");
        so.FindProperty("workTool").objectReferenceValue = Item("FishingRod");
        so.FindProperty("workToolScale").floatValue = 1f;
        so.FindProperty("workSound").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Footstep Water.asset");
        so.FindProperty("workSoundEvery").vector2Value = new Vector2(4f, 9f);
        SerializedProperty orders = so.FindProperty("workOrders");
        (RecipeDefinition recipe, int stock, int keep)[] work = { (nets, 20, 5), (lines, 15, 6), (traps, 6, 6) };
        orders.arraySize = work.Length;
        for (int i = 0; i < work.Length; i++)
        {
            SerializedProperty order = orders.GetArrayElementAtIndex(i);
            order.FindPropertyRelative("recipe").objectReferenceValue = work[i].recipe;
            order.FindPropertyRelative("stockTarget").intValue = work[i].stock;
            order.FindPropertyRelative("keepInput").intValue = work[i].keep;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(job);
        return job;
    }

    // Adds the job to who can take it, after the jobs they have.
    private static string OfferTo(JobDefinition job, params string[] names)
    {
        var offered = new List<string>();
        foreach (string name in names)
        {
            ResidentDefinition resident = null;
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:ResidentDefinition", new[] { "Assets/_Project/Data" }))
            {
                var found = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (found != null && found.name == name)
                {
                    resident = found;
                }
            }

            if (resident == null)
            {
                continue;
            }

            var so = new SerializedObject(resident);
            SerializedProperty jobs = so.FindProperty("jobs");
            bool has = false;
            for (int i = 0; i < jobs.arraySize; i++)
            {
                has |= jobs.GetArrayElementAtIndex(i).objectReferenceValue == job;
            }

            if (!has)
            {
                jobs.InsertArrayElementAtIndex(jobs.arraySize);
                jobs.GetArrayElementAtIndex(jobs.arraySize - 1).objectReferenceValue = job;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(resident);
            }

            offered.Add(name);
        }

        return "Fisher offered to " + string.Join(", ", offered);
    }

    private static Transform Piece(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<MeshRenderer>().sharedMaterial = material;
        return piece.transform;
    }

    private static Material Tint(string name, Color color)
    {
        string path = Tints + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static ItemDefinition Item(string file)
    {
        var found = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + file + ".asset");
        return found != null ? found : throw new Exception("No item " + file);
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
