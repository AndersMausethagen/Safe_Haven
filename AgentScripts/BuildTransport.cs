using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Carts, barrows and boats (`unity command run_script --file AgentScripts/BuildTransport.cs --entry BuildTransport.Apply`,
/// GDD §8.5): the Wheelbarrow, Handcart and Rowboat with their physics prefabs, kit items and definitions, the Wainwright's
/// Yard that builds them, and PlayerTransport on the player. Models are generated: primitives, and a lofted mesh for the
/// hull. Prefabs are rebuilt each run. Leaves temporary objects in the open scene while it works; revert it afterwards.
/// </summary>
public static class BuildTransport
{
    private const int IgnoreRaycast = 2;
    private const string Data = "Assets/_Project/Data/Transport/";
    private const string Art = "Assets/_Project/Art/Transport/";
    private const string Prefabs = "Assets/_Project/Prefabs/Transport/";
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Materials = Items + "Materials/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";
    private const string Buildings = "Assets/_Project/Data/Buildings/";
    private const string Models = "Assets/_Project/Art/Haven/Models/";
    private const string Tints = "Assets/_Project/Materials/Haven/";
    private const string Player = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";

    private static Material plank;
    private static Material darkPlank;
    private static Material iron;
    private static PhysicsMaterial slick;

    public static string Apply()
    {
        foreach (string folder in new[] { Data, Art, Prefabs })
        {
            PrefabBuildUtility.EnsureFolder(folder.TrimEnd('/'));
        }

        plank = Tint("Cart Plank", new Color(0.46f, 0.31f, 0.18f));
        darkPlank = Tint("Cart Plank Dark", new Color(0.32f, 0.21f, 0.12f));
        iron = Tint("Dark Iron", new Color(0.2f, 0.21f, 0.24f));
        slick = Slick();

        TransportDefinition barrow = Definition("Wheelbarrow", "Wheelbarrow", "One wheel and two handles. Goes where you point it, on any path you can walk.",
            6, 3, 100f, 160f, 0.96f, 0.86f, 0.5f, 30f, 1.2f, 1400f);
        TransportDefinition cart = Definition("Handcart", "Handcart", "Two big wheels and a deep bed. It holds more than you do, and it wants a wide road.",
            8, 4, 200f, 320f, 0.92f, 0.78f, 0.35f, 70f, 1.3f, 2200f);
        TransportDefinition boat = Definition("Rowboat", "Rowboat", "Oak planks and a pair of oars. Water carries what your back can't.",
            8, 3, 160f, 240f, 0.95f, 0.8f, 0.4f, 90f, 1.2f, 1f);

        Finish(barrow, BuildBarrow(barrow), 1, 25, new[] { ("OakPlank", 6), ("Sticks", 4), ("NettleCord", 2) }, 20f, 12f, ItemRarity.Common);
        Finish(cart, BuildCart(cart), 2, 60, new[] { ("OakPlank", 14), ("NettleCord", 4), ("CopperIngot", 2) }, 40f, 30f, ItemRarity.Uncommon);
        Finish(boat, BuildBoat(boat), 2, 70, new[] { ("OakPlank", 18), ("NettleCord", 6), ("CopperIngot", 2) }, 60f, 40f, ItemRarity.Uncommon);
        Settings();
        AssetDatabase.SaveAssets();
        Yard();
        string player = WirePlayer();
        AssetDatabase.SaveAssets();
        Refresh();
        AssetDatabase.SaveAssets();
        return $"barrow, cart, boat, yard; {player}";
    }

    // ----- Definitions and kits -----

    private static TransportDefinition Definition(string file, string label, string text, int columns, int rows, float capacity, float limit,
        float empty, float full, float overfilled, float mass, float perWeight, float pull)
    {
        string path = Data + file + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<TransportDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<TransportDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        var so = new SerializedObject(definition);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("columns").intValue = columns;
        so.FindProperty("rows").intValue = rows;
        so.FindProperty("capacity").floatValue = capacity;
        so.FindProperty("overloadLimit").floatValue = limit;
        so.FindProperty("speedEmpty").floatValue = empty;
        so.FindProperty("speedFull").floatValue = full;
        so.FindProperty("speedOverfilled").floatValue = overfilled;
        so.FindProperty("emptyMass").floatValue = mass;
        so.FindProperty("massPerWeight").floatValue = perWeight;
        so.FindProperty("pullForce").floatValue = pull;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void Finish(TransportDefinition definition, GameObject prefab, int tier, int value, (string item, int amount)[] cost, float seconds, float weight, ItemRarity rarity)
    {
        string path = Items + definition.name + ".asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        string iconPath = Art + definition.name + " Icon.png";
        var icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = definition.name.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = definition.DisplayName;
        so.FindProperty("description").stringValue = definition.Description + " Use it to set it down.";
        so.FindProperty("category").intValue = (int)ItemCategory.Equipment;
        so.FindProperty("tier").intValue = tier;
        so.FindProperty("rarity").intValue = (int)rarity;
        so.FindProperty("value").intValue = value;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("icon").objectReferenceValue = icon != null ? icon : IconRenderer.RenderToSprite(prefab, iconPath);
        so.FindProperty("stackable").boolValue = false;
        so.FindProperty("transport").objectReferenceValue = definition;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);

        var data = new SerializedObject(definition);
        data.FindProperty("prefab").objectReferenceValue = prefab.GetComponent<SaveableEntity>();
        data.FindProperty("kit").objectReferenceValue = item;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        Recipe(definition.name, item, seconds, cost);
    }

    private static void Recipe(string file, ItemDefinition output, float seconds, (string item, int amount)[] cost)
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
        so.FindProperty("output").objectReferenceValue = output;
        so.FindProperty("outputAmount").intValue = 1;
        so.FindProperty("station").intValue = (int)CraftingStationType.Wainwright;
        so.FindProperty("craftSeconds").floatValue = seconds;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("sortOrder").intValue = 100 + output.Tier * 10;
        SerializedProperty items = so.FindProperty("itemCost");
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Material(cost[i].item);
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static void Settings()
    {
        const string path = "Assets/_Project/Resources/TransportSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<TransportSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TransportSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("slotPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemSlotView>("Assets/_Project/Prefabs/UI/ItemSlot.prefab");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    // ----- Prefabs -----

    private static GameObject BuildBarrow(TransportDefinition definition)
    {
        var root = new GameObject(definition.DisplayName);
        Transform frame = Empty("Frame", root.transform, Vector3.zero);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0.1f), new Vector3(0.62f, 0.05f, 0.9f), plank);
        Piece(frame, PrimitiveType.Cube, new Vector3(-0.31f, 0.56f, 0.1f), new Vector3(0.05f, 0.3f, 0.9f), darkPlank);
        Piece(frame, PrimitiveType.Cube, new Vector3(0.31f, 0.56f, 0.1f), new Vector3(0.05f, 0.3f, 0.9f), darkPlank);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.56f, -0.33f), new Vector3(0.62f, 0.3f, 0.05f), darkPlank);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.56f), new Vector3(0.62f, 0.3f, 0.05f), darkPlank);
        foreach (float side in new[] { -0.24f, 0.24f })
        {
            Piece(frame, PrimitiveType.Cube, new Vector3(side, 0.4f, -0.05f), new Vector3(0.06f, 0.06f, 1.95f), plank);
            Piece(frame, PrimitiveType.Cube, new Vector3(side, 0.2f, -0.45f), new Vector3(0.06f, 0.4f, 0.06f), darkPlank);
            Solid(root.transform, "Leg", new Vector3(side, 0.2f, -0.45f), new Vector3(0.08f, 0.4f, 0.08f), false);
        }

        Solid(root.transform, "Tray", new Vector3(0f, 0.53f, 0.1f), new Vector3(0.66f, 0.26f, 0.94f), false);
        Transform wheel = Wheel(root.transform, "Wheel", new Vector3(0f, 0.25f, 0.82f), 0.25f, 0.07f);
        Transform handle = Empty("Handle", root.transform, new Vector3(0f, 0.4f, -1f));
        Haulable(root, handle, new[] { wheel }, 0.25f, true, new Vector3(0f, 0.42f, 0.12f), 0.74f, 0.42f);
        return Save(root, definition, 2.2f, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 2.2f));
    }

    private static GameObject BuildCart(TransportDefinition definition)
    {
        var root = new GameObject(definition.DisplayName);
        Transform frame = Empty("Frame", root.transform, Vector3.zero);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.6f, -0.3f), new Vector3(1.15f, 0.07f, 1.85f), plank);
        foreach (float side in new[] { -0.58f, 0.58f })
        {
            Piece(frame, PrimitiveType.Cube, new Vector3(side, 0.8f, -0.3f), new Vector3(0.06f, 0.36f, 1.85f), darkPlank);
            Piece(frame, PrimitiveType.Cube, new Vector3(side * 0.6f, 0.58f, 0.95f), new Vector3(0.07f, 0.07f, 1.75f), plank);
        }

        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.8f, -1.22f), new Vector3(1.15f, 0.36f, 0.06f), darkPlank);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0.62f), new Vector3(1.15f, 0.36f, 0.06f), darkPlank);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.58f, 1.8f), new Vector3(0.8f, 0.07f, 0.07f), plank);
        Piece(frame, PrimitiveType.Cylinder, new Vector3(0f, 0.43f, -0.35f), new Vector3(0.06f, 0.82f, 0.06f), iron).localRotation = Quaternion.Euler(0f, 0f, 90f);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.4f, 1.25f), new Vector3(0.07f, 0.36f, 0.07f), darkPlank);
        Solid(root.transform, "Bed", new Vector3(0f, 0.78f, -0.3f), new Vector3(1.22f, 0.42f, 1.92f), false);
        Solid(root.transform, "Prop", new Vector3(0f, 0.4f, 1.25f), new Vector3(0.1f, 0.36f, 0.1f), false);
        Transform left = Wheel(root.transform, "Wheel Left", new Vector3(-0.74f, 0.43f, -0.35f), 0.43f, 0.09f);
        Transform right = Wheel(root.transform, "Wheel Right", new Vector3(0.74f, 0.43f, -0.35f), 0.43f, 0.09f);
        Transform handle = Empty("Handle", root.transform, new Vector3(0f, 0.58f, 1.85f));
        Haulable(root, handle, new[] { left, right }, 0.43f, false, new Vector3(0f, 0.5f, -0.15f), 0.76f, 0.5f);
        return Save(root, definition, 2.6f, new Vector3(0f, 0.7f, 0f), new Vector3(1.6f, 1.2f, 3.2f));
    }

    private static GameObject BuildBoat(TransportDefinition definition)
    {
        var root = new GameObject(definition.DisplayName);
        Mesh hull = HullMesh();
        var shell = new GameObject("Hull");
        shell.transform.SetParent(root.transform, false);
        shell.AddComponent<MeshFilter>().sharedMesh = hull;
        shell.AddComponent<MeshRenderer>().sharedMaterials = new[] { plank, darkPlank };
        var collider = root.AddComponent<MeshCollider>();
        collider.sharedMesh = hull;
        collider.convex = true;

        Transform frame = Empty("Frame", root.transform, Vector3.zero);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.04f, -0.15f), new Vector3(0.96f, 0.05f, 0.26f), darkPlank);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.9f), new Vector3(0.72f, 0.05f, 0.22f), darkPlank);
        Piece(frame, PrimitiveType.Cube, new Vector3(0f, 0.04f, -1.15f), new Vector3(0.82f, 0.05f, 0.22f), darkPlank);
        Transform seat = Empty("Seat", root.transform, new Vector3(0f, 0.1f, -0.15f));
        Transform left = Oar(root.transform, "Oar Left", -1f);
        Transform right = Oar(root.transform, "Oar Right", 1f);
        Empty("Floats", root.transform, Vector3.zero);
        var floats = new List<Transform>();
        foreach (Vector3 point in new[]
                 {
                     new Vector3(-0.36f, -0.22f, -0.95f), new Vector3(0.36f, -0.22f, -0.95f), new Vector3(-0.42f, -0.22f, 0.1f),
                     new Vector3(0.42f, -0.22f, 0.1f), new Vector3(-0.22f, -0.2f, 1.05f), new Vector3(0.22f, -0.2f, 1.05f),
                 })
        {
            floats.Add(Empty("Float", root.transform.Find("Floats"), point));
        }

        var body = root.AddComponent<Rigidbody>();
        body.mass = definition.EmptyMass;
        root.AddComponent<Interactable>();
        root.AddComponent<Transport>();
        var rowboat = root.AddComponent<Rowboat>();
        var so = new SerializedObject(rowboat);
        SetList(so.FindProperty("floats"), floats.ToArray());
        SetList(so.FindProperty("oars"), new[] { left, right });
        so.FindProperty("seat").objectReferenceValue = seat;
        so.FindProperty("buoyancy").floatValue = 2000f;
        so.FindProperty("heaveDamping").floatValue = 140f;
        so.ApplyModifiedPropertiesWithoutUndo();

        var water = new SerializedObject(root.AddComponent<WaterInteractor>());
        water.FindProperty("radius").floatValue = 1.1f;
        water.FindProperty("foam").floatValue = 0.4f;
        water.ApplyModifiedPropertiesWithoutUndo();
        return Save(root, definition, 2.4f, Vector3.zero, new Vector3(1.2f, 0.6f, 3.2f));
    }

    private static Transform Oar(Transform parent, string name, float side)
    {
        Transform pivot = Empty(name, parent, new Vector3(side * 0.56f, 0.3f, -0.1f));
        pivot.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
        Piece(pivot, PrimitiveType.Cube, new Vector3(side * 0.7f, 0f, 0f), new Vector3(1.9f, 0.04f, 0.04f), plank);
        Piece(pivot, PrimitiveType.Cube, new Vector3(side * 1.6f, 0f, 0f), new Vector3(0.42f, 0.02f, 0.16f), darkPlank);
        return pivot;
    }

    private static void Haulable(GameObject root, Transform handle, Transform[] wheels, float radius, bool pushed, Vector3 centre, float height, float hold)
    {
        var body = root.AddComponent<Rigidbody>();
        body.mass = 60f;
        root.AddComponent<Interactable>();
        root.AddComponent<Transport>();
        var so = new SerializedObject(root.AddComponent<Haulable>());
        so.FindProperty("handle").objectReferenceValue = handle;
        SetList(so.FindProperty("wheels"), wheels);
        so.FindProperty("wheelRadius").floatValue = radius;
        so.FindProperty("pushed").boolValue = pushed;
        so.FindProperty("centreOfMass").vector3Value = centre;
        so.FindProperty("handleHeight").floatValue = height;
        so.FindProperty("holdDistance").floatValue = hold;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject Save(GameObject root, TransportDefinition definition, float reach, Vector3 unusedCentre, Vector3 unusedSize)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = IgnoreRaycast;
        }

        var interactable = new SerializedObject(root.GetComponent<Interactable>());
        interactable.FindProperty("animationType").intValue = (int)InteractionAnimation.None;
        interactable.FindProperty("duration").floatValue = 0f;
        interactable.FindProperty("range").floatValue = reach;
        interactable.FindProperty("deactivateOnComplete").boolValue = false;
        interactable.FindProperty("kind").intValue = (int)InteractionKind.Use;
        interactable.ApplyModifiedPropertiesWithoutUndo();

        var transport = new SerializedObject(root.GetComponent<Transport>());
        transport.FindProperty("definition").objectReferenceValue = definition;
        transport.ApplyModifiedPropertiesWithoutUndo();
        root.AddComponent<SaveableEntity>();

        string path = Prefabs + definition.name + ".prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        // Without it a transport set down in the world can't be recreated from a save.
        prefab.GetComponent<SaveableEntity>().RefreshPrefabId();
        PrefabUtility.SavePrefabAsset(prefab);
        return prefab;
    }

    private static Transform Wheel(Transform parent, string name, Vector3 at, float radius, float width)
    {
        Transform pivot = Empty(name, parent, at);
        var collider = pivot.gameObject.AddComponent<SphereCollider>();
        collider.radius = radius;
        collider.sharedMaterial = slick;
        Transform spin = Empty("Spin", pivot, Vector3.zero);
        Piece(spin, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 2f, width * 0.5f, radius * 2f), darkPlank).localRotation = Quaternion.Euler(0f, 0f, 90f);
        Piece(spin, PrimitiveType.Cylinder, Vector3.zero, new Vector3(radius * 0.6f, width * 0.8f, radius * 0.6f), iron).localRotation = Quaternion.Euler(0f, 0f, 90f);
        for (int i = 0; i < 3; i++)
        {
            Piece(spin, PrimitiveType.Cube, Vector3.zero, new Vector3(width * 1.3f, radius * 1.85f, 0.05f), plank).localRotation = Quaternion.Euler(i * 60f, 0f, 0f);
        }

        return pivot;
    }

    private static Transform Empty(string name, Transform parent, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        return go.transform;
    }

    private static void Solid(Transform parent, string name, Vector3 at, Vector3 size, bool slippery)
    {
        Transform part = Empty(name, parent, at);
        var box = part.gameObject.AddComponent<BoxCollider>();
        box.size = size;
        box.sharedMaterial = slippery ? slick : null;
    }

    private static Transform Piece(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = material;
        return piece.transform;
    }

    private static void SetList(SerializedProperty list, Transform[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    // ----- The hull -----

    // A loft: cross sections along the boat, faceted so the planks show, every other strake a darker submesh.
    private static Mesh HullMesh()
    {
        const int along = 16;
        const int up = 5;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new[] { new List<int>(), new List<int>() };

        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int strake)
        {
            int start = vertices.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            vertices.AddRange(new[] { a, b, c, d });
            normals.AddRange(new[] { normal, normal, normal, normal });
            triangles[strake].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        Vector3 Point(int i, int j, float side, float inset)
        {
            float t = i / (float)along;
            float v = j / (float)up;
            float beam = t < 0.4f ? Mathf.Lerp(0.74f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.4f)) : Mathf.Lerp(1f, 0.02f, Mathf.Pow((t - 0.4f) / 0.6f, 1.9f));
            float top = 0.27f + 0.17f * t * t * t;
            float bottom = -0.28f + 0.24f * Mathf.Pow(t, 4f);
            float x = 0.58f * beam * Mathf.Pow(v, 0.55f);
            float y = Mathf.Lerp(bottom, top, v);
            x = Mathf.Max(0f, x - inset);
            return new Vector3(x * side, y + inset * (1f - v), Mathf.Lerp(-1.55f, 1.7f, t));
        }

        for (int i = 0; i < along; i++)
        {
            for (int j = 0; j < up; j++)
            {
                int strake = j % 2;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 a = Point(i, j, side, 0f);
                    Vector3 b = Point(i + 1, j, side, 0f);
                    Vector3 c = Point(i + 1, j + 1, side, 0f);
                    Vector3 d = Point(i, j + 1, side, 0f);
                    Vector3 e = Point(i, j, side, 0.035f);
                    Vector3 f = Point(i + 1, j, side, 0.035f);
                    Vector3 g = Point(i + 1, j + 1, side, 0.035f);
                    Vector3 h = Point(i, j + 1, side, 0.035f);
                    if (side > 0f)
                    {
                        Quad(a, d, c, b, strake);
                        Quad(e, f, g, h, 1 - strake);
                    }
                    else
                    {
                        Quad(a, b, c, d, strake);
                        Quad(e, h, g, f, 1 - strake);
                    }
                }
            }

            // The rim along the top of each side.
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 a = Point(i, up, side, 0f);
                Vector3 b = Point(i + 1, up, side, 0f);
                Vector3 c = Point(i + 1, up, side, 0.035f);
                Vector3 d = Point(i, up, side, 0.035f);
                if (side > 0f)
                {
                    Quad(a, b, c, d, 1);
                }
                else
                {
                    Quad(a, d, c, b, 1);
                }
            }
        }

        // The transom closes the stern.
        for (int j = 0; j < up; j++)
        {
            Vector3 a = Point(0, j, -1f, 0f);
            Vector3 b = Point(0, j, 1f, 0f);
            Vector3 c = Point(0, j + 1, 1f, 0f);
            Vector3 d = Point(0, j + 1, -1f, 0f);
            Quad(a, d, c, b, j % 2);
            Quad(a, b, c, d, 1);
        }

        string path = Art + "Rowboat Hull.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Rowboat Hull" };
            AssetDatabase.CreateAsset(mesh, path);
        }

        mesh.Clear();
        mesh.subMeshCount = 2;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles[0], 0);
        mesh.SetTriangles(triangles[1], 1);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // ----- The Wainwright's Yard -----

    private static void Yard()
    {
        const string file = "WainwrightsYard";
        string path = Buildings + file + ".asset";
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }

        var so = new SerializedObject(building);
        so.FindProperty("id").stringValue = "wainwrights_yard";
        so.FindProperty("displayName").stringValue = "Wainwright's Yard";
        so.FindProperty("description").stringValue = "Trestles, a steaming trough and a wall of clamps. Barrows, carts and boats are built here, from planks and patience.";
        so.FindProperty("combatStats").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>("Assets/_Project/Data/Combat/Stats/WoodenStructureStats.asset");
        SerializedProperty items = so.FindProperty("constructionItems");
        (string item, int amount)[] cost = { ("OakPlank", 10), ("OakWood", 8), ("NettleCord", 4) };
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Material(cost[i].item);
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.FindProperty("station").intValue = (int)CraftingStationType.Wainwright;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("showInBuildMenu").boolValue = true;
        so.FindProperty("sortOrder").intValue = 18;
        so.FindProperty("yardDepth").floatValue = 2.5f;
        so.FindProperty("placeInLines").boolValue = false;
        if (so.FindProperty("icon").objectReferenceValue == null)
        {
            so.FindProperty("icon").objectReferenceValue = IconRenderer.RenderToSprite(YardModel(), Art + "Wainwrights Yard Icon.png");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        var level = new BuildingLevel
        {
            title = "Sturdy",
            description = "A proper steam box. The planks bend sooner.",
            renown = 5,
            work = 8.5f,
            craftSpeed = 0.3f,
            extraQueue = 1,
            cost = new List<ItemAmount> { new ItemAmount(Material("OakPlank"), 8), new ItemAmount(Material("IronIngot"), 2) },
        };
        building.ConfigureBuildingUp(10f, 8, new[] { level });
        EditorUtility.SetDirty(building);
        if (building.Prefab == null)
        {
            GameObject prefab = BuildingPrefabBuilder.Create(building, YardModel());
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
    }

    private static GameObject YardModel()
    {
        string path = Models + "WainwrightsYard Model.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject("WainwrightsYard Model");
        Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(3.6f, 0.08f, 2.4f), darkPlank);
        foreach (float x in new[] { -1.2f, 1.2f })
        {
            Piece(root.transform, PrimitiveType.Cube, new Vector3(x, 0.7f, 0.4f), new Vector3(0.12f, 1.3f, 0.12f), plank);
            Piece(root.transform, PrimitiveType.Cube, new Vector3(x, 0.7f, -0.4f), new Vector3(0.12f, 1.3f, 0.12f), plank);
            Piece(root.transform, PrimitiveType.Cube, new Vector3(x, 1.32f, 0f), new Vector3(0.14f, 0.1f, 1.1f), darkPlank);
        }

        for (int i = 0; i < 5; i++)
        {
            Piece(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.42f + i * 0.05f, -0.42f + i * 0.2f), new Vector3(3f, 0.05f, 0.16f), plank);
        }

        Piece(root.transform, PrimitiveType.Cylinder, new Vector3(1.5f, 0.55f, -1.05f), new Vector3(0.95f, 0.05f, 0.95f), darkPlank).localRotation = Quaternion.Euler(82f, 0f, 0f);
        Piece(root.transform, PrimitiveType.Cylinder, new Vector3(1.5f, 0.55f, -1.05f), new Vector3(0.25f, 0.08f, 0.25f), iron).localRotation = Quaternion.Euler(82f, 0f, 0f);
        Piece(root.transform, PrimitiveType.Cylinder, new Vector3(-1.3f, 0.45f, -0.9f), new Vector3(0.9f, 0.4f, 0.9f), iron);
        PrefabBuildUtility.EnsureFolder(Models.TrimEnd('/'));
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ----- Shared -----

    private static string WirePlayer()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Player);
        try
        {
            if (root.GetComponent<PlayerTransport>() != null)
            {
                return "player already wired";
            }

            root.AddComponent<PlayerTransport>();
            root.AddComponent<PlayerTransportPose>();
            PrefabUtility.SaveAsPrefabAsset(root, Player);
            return "PlayerTransport on the player";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static PhysicsMaterial Slick()
    {
        string path = Art + "Wheel.physicMaterial";
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
        if (material == null)
        {
            material = new PhysicsMaterial("Wheel");
            AssetDatabase.CreateAsset(material, path);
        }

        material.dynamicFriction = 0f;
        material.staticFriction = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        material.bounciness = 0f;
        EditorUtility.SetDirty(material);
        return material;
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

    private static ItemDefinition Material(string file)
    {
        var found = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Materials + file + ".asset");
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
