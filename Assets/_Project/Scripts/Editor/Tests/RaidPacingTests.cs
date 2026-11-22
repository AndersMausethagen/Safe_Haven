using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The raid's end as the user asked for it (Docs/RaidLoopResearch_2026-11-22.md and the answers of 2026-11-22): the clock runs into
/// overtime with the portals open; the mist grips harder the longer one stays; stages pay more on nodes and wear tools for it; nodes
/// don't regrow in a raid; quick raids pay those away less; Well Rested lasts the raid by comfort; rescues from the second on call out
/// later.
/// </summary>
public sealed class RaidPacingTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        if (GameSession.ActiveRaid != null)
        {
            GameSession.AbandonRaid(GameSession.ActiveRaid);
        }

        foreach (Object made in created)
        {
            Object.DestroyImmediate(made);
        }

        created.Clear();
    }

    private RaidSession Session()
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        created.Add(raid);
        var none = new RaidModifierDefinition[0];
        return new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SafeHaven");
    }

    private static IEnumerable<RaidDefinition> OpenRaids() => new[] { "MistyWoods", "TrollValley", "MossMarsh" }
        .Select(name => AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{name}.asset"));

    // ----- Overtime -----

    [Test]
    public void OutOfTime_WithOvertime_ThePortalsStandOpen_ThenTheMistTakesYou()
    {
        RaidSession session = Session();
        Assert.IsFalse(session.Tick(session.TotalSeconds, false, 180f), "the clock running out doesn't end it");
        Assert.IsFalse(session.IsOver);
        Assert.IsFalse(session.Tick(100f, false, 180f));
        Assert.AreEqual(100f, session.Overtime, 0.01f);
        Assert.AreEqual(session.TotalSeconds + 100f, session.PlayedSeconds, 0.01f, "played time counts the overtime");
        Assert.IsTrue(session.Tick(80f, false, 180f), "the portals close");
        Assert.AreEqual(RaidOutcome.TimeExpired, session.Outcome);
    }

    [Test]
    public void OutOfTime_WithoutOvertime_EndsAtTheClock_AsBefore()
    {
        RaidSession session = Session();
        Assert.IsTrue(session.Tick(session.TotalSeconds), "the canary: the old rule ends at zero");
        Assert.AreEqual(RaidOutcome.TimeExpired, session.Outcome);
        Assert.AreEqual(0f, session.Overtime);
    }

    [Test]
    public void TheMistGripsHarder_EachStep()
    {
        RaidPacingSettings pacing = RaidPacingSettings.Current;
        Assert.AreEqual(0, pacing.MistStepsAt(0f));
        Assert.AreEqual(1, pacing.MistStepsAt(0.1f), "the first step as the portals open");
        Assert.AreEqual(2, pacing.MistStepsAt(pacing.MistStepSeconds + 0.1f));
        Assert.Greater(pacing.OvertimeSeconds, 0f, "overtime is on");
        Assert.IsNotNull(pacing.PortalLook, "a portal stands at the stones");
        Assert.IsTrue(pacing.MistStepPenalties.Count > 0 && pacing.MistStepPenalties.All(Harms), "each step takes something");
    }

    // A penalty takes from the player: less of a stat, or more of a cost.
    private static bool Harms(StatBonus bonus) => bonus.stat.ToString().EndsWith("Cost") ? bonus.value > 0f : bonus.value < 0f;

    // ----- Stages -----

    [Test]
    public void EachRaidsStages_PayMoreOnNodes_HoldShorter_AndTheMistGripsHarder()
    {
        foreach (RaidDefinition raid in OpenRaids())
        {
            IReadOnlyList<RaidStage> stages = raid.Stages;
            Assert.GreaterOrEqual(stages.Count, 3, raid.name);
            Assert.Greater(stages[0].GatherBonus, 0f, $"{raid.name}: the canary, the first stage pays");
            Assert.IsTrue(stages.Any(stage => stage.Veins), $"{raid.name}: a stage sprouts veins");
            for (int i = 1; i < stages.Count; i++)
            {
                Assert.Greater(stages[i].GatherBonus, stages[i - 1].GatherBonus, $"{raid.name} stage {i}: more for staying");
                Assert.Less(stages[i].HoldScale, stages[i - 1].HoldScale, $"{raid.name} stage {i}: the way home is quicker");
                Assert.GreaterOrEqual(stages[i].MistPenalties.Count, stages[i - 1].MistPenalties.Count, $"{raid.name} stage {i}: the mist only grips harder");
            }

            Assert.IsTrue(stages.SelectMany(stage => stage.MistPenalties).All(Harms), $"{raid.name}: Mist-touched only takes");
        }
    }

    [Test]
    public void TheVeins_HaveANodeForEveryTier_ThatIsOre()
    {
        RaidPacingSettings pacing = RaidPacingSettings.Current;
        for (int tier = 1; tier <= Tiers.Max; tier++)
        {
            GameObject node = pacing.VeinNodeAt(tier);
            Assert.IsNotNull(node, $"tier {tier}");
            Assert.IsTrue(node.TryGetComponent(out ResourceNode resource) && resource.Definition != null
                          && resource.Definition.GatherTool == ToolType.Pickaxe, $"tier {tier}: {node.name} is mined");
        }

        Assert.Greater(pacing.VeinYield, 1f, "a vein is richer");
        Assert.Greater(pacing.ToolWearFor(0.3f), 0f, "staying wears the tools");
        Assert.AreEqual(0f, pacing.ToolWearFor(0f), "no stage, no extra wear");
    }

    [Test]
    public void ANodesYield_IsTheStagesAndTheVeinsTogether()
    {
        var go = new GameObject("Node");
        created.Add(go);
        ResourceSource source = go.AddComponent<ResourceSource>();
        Assert.AreEqual(1f, source.YieldNow, 1e-4f, "at home, as it is");
        source.VeinYield = 1.5f;
        Assert.AreEqual(1.5f, source.YieldNow, 1e-4f);
        source.VeinYield = 0.2f;
        Assert.AreEqual(1f, source.VeinYield, 1e-4f, "a vein is never poorer");
    }

    // ----- No regrowth in a raid -----

    [Test]
    public void ANode_NeverRegrows_OnARaid_NorAtHome()
    {
        GatheringNodeDefinition copper = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Resources/CopperOreNode.prefab")
            .GetComponent<ResourceNode>().Definition;
        Assume.That(copper.RespawnSeconds > 0f);
        Assert.IsTrue(Regrowth.Grows(false, true), "the canary: at home, where the Haven allows it, it grows back");
        Assert.IsFalse(Regrowth.Grows(true, true), "never in a raid");
        Assert.IsFalse(HavenSettings.Current.NodesRegrowAtHome, "the user, 2026-11-22: nothing grows back at home");
        Assert.AreEqual(0f, copper.RegrowsAfter, "at home, gone for good");
        GameSession.BeginRaid(Session());
        // A raid's scene: its director is up (inactive, so nothing of it runs).
        var holder = new GameObject("Raid");
        holder.SetActive(false);
        created.Add(holder);
        RaidDirector director = holder.AddComponent<RaidDirector>();
        typeof(RaidDirector).GetProperty(nameof(RaidDirector.Current)).SetValue(null, director);
        try
        {
            Assert.AreEqual(0f, copper.RegrowsAfter, "in a raid's scene, gone for good");
        }
        finally
        {
            typeof(RaidDirector).GetProperty(nameof(RaidDirector.Current)).SetValue(null, null);
        }
    }

    // ----- Time away -----

    [Test]
    public void AQuickRaid_PaysThoseAwayLess_UpToAFullShareAtNineMinutes()
    {
        RaidPacingSettings pacing = RaidPacingSettings.Current;
        float quick = pacing.TimeAwayShare(180f);
        Assert.Less(quick, 1f, "the canary: three minutes pays less");
        Assert.AreEqual(quick, pacing.TimeAwayShare(240f), 1e-4f, "the floor holds to four minutes");
        Assert.Greater(pacing.TimeAwayShare(400f), quick, "rising");
        Assert.AreEqual(1f, pacing.TimeAwayShare(540f), 1e-4f, "all of it at nine minutes");
        Assert.AreEqual(1f, pacing.TimeAwayShare(900f), 1e-4f, "and no more beyond");
    }

    // ----- Well Rested -----

    [Test]
    public void WellRested_IsSizedByComfort()
    {
        int full = HavenSettings.Current.RestedFullComfort;
        Assert.AreEqual(0f, HavenReadiness.RestedShareAt(0, full));
        Assert.AreEqual(0.5f, HavenReadiness.RestedShareAt(full / 2, full), 1f / full);
        Assert.AreEqual(1f, HavenReadiness.RestedShareAt(full, full));
        Assert.AreEqual(1f, HavenReadiness.RestedShareAt(full * 3, full), "no more than all of it");
    }

    // ----- Mist-touched, named once a stat -----

    [Test]
    public void Bonuses_ToTheSameStat_AddUpIntoOne()
    {
        var list = new List<StatBonus>
        {
            new StatBonus(StatType.MaxHealth, -0.05f, StatModifierOp.Percent),
            new StatBonus(StatType.MoveSpeed, -0.03f, StatModifierOp.Percent),
            new StatBonus(StatType.MaxHealth, -0.08f, StatModifierOp.Percent),
            new StatBonus(StatType.MaxHealth, -4f),
        };
        StatBonus.Combine(list);
        Assert.AreEqual(3, list.Count, "flat and percent stay apart");
        Assert.AreEqual(StatType.MaxHealth, list[0].stat, "first seen, first");
        Assert.AreEqual(-0.13f, list[0].value, 1e-4f);
        Assert.AreEqual(StatType.MoveSpeed, list[1].stat);
    }

    // ----- Rescues and rumours -----

    [Test]
    public void RescuesFromTheSecondOn_CallOutWhenTheMistStirs()
    {
        var rumours = AssetDatabase.FindAssets("t:RaidRumourDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToList();
        List<RaidRumourDefinition> rescues = rumours.Where(rumour => rumour.Seeks != null).ToList();
        Assert.Greater(rescues.Count, 3);
        Assert.IsTrue(rescues.All(rumour => rumour.AppearsAtStage >= 1), string.Join(", ", rescues.Where(r => r.AppearsAtStage < 1).Select(r => r.name)));
        Assert.IsTrue(rumours.Any(rumour => rumour.Seeks == null && rumour.AppearsAtStage > 0), "a late rumour that isn't a rescue");
        RaidDefinition misty = OpenRaids().First();
        StringAssert.Contains(misty.Stages[0].DisplayName, rescues[0].AppearsWhen(misty), "the briefing names the stage");
        Assert.IsNull(rumours.First(rumour => rumour.AppearsAtStage == 0).AppearsWhen(misty), "the canary: one from the start says nothing");
    }
}
