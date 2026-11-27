using System;
using UnityEngine;

/// <summary>
/// Puts a GameObject into combat: it has a faction and a StatSheet, built from its CombatStatsDefinition. Attackers use
/// the sheet to scale their damage, and Damageable uses it for max health, armor and resistances, so the player, an enemy
/// and a building all work the same way. Gear and buffs change the stats through <see cref="Stats"/>.
/// </summary>
[DisallowMultipleComponent]
[OnAFriendsCopy(CopyRule.Same, "the creature's side and stats, readied as the host's (RaidCreatures.Prepare)")]
public sealed class Combatant : MonoBehaviour
{
    [SerializeField] private Faction faction = Faction.Enemy;
    [SerializeField] private CombatStatsDefinition baseStats;
    [Tooltip("Left empty, the default rules are used.")]
    [SerializeField] private CombatRules rules;
    [Tooltip("Enemies go for this first while they can reach it, before anyone nearer on its side (the player, not their companions). See TargetPriority.")]
    [SerializeField] private bool priorityTarget;

    private StatSheet stats;

    public Faction Faction => faction;

    /// <summary>
    /// What everything this one deals is multiplied by: its blows (DamageCalculator.Build) and the burning, poison and hazards they
    /// leave (DamageCalculator.BonusScale). The player's is Combat Technique › Player Damage, set by Player; 1 for everyone else.
    /// </summary>
    public float DamageDealtScale { get; set; } = 1f;

    /// <summary>How this one's physical blows land whatever the attack says: set from the weapon in hand (a club's sword
    /// moves land blunt, WeaponDefinition › Strike Override). None: each attack's own kind.</summary>
    public StrikeKind StrikeOverride { get; set; }

    /// <summary>Changes sides for a while (a practice duel, CompanionSpar), so the other side's blows land; put it back after.</summary>
    public void SetFaction(Faction value) => faction = value;
    public bool IsPriorityTarget => priorityTarget;
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
        DrainLife(target, damage, result);
    }

    // Drain feeds on fighting (FightBlows): not poison ticking away, not a crate or a fence (review 2026-11-27), and only on what the
    // target really lost.
    private void DrainLife(IDamageable target, DamageInfo damage, DamageResult result)
    {
        float share = Stats.Get(StatType.Lifesteal);
        if (share > 0f && FightBlows.Counts(this, target, damage, result) && TryGetComponent(out Damageable health))
        {
            health.Heal(result.HealthLost * share);
        }
    }
}
