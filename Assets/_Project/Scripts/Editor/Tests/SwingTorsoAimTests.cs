using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A swing walked through keeps its torso aimed as its clip aims it (SwingTorsoAim, after V Rising's chest proxy): the facing measure,
/// and the player's baked facings being current.
/// </summary>
public sealed class SwingTorsoAimTests
{
    // The measure can fail: shoulders square to the body face ahead; turned a quarter right they face 90; a quarter left, -90.
    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(-90f)]
    [TestCase(35f)]
    public void TheShoulderFacing_IsTheirTurnFromAhead(float degrees)
    {
        var body = new GameObject("body").transform;
        try
        {
            Quaternion turn = Quaternion.AngleAxis(degrees, Vector3.up);
            Assert.AreEqual(degrees, SwingTorsoAim.ShoulderYaw(body, turn * new Vector3(-0.2f, 1.4f, 0f), turn * new Vector3(0.2f, 1.4f, 0f)), 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(body.gameObject);
        }
    }

    [Test]
    public void ThePlayer_HasEverySwingsFacingMeasured()
    {
        SwingTorsoAim.Facing[] measured = SwingTorsoAimFit.Measure();
        // Some sixty swings the Upper Body layer plays, less the spins (more than Most Swing of turn, left to the legs): 49 on 2026-11-20.
        Assert.Greater(measured.Length, 40, "swings the Upper Body layer plays");
        Assert.IsFalse(System.Array.Exists(measured, f => f.state == "Sword Finisher"), "a spin (the sword's finisher, 476 degrees) isn't aimed back");
        Assert.IsTrue(System.Array.Exists(measured, f => f.state == "KI Punch Straight"), "a punch is");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        var aim = prefab.GetComponentInChildren<SwingTorsoAim>(true);
        Assert.IsNotNull(aim, "on the player's Animator");
        Assert.IsTrue(SwingTorsoAimFit.Same(aim.Facings, measured), "run Safe Haven › Tools › Fit Swing Torso Aim");
    }
}
