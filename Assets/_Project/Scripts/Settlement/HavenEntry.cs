using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a line in the Haven window's lists stands: what colour its mark takes and whether it is dimmed.</summary>
public enum HavenEntryState
{
    /// <summary>All is well: stored, standing, home.</summary>
    Good = 0,
    /// <summary>Under way: a scaffold, something being made.</summary>
    Working = 1,
    /// <summary>Wants the player: damaged, in ruins, an empty job.</summary>
    Attention = 2,
    /// <summary>Could be had now but isn't yet.</summary>
    Ready = 3,
    /// <summary>Out of reach for now. Dimmed.</summary>
    Locked = 4,
}

/// <summary>One thing to pay, against what the player has.</summary>
public readonly struct HavenCost
{
    public readonly string Name;
    public readonly int Have;
    public readonly int Need;

    public HavenCost(string name, int have, int need)
    {
        Name = name;
        Have = have;
        Need = need;
    }
}

/// <summary>A headed part of an entry's details: words, costs or both.</summary>
public sealed class HavenDetail
{
    public HavenDetail(string heading, string body, IReadOnlyList<HavenCost> costs = null)
    {
        Heading = heading;
        Body = body;
        Costs = costs ?? Array.Empty<HavenCost>();
    }

    public string Heading { get; }
    public string Body { get; }
    public IReadOnlyList<HavenCost> Costs { get; }
}

/// <summary>A button under an entry's details. One that hands over to the world (going there, placing a building) closes the window first.</summary>
public sealed class HavenAction
{
    public HavenAction(string label, Action run, bool enabled = true, bool closesWindow = true, bool primary = true)
    {
        Label = label;
        Run = run;
        Enabled = enabled;
        ClosesWindow = closesWindow;
        Primary = primary;
    }

    public string Label { get; }
    public Action Run { get; }
    public bool Enabled { get; }
    public bool ClosesWindow { get; }
    public bool Primary { get; }
}

/// <summary>
/// A line in one of the Haven window's lists, and what is shown when it is chosen: a stored item, a building, a person. Plain
/// data read fresh from the game, so the page only draws it. Key is what it stands for (a definition, a page's title), which keeps the choice across reads.
/// </summary>
public sealed class HavenEntry
{
    public object Key { get; set; }
    /// <summary>What marks it as read when it is chosen, for a page that keeps count. Empty: nothing does.</summary>
    public string ReadKey { get; set; }
    public string Name { get; set; }
    public string Line { get; set; }
    public string Badge { get; set; }
    public Sprite Icon { get; set; }
    /// <summary>A locked entry's picture drawn as a dark shape, not its colours (the journal's unknown modules).</summary>
    public bool Silhouette { get; set; }
    public HavenEntryState State { get; set; }
    public string Status { get; set; }
    public string Description { get; set; }
    public List<HavenDetail> Details { get; } = new List<HavenDetail>();
    public List<HavenAction> Actions { get; } = new List<HavenAction>();
}

/// <summary>A headed group of entries. Empty ones aren't shown.</summary>
public sealed class HavenShelf
{
    public HavenShelf(string title)
    {
        Title = title;
    }

    public string Title { get; }
    public List<HavenEntry> Entries { get; } = new List<HavenEntry>();
}
