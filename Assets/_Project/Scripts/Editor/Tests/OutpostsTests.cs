using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Outposts at the Secure lands (Outposts; Docs/EconomyAndPhases_2026-11-22.md §4.4): raised only on a Secure land worth one, kept by a
/// free resident who goes out on raids as a party does, sending the land's own material home, capped; paused by a push or a slipping
/// land; and the ring of Secure lands that gives a safe pocket slot; all kept by a save.
/// </summary>
public sealed class OutpostsTests
{
    private const string Land = "Greyfell";
    private static TerritorySettings Settings => TerritorySettings.Current;
    private static JobDefinition Job(string name) => AssetDatabase.LoadAssetAtPath<JobDefinition>($"Assets/_Project/Data/Settlement/{name}.asset");

    private static ResidentDefinition[] People(int count) => AssetDatabase.FindAssets("t:ResidentDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(one => one != null).Take(count).ToArray();

    private static SettlementState HomeWithASecureLand(out ResidentDefinition[] people)
    {
        people = People(2);
        var home = new SettlementState();
        home.AddResident(people[0]);
        home.AddResident(people[1]);
        home.Territory.Claim(Land, Settings);
        home.Territory.Raise(Land, 100f);
        Assume.That(home.Territory.StandingOf(Land, Settings) == RegionStanding.Secure);
        return home;
    }

    [Test]
    public void OutpostYields_AreTheLandsOwn_SmallAndCapped_AndNeverElderwood()
    {
        int lands = 0;
        foreach (TerritorySettings.RegionInfo region in Settings.Regions.Where(r => r != null && r.outpost.Count > 0))
        {
            lands++;
            foreach (TerritorySettings.PartyYield yield in region.outpost)
            {
                Assert.IsNotNull(yield.item, region.region);
                Assert.LessOrEqual(yield.most, 5, $"{region.region}: a small share of the tier's need, not a raid's worth");
                Assert.AreNotEqual("Elderwood", yield.item.name, "Elderwood is a raid wood (the user's answer 2)");
            }
        }

        Assert.GreaterOrEqual(lands, 4, "the lands worth an outpost");
        Assert.Greater(Settings.OutpostCost.Count, 0, "an outpost costs something");
        Assert.AreEqual(0, Settings.Info("Stillwater").outpost.Count, "a plain land has none: its tithe is its flavour");
    }

    [Test]
    public void AnOutpost_NeedsASecureLand_AndAFreeKeeper_WhoGoesOutOnRaids()
    {
        ResidentDefinition[] people = People(2);
        var home = new SettlementState();
        Assert.IsNotNull(Outposts.WhyNot(Land, false, home, Settings), "not a land the Haven holds");
        home.AddResident(people[0]);
        home.AddResident(people[1]);
        home.Territory.Claim(Land, Settings);
        Assert.IsNotNull(Outposts.WhyNot(Land, false, home, Settings), "held, but not Secure");
        home.Territory.Raise(Land, 100f);
        Assert.IsNull(Outposts.WhyNot(Land, false, home, Settings));
        Assert.IsNotNull(Outposts.WhyNot(Land, true, home, Settings), "not from a friend's Haven");
        Assert.IsNotNull(Outposts.WhyNot("Stillwater", false, home, Settings), "nothing out there worth one");

        ResidentRecord keeper = Outposts.Candidate(home, Settings);
        Assert.IsNotNull(keeper);
        home.SetOutpost(Land, keeper.Resident.Id);
        Assert.AreSame(keeper, Outposts.KeeperOf(home, Land));
        Assert.IsNotNull(Outposts.WhyNot(Land, false, home, Settings), "it has one");
        Assert.AreNotSame(keeper, Outposts.Candidate(home, Settings), "one keeper, one outpost");
        Assert.IsTrue(LandParties.IsOut(home, keeper, null, Settings), "out at the outpost while the player raids: their station waits");
        Assert.IsFalse(LandParties.IsOut(home, keeper, keeper.Resident, Settings), "along on the raid instead");

        Assert.AreEqual(1, Outposts.Working(home, Settings.PartyMinSeconds + 1f, null, Settings).Count);
        Assert.AreEqual(0, Outposts.Working(home, Settings.PartyMinSeconds - 1f, null, Settings).Count, "a raid too short");
        Assert.AreEqual(0, Outposts.Working(home, Settings.PartyMinSeconds + 1f, keeper.Resident, Settings).Count, "its keeper along on the raid");

        // A builder keeps building at home.
        var builders = new SettlementState();
        builders.AddResident(people[0], HomeLifeSettings.Current.Builders[0]);
        Assert.IsNull(Outposts.Candidate(builders, Settings), "a builder's work is the Haven's");
    }

    [Test]
    public void AnOutpost_SendsItsLandsMaterial_AndWaitsWhileTheLandIsntSecure()
    {
        SettlementState home = HomeWithASecureLand(out ResidentDefinition[] people);
        home.SetOutpost(Land, people[0].Id);
        TerritorySettings.PartyYield first = Settings.Info(Land).outpost[0];
        List<ItemAmount> least = Outposts.Roll(Land, Settings, (low, high) => low);
        List<ItemAmount> most = Outposts.Roll(Land, Settings, (low, high) => high);
        Assert.AreEqual(first.least, least.Find(a => a.Item == first.item).Amount);
        Assert.AreEqual(first.most, most.Find(a => a.Item == first.item).Amount);
        StringAssert.Contains($"{first.least}-{first.most} {first.item.DisplayName}", Outposts.Odds(Land, Settings), "the odds are shown first");
        Assert.IsNull(Outposts.Paused(home, Land, Settings));

        home.Territory.Restore(0, 0, 0, true, new[] { new TerritoryState.Hold { Region = Land, Claimed = true, EverClaimed = true, Value = Settings.SecureFrom - 1f } }, null);
        Assert.AreNotEqual(RegionStanding.Secure, home.Territory.StandingOf(Land, Settings));
        Assert.IsNotNull(Outposts.Paused(home, Land, Settings), "slipped below Secure: it waits");
        Assert.AreEqual(0, Outposts.Working(home, Settings.PartyMinSeconds + 1f, null, Settings).Count);
    }

    [Test]
    public void Outposts_AndTheRing_AreSaved()
    {
        SettlementState home = HomeWithASecureLand(out ResidentDefinition[] people);
        home.SetOutpost(Land, people[1].Id);
        Assert.IsFalse(home.RingHeld);
        home.HoldRing();
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), content);
        Assert.AreEqual(people[1].Id, back.OutpostKeeper(Land));
        Assert.IsTrue(back.RingHeld);
        Assert.Greater(Settings.RingPocketSlots, 0, "the ring held is worth a slot");
        home.SetOutpost(Land, null);
        Assert.IsNull(home.OutpostKeeper(Land), "given up");
    }
}
