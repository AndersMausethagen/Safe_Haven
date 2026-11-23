using System.Collections.Generic;
using UnityEngine;

/// <summary>An inspired day (Docs/PeopleResearch_2026-11-23.md §3.4, RimWorld's inspirations). Saved as numbers: add new ones at the end.</summary>
public enum Inspiration
{
    None = 0,
    /// <summary>The next piece of gear made at their station is one quality step better, and named for the day.</summary>
    Craft = 1,
    /// <summary>Half as fast again at work for the day.</summary>
    WorkFever = 2,
    /// <summary>Their next foray brings more home.</summary>
    GoodFind = 3,
    /// <summary>Along on the next raid, they hit harder.</summary>
    FightingHeart = 4,
    /// <summary>A second lesson today.</summary>
    Lesson = 5,
    /// <summary>A story told at the fire: everyone at home is cheered.</summary>
    Story = 6,
}

/// <summary>
/// Inspired days (§3.4): each dawn, someone in Bright spirits may have one, of a kind that comes from what they love (their passions:
/// a crafter makes a finer piece, a gatherer finds more, a fighter fights better), told by name. A day's kind (Work Fever, a second
/// lesson, a story) lasts the day; a kind that waits for its moment (the next piece, foray or raid) lasts until used or a few days
/// pass. Plain rules (PeopleTests).
/// </summary>
public static class Inspirations
{
    /// <summary>Dawn: inspirations that ran out end, and each one in Bright spirits may have a new one (<paramref name="roll"/> 0-1).</summary>
    public static List<ResidentRecord> Dawn(SettlementState home, System.Func<float> roll)
    {
        var inspired = new List<ResidentRecord>();
        PeopleSettings settings = PeopleSettings.Current;
        int today = home.DaysPassed;
        foreach (ResidentRecord record in home.Residents)
        {
            if (record.Inspiration != Inspiration.None && (LastsTheDay(record.Inspiration) || today - record.InspiredOn > settings.InspirationWaits))
            {
                record.Inspiration = Inspiration.None;
            }

            if (record.Inspiration != Inspiration.None || record.IsAway || ResidentSpirits.Of(record, home) != Spirits.Bright)
            {
                continue;
            }

            if (roll() < settings.InspirationChance)
            {
                record.Inspiration = Choose(record, roll());
                record.InspiredOn = today;
                inspired.Add(record);
            }
        }

        return inspired;
    }

    /// <summary>
    /// The kind an inspiration takes (<paramref name="pick"/> 0-1): a skill they love chosen by its passion (Burning five times a
    /// Keen... and a skill without passion once, as RimWorld weighs them), then what that skill does: crafting makes a piece, gathering a
    /// find, fighting a fighting heart; a teacher may give a second lesson, and anyone may tell a story.
    /// </summary>
    public static Inspiration Choose(ResidentRecord record, float pick)
    {
        var kinds = new List<(Inspiration kind, float weight)>();
        foreach (ProficiencyDefinition skill in LevelingSettings.Current.Skills)
        {
            if (skill == null)
            {
                continue;
            }

            Passion passion = ResidentTalents.PassionOf(record, skill);
            float weight = passion == Passion.Burning ? 5f : passion == Passion.Keen ? 2.5f : 0f;
            if (weight > 0f)
            {
                kinds.Add((KindFor(record, skill), weight));
            }
        }

        kinds.Add((Inspiration.WorkFever, 1f));
        kinds.Add((Inspiration.Story, 1f));
        if (record.Job != null && record.Job.Teaches != null)
        {
            kinds.Add((Inspiration.Lesson, 1f));
        }

        float total = 0f;
        foreach ((Inspiration _, float weight) in kinds)
        {
            total += weight;
        }

        float at = Mathf.Clamp01(pick) * total;
        foreach ((Inspiration kind, float weight) in kinds)
        {
            if (at < weight)
            {
                return kind;
            }

            at -= weight;
        }

        return Inspiration.WorkFever;
    }

    private static Inspiration KindFor(ResidentRecord record, ProficiencyDefinition skill)
    {
        if (skill.Training == ProficiencyTraining.Melee || skill.Training == ProficiencyTraining.Archery || skill.Training == ProficiencyTraining.Sneaking)
        {
            return Inspiration.FightingHeart;
        }

        // Their best work at the bench: only for one whose work is making things at their station (the job's orders); a gear piece
        // comes out a step finer, anything else with more of it (ResidentTalents.WorkerPiece, SettlementSystem).
        if (skill.Crafts != CraftedGoods.None && record?.Job != null && record.Job.WorkOrders.Count > 0)
        {
            return Inspiration.Craft;
        }

        return skill.Training == ProficiencyTraining.Gathering || skill.Training == ProficiencyTraining.Fishing ? Inspiration.GoodFind : Inspiration.WorkFever;
    }

    /// <summary>A kind that is over at the next dawn (the rest wait for their moment).</summary>
    public static bool LastsTheDay(Inspiration kind) => kind == Inspiration.WorkFever || kind == Inspiration.Lesson || kind == Inspiration.Story;

    /// <summary>Their work speed share from an inspired day (Work Fever).</summary>
    public static float WorkShare(ResidentRecord record) =>
        record != null && record.Inspiration == Inspiration.WorkFever ? PeopleSettings.Current.FeverWork : 0f;

    /// <summary>Uses an inspiration that waited for its moment (a piece made, a foray come home, a raid along). True when it was that kind.</summary>
    public static bool Use(ResidentRecord record, Inspiration kind)
    {
        if (record == null || record.Inspiration != kind)
        {
            return false;
        }

        record.Inspiration = Inspiration.None;
        return true;
    }

    /// <summary>What it is, said as news: "Bjarne woke inspired: his next piece will be a fine one."</summary>
    public static string News(ResidentRecord record)
    {
        string who = record.Resident.DisplayName;
        switch (record.Inspiration)
        {
            case Inspiration.Craft:
                return $"{who} woke with an idea at the bench: the next thing made there will be their best work.";
            case Inspiration.WorkFever:
                return $"{who} is in a working fever today: half as fast again.";
            case Inspiration.GoodFind:
                return $"{who} has a feeling about the woods: their next foray will bring more home.";
            case Inspiration.FightingHeart:
                return $"{who} is spoiling for a fight: along on your next raid, they hit harder.";
            case Inspiration.Lesson:
                return $"{who} has more to teach today: a second lesson, if you ask.";
            case Inspiration.Story:
                return $"{who} told a story at the fire this morning: everyone is cheered.";
            default:
                return null;
        }
    }

    public static string NameOf(Inspiration kind) => kind switch
    {
        Inspiration.Craft => "An idea at the bench",
        Inspiration.WorkFever => "A working fever",
        Inspiration.GoodFind => "A feeling about the woods",
        Inspiration.FightingHeart => "Spoiling for a fight",
        Inspiration.Lesson => "More to teach",
        Inspiration.Story => "Told a story",
        _ => "",
    };
}
