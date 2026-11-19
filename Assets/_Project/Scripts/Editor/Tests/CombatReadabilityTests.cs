using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Reading a fight (Docs/CombatFeelReview_2026-11-19.md §8, §9, §12, §15): whose blow a number shows and how big it is, which kill ends a
/// fight, when a strike counts as out of view, and the shape of a camera kick.
/// </summary>
public sealed class CombatReadabilityTests
{
    [Test]
    public void ANumber_SaysWhoseBlowItWas()
    {
        var player = new GameObject("Player", typeof(Player));
        var arm = new GameObject("Arm");
        arm.transform.SetParent(player.transform);
        var wolf = new GameObject("Wolf");
        var companion = new GameObject("Companion");
        try
        {
            Assert.AreEqual(DamageNumber.Kind.Taken, DamageNumberSpawner.KindOf(player, wolf, player), "a blow on the player");
            Assert.AreEqual(DamageNumber.Kind.Mine, DamageNumberSpawner.KindOf(wolf, arm, player), "the player's own blow, from a bone");
            Assert.AreEqual(DamageNumber.Kind.Others, DamageNumberSpawner.KindOf(wolf, companion, player), "a companion's");
            Assert.AreEqual(DamageNumber.Kind.Others, DamageNumberSpawner.KindOf(wolf, null, player), "a burn with no one behind it");
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(wolf);
            Object.DestroyImmediate(companion);
        }
    }

    [Test]
    public void ANumber_GrowsWithTheShareOfHealthItTook()
    {
        Assert.AreEqual(0.85f, DamageNumber.SizeForShare(0.02f), 1e-4f, "chip damage is small");
        Assert.AreEqual(1f, DamageNumber.SizeForShare(0.1f), 1e-4f);
        Assert.AreEqual(1.3f, DamageNumber.SizeForShare(0.5f), 1e-4f, "a third of its health and more is the largest");
        Assert.Less(DamageNumber.SizeForShare(0.07f), DamageNumber.SizeForShare(0.2f));
    }

    [Test]
    public void OnlyTheLastFoeOfARealFight_EndsIt()
    {
        CombatFeelSettings feel = CombatFeelSettings.Current;
        Assert.IsFalse(CombatFeedback.EndsAFight(1, 1f, false, feel), "a lone rootling felled at once is no fight won");
        Assert.IsTrue(CombatFeedback.EndsAFight(feel.FightWonFoes, 1f, false, feel), "a pack cleared");
        Assert.IsTrue(CombatFeedback.EndsAFight(1, feel.FightWonSeconds + 0.1f, false, feel), "a long duel");
        Assert.IsFalse(CombatFeedback.EndsAFight(3, 20f, true, feel), "a troll still hunting the player: not over");
    }

    [Test]
    public void AStrikeOutOfFrame_IsOffScreen()
    {
        Assert.IsFalse(OffScreenWarnings.IsOffScreen(new Vector3(0.5f, 0.5f, 10f)));
        Assert.IsFalse(OffScreenWarnings.IsOffScreen(new Vector3(1.01f, 0.5f, 10f)), "its outline still shows at the edge");
        Assert.IsTrue(OffScreenWarnings.IsOffScreen(new Vector3(0.5f, -0.2f, 10f)), "below the frame");
        Assert.IsTrue(OffScreenWarnings.IsOffScreen(new Vector3(0.5f, 0.5f, -3f)), "behind the camera");
    }

    [Test]
    public void AKick_PushesAtOnce_AndSpringsBack()
    {
        Assert.AreEqual(0f, CameraShake.KickShare(-0.01f));
        Assert.AreEqual(1f, CameraShake.KickShare(0.03f), 1e-3f, "full within 30 ms");
        float midway = CameraShake.KickShare(0.09f);
        Assert.Greater(midway, 0f);
        Assert.Less(midway, 1f);
        Assert.AreEqual(0f, CameraShake.KickShare(0.2f), "gone after 150 ms");
    }

    // Failure that teaches (Docs/SystemsGapReview_2026-11-19.md §7a).
    [Test]
    public void ADeath_KeepsItsLastThreeBlows()
    {
        RecentBlows.Clear();
        RecentBlows.Note("one");
        RecentBlows.Note("two");
        RecentBlows.Note("three");
        RecentBlows.Note("four");
        CollectionAssert.AreEqual(new[] { "two", "three", "four" }, RecentBlows.Take(), "the oldest goes");
        Assert.IsEmpty(RecentBlows.Take(), "taken once");
    }

    [Test]
    public void GrowsWithFalls_EasesTwoPercentAFall_ToHalf()
    {
        Assert.AreEqual(0.9f, GameOptions.FallsShare(0), 1e-4f);
        Assert.AreEqual(0.86f, GameOptions.FallsShare(2), 1e-4f);
        Assert.AreEqual(0.5f, GameOptions.FallsShare(40), 1e-4f, "never below half");
    }

    [Test]
    public void RaidLength_LengthensTheClock()
    {
        RaidDefinition raid = UnityEditor.AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        Assert.IsNotNull(raid);
        float was = GameOptions.RaidLength.Value;
        try
        {
            GameOptions.RaidLength.Value = 0f;
            float normal = RaidParameters.Calculate(raid, (System.Collections.Generic.IEnumerable<RaidModifierDefinition>)null).Duration;
            GameOptions.RaidLength.Value = 1f;
            Assert.AreEqual(normal * 1.25f, RaidParameters.Calculate(raid, (System.Collections.Generic.IEnumerable<RaidModifierDefinition>)null).Duration, 1f, "+25%");
        }
        finally
        {
            GameOptions.RaidLength.Value = was;
        }
    }

    // A friend following a raid runs on the host's clock, whatever their own Raid Length (the review of 2026-11-19).
    [Test]
    public void AFollower_RunsOnTheHostsClock()
    {
        RaidDefinition raid = UnityEditor.AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        float was = GameOptions.RaidLength.Value;
        try
        {
            GameOptions.RaidLength.Value = 2f;
            var invite = new RaidInvite { raid = raid.Id, seed = 7, totalSeconds = 777f };
            RaidSession follower = RaidLauncher.FromInvite(invite, raid, content, null, "SafeHaven", 0f);
            Assert.AreEqual(777f, follower.TotalSeconds, 0.5f, "the host's total, not this game's +50%");
        }
        finally
        {
            GameOptions.RaidLength.Value = was;
        }
    }
}
