using System;
using UnityEngine;

/// <summary>Moments on a gathering node that can have feedback.</summary>
public enum HarvestMoment
{
    Critical,
    Normal,
    Glancing,
    Harvested,
}

/// <summary>What one moment on a node looks and sounds like. Any part may be left empty.</summary>
[Serializable]
public sealed class HarvestFeedback
{
    [Tooltip("Played at the node.")]
    [SerializeField] private SoundDefinition sound;
    [Tooltip("Particle prefab spawned where the tool hits (pooled). Give it a PooledEffect so it returns itself when done.")]
    [SerializeField] private GameObject effect;
    [Tooltip("How far the node's model rocks away from the hit, in degrees. 0 = no shake.")]
    [SerializeField, Range(0f, 20f)] private float shakeDegrees;

    public HarvestFeedback()
    {
    }

    public HarvestFeedback(float shakeDegrees)
    {
        this.shakeDegrees = shakeDegrees;
    }

    public SoundDefinition Sound => sound;
    public GameObject Effect => effect;
    public float ShakeDegrees => shakeDegrees;
}

/// <summary>
/// How gathering a family of nodes feels: the sound, particles and node shake of each swing result, and the break when
/// the node is harvested. One profile is shared by a family (Wood, Stone, Ore, Fibre) and picked on each
/// GatheringNodeDefinition; empty sounds and effects fall back to the Default profile in Gathering Minigame settings.
/// Played by ResourceNodeFeedback. Authored data only.
/// </summary>
[CreateAssetMenu(fileName = "NewHarvestFeedback", menuName = "Safe Haven/Resources/Harvest Feedback")]
public sealed class HarvestFeedbackProfile : ScriptableObject
{
    [Tooltip("A swing in the green zone.")]
    [SerializeField] private HarvestFeedback critical = new HarvestFeedback(8f);
    [Tooltip("A swing in the yellow zone.")]
    [SerializeField] private HarvestFeedback normal = new HarvestFeedback(4f);
    [Tooltip("A missed or late swing.")]
    [SerializeField] private HarvestFeedback glancing = new HarvestFeedback(1.5f);
    [Tooltip("The node is gathered (also for nodes picked by hand). Its shake is ignored; the node breaks instead.")]
    [SerializeField] private HarvestFeedback harvested = new HarvestFeedback();

    [Header("Break and regrow")]
    [Tooltip("Seconds the node shrinks and sinks before it disappears. 0 = it vanishes at once.")]
    [SerializeField, Range(0f, 2f)] private float breakSeconds = 0.35f;
    [Tooltip("Seconds of the pop when the node grows back. 0 = it just appears.")]
    [SerializeField, Range(0f, 2f)] private float regrowSeconds = 0.4f;

    [Header("Burst (nodes with Break Apart pieces)")]
    [Tooltip("How fast the pieces fly out, in metres a second. The small ones fly faster than the big ones.")]
    [SerializeField, Min(0f)] private float burstSpeed = 4.5f;
    [Tooltip("Seconds the pieces lie around before they shrink away.")]
    [SerializeField, Min(0f)] private float pieceSeconds = 4.5f;
    [Tooltip("How much the camera shakes as it goes (as Shake Camera in a story). 0: not at all.")]
    [SerializeField, Range(0f, 1f)] private float burstShake = 0.2f;
    [Tooltip("Played when a piece lands or knocks as it rolls. Optional.")]
    [SerializeField] private SoundDefinition pieceKnock;
    [Tooltip("The inside of the rock, on the faces where it broke. Empty: the model's own material.")]
    [SerializeField] private Material pieceInterior;

    public float BurstSpeed => burstSpeed;
    public float PieceSeconds => pieceSeconds;
    public float BurstShake => burstShake;
    public SoundDefinition PieceKnock => pieceKnock;
    public Material PieceInterior => pieceInterior;
    public float BreakSeconds => breakSeconds;
    public float RegrowSeconds => regrowSeconds;

    public HarvestFeedback For(HarvestMoment moment)
    {
        switch (moment)
        {
            case HarvestMoment.Critical: return critical;
            case HarvestMoment.Normal: return normal;
            case HarvestMoment.Glancing: return glancing;
            default: return harvested;
        }
    }

    public static HarvestMoment MomentOf(SwingResult result)
    {
        switch (result)
        {
            case SwingResult.Critical: return HarvestMoment.Critical;
            case SwingResult.Normal: return HarvestMoment.Normal;
            default: return HarvestMoment.Glancing;
        }
    }

    /// <summary>The sound for a moment: the node's own, else the default profile's.</summary>
    public static SoundDefinition SoundFor(HarvestFeedbackProfile own, HarvestFeedbackProfile fallback, HarvestMoment moment)
    {
        SoundDefinition sound = own != null ? own.For(moment).Sound : null;
        return sound != null || fallback == null ? sound : fallback.For(moment).Sound;
    }

    /// <summary>The effect for a moment: the node's own, else the default profile's.</summary>
    public static GameObject EffectFor(HarvestFeedbackProfile own, HarvestFeedbackProfile fallback, HarvestMoment moment)
    {
        GameObject effect = own != null ? own.For(moment).Effect : null;
        return effect != null || fallback == null ? effect : fallback.For(moment).Effect;
    }
}
