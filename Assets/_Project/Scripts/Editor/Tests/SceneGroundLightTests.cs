using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The Haven's ground light field (SceneGroundLight, the overhaul): the scene carries it with no light thrown back (its hours turn), and
/// its shores come from its own water (ShoreGrid): ground under a pond's surface is water, a cell a few metres off has the pond's surface
/// and its distance, and beyond the reach none.
/// </summary>
public sealed class SceneGroundLightTests
{
    [Test]
    public void TheShoresComeFromTheScenesWater()
    {
        // A flat terrain 64 m across at 10 m, a pond (surface 10.5 m) over x 20-30, z 20-30, and a river along z = 50 at 10.4 m, 2 m wide.
        var heights = new float[65, 65];
        for (int z = 0; z < 65; z++) for (int x = 0; x < 65; x++) heights[z, x] = 0.1f;
        var still = new List<ShoreGrid.Still> { new ShoreGrid.Still(new Rect(20f, 20f, 10f, 10f), 10.5f) };
        var rivers = new List<Vector4[]> { new[] { new Vector4(0f, 10.4f, 50f, 2f), new Vector4(64f, 10.4f, 50f, 2f) } };
        ShoreGrid grid = ShoreGrid.Build(heights, new Vector3(64f, 100f, 64f), Vector3.zero, 64, still, rivers, 5f);
        Vector2 inPond = grid.At(new Vector2(25f, 25f));
        Assert.AreEqual(10.5f, inPond.x, 1e-3f, "in the pond: its surface (metres above the terrain's base)");
        Assert.AreEqual(0f, inPond.y, 1e-3f, "and no distance to its edge");
        Vector2 beside = grid.At(new Vector2(33.2f, 25f));
        Assert.AreEqual(10.5f, beside.x, 1e-3f, "three metres off: still the pond's");
        Assert.That(beside.y, Is.InRange(2f, 4f), "about three metres to the edge");
        Assert.IsTrue(float.IsNaN(grid.At(new Vector2(40f, 25f)).x), "ten metres off: no shore");
        Assert.AreEqual(10.4f, grid.At(new Vector2(10f, 51f)).x, 1e-3f, "by the river: its surface");
    }

    [Test]
    public void TheHavenCarriesItsField()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity", OpenSceneMode.Additive);
        try
        {
            SceneGroundLight[] found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneGroundLight>(true)).ToArray();
            Assert.AreEqual(1, found.Length, "one per scene");
            Assert.IsTrue(found[0].Look.Enabled);
            Assert.AreEqual(0f, found[0].Look.Bounce, "its hours turn: no light thrown back from one hour's sun");
            Assert.Greater(found[0].Look.Sunflecks, 0f, "sun through the crowns");
            Assert.That(found[0].Look.Relief, Is.InRange(0.05f, 0.4f), "a gentle land's shade: the Haven stays light");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
