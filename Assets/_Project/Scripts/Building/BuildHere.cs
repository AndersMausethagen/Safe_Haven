using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The "Here" readout on the build ghost (Docs/BuildingSystemResearch.md §3.4, step 2): what a spot is near, whether it is lit
/// and guarded, the yard it is in, and for the kind being placed what that spot means: a house's quality and who would move
/// in, a workplace's walk from its worker's home, an extension's station and slot, a path's speed. Plain text with kit icons,
/// worked out only when the ghost moves to another spot, and then at most a few times a second (BuildOverlay). The radii it
/// speaks of are drawn by BuildOverlay. What it looks up in the scene (the Watch Posts, Ward Totems and which building each
/// light belongs to) is kept for the placement session (<see cref="BeginSession"/>) and looked up again when a building
/// comes or goes.
/// </summary>
public static class BuildHere
{
    private const float NearReach = 20f;
    private const int MostNear = 3;
    private static readonly StringBuilder text = new StringBuilder();
    private static readonly StringBuilder said = new StringBuilder();
    private static readonly List<(BuildingDefinition kind, float distance)> near = new List<(BuildingDefinition, float)>();
    private static readonly List<Vector2> work = new List<Vector2>();
    private static readonly List<ResidentRecord> movers = new List<ResidentRecord>();
    private static readonly List<string> who = new List<string>();
    private static readonly List<string> have = new List<string>();
    private static readonly List<WatchPostArcher> posts = new List<WatchPostArcher>();
    private static readonly List<WardTotem> totems = new List<WardTotem>();
    private static readonly Dictionary<VisibilitySource, BuildingObject> lampOf = new Dictionary<VisibilitySource, BuildingObject>();
    private static readonly System.Comparison<(BuildingDefinition kind, float distance)> ByDistance = (a, b) => a.distance.CompareTo(b.distance);
    private static bool guardsKnown;
    private static bool listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        text.Clear();
        said.Clear();
        near.Clear();
        work.Clear();
        movers.Clear();
        who.Clear();
        have.Clear();
        Forget();
        listening = false;
    }

    /// <summary>A placement session begins (the overlay shows): what was looked up in the scene is looked up afresh.</summary>
    public static void BeginSession()
    {
        if (!listening)
        {
            // Play sessions don't reload scripts here: off first, so a second session doesn't listen twice.
            listening = true;
            BuildingObject.ActiveChanged -= Forget;
            BuildingObject.ActiveChanged += Forget;
        }

        Forget();
    }

    private static void Forget()
    {
        posts.Clear();
        totems.Clear();
        lampOf.Clear();
        guardsKnown = false;
    }

    /// <summary>The Watch Posts standing now, found once per session (or again when a building comes or goes).</summary>
    public static IReadOnlyList<WatchPostArcher> Posts
    {
        get
        {
            KnowGuards();
            return posts;
        }
    }

    /// <summary>The Ward Totems standing now, found as <see cref="Posts"/>.</summary>
    public static IReadOnlyList<WardTotem> Totems
    {
        get
        {
            KnowGuards();
            return totems;
        }
    }

    private static void KnowGuards()
    {
        if (guardsKnown)
        {
            return;
        }

        guardsKnown = true;
        posts.Clear();
        totems.Clear();
        posts.AddRange(Object.FindObjectsByType<WatchPostArcher>());
        totems.AddRange(Object.FindObjectsByType<WardTotem>());
    }

    /// <summary>The building a light belongs to (null for one that isn't a building's), looked up once per session.</summary>
    public static BuildingObject LampOf(VisibilitySource light)
    {
        if (light == null)
        {
            return null;
        }

        if (!lampOf.TryGetValue(light, out BuildingObject lamp))
        {
            lamp = light.GetComponentInParent<BuildingObject>();
            lampOf[light] = lamp;
        }

        return lamp;
    }

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

            int known = -1;
            for (int i = 0; i < near.Count; i++)
            {
                if (near[i].kind == kind)
                {
                    known = i;
                    break;
                }
            }

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

        near.Sort(ByDistance);
        said.Clear().Append("Near ");
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

            BuildingObject lamp = LampOf(source);
            if (lamp != null && lamp.Definition != null && Flat(source.transform.position - point).magnitude <= source.Radius)
            {
                return lamp.Definition.DisplayName;
            }
        }

        return null;
    }

    private static string GuardedBy(Vector3 point)
    {
        foreach (WatchPostArcher post in Posts)
        {
            if (post != null && Flat(post.transform.position - point).magnitude <= post.Reach)
            {
                return "Within the Watch Post's bowshot";
            }
        }

        foreach (WardTotem totem in Totems)
        {
            if (totem != null && Flat(totem.transform.position - point).magnitude <= totem.Radius)
            {
                return "Within a Ward Totem's reach";
            }
        }

        return null;
    }

    // A house: its quality from what is round it, what to add next, and who would move in.
    private static void House(BuildingDefinition kind, Vector3 point, float yaw, Bounds local)
    {
        // The door on the yard's side, where the house will have it once built (BuildingObject.Door).
        Vector3 door = BuildingObject.DoorAt(point, yaw, local, kind.YardSide, out _);
        HavenHomes.WouldMoveIn(door, kind.Beds, movers);
        work.Clear();
        who.Clear();
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
        have.Clear();
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

        Line("Bed", who.Count > 0 ? "Would move in: " + string.Join(", ", who) : "Nobody would move in now: everyone keeps the bed they have");
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
