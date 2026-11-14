#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the Haven's atlas (Resources/Atlases/SafeHaven): its named regions and named places, for the world map (WorldMapHud) and
/// for finding places (PlaceDiscovery). Run with `unity command script run --file AgentScripts/BuildHavenAtlas.cs --entry
/// BuildHavenAtlas.Apply`. It reads the same plan the world was generated from (HavenWorldPlan.Make on the Haven World Recipe), so
/// run it again whenever the world is generated again.
/// <para>
/// Regions, in the order they win where they meet: the lake and the meres with a strip of shore, the bog, the mountain, the Haven's
/// meadows (inside the recipe's Haven ring), and the Misty Woods beyond, split into one wood between each pair of roads out from the
/// Haven, named by the way it lies. The grid is smoothed so borders don't fray at the bog's ragged rim. Places are the plan's points
/// of interest; where a kind stands more than once each gets a name of its own (Names), in order round the compass.
/// </para>
/// </summary>
public static class BuildHavenAtlas
{
    private const string Recipe = "Assets/_Project/Data/World/Safe Haven/Haven World Recipe.asset";
    private const string AtlasPath = "Assets/_Project/Resources/Atlases/SafeHaven.asset";
    private const int GridSize = 512;
    private const float ShoreStrip = 16f;

    private const string LakeName = "Stillwater";
    private const string BogName = "The Drowning Fen";
    private const string MountainName = "Greyfell";
    private const string MeadowsName = "Haven Meadows";

    // The meres by the recipe's name for them.
    private static readonly Dictionary<string, string> MereNames = new Dictionary<string, string>
    {
        ["Tarn"] = "Hightarn",
        ["Mere"] = "Alder Mere",
    };

    // The woods between the roads, by the way their middle lies from the Haven (0 is north, clockwise).
    private static readonly (float bearing, string name)[] Woods =
    {
        (0f, "Ravenwood"), (45f, "The Cedar Deeps"), (90f, "Eastwood"), (135f, "The Hollow Pines"),
        (180f, "Southwood"), (225f, "The Wolfwood"), (270f, "Westwood"), (315f, "The Grey Pines"),
    };

    // A kind of place that stands more than once: a name for each, in order round the compass from the north.
    private static readonly Dictionary<string, string[]> Names = new Dictionary<string, string[]>
    {
        ["Wayfarer's Stones"] = new[] { "Wayfarer's Stones", "Pilgrim's Stones", "Wanderer's Stones", "Farewell Stones" },
        ["Fisher's Rest"] = new[] { "Fisher's Rest", "Netmender's Rest" },
        ["Shepherd's Fold"] = new[] { "Shepherd's Fold", "Ewe Fold" },
        ["Ruined Watchtower"] = new[] { "Ruined Watchtower", "Broken Tower" },
        ["Lookout Cairn"] = new[] { "Lookout Cairn", "Raven Cairn", "Windy Cairn" },
        ["Old Quarry"] = new[] { "Old Quarry", "Grey Quarry" },
        ["Elder Cedar"] = new[] { "Elder Cedar", "Grandmother Cedar" },
        ["Woodland Camp"] = new[] { "Woodland Camp", "Trapper's Camp", "Charcoal Camp" },
    };

    public static string Apply()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(Recipe);
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe);
        float half = recipe.size * 0.5f;
        var area = new Rect(-half, -half, recipe.size, recipe.size);

        var names = new List<string>();
        int IndexOf(string name)
        {
            int at = names.IndexOf(name);
            if (at < 0)
            {
                names.Add(name);
                at = names.Count - 1;
            }

            return at;
        }

        // The woods' sectors lie between the roads, and their borders follow each road as it winds out.
        List<float[]> roads = RoadCourses(plan, half);
        var grid = new byte[GridSize * GridSize];
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                var point = new Vector2(area.xMin + (x + 0.5f) / GridSize * area.width, area.yMin + (y + 0.5f) / GridSize * area.height);
                grid[y * GridSize + x] = (byte)IndexOf(RegionOf(plan, recipe, point, roads));
            }
        }

        for (int pass = 0; pass < 3; pass++)
        {
            grid = Smooth(grid);
        }

        var regions = new List<WorldAtlas.Region>();
        for (int i = 0; i < names.Count; i++)
        {
            int cells = grid.Count(c => c == i);
            if (cells == 0)
            {
                continue;
            }

            regions.Add(new WorldAtlas.Region { name = names[i], share = cells / (float)grid.Length, label = LabelPoint(grid, i, area) });
        }

        // Regions smoothed away leave gaps in the numbering: number them again by the list that stays.
        var renumber = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            renumber[i] = WorldAtlas.NoRegion;
        }

        for (int i = 0; i < names.Count; i++)
        {
            int kept = regions.FindIndex(r => r.name == names[i]);
            renumber[i] = kept >= 0 ? (byte)kept : WorldAtlas.NoRegion;
        }

        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = renumber[grid[i]];
        }

        // Each region but home has a boundary stone (Territory), beside a path near its middle.
        for (int i = 0; i < regions.Count; i++)
        {
            regions[i].stoneAt = regions[i].name == MeadowsName ? Vector2.zero : StonePoint(plan, grid, i, regions[i].label, area);
        }

        List<WorldAtlas.Place> places = Places(plan);
        Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
        var atlas = AssetDatabase.LoadAssetAtPath<WorldAtlas>(AtlasPath);
        if (atlas == null)
        {
            atlas = ScriptableObject.CreateInstance<WorldAtlas>();
            AssetDatabase.CreateAsset(atlas, AtlasPath);
        }

        atlas.Set(area, GridSize, grid, regions, places);
        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssetIfDirty(atlas);
        return $"Haven atlas: {regions.Count} regions ({string.Join(", ", regions.Select(r => $"{r.name} {r.share * 100f:0}%"))}); {places.Count} places.";
    }

    private static string RegionOf(HavenWorldPlan plan, HavenWorldRecipe recipe, Vector2 point, List<float[]> roads)
    {
        if (plan.Lake != null && plan.Lake.Edge(point) < ShoreStrip)
        {
            return LakeName;
        }

        for (int i = 0; i < plan.Meres.Count; i++)
        {
            if (plan.Meres[i].Edge(point) < ShoreStrip)
            {
                string named = i < plan.MereNames.Count ? plan.MereNames[i] : "Mere";
                return MereNames.TryGetValue(named, out string name) ? name : named;
            }
        }

        if (plan.HasBog && plan.BogWeight(point) > 0.5f)
        {
            return BogName;
        }

        if (plan.MountainWeight(point) > 0.5f)
        {
            return MountainName;
        }

        if (point.magnitude < recipe.haven)
        {
            return MeadowsName;
        }

        return WoodOf(point, roads);
    }

    // The wood between the two roads either side of a point at its distance from the Haven, named for the way the middle of
    // that wood lies where the roads leave the meadows (so a wood keeps one name all the way out).
    private static string WoodOf(Vector2 point, List<float[]> roads)
    {
        float bearing = HavenWorldPlan.BearingOf(point);
        if (roads.Count < 2)
        {
            return NameFor(bearing);
        }

        int ring = Mathf.Clamp(Mathf.RoundToInt(point.magnitude), 0, roads[0].Length - 1);
        var here = roads.Select((course, i) => (bearing: course[ring], index: i)).OrderBy(r => r.bearing).ToList();
        for (int i = 0; i < here.Count; i++)
        {
            float from = here[i].bearing;
            float to = here[(i + 1) % here.Count].bearing;
            float span = Mathf.Repeat(to - from, 360f);
            if (Mathf.Repeat(bearing - from, 360f) <= span)
            {
                // Named by the same pair of roads where they leave the meadows.
                float a = roads[here[i].index][LeavingRing];
                float b = roads[here[(i + 1) % here.Count].index][LeavingRing];
                return NameFor(Mathf.Repeat(a + Mathf.Repeat(b - a, 360f) * 0.5f, 360f));
            }
        }

        return NameFor(bearing);
    }

    private const int LeavingRing = 230;

    private static string NameFor(float bearing) =>
        Woods.OrderBy(w => Mathf.Abs(Mathf.DeltaAngle(w.bearing, bearing))).First().name;

    // Each road's bearing at every metre out from the Haven (index: metres), from its line: where the road is the nearest point of it
    // at that distance; past its end, where it ends.
    private static List<float[]> RoadCourses(HavenWorldPlan plan, float half)
    {
        int reach = Mathf.CeilToInt(half * 1.5f);
        var courses = new List<float[]>();
        for (int p = 0; p < plan.Paths.Count; p++)
        {
            if (plan.PathKinds[p] != HavenWorldPlan.PathKind.Road)
            {
                continue;
            }

            // Points along the line, a metre apart, so a long straight stretch still has a point at every distance.
            List<Vector2> line = plan.Paths[p];
            var dense = new List<Vector2>();
            for (int k = 0; k + 1 < line.Count; k++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(line[k], line[k + 1])));
                for (int s = 0; s < steps; s++)
                {
                    dense.Add(Vector2.Lerp(line[k], line[k + 1], s / (float)steps));
                }
            }

            if (line.Count > 0)
            {
                dense.Add(line[line.Count - 1]);
            }

            var course = new float[reach + 1];
            for (int r = 0; r <= reach; r++)
            {
                Vector2 nearest = dense.OrderBy(point => Mathf.Abs(point.magnitude - r)).First();
                course[r] = HavenWorldPlan.BearingOf(nearest);
            }

            courses.Add(course);
        }

        return courses;
    }

    // Each cell takes the region most of its 5x5 neighbours have: borders follow the land, not its noise.
    private static byte[] Smooth(byte[] grid)
    {
        var smoothed = new byte[grid.Length];
        var counts = new int[256];
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                System.Array.Clear(counts, 0, counts.Length);
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int nx = Mathf.Clamp(x + dx, 0, GridSize - 1);
                        int ny = Mathf.Clamp(y + dy, 0, GridSize - 1);
                        counts[grid[ny * GridSize + nx]]++;
                    }
                }

                byte best = grid[y * GridSize + x];
                for (int i = 0; i < 256; i++)
                {
                    if (counts[i] > counts[best])
                    {
                        best = (byte)i;
                    }
                }

                smoothed[y * GridSize + x] = best;
            }
        }

        return smoothed;
    }

    // Where a region's name goes: the cell of it furthest from its borders and the map's edge (a breadth-first walk in from both).
    private static Vector2 LabelPoint(byte[] grid, int region, Rect area)
    {
        var distance = new int[grid.Length];
        var queue = new Queue<int>();
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                int i = y * GridSize + x;
                bool inside = grid[i] == region;
                bool rim = x == 0 || y == 0 || x == GridSize - 1 || y == GridSize - 1;
                if (!inside || rim)
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
                else
                {
                    distance[i] = int.MaxValue;
                }
            }
        }

        int best = -1;
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            int x = i % GridSize, y = i / GridSize;
            foreach (int n in new[] { x > 0 ? i - 1 : -1, x < GridSize - 1 ? i + 1 : -1, y > 0 ? i - GridSize : -1, y < GridSize - 1 ? i + GridSize : -1 })
            {
                if (n >= 0 && distance[n] == int.MaxValue)
                {
                    distance[n] = distance[i] + 1;
                    queue.Enqueue(n);
                    best = n;
                }
            }
        }

        if (best < 0)
        {
            best = System.Array.IndexOf(grid, (byte)region);
        }

        return new Vector2(area.xMin + (best % GridSize + 0.5f) / GridSize * area.width, area.yMin + (best / GridSize + 0.5f) / GridSize * area.height);
    }

    private static List<WorldAtlas.Place> Places(HavenWorldPlan plan)
    {
        var places = new List<WorldAtlas.Place>();
        foreach (IGrouping<string, HavenWorldPlan.Poi> kind in plan.Pois.Where(p => p.Rule != null).GroupBy(p => p.Rule.name))
        {
            int n = 0;
            foreach (HavenWorldPlan.Poi poi in kind.OrderBy(p => HavenWorldPlan.BearingOf(p.At)))
            {
                string[] own = Names.TryGetValue(kind.Key, out string[] list) ? list : null;
                string name = own != null && n < own.Length ? own[n] : n == 0 ? kind.Key : $"{kind.Key} {n + 1}";
                places.Add(new WorldAtlas.Place
                {
                    id = $"haven.{Slug(kind.Key)}.{n}",
                    name = name,
                    kind = kind.Key,
                    at = poi.At,
                    radius = Mathf.Max(18f, poi.Rule.clearing + 10f),
                });
                n++;
            }
        }

        return places;
    }

    // The point on a path inside the region nearest its middle, on dry ground, set a few metres off the path; else one of its
    // places; else its middle.
    private static Vector2 StonePoint(HavenWorldPlan plan, byte[] grid, int region, Vector2 label, Rect area)
    {
        bool Inside(Vector2 p)
        {
            int x = Mathf.FloorToInt((p.x - area.xMin) / area.width * GridSize);
            int y = Mathf.FloorToInt((p.y - area.yMin) / area.height * GridSize);
            return x >= 0 && y >= 0 && x < GridSize && y < GridSize && grid[y * GridSize + x] == region;
        }

        bool Dry(Vector2 p) => (plan.Lake == null || plan.Lake.Edge(p) > 3f) && plan.Meres.All(m => m.Edge(p) > 3f) && !(plan.HasBog && plan.BogWet(p))
                               && !NearRiver(plan, p);

        Vector2 best = Vector2.zero, along = Vector2.up;
        float bestDistance = float.MaxValue;
        foreach (List<Vector2> path in plan.Paths)
        {
            for (int k = 0; k + 1 < path.Count; k++)
            {
                Vector2 a = path[k], b = path[k + 1];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 3f));
                for (int s = 0; s <= steps; s++)
                {
                    Vector2 point = Vector2.Lerp(a, b, s / (float)steps);
                    float distance = (point - label).sqrMagnitude;
                    if (distance < bestDistance && Inside(point) && Dry(point))
                    {
                        bestDistance = distance;
                        best = point;
                        along = (b - a).sqrMagnitude > 0.01f ? (b - a).normalized : Vector2.up;
                    }
                }
            }
        }

        if (bestDistance < float.MaxValue)
        {
            var side = new Vector2(-along.y, along.x) * 4.5f;
            foreach (Vector2 beside in new[] { best + side, best - side })
            {
                if (Inside(beside) && Dry(beside))
                {
                    return beside;
                }
            }

            return best;
        }

        HavenWorldPlan.Poi poi = plan.Pois.Where(p => Inside(p.At)).OrderBy(p => (p.At - label).sqrMagnitude).FirstOrDefault();
        if (poi != null)
        {
            return poi.At + new Vector2(6f, 0f);
        }

        // No path and no place (a mere ringed by its shore): the dry ground of it nearest its middle, a little back from the water.
        Vector2 shore = label;
        float nearest = float.MaxValue;
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                var point = new Vector2(area.xMin + (x + 0.5f) / GridSize * area.width, area.yMin + (y + 0.5f) / GridSize * area.height);
                float distance = (point - label).sqrMagnitude;
                if (distance < nearest && grid[y * GridSize + x] == region && Dry(point) && plan.Meres.All(m => m.Edge(point) > 6f)
                    && (plan.Lake == null || plan.Lake.Edge(point) > 6f))
                {
                    nearest = distance;
                    shore = point;
                }
            }
        }

        return shore;
    }

    // Within a river's or stream's banks, or close to them: paths cross water at fords, and a stone must not stand in one.
    private static bool NearRiver(HavenWorldPlan plan, Vector2 point)
    {
        for (int r = 0; r < plan.Rivers.Count; r++)
        {
            List<Vector2> line = plan.Rivers[r];
            float clear = (r < plan.RiverWidths.Count ? plan.RiverWidths[r] * 0.5f : 3f) + 5f;
            for (int k = 0; k + 1 < line.Count; k++)
            {
                Vector2 a = line[k], b = line[k + 1];
                Vector2 ab = b - a;
                float along = ab.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
                if ((a + ab * along - point).sqrMagnitude < clear * clear)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Slug(string name) => System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
}
