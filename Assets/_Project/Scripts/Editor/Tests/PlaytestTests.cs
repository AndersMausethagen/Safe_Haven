using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PlaytestTests
{
    private sealed class NoContent : IContentLookup
    {
        public ItemDefinition FindItem(string id) => null;
        public ResourceDefinition FindResource(string id) => null;
        public RecipeDefinition FindRecipe(string id) => null;
        public QuestDefinition FindQuest(string id) => null;
        public BuildingDefinition FindBuilding(string id) => null;
        public ResidentDefinition FindResident(string id) => null;
        public JobDefinition FindJob(string id) => null;
    }

    private static PlaytestEntry Row(float minutes, string eventName, string subject = "", string detail = "", int session = 1) =>
        new PlaytestEntry { PlayedSeconds = minutes * 60f, Session = session, Event = eventName, Subject = subject, Detail = detail, Utc = "", Scene = "" };

    [Test]
    public void Format_ThenTryParse_KeepsEveryField_EvenWithCommasAndQuotes()
    {
        var entry = new PlaytestEntry
        {
            PlayedSeconds = 754.2f, Utc = "2026-10-14T13:00:00Z", Session = 3, Scene = "Mistwood", Event = "ItemCrafted",
            Subject = "Axe, \"sharp\"", Amount = 2, Detail = "a,b",
        };

        Assert.IsTrue(PlaytestLog.TryParse(PlaytestLog.Format(entry), out PlaytestEntry read));

        Assert.AreEqual(entry.PlayedSeconds, read.PlayedSeconds, 0.6f, "Played time is stored to a hundredth of a minute.");
        Assert.AreEqual(entry.Utc, read.Utc);
        Assert.AreEqual(3, read.Session);
        Assert.AreEqual("Mistwood", read.Scene);
        Assert.AreEqual("ItemCrafted", read.Event);
        Assert.AreEqual("Axe, \"sharp\"", read.Subject);
        Assert.AreEqual(2, read.Amount);
        Assert.AreEqual("a,b", read.Detail);
    }

    [Test]
    public void TryParse_RejectsTheHeader()
    {
        Assert.IsFalse(PlaytestLog.TryParse(PlaytestLog.Header, out _));
    }

    [Test]
    public void Summarise_FindsTheFirstMatchingRow_AndCountsDeathsAndRaids()
    {
        var milestones = new List<PlaytestMilestone>
        {
            new PlaytestMilestone("Camp", "QuestCompleted", "FirstCamp", "", 25f),
            new PlaytestMilestone("Extracted", "RaidFinished", "", "Extracted", 60f),
            new PlaytestMilestone("Never", "QuestCompleted", "Missing", "", 0f),
        };
        var entries = new List<PlaytestEntry>
        {
            Row(5f, "QuestCompleted", "Awakening"),
            Row(20f, "questcompleted", "firstcamp"),
            Row(30f, "RaidFinished", "Mistwood", "Died"),
            Row(31f, PlaytestLog.PlayerDied, "Wolf"),
            Row(50f, "RaidFinished", "Mistwood", "Extracted", 2),
            Row(55f, "QuestCompleted", "FirstCamp", "", 2),
        };

        PlaytestRunSummary summary = PlaytestLog.Summarise("run", entries, milestones);

        Assert.AreEqual(20f, summary.MilestoneMinutes[0].Value, 0.01f, "Matching ignores case and keeps the first hit.");
        Assert.AreEqual(50f, summary.MilestoneMinutes[1].Value, 0.01f, "The detail filter skips the failed raid.");
        Assert.IsNull(summary.MilestoneMinutes[2]);
        Assert.AreEqual(1, summary.Deaths);
        Assert.AreEqual(2, summary.Raids);
        Assert.AreEqual(1, summary.Extractions);
        Assert.AreEqual(2, summary.Sessions);
        Assert.AreEqual(55f, summary.PlayedMinutes, 0.01f);
    }

    [Test]
    public void Save_KeepsThePlaytestRun_SoALoadContinuesItsLog()
    {
        var run = new PlaytestRun("20261014-130000", 1234f, 3);

        SaveData data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, playtest: run);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), new NoContent());

        Assert.AreEqual("20261014-130000", loaded.Playtest.Id);
        Assert.AreEqual(1234f, loaded.Playtest.PlayedSeconds, 0.01f);
        Assert.AreEqual(3, loaded.Playtest.Sessions);
    }

    [Test]
    public void Save_FromBeforePlaytestLogging_StartsANewRun()
    {
        var data = new SaveData { version = SaveMapper.CurrentVersion, scene = "SafeHaven", playtest = null };

        LoadedGame loaded = SaveMapper.FromSaveData(data, new NoContent());

        Assert.IsNull(loaded.Playtest, "GameSession then starts a fresh run.");
    }

    [Test]
    public void KillLedger_CountsThePlayersBlows_AndWhoMadeTheKill()
    {
        var ledger = new KillLedger();
        object wolf = new object(), other = new object();

        Assert.IsNull(ledger.Note(wolf, "ForestBittenWolf", 156f, true, "", false, false, 40f, false, 10f));
        Assert.IsNull(ledger.Note(other, "Rootling", 50f, true, "", false, false, 20f, false, 10.5f));
        Assert.IsNull(ledger.Note(wolf, "ForestBittenWolf", 156f, false, "Ingrid", false, false, 30f, false, 11f));
        Assert.IsNull(ledger.Note(wolf, "ForestBittenWolf", 156f, true, "", true, false, 6f, false, 11.5f));
        KillLedger.Record done = ledger.Note(wolf, "ForestBittenWolf", 156f, true, "", false, true, 80f, true, 13f);

        Assert.NotNull(done);
        Assert.AreEqual(KillLedger.ByPlayer, done.Killer);
        Assert.AreEqual(2, done.PlayerHits, "a burn ticking is not a blow");
        Assert.AreEqual(120f, done.PlayerDamage, 0.01f);
        Assert.AreEqual(80f, done.PlayerBiggestHit, 0.01f);
        Assert.AreEqual(1, done.PlayerCriticals);
        Assert.AreEqual(1, done.OtherHits);
        Assert.AreEqual(30f, done.OtherDamage, 0.01f);
        Assert.AreEqual(6f, done.OverTimeDamage, 0.01f);
        Assert.AreEqual(3f, done.Seconds, 0.01f);
        Assert.AreEqual(1, ledger.Open, "the rootling's fight is still open");
        StringAssert.DoesNotContain(",", done.Detail, "the detail stays one CSV field without quoting");

        KillLedger.Record stolen = ledger.Note(other, "Rootling", 50f, false, "Ingrid", false, false, 30f, true, 12f);
        Assert.AreEqual("Ingrid", stolen.Killer);
        Assert.AreEqual(0, ledger.Open);
    }
}
