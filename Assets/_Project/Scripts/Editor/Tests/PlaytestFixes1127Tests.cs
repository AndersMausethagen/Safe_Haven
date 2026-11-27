using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The user's playtest notes of 2026-11-27: nodes grow back at home by days or raids, meals last half an hour and hold hunger, the
/// character sheet says what gave each effect, and a thief carrying loot is slower.
/// </summary>
public sealed class PlaytestFixes1127Tests
{
    private static GatheringNodeDefinition Node(ToolType tool, float respawnSeconds, int ownDays = 0)
    {
        var node = ScriptableObject.CreateInstance<GatheringNodeDefinition>();
        var data = new SerializedObject(node);
        data.FindProperty("gatherTool").intValue = (int)tool;
        data.FindProperty("respawnSeconds").floatValue = respawnSeconds;
        data.FindProperty("regrowsAtHomeAfterDays").intValue = ownDays;
        data.ApplyModifiedPropertiesWithoutUndo();
        return node;
    }

    [Test]
    public void ANodeGatheredAtHome_ComesBackAfterItsDaysOrRaids_WhicheverIsFirst()
    {
        Assert.IsFalse(Regrowth.HomeDue(13, 7, 12, 6), "day 12 of 13, raid 6 of 7: not yet");
        Assert.IsTrue(Regrowth.HomeDue(13, 7, 13, 6), "its day has come");
        Assert.IsTrue(Regrowth.HomeDue(13, 7, 12, 7), "or its third raid");
        Assert.IsFalse(Regrowth.HomeDue(0, 0, 99, 99), "a node that waits for nothing never comes back");
    }

    [Test]
    public void StoneAndOre_GrowBackAtHome_TreesAndOvergrowthDont()
    {
        HavenSettings haven = HavenSettings.Current;
        GatheringNodeDefinition stone = Node(ToolType.Pickaxe, 180f), tree = Node(ToolType.Axe, 180f), bramble = Node(ToolType.None, 0f),
            bush = Node(ToolType.None, 180f, ownDays: 5);
        try
        {
            Assert.Greater(haven.RegrowAfterDays, 0, "the Haven's days are set");
            Assert.AreEqual(haven.RegrowAfterDays, Regrowth.HomeDays(stone), "stone and ore come back after the Haven's days");
            Assert.AreEqual(haven.RegrowAfterRaids, Regrowth.HomeRaids(stone), "or its raids");
            Assert.AreEqual(0, Regrowth.HomeDays(tree), "a felled tree stays felled: saplings bring trees back");
            Assert.AreEqual(0, Regrowth.HomeDays(bramble), "the overgrowth (Respawn Seconds 0) never comes back");
            Assert.AreEqual(5, Regrowth.HomeDays(bush), "a node's own days come first");
        }
        finally
        {
            Object.DestroyImmediate(stone);
            Object.DestroyImmediate(tree);
            Object.DestroyImmediate(bramble);
            Object.DestroyImmediate(bush);
        }
    }

    [Test]
    public void EveryMeal_LastsHalfAnHour_AndHoldsHungerTenMinutes()
    {
        List<ItemDefinition> meals = AssetDatabase.FindAssets("t:ItemDefinition")
            .Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(item => item != null && item.IsMeal).ToList();
        Assert.Greater(meals.Count, 10, "the meals were found");
        foreach (ItemDefinition meal in meals)
        {
            Assert.GreaterOrEqual(meal.BoonSeconds, 1800f, $"{meal.name}'s boon lasts at least 30 minutes");
        }

        Assert.GreaterOrEqual(SurvivalSettings.Current.MealHoldsHunger, 600f, "a meal holds hunger at least 10 minutes");
    }

    [Test]
    public void AMealsHeldHunger_GoesWithItsBoon_ThroughThePortal()
    {
        var player = new GameObject("Boons");
        var home = new GameObject("Boons Home");
        try
        {
            var boons = player.AddComponent<PlayerBoons>();
            var bonus = new List<StatBonus> { new StatBonus(StatType.MaxHealth, 0.1f, StatModifierOp.Percent) };
            Assert.IsTrue(boons.Add("stew", "Stew", bonus, 1800f, true, holdsHunger: 600f));
            Assert.AreEqual(600f, boons.HungerHeldLeft, 0.01f);
            List<BoonRecord> carried = boons.Capture();

            var other = home.AddComponent<PlayerBoons>();
            other.Restore(carried);
            Assert.AreEqual(600f, other.HungerHeldLeft, 0.01f, "the hold is carried with the boon (PlayerSnapshot)");
            Assert.IsTrue(other.Add("tea", "Tea", bonus, 60f, true, holdsHunger: 600f));
            Assert.AreEqual(60f, other.HungerHeldLeft, 0.01f, "never longer than the boon itself, and the new meal took the family's place");
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(home);
        }
    }

    [Test]
    public void AnEffect_SaysWhatGaveIt()
    {
        var stew = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Food/HuntersStew.asset");
        var draught = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Food/BarkskinDraught.asset");
        Assert.AreEqual($"Eaten: {stew.DisplayName}", ActiveEffects.SourceOf(new BoonRecord(stew.Id, stew.DisplayName, true, 10f, null), stew));
        Assert.AreEqual($"Drunk: {draught.DisplayName}",
            ActiveEffects.SourceOf(new BoonRecord(draught.Id, draught.DisplayName, false, 10f, null, isTonic: true), draught));
        StringAssert.Contains("rest", ActiveEffects.SourceOf(new BoonRecord(HavenReadiness.RestedId, "Well Rested", false, 10f, null), null));
        StringAssert.Contains("cold", ActiveEffects.SourceOf(new BoonRecord(PlayerWarmth.ChillId, "Chilled", false, 10f, null), null));
    }

    [Test]
    public void TheRaidCount_GoesOnPastWhatTheJournalRemembers_AndIsSaved()
    {
        var journal = new JournalRecord();
        for (int i = 0; i < JournalRecord.MostRaids + 5; i++)
        {
            journal.AddRaid(new RaidRecord { Raid = "Misty Woods", Outcome = "Extracted" });
        }

        Assert.AreEqual(JournalRecord.MostRaids, journal.Raids.Count, "the journal keeps the last ones");
        Assert.AreEqual(JournalRecord.MostRaids + 5, journal.RaidsEver, "the count a node waits on goes on");

        var progress = new CharacterProgress();
        for (int i = 0; i < JournalRecord.MostRaids + 5; i++)
        {
            progress.Journal.AddRaid(new RaidRecord { Raid = "Misty Woods", Outcome = "Extracted" });
        }

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, System.DateTime.UtcNow, null, new QuestLog(), new SettlementState(), null, progress);
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        CharacterProgress loaded = SaveMapper.FromSaveData(data, content).Progress;
        Assert.AreEqual(JournalRecord.MostRaids + 5, loaded.Journal.RaidsEver, "saved and loaded");

        data.progress.journal.raidsEver = 0;
        CharacterProgress old = SaveMapper.FromSaveData(data, content).Progress;
        Assert.AreEqual(JournalRecord.MostRaids, old.Journal.RaidsEver, "a save from before counts the raids it remembers");
    }

    [Test]
    public void ATusseWithYourThings_IsThirtyPercentSlower()
    {
        var tusse = AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/Tusse.asset");
        Assert.IsTrue(tusse.Thief);
        Assert.AreEqual(0.7f, tusse.LadenSpeed, 0.001f);
    }
}
