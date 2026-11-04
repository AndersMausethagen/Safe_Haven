using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Gives every creature a voice and the heavy ones a tread (run with `unity command run_script --file
/// AgentScripts/BuildCreatureVoices.cs --entry BuildCreatureVoices.Apply`, after `python Tools/Audio/creatures.py`): an
/// Attack, Hurt and Death sound per creature under Audio/Sounds/Creatures, set on its definition, the howl and the shaman's
/// call on their actions, and a CreatureFootfalls on its prefab. Size is the sound's pitch: the clips are shared by kin.
/// Re-running updates them.
/// </summary>
public static class BuildCreatureVoices
{
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Creatures/";
    private const string TrollSteps = "Assets/_Project/Audio/SFX/Safe Haven World/Troll Step";
    private const string Folder = "Assets/_Project/Audio/Sounds/Creatures/";
    private const string Definitions = "Assets/_Project/Data/Monsters/";
    private const string Prefabs = "Assets/_Project/Prefabs/Enemies/";

    private sealed class Voice
    {
        public string Monster, Short, Attack, Hurt, Death, Caption;
        public float Pitch = 1f, Level = 1f, Reach = 1f;
    }

    private sealed class Tread
    {
        public string Prefab;
        public string Sound;
        public float Stride, Shake;
    }

    private static readonly Voice[] Voices =
    {
        new Voice { Monster = "ForestBittenWolf", Short = "Wolf", Attack = "Wolf Snarl", Hurt = "Wolf Yelp", Death = "Wolf Dies", Caption = "A wolf snarls" },
        new Voice { Monster = "SmallMossTroll", Short = "Small Troll", Attack = "Troll Roar", Hurt = "Troll Hurt", Death = "Troll Dies", Pitch = 1.32f, Level = 0.85f, Caption = "A troll bellows" },
        new Voice { Monster = "OldMossTroll", Short = "Old Troll", Attack = "Troll Roar", Hurt = "Troll Hurt", Death = "Troll Dies", Pitch = 0.82f, Reach = 1.5f, Caption = "The old troll roars" },
        new Voice { Monster = "MossShaman", Short = "Shaman", Attack = "Troll Roar", Hurt = "Troll Hurt", Death = "Troll Dies", Pitch = 1.14f, Level = 0.85f, Caption = "A shaman growls" },
        new Voice { Monster = "Draug", Short = "Draug", Attack = "Draug Moan", Hurt = "Draug Hurt", Death = "Draug Dies", Caption = "A draug moans" },
        new Voice { Monster = "BarrowShieldbearer", Short = "Shieldbearer", Attack = "Draug Moan", Hurt = "Draug Hurt", Death = "Draug Dies", Pitch = 0.86f, Caption = "A draug moans" },
        new Voice { Monster = "RootGuardian", Short = "Guardian", Attack = "Root Strain", Hurt = "Root Hurt", Death = "Root Dies", Caption = "Old wood strains" },
        new Voice { Monster = "Rootling", Short = "Rootling", Attack = "Root Strain", Hurt = "Root Hurt", Death = "Root Dies", Pitch = 1.85f, Level = 0.55f, Reach = 0.7f, Caption = "Roots creak" },
        new Voice { Monster = "TheHeartroot", Short = "Heartroot", Attack = "Root Strain", Hurt = "Root Hurt", Death = "Root Dies", Pitch = 0.66f, Reach = 1.6f, Caption = "The Heartroot strains" },
        new Voice { Monster = "BlightBoar", Short = "Boar", Attack = "Boar Charge", Hurt = "Boar Hurt", Death = "Boar Dies", Caption = "A boar squeals" },
        new Voice { Monster = "BogSpitter", Short = "Bog Spitter", Attack = "Bog Spit", Hurt = "Bog Hurt", Death = "Bog Dies", Caption = "Something gurgles" },
        new Voice { Monster = "MarshLight", Short = "Marsh Light", Attack = "Light Rises", Hurt = "Light Hurt", Death = "Light Dies", Level = 0.8f, Caption = "A thin light sings" },
        new Voice { Monster = "Mistwing", Short = "Mistwing", Attack = "Wing Screech", Hurt = "Wing Chitter", Death = "Wing Dies", Level = 0.85f, Caption = "A mistwing shrieks" },
        new Voice { Monster = "Huldra", Short = "Huldra", Attack = "Huldra Wail", Hurt = "Huldra Hurt", Death = "Huldra Dies", Reach = 1.2f, Caption = "The huldra wails" },
    };

    private static readonly Tread[] Treads =
    {
        new Tread { Prefab = "ForestBittenWolf", Sound = "Paw Step", Stride = 1.7f },
        new Tread { Prefab = "BlightBoar", Sound = "Boar Step", Stride = 1.5f },
        new Tread { Prefab = "Draug", Sound = "Mail Tread", Stride = 1.3f },
        new Tread { Prefab = "BarrowShieldbearer", Sound = "Mail Tread", Stride = 1.2f },
        new Tread { Prefab = "SmallMossTroll", Sound = "Small Troll Step", Stride = 1.5f },
        new Tread { Prefab = "MossShaman", Sound = "Small Troll Step", Stride = 1.7f },
        new Tread { Prefab = "RootGuardian", Sound = "Guardian Step", Stride = 1.9f, Shake = 0.06f },
        new Tread { Prefab = "TheHeartroot", Sound = "Heartroot Step", Stride = 3f, Shake = 0.22f },
    };

    public static string Apply()
    {
        var log = new List<string>();
        foreach (Voice voice in Voices)
        {
            SoundDefinition attack = Sound($"{voice.Short} Attack", voice.Attack, voice.Caption, voice.Pitch, 0.8f * voice.Level, SoundPriority.Important, 2, 0.12f, 5f, 32f * voice.Reach);
            SoundDefinition hurt = Sound($"{voice.Short} Hurt", voice.Hurt, "", voice.Pitch, 0.7f * voice.Level, SoundPriority.Normal, 2, 0.22f, 4f, 26f * voice.Reach);
            SoundDefinition death = Sound($"{voice.Short} Death", voice.Death, "", voice.Pitch, 0.85f * voice.Level, SoundPriority.Normal, 3, 0.05f, 5f, 38f * voice.Reach);
            var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{Definitions}{voice.Monster}.asset");
            if (definition == null)
            {
                throw new InvalidOperationException($"No monster definition for {voice.Monster}");
            }

            var so = new SerializedObject(definition);
            so.FindProperty("attackSound").objectReferenceValue = attack;
            so.FindProperty("hurtSound").objectReferenceValue = hurt;
            so.FindProperty("deathSound").objectReferenceValue = death;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        SoundDefinition howl = Sound("Wolf Howl", "Wolf Howl", "A wolf howls", 1f, 0.8f, SoundPriority.Important, 2, 0.4f, 8f, 55f);
        SoundDefinition call = Sound("Shaman Call", "Shaman Call", "A shaman calls to the ground", 1f, 0.8f, SoundPriority.Important, 1, 0.4f, 6f, 40f);
        SoundDefinition warCry = Sound("Draug War Cry", "Draug Moan", "A draug cries out", 0.74f, 0.85f, SoundPriority.Important, 1, 0.4f, 8f, 48f);
        SoundDefinition callsKin = Sound("Old Troll Call", "Troll Roar", "The old troll calls its kin", 0.66f, 0.9f, SoundPriority.Important, 1, 0.4f, 10f, 70f);
        SoundDefinition rootsCalled = Sound("Guardian Call", "Root Strain", "The ground stirs", 0.78f, 0.85f, SoundPriority.Important, 1, 0.4f, 8f, 44f);
        SoundDefinition heartCalls = Sound("Heartroot Call", "Root Strain", "The ground stirs", 0.54f, 0.95f, SoundPriority.Important, 1, 0.4f, 10f, 60f);
        ActionSound("ForestBittenWolf", MonsterAttackKind.Howl, howl, log);
        ActionSound("Draug", MonsterAttackKind.Howl, warCry, log);
        ActionSound("MossShaman", MonsterAttackKind.Summon, call, log);
        ActionSound("MossShaman", MonsterAttackKind.Howl, call, log);
        ActionSound("OldMossTroll", MonsterAttackKind.Summon, callsKin, log);
        ActionSound("RootGuardian", MonsterAttackKind.Summon, rootsCalled, log);
        ActionSound("TheHeartroot", MonsterAttackKind.Summon, heartCalls, log);

        Sound("Paw Step", "Paw Step", "", 1f, 0.42f, SoundPriority.Background, 3, 0.06f, 2.5f, 14f);
        Sound("Boar Step", "Paw Step", "", 0.74f, 0.55f, SoundPriority.Background, 2, 0.06f, 3f, 18f);
        Sound("Mail Tread", "Mail Tread", "", 1f, 0.5f, SoundPriority.Background, 3, 0.08f, 3f, 20f);
        Sound("Small Troll Step", TrollSteps, "", 1.45f, 0.42f, SoundPriority.Background, 2, 0.08f, 4f, 24f);
        Sound("Guardian Step", TrollSteps, "", 1.2f, 0.55f, SoundPriority.Background, 2, 0.1f, 5f, 30f);
        Sound("Heartroot Step", TrollSteps, "", 0.9f, 0.85f, SoundPriority.Normal, 2, 0.1f, 8f, 50f);
        foreach (Tread tread in Treads)
        {
            AddTread(tread, log);
        }

        AssetDatabase.SaveAssets();
        return $"{Voices.Length} voices, {Treads.Length} treads. " + string.Join(" ", log);
    }

    private static SoundDefinition Sound(string name, string clipStem, string caption, float pitch, float volume, SoundPriority priority, int maxAtOnce, float gap, float full, float audible)
    {
        string path = $"{Folder}{name}.asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            System.IO.Directory.CreateDirectory(Folder);
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        var so = new SerializedObject(sound);
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("caption").stringValue = caption;
        SerializedProperty clips = so.FindProperty("clips");
        clips.ClearArray();
        string stem = clipStem.StartsWith("Assets/", StringComparison.Ordinal) ? clipStem : Clips + clipStem;
        for (int i = 1; i <= 6; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{stem} {i}.wav");
            if (clip != null)
            {
                clips.InsertArrayElementAtIndex(clips.arraySize);
                clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
            }
        }

        if (clips.arraySize == 0)
        {
            throw new InvalidOperationException($"No clips for {stem}: run Tools/Audio/creatures.py first.");
        }

        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("volumeVariation").floatValue = 0.1f;
        so.FindProperty("pitchRange").vector2Value = new Vector2(pitch * 0.95f, pitch * 1.05f);
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = maxAtOnce;
        so.FindProperty("minimumGap").floatValue = gap;
        so.FindProperty("priority").intValue = (int)priority;
        so.ApplyModifiedPropertiesWithoutUndo();
        return sound;
    }

    private static void ActionSound(string monster, MonsterAttackKind kind, SoundDefinition sound, List<string> log)
    {
        var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"{Definitions}{monster}.asset");
        var so = new SerializedObject(definition);
        SerializedProperty attacks = so.FindProperty("attacks");
        int set = 0;
        for (int i = 0; i < attacks.arraySize; i++)
        {
            SerializedProperty attack = attacks.GetArrayElementAtIndex(i);
            if (attack.FindPropertyRelative("kind").intValue == (int)kind)
            {
                attack.FindPropertyRelative("sound").objectReferenceValue = sound;
                set++;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add($"{monster} {kind}: {set}.");
    }

    // Through prefab contents, so it never lands in the open scene.
    private static void AddTread(Tread tread, List<string> log)
    {
        string path = $"{Prefabs}{tread.Prefab}.prefab";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>($"{Folder}{tread.Sound}.asset");
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (!root.TryGetComponent(out MonsterAI _))
            {
                log.Add($"{tread.Prefab} has no MonsterAI on its root: no tread.");
                return;
            }

            if (!root.TryGetComponent(out CreatureFootfalls footfalls))
            {
                footfalls = root.AddComponent<CreatureFootfalls>();
            }

            var so = new SerializedObject(footfalls);
            so.FindProperty("stepSound").objectReferenceValue = sound;
            so.FindProperty("strideMeters").floatValue = tread.Stride;
            so.FindProperty("shake").floatValue = tread.Shake;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
