using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The death rules (Docs/DeathPenaltyResearch_2026-11-22.md, the user's "as recommended, also on harsh, when a player dies in a raid,
/// everything gets lost. even equipped items"): lessons fade without a level lost, with a grace; Gentle costs nothing; Harsh takes
/// everything from a raid; a lost breach's plunder takes plain materials, bounded; the rules and the plunder are saved.
/// </summary>
public sealed class DeathRulesTests
{
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (UnityEngine.Object made in created)
        {
            UnityEngine.Object.DestroyImmediate(made);
        }

        created.Clear();
        GameSession.StartNewGame();
    }

    private static LevelingSettings Leveling => LevelingSettings.Current;

    [Test]
    public void ThePresets_RunFromNothingToEverything()
    {
        DeathSettings settings = DeathSettings.Current;
        Assert.AreEqual(DeathRules.Normal, settings.NewGameRules, "Normal is the default");
        DeathRuleSet gentle = settings.For(DeathRules.Gentle), normal = settings.For(DeathRules.Normal), harsh = settings.For(DeathRules.Harsh);
        Assert.IsFalse(gentle.cairn || gentle.lessonShare > 0f || gentle.boonsEnd || gentle.wardPerFall > 0f || gentle.plunderShare > 0f, "Gentle costs nothing");
        Assert.IsTrue(normal.cairn && !normal.cairnTakesGear && normal.boonsEnd);
        Assert.AreEqual(0.25f, normal.lessonShare, 1e-4f);
        Assert.AreEqual(0.1f, normal.wardPerFall, 1e-4f);
        Assert.AreEqual(0.2f, normal.wardFloor, 1e-4f);
        Assert.IsFalse(normal.raidTakesEverything, "the canary: Normal keeps worn gear from a raid");
        Assert.IsTrue(harsh.raidTakesEverything && harsh.cairnTakesGear, "Harsh takes everything");
        Assert.Greater(harsh.lessonShare, normal.lessonShare);
        Assert.IsNotNull(settings.CairnPrefab, "a cairn to leave");
        Assert.IsNotNull(Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).FindPrefab(settings.CairnPrefab.PrefabId), "and saved");
    }

    [Test]
    public void LessonsFade_NeverBelowTheLevel()
    {
        var progress = new CharacterProgress();
        ProficiencyDefinition skill = Leveling.Skills.First(s => s != null);
        progress.AddExperience(skill, Leveling.TotalExperienceFor(5) + 40, Leveling);
        int level = progress.LevelOf(skill, Leveling);
        Assert.AreEqual(40, progress.LoseExperience(skill, 25, Leveling) + 15, "the canary: some can go");
        Assert.AreEqual(15, progress.LoseExperience(skill, 1000, Leveling), "only down to the level's start");
        Assert.AreEqual(level, progress.LevelOf(skill, Leveling), "no level lost");
        Assert.AreEqual(0, progress.LoseExperience(skill, 10, Leveling));
    }

    [Test]
    public void AFallAtHome_CostsAQuarterOfTheNextLevel_ThenTheGraceSparesTheNext()
    {
        GameSession.StartNewGame();
        CharacterProgress progress = GameSession.Progress;
        progress.DeathRules = DeathRules.Normal;
        ProficiencyDefinition skill = Leveling.Skills.First(s => s != null);
        int level = 8;
        int needed = Leveling.ExperienceToNext(level);
        progress.AddExperience(skill, Leveling.TotalExperienceFor(level) + needed - 1, Leveling);
        var player = new GameObject("Player");
        created.Add(player);

        HomeFalls.Cost first = HomeFalls.Take(player, Vector3.zero);
        Assert.IsFalse(first.Spared);
        Assert.AreEqual(Mathf.RoundToInt(needed * 0.25f), first.Lessons[skill], "a quarter of the next level");
        Assert.Greater(progress.FallGraceLeft, 590f, "the grace begins");

        HomeFalls.Cost second = HomeFalls.Take(player, Vector3.zero);
        Assert.IsTrue(second.InGrace);
        Assert.AreEqual(0, second.Lessons.Count, "nothing more so soon");
        Assert.IsNotNull(HomeFalls.Card(first), "the card says what it cost");

        progress.DeathRules = DeathRules.Gentle;
        Assert.IsTrue(HomeFalls.Take(player, Vector3.zero).Spared, "Gentle costs nothing");
    }

    [Test]
    public void AHarshRaidDeath_KeepsNothing_ANormalOneKeepsTheGear()
    {
        ItemDefinition sword = AssetDatabase.FindAssets("t:ItemDefinition").Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .First(i => i != null && i.HasDurability);
        var bag = new ItemContainer(4, 1);
        bag.SetSlot(0, new ItemStack(sword, 1));
        var pocket = new ItemContainer(1, 1);
        pocket.SetSlot(0, new ItemStack(sword, 1));
        PlayerSnapshot carried = PlayerSnapshot.Capture(bag, null, null, pocket);
        PlayerSnapshot normal = carried.AfterFailedRaid(carried, 0.25f);
        PlayerSnapshot harsh = carried.AfterFailedRaid(carried, 0.25f, null, true);
        Assert.Greater(normal.CountItems().Values.Sum(), 0, "the canary: Normal brings the sword home, worn");
        Assert.AreEqual(0, harsh.CountItems().Values.Sum(), "Harsh: nothing comes home, not even the safe pocket");
    }

    [Test]
    public void APlunder_TakesAShareOfTheMostValuableMaterials_Bounded()
    {
        var materials = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Data/Items/Materials" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(Plunder.Takeable).OrderByDescending(i => i.Value).Take(4).ToList();
        Assume.That(materials.Count == 4);
        var stash = new ItemContainer(200, 1);
        foreach (ItemDefinition item in materials)
        {
            Assume.That(stash.Add(item, 400) == 400, item.name);
        }

        List<ItemAmount> taken = Plunder.Take(stash, 0.2f, 3, 60);
        Assert.AreEqual(3, taken.Count, "three stacks");
        Assert.IsTrue(taken.All(t => t.Amount == 60), "a fifth of 400 is 80: at most 60");
        Assert.IsFalse(taken.Any(t => t.Item == materials[3]), "the least valuable is left");
        Assert.AreEqual(340, stash.Count(taken[0].Item), "taken from the stash");
        Assert.AreEqual(2, Plunder.Take(new ItemContainer(4, 1), 0.2f, 3, 60).Count + 2, "the canary: an empty stash gives nothing");
        Assert.IsEmpty(Plunder.Take(stash, 0f, 3, 60), "Gentle takes nothing");
    }

    [Test]
    public void TheRulesAndThePlunder_AreSaved_AndOldSavesAreNormal()
    {
        Assert.AreEqual((int)DeathRules.Normal, JsonUtility.FromJson<ProgressSaveData>("{}").deathRules, "a save from before is Normal");
        var progress = new CharacterProgress { DeathRules = DeathRules.Harsh };
        progress.StartFallGrace(300f);
        var settlement = new SettlementState();
        ItemDefinition item = AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Data/Items/Materials" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).First(Plunder.Takeable);
        var plunder = new PlunderRecord { Leader = "stone_troll" };
        plunder.Items.Add(new ItemAmount(item, 12));
        settlement.SetPlunder(plunder);

        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, DateTime.UtcNow, null, new QuestLog(), settlement, null, progress);
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        LoadedGame loaded = SaveMapper.FromSaveData(data, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName));
        Assert.AreEqual(DeathRules.Harsh, loaded.Progress.DeathRules);
        Assert.AreEqual(300f, loaded.Progress.FallGraceLeft, 2f, "the grace carries on");
        Assert.IsNotNull(loaded.Settlement.Plunder);
        Assert.AreEqual("stone_troll", loaded.Settlement.Plunder.Leader);
        Assert.AreEqual(12, loaded.Settlement.Plunder.Items[0].Amount);
    }
}
