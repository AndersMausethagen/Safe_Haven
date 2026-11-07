using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Residents learn their jobs by doing them, which speeds their station, and give the player lessons in their skill.</summary>
public sealed class ResidentLearningTests
{
    private static ResidentDefinition Bjarne => AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Bjarne.asset");

    private static JobDefinition Job(string name) => AssetDatabase.FindAssets("t:JobDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
        .First(job => job != null && job.name == name);

    [Test]
    public void Workers_RiseInRank_ByTheCraftsTheyMake()
    {
        SettlementSettings settings = SettlementSettings.Current;
        Assert.AreEqual("Novice", settings.RankFor(0).name);
        Assert.AreEqual(1f, settings.RankFor(0).speed);
        Assert.AreEqual("Journeyman", settings.RankFor(30).name);
        Assert.AreEqual("Master", settings.RankFor(1000).name);
        Assert.Greater(settings.RankFor(70).speed, settings.RankFor(30).speed);

        var settlement = new SettlementState();
        JobDefinition smith = Job("Smith");
        settlement.AddResident(Bjarne, smith);
        Assert.AreEqual(1f, settlement.WorkerSpeed(smith.Workplace.Station), "a novice smith");
        settlement.Find(Bjarne).WorkDone = 70;
        Assert.AreEqual(settings.RankFor(70).speed, settlement.WorkerSpeed(smith.Workplace.Station), 0.0001f, "an expert speeds the smithy");
        Assert.AreEqual(1f, settlement.WorkerSpeed(CraftingStationType.Kitchen), "nobody works the kitchen");
    }

    [Test]
    public void AResident_GivesOneLessonBetweenRaids_InTheirJobsSkill()
    {
        JobDefinition smith = Job("Smith");
        Assert.NotNull(smith.Teaches, "the smith teaches");
        GameSession.StartNewGame();
        try
        {
            GameSession.Settlement.AddResident(Bjarne, smith);
            ResidentRecord bjarne = GameSession.Settlement.Find(Bjarne);
            int learned = ExperienceTracker.TakeLesson(bjarne);
            Assert.AreEqual(Mathf.FloorToInt(LevelingSettings.Current.TierExperience(1) * SettlementSettings.Current.LessonWork), learned);
            Assert.AreEqual(learned, GameSession.Progress.ExperienceOf(smith.Teaches));
            Assert.AreEqual(0, ExperienceTracker.TakeLesson(bjarne), "one between raids");

            GameSession.Settlement.RecordExtraction();
            Assert.Greater(ExperienceTracker.TakeLesson(bjarne), 0, "again after a raid survived");

            bjarne.WorkDone = 150;
            Assert.AreEqual(5, ExperienceTracker.LessonTier(bjarne), "a master teaches at the top tier");
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void WhatAResidentHasLearned_IsSaved()
    {
        var settlement = new SettlementState();
        settlement.AddResident(Bjarne, Job("Smith"));
        settlement.Find(Bjarne).WorkDone = 42;
        settlement.Find(Bjarne).TaughtSinceRaid = true;

        SaveData data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, settlement: settlement);
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content);

        ResidentRecord read = loaded.Settlement.Find(Bjarne);
        Assert.AreEqual(42, read.WorkDone);
        Assert.IsTrue(read.TaughtSinceRaid);
    }

    [Test]
    public void TheJobsTeach_MeleeAndSneakingToo()
    {
        Assert.AreEqual("melee", Job("Warden").Teaches.Id);
        Assert.AreEqual("sneaking", Job("Tracker").Teaches.Id);
    }
}
