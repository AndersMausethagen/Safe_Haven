using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Valley shape (Troll Valley): a long valley between high walls running from one edge of the map to the opposite one, a river down
/// its middle, the start at its mouth and the main way out at its head. Clearings stand on alternating banks, so the ways up cross the
/// river, each squarely, at a ford or over a bridge, and the two banks are the two routes. Part of RaidLayoutPlanner, in its own file.
/// Each piece draws from its own random stream ("Valley", "Valley Rooms", "Crossings"), so a valley never moves another raid's map.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float ValleyStep = 4f;
    private const float ValleyExtend = 14f;
    private const float ValleyFromWall = 3f;
    // Metres kept between the water and a clearing or a trail beside the river: a ford's banks (RaidLayoutField's RiverBank and
    // RiverFordBank) and a little more, so nothing walks along a bank's slope.
    private const float ValleyRiverBank = 6.5f;
    private const float ValleyCrossingSlack = 1.5f;
    private const float BridgesApart = 30f;

    // The valley and its river, before anything else is placed: the clearings go where it leaves room.
    private static void PlaceValley(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        RaidValleyRule rule = rules.Valley;
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Valley " + attempt));
        float size = plan.Size;
        // The valley runs south to north or north to south, so its walls stand to the sides of the top-down camera, never between it
        // and the player.
        bool fromSouth = random.Next(2) == 0;
        var valley = new RaidLayoutValley
        {
            Axis = fromSouth ? Vector2.up : Vector2.down,
            Step = ValleyStep,
            Extend = ValleyExtend,
            Length = size,
            WallRun = rule.wallRun,
            Benches = Mathf.Max(1, rule.benches),
            Tilt = Range(random, rule.tilt.x, Mathf.Max(rule.tilt.x, rule.tilt.y)),
            LeftWall = Range(random, rule.wallHeight.x, Mathf.Max(rule.wallHeight.x, rule.wallHeight.y)),
            RightWall = Range(random, rule.wallHeight.x, Mathf.Max(rule.wallHeight.x, rule.wallHeight.y)),
        };
        valley.Across = new Vector2(-valley.Axis.y, valley.Axis.x);
        valley.Origin = new Vector2(size * 0.5f, fromSouth ? -ValleyExtend : size + ValleyExtend);

        float floorHalf = Range(random, rule.floorWidth.x, Mathf.Max(rule.floorWidth.x, rule.floorWidth.y)) * 0.5f * plan.Mood.valleyWidth;
        // The floor and most of each wall stay on the map, however far the valley swings.
        float swing = Mathf.Min(Range(random, rule.meander.x, Mathf.Max(rule.meander.x, rule.meander.y)),
            Mathf.Max(0f, size * 0.5f - floorHalf - rule.wallRun * 0.6f - rules.EdgeMargin * 0.5f));
        float bend = Mathf.Max(20f, Range(random, rule.bendLength.x, Mathf.Max(rule.bendLength.x, rule.bendLength.y)));
        float phase = Range(random, 0f, Mathf.PI * 2f);
        float noiseAt = Range(random, 0f, 500f);
        for (float along = -ValleyExtend; along <= size + ValleyExtend + 0.01f; along += ValleyStep)
        {
            float wander = Mathf.Sin(along / bend * Mathf.PI * 2f + phase) + (Mathf.PerlinNoise(noiseAt + along / 47f, noiseAt) - 0.5f) * 0.7f;
            valley.Line.Add(valley.Origin + valley.Axis * (along + ValleyExtend) + valley.Across * (swing * wander));
            valley.FloorHalf.Add(floorHalf * (1f + (Mathf.PerlinNoise(noiseAt, noiseAt + along / 61f) - 0.5f) * 0.3f));
        }

        plan.Valley = valley;

        // The river down its middle, from edge to edge; it runs down the valley's tilt (RaidLayoutField.BuildRivers).
        float riverHalf = Range(random, rule.riverWidth.x, Mathf.Max(rule.riverWidth.x, rule.riverWidth.y)) * 0.5f;
        var river = new RaidLayoutRiver { Depth = Range(random, rule.riverDepth.x, Mathf.Max(rule.riverDepth.x, rule.riverDepth.y)) };
        float run = 0f;
        for (int i = 0; i < valley.Line.Count; i++)
        {
            run += i > 0 ? Vector2.Distance(valley.Line[i - 1], valley.Line[i]) : 0f;
            river.Line.Add(valley.Line[i]);
            river.HalfWidths.Add(riverHalf * (1f + (Mathf.PerlinNoise(noiseAt + run / 29f, noiseAt + 7f) - 0.5f) * 0.4f));
        }

        plan.Rivers.Add(river);
    }

    // Metres from a point to the valley river's water: its distance to the river's line less the water's half width there. At a bend
    // this is less than the distance straight across the valley (OffsetOf), which is why placement checks it.
    private static float WaterClearance(RaidLayoutPlan plan, Vector2 point)
    {
        RaidLayoutRiver river = plan.Rivers[0];
        river.Nearest(point, out int segment, out float along, out float distance);
        return distance - RaidLayoutRavine.At(river.HalfWidths, segment, along);
    }

    // Moves a point out across the valley, on its bank, until it is this far from the water (or as far as the floor allows).
    private static Vector2 AwayFromWater(RaidLayoutPlan plan, Vector2 point, int bank, float keep)
    {
        RaidLayoutValley valley = plan.Valley;
        float along = valley.AlongOf(point);
        valley.CentreAt(along, out float half);
        float offset = valley.OffsetOf(point) * bank;
        for (int i = 0; i < 24 && WaterClearance(plan, point) < keep && offset < half - ValleyFromWall; i++)
        {
            offset += Mathf.Max(0.5f, keep - WaterClearance(plan, point));
            point = valley.At(along, Mathf.Min(offset, half - ValleyFromWall) * bank);
        }

        return point;
    }

    // The widest the valley's river gets, with its banks: what clearings and pockets keep clear of.
    private static float RiverClear(RaidLayoutPlan plan)
    {
        float widest = 0f;
        if (plan.Rivers.Count > 0)
        {
            foreach (float half in plan.Rivers[0].HalfWidths)
            {
                widest = Mathf.Max(widest, half);
            }
        }

        return widest + ValleyRiverBank;
    }

    private static void PlaceValleyRooms(RaidLayoutPlan plan, RaidLayoutRules rules, out List<int> spine)
    {
        RaidLayoutValley valley = plan.Valley;
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Valley Rooms"));
        float size = plan.Size;
        float margin = rules.EdgeMargin + rules.RoomRadius.y * 0.5f;
        float clear = RiverClear(plan);
        spine = new List<int>();

        // A clearing on a bank at a place along the valley: at least clear of the river, at most a few metres from the wall's foot, and
        // no larger than that bank leaves room for (a gorge's banks are narrow).
        Vector2 OnBank(float along, int bank, ref float radius, float lean)
        {
            valley.CentreAt(along, out float half);
            radius = Mathf.Min(radius, Mathf.Max(3f, (half - clear - ValleyFromWall - 1f) * 0.5f));
            float nearest = clear + radius + 1f;
            float farthest = Mathf.Max(nearest, half - radius - ValleyFromWall);
            Vector2 spot = AwayFromWater(plan, valley.At(along, bank * Mathf.Lerp(nearest, farthest, lean)), bank, radius + ValleyRiverBank);
            // At a sharp bend the floor may not leave room for the whole clearing: it shrinks to what its bank allows.
            radius = Mathf.Max(3f, Mathf.Min(radius, WaterClearance(plan, spot) - ValleyRiverBank, -valley.FloorEdge(spot) - ValleyFromWall));
            return spot;
        }

        int startBank = random.Next(2) == 0 ? 1 : -1;
        float startRadius = rules.RoomRadius.x;
        Vector2 start = OnBank(margin, startBank, ref startRadius, Range(random, 0.2f, 0.6f));
        AddRoom(plan, start, startRadius, RaidRoomRole.Start);

        // The ways out: the main one at the head (the middle of the exits, as EnsureRoutes looks for it), the others high up the valley
        // at the feet of its walls, on alternating sides.
        int exits = Mathf.Max(1, rules.ExitRooms);
        int main = exits / 2;
        int sideCount = 0;
        for (int i = 0; i < exits; i++)
        {
            Vector2 spot;
            float radius = rules.RoomRadius.x;
            if (i == main)
            {
                spot = OnBank(size - margin, random.Next(2) == 0 ? 1 : -1, ref radius, Range(random, 0.1f, 0.5f));
            }
            else
            {
                float along = size * Range(random, 0.6f, 0.82f);
                spot = OnBank(along, sideCount % 2 == 0 ? 1 : -1, ref radius, 1f);
                sideCount++;
            }

            AddRoom(plan, spot, radius, RaidRoomRole.Exit);
        }

        // The stops up the valley, each on the other bank from the one before, evenly spread with a little give.
        int wanted = random.Next(rules.Rooms.x, rules.Rooms.y + 1);
        float first = margin + rules.RoomSeparation * 0.7f;
        float last = size - margin - rules.RoomSeparation * 0.7f;
        int bank = -startBank;
        spine.Add(0);
        for (int i = 0; i < wanted; i++)
        {
            float along = Mathf.Lerp(first, last, wanted > 1 ? i / (wanted - 1f) : 0.5f) + Range(random, -0.2f, 0.2f) * (last - first) / Mathf.Max(1, wanted);
            float size0 = RoomSize(rules, plan.Mood, random);
            float lean = Range(random, 0f, 1f);
            // A few tries across the bank and a little along it before the stop is given up (an exit may stand near).
            for (int tries = 0; tries < 6; tries++)
            {
                float radius = size0;
                Vector2 spot = OnBank(along + (tries % 2 == 0 ? 1f : -1f) * tries * 4f, bank, ref radius, (lean + tries * 0.37f) % 1f);
                if (IsFree(plan, spot, rules.RoomSeparation * 0.7f))
                {
                    spine.Add(plan.Rooms.Count);
                    AddRoom(plan, spot, radius, RaidRoomRole.Plain);
                    bank = -bank;
                    break;
                }
            }
        }

        spine.Add(1 + main);

        // The greatest danger waits farthest up the valley.
        RaidLayoutRoom farthest = null;
        float far = -1f;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            float up = valley.ShareOf(room.Position);
            if (room.Role == RaidRoomRole.Plain && up > far)
            {
                far = up;
                farthest = room;
            }
        }

        if (farthest != null)
        {
            // Larger, as on any map, as far as its bank allows: clear of the river and off the wall.
            float room = Mathf.Min(WaterClearance(plan, farthest.Position) - ValleyRiverBank, -valley.FloorEdge(farthest.Position) - 0.5f);
            farthest.Role = RaidRoomRole.Apex;
            farthest.Radius = Mathf.Max(farthest.Radius, Mathf.Min(rules.RoomRadius.y * 1.2f * plan.Mood.rooms, room));
        }
    }

    // Every trail stays on its own bank except where it crosses, and crosses once, straight across: two points either side of the
    // river, square to it. Each crossing is recorded; some carry a bridge (the short way's first), the rest are fords.
    private static void ShapeCrossings(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        RaidLayoutValley valley = plan.Valley;
        if (valley == null || plan.Rivers.Count == 0)
        {
            return;
        }

        RaidLayoutRiver river = plan.Rivers[0];
        float clear = RiverClear(plan);
        for (int t = 0; t < plan.Trails.Count; t++)
        {
            List<Vector2> trail = plan.Trails[t];
            int fromBank = valley.BankOf(trail[0]);
            int toBank = valley.BankOf(trail[trail.Count - 1]);
            if (fromBank == toBank)
            {
                for (int i = 1; i + 1 < trail.Count; i++)
                {
                    trail[i] = KeepOnBank(plan, trail[i], fromBank, clear);
                }

                continue;
            }

            // Where it first reaches the river's line; everything before it on the near bank, after it on the far one.
            int at = 0;
            for (int i = 0; i + 1 < trail.Count; i++)
            {
                if (valley.BankOf(trail[i + 1]) != fromBank)
                {
                    at = i;
                    break;
                }
            }

            float before = valley.OffsetOf(trail[at]);
            float after = valley.OffsetOf(trail[at + 1]);
            Vector2 crossing = Vector2.Lerp(trail[at], trail[at + 1], Mathf.Clamp01(before / Mathf.Max(0.0001f, before - after)));
            float along = valley.AlongOf(crossing);
            Vector2 tangent = (valley.CentreAt(along + 2f, out _) - valley.CentreAt(along - 2f, out _)).normalized;
            var square = new Vector2(-tangent.y, tangent.x);
            if (Vector2.Dot(square, valley.Across) < 0f)
            {
                square = -square;
            }

            Vector2 direction = square * toBank;
            river.Nearest(crossing, out int segment, out float share, out _);
            float reach = RaidLayoutRavine.At(river.HalfWidths, segment, share) + ValleyRiverBank + ValleyCrossingSlack;
            var shaped = new List<Vector2> { trail[0] };
            for (int i = 1; i <= at; i++)
            {
                if (Vector2.Distance(trail[i], crossing) > reach + 2f)
                {
                    shaped.Add(KeepOnBank(plan, trail[i], fromBank, clear));
                }
            }

            shaped.Add(crossing - direction * reach);
            shaped.Add(crossing + direction * reach);
            for (int i = at + 1; i < trail.Count - 1; i++)
            {
                if (Vector2.Distance(trail[i], crossing) > reach + 2f)
                {
                    shaped.Add(KeepOnBank(plan, trail[i], toBank, clear));
                }
            }

            shaped.Add(trail[trail.Count - 1]);
            plan.Trails[t] = shaped;
            plan.Crossings.Add(new RaidLayoutCrossing { Position = crossing, Direction = direction, Reach = reach, Trail = t });
        }

        ChooseBridges(plan, rules.Valley, attempt);
    }

    private static Vector2 KeepOnBank(RaidLayoutPlan plan, Vector2 point, int bank, float clear)
    {
        RaidLayoutValley valley = plan.Valley;
        float along = valley.AlongOf(point);
        float offset = valley.OffsetOf(point);
        valley.CentreAt(along, out float half);
        float wanted = Mathf.Clamp(offset * bank, clear, Mathf.Max(clear, half - ValleyFromWall)) * bank;
        Vector2 kept = Mathf.Approximately(wanted, offset) ? point : valley.At(along, wanted);
        return AwayFromWater(plan, kept, bank, ValleyRiverBank);
    }

    // The short way's crossings get the bridges first (a guard stands on each: the quick way is the watched one), then any, never two
    // close together.
    private static void ChooseBridges(RaidLayoutPlan plan, RaidValleyRule rule, int attempt)
    {
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Crossings " + attempt));
        int wanted = random.Next(rule.bridges.x, Mathf.Max(rule.bridges.x, rule.bridges.y) + 1);
        List<int> order = RaidLayout.PickIndices(random, plan.Crossings.Count, plan.Crossings.Count);
        order.Sort((a, b) => (plan.ShortRoute.Contains(plan.Crossings[b].Trail) ? 1 : 0).CompareTo(plan.ShortRoute.Contains(plan.Crossings[a].Trail) ? 1 : 0));
        int made = 0;
        foreach (int index in order)
        {
            if (made >= wanted)
            {
                break;
            }

            RaidLayoutCrossing crossing = plan.Crossings[index];
            bool tooClose = false;
            foreach (RaidLayoutCrossing other in plan.Crossings)
            {
                tooClose |= other.Bridge && Vector2.Distance(other.Position, crossing.Position) < BridgesApart;
            }

            if (!tooClose)
            {
                crossing.Bridge = true;
                made++;
            }
        }
    }

    // On a Valley map a pocket stands on the floor, clear of the river.
    private static bool ValleyHolds(RaidLayoutPlan plan, Vector2 spot, float radius)
    {
        RaidLayoutValley valley = plan.Valley;
        return valley.Holds(spot, radius + 1f) && WaterClearance(plan, spot) >= radius + ValleyRiverBank;
    }
}
