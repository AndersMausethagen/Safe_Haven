using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds a <see cref="RiverFeature"/>: a water surface that only runs downhill, a bed dug below it with
/// sloping banks, bed and bank paint, cleared grass and trees, a water mesh whose flow follows the bends, plunge pools
/// under steep drops, and stones and bank objects.</summary>
public sealed class RiverBuilder : FeatureBuilder
{
    private const float BankLip = 0.12f;     // banks meet the water this far above it, so the edge stays hidden
    private const float MeshOverlap = 0.6f;  // the water mesh reaches this far under the banks
    private readonly RiverFeature river;
    private float[] halfWidth;
    private float[] depth;
    private float[] surface;
    private float maxHalfWidth;

    public RiverBuilder(RiverFeature river) : base(river)
    {
        this.river = river;
    }

    public override System.Collections.Generic.IEnumerable<TerrainLayer> PaintLayers
    {
        get
        {
            if (river.BedLayer != null) yield return river.BedLayer;
            if (river.BankLayer != null) yield return river.BankLayer;
        }
    }

    /// <summary>The water height at each sample (world metres).</summary>
    public float[] Surface => surface;
    public float[] HalfWidth => halfWidth;

    public override void Prepare(WorldSampler ground)
    {
        base.Prepare(ground);
        int n = Line.Count;
        halfWidth = new float[n];
        depth = new float[n];
        var raw = new float[n];
        maxHalfWidth = 0f;
        for (int i = 0; i < n; i++)
        {
            halfWidth[i] = river.Width * 0.5f * river.WidthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed);
            depth[i] = river.Depth * river.DepthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed);
            maxHalfWidth = Mathf.Max(maxHalfWidth, halfWidth[i]);
            raw[i] = river.Surface == RiverFeature.SurfaceMode.SplineHeight
                ? Line.Points[i].y
                : ground.Height(SplineLine.Flat(Line.Points[i])) - river.SurfaceBelowGround;
        }

        surface = Downhill(raw);
    }

    /// <summary>Never uphill: each sample is at most as high as the one before; lightly smoothed, which keeps the
    /// order (an average of a falling run still falls).</summary>
    public static float[] Downhill(float[] raw)
    {
        var y = new float[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            y[i] = i == 0 ? raw[0] : Mathf.Min(y[i - 1], raw[i]);
        }

        return Smoothed(y, 1, 2);
    }

    /// <summary>Bed height at distance <paramref name="d"/> from the middle (a rounded channel), or the bank rising from
    /// the water to the surrounding ground <paramref name="around"/>; NaN beyond the banks.</summary>
    public static float Profile(float d, float halfWidth, float depth, float bank, float water, float around)
    {
        if (d <= halfWidth)
        {
            float t = d / Mathf.Max(halfWidth, 0.01f);
            return water - 0.05f - depth * (1f - t * t);
        }

        if (d <= halfWidth + bank)
        {
            return Mathf.Lerp(water + BankLip, around, WorldNoise2D.Smooth((d - halfWidth) / bank));
        }

        return float.NaN;
    }

    public override void Heights(HeightLayer layer)
    {
        float bank = river.BankWidth;
        ForField(layer.Grid, layer.Rect, maxHalfWidth + bank, (x, y, d, s, f, side) =>
        {
            float h = Profile(d, Line.Lerp(halfWidth, s, f), Line.Lerp(depth, s, f), bank, Line.Lerp(surface, s, f), layer.Get(x, y));
            if (!float.IsNaN(h))
            {
                layer.Set(x, y, h);
            }
        });
    }

    public override void Paint(PaintLayer layer)
    {
        int bed = layer.LayerIndex(river.BedLayer);
        int banks = layer.LayerIndex(river.BankLayer);
        float reach = maxHalfWidth + river.BankWidth + river.BankPaint;
        ForField(layer.Grid, layer.Rect, reach, (x, y, d, s, f, side) =>
        {
            Vector2 p = layer.Grid.World(x, y);
            float noise = WorldNoise2D.Fbm(p.x * 0.35f, p.y * 0.35f, Feature.Seed);
            float w = Line.Lerp(halfWidth, s, f);
            float bankEdge = w + river.BankPaint * (0.5f + noise);
            if (banks >= 0 && d < bankEdge)
            {
                layer.Paint(x, y, banks, 1f - WorldNoise2D.Smooth((d - (bankEdge - 1.2f)) / 1.2f));
            }

            float bedEdge = w + 0.3f + (noise - 0.5f) * 0.8f;
            if (bed >= 0 && d < bedEdge + 0.6f)
            {
                layer.Paint(x, y, bed, 1f - WorldNoise2D.Smooth((d - bedEdge) / 0.6f));
            }
        });
    }

    public override void ClearDetails(ClearLayer layer)
    {
        if (!river.ClearGrass)
        {
            return;
        }

        ForField(layer.Grid, layer.Rect, maxHalfWidth + 1f, (x, y, d, s, f, side) =>
        {
            if (d < Line.Lerp(halfWidth, s, f) + 0.6f)
            {
                layer.Clear[layer.IndexOf(x, y)] = true;
            }
        });
    }

    public override bool ClearsTree(Vector2 xz)
    {
        if (!river.RemoveTrees)
        {
            return false;
        }

        float d = Line.Nearest(xz, out int s, out float f);
        return d < Line.Lerp(halfWidth, s, f) + 1.5f;
    }

    public override float EdgeDistance(Vector2 xz)
    {
        float d = Line.Nearest(xz, out int s, out float f);
        return Mathf.Max(0f, d - Line.Lerp(halfWidth, s, f));
    }

    public override void Objects(Transform root, BuildContext context)
    {
        BuildWater(root);
        BuildWaterfalls(root);
        ScatterAlong(river.Stones, river.StonesPer10m, root, context,
                     (s, f) => ((float)Random.NextDouble() * 2f - 1f) * Line.Lerp(halfWidth, s, f) * 0.8f,
                     null, 0.1f);
        ScatterAlong(river.BankScatter, river.BankScatterPer10m, root, context,
                     (s, f) =>
                     {
                         float w = Line.Lerp(halfWidth, s, f);
                         float d = w + 0.2f + (float)Random.NextDouble() * river.BankWidth * 0.9f;
                         return Random.NextDouble() < 0.5 ? d : -d;
                     },
                     ground => ground.y > LowestSurface(ground) + 0.05f);
    }

    private float LowestSurface(Vector3 point)
    {
        Line.Nearest(SplineLine.Flat(point), out int s, out float f);
        return Line.Lerp(surface, s, f);
    }

    private void BuildWater(Transform root)
    {
        var go = new GameObject("Water", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false);
        Transform t = go.transform;
        const int across = 7;
        int rows = Line.Count;
        var vertices = new Vector3[rows * across];
        var uv = new Vector2[rows * across];
        var flow = new Vector2[rows * across];
        var centreline = new Vector4[rows];
        float meanHalfWidth = 0f;
        foreach (float w in halfWidth)
        {
            meanHalfWidth += w / rows;
        }

        float v = 0f;
        for (int i = 0; i < rows; i++)
        {
            Vector3 centre = new Vector3(Line.Points[i].x, surface[i], Line.Points[i].z);
            if (i > 0)
            {
                Vector3 previous = new Vector3(Line.Points[i - 1].x, surface[i - 1], Line.Points[i - 1].z);
                v += Vector3.Distance(previous, centre);
            }

            Vector2 direction = Line.Directions[i];
            Vector3 right = new Vector3(direction.y, 0f, -direction.x);
            float extent = halfWidth[i] + MeshOverlap;
            Vector3 flowLocal = t.InverseTransformDirection(new Vector3(direction.x, 0f, direction.y));
            // The material's Flow Speed is 1: each vertex carries this river's speed, faster where it narrows.
            float speed = river.FlowSpeed * Mathf.Clamp(meanHalfWidth / Mathf.Max(halfWidth[i], 0.1f), 0.6f, 1.8f);
            for (int k = 0; k < across; k++)
            {
                float u = k / (float)(across - 1) * 2f - 1f;
                int index = i * across + k;
                vertices[index] = t.InverseTransformPoint(centre + right * (u * extent));
                uv[index] = new Vector2(u * extent, v);
                flow[index] = new Vector2(flowLocal.x, flowLocal.z).normalized * speed;
            }

            Vector3 local = t.InverseTransformPoint(centre);
            centreline[i] = new Vector4(local.x, local.y, local.z, halfWidth[i] + 0.2f);
        }

        var triangles = new int[(rows - 1) * (across - 1) * 6];
        int tri = 0;
        for (int i = 0; i + 1 < rows; i++)
        {
            for (int k = 0; k + 1 < across; k++)
            {
                int a = i * across + k, b = a + 1, c = a + across, d = c + 1;
                triangles[tri++] = a;
                triangles[tri++] = c;
                triangles[tri++] = b;
                triangles[tri++] = b;
                triangles[tri++] = c;
                triangles[tri++] = d;
            }
        }

        var mesh = new Mesh { name = Feature.name + " Water", indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.uv2 = flow;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = river.Water != null ? river.Water : WorldAssets.RiverWater();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var water = go.AddComponent<WaterSurface>();
        water.SetCentreline(centreline);
    }

    /// <summary>Plunge pools (foam, mist, spray) at the foot of every drop of at least the waterfall height.</summary>
    private void BuildWaterfalls(Transform root)
    {
        WaterSetup.Set set = WaterSetup.EnsureAssets();
        if (set.Plunge == null)
        {
            return;
        }

        int start = -1;
        for (int i = 1; i <= Line.Count; i++)
        {
            bool steep = i < Line.Count && (surface[i - 1] - surface[i]) / Mathf.Max(Line.Along[i] - Line.Along[i - 1], 0.01f) > 0.7f;
            if (steep && start < 0)
            {
                start = i - 1;
            }
            else if (!steep && start >= 0)
            {
                int foot = i - 1;
                float drop = surface[start] - surface[foot];
                if (drop >= river.WaterfallDrop)
                {
                    int at = Mathf.Min(foot + 2, Line.Count - 1);
                    var plunge = (GameObject)PrefabUtility.InstantiatePrefab(set.Plunge, root);
                    Vector2 direction = Line.Directions[foot];
                    plunge.name = $"Plunge ({drop:0.0} m)";
                    plunge.transform.SetPositionAndRotation(new Vector3(Line.Points[at].x, surface[at] + 0.02f, Line.Points[at].z),
                                                            Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y)));
                    WaterSetup.FitPlunge(plunge, halfWidth[foot] * 2f);
                }

                start = -1;
            }
        }
    }
}
