using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Edit Mode tests for rules of the damage pipeline that a bug hid in: sure criticals, a death decided elsewhere
/// (<see cref="Damageable.Finish"/>), what a block costs, how far a Silent Takedown reaches, the Damage Taken assist, and who
/// collects what a companion or a trap brings down.
/// </summary>
public sealed class DamageRulesTests
{
    private readonly System.Collections.Generic.List<GameObject> made = new System.Collections.Generic.List<GameObject>();

    private static AttackDefinition Slash => AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/SwordSlash.asset");

    [TearDown]
    public void TearDown()
    {
        foreach (GameObject body in made)
        {
            if (body != null)
            {
                Object.DestroyImmediate(body);
            }
        }

        made.Clear();
    }

    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(target, null);

    private Combatant Fighter(string name, Faction side)
    {
        var body = new GameObject(name);
        made.Add(body);
        var combatant = body.AddComponent<Combatant>();
        combatant.SetFaction(side);
        return combatant;
    }

    private Damageable Victim(float health, float armor)
    {
        Combatant combatant = Fighter("Victim", Faction.Enemy);
        // No stats definition (it warns): the values come from here.
        combatant.Stats.SetBase(StatType.MaxHealth, health);
        combatant.Stats.SetBase(StatType.Armor, armor);
        var victim = combatant.gameObject.AddComponent<Damageable>();
        var data = new SerializedObject(victim);
        data.FindProperty("destroyOnDeath").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();
        Invoke(victim, "Awake");
        return victim;
    }

    // ----- Criticals -----

    [Test]
    public void ASureCritical_NeedsNoCritChance()
    {
        var attacker = new StatSheet();
        attacker.SetBase(StatType.PhysicalCritDamage, 0.5f);

        float plain = DamageCalculator.CalculateOutgoing(attacker, CombatRules.Default, Slash, 20f, TargetCategory.Creature, 1f, out bool lucky);
        float sure = DamageCalculator.CalculateOutgoing(attacker, CombatRules.Default, Slash, 20f, TargetCategory.Creature, DamageCalculator.SureCrit, out bool critical);

        Assert.IsFalse(lucky);
        Assert.IsTrue(critical, "a counter or a backstab on a staggered creature is a critical whatever the attacker's chance");
        Assert.AreEqual(plain * 1.5f, sure, 0.001f);
    }

    [Test]
    public void ARollOfZero_IsNoCriticalForAnAttackerWithoutTheChance()
    {
        var attacker = new StatSheet();
        attacker.SetBase(StatType.PhysicalCritDamage, 0.5f);

        DamageCalculator.CalculateOutgoing(attacker, CombatRules.Default, Slash, 20f, TargetCategory.Creature, 0f, out bool critical);

        Assert.IsFalse(critical, "which is why a forced critical rolls SureCrit and not 0");
    }

    // ----- A death decided elsewhere -----

    [Test]
    public void Finish_TakesWhatIsLeft_WhateverTheDefenses()
    {
        Damageable victim = Victim(100f, 300f);
        victim.ApplyDamage(new DamageInfo(null, null, 160f, DamageType.Physical, false, Vector3.zero, Vector3.forward));
        float left = victim.CurrentHealth;
        Assert.Greater(left, 0f);
        Assert.Less(left, 100f);
        victim.AddInvulnerability(this);
        DamageResult last = default;
        victim.Damaged += (damage, result) => last = result;

        victim.Finish(null);

        Assert.IsFalse(victim.IsAlive, "a hop's or a dodge's invulnerability doesn't hold off a death decided by the host's game");
        Assert.IsTrue(last.Killed);
        Assert.AreEqual(left, last.Damage, 0.001f, "the number shown is what it had left, not one made up to get through its armor");
    }

    [Test]
    public void Finish_StillHonoursAFloorSomeoneHolds()
    {
        Damageable victim = Victim(100f, 0f);
        victim.HoldFloor(this, 1f);

        victim.Finish(null);

        Assert.IsTrue(victim.IsAlive);
        Assert.AreEqual(1f, victim.CurrentHealth, 0.001f);
    }

    // ----- Blocking -----

    [Test]
    public void ABlock_CostsByWhatTheBlowIsWorthAfterArmor()
    {
        var blocker = new StatSheet();
        blocker.SetBase(StatType.Armor, 100f); // halves physical damage
        float worth = DamageCalculator.Mitigate(100f, DamageType.Physical, blocker, CombatRules.Default);

        Assert.AreEqual(50f, worth, 0.001f);
        Assert.AreEqual(40f, GuardRules.BlockCost(worth, 0.8f, 1f), 0.001f);
        Assert.AreEqual(48f, GuardRules.BlockCost(worth, 0.8f, 1.2f), 0.001f, "a beginner's shield arm tires sooner");
        Assert.AreEqual(0f, GuardRules.BlockCost(-5f, 0.8f, 1f));
    }

    [Test]
    public void BlockingAtAHigherTier_CostsAboutWhatItDidWithThatTiersArmor()
    {
        // A wolf's bite (18) at tier I in the woven kit (32 armor) and at tier IV (x3.4) in moonsilver (184 armor).
        float rules = CombatTechniqueSettings.Current.BlockStaminaPerDamage;
        var first = new StatSheet();
        first.SetBase(StatType.Armor, 32f);
        var fourth = new StatSheet();
        fourth.SetBase(StatType.Armor, 184f);

        float early = GuardRules.BlockCost(DamageCalculator.Mitigate(18f, DamageType.Physical, first, CombatRules.Default), rules, 1f);
        float late = GuardRules.BlockCost(DamageCalculator.Mitigate(18f * 3.4f, DamageType.Physical, fourth, CombatRules.Default), rules, 1f);

        Assert.Less(late, early * 2f, "by the raw blow it was 3.4 times as dear, with the same stamina to pay it from");
        Assert.Less(late, 25f, "a bite is still a small part of a stamina bar");
    }

    // ----- Silent Takedown -----

    [Test]
    public void ASilentTakedown_ReachesAFewBlowsDeep_AndNoFurther()
    {
        Assert.IsTrue(StrikeRules.IsTakedown(40f, 12f, 4f), "48 would do it");
        Assert.IsTrue(StrikeRules.IsTakedown(48f, 12f, 4f));
        Assert.IsFalse(StrikeRules.IsTakedown(50f, 12f, 4f));
        Assert.IsFalse(StrikeRules.IsTakedown(40f, 0f, 4f), "a blow worth nothing finishes nothing");
    }

    // ----- The Damage Taken assist -----

    [Test]
    public void TheDamageTakenAssist_AndAHeavySwing_Multiply()
    {
        Assert.AreEqual(1f, PlayerCombat.DamageTakenScale(1f, 0f), 0.0001f);
        Assert.AreEqual(0.5f, PlayerCombat.DamageTakenScale(0.5f, 0f), 0.0001f, "the assist alone: it used to be put back to 1 every frame");
        Assert.AreEqual(0.7f, PlayerCombat.DamageTakenScale(1f, 0.3f), 0.0001f);
        Assert.AreEqual(0.45f, PlayerCombat.DamageTakenScale(0.75f, 0.4f), 0.0001f);
    }

    // ----- Who collects a kill -----

    [Test]
    public void WhatACompanionBringsDown_IsThePlayersHaul()
    {
        Combatant player = Fighter("Player", Faction.Player);
        Combatant companion = Fighter("Companion", Faction.Ally);
        Combatant trap = Fighter("Spike Trap", Faction.Player);
        Combatant wolf = Fighter("Wolf", Faction.Enemy);

        Assert.AreSame(player, LootOnDeath.TakerOf(companion, player));
        Assert.AreSame(player, LootOnDeath.TakerOf(trap, player));
        Assert.AreSame(player, LootOnDeath.TakerOf(player, player));
        Assert.AreSame(wolf, LootOnDeath.TakerOf(wolf, player), "a creature's kill is nobody's haul");
        Assert.IsNull(LootOnDeath.TakerOf(null, player), "nor is the forest's");
        Assert.AreSame(companion, LootOnDeath.TakerOf(companion, null));
    }
}
