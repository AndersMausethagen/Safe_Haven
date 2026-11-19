using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edges in the wild ground (Docs/WorldDesignReview_2026-11-17.md G2, RaidLayoutPlanner.Barriers): the band stays off everything walkable,
/// every run of it is broken by gaps, the same seed gives the same band, it moves nothing placed before it, and with it standing every
/// clearing, site and nearly all the open ground can still be walked to (a flood fill over a 1 m grid with the runs as walls).
/// </summary>
public sealed class RaidBarrierTests
{
    private const string WoodsRulesPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string ValleyRulesPath = "Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset";
    private const string MarshRulesPath = "Assets/_Project/Data/Raids/Maps/MossMarsh Layout.asset";
    private const string WoodsMapPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const float Size = 200f;
    private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };

    private static RaidLayoutRules Walled(string path, float share = 1f)
    {
        // The asset's own band (Edge From as shipped: 11 m in the Woods, 12 in the Marsh, 16 in the Valley), every mood at this share.
        RaidLayoutRules rules = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(path));
        rules.ConfigureBarriers(rules.Barriers, share);
        return rules;
    }

    [Test]
    public void Defaults_WallNothing()
    {
        var fresh = new RaidBarrierRule();
        Assert.AreEqual(16f, fresh.edgeFrom);
        Assert.AreEqual(0f, new RaidMapMood().barrier, "no mood walls by default");
        RaidMapDefinition map = ScriptableObject.CreateInstance<RaidMapDefinition>();
        try
        {
            Assert.IsEmpty(map.BarrierDressing);
            Assert.IsFalse(map.BarrierInWater);
        }
        finally
        {
            Object.DestroyImmediate(map);
        }

        RaidLayoutRules rules = Walled(WoodsRulesPath, 0f);
        try
        {
            foreach (int seed in Seeds)
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                Assert.IsEmpty(plan.Barriers, $"seed {seed}");
                Assert.IsEmpty(plan.BarrierGaps, $"seed {seed}");
            }
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void TheBand_KeepsOffWalkableGround_AndHasGaps()
    {
        foreach (string path in new[] { WoodsRulesPath, ValleyRulesPath, MarshRulesPath })
        {
            RaidLayoutRules rules = Walled(path);
            try
            {
                RaidBarrierRule rule = rules.Barriers;
                int runs = 0, gaps = 0;
                foreach (int seed in Seeds)
                {
                    RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                    float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
                    runs += plan.Barriers.Count;
                    gaps += plan.BarrierGaps.Count;
                    // In a valley a line's gaps may all fall up its walls (no wall stands there, so none counts): its ends are open anyway.
                    if (plan.Barriers.Count > 0 && plan.Valley == null)
                    {
                        Assert.IsNotEmpty(plan.BarrierGaps, $"{path} seed {seed}: a band with no way through");
                    }

                    // No two runs meet end to end: where they did (a ring's closing point) the wall was whole but its dressing kept back
                    // as if at a gap, a hidden wall (review 2026-11-19).
                    foreach (RaidLayoutBarrier run in plan.Barriers)
                    {
                        foreach (RaidLayoutBarrier other in plan.Barriers)
                        {
                            if (other == run)
                            {
                                continue;
                            }

                            Assert.Greater(Vector2.Distance(run.Points[run.Points.Count - 1], other.Points[0]), 1.5f, $"{path} seed {seed}: two runs joined with no gap");
                        }
                    }

                    foreach (RaidLayoutBarrier run in plan.Barriers)
                    {
                        // A run lies between two gaps: never longer than the most wall between them, plus as far as a gap is moved off a
                        // boulder, landmark or ravine lip (up to six of its widths: RaidLayoutPlanner.FitGap), and a cell's slack.
                        Assert.LessOrEqual(run.Length, rule.gapEvery.y + rule.gapWidth.y * 6f + RaidLayoutPlanner.BarrierCell * 2f, $"{path} seed {seed}: a run longer than Gap Every");
                        foreach (Vector2 point in run.Points)
                        {
                            // The traced line sits on the Edge From level of a chamfer distance (within ~8% and a cell).
                            float least = rule.edgeFrom * 0.9f - RaidLayoutPlanner.BarrierCell;
                            for (int link = 0; link < plan.Trails.Count; link++)
                            {
                                float width = plan.IsRoad(link) ? halfWidth * rules.RoadWidth : halfWidth;
                                Assert.Greater(DistanceToLine(plan.Trails[link], point) - width, least, $"{path} seed {seed}: the band by a trail");
                            }

                            foreach (RaidLayoutRoom room in plan.Rooms)
                            {
                                Assert.Greater(Vector2.Distance(room.Position, point) - room.Radius, least, $"{path} seed {seed}: the band by a clearing");
                            }

                            foreach (RaidSite site in plan.Sites)
                            {
                                Assert.Greater(Vector2.Distance(site.Position * Size, point) - rule.siteRoom, least, $"{path} seed {seed}: the band by a site");
                            }

                            float fromEdge = Mathf.Min(Mathf.Min(point.x, point.y), Mathf.Min(Size - point.x, Size - point.y)) - rules.EdgeMargin;
                            Assert.Greater(fromEdge, least, $"{path} seed {seed}: the band along the map's edge (a dead-end strip by the rim)");
                            if (plan.Valley != null)
                            {
                                Assert.LessOrEqual(plan.Valley.FloorEdge(point), 0f, $"{path} seed {seed}: walls only on the valley's floor");
                            }
                        }
                    }
                }

                // The Valley's floor is nearly all within the band's distance of a trail (its walls are its edges): its few runs are
                // checked above, but it isn't asked to have any.
                if (path != ValleyRulesPath)
                {
                    Assert.Greater(runs, Seeds.Length, $"{path}: walls on most maps");
                    Assert.Greater(gaps, Seeds.Length, $"{path}: and gaps in them");
                }
            }
            finally
            {
                Object.DestroyImmediate(rules);
            }
        }
    }

    [Test]
    public void TheBand_MovesNothingPlacedBefore_AndRepeatsForTheSeed()
    {
        RaidLayoutRules open = Walled(WoodsRulesPath, 0f);
        RaidLayoutRules walled = Walled(WoodsRulesPath);
        try
        {
            foreach (int seed in new[] { 1, 2, 3, 4 })
            {
                Assert.AreEqual(RaidLayoutGoldenTests.Fingerprint(open, Size, seed), RaidLayoutGoldenTests.Fingerprint(walled, Size, seed), $"seed {seed}");
                RaidLayoutPlan first = RaidLayoutPlanner.Plan(walled, Size, seed), again = RaidLayoutPlanner.Plan(walled, Size, seed);
                CollectionAssert.AreEqual(first.Barriers.SelectMany(run => run.Points), again.Barriers.SelectMany(run => run.Points), $"seed {seed}");
                CollectionAssert.AreEqual(first.BarrierGaps, again.BarrierGaps, $"seed {seed}");
            }
        }
        finally
        {
            Object.DestroyImmediate(open);
            Object.DestroyImmediate(walled);
        }
    }

    [Test]
    public void WithTheBand_EverythingIsStillReached()
    {
        // A 1 m grid, the runs as walls as thick as their colliders and a creature's width; flood from the start.
        const float cell = 1f;
        const float block = RaidMapGenerator.BarrierThickness * 0.5f + 0.5f;
        foreach (string path in new[] { WoodsRulesPath, MarshRulesPath })
        {
            RaidLayoutRules rules = Walled(path);
            try
            {
                int walledMaps = 0;
                foreach (int seed in Seeds)
                {
                    RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                    if (plan.Barriers.Count == 0)
                    {
                        continue;
                    }

                    walledMaps++;
                    int cells = Mathf.CeilToInt(Size / cell);
                    var blocked = new bool[cells * cells];
                    foreach (RaidLayoutBarrier run in plan.Barriers)
                    {
                        for (int i = 1; i < run.Points.Count; i++)
                        {
                            Vector2 a = run.Points[i - 1], b = run.Points[i];
                            int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - block) / cell)), x1 = Mathf.Min(cells - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + block) / cell));
                            int z0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - block) / cell)), z1 = Mathf.Min(cells - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + block) / cell));
                            for (int z = z0; z <= z1; z++)
                            {
                                for (int x = x0; x <= x1; x++)
                                {
                                    if (DistanceToSegment(new Vector2((x + 0.5f) * cell, (z + 0.5f) * cell), a, b) <= block)
                                    {
                                        blocked[z * cells + x] = true;
                                    }
                                }
                            }
                        }
                    }

                    var reached = new bool[cells * cells];
                    var open = new Queue<int>();
                    int start = Index(plan.Start.Position, cells, cell);
                    Assert.IsFalse(blocked[start], $"{path} seed {seed}: the start walled");
                    reached[start] = true;
                    open.Enqueue(start);
                    while (open.Count > 0)
                    {
                        int at = open.Dequeue();
                        int x = at % cells, z = at / cells;
                        foreach (int next in new[] { x > 0 ? at - 1 : -1, x < cells - 1 ? at + 1 : -1, z > 0 ? at - cells : -1, z < cells - 1 ? at + cells : -1 })
                        {
                            if (next >= 0 && !blocked[next] && !reached[next])
                            {
                                reached[next] = true;
                                open.Enqueue(next);
                            }
                        }
                    }

                    foreach (RaidLayoutRoom room in plan.Rooms)
                    {
                        Assert.IsTrue(reached[Index(room.Position, cells, cell)], $"{path} seed {seed}: a clearing cut off");
                    }

                    foreach (RaidSite site in plan.Sites)
                    {
                        Assert.IsTrue(reached[Index(site.Position * Size, cells, cell)], $"{path} seed {seed}: a {site.Kind} cut off");
                    }

                    int free = 0, got = 0;
                    for (int i = 0; i < blocked.Length; i++)
                    {
                        free += blocked[i] ? 0 : 1;
                        got += reached[i] ? 1 : 0;
                    }

                    Assert.GreaterOrEqual(got, free * 0.98f, $"{path} seed {seed}: ground walled in with no gap ({free - got} of {free} m² unreached)");
                }

                Assert.GreaterOrEqual(walledMaps, Seeds.Length / 2, $"{path}: walls on half the maps or more");
            }
            finally
            {
                Object.DestroyImmediate(rules);
            }
        }
    }

    [Test]
    public void TheDressing_GrowsAlongTheRuns()
    {
        RaidMapDefinition map = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(WoodsMapPath));
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(map.Size, 30f, map.Size) };
        try
        {
            map.ConfigureDressing(new RaidMapDefinition.Hero[0], new RaidMapDefinition.Scatter[0], 30f, 22f, new Vector2(6f, 15f), 10f,
                new Vector2Int(2, 4), new Vector2(1.5f, 12f));
            map.ConfigureBarrierDressing(new[] { map.Rocks[0] }, 1.4f, 1.6f, false);
            const int zones = 16;
            map.UseGenerated(zones, new byte[zones * zones], Enumerable.Repeat((byte)90, zones * zones).ToArray(), new byte[zones * zones], new RaidMapMood(), 21);
            var run = new RaidLayoutBarrier();
            for (float x = 40f; x <= 160f; x += 2f)
            {
                run.Points.Add(new Vector2(x, 100f));
            }

            RaidMapSurface.Scatter(data, map, new List<RaidMapSurface.Clearing>(), out _, barriers: new[] { run });
            RaidMapSurface.Group[] groups = RaidMapSurface.PrototypeGroups(map);
            int barrierPrototype = System.Array.IndexOf(groups, RaidMapSurface.Group.Barrier);
            Assert.GreaterOrEqual(barrierPrototype, 0, "a prototype of its own");
            Assert.AreEqual(groups.Length, data.treePrototypes.Length, "the groups follow the prototypes");
            TreeInstance[] dressing = data.treeInstances.Where(tree => tree.prototypeIndex == barrierPrototype).ToArray();
            // Two picks every 1.4 m along 120 m is about 170 tries; the forest's own trunks take some.
            Assert.Greater(dressing.Length, 40, "thick along 120 m of run");
            foreach (TreeInstance tree in dressing)
            {
                float z = tree.position.z * map.Size;
                Assert.AreEqual(100f, z, 1.6f + 0.1f, "within the band's depth of its line");
            }
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(map);
        }
    }

    [Test]
    public void TheWindthrow_LiesAlongTheRun_InsideIt_WithAPostAtEachEnd()
    {
        RaidMapDefinition map = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(WoodsMapPath));
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(map.Size, 30f, map.Size) };
        try
        {
            Assert.IsNotEmpty(map.BarrierLogs, "the Woods lays windthrow along its walls");
            Assert.IsNotEmpty(map.BarrierEnds, "and stands a post by each gap");
            map.ConfigureDressing(new RaidMapDefinition.Hero[0], new RaidMapDefinition.Scatter[0], 30f, 22f, new Vector2(6f, 15f), 10f,
                new Vector2Int(2, 4), new Vector2(1.5f, 12f));
            const int zones = 16;
            map.UseGenerated(zones, new byte[zones * zones], Enumerable.Repeat((byte)90, zones * zones).ToArray(), new byte[zones * zones], new RaidMapMood(), 21);
            var run = new RaidLayoutBarrier();
            for (float x = 40f; x <= 160f; x += 2f)
            {
                run.Points.Add(new Vector2(x, 100f));
            }

            var logs = new List<RaidMapSurface.LogObject>();
            RaidMapSurface.Scatter(data, map, new List<RaidMapSurface.Clearing>(), out _, barriers: new[] { run }, barrierLogs: logs);
            // Two rows along 120 m of flat ground, logs and rocks 3-9 m long, each a little over the one before.
            Assert.Greater(logs.Count, 20, "laid end to end along 120 m");
            float maxTurn = map.BarrierLogTurn + 50f;
            foreach (RaidMapSurface.LogObject log in logs)
            {
                Bounds bounds = RaidMapSurface.PrefabBounds(log.Prefab);
                float width = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)) * log.Prefab.transform.localScale.x * log.Scale;
                float along = log.Half * Mathf.Abs(log.Axis.x) + width * Mathf.Abs(log.Axis.y);
                Assert.GreaterOrEqual(log.Position.x - along, 40f + 0.5f - 0.01f, $"{log.Prefab.name} keeps inside the run's start, off its gap");
                Assert.LessOrEqual(log.Position.x + along, 160f - 0.5f + 0.01f, $"{log.Prefab.name} keeps inside the run's end, off its gap");
                Assert.AreEqual(100f, log.Position.y, 0.6f * map.BarrierDepth + 0.01f, "in a row by the run's line");
                Assert.GreaterOrEqual(Mathf.Abs(log.Axis.x), Mathf.Cos(maxTurn * Mathf.Deg2Rad) - 0.01f, "turned with the run (or crossed over one)");
            }

            Assert.Greater(logs.Count(log => Mathf.Abs(log.Axis.x) >= Mathf.Cos(map.BarrierLogTurn * Mathf.Deg2Rad) - 0.01f), logs.Count / 2,
                "most lie along the run");

            RaidMapSurface.Group[] groups = RaidMapSurface.PrototypeGroups(map);
            var posts = new HashSet<int>();
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == RaidMapSurface.Group.Barrier && map.BarrierEnds.Any(end => end.prefab == data.treePrototypes[i].prefab))
                {
                    posts.Add(i);
                }
            }

            TreeInstance[] standing = data.treeInstances.Where(tree => posts.Contains(tree.prototypeIndex)).ToArray();
            float[] postsAt = standing.Select(tree => tree.position.x * map.Size).ToArray();
            Assert.IsTrue(postsAt.Any(x => x < 45f) && postsAt.Any(x => x > 155f), $"a post by each end (at {string.Join(", ", postsAt)})");
            foreach (TreeInstance post in standing)
            {
                GameObject prefab = data.treePrototypes[post.prototypeIndex].prefab;
                Bounds bounds = RaidMapSurface.PrefabBounds(prefab);
                float reach = Mathf.Max(Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)), Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)))
                              * prefab.transform.localScale.x * post.widthScale;
                float x = post.position.x * map.Size;
                Assert.GreaterOrEqual(x - reach, 40f + 0.5f - 0.01f, $"{prefab.name} stands inside the run, off its gap");
                Assert.LessOrEqual(x + reach, 160f - 0.5f + 0.01f, $"{prefab.name} stands inside the run, off its gap");
            }
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(map);
        }
    }

    [Test]
    public void AGap_GetsAGameTrailAcrossTheBand()
    {
        RaidMapDefinition map = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(WoodsMapPath));
        var data = new TerrainData { heightmapResolution = 33, alphamapResolution = 128, size = new Vector3(map.Size, 30f, map.Size) };
        try
        {
            Assert.Greater(map.BarrierGapTrail, 0f, "the Woods paints its gaps' game trails");
            data.terrainLayers = map.Layers;
            const int zones = 16;
            map.UseGenerated(zones, new byte[zones * zones], Enumerable.Repeat((byte)90, zones * zones).ToArray(), new byte[zones * zones], new RaidMapMood(), 21);
            // Two runs along z = 100 with a gap at x = 100 between them.
            var left = new RaidLayoutBarrier();
            var right = new RaidLayoutBarrier();
            for (float x = 40f; x <= 96f; x += 2f) left.Points.Add(new Vector2(x, 100f));
            for (float x = 104f; x <= 160f; x += 2f) right.Points.Add(new Vector2(x, 100f));
            var runs = new[] { left, right };
            var gap = new Vector2(100f, 100f);

            float PathAt(Vector2 spot)
            {
                float cell = map.Size / data.alphamapResolution;
                return data.GetAlphamaps(Mathf.FloorToInt(spot.x / cell), Mathf.FloorToInt(spot.y / cell), 1, 1)[0, 0, 1];
            }

            RaidMapSurface.Paint(data, map, barriers: runs, gaps: new List<Vector2>());
            float inWithout = PathAt(gap + Vector2.up * 6f), outWithout = PathAt(gap - Vector2.up * 6f);
            float besideWithout = PathAt(new Vector2(70f, 106f));
            RaidMapSurface.Paint(data, map, barriers: runs, gaps: new[] { gap });
            Assert.Greater(PathAt(gap + Vector2.up * 6f) - inWithout, 0.2f, "soil worn across the band, 6 m in from the gap");
            Assert.Greater(PathAt(gap - Vector2.up * 6f) - outWithout, 0.2f, "and 6 m out");
            Assert.AreEqual(besideWithout, PathAt(new Vector2(70f, 106f)), 0.01f, "nothing beside a run away from its gap");
            byte[,] verges = RaidMapSurface.Paint(data, map, barriers: runs, gaps: new[] { gap });
            float vergeCell = map.Size / data.alphamapResolution;
            Assert.Less(verges[Mathf.FloorToInt((gap.y + 6f) / vergeCell), Mathf.FloorToInt(gap.x / vergeCell)], 128, "the verge's grass keeps off the trail");
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(map);
        }
    }

    private static int Index(Vector2 point, int cells, float cell) =>
        Mathf.Clamp(Mathf.FloorToInt(point.y / cell), 0, cells - 1) * cells + Mathf.Clamp(Mathf.FloorToInt(point.x / cell), 0, cells - 1);

    private static float DistanceToLine(List<Vector2> line, Vector2 point)
    {
        float best = float.MaxValue;
        for (int i = 1; i < line.Count; i++)
        {
            best = Mathf.Min(best, DistanceToSegment(point, line[i - 1], line[i]));
        }

        return best;
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(point, a + ab * t);
    }
}
