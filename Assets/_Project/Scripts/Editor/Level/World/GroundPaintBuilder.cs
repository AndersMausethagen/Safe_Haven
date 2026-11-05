using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds a <see cref="GroundPaintArea"/>: inside its loop, each of its paint rules lays its layer by the ground's
/// steepness, height and hollows, broken into patches (WorldPaint.RuleWeight, as the terrain's auto-paint), faded in
/// from the edge over Edge Fade with noise so the border frays instead of drawing a line.
/// </summary>
public sealed class GroundPaintBuilder : FeatureBuilder
{
    private readonly GroundPaintArea area;

    public GroundPaintBuilder(GroundPaintArea area) : base(area)
    {
        this.area = area;
    }

    public override IEnumerable<TerrainLayer> PaintLayers
    {
        get
        {
            if (area.Rules == null) yield break;
            foreach (TerrainPaintRules.Rule rule in area.Rules.Rules)
            {
                if (rule.layer != null) yield return rule.layer;
            }
        }
    }

    public override void Paint(PaintLayer layer)
    {
        if (area.Rules == null || Line == null || Line.Count < 3)
        {
            return;
        }

        TerrainData data = layer.Terrain.terrainData;
        Vector3 origin = layer.Terrain.transform.position;
        Vector3 size = data.size;
        Bounds bounds = area.WorldBounds();
        var rules = area.Rules.Rules;
        var index = new int[rules.Count];
        for (int r = 0; r < rules.Count; r++)
        {
            index[r] = layer.LayerIndex(rules[r].layer);
        }

        // Testing every cell against the whole outline (inside, then nearest edge) cost cells × outline points: about
        // 17 s for a 400 m grove. The outline's crossings are found once per row and the edge is looked up in a bucket
        // grid instead; both give the same answers as SplineLine.Inside and Nearest.
        var crossings = new List<float>();
        var edges = area.EdgeFade > 0f ? new EdgeGrid(Line, area.EdgeFade * 2f) : null;
        float du = WorldPaint.HollowRadius / size.x, dv = WorldPaint.HollowRadius / size.z;
        for (int y = layer.Rect.yMin; y < layer.Rect.yMax; y++)
        {
            float rowZ = layer.Grid.World(layer.Rect.xMin, y).y;
            if (rowZ < bounds.min.z || rowZ > bounds.max.z)
            {
                continue;
            }

            Crossings(Line, rowZ, crossings);
            int passed = 0; // crossings at or left of the cell
            for (int x = layer.Rect.xMin; x < layer.Rect.xMax; x++)
            {
                Vector2 p = layer.Grid.World(x, y);
                while (passed < crossings.Count && crossings[passed] <= p.x)
                {
                    passed++;
                }

                // Even-odd rule, as SplineLine.Inside: inside when an odd number of crossings lie to the right.
                if (p.x < bounds.min.x || p.x > bounds.max.x || ((crossings.Count - passed) & 1) == 0)
                {
                    continue;
                }

                float fade = Fade(p, edges);
                if (fade <= 0f)
                {
                    continue;
                }

                float u = Mathf.Clamp01((p.x - origin.x) / size.x), v = Mathf.Clamp01((p.y - origin.z) / size.z);
                float slope = data.GetSteepness(u, v);
                float h = data.GetInterpolatedHeight(u, v);
                float around = (data.GetInterpolatedHeight(Mathf.Clamp01(u - du), v) + data.GetInterpolatedHeight(Mathf.Clamp01(u + du), v) +
                                data.GetInterpolatedHeight(u, Mathf.Clamp01(v - dv)) + data.GetInterpolatedHeight(u, Mathf.Clamp01(v + dv))) * 0.25f;
                float hollow = WorldPaint.Hollowness(h, around, WorldPaint.HollowRadius);
                for (int r = 0; r < rules.Count; r++)
                {
                    if (index[r] < 0) continue;
                    float noise = WorldNoise2D.Fbm(p.x / rules[r].patchSize, p.y / rules[r].patchSize, area.Rules.Seed + r * 17 + area.Seed);
                    float w = WorldPaint.RuleWeight(rules[r], slope, origin.y + h, hollow, noise);
                    layer.Paint(x, y, index[r], w * fade * area.Strength);
                }
            }
        }
    }

    // 0 at the loop's edge, 1 once Edge Fade inside; the edge frays with noise.
    private float Fade(Vector2 p, EdgeGrid edges)
    {
        if (edges == null)
        {
            return 1f;
        }

        // Past twice Edge Fade from the edge the fray (at most 0.4 × Edge Fade) cannot pull it below full strength.
        if (!edges.Within(p, out float inside))
        {
            return 1f;
        }

        float fray = (WorldNoise2D.Fbm(p.x / 4f, p.y / 4f, area.Seed + 101) - 0.5f) * area.EdgeFade * 0.8f;
        return WorldNoise2D.Smooth(Mathf.Clamp01((inside + fray) / area.EdgeFade));
    }

    /// <summary>Where the closed line crosses the row at <paramref name="z"/>, sorted (the edges SplineLine.Inside counts).</summary>
    private static void Crossings(SplineLine line, float z, List<float> crossings)
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
