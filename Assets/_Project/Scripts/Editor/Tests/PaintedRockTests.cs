using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Painted rocks (TerrainNodeHarvesting) taken away stay away: one broken with no stand-in (a mason's quarry ring) is hidden and
/// counted gone, kept by the woods' memory (TerrainTreeMemory) and hidden again on a fresh terrain from the save; the rest stand. And
/// a quarry ring counts the painted rocks a mason may break.
/// </summary>
public sealed class PaintedRockTests
{
    private const string RockPrefab = "Assets/_Project/Art/Vegetation/Misty Woods/Rocks and Logs/Mossy Rock.prefab";
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            if (thing != null)
            {
                Object.DestroyImmediate(thing);
            }
        }

        made.Clear();
    }

    // A small terrain with three painted rocks of the prefab, 10 m apart, a memory beside it.
    private Terrain Painted(GameObject rock)
    {
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(64f, 10f, 64f) };
        made.Add(data);
        data.treePrototypes = new[] { new TreePrototype { prefab = rock } };
        var instances = new List<TreeInstance>();
        for (int i = 0; i < 3; i++)
        {
            instances.Add(new TreeInstance
            {
                position = new Vector3((20f + i * 10f) / 64f, 0f, 0.5f), prototypeIndex = 0, widthScale = 1f, heightScale = 1f,
                color = Color.white, lightmapColor = Color.white,
            });
        }

        data.SetTreeInstances(instances.ToArray(), false);
        GameObject go = Terrain.CreateTerrainGameObject(data);
        made.Add(go);
        go.AddComponent<TerrainTreeMemory>();
        return go.GetComponent<Terrain>();
    }

    [Test]
    public void APaintedRockBroken_StaysGone_AndIsKeptByTheSave()
    {
        var rock = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefab);
        Assume.That(rock != null && TerrainNodeHarvesting.IsGatherable(rock), "a painted rock that can be gathered");
        Terrain first = Painted(rock);
        TerrainNodeHarvesting harvesting = TerrainNodeHarvesting.On(first);
        Assert.IsTrue(harvesting.IsPresent(1));
        Assert.IsTrue(harvesting.BreakByHand(1), "broken where it stands, with no stand-in");
        Assert.IsFalse(harvesting.IsPresent(1), "gone");
        Assert.IsFalse(harvesting.BreakByHand(1), "and not twice");
        Assert.AreEqual(0f, first.terrainData.GetTreeInstance(1).heightScale, "hidden on the terrain");
        Assert.IsTrue(harvesting.IsPresent(0) && harvesting.IsPresent(2), "the others stand");
        Assert.AreEqual(1, harvesting.TakenCount);

        string saved = first.GetComponent<TerrainTreeMemory>().CaptureState();
        Assert.IsNotNull(saved, "the woods' memory keeps a rock gone");

        Terrain second = Painted(rock);
        second.GetComponent<TerrainTreeMemory>().RestoreState(saved);
        TerrainNodeHarvesting again = TerrainNodeHarvesting.On(second);
        Assert.IsFalse(again.IsPresent(1), "still gone after a load");
        Assert.AreEqual(0f, second.terrainData.GetTreeInstance(1).heightScale);
        Assert.IsTrue(again.IsPresent(0) && again.IsPresent(2));
        made.Add(first.terrainData);
        made.Add(second.terrainData);
    }

    [Test]
    public void AQuarryRing_CountsThePaintedRocksAMasonMayBreak()
    {
        var rock = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefab);
        Assume.That(rock != null);
        ResourceNode node = rock.GetComponent<ResourceNode>();
        Assume.That(node != null && node.Definition != null);
        Terrain terrain = Painted(rock);
        TerrainNodeHarvesting.On(terrain);
        // The rocks stand at x 20, 30 and 40 on a terrain at the origin, half way up z (32).
        var ring = new RingOrder(new Vector3(30f, 0f, 32f), 6f, RingKind.Quarry);
        bool mayBreak = RingWork.Quarries(node.Definition, WorkerSettings.Current);
        Assert.AreEqual(mayBreak ? 1 : 0, RingWork.Standing(ring), "the one in the ring, if a mason may break its kind");
        Assert.IsTrue(mayBreak, "Mossy Rock is a Boulder: masons quarry those");
        Assert.IsTrue(RingWork.NextTarget(ring, out RingTarget target) && target.Painted != null && target.Index == 1);
        Assert.IsTrue(target.Break(true));
        Assert.AreEqual(0, RingWork.Standing(ring), "broken, the ring has none left");
        Assert.Greater(RingWork.Stone(target.RockNode).Count, 0, "its stone is its prefab's yield");
        made.Add(terrain.terrainData);
    }
}
