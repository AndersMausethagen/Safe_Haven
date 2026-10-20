using System;
using UnityEngine;

/// <summary>
/// Puts a GameObject into combat: it has a faction and a StatSheet, built from its CombatStatsDefinition. Attackers use
/// the sheet to scale their damage, and Damageable uses it for max health, armor and resistances, so the player, an enemy
/// and a building all work the same way. Gear and buffs change the stats through <see cref="Stats"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class Combatant : MonoBehaviour
{
    [SerializeField] private Faction faction = Faction.Enemy;
    [SerializeField] private CombatStatsDefinition baseStats;
    [Tooltip("Left empty, the default rules are used.")]
    [SerializeField] private CombatRules rules;

    private StatSheet stats;

    public Faction Faction => faction;
    public CombatRules Rules => rules != null ? rules : CombatRules.Default;

    /// <summary>The live stats. Built on first use, so it is safe to read from any Awake.</summary>
    public StatSheet Stats
    {
        get
        {
            if (stats == null)
            {
                stats = new StatSheet();
                if (baseStats != null)
                {
                    baseStats.ApplyTo(stats);
                }
                else
                {
                    Debug.LogWarning($"{name} has a Combatant with no stats definition, so every stat is 0.", this);
                }
            }

            return stats;
        }
    }

    /// <summary>Raised after this combatant's hit lands on something. Use it for lifesteal, combat timers and the like.</summary>
    public event Action<IDamageable, DamageInfo, DamageResult> DamageDealt;

    public bool IsHostileTo(Faction other)
    {
        return faction != other;
    }

    /// <summary>Called by whatever applied the hit, once the target has answered.</summary>
    public void ReportDamageDealt(IDamageable target, DamageInfo damage, DamageResult result)
    {
        DamageDealt?.Invoke(target, damage, result);
    }
}
