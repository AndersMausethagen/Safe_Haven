using System.Collections.Generic;
using UnityEngine;

/// <summary>Which volume slider a sound follows. Values are serialized as numbers, so add new channels at the end.</summary>
public enum AudioChannel
{
    Music = 0,
    Ambience = 1,
    Effects = 2,
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

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public IReadOnlyList<AudioClip> Clips => clips;
    public AudioChannel Channel => channel;
    public float Volume => volume;
    public bool Positional => positional;
    public float FullVolumeDistance => fullVolumeDistance;
    public float AudibleDistance => Mathf.Max(audibleDistance, fullVolumeDistance + 0.1f);
    public int MaxAtOnce => maxAtOnce;
    public bool HasClips => clips.Exists(clip => clip != null);

    /// <summary>A random clip, or null when there are none.</summary>
    public AudioClip PickClip()
    {
        if (!HasClips)
        {
            return null;
        }

        AudioClip clip;
        do
        {
            clip = clips[Random.Range(0, clips.Count)];
        }
        while (clip == null);

        return clip;
    }

    public float PickVolume() => volume * (1f - Random.Range(0f, volumeVariation));

    public float PickPitch() => Random.Range(Mathf.Min(pitchRange.x, pitchRange.y), Mathf.Max(pitchRange.x, pitchRange.y));

    private void OnValidate()
    {
        pitchRange.x = Mathf.Clamp(pitchRange.x, 0.1f, 3f);
        pitchRange.y = Mathf.Clamp(pitchRange.y, 0.1f, 3f);
    }
}
