using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How a raid ended. Values are serialized as numbers, so add new outcomes at the end and never renumber.</summary>
public enum RaidOutcome
{
    InProgress = 0,
    /// <summary>The player reached an extraction point and keeps everything carried.</summary>
    Extracted = 1,
    /// <summary>The raid collapsed before the player got out; everything carried is lost.</summary>
    TimeExpired = 2,
    /// <summary>The player was killed; everything carried is lost.</summary>
    Died = 3,
}

/// <summary>
/// One raid run, from the portal until it ends: which raid, the modifiers used, the rules they produced, what the player
/// brought in, where they came from, and how much time is left. Plain C# so the rules can be tested without a scene; the
/// RaidDirector advances it in the raid scene and decides what happens once it is over.
/// </summary>
public sealed class RaidSession
{
    private const float MinimumSecondsLeft = 15f;

    /// <summary>What the briefing said would happen on this raid. These are drawn first, so it comes true.</summary>
    public IReadOnlyList<RaidEventDefinition> Foretold { get; set; } = Array.Empty<RaidEventDefinition>();

    /// <summary>The rumours the Haven's buildings add (a Watch Post's), counted at home: the raid's scene has none of them.</summary>
    public int ExtraRumours { get; set; }

    /// <summary>How many bosses fell on this raid, for what those who went along remember.</summary>
    public int BossesBeaten { get; set; }

    /// <summary>The player chose a large raid: a bigger map with more on it (RaidDefinition › Large Raid).</summary>
    public bool IsLarge { get; set; }

    /// <summary>How many times wider the map is than the raid's normal one.</summary>
    public float WidthScale => IsLarge ? Definition.LargeRaid.widthScale : 1f;

    /// <summary>How many times more ground there is, and so how much more of what fills it.</summary>
    public float AreaScale => WidthScale * WidthScale;

    /// <summary>Who goes along on this raid, or null.</summary>
    public ResidentDefinition Companion { get; set; }


    private readonly List<string> eventLog = new List<string>();
    private float bonusSeconds;

    public RaidSession(
        RaidDefinition definition,
        IReadOnlyList<RaidModifierDefinition> modifiers,
        RaidParameters parameters,
        PlayerSnapshot loadout,
        string returnScene,
        int sparksUsed = 0,
        int? seed = null,
        RaidDifficulty difficulty = null)
    {
        Seed = seed ?? NewSeed();
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Modifiers = modifiers ?? Array.Empty<RaidModifierDefinition>();
        Loadout = loadout;
        ReturnScene = returnScene;
        SparksUsed = Math.Max(0, sparksUsed);
        TimeOfDay = RaidTimeOfDay.For(definition, Seed);
        Difficulty = difficulty ?? definition.LowestDifficulty;
    }

    /// <summary>The difficulty this run was entered at, or null for a raid without tiers.</summary>
    public RaidDifficulty Difficulty { get; }

    /// <summary>The gear score the player set out with, for the playtest log.</summary>
    public int GearScoreAtEntry { get; set; }

    /// <summary>The tier this run is played at.</summary>
    public int Tier => Difficulty != null ? Difficulty.tier : Definition.Tier;

    /// <summary>Whether this run's tier can hand over the item.</summary>
    public bool Allows(ItemDefinition item) => Difficulty == null || Difficulty.Allows(item);

    /// <summary>The hour this run takes place at (rolled from the seed), or null for the scene's own hour.</summary>
    public RaidTimeOfDay TimeOfDay { get; }

    /// <summary>
    /// Picks this run's layout (see RaidGenerator): which spawn points get creatures, caches and resources. Fixed for the
    /// whole run; a new run through the portal gets a new one.
    /// </summary>
    public int Seed { get; }

    public RaidDefinition Definition { get; }
    public IReadOnlyList<RaidModifierDefinition> Modifiers { get; }
    public RaidParameters Parameters { get; }
    /// <summary>Portal Sparks spent on extra time for this raid (GDD §7.3).</summary>
    public int SparksUsed { get; }
    /// <summary>What the player carried in, to compare against at the end. Null for test raids started in the editor.</summary>
    public PlayerSnapshot Loadout { get; }
    /// <summary>The scene the player returns to when the raid ends.</summary>
    public string ReturnScene { get; }
    /// <summary>How the player gathered this raid, for the summary.</summary>
    public GatheringStats Gathering { get; } = new GatheringStats();
    /// <summary>What happened on the way (raid events and the choices made), for the summary.</summary>
    public IReadOnlyList<string> EventLog => eventLog;
    public float Elapsed { get; private set; }
    /// <summary>The raid's length: its rules plus what events have added or taken away.</summary>
    public float TotalSeconds => Parameters.Duration + bonusSeconds;
    public float TimeRemaining => Mathf.Max(0f, TotalSeconds - Elapsed);
    public RaidOutcome Outcome { get; private set; } = RaidOutcome.InProgress;
    public bool IsOver => Outcome != RaidOutcome.InProgress;

    /// <summary>
    /// Advances the clock. Returns true on the tick that runs out of time, which ends the raid as TimeExpired. With
    /// <paramref name="holdAtZero"/> (the player stands in an open extraction circle) the clock stops at zero instead and
    /// the raid goes on, so they can still extract (the user, 2026-11-14).
    /// </summary>
    public bool Tick(float deltaTime, bool holdAtZero = false)
    {
        if (IsOver || deltaTime <= 0f)
        {
            return false;
        }

        Elapsed = Mathf.Min(Elapsed + deltaTime, TotalSeconds);
        return Elapsed >= TotalSeconds && !holdAtZero && End(RaidOutcome.TimeExpired);
    }

    /// <summary>Adds seconds to the clock, or takes them away; an event can't end the raid on the spot.</summary>
    public void AddTime(float seconds)
    {
        // The floor keeps a loss from ending the raid, but never turns one into a gain: a "-0:45" at 8 s left gave +7 s.
        float floor = Mathf.Min(bonusSeconds, Elapsed + MinimumSecondsLeft - Parameters.Duration);
        bonusSeconds = Mathf.Max(bonusSeconds + seconds, floor);
    }

    /// <summary>What didn't fit in the bag (or the essence bars) and stayed out there, for the summary.</summary>
    public IReadOnlyList<ItemAmount> ItemsLeftBehind => itemsLeftBehind;
    public IReadOnlyList<ResourceAmount> ResourcesLeftBehind => resourcesLeftBehind;
    private readonly List<ItemAmount> itemsLeftBehind = new List<ItemAmount>();
    private readonly List<ResourceAmount> resourcesLeftBehind = new List<ResourceAmount>();

    /// <summary>Notes loot that didn't fit (an ItemDefinition or a ResourceDefinition), adding to what was left of it before.</summary>
    public void RecordLeftBehind(UnityEngine.Object what, int amount)
    {
        if (IsOver || amount <= 0)
        {
            return;
        }

        if (what is ItemDefinition item)
        {
            int at = itemsLeftBehind.FindIndex(entry => entry.Item == item);
            int had = at >= 0 ? itemsLeftBehind[at].Amount : 0;
            if (at >= 0) itemsLeftBehind[at] = new ItemAmount(item, had + amount);
            else itemsLeftBehind.Add(new ItemAmount(item, amount));
        }
        else if (what is ResourceDefinition resource)
        {
            int at = resourcesLeftBehind.FindIndex(entry => entry.Resource == resource);
            int had = at >= 0 ? resourcesLeftBehind[at].Amount : 0;
            if (at >= 0) resourcesLeftBehind[at] = new ResourceAmount(resource, had + amount);
            else resourcesLeftBehind.Add(new ResourceAmount(resource, amount));
        }
    }

    /// <summary>Notes something that happened, for the summary, in place of <paramref name="replaces"/> when it was noted.</summary>
    public void RecordEvent(string what, string replaces = null)
    {
        int at = replaces != null ? eventLog.IndexOf(replaces) : -1;
        if (at >= 0)
        {
            eventLog[at] = what;
        }
        else
        {
            eventLog.Add(what);
        }
    }

    public static int NewSeed()
    {
        return Guid.NewGuid().GetHashCode() & int.MaxValue;
    }

    /// <summary>Ends the raid. Only the first outcome counts; returns false if it was already over.</summary>
    public bool End(RaidOutcome outcome)
    {
        if (IsOver || outcome == RaidOutcome.InProgress)
        {
            return false;
        }

        Outcome = outcome;
        return true;
    }
}
