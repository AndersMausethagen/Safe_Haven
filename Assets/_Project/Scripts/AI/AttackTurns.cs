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

    /// <summary>
    /// Who of those ready goes first (Docs/EnemyAIReview_2026-11-19.md §2): the turn went to whichever creature's Update ran first, so
    /// the same one tended to win and the one that had just struck went again. Now: the seconds it has waited, plus Front Bonus inside
    /// the target's guard arc (blows come from where a guard works), plus On Screen Bonus in view, less Last Striker Penalty for the one
    /// that struck last. The highest goes.
    /// </summary>
    public static float Rank(float waited, bool inFront, bool onScreen, bool struckLast, float frontBonus, float onScreenBonus, float lastPenalty) =>
        Mathf.Max(0f, waited) + (inFront ? frontBonus : 0f) + (onScreen ? onScreenBonus : 0f) - (struckLast ? lastPenalty : 0f);

    /// <summary>True when <paramref name="self"/> stands inside the guard arc of a target facing <paramref name="facing"/> (half-arc in
    /// degrees).</summary>
    public static bool InFront(Vector3 self, Vector3 target, Vector3 facing, float halfArc)
    {
        Vector3 to = self - target;
        to.y = 0f;
        facing.y = 0f;
        return to.sqrMagnitude > 0.0001f && facing.sqrMagnitude > 0.0001f && Vector3.Angle(facing, to) <= halfArc;
    }

    /// <summary>
    /// Where a creature runs after its strike (§1, bite and break away): <paramref name="metres"/> from the target, swung 50-70° round from
    /// where it struck, on the side away from the nearest other attacker (<paramref name="other"/>, or <paramref name="side"/> when alone).
    /// Hit and run instead of standing at the player's feet and striking on cooldown.
    /// </summary>
    public static Vector3 BreakAwaySpot(Vector3 self, Vector3 target, Vector3? other, float metres, float side, float swing = 60f)
    {
        Vector3 from = self - target;
        from.y = 0f;
        if (from.sqrMagnitude < 0.0001f)
        {
            from = Vector3.back;
        }

        float bearing = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
        if (other.HasValue)
        {
            Vector3 theirs = other.Value - target;
            float delta = Mathf.DeltaAngle(bearing, Mathf.Atan2(theirs.x, theirs.z) * Mathf.Rad2Deg);
            side = delta > 0f ? -1f : 1f;
        }

        float heading = (bearing + Mathf.Sign(side == 0f ? 1f : side) * Mathf.Clamp(swing, 50f, 70f)) * Mathf.Deg2Rad;
        return target + new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading)) * Mathf.Max(0.5f, metres);
    }

    /// <summary>
    /// Where a creature that lost its target looks (Docs/EnemyAIReview_2026-11-19.md §5): first ahead of where it last saw it, along the
    /// way it was going (<paramref name="velocity"/> × 1.5 s, at most 8 m further); then <paramref name="more"/> points 5-9 m further on,
    /// within 50° either side of that heading, turned by <paramref name="share"/> (its place among the pack searching the same ground:
    /// 0, then one side, then the other), so a pack spreads instead of stacking. Not where the target is: where it was going.
    /// </summary>
    public static List<Vector3> SearchPoints(Vector3 lastKnown, Vector3 velocity, int more, int share, System.Random random)
    {
        var points = new List<Vector3>();
        velocity.y = 0f;
        Vector3 ahead = Vector3.ClampMagnitude(velocity * 1.5f, 8f);
        Vector3 first = lastKnown + ahead;
        points.Add(first);
        if (velocity.sqrMagnitude < 0.25f)
        {
            return points;
        }

        float heading = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg + (share == 0 ? 0f : (share % 2 == 1 ? 35f : -35f));
        Vector3 from = first;
        for (int i = 0; i < more; i++)
        {
            float turn = heading + ((float)random.NextDouble() * 100f - 50f);
            float step = 5f + (float)random.NextDouble() * 4f;
            from += new Vector3(Mathf.Sin(turn * Mathf.Deg2Rad), 0f, Mathf.Cos(turn * Mathf.Deg2Rad)) * step;
            points.Add(from);
        }

        return points;
    }

    /// <summary>
    /// How good a spot is for a shooter to move to between shots (§6): higher ground pays (a point a metre, at most 2), standing within
    /// 3 m of another shooter costs 2. Spots with no clear shot, in water or past its leash are left out before scoring.
    /// </summary>
    public static float ScoreSpot(float heightGain, bool nearAnotherShooter) => Mathf.Clamp(heightGain, 0f, 2f) - (nearAnotherShooter ? 2f : 0f);

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
