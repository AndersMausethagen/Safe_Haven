using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rivers across the open land: grown step by step around clearings, sites and ravines, crossed squarely by trails at
/// fords. Part of RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float RiverStep = 5f;

    private const float RiverTurn = 7f;

    private const int RiverTries = 60;

    private const float RiverCrossing = 0.57f;

    private const float RiverFromRoom = 3f;

    private const float RiverFromSite = 5f;

    private const float RiverFromRavine = 6f;

    private const float RiverFromRiver = 25f;

    private static void PlaceRivers(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        if (rules.SurfaceRivers.y <= 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Rivers " + attempt));
        float share = 0f;
        foreach (RaidLayoutRegion region in plan.Regions)
        {
            share = Mathf.Max(share, region.Mood.rivers);
        }

        int wanted = Mathf.RoundToInt(random.Next(rules.SurfaceRivers.x, rules.SurfaceRivers.y + 1) * Mathf.Min(1f, share));
        for (int i = 0; i < wanted; i++)
        {
            for (int tries = 0; tries < RiverTries; tries++)
            {
                RaidLayoutRiver river = GrowRiver(plan, rules, random, share);
                if (river != null)
                {
                    plan.Rivers.Add(river);
                    break;
                }
            }
        }
    }

    private static RaidLayoutRiver GrowRiver(RaidLayoutPlan plan, RaidLayoutRules rules, System.Random random, float most)
    {
        float half = Range(random, rules.SurfaceRiverWidth.x, rules.SurfaceRiverWidth.y) * 0.5f;
        float length = Range(random, rules.SurfaceRiverLength.x, rules.SurfaceRiverLength.y);
        var start = new Vector2(Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin), Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin));
        // A river comes up in a region as often as its mood says.
        if (random.NextDouble() * most > plan.MoodAt(start).rivers)
        {
            return null;
        }

        float heading = Range(random, 0f, 360f);
        if (!RiverCanPass(plan, rules, start, Direction(heading), half, out _))
        {
            return null;
        }

        var ahead = new List<Vector2>();
        var behind = new List<Vector2>();
        float grown = 0f;
        float headingAhead = heading, headingBehind = heading + 180f;
        float turnAhead = Range(random, -RiverTurn, RiverTurn), turnBehind = Range(random, -RiverTurn, RiverTurn);
        bool openAhead = true, openBehind = true;
        Vector2 tipAhead = start, tipBehind = start;
        while (grown < length && (openAhead || openBehind))
        {
            if (openAhead)
            {
                openAhead = Step(ref tipAhead, ref headingAhead, ref turnAhead, ahead);
            }

            if (openBehind && grown < length)
            {
                openBehind = Step(ref tipBehind, ref headingBehind, ref turnBehind, behind);
            }
        }

        if (grown < rules.SurfaceRiverLength.x * 0.5f)
        {
            return null;
        }

        var river = new RaidLayoutRiver { Depth = Range(random, rules.SurfaceRiverDepth.x, rules.SurfaceRiverDepth.y) };
        for (int i = behind.Count - 1; i >= 0; i--)
        {
            river.Line.Add(behind[i]);
        }

        river.Line.Add(start);
        river.Line.AddRange(ahead);
        float noiseAt = Range(random, 0f, 500f);
        float run = 0f;
        for (int i = 0; i < river.Line.Count; i++)
        {
            run += i > 0 ? Vector2.Distance(river.Line[i - 1], river.Line[i]) : 0f;
            river.HalfWidths.Add(half * (1f + (Mathf.PerlinNoise(noiseAt + run / 31f, noiseAt) - 0.5f) * 0.5f));
        }

        return river;

        bool Step(ref Vector2 tip, ref float facing, ref float turn, List<Vector2> into)
        {
            turn = Mathf.Clamp(turn + Range(random, -RiverTurn, RiverTurn) * 0.5f, -RiverTurn * 1.5f, RiverTurn * 1.5f);
            float side = random.Next(2) == 0 ? 1f : -1f;
            foreach (float steer in RavineSteers)
            {
                float facingNow = facing + turn + steer * side;
                Vector2 next = tip + Direction(facingNow) * RiverStep;
                if (RiverCanPass(plan, rules, next, Direction(facingNow), half, out bool atEdge))
                {
                    facing = facingNow;
                    into.Add(next);
                    tip = next;
                    grown += RiverStep;
                    return true;
                }

                if (atEdge)
                {
                    // Off the edge of the map: it runs on out of sight, the best end a river can have.
                    return false;
                }
            }

            return false;
        }
    }

    private static bool RiverCanPass(RaidLayoutPlan plan, RaidLayoutRules rules, Vector2 point, Vector2 heading, float half, out bool atEdge)
    {
        float margin = rules.EdgeMargin * 0.5f;
        atEdge = point.x < margin || point.y < margin || point.x > plan.Size - margin || point.y > plan.Size - margin;
        if (atEdge)
        {
            return false;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, point) < room.Radius + half + RiverBank + RiverFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, point) < half + RiverBank + RiverFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, point) < radius + half + RiverBank)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(point) < half + RiverBank + 2f)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(point, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + half + RiverFromRavine)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(point) < half + RiverBank + 2f)
            {
                return false;
            }
        }

        foreach (RaidLayoutRiver other in plan.Rivers)
        {
            other.Nearest(point, out _, out _, out float distance);
            if (distance < half + RiverFromRiver)
            {
                return false;
            }
        }

        // A trail meets it at a ford, so it crosses trails at a fair angle and never runs along one.
        float keep = rules.TrailHalfWidth * plan.Mood.trails + half + 2f;
        foreach (List<Vector2> line in plan.Trails)
        {
            if (!CrossesAtLeast(line, point, heading, keep, RiverCrossing))
            {
                return false;
            }
        }

        foreach (List<Vector2> line in plan.Spurs)
        {
            if (!CrossesAtLeast(line, point, heading, keep, RiverCrossing))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CrossesAtLeast(List<Vector2> line, Vector2 point, Vector2 heading, float keep, float sine)
    {
        float squared = keep * keep;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 along = line[i + 1] - line[i];
            float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
            if ((line[i] + along * t - point).sqrMagnitude >= squared)
            {
                continue;
            }

            Vector2 direction = along.normalized;
            if (Mathf.Abs(heading.x * direction.y - heading.y * direction.x) < sine)
            {
                return false;
            }
        }

        return true;
    }
}
