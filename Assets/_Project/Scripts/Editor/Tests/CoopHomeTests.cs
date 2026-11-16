using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What a guest earns or does on a visit goes where it belongs (2026-11-11 sweep). What is theirs goes to their own Haven
/// (HavenVisit.ChangeHome): an extraction, the Huldra's respect. What is the host's goes to the host's game (HavenHelp): a
/// breach source struck, once a run however many strike it, and one of the host's lost led home. Before, both only changed
/// the guest's copy of the host's settlement, which the host's next word undid.
/// </summary>
public sealed class CoopHomeTests
{
    private static readonly FieldInfo Home = typeof(HavenVisit).GetField("home", BindingFlags.NonPublic | BindingFlags.Static);

    private static void Visit(SettlementState own)
    {
        Home.SetValue(null, new SaveData { scene = "SafeHaven", settlement = SaveMapper.ToData(own) });
        // A new own Haven: the live one is made from it when first wanted (HavenVisit.OwnHaven).
        typeof(HavenVisit).GetField("ownHaven", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        typeof(HavenVisit).GetProperty(nameof(HavenVisit.IsVisiting)).SetValue(null, true);
    }

    // The guest's own Haven, alive while they visit (GameSession.Home): written into the game kept as home when it is saved.
    private static SettlementState OwnHaven() => HavenVisit.OwnHaven;

    private static void Leave()
    {
        HavenVisit.Forget();
        HavenHelp.ForHost = null;
        typeof(HavenHelp).GetMethod("ResetForNewPlaySession", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        GameSession.StartNewGame();
    }

    [Test]
    public void AnExtraction_OnAFollowedRaid_CountsForTheGuestsOwnHaven()
    {
        GameSession.StartNewGame();
        try
        {
            Visit(new SettlementState());
            int hostsBefore = GameSession.Here.SuccessfulExtractions;
            typeof(SettlementSystem).GetMethod("OnGameEvent", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { new GameEvent(GameEventKind.RaidFinished, null, outcome: RaidOutcome.Extracted) });
            Assert.AreEqual(1, OwnHaven().SuccessfulExtractions, "their own Haven counts it (its breaches come by extractions)");
            Assert.AreEqual(hostsBefore, GameSession.Here.SuccessfulExtractions, "the copy of the host's is left to the host");
        }
        finally
        {
            Leave();
        }
    }

    // Review 2026-11-16: "Bring Tor Home" completed on arriving in a friend's Haven where Tor lives (the settlement there is the
    // host's), and Tor never came to the guest's own.
    [Test]
    public void APersonLivingInTheHostsHaven_IsntRescuedForTheGuest()
    {
        ResidentDefinition person = null;
        foreach (string guid in AssetDatabase.FindAssets("t:ResidentDefinition"))
        {
            person = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (person != null && !string.IsNullOrEmpty(person.Id))
            {
                break;
            }
        }

        Assert.IsNotNull(person);
        QuestObjective rescue = QuestObjective.Create(ObjectiveType.RescuePerson, 1, person);
        typeof(QuestObjective).GetField("countAlreadyOwned", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(rescue, true);
        var log = new QuestLog();
        GameSession.StartNewGame();
        try
        {
            GameSession.Home.AddResident(person);
            Assert.AreEqual(1, rescue.Owned(null, log), "at home, one living there is found and home both");

            Visit(new SettlementState());
            Assert.AreEqual(0, rescue.Owned(null, log), "in a friend's Haven, the one living there is the host's");

            var own = new SettlementState();
            own.AddResident(person);
            Visit(own);
            Assert.AreEqual(1, rescue.Owned(null, log), "one living in the guest's own Haven counts");
        }
        finally
        {
            Leave();
        }
    }

    [Test]
    public void ChangingHome_LeavesEverythingElseAsItWas()
    {
        var own = new SettlementState();
        own.RecordExtraction();
        own.SetHuldra(2, 0);
        string before = JsonUtility.ToJson(SaveMapper.ToData(own));
        try
        {
            Visit(own);
            Assert.IsTrue(HavenVisit.ChangeHome(_ => { }));
            Assert.AreEqual(before, JsonUtility.ToJson(((SaveData)Home.GetValue(null)).settlement));
        }
        finally
        {
            Leave();
        }

        Assert.IsFalse(HavenVisit.ChangeHome(_ => { }), "at home there is no other Haven to change");
    }

    [Test]
    public void AGuestsBlowAtTheBreach_GoesToTheHost_OnceARun()
    {
        GameSession.StartNewGame();
        try
        {
            Visit(new SettlementState());
            HavenHelpKind sent = 0;
            HavenHelp.ForHost = (kind, id, share, most, run) => sent = kind;
            HavenHelp.WeakenBreach(0.3f, 0.6f);
            Assert.AreEqual(HavenHelpKind.WeakenBreach, sent);
            Assert.AreEqual(0f, GameSession.Here.BreachWeakening, 0.001f, "nothing changes in the copy");
        }
        finally
        {
            Leave();
        }

        HavenHelp.FromFriend(HavenHelpKind.WeakenBreach, "", 0.3f, 0.6f, 777, "Ingrid", null);
        HavenHelp.FromFriend(HavenHelpKind.WeakenBreach, "", 0.3f, 0.6f, 777, "Bjarne", null);
        Assert.AreEqual(0.3f, GameSession.Here.BreachWeakening, 0.001f, "every player on the run has the rumour, but it is one blow");
        HavenHelp.FromFriend(HavenHelpKind.WeakenBreach, "", 0.3f, 0.6f, 778, "Ingrid", null);
        Assert.AreEqual(0.6f, GameSession.Here.BreachWeakening, 0.001f);
        Leave();
    }

    [Test]
    public void TheHostsLost_LedHomeByAGuest_ComeHomeToTheHost()
    {
        GameSession.StartNewGame();
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var tor = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Tor.asset");
        GameSession.Home.AddResident(tor);
        GameSession.Home.LoseResident(tor, "the Misty Woods");
        try
        {
            Visit(new SettlementState());
            string sent = null;
            HavenHelp.ForHost = (kind, id, share, most, run) => sent = id;
            Assert.IsTrue(HavenHelp.FoundAgain(tor));
            Assert.AreEqual(tor.Id, sent);
            Assert.IsNull(GameSession.Here.Find(tor), "still lost in the copy until the host says otherwise");
        }
        finally
        {
            HavenVisit.Forget();
            HavenHelp.ForHost = null;
        }

        HavenHelp.FromFriend(HavenHelpKind.FoundAgain, tor.Id, 0f, 0f, 0, "Ingrid", content);
        Assert.IsNotNull(GameSession.Here.Find(tor), "home in the host's Haven");
        Leave();
    }

    [Test]
    public void ABorrowedSettlement_SaysSoWhenChanged()
    {
        GameSession.StartNewGame();
        typeof(SettlementState).GetMethod("ForgetTold", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        try
        {
            Visit(new SettlementState());
            Assert.IsTrue(GameSession.Here.IsBorrowed);
            LogAssert.Expect(LogType.Error, new Regex("Co-op: ChangeBlight changed the copy of the host's settlement"));
            GameSession.Here.ChangeBlight(0.1f); // the copy, on purpose: what the error is for
            // Its own Haven, read and written by ChangeHome, is the guest's to change: no word.
            Assert.IsTrue(HavenVisit.ChangeHome(own => own.ChangeBlight(0.1f)));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Leave();
        }

        Assert.IsFalse(GameSession.Here.IsBorrowed, "at home the settlement is the player's own");
    }

    [Test]
    public void TheHelpMessage_SurvivesTheWire()
    {
        using var writer = new Unity.Netcode.FastBufferWriter(128, Unity.Collections.Allocator.Temp);
        CoopLands.WriteHelp(writer, HavenHelpKind.WeakenBreach, "tor", 0.25f, 0.5f, -42);
        using var reader = new Unity.Netcode.FastBufferReader(writer, Unity.Collections.Allocator.Temp);
        CoopLands.ReadHelp(reader, out HavenHelpKind kind, out string id, out float share, out float most, out int run);
        Assert.AreEqual(HavenHelpKind.WeakenBreach, kind);
        Assert.AreEqual("tor", id);
        Assert.AreEqual(0.25f, share);
        Assert.AreEqual(0.5f, most);
        Assert.AreEqual(-42, run);
    }
}
