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
    public static float Shape(float u, float h, float reference, float height, float faceDepth, float plateau, CliffFeature.Shaping shaping)
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
            float t = (u + faceDepth) / faceDepth;
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
            float h = Shape(u, layer.Get(x, y), Line.Lerp(reference, s, f), Line.Lerp(height, s, f),
                            cliff.FaceDepth, cliff.Plateau, cliff.Shape);
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

    /// <summary>Stands rock-face pieces along the face, each fitted exactly: sized to the face's height (stacked in
    /// rows on tall faces), its finished side turned outwards, its front moved onto the face (Inset decides how far up
    /// the slope) and its top onto the rim, the rest buried in the ground; neighbours overlap by the cliff's Overlap.</summary>
    private void StandPieces(Transform root)
    {
        var shapes = new Dictionary<GameObject, Vector3[]>();
        float along = 0f;
        int guard = 0;
        while (along < Line.Length && guard++ < 4000)
        {
            CliffKit.Piece piece = cliff.Kit.Pick(Random);
            if (piece == null)
            {
                return;
            }

            int s = SegmentAt(along, out float f);
            Vector2 high = HighNormal(s);
            Vector2 centre = SplineLine.Flat(Line.PointAt(s, f));
            float rag = Line.Lerp(ragged, s, f);
            WorldGround.TryGround(centre + high * (-cliff.FaceDepth - 0.4f + rag), out Vector3 foot, out _);
            float rim = Rim(s, f, centre);
            float faceHeight = Mathf.Max(rim - foot.y, 0.5f);
            float size = Mathf.Max(piece.size.y, 0.1f);
            int rows = Mathf.Max(1, Mathf.CeilToInt(faceHeight / (size * 2.5f)));
            float rowHeight = faceHeight / rows;
            float variation = Mathf.Lerp(cliff.SizeVariation.x, cliff.SizeVariation.y, (float)Random.NextDouble());
            float scale = Mathf.Clamp(rowHeight / size * variation, 0.3f, 4f);
            float width = Mathf.Max(piece.size.x * scale, 0.5f);
            along = Mathf.Min(along + width * 0.5f, Line.Length);
            s = SegmentAt(along, out f);
            high = HighNormal(s);
            centre = SplineLine.Flat(Line.PointAt(s, f));
            rag = Line.Lerp(ragged, s, f);
            rim = Rim(s, f, centre);
            Vector3 outward = new Vector3(-high.x, 0f, -high.y);
            // Inset 0 puts the front just outside the foot of the slope, so the piece covers the whole face.
            Vector2 front = centre + high * (-cliff.FaceDepth * (1f - cliff.Inset) - 0.15f + rag);
            for (int row = 0; row < rows; row++)
            {
                float rowScale = row == 0 ? scale : scale * Mathf.Lerp(0.9f, 1.05f, (float)Random.NextDouble());
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(piece.prefab, root);
                float yaw = ((float)Random.NextDouble() - 0.5f) * 14f;
                float lean = 3f + (float)Random.NextDouble() * 5f; // a real crag leans back a little
                instance.transform.SetPositionAndRotation(new Vector3(front.x, rim, front.y),
                    Quaternion.LookRotation(outward) * Quaternion.Euler(-lean, yaw, 0f) * WorldAssets.StandUp(piece));
                instance.transform.localScale = piece.prefab.transform.localScale * rowScale;
                // Fit by the mesh itself: the bulk of its finished side onto the face (a few bumps and overhangs stick
                // out further), its top onto the rim (lower rows lower).
                Extent(instance, piece.prefab, shapes, outward, out float frontmost, out float top);
                float wantedTop = rim + 0.15f - row * rowHeight * 0.9f;
                float stepOut = row * 0.35f * cliff.FaceDepth; // lower rows stand a little further out
                float wantedFront = Vector3.Dot(new Vector3(front.x, 0f, front.y), outward) + stepOut;
                instance.transform.position += outward * (wantedFront - frontmost) + Vector3.up * (wantedTop - top);
            }

            along += width * (0.5f - cliff.Overlap); // to this piece's far end, back by the overlap
        }
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

    /// <summary>Where the placed piece's finished side stands (the 70th percentile of its points along
    /// <paramref name="outward"/>, so a single overhang does not decide) and how high it reaches.</summary>
    private static void Extent(GameObject instance, GameObject prefab, Dictionary<GameObject, Vector3[]> shapes, Vector3 outward,
                               out float frontmost, out float top)
    {
        if (!shapes.TryGetValue(prefab, out Vector3[] points))
        {
            var list = new List<Vector3>();
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null ||
                    (filter.name.Contains("_LOD") && !filter.name.EndsWith("_LOD0")))
                {
                    continue;
                }

                Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3[] vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    list.Add(toRoot.MultiplyPoint3x4(vertices[i]));
                }
            }

            shapes[prefab] = points = list.ToArray();
        }

        // Points are in the prefab root's space; the instance root has the same hierarchy.
        Matrix4x4 toWorld = instance.transform.localToWorldMatrix;
        var along = new float[points.Length];
        top = float.MinValue;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 world = toWorld.MultiplyPoint3x4(points[i]);
            along[i] = Vector3.Dot(world, outward);
            top = Mathf.Max(top, world.y);
        }

        System.Array.Sort(along);
        frontmost = along.Length > 0 ? along[Mathf.Min(along.Length - 1, (int)(along.Length * 0.7f))] : 0f;
    }
}
