using UnityEngine;

/// <summary>
/// What attackers on the Haven go for (ward breaches, corruption nights): a fight with someone close is left alone, and so
/// is a building still standing; otherwise the nearest standing building, or the fallback (the breach's ward).
/// </summary>
public static class HomeAssault
{
    /// <summary>Keeps <paramref name="ai"/> on its fight or sends it at the next thing to break.</summary>
    public static void Direct(MonsterAI ai, float chaseLimit, Damageable fallback = null)
    {
        if (ai == null || ai.CurrentState == MonsterAI.State.Dead)
        {
            return;
        }

        if (ai.Target is Damageable current && current != null && current.IsAlive)
        {
            bool person = current.Category == TargetCategory.Creature;
            if (person ? (current.transform.position - ai.transform.position).sqrMagnitude <= chaseLimit * chaseLimit : !current.IsBroken)
            {
                return;
            }
        }

        Damageable objective = NearestBuilding(ai.transform.position) ?? fallback;
        if (objective != null)
        {
            ai.Engage(objective);
        }
    }

    /// <summary>The nearest building still standing, or null.</summary>
    public static Damageable NearestBuilding(Vector3 from)
    {
        Damageable nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building != null && building.TryGetComponent(out Damageable health) && health.IsAlive && !health.IsBroken)
            {
                float distance = (building.transform.position - from).sqrMagnitude;
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
