using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The user's playtest list of 2026-11-25: every strike but a heavy stops for a parry at once; health regained out of combat (Stewed
/// Berries); every tier I dish gives some boon. Rules over the shipped data, not copies of it.
/// </summary>
public sealed class PlayRequestsTests1125
{
    private static IEnumerable<T> All<T>() where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                yield return asset;
            }
        }
    }

    // "normal strikes should automatically stop for a parry and it should happen instantly ... except for when we are in a dodge or
    // doing a heavy strike". Every third swing (the finishers) and every rush used to play its blow out first.
    [Test]
    public void EveryStrikeButAHeavy_GivesWayToTheGuardInItsWindup()
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        var held = new List<string>();
        int checkedStrikes = 0;
        foreach (WeaponDefinition weapon in All<WeaponDefinition>())
        {
            var strikes = new List<(AttackDefinition attack, SwingKind kind)>();
            foreach (AttackDefinition attack in weapon.Combo)
            {
                strikes.Add((attack, SwingKind.Light));
            }

            strikes.Add((weapon.RushAttack, SwingKind.Rush));
            strikes.Add((weapon.BackstepAttack, SwingKind.Rush));
            strikes.Add((weapon.OwnGuardCounter, SwingKind.GuardCounter));
            foreach ((AttackDefinition attack, SwingKind kind) in strikes)
            {
                if (attack == null || attack == weapon.HeavyAttack || attack == weapon.ChargedAttack)
                {
                    continue;
                }

                checkedStrikes++;
                if ((SwingMotion.WindupCancels(attack, kind, rules) & SwingCancels.Guard) == 0)
                {
                    held.Add($"{weapon.name} › {attack.name} ({kind})");
                }
            }
        }

        Assert.Greater(checkedStrikes, 20, "the weapons' strikes were found");
        Assert.IsEmpty(held, "these wind-ups keep the guard waiting for their blow");
        Assert.AreEqual(SwingCancels.None, SwingMotion.WindupCancels(null, SwingKind.Heavy, rules) & SwingCancels.Guard,
            "a heavy's wind-up is still a commitment");
        Assert.AreEqual(SwingCancels.None, SwingMotion.WindupCancels(null, SwingKind.Art, rules) & SwingCancels.Guard,
            "so is a weapon art's (its Focus is paid as it starts)");
        Assert.AreNotEqual(SwingCancels.None, SwingMotion.WindupCancels(null, SwingKind.ShieldBash, rules) & SwingCancels.Guard, "a shield bash gives way");
    }

    // "it should happen the instant we press it": the guard always goes up on the press; a raise too soon after lowering it (a tap
    // fished for a parry) blocks but never parries, not even a blow landing in the very frame it went up.
    [Test]
    public void ARaiseTooSoon_Blocks_ButNeverParries()
    {
        Vector3 facing = Vector3.forward, blow = Vector3.back;
        Assert.AreEqual(GuardOutcome.Parried, GuardRules.Resolve(true, 0f, facing, blow, 120f, 0.15f, 100f, 10f), "in its window: a parry");
        Assert.AreEqual(GuardOutcome.Blocked, GuardRules.Resolve(true, 0f, facing, blow, 120f, float.NegativeInfinity, 100f, 10f),
            "parry-locked (PlayerGuard): blocked, in the same frame too");
        Assert.AreEqual(GuardOutcome.Blocked, GuardRules.Resolve(true, -0.01f, facing, blow, 120f, float.NegativeInfinity, 100f, 10f),
            "and from a physics step a moment before the raise");
    }

    [Test]
    public void HealthRegen_GivesItsShareBack_OutOfCombat_OnceAnInterval()
    {
        var body = new GameObject("Resting Player");
        try
        {
            Combatant fighter = body.AddComponent<Combatant>();
            Damageable health = body.AddComponent<Damageable>();
            Call(fighter, "Awake");
            fighter.Stats.SetBase(StatType.MaxHealth, 200f);
            Call(health, "Awake");
            Call(health, "OnEnable");
            health.SetHealthFraction(0.5f);
            var resting = body.AddComponent<PlayerRestingHeal>();
            Call(resting, "Awake");

            Call(resting, "Update");
            Assert.AreEqual(100f, health.CurrentHealth, 0.01f, "nothing eaten: nothing back");

            fighter.Stats.AddModifier(new StatModifier(StatType.HealthRegen, StatModifierOp.Flat, 0.01f, this));
            typeof(PlayerRestingHeal).GetField("next", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(resting, float.NegativeInfinity); // the five seconds already waited
            Call(resting, "Update");
            Assert.AreEqual(102f, health.CurrentHealth, 0.01f, "1% of 200");
            Call(resting, "Update");
            Assert.AreEqual(102f, health.CurrentHealth, 0.01f, "then not again until the next five seconds");
        }
        finally
        {
            Object.DestroyImmediate(body);
        }
    }

    // "every tier 1 food should give some buff, if only just very minor for the basic recipies". Cooked dishes (a recipe's output that
    // feeds, made at the campfire or in the kitchen); raw finds and produce (berries, a fish off the line, a farm's turnips) are what they are.
    [Test]
    public void EveryTierOneDish_GivesABoon()
    {
        var plain = new List<string>();
        int dishes = 0;
        foreach (RecipeDefinition recipe in All<RecipeDefinition>())
        {
            ItemDefinition dish = recipe.Output;
            bool cooked = recipe.Station == CraftingStationType.Campfire || recipe.Station == CraftingStationType.Kitchen;
            if (!cooked || dish == null || dish.Tier > 1 || dish.HungerRestore <= 0f || dish.IsTonic || dish.IsCoating)
            {
                continue;
            }

            dishes++;
            if (!dish.HasBoon || dish.BoonBonuses.Count == 0)
            {
                plain.Add($"{dish.name} ({recipe.name})");
            }
        }

        Assert.Greater(dishes, 5, "the tier I dishes were found");
        Assert.IsEmpty(plain, "these tier I dishes give nothing but food");
    }

    private static void Call(object target, string method) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(target, null);
}
