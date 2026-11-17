using System;
using System.Collections.Generic;

/// <summary>
/// The player's permanent growth: experience in each skill (woodcutting, melee...; levels and bonuses follow from it,
/// see LevelingSettings), the perk chosen at each skill milestone, unspent Insight (experience from quests, raids and
/// discoveries, put into a skill of the player's choice), the ranks spells have been deepened to at the altar (GDD §16.4),
/// the items crafted at least once (for first-craft Insight), the experience stones already touched and the journal's
/// record. Lives in GameSession (it belongs to the game, not to the player object, so it survives every scene change) and
/// is saved by SaveMapper. Plain C#, so the rules are testable; the numbers come from LevelingSettings.
/// </summary>
public sealed class CharacterProgress
{
    private readonly Dictionary<string, int> experience = new Dictionary<string, int>();
    private readonly HashSet<string> perks = new HashSet<string>();
    private readonly Dictionary<string, int> skillRanks = new Dictionary<string, int>();
    private readonly HashSet<string> crafted = new HashSet<string>();
    private readonly HashSet<string> stones = new HashSet<string>();
    // Creatures met, by id, with how many the player's side has slain: the journal's record.
    private readonly Dictionary<string, int> creatures = new Dictionary<string, int>();

    private readonly Dictionary<string, int> plainRuns = new Dictionary<string, int>();
    private readonly HashSet<string> techniquesSeen = new HashSet<string>();

    /// <summary>Techniques that have come into play at least once (each is announced the first time).</summary>
    public IEnumerable<string> TechniquesSeen => techniquesSeen;

    /// <summary>True the first time a technique comes into play in this game.</summary>
    public bool MarkTechniqueSeen(PerkTechnique technique) => techniquesSeen.Add(technique.ToString());

    public void RestoreTechniquesSeen(IEnumerable<string> seen)
    {
        techniquesSeen.Clear();
        if (seen != null)
        {
            techniquesSeen.UnionWith(seen);
        }
    }

    // Art runes attuned, by the kind of weapon they fit (WeaponSignature name -> rune item id).
    private readonly Dictionary<string, string> attunedRunes = new Dictionary<string, string>();

    /// <summary>Art runes attuned, as "Kind=itemId" (the save's form).</summary>
    public IEnumerable<string> AttunedRunes
    {
        get
        {
            foreach (KeyValuePair<string, string> rune in attunedRunes)
            {
                yield return rune.Key + "=" + rune.Value;
            }
        }
    }

    /// <summary>The rune attuned for this kind of weapon, by item id, or null.</summary>
    public string RuneIdFor(WeaponSignature kind) => attunedRunes.TryGetValue(kind.ToString(), out string id) ? id : null;

    /// <summary>Attunes the kind of weapon to the rune; true when that changed anything.</summary>
    public bool AttuneRune(WeaponSignature kind, string itemId)
    {
        if (kind == WeaponSignature.None || string.IsNullOrEmpty(itemId) || RuneIdFor(kind) == itemId)
        {
            return false;
        }

        attunedRunes[kind.ToString()] = itemId;
        return true;
    }

    public void RestoreAttunedRunes(IEnumerable<string> saved)
    {
        attunedRunes.Clear();
        if (saved == null)
        {
            return;
        }

        foreach (string entry in saved)
        {
            int at = entry != null ? entry.IndexOf('=') : -1;
            if (at > 0 && at < entry.Length - 1)
            {
                attunedRunes[entry.Substring(0, at)] = entry.Substring(at + 1);
            }
        }
    }

    // Enchantments learned from inscriptions, by id (Docs/EnchantingResearch.md #2). Those Known From Start need no entry.
    private readonly HashSet<string> enchantments = new HashSet<string>();

    /// <summary>Enchantments learned, by id (the save's form). Those known from the start aren't listed.</summary>
    public IEnumerable<string> LearnedEnchantments => enchantments;

    /// <summary>True when this character can carve the enchantment: known from the start, or learned.</summary>
    public bool Knows(EnchantmentDefinition enchantment) =>
        enchantment != null && (enchantment.KnownFromStart || enchantments.Contains(enchantment.Id));

    /// <summary>Learns an enchantment for good; true when it was new.</summary>
    public bool Learn(EnchantmentDefinition enchantment)
    {
        if (enchantment == null || Knows(enchantment) || !enchantments.Add(enchantment.Id))
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    public void RestoreEnchantments(IEnumerable<string> saved)
    {
        enchantments.Clear();
        if (saved != null)
        {
            enchantments.UnionWith(saved);
        }
    }

    private readonly HashSet<string> raidsVisited = new HashSet<string>();

    /// <summary>Raids this character has entered at least once, by raid id (a first visit plays the raid's cinematic).</summary>
    public IEnumerable<string> RaidsVisited => raidsVisited;

    public bool HasVisitedRaid(string raidId) => !string.IsNullOrEmpty(raidId) && raidsVisited.Contains(raidId);

    /// <summary>True the first time this character enters the raid.</summary>
    public bool MarkRaidVisited(string raidId) => !string.IsNullOrEmpty(raidId) && raidsVisited.Add(raidId);

    public void RestoreRaidsVisited(IEnumerable<string> visited)
    {
        raidsVisited.Clear();
        if (visited != null)
        {
            raidsVisited.UnionWith(visited);
        }
    }

    private readonly HashSet<string> encounterGates = new HashSet<string>();
    private readonly Dictionary<string, DateTime> encounterKills = new Dictionary<string, DateTime>();

    /// <summary>Boss encounters (by raid id) whose gate this character has reached: later tries start at its rest stone.</summary>
    public IEnumerable<string> EncounterGates => encounterGates;

    /// <summary>When this character last brought down each boss encounter's boss (by raid id, UTC): rematches wait a day after.</summary>
    public IReadOnlyDictionary<string, DateTime> EncounterKills => encounterKills;

    public bool HasReachedGate(string raidId) => !string.IsNullOrEmpty(raidId) && encounterGates.Contains(raidId);

    /// <summary>True the first time this character reaches the encounter's gate.</summary>
    public bool MarkGateReached(string raidId) => !string.IsNullOrEmpty(raidId) && encounterGates.Add(raidId);

    public DateTime? LastKill(string raidId) =>
        !string.IsNullOrEmpty(raidId) && encounterKills.TryGetValue(raidId, out DateTime at) ? at : (DateTime?)null;

    public void RecordKill(string raidId, DateTime utc)
    {
        if (!string.IsNullOrEmpty(raidId))
        {
            encounterKills[raidId] = utc;
        }
    }

    public void RestoreEncounters(IEnumerable<string> gates, IEnumerable<KeyValuePair<string, DateTime>> kills)
    {
        encounterGates.Clear();
        encounterKills.Clear();
        if (gates != null)
        {
            encounterGates.UnionWith(gates);
        }

        if (kills != null)
        {
            foreach (KeyValuePair<string, DateTime> kill in kills)
            {
                if (!string.IsNullOrEmpty(kill.Key))
                {
                    encounterKills[kill.Key] = kill.Value;
                }
            }
        }
    }

    /// <summary>Counts up whenever the choice of perks changes, so what they teach is worked out again (Techniques).</summary>
    [field: NotSaved("a change counter for windows that redraw on it")]
    public int PerkVersion { get; private set; }

    /// <summary>Plain pieces made in a row, by kind ("weapons", "armour"), for Sure Hand.</summary>
    public IReadOnlyDictionary<string, int> PlainRuns => plainRuns;

    public int PlainRunOf(string kind) => plainRuns.TryGetValue(kind, out int run) ? run : 0;

    /// <summary>Notes a piece made: a plain one lengthens the run, a better one ends it.</summary>
    public void NoteMade(string kind, bool plain) => plainRuns[kind] = plain ? PlainRunOf(kind) + 1 : 0;

    public void RestorePlainRuns(IEnumerable<KeyValuePair<string, int>> saved) => Fill(plainRuns, saved);

    /// <summary>Experience not yet put into a skill.</summary>
    public int Insight { get; private set; }

    /// <summary>Raised after anything changes: experience, a level, a perk, Insight.</summary>
    public event Action Changed;

    /// <summary>Raised with the skill and its new level each time one is reached.</summary>
    public event Action<ProficiencyDefinition, int> LeveledUp;

    /// <summary>Total experience by skill id.</summary>
    public IReadOnlyDictionary<string, int> Experience => experience;
    public IEnumerable<string> Perks => perks;
    public int PerkCount => perks.Count;
    public IReadOnlyDictionary<string, int> SkillRanks => skillRanks;
    public IEnumerable<string> CraftedItems => crafted;
    public IEnumerable<string> UsedStones => stones;

    public int ExperienceOf(ProficiencyDefinition skill) => skill != null && experience.TryGetValue(skill.Id, out int total) ? total : 0;

    public int LevelOf(ProficiencyDefinition skill, LevelingSettings settings) => settings.LevelFor(ExperienceOf(skill));

    public bool IsMaxLevel(ProficiencyDefinition skill, LevelingSettings settings) => LevelOf(skill, settings) >= settings.MaxLevel;

    /// <summary>Every skill's level added up: one number for how far the player has come.</summary>
    public int TotalLevel(LevelingSettings settings)
    {
        int total = 0;
        foreach (ProficiencyDefinition skill in settings.Skills)
        {
            if (skill != null)
            {
                total += LevelOf(skill, settings);
            }
        }

        return total;
    }

    /// <summary>Experience towards the skill's next level, and how much that level takes (0 at the top).</summary>
    public int TowardsNext(ProficiencyDefinition skill, LevelingSettings settings, out int needed)
    {
        int level = LevelOf(skill, settings);
        if (level >= settings.MaxLevel)
        {
            needed = 0;
            return 0;
        }

        needed = settings.ExperienceToNext(level);
        return ExperienceOf(skill) - settings.TotalExperienceFor(level);
    }

    /// <summary>Adds experience to a skill, never past its last level. Returns the levels gained.</summary>
    public int AddExperience(ProficiencyDefinition skill, int amount, LevelingSettings settings)
    {
        if (skill == null || amount <= 0)
        {
            return 0;
        }

        int before = LevelOf(skill, settings);
        int cap = settings.TotalExperienceFor(settings.MaxLevel);
        int total = Math.Min(cap, ExperienceOf(skill) + amount);
        if (total == ExperienceOf(skill))
        {
            return 0;
        }

        experience[skill.Id] = total;
        int after = settings.LevelFor(total);
        for (int level = before + 1; level <= after; level++)
        {
            LeveledUp?.Invoke(skill, level);
        }

        Changed?.Invoke();
        return after - before;
    }

    public void AddInsight(int amount)
    {
        if (amount > 0)
        {
            Insight += amount;
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Puts up to <paramref name="amount"/> Insight into a skill, never past its last level: in full below
    /// LevelingSettings › Insight Full Below, at a share from there on (InsightRate), level by level. Returns the Insight
    /// that went in.
    /// </summary>
    public int SpendInsight(ProficiencyDefinition skill, int amount, LevelingSettings settings)
    {
        if (skill == null)
        {
            return 0;
        }

        int spent = 0;
        while (spent < amount && Insight > 0 && !IsMaxLevel(skill, settings))
        {
            int level = LevelOf(skill, settings);
            float rate = settings.InsightRate(level);
            int room = settings.TotalExperienceFor(level + 1) - ExperienceOf(skill);
            int affordable = (int)Math.Floor(Math.Min(amount - spent, Insight) * rate + 0.0001);
            int experience = Math.Min(room, affordable);
            if (experience <= 0)
            {
                break;
            }

            int cost = Math.Min(Insight, (int)Math.Ceiling(experience / rate - 0.0001));
            Insight -= cost;
            spent += cost;
            AddExperience(skill, experience, settings);
        }

        return spent;
    }

    public bool HasPerk(PerkDefinition perk) => perk != null && perks.Contains(perk.Id);

    /// <summary>The perk chosen at a milestone, or null while it waits for a choice.</summary>
    public PerkDefinition ChosenAt(ProficiencyDefinition.Milestone milestone) =>
        HasPerk(milestone.first) ? milestone.first : HasPerk(milestone.second) ? milestone.second : null;

    /// <summary>Milestones reached whose perk hasn't been chosen yet, across every skill.</summary>
    public int PendingChoices(LevelingSettings settings)
    {
        int pending = 0;
        foreach (ProficiencyDefinition skill in settings.Skills)
        {
            if (skill == null)
            {
                continue;
            }

            int level = LevelOf(skill, settings);
            foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
            {
                if (milestone.level <= level && ChosenAt(milestone) == null)
                {
                    pending++;
                }
            }
        }

        return pending;
    }

    /// <summary>
    /// Whether this perk can be taken now; reason says why not. A milestone's first choice can be made anywhere;
    /// changing it for the other needs <paramref name="atHome"/> (the same quiet place as a full reset).
    /// </summary>
    public bool CanChoose(ProficiencyDefinition skill, PerkDefinition perk, LevelingSettings settings, bool atHome, out string reason)
    {
        reason = null;
        if (skill == null || perk == null || !skill.TryFindMilestone(perk, out ProficiencyDefinition.Milestone milestone))
        {
            reason = "No such perk.";
        }
        else if (HasPerk(perk))
        {
            reason = "Already chosen.";
        }
        else if (LevelOf(skill, settings) < milestone.level)
        {
            reason = $"Needs {skill.DisplayName} {milestone.level}.";
        }
        else if (ChosenAt(milestone) != null && !atHome)
        {
            reason = "A choice can only be changed at home, beside the portal.";
        }

        return reason == null;
    }

    /// <summary>Takes the perk (giving up the milestone's other one). False when CanChoose says no.</summary>
    public bool Choose(ProficiencyDefinition skill, PerkDefinition perk, LevelingSettings settings, bool atHome)
    {
        if (!CanChoose(skill, perk, settings, atHome, out _))
        {
            return false;
        }

        skill.TryFindMilestone(perk, out ProficiencyDefinition.Milestone milestone);
        PerkDefinition other = milestone.Other(perk);
        if (other != null)
        {
            perks.Remove(other.Id);
        }

        perks.Add(perk.Id);
        PerkVersion++;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Gives back every perk choice (chosen again for free at home).</summary>
    public void ResetPerks()
    {
        if (perks.Count == 0)
        {
            return;
        }

        perks.Clear();
        PerkVersion++;
        Changed?.Invoke();
    }

    /// <summary>How far a skill has been deepened at the altar. 0 is the skill as it was learned.</summary>
    public int RankOf(SkillDefinition skill) => skill != null && skillRanks.TryGetValue(skill.Id, out int rank) ? rank : 0;

    /// <summary>Deepens a skill one rank. What it costs and how far it goes are the altar's business (AltarWork).</summary>
    public void DeepenSkill(SkillDefinition skill)
    {
        if (skill != null)
        {
            skillRanks[skill.Id] = RankOf(skill) + 1;
            Changed?.Invoke();
        }
    }

    /// <summary>The rest of what the journal keeps.</summary>
    public JournalRecord Journal { get; } = new JournalRecord();

    public IReadOnlyDictionary<string, int> Creatures => creatures;

    public bool HasMet(string creatureId) => creatureId != null && creatures.ContainsKey(creatureId);

    public int TimesSlain(string creatureId) => creatureId != null && creatures.TryGetValue(creatureId, out int slain) ? slain : 0;

    public void NoteMet(string creatureId)
    {
        if (!string.IsNullOrEmpty(creatureId) && !creatures.ContainsKey(creatureId))
        {
            creatures[creatureId] = 0;
        }
    }

    public void NoteSlain(string creatureId)
    {
        if (!string.IsNullOrEmpty(creatureId))
        {
            creatures[creatureId] = TimesSlain(creatureId) + 1;
        }
    }

    /// <summary>The record as a save holds it. Kept apart from Restore, as older saves have none.</summary>
    public void RestoreCreatures(IEnumerable<KeyValuePair<string, int>> saved)
    {
        creatures.Clear();
        foreach (KeyValuePair<string, int> creature in saved)
        {
            creatures[creature.Key] = creature.Value;
        }
    }

    /// <summary>True the first time an item is crafted (for first-craft experience).</summary>
    public bool MarkCrafted(string itemId) => !string.IsNullOrEmpty(itemId) && crafted.Add(itemId);

    /// <summary>True the first time an experience stone is touched: each gives its experience once per game, even in
    /// raids, whose scenes start fresh every time.</summary>
    public bool MarkStoneUsed(string stoneId) => !string.IsNullOrEmpty(stoneId) && stones.Add(stoneId);

    public bool HasUsedStone(string stoneId) => stones.Contains(stoneId);

    /// <summary>Puts back saved progress (loading).</summary>
    public void Restore(IEnumerable<KeyValuePair<string, int>> savedExperience, IEnumerable<string> savedPerks, int savedInsight,
        IEnumerable<string> craftedItems, IEnumerable<string> usedStones = null, IEnumerable<KeyValuePair<string, int>> savedSkillRanks = null)
    {
        Fill(experience, savedExperience);
        Fill(skillRanks, savedSkillRanks);
        Insight = Math.Max(0, savedInsight);
        PerkVersion++;
        perks.Clear();
        if (savedPerks != null)
        {
            perks.UnionWith(savedPerks);
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

    private static void Fill(Dictionary<string, int> into, IEnumerable<KeyValuePair<string, int>> saved)
    {
        into.Clear();
        if (saved == null)
        {
            return;
        }

        foreach (KeyValuePair<string, int> entry in saved)
        {
            if (!string.IsNullOrEmpty(entry.Key) && entry.Value > 0)
            {
                into[entry.Key] = entry.Value;
            }
        }
    }
}
