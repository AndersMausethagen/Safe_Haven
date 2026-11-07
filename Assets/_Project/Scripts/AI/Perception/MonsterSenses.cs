using System;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A monster's eyes: looks for targets in its sight cone and close range, checks line of sight and fills its
/// <see cref="Awareness"/> through PerceptionRules. Owned by MonsterAI, which decides what to do about it (look,
/// investigate, hunt). Terrain, trees, rocks and buildings block sight; characters don't. The player's visibility comes
/// from PlayerStealth (sneaking, standing still, fighting); every other target is plainly visible.
/// </summary>
public sealed class MonsterSenses
{
    private static readonly Collider[] Overlaps = new Collider[64];
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    /// <summary>How much of what it sees gets through: less while it sleeps.</summary>
    public float Attention { get; set; } = 1f;


    private readonly Transform self;
    private readonly MonsterDefinition definition;
    private readonly LayerMask targetLayers;
    private readonly Func<IDamageable, Collider, bool> isCandidate;

    public MonsterSenses(Transform self, MonsterDefinition definition, LayerMask targetLayers,
        Func<IDamageable, Collider, bool> isCandidate)
    {
        this.self = self;
        this.definition = definition;
        this.targetLayers = targetLayers;
        this.isCandidate = isCandidate;
    }

    public Awareness Awareness { get; } = new Awareness();

    public AwarenessStage Stage => Awareness.StageAt(PerceptionSettings.Current.SuspiciousAt);

    /// <summary>True when the last <see cref="Look"/> saw something it is suspicious of.</summary>
    public bool IsSeeingSomething { get; private set; }

    /// <summary>The target it spotted on the <see cref="Look"/> that returned true.</summary>
    public IDamageable Spotted { get; private set; }
    public Collider SpottedCollider { get; private set; }

    /// <summary>
    /// Looks around for <paramref name="elapsed"/> seconds' worth of noticing. Returns true the moment awareness fills
    /// and a target is spotted (<see cref="Spotted"/>); otherwise awareness rises toward the best glimpse or drains.
    /// </summary>
    public bool Look(float elapsed)
    {
        elapsed *= Attention;
        PerceptionSettings rules = PerceptionSettings.Current;
        SenseProfile senses = SenseProfile.Of(definition);
        float environment = AtmosphereConditions.SightScale(rules);
        Vector3 eye = Eye(rules);
        float reach = Mathf.Max(senses.CloseRange, senses.SightRange * rules.MaxRangeScale);
        int count = Physics.OverlapSphereNonAlloc(eye, reach, Overlaps, targetLayers, QueryTriggerInteraction.Ignore);

        float bestRate = 0f;
        IDamageable best = null;
        Collider bestCollider = null;
        Vector3 bestPoint = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider candidateCollider = Overlaps[i];
            IDamageable candidate = candidateCollider.GetComponentInParent<IDamageable>();
            if (candidate == null || !isCandidate(candidate, candidateCollider))
            {
                continue;
            }

            Transform target = ((Component)candidate).transform;
            Vector3 point = target.position + Vector3.up * rules.TargetHeight;
            Vector3 flat = point - self.position;
            flat.y = 0f;
            float distance = flat.magnitude;
            float angle = distance > 0.01f ? Vector3.Angle(self.forward, flat) : 0f;
            float rate = PerceptionRules.NoticeRate(distance, angle, senses, VisibilityOf(target), rules, environment);
            if (rate <= bestRate)
            {
                continue;
            }

            if (distance > senses.CloseRange && !CanSeeAny(eye, target, rules))
            {
                continue;
            }

            bestRate = rate;
            best = candidate;
            bestCollider = candidateCollider;
            bestPoint = target.position;
        }

        IsSeeingSomething = best != null;
        if (best == null)
        {
            Awareness.Decay(rules.DecayPerSecond * elapsed);
            return false;
        }

        if (!Awareness.Notice(bestRate * elapsed, bestPoint))
        {
            return false;
        }

        Spotted = best;
        SpottedCollider = bestCollider;
        return true;
    }

    /// <summary>
    /// True when a target it already hunts is still in sight: within <paramref name="range"/> with nothing in between
    /// (a hunting monster watches its target, so the cone doesn't apply), or inside close range.
    /// </summary>
    public bool CanSee(Transform target, float range)
    {
        if (target == null)
        {
            return false;
        }

        PerceptionSettings rules = PerceptionSettings.Current;
        Vector3 point = target.position + Vector3.up * rules.TargetHeight;
        Vector3 flat = point - self.position;
        flat.y = 0f;
        float distance = flat.magnitude;
        if (distance <= definition.CloseSenseRange)
        {
            return true;
        }

        return distance <= range && CanSeeAny(Eye(rules), target, rules);
    }

    /// <summary>True when no cover stands between it and a sound at <paramref name="position"/>.</summary>
    public bool HasClearPathTo(Vector3 position)
    {
        PerceptionSettings rules = PerceptionSettings.Current;
        return HasLineOfSight(Eye(rules), position + Vector3.up * rules.TargetHeight, self);
    }

    private Vector3 Eye(PerceptionSettings rules) => self.position + Vector3.up * rules.EyeHeight;

    // Chest or head: a small rise in the ground that hides the body doesn't hide a standing head, a hill hides both.
    private bool CanSeeAny(Vector3 eye, Transform target, PerceptionSettings rules)
    {
        Vector3 chest = target.position + Vector3.up * rules.TargetHeight;
        return HasLineOfSight(eye, chest, target)
            || HasLineOfSight(eye, chest + Vector3.up * rules.HeadAboveTarget, target);
    }

    private bool HasLineOfSight(Vector3 eye, Vector3 point, Transform target)
    {
        Vector3 toPoint = point - eye;
        float length = toPoint.magnitude;
        if (length < 0.01f)
        {
            return true;
        }

        int count = Physics.RaycastNonAlloc(eye, toPoint / length, Hits, length,
            PerceptionSettings.Current.SightBlockers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider blocker = Hits[i].collider;
            Transform hit = blocker.transform;
            if (hit.IsChildOf(self) || hit.IsChildOf(target) || IsCharacter(blocker))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    // Creatures, residents and the player move on NavMeshAgents or a CharacterController; they never hide anyone.
    private static bool IsCharacter(Collider collider)
    {
        return collider is CharacterController || collider.GetComponentInParent<NavMeshAgent>() != null
            || collider.GetComponentInParent<Player>() != null;
    }

    private static float VisibilityOf(Transform target)
    {
        // Co-op: a friend's body carries their own game's stealth.
        if (target.TryGetComponent(out FriendHitbox friend))
        {
            return friend.Visibility;
        }

        Player player = Player.Current;
        if (player != null && player.Stealth != null && target.IsChildOf(player.transform))
        {
            return player.Stealth.Visibility;
        }

        return 1f;
    }
}
