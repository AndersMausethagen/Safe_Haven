using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a spawn group can place, with weights: "wolves", "ore veins", "supply caches". A group picks one entry per point
/// it fills. Entries can be capped per raid so a rare find stays rare. Shared between raids; never changed while playing.
/// </summary>
[CreateAssetMenu(fileName = "SpawnTable", menuName = "Safe Haven/Raids/Spawn Table")]
public sealed class SpawnTable : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public GameObject prefab;
        [Tooltip("Relative chance against the other entries. 0 never spawns.")]
        [Min(0f)] public float weight = 1f;
        [Tooltip("The most of this a single group (or raid spawn list) places in one raid. 0 means no limit.")]
        [Min(0)] public int maxPerGroup;
        [Tooltip("At least this many are placed in every raid, before the weighted picks, so a quest resource is always there.")]
        [Min(0)] public int minPerGroup;
        [Tooltip("The lowest raid tier this can appear at. 0: any.")]
        [Range(0, Tiers.Max)] public int fromTier;
        [Tooltip("The highest raid tier this can appear at. 0: any.")]
        [Range(0, Tiers.Max)] public int toTier;

        /// <summary>Whether this can appear at a raid tier.</summary>
        public bool AppearsAt(int tier) => (fromTier <= 0 || tier >= fromTier) && (toTier <= 0 || tier <= toTier);
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    /// <summary>
    /// What a group with this many points to fill places: the guaranteed minimums first (as many as fit), then weighted
    /// picks within the caps. Fewer than amount when every entry is capped.
    /// </summary>
    public List<GameObject> Roll(System.Random random, int amount) => Roll(entries, random, amount);

    /// <summary>Picks a prefab, skipping entries that hit their cap in counts (prefab to placed so far). Null when none can.</summary>
    public GameObject Pick(System.Random random, Dictionary<GameObject, int> counts) => Pick(entries, random, counts);

    /// <summary>Picks a prefab among what can appear at a raid tier; among everything when there is no raid (tier 0).</summary>
    public GameObject PickAt(System.Random random, int tier)
    {
        if (tier <= 0)
        {
            return Pick(entries, random, null);
        }

        return Pick(entries.FindAll(entry => entry.AppearsAt(tier)), random, null);
    }

    /// <summary>Roll for any list of entries (a spawn table's or a raid's spawn list).</summary>
    public static List<GameObject> Roll(IReadOnlyList<Entry> entries, System.Random random, int amount)
    {
        var picks = new List<GameObject>(amount);
        var counts = new Dictionary<GameObject, int>();
        foreach (Entry entry in entries)
        {
            int guaranteed = entry.maxPerGroup > 0 ? Mathf.Min(entry.minPerGroup, entry.maxPerGroup) : entry.minPerGroup;
            for (int i = 0; i < guaranteed && picks.Count < amount && entry.prefab != null; i++)
            {
                Add(entry.prefab);
            }
        }

        while (picks.Count < amount)
        {
            GameObject prefab = Pick(entries, random, counts);
            if (prefab == null)
            {
                break;
            }

            Add(prefab);
        }

        return picks;

        void Add(GameObject prefab)
        {
            picks.Add(prefab);
            counts.TryGetValue(prefab, out int placed);
            counts[prefab] = placed + 1;
        }
    }

    /// <summary>Pick for any list of entries.</summary>
    public static GameObject Pick(IReadOnlyList<Entry> entries, System.Random random, Dictionary<GameObject, int> counts)
    {
        var weights = new List<float>(entries.Count);
        foreach (Entry entry in entries)
        {
            bool capped = entry.maxPerGroup > 0 && counts != null && entry.prefab != null
                          && counts.TryGetValue(entry.prefab, out int placed) && placed >= entry.maxPerGroup;
            weights.Add(entry.prefab == null || capped ? 0f : entry.weight);
        }

        int index = RaidLayout.PickWeighted(random, weights);
        return index >= 0 ? entries[index].prefab : null;
    }

    /// <summary>True when every prefab in the entries is (or carries) a creature, so enemy density applies to them.</summary>
    public static bool AllCreatures(IReadOnlyList<Entry> entries)
    {
        bool any = false;
        foreach (Entry entry in entries)
        {
            if (entry.prefab == null)
            {
                continue;
            }

            if (entry.prefab.GetComponentInChildren<MonsterAI>(true) == null)
            {
                return false;
            }

            any = true;
        }

        return any;
    }

    /// <summary>The chance of each entry on a single pick, before caps; for the editor.</summary>
    public float ChanceOf(Entry entry)
    {
        float total = 0f;
        foreach (Entry other in entries)
        {
            total += other.prefab != null ? other.weight : 0f;
        }

        return total > 0f && entry.prefab != null ? entry.weight / total : 0f;
    }

#if UNITY_EDITOR
    /// <summary>Replaces the entries; for editor tools that build tables.</summary>
    public void SetEntries(IEnumerable<Entry> newEntries)
    {
        entries = new List<Entry>(newEntries);
    }
#endif
}
