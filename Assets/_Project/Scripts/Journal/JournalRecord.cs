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
    private readonly HashSet<string> discovered = new HashSet<string>();
    private readonly HashSet<string> noticed = new HashSet<string>();
    private readonly HashSet<string> seen = new HashSet<string>();
    private readonly List<RaidRecord> raids = new List<RaidRecord>();
    private readonly Dictionary<string, float> biggestFish = new Dictionary<string, float>();

    public string PinnedQuest { get; set; }
    public IEnumerable<string> Read => read;
    public IEnumerable<string> Announced => announced;
    public IEnumerable<string> Found => found;
    public IEnumerable<string> KilledBy => killedBy;
    /// <summary>The skills the player has tried at least once (by id), which open their mechanics pages.</summary>
    public IEnumerable<string> Tried => tried;
    /// <summary>The named places found on the map (WorldAtlas.Place ids).</summary>
    public IEnumerable<string> Discovered => discovered;
    /// <summary>The world's curious places the player has stopped to think about (CuriousPlace keys), so a thought isn't
    /// thought again after a load.</summary>
    public IEnumerable<string> Noticed => noticed;
    /// <summary>Named places seen from a lookout and not yet walked to (WorldAtlas.Place ids): on the map in grey.</summary>
    public IEnumerable<string> Seen => seen;
    public IReadOnlyList<RaidRecord> Raids => raids;

    /// <summary>The biggest of each kind of fish caught (item id to centimetres), for the journal's fishing page.</summary>
    public IReadOnlyDictionary<string, float> BiggestFish => biggestFish;

    /// <summary>Notes a catch's length: true when it is the biggest of its kind yet (<paramref name="before"/>: the last
    /// record, 0 for the first of its kind).</summary>
    public bool NoteCatch(string fishId, float centimetres, out float before)
    {
        before = 0f;
        if (string.IsNullOrEmpty(fishId) || centimetres <= 0f)
        {
            return false;
        }

        if (biggestFish.TryGetValue(fishId, out before) && before >= centimetres)
        {
            return false;
        }

        biggestFish[fishId] = centimetres;
        return true;
    }

    public bool HasRead(string key) => read.Contains(key);
    public void MarkRead(string key) => read.Add(key);
    public bool HasFound(string page) => found.Contains(page);
    public void NoteFound(string page) => found.Add(page);
    public bool WasKilledBy(string creatureId) => creatureId != null && killedBy.Contains(creatureId);
    public void NoteKilledBy(string creatureId) => killedBy.Add(creatureId);
    public bool HasTried(string skillId) => skillId != null && tried.Contains(skillId);
    /// <summary>True the first time a skill is tried.</summary>
    public bool NoteTried(string skillId) => skillId != null && tried.Add(skillId);
    public bool HasDiscovered(string placeId) => placeId != null && discovered.Contains(placeId);
    /// <summary>True the first time a place is found.</summary>
    public bool NoteDiscovered(string placeId) => placeId != null && discovered.Add(placeId);
    public bool HasSeen(string placeId) => placeId != null && seen.Contains(placeId);
    /// <summary>True the first time a place is seen from afar (a lookout).</summary>
    public bool NoteSeen(string placeId) => placeId != null && seen.Add(placeId);
    public bool HasNoticed(string placeKey) => placeKey != null && noticed.Contains(placeKey);
    /// <summary>True the first time a curious place is noticed.</summary>
    public bool NoteNoticed(string placeKey) => placeKey != null && noticed.Add(placeKey);

    public void RestoreNoticed(IEnumerable<string> saved)
    {
        noticed.Clear();
        noticed.UnionWith(saved ?? new string[0]);
    }

    public void RestoreSeen(IEnumerable<string> saved)
    {
        seen.Clear();
        seen.UnionWith(saved ?? new string[0]);
    }

    /// <summary>True the first time a page is told about.</summary>
    public bool Announce(string page) => announced.Add(page);

    /// <summary>Every raid run in this game, however many the journal still remembers (it keeps the last MostRaids): what a node
    /// gathered at home counts its raids by (Regrowth.RaidsRun).</summary>
    public int RaidsEver { get; private set; }

    /// <summary>Puts back the lifetime count from a save; one from before it was kept counts the raids remembered.</summary>
    public void RestoreRaidsEver(int saved) => RaidsEver = System.Math.Max(saved, raids.Count);

    public void AddRaid(RaidRecord raid)
    {
        RaidsEver++;
        raids.Add(raid);
        if (raids.Count > MostRaids)
        {
            raids.RemoveAt(0);
        }
    }

    public void Restore(IEnumerable<string> savedRead, IEnumerable<string> savedAnnounced, IEnumerable<string> savedFound, IEnumerable<string> savedKilledBy, IEnumerable<RaidRecord> savedRaids, string pinned,
        IEnumerable<string> savedTried = null, IEnumerable<string> savedDiscovered = null)
    {
        read.Clear();
        biggestFish.Clear();
        tried.Clear();
        tried.UnionWith(savedTried ?? new string[0]);
        discovered.Clear();
        discovered.UnionWith(savedDiscovered ?? new string[0]);
        announced.Clear();
        found.Clear();
        killedBy.Clear();
        raids.Clear();
        read.UnionWith(savedRead);
        announced.UnionWith(savedAnnounced);
        found.UnionWith(savedFound);
        killedBy.UnionWith(savedKilledBy);
        raids.AddRange(savedRaids);
        RaidsEver = raids.Count;
        PinnedQuest = pinned;
    }
}
