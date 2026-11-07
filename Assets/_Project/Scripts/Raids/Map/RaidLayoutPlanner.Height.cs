using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the land is high and low: heights of points on the levels, the apex the run climbs to, sites set over ramps,
/// and the ways down into a ravine. Part of RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float DescentSlope = 2.6f;

    private const float DescentFromTrail = 45f;

    private const float DescentEntry = 3f;

    private const float DescentAfterRamp = 10f;

    private const float DescentSiteGap = 12f;

    private const float DescentShortest = 80f;

    /// <summary>Roughly how high the ground stands at a point before the ridges and roughness: its terrace and the great hills.</summary>
    public static float HeightOf(RaidLayoutPlan plan, Vector2 point)
    {
        float height = 0f;
        foreach (RaidLayoutHill hill in plan.Hills)
        {
            height += hill.RiseAt(point);
        }

        if (plan.HasTerraces)
        {
            RaidLayoutRoom nearest = null;
            float closest = float.MaxValue;
            foreach (RaidLayoutRoom room in plan.Rooms)
            {
                float distance = Vector2.Distance(room.Position, point) - room.Radius;
                if (distance < closest)
                {
                    closest = distance;
                    nearest = room;
                }
            }

            height += nearest != null ? nearest.Level * plan.TerraceHeight : 0f;
        }

        return height;
    }

    // The greatest danger waits on the highest ground of the far third of the map, which a terrace gives a hill fort's single way up.
    private static void ApexByHeight(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        if (!plan.HasTerraces && plan.Hills.Count == 0)
        {
            return;
        }

        RaidLayoutRoom apex = plan.Rooms.Find(room => room.Role == RaidRoomRole.Apex);
        if (apex == null)
        {
            return;
        }

        float far = Vector2.Distance(apex.Position, plan.Start.Position) * 0.67f;
        RaidLayoutRoom best = apex;
        float highest = HeightOf(plan, apex.Position);
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (room.Role == RaidRoomRole.Plain && Vector2.Distance(room.Position, plan.Start.Position) >= far)
            {
                float height = HeightOf(plan, room.Position);
                if (height > highest + 0.5f)
                {
                    highest = height;
                    best = room;
                }
            }
        }

        if (best != apex)
        {
            apex.Role = RaidRoomRole.Plain;
            best.Role = RaidRoomRole.Apex;
            best.Radius = Mathf.Max(best.Radius, rules.RoomRadius.y * 1.2f * plan.Mood.rooms);
        }
    }

    // At the top of each ramp, at the edge of the higher clearing, looking down the way up.
    private static void PlaceOverRamps(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random)
    {
        if (!quota.overlooksRamps || !plan.HasTerraces)
        {
            return;
        }

        foreach (int index in RaidLayout.PickIndices(random, plan.Links.Count, plan.Links.Count))
        {
            (int a, int b) = plan.Links[index];
            RaidLayoutRoom first = plan.Rooms[a], second = plan.Rooms[b];
            if (first.Level == second.Level || CountOf(plan, quota.kind) >= quota.count)
            {
                continue;
            }

            RaidLayoutRoom upper = first.Level > second.Level ? first : second;
            RaidLayoutRoom lower = upper == first ? second : first;
            if (upper.Role != RaidRoomRole.Plain && upper.Role != RaidRoomRole.Hub)
            {
                continue;
            }

            Vector2 spot = upper.Position + (lower.Position - upper.Position).normalized * upper.Radius * 0.7f;
            spot = Inside(spot, plan.Size, rules.EdgeMargin);
            if (CanStand(plan, quota, spot) && PocketFits(plan, rules, spot, quota.clearing))
            {
                AddSite(plan, quota, spot);
            }
        }
    }

    // A long ravine near a trail gets one end sloping down to its floor from a short spur, and a find or two waiting at the bottom.
    private static void PlaceDescents(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        var candidates = plan.Ravines.FindAll(one => one.Mood != null && one.Mood.ravineDescents > 0 && Length(one.Line) >= DescentShortest);
        if (candidates.Count == 0)
        {
            return;
        }

        int wanted = 0;
        foreach (RaidLayoutRavine ravine in candidates)
        {
            wanted = Mathf.Max(wanted, ravine.Mood.ravineDescents);
        }

        candidates.Sort((a, b) => Length(b.Line).CompareTo(Length(a.Line)));
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        int made = 0;
        foreach (RaidLayoutRavine ravine in candidates)
        {
            if (made >= wanted)
            {
                break;
            }

            if (TryDescent(plan, rules, ravine, true, halfWidth) || TryDescent(plan, rules, ravine, false, halfWidth))
            {
                made++;
            }
        }

        if (made > 0)
        {
            RaidMapMarkers.Sort(plan.Sites);
        }
    }

    private static bool TryDescent(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutRavine ravine, bool atStart, float halfWidth)
    {
        int count = ravine.Line.Count;
        Vector2 end = atStart ? ravine.Line[0] : ravine.Line[count - 1];
        Vector2 inward = atStart ? ravine.Line[1] : ravine.Line[count - 2];
        Vector2 outward = (end - inward).normalized;
        float half = (atStart ? ravine.HalfWidths[0] : ravine.HalfWidths[count - 1]) / Mathf.Max(0.01f, atStart ? ravine.Tapers[0] : ravine.Tapers[count - 1]);
        Vector2 entry = end + outward * (half + DescentEntry);
        Vector2 nearest = default;
        float closest = DescentFromTrail;
        foreach (List<Vector2> trail in plan.Trails)
        {
            for (int i = 0; i + 1 < trail.Count; i++)
            {
                Vector2 along = trail[i + 1] - trail[i];
                float t = Mathf.Clamp01(Vector2.Dot(entry - trail[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                Vector2 point = trail[i] + along * t;
                float distance = Vector2.Distance(point, entry);
                if (distance < closest)
                {
                    closest = distance;
                    nearest = point;
                }
            }
        }

        if (closest >= DescentFromTrail)
        {
            return false;
        }

        if (!atStart)
        {
            ravine.Line.Reverse();
            ravine.HalfWidths.Reverse();
            ravine.Tapers.Reverse();
        }

        // Full width at the way in: a ravine narrows towards its closed ends, not its open one.
        for (int i = 0; i < count / 2; i++)
        {
            ravine.HalfWidths[i] /= Mathf.Max(0.01f, ravine.Tapers[i]);
            ravine.Tapers[i] = 1f;
        }

        ravine.Descent = true;
        ravine.River = false;
        plan.Spurs.Add(new List<Vector2> { nearest, entry });

        // The finds stand past the foot of the slope, on the floor.
        float ramp = ravine.Depth * DescentSlope;
        float run = 0f;
        int placed = 0;
        for (int i = 1; i < count && placed < rules.DescentSites.Count; i++)
        {
            run += Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]);
            if (run >= ramp + DescentAfterRamp + placed * DescentSiteGap)
            {
                plan.Sites.Add(new RaidSite(rules.DescentSites[placed], ravine.Line[i] / plan.Size));
                placed++;
            }
        }

        return true;
    }
}
