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
        List<RaidRumourDefinition> rumours = RaidRumours.For(director.Session.Definition, director.Session.Seed, GameSession.Settlement, crafting);
        RaidMap map = FindAnyObjectByType<RaidMap>();
        IReadOnlyList<Vector3> spots = map != null ? map.RumourSpots : null;
        var random = new System.Random(RaidLayout.SeedFor(director.Session.Seed, "Rumour Spots"));
        var path = new NavMeshPath();
        for (int i = 0; i < rumours.Count; i++)
        {
            Vector3 spot;
            if (spots != null && i < spots.Count)
            {
                spot = spots[i];
            }
            else if (!RaidSpots.TryFindSpot(director.Player.transform.position, FarFrom, FarTo, random, path, out spot))
            {
                continue;
            }

            events.BeginAt(rumours[i].Site, spot);
        }
    }
}
