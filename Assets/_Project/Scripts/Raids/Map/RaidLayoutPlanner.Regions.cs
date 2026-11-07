using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The regions of a large map: each takes the land nearest its centre and its mood (forest, ridges, cliffs, ravines,
/// hills); the start's reaches furthest. Part of RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float StartRegionReach = 1.4f;

    private const float RegionFromEdge = 0.2f;

    private const int RegionTries = 40;

    /// <summary>
    /// The moods of a run's regions, the run's own first: fixed by the seed, so the briefing can name them cheaply. A map of the normal size
    /// (or rules without regions) has one.
    /// </summary>
    public static List<RaidMapMood> RegionMoods(RaidLayoutRules rules, int seed) => RegionMoods(rules, seed, rules.Regions);

    /// <summary>The moods of a run's regions for this many regions, least and most (a briefing asks with the large map's range).</summary>
    public static List<RaidMapMood> RegionMoods(RaidLayoutRules rules, int seed, Vector2Int range)
    {
        (RaidShapeRule _, RaidMapMood first) = Choose(rules, seed);
        var moods = new List<RaidMapMood> { first };
        var random = new System.Random(RaidLayout.SeedFor(seed, "Region Moods"));
        int count = random.Next(Mathf.Max(1, range.x), Mathf.Max(1, range.y) + 1);
        while (moods.Count < count)
        {
            var weights = new List<float>();
            foreach (RaidMapMood mood in rules.Moods)
            {
                weights.Add(moods.Exists(one => one.name == mood.name) ? 0f : mood.weight);
            }

            int pick = RaidLayout.PickWeighted(random, weights);
            if (pick < 0)
            {
                break;
            }

            moods.Add(rules.Moods[pick]);
        }

        return moods;
    }

    // The start's region around the start, the others as far from it and from each other as the map allows.
    private static void PlaceRegions(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        List<RaidMapMood> moods = RegionMoods(rules, plan.Seed);
        // The moods come from the same list (copies, on a larger map); the run's own stays the plan's Mood.
        moods[0] = plan.Mood;
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Regions " + attempt));
        var centre = new Vector2(plan.Size * 0.5f, plan.Size * 0.5f);
        plan.Regions.Add(new RaidLayoutRegion { Centre = Vector2.Lerp(plan.Start.Position, centre, 0.3f), Mood = moods[0], Reach = StartRegionReach });
        float margin = plan.Size * RegionFromEdge;
        for (int i = 1; i < moods.Count; i++)
        {
            Vector2 best = centre;
            float bestDistance = -1f;
            for (int tries = 0; tries < RegionTries; tries++)
            {
                var spot = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
                float nearest = float.MaxValue;
                foreach (RaidLayoutRegion region in plan.Regions)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(region.Centre, spot));
                }

                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = spot;
                }
            }

            plan.Regions.Add(new RaidLayoutRegion { Centre = best, Mood = moods[i] });
        }

        plan.RegionBlend = rules.RegionBlend;
    }
}
