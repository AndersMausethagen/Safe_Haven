using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LevelSpawnerTests
{
    private static readonly List<Vector3> LShape = new List<Vector3>
    {
        new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 4f),
        new Vector3(4f, 0f, 4f), new Vector3(4f, 0f, 10f), new Vector3(0f, 0f, 10f),
    };

    [Test]
    public void AZoneKnowsWhatIsInsideIt()
    {
        Assert.IsTrue(ZonePolygon.Contains(LShape, new Vector3(2f, 5f, 8f)), "in the upright of the L (height ignored)");
        Assert.IsTrue(ZonePolygon.Contains(LShape, new Vector3(8f, 0f, 2f)), "in the foot of the L");
        Assert.IsFalse(ZonePolygon.Contains(LShape, new Vector3(8f, 0f, 8f)), "in the notch");
        Assert.AreEqual(64f, ZonePolygon.Area(LShape), 0.001f);
    }

    [Test]
    public void RandomSpotsFallInsideTheZone()
    {
        var random = new System.Random(7);
        for (int i = 0; i < 200; i++)
        {
            Assert.IsTrue(ZonePolygon.TryRandomPoint(LShape, random, out Vector3 point));
            Assert.IsTrue(ZonePolygon.Contains(LShape, point));
        }

        Assert.IsFalse(ZonePolygon.TryRandomPoint(LShape.Take(2).ToList(), random, out _), "two corners are no area");
    }

    [Test]
    public void ARandomNodePicksByKindAndTier()
    {
        List<GatheringNodeDefinition> all = AssetDatabase.FindAssets("t:GatheringNodeDefinition")
            .Select(g => AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(g))).ToList();
        var picked = new List<GatheringNodeDefinition>();
        RandomResourceNode.Collect(all, ResourceNodeKind.Rock, new Vector2Int(1, 1), picked);
        Assert.IsNotEmpty(picked);
        Assert.IsTrue(picked.All(n => n.GatherTool == ToolType.Pickaxe && n.Tier == 1 && n.WorldPrefab != null));
        RandomResourceNode.Collect(all, ResourceNodeKind.Wood, new Vector2Int(1, 3), picked);
        Assert.IsTrue(picked.All(n => n.GatherTool == ToolType.Axe));
        Assert.IsFalse(picked.Any(n => n.name == "Tree"), "the shared Tree node has no prefab of its own to place");
        Assert.IsNotEmpty(Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).GatheringNodes, "the content database lists the nodes");
    }

    [Test]
    public void TheSpawnersArePlaceable()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<MobSpawnZone>("Assets/_Project/Prefabs/Raids/Mob Spawn Zone.prefab"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<RandomResourceNode>("Assets/_Project/Prefabs/Resources/Random Resource Node.prefab"));
        var stone = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/GreyStone.asset");
        Assert.GreaterOrEqual(stone.ModelVariants.Count(v => v != null), 2, "grey stones look different spot to spot");
    }
}
