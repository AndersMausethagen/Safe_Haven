using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for wading and swimming: how water slows the player and when they swim or stand.</summary>
public sealed class SwimmingTests
{
    private PlayerMovementSettings settings;

    [SetUp]
    public void SetUp() => settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(settings);

    [Test]
    public void Wading_ShallowWaterDoesNotSlow()
    {
        Assert.AreEqual(1f, settings.WadeSpeedShare(0f), 0.001f);
        Assert.AreEqual(1f, settings.WadeSpeedShare(0.1f), 0.001f);
    }

    [Test]
    public void Wading_DeeperWaterIsSlower()
    {
        float knee = settings.WadeSpeedShare(0.5f);
        float waist = settings.WadeSpeedShare(1f);

        Assert.Less(knee, 1f);
        Assert.Less(waist, knee, "Deeper water should slow the player more.");
        Assert.Greater(waist, settings.WadeSpeedShare(settings.SwimDepth) - 0.001f);
    }

    [Test]
    public void Wading_NeverSlowerThanDeepWadeSpeed()
    {
        Assert.AreEqual(settings.WadeSpeedShare(settings.SwimDepth), settings.WadeSpeedShare(10f), 0.001f);
        Assert.Greater(settings.WadeSpeedShare(10f), 0f);
    }

    [Test]
    public void Swimming_StartsAtSwimDepth()
    {
        float swimDepth = settings.SwimDepth;

        Assert.IsFalse(PlayerSwimming.ShouldSwim(false, swimDepth - 0.01f, true, swimDepth), "Wading just short of swim depth.");
        Assert.IsTrue(PlayerSwimming.ShouldSwim(false, swimDepth, true, swimDepth));
        Assert.IsTrue(PlayerSwimming.ShouldSwim(false, swimDepth + 1f, false, swimDepth), "Jumping into deep water.");
    }

    [Test]
    public void Swimming_KeepsFloatingWithoutTheBottomUnderfoot()
    {
        float swimDepth = settings.SwimDepth;

        Assert.IsTrue(PlayerSwimming.ShouldSwim(true, swimDepth, false, swimDepth));
        Assert.IsTrue(PlayerSwimming.ShouldSwim(true, swimDepth - 0.2f, false, swimDepth), "Bobbing up a little still swims.");
        Assert.IsTrue(PlayerSwimming.ShouldSwim(true, swimDepth - 0.05f, true, swimDepth), "Brushing the bottom still swims.");
    }

    [Test]
    public void Stamina_DrainTakesWhatThereIsAndNeverRefuses()
    {
        var pool = new StaminaPool(10f);

        Assert.IsTrue(pool.Drain(4f));
        Assert.AreEqual(6f, pool.Current, 0.001f);
        Assert.IsTrue(pool.Drain(100f), "A drain larger than what is left empties the pool.");
        Assert.IsTrue(pool.IsEmpty);
        Assert.IsFalse(pool.Drain(1f), "An empty pool has nothing left to drain.");
    }

    [Test]
    public void Swimming_ExhaustedIsSlowerThanFresh()
    {
        // The shipped tuning, not the fresh defaults the other tests here use to test the rules themselves.
        var shipped = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerMovementSettings>("Assets/_Project/Data/Player/PlayerMovementSettings.asset");
        Assert.IsNotNull(shipped, "Data/Player/PlayerMovementSettings");
        Assert.Less(shipped.SwimSpeed * shipped.ExhaustedSwimSpeed, shipped.SwimSpeed);
        Assert.Greater(shipped.ExhaustedSwimSpeed, 0f, "Out of stamina the player still swims: they never drown.");
    }

    [Test]
    public void Swimming_StandsWhenTheBottomComesUp()
    {
        float swimDepth = settings.SwimDepth;

        Assert.IsFalse(PlayerSwimming.ShouldSwim(true, swimDepth - 0.2f, true, swimDepth));
        Assert.IsFalse(PlayerSwimming.ShouldSwim(true, 0f, false, swimDepth), "Out of the water altogether.");
    }
}
