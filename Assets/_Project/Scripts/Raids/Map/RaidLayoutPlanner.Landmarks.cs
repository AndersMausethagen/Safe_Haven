using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Landmarks seen from afar, set apart from trails, clearings and each other. Part of RaidLayoutPlanner, in its own
/// file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float LandmarkFromTrail = 9f;

    private const float LandmarkFromRoom = 8f;

    private const float LandmarkFromOther = 30f;

    private const int LandmarkTries = 60;

    // Hill tops first, as a landmark is for seeing from afar; then open wild ground, well off the trails.
    private static void PlaceLandmarks(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        if (rules.LandmarkCount.y <= 0 || rules.Landmarks.Count == 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Landmarks " + attempt));
        int wanted = random.Next(rules.LandmarkCount.x, rules.LandmarkCount.y + 1);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        var hills = new List<RaidLayoutHill>(plan.Hills);
        hills.Sort((a, b) => b.Height.CompareTo(a.Height));
        foreach (RaidLayoutHill hill in hills)
        {
            if (plan.Landmarks.Count >= wanted)
            {
                return;
            }

            TryLandmark(plan, hill.Position, halfWidth, random);
        }

        for (int tries = 0; tries < LandmarkTries && plan.Landmarks.Count < wanted; tries++)
        {
            var spot = new Vector2(Range(random, rules.EdgeMargin + rules.RimWidth, plan.Size - rules.EdgeMargin - rules.RimWidth),
                Range(random, rules.EdgeMargin + rules.RimWidth, plan.Size - rules.EdgeMargin - rules.RimWidth));
            TryLandmark(plan, spot, halfWidth, random);
        }
    }

    private static void TryLandmark(RaidLayoutPlan plan, Vector2 spot, float halfWidth, System.Random random)
    {
        if (LineWithin(plan.Trails, spot, halfWidth + LandmarkFromTrail) || LineWithin(plan.Spurs, spot, halfWidth + LandmarkFromTrail) || plan.InRiver(spot, RiverBank + 4f))
        {
            return;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, spot) < room.Radius + LandmarkFromRoom)
            {
                return;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < LandmarkFromRoom)
            {
                return;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + 5f)
            {
                return;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(spot) < 6f)
            {
                return;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < 5f)
            {
                return;
            }
        }

        foreach (RaidLayoutLandmark other in plan.Landmarks)
        {
            if (Vector2.Distance(other.Position, spot) < LandmarkFromOther)
            {
                return;
            }
        }

        plan.Landmarks.Add(new RaidLayoutLandmark { Position = spot, Turn = Range(random, 0f, 360f), Pick = (float)random.NextDouble(), Scale = (float)random.NextDouble() });
    }
}
