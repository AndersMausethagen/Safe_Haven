using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Puts the player's skills on the player: each skill's level bonuses at its current level (a beginner's penalties
/// below the baseline level, gains above it) and the bonuses of every perk chosen, as stat modifiers on the player's
/// Combatant. Kept up to date when a level is reached, a perk chosen or a game loaded; plain experience gains that don't
/// change a level change nothing. Lives on the player. Once PlayerTalents; the script keeps its GUID.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Combatant))]
public sealed class PlayerProficiencies : MonoBehaviour
{
    private readonly List<StatModifier> pending = new List<StatModifier>();
    private Combatant combatant;
    private CharacterProgress watched;
    private int appliedKey = int.MinValue;

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
    }

    private void OnEnable()
    {
        Watch(GameSession.Progress);
        Apply();
    }

    private void OnDisable()
    {
        Watch(null);
        combatant.Stats.RemoveModifiersFrom(this);
        appliedKey = int.MinValue;
    }

    // A loaded game replaces the progress object, so it is checked again every frame (cheap: a reference compare).
    private void Update()
    {
        if (watched != GameSession.Progress)
        {
            Watch(GameSession.Progress);
            Apply();
        }
    }

    private void Watch(CharacterProgress progress)
    {
        if (watched != null)
        {
            watched.Changed -= OnChanged;
        }

        watched = progress;
        if (watched != null)
        {
            watched.Changed += OnChanged;
        }
    }

    private void OnChanged()
    {
        if (StateKey(watched, LevelingSettings.Current) != appliedKey)
        {
            Apply();
        }
    }

    /// <summary>Replaces the skill modifiers with the ones from the current levels and perks.</summary>
    public void Apply()
    {
        LevelingSettings settings = LevelingSettings.Current;
        pending.Clear();
        Collect(watched, settings, this, pending);
        StatSheet stats = combatant.Stats;
        stats.RemoveModifiersFrom(this);
        foreach (StatModifier modifier in pending)
        {
            stats.AddModifier(modifier);
        }

        appliedKey = StateKey(watched, settings);
    }

    /// <summary>
    /// Every modifier the player's skills give right now, from <paramref name="source"/>. Static so the skills window
    /// and tests can read the same numbers without a player.
    /// </summary>
    public static void Collect(CharacterProgress progress, LevelingSettings settings, object source, List<StatModifier> into)
    {
        if (progress == null)
        {
            return;
        }

        foreach (ProficiencyDefinition skill in settings.Skills)
        {
            if (skill == null)
            {
                continue;
            }

            int level = progress.LevelOf(skill, settings);
            foreach (ProficiencyDefinition.LevelBonus bonus in skill.LevelBonuses)
            {
                float value = ProficiencyDefinition.ValueAt(bonus, level, settings);
                if (!Mathf.Approximately(value, 0f))
                {
                    into.Add(new StatModifier(bonus.stat, StatModifierOp.Flat, value, source));
                }
            }

            foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
            {
                PerkDefinition chosen = milestone.level <= level ? progress.ChosenAt(milestone) : null;
                if (chosen == null)
                {
                    continue;
                }

                foreach (StatBonus bonus in chosen.Bonuses)
                {
                    into.Add(new StatModifier(bonus.stat, bonus.op, bonus.value, source));
                }
            }
        }
    }

    /// <summary>A sneak attack: the target is a monster that doesn't know the attacker is there. Always a critical, from a
    /// blade or a bow (the user, 2026-11-13).</summary>
    public static bool CaughtUnaware(IDamageable target) =>
        target is Component body && body != null && body.TryGetComponent(out MonsterAI monster) && monster.IsUnaware;

    /// <summary>Extra damage from the attacker's Ambush Damage when the target is an unaware monster; 1 otherwise.</summary>
    public static float AmbushScale(StatSheet attacker, IDamageable target)
    {
        float bonus = attacker.Get(StatType.AmbushDamage);
        return bonus > 0f && target is Component body && body.TryGetComponent(out MonsterAI monster) && monster.IsUnaware ? 1f + bonus : 1f;
    }

    // Levels and perks, folded into one number: a change in either means the modifiers must be made again.
    private static int StateKey(CharacterProgress progress, LevelingSettings settings)
    {
        if (progress == null)
        {
            return 0;
        }

        unchecked
        {
            int key = 17;
            foreach (ProficiencyDefinition skill in settings.Skills)
            {
                if (skill == null)
                {
                    continue;
                }

                key = key * 31 + progress.LevelOf(skill, settings);
                foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
                {
                    PerkDefinition chosen = progress.ChosenAt(milestone);
                    key = key * 31 + (chosen == null ? 0 : chosen == milestone.first ? 1 : 2);
                }
            }

            return key;
        }
    }
}
