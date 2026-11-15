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
    /// <summary>A tick of damage over time (burning, poison): no hit stop, shake, sparks, flinch, guard or dodge.</summary>
    OverTime = 32,
    /// <summary>Struck from behind while the target staggers or is in the middle of a strike.</summary>
    Backstab = 64,
    /// <summary>A weapon art, paid for with Focus.</summary>
    WeaponArt = 128,
    /// <summary>A swing of the weapon in hand (not an arrow or spell): trains the Melee skill.</summary>
    Melee = 256,
    /// <summary>The player's arrow: trains the Archery skill.</summary>
    Shot = 512,
    /// <summary>The player's spell: trains the Runecraft skill.</summary>
    Spell = 1024,
    /// <summary>Attacking out of the guard just after a block: poise-heavy, never a sure critical.</summary>
    GuardCounter = 2048,
    /// <summary>A fully charged heavy swing let go.</summary>
    Charged = 4096,
    /// <summary>A perfect release from a bow with Hunter's Mark: the creature it strikes is marked and takes more poise from every blow.</summary>
    Marking = 8192,
}
