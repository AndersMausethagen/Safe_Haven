using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One timed bonus on the player, as snapshots carry it across scenes. Plain values, so nothing refers to a scene.</summary>
public readonly struct BoonRecord
{
    public readonly string Id;
    public readonly string Name;
    public readonly bool IsMeal;
    public readonly bool IsTonic;
    /// <summary>A weapon coating (Ember Pitch): its own slot, one at a time.</summary>
    public readonly bool IsCoating;
    public readonly float SecondsLeft;
    public readonly IReadOnlyList<StatBonus> Bonuses;
    /// <summary>Seconds more a meal keeps hunger from falling (SurvivalSettings › Meal Holds Hunger), 0 when it doesn't.</summary>
    public readonly float HungerHeldLeft;

    public BoonRecord(string id, string name, bool isMeal, float secondsLeft, IReadOnlyList<StatBonus> bonuses, bool isTonic = false,
        bool isCoating = false, float hungerHeldLeft = 0f)
    {
        Id = id;
        Name = name;
        IsMeal = isMeal;
        IsTonic = isTonic;
        IsCoating = isCoating;
        SecondsLeft = secondsLeft;
        Bonuses = bonuses;
        HungerHeldLeft = hungerHeldLeft;
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
        public bool IsTonic;
        public bool IsCoating;
        public float SecondsLeft;
        public IReadOnlyList<StatBonus> Bonuses;
        public float HungerHeldLeft;
    }

    private readonly List<Entry> active = new List<Entry>();
    private Combatant combatant;

    /// <summary>Raised when a boon starts or ends.</summary>
    public event Action Changed;

    /// <summary>How many boons are at work.</summary>
    public int Count => active.Count;

    /// <summary>Seconds more hunger is held where it is by a meal eaten (the longest of them), 0 when none: PlayerNeeds doesn't drain it.</summary>
    public float HungerHeldLeft
    {
        get
        {
            float longest = 0f;
            foreach (Entry entry in active)
            {
                longest = Mathf.Max(longest, entry.HungerHeldLeft);
            }

            return longest;
        }
    }

    /// <summary>What is at work, for the HUD and for carrying across scenes.</summary>
    public List<BoonRecord> Capture()
    {
        var records = new List<BoonRecord>(active.Count);
        foreach (Entry entry in active)
        {
            records.Add(new BoonRecord(entry.Id, entry.Name, entry.IsMeal, entry.SecondsLeft, entry.Bonuses, entry.IsTonic, entry.IsCoating,
                entry.HungerHeldLeft));
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

    /// <summary>
    /// Starts a boon, or restarts it when one with this id is at work. Returns false when it gives nothing. A coating (Ember Pitch) has a
    /// slot of its own beside the meals and the tonics: one coating at a time, and it shares no family with them, so it adds to a fed
    /// and dosed player rather than taking their food's place.
    /// </summary>
    /// <param name="holdsHunger">Seconds it keeps hunger from falling (a meal: SurvivalSettings › Meal Holds Hunger), 0 for none.</param>
    public bool Add(string id, string displayName, IReadOnlyList<StatBonus> bonuses, float seconds, bool isMeal, bool isTonic = false,
        bool isCoating = false, float holdsHunger = 0f)
    {
        if (bonuses == null || bonuses.Count == 0 || seconds <= 0f)
        {
            return false;
        }

        Remove(id);
        // One boon of each family at a time, meal or tonic (BoonFamily; the user, 2026-11-22: "one of each boon"): a Hearty tonic takes
        // a Hearty meal's place, whatever the slots. Extra slots hold other families: breadth, not a second helping of armour.
        string family = isMeal || isTonic ? BoonFamily.Of(bonuses) : "";
        for (int i = active.Count - 1; i >= 0 && family.Length > 0; i--)
        {
            if ((active[i].IsMeal || active[i].IsTonic) && BoonFamily.Of(active[i].Bonuses) == family)
            {
                GameMessages.Post($"{active[i].Name} gives way: one {family.ToLowerInvariant()} boon at a time.");
                Stop(i);
            }
        }

        if (isMeal)
        {
            MakeRoomForMeal();
        }
        else if (isTonic)
        {
            MakeRoomForTonic();
        }
        else if (isCoating)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].IsCoating)
                {
                    GameMessages.Post($"{active[i].Name} is wiped off for the new coating.");
                    Stop(i);
                }
            }
        }

        Begin(new Entry
        {
            Id = id, Name = displayName, IsMeal = isMeal, IsTonic = isTonic, IsCoating = isCoating, SecondsLeft = seconds, Bonuses = bonuses,
            HungerHeldLeft = Mathf.Min(Mathf.Max(0f, holdsHunger), seconds),
        });
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
            Begin(new Entry { Id = record.Id, Name = record.Name, IsMeal = record.IsMeal, IsTonic = record.IsTonic, IsCoating = record.IsCoating,
                SecondsLeft = record.SecondsLeft, Bonuses = record.Bonuses, HungerHeldLeft = record.HungerHeldLeft });
        }

        Changed?.Invoke();
    }

    private void Update()
    {
        bool ended = false;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            active[i].SecondsLeft -= Time.deltaTime;
            active[i].HungerHeldLeft = Mathf.Max(0f, active[i].HungerHeldLeft - Time.deltaTime);
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

        int slots = SurvivalSettings.Current.MealSlots + GameSession.Here.UpgradeBonus(level => level.extraMealSlots)
                    + (Techniques.Has(PerkTechnique.ThirdPlate) ? 1 : 0);
        if (Techniques.Has(PerkTechnique.ThirdPlate) && meals == slots - 1)
        {
            Techniques.Used(PerkTechnique.ThirdPlate); // the plate only the technique made room for
        }
        if (meals >= slots && shortest >= 0)
        {
            GameMessages.Post($"{active[shortest].Name} gives way to the new meal.");
            Stop(shortest);
        }
    }

    /// <summary>How many tonics can work at once: Survival Settings › Tonic Slots, the Cauldron's top upgrade, the Second Vial perk.</summary>
    public int TonicSlots => SurvivalSettings.Current.TonicSlots + GameSession.Here.UpgradeBonus(level => level.extraTonicSlots)
                             + (Techniques.Has(PerkTechnique.SecondVial) ? 1 : 0);

    // Tonics share their slots as meals do (Docs/PlayerStatsResearch.md #4): a new one pushes out the one closest to ending.
    private void MakeRoomForTonic()
    {
        int tonics = 0;
        int shortest = -1;
        for (int i = 0; i < active.Count; i++)
        {
            if (!active[i].IsTonic)
            {
                continue;
            }

            tonics++;
            if (shortest < 0 || active[i].SecondsLeft < active[shortest].SecondsLeft)
            {
                shortest = i;
            }
        }

        int slots = TonicSlots;
        if (Techniques.Has(PerkTechnique.SecondVial) && tonics == slots - 1)
        {
            Techniques.Used(PerkTechnique.SecondVial);
        }

        if (tonics >= slots && shortest >= 0)
        {
            GameMessages.Post($"{active[shortest].Name} gives way to the new tonic.");
            Stop(shortest);
        }
    }

    /// <summary>Ends every meal, tonic and coating at work (a fall under the death rules: HomeFalls); other boons and hindrances go
    /// on. Returns how many.</summary>
    public int EndMealsAndTonics()
    {
        int ended = 0;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (active[i].IsMeal || active[i].IsTonic || active[i].IsCoating)
            {
                Stop(i);
                ended++;
            }
        }

        if (ended > 0)
        {
            Changed?.Invoke();
        }

        return ended;
    }

    /// <summary>Ends a boon (or a hindrance such as the cold's Chilled) early, if it is at work.</summary>
    public void End(string id)
    {
        int before = active.Count;
        Remove(id);
        if (active.Count != before)
        {
            Changed?.Invoke();
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

    private void Begin(Entry entry) // not "Start": Unity takes that name for its lifecycle message and logs an error
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
