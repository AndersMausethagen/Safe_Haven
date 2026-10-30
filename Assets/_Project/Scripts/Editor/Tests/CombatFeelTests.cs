using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for combat feel: slash arc direction, telegraph shapes, the shared settings and the dodge tuning.</summary>
public sealed class CombatFeelTests
{
    [Test]
    public void HitRecoil_SnapsOutThenSettlesWithASmallOvershoot()
    {
        Assert.AreEqual(0f, MonsterPose.RecoilCurve(0f), 0.0001f);
        Assert.AreEqual(0f, MonsterPose.RecoilCurve(1f), 0.0001f, "Back at rest when it ends.");
        Assert.Greater(MonsterPose.RecoilCurve(0.25f), 0.5f, "Most of the snap comes early.");
        float overshoot = MonsterPose.RecoilCurve(0.8f);
        Assert.Less(overshoot, 0f, "It swings a little past rest on the way back.");
        Assert.Greater(overshoot, -0.25f);
    }

    [Test]
    public void SlashArc_SweepsTheWayTheHandMoves()
    {
        Assert.AreEqual(SlashSweep.RightToLeft, CombatAnimationSetup.SlashFor(new Vector3(-8f, 0f, 1f)).sweep);
        Assert.AreEqual(SlashSweep.LeftToRight, CombatAnimationSetup.SlashFor(new Vector3(8f, 0f, 1f)).sweep);
        Assert.AreEqual(0f, CombatAnimationSetup.SlashFor(new Vector3(-8f, 0f, 1f)).tilt, 0.01f, "A level swing draws a flat arc.");
    }

    [Test]
    public void SlashArc_RaisesTheEndTheSwingStartsFrom()
    {
        // Coming down from the right: the right end sits higher (positive roll); from the left, the left end.
        Assert.Greater(CombatAnimationSetup.SlashFor(new Vector3(-3f, -3f, 0f)).tilt, 30f);
        Assert.Less(CombatAnimationSetup.SlashFor(new Vector3(3f, -3f, 0f)).tilt, -30f);
        Assert.LessOrEqual(Mathf.Abs(CombatAnimationSetup.SlashFor(new Vector3(-0.5f, -20f, 0f)).tilt), 80f, "Never fully on edge to the camera.");
    }

    [Test]
    public void SlashArc_ThrustsStayFlat()
    {
        Assert.AreEqual(0f, CombatAnimationSetup.SlashFor(new Vector3(-1f, -1f, 12f)).tilt, 0.01f);
    }

    [Test]
    public void TelegraphFan_CoversTheArcAtRadiusOne()
    {
        Mesh fan = AttackTelegraph.FanFor(90f);

        Bounds bounds = fan.bounds;
        Assert.AreEqual(1f, bounds.max.z, 0.001f, "The fan reaches radius 1 straight ahead.");
        Assert.AreEqual(Mathf.Sin(45f * Mathf.Deg2Rad), bounds.max.x, 0.001f, "A 90 degree arc spans 45 degrees each side.");
        Assert.AreEqual(0f, bounds.size.y, 0.0001f, "The fan is flat.");
        Assert.AreEqual(0, fan.triangles.Length % 3);
    }

    [Test]
    public void TelegraphFan_FullCircleReachesBehind_AndIsShared()
    {
        Mesh circle = AttackTelegraph.FanFor(360f);

        Assert.AreEqual(-1f, circle.bounds.min.z, 0.001f);
        Assert.AreSame(circle, AttackTelegraph.FanFor(360f));
    }

    [Test]
    public void TelegraphFans_FaceUp_SoTheyAreVisibleFromAbove()
    {
        Mesh fan = AttackTelegraph.FanFor(120f);

        foreach (Vector3 normal in fan.normals)
        {
            Assert.Greater(normal.y, 0.99f);
        }
    }

    [Test]
    public void CombatFeel_LoadsTheProjectAsset_WithATelegraphMaterial()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;

        Assert.IsNotNull(feel);
        Assert.IsNotNull(feel.TelegraphMaterial, "Resources/CombatFeel needs a material, or monsters show no attack warning.");
        Assert.LessOrEqual(feel.HitStopOnHit, feel.HitStopOnKill, "A kill should hit at least as hard as a normal hit.");
    }

    [Test]
    public void DodgeDefaults_GiveInvulnerabilityAndFitInsideTheCooldown()
    {
        var settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
        try
        {
            Assert.Greater(settings.DodgeInvulnerability, 0f);
            Assert.Greater(settings.DodgeCooldown, settings.DodgeDuration, "Dodges can't chain into permanent movement.");
            Assert.Greater(settings.DodgeDistance / settings.DodgeDuration, settings.MoveSpeed, "A dodge is faster than running.");
        }
        finally
        {
            Object.DestroyImmediate(settings);
        }
    }
}
