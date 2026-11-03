using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CompanionAmnesiaTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object one in made)
        {
            Object.DestroyImmediate(one);
        }

        made.Clear();
    }

    private ResidentDefinition Person(string id, bool walksRaids = true)
    {
        var person = ScriptableObject.CreateInstance<ResidentDefinition>();
        person.name = id;
        made.Add(person);
        var data = new SerializedObject(person);
        data.FindProperty("id").stringValue = id;
        if (walksRaids)
        {
            var how = ScriptableObject.CreateInstance<CompanionDefinition>();
            made.Add(how);
            data.FindProperty("raidCompanion").objectReferenceValue = how;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return person;
    }

    private static void Along(SettlementState settlement, ResidentDefinition person, int helped = 0, int bosses = 0, int every = 2)
    {
        settlement.RecordRaidAlong(person, null, true, bosses, helped, 500f, 0, every);
    }

    [Test]
    public void FoundAgainTheyAreAStrangerWithNoBondAndNoJob()
    {
        var settlement = new SettlementState();
        ResidentDefinition sigrid = Person("sigrid");
        var job = ScriptableObject.CreateInstance<JobDefinition>();
        made.Add(job);
        settlement.AddResident(sigrid, job);
        for (int i = 0; i < 9; i++)
        {
            Along(settlement, sigrid);
        }

        settlement.LoseResident(sigrid, "The Misty Woods");
        Assert.IsTrue(settlement.FindAgain(sigrid));

        ResidentRecord record = settlement.Find(sigrid);
        Assert.AreEqual(0, record.RaidsAlong, "Back to the start");
        Assert.AreEqual(0, record.KillsAlong);
        Assert.IsNull(record.Job);
        Assert.IsTrue(record.IsStranger);
        Assert.IsFalse(settlement.IsFallen(sigrid));
        CollectionAssert.Contains(settlement.VacantJobs, job, "Their old work stays open to anyone, them included");
        CollectionAssert.Contains(settlement.JobsOpenTo(sigrid), job);
    }

    [Test]
    public void OnlyTheLostCanBeFoundAgain()
    {
        var settlement = new SettlementState();
        ResidentDefinition sigrid = Person("sigrid");
        Assert.IsFalse(settlement.FindAgain(sigrid));
        settlement.AddResident(sigrid);
        Assert.IsFalse(settlement.FindAgain(sigrid), "They never left");
        Assert.AreEqual(1, settlement.Residents.Count);
        Assert.IsFalse(settlement.FindAgain(null));
    }

    [Test]
    public void TheyFindTheLongestLostFirstAndOnlyThoseWhoWalkRaids()
    {
        var settlement = new SettlementState();
        ResidentDefinition first = Person("first");
        ResidentDefinition second = Person("second");
        Assert.IsNull(settlement.LostToFind);
        settlement.RestoreFallen(Person("smith", false), "Long ago", 0);
        Assert.IsNull(settlement.LostToFind, "Nobody who can be met out there");
        settlement.RestoreFallen(first, "There", 1);
        settlement.RestoreFallen(second, "There", 2);
        Assert.AreEqual(first, settlement.LostToFind.Resident);
        settlement.FindAgain(first);
        Assert.AreEqual(second, settlement.LostToFind.Resident);
    }

    [Test]
    public void WhatWasForgottenComesBackBiggestLast()
    {
        var settlement = new SettlementState();
        ResidentDefinition sigrid = Person("sigrid");
        settlement.AddResident(sigrid);
        Along(settlement, sigrid, bosses: 1);
        Along(settlement, sigrid, helped: 1);
        settlement.LoseResident(sigrid, "There");
        settlement.FindAgain(sigrid);
        ResidentRecord record = settlement.Find(sigrid);
        CollectionAssert.AreEqual(new[] { CompanionMemory.HelpedUp, CompanionMemory.BossBeaten }, record.Forgotten);
        CollectionAssert.IsEmpty(record.Remembers);

        Along(settlement, sigrid);
        Assert.AreEqual(CompanionMemory.None, record.ComingBack, "Not after one raid");
        Along(settlement, sigrid);
        Assert.AreEqual(CompanionMemory.HelpedUp, record.ComingBack);
        Assert.IsFalse(record.AllCameBack);

        record.ComingBack = CompanionMemory.None;
        Along(settlement, sigrid);
        Along(settlement, sigrid);
        Assert.AreEqual(CompanionMemory.BossBeaten, record.ComingBack);
        Assert.IsTrue(record.AllCameBack);
        CollectionAssert.IsEmpty(record.Forgotten);
        CollectionAssert.Contains(record.Remembers, CompanionMemory.BossBeaten);
    }

    [Test]
    public void AMemoryWaitsToBeSpokenBeforeTheNextReturns()
    {
        var settlement = new SettlementState();
        ResidentDefinition sigrid = Person("sigrid");
        settlement.AddResident(sigrid);
        Along(settlement, sigrid, bosses: 1);
        Along(settlement, sigrid, helped: 1);
        settlement.LoseResident(sigrid, "There");
        settlement.FindAgain(sigrid);
        ResidentRecord record = settlement.Find(sigrid);
        for (int i = 0; i < 8; i++)
        {
            Along(settlement, sigrid);
        }

        Assert.AreEqual(CompanionMemory.HelpedUp, record.ComingBack, "Never talked to: nothing skipped");
        Assert.AreEqual(1, record.Forgotten.Count);
    }

    [Test]
    public void LostAgainWhatTheyHadNotGotBackIsStillGone()
    {
        var settlement = new SettlementState();
        ResidentDefinition sigrid = Person("sigrid");
        settlement.AddResident(sigrid);
        Along(settlement, sigrid, bosses: 1);
        settlement.LoseResident(sigrid, "There");
        settlement.FindAgain(sigrid);
        Along(settlement, sigrid, helped: 1);
        settlement.LoseResident(sigrid, "There");
        settlement.FindAgain(sigrid);
        CollectionAssert.AreEqual(new[] { CompanionMemory.HelpedUp, CompanionMemory.BossBeaten }, settlement.Find(sigrid).Forgotten);
    }

    [Test]
    public void ItAllSurvivesASave()
    {
        var settlement = new SettlementState();
        ResidentDefinition found = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Sigrid.asset");
        ResidentDefinition lost = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Eirik.asset");
        settlement.AddResident(found);
        ResidentRecord record = settlement.Find(found);
        record.Forgotten.Add(CompanionMemory.CloseCall);
        record.Remembers.Add(CompanionMemory.HardFight);
        record.IsStranger = true;
        record.ComingBack = CompanionMemory.HelpedUp;
        record.AllCameBack = true;
        settlement.RestoreFallen(lost, "There", 3, 4, new[] { CompanionMemory.BossBeaten });

        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var saved = new SettlementSaveData();
        foreach (System.Reflection.MethodInfo method in typeof(SaveMapper).GetMethods(flags))
        {
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();
            if (method.ReturnType == typeof(void) && parameters.Length == 2 && parameters[0].ParameterType == typeof(SettlementState) && parameters[1].ParameterType == typeof(SettlementSaveData))
            {
                method.Invoke(null, new object[] { settlement, saved });
            }
        }

        Assert.AreEqual(1, saved.residents.Count, "Written");
        SettlementSaveData read = JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(saved));
        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:ContentDatabase")[0]));
        var back = (SettlementState)typeof(SaveMapper).GetMethod("ReadSettlement", flags).Invoke(null, new object[] { read, database, new List<string>() });

        ResidentRecord again = back.Find(found);
        CollectionAssert.AreEqual(new[] { CompanionMemory.CloseCall }, again.Forgotten);
        CollectionAssert.AreEqual(new[] { CompanionMemory.HardFight }, again.Remembers);
        Assert.IsTrue(again.IsStranger);
        Assert.AreEqual(CompanionMemory.HelpedUp, again.ComingBack);
        Assert.IsTrue(again.AllCameBack);
        Assert.AreEqual(lost, back.Fallen[0].Resident);
        CollectionAssert.AreEqual(new[] { CompanionMemory.BossBeaten }, back.Fallen[0].Forgotten);
    }

    [Test]
    public void AnOlderSaveHasNothingToGetBack()
    {
        var entry = JsonUtility.FromJson<FallenSaveData>("{\"id\":\"sigrid\",\"where\":\"There\",\"atExtraction\":3,\"raidsAlong\":2}");
        Assert.AreEqual(2, entry.raidsAlong);
        CollectionAssert.IsEmpty(entry.forgotten);
    }

    [Test]
    public void TheRumourOnlyComesUpWhileSomeoneIsOutThereAndNamesThem()
    {
        var rumour = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/TheLost.asset");
        Assert.IsTrue(rumour.SeeksLost);
        Assert.IsTrue(rumour.Urgent);
        var settlement = new SettlementState();
        Assert.IsFalse(rumour.CanComeUp(null, settlement, null));

        ResidentDefinition person = Person("sigrid");
        var data = new SerializedObject(person);
        data.FindProperty("displayName").stringValue = "Sigrid";
        data.ApplyModifiedPropertiesWithoutUndo();
        settlement.RestoreFallen(person, "There", 0);
        Assert.IsTrue(rumour.CanComeUp(null, settlement, null));
        StringAssert.Contains("Sigrid", rumour.BriefingFor(settlement));
        settlement.FindAgain(person);
        Assert.IsFalse(rumour.CanComeUp(null, settlement, null), "Nobody left to find");
    }

    [Test]
    public void EveryRaidThatCanLoseSomeoneCanFindThem()
    {
        foreach (string path in new[] { "Assets/_Project/Data/Raids/MistyWoods.asset", "Assets/_Project/Data/Raids/TrollValley.asset" })
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(path);
            bool found = false;
            foreach (RaidRumourDefinition rumour in raid.GetSystem<RaidRumoursSystem>().Pool)
            {
                found |= rumour != null && rumour.SeeksLost;
            }

            Assert.IsTrue(found, raid.DisplayName);
        }
    }

    [Test]
    public void ThereIsSomethingToSayForEveryMemory()
    {
        SettlementSettings settings = SettlementSettings.Current;
        Assert.Greater(settings.StrangerLines.Length, 0);
        Assert.IsNotEmpty(settings.AllBackLine);
        foreach (CompanionMemory memory in System.Enum.GetValues(typeof(CompanionMemory)))
        {
            if (memory != CompanionMemory.None)
            {
                Assert.IsNotNull(CompanionBond.LineFor(memory, "anyone", null, settings.ReturningLines), memory.ToString());
            }
        }
    }

    [Test]
    public void ExitsSpreadFromEndToEnd()
    {
        var row = new List<Vector2>();
        for (int i = 0; i < 12; i++)
        {
            row.Add(new Vector2(i * 10f, 0f));
        }

        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, RaidMapGenerator.SpreadOver(row.GetRange(0, 3), 3), "A normal map, as it was");
        int[] five = RaidMapGenerator.SpreadOver(row, 5);
        Assert.AreEqual(0, five[0], "West stays furthest west");
        Assert.AreEqual(11, five[4], "East furthest east");
        CollectionAssert.AllItemsAreUnique(five);
        for (int i = 1; i < five.Length; i++)
        {
            Assert.Greater(five[i], five[i - 1], "West to east");
            Assert.GreaterOrEqual(five[i] - five[i - 1], 2, "No two side by side when there is room");
        }

        CollectionAssert.AreEqual(new[] { 0 }, RaidMapGenerator.SpreadOver(row, 1));
        CollectionAssert.AreEqual(new[] { 0, 1 }, RaidMapGenerator.SpreadOver(row.GetRange(0, 2), 5), "More exits than sites: as many as fit");
    }

    [Test]
    public void TwoExitsDoNotShareAClearingWhenThereIsAnother()
    {
        var sites = new List<Vector2> { new Vector2(0f, 0f), new Vector2(50f, 0f), new Vector2(55f, 2f), new Vector2(60f, 200f), new Vector2(100f, 0f) };
        CollectionAssert.AreEqual(new[] { 0, 3, 4 }, RaidMapGenerator.SpreadOver(sites, 3));
    }

    [Test]
    public void ALargeRaidHasMoreWaysOut()
    {
        foreach (string path in new[] { "Assets/_Project/Data/Raids/MistyWoods.asset", "Assets/_Project/Data/Raids/TrollValley.asset" })
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(path);
            RaidLayoutRules large = raid.LayoutRules.ScaledBy(raid.LargeRaid.widthScale);
            RaidLayoutPlan normal = RaidLayoutPlanner.Plan(raid.LayoutRules, 200f, 9);
            RaidLayoutPlan big = RaidLayoutPlanner.Plan(large, 200f * raid.LargeRaid.widthScale, 9);
            Assert.AreEqual(3, normal.SitesOf(RaidSiteKind.Extraction).Count, "A normal map is as it was");
            Assert.AreEqual(3 + raid.LargeRaid.extraExits.Length, big.SitesOf(RaidSiteKind.Extraction).Count, "A place for every exit: " + path);
            Object.DestroyImmediate(large);
        }
    }

    [Test]
    public void ARaidCanMoveOnWhatIsOutThereButNotWhatIsAtHome()
    {
        Assert.IsTrue(Objective(ObjectiveType.KillMonster).CanProgressOnARaid);
        Assert.IsTrue(Objective(ObjectiveType.GatherResource).CanProgressOnARaid);
        Assert.IsTrue(Objective(ObjectiveType.CompleteRaids).CanProgressOnARaid);
        Assert.IsFalse(Objective(ObjectiveType.BuildStructure).CanProgressOnARaid);
        Assert.IsFalse(Objective(ObjectiveType.CraftItem).CanProgressOnARaid);
        Assert.IsFalse(Objective(ObjectiveType.QuestEvent).CanProgressOnARaid, "That depends on a waypoint being here");
    }

    private static QuestObjective Objective(ObjectiveType type)
    {
        var objective = new QuestObjective();
        typeof(QuestObjective).GetField("type", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(objective, type);
        return objective;
    }
}
