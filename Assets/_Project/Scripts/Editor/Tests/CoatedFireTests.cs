using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A coated blade (Ember Pitch, the user's 2026-11-23 recipe): the swing's fire and its burn, the coating's own slot among the boons, the
/// balance model counting it, and the way to it: Cursed Sap from the Misty Woods' felled trees, a recipe the portal's errand teaches and
/// then asks for.
/// </summary>
public sealed class CoatedFireTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            Object.DestroyImmediate(thing);
        }

        made.Clear();
    }

    private static ItemDefinition Pitch => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/EmberPitch.asset");
    private static ItemDefinition Sap => AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/CursedSap.asset");
    private static QuestDefinition Errand => AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/WhatTheOldWoodFears.asset");

    [Test]
    public void TheFire_IsAShareOfTheSwing_AndOnlyWhenCoated()
    {
        Assert.AreEqual(5f, CoatedFire.Amount(20f, 0.25f), 1e-4f, "a quarter of a 20-damage swing");
        Assert.AreEqual(0f, CoatedFire.Amount(20f, 0f), "no coating, no fire");
        Assert.AreEqual(0f, CoatedFire.Amount(0f, 0.25f), "a swing that brought nothing carries nothing");
        Assert.IsTrue(CoatedFire.Ignites(0.05f, 0.01f));
        Assert.IsFalse(CoatedFire.Ignites(0.05f, 0.2f));
        Assert.IsFalse(CoatedFire.Ignites(0f, 0f), "no chance never ignites, even on a roll of 0");
    }

    [Test]
    public void EmberPitch_IsACoating_ThatAddsFireAndCanIgnite()
    {
        ItemDefinition pitch = Pitch;
        Assert.IsNotNull(pitch);
        Assert.IsTrue(pitch.IsCoating && !pitch.IsTonic && !pitch.IsMeal, "a coating: neither a meal nor a tonic");
        Assert.IsTrue(pitch.HasBoon);
        Assert.Greater(pitch.BoonBonuses.Where(b => b.stat == StatType.MeleeFireDamage).Sum(b => b.value), 0f, "every blow carries fire");
        Assert.Greater(pitch.BoonBonuses.Where(b => b.stat == StatType.MeleeIgniteChance).Sum(b => b.value), 0f, "and can set things alight");
        Assert.AreEqual(CraftedGoods.Tonics, CraftedWork.Of(pitch), "made and used as a tonic is");
    }

    [Test]
    public void ACoating_HasItsOwnSlot_OneAtATime_BesideTheFood()
    {
        var player = new GameObject("Coated");
        made.Add(player);
        player.AddComponent<Combatant>();
        PlayerBoons boons = player.AddComponent<PlayerBoons>();
        var fire = new List<StatBonus> { new StatBonus(StatType.MeleeFireDamage, 0.25f) };
        var meal = new List<StatBonus> { new StatBonus(StatType.Strength, 3f) };
        Assert.IsTrue(boons.Add("meal", "Roast", meal, 300f, true));
        Assert.IsTrue(boons.Add("pitch_a", "Pitch A", fire, 300f, false, false, true));
        Assert.IsTrue(boons.Add("pitch_b", "Pitch B", fire, 300f, false, false, true));
        List<BoonRecord> now = boons.Capture();
        Assert.AreEqual(1, now.Count(b => b.IsCoating), "a new coating wipes the old one off");
        Assert.AreEqual("pitch_b", now.First(b => b.IsCoating).Id);
        Assert.AreEqual(1, now.Count(b => b.IsMeal), "the meal stays: a coating shares no slot or family with food");

        // Through the portal and back (PlayerSnapshot carries the records).
        var other = new GameObject("Arrived");
        made.Add(other);
        other.AddComponent<Combatant>();
        PlayerBoons arrived = other.AddComponent<PlayerBoons>();
        arrived.Restore(now);
        Assert.AreEqual(1, arrived.Capture().Count(b => b.IsCoating), "the coating goes through the portal as a coating");
        Assert.AreEqual(2, arrived.EndMealsAndTonics(), "a fall ends the coating with the food");
    }

    [Test]
    public void TheBalanceModel_CountsTheFire()
    {
        var plain = new FightMath.Totals();
        var coated = new FightMath.Totals();
        coated.Add(new StatBonus(StatType.MeleeFireDamage, 0.25f));
        CombatRules rules = GearBalance.GameRules;
        Assert.AreEqual(1.25f, coated.DamageShare(rules) / plain.DamageShare(rules), 1e-3f, "a quarter more, as fire");
        coated.Add(new StatBonus(StatType.MeleeIgniteChance, 0.1f));
        Assert.Greater(coated.DamageShare(rules) / plain.DamageShare(rules), 1.25f, "and a burn now and then on top");
    }

    [Test]
    public void AStackedKit_CarriesTheBestCoating()
    {
        GearBalance.Kit stacked = GearBalance.StackedKitOf(1, GearBalance.AllGear(), false, true);
        Assert.IsTrue(stacked.Food.Any(item => item.IsCoating), "the reach counts what a prepared player carries, coating included");
        Assert.AreEqual(1, stacked.Food.Count(item => item.IsCoating), "one coating at a time");
    }

    [Test]
    public void ARaidOnlyRow_RollsInItsRaid_AndNowhereElse()
    {
        ItemDefinition sap = Sap;
        var misty = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        var valley = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/TrollValley.asset");
        Assert.IsNotNull(sap);
        Assert.IsNotNull(misty);
        var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>("Assets/_Project/Data/Gathering/Tree.asset");
        // Rolled many times, a half chance shows up; never outside its raid.
        int inMisty = 0, elsewhere = 0;
        for (int i = 0; i < 200; i++)
        {
            inMisty += node.Yields.RollOnlyIn(misty).Count(drop => drop.Item == sap);
            elsewhere += node.Yields.RollOnlyIn(null).Count(drop => drop.Item == sap) + node.Yields.RollOnlyIn(valley).Count(drop => drop.Item == sap);
            elsewhere += node.Yields.Roll(0, 1f).Count(drop => drop.Item == sap) + node.Yields.Roll(1, 1f).Count(drop => drop.Item == sap);
        }

        Assert.Greater(inMisty, 50, "a felled Misty Woods tree often weeps sap");
        Assert.AreEqual(0, elsewhere, "never at home, in another raid, or through the ordinary roll");
    }

    [Test]
    public void TheTreePrefabs_DontCarryTheRaidOnlyRow()
    {
        // The prefabs' own drops have no raid to check: carried there, the sap would drop at home too (the review, 2026-11-23).
        ItemDefinition sap = Sap;
        int trees = 0;
        foreach (string id in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id));
            if (prefab == null || !prefab.TryGetComponent(out ChoppableTree _) || !prefab.TryGetComponent(out ResourceSource source))
            {
                continue;
            }

            trees++;
            Assert.IsFalse(source.Drops.Entries.Any(entry => entry != null && entry.Item == sap), prefab.name);
        }

        Assert.Greater(trees, 0, "the canary: tree prefabs were found");
    }

    [Test]
    public void ThePortalsErrand_TeachesThePitch_ThenAsksForIt()
    {
        QuestDefinition errand = Errand;
        Assert.IsNotNull(errand);
        RecipeDefinition recipe = errand.TeachesAtStart.FirstOrDefault(r => r != null && r.Output == Pitch);
        Assert.IsNotNull(recipe, "it teaches Ember Pitch as it starts");
        Assert.IsFalse(recipe.KnownFromStart, "nobody knows it before the portal says");
        Assert.IsTrue(recipe.ItemCost.Any(cost => cost.Item == Sap), "made from Cursed Sap");
        Assert.IsTrue(errand.Objectives.Any(o => o.Type == ObjectiveType.CollectItem && o.Item == Sap), "it sends the player for sap");
        Assert.IsTrue(errand.Objectives.Any(o => o.Type == ObjectiveType.CraftItem && o.Item == Pitch), "and asks for the pitch");
        Assert.AreNotEqual(QuestRank.MainStory, errand.Rank, "an errand: it never stands in the story's way");
    }
}
