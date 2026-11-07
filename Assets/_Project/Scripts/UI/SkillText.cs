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
