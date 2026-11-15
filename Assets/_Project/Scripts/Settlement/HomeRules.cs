using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Who sleeps in which house (Docs/BuildingSystemResearch.md §3.4.1), kept pure so it can be tested: the player's choices
/// first, in order of arrival, then everyone else to the nearest free bed to their work, the closest pairs first. Anyone
/// left over sleeps at the home spots by the portal, as before houses were lived in. The same houses and people always
/// give the same answer, so every machine in co-op works out the same homes.
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

    /// <summary>Someone who lives here: who, the house the player chose for them (or empty), and where they work (or the
    /// middle of the Haven when they have no work).</summary>
    public readonly struct Person
    {
        public readonly string Id;
        public readonly string Chosen;
        public readonly Vector2 Work;

        public Person(string id, string chosen, Vector2 work)
        {
            Id = id;
            Chosen = chosen;
            Work = work;
        }
    }

    /// <summary>Each person's house id, in the order given; null for someone without a bed.</summary>
    public static string[] Assign(IReadOnlyList<House> houses, IReadOnlyList<Person> people)
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
