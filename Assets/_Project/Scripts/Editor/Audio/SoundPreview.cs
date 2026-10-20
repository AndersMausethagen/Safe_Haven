using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Plays audio in the editor without entering Play mode, through the same preview player the Inspector uses, so it is
/// heard even when no scene is playing. Volume and pitch are not applied by that player; the game applies them.
/// </summary>
public static class SoundPreview
{
    private static readonly Type AudioUtil = typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil");
    private static readonly MethodInfo PlayClip = AudioUtil?.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public,
        null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
    private static readonly MethodInfo StopAll = AudioUtil?.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public);
    private static readonly MethodInfo IsClipPlaying = AudioUtil?.GetMethod("IsPreviewClipPlaying", BindingFlags.Static | BindingFlags.Public);
    private static readonly MethodInfo ClipPosition = AudioUtil?.GetMethod("GetPreviewClipPosition", BindingFlags.Static | BindingFlags.Public);

    // Fallback when Unity's internal preview API changes: a hidden audio source.
    private static AudioSource fallback;

    /// <summary>The clip last started, while it is still playing.</summary>
    public static AudioClip Current => IsPlaying ? current : null;
    private static AudioClip current;

    public static bool IsPlaying
    {
        get
        {
            if (current == null) return false;
            if (IsClipPlaying != null) return (bool)IsClipPlaying.Invoke(null, null);
            return fallback != null && fallback.isPlaying;
        }
    }

    /// <summary>Seconds into the clip that is playing.</summary>
    public static float Position => IsPlaying && ClipPosition != null ? (float)ClipPosition.Invoke(null, null) : fallback != null ? fallback.time : 0f;

    public static void Play(AudioClip clip, bool loop = false)
    {
        Stop();
        if (clip == null) return;
        current = clip;
        if (PlayClip != null)
        {
            PlayClip.Invoke(null, new object[] { clip, 0, loop });
            return;
        }

        if (fallback == null)
        {
            var host = EditorUtility.CreateGameObjectWithHideFlags("Sound Preview", HideFlags.HideAndDontSave, typeof(AudioSource));
            fallback = host.GetComponent<AudioSource>();
            fallback.playOnAwake = false;
            fallback.spatialBlend = 0f;
        }

        fallback.clip = clip;
        fallback.loop = loop;
        fallback.Play();
    }

    /// <summary>Plays one of the sound's clips, picked at random like the game does.</summary>
    public static void Play(SoundDefinition sound)
    {
        if (sound != null) Play(sound.PickClip(), sound.Channel != AudioChannel.Effects);
    }

    /// <summary>Starts the clip, or stops it when it is already the one playing.</summary>
    public static void Toggle(AudioClip clip, bool loop = false)
    {
        if (clip != null && Current == clip) Stop();
        else Play(clip, loop);
    }

    public static void Stop()
    {
        current = null;
        StopAll?.Invoke(null, null);
        if (fallback != null) fallback.Stop();
    }

    /// <summary>A ▶ / ■ button for a clip.</summary>
    public static void Button(AudioClip clip, float width = 26f, bool loop = false)
    {
        using (new EditorGUI.DisabledScope(clip == null))
        {
            bool playing = clip != null && Current == clip;
            if (GUILayout.Button(new GUIContent(playing ? "■" : "▶", playing ? "Stop" : "Listen"), GUILayout.Width(width)))
            {
                Toggle(clip, loop);
            }
        }
    }
}
