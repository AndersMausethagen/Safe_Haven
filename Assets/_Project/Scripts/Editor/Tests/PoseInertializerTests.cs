using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The blend between moves (PoseInertializer): the ease leaves the old move and reaches the new one flat, the let-go pose coasts on at
/// its own speed and slows to a stop, and the player has one.
/// </summary>
public sealed class PoseInertializerTests
{
    private const float Dt = 1f / 600f;

    [Test]
    public void TheEase_StartsAndEndsFlat()
    {
        Assert.AreEqual(0f, PoseInertializer.Ease(0f), 1e-6f);
        Assert.AreEqual(1f, PoseInertializer.Ease(1f), 1e-6f);
        Assert.AreEqual(0.5f, PoseInertializer.Ease(0.5f), 1e-6f);
        Assert.Less(PoseInertializer.Ease(Dt) / Dt, 0.02f, "no jump into the new move");
        Assert.Less((1f - PoseInertializer.Ease(1f - Dt)) / Dt, 0.02f, "none at the end");
        // It can fail: a crossfade's straight line jumps to full pace at once and stops dead at the end.
        Assert.Less(PoseInertializer.Ease(0.1f), 0.1f * 0.5f, "a line would be at 0.1");
    }

    [Test]
    public void TheLetGoPose_CoastsAtItsOwnSpeed_ThenStops()
    {
        Assert.AreEqual(0f, PoseInertializer.Coast(0f, 0.05f), 1e-6f);
        Assert.AreEqual(1f, PoseInertializer.Coast(1e-4f, 0.05f) / 1e-4f, 0.002f, "as fast as it was moving");
        float far = 0.05f / 0.69314718f;
        Assert.AreEqual(far * 0.5f, PoseInertializer.Coast(0.05f, 0.05f), 1e-4f, "half its way after one half-life");
        Assert.Less(PoseInertializer.Coast(10f, 0.05f), far + 1e-5f, "never further than half-life / ln 2");
        Assert.Greater(PoseInertializer.Coast(10f, 0.05f), far * 0.999f);
    }

    [Test]
    public void ThePlayer_BlendsThisWay()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        var animator = prefab.GetComponentInChildren<Animator>(true);
        Assert.IsNotNull(animator.GetComponent<PoseInertializer>(), "on the player's Animator (PlayerAnimationController.Blend finds it there)");
    }
}
