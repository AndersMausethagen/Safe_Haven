using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The "Here" readout on the build ghost (Docs/BuildingSystemResearch.md §3.4, step 2): what a spot is near, whether it is lit
/// and guarded, the yard it is in, and for the kind being placed what that spot means: a house's quality and who would move
/// in, a workplace's walk from its worker's home, an extension's station and slot, a path's speed. Plain text with kit icons,
/// worked out only when the ghost moves to another spot. The radii it speaks of are drawn by BuildOverlay.
/// </summary>
public static class BuildHere
{
    private const float NearReach = 20f;
    private const int MostNear = 3;
    private static readonly StringBuilder text = new StringBuilder();
    private static readonly List<(BuildingDefinition kind, float distance)> near = new List<(BuildingDefinition, float)>();
    private static readonly List<Vector2> work = new List<Vector2>();

    /// <summary>The readout for <paramref name="kind"/> standing at <paramref name="point"/> turned by <paramref name="yaw"/>.</summary>
    public static string Describe(BuildingDefinition kind, Vector3 point, float yaw, Bounds local)
    {
        text.Clear();
        if (kind == null)
        {
            return "";
        }

        UIStyle style = UIStyle.Current;
        Line("Info", Near(point, kind));

        // Light and defence: the radii already in the game.
        string light = LitBy(point);
        Line("Sun", light != null ? $"Lit at night ({light})" : "Dark at night: prowlers come close");
        string guard = GuardedBy(point);
        if (guard != null)
        {
            Line("Eye", guard);
        }

        Yards.Yard yard = Yards.At(point);
        if (yard != null)
        {
            Line("Fence", yard.Role != null ? $"In a {yard.Role.DisplayName} (quality {yard.Quality})" : "In a yard with no role yet");
        }

        if (kind.Beds > 0)
        {
            House(kind, point, yaw, local);
        }

        Workplace(kind, point);
        Extension(kind, point);
        if (kind.IsPath)
        {
            Line("Path", $"{(kind.PathSpeed - 1f) * 100f:0}% faster on foot at home" + (kind.Paved ? "; a paved lane for the homes by it" : ""));
        }

        return text.ToString().TrimEnd();
    }

    private static void Line(string icon, string words)
    {
        if (!string.IsNullOrEmpty(words))
        {
            text.Append(HearthKit.InlineIcon(icon)).Append(' ').Append(words).Append('\n');
        }
    }

    // The three nearest kinds of building within reach, each once: "Near the Well 8 m, Campfire 12 m".
    private static string Near(Vector3 point, BuildingDefinition placing)
    {
        near.Clear();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            BuildingDefinition kind = building.Definition;
            if (kind == null || kind.PlaceInLines || !building.IsStanding || !building.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = Flat(building.transform.position - point).magnitude;
            if (distance > NearReach || distance < 0.5f)
            {
                continue;
            }

            int known = near.FindIndex(entry => entry.kind == kind);
            if (known < 0)
            {
                near.Add((kind, distance));
            }
            else if (distance < near[known].distance)
            {
                near[known] = (kind, distance);
            }
        }

        if (near.Count == 0)
        {
            return "Open ground: nothing built within 20 m";
        }

        near.Sort((a, b) => a.distance.CompareTo(b.distance));
        var said = new StringBuilder("Near ");
        for (int i = 0; i < near.Count && i < MostNear; i++)
        {
            said.Append(i > 0 ? ", " : "").Append(near[i].kind.DisplayName).Append(' ').Append(Mathf.RoundToInt(near[i].distance)).Append(" m");
        }

        return said.ToString();
    }

    private static string LitBy(Vector3 point)
    {
        foreach (VisibilitySource source in VisibilitySource.Active)
        {
            if (source == null || !source.IsLight)
            {
                continue;
            }

            BuildingObject lamp = source.GetComponentInParent<BuildingObject>();
            if (lamp != null && lamp.Definition != null && Flat(source.transform.position - point).magnitude <= source.Radius)
            {
                return lamp.Definition.DisplayName;
            }
        }

        return null;
    }

    private static string GuardedBy(Vector3 point)
    {
        foreach (WatchPostArcher post in Object.FindObjectsByType<WatchPostArcher>())
        {
            if (Flat(post.transform.position - point).magnitude <= post.Reach)
            {
                return "Within the Watch Post's bowshot";
            }
        }

        foreach (WardTotem totem in Object.FindObjectsByType<WardTotem>())
        {
            if (Flat(totem.transform.position - point).magnitude <= totem.Radius)
            {
                return "Within a Ward Totem's reach";
            }
        }

        return null;
    }

    // A house: its quality from what is round it, what to add next, and who would move in.
    private static void House(BuildingDefinition kind, Vector3 point, float yaw, Bounds local)
    {
        Vector3 door = point + Quaternion.Euler(0f, yaw, 0f) * new Vector3(local.center.x, 0f, local.max.z + 0.55f);
        List<ResidentRecord> movers = HavenHomes.WouldMoveIn(door, kind.Beds);
        work.Clear();
        var who = new List<string>();
        foreach (ResidentRecord record in movers)
        {
            BuildingObject place = record.Job != null && record.Job.Workplace != null ? BuildingObject.Nearest(record.Job.Workplace, door) : null;
            if (place != null)
            {
                work.Add(new Vector2(place.transform.position.x, place.transform.position.z));
                who.Add($"{record.Resident.DisplayName} ({Mathf.RoundToInt(Flat(place.transform.position - door).magnitude)} m to the {place.Definition.DisplayName})");
            }
            else
            {
                who.Add(record.Resident.DisplayName);
            }
        }

        HomeQuality.Report report = HomeLife.Imagine(door, work);
        var have = new List<string>();
        foreach (HomeQuality.Line line in report.Lines)
        {
            if (line.Met)
            {
                have.Add(line.Name);
            }
        }

        Line("House", $"Home quality {report.Quality} ({report.Level.name})" + (have.Count > 0 ? ": " + string.Join(", ", have) : ""));
        string next = report.Next;
        if (next != null)
        {
            Line("Star", "Better with " + char.ToLowerInvariant(next[0]) + next.Substring(1));
        }

        Line("Bed", who.Count > 0 ? "Would move in: " + string.Join(", ", who) : "Nobody would move in now: everyone has a nearer bed");
    }

    // A workplace: how far its worker walks from home.
    private static void Workplace(BuildingDefinition kind, Vector3 point)
    {
        foreach (ResidentRecord record in GameSession.Settlement.Residents)
        {
            if (record.Job == null || record.Job.Workplace != kind)
            {
                continue;
            }

            BuildingObject home = HavenHomes.HomeOf(record);
            Vector3 from = home != null ? home.Door(out _) : (HomePortal.Current != null ? HomePortal.Current.transform.position : point);
            BuildingObject now = HavenHomes.WorkplaceOf(record, from);
            float walk = Flat(point - from).magnitude;
            string where = home != null ? "their house" : "where they sleep";
            string compared = now != null ? $" (now {Mathf.RoundToInt(Flat(now.transform.position - from).magnitude)} m)" : "";
            Line("Lead", $"{record.Resident.DisplayName}'s walk to work: {Mathf.RoundToInt(walk)} m from {where}{compared}");
        }
    }

    private static void Extension(BuildingDefinition kind, Vector3 point)
    {
        if (kind.Extends == null)
        {
            return;
        }

        BuildingObject station = StationExtensions.Candidate(kind, point, out int used, out int slots);
        if (station == null)
        {
            Line("Gear", $"No {kind.Extends.DisplayName} within {kind.ExtensionReach:0} m: it would stand idle");
            return;
        }

        bool kindTaken = false;
        foreach (BuildingObject other in StationExtensions.Of(station))
        {
            kindTaken |= other.Definition == kind;
        }

        string bonus = kind.Extension.Describe();
        Line("Gear", kindTaken ? $"The {station.Definition.DisplayName} has a {kind.DisplayName} already: it would stand idle"
            : used >= slots ? $"The {station.Definition.DisplayName} has no slot free ({used} of {slots}): upgrade it or fence a Work Yard"
            : $"Extends the {station.Definition.DisplayName}: {bonus} (slot {used + 1} of {slots})");
    }

    private static Vector2 Flat(Vector3 value) => new Vector2(value.x, value.z);
}
