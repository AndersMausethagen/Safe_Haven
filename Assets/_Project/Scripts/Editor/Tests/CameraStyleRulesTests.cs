using NUnit.Framework;

public sealed class CameraStyleRulesTests
{
    private static CameraStyleOverride[] Overrides(
        CameraStyleOverride exploration = CameraStyleOverride.PlayerChoice,
        CameraStyleOverride combat = CameraStyleOverride.PlayerChoice,
        CameraStyleOverride building = CameraStyleOverride.PlayerChoice,
        CameraStyleOverride raid = CameraStyleOverride.PlayerChoice)
    {
        return new[] { exploration, combat, building, raid };
    }

    [Test]
    public void WithoutOverrides_TheToggleDecides()
    {
        var situation = new CameraSituation(building: false, combat: true, raid: true);
        Assert.AreEqual(CameraStyle.Action, CameraStyleRules.Resolve(CameraStyle.Action, Overrides(), situation, out CameraContext? by));
        Assert.IsNull(by);
        Assert.AreEqual(CameraStyle.TopDown, CameraStyleRules.Resolve(CameraStyle.TopDown, Overrides(), situation, out _));
    }

    [Test]
    public void AnOverrideOnlyAppliesInItsSituation()
    {
        CameraStyleOverride[] overrides = Overrides(combat: CameraStyleOverride.TopDown);
        Assert.AreEqual(CameraStyle.Action, CameraStyleRules.Resolve(CameraStyle.Action, overrides, new CameraSituation(false, false, false), out _));
        Assert.AreEqual(CameraStyle.TopDown, CameraStyleRules.Resolve(CameraStyle.Action, overrides, new CameraSituation(false, true, false), out CameraContext? by));
        Assert.AreEqual(CameraContext.Combat, by);
    }

    [Test]
    public void TheMostSpecificSituationWins()
    {
        CameraStyleOverride[] overrides = Overrides(
            exploration: CameraStyleOverride.Action,
            combat: CameraStyleOverride.TopDown,
            raid: CameraStyleOverride.Action);

        // Combat inside a raid: Combat is more specific than Raid.
        Assert.AreEqual(CameraStyle.TopDown, CameraStyleRules.Resolve(CameraStyle.Action, overrides, new CameraSituation(false, true, true), out CameraContext? by));
        Assert.AreEqual(CameraContext.Combat, by);

        // Exploring a raid: Raid beats Exploration.
        Resolve(overrides, new CameraSituation(false, false, true), out by);
        Assert.AreEqual(CameraContext.Raid, by);
    }

    [Test]
    public void ASituationLeftToTheToggleFallsThroughToTheNextOne()
    {
        CameraStyleOverride[] overrides = Overrides(raid: CameraStyleOverride.TopDown);
        // Combat isn't overridden, so the raid's choice applies to raid combat.
        Assert.AreEqual(CameraStyle.TopDown, CameraStyleRules.Resolve(CameraStyle.Action, overrides, new CameraSituation(false, true, true), out CameraContext? by));
        Assert.AreEqual(CameraContext.Raid, by);
    }

    [Test]
    public void BuildingAtHomeUsesBuildingThenExploration()
    {
        Assert.AreEqual(CameraStyle.TopDown,
            CameraStyleRules.Resolve(CameraStyle.Action, Overrides(building: CameraStyleOverride.TopDown, exploration: CameraStyleOverride.Action),
                new CameraSituation(true, false, false), out CameraContext? by));
        Assert.AreEqual(CameraContext.Building, by);

        Assert.AreEqual(CameraStyle.Action,
            CameraStyleRules.Resolve(CameraStyle.TopDown, Overrides(exploration: CameraStyleOverride.Action),
                new CameraSituation(true, false, false), out by));
        Assert.AreEqual(CameraContext.Exploration, by);
    }

    [Test]
    public void ExplorationDoesNotApplyInCombat()
    {
        Assert.AreEqual(CameraStyle.TopDown,
            CameraStyleRules.Resolve(CameraStyle.TopDown, Overrides(exploration: CameraStyleOverride.Action), new CameraSituation(false, true, false), out CameraContext? by));
        Assert.IsNull(by);
    }

    private static CameraStyle Resolve(CameraStyleOverride[] overrides, CameraSituation situation, out CameraContext? by)
    {
        return CameraStyleRules.Resolve(CameraStyle.TopDown, overrides, situation, out by);
    }
}
