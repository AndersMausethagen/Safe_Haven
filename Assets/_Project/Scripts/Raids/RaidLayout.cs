using System;
using System.Collections.Generic;

/// <summary>
/// The dice behind raid generation, kept free of Unity so they can be tested: deterministic seeds per spawn group, picking
/// some of a group's points without repeats, and weighted picks. The same seed always gives the same layout.
/// </summary>
public static class RaidLayout
{
    /// <summary>
    /// A seed for one part of the layout (a spawn group), from the raid's seed and a stable key such as the group's path
    /// in the hierarchy. Each group then rolls on its own, so adding or reordering groups doesn't reshuffle the others.
    /// </summary>
    public static int SeedFor(int raidSeed, string key)
    {
        // FNV-1a: string.GetHashCode isn't guaranteed to be the same between runs or platforms.
        unchecked
        {
            uint hash = 2166136261u ^ (uint)raidSeed;
            hash *= 16777619u;
            foreach (char c in key ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619u;
            }

            return (int)(hash & int.MaxValue);
        }
    }

    /// <summary>How many to spawn: a number from min to max inclusive, limited to the points available.</summary>
    public static int RollCount(Random random, int min, int max, int available)
    {
        if (available <= 0)
        {
            return 0;
        }

        int low = Math.Max(0, Math.Min(min, max));
        int high = Math.Max(min, max);
        return Math.Min(available, random.Next(low, high + 1));
    }

    /// <summary>Picks count different indices from 0 to available - 1, in a random order (a partial shuffle).</summary>
    public static List<int> PickIndices(Random random, int available, int count)
    {
        var indices = new List<int>(available);
        for (int i = 0; i < available; i++)
        {
            indices.Add(i);
        }

        count = Math.Max(0, Math.Min(count, available));
        for (int i = 0; i < count; i++)
        {
            int swap = random.Next(i, available);
            (indices[i], indices[swap]) = (indices[swap], indices[i]);
        }

        indices.RemoveRange(count, available - count);
        return indices;
    }

    /// <summary>
    /// Rolls which options are available, each with its own chance (0-1), so a raid can offer different exits each run.
    /// At least one is always available when there are any (GDD §7.6: a reachable extraction must always exist): when
    /// every roll fails, one is chosen weighted by chance, or the first when every chance is 0.
    /// </summary>
    public static bool[] PickAvailable(Random random, IReadOnlyList<float> chances)
    {
        var available = new bool[chances.Count];
        bool any = false;
        for (int i = 0; i < chances.Count; i++)
        {
            // Rolled for every option, so one option's chance doesn't change the others' rolls.
            double roll = random.NextDouble();
            available[i] = chances[i] >= 1f || roll < chances[i];
            any |= available[i];
        }

        if (!any && chances.Count > 0)
        {
            int pick = PickWeighted(random, chances);
            available[pick < 0 ? 0 : pick] = true;
        }

        return available;
    }

    /// <summary>A weighted pick: index i is chosen with chance weights[i] / sum. Returns -1 when nothing has weight.</summary>
    public static int PickWeighted(Random random, IReadOnlyList<float> weights)
    {
        double total = 0d;
        foreach (float weight in weights)
        {
            total += Math.Max(0f, weight);
        }

        if (total <= 0d)
        {
            return -1;
        }

        double roll = random.NextDouble() * total;
        int last = -1;
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0f)
            {
                continue;
            }

            last = i;
            roll -= weights[i];
            if (roll < 0d)
            {
                return i;
            }
        }

        // Rounding can leave a sliver at the end; it belongs to the last weighted entry.
        return last;
    }
}
