using System.Collections.Generic;
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
    private const float MostApart = 80f;
    private const float LargestTurn = 60f;

    public static AttackTurn Decide(int closingIn, int most, float now, float nextTurnAt)
    {
        if (closingIn >= Mathf.Max(1, most))
        {
            return AttackTurn.Circle;
        }

        return now < nextTurnAt ? AttackTurn.StandReady : AttackTurn.Strike;
    }

    /// <summary>Where to run while closing in with others, so a group comes at <paramref name="target"/> from around it instead of in a line:
    /// its own way in, pushed off the ways the <paramref name="others"/> are coming, by up to <paramref name="spread"/> (0 to 1) of the widest flank.
    /// Each decides for itself, and they settle apart.</summary>
    public static Vector3 FlankSpot(Vector3 self, Vector3 target, IReadOnlyList<Vector3> others, float spread, float standOff)
    {
        Vector3 from = self - target;
        from.y = 0f;
        float distance = from.magnitude;
        if (spread <= 0f || others.Count == 0 || distance < 0.01f)
        {
            return target;
        }

        float apart = MostApart * spread;
        float bearing = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
        float push = 0f;
        foreach (Vector3 other in others)
        {
            Vector3 theirs = other - target;
            float delta = Mathf.DeltaAngle(bearing, Mathf.Atan2(theirs.x, theirs.z) * Mathf.Rad2Deg);
            if (Mathf.Abs(delta) < apart)
            {
                // Two on the very same line part by which is which, so they don't both go the same way.
                float away = delta != 0f ? -Mathf.Sign(delta) : other.x > self.x ? -1f : 1f;
                push += away * (apart - Mathf.Abs(delta));
            }
        }

        float heading = (bearing + Mathf.Clamp(push, -LargestTurn, LargestTurn)) * Mathf.Deg2Rad;
        return target + new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)) * Mathf.Max(standOff, distance * 0.75f);
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
