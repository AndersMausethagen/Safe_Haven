using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Parties to the held lands (LandParties; the Keeper's view, Docs/Territory.md "Not yet"): who goes and who stays, what a party brings
/// (a range, more for a practised hand), how a tended land wears less and a scouted one is warned sooner, the raid too short for a party,
/// and the orders kept by a save.
/// </summary>
public sealed class LandPartiesTests
{
    private static TerritorySettings Settings => TerritorySettings.Current;
    private static JobDefinition Job(string name) => AssetDatabase.LoadAssetAtPath<JobDefinition>($"Assets/_Project/Data/Settlement/{name}.asset");
    private static ResidentDefinition Person(string path) => AssetDatabase.LoadAssetAtPath<ResidentDefinition>($"Assets/_Project/Data/{path}.asset");

    [Test]
    public void EveryKindOfParty_HasSomeoneToLeadIt_AndTheHuntersBringSomething()
    {
        foreach (LandPartyKind kind in LandParties.All)
        {
            TerritorySettings.PartyInfo info = Settings.PartyOf(kind);
            Assert.IsNotNull(info, $"a {kind} party is written");
            Assert.IsNotNull(info.job, $"someone leads the {kind} party");
        }

        Assert.AreEqual(Job("Hunter"), Settings.PartyOf(LandPartyKind.Hunt).job);
        Assert.Greater(Settings.PartyOf(LandPartyKind.Hunt).yields.Count, 0);
        Assert.Greater(Settings.PartyOf(LandPartyKind.Fish).yields.Count, 0);
        Assert.Greater(Settings.PartyMinSeconds, 0f, "a quick in-and-out isn't a day's hunting");
    }

    [Test]
    public void TheJobHolderGoes_NotTheOneAlong_NorOneWhoseWorkIsPaused()
    {
        var hunter = Job("Hunter");
        var gunnar = Person("Settlement/Gunnar");
        Assume.That(hunter != null && gunnar != null);
        var home = new SettlementState();
        Assert.IsNull(LandParties.WhoGoes(home, LandPartyKind.Hunt, null, Settings), "nobody hunts yet");
        home.AddResident(gunnar, hunter);
        Assert.AreEqual(gunnar, LandParties.WhoGoes(home, LandPartyKind.Hunt, null, Settings).Resident);
        Assert.IsNull(LandParties.WhoGoes(home, LandPartyKind.Hunt, gunnar, Settings), "along on the raid, not out hunting");
        Assert.IsNull(LandParties.WhoGoes(home, LandPartyKind.Fish, null, Settings), "a hunter doesn't fish");
        home.Find(gunnar).WorkPaused = true;
        Assert.IsNull(LandParties.WhoGoes(home, LandPartyKind.Hunt, null, Settings), "paused work goes nowhere");
        home.Find(gunnar).WorkPaused = false;

        Assert.IsNotNull(LandParties.WhyNot(LandPartyKind.Hunt, "Eastwood", false, home, Settings), "only to a land the Haven holds");
        home.Territory.Claim("Eastwood", Settings);
        Assert.IsNull(LandParties.WhyNot(LandPartyKind.Hunt, "Eastwood", false, home, Settings));
        Assert.IsNotNull(LandParties.WhyNot(LandPartyKind.Hunt, "Eastwood", true, home, Settings), "not from a friend's Haven");

        home.SetParty(LandPartyKind.Hunt, "Eastwood");
        Assert.IsTrue(LandParties.IsOut(home, home.Find(gunnar), null, Settings), "out hunting while the player raids: the lodge waits");
        Assert.IsFalse(LandParties.IsOut(home, home.Find(gunnar), gunnar, Settings));
        Assert.AreEqual(1, LandParties.Going(home, Settings.PartyMinSeconds + 1f, null, Settings).Count);
        Assert.AreEqual(0, LandParties.Going(home, Settings.PartyMinSeconds - 1f, null, Settings).Count, "a raid too short for a party");
    }

    [Test]
    public void APartyBringsWhatItsOddsSay_AndAPractisedHandBringsMore()
    {
        TerritorySettings.PartyInfo hunt = Settings.PartyOf(LandPartyKind.Hunt);
        var novice = new ResidentRecord(null, hunt.job);
        var practised = new ResidentRecord(null, hunt.job);
        practised.Talents.SetExperience(hunt.job.Skill.Id, LevelingSettings.Current.TotalExperienceFor(LevelingSettings.Current.MaxLevel));
        Assert.Greater(ResidentTalents.RankIndex(practised), 0, "a master of the job's skill");

        List<ItemAmount> least = LandParties.Roll(LandPartyKind.Hunt, novice, Settings, (low, high) => low);
        List<ItemAmount> most = LandParties.Roll(LandPartyKind.Hunt, practised, Settings, (low, high) => high);
        TerritorySettings.PartyYield first = hunt.yields[0];
        Assert.AreEqual(first.least, least.Find(a => a.Item == first.item).Amount, "the least");
        Assert.Greater(most.Find(a => a.Item == first.item).Amount, first.most, "a practised hand beats the novice's best");

        string odds = LandParties.Odds(LandPartyKind.Hunt, novice, Settings);
        StringAssert.Contains($"{first.least}-{first.most} {first.item.DisplayName}", odds, "the odds are shown before sending");
        StringAssert.Contains("sooner", LandParties.Odds(LandPartyKind.Scout, novice, Settings));
    }

    [Test]
    public void ATendedLandWearsLess_AndAScoutedOneIsWarnedSooner()
    {
        var tended = new TerritoryState();
        var left = new TerritoryState();
        tended.Claim("Eastwood", Settings);
        left.Claim("Eastwood", Settings);
        tended.AdvanceTurn(Settings, 1f, () => 1f, out _, new HashSet<string> { "Eastwood" });
        left.AdvanceTurn(Settings, 1f, () => 1f, out _);
        Assert.Greater(tended.HoldOf("Eastwood"), left.HoldOf("Eastwood"), "worked by a party, it holds better");
        Assert.Less(tended.HoldOf("Eastwood"), Settings.ClaimHold, "never a raise in hold");

        var scouted = new TerritoryState();
        var unseen = new TerritoryState();
        scouted.Claim("Eastwood", Settings);
        unseen.Claim("Eastwood", Settings);
        for (int i = 0; i < 30 && (scouted.Incursion == null || unseen.Incursion == null); i++)
        {
            if (scouted.Incursion == null)
            {
                scouted.AdvanceTurn(Settings, 0f, () => 0f, out _, null, new HashSet<string> { "Eastwood" });
            }

            if (unseen.Incursion == null)
            {
                unseen.AdvanceTurn(Settings, 0f, () => 0f, out _);
            }
        }

        Assume.That(scouted.Incursion != null && unseen.Incursion != null);
        Assert.AreEqual(Settings.WarningTurns + Settings.ScoutWarningTurns, scouted.Incursion.DueTurn - scouted.Turn, "seen coming sooner");
        Assert.AreEqual(Settings.WarningTurns, unseen.Incursion.DueTurn - unseen.Turn);
    }

    [Test]
    public void Parties_AreSaved()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var home = new SettlementState();
        home.SetParty(LandPartyKind.Fish, "Stillwater");
        home.SetParty(LandPartyKind.Scout, "Greyfell");
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), content);
        Assert.AreEqual("Stillwater", back.PartyOn(LandPartyKind.Fish));
        Assert.AreEqual("Greyfell", back.PartyOn(LandPartyKind.Scout));
        Assert.IsNull(back.PartyOn(LandPartyKind.Hunt));
        home.SetParty(LandPartyKind.Fish, null);
        Assert.IsNull(home.PartyOn(LandPartyKind.Fish), "called home");
    }
}
