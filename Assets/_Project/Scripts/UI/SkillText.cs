using System.Text;
using UnityEngine;

/// <summary>How a skill is described to the player, shared by the skill window's tooltip and anything else that names one.</summary>
public static class SkillText
{
    /// <summary>
    /// The art a Weapon's Art or Rune Art card stands for right now (the user, 2026-11-25: "weapon art should change description based
    /// on which weapon we are holding"): the held weapon's own art, or the art of the rune attuned to its kind. Null for any other
    /// skill, with nothing in hand, or with no rune attuned; <paramref name="weapon"/> is what is held.
    /// </summary>
    public static AttackDefinition HeldArt(SkillDefinition skill, out WeaponDefinition weapon)
    {
        weapon = null;
        if (skill == null || skill.WeaponArt == WeaponArtSlot.None || Player.Current == null
            || !Player.Current.TryGetComponent(out PlayerCombat combat))
        {
            return null;
        }

        weapon = combat.Weapon;
        if (weapon == null)
        {
            return null;
        }

        if (skill.WeaponArt == WeaponArtSlot.Own)
        {
            return weapon.WeaponArt;
        }

        ItemDefinition rune = ArtRunes.For(weapon);
        return rune != null ? rune.RuneArt : null;
    }

    /// <summary>The card's title: an art card takes the name of the art it stands for now.</summary>
    public static string Name(SkillDefinition skill)
    {
        AttackDefinition art = HeldArt(skill, out _);
        return art != null ? art.DisplayName : skill.DisplayName;
    }

    public static string Describe(SkillDefinition skill, int equippedSlot)
    {
        var builder = new StringBuilder();
        builder.Append(SkillSchools.Rich(skill.School)).Append($"  <color={UIStyle.Current.MutedHex}>{Costs(skill)}</color>");
        AttackDefinition art = HeldArt(skill, out WeaponDefinition held);
        if (skill.WeaponArt != WeaponArtSlot.None)
        {
            string whose = held == null ? "Take a weapon in hand to see its art."
                : art != null ? $"The art of your {held.DisplayName}."
                : skill.WeaponArt == WeaponArtSlot.Rune ? $"No art rune is attuned to weapons like your {held.DisplayName}."
                : $"Your {held.DisplayName} has no art.";
            builder.Append($"\n<color={UIStyle.Current.TextHex}>{whose}</color>");
            string motion = art != null ? MotionText(art) : null;
            if (motion != null)
            {
                builder.Append($"\n<color={UIStyle.Current.AccentHex}>{motion}</color>");
            }
        }
        int rank = GameSession.Progress.RankOf(skill);
        if (rank > 0)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Deepened to rank {AltarWork.Roman(rank)}: {(PlayerSkills.Power(skill) - 1f) * 100f:+0}% power.</color>");
        }

        foreach (SkillOption option in SkillChoices.For(skill).Chosen)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>{option.displayName}: {option.description}</color>");
        }

        if (skill.LungeDistance > 0f)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Lunges toward the cursor, as far as you aim: up to {skill.LungeDistance:0.#} m.</color>");
        }

        AttackDefinition attack = skill.Attack != null ? skill.Attack : art;
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

            string area = attack.Motion == ArtMotion.Throw ? "to all it meets in its flight"
                : attack.CastProjectile != null ? "where it strikes"
                : attack.Arc >= 359f ? $"all around you within {attack.Range:0.#} m" : $"in a {attack.Arc:0}° arc, {attack.Range:0.#} m ahead";
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
            builder.Append($"\n<color={UIStyle.Current.MutedHex}>{(skill.IsPhysical ? "A heavy blow: it shatters frozen foes and trains Melee." : "A spell: Runecraft trains and strengthens it.")}</color>");
        }

        if (skill.WeaponArt != WeaponArtSlot.None)
        {
            builder.Append($"\n<color={UIStyle.Current.AccentHex}>Press again just after it lands to strike it again, free (twice).</color>");
        }

        if (skill.Heal > 0f)
        {
            builder.Append($"\n<color={UIStyle.Current.GoodHex}>Restores {skill.Heal:0} health.</color>");
        }

        if (skill.HasBuffs)
        {
            // In the stats' own names and units, as gear shows them: "+20% Physical Resistance", not "+0.2 Physical resistance".
            var gives = new System.Collections.Generic.List<string>();
            foreach (SkillStatBuff buff in skill.Buffs)
            {
                gives.Add(new StatBonus(buff.stat, buff.value, buff.op).Describe());
            }

            builder.Append($"\n<color={UIStyle.Current.GoodHex}>{skill.BuffName} ({skill.BuffDuration:0.#} s): {string.Join(", ", gives)}.</color>");
        }

        string words = art != null && !string.IsNullOrWhiteSpace(art.Description) ? art.Description : skill.Description;
        if (!string.IsNullOrWhiteSpace(words))
        {
            builder.Append("\n\n").Append(words);
        }

        builder.Append(equippedSlot >= 0
            ? $"\n\n<color={UIStyle.Current.AccentHex}>On the skill bar: {PlayerInputHandler.SkillKeyLabel(equippedSlot)}</color>"
            : $"\n\n<color={UIStyle.Current.MutedHex}>Drag onto the skill bar, or right-click to slot it.</color>");
        return builder.ToString();
    }

    // How a weapon art moves the player, in its own numbers. Null for one that stands still.
    private static string MotionText(AttackDefinition art)
    {
        float distance = art.MotionDistance;
        switch (art.Motion)
        {
            case ArtMotion.DashThrough: return $"Dashes {distance:0.#} m through foes, striking all it passes.";
            case ArtMotion.BlinkBehind: return $"Steps behind the foe in focus, up to {distance:0.#} m away, then strikes.";
            case ArtMotion.LeapTo: return $"Leaps up to {distance:0.#} m onto the foe in focus.";
            case ArtMotion.Throw: return $"Thrown up to {distance:0.#} m, it comes back to the hand.";
        }

        if (art.CastProjectile != null)
        {
            return art.CastHazard != null ? $"Cast out to {art.CastRange:0.#} m; the ground holds where it lands." : $"Cast out to {art.CastRange:0.#} m.";
        }

        return null;
    }

    // What it takes to cast: its cooldown (an art's is the arts' own), its stamina, and the Focus circles it spends.
    private static string Costs(SkillDefinition skill)
    {
        bool art = skill.WeaponArt != WeaponArtSlot.None;
        var parts = new System.Collections.Generic.List<string>
        {
            $"Cooldown {(art ? CombatTechniqueSettings.Current.ArtCooldown : PlayerSkills.CooldownOf(skill)):0.#}s",
        };
        if (!art && skill.StaminaCost > 0f) parts.Add($"Stamina {skill.StaminaCost:0}");
        int circles = art ? 1 : skill.FocusCircles;
        if (circles > 0) parts.Add(circles == 1 ? "1 Focus circle" : $"{circles} Focus circles");
        return string.Join("  ·  ", parts);
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
}
