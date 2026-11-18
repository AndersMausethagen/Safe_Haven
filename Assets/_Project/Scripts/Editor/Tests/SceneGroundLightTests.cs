using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The painted scenes' ground light field (SceneGroundLight, the overhaul): the Haven and the prologue carry it with no light thrown
/// back (their hours turn), its shores come from the scene's own water (ShoreGrid): ground under a pond's surface is water, a cell a
/// few metres off has the pond's surface and its distance, and beyond the reach none. Over several terrains (the prologue's meadow and
/// forest) their heights are laid on one grid (FieldGround.Across).
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
    public void SeveralTerrains_AreLaidOnOneGrid()
    {
        // A 100 m terrain at height 0 (flat at 10 m) and beside it, east, a 50 m one sunk 7 m (flat at 2 m above its base), as the
        // prologue's meadow and forest lie.
        var low = new float[33, 33];
        var high = new float[33, 33];
        for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) { high[z, x] = 0.1f; low[z, x] = 0.02f; }
        var pieces = new List<FieldGround.Piece>
        {
            new FieldGround.Piece { Heights = high, Resolution = 33, Position = new Vector3(0f, 0f, 0f), Size = new Vector3(100f, 100f, 100f) },
            new FieldGround.Piece { Heights = low, Resolution = 33, Position = new Vector3(100f, -7f, 0f), Size = new Vector3(50f, 100f, 100f) },
        };
        FieldGround ground = FieldGround.Across(pieces, 151);
        Assert.AreEqual(150f, ground.Size.x, 1e-3f, "a square over both: 150 m");
        Assert.AreEqual(150f, ground.Size.z, 1e-3f);
        Assert.AreEqual(Vector3.zero.x, ground.Origin.x);
        Assert.AreEqual(-5f, ground.Lowest, 1e-3f, "the sunk terrain's ground: -7 + 2");
        Assert.AreEqual(10f, ground.Highest, 1e-3f);
        float At(float x, float z) => ground.Origin.y + ground.Heights[Mathf.RoundToInt(z), Mathf.RoundToInt(x)] * ground.Size.y;
        Assert.AreEqual(10f, At(50f, 50f), 1e-3f, "on the first terrain: its ground");
        Assert.AreEqual(-5f, At(125f, 50f), 1e-3f, "on the second: its own, in world heights");
        Assert.AreEqual(10f, At(50f, 140f), 1e-3f, "off both (north of the first): the nearest one's edge, not a pit");
    }

    [Test]
    public void TheHavenCarriesItsField() => CarriesItsField("Assets/_Project/Scenes/SafeHaven.unity", 1);

    [Test]
    public void ThePrologueCarriesItsField_OverBothTerrains() => CarriesItsField("Assets/_Project/Scenes/Prologue_MistyWoods.unity", 2);

    private static void CarriesItsField(string path, int terrains)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            SceneGroundLight[] found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneGroundLight>(true)).ToArray();
            Assert.AreEqual(1, found.Length, "one per scene");
            Assert.AreEqual(terrains, found[0].Terrains().Count(t => t.gameObject.scene == scene), "the terrains it lies over");
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
