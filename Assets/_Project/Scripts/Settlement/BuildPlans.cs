using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plans laid out before their materials are home (Construction.BeginPlan; the Keeper's view, Docs/ColonyModeResearch_2026-11-21.md
/// §4.5): every few seconds a plan whose whole cost the chests now hold is paid from them, and the builders start; the rest are noted
/// on the settlement (SettlementState.Plans) so a raid can mark what they want (WantedAtHome). On the portal, added by it; only in the
/// game that keeps the Haven.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildPlans : MonoBehaviour
{
    private const float LookSeconds = 2f;
    private readonly List<BuildingDefinition> waiting = new List<BuildingDefinition>();
    private float nextLook;

    private float nextRingLook;

    // Home again: the trees felled while away come down in their rings, with their saplings (RingWork); saved felling is put back
    // in the buildings' and terrains' own Start, so wait for it.
    private System.Collections.IEnumerator Start()
    {
        yield return null;
        yield return null;
        if (!HavenVisit.IsVisiting)
        {
            RingWork.SettleAtHome(GameSession.Home, WorkerSettings.Current);
        }
    }

    private void Update()
    {
        if (HavenVisit.IsVisiting || Time.unscaledTime < nextLook)
        {
            return;
        }

        nextLook = Time.unscaledTime + LookSeconds;
        // The trees standing in each ring, now and then, so the work while away stops where the ring runs out.
        if (Time.unscaledTime >= nextRingLook)
        {
            nextRingLook = Time.unscaledTime + 20f;
            foreach (RingOrder ring in GameSession.Home.Rings)
            {
                ring.StandingSeen = RingWork.Standing(ring);
            }
        }
        waiting.Clear();
        // The chests only: what the player carries is theirs to spend (they can hammer a plan with it themselves).
        var chests = new MaterialPool(null, null, GameSession.Stash.Resources, GameSession.Stash.Items);
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building == null || building.Definition == null || !building.TryGetComponent(out Construction construction) || !construction.IsPlan)
            {
                continue;
            }

            if (construction.TryPayPlan(chests))
            {
                GameMessages.Post($"The {building.Definition.DisplayName}'s materials are home: it is paid for, and the builders can start.", MessageKind.Gain);
            }
            else
            {
                waiting.Add(building.Definition);
            }
        }

        GameSession.Home.NotePlans(waiting);
    }
}
