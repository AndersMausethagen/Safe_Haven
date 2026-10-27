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
    public RaidSession(
        RaidDefinition definition,
        IReadOnlyList<RaidModifierDefinition> modifiers,
        RaidParameters parameters,
        PlayerSnapshot loadout,
        string returnScene,
        int sparksUsed = 0,
        int? seed = null)
    {
        Seed = seed ?? NewSeed();
        Definition = definition != null ? definition : throw new ArgumentNullException(nameof(definition));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Modifiers = modifiers ?? Array.Empty<RaidModifierDefinition>();
        Loadout = loadout;
        ReturnScene = returnScene;
        SparksUsed = Math.Max(0, sparksUsed);
        TimeOfDay = RaidTimeOfDay.For(definition, Seed);
    }

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
    public float Elapsed { get; private set; }
    public float TimeRemaining => Mathf.Max(0f, Parameters.Duration - Elapsed);
    public RaidOutcome Outcome { get; private set; } = RaidOutcome.InProgress;
    public bool IsOver => Outcome != RaidOutcome.InProgress;

    /// <summary>Advances the clock. Returns true on the tick that runs out of time, which ends the raid as TimeExpired.</summary>
    public bool Tick(float deltaTime)
    {
        if (IsOver || deltaTime <= 0f)
        {
            return false;
        }

        Elapsed = Mathf.Min(Elapsed + deltaTime, Parameters.Duration);
        return Elapsed >= Parameters.Duration && End(RaidOutcome.TimeExpired);
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
