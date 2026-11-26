using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a Watch Post sees coming (GDD §14.2): some of a raid's events, named in the briefing and drawn first in the raid so it comes true.
/// From the seed alone, so the briefing and the raid agree.
/// </summary>
public static class RaidForecast
{
    /// <summary>
    /// Up to this many events from the raid's pool, or none on a raid without events. Only those the save's marks allow
    /// (<paramref name="hasMark"/>, <see cref="RaidEventDirector.SaveHasMark"/> in the game): never a legend already met, a follow-up
    /// once its mark is held. The briefing and the launch read the same save, so they agree.
    /// </summary>
    public static List<RaidEventDefinition> For(RaidDefinition raid, int seed, int count, System.Func<string, bool> hasMark = null)
    {
        var foretold = new List<RaidEventDefinition>();
        RaidEventsSystem system = raid != null ? raid.GetSystem<RaidEventsSystem>() : null;
        if (system == null || !system.Enabled || count <= 0)
        {
            return foretold;
        }

        RaidEventSettings settings = system.Settings;
        var candidates = new List<RaidEventDefinition>();
        var weights = new List<float>();
        foreach (RaidEventDefinition candidate in settings.pool)
        {
            if (candidate != null && candidate.Weight > 0f && candidate.AllowedBy(hasMark))
            {
                candidates.Add(candidate);
                weights.Add(candidate.Weight);
            }
        }

        var random = new System.Random(RaidLayout.SeedFor(seed, "Forecast"));
        int wanted = Mathf.Min(count, settings.maxPerRaid);
        while (foretold.Count < wanted)
        {
            int index = RaidLayout.PickWeighted(random, weights);
            if (index < 0)
            {
                break;
            }

            foretold.Add(candidates[index]);
            weights[index] = 0f;
        }

        return foretold;
    }
}

/// <summary>How a rumour says where something is: a rough distance and a compass point, or the side it is on once close.</summary>
public static class RumourWay
{
    private const float CloseBy = 25f;
    private const float AWayOff = 70f;
    /// <summary>Size of the squares a rumoured spot is rounded to. Its corners are under 36 m from its centre, inside the 40 m a hidden beacon shows from: go to the mark and it can be seen.</summary>
    private const float RoughCell = 50f;

    /// <summary>How far from <see cref="RoughSpot"/> the thing may be: the square's corner, with a little to spare. The world map
    /// shades this much round a rumour not found yet.</summary>
    public const float RoughRadius = 38f;
    private static readonly string[] Points = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

    public static string Describe(Vector3 offset)
    {
        offset.y = 0f;
        float distance = offset.magnitude;
        if (distance < CloseBy)
        {
            return "close by";
        }

        return (distance < AWayOff ? "a way off, " : "far off, ") + CompassPoint(offset);
    }

    /// <summary>Where a rumour's mark goes on a map: the middle of the square it is in, so the mark doesn't give the spot away and doesn't move.</summary>
    public static Vector3 RoughSpot(Vector3 position)
    {
        return new Vector3((Mathf.Floor(position.x / RoughCell) + 0.5f) * RoughCell, position.y, (Mathf.Floor(position.z / RoughCell) + 0.5f) * RoughCell);
    }

    /// <summary>The nearest of eight compass points. North is +Z, as on the minimap.</summary>
    public static string CompassPoint(Vector3 offset)
    {
        float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        int index = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % Points.Length;
        return Points[index];
    }
}
