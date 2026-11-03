using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How far a companion has come with the player. Each rank is reached by raids together, and makes them a little more use in a fight.</summary>
[Serializable]
public sealed class CompanionBondRank
{
    public string name = "Trusted";
    [Tooltip("Raids together to reach it.")]
    [Min(0)] public int raids = 3;
    [Tooltip("Share more health on a raid: 0.15 is 15%.")]
    [Min(0f)] public float healthBonus;
    [Tooltip("Share more damage on a raid.")]
    [Min(0f)] public float damageBonus;
}

/// <summary>What a companion will bring up when spoken to at home, from the raid just gone.</summary>
public enum CompanionMemory
{
    None = 0,
    Failed = 1,
    HardFight = 2,
    CloseCall = 3,
    RoseInRank = 4,
    HelpedUp = 5,
    BossBeaten = 6,
}

/// <summary>Lines for one kind of memory. One is kept per person, so they stay in character.</summary>
[Serializable]
public sealed class CompanionMemoryLines
{
    public CompanionMemory kind;
    [TextArea] public string[] lines = new string[0];
}

/// <summary>
/// The rules of going along together (GDD §15.4, §15.6): raids make a bond, a bond is a rank, and each raid leaves one thing worth saying.
/// The numbers and lines are data: Settlement Settings › Going Along on Raids, and the person's own lines on their ResidentDefinition.
/// </summary>
public static class CompanionBond
{
    private const int HardFightKills = 4;
    private const float CloseCallSeconds = 60f;

    /// <summary>The highest rank these raids reach, or null with none.</summary>
    public static CompanionBondRank RankFor(int raids, IReadOnlyList<CompanionBondRank> ranks)
    {
        CompanionBondRank best = null;
        if (ranks == null)
        {
            return null;
        }

        foreach (CompanionBondRank rank in ranks)
        {
            if (rank != null && raids >= rank.raids && (best == null || rank.raids > best.raids))
            {
                best = rank;
            }
        }

        return best;
    }

    /// <summary>What stays with them from a raid: the biggest thing that happened.</summary>
    public static CompanionMemory Remember(bool extracted, int bossesBeaten, int timesHelpedUp, bool roseInRank, float secondsLeft, int kills)
    {
        if (bossesBeaten > 0)
        {
            return CompanionMemory.BossBeaten;
        }

        if (timesHelpedUp > 0)
        {
            return CompanionMemory.HelpedUp;
        }

        if (!extracted)
        {
            return CompanionMemory.Failed;
        }

        if (roseInRank)
        {
            return CompanionMemory.RoseInRank;
        }

        if (secondsLeft <= CloseCallSeconds)
        {
            return CompanionMemory.CloseCall;
        }

        return kills >= HardFightKills ? CompanionMemory.HardFight : CompanionMemory.None;
    }

    /// <summary>
    /// All they lose when the forest takes them, in the order it comes back: the biggest thing last, as the enum runs. Whatever they had
    /// already forgotten and not got back stays forgotten.
    /// </summary>
    public static List<CompanionMemory> WhatIsForgotten(IEnumerable<CompanionMemory> remembered, IEnumerable<CompanionMemory> alreadyForgotten)
    {
        var forgotten = new List<CompanionMemory>();
        foreach (IEnumerable<CompanionMemory> list in new[] { remembered, alreadyForgotten })
        {
            foreach (CompanionMemory memory in list)
            {
                if (memory != CompanionMemory.None && !forgotten.Contains(memory))
                {
                    forgotten.Add(memory);
                }
            }
        }

        forgotten.Sort();
        return forgotten;
    }

    /// <summary>True when raids together have brought something back: one for every so many.</summary>
    public static bool MemoryComesBack(int raidsAlong, int every, int forgottenCount)
    {
        return every > 0 && forgottenCount > 0 && raidsAlong > 0 && raidsAlong % every == 0;
    }

    /// <summary>Their own line for it, else the general one, or null with neither. The same person always says the same thing.</summary>
    public static string LineFor(CompanionMemory memory, string personId, IReadOnlyList<CompanionMemoryLines> own, IReadOnlyList<CompanionMemoryLines> general)
    {
        return Pick(memory, personId, own) ?? Pick(memory, personId, general);
    }

    /// <summary>The same index for the same person every time. Not GetHashCode: it changes between runs, and they should keep to their words.</summary>
    public static int StableIndex(string personId, int count)
    {
        int seed = 0;
        foreach (char letter in personId ?? "")
        {
            seed = unchecked(seed * 31 + letter);
        }

        return count > 0 ? (seed % count + count) % count : 0;
    }

    private static string Pick(CompanionMemory memory, string personId, IReadOnlyList<CompanionMemoryLines> from)
    {
        if (from == null || memory == CompanionMemory.None)
        {
            return null;
        }

        foreach (CompanionMemoryLines entry in from)
        {
            if (entry != null && entry.kind == memory && entry.lines != null && entry.lines.Length > 0)
            {
                return entry.lines[StableIndex(personId, entry.lines.Length)];
            }
        }

        return null;
    }
}
