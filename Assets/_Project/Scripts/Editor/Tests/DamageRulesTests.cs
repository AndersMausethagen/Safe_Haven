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

    // Review 2026-11-16: a Draug held at its floor and ended by fire (or a finishing blow) died of a made-up blow, and whoever struck
    // heard no kill: no sever or smash, no brutal-kill waver, no Second Wind or Flurry Focus.
    [Test]
    public void ADraugEndedByFire_DiesOfThatBlow_AndTheStrikerHearsOfTheKill()
    {
        Damageable victim = Victim(50f, 0f);
        var rises = victim.gameObject.AddComponent<RisesAgain>();
        Invoke(rises, "Awake");
        Invoke(rises, "OnEnable");
        Combatant player = Fighter("Player", Faction.Player);
        DamageInfo diedOf = default;
        victim.Died += (body, blow) => diedOf = blow;

        DamageResult plain = victim.ApplyDamage(new DamageInfo(player, Slash, 80f, DamageType.Physical, false, Vector3.zero, Vector3.forward));
        Assert.IsTrue(victim.IsAlive, "an ordinary blow leaves it down at its floor");
        Assert.IsFalse(plain.Killed);

        DamageResult fire = victim.ApplyDamage(new DamageInfo(player, Slash, 5f, DamageType.Fire, false, Vector3.zero, Vector3.right));

        Assert.IsFalse(victim.IsAlive);
        Assert.IsTrue(fire.Killed, "the blow that ended it is reported as the kill");
        Assert.AreEqual(DamageType.Fire, diedOf.Type);
        Assert.AreSame(Slash, diedOf.Attack, "the death carries the blow's own attack");
        Assert.AreEqual(Vector3.right, diedOf.Direction, "and its direction, for the ragdoll's throw");
        Object.DestroyImmediate(rises);
    }

    // The user, 2026-11-16: damage over time and hazards "scale like blows" (a tier V fire knight's trail burnt as a tier I one's).
    [Test]
    public void DamageOverTime_IsAsStrongAsItsSourceHits()
    {
        Combatant knight = Fighter("Knight", Faction.Enemy);
        knight.Stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, 1f, this));
        Assert.AreEqual(2f, DamageCalculator.BonusScale(knight), 1e-4f, "the raid's power and an elite's share live in Damage Bonus");
        Assert.AreEqual(1f, DamageCalculator.BonusScale(null), 1e-4f, "the forest's own hurts as written");

        var poison = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Poison.asset");
        Damageable victim = Victim(100f, 0f);
        StatusEffects effects = StatusEffects.Apply(victim, poison, knight);
        Assert.AreEqual(2f, effects.Current[0].Scale, 1e-4f, "taken as it is laid, so it keeps its power after the knight falls");
        Object.DestroyImmediate(effects);
    }

    // Guard 7: Silent Takedown's two estimates (the focus ring before, the blow as it lands) are one rule, and it counts what being
    // caught unaware adds, as the blow is judged when it lands (one read the creature's last Damage Taken, often 1).
    [Test]
    public void ABlowFromTheShadows_IsWorthWhatItDoesAsItLands()
    {
        Combatant player = Fighter("Player", Faction.Player);
        Damageable victim = Victim(100f, 0f);
        float outgoing = DamageCalculator.CalculateOutgoing(player.Stats, player.Rules, Slash, 20f, victim.Category, 1f, out _);
        float expected = DamageCalculator.Mitigate(outgoing * 1.5f, Slash.DamageType, StrikeKindExtensions.Of(player, Slash), victim.GetComponent<Combatant>().Stats, victim.GetComponent<Combatant>().Rules)
            * PerceptionSettings.Current.AmbushDamageTaken;

        Assert.Greater(PerceptionSettings.Current.AmbushDamageTaken, 1f, "being caught unaware adds");
        Assert.AreEqual(expected, PlayerBlows.UnawareBlowWorth(player, victim, Slash, 20f, 1.5f), 1e-3f);
    }

    // Second round: Blood-Price's health went through armour and the Damage Taken assist, and a dodge skipped it while the Focus came
    // back all the same; a price is paid exactly.
    [Test]
    public void APriceInHealth_IsPaidExactly_WhateverTheArmourOrADodge()
    {
        Damageable payer = Victim(100f, 300f);
        payer.AddInvulnerability(this);
        payer.DamageTakenMultiplier = 0.5f;
        var price = new DamageInfo(null, null, 10f, DamageType.Physical, false, Vector3.zero, Vector3.down).Scaled(1f, 0f, HitTechnique.OverTime, false);

        payer.PayExactly(price);

        Assert.AreEqual(90f, payer.CurrentHealth, 1e-3f);
    }

    // ----- What a dodge escapes -----

    // Review 2026-11-16: a bog troll's drag and a shield rush pulled a player who dodged through them.
    [Test]
    public void ADodgedBlow_DragsNobody_AndOneThatLandsDoes()
    {
        var drag = AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/BogTrollDrag.asset");
        Assert.IsNotNull(drag);
        Assert.Greater(Mathf.Abs(drag.Pull), 0.01f);
        Combatant troll = Fighter("Bog Troll", Faction.Enemy);
        Damageable dodger = Victim(100f, 0f);
        dodger.transform.position = new Vector3(0f, 0f, 6f);

        ForcedMove.AfterHit(drag, dodger, troll, DamageResult.None);
        Assert.IsNull(dodger.GetComponent<ForcedMove>(), "a dodged blow (DamageResult.None) moves no one");

        ForcedMove.AfterHit(drag, dodger, troll, new DamageResult(0f, 0f, 0f, false, false, DamageType.Physical));
        Assert.IsNotNull(dodger.GetComponent<ForcedMove>(), "one that lands drags, even for no damage (the drag's own is 0)");
    }

    // ----- Blunt, slash and stab (2026-11-14) -----

    [Test]
    public void AKindOfBlow_MeetsItsResistanceOrWeakness_AndOnlyPhysicalBlowsHaveOne()
    {
        var wall = new StatSheet();
        wall.SetBase(StatType.BluntResistance, -0.5f);
        wall.SetBase(StatType.SlashResistance, 0.5f);
        Assert.AreEqual(150f, DamageCalculator.Mitigate(100f, DamageType.Physical, StrikeKind.Blunt, wall, CombatRules.Default), 0.01f, "weak to a hammer");
        Assert.AreEqual(50f, DamageCalculator.Mitigate(100f, DamageType.Physical, StrikeKind.Slash, wall, CombatRules.Default), 0.01f, "a blade glances off");
        Assert.AreEqual(100f, DamageCalculator.Mitigate(100f, DamageType.Physical, StrikeKind.Stab, wall, CombatRules.Default), 0.01f, "no say on stabs");
        Assert.AreEqual(100f, DamageCalculator.Mitigate(100f, DamageType.Physical, wall, CombatRules.Default), 0.01f, "a blow of no kind as before");
        Assert.AreEqual(100f, DamageCalculator.Mitigate(100f, DamageType.Fire, StrikeKind.Blunt, wall, CombatRules.Default), 0.01f, "fire is not a blow");
    }

    [Test]
    public void Weapons_AndTheWalls_HaveTheirKinds()
    {
        Assert.AreEqual(StrikeKind.Slash, Slash.Strike);
        var hammer = AssetDatabase.LoadAssetAtPath<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/Combos/Hammer A.asset");
        Assert.AreEqual(StrikeKind.Blunt, hammer.Strike);
        foreach (string wall in new[] { "Palisade", "StoneWall", "Skigard", "Skigard Gate" })
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{wall}.asset");
            var sheet = new StatSheet();
            building.CombatStats.ApplyTo(sheet);
            Assert.Less(sheet.Get(StatType.BluntResistance), 0f, wall + " is weak to blunt blows");
            Assert.AreEqual(building.CombatStats, building.Prefab.GetComponent<Combatant>() != null
                ? new SerializedObject(building.Prefab.GetComponent<Combatant>()).FindProperty("baseStats").objectReferenceValue : null, wall + "'s prefab reads its stats");
        }
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

    [Test]
    public void ACreatureStruckDownByThePlayersSide_LeavesItsLootOnTheBody_ACrateDoesNot()
    {
        Combatant player = Fighter("Player", Faction.Player);
        Combatant companion = Fighter("Companion", Faction.Ally);
        Combatant wolf = Fighter("Wolf", Faction.Enemy);

        Assert.IsTrue(LootOnDeath.LeavesOnBody(true, true, player, true));
        Assert.IsTrue(LootOnDeath.LeavesOnBody(true, true, companion, true), "a companion's kill waits for the player too");
        Assert.IsFalse(LootOnDeath.LeavesOnBody(true, false, player, true), "a crate or a pot still hands its loot over");
        Assert.IsFalse(LootOnDeath.LeavesOnBody(true, true, wolf, true), "one creature killing another leaves nothing");
        Assert.IsFalse(LootOnDeath.LeavesOnBody(true, true, null, true), "nor does the forest");
        Assert.IsFalse(LootOnDeath.LeavesOnBody(false, true, player, true), "bodies off: into the bag, as before");
        Assert.IsFalse(LootOnDeath.LeavesOnBody(true, true, player, false), "nobody here to loot it");
        Assert.IsFalse(LootOnDeath.LeavesOnBody(true, true, companion, true, nearPlayer: false),
            "a trap or a companion far from the player hands it over, as before: nobody is there to loot it");
    }
}
