using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Haven's own ground and water, where the player builds: low shelves and hollows in its meadows beyond the yard (which stays
/// as level as ever, where saved buildings stand), and a stream from a spring at the meadows' edge into a fishing pond and on
/// out of it to the lake. Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    /// <summary>The Haven's fishing pond (one of <see cref="Ponds"/>), or null without one.</summary>
    public RaidLayoutPond HavenPond { get; private set; }

    /// <summary>The Haven's own streams (indexes into <see cref="Rivers"/>): into the fishing pond, and out of it.</summary>
    public readonly List<int> HavenStreams = new List<int>();

    /// <summary>A knoll in the meadows: where, how high, and how far its sides reach.</summary>
    public readonly struct Knoll
    {
        public readonly Vector2 At;
        public readonly float Height;
        public readonly float Radius;

        public Knoll(Vector2 at, float height, float radius)
        {
            At = at;
            Height = height;
            Radius = radius;
        }
    }

    public readonly List<Knoll> Knolls = new List<Knoll>();

    /// <summary>The knolls' height at a point: flat on top (the inner 30%), easing down their sides.</summary>
    public float KnollAt(Vector2 point)
    {
        float height = 0f;
        foreach (Knoll knoll in Knolls)
        {
            float distance = Vector2.Distance(point, knoll.At);
            if (distance < knoll.Radius)
            {
                height = Mathf.Max(height, knoll.Height * (1f - Ease(knoll.Radius * 0.3f, knoll.Radius, distance)));
            }
        }

        return height;
    }

    // Out in the meadows, past the yard and the props round it, off the roads and the water, apart from each other.
    private void PlaceKnolls()
    {
        int count = random.Next(Recipe.havenKnolls.x, Recipe.havenKnolls.y + 1);
        for (int tries = 0; tries < Tries * 2 && Knolls.Count < count; tries++)
        {
            float radius = Range(Recipe.knollRadius.x, Recipe.knollRadius.y);
            // Its foot never reaches the yard.
            Vector2 at = Around(Recipe.yard + radius + 3f, Recipe.haven);
            if (Near(Paths, at, radius * 0.35f) || Near(Rivers, at, radius + 10f) || InWater(at, radius + 8f) || !OneBench(at, radius * 0.8f)
                || Knolls.Exists(other => Vector2.Distance(other.At, at) < other.Radius + radius + 25f))
            {
                continue;
            }

            Knolls.Add(new Knoll(at, Range(Recipe.knollHeight.x, Recipe.knollHeight.y), radius));
        }
    }

    /// <summary>
    /// The meadows' shelves at a point: 1 on top of one, -0.6 down in a hollow, 0 on the level ground between (and in the yard,
    /// and out on the benches). A shelf's top is flat enough to build on; its edge is a few metres of slope.
    /// </summary>
    public float Shelf(Vector2 point)
    {
        float r = point.magnitude;
        float meadow = Ease(Recipe.yard + 20f, Recipe.yard + 40f, r) * (1f - Ease(Recipe.haven - 20f, Recipe.haven + 30f, r));
        if (meadow <= 0f || Recipe.havenShelves <= 0f)
        {
            return 0f;
        }

        float field = Perlin(point, Recipe.shelfScale, 601f);
        return (Ease(0.55f, 0.71f, field) - 0.6f * Ease(0.38f, 0.24f, field)) * meadow;
    }

    private bool InHavenBearings(Vector2 point)
    {
        float bearing = BearingOf(point);
        Vector2 range = Recipe.havenWaterBearings;
        return range.x <= range.y ? bearing >= range.x && bearing <= range.y : bearing >= range.x || bearing <= range.y;
    }

    private float HavenBearing()
    {
        Vector2 range = Recipe.havenWaterBearings;
        float span = Mathf.Repeat(range.y - range.x, 360f);
        return Mathf.Repeat(range.x + Range(0f, span), 360f);
    }

    // Ground even enough round a point for still water: its banks rise and fall less than this much (one bank may stand higher,
    // as a shelf's edge, but the water never sits on a slope).
    private bool LevelGround(Vector2 point, float radius, float most = 1.6f)
    {
        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = 0; i < 12; i++)
        {
            float ground = Ground(point + Dir(i * 30f) * radius);
            lowest = Mathf.Min(lowest, ground);
            highest = Mathf.Max(highest, ground);
        }

        return highest - lowest <= most && Mathf.Abs(Shelf(point)) <= 0.2f;
    }

    private void PlaceHavenWater()
    {
        if (!Recipe.havenStream)
        {
            return;
        }

        // The pond: out in the meadows past the yard, between the roads, on level ground; of the spots that will do, the one
        // with the shortest way on to the lake or a river lower down, so its stream out has somewhere to go.
        var spots = new List<RaidLayoutPond>();
        for (int tries = 0; tries < Tries * 3 && spots.Count < 16; tries++)
        {
            float radius = Range(Recipe.havenPondRadius.x, Recipe.havenPondRadius.y);
            var pond = new RaidLayoutPond
            {
                Position = Dir(HavenBearing()) * Range(Recipe.havenPondDistance.x, Recipe.havenPondDistance.y),
                Radii = new Vector2(radius, radius / Range(1.1f, 1.45f)),
                Turn = Range(0f, Mathf.PI * 2f),
                Depth = 2.4f,
            };
            if (pond.Position.magnitude - pond.Reach < Recipe.yard + 6f || Near(Paths, pond.Position, pond.Reach + 7f) || Near(Rivers, pond.Position, pond.Reach + 18f)
                || Knolls.Exists(knoll => Vector2.Distance(knoll.At, pond.Position) < knoll.Radius + pond.Reach + 8f)
                || Lake.Edge(pond.Position) < pond.Reach + 25f || !OneBench(pond.Position, pond.Reach + 5f) || !LevelGround(pond.Position, pond.Reach + 2f))
            {
                continue;
            }

            pond.Level = LevelAround(pond, 2f, 0.2f);
            spots.Add(pond);
        }

        float bestWay = float.MaxValue;
        foreach (RaidLayoutPond spot in spots)
        {
            float way = WayOnFrom(spot);
            if (way < bestWay)
            {
                bestWay = way;
                HavenPond = spot;
            }
        }

        if (HavenPond == null)
        {
            return;
        }

        Ponds.Add(HavenPond);
        // Out first (the stream in may then come down beside its start): on to the lake, or the nearest river lower down, as a
        // mere's stream goes, through the meadows.
        int outOf = StreamFrom(HavenPond.Position, HavenPond, HavenPond.Level, Recipe.yard + 6f, 15f) ? Rivers.Count - 1 : -1;
        // In: from a spring out in the meadows, round the yard, into the pond.
        for (int tries = 0; tries < Tries * 3; tries++)
        {
            float bearing = BearingOf(HavenPond.Position) + Range(45f, 130f) * (random.Next(2) == 0 ? 1f : -1f);
            Vector2 at = Dir(bearing) * Range(Recipe.yard + 50f, Recipe.haven - 6f);
            if (!InHavenBearings(at) || InWater(at, 10f) || Near(Paths, at, 8f) || Near(Rivers, at, 16f) || !OneBench(at, 6f) || KnollAt(at) > 0.3f
                || Vector2.Distance(at, HavenPond.Position) < 60f)
            {
                continue;
            }

            float radius = Range(2.6f, 3.6f);
            var spring = new RaidLayoutPond { Position = at, Radii = new Vector2(radius, radius / Range(1f, 1.3f)), Turn = Range(0f, Mathf.PI * 2f), Depth = 0.7f };
            spring.Level = LevelAround(spring, 2f, 0.15f);
            Vector2 toward = (HavenPond.Position - at).normalized;
            Vector2 start = spring.Rim(Mathf.Atan2(toward.y, toward.x) - spring.Turn, -1f);
            Vector2 back = (at - HavenPond.Position).normalized;
            Vector2 end = HavenPond.Rim(Mathf.Atan2(back.y, back.x) - HavenPond.Turn, -1.5f);
            List<Vector2> line = Meander(Wander(start, end, 0.14f), Range(3f, 5f), Range(24f, 34f));
            bool clear = true;
            for (int k = 1; k + 1 < line.Count && clear; k++)
            {
                Vector2 point = line[k];
                clear = point.magnitude > Recipe.yard + 6f && (k + 4 >= line.Count || !Near(Rivers, point, 14f)) && Lake.Edge(point) > 12f
                        && KnollAt(point) < 0.3f && !Meres.Exists(mere => mere.Edge(point) < 10f) && (k + 4 >= line.Count || HavenPond.Edge(point) > 0f);
            }

            if (!clear)
            {
                continue;
            }

            Ponds.Add(spring);
            Springs.Add(at);
            AddRiver(line, Range(1.3f, 1.8f), 0.6f, -1, spring.Level);
            riverPonds[riverPonds.Count - 1] = HavenPond;
            HavenStreams.Add(Rivers.Count - 1);
            break;
        }

        if (outOf >= 0)
        {
            HavenStreams.Add(outOf);
        }

    }

    // How far a pond's water would have to run to the lake's shore or a river lying lower than it.
    private float WayOnFrom(RaidLayoutPond pond)
    {
        float way = Vector2.Distance(Lake.Shore(pond.Position), pond.Position);
        foreach (List<Vector2> river in Rivers)
        {
            Vector2 on = NearestOn(new List<List<Vector2>> { river }, pond.Position);
            float distance = Vector2.Distance(on, pond.Position);
            if (distance >= 20f && Ground(on) < pond.Level - 1f)
            {
                way = Mathf.Min(way, distance);
            }
        }

        return way;
    }
}
