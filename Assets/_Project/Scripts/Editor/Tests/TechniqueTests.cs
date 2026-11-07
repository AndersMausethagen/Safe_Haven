using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Techniques: perks at levels 20 and 40 that teach a new way of doing something (PerkTechnique).</summary>
public sealed class TechniqueTests
{
    [Test]
    public void EverySkill_TeachesATechnique_AtTwentyAndForty()
    {
        LevelingSettings settings = LevelingSettings.Current;
        var taught = settings.Skills.SelectMany(skill => skill.Milestones)
            .SelectMany(milestone => new[] { milestone.first, milestone.second })
            .Where(perk => perk != null && perk.IsTechnique).Select(perk => perk.Technique).ToList();
        foreach (ProficiencyDefinition skill in settings.Skills)
        {
            foreach (int level in new[] { 20, 40 })
            {
                ProficiencyDefinition.Milestone milestone = skill.Milestones.First(m => m.level == level);
                Assert.IsTrue(milestone.first.IsTechnique || milestone.second.IsTechnique, $"{skill.name} {level}");
                Assert.IsFalse(milestone.first.IsTechnique && milestone.second.IsTechnique, $"{skill.name} {level}: a technique or bonuses, a real choice");
            }
        }

        var every = Enum.GetValues(typeof(PerkTechnique)).Cast<PerkTechnique>().Where(t => t != PerkTechnique.None).ToList();
        CollectionAssert.AreEquivalent(every, taught, "each technique is taught exactly once");
        Assert.NotNull(TechniqueSettings.Current.Pinning, "Pinning Shot roots");
    }

    [Test]
    public void ATechnique_IsKnown_WhileItsPerkIsChosen()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition melee = settings.FindSkill("melee");
        ProficiencyDefinition.Milestone milestone = melee.Milestones.First(m => m.level == 20);
        PerkDefinition riposte = milestone.first.IsTechnique ? milestone.first : milestone.second;
        GameSession.StartNewGame();
        try
        {
            Assert.IsFalse(Techniques.Has(PerkTechnique.Riposte));
            GameSession.Progress.AddExperience(melee, settings.TotalExperienceFor(20), settings);
            Assert.IsTrue(GameSession.Progress.Choose(melee, riposte, settings, false));
            Assert.IsTrue(Techniques.Has(PerkTechnique.Riposte));
            GameSession.Progress.ResetPerks();
            Assert.IsFalse(Techniques.Has(PerkTechnique.Riposte), "chosen again, it is forgotten");
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }

    [Test]
    public void SteadyRhythm_KeepsTheCombo_ThroughANormalSwing()
    {
        GatheringSession Session() => new GatheringSession(6, 1f, 0.5f, 0.4f, 0.1f, 0f, 3, 0f, new System.Random(1), result => 1f);
        GatheringSession plain = Session();
        plain.Resolve(SwingResult.Critical);
        plain.Resolve(SwingResult.Normal);
        Assert.AreEqual(0, plain.Combo);

        GatheringSession steady = Session();
        steady.KeepsComboOnNormal = true;
        steady.Resolve(SwingResult.Critical);
        steady.Resolve(SwingResult.Normal);
        Assert.AreEqual(1, steady.Combo, "a sound swing keeps the run");
        steady.Resolve(SwingResult.Glancing);
        Assert.AreEqual(0, steady.Combo, "a glancing one still breaks it");
    }

    [Test]
    public void SureHand_MakesTheNextPieceFine_AfterAPlainRun()
    {
        LevelingSettings settings = LevelingSettings.Current;
        ProficiencyDefinition smithing = settings.FindSkill("weaponcrafting");
        ProficiencyDefinition.Milestone milestone = smithing.Milestones.First(m => m.level == 20);
        PerkDefinition sureHand = milestone.first.IsTechnique ? milestone.first : milestone.second;
        var sword = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(
            AssetDatabase.FindAssets("t:ItemDefinition CopperSword").First()));
        GameSession.StartNewGame();
        try
        {
            GameSession.Progress.AddExperience(smithing, settings.TotalExperienceFor(20), settings);
            GameSession.Progress.Choose(smithing, sureHand, settings, false);
            var maker = new StatSheet();
            int run = TechniqueSettings.Current.SureHandRun;
            for (int i = 0; i < run; i++)
            {
                Assert.AreEqual(ItemQuality.Plain, CraftedWork.Make(sword, maker).Quality, "no quality chance yet");
            }

            Assert.AreEqual(ItemQuality.Fine, CraftedWork.Make(sword, maker).Quality, "the run ends in good work");
            Assert.AreEqual(0, GameSession.Progress.PlainRunOf("weapons"));
        }
        finally
        {
            GameSession.StartNewGame();
        }
    }
}
