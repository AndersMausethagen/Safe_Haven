/// <summary>
/// The rule every way into an attack keeps (the user, 2026-11-20: "make sure all attack animations transition smoothly and fit in a
/// chain"): the blend from the pose before it (the ready pose, the chain's swing before it, the guard) moves the body no faster than
/// <see cref="MaxBlendRate"/> muscle units a second, or half the swing's own strike, whichever is faster, and is over
/// <see cref="StrikeMargin"/> before the strike starts, so the blade's path into the blow is the clip's own. Measured on the clips'
/// Humanoid muscle curves by the editor's AttackFlow, which writes each attack's Blend In; AttackFlowTests keeps it.
/// </summary>
public static class AttackFlowRules
{
    /// <summary>Muscle units a second a blend may move the body. The moveset's strikes move at 25-60.</summary>
    public const float MaxBlendRate = 20f;
    /// <summary>A blend may move as fast as this share of the swing's own strike.</summary>
    public const float StrikeShare = 0.5f;
    /// <summary>Seconds before the strike (Hit Delay less Strike Lead) a blend is over.</summary>
    public const float StrikeMargin = 0.03f;
    /// <summary>How fast a finished swing may settle back into the ready pose, muscle units a second.</summary>
    public const float SettleRate = 12f;
    /// <summary>Shortest and longest blend back to the ready pose.</summary>
    public const float ShortestSettle = 0.12f;
    public const float LongestSettle = 0.45f;

    /// <summary>Seconds a blend over <paramref name="pop"/> needs into a swing that winds up at <paramref name="windupRate"/> and
    /// strikes at <paramref name="strikeRate"/> (muscle units a second).</summary>
    public static float BlendNeeded(float pop, float windupRate, float strikeRate) =>
        pop / System.Math.Max(MaxBlendRate, System.Math.Max(windupRate, StrikeShare * strikeRate));

    /// <summary>The longest blend that is over before the swing's strike.</summary>
    public static float BlendRoom(float hitDelay, float strikeLead) => System.Math.Max(0.05f, hitDelay - strikeLead - StrikeMargin);
}
