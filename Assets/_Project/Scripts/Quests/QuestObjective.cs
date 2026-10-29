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
    /// <summary>Have a Quest Event happen X times: reach a place, use an object, talk to someone.</summary>
    QuestEvent = 6,
    /// <summary>Build X of a structure (or any structure) with the build menu.</summary>
    BuildStructure = 7,
}

/// <summary>
/// One step of a quest: "Gather 10 Iron Ore", "Kill 3 Forest-Bitten Wolves", "Extract from 2 raids in the Misty Woods". Pick a type
/// and fill in the fields it uses (the Inspector shows only those). It counts matching game events; it holds no progress
/// itself, which the QuestLog keeps per player.
/// <para>
/// Extending, to add an objective type: 1) add an ObjectiveType at the end, 2) add any field it needs here, 3) count it
/// in <see cref="Progress"/> from a GameEventKind (add one in GameEvents if nothing reports it yet), 4) word it in
/// <see cref="Describe"/>, 5) list its fields in QuestObjectiveDrawer so the Inspector shows them.
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
    [Tooltip("Counts what the player already has when the quest starts, including a Quest Event that already happened (counts once).")]
    [SerializeField] private bool countAlreadyOwned;

    public ObjectiveType Type => type;
    public int Amount => Mathf.Max(1, amount);
    public bool CountAlreadyOwned => countAlreadyOwned && (type == ObjectiveType.CollectItem || type == ObjectiveType.GatherResource || type == ObjectiveType.BuildStructure || type == ObjectiveType.QuestEvent);
    public ItemDefinition Item => item;
    public ResourceDefinition Resource => resource;
    /// <summary>What a gather objective counts: the material item, or the essence when no item is set.</summary>
    public Object GatherTarget => item != null ? (Object)item : resource;
    /// <summary>The structure to build or destroy; null counts any.</summary>
    public BuildingDefinition Building => building;
    public GameEventDefinition QuestEvent => questEvent;

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
        return objective;
    }

    /// <summary>How much the event advances this objective; 0 when it doesn't count.</summary>
    public int Progress(in GameEvent gameEvent)
    {
        if (!gameEvent.ByPlayer || gameEvent.Amount <= 0)
        {
            return 0;
        }

        switch (type)
        {
            case ObjectiveType.CollectItem:
                return gameEvent.Kind == GameEventKind.ItemObtained && Is(item, gameEvent.Subject, true) ? gameEvent.Amount : 0;
            case ObjectiveType.GatherResource:
                // Material items arrive in the bag like any item (gathered, looted, found); essences arrive as ResourceGathered.
                GameEventKind arrives = item != null ? GameEventKind.ItemObtained : GameEventKind.ResourceGathered;
                return gameEvent.Kind == arrives && Is(GatherTarget, gameEvent.Subject, true) ? gameEvent.Amount : 0;
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
            default:
                return 0;
        }
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
                return $"Build {(Amount == 1 ? "a" : count)} {(building != null ? building.DisplayName : Amount == 1 ? "structure" : "structures")} (build menu: G)";
            case ObjectiveType.CompleteRaids:
                string verb = mustExtract ? "Extract from" : "Complete";
                string where = raid != null ? $" in {raid.DisplayName}" : "";
                string time = withinMinutes > 0f ? $" within {RaidStats.FormatTime(withinMinutes * 60f)}" : "";
                return $"{verb} {count} raid{(Amount == 1 ? "" : "s")}{where}{time}";
            case ObjectiveType.QuestEvent:
                string text = questEvent != null ? questEvent.ObjectiveText : "(no quest event)";
                return Amount > 1 ? $"{text} ({count} times)" : text;
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
                return questEvent == null ? "has no quest event" : null;
            default:
                return null;
        }
    }

    private static string Name(Object definition)
    {
        switch (definition)
        {
            case ItemDefinition found:
                return found.DisplayName;
            case ResourceDefinition found:
                return found.DisplayName;
            default:
                return "(nothing chosen)";
        }
    }
}
