using UnityEngine;

/// <summary>A small authored pack, with shared alerts and staggered attack starts. It can break: once its leader falls or too many have,
/// the rest flee for a while.</summary>
[OnAFriendsCopy(CopyRule.Same, "on a copy its creatures think nothing, so its calls change nothing; the host's pack decides")]
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
    private int startCount;
    private bool broken;
    private bool leaderMarked;
    private const float LeaderScale = 1.1f;

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

        // The pack as it set out: a fallen member's body is destroyed a moment after it dies, and counting only what is still
        // there shrank the whole with every corpse, so the loss rule fired only when two fell within that moment.
        int left = 0;
        foreach (MonsterAI member in members)
        {
            if (member != null && member.gameObject.activeInHierarchy && member != fallen && member.CurrentState != MonsterAI.State.Dead)
            {
                left++;
            }
        }

        if (!Breaks(left, startCount, fallen == leader, breaksWithItsLeader, breaksAtLosses))
        {
            return;
        }

        broken = true;
        int crying = 0;
        foreach (MonsterAI member in members)
        {
            if (member != null && member != fallen)
            {
                // One after another, so a pack's yelps are heard as a pack breaking, not one sound.
                member.Rout(fleeSeconds, 0.15f * crying++);
            }
        }

        // Said once a pack, so the player knows what they did (Halo, F.E.A.R.: show the group's decision).
        if (crying > 0 && Player.Current != null && Vector3.Distance(Player.Current.transform.position, transform.position) < 40f)
        {
            GameMessages.Post("The pack breaks!", MessageKind.Info);
        }

        PlaytestLogger.Note(PlaytestLog.EnemyAI, "Rout", crying, fallen != null && fallen.Definition != null ? fallen.Definition.name : name);
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

        // The one that leads stands out (§3): a little bigger and paler, so "bring down the one that howls" can be acted on.
        if (leader != null && !leaderMarked)
        {
            leaderMarked = true;
            leader.transform.localScale *= LeaderScale;
            CreatureLook.Tint(leader.gameObject, new Color(1.15f, 1.12f, 1.08f, 1f));
        }

        startCount = 0;
        foreach (MonsterAI member in members)
        {
            if (member.gameObject.activeInHierarchy)
            {
                startCount++;
            }
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
            // One holding a grudge against a companion keeps it (MonsterAI.TakeGrudge).
            if (member != null && member.isActiveAndEnabled && member.Target != target && !member.HoldsGrudgeNow)
            {
                member.Engage(target);
            }
        }
    }
}
