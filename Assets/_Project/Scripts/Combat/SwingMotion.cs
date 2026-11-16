using UnityEngine;

/// <summary>
/// How much a swing lets the player move and turn, and when, as plain functions (tested in SwingMotionTests). Each attack can
/// say for itself (AttackDefinition › Movement); what it leaves below 0 comes from <see cref="CombatTechniqueSettings"/>.
/// <para>
/// None of it moves the chain point: the next swing still waits for the attack's Recovery Time, so walking out of a swing never
/// brings the next one sooner and cadence and damage per second hold (Docs/CombatResearch.md §3.4.1-3.4.2). What it changes is
/// what the player may do meanwhile: walk at a share of their speed while it winds up (on the arms, over walking legs), turn the
/// blow toward where they aim early in the wind-up, step into the blow over its last part, and walk and turn again a moment after
/// it lands. A press is only kept for the next swing in the last moments before the chain point (the bounded buffer), so mashing
/// no longer commits the player to a whole chain.
/// </para>
/// </summary>
public static class SwingMotion
{
    /// <summary>Share of walking speed the player keeps while the swing winds up. A heavy swing, a rush (which lunges all through its
    /// wind-up) and a weapon art (leaps and spins are whole-body clips) stay planted unless the attack says otherwise.</summary>
    public static float WindupMove(AttackDefinition attack, SwingKind kind, CombatTechniqueSettings rules)
    {
        if (attack != null && attack.OwnWindupMove.HasValue)
        {
            return attack.OwnWindupMove.Value;
        }

        return kind == SwingKind.Light || kind == SwingKind.ShieldBash ? rules.WindupMoveSpeed : 0f;
    }

    /// <summary>Share of walking speed from <see cref="MoveAt"/> to the end of the swing: the arms finish it over the legs.</summary>
    public static float AfterContactMove(AttackDefinition attack, SwingKind kind, CombatTechniqueSettings rules)
    {
        if (attack != null && attack.OwnAfterContactMove.HasValue)
        {
            return attack.OwnAfterContactMove.Value;
        }

        return kind == SwingKind.Heavy ? rules.HeavyMoveSpeed : rules.SwingMoveSpeed;
    }

    /// <summary>Degrees per second the blow turns toward the aim early in its wind-up (the focused enemy is followed at Swing Tracking).</summary>
    public static float TurnRate(AttackDefinition attack, CombatTechniqueSettings rules) =>
        (attack != null ? attack.OwnWindupTurnRate : null) ?? rules.WindupTurnRate;

    /// <summary>Seconds after the swing starts (at its speed) when the player walks and turns again: a moment after the blow, never after
    /// the swing has recovered.</summary>
    public static float MoveAt(AttackDefinition attack, float hitAt, float recoverAt, float speed, CombatTechniqueSettings rules)
    {
        float after = (attack != null ? attack.OwnMoveAt : null) ?? rules.MoveAfterContact;
        return Mathf.Min(hitAt + after / Mathf.Max(0.01f, speed), recoverAt);
    }

    /// <summary>
    /// Seconds before the blow that the step into it starts: the attack's (or the settings') share of the wind-up, at least
    /// <paramref name="shortest"/>. A rush steps all through its wind-up. A heavy swing steps only after its charge, so never before
    /// the top of its wind-up (<paramref name="chargeLead"/> before the blow).
    /// </summary>
    public static float LungeLead(AttackDefinition attack, SwingKind kind, float hitAt, float shortest, float chargeLead, CombatTechniqueSettings rules)
    {
        if (hitAt <= 0f)
        {
            return 0f;
        }

        if (kind == SwingKind.Rush)
        {
            return hitAt;
        }

        float share = (attack != null ? attack.OwnLungeShare : null) ?? rules.LungeShare;
        float lead = Mathf.Clamp(hitAt * share, Mathf.Min(shortest, hitAt), hitAt);
        return kind == SwingKind.Heavy ? Mathf.Min(lead, Mathf.Max(chargeLead, Mathf.Min(shortest, hitAt))) : lead;
    }

    /// <summary>Whether the blow may still turn toward the aim this far into its wind-up: in the first Windup Turn Share of it, and
    /// never once it steps in.</summary>
    public static bool CanTurn(float elapsed, float hitAt, float lungeLead, CombatTechniqueSettings rules) =>
        elapsed < Mathf.Min(hitAt * rules.WindupTurnShare, hitAt - lungeLead);

    /// <summary>The bounded buffer: is a press this far into a swing kept for the next one? Only in the Buffer Window before the chain
    /// point (its Recovery Time). 0 keeps every press, as before.</summary>
    public static bool Buffers(float elapsed, float chainAt, CombatTechniqueSettings rules) =>
        rules.BufferWindow <= 0f || elapsed >= chainAt - rules.BufferWindow;

    /// <summary>The same for something that ends by itself (a dodge, a flinch, a broken guard): kept only in its last moments.</summary>
    public static bool BuffersWithin(float secondsLeft, CombatTechniqueSettings rules) =>
        rules.BufferWindow <= 0f || secondsLeft <= rules.BufferWindow;
}
