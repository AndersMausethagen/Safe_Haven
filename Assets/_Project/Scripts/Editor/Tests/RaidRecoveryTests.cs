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
        string notice = SaveGameService.NoticeFor(new SaveData { raidUnderWay = "The Misty Woods" }, 0);
        StringAssert.Contains("The Misty Woods", notice);
        StringAssert.Contains("cut short", notice);
        StringAssert.Contains("Nothing from that raid came home", notice);
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
        string slot = SaveGameService.CurrentSlot;
        SaveGameService.SaveBeforeRaid(ScriptableObject.CreateInstance<RaidDefinition>());
        SaveGameService.SaveOnceHome();
        Assert.AreEqual(slot, SaveGameService.CurrentSlot);
    }
}
