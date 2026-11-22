using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Forays (Palworld's expeditions, the user 2026-11-22): power against what a place asks sets the odds, the time and the haul; a
/// place opens with the portal's tier; those who go are away (no work, no party, never along) until they are home, which a save keeps;
/// gone wrong they bring a share home, bruised; and one standing order each (ResidentsOut, the review of 2026-11-22).
/// </summary>
public sealed class ForaysTests
{
    private static ForaySettings Settings => ForaySettings.Current;
    private static ContentDatabase Content => Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);

    private static ResidentDefinition[] People(int count) => AssetDatabase.FindAssets("t:ResidentDefinition")
        .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(one => one != null).Take(count).ToArray();

    private static SettlementState Home(int people, out ResidentDefinition[] who)
    {
        who = People(people);
        var home = new SettlementState();
        foreach (ResidentDefinition one in who)
        {
            home.AddResident(one);
        }

        home.RepairPortal();
        return home;
    }

    private static ItemContainer ChestsWithBread(int loaves)
    {
        var chests = new ItemContainer(10, 6);
        chests.Add(Content.Items.First(i => i != null && i.name == "BarleyBread"), loaves);
        return chests;
    }

    [Test]
    public void MorePowerThanAsked_IsLikelier_Quicker_AndBringsMore()
    {
        ForaySettings.Destination place = Settings.Destinations[0];
        Assert.AreEqual(Settings.ChanceAtPower, Forays.Chance(place.power, place.power, Settings), 0.001f, "at the power asked");
        Assert.Greater(Forays.Chance(place.power * 1.1f, place.power, Settings), Forays.Chance(place.power * 0.5f, place.power, Settings));
        Assert.LessOrEqual(Forays.Chance(place.power * 10f, place.power, Settings), Settings.MostChance, "never certain");
        Assert.GreaterOrEqual(Forays.Chance(0f, place.power, Settings), Settings.LeastChance);

        float asked = Forays.Seconds(place.power, place, Settings);
        Assert.AreEqual(place.minutes * 60f, asked, 0.01f);
        Assert.Less(Forays.Seconds(place.power * 2f, place, Settings), asked, "more power, a quicker trip");
        Assert.Greater(Forays.Seconds(place.power * 0.5f, place, Settings), asked, "less, a slower one");
        Assert.GreaterOrEqual(Forays.Seconds(place.power * 100f, place, Settings), place.minutes * 60f * Settings.Shortest - 0.01f);
        Assert.AreEqual(Settings.MostHaul, Forays.HaulScale(place.power * 100f, place.power, Settings), 0.001f);

        // Rank, raids along and gear each add power: level, experience, equipment.
        var novice = new ResidentRecord(People(1)[0]);
        var veteran = new ResidentRecord(People(1)[0]) { WorkDone = 100000, RaidsAlong = 5 };
        Assume.That(SettlementSettings.Current.RankIndex(veteran.WorkDone) > 0);
        Assert.AreEqual(Settings.BasePower, Forays.PowerOf(novice, Settings), 0.001f);
        Assert.Greater(Forays.PowerOf(veteran, Settings), Forays.PowerOf(novice, Settings) + 5f * Settings.PowerPerRaid);
    }

    [Test]
    public void APlaceOpensWithThePortalsTier()
    {
        SettlementState home = Home(2, out _);
        ForaySettings.Destination first = Settings.Destinations.First(d => d.tier == 1);
        ForaySettings.Destination later = Settings.Destinations.First(d => d.tier >= 2);
        List<ResidentRecord> party = Forays.Strongest(home, 2, Settings, TerritorySettings.Current);
        ItemContainer chests = ChestsWithBread(10);
        Assert.IsNull(Forays.WhyNot(home, first, party, chests, false, Settings));
        StringAssert.Contains("portal", Forays.WhyNot(home, later, party, chests, false, Settings));
        Assert.IsNotNull(Forays.WhyNot(home, first, party, chests, true, Settings), "not from a friend's Haven");
        StringAssert.Contains("food", Forays.WhyNot(home, first, party, new ItemContainer(10, 6), false, Settings), "food for the road");
    }

    [Test]
    public void ThoseWhoGo_AreAwayUntilHome_ASaveKeepsThem_AndTheHaulComesHome()
    {
        SettlementState home = Home(3, out ResidentDefinition[] people);
        ItemContainer chests = ChestsWithBread(10);
        ForaySettings.Destination place = Settings.Destinations.First(d => d.tier == 1);
        List<ResidentRecord> party = Forays.Strongest(home, 2, Settings, TerritorySettings.Current);
        Assert.AreEqual(2, party.Count);
        Assert.IsTrue(Forays.Send(home, place, party, chests, Settings));
        Assert.Less(chests.Count(Content.Items.First(i => i.name == "BarleyBread")), 10, "food for the road was taken");
        foreach (ResidentRecord member in party)
        {
            Assert.IsTrue(member.IsAway);
            Assert.IsTrue(member.WorkPaused, "away is no work: every work check covers it");
            Assert.IsFalse(member.PausedByPlayer, "but not the player's own pause");
        }

        ResidentRecord stayed = home.Residents.First(r => !party.Contains(r));
        Assert.IsFalse(stayed.IsAway);
        Assert.IsNotNull(Forays.WhyNot(home, place, new[] { stayed }, chests, false, Settings), "one foray at a time");

        // Saved and read back: still out, the same ones away.
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), Content);
        Assert.IsNotNull(back.Foray);
        Assert.AreEqual(home.Foray.SecondsLeft, back.Foray.SecondsLeft, 0.01f);
        Assert.AreEqual(2, back.Residents.Count(r => r.IsAway));
        Assert.IsFalse(back.Residents.Any(r => r.PausedByPlayer), "away isn't saved as a pause");

        // Time passes; it comes home, won (every roll 0: each find comes, at its least).
        int before = chests.CountItems();
        Forays.Tick(home, home.Foray.SecondsLeft - 1f, chests, Settings, () => 0f, (least, most) => least);
        Assert.IsNotNull(home.Foray, "not yet");
        Forays.Tick(home, 2f, chests, Settings, () => 0f, (least, most) => least);
        Assert.IsNull(home.Foray, "home");
        Assert.IsFalse(party.Any(r => r.IsAway || r.Bruised));
        Assert.Greater(chests.CountItems(), before, "the haul is in the chests");
    }

    [Test]
    public void AForayGoneWrong_BringsAShare_AndBruises_NobodyLost()
    {
        SettlementState home = Home(2, out _);
        ItemContainer chests = ChestsWithBread(10);
        ForaySettings.Destination place = Settings.Destinations.First(d => d.tier == 1);
        List<ResidentRecord> party = Forays.Strongest(home, 2, Settings, TerritorySettings.Current);
        Forays.Send(home, place, party, chests, Settings);
        float chance = home.Foray.Chance;
        // The odds roll is the first: above them it went wrong; the finds still roll 0 (all come), at their most.
        bool first = true;
        Forays.Tick(home, home.Foray.SecondsLeft + 1f, chests, Settings, () => { bool odds = first; first = false; return odds ? Mathf.Min(0.999f, chance + 0.01f) : 0f; },
            (least, most) => most);
        Assert.IsNull(home.Foray);
        Assert.AreEqual(2, home.Residents.Count, "nobody is lost");
        Assert.IsTrue(party.All(r => r.Bruised), "bruised");
        Assert.Less(HomeLife.WorkBonus(party[0]), HomeLife.HomeBonus(party[0]) + 0.001f, "slower at work for it");
        var whole = Forays.Haul(place, 1f, () => 0f, (least, most) => most).Sum(a => a.Amount);
        var share = Forays.Haul(place, Settings.FailedHaul, () => 0f, (least, most) => most).Sum(a => a.Amount);
        Assert.Less(share, whole, "a share of the haul");
    }

    [Test]
    public void OneStandingOrderEach_AKeeperNeverLeadsAParty_AndTheAwayAreCountedNowhere()
    {
        var hunter = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Hunter.asset");
        var gunnar = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Settlement/Gunnar.asset");
        Assume.That(hunter != null && gunnar != null);
        var home = new SettlementState();
        home.AddResident(gunnar, hunter);
        TerritorySettings lands = TerritorySettings.Current;
        Assert.AreEqual(gunnar, LandParties.WhoGoes(home, LandPartyKind.Hunt, null, lands).Resident, "the canary: the Hunter leads a hunt");
        home.SetOutpost("Greyfell", gunnar.Id);
        Assert.IsNull(LandParties.WhoGoes(home, LandPartyKind.Hunt, null, lands), "keeping an outpost, not hunting too");
        Assert.IsFalse(Forays.CanGo(home, home.Find(gunnar), lands), "nor off on a foray, leaving the outpost unkept");
        home.SetOutpost("Greyfell", null);

        // Away on a foray: no builder, no helper, no party leader.
        SettlementState builders = Home(0, out _);
        JobDefinition carpenter = HomeLifeSettings.Current.Builders[0];
        ResidentDefinition one = People(1)[0];
        builders.AddResident(one, carpenter);
        Assert.AreEqual(1, ResidentBuilders.Count(builders));
        builders.SetForay(new ForayRecord(Settings.Destinations[0].id, new[] { one.Id }, 60f, 60f, 0.8f, 1f));
        Assert.AreEqual(0, ResidentBuilders.Count(builders), "away, building nothing");
        Assert.IsFalse(ResidentBuilders.Helps(builders.Find(one)));
        Assert.IsTrue(ResidentsOut.Now(builders, builders.Find(one), null), "out of the Haven, asked in one place");
    }

    // The review of 2026-11-22, cause A: away on a foray was known only to WorkPaused and the hidden body; whatever asked "who is here"
    // its own way (a station's worker, the guards, breakfast) still counted them. One rule now: the away are in the Haven for nothing.
    [Test]
    public void TheAway_AreInTheHavenForNothing_NoStation_NoGuardPost_NoBreakfast()
    {
        JobDefinition job = AssetDatabase.FindAssets("t:JobDefinition").Select(g => AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .First(j => j != null && j.Workplace != null && j.Workplace.Station != CraftingStationType.None);
        ResidentDefinition[] people = People(2);
        var home = new SettlementState();
        home.AddResident(people[0], job);
        home.AddResident(people[1], job);
        CraftingStationType station = job.Workplace.Station;
        Assert.AreSame(people[0], home.WorkerAt(station).Resident, "the canary: the first of the trade works it");
        home.SetForay(new ForayRecord(Settings.Destinations[0].id, new[] { people[0].Id }, 60f, 60f, 0.8f, 1f));
        Assert.AreSame(people[1], home.WorkerAt(station).Resident, "the other of the trade takes it meanwhile");
        Assert.IsFalse(ResidentsOut.Working(home, null).Any(r => r.Resident == people[0]), "not at work in the Haven");

        var guardJob = AssetDatabase.FindAssets("t:JobDefinition").Select(g => AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(j => j != null && j.DefendsAs != null);
        Assume.That(guardJob != null);
        var guarded = new SettlementState();
        guarded.AddResident(people[0], guardJob);
        Assert.IsTrue(GuardPosts.Defends(guarded.Find(people[0])));
        Assert.AreEqual(1, GuardPosts.Defenders(guarded));
        guarded.SetForay(new ForayRecord(Settings.Destinations[0].id, new[] { people[0].Id }, 60f, 60f, 0.8f, 1f));
        Assert.IsFalse(GuardPosts.Defends(guarded.Find(people[0])), "no guard post kept from the woods");
        Assert.AreEqual(0, GuardPosts.Defenders(guarded));

        HomeLifeSettings meals = HomeLifeSettings.Current;
        home.NoteBuilt(meals.MealsFrom, 1);
        ItemContainer chests = ChestsWithBread(1);
        home.Find(people[1]).Meal = ResidentMeal.Fed;
        ResidentMeals.Eat(home, chests, meals);
        Assert.AreEqual(ResidentMeal.WellFed, home.Find(people[1]).Meal, "the one at home has the only loaf");
    }

    [Test]
    public void AFailedForaysShare_IsRoundedByChance_NotAlwaysNothing()
    {
        ForaySettings.Destination place = Settings.Destinations.First(d => d.loot.Any(l => l.most >= 3));
        // A quarter of each find: with every chance roll 0 the fraction rounds up, with every roll just under 1 it rounds down.
        int up = Forays.Haul(place, 0.25f, () => 0f, (least, most) => most).Sum(a => a.Amount);
        int down = Forays.Haul(place, 0.25f, () => 0.999f, (least, most) => most).Sum(a => a.Amount);
        // Without luck only the sure finds come, each its whole quarters (8 Nettle Fiber is 2); with luck every find comes, and each
        // fraction rounds up (3 Copper Ore is 1, not 0).
        int whole = place.loot.Where(l => l.item != null && l.chance > 0.999f).Sum(l => Mathf.FloorToInt(l.most * 0.25f));
        Assert.AreEqual(whole, down);
        Assert.Greater(up, down, "a small share comes home with luck, where plain rounding gave nothing");
    }
}
