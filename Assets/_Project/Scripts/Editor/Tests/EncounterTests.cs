using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Boss encounters (Docs/BossEncounterResearch_2026-11-17.md, option C, with the user's decisions): a rematch waits a day after a
/// kill (and never longer), the first fight is at the encounter's own tier, every encounter has its boss and that tier, the ways
/// home an arena holds shut or closes behave, the launcher refuses a rematch too soon, and what a character has done in encounters
/// survives a save. Co-op: the arena and When Boss know the boss by its definition, so a friend's copy of the host's stands in for
/// the one placed (removed on a friend's game).
/// </summary>
public sealed class EncounterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

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

    [Test]
    public void ThePlanner_HidesAnEncounterUntilItsStoryOpensIt_AndOffersNoSparks()
    {
        BossEncounterDefinition encounter = Encounter();
        RaidDefinition raid = Raid(encounter, 2);
        RaidDefinition woods = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        Assert.AreEqual(2, RaidPlan.Shown(new[] { woods, raid }).Count, "open: shown");

        QuestDefinition quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(FirstQuestPath());
        if (GameSession.Quests.StatusOf(quest) != QuestStatus.NotStarted)
        {
            Assert.Ignore("the first quest is already under way in a new game");
        }

        var so = new SerializedObject(encounter);
        so.FindProperty("opensWhen.quest").objectReferenceValue = quest;
        so.ApplyModifiedPropertiesWithoutUndo();
        IReadOnlyList<RaidDefinition> shown = RaidPlan.Shown(new[] { woods, raid });
        Assert.AreEqual(Cheats.NoRequirements ? 2 : 1, shown.Count, "not yet opened by the story: hidden");
    }

    [Test]
    public void WhenBoss_SaysWhatIsMissing()
    {
        StringAssert.Contains("boss", new WhenBoss().FindProblem());
    }

    [Test]
    public void ARematch_NeverWaitsLongerThanItsHours_HoweverFarAheadTheKill()
    {
        BossEncounterDefinition encounter = Encounter();
        var now = new DateTime(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);
        Assert.AreEqual(24.0, encounter.RematchWait(now.AddDays(3), now).TotalHours, 1e-6, "a kill stamped ahead of now (a clock set back)");
        Assert.AreEqual(24.0, encounter.RematchWait(DateTime.MaxValue, now).TotalHours, 1e-6, "and no overflow at the end of time");
    }

    [Test]
    public void AKillStampedAheadOfNow_IsDroppedOnLoading()
    {
        var progress = new CharacterProgress();
        DateTime now = DateTime.UtcNow;
        progress.RecordKill("ahead", now.AddHours(5));
        progress.RecordKill("behind", now.AddHours(-5));

        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, DateTime.UtcNow, null, new QuestLog(), new SettlementState(), null, progress);
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        CharacterProgress loaded = SaveMapper.FromSaveData(data, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName)).Progress;

        Assert.IsNull(loaded.LastKill("ahead"), "it would hold the rematch shut by a wait no kill earned");
        Assert.AreEqual(now.AddHours(-5), loaded.LastKill("behind"));
    }

    [Test]
    public void AnEncounter_SaysWhenItHasNoBossOrItsRaidLacksTheFirstTier()
    {
        BossEncounterDefinition encounter = Encounter(firstTier: 2);
        StringAssert.Contains("boss", encounter.Problem(Raid(encounter, 2, 3)));

        var so = new SerializedObject(encounter);
        so.FindProperty("boss").objectReferenceValue = BossKind();
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsNull(encounter.Problem(Raid(encounter, 2, 3)));

        RaidDefinition lacking = Raid(encounter, 3, 4);
        StringAssert.Contains("tier II", encounter.Problem(lacking));
        Assert.AreEqual(4, encounter.DifficultyFor(lacking, false, lacking.Difficulties[1]).tier, "the launch still falls back to the tier picked");
    }

    [Test]
    public void EveryEncounterRaid_HasItsBossAndItsFirstTier()
    {
        var problems = new List<string>();
        int encounters = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (raid == null || !raid.IsEncounter)
            {
                continue;
            }

            encounters++;
            string problem = raid.Encounter.Problem(raid);
            if (problem != null)
            {
                problems.Add($"{raid.name}: {problem}");
            }
        }

        Assert.Greater(encounters, 0, "the encounters were found");
        CollectionAssert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void TheBossEvents_AreClearedWhenPlayStarts()
    {
        Action<BossEncounter> heard = _ => { };
        Action<BossEncounter, int> heardPhase = (boss, phase) => { };
        BossEncounter.Appeared += heard;
        BossEncounter.AnyPhaseEntered += heardPhase;
        BossEncounter.AnyFell += heard;
        try
        {
            typeof(BossEncounter).GetMethod("ResetForNewPlaySession", PrivateStatic).Invoke(null, null);
            foreach (string name in new[] { nameof(BossEncounter.Appeared), nameof(BossEncounter.AnyPhaseEntered), nameof(BossEncounter.AnyFell) })
            {
                Assert.IsNull(typeof(BossEncounter).GetField(name, PrivateStatic).GetValue(null), $"{name} keeps the last session's listeners");
            }
        }
        finally
        {
            BossEncounter.Appeared -= heard;
            BossEncounter.AnyPhaseEntered -= heardPhase;
            BossEncounter.AnyFell -= heard;
        }
    }

    // Co-op: on a friend's game the boss placed in the scene is removed as it wakes, and the host's comes later as a copy.
    [Test]
    public void WhenBoss_HearsItsBossByDefinition_EvenOnceThePlacedOneIsGone()
    {
        BossDefinition kind = BossKind();
        BossEncounter placed = Boss("Placed Boss", kind);
        BossEncounter copy = Boss("The host's copy", kind);
        BossEncounter other = Boss("Another boss", BossKind());
        var falls = new WhenBoss { boss = placed, moment = WhenBoss.Moment.Falls };
        var reaches = new WhenBoss { boss = placed, moment = WhenBoss.Moment.ReachesPhase, phase = 2 };
        Object.DestroyImmediate(placed.gameObject);

        int fell = 0;
        int reached = 0;
        Action stopFalls = falls.Listen(null, () => fell++);
        Action stopReaches = reaches.Listen(null, () => reached++);
        Assert.IsNotNull(stopFalls, "it still knows which boss it waits for");
        Assert.IsNotNull(stopReaches);
        try
        {
            RaiseFell(other);
            RaisePhase(other, 2);
            Assert.AreEqual(0, fell + reached, "another boss isn't this one");

            RaisePhase(copy, 1);
            Assert.AreEqual(0, reached, "not the phase it waits for");
            RaisePhase(copy, 2);
            RaiseFell(copy);
            Assert.AreEqual(1, reached, "the copy's phase");
            Assert.AreEqual(1, fell, "the copy's fall");
        }
        finally
        {
            stopFalls();
            stopReaches();
        }

        RaiseFell(copy);
        Assert.AreEqual(1, fell, "no longer listening");
    }

    [Test]
    public void AnArena_TakesItsBossInByDefinition_HoldsItBeforeTheGate_AndGivesALateOneTheRageSoFar()
    {
        BossDefinition kind = BossKind();
        BossEncounter placed = Boss("Placed Boss", kind);
        BossEncounter copy = Boss("The host's copy", kind);
        BossEncounter other = Boss("Another boss", BossKind());
        var arena = new GameObject("Arena").AddComponent<EncounterArena>();
        created.Add(arena.gameObject);
        var so = new SerializedObject(arena);
        so.FindProperty("boss").objectReferenceValue = placed;
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(EncounterArena).GetMethod("Awake", Private).Invoke(arena, null); // outside Play nothing wakes
        try
        {
            Object.DestroyImmediate(placed.gameObject);
            arena.Adopt(other);
            Assert.IsNull(arena.Boss, "another boss isn't this arena's");

            arena.Adopt(copy);
            Assert.AreSame(copy, arena.Boss, "the host's copy stands in for the one placed");
            Assert.IsTrue(IsHeld(copy), "before the gate it waits");
            arena.Adopt(copy);
            Assert.AreSame(copy, arena.Boss, "taken in once");

            // A copy made after the gate has sealed and the rage has begun: it wakes, as strong as the clock has made the boss.
            BossEncounter late = Boss("A copy made late", kind);
            typeof(EncounterArena).GetField("sealedAt", Private).SetValue(arena, 1f);
            typeof(EncounterArena).GetField("rageAdded", Private).SetValue(arena, 0.65f);
            Combatant combatant = late.GetComponent<Combatant>();
            float before = combatant.Stats.Get(StatType.DamageBonus);
            late.Hold();
            arena.Adopt(late);
            Assert.AreSame(late, arena.Boss);
            Assert.IsFalse(IsHeld(late), "past the gate it wakes");
            Assert.AreEqual(before + 0.65f, combatant.Stats.Get(StatType.DamageBonus), 1e-4f, "with the rage added so far");
        }
        finally
        {
            typeof(EncounterArena).GetMethod("OnDestroy", Private).Invoke(arena, null);
        }
    }

    private BossDefinition BossKind()
    {
        var kind = ScriptableObject.CreateInstance<BossDefinition>();
        created.Add(kind);
        return kind;
    }

    // A boss of a kind, readied as its Awake would (outside Play nothing wakes).
    private BossEncounter Boss(string name, BossDefinition kind)
    {
        var body = new GameObject(name);
        created.Add(body);
        BossEncounter boss = body.AddComponent<BossEncounter>();
        var so = new SerializedObject(boss);
        so.FindProperty("definition").objectReferenceValue = kind;
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(BossEncounter).GetMethod("Awake", Private).Invoke(boss, null);
        return boss;
    }

    private static bool IsHeld(BossEncounter boss) => (bool)typeof(BossEncounter).GetField("held", Private).GetValue(boss);

    private static void RaiseFell(BossEncounter boss) =>
        (typeof(BossEncounter).GetField(nameof(BossEncounter.AnyFell), PrivateStatic).GetValue(null) as Action<BossEncounter>)?.Invoke(boss);

    private static void RaisePhase(BossEncounter boss, int phase) =>
        (typeof(BossEncounter).GetField(nameof(BossEncounter.AnyPhaseEntered), PrivateStatic).GetValue(null) as Action<BossEncounter, int>)?.Invoke(boss, phase);

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
