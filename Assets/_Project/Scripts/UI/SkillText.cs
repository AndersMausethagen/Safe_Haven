using System.Text;
using UnityEngine;

/// <summary>How a skill is described to the player, shared by the skill window's tooltip and anything else that names one.</summary>
public static class SkillText
{
    public static string Describe(SkillDefinition skill, int equippedSlot)
    {
        var builder = new StringBuilder();
        builder.Append(SkillSchools.Rich(skill.School))
            .Append($"  <color={UIStyle.Current.MutedHex}>Cooldown {PlayerSkills.CooldownOf(skill):0.#}s  ·  Stamina {skill.StaminaCost:0}</color>");
        int rank = GameSession.Progress.RankOf(skill);
        if (rank > 0)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Deepened to rank {AltarWork.Roman(rank)}: {(PlayerSkills.Power(skill) - 1f) * 100f:+0}% power.</color>");
        }

        if (skill.LungeDistance > 0f)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Lunges {skill.LungeDistance:0.#} m toward the cursor.</color>");
        }

        AttackDefinition attack = skill.Attack;
        if (attack != null)
        {
            builder.Append($"\n<color={UIStyle.Current.DangerHex}>Deals ");
            bool hasBase = attack.BaseDamage > 0f;
            if (hasBase)
            {
                builder.Append($"{attack.BaseDamage:0} {attack.DamageType}");
            }

            if (attack.WeaponDamageRatio > 0f)
            {
                builder.Append(hasBase ? " + " : "").Append($"{attack.WeaponDamageRatio * 100f:0}% weapon");
            }

            string area = attack.Arc >= 359f ? $"all around you within {attack.Range:0.#} m" : $"in a {attack.Arc:0}° arc, {attack.Range:0.#} m ahead";
            builder.Append($" damage {area}.</color>");
        }

        // What it leaves on foes, in the statuses' own words and numbers (Docs/SkillsImprovementResearch_2026-11-24.md 8).
        if (attack != null && attack.AppliesStatus != null)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>{Keyword(attack.AppliesStatus)}</color>");
            if (attack.OnRepeatStatus != null)
            {
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>Again on one already {attack.AppliesStatus.DisplayName.ToLowerInvariant()}: {Keyword(attack.OnRepeatStatus)}</color>");
            }
        }

        if (skill.FoesStatus != null)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Foes within {skill.FoesRadius:0.#} m: {Keyword(skill.FoesStatus)}</color>");
        }

        if (attack != null)
        {
            builder.Append($"\n<color={UIStyle.Current.MutedHex}>{(skill.IsPhysical ? "A heavy blow: Melee trains, the frozen shatter." : "A spell: Runecraft trains and strengthens it.")}</color>");
        }

        if (skill.Heal > 0f)
        {
            builder.Append($"\n<color={UIStyle.Current.GoodHex}>Restores {skill.Heal:0} health.</color>");
        }

        if (skill.HasBuffs)
        {
            foreach (SkillStatBuff buff in skill.Buffs)
            {
                string amount = buff.op == StatModifierOp.Percent ? $"{buff.value * 100f:+0;-0}%" : $"{buff.value:+0.##;-0.##}";
                builder.Append($"\n<color={UIStyle.Current.GoodHex}>{amount} {Spaced(buff.stat.ToString())} for {skill.BuffDuration:0.#}s.</color>");
            }
        }

        if (!string.IsNullOrWhiteSpace(skill.Description))
        {
            builder.Append("\n\n").Append(skill.Description);
        }

        builder.Append(equippedSlot >= 0
            ? $"\n\n<color={UIStyle.Current.AccentHex}>On the skill bar: {PlayerInputHandler.SkillKeyLabel(equippedSlot)}</color>"
            : $"\n\n<color={UIStyle.Current.MutedHex}>Drag onto the skill bar, or right-click to slot it.</color>");
        return builder.ToString();
    }

    /// <summary>A status in a few words with its numbers: "Sundered (8 s): takes 15% more".</summary>
    public static string Keyword(StatusEffectDefinition status)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (status.DamagePerSecond > 0f) parts.Add($"{status.DamagePerSecond:0.#} {status.DamageType} a second");
        if (status.DamageTaken > 1f) parts.Add($"takes {(status.DamageTaken - 1f) * 100f:0}% more");
        if (status.DamageDealt < 1f) parts.Add($"deals {(1f - status.DamageDealt) * 100f:0}% less");
        if (status.MoveSpeed <= 0f) parts.Add("held fast");
        else if (status.MoveSpeed < 1f) parts.Add($"{(1f - status.MoveSpeed) * 100f:0}% slower");
        if (status.StopsStrikes) parts.Add("strikes nothing");
        if (status.ConsumedBy != HitTechnique.None && status.ConsumeDamage > 0f) parts.Add("a heavy bursts it");
        return $"{status.DisplayName} ({status.Seconds:0.#} s){(parts.Count > 0 ? ": " + string.Join(", ", parts) : "")}";
    }

    private static string Spaced(string name)
    {
        var builder = new StringBuilder(name.Length + 4);
        for (int i = 0; i < name.Length; i++)
        {
            if (i > 0 && char.IsUpper(name[i]))
            {
                builder.Append(' ');
            }

            builder.Append(i > 0 ? char.ToLowerInvariant(name[i]) : name[i]);
        }

        return builder.ToString();
    }
}
