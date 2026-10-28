using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TreeChoppingTests
{
    private static TreeChoppingSettings Rules => AssetDatabase.LoadAssetAtPath<TreeChoppingSettings>("Assets/_Project/Resources/TreeChoppingSettings.asset");

    [Test]
    public void ABetterAxeAndABiggerTreeGiveMoreLogs()
    {
        TreeChoppingSettings rules = Rules;
        Assert.Less(rules.Logs(1, 1f), rules.Logs(3, 1f));
        Assert.Less(rules.Logs(1, 1f), rules.Logs(1, 2f));
        Assert.GreaterOrEqual(rules.Logs(1, 0.1f), 1, "even a sapling gives a log");
    }

    [Test]
    public void BetterWoodComesWithABetterAxeAndABiggerTree()
    {
        TreeChoppingSettings rules = Rules;
        Assert.Greater(rules.ChanceOf(2, 1, 1f), 0f, "a stone axe can find hardwood");
        Assert.Greater(rules.ChanceOf(2, 3, 1f), rules.ChanceOf(2, 1, 1f));
        Assert.Greater(rules.ChanceOf(2, 1, 2f), rules.ChanceOf(2, 1, 1f));
        Assert.AreEqual(0f, rules.ChanceOf(3, 1, 2.5f), "elderwood needs at least a Tier II axe");
        Assert.LessOrEqual(rules.ChanceOf(3, 5, 2.5f), 0.75f + 0.0001f);
    }

    [Test]
    public void FellingMergesTheLogsByWood()
    {
        var logs = new List<ItemAmount>();
        int i = 0;
        // Rolls alternate: a hit for the better wood every other try.
        ChoppableTree.Roll(3, 1f, () => i++ % 2 == 0 ? 0f : 1f, logs);
        Assert.AreEqual(Rules.Logs(3, 1f), logs.Sum(l => l.Amount));
        Assert.AreEqual(logs.Count, logs.Select(l => l.Item).Distinct().Count());
    }

    [Test]
    public void EveryTreeCanBeFelledAndOakWoodIsNowWood()
    {
        PrefabCatalog.Refresh();
        foreach (string path in PrefabCatalog.All.Where(e => e.Category == "Trees").Select(e => e.Path))
        {
            var tree = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsTrue(tree.GetComponent<ChoppableTree>() != null || tree.GetComponent<ResourceNode>() != null, $"{path} can't be felled");
        }

        Assert.AreEqual("Wood", AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/OakWood.asset").DisplayName);
        GameObject proxy = Rules.TerrainTreeProxy;
        Assert.IsNotNull(proxy, "terrain trees get a stand-in");
        Assert.AreEqual(ToolType.Axe, proxy.GetComponent<ResourceNode>().Definition.GatherTool);
    }
}
