using System;
using System.Collections.Generic;

/// <summary>
/// The Haven one stands in, to read (<see cref="GameSession.Here"/>): in a friend's Haven (co-op) it is the copy of the host's,
/// which the host's word keeps (SettlementState.TakeOn) and a guest never changes, so nothing here changes it. Change one's own
/// through <see cref="GameSession.Home"/> (Docs/BugPatterns.md, guard 9). Implemented by <see cref="SettlementState"/>.
/// <para>
/// A few getters still hand out an object that can be changed (Territory, Workshops, a resident's record, a house's comforts):
/// read them here, change them through one's own Haven.
/// </para>
/// </summary>
public interface IHavenView
{
    /// <summary>Raised after anything in the settlement changes: listening is reading.</summary>
    event Action Changed;

    /// <summary>True while this is the copy of the host's settlement a guest plays in (co-op).</summary>
    bool IsBorrowed { get; }

    bool PortalRepaired { get; }
    int PortalLevel { get; }
    int WardLevel { get; }
    int HavenRank { get; }
    int SuccessfulExtractions { get; }
    int DaysPassed { get; }
    float Blight { get; }
    TerritoryState Territory { get; }

    IReadOnlyList<string> Requests { get; }
    int RequestsAnswered { get; }
    int OrderRenown { get; }
    int RequestsAskedAt { get; }

    IReadOnlyCollection<string> SpiritGifts { get; }
    bool HasSpiritGift(string id);
    int HuldraRespect { get; }
    int HuldraAwayUntil { get; }

    BreachStatus Breach { get; }
    int BreachesWon { get; }
    int NextBreachAt { get; }
    int BreachPendingSince { get; }
    int WardStrain { get; }
    float BreachWeakening { get; }
    bool BreachIsNear { get; }
    int NextBreachDueAt { get; }

    IReadOnlyList<ResidentRecord> Residents { get; }

    /// <summary>Rings drawn in the Keeper's view for the woodcutters to clear (RingWork).</summary>
    IReadOnlyList<RingOrder> Rings { get; }

    /// <summary>Guard posts set in the Keeper's view (GuardPosts), in order.</summary>
    IReadOnlyList<UnityEngine.Vector3> Posts { get; }

    /// <summary>The land a party of <paramref name="kind"/> goes out to on raids (LandParties), or null.</summary>
    string PartyOn(LandPartyKind kind);
    /// <summary>The outposts (Outposts): each land's keeper's resident id.</summary>
    IReadOnlyDictionary<string, string> Outposts { get; }
    /// <summary>The resident id keeping the outpost on <paramref name="region"/>, or null.</summary>
    string OutpostKeeper(string region);
    /// <summary>True once every land has been Secure at once (the ring held): a safe pocket slot more from then on.</summary>
    bool RingHeld { get; }
    ResidentRecord Find(ResidentDefinition resident);
    bool HasResident(ResidentDefinition resident);
    ResidentRecord WorkerAt(CraftingStationType station);
    ResidentRecord WorkerAt(string stationId, CraftingStationType station);
    float WorkerSpeed(CraftingStationType station);
    float WorkerSpeed(string stationId, CraftingStationType station);
    bool HasWorker(JobDefinition job);
    List<JobDefinition> JobsOpenTo(ResidentDefinition resident);
    /// <summary>The trades known in the Haven (someone whose written trade it is lives here or is lost).</summary>
    List<JobDefinition> KnownJobs();
    /// <summary>True when the job has room for one more who isn't its written holder (SettlementState.HasRoom).</summary>
    bool HasRoom(JobDefinition job, ResidentDefinition resident);
    /// <summary>The resident id a standing ask from who they are was posted for (SettlementState.RequestAsker), or null.</summary>
    string RequestAsker(string id);

    int BuiltCount(BuildingDefinition building);
    IReadOnlyList<JobDefinition> VacantJobs { get; }
    int JobBonus(Func<JobDefinition, int> bonus);
    float JobBonus(Func<JobDefinition, float> bonus);

    IReadOnlyList<FallenRecord> Fallen { get; }
    /// <summary>How two residents stand with each other, by their ids: -2.5 (Rival) to 2.5 (Friend) (PeopleStandings).</summary>
    float StandingOf(string a, string b);
    FallenRecord LostToFind { get; }
    bool IsFallen(ResidentDefinition resident);
    FallenRecord RecentLoss(int raidsItLasts);

    int Beds { get; }
    int Housed { get; }
    bool HomesKnown { get; }
    bool HasBed(ResidentRecord record);
    IReadOnlyDictionary<string, List<string>> HouseComforts { get; }
    IReadOnlyList<string> ComfortsIn(string house);
    bool HasComfort(string house, string comfort);
    float BuilderSeconds { get; }
    string Footpaths { get; }

    int BuiltTier(BuildingDefinition building);
    int BuiltTier(CraftingStationType station);
    int SmithyTier { get; }
    int UpgradeBonus(Func<BuildingBonus, int> bonus);
    bool ScaffoldsWaiting { get; }

    WorkshopState Workshops { get; }
    int PurificationQueued { get; }
    float PurificationProgress { get; }
    float PurificationBonus { get; }
    float PurificationYield { get; }
    float PurifiedRemainder { get; }

    IReadOnlyCollection<string> DefeatedBosses { get; }
    bool IsBossDefeated(string id);
    string LastBossBeaten { get; }
    int LastBossBeatenAt { get; }

    int HeldQuestsHomecoming { get; }
    int HeldQuestsStarted { get; }

    int BoughtSinceLastRaid(string offer);
    IEnumerable<KeyValuePair<string, int>> TradeBought { get; }
}
