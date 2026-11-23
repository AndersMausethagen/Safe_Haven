using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How much a resident loves a skill (Docs/PeopleResearch_2026-11-23.md §3.2, RimWorld's passion): they learn it faster, are a
/// little quicker and happier at it, and their inspired days come from it. Saved as numbers: add new ones at the end.</summary>
public enum Passion
{
    None = 0,
    /// <summary>One flame.</summary>
    Keen = 1,
    /// <summary>Two flames.</summary>
    Burning = 2,
}

/// <summary>Where a written resident starts in one skill (ResidentDefinition › Talents): who they were before the forest.</summary>
[Serializable]
public struct TalentStart
{
    public ProficiencyDefinition skill;
    [Tooltip("Their level in it when they come home (the player's scale: 10 is ordinary).")]
    [Range(1, 50)] public int level;
    public Passion passion;

    public TalentStart(ProficiencyDefinition skill, int level, Passion passion)
    {
        this.skill = skill;
        this.level = level;
        this.passion = passion;
    }
}

/// <summary>
/// One person's talents: their experience in each skill (by the skill's id, on the player's curve, LevelingSettings) and their
/// passions. Kept by their ResidentRecord while they live here, and by their FallenRecord while the forest has them, so who they are
/// comes back with them (only the player is forgotten). Every change goes through <see cref="changing"/>, as a record's own fields
/// do (the co-op guard: SettlementState.RecordChanging).
/// </summary>
public sealed class TalentSheet
{
    private readonly Dictionary<string, float> experience = new Dictionary<string, float>();
    private readonly Dictionary<string, int> passions = new Dictionary<string, int>();
    private int picksOwed;

    [NotSaved("a day's practice for the daily cap: lost on a load, at worst one more day's worth comes in")]
    private int practisedOn = -1;
    [NotSaved("a day's practice for the daily cap: lost on a load, at worst one more day's worth comes in")]
    private readonly Dictionary<string, float> practisedToday = new Dictionary<string, float>();
    [NotSaved("who is told of a change: set by the owner")]
    private readonly Action<string> changing;

    // What a change is called to the co-op guard (SettlementState.RecordChanging).
    private const string ChangeName = "Talents";

    public TalentSheet(Action<string> changing = null)
    {
        this.changing = changing;
    }

    /// <summary>True once their experience has been set (written start, a save, or carried from the forest).</summary>
    public bool IsSet => experience.Count > 0;

    /// <summary>Passion picks the player has yet to make for them (a bond rank reached: ResidentTalents.OwePick).</summary>
    public int PicksOwed => picksOwed;

    public IReadOnlyDictionary<string, float> Experience => experience;
    public IReadOnlyDictionary<string, int> Passions => passions;

    public float ExperienceOf(string skillId) => skillId != null && experience.TryGetValue(skillId, out float had) ? had : 0f;

    public Passion PassionOf(string skillId) => skillId != null && passions.TryGetValue(skillId, out int had) ? (Passion)had : Passion.None;

    public void SetExperience(string skillId, float amount)
    {
        if (string.IsNullOrEmpty(skillId))
        {
            return;
        }

        changing?.Invoke(ChangeName);
        experience[skillId] = Mathf.Max(0f, amount);
    }

    public void SetPassion(string skillId, Passion passion)
    {
        if (string.IsNullOrEmpty(skillId))
        {
            return;
        }

        changing?.Invoke(ChangeName);
        if (passion == Passion.None)
        {
            passions.Remove(skillId);
        }
        else
        {
            passions[skillId] = (int)passion;
        }
    }

    public void SetPicksOwed(int owed)
    {
        changing?.Invoke(ChangeName);
        picksOwed = Mathf.Max(0, owed);
    }

    /// <summary>Practice already counted today in a skill (for the daily cap), and that much more on <paramref name="day"/>.</summary>
    public float PractisedToday(string skillId, int day)
    {
        if (day != practisedOn)
        {
            practisedToday.Clear();
            practisedOn = day;
        }

        return practisedToday.TryGetValue(skillId, out float had) ? had : 0f;
    }

    public void NotePractised(string skillId, int day, float amount)
    {
        PractisedToday(skillId, day);
        practisedToday[skillId] = (practisedToday.TryGetValue(skillId, out float had) ? had : 0f) + amount;
    }

    /// <summary>Takes on everything of <paramref name="other"/> (lost and found: who they are comes back with them).</summary>
    public void CopyFrom(TalentSheet other)
    {
        if (other == null)
        {
            return;
        }

        changing?.Invoke(ChangeName);
        experience.Clear();
        passions.Clear();
        foreach (KeyValuePair<string, float> pair in other.experience)
        {
            experience[pair.Key] = pair.Value;
        }

        foreach (KeyValuePair<string, int> pair in other.passions)
        {
            passions[pair.Key] = pair.Value;
        }

        picksOwed = other.picksOwed;
    }
}
