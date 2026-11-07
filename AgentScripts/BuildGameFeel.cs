using UnityEditor;
using UnityEngine;

/// <summary>
/// The sounds of getting better (`unity command script run --file AgentScripts/BuildGameFeel.cs --entry BuildGameFeel.Apply`):
/// wraps the clips Tools/Audio/progression.py makes (Level Up, Milestone, Perk Chosen, Insight, Craft Fine, Craft
/// Masterwork, Craft Queued) in sound assets in Audio/Sounds/Library, and hands them to Resources/AudioSettings and
/// Resources/LevelingSettings (its level-up sound). Run progression.py first. Safe to run again.
/// </summary>
public static class BuildGameFeel
{
    private const string Clips = "Assets/_Project/Audio/SFX/Safe Haven Interface/";
    private const string Sounds = "Assets/_Project/Audio/Sounds/Library/";

    public static string Apply()
    {
        AssetDatabase.Refresh();
        // Volume, whether it takes the mix (ducks the music and the rest) for its moment, clips.
        SoundDefinition levelUp = Sound("Level Up", 0.75f, false, "Level Up");
        SoundDefinition milestone = Sound("Milestone", 0.9f, true, "Milestone");
        SoundDefinition perk = Sound("Perk Chosen", 0.75f, false, "Perk Chosen");
        SoundDefinition insight = Sound("Insight", 0.6f, false, "Insight");
        SoundDefinition fine = Sound("Craft Fine", 0.7f, false, "Craft Fine");
        SoundDefinition masterwork = Sound("Craft Masterwork", 0.85f, true, "Craft Masterwork");
        SoundDefinition queued = Sound("Craft Queued", 0.45f, false, "Craft Queued 1", "Craft Queued 2");

        var audio = new SerializedObject(AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>("Assets/_Project/Resources/AudioSettings.asset"));
        audio.FindProperty("milestone").objectReferenceValue = milestone;
        audio.FindProperty("perkChosen").objectReferenceValue = perk;
        audio.FindProperty("insight").objectReferenceValue = insight;
        audio.FindProperty("craftFine").objectReferenceValue = fine;
        audio.FindProperty("craftMasterwork").objectReferenceValue = masterwork;
        audio.FindProperty("craftQueued").objectReferenceValue = queued;
        audio.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(audio.targetObject);

        var leveling = new SerializedObject(AssetDatabase.LoadAssetAtPath<LevelingSettings>("Assets/_Project/Resources/LevelingSettings.asset"));
        leveling.FindProperty("levelUpSound").objectReferenceValue = levelUp;
        leveling.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(leveling.targetObject);
        AssetDatabase.SaveAssets();
        return "7 sounds: level up, milestone, perk chosen, insight, fine, masterwork, queued";
    }

    private static SoundDefinition Sound(string name, float volume, bool takesTheMix, params string[] clips)
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
        so.FindProperty("volumeVariation").floatValue = 0.05f;
        so.FindProperty("pitchRange").vector2Value = Vector2.one;
        so.FindProperty("positional").boolValue = false;
        so.FindProperty("maxAtOnce").intValue = 1;
        so.FindProperty("minimumGap").floatValue = 0.05f;
        so.FindProperty("priority").intValue = 2;
        so.FindProperty("takesTheMix").boolValue = takesTheMix;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }
}
