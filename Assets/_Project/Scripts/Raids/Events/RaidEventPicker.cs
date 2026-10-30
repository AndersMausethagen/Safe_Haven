using System.Collections.Generic;

/// <summary>
/// Which event a raid gets next (GDD §9.4 event budgets): one that may happen now and hasn't happened this raid, weighted,
/// from the raid's own random stream, so the same seed can be told apart from another in bug reports. Plain C#.
/// </summary>
public static class RaidEventPicker
{
    /// <summary>The next event, or null when none can happen now.</summary>
    public static RaidEventDefinition Pick(IReadOnlyList<RaidEventDefinition> pool, System.Random random, float elapsed, float secondsLeft, int stage,
        ICollection<RaidEventDefinition> used)
    {
        var weights = new List<float>(pool.Count);
        foreach (RaidEventDefinition candidate in pool)
        {
            bool open = candidate != null && !used.Contains(candidate) && candidate.CanHappen(elapsed, secondsLeft, stage);
            weights.Add(open ? candidate.Weight : 0f);
        }

        int index = RaidLayout.PickWeighted(random, weights);
        return index >= 0 ? pool[index] : null;
    }
}
