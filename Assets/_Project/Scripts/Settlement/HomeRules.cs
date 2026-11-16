using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who sleeps in which house (Docs/BuildingSystemResearch.md §3.4.1), kept pure so it can be tested: the player's choices
/// first, in order of arrival; then everyone keeps the house they live in (homes are sticky, review 2026-11-16: a nearer
/// Cottage built later no longer moves someone out of the house the player furnished for them), unless a free bed is
/// clearly better, at least <c>moveFor</c> metres nearer their work (HomeLifeSettings › Move Home For), the biggest
/// gains first; then everyone without a bed (newcomers, or their house is gone) to the nearest free bed to their work,
/// the closest pairs first. Anyone left over sleeps at the home spots by the portal, as before houses were lived in. The
/// same houses and people always give the same answer, so every machine in co-op works out the same homes.
/// </summary>
public static class HomeRules
{
    /// <summary>A house with beds: its building's id, where its door is (on the ground) and how many sleep in it.</summary>
    public readonly struct House
    {
        public readonly string Id;
        public readonly Vector2 Door;
        public readonly int Beds;

        public House(string id, Vector2 door, int beds)
        {
            Id = id;
            Door = door;
            Beds = beds;
        }
    }

    /// <summary>Someone who lives here: who, the house the player chose for them (or empty), where they work (or the
    /// middle of the Haven when they have no work), and the house they live in now (or empty).</summary>
    public readonly struct Person
    {
        public readonly string Id;
        public readonly string Chosen;
        public readonly Vector2 Work;
        public readonly string Current;

        public Person(string id, string chosen, Vector2 work, string current = "")
        {
            Id = id;
            Chosen = chosen;
            Work = work;
            Current = current;
        }
    }

    /// <summary>Each person's house id, in the order given; null for someone without a bed. Nobody keeps a house they live in
    /// (as if everyone were new): see the overload with <c>moveFor</c>.</summary>
    public static string[] Assign(IReadOnlyList<House> houses, IReadOnlyList<Person> people) => Assign(houses, people, -1f);

    /// <summary>
    /// Each person's house id, in the order given; null for someone without a bed. With <paramref name="moveFor"/> at 0 or
    /// more, everyone keeps the house they live in (Person.Current) unless a free bed is that many metres nearer their work.
    /// </summary>
    public static string[] Assign(IReadOnlyList<House> houses, IReadOnlyList<Person> people, float moveFor)
    {
        var homes = new string[people.Count];
        var free = new int[houses.Count];
        for (int h = 0; h < houses.Count; h++)
        {
            free[h] = Mathf.Max(0, houses[h].Beds);
        }

        // The player's word first: whoever arrived first keeps a bed the player gave two people.
        for (int p = 0; p < people.Count; p++)
        {
            int chosen = IndexOf(houses, people[p].Chosen);
            if (chosen >= 0 && free[chosen] > 0)
            {
                homes[p] = houses[chosen].Id;
                free[chosen]--;
            }
        }

        if (moveFor >= 0f)
        {
            Keep(houses, people, moveFor, homes, free);
        }

        // Then the nearest free bed to each one's work, the shortest walks settled first.
        var pairs = new List<(float distance, int person, int house)>();
        for (int p = 0; p < people.Count; p++)
        {
            if (homes[p] != null)
            {
                continue;
            }

            for (int h = 0; h < houses.Count; h++)
            {
                if (free[h] > 0)
                {
                    pairs.Add(((houses[h].Door - people[p].Work).sqrMagnitude, p, h));
                }
            }
        }

        pairs.Sort((a, b) => a.distance != b.distance ? a.distance.CompareTo(b.distance)
            : a.person != b.person ? a.person.CompareTo(b.person) : string.CompareOrdinal(houses[a.house].Id, houses[b.house].Id));
        foreach ((float _, int person, int house) in pairs)
        {
            if (homes[person] == null && free[house] > 0)
            {
                homes[person] = houses[house].Id;
                free[house]--;
            }
        }

        return homes;
    }

    // Everyone keeps the house they live in, then moves only for a free bed clearly nearer their work: the biggest gain first,
    // and the bed they leave is free for whoever comes after.
    private static void Keep(IReadOnlyList<House> houses, IReadOnlyList<Person> people, float moveFor, string[] homes, int[] free)
    {
        var kept = new int[people.Count];
        for (int p = 0; p < people.Count; p++)
        {
            kept[p] = -1;
            int current = homes[p] == null ? IndexOf(houses, people[p].Current) : -1;
            if (current >= 0 && free[current] > 0)
            {
                homes[p] = houses[current].Id;
                free[current]--;
                kept[p] = current;
            }
        }

        var moves = new List<(float gain, int person, int house)>();
        for (int p = 0; p < people.Count; p++)
        {
            if (kept[p] < 0)
            {
                continue;
            }

            float now = Vector2.Distance(houses[kept[p]].Door, people[p].Work);
            for (int h = 0; h < houses.Count; h++)
            {
                float gain = now - Vector2.Distance(houses[h].Door, people[p].Work);
                if (h != kept[p] && free[h] > 0 && gain >= moveFor && gain > 0f)
                {
                    moves.Add((gain, p, h));
                }
            }
        }

        moves.Sort((a, b) => a.gain != b.gain ? b.gain.CompareTo(a.gain)
            : a.person != b.person ? a.person.CompareTo(b.person) : string.CompareOrdinal(houses[a.house].Id, houses[b.house].Id));
        var moved = new bool[people.Count];
        foreach ((float _, int person, int house) in moves)
        {
            if (!moved[person] && free[house] > 0)
            {
                moved[person] = true;
                free[kept[person]]++;
                free[house]--;
                homes[person] = houses[house].Id;
            }
        }
    }

    private static int IndexOf(IReadOnlyList<House> houses, string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return -1;
        }

        for (int h = 0; h < houses.Count; h++)
        {
            if (houses[h].Id == id)
            {
                return h;
            }
        }

        return -1;
    }
}
