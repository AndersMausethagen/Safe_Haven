using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

// Content Editor pages for Safe Haven as a place: the people who can live there, the jobs they take and the ward breaches
// that test it. Their balance numbers (purification, ward size) are tuned on the Systems Editor's World pages.

/// <summary>People who can be rescued and live in Safe Haven (GDD §15).</summary>
public sealed class ResidentSection : ContentSection
{
    public override string Title => "Residents";
    public override string Noun => "Resident";
    public override string SidebarGroup => "Settlement";
    public override Type AssetType => typeof(ResidentDefinition);
    public override string Folder => "Assets/_Project/Data/Settlement";
    public override string Help =>
        "Someone who can be found in a raid and brought home: their name, lines, model and the jobs they can take. Put a " +
        "StrandedTraveler prefab in a raid scene and point its Rescuable Traveler at the resident to make them findable.";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var resident = (ResidentDefinition)asset;
        if (resident.Jobs == null || resident.Jobs.Length == 0)
        {
            problems.Add("Has no jobs, so they can't be given work at home.");
        }

        if (resident.Model == null)
        {
            problems.Add("Has no model, so they are invisible at home.");
        }

        return problems;
    }
}

/// <summary>Jobs residents can hold (GDD §15.3).</summary>
public sealed class JobSection : ContentSection
{
    public override string Title => "Jobs";
    public override string Noun => "Job";
    public override string SidebarGroup => "Settlement";
    public override Type AssetType => typeof(JobDefinition);
    public override string Folder => "Assets/_Project/Data/Settlement";
    public override string Help =>
        "Work a resident can do. Giving the job teaches its buildings and recipes; systems that need a worker (the " +
        "Purification Altar) look for someone with their job. The resident stands by the workplace once it is built.";
}

/// <summary>A resident's traits (Docs/PeopleResearch_2026-11-23.md §3.3): what they do differently, each effect asked for by kind.</summary>
public sealed class ResidentTraitSection : ContentSection
{
    public override string Title => "Traits";
    public override string Noun => "Trait";
    public override string SidebarGroup => "Settlement";
    public override Type AssetType => typeof(ResidentTraitDefinition);
    public override string Folder => "Assets/_Project/Data/People/Traits";
    public override string Help =>
        "One of a resident's two or three traits: its icon and name on their card, what it means on hover, lines they say of it, and its " +
        "effects (each kind is read by one system: ARCHITECTURE.md \"People\"). Give a resident theirs in their Who they are.";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var trait = (ResidentTraitDefinition)asset;
        if (trait.Effects.Count == 0)
        {
            problems.Add("Changes nothing: give it at least one effect.");
        }

        if (string.IsNullOrWhiteSpace(trait.Description))
        {
            problems.Add("Says nothing on hover: write what it means.");
        }

        return problems;
    }
}

/// <summary>The marks raids leave on people (§3.6): good ones, and hard ones with a way back.</summary>
public sealed class MarkSection : ContentSection
{
    public override string Title => "Marks";
    public override string Noun => "Mark";
    public override string SidebarGroup => "Settlement";
    public override Type AssetType => typeof(MarkDefinition);
    public override string Folder => "Assets/_Project/Data/People/Marks";
    public override string Help =>
        "What a raid (or a breach, a foray, a night) leaves on someone: what causes it and how likely, its effects, and for a hard mark its " +
        "way back. A mark is given only once it is in People Settings › Marks.";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        var mark = (MarkDefinition)asset;
        if (!mark.Good && mark.HealedBy == null && !mark.FriendsHeal && mark.TurnsInto == null)
        {
            problems.Add("A hard mark with no way back: give it an item, a friend's scene or what it turns into.");
        }

        bool listed = false;
        foreach (MarkDefinition had in PeopleSettings.Current.Marks)
        {
            listed |= had == mark;
        }

        if (!listed)
        {
            problems.Add("Not in People Settings › Marks, so it never comes.");
        }

        return problems;
    }
}

/// <summary>Announced defenses of Safe Haven (GDD §6.6–6.9).</summary>
public sealed class WardBreachSection : ContentSection
{
    public override string Title => "Ward Breaches";
    public override string Noun => "Ward Breach";
    public override string SidebarGroup => "Settlement";
    public override Type AssetType => typeof(WardBreachDefinition);
    public override string Folder => "Assets/_Project/Data/Settlement";
    public override string Help =>
        "A warned attack on Safe Haven: when it is offered, what the player is told, which enemies come and the reward. A " +
        "WardBreachDirector in the Safe Haven scene plays it.";

    public override List<string> FindProblems(Object asset, IList<Object> all)
    {
        List<string> problems = base.FindProblems(asset, all);
        if (!((WardBreachDefinition)asset).HasEnemies)
        {
            problems.Add("Has no enemy prefab, so nothing comes.");
        }

        return problems;
    }
}
