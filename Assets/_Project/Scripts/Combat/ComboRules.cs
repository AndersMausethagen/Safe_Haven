using UnityEngine;

/// <summary>When an attack press came, measured against the swing before it.</summary>
public enum ChainPress
{
    /// <summary>Before the previous swing had even landed: mashing. Fumbles the chain.</summary>
    Early,
    /// <summary>In time to continue the chain.</summary>
    InTime,
    /// <summary>Right as the previous swing recovered: continues the chain and builds rhythm.</summary>
    Rhythm,
    /// <summary>Too long after: the chain starts over.</summary>
    Late,
}

/// <summary>
/// The combo chain as plain functions (tested in ComboRulesTests). A weapon's light attacks form a chain: pressing in
/// time after a swing moves to the next one, the last is the finisher, and pressing right as a swing recovers (the
/// rhythm) makes the next one faster and harder. Mashing, a press before the swing has landed, fumbles the chain, so
/// spamming the button only ever repeats the first swing.
/// </summary>
public static class ComboRules
{
    /// <param name="elapsed">Seconds since the previous swing started.</param>
    /// <param name="hitDelay">When that swing landed, in seconds after it started (after speed changes).</param>
    /// <param name="recovery">When that swing could chain, in seconds after it started (after speed changes).</param>
    public static ChainPress Judge(float elapsed, float hitDelay, float recovery, CombatTechniqueSettings settings)
    {
        if (elapsed < hitDelay)
        {
            return ChainPress.Early;
        }

        if (elapsed >= recovery - settings.RhythmEarly && elapsed <= recovery + settings.RhythmLate)
        {
            return ChainPress.Rhythm;
        }

        return elapsed <= recovery + settings.ChainGrace ? ChainPress.InTime : ChainPress.Late;
    }

    /// <summary>The chain step the next swing uses: the next one, or the first after a fumble, a late press or the finisher.</summary>
    public static int NextStep(int previousStep, int steps, ChainPress press, bool fumbled)
    {
        if (steps <= 1 || fumbled || press == ChainPress.Late || previousStep < 0)
        {
            return 0;
        }

        return (previousStep + 1) % steps;
    }

    /// <summary>Rhythm stacks after a press: one more on the rhythm, none otherwise.</summary>
    public static int NextRhythm(int rhythm, ChainPress press, CombatTechniqueSettings settings)
    {
        return press == ChainPress.Rhythm ? Mathf.Min(rhythm + 1, settings.MaxRhythm) : 0;
    }

    public static float DamageMultiplier(int rhythm, CombatTechniqueSettings settings) => 1f + rhythm * settings.RhythmDamagePerStack;

    public static float SpeedMultiplier(int rhythm, CombatTechniqueSettings settings) => 1f + rhythm * settings.RhythmSpeedPerStack;
}
