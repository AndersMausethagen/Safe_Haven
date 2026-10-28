using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.SceneManagement;

public sealed class WorldToolsTests
{
    [Test]
    public void EveryWorldToolsButtonRunsAMenuThatExists()
    {
        var menus = TypeCache.GetMethodsWithAttribute<MenuItem>()
                             .SelectMany(m => m.GetCustomAttributes(typeof(MenuItem), false).Cast<MenuItem>())
                             .Select(a => a.menuItem)
                             .ToHashSet();
        foreach (string path in WorldToolsWindow.MenuPaths)
        {
            Assert.IsTrue(menus.Contains(path), $"World Tools runs '{path}', which no menu item has (renamed?)");
        }
    }

    [TestCase("Assets/_Project/Prefabs/Environment/World/Rock Face A.prefab", "Rock Face A", "Rocks & Cliffs")]
    [TestCase("Assets/_Project/Prefabs/Environment/World/Norway Spruce B.prefab", "Norway Spruce B", "Trees")]
    [TestCase("Assets/_Project/Prefabs/Environment/World/Old Stump A.prefab", "Old Stump A", "Forest Floor")]
    [TestCase("Assets/_Project/Prefabs/Resources/GreyStoneNode.prefab", "GreyStoneNode", "Resources")]
    [TestCase("Assets/_Project/Prefabs/Raids/ExtractionPoint.prefab", "ExtractionPoint", "Raid")]
    [TestCase("Assets/_Project/Prefabs/Structures/Torch.prefab", "Torch", "Buildings")]
    public void PrefabsAreSortedIntoTheGroupsADesignerLooksFor(string path, string name, string expected)
    {
        Assert.AreEqual(expected, PrefabCatalog.CategoryOf(path, name));
    }

    [Test]
    public void TheCatalogOffersTheWorldBuildersRocksAndTrees()
    {
        PrefabCatalog.Refresh();
        Assert.IsTrue(PrefabCatalog.All.Any(e => e.Category == "Rocks & Cliffs" && e.Name.Contains("Mossy Boulder")));
        Assert.IsTrue(PrefabCatalog.All.Any(e => e.Category == "Trees" && e.Name.Contains("Norway Spruce")));
        Assert.IsFalse(PrefabCatalog.All.Any(e => e.Path.Contains("_Leaves_Wind")), "tree parts are not offered on their own");
    }

    [Test]
    public void GeneratedTreesCantBeWalkedThrough()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Models/Generated/Vegetation" }))
        {
            var tree = AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            bool whole = tree.GetComponentsInChildren<UnityEngine.MeshFilter>(true).Any(f => f.name == "Trunk" && f.transform != tree.transform);
            if (whole)
            {
                Assert.IsNotNull(tree.GetComponentInChildren<UnityEngine.Collider>(true), $"{tree.name} has no trunk collider");
            }
        }
    }

    [Test]
    public void AnEmptySceneIsReportedAsUnplayable()
    {
        // The test runner's own scene: untitled and empty of gameplay.
        Scene scene = SceneManager.GetActiveScene();
        Assume.That(string.IsNullOrEmpty(scene.path), "runs in the test runner's untitled scene");
        var raid = ScenePlayability.Check(scene, ScenePlayability.Kind.Raid);
        Assert.IsTrue(raid.Any(i => i.Severity == ScenePlayability.Severity.Error && i.Message.Contains("No player")));
        Assert.IsTrue(raid.Any(i => i.Message.Contains("extraction point")), "a raid needs a way home");
        Assert.IsTrue(raid.Any(i => i.Message.Contains("not saved")), "an unsaved scene can't be made playable");
        var home = ScenePlayability.Check(scene, ScenePlayability.Kind.SafeHaven);
        Assert.IsTrue(home.Any(i => i.Message.Contains("raid portal")));
        Assert.IsFalse(home.Any(i => i.Message.Contains("extraction point")), "a home has no exits to check");
        Assert.AreEqual(ScenePlayability.Severity.Error, raid[0].Severity, "errors come first");
    }
}
