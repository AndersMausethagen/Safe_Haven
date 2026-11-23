using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A resident whose job defends the Haven, standing guard through a ward breach (GDD §6.8). The resident steps out of the scene for the
/// fight and a Companion with their model steps in where they stood, so they fight, get knocked down and get up as anyone in a party
/// does. Nobody dies of a breach: on a guard's post a Companion gets up by itself. Plain C#, held by ResidentLife, as that is all a
/// resident needs of it.
/// </summary>
public sealed class ResidentDefender
{
    private readonly Transform self;
    private readonly Renderer[] renderers;
    private readonly Collider[] colliders;
    private readonly Animator animator;
    private readonly NavMeshAgent agent;
    private readonly Interactable interactable;
    private Companion guard;

    public ResidentDefender(Transform self, Animator animator, NavMeshAgent agent)
    {
        this.self = self;
        this.animator = animator;
        this.agent = agent;
        renderers = self.GetComponentsInChildren<Renderer>(true);
        colliders = self.GetComponentsInChildren<Collider>(true);
        interactable = self.GetComponent<Interactable>();
    }

    public bool IsOnGuard => guard != null;

    /// <summary>The guard they have become, or null.</summary>
    public Companion Guard => guard;

    public void Stand(ResidentDefinition who, CompanionDefinition how, Vector3 post)
    {
        Companion prefab = SettlementSettings.Current.RaidCompanionPrefab;
        if (prefab == null || !NavMesh.SamplePosition(self.position, out NavMeshHit here, 6f, NavMesh.AllAreas))
        {
            return;
        }

        guard = Companion.SpawnAs(prefab, who, how, here.position, false, false);
        // Who they are counts at the ward too: talents, traits, marks and their own kit (CompanionPeople).
        ResidentRecord record = GameSession.Here.Find(who);
        guard.KitWeapon = how == who.RaidCompanion ? CompanionKit.ShownWeapon(record) : null;
        CompanionPeople.Apply(guard, record, true, false);
        guard.Command(CompanionOrder.Stay, post);
        Show(false);
    }

    public void StandDown()
    {
        if (guard == null)
        {
            return;
        }

        Vector3 there = guard.transform.position;
        Object.Destroy(guard.gameObject);
        guard = null;
        Show(true);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(there);
        }
    }

    private void Show(bool shown)
    {
        foreach (Renderer part in renderers)
        {
            if (part != null)
            {
                part.enabled = shown;
            }
        }

        foreach (Collider part in colliders)
        {
            if (part != null)
            {
                part.enabled = shown;
            }
        }

        if (animator != null)
        {
            animator.enabled = shown;
        }

        if (agent != null)
        {
            agent.isStopped = !shown;
        }

        if (interactable != null)
        {
            interactable.enabled = shown;
        }
    }
}
