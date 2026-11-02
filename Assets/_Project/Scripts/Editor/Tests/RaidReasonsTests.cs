using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>What raids alone can give: relics, people, plans, living things, and the rumours that promise them.</summary>
public sealed class RaidReasonsTests
{
    private const string Raid = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string Layout = "Assets/_Project/Data/Raids/Maps/MistyWoods Layout.asset";
    private const string People = "Assets/_Project/Data/Prologue/Residents/";

    private static RaidDefinition Woods => AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid);

    private static IEnumerable<RaidRumourDefinition> AllRumours => Woods.GetSystem<RaidRumoursSystem>().Pool;

    private static ResidentDefinition Person(string file) => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(People + file + ".asset");

    private static RaidRumourDefinition Rumour(string file) => AllRumours.First(rumour => rumour.name == file);

    private static SettlementState Survived(int raids)
    {
        var settlement = new SettlementState();
        for (int i = 0; i < raids; i++)
        {
            settlement.RecordExtraction();
        }

        return settlement;
    }

    private static int SeedAt(string hour)
    {
        for (int seed = 1; seed < 10000; seed++)
        {
            if (RaidTimeOfDay.For(Woods, seed).name == hour)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("No seed gives " + hour);
    }

    [Test]
    public void ARelic_FitsItsPower_AndNeverOutranksTheRaidByMuch()
    {
        var random = new System.Random(5);
        for (int i = 0; i < 300; i++)
        {
            ItemStack relic = RelicSettings.Current.Roll(random, 1);
            Assert.IsFalse(relic.IsEmpty);
            Assert.IsTrue(relic.Enchantment.IsRelic);
            Assert.IsTrue(relic.Enchantment.Fits(relic.Item), relic.DisplayName);
            Assert.LessOrEqual(relic.Item.Tier, 2, relic.DisplayName);
        }
    }

    [Test]
    public void Relics_ComeInManyForms()
    {
        var random = new System.Random(9);
        var seen = new HashSet<string>();
        var powers = new HashSet<EnchantmentDefinition>();
        for (int i = 0; i < 300; i++)
        {
            ItemStack relic = RelicSettings.Current.Roll(random, 1);
            seen.Add(relic.DisplayName);
            powers.Add(relic.Enchantment);
        }

        Assert.AreEqual(RelicSettings.Current.Powers.Count, powers.Count, "every power turns up");
        Assert.Greater(seen.Count, 40, "finding another stays worthwhile");
    }

    [Test]
    public void EveryRelicPower_DoesSomethingNumbersCant_AndSaysSo()
    {
        foreach (EnchantmentDefinition power in RelicSettings.Current.Powers)
        {
            Assert.IsTrue(power.IsRelic, power.name);
            Assert.IsNotEmpty(power.Effects, power.name);
            Assert.IsNotNull(power.Effects[0], power.name);
            Assert.IsNotEmpty(power.Effects[0].Describe(), power.name);
            Assert.IsFalse(EssenceSettings.Current.Enchantments.Contains(power), $"{power.name}: the altar must not offer it");
        }
    }

    [Test]
    public void Saves_KeepARelic()
    {
        ItemStack relic = RelicSettings.Current.Roll(new System.Random(3), 1);
        var bag = new ItemContainer(4, 1);
        bag.SetSlot(0, relic.WithEmpower(2));
        var content = Resources.Load<ContentDatabase>("ContentDatabase");
        SaveData data = SaveMapper.ToSaveData(PlayerSnapshot.Capture(bag, null, null), null, "SafeHaven", null, DateTime.UtcNow);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content);

        ItemStack back = loaded.Player.Slots.First().Stack;
        Assert.AreEqual(relic.Enchantment, back.Enchantment);
        Assert.AreEqual(2, back.Empower);
        Assert.IsEmpty(loaded.MissingContent);
    }

    [Test]
    public void Rumours_AreTheSameInTheBriefingAndTheRaid()
    {
        SettlementState settlement = Survived(5);
        for (int seed = 1; seed < 30; seed++)
        {
            CollectionAssert.AreEqual(RaidRumours.For(Woods, seed, settlement, null), RaidRumours.For(Woods, seed, settlement, null));
        }
    }

    [Test]
    public void ARun_HasItsRumours_EvenOnTheFirstRaid()
    {
        for (int seed = 1; seed < 60; seed++)
        {
            Assert.GreaterOrEqual(RaidRumours.For(Woods, seed, new SettlementState(), null).Count, 1, $"seed {seed}");
        }
    }

    [Test]
    public void NobodyIsSought_WhoHasComeHome()
    {
        RaidRumourDefinition tor = Rumour("Tor");
        int day = SeedAt("Day");
        SettlementState settlement = Survived(1);
        Assert.IsFalse(tor.CanComeUp(RaidTimeOfDay.For(Woods, day), settlement, null), "nowhere for him to work yet");
        settlement.NoteBuilt(AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Sawbench.asset"), 1);
        Assert.IsTrue(tor.CanComeUp(RaidTimeOfDay.For(Woods, day), settlement, null));
        settlement.AddResident(Person("Tor"));
        Assert.IsFalse(tor.CanComeUp(RaidTimeOfDay.For(Woods, day), settlement, null));
    }

    [Test]
    public void SomeThings_AreOnlyOutThereAtNight()
    {
        RaidRumourDefinition cairn = Rumour("NightCairn");
        var settlement = new SettlementState();
        Assert.IsFalse(cairn.CanComeUp(RaidTimeOfDay.For(Woods, SeedAt("Day")), settlement, null));
        Assert.IsTrue(cairn.CanComeUp(RaidTimeOfDay.For(Woods, SeedAt("Night")), settlement, null));
        Assert.IsTrue(cairn.CanComeUp(RaidTimeOfDay.For(Woods, SeedAt("Dusk")), settlement, null));
    }

    [Test]
    public void TheBreachRumour_OnlyComesUp_WhenABreachIsNear()
    {
        RaidRumourDefinition source = Rumour("BreachSource");
        RaidTimeOfDay hour = RaidTimeOfDay.For(Woods, SeedAt("Day"));
        var settlement = new SettlementState();
        Assert.IsFalse(source.CanComeUp(hour, settlement, null));
        settlement.WarnOfBreach();
        Assert.IsTrue(source.CanComeUp(hour, settlement, null));
    }

    [Test]
    public void ATracker_AddsARumour()
    {
        var runa = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Tracker.asset");
        SettlementState without = Survived(5);
        SettlementState with = Survived(5);
        with.AddResident(Person("Runa"), runa);
        int seed = SeedAt("Night");
        Assert.AreEqual(RaidRumours.For(Woods, seed, without, null).Count + 1, RaidRumours.For(Woods, seed, with, null).Count);
    }

    [Test]
    public void AMap_HasPlaceForEveryRumour_EvenWithATracker()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(Layout);
        RaidRumoursSystem system = Woods.GetSystem<RaidRumoursSystem>();
        Assert.GreaterOrEqual(rules.Sites.First(site => site.kind == RaidSiteKind.Rumour).count, system.Count + 1);
    }

    [Test]
    public void EveryRumour_LeadsToAChoice()
    {
        foreach (RaidRumourDefinition rumour in AllRumours)
        {
            Assert.IsNotEmpty(rumour.Briefing, rumour.name);
            Assert.IsNotNull(rumour.Site.SitePrefab, rumour.name);
            Assert.IsTrue(rumour.Site.SitePrefab.TryGetComponent(out RaidEventSite _), rumour.name);
            Assert.IsNotEmpty(rumour.Site.Choices, rumour.name);
            foreach (RaidEventChoice choice in rumour.Site.Choices)
            {
                Assert.IsFalse(choice.outcomes.Contains(null), $"{rumour.name}: {choice.label}");
            }

            Assert.AreEqual(0f, rumour.Site.Weight, $"{rumour.name}: only a rumour places it");
        }
    }

    [Test]
    public void WhatRaidsTeach_HasToReachHomeFirst()
    {
        foreach (string file in new[] { "WainwrightsLedger", "BathhousePlans", "MoonsilverFolio", "SwarmInASkep", "PouchOfWildSeed", "ElderSapling" })
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/{file}.asset");
            Assert.IsTrue(item.IsLearnedAtHome, file);
            Assert.IsTrue(item.IsBlueprint, file);
            foreach (BuildingDefinition building in item.TeachesBuildings)
            {
                Assert.IsFalse(building.KnownFromStart, $"{file} must teach something new");
            }

            foreach (RecipeDefinition recipe in item.TeachesRecipes)
            {
                Assert.IsFalse(recipe.KnownFromStart, $"{file} must teach something new");
            }
        }
    }

    [Test]
    public void EveryoneOfTheExpedition_HasAJobThatDoesSomething()
    {
        foreach (string file in new[] { "Tor", "Eirik", "Runa", "Liv", "Sigrid", "Bjarne" })
        {
            ResidentDefinition person = Person(file);
            Assert.IsNotEmpty(person.Jobs, file);
            JobDefinition job = person.Jobs[0];
            Assert.IsTrue(job.DescribeBonus().Length > 0 || job.TeachesRecipes.Count > 0, file);
            Assert.AreNotEqual("You came back. I knew the forest had not taken all of us.", person.RescueLine, $"{file} has a voice of their own");
        }
    }

    [Test]
    public void JobBonuses_AddUp_OnlyWhileTheJobIsHeld()
    {
        var smith = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Smith.asset");
        var settlement = new SettlementState();
        settlement.AddResident(Person("Bjarne"));
        Assert.AreEqual(0f, settlement.JobBonus(job => job.GearRepairDiscount));
        settlement.AssignJob(Person("Bjarne"), smith);
        Assert.AreEqual(0.5f, settlement.JobBonus(job => job.GearRepairDiscount), 0.001f);
    }

    [Test]
    public void BlowsAgainstABreach_AddUpToALimit_AndEndWithIt()
    {
        var settlement = new SettlementState();
        settlement.WeakenBreach(0.3f, 0.6f);
        settlement.WeakenBreach(0.3f, 0.6f);
        settlement.WeakenBreach(0.3f, 0.6f);
        Assert.AreEqual(0.6f, settlement.BreachWeakening, 0.001f);
        settlement.WinBreach(3);
        Assert.AreEqual(0f, settlement.BreachWeakening);
    }

    [Test]
    public void Saves_RememberTheBlowAgainstTheBreach()
    {
        var settlement = new SettlementState();
        settlement.WeakenBreach(0.3f, 0.6f);
        var content = Resources.Load<ContentDatabase>("ContentDatabase");
        SaveData data = SaveMapper.ToSaveData(PlayerSnapshot.Capture(new ItemContainer(1, 1), null, null), null, "SafeHaven", null, DateTime.UtcNow, settlement: settlement);
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), content);
        Assert.AreEqual(0.3f, loaded.Settlement.BreachWeakening, 0.001f);
    }

    [Test]
    public void TheLayoutRules_MatchTheMapTheyDrawOn()
    {
        var rules = AssetDatabase.LoadAssetAtPath<RaidLayoutRules>(Layout);
        var map = AssetDatabase.LoadAssetAtPath<RaidMapDefinition>("Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset");
        Assert.AreEqual(rules, Woods.LayoutRules);
        Assert.AreEqual(4, rules.Shapes.Count);
        Assert.GreaterOrEqual(rules.Moods.Count, 4);
        foreach (RaidMapMood mood in rules.Moods)
        {
            Assert.IsNotEmpty(mood.briefing, mood.name);
            if (mood.treeWeights.Count > 0)
            {
                Assert.AreEqual(map.Trees.Count, mood.treeWeights.Count, $"{mood.name} names a weight for every tree");
            }
        }
    }
}
