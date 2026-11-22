using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Docs/PlayerStatsResearch.md: the Fight card's arithmetic (FightMath), the tonic slot, and the raid planner's harms. The stacked kit's
/// reach is held in RaidTierTests, the dodge classes in ContentExpansionTests.
/// </summary>
public sealed class PlayerStatsTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object made in created)
        {
            if (made != null)
            {
                Object.DestroyImmediate(made);
            }
        }

        created.Clear();
    }

    // ----- FightMath -----

    [Test]
    public void TheDamageShare_MultipliesMightBonusSkillAndExpectedCrits()
    {
        CombatRules rules = CombatRules.Default;
        var plain = new FightMath.Totals { CritChance = 0f };
        Assert.AreEqual(1f, plain.DamageShare(rules), 1e-4f, "no stats: the weapon as it is");

        var totals = new FightMath.Totals { Strength = 3f, Power = 2f, DamageBonus = 0.1f, MeleeDamage = 0.2f, CritChance = 0.1f, CritDamage = 0.5f };
        float might = 1f + (3f * rules.PowerPerStrength + 2f) * rules.DamagePerPower;
        float expected = might * 1.1f * 1.2f * (1f + 0.1f * 0.5f);
        Assert.AreEqual(expected, totals.DamageShare(rules), 1e-4f);
        Assert.AreEqual(12f * expected, totals.Damage(12f, rules), 1e-3f);
    }

    [Test]
    public void HealthCountsFlatThenPercent_AndToughnessIsHealthOverWhatArmourLetsThrough()
    {
        CombatRules rules = CombatRules.Default;
        var totals = new FightMath.Totals();
        totals.Add(new StatBonus(StatType.MaxHealth, 20f));
        totals.Add(new StatBonus(StatType.MaxHealth, 0.1f, StatModifierOp.Percent));
        totals.Add(new StatBonus(StatType.Armor, 10f));
        Assert.AreEqual(120f * 1.1f, totals.FullHealth, 1e-3f);
        Assert.AreEqual(totals.FullHealth / (1f - rules.GetArmorReduction(10f)), totals.Toughness(rules), 1e-3f);

        FightMath.Totals copy = totals.Copy();
        copy.Add(new StatBonus(StatType.Armor, 5f));
        Assert.AreEqual(10f, totals.Armor, 1e-4f, "a copy doesn't share its totals");
    }

    [Test]
    public void BlowsToFellAndToTake_FollowTheTiersScales()
    {
        CombatRules rules = CombatRules.Default;
        float letThrough = 1f - rules.GetArmorReduction(5f);
        Assert.AreEqual(60f / (15f * letThrough), FightMath.HitsToKill(15f, 60f, 5f, 1f, rules), 1e-3f);
        Assert.AreEqual(2f * FightMath.HitsToKill(15f, 60f, 5f, 1f, rules), FightMath.HitsToKill(15f, 60f, 5f, 2f, rules), 1e-3f, "twice the health, twice the blows");
        Assert.AreEqual(100f / 15f, FightMath.HitsToDie(100f, 15f, 1f), 1e-3f);
        Assert.AreEqual(100f / 30f, FightMath.HitsToDie(100f, 15f, 2f), 1e-3f);
    }

    [Test]
    public void ALiveStatSheet_ComesToTheSameTotals()
    {
        var holder = new GameObject("Fighter");
        created.Add(holder);
        StatSheet stats = holder.AddComponent<Combatant>().Stats;
        stats.SetBase(StatType.Strength, 4f);
        stats.SetBase(StatType.DamageBonus, 0.05f);
        stats.SetBase(StatType.Armor, 12f);
        FightMath.Totals totals = FightMath.Of(stats, 140f);
        Assert.AreEqual(4f, totals.Strength, 1e-4f);
        Assert.AreEqual(0.05f, totals.DamageBonus, 1e-4f);
        Assert.AreEqual(12f, totals.Armor, 1e-4f);
        Assert.AreEqual(140f, totals.FullHealth, 1e-4f, "the live max health already holds its percent parts");
    }

    // ----- The tonic slot -----

    [Test]
    public void ASecondTonicPushesOutTheFirst_WhileMealsKeepTheirOwnSlots()
    {
        var eater = new GameObject("Eater");
        created.Add(eater);
        eater.AddComponent<Combatant>();
        PlayerBoons boons = eater.AddComponent<PlayerBoons>();
        StatBonus[] strong = { new StatBonus(StatType.Strength, 3f) };
        // Of another family than the tonics (BoonFamily: one of each family), so only the tonic slot is in question here.
        StatBonus[] hearty = { new StatBonus(StatType.MaxHealth, 0.05f, StatModifierOp.Percent) };

        Assert.AreEqual(1, SurvivalSettings.Current.TonicSlots);
        Assert.AreEqual(1, boons.TonicSlots, "a fresh settlement and no Second Vial: one tonic at once");
        boons.Add("stew", "Stew", hearty, 300f, true);
        boons.Add("first", "First", strong, 300f, false, true);
        boons.Add("second", "Second", strong, 120f, false, true);
        CollectionAssert.AreEquivalent(new[] { "stew", "second" }, boons.Capture().Select(boon => boon.Id));

        boons.Add("chill", "Chill", strong, 60f, false);
        Assert.AreEqual(3, boons.Count, "a boon that is neither meal nor tonic (the chill, a blessing) takes no slot");
    }

    [Test]
    public void TheMastersShelf_AndSecondVial_AreWhereTheExtraTonicComesFrom()
    {
        var cauldron = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/Cauldron.asset");
        Assert.IsNotNull(cauldron);
        var levels = new SerializedObject(cauldron).FindProperty("upgrades");
        bool shelf = false;
        for (int i = 0; levels != null && i < levels.arraySize; i++)
        {
            SerializedProperty extra = levels.GetArrayElementAtIndex(i).FindPropertyRelative("extraTonicSlots");
            shelf |= extra != null && extra.intValue > 0;
        }

        Assert.IsTrue(shelf, "a Cauldron upgrade holds a second tonic");
        Assert.IsTrue(AssetDatabase.FindAssets("t:PerkDefinition").Select(guid => AssetDatabase.LoadAssetAtPath<PerkDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .Any(perk => perk != null && perk.Technique == PerkTechnique.SecondVial), "a perk teaches Second Vial");
    }

    // ----- What a run can do beyond blows -----

    [Test]
    public void TheMistyWoods_NameItsBlightAndFrost()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        List<RaidHarms.Harm> harms = RaidHarms.For(raid, Tiers.Max);
        CollectionAssert.AreEquivalent(new[] { DamageType.Frost, DamageType.Holy }, harms.Select(harm => harm.Type));
        Assert.IsTrue(harms.Single(harm => harm.Type == DamageType.Holy).From.Count > 0);

        string line = RaidHarms.Describe(harms, false, "#888888");
        StringAssert.Contains("Blight", line);
        StringAssert.Contains("Frost or Blight Resistance", line);
        StringAssert.Contains("Warding", line);
        StringAssert.DoesNotContain("Holy", line, "Holy only ever came from poison: it reads Blight");
    }

    [Test]
    public void ARunOfPlainBlows_SuggestsHeartyOrFierceFood_AndTheColdAsksForFrost()
    {
        StringAssert.Contains("Hearty", RaidHarms.Describe(new List<RaidHarms.Harm>(), false, "#888888"));
        string cold = RaidHarms.Describe(new List<RaidHarms.Harm>(), true, "#888888");
        StringAssert.Contains("the cold", cold);
        StringAssert.Contains("Frost Resistance", cold);
    }

    [Test]
    public void EveryMealAndTonicWithAFightStat_HasAFamily()
    {
        foreach (ItemDefinition item in AssetDatabase.FindAssets("t:ItemDefinition").Select(guid => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid))))
        {
            if (item == null || item.BoonBonuses == null || item.BoonBonuses.Count == 0)
            {
                continue;
            }

            bool fights = item.BoonBonuses.Any(bonus => bonus.stat != StatType.GatherYield && bonus.stat != StatType.ForageYield);
            if (fights)
            {
                Assert.IsNotEmpty(ItemTooltipView.MealFamily(item.BoonBonuses), item.name);
            }
        }
    }

    [Test]
    public void APercentBonus_Works_OnAStatWithItsBaseOutsideTheSheet()
    {
        // Review 2026-11-16: Chilled (-8% speed, -15% stamina), Rested and Moon Broth (+15% stamina) and the Waystone (+10% gather
        // yield, +5% speed) did nothing: the player has no base for these in the sheet, and a percent only scaled the flat ones.
        var stats = new StatSheet();
        stats.AddModifier(new StatModifier(StatType.MaxStamina, StatModifierOp.Percent, 0.15f, this));
        Assert.AreEqual(115f, stats.GetOn(StatType.MaxStamina, 100f), 0.01f, "Rested: 15% more of the pool");

        stats.AddModifier(new StatModifier(StatType.MoveSpeed, StatModifierOp.Percent, -0.08f, this));
        Assert.AreEqual(0.92f, stats.GetOn(StatType.MoveSpeed, 1f), 0.001f, "Chilled slows");
        stats.AddModifier(new StatModifier(StatType.MoveSpeed, StatModifierOp.Flat, -0.06f, this));
        Assert.Less(stats.GetOn(StatType.MoveSpeed, 1f), 0.92f, "a slowing armour and the chill: slower still, not faster");

        stats.AddModifier(new StatModifier(StatType.GatherYield, StatModifierOp.Percent, 0.1f, this));
        Assert.AreEqual(0.1f, stats.GetChance(StatType.GatherYield), 0.001f, "the Waystone's ten points of extra yield");
    }
}
