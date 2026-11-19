using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes the briefing's rumours true: when the raid begins it stands each one's event at a place the map set aside for it
/// (RaidMap.RumourSpots), or on a painted map somewhere far from the player. They wait on the HUD for the whole raid and don't
/// count against the raid's random events. Installed by RaidRumoursSystem.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RaidEventDirector))]
public sealed class RaidRumourDirector : MonoBehaviour
{
    private const float FarFrom = 60f;
    private const float FarTo = 130f;

    /// <summary>Metres within which a rumour's beacon shows and its site counts as found; a standing site's too (RaidEventDirector.Stand).</summary>
    public const float BeaconShowsWithin = 40f;

    private RaidDirector director;
    private RaidEventDirector events;
    private bool placed;

    private void Awake()
    {
        director = GetComponent<RaidDirector>();
        events = GetComponent<RaidEventDirector>();
    }

    private void Update()
    {
        if (placed || !director.IsRunning || director.Player == null)
        {
            return;
        }

        placed = true;
        PlayerCrafting crafting = director.Player.GetComponent<PlayerCrafting>();
        List<RaidRumourDefinition> rumours = RaidRumours.For(director.Session.Definition, director.Session.Seed, GameSession.Here, crafting,
            director.Session.WidthScale, director.Session.ExtraRumours, director.Session.QuestsUnderWay);
        RaidMap map = FindAnyObjectByType<RaidMap>();
        IReadOnlyList<Vector3> spots = map != null ? map.RumourSpots : null;
        var random = new System.Random(RaidLayout.SeedFor(director.Session.Seed, "Rumour Spots"));
        var path = new NavMeshPath();
        var free = new List<Vector3>();
        if (spots != null)
        {
            free.AddRange(spots);
        }

        // Those who ask for a place choose first.
        rumours.Sort((a, b) => (b.SetPiece != null).CompareTo(a.SetPiece != null));
        for (int i = 0; i < rumours.Count; i++)
        {
            Vector3 spot;
            if (free.Count > 0)
            {
                spot = TakeSpot(free, rumours[i].SetPiece);
            }
            else if (!RaidSpots.TryFindSpot(director.Player.transform.position, FarFrom, FarTo, random, path, out spot))
            {
                continue;
            }

            events.BeginAt(rumours[i].Site, spot);
            RaidEventSite site = events.PinnedSites.Count > 0 ? events.PinnedSites[events.PinnedSites.Count - 1] : null;
            if (site != null)
            {
                site.PlaceName = PieceAt(spot)?.DisplayName ?? "";
                site.HideBeaconBeyond(BeaconShowsWithin);
                PointTheWay(site, rumours[i].Seeks);
            }
        }
    }

    /// <summary>
    /// A rumour that seeks someone whose rescue is under way points the way on screen as Ingrid's own traveler does: the quest's
    /// waypoint, at the middle of the map's search area until the site is found, then on the site (the user, 2026-11-19: every
    /// rescue quest should say roughly where to look; Bjarne's only stood at his forge once found).
    /// </summary>
    private void PointTheWay(RaidEventSite site, ResidentDefinition person)
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        if (person == null || log == null)
        {
            return;
        }

        foreach (QuestLog.Entry entry in log.Entries)
        {
            if (entry.Status != QuestStatus.Active || entry.Quest == null)
            {
                continue;
            }

            IReadOnlyList<QuestObjective> objectives = entry.Quest.Objectives;
            for (int o = 0; o < objectives.Count; o++)
            {
                if (objectives[o].Type != ObjectiveType.RescuePerson || objectives[o].Person != person)
                {
                    continue;
                }

                var mark = new GameObject("Rescue Waypoint - " + person.DisplayName);
                mark.SetActive(false);
                mark.transform.SetParent(site.transform, false);
                var waypoint = mark.AddComponent<QuestWaypoint>();
                waypoint.Configure(entry.Quest, o + 1, person.DisplayName + ", somewhere near");
                mark.AddComponent<RescueWaypointFollower>().Bind(site, waypoint, person.DisplayName);
                mark.SetActive(true);
                return;
            }
        }
    }

    // The spot with the place it asked for, else the first left. A hollow kept for a rumour that didn't come up is left till last.
    private Vector3 TakeSpot(List<Vector3> free, RaidSetPieceDefinition wanted)
    {
        int chosen = 0;
        for (int i = 0; i < free.Count; i++)
        {
            RaidSetPieceDefinition here = PieceAt(free[i]);
            if (wanted != null && here == wanted)
            {
                chosen = i;
                break;
            }
        }

        Vector3 spot = free[chosen];
        free.RemoveAt(chosen);
        return spot;
    }

    private static RaidSetPieceDefinition PieceAt(Vector3 spot)
    {
        foreach (RaidSetPieceSite site in FindObjectsByType<RaidSetPieceSite>(FindObjectsInactive.Exclude))
        {
            Vector3 between = site.transform.position - spot;
            between.y = 0f;
            if (between.sqrMagnitude < 4f)
            {
                return site.Definition;
            }
        }

        return null;
    }
}
