using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds a <see cref="PathFeature"/>: the ground evened out across it (following the smoothed ground along
/// it), sunk a little, painted with a frayed edge, grass and trees cleared, objects along the edges. Its natural look
/// (all off by default): a width that swells and pinches along it, paint worn through in patches (least in the middle),
/// a lip at the edges that catches the light, and a ragged grass edge with tufts left in it.</summary>
public sealed class PathBuilder : FeatureBuilder
{
    private readonly PathFeature path;
    private float[] halfWidth;
    private float[] centre;
    private float[] cut;
    private float maxHalfWidth;
    private float maxBank;

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
        float along = 0f;
        for (int i = 0; i < n; i++)
        {
            along += i > 0 ? Vector3.Distance(Line.Points[i - 1], Line.Points[i]) : 0f;
            // Swells and pinches every dozen metres or so, the way feet wear a track.
            float wander = 1f + path.WidthWander * 0.45f * (WorldNoise2D.Fbm(along / 12f, 0.5f, Feature.Seed + 31) * 2f - 1f);
            halfWidth[i] = path.Width * 0.5f * wander * path.WidthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed);
            maxHalfWidth = Mathf.Max(maxHalfWidth, halfWidth[i]);
            heights[i] = ground.Height(SplineLine.Flat(Line.Points[i]));
        }

        // Even out the bumps along the path over a few metres, so it does not follow every hump of the ground; a graded path over
        // tens of metres, cutting into rises and filling dips. How deep it cuts sets how far out its banks reach.
        centre = Smoothed(heights, Mathf.RoundToInt(path.GradeSmoothing / SampleStep), 2);
        cut = new float[n];
        maxBank = path.SoftEdge;
        for (int i = 0; i < n; i++)
        {
            cut[i] = Mathf.Abs(heights[i] - centre[i]);
            maxBank = Mathf.Max(maxBank, Bank(cut[i]));
        }
    }

    /// <summary>How strongly a cell at distance <paramref name="d"/> belongs to the path (1 in the middle, fading over
    /// the soft edge).</summary>
    // Old paths (4 m) keep their soft edge as it was; a graded one's banks widen with the cut.
    private float Bank(float depth) => path.SoftEdge + (path.GradeSmoothing > 4.01f ? Mathf.Min(depth * PathFeature.BankPerMetre, PathFeature.MaxBank) : 0f);

    public static float Weight(float d, float halfWidth, float softEdge) =>
        d <= halfWidth ? 1f : softEdge > 0f ? 1f - WorldNoise2D.Smooth((d - halfWidth) / softEdge) : 0f;

    public override void Heights(HeightLayer layer)
    {
        if (path.Flatten <= 0f && path.Sink <= 0f)
        {
            return;
        }

        float lipWidth = path.SoftEdge * 0.5f + 0.3f;
        float reach = maxHalfWidth + maxBank + (path.Shoulder > 0f ? lipWidth * 2f : 0f);
        ForField(layer.Grid, layer.Rect, reach, (x, y, d, s, f, side) =>
        {
            float half = Line.Lerp(halfWidth, s, f);
            float bank = Bank(Line.Lerp(cut, s, f));
            float level = Weight(d, half, bank);
            float w = Weight(d, half, path.SoftEdge);
            // Worn deepest along the middle; the edges keep a soft lip that the low sun picks out from above.
            float lip = path.Shoulder > 0f ? path.Shoulder * Mathf.Exp(-Mathf.Pow((d - half) / lipWidth, 2f)) : 0f;
            if (level <= 0f && lip < 0.001f)
            {
                return;
            }

            float across = Mathf.Clamp01(d / Mathf.Max(0.1f, half));
            float worn = path.Sink * w * (1f - 0.5f * across * across);
            float h = layer.Get(x, y);
            // The tread is levelled across (Flatten); the banks of a graded path run all the way to its height.
            float toGrade = d <= half ? path.Flatten : Mathf.Max(level * (path.GradeSmoothing > 4.01f ? 1f : 0f), w * path.Flatten);
            h = Mathf.Lerp(h, Line.Lerp(centre, s, f), toGrade) - worn + lip;
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
            float strength = Weight(d, w, soft) * path.PaintStrength;
            if (path.Patchiness > 0f && strength > 0f)
            {
                // Patches of the ground showing through, a few metres across, fewer towards the worn middle.
                float patch = WorldNoise2D.Fbm(p.x / 3.4f, p.y / 3.4f, Feature.Seed + 17);
                float middle = 1f - Mathf.Clamp01(d / Mathf.Max(0.1f, w));
                float keep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.65f, patch + middle * 0.45f));
                strength *= Mathf.Lerp(1f, Mathf.Lerp(0.3f, 1f, keep), path.Patchiness);
            }

            layer.Paint(x, y, index, strength);
        });
    }

    public override void ClearDetails(ClearLayer layer)
    {
        if (!path.ClearGrass)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, maxHalfWidth + path.SoftEdge * 1.5f, (x, y, d, s, f, side) =>
        {
            float half = Line.Lerp(halfWidth, s, f);
            float limit = half + path.SoftEdge * 0.25f;
            if (path.RaggedGrass > 0f)
            {
                Vector2 p = layer.Grid.World(x, y);
                limit += path.RaggedGrass * path.SoftEdge * (WorldNoise2D.Fbm(p.x / 1.7f, p.y / 1.7f, Feature.Seed + 5) - 0.5f) * 1.6f;
                // Tufts hold on along the edges of the tread.
                if (d > half * 0.6f && d < limit && WorldNoise2D.Fbm(p.x / 0.9f, p.y / 0.9f, Feature.Seed + 9) > 1f - path.RaggedGrass * 0.3f)
                {
                    return;
                }
            }

            if (d < limit)
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
