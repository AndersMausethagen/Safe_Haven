using System;
using UnityEngine;

/// <summary>Where a habit takes someone in their free time (TraitHabit). Saved as numbers in trait assets: add new ones at the end.</summary>
public enum HabitPlace
{
    /// <summary>The edge of the Haven, past their own home from the portal: alone.</summary>
    Edge = 0,
    /// <summary>The nearest standing stone (an offering stone): to read or to sit.</summary>
    Stones = 1,
    /// <summary>The still water's shore.</summary>
    Water = 2,
    /// <summary>The lanes between the houses, door to door, as the hours pass.</summary>
    Lanes = 3,
    /// <summary>The nearest guard post (or the edge with none): an eye on the dark.</summary>
    Watch = 4,
}

/// <summary>A trait's habit (Docs/PeopleSystemReview_2026-11-23.md §1.2): in its hours, their free time (and their meal, if it says) is spent
/// at its place rather than at the fire.</summary>
[Serializable]
public struct TraitHabit
{
    public HabitPlace place;
    [Range(0f, 24f)] public float from;
    [Range(0f, 24f)] public float until;
    [Tooltip("Their meal too (a loner eats apart), not only their free time.")]
    public bool atMeals;

    public TraitHabit(HabitPlace place, float from, float until, bool atMeals = false)
    {
        this.place = place;
        this.from = from;
        this.until = until;
        this.atMeals = atMeals;
    }

    /// <summary>True at <paramref name="hour"/> (a span past midnight, 22 to 2, counts too).</summary>
    public bool At(float hour) => from <= until ? hour >= from && hour < until : hour >= from || hour < until;
}

/// <summary>
/// Habits (§1.2): who they are shown in where they go, not only in what they say. Each trait may hold habits (ResidentTraitDefinition ›
/// Habits); ResidentLife asks which one applies now and walks them there. Plain rules (PeopleTests).
/// </summary>
public static class PeopleHabits
{
    /// <summary>The habit of theirs that applies at <paramref name="hour"/> for an activity (free time at the fire, or a meal), if any:
    /// the first of their traits' habits that does.</summary>
    public static bool Now(ResidentRecord record, float hour, ResidentActivity activity, out TraitHabit habit)
    {
        habit = default;
        if (record?.Resident == null || (activity != ResidentActivity.Fire && activity != ResidentActivity.Meal))
        {
            return false;
        }

        foreach (ResidentTraitDefinition trait in record.Resident.Traits)
        {
            if (trait == null)
            {
                continue;
            }

            foreach (TraitHabit candidate in trait.Habits)
            {
                if (candidate.At(hour) && (activity == ResidentActivity.Fire || candidate.atMeals))
                {
                    habit = candidate;
                    return true;
                }
            }
        }

        return false;
    }
}
