using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Station extensions (Docs/BuildingSystemResearch.md §3.4.3, after Valheim's forge and RimWorld's tool cabinet): a
/// Bellows, Quench Tub or Tool Rack placed by the Smithy (within its Extension Reach, or anywhere in the same Work Yard)
/// adds its bonus there. Each station takes a few (BuildingDefinition › Extension Slots, more with upgrades and in a Work
/// Yard), each kind once, so the player chooses what the forge is good at. An extension counts for the nearest station it
/// can reach; ones beyond the slots stand idle and say so. Worked out again when something is built, moved or finished,
/// never per frame. Reads the loaded Haven.
/// </summary>
public static class StationExtensions
{
    private static readonly Dictionary<BuildingObject, List<BuildingObject>> served = new Dictionary<BuildingObject, List<BuildingObject>>();
    private static readonly Dictionary<BuildingObject, BuildingObject> serving = new Dictionary<BuildingObject, BuildingObject>();
    private static readonly List<BuildingObject> empty = new List<BuildingObject>();
    private static bool dirty = true;
    private static bool listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        served.Clear();
        serving.Clear();
        empty.Clear();
        dirty = true;
        listening = false;
    }

    private static void Listen()
    {
        if (listening)
        {
            return;
        }

        // Play sessions don't reload scripts here: off first, so a second session doesn't listen twice.
        listening = true;
        BuildingObject.ActiveChanged -= Forget;
        Construction.Finished -= OnFinished;
        Yards.Changed -= Forget;
        BuildingObject.ActiveChanged += Forget;
        Construction.Finished += OnFinished;
        Yards.Changed += Forget;
    }

    private static void OnFinished(Construction _) => Forget();

    /// <summary>Works it out again on the next question.</summary>
    public static void Forget() => dirty = true;

    /// <summary>A bonus added up over the extensions in use at this station.</summary>
    public static float Bonus(BuildingObject station, Func<ExtensionBonus, float> part)
    {
        float total = 0f;
        foreach (BuildingObject extension in Of(station))
        {
            total += part(extension.Definition.Extension);
        }

        return total;
    }

    /// <summary>The extensions in use at this station, each kind once, up to its slots.</summary>
    public static IReadOnlyList<BuildingObject> Of(BuildingObject station)
    {
        Settle();
        return station != null && served.TryGetValue(station, out List<BuildingObject> found) ? found : empty;
    }

    /// <summary>The station this extension works for, or null when it stands idle (none in reach, or no slot free).</summary>
    public static BuildingObject StationOf(BuildingObject extension)
    {
        Settle();
        return extension != null && serving.TryGetValue(extension, out BuildingObject station) ? station : null;
    }

    /// <summary>How many extensions this station takes: its own, its upgrades' and its Work Yard's.</summary>
    public static int Slots(BuildingObject station)
    {
        if (station == null || station.Definition == null || !station.IsStanding)
        {
            return 0;
        }

        int slots = station.Definition.ExtensionSlots;
        if (station.TryGetComponent(out Construction construction))
        {
            slots += Mathf.RoundToInt(construction.Sum(level => level.extensionSlots));
        }

        Yards.Yard yard = Yards.Of(station);
        return slots > 0 && yard != null && yard.Role != null ? slots + yard.Role.ExtensionSlots : slots;
    }

    /// <summary>
    /// For the build ghost: the station an extension of this kind would serve if it stood at <paramref name="at"/>, how
    /// many it already has and takes. Null when none is in reach.
    /// </summary>
    public static BuildingObject Candidate(BuildingDefinition kind, Vector3 at, out int used, out int slots)
    {
        used = 0;
        slots = 0;
        BuildingObject station = kind != null ? Reachable(kind, at, null) : null;
        if (station != null)
        {
            used = Of(station).Count;
            slots = Slots(station);
        }

        return station;
    }

    private static void Settle()
    {
        Listen();
        if (!dirty)
        {
            return;
        }

        dirty = false;
        served.Clear();
        serving.Clear();
        var waiting = new Dictionary<BuildingObject, List<BuildingObject>>();
        foreach (BuildingObject extension in BuildingObject.Active)
        {
            BuildingDefinition kind = extension.Definition;
            if (kind == null || kind.Extends == null || !extension.IsStanding)
            {
                continue;
            }

            BuildingObject station = Reachable(kind, extension.transform.position, extension);
            if (station == null)
            {
                continue;
            }

            if (!waiting.TryGetValue(station, out List<BuildingObject> list))
            {
                waiting[station] = list = new List<BuildingObject>();
            }

            list.Add(extension);
        }

        foreach (KeyValuePair<BuildingObject, List<BuildingObject>> entry in waiting)
        {
            // In the order they were placed (ids are stable and the same on every machine): first come, first served.
            entry.Value.Sort((a, b) => string.CompareOrdinal(a.Id ?? a.name, b.Id ?? b.name));
            int slots = Slots(entry.Key);
            var used = new List<BuildingObject>();
            foreach (BuildingObject extension in entry.Value)
            {
                bool kindTaken = used.Exists(other => other.Definition == extension.Definition);
                if (used.Count < slots && !kindTaken)
                {
                    used.Add(extension);
                    serving[extension] = entry.Key;
                }
            }

            served[entry.Key] = used;
        }
    }

    // The nearest standing station of the kind it extends within its reach (from the station's walls), or in its Work Yard.
    private static BuildingObject Reachable(BuildingDefinition kind, Vector3 at, BuildingObject self)
    {
        BuildingObject best = null;
        float bestDistance = float.MaxValue;
        Yards.Yard yard = self != null ? Yards.Of(self) : Yards.At(at);
        foreach (BuildingObject station in BuildingObject.Active)
        {
            if (station.Definition != kind.Extends || !station.IsStanding)
            {
                continue;
            }

            FootprintRect box = station.Footprint;
            Vector2 offset = new Vector2(at.x, at.z) - box.Center;
            float dx = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(offset, box.AxisX)) - box.HalfSize.x);
            float dz = Mathf.Max(0f, Mathf.Abs(Vector2.Dot(offset, box.AxisZ)) - box.HalfSize.y);
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            bool sameYard = yard != null && yard.Role != null && yard.Role.ExtensionSlots > 0 && yard.Holds(station);
            if ((distance <= kind.ExtensionReach || sameYard) && distance < bestDistance)
            {
                best = station;
                bestDistance = distance;
            }
        }

        return best;
    }
}
