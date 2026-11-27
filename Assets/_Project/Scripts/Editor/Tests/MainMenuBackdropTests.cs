using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The start menu's living picture (the user, 2026-11-27): the Haven at dusk in painted layers that drift as a cinematic's do, looping
/// without a seam, and the menu's column fitted to the screen.
/// </summary>
public sealed class MainMenuBackdropTests
{
    private const string BackdropPath = "Assets/_Project/Data/Cinematics/MainMenuBackdrop.asset";

    [Test]
    public void TheCamera_GoesThereAndBack_AndIsWhereItBeganEveryLoop()
    {
        var from = new CinematicPose(-0.01f, 0f, 0f);
        var to = new CinematicPose(0.012f, 0.004f, 0.035f);
        const float sway = 40f;
        Assert.AreEqual(from.position, MenuBackdrop.PoseAt(from, to, 0f, sway).position, "it starts at Camera From");
        Assert.AreEqual(to.push, MenuBackdrop.PoseAt(from, to, sway, sway).push, 1e-5f, "one sway reaches Camera To");
        for (float t = 0f; t < 2f * sway; t += 3.7f)
        {
            CinematicPose now = MenuBackdrop.PoseAt(from, to, t, sway), aLoopOn = MenuBackdrop.PoseAt(from, to, t + 2f * sway, sway);
            Assert.AreEqual(now.position.x, aLoopOn.position.x, 1e-4f, $"the same at {t} s and a loop later");
            Assert.AreEqual(now.push, aLoopOn.push, 1e-4f);
        }

        // Eased at the turn: no jolt where it comes back.
        float before = MenuBackdrop.PoseAt(from, to, sway - 0.5f, sway).push, after = MenuBackdrop.PoseAt(from, to, sway + 0.5f, sway).push;
        Assert.AreEqual(before, after, 1e-4f, "it turns smoothly at Camera To");
    }

    [Test]
    public void TheColumn_ShrinksOnlyWhenTheScreenIsTooShort()
    {
        Assert.AreEqual(1f, FitColumnToScreen.Scale(700f, 900f, 64f), "fits: left as it is");
        Assert.AreEqual((720f - 128f) / 964f, FitColumnToScreen.Scale(964f, 720f, 64f), 1e-4f, "a 16:9 canvas: shrunk to fit inside the margins");
    }

    [Test]
    public void TheBackdrop_IsPaintedInLayers_ThatCoverTheFrame_AndTheMenuDrawsIt()
    {
        var backdrop = AssetDatabase.LoadAssetAtPath<CinematicDefinition>(BackdropPath);
        Assert.IsNotNull(backdrop);
        CinematicShot shot = backdrop.Shots[0];
        var paintings = shot.layers.Where(layer => layer.kind == CinematicLayerKind.Picture && !layer.additive && !layer.round && layer.name != "Vignette"
                                                   && !layer.name.Contains("Mist")).ToList();
        Assert.GreaterOrEqual(paintings.Count, 5, "sky, houses, portal, grass and stones");
        foreach (CinematicLayer layer in paintings)
        {
            Assert.IsNotNull(layer.picture, $"{layer.name} has its painting");
            Assert.GreaterOrEqual(layer.size + 0.0001f, CinematicCamera.SizeToCover(layer.depth, layer.offset, shot.cameraFrom, shot.cameraTo),
                $"{layer.name} never shows its edge as the camera sways");
        }

        Assert.IsTrue(shot.layers.Any(layer => layer.name.Contains("Fire") && layer.flicker > 0f), "the fire's light flickers");
        Assert.IsTrue(shot.layers.Any(layer => layer.name.Contains("Smoke") && layer.kind == CinematicLayerKind.Motes), "smoke from the chimneys");

        string scene = System.IO.File.ReadAllText("Assets/_Project/Scenes/MainMenu.unity");
        StringAssert.Contains(AssetDatabase.AssetPathToGUID(BackdropPath), scene, "the main menu draws it");
    }
}
