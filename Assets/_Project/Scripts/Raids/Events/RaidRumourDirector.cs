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
    private const float BeaconShowsWithin = 40f;

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
        List<RaidRumourDefinition> rumours = RaidRumours.For(director.Session.Definition, director.Session.Seed, GameSession.Settlement, crafting, director.Session.WidthScale);
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
