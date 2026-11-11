using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// The interface's paintings are in place: raid events' scenes, the Haven's camp at every rank, the journal's backstory pages,
/// residents' portraits (also in conversation) and raids' pictures. Painted with Tools/LocalSpawn/generate_ui_art.py and put in by
/// AgentScripts/BuildUIArt.cs and BuildJournal.cs.
/// </summary>
public sealed class InterfaceArtTests
{
    private const string Fix = " Paint it (Tools/LocalSpawn/generate_ui_art.py) and run BuildUIArt / BuildJournal.";

    private static T[] All<T>() where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).Where(x => x != null).ToArray();

    [Test]
    public void EveryRaidEvent_HasItsScene()
    {
        foreach (RaidEventDefinition raidEvent in All<RaidEventDefinition>())
        {
            Assert.IsNotNull(raidEvent.Artwork, raidEvent.name + Fix);
        }
    }

    [Test]
    public void EveryRankOfTheHaven_HasItsPicture()
    {
        HavenSettings settings = AssetDatabase.LoadAssetAtPath<HavenSettings>("Assets/_Project/Resources/HavenSettings.asset");
        Assert.IsNotEmpty(settings.Ranks);
        foreach (HavenSettings.Rank rank in settings.Ranks)
        {
            Assert.IsNotNull(rank.picture, rank.name + Fix);
        }
    }

    [Test]
    public void EveryBackstoryPage_HasAPicture()
    {
        JournalSettings journal = AssetDatabase.LoadAssetAtPath<JournalSettings>("Assets/_Project/Resources/JournalSettings.asset");
        foreach (JournalPage page in journal.Backstory)
        {
            Assert.IsNotNull(page.art, page.title + Fix);
        }
    }

    [Test]
    public void EveryResident_HasAPortrait_AndEveryRaidItsPictures()
    {
        foreach (ResidentDefinition resident in All<ResidentDefinition>())
        {
            Assert.IsNotNull(resident.Portrait, resident.name + Fix);
        }

        foreach (RaidDefinition raid in All<RaidDefinition>())
        {
            Assert.IsNotNull(raid.PlaceArtwork, raid.name + Fix);
            Assert.IsNotNull(raid.ResultArtwork(true), raid.name + Fix);
            Assert.IsNotNull(raid.ResultArtwork(false), raid.name + Fix);
        }
    }
}
