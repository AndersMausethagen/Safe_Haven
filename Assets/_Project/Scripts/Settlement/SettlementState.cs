using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where the current ward breach stands (GDD §6.6). Values are saved as numbers: add new ones at the end.</summary>
public enum BreachStatus
{
    /// <summary>The ward holds; nothing threatens the clearing yet.</summary>
    Dormant = 0,
    /// <summary>The ward is weakening: the player has been warned and can start the defense at the Heart Tree.</summary>
    Pending = 1,
    /// <summary>The last breach was beaten back.</summary>
    Won = 2,
}

/// <summary>Someone who lives in Safe Haven and the job they hold (null while unassigned).</summary>
public sealed class ResidentRecord
{
    public ResidentRecord(ResidentDefinition resident, JobDefinition job = null)
    {
        Resident = resident;
        Job = job;
    }

    public ResidentDefinition Resident { get; }
    public JobDefinition Job { get; set; }
    /// <summary>The player asked them to stop working through their job's orders.</summary>
    public bool WorkPaused { get; set; }
}

/// <summary>
/// The state of Safe Haven as a place, beyond what lies in its scene: whether the portal is repaired, how strong the
/// Heart Tree's ward is, how many raids the player has come home from, the first ward breach, who lives there with which
/// job, and the Purification Altar's queue. Plain data owned by GameSession (wiped by New Game, replaced by Load) and
/// written to saves by SaveMapper.
/// <para>
/// Systems change it through the methods here so Changed is raised; UI and scene objects (the portal, the Heart Tree,
/// residents) listen to Changed to update themselves.
/// </para>
/// </summary>
public sealed class SettlementState
{
    private readonly Dictionary<BuildingDefinition, int> builtTiers = new Dictionary<BuildingDefinition, int>();
    private readonly List<ResidentRecord> residents = new List<ResidentRecord>();
    private readonly Dictionary<string, int> tradeBought = new Dictionary<string, int>();
    private float purifiedRemainder;

    public bool PortalRepaired { get; private set; }
    public int WardLevel { get; private set; }

    /// <summary>The highest Haven rank reached (HavenSettings): it never drops, so the ward it grew stays grown.</summary>
    public int HavenRank { get; private set; }
    public int SuccessfulExtractions { get; private set; }
    public BreachStatus Breach { get; private set; }

    /// <summary>How many ward breaches have been beaten back: which one comes next, and how hard.</summary>
    public int BreachesWon { get; private set; }

    /// <summary>Successful extractions at which the next breach comes due, after the first.</summary>
    public int NextBreachAt { get; private set; }

    /// <summary>Successful extractions when the waiting breach was announced, as it can only be put off so long.</summary>
    public int BreachPendingSince { get; private set; }

    /// <summary>Successful raids until a ward strained by a lost breach has recovered; 0 when it is whole.</summary>
    public int WardStrain { get; private set; }

    /// <summary>Share taken off the next breach by what was done to its source in a raid. 0 when nothing was.</summary>
    public float BreachWeakening { get; private set; }

    /// <summary>True when a breach is waiting, or the next successful raid will bring one on.</summary>
    public bool BreachIsNear => Breach == BreachStatus.Pending || (BreachesWon > 0 && SuccessfulExtractions + 1 >= NextBreachAt);

    public IReadOnlyList<ResidentRecord> Residents => residents;

    /// <summary>Beds standing at home, as last seen there. Nobody needs one; whoever has one works faster and adds comfort.</summary>
    public int Beds { get; private set; }

    /// <summary>How many residents have a bed.</summary>
    public int Housed => Math.Min(Beds, residents.Count);

    /// <summary>Whether this resident has a bed. They are shared out in order of arrival.</summary>
    public bool HasBed(ResidentRecord record)
    {
        int index = residents.IndexOf(record);
        return index >= 0 && index < Beds;
    }

    /// <summary>How far the portal has been improved (SettlementSettings.PortalLevels).</summary>
    public int PortalLevel { get; private set; }

    /// <summary>What every kind of building's reached upgrades add up to, as last seen at home, where raids can't look.</summary>
    public int UpgradeBonus(Func<BuildingLevel, int> bonus)
    {
        int total = 0;
        foreach (KeyValuePair<BuildingDefinition, int> built in builtTiers)
        {
            for (int level = 2; level <= built.Value; level++)
            {
                BuildingLevel upgrade = built.Key.UpgradeTo(level);
                total += upgrade != null ? bonus(upgrade) : 0;
            }
        }

        return total;
    }

    /// <summary>The Smithy's tier as last seen at home: 0 before there is one. What raids and workers go by, where no building is loaded.</summary>
    public int SmithyTier => BuiltTier(CraftingStationType.Anvil);

    /// <summary>What workers have made since the player last heard about it.</summary>
    public Dictionary<IStackDefinition, int> MadeByWorkers { get; } = new Dictionary<IStackDefinition, int>();

    /// <summary>The best tier standing of a kind of building, as last seen at home.</summary>
    public int BuiltTier(BuildingDefinition building) => building != null && builtTiers.TryGetValue(building, out int tier) ? tier : 0;

    public int BuiltTier(CraftingStationType station)
    {
        int best = 0;
        foreach (KeyValuePair<BuildingDefinition, int> built in builtTiers)
        {
            if (built.Key.Station == station)
            {
                best = UnityEngine.Mathf.Max(best, built.Value);
            }
        }

        return best;
    }

    /// <summary>Remembers what stands at home, since a raid can't look. Read again by HavenProgress; not saved, as loading a game lands at home.</summary>
    public void NoteBuildings(IEnumerable<BuildingObject> standing)
    {
        builtTiers.Clear();
        int beds = 0;
        foreach (BuildingObject building in standing)
        {
            if (building.Definition == null)
            {
                continue;
            }

            bool upgrades = building.TryGetComponent(out Construction construction);
            NoteBuilt(building.Definition, upgrades ? construction.Level : 1);
            beds += building.Definition.Beds + (upgrades ? Mathf.RoundToInt(construction.Sum(level => level.beds)) : 0);
        }

        Beds = beds;
    }

    public void NoteBuilt(BuildingDefinition building, int tier)
    {
        builtTiers[building] = UnityEngine.Mathf.Max(BuiltTier(building), tier);
    }

    public void NoteMadeByWorker(IStackDefinition made, int amount)
    {
        MadeByWorkers.TryGetValue(made, out int known);
        MadeByWorkers[made] = known + amount;
    }
    /// <summary>Purification batches waiting, including the one in progress.</summary>
    public int PurificationQueued { get; private set; }
    /// <summary>Seconds spent on the batch in progress.</summary>
    public float PurificationProgress { get; private set; }
    /// <summary>Every crafting station's queue and output slot.</summary>
    public WorkshopState Workshops { get; } = new WorkshopState();

    /// <summary>Share faster the altar works for its upgrades. Set by the altar; kept here as batches finish during raids.</summary>
    public float PurificationBonus { get; private set; }

    /// <summary>Share more Purified Essence from each batch for the altar's upgrades. Set by the altar, like the speed.</summary>
    public float PurificationYield { get; private set; }

    /// <summary>Raised after anything here changes (but not on every purification tick).</summary>
    public event Action Changed;

    public void RepairPortal()
    {
        if (!PortalRepaired)
        {
            PortalRepaired = true;
            Changed?.Invoke();
        }
    }

    public void RaiseWardLevel()
    {
        WardLevel++;
        Changed?.Invoke();
    }

    public void ImprovePortal()
    {
        PortalLevel++;
        Changed?.Invoke();
    }

    /// <summary>Puts back how far the portal was improved. Used by SaveMapper only.</summary>
    public void RestorePortal(int level) => PortalLevel = Math.Max(0, level);

    public void RecordExtraction()
    {
        SuccessfulExtractions++;
        WardStrain = Math.Max(0, WardStrain - 1);
        tradeBought.Clear();
        Changed?.Invoke();
    }

    public void SetBreach(BreachStatus status)
    {
        if (Breach != status)
        {
            Breach = status;
            Changed?.Invoke();
        }
    }

    /// <summary>The next breach is announced: it waits for the player, and is put off from now.</summary>
    public void WarnOfBreach()
    {
        Breach = BreachStatus.Pending;
        BreachPendingSince = SuccessfulExtractions;
        Changed?.Invoke();
    }

    /// <summary>A breach was beaten back: the next comes after this many more successful raids.</summary>
    public void WinBreach(int raidsUntilNext)
    {
        Breach = BreachStatus.Won;
        BreachesWon++;
        BreachWeakening = 0f;
        NextBreachAt = SuccessfulExtractions + raidsUntilNext;
        Changed?.Invoke();
    }

    /// <summary>Something was done in a raid to the source of the next breach. Blows add up, to at most <paramref name="most"/>.</summary>
    public void WeakenBreach(float share, float most)
    {
        BreachWeakening = Math.Min(most, BreachWeakening + Math.Max(0f, share));
        Changed?.Invoke();
    }

    /// <summary>The ward fell: it stays strained for a while, and the same breach waits to be tried again.</summary>
    public void LoseBreach(int strainRaids)
    {
        WardStrain = Math.Max(WardStrain, strainRaids);
        BreachPendingSince = SuccessfulExtractions;
        Changed?.Invoke();
    }

    public ResidentRecord Find(ResidentDefinition resident)
    {
        return resident == null ? null : residents.Find(record => record.Resident == resident);
    }

    public bool HasResident(ResidentDefinition resident) => Find(resident) != null;

    /// <summary>Moves someone into Safe Haven. Returns false when they already live here.</summary>
    public bool AddResident(ResidentDefinition resident, JobDefinition job = null)
    {
        if (resident == null || HasResident(resident))
        {
            return false;
        }

        residents.Add(new ResidentRecord(resident, job));
        Changed?.Invoke();
        return true;
    }

    public void AssignJob(ResidentDefinition resident, JobDefinition job)
    {
        ResidentRecord record = Find(resident);
        if (record != null && record.Job != job)
        {
            record.Job = job;
            Changed?.Invoke();
        }
    }

    /// <summary>What everyone's jobs add up to for one bonus (JobDefinition): the Tracker's rumours, say.</summary>
    public int JobBonus(Func<JobDefinition, int> bonus)
    {
        int total = 0;
        foreach (ResidentRecord record in residents)
        {
            total += record.Job != null ? bonus(record.Job) : 0;
        }

        return total;
    }

    /// <summary>What everyone's jobs add up to for one bonus, as a share or an amount.</summary>
    public float JobBonus(Func<JobDefinition, float> bonus)
    {
        float total = 0f;
        foreach (ResidentRecord record in residents)
        {
            total += record.Job != null ? bonus(record.Job) : 0f;
        }

        return total;
    }

    /// <summary>True when some resident holds the job.</summary>
    public bool HasWorker(JobDefinition job)
    {
        return job != null && residents.Exists(record => record.Job == job);
    }

    /// <summary>Adds batches to the purification queue. Returns how many were added.</summary>
    public int QueuePurification(int batches, int maxQueued)
    {
        int added = Math.Max(0, Math.Min(batches, maxQueued - PurificationQueued));
        if (added > 0)
        {
            PurificationQueued += added;
            Changed?.Invoke();
        }

        return added;
    }

    public void SetPurificationBonus(float bonus)
    {
        PurificationBonus = Math.Max(0f, bonus);
    }

    public void SetPurificationYield(float share)
    {
        PurificationYield = Math.Max(0f, share);
    }

    /// <summary>What a number of finished batches come to with the altar's yield. A fraction waits for the next batch.</summary>
    public int PurifiedFrom(int made)
    {
        float exact = made * (1f + PurificationYield) + purifiedRemainder;
        int whole = Mathf.FloorToInt(exact);
        purifiedRemainder = exact - whole;
        return whole;
    }

    // ----- The Trading Post: what the caravan has left. It comes round again after each raid survived. -----

    public int BoughtSinceLastRaid(string offer) => tradeBought.TryGetValue(offer, out int bought) ? bought : 0;

    public void NoteBought(string offer)
    {
        tradeBought[offer] = BoughtSinceLastRaid(offer) + 1;
        Changed?.Invoke();
    }

    public IEnumerable<KeyValuePair<string, int>> TradeBought => tradeBought;

    /// <summary>Puts back what was bought. Used by SaveMapper only.</summary>
    public void RestoreBought(string offer, int count) => tradeBought[offer] = count;

    /// <summary>Advances the batch in progress. Returns how many batches finished.</summary>
    public int TickPurification(float seconds, float secondsPerBatch)
    {
        if (PurificationQueued <= 0 || seconds <= 0f || secondsPerBatch <= 0f)
        {
            return 0;
        }

        PurificationProgress += seconds;
        int finished = 0;
        while (PurificationQueued > 0 && PurificationProgress >= secondsPerBatch)
        {
            PurificationProgress -= secondsPerBatch;
            PurificationQueued--;
            finished++;
        }

        if (PurificationQueued == 0)
        {
            PurificationProgress = 0f;
        }

        if (finished > 0)
        {
            Changed?.Invoke();
        }

        return finished;
    }

    /// <summary>Records a Haven rank reached; lower ranks than the best so far are ignored.</summary>
    public bool ReachHavenRank(int rank)
    {
        if (rank <= HavenRank)
        {
            return false;
        }

        HavenRank = rank;
        Changed?.Invoke();
        return true;
    }

    /// <summary>Puts back saved breach history. Used by SaveMapper only. A save from before there were several knows only of the first.</summary>
    public void RestoreBreaches(int won, int nextAt, int pendingSince, int strain, float weakening = 0f)
    {
        BreachWeakening = Math.Max(0f, weakening);
        BreachesWon = Math.Max(won, Breach == BreachStatus.Won ? 1 : 0);
        NextBreachAt = Math.Max(0, nextAt);
        BreachPendingSince = Math.Max(0, pendingSince);
        WardStrain = Math.Max(0, strain);
    }

    /// <summary>Puts back saved state. Used by SaveMapper only.</summary>
    public void Restore(bool portalRepaired, int wardLevel, int extractions, BreachStatus breach, int queued, float progress, float bonus = 0f)
    {
        PurificationBonus = Math.Max(0f, bonus);
        PortalRepaired = portalRepaired;
        WardLevel = Math.Max(0, wardLevel);
        SuccessfulExtractions = Math.Max(0, extractions);
        Breach = breach;
        PurificationQueued = Math.Max(0, queued);
        PurificationProgress = Math.Max(0f, progress);
    }
}
