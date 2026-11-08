using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Small trees are broken down by hand, felled trees stay down at home, and saplings planted there grow back.</summary>
public sealed class SaplingTests
{
    private static ItemDefinition Sapling => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/CedarSapling.asset");

    [Test]
    public void ASapling_GrowsFromAHandsHeight_ToFullSize_InAFewDays()
    {
        SaplingDefinition sapling = Sapling.Sapling;
        Assert.NotNull(sapling);
        Assert.That(sapling.GrowDays, Is.InRange(1f, 5f), "a few in-game days");
        Assert.Greater(sapling.GrowsInto.Count, 0);
        Assert.IsTrue(sapling.GrowsInto.All(prefab => prefab != null && prefab.GetComponent<ChoppableTree>() != null), "it grows into a tree to fell");

        float last = 0f;
        for (float day = 0f; day <= sapling.GrowDays + 1f; day += 0.25f)
        {
            float scale = SaplingDefinition.ScaleAt(day, sapling.GrowDays, sapling.StartScale, 1f);
            Assert.GreaterOrEqual(scale, last - 1e-5f, "it never shrinks");
            last = scale;
        }

        Assert.AreEqual(sapling.StartScale, SaplingDefinition.ScaleAt(0f, sapling.GrowDays, sapling.StartScale, 1f), 1e-4f);
        Assert.AreEqual(1f, SaplingDefinition.ScaleAt(sapling.GrowDays, sapling.GrowDays, sapling.StartScale, 1f), 1e-4f);
        Assert.IsTrue(Sapling.IsUsable, "right-click plants it");
    }

    [Test]
    public void SmallTrees_AreBrokenDownByHand_ForSticksAndASapling()
    {
        TreeChoppingSettings settings = TreeChoppingSettings.Current;
        Assert.That(settings.SmallTreeHeight, Is.InRange(3f, 8f));
        GameObject proxy = settings.SmallTreeProxy;
        Assert.NotNull(proxy);
        Assert.IsNull(proxy.GetComponent<ChoppableTree>(), "no axe and no fall");
        Assert.AreEqual("Break down", proxy.GetComponent<InteractionVerb>().Verb);
        var node = new SerializedObject(proxy.GetComponent<ResourceNode>()).FindProperty("node").objectReferenceValue as GatheringNodeDefinition;
        Assert.AreEqual(ToolType.None, node.GatherTool);
        Assert.AreEqual(0f, node.RespawnSeconds, "it doesn't grow back by itself");
        var drops = new SerializedObject(proxy.GetComponent<ResourceSource>());
        Assert.IsTrue(drops.FindProperty("grantOnInteractionComplete").boolValue);
        SerializedProperty entries = drops.FindProperty("drops").FindPropertyRelative("entries");
        var items = Enumerable.Range(0, entries.arraySize).Select(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue).ToList();
        Assert.IsTrue(items.Any(i => i != null && i.name == "Sticks"));
        Assert.Contains(Sapling, items);
    }

    [Test]
    public void FellingATree_GivesASapling()
    {
        var tree = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/Tree.asset");
        Assert.IsTrue(tree.Yields.Entries.Any(entry => entry.Item == Sapling));
    }

    [Test]
    public void TheHaven_KeepsFelledTreesDown_AndGrowsWhatIsPlanted()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/SafeHaven.unity");
        try
        {
            Terrain home = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single(t => t.GetComponent<TerrainTreeMemory>() != null);
            Assert.IsTrue(home.GetComponent<TerrainTreeMemory>().KeepsFelled, "only planted saplings grow back at home");
            Assert.NotNull(home.GetComponent<PlantedTrees>());
            Assert.NotNull(home.GetComponent<SaveableEntity>(), "both are saved");
            var prototypes = home.terrainData.treePrototypes.Select(p => p.prefab).ToList();
            Assert.IsTrue(Sapling.Sapling.GrowsInto.Any(prototypes.Contains), "the Haven paints the trees a sapling grows into");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
