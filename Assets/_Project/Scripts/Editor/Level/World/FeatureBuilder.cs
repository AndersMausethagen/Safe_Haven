using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds one <see cref="WorldFeature"/> for the World Builder, in stages called in feature order: Prepare (sample the
/// spline; read the ground shaped so far), Heights, Paint, ClearDetails / ClearsTree (grass and terrain trees), then,
/// after the terrain is written, Objects (meshes and placed objects under the feature's Generated child).
/// </summary>
public abstract class FeatureBuilder
{
    protected const float SampleStep = 0.5f;

    public readonly WorldFeature Feature;
    public SplineLine Line { get; protected set; }
    protected System.Random Random;

    protected FeatureBuilder(WorldFeature feature)
    {
        Feature = feature;
    }

    public static FeatureBuilder For(WorldFeature feature) => feature switch
    {
        RiverFeature river => new RiverBuilder(river),
        PathFeature path => new PathBuilder(path),
        CliffFeature cliff => new CliffBuilder(cliff),
        BiomeArea area => new AreaBuilder(area),
        _ => null,
    };

    /// <summary>Terrain layers this feature paints (the builder adds missing ones to the terrain first).</summary>
    public virtual IEnumerable<TerrainLayer> PaintLayers
    {
        get { yield break; }
    }

    public virtual void Prepare(WorldSampler ground)
    {
        Line = SplineLine.Sample(Feature.Container, SampleStep);
        Random = new System.Random(Feature.Seed);
    }

    public virtual void Heights(HeightLayer layer) { }

    public virtual void Paint(PaintLayer layer) { }

    public virtual void ClearDetails(ClearLayer layer) { }

    public virtual bool ClearsTree(Vector2 xz) => false;

    public virtual void Objects(Transform root, BuildContext context) { }

    /// <summary>Distance (metres) from the feature's edge (water's edge, path edge, cliff face); 0 inside it,
    /// infinity for features that have none.</summary>
    public virtual float EdgeDistance(Vector2 xz) => float.PositiveInfinity;

    /// <summary>Runs <paramref name="cell"/> for every grid cell within <paramref name="reach"/> of the line.</summary>
    protected void ForField(TerrainGrid grid, RectInt clip, float reach, System.Action<int, int, float, int, float, int> cell)
    {
        LineField field = LineField.Build(Line, grid, reach);
        int x0 = Mathf.Max(field.Rect.xMin, clip.xMin), x1 = Mathf.Min(field.Rect.xMax, clip.xMax);
        int y0 = Mathf.Max(field.Rect.yMin, clip.yMin), y1 = Mathf.Min(field.Rect.yMax, clip.yMax);
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int i = field.IndexOf(x, y);
                float d = field.Distance[i];
                if (!float.IsPositiveInfinity(d))
                {
                    cell(x, y, d, field.Segment[i], field.Fraction[i], field.Side[i]);
                }
            }
        }
    }

    /// <summary>A moving average over <paramref name="window"/> samples each side, repeated <paramref name="passes"/> times.</summary>
    protected static float[] Smoothed(float[] values, int window, int passes)
    {
        float[] current = (float[])values.Clone();
        for (int pass = 0; pass < passes; pass++)
        {
            var next = new float[current.Length];
            for (int i = 0; i < current.Length; i++)
            {
                float sum = 0f;
                int count = 0;
                for (int k = -window; k <= window; k++)
                {
                    int j = Mathf.Clamp(i + k, 0, current.Length - 1);
                    sum += current[j];
                    count++;
                }

                next[i] = sum / count;
            }

            current = next;
        }

        return current;
    }

    /// <summary>Random palette objects scattered beside the line: <paramref name="offset"/> picks the signed distance
    /// from the line (metres) for a sample; <paramref name="accept"/> can refuse a ground point.</summary>
    protected int ScatterAlong(ScatterPalette palette, float per10m, Transform root, BuildContext context,
                               System.Func<int, float, float> offset, System.Func<Vector3, bool> accept, float extraSink = 0f)
    {
        if (palette == null || per10m <= 0f || Line.Length <= 0f)
        {
            return 0;
        }

        int wanted = Mathf.RoundToInt(Line.Length / 10f * per10m);
        int placed = 0;
        for (int attempt = 0; attempt < wanted * 4 && placed < wanted; attempt++)
        {
            float along = (float)Random.NextDouble() * Line.Length;
            int segment = SegmentAt(along, out float f);
            Vector2 direction = Line.Directions[segment];
            Vector2 left = new Vector2(-direction.y, direction.x);
            float across = offset(segment, f);
            Vector2 xz = SplineLine.Flat(Line.PointAt(segment, f)) + left * across;
            ScatterPalette.Entry entry = palette.Pick(Random);
            if (entry == null || !context.Spacing.IsFree(xz, palette.spacing * 0.5f) ||
                !WorldGround.TryGround(xz, out Vector3 ground, out Vector3 normal) || (accept != null && !accept(ground)))
            {
                continue;
            }

            WorldScatter.Place(entry, ground, normal, root, Random, extraSink);
            context.Spacing.Add(xz, palette.spacing * 0.5f);
            placed++;
        }

        return placed;
    }

    protected int SegmentAt(float along, out float fraction)
    {
        float[] a = Line.Along;
        int lo = 0, hi = a.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (a[mid] <= along)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        float span = a[hi] - a[lo];
        fraction = span > 1e-6f ? Mathf.Clamp01((along - a[lo]) / span) : 0f;
        return lo;
    }
}

/// <summary>What the object stage of a build shares: spacing between everything placed, the other builders (for
/// keep-away distances) and the terrains' paint for ground rules.</summary>
public sealed class BuildContext
{
    public readonly WorldScatter.Spacing Spacing = new WorldScatter.Spacing();
    public readonly List<FeatureBuilder> Builders = new List<FeatureBuilder>();
    private readonly Dictionary<Terrain, float[,,]> alphamaps = new Dictionary<Terrain, float[,,]>();

    /// <summary>Weight of the given layers on the ground at a point (0-1).</summary>
    public float GroundWeight(Vector2 xz, TerrainLayer[] layers)
    {
        Terrain terrain = WorldGround.TerrainAt(xz);
        if (terrain == null || layers == null || layers.Length == 0)
        {
            return 1f;
        }

        TerrainData data = terrain.terrainData;
        if (!alphamaps.TryGetValue(terrain, out float[,,] maps))
        {
            maps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            alphamaps[terrain] = maps;
        }

        Vector2 g = TerrainGrid.Alphas(terrain).GridPosition(xz);
        int x = Mathf.Clamp(Mathf.RoundToInt(g.x), 0, data.alphamapWidth - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(g.y), 0, data.alphamapHeight - 1);
        float weight = 0f;
        TerrainLayer[] terrainLayers = data.terrainLayers;
        for (int l = 0; l < terrainLayers.Length; l++)
        {
            if (System.Array.IndexOf(layers, terrainLayers[l]) >= 0)
            {
                weight += maps[y, x, l];
            }
        }

        return weight;
    }

    public float DistanceTo<T>(Vector2 xz) where T : FeatureBuilder
    {
        float best = float.PositiveInfinity;
        foreach (FeatureBuilder builder in Builders)
        {
            if (builder is T)
            {
                best = Mathf.Min(best, builder.EdgeDistance(xz));
            }
        }

        return best;
    }
}
