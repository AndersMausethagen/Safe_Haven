using System.Collections.Generic;

/// <summary>
/// Which event a raid gets next (GDD §9.4 event budgets): one that may happen now and hasn't happened this raid, weighted,
/// from the raid's own random stream, so the same seed can be told apart from another in bug reports. Plain C#.
/// </summary>
public static class RaidEventPicker
{
    /// <summary>The next event, or null when none can happen now.</summary>
    public static RaidEventDefinition Pick(IReadOnlyList<RaidEventDefinition> pool, System.Random random, float elapsed, float secondsLeft, int stage,
        ICollection<RaidEventDefinition> used) => Pick(pool, random, elapsed, secondsLeft, stage, used, null, null, null);

    /// <summary>
    /// The next event, or null when none can happen now (Docs/RaidEventsPlan_2026-11-26.md E4-E6): only those the save's marks allow
    /// (<paramref name="hasMark"/>; a follow-up, a legend not yet met), weighted up by the states that hold (<paramref name="states"/>) and
    /// by the run's condition (<paramref name="condition"/>). A follow-up's needed mark is asked of <paramref name="hadAtStart"/> when given
    /// (see <see cref="RaidEventDefinition.AllowedBy"/>).
    /// </summary>
    public static RaidEventDefinition Pick(IReadOnlyList<RaidEventDefinition> pool, System.Random random, float elapsed, float secondsLeft, int stage,
        ICollection<RaidEventDefinition> used, System.Func<string, bool> hasMark, ICollection<RaidEventState> states, RaidEventCondition condition,
        System.Func<string, bool> hadAtStart = null)
    {
        var weights = new List<float>(pool.Count);
        foreach (RaidEventDefinition candidate in pool)
        {
            bool open = candidate != null && !used.Contains(candidate) && candidate.CanHappen(elapsed, secondsLeft, stage) && candidate.AllowedBy(hasMark, hadAtStart);
            float weight = open ? candidate.WeightIn(states, hasMark) : 0f;
            if (open && condition != null)
            {
                weight *= condition.Boosts(candidate);
            }

            weights.Add(weight);
        }

        int index = RaidLayout.PickWeighted(random, weights);
        return index >= 0 ? pool[index] : null;
    }
}
