using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Builds a <see cref="RiverFeature"/>: a water surface that only runs downhill, a bed dug below it with
/// sloping banks, bed and bank paint, cleared grass and trees, a water mesh whose flow follows the bends, plunge pools
/// under steep drops, and stones and bank objects. An end drawn into a pond joins it: see <see cref="MouthShare"/>.</summary>
public sealed class RiverBuilder : FeatureBuilder
{
    private const float BankLip = 0.12f;     // banks meet the water this far above it, so the edge stays hidden
    private const float MeshOverlap = 0.6f;  // the water mesh reaches this far under the banks
    private const float ChannelInPond = 1.5f; // the channel runs this far past the shore, where the pond's own bed has dropped below it
    private const float WaterIntoPond = 3.5f; // the river's water runs this far out over the pond's own, fading as it goes
    private const float WaterFadesFrom = 2.5f; // ...from this far up the river from the shore: no hard edge where the two meet
    private const float WaterOverPond = 0.02f; // and lies this far over the pond's, so the two never flicker
    private const float ShorePaintFade = 2f;  // its bed and bank paint give way over the last of the way to the shore
    private readonly RiverFeature river;
    private float[] halfWidth;
    private float[] depth;
    private float[] surface;
    private float maxHalfWidth;
    private readonly System.Collections.Generic.List<(WaterSurface water, Bounds local)> ponds = new System.Collections.Generic.List<(WaterSurface, Bounds)>();
    // The level of the still water each end lies in (NaN: none). A stream out of a pond and into the lake has two, and its
    // water near each end goes to that end's own: one level for both dug the pond's stream down to the lake all the way.
    private float startLevel = float.NaN;
    private float endLevel = float.NaN;
    private float[] toPond;
    private float mouthLength;

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
        FindPonds();
        mouthLength = Mathf.Max(10f, river.Width * 2f);
        toPond = new float[n];
        for (int i = 0; i < n; i++)
        {
            toPond[i] = ShoreAt(SplineLine.Flat(Line.Points[i]));
            float mouth = MouthShare(toPond[i], mouthLength);
            halfWidth[i] = river.Width * 0.5f * river.WidthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed) * Mathf.Lerp(1f, RiverFeature.MouthFlare, mouth);
            depth[i] = river.Depth * river.DepthAtKnots.Evaluate(Line.KnotT[i], Line.KnotCount, Line.Closed) * Mathf.Lerp(1f, 0.35f, mouth);
            maxHalfWidth = Mathf.Max(maxHalfWidth, halfWidth[i]);
            raw[i] = river.Surface == RiverFeature.SurfaceMode.SplineHeight
                ? Line.Points[i].y
                : ground.Height(SplineLine.Flat(Line.Points[i])) - river.SurfaceBelowGround;
            // Near an end in still water, the water goes to that water's level: the nearer end's, by the way along.
            float level = i < n / 2 ? (float.IsNaN(startLevel) ? endLevel : startLevel) : (float.IsNaN(endLevel) ? startLevel : endLevel);
            if (!float.IsNaN(level))
            {
                raw[i] = Mathf.Lerp(raw[i], level, mouth);
            }
        }

        surface = Downhill(raw);
        // Running into a pond, it never lies below the pond.
        if (!float.IsNaN(endLevel) && toPond[n - 1] < 0f)
        {
            for (int i = 0; i < n; i++)
            {
                surface[i] = Mathf.Max(surface[i], endLevel);
            }
        }
    }

    /// <summary>How far along a river's mouth a point is, from 0 up the river to 1 at a pond's shore (and in the pond):
    /// <paramref name="shore"/> metres from the shore on a mouth this long.</summary>
    public static float MouthShare(float shore, float length) =>
        float.IsPositiveInfinity(shore) ? 0f : 1f - Mathf.SmoothStep(0f, 1f, shore / Mathf.Max(length, 0.01f));

    // The still water this river's ends lie in, and the level of each end's (startLevel, endLevel).
    private void FindPonds()
    {
        ponds.Clear();
        startLevel = endLevel = float.NaN;
        if (Line.Count == 0)
        {
            return;
        }

        Vector2 first = SplineLine.Flat(Line.Points[0]), last = SplineLine.Flat(Line.Points[Line.Count - 1]);
        foreach (WaterSurface water in Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Exclude))
        {
            var filter = water.GetComponent<MeshFilter>();
            if (!water.IsStill || filter == null || filter.sharedMesh == null)
            {
                continue;
            }

            Bounds local = filter.sharedMesh.bounds;
            bool atStart = Shore(water, local, first) < 0f, atEnd = Shore(water, local, last) < 0f;
            if (atStart || atEnd)
            {
                ponds.Add((water, local));
                float level = water.transform.TransformPoint(new Vector3(local.center.x, local.max.y, local.center.z)).y;
                startLevel = atStart ? level : startLevel;
                endLevel = atEnd ? level : endLevel;
            }
        }
    }

    // Metres from the shore of the pond it joins (negative in the water); infinity where it joins none.
    private float ShoreAt(Vector2 xz)
    {
        float nearest = float.PositiveInfinity;
        foreach ((WaterSurface water, Bounds local) in ponds)
        {
            nearest = Mathf.Min(nearest, Shore(water, local, xz));
        }

        return nearest;
    }

    // The water's own shoreline where it has one (a turned or ragged lake's mesh is not its shore: its box reaches tens of metres
    // past the water at the corners, and a river stopped there, on dry land); else its mesh's box or oval.
    private static float Shore(WaterSurface water, Bounds local, Vector2 xz) =>
        water.HasShoreline ? water.ShorelineDistance(xz.x, xz.y) : WaterSetup.ShoreDistance(water.transform, local, water.Oval, xz.x, xz.y);

    // Only near a pond is it worth working out, cell by cell.
    private float ShoreNear(int segment, Vector2 xz) =>
        toPond[segment] < mouthLength + maxHalfWidth + river.BankWidth + river.BankPaint ? ShoreAt(xz) : float.PositiveInfinity;

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
            if (ShoreNear(s, layer.Grid.World(x, y)) < -ChannelInPond)
            {
                return; // the pond's own bed
            }

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
            float shore = ShoreNear(s, p);
            if (shore <= -ChannelInPond)
            {
                return;
            }

            // The banks give way at the shore; the bed carries on to the end of its channel, a little way out in the pond.
            float nearShore = WorldNoise2D.Smooth(shore / ShorePaintFade);
            float bedNearShore = WorldNoise2D.Smooth((shore + ChannelInPond) / (ShorePaintFade + ChannelInPond));
            float noise = WorldNoise2D.Fbm(p.x * 0.35f, p.y * 0.35f, Feature.Seed);
            float w = Line.Lerp(halfWidth, s, f);
            float bankEdge = w + river.BankPaint * (0.5f + noise);
            if (banks >= 0 && d < bankEdge)
            {
                layer.Paint(x, y, banks, (1f - WorldNoise2D.Smooth((d - (bankEdge - 1.2f)) / 1.2f)) * nearShore);
            }

            float bedEdge = w + 0.3f + (noise - 0.5f) * 0.8f;
            if (bed >= 0 && d < bedEdge + 0.6f)
            {
                layer.Paint(x, y, bed, (1f - WorldNoise2D.Smooth((d - bedEdge) / 0.6f)) * bedNearShore);
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
                     ground => ShoreAt(SplineLine.Flat(ground)) > 0f, 0.1f);
        ScatterAlong(river.BankScatter, river.BankScatterPer10m, root, context,
                     (s, f) =>
                     {
                         float w = Line.Lerp(halfWidth, s, f);
                         float d = w + 0.2f + (float)Random.NextDouble() * river.BankWidth * 0.9f;
                         return Random.NextDouble() < 0.5 ? d : -d;
                     },
                     ground => ground.y > LowestSurface(ground) + 0.05f && ShoreAt(SplineLine.Flat(ground)) > 0f);
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
        // Only the river's own water: where it joins a pond it runs a little way out over the pond's and fades into it.
        int from = 0, to = Line.Count - 1;
        while (from < to && toPond[from] < -WaterIntoPond) from++;
        while (to > from && toPond[to] < -WaterIntoPond) to--;
        int rows = to - from + 1;
        if (rows < 2)
        {
            Object.DestroyImmediate(go);
            return;
        }

        var vertices = new Vector3[rows * across];
        var uv = new Vector2[rows * across];
        var flow = new Vector2[rows * across];
        var colours = new Color[rows * across];
        var centreline = new Vector4[rows];
        float meanHalfWidth = 0f;
        for (int i = from; i <= to; i++)
        {
            meanHalfWidth += halfWidth[i] / rows;
        }

        float v = 0f;
        for (int i = 0; i < rows; i++)
        {
            int sample = from + i;
            // Whole up the river, gone by the time it is out over the pond (the material's Edge Fade from Vertex Alpha).
            float shown = float.IsPositiveInfinity(toPond[sample]) ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-WaterIntoPond, WaterFadesFrom, toPond[sample]));
            Vector3 centre = new Vector3(Line.Points[sample].x, surface[sample] + (toPond[sample] < WaterFadesFrom ? WaterOverPond : 0f), Line.Points[sample].z);
            if (i > 0)
            {
                Vector3 previous = new Vector3(Line.Points[sample - 1].x, surface[sample - 1], Line.Points[sample - 1].z);
                v += Vector3.Distance(previous, centre);
            }

            Vector2 direction = Line.Directions[sample];
            Vector3 right = new Vector3(direction.y, 0f, -direction.x);
            float extent = halfWidth[sample] + MeshOverlap;
            Vector3 flowLocal = t.InverseTransformDirection(new Vector3(direction.x, 0f, direction.y));
            // The material's Flow Speed is 1: each vertex carries this river's speed, faster where it narrows.
            float speed = river.FlowSpeed * Mathf.Clamp(meanHalfWidth / Mathf.Max(halfWidth[sample], 0.1f), 0.6f, 1.8f);
            for (int k = 0; k < across; k++)
            {
                float u = k / (float)(across - 1) * 2f - 1f;
                int index = i * across + k;
                vertices[index] = t.InverseTransformPoint(centre + right * (u * extent));
                uv[index] = new Vector2(u * extent, v);
                flow[index] = new Vector2(flowLocal.x, flowLocal.z).normalized * speed;
                colours[index] = new Color(1f, 1f, 1f, shown);
            }

            Vector3 local = t.InverseTransformPoint(centre);
            centreline[i] = new Vector4(local.x, local.y, local.z, halfWidth[sample] + 0.2f);
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
        mesh.colors = colours;
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
