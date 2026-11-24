using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The altar's choices for abilities (Docs/SkillsImprovementResearch_2026-11-24.md 3, 4, 12): each ability that has them offers three at
/// rank I, three at rank II and two at rank III, never has two options using up the same status (they would fight over it), and a choice only counts once the
/// ability holds its rank. Choices and casts survive a save; a rank is free once practised.
/// </summary>
public sealed class SkillChoiceTests
{
    private CharacterProgress saved;

    [SetUp]
    public void Keep() => saved = GameSession.Progress;

    [TearDown]
    public void PutBack() => GameSession.UseProgressForTests(saved);

    private static SkillDefinition Skill(string name) => AssetDatabase.LoadAssetAtPath<SkillDefinition>($"Assets/_Project/Data/Skills/{name}.asset");

    [Test]
    public void EachAbilityWithChoices_OffersThreeThreeAndTwo_AndNoTwoUseUpOneStatus()
    {
        int withChoices = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { "Assets/_Project/Data/Skills" }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill.Options.Count == 0)
            {
                continue;
            }

            withChoices++;
            Assert.AreEqual(3, skill.Options.Count(o => o.rank == 1), skill.name);
            Assert.AreEqual(3, skill.Options.Count(o => o.rank == 2), skill.name);
            Assert.AreEqual(2, skill.Options.Count(o => o.rank == 3), skill.name);
            var consumed = skill.Options.Where(o => o.consumes != null).Select(o => o.consumes).ToList();
            Assert.AreEqual(consumed.Count, consumed.Distinct().Count(), $"{skill.name}: two options using up one status would fight over it");
            Assert.AreEqual(skill.Options.Count, skill.Options.Select(o => o.id).Distinct().Count(), $"{skill.name}: ids are saved, so each is its own");
            foreach (SkillOption option in skill.Options)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(option.description), $"{skill.name}/{option.id} says what it does");
            }
        }

        Assert.GreaterOrEqual(withChoices, 3, "Frost Rune, Wolf's Lunge and War Cry to start");
    }

    [Test]
    public void AChoiceCounts_OnlyOnceTheAbilityHoldsItsRank_AndSurvivesASave()
    {
        var progress = new CharacterProgress();
        GameSession.UseProgressForTests(progress);
        SkillDefinition rune = Skill("FrostRune");
        float plain = PlayerSkills.CooldownOf(rune);

        progress.Choose(rune, 1, "frost_quick");
        Assert.AreEqual(plain, PlayerSkills.CooldownOf(rune), 1e-4f, "chosen, but the rune isn't deepened yet");

        progress.DeepenSkill(rune);
        float deepened = rune.Cooldown * (1f - EssenceSettings.Current.CooldownCutAt(1));
        Assert.AreEqual(deepened * 0.8f, PlayerSkills.CooldownOf(rune), 1e-3f, "Quick Rune: a fifth off");

        progress.Choose(rune, 1, "frost_keen");
        Assert.AreEqual(deepened, PlayerSkills.CooldownOf(rune), 1e-3f, "changed to Keen Ice: the cooldown is back");
        Assert.AreEqual(1.2f, SkillChoices.For(rune).Damage, 1e-4f);

        progress.CountUse(rune);
        progress.CountUse(rune);
        // Through the save itself (SaveMapper to JSON and back), so a mapping that drops them fails here.
        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, System.DateTime.UtcNow, null, new QuestLog(),
            new SettlementState(), null, progress);
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        CharacterProgress copy = SaveMapper.FromSaveData(data, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName)).Progress;
        Assert.AreEqual("frost_keen", copy.ChoiceOf(rune, 1));
        Assert.AreEqual(2, copy.UsesOf(rune));
    }

    [Test]
    public void APractisedAbility_DeepensWithoutEssence()
    {
        var progress = new CharacterProgress();
        GameSession.UseProgressForTests(progress);
        SkillDefinition rune = Skill("FrostRune");
        int needed = EssenceSettings.Current.SkillRanks[0].practisedUses;
        Assert.Greater(needed, 0, "rank I can be earned by use");

        var player = new GameObject("A test's player");
        try
        {
            var skills = player.AddComponent<PlayerSkills>();
            skills.Learn(rune, false);
            AltarOffer RankOne() => AltarWork.Deepening(player).First(s => s.Title == rune.DisplayName).Offers.First(o => o.Title == "Rank I");
            Assert.IsTrue(RankOne().Cost.Resources.Count > 0, "unpractised: it costs essence");
            for (int i = 0; i < needed; i++)
            {
                progress.CountUse(rune);
            }

            Assert.AreEqual(0, RankOne().Cost.Resources.Count, "practised: no essence");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }
}
