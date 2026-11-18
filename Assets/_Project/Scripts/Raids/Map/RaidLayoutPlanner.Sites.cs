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

        PlaceCompounds(plan, rules, seed, attempt, used);
    }

    // A compound keeps this far from the start's middle, so the first steps are never into a walled yard.
    private const float CompoundFromStart = 45f;

    // A compound keeps this far (metres beyond its own Radius) from any site already in its clearing, so a camp, cache or story site
    // never ends up inside its ruin (review 2026-11-18).
    private const float CompoundFromSite = 2f;

    // Metres beyond the ring kept clear for it as a pocket.
    private const float CompoundRingKeep = 1.5f;

    private static bool HoldsASite(RaidLayoutPlan plan, Vector2 middle, float keep)
    {
        foreach (RaidSite site in plan.Sites)
        {
            if (site.Kind != RaidSiteKind.None && (site.Position * plan.Size - middle).sqrMagnitude < keep * keep)
            {
                return true;
            }
        }

        return false;
    }

    // A compound's wall stands round at least this share of its ring, or the clearing isn't one for a compound.
    private const float CompoundLeastWall = 0.45f;

    // Extra openings keep at least this far (degrees) from every way in.
    private const float ExtraOpeningApart = 50f;

    /// <summary>
    /// Compounds (Docs/WorldDesignReview_2026-11-17.md G3): set pieces that take a whole clearing (Owns Clearing), a plain clearing or hub
    /// with room for the piece inside its ring, away from the start and from any piece already placed. Its ring gets an opening wherever a
    /// trail or a side path crosses it (<see cref="CompoundOpenings"/>), so whatever the clearing holds (a camp, a cache) stays reachable
    /// and its ways in become chokepoints. From their own stream, after everything else in a clearing, so turning them on moves nothing.
    /// </summary>
    private static void PlaceCompounds(RaidLayoutPlan plan, RaidLayoutRules rules, int seed, int attempt, List<RaidSetPieceDefinition> used)
    {
        if (rules.Compounds.y <= 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(seed, "Compounds " + attempt));
        int wanted = random.Next(rules.Compounds.x, rules.Compounds.y + 1);
        // The clearings with the fewest ways in first: a yard with one or two gates is a chokepoint, a hub with four is a sieve.
        List<int> shuffled = RaidLayout.PickIndices(random, plan.Rooms.Count, plan.Rooms.Count);
        var order = new List<int>(shuffled);
        order.Sort((a, b) => plan.Rooms[a].Links != plan.Rooms[b].Links ? plan.Rooms[a].Links.CompareTo(plan.Rooms[b].Links)
            : shuffled.IndexOf(a).CompareTo(shuffled.IndexOf(b)));
        for (int placed = 0; placed < wanted; placed++)
        {
            RaidSetPieceDefinition piece = PickCompound(rules, plan, used, random);
            if (piece is null)
            {
                return;
            }

            bool found = false;
            foreach (int index in order)
            {
                RaidLayoutRoom room = plan.Rooms[index];
                float ring = room.Radius * piece.WallAt;
                if ((room.Role != RaidRoomRole.Plain && room.Role != RaidRoomRole.Hub) || ring < piece.Radius + 1.5f
                    || Vector2.Distance(room.Position, plan.Start.Position) < CompoundFromStart || TakenByAPiece(plan, room.Position, ring)
                    || HoldsASite(plan, room.Position, piece.Radius + CompoundFromSite))
                {
                    continue;
                }

                var compound = new RaidLayoutPiece { Definition = piece, Position = room.Position, Turn = Range(random, 0f, 360f), WallRadius = ring };
                CompoundOpenings(plan, room.Position, ring, piece.ExtraOpenings, random, compound.Openings);
                CompoundWall(plan, rules, compound);
                // A hub with many ways in leaves stubs of wall, not a yard: such a clearing is passed over.
                int whole = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * ring / piece.PieceLength));
                if (compound.Wall.Count < whole * CompoundLeastWall)
                {
                    continue;
                }

                plan.Pieces.Add(compound);
                // The yard and its ring, so what is placed after (boulders, ponds, landmarks) keeps off the wall.
                plan.Pockets.Add((room.Position, ring + CompoundRingKeep));
                found = true;
                break;
            }

            // No clearing has room (Troll Valley's are small and full): a yard of its own just off a trail, a side path to its one gate.
            if (!found && !YardOffATrail(plan, rules, piece, random))
            {
                return;
            }

            used.Add(piece);
        }
    }

    // A yard off a trail: its ring this far round (metres), its middle this far beyond the trail's edge, and so many tries.
    private const float YardRing = 9.5f;
    private const float YardFromTrail = 3f;
    private const int YardTries = 80;

    /// <summary>
    /// A compound standing in a yard of its own beside a trail (review 2026-11-18: no Valley clearing had room once its sites and trails
    /// were kept clear): its middle Yard Ring + the trail's half width + Yard From Trail off a trail's side, clear of every other trail,
    /// clearing, site and piece, on the trail's own bank of a valley's river, joined to the trail by a side path through its gate. False
    /// when no try fits.
    /// </summary>
    private static bool YardOffATrail(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSetPieceDefinition piece, System.Random random)
    {
        if (plan.Trails.Count == 0)
        {
            return false;
        }

        float ring = Mathf.Max(YardRing, piece.Radius + 1.5f);
        float half = rules.TrailHalfWidth * (plan.Mood != null ? plan.Mood.trails : 1f);
        float margin = rules.EdgeMargin + ring + CompoundRingKeep;
        for (int tries = 0; tries < YardTries; tries++)
        {
            List<Vector2> trail = plan.Trails[random.Next(plan.Trails.Count)];
            if (trail.Count < 2 || OnRamp(plan, trail))
            {
                continue;
            }

            int at = random.Next(trail.Count - 1);
            Vector2 point = Vector2.Lerp(trail[at], trail[at + 1], Range(random, 0.2f, 0.8f));
            Vector2 side = Vector2.Perpendicular((trail[at + 1] - trail[at]).normalized) * (random.Next(2) == 0 ? -1f : 1f);
            Vector2 spot = point + side * (ring + half + YardFromTrail);
            if (spot.x < margin || spot.y < margin || spot.x > plan.Size - margin || spot.y > plan.Size - margin
                || Vector2.Distance(spot, plan.Start.Position) < CompoundFromStart
                || (plan.Valley != null && plan.Valley.BankOf(spot) != plan.Valley.BankOf(point))
                || LineWithin(plan.Trails, spot, ring + half + 1f) || LineWithin(plan.Spurs, spot, ring + half)
                || plan.InRiver(spot, ring + CompoundRingKeep) || plan.InWater(spot) || TakenByAPiece(plan, spot, ring)
                || HoldsASite(plan, spot, ring + CompoundFromSite) || !PocketFits(plan, rules, spot, ring + CompoundRingKeep))
            {
                continue;
            }

            bool clear = true;
            foreach (RaidLayoutRoom room in plan.Rooms)
            {
                clear &= Vector2.Distance(room.Position, spot) >= room.Radius + ring + CompoundRingKeep;
            }

            if (!clear)
            {
                continue;
            }

            plan.Spurs.Add(new List<Vector2> { point, spot });
            var compound = new RaidLayoutPiece { Definition = piece, Position = spot, Turn = Range(random, 0f, 360f), WallRadius = ring };
            CompoundOpenings(plan, spot, ring, piece.ExtraOpenings, random, compound.Openings);
            CompoundWall(plan, rules, compound);
            plan.Pieces.Add(compound);
            plan.Pockets.Add((spot, ring + CompoundRingKeep));
            return true;
        }

        return false;
    }

    private static bool TakenByAPiece(RaidLayoutPlan plan, Vector2 middle, float ring)
    {
        foreach (RaidLayoutPiece other in plan.Pieces)
        {
            float keep = ring + Mathf.Max(other.WallRadius, other.Definition is null ? 0f : other.Definition.Radius);
            if ((other.Position - middle).sqrMagnitude < keep * keep)
            {
                return true;
            }
        }

        return false;
    }

    // By weight among the compounds that suit the mood, one not on this map yet first; never one whose model already stands on the map
    // (a compound made from a rumour's set piece, the Ruined Seter as the Seter Yard, would show the same ruin twice).
    private static RaidSetPieceDefinition PickCompound(RaidLayoutRules rules, RaidLayoutPlan plan, List<RaidSetPieceDefinition> used, System.Random random)
    {
        string mood = plan.Mood != null ? plan.Mood.name : "";
        var fresh = new List<RaidSetPieceDefinition>();
        var any = new List<RaidSetPieceDefinition>();
        foreach (RaidSetPieceDefinition piece in rules.SetPieces)
        {
            if (piece is null || piece.Prefab is null || !piece.OwnsClearing || piece.Weight <= 0f || !piece.Suits(mood) || piece.WallPieces.Count == 0
                || plan.Pieces.Exists(placed => ReferenceEquals(placed.Definition.Prefab, piece.Prefab)))
            {
                continue;
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

    /// <summary>
    /// Where a compound's ring of <paramref name="ring"/> metres about <paramref name="middle"/> must stay open: every place a trail or a
    /// side path crosses it (in degrees, as Atan2 on the map), then <paramref name="extra"/> more, each well away from those. Into
    /// <paramref name="into"/>, cleared first.
    /// </summary>
    public static void CompoundOpenings(RaidLayoutPlan plan, Vector2 middle, float ring, int extra, System.Random random, List<float> into)
    {
        into.Clear();
        foreach (List<Vector2> trail in plan.Trails)
        {
            Crossings(trail, middle, ring, into);
        }

        foreach (List<Vector2> spur in plan.Spurs)
        {
            Crossings(spur, middle, ring, into);
        }

        int ways = into.Count;
        for (int i = 0, tries = 0; i < extra && tries < 40; tries++)
        {
            float angle = Range(random, -180f, 180f);
            bool apart = true;
            foreach (float other in into)
            {
                apart &= Mathf.Abs(Mathf.DeltaAngle(angle, other)) >= ExtraOpeningApart;
            }

            if (apart || (ways == 0 && into.Count == 0))
            {
                into.Add(angle);
                i++;
            }
        }
    }

    // A wall piece keeps this far (metres) beyond a trail's or side path's edge, its own half depth included: a trail that meets the ring
    // at a slant runs along it for a while (review 2026-11-18: measured from the trail's middle, pieces stood on the path).
    private const float WallBeyondTrail = 1f;

    // A side path's width as a share of a trail's (RaidLayoutField.SpurWidth).
    private const float SpurShare = 0.6f;

    /// <summary>How far a compound's wall piece keeps from a trail's middle (its half width in this mood, plus a margin) and from a side path's.</summary>
    public static (float trail, float spur) WallClearance(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        float half = rules.TrailHalfWidth * (plan.Mood != null ? plan.Mood.trails : 1f);
        return (half + WallBeyondTrail, half * SpurShare + WallBeyondTrail);
    }

    /// <summary>
    /// Where a compound's wall pieces stand (<see cref="RaidLayoutPiece.Wall"/>): evenly round its ring, one every Piece Length, leaving
    /// out each that comes within half the Opening Width of an opening or within <see cref="WallClearance"/> of a trail or side path along
    /// its whole length. The generator lays these, leaving out only what falls in water or on a steep bank.
    /// </summary>
    public static void CompoundWall(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutPiece compound)
    {
        (float fromTrail, float fromSpur) = WallClearance(plan, rules);
        compound.Wall.Clear();
        RaidSetPieceDefinition definition = compound.Definition;
        float radius = compound.WallRadius;
        if (definition is null || radius <= 0f)
        {
            return;
        }

        int count = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * radius / definition.PieceLength));
        float clear = (definition.OpeningWidth * 0.5f + definition.PieceLength * 0.5f) / radius * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            float angle = (i + 0.5f) / count * 360f - 180f;
            if (RaidMapGenerator.CompoundOpenAt(angle, compound.Openings, clear))
            {
                continue;
            }

            float rad = angle * Mathf.Deg2Rad;
            var outward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var along = new Vector2(-outward.y, outward.x);
            Vector2 middle = compound.Position + outward * radius, half = along * (definition.PieceLength * 0.5f);
            if (NearALine(plan.Trails, middle - half, middle + half, fromTrail) || NearALine(plan.Spurs, middle - half, middle + half, fromSpur))
            {
                continue;
            }

            compound.Wall.Add(angle);
        }
    }

    private static bool NearALine(List<List<Vector2>> lines, Vector2 from, Vector2 to, float keep)
    {
        foreach (List<Vector2> line in lines)
        {
            for (int i = 0; i + 1 < line.Count; i++)
            {
                if (SegmentsApart(from, to, line[i], line[i + 1]) < keep)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // The least distance between two segments (zero when they cross).
    private static float SegmentsApart(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
        Vector2 r = b - a, s = d - c;
        float denominator = Cross(r, s);
        if (Mathf.Abs(denominator) > 1e-6f)
        {
            float t = Cross(c - a, s) / denominator, u = Cross(c - a, r) / denominator;
            if (t >= 0f && t <= 1f && u >= 0f && u <= 1f)
            {
                return 0f;
            }
        }

        return Mathf.Min(Mathf.Min(PointToSegment(a, c, d), PointToSegment(b, c, d)), Mathf.Min(PointToSegment(c, a, b), PointToSegment(d, a, b)));
    }

    private static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    // Each segment that has one end inside the circle and the other outside crosses it once: where, as an angle.
    private static void Crossings(List<Vector2> line, Vector2 middle, float radius, List<float> into)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a = line[i] - middle, b = line[i + 1] - middle;
            bool aIn = a.sqrMagnitude < radius * radius, bIn = b.sqrMagnitude < radius * radius;
            if (aIn == bIn)
            {
                // Both outside can still cut through the ring twice (a trail passing across the yard).
                if (!aIn && SegmentCutsCircle(a, b, radius, out float first, out float second))
                {
                    into.Add(AngleOf(Vector2.Lerp(a, b, first)));
                    into.Add(AngleOf(Vector2.Lerp(a, b, second)));
                }

                continue;
            }

            SegmentCutsCircle(a, b, radius, out float t0, out float t1);
            float t = aIn ? t1 : t0;
            into.Add(AngleOf(Vector2.Lerp(a, b, t)));
        }
    }

    private static float AngleOf(Vector2 offset) => Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

    // Where the segment a→b meets the circle about the origin, as shares of its length (first ≤ second); false when it misses or only
    // meets it beyond its ends.
    private static bool SegmentCutsCircle(Vector2 a, Vector2 b, float radius, out float first, out float second)
    {
        Vector2 d = b - a;
        float qa = Vector2.Dot(d, d), qb = 2f * Vector2.Dot(a, d), qc = Vector2.Dot(a, a) - radius * radius;
        float disc = qb * qb - 4f * qa * qc;
        first = second = 0f;
        if (qa < 1e-6f || disc < 0f)
        {
            return false;
        }

        float root = Mathf.Sqrt(disc);
        first = (-qb - root) / (2f * qa);
        second = (-qb + root) / (2f * qa);
        return first >= 0f && second <= 1f;
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
            if (piece is null || piece.Prefab is null || piece.Weight <= 0f || piece.OwnsClearing || !piece.Suits(mood) || (forRumour && !piece.HoldsRumours))
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
