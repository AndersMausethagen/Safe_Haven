using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws a <see cref="MusicPlaylist"/>: one row per track with listen, clip, volume and length, reordering, and a drop
/// area for audio files. Shared by the Scene Music inspector and the Sound Editor.
/// </summary>
public static class MusicPlaylistGUI
{
    private const float LongClipSeconds = 30f;
    private static GUIStyle titleStyle;
    private static GUIStyle noteStyle;

    private static GUIStyle TitleStyle => titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
    private static GUIStyle NoteStyle => noteStyle ??= new GUIStyle(EditorStyles.wordWrappedMiniLabel) { richText = true };

    /// <param name="whenEmpty">What plays instead when the list is empty, e.g. "Plays the default exploring music".</param>
    /// <returns>True when something changed.</returns>
    public static bool Draw(SerializedProperty playlist, string title, string help, string whenEmpty, bool loopPreview = true)
    {
        SerializedProperty tracks = playlist.FindPropertyRelative("tracks");
        SerializedProperty order = playlist.FindPropertyRelative("order");
        bool changed = false;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(title, TitleStyle, GUILayout.Width(150f));
            GUILayout.FlexibleSpace();
            if (tracks.arraySize > 1)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(order, GUIContent.none, GUILayout.Width(90f));
                changed |= EditorGUI.EndChangeCheck();
            }

            if (GUILayout.Button(new GUIContent("+ Track", "Add an empty row, or drop audio files on this box."), GUILayout.Width(70f)))
            {
                tracks.arraySize++;
                SerializedProperty added = tracks.GetArrayElementAtIndex(tracks.arraySize - 1);
                added.FindPropertyRelative("clip").objectReferenceValue = null;
                added.FindPropertyRelative("volume").floatValue = 0.8f;
                changed = true;
            }
        }

        if (!string.IsNullOrEmpty(help))
        {
            EditorGUILayout.LabelField(help, NoteStyle);
        }

        if (tracks.arraySize == 0)
        {
            EditorGUILayout.LabelField($"<i>Empty. {whenEmpty}</i>", NoteStyle);
        }

        int remove = -1, moveUp = -1;
        for (int i = 0; i < tracks.arraySize; i++)
        {
            SerializedProperty track = tracks.GetArrayElementAtIndex(i);
            SerializedProperty clip = track.FindPropertyRelative("clip");
            SerializedProperty volume = track.FindPropertyRelative("volume");
            var audio = clip.objectReferenceValue as AudioClip;
            using (new EditorGUILayout.HorizontalScope())
            {
                SoundPreview.Button(audio, 24f, loopPreview);
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(clip, GUIContent.none, GUILayout.MinWidth(120f));
                volume.floatValue = GUILayout.HorizontalSlider(volume.floatValue, 0f, 1f, GUILayout.Width(70f));
                EditorGUILayout.LabelField($"{volume.floatValue:0.00}", EditorStyles.miniLabel, GUILayout.Width(30f));
                changed |= EditorGUI.EndChangeCheck();
                EditorGUILayout.LabelField(audio != null ? FormatLength(audio.length) : "", EditorStyles.miniLabel, GUILayout.Width(36f));
                using (new EditorGUI.DisabledScope(i == 0))
                {
                    if (GUILayout.Button(new GUIContent("↑", "Move up"), EditorStyles.miniButtonLeft, GUILayout.Width(20f))) moveUp = i;
                }

                if (GUILayout.Button(new GUIContent("✕", "Remove"), EditorStyles.miniButtonRight, GUILayout.Width(20f))) remove = i;
            }

            if (audio != null && audio.length > LongClipSeconds && audio.loadType != AudioClipLoadType.Streaming)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Long track loaded into memory. Streaming saves memory and load time.", NoteStyle);
                    if (GUILayout.Button("Make Streaming", EditorStyles.miniButton, GUILayout.Width(100f)))
                    {
                        MakeStreaming(audio);
                    }
                }
            }
        }

        if (moveUp > 0)
        {
            tracks.MoveArrayElement(moveUp, moveUp - 1);
            changed = true;
        }

        if (remove >= 0)
        {
            tracks.DeleteArrayElementAtIndex(remove);
            changed = true;
        }

        EditorGUILayout.EndVertical();
        changed |= HandleDrop(GUILayoutUtility.GetLastRect(), tracks);
        return changed;
    }

    /// <summary>Clip names of a playlist, for summaries: "A, B (shuffle)".</summary>
    public static string Describe(MusicPlaylist playlist)
    {
        if (playlist == null || !playlist.HasTracks) return "";
        var names = new List<string>();
        foreach (MusicTrack track in playlist.tracks)
        {
            if (track?.clip != null) names.Add(track.clip.name);
        }

        return string.Join(", ", names) + (names.Count > 1 ? $"  ({playlist.order})" : "");
    }

    public static string FormatLength(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

    public static void MakeStreaming(AudioClip clip)
    {
        if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip)) is AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }

    private static bool HandleDrop(Rect area, SerializedProperty tracks)
    {
        Event current = Event.current;
        if ((current.type != EventType.DragUpdated && current.type != EventType.DragPerform) || !area.Contains(current.mousePosition))
        {
            return false;
        }

        var clips = new List<AudioClip>();
        foreach (Object dragged in DragAndDrop.objectReferences)
        {
            if (dragged is AudioClip clip) clips.Add(clip);
        }

        if (clips.Count == 0) return false;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type != EventType.DragPerform) return false;

        DragAndDrop.AcceptDrag();
        foreach (AudioClip clip in clips)
        {
            tracks.arraySize++;
            SerializedProperty added = tracks.GetArrayElementAtIndex(tracks.arraySize - 1);
            added.FindPropertyRelative("clip").objectReferenceValue = clip;
            added.FindPropertyRelative("volume").floatValue = 0.8f;
        }

        current.Use();
        return true;
    }
}

/// <summary>The Scene Music inspector: the playlists with listen buttons, what each falls back to, and the rules.</summary>
[CustomEditor(typeof(SceneAudio))]
public sealed class SceneAudioEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        if (DrawSceneMusic(serializedObject))
        {
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(4f);
        if (GUILayout.Button("Open Sound Editor"))
        {
            SoundEditorWindow.OpenMusic();
        }
    }

    /// <summary>Draws every setting of a SceneAudio. Returns true when something changed.</summary>
    public static bool DrawSceneMusic(SerializedObject data)
    {
        bool changed = false;
        changed |= MusicPlaylistGUI.Draw(data.FindProperty("exploring"), "Exploring",
            "Plays while the player looks around, gathers and builds.", "Plays the game-wide default exploring music.");
        changed |= MusicPlaylistGUI.Draw(data.FindProperty("combat"), "Combat",
            "Plays while an enemy chases the player, the weapon is drawn or blows were just traded.",
            "Plays the game-wide default combat music.");
        changed |= MusicPlaylistGUI.Draw(data.FindProperty("finalStretch"), "Raid Final Stretch",
            "Raids only. Replaces all music when the raid timer gets low.", "The normal music keeps playing.");
        changed |= MusicPlaylistGUI.Draw(data.FindProperty("ambience"), "Ambience",
            "Background sound under the music: wind, birds, fire.", "No ambience.");

        EditorGUILayout.Space(2f);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(data.FindProperty("combatMusic"));
        EditorGUILayout.PropertyField(data.FindProperty("finalStretchSeconds"));
        changed |= EditorGUI.EndChangeCheck();
        return changed;
    }
}
