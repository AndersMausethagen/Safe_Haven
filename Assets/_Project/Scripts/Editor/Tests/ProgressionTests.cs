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
    public void TheFirstLevels_AreSlowerThanTheCurve_ButNeverCheaperThanTheOneBefore()
    {
        LevelingSettings authored = LevelingSettings.Current;
        Assert.Greater(authored.ExperienceToNext(1), 50, "a little slower at the start: the level sounds piled up");
        int previous = 0;
        for (int level = 1; level < 50; level++)
        {
            int needed = authored.ExperienceToNext(level);
            Assert.GreaterOrEqual(needed, previous, $"level {level}");
            previous = needed;
        }
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
        Assert.AreEqual(-0.3f * Mathf.Pow(5f / 9f, 2.5f), ProficiencyDefinition.ValueAt(bonus, 5, settings), 0.0001f,
            "a beginner's penalties fade fast: three quarters gone by level 5");
        Assert.AreEqual(0.2f, ProficiencyDefinition.ValueAt(bonus, 30, settings), 0.0001f, "halfway up");
    }

    [Test]
    public void ActionExperience_FallsOffBelowTheSkillsBand_AndCountsMoreOnARaid()
    {
        // The shape of the shipped curve, not its numbers (those are LevelingSettings' to tune).
        LevelingSettings settings = LevelingSettings.Current;
        int atBand = settings.ActionExperience(1, 1, false);
        Assert.Greater(atBand, 0, "a tier I tree pays a beginner");
        Assert.Greater(settings.ActionExperience(1, 1, true), atBand, "more on a raid");
        int oneBelow = settings.ActionExperience(1, 15, false);
        Assert.Less(oneBelow, atBand, "one tier below the band pays less");
        Assert.LessOrEqual(settings.ActionExperience(1, 35, false), oneBelow, "far below: farming easy trees stops paying");
        Assert.Greater(settings.ActionExperience(3, 25, false), atBand, "a higher tier pays more at its own band");
        Assert.AreEqual(settings.ActionExperience(3, 25, false), settings.ActionExperience(3, 1, false), "harder than the band pays in full");
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
    public void Insight_CountsHalfFromLevelTwenty_SoPracticeLeads()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition skill = settings.Skills.First();
        var progress = new CharacterProgress();
        progress.AddExperience(skill, settings.TotalExperienceFor(settings.InsightFullBelow), settings);
        progress.AddInsight(400);

        int spent = progress.SpendInsight(skill, 400, settings);
        Assert.AreEqual(400, spent);
        Assert.AreEqual(settings.TotalExperienceFor(settings.InsightFullBelow) + 200, progress.ExperienceOf(skill), 1, "half its worth");

        var fresh = new CharacterProgress();
        fresh.AddInsight(100);
        fresh.SpendInsight(skill, 100, settings);
        Assert.AreEqual(100, fresh.ExperienceOf(skill), "in full for a neglected skill");
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
            // Practice Time Scale stretches every level (1.3 since 2026-11-14): what is practised counts for that much less.
            ExperienceTracker.Practise(woodcutting, 1, settings.TierExperience(1));
            Assert.AreEqual(Mathf.FloorToInt(10f / settings.PracticeTimeScale), GameSession.Progress.ExperienceOf(woodcutting));
            Assert.AreEqual(0, GameSession.Progress.Insight);

            ExperienceTracker.Practise(ProficiencyTraining.Archery, 1, 10f);
            Assert.AreEqual(Mathf.FloorToInt(10f / settings.PracticeTimeScale), GameSession.Progress.ExperienceOf(settings.SkillTrainedBy(ProficiencyTraining.Archery)), "a kind of practice trains its skill");
            Assert.AreEqual(0, GameSession.Progress.Insight);
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void AMentorBuilding_AndRest_SpeedPractice()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition smithing = settings.FindSkill("weaponcrafting");
        Assert.NotNull(smithing.Mentor, "the smithy teaches weaponcrafting");
        GameSession.StartNewGame();
        try
        {
            Assert.AreEqual(1f, ExperienceTracker.Boost(smithing), 0.0001f, "no smithy, no lessons");
            GameSession.Home.NoteBuilt(smithing.Mentor, 3);
            Assert.AreEqual(1.1f, ExperienceTracker.Boost(smithing), 0.0001f, "two tiers above I: +10%");
            ExperienceTracker.Practise(smithing, 1, 10f, false);
            Assert.AreEqual(Mathf.FloorToInt(11f / settings.PracticeTimeScale), GameSession.Progress.ExperienceOf(smithing));
        }
        finally
        {
            GameSession.StartNewGame();
        }

        Assert.IsTrue(HavenSettings.Current.RestedBonuses.Any(b => b.stat == StatType.ExperienceGain && b.value > 0f),
            "setting out rested makes practice count for more");
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
            Assert.Less(combatant.Stats.Get(StatType.MeleeDamage), 0f, "a beginner hits softer");
            Assert.Greater(combatant.Stats.Get(StatType.MeleeStaminaCost), 0f, "and tires sooner");

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
        Assert.IsNotEmpty(ids);
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
                Assert.IsTrue((milestone.first.Bonuses.Count > 0 || milestone.first.IsTechnique) && (milestone.second.Bonuses.Count > 0 || milestone.second.IsTechnique), skill.name);
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
    public void ThePlaytestLog_MeasuresPace_FromLevelsReachedByPractice()
    {
        var entries = new List<PlaytestEntry>
        {
            new PlaytestEntry { PlayedSeconds = 300f, Event = PlaytestLog.SkillLevel, Subject = "melee", Amount = 2 },
            new PlaytestEntry { PlayedSeconds = 600f, Event = PlaytestLog.SkillLevel, Subject = "melee", Amount = 3, Detail = "insight" },
            new PlaytestEntry { PlayedSeconds = 900f, Event = PlaytestLog.SkillLevel, Subject = "melee", Amount = 4, Detail = "milestone" },
            new PlaytestEntry { PlayedSeconds = 960f, Event = PlaytestLog.SkillLevel, Subject = "melee", Amount = 2 },
        };

        SortedDictionary<int, float> melee = PlaytestLog.SkillPace(entries)["melee"];
        Assert.AreEqual(5f, melee[2], 0.001f, "the first time counts");
        Assert.IsFalse(melee.ContainsKey(3), "a level Insight bought says nothing about pace");
        Assert.AreEqual(15f, melee[4], 0.001f);
    }

    [Test]
    public void TheTotalLevel_GivesATitle_AndRenown()
    {
        LevelingSettings settings = LevelingSettings.Current;
        int start = settings.Skills.Count;
        Assert.AreEqual("Newcomer", settings.TitleFor(start));
        Assert.AreEqual(0, settings.RenownFor(start), "a newcomer adds nothing");
        Assert.AreEqual(2, settings.RenownFor(start + 40));
        Assert.AreNotEqual(settings.TitleFor(start), settings.TitleFor(settings.Skills.Count * settings.MaxLevel), "a master is called something else");
    }

    [Test]
    public void WatchingAFriend_TeachesAShare_ThatIsNotPassedOn()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition skill = settings.FindSkill("fishing");
        var practised = new List<float>();
        void Note(ProficiencyDefinition s, float amount) => practised.Add(amount);
        ExperienceTracker.Practised += Note;
        GameSession.StartNewGame();
        try
        {
            ExperienceTracker.Practise(skill, 1, 20f);
            float done = 20f / settings.PracticeTimeScale;
            Assert.AreEqual(1, practised.Count, "practice is announced for friends to watch");
            Assert.AreEqual(done, practised[0], 0.001f);
            ExperienceTracker.LearnByWatching(skill, done * settings.WatchShare);
            // Each is paid in whole points (no tracker in an edit-mode test keeps the fractions).
            Assert.AreEqual(Mathf.FloorToInt(done) + Mathf.FloorToInt(done * settings.WatchShare), GameSession.Progress.ExperienceOf(skill));
            Assert.AreEqual(1, practised.Count, "what was watched is not announced again, so it can't echo");
        }
        finally
        {
            ExperienceTracker.Practised -= Note;
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void ThePlaytestLog_MeasuresExperiencePerPlayedHour()
    {
        var entries = new List<PlaytestEntry>
        {
            new PlaytestEntry { PlayedSeconds = 60f, Event = PlaytestLog.SkillXp, Subject = "mining", Amount = 100 },
            new PlaytestEntry { PlayedSeconds = 120f, Event = PlaytestLog.SkillXp, Subject = "mining", Amount = 50 },
            new PlaytestEntry { PlayedSeconds = 1800f, Event = PlaytestLog.SessionEnd },
        };

        Dictionary<string, float> rate = PlaytestLog.SkillRate(entries);
        Assert.AreEqual(300f, rate["mining"], 0.01f, "150 in half an hour");
        Assert.IsFalse(rate.ContainsKey("melee"));
    }

    [Test]
    public void RuneBook_IsReadForInsightAndFoundInSupplyCaches()
    {
        var book = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/RuneBook.asset");
        Assert.Greater(book.Experience, 0);
        Assert.IsTrue(book.IsUsable);
        Assert.IsFalse(book.IsBlueprint);
        bool inACache = AssetDatabase.FindAssets("t:LootTable", new[] { "Assets/_Project/Data/Loot/Caches" })
            .Select(guid => AssetDatabase.LoadAssetAtPath<LootTable>(AssetDatabase.GUIDToAssetPath(guid)))
            .Any(table => table.Drops.Entries.Any(entry => entry.Item == book));
        Assert.IsTrue(inACache, "a kind of supply cache holds Rune Books");
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
