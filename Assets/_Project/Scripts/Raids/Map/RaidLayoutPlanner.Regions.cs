using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The regions of a large map, and the districts of a normal one (Rules › Normal Regions): each takes the land nearest its centre and
/// its mood (forest, ridges, cliffs, ravines, hills); the start's reaches furthest. Each has a role (Approach, Wild, Compound, Lair) that,
/// with its mood, says how many of each kind of site it holds. Part of RaidLayoutPlanner, in its own file.
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
        AssignRoles(plan);
    }

    // Draws nothing, so roles never move a map. The start's district is the Approach; of the others, the one whose centre lies nearest the
    // apex is the Lair (danger rises toward it), the rest Wild, unless a district's mood asks for a role of its own (a Compound).
    private static void AssignRoles(RaidLayoutPlan plan)
    {
        RaidLayoutRoom apex = plan.Rooms.Find(room => room.Role == RaidRoomRole.Apex);
        int lair = -1;
        float nearest = float.MaxValue;
        for (int i = 1; i < plan.Regions.Count && apex != null; i++)
        {
            float distance = Vector2.Distance(plan.Regions[i].Centre, apex.Position);
            if (distance < nearest)
            {
                nearest = distance;
                lair = i;
            }
        }

        for (int i = 0; i < plan.Regions.Count; i++)
        {
            RaidLayoutRegion region = plan.Regions[i];
            RaidDistrictRole wanted = region.Mood != null ? region.Mood.role : RaidDistrictRole.Any;
            region.Role = i == 0 ? RaidDistrictRole.Approach
                : wanted != RaidDistrictRole.Any ? wanted
                : i == lair ? RaidDistrictRole.Lair
                : RaidDistrictRole.Wild;
        }
    }

    /// <summary>
    /// How a quota of one site kind is shared among the districts (G1): each district's target by its weight (role × mood, Rules ›
    /// District Roles and Mood › Site Density), not by its area; which districts may take any at all; and how many each has. Null when
    /// every district weighs the same, so placement is as it always was.
    /// </summary>
    private sealed class DistrictShare
    {
        public int[] Targets;
        public bool[] Open;
        /// <summary>While true a district takes no more than its target; afterwards (what couldn't fit) any open district may.</summary>
        public bool Strict = true;

        public bool Takes(RaidLayoutPlan plan, RaidSiteKind kind, Vector2 spot)
        {
            int region = plan.RegionIndexAt(spot);
            if (region < 0 || region >= Open.Length || !Open[region])
            {
                return false;
            }

            if (!Strict)
            {
                return true;
            }

            int count = 0;
            foreach (RaidSite site in plan.Sites)
            {
                if (site.Kind == kind && plan.RegionIndexAt(site.Position * plan.Size) == region)
                {
                    count++;
                }
            }

            return count < Targets[region];
        }
    }

    private static DistrictShare ShareOut(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota)
    {
        int count = plan.Regions.Count;
        if (count <= 1)
        {
            return null;
        }

        var weights = new float[count];
        bool even = true;
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            weights[i] = rules.SiteDensity(plan.Regions[i].Role, plan.Regions[i].Mood, quota.kind);
            even &= Mathf.Approximately(weights[i], weights[0]);
            total += weights[i];
        }

        if (even)
        {
            return null;
        }

        // Largest remainders, ties to the earlier district: no draws, so the share is the same on every machine.
        var share = new DistrictShare { Targets = new int[count], Open = new bool[count] };
        var remainders = new float[count];
        int given = 0;
        for (int i = 0; i < count; i++)
        {
            float exact = total > 0f ? quota.count * weights[i] / total : 0f;
            share.Targets[i] = Mathf.FloorToInt(exact);
            share.Open[i] = weights[i] > 0f;
            remainders[i] = exact - share.Targets[i];
            given += share.Targets[i];
        }

        for (int left = total > 0f ? quota.count - given : 0; left > 0; left--)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (share.Open[i] && (best < 0 || remainders[i] > remainders[best]))
                {
                    best = i;
                }
            }

            if (best < 0)
            {
                break;
            }

            share.Targets[best]++;
            remainders[best] = -1f;
        }

        return share;
    }
}
