using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A raid being planned at the portal: where to (one of the portal's destinations), the run's seed, the difficulty tier,
/// Portal Sparks for extra time, a large map or the usual one, who goes along and which raid modifier runes are used. It
/// keeps the choices and their rules (the defaults, the limits, what stops the launch, the rules the run will have) and
/// launches the run through RaidLauncher. RaidPreparationWindow shows it and turns clicks into its methods. Plain C#.
/// </summary>
public sealed class RaidPlan
{
    private readonly HashSet<ItemDefinition> picked = new HashSet<ItemDefinition>();
    private readonly List<ResidentDefinition> companions = new List<ResidentDefinition>();
    private int destinationIndex;

    public RaidPortal Portal { get; private set; }

    public GameObject Player { get; private set; }

    /// <summary>The run's seed, rolled when planning starts, so the briefing shows the hour and layout the raid will really have.</summary>
    public int Seed { get; private set; }

    /// <summary>The tier picked, or (after <see cref="Settle"/>) the highest the player's gear is ready for.</summary>
    public RaidDifficulty Difficulty { get; private set; }

    public int Sparks { get; private set; }

    public bool Large { get; private set; }

    /// <summary>Who goes along, or null for going alone.</summary>
    public ResidentDefinition Companion { get; private set; }

    /// <summary>The residents who could go along (refreshed by <see cref="Settle"/>).</summary>
    public IReadOnlyList<ResidentDefinition> Companions => companions;

    public IReadOnlyCollection<ItemDefinition> Picked => picked;

    /// <summary>Which of the portal's destinations is planned for (clamped to them).</summary>
    public int DestinationIndex
    {
        get
        {
            int count = Portal != null ? Portal.Destinations.Count : 0;
            return Mathf.Clamp(destinationIndex, 0, Mathf.Max(0, count - 1));
        }
    }

    public RaidDefinition Destination
    {
        get
        {
            if (Portal == null)
            {
                return null;
            }

            IReadOnlyList<RaidDefinition> destinations = Portal.Destinations;
            return destinations.Count > 0 ? destinations[DestinationIndex] : null;
        }
    }

    /// <summary>A locked place: it can be read about but not planned for (unless the cheat menu lifts requirements).</summary>
    public bool IsClosed => Destination != null && Destination.IsLocked && !Cheats.NoRequirements;

    /// <summary>Portal Sparks can buy time on this run.</summary>
    public bool SparksUsable => Destination != null && !IsClosed && SparksOffered;

    private static bool SparksOffered => SettlementSettings.Current.PortalSpark != null && SettlementSettings.Current.MaxSparksPerRaid > 0;

    /// <summary>Starts a fresh plan at <paramref name="portal"/> for <paramref name="player"/>, and lays its first run out ahead.</summary>
    public void Begin(RaidPortal portal, GameObject player)
    {
        Portal = portal;
        Player = player;
        picked.Clear();
        destinationIndex = 0;
        Difficulty = null;
        Sparks = 0;
        Companion = null;
        Large = false;
        Seed = FreshSeed(Destination);
        Large = LargeByDefault(Destination);
        RaidLayoutPrefetch.Prepare(Destination, Seed, Large);
    }

    public void End()
    {
        Portal = null;
        Player = null;
    }

    /// <summary>Another destination: its own default tier and size.</summary>
    public void PickDestination(int index)
    {
        destinationIndex = index;
        Difficulty = null;
        Large = LargeByDefault(Destination);
        RaidLayoutPrefetch.Prepare(Destination, Seed, Large);
    }

    public void PickTier(RaidDifficulty tier) => Difficulty = tier;

    public void ToggleSize()
    {
        Large = !Large;
        RaidLayoutPrefetch.Prepare(Destination, Seed, Large);
    }

    public void StepSparks(int step) => Sparks = Mathf.Clamp(Sparks + step, 0, SettlementSettings.Current.MaxSparksPerRaid);

    /// <summary>The next or previous companion; going alone is one of the places in the list.</summary>
    public void StepCompanion(int step)
    {
        int at = companions.IndexOf(Companion);
        int count = companions.Count + 1;
        at = (at + 1 + step + count) % count;
        Companion = at == 0 ? null : companions[at - 1];
    }

    /// <summary>Uses a rune kind, or stops using it. Each kind at most once.</summary>
    public void Toggle(ItemDefinition item)
    {
        if (!picked.Remove(item))
        {
            picked.Add(item);
        }
    }

    public bool IsPicked(ItemDefinition item) => item != null && picked.Contains(item);

    /// <summary>Forgets runes no longer in the bag (used up or put away while planning).</summary>
    public void ForgetMissing(ICollection<ItemDefinition> inBag) => picked.RemoveWhere(item => !inBag.Contains(item));

    /// <summary>
    /// Fits the choices to what is on offer now: the highest tier the player's gear is ready for when none is picked
    /// (refused tiers stay on offer and say why), no Sparks where they can't be used, no large map where there is none,
    /// and no companion who can no longer go along.
    /// </summary>
    public void Settle()
    {
        RaidDefinition raid = Destination;
        if (raid != null && Difficulty == null)
        {
            Difficulty = raid.DifficultyFor(GearScore.Of(Player));
        }

        if (!SparksUsable)
        {
            Sparks = 0;
        }

        if (raid != null && !raid.CanBeLarge)
        {
            Large = false;
        }

        companions.Clear();
        foreach (ResidentRecord record in GameSession.Settlement.Residents)
        {
            if (RaidLauncher.CanGoAlong(record.Resident))
            {
                companions.Add(record.Resident);
            }
        }

        if (!companions.Contains(Companion))
        {
            Companion = null;
        }
    }

    /// <summary>What stops the launch, or null: the place, the ward, the gear, the Sparks.</summary>
    public string Problem()
    {
        return RaidLauncher.CheckLaunch(Destination)
            ?? RaidLauncher.CheckWard(Difficulty)
            ?? RaidLauncher.CheckGear(Difficulty, Player)
            ?? RaidLauncher.CheckSparks(Player, Sparks);
    }

    /// <summary>The hour the run will have.</summary>
    public RaidTimeOfDay TimeOfDay => RaidTimeOfDay.For(Destination, Seed);

    /// <summary>The rules the run will have with these choices.</summary>
    public RaidParameters Rules(RaidTimeOfDay timeOfDay)
    {
        RaidDefinition raid = Destination;
        var modifiers = new List<RaidModifierDefinition>();
        foreach (ItemDefinition item in picked)
        {
            modifiers.Add(item.RaidModifier);
        }

        float extraSeconds = RaidLauncher.ExtraSeconds(Sparks) + (Large && raid.CanBeLarge ? raid.LargeRaid.extraSeconds : 0f);
        return RaidParameters.Calculate(raid, modifiers, extraSeconds, timeOfDay, Difficulty, RaidLauncher.ExtractionCut);
    }

    /// <summary>Launches the planned run, or says why it can't go.</summary>
    public bool TryLaunch(out string problem)
    {
        if (Portal == null)
        {
            problem = null;
            return false;
        }

        return RaidLauncher.TryLaunch(Destination, Player, picked, out problem, Sparks, Seed, Difficulty, Companion, Large);
    }

    // A run unlike the last one: a different mood or shape, where the raid has more than one.
    private static int FreshSeed(RaidDefinition raid)
    {
        int seed = RaidSession.NewSeed();
        if (raid == null || raid.LayoutRules == null || RaidLauncher.LastRun.raid != raid.Id)
        {
            return seed;
        }

        for (int tries = 0; tries < 20; tries++)
        {
            (RaidShapeRule shape, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, seed);
            if (mood.name != RaidLauncher.LastRun.mood || shape.shape != RaidLauncher.LastRun.shape)
            {
                return seed;
            }

            seed = RaidSession.NewSeed();
        }

        return seed;
    }

    // A run whose mood is made for a large map (the ravine country) is offered large first; the player can still pick Normal.
    private bool LargeByDefault(RaidDefinition raid)
    {
        if (raid == null || !raid.CanBeLarge || raid.LayoutRules == null)
        {
            return false;
        }

        (RaidShapeRule _, RaidMapMood mood) = RaidLayoutPlanner.Choose(raid.LayoutRules, Seed);
        return mood != null && mood.largeByDefault;
    }
}
