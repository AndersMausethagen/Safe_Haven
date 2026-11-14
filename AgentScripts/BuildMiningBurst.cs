#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;

/// <summary>Mined nodes crack apart: their sounds, how each family feels, what each node gives on top and the cracked models
/// (`--entry BuildMiningBurst.Run`). All of it is data afterwards: Content Editor › Gathering Nodes › Break Apart, and Harvest Feedback › Burst.
/// `--entry BuildMiningBurst.SyncYields` gives every node prefab the yields its definition states.</summary>
public static class BuildMiningBurst
{
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Mining/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";
    private const string Nodes = "Assets/_Project/Data/Gathering/";
    private const string Feedback = Nodes + "Feedback/";

    public static string Run()
    {
        AssetDatabase.Refresh();
        SoundDefinition burst = Sound("Rock Burst", 3, 1f, new Vector2(0.92f, 1.08f), 5f, 40f, 2);
        SoundDefinition knock = Sound("Rock Knock", 5, 0.4f, new Vector2(0.88f, 1.12f), 3f, 18f, 5);
        Feel("StoneFeedback", burst, knock, 4.2f, Interior("Rock Interior", new Color(0.72f, 0.69f, 0.65f), 0.05f));
        Feel("OreFeedback", burst, knock, 4.8f, Interior("Ore Interior", new Color(0.46f, 0.45f, 0.5f), 0.35f));

        var rocks = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/SmallRocks.asset");
        foreach ((string file, int pieces, int most) in new[]
        {
            ("GreyStone", 7, 2), ("Granite", 7, 2), ("Deepstone", 8, 1), ("CopperOre", 6, 2), ("IronOre", 6, 2), ("MoonsilverOre", 7, 1),
            ("Overgrowth/Boulder", 8, 2), ("Overgrowth/GreatBoulder", 10, 2),
        })
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(Nodes + file + ".asset");
            var so = new SerializedObject(node);
            so.FindProperty("burstPieces").intValue = pieces;
            SerializedProperty entries = so.FindProperty("burstYields").FindPropertyRelative("entries");
            entries.arraySize = 2;
            Line(entries.GetArrayElementAtIndex(0), node.Yields.Entries[0].Item, 1, most);
            Line(entries.GetArrayElementAtIndex(1), rocks, 2, 4);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(node);
        }

        AssetDatabase.SaveAssets();
        NodeFractureBuilder.CrackAll();
        return "eight kinds of node crack apart";
    }

    /// <summary>The definition is the source of truth: the prefabs had drifted from it, so the game gave less than the editor said.</summary>
    public static string SyncYields()
    {
        var changed = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:GatheringNodeDefinition"))
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (node.WorldPrefab == null || !node.WorldPrefab.TryGetComponent(out ResourceSource _) || !Differs(node)) continue;
            string path = AssetDatabase.GetAssetPath(node.WorldPrefab);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            var data = new SerializedObject(contents.GetComponent<ResourceSource>());
            ResourceNodePrefabBuilder.CopyYields(node, data.FindProperty("drops").FindPropertyRelative("entries"));
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            changed.Add(node.name);
        }

        AssetDatabase.SaveAssets();
        return "synced: " + string.Join(", ", changed);
    }

    private static bool Differs(GatheringNodeDefinition node)
    {
        ItemDropTable drops = node.WorldPrefab.GetComponent<ResourceSource>().Drops;
        if (drops.Entries.Count != node.Yields.Entries.Count) return true;
        for (int i = 0; i < drops.Entries.Count; i++)
        {
            ItemDropEntry a = drops.Entries[i], b = node.Yields.Entries[i];
            if (a.Drop != b.Drop || a.MinQuantity != b.MinQuantity || a.MaxQuantity != b.MaxQuantity || !Mathf.Approximately(a.Probability, b.Probability)) return true;
        }

        return false;
    }

    // Fresh stone, lighter than the weathered outside: stone_01's own texture, on the broken faces' flat mapping.
    private static Material Interior(string name, Color tint, float metal)
    {
        string path = Feedback + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Models/PolyHaven/stone_01/stone_01_Color.jpg"));
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Metallic", metal);
        material.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Line(SerializedProperty line, ItemDefinition item, int least, int most)
    {
        line.FindPropertyRelative("item").objectReferenceValue = item;
        line.FindPropertyRelative("resource").objectReferenceValue = null;
        line.FindPropertyRelative("minQuantity").intValue = least;
        line.FindPropertyRelative("maxQuantity").intValue = most;
        line.FindPropertyRelative("probability").floatValue = 1f;
    }

    private static void Feel(string file, SoundDefinition burst, SoundDefinition knock, float speed, Material interior)
    {
        var profile = AssetDatabase.LoadAssetAtPath<HarvestFeedbackProfile>(Feedback + file + ".asset");
        var so = new SerializedObject(profile);
        so.FindProperty("harvested").FindPropertyRelative("sound").objectReferenceValue = burst;
        so.FindProperty("burstSpeed").floatValue = speed;
        so.FindProperty("pieceSeconds").floatValue = 4.5f;
        so.FindProperty("burstShake").floatValue = 0.22f;
        so.FindProperty("pieceKnock").objectReferenceValue = knock;
        so.FindProperty("pieceInterior").objectReferenceValue = interior;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static SoundDefinition Sound(string name, int variants, float volume, Vector2 pitch, float full, float audible, int atOnce)
    {
        string path = Sounds + name + ".asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var so = new SerializedObject(sound);
        so.FindProperty("displayName").stringValue = name;
        SerializedProperty clips = so.FindProperty("clips");
        clips.arraySize = variants;
        for (int i = 0; i < variants; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Clips}{name} {i + 1}.wav");
            clips.GetArrayElementAtIndex(i).objectReferenceValue = clip != null ? clip : throw new System.Exception("No clip: " + name);
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("pitchRange").vector2Value = pitch;
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = atOnce;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }
}
