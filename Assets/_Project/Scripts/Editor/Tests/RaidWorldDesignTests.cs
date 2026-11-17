using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The generator changes of Docs/WorldDesignReview_2026-11-17.md §4, on plans, height fields and a bare TerrainData (no scene): districts on
/// normal maps with sites shared by role (G1), landmarks at nodes and short flags along the roads (G4), roads wider than side trails and
/// their own paint (G5), the Valley's crossings capped (G8) and the hero and mid dressing (G9). For each: its defaults change nothing (the
/// Misty Woods' maps are also pinned by RaidLayoutGoldenTests), it does its thing on a seed, and the same seed gives the same map.
/// </summary>
public sealed class RaidWorldDesignTests
{
    private const string WoodsRulesPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string ValleyRulesPath = "Assets/_Project/Data/Raids/Maps/TrollValley Layout.asset";
    private const string MarshRulesPath = "Assets/_Project/Data/Raids/Maps/MossMarsh Layout.asset";
    private const string WoodsMapPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const float Size = 200f;
    private const int Zones = 16;
    private static readonly int[] Seeds = { 1, 2, 3, 4, 5, 6, 7, 8 };

    private static RaidLayoutRules Load(string path) => AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(path);

    // ----- Defaults -----

    [Test]
    public void NewFields_DefaultToTodaysBehaviour()
    {
        RaidLayoutRules fresh = ScriptableObject.CreateInstance<RaidLayoutRules>();
        RaidMapDefinition map = ScriptableObject.CreateInstance<RaidMapDefinition>();
        try
        {
            Assert.AreEqual(Vector2Int.one, fresh.NormalRegions, "one district on a normal map");
            Assert.AreEqual(Vector2Int.one, fresh.Regions);
            Assert.IsEmpty(fresh.DistrictRoles);
            Assert.AreEqual(1f, fresh.RoadWidth, "roads as wide as any trail");
            Assert.IsEmpty(fresh.ShortFlags, "no flags");
            Assert.AreEqual(0, fresh.Valley.maxCrossings, "no limit on crossings");
            var mood = new RaidMapMood();
            Assert.AreEqual(RaidDistrictRole.Any, mood.role);
            Assert.IsEmpty(mood.siteDensity);
            Assert.AreEqual(1f, fresh.SiteDensity(RaidDistrictRole.Lair, mood, RaidSiteKind.EnemyCamp), "every district weighs the same");
            var landmark = new RaidLandmarkRule();
            Assert.IsFalse(landmark.atNodes);
            Assert.IsFalse(landmark.Marked, "no minimap mark and no sound");
            Assert.IsNull(map.RoadLayer);
            Assert.AreEqual(-1, map.RoadLayerIndex);
            Assert.IsEmpty(map.Heroes);
            Assert.IsEmpty(map.Mids);
        }
        finally
        {
            Object.DestroyImmediate(fresh);
            Object.DestroyImmediate(map);
        }
    }

    [Test]
    public void TheRaidsRules_AtTheNewDefaults_PlanAsTheyDid()
    {
        // Every new setting written out at its default onto a copy: the plan and its heights must not move (the Woods' are also golden).
        foreach (string path in new[] { WoodsRulesPath, ValleyRulesPath, MarshRulesPath })
        {
            RaidLayoutRules asset = Load(path);
            RaidLayoutRules copy = Object.Instantiate(asset);
            try
            {
                copy.ConfigureDistricts(Vector2Int.one, new RaidDistrictRule[0]);
                copy.ConfigureRoads(1f, new GameObject[0], new Vector2(100f, 140f), 1.5f);
                copy.ConfigureNodeLandmarks(new Vector2Int(3, 5));
                copy.Valley.maxCrossings = 0;
                foreach (int seed in new[] { 1, 2, 3 })
                {
                    Assert.AreEqual(RaidLayoutGoldenTests.Fingerprint(asset, Size, seed), RaidLayoutGoldenTests.Fingerprint(copy, Size, seed), $"{path}, seed {seed}");
                    RaidLayoutPlan plan = RaidLayoutPlanner.Plan(copy, Size, seed);
                    Assert.AreEqual(1, Mathf.Max(1, plan.Regions.Count), $"{path}, seed {seed}: one district");
                    Assert.IsEmpty(plan.Flags);
                    Assert.IsFalse(plan.Landmarks.Any(landmark => landmark.Rule >= 0), "no landmark at a node without a rule for one");
                }
            }
            finally
            {
                Object.DestroyImmediate(copy);
            }
        }
    }

    // ----- G1: districts -----

    private static RaidLayoutRules WoodsWithDistricts()
    {
        RaidLayoutRules rules = Object.Instantiate(Load(WoodsRulesPath));
        rules.ConfigureDistricts(new Vector2Int(2, 2), new[]
        {
            new RaidDistrictRule { role = RaidDistrictRole.Approach, siteDensity = new List<RaidSiteDensity>
            {
                new RaidSiteDensity { kind = RaidSiteKind.EnemyCamp, density = 0.5f },
                new RaidSiteDensity { kind = RaidSiteKind.ResourceSite, density = 2f },
            } },
            new RaidDistrictRule { role = RaidDistrictRole.Lair, siteDensity = new List<RaidSiteDensity>
            {
                new RaidSiteDensity { kind = RaidSiteKind.EnemyCamp, density = 3f },
                new RaidSiteDensity { kind = RaidSiteKind.ResourceSite, density = 0.5f },
            } },
        });
        return rules;
    }

    [Test]
    public void Districts_SplitANormalMap_StartsInTheApproach_AndTheApexDistrictIsTheLair()
    {
        RaidLayoutRules rules = WoodsWithDistricts();
        try
        {
            foreach (int seed in Seeds)
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                Assert.AreEqual(2, plan.Regions.Count, $"seed {seed}: two districts at 200 m");
                Assert.AreEqual(RaidDistrictRole.Approach, plan.Regions[0].Role, $"seed {seed}");
                Assert.AreEqual(RaidDistrictRole.Lair, plan.Regions[1].Role, $"seed {seed}: the other is the Lair");
            }
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void Districts_ShareSitesByRole_NotByArea_AndKeepTheMapsCounts()
    {
        RaidLayoutRules rules = WoodsWithDistricts();
        RaidLayoutRules plain = Load(WoodsRulesPath);
        try
        {
            int campsInLair = 0, campsInApproach = 0, campsWith = 0, campsWithout = 0, resourcesInLair = 0, resourcesInApproach = 0;
            foreach (int seed in Seeds)
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                foreach (RaidSite site in plan.Sites)
                {
                    bool lair = plan.Regions[plan.RegionIndexAt(site.Position * Size)].Role == RaidDistrictRole.Lair;
                    if (site.Kind == RaidSiteKind.EnemyCamp)
                    {
                        campsWith++;
                        campsInLair += lair ? 1 : 0;
                        campsInApproach += lair ? 0 : 1;
                    }
                    else if (site.Kind == RaidSiteKind.ResourceSite)
                    {
                        resourcesInLair += lair ? 1 : 0;
                        resourcesInApproach += lair ? 0 : 1;
                    }
                }

                campsWithout += RaidLayoutPlanner.Plan(plain, Size, seed).SitesOf(RaidSiteKind.EnemyCamp).Count;
            }

            Assert.Greater(campsInLair, campsInApproach * 2, "camps gather in the Lair (3 against 0.5)");
            Assert.Greater(resourcesInApproach, resourcesInLair, "resources in the Approach (2 against 0.5)");
            Assert.GreaterOrEqual(campsWith, Mathf.FloorToInt(campsWithout * 0.85f), "the map's count of camps holds (what didn't fit spills over)");
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void Districts_SameSeed_SameMap()
    {
        RaidLayoutRules rules = WoodsWithDistricts();
        RaidLayoutRules again = WoodsWithDistricts();
        try
        {
            foreach (int seed in new[] { 1, 2, 3 })
            {
                Assert.AreEqual(RaidLayoutGoldenTests.Fingerprint(rules, Size, seed), RaidLayoutGoldenTests.Fingerprint(again, Size, seed), $"seed {seed}");
            }
        }
        finally
        {
            Object.DestroyImmediate(rules);
            Object.DestroyImmediate(again);
        }
    }

    // ----- G4: landmarks at nodes, short flags -----

    private static RaidLayoutRules WoodsWithNodeLandmarks(bool districts)
    {
        RaidLayoutRules rules = districts ? WoodsWithDistricts() : Object.Instantiate(Load(WoodsRulesPath));
        var landmarks = new List<RaidLandmarkRule>(rules.Landmarks);
        GameObject prefab = rules.Landmarks[0].prefab;
        for (int i = 0; i < 3; i++)
        {
            landmarks.Add(new RaidLandmarkRule { prefab = prefab, weight = 1f, atNodes = true, clearing = 4f, revealRadius = 70f });
        }

        rules.ConfigureLandmarks(landmarks, rules.LandmarkCount);
        rules.ConfigureNodeLandmarks(new Vector2Int(3, 5));
        return rules;
    }

    [Test]
    public void NodeLandmarks_StandAtNodes_EachADifferentKind_OffTheTrails()
    {
        RaidLayoutRules rules = WoodsWithNodeLandmarks(districts: true);
        try
        {
            int placed = 0;
            foreach (int seed in Seeds)
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                List<RaidLayoutLandmark> atNodes = plan.Landmarks.Where(landmark => landmark.Rule >= 0).ToList();
                placed += atNodes.Count;
                Assert.LessOrEqual(atNodes.Count, 5, $"seed {seed}");
                // Each kind once while another kind is left: with more landmarks than kinds, a kind comes again.
                int kinds = rules.Landmarks.Count(rule => rule.atNodes);
                Assert.AreEqual(Mathf.Min(atNodes.Count, kinds), atNodes.Select(landmark => landmark.Rule).Distinct().Count(), $"seed {seed}: each a different kind while another is left");
                float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
                foreach (RaidLayoutLandmark landmark in atNodes)
                {
                    Assert.IsTrue(rules.Landmarks[landmark.Rule].atNodes, $"seed {seed}");
                    Assert.IsTrue(plan.Rooms.Any(room => (room.Role == RaidRoomRole.Hub || room.Role == RaidRoomRole.Apex || (room.Role == RaidRoomRole.Plain && room.Links >= 3))
                        && Vector2.Distance(room.Position, landmark.Position) <= room.Radius * 1.25f + 0.01f), $"seed {seed}: at the edge of a node");
                    Assert.Greater(DistanceToLines(plan.Trails, landmark.Position), halfWidth + 2f, $"seed {seed}: never in a way");
                    Assert.IsTrue(plan.Pockets.Any(pocket => pocket.centre == landmark.Position && Mathf.Approximately(pocket.radius, 4f)), $"seed {seed}: its ground cleared");
                }
            }

            Assert.GreaterOrEqual(placed, Seeds.Length * 3 / 2, "one and a half or more a map on average");
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void NodeLandmarks_MoveNothingPlacedBefore_AndRepeatForTheSeed()
    {
        RaidLayoutRules plain = Load(WoodsRulesPath);
        RaidLayoutRules rules = WoodsWithNodeLandmarks(districts: false);
        try
        {
            foreach (int seed in new[] { 1, 2, 3, 4 })
            {
                RaidLayoutPlan before = RaidLayoutPlanner.Plan(plain, Size, seed);
                RaidLayoutPlan after = RaidLayoutPlanner.Plan(rules, Size, seed);
                CollectionAssert.AreEqual(before.Rooms.Select(room => room.Position), after.Rooms.Select(room => room.Position), $"seed {seed}");
                CollectionAssert.AreEqual(before.Sites.Select(site => site.Position), after.Sites.Select(site => site.Position), $"seed {seed}");
                CollectionAssert.AreEqual(before.Boulders.Select(boulder => boulder.Position), after.Boulders.Select(boulder => boulder.Position), $"seed {seed}");
                CollectionAssert.AreEqual(before.Landmarks.Select(landmark => landmark.Position), after.Landmarks.Where(landmark => landmark.Rule < 0).Select(landmark => landmark.Position),
                    $"seed {seed}: the hill-top landmarks stay");
                RaidLayoutPlan again = RaidLayoutPlanner.Plan(rules, Size, seed);
                CollectionAssert.AreEqual(after.Landmarks.Select(landmark => landmark.Position), again.Landmarks.Select(landmark => landmark.Position), $"seed {seed}");
            }
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void ShortFlags_LineBothRoutes_BesideTheTrail_AtTheirSpacing()
    {
        RaidLayoutRules plain = Load(WoodsRulesPath);
        RaidLayoutRules rules = Object.Instantiate(plain);
        rules.ConfigureRoads(1f, new[] { plain.Landmarks[0].prefab }, new Vector2(100f, 140f), 1.5f);
        try
        {
            int flags = 0;
            foreach (int seed in Seeds)
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                RaidLayoutPlan before = RaidLayoutPlanner.Plan(plain, Size, seed);
                CollectionAssert.AreEqual(before.Sites.Select(site => site.Position), plan.Sites.Select(site => site.Position), $"seed {seed}: flags move nothing");
                float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
                var routes = plan.ShortRoute.Concat(plan.LongRoute).Select(link => plan.Trails[link]).ToList();
                flags += plan.Flags.Count;
                foreach (RaidLayoutFlag flag in plan.Flags)
                {
                    Assert.That(DistanceToLines(routes, flag.Position), Is.InRange(halfWidth + 0.5f, halfWidth + 1.51f), $"seed {seed}: beside a route, just off its edge");
                    Assert.Greater(DistanceToLines(plan.Trails, flag.Position), halfWidth + 0.4f, $"seed {seed}: on no trail");
                    Assert.IsFalse(plan.Rooms.Any(room => Vector2.Distance(room.Position, flag.Position) < room.Radius), $"seed {seed}: not in a clearing");
                    Assert.IsFalse(plan.Flags.Any(other => other != flag && Vector2.Distance(other.Position, flag.Position) < 50f), $"seed {seed}: spaced");
                }

                CollectionAssert.AreEqual(plan.Flags.Select(flag => flag.Position), RaidLayoutPlanner.Plan(rules, Size, seed).Flags.Select(flag => flag.Position), $"seed {seed}");
            }

            Assert.Greater(flags, Seeds.Length, "a flag or more a map");
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void ALandmark_IsFoundWithinItsRadius_AndStaysFound()
    {
        var thing = new GameObject("Landmark");
        try
        {
            var landmark = thing.AddComponent<RaidMapLandmark>();
            landmark.Configure(70f, false, null, null, new Vector2(8f, 20f), false);
            Assert.IsTrue(landmark.CanBeMarked);
            Assert.IsFalse(landmark.IsFoundFrom(new Vector3(100f, 0f, 0f)), "not yet seen");
            Assert.IsTrue(landmark.IsFoundFrom(new Vector3(60f, 30f, 0f)), "within 70 m, whatever the height");
            Assert.IsTrue(landmark.IsFoundFrom(new Vector3(300f, 0f, 0f)), "and stays found");

            landmark.Configure(0f, false, null, null, Vector2.one, false);
            Assert.IsFalse(landmark.CanBeMarked);
            landmark.Configure(0f, true, null, null, Vector2.one, false);
            Assert.IsTrue(landmark.IsFoundFrom(new Vector3(900f, 0f, 0f)), "known from the start");
        }
        finally
        {
            Object.DestroyImmediate(thing);
        }
    }

    // ----- G5: roads -----

    [Test]
    public void Roads_WidenOnlyTheTwoRoutes_AndTheirZoneFollowsThem()
    {
        RaidLayoutRules plain = Load(WoodsRulesPath);
        RaidLayoutRules rules = Object.Instantiate(plain);
        rules.ConfigureRoads(1.6f, new GameObject[0], new Vector2(100f, 140f), 1.5f);
        try
        {
            int wider = 0, sampled = 0;
            foreach (int seed in new[] { 1, 2, 3, 4 })
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                RaidLayoutField road = RaidLayoutField.Build(plan, rules);
                RaidLayoutField narrow = RaidLayoutField.Build(RaidLayoutPlanner.Plan(plain, Size, seed), plain);
                byte[] zone = road.RoadZone();
                Assert.IsNotNull(zone, $"seed {seed}");
                float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
                var roadLines = plan.ShortRoute.Concat(plan.LongRoute).Select(link => plan.Trails[link]).ToList();
                for (int link = 0; link < plan.Trails.Count; link++)
                {
                    List<Vector2> trail = plan.Trails[link];
                    Vector2 middle = trail[trail.Count / 2];
                    Vector2 across = Vector2.Perpendicular((trail[trail.Count / 2 + 1] - middle).normalized);
                    Vector2 edge = middle + across * (halfWidth * 1.3f);
                    float u = edge.x / Size, v = edge.y / Size;
                    if (plan.IsRoad(link))
                    {
                        sampled++;
                        wider += road.WalkedDistance(u, v) < narrow.WalkedDistance(u, v) - 0.5f ? 1 : 0;
                        Assert.Greater(ZoneAt(zone, road.Resolution, middle), 200, $"seed {seed}: the road's middle is road");
                    }
                    else if (DistanceToLines(roadLines, middle) > 15f)
                    {
                        Assert.Less(ZoneAt(zone, road.Resolution, middle), 10, $"seed {seed}: a side trail is no road");
                        Assert.AreEqual(narrow.WalkedDistance(u, v), road.WalkedDistance(u, v), 0.3f, $"seed {seed}: a side trail keeps its width");
                    }
                }
            }

            Assert.Greater(sampled, 4);
            Assert.GreaterOrEqual(wider, Mathf.CeilToInt(sampled * 0.7f), "the roads are wider");
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void RoadLayer_PaintsTheRoadZone_AndWithoutAZonePaintsAsBefore()
    {
        RaidMapDefinition woods = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(WoodsMapPath);
        RaidMapDefinition plainMap = Object.Instantiate(woods);
        RaidMapDefinition roadMap = Object.Instantiate(woods);
        var layer = new TerrainLayer();
        var plainData = new TerrainData { heightmapResolution = 33, alphamapResolution = 32, size = new Vector3(woods.Size, 30f, woods.Size) };
        var roadData = new TerrainData { heightmapResolution = 33, alphamapResolution = 32, size = new Vector3(woods.Size, 30f, woods.Size) };
        try
        {
            roadMap.ConfigureRoad(layer, 1f);
            Assert.AreEqual(plainMap.Layers.Length + 1, roadMap.Layers.Length, "a layer of its own goes last");
            int roadIndex = roadMap.RoadLayerIndex;
            Assert.AreEqual(roadMap.Layers.Length - 1, roadIndex);
            plainData.terrainLayers = plainMap.Layers;
            roadData.terrainLayers = roadMap.Layers;

            byte[] all = Enumerable.Repeat((byte)255, Zones * Zones).ToArray();
            byte[] none = new byte[Zones * Zones];
            byte[] west = new byte[Zones * Zones];
            for (int i = 0; i < west.Length; i++)
            {
                west[i] = (byte)(i % Zones < Zones / 2 ? 255 : 0);
            }

            // No road zone: paint as before, the road layer empty.
            plainMap.UseGenerated(Zones, all, none, none, new RaidMapMood(), 3);
            roadMap.UseGenerated(Zones, all, none, none, new RaidMapMood(), 3);
            RaidMapSurface.Paint(plainData, plainMap);
            RaidMapSurface.Paint(roadData, roadMap);
            float[,,] before = plainData.GetAlphamaps(0, 0, 32, 32), after = roadData.GetAlphamaps(0, 0, 32, 32);
            for (int z = 0; z < 32; z++)
            {
                for (int x = 0; x < 32; x++)
                {
                    for (int l = 0; l < before.GetLength(2); l++)
                    {
                        Assert.AreEqual(before[z, x, l], after[z, x, l], 1e-5f, $"cell {x},{z} layer {l}");
                    }

                    Assert.AreEqual(0f, after[z, x, roadIndex], 1e-5f);
                }
            }

            // A road down the west half: its soil is the road layer, the east keeps the path layer.
            roadMap.UseGenerated(Zones, all, none, none, new RaidMapMood(), 3, 0, west);
            RaidMapSurface.Paint(roadData, roadMap);
            after = roadData.GetAlphamaps(0, 0, 32, 32);
            Assert.Greater(after[16, 2, roadIndex], 0.45f, "the road is the road layer");
            Assert.Less(after[16, 29, roadIndex], 0.01f, "off the road, none of it");
            Assert.Greater(after[16, 29, 1], 0.3f, "the trail elsewhere keeps its path layer");

            // A layer the map already has costs no layer more.
            roadMap.ConfigureRoad(woods.LitterLayer != null ? woods.LitterLayer : woods.PathLayer, 1f);
            Assert.AreEqual(plainMap.Layers.Length, roadMap.Layers.Length);
        }
        finally
        {
            Object.DestroyImmediate(plainData);
            Object.DestroyImmediate(roadData);
            Object.DestroyImmediate(plainMap);
            Object.DestroyImmediate(roadMap);
            Object.DestroyImmediate(layer);
        }
    }

    // ----- G8: the Valley's crossings -----

    [Test]
    public void MaxCrossings_CapsTheRiver_KeepingEveryClearingJoined_AndTheRoutesToTheExit()
    {
        RaidLayoutRules plain = Load(ValleyRulesPath);
        RaidLayoutRules rules = Object.Instantiate(plain);
        rules.Valley.maxCrossings = 2;
        try
        {
            int fewer = 0, twoWays = 0;
            foreach (int seed in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
                RaidLayoutPlan before = RaidLayoutPlanner.Plan(plain, Size, seed);
                Assert.LessOrEqual(plan.Crossings.Count, 2, $"seed {seed}: the river is crossed at two places at most");
                fewer += plan.Crossings.Count < before.Crossings.Count ? 1 : 0;
                Assert.AreEqual(plan.Links.Count, plan.Trails.Count, $"seed {seed}: trails and links in step");
                Assert.IsTrue(AllJoined(plan), $"seed {seed}: every clearing still joins the start");
                for (int link = 0; link < plan.Trails.Count; link++)
                {
                    List<Vector2> trail = plan.Trails[link];
                    bool crosses = plan.Valley.BankOf(trail[0]) != plan.Valley.BankOf(trail[trail.Count - 1]);
                    Assert.AreEqual(crosses ? 1 : 0, plan.Crossings.Count(crossing => crossing.Trail == link), $"seed {seed}: trail {link}'s crossing is recorded");
                }

                Assert.IsNotEmpty(plan.ShortRoute, $"seed {seed}: a way to the exit ahead");
                List<Vector2> line = RaidLayoutPlanner.RouteLine(plan, plan.ShortRoute);
                Assert.AreEqual(plan.Rooms[plan.MainExit].Position, line[line.Count - 1], $"seed {seed}: the short way reaches the exit");
                twoWays += plan.LongRoute.Count > 0 ? 1 : 0;
                if (plan.Crossings.Count > 0)
                {
                    Assert.IsTrue(plan.Crossings.Any(crossing => crossing.Bridge), $"seed {seed}: a crossing still carries a bridge");
                }

                Assert.AreEqual(RaidLayoutGoldenTests.Fingerprint(rules, Size, seed), RaidLayoutGoldenTests.Fingerprint(rules, Size, seed), $"seed {seed}");
            }

            Assert.Greater(fewer, 0, "some valley had more crossings than two");
            Assert.Greater(twoWays, 0, "the long way is found again where the trails give one");
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    // ----- G9: heroes and mids -----

    private const float TrailY = 100f;
    private const float TrailHalf = 2.6f;

    // Metres from a spot to a straight trail across the map's middle.
    private static float Walked(Vector2 spot) => Mathf.Abs(spot.y - TrailY) - TrailHalf;

    private static (TreeInstance[] trees, List<RaidMapDefinition.HeroObject> objects, RaidMapSurface.Group[] groups) Dress(bool dressed, bool walked)
    {
        RaidMapDefinition map = Object.Instantiate(AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(WoodsMapPath));
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(map.Size, 30f, map.Size) };
        try
        {
            if (dressed)
            {
                RaidMapDefinition.Scatter rock = map.Rocks[0];
                map.ConfigureDressing(new[] { new RaidMapDefinition.Hero { prefab = rock.prefab, weight = 1f, scale = new Vector2(1f, 1.2f), room = 3f } },
                    new[] { map.Undergrowth[0] }, 30f, 30f, new Vector2(6f, 15f), 10f, new Vector2Int(2, 4), new Vector2(1.5f, 12f));
            }

            // No painted trail: the dressing goes by the walked distance alone (a straight trail across the middle).
            byte[] path = new byte[Zones * Zones];
            byte[] forest = Enumerable.Repeat((byte)90, Zones * Zones).ToArray();
            map.UseGenerated(Zones, path, forest, new byte[Zones * Zones], new RaidMapMood(), 21);
            var objects = new List<RaidMapDefinition.HeroObject>();
            RaidMapSurface.Scatter(data, map, new List<RaidMapSurface.Clearing>(), out _, walked: walked ? Walked : (System.Func<Vector2, float>)null,
                heroObjects: objects);
            return (data.treeInstances, objects, RaidMapSurface.PrototypeGroups(map));
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(map);
        }
    }

    [Test]
    public void Dressing_WithEmptyLists_LeavesTheForestTreeForTree()
    {
        TreeInstance[] plain = Dress(false, false).trees;
        TreeInstance[] asked = Dress(false, true).trees;
        Assert.Greater(plain.Length, 100);
        Assert.AreEqual(plain.Length, asked.Length);
        for (int i = 0; i < plain.Length; i++)
        {
            Assert.AreEqual(plain[i].position, asked[i].position, $"instance {i}");
            Assert.AreEqual(plain[i].prototypeIndex, asked[i].prototypeIndex, $"instance {i}");
        }
    }

    [Test]
    public void Dressing_PutsAHeroOffTheTrail_ObjectsNearIt_AndMidsBeside()
    {
        (TreeInstance[] trees, List<RaidMapDefinition.HeroObject> objects, RaidMapSurface.Group[] groups) = Dress(true, true);
        var heroTrees = trees.Where(tree => groups[tree.prototypeIndex] == RaidMapSurface.Group.Hero).ToList();
        var mids = trees.Where(tree => groups[tree.prototypeIndex] == RaidMapSurface.Group.Mid).ToList();
        Vector2 At(TreeInstance tree) => new Vector2(tree.position.x * Size, tree.position.z * Size);

        Assert.Greater(objects.Count + heroTrees.Count, 4, "heroes along the trail");
        Assert.Greater(mids.Count, 8, "mids along the trail");
        foreach (RaidMapDefinition.HeroObject hero in objects)
        {
            Assert.That(Walked(hero.Position), Is.InRange(6f, 10f), "a hero near the trail stands as an object");
        }

        foreach (TreeInstance tree in heroTrees)
        {
            Assert.That(Walked(At(tree)), Is.InRange(10f, 15f), "one farther off is a terrain tree");
        }

        foreach (TreeInstance tree in mids)
        {
            Assert.That(Walked(At(tree)), Is.InRange(1.5f, 12f));
        }

        // At most one hero to a 30 m cell.
        var cells = objects.Select(hero => hero.Position).Concat(heroTrees.Select(At))
            .Select(spot => new Vector2Int((int)(spot.x / 30f), (int)(spot.y / 30f))).ToList();
        Assert.AreEqual(cells.Count, cells.Distinct().Count());

        (TreeInstance[] again, List<RaidMapDefinition.HeroObject> againObjects, _) = Dress(true, true);
        Assert.AreEqual(trees.Length, again.Length, "the same seed, the same dressing");
        CollectionAssert.AreEqual(objects.Select(hero => hero.Position), againObjects.Select(hero => hero.Position));
    }

    // ----- Helpers -----

    private static bool AllJoined(RaidLayoutPlan plan, int without = -1)
    {
        var reached = new HashSet<int> { 0 };
        var open = new Queue<int>();
        open.Enqueue(0);
        while (open.Count > 0)
        {
            int room = open.Dequeue();
            for (int link = 0; link < plan.Links.Count; link++)
            {
                if (link == without)
                {
                    continue;
                }

                (int a, int b) = plan.Links[link];
                int other = a == room ? b : b == room ? a : -1;
                if (other >= 0 && reached.Add(other))
                {
                    open.Enqueue(other);
                }
            }
        }

        return reached.Count == plan.Rooms.Count;
    }

    private static float DistanceToLines(IEnumerable<List<Vector2>> lines, Vector2 point)
    {
        float best = float.MaxValue;
        foreach (List<Vector2> line in lines)
        {
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 along = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(line[i] + along * t, point));
            }
        }

        return best;
    }

    private static int ZoneAt(byte[] zone, int resolution, Vector2 point)
    {
        int x = Mathf.Clamp(Mathf.RoundToInt(point.x / Size * (resolution - 1)), 0, resolution - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(point.y / Size * (resolution - 1)), 0, resolution - 1);
        return zone[y * resolution + x];
    }
}
