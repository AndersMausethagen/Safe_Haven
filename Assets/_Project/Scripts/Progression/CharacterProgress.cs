using System;
using System.Collections.Generic;

/// <summary>
/// The player's permanent growth: level, experience towards the next level and the ranks bought in each talent, plus
/// the items crafted at least once (for first-craft experience) and the experience stones already touched. Lives in GameSession (it belongs to the game, not to
/// the player object, so it survives every scene change) and is saved by SaveMapper. Plain C#, so the rules are
/// testable; the numbers come from LevelingSettings.
/// </summary>
public sealed class CharacterProgress
{
    private readonly Dictionary<string, int> ranks = new Dictionary<string, int>();
    private readonly HashSet<string> crafted = new HashSet<string>();
    private readonly HashSet<string> stones = new HashSet<string>();

    public int Level { get; private set; } = 1;

    /// <summary>Experience gathered towards the next level.</summary>
    public int Experience { get; private set; }

    /// <summary>Raised after anything changes: experience, a level, a talent.</summary>
    public event Action Changed;

    /// <summary>Raised with the new level each time one is reached.</summary>
    public event Action<int> LeveledUp;

    public IReadOnlyDictionary<string, int> Ranks => ranks;
    public IEnumerable<string> CraftedItems => crafted;
    public IEnumerable<string> UsedStones => stones;

    public int PointsEarned(LevelingSettings settings) => (Level - 1) * settings.TalentPointsPerLevel;

    public int PointsSpent
    {
        get
        {
            int spent = 0;
            foreach (int rank in ranks.Values)
            {
                spent += rank;
            }

            return spent;
        }
    }

    public int UnspentPoints(LevelingSettings settings) => Math.Max(0, PointsEarned(settings) - PointsSpent);

    public bool IsMaxLevel(LevelingSettings settings) => Level >= settings.MaxLevel;

    public int RankOf(TalentDefinition talent) => talent != null && ranks.TryGetValue(talent.Id, out int rank) ? rank : 0;

    /// <summary>Points spent in one tree, which opens its later rows.</summary>
    public int SpentIn(TalentTree tree, LevelingSettings settings)
    {
        int spent = 0;
        foreach (KeyValuePair<string, int> entry in ranks)
        {
            TalentDefinition talent = settings.FindTalent(entry.Key);
            if (talent != null && talent.Tree == tree)
            {
                spent += entry.Value;
            }
        }

        return spent;
    }

    /// <summary>Adds experience, levelling up as often as it reaches (never past the last level). Returns levels gained.</summary>
    public int AddExperience(int amount, LevelingSettings settings)
    {
        if (amount <= 0 || IsMaxLevel(settings))
        {
            return 0;
        }

        Experience += amount;
        int gained = 0;
        while (!IsMaxLevel(settings) && Experience >= settings.ExperienceToNext(Level))
        {
            Experience -= settings.ExperienceToNext(Level);
            Level++;
            gained++;
            LeveledUp?.Invoke(Level);
        }

        if (IsMaxLevel(settings))
        {
            Experience = 0;
        }

        Changed?.Invoke();
        return gained;
    }

    /// <summary>Whether one more rank of the talent can be bought now; reason says why not.</summary>
    public bool CanRaise(TalentDefinition talent, LevelingSettings settings, out string reason)
    {
        reason = null;
        if (talent == null)
        {
            reason = "No such talent.";
        }
        else if (RankOf(talent) >= talent.MaxRank)
        {
            reason = "Already at its highest rank.";
        }
        else if (UnspentPoints(settings) <= 0)
        {
            reason = "No talent points left. Level up to earn more.";
        }
        else if (SpentIn(talent.Tree, settings) < talent.Row * settings.PointsPerRow)
        {
            reason = $"Spend {talent.Row * settings.PointsPerRow} points in {talent.Tree} first.";
        }

        return reason == null;
    }

    public bool Raise(TalentDefinition talent, LevelingSettings settings)
    {
        if (!CanRaise(talent, settings, out _))
        {
            return false;
        }

        ranks[talent.Id] = RankOf(talent) + 1;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Gives back every talent point (respec).</summary>
    public void ResetTalents()
    {
        if (ranks.Count == 0)
        {
            return;
        }

        ranks.Clear();
        Changed?.Invoke();
    }

    /// <summary>True the first time an item is crafted (for first-craft experience).</summary>
    public bool MarkCrafted(string itemId) => !string.IsNullOrEmpty(itemId) && crafted.Add(itemId);

    /// <summary>True the first time an experience stone is touched: each gives its experience once per game, even in
    /// raids, whose scenes start fresh every time.</summary>
    public bool MarkStoneUsed(string stoneId) => !string.IsNullOrEmpty(stoneId) && stones.Add(stoneId);

    public bool HasUsedStone(string stoneId) => stones.Contains(stoneId);

    /// <summary>Puts back saved progress (loading).</summary>
    public void Restore(int level, int experience, IEnumerable<KeyValuePair<string, int>> savedRanks, IEnumerable<string> craftedItems,
        IEnumerable<string> usedStones = null)
    {
        Level = Math.Max(1, level);
        Experience = Math.Max(0, experience);
        ranks.Clear();
        if (savedRanks != null)
        {
            foreach (KeyValuePair<string, int> entry in savedRanks)
            {
                if (!string.IsNullOrEmpty(entry.Key) && entry.Value > 0)
                {
                    ranks[entry.Key] = entry.Value;
                }
            }
        }

        crafted.Clear();
        if (craftedItems != null)
        {
            crafted.UnionWith(craftedItems);
        }

        stones.Clear();
        if (usedStones != null)
        {
            stones.UnionWith(usedStones);
        }

        Changed?.Invoke();
    }
}
