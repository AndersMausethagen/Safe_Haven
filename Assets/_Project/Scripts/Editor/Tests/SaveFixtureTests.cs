using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Real saves from earlier builds (Editor/Tests/SaveFixtures, copied from playtests, named by the day they were made) still load
/// into today's game: with all their content found, and what they hold kept through a save made from them
/// (Docs/BugPatterns.md, second round: SaveCoverageTests proves a save made today comes back, this that last week's still does).
/// When the format changes, add a save from the new build here and keep the old ones.
/// </summary>
public sealed class SaveFixtureTests
{
    private const string Folder = "Assets/_Project/Scripts/Editor/Tests/SaveFixtures";

    // Content removed from the game on purpose since a fixture was made, and why: its loss from old saves is accepted.
    private static readonly Dictionary<string, string> RemovedOnPurpose = new Dictionary<string, string>
    {
        ["quest 'prologue.berries'"] = "Berries for the Pot, cut from the prologue's shelter at the user's word (2026-11-19).",
    };

    private static IEnumerable<string> Fixtures() =>
        Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.json").Select(path => path.Replace('\\', '/')).OrderBy(path => path) : Enumerable.Empty<string>();

    [Test]
    public void ThereAreSavesFromEarlierBuilds()
    {
        Assert.GreaterOrEqual(Fixtures().Count(), 3);
    }

    [TestCaseSource(nameof(Fixtures))]
    public void AnEarlierSave_StillLoads_AndKeepsWhatItHolds(string path)
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SaveData saved = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        Assert.IsNotNull(saved, "it reads");

        LoadedGame loaded = SaveMapper.FromSaveData(saved, content);
        CollectionAssert.IsEmpty(loaded.MissingContent.Where(id => !RemovedOnPurpose.ContainsKey(id)),
            "content it names is gone from the game: put it back, map its old id to the new one, or list it in RemovedOnPurpose");

        // What it holds survives a save made from it, and that save loads again the same.
        SaveData again = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(SaveMapper.ToSaveData(loaded.Player, loaded.Stash, loaded.Scene, loaded.Pose,
            System.DateTime.UtcNow, loaded.World, loaded.Quests, loaded.Settlement, loaded.Playtest, loaded.Progress)));
        LoadedGame reloaded = SaveMapper.FromSaveData(again, content);
        Assert.AreEqual(Count(saved.settlement?.residents), loaded.Settlement.Residents.Count, "every resident");
        Assert.AreEqual(loaded.Settlement.Residents.Count, reloaded.Settlement.Residents.Count);
        // Every quest, but those taken out of the game on purpose (RemovedOnPurpose), which a load drops.
        int removedQuests = saved.quests != null ? saved.quests.Count(entry => entry != null && RemovedOnPurpose.ContainsKey($"quest '{entry.id}'")) : 0;
        Assert.AreEqual(Count(saved.quests) - removedQuests, loaded.Quests.Entries.Count(), "every quest");
        Assert.AreEqual(loaded.Quests.Entries.Count(), reloaded.Quests.Entries.Count());
        Assert.AreEqual(saved.settlement != null ? saved.settlement.successfulExtractions : 0, loaded.Settlement.SuccessfulExtractions, "the raids come home");
        Assert.AreEqual(loaded.Settlement.SuccessfulExtractions, reloaded.Settlement.SuccessfulExtractions);
        Assert.AreEqual(Count(saved.player?.bag), again.player.bag.Count, "the bag");
        Assert.AreEqual(JsonUtility.ToJson(again.settlement), JsonUtility.ToJson(SaveMapper.ToSaveData(reloaded.Player, reloaded.Stash, reloaded.Scene,
            reloaded.Pose, System.DateTime.UtcNow, reloaded.World, reloaded.Quests, reloaded.Settlement, reloaded.Playtest, reloaded.Progress).settlement),
            "and saving it again changes nothing");
    }

    private static int Count<T>(List<T> list) => list != null ? list.Count : 0;
}
