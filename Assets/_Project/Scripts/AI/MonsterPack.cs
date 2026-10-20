using UnityEngine;

/// <summary>A small authored pack, with shared alerts and staggered attack starts.</summary>
public sealed class MonsterPack : MonoBehaviour
{
    [SerializeField] private MonsterAI leader;
    [SerializeField] private GameObject optionalMember;
    [SerializeField, Min(0.1f)] private float attackSpacing = 0.65f;
    private float nextAttackAt;
    private MonsterAI[] members;

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
