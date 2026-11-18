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
    private readonly System.Action<RaidDefinition, int, bool> layOutAhead;
    // The portal's destinations as planning began (RaidPortal.Destinations builds a new list on every read).
    private IReadOnlyList<RaidDefinition> destinations = System.Array.Empty<RaidDefinition>();
    private int destinationIndex;

    /// <param name="layOutAhead">Lays a run out ahead (raid, seed, large), so entering is quick and the briefing can tell
    /// of its routes: RaidLayoutPrefetch.Prepare unless given another (tests).</param>
    public RaidPlan(System.Action<RaidDefinition, int, bool> layOutAhead = null)
    {
        this.layOutAhead = layOutAhead ?? RaidLayoutPrefetch.Prepare;
    }

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
    /// <summary>The portal's destinations, the first one first.</summary>
    public IReadOnlyList<RaidDefinition> Destinations => destinations;

    public int DestinationIndex => Mathf.Clamp(destinationIndex, 0, Mathf.Max(0, destinations.Count - 1));

    public RaidDefinition Destination => destinations.Count > 0 ? destinations[DestinationIndex] : null;

    /// <summary>A locked place: it can be read about but not planned for (unless the cheat menu lifts requirements).</summary>
    public bool IsClosed => Destination != null && Destination.IsLocked && !Cheats.NoRequirements;

    /// <summary>Portal Sparks can buy time on this run (never in a boss encounter: it has no raid clock to extend).</summary>
    public bool SparksUsable => Destination != null && !IsClosed && SparksOffered && !Destination.IsEncounter;

    /// <summary>
    /// The destinations the planner shows: a boss encounter only once the story has opened it (the research: "its tab is hidden
    /// until it is open"), unless the cheat menu lifts requirements.
    /// </summary>
    public static IReadOnlyList<RaidDefinition> Shown(IReadOnlyList<RaidDefinition> all)
    {
        var shown = new List<RaidDefinition>();
        foreach (RaidDefinition raid in all)
        {
            if (raid != null && (!raid.IsEncounter || !raid.IsLocked || Cheats.NoRequirements))
            {
                shown.Add(raid);
            }
        }

        return shown;
    }

    /// <summary>True when the destination is a boss encounter whose boss has fallen here: rematches, at any of its tiers.</summary>
    public bool IsRematch => Destination != null && Destination.IsEncounter && Destination.Encounter.IsBeatenIn(GameSession.Here);

    private static bool SparksOffered => SettlementSettings.Current.PortalSpark != null && SettlementSettings.Current.MaxSparksPerRaid > 0;

    /// <summary>Starts a fresh plan at <paramref name="portal"/> for <paramref name="player"/>, settled (<see cref="Settle"/>:
    /// a tier, who could come along), and lays its first run out ahead.</summary>
    public void Begin(RaidPortal portal, GameObject player)
    {
        Portal = portal;
        Player = player;
        destinations = Shown(portal != null ? portal.Destinations : System.Array.Empty<RaidDefinition>());
        picked.Clear();
        destinationIndex = 0;
        Difficulty = null;
        Sparks = 0;
        Companion = null;
        Large = false;
        Seed = PinnedSeed(Destination);
        Large = LargeByDefault(Destination);
        Settle();
        layOutAhead(Destination, Seed, Large);
    }

    public void End()
    {
        Portal = null;
        Player = null;
        destinations = System.Array.Empty<RaidDefinition>();
    }

    /// <summary>Another destination: its own default tier and size.</summary>
    public void PickDestination(int index)
    {
        destinationIndex = index;
        Difficulty = null;
        // Its own seed: every other destination planned, briefed and launched on the first one's.
        Seed = PinnedSeed(Destination);
        Large = LargeByDefault(Destination);
        layOutAhead(Destination, Seed, Large);
    }

    public void PickTier(RaidDifficulty tier) => Difficulty = tier;

    public void ToggleSize()
    {
        Large = !Large;
        layOutAhead(Destination, Seed, Large);
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

        // A boss encounter's first fight is at its own tier, whatever is picked.
        if (raid != null && raid.IsEncounter)
        {
            Difficulty = raid.Encounter.DifficultyFor(raid, IsRematch, Difficulty);
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
        foreach (ResidentRecord record in GameSession.Here.Residents)
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

    // The briefing is pinned to the turn (Docs/SystemDesignReview_2026-11-17.md K2): reopening the planner showed a new seed each time,
    // so the hour (night pays more), the rumours and the foretold events could be fished for. A destination keeps its seed until the
    // territory's turn moves on (at a dawn or a raid's end).
    private static readonly Dictionary<string, (int turn, int seed)> Pinned = new Dictionary<string, (int turn, int seed)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPinned() => Pinned.Clear();

    private static int PinnedSeed(RaidDefinition raid)
    {
        if (raid == null)
        {
            return FreshSeed(null);
        }

        int turn = Territory.TurnNow;
        if (Pinned.TryGetValue(raid.Id, out (int turn, int seed) pinned) && pinned.turn == turn)
        {
            return pinned.seed;
        }

        int seed = FreshSeed(raid);
        Pinned[raid.Id] = (turn, seed);
        return seed;
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
