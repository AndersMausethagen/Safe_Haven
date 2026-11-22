using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ring drawn in the Keeper's view for the woodcutters to clear (Docs/ColonyModeResearch_2026-11-21.md §4.6-4.7, round two: the
/// Haven's wood is its painted trees, and felled ones stay down): the woodcutter fells the grown trees inside it one by one, carries the
/// logs to the chests and plants a sapling where each stood (the forester's loop: the woods grow back and stay green). Saved on the
/// settlement; a ring with no grown tree left is done.
/// </summary>
public sealed class RingOrder
{
    public RingOrder(Vector3 centre, float radius)
    {
        Centre = centre;
        Radius = Mathf.Max(1f, radius);
    }

    public Vector3 Centre { get; }
    public float Radius { get; }

    /// <summary>Trees felled while the player was away, still to be laid down at home when the Haven is next the scene.</summary>
    public int PendingFells { get; set; }

    /// <summary>Grown trees standing in it when last seen at home (caps the felling while away); -1 before it has been looked at.</summary>
    public int StandingSeen { get; set; } = -1;

    /// <summary>Where a sapling didn't take (a tree still too near it): tried again when the ring is done. Not saved.</summary>
    public List<Vector3> Unplanted { get; } = new List<Vector3>();

    public bool Contains(Vector3 point)
    {
        Vector3 apart = point - Centre;
        apart.y = 0f;
        return apart.sqrMagnitude <= Radius * Radius;
    }

    /// <summary>Whether the two rings share ground (a tree in both would be counted twice away).</summary>
    public bool Overlaps(RingOrder other)
    {
        Vector3 apart = other.Centre - Centre;
        apart.y = 0f;
        return apart.magnitude < Radius + other.Radius;
    }
}

/// <summary>
/// The felling of rings (WorkerSettings › Felling): who fells (the Fellers' jobs), the next tree in a ring, a tree's logs, the sapling
/// planted after it, and the work done while the player is away, laid down on coming home. Plain rules where they can be (RingWorkTests);
/// the scene's terrains through TerrainTreeFelling. A woodcutter's own bench comes first, at home and away: they fell while it has
/// nothing to work on, so the logs they bring feed it.
/// </summary>
public static class RingWork
{
    // Away: seconds towards the next tree, by ring (not saved: a tree under way when the game is saved starts over).
    private static readonly Dictionary<RingOrder, float> AwayClock = new Dictionary<RingOrder, float>();

    // Trees a woodcutter is at now (walking to it or chopping): nobody else takes them, and coming home doesn't lay them down.
    private static readonly HashSet<(TerrainTreeFelling, int)> Claimed = new HashSet<(TerrainTreeFelling, int)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => ForgetGame();

    /// <summary>Forgets felling under way (a new game, a load).</summary>
    public static void ForgetGame()
    {
        AwayClock.Clear();
        Claimed.Clear();
    }

    /// <summary>A woodcutter takes this tree (until <see cref="Release"/>).</summary>
    public static void Claim(TerrainTreeFelling felling, int index)
    {
        if (felling != null && index >= 0)
        {
            Claimed.Add((felling, index));
        }
    }

    /// <summary>Lets go of a tree taken with <see cref="Claim"/>.</summary>
    public static void Release(TerrainTreeFelling felling, int index)
    {
        if (felling != null && index >= 0)
        {
            Claimed.Remove((felling, index));
        }
    }

    /// <summary>Whether <paramref name="record"/>'s job fells rings (WorkerSettings › Fellers), at work and not paused.</summary>
    public static bool Fells(ResidentRecord record, WorkerSettings settings)
    {
        if (record == null || record.Job == null || record.WorkPaused || settings.Fellers == null)
        {
            return false;
        }

        foreach (JobDefinition job in settings.Fellers)
        {
            if (job == record.Job)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Seconds a feller spends on one tree: the chopping (shared by their work speed) and the walk with the logs.</summary>
    public static float SecondsPerTree(float workSpeed, WorkerSettings settings) =>
        settings.ChopSeconds / Mathf.Max(0.1f, workSpeed) + settings.FellWalkSeconds;

    /// <summary>
    /// The logs a tree of <paramref name="height"/> metres gives a feller: as many as the player's tree worked to the end (felled, every
    /// section cut: TreeChoppingSettings.AllLogs), of the woods the ring's axe tier rolls.
    /// </summary>
    public static List<ItemAmount> Logs(float height, WorkerSettings settings, System.Func<float> random)
    {
        var logs = new List<ItemAmount>();
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        float size = rules.SizeOf(Mathf.Max(0.5f, height));
        ChoppableTree.Roll(rules.AllLogs(settings.FellerAxeTier, size), settings.FellerAxeTier, size, random, logs);
        return logs;
    }

    /// <summary>The grown tree nearest the ring's centre that nobody else is at, on any terrain under it, or false.</summary>
    public static bool NextTree(RingOrder ring, out TerrainTreeFelling felling, out TerrainTreeFelling.Standing tree)
    {
        felling = null;
        tree = default;
        float best = float.MaxValue;
        foreach (TerrainTreeFelling candidate in TerrainTreeFelling.Active)
        {
            if (candidate == null)
            {
                continue;
            }

            foreach (TerrainTreeFelling.Standing standing in candidate.StandingNear(ring.Centre, ring.Radius))
            {
                if (Growing(candidate, standing.Index) || Claimed.Contains((candidate, standing.Index)))
                {
                    continue;
                }

                Vector3 apart = standing.Foot - ring.Centre;
                apart.y = 0f;
                if (apart.sqrMagnitude < best)
                {
                    best = apart.sqrMagnitude;
                    felling = candidate;
                    tree = standing;
                }
            }
        }

        return felling != null;
    }

    /// <summary>How many grown trees stand in the ring now (at home), those being chopped too; saplings still growing don't count.</summary>
    public static int Standing(RingOrder ring)
    {
        int count = 0;
        foreach (TerrainTreeFelling candidate in TerrainTreeFelling.Active)
        {
            if (candidate == null)
            {
                continue;
            }

            foreach (TerrainTreeFelling.Standing standing in candidate.StandingNear(ring.Centre, ring.Radius))
            {
                count += Growing(candidate, standing.Index) ? 0 : 1;
            }
        }

        return count;
    }

    // A sapling still growing (planted by the forester, or by hand) is left standing.
    private static bool Growing(TerrainTreeFelling felling, int index) => PlantedTrees.Current != null && PlantedTrees.Current.IsGrowing(felling, index);

    /// <summary>A sapling where a tree came down (the forester), if the Haven plants and the ground takes one. False when it didn't.</summary>
    public static bool Replant(Vector3 foot, WorkerSettings settings) =>
        settings.Sapling != null && PlantedTrees.Current != null && PlantedTrees.Current.TryPlant(foot, settings.Sapling, out _);

    /// <summary>The ring done: saplings where they didn't take before, now that the trees round them are down.</summary>
    public static void ReplantLeft(RingOrder ring, WorkerSettings settings)
    {
        foreach (Vector3 foot in ring.Unplanted)
        {
            Replant(foot, settings);
        }

        ring.Unplanted.Clear();
    }

    /// <summary>
    /// The player away (SettlementSystem): <paramref name="seconds"/> more of each ring's fellers' work: those at home (not the one
    /// along) whose bench has nothing to work on, each for Away Felling Share of the time (working hours). Each tree done gives its logs
    /// to the chests (an average tree) and is laid down at home later (PendingFells), up to the trees last seen standing.
    /// </summary>
    public static void WorkAway(SettlementState home, StashStorage stash, float seconds, ResidentDefinition along, WorkerSettings settings)
    {
        if (home.Rings.Count == 0 || seconds <= 0f)
        {
            return;
        }

        float speed = 0f;
        foreach (ResidentRecord record in home.Residents)
        {
            if (record.Resident == along || !Fells(record, settings))
            {
                continue;
            }

            // Their own bench first, as at home.
            StationQueue bench = WorkerProduction.QueueOf(home, record);
            if (bench != null && !bench.IsIdle && !bench.Blocked)
            {
                continue;
            }

            speed += settings.AwayFellingShare;
        }

        if (speed <= 0f)
        {
            return;
        }

        // The first ring with trees left takes the work, as at home; one never looked at waits to be seen.
        foreach (RingOrder ring in home.Rings)
        {
            if (ring.StandingSeen < 0 || ring.StandingSeen - ring.PendingFells <= 0)
            {
                continue;
            }

            AwayClock.TryGetValue(ring, out float clock);
            clock += seconds * speed;
            float each = SecondsPerTree(1f, settings);
            while (clock >= each && ring.StandingSeen - ring.PendingFells > 0)
            {
                clock -= each;
                ring.PendingFells++;
                foreach (ItemAmount log in Logs(settings.AverageTreeHeight, settings, () => Random.value))
                {
                    stash.Items.Add(log.Item, log.Amount);
                }
            }

            AwayClock[ring] = clock;
            return;
        }
    }

    /// <summary>
    /// At home again: the trees felled while away are laid down (nearest the centre first, none a woodcutter is already at) with their
    /// saplings, each ring's standing trees noted, and rings with none left are done. Only in the game that keeps the Haven, and only
    /// once a terrain is up to lay them on.
    /// </summary>
    public static void SettleAtHome(SettlementState home, WorkerSettings settings)
    {
        if (TerrainTreeFelling.Active.Count == 0)
        {
            return;
        }

        var done = new List<RingOrder>();
        foreach (RingOrder ring in home.Rings)
        {
            for (; ring.PendingFells > 0; ring.PendingFells--)
            {
                if (!NextTree(ring, out TerrainTreeFelling felling, out TerrainTreeFelling.Standing tree))
                {
                    ring.PendingFells = 0;
                    break;
                }

                if (felling.FellByHand(tree.Index) && !Replant(tree.Foot, settings))
                {
                    ring.Unplanted.Add(tree.Foot);
                }
            }

            ring.StandingSeen = Standing(ring);
            if (ring.StandingSeen == 0)
            {
                ReplantLeft(ring, settings);
                done.Add(ring);
            }
        }

        foreach (RingOrder ring in done)
        {
            home.RemoveRing(ring);
        }
    }
}
