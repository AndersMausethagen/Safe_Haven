using System.Collections.Generic;
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

    // What called them out, kept from when they were posted: by the time they stand down the breach is already over (IsActive false).
    private bool inBreach;

    public void Stand(ResidentDefinition who, CompanionDefinition how, Vector3 post, bool breach)
    {
        inBreach = breach;
        Companion prefab = SettlementSettings.Current.RaidCompanionPrefab;
        if (prefab == null || !NavMesh.SamplePosition(self.position, out NavMeshHit here, 6f, NavMesh.AllAreas))
        {
            return;
        }

        guard = Companion.SpawnAs(prefab, who, how, here.position, false, false);
        // Who they are counts at the ward too: talents, traits, marks and their own kit (CompanionPeople).
        ResidentRecord record = GameSession.Here.Find(who);
        guard.KitWeapon = how == who.RaidCompanion ? CompanionKit.ShownWeapon(record) : null;
        // (CompanionPeople counts the kit's weapon only for their own kind, as it is shown.)
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

        Remember(guard, inBreach);

        Vector3 there = guard.transform.position;
        Object.Destroy(guard.gameObject);
        guard = null;
        Show(true);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.Warp(there);
        }
    }

    // What defending the Haven did to them (Docs/PeopleSystemReview_2026-11-23.md §2.7): helped up by the player grows their bond; downed in
    // a breach may leave a raid's marks (an old wound, a fear of what downed them), downed on a corruption night its own; foes felled count
    // toward marks as on raids. Only in the game that keeps the Haven.
    private static void Remember(Companion fought, bool breach)
    {
        if (HavenVisit.IsVisiting || fought.Resident == null)
        {
            return;
        }

        ResidentRecord record = GameSession.Home.Find(fought.Resident);
        if (record == null)
        {
            return;
        }

        if (fought.TimesHelpedUp > 0)
        {
            PeopleBond.Grow(record, PeopleSettings.Current.BondHelpedUp, "helped up defending");
        }

        var facts = new RaidFacts { Extracted = true, TimesDowned = Mathf.Max(fought.TimesHelpedUp, fought.DownedBy.Count), KillsBy = fought.KillsBy, DownedBy = fought.DownedBy };
        // A night's terrors only on a corruption night (a horn call by day leaves none).
        bool night = CorruptionNights.Current != null && CorruptionNights.Current.IsActive;
        List<MarkDefinition> came = breach ? ResidentMarks.AfterRaid(record, facts, () => Random.value)
            : facts.TimesDowned > 0 && night ? ResidentMarks.AfterEvent(record, MarkCause.NightDowned, "", () => Random.value) : new List<MarkDefinition>();
        foreach (MarkDefinition mark in came)
        {
            PeopleNews.Tell($"{record.Resident.DisplayName} came out of the fight {(mark.Good ? "with" : "carrying")} {mark.DisplayName}.", mark.Good ? MessageKind.Gain : MessageKind.Info);
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
