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
    public void TheDaysLight_IsTheSunsPlacesWeighted()
    {
        // Flat ground 16 cells of 1 m with a wall 10 m high along x = 8: the morning sun (from +x) is blocked west of it, the evening
        // sun (from -x) east of it. Over the day, each side gets about half; a weight of 0 (night) adds nothing.
        const int size = 16;
        var ground = new float[size * size];
        var tops = new float[size * size];
        for (int z = 0; z < size; z++)
        {
            tops[z * size + 8] = 10f;
        }

        Vector3 morning = new Vector3(1f, 0.5f, 0f).normalized, evening = new Vector3(-1f, 0.5f, 0f).normalized;
        float[] westMorning = GroundLightField.SunLight(ground, tops, size, 1f, morning);
        float[] day = GroundLightField.DayLight(ground, tops, size, 1f, new[] { morning, evening, Vector3.down }, new[] { 1f, 1f, 0f });
        int west = 8 * size + 5, east = 8 * size + 11;
        Assert.Less(westMorning[west], 0.5f, "the wall shades the morning sun west of it");
        float open = GroundLightField.SunLight(ground, tops, size, 1f, evening)[west]; // the evening's sun there, unblocked
        Assert.Greater(open, 0.3f);
        Assert.AreEqual(open * 0.5f, day[west], 0.02f, "over the day, half the sun: the evening's, not the morning's");
        Assert.AreEqual(open * 0.5f, day[east], 0.02f, "and east of the wall, the morning's");
        CollectionAssert.Contains(GameOptions.Graphics, GameOptions.HavenSunBounce, "a graphics option");
        Assert.AreEqual(1f, GameOptions.HavenSunBounce.Default, "on by default: the game as designed");
    }

    [Test]
    public void TheHavenCarriesItsField() => CarriesItsField("Assets/_Project/Scenes/SafeHaven.unity", 1, true);

    [Test]
    public void ThePrologueCarriesItsField_OverBothTerrains() => CarriesItsField("Assets/_Project/Scenes/Prologue_MistyWoods.unity", 2, false);

    // The Haven throws light back from the day's sun (the user, 2026-11-17: "sun bounce too, as a toggle in the graphics options"):
    // from its terrain's colour map. Over the prologue's two terrains there is no one colour map, so none.
    private static void CarriesItsField(string path, int terrains, bool bounces)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            SceneGroundLight[] found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SceneGroundLight>(true)).ToArray();
            Assert.AreEqual(1, found.Length, "one per scene");
            Assert.AreEqual(terrains, found[0].Terrains().Count(t => t.gameObject.scene == scene), "the terrains it lies over");
            Assert.IsTrue(found[0].Look.Enabled);
            if (bounces)
            {
                Assert.That(found[0].Look.Bounce, Is.InRange(0.3f, 1.2f), "light thrown back from the day's sun, about the raids'");
                Terrain ground = found[0].Terrains()[0];
                Assert.IsTrue(ground.TryGetComponent(out GrassTerrainColour paint) && paint.ColourMap != null, "the ground's colours come from its colour map");
            }

            Assert.Greater(found[0].Look.Sunflecks, 0f, "sun through the crowns");
            Assert.That(found[0].Look.Relief, Is.InRange(0.05f, 0.4f), "a gentle land's shade: the Haven stays light");
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
