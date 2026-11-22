using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Carries out an attack: finds the targets in its hit area, builds each target's hit from the attacker's stats, applies
/// it and tells the attacker how it went. The player's weapon swing calls it when the hit lands, and an ability caster
/// does the same with its own AttackDefinition, so every source of damage goes through one pipeline.
/// Projectiles and damage over time skip the area query and call DamageCalculator.Build and ApplyDamage directly.
/// </summary>
public static class AttackResolver
{
    // Lists of hits, lent out and handed back: a hit can set off another attack (a burst on death), so one shared list won't do.
    private static readonly Stack<List<HitDetector.Hit>> spareLists = new Stack<List<HitDetector.Hit>>();

    /// <summary>
    /// Hits everything the attack reaches from origin toward direction. weaponDamage is the damage of the weapon behind
    /// the attack, or 0 for attacks that don't use one. The player's technique (rhythm, counters, finishers) comes in as
    /// a damage and poise multiplier and the techniques the hits carry. Returns how many targets were hit.
    /// </summary>
    public static int Execute(
        Combatant attacker,
        AttackDefinition attack,
        float weaponDamage,
        Vector3 origin,
        Vector3 direction,
        int layerMask,
        float damageMultiplier = 1f,
        float poiseMultiplier = 1f,
        HitTechnique technique = HitTechnique.None,
        bool forceCritical = false)
    {
        // Fire sets webs, gas and dry grass alight where it lands; frost freezes pools and puts fires out (GroundHazard).
        if (attack.DamageType == DamageType.Fire || attack.DamageType == DamageType.Frost)
        {
            Vector3 flat = direction;
            flat.y = 0f;
            Vector3 middle = origin + (flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.zero) * (attack.Arc >= 300f ? 0f : attack.Range * 0.5f);
            GroundHazard.Meet(attack.DamageType == DamageType.Fire ? HazardElement.Fire : HazardElement.Frost, middle, attack.Range * 0.6f, attacker);
        }

        // Every blow throws the mist the way it goes.
        MistWakes.Strike(origin, direction, attack.Range, attack.Arc, attacker);
        List<HitDetector.Hit> hits = spareLists.Count > 0 ? spareLists.Pop() : new List<HitDetector.Hit>(8);
        try
        {
            HitDetector.FindTargets(origin, direction, attack, attacker.Faction, layerMask, hits);
            for (int i = 0; i < hits.Count; i++)
            {
                Apply(attacker, attack, weaponDamage, hits[i], origin, direction, damageMultiplier, poiseMultiplier, technique, forceCritical);
            }

            return hits.Count;
        }
        finally
        {
            hits.Clear();
            spareLists.Push(hits);
        }
    }

    /// <summary>Lands the attack on one target already found. The player's swing strikes its targets one after another with this.</summary>
    public static DamageResult Apply(
        Combatant attacker,
        AttackDefinition attack,
        float weaponDamage,
        in HitDetector.Hit hit,
        Vector3 origin,
        Vector3 direction,
        float damageMultiplier = 1f,
        float poiseMultiplier = 1f,
        HitTechnique technique = HitTechnique.None,
        bool forceCritical = false)
    {
        Vector3 hitDirection = hit.Point - origin;
        hitDirection.y = 0f;
        hitDirection = hitDirection.sqrMagnitude > 0.0001f ? hitDirection.normalized : direction;

        DamageInfo damage = DamageCalculator.Build(
            attacker,
            attack,
            weaponDamage,
            hit.Target.Category,
            hit.Point,
            hitDirection,
            forceCritical ? DamageCalculator.SureCrit : Random.value);
        if (damageMultiplier != 1f || poiseMultiplier != 1f || technique != HitTechnique.None || forceCritical)
        {
            damage = damage.Scaled(damageMultiplier, poiseMultiplier, technique, forceCritical && attack.CanCrit);
        }

        DamageResult result = hit.Target.ApplyDamage(damage);
        attacker.ReportDamageDealt(hit.Target, damage, result);
        StatusEffects.AfterHit(attack, hit.Target, attacker, result);
        ForcedMove.AfterHit(attack, hit.Target, attacker, result);
        return result;
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        spareLists.Clear();
    }
}
