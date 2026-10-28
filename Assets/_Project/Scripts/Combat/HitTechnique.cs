using System;

/// <summary>How a hit was earned, for feedback and stagger: a heavy swing, the last swing of a combo, a counter after a
/// parry or perfect dodge, a swing timed on the rhythm, a shield bash. A hit can be several at once.</summary>
[Flags]
public enum HitTechnique
{
    None = 0,
    Heavy = 1,
    Finisher = 2,
    Counter = 4,
    Rhythm = 8,
    ShieldBash = 16,
    /// <summary>A tick of damage over time (burning): no hit stop, shake, sparks or flinch.</summary>
    OverTime = 32,
}
