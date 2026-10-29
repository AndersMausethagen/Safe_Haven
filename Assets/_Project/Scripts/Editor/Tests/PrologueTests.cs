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

    [Test]
    public void SavedLeaderOffers_AreIndexedAsStandardGivers()
    {
        QuestSceneIndex.Invalidate();
        foreach (var chapter in Definition.Chapters)
        {
            var givers = QuestSceneIndex.UsesOf(chapter.Quest).Where(u => u.Role == QuestSceneRole.Gives).ToArray();
            if (chapter.Quest.Start == QuestStart.ByQuestGiver)
            {
                Assert.IsTrue(givers.Any(u => u.AssetPath == "Assets/_Project/Scenes/Prologue_MistyWoods.unity" &&
                    u.ObjectName.Contains("Sigrid") && u.ObjectFileId != 0), chapter.Quest.name);
                Assert.IsFalse(QuestSection.FindSceneProblems(chapter.Quest, Definition.Chapters.Select(c => c.Quest))
                    .Any(p => p.Contains("no scene or prefab")), chapter.Quest.name);
            }
            else Assert.IsEmpty(givers, "Automatic emergency chapters are not leader briefings.");
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
    public void AuthoredPrologue_IsValidAndUsesThreeMinutePortal()
    {
        Assert.NotNull(Definition);
        Assert.IsNull(Definition.FindProblem());
        Assert.AreEqual(9, Definition.Chapters.Length);
        Assert.AreEqual(180f, Definition.Chapters.Single(c => c.Behaviour == PrologueStep.HoldPortal).Duration);
        // The flight from the old troll is told by its story graph in the scene (Story - The Old Troll), not an arrival line.
        Assert.IsTrue(Definition.Chapters.Where(c => c.Behaviour != PrologueStep.ReachPortal).All(c => !string.IsNullOrWhiteSpace(c.ArrivalText)));
        Assert.IsNull(Definition.StartingWeapon, "The bow or the axe is chosen in the scene's story (Story - Bow or Axe).");
        Assert.AreEqual(3, Definition.WaveSize(1));
        Assert.AreEqual(7, Definition.WaveSize(100));
    }

    [Test]
    public void AuthoredQuests_FormACompleteEventDrivenSequence()
    {
        var log = new QuestLog();
        foreach (PrologueChapter chapter in Definition.Chapters)
        {
            Assert.AreSame(chapter, Definition.Chapter(log));
            Assert.IsTrue(log.TryStart(chapter.Quest), chapter.Quest.DisplayName);
            Assert.IsEmpty(chapter.Quest.FindProblems(), chapter.Quest.DisplayName);
            foreach (QuestObjective objective in chapter.Quest.Objectives)
                log.Handle(new GameEvent(objective.Type == ObjectiveType.QuestEvent ? GameEventKind.Custom :
                    objective.Type == ObjectiveType.BuildStructure ? GameEventKind.Built :
                    objective.Type == ObjectiveType.CraftItem ? GameEventKind.Crafted : GameEventKind.ItemObtained,
                    objective.Type == ObjectiveType.QuestEvent ? (UnityEngine.Object)objective.QuestEvent :
                    objective.Type == ObjectiveType.BuildStructure ? (UnityEngine.Object)objective.Building : objective.Item, objective.Amount));
            Assert.AreEqual(QuestStatus.Completed, log.StatusOf(chapter.Quest));
        }
        Assert.IsNull(Definition.Chapter(log));
        Assert.IsTrue(log.HasHappened(Definition.FinishedEvent), "The hold objective is the actual finished story event.");
    }

    [Test]
    public void LaterChapters_CannotStartBeforeTheirPrerequisites()
    {
        foreach (PrologueChapter chapter in Definition.Chapters.Skip(1))
            Assert.IsFalse(new QuestLog().TryStart(chapter.Quest), chapter.Quest.name);
    }

    [Test]
    public void PeacefulChapters_NeedLeaderAcceptanceButEmergenciesRemainAutomatic()
    {
        // The leader's briefings wait to be accepted; what a companion calls out ("Eirik: ...", "Runa: Wolves!") or a
        // story tells starts at once.
        foreach (var chapter in Definition.Chapters)
        {
            int colon = chapter.ArrivalText.IndexOf(':');
            bool spokenByCompanion = colon > 0 && colon < 16;
            bool briefing = !string.IsNullOrWhiteSpace(chapter.ArrivalText) && !spokenByCompanion;
            Assert.AreEqual(briefing ? QuestStart.ByQuestGiver : QuestStart.Automatically, chapter.Quest.Start, chapter.Quest.name);
            Assert.IsFalse(chapter.Quest.TurnInToGiver, "The next briefing is the handoff; avoid two separate leader interactions.");
        }
    }

    [Test]
    public void Campfire_RequiresARealBuildAndGatheringCoversItsSingleCost()
    {
        var log = new QuestLog();
        foreach (var chapter in Definition.Chapters.Take(2)) log.ForceComplete(chapter.Quest);
        var fire = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.LightFire).Quest;
        Assert.IsTrue(log.TryStart(fire));
        log.Handle(new GameEvent(GameEventKind.Custom, Definition.FireEvent));
        Assert.AreEqual(QuestStatus.Active, log.StatusOf(fire), "The old use-fire shortcut must not complete construction.");
        Assert.AreSame(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Campfire.asset"),
            Definition.Campfire, "The expedition builds the same campfire the player can build at home.");
        Assert.IsTrue(Definition.Campfire.ShowInBuildMenu);
        var gather = Definition.Chapters[1].Quest;
        foreach (var cost in Definition.Campfire.ConstructionItems)
            Assert.AreEqual(cost.Amount, gather.Objectives.Single(o => o.Item == cost.Item).Amount);
        log.Handle(new GameEvent(GameEventKind.Built, Definition.Campfire));
        Assert.AreEqual(QuestStatus.Completed, log.StatusOf(fire));
        Assert.AreEqual(QuestStatus.NotStarted, log.StatusOf(Definition.Chapters[3].Quest));
    }

    [Test]
    public void StartingArmor_IsACompleteHideSetAndEncounterActorsDoNotPersist()
    {
        // Head, body (which covers the legs), hands and feet.
        Assert.AreEqual(4, Definition.StartingArmor.Length);
        Assert.IsTrue(Definition.StartingArmor.All(i => i != null && i.IsArmor && i.name.StartsWith("WovenHide")));
        Assert.AreEqual(4, Definition.StartingArmor.Select(i => i.ArmorSlot).Distinct().Count());
        Assert.IsEmpty(Definition.WolfPrefab.GetComponentsInChildren<SaveableEntity>(true),
            "Encounters reset on load; only placed construction and gathering nodes persist.");
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
        var rescue = AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root + "/Quests/TheyAreStillOutThere.asset");
        Assert.AreEqual(2, rescue.Objectives.Count);
        Assert.IsTrue(rescue.Objectives.All(o => o.CountAlreadyOwned));
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
            Assert.AreEqual(1, companions.Count(c => c.GetComponent<QuestGiver>() != null));
            foreach (var companion in companions)
            {
                Assert.AreEqual(1, companion.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Expedition Torch"));
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
    public void Definitions_AreNotChangedByQuestProgress()
    {
        string before = EditorJsonUtility.ToJson(Definition);
        var log = new QuestLog();
        log.TryStart(Definition.Chapters[0].Quest);
        log.Handle(new GameEvent(GameEventKind.Custom, Definition.ShelterEvent));
        Assert.AreEqual(before, EditorJsonUtility.ToJson(Definition));
        Assert.AreSame(Definition.Chapters[1], Definition.Chapter(log));
    }

    [Test]
    public void Designer_CanAddAChapterWithoutChangingRuntimeCode()
    {
        var copy = UnityEngine.Object.Instantiate(Definition);
        try
        {
            var rescue = AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root + "/Quests/TheyAreStillOutThere.asset");
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
}
