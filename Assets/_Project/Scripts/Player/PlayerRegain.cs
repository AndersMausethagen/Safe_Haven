using System;
using UnityEngine;

/// <summary>
/// Regain (Bloodborne, Lies of P; Docs/CombatResearch.md §3.6 Phase 3 "build and spend"): part of a blow that got through can be won back
/// for a few seconds by striking back. Every melee blow that lands heals some of it; what isn't won back drains away. It rewards answering
/// a hit with aggression rather than backing off to wait for a parry. The health bar shows what can still be won back (PlayerVitalsView).
/// Numbers are in Combat Technique Settings › Regain.
/// </summary>
[RequireComponent(typeof(Damageable))]
public sealed class PlayerRegain : MonoBehaviour
{
    private Damageable health;
    private Combatant combatant;
    private float pool;
    private float hurtAt = float.NegativeInfinity;

    /// <summary>Multiplies what a blow wins back (a great enchantment, Regrowing). 1 as made.</summary>
    public float WonScale { get; set; } = 1f;

    /// <summary>Health that striking back can still win back right now (drains to 0 over the Regain window).</summary>
    public float Regainable => RegainRules.Left(pool, Time.time - hurtAt, Rules.RegainSeconds);

    /// <summary>Raised when health is won back, with how much.</summary>
    public event Action<float> Regained;

    private static CombatTechniqueSettings Rules => CombatTechniqueSettings.Current;

    private void Awake()
    {
        health = GetComponent<Damageable>();
        combatant = GetComponent<Combatant>();
    }

    private void OnEnable()
    {
        health.Damaged += OnHurt;
        if (combatant != null)
        {
            combatant.DamageDealt += OnDamageDealt;
        }
    }

    private void OnDisable()
    {
        health.Damaged -= OnHurt;
        if (combatant != null)
        {
            combatant.DamageDealt -= OnDamageDealt;
        }

        pool = 0f;
    }

    private void OnHurt(DamageInfo damage, DamageResult result)
    {
        if (result.Blocked || result.Killed || damage.IsOverTime || result.Damage <= 0f || Rules.RegainShare <= 0f)
        {
            return;
        }

        // What is left of the last window carries into the new one. Health lost, not the blow's size: one a floor held off (downed,
        // sparring) cost nothing to win back (review 2026-11-27).
        if (result.HealthLost <= 0f)
        {
            return;
        }

        pool = Mathf.Min(health.MaxHealth, Regainable + result.HealthLost * Rules.RegainShare);
        hurtAt = Time.time;
    }

    private void OnDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        // Won back by fighting (FightBlows): a crate or a fence healed (review 2026-11-27).
        if ((damage.Technique & HitTechnique.Melee) == 0 || !health.IsAlive || !FightBlows.Counts(combatant, target, damage, result))
        {
            return;
        }

        float left = Regainable;
        float heal = RegainRules.Won(left, result.Damage, Rules.RegainPerDamage * Mathf.Max(0f, WonScale));
        if (heal <= 0f)
        {
            return;
        }

        float healed = health.Heal(heal);
        // The window keeps its clock: what is left after the heal drains on as it was.
        float drained = left > 0f ? pool / left : 1f;
        pool = Mathf.Max(0f, (left - heal) * drained);
        if (healed > 0f)
        {
            Regained?.Invoke(healed);
        }
    }
}

/// <summary>Regain as plain functions (tested in RegainTests).</summary>
public static class RegainRules
{
    /// <summary>What is left of <paramref name="pool"/> <paramref name="since"/> seconds after the hurt, draining evenly over <paramref name="window"/>.</summary>
    public static float Left(float pool, float since, float window) => window <= 0f || since < 0f ? 0f : pool * Mathf.Clamp01(1f - since / window);

    /// <summary>Health a blow of <paramref name="dealt"/> damage wins back, at most what is <paramref name="left"/>.</summary>
    public static float Won(float left, float dealt, float perDamage) => Mathf.Min(Mathf.Max(0f, left), Mathf.Max(0f, dealt * perDamage));
}
