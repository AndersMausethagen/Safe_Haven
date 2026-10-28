using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ProgressionTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    [Test]
    public void Experience_LevelsUpAndCarriesTheRestOver_UpToTheLastLevel()
    {
        LevelingSettings settings = Settings(5);
        var progress = new CharacterProgress();
        var reached = new List<int>();
        progress.LeveledUp += reached.Add;

        Assert.AreEqual(100, settings.ExperienceToNext(1));
        Assert.AreEqual(1, progress.AddExperience(130, settings));
        Assert.AreEqual(2, progress.Level);
        Assert.AreEqual(30, progress.Experience);

        Assert.AreEqual(3, progress.AddExperience(10000, settings), "levels stop at the last one");
        Assert.AreEqual(5, progress.Level);
        Assert.AreEqual(0, progress.Experience);
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5 }, reached);
        Assert.AreEqual(0, progress.AddExperience(50, settings));
    }

    [Test]
    public void Talents_CostPointsAndOpenRowsByPointsInTheirTree()
    {
        TalentDefinition first = Talent("first", TalentTree.Ranger, 0, 3);
        TalentDefinition second = Talent("second", TalentTree.Ranger, 1, 1);
        TalentDefinition other = Talent("other", TalentTree.Soldier, 0, 3);
        LevelingSettings settings = Settings(10, first, second, other);
        var progress = new CharacterProgress();

        Assert.IsFalse(progress.CanRaise(first, settings, out string reason));
        StringAssert.Contains("No talent points", reason);

        progress.AddExperience(10000, settings);
        Assert.AreEqual(9, progress.UnspentPoints(settings));
        Assert.IsFalse(progress.CanRaise(second, settings, out reason), "row 1 needs 2 points in the tree");
        Assert.IsTrue(progress.Raise(other, settings));
        Assert.IsFalse(progress.CanRaise(second, settings, out _), "points in another tree don't open it");
        Assert.IsTrue(progress.Raise(first, settings));
        Assert.IsTrue(progress.Raise(first, settings));
        Assert.IsTrue(progress.Raise(second, settings));
        Assert.IsFalse(progress.CanRaise(second, settings, out reason));
        StringAssert.Contains("highest rank", reason);
        Assert.AreEqual(3, progress.SpentIn(TalentTree.Ranger, settings));
        Assert.AreEqual(5, progress.UnspentPoints(settings));

        progress.ResetTalents();
        Assert.AreEqual(9, progress.UnspentPoints(settings));
        Assert.AreEqual(0, progress.RankOf(first));
    }

    [Test]
    public void Talents_BecomeStatModifiersOnThePlayer()
    {
        var player = new GameObject("Talented player");
        created.Add(player);
        var combatant = player.AddComponent<Combatant>();
        var talents = player.AddComponent<PlayerTalents>();
        TalentDefinition feet = LevelingSettings.Current.FindTalent("light_feet");
        Assert.NotNull(feet, "the authored settings hold the talents");
        GameSession.Progress.ResetTalents();
        GameSession.Progress.AddExperience(1000, LevelingSettings.Current);
        try
        {
            Assert.IsTrue(GameSession.Progress.Raise(feet, LevelingSettings.Current));
            Assert.IsTrue(GameSession.Progress.Raise(feet, LevelingSettings.Current));
            typeof(PlayerTalents).GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(talents, null);
            typeof(PlayerTalents).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(talents, null);
            Assert.AreEqual(feet.PerRank[0].value * 2, combatant.Stats.Get(StatType.MoveSpeed), 0.0001f);
            GameSession.Progress.ResetTalents();
            Assert.AreEqual(0f, combatant.Stats.Get(StatType.MoveSpeed), 0.0001f, "reset takes them off again");
        }
        finally
        {
            typeof(PlayerTalents).GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(talents, null);
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void Progress_IsSavedAndLoaded()
    {
        LevelingSettings settings = LevelingSettings.Current;
        TalentDefinition talent = settings.Talents.First();
        var progress = new CharacterProgress();
        progress.AddExperience(400, settings);
        progress.Raise(talent, settings);
        progress.MarkCrafted("StewedBerries");
        progress.MarkStoneUsed("Raid:1,2,3");

        SaveData data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, progress: progress);
        string json = JsonUtility.ToJson(data);
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        LoadedGame loaded = SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(json), content);

        Assert.AreEqual(progress.Level, loaded.Progress.Level);
        Assert.AreEqual(progress.Experience, loaded.Progress.Experience);
        Assert.AreEqual(1, loaded.Progress.RankOf(talent));
        Assert.IsFalse(loaded.Progress.MarkCrafted("StewedBerries"), "first crafts are remembered");
        Assert.IsTrue(loaded.Progress.HasUsedStone("Raid:1,2,3"));
    }

    [Test]
    public void AuthoredTalents_FillFourTreesThatCanBeCompleted()
    {
        LevelingSettings settings = LevelingSettings.Current;
        Assert.AreEqual(20, settings.Talents.Count);
        Assert.AreEqual(settings.Talents.Count, settings.Talents.Select(t => t.Id).Distinct().Count(), "ids are unique");
        foreach (TalentTree tree in Enum.GetValues(typeof(TalentTree)))
        {
            TalentDefinition[] inTree = settings.Talents.Where(t => t.Tree == tree).ToArray();
            Assert.AreEqual(5, inTree.Length, tree.ToString());
            Assert.IsTrue(inTree.All(t => t.PerRank.Count > 0 && !string.IsNullOrWhiteSpace(t.Description)), tree.ToString());
            // Every row is reachable with the ranks below it.
            foreach (TalentDefinition talent in inTree)
            {
                int below = inTree.Where(t => t.Row < talent.Row).Sum(t => t.MaxRank);
                Assert.GreaterOrEqual(below, talent.Row * settings.PointsPerRow, talent.name);
            }
        }

        int allRanks = settings.Talents.Sum(t => t.MaxRank);
        Assert.Less((settings.MaxLevel - 1) * settings.TalentPointsPerLevel, allRanks, "the last level still leaves choices");
        Assert.NotNull(settings.LevelUpSound);
    }

    [Test]
    public void RuneBook_IsReadForExperienceAndFoundInSupplyCaches()
    {
        var book = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/RuneBook.asset");
        Assert.Greater(book.Experience, 0);
        Assert.IsTrue(book.IsUsable);
        Assert.IsFalse(book.IsBlueprint);
        var cache = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/SupplyCache.prefab").GetComponentInChildren<LootCache>(true);
        string drops = new SerializedObject(cache).FindProperty("drops").FindPropertyRelative("entries").arraySize > 0
            ? EditorJsonUtility.ToJson(cache)
            : "";
        StringAssert.Contains(AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Items/RuneBook.asset"), drops);
    }

    private LevelingSettings Settings(int maxLevel, params TalentDefinition[] talents)
    {
        var settings = ScriptableObject.CreateInstance<LevelingSettings>();
        created.Add(settings);
        settings.Configure(maxLevel, 100, 1.2f, 1, 2, talents);
        return settings;
    }

    private TalentDefinition Talent(string id, TalentTree tree, int row, int ranks)
    {
        var talent = ScriptableObject.CreateInstance<TalentDefinition>();
        created.Add(talent);
        talent.Configure(id, id, id, tree, row, ranks, new StatBonus(StatType.MaxHealth, 5f));
        return talent;
    }
}
