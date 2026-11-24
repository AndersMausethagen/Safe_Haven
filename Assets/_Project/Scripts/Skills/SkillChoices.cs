using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What an ability's chosen altar options (SkillOption) add up to, for the player's game: its damage, cooldown, cost and buff scales,
/// the heal, the ground and the reach, and the options themselves (for the statuses they lay and use up). Kept per ability and made
/// again when a choice or a rank changes (CharacterProgress.ChoiceVersion), since cooldowns are read every frame.
/// </summary>
public static class SkillChoices
{
    public sealed class Totals
    {
        public float Damage = 1f;
        public float Cooldown = 1f;
        public float Cost = 1f;
        public float Buff = 1f;
        public float Heal;
        public float FoesRadius = 1f;
        public int ExtraLeaves;
        public readonly List<SkillOption> Chosen = new List<SkillOption>();
        public int Version = -1;
        public CharacterProgress Of;
    }

    private static readonly Dictionary<SkillDefinition, Totals> Cache = new Dictionary<SkillDefinition, Totals>();
    private static readonly Totals None = new Totals();

    /// <summary>The totals for <paramref name="skill"/> as the player has it now; nothing chosen gives ones and zeros.</summary>
    public static Totals For(SkillDefinition skill)
    {
        CharacterProgress progress = GameSession.Progress;
        if (skill == null || progress == null || skill.Options.Count == 0)
        {
            return None;
        }

        if (!Cache.TryGetValue(skill, out Totals totals))
        {
            Cache[skill] = totals = new Totals();
        }

        if (totals.Of == progress && totals.Version == progress.ChoiceVersion)
        {
            return totals;
        }

        totals.Version = progress.ChoiceVersion;
        totals.Of = progress;
        totals.Damage = totals.Cooldown = totals.Cost = totals.Buff = totals.FoesRadius = 1f;
        totals.Heal = 0f;
        totals.ExtraLeaves = 0;
        totals.Chosen.Clear();
        foreach (SkillOption option in progress.ChosenOptions(skill))
        {
            totals.Chosen.Add(option);
            totals.Damage *= option.damageScale;
            totals.Cooldown *= option.cooldownScale;
            totals.Cost *= option.costScale;
            totals.Buff *= option.buffScale;
            totals.FoesRadius *= option.foesRadiusScale;
            totals.Heal += option.heal;
            totals.ExtraLeaves += option.extraLeaves;
        }

        return totals;
    }

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Cache.Clear();
}
