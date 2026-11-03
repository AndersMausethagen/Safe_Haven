using UnityEngine;

/// <summary>A small authored pack, with shared alerts and staggered attack starts. It can break: once its leader falls or too many have,
/// the rest flee for a while.</summary>
public sealed class MonsterPack : MonoBehaviour
{
    [SerializeField] private MonsterAI leader;
    [SerializeField] private GameObject optionalMember;
    [SerializeField, Min(0.1f)] private float attackSpacing = 0.65f;
    [Tooltip("The rest flee once the leader has fallen.")]
    [SerializeField] private bool breaksWithItsLeader;
    [Tooltip("The rest flee once this share of the pack has fallen. 0: it never breaks from losses.")]
    [SerializeField, Range(0f, 1f)] private float breaksAtLosses;
    [Tooltip("Seconds they flee for, before they come back at whoever is still in reach.")]
    [SerializeField, Min(0f)] private float fleeSeconds = 8f;
    private float nextAttackAt;
    private MonsterAI[] members;
    private bool broken;

    /// <summary>Whether a pack breaks, with this many of it left of its whole.</summary>
    public static bool Breaks(int left, int whole, bool leaderFell, bool breaksWithLeader, float atLosses)
    {
        if (left <= 0 || whole <= 0)
        {
            return false;
        }

        return (leaderFell && breaksWithLeader) || (atLosses > 0f && 1f - (float)left / whole >= atLosses);
    }

    /// <summary>A member has died: the rest may break.</summary>
    public void MemberFell(MonsterAI fallen)
    {
        if (broken || members == null)
        {
            return;
        }

        int whole = 0, left = 0;
        foreach (MonsterAI member in members)
        {
            if (member != null && member.gameObject.activeInHierarchy)
            {
                whole++;
                if (member != fallen && member.CurrentState != MonsterAI.State.Dead)
                {
                    left++;
                }
            }
        }

        if (!Breaks(left, whole, fallen == leader, breaksWithItsLeader, breaksAtLosses))
        {
            return;
        }

        broken = true;
        foreach (MonsterAI member in members)
        {
            if (member != null && member != fallen)
            {
                member.Rout(fleeSeconds);
            }
        }
    }

    private void Awake()
    {
        members = GetComponentsInChildren<MonsterAI>(true);
        if (optionalMember != null)
        {
            int seed = GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed : 1;
            seed = RaidLayout.SeedFor(seed, $"{transform.position.x:F2},{transform.position.z:F2}");
            optionalMember.SetActive(new System.Random(seed).Next(2) == 1);
        }
        foreach (MonsterAI member in members)
        {
            var agent = member.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (member.gameObject.activeInHierarchy
                && UnityEngine.AI.NavMesh.SamplePosition(member.transform.position, out UnityEngine.AI.NavMeshHit hit, 3f, agent.areaMask))
            {
                agent.Warp(hit.position);
            }
        }
    }

    public bool IsLeader(MonsterAI member) => member == leader;

    public bool TryAttack()
    {
        if (Time.time < nextAttackAt)
        {
            return false;
        }
        nextAttackAt = Time.time + attackSpacing;
        return true;
    }

    public void Alert(IDamageable target)
    {
        foreach (MonsterAI member in members)
        {
            if (member != null && member.isActiveAndEnabled && member.Target != target)
            {
                member.Engage(target);
            }
        }
    }
}
