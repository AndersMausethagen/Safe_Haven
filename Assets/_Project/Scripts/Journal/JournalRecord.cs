using System.Collections.Generic;

/// <summary>One raid, as the journal remembers it. Plain words and numbers: it outlives whatever it was about.</summary>
public sealed class RaidRecord
{
    public string Raid;
    public string Outcome;
    public int Seconds;
    public int Value;
    public string BroughtDownBy;
    public List<string> Lines = new List<string>();
}

/// <summary>
/// What the player has come to know, for the journal, beyond the creature counts in CharacterProgress: pages read and announced, torn pages found in
/// raids, what has killed them, the quest pinned to the tracker and the raids they have been on. Runtime state, kept in CharacterProgress and saved with it.
/// </summary>
public sealed class JournalRecord
{
    /// <summary>The most raids it remembers: the oldest give way.</summary>
    public const int MostRaids = 60;

    private readonly HashSet<string> read = new HashSet<string>();
    private readonly HashSet<string> announced = new HashSet<string>();
    private readonly HashSet<string> found = new HashSet<string>();
    private readonly HashSet<string> killedBy = new HashSet<string>();
    private readonly HashSet<string> tried = new HashSet<string>();
    private readonly List<RaidRecord> raids = new List<RaidRecord>();

    public string PinnedQuest { get; set; }
    public IEnumerable<string> Read => read;
    public IEnumerable<string> Announced => announced;
    public IEnumerable<string> Found => found;
    public IEnumerable<string> KilledBy => killedBy;
    /// <summary>The skills the player has tried at least once (by id), which open their mechanics pages.</summary>
    public IEnumerable<string> Tried => tried;
    public IReadOnlyList<RaidRecord> Raids => raids;

    public bool HasRead(string key) => read.Contains(key);
    public void MarkRead(string key) => read.Add(key);
    public bool HasFound(string page) => found.Contains(page);
    public void NoteFound(string page) => found.Add(page);
    public bool WasKilledBy(string creatureId) => creatureId != null && killedBy.Contains(creatureId);
    public void NoteKilledBy(string creatureId) => killedBy.Add(creatureId);
    public bool HasTried(string skillId) => skillId != null && tried.Contains(skillId);
    /// <summary>True the first time a skill is tried.</summary>
    public bool NoteTried(string skillId) => skillId != null && tried.Add(skillId);

    /// <summary>True the first time a page is told about.</summary>
    public bool Announce(string page) => announced.Add(page);

    public void AddRaid(RaidRecord raid)
    {
        raids.Add(raid);
        if (raids.Count > MostRaids)
        {
            raids.RemoveAt(0);
        }
    }

    public void Restore(IEnumerable<string> savedRead, IEnumerable<string> savedAnnounced, IEnumerable<string> savedFound, IEnumerable<string> savedKilledBy, IEnumerable<RaidRecord> savedRaids, string pinned,
        IEnumerable<string> savedTried = null)
    {
        read.Clear();
        tried.Clear();
        tried.UnionWith(savedTried ?? new string[0]);
        announced.Clear();
        found.Clear();
        killedBy.Clear();
        raids.Clear();
        read.UnionWith(savedRead);
        announced.UnionWith(savedAnnounced);
        found.UnionWith(savedFound);
        killedBy.UnionWith(savedKilledBy);
        raids.AddRange(savedRaids);
        PinnedQuest = pinned;
    }
}
