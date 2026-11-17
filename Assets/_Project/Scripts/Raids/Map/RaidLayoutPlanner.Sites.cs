using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Placing the sites where each kind belongs (in clearings or along trails, apart from each other) and the set pieces,
/// including the ones a rumour promised. Part of RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float MinimumFromOtherSites = 7f;

    private const int SiteTries = 60;

    // From their own stream, so adding water never moves a site. They go last and give way to everything already on the map:
    // beside a trail or at the edge of a clearing, so the player comes across them, and never where a trail would run through.
    // From their own stream, so a set-piece added to the rules never moves a site. Rumours first: a reward should stand somewhere.
    private static void PlaceSetPieces(RaidLayoutPlan plan, RaidLayoutRules rules, int seed, int attempt)
    {
        if (rules.SetPieces.Count == 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(seed, "Set Pieces " + attempt));
        var used = new List<RaidSetPieceDefinition>();
        // What is always there goes farthest from the apex: a boss's hollow shouldn't be on the great troll's round.
        List<RaidSite> rumours = plan.SitesOf(RaidSiteKind.Rumour);
        List<RaidSite> apex = plan.SitesOf(RaidSiteKind.Apex);
        if (apex.Count > 0)
        {
            Vector2 from = apex[0].Position;
            rumours.Sort((a, b) => Vector2.Distance(b.Position, from).CompareTo(Vector2.Distance(a.Position, from)));
        }

        foreach (RaidSite site in rumours)
        {
            AddPiece(plan, rules, site, true, used, random);
        }

        List<RaidSite> camps = plan.SitesOf(RaidSiteKind.EnemyCamp);
        int wanted = random.Next(rules.CampPieces.x, rules.CampPieces.y + 1);
        foreach (int index in RaidLayout.PickIndices(random, camps.Count, Mathf.Min(wanted, camps.Count)))
        {
            AddPiece(plan, rules, camps[index], false, used, random);
        }
    }

    private static void AddPiece(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSite site, bool forRumour, List<RaidSetPieceDefinition> used, System.Random random)
    {
        RaidSetPieceDefinition piece = PickPiece(rules, plan.Mood != null ? plan.Mood.name : "", forRumour, used, random);
        if (piece is null)
        {
            return;
        }

        Vector2 spot = site.Position * plan.Size;
        if (!PocketFits(plan, rules, spot, piece.Radius))
        {
            return;
        }

        used.Add(piece);
        plan.Pieces.Add(new RaidLayoutPiece { Definition = piece, Position = spot, Turn = Range(random, 0f, 360f) });
        plan.Pockets.Add((spot, piece.Radius));
    }

    // By weight, and one not seen on this map yet while there is one.
    private static RaidSetPieceDefinition PickPiece(RaidLayoutRules rules, string mood, bool forRumour, List<RaidSetPieceDefinition> used, System.Random random)
    {
        var fresh = new List<RaidSetPieceDefinition>();
        var any = new List<RaidSetPieceDefinition>();
        foreach (RaidSetPieceDefinition piece in rules.SetPieces)
        {
            // Reference checks only: a run can be laid out off the main thread, where Unity's own null check can't run.
            if (piece is null || piece.Prefab is null || piece.Weight <= 0f || !piece.Suits(mood) || (forRumour && !piece.HoldsRumours))
            {
                continue;
            }

            if (forRumour && piece.AlwaysThere && !used.Contains(piece))
            {
                return piece;
            }

            any.Add(piece);
            if (!used.Contains(piece))
            {
                fresh.Add(piece);
            }
        }

        List<RaidSetPieceDefinition> from = fresh.Count > 0 ? fresh : any;
        float total = 0f;
        foreach (RaidSetPieceDefinition piece in from)
        {
            total += piece.Weight;
        }

        float roll = (float)random.NextDouble() * total;
        foreach (RaidSetPieceDefinition piece in from)
        {
            roll -= piece.Weight;
            if (roll <= 0f)
            {
                return piece;
            }
        }

        return from.Count > 0 ? from[from.Count - 1] : null;
    }

    // With districts that weigh differently (G1), each takes up to its share first; what found no room in its own district then goes to
    // any district that takes the kind at all, so the map's count holds where it can.
    private static void PlaceSites(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random)
    {
        DistrictShare districts = ShareOut(plan, rules, quota);
        if (quota.placement == RaidSitePlacement.Room || quota.placement == RaidSitePlacement.DeadEnd)
        {
            PlaceInRooms(plan, rules, quota, random, districts);
        }

        for (int tries = 0; tries < SiteTries * quota.count && CountOf(plan, quota.kind) < quota.count; tries++)
        {
            TryAlongTrail(plan, rules, quota, random, districts);
        }

        if (districts == null || CountOf(plan, quota.kind) >= quota.count)
        {
            return;
        }

        districts.Strict = false;
        for (int tries = 0; tries < SiteTries * quota.count && CountOf(plan, quota.kind) < quota.count; tries++)
        {
            TryAlongTrail(plan, rules, quota, random, districts);
        }
    }

    private static int CountOf(RaidLayoutPlan plan, RaidSiteKind kind)
    {
        int count = 0;
        foreach (RaidSite site in plan.Sites)
        {
            count += site.Kind == kind ? 1 : 0;
        }

        return count;
    }

    private static void PlaceInRooms(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random, DistrictShare districts)
    {
        PlaceOverRamps(plan, rules, quota, random);
        // Dead ends first, or only them; what stays unplaced finds a pocket along a trail. Camps take the short way to the exit first and
        // the long way last; what prefers height takes the highest clearings first.
        List<int> order = RaidLayout.PickIndices(random, plan.Rooms.Count, plan.Rooms.Count);
        if (quota.kind == RaidSiteKind.EnemyCamp && rules.CampsOnShortRoute && plan.ShortRoute.Count > 0)
        {
            HashSet<int> watched = RoomsOf(plan, plan.ShortRoute);
            HashSet<int> quiet = RoomsOf(plan, plan.LongRoute);
            quiet.ExceptWith(watched);
            order.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            int Rank(int room) => watched.Contains(room) ? 0 : quiet.Contains(room) ? 2 : 1;
        }
        else if (quota.prefersHeight)
        {
            var heights = new float[plan.Rooms.Count];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = HeightOf(plan, plan.Rooms[i].Position);
            }

            order.Sort((a, b) => heights[b].CompareTo(heights[a]));
        }

        foreach (int index in order)
        {
            RaidLayoutRoom room = plan.Rooms[index];
            bool fits = room.Role == RaidRoomRole.Plain || room.Role == RaidRoomRole.Hub;
            if (!fits || (quota.placement == RaidSitePlacement.DeadEnd && room.Links > 1) || CountOf(plan, quota.kind) >= quota.count)
            {
                continue;
            }

            Vector2 spot = room.Position + Direction(Range(random, 0f, 360f)) * (room.Radius * Range(random, 0f, 0.5f));
            spot = Inside(spot, plan.Size, rules.EdgeMargin);
            if (CanStand(plan, quota, spot) && PocketFits(plan, rules, spot, quota.clearing) && (districts == null || districts.Takes(plan, quota.kind, spot)))
            {
                AddSite(plan, quota, spot);
            }
        }
    }

    private static void TryAlongTrail(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random, DistrictShare districts)
    {
        if (plan.Trails.Count == 0)
        {
            return;
        }

        List<Vector2> trail = plan.Trails[random.Next(plan.Trails.Count)];
        int at = random.Next(trail.Count - 1);
        Vector2 point = Vector2.Lerp(trail[at], trail[at + 1], (float)random.NextDouble());
        // Camps beside the short way to the exit, half the time; what prefers height takes the highest of a few tries.
        if (quota.kind == RaidSiteKind.EnemyCamp && rules.CampsOnShortRoute && plan.ShortRoute.Count > 0 && random.Next(2) == 0)
        {
            var watched = new List<int>(plan.ShortRoute);
            trail = plan.Trails[watched[random.Next(watched.Count)]];
            at = random.Next(trail.Count - 1);
            point = Vector2.Lerp(trail[at], trail[at + 1], (float)random.NextDouble());
        }
        else if (quota.prefersHeight)
        {
            for (int tries = 0; tries < 3; tries++)
            {
                List<Vector2> other = plan.Trails[random.Next(plan.Trails.Count)];
                int otherAt = random.Next(other.Count - 1);
                Vector2 candidate = Vector2.Lerp(other[otherAt], other[otherAt + 1], (float)random.NextDouble());
                if (HeightOf(plan, candidate) > HeightOf(plan, point))
                {
                    trail = other;
                    at = otherAt;
                    point = candidate;
                }
            }
        }

        if (OnRamp(plan, trail))
        {
            return;
        }

        if (quota.placement == RaidSitePlacement.OnPath)
        {
            if (CanStand(plan, quota, point) && PocketFits(plan, rules, point, quota.clearing) && (districts == null || districts.Takes(plan, quota.kind, point)))
            {
                AddSite(plan, quota, point);
            }

            return;
        }

        Vector2 side = Vector2.Perpendicular((trail[at + 1] - trail[at]).normalized) * (random.Next(2) == 0 ? -1f : 1f);
        Vector2 spot = point + side * Range(random, quota.offPath.x, quota.offPath.y);
        if (plan.Valley != null && plan.Valley.BankOf(spot) != plan.Valley.BankOf(point))
        {
            return; // a side trail never wades the valley's river
        }

        if (spot.x < rules.EdgeMargin || spot.y < rules.EdgeMargin || spot.x > plan.Size - rules.EdgeMargin || spot.y > plan.Size - rules.EdgeMargin
            || !CanStand(plan, quota, spot) || !PocketFits(plan, rules, spot, quota.clearing) || (districts != null && !districts.Takes(plan, quota.kind, spot)))
        {
            return;
        }

        plan.Spurs.Add(new List<Vector2> { point, spot });
        AddSite(plan, quota, spot);
    }

    private static bool CanStand(RaidLayoutPlan plan, RaidSiteQuota quota, Vector2 spot)
    {
        if (Vector2.Distance(spot, plan.Start.Position) < quota.fromStart)
        {
            return false;
        }

        foreach (RaidSite site in plan.Sites)
        {
            float keep = site.Kind == quota.kind ? quota.separation : MinimumFromOtherSites;
            if ((site.Position * plan.Size - spot).sqrMagnitude < keep * keep)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddSite(RaidLayoutPlan plan, RaidSiteQuota quota, Vector2 spot)
    {
        plan.Sites.Add(new RaidSite(quota.kind, spot / plan.Size));
        if (quota.clearing > 0f)
        {
            plan.Pockets.Add((spot, quota.clearing));
        }
    }
}
