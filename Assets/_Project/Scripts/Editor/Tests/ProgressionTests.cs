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
    public void Experience_RaisesTheSkillsLevel_UpToTheLastLevel()
    {
        ProficiencyDefinition skill = Skill("chopping");
        LevelingSettings settings = Settings(5, 3, skill);
        var progress = new CharacterProgress();
        var reached = new List<int>();
        progress.LeveledUp += (leveled, level) => reached.Add(level);

        Assert.AreEqual(50, settings.ExperienceToNext(1));
        Assert.AreEqual(1, progress.AddExperience(skill, 65, settings));
        Assert.AreEqual(2, progress.LevelOf(skill, settings));
        Assert.AreEqual(15, progress.TowardsNext(skill, settings, out int needed));
        Assert.AreEqual(60, needed);

        Assert.AreEqual(3, progress.AddExperience(skill, 100000, settings), "levels stop at the last one");
        Assert.AreEqual(5, progress.LevelOf(skill, settings));
        Assert.AreEqual(settings.TotalExperienceFor(5), progress.ExperienceOf(skill), "experience stops there too");
        CollectionAssert.AreEqual(new[] { 2, 3, 4, 5 }, reached);
        Assert.AreEqual(0, progress.AddExperience(skill, 50, settings));
        Assert.AreEqual(5, progress.TotalLevel(settings));
    }

    [Test]
    public void LevelBonuses_PenaliseBeginners_AreNothingAtTheBaseline_AndGrowToTheTop()
    {
        LevelingSettings settings = Settings(50, 10);
        var bonus = new ProficiencyDefinition.LevelBonus(StatType.ChopSweetSpot, -0.3f, 0.4f);

        Assert.AreEqual(-0.3f, ProficiencyDefinition.ValueAt(bonus, 1, settings), 0.0001f, "a beginner");
        Assert.AreEqual(0f, ProficiencyDefinition.ValueAt(bonus, 10, settings), 0.0001f, "the player the game was tuned for");
        Assert.AreEqual(0.4f, ProficiencyDefinition.ValueAt(bonus, 50, settings), 0.0001f, "a master");
        Assert.AreEqual(-0.3f * 5f / 9f, ProficiencyDefinition.ValueAt(bonus, 5, settings), 0.0001f, "evenly in between");
        Assert.AreEqual(0.2f, ProficiencyDefinition.ValueAt(bonus, 30, settings), 0.0001f, "halfway up");
    }

    [Test]
    public void ActionExperience_FallsOffBelowTheSkillsBand_AndCountsMoreOnARaid()
    {
        LevelingSettings settings = LevelingSettings.Current;

        Assert.AreEqual(10, settings.ActionExperience(1, 1, false), "a tier I tree for a beginner");
        Assert.AreEqual(15, settings.ActionExperience(1, 1, true), "on a raid");
        Assert.AreEqual(6, settings.ActionExperience(1, 15, false), "one tier below the band");
        Assert.AreEqual(4, settings.ActionExperience(2, 35, false), "far below: farming easy trees stops paying");
        Assert.AreEqual(25, settings.ActionExperience(3, 25, false), "the band's own tier");
        Assert.AreEqual(25, settings.ActionExperience(3, 1, false), "harder than the band pays in full");
        Assert.AreEqual(8, settings.ActionExperience(3, 25, false, 3), "measured in tier I experience, grown with the tier");
    }

    [Test]
    public void Perks_OneChoicePerMilestone_ChangedOnlyAtHome_AndChosenAgainAfterAReset()
    {
        PerkDefinition first = Perk("first", StatType.MaxHealth);
        PerkDefinition second = Perk("second", StatType.Armor);
        ProficiencyDefinition skill = Skill("melee", new ProficiencyDefinition.Milestone(3, first, second));
        LevelingSettings settings = Settings(5, 2, skill);
        var progress = new CharacterProgress();

        Assert.IsFalse(progress.CanChoose(skill, first, settings, false, out string reason));
        StringAssert.Contains("Needs", reason);
        Assert.AreEqual(0, progress.PendingChoices(settings));

        progress.AddExperience(skill, settings.TotalExperienceFor(3), settings);
        Assert.AreEqual(1, progress.PendingChoices(settings), "a milestone reached waits for a choice");
        Assert.IsTrue(progress.Choose(skill, first, settings, false), "the first choice can be made anywhere");
        Assert.AreEqual(0, progress.PendingChoices(settings));
        Assert.IsFalse(progress.Choose(skill, second, settings, false), "changing it needs home");
        Assert.IsTrue(progress.Choose(skill, second, settings, true));
        Assert.IsFalse(progress.HasPerk(first), "the other is given up");
        Assert.AreSame(second, progress.ChosenAt(skill.Milestones[0]));

        progress.ResetPerks();
        Assert.AreEqual(1, progress.PendingChoices(settings));
    }

    [Test]
    public void Insight_GoesIntoTheSkillTheyChoose_NeverPastTheTop()
    {
        ProficiencyDefinition skill = Skill("sneaking");
        LevelingSettings settings = Settings(3, 2, skill);
        var progress = new CharacterProgress();
        progress.AddInsight(1000);

        Assert.AreEqual(100, progress.SpendInsight(skill, 100, settings));
        Assert.AreEqual(900, progress.Insight);
        Assert.AreEqual(2, progress.LevelOf(skill, settings));
        int room = settings.TotalExperienceFor(3) - progress.ExperienceOf(skill);
        Assert.AreEqual(room, progress.SpendInsight(skill, int.MaxValue, settings), "only what the skill still has room for");
        Assert.AreEqual(900 - room, progress.Insight, "the rest stays to spend elsewhere");
    }

    [Test]
    public void Practice_TrainsTheSkill_AndAHarvestTrainsTheSkillOfItsTool()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition woodcutting = settings.SkillFor(ToolType.Axe);
        Assert.NotNull(woodcutting, "the axe trains a skill");
        GameSession.StartNewGame();
        try
        {
            ExperienceTracker.Practise(woodcutting, 1, settings.TierExperience(1));
            Assert.AreEqual(10, GameSession.Progress.ExperienceOf(woodcutting));
            Assert.AreEqual(0, GameSession.Progress.Insight);

            ExperienceTracker.Practise(ProficiencyTraining.Archery, 1, 10f);
            Assert.AreEqual(10, GameSession.Progress.ExperienceOf(settings.SkillTrainedBy(ProficiencyTraining.Archery)), "a kind of practice trains its skill");
            Assert.AreEqual(0, GameSession.Progress.Insight);
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void Skills_BecomeStatModifiersOnThePlayer()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition melee = settings.SkillTrainedBy(ProficiencyTraining.Melee);
        Assert.NotNull(melee, "the authored settings hold a melee skill");
        var player = new GameObject("Skilled player");
        created.Add(player);
        var combatant = player.AddComponent<Combatant>();
        var skills = player.AddComponent<PlayerProficiencies>();
        GameSession.StartNewGame();
        try
        {
            Call(skills, "Awake");
            Call(skills, "OnEnable");
            Assert.AreEqual(-0.12f, combatant.Stats.Get(StatType.MeleeDamage), 0.0001f, "a beginner hits softer");
            Assert.AreEqual(0.25f, combatant.Stats.Get(StatType.MeleeStaminaCost), 0.0001f, "and tires sooner");

            GameSession.Progress.AddExperience(melee, settings.TotalExperienceFor(settings.BaselineLevel), settings);
            Assert.AreEqual(0f, combatant.Stats.Get(StatType.MeleeDamage), 0.0001f, "at the baseline the player is as the game was tuned");

            PerkDefinition perk = melee.Milestones[0].second;
            StatType stat = perk.Bonuses[0].stat;
            float before = combatant.Stats.Get(stat);
            Assert.IsTrue(GameSession.Progress.Choose(melee, perk, settings, false));
            Assert.AreEqual(before + perk.Bonuses[0].value, combatant.Stats.Get(stat), 0.0001f, "a chosen perk counts");
            GameSession.Progress.ResetPerks();
            Assert.AreEqual(before, combatant.Stats.Get(stat), 0.0001f, "and comes off again");
        }
        finally
        {
            Call(skills, "OnDisable");
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void Progress_IsSavedAndLoaded()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition skill = settings.Skills.First();
        var progress = new CharacterProgress();
        progress.AddExperience(skill, settings.TotalExperienceFor(12), settings);
        progress.AddInsight(70);
        PerkDefinition perk = skill.Milestones[0].first;
        Assert.IsTrue(progress.Choose(skill, perk, settings, false));
        progress.MarkCrafted("StewedBerries");
        progress.MarkStoneUsed("Raid:1,2,3");

        LoadedGame loaded = RoundTrip(SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, progress: progress));

        Assert.AreEqual(progress.ExperienceOf(skill), loaded.Progress.ExperienceOf(skill));
        Assert.AreEqual(12, loaded.Progress.LevelOf(skill, settings));
        Assert.AreEqual(70, loaded.Progress.Insight);
        Assert.IsTrue(loaded.Progress.HasPerk(perk));
        Assert.IsFalse(loaded.Progress.MarkCrafted("StewedBerries"), "first crafts are remembered");
        Assert.IsTrue(loaded.Progress.HasUsedStone("Raid:1,2,3"));
    }

    [Test]
    public void ASaveFromBeforeSkills_TurnsItsCharacterExperienceIntoInsight()
    {
        // The old curve: 100, 120, 140, 165 to reach level 5, plus 30 towards level 6.
        Assert.AreEqual(555, SaveMapper.LegacyExperience(5, 30));
        Assert.AreEqual(0, SaveMapper.LegacyExperience(1, 0));

        SaveData data = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, progress: new CharacterProgress());
        data.progress.level = 5;
        data.progress.experience = 30;
        data.progress.talents.Add(new AmountSaveData { id = "light_feet", amount = 2 });
        LoadedGame loaded = RoundTrip(data);

        Assert.AreEqual(555, loaded.Progress.Insight);
        Assert.AreEqual(0, loaded.Progress.PerkCount, "talents are gone");
        SaveData again = SaveMapper.ToSaveData(null, null, "SafeHaven", null, DateTime.UtcNow, progress: loaded.Progress);
        Assert.AreEqual(1, again.progress.level, "saved again, it is a new save: no second helping of Insight");
        Assert.AreEqual(555, RoundTrip(again).Progress.Insight);
    }

    [Test]
    public void AuthoredSkills_HaveBeginnerPenalties_AndFiveMilestonesOfTwoPerksEach()
    {
        LevelingSettings settings = LevelingSettings.Current;
        string[] ids = settings.Skills.Select(s => s.Id).ToArray();
        CollectionAssert.AreEquivalent(new[]
        {
            "woodcutting", "mining", "foraging", "fishing", "weaponcrafting", "armorcrafting", "cooking", "alchemy", "construction",
            "melee", "archery", "runecraft", "sneaking",
        }, ids);
        Assert.AreSame(settings.FindSkill("foraging"), settings.SkillFor(ToolType.None), "picking by hand trains Foraging");
        Assert.AreSame(settings.FindSkill("foraging"), settings.SkillFor(ToolType.Sickle));
        Assert.AreSame(settings.FindSkill("fishing"), settings.SkillTrainedBy(ProficiencyTraining.Fishing));
        Assert.AreSame(settings.FindSkill("runecraft"), settings.SkillTrainedBy(ProficiencyTraining.Spells));
        Assert.AreEqual(ids.Length, ids.Distinct().Count(), "skill ids are unique");
        Assert.AreSame(settings.FindSkill("mining"), settings.SkillFor(ToolType.Pickaxe));
        Assert.AreSame(settings.FindSkill("sneaking"), settings.SkillTrainedBy(ProficiencyTraining.Sneaking));
        Assert.NotNull(settings.LevelUpSound);

        var perkIds = new List<string>();
        foreach (ProficiencyDefinition skill in settings.Skills)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(skill.Description) || string.IsNullOrWhiteSpace(skill.TrainedBy), skill.name);
            Assert.IsTrue(skill.LevelBonuses.Any(b => !Mathf.Approximately(b.atFirstLevel, 0f)), $"{skill.name}: a beginner is worse than the baseline");
            CollectionAssert.AreEqual(new[] { 10, 20, 30, 40, 50 }, skill.Milestones.Select(m => m.level).ToArray(), skill.name);
            foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
            {
                Assert.NotNull(milestone.first, skill.name);
                Assert.NotNull(milestone.second, skill.name);
                Assert.IsTrue(milestone.first.Bonuses.Count > 0 && milestone.second.Bonuses.Count > 0, skill.name);
                perkIds.Add(milestone.first.Id);
                perkIds.Add(milestone.second.Id);
            }
        }

        Assert.AreEqual(perkIds.Count, perkIds.Distinct().Count(), "perk ids are unique");
    }

    [Test]
    public void AuthoredCurve_TakesAboutAnHourAndAHalfToTheBaseline_AndThirtyToTheTop()
    {
        // Docs/Skills.md: about 180 actions an hour, a quarter of them for any one skill, at the tier of each band.
        LevelingSettings settings = LevelingSettings.Current;
        double hours = 0;
        var at = new Dictionary<int, double>();
        for (int level = 1; level < settings.MaxLevel; level++)
        {
            hours += settings.ExperienceToNext(level) / settings.TierExperience(settings.TierBand(level)) / (180.0 / 4.0);
            at[level + 1] = hours;
        }

        Assert.That(at[settings.BaselineLevel], Is.InRange(1.0, 2.0), "the baseline: the first raids");
        Assert.That(at[20], Is.InRange(3.5, 5.5));
        Assert.That(at[settings.MaxLevel], Is.InRange(25.0, 40.0), "slow, not grindy");
    }

    [Test]
    public void RuneBook_IsReadForInsightAndFoundInSupplyCaches()
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

    private static LoadedGame RoundTrip(SaveData data)
    {
        string json = JsonUtility.ToJson(data);
        var content = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        return SaveMapper.FromSaveData(JsonUtility.FromJson<SaveData>(json), content);
    }

    private static void Call(PlayerProficiencies skills, string method) =>
        typeof(PlayerProficiencies).GetMethod(method, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(skills, null);

    private LevelingSettings Settings(int maxLevel, int baseline, params ProficiencyDefinition[] skills)
    {
        var settings = ScriptableObject.CreateInstance<LevelingSettings>();
        created.Add(settings);
        settings.Configure(maxLevel, 50, 1.2f, baseline, skills);
        return settings;
    }

    private ProficiencyDefinition Skill(string id, params ProficiencyDefinition.Milestone[] milestones)
    {
        var skill = ScriptableObject.CreateInstance<ProficiencyDefinition>();
        created.Add(skill);
        skill.Configure(id, id, id, id, ProficiencyGroup.Gathering, ProficiencyTraining.Gathering, ToolType.None,
            new ProficiencyDefinition.LevelBonus[0], milestones);
        return skill;
    }

    private PerkDefinition Perk(string id, StatType stat)
    {
        var perk = ScriptableObject.CreateInstance<PerkDefinition>();
        created.Add(perk);
        perk.Configure(id, id, id, new StatBonus(stat, 5f));
        return perk;
    }
}
