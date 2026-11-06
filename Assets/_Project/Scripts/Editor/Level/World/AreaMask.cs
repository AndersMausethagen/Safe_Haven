using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cells of a grid inside a closed line (even-odd, as SplineLine.Inside) and how far in each lies, for areas that
/// paint (Ground Paint Areas, Biome Areas). Testing every cell against the whole outline cost cells × outline points
/// (about 17 s for a 400 m grove); here the outline's crossings are found once per row and the nearest edge is looked
/// up in a bucket grid, with the same answers.
/// </summary>
public sealed class AreaMask
{
    private readonly SplineLine line;
    private readonly Rect bounds;
    private readonly float edgeFade;
    private readonly int seed;
    private readonly EdgeGrid edges;
    private readonly List<float> crossings = new List<float>();

    public AreaMask(SplineLine line, float edgeFade, int seed)
    {
        this.line = line;
        this.edgeFade = edgeFade;
        this.seed = seed;
        bounds = line.FlatBounds(0f);
        edges = edgeFade > 0f ? new EdgeGrid(line, edgeFade * 2f) : null;
    }

    /// <summary>Calls <paramref name="cell"/>(x, y, world point, fade) for every cell of <paramref name="rect"/> inside
    /// the loop whose fade (0 at the edge, 1 once Edge Fade in, frayed) is above zero.</summary>
    public void ForEachInside(TerrainGrid grid, RectInt rect, System.Action<int, int, Vector2, float> cell)
    {
        if (line == null || line.Count < 3)
        {
            return;
        }

        for (int y = rect.yMin; y < rect.yMax; y++)
        {
            float rowZ = grid.World(rect.xMin, y).y;
            if (rowZ < bounds.yMin || rowZ > bounds.yMax)
            {
                continue;
            }

            Crossings(line, rowZ, crossings);
            int passed = 0; // crossings at or left of the cell
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                Vector2 p = grid.World(x, y);
                while (passed < crossings.Count && crossings[passed] <= p.x)
                {
                    passed++;
                }

                // Even-odd rule, as SplineLine.Inside: inside when an odd number of crossings lie to the right.
                if (p.x < bounds.xMin || p.x > bounds.xMax || ((crossings.Count - passed) & 1) == 0)
                {
                    continue;
                }

                float fade = Fade(p);
                if (fade > 0f)
                {
                    cell(x, y, p, fade);
                }
            }
        }
    }

    /// <summary>0 at the loop's edge, 1 once Edge Fade inside; the edge frays with noise. (For a point inside.)</summary>
    public float Fade(Vector2 p)
    {
        // Past twice Edge Fade from the edge the fray (at most 0.4 × Edge Fade) cannot pull it below full strength.
        if (edges == null || !edges.Within(p, out float inside))
        {
            return 1f;
        }

        float fray = (WorldNoise2D.Fbm(p.x / 4f, p.y / 4f, seed + 101) - 0.5f) * edgeFade * 0.8f;
        return WorldNoise2D.Smooth(Mathf.Clamp01((inside + fray) / edgeFade));
    }

    /// <summary>Where the closed line crosses the row at <paramref name="z"/>, sorted (the edges SplineLine.Inside counts).</summary>
    public static void Crossings(SplineLine line, float z, List<float> crossings)
    {
        crossings.Clear();
        Vector3[] points = line.Points;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            Vector3 a = points[i], b = points[j];
            if ((a.z > z) != (b.z > z))
            {
                crossings.Add((b.x - a.x) * (z - a.z) / (b.z - a.z) + a.x);
            }
        }

        crossings.Sort();
    }

    /// <summary>The line's segments in square buckets, so the nearest edge is found among the few near a point.</summary>
    private sealed class EdgeGrid
    {
        private readonly SplineLine line;
        private readonly float cell;
        private readonly Vector2 origin;
        private readonly int width, height;
        private readonly List<int>[] buckets;

        public EdgeGrid(SplineLine line, float cell)
        {
            this.line = line;
            this.cell = cell;
            Rect bounds = line.FlatBounds(cell);
            origin = bounds.min;
            width = Mathf.Max(1, Mathf.CeilToInt(bounds.width / cell));
            height = Mathf.Max(1, Mathf.CeilToInt(bounds.height / cell));
            buckets = new List<int>[width * height];
            for (int i = 0; i + 1 < line.Points.Length; i++)
            {
                Vector2 a = SplineLine.Flat(line.Points[i]), b = SplineLine.Flat(line.Points[i + 1]);
                int x0 = Column(Mathf.Min(a.x, b.x)), x1 = Column(Mathf.Max(a.x, b.x));
                int y0 = Row(Mathf.Min(a.y, b.y)), y1 = Row(Mathf.Max(a.y, b.y));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        (buckets[y * width + x] ??= new List<int>()).Add(i);
                    }
                }
            }
        }

        /// <summary>True, with the exact distance, when the line passes within one bucket of the point.</summary>
        public bool Within(Vector2 p, out float distance)
        {
            distance = float.MaxValue;
            int cx = Column(p.x), cy = Row(p.y);
            for (int y = Mathf.Max(0, cy - 1); y <= Mathf.Min(height - 1, cy + 1); y++)
            {
                for (int x = Mathf.Max(0, cx - 1); x <= Mathf.Min(width - 1, cx + 1); x++)
                {
                    List<int> segments = buckets[y * width + x];
                    if (segments == null) continue;
                    foreach (int i in segments)
                    {
                        float d = SplineLine.SegmentDistance(p, SplineLine.Flat(line.Points[i]), SplineLine.Flat(line.Points[i + 1]), out _);
                        if (d < distance) distance = d;
                    }
                }
            }

            // Anything nearer than one bucket lies in the 3 × 3 around the point, so a hit that close is the nearest.
            return distance <= cell;
        }

        private int Column(float x) => Mathf.Clamp((int)((x - origin.x) / cell), 0, width - 1);
        private int Row(float z) => Mathf.Clamp((int)((z - origin.y) / cell), 0, height - 1);
    }
}
