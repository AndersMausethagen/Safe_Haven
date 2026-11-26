using System;
using UnityEngine;

/// <summary>
/// What a raid event's choice needs beyond an item cost (Docs/RaidEventsPlan_2026-11-26.md E2): a skill, a thing carried, someone along,
/// the hour, something known. A choice whose needs aren't met still shows, greyed, with the reason, so it is something to work toward.
/// Authored in the choice's Requires list (SerializeReference). To add a kind: derive with a [Serializable] class and a parameterless
/// constructor.
/// </summary>
[Serializable]
public abstract class ChoiceRequirement
{
    /// <summary>The name in the Content Editor's menu.</summary>
    public abstract string DisplayName { get; }

    /// <summary>True when the player meets it, in this raid.</summary>
    public abstract bool IsMet(GameObject player, RaidSession session);

    /// <summary>What the greyed choice says: "Needs Woodcutting 20".</summary>
    public abstract string Reason { get; }
}

/// <summary>A skill at a level or above.</summary>
[Serializable]
public sealed class SkillAtLeast : ChoiceRequirement
{
    [SerializeField] private ProficiencyDefinition skill;
    [SerializeField, Min(1)] private int level = 20;

    public SkillAtLeast() { }

    public SkillAtLeast(ProficiencyDefinition skill, int level)
    {
        this.skill = skill;
        this.level = level;
    }

    public override string DisplayName => "Skill At Least";
    public override string Reason => $"Needs {(skill != null ? skill.DisplayName : "a skill")} {level}";

    public override bool IsMet(GameObject player, RaidSession session) =>
        skill != null && GameSession.Progress.LevelOf(skill, LevelingSettings.Current) >= level;
}

/// <summary>An item in the bag or worn (a pickaxe, a troll trophy), not spent.</summary>
[Serializable]
public sealed class CarriesItem : ChoiceRequirement
{
    [Tooltip("Any one of these will do (a copper or an iron pickaxe).")]
    [SerializeField] private ItemDefinition[] anyOf = Array.Empty<ItemDefinition>();
    [Tooltip("What the reason calls it: \"a pickaxe\".")]
    [SerializeField] private string called = "";

    public CarriesItem() { }

    public CarriesItem(string called, params ItemDefinition[] anyOf)
    {
        this.called = called;
        this.anyOf = anyOf;
    }

    public override string DisplayName => "Carries Item";
    public override string Reason => $"Needs {(string.IsNullOrEmpty(called) ? "the right thing" : called)}";

    public override bool IsMet(GameObject player, RaidSession session)
    {
        ItemInventory bag = player != null ? player.GetComponentInParent<ItemInventory>() : null;
        PlayerEquipment worn = player != null ? player.GetComponentInParent<PlayerEquipment>() : null;
        foreach (ItemDefinition item in anyOf)
        {
            if (item == null)
            {
                continue;
            }

            if (bag != null && bag.CountCarried(item) > 0)
            {
                return true;
            }

            if (worn != null && worn.Gear != null && worn.Gear.Slots.Count(item) > 0)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Someone along: a named resident, or anyone.</summary>
[Serializable]
public sealed class CompanionAlong : ChoiceRequirement
{
    [Tooltip("Empty: anyone along will do.")]
    [SerializeField] private ResidentDefinition who;

    public CompanionAlong() { }

    public CompanionAlong(ResidentDefinition who) => this.who = who;

    public override string DisplayName => "Companion Along";
    public override string Reason => who != null ? $"Needs {who.DisplayName} along" : "Needs someone along";

    public override bool IsMet(GameObject player, RaidSession session)
    {
        Companion along = RaidDirector.Current != null ? RaidDirector.Current.Companions.Companion : null;
        return along != null && !along.IsLost && !along.IsDowned && (who == null || along.Resident == who);
    }
}

/// <summary>The run's hour: only at night (dusk and night), or only by day.</summary>
[Serializable]
public sealed class HourIs : ChoiceRequirement
{
    [SerializeField] private bool dark = true;

    public HourIs() { }

    public HourIs(bool dark) => this.dark = dark;

    public override string DisplayName => "Hour Is";
    public override string Reason => dark ? "Only after dark" : "Only by day";

    public override bool IsMet(GameObject player, RaidSession session)
    {
        RaidTimeOfDay hour = session != null ? session.TimeOfDay : null;
        return hour != null && hour.IsDark == dark;
    }
}

/// <summary>Something the character knows: a mark in the journal (a plan read, a place seen, a choice made before).</summary>
[Serializable]
public sealed class KnowsMark : ChoiceRequirement
{
    [Tooltip("The journal's noticed mark.")]
    [SerializeField] private string mark = "";
    [Tooltip("What the reason says is needed: \"to know the runes\".")]
    [SerializeField] private string called = "";

    public KnowsMark() { }

    public KnowsMark(string mark, string called)
    {
        this.mark = mark;
        this.called = called;
    }

    public override string DisplayName => "Knows Mark";
    public override string Reason => $"Needs {(string.IsNullOrEmpty(called) ? "to know more" : called)}";

    public override bool IsMet(GameObject player, RaidSession session) =>
        !string.IsNullOrEmpty(mark) && GameSession.Progress.Journal.HasNoticed(mark);
}
