using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cliffs and ravines, which only moods with terraces, ravines or spires run: the levels clearings stand on and the
/// ramps between them, ravines grown across the open land (crossed squarely by trails), and spires. Part of
/// RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    // A ramp eases in and out (smoothstep), so its steepest stretch is 1.5 times its mean slope; 0.53 is about 28 degrees.
    private const float RampSteepest = 0.53f;

    private const float RampSmoothSteepness = 1.5f;

    private const float RavineStep = 4f;

    private const float RavineTurn = 9f;

    private const int RavineTries = 150;

    private const float RavineFromRoom = 2.5f;

    private const float RavineFromSite = 6f;

    private const float RavineFromPocket = 2f;

    private const float RavineFromPond = 4f;

    private const float RavineFromRavine = 12f;

    private const float RavineFromTrail = 1.5f;

    // How squarely a ravine must cross a trail (the sine of the angle between them), so the neck over it stays short.
    private const float RavineCrossing = 0.7f;

    private const float RavineShortest = 0.5f;

    private const float RavineWobble = 0.14f;

    private const float RavineTaper = 18f;

    private const float RavineEndWidth = 0.45f;

    private const float SpireFromTrail = 7f;

    private const float SpireFromRoom = 6f;

    private const float SpireFromSite = 8f;

    private const float SpireFromRavine = 5f;

    private const float SpireFromSpire = 10f;

    private const int SpireTries = 120;

    // Two trails this close (beyond their own sides), or a trail this close to a clearing it doesn't end at, share one level.
    private const float LevelsApart = 4f;

    private const float LevelSample = 2f;

    // Each clearing's level comes from broad noise, so high and low ground lie in patches. Then a trail never climbs more than one level,
    // nor more steeply than a walkable ramp, and what stands too close together to have a cliff between (a trail passing a clearing, two
    // trails side by side) shares one level: each time, the higher end comes down. It only ever lowers, so it always settles.
    private static void AssignLevels(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        RaidMapMood terraced = null;
        foreach (RaidLayoutRegion region in plan.Regions)
        {
            if (region.Mood.terraces > 1 && terraced == null)
            {
                terraced = region.Mood;
            }
        }

        if (plan.Regions.Count == 0 && plan.Mood != null && plan.Mood.terraces > 1)
        {
            terraced = plan.Mood;
        }

        if (terraced == null)
        {
            return;
        }

        plan.HasTerraces = true;
        plan.TerraceHeight = terraced.terraceHeight;
        RaidMapMood mood = terraced;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            // Each clearing on its region's levels; the ways out lie low.
            RaidMapMood here = plan.MoodAt(room.Position);
            if (here.terraces <= 1 || room.Role == RaidRoomRole.Exit)
            {
                room.Level = 0;
                continue;
            }

            float noise = Mathf.PerlinNoise((room.Position.x + plan.NoiseOffset.x + 517f) / here.terraceScale, (room.Position.y + plan.NoiseOffset.y - 517f) / here.terraceScale);
            room.Level = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(0.25f, 0.75f, noise) * here.terraces), 0, here.terraces - 1);
        }

        List<List<int>> together = SharedLevels(plan, rules.TrailHalfWidth * plan.Mood.trails);
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < plan.Links.Count; i++)
            {
                (int a, int b) = plan.Links[i];
                RaidLayoutRoom first = plan.Rooms[a], second = plan.Rooms[b];
                int climb = Mathf.Abs(first.Level - second.Level);
                if (climb == 0)
                {
                    continue;
                }

                float walk = Length(plan.Trails[i]) - first.Radius - second.Radius;
                if (climb > 1 || walk <= 0f || RampSmoothSteepness * mood.terraceHeight / walk > RampSteepest)
                {
                    (first.Level > second.Level ? first : second).Level--;
                    changed = true;
                }
            }

            foreach (List<int> group in together)
            {
                int lowest = int.MaxValue;
                foreach (int room in group)
                {
                    lowest = Mathf.Min(lowest, plan.Rooms[room].Level);
                }

                foreach (int room in group)
                {
                    if (plan.Rooms[room].Level > lowest)
                    {
                        plan.Rooms[room].Level = lowest;
                        changed = true;
                    }
                }
            }
        }
    }

    // The clearings that must stand on one level: a trail's two ends with any clearing it passes close by, and the ends of two trails
    // that run close beside each other away from the clearings they start from.
    private static List<List<int>> SharedLevels(RaidLayoutPlan plan, float halfWidth)
    {
        var groups = new List<List<int>>();
        for (int i = 0; i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            var trail = new List<List<Vector2>> { plan.Trails[i] };
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                RaidLayoutRoom room = plan.Rooms[r];
                if (r != a && r != b && LineWithin(trail, room.Position, room.Radius + halfWidth + LevelsApart))
                {
                    groups.Add(new List<int> { a, b, r });
                }
            }
        }

        float close = halfWidth * 2f + LevelsApart;
        for (int i = 0; i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            List<Vector2> trail = plan.Trails[i];
            for (int j = i + 1; j < plan.Links.Count; j++)
            {
                (int c, int d) = plan.Links[j];
                var other = new List<List<Vector2>> { plan.Trails[j] };
                if (AnyClose(plan, trail, a, b, c, d, other, close))
                {
                    groups.Add(new List<int> { a, b, c, d });
                }
            }
        }

        return groups;
    }

    // Along the trail, outside every clearing either trail starts or ends at (where trails meet by design).
    private static bool AnyClose(RaidLayoutPlan plan, List<Vector2> trail, int a, int b, int c, int d, List<List<Vector2>> other, float close)
    {
        for (int k = 0; k + 1 < trail.Count; k++)
        {
            float length = Vector2.Distance(trail[k], trail[k + 1]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / LevelSample));
            for (int n = 0; n <= steps; n++)
            {
                Vector2 point = Vector2.Lerp(trail[k], trail[k + 1], n / (float)steps);
                if (InRoom(plan, point, a) || InRoom(plan, point, b) || InRoom(plan, point, c) || InRoom(plan, point, d))
                {
                    continue;
                }

                if (LineWithin(other, point, close))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool InRoom(RaidLayoutPlan plan, Vector2 point, int room) =>
        Vector2.Distance(point, plan.Rooms[room].Position) < plan.Rooms[room].Radius + LevelsApart;

    // On a map with terraces, a site's level pocket keeps clear of ramps and of what stands on another level, or a cliff would cut it.
    private static bool PocketFits(RaidLayoutPlan plan, RaidLayoutRules rules, Vector2 spot, float radius)
    {
        if (plan.Valley != null && !ValleyHolds(plan, spot, radius))
        {
            return false;
        }

        if (!plan.HasTerraces || radius <= 0f)
        {
            return true;
        }

        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        int level = LevelAt(plan, spot);
        for (int i = 0; i < plan.Trails.Count && i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            if ((plan.Rooms[a].Level != level || plan.Rooms[b].Level != level) && NearLine(plan.Trails[i], spot, radius + halfWidth + LevelsApart))
            {
                return false;
            }
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (room.Level != level && Vector2.Distance(room.Position, spot) < room.Radius + radius + LevelsApart)
            {
                return false;
            }
        }

        return true;
    }

    // The level of the nearest clearing or level trail.
    private static int LevelAt(RaidLayoutPlan plan, Vector2 spot)
    {
        int level = 0;
        float nearest = float.MaxValue;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            float distance = Vector2.Distance(room.Position, spot) - room.Radius;
            if (distance < nearest)
            {
                nearest = distance;
                level = room.Level;
            }
        }

        for (int i = 0; i < plan.Trails.Count && i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            if (plan.Rooms[a].Level == plan.Rooms[b].Level && NearLine(plan.Trails[i], spot, nearest))
            {
                nearest = DistanceToLine(plan.Trails[i], spot);
                level = plan.Rooms[a].Level;
            }
        }

        return level;
    }

    // On a map with terraces, a trail between two levels is all ramp: a site and its level pocket stand on level trails only.
    private static bool OnRamp(RaidLayoutPlan plan, List<Vector2> trail)
    {
        if (!plan.HasTerraces)
        {
            return false;
        }

        int index = plan.Trails.IndexOf(trail);
        return index >= 0 && plan.Rooms[plan.Links[index].a].Level != plan.Rooms[plan.Links[index].b].Level;
    }

    // From their own stream, after the ponds, so ravines never move a site. A map gets fewer when they can't find room.
    // Each region's ravines, as many as its mood wants for its share of the map, staying within it.
    private static void PlaceRavines(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Ravines " + attempt));
        foreach (RaidLayoutRegion region in RegionsOrMood(plan))
        {
            RaidMapMood mood = region.Mood;
            if (mood == null || mood.ravines.y <= 0)
            {
                continue;
            }

            int wanted = Share(random, random.Next(Mathf.Max(0, mood.ravines.x), mood.ravines.y + 1), plan, region);
            for (int i = 0; i < wanted; i++)
            {
                for (int tries = 0; tries < RavineTries; tries++)
                {
                    RaidLayoutRavine ravine = GrowRavine(plan, rules, mood, random);
                    if (ravine != null)
                    {
                        plan.Ravines.Add(ravine);
                        break;
                    }
                }
            }
        }
    }

    // From a spot in the wild ground both ways, turning a little at each step, until it is long enough or would cut what it must not: a
    // clearing, a site, a pond, another ravine, the map's edge, or a trail it doesn't cross squarely (a crossing becomes a neck).
    private static RaidLayoutRavine GrowRavine(RaidLayoutPlan plan, RaidLayoutRules rules, RaidMapMood mood, System.Random random)
    {
        float half = Range(random, mood.ravineWidth.x, mood.ravineWidth.y) * 0.5f;
        float widest = half * (1f + RavineWobble);
        float length = Range(random, mood.ravineLength.x, mood.ravineLength.y);
        float margin = rules.EdgeMargin + widest;
        var start = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
        float heading = Range(random, 0f, 360f);
        if (!RavineCanPass(plan, rules, start, Direction(heading), widest) || (plan.Regions.Count > 1 && plan.MoodAt(start) != mood))
        {
            return null;
        }

        var ahead = new List<Vector2>();
        var behind = new List<Vector2>();
        float grown = 0f;
        float headingAhead = heading, headingBehind = heading + 180f;
        float turnAhead = Range(random, -RavineTurn, RavineTurn), turnBehind = Range(random, -RavineTurn, RavineTurn);
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

        if (grown < length * RavineShortest)
        {
            return null;
        }

        var ravine = new RaidLayoutRavine
        {
            Depth = Range(random, mood.ravineDepth.x, mood.ravineDepth.y),
            River = random.NextDouble() < mood.riverChance,
            Mood = mood,
        };
        for (int i = behind.Count - 1; i >= 0; i--)
        {
            ravine.Line.Add(behind[i]);
        }

        ravine.Line.Add(start);
        ravine.Line.AddRange(ahead);
        float noiseAt = Range(random, 0f, 500f);
        float run = 0f;
        float total = (ravine.Line.Count - 1) * RavineStep;
        for (int i = 0; i < ravine.Line.Count; i++)
        {
            run += i > 0 ? Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]) : 0f;
            // Narrowing towards both ends, so a ravine closes up rather than stopping in a round pit.
            float taper = Mathf.Lerp(RavineEndWidth, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(run, total - run) / RavineTaper));
            ravine.Tapers.Add(taper);
            ravine.HalfWidths.Add(half * taper * (1f + (Mathf.PerlinNoise(noiseAt + run / 23f, noiseAt) - 0.5f) * 2f * RavineWobble));
        }

        return ravine;

        // The turn itself drifts, so the ravine bends in long curves rather than zigzags; something in the way bends it further, up to
        // a steer each way, before it stops.
        bool Step(ref Vector2 tip, ref float facing, ref float turn, List<Vector2> into)
        {
            turn = Mathf.Clamp(turn + Range(random, -RavineTurn, RavineTurn) * 0.5f, -RavineTurn, RavineTurn);
            float side = random.Next(2) == 0 ? 1f : -1f;
            foreach (float steer in RavineSteers)
            {
                float heading = facing + turn + steer * side;
                Vector2 next = tip + Direction(heading) * RavineStep;
                // Begun in its region, a ravine may run on into the next, as a gorge does.
                if (RavineCanPass(plan, rules, next, Direction(heading), widest) && !NearOwnLine(next, into))
                {
                    facing = heading;
                    into.Add(next);
                    tip = next;
                    grown += RavineStep;
                    return true;
                }
            }

            return false;
        }

        // A ravine bending back on itself would cut through its own walls.
        bool NearOwnLine(Vector2 point, List<Vector2> line)
        {
            for (int i = 0; i < line.Count - 3; i++)
            {
                if ((line[i] - point).sqrMagnitude < widest * widest * 4f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool RavineCanPass(RaidLayoutPlan plan, RaidLayoutRules rules, Vector2 point, Vector2 heading, float half)
    {
        float margin = rules.EdgeMargin + half;
        if (point.x < margin || point.y < margin || point.x > plan.Size - margin || point.y > plan.Size - margin)
        {
            return false;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, point) < room.Radius + half + RavineFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, point) < half + RavineFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, point) < radius + half + RavineFromPocket)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(point) < half + RavineFromPond)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine other in plan.Ravines)
        {
            other.Nearest(point, out _, out _, out float distance);
            if (distance < half + other.WidestHalf + RavineFromRavine)
            {
                return false;
            }
        }

        // Near a trail only while crossing it squarely, so a neck stays short and no trail runs along an edge.
        float keep = rules.TrailHalfWidth * plan.Mood.trails + half + RavineFromTrail;
        return CrossesSquarely(plan.Trails, point, heading, keep) && CrossesSquarely(plan.Spurs, point, heading, keep);
    }

    private static bool CrossesSquarely(List<List<Vector2>> lines, Vector2 point, Vector2 heading, float keep)
    {
        float squared = keep * keep;
        foreach (List<Vector2> line in lines)
        {
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 along = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                if ((line[i] + along * t - point).sqrMagnitude >= squared)
                {
                    continue;
                }

                Vector2 direction = along.normalized;
                if (Mathf.Abs(heading.x * direction.y - heading.y * direction.x) < RavineCrossing)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void PlaceSpires(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Spires " + attempt));
        foreach (RaidLayoutRegion region in RegionsOrMood(plan))
        {
            PlaceSpires(plan, rules, region, random);
        }
    }

    private static void PlaceSpires(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutRegion region, System.Random random)
    {
        RaidMapMood mood = region.Mood;
        if (mood == null || mood.spires.y <= 0)
        {
            return;
        }

        int wanted = Share(random, random.Next(Mathf.Max(0, mood.spires.x), mood.spires.y + 1), plan, region);
        for (int i = 0; i < wanted; i++)
        {
            for (int tries = 0; tries < SpireTries; tries++)
            {
                var spire = new RaidLayoutSpire
                {
                    Radius = Range(random, mood.spireRadius.x, mood.spireRadius.y),
                    Height = Range(random, mood.spireHeight.x, mood.spireHeight.y),
                    Turn = Range(random, 0f, Mathf.PI * 2f),
                };
                float margin = rules.EdgeMargin + rules.RimWidth + spire.Reach;
                spire.Position = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
                if ((plan.Regions.Count <= 1 || plan.MoodAt(spire.Position) == mood) && SpireFits(plan, rules, spire))
                {
                    plan.Spires.Add(spire);
                    break;
                }
            }
        }
    }

    private static bool SpireFits(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutSpire spire)
    {
        Vector2 spot = spire.Position;
        float reach = spire.Reach;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, spot) < room.Radius + reach + SpireFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < reach + SpireFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, spot) < radius + reach + SpireFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < reach + SpireFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + reach + SpireFromRavine)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire other in plan.Spires)
        {
            if (Vector2.Distance(other.Position, spot) < other.Reach + reach + SpireFromSpire)
            {
                return false;
            }
        }

        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        return !LineWithin(plan.Trails, spot, reach + halfWidth + SpireFromTrail)
            && !LineWithin(plan.Spurs, spot, reach + halfWidth + SpireFromTrail);
    }
}
