using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a person's traits and marks add up to (Docs/PeopleResearch_2026-11-23.md §3.3, §3.6): each system asks for the kinds it
/// concerns (TraitEffectKind) and nothing else knows a trait by name, so a trait is data (ResidentTraitDefinition) and a new one needs no
/// code. Traits are their written ones (ResidentDefinition › Traits); marks are what raids left on them (ResidentRecord.Marks).
/// </summary>
public static class ResidentTraits
{
    /// <summary>Their traits (written, fixed).</summary>
    public static IReadOnlyList<ResidentTraitDefinition> Of(ResidentRecord record) =>
        record?.Resident != null ? record.Resident.Traits : System.Array.Empty<ResidentTraitDefinition>();

    /// <summary>True when one of their traits or marks has an effect of <paramref name="kind"/>.</summary>
    public static bool Has(ResidentRecord record, TraitEffectKind kind)
    {
        bool found = false;
        Visit(record, (in TraitEffect effect) => found |= effect.kind == kind);
        return found;
    }

    /// <summary>
    /// The sum of their traits' and marks' effects of <paramref name="kind"/>: those for <paramref name="skill"/> and those for any skill
    /// (an effect with a skill counts only for it; asked with no skill, only effects for any skill count). No garbage: asked every frame
    /// by station speeds.
    /// </summary>
    public static float Sum(ResidentRecord record, TraitEffectKind kind, ProficiencyDefinition skill = null)
    {
        if (record?.Resident == null)
        {
            return 0f;
        }

        float total = 0f;
        IReadOnlyList<ResidentTraitDefinition> traits = record.Resident.Traits;
        for (int i = 0; i < traits.Count; i++)
        {
            if (traits[i] != null)
            {
                total += SumOf(traits[i].Effects, kind, skill);
            }
        }

        IReadOnlyList<string> marks = record.Marks;
        for (int i = 0; i < marks.Count; i++)
        {
            MarkDefinition mark = ResidentMarks.Find(marks[i]);
            if (mark != null)
            {
                total += SumOf(mark.Effects, kind, skill);
            }
        }

        return total;
    }

    private static float SumOf(IReadOnlyList<TraitEffect> effects, TraitEffectKind kind, ProficiencyDefinition skill)
    {
        float total = 0f;
        for (int i = 0; i < effects.Count; i++)
        {
            TraitEffect effect = effects[i];
            if (effect.kind == kind && (effect.skill == null || effect.skill == skill))
            {
                total += effect.amount;
            }
        }

        return total;
    }

    private delegate void EffectVisitor(in TraitEffect effect);

    // Every effect of their traits and marks, for the rarer questions (not every frame).
    private static void Visit(ResidentRecord record, EffectVisitor visit)
    {
        if (record?.Resident == null)
        {
            return;
        }

        foreach (ResidentTraitDefinition trait in record.Resident.Traits)
        {
            if (trait != null)
            {
                foreach (TraitEffect effect in trait.Effects)
                {
                    visit(effect);
                }
            }
        }

        foreach (string id in record.Marks)
        {
            MarkDefinition mark = ResidentMarks.Find(id);
            if (mark != null)
            {
                foreach (TraitEffect effect in mark.Effects)
                {
                    visit(effect);
                }
            }
        }
    }

    /// <summary>Damage against a creature whose id holds an effect's word (DamageAgainst: Troll-wary, Afraid of Widows).</summary>
    public static float DamageAgainst(ResidentRecord record, string creatureId)
    {
        if (string.IsNullOrEmpty(creatureId))
        {
            return 0f;
        }

        float total = 0f;
        Visit(record, (in TraitEffect effect) =>
        {
            if (effect.kind == TraitEffectKind.DamageAgainst && !string.IsNullOrEmpty(effect.creature)
                && creatureId.IndexOf(effect.creature, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                total += effect.amount;
            }
        });
        return total;
    }

    /// <summary>Their learning multiplier in <paramref name="skill"/> from traits (1 with none): Bookish's x1.5 in Runecraft and Alchemy.</summary>
    public static float LearningOf(ResidentRecord record, ProficiencyDefinition skill) =>
        Mathf.Max(0.1f, 1f + Sum(record, TraitEffectKind.Learning, skill) + (skill != null ? Sum(record, TraitEffectKind.Learning) : 0f));

    /// <summary>What their traits make of their own foray power in a party of <paramref name="partyCount"/>: a share (-0.1, +0.25).</summary>
    public static float ForayShare(ResidentRecord record, int partyCount)
    {
        float share = 0f;
        Visit(record, (in TraitEffect effect) =>
        {
            if (effect.kind == TraitEffectKind.ForayPowerSmall && (effect.partySize <= 0 || partyCount <= effect.partySize))
            {
                share += effect.amount;
            }
            else if (effect.kind == TraitEffectKind.ForayPowerLarge && partyCount > effect.partySize)
            {
                share += effect.amount;
            }
        });
        return share;
    }

    /// <summary>Their work speed share from traits and marks, at work with <paramref name="skill"/> (any work, and that skill's).</summary>
    public static float WorkShare(ResidentRecord record, ProficiencyDefinition skill) =>
        Sum(record, TraitEffectKind.WorkSpeed) + (skill != null ? Sum(record, TraitEffectKind.WorkSpeed, skill) : 0f);

    /// <summary>A line one of their traits gives them to say in passing, or null (their tell; ResidentAsides).</summary>
    public static string LineOf(ResidentRecord record, int pick)
    {
        var lines = new List<string>();
        foreach (ResidentTraitDefinition trait in Of(record))
        {
            if (trait != null)
            {
                foreach (string line in trait.Lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }
            }
        }

        return lines.Count > 0 ? lines[Mathf.Abs(pick) % lines.Count] : null;
    }
}
