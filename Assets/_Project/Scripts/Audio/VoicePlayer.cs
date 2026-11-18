using UnityEngine;

/// <summary>
/// Says recorded lines: one voice at a time, on the Voice volume, heard whether or not the game is held still. Whoever
/// shows a line (the dialogue box, the portal's menu) asks it to say it; when the <see cref="VoiceBank"/> has no
/// recording it says so, and the caller falls back on what it did before.
/// </summary>
public static class VoicePlayer
{
    private static AudioSource source;

    /// <summary>True while a recorded line is being heard.</summary>
    public static bool IsSpeaking => source != null && source.isPlaying;

    /// <summary>Seconds of the line still to be heard.</summary>
    public static float SecondsLeft => IsSpeaking && source.clip != null ? Mathf.Max(0f, source.clip.length - source.time) : 0f;

    /// <summary>Says a line, ending the one before it. False when the line has no recording (nothing is heard), and outside Play.</summary>
    public static bool Say(string speaker, string line)
    {
        Stop();
        // Its voice lives across scenes (DontDestroyOnLoad), which only Play has: outside it the host was made, the call threw and
        // the host stayed in the open scene.
        if (!Application.isPlaying)
        {
            return false;
        }

        AudioClip recording = VoiceBank.Current.Find(speaker, line);
        if (recording == null)
        {
            return false;
        }

        if (source == null)
        {
            var host = new GameObject("Voice");
            Object.DontDestroyOnLoad(host);
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            // A conversation can hold the game still, and a held game hushes the world's sounds.
            source.ignoreListenerPause = true;
            // A voice is heard as close and dry, never in the place's reverb (AudioSpace).
            source.bypassReverbZones = true;
        }

        source.clip = recording;
        source.volume = AudioManager.ChannelVolume(AudioChannel.Voice);
        source.Play();
        return true;
    }

    public static void Stop()
    {
        if (IsSpeaking)
        {
            source.Stop();
        }
    }

    /// <summary>Follows the player's Voice volume while a line is heard. Called by whoever is showing the line.</summary>
    public static void FollowVolume()
    {
        if (IsSpeaking)
        {
            source.volume = AudioManager.ChannelVolume(AudioChannel.Voice);
        }
    }

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => source = null;
}
