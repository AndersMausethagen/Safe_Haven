using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class GatheringMinigameTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created)
        {
            if (item != null) Object.DestroyImmediate(item);
        }
        created.Clear();
    }

    [Test]
    public void HarvestFeedbackFallsBackPerFieldToTheDefaultProfile()
    {
        var sound = ScriptableObject.CreateInstance<SoundDefinition>();
        var effect = new GameObject("Test Effect");
        var own = ScriptableObject.CreateInstance<HarvestFeedbackProfile>();
        var fallback = ScriptableObject.CreateInstance<HarvestFeedbackProfile>();
        created.AddRange(new Object[] { sound, effect, own, fallback });

        var fallbackData = new SerializedObject(fallback);
        fallbackData.FindProperty("critical.sound").objectReferenceValue = sound;
        fallbackData.FindProperty("critical.effect").objectReferenceValue = effect;
        fallbackData.ApplyModifiedPropertiesWithoutUndo();
        var ownData = new SerializedObject(own);

        Assert.AreSame(sound, HarvestFeedbackProfile.SoundFor(own, fallback, HarvestMoment.Critical));
        Assert.AreSame(effect, HarvestFeedbackProfile.EffectFor(null, fallback, HarvestMoment.Critical));
        Assert.IsNull(HarvestFeedbackProfile.SoundFor(own, fallback, HarvestMoment.Normal));
        Assert.IsNull(HarvestFeedbackProfile.SoundFor(null, null, HarvestMoment.Harvested));

        var ownSound = ScriptableObject.CreateInstance<SoundDefinition>();
        created.Add(ownSound);
        ownData.FindProperty("critical.sound").objectReferenceValue = ownSound;
        ownData.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreSame(ownSound, HarvestFeedbackProfile.SoundFor(own, fallback, HarvestMoment.Critical));
    }

    [Test]
    public void SwingResultsMapToHarvestMoments()
    {
        Assert.AreEqual(HarvestMoment.Critical, HarvestFeedbackProfile.MomentOf(SwingResult.Critical));
        Assert.AreEqual(HarvestMoment.Normal, HarvestFeedbackProfile.MomentOf(SwingResult.Normal));
        Assert.AreEqual(HarvestMoment.Glancing, HarvestFeedbackProfile.MomentOf(SwingResult.Glancing));
    }

    [Test]
    public void ResourceNodeAddsFeedbackAndRaisesSwingAndHarvestEvents()
    {
        var go = new GameObject("Test Node");
        created.Add(go);
        Interactable interactable = go.AddComponent<Interactable>();
        ResourceNode node = go.AddComponent<ResourceNode>();
        Invoke(interactable, "Awake");
        Invoke(node, "Awake");
        Invoke(node, "OnEnable");
        Assert.IsNotNull(go.GetComponent<ResourceNodeFeedback>());

        SwingResult swung = SwingResult.Glancing;
        int harvested = 0;
        node.Swung += (result, from) => swung = result;
        node.Harvested += () => harvested++;
        node.NotifySwing(SwingResult.Critical, Vector3.zero);
        Invoke(node, "OnGathered");

        Assert.AreEqual(SwingResult.Critical, swung);
        Assert.AreEqual(1, harvested);
        Invoke(node, "OnDisable");
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(target, null);
    }

    [Test]
    public void EveryRaidResourceSpawnIsAGatherableNode()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (RaidSpawnList list in raid.SpawnLists)
            {
                if (list.category != SpawnCategory.Resources)
                {
                    continue;
                }

                foreach (SpawnTable.Entry entry in list.entries)
                {
                    Assert.IsNotNull(entry.prefab, $"{raid.name}/{list.name} has an empty entry");
                    Assert.IsNotNull(entry.prefab.GetComponentInChildren<ResourceNode>(true),
                        $"{raid.name}/{list.name}: {entry.prefab.name} has no ResourceNode, so it can't be gathered");
                }
            }
        }
    }

    [Test]
    public void ZonesJudgeCriticalNormalAndGlancing()
    {
        var zones = new SwingZones(0.5f, 0.4f, 0.1f);
        Assert.AreEqual(SwingResult.Critical, zones.Evaluate(0.52f));
        Assert.AreEqual(SwingResult.Normal, zones.Evaluate(0.65f));
        Assert.AreEqual(SwingResult.Glancing, zones.Evaluate(0.9f));
    }

    [Test]
    public void ZonesStayOnTheBarAndCriticalNeverOutgrowsNormal()
    {
        var zones = new SwingZones(0.95f, 0.4f, 0.6f);
        Assert.AreEqual(0.8f, zones.Center, 1e-5f);
        Assert.AreEqual(0.4f, zones.CriticalWidth, 1e-5f);
    }

    [Test]
    public void AFullCycleWithoutAPressIsAGlancingBlow()
    {
        var session = Session(swings: 2, jitter: 0f);
        Assert.IsFalse(session.Tick(0.9f));
        Assert.IsTrue(session.Tick(0.2f));
        Assert.AreEqual(1, session.SwingsDone);
        Assert.AreEqual(1, session.Glancing);
        Assert.AreEqual(0f, session.Position, 1e-5f);
    }

    [Test]
    public void PressAtTheTopOfTheBarCentreIsCritical()
    {
        var session = Session(swings: 3, jitter: 0f);
        session.Tick(0.25f); // marker at 0.5, the zone centre
        Assert.AreEqual(SwingResult.Critical, session.Press());
        Assert.AreEqual(1, session.Criticals);
        Assert.AreEqual(1, session.Combo);
    }

    [Test]
    public void ComboWidensTheCriticalZoneUpToTheCapAndResetsOnAMiss()
    {
        var session = Session(swings: 10, jitter: 0f, criticalWidth: 0.1f, perCombo: 0.02f, maxCombo: 2);
        for (int i = 0; i < 3; i++) session.Resolve(SwingResult.Critical);
        Assert.AreEqual(3, session.Combo);
        Assert.AreEqual(0.14f, session.Zones.CriticalWidth, 1e-5f);

        session.Resolve(SwingResult.Normal);
        Assert.AreEqual(0, session.Combo);
        Assert.AreEqual(0.1f, session.Zones.CriticalWidth, 1e-5f);
    }

    [Test]
    public void SessionCompletesAfterItsSwingsAndIgnoresMore()
    {
        var session = Session(swings: 2, jitter: 0f);
        session.Resolve(SwingResult.Critical);
        session.Resolve(SwingResult.Glancing);
        session.Resolve(SwingResult.Critical);
        Assert.IsTrue(session.IsComplete);
        Assert.AreEqual(2, session.SwingsDone);
        Assert.AreEqual(1, session.Criticals);
        Assert.IsFalse(session.Tick(5f));
    }

    [Test]
    public void BetterToolTiersWidenTheCriticalZoneCappedAtTheNormalZone()
    {
        var settings = ScriptableObject.CreateInstance<GatheringMinigameSettings>();
        created.Add(settings);
        GatheringToolProfile axe = settings.Profile(ToolType.Axe);
        float baseWidth = settings.CriticalWidthFor(axe, 1, 1);
        Assert.AreEqual(axe.CriticalZoneWidth, baseWidth, 1e-5f);
        Assert.AreEqual(baseWidth + settings.CriticalWidthPerTier * 2f, settings.CriticalWidthFor(axe, 3, 1), 1e-5f);
        Assert.AreEqual(axe.NormalZoneWidth, settings.CriticalWidthFor(axe, 100, 1), 1e-5f);
        Assert.IsTrue(settings.UsesMinigame(ToolType.Axe));
        Assert.IsTrue(settings.UsesMinigame(ToolType.Pickaxe));
        Assert.IsTrue(settings.NoiseRadius(SwingResult.Critical) < settings.NoiseRadius(SwingResult.Glancing));
    }

    [Test]
    public void TheSickleHasAQuickQuietProfileAndHandGatheringHasNone()
    {
        var settings = ScriptableObject.CreateInstance<GatheringMinigameSettings>();
        created.Add(settings);
        GatheringToolProfile sickle = settings.Profile(ToolType.Sickle);
        GatheringToolProfile axe = settings.Profile(ToolType.Axe);
        Assert.IsNotNull(sickle);
        Assert.Less(sickle.CycleSeconds, axe.CycleSeconds);
        Assert.Less(sickle.Loudness, axe.Loudness);
        Assert.IsNull(settings.Profile(ToolType.None));
        Assert.IsFalse(settings.UsesMinigame(ToolType.None));
    }

    [Test]
    public void LoudnessScalesAnnoyanceAndThePeakIsKeptAfterCalming()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        annoyance.Add(SwingResult.Normal, 0.5f);
        Assert.AreEqual(new ForestAnnoyanceSettings().AnnoyanceFor(SwingResult.Normal) * 0.5f, annoyance.Value, 1e-4f);
        annoyance.Add(SwingResult.Glancing);
        float peak = annoyance.Peak;
        annoyance.Tick(1000f);
        annoyance.Tick(1000f);
        Assert.AreEqual(0f, annoyance.Value, 1e-4f);
        Assert.AreEqual(peak, annoyance.Peak, 1e-4f);
    }

    [Test]
    public void ThePeakIsTheWrathThresholdOnceAWaveFires()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        for (int i = 0; i < 20; i++)
        {
            annoyance.Add(SwingResult.Glancing);
        }

        Assert.AreEqual(1f, annoyance.PeakFill, 1e-4f);
    }

    [Test]
    public void GatheringStatsCountCriticalRateAndKeepThePeak()
    {
        var stats = new GatheringStats();
        Assert.IsNull(RaidReportText.Gathering(stats));
        stats.Record(SwingResult.Critical);
        stats.Record(SwingResult.Normal);
        stats.Record(SwingResult.Glancing);
        stats.Record(SwingResult.Critical);
        Assert.AreEqual(4, stats.Swings);
        Assert.AreEqual(0.5f, stats.CriticalRate, 1e-5f);
        Assert.AreEqual(1, stats.Glancing);
        Assert.IsFalse(stats.HasAnnoyance);
        stats.RecordAnnoyance(0.6f, 0);
        stats.RecordAnnoyance(0.2f, 0);
        Assert.AreEqual(0.6f, stats.PeakAnnoyance, 1e-5f);
        StringAssert.Contains("50", RaidReportText.Gathering(stats));
        StringAssert.Contains("60", RaidReportText.Gathering(stats));
    }

    [Test]
    public void AnnoyanceClimbsThroughTheLevels()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        var levels = new List<AnnoyanceLevel>();
        annoyance.LevelChanged += levels.Add;

        annoyance.Add(SwingResult.Critical);
        Assert.AreEqual(2.5f, annoyance.Value, 1e-5f);
        annoyance.Add(30f);
        Assert.AreEqual(AnnoyanceLevel.Stirring, annoyance.Level);
        annoyance.Add(30f);
        Assert.AreEqual(AnnoyanceLevel.Angry, annoyance.Level);
        CollectionAssert.AreEqual(new[] { AnnoyanceLevel.Stirring, AnnoyanceLevel.Angry }, levels);
    }

    [Test]
    public void AnnoyanceDecaysOnlyAfterTheQuietDelay()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        annoyance.Add(40f);
        annoyance.Tick(4f);
        Assert.AreEqual(40f, annoyance.Value, 1e-5f);
        annoyance.Tick(2f);
        Assert.Less(annoyance.Value, 40f);

        annoyance.Add(SwingResult.Normal);
        float value = annoyance.Value;
        annoyance.Tick(1f);
        Assert.AreEqual(value, annoyance.Value, 1e-5f, "a swing restarts the quiet delay");
    }

    [Test]
    public void WrathFiresOnceThenAnnoyanceIsCapped()
    {
        var annoyance = new ForestAnnoyance(new ForestAnnoyanceSettings());
        int wraths = 0;
        annoyance.Wrath += () => wraths++;

        annoyance.Add(100f);
        Assert.AreEqual(1, wraths);
        Assert.AreEqual(annoyance.Settings.AngryAt, annoyance.Value, 1e-5f);
        Assert.AreEqual(AnnoyanceLevel.Angry, annoyance.Level);

        annoyance.Add(500f);
        Assert.AreEqual(1, wraths);
        Assert.AreEqual(annoyance.Settings.WrathAt, annoyance.Value, 1e-5f);
        Assert.AreEqual(1f, annoyance.Fill, 1e-5f);
    }

    [Test]
    public void BestToolIndexPicksTheHighestUnbrokenTool()
    {
        var container = new ItemContainer(4, 1);
        container.AddStack(new ItemStack(Tool(ToolType.Axe, 1), 1, 1f));
        container.AddStack(new ItemStack(Tool(ToolType.Axe, 3), 1, 0f));
        container.AddStack(new ItemStack(Tool(ToolType.Axe, 2), 1, 0.5f));
        container.AddStack(new ItemStack(Tool(ToolType.Pickaxe, 4), 1, 1f));

        int best = container.BestToolIndex(ToolType.Axe);
        Assert.AreEqual(2, container.GetSlot(best).Item.ToolTier);
        Assert.AreEqual(-1, container.BestToolIndex(ToolType.None));
    }

    private static GatheringSession Session(int swings, float jitter, float criticalWidth = 0.1f, float perCombo = 0f, int maxCombo = 0)
    {
        return new GatheringSession(swings, 1f, 0.4f, criticalWidth, perCombo, maxCombo, jitter, new System.Random(1));
    }

    private ItemDefinition Tool(ToolType type, int tier)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(item);
        item.name = type + " " + tier;
        var data = new SerializedObject(item);
        data.FindProperty("toolType").intValue = (int)type;
        data.FindProperty("toolTier").intValue = tier;
        data.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }
}
