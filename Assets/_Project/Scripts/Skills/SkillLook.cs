using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a skill effect is put (SkillFxCue): on the caster, on the ground under them, or spread over the skill's own reach.</summary>
public enum SkillFxPlace
{
    /// <summary>At the caster's chest, turned the way they aim.</summary>
    Caster = 0,
    /// <summary>On the ground under the caster.</summary>
    CasterGround = 1,
    /// <summary>Count of them on the ground along the aim, from near the caster out to the attack's reach: a lance's path.</summary>
    AlongReach = 2,
    /// <summary>Count of them on the ground spread over the attack's arc at its reach: a cone's front.</summary>
    AcrossArc = 3,
    /// <summary>Count of them on the ground round a circle of the attack's reach: a shockwave's edge.</summary>
    Ring = 4,
    /// <summary>One on each creature the skill struck.</summary>
    Struck = 5,
    /// <summary>Count of them on the ground along the path a dash took, from where it began to where it ended.</summary>
    Trail = 6,
}

/// <summary>One effect a skill plays: what, where, how many, when, and for how long.</summary>
[Serializable]
public sealed class SkillFxCue
{
    [Tooltip("The effect: a prefab with a VisualEffect (and VfxValues), from Art/Effects/Skills/Prefabs.")]
    public GameObject prefab;
    public SkillFxPlace place;
    [Tooltip("How many, for the shapes that spread them (Along Reach, Across Arc, Ring).")]
    [Min(1)] public int count = 1;
    [Tooltip("Seconds after its phase begins (the cast, the release, the hit).")]
    [Min(0f)] public float delay;
    [Tooltip("Seconds between one and the next along the shape: an eruption racing out ahead.")]
    [Min(0f)] public float stepDelay;
    [Tooltip("Seconds it stays before it goes back to the pool.")]
    [Min(0.1f)] public float seconds = 2f;
    [Tooltip("Its size, times the prefab's own.")]
    [Min(0.05f)] public float scale = 1f;
}

/// <summary>
/// How a skill looks (Docs/CombatSkillsResearch_2026-11-24.md §6), in three phases: as it is cast (under the caster: the school's rune
/// circle, a glow, a mist), as it is let go (spread over the attack's own reach and arc, so the effect shows the area it strikes), and on
/// each creature struck. While it winds up, its area shows on the ground in the school's colour, as a monster's strike does in red.
/// A light flashes and the camera shakes at the release. Data only; SkillFx plays it.
/// </summary>
[Serializable]
public sealed class SkillLook
{
    [Tooltip("Show the skill's area on the ground while it winds up, in its school's colour.")]
    public bool showArea = true;
    public List<SkillFxCue> cast = new List<SkillFxCue>();
    public List<SkillFxCue> release = new List<SkillFxCue>();
    public List<SkillFxCue> struck = new List<SkillFxCue>();
    [Tooltip("A light at the release, in this colour (HDR intensity in its alpha's place: Intensity below). Clear: none.")]
    public Color light = Color.clear;
    [Min(0f)] public float lightIntensity = 6f;
    [Min(0f)] public float lightRange = 8f;
    [Min(0f)] public float lightSeconds = 0.35f;
    [Tooltip("Camera shake at the release (0 none, 1 a heavy blow's).")]
    [Range(0f, 2f)] public float shake;
    [Tooltip("Heard as it is cast, from the caster (ice gathering, embers catching). Empty: nothing.")]
    public SoundDefinition castSound;
    [Tooltip("Heard as it lands, where it lands (the lance breaking out of the ground, the slam). Empty: nothing.")]
    public SoundDefinition releaseSound;
    [Tooltip("Seconds after the release that its sound is heard (a war cry's roar comes into its loudest a moment in).")]
    [Min(0f)] public float releaseSoundDelay;

    public bool IsEmpty => cast.Count == 0 && release.Count == 0 && struck.Count == 0 && castSound == null && releaseSound == null;
}
