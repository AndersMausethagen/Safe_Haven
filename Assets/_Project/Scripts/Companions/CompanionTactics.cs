using System.Collections.Generic;
using UnityEngine;

/// <summary>An enemy near a companion, as the tactics see it.</summary>
public readonly struct CompanionThreat
{
    public readonly Vector3 Position;
    /// <summary>It is chasing or attacking the companion's leader.</summary>
    public readonly bool HuntsLeader;
    /// <summary>It is chasing or attacking the companion itself.</summary>
    public readonly bool HuntsSelf;
    /// <summary>How many other companions are already on it.</summary>
    public readonly int TakenBy;

    public CompanionThreat(Vector3 position, bool huntsLeader, bool huntsSelf, int takenBy = 0)
    {
        Position = position;
        HuntsLeader = huntsLeader;
        HuntsSelf = huntsSelf;
        TakenBy = takenBy;
    }
}

/// <summary>How far a companion's orders reach, in metres (from its CompanionDefinition).</summary>
public readonly struct CompanionRanges
{
    /// <summary>Following: fights enemies this close to the leader, and is called back beyond twice this.</summary>
    public readonly float Engage;
    /// <summary>Defending: fights only what hunts the leader or itself, or comes this close to the leader.</summary>
    public readonly float Defend;
    /// <summary>Staying: fights what comes this close to its spot.</summary>
    public readonly float Hold;
    /// <summary>Retreating: keeps at least this far from enemies.</summary>
    public readonly float Retreat;
    /// <summary>It walks back to its place once this far from it.</summary>
    public readonly float Slack;

    public CompanionRanges(float engage, float defend, float hold, float retreat, float slack)
    {
        Engage = engage;
        Defend = defend;
        Hold = hold;
        Retreat = retreat;
        Slack = slack;
    }
}

/// <summary>Everything the tactics need to choose a companion's next move.</summary>
public struct CompanionSituation
{
    public CompanionOrder Order;
    public Vector3 Self;
    public Vector3 Leader;
    /// <summary>Where it walks while following: the leader plus its place in the formation.</summary>
    public Vector3 FollowPoint;
    /// <summary>The spot it was told to stay at.</summary>
    public Vector3 HoldPoint;
    /// <summary>Index in <see cref="Threats"/> of what the leader last hit, or -1.</summary>
    public int LeaderTarget;
    public IReadOnlyList<CompanionThreat> Threats;
}

public enum CompanionAction
{
    /// <summary>Stand where it is.</summary>
    Idle,
    /// <summary>Walk to <see cref="CompanionIntent.Point"/>.</summary>
    Move,
    /// <summary>Close in on and attack threat <see cref="CompanionIntent.Target"/>.</summary>
    Attack,
}

public readonly struct CompanionIntent
{
    public readonly CompanionAction Action;
    public readonly Vector3 Point;
    public readonly int Target;

    private CompanionIntent(CompanionAction action, Vector3 point, int target)
    {
        Action = action;
        Point = point;
        Target = target;
    }

    public static CompanionIntent Idle => new CompanionIntent(CompanionAction.Idle, default, -1);
    public static CompanionIntent MoveTo(Vector3 point) => new CompanionIntent(CompanionAction.Move, point, -1);
    public static CompanionIntent Attack(int target) => new CompanionIntent(CompanionAction.Attack, default, target);
}

/// <summary>
/// The rules behind a companion's orders: from where it, its leader and the enemies are, what it does next (stand,
/// walk somewhere, or attack one enemy). Plain C# so every order can be tested without a scene (CompanionTacticsTests);
/// the Companion component carries the intent out (pathing, swinging, shooting). A new order is a new case here.
/// Among enemies it may fight it picks those hunting itself first, then those hunting the leader, then the nearest to
/// the leader, and leaves one someone else already has if there is another about.
/// </summary>
public static class CompanionTactics
{
    // An enemy someone else is on counts as this many metres further off.
    private const float TakenPenalty = 7f;
    private const float StepAside = 2.8f;
    private const float StepRound = 3f;

    public enum CatchUp
    {
        Keeping,
        Hurrying,
        Warp,
    }

    /// <summary>Its place while following: <paramref name="offset"/> (x to their right, z ahead) turned to the way the leader is heading, so
    /// the one meant to walk behind them walks behind them whichever way they go.</summary>
    public static Vector3 FormationPoint(Vector3 leader, Vector3 heading, Vector3 offset)
    {
        heading.y = 0f;
        return heading.sqrMagnitude > 0.0001f ? leader + Quaternion.LookRotation(heading) * offset : leader + offset;
    }

    /// <summary>How far behind it has fallen calls for what: to hurry, or (out of sight, or stuck) to come up from behind by other ways.</summary>
    public static CatchUp HowToCatchUp(float distance, float hurryFrom, float warpFrom, float stuckSeconds, float stuckLimit)
    {
        if (distance >= warpFrom || (stuckSeconds >= stuckLimit && distance >= hurryFrom * 0.5f))
        {
            return CatchUp.Warp;
        }

        return distance >= hurryFrom ? CatchUp.Hurrying : CatchUp.Keeping;
    }

    /// <summary>True when a blow from <paramref name="from"/>, aimed along <paramref name="aim"/>, would reach <paramref name="self"/>.</summary>
    public static bool InTheWay(Vector3 self, Vector3 from, Vector3 aim, float reach, float arc)
    {
        Vector3 to = self - from;
        to.y = 0f;
        aim.y = 0f;
        return to.magnitude <= reach && (to.sqrMagnitude < 0.01f || Vector3.Angle(aim, to) <= arc * 0.5f);
    }

    /// <summary>Where to step to get out of that blow: sideways, the short way out of its line.</summary>
    public static Vector3 StepOutOfTheWay(Vector3 self, Vector3 from, Vector3 aim)
    {
        aim.y = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward;
        Vector3 across = Vector3.Cross(Vector3.up, aim);
        Vector3 to = self - from;
        float side = Vector3.Dot(to, across) >= 0f ? 1f : -1f;
        return self + across * (side * StepAside);
    }

    /// <summary>Where an archer goes to shoot <paramref name="enemy"/>: back if it is too close, round to where the line is clear, in if it is out
    /// of range. Null: it stands and shoots.</summary>
    public static Vector3? ArcherStep(Vector3 self, Vector3 enemy, float keepAway, float reach, bool clearShot, float side)
    {
        Vector3 to = enemy - self;
        to.y = 0f;
        float distance = to.magnitude;
        Vector3 toward = distance > 0.01f ? to / distance : Vector3.forward;
        if (distance < keepAway)
        {
            return self - toward * (keepAway - distance + 1.5f);
        }

        if (!clearShot)
        {
            return self + Vector3.Cross(Vector3.up, toward) * (side * StepRound) + toward * 0.5f;
        }

        return distance > reach ? enemy : (Vector3?)null;
    }

    public static CompanionIntent Decide(in CompanionSituation situation, in CompanionRanges ranges)
    {
        switch (situation.Order)
        {
            case CompanionOrder.Stay:
                return Stay(situation, ranges);
            case CompanionOrder.Defend:
                return Guard(situation, ranges, ranges.Defend, defendOnly: true);
            case CompanionOrder.Retreat:
                return Retreat(situation, ranges);
            case CompanionOrder.AttackTarget:
                return situation.LeaderTarget >= 0 && situation.LeaderTarget < Count(situation)
                    ? CompanionIntent.Attack(situation.LeaderTarget)
                    : Guard(situation, ranges, ranges.Engage, defendOnly: false);
            default:
                return Guard(situation, ranges, ranges.Engage, defendOnly: false);
        }
    }

    // Following and defending: fight near the leader, but never get led away from them.
    private static CompanionIntent Guard(in CompanionSituation situation, in CompanionRanges ranges, float radius, bool defendOnly)
    {
        bool tooFar = Flat(situation.Self - situation.Leader) > radius * 2f + ranges.Slack;
        if (!tooFar)
        {
            int target = Pick(situation, situation.Leader, radius, defendOnly);
            if (target >= 0)
            {
                return CompanionIntent.Attack(target);
            }
        }

        return GoTo(situation.Self, situation.FollowPoint, ranges.Slack);
    }

    private static CompanionIntent Stay(in CompanionSituation situation, in CompanionRanges ranges)
    {
        int target = Pick(situation, situation.HoldPoint, ranges.Hold, defendOnly: false);
        return target >= 0 ? CompanionIntent.Attack(target) : GoTo(situation.Self, situation.HoldPoint, ranges.Slack);
    }

    // Away from the enemies' middle, on the far side of the leader; with no enemy near, back in formation.
    private static CompanionIntent Retreat(in CompanionSituation situation, in CompanionRanges ranges)
    {
        Vector3 middle = Vector3.zero;
        int near = 0;
        for (int i = 0; i < Count(situation); i++)
        {
            Vector3 position = situation.Threats[i].Position;
            if (Flat(position - situation.Self) <= ranges.Retreat || Flat(position - situation.Leader) <= ranges.Retreat)
            {
                middle += position;
                near++;
            }
        }

        if (near == 0)
        {
            return GoTo(situation.Self, situation.FollowPoint, ranges.Slack);
        }

        middle /= near;
        Vector3 away = situation.Leader - middle;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = situation.Self - middle;
            away.y = 0f;
        }

        Vector3 safe = situation.Leader + (away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.back) * ranges.Retreat;
        return GoTo(situation.Self, safe, ranges.Slack);
    }

    // The enemy to fight within radius of centre, or -1. Defending only counts enemies hunting the leader or itself.
    private static int Pick(in CompanionSituation situation, Vector3 centre, float radius, bool defendOnly)
    {
        int best = -1;
        int bestRank = int.MaxValue;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < Count(situation); i++)
        {
            CompanionThreat threat = situation.Threats[i];
            float distance = Flat(threat.Position - centre);
            bool hunting = threat.HuntsSelf || threat.HuntsLeader;
            if (defendOnly ? !hunting && distance > radius : distance > radius && !threat.HuntsSelf)
            {
                continue;
            }

            int rank = threat.HuntsSelf ? 0 : threat.HuntsLeader ? 1 : 2;
            float score = distance + threat.TakenBy * TakenPenalty;
            if (rank < bestRank || (rank == bestRank && score < bestDistance))
            {
                best = i;
                bestRank = rank;
                bestDistance = score;
            }
        }

        return best;
    }

    private static CompanionIntent GoTo(Vector3 self, Vector3 point, float slack) =>
        Flat(point - self) > slack ? CompanionIntent.MoveTo(point) : CompanionIntent.Idle;

    private static int Count(in CompanionSituation situation) => situation.Threats != null ? situation.Threats.Count : 0;

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }
}
