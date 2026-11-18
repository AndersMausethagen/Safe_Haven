using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class RaidRecoveryTests
{
    private string folder;
    private SaveStorage storage;

    [SetUp]
    public void SetUp()
    {
        folder = Path.Combine(Application.temporaryCachePath, "RaidRecoveryTests");
        Directory.CreateDirectory(folder);
        storage = new SaveStorage(folder);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    [Test]
    public void ASaveMadeOnTheWayIntoARaidRemembersItOnDisk()
    {
        storage.Write("slot", new SaveData { scene = "SafeHaven", raidUnderWay = "The Misty Woods" });
        Assert.IsTrue(storage.TryRead("slot", out SaveData read, out _));
        Assert.AreEqual("The Misty Woods", read.raidUnderWay);
    }

    [Test]
    public void AnOlderSaveHasNoRaidUnderWay()
    {
        File.WriteAllText(storage.PathFor("old"), "{\"version\":1,\"scene\":\"SafeHaven\",\"savedAtUtc\":\"2026-10-01T10:00:00Z\"}");
        Assert.IsTrue(storage.TryRead("old", out SaveData read, out _));
        Assert.IsTrue(string.IsNullOrEmpty(read.raidUnderWay));
        StringAssert.StartsWith("Game loaded", SaveGameService.NoticeFor(read, 0));
    }

    [Test]
    public void LoadingARaidThatNeverCameHomeSaysWhatHappened()
    {
        // It names the raid and says something other than a plain load does (its wording is free to change).
        string notice = SaveGameService.NoticeFor(new SaveData { raidUnderWay = "The Misty Woods" }, 0);
        StringAssert.Contains("The Misty Woods", notice);
        Assert.AreNotEqual(SaveGameService.NoticeFor(new SaveData(), 0), notice);
    }

    [Test]
    public void TheSavesListShowsARaidCutShort()
    {
        var info = new SaveSlotInfo("slot", new SaveData { scene = "SafeHaven", raidUnderWay = "The Misty Woods" }, null, System.DateTime.Now);
        Assert.AreEqual("The Misty Woods", info.RaidCutShort);
    }

    [Test]
    public void OutsidePlayNothingIsAutosaved()
    {
        SaveStorage before = SaveGameService.Storage;
        var raid = ScriptableObject.CreateInstance<RaidDefinition>();
        try
        {
            // This test's own folder: a save made by mistake would land here, not in the player's saves.
            SaveGameService.Storage = storage;
            SaveGameService.SaveBeforeRaid(raid);
            SaveGameService.SaveOnceHome();
            Assert.IsFalse(SaveGameService.IsSaveWaitingForHome, "no save waits to be made at home");
            Assert.IsEmpty(Directory.GetFiles(folder), "nothing was written");
        }
        finally
        {
            SaveGameService.Storage = before;
            Object.DestroyImmediate(raid);
        }
    }
}
