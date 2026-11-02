using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which tab of the raid editor a spawn list belongs to; only for organising, it changes nothing in play.</summary>
public enum SpawnCategory
{
    Enemies = 0,
    Resources = 1,
    LootAndEvents = 2,
    Escalation = 3,
}

/// <summary>
/// One "what spawns where" rule of a raid, kept on the RaidDefinition so a designer tunes a raid in one place: the scene's
/// Spawn Groups (by name) only supply the points. Each raid the list fills count points chosen among all its groups and
/// picks a prefab for each from its entries, whose weights are percentages of the list (they add up to 100). Groups not
/// named by any list keep using their own spawn table. Read only while playing.
/// </summary>
[Serializable]
public sealed class RaidSpawnList
{
    [Tooltip("Shown in the editor and the layout log; also seeds the list's roll, so renaming it reshuffles it.")]
    public string name = "New spawn list";
    public SpawnCategory category;
    [Tooltip("The Spawn Groups (GameObjects under 'Raid Layout' in the raid scene) whose points this list fills.")]
    public List<string> pointGroups = new List<string>();
    [Tooltip("How many points are filled each raid, at least and at most, among all the groups' points.")]
    public Vector2Int count = new Vector2Int(3, 5);
    [Tooltip("Only uses points this far from the player start; the distances are on the raid (or the global rules).")]
    public EntryBand entryBand = EntryBand.Any;
    [Tooltip("Keeps chosen points at least this far apart, useful when one spawn is a whole pack. 0 = no limit.")]
    [Min(0f)] public float separation;
    [Tooltip("RaidStart: filled while the raid loads. Stage: when the raid reaches the stage. Alarm: when something sets " +
             "off one of its groups, such as an extraction point's alarm.")]
    public SpawnTrigger trigger = SpawnTrigger.RaidStart;
    [Tooltip("For Stage lists: the raid stage that fills it (1 is the first stage).")]
    [Min(1)] public int stage = 1;
    [Tooltip("Creatures from this list head straight for the player and never give up the chase.")]
    public bool huntPlayer;
    [Tooltip("What can spawn. Weight is the % share of each pick; min guarantees some, max caps them per raid.")]
    public List<SpawnTable.Entry> entries = new List<SpawnTable.Entry>();

    public int MinCount => Mathf.Max(0, Mathf.Min(count.x, count.y));
    public int MaxCount => Mathf.Max(count.x, count.y);

    /// <summary>True when everything the list can place is a creature, so enemy density scales its count.</summary>
    public bool SpawnsEnemies => SpawnTable.AllCreatures(entries);

    /// <summary>The count range after the raid's enemy density, before limiting to the points.</summary>
    public Vector2Int CountFor(SpawnRules rules)
    {
        return SpawnsEnemies ? new Vector2Int(rules.Scale(MinCount), rules.Scale(MaxCount)) : new Vector2Int(MinCount, MaxCount);
    }

    /// <summary>What can spawn at a raid tier. Weights are relative, so those left share the picks.</summary>
    public List<SpawnTable.Entry> EntriesFor(int tier) => entries.FindAll(entry => entry.AppearsAt(tier));

    /// <summary>The entry's share of a single pick, 0 to 1, before caps.</summary>
    public float ChanceOf(SpawnTable.Entry entry)
    {
        float total = TotalWeight(entries);
        return total > 0f && entry.prefab != null ? entry.weight / total : 0f;
    }

    public static float TotalWeight(IReadOnlyList<SpawnTable.Entry> entries)
    {
        float total = 0f;
        foreach (SpawnTable.Entry entry in entries)
        {
            total += entry.prefab != null ? entry.weight : 0f;
        }

        return total;
    }

    /// <summary>
    /// Sets one entry's share in percent and rescales the others proportionally so the list still totals 100. Others at
    /// 0 share the rest evenly. A single entry is always 100.
    /// </summary>
    public static void SetShare(IList<SpawnTable.Entry> entries, int index, float percent)
    {
        if (index < 0 || index >= entries.Count)
        {
            return;
        }

        if (entries.Count == 1)
        {
            entries[0].weight = 100f;
            return;
        }

        percent = Mathf.Clamp(percent, 0f, 100f);
        float othersTotal = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i != index)
            {
                othersTotal += Mathf.Max(0f, entries[i].weight);
            }
        }

        float rest = 100f - percent;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i == index)
            {
                continue;
            }

            entries[i].weight = othersTotal > 0f ? Mathf.Max(0f, entries[i].weight) / othersTotal * rest : rest / (entries.Count - 1);
        }

        entries[index].weight = percent;
    }

    /// <summary>Rescales all weights to percentages totalling 100 (evenly when all are 0); keeps an empty list empty.</summary>
    public static void Normalize(IList<SpawnTable.Entry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        float total = 0f;
        foreach (SpawnTable.Entry entry in entries)
        {
            total += Mathf.Max(0f, entry.weight);
        }

        foreach (SpawnTable.Entry entry in entries)
        {
            entry.weight = total > 0f ? Mathf.Max(0f, entry.weight) / total * 100f : 100f / entries.Count;
        }
    }
}
