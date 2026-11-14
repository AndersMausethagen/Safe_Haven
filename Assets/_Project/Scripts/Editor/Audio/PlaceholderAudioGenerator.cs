using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Makes simple synthesized placeholder audio (music loops, ambience, death effects and rare-loot chimes), the effect
/// Sound assets, Audio Settings (with placeholder default music where it has none) and Rarity Settings, so the audio system
/// works before real audio exists. Scene music is set per scene in the Sound Editor. Existing assets and music already set
/// are never overwritten, so it is safe to run again. Safe Haven > Setup > Audio > Generate Placeholder Sounds.
/// </summary>
public static class PlaceholderAudioGenerator
{
    private const int Rate = 22050;
    private const string ClipFolder = "Assets/_Project/Audio/Placeholder";
    private const string SoundFolder = "Assets/_Project/Audio/Sounds";

    private const string SettingsPath = "Assets/_Project/Resources/AudioSettings.asset";
    private const string RaritySettingsPath = "Assets/_Project/Resources/RaritySettings.asset";

    [MenuItem("Safe Haven/Setup/Audio/Generate Placeholder Sounds", false, 200)]
    public static void Generate()
    {
        if (RetiredBuilders.Refuse("Generate Placeholder Sounds")) return;
        PrefabBuildUtility.EnsureFolder(ClipFolder);
        PrefabBuildUtility.EnsureFolder(SoundFolder);
        PrefabBuildUtility.EnsureFolder("Assets/_Project/Resources");

        // Music and ambience are plain clips: scenes and Audio Settings list them directly.
        AudioClip havenTheme = Clip("Music_HavenTheme", true, HavenTheme);
        Clip("Music_WildsTheme", true, WildsTheme);
        AudioClip combat = Clip("Music_Combat", true, CombatTheme);
        Clip("Music_FinalMinute", true, FinalMinute);
        Clip("Ambience_HavenBreeze", true, () => Wind(11, 0.02f));
        Clip("Ambience_WildsWind", true, () => Wind(23, 0.06f));
        SoundDefinition creatureDies = Sound("Creature Dies", AudioChannel.Effects, 0.85f, true,
            Clip("Sfx_CreatureDies_A", false, () => Growl(210f, 1)), Clip("Sfx_CreatureDies_B", false, () => Growl(170f, 2)));
        SoundDefinition structureBreaks = Sound("Structure Breaks", AudioChannel.Effects, 0.9f, true,
            Clip("Sfx_StructureBreaks_A", false, () => Crash(3)), Clip("Sfx_StructureBreaks_B", false, () => Crash(4)));
        SoundDefinition lootRare = Sound("Loot Rare", AudioChannel.Effects, 0.6f, false, Clip("Sfx_LootRare", false, () => Chime(new[] { 76, 83 })));
        SoundDefinition lootEpic = Sound("Loot Epic", AudioChannel.Effects, 0.7f, false, Clip("Sfx_LootEpic", false, () => Chime(new[] { 72, 76, 79, 84 })));
        SoundDefinition lootLegendary = Sound("Loot Legendary", AudioChannel.Effects, 0.8f, false,
            Clip("Sfx_LootLegendary", false, () => Chime(new[] { 67, 72, 76, 79, 84, 88 })));
        EnsureRaritySettings(lootRare, lootEpic, lootLegendary);

        var settings = AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<AudioSettingsDefinition>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            var data = new SerializedObject(settings);
            data.FindProperty("creatureKilled").objectReferenceValue = creatureDies;
            data.FindProperty("structureDestroyed").objectReferenceValue = structureBreaks;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        var settingsData = new SerializedObject(settings);
        FillIfEmpty(settingsData.FindProperty("defaultExploring"), havenTheme, 0.55f);
        FillIfEmpty(settingsData.FindProperty("defaultCombat"), combat, 0.6f);
        settingsData.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.SaveAssets();
        Debug.Log("Placeholder audio ready: clips in " + ClipFolder + ", effect sounds in " + SoundFolder + ", Audio Settings filled where empty.");
    }

    // ----- Assets -----

    // Creates Resources/RaritySettings with the default styles and gives the announced rarities their pickup sounds.
    // Sounds already set by a designer are kept.
    private static void EnsureRaritySettings(SoundDefinition rare, SoundDefinition epic, SoundDefinition legendary)
    {
        var settings = AssetDatabase.LoadAssetAtPath<RaritySettingsDefinition>(RaritySettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RaritySettingsDefinition>();
            AssetDatabase.CreateAsset(settings, RaritySettingsPath);
        }

        var data = new SerializedObject(settings);
        SerializedProperty styles = data.FindProperty("styles");
        for (int i = 0; i < styles.arraySize; i++)
        {
            SerializedProperty style = styles.GetArrayElementAtIndex(i);
            SerializedProperty sound = style.FindPropertyRelative("pickupSound");
            var rarity = (ItemRarity)style.FindPropertyRelative("rarity").enumValueIndex;
            SoundDefinition placeholder = rarity == ItemRarity.Rare ? rare : rarity == ItemRarity.Epic ? epic : rarity == ItemRarity.Legendary ? legendary : null;
            if (sound.objectReferenceValue == null && placeholder != null)
            {
                sound.objectReferenceValue = placeholder;
            }
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static AudioClip Clip(string name, bool music, Func<float[]> synth)
    {
        string path = $"{ClipFolder}/{name}.wav";
        var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (existing != null)
        {
            return existing;
        }

        float[] samples = synth();
        WriteWav(path, samples);
        AssetDatabase.ImportAsset(path);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;
        sampleSettings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
        sampleSettings.quality = 0.6f;
        importer.defaultSampleSettings = sampleSettings;
        importer.forceToMono = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }

    private static SoundDefinition Sound(string displayName, AudioChannel channel, float volume, bool positional, params AudioClip[] clips)
    {
        string path = $"{SoundFolder}/{PrefabBuildUtility.MakeFileName(displayName)}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (existing != null)
        {
            return existing;
        }

        var sound = ScriptableObject.CreateInstance<SoundDefinition>();
        AssetDatabase.CreateAsset(sound, path);
        var data = new SerializedObject(sound);
        data.FindProperty("displayName").stringValue = displayName;
        data.FindProperty("channel").enumValueIndex = (int)channel;
        data.FindProperty("volume").floatValue = volume;
        data.FindProperty("positional").boolValue = positional;
        if (!positional)
        {
            data.FindProperty("volumeVariation").floatValue = 0f;
            data.FindProperty("pitchRange").vector2Value = Vector2.one;
        }

        SerializedProperty list = data.FindProperty("clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return sound;
    }

    private static void FillIfEmpty(SerializedProperty playlist, AudioClip clip, float volume)
    {
        SerializedProperty tracks = playlist.FindPropertyRelative("tracks");
        if (tracks.arraySize > 0 || clip == null)
        {
            return;
        }

        tracks.arraySize = 1;
        tracks.GetArrayElementAtIndex(0).FindPropertyRelative("clip").objectReferenceValue = clip;
        tracks.GetArrayElementAtIndex(0).FindPropertyRelative("volume").floatValue = volume;
    }
    private static void WriteWav(string path, float[] samples)
    {
        using (var stream = new FileStream(Path.GetFullPath(path), FileMode.Create))
        using (var writer = new BinaryWriter(stream))
        {
            int bytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + bytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(Rate);
            writer.Write(Rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(bytes);
            foreach (float sample in samples)
            {
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32000f));
            }
        }
    }

    // ----- Synthesis. Loops add notes modulo the loop length, so anything ringing past the end wraps to the start. -----

    private static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);

    private static void Note(float[] buffer, float start, float duration, float frequency, float volume, float attack, float release,
        int harmonics = 1, float vibrato = 0f)
    {
        int first = Mathf.RoundToInt(start * Rate);
        int count = Mathf.RoundToInt((duration + release) * Rate);
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float envelope = t < attack ? t / attack : t < duration ? 1f : Mathf.Max(0f, 1f - (t - duration) / release);
            phase += 2f * Mathf.PI * frequency * (1f + vibrato * Mathf.Sin(2f * Mathf.PI * 5f * t)) / Rate;
            float value = 0f;
            for (int h = 1; h <= harmonics; h++)
            {
                value += Mathf.Sin(phase * h) / (h * h);
            }

            buffer[(first + i) % buffer.Length] += value * envelope * volume;
        }
    }

    private static void Pluck(float[] buffer, float start, float frequency, float volume, float decay)
    {
        int first = Mathf.RoundToInt(start * Rate);
        int count = Mathf.RoundToInt(decay * 4f * Rate);
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)Rate;
            float value = Mathf.Sin(2f * Mathf.PI * frequency * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * frequency * t);
            buffer[(first + i) % buffer.Length] += value * volume * Mathf.Exp(-t / decay) * Mathf.Min(1f, t * 200f);
        }
    }

    private static void Kick(float[] buffer, float start, float volume)
    {
        int first = Mathf.RoundToInt(start * Rate);
        float phase = 0f;
        for (int i = 0; i < Rate / 3; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * Mathf.Lerp(110f, 40f, Mathf.Min(1f, t * 8f)) / Rate;
            buffer[(first + i) % buffer.Length] += Mathf.Sin(phase) * volume * Mathf.Exp(-t * 9f);
        }
    }

    private static void Hit(float[] buffer, float start, float volume, float decay, System.Random random, float smoothing)
    {
        int first = Mathf.RoundToInt(start * Rate);
        float filtered = 0f;
        for (int i = 0; i < Mathf.RoundToInt(decay * 5f * Rate); i++)
        {
            float t = i / (float)Rate;
            filtered += ((float)random.NextDouble() * 2f - 1f - filtered) * smoothing;
            buffer[(first + i) % buffer.Length] += filtered * volume * Mathf.Exp(-t / decay);
        }
    }

    private static float[] Finish(float[] buffer, float peak)
    {
        float max = 0.0001f;
        foreach (float sample in buffer)
        {
            max = Mathf.Max(max, Mathf.Abs(sample));
        }

        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] *= peak / max;
        }

        return buffer;
    }

    // A bright rising arpeggio: more notes for rarer finds.
    private static float[] Chime(int[] notes)
    {
        var buffer = new float[Rate * 2];
        for (int i = 0; i < notes.Length; i++)
        {
            Note(buffer, i * 0.07f, 0.12f, Midi(notes[i]), 0.25f, 0.005f, 0.6f, 3);
        }

        return Finish(buffer, 0.7f);
    }

    // Warm and calm: a slow major progression with soft pads and a gentle arpeggio.
    private static float[] HavenTheme()
    {
        var buffer = new float[Rate * 16];
        int[][] chords = { new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 } };
        for (int c = 0; c < 4; c++)
        {
            foreach (int note in chords[c])
            {
                Note(buffer, c * 4f, 3.6f, Midi(note - 12), 0.18f, 0.8f, 1.2f, 2);
            }

            Note(buffer, c * 4f, 3.8f, Midi(chords[c][0] - 24), 0.2f, 0.3f, 0.8f, 3);
            int[] pattern = { 0, 1, 2, 1, 2, 1, 0, 1 };
            for (int i = 0; i < 8; i++)
            {
                Pluck(buffer, c * 4f + i * 0.5f, Midi(chords[c][pattern[i]] + 12), i == 0 ? 0.16f : 0.1f, 0.35f);
            }
        }

        return Finish(buffer, 0.8f);
    }

    // Uneasy: a minor drone with slow swells, a distant pulse and sparse high notes.
    private static float[] WildsTheme()
    {
        var buffer = new float[Rate * 16];
        Note(buffer, 0f, 16f, Midi(45), 0.22f, 2f, 0.01f, 3, 0.002f);
        Note(buffer, 0f, 16f, Midi(52), 0.12f, 3f, 0.01f, 2, 0.003f);
        for (int beat = 0; beat < 8; beat++)
        {
            Kick(buffer, beat * 2f, 0.25f);
        }

        int[] melody = { 72, 76, 74, 71, 69, 72, 76, 81 };
        for (int i = 0; i < melody.Length; i++)
        {
            Note(buffer, 1f + i * 2f, 0.8f, Midi(melody[i]), i % 3 == 0 ? 0.07f : 0.04f, 0.3f, 1.5f, 1, 0.004f);
        }

        return Finish(buffer, 0.75f);
    }

    // Driving: drums on every beat, a pushing minor bass line and stabs. 128 bpm.
    private static float[] CombatTheme()
    {
        const float beat = 60f / 128f;
        var buffer = new float[Mathf.RoundToInt(Rate * beat * 16f)];
        var random = new System.Random(7);
        int[] bass = { 38, 38, 50, 38, 41, 38, 45, 43 };
        for (int i = 0; i < 16; i++)
        {
            Kick(buffer, i * beat, 0.55f);
            if (i % 2 == 1)
            {
                Hit(buffer, i * beat, 0.35f, 0.07f, random, 0.6f);
            }

            Hit(buffer, i * beat + beat * 0.5f, 0.08f, 0.02f, random, 0.9f);
            Note(buffer, i * beat, beat * 0.45f, Midi(bass[(i * 2) % 8]), 0.3f, 0.005f, 0.05f, 4);
            Note(buffer, i * beat + beat * 0.5f, beat * 0.45f, Midi(bass[(i * 2 + 1) % 8]), 0.25f, 0.005f, 0.05f, 4);
            if (i % 4 == 0)
            {
                foreach (int note in new[] { 62, 65, 69 })
                {
                    Note(buffer, i * beat, beat * 0.3f, Midi(note), 0.09f, 0.01f, 0.2f, 3);
                }
            }
        }

        return Finish(buffer, 0.85f);
    }

    // Urgent: a ticking clock, a heartbeat and a rising drone.
    private static float[] FinalMinute()
    {
        var buffer = new float[Rate * 8];
        var random = new System.Random(3);
        for (int i = 0; i < 32; i++)
        {
            Hit(buffer, i * 0.25f, i % 2 == 0 ? 0.25f : 0.15f, 0.012f, random, 0.95f);
        }

        for (int i = 0; i < 8; i++)
        {
            Kick(buffer, i, 0.5f);
            Kick(buffer, i + 0.22f, 0.35f);
        }

        Note(buffer, 0f, 4f, Midi(46), 0.2f, 1f, 0.5f, 4, 0.004f);
        Note(buffer, 4f, 4f, Midi(47), 0.22f, 0.5f, 0.5f, 4, 0.006f);
        return Finish(buffer, 0.85f);
    }

    // Filtered noise with slow gusts; the end is crossfaded into the start so it loops without a seam.
    private static float[] Wind(int seed, float smoothing)
    {
        int length = Rate * 16;
        int fade = Rate;
        var raw = new float[length + fade];
        var random = new System.Random(seed);
        float filtered = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)Rate;
            filtered += ((float)random.NextDouble() * 2f - 1f - filtered) * smoothing;
            float gust = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / 16f) + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 4f);
            raw[i] = filtered * gust;
        }

        var buffer = new float[length];
        for (int i = 0; i < length; i++)
        {
            buffer[i] = i < fade ? Mathf.Lerp(raw[length + i], raw[i], i / (float)fade) : raw[i];
        }

        return Finish(buffer, 0.7f);
    }

    // A falling growl: a buzzy tone sliding down with breath noise.
    private static float[] Growl(float startFrequency, int seed)
    {
        var buffer = new float[Mathf.RoundToInt(Rate * 0.9f)];
        var random = new System.Random(seed);
        float phase = 0f;
        float filtered = 0f;
        for (int i = 0; i < buffer.Length; i++)
        {
            float t = i / (float)Rate;
            phase += 2f * Mathf.PI * Mathf.Lerp(startFrequency, startFrequency * 0.3f, t / 0.9f) / Rate;
            float tone = 0f;
            for (int h = 1; h <= 6; h++)
            {
                tone += Mathf.Sin(phase * h) / h;
            }

            filtered += ((float)random.NextDouble() * 2f - 1f - filtered) * 0.3f;
            float envelope = Mathf.Min(1f, t * 40f) * Mathf.Exp(-t * 3.5f);
            buffer[i] = (tone * 0.6f + filtered * 0.5f) * envelope;
        }

        return Finish(buffer, 0.9f);
    }

    // A heavy crash: a low thump, crumbling noise and a few cracks.
    private static float[] Crash(int seed)
    {
        var buffer = new float[Mathf.RoundToInt(Rate * 1.4f)];
        var random = new System.Random(seed);
        Kick(buffer, 0f, 1f);
        Hit(buffer, 0f, 0.9f, 0.25f, random, 0.25f);
        for (int i = 0; i < 6; i++)
        {
            Hit(buffer, 0.05f + (float)random.NextDouble() * 0.6f, 0.4f, 0.03f, random, 0.7f);
        }

        // The tail must not wrap into the start in a one-shot sound, so it is faded out before the end.
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] *= Mathf.Clamp01((buffer.Length - i) / (Rate * 0.2f));
        }

        return Finish(buffer, 0.9f);
    }
}
