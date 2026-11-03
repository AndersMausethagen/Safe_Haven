using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes beating the Heartroot change things: how it wakes, its music, what stands at home and what people say, and Troll Valley, which it opens.
/// Troll Valley uses the Misty Woods' scene with rules, creatures and tiers of its own, as it has no art yet. Run after BuildFirstBoss and BuildSetPieces. Safe to run again.
/// Run: unity command script run -- --file AgentScripts/BuildBossTurningPoint.cs --entry BuildBossTurningPoint.Run
/// </summary>
public static class BuildBossTurningPoint
{
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Boss/";
    private const string Trophy = "Assets/_Project/Prefabs/Raids/SetPieces/Heartroot Trophy.prefab";
    private const string MistyLayout = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string TrollLayout = "Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset";

    public static string Run()
    {
        AssetDatabase.Refresh();
        var boss = Load<BossDefinition>("Assets/_Project/Data/Monsters/Boss The Heartroot.asset");
        SoundDefinition wake = WakeSound();
        BossTrophy trophy = MakeTrophy();
        Edit(boss, so =>
        {
            so.FindProperty("wakeSeconds").floatValue = 3.2f;
            so.FindProperty("wakeSound").objectReferenceValue = wake;
            so.FindProperty("trophy").objectReferenceValue = trophy;
            so.FindProperty("defeatLine").stringValue = "The Heartroot lies still. What it knew of the forest's hold is yours now: you have learned Heartroot's Grasp. And something has opened: the portal can reach Troll Valley.";
            SerializedProperty tracks = so.FindProperty("music").FindPropertyRelative("tracks");
            tracks.arraySize = 1;
            tracks.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = Load<AudioClip>(Clips + "Heartroot Fight.wav") ?? throw new System.Exception("No fight music");
            Strings(so.FindProperty("homeLines"), new[]
            {
                "They say it's dead. The Heartroot. I felt the ground go quiet, and I don't know if I like it quiet.",
                "You killed it? Truly? Then what else is out there that we were told couldn't be?",
                "I'd have said nobody could. I'll say it about something else next time, and you can prove me wrong again.",
                "Whatever you carried back from that hollow, keep it away from the fire. Please.",
            });
        });

        Edit(SettlementSettings.Current, so =>
        {
            SerializedProperty bosses = so.FindProperty("bosses");
            bosses.arraySize = 1;
            bosses.GetArrayElementAtIndex(0).objectReferenceValue = boss;
        });

        string valley = MakeTrollValley(boss);
        AssetDatabase.SaveAssets();
        return valley;
    }

    private static SoundDefinition WakeSound()
    {
        const string path = "Assets/_Project/Audio/Sounds/Library/Heartroot Wakes.asset";
        var sound = Load<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        Edit(sound, so =>
        {
            so.FindProperty("displayName").stringValue = "Heartroot Wakes";
            so.FindProperty("caption").stringValue = "Something huge is waking";
            SerializedProperty clips = so.FindProperty("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = Load<AudioClip>(Clips + "Heartroot Wakes.wav") ?? throw new System.Exception("No wake sound");
            so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
            so.FindProperty("volume").floatValue = 0.95f;
            so.FindProperty("positional").boolValue = false;
        });
        return sound;
    }

    // A split length of the old trunk, with what kept it alive gone cold in the middle.
    private static BossTrophy MakeTrophy()
    {
        var root = new GameObject("Heartroot Trophy");
        var bark = Load<Material>("Assets/_Project/Art/Forest/Materials/Bark.mat");
        var cut = Load<Material>("Assets/_Project/Art/Forest/Materials/Log Cut Face.mat");
        var ember = Load<Material>("Assets/_Project/Materials/Haven/Base Ember.mat");
        foreach ((PrimitiveType type, Vector3 at, Vector3 tilt, Vector3 size, Material look) in new[]
        {
            (PrimitiveType.Cylinder, new Vector3(-0.42f, 0.7f, 0f), new Vector3(0f, 0f, 9f), new Vector3(0.75f, 0.75f, 1.1f), bark),
            (PrimitiveType.Cylinder, new Vector3(0.42f, 0.6f, 0f), new Vector3(0f, 0f, -11f), new Vector3(0.75f, 0.65f, 1.05f), bark),
            (PrimitiveType.Cylinder, new Vector3(-0.42f, 1.46f, 0f), new Vector3(0f, 0f, 9f), new Vector3(0.7f, 0.02f, 1.02f), cut),
            (PrimitiveType.Cylinder, new Vector3(0.42f, 1.26f, 0f), new Vector3(0f, 0f, -11f), new Vector3(0.7f, 0.02f, 0.97f), cut),
            (PrimitiveType.Sphere, new Vector3(0f, 0.8f, 0f), Vector3.zero, new Vector3(0.42f, 0.5f, 0.42f), ember),
        })
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = at;
            part.transform.localRotation = Quaternion.Euler(tilt);
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = look;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        Interactable interactable = root.AddComponent<Interactable>();
        interactable.Configure(InteractionAnimation.None, 0f, 2.4f, false);
        Edit(interactable, so => so.FindProperty("kind").intValue = (int)InteractionKind.Use);
        BossTrophy trophy = root.AddComponent<BossTrophy>();
        Edit(trophy, so => so.FindProperty("text").stringValue = "Part of the old trunk, split down the middle. It is still warm, a little, right at the heart. Nobody has wanted to ask why.");
        PrefabUtility.SaveAsPrefabAsset(root, Trophy);
        Object.DestroyImmediate(root);
        return Load<GameObject>(Trophy).GetComponent<BossTrophy>();
    }

    private static string MakeTrollValley(BossDefinition boss)
    {
        var misty = Load<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        var valley = Load<RaidDefinition>("Assets/_Project/Data/Raids/TrollValley.asset");
        RaidLayoutRules layout = MakeLayout();

        var was = new SerializedObject(valley);
        string id = was.FindProperty("id").stringValue;
        string displayName = was.FindProperty("displayName").stringValue;
        string description = was.FindProperty("description").stringValue;
        EditorUtility.CopySerialized(misty, valley);
        valley.name = "TrollValley";
        Edit(valley, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = displayName;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("tier").intValue = 2;
            so.FindProperty("locked").boolValue = true;
            so.FindProperty("unlockedBy").objectReferenceValue = boss;
            so.FindProperty("lockedText").stringValue = "The way to Troll Valley lies beyond the Misty Woods. Something old in the deep woods holds it shut.";
            so.FindProperty("layoutRules").objectReferenceValue = layout;

            // No first tier here: it is for those who have beaten the Heartroot.
            SerializedProperty tiers = so.FindProperty("difficulties");
            if (tiers.arraySize > 0 && tiers.GetArrayElementAtIndex(0).FindPropertyRelative("tier").intValue == 1)
            {
                tiers.DeleteArrayElementAtIndex(0);
            }

            SerializedProperty lists = so.FindProperty("spawnLists");
            for (int i = 0; i < lists.arraySize; i++)
            {
                SerializedProperty list = lists.GetArrayElementAtIndex(i);
                if (list.FindPropertyRelative("name").stringValue == "Creatures")
                {
                    Weights(list.FindPropertyRelative("entries"), new Dictionary<string, float>
                    {
                        ["SmallMossTroll"] = 34f, ["Draug"] = 16f, ["BarrowShieldbearer"] = 12f, ["RootGuardian"] = 14f, ["MossShaman"] = 8f,
                        ["WolfPack"] = 6f, ["BlightBoar"] = 4f, ["Mistwing"] = 4f, ["RootlingSwarm"] = 2f, ["BogSpitter"] = 3f, ["MarshLight"] = 2f,
                    });
                }
            }

            SetEnemies(so.FindProperty("knownEnemies"), "SmallMossTroll", "Draug", "BarrowShieldbearer", "RootGuardian", "OldMossTroll");
        });

        RaidRumoursSystem rumours = valley.GetSystem<RaidRumoursSystem>();
        var pool = new List<RaidRumourDefinition>(rumours.Pool);
        pool.RemoveAll(one => one != null && one.name == "TheHeartroot");
        rumours.Configure(pool, rumours.Count);
        EditorUtility.SetDirty(valley);

        return $"{displayName}: tiers {valley.Difficulties.Count}, from tier {valley.LowestDifficulty.tier}; rumours {pool.Count}; misty still has {misty.GetSystem<RaidRumoursSystem>().Pool.Count}";
    }

    // Steeper ground and thinner forest than the Woods: ridges to look across, hollows to be caught in.
    private static RaidLayoutRules MakeLayout()
    {
        var source = Load<RaidLayoutRules>(MistyLayout);
        var layout = Load<RaidLayoutRules>(TrollLayout);
        if (layout == null)
        {
            layout = ScriptableObject.CreateInstance<RaidLayoutRules>();
            AssetDatabase.CreateAsset(layout, TrollLayout);
        }

        EditorUtility.CopySerialized(source, layout);
        layout.name = "TrollValley Layout";
        Edit(layout, so =>
        {
            so.FindProperty("ridgeHeight").floatValue = source.RidgeHeight * 1.55f;
            so.FindProperty("ridgeWidth").floatValue = source.RidgeWidth * 0.8f;
            so.FindProperty("hills").floatValue = source.Hills * 1.6f;
            so.FindProperty("rimHeight").floatValue = source.RimHeight * 1.35f;
            so.FindProperty("forestDensity").floatValue = source.ForestDensity * 0.62f;
            so.FindProperty("ponds").vector2IntValue = new Vector2Int(0, 1);
            SerializedProperty moods = so.FindProperty("moods");
            for (int i = moods.arraySize - 1; i >= 0; i--)
            {
                string name = moods.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                if (name != "The Hollows" && name != "Windthrow")
                {
                    moods.DeleteArrayElementAtIndex(i);
                }
            }

            // The Hollow is the Woods' own.
            SerializedProperty pieces = so.FindProperty("setPieces");
            for (int i = pieces.arraySize - 1; i >= 0; i--)
            {
                if (pieces.GetArrayElementAtIndex(i).objectReferenceValue?.name == "Guardian's Hollow")
                {
                    pieces.DeleteArrayElementAtIndex(i);
                    if (i < pieces.arraySize && pieces.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        pieces.DeleteArrayElementAtIndex(i);
                    }
                }
            }
        });
        return layout;
    }

    private static void Weights(SerializedProperty entries, Dictionary<string, float> weights)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string name = entry.FindPropertyRelative("prefab").objectReferenceValue?.name;
            if (name != null && weights.TryGetValue(name, out float weight))
            {
                entry.FindPropertyRelative("weight").floatValue = weight;
            }
        }
    }

    private static void SetEnemies(SerializedProperty list, params string[] names)
    {
        list.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = Load<MonsterDefinition>($"Assets/_Project/Data/Monsters/{names[i]}.asset");
        }
    }

    private static void Strings(SerializedProperty list, string[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }

    private static void Edit(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
