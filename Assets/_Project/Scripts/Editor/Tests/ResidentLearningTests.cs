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
    public void Workers_RiseInRank_ByTheirJobsSkill()
    {
        SettlementSettings settings = SettlementSettings.Current;
        // The ranks' names and levels are the settings' own; the rule is that each rank needs more of the job's skill than the one
        // before, and more skill never makes anyone slower (ResidentTalents, Docs/PeopleResearch_2026-11-23.md §3.1).
        for (int i = 1; i < settings.WorkerRanks.Count; i++)
        {
            Assert.Greater(settings.WorkerRanks[i].level, settings.WorkerRanks[i - 1].level, settings.WorkerRanks[i].name);
        }

        var settlement = new SettlementState();
        JobDefinition smith = Job("Smith");
        settlement.AddResident(Bjarne, smith);
        ResidentRecord bjarne = settlement.Find(Bjarne);
        float before = settlement.WorkerSpeed(smith.Workplace.Station);
        Assert.AreEqual(ResidentTalents.StationSpeed(bjarne), before, 0.0001f, "the smithy works at his skill's speed");
        LevelingSettings leveling = LevelingSettings.Current;
        bjarne.Talents.SetExperience(smith.Skill.Id, leveling.TotalExperienceFor(settings.WorkerRanks[settings.WorkerRanks.Count - 1].level));
        Assert.AreEqual(settings.WorkerRanks[settings.WorkerRanks.Count - 1].name, ResidentTalents.RankName(bjarne), "a master");
        Assert.Greater(settlement.WorkerSpeed(smith.Workplace.Station), before, "a master speeds the smithy");
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
            GameSession.Home.AddResident(Bjarne, smith);
            ResidentRecord bjarne = GameSession.Here.Find(Bjarne);
            int learned = ExperienceTracker.TakeLesson(bjarne);
            // A lesson is practice, so the Practice Time Scale slows it like any other (as the crafting test counts it).
            LevelingSettings leveling = LevelingSettings.Current;
            int tier = ExperienceTracker.LessonTier(bjarne);
            Assert.AreEqual(Mathf.FloorToInt(leveling.TierExperience(tier) * SettlementSettings.Current.LessonWork / leveling.PracticeTimeScale), learned);
            Assert.AreEqual(learned, GameSession.Progress.ExperienceOf(smith.Teaches));
            Assert.AreEqual(0, ExperienceTracker.TakeLesson(bjarne), "one between raids");

            GameSession.Home.RecordExtraction();
            Assert.Greater(ExperienceTracker.TakeLesson(bjarne), 0, "again after a raid survived");

            bjarne.Talents.SetExperience(smith.Teaches.Id, leveling.TotalExperienceFor(leveling.MaxLevel));
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
}
