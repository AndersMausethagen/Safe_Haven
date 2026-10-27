using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds a <see cref="PathFeature"/>: the ground evened out across it (following the smoothed ground along
/// it), sunk a little, painted with a frayed edge, grass and trees cleared, objects along the edges.</summary>
public sealed class PathBuilder : FeatureBuilder
{
    private readonly PathFeature path;
    private float[] halfWidth;
    private float[] centre;
    private float maxHalfWidth;

    public PathBuilder(PathFeature path) : base(path)
    {
        this.path = path;
    }

    public override IEnumerable<TerrainLayer> PaintLayers
    {
        get
        {
            if (path.Layer != null) yield return path.Layer;
        }
    }

    public override void Prepare(WorldSampler ground)
    {
        base.Prepare(ground);
        int n = Line.Count;
        halfWidth = new float[n];
        var heights = new float[n];
        maxHalfWidth = 0f;
        for (int i = 0; i < n; i++)
        {
            halfWidth[i] = path.Width * 0.5f * path.WidthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed);
            maxHalfWidth = Mathf.Max(maxHalfWidth, halfWidth[i]);
            heights[i] = ground.Height(SplineLine.Flat(Line.Points[i]));
        }

        // Even out the bumps along the path over a few metres, so it does not follow every hump of the ground.
        centre = Smoothed(heights, Mathf.RoundToInt(4f / SampleStep), 2);
    }

    /// <summary>How strongly a cell at distance <paramref name="d"/> belongs to the path (1 in the middle, fading over
    /// the soft edge).</summary>
    public static float Weight(float d, float halfWidth, float softEdge) =>
        d <= halfWidth ? 1f : softEdge > 0f ? 1f - WorldNoise2D.Smooth((d - halfWidth) / softEdge) : 0f;

    public override void Heights(HeightLayer layer)
    {
        if (path.Flatten <= 0f && path.Sink <= 0f)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, maxHalfWidth + path.SoftEdge, (x, y, d, s, f, side) =>
        {
            float w = Weight(d, Line.Lerp(halfWidth, s, f), path.SoftEdge);
            if (w <= 0f)
            {
                return;
            }

            float h = layer.Get(x, y);
            h = Mathf.Lerp(h, Line.Lerp(centre, s, f), w * path.Flatten) - path.Sink * w;
            layer.Set(x, y, h);
        });
    }

    public override void Paint(PaintLayer layer)
    {
        int index = layer.LayerIndex(path.Layer);
        if (index < 0)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, maxHalfWidth + path.SoftEdge * 1.6f, (x, y, d, s, f, side) =>
        {
            Vector2 p = layer.Grid.World(x, y);
            float noise = WorldNoise2D.Fbm(p.x / 1.3f, p.y / 1.3f, Feature.Seed);
            float w = Line.Lerp(halfWidth, s, f) * (0.85f + 0.3f * noise);
            float soft = path.SoftEdge * (0.4f + 1.2f * noise);
            layer.Paint(x, y, index, Weight(d, w, soft) * path.PaintStrength);
        });
    }

    public override void ClearDetails(ClearLayer layer)
    {
        if (!path.ClearGrass)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, maxHalfWidth + path.SoftEdge, (x, y, d, s, f, side) =>
        {
            if (d < Line.Lerp(halfWidth, s, f) + path.SoftEdge * 0.25f)
            {
                layer.Clear[layer.IndexOf(x, y)] = true;
            }
        });
    }

    public override bool ClearsTree(Vector2 xz)
    {
        if (!path.RemoveTrees)
        {
            return false;
        }

        float d = Line.Nearest(xz, out int s, out float f);
        return d < Line.Lerp(halfWidth, s, f) + 1f;
    }

    public override float EdgeDistance(Vector2 xz)
    {
        float d = Line.Nearest(xz, out int s, out float f);
        return Mathf.Max(0f, d - Line.Lerp(halfWidth, s, f));
    }

    public override void Objects(Transform root, BuildContext context)
    {
        ScatterAlong(path.EdgeScatter, path.EdgeScatterPer10m, root, context, (s, f) =>
        {
            float d = Line.Lerp(halfWidth, s, f) + path.SoftEdge * (0.5f + (float)Random.NextDouble()) + 0.3f;
            return Random.NextDouble() < 0.5 ? d : -d;
        }, ground => context.DistanceTo<RiverBuilder>(SplineLine.Flat(ground)) > 0.5f);
    }
}
