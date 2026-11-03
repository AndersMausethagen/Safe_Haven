using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The authored places of the Misty Woods (GDD §9.2), from the same forest kit as the map so they belong on it. Each keeps its middle clear
/// for what a rumour brings. Guards go by tier, so a first raid doesn't meet what a fourth does. Run once, and again to rebuild the prefabs.
/// Run: unity command script run -- --file AgentScripts/BuildSetPieces.cs --entry BuildSetPieces.Run
/// </summary>
public static class BuildSetPieces
{
    private const string Prefabs = "Assets/_Project/Prefabs/Raids/SetPieces/";
    private const string Data = "Assets/_Project/Data/Raids/SetPieces/";
    private const string KitFolder = "Assets/_Project/Art/Forest/Prefabs/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";

    private static Material wood;
    private static Material bark;
    private static Material stone;
    private static Material charred;
    private static Material turf;
    private static Material bone;
    private static Material dark;

    public static string Run()
    {
        Directory.CreateDirectory(Prefabs);
        Directory.CreateDirectory(Data);
        AssetDatabase.Refresh();
        wood = Load<Material>("Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
        bark = Load<Material>("Assets/_Project/Art/Forest/Materials/Bark.mat");
        stone = Load<Material>("Assets/_Project/Art/Forest/Materials/Forest Boulder.mat");
        turf = Load<Material>("Assets/_Project/Prefabs/Raids/Events/Sites/Barrow Turf.mat");
        bone = Load<Material>("Assets/_Project/Prefabs/Raids/Events/Sites/Old Bone.mat");
        dark = Load<Material>("Assets/_Project/Prefabs/Raids/Events/Sites/Barrow Dark.mat");
        charred = Tint("Charred Wood", new Color(0.075f, 0.065f, 0.06f));

        var pieces = new List<RaidSetPieceDefinition>
        {
            Piece("Ruined Watchtower", "ruined_watchtower", Watchtower(), 10f, 1f, true, Guard("Rootling", 3, 1), Guard("Draug", 2, 2), Guard("BarrowShieldbearer", 1, 3)),
            Piece("Standing Stones", "standing_stones", Stones(), 9f, 1f, true, Guard("MarshLight", 2, 1), Guard("MossShaman", 1, 2)),
            Piece("Burnt Farmstead", "burnt_farmstead", Farmstead(), 11f, 1f, true, Guard("BlightBoar", 2, 1), Guard("Draug", 2, 2)),
            Piece("Wolf Den", "wolf_den", Den(), 10f, 1f, false, Guard("WolfPack", 1, 1), Guard("ForestBittenWolf", 2, 3)),
            Piece("Old Barrow", "old_barrow", Barrow(), 11f, 1f, true, Guard("Rootling", 2, 1), Guard("Draug", 2, 2), Guard("BarrowShieldbearer", 1, 2)),
            Piece("Guardian's Hollow", "guardians_hollow", Hollow(), 14f, 0.5f, true),
        };

        var rules = new SerializedObject(Load<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset"));
        SerializedProperty list = rules.FindProperty("setPieces");
        list.arraySize = pieces.Count;
        for (int i = 0; i < pieces.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
        }

        rules.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rules.targetObject);

        int rumours = 0;
        foreach ((string rumour, string place) in new[]
        {
            ("BarrowHoard", "Old Barrow"), ("Bjarne", "Burnt Farmstead"), ("Eirik", "Ruined Watchtower"), ("Sigrid", "Ruined Watchtower"),
            ("Liv", "Standing Stones"), ("NightCairn", "Standing Stones"), ("Tor", "Burnt Farmstead"), ("Runa", "Ruined Watchtower"),
        })
        {
            var asset = Load<RaidRumourDefinition>($"Assets/_Project/Data/Raids/Rumours/{rumour}.asset");
            if (asset != null)
            {
                var so = new SerializedObject(asset);
                so.FindProperty("setPiece").objectReferenceValue = Load<RaidSetPieceDefinition>($"{Data}{place}.asset");
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                rumours++;
            }
        }

        AssetDatabase.SaveAssets();
        return $"{pieces.Count} set pieces; {rumours} rumours have a place";
    }

    // ----- The places -----

    private static GameObject Watchtower()
    {
        Transform root = Root("Ruined Watchtower");
        // On one side of the clearing, its four legs and what is left of the platform, broken toward the middle.
        Vector3 at = new Vector3(5.5f, 0f, 0f);
        foreach ((float x, float z, float height, float lean) in new[] { (-1.6f, -1.6f, 6f, 2f), (1.6f, -1.6f, 6.4f, -3f), (-1.6f, 1.6f, 4.1f, 7f), (1.6f, 1.6f, 6.2f, -2f) })
        {
            Beam(root, at + new Vector3(x, height * 0.5f, z), new Vector3(lean, 0f, lean * 0.5f), new Vector3(0.34f, height, 0.34f), wood);
        }

        Beam(root, at + new Vector3(0.5f, 5.8f, -0.4f), new Vector3(3f, 0f, 2f), new Vector3(3.6f, 0.18f, 2.9f), wood);
        Beam(root, at + new Vector3(0f, 3f, -1.6f), new Vector3(0f, 0f, 27f), new Vector3(3.6f, 0.16f, 0.16f), wood);
        Beam(root, at + new Vector3(1.6f, 2.6f, 0f), new Vector3(27f, 0f, 0f), new Vector3(0.16f, 0.16f, 3.6f), wood);
        // A rail that came down, and a stretch of the old wall.
        Beam(root, new Vector3(2.2f, 0.45f, 3.6f), new Vector3(0f, 34f, 76f), new Vector3(0.22f, 3.8f, 0.22f), wood);
        Kit("Fallen Log", root, new Vector3(-3.5f, 0f, 5.5f), 110f, 1f);
        Kit("Boulder A", root, new Vector3(-6.5f, 0f, -3f), 40f, 0.7f);
        Kit("Boulder B", root, new Vector3(-7f, 0f, 1.5f), 200f, 0.5f);
        Kit("Boulder A", root, new Vector3(-1f, 0f, -7.5f), 300f, 0.55f);
        return Save(root);
    }

    private static GameObject Stones()
    {
        Transform root = Root("Standing Stones");
        const int Count = 7;
        for (int i = 0; i < Count; i++)
        {
            float angle = i * 360f / Count;
            Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 6.6f;
            bool fallen = i == 2;
            Transform one = Kit(i % 2 == 0 ? "Boulder A" : "Boulder B", root, at, angle * 2.3f, 1f);
            one.localScale = fallen ? new Vector3(0.7f, 0.5f, 1.7f) : new Vector3(0.55f + 0.1f * (i % 3), 1.7f + 0.3f * (i % 3), 0.5f);
            one.localRotation = Quaternion.Euler(fallen ? 0f : (i % 3) * 3f - 3f, angle, 0f);
        }

        Kit("Dead Spruce", root, new Vector3(8.2f, 0f, 2.6f), 30f, 0.8f);
        return Save(root);
    }

    private static GameObject Farmstead()
    {
        Transform root = Root("Burnt Farmstead");
        // A long house burnt to its frame along one side of the yard, and a chimney still standing.
        float z = -5.5f;
        foreach (float x in new[] { -5f, -2.5f, 0f, 2.5f, 5f })
        {
            float height = x == 0f ? 1.6f : x == 2.5f ? 2.6f : 3.4f;
            Beam(root, new Vector3(x, height * 0.5f, z - 2.2f), new Vector3(2f, 0f, x), new Vector3(0.3f, height, 0.3f), charred);
            Beam(root, new Vector3(x, (height + 0.4f) * 0.5f, z + 2.2f), new Vector3(-3f, 0f, -x), new Vector3(0.3f, height + 0.4f, 0.3f), charred);
        }

        Beam(root, new Vector3(-2.5f, 3.3f, z - 2.2f), new Vector3(0f, 0f, 2f), new Vector3(6f, 0.24f, 0.24f), charred);
        Beam(root, new Vector3(-2.5f, 3.6f, z + 2.2f), new Vector3(0f, 0f, -2f), new Vector3(6f, 0.24f, 0.24f), charred);
        Beam(root, new Vector3(1.4f, 1f, z), new Vector3(30f, 20f, 60f), new Vector3(0.26f, 5.2f, 0.26f), charred);
        Beam(root, new Vector3(-3f, 0.7f, z + 0.6f), new Vector3(-20f, 50f, 72f), new Vector3(0.26f, 4.6f, 0.26f), charred);
        Beam(root, new Vector3(6.2f, 2.2f, z), Vector3.zero, new Vector3(1.3f, 4.4f, 1.3f), stone);
        // What stood in the yard.
        Beam(root, new Vector3(-7f, 0.6f, 3f), new Vector3(0f, 25f, 0f), new Vector3(2.1f, 1.2f, 1.3f), charred);
        Kit("Fallen Log", root, new Vector3(6f, 0f, 5.5f), 20f, 0.9f);
        Kit("Dead Spruce", root, new Vector3(-8.5f, 0f, -2f), 200f, 0.9f);
        Kit("Boulder B", root, new Vector3(8.5f, 0f, 1.5f), 90f, 0.45f);
        return Save(root);
    }

    private static GameObject Den()
    {
        Transform root = Root("Wolf Den");
        // Rocks round three sides, open to the trail, and what they have eaten.
        foreach ((float angle, float size, string kit) in new[] { (150f, 1f, "Boulder A"), (195f, 1.25f, "Boulder B"), (240f, 1.05f, "Boulder A"), (285f, 0.9f, "Boulder B"), (105f, 0.75f, "Boulder A") })
        {
            Kit(kit, root, Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 7.2f, angle * 1.7f, size);
        }

        Kit("Fallen Log", root, new Vector3(3.2f, 0f, -5.4f), 30f, 1f);
        Kit("Dead Spruce", root, new Vector3(-6f, 0f, 5.6f), 120f, 0.75f);
        for (int i = 0; i < 9; i++)
        {
            float angle = i * 83f;
            Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (2.8f + (i % 4) * 0.9f);
            Beam(root, at + Vector3.up * 0.06f, new Vector3(0f, angle * 1.7f, 88f), new Vector3(0.09f, 0.75f + 0.1f * (i % 3), 0.09f), bone);
        }

        return Save(root);
    }

    private static GameObject Barrow()
    {
        Transform root = Root("Old Barrow");
        // The mound along one side, its door of three stones, and kerbstones round the yard.
        var mound = Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.2f, -7.6f), Vector3.zero, new Vector3(8.5f, 3.6f, 5.6f), turf);
        mound.name = "Mound";
        Beam(root, new Vector3(-1.05f, 1f, -4.5f), new Vector3(0f, 0f, 3f), new Vector3(0.6f, 2f, 0.6f), stone);
        Beam(root, new Vector3(1.05f, 1f, -4.5f), new Vector3(0f, 0f, -3f), new Vector3(0.6f, 2f, 0.6f), stone);
        Beam(root, new Vector3(0f, 2.1f, -4.5f), Vector3.zero, new Vector3(3f, 0.5f, 0.75f), stone);
        var door = Part(PrimitiveType.Cube, root, new Vector3(0f, 0.9f, -4.72f), Vector3.zero, new Vector3(1.5f, 1.85f, 0.1f), dark);
        door.name = "Dark";
        foreach ((float angle, string kit) in new[] { (60f, "Boulder A"), (105f, "Boulder B"), (255f, "Boulder B"), (300f, "Boulder A") })
        {
            Transform kerb = Kit(kit, root, Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 8f, angle, 0.45f);
            kerb.localScale = new Vector3(0.45f, 0.75f, 0.45f);
        }

        return Save(root);
    }

    private static GameObject Hollow()
    {
        Transform root = Root("Guardian's Hollow");
        // Great dead trees and roots in a ring wide enough to fight in, with gaps to run through and a way in. What sleeps here wakes in the middle.
        const int Count = 11;
        for (int i = 0; i < Count; i++)
        {
            if (i == 0)
            {
                continue;
            }

            float angle = i * 360f / Count;
            Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (12.2f + (i % 2) * 0.8f);
            Transform tree = Kit("Dead Spruce", root, at, angle * 1.9f, 1.25f + (i % 3) * 0.2f);
            if (i % 3 == 0)
            {
                Kit("Boulder A", root, at * 0.86f, angle, 0.6f);
            }
        }

        foreach ((float angle, float length) in new[] { (35f, 5.5f), (110f, 6.5f), (180f, 5f), (255f, 6f), (320f, 5.2f) })
        {
            Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 9.4f;
            Beam(root, at + Vector3.up * 0.35f, new Vector3(0f, angle + 90f, 78f), new Vector3(0.75f, length, 0.75f), bark);
        }

        return Save(root);
    }

    // ----- Parts -----

    private static Transform Root(string name) => new GameObject(name).transform;

    private static Transform Kit(string kit, Transform parent, Vector3 at, float yaw, float size)
    {
        var prefab = Load<GameObject>($"{KitFolder}{kit}.prefab");
        var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        made.transform.localPosition = at;
        made.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        made.transform.localScale = Vector3.one * size;
        return made.transform;
    }

    private static Transform Beam(Transform parent, Vector3 at, Vector3 euler, Vector3 size, Material material) =>
        Part(PrimitiveType.Cube, parent, at, euler, size, material).transform;

    private static GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 euler, Vector3 size, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part;
    }

    private static GameObject Save(Transform root)
    {
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, $"{Prefabs}{root.name}.prefab");
        Object.DestroyImmediate(root.gameObject);
        return saved;
    }

    private static RaidSetPieceGuard Guard(string prefab, int count, int fromTier) =>
        new RaidSetPieceGuard { prefab = Load<GameObject>($"{Enemies}{prefab}.prefab") ?? throw new System.Exception("No creature: " + prefab), count = count, fromTier = fromTier };

    private static RaidSetPieceDefinition Piece(string name, string id, GameObject prefab, float radius, float weight, bool holdsRumours, params RaidSetPieceGuard[] guards)
    {
        string path = $"{Data}{name}.asset";
        var piece = Load<RaidSetPieceDefinition>(path);
        if (piece == null)
        {
            piece = ScriptableObject.CreateInstance<RaidSetPieceDefinition>();
            AssetDatabase.CreateAsset(piece, path);
        }

        var so = new SerializedObject(piece);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("prefab").objectReferenceValue = Load<GameObject>($"{Prefabs}{prefab.name}.prefab");
        so.FindProperty("radius").floatValue = radius;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("holdsRumours").boolValue = holdsRumours;
        SerializedProperty list = so.FindProperty("guards");
        list.arraySize = guards.Length;
        for (int i = 0; i < guards.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("prefab").objectReferenceValue = guards[i].prefab;
            entry.FindPropertyRelative("count").intValue = guards[i].count;
            entry.FindPropertyRelative("fromTier").intValue = guards[i].fromTier;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(piece);
        return piece;
    }

    private static Material Tint(string name, Color color)
    {
        string path = Prefabs + name + ".mat";
        var material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(stone);
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
