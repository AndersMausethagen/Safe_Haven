using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The host's stash, shared with a visiting guest the host lets use it (co-op). The host's stash is the real one:
/// <list type="bullet">
/// <item>The host sends its whole stash (<see cref="Snapshot"/>) when a guest is let in and whenever it changes.</item>
/// <item>A guest's stash is a copy. What they put in or take out goes to the host as a change (<see cref="Change"/>):
/// so many of this item, in this condition, in or out. Never a whole stash, so the host's own workshops and residents
/// can keep filling it at the same time.</item>
/// <item>Each change is numbered. A guest takes on the host's stash only once it includes their latest change, or a stack
/// just taken could show up again for a moment and be taken twice.</item>
/// </list>
/// Plain C#: <see cref="CoopSession"/> does the sending.
/// </summary>
public sealed class StashShare
{
    [Serializable]
    public sealed class Snapshot
    {
        /// <summary>The last of this guest's changes the stash includes.</summary>
        public int applied;
        public StashSaveData stash = new StashSaveData();
    }

    [Serializable]
    public sealed class Change
    {
        public int number;
        /// <summary>Stacks by kind and condition; a negative count was taken out.</summary>
        public List<SlotSaveData> items = new List<SlotSaveData>();
        public List<AmountSaveData> resources = new List<AmountSaveData>();

        public bool IsEmpty => items.Count == 0 && resources.Count == 0;
    }

    // A guest's copy as the host last sent it, by kind; what was sent since counts as included.
    private Dictionary<string, int> itemsBefore = new Dictionary<string, int>();
    private Dictionary<string, int> resourcesBefore = new Dictionary<string, int>();
    private int sent;
    private bool hasCopy;
    private int hostMark;

    /// <summary>True once a guest has the host's stash to use.</summary>
    public bool HasCopy => hasCopy;

    public void Reset()
    {
        itemsBefore.Clear();
        resourcesBefore.Clear();
        sent = 0;
        hasCopy = false;
        hostMark = 0;
    }

    // ---- Host

    /// <summary>True when the host's stash changed since this was last asked (it is then sent to the guests using it).</summary>
    public bool HostStashChanged(StashStorage stash)
    {
        int mark = Mark(SaveMapper.ToSlots(stash.Items), Amounts(stash.Resources));
        if (mark == hostMark)
        {
            return false;
        }

        hostMark = mark;
        return true;
    }

    public static Snapshot SnapshotOf(StashStorage stash, int applied)
    {
        var snapshot = new Snapshot { applied = applied };
        snapshot.stash.items = SaveMapper.ToSlots(stash.Items);
        snapshot.stash.resources = Amounts(stash.Resources);
        return snapshot;
    }

    /// <summary>A guest's change on the host's stash. What can't be done (the stash got full, or what they took is gone) is logged.</summary>
    public static void ApplyChange(Change change, StashStorage stash, IContentLookup content)
    {
        foreach (SlotSaveData kind in change.items)
        {
            ItemDefinition item = content.FindItem(kind.item);
            if (item == null || kind.count == 0)
            {
                continue;
            }

            if (kind.count > 0)
            {
                var stack = new List<SlotSaveData> { new SlotSaveData { slot = -1, item = kind.item, count = kind.count, wear = kind.wear, empower = kind.empower, enchantment = kind.enchantment, quality = kind.quality, maker = kind.maker } };
                int before = stash.Items.Count(item);
                SaveMapper.ReadSlots(stack, stash.Items, content, new List<string>());
                if (stash.Items.Count(item) - before < kind.count)
                {
                    Debug.LogWarning($"Co-op: the stash was too full for all of a friend's {item.DisplayName}.");
                }
            }
            else
            {
                int left = Take(stash.Items, kind, -kind.count);
                left -= stash.Items.Remove(item, left);
                if (left > 0)
                {
                    Debug.LogWarning($"Co-op: a friend took {left} {item.DisplayName} the stash no longer had.");
                }
            }
        }

        foreach (AmountSaveData amount in change.resources)
        {
            ResourceDefinition resource = content.FindResource(amount.id);
            if (resource == null || amount.amount == 0)
            {
                continue;
            }

            if (amount.amount > 0)
            {
                stash.Resources.Add(resource, amount.amount);
            }
            else
            {
                stash.Resources.Remove(resource, -amount.amount);
            }
        }
    }

    // Takes stacks of exactly this kind and condition first. Returns how many are still to take.
    private static int Take(ItemContainer from, SlotSaveData kind, int count)
    {
        for (int i = 0; i < from.SlotCount && count > 0; i++)
        {
            ItemStack stack = from.GetSlot(i);
            if (!stack.IsEmpty && Key(KindOf(stack)) == Key(kind))
            {
                count -= from.RemoveAt(i, Mathf.Min(count, stack.Count));
            }
        }

        return count;
    }

    private static SlotSaveData KindOf(ItemStack stack) => new SlotSaveData
    {
        item = stack.Item.Id,
        count = stack.Count,
        wear = 1f - stack.Durability,
        empower = stack.Empower,
        enchantment = stack.Enchantment != null ? stack.Enchantment.Id : "",
        quality = (int)stack.Quality,
        maker = stack.Maker,
    };

    // ---- Guest

    /// <summary>The host's stash, taken on unless it doesn't yet include this guest's latest change.</summary>
    public bool TakeSnapshot(Snapshot snapshot, StashStorage stash, IContentLookup content)
    {
        if (snapshot?.stash == null || snapshot.applied < sent)
        {
            return false;
        }

        stash.Items.Clear();
        SaveMapper.ReadSlots(snapshot.stash.items ?? new List<SlotSaveData>(), stash.Items, content, new List<string>());
        var keep = new HashSet<string>();
        foreach (AmountSaveData amount in snapshot.stash.resources ?? new List<AmountSaveData>())
        {
            ResourceDefinition resource = content.FindResource(amount.id);
            if (resource != null)
            {
                stash.Resources.SetAmount(resource, amount.amount);
                keep.Add(amount.id);
            }
        }

        foreach (ResourceAmount stored in new List<ResourceAmount>(stash.Resources.StoredAmounts))
        {
            if (!keep.Contains(stored.Resource.Id))
            {
                stash.Resources.SetAmount(stored.Resource, 0);
            }
        }

        itemsBefore = Count(SaveMapper.ToSlots(stash.Items));
        resourcesBefore = Count(Amounts(stash.Resources));
        hasCopy = true;
        return true;
    }

    /// <summary>What this guest put in or took out since the last look, numbered, or null when nothing.</summary>
    public Change CollectChange(StashStorage stash)
    {
        if (!hasCopy)
        {
            return null;
        }

        Dictionary<string, int> items = Count(SaveMapper.ToSlots(stash.Items));
        Dictionary<string, int> resources = Count(Amounts(stash.Resources));
        var change = new Change();
        foreach ((string key, int difference) in Differences(itemsBefore, items))
        {
            SlotSaveData kind = FromKey(key);
            kind.count = difference;
            change.items.Add(kind);
        }

        foreach ((string key, int difference) in Differences(resourcesBefore, resources))
        {
            change.resources.Add(new AmountSaveData { id = key, amount = difference });
        }

        if (change.IsEmpty)
        {
            return null;
        }

        itemsBefore = items;
        resourcesBefore = resources;
        change.number = ++sent;
        return change;
    }

    // ---- Counting by kind

    // An item's kind: what it is and its condition, to the tenth of a percent of wear.
    private static string Key(SlotSaveData slot) =>
        $"{slot.item}|{Mathf.RoundToInt(slot.wear * 1000f)}|{slot.empower}|{slot.enchantment ?? ""}|{slot.quality}|{(slot.maker ?? "").Replace("|", "/")}";

    private static SlotSaveData FromKey(string key)
    {
        string[] parts = key.Split('|');
        return new SlotSaveData
        {
            slot = -1,
            item = parts[0],
            wear = int.Parse(parts[1]) / 1000f,
            empower = int.Parse(parts[2]),
            enchantment = parts.Length > 3 ? parts[3] : "",
            quality = parts.Length > 4 ? int.Parse(parts[4]) : 0,
            maker = parts.Length > 5 ? parts[5] : "",
        };
    }

    private static Dictionary<string, int> Count(List<SlotSaveData> slots)
    {
        var counts = new Dictionary<string, int>();
        foreach (SlotSaveData slot in slots)
        {
            string key = Key(slot);
            counts[key] = (counts.TryGetValue(key, out int had) ? had : 0) + slot.count;
        }

        return counts;
    }

    private static Dictionary<string, int> Count(List<AmountSaveData> amounts)
    {
        var counts = new Dictionary<string, int>();
        foreach (AmountSaveData amount in amounts)
        {
            counts[amount.id] = (counts.TryGetValue(amount.id, out int had) ? had : 0) + amount.amount;
        }

        return counts;
    }

    private static IEnumerable<(string key, int difference)> Differences(Dictionary<string, int> before, Dictionary<string, int> now)
    {
        foreach (KeyValuePair<string, int> pair in now)
        {
            int difference = pair.Value - (before.TryGetValue(pair.Key, out int had) ? had : 0);
            if (difference != 0)
            {
                yield return (pair.Key, difference);
            }
        }

        foreach (KeyValuePair<string, int> pair in before)
        {
            if (!now.ContainsKey(pair.Key) && pair.Value != 0)
            {
                yield return (pair.Key, -pair.Value);
            }
        }
    }

    private static List<AmountSaveData> Amounts(ResourceStorage storage)
    {
        var amounts = new List<AmountSaveData>();
        foreach (ResourceAmount amount in storage.StoredAmounts)
        {
            if (amount.Amount > 0)
            {
                amounts.Add(new AmountSaveData { id = amount.Resource.Id, amount = amount.Amount });
            }
        }

        return amounts;
    }

    private static int Mark(List<SlotSaveData> items, List<AmountSaveData> resources)
    {
        unchecked
        {
            int mark = 17;
            foreach (SlotSaveData slot in items)
            {
                mark = mark * 31 + slot.slot;
                mark = mark * 31 + Key(slot).GetHashCode();
                mark = mark * 31 + slot.count;
            }

            foreach (AmountSaveData amount in resources)
            {
                mark = mark * 31 + amount.id.GetHashCode();
                mark = mark * 31 + amount.amount;
            }

            return mark;
        }
    }
}
