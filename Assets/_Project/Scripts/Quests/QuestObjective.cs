using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>What an objective asks for. Values are serialized as numbers, so add new types at the end and never renumber.</summary>
public enum ObjectiveType
{
    /// <summary>Get X of an item into the bag: looted, crafted, found in caches.</summary>
    CollectItem = 0,
    /// <summary>Gather X of a material item or an essence: from nodes, monster loot or caches.</summary>
    GatherResource = 1,
    /// <summary>Kill X of a monster, or any of a faction (enemies).</summary>
    KillMonster = 2,
    /// <summary>Destroy X of a building (or any structure).</summary>
    DestroyBuilding = 3,
    /// <summary>Craft X of an item, or of a resource such as Oak Plank.</summary>
    CraftItem = 4,
    /// <summary>Finish X raids, optionally in one place, by extracting, within a time.</summary>
    CompleteRaids = 5,
    /// <summary>Have a Story Event happen X times: a moment only a story or a scene knows (a lesson, an answer chosen).
    /// For talking, reaching and using, the dynamic types below need no event made for each.</summary>
    [InspectorName("Story Event (one-off moment)")]
    QuestEvent = 6,
    /// <summary>Build X of a structure (or any structure) with the build menu.</summary>
    BuildStructure = 7,
    /// <summary>Land X fish with a rod: of one kind, or any fish.</summary>
    CatchFish = 8,
    /// <summary>Talk to someone (their Resident Definition) or something (a Quest Target, such as the Portal).</summary>
    TalkTo = 9,
    /// <summary>Reach a place: a Quest Target that a Quest Zone in a scene is marked with.</summary>
    ReachPlace = 10,
    /// <summary>Use, inspect or light a thing: a Quest Target that an Inspect Point in a scene is marked with.</summary>
    UseObject = 11,
    /// <summary>Find and free a stranded person, or bring them home: one person, or anyone.</summary>
    RescuePerson = 12,
    /// <summary>Give someone work: a job, a person, or both.</summary>
    AssignJob = 13,
    /// <summary>Beat back a ward breach: one kind, or any.</summary>
    DefendBreach = 14,
    /// <summary>Raise Safe Haven to a rank (Haven Settings › Ranks), counting a rank already reached.</summary>
    ReachHavenRank = 15,
    /// <summary>Put X of an item, or anything, in the stash at home (a chest).</summary>
    StoreItem = 16,
    /// <summary>Repair X pieces of gear, or one kind of item: at the Workshop, or with a repair kit.</summary>
    RepairItem = 17,
    /// <summary>Raise X of a building (or any building) to a tier: 2 is its first upgrade.</summary>
    UpgradeBuilding = 18,
    /// <summary>Enchant X pieces of gear at the altar: with one enchantment, any, or any great one.</summary>
    EnchantItem = 19,
    /// <summary>Learn X enchantments (an inscription read at home, or a teacher): one, any, any great one, or any of one source.</summary>
    LearnEnchantment = 20,
    /// <summary>Hand X inscriptions to someone who carves (Liv): to the person named, or anyone.</summary>
    GiveInscription = 21,
}

/// <summary>
/// One step of a quest: "Gather 10 Iron Ore", "Kill 3 Forest-Bitten Wolves", "Extract from 2 raids in the Misty Woods". Pick a type
/// and fill in the fields it uses (the Inspector shows only those). It counts matching game events; it holds no progress
/// itself, which the QuestLog keeps per player.
/// <para>
/// Every rule of a type lives here. Extending, to add an objective type: 1) add an ObjectiveType at the end, 2) add any
/// field it needs, 3) list its fields in <see cref="FieldsOf"/> (the Inspector shows those), 4) count it in
/// <see cref="Progress"/> from a GameEventKind (add one in GameEvents if nothing reports it yet), 5) word it in
/// <see cref="Describe"/>; check it in <see cref="FindProblem"/> and, if it can be met before the quest starts, in
/// <see cref="Owned"/>.
/// </para>
/// <para>
/// The deed types (Talk To, Reach Place, Use Object, Rescue Person) name their target as an asset, so one type serves every
/// person, place and thing: a designer assigns who or what, and makes no new event. What raises each is in GameEventKind.
/// </para>
/// </summary>
[Serializable]
public sealed class QuestObjective
{
    [SerializeField] private ObjectiveType type;
    [Tooltip("Replaces the automatic text, e.g. \"Clear the wolf den\". Empty writes it from the settings.")]
    [SerializeField] private string description;
    [SerializeField, Min(1)] private int amount = 1;

    [SerializeField] private ItemDefinition item;
    [SerializeField] private ResourceDefinition resource;
    [Tooltip("Empty counts any kill of the Faction below.")]
    [SerializeField] private MonsterDefinition monster;
    [Tooltip("With no monster set: whose kills count. Enemy = monsters and hostile people; Ally = friendly NPCs.")]
    [SerializeField] private Faction faction = Faction.Enemy;
    [Tooltip("Empty counts any structure.")]
    [SerializeField] private BuildingDefinition building;
    [Tooltip("Empty counts raids anywhere.")]
    [SerializeField] private RaidDefinition raid;
    [Tooltip("Only raids the player got out of alive count.")]
    [SerializeField] private bool mustExtract = true;
    [Tooltip("Only raids finished within this many minutes count. 0 ignores time.")]
    [SerializeField, Min(0f)] private float withinMinutes;
    [SerializeField] private GameEventDefinition questEvent;
    [Tooltip("Who: their Resident Definition. Empty, for Rescue Person: anyone; for Assign Job: whoever gets it.")]
    [SerializeField] private ResidentDefinition person;
    [Tooltip("The place or thing (a Quest Target). For Talk To, instead of a person: the Portal, a shrine.")]
    [SerializeField] private QuestTargetDefinition target;
    [Tooltip("Empty: any job.")]
    [SerializeField] private JobDefinition job;
    [Tooltip("Empty: any ward breach.")]
    [SerializeField] private WardBreachDefinition breach;
    [Tooltip("The rank to reach: 1 is the first above where Safe Haven starts (Haven Settings › Ranks).")]
    [SerializeField, Min(1)] private int rank = 1;
    [Tooltip("The tier to raise it to: 2 is its first upgrade (Tier II).")]
    [SerializeField, Min(2)] private int tier = 2;
    [Tooltip("Off: finding and freeing them is enough. On: they must come home with the player.")]
    [SerializeField] private bool bringHome;
    [Tooltip("Counts what the player already has when the quest starts, including a Quest Event that already happened (counts once).")]
    [SerializeField] private bool countAlreadyOwned;
    [Tooltip("Empty: any enchantment.")]
    [SerializeField] private EnchantmentDefinition enchantment;
    [Tooltip("Only great enchantments count.")]
    [SerializeField] private bool greatOnly;
    [Tooltip("Only enchantments whose inscriptions come from the Source below count.")]
    [SerializeField] private bool onlySource;
    [SerializeField] private EnchantmentSource source = EnchantmentSource.Elite;

    public ObjectiveType Type => type;

    /// <summary>Whether a raid can move this on whatever is out there. A Quest Event depends on the place: see what has a waypoint there.</summary>
    public bool CanProgressOnARaid => type != ObjectiveType.CraftItem && type != ObjectiveType.BuildStructure && type != ObjectiveType.QuestEvent
        && type != ObjectiveType.AssignJob && type != ObjectiveType.ReachHavenRank && type != ObjectiveType.StoreItem
        && type != ObjectiveType.RepairItem && type != ObjectiveType.UpgradeBuilding && type != ObjectiveType.EnchantItem
        && type != ObjectiveType.LearnEnchantment && type != ObjectiveType.GiveInscription;
    public int Amount => Mathf.Max(1, amount);
    public bool CountAlreadyOwned => countAlreadyOwned && Array.IndexOf(FieldsOf(type), nameof(countAlreadyOwned)) >= 0;
    public ItemDefinition Item => item;
    public ResourceDefinition Resource => resource;
    /// <summary>What a gather objective counts: the material item, or the essence when no item is set.</summary>
    public Object GatherTarget => item != null ? (Object)item : resource;
    /// <summary>The structure to build or destroy; null counts any.</summary>
    public BuildingDefinition Building => building;
    public GameEventDefinition QuestEvent => questEvent;
    public ResidentDefinition Person => person;
    public QuestTargetDefinition Target => target;
    public JobDefinition Job => job;
    public WardBreachDefinition Breach => breach;
    public int Rank => Mathf.Max(1, rank);
    /// <summary>The tier an Upgrade Building objective asks for, from 2.</summary>
    public int Tier => Mathf.Max(2, tier);
    public bool BringHome => bringHome;

    /// <summary>Who or what a deed objective is about (Talk To, Reach Place, Use Object, Rescue Person); null otherwise.</summary>
    public Object DeedTarget
    {
        get
        {
            switch (type)
            {
                case ObjectiveType.TalkTo: return person != null ? (Object)person : target;
                case ObjectiveType.ReachPlace:
                case ObjectiveType.UseObject: return target;
                case ObjectiveType.RescuePerson: return person;
                default: return null;
            }
        }
    }

    /// <summary>The kind of game event a deed objective waits for; null for other types.</summary>
    public GameEventKind? DeedKind
    {
        get
        {
            switch (type)
            {
                case ObjectiveType.TalkTo: return GameEventKind.Talked;
                case ObjectiveType.ReachPlace: return GameEventKind.Reached;
                case ObjectiveType.UseObject: return GameEventKind.Used;
                case ObjectiveType.RescuePerson: return bringHome ? GameEventKind.PersonRescued : GameEventKind.PersonFreed;
                default: return null;
            }
        }
    }

    /// <summary>The key the quest log keeps once this deed is done (QuestDeeds); null when it isn't one, or has no target.</summary>
    public string DeedKey => DeedKind.HasValue ? QuestDeeds.Key(DeedKind.Value, DeedTarget) : null;

    public static QuestObjective Create(ObjectiveType type, int amount, Object target = null, bool mustExtract = true, float withinMinutes = 0f,
        Faction faction = Faction.Enemy)
    {
        var objective = new QuestObjective { type = type, amount = amount, mustExtract = mustExtract, withinMinutes = withinMinutes, faction = faction };
        objective.item = target as ItemDefinition;
        objective.resource = target as ResourceDefinition;
        objective.monster = target as MonsterDefinition;
        objective.building = target as BuildingDefinition;
        objective.raid = target as RaidDefinition;
        objective.questEvent = target as GameEventDefinition;
        objective.person = target as ResidentDefinition;
        objective.target = target as QuestTargetDefinition;
        objective.job = target as JobDefinition;
        objective.breach = target as WardBreachDefinition;
        return objective;
    }

    /// <summary>A deed objective ("talk to Sigrid", "bring Ingrid home") for code and tools; also used as a quest's or a
    /// story's "when the player does" condition.</summary>
    public static QuestObjective Deed(ObjectiveType type, Object who, bool home = false)
    {
        QuestObjective objective = Create(type, 1, who);
        objective.bringHome = home;
        return objective;
    }

    /// <summary>A Reach Haven Rank objective.</summary>
    public static QuestObjective HavenRank(int haveRank, bool countReached = true)
    {
        return new QuestObjective { type = ObjectiveType.ReachHavenRank, rank = Mathf.Max(1, haveRank), countAlreadyOwned = countReached };
    }

    /// <summary>An Upgrade Building objective: raise the building (null: any) to a tier, counting one standing there already.</summary>
    public static QuestObjective Upgrade(BuildingDefinition building, int toTier, bool countReached = true)
    {
        return new QuestObjective { type = ObjectiveType.UpgradeBuilding, building = building, tier = Mathf.Max(2, toTier), countAlreadyOwned = countReached };
    }

    /// <summary>The serialized fields an objective of <paramref name="type"/> uses, besides its type, amount and text.</summary>
    public static string[] FieldsOf(ObjectiveType type)
    {
        switch (type)
        {
            case ObjectiveType.CollectItem:
                return new[] { nameof(item), nameof(countAlreadyOwned) };
            case ObjectiveType.GatherResource:
                return new[] { nameof(item), nameof(resource), nameof(countAlreadyOwned) };
            case ObjectiveType.KillMonster:
                return new[] { nameof(monster), nameof(faction), nameof(countAlreadyOwned) };
            case ObjectiveType.DestroyBuilding:
                return new[] { nameof(building) };
            case ObjectiveType.CraftItem:
                return new[] { nameof(item), nameof(resource), nameof(countAlreadyOwned) };
            case ObjectiveType.CompleteRaids:
                return new[] { nameof(raid), nameof(mustExtract), nameof(withinMinutes) };
            case ObjectiveType.QuestEvent:
                return new[] { nameof(questEvent), nameof(countAlreadyOwned) };
            case ObjectiveType.BuildStructure:
                return new[] { nameof(building), nameof(countAlreadyOwned) };
            case ObjectiveType.CatchFish:
                return new[] { nameof(item) };
            case ObjectiveType.TalkTo:
                return new[] { nameof(person), nameof(target), nameof(countAlreadyOwned) };
            case ObjectiveType.ReachPlace:
            case ObjectiveType.UseObject:
                return new[] { nameof(target), nameof(countAlreadyOwned) };
            case ObjectiveType.RescuePerson:
                return new[] { nameof(person), nameof(bringHome), nameof(countAlreadyOwned) };
            case ObjectiveType.AssignJob:
                return new[] { nameof(job), nameof(person), nameof(countAlreadyOwned) };
            case ObjectiveType.DefendBreach:
                return new[] { nameof(breach) };
            case ObjectiveType.ReachHavenRank:
                return new[] { nameof(rank), nameof(countAlreadyOwned) };
            case ObjectiveType.StoreItem:
                return new[] { nameof(item), nameof(countAlreadyOwned) };
            case ObjectiveType.RepairItem:
                return new[] { nameof(item) };
            case ObjectiveType.UpgradeBuilding:
                return new[] { nameof(building), nameof(tier), nameof(countAlreadyOwned) };
            case ObjectiveType.EnchantItem:
                return new[] { nameof(enchantment), nameof(greatOnly) };
            case ObjectiveType.LearnEnchantment:
                return new[] { nameof(enchantment), nameof(greatOnly), nameof(onlySource), nameof(source) };
            case ObjectiveType.GiveInscription:
                return new[] { nameof(person) };
            default:
                return Array.Empty<string>();
        }
    }

    /// <summary>How much the event advances this objective; 0 when it doesn't count.</summary>
    // ---- Co-op: where an objective counts, decided here once for every type, so no quest needs a multiplayer check.

    /// <summary>
    /// Whether each type moves on while the player visits a friend's Haven. What the player does with their own hands counts
    /// anywhere: kills, gathering, crafting, fish, raids, a rescue (the traveller goes to their own Haven), reaching a place,
    /// mending their own gear. What belongs to a Haven counts only in their own: its buildings, stash, jobs, rank and breaches,
    /// and talking to or using what is the friend's (their people, their portal, their things), which a quest at home means.
    /// A Story Event says so itself (Quest Event › Home Only). On a raid followed with the host everything counts as usual.
    /// A new objective type must be added here (QuestCoopTests checks every type is).
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ObjectiveType, bool> InAFriendsHaven = new System.Collections.Generic.Dictionary<ObjectiveType, bool>
    {
        [ObjectiveType.CollectItem] = true,
        [ObjectiveType.GatherResource] = true,
        [ObjectiveType.KillMonster] = true,
        [ObjectiveType.DestroyBuilding] = false,
        [ObjectiveType.CraftItem] = true,
        [ObjectiveType.CompleteRaids] = true,
        [ObjectiveType.QuestEvent] = true,
        [ObjectiveType.BuildStructure] = false,
        [ObjectiveType.CatchFish] = true,
        [ObjectiveType.TalkTo] = false,
        [ObjectiveType.ReachPlace] = true,
        [ObjectiveType.UseObject] = false,
        [ObjectiveType.RescuePerson] = true,
        [ObjectiveType.AssignJob] = false,
        [ObjectiveType.DefendBreach] = false,
        [ObjectiveType.ReachHavenRank] = false,
        [ObjectiveType.StoreItem] = false,
        [ObjectiveType.RepairItem] = true,
        [ObjectiveType.UpgradeBuilding] = false,
        // The altar and the carver are a Haven's: a visitor works at their own.
        [ObjectiveType.EnchantItem] = false,
        // What the player learns is their own, wherever they read it.
        [ObjectiveType.LearnEnchantment] = true,
        [ObjectiveType.GiveInscription] = false,
    };

    /// <summary>The types the co-op rule covers (for the check that none is missing).</summary>
    public static System.Collections.Generic.IEnumerable<ObjectiveType> TypesWithACoopRule => InAFriendsHaven.Keys;

    /// <summary>Whether objectives of this type count while visiting a friend's Haven (Story Events: by their own Home Only).</summary>
    public static bool CountsInAFriendsHaven(ObjectiveType type) => InAFriendsHaven.TryGetValue(type, out bool counts) && counts;

    /// <summary>Whether this objective counts while visiting a friend's Haven.</summary>
    public bool CountsInAFriendsHavenNow => type == ObjectiveType.QuestEvent ? questEvent == null || !questEvent.HomeOnly : CountsInAFriendsHaven(type);

    /// <summary>Whether this objective can move on where the player is now: anywhere but a friend's Haven, or there if it counts there.</summary>
    public bool CountsHere => !HavenVisit.InHostsHaven || CountsInAFriendsHavenNow;

    public int Progress(in GameEvent gameEvent)
    {
        if (!gameEvent.ForPlayerSide || gameEvent.Amount <= 0 || !CountsHere)
        {
            return 0;
        }

        switch (type)
        {
            case ObjectiveType.CollectItem:
                // Only what is newly had (ArrivalCauses): a refund or a move raised as an arrival would never count.
                return gameEvent.Kind == GameEventKind.ItemObtained && ArrivalCauses.IsNewlyHad(gameEvent.Cause) && Is(item, gameEvent.Subject, true) ? gameEvent.Amount : 0;
            case ObjectiveType.GatherResource:
                // Material items arrive in the bag like any item (gathered, looted, found); essences arrive as ResourceGathered.
                GameEventKind arrives = item != null ? GameEventKind.ItemObtained : GameEventKind.ResourceGathered;
                return gameEvent.Kind == arrives && ArrivalCauses.IsNewlyHad(gameEvent.Cause) && Is(GatherTarget, gameEvent.Subject, true) ? gameEvent.Amount : 0;
            case ObjectiveType.KillMonster:
                // With no monster set, kills of the chosen side count: "kill 5 enemies" never counts the player, a
                // companion or a traveller falling.
                if (gameEvent.Kind != GameEventKind.Killed) return 0;
                return (monster != null ? monster == gameEvent.Subject : gameEvent.KilledFaction == faction) ? gameEvent.Amount : 0;
            case ObjectiveType.DestroyBuilding:
                return gameEvent.Kind == GameEventKind.Destroyed && Is(building, gameEvent.Subject, false) ? gameEvent.Amount : 0;
            case ObjectiveType.CraftItem:
                return gameEvent.Kind == GameEventKind.Crafted && Is(item != null ? (Object)item : resource, gameEvent.Subject, true) ? gameEvent.Amount : 0;
            case ObjectiveType.CompleteRaids:
                bool counts = gameEvent.Kind == GameEventKind.RaidFinished && Is(raid, gameEvent.Subject, false)
                    && (!mustExtract || gameEvent.Outcome == RaidOutcome.Extracted)
                    && (withinMinutes <= 0f || gameEvent.Seconds <= withinMinutes * 60f);
                return counts ? 1 : 0;
            case ObjectiveType.BuildStructure:
                return gameEvent.Kind == GameEventKind.Built && Is(building, gameEvent.Subject, false) ? gameEvent.Amount : 0;
            case ObjectiveType.QuestEvent:
                return gameEvent.Kind == GameEventKind.Custom && questEvent != null && gameEvent.Subject == questEvent ? gameEvent.Amount : 0;
            case ObjectiveType.CatchFish:
                // An empty item counts any fish.
                return gameEvent.Kind == GameEventKind.FishCaught && Is(item, gameEvent.Subject, false) ? gameEvent.Amount : 0;
            case ObjectiveType.TalkTo:
            case ObjectiveType.ReachPlace:
            case ObjectiveType.UseObject:
                return gameEvent.Kind == DeedKind && DeedTarget != null && gameEvent.Subject == DeedTarget ? gameEvent.Amount : 0;
            case ObjectiveType.RescuePerson:
                // An empty person counts anyone found (or brought home).
                return gameEvent.Kind == DeedKind && Is(person, gameEvent.Subject, false) ? gameEvent.Amount : 0;
            case ObjectiveType.AssignJob:
                return gameEvent.Kind == GameEventKind.JobAssigned && Is(job, gameEvent.Subject, false) && Is(person, gameEvent.Detail, false) ? 1 : 0;
            case ObjectiveType.DefendBreach:
                return gameEvent.Kind == GameEventKind.BreachRepelled && Is(breach, gameEvent.Subject, false) ? 1 : 0;
            case ObjectiveType.ReachHavenRank:
                return gameEvent.Kind == GameEventKind.HavenRankReached && gameEvent.Amount >= Rank ? 1 : 0;
            case ObjectiveType.StoreItem:
                // An empty item counts anything put away, essences too.
                return gameEvent.Kind == GameEventKind.Stored && Is(item, gameEvent.Subject, false) ? gameEvent.Amount : 0;
            case ObjectiveType.RepairItem:
                return gameEvent.Kind == GameEventKind.Repaired && Is(item, gameEvent.Subject, false) ? gameEvent.Amount : 0;
            case ObjectiveType.UpgradeBuilding:
                // Amount carries the tier reached, not a count: one building raised is one.
                return gameEvent.Kind == GameEventKind.Upgraded && Is(building, gameEvent.Subject, false) && gameEvent.Amount >= Tier ? 1 : 0;
            case ObjectiveType.EnchantItem:
                return gameEvent.Kind == GameEventKind.Enchanted && EnchantmentCounts(gameEvent.Subject) ? 1 : 0;
            case ObjectiveType.LearnEnchantment:
                return gameEvent.Kind == GameEventKind.EnchantmentLearned && EnchantmentCounts(gameEvent.Subject)
                       && (!onlySource || (gameEvent.Subject is EnchantmentDefinition learned && learned.Source == source)) ? 1 : 0;
            case ObjectiveType.GiveInscription:
                return gameEvent.Kind == GameEventKind.InscriptionGiven && Is(person, gameEvent.Detail, false) ? 1 : 0;
            default:
                return 0;
        }
    }

    // The enchantment named, or any; great ones only when asked.
    private bool EnchantmentCounts(Object subject) =>
        subject is EnchantmentDefinition done && (enchantment == null || done == enchantment) && (!greatOnly || done.IsGreat);

    /// <summary>True when the event is what this objective waits for: a story's or a quest's "when the player does".</summary>
    public bool Matches(in GameEvent gameEvent) => Progress(gameEvent) > 0;

    /// <summary>What already counts when the quest starts, with Count Already Owned: what the player holds (bag and gear,
    /// or their essences), the structures standing, or a Quest Event that already happened (once). 0 otherwise.</summary>
    public int Owned(Player player, QuestLog log)
    {
        // In a friend's Haven what stands, is stored or has its rank there is theirs (CountsHere); it is counted on coming home.
        if (!CountAlreadyOwned || !CountsHere)
        {
            return 0;
        }

        // What the player carries needs the player (not yet there while a scene loads); deeds and states need only the log.
        switch (type)
        {
            case ObjectiveType.QuestEvent:
                return log != null && log.HasHappened(questEvent) ? 1 : 0;
            case ObjectiveType.CollectItem:
            case ObjectiveType.CraftItem: // made before the quest (an older save, a found tool): having it is enough
                return item == null || player == null ? 0 : player.Bag.Container.Count(item) + (player.Equipment != null ? player.Equipment.Gear.Slots.Count(item) : 0);
            case ObjectiveType.BuildStructure:
                return BuildingObject.Count(building);
            case ObjectiveType.GatherResource:
                return player == null ? 0 : item != null ? player.Bag.Container.Count(item) : player.Resources.Storage.GetAmount(resource);
            case ObjectiveType.TalkTo:
            case ObjectiveType.ReachPlace:
            case ObjectiveType.UseObject:
                return log != null && log.HasDone(DeedKey) ? 1 : 0;
            case ObjectiveType.RescuePerson:
                // Home already (an older save, or rescued before the quest): that is found and home both. Home is the player's own
                // Haven: in a friend's (co-op) the settlement is the host's, where the person may long have lived.
                return log != null && (log.HasDone(DeedKey) || (person != null && LivesAtHome(person))) ? 1 : 0;
            case ObjectiveType.AssignJob:
                return HasJob() ? 1 : 0;
            case ObjectiveType.ReachHavenRank:
                return GameSession.Home.HavenRank >= Rank ? 1 : 0; // the player's own Haven's rank, also on a friend's raid
            case ObjectiveType.StoreItem:
                // Already in the stash (an older save, or put away before the quest): it is safe, which is the point. One's own stash
                // only: a guest's game holds the host's here, also on a raid followed with them (one's own waits in the kept home game).
                if (HavenVisit.IsVisiting)
                {
                    return 0;
                }

                StashStorage stash = GameSession.Stash;
                return stash == null ? 0 : item != null ? stash.Items.Count(item) : stash.Items.CountItems();
            case ObjectiveType.UpgradeBuilding:
                return StandingAtTier();
            case ObjectiveType.KillMonster:
                // A boss beaten before the quest (an older save, or the player got there first) counts: it won't come again.
                return BossBeaten() ? Amount : 0;
            default:
                return 0;
        }
    }

    private bool BossBeaten()
    {
        BossEncounter boss = monster != null && monster.Prefab != null ? monster.Prefab.GetComponent<BossEncounter>() : null;
        if (boss == null || boss.Definition == null)
        {
            return false;
        }

        // Beaten for the player's own Haven: in a friend's, the Haven here is the host's, and their win isn't this player's.
        return GameSession.Home.IsBossDefeated(boss.Definition.Id);
    }

    private static bool LivesAtHome(ResidentDefinition person) => GameSession.Home.HasResident(person);

    // Buildings of the kind (or any) standing at the tier asked for or above.
    private int StandingAtTier()
    {
        int count = 0;
        foreach (BuildingObject built in BuildingObject.Active)
        {
            if (built.Definition != null && (building == null || built.Definition == building)
                && built.TryGetComponent(out Construction construction) && construction.Level >= Tier)
            {
                count++;
            }
        }

        return count;
    }

    // An empty target counts anything only where the objective allows "any" (kills, raids); items and resources must be named.
    private static bool Is(Object wanted, Object actual, bool required)
    {
        return wanted == null ? !required : wanted == actual;
    }

    /// <summary>The objective in words, without progress: "Gather 10 Iron Ore".</summary>
    public string Describe()
    {
        if (!string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        string count = Amount.ToString();
        switch (type)
        {
            case ObjectiveType.CollectItem:
                return $"Collect {count} {Name(item)}";
            case ObjectiveType.GatherResource:
                return $"Gather {count} {Name(GatherTarget)}";
            case ObjectiveType.KillMonster:
                return $"Kill {count} {(monster != null ? monster.DisplayName : faction.Noun(Amount != 1))}";
            case ObjectiveType.DestroyBuilding:
                return $"Destroy {count} {(building != null ? building.DisplayName : Amount == 1 ? "structure" : "structures")}";
            case ObjectiveType.CraftItem:
                return $"Craft {count} {(item != null ? Name(item) : Name(resource))}";
            case ObjectiveType.BuildStructure:
                return $"Build {(Amount == 1 ? "a" : count)} {(building != null ? building.DisplayName : Amount == 1 ? "structure" : "structures")} (build menu: {{key:Build}})";
            case ObjectiveType.CompleteRaids:
                string verb = mustExtract ? "Extract from" : "Complete";
                string where = raid != null ? $" in {raid.DisplayName}" : "";
                string time = withinMinutes > 0f ? $" within {RaidStats.FormatTime(withinMinutes * 60f)}" : "";
                return $"{verb} {count} raid{(Amount == 1 ? "" : "s")}{where}{time}";
            case ObjectiveType.QuestEvent:
                string text = questEvent != null ? questEvent.ObjectiveText : "(no quest event)";
                return Amount > 1 ? $"{text} ({count} times)" : text;
            case ObjectiveType.CatchFish:
                return $"Catch {count} {(item != null ? Name(item) : "fish")}";
            case ObjectiveType.TalkTo:
                return $"Talk to {(DeedTarget != null ? Name(DeedTarget) : "(no one chosen)")}";
            case ObjectiveType.ReachPlace:
                return $"Reach {(target != null ? target.DisplayName : "(no place chosen)")}";
            case ObjectiveType.UseObject:
                return target != null ? $"{target.UseVerb} {target.DisplayName}" : "Use (nothing chosen)";
            case ObjectiveType.RescuePerson:
                string who = person != null ? person.DisplayName : Amount == 1 ? "a stranded traveller" : $"{count} stranded travellers";
                return bringHome ? $"Bring {who} home" : $"Find {who}";
            case ObjectiveType.AssignJob:
                return job != null ? $"Give {(person != null ? person.DisplayName : "someone")} work as {job.DisplayName}"
                    : $"Give {(person != null ? person.DisplayName : "someone")} work";
            case ObjectiveType.DefendBreach:
                return breach != null ? $"Defend Safe Haven: {breach.DisplayName}" : "Defend Safe Haven from a ward breach";
            case ObjectiveType.ReachHavenRank:
                return $"Raise Safe Haven to {HavenRankName(Rank)} ({{key:Haven}})";
            case ObjectiveType.StoreItem:
                return item != null ? $"Put {count} {Name(item)} in a chest" : Amount == 1 ? "Put something in a chest" : $"Put {count} things in a chest";
            case ObjectiveType.RepairItem:
                string bench = DurabilitySettings.Current != null ? DurabilitySettings.Current.RepairStation.DisplayName() : "Workshop";
                return item != null ? $"Repair your {Name(item)} ({bench})" : Amount == 1 ? $"Repair a piece of gear ({bench})" : $"Repair {count} pieces of gear ({bench})";
            case ObjectiveType.EnchantItem:
                string carved = enchantment != null ? enchantment.DisplayName : greatOnly ? "a great enchantment" : "an enchantment";
                return Amount == 1 ? $"Carve {carved} into your gear at the altar" : $"Carve {carved} into {count} pieces of gear at the altar";
            case ObjectiveType.LearnEnchantment:
                string what = enchantment != null ? enchantment.DisplayName : greatOnly ? "great enchantment" : "enchantment";
                string from = onlySource ? source switch
                {
                    EnchantmentSource.CarvedStone => " from a carved stone's rubbing",
                    EnchantmentSource.Elite => " carried by an elite",
                    EnchantmentSource.Boss => " carried by a great creature",
                    EnchantmentSource.Found => " found in the woods",
                    _ => "",
                } : "";
                return enchantment != null ? $"Learn {what} (read its inscription at home)"
                    : Amount == 1 ? $"Learn an {what}{from} (read its inscription at home)" : $"Learn {count} {what}s{from}";
            case ObjectiveType.GiveInscription:
                string carver = person != null ? person.DisplayName : "someone who carves";
                return Amount == 1 ? $"Give {carver} an inscription" : $"Give {carver} {count} inscriptions";
            case ObjectiveType.UpgradeBuilding:
                string which = building != null ? $"the {building.DisplayName}" : "a building";
                return Amount == 1 ? $"Raise {which} to Tier {Tiers.ToRoman(Tier)} ({{key:Haven}})" : $"Raise {count} of {which} to Tier {Tiers.ToRoman(Tier)} ({{key:Haven}})";
            default:
                return type.ToString();
        }
    }

    /// <summary>What is wrong with the objective's settings, or null when it is fine.</summary>
    public string FindProblem()
    {
        switch (type)
        {
            case ObjectiveType.CollectItem:
                return item == null ? "has no item" : null;
            case ObjectiveType.CraftItem:
                return item == null && resource == null ? "has no item or resource" : null;
            case ObjectiveType.GatherResource:
                return GatherTarget == null ? "has no material or essence" : null;
            case ObjectiveType.QuestEvent:
                return questEvent == null ? "has no story event" : null;
            case ObjectiveType.TalkTo:
                return person == null && target == null ? "has no one to talk to" : null;
            case ObjectiveType.ReachPlace:
                return target == null ? "has no place" : null;
            case ObjectiveType.UseObject:
                return target == null ? "has no object" : null;
            case ObjectiveType.ReachHavenRank:
                HavenSettings ranks = HavenSettings.Current;
                return ranks != null && Rank >= ranks.Ranks.Count ? $"asks for rank {Rank}, but Safe Haven has only {ranks.Ranks.Count} ranks" : null;
            case ObjectiveType.UpgradeBuilding:
                return building != null && Tier > building.MaxLevel ? $"asks for Tier {Tiers.ToRoman(Tier)}, but the {building.DisplayName} goes up to Tier {Tiers.ToRoman(building.MaxLevel)}" : null;
            default:
                return null;
        }
    }

    private static string Name(Object definition) => definition is IDisplayName named ? named.DisplayName
        : definition is IQuestTarget target ? target.DisplayName : "(nothing chosen)";

    // Someone holds the job (the person named, or anyone).
    private bool HasJob()
    {
        // The player's own people's jobs, also on a friend's raid.
        SettlementState settlement = GameSession.Home;
        if (person != null)
        {
            ResidentRecord record = settlement.Find(person);
            return record != null && (job == null ? record.Job != null : record.Job == job);
        }

        return job != null && settlement.HasWorker(job);
    }

    private static string HavenRankName(int index)
    {
        HavenSettings settings = HavenSettings.Current;
        return settings != null && index >= 0 && index < settings.Ranks.Count ? $"a {settings.Ranks[index].name}" : $"rank {index}";
    }
}
