using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who works out at a place in the Haven's land, and when (Docs/HavenWorldStory_2026-11-26.md §6, "people at work out there"): a region's
/// arc leaves its place worked (the quarry cut again, Asa's field), and the one who works it walks out there in their free time and is
/// home at night. Filled by the places' stages as they stand (PlaceWorkerSpot); read by PeopleHabits (a habit at HabitPlace.Worked) and
/// ResidentLife (where it takes them). Nothing saved: it follows what the places show.
/// </summary>
public static class PlaceWorkers
{
    public readonly struct Spot
    {
        public Spot(Vector3 at, Vector3 look, float from, float until)
        {
            At = at;
            Look = look;
            From = from;
            Until = until;
        }

        public Vector3 At { get; }
        public Vector3 Look { get; }
        public float From { get; }
        public float Until { get; }

        /// <summary>True at <paramref name="hour"/> (a span past midnight counts too).</summary>
        public bool During(float hour) => From <= Until ? hour >= From && hour < Until : hour >= From || hour < Until;
    }

    private static readonly Dictionary<ResidentDefinition, (Object owner, Spot spot)> Spots = new Dictionary<ResidentDefinition, (Object, Spot)>();

    // Entering play mode doesn't reload scripts in this project.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Spots.Clear();

    public static void Register(Object owner, ResidentDefinition who, Spot spot)
    {
        if (owner != null && who != null)
        {
            Spots[who] = (owner, spot);
        }
    }

    /// <summary>The owner's spot goes (its stage no longer shows).</summary>
    public static void Unregister(Object owner)
    {
        ResidentDefinition gone = null;
        foreach (KeyValuePair<ResidentDefinition, (Object owner, Spot spot)> entry in Spots)
        {
            if (entry.Value.owner == owner)
            {
                gone = entry.Key;
                break;
            }
        }

        if (gone != null)
        {
            Spots.Remove(gone);
        }
    }

    /// <summary>Where <paramref name="who"/> works out in the land at <paramref name="hour"/>, if anywhere.</summary>
    public static bool TryGet(ResidentDefinition who, float hour, out Spot spot)
    {
        if (who != null && Spots.TryGetValue(who, out (Object owner, Spot spot) entry) && entry.owner != null && entry.spot.During(hour))
        {
            spot = entry.spot;
            return true;
        }

        spot = default;
        return false;
    }
}

/// <summary>A worked place's spot for its worker (PlaceWorkers), while the stage that stood it shows.</summary>
[DisallowMultipleComponent]
public sealed class PlaceWorkerSpot : MonoBehaviour
{
    private ResidentDefinition worker;
    private Vector2 hours;
    private Vector3 look;

    /// <summary>Call before it is enabled (on an inactive holder).</summary>
    public void Configure(ResidentDefinition who, Vector2 workHours, Vector3 lookAt)
    {
        worker = who;
        hours = workHours;
        look = lookAt;
    }

    private void OnEnable() => PlaceWorkers.Register(this, worker, new PlaceWorkers.Spot(transform.position, look, hours.x, hours.y));

    private void OnDisable() => PlaceWorkers.Unregister(this);
}
