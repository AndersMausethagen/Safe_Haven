#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

/// <summary>
/// Marks the moments the player must not miss (`unity command script run -- --file AgentScripts/BuildAudioPriorities.cs --entry BuildAudioPriorities.Apply`): quests
/// taken and done, ranks and levels, a breach's warnings and ending, and the portal's voice. Each makes music and background sounds dip while it plays, and
/// isn't cut off when too many sounds play at once. Run after BuildAudio. Safe to run again.
/// </summary>
public static class BuildAudioPriorities
{
    public static string Apply()
    {
        AudioSettingsDefinition sounds = AssetDatabase.FindAssets("t:AudioSettingsDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>).First();
        var moments = new List<SoundDefinition>
        {
            sounds.QuestStarted, sounds.QuestCompleted, sounds.BreachWarning, sounds.BreachBegins, sounds.BreachWon, sounds.BreachLost,
            HavenSettings.Current.RankSound, LevelingSettings.Current.LevelUpSound,
            AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Portal Voice.asset"),
        };

        var done = new List<string>();
        foreach (SoundDefinition moment in moments.Where(one => one != null).Distinct())
        {
            var so = new SerializedObject(moment);
            so.FindProperty("takesTheMix").boolValue = true;
            so.FindProperty("priority").intValue = (int)SoundPriority.Important;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(moment);
            float longest = moment.Clips.Where(clip => clip != null).Select(clip => clip.length).DefaultIfEmpty(0f).Max();
            done.Add($"{moment.name} (up to {longest:0.0} s)");
        }

        AssetDatabase.SaveAssets();
        return string.Join(", ", done) + $"; missing {moments.Count(one => one == null)}";
    }
}
