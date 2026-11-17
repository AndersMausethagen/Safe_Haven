using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds what an attack hits: every living, hostile IDamageable inside a cone (or all the way around, for a 360 degree
/// attack) in front of the attacker, nearest first, up to the attack's target limit. Range is measured to the nearest
/// point of each target, so a wide wall is hit by its edge. Melee attacks, area abilities and explosions all use it.
/// </summary>
public static class HitDetector
{
    // Where the hit area is centered above the attacker's feet.
    private const float CenterHeight = 1f;

    private static readonly Collider[] Overlaps = new Collider[64];
    private static readonly RaycastHit[] Between = new RaycastHit[16];
    private static readonly System.Comparison<Hit> ByDistance = (a, b) => a.Distance.CompareTo(b.Distance);

    public readonly struct Hit
    {
        public readonly IDamageable Target;
        public readonly Vector3 Point;
        public readonly float Distance;

        public Hit(IDamageable target, Vector3 point, float distance)
        {
            Target = target;
            Point = point;
            Distance = distance;
        }
    }

    /// <summary>Fills results with the targets the attack reaches, nearest first.</summary>
    public static void FindTargets(
        Vector3 origin,
        Vector3 direction,
        AttackDefinition attack,
        Faction attackerFaction,
        int layerMask,
        List<Hit> results,
        float arcBonus = 0f)
    {
        results.Clear();

        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        Vector3 center = origin + Vector3.up * CenterHeight;
        int count = Physics.OverlapSphereNonAlloc(center, attack.Range, Overlaps, layerMask, QueryTriggerInteraction.Ignore);
        float halfArc = Mathf.Min(360f, attack.Arc + arcBonus) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            // Resolve the target first: most colliders (ground, props) aren't damageable and need no further work.
            IDamageable target = Overlaps[i].GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || target.Faction.AreAllies(attackerFaction) || Contains(results, target))
            {
                continue;
            }

            Vector3 point = NearestPoint(Overlaps[i], center);
            Vector3 toTarget = point - origin;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            // A target touching the attacker counts as inside the cone however it is angled.
            if (distance > 0.05f && Vector3.Angle(direction, toTarget) > halfArc)
            {
                continue;
            }

            if (distance > 0.05f && Walled(center, point, target))
            {
                continue;
            }

            results.Add(new Hit(target, point, distance));
        }

        results.Sort(ByDistance);

        if (attack.MaxTargets > 0 && results.Count > attack.MaxTargets)
        {
            results.RemoveRange(attack.MaxTargets, results.Count - attack.MaxTargets);
        }
    }

    // Scenery between the blow and the target (a palisade, a gate, an arena's root wall, a hill) takes it: a blow was a sphere and a
    // cone, and trolls and the player hit each other through walls. Creatures and players in between don't stop it (a sweep through a
    // crowd), nor does the target's own body.
    private static bool Walled(Vector3 from, Vector3 to, IDamageable target)
    {
        Vector3 way = to - from;
        float length = way.magnitude;
        if (length < 0.05f)
        {
            return false;
        }

        int count = Physics.RaycastNonAlloc(from, way / length, Between, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider wall = Between[i].collider;
            // The living (creatures, companions, villagers, horses, players) walk on agents or controllers.
            if (wall == null || wall.GetComponentInParent<IDamageable>() == target || wall.GetComponentInParent<MonsterAI>() != null
                || wall.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() != null || wall.GetComponentInParent<CharacterController>() != null
                || wall.GetComponentInParent<NetworkPlayerAvatar>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>Fills results with the living, hostile targets within <paramref name="radius"/> of the path from <paramref name="start"/> to
    /// <paramref name="end"/> (feet height; the path is searched at body height), nearest the start first, up to <paramref name="maxTargets"/>
    /// (0: all). A dash through enemies and a thrown weapon use it.</summary>
    public static void FindAlong(Vector3 start, Vector3 end, float radius, Faction attackerFaction, int layerMask, int maxTargets, List<Hit> results)
    {
        results.Clear();
        Vector3 a = start + Vector3.up * CenterHeight, b = end + Vector3.up * CenterHeight;
        int count = Physics.OverlapCapsuleNonAlloc(a, b, Mathf.Max(0.05f, radius), Overlaps, layerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            IDamageable target = Overlaps[i].GetComponentInParent<IDamageable>();
            if (target == null || !target.IsAlive || target.Faction.AreAllies(attackerFaction) || Contains(results, target))
            {
                continue;
            }

            Vector3 point = NearestPoint(Overlaps[i], Vector3.Lerp(a, b, 0.5f));
            Vector3 flat = point - start;
            flat.y = 0f;
            results.Add(new Hit(target, point, flat.magnitude));
        }

        results.Sort(ByDistance);
        if (maxTargets > 0 && results.Count > maxTargets)
        {
            results.RemoveRange(maxTargets, results.Count - maxTargets);
        }
    }

    private static bool Contains(List<Hit> hits, IDamageable target)
    {
        foreach (Hit hit in hits)
        {
            if (hit.Target == target)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The point of a collider nearest to a position. Collider.ClosestPoint only supports convex shapes; anything else
    /// falls back to its bounds.
    /// </summary>
    public static Vector3 NearestPoint(Collider collider, Vector3 from)
    {
        bool supported = collider is BoxCollider
            || collider is SphereCollider
            || collider is CapsuleCollider
            || collider is CharacterController
            || (collider is MeshCollider mesh && mesh.convex);

        return supported ? collider.ClosestPoint(from) : collider.bounds.ClosestPoint(from);
    }
}
