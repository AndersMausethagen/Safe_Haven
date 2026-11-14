using UnityEngine;

/// <summary>
/// What attackers on the Haven go for (ward breaches, corruption nights): a fight with someone close is left alone, and so
/// is a building still standing; otherwise the nearest standing building, or the fallback (the breach's ward). The big
/// trolls (MonsterDefinition › Breaks Walls) go for walls first and leave people be until one hurts them.
/// </summary>
public static class HomeAssault
{
    // A wall-breaker takes a wall this many times as far away over the nearest house: walls first, as they stand in the way.
    private const float WallPreference = 2f;

    /// <summary>Keeps <paramref name="ai"/> on its fight or sends it at the next thing to break.</summary>
    public static void Direct(MonsterAI ai, float chaseLimit, Damageable fallback = null)
    {
        if (ai == null || ai.CurrentState == MonsterAI.State.Dead || StaysOnFight(ai, chaseLimit))
        {
            return;
        }

        Damageable objective = Objective(ai, ai.transform.position) ?? fallback;
        if (objective != null)
        {
            ai.Engage(objective);
        }
    }

    /// <summary>
    /// True while <paramref name="ai"/>'s fight is worth keeping: a building still standing, or someone within
    /// <paramref name="chaseLimit"/>. A wall-breaker (MonsterDefinition › Breaks Walls) keeps a person only while they
    /// have hurt it lately; otherwise it goes back to the walls.
    /// </summary>
    public static bool StaysOnFight(MonsterAI ai, float chaseLimit)
    {
        if (!(ai.Target is Damageable current) || current == null || !current.IsAlive)
        {
            return false;
        }

        if (current.Category != TargetCategory.Creature)
        {
            return !current.IsBroken;
        }

        if (BreaksWalls(ai) && !ai.IsProvoked)
        {
            return false;
        }

        return (current.transform.position - ai.transform.position).sqrMagnitude <= chaseLimit * chaseLimit;
    }

    /// <summary>What it goes for next: for a wall-breaker the nearest wall (or a house when none is near), otherwise the
    /// nearest building.</summary>
    public static Damageable Objective(MonsterAI ai, Vector3 from) =>
        BreaksWalls(ai) ? NearestBuilding(from, WallPreference) : NearestBuilding(from);

    public static bool BreaksWalls(MonsterAI ai) => ai != null && ai.Definition != null && ai.Definition.BreaksWalls;

    /// <summary>The nearest building still standing, or null. With <paramref name="wallPreference"/> above 1, a wall (a
    /// building placed in lines) counts as that many times nearer than it is.</summary>
    public static Damageable NearestBuilding(Vector3 from, float wallPreference = 1f)
    {
        Damageable nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building != null && building.TryGetComponent(out Damageable health) && health.IsAlive && !health.IsBroken)
            {
                float distance = (building.transform.position - from).sqrMagnitude;
                if (wallPreference > 1f && building.Definition != null && building.Definition.PlaceInLines)
                {
                    distance /= wallPreference * wallPreference;
                }

                if (distance < nearestDistance)
                {
                    nearest = health;
                    nearestDistance = distance;
                }
            }
        }

        return nearest;
    }
}
