using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Whom a monster may hunt, and whether a better target than its current one is in sight: one it would fight (faction,
/// alive, active; only priority targets when <see cref="PlayerOnly"/>), not standing in ground it avoids, and for a
/// defensive monster inside its ground. While hunting it asks <see cref="TryFindBetter"/> for a priority target (the
/// player) it can reach, otherwise a clearly nearer one (<see cref="TargetPriority"/>). How it hunts (chasing, leash,
/// giving up) stays in MonsterAI, which owns one of these.
/// </summary>
public sealed class MonsterTargeting
{
    // Another target must be this much nearer before it switches, so it doesn't flip between two.
    private const float SwitchTargetMargin = 1.5f;

    private static readonly Collider[] Overlaps = new Collider[64];

    private Transform self;
    private MonsterDefinition definition;
    private Combatant side;
    private IDamageable body;
    private NavMeshAgent agent;
    private LayerMask layers;
    private NavMeshPath reachPath;

    /// <summary>Only priority targets (the player), leaving buildings and residents alone (ward-breach and raid attackers).</summary>
    public bool PlayerOnly { get; set; }

    /// <summary>Ground it will not enter (night prowlers: light): targets standing there are not noticed or followed.</summary>
    public System.Predicate<Vector3> AvoidArea { get; set; }

    /// <summary>Called by MonsterAI on Awake.</summary>
    public void Bind(Transform owner, MonsterDefinition monster, Combatant faction, IDamageable ownBody, NavMeshAgent navigation, LayerMask targetLayers)
    {
        self = owner;
        definition = monster;
        side = faction;
        body = ownBody;
        agent = navigation;
        layers = targetLayers;
    }

    public static bool IsPriorityTarget(Component candidate) =>
        (candidate.TryGetComponent(out Combatant other) && other.IsPriorityTarget) || candidate.TryGetComponent(out FriendHitbox _);

    public bool IsAvoided(Vector3 position) => AvoidArea != null && AvoidArea(position);

    /// <summary>One it would fight: alive, active, hostile (and a priority target when Player Only).</summary>
    public bool CanTarget(IDamageable candidate)
    {
        var component = candidate as Component;
        if (component == null || !component.gameObject.activeInHierarchy || ReferenceEquals(candidate, body))
        {
            return false;
        }

        // Co-op: a knocked-down player is left for their friends to help (CoopDowned).
        if (CoopDowned.IsDown && Player.IsPlayer(component.gameObject))
        {
            return false;
        }

        if (PlayerOnly && !IsPriorityTarget(component))
        {
            return false;
        }

        // Someone waiting to be rescued is left alone until the player frees them (RescuableTraveler.IsWaiting).
        if (component.TryGetComponent(out RescuableTraveler stranded) && stranded.IsWaiting)
        {
            return false;
        }

        return candidate.IsAlive && side.Faction.WillAttack(candidate.Faction);
    }

    /// <summary>A target its senses may notice: one it would fight, outside avoided ground, and for a defensive monster
    /// (unless relentless) one inside its ground around <paramref name="home"/>.</summary>
    public bool IsSenseCandidate(IDamageable candidate, Collider candidateCollider, Vector3 home, bool relentless)
    {
        // Just after a raid's first-visit cinematic nothing notices anyone yet (a blow still provokes, by another way).
        if (RaidDirector.IsArrivalCalm || !CanTarget(candidate) || IsAvoided(candidateCollider.transform.position))
        {
            return false;
        }

        if (candidate is Component body && Companion.IsHidden(body))
        {
            return false;
        }

        return relentless || !definition.HasTemperament(MonsterTemperament.Defensive)
            || Vector3.Distance(home, candidateCollider.transform.position) <= definition.DefensiveRadius;
    }

    /// <summary>True when it can walk to within striking distance of the point: a complete path to the NavMesh beneath it.
    /// A player on a boulder or a roof is out of reach; their companions are not.</summary>
    public bool CanReach(Vector3 point)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            return true;
        }

        float reach = Mathf.Max(1.5f, definition.Attack != null ? definition.Attack.Range : 0f);
        if (!NavMesh.SamplePosition(point, out NavMeshHit hit, reach, agent.areaMask))
        {
            return false;
        }

        reachPath ??= new NavMeshPath();
        return NavMesh.CalculatePath(agent.nextPosition, hit.position, agent.areaMask, reachPath)
            && reachPath.status == NavMeshPathStatus.PathComplete;
    }

    /// <summary>
    /// While hunting <paramref name="current"/> (at <paramref name="currentDistance"/>), looks for a better target it can
    /// see: a priority target first while it can reach them, otherwise a clearly nearer one (a companion or traveller
    /// between it and the player).
    /// </summary>
    public bool TryFindBetter(IDamageable current, float currentDistance, MonsterSenses senses, Vector3 home, bool relentless,
        out IDamageable better, out Collider betterCollider)
    {
        better = null;
        betterCollider = null;
        Vector3 center = self.position + Vector3.up * MonsterAI.CenterHeight;
        int count = Physics.OverlapSphereNonAlloc(center, definition.DetectionRange, Overlaps, layers, QueryTriggerInteraction.Ignore);

        IDamageable best = null;
        Collider bestCollider = null;
        float bestDistance = float.MaxValue;
        IDamageable seenPlayer = null;
        Collider seenPlayerCollider = null;

        for (int i = 0; i < count; i++)
        {
            IDamageable candidate = Overlaps[i].GetComponentInParent<IDamageable>();
            if (ReferenceEquals(candidate, current) || !IsSenseCandidate(candidate, Overlaps[i], home, relentless))
            {
                continue;
            }

            Transform candidateBody = ((Component)candidate).transform;
            bool isPlayer = IsPriorityTarget(candidateBody);
            if (isPlayer && seenPlayer != null)
            {
                continue;
            }

            float distance = Flat(HitDetector.NearestPoint(Overlaps[i], center) - self.position);
            if ((isPlayer || distance < bestDistance) && senses.CanSee(candidateBody, definition.DetectionRange))
            {
                if (isPlayer)
                {
                    seenPlayer = candidate;
                    seenPlayerCollider = Overlaps[i];
                    continue;
                }

                best = candidate;
                bestCollider = Overlaps[i];
                bestDistance = distance;
            }
        }

        if (seenPlayer == null && best == null)
        {
            return false;
        }

        bool hunting = CanTarget(current);
        Component currentBody = current as Component;
        bool huntingPlayer = hunting && IsPriorityTarget(currentBody);
        // Reachability costs a path query, so it is only asked when the answer can change the choice.
        bool playerReachable = huntingPlayer ? best == null || CanReach(currentBody.transform.position)
            : seenPlayer != null && CanReach(((Component)seenPlayer).transform.position);
        bool otherNearer = best != null && (!hunting || bestDistance <= currentDistance - SwitchTargetMargin);
        bool otherReachable = best != null && (huntingPlayer ? !playerReachable : otherNearer) && CanReach(((Component)best).transform.position);
        switch (TargetPriority.Decide(huntingPlayer, seenPlayer != null, playerReachable, best != null, otherReachable, otherNearer))
        {
            case TargetPriority.Choice.Player:
                better = seenPlayer;
                betterCollider = seenPlayerCollider;
                return true;
            case TargetPriority.Choice.Other:
                better = best;
                betterCollider = bestCollider;
                return true;
            default:
                return false;
        }
    }

    private static float Flat(Vector3 offset)
    {
        offset.y = 0f;
        return offset.magnitude;
    }
}
