using System;
using System.Collections.Generic;

/// <summary>Where the first ward breach stands (GDD §6.6). Values are saved as numbers: add new ones at the end.</summary>
public enum BreachStatus
{
    /// <summary>The ward holds; nothing threatens the clearing yet.</summary>
    Dormant = 0,
    /// <summary>The ward is weakening: the player has been warned and can start the defense at the Heart Tree.</summary>
    Pending = 1,
    /// <summary>The breach was beaten back.</summary>
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
    private readonly List<ResidentRecord> residents = new List<ResidentRecord>();

    public bool PortalRepaired { get; private set; }
    public int WardLevel { get; private set; }
    public int SuccessfulExtractions { get; private set; }
    public BreachStatus Breach { get; private set; }
    public IReadOnlyList<ResidentRecord> Residents => residents;
    /// <summary>Purification batches waiting, including the one in progress.</summary>
    public int PurificationQueued { get; private set; }
    /// <summary>Seconds spent on the batch in progress.</summary>
    public float PurificationProgress { get; private set; }
    /// <summary>Every crafting station's queue and output slot.</summary>
    public WorkshopState Workshops { get; } = new WorkshopState();

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

    public void RecordExtraction()
    {
        SuccessfulExtractions++;
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

    /// <summary>Puts back saved state. Used by SaveMapper only.</summary>
    public void Restore(bool portalRepaired, int wardLevel, int extractions, BreachStatus breach, int queued, float progress)
    {
        PortalRepaired = portalRepaired;
        WardLevel = Math.Max(0, wardLevel);
        SuccessfulExtractions = Math.Max(0, extractions);
        Breach = breach;
        PurificationQueued = Math.Max(0, queued);
        PurificationProgress = Math.Max(0f, progress);
    }
}
