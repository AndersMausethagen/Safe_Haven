using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the Haven may ask of the player (HavenRequestDefinition) and how many asks stand at once. The asks in hand are
/// SettlementState.Requests (saved); HavenRequestBoard hands them in and asks again.
/// </summary>
[CreateAssetMenu(fileName = "HavenRequestSettings", menuName = "Safe Haven/Settings/Haven Requests")]
public sealed class HavenRequestSettings : GameSettings<HavenRequestSettings>
{
    [SerializeField] private List<HavenRequestDefinition> requests = new List<HavenRequestDefinition>();
    [Tooltip("Asks standing at once.")]
    [SerializeField, Min(1)] private int atOnce = 3;
    [Tooltip("The building the asks are posted at: none are asked until one stands (asks already standing stay). Empty: asked from the " +
             "start. The Notice Board, built once the Haven is a Homestead (the user, 2026-11-15: the board comes later, and brings " +
             "the fetch and crafting asks with it).")]
    [SerializeField] private BuildingDefinition postedAt;

    public IReadOnlyList<HavenRequestDefinition> Requests => requests;
    public int AtOnce => atOnce;
    public BuildingDefinition PostedAt => postedAt;

    /// <summary>Whether new asks can be posted: the Posted At building stands (or none is set).</summary>
    public bool CanPost => postedAt == null || BuildingUnlocks.HighestTier(postedAt) > 0;

    public HavenRequestDefinition Find(string id) => requests.Find(request => request != null && request.Id == id);

    public void Configure(List<HavenRequestDefinition> all, int standing)
    {
        requests = all;
        atOnce = standing;
    }

    /// <summary>
    /// New asks to fill the open places: those that may be asked now and aren't already, picked by the day (so a
    /// reload asks the same), never two from the same person at once.
    /// </summary>
    public static List<HavenRequestDefinition> Choose(IReadOnlyList<HavenRequestDefinition> pool, IReadOnlyList<HavenRequestDefinition> standing,
        int places, SettlementState settlement, string season, int day)
    {
        var chosen = new List<HavenRequestDefinition>();
        if (pool == null || places <= 0)
        {
            return chosen;
        }

        var open = new List<HavenRequestDefinition>();
        foreach (HavenRequestDefinition request in pool)
        {
            if (request != null && request.CanAsk(settlement, season) && !Contains(standing, request))
            {
                open.Add(request);
            }
        }

        var random = new System.Random(day * 7919 + 17);
        while (chosen.Count < places && open.Count > 0)
        {
            int index = random.Next(open.Count);
            HavenRequestDefinition pick = open[index];
            open.RemoveAt(index);
            if (pick.Asker != null && (SameAsker(standing, pick) || SameAsker(chosen, pick)))
            {
                continue;
            }

            // One crafting order at a time: the raids stay the main reason to go out.
            if (pick.IsOrder && (HasOrder(standing) || HasOrder(chosen)))
            {
                continue;
            }

            chosen.Add(pick);
        }

        return chosen;
    }

    private static bool Contains(IReadOnlyList<HavenRequestDefinition> list, HavenRequestDefinition request)
    {
        if (list == null)
        {
            return false;
        }

        foreach (HavenRequestDefinition one in list)
        {
            if (one == request)
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameAsker(IReadOnlyList<HavenRequestDefinition> list, HavenRequestDefinition request)
    {
        if (list == null)
        {
            return false;
        }

        foreach (HavenRequestDefinition one in list)
        {
            if (one != null && one.Asker == request.Asker)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOrder(IReadOnlyList<HavenRequestDefinition> requests)
    {
        if (requests == null)
        {
            return false;
        }

        foreach (HavenRequestDefinition request in requests)
        {
            if (request != null && request.IsOrder)
            {
                return true;
            }
        }

        return false;
    }
}
