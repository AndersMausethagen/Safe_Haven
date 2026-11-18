using UnityEngine;

/// <summary>
/// How a scene's open air sounds (AudioSpace, Docs/LookAndFeelResearch_2026-11-18.md A2): the reverb its placed sounds take, the forest's
/// close damped reflections, the valley's long open tail, a gorge's slap echo off stone. Swimming and a roof overhead take over from it.
/// <para>Where it belongs: one per scene (raid scenes, arenas, the Haven, the prologue).</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class PlaceAcoustics : MonoBehaviour
{
    [Tooltip("Unity's reverb for the place: Forest, Mountains (an open valley), Quarry (a stone gorge), Plain (flat open land).")]
    [SerializeField] private AudioReverbPreset preset = AudioReverbPreset.Forest;
    [Tooltip("How strong, as a share of the preset's own room level: a top-down game's reverb wants to sit under the sound.")]
    [SerializeField, Range(0.05f, 1f)] private float level = 0.5f;

    /// <summary>The place in the loaded scene, or null.</summary>
    public static PlaceAcoustics Current { get; private set; }

    public AudioReverbPreset Preset => preset;
    public float Level => level;

    public void Configure(AudioReverbPreset with, float strength)
    {
        preset = with;
        level = strength;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
        if (Current == this)
        {
            Current = null;
        }
    }
}
