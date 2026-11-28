using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where a skill not yet known is learned, in a line for the skill book (UX pass 2026-11-28: unknown skills shown as silhouettes with
/// "Learn from: …", so an early book reads as a list of goals; Hades II, V Rising). Read from the content: a quest's reward, a lore
/// find that teaches it, a weapon technique learned by wielding, a starting skill; anything else is won from a great foe.
/// </summary>
public static class SkillSources
{
    private static Dictionary<SkillDefinition, string> lines;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => lines = null;

    /// <summary>"Learn from: …" for a skill, as one line.</summary>
    public static string Of(SkillDefinition skill, PlayerSkillSettings settings)
    {
        if (skill == null)
        {
            return "";
        }

        lines ??= Build(settings);
        return lines.TryGetValue(skill, out string line) ? line : "Won from a great foe out in the wilds.";
    }

    /// <summary>Every skill the book can show (known or not): the content's skills, weapon arts left out (they come with the weapon in hand).</summary>
    public static List<SkillDefinition> All()
    {
        var all = new List<SkillDefinition>();
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (content == null)
        {
            return all;
        }

        foreach (SkillDefinition skill in content.Skills)
        {
            if (skill != null && skill.Kind != SkillKind.Art && !all.Contains(skill))
            {
                all.Add(skill);
            }
        }

        return all;
    }

    private static Dictionary<SkillDefinition, string> Build(PlayerSkillSettings settings)
    {
        var found = new Dictionary<SkillDefinition, string>();
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (settings != null)
        {
            foreach (SkillDefinition skill in settings.StartingSkills)
            {
                if (skill != null)
                {
                    found[skill] = "Known from the start.";
                }
            }

            foreach (SkillDefinition skill in settings.WeaponTechniques)
            {
                if (skill != null && !found.ContainsKey(skill))
                {
                    found[skill] = $"Wield {WeaponFamilies.Names(skill.Weapons)} of copper or better, and it comes to you.";
                }
            }
        }

        if (content == null)
        {
            return found;
        }

        foreach (QuestDefinition quest in content.Quests)
        {
            if (quest == null)
            {
                continue;
            }

            foreach (SkillDefinition skill in quest.RewardSkills)
            {
                if (skill != null && !found.ContainsKey(skill))
                {
                    found[skill] = $"The reward of \"{quest.DisplayName}\".";
                }
            }
        }

        foreach (ItemDefinition item in content.Items)
        {
            if (item != null && item.TeachesSkill != null && !found.ContainsKey(item.TeachesSkill))
            {
                found[item.TeachesSkill] = $"Read from {item.DisplayName}, a rare find in the raids.";
            }
        }

        return found;
    }
}
