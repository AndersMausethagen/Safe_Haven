#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gives the portal its voice (`unity command script run -- --file AgentScripts/BuildPortalVoice.cs --entry BuildPortalVoice.Apply`), with SafeHaven
/// open: a sound from the takes of Tools/Audio/portal.py, on the portal for its whispers and on every line its story graphs give it. Run it again
/// after BuildHomeStory, which rebuilds those graphs. Safe to run again.
/// </summary>
public static class BuildPortalVoice
{
    private const string ClipFolder = "Assets/_Project/Audio/SFX/Safe Haven Portal";
    private const string SoundPath = "Assets/_Project/Audio/Sounds/Library/Portal Voice.asset";

    public static string Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/SafeHaven.unity" || scene.isDirty)
        {
            return $"Open SafeHaven with no unsaved changes first ({scene.path}, dirty={scene.isDirty}). Nothing done.";
        }

        AudioClip[] takes = AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
        if (takes.Length == 0)
        {
            return "No takes: run Tools/Audio/portal.py first.";
        }

        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(SoundPath);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, SoundPath);
        }

        var so = new SerializedObject(sound);
        so.FindProperty("displayName").stringValue = "Portal Voice";
        SerializedProperty clips = so.FindProperty("clips");
        clips.arraySize = takes.Length;
        for (int i = 0; i < takes.Length; i++)
        {
            clips.GetArrayElementAtIndex(i).objectReferenceValue = takes[i];
        }

        so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
        so.FindProperty("volume").floatValue = 0.85f;
        so.FindProperty("volumeVariation").floatValue = 0.05f;
        so.FindProperty("pitchRange").vector2Value = new Vector2(0.96f, 1.03f);
        // It comes from the arch, but it speaks wherever the player stands in the ward: a place to hear it from, not a limit.
        so.FindProperty("positional").boolValue = true;
        so.FindProperty("fullVolumeDistance").floatValue = 35f;
        so.FindProperty("audibleDistance").floatValue = 120f;
        // A second take never starts over the first: a skipped line just lets the murmur run on.
        so.FindProperty("maxAtOnce").intValue = 1;
        so.FindProperty("minimumGap").floatValue = 0.2f;
        so.FindProperty("priority").intValue = (int)SoundPriority.Important;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        AssetDatabase.SaveAssets();

        HomePortal portal = Object.FindAnyObjectByType<HomePortal>(FindObjectsInactive.Include);
        var home = new SerializedObject(portal);
        home.FindProperty("voice").objectReferenceValue = sound;
        home.ApplyModifiedPropertiesWithoutUndo();

        int lines = 0;
        foreach (StoryScript story in Object.FindObjectsByType<StoryScript>(FindObjectsInactive.Include))
        {
            var graph = new SerializedObject(story);
            SerializedProperty it = graph.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.name == "speaker" && it.FindPropertyRelative("name")?.stringValue == "The Portal")
                {
                    it.FindPropertyRelative("voice").objectReferenceValue = sound;
                    it.FindPropertyRelative("voiceFrom").objectReferenceValue = portal.transform;
                    lines++;
                }
            }

            graph.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{takes.Length} takes, {lines} story lines voiced, dirty={scene.isDirty}";
    }
}
