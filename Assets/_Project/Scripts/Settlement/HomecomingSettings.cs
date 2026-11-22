using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One good thing that happened while the player was away (the Keeper's view, Docs/ColonyModeResearch_2026-11-21.md §4.5, after
/// Darkest Dungeon's town events, only ever kind): on coming home from a raid, with a chance, one gift is drawn from those whose job
/// someone at home has; its item goes into the chests and its line heads the homecoming card. Lives at Resources/HomecomingSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Homecoming Settings", fileName = "HomecomingSettings")]
public sealed class HomecomingSettings : GameSettings<HomecomingSettings>
{
    [Serializable]
    public sealed class Gift
    {
        [Tooltip("Someone at home with this job gives it (their name is {name} in the line).")]
        public JobDefinition job;
        public ItemDefinition item;
        [Min(1)] public int amount = 1;
        [TextArea(1, 3)] public string line = "{name} made something while you were away.";
        [Min(0f)] public float weight = 1f;
    }

    [Tooltip("How likely a gift is on coming home from a raid.")]
    [SerializeField, Range(0f, 1f)] private float chance = 0.6f;
    [SerializeField] private List<Gift> gifts = new List<Gift>();

    public float Chance => chance;
    public IReadOnlyList<Gift> Gifts => gifts;
}

/// <summary>Draws the homecoming's gift (HomecomingSettings). Plain rules apart from the stash it fills; HomecomingTests.</summary>
public static class AwayGifts
{
    /// <summary>
    /// The gift's line (with what it gave) and its item put in the chests, or null when none comes: by chance, from the gifts whose job
    /// someone at home has (not one away with the player), weighted. <paramref name="roll"/> and <paramref name="pick"/> are 0-1.
    /// </summary>
    public static string Draw(HomecomingSettings settings, SettlementState home, StashStorage stash, float roll, float pick, ResidentDefinition away = null)
    {
        if (settings == null || home == null || stash == null || roll >= settings.Chance)
        {
            return null;
        }

        var open = new List<(HomecomingSettings.Gift gift, ResidentRecord who)>();
        float total = 0f;
        foreach (HomecomingSettings.Gift gift in settings.Gifts)
        {
            ResidentRecord who = gift != null && gift.item != null && gift.weight > 0f ? WithJob(home, gift.job, away) : null;
            if (who != null)
            {
                open.Add((gift, who));
                total += gift.weight;
            }
        }

        float at = Mathf.Clamp01(pick) * total;
        foreach ((HomecomingSettings.Gift gift, ResidentRecord who) in open)
        {
            at -= gift.weight;
            if (at > 0f)
            {
                continue;
            }

            int given = stash.Items.Add(gift.item, gift.amount);
            if (given <= 0)
            {
                return null;
            }

            string name = who.Resident != null ? who.Resident.DisplayName : "Someone";
            return $"{gift.line.Replace("{name}", name)} (+{given} {gift.item.DisplayName})";
        }

        return null;
    }

    private static ResidentRecord WithJob(SettlementState home, JobDefinition job, ResidentDefinition away)
    {
        foreach (ResidentRecord record in home.Residents)
        {
            if (record != null && record.Job == job && job != null && record.Resident != away)
            {
                return record;
            }
        }

        return null;
    }
}
