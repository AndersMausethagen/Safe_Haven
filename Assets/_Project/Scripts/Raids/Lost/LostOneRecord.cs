using System.Collections.Generic;

/// <summary>Whether one of the Lost still walks the mist. Saved as a number: add new ones at the end and never renumber.</summary>
public enum LostOneStatus
{
    Walking = 0,
    LaidToRest = 1,
}

/// <summary>
/// One of the Lost as this character knows it: who it was, how far it has grown, what it took from the player and what the player has
/// learned of it. Plain data, kept in the character's roster (CharacterProgress.LostOnes) and saved with it, so a failed raid never takes
/// back what it did; the rules that change it are LostOneRules'. Traits are kept by their affix asset's name, so a record outlives a
/// renamed or removed affix (it is simply left out when it walks).
/// </summary>
public sealed class LostOneRecord
{
    /// <summary>Stable for the character's whole game ("lost-3"): its body is chosen from it, and the journal remembers it by it.</summary>
    public string Id { get; set; } = "";
    public string GivenName { get; set; } = "";
    public string Epithet { get; set; } = "";
    /// <summary>Who it was before the mist, as "once ...".</summary>
    public string PastLife { get; set; } = "";
    /// <summary>1 to the rank table's length. Kept as saved; the rules clamp it where they read it.</summary>
    public int Rank { get; set; } = 1;
    /// <summary>Its elite affixes, by asset name, in the order it gained them.</summary>
    public List<string> Traits { get; } = new List<string>();
    /// <summary>The traits the player has seen through (one each time it flees; all once it is laid to rest).</summary>
    public List<string> Learned { get; } = new List<string>();
    /// <summary>What it took from the player when it killed them, the most valuable kept; dropped where it is laid to rest.</summary>
    public List<ItemStack> Carried { get; } = new List<ItemStack>();
    /// <summary>How many times it has killed the player.</summary>
    public int Kills { get; set; }
    /// <summary>How many times it has fled the player into the mist.</summary>
    public int Escapes { get; set; }
    /// <summary>How many raids the player met it on.</summary>
    public int RaidsMet { get; set; }
    public LostOneStatus Status { get; set; }
    /// <summary>The player has met it: the journal lists it and the briefing names it.</summary>
    public bool Met { get; set; }
    /// <summary>The roster's homecoming count when it was laid to rest: its place is held open a while after (LostOneRules.Refill).</summary>
    public int RestedAtHomecoming { get; set; }
    /// <summary>The raid it was last met in, for the journal.</summary>
    public string LastSeenIn { get; set; } = "";
    /// <summary>What the journal says of it once it is laid to rest.</summary>
    public string Epitaph { get; set; } = "";

    /// <summary>"Halvard Hollow-Eyed".</summary>
    public string FullName => string.IsNullOrWhiteSpace(Epithet) ? GivenName : $"{GivenName} {Epithet}";

    public bool IsWalking => Status == LostOneStatus.Walking;

    public bool Knows(string trait) => Learned.Contains(trait);
}

/// <summary>
/// A character's Lost: every one that has risen, walking or laid to rest (the journal's record), and the counts the rules need: the seed
/// they are born from, the raids' ends so far and how many have risen. Runtime state in CharacterProgress, saved by SaveMapper; changed
/// only through LostOneRules and RaidLostOnes.
/// </summary>
public sealed class LostOneRoster
{
    private readonly List<LostOneRecord> records = new List<LostOneRecord>();

    /// <summary>Every one that has risen, oldest first.</summary>
    public IReadOnlyList<LostOneRecord> All => records;

    /// <summary>Rolled when the first rises (0 until then): each new one is born from it and its number.</summary>
    public int Seed { get; set; }

    /// <summary>Raids' ends (homecomings) since the game began: what a laid-to-rest one's held-open place counts against.</summary>
    public int Homecomings { get; set; }

    /// <summary>How many have ever risen: the next one's number.</summary>
    public int Risen { get; set; }

    public int WalkingCount
    {
        get
        {
            int count = 0;
            foreach (LostOneRecord record in records)
            {
                count += record.IsWalking ? 1 : 0;
            }

            return count;
        }
    }

    /// <summary>True when the player has met any of them (the journal shows its tab).</summary>
    public bool AnyMet => records.Exists(record => record.Met);

    public LostOneRecord Find(string id) => string.IsNullOrEmpty(id) ? null : records.Find(record => record.Id == id);

    public void Add(LostOneRecord record)
    {
        if (record != null)
        {
            records.Add(record);
        }
    }

    /// <summary>Puts back a saved roster (loading).</summary>
    public void Restore(IEnumerable<LostOneRecord> saved, int seed, int homecomings, int risen)
    {
        records.Clear();
        if (saved != null)
        {
            foreach (LostOneRecord record in saved)
            {
                Add(record);
            }
        }

        Seed = seed;
        Homecomings = homecomings;
        Risen = risen;
    }
}
