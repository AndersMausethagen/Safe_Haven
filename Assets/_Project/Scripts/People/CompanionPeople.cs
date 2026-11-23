using UnityEngine;

/// <summary>
/// Who a person is, made into how they fight beside the player (Docs/PeopleResearch_2026-11-23.md §3.1, §3.3, §3.6): their fighting
/// skill's damage and their footwork's dodge (ResidentTalents), their traits' and marks' fighting effects (ResidentTraits), an inspired
/// fighting heart on a raid (Inspirations), and the kit the player gave them (CompanionKit: its stats as the player's gear gives them,
/// its weapon's blow against their kind's own). Put on once, as they step out (a raid's companion, a guard in a breach), under one
/// source of their own, apart from the raid's toughening.
/// </summary>
public static class CompanionPeople
{
    /// <summary>The source of these modifiers on their stat sheet.</summary>
    public static readonly LeaseKey Source = new LeaseKey("Who they are (CompanionPeople)");

    /// <summary>Makes <paramref name="who"/> fight as <paramref name="record"/> is. <paramref name="defending"/>: in a breach at home.</summary>
    public static void Apply(Companion who, ResidentRecord record, bool defending, bool onRaid)
    {
        if (who == null || record == null || !who.TryGetComponent(out Combatant combatant))
        {
            return;
        }

        StatSheet stats = combatant.Stats;
        stats.RemoveModifiersFrom(Source);
        float health = ResidentTraits.Sum(record, TraitEffectKind.FightHealth) + (defending ? ResidentTraits.Sum(record, TraitEffectKind.DefendHealth) : 0f);
        // Their own kit's weapon only where they fight as their own kind (a guard defending as their job's kind holds its weapon).
        bool ownKind = who.Definition != null && who.Definition == record.Resident.RaidCompanion;
        float damage = ResidentTalents.CompanionDamage(record, who.Definition) + ResidentTraits.Sum(record, TraitEffectKind.FightDamage)
                       + (onRaid && record.Inspiration == Inspiration.FightingHeart ? PeopleSettings.Current.FightingHeart : 0f);
        if (health != 0f)
        {
            stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, health, Source));
        }

        if (damage != 0f)
        {
            stats.AddModifier(new StatModifier(StatType.DamageBonus, StatModifierOp.Flat, damage, Source));
        }

        CompanionKit.Apply(stats, record, Source, ownKind);
        who.DodgeBonus = ResidentTalents.CompanionDodge(record) + ResidentTraits.Sum(record, TraitEffectKind.FightDodge);
        who.NightSight = Mathf.Max(0f, ResidentTraits.Sum(record, TraitEffectKind.NightSight));
        if (who.Weapon != null)
        {
            who.Weapon.DamageScale = ownKind ? CompanionKit.DamageScale(record, who.Definition) : 1f;
            who.Weapon.Against = target => ResidentTraits.DamageAgainst(record, target.Definition != null ? target.Definition.Id : target.name);
        }

        if (who.TryGetComponent(out Damageable body))
        {
            body.Heal(body.MaxHealth);
        }
    }
}
