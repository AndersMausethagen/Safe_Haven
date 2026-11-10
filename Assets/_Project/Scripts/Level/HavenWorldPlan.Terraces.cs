using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// The terraces' cliffs and the ways up them: the lines where one bench meets the next (traced from the land), a ramp wherever a
/// path crosses one, scrambles where a cliff has crumbled into a slope so no bench is cut off, the cliff lines left between
/// them (the generator stands rock along these), and which ground can be walked to from the portal. Part of HavenWorldPlan, in
/// its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float RampCell = 2f;

    private const float ContourCell = 4f;

    private const float ReachCell = 4f;

    // The steepest a creature or the player walks: a rise of this much across one 4 m cell (about 40 degrees).
    private const float WalkableRise = 3.4f;

    // Deeper than this, still water is swum or sailed, not walked.
    private const float WadeDepth = 1.1f;

    /// <summary>Ways up a cliff without a path: where it has crumbled into a slope.</summary>
    public readonly List<Vector2> Scrambles = new List<Vector2>();

    /// <summary>Metres of cliff in the world, and how many of them have rock stood along them (<see cref="Cliffs"/>).</summary>
    public float CliffMetres { get; private set; }
    public float DressedCliffMetres { get; private set; }

    /// <summary>The share of the dry land that can be walked to from the portal, 0 to 1.</summary>
    public float ReachableShare { get; private set; } = 1f;

    private float[] ramps;
    private int rampSize;
    private bool[] reachable;
    private int reachSize;

    /// <summary>How much of a ramp lies at a point, 0 to 1: on a path's way up a cliff, or on a scramble.</summary>
    public float RampAt(Vector2 point)
    {
        if (ramps == null)
        {
            return 0f;
        }

        float x = Mathf.Clamp((point.x + Half) / RampCell - 0.5f, 0f, rampSize - 1.001f), y = Mathf.Clamp((point.y + Half) / RampCell - 0.5f, 0f, rampSize - 1.001f);
        int x0 = (int)x, y0 = (int)y;
        float tx = x - x0, ty = y - y0;
        return Mathf.Lerp(Mathf.Lerp(ramps[y0 * rampSize + x0], ramps[y0 * rampSize + x0 + 1], tx),
                          Mathf.Lerp(ramps[(y0 + 1) * rampSize + x0], ramps[(y0 + 1) * rampSize + x0 + 1], tx), ty);
    }

    /// <summary>True where the ground can be walked to from the portal (always true before the plan has worked it out).</summary>
    public bool Reachable(Vector2 point)
    {
        if (reachable == null)
        {
            return true;
        }

        int x = Mathf.Clamp(Mathf.FloorToInt((point.x + Half) / ReachCell), 0, reachSize - 1), y = Mathf.Clamp(Mathf.FloorToInt((point.y + Half) / ReachCell), 0, reachSize - 1);
        return reachable[y * reachSize + x];
    }

    // Once every path is laid: ramps where they cross the cliffs, scrambles between, and the cliff lines that are left.
    private void BuildRamps()
    {
        List<List<Vector2>> contours = Contours();
        rampSize = Mathf.CeilToInt(Recipe.size / RampCell);
        var field = new float[rampSize * rampSize];
        for (int p = 0; p < Paths.Count; p++)
        {
            float full = PathWidthOf(PathKinds[p]) * 0.5f + 2.5f;
            for (int i = 0; i + 1 < Paths[p].Count; i++)
            {
                Stamp(field, Paths[p][i], Paths[p][i + 1], full, 6f);
            }
        }

        // From here on the ground knows its ramps.
        ramps = field;
        foreach (List<Vector2> contour in contours)
        {
            PlaceScrambles(contour);
        }

        foreach (List<Vector2> contour in contours)
        {
            AddCliffs(contour);
        }
    }

    private float PathWidthOf(PathKind kind) => kind switch
    {
        PathKind.Road => Recipe.roadWidth,
        PathKind.Ring => Recipe.ringWidth,
        PathKind.Spur => Recipe.spurWidth,
        PathKind.Loop => Recipe.loopWidth,
        _ => Recipe.trailWidth,
    };

    // Marks a ramp along a stretch: whole within `full` metres of it, fading out over `fade` more.
    private void Stamp(float[] field, Vector2 a, Vector2 b, float full, float fade)
    {
        float reach = full + fade;
        int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach + Half) / RampCell));
        int x1 = Mathf.Min(rampSize - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach + Half) / RampCell));
        int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach + Half) / RampCell));
        int y1 = Mathf.Min(rampSize - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach + Half) / RampCell));
        Vector2 along = b - a;
        float squared = Mathf.Max(0.0001f, along.sqrMagnitude);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                var point = new Vector2((x + 0.5f) * RampCell - Half, (y + 0.5f) * RampCell - Half);
                float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / squared);
                float weight = 1f - Ease(full, reach, Vector2.Distance(a + along * t, point));
                int cell = y * rampSize + x;
                if (weight > field[cell])
                {
                    field[cell] = weight;
                }
            }
        }
    }

    // The lines where one bench meets the next: where the land crosses half a step, traced over a grid (marching squares) and
    // joined into runs. A closed run (a table rock, a hollow) ends where it began.
    private List<List<Vector2>> Contours()
    {
        int cells = Mathf.CeilToInt(Recipe.size / ContourCell), corners = cells + 1;
        var land = new float[corners * corners];
        float step = Recipe.terraceStep;
        Parallel.For(0, corners, y =>
        {
            for (int x = 0; x < corners; x++)
            {
                land[y * corners + x] = Land(new Vector2(x * ContourCell - Half, y * ContourCell - Half)) / step;
            }
        });

        float lowest = float.MaxValue, highest = float.MinValue;
        foreach (float value in land)
        {
            lowest = Mathf.Min(lowest, value);
            highest = Mathf.Max(highest, value);
        }

        var runs = new List<List<Vector2>>();
        for (int tier = Mathf.FloorToInt(lowest); tier <= Mathf.CeilToInt(highest); tier++)
        {
            float level = tier + 0.5f;
            if (level <= lowest || level >= highest)
            {
                continue;
            }

            // Each crossing of a grid edge, by the edge it lies on; each cell joins two of them (four in a saddle).
            var points = new Dictionary<int, Vector2>();
            var links = new Dictionary<int, List<int>>();

            int Edge(int x, int y, bool upright) => (y * corners + x) * 2 + (upright ? 1 : 0);

            int Cross(int x, int y, bool upright)
            {
                int key = Edge(x, y, upright);
                if (!points.ContainsKey(key))
                {
                    float a = land[y * corners + x], b = upright ? land[(y + 1) * corners + x] : land[y * corners + x + 1];
                    float t = Mathf.Clamp01((level - a) / (Mathf.Abs(b - a) < 1e-6f ? 1e-6f : b - a));
                    points[key] = new Vector2((x + (upright ? 0f : t)) * ContourCell - Half, (y + (upright ? t : 0f)) * ContourCell - Half);
                }

                return key;
            }

            void Join(int a, int b)
            {
                if (!links.TryGetValue(a, out List<int> from))
                {
                    links[a] = from = new List<int>(2);
                }

                if (!links.TryGetValue(b, out List<int> to))
                {
                    links[b] = to = new List<int>(2);
                }

                from.Add(b);
                to.Add(a);
            }

            for (int y = 0; y < cells; y++)
            {
                for (int x = 0; x < cells; x++)
                {
                    int kind = (land[y * corners + x] > level ? 1 : 0) | (land[y * corners + x + 1] > level ? 2 : 0)
                             | (land[(y + 1) * corners + x + 1] > level ? 4 : 0) | (land[(y + 1) * corners + x] > level ? 8 : 0);
                    if (kind == 0 || kind == 15)
                    {
                        continue;
                    }

                    int bottom = Edge(x, y, false), right = Edge(x + 1, y, true), top = Edge(x, y + 1, false), left = Edge(x, y, true);
                    void Line(int a, int b) => Join(Cross(a / 2 % corners, a / 2 / corners, a % 2 == 1), Cross(b / 2 % corners, b / 2 / corners, b % 2 == 1));
                    switch (kind)
                    {
                        case 1: case 14: Line(left, bottom); break;
                        case 2: case 13: Line(bottom, right); break;
                        case 3: case 12: Line(left, right); break;
                        case 4: case 11: Line(right, top); break;
                        case 6: case 9: Line(bottom, top); break;
                        case 7: case 8: Line(left, top); break;
                        case 5: Line(left, top); Line(bottom, right); break;
                        default: Line(left, bottom); Line(right, top); break;
                    }
                }
            }

            // Walk each run from an end (a crossing with one neighbour, at the world's edge) or, for a closed one, from anywhere.
            var used = new HashSet<int>();
            foreach (bool ends in new[] { true, false })
            {
                foreach (KeyValuePair<int, List<int>> start in links)
                {
                    if (used.Contains(start.Key) || (ends && start.Value.Count != 1))
                    {
                        continue;
                    }

                    var run = new List<Vector2>();
                    int at = start.Key, before = -1;
                    while (at >= 0 && used.Add(at))
                    {
                        run.Add(points[at]);
                        int next = -1;
                        foreach (int other in links[at])
                        {
                            if (other != before && !used.Contains(other))
                            {
                                next = other;
                                break;
                            }
                        }

                        before = at;
                        at = next;
                    }

                    if (!ends && run.Count > 2)
                    {
                        run.Add(run[0]);
                    }

                    if (run.Count >= 3)
                    {
                        runs.Add(run);
                    }
                }
            }
        }

        return runs;
    }

    private static float Length(List<Vector2> line)
    {
        float length = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            length += Vector2.Distance(line[i], line[i + 1]);
        }

        return length;
    }

    // Deep in the rim the ledges are the world's wall: no ways up, no rock stood along them.
    private bool InTheRim(Vector2 point) => Half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y)) < Recipe.rim.y - 8f;

    // Along a cliff, a way up about every so many metres where no path already climbs it; a closed one (a table rock) gets at
    // least one, so its top can be reached.
    private void PlaceScrambles(List<Vector2> contour)
    {
        float length = Length(contour);
        if (length < 45f)
        {
            return;
        }

        bool closed = (contour[0] - contour[contour.Count - 1]).sqrMagnitude < 0.01f;
        bool any = false;
        float next = Range(Recipe.scrambleSpacing.x, Recipe.scrambleSpacing.y) * (closed ? 0.25f : 0.5f);
        float walked = 0f;
        int fallback = -1;
        for (int i = 0; i + 1 < contour.Count; i++)
        {
            Vector2 at = contour[i];
            walked += Vector2.Distance(at, contour[i + 1]);
            if (RampAt(at) > 0.3f)
            {
                // A path climbs here: the next way up is a good walk on.
                any = true;
                next = walked + Range(Recipe.scrambleSpacing.x, Recipe.scrambleSpacing.y);
                continue;
            }

            if (!ScrambleFits(at))
            {
                continue;
            }

            fallback = i;
            if (walked >= next)
            {
                AddScramble(at);
                any = true;
                next = walked + Range(Recipe.scrambleSpacing.x, Recipe.scrambleSpacing.y);
            }
        }

        if (!any && fallback >= 0)
        {
            AddScramble(contour[fallback]);
        }
    }

    private bool ScrambleFits(Vector2 at) =>
        !InTheRim(at) && at.magnitude > Recipe.buildable + 30f && !InWater(at, 10f) && !Near(Rivers, at, 16f)
        && !Pois.Exists(poi => Vector2.Distance(poi.At, at) < poi.Rule.clearing + 14f)
        && !Ravines.Exists(ravine => NearLine(ravine.Line, at, ravine.FloorWidth + 16f))
        && !Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, at) < outcrop.Radius + 14f)
        && !Scrambles.Exists(other => (other - at).sqrMagnitude < 40f * 40f);

    private void AddScramble(Vector2 at)
    {
        Scrambles.Add(at);
        Stamp(ramps, at, at, Recipe.scrambleRadius, Recipe.scrambleRadius * 0.7f);
    }

    // What is left of a cliff between its ramps, rivers and water: runs long enough get rock stood along them. Each line is laid
    // along the top of its face with the high ground on its left.
    private void AddCliffs(List<Vector2> contour)
    {
        var run = new List<Vector2>();
        for (int i = 0; i < contour.Count; i++)
        {
            Vector2 at = contour[i];
            if (i + 1 < contour.Count && !InTheRim(at))
            {
                CliffMetres += Vector2.Distance(at, contour[i + 1]);
            }

            if (CliffStands(at))
            {
                run.Add(at);
                continue;
            }

            AddCliff(run);
            run = new List<Vector2>();
        }

        AddCliff(run);
    }

    // Rock is stood where a cliff is walked beside: near a path, round a glade, by a place.
    private bool WalkedBeside(Vector2 at)
    {
        float near = Recipe.cliffRockNear;
        return Near(Paths, at, near) || Glades.Exists(glade => (glade.Centre - at).sqrMagnitude < (glade.Radius + near * 0.5f) * (glade.Radius + near * 0.5f))
               || Pois.Exists(poi => (poi.At - at).sqrMagnitude < (poi.Rule.clearing + near) * (poi.Rule.clearing + near));
    }

    private bool CliffStands(Vector2 at) =>
        RampAt(at) < 0.2f && !InTheRim(at) && WalkedBeside(at) && at.magnitude > Recipe.buildable + 15f && !InWater(at, 3f) && !Near(Rivers, at, Recipe.riverWidth.y + 6f)
        && !Ravines.Exists(ravine => NearLine(ravine.Line, at, ravine.FloorWidth + 7f));

    private void AddCliff(List<Vector2> run)
    {
        float length = Length(run);
        if (run.Count < 3 || length < Recipe.cliffMinLength)
        {
            return;
        }

        // A knot about every 9 m, moved from the middle of the face to its top.
        var line = new List<Vector2>();
        float walked = 0f, next = 0f;
        for (int i = 0; i < run.Count; i++)
        {
            if (i > 0)
            {
                walked += Vector2.Distance(run[i - 1], run[i]);
            }

            if (walked >= next || i == run.Count - 1)
            {
                line.Add(run[i] + Uphill(run[i]) * (Recipe.riserWidth * 0.5f + 0.3f));
                next = walked + 9f;
            }
        }

        if (line.Count < 3)
        {
            return;
        }

        Vector2 along = line[line.Count / 2 + 1 < line.Count ? line.Count / 2 + 1 : line.Count - 1] - line[line.Count / 2 - 1];
        if (Vector2.Dot(new Vector2(-along.y, along.x), Uphill(run[run.Count / 2])) < 0f)
        {
            line.Reverse();
        }

        DressedCliffMetres += length;
        Cliffs.Add(new Cliff { Line = line, Height = Recipe.terraceStep, HighOnLeft = true });
    }

    // The way the land climbs at a point.
    private Vector2 Uphill(Vector2 point)
    {
        const float apart = 3f;
        var slope = new Vector2(Land(point + new Vector2(apart, 0f)) - Land(point - new Vector2(apart, 0f)),
                                Land(point + new Vector2(0f, apart)) - Land(point - new Vector2(0f, apart)));
        return slope.sqrMagnitude > 1e-8f ? slope.normalized : Vector2.zero;
    }

    // What can be walked to from the portal: over a grid, from each cell to its neighbours where the ground rises no faster than
    // can be walked and the water is shallow enough to wade. What stands on ground that cannot be reached is not placed there.
    private void FindReachable()
    {
        reachSize = Mathf.CeilToInt(Recipe.size / ReachCell);
        int size = reachSize;
        var heights = new float[size * size];
        var deep = new bool[size * size];
        Parallel.For(0, size, y =>
        {
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2((x + 0.5f) * ReachCell - Half, (y + 0.5f) * ReachCell - Half);
                heights[y * size + x] = Height(point);
                deep[y * size + x] = Lake.Edge(point) < 0f && Lake.Level - heights[y * size + x] > WadeDepth
                                     || Meres.Exists(mere => mere.Edge(point) < 0f && mere.Level - heights[y * size + x] > WadeDepth);
            }
        });

        var found = new bool[size * size];
        var queue = new Queue<int>();
        int start = size / 2 * size + size / 2;
        found[start] = true;
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            int cell = queue.Dequeue();
            int x = cell % size, y = cell / size;
            for (int k = 0; k < 4; k++)
            {
                int nx = x + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                {
                    continue;
                }

                int other = ny * size + nx;
                if (!found[other] && !deep[other] && Mathf.Abs(heights[other] - heights[cell]) <= WalkableRise)
                {
                    found[other] = true;
                    queue.Enqueue(other);
                }
            }
        }

        int dry = 0, walked = 0;
        for (int i = 0; i < found.Length; i++)
        {
            int x = i % size, y = i / size;
            var point = new Vector2((x + 0.5f) * ReachCell - Half, (y + 0.5f) * ReachCell - Half);
            if (deep[i] || InTheRim(point))
            {
                continue;
            }

            dry++;
            if (found[i])
            {
                walked++;
            }
        }

        reachable = found;
        ReachableShare = dry > 0 ? walked / (float)dry : 1f;
    }
}
