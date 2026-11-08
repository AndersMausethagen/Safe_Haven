using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The sunlit start of the prologue: the shared features it needs (quests that close, side offers, catching fish, a party's
/// walk) and that what it authored is sound.
/// </summary>
public sealed class PrologueMeadowTests
{
    private const string Root = "Assets/_Project/Data/Prologue/";
    private const string ScenePath = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    private static PrologueDefinition Definition => AssetDatabase.LoadAssetAtPath<PrologueDefinition>(Root + "ForestPrologue.asset");
    private static QuestDefinition Quest(string name) => AssetDatabase.LoadAssetAtPath<QuestDefinition>(Root + "Quests/" + name + ".asset");

    private static QuestDefinition Make(string name, QuestDefinition closedBy = null, bool turnIn = false, ObjectiveType type = ObjectiveType.QuestEvent, Object target = null)
    {
        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        quest.name = name;
        var data = new SerializedObject(quest);
        data.FindProperty("closedBy").objectReferenceValue = closedBy;
        data.FindProperty("turnInToGiver").boolValue = turnIn;
        data.FindProperty("start").intValue = (int)QuestStart.ByQuestGiver;
        SerializedProperty objectives = data.FindProperty("objectives");
        objectives.arraySize = 1;
        SerializedProperty objective = objectives.GetArrayElementAtIndex(0);
        objective.FindPropertyRelative("type").intValue = (int)type;
        objective.FindPropertyRelative("amount").intValue = 1;
        objective.FindPropertyRelative("item").objectReferenceValue = target as ItemDefinition;
        data.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    // ----- Quests that close when the story moves on -----

    [Test]
    public void ClosedQuest_IsNoLongerOfferedOrStartable()
    {
        QuestDefinition road = Make("road"), errand = Make("errand", road);
        try
        {
            var log = new QuestLog();
            Assert.AreEqual(QuestGiverState.Available, QuestGiver.StateFor(errand, log));
            Assert.IsTrue(log.TryStart(road));
            Assert.IsTrue(log.IsClosed(errand));
            Assert.AreEqual(QuestGiverState.None, QuestGiver.StateFor(errand, log), "Nobody offers it once the road has begun.");
            Assert.IsFalse(log.TryStart(errand));
        }
        finally
        {
            Object.DestroyImmediate(road);
            Object.DestroyImmediate(errand);
        }
    }

    [Test]
    public void LapsedQuest_LeavesTheLogWithoutCountingAnyMoreAndIsSaved()
    {
        QuestDefinition errand = Make("errand", null, true);
        try
        {
            var log = new QuestLog();
            Assert.IsTrue(log.TryStart(errand));
            bool lapsed = false;
            log.Lapsed += _ => lapsed = true;
            Assert.IsTrue(log.TryLapse(errand));
            Assert.IsTrue(lapsed);
            Assert.AreEqual(QuestStatus.Lapsed, log.StatusOf(errand));
            Assert.AreEqual(QuestGiverState.None, QuestGiver.StateFor(errand, log));
            Assert.IsFalse(log.TryStart(errand), "A lapsed quest doesn't come back.");
            Assert.IsFalse(log.TryLapse(errand));

            var restored = new QuestLog();
            restored.Restore(errand, QuestStatus.Lapsed, new[] { 0 });
            Assert.AreEqual(QuestStatus.Lapsed, restored.StatusOf(errand));
        }
        finally
        {
            Object.DestroyImmediate(errand);
        }
    }

    [Test]
    public void ClosedBy_ReportsWhatCanNeverWork()
    {
        QuestDefinition quest = Make("quest"), other = Make("other");
        try
        {
            var data = new SerializedObject(quest);
            data.FindProperty("closedBy").objectReferenceValue = quest;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(quest.FindProblems().Any(p => p.Contains("closed by itself")));

            data.FindProperty("closedBy").objectReferenceValue = other;
            data.FindProperty("prerequisites").arraySize = 1;
            data.FindProperty("prerequisites").GetArrayElementAtIndex(0).objectReferenceValue = other;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.IsTrue(quest.FindProblems().Any(p => p.Contains("closes it")));
        }
        finally
        {
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(other);
        }
    }

    // ----- Catching fish -----

    [Test]
    public void CatchFish_CountsAnyFishOrTheNamedKind_NotFishFromElsewhere()
    {
        var trout = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Fish/BrookTrout.asset");
        var perch = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Fish/Perch.asset");
        Assert.NotNull(trout);
        Assert.NotNull(perch);
        QuestDefinition any = Make("any", null, false, ObjectiveType.CatchFish), one = Make("one", null, false, ObjectiveType.CatchFish, trout);
        try
        {
            QuestObjective anyFish = any.Objectives[0], named = one.Objectives[0];
            Assert.AreEqual(1, anyFish.Progress(new GameEvent(GameEventKind.FishCaught, perch)));
            Assert.AreEqual(0, named.Progress(new GameEvent(GameEventKind.FishCaught, perch)));
            Assert.AreEqual(1, named.Progress(new GameEvent(GameEventKind.FishCaught, trout)));
            Assert.AreEqual(0, anyFish.Progress(new GameEvent(GameEventKind.ItemObtained, perch)), "Bought or found fish isn't caught fish.");
            StringAssert.Contains("fish", anyFish.Describe());
            StringAssert.Contains(trout.DisplayName, named.Describe());
        }
        finally
        {
            Object.DestroyImmediate(any);
            Object.DestroyImmediate(one);
        }
    }

    // ----- The party's walk -----

    [Test]
    public void PartyWalk_MovesSteadilyAlongTheRoute_AndNeverPastItsEnd()
    {
        var walk = new PartyWalk(new[] { Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f) });
        Assert.AreEqual(20f, walk.Length, 0.001f);
        walk.Advance(5f);
        Assert.AreEqual(0.25f, walk.Progress, 0.001f);
        Assert.AreEqual(new Vector3(5f, 0f, 0f), walk.Position);
        Assert.AreEqual(Vector3.right, walk.Heading());
        Assert.AreEqual(new Vector3(10f, 0f, 5f), walk.Ahead(10f), "Looks ahead around the corner.");
        walk.Advance(-3f);
        Assert.AreEqual(0.25f, walk.Progress, 0.001f, "It doesn't walk backwards.");
        walk.Advance(100f);
        Assert.IsTrue(walk.IsDone);
        Assert.AreEqual(1f, walk.Progress, 0.001f);
        Assert.AreEqual(new Vector3(10f, 0f, 10f), walk.Ahead(50f));
    }

    [Test]
    public void ForwardTo_TheSunNeverGoesBackwards()
    {
        Assert.AreEqual(22f, DayNightCycle.ForwardTo(8f, 22f), 0.001f);
        Assert.AreEqual(29f, DayNightCycle.ForwardTo(22f, 5f), 0.001f, "Morning comes after night, the long way round.");
    }

    // ----- Chapters -----

    [Test]
    public void StoryChapters_SayWhereTheyAreAndWhatTheCompanionsDo()
    {
        PrologueChapter morning = Definition.Chapters[0], road = Definition.Chapters[1], treeLine = Definition.Chapters[2];
        Assert.AreSame(Quest("MorningCamp"), morning.Quest);
        Assert.AreEqual(PrologueSite.Meadow, morning.Checkpoint);
        Assert.AreEqual(PrologueCompanions.StandStill, morning.Companions);
        Assert.IsTrue(morning.AtRest, "There is time to talk by the fire.");

        Assert.AreSame(Quest("RoadToForest"), road.Quest);
        Assert.AreEqual(PrologueSite.Meadow, road.Checkpoint, "Loading in the middle of the walk starts it again from the camp.");
        Assert.AreEqual(PrologueCompanions.StoryLeads, road.Companions);
        Assert.IsFalse(road.AtRest);

        Assert.AreSame(Quest("TreeLine"), treeLine.Quest);
        Assert.AreEqual(PrologueSite.Forest, treeLine.Checkpoint);
        Assert.AreEqual(PrologueCompanions.WatchOnly, treeLine.Companions);

        PrologueChapter shelter = Definition.Chapters.Single(c => c.Behaviour == PrologueStep.FindShelter);
        Assert.AreEqual(PrologueSite.Forest, shelter.Checkpoint);
        Assert.AreEqual(PrologueCompanions.Follow, shelter.Companions);
        CollectionAssert.AreEqual(new[] { Quest("TreeLine") }, shelter.Quest.Prerequisites);
    }

    [Test]
    public void SideQuests_CloseWhenTheExpeditionSetsOut_AndNoneIsAChapter()
    {
        foreach (string name in new[] { "HuntForThePot", "FishForBreakfast", "WoodForTheRoad" })
        {
            QuestDefinition quest = Quest(name);
            Assert.AreSame(Quest("RoadToForest"), quest.ClosedBy, name);
            Assert.AreEqual(QuestStart.ByQuestGiver, quest.Start, name);
            Assert.IsTrue(quest.TurnInToGiver, name);
            Assert.IsEmpty(quest.FindProblems(), name);
            Assert.IsFalse(Definition.Chapters.Any(c => c.Quest == quest), name);
        }
    }

    [Test]
    public void WeaponChoice_OffersEveryTierOneWeaponType()
    {
        var names = new[] { "Items/Weapons/WornSword", "Items/WoodsmansAxe", "Items/Weapons/FireHardenedSpear", "Items/Weapons/KnottedClub", "Items/CrudeWoodenBow" };
        foreach (string name in names)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/" + name + ".asset");
            Assert.NotNull(item, name);
            Assert.NotNull(item.Weapon, name);
            Assert.AreEqual(1, new SerializedObject(item).FindProperty("tier").intValue, name);
        }

        var sword = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Weapons/WornSword.asset");
        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        Assert.IsTrue(database.Items.Contains(sword), "New items must be in the content database, or saves can't find them.");
    }

    // ----- Playtest fixes -----

    [Test]
    public void FirstQuest_WaitsForThePlayerToTalkToSigrid()
    {
        QuestDefinition first = Definition.Chapters[0].Quest;
        Assert.AreEqual(QuestStart.ByQuestGiver, first.Start, "The player starts the story by talking to Sigrid.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.AvailableObjective), "The tracker says who to talk to.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(Definition.Chapters[0].ArrivalText));
    }

    [Test]
    public void Ranks_TellTheStoryFromSideQuestsAndErrands()
    {
        foreach (PrologueChapter chapter in Definition.Chapters)
        {
            Assert.AreEqual(QuestRank.MainStory, chapter.Quest.Rank, chapter.Quest.name);
        }

        foreach (string name in new[] { "HuntForThePot", "FishForBreakfast", "WoodForTheRoad" })
        {
            Assert.AreEqual(QuestRank.Optional, Quest(name).Rank, name);
        }

        Assert.AreEqual(QuestRank.SideQuest, AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/SmithsRequest.asset").Rank);
        QuestMarkerSettings settings = QuestMarkerSettings.Current;
        Assert.AreNotEqual(settings.RankColor(QuestRank.MainStory), settings.RankColor(QuestRank.SideQuest));
        Assert.AreNotEqual(settings.RankColor(QuestRank.SideQuest), settings.RankColor(QuestRank.Optional));
        Assert.IsTrue(settings.TryGetLook(QuestGiverState.Available, QuestRank.Optional, out _, out Color available));
        Assert.AreEqual(settings.RankColor(QuestRank.Optional), available, "A giver's ! takes the colour of the quest on offer.");
    }

    [Test]
    public void DialogueAnswers_SayWhatTheyDoToAQuest()
    {
        QuestDefinition errand = Quest("HuntForThePot"), story = Quest("RoadToForest");
        Assert.IsNull(new DialogueChoice("Goodbye.").QuestTag(), "An ordinary answer has no tag.");
        StringAssert.Contains("Starts optional quest: " + errand.DisplayName, new DialogueChoice("Yes", null, true, errand, QuestAnswer.Starts).QuestTag());
        StringAssert.Contains("Starts main quest: " + story.DisplayName, new DialogueChoice("Yes", null, true, story, QuestAnswer.Starts).QuestTag());
        StringAssert.Contains("Hands in: " + errand.DisplayName, new DialogueChoice("Done", null, true, errand, QuestAnswer.HandsIn).QuestTag());
        StringAssert.Contains(QuestMarkerSettings.Current.RankHex(errand.Rank), new DialogueChoice("Yes", null, true, errand, QuestAnswer.Starts).QuestTag());
    }

    [Test]
    public void SavedScene_KeepsPlayerAndViewInsideTheTerrain_AndWaterIsDrinkable()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var roots = scene.GetRootGameObjects();
            Assert.IsFalse(roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Any(t => t.name == "View Bounds"),
                "The camera follows the player to the edge; the walls keep them in.");
            Transform West(string name) => roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).First(t => t.name == name && t.parent != null && t.parent.name == "Boundary");
            Terrain meadow = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single(t => t.transform.position.x < -100f);
            Assert.Greater(West("West Wall").position.x, meadow.transform.position.x + 5f, "Nobody walks to where the ground runs out.");
            Assert.Less(West("North Wall").position.z, 95f);

            WaterSurface[] water = roots.SelectMany(r => r.GetComponentsInChildren<WaterSurface>(true)).ToArray();
            Assert.GreaterOrEqual(water.Length, 3);
            Assert.IsTrue(water.Where(w => w.GetComponent<MeshRenderer>() != null && w.TakesRipples).All(w => w.Drinkable), "The lake, the river and the stream.");

            MobSpawnZone boars = roots.SelectMany(r => r.GetComponentsInChildren<MobSpawnZone>(true)).Single(z => z.name == "Meadow Boars");
            Assert.AreEqual(MobSpawnTrigger.Manual, boars.Trigger, "They come with the hunt.");
            foreach (BiomeArea grove in roots.SelectMany(r => r.GetComponentsInChildren<BiomeArea>(true)))
            {
                Assert.IsFalse(grove.WorldBounds().Contains(new Vector3(boars.transform.position.x, grove.WorldBounds().center.y, boars.transform.position.z)),
                    "Under an oak's crown they can't be seen from above: " + grove.name);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // ----- The playtest's fixes -----

    [Test]
    public void Footprints_OnEveryTerrain_AreTheHavens()
    {
        var haven = AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>("Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain Surface Profile.asset");
        var brandNew = ScriptableObject.CreateInstance<TerrainSurfaceProfile>();
        try
        {
            foreach (TerrainSurfaceProfile profile in new[] { brandNew,
                AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(Root + "Vegetation/Prologue Meadow Terrain Surface Profile.asset"),
                AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(Root + "Vegetation/Prologue Forest Terrain Surface Profile.asset") })
            {
                Assert.AreEqual(haven.footprintDepth, profile.footprintDepth, 0.0001f);
                Assert.AreEqual(haven.footprintFlattening, profile.footprintFlattening, 0.0001f);
                Assert.AreEqual(haven.footprintNormalFlattening, profile.footprintNormalFlattening, 0.0001f);
                Assert.AreEqual(haven.footprintDarkening, profile.footprintDarkening, 0.0001f);
                Assert.AreEqual(haven.footprintGrassFlattening, profile.footprintGrassFlattening, 0.0001f);
                foreach (TerrainSurfaceProfile.Layer layer in profile.layers)
                {
                    TerrainSurfaceProfile.Layer same = haven.Find(layer.terrainLayer);
                    if (same != null) Assert.AreEqual(same.footprintResponse, layer.footprintResponse, 0.0001f, layer.terrainLayer.name);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(brandNew);
        }

        var havenGround = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain Displacement.mat");
        foreach (string ground in new[] { "Meadow", "Forest" })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Vegetation/Prologue " + ground + " Terrain Displacement.mat");
            foreach (string property in new[] { "_DisplacementStrength", "_DisplacementNear", "_DisplacementFar", "_TessellationFactor", "_TessellationEdgeLength" })
                Assert.AreEqual(havenGround.GetFloat(property), material.GetFloat(property), 0.0001f, ground + " " + property);
        }
    }

    [Test]
    public void ThePlayer_StartsWithAGoodBackpack()
    {
        ItemDefinition pack = Definition.StartingBackpack;
        Assert.NotNull(pack);
        Assert.IsTrue(pack.IsBackpack);
        Assert.GreaterOrEqual(pack.BagSlotBonus, 16);
    }

    [Test]
    public void WoodQuest_GivesATool_AndTheFightingAxeDoesNotPretend()
    {
        var hatchet = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/StoneHatchet.asset");
        Assert.AreEqual(ToolType.Axe, hatchet.ToolType);
        Assert.GreaterOrEqual(hatchet.ToolTier, 1);
        var axe = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/WoodsmansAxe.asset");
        Assert.AreEqual("Hand Axe", axe.DisplayName);
        Assert.AreEqual("woodsmans_axe", axe.Id, "Saves know it by this.");
    }

    [Test]
    public void Bow_AimsWhereTheCursorPassesAtTheArrowsHeight()
    {
        var ray = new Ray(new Vector3(0f, 20f, -20f), new Vector3(0f, -1f, 1f).normalized);
        Vector3 atBow = ArcheryRules.PointAtHeight(ray, 1.5f, Vector3.zero);
        Assert.AreEqual(1.5f, atBow.y, 0.001f);
        Assert.AreEqual(-1.5f, atBow.z, 0.001f, "Nearer the camera than the spot on the ground the ray reaches.");
        Assert.AreEqual(Vector3.one, ArcheryRules.PointAtHeight(new Ray(Vector3.zero, Vector3.up), 1.5f, Vector3.one), "A ray that never gets there.");
    }

    [Test]
    public void TimeFade_ShadowsAreGoneWhileTheLightTurns()
    {
        var early = new DayNightState { LightRotation = Quaternion.Euler(30f, 0f, 0f), ShadowStrength = 1f };
        var late = new DayNightState { LightRotation = Quaternion.Euler(30f, 120f, 0f), ShadowStrength = 1f };
        Assert.AreEqual(1f, DayNightState.Crossfade(early, late, 0f).ShadowStrength, 0.001f);
        Assert.AreEqual(0f, DayNightState.Crossfade(early, late, 0.5f).ShadowStrength, 0.001f);
        Assert.AreEqual(1f, DayNightState.Crossfade(early, late, 1f).ShadowStrength, 0.001f);
        Assert.Less(Quaternion.Angle(DayNightState.Crossfade(early, late, 0.3f).LightRotation, early.LightRotation), 0.01f, "Still where it was while its shadows can be seen.");
        Assert.Less(Quaternion.Angle(DayNightState.Crossfade(early, late, 0.7f).LightRotation, late.LightRotation), 0.01f);
    }

    [Test]
    public void RiverMouth_WidensTowardsTheShore()
    {
        Assert.AreEqual(0f, RiverBuilder.MouthShare(float.PositiveInfinity, 10f));
        Assert.AreEqual(0f, RiverBuilder.MouthShare(10f, 10f), 0.001f);
        Assert.AreEqual(1f, RiverBuilder.MouthShare(0f, 10f), 0.001f);
        Assert.AreEqual(1f, RiverBuilder.MouthShare(-3f, 10f), 0.001f, "In the pond itself.");
        Assert.Greater(RiverBuilder.MouthShare(3f, 10f), RiverBuilder.MouthShare(7f, 10f));
    }

    [Test]
    public void Shore_IsTheCarvedOutline()
    {
        var pond = GameObject.CreatePrimitive(PrimitiveType.Plane);
        try
        {
            pond.transform.localScale = new Vector3(3f, 1f, 2f);
            Bounds local = pond.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.Less(WaterSetup.ShoreDistance(pond.transform, local, true, 0f, 0f), -5f, "The middle.");
            Assert.Greater(WaterSetup.ShoreDistance(pond.transform, local, true, 40f, 0f), 5f, "Far outside.");
            Assert.AreEqual(5f, WaterSetup.ShoreDistance(pond.transform, local, false, 20f, 0f), 0.01f, "A box pond: 5 m past its 15 m half width.");
        }
        finally
        {
            Object.DestroyImmediate(pond);
        }
    }

    // ----- The second telling -----

    [Test]
    public void ForestMoon_IsBrighterThanTheRaidsNight_WhichStaysAsItWas()
    {
        var raid = AssetDatabase.LoadAssetAtPath<DayNightProfile>("Assets/_Project/Data/Environment/Misty Woods Day Night.asset");
        var forest = AssetDatabase.LoadAssetAtPath<DayNightProfile>(Root + "Prologue Forest Night.asset");
        Assert.NotNull(forest);
        Assert.AreEqual(1.6f, raid.Evaluate(23f).LightIntensity, 0.01f, "The raid's own night isn't touched.");
        Assert.Greater(forest.Evaluate(23f).LightIntensity, raid.Evaluate(23f).LightIntensity);
        Assert.Greater(forest.Evaluate(23f).AmbientSky.b, raid.Evaluate(23f).AmbientSky.b);
        Assert.AreEqual(raid.Evaluate(11f).LightIntensity, forest.Evaluate(11f).LightIntensity, 0.01f, "Only the night changes.");
    }

    [Test]
    public void EirikLesson_CanBeTakenAfterTheCrossing_AndClosesWithTheShelter()
    {
        QuestDefinition lesson = Quest("EirikLesson");
        Assert.AreEqual(QuestRank.Optional, lesson.Rank);
        Assert.AreSame(Quest("FindShelter"), lesson.ClosedBy);
        CollectionAssert.AreEqual(new[] { Quest("RoadToForest") }, lesson.Prerequisites, "It must be over before the shelter quest, so it can't wait for that.");
        Assert.IsTrue(lesson.ObjectivesInOrder);
        Assert.AreEqual(4, lesson.Objectives.Count);
        Assert.IsEmpty(lesson.FindProblems());
        Assert.AreEqual(PrologueCompanions.WatchOnly, Definition.Chapters[2].Companions, "Nobody kills the practice target for the player.");
    }

    [Test]
    public void Errands_MakeLaterQuestsShorter()
    {
        var sticks = Definition.Sticks;
        QuestDefinition wood = Quest("WoodForTheRoad"), fuel = Quest("GatherFuel");
        Assert.IsTrue(wood.RewardItems.Any(r => r.Item == sticks && r.Amount == 4), "The wood is the fire's kindling.");
        Assert.IsTrue(fuel.Objectives.Single(o => o.Item == sticks).CountAlreadyOwned);
    }

    [Test]
    public void TheNewNodes_ReportWhatIsMissing()
    {
        Assert.IsNotNull(new WaitForEventNode().FindProblem());
        Assert.IsNotNull(new QuestProgressNode().FindProblem());
        Assert.IsNotNull(new WhenObjectHit().FindProblem());
        Assert.IsNotNull(new DespawnEnemiesNode().FindProblem());
        Assert.IsNull(new QuestProgressNode { quest = Quest("BerriesForThePot"), objective = 1 }.FindProblem());
        Assert.IsNotNull(new QuestProgressNode { quest = Quest("BerriesForThePot"), objective = 9 }.FindProblem());
    }

    [Test]
    public void SavedScene_HasTheNewBeats()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var roots = scene.GetRootGameObjects();
            var stories = roots.SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)).ToArray();
            foreach (string name in new[] { "Story - The Old Troll Returns", "Story - The Handcart", "Story - The Road Behind", "Story - A Lesson from Eirik",
                "Story - A Moment of Warmth", "Story - They Remember", "Story - What Sigrid Knows", "Story - The Stone by the Lake" })
            {
                StoryScript story = stories.SingleOrDefault(s => s.name == name);
                Assert.NotNull(story, name);
                Assert.IsEmpty(story.FindProblems(), name);
                Assert.NotNull(story.GetComponent<SaveableEntity>(), name);
            }

            Assert.AreEqual(2, stories.Single(s => s.name == "Story - The Road East").Nodes.OfType<WalkPartyNode>().Count(), "The walk stops at the cart.");
            StoryScript intro = stories.Single(s => s.name == "Story - The Morning Fire");
            Assert.IsTrue(intro.Nodes.OfType<SayNode>().Any(n => n.line.Contains("Ingrid") && n.line.Contains("Liv")), "The player learns who they are looking for.");
            Assert.AreEqual(3, intro.Nodes.OfType<ChoiceNode>().Single().answers.Count, "Why the player came.");
            // Around a fight a word from someone is enough: these speak in asides and never turn the camera.
            foreach (string fight in new[] { "Story - The Old Troll", "Story - The Old Troll Returns", "Story - Rootlings on the Road", "Story - Troll Ambush" })
                Assert.IsTrue(stories.Single(s => s.name == fight).IsFight, fight);
            Assert.IsFalse(stories.Single(s => s.name == "Story - The Morning Fire").IsFight, "a conversation by the fire stays one");
            CreatureHuntsNode troll = stories.Single(s => s.name == "Story - The Old Troll").Nodes.OfType<CreatureHuntsNode>().Single();
            Assert.Less(troll.walkSpeed, 0.5f, "The old troll walks to the camp, slow and heavy.");
            Assert.Greater(troll.stayWithin, 0f, "It stays at the camp and doesn't chase the party up the path.");
            InvokeEventNode swing = stories.Single(s => s.name == "Story - The Old Troll Returns").Nodes.OfType<InvokeEventNode>().Single();
            Assert.AreEqual(1, swing.response.GetPersistentEventCount(), "The troll's blow is wired to its animator.");

            var behind = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "The Road Behind");
            Assert.IsFalse(behind.gameObject.activeSelf, "The way back only closes once the player is inside.");
            MobSpawnZone road = roots.SelectMany(r => r.GetComponentsInChildren<MobSpawnZone>(true)).Single(z => z.name == "Rootlings on the Road");
            Assert.AreEqual("ExpeditionRootling", road.Entries[0].prefab.name, "Not wolves again: the camp has those.");
            foreach (string found in new[] { "Story - The Stone by the Lake", "Story - The Handcart" })
            {
                SayNode reading = stories.Single(s => s.name == found).Nodes.OfType<SayNode>().First();
                Assert.AreEqual("Narrator", reading.speaker.Describe(), "What the player finds isn't the player talking: " + found);
            }

            StringAssert.DoesNotContain("35", roots.SelectMany(r => r.GetComponentsInChildren<PrologueCompanion>(true)).Single(c => c.Resident.DisplayName == "Sigrid")
                .GetComponent<QuestGiver>().Offers.Single(o => o.Quest == Quest("CatchBreath")).Text, "The rest has no countdown any more.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // ----- The saved scene -----

    [Test]
    public void SavedScene_HasTheMeadowAndEverythingTheStoryNeeds()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var roots = scene.GetRootGameObjects();
            Terrain[] terrains = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
            Assert.AreEqual(2, terrains.Length);
            Terrain meadow = terrains.Single(t => t.transform.position.x < -100f);
            Terrain forest = terrains.Single(t => t.transform.position.x >= -100f);
            Assert.AreEqual(forest.transform.position.x, meadow.transform.position.x + meadow.terrainData.size.x, 0.01f, "The two terrains share an edge.");
            float[,] west = forest.terrainData.GetHeights(0, 0, 1, forest.terrainData.heightmapResolution);
            float[,] east = meadow.terrainData.GetHeights(meadow.terrainData.heightmapResolution - 1, 0, 1, meadow.terrainData.heightmapResolution);
            for (int row = 0; row < west.GetLength(0); row += 8)
            {
                // In world heights: the World Builder can lower a terrain to make room for a river bed, without moving the ground.
                Assert.AreEqual(west[row, 0] * forest.terrainData.size.y + forest.transform.position.y,
                    east[row, 0] * meadow.terrainData.size.y + meadow.transform.position.y, 0.05f, "No step at the seam, row " + row);
            }

            WaterSurface lake = roots.SelectMany(r => r.GetComponentsInChildren<WaterSurface>(true)).Single(w => w.name == "Morning Lake");
            Assert.IsTrue(lake.IsStill, "Fishing needs still water to bite well.");

            var director = roots.SelectMany(r => r.GetComponentsInChildren<PrologueDirector>(true)).Single();
            var directorData = new SerializedObject(director);
            var camp = (Transform)directorData.FindProperty("meadowPoint").objectReferenceValue;
            Assert.NotNull(camp);
            Assert.Less(camp.position.x, meadow.transform.position.x + 60f, "The game starts in the north-west of the meadow.");

            var look = roots.SelectMany(r => r.GetComponentsInChildren<SceneLook>(true)).Single();
            var lookData = new SerializedObject(look);
            Assert.AreEqual(0f, lookData.FindProperty("startAmount").floatValue, "The scene starts in the sun.");
            SerializedProperty pairs = lookData.FindProperty("volumes");
            Assert.AreEqual(4, pairs.arraySize);
            var raidBase = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Project/Settings/Rendering/Volumes/MistyWoods_Raid_Volume.asset");
            var forestBase = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(Root + "Prologue Forest Look.asset");
            var second = (UnityEngine.Rendering.Volume)pairs.GetArrayElementAtIndex(0).FindPropertyRelative("second").objectReferenceValue;
            Assert.AreSame(forestBase, second.sharedProfile, "Across the line it is the raid's post-processing, with a thinner night veil.");
            Assert.IsTrue(raidBase.TryGet(out FogOfWar raidVeil) && forestBase.TryGet(out FogOfWar forestVeil) && forestVeil.nightVeil.value < raidVeil.nightVeil.value);
            Assert.AreEqual(0.85f, raidVeil.nightVeil.value, 0.001f, "The raid's own veil isn't touched.");

            var entered = AssetDatabase.LoadAssetAtPath<GameEventDefinition>(Root + "Events/ForestEntered.asset");
            var reaction = roots.SelectMany(r => r.GetComponentsInChildren<GameEventListener>(true))
                .Single(l => new SerializedObject(l).FindProperty("gameEvent").objectReferenceValue == entered);
            var reactionData = new SerializedObject(reaction);
            Assert.IsTrue(reactionData.FindProperty("respondIfAlreadyHappened").boolValue, "Loading in the forest brings the night and the mist back.");
            SerializedProperty actions = reactionData.FindProperty("actions");
            var kinds = Enumerable.Range(0, actions.arraySize).Select(i => (SceneEventActionKind)actions.GetArrayElementAtIndex(i).FindPropertyRelative("kind").intValue).ToArray();
            CollectionAssert.Contains(kinds, SceneEventActionKind.ChangeLook);
            CollectionAssert.Contains(kinds, SceneEventActionKind.ChangeWeather);
            CollectionAssert.Contains(kinds, SceneEventActionKind.ChangeTimeOfDay);

            var stories = roots.SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)).ToArray();
            foreach (StoryScript story in stories)
            {
                Assert.IsEmpty(story.FindProblems(), story.name);
            }

            Assert.IsTrue(stories.SelectMany(s => s.Nodes).OfType<WalkPartyNode>().Any(), "The expedition walks to the forest in a story.");
            Assert.IsFalse(stories.Any(s => s.name == "Story - Bow or Axe"), "The weapon is chosen at the tree line now, from all the types.");

            var givers = roots.SelectMany(r => r.GetComponentsInChildren<QuestGiver>(true)).ToArray();
            Assert.IsTrue(givers.Any(g => g.SideOffers.Any(o => o.Quest == Quest("HuntForThePot"))), "Runa offers the hunt.");
            Assert.IsTrue(givers.Any(g => g.SideOffers.Any(o => o.Quest == Quest("FishForBreakfast"))), "Eirik offers the fishing.");
            Assert.IsTrue(givers.Any(g => g.SideOffers.Any(o => o.Quest == Quest("WoodForTheRoad"))), "Eirik offers the wood.");
            foreach (QuestGiver giver in givers)
            {
                var parts = giver.GetComponents<IConversationTopics>().ToList();
                Assert.Less(parts.IndexOf(giver), parts.IndexOf(giver.GetComponent<PrologueCompanion>()), giver.name + ": the quest speaks before the small talk.");
            }

            Assert.IsFalse(roots.SelectMany(r => r.GetComponentsInChildren<ExpeditionTorch>(true)).Where(t => t.transform.parent != null)
                .Any(t => t.gameObject.activeSelf && t.GetComponentInParent<PrologueCompanion>(true).gameObject.activeSelf), "Nobody holds a lit torch in the morning sun.");
            Assert.IsTrue(roots.SelectMany(r => r.GetComponentsInChildren<BoxCollider>(true)).Any(b => b.name == "Forest Threshold"), "Nobody goes in before the expedition does.");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void SavedScene_UsesItsOwnTrunksSoTheRaidsMapStaysAsItIs()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var holder = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Single(t => t.name == RaidMapSurface.TrunksName);
            Assert.AreEqual(Root + "Vegetation/Prologue Forest Trunks.asset", AssetDatabase.GetAssetPath(holder.GetComponent<MeshCollider>().sharedMesh));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
