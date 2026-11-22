using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ring drawn in the Keeper's view for the woodcutters to clear (Docs/ColonyModeResearch_2026-11-21.md §4.6-4.7, round two: the
/// Haven's wood is its painted trees, and felled ones stay down): the woodcutter fells the trees inside it one by one, carries the logs
/// to the chests and plants a sapling where each stood (the forester's loop: the woods grow back and stay green). Saved on the
/// settlement; a ring with no tree left is done.
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

    /// <summary>Trees standing in it when last seen at home (caps the felling while away); -1 before it has been looked at.</summary>
    public int StandingSeen { get; set; } = -1;

    public bool Contains(Vector3 point)
    {
        Vector3 apart = point - Centre;
        apart.y = 0f;
        return apart.sqrMagnitude <= Radius * Radius;
    }
}

/// <summary>
/// The felling of rings (WorkerSettings › Felling): who fells (the Fellers' jobs), the next tree in a ring, a tree's logs, the sapling
/// planted after it, and the work done while the player is away, laid down on coming home. Plain rules where they can be (RingWorkTests);
/// the scene's terrains through TerrainTreeFelling.
/// </summary>
public static class RingWork
{
    // Away: seconds towards the next tree, by ring (not saved: a tree under way when the game is saved starts over).
    private static readonly Dictionary<RingOrder, float> AwayClock = new Dictionary<RingOrder, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => AwayClock.Clear();

    /// <summary>Forgets felling under way (a new game, a load).</summary>
    public static void ForgetGame() => AwayClock.Clear();

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

    /// <summary>The logs a tree of <paramref name="height"/> metres gives a feller (Plain wood by the ring's axe tier).</summary>
    public static List<ItemAmount> Logs(float height, WorkerSettings settings, System.Func<float> random)
    {
        var logs = new List<ItemAmount>();
        ChoppableTree.Roll(settings.FellerAxeTier, Mathf.Max(1f, height), random, logs);
        return logs;
    }

    /// <summary>The grown tree nearest the ring's centre, on any terrain under it (the first to come down), or false.</summary>
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
                if (Growing(candidate, standing.Index))
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

    /// <summary>How many grown trees stand in the ring now (at home): saplings still growing don't count.</summary>
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

    /// <summary>A sapling where a tree came down (the forester), if the Haven plants and the ground takes one.</summary>
    public static void Replant(Vector3 foot, WorkerSettings settings)
    {
        if (settings.Sapling != null && PlantedTrees.Current != null)
        {
            PlantedTrees.Current.TryPlant(foot, settings.Sapling, out _);
        }
    }

    /// <summary>
    /// The player away (SettlementSystem): <paramref name="seconds"/> more of each ring's fellers' work. Each tree done gives its logs to
    /// the chests (an average tree) and is laid down at home later (PendingFells), up to the trees last seen standing.
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
            if (record.Resident != along && Fells(record, settings))
            {
                speed += 1f;
            }
        }

        if (speed <= 0f)
        {
            return;
        }

        // The first ring with trees left takes the work, as at home.
        foreach (RingOrder ring in home.Rings)
        {
            int left = ring.StandingSeen - ring.PendingFells;
            if (ring.StandingSeen >= 0 && left <= 0)
            {
                continue;
            }

            AwayClock.TryGetValue(ring, out float clock);
            clock += seconds * speed;
            float each = SecondsPerTree(1f, settings);
            while (clock >= each && (ring.StandingSeen < 0 || ring.StandingSeen - ring.PendingFells > 0))
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
    /// At home again: the trees felled while away are laid down (nearest the centre first) with their saplings, each ring's standing trees
    /// noted, and rings with none left are done. Only in the game that keeps the Haven.
    /// </summary>
    public static void SettleAtHome(SettlementState home, WorkerSettings settings)
    {
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

                felling.FellByHand(tree.Index);
                Replant(tree.Foot, settings);
            }

            ring.StandingSeen = Standing(ring);
            if (ring.StandingSeen == 0)
            {
                done.Add(ring);
            }
        }

        foreach (RingOrder ring in done)
        {
            home.RemoveRing(ring);
        }
    }
}
