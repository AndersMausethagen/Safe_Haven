/// <summary>
/// When a status effect takes hold, how long it lasts and when it may take hold again, as plain functions (StatusEffects carries
/// them out; tested in StatusTimingTests). Root Eruption's three rings used to hold a player for all three: each ring restarted
/// Roots (Docs/HeartrootPlaytest_2026-11-23.md P5).
/// </summary>
public static class StatusTiming
{
    /// <summary>True once its rest after ending is over (<paramref name="restUntil"/>): it may take hold again.</summary>
    public static bool MayTakeHold(float now, float restUntil) => now >= restUntil;

    /// <summary>
    /// When an effect laid at <paramref name="now"/> ends. Already at work (<paramref name="atWork"/>), one that doesn't refresh keeps
    /// its end (<paramref name="currentEnd"/>); anything else runs its full time from now.
    /// </summary>
    public static float EndsAt(StatusEffectDefinition effect, bool atWork, float currentEnd, float now) =>
        atWork && effect != null && !effect.Refreshes ? currentEnd : now + (effect != null ? effect.Seconds : 0f);

    /// <summary>Until when an effect that ended at <paramref name="endedAt"/> can't take hold again.</summary>
    public static float RestUntil(StatusEffectDefinition effect, float endedAt) => endedAt + (effect != null ? effect.RestSeconds : 0f);
}
