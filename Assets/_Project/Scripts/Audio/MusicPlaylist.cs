using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What the player is doing, as far as the music cares.</summary>
public enum AudioMood
{
    Exploring = 1,
    Combat = 2,
}

/// <summary>How a playlist with several tracks moves on when a track ends.</summary>
public enum PlaylistOrder
{
    Shuffle,
    InOrder,
}

/// <summary>
/// Where playlists were left, so a short fight doesn't send the exploring music back to its first bar every time. Forgotten
/// after a while: a long way on, starting afresh is better than picking up a thought from ten minutes ago.
/// </summary>
public sealed class MusicMemory
{
    private readonly Dictionary<MusicPlaylist, (int index, float time, float leftAt)> places = new Dictionary<MusicPlaylist, (int, float, float)>();

    public void Leave(MusicPlaylist playlist, int index, float time, float now) => places[playlist] = (index, time, now);

    /// <summary>Where to pick a playlist up, when it was left no more than <paramref name="within"/> seconds ago. A place can be taken once.</summary>
    public bool TryReturn(MusicPlaylist playlist, float now, float within, out int index, out float time)
    {
        index = -1;
        time = 0f;
        if (playlist == null || !places.TryGetValue(playlist, out (int index, float time, float leftAt) place))
        {
            return false;
        }

        places.Remove(playlist);
        if (now - place.leftAt > within)
        {
            return false;
        }

        index = place.index;
        time = place.time;
        return true;
    }

    public void Clear() => places.Clear();
}

/// <summary>One music or ambience file and how loud it plays. Long files should use the Streaming load type.</summary>
[Serializable]
public sealed class MusicTrack
{
    [Tooltip("The audio file.")]
    public AudioClip clip;
    [Tooltip("Loudness of this track before the player's volume settings.")]
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Seconds into the file where the track starts, to skip a slow intro (combat music has to be there at once).")]
    [Min(0f)] public float startAt;

    public MusicTrack()
    {
    }

    public MusicTrack(AudioClip clip, float volume)
    {
        this.clip = clip;
        this.volume = volume;
    }
}

/// <summary>
/// A list of tracks for one situation (exploring, fighting...). One track loops forever; several play one after another
/// in order or shuffled. Lives inline on the component that uses it, so music needs no separate assets.
/// </summary>
[Serializable]
public sealed class MusicPlaylist
{
    public List<MusicTrack> tracks = new List<MusicTrack>();
    [Tooltip("With several tracks: shuffle them or play them top to bottom.")]
    public PlaylistOrder order = PlaylistOrder.Shuffle;

    public bool HasTracks => PlayableCount > 0;

    public int PlayableCount
    {
        get
        {
            int count = 0;
            foreach (MusicTrack track in tracks)
            {
                if (track != null && track.clip != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>The track to play after <paramref name="current"/> (-1 to start), or -1 when there is none.</summary>
    public int NextIndex(int current)
    {
        int playable = PlayableCount;
        if (playable == 0)
        {
            return -1;
        }

        if (order == PlaylistOrder.Shuffle && playable > 1)
        {
            int pick;
            do
            {
                pick = UnityEngine.Random.Range(0, tracks.Count);
            }
            while (pick == current || tracks[pick] == null || tracks[pick].clip == null);

            return pick;
        }

        for (int step = 1; step <= tracks.Count; step++)
        {
            int index = ((current < 0 ? -1 : current) + step) % tracks.Count;
            if (tracks[index] != null && tracks[index].clip != null)
            {
                return index;
            }
        }

        return -1;
    }
}

/// <summary>The music picked for the current situation, and why, for the Sound Editor's "Playing now" panel.</summary>
public readonly struct MusicChoice
{
    public readonly MusicPlaylist Playlist;
    public readonly string Reason;
    public readonly bool IsCombat;

    public MusicChoice(MusicPlaylist playlist, string reason, bool isCombat = false)
    {
        Playlist = playlist;
        Reason = reason;
        IsCombat = isCombat;
    }
}

/// <summary>
/// The rules that pick music: the scene's final-stretch music near the end of a raid, then combat music while fighting
/// (the scene's, else the game-wide default), then exploring music (the scene's, else the default). Plain logic so it can
/// be tested without scenes.
/// </summary>
public static class MusicRules
{
    // Event music first (unless it lets fights interrupt it, as a place's mood music does); then a fight's combat music, even in a scene kept silent (the prologue's quiet forest); a
    // silence holds only while nobody fights.
    public static MusicChoice Choose(SceneAudio scene, AudioSettingsDefinition defaults, AudioMood mood, float raidTimeLeft)
    {
        bool overridden = scene != null && scene.MusicOverridden;
        bool combatAllowed = scene == null || scene.CombatMusic;
        bool fightTakesOver = mood == AudioMood.Combat && combatAllowed && scene != null && scene.FightsInterruptEventMusic;
        if (overridden && scene.EventMusic != null && !fightTakesOver)
            return new MusicChoice(scene.EventMusic, "Quest/event: music");
        if (!overridden && scene != null && scene.FinalStretch.HasTracks && raidTimeLeft <= scene.FinalStretchSeconds)
        {
            return new MusicChoice(scene.FinalStretch, "Scene: final stretch");
        }

        if (mood == AudioMood.Combat && combatAllowed)
        {
            if (scene != null && scene.Combat.HasTracks)
            {
                return new MusicChoice(scene.Combat, "Scene: combat", true);
            }

            if (defaults != null && defaults.DefaultCombat.HasTracks)
            {
                return new MusicChoice(defaults.DefaultCombat, "Default: combat", true);
            }
        }

        if (overridden && scene.EventMusic != null)
            return new MusicChoice(scene.EventMusic, "Quest/event: music");
        if (overridden)
            return new MusicChoice(null, "Quest/event: silence");

        if (scene != null && scene.Exploring.HasTracks)
        {
            return new MusicChoice(scene.Exploring, "Scene: exploring");
        }

        if (defaults != null && defaults.DefaultExploring.HasTracks)
        {
            return new MusicChoice(defaults.DefaultExploring, "Default: exploring");
        }

        return new MusicChoice(null, "No music set");
    }

    /// <summary>Whether a new fight is far enough from the previous one to restart its soundtrack.</summary>
    public static bool ShouldRestartCombat(float now, float lastCombatEndedAt) =>
        !float.IsNegativeInfinity(lastCombatEndedAt) && now - lastCombatEndedAt >= 60f;
}
