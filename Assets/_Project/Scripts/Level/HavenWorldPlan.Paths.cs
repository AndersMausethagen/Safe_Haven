using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Paths: roads out from the Haven and the ring joining them, wandering a little, and the points of interest along them
/// with a spur each. Every road leads somewhere: the lake's landing, the mountain, the bog's heart, or a place at its end.
/// Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float PathStart = 32f;

    /// <summary>Where the roads that run out to the edge of the woods end: a place stands at each (recipe: At Road's End).</summary>
    public readonly List<Vector2> RoadEnds = new List<Vector2>();

    // Out from the Haven's edge in every direction: one to the lake shore (where a boat is put in), one up into the mountain, one to the
    // bog and on into it by a causeway, the rest to the edge of the woods. A ring joins them, except across the lake, the mountain
    // and the bog.
    private void PlacePaths()
    {
        int count = Mathf.Max(0, Recipe.radialPaths);
        float turn = Range(0f, 360f / Mathf.Max(1, count));
        float lakeBearing = BearingOf(Lake.Position);
        float bogBearing = HasBog ? BearingOf(BogCentre) : -1f;
        var bearings = new List<float>();
        for (int i = 0; i < count; i++)
        {
            bearings.Add(turn + i * 360f / count);
        }

        var taken = new HashSet<int>();
        int lakeRoad = Snap(bearings, lakeBearing, taken);
        Snap(bearings, MountainPathAngle, taken);
        int bogRoad = HasBog ? Snap(bearings, bogBearing, taken) : -1;
        for (int i = 0; i < bearings.Count; i++)
        {
            float bearing = bearings[i];
            float end = MaxReach;
            if (i == lakeRoad)
            {
                end = Vector2.Distance(Vector2.zero, Lake.Rim(Mathf.Atan2(-Lake.Position.y, -Lake.Position.x) - Lake.Turn, 3f));
            }
            else if (i == bogRoad)
            {
                end = Recipe.bogAt.y - Recipe.bogRadius * 0.62f;
            }

            List<Vector2> line = null;
            for (int tries = 0; tries < 8; tries++)
            {
                line = Wander(Dir(bearing) * PathStart, Dir(bearing) * end, 0.08f);
                // The other roads keep out of the bog and off the meres' ground: a few degrees aside until they do.
                if (i == lakeRoad || i == bogRoad || !line.Exists(point => BogWeight(point) > 0.1f))
                {
                    break;
                }

                bearing += Mathf.DeltaAngle(bogBearing, bearing) >= 0f ? 7f : -7f;
            }

            bearings[i] = bearing;
            AddPath(line, PathKind.Road);
            if (i == bogRoad)
            {
                // On into the bog on a causeway, to its heart.
                Vector2 from = line[line.Count - 1];
                AddPath(Wander(from, BogCentre + (from - BogCentre).normalized * Recipe.bogRadius * 0.12f, 0.1f), PathKind.Trail);
            }
            else if (i != lakeRoad)
            {
                // The mountain road too: something stands at the top.
                RoadEnds.Add(line[line.Count - 1]);
            }
        }

        radialBearings.AddRange(bearings);
        MarkRoads();

        if (Recipe.ringPath <= 0f)
        {
            return;
        }

        bearings = new List<float>(bearings);
        bearings.Sort();
        for (int i = 0; i < bearings.Count; i++)
        {
            float a = bearings[i], b = bearings[(i + 1) % bearings.Count] + (i + 1 == bearings.Count ? 360f : 0f);
            var arc = new List<Vector2>();
            bool open = true;
            for (float t = a; t <= b + 0.01f; t += 8f)
            {
                Vector2 point = Dir(t) * (Recipe.ringPath + (Perlin(Dir(t) * 50f, 30f, 7f) - 0.5f) * 30f);
                if (Lake.Edge(point) < 12f || MountainWeight(point) > 0.25f || BogWeight(point) > 0.05f)
                {
                    open = false;
                    break;
                }

                arc.Add(point);
            }

            if (open && arc.Count >= 3)
            {
                // Its ends on the roads it joins, not a few metres short of them.
                arc[0] = NearestOn(Paths, arc[0]);
                arc[arc.Count - 1] = NearestOn(Paths, arc[arc.Count - 1]);
                AddPath(arc, PathKind.Ring);
            }
        }
    }

    // How far each point is from the nearest road out from the Haven, over a grid: the land comes down into a valley along each.
    private void MarkRoads()
    {
        roadSize = Mathf.CeilToInt(Recipe.size / RoadCell);
        float reach = Recipe.roadValleyWidth + RoadCell * 2f;
        var field = new float[roadSize * roadSize];
        for (int i = 0; i < field.Length; i++)
        {
            field[i] = reach;
        }

        for (int p = 0; p < Paths.Count; p++)
        {
            if (PathKinds[p] != PathKind.Road)
            {
                continue;
            }

            for (int i = 0; i + 1 < Paths[p].Count; i++)
            {
                Vector2 a = Paths[p][i], b = Paths[p][i + 1];
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach + Half) / RoadCell));
                int x1 = Mathf.Min(roadSize - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach + Half) / RoadCell));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach + Half) / RoadCell));
                int y1 = Mathf.Min(roadSize - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach + Half) / RoadCell));
                Vector2 along = b - a;
                float squared = Mathf.Max(0.0001f, along.sqrMagnitude);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var point = new Vector2((x + 0.5f) * RoadCell - Half, (y + 0.5f) * RoadCell - Half);
                        float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / squared);
                        int cell = y * roadSize + x;
                        field[cell] = Mathf.Min(field[cell], Vector2.Distance(a + along * t, point));
                    }
                }
            }
        }

        roads = field;
    }

    private const float RoadCell = 4f;
    private float[] roads;
    private int roadSize;

    // Metres to the nearest road out from the Haven (far, before the roads are laid or past their valleys' reach).
    private float RoadDistance(Vector2 point)
    {
        if (roads == null)
        {
            return float.MaxValue;
        }

        float x = Mathf.Clamp((point.x + Half) / RoadCell - 0.5f, 0f, roadSize - 1.001f), y = Mathf.Clamp((point.y + Half) / RoadCell - 0.5f, 0f, roadSize - 1.001f);
        int x0 = (int)x, y0 = (int)y;
        float tx = x - x0, ty = y - y0;
        return Mathf.Lerp(Mathf.Lerp(roads[y0 * roadSize + x0], roads[y0 * roadSize + x0 + 1], tx),
                          Mathf.Lerp(roads[(y0 + 1) * roadSize + x0], roads[(y0 + 1) * roadSize + x0 + 1], tx), ty);
    }

    // The radial closest to a bearing (and not already turned to another) turns to it.
    private static int Snap(List<float> bearings, float to, HashSet<int> taken)
    {
        int best = -1;
        for (int i = 0; i < bearings.Count; i++)
        {
            if (!taken.Contains(i) && (best < 0 || Mathf.Abs(Mathf.DeltaAngle(bearings[i], to)) < Mathf.Abs(Mathf.DeltaAngle(bearings[best], to))))
            {
                best = i;
            }
        }

        if (best >= 0)
        {
            bearings[best] = to;
            taken.Add(best);
        }

        return best;
    }

    // Midpoint displacement: each pass splits every stretch and pushes the middle aside, less each time.
    private List<Vector2> Wander(Vector2 from, Vector2 to, float share)
    {
        var line = new List<Vector2> { from, to };
        float reach = Vector2.Distance(from, to) * share;
        for (int level = 0; level < 4; level++)
        {
            var next = new List<Vector2>(line.Count * 2) { line[0] };
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 side = Vector2.Perpendicular((line[i + 1] - line[i]).normalized);
                next.Add((line[i] + line[i + 1]) * 0.5f + side * Range(-reach, reach));
                next.Add(line[i + 1]);
            }

            line = next;
            reach *= 0.5f;
        }

        return line;
    }

    private void PlacePois()
    {
        foreach (HavenPointOfInterest rule in Recipe.pointsOfInterest)
        {
            if (rule == null || rule.prefab == null)
            {
                continue;
            }

            for (int i = 0; i < rule.count; i++)
            {
                if (rule.onIsland)
                {
                    // The lake's islands first (reached by boat), then a mere's.
                    Island island = Islands.Find(one => one.Water == Lake && one.Radius >= rule.clearing * 0.8f && !Pois.Exists(poi => Vector2.Distance(poi.At, one.Centre) < 1f))
                                    ?? Islands.Find(one => one.Radius >= rule.clearing * 0.8f && !Pois.Exists(poi => Vector2.Distance(poi.At, one.Centre) < 1f));
                    if (island != null)
                    {
                        AddPoi(rule, island.Centre, Range(0f, 360f));
                    }

                    continue;
                }

                if (rule.atRoadEnd)
                {
                    // Just past the end of a road that has nothing at its end yet: the road leads to it.
                    int road = RoadEnds.FindIndex(end => !Pois.Exists(poi => Vector2.Distance(poi.At, end) < 40f));
                    if (road >= 0)
                    {
                        Vector2 end = RoadEnds[road];
                        AddPoi(rule, end + end.normalized * rule.clearing * 0.6f, BearingOf(-end));
                    }

                    continue;
                }

                Vector2 best = default;
                float bestHeight = float.MinValue;
                int found = 0;
                for (int tries = 0; tries < Tries && found < (rule.high ? 12 : 1); tries++)
                {
                    Vector2 at = PoiCandidate(rule);
                    if (!PoiFits(rule, at))
                    {
                        continue;
                    }

                    found++;
                    float height = Land(at);
                    if (height > bestHeight)
                    {
                        bestHeight = height;
                        best = at;
                    }
                }

                if (found == 0)
                {
                    continue;
                }

                AddPoi(rule, best, Range(0f, 360f));
                if (rule.path)
                {
                    AddSpur(best, rule.clearing);
                }
            }
        }
    }

    // The land's height is read before the place is added: once it is, the land there is levelled to that height.
    private void AddPoi(HavenPointOfInterest rule, Vector2 at, float turn)
    {
        Pois.Add(new Poi { Rule = rule, At = at, Turn = turn, Land = rule.inBog ? BogBench : Tier(at) * Recipe.terraceStep });
    }

    // Somewhere a place of this kind may stand: in the bog's heart, on a shore, or anywhere between its distances.
    private Vector2 PoiCandidate(HavenPointOfInterest rule)
    {
        if (rule.inBog && HasBog)
        {
            return BogCentre + Around(0f, Recipe.bogRadius * 0.55f);
        }

        if (rule.onShore)
        {
            // A mere's shore two times in three, when there is one: the lake has its landing.
            RaidLayoutPond water = Meres.Count > 0 && Range(0f, 1f) < 0.67f ? Meres[random.Next(Meres.Count)] : Lake;
            return water.Rim(Range(0f, Mathf.PI * 2f), rule.clearing + 3f);
        }

        return Around(rule.distance.x, Mathf.Min(rule.distance.y, MaxReach));
    }

    private bool PoiFits(HavenPointOfInterest rule, Vector2 at)
    {
        float edge = Half - Mathf.Max(Mathf.Abs(at.x), Mathf.Abs(at.y));
        if (edge < Recipe.rim.y + rule.clearing + 6f || at.magnitude < Recipe.buildable + rule.clearing + 30f
            || Pois.Exists(poi => Vector2.Distance(poi.At, at) < 50f) || Near(Rivers, at, rule.clearing + 8f))
        {
            return false;
        }

        if (rule.inBog)
        {
            return BogWeight(at) > 0.8f && Near(Paths, at, rule.clearing + 45f) && !Near(Paths, at, rule.clearing * 0.5f);
        }

        // On one bench: the ground under a place is levelled anyway, but a cliff close by would crowd its clearing.
        return !InWater(at, rule.onShore ? 2f : rule.clearing + 8f) && BogWeight(at) <= 0f && OneBench(at, rule.clearing + 3f)
               && (!rule.inMeadows || WoodsWeight(at) < 0.35f)
               && !Near(Paths, at, rule.clearing + 3f) && Near(Paths, at, rule.clearing + (rule.onShore ? 110f : 70f))
               && !Meres.Exists(mere => !rule.onShore && mere.Edge(at) < rule.clearing + MereBasin);
    }

    // A short path from the nearest path to the edge of a place: round water and rock if it can, straight if it cannot.
    private void AddSpur(Vector2 to, float clearing)
    {
        Vector2 from = NearestOn(Paths, to);
        Vector2 end = to + (from - to).normalized * clearing * 0.5f;
        if (Vector2.Distance(from, end) <= 4f)
        {
            return;
        }

        List<Vector2> line = null;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            line = Wander(from, end, 0.1f);
            if (!line.GetRange(1, line.Count - 2).Exists(point => InWater(point, 2f) && BogWeight(point) <= 0f))
            {
                break;
            }
        }

        AddPath(line, PathKind.Spur);
    }

    // A place with one way in is a dead end. Some get a second: a trail on to the nearest glade, so a walk there carries on somewhere.
    private void LinkPlaces()
    {
        foreach (Poi poi in Pois)
        {
            if (!poi.Rule.path || poi.Rule.inBog || Range(0f, 1f) >= Recipe.placeLinks)
            {
                continue;
            }

            Glade nearest = null;
            float best = 170f;
            foreach (Glade glade in Glades)
            {
                float distance = Vector2.Distance(glade.Centre, poi.At) - glade.Radius;
                // Not the glade the place's own spur already passes.
                if (distance > 35f && distance < best)
                {
                    best = distance;
                    nearest = glade;
                }
            }

            if (nearest == null)
            {
                continue;
            }

            Vector2 from = poi.At + (nearest.Centre - poi.At).normalized * poi.Rule.clearing * 0.5f;
            List<Vector2> link = TrailTo(nearest, from, 190f);
            if (link != null && !SameAs(link))
            {
                AddPath(link, PathKind.Trail);
            }
        }
    }

    private void AddPath(List<Vector2> line, PathKind kind)
    {
        Paths.Add(line);
        PathKinds.Add(kind);
    }
}
