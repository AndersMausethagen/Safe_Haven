using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PrologueTests
{
    private const string Root = "Assets/_Project/Data/Prologue";
    private PrologueDefinition Definition => AssetDatabase.LoadAssetAtPath<PrologueDefinition>(Root + "/ForestPrologue.asset");
    // The quests of the sequence, each once: a quest can run through several chapters (Ends At Objective).
    private QuestDefinition[] Quests => Definition.Chapters.Select(c => c.Quest).Distinct().ToArray();

    // What doing an objective looks like to the quest log.
    private static GameEvent Doing(QuestObjective objective) =>
        objective.DeedKind.HasValue ? new GameEvent(objective.DeedKind.Value, objective.DeedTarget, objective.Amount) :
                    new GameEvent(objective.Type == ObjectiveType.QuestEvent ? GameEventKind.Custom :
                    objective.Type == ObjectiveType.BuildStructure ? GameEventKind.Built :
                    objective.Type == ObjectiveType.CraftItem ? GameEventKind.Crafted : GameEventKind.ItemObtained,
                    objective.Type == ObjectiveType.QuestEvent ? (UnityEngine.Object)objective.QuestEvent :
                    objective.Type == ObjectiveType.BuildStructure ? (UnityEngine.Object)objective.Building : objective.Item, objective.Amount);

    [Test]
    public void TheCampfire_OpensTheSameStationWindowAsInTheHaven()
    {
        // Without the StationWindow under Screen Canvas, stations fall back to the old text menu (StationMenu).
        string guid = AssetDatabase.AssetPathToGUID("Assets/_Project/Prefabs/UI/StationWindow.prefab");
        Assert.IsNotEmpty(guid);
        foreach (string scene in new[] { "Assets/_Project/Scenes/SafeHaven.unity", "Assets/_Project/Scenes/Prologue_MistyWoods.unity" })
        {
            StringAssert.Contains(guid, System.IO.File.ReadAllText(scene), scene);
        }
    }

    [Test]
    public void Steps_SayWhereTheNightIs()
    {
        // FindShelter, GatherFuel, LightFire, CatchBreath, DefendCamp, ReachPortal, ActivatePortal, HoldPortal, Finished, Story
        // (a Story chapter says for itself where it is: these are its defaults)
        var steps = (PrologueStep[])System.Enum.GetValues(typeof(PrologueStep));
        const PrologueSite F = PrologueSite.Forest, C = PrologueSite.Camp, P = PrologueSite.Portal, M = PrologueSite.Meadow;
        CollectionAssert.AreEqual(new[] { F, C, C, C, C, C, P, P, P, M }, steps.Select(s => s.Checkpoint()),
            "A checkpoint on the way to the portal restarts at the camp.");
        CollectionAssert.AreEqual(new PrologueSite?[] { null, C, C, C, C, P, P, P, P, M }, steps.Select(s => s.Rally()),
            "Companions walk with the player to the shelter, and run ahead to the portal from the old troll.");
        CollectionAssert.AreEqual(new[] { F, C, C, C, C, P, P, P, P, M }, steps.Select(s => s.Talk()));
        CollectionAssert.AreEqual(new[] { false, true, true, true, false, false, false, false, false, false }, steps.Select(s => s.AtRest()),
            "No small talk while wolves attack.");
        CollectionAssert.AreEqual(new[] { false, false, false, false, false, false, false, true, true, false }, steps.Select(s => s.PortalOpen()));
        PrologueChapter flight = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.ReachPortal);
        Assert.AreEqual(PrologueCompanions.Flee, flight.Companions, "Nobody stays to fight the old troll.");
    }

    [Test]
    public void SavedLeaderOffers_AreIndexedAsStandardGivers()
    {
        QuestSceneIndex.Invalidate();
        foreach (var chapter in Definition.Chapters)
        {
            var givers = QuestSceneIndex.UsesOf(chapter.Quest).Where(u => u.Role == QuestSceneRole.Gives).ToArray();
            if (chapter.Quest.Start == QuestStart.ByQuestGiver || chapter.Quest.TurnInToGiver)
            {
                Assert.IsTrue(givers.Any(u => u.AssetPath == "Assets/_Project/Scenes/Prologue_MistyWoods.unity" &&
                    u.ObjectName.Contains("Sigrid") && u.ObjectFileId != 0), chapter.Quest.name);
                Assert.IsFalse(QuestSection.FindSceneProblems(chapter.Quest, Definition.Chapters.Select(c => c.Quest))
                    .Any(p => p.Contains("no scene or prefab")), chapter.Quest.name);
            }
            else Assert.IsEmpty(givers, "Automatic emergency chapters without a return objective need no giver.");
        }
        var unassigned = ScriptableObject.CreateInstance<QuestDefinition>();
        try
        {
            var data = new SerializedObject(unassigned);
            data.FindProperty("start").enumValueIndex = (int)QuestStart.ByQuestGiver;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(QuestSection.FindSceneProblems(unassigned, new[] { unassigned })
                .Any(p => p.Contains("no scene or prefab")), "A genuinely missing giver must still warn.");
        }
        finally { UnityEngine.Object.DestroyImmediate(unassigned); }
    }

    [Test]
    public void AuthoredPrologue_IsValidAndUsesOneMinutePortal()
    {
        Assert.NotNull(Definition);
        Assert.IsNull(Definition.FindProblem());
        // Seven quests in nine chapters (Docs/QuestRedesign.md, step 2): it was twelve of each.
        Assert.AreEqual(9, Definition.Chapters.Length);
        Assert.AreEqual(7, Quests.Length);
        CollectionAssert.AreEqual(new[] { "MorningCamp", "RoadToForest", "FindShelter", "CatchBreath", "DefendCamp", "ReachPortal", "HoldPortal" }, Quests.Select(q => q.name));
        Assert.AreEqual(60f, Definition.Chapters.Single(c => c.Behaviour == PrologueStep.HoldPortal).Duration);
        // Story graphs in the scene tell the flight from the old troll and what happens at the stones (Story - The Old Troll,
        // Story - What Sigrid Knows), and open the rest (Story - A Name Gone Missing): those chapters have no arrival line.
        Assert.IsTrue(Definition.Chapters.Where(c => c.Quest.name != "ReachPortal" && c.Quest.name != "CatchBreath").All(c => !string.IsNullOrWhiteSpace(c.ArrivalText)));
        Assert.IsNull(Definition.StartingWeapon, "The weapon is chosen as the party sets out, in the scene's story (Story - Choose a Weapon).");
        Assert.IsNull(Definition.StartingTorch, "Nobody carries a lit torch in the morning: it is handed out on the road (Story - The Road East).");
        // 40% fewer at the portal on request (2026-11-09): 2 at first, waves of 2, 3, 3, 3 (13 in all, was 22).
        Assert.AreEqual(2, Definition.WaveSize(1));
        Assert.AreEqual(3, Definition.WaveSize(100), "Waves are capped; the companions fight too.");
    }

    [Test]
    public void AuthoredQuests_FormACompleteEventDrivenSequence()
    {
        var log = new QuestLog();
        var visited = new System.Collections.Generic.List<PrologueChapter>();
        foreach (QuestDefinition quest in Quests)
        {
            Assert.AreSame(quest, Definition.Chapter(log).Quest);
            Assert.IsTrue(log.TryStart(quest), quest.DisplayName);
            Assert.IsEmpty(quest.FindProblems(), quest.DisplayName);
            foreach (QuestObjective objective in quest.Objectives)
            {
                // Every chapter of the quest gets its turn as the objectives are done in order.
                if (!visited.Contains(Definition.Chapter(log))) visited.Add(Definition.Chapter(log));
                log.Handle(Doing(objective));
            }

            if (quest.TurnInToGiver)
            {
                Assert.AreEqual(QuestStatus.ReadyToTurnIn, log.StatusOf(quest));
                Assert.IsTrue(log.TryTurnIn(quest));
            }
            Assert.AreEqual(QuestStatus.Completed, log.StatusOf(quest));
        }
        CollectionAssert.AreEqual(Definition.Chapters, visited, "each chapter in its turn, none skipped");
        Assert.IsNull(Definition.Chapter(log));
        Assert.IsTrue(log.HasHappened(Definition.FinishedEvent), "The hold objective is the actual finished story event.");
    }

    [Test]
    public void LaterChapters_CannotStartBeforeTheirPrerequisites()
    {
        foreach (QuestDefinition quest in Quests.Skip(1))
            Assert.IsFalse(new QuestLog().TryStart(quest), quest.name);
    }

    // Sigrid briefs twice, in the morning and before the road. From the tree line on the night gives nobody time to be
    // briefed: each quest starts by itself, and what a part of a quest needs said is called out in passing.
    [Test]
    public void OnlyTheMorningAndTheRoad_AreBriefings()
    {
        QuestDefinition[] quests = Quests;
        for (int i = 0; i < quests.Length; i++)
        {
            Assert.AreEqual(i < 2 ? QuestStart.ByQuestGiver : QuestStart.Automatically, quests[i].Start, quests[i].name);
            bool nextBriefing = i + 1 < quests.Length && quests[i + 1].Start == QuestStart.ByQuestGiver;
            Assert.AreEqual(nextBriefing, quests[i].TurnInToGiver,
                "The next briefing hands this quest in and accepts its successor in one interaction.");
            if (nextBriefing) Assert.AreEqual("Sigrid", quests[i].TurnInTo);
        }
    }

    [Test]
    public void OneQuest_RunsThroughSeveralChapters()
    {
        PrologueChapter[] shelter = Definition.Chapters.Where(c => c.Quest.name == "FindShelter").ToArray();
        CollectionAssert.AreEqual(new[] { PrologueStep.FindShelter, PrologueStep.LightFire }, shelter.Select(c => c.Behaviour));
        Assert.AreEqual(1, shelter[0].EndsAtObjective, "the first part ends at the shelter");
        Assert.AreEqual(0, shelter[1].EndsAtObjective, "the last part lasts as long as the quest");
        Assert.IsTrue(Definition.OpensItsQuest(shelter[0]));
        Assert.IsFalse(Definition.OpensItsQuest(shelter[1]), "no hand-over in the middle of a quest: its line is said in passing");
        Assert.AreEqual(PrologueCompanions.Follow, shelter[0].Companions);
        Assert.IsTrue(shelter[1].AtRest, "the companions can be asked things while the player gathers");

        PrologueChapter[] stones = Definition.Chapters.Where(c => c.Quest.name == "ReachPortal").ToArray();
        CollectionAssert.AreEqual(new[] { PrologueStep.ReachPortal, PrologueStep.ActivatePortal }, stones.Select(c => c.Behaviour));
        Assert.IsTrue(stones[0].Quest.ObjectivesInOrder, "the stone can't be touched before it is reached");
        Assert.AreEqual(Definition.PortalActivatedEvent, stones[0].Quest.Objectives[1].QuestEvent);

        // The rules a designer is told about when they are broken.
        var copy = UnityEngine.Object.Instantiate(Definition);
        try
        {
            var data = new SerializedObject(copy);
            SerializedProperty chapters = data.FindProperty("chapters");
            int first = System.Array.IndexOf(Definition.Chapters, shelter[0]);
            chapters.GetArrayElementAtIndex(first).FindPropertyRelative("endsAtObjective").intValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
            StringAssert.Contains("Ends At Objective", copy.FindProblem());
            chapters.GetArrayElementAtIndex(first).FindPropertyRelative("endsAtObjective").intValue = 9;
            data.ApplyModifiedPropertiesWithoutUndo();
            StringAssert.Contains("no objective 9", copy.FindProblem());
            chapters.GetArrayElementAtIndex(first).FindPropertyRelative("endsAtObjective").intValue = 1;
            chapters.MoveArrayElement(first + 1, 0);
            data.ApplyModifiedPropertiesWithoutUndo();
            StringAssert.Contains("must follow each other", copy.FindProblem());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }

    // The camp was two wolves, over in fifteen seconds of the playtest.
    [Test]
    public void TheCamp_IsAttackedTwice()
    {
        PrologueChapter camp = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.DefendCamp);
        Assert.AreEqual(3, camp.InitialEnemies);
        Assert.AreEqual(2, camp.Waves);
        StringAssert.StartsWith("Runa:", camp.NextWaveText, "someone calls out the second wave");
        Assert.IsTrue(Definition.Chapters.Where(c => c != camp).All(c => c.Waves == 1));
    }

    // Eirik's errand during the rest: it gates nothing, and the wolves end it.
    [Test]
    public void TheBerries_AreAnErrand()
    {
        var pot = AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root + "/Quests/BerriesForThePot.asset");
        Assert.AreEqual(QuestRank.Optional, pot.Rank);
        Assert.AreEqual(QuestStart.ByQuestGiver, pot.Start);
        Assert.IsFalse(Definition.Chapters.Any(c => c.Quest == pot), "not a step of the story");
        Assert.AreEqual("DefendCamp", pot.ClosedBy.name);
        Assert.AreEqual("Eirik", pot.TurnInTo);
        Assert.IsFalse(Quests.Any(q => q.Prerequisites.Contains(pot)), "nothing waits for it");
        Assert.IsNotEmpty(pot.RewardItems);
        Assert.IsEmpty(pot.FindProblems());
    }

    [Test]
    public void Chain_AnnouncesOnlyItsBeginningAndEnd_AndMakesTimedInstructionsExplicit()
    {
        QuestDefinition[] quests = Quests;
        for (int i = 0; i < quests.Length; i++)
        {
            var quest = quests[i];
            Assert.AreEqual(i == 0, quest.AnnounceStart, quest.name);
            Assert.AreEqual(i == quests.Length - 1, quest.AnnounceCompletion, quest.name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(quest.AvailableObjective), quest.name);
        }
        var rest = Definition.Chapters.Single(c => c.Quest.name == "CatchBreath");
        Assert.AreEqual(PrologueStep.Story, rest.Behaviour, "The rest has no countdown: it lasts until the wolves interrupt it.");
        Assert.IsTrue(rest.AtRest);
        Assert.AreEqual(PrologueSite.Camp, rest.Checkpoint);
        Assert.IsTrue(Definition.Chapters.Single(c => c.Behaviour == PrologueStep.HoldPortal).Quest.ShowTimerInHud);
    }

    [Test]
    public void Campfire_RequiresARealBuildAndGatheringCoversItsSingleCost()
    {
        var log = new QuestLog();
        var fire = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.LightFire).Quest;
        foreach (var chapter in Definition.Chapters.TakeWhile(c => c.Quest != fire)) log.ForceComplete(chapter.Quest);
        Assert.IsTrue(log.TryStart(fire));
        foreach (QuestObjective before in fire.Objectives.Where(o => o.Type != ObjectiveType.BuildStructure)) log.Handle(Doing(before));
        log.Handle(new GameEvent(GameEventKind.Custom, Definition.FireEvent));
        Assert.AreEqual(QuestStatus.Active, log.StatusOf(fire), "The old use-fire shortcut must not complete construction.");
        Assert.AreSame(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Campfire.asset"),
            Definition.Campfire, "The expedition builds the same campfire the player can build at home.");
        Assert.IsTrue(Definition.Campfire.ShowInBuildMenu);
        // The quest asks for exactly what the fire costs, and counts what the player already carries.
        Assert.AreEqual(Definition.Campfire.ConstructionItems.Count, fire.Objectives.Count(o => o.Type == ObjectiveType.CollectItem));
        foreach (var cost in Definition.Campfire.ConstructionItems)
        {
            QuestObjective gather = fire.Objectives.Single(o => o.Item == cost.Item);
            Assert.AreEqual(cost.Amount, gather.Amount);
            Assert.IsTrue(gather.CountAlreadyOwned);
        }

        log.Handle(new GameEvent(GameEventKind.Built, Definition.Campfire));
        Assert.AreEqual(QuestStatus.Completed, log.StatusOf(fire));
        Assert.AreEqual("CatchBreath", Definition.Chapter(log).Quest.name, "the rest follows the fire, with nothing to hand in between");
    }

    [Test]
    public void StartingArmor_IsACompleteHideSetAndTheCampfirePersists()
    {
        // Head, body (which covers the legs), hands and feet.
        Assert.AreEqual(4, Definition.StartingArmor.Length);
        Assert.IsTrue(Definition.StartingArmor.All(i => i != null && i.IsArmor && i.name.StartsWith("WovenHide")));
        Assert.AreEqual(4, Definition.StartingArmor.Select(i => i.ArmorSlot).Distinct().Count());
        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        var prefab = Definition.Campfire.Prefab.GetComponent<SaveableEntity>();
        Assert.AreSame(prefab, database.FindPrefab(prefab.PrefabId));
    }

    [Test]
    public void HomeAwakening_DoesNotAutomaticallyStartInForest()
    {
        var awakening = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/Awakening.asset");
        Assert.AreEqual(QuestStart.OnQuestEvent, awakening.Start);
        Assert.AreNotEqual(Definition.StartedEvent, awakening.StartEvent);
    }

    [Test]
    public void CampDefence_CountsTheEncounterRatherThanPlayerKillCredit()
    {
        var chapter = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.DefendCamp);
        Assert.AreEqual(1, chapter.Quest.Objectives.Count);
        Assert.AreEqual(ObjectiveType.QuestEvent, chapter.Quest.Objectives[0].Type);
        Assert.AreEqual(Definition.CampDefendedEvent, chapter.Quest.Objectives[0].QuestEvent);
    }

    [Test]
    public void PortalRepair_RemembersEarlierRepairAndDoesNotBlockLaterQuests()
    {
        var repair = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/AwakeningThePortal.asset");
        Assert.IsTrue(repair.Objectives[0].CountAlreadyOwned);
    }

    [Test]
    public void PrologueScene_IsEnabledInBuild()
    {
        Assert.IsTrue(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/_Project/Scenes/Prologue_MistyWoods.unity"));
    }

    [Test]
    public void OrdinaryPlay_RejectsMixedPrologueScenesButAllowsAnExplicitStartScene()
    {
        const string path = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
        Scene prologue = SceneManager.GetSceneByPath(path);
        bool opened = !prologue.isLoaded;
        Scene active = SceneManager.GetActiveScene();
        SceneAsset previous = EditorSceneManager.playModeStartScene;
        Scene extra = default;
        try
        {
            if (opened) prologue = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (SceneManager.sceneCount == 1)
                extra = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainMenu.unity", OpenSceneMode.Additive);
            EditorSceneManager.playModeStartScene = null;
            Assert.IsTrue(ProloguePreview.HasConflictingScenes());
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            Assert.IsFalse(ProloguePreview.HasConflictingScenes(), "The isolated preview loads only its explicit start scene.");
        }
        finally
        {
            EditorSceneManager.playModeStartScene = previous;
            if (extra.IsValid()) EditorSceneManager.CloseScene(extra, true);
            if (opened) EditorSceneManager.CloseScene(prologue, true);
            if (active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }

    [Test]
    public void Shelter_RoofColliderClearsThePlayerAndGeometryIsIncludedInNavigation()
    {
        const string path = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var director = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PrologueDirector>(true)).Single();
            var roof = director.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Overhang");
            var collider = roof.GetComponent<BoxCollider>();
            Assert.NotNull(collider, "A scaled sphere collider expands to its largest axis and can push the player through terrain.");
            Assert.IsNull(roof.GetComponent<SphereCollider>());
            var camp = (Transform)new SerializedObject(director).FindProperty("campPoint").objectReferenceValue;
            Assert.Greater(collider.bounds.min.y, camp.position.y + 1.8f, "The shelter must leave standing clearance.");
            var surface = director.GetComponentInParent<Unity.AI.Navigation.NavMeshSurface>();
            Assert.NotNull(surface, "The isolated navigation bake must include authored shelter and waystone geometry.");
            Assert.NotNull(surface.navMeshData, "Without baked data no companion is on the NavMesh, so the escort never moves.");
            Assert.NotNull(director.GetComponent<PersistentScene>(), "The player's placed fire must survive reload.");
            Assert.NotNull(director.BuildTerrain, "Forest placement must follow real terrain rather than a flat ward plane.");
            Assert.AreEqual(1, scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ActionMenuWindow>(true)).Count(),
                "Leader conversations and the build menu need the real action-menu UI.");
            var companions = director.GetComponentsInChildren<PrologueCompanion>(true);
            Assert.AreEqual(7, companions.Length);
            Assert.AreEqual(3, companions.Count(c => c.GetComponent<QuestGiver>() != null), "Sigrid leads; Eirik and Runa have errands.");
            foreach (var companion in companions)
            {
                Assert.AreEqual(1, companion.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Expedition Torch"));
                if (companion.gameObject.activeSelf)
                    Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(companion.transform.position, out _, 1f, UnityEngine.AI.NavMesh.AllAreas),
                        companion.name + " must start on the baked NavMesh to escort the player.");
            }
            var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Player>(true)).Single();
            Assert.IsTrue(player.GetComponent<BuildPlacer>().enabled);
            Assert.AreEqual(0, new SerializedObject(player.GetComponent<ItemInventory>()).FindProperty("startingItems").arraySize,
                "The definition is the single source of the forest loadout.");
            Assert.AreEqual(0, player.GetComponentsInChildren<ExpeditionTorch>(true).Length,
                "The player's torch must come from the normal off-hand item, not an attached scene prop.");
            Assert.IsFalse(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BuildingObject>(true))
                .Any(b => b.Definition == Definition.Campfire), "There must not be a prebuilt campfire.");
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void Lights_DoNotSitInsideACharactersHead()
    {
        // Point-light falloff is inverse-square: a light a hand's width from a face blows it out with a hard edge.
        // Areas that only need sight around a character use a VisibilitySource, not a Light.
        const string path = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
        const float minDistance = 0.5f;
        Scene scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var heads = roots.SelectMany(r => r.GetComponentsInChildren<Animator>(true))
                .Where(a => a.isHuman && a.GetBoneTransform(HumanBodyBones.Head) != null)
                .ToArray();
            Assert.IsNotEmpty(heads);
            foreach (var light in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l.type != LightType.Directional))
            {
                foreach (var character in heads)
                {
                    Vector3 head = character.GetBoneTransform(HumanBodyBones.Head).position;
                    Assert.Greater(Vector3.Distance(light.transform.position, head), minDistance,
                        light.name + " sits inside " + character.name + "'s head.");
                }
            }
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void Definitions_AreNotChangedByQuestProgress()
    {
        string before = EditorJsonUtility.ToJson(Definition);
        var log = new QuestLog();
        PrologueChapter shelter = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.FindShelter);
        foreach (var chapter in Definition.Chapters.TakeWhile(c => c != shelter)) log.ForceComplete(chapter.Quest);
        log.TryStart(shelter.Quest);
        // The shelter's zone raises the story event and counts as reaching the shelter, which the quest asks for.
        log.Handle(new GameEvent(GameEventKind.Custom, Definition.ShelterEvent));
        log.Handle(new GameEvent(GameEventKind.Reached, shelter.Quest.Objectives[0].DeedTarget));
        Assert.AreEqual(before, EditorJsonUtility.ToJson(Definition));
        PrologueChapter fire = Definition.Chapters[System.Array.IndexOf(Definition.Chapters, shelter) + 1];
        Assert.AreSame(fire, Definition.Chapter(log), "Reaching the shelter moves on to the fire, with nobody to report to.");
        Assert.AreSame(shelter.Quest, fire.Quest, "It is still the same quest for the player.");
        Assert.AreEqual(QuestStatus.Active, log.StatusOf(shelter.Quest));
    }

    [Test]
    public void Designer_CanAddAChapterWithoutChangingRuntimeCode()
    {
        var copy = UnityEngine.Object.Instantiate(Definition);
        try
        {
            var rescue = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/Awakening.asset");
            Assert.NotNull(rescue);
            var serialized = new SerializedObject(copy);
            SerializedProperty chapters = serialized.FindProperty("chapters");
            int index = chapters.arraySize++;
            SerializedProperty extra = chapters.GetArrayElementAtIndex(index);
            extra.FindPropertyRelative("quest").objectReferenceValue = rescue;
            extra.FindPropertyRelative("behaviour").enumValueIndex = (int)PrologueStep.CatchBreath;
            extra.FindPropertyRelative("duration").floatValue = 7f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var log = new QuestLog();
            foreach (var original in Definition.Chapters) log.ForceComplete(original.Quest);
            Assert.IsNull(copy.FindProblem());
            Assert.AreEqual(rescue, copy.Chapter(log).Quest);
            Assert.AreEqual(7f, copy.Chapter(log).Duration);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }
    [Test]
    public void PortalHold_IsOneMinute_WithMixedEnemiesThatDoNotPersist()
    {
        PrologueChapter hold = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.HoldPortal);
        Assert.AreEqual(60f, hold.Duration);
        Assert.IsNull(Definition.FindProblem());
        var first = Enumerable.Range(0, 50).Select(i => hold.Enemy(1, i / 50f).name).Distinct().ToArray();
        var later = Enumerable.Range(0, 50).Select(i => hold.Enemy(3, i / 50f).name).Distinct().ToArray();
        PrologueChapter camp = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.DefendCamp);
        CollectionAssert.AreEqual(new[] { "ExpeditionWolf" }, Enumerable.Range(0, 20).Select(i => camp.Enemy(1, i / 20f).name).Distinct(),
            "The camp is attacked by wolves; both fights are authored the same way.");
        Assert.Greater(first.Length, 1, "More than wolves come through the dark.");
        CollectionAssert.DoesNotContain(first, "ExpeditionMistwing", "Mistwings come from the second wave.");
        CollectionAssert.Contains(later, "ExpeditionMistwing");
    }

    // Playtest 2026-11-05: nothing optional before the leader has spoken, and the optional quests come one at a time.
    [Test]
    public void OptionalQuests_OpenOnceTheLeaderHasSpoken_OneAtATime()
    {
        QuestDefinition Quest(string name) => AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root + "/Quests/" + name + ".asset");
        QuestDefinition morning = Definition.Chapters.First().Quest;
        QuestDefinition wood = Quest("WoodForTheRoad"), fish = Quest("FishForBreakfast"), hunt = Quest("HuntForThePot");
        Assert.AreEqual("MorningCamp", morning.name);

        var log = new QuestLog();
        Assert.IsFalse(log.PrerequisitesMet(wood), "nothing optional before Sigrid has given the morning");
        Assert.IsTrue(log.TryStart(morning));
        Assert.IsTrue(log.PrerequisitesMet(wood), "offered once the morning quest has begun, not when it is done");
        Assert.IsFalse(log.PrerequisitesMet(fish));
        Assert.IsFalse(log.PrerequisitesMet(hunt));
        CollectionAssert.AreEqual(new[] { wood }, fish.Prerequisites);
        CollectionAssert.AreEqual(new[] { fish }, hunt.Prerequisites);
    }
}
