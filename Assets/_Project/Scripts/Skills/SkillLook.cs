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

/// <summary>When a sound layer is heard: as the skill is cast, or as it lands.</summary>
public enum SkillSoundPhase
{
    Cast = 0,
    Release = 1,
}

/// <summary>One more sound a skill plays, on top of its cast and release sounds: a riser under the wind-up, a boom under the impact, a
/// tail of falling debris or wind after it (the user, 2026-11-27: "make sure that ultimates look, feel and sound satisfying").</summary>
[Serializable]
public sealed class SkillSoundCue
{
    public SoundDefinition sound;
    public SkillSoundPhase phase = SkillSoundPhase.Release;
    [Tooltip("Real seconds after its phase begins (slow motion doesn't stretch it).")]
    [Min(0f)] public float delay;
}

/// <summary>
/// The big moment of a skill (an ultimate's): the camera pushes in through the wind-up; at the impact the screen punches (a flash of
/// exposure, colour fringing, a lens bulge, a tint), time slows for a beat after the hit stop, and the world keeps the skill's colour a
/// while (an afterglow: frost leaves it cold and grey, fire warm). Played by SkillMoments; empty (the defaults) does nothing.
/// </summary>
[Serializable]
public sealed class SkillMomentLook
{
    [Tooltip("The camera's distance through the wind-up, times its own (0.85 pushes in 15%). 1: none.")]
    [Range(0.6f, 1f)] public float windUpZoom = 1f;
    [Tooltip("For a skill with no blow (a war cry): when its moment comes, in seconds after the cast.")]
    [Min(0f)] public float impactDelay;
    [Tooltip("Time after the hit stop: this slow, for Slow Seconds of real time (0.3 for 0.4 s). 1: none.")]
    [Range(0.1f, 1f)] public float slowScale = 1f;
    [Min(0f)] public float slowSeconds;
    [Tooltip("How long the screen punch takes to fade, in real seconds.")]
    [Min(0.05f)] public float punchSeconds = 0.6f;
    [Tooltip("Exposure added at the punch's peak (EV): a white flash.")]
    [Range(0f, 3f)] public float exposure;
    [Tooltip("Colour fringing at the peak.")]
    [Range(0f, 1f)] public float chromatic;
    [Tooltip("Lens distortion at the peak: below 0 the screen bulges out from the blow, above 0 it pinches in (a roar's pull).")]
    [Range(-0.6f, 0.6f)] public float distortion;
    [Tooltip("A colour over the screen at the peak (white: none).")]
    public Color tint = Color.white;
    [Tooltip("The world keeps this colour a while after (clear: none), at Afterglow Strength, fading over Afterglow Seconds.")]
    public Color afterglow = Color.clear;
    [Range(0f, 1f)] public float afterglowStrength = 0.5f;
    [Min(0f)] public float afterglowSeconds;
    [Tooltip("Saturation through the afterglow (-40: the cold greys the world).")]
    [Range(-80f, 40f)] public float afterglowSaturation;

    public bool HasPunch => exposure > 0f || chromatic > 0f || Mathf.Abs(distortion) > 0.001f || tint != Color.white;
    public bool IsEmpty => windUpZoom >= 0.999f && slowScale >= 0.999f && !HasPunch && afterglowSeconds <= 0f;
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
    [Tooltip("Hit stop when it lands on something, in seconds, by its size: a light skill 0.04, a heavy one 0.06, an ultimate 0.09.")]
    [Range(0f, 0.2f)] public float hitStop;
    [Tooltip("The frame pushed toward the blow as it lands, in metres (a light skill 0.08, an ultimate 0.25). 0: none.")]
    [Range(0f, 0.5f)] public float kick;
    [Tooltip("Heard as it is cast, from the caster (ice gathering, embers catching). Empty: nothing.")]
    public SoundDefinition castSound;
    [Tooltip("Heard as it lands, where it lands (the lance breaking out of the ground, the slam). Empty: nothing.")]
    public SoundDefinition releaseSound;
    [Tooltip("Seconds after the release that its sound is heard (a war cry's roar comes into its loudest a moment in).")]
    [Min(0f)] public float releaseSoundDelay;
    [Tooltip("More sound layers: a riser, a boom, a tail.")]
    public List<SkillSoundCue> sounds = new List<SkillSoundCue>();
    [Tooltip("Its big moment: a camera push, a screen punch, slow time, an afterglow (the ultimates).")]
    public SkillMomentLook moment = new SkillMomentLook();

    public bool IsEmpty => cast.Count == 0 && release.Count == 0 && struck.Count == 0 && castSound == null && releaseSound == null;

    /// <summary>How long the cast's marks on the caster's own ground last (its rune circle, the dust at its feet): the longest of their
    /// delay and seconds. The caster stands in them until they are gone (PlayerSkills); 0 when the cast draws nothing there.</summary>
    public float CasterGroundSeconds
    {
        get
        {
            float longest = 0f;
            foreach (SkillFxCue cue in cast)
            {
                if (cue != null && cue.prefab != null && cue.place == SkillFxPlace.CasterGround)
                {
                    longest = Mathf.Max(longest, cue.delay + cue.seconds);
                }
            }

            return longest;
        }
    }
}
