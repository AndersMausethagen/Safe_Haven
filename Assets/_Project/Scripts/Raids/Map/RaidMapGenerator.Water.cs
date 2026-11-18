using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Water on the made map: ponds with a place to drink, streams down the ravines with their falls and mist, and rivers
/// across the open land, drawn as strips along their lines. Part of RaidMapGenerator, in its own file.
/// </summary>
public static partial class RaidMapGenerator
{
    public const string PondsName = "Ponds";

    public const string RiversName = "Rivers";

    private const float PondMeshBeyond = 1.5f;

    private const int PondMeshPoints = 48;

    private const float PondDrinkSeconds = 1.5f;

    private const float RiverSurface = 0.15f;

    private const float RiverIntoWall = 0.8f;

    private const float RiverSpeed = 1.6f;

    private const int RiverAcross = 5;

    private const float MistEvery = 32f;

    private const float MistAboveFloor = 1.5f;

    private const float NeckClearance = 3.5f;

    private const float RiverEndNarrows = 0.6f;

    private const float FallLip = 0.5f;

    private const float FallThrow = 1.1f;

    private const float FallWidest = 2.6f;

    private const float LipSearchStep = 0.25f;

    /// <summary>
    /// Lays the plan's ponds on the map, in place of an earlier attempt's. Kept out of the map's Content, which is snapped to the
    /// ground. <paramref name="meshes"/> holds the meshes made, for whoever owns the map to destroy.
    /// </summary>
    public const string FenWaterName = "Fen Water";

    /// <summary>Metres the fen's water sheet reaches past the map's edge, so no gap shows at the rim.</summary>
    private const float FenSheetBeyond = 20f;

    /// <summary>
    /// A fen map's water (RaidLayoutRules › Fen): one still sheet over the whole map at the fen's water level, so the sunken wild ground is
    /// its pools and the trails, clearings, hummocks and strings stand out of it. One sheet, not hundreds of ponds: one draw call.
    /// </summary>
    public static void PlaceFenWater(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, float level, List<Mesh> meshes)
    {
        Transform old = map.transform.Find(FenWaterName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        Material material = rules.Fen.water != null ? rules.Fen.water : rules.PondMaterial;
        if (!rules.Fen.enabled || material == null)
        {
            return;
        }

        var water = new GameObject(FenWaterName);
        water.transform.SetParent(map.transform, false);
        water.transform.position = map.Terrain.transform.position + new Vector3(plan.Size * 0.5f, level, plan.Size * 0.5f);
        float half = plan.Size * 0.5f + FenSheetBeyond;
        var mesh = new Mesh { name = FenWaterName };
        mesh.vertices = new[] { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshes.Add(mesh);
        water.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = water.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        water.AddComponent<WaterSurface>().IsStill = true;
    }

    public static void PlacePonds(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, List<Mesh> meshes)
    {
        Transform old = map.transform.Find(PondsName);
        if (old != null)
        {
            // Detached first, as it lives on to the end of the frame and the name must be free for the new ones.
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        foreach (Mesh mesh in meshes)
        {
            Object.Destroy(mesh);
        }

        meshes.Clear();
        if (plan.Ponds.Count == 0)
        {
            return;
        }

        if (rules.PondMaterial == null)
        {
            Debug.LogWarning($"{rules.name} wants ponds but has no Pond Material, so there is only an empty hollow.", rules);
            return;
        }

        var holder = new GameObject(PondsName).transform;
        holder.SetParent(map.transform, false);
        Vector3 origin = map.Terrain.transform.position;
        for (int i = 0; i < plan.Ponds.Count; i++)
        {
            RaidLayoutPond pond = plan.Ponds[i];
            var water = new GameObject($"Pond {i + 1}");
            water.transform.SetParent(holder, false);
            water.transform.position = origin + new Vector3(pond.Position.x, pond.Level, pond.Position.y);
            Mesh mesh = PondMesh(pond);
            meshes.Add(mesh);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rules.PondMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<WaterSurface>().IsStill = true;
            if (rules.PondDrink > 0f || rules.PondFillItem != null)
            {
                AddDrinkingPlace(water.transform, pond, rules, origin);
            }
        }
    }

    // Its own object, as a hovered Interactable outlines every renderer under it and the water should not light up.
    private static void AddDrinkingPlace(Transform water, RaidLayoutPond pond, RaidLayoutRules rules, Vector3 origin)
    {
        var place = new GameObject("Drinking Place");
        place.transform.SetParent(water, false);
        place.AddComponent<RaidPondShore>().Configure(pond, origin);
        var source = place.AddComponent<WaterSource>();
        source.Configure("Pond", rules.PondDrink, rules.PondFillItem, rules.PondFillUpTo);
        place.GetComponent<Interactable>().Configure(InteractionAnimation.GatherLow, PondDrinkSeconds, rules.PondReach, false);
    }

    // A disc running a little past the shore, under the banks: the terrain draws the waterline.
    private static Mesh PondMesh(RaidLayoutPond pond)
    {
        var vertices = new Vector3[PondMeshPoints + 1];
        var normals = new Vector3[vertices.Length];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[PondMeshPoints * 3];
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < PondMeshPoints; i++)
        {
            Vector2 rim = pond.Rim(i * Mathf.PI * 2f / PondMeshPoints, PondMeshBeyond) - pond.Position;
            vertices[i + 1] = new Vector3(rim.x, 0f, rim.y);
            normals[i + 1] = Vector3.up;
            uvs[i + 1] = new Vector2(0.5f + rim.x * 0.5f / pond.Reach, 0.5f + rim.y * 0.5f / pond.Reach);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % PondMeshPoints + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        var mesh = new Mesh { name = "Pond" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Lays the plan's ravines on the map, in place of an earlier attempt's: a river along the floor of those that have one, mist lying
    /// in them, and a RaidChasm that takes whoever falls in. Kept out of the map's Content, which is snapped to the ground.
    /// <paramref name="meshes"/> holds the river meshes made, for whoever owns the map to destroy.
    /// </summary>
    public static void PlaceRavines(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, List<Mesh> meshes)
    {
        Transform old = map.transform.Find(RavinesName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        if (plan.Ravines.Count == 0)
        {
            return;
        }

        var holder = new GameObject(RavinesName).transform;
        holder.SetParent(map.transform, false);
        Vector3 origin = map.Terrain.transform.position;
        holder.position = origin;
        holder.rotation = Quaternion.identity;
        holder.gameObject.AddComponent<RaidChasm>().Bind(plan, map.Terrain);
        float neck = rules.TrailHalfWidth * (plan.Mood != null ? plan.Mood.trails : 1f) + NeckClearance;
        bool IsNeck(Vector2 point) => NearAny(plan.Trails, point, neck) || NearAny(plan.Spurs, point, neck);
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Ravine Crags"));

        FogArea[] areas = map.Content.GetComponentsInChildren<FogArea>(true);
        FogArea mist = plan.Mood != null && plan.Mood.ravineMist && areas.Length > 0 ? areas[0] : null;
        for (int i = 0; i < plan.Ravines.Count; i++)
        {
            RaidLayoutRavine ravine = plan.Ravines[i];
            if (ravine.River)
            {
                if (rules.RiverMaterial == null)
                {
                    Debug.LogWarning($"{rules.name} has rivers in its ravines but no River Material, so their floors are dry.", rules);
                }
                else
                {
                    AddRiver(holder, ravine, rules.RiverMaterial, meshes, i);
                }
            }

            if (mist != null)
            {
                AddMist(holder, ravine, mist, origin);
            }

            RaidRavineCrags.Dress(holder, ravine, rules.RavineCrags, map.Terrain, IsNeck, random);
            if (ravine.River && rules.RiverMaterial != null)
            {
                AddFalls(holder, ravine, rules, map.Terrain);
            }
        }
    }

    // A waterfall pours over the ravine's closed head into the river, and foam churns at the foot of every drop along it.
    private static void AddFalls(Transform holder, RaidLayoutRavine ravine, RaidLayoutRules rules, Terrain terrain)
    {
        Vector3 origin = terrain.transform.position;
        if (rules.WaterfallMaterial != null)
        {
            Vector2 head = ravine.Line[0];
            Vector2 down = (ravine.Line[1] - ravine.Line[0]).normalized;
            float half = ravine.HalfWidths[0];
            float floor = ravine.Floor[0] + origin.y + RiverSurface;
            // Back from the head until the ground stands near the rim: that is the lip.
            Vector3 lip = default;
            bool found = false;
            for (float back = 0f; back < half + RaidLayoutRavine.RagReach + 3f; back += LipSearchStep)
            {
                Vector3 at = origin + new Vector3(head.x - down.x * back, 0f, head.y - down.y * back);
                at.y = terrain.SampleHeight(at) + origin.y;
                if (at.y > ravine.Rim[0] + origin.y - 1f)
                {
                    lip = at;
                    found = true;
                    break;
                }
            }

            float height = lip.y - floor;
            if (found && height > 2f)
            {
                float width = Mathf.Min(FallWidest, (half - RaidLayoutRavine.WallRun(half)) * 1.4f);
                var forward = new Vector3(down.x, 0f, down.y);
                var fall = new GameObject("Waterfall", typeof(MeshFilter), typeof(MeshRenderer));
                fall.transform.SetParent(holder, false);
                fall.transform.SetPositionAndRotation(lip + forward * FallLip - Vector3.up * height, Quaternion.LookRotation(forward));
                var renderer = fall.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = rules.WaterfallMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Waterfall sheet = fall.AddComponent<Waterfall>();
                if (rules.WaterfallPlunge != null)
                {
                    GameObject plunge = Object.Instantiate(rules.WaterfallPlunge, fall.transform);
                    sheet.Plunge = plunge.transform;
                    FitPlunge(plunge, width);
                }

                sheet.Configure(width, height, FallLip, FallThrow);
            }
        }

        if (rules.WaterfallPlunge == null)
        {
            return;
        }

        foreach (int at in ravine.Cascades)
        {
            if (at + 1 >= ravine.Line.Count)
            {
                continue;
            }

            Vector2 foot = ravine.Line[at + 1];
            GameObject plunge = Object.Instantiate(rules.WaterfallPlunge, holder);
            plunge.name = "Cascade Foam";
            plunge.transform.position = origin + new Vector3(foot.x, ravine.Floor[at + 1] + RiverSurface, foot.y);
            Vector2 down = (ravine.Line[at + 1] - ravine.Line[at]).normalized;
            plunge.transform.rotation = Quaternion.LookRotation(new Vector3(down.x, 0f, down.y));
            FitPlunge(plunge, Mathf.Min(FallWidest, ravine.HalfWidths[at + 1]));
        }
    }

    /// <summary>How fast a river runs at each point of its line, as a share of its everyday speed (Docs/LookAndFeelResearch_2026-11-18.md
    /// W3): faster where it narrows (the same water through less width) and where it falls, slower where it spreads into a pool; eased
    /// over five points so it never jumps. Pure, for tests.</summary>
    public static float[] FlowAlong(List<Vector2> line, System.Func<int, float> half, System.Func<int, float> height)
    {
        int count = line.Count;
        var widths = new float[count];
        for (int i = 0; i < count; i++)
        {
            widths[i] = Mathf.Max(0.1f, half(i));
        }

        var sorted = (float[])widths.Clone();
        System.Array.Sort(sorted);
        float typical = sorted[count / 2];
        var raw = new float[count];
        for (int i = 0; i < count; i++)
        {
            float narrows = Mathf.Clamp(Mathf.Pow(typical / widths[i], 0.8f), 0.6f, 1.7f);
            int a = Mathf.Max(i - 1, 0), b = Mathf.Min(i + 1, count - 1);
            float length = Vector2.Distance(line[a], line[b]);
            float grade = length > 0.01f ? (height(a) - height(b)) / length : 0f;
            raw[i] = narrows * (1f + Mathf.Clamp(grade * 20f, 0f, 1.2f));
        }

        return Eased(raw, 2);
    }

    /// <summary>How sharply a line turns at each point (radians per metre, above zero turning left, whose outside is on the right),
    /// eased over five points. Pure, for tests.</summary>
    public static float[] Bends(List<Vector2> line)
    {
        int count = line.Count;
        var raw = new float[count];
        for (int i = 1; i + 1 < count; i++)
        {
            Vector2 before = line[i] - line[i - 1], after = line[i + 1] - line[i];
            float length = (before.magnitude + after.magnitude) * 0.5f;
            if (length < 0.01f)
            {
                continue;
            }

            Vector2 d0 = before.normalized, d1 = after.normalized;
            raw[i] = Mathf.Asin(Mathf.Clamp(d0.x * d1.y - d0.y * d1.x, -1f, 1f)) / length;
        }

        return Eased(raw, 2);
    }

    /// <summary>A river's speed across it (<paramref name="across"/> -1 the left bank, 1 the right): slower at the banks, faster round the
    /// outside of a bend (<paramref name="bend"/>: the turn's sharpness times the river's half width, above zero turning left).
    /// Pure, for tests.</summary>
    public static float FlowAcross(float across, float bend)
    {
        float banks = Mathf.Lerp(0.55f, 1f, 1f - across * across);
        return banks * (1f + Mathf.Clamp(bend * across, -0.35f, 0.35f));
    }

    private static float[] Eased(float[] values, int reach)
    {
        var eased = new float[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float sum = 0f;
            int n = 0;
            for (int k = Mathf.Max(0, i - reach); k <= Mathf.Min(values.Length - 1, i + reach); k++)
            {
                sum += values[k];
                n++;
            }

            eased[i] = sum / n;
        }

        return eased;
    }

    /// <summary>
    /// A strip of running water along a line, as the World Builder lays a river: rows across the line, uv in metres, the flow in uv2 (the
    /// material reads it from the mesh), and the centre line for WaterSurface (half widths from <paramref name="waterHalf"/>). Each end runs on
    /// a row further, narrowed, under the ground there, so the terrain draws the waterline.
    /// </summary>
    public static Mesh WaterStrip(List<Vector2> line, System.Func<int, float> meshHalf, System.Func<int, float> height, System.Func<int, float> waterHalf,
        float speed, out Vector4[] centreline)
    {
        int count = line.Count;
        int rows = count + 2;
        var vertices = new Vector3[rows * RiverAcross];
        var uv = new Vector2[vertices.Length];
        var flow = new Vector2[vertices.Length];
        centreline = new Vector4[rows];
        // How fast it runs along its line and across it (W3): the shader whitens, streaks and clouds faster water.
        float[] along = FlowAlong(line, waterHalf, height);
        float[] bends = Bends(line);
        float run = 0f;
        Vector2 previous = line[0];
        for (int i = 0; i < rows; i++)
        {
            int source = Mathf.Clamp(i - 1, 0, count - 1);
            Vector2 direction = (line[Mathf.Min(source + 1, count - 1)] - line[Mathf.Max(source - 1, 0)]).normalized;
            Vector2 point = line[source];
            float narrows = 1f;
            if (i == 0 || i == rows - 1)
            {
                point += direction * (i == 0 ? -waterHalf(source) : waterHalf(source));
                narrows = RiverEndNarrows;
            }

            run += Vector2.Distance(previous, point);
            previous = point;
            var right = new Vector3(direction.y, 0f, -direction.x);
            float extent = meshHalf(source) * narrows;
            var centre = new Vector3(point.x, height(source), point.y);
            for (int k = 0; k < RiverAcross; k++)
            {
                float across = k / (float)(RiverAcross - 1) * 2f - 1f;
                int at = i * RiverAcross + k;
                vertices[at] = centre + right * (across * extent);
                uv[at] = new Vector2(across * extent, run);
                flow[at] = direction * (RiverSpeed * speed * along[source] * FlowAcross(across, bends[source] * waterHalf(source)));
            }

            centreline[i] = new Vector4(centre.x, centre.y, centre.z, waterHalf(source) * narrows);
        }

        var triangles = new int[(rows - 1) * (RiverAcross - 1) * 6];
        int next = 0;
        for (int i = 0; i + 1 < rows; i++)
        {
            for (int k = 0; k + 1 < RiverAcross; k++)
            {
                int a = i * RiverAcross + k, b = a + 1, c = a + RiverAcross, d = c + 1;
                triangles[next++] = a;
                triangles[next++] = c;
                triangles[next++] = b;
                triangles[next++] = b;
                triangles[next++] = c;
                triangles[next++] = d;
            }
        }

        var mesh = new Mesh { name = "River" };
        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.uv2 = flow;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // As WaterSetup.FitPlunge (editor): the churn, mist and spray sized to the falling water's width.
    private static void FitPlunge(GameObject plunge, float width)
    {
        if (plunge.TryGetComponent(out WaterInteractor interactor))
        {
            interactor.Radius = Mathf.Max(width * 0.6f, 0.8f);
        }

        foreach (ParticleSystem system in plunge.GetComponentsInChildren<ParticleSystem>())
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.scale = new Vector3(width, shape.scale.y, shape.scale.z);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = system.name == "Mist" ? 4f * width : 14f * width;
        }
    }

    private static bool NearAny(List<List<Vector2>> lines, Vector2 point, float distance)
    {
        float squared = distance * distance;
        foreach (List<Vector2> line in lines)
        {
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 along = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                if ((line[i] + along * t - point).sqrMagnitude < squared)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Lays the plan's rivers across the open land, in place of an earlier attempt's: running water (drinkable, swimmable, with fish) along
    /// each, its surface as RaidLayoutField worked it out. Kept out of Content. <paramref name="meshes"/> holds the meshes made.
    /// </summary>
    public static void PlaceRivers(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules, List<Mesh> meshes)
    {
        Transform old = map.transform.Find(RiversName);
        if (old != null)
        {
            old.SetParent(null);
            Object.Destroy(old.gameObject);
        }

        if (plan.Rivers.Count == 0 || rules.RiverMaterial == null)
        {
            return;
        }

        var holder = new GameObject(RiversName).transform;
        holder.SetParent(map.transform, false);
        holder.position = map.Terrain.transform.position;
        holder.rotation = Quaternion.identity;
        for (int i = 0; i < plan.Rivers.Count; i++)
        {
            RaidLayoutRiver river = plan.Rivers[i];
            var water = new GameObject($"River {i + 1}");
            water.transform.SetParent(holder, false);
            Mesh mesh = WaterStrip(river.Line, k => river.HalfWidths[k] + RiverIntoWall, k => river.Level[k], k => river.HalfWidths[k], 1f, out Vector4[] centreline);
            meshes.Add(mesh);
            water.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = rules.RiverMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            water.AddComponent<WaterSurface>().SetCentreline(centreline);
        }
    }

    private static void AddRiver(Transform holder, RaidLayoutRavine ravine, Material material, List<Mesh> meshes, int index)
    {
        var water = new GameObject($"River {index + 1}");
        water.transform.SetParent(holder, false);
        Mesh mesh = RiverMesh(ravine, out Vector4[] centreline);
        meshes.Add(mesh);
        water.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = water.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        water.AddComponent<WaterSurface>().SetCentreline(centreline);
    }

    // As the World Builder lays a river: rows across the line, uv in metres, the flow in uv2 (the material reads it from the mesh). Its sides
    // run into the foot of the walls, and under a neck it runs on hidden inside the ground.
    // Along the floor, its sides running into the foot of the walls; under a neck it runs on hidden inside the ground.
    private static Mesh RiverMesh(RaidLayoutRavine ravine, out Vector4[] centreline)
    {
        float FloorHalf(int i) => ravine.HalfWidths[i] - RaidLayoutRavine.WallRun(ravine.HalfWidths[i]);
        return WaterStrip(ravine.Line, i => FloorHalf(i) + RiverIntoWall, i => ravine.Floor[i] + RiverSurface, FloorHalf, 1f, out centreline);
    }

    // Copies of the scene's own fog bank, low along the floor: seen from the rim, the ravine fills with mist.
    private static void AddMist(Transform holder, RaidLayoutRavine ravine, FogArea template, Vector3 origin)
    {
        float run = MistEvery * 0.5f;
        float travelled = 0f;
        for (int i = 0; i + 1 < ravine.Line.Count; i++)
        {
            float length = Vector2.Distance(ravine.Line[i], ravine.Line[i + 1]);
            while (run <= travelled + length)
            {
                float t = (run - travelled) / Mathf.Max(0.0001f, length);
                Vector2 point = Vector2.Lerp(ravine.Line[i], ravine.Line[i + 1], t);
                float floor = Mathf.Lerp(ravine.Floor[i], ravine.Floor[i + 1], t);
                FogArea area = Object.Instantiate(template, holder);
                area.name = "Ravine Mist";
                area.transform.position = origin + new Vector3(point.x, floor + MistAboveFloor, point.y);
                run += MistEvery;
            }

            travelled += length;
        }
    }
}
