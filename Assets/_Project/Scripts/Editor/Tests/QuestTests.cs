using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Edit Mode tests for quest objectives, the quest log, chains and saving quest progress.</summary>
public sealed class QuestTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    // ----- Objectives -----

    [Test]
    public void Objectives_MatchTheirEventsAndTargets()
    {
        var ore = Create<ResourceDefinition>("ore");
        var wood = Create<ResourceDefinition>("wood");
        var brute = Create<MonsterDefinition>("brute");
        var ravager = Create<MonsterDefinition>("ravager");

        QuestObjective gather = QuestObjective.Create(ObjectiveType.GatherResource, 10, ore);
        Assert.AreEqual(3, gather.Progress(new GameEvent(GameEventKind.ResourceGathered, ore, 3)));
        Assert.AreEqual(0, gather.Progress(new GameEvent(GameEventKind.ResourceGathered, wood, 3)));
        Assert.AreEqual(0, gather.Progress(new GameEvent(GameEventKind.ItemObtained, ore, 3)), "Wrong kind of event.");

        QuestObjective killBrute = QuestObjective.Create(ObjectiveType.KillMonster, 1, brute);
        QuestObjective killAny = QuestObjective.Create(ObjectiveType.KillMonster, 5);
        Assert.AreEqual(1, killBrute.Progress(Kill(brute)));
        Assert.AreEqual(0, killBrute.Progress(Kill(ravager)));
        Assert.AreEqual(1, killAny.Progress(Kill(ravager)), "An empty monster counts any enemy kill.");
        Assert.AreEqual("Kill 5 enemies", killAny.Describe());

        // Kills count by the side that fell: the player, a companion or a traveller falling is no enemy kill.
        Assert.AreEqual(0, killAny.Progress(Kill(null, Faction.Player)));
        Assert.AreEqual(0, killAny.Progress(Kill(null, Faction.Ally)));
        Assert.AreEqual(1, killAny.Progress(Kill(null, Faction.Enemy)));
        Assert.AreEqual(0, killAny.Progress(Kill(null)), "A kill of unknown side counts for no side.");
        QuestObjective killAllies = QuestObjective.Create(ObjectiveType.KillMonster, 2, faction: Faction.Ally);
        Assert.AreEqual(1, killAllies.Progress(Kill(null, Faction.Ally)));
        Assert.AreEqual("Kill 2 allies", killAllies.Describe());
    }

    [Test]
    public void Objectives_CountWhatThePlayersSideDid()
    {
        var brute = Create<MonsterDefinition>("brute");
        QuestObjective kill = QuestObjective.Create(ObjectiveType.KillMonster, 1, brute);
        Assert.AreEqual(1, kill.Progress(new GameEvent(GameEventKind.Killed, brute, 1, Fighter(Faction.Player))));
        Assert.AreEqual(1, kill.Progress(new GameEvent(GameEventKind.Killed, brute, 1, Fighter(Faction.Ally))), "A companion's kill counts.");
        Assert.AreEqual(0, kill.Progress(new GameEvent(GameEventKind.Killed, brute, 1, Fighter(Faction.Enemy))), "Another monster's kill doesn't.");
        Assert.AreEqual(0, kill.Progress(new GameEvent(GameEventKind.Killed, brute, 1, Created(new GameObject("Bystander")))));
        Assert.AreEqual(0, kill.Progress(new GameEvent(GameEventKind.Killed, brute)), "A death nobody caused is no one's kill.");

        var ore = Create<ResourceDefinition>("ore");
        QuestObjective gather = QuestObjective.Create(ObjectiveType.GatherResource, 5, ore);
        Assert.AreEqual(2, gather.Progress(new GameEvent(GameEventKind.ResourceGathered, ore, 2)), "Nobody in particular: the world, for the player.");
    }

    [Test]
    public void RaidObjectives_RespectPlaceExtractionAndTime()
    {
        var wilds = Create<RaidDefinition>("wilds");
        var caves = Create<RaidDefinition>("caves");
        QuestObjective objective = QuestObjective.Create(ObjectiveType.CompleteRaids, 2, wilds, mustExtract: true, withinMinutes: 5f);

        Assert.AreEqual(1, objective.Progress(Raid(wilds, RaidOutcome.Extracted, 200f)));
        Assert.AreEqual(0, objective.Progress(Raid(caves, RaidOutcome.Extracted, 200f)), "Wrong place.");
        Assert.AreEqual(0, objective.Progress(Raid(wilds, RaidOutcome.Died, 200f)), "Didn't extract.");
        Assert.AreEqual(0, objective.Progress(Raid(wilds, RaidOutcome.Extracted, 400f)), "Too slow.");
        Assert.AreEqual("Extract from 2 raids in wilds within 5:00", objective.Describe());
    }

    // ----- Quest log -----

    [Test]
    public void Log_CompletesAQuestWhenEveryObjectiveIsMet()
    {
        var ore = Create<ResourceDefinition>("ore");
        var brute = Create<MonsterDefinition>("brute");
        QuestDefinition quest = Quest("q", QuestObjective.Create(ObjectiveType.GatherResource, 5, ore), QuestObjective.Create(ObjectiveType.KillMonster, 1, brute));
        var log = new QuestLog();
        var completed = new List<QuestDefinition>();
        log.Completed += completed.Add;

        Assert.IsTrue(log.TryStart(quest));
        log.Handle(new GameEvent(GameEventKind.ResourceGathered, ore, 3));
        log.Handle(new GameEvent(GameEventKind.ResourceGathered, ore, 4));
        Assert.AreEqual(5, log.Get(quest).Counts[0], "Progress stops at the amount needed.");
        Assert.AreEqual(QuestStatus.Active, log.StatusOf(quest));

        log.Handle(Kill(brute));
        Assert.AreEqual(QuestStatus.Completed, log.StatusOf(quest));
        CollectionAssert.AreEqual(new[] { quest }, completed);
        Assert.IsFalse(log.TryStart(quest), "A completed quest doesn't start again.");
    }

    [Test]
    public void Log_OrderedObjectives_OnlyCountTheCurrentStep()
    {
        var ore = Create<ResourceDefinition>("ore");
        var brute = Create<MonsterDefinition>("brute");
        QuestDefinition quest = Quest("q", QuestObjective.Create(ObjectiveType.GatherResource, 2, ore), QuestObjective.Create(ObjectiveType.KillMonster, 1, brute));
        SetField(quest, "objectivesInOrder", true);
        var log = new QuestLog();
        log.TryStart(quest);

        log.Handle(Kill(brute));
        Assert.AreEqual(0, log.Get(quest).Counts[1], "The kill doesn't count before the ore is gathered.");

        log.Handle(new GameEvent(GameEventKind.ResourceGathered, ore, 2));
        log.Handle(Kill(brute));
        Assert.AreEqual(QuestStatus.Completed, log.StatusOf(quest));
    }

    [Test]
    public void Chains_StartOnlyAfterTheirPrerequisites()
    {
        var gameEvent = Create<GameEventDefinition>("reached_tower");
        QuestDefinition first = Quest("first", QuestObjective.Create(ObjectiveType.QuestEvent, 1, gameEvent));
        QuestDefinition second = Quest("second", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        SetField(second, "prerequisites", new List<QuestDefinition> { first });
        var log = new QuestLog();

        Assert.IsFalse(log.TryStart(second));
        log.TryStart(first);
        log.Handle(new GameEvent(GameEventKind.Custom, gameEvent));
        Assert.AreEqual(QuestStatus.Completed, log.StatusOf(first));
        Assert.IsTrue(log.TryStart(second));
    }

    [Test]
    public void Quests_ReportPrerequisiteLoops()
    {
        QuestDefinition a = Quest("a", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        QuestDefinition b = Quest("b", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        SetField(a, "prerequisites", new List<QuestDefinition> { b });
        SetField(b, "prerequisites", new List<QuestDefinition> { a });

        CollectionAssert.Contains(a.FindProblems(), "Its prerequisites lead back to itself, so it can never start.");
    }

    [Test]
    public void Progress_SurvivesSavingAndLoading()
    {
        var ore = Create<ResourceDefinition>("ore");
        QuestDefinition active = Quest("active", QuestObjective.Create(ObjectiveType.GatherResource, 10, ore));
        QuestDefinition done = Quest("done", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        var log = new QuestLog();
        log.TryStart(active);
        log.Handle(new GameEvent(GameEventKind.ResourceGathered, ore, 4));
        log.TryStart(done);
        log.Handle(Kill(null, Faction.Enemy));
        var lookup = new Lookup { Quests = { ["active"] = active, ["done"] = done } };

        SaveData data = SaveMapper.ToSaveData(null, null, "SampleScene", null, DateTime.UtcNow, null, log);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);

        Assert.AreEqual(QuestStatus.Active, loaded.Quests.StatusOf(active));
        Assert.AreEqual(4, loaded.Quests.Get(active).Counts[0]);
        Assert.AreEqual(QuestStatus.Completed, loaded.Quests.StatusOf(done));
    }

    [Test]
    public void QuestEvents_AreRememberedAcrossSaves()
    {
        var bloomed = Create<GameEventDefinition>("heart_tree_bloomed");
        var other = Create<GameEventDefinition>("other");
        var log = new QuestLog();
        Assert.IsFalse(log.HasHappened(bloomed));

        log.Handle(new GameEvent(GameEventKind.Custom, bloomed));
        Assert.IsTrue(log.HasHappened(bloomed), "Raised even with no quest waiting for it.");
        Assert.IsFalse(log.HasHappened(other));

        SaveData data = SaveMapper.ToSaveData(null, null, "SampleScene", null, DateTime.UtcNow, null, log);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), new Lookup());
        Assert.IsTrue(loaded.Quests.HasHappened(bloomed));
        Assert.IsFalse(loaded.Quests.HasHappened(other));
    }

    [Test]
    public void QuestConditions_CheckStartedOrCompleted()
    {
        QuestDefinition quest = Quest("first_raid", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        var started = new QuestCondition(quest, QuestGate.Requirement.Started);
        var completed = new QuestCondition(quest, QuestGate.Requirement.Completed);
        var log = new QuestLog();

        Assert.IsTrue(default(QuestCondition).IsMetBy(log), "No quest picked means no condition.");
        Assert.IsFalse(started.IsMetBy(log));
        log.TryStart(quest);
        Assert.IsTrue(started.IsMetBy(log));
        Assert.IsFalse(completed.IsMetBy(log));
        log.Handle(Kill(null, Faction.Enemy));
        Assert.IsTrue(completed.IsMetBy(log));
    }

    // ----- Turn-in and quest givers -----

    [Test]
    public void TurnIn_WaitsForTheGiverBeforeCompleting()
    {
        QuestDefinition quest = Quest("q", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        SetField(quest, "turnInToGiver", true);
        var log = new QuestLog();
        var completed = new List<QuestDefinition>();
        var ready = new List<QuestDefinition>();
        log.Completed += completed.Add;
        log.ReadyToTurnIn += ready.Add;

        Assert.IsFalse(log.TryTurnIn(quest), "Nothing to hand in yet.");
        log.TryStart(quest);
        log.Handle(Kill(null, Faction.Enemy));
        Assert.AreEqual(QuestStatus.ReadyToTurnIn, log.StatusOf(quest));
        CollectionAssert.AreEqual(new[] { quest }, ready);
        CollectionAssert.IsEmpty(completed, "No rewards until it is handed in.");

        Assert.IsTrue(log.TryTurnIn(quest));
        Assert.AreEqual(QuestStatus.Completed, log.StatusOf(quest));
        CollectionAssert.AreEqual(new[] { quest }, completed);
    }

    [Test]
    public void ForceComplete_SkipsTheHandIn()
    {
        QuestDefinition quest = Quest("q", QuestObjective.Create(ObjectiveType.KillMonster, 3));
        SetField(quest, "turnInToGiver", true);
        var log = new QuestLog();
        log.TryStart(quest);
        log.ForceComplete(quest);
        Assert.AreEqual(QuestStatus.Completed, log.StatusOf(quest));
    }

    [Test]
    public void TurnIn_ReadyStateSurvivesSavingAndLoading()
    {
        QuestDefinition quest = Quest("ready", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        SetField(quest, "turnInToGiver", true);
        var log = new QuestLog();
        log.TryStart(quest);
        log.Handle(Kill(null, Faction.Enemy));
        var lookup = new Lookup { Quests = { ["ready"] = quest } };

        SaveData data = SaveMapper.ToSaveData(null, null, "SampleScene", null, DateTime.UtcNow, null, log);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), lookup);
        Assert.AreEqual(QuestStatus.ReadyToTurnIn, loaded.Quests.StatusOf(quest));
    }

    [Test]
    public void QuestGiver_StateFollowsTheQuest()
    {
        QuestDefinition first = Quest("first", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        QuestDefinition given = Quest("given", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        SetField(given, "start", QuestStart.ByQuestGiver);
        SetField(given, "turnInToGiver", true);
        SetField(given, "prerequisites", new List<QuestDefinition> { first });
        QuestDefinition automatic = Quest("auto", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        var log = new QuestLog();

        Assert.AreEqual(QuestGiverState.None, QuestGiver.StateFor(null, log));
        Assert.AreEqual(QuestGiverState.None, QuestGiver.StateFor(automatic, log), "Givers don't offer quests that start by themselves.");
        Assert.AreEqual(QuestGiverState.NotYet, QuestGiver.StateFor(given, log));
        log.ForceComplete(first);
        Assert.AreEqual(QuestGiverState.Available, QuestGiver.StateFor(given, log));
        log.TryStart(given);
        Assert.AreEqual(QuestGiverState.InProgress, QuestGiver.StateFor(given, log));
        log.Handle(Kill(null, Faction.Enemy));
        Assert.AreEqual(QuestGiverState.ReadyToTurnIn, QuestGiver.StateFor(given, log));
        log.TryTurnIn(given);
        Assert.AreEqual(QuestGiverState.Done, QuestGiver.StateFor(given, log));
    }

    [Test]
    public void SceneIndex_FindsGiversTriggersAndOverrides()
    {
        const string giverScript = "11111111111111111111111111111111";
        const string triggerScript = "22222222222222222222222222222222";
        const string quest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string questEvent = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string yaml = "%YAML 1.1\n" +
                            "--- !u!1 &100\nGameObject:\n  m_Name: Smith\n" +
                            "--- !u!114 &101\nMonoBehaviour:\n  m_GameObject: {fileID: 100}\n" +
                            "  m_Script: {fileID: 11500000, guid: 11111111111111111111111111111111, type: 3}\n" +
                            "  quest: {fileID: 11400000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 2}\n" +
                            "--- !u!1 &200\nGameObject:\n  m_Name: Tower Zone\n" +
                            "--- !u!114 &201\nMonoBehaviour:\n  m_GameObject: {fileID: 200}\n" +
                            "  m_Script: {fileID: 11500000, guid: 22222222222222222222222222222222, type: 3}\n" +
                            "  gameEvent: {fileID: 11400000, guid: bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, type: 2}\n" +
                            "  onlyDuringQuest: {fileID: 11400000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 2}\n" +
                            "  unrelated: {fileID: 11400000, guid: cccccccccccccccccccccccccccccccc, type: 2}\n" +
                            "--- !u!1001 &300\nPrefabInstance:\n  m_Modification:\n    m_Modifications:\n" +
                            "    - target: {fileID: 5, guid: dddddddddddddddddddddddddddddddd, type: 3}\n" +
                            "      propertyPath: quest\n      value: \n" +
                            "      objectReference: {fileID: 11400000, guid: aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, type: 2}\n";
        var scripts = new Dictionary<string, Type> { [giverScript] = typeof(QuestGiver), [triggerScript] = typeof(GameEventTrigger) };

        List<QuestSceneUse> uses = QuestSceneIndex.Parse(yaml, "Assets/Test.unity", scripts, new HashSet<string> { quest, questEvent });

        Assert.AreEqual(4, uses.Count);
        Assert.IsTrue(uses.Exists(u => u.Role == QuestSceneRole.Gives && u.TargetGuid == quest && u.ObjectName == "Smith" && u.ObjectFileId == 100));
        Assert.IsTrue(uses.Exists(u => u.Role == QuestSceneRole.Raises && u.TargetGuid == questEvent && u.ObjectName == "Tower Zone"));
        Assert.IsTrue(uses.Exists(u => u.Role == QuestSceneRole.LimitedTo && u.TargetGuid == quest));
        Assert.IsTrue(uses.Exists(u => u.Role == QuestSceneRole.PrefabOverride && u.TargetGuid == quest));
    }

    [Test]
    public void SceneIndex_JobAssetRaisesItsAssignedEvent()
    {
        const string questEvent = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        const string jobScript = "ffffffffffffffffffffffffffffffff";
        string yaml = "%YAML 1.1\n--- !u!114 &11400000\nMonoBehaviour:\n  m_GameObject: {fileID: 0}\n"
            + $"  m_Script: {{fileID: 11500000, guid: {jobScript}, type: 3}}\n  m_Name: Smith Job\n"
            + $"  assignedEvent: {{fileID: 11400000, guid: {questEvent}, type: 2}}\n";
        var scripts = new Dictionary<string, Type> { [jobScript] = typeof(JobDefinition) };

        List<QuestSceneUse> uses = QuestSceneIndex.Parse(yaml, "Assets/Jobs/Smith Job.asset", scripts, new HashSet<string> { questEvent });

        Assert.AreEqual(1, uses.Count);
        Assert.AreEqual(QuestSceneRole.Raises, uses[0].Role);
        Assert.IsTrue(uses[0].IsDataAsset);
        Assert.AreEqual("Smith Job  (asset)", uses[0].Describe());
    }

    [Test]
    public void BuildingObject_ActiveRegistryCountsAndFinds()
    {
        var hut = ScriptableObject.CreateInstance<BuildingDefinition>();
        var first = new GameObject("Hut A").AddComponent<BuildingObject>();
        var second = new GameObject("Hut B").AddComponent<BuildingObject>();
        // Edit Mode doesn't run OnEnable/OnDisable on plain MonoBehaviours, so the test drives them.
        void Call(BuildingObject building, string method) =>
            typeof(BuildingObject).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(building, null);
        try
        {
            foreach (BuildingObject building in new[] { first, second })
            {
                var data = new UnityEditor.SerializedObject(building);
                data.FindProperty("definition").objectReferenceValue = hut;
                data.ApplyModifiedPropertiesWithoutUndo();
                Call(building, "OnEnable");
            }

            Assert.AreEqual(2, BuildingObject.Count(hut));
            Assert.IsNotNull(BuildingObject.Find(hut));
            Call(second, "OnDisable");
            Assert.AreEqual(1, BuildingObject.Count(hut), "Disabled buildings leave the registry.");
            Call(first, "OnDisable");
        }
        finally
        {
            Object.DestroyImmediate(first.gameObject);
            Object.DestroyImmediate(second.gameObject);
            Object.DestroyImmediate(hut);
        }
    }

    // ----- Dialogue lines and the Quest Map -----

    [Test]
    public void DialogueLines_InOrderCyclesAndSkipsBlanks()
    {
        var lines = new DialogueLines(LineOrder.InOrder, "a", " ", "b");
        Assert.AreEqual(new[] { "a", "b", "a" }, new[] { lines.Advance(), lines.Advance(), lines.Advance() });
        Assert.AreEqual("a", lines.Current);
    }

    [Test]
    public void DialogueLines_ThenLastSticksAndEmptyHasNone()
    {
        var lines = new DialogueLines(LineOrder.InOrderThenLast, "a", "b");
        Assert.AreEqual(new[] { "a", "b", "b" }, new[] { lines.Advance(), lines.Advance(), lines.Advance() });
        var empty = new DialogueLines(LineOrder.InOrder, "", null);
        Assert.IsFalse(empty.HasAny);
        Assert.IsNull(empty.Advance());
    }

    [Test]
    public void DialogueLines_RandomNeverRepeatsALine()
    {
        var lines = new DialogueLines(LineOrder.Random, "a", "b", "c");
        string previous = lines.Advance(count => 0);
        for (int i = 0; i < 10; i++)
        {
            string line = lines.Advance(count => 0);
            Assert.AreNotEqual(previous, line);
            previous = line;
        }
    }

    [Test]
    public void QuestMap_ColumnsFollowLongestPrerequisitePathAndChainsGetBands()
    {
        QuestDefinition first = Quest("First");
        QuestDefinition second = Quest("Second");
        QuestDefinition third = Quest("Third");
        QuestDefinition loose = Quest("Loose");
        SetField(first, "chain", "Story");
        SetField(second, "chain", "Story");
        SetField(third, "chain", "Story");
        SetField(second, "prerequisites", new List<QuestDefinition> { first });
        SetField(third, "prerequisites", new List<QuestDefinition> { first, second });

        List<QuestMapNode> nodes = QuestMapLayout.Build(new[] { loose, third, second, first });
        QuestMapNode Find(QuestDefinition quest) => nodes.Find(node => node.Quest == quest);

        Assert.AreEqual(0, Find(first).Column);
        Assert.AreEqual(1, Find(second).Column);
        Assert.AreEqual(2, Find(third).Column);
        Assert.AreEqual("Story", Find(first).Band);
        Assert.AreEqual(QuestMapLayout.NoChain, Find(loose).Band);
        Assert.Greater(Find(loose).Row, Find(third).Row, "quests without a chain go below the chains");
    }

    [Test]
    public void QuestMap_LinkRulesBlockSelfDuplicatesAndLoops()
    {
        QuestDefinition a = Quest("A");
        QuestDefinition b = Quest("B");
        QuestDefinition c = Quest("C");
        SetField(b, "prerequisites", new List<QuestDefinition> { a });
        SetField(c, "prerequisites", new List<QuestDefinition> { b });

        Assert.IsNull(QuestMapLayout.CheckLink(a, c), "a shortcut link is fine");
        Assert.IsNotNull(QuestMapLayout.CheckLink(a, a), "itself");
        Assert.IsNotNull(QuestMapLayout.CheckLink(a, b), "already linked");
        Assert.IsNotNull(QuestMapLayout.CheckLink(c, a), "would loop through b");
    }

    [Test]
    public void QuestMap_SurvivesPrerequisiteCycles()
    {
        QuestDefinition a = Quest("A");
        QuestDefinition b = Quest("B");
        SetField(a, "prerequisites", new List<QuestDefinition> { b });
        SetField(b, "prerequisites", new List<QuestDefinition> { a });

        Assert.AreEqual(2, QuestMapLayout.Build(new[] { a, b }).Count);
    }

    [Test]
    public void QuestMap_FollowUpCopiesPrerequisiteChainGiverAndHandIn()
    {
        QuestDefinition first = Quest("First");
        SetField(first, "chain", "Village");
        SetField(first, "start", QuestStart.ByQuestGiver);
        SetField(first, "turnInToGiver", true);
        QuestDefinition next = Quest("Next");

        QuestSection.MakeFollow(next, first);

        CollectionAssert.AreEqual(new[] { first }, next.Prerequisites);
        Assert.AreEqual("Village", next.Chain);
        Assert.AreEqual(QuestStart.ByQuestGiver, next.Start);
        Assert.IsTrue(new UnityEditor.SerializedObject(next).FindProperty("turnInToGiver").boolValue);

        SetField(first, "start", QuestStart.OnQuestEvent);
        QuestDefinition other = Quest("Other");
        QuestSection.MakeFollow(other, first);
        Assert.AreEqual(QuestStart.Automatically, other.Start, "a start event would start both quests, so it is not copied");
    }

    [Test]
    public void ContentDraft_IsEditable()
    {
        ScriptableObject draft = new QuestSection().CreateDraft();
        try
        {
            Assert.AreEqual(HideFlags.None, draft.hideFlags & HideFlags.NotEditable, "NotEditable greys out the whole draft");
        }
        finally
        {
            Object.DestroyImmediate(draft);
        }
    }

    // ----- Quest gates -----

    [Test]
    public void QuestGate_LocksUntilItsQuestStarts()
    {
        QuestDefinition quest = Quest("portal", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        var portal = new GameObject("Portal");
        GameSession.StartNewGame();
        try
        {
            QuestGate gate = portal.AddComponent<QuestGate>();
            SetField(gate, "quest", quest);

            Assert.IsFalse(gate.CanInteract(null, out string reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason), "A locked gate tells the player why.");

            GameSession.Quests.TryStart(quest);
            Assert.IsTrue(gate.CanInteract(null, out _), "Opens while the quest is active.");

            SetField(gate, "requirement", QuestGate.Requirement.Completed);
            Assert.IsFalse(gate.CanInteract(null, out _));
            GameSession.Quests.ForceComplete(quest);
            Assert.IsTrue(gate.CanInteract(null, out _));
        }
        finally
        {
            Object.DestroyImmediate(portal);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void BuildObjectives_CanCountWhatIsAlreadyBuilt()
    {
        var campfire = Create<BuildingDefinition>("campfire");
        QuestObjective build = QuestObjective.Create(ObjectiveType.BuildStructure, 1, campfire);
        Assert.IsFalse(build.CountAlreadyOwned);
        SetField(build, "countAlreadyOwned", true);
        Assert.IsTrue(build.CountAlreadyOwned);
        Assert.AreEqual(ObjectiveType.BuildStructure, build.Type);
        Assert.AreSame(campfire, build.Building);
    }

    [Test]
    public void SceneIndex_FindsEveryRaisedByGameField()
    {
        IReadOnlyDictionary<Type, HashSet<string>> fields = QuestSceneIndex.RaisingFields;
        CollectionAssert.Contains(fields[typeof(JobDefinition)], "assignedEvent");
        CollectionAssert.Contains(fields[typeof(WardBreachDefinition)], "wonEvent");
        CollectionAssert.AreEquivalent(new[] { "freedEvent", "rescuedEvent" }, fields[typeof(RescuableTraveler)]);
        CollectionAssert.Contains(fields[typeof(GameEventTrigger)], "gameEvent");
        Assert.IsFalse(fields.ContainsKey(typeof(GameEventListener)), "Listeners react to events; they don't raise them.");
    }

    // ----- Whole quest flow -----

    [Test]
    public void QuestFlow_StartsProgressesAndHandsInThroughTheEventBus()
    {
        var talk = Create<GameEventDefinition>("talked_to_smith");
        var forged = Create<GameEventDefinition>("forged_blade");
        var opened = Create<GameEventDefinition>("forge_opened");
        QuestDefinition first = Quest("smith", QuestObjective.Create(ObjectiveType.QuestEvent, 1, forged));
        SetField(first, "start", QuestStart.OnQuestEvent);
        SetField(first, "startEvent", talk);
        SetField(first, "turnInToGiver", true);
        SetField(first, "completionEvent", opened);
        QuestDefinition second = Quest("smith 2", QuestObjective.Create(ObjectiveType.KillMonster, 1));
        SetField(second, "prerequisites", new List<QuestDefinition> { first });
        var database = Create<ContentDatabase>("content");
        SetField(database, "quests", new List<QuestDefinition> { first, second });

        var raised = new List<GameEvent>();
        Action<GameEvent> record = raised.Add;
        GameSession.StartNewGame();
        var host = new GameObject("Quest Manager");
        QuestManager manager = host.AddComponent<QuestManager>();
        GameEvents.Raised += record;
        try
        {
            manager.Initialise(database);
            Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(first));

            GameEvents.Raise(talk);
            Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(first), "Its start event starts it.");
            Assert.IsFalse(manager.TryTurnIn(first, out string reason));
            Assert.AreEqual("That isn't finished yet.", reason);

            GameEvents.Raise(forged);
            Assert.AreEqual(QuestStatus.ReadyToTurnIn, manager.Log.StatusOf(first));
            Assert.IsFalse(raised.Exists(e => e.Subject == opened), "The completion event waits for the hand-in.");

            Assert.IsTrue(manager.TryTurnIn(first, out _));
            Assert.AreEqual(QuestStatus.Completed, manager.Log.StatusOf(first));
            Assert.IsTrue(raised.Exists(e => e.Kind == GameEventKind.QuestCompleted && e.Subject == first));
            Assert.IsTrue(raised.Exists(e => e.Kind == GameEventKind.Custom && e.Subject == opened));
            Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(second), "The next quest of the chain starts.");
            Assert.IsFalse(manager.TryStartQuest(first, out reason));
            StringAssert.Contains("already done", reason);
        }
        finally
        {
            GameEvents.Raised -= record;
            // Edit Mode doesn't call OnDestroy, so the test unsubscribes the manager itself.
            typeof(QuestManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Object.DestroyImmediate(host);
            GameSession.StartNewGame();
        }
    }

    // ----- Helpers -----

    private sealed class Lookup : IContentLookup
    {
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
        public readonly Dictionary<string, QuestDefinition> Quests = new Dictionary<string, QuestDefinition>();

        public ItemDefinition FindItem(string id) => null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public QuestDefinition FindQuest(string id) => id != null && Quests.TryGetValue(id, out QuestDefinition quest) ? quest : null;
    }

    private static GameEvent Raid(RaidDefinition raid, RaidOutcome outcome, float seconds)
    {
        return new GameEvent(GameEventKind.RaidFinished, raid, 1, null, outcome, seconds);
    }

    private QuestDefinition Quest(string name, params QuestObjective[] objectives)
    {
        QuestDefinition quest = Create<QuestDefinition>(name);
        SetField(quest, "objectives", new List<QuestObjective>(objectives));
        return quest;
    }

    private static void SetField(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    // A kill by someone on the player's side; the victim's side when the subject doesn't say.
    private GameEvent Kill(MonsterDefinition monster, Faction? victim = null) =>
        new GameEvent(GameEventKind.Killed, monster, 1, Fighter(Faction.Player), victimFaction: victim);

    private GameObject Fighter(Faction faction)
    {
        var fighter = Created(new GameObject($"{faction} fighter"));
        var serialized = new UnityEditor.SerializedObject(fighter.AddComponent<Combatant>());
        serialized.FindProperty("faction").enumValueIndex = (int)faction;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return fighter;
    }

    private GameObject Created(GameObject gameObject)
    {
        created.Add(gameObject);
        return gameObject;
    }

    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }
}
