using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Boss encounters (Docs/BossEncounterResearch_2026-11-17.md, option C, with the user's decisions): a rematch waits a day after a
/// kill, the first fight is at the encounter's own tier, the ways home an arena holds shut or closes behave, the launcher refuses
/// a rematch too soon, and what a character has done in encounters survives a save.
/// </summary>
public sealed class EncounterTests
{
    private readonly List<Object> created = new List<Object>();

    [SetUp]
    public void SetUp() => GameSession.StartNewGame();

    [TearDown]
    public void TearDown()
    {
        foreach (Object made in created)
        {
            if (made != null)
            {
                Object.DestroyImmediate(made);
            }
        }

        created.Clear();
        GameSession.StartNewGame();
    }

    private BossEncounterDefinition Encounter(float rematchHours = 24f, int firstTier = 2)
    {
        var encounter = ScriptableObject.CreateInstance<BossEncounterDefinition>();
        created.Add(encounter);
        var so = new SerializedObject(encounter);
        so.FindProperty("rematchHours").floatValue = rematchHours;
        so.FindProperty("firstTier").intValue = firstTier;
        so.ApplyModifiedPropertiesWithoutUndo();
        return encounter;
    }

    private RaidDefinition Raid(BossEncounterDefinition encounter, params int[] tiers)
    {
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        created.Add(raid);
        var so = new SerializedObject(raid);
        so.FindProperty("id").stringValue = "test_encounter";
        so.FindProperty("encounter").objectReferenceValue = encounter;
        SerializedProperty list = so.FindProperty("difficulties");
        list.ClearArray();
        foreach (int tier in tiers)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).FindPropertyRelative("tier").intValue = tier;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return raid;
    }

    [Test]
    public void ARematch_WaitsADayAfterAKill_AndNotBeforeAnyKill()
    {
        BossEncounterDefinition encounter = Encounter();
        var kill = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(TimeSpan.Zero, encounter.RematchWait(null, kill), "a boss never brought down can be fought");
        Assert.AreEqual(24.0, encounter.RematchWait(kill, kill).TotalHours, 1e-6);
        Assert.AreEqual(6.0, encounter.RematchWait(kill, kill.AddHours(18)).TotalHours, 1e-6);
        Assert.AreEqual(TimeSpan.Zero, encounter.RematchWait(kill, kill.AddHours(24)));
        Assert.AreEqual(TimeSpan.Zero, encounter.RematchWait(kill, kill.AddDays(3)));
        Assert.AreEqual(TimeSpan.Zero, Encounter(0f).RematchWait(kill, kill), "Rematch Hours 0: at once");
    }

    [Test]
    public void AWait_IsSaidInHoursAndMinutes()
    {
        Assert.AreEqual("18 h 20 min", BossEncounterDefinition.Describe(TimeSpan.FromMinutes(18 * 60 + 20)));
        Assert.AreEqual("40 min", BossEncounterDefinition.Describe(TimeSpan.FromMinutes(40)));
        Assert.AreEqual("1 min", BossEncounterDefinition.Describe(TimeSpan.FromSeconds(5)));
        Assert.AreEqual("2 h", BossEncounterDefinition.Describe(TimeSpan.FromMinutes(119.5)));
    }

    [Test]
    public void TheFirstFight_IsAtTheEncountersTier_RematchesAtTheChosenOne()
    {
        BossEncounterDefinition encounter = Encounter(firstTier: 2);
        RaidDefinition raid = Raid(encounter, 2, 3, 4);
        RaidDifficulty chosen = raid.Difficulties[2];
        Assert.AreEqual(2, encounter.DifficultyFor(raid, false, chosen).tier, "the first fight is at its own tier");
        Assert.AreEqual(4, encounter.DifficultyFor(raid, true, chosen).tier, "a rematch is at the tier chosen");
        Assert.IsTrue(raid.IsEncounter);
    }

    [Test]
    public void TheLauncher_RefusesARematchTooSoon_AndAllowsItADayLater()
    {
        RaidDefinition raid = Raid(Encounter(), 2);
        var now = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        Assert.IsNull(RaidLauncher.CheckRematch(raid, now), "never fought: open");
        GameSession.Progress.RecordKill(raid.Id, now.AddHours(-2));
        StringAssert.Contains("22 h", RaidLauncher.CheckRematch(raid, now));
        Assert.IsNull(RaidLauncher.CheckRematch(raid, now.AddHours(22.01)));
    }

    [Test]
    public void AnArena_HoldsItsWayHomeShut_UntilReleased_AndClosesTheArrivalStoneForGood()
    {
        var wayHome = new GameObject("Way Home", typeof(Interactable), typeof(ExtractionPoint)).GetComponent<ExtractionPoint>();
        var arrival = new GameObject("Arrival Stone", typeof(Interactable), typeof(ExtractionPoint)).GetComponent<ExtractionPoint>();
        created.Add(wayHome.gameObject);
        created.Add(arrival.gameObject);
        Assert.IsTrue(wayHome.IsOpen, "outside a held raid a way home is open");

        wayHome.HoldShut("The way home opens when the Brukam falls.");
        Assert.AreEqual(ExtractionStatus.NotYetOpen, wayHome.Status);
        Assert.AreEqual(-1f, wayHome.SecondsUntilChange, "held shut: no time to it");
        Assert.IsFalse(wayHome.CanInteract(null, out string reason));
        Assert.AreEqual("The way home opens when the Brukam falls.", reason);
        wayHome.Release();
        Assert.IsTrue(wayHome.IsOpen);

        arrival.Close();
        Assert.AreEqual(ExtractionStatus.Closed, arrival.Status);
        arrival.Release();
        Assert.AreEqual(ExtractionStatus.Closed, arrival.Status, "closed stays closed");
    }

    [Test]
    public void GatesReachedAndKills_SurviveASave()
    {
        var progress = new CharacterProgress();
        var kill = new DateTime(2026, 11, 17, 9, 30, 0, DateTimeKind.Utc);
        Assert.IsTrue(progress.MarkGateReached("brukams_bridge"));
        Assert.IsFalse(progress.MarkGateReached("brukams_bridge"), "only the first time is new");
        progress.RecordKill("brukams_bridge", kill);

        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, DateTime.UtcNow, null, new QuestLog(), new SettlementState(), null, progress);
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        CharacterProgress loaded = SaveMapper.FromSaveData(data, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName)).Progress;

        Assert.IsTrue(loaded.HasReachedGate("brukams_bridge"));
        Assert.IsFalse(loaded.HasReachedGate("the_heartroot"));
        Assert.AreEqual(kill, loaded.LastKill("brukams_bridge"));
        Assert.AreEqual(DateTimeKind.Utc, loaded.LastKill("brukams_bridge").Value.Kind);
        Assert.IsNull(loaded.LastKill("the_heartroot"));
    }

    [Test]
    public void AnEncounter_IsLockedUntilItsStoryOpensIt_AndAnExpeditionIsNotAnEncounter()
    {
        BossEncounterDefinition encounter = Encounter();
        RaidDefinition raid = Raid(encounter, 2);
        Assert.IsFalse(raid.IsLocked, "no quest named: open with the raid");

        QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(FirstQuestPath());
        var so = new SerializedObject(encounter);
        so.FindProperty("opensWhen.quest").objectReferenceValue = quest;
        so.FindProperty("opensWhen.requirement").enumValueIndex = (int)QuestGate.Requirement.Started;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.AreEqual(GameSession.Quests.StatusOf(quest) == QuestStatus.NotStarted, raid.IsLocked, "locked until its quest has begun");

        RaidDefinition woods = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        Assert.IsFalse(woods.IsEncounter);
    }

    private static string FirstQuestPath()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition", new[] { "Assets/_Project/Data" }))
        {
            return AssetDatabase.GUIDToAssetPath(guid);
        }

        Assert.Fail("no quests in the project");
        return null;
    }
}
