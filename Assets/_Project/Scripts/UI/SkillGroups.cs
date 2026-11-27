using System.Collections.Generic;
using UnityEngine;

/// <summary>One section of the skill window's book: its heading, a short line beside it, its colour, and its skills in order.</summary>
public sealed class SkillGroup
{
    public string Name;
    public string Note;
    /// <summary>The heading's colour: an element's own, or none (the accent) for a type.</summary>
    public Color? Colour;
    public readonly List<SkillDefinition> Skills = new List<SkillDefinition>();
}

/// <summary>
/// How the skill window sorts the known skills into sections (the user, 2026-11-27: "some grouping of the skills in order of type or
/// element"): by type in the order the bar's slots go (Ultimates, Powers, Techniques, Weapon Arts), each by element within; or by
/// element (Steel, Beast, Frost, Ember, Wyrd), each by type within; names alphabetical last. Empty sections are left out.
/// </summary>
public static class SkillGroups
{
    private static readonly SkillKind[] KindOrder = { SkillKind.Ultimate, SkillKind.Power, SkillKind.Technique, SkillKind.Art };

    public static List<SkillGroup> Of(IReadOnlyList<SkillDefinition> known, bool byElement)
    {
        var groups = new List<SkillGroup>();
        if (known == null)
        {
            return groups;
        }

        if (byElement)
        {
            foreach (SkillSchool school in System.Enum.GetValues(typeof(SkillSchool)))
            {
                Add(groups, known, skill => skill.School == school, school.ToString(), SkillSchools.Explain(school), SkillSchools.ColorOf(school));
            }
        }
        else
        {
            foreach (SkillKind kind in KindOrder)
            {
                Add(groups, known, skill => skill.Kind == kind, Plural(kind), SkillKinds.Explain(kind), null);
            }
        }

        return groups;
    }

    private static void Add(List<SkillGroup> groups, IReadOnlyList<SkillDefinition> known, System.Predicate<SkillDefinition> belongs,
        string name, string note, Color? colour)
    {
        var group = new SkillGroup { Name = name, Note = note, Colour = colour };
        foreach (SkillDefinition skill in known)
        {
            if (skill != null && belongs(skill) && !group.Skills.Contains(skill))
            {
                group.Skills.Add(skill);
            }
        }

        if (group.Skills.Count == 0)
        {
            return;
        }

        group.Skills.Sort(Compare);
        groups.Add(group);
    }

    // Within a section: by type, then by element, then by name (a section holds one type or one element, so one of the first two ties).
    private static int Compare(SkillDefinition a, SkillDefinition b)
    {
        int kind = System.Array.IndexOf(KindOrder, a.Kind).CompareTo(System.Array.IndexOf(KindOrder, b.Kind));
        if (kind != 0)
        {
            return kind;
        }

        int school = ((int)a.School).CompareTo((int)b.School);
        return school != 0 ? school : string.CompareOrdinal(a.DisplayName, b.DisplayName);
    }

    private static string Plural(SkillKind kind) => kind switch
    {
        SkillKind.Ultimate => "Ultimates",
        SkillKind.Technique => "Techniques",
        SkillKind.Art => "Weapon Arts",
        _ => "Powers",
    };
}
