using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Transport/";
    private const string Impacts = "Assets/_Project/Audio/SFX/Kenney Impact/";

    private static Material plank;
    private static Material darkPlank;
    private static Material iron;
    private static PhysicsMaterial slick;
    private static Material sack;

    public static string Apply()
    {
        foreach (string folder in new[] { Data, Art, Prefabs })
        {
            PrefabBuildUtility.EnsureFolder(folder.TrimEnd('/'));
        }

        plank = Tint("Cart Plank", new Color(0.46f, 0.31f, 0.18f));
        darkPlank = Tint("Cart Plank Dark", new Color(0.32f, 0.21f, 0.12f));
        iron = Tint("Dark Iron", new Color(0.2f, 0.21f, 0.24f));
        sack = Tint("Cargo Sack", new Color(0.6f, 0.5f, 0.34f));
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
        so.FindProperty("rolling").objectReferenceValue = Sound("Cart Roll", 0.55f, 0f, Vector2.one, 3f, 30f, 1, Clips + "Cart Roll Loop.wav");
        so.FindProperty("hullWater").objectReferenceValue = Sound("Hull Water", 0.4f, 0f, Vector2.one, 3f, 25f, 1, Clips + "Hull Water Loop.wav");
        so.FindProperty("creak").objectReferenceValue = Sound("Cart Creak", 0.32f, 0.3f, new Vector2(0.9f, 1.12f), 3f, 25f, 2,
            Clips + "Cart Creak 1.wav", Clips + "Cart Creak 2.wav", Clips + "Cart Creak 3.wav", Clips + "Cart Creak 4.wav");
        so.FindProperty("oarStroke").objectReferenceValue = Sound("Oar Stroke", 0.55f, 0.2f, new Vector2(0.92f, 1.08f), 3f, 30f, 2,
            Clips + "Oar Stroke 1.wav", Clips + "Oar Stroke 2.wav", Clips + "Oar Stroke 3.wav", Clips + "Oar Stroke 4.wav");
        so.FindProperty("bump").objectReferenceValue = Sound("Transport Bump", 0.7f, 0.25f, new Vector2(0.85f, 1.05f), 3f, 35f, 2,
            Impacts + "impactWood_heavy_000.ogg", Impacts + "impactWood_heavy_001.ogg", Impacts + "impactWood_heavy_002.ogg", Impacts + "impactWood_heavy_003.ogg", Impacts + "impactWood_heavy_004.ogg");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    private static SoundDefinition Sound(string name, float volume, float variation, Vector2 pitch, float full, float audible, int atOnce, params string[] clips)
    {
        string path = Sounds + name + ".asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var so = new SerializedObject(sound);
        SerializedProperty list = so.FindProperty("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]);
            list.GetArrayElementAtIndex(i).objectReferenceValue = clip != null ? clip : throw new Exception("No clip " + clips[i]);
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("volumeVariation").floatValue = variation;
        so.FindProperty("pitchRange").vector2Value = pitch;
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = atOnce;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
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
        Pile(root, new Vector3(0f, 0.5f, 0.1f), new Vector3(0.42f, 0.13f, 0.68f), 0.62f, 5, 3);
        Dress(root, BarrowLook);
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
        Pile(root, new Vector3(0f, 0.7f, -0.3f), new Vector3(0.9f, 0.17f, 1.5f), 1f, 10, 5);
        Dress(root, CartLook);
        return Save(root, definition, 2.6f, new Vector3(0f, 0.7f, 0f), new Vector3(1.6f, 1.2f, 3.2f));
    }

    // ----- The generated looks -----

    // The carts' looks made by LocalSpawn (Tools/LocalSpawn/transport_manifest.json, `generate_gathering.py --group
    // transport all`): Tools/Blender/ingest_generated.py bakes each into Art/Transport/<Asset>/<Asset>.fbx with its body
    // ("<Asset> A", LOD1, LOD2) and its wheels cut out of the generated model ("<Asset> A Wheel N", each centred on its
    // hub, the node standing on it). A transport with a look keeps its physics parts (wheel pivots, handle, solids, the
    // load) and wears the look: the body under Frame, each wheel under its pivot's Spin, the pivot on the model's hub, its
    // collider the model's radius. Until the look is baked the primitives stay.
    private const string Looks = "Assets/_Project/Art/Transport/";
    private const string CargoModel = Looks + "Cargo/Cargo.fbx";
    private const string FieldStone = "Assets/_Project/Models/PolyHaven/stone_01/stone_01.fbx";

    private sealed class Look
    {
        public string Asset;
        // The prefab's wheel pivots, from left (-x) to right.
        public string[] Wheels;
        public Vector3 Handle;
        public Vector3 CentreOfMass;
        // What it stands on and what the player bumps into besides the wheels: (name, centre, size).
        public (string Name, Vector3 Centre, Vector3 Size)[] Solids;
        // The load (Pile): the bed's floor, its inside, the rim's height.
        public Vector3 LoadFloor, LoadSize;
        public float Rim;
        public int Full, Heap;
        // A look made without usable wheels borrows another look's ("<WheelFrom> A Wheel 1"), scaled per axis, its hubs
        // given here (the prefab's space, left to right).
        public string WheelFrom;
        public Vector3 WheelScale = Vector3.one;
        public Vector3[] WheelHubs;
    }

    // Measured on the look: the bed z -1.2..0.45, 1.4 wide, rim 1.27; the shafts reach z 1.55 at 0.68; the hubs at
    // z -0.46. No leg was generated: its weight a little ahead of the axle, it rests tipped onto its shaft tips (Prop)
    // and comes level in the hands.
    private static readonly Look CartLook = new Look
    {
        Asset = "Handcart",
        Wheels = new[] { "Wheel Left", "Wheel Right" },
        Handle = new Vector3(0f, 0.68f, 1.5f),
        CentreOfMass = new Vector3(0f, 0.7f, -0.2f),
        Solids = new[]
        {
            ("Bed", new Vector3(0f, 0.95f, -0.38f), new Vector3(1.4f, 0.66f, 1.66f)),
            ("Prop", new Vector3(0f, 0.68f, 1.48f), new Vector3(1.1f, 0.1f, 0.14f)),
        },
        LoadFloor = new Vector3(0f, 0.8f, -0.38f), LoadSize = new Vector3(1.1f, 0.17f, 1.4f), Rim = 1.25f, Full = 10, Heap = 5,
    };

    // No generated barrow had one usable wheel (transport_manifest.json): the body was made without one, its fork's
    // crossbar cut away, and the Handcart's wheel turns between the fork ends, scaled to the fork (radius 0.27, 9 cm
    // wide). Measured on the look: the tray z -0.55..0.5 at 0.21..0.49, two A-frame legs at z -0.3 and 0.3, the grips at
    // z -0.78, 0.55 up. It stands on its legs and its wheel; lifted by the grips it rolls on the wheel alone.
    private static readonly Look BarrowLook = new Look
    {
        Asset = "Wheelbarrow",
        Wheels = new[] { "Wheel" },
        WheelFrom = "Handcart",
        WheelScale = new Vector3(0.4f, 0.53f, 0.53f),
        WheelHubs = new[] { new Vector3(0f, 0.27f, 0.81f) },
        Handle = new Vector3(0f, 0.55f, -0.78f),
        CentreOfMass = new Vector3(0f, 0.3f, 0.12f),
        Solids = new[]
        {
            ("Leg", new Vector3(0f, 0.12f, -0.3f), new Vector3(0.5f, 0.24f, 0.07f)),
            ("Leg", new Vector3(0f, 0.12f, 0.3f), new Vector3(0.5f, 0.24f, 0.07f)),
            ("Tray", new Vector3(0f, 0.35f, -0.02f), new Vector3(0.6f, 0.28f, 1.05f)),
        },
        LoadFloor = new Vector3(0f, 0.3f, -0.02f), LoadSize = new Vector3(0.44f, 0.12f, 0.8f), Rim = 0.49f, Full = 5, Heap = 3,
    };

    /// <summary>
    /// Puts the generated looks on the Handcart and Wheelbarrow prefabs, and the generated cargo in the Rowboat, and nothing
    /// else (`--entry BuildTransport.DressLooks`). The icons are painted (item_icons.json) and stay. Safe to rerun.
    /// </summary>
    public static string DressLooks()
    {
        slick = Slick();
        var report = new List<string>();
        foreach (Look look in new[] { CartLook, BarrowLook })
        {
            string path = Prefabs + look.Asset + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                report.Add(Dress(root, look));
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = IgnoreRaycast;
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        GameObject boat = PrefabUtility.LoadPrefabContents(Prefabs + "Rowboat.prefab");
        try
        {
            report.Add(DressBoat(boat));
            foreach (Transform child in boat.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = IgnoreRaycast;
            PrefabUtility.SaveAsPrefabAsset(boat, Prefabs + "Rowboat.prefab");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(boat);
        }

        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // The look on a transport being built (BuildCart, BuildBarrow) or loaded (DressLooks). False-y report: not baked yet.
    private static string Dress(GameObject root, Look look)
    {
        string fbx = Looks + look.Asset + "/" + look.Asset + ".fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
        {
            return $"{look.Asset}: no look baked yet, primitives kept";
        }

        Material material = LookMaterial(look.Asset);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        Mesh[] body = Levels(fbx, look.Asset + " A");
        var lods = new List<Renderer>[body.Length];
        for (int i = 0; i < lods.Length; i++) lods[i] = new List<Renderer>();

        Transform frame = root.transform.Find("Frame");
        Clear(frame);
        for (int i = 0; i < body.Length; i++)
        {
            lods[i].Add(Part(frame, $"LOD{i}", body[i], material, i < body.Length - 1));
        }

        // The wheels, left to right, each pivot on its hub: the look's own, or borrowed (WheelFrom).
        var wheels = new List<(Vector3 Hub, Mesh[] Levels, Material Material)>();
        if (look.WheelFrom != null)
        {
            string from = Looks + look.WheelFrom + "/" + look.WheelFrom + ".fbx";
            Material borrowed = LookMaterial(look.WheelFrom);
            wheels.AddRange(look.WheelHubs.Select(hub => (hub, Levels(from, look.WheelFrom + " A Wheel 1"), borrowed)));
        }
        else
        {
            for (int k = 1; ; k++)
            {
                string name = $"{look.Asset} A Wheel {k}";
                Transform node = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
                if (node == null) break;
                wheels.Add((Hub(model, node), Levels(fbx, name), material));
            }
        }

        wheels.Sort((a, b) => a.Hub.x.CompareTo(b.Hub.x));
        if (wheels.Count != look.Wheels.Length || wheels.Any(w => w.Levels.Length == 0))
        {
            throw new Exception($"{look.Asset}: the model has {wheels.Count} wheels, the prefab {look.Wheels.Length}");
        }

        float radius = 0f;
        for (int k = 0; k < wheels.Count; k++)
        {
            Transform pivot = root.transform.Find(look.Wheels[k]);
            pivot.localPosition = wheels[k].Hub;
            pivot.localRotation = Quaternion.identity;
            Mesh mesh = wheels[k].Levels[0];
            float r = Mathf.Max(mesh.bounds.extents.y * look.WheelScale.y, mesh.bounds.extents.z * look.WheelScale.z);
            radius += r / wheels.Count;
            var collider = pivot.GetComponent<SphereCollider>();
            collider.center = Vector3.zero;
            collider.radius = r;
            collider.sharedMaterial = slick;
            Transform spin = pivot.Find("Spin");
            Clear(spin);
            for (int i = 0; i < wheels[k].Levels.Length && i < lods.Length; i++)
            {
                Renderer part = Part(spin, $"LOD{i}", wheels[k].Levels[i], wheels[k].Material, i < lods.Length - 1);
                part.transform.localScale = look.WheelScale;
                lods[i].Add(part);
            }
        }

        LODGroup group = root.TryGetComponent(out LODGroup found) ? found : root.AddComponent<LODGroup>();
        float[] heights = ScreenBudget.LodHeights(body[0].bounds, body.Select(Triangles).ToArray());
        group.SetLODs(lods.Select((renderers, i) => new LOD(heights[Mathf.Min(i, heights.Length - 1)], renderers.ToArray())).ToArray());
        group.fadeMode = LODFadeMode.None;
        group.RecalculateBounds();

        // Its solids and the handle, measured on the look.
        foreach (BoxCollider box in root.GetComponentsInChildren<BoxCollider>(true).Where(box => box.transform != root.transform))
        {
            Object.DestroyImmediate(box.gameObject);
        }

        foreach ((string name, Vector3 centre, Vector3 size) in look.Solids)
        {
            Solid(root.transform, name, centre, size, false);
        }

        root.transform.Find("Handle").localPosition = look.Handle;
        var haul = new SerializedObject(root.GetComponent<Haulable>());
        haul.FindProperty("wheelRadius").floatValue = radius;
        haul.FindProperty("centreOfMass").vector3Value = look.CentreOfMass;
        haul.ApplyModifiedPropertiesWithoutUndo();
        Pile(root, look.LoadFloor, look.LoadSize, look.Rim, look.Full, look.Heap);
        return $"{look.Asset}: {string.Join("/", body.Select(Triangles))} body triangles, {wheels.Count} wheels r {radius:0.00} at " +
               string.Join(" ", wheels.Select(w => w.Hub.ToString("F2"))) + $", body {body[0].bounds.center:F2} {body[0].bounds.size:F2}";
    }

    // Where a node of the model stands, in the model's space (the hub of a wheel).
    private static Vector3 Hub(GameObject model, Transform node) => model.transform.InverseTransformPoint(node.position);

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }

    private static Renderer Part(Transform parent, string name, Mesh mesh, Material material, bool shadows)
    {
        var part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.layer = IgnoreRaycast;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    private static Mesh[] Levels(string fbx, string look)
    {
        Mesh[] meshes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().ToArray();
        return new[] { look, look + " LOD1", look + " LOD2" }
            .Select(name => meshes.FirstOrDefault(mesh => mesh.name == name))
            .Where(mesh => mesh != null).ToArray();
    }

    private static int Triangles(Mesh mesh)
    {
        long indices = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
        return (int)(indices / 3);
    }

    // Layered Lit with every baked map, like the world props (BuildWorldProps): parallax, moss from the baked coverage,
    // snow and rain. One per look, beside its FBX.
    private static Material LookMaterial(string asset)
    {
        string prefix = Looks + asset + "/" + asset;
        if (AssetDatabase.LoadAssetAtPath<Material>(prefix + ".mat") is Material done && done.GetTexture("_BaseMap") != null && importedLooks.Contains(asset))
        {
            return done;
        }

        LookTexture(prefix + "_Albedo.png", TextureImporterType.Default, true, 2048);
        LookTexture(prefix + "_Normal.png", TextureImporterType.NormalMap, false, 2048);
        LookTexture(prefix + "_ORM.png", TextureImporterType.Default, false, 1024);
        LookTexture(prefix + "_Height.png", TextureImporterType.Default, false, 1024);
        LookTexture(prefix + "_BlendMask.png", TextureImporterType.Default, false, 1024);
        var model = (ModelImporter)AssetImporter.GetAtPath(prefix + ".fbx");
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importAnimation = false;
        model.animationType = ModelImporterAnimationType.None;
        model.importCameras = false;
        model.importLights = false;
        model.isReadable = false;
        model.SaveAndReimport();

        Material scan = AssetDatabase.LoadAllAssetsAtPath(FieldStone).OfType<Material>().First();
        var material = AssetDatabase.LoadAssetAtPath<Material>(prefix + ".mat");
        if (material == null)
        {
            material = new Material(scan);
            AssetDatabase.CreateAsset(material, prefix + ".mat");
        }
        else
        {
            material.shader = scan.shader;
            material.CopyPropertiesFromMaterial(scan);
        }

        material.name = asset;
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Albedo.png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Normal.png"));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_ORMMap", AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_ORM.png"));
        material.EnableKeyword("_ORMMAP");
        material.SetFloat("_Roughness", 1f);
        material.SetFloat("_Metallic", 1f); // the ORM's blue is the metallic itself
        material.SetFloat("_OcclusionStrength", 1f);
        material.SetFloat("_L2Amount", 0f);
        var height = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_Height.png");
        material.SetTexture("_ParallaxMap", height);
        material.SetFloat("_Parallax", height != null ? 0.006f : 0f);
        if (height != null) material.EnableKeyword("_PARALLAXMAP");
        else material.DisableKeyword("_PARALLAXMAP");
        // A little moss in the cracks of old wood; a cart is used, not left to grow over.
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(prefix + "_BlendMask.png");
        material.SetTexture("_BlendMask", mask);
        material.SetFloat("_L1Amount", 0.08f);
        material.SetFloat("_L1Source", mask != null ? 3f : 4f);
        material.SetFloat("_L1Upward", 0.3f);
        material.SetFloat("_L1Patchiness", 0.5f);
        material.SetFloat("_L1PatchScale", 3f);
        material.SetFloat("_SeasonSnow", 1f);
        material.EnableKeyword("_SEASON_SNOW");
        material.SetTexture("_EmissionMap", null);
        material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        importedLooks.Add(asset);
        return material;
    }

    // Looks whose import settings were set this run (the cargo's is used by three prefabs).
    private static readonly HashSet<string> importedLooks = new HashSet<string>();

    private static void LookTexture(string path, TextureImporterType type, bool colour, int size)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
        {
            return;
        }

        importer.textureType = type;
        importer.sRGBTexture = colour;
        importer.maxTextureSize = size;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        importer.SaveAndReimport();
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

        var cap = new GameObject("Water Mask");
        cap.transform.SetParent(root.transform, false);
        cap.AddComponent<MeshFilter>().sharedMesh = HullCap();
        var capRenderer = cap.AddComponent<MeshRenderer>();
        capRenderer.sharedMaterial = WaterMask();
        capRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        capRenderer.receiveShadows = false;

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
        BoatPile(root);
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
        DressBoat(root);
        return Save(root, definition, 2.4f, Vector3.zero, new Vector3(1.2f, 0.6f, 3.2f));
    }

    // ----- The boat's look -----

    // The Rowboat's look (transport_manifest.json: a clinker-built faering made without oars, and the Oar on its own, so
    // the oars still swing with the stroke). Rowboat sets its centre of mass from the origin, so the origin stays where
    // the lofted hull had it: the hull hangs with its keel 0.28 below, and its convex collider goes on the Hull with it.
    // Measured on the look (keel 0): floorboards 0.09, thwarts 0.5 (at z -1.0, -0.2, 0.4, 1.05), the rim 0.63 amidships,
    // the posts 0.93; 1.27 wide. The floats keep their place over the keel, so it floats as deep as before.
    private const float Keel = -0.28f;
    private static readonly Vector3 BoatSeat = new Vector3(0f, 0.25f, -0.2f);
    private const float OarHeight = 0.38f, OarSide = 0.6f, OarZ = -0.15f, OarInboard = 0.55f;

    private static string DressBoat(GameObject root)
    {
        string fbx = Looks + "Rowboat/Rowboat.fbx", oarFbx = Looks + "Oar/Oar.fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null || AssetDatabase.LoadAssetAtPath<GameObject>(oarFbx) == null)
        {
            BoatPile(root);
            return "Rowboat: no look baked yet, primitives kept";
        }

        Material material = LookMaterial("Rowboat"), oarMaterial = LookMaterial("Oar");
        Mesh[] hull = Levels(fbx, "Rowboat A");
        Mesh collision = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().First(mesh => mesh.name == "Rowboat A Collider");
        Mesh oar = Levels(oarFbx, "Oar A")[0];

        Transform shell = root.transform.Find("Hull");
        foreach (Component part in shell.GetComponents<Component>().Where(c => !(c is Transform)).ToArray()) Object.DestroyImmediate(part);
        Clear(shell);
        shell.localPosition = new Vector3(0f, Keel, 0f);
        var lods = new List<Renderer>[hull.Length];
        for (int i = 0; i < hull.Length; i++)
        {
            lods[i] = new List<Renderer> { Part(shell, $"LOD{i}", hull[i], material, i < hull.Length - 1) };
        }

        if (root.TryGetComponent(out MeshCollider rootCollider)) Object.DestroyImmediate(rootCollider);
        var collider = shell.gameObject.AddComponent<MeshCollider>();
        collider.sharedMesh = collision;
        collider.convex = true;

        Clear(root.transform.Find("Frame")); // the thwarts are in the look
        root.transform.Find("Seat").localPosition = BoatSeat;
        foreach ((string name, float side) in new[] { ("Oar Left", -1f), ("Oar Right", 1f) })
        {
            Transform pivot = root.transform.Find(name);
            pivot.localPosition = new Vector3(side * OarSide, OarHeight, OarZ);
            pivot.localRotation = Quaternion.Euler(0f, 0f, side * -18f);
            Clear(pivot);
            // The Oar lies along x: the grip end inboard, the blade out, the shaft in the thole OarInboard from the grip.
            Renderer blade = Part(pivot, "Oar", oar, oarMaterial, true);
            blade.transform.localPosition = new Vector3(side * (oar.bounds.extents.x - OarInboard), -oar.bounds.center.y, 0f);
            blade.transform.localRotation = Quaternion.Euler(0f, side > 0f ? 180f : 0f, 0f); // the export mirrors x: the blade comes in at -x
            for (int i = 0; i < lods.Length - 1; i++) lods[i].Add(blade);
        }

        LODGroup group = root.TryGetComponent(out LODGroup found) ? found : root.AddComponent<LODGroup>();
        float[] heights = ScreenBudget.LodHeights(hull[0].bounds, hull.Select(Triangles).ToArray());
        group.SetLODs(lods.Select((renderers, i) => new LOD(heights[Mathf.Min(i, heights.Length - 1)], renderers.ToArray())).ToArray());
        group.fadeMode = LODFadeMode.None;
        group.RecalculateBounds();

        root.transform.Find("Water Mask").GetComponent<MeshFilter>().sharedMesh = BoatMask(fbx);
        Pile(root, new Vector3(0f, -0.12f, -0.65f), new Vector3(0.62f, 0.13f, 0.62f), 0.33f, 6, 3);
        return $"Rowboat: {string.Join("/", hull.Select(Triangles))} hull triangles, oar {Triangles(oar)} ({oar.bounds.size:F2}), keel {Keel}";
    }

    // The water mask (Water Mask.shader) across the look's opening, just under its rim: per slice along the boat, the rim's
    // height and half its beam, measured on the look's mesh (read once, the importer made readable for it).
    private static Mesh BoatMask(string fbx)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
        importer.isReadable = true;
        importer.SaveAndReimport();
        Vector3[] points = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Mesh>().First(mesh => mesh.name == "Rowboat A").vertices;
        importer.isReadable = false;
        importer.SaveAndReimport();

        float front = points.Max(p => p.z), back = points.Min(p => p.z);
        const int slices = 24;
        var rim = new List<Vector3>();
        for (int i = 0; i <= slices; i++)
        {
            float z = Mathf.Lerp(back + 0.08f, front - 0.08f, i / (float)slices);
            Vector3[] near = points.Where(p => Mathf.Abs(p.z - z) < 0.05f).ToArray();
            if (near.Length == 0) continue;
            float half = near.Max(p => Mathf.Abs(p.x));
            // The rim is the top of the sides, not the posts on the middle line.
            float top = near.Where(p => Mathf.Abs(p.x) > half * 0.6f).Select(p => p.y).DefaultIfEmpty(near.Max(p => p.y)).Max();
            rim.Add(new Vector3(Mathf.Max(0f, half - 0.045f), top - 0.04f + Keel, z));
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        foreach (Vector3 point in rim)
        {
            vertices.Add(new Vector3(-point.x, point.y, point.z));
            vertices.Add(point);
        }

        for (int i = 0; i < rim.Count - 1; i++)
        {
            int a = i * 2;
            triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
        }

        string path = Looks + "Rowboat/Rowboat Water Mask.asset";
        var mask = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mask == null)
        {
            mask = new Mesh { name = "Rowboat Water Mask" };
            AssetDatabase.CreateAsset(mask, path);
        }

        mask.Clear();
        mask.SetVertices(vertices);
        mask.SetTriangles(triangles, 0);
        mask.RecalculateBounds();
        EditorUtility.SetDirty(mask);
        return mask;
    }

    // Sacks, crates and logs, bottom layer first; the last ones are the heap over the rim. Seeded, so a rebuild changes nothing.
    private static void BoatPile(GameObject root) => Pile(root, new Vector3(0f, -0.08f, -0.68f), new Vector3(0.62f, 0.13f, 0.62f), 0.3f, 6, 3);

    private static void Pile(GameObject root, Vector3 floor, Vector3 size, float rim, int full, int heap)
    {
        Transform old = root.transform.Find("Load");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        Transform load = Empty("Load", root.transform, Vector3.zero);
        var random = new System.Random(root.name.GetHashCode() == 0 ? 7 : root.name.Length * 31 + 7);
        float Spread(double span) => (float)((random.NextDouble() - 0.5) * span);
        var pieces = new List<Transform>();
        int total = full + heap;
        for (int i = 0; i < total; i++)
        {
            bool heaped = i >= full;
            float layer = heaped ? rim + (i - full + 1) * size.y * 0.5f : Mathf.Lerp(floor.y, rim - size.y * 0.25f, i / (float)full);
            float narrow = heaped ? 0.55f : 1f;
            // Measured to the piece's edge, so nothing pushes through the walls.
            float widest = size.y * 3f;
            var at = new Vector3(floor.x + Spread(Mathf.Max(0f, size.x - widest) * narrow), layer, floor.z + Spread(Mathf.Max(0f, size.z - widest) * narrow));
            pieces.Add(Cargo(load, i, at, size.y * 2f, Spread(60.0)));
        }

        var so = new SerializedObject(root.TryGetComponent(out CargoPile existing) ? existing : root.AddComponent<CargoPile>());
        SetList(so.FindProperty("pieces"), pieces.ToArray());
        so.FindProperty("fullPieces").intValue = full;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The generated cargo (Art/Transport/Cargo: a sack, a crate, a keg, a bundle of birch logs), each as wide as the
    // primitive it replaces, standing on the layer's floor (their pivots are underneath; a primitive's is its middle).
    private static readonly float[] CargoWidths = { 1.5f, 1.05f, 0.95f, 1.35f };

    private static Transform Cargo(Transform parent, int index, Vector3 at, float size, float turn)
    {
        Mesh[] cargo = AssetDatabase.LoadAssetAtPath<GameObject>(CargoModel) == null ? null
            : "ABCD".Select(key => Levels(CargoModel, "Cargo " + key).FirstOrDefault()).ToArray();
        if (cargo != null && cargo.All(mesh => mesh != null))
        {
            int kind = index % cargo.Length;
            Mesh mesh = cargo[kind];
            Vector3 extent = mesh.bounds.size;
            float scale = size * CargoWidths[kind] / Mathf.Max(extent.x, extent.z);
            var piece = new GameObject($"Cargo {index + 1}");
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = at - Vector3.up * (extent.y * scale * 0.5f);
            piece.transform.localRotation = Quaternion.Euler(0f, turn + (kind == 3 ? 90f : 0f), 0f);
            piece.transform.localScale = Vector3.one * scale;
            Part(piece.transform, "Look", mesh, LookMaterial("Cargo"), true);
            return piece.transform;
        }

        switch (index % 3)
        {
            case 0:
                Transform sackPiece = Piece(parent, PrimitiveType.Sphere, at, new Vector3(size * 1.5f, size, size * 1.2f), sack);
                sackPiece.localRotation = Quaternion.Euler(0f, turn, 0f);
                return sackPiece;
            case 1:
                Transform crate = Piece(parent, PrimitiveType.Cube, at, Vector3.one * size, darkPlank);
                crate.localRotation = Quaternion.Euler(0f, turn, 0f);
                return crate;
            default:
                Transform log = Piece(parent, PrimitiveType.Cylinder, at, new Vector3(size * 0.6f, size * 0.65f, size * 0.6f), plank);
                log.localRotation = Quaternion.Euler(0f, turn, 90f);
                return log;
        }
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
        root.AddComponent<TransportAudio>();
        if (root.GetComponent<Haulable>() != null)
        {
            root.AddComponent<CartScavenging>();
        }

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
    /// <summary>Rebuilds the hull's mesh and nothing else.</summary>
    public static string RebuildHull()
    {
        Mesh mesh = HullMesh();
        AssetDatabase.SaveAssets();
        return $"{mesh.name}: {mesh.vertexCount} vertices";
    }

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
            // From the diagonals: a quad that narrows to a point has no normal along its short edge, and a vertex without one
            // lights as NaN, which bloom spreads over the whole screen.
            Vector3 normal = Vector3.Cross(c - a, d - b).normalized;
            vertices.AddRange(new[] { a, b, c, d });
            normals.AddRange(new[] { normal, normal, normal, normal });
            triangles[strake].AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        Vector3 Point(int i, int j, float side, float inset) => HullPoint(i, along, j, up, side, inset);

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

    private static Vector3 HullPoint(int i, int along, int j, int up, float side, float inset)
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

    // Spans the opening of the hull just under the rim. Only ever writes depth (Water Mask.shader).
    private static Mesh HullCap()
    {
        const int along = 16;
        const int up = 5;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int i = 0; i <= along; i++)
        {
            vertices.Add(HullPoint(i, along, up, up, -1f, 0.03f) + Vector3.down * 0.02f);
            vertices.Add(HullPoint(i, along, up, up, 1f, 0.03f) + Vector3.down * 0.02f);
        }

        for (int i = 0; i < along; i++)
        {
            int a = i * 2;
            triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
        }

        string path = Art + "Rowboat Hull Cap.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Rowboat Hull Cap" };
            AssetDatabase.CreateAsset(mesh, path);
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    private static Material WaterMask()
    {
        string path = Art + "Water Mask.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/Water Mask.shader"));
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
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
