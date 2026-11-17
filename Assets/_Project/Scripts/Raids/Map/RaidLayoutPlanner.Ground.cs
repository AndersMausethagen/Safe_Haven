using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The lie of the land between the trails: hills, boulders and ponds, each kept clear of the clearings, trails and
/// sites. Part of RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const int PondTries = 90;

    private const float PondShrink = 0.86f;

    private const float PondFromTrail = 1.5f;

    private const float PondFromSite = 5f;

    private const float PondFromStart = 18f;

    private const float PondFromPond = 10f;

    private const float HillFromRoom = 0.55f;

    private const float HillSteepest = 0.35f;

    private const float HillFromRamp = 0.85f;

    private const float HillFromTrail = 0.3f;

    private const int HillTries = 40;

    // From a boulder's middle: the largest is about 9 m across (RaidMapGenerator.BoulderWidest).
    private const float BoulderFromTrail = 6.5f;

    private const float BoulderFromRoom = 5.5f;

    private const float BoulderFromSite = 9f;

    private const float BoulderSpread = 6f;

    private const int BoulderTries = 30;

    // An outcrop's hugging rocks (B4): the gap between them and the first rock's side, their scale, and the half width taken for a first
    // rock whose piece can't be told.
    private const float OutcropGapLeast = 0.3f;
    private const float OutcropGapMost = 1.5f;
    private const float OutcropScaleLeast = 0.3f;
    private const float OutcropScaleMost = 0.55f;
    private const float OutcropUnknownHalf = 1.5f;

    // Their tops in the wild ground and off the clearings; trails may climb their outer flanks. From their own stream, so nothing else moves.
    private static void PlaceHills(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        plan.Hilliness = Hilliness(rules, plan.Seed);
        float share = MostOf(plan, mood => mood.hills);
        if (rules.GreatHills.y <= 0 || share <= 0f)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Hills " + attempt));
        int wanted = Mathf.RoundToInt(random.Next(rules.GreatHills.x, rules.GreatHills.y + 1) * share);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        for (int i = 0; i < wanted; i++)
        {
            for (int tries = 0; tries < HillTries; tries++)
            {
                var hill = new RaidLayoutHill
                {
                    Radius = Range(random, rules.GreatHillRadius.x, rules.GreatHillRadius.y),
                    Height = Range(random, rules.GreatHillHeight.x, rules.GreatHillHeight.y) * plan.Hilliness,
                };
                float margin = rules.EdgeMargin;
                hill.Position = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
                // As many as the region's mood wants, as high as it wants, and no steeper than a trail can climb over a flank.
                float here = plan.MoodAt(hill.Position).hills;
                if (random.NextDouble() * share > here)
                {
                    continue;
                }

                hill.Height = Mathf.Min(hill.Height * Mathf.Max(1f, here), hill.Radius * HillSteepest);
                if (HillFits(plan, hill, halfWidth))
                {
                    plan.Hills.Add(hill);
                    break;
                }
            }
        }
    }

    private static bool HillFits(RaidLayoutPlan plan, RaidLayoutHill hill, float halfWidth)
    {
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, hill.Position) < room.Radius + hill.Radius * HillFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, hill.Position) < hill.Radius * HillFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutHill other in plan.Hills)
        {
            if (Vector2.Distance(other.Position, hill.Position) < (other.Radius + hill.Radius) * 0.6f)
            {
                return false;
            }
        }

        // A ramp already climbs: a hill's flank on it as well would make it too steep, so only its gentle outer edge may reach one.
        for (int i = 0; i < plan.Links.Count && i < plan.Trails.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            if (plan.Rooms[a].Level != plan.Rooms[b].Level && NearLine(plan.Trails[i], hill.Position, halfWidth + hill.Radius * HillFromRamp))
            {
                return false;
            }
        }

        return !LineWithin(plan.Trails, hill.Position, halfWidth + hill.Radius * HillFromTrail);
    }

    // Clusters of a few in the wild ground, never on a trail, clearing, site, pond, ravine or spire.
    private static void PlaceBoulders(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        float share = MostOf(plan, mood => mood.boulders);
        if (rules.BoulderClusters.y <= 0 || share <= 0f || rules.BoulderKit is null)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Boulders " + attempt));
        int clusters = Mathf.RoundToInt(random.Next(rules.BoulderClusters.x, rules.BoulderClusters.y + 1) * share);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        for (int i = 0; i < clusters; i++)
        {
            for (int tries = 0; tries < BoulderTries; tries++)
            {
                var centre = new Vector2(Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin), Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin));
                if (random.NextDouble() * share > plan.MoodAt(centre).boulders || !BoulderFits(plan, centre, halfWidth))
                {
                    continue;
                }

                int count = random.Next(rules.BoulderClusterSize.x, rules.BoulderClusterSize.y + 1);
                if (rules.BoulderOutcrops)
                {
                    PlaceOutcrop(plan, rules, random, centre, count, halfWidth);
                    break;
                }

                for (int k = 0; k < count; k++)
                {
                    Vector2 spot = k == 0 ? centre : centre + Direction(Range(random, 0f, 360f)) * Range(random, 2f, BoulderSpread);
                    if (k == 0 || BoulderFits(plan, spot, halfWidth))
                    {
                        plan.Boulders.Add(new RaidLayoutBoulder
                        {
                            Position = spot,
                            Scale = Range(random, rules.BoulderScale.x, rules.BoulderScale.y) * (k == 0 ? 1f : 0.75f),
                            Turn = Range(random, 0f, 360f),
                            Pick = (float)random.NextDouble(),
                        });
                    }
                }

                break;
            }
        }
    }

    // An outcrop (Rules › Boulder Outcrops): the first rock, and the rest hugging it, smaller and set deeper, so a cluster reads as one
    // piece of the land's rock rather than stones set down apart (Docs/WorldLookPlan_2026-11-17.md B4). Every value is drawn whether or
    // not a rock fits, so one that doesn't never shifts the next.
    private static void PlaceOutcrop(RaidLayoutPlan plan, RaidLayoutRules rules, System.Random random, Vector2 centre, int count, float halfWidth)
    {
        var main = new RaidLayoutBoulder
        {
            Position = centre,
            Scale = Range(random, rules.BoulderScale.x, rules.BoulderScale.y),
            Turn = Range(random, 0f, 360f),
            Pick = (float)random.NextDouble(),
        };
        plan.Boulders.Add(main);
        for (int k = 1; k < count; k++)
        {
            Vector2 direction = Direction(Range(random, 0f, 360f));
            float gap = Range(random, OutcropGapLeast, OutcropGapMost);
            var hugging = new RaidLayoutBoulder
            {
                Position = centre + direction * (HugRadius(rules.BoulderKit, main, direction) + gap),
                Scale = Range(random, OutcropScaleLeast, OutcropScaleMost),
                Turn = Range(random, 0f, 360f),
                Pick = (float)random.NextDouble(),
                Outcrop = true,
            };
            if (BoulderFits(plan, hugging.Position, halfWidth))
            {
                plan.Boulders.Add(hugging);
            }
        }
    }

    // How far the first rock reaches from its middle towards a direction: its kit piece's footprint (across its face and its depth, as
    // RaidMapGenerator stands it) at its scale and turn, taken as an ellipse. Picks the piece as RaidMapGenerator.PickByShare does, with
    // plain reference tests only, as the planner may run on a worker thread.
    private static float HugRadius(CliffKit kit, RaidLayoutBoulder main, Vector2 direction)
    {
        CliffKit.Piece piece = PieceByShare(kit, main.Pick);
        if (piece == null)
        {
            return OutcropUnknownHalf * main.Scale;
        }

        float scale = Mathf.Min(main.Scale, RaidMapGenerator.BoulderWidest / Mathf.Max(0.5f, Mathf.Max(piece.size.x, piece.size.z)));
        float across = Mathf.Max(0.1f, piece.size.x * 0.5f * scale), deep = Mathf.Max(0.1f, piece.size.z * 0.5f * scale);
        float turn = main.Turn * Mathf.Deg2Rad;
        // The direction in the turned piece's own frame (a yaw turns +x towards -z in the plan's y).
        float x = direction.x * Mathf.Cos(turn) - direction.y * Mathf.Sin(turn);
        float z = direction.x * Mathf.Sin(turn) + direction.y * Mathf.Cos(turn);
        return 1f / Mathf.Sqrt(x * x / (across * across) + z * z / (deep * deep));
    }

    private static CliffKit.Piece PieceByShare(CliffKit kit, float share)
    {
        if (kit is null)
        {
            return null;
        }

        float total = 0f;
        foreach (CliffKit.Piece piece in kit.Pieces)
        {
            total += !(piece.prefab is null) ? piece.weight : 0f;
        }

        float roll = share * total;
        foreach (CliffKit.Piece piece in kit.Pieces)
        {
            if (piece.prefab is null)
            {
                continue;
            }

            roll -= piece.weight;
            if (roll <= 0f)
            {
                return piece;
            }
        }

        return null;
    }

    /// <summary>
    /// True when a boulder's middle at <paramref name="spot"/> keeps its distance from every clearing, site, pocket, pond, ravine, spire,
    /// trail and river: what may stand solid in the wild ground without blocking a way. Also used for the great blocks at a valley wall's
    /// foot (RaidMapGenerator).
    /// </summary>
    internal static bool BoulderFits(RaidLayoutPlan plan, Vector2 spot, float halfWidth)
    {
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, spot) < room.Radius + BoulderFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < BoulderFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, spot) < radius + BoulderFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < BoulderFromRoom + 1f)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + BoulderFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(spot) < BoulderFromRoom + 2f)
            {
                return false;
            }
        }

        return !LineWithin(plan.Trails, spot, halfWidth + BoulderFromTrail) && !LineWithin(plan.Spurs, spot, halfWidth + BoulderFromTrail)
            && !plan.InRiver(spot, BoulderFromRoom + RiverBank);
    }

    private static void PlacePonds(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        if (rules.Ponds.y <= 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Ponds " + attempt));
        int wanted = random.Next(rules.Ponds.x, rules.Ponds.y + 1);

        // One draw from each slice of the size range, in random order, so a run's ponds never all come out alike.
        List<int> slices = RaidLayout.PickIndices(random, wanted, wanted);
        foreach (int slice in slices)
        {
            float share = (slice + (float)random.NextDouble()) / wanted;
            float radius = Mathf.Lerp(rules.PondRadius.x, rules.PondRadius.y, share);
            float stretch = Range(random, 1f, rules.PondStretch);
            float turn = Range(random, 0f, Mathf.PI * 2f);
            for (int tries = 0; tries < PondTries; tries++)
            {
                // A large pond that finds no room comes out smaller, down to the smallest size.
                if (tries > 0 && tries % 15 == 0)
                {
                    radius = Mathf.Max(rules.PondRadius.x, radius * PondShrink);
                }

                float depth = Mathf.Lerp(rules.PondDepth.x, rules.PondDepth.y, Mathf.InverseLerp(rules.PondRadius.x, rules.PondRadius.y, radius));
                var pond = new RaidLayoutPond { Radii = new Vector2(radius, radius / stretch), Turn = turn, Depth = depth };
                pond.Position = PondSpot(plan, rules, pond, random);
                if (PondFits(plan, rules, pond))
                {
                    plan.Ponds.Add(pond);
                    break;
                }
            }
        }
    }

    private static Vector2 PondSpot(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutPond pond, System.Random random)
    {
        if (plan.Trails.Count > 0 && random.Next(2) == 0)
        {
            List<Vector2> trail = plan.Trails[random.Next(plan.Trails.Count)];
            int at = random.Next(trail.Count - 1);
            Vector2 point = Vector2.Lerp(trail[at], trail[at + 1], (float)random.NextDouble());
            Vector2 side = Vector2.Perpendicular((trail[at + 1] - trail[at]).normalized) * (random.Next(2) == 0 ? -1f : 1f);
            return point + side * (rules.TrailHalfWidth * plan.Mood.trails + pond.Reach + Range(random, PondFromTrail, PondFromTrail + 5f));
        }

        RaidLayoutRoom room = plan.Rooms[random.Next(plan.Rooms.Count)];
        return room.Position + Direction(Range(random, 0f, 360f)) * (room.Radius + pond.Reach * Range(random, 0.2f, 0.9f));
    }

    private static bool PondFits(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutPond pond)
    {
        Vector2 spot = pond.Position;
        float reach = pond.Reach;
        float margin = rules.EdgeMargin + reach;
        if (spot.x < margin || spot.y < margin || spot.x > plan.Size - margin || spot.y > plan.Size - margin
            || Vector2.Distance(spot, plan.Start.Position) < reach + PondFromStart)
        {
            return false;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            // It may lie at the edge of an ordinary clearing, but leaves the ones with a purpose whole.
            float keep = room.Role == RaidRoomRole.Plain ? room.Radius * 0.35f : room.Radius + 1f;
            if (Vector2.Distance(spot, room.Position) < reach + keep)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < reach + PondFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, spot) < reach + radius)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond other in plan.Ponds)
        {
            if (Vector2.Distance(other.Position, spot) < reach + other.Reach + PondFromPond)
            {
                return false;
            }
        }

        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        return !LineWithin(plan.Trails, spot, reach + halfWidth + PondFromTrail)
            && !LineWithin(plan.Spurs, spot, reach + halfWidth + PondFromTrail);
    }
}
