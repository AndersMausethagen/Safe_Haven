using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a run can do to the player beyond plain blows (Docs/PlayerStatsResearch.md #8-9), read off the raid's data so it is always true:
/// fire, frost and blight from the creatures that can be met at the tier (their blows, what their blows lay on, the ground they leave),
/// and the season's cold. The raid planner names them under the known dangers and says what answers them (a meal family, a
/// resistance), so a resistance piece or a Warding meal becomes a packing choice. Plain C#.
/// </summary>
public static class RaidHarms
{
    /// <summary>One kind of harm and the creatures that deal it.</summary>
    public readonly struct Harm
    {
        public readonly DamageType Type;
        public readonly IReadOnlyList<string> From;

        public Harm(DamageType type, IReadOnlyList<string> from)
        {
            Type = type;
            From = from;
        }
    }

    // Below this the season's cold (PlayerWarmth.ColdNow) is a mild day: not worth packing for.
    private const float NotableCold = 0.25f;

    private static readonly DamageType[] Kinds = { DamageType.Fire, DamageType.Frost, DamageType.Holy };

    /// <summary>The kinds of harm the creatures of <paramref name="raid"/> deal at <paramref name="tier"/>, fire first.</summary>
    public static List<Harm> For(RaidDefinition raid, int tier)
    {
        var from = new Dictionary<DamageType, List<string>>();
        if (raid != null && raid.KnownEnemies != null)
        {
            foreach (MonsterDefinition monster in raid.KnownEnemies)
            {
                if (monster == null || !raid.CanMeet(monster, tier))
                {
                    continue;
                }

                foreach (MonsterAttack attack in monster.Attacks)
                {
                    if (attack == null || attack.fromTier > tier)
                    {
                        continue;
                    }

                    Note(from, monster, attack.attack != null ? attack.attack.DamageType : DamageType.Physical);
                    Note(from, monster, attack.attack != null && attack.attack.AppliesStatus != null ? attack.attack.AppliesStatus.DamageType : DamageType.Physical);
                    Note(from, monster, attack.foeStatus != null ? attack.foeStatus.DamageType : DamageType.Physical);
                    GroundHazard hazard = attack.hazardPrefab != null ? attack.hazardPrefab.GetComponentInChildren<GroundHazard>(true) : null;
                    Note(from, monster, hazard != null ? hazard.Harm : DamageType.Physical);
                }
            }
        }

        // Elites the tier brings carry harms of their own (Frostbound chills): named "elites", from the affixes open at this tier.
        bool elites = false;
        foreach (RaidDifficulty difficulty in raid != null ? raid.Difficulties : new List<RaidDifficulty>())
        {
            elites |= difficulty != null && difficulty.tier == tier && difficulty.eliteCount.y > 0;
        }

        foreach (EliteAffixDefinition affix in elites ? raid.EliteAffixes : new List<EliteAffixDefinition>())
        {
            if (affix == null || affix.FromTier > tier)
            {
                continue;
            }

            foreach (EliteBehaviour behaviour in affix.Behaviours)
            {
                if (behaviour is StatusOnHitBehaviour onHit && onHit.Effect != null)
                {
                    Note(from, "elites", onHit.Effect.DamageType);
                }
            }
        }

        var harms = new List<Harm>();
        foreach (DamageType kind in Kinds)
        {
            if (from.TryGetValue(kind, out List<string> names))
            {
                harms.Add(new Harm(kind, names));
            }
        }

        return harms;
    }

    /// <summary>Whether the season is cold enough out of doors that the chill (PlayerWarmth) is worth packing for.</summary>
    public static bool IsCold(float coldNow) => coldNow >= NotableCold;

    /// <summary>
    /// The planner's line: what hurts beyond blows and what answers it, or which food suits a run of plain blows. Empty when there is
    /// nothing to say.
    /// </summary>
    public static string Describe(IReadOnlyList<Harm> harms, bool cold, string mutedHex)
    {
        var parts = new List<string>();
        var answers = new List<string>();
        foreach (Harm harm in harms)
        {
            parts.Add($"{NameOf(harm.Type)} <color={mutedHex}>({string.Join(", ", harm.From)})</color>");
            answers.Add(StatNames.Of(ResistanceOf(harm.Type)).Replace(" Resistance", ""));
        }

        if (cold)
        {
            parts.Add("the cold");
            if (!answers.Contains("Frost"))
            {
                answers.Add("Frost");
            }
        }

        if (parts.Count == 0)
        {
            return $"<color={mutedHex}>Nothing out there burns, chills or poisons: Hearty or Fierce food.</color>";
        }

        return $"Harms: {string.Join("  ·  ", parts)}\n<color={mutedHex}>Answered by Warding food or {string.Join(" or ", answers)} Resistance.</color>";
    }

    /// <summary>A harm's name as the player reads it: Holy only ever came from poison, so it reads Blight.</summary>
    public static string NameOf(DamageType type) => type == DamageType.Holy ? "Blight" : type.ToString();

    /// <summary>The resistance that answers a harm.</summary>
    public static StatType ResistanceOf(DamageType type) => type switch
    {
        DamageType.Fire => StatType.FireResistance,
        DamageType.Frost => StatType.FrostResistance,
        DamageType.Holy => StatType.HolyResistance,
        _ => StatType.PhysicalResistance,
    };

    private static void Note(Dictionary<DamageType, List<string>> from, MonsterDefinition monster, DamageType type) =>
        Note(from, monster.DisplayName, type);

    private static void Note(Dictionary<DamageType, List<string>> from, string who, DamageType type)
    {
        if (type == DamageType.Physical)
        {
            return;
        }

        if (!from.TryGetValue(type, out List<string> names))
        {
            from[type] = names = new List<string>();
        }

        if (!names.Contains(who))
        {
            names.Add(who);
        }
    }
}
