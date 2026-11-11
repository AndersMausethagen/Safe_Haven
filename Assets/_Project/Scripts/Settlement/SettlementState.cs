using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where the current ward breach stands (GDD §6.6). Values are saved as numbers: add new ones at the end.</summary>
public enum BreachStatus
{
    /// <summary>The ward holds; nothing threatens the clearing yet.</summary>
    Dormant = 0,
    /// <summary>The ward is weakening: the player has been warned and can start the defense at the portal.</summary>
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
    /// <summary>Raids they have gone on with the player and come back from.</summary>
    public int RaidsAlong { get; set; }
    public int KillsAlong { get; set; }
    public int TimesHelpedUp { get; set; }
    /// <summary>What they will bring up next time they are spoken to.</summary>
    public CompanionMemory Memory { get; set; }
    /// <summary>What has happened with the player, as far as they know.</summary>
    public List<CompanionMemory> Remembers { get; } = new List<CompanionMemory>();
    /// <summary>What the forest took from them and raids together haven't yet brought back, next to return first.</summary>
    public List<CompanionMemory> Forgotten { get; } = new List<CompanionMemory>();
    /// <summary>Found again and not yet spoken to: they don't know the player.</summary>
    public bool IsStranger { get; set; }
    /// <summary>A memory that has just come back, to say when next spoken to.</summary>
    public CompanionMemory ComingBack { get; set; }
    /// <summary>That was the last of them.</summary>
    public bool AllCameBack { get; set; }
    /// <summary>Crafts made at their workplace on their own: their rank in the job grows with it (SettlementSettings › Worker Ranks).</summary>
    public int WorkDone { get; set; }
    /// <summary>They have given the player a lesson since the last raid survived (one each in between).</summary>
    public bool TaughtSinceRaid { get; set; }
}

/// <summary>Someone who went along on a raid and didn't come home. The forest has them: they can be found again, and won't know the player.</summary>
public sealed class FallenRecord
{
    public FallenRecord(ResidentDefinition resident, string where, int atExtraction, int raidsAlong = 0, IEnumerable<CompanionMemory> forgotten = null)
    {
        Resident = resident;
        Where = where;
        AtExtraction = atExtraction;
        RaidsAlong = raidsAlong;
        if (forgotten != null)
        {
            Forgotten.AddRange(forgotten);
        }
    }

    /// <summary>What they will have to get back.</summary>
    public List<CompanionMemory> Forgotten { get; } = new List<CompanionMemory>();

    public ResidentDefinition Resident { get; }
    public string Where { get; }
    /// <summary>How many raids had come home when it happened, so talk of it fades with time.</summary>
    public int AtExtraction { get; }
    /// <summary>Raids they had come back from with the player.</summary>
    public int RaidsAlong { get; }
}

/// <summary>
/// The state of Safe Haven as a place, beyond what lies in its scene: whether the portal is repaired, how strong the
/// Portal's ward is, how many raids the player has come home from, the first ward breach, who lives there with which
/// job, and the Purification Altar's queue. Plain data owned by GameSession (wiped by New Game, replaced by Load) and
/// written to saves by SaveMapper.
/// <para>
/// Systems change it through the methods here so Changed is raised; UI and scene objects (the portal, the portal,
/// residents) listen to Changed to update themselves.
/// </para>
/// </summary>
public sealed class SettlementState
{
    private readonly Dictionary<BuildingDefinition, int> builtTiers = new Dictionary<BuildingDefinition, int>();
    private readonly List<ResidentRecord> residents = new List<ResidentRecord>();
    private readonly List<FallenRecord> fallen = new List<FallenRecord>();
    private readonly HashSet<string> defeatedBosses = new HashSet<string>();
    private readonly List<JobDefinition> vacantJobs = new List<JobDefinition>();
    private readonly Dictionary<string, int> tradeBought = new Dictionary<string, int>();
    private float purifiedRemainder;

    public bool PortalRepaired { get; private set; }
    public int WardLevel { get; private set; }

    /// <summary>The highest Haven rank reached (HavenSettings): it never drops, so the ward it grew stays grown.</summary>
    public int HavenRank { get; private set; }
    public int SuccessfulExtractions { get; private set; }

    /// <summary>Days that have passed at home: the seasons (Seasons) follow them. Counted when the night turns to day.</summary>
    public int DaysPassed { get; private set; }

    /// <summary>A day has passed (dawn has come round). The blight eases a little each day.</summary>
    public void PassDay()
    {
        DaysPassed++;
        Blight = Math.Max(0f, Blight - BlightEasedPerDay);
    }

    public void RestoreDays(int days) => DaysPassed = Math.Max(0, days);

    /// <summary>How far the blight has crept into the woods round home, 0 to 1: corrupted nights whose creatures got away
    /// spread it; beating them back and the days passing ease it. Hollowed animals grow common as it spreads.</summary>
    public float Blight { get; private set; }

    public const float BlightEasedPerDay = 0.02f;

    /// <summary>The Haven's hold on the regions round it (Territory): which are claimed, how firmly, and the forest's push.</summary>
    public TerritoryState Territory { get; private set; } = new TerritoryState();

    /// <summary>Spreads (positive) or eases (negative) the blight.</summary>
    public void ChangeBlight(float by) => Blight = Math.Min(1f, Math.Max(0f, Blight + by));

    public void RestoreBlight(float blight) => Blight = Math.Min(1f, Math.Max(0f, blight));

    private readonly List<string> requests = new List<string>();

    /// <summary>What the Haven is asking to be brought home (HavenRequestDefinition ids), oldest first.</summary>
    public IReadOnlyList<string> Requests => requests;

    public void AddRequest(string id)
    {
        if (!string.IsNullOrEmpty(id) && !requests.Contains(id))
        {
            requests.Add(id);
        }
    }

    public bool RemoveRequest(string id) => requests.Remove(id);

    /// <summary>Asks answered, all told.</summary>
    public int RequestsAnswered { get; private set; }

    /// <summary>Renown the Haven keeps for crafting orders filled (HavenRequestDefinition › Renown).</summary>
    public int OrderRenown { get; private set; }

    public void RecordRequestAnswered(int renown = 0)
    {
        RequestsAnswered++;
        OrderRenown += Math.Max(0, renown);
    }

    public void RestoreRequests(IEnumerable<string> asked, int answered, int orderRenown = 0)
    {
        OrderRenown = Math.Max(0, orderRenown);
        requests.Clear();
        if (asked != null)
        {
            foreach (string id in asked)
            {
                AddRequest(id);
            }
        }

        RequestsAnswered = Math.Max(0, answered);
    }

    private readonly HashSet<string> spiritGifts = new HashSet<string>();

    /// <summary>What the spirits have given that lasts (SpiritBonds): "nokk", the Nøkk's air.</summary>
    public IReadOnlyCollection<string> SpiritGifts => spiritGifts;

    public bool HasSpiritGift(string id) => spiritGifts.Contains(id);

    public void GiveSpiritGift(string id)
    {
        if (!string.IsNullOrEmpty(id))
        {
            spiritGifts.Add(id);
        }
    }

    /// <summary>Times the Huldra has been let be since she last left a gift (or was chased).</summary>
    public int HuldraRespect { get; private set; }

    /// <summary>The day before which she won't show herself, having been chased.</summary>
    public int HuldraAwayUntil { get; private set; }

    public void SetHuldra(int respect, int awayUntil)
    {
        HuldraRespect = Math.Max(0, respect);
        HuldraAwayUntil = Math.Max(0, awayUntil);
    }

    public void RestoreSpirits(IEnumerable<string> gifts, int huldraRespect, int huldraAwayUntil)
    {
        spiritGifts.Clear();
        if (gifts != null)
        {
            foreach (string gift in gifts)
            {
                GiveSpiritGift(gift);
            }
        }

        SetHuldra(huldraRespect, huldraAwayUntil);
    }
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

    /// <summary>
    /// Becomes a copy of <paramref name="other"/> (a friend's settlement in co-op, freshly read and not used elsewhere),
    /// keeping this object, so everything listening to <see cref="Changed"/> stays subscribed; then raises it. Every field
    /// is copied by reflection so new ones are never missed; <paramref name="other"/> must not be used afterwards.
    /// </summary>
    public void TakeOn(SettlementState other)
    {
        if (other == null || other == this)
        {
            return;
        }

        foreach (System.Reflection.FieldInfo field in typeof(SettlementState).GetFields(
                     System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            if (field.Name != nameof(Changed))
            {
                field.SetValue(this, field.GetValue(other));
            }
        }

        Changed?.Invoke();
    }

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
        foreach (ResidentRecord record in residents)
        {
            record.TaughtSinceRaid = false;
        }

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

    /// <summary>The resident whose job works at this kind of station, if one does.</summary>
    public ResidentRecord WorkerAt(CraftingStationType station)
    {
        foreach (ResidentRecord record in residents)
        {
            if (record.Job != null && record.Job.Workplace != null && record.Job.Workplace.Station == station && station != CraftingStationType.None)
            {
                return record;
            }
        }

        return null;
    }

    /// <summary>How much faster a station works for the rank of the resident who works there (1 with nobody).</summary>
    public float WorkerSpeed(CraftingStationType station)
    {
        ResidentRecord worker = WorkerAt(station);
        return worker != null ? SettlementSettings.Current.RankFor(worker.WorkDone).speed : 1f;
    }

    public ResidentRecord Find(ResidentDefinition resident)
    {
        return resident == null ? null : residents.Find(record => record.Resident == resident);
    }

    /// <summary>True for anyone who lives here, or did and was lost: neither is sought as a new face (the lost have a way back: FindAgain).</summary>
    public bool HasResident(ResidentDefinition resident) => Find(resident) != null || IsFallen(resident);

    /// <summary>Ids of the bosses beaten. Kept whatever became of the raid it happened in.</summary>
    public IReadOnlyCollection<string> DefeatedBosses => defeatedBosses;

    public bool IsBossDefeated(string id) => id != null && defeatedBosses.Contains(id);

    /// <summary>A boss has fallen. False when it had already been beaten before, so what it gives is given once.</summary>
    public bool MarkBossDefeated(string id)
    {
        if (string.IsNullOrEmpty(id) || !defeatedBosses.Add(id))
        {
            return false;
        }

        LastBossBeaten = id;
        LastBossBeatenAt = SuccessfulExtractions;
        Changed?.Invoke();
        return true;
    }

    /// <summary>The boss beaten most recently and how many raids had come home by then, so talk of it fades.</summary>
    public string LastBossBeaten { get; private set; }
    public int LastBossBeatenAt { get; private set; }

    public void RestoreLastBoss(string id, int atExtraction)
    {
        LastBossBeaten = id;
        LastBossBeatenAt = atExtraction;
    }

    /// <summary>Those who went along on a raid and haven't come home.</summary>
    public IReadOnlyList<FallenRecord> Fallen => fallen;

    /// <summary>Who a raid's rumour can lead to next: the one lost longest ago who can walk a raid at all. Null with nobody to find.</summary>
    public FallenRecord LostToFind => fallen.Find(record => record.Resident != null && record.Resident.RaidCompanion != null);

    public bool IsFallen(ResidentDefinition resident) => resident != null && fallen.Exists(record => record.Resident == resident);

    /// <summary>
    /// Jobs left by those who were lost. Anyone can take one (GDD §7: a casualty mustn't block crafting for good), so no job a
    /// resident could hold is ever out of reach because the only one who could hold it is gone.
    /// </summary>
    public IReadOnlyList<JobDefinition> VacantJobs => vacantJobs;

    /// <summary>The jobs someone could take: their own, and any left vacant.</summary>
    public List<JobDefinition> JobsOpenTo(ResidentDefinition resident)
    {
        var jobs = new List<JobDefinition>();
        if (resident != null)
        {
            foreach (JobDefinition job in resident.Jobs)
            {
                if (job != null && !jobs.Contains(job))
                {
                    jobs.Add(job);
                }
            }
        }

        foreach (JobDefinition job in vacantJobs)
        {
            if (job != null && !jobs.Contains(job))
            {
                jobs.Add(job);
            }
        }

        return jobs;
    }

    /// <summary>They came home from a raid with the player: the bond grows, and it leaves them something to say. True when they rose in rank.</summary>
    public bool RecordRaidAlong(ResidentDefinition resident, IReadOnlyList<CompanionBondRank> ranks, bool extracted, int bossesBeaten, int timesHelpedUp, float secondsLeft, int kills, int memoryReturnsEvery = 0)
    {
        ResidentRecord record = Find(resident);
        if (record == null)
        {
            return false;
        }

        CompanionBondRank before = CompanionBond.RankFor(record.RaidsAlong, ranks);
        record.RaidsAlong++;
        record.KillsAlong += kills;
        record.TimesHelpedUp += timesHelpedUp;
        bool rose = CompanionBond.RankFor(record.RaidsAlong, ranks) != before;
        record.Memory = CompanionBond.Remember(extracted, bossesBeaten, timesHelpedUp, rose, secondsLeft, kills);
        Keep(record, record.Memory);
        // One at a time, and not over one they haven't had the chance to say.
        if (record.ComingBack == CompanionMemory.None && CompanionBond.MemoryComesBack(record.RaidsAlong, memoryReturnsEvery, record.Forgotten.Count))
        {
            record.ComingBack = record.Forgotten[0];
            record.Forgotten.RemoveAt(0);
            record.AllCameBack = record.Forgotten.Count == 0;
            Keep(record, record.ComingBack);
        }

        return rose;
    }

    private static void Keep(ResidentRecord record, CompanionMemory memory)
    {
        if (memory != CompanionMemory.None && !record.Remembers.Contains(memory))
        {
            record.Remembers.Add(memory);
        }
    }

    /// <summary>Someone went along on a raid and the forest has them. They can be found again, on a later raid, with all of it gone.</summary>
    public void LoseResident(ResidentDefinition resident, string where)
    {
        ResidentRecord record = Find(resident);
        if (record == null)
        {
            return;
        }

        residents.Remove(record);
        fallen.Add(new FallenRecord(resident, where, SuccessfulExtractions, record.RaidsAlong, CompanionBond.WhatIsForgotten(record.Remembers, record.Forgotten)));
        if (record.Job != null && !vacantJobs.Contains(record.Job) && !IsHeld(record.Job))
        {
            vacantJobs.Add(record.Job);
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Someone lost has been brought out of a raid. They live here again as a stranger: no bond, no job, none of what they knew, which raids
    /// together bring back. False unless they were lost.
    /// </summary>
    public bool FindAgain(ResidentDefinition resident)
    {
        FallenRecord lost = fallen.Find(record => record.Resident == resident);
        if (resident == null || lost == null)
        {
            return false;
        }

        fallen.Remove(lost);
        var found = new ResidentRecord(resident) { IsStranger = true };
        found.Forgotten.AddRange(lost.Forgotten);
        residents.Add(found);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Puts someone lost back on the list when loading a save.</summary>
    public void RestoreFallen(ResidentDefinition resident, string where, int atExtraction, int raidsAlong = 0, IEnumerable<CompanionMemory> forgotten = null)
    {
        if (resident != null && !IsFallen(resident))
        {
            fallen.Add(new FallenRecord(resident, where, atExtraction, raidsAlong, forgotten));
        }
    }

    public void RestoreVacantJob(JobDefinition job)
    {
        if (job != null && !vacantJobs.Contains(job))
        {
            vacantJobs.Add(job);
        }
    }

    /// <summary>The loss still spoken of, for a resident to mention, or null when it has been long enough.</summary>
    public FallenRecord RecentLoss(int raidsItLasts)
    {
        if (fallen.Count == 0)
        {
            return null;
        }

        FallenRecord last = fallen[fallen.Count - 1];
        return SuccessfulExtractions - last.AtExtraction <= raidsItLasts ? last : null;
    }

    private bool IsHeld(JobDefinition job) => residents.Exists(record => record.Job == job);

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
            vacantJobs.Remove(job);
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
