using UnityEngine;

/// <summary>What a monster ready to strike the player does about the others also closing in.</summary>
public enum AttackTurn
{
    Strike,
    /// <summary>Another's blow has only just started: it goes in a moment.</summary>
    StandReady,
    /// <summary>Enough are closing in already: it circles until its next try.</summary>
    Circle,
}

/// <summary>
/// The rules of taking turns at the player, kept pure so they can be tested. MonsterAI counts who is closing in and carries the answer out;
/// the numbers are in CombatTechniqueSettings.
/// </summary>
public static class AttackTurns
{
    private const float StepAround = 2f;

    public static AttackTurn Decide(int closingIn, int most, float now, float nextTurnAt)
    {
        if (closingIn >= Mathf.Max(1, most))
        {
            return AttackTurn.Circle;
        }

        return now < nextTurnAt ? AttackTurn.StandReady : AttackTurn.Strike;
    }

    /// <summary>Where to walk next while circling: a step around the target on <paramref name="side"/> (1 or -1), corrected toward the
    /// distance it waits at.</summary>
    public static Vector3 CircleSpot(Vector3 position, Vector3 toTarget, float side, float distance)
    {
        toTarget.y = 0f;
        float now = toTarget.magnitude;
        Vector3 away = now > 0.01f ? -toTarget / now : Vector3.back;
        return position + Vector3.Cross(Vector3.up, away) * (side * StepAround) + away * (distance - now);
    }
}
