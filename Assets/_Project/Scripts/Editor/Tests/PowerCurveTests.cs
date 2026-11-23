using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The player's whole power curve (Docs/PowerCurveResearch_2026-11-22.md §5.2), at the game's own combat rules and over every raid:
/// every way to stack stays within PowerCurveSettings' reach; gear of every tier has a raid tier to be measured in; a co-op party is the
/// same share of a creature at every tier; a draught heals the same share of a tier's player; and blows from the saddle stay bounded.
/// </summary>
public sealed class PowerCurveTests
{
    private static PowerCurveSettings Reach => PowerCurveSettings.Current;

    private static IEnumerable<RaidDefinition> AllRaids() => AssetDatabase.FindAssets("t:RaidDefinition", new[] { "Assets/_Project/Data/Raids" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
        .Where(raid => raid != null && raid.Difficulties.Count > 0);

    [Test]
    public void TheBalanceTool_ReadsTheGamesOwnRules()
    {
        CombatRules game = AssetDatabase.LoadAssetAtPath<CombatRules>(GearBalance.GameRulesPath);
        Assert.IsNotNull(game);
        Assert.AreSame(game, GearBalance.GameRules, "the tool weighs Might as the player's prefab does");
        Assert.AreNotEqual(CombatRules.Default.DamagePerPower, game.DamagePerPower, "the canary: the default would have been another game");
    }

    [Test]
    public void EveryStack_StaysWithinItsReach_AtTheGamesOwnRules_OnEveryRaid()
    {
        List<ItemDefinition> gear = GearBalance.AllGear();
        var tiers = new HashSet<int>();
        foreach (RaidDefinition raid in AllRaids())
        {
            foreach (RaidDifficulty tier in raid.Difficulties)
            {
                tiers.Add(tier.tier);
            }
        }

        Assert.Greater(tiers.Count, 3, "tiers found");
        foreach (int tier in tiers)
        {
            GearBalance.Kit plain = GearBalance.KitOf(tier, gear);
            // The food's reach without the coating, which has a budget of its own on top (PowerCurveSettings › Coating Damage).
            GearBalance.Kit stacked = GearBalance.StackedKitOf(tier, gear, true, tier, true, GearBalance.FoodSlots.Every, coated: false);
            GearBalance.Kit coated = GearBalance.StackedKitOf(tier, gear, true, tier, true, GearBalance.FoodSlots.Every, coated: true);
            string what = string.Join(", ", stacked.Food.Select(food => food.name));
            Assert.Greater(stacked.Toughness, plain.Toughness, $"the canary: stacking adds at tier {tier}");
            Assert.LessOrEqual(stacked.Toughness / plain.Toughness, Reach.StackedToughness, $"tier {tier} toughness, eating {what}");
            Assert.LessOrEqual(stacked.Damage / plain.Damage, Reach.StackedDamageAt(tier), $"tier {tier} damage, eating {what}");
            Assert.LessOrEqual(coated.Damage / plain.Damage, Reach.StackedDamageAt(tier) * Reach.CoatingDamage, $"tier {tier} damage with a coating");
            // One of each family, however many slots (BoonFamily).
            var families = stacked.Food.Where(f => !f.IsTonic).Select(f => BoonFamily.Of(f.BoonBonuses)).Where(f => f.Length > 0).ToList();
            Assert.AreEqual(families.Count, families.Distinct().Count(), $"tier {tier}: one meal of each family ({what})");
        }
    }

    [Test]
    public void TheHighestGearTier_HasARaidTier_OnEveryOpenRaid()
    {
        int gearTop = GearBalance.AllGear().Where(item => item.Category == ItemCategory.Equipment).Max(item => item.Tier);
        foreach (string name in new[] { "MistyWoods", "TrollValley", "MossMarsh" })
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{name}.asset");
            Assert.GreaterOrEqual(raid.Difficulties.Max(d => d.tier), gearTop, $"{name}: Trollstone gear has a raid to be measured in");
        }

        Assert.Greater(SettlementSettings.Current.WardLevels.Count, 3, "a ward level opens tier V");
    }

    [Test]
    public void APartysShare_IsTheSameAtEveryTier()
    {
        float perFriend = MultiplayerSettings.Current.CreatureHealthPerFriend;
        Assume.That(perFriend > 0f);
        RaidDefinition misty = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        var shares = new List<float>();
        foreach (RaidDifficulty tier in misty.Difficulties)
        {
            // A creature's health percent: the tier's own and the party's, added (StatModifierOp.Percent); against a solo one's.
            float solo = tier.enemyHealth;
            float party = 1f + (tier.enemyHealth - 1f) + CreatureHost.PartyHealth(perFriend, 3, tier.enemyHealth);
            shares.Add(party / solo);
        }

        Assert.Greater(shares[0], 1f, "the canary: a party makes creatures tougher");
        Assert.LessOrEqual(shares.Max() - shares.Min(), Reach.PartyShareTolerance, string.Join(", ", shares.Select(s => s.ToString("0.00"))));
    }

    [Test]
    public void ADraught_HealsTheSameShareOfATiersPlayer()
    {
        List<ItemDefinition> gear = GearBalance.AllGear();
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        List<ItemDefinition> draughts = content.Items.Where(item => item != null && item.HealTotal > 0f && item.HungerRestore <= 0f && item.name.Contains("Draught")).ToList();
        Assert.Greater(draughts.Count, 3);
        Assert.IsTrue(draughts.All(d => d.HealAsShare), "draughts heal a share: " + string.Join(", ", draughts.Where(d => !d.HealAsShare).Select(d => d.name)));
        ItemDefinition healing = draughts.First(d => d.name == "HealingDraught");
        var shares = new List<float>();
        for (int tier = 2; tier <= 5; tier++)
        {
            float health = GearBalance.EntryKitOf(tier, gear).Health;
            shares.Add(healing.HealTotalFor(health) / health);
        }

        Assert.LessOrEqual(shares.Max() - shares.Min(), Reach.HealShareTolerance, string.Join(", ", shares.Select(s => s.ToString("0.00"))));
        // The Mending Draught keeps the user's numbers at a tier I kit's 150 health: 30 at once, 40 over its time.
        ItemDefinition mending = draughts.First(d => d.name == "MendingDraught");
        Assert.AreEqual(30f, mending.HealAtOnceFor(150f), 1f);
        Assert.AreEqual(40f, mending.HealAmountFor(150f), 1.5f);
    }

    [Test]
    public void FromTheSaddle_StaysWithinTheDamageReach()
    {
        float most = 0f;
        var horse = AssetDatabase.LoadAssetAtPath<ProficiencyDefinition>("Assets/_Project/Data/Proficiencies/Horsemanship.asset");
        var so = new SerializedObject(horse);
        SerializedProperty levels = so.FindProperty("levelBonuses");
        for (int i = 0; i < levels.arraySize; i++)
        {
            if (levels.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue == (int)StatType.MountedDamage)
            {
                most += Mathf.Max(0f, levels.GetArrayElementAtIndex(i).FindPropertyRelative("atLastLevel").floatValue);
            }
        }

        foreach (string perk in new[] { "Lancer", "RideroftheNorth" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>($"Assets/_Project/Data/Proficiencies/Perks/{perk}.asset");
            SerializedProperty bonuses = new SerializedObject(asset).FindProperty("bonuses");
            for (int i = 0; i < bonuses.arraySize; i++)
            {
                if (bonuses.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue == (int)StatType.MountedDamage)
                {
                    most += bonuses.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue;
                }
            }
        }

        Assert.Greater(most, 0f, "the canary: riding adds damage");
        Assert.LessOrEqual(1f + most, Reach.Mounted, "a steady blow from the saddle");
        Assert.LessOrEqual((1f + most) * 1.25f, Reach.Charge, "a charge's first blow");
    }
}
