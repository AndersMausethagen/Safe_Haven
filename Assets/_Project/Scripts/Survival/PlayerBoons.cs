using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One timed bonus on the player, as snapshots carry it across scenes. Plain values, so nothing refers to a scene.</summary>
public readonly struct BoonRecord
{
    public readonly string Id;
    public readonly string Name;
    public readonly bool IsMeal;
    public readonly float SecondsLeft;
    public readonly IReadOnlyList<StatBonus> Bonuses;

    public BoonRecord(string id, string name, bool isMeal, float secondsLeft, IReadOnlyList<StatBonus> bonuses)
    {
        Id = id;
        Name = name;
        IsMeal = isMeal;
        SecondsLeft = secondsLeft;
        Bonuses = bonuses;
    }
}

/// <summary>
/// Timed bonuses on the player: what a cooked meal gives (GDD §11.4), a tonic, a hot sauna, a stranger's gift. Meals share
/// a few slots (SurvivalSettings › Meal Slots): another one pushes out the one closest to ending, which is what makes the
/// meal before a raid a choice. Each adds its StatBonus list to the player's stats for its time. Boons go with the player
/// through the portal (PlayerSnapshot), are lost on a failed raid, and aren't written to the save file.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBoons : MonoBehaviour
{
    private sealed class Entry
    {
        public string Id;
        public string Name;
        public bool IsMeal;
        public float SecondsLeft;
        public IReadOnlyList<StatBonus> Bonuses;
    }

    private readonly List<Entry> active = new List<Entry>();
    private Combatant combatant;

    /// <summary>Raised when a boon starts or ends.</summary>
    public event Action Changed;

    /// <summary>How many boons are at work.</summary>
    public int Count => active.Count;

    /// <summary>What is at work, for the HUD and for carrying across scenes.</summary>
    public List<BoonRecord> Capture()
    {
        var records = new List<BoonRecord>(active.Count);
        foreach (Entry entry in active)
        {
            records.Add(new BoonRecord(entry.Id, entry.Name, entry.IsMeal, entry.SecondsLeft, entry.Bonuses));
        }

        return records;
    }

    private StatSheet Stats
    {
        get
        {
            if (combatant == null)
            {
                combatant = GetComponent<Combatant>();
            }

            return combatant != null ? combatant.Stats : null;
        }
    }

    /// <summary>Starts a boon, or restarts it when one with this id is at work. Returns false when it gives nothing.</summary>
    public bool Add(string id, string displayName, IReadOnlyList<StatBonus> bonuses, float seconds, bool isMeal)
    {
        if (bonuses == null || bonuses.Count == 0 || seconds <= 0f)
        {
            return false;
        }

        Remove(id);
        if (isMeal)
        {
            MakeRoomForMeal();
        }

        Start(new Entry { Id = id, Name = displayName, IsMeal = isMeal, SecondsLeft = seconds, Bonuses = bonuses });
        Changed?.Invoke();
        return true;
    }

    /// <summary>Replaces everything at work with what a snapshot carried.</summary>
    public void Restore(IReadOnlyList<BoonRecord> records)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Stop(i);
        }

        foreach (BoonRecord record in records)
        {
            Start(new Entry { Id = record.Id, Name = record.Name, IsMeal = record.IsMeal, SecondsLeft = record.SecondsLeft, Bonuses = record.Bonuses });
        }

        Changed?.Invoke();
    }

    private void Update()
    {
        bool ended = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            active[i].SecondsLeft -= Time.deltaTime;
            if (active[i].SecondsLeft <= 0f)
            {
                GameMessages.Post($"{active[i].Name} has worn off.");
                Stop(i);
                ended = true;
            }
        }

        if (ended)
        {
            Changed?.Invoke();
        }
    }

    private void MakeRoomForMeal()
    {
        int meals = 0;
        int shortest = -1;
        for (int i = 0; i < active.Count; i++)
        {
            if (!active[i].IsMeal)
            {
                continue;
            }

            meals++;
            if (shortest < 0 || active[i].SecondsLeft < active[shortest].SecondsLeft)
            {
                shortest = i;
            }
        }

        if (meals >= SurvivalSettings.Current.MealSlots && shortest >= 0)
        {
            GameMessages.Post($"{active[shortest].Name} gives way to the new meal.");
            Stop(shortest);
        }
    }

    private void Remove(string id)
    {
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].Id == id)
            {
                Stop(i);
            }
        }
    }

    private void Start(Entry entry)
    {
        active.Add(entry);
        StatSheet stats = Stats;
        if (stats == null)
        {
            return;
        }

        foreach (StatBonus bonus in entry.Bonuses)
        {
            stats.AddModifier(new StatModifier(bonus.stat, bonus.op, bonus.value, entry));
        }
    }

    private void Stop(int index)
    {
        Stats?.RemoveModifiersFrom(active[index]);
        active.RemoveAt(index);
    }
}
