using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Ponds on generated maps: a few, of different sizes, out of the way, and holding water. And what grows around them.</summary>
public sealed class RaidPondTests
{
    private const float Size = 200f;
    private const string TerrainPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset";
    private RaidLayoutRules rules;

    private static RaidLayoutRules MistyWoods => AssetDatabase.LoadAssetAtPath<RaidLayoutRules>("Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset");

    private static RaidMapDefinition Map => AssetDatabase.LoadAssetAtPath<RaidMapDefinition>("Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset");

    [SetUp]
    public void SetUp()
    {
        rules = Object.Instantiate(MistyWoods);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(rules);

    private static float DistanceToLine(List<Vector2> line, Vector2 point)
    {
        float nearest = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 along = line[i + 1] - line[i];
            float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
            nearest = Mathf.Min(nearest, Vector2.Distance(line[i] + along * t, point));
        }

        return nearest;
    }

    [Test]
    public void TheMistyWoods_HaveAtMostThreePonds_AndUsuallySome()
    {
        Assert.AreEqual(3, MistyWoods.Ponds.y, "no more than three for now");
        Assert.IsNotNull(MistyWoods.PondMaterial);
        int without = 0;
        var counts = new HashSet<int>();
        for (int seed = 1; seed <= 200; seed++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            Assert.LessOrEqual(plan.Ponds.Count, 3);
            counts.Add(plan.Ponds.Count);
            without += plan.Ponds.Count == 0 ? 1 : 0;
        }

        Assert.Less(without, 10, "nearly every map finds room for its ponds");
        Assert.Greater(counts.Count, 1, "how many is random");
    }

    [Test]
    public void PondSizes_Vary()
    {
        float smallest = float.MaxValue, largest = 0f;
        for (int seed = 1; seed <= 100; seed++)
        {
            foreach (RaidLayoutPond pond in RaidLayoutPlanner.Plan(rules, Size, seed).Ponds)
            {
                smallest = Mathf.Min(smallest, pond.Radii.x);
                largest = Mathf.Max(largest, pond.Radii.x);
                Assert.GreaterOrEqual(pond.Radii.x, rules.PondRadius.x - 0.01f);
                Assert.LessOrEqual(pond.Radii.x, rules.PondRadius.y + 0.01f);
            }
        }

        Assert.Greater(largest, smallest * 2f, "from small pools to proper ponds");
    }

    [Test]
    public void Ponds_KeepOffTrailsAndSites()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            foreach (RaidLayoutPond pond in plan.Ponds)
            {
                foreach (List<Vector2> trail in plan.Trails.Concat(plan.Spurs))
                {
                    Assert.Greater(DistanceToLine(trail, pond.Position), pond.Reach, $"seed {seed}: a trail through a pond");
                }

                foreach (RaidSite site in plan.Sites)
                {
                    Assert.Greater(pond.Edge(site.Position * Size), 0f, $"seed {seed}: {site.Kind} in a pond");
                }
            }
        }
    }

    [Test]
    public void AddingPonds_MovesNothingElse()
    {
        RaidLayoutPlan wet = RaidLayoutPlanner.Plan(rules, Size, 4242);
        rules.ConfigureWater(Vector2Int.zero, rules.PondRadius, rules.PondMaterial);
        RaidLayoutPlan dry = RaidLayoutPlanner.Plan(rules, Size, 4242);

        Assert.AreEqual(0, dry.Ponds.Count);
        CollectionAssert.AreEqual(dry.Sites.Select(site => site.Position), wet.Sites.Select(site => site.Position));
        CollectionAssert.AreEqual(dry.Rooms.Select(room => room.Position), wet.Rooms.Select(room => room.Position));
    }

    [Test]
    public void ThePondsHoldWater_BedBelowTheSurface_BanksAbove()
    {
        for (int seed = 1; seed <= 40; seed++)
        {
            RaidLayoutPlan plan = RaidLayoutPlanner.Plan(rules, Size, seed);
            RaidLayoutField field = RaidLayoutField.Build(plan, rules);
            foreach (RaidLayoutPond pond in plan.Ponds)
            {
                float bed = field.HeightAt(pond.Position.x / Size, pond.Position.y / Size);
                Assert.AreEqual(pond.Level - pond.Depth, bed, 0.2f, $"seed {seed}: the middle is as deep as planned");
                for (int i = 0; i < 16; i++)
                {
                    Vector2 bank = pond.Rim(i * Mathf.PI / 8f, 1.5f);
                    Assert.GreaterOrEqual(field.HeightAt(bank.x / Size, bank.y / Size), pond.Level - 0.01f, $"seed {seed}: water would spill over its bank");
                }
            }
        }
    }

    [Test]
    public void ThePonds_GiveADrink_AndFillWaterskins()
    {
        Assert.Greater(MistyWoods.PondDrink, 0f);
        Assert.IsNotNull(MistyWoods.PondFillItem);
        Assert.AreEqual("Waterskin", MistyWoods.PondFillItem.name);
        Assert.Greater(MistyWoods.PondFillUpTo, 0);
    }

    [Test]
    public void APondIsReached_AlongItsWholeShore_NotAtItsMiddle()
    {
        var pond = new RaidLayoutPond { Position = new Vector2(50f, 50f), Radii = new Vector2(11f, 7f), Turn = 0.6f, Depth = 1.5f, Level = 3f };
        var holder = new GameObject("Drinking Place");
        try
        {
            holder.AddComponent<RaidPondShore>().Configure(pond, new Vector3(-100f, 2f, -100f));
            var interactable = holder.AddComponent<Interactable>();
            interactable.Configure(InteractionAnimation.GatherLow, 1.5f, 1.5f, false);
            Vector3 World(Vector2 point) => new Vector3(point.x - 100f, 0f, point.y - 100f);

            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector2 near = pond.Rim(angle, 1f);
                Assert.AreEqual(0f, pond.Edge(pond.Shore(near)), 0.05f, "the nearest point lies on the shore");
                Assert.LessOrEqual(interactable.DistanceFrom(World(near)), interactable.Range, $"in reach 1 m out, at {angle:0.0}");
                Assert.IsTrue(interactable.MightBeWithin(World(near), interactable.Range), "and scans find it");
                Assert.Greater(interactable.DistanceFrom(World(pond.Rim(angle, 4f))), interactable.Range, "out of reach 4 m out");
            }

            Assert.AreEqual(0f, interactable.DistanceFrom(World(pond.Position)), 0.001f, "anywhere in the water is in reach");
        }
        finally
        {
            Object.DestroyImmediate(holder);
        }
    }

    [Test]
    public void TheForest_GrowsGlowingPlants_AndMushroomsLoveTheWet()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        foreach (TerrainGrowth.Plant plant in Map.Plants)
        {
            Assert.IsTrue(data.detailPrototypes.Any(prototype => prototype.prototype != null && prototype.prototype.name == plant.prototype), plant.prototype);
        }

        Assert.IsTrue(Map.Plants.Any(plant => plant.prototype == "Glow Flowers" && plant.enabled));
        TerrainGrowth.Plant mushrooms = Map.Plants.First(plant => plant.prototype == "Glow Mushrooms");
        Assert.IsTrue(mushrooms.enabled);
        Assert.Greater(mushrooms.loveOfWet, 0.5f);
    }

    [Test]
    public void Mushrooms_GrowThickerInWetGround()
    {
        TerrainData data = Object.Instantiate(AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath));
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
            float middle = holder.transform.position.x + data.size.x * 0.5f;
            TerrainGrowth.Fill(terrain, Map.Growth, 7, null, Map.Plants, point => point.x < middle ? 1f : 0f);
            int layer = System.Array.FindIndex(data.detailPrototypes, prototype => prototype.prototype.name == "Glow Mushrooms");
            int[,] cells = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            int wet = 0, dry = 0;
            for (int z = 0; z < data.detailHeight; z++)
            {
                for (int x = 0; x < data.detailWidth; x++)
                {
                    if (x < data.detailWidth / 2)
                    {
                        wet += cells[z, x];
                    }
                    else
                    {
                        dry += cells[z, x];
                    }
                }
            }

            Assert.Greater(dry, 0, "a few grow anywhere");
            Assert.Greater(wet, dry * 2, $"{wet} in the wet half against {dry} in the dry");
        }
        finally
        {
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void TheRaidsFog_IsItsOwn_AndLiesOnTheGround()
    {
        var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity", OpenSceneMode.Additive);
        try
        {
            Volume volume = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Volume>(true))
                .First(candidate => candidate.isGlobal && candidate.priority <= 0f);
            StringAssert.DoesNotContain("MistyWoods_Volume", AssetDatabase.GetAssetPath(volume.sharedProfile), "the prologue keeps its own look");
            Assert.IsTrue(volume.sharedProfile.TryGet(out AtmosphericFog fog));
            Assert.AreEqual(1f, fog.followTerrain.value, "a blanket over the ground, however high it lies");
            Assert.Greater(fog.heightFalloff.value, 0.4f, "and low on it");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
