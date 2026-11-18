using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The rules Docs/BaseProgression.md promises, checked against the real assets.</summary>
public sealed class HavenProgressionTests
{
    private static IEnumerable<T> All<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static BuildingDefinition Building(string file) => AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/" + file + ".asset");

    private static List<BuildingDefinition> Menu => All<BuildingDefinition>().Where(building => building.ShowInBuildMenu).ToList();

    [Test]
    public void EveryRecipe_CanBeReachedAtItsStation()
    {
        foreach (RecipeDefinition recipe in All<RecipeDefinition>().Where(one => !one.IsHandcrafted && one.Station != CraftingStationType.Portal))
        {
            int best = Menu.Where(building => building.Station == recipe.Station).Select(building => building.MaxLevel).DefaultIfEmpty(0).Max();
            Assert.GreaterOrEqual(best, recipe.RequiredStationTier, $"{recipe.name} needs tier {recipe.RequiredStationTier} of {recipe.Station}, which only goes to {best}");
        }
    }

    [Test]
    public void NoUpgrade_IsOnlySpeed()
    {
        var recipes = All<RecipeDefinition>().ToList();
        foreach (BuildingDefinition building in Menu)
        {
            for (int tier = 2; tier <= building.MaxLevel; tier++)
            {
                BuildingLevel level = building.UpgradeTo(tier);
                bool opensRecipe = building.Station != CraftingStationType.None && recipes.Any(recipe => recipe.Station == building.Station && recipe.RequiredStationTier == tier);
                bool opensBuilding = Menu.Any(other => other.Requires.Any(need => need.building == building && need.tier == tier));
                bool brings = level.extraStorage > 0 || level.raidSeconds > 0f || level.extraRumours > 0 || level.comfort > 0 || level.extraLogs > 0 || level.teachesRecipes.Count > 0
                    || level.beds > 0 || level.purifyYield > 0f || level.extraMealSlots > 0 || level.extraGame > 0 || level.extraLight > 0f;
                Assert.IsTrue(opensRecipe || opensBuilding || brings, $"{building.DisplayName} tier {tier} ({level.title}) only makes it faster");
            }
        }
    }

    [Test]
    public void TheChain_KeepsOpeningBuildings_PastCopper()
    {
        // Which buildings they are is the content's to choose; that the late game still has several to open is the rule.
        var late = Menu.Where(building => building.RequiredRank >= 3 || building.Requires.Any(need => need.tier >= 3)).Select(building => building.Id).ToList();
        Assert.GreaterOrEqual(late.Count, 4, "buildings still to open past copper: " + string.Join(", ", late));
    }

    [Test]
    public void NoUpgradeOrBuilding_AsksForWhatDoesNotExist()
    {
        foreach (BuildingDefinition building in Menu)
        {
            foreach (BuildingRequirement need in building.Requires)
            {
                Assert.NotNull(need.building, building.name);
                Assert.GreaterOrEqual(need.building.MaxLevel, need.tier, $"{building.name} asks for {need.building.name} tier {need.tier}");
            }

            for (int tier = 2; tier <= building.MaxLevel; tier++)
            {
                foreach (BuildingRequirement need in building.UpgradeTo(tier).requires)
                {
                    Assert.NotNull(need.building, building.name);
                    Assert.GreaterOrEqual(need.building.MaxLevel, need.tier, $"{building.name} {tier} asks for {need.building.name} tier {need.tier}");
                }
            }
        }
    }

    [Test]
    public void WorkOrders_AreMadeWhereTheJobWorks()
    {
        foreach (JobDefinition job in All<JobDefinition>())
        {
            foreach (WorkOrder order in job.WorkOrders)
            {
                Assert.NotNull(order.recipe, job.name);
                Assert.NotNull(job.Workplace, job.name);
                Assert.AreEqual(job.Workplace.Station, order.recipe.Station, $"{job.name} is asked to make {order.recipe.name} away from {job.Workplace.name}");
                Assert.GreaterOrEqual(job.Workplace.MaxLevel, order.recipe.RequiredStationTier, $"{job.name}: {order.recipe.name}");
            }
        }
    }

    [Test]
    public void Beds_HelpButNobodyNeedsOne()
    {
        var settlement = new SettlementState();
        ResidentDefinition first = ScriptableObject.CreateInstance<ResidentDefinition>();
        ResidentDefinition second = ScriptableObject.CreateInstance<ResidentDefinition>();
        try
        {
            Assert.IsTrue(settlement.AddResident(first), "a person comes home with no bed to go to");
            Assert.IsTrue(settlement.AddResident(second));
            Assert.AreEqual(0, settlement.Housed);
            Assert.IsFalse(settlement.HasBed(settlement.Find(first)));
        }
        finally
        {
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        Assert.Greater(CraftingStation.BedSpeedBonus, 0f);
        Assert.AreEqual(2, Building("Hut").Beds);
        Assert.AreEqual(4, Building("Cottage").Beds);
        Assert.AreEqual(0, Building("Hut").Requires.Count(need => need.tier > 1), "a first bed is within reach of the first raid");
    }

    [Test]
    public void TheWard_OpensTheRaidTiers()
    {
        RaidDefinition raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        Assert.GreaterOrEqual(SettlementSettings.Current.WardLevels.Count, raid.Difficulties.Max(tier => tier.wardLevelNeeded), "a tier asks for a ward level that doesn't exist");
        Assert.IsNull(RaidLauncher.CheckWard(raid.Difficulties[0]), "the first tier is open to anyone");
        foreach (RaidDifficulty tier in raid.Difficulties.Skip(1))
        {
            Assert.AreEqual(tier.tier - 1, tier.wardLevelNeeded);
        }
    }

    [Test]
    public void ThePortal_ShortensTheWayHome_AndGrowsThePocket()
    {
        SettlementSettings settings = SettlementSettings.Current;
        Assert.AreEqual(0f, settings.ExtractionCutAt(0));
        Assert.AreEqual(0, settings.ExtraPocketSlotsAt(0));
        Assert.Greater(settings.ExtractionCutAt(int.MaxValue), 0f);
        Assert.Greater(settings.ExtraPocketSlotsAt(int.MaxValue), 0);

        RaidDefinition raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        float plain = RaidParameters.Calculate(raid, null, 0f).ExtractionTime;
        float cut = RaidParameters.Calculate(raid, null, 0f, null, null, 0.2f).ExtractionTime;
        Assert.AreEqual(plain * 0.8f, cut, 0.01f);
    }

    [Test]
    public void TheAltarsYield_AddsUp_WithoutLosingFractions()
    {
        var settlement = new SettlementState();
        settlement.SetPurificationYield(0.25f);
        int made = 0;
        for (int i = 0; i < 8; i++)
        {
            made += settlement.PurifiedFrom(1);
        }

        Assert.AreEqual(10, made);
    }

    [Test]
    public void TheCaravan_ComesBack_AfterARaid()
    {
        var settlement = new SettlementState();
        settlement.NoteBought("iron");
        settlement.NoteBought("iron");
        Assert.AreEqual(2, settlement.BoughtSinceLastRaid("iron"));
        settlement.RecordExtraction();
        Assert.AreEqual(0, settlement.BoughtSinceLastRaid("iron"));
    }

    [Test]
    public void TheCaravan_DealsInThingsThatExist_AndKeepsToTheLadder()
    {
        TradeSettings trade = TradeSettings.Current;
        Assert.NotNull(trade.Coin);
        Assert.IsNotEmpty(trade.Sales);
        foreach (TradeSettings.Offer offer in trade.Offers)
        {
            Assert.NotNull(offer.item);
            Assert.LessOrEqual(offer.item.Tier, offer.smithyTier + 2, $"{offer.item.name} climbs ahead of the Smithy");
        }
    }

    [Test]
    public void Ranks_LandThroughTheGame()
    {
        IReadOnlyList<HavenSettings.Rank> ranks = HavenSettings.Current.Ranks;
        for (int i = 1; i < ranks.Count; i++)
        {
            Assert.Greater(ranks[i].renown, ranks[i - 1].renown);
        }

        Assert.Greater(ranks[ranks.Count - 1].renown, 180, "the top rank waits for the late game");
        // Nobody comes with a rank any more (HomeStoryTests.NoRank_BringsSomeone): people are found on raids.
    }

    [Test]
    public void WellRested_KeepsItsWorth_AsGearImproves()
    {
        // A share stat (experience gain, move speed) is relative already; a flat amount of health or stamina is not.
        Assert.IsTrue(HavenSettings.Current.RestedBonuses.All(bonus => bonus.op == StatModifierOp.Percent || StatNames.IsFraction(bonus.stat)),
            "a flat bonus shrinks against a late kit");
    }

    [Test]
    public void LateMealsAndPotions_Exist()
    {
        var items = All<ItemDefinition>().ToList();
        Assert.IsTrue(items.Any(item => item.Category == ItemCategory.Consumable && item.Tier == 4 && item.HasBoon && !item.IsTonic), "a tier IV meal");
        Assert.IsTrue(items.Any(item => item.Category == ItemCategory.Consumable && item.Tier == 4 && item.HealAmount >= 150f), "a tier IV potion");
        foreach (string slot in new[] { "TrollstoneHauberk", "TrollstoneHelm", "TrollstoneGauntlets", "TrollstoneBoots", "TrollstoneGirdle" })
        {
            Assert.IsTrue(items.Any(item => item.name == slot && item.Tier == 5), slot);
        }
    }

    [Test]
    public void TheFolio_IsSoughtOnceTheSmithyCanUseIt()
    {
        var folio = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/Folio.asset");
        Assert.IsTrue(folio.Urgent, "moonsilver can't wait on luck");
        var below = new SettlementState();
        below.NoteBuilt(Building("Forge"), 2);
        var ready = new SettlementState();
        ready.NoteBuilt(Building("Forge"), 3);
        RaidTimeOfDay day = null;
        Assert.IsFalse(folio.CanComeUp(day, below, null));
        Assert.IsTrue(folio.CanComeUp(day, ready, null));
    }

    [Test]
    public void NoStoryText_StillSpeaksOfTheHeartTree()
    {
        foreach (string folder in new[] { "Quests", "Settlement", "ResourceInventory", "Prologue" })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project/Data/" + folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SerializedProperty it = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path)).GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType == SerializedPropertyType.String)
                    {
                        StringAssert.DoesNotContain("Heart Tree", it.stringValue, path);
                    }
                }
            }
        }
    }

    [Test]
    public void SavedTalksWithThePortal_StillCount()
    {
        // Saves from before deeds remember talking to the portal by the old event's id ("talkedtohearttree").
        var portal = AssetDatabase.LoadAssetAtPath<QuestTargetDefinition>("Assets/_Project/Data/Quests/Targets/The Portal.asset");
        Assert.NotNull(portal);
        var log = new QuestLog();
        log.RestoreHappened("talkedtohearttree");
        Assert.IsTrue(log.HasDone(QuestDeeds.Key(GameEventKind.Talked, portal)), "an old save's talk with the portal is the deed");
    }

    [Test]
    public void ThePortalsVoice_HasTakes_AndIsHeardAcrossTheBase()
    {
        var voice = AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Portal Voice.asset");
        Assert.NotNull(voice);
        Assert.IsTrue(voice.HasClips);
        Assert.AreEqual(1, voice.MaxAtOnce, "takes must never pile up when lines are skipped");
        Assert.GreaterOrEqual(voice.AudibleDistance, 60f, "it speaks wherever the player is in the ward");
    }

    [Test]
    public void ASpeaker_CarriesTheirVoice_ToTheBox()
    {
        var voice = ScriptableObject.CreateInstance<SoundDefinition>();
        try
        {
            var from = new StorySpeaker { name = "The Portal", voice = voice }.ToSpeaker();
            Assert.AreEqual(voice, from.Voice);
            Assert.IsNull(new StorySpeaker { name = "Sigrid" }.ToSpeaker().Voice, "people are only read");
        }
        finally
        {
            Object.DestroyImmediate(voice);
        }
    }

    [Test]
    public void TheMix_DipsQuickly_AndComesBackSlowly()
    {
        float level = AudioDucking.Step(0f, true, 0.3f, 1.4f, 0.15f);
        Assert.AreEqual(0.5f, level, 0.001f);
        Assert.AreEqual(1f, AudioDucking.Step(level, true, 0.3f, 1.4f, 1f), "it stays down while wanted, no further");
        Assert.AreEqual(1f - 0.7f / 1.4f, AudioDucking.Step(1f, false, 0.3f, 1.4f, 0.7f), 0.001f);
        Assert.AreEqual(1f, AudioDucking.Step(0.5f, true, 0f, 0f, 0.01f), "no fade time means at once");
        Assert.AreEqual(1f, AudioDucking.Scale(0f, 0.35f), "undipped, nothing changes");
        Assert.AreEqual(0.35f, AudioDucking.Scale(1f, 0.35f), 0.001f);
    }

    [Test]
    public void BigMoments_TakeTheMix_AndAreNotCrowdedOut()
    {
        AudioSettingsDefinition sounds = AssetDatabase.FindAssets("t:AudioSettingsDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AudioSettingsDefinition>).First();
        foreach (SoundDefinition moment in new[] { sounds.QuestStarted, sounds.QuestCompleted, sounds.BreachWarning, sounds.BreachWon, sounds.BreachLost, HavenSettings.Current.RankSound,
                     AssetDatabase.LoadAssetAtPath<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Portal Voice.asset") })
        {
            Assert.NotNull(moment);
            Assert.IsTrue(moment.TakesTheMix, moment.name);
            Assert.AreEqual(SoundPriority.Important, moment.Priority, moment.name);
        }

        Assert.Less(sounds.MusicWhileSpeaking, sounds.AmbienceWhileSpeaking, "music gives way first, so the world doesn't go silent");
        Assert.Less(sounds.DipSeconds, sounds.ReturnSeconds, "a slow return keeps the music from pumping");
        Assert.Greater(sounds.MusicWhileSpeaking, 0f, "the music dips, it doesn't stop");
    }
}
