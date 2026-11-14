using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class GroundPaintTests
{
    [TestCase("Forest Moss")]
    [TestCase("Dark Humus")]
    [TestCase("Forest Litter")]
    [TestCase("Muddy Bank")]
    public void ForestFloorLayers_HaveEveryMap(string name)
    {
        TerrainLayer layer = WorldAssets.Layer(name);
        Assert.NotNull(layer, name);
        Assert.NotNull(layer.diffuseTexture);
        Assert.NotNull(layer.normalMapTexture);
        Assert.NotNull(layer.maskMapTexture, "occlusion, height (for height blending) and smoothness");
        foreach (string map in new[] { "_Height.png", "_AO.jpg", "_Roughness.jpg" })
        {
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_Project/Art/Terrain/World/{name}/{name}{map}"), name + map);
        }
    }

    [TestCase("Haven Meadow")]
    [TestCase("Old-Growth Forest")]
    [TestCase("Dark Forest")]
    public void ReadyMadeLooks_Exist(string look)
    {
        TerrainPaintRules rules = WorldAssets.PaintRules(look);
        Assert.NotNull(rules, look);
        Assert.IsNotEmpty(rules.Rules);
        Assert.IsTrue(rules.Rules.All(r => r.layer != null), look);
    }

    // The Haven's four old-growth groves (Ground Paint Areas from the old BuildHavenWorld) went with the regenerated Haven:
    // HavenWorldGenerator paints its ground itself, the Haven Meadow and Dark Forest rules blended by distance.
}
