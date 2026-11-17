using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The generator's world-look steps (Docs/WorldLookPlan_2026-11-17.md, section b): copses, rocks in the open, piles, stones by the water,
/// plants by water height and rocks, boulder outcrops and the climate's wetness floor. Above all, that each new setting at its default
/// leaves a map as it was; the Misty Woods' plans are pinned by RaidLayoutGoldenTests.
/// </summary>
public sealed class WorldLookGeneratorTests
{
    private const string WoodsMapPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string WoodsRulesPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string WoodsTerrainPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset";
    private const int Zones = 16;

    private static RaidMapDefinition WoodsMap => AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(WoodsMapPath);

    [Test]
    public void Copse_StaysWithinZeroAndOne_AndMakesBothMeadowAndCopses()
    {
        var offset = new Vector2(123.4f, 567.8f);
        float least = 1f, most = 0f;
        for (int z = 0; z < 120; z++)
        {
            for (int x = 0; x < 120; x++)
            {
                float copse = RaidMapSurface.Copse(new Vector2(x * 3.3f, z * 3.3f), 14f, offset);
                Assert.That(copse, Is.InRange(0f, 1f));
                least = Mathf.Min(least, copse);
                most = Mathf.Max(most, copse);
            }
        }

        Assert.Less(least, 0.05f, "open meadow between the copses");
        Assert.Greater(most, 0.95f, "and copses' hearts");
    }

    [Test]
    public void CopseFactor_IsExactlyOneWithoutClumping_AndRunsFromLoneTreesToCopseHearts()
    {
        for (float copse = 0f; copse <= 1f; copse += 0.125f)
        {
            Assert.AreEqual(1f, RaidMapSurface.CopseFactor(copse, 0f), "no clumping: every tree as before");
        }

        Assert.AreEqual(0.12f, RaidMapSurface.CopseFactor(0f, 1f), 1e-5f, "a lone tree now and then in the meadow");
        Assert.AreEqual(1.72f, RaidMapSurface.CopseFactor(1f, 1f), 1e-5f, "thickest in a copse's heart");
        Assert.Less(RaidMapSurface.CopseFactor(0.2f, 1f), RaidMapSurface.CopseFactor(0.8f, 1f));
    }

    [Test]
    public void NewScatterSettings_AtTheirDefaults_LeaveTheForestTreeForTree()
    {
        RaidMapDefinition plain = Object.Instantiate(WoodsMap);
        RaidMapDefinition extended = Object.Instantiate(WoodsMap);
        var plainData = new TerrainData { heightmapResolution = 33, size = new Vector3(plain.Size, 30f, plain.Size) };
        var extendedData = new TerrainData { heightmapResolution = 33, size = new Vector3(plain.Size, 30f, plain.Size) };
        try
        {
            // Pile and shore lists filled but switched off, and a copse size with no clumping: none of it may move a thing.
            var so = new SerializedObject(extended);
            AddStone(so.FindProperty("pileStones"), extended.Rocks[0].prefab);
            AddStone(so.FindProperty("shore.stones"), extended.Rocks[0].prefab);
            so.FindProperty("clumpSize").floatValue = 7f;
            so.ApplyModifiedPropertiesWithoutUndo();

            byte[] none = new byte[Zones * Zones];
            byte[] forest = Enumerable.Repeat((byte)150, Zones * Zones).ToArray();
            plain.UseGenerated(Zones, none, forest, none, new RaidMapMood(), 11);
            extended.UseGenerated(Zones, none, forest, none, new RaidMapMood(), 11);
            int plainTrees = RaidMapSurface.Scatter(plainData, plain, new List<RaidMapSurface.Clearing>(), out _);
            int extendedTrees = RaidMapSurface.Scatter(extendedData, extended, new List<RaidMapSurface.Clearing>(), out _);

            Assert.Greater(plainTrees, 100);
            Assert.AreEqual(plainTrees, extendedTrees);
            TreeInstance[] before = plainData.treeInstances, after = extendedData.treeInstances;
            Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.AreEqual(before[i].prototypeIndex, after[i].prototypeIndex, $"instance {i}");
                Assert.AreEqual(before[i].position, after[i].position, $"instance {i}");
                Assert.AreEqual(before[i].widthScale, after[i].widthScale, $"instance {i}");
            }

            Assert.AreEqual(plainData.treePrototypes.Length + 2, extendedData.treePrototypes.Length, "the stones' prototypes come after the rest");
        }
        finally
        {
            Object.DestroyImmediate(plainData);
            Object.DestroyImmediate(extendedData);
            Object.DestroyImmediate(plain);
            Object.DestroyImmediate(extended);
        }
    }

    [Test]
    public void RocksInOpen_AtZero_LeavesTheOpenBare_AndAboveZero_LaysRocksThere()
    {
        Assert.AreEqual(0, RocksOnOpenGround(0f), "rocks only at cliffs and in the forest, as before");
        Assert.Greater(RocksOnOpenGround(0.5f), 10, "rocks out in the open");
    }

    [Test]
    public void PileStones_GetNoTrunkPrism_EverythingElseKeepsItsOwn()
    {
        RaidMapDefinition map = Object.Instantiate(WoodsMap);
        try
        {
            bool[] before = RaidMapSurface.Prisms(map);
            Assert.IsTrue(before.All(prism => prism), "without piles every capsule cuts the navmesh, as before");

            var so = new SerializedObject(map);
            AddStone(so.FindProperty("pileStones"), map.Rocks[0].prefab);
            AddStone(so.FindProperty("shore.stones"), map.Rocks[0].prefab);
            so.ApplyModifiedPropertiesWithoutUndo();
            RaidMapSurface.Group[] groups = RaidMapSurface.PrototypeGroups(map);
            bool[] prisms = RaidMapSurface.Prisms(map);
            Assert.AreEqual(before.Length + 2, prisms.Length);
            Assert.AreEqual(RaidMapSurface.Group.PileStone, groups[before.Length]);
            Assert.IsFalse(prisms[before.Length], "a pile stone leaves the navmesh as it was");
            Assert.AreEqual(RaidMapSurface.Group.ShoreStone, groups[before.Length + 1]);
            Assert.IsTrue(prisms[before.Length + 1], "a stone by the water keeps its capsule, so creatures path round it");
        }
        finally
        {
            Object.DestroyImmediate(map);
        }
    }

    [Test]
    public void WaterBand_GrowsInsideItsBand_ThinsAtItsEnds_AndNeedsWater()
    {
        var reeds = new Vector2(-0.5f, 0.35f);
        Assert.AreEqual(1f, TerrainGrowth.InBand(-0.1f, reeds), 1e-5f, "standing in the shallows");
        Assert.AreEqual(0f, TerrainGrowth.InBand(-0.6f, reeds), "too deep");
        Assert.AreEqual(0f, TerrainGrowth.InBand(0.4f, reeds), "too high and dry");
        Assert.AreEqual(0f, TerrainGrowth.InBand(float.NaN, reeds), "no water near");
        float margin = TerrainGrowth.InBand(0.33f, reeds);
        Assert.Greater(margin, 0f);
        Assert.Less(margin, 1f, "a ragged margin, not a contour line");
    }

    [Test]
    public void NearRocks_IsFullAtARock_AndGoneBeyondReach()
    {
        var origin = new Vector3(-10f, 0f, -10f);
        var rocks = new List<Vector3> { new Vector3(5f, 5f, 1f) };
        System.Func<Vector3, float> near = TerrainGrowth.NearRocks(origin, new Vector2(40f, 40f), rocks);
        Assert.AreEqual(1f, near(new Vector3(5.6f, 0f, 5f)), 0.02f, "at the rock");
        float halfWay = near(new Vector3(5f + 1f + TerrainGrowth.RockReach * 0.5f, 0f, 5f));
        Assert.That(halfWay, Is.InRange(0.2f, 0.8f), "fading off");
        Assert.AreEqual(0f, near(new Vector3(5f + 1f + TerrainGrowth.RockReach + 0.6f, 0f, 5f)), "beyond its reach");
        Assert.AreEqual(0f, near(new Vector3(100f, 0f, 100f)), "off the ground");
        Assert.IsNull(TerrainGrowth.NearRocks(origin, new Vector2(40f, 40f), new List<Vector3>()), "no rocks, nothing to ask");
    }

    [Test]
    public void Plants_WithoutTheNewSettings_GrowAsBefore_AndByWaterOnesOnlyInTheirBand()
    {
        TerrainData data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainData>(WoodsTerrainPath));
        var holder = new GameObject("Terrain", typeof(Terrain));
        try
        {
            var terrain = holder.GetComponent<Terrain>();
            terrain.terrainData = data;
            var weights = new float[data.alphamapHeight, data.alphamapWidth, data.terrainLayers.Length];
            for (int z = 0; z < data.alphamapHeight; z++)
            {
                for (int x = 0; x < data.alphamapWidth; x++)
                {
                    weights[z, x, 0] = 1f;
                }
            }

            data.SetAlphamaps(0, 0, weights);
            string name = WoodsMap.Plants.First(plant => plant.enabled && data.detailPrototypes.Any(p => p.prototype != null && p.prototype.name == plant.prototype)).prototype;
            int layer = System.Array.FindIndex(data.detailPrototypes, p => p.prototype != null && p.prototype.name == name);
            int grass = System.Array.FindIndex(data.detailPrototypes, p => p.prototype != null && p.prototype.name == "Short Grass");
            float middle = holder.transform.position.x + data.size.x * 0.5f;
            int half = data.detailWidth / 2;

            var plain = new[] { new TerrainGrowth.Plant(name, 6f, 0.45f, 1f, 0) };
            TerrainGrowth.Fill(terrain, WoodsMap.Growth, 3, null, plain);
            int[,] asBefore = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            int grassBefore = Sum(data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, grass), 0, data.detailWidth);
            Assert.Greater(Sum(asBefore, 0, data.detailWidth), 0);

            // Surroundings that say "no water near, not in water, no rocks": a plant that asks none of it grows exactly as before.
            var nothingNear = new TerrainGrowth.Surroundings { AboveWater = point => float.NaN, InWater = point => false };
            TerrainGrowth.Fill(terrain, WoodsMap.Growth, 3, null, plain, null, null, nothingNear);
            CollectionAssert.AreEqual(asBefore, data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer));

            // By water height: only on the side whose ground stands in the band.
            var bank = new[] { new TerrainGrowth.Plant(name, 6f, 0.45f, 1f, 0) { byWater = true, aboveWater = new Vector2(0f, 0.5f) } };
            var oneBank = new TerrainGrowth.Surroundings { AboveWater = point => point.x < middle ? 0.25f : float.NaN };
            TerrainGrowth.Fill(terrain, WoodsMap.Growth, 3, null, bank, null, null, oneBank);
            int[,] cells = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            Assert.Greater(Sum(cells, 0, half - 1), 0, "on the bank");
            Assert.AreEqual(0, Sum(cells, half + 1, data.detailWidth), "away from water");

            // In water: what stands in the shallows grows; grass doesn't.
            var shallows = new[] { new TerrainGrowth.Plant(name, 6f, 0.45f, 1f, 0) { byWater = true, aboveWater = new Vector2(-0.5f, 0.3f) } };
            var pond = new TerrainGrowth.Surroundings
            {
                AboveWater = point => point.x < middle ? -0.2f : 5f,
                InWater = point => point.x < middle,
            };
            TerrainGrowth.Fill(terrain, WoodsMap.Growth, 3, null, shallows, null, null, pond);
            Assert.Greater(Sum(data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer), 0, half - 1), 0, "reeds in the shallows");
            int[,] wetGrass = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, grass);
            Assert.AreEqual(0, Sum(wetGrass, 0, half - 1), "no grass under water");
            Assert.Greater(grassBefore, 0);
        }
        finally
        {
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void WaterNear_FindsAPondsSurface_AndTheFenEverywhere()
    {
        var woods = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(WoodsRulesPath);
        RaidLayoutRules rules = Object.Instantiate(woods);
        try
        {
            int ponds = 0;
            for (int seed = 1; seed <= 6; seed++)
            {
                RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, 200f, seed);
                RaidLayoutField field = RaidLayoutField.Build(plan, rules);
                foreach (RaidLayoutPond pond in plan.Ponds)
                {
                    if (plan.InRiver(pond.Position, pond.Reach + 2f))
                    {
                        continue;
                    }

                    ponds++;
                    Assert.IsTrue(field.WaterNear(pond.Position, 0f, out float level, out float edge), $"seed {seed}: in its pond");
                    Assert.Less(edge, 0f);
                    Assert.AreEqual(pond.Level, level, 1e-4f);
                }

                if (plan.Ponds.Count > 0 && plan.Rivers.Count == 0)
                {
                    RaidLayoutPond first = plan.Ponds[0];
                    Vector2 far = first.Position + Vector2.right * (first.Reach + 40f);
                    if (plan.Ponds.TrueForAll(pond => pond.Edge(far) > 5f))
                    {
                        Assert.IsFalse(field.WaterNear(far, 5f, out _, out _), $"seed {seed}: no water within 5 m");
                    }
                }
            }

            Assert.Greater(ponds, 0, "the Woods have ponds to look at");

            var so = new SerializedObject(rules);
            so.FindProperty("fen.enabled").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            RaidLayoutPlan fenPlan = RaidLayoutPlanner.Plan(rules, 200f, 2);
            RaidLayoutField fen = RaidLayoutField.Build(fenPlan, rules);
            Assert.IsTrue(fen.WaterNear(new Vector2(100f, 100f), 0f, out float fenLevel, out float fenEdge) || fenPlan.Ponds.Count > 0);
            if (float.IsNaN(fenEdge))
            {
                Assert.AreEqual(fen.FenWater, fenLevel, 1e-4f, "the fen's one sheet, wherever no pond or river is nearer");
            }
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void BoulderOutcrops_Off_AsBefore_On_SmallRocksHugTheFirst()
    {
        var woods = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(WoodsRulesPath);
        RaidLayoutRules rules = Object.Instantiate(woods);
        try
        {
            var so = new SerializedObject(rules);
            so.FindProperty("boulderClusterSize").vector2IntValue = new Vector2Int(3, 4);
            so.ApplyModifiedPropertiesWithoutUndo();
            for (int seed = 1; seed <= 4; seed++)
            {
                Assert.IsFalse(RaidLayoutPlanner.Plan(rules, 200f, seed).Boulders.Any(boulder => boulder.Outcrop), "off: no outcrops");
            }

            so.FindProperty("boulderOutcrops").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            int hugging = 0;
            for (int seed = 1; seed <= 4; seed++)
            {
                RaidLayoutBoulder first = null;
                foreach (RaidLayoutBoulder boulder in RaidLayoutPlanner.Plan(rules, 200f, seed).Boulders)
                {
                    if (!boulder.Outcrop)
                    {
                        first = boulder;
                        continue;
                    }

                    hugging++;
                    Assert.IsNotNull(first);
                    Assert.That(boulder.Scale, Is.InRange(0.3f, 0.55f));
                    float distance = Vector2.Distance(first.Position, boulder.Position);
                    Assert.That(distance, Is.InRange(0.3f, RaidMapGenerator.BoulderWidest * 0.5f + 1.5f + 0.01f), $"seed {seed}: hugs its first rock");
                }
            }

            Assert.Greater(hugging, 0);
        }
        finally
        {
            Object.DestroyImmediate(rules);
        }
    }

    [Test]
    public void GroundWetnessFloor_IsTheLeastTheGroundShows()
    {
        Assert.AreEqual(0.35f, WeatherSystem.LooksWet(0.1f, 0.35f), 1e-6f, "a marsh's paths shine without rain");
        Assert.AreEqual(0.8f, WeatherSystem.LooksWet(0.8f, 0.35f), 1e-6f, "rain soaks it further");
        foreach (float wetness in new[] { 0f, 0.3f, 1f })
        {
            Assert.AreEqual(wetness, WeatherSystem.LooksWet(wetness, 0f), "no floor: the rain's wetness, as before");
        }
    }

    private static int RocksOnOpenGround(float rocksInOpen)
    {
        RaidMapDefinition map = Object.Instantiate(WoodsMap);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(map.Size, 30f, map.Size) };
        try
        {
            var so = new SerializedObject(map);
            so.FindProperty("rocksInOpen").floatValue = rocksInOpen;
            so.ApplyModifiedPropertiesWithoutUndo();
            byte[] none = new byte[Zones * Zones];
            map.UseGenerated(Zones, none, none, none, new RaidMapMood(), 5);
            RaidMapSurface.Scatter(data, map, new List<RaidMapSurface.Clearing>(), out _);
            RaidMapSurface.Group[] groups = RaidMapSurface.PrototypeGroups(map);
            return data.treeInstances.Count(tree => groups[tree.prototypeIndex] == RaidMapSurface.Group.Rock);
        }
        finally
        {
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(map);
        }
    }

    private static void AddStone(SerializedProperty list, GameObject prefab)
    {
        list.arraySize++;
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("weight").floatValue = 1f;
        entry.FindPropertyRelative("scale").vector2Value = new Vector2(0.3f, 0.6f);
    }

    private static int Sum(int[,] cells, int fromColumn, int toColumn)
    {
        int sum = 0;
        for (int z = 0; z < cells.GetLength(0); z++)
        {
            for (int x = Mathf.Max(0, fromColumn); x < Mathf.Min(cells.GetLength(1), toColumn); x++)
            {
                sum += cells[z, x];
            }
        }

        return sum;
    }
}
