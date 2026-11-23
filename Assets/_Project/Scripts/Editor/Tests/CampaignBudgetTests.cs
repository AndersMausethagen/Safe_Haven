using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Campaign's budget (Docs/CampaignAudit_2026-11-23.md, the user's answers): every system comes through a story beat or a person, and
/// the writing wins. The horse comes once there are carts for it to pull; forays come with Tor's Past the Ridge, and a foray home counts
/// for its quest; written friends stay friends, and only a rivals' scene makes two people Rivals.
/// </summary>
public sealed class CampaignBudgetTests
{
    private static QuestDefinition Quest(string name) => AssetDatabase.LoadAssetAtPath<QuestDefinition>($"Assets/_Project/Data/Quests/{name}.asset");

    private static ResidentDefinition Person(string name) => AssetDatabase.FindAssets("t:ResidentDefinition " + name)
        .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).First(one => one != null && one.name == name);

    [Test]
    public void TheHorse_ComesWithTheCarts()
    {
        QuestDefinition horse = Quest("AHorseOfYourOwn");
        CollectionAssert.AreEqual(new[] { Quest("HeavyLoads") }, horse.Prerequisites.ToArray(), "after the Wainwright's Yard: a cart to pull");
        StringAssert.Contains("cart", horse.Description, "its reason is said");
    }

    [Test]
    public void Forays_ComeWithTorsQuest_AndAForayHomeCountsForIt()
    {
        QuestDefinition ridge = Quest("PastTheRidge");
        Assert.IsNotNull(ridge);
        Assert.AreEqual(ridge, ForaySettings.Current.UnlockedBy);
        List<QuestDefinition> line = Person("Tor").QuestLine.Select(offer => offer.Quest).ToList();
        Assert.AreEqual(line.IndexOf(Quest("RaiseTheWalls")) + 1, line.IndexOf(ridge), "Tor asks it after the palisade");
        CollectionAssert.AreEqual(new[] { Quest("RaiseTheWalls") }, ridge.Prerequisites.ToArray());
        Assert.AreEqual(ObjectiveType.CompleteForay, ridge.Objectives.Last().Type);
        Assert.IsTrue(Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).Quests.Contains(ridge), "a save can name it");

        var heard = new List<GameEvent>();
        void Heard(GameEvent gameEvent)
        {
            if (gameEvent.Kind == GameEventKind.ForayReturned)
            {
                heard.Add(gameEvent);
            }
        }

        GameSession.StartNewGame();
        GameEvents.Raised += Heard;
        try
        {
            Assert.IsFalse(Forays.Unlocked(ForaySettings.Current), "none before Tor asks");
            Assert.IsTrue(GameSession.Quests.TryStart(ridge, true));
            Assert.IsTrue(Forays.Unlocked(ForaySettings.Current), "his quest brings them");

            SettlementState home = GameSession.Home;
            foreach (ResidentDefinition one in new[] { Person("Tor"), Person("Bjarne"), Person("Ingrid") })
            {
                home.AddResident(one);
            }

            home.RepairPortal();
            var chests = new ItemContainer(10, 6);
            chests.Add(AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Food/BarleyBread.asset"), 10);
            ForaySettings.Destination place = ForaySettings.Current.Destinations.First(d => d.tier == 1);
            Assert.IsTrue(Forays.Send(home, place, Forays.Strongest(home, 2, ForaySettings.Current, TerritorySettings.Current), chests, ForaySettings.Current));
            Forays.Tick(home, home.Foray.SecondsLeft + 1f, chests, ForaySettings.Current, () => 0f, (least, most) => least);
            Assert.IsNull(home.Foray, "home");
            Assert.AreEqual(1, heard.Count, "its coming home is told once");
            Assert.AreEqual(1, ridge.Objectives.Last().Progress(heard[0]), "and counts for Past the Ridge");
            Assert.AreEqual(0, ridge.Objectives.Last().Progress(new GameEvent(GameEventKind.RaidFinished, null, 1)), "nothing else does");
        }
        finally
        {
            GameEvents.Raised -= Heard;
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void TheWriting_KeepsWrittenFriends_AndOnlyAScene_MakesRivals()
    {
        Assert.IsTrue(PeopleSettings.Current.KeepToTheWriting, "the Campaign keeps to the writing");
        ResidentDefinition one = AssetDatabase.FindAssets("t:ResidentDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(person => person != null && person.Friend != null);
        var home = new SettlementState();
        home.AddResident(one);
        home.AddResident(one.Friend);
        ResidentRecord a = home.Find(one), b = home.Find(one.Friend);
        Assert.GreaterOrEqual(PeopleStandings.LevelOf(home, a, b), 1, "written friends start Friendly");
        for (int i = 0; i < 6; i++)
        {
            PeopleStandings.Argue(home, a, b, 0.999f);
        }

        Assert.AreEqual(1, PeopleStandings.LevelOf(home, a, b), "hardened six times, still Friendly: the writing wins");

        // Two with no written tie: work alone never makes Rivals; a rivals' scene may.
        ResidentDefinition other = AssetDatabase.FindAssets("t:ResidentDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(person => person != null && person != one && person != one.Friend && one.Friend != person && person.Friend != one
                             && one.Friction != person && person.Friction != one);
        home.AddResident(other);
        ResidentRecord c = home.Find(other);
        home.SetStanding(a.Resident.Id, c.Resident.Id, -1f);
        PeopleStandings.Change(home, a, c, -3f);
        Assert.AreEqual(-1, PeopleStandings.LevelOf(home, a, c), "Cool, not Rivals, without a scene");
        PeopleStandings.Argue(home, a, c, 0.999f);
        PeopleStandings.Argue(home, a, c, 0.999f);
        Assert.AreEqual(-2, PeopleStandings.LevelOf(home, a, c), "the canary: a rivals' scene can harden it");
    }
}
