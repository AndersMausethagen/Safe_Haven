using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The sound system's data: how clips are loaded, the sounds of the moments that had none, the made sounds that replaced the
/// placeholders, and which sounds matter most. Run Tools/Audio/moments.py, world.py and interface.py first, then
/// unity command run_script --file AgentScripts/BuildAudio.cs --entry BuildAudio.All
/// </summary>
public static class BuildAudio
{
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Moments/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";
    private const string Top = "Assets/_Project/Audio/Sounds/";
    private const string Interface = "Assets/_Project/Audio/SFX/Safe Haven Interface/";
    private const string World = "Assets/_Project/Audio/SFX/Safe Haven World/";
    private const string Winds = "Assets/_Project/Audio/Placeholder/";

    // Long enough that decompressing it into memory costs real megabytes. Music streams from disk; shorter loops stay compressed
    // in memory, as a streamed clip can gap at its loop point.
    public const float CompressedFrom = 8f;
    public const float StreamedFrom = 60f;

    private static readonly string[] Background =
    {
        "Bird Chirp", "Small Bird Wings", "Crow Wings", "Crow Caw", "Cart Creak",
        "Footstep Forest", "Footstep Grass", "Footstep Wood", "Footstep Water", "Footstep Dirt", "Footstep Snow", "Footstep Stone",
    };

    private static readonly string[] Important =
    {
        "Player Hurt", "Parry", "Perfect Dodge", "Guard Break", "Click", "Quest Started", "Quest Completed", "Ward Toll", "War Horn", "Ward Mended", "Ward Falls",
    };

    public static string All()
    {
        int moved = LoadClips();
        Moments();
        Replacements();
        Wind();
        Priorities();
        AssetDatabase.SaveAssets();
        return $"{moved} clips changed how they load";
    }

    private static int LoadClips()
    {
        int moved = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (importer == null || clip == null || clip.length < CompressedFrom)
            {
                continue;
            }

            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            AudioClipLoadType wanted = clip.length >= StreamedFrom ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
            if (sample.loadType == AudioClipLoadType.DecompressOnLoad || (wanted == AudioClipLoadType.Streaming && sample.loadType != wanted))
            {
                sample.loadType = wanted;
                sample.preloadAudioData = wanted != AudioClipLoadType.Streaming;
                importer.defaultSampleSettings = sample;
                importer.SaveAndReimport();
                moved++;
            }
        }

        return moved;
    }

    private static void Moments()
    {
        var settings = Resources.Load<AudioSettingsDefinition>(AudioSettingsDefinition.ResourceName);
        var so = new SerializedObject(settings);
        so.FindProperty("consumed").objectReferenceValue = Sound("Bite", 0.42f, 0.2f, new Vector2(0.92f, 1.1f), 3f, 14f, 2, true, "Bite 1", "Bite 2", "Bite 3", "Bite 4");
        so.FindProperty("itemDropped").objectReferenceValue = Sound("Item Drop", 0.5f, 0.15f, new Vector2(0.9f, 1.1f), 3f, 16f, 3, true, "Item Drop 1", "Item Drop 2", "Item Drop 3");
        so.FindProperty("repaired").objectReferenceValue = so.FindProperty("built").objectReferenceValue;
        so.FindProperty("breachWarning").objectReferenceValue = Sound("Ward Toll", 0.5f, 0f, Vector2.one, 3f, 35f, 1, false, "Ward Toll");
        so.FindProperty("breachBegins").objectReferenceValue = Sound("War Horn", 0.4f, 0.05f, new Vector2(0.98f, 1.02f), 3f, 35f, 1, false, "War Horn");
        so.FindProperty("breachWon").objectReferenceValue = Sound("Ward Mended", 0.43f, 0f, Vector2.one, 3f, 35f, 1, false, "Ward Mended");
        so.FindProperty("breachLost").objectReferenceValue = Sound("Ward Falls", 0.76f, 0f, Vector2.one, 3f, 35f, 1, false, "Ward Falls");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
    }

    // The sounds SoundLibrarySetup doesn't own. Volumes are set from measured loudness, as the import normalizes every clip's peak.
    private static void Replacements()
    {
        Retune(Top + "LootRare.asset", 0.5f, Vector2.one, Clip(Interface, "Loot Rare"));
        Retune(Top + "LootEpic.asset", 0.68f, Vector2.one, Clip(Interface, "Loot Epic"));
        Retune(Top + "LootLegendary.asset", 0.57f, Vector2.one, Clip(Interface, "Loot Legendary"));
        Vector2 near = new Vector2(0.95f, 1.05f);
        Retune(Top + "CreatureDies.asset", 0.81f, near, Numbered("Creature Dies", 4));
        Retune(Top + "StructureBreaks.asset", 0.72f, near, Numbered("Structure Breaks", 3));
        Retune(Sounds + "Spotted.asset", 0.6f, new Vector2(0.97f, 1.03f), Numbered("Spotted", 3));
        Retune(Sounds + "TrollStep.asset", 1f, new Vector2(0.94f, 1.06f), Numbered("Troll Step", 5));

        string suspiciousPath = Sounds + "Suspicious.asset";
        if (AssetDatabase.LoadAssetAtPath<SoundDefinition>(suspiciousPath) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<SoundDefinition>(), suspiciousPath);
        }

        SoundDefinition suspicious = Retune(suspiciousPath, 0.5f, near, Numbered("Suspicious", 3));
        var data = new SerializedObject(suspicious);
        data.FindProperty("displayName").stringValue = "Suspicious";
        data.FindProperty("audibleDistance").floatValue = 22f;
        data.FindProperty("maxAtOnce").intValue = 2;
        data.ApplyModifiedPropertiesWithoutUndo();
        var perception = new SerializedObject(Resources.Load<PerceptionSettings>("PerceptionSettings"));
        perception.FindProperty("suspiciousSound").objectReferenceValue = suspicious;
        perception.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(perception.targetObject);
    }

    // Written over the clips the scenes list, in stereo. Normalizing would lift them over the volumes those scenes were mixed at,
    // and streaming can gap at the loop point.
    private static void Wind()
    {
        foreach (string name in new[] { "Ambience_WildsWind", "Ambience_HavenBreeze" })
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(Winds + name + ".wav");
            var so = new SerializedObject(importer);
            so.FindProperty("m_Normalize").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            importer.forceToMono = false;
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            sample.loadType = AudioClipLoadType.CompressedInMemory;
            sample.preloadAudioData = true;
            importer.defaultSampleSettings = sample;
            importer.SaveAndReimport();
        }
    }

    private static AudioClip Clip(string folder, string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + name + ".wav");
        return clip != null ? clip : throw new System.Exception("No clip " + name);
    }

    private static AudioClip[] Numbered(string stem, int count)
    {
        var clips = new AudioClip[count];
        for (int i = 0; i < count; i++)
        {
            clips[i] = Clip(World, $"{stem} {i + 1}");
        }

        return clips;
    }

    private static SoundDefinition Retune(string path, float volume, Vector2 pitch, params AudioClip[] clips)
    {
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            throw new System.Exception("No sound " + path);
        }

        var so = new SerializedObject(sound);
        SerializedProperty list = so.FindProperty("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("pitchRange").vector2Value = pitch;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    private static void Priorities()
    {
        foreach ((string[] names, SoundPriority priority) in new[] { (Background, SoundPriority.Background), (Important, SoundPriority.Important) })
        {
            foreach (string name in names)
            {
                var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(Sounds + name + ".asset");
                if (sound == null)
                {
                    Debug.LogWarning("No sound " + name);
                    continue;
                }

                var so = new SerializedObject(sound);
                so.FindProperty("priority").intValue = (int)priority;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(sound);
            }
        }
    }

    private static SoundDefinition Sound(string name, float volume, float variation, Vector2 pitch, float full, float audible, int atOnce, bool positional, params string[] clips)
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
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Clips + clips[i] + ".wav");
            list.GetArrayElementAtIndex(i).objectReferenceValue = clip != null ? clip : throw new System.Exception("No clip " + clips[i]);
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = volume;
        so.FindProperty("volumeVariation").floatValue = variation;
        so.FindProperty("pitchRange").vector2Value = pitch;
        so.FindProperty("positional").boolValue = positional;
        so.FindProperty("fullVolumeDistance").floatValue = full;
        so.FindProperty("audibleDistance").floatValue = audible;
        so.FindProperty("maxAtOnce").intValue = atOnce;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }
}
