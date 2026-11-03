using System.Collections.Generic;
using UnityEngine;

/// <summary>Which volume slider a sound follows. Values are serialized as numbers, so add new channels at the end.</summary>
public enum AudioChannel
{
    Music = 0,
    Ambience = 1,
    Effects = 2,
}

/// <summary>What gives way when too many sounds play at once. Serialized as numbers, so add new ones at the end.</summary>
public enum SoundPriority
{
    /// <summary>Mood: birds, footsteps, creaks. The first to go, and never cuts anything else off.</summary>
    Background = 0,
    /// <summary>Most of the game.</summary>
    Normal = 1,
    /// <summary>What the player needs to hear: being hurt, a parry, a warning. Only ever cut off by its own kind.</summary>
    Important = 2,
}

/// <summary>
/// One sound as designers think of it: "monster dies", "wall breaks", "Safe Haven theme". It holds one or more clips
/// (one is picked at random each time, so repeats sound less mechanical), how loud it is, how much the pitch varies and
/// whether it is heard everywhere or only near where it happens. Gameplay and the soundtrack refer to these assets,
/// never to clips directly, so a sound can be replaced or tuned without touching anything else.
/// </summary>
[CreateAssetMenu(fileName = "NewSound", menuName = "Safe Haven/Audio/Sound")]
public sealed class SoundDefinition : ScriptableObject
{
    [Tooltip("Shown in the editor. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [Tooltip("Written on the screen when it plays, if the player has Sound Captions on: \"Something has seen you\". Empty for all but sounds worth knowing about.")]
    [SerializeField] private string caption;
    [Tooltip("One is picked at random each time the sound plays.")]
    [SerializeField] private List<AudioClip> clips = new List<AudioClip>();
    [Tooltip("Which volume setting controls it.")]
    [SerializeField] private AudioChannel channel = AudioChannel.Effects;

    [Header("Loudness and Variation")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [Tooltip("Each play is up to this much quieter, at random. 0.1 is up to 10% quieter.")]
    [SerializeField, Range(0f, 0.5f)] private float volumeVariation = 0.1f;
    [Tooltip("Lowest and highest pitch. 1 is the clip as recorded; a small range keeps repeats from sounding identical.")]
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    [Header("Where It Is Heard")]
    [Tooltip("On: heard only near where it happens, and from that direction. Off: heard the same everywhere (music, UI).")]
    [SerializeField] private bool positional = true;
    [Tooltip("Full volume up to this distance, in meters.")]
    [SerializeField, Min(0.1f)] private float fullVolumeDistance = 3f;
    [Tooltip("Silent beyond this distance, in meters.")]
    [SerializeField, Min(1f)] private float audibleDistance = 35f;

    [Header("Repeats")]
    [Tooltip("At most this many copies play at the same time; more are skipped. Stops ten deaths at once from deafening.")]
    [SerializeField, Range(1, 16)] private int maxAtOnce = 4;
    [Tooltip("Seconds that must pass between two plays. Two copies in the same instant only sound like one, louder and hollow.")]
    [SerializeField, Range(0f, 0.5f)] private float minimumGap = 0.03f;
    [Tooltip("What gives way when too many sounds play at once.")]
    [SerializeField] private SoundPriority priority = SoundPriority.Normal;
    [Tooltip("A big moment: while it plays, music and background sounds dip to make room (Audio Settings › Making Room). Quests, ranks, warnings, a voice. Keep it to what the player must not miss.")]
    [SerializeField] private bool takesTheMix;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public IReadOnlyList<AudioClip> Clips => clips;
    public AudioChannel Channel => channel;
    public float Volume => volume;
    public bool Positional => positional;
    public string Caption => caption;
    public float FullVolumeDistance => fullVolumeDistance;
    public float AudibleDistance => Mathf.Max(audibleDistance, fullVolumeDistance + 0.1f);
    public int MaxAtOnce => maxAtOnce;
    public float MinimumGap => minimumGap;
    public SoundPriority Priority => priority;
    public bool TakesTheMix => takesTheMix;
    public bool HasClips => clips.Exists(clip => clip != null);

    /// <summary>A random clip, or null when there are none. With a choice, never <paramref name="last"/>: a repeat is what gives a sound away.</summary>
    public AudioClip PickClip(AudioClip last = null)
    {
        int usable = 0;
        foreach (AudioClip candidate in clips)
        {
            if (candidate != null && candidate != last)
            {
                usable++;
            }
        }

        if (usable == 0)
        {
            return last != null && clips.Contains(last) ? last : null;
        }

        int pick = Random.Range(0, usable);
        foreach (AudioClip candidate in clips)
        {
            if (candidate != null && candidate != last && pick-- == 0)
            {
                return candidate;
            }
        }

        return null;
    }

    public float PickVolume() => volume * (1f - Random.Range(0f, volumeVariation));

    public float PickPitch() => Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));

    private void OnValidate()
    {
        pitchRange.x = Mathf.Clamp(pitchRange.x, 0.1f, 3f);
        pitchRange.y = Mathf.Clamp(pitchRange.y, 0.1f, 3f);
    }
}
