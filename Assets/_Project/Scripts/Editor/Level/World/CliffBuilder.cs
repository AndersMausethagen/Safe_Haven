using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Builds a <see cref="CliffFeature"/>: a steep face along the line between the low and the raised (or cut)
/// ground, a ragged edge, rock paint, rock-face pieces stood overlapping along the face with their finished side out
/// and their backs pushed into the slope, and rubble at the foot.</summary>
public sealed class CliffBuilder : FeatureBuilder
{
    private readonly CliffFeature cliff;
    private float[] height;
    private float[] reference;
    private float[] ragged;
    private int highSign;

    public CliffBuilder(CliffFeature cliff) : base(cliff)
    {
        this.cliff = cliff;
    }

    public override IEnumerable<TerrainLayer> PaintLayers
    {
        get
        {
            if (cliff.RockLayer != null) yield return cliff.RockLayer;
        }
    }

    public override void Prepare(WorldSampler ground)
    {
        base.Prepare(ground);
        int n = Line.Count;
        height = new float[n];
        ragged = new float[n];
        var raw = new float[n];
        for (int i = 0; i < n; i++)
        {
            height[i] = cliff.Height * cliff.HeightAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed);
            raw[i] = ground.Height(SplineLine.Flat(Line.Points[i]));
            float wobble = WorldNoise2D.Fbm(Line.Along[i] / 3.5f, 0.5f, Feature.Seed) - 0.5f;
            ragged[i] = wobble * 2f * cliff.Raggedness * cliff.FaceDepth * 1.5f;
        }

        // The face's foot (or top, when cutting) follows the ground along the line, smoothed so the rim is even.
        reference = Smoothed(raw, Mathf.RoundToInt(6f / SampleStep), 2);
        // Field sides: +1 is left of the line (inside, for a closed line).
        highSign = cliff.High == CliffFeature.HighSide.Left ? 1 : -1;
    }

    /// <summary>The cliff's ground at signed distance <paramref name="u"/> from the face edge (+ on the high side), given
    /// the ground there now (<paramref name="h"/>), the rim reference and the face height; NaN where it does not change.</summary>
    /// <param name="bump">Face noise at the cell (-0.5 to 0.5, already scaled); 0 for a smooth face.</param>
    public static float Shape(float u, float h, float reference, float height, float faceDepth, float plateau, CliffFeature.Shaping shaping,
                              int ledges = 0, float ledgeStrength = 0f, float bump = 0f)
    {
        bool raise = shaping == CliffFeature.Shaping.Raise;
        float top = raise ? reference + height : reference;
        float bottom = raise ? reference : reference - height;
        if (u >= 0f)
        {
            if (!raise || u > plateau)
            {
                return float.NaN;
            }

            float w = 1f - WorldNoise2D.Smooth((u - plateau * 0.5f) / (plateau * 0.5f));
            return Mathf.Lerp(h, Mathf.Max(h, top), w);
        }

        if (u >= -faceDepth)
        {
            float t = RavineBuilder.WallProfile((u + faceDepth) / faceDepth + bump, ledges, ledgeStrength);
            return raise ? Mathf.Lerp(h, Mathf.Max(h, top), t) : Mathf.Lerp(Mathf.Min(h, bottom), h, t);
        }

        float beyond = -u - faceDepth;
        if (raise || beyond > plateau)
        {
            return float.NaN;
        }

        float wLow = 1f - WorldNoise2D.Smooth((beyond - plateau * 0.5f) / (plateau * 0.5f));
        return Mathf.Lerp(h, Mathf.Min(h, bottom), wLow);
    }

    private float SignedU(float d, int side, int s, float f) => d * side * highSign + Line.Lerp(ragged, s, f);

    public override void Heights(HeightLayer layer)
    {
        if (cliff.Shape == CliffFeature.Shaping.RocksOnly)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, cliff.Plateau + cliff.FaceDepth + 2f, (x, y, d, s, f, side) =>
        {
            float u = SignedU(d, side, s, f);
            Vector2 p = layer.Grid.World(x, y);
            float bump = cliff.FaceNoise > 0f ? (WorldNoise2D.Fbm(p.x / 3f, p.y / 3f, Feature.Seed + 7) - 0.5f) * cliff.FaceNoise * 0.6f : 0f;
            float h = Shape(u, layer.Get(x, y), Line.Lerp(reference, s, f), Line.Lerp(height, s, f),
                            cliff.FaceDepth, cliff.Plateau, cliff.Shape, cliff.Ledges, cliff.LedgeStrength, bump);
            if (!float.IsNaN(h))
            {
                layer.Set(x, y, h);
            }
        });
    }

    public override void Paint(PaintLayer layer)
    {
        int rock = layer.LayerIndex(cliff.RockLayer);
        if (rock < 0)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, cliff.FaceDepth + 4f, (x, y, d, s, f, side) =>
        {
            Vector2 p = layer.Grid.World(x, y);
            float noise = WorldNoise2D.Fbm(p.x / 2f, p.y / 2f, Feature.Seed + 3);
            float u = SignedU(d, side, s, f);
            float low = -cliff.FaceDepth - 0.8f - noise * 1.8f;
            float high = 0.4f + noise * 1.2f;
            float w = u < low ? 1f - WorldNoise2D.Smooth((low - u) / 0.8f) : u > high ? 1f - WorldNoise2D.Smooth((u - high) / 0.8f) : 1f;
            layer.Paint(x, y, rock, w);
        });
    }

    public override void ClearDetails(ClearLayer layer)
    {
        if (!cliff.ClearGrass)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, cliff.FaceDepth + 2f, (x, y, d, s, f, side) =>
        {
            float u = SignedU(d, side, s, f);
            if (u > -cliff.FaceDepth - 1f && u < 0.6f)
            {
                layer.Clear[layer.IndexOf(x, y)] = true;
            }
        });
    }

    public override bool ClearsTree(Vector2 xz)
    {
        if (!cliff.RemoveTrees)
        {
            return false;
        }

        float d = Line.Nearest(xz, out int s, out float f);
        return d < cliff.FaceDepth + 1.5f;
    }

    public override float EdgeDistance(Vector2 xz) => Mathf.Max(0f, Line.Nearest(xz, out _, out _) - cliff.FaceDepth);

    /// <summary>The flat direction pointing to the high side at a sample.</summary>
    private Vector2 HighNormal(int s)
    {
        Vector2 direction = Line.Directions[s];
        Vector2 left = new Vector2(-direction.y, direction.x);
        if (!Line.Closed)
        {
            return left * highSign;
        }

        bool leftIsInside = Line.Inside(SplineLine.Flat(Line.Points[s]) + left * 0.3f);
        return (leftIsInside ? left : -left) * highSign;
    }

    public override void Objects(Transform root, BuildContext context)
    {
        if (cliff.Kit != null && cliff.Kit.Pieces.Count > 0)
        {
            StandPieces(root);
        }

        ScatterAlong(cliff.Rubble, cliff.RubblePer10m, root, context, (s, f) =>
        {
            Vector2 high = HighNormal(s);
            Vector2 direction = Line.Directions[s];
            float sign = Vector2.Dot(high, new Vector2(-direction.y, direction.x)) > 0f ? -1f : 1f; // towards the low side
            return sign * (cliff.FaceDepth + 0.6f + (float)Random.NextDouble() * 3f);
        }, null);
    }

    /// <summary>Stands rock-face pieces along the face (CragWall), its edge on the line.</summary>
    private void StandPieces(Transform root)
    {
        CragWall.Stand(root, new CragWall.Face
        {
            Line = Line,
            Kit = cliff.Kit,
            Random = Random,
            SegmentAt = SegmentAt,
            HighNormal = HighNormal,
            Edge = (s, f) => SplineLine.Flat(Line.PointAt(s, f)),
            Rag = (s, f) => Line.Lerp(ragged, s, f),
            Rim = Rim,
            FaceDepth = cliff.FaceDepth,
            Inset = cliff.Inset,
            Overlap = cliff.Overlap,
            SizeVariation = cliff.SizeVariation,
        });
    }

    /// <summary>Height of the cliff's rim at a sample: the raised top, the uncut edge, or (rocks only) the ground there
    /// plus the height.</summary>
    private float Rim(int s, float f, Vector2 centre)
    {
        float reach = Line.Lerp(height, s, f);
        switch (cliff.Shape)
        {
            case CliffFeature.Shaping.Raise:
                return Line.Lerp(reference, s, f) + reach;
            case CliffFeature.Shaping.Cut:
                return Line.Lerp(reference, s, f);
            default:
                WorldGround.TryGround(centre, out Vector3 ground, out _);
                return ground.y + reach;
        }
    }
}
