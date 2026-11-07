using UnityEngine;

/// <summary>
/// Where a melee swing goes: the aim nudged towards the nearest hostile creature in the assist cone (so a swing meant for
/// a wolf finds it), and which staggered creature a light or rushing swing can finish from behind. Pure queries over the
/// scene; the swing itself is PlayerCombat's.
/// </summary>
public static class MeleeTargeting
{
    private static readonly Collider[] Nearby = new Collider[32];

    /// <summary>
    /// The nearest hostile creature within <paramref name="assistAngle"/> of the aim and the attack's reach (plus
    /// <paramref name="assistReach"/>), preferring what is near the aim, then what is near: the direction to it, else the
    /// aim as given (flattened). <paramref name="targetDistance"/> is how far it is, or -1 when none.
    /// </summary>
    public static Vector3 AimAssist(Vector3 position, Vector3 forward, Vector3 aim, AttackDefinition attack, float assistAngle, float assistReach,
        LayerMask layers, Faction side, out float targetDistance)
    {
        targetDistance = -1f;
        aim.y = 0f;
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : forward;
        if (attack == null || assistAngle <= 0f)
        {
            return aim;
        }

        float reach = attack.Range + attack.Lunge + assistReach;
        Vector3 chest = position + Vector3.up;
        int count = Physics.OverlapSphereNonAlloc(chest, reach, Nearby, layers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 chosen = aim;
        for (int i = 0; i < count; i++)
        {
            IDamageable target = Nearby[i].GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || target.Faction.AreAllies(side) || target.Category != TargetCategory.Creature)
            {
                continue;
            }

            Vector3 to = HitDetector.NearestPoint(Nearby[i], chest) - position;
            to.y = 0f;
            float distance = to.magnitude;
            float angle = distance > 0.05f ? Vector3.Angle(aim, to) : 0f;
            // Prefer what is near the aim, then what is near the player.
            float score = angle / Mathf.Max(assistAngle, 1f) + distance / reach;
            if (angle <= assistAngle && score < best)
            {
                best = score;
                chosen = distance > 0.05f ? to / distance : aim;
                targetDistance = distance;
            }
        }

        return chosen;
    }

    /// <summary>The nearest staggered creature that can be finished from <paramref name="position"/> (behind it, in
    /// reach: StrikeRules.CanFinish), or null.</summary>
    public static MonsterAI FindFinishable(Vector3 position, CombatTechniqueSettings rules)
    {
        MonsterAI best = null;
        float bestDistance = float.MaxValue;
        foreach (MonsterAI monster in MonsterAI.Active)
        {
            if (monster == null)
            {
                continue;
            }

            Vector3 at = monster.transform.position;
            if (!StrikeRules.CanFinish(monster.IsStaggered, monster.CanBeFinished, at, monster.transform.forward, position, rules.BackstabArc, rules.FinishReach))
            {
                continue;
            }

            float distance = (at - position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = monster;
            }
        }

        return best;
    }
}
