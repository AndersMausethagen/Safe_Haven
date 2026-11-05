using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds a <see cref="RavineFeature"/>: a floor carved the depth below the ground along the line, steep walls on
/// both sides rising to whatever ground is there (ragged in plan, noisy in face, optionally stepped in ledges), rock and
/// floor paint, crag pieces along both walls (CragWall) and rubble on the floor. It only cuts down.</summary>
public sealed class RavineBuilder : FeatureBuilder
{
    private readonly RavineFeature ravine;
    private float[] floor;      // floor height at each sample
    private float[] halfFloor;  // half the floor's width at each sample
    private float[] ragLeft;    // how far each wall wanders out (+) or in (-) at each sample
    private float[] ragRight;

    public RavineBuilder(RavineFeature ravine) : base(ravine)
    {
        this.ravine = ravine;
    }

    public override IEnumerable<TerrainLayer> PaintLayers
    {
        get
        {
            if (ravine.WallLayer != null) yield return ravine.WallLayer;
            if (ravine.MossLayer != null) yield return ravine.MossLayer;
            if (ravine.FloorLayer != null) yield return ravine.FloorLayer;
        }
    }

    public override void Prepare(WorldSampler ground)
    {
        base.Prepare(ground);
        int n = Line.Count;
        floor = new float[n];
        halfFloor = new float[n];
        ragLeft = new float[n];
        ragRight = new float[n];
        var raw = new float[n];
        for (int i = 0; i < n; i++)
        {
            raw[i] = ground.Height(SplineLine.Flat(Line.Points[i]));
            halfFloor[i] = 0.5f * ravine.FloorWidth * ravine.WidthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed);
            float wander = Mathf.Max(halfFloor[i], 1f) * 0.7f * ravine.Raggedness;
            ragLeft[i] = (WorldNoise2D.Fbm(Line.Along[i] / 7f, 0.3f, Feature.Seed + 11) - 0.5f) * 2f * wander;
            ragRight[i] = (WorldNoise2D.Fbm(Line.Along[i] / 7f, 0.7f, Feature.Seed + 23) - 0.5f) * 2f * wander;
        }

        // The floor sits the depth below the ground along the line, smoothed so it runs on rather than bumping.
        float[] reference = Smoothed(raw, Mathf.RoundToInt(8f / SampleStep), 2);
        for (int i = 0; i < n; i++)
        {
            floor[i] = reference[i] - ravine.Depth * ravine.DepthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed);
        }

        floor = Smoothed(floor, Mathf.RoundToInt(4f / SampleStep), 1);
    }

    /// <summary>The share of the way up a wall (0 at its foot, 1 at its top) for a distance <paramref name="t"/> across it
    /// (0-1): even, or stepped into <paramref name="ledges"/> ledges, each band flat at its foot and steep after.</summary>
    public static float WallProfile(float t, int ledges, float strength)
    {
        t = Mathf.Clamp01(t);
        if (ledges <= 0 || strength <= 0f)
        {
            return t;
        }

        float k = t * (ledges + 1);
        float band = Mathf.Floor(k);
        float inBand = k - band;
        float stepped = Mathf.Min(1f, (band + Mathf.Pow(inBand, 1f + 3f * strength)) / (ledges + 1));
        return Mathf.Lerp(t, stepped, strength);
    }

    private float HalfFloor(int s, float f, int side) => Mathf.Max(0.3f, Line.Lerp(halfFloor, s, f) + Line.Lerp(side > 0 ? ragLeft : ragRight, s, f));

    private float MostReach()
    {
        float most = 0f;
        for (int i = 0; i < halfFloor.Length; i++)
        {
            most = Mathf.Max(most, halfFloor[i] + Mathf.Max(Mathf.Abs(ragLeft[i]), Mathf.Abs(ragRight[i])));
        }

        return most + ravine.WallRun + 1f;
    }

    public override void Heights(HeightLayer layer)
    {
        ForField(layer.Grid, layer.Rect, MostReach(), (x, y, d, s, f, side) =>
        {
            float h = layer.Get(x, y);
            float hf = HalfFloor(s, f, side);
            float bottom = Line.Lerp(floor, s, f);
            Vector2 p = layer.Grid.World(x, y);
            float target;
            if (d <= hf)
            {
                // A floor, a little lower in the middle and uneven, as water and rockfall leave it.
                float dip = d / Mathf.Max(hf, 0.01f);
                target = bottom + 0.3f * dip * dip + (WorldNoise2D.Fbm(p.x / 2.5f, p.y / 2.5f, Feature.Seed + 5) - 0.5f) * 0.3f;
            }
            else
            {
                float t = (d - hf) / ravine.WallRun;
                if (t >= 1f)
                {
                    return;
                }

                float bumps = (WorldNoise2D.Fbm(p.x / 3f, p.y / 3f, Feature.Seed + 7) - 0.5f) * ravine.WallNoise * 0.6f;
                float up = WallProfile(Mathf.Clamp01(t + bumps), ravine.Ledges, ravine.LedgeStrength);
                // The wall rises to the ground beside it, so the top meets the slope wherever it is.
                target = Mathf.Lerp(bottom, h, up);
            }

            if (target < h)
            {
                layer.Set(x, y, target);
            }
        });
    }

    public override void Paint(PaintLayer layer)
    {
        int rock = layer.LayerIndex(ravine.WallLayer);
        int bed = layer.LayerIndex(ravine.FloorLayer);
        int moss = layer.LayerIndex(ravine.MossLayer);
        if (rock < 0 && bed < 0)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, MostReach() + 1f, (x, y, d, s, f, side) =>
        {
            Vector2 p = layer.Grid.World(x, y);
            float noise = WorldNoise2D.Fbm(p.x / 2f, p.y / 2f, Feature.Seed + 3);
            float hf = HalfFloor(s, f, side);
            float t = (d - hf) / ravine.WallRun;
            if (bed >= 0 && d <= hf + 0.4f * noise)
            {
                layer.Paint(x, y, bed, 1f);
            }
            else if (rock >= 0 && t < 1.1f + noise * 0.3f)
            {
                layer.Paint(x, y, rock, t > 1f ? 1f - WorldNoise2D.Smooth((t - 1f) / 0.3f) : 1f);
                // A wall faces back across the ravine; facing north it is shaded and mossy, in patches.
                Vector2 facing = -Outward(s, side);
                if (moss >= 0 && facing.y > 0.3f && t < 1f)
                {
                    float patch = WorldNoise2D.Fbm(p.x / 3f, p.y / 3f, Feature.Seed + 9);
                    layer.Paint(x, y, moss, ravine.NorthMoss * Mathf.InverseLerp(0.3f, 0.9f, facing.y) * Mathf.Clamp01(patch * 1.6f - 0.3f));
                }
            }
        });
    }

    public override void ClearDetails(ClearLayer layer)
    {
        if (!ravine.ClearGrass)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, MostReach(), (x, y, d, s, f, side) =>
        {
            if (d < HalfFloor(s, f, side) + ravine.WallRun + 0.4f)
            {
                layer.Clear[layer.IndexOf(x, y)] = true;
            }
        });
    }

    public override bool ClearsTree(Vector2 xz)
    {
        if (!ravine.RemoveTrees)
        {
            return false;
        }

        float d = Line.Nearest(xz, out int s, out float f);
        return d < Line.Lerp(halfFloor, s, f) + ravine.WallRun + 1.5f;
    }

    public override float EdgeDistance(Vector2 xz)
    {
        float d = Line.Nearest(xz, out int s, out float f);
        return Mathf.Max(0f, d - Line.Lerp(halfFloor, s, f) - ravine.WallRun);
    }

    public override void Objects(Transform root, BuildContext context)
    {
        if (ravine.Kit != null && ravine.Kit.Pieces.Count > 0)
        {
            foreach (int side in new[] { 1, -1 })
            {
                CragWall.Stand(root, new CragWall.Face
                {
                    Line = Line,
                    Kit = ravine.Kit,
                    Random = Random,
                    SegmentAt = SegmentAt,
                    HighNormal = s => Outward(s, side),
                    Edge = (s, f) => SplineLine.Flat(Line.PointAt(s, f)) + Outward(s, side) * (HalfFloor(s, f, side) + ravine.WallRun),
                    Rag = (s, f) => 0f,
                    Rim = (s, f, edge) => WorldGround.TryGround(edge + Outward(s, side) * 0.3f, out Vector3 top, out _) ? top.y : Line.Lerp(floor, s, f) + ravine.Depth,
                    FaceDepth = ravine.WallRun,
                    Inset = ravine.Inset,
                    Overlap = ravine.Overlap,
                    SizeVariation = ravine.SizeVariation,
                    Overhangs = ravine.Overhangs,
                });
            }
        }

        ScatterAlong(ravine.Rubble, ravine.RubblePer10m, root, context,
            (s, f) => ((float)Random.NextDouble() * 2f - 1f) * Line.Lerp(halfFloor, s, f) * 0.85f, null);
    }

    // The flat direction from the floor up a wall: left of the line for side +1, right for -1.
    private Vector2 Outward(int s, int side)
    {
        Vector2 direction = Line.Directions[s];
        return new Vector2(-direction.y, direction.x) * side;
    }
}
