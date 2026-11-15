using System;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>What kind of thing happened. Values are serialized as numbers, so add new kinds at the end and never renumber.</summary>
public enum GameEventKind
{
    /// <summary>Items arrived in a bag: looted, crafted, picked up. Subject: the ItemDefinition.</summary>
    ItemObtained = 0,
    /// <summary>Essences arrived: gathered from a node, looted or rewarded. Subject: the ResourceDefinition.</summary>
    ResourceGathered = 1,
    /// <summary>A monster died. Subject: its MonsterDefinition (null for creatures without one).</summary>
    Killed = 2,
    /// <summary>A building was destroyed. Subject: its BuildingDefinition (null for structures without one).</summary>
    Destroyed = 3,
    /// <summary>Something was crafted. Subject: the ItemDefinition or ResourceDefinition made.</summary>
    Crafted = 4,
    /// <summary>A raid ended. Subject: the RaidDefinition; Outcome and Seconds say how.</summary>
    RaidFinished = 5,
    /// <summary>A designer-made Quest Event was raised (a trigger zone, an interaction). Subject: the GameEventDefinition.</summary>
    Custom = 6,
    /// <summary>A quest started. Subject: the QuestDefinition.</summary>
    QuestStarted = 7,
    /// <summary>A quest was completed. Subject: the QuestDefinition.</summary>
    QuestCompleted = 8,
    /// <summary>The player placed a building with the build menu. Subject: the BuildingDefinition.</summary>
    Built = 9,
    /// <summary>Something was eaten or drunk. Subject: the ItemDefinition or ResourceDefinition consumed.</summary>
    Consumed = 10,
    /// <summary>A raid event began (Amount 0) or the player acted on it (Amount: the choice, from 1). Subject: the RaidEventDefinition.</summary>
    RaidEvent = 11,
    /// <summary>A fish was landed with a rod. Subject: the fish's ItemDefinition.</summary>
    FishCaught = 12,
    /// <summary>The player started talking to someone or something. Subject: who (ResidentDefinition) or what (QuestTargetDefinition).</summary>
    Talked = 13,
    /// <summary>The player reached a place (a Quest Zone or area marked with it). Subject: the QuestTargetDefinition.</summary>
    Reached = 14,
    /// <summary>The player used, inspected or lit a thing (an Inspect Point marked with it). Subject: the QuestTargetDefinition.</summary>
    Used = 15,
    /// <summary>A stranded person was found and freed. Subject: their ResidentDefinition.</summary>
    PersonFreed = 16,
    /// <summary>A rescued person came home with the player. Subject: their ResidentDefinition.</summary>
    PersonRescued = 17,
    /// <summary>Someone was given work. Subject: the JobDefinition; Detail: their ResidentDefinition.</summary>
    JobAssigned = 18,
    /// <summary>A ward breach was beaten back. Subject: the WardBreachDefinition.</summary>
    BreachRepelled = 19,
    /// <summary>Safe Haven stands at a rank (on reaching it, and again on loading). Amount: the rank, from 1.</summary>
    HavenRankReached = 20,
    /// <summary>Loot or a gathered yield didn't fit and is gone (bag full, bars capped). Subject: the ItemDefinition or ResourceDefinition.</summary>
    LeftBehind = 21,
    /// <summary>The player put things in the stash at home. Subject: the ItemDefinition or ResourceDefinition.</summary>
    Stored = 22,
    /// <summary>A piece of gear was repaired, at a bench or with a kit. Subject: its ItemDefinition.</summary>
    Repaired = 23,
    /// <summary>A building's upgrade was finished. Subject: the BuildingDefinition; Amount: the tier it stands at now, from 2.</summary>
    Upgraded = 24,
    /// <summary>A piece of gear was enchanted at the altar. Subject: the EnchantmentDefinition; Detail: the ItemDefinition.</summary>
    Enchanted = 25,
    /// <summary>An enchantment was learned from an inscription or a teacher. Subject: the EnchantmentDefinition.</summary>
    EnchantmentLearned = 26,
    /// <summary>An inscription was handed to someone (Liv). Subject: the EnchantmentDefinition it carried; Detail: the ResidentDefinition.</summary>
    InscriptionGiven = 27,
    /// <summary>An inscription was found: a drop, a cache, a rubbing. Subject: the EnchantmentDefinition; Amount: its tier.</summary>
    InscriptionFound = 28,
}

/// <summary>
/// Something that happened in the game, in a form any system can react to: what kind of thing, what it was about
/// (an item, a monster, a raid...), how many, who did it and what it happened to. Quests count these; the audio manager
/// plays death sounds from them; GameEventListeners turn them into scene reactions.
/// </summary>
public readonly struct GameEvent
{
    public readonly GameEventKind Kind;
    /// <summary>The definition it is about, such as the monster killed. Null when unknown.</summary>
    public readonly Object Subject;
    public readonly int Amount;
    /// <summary>Who caused it: the killer, the gatherer. Null for the world itself.</summary>
    public readonly GameObject Actor;
    /// <summary>The object it happened to, such as the creature that died. Null when it didn't happen to an object.</summary>
    public readonly GameObject Origin;
    /// <summary>For RaidFinished: how the raid ended.</summary>
    public readonly RaidOutcome Outcome;
    /// <summary>For RaidFinished: how long the raid took.</summary>
    public readonly float Seconds;
    /// <summary>For Killed and Destroyed: the side of what fell. Null when unknown.</summary>
    public readonly Faction? VictimFaction;
    /// <summary>A second thing it was about, when there is one: for JobAssigned, the person given the job.</summary>
    public readonly Object Detail;

    public GameEvent(GameEventKind kind, Object subject, int amount = 1, GameObject actor = null, RaidOutcome outcome = RaidOutcome.InProgress,
        float seconds = 0f, GameObject origin = null, Faction? victimFaction = null, Object detail = null)
    {
        Detail = detail;
        Kind = kind;
        Subject = subject;
        Amount = amount;
        Actor = actor;
        Outcome = outcome;
        Seconds = seconds;
        Origin = origin;
        VictimFaction = victimFaction;
    }

    /// <summary>The side of what was killed: the victim's own, or, when the event didn't say, its monster's.</summary>
    public Faction? KilledFaction => VictimFaction ?? (Subject is MonsterDefinition monster ? monster.Faction : (Faction?)null);

    /// <summary>True when the player themselves did it: their own pickups, meals and builds.</summary>
    public bool ByPlayer => Player.IsPlayer(Actor);

    /// <summary>
    /// True when it counts for the player, for quests and experience: someone on their side did it (the player, a
    /// companion, a tower), or nobody in particular did (raids, quest events, the settlement's work). A death nobody
    /// caused (a fall, the water) is no one's kill.
    /// </summary>
    public bool ForPlayerSide => Actor == null
        ? Kind != GameEventKind.Killed && Kind != GameEventKind.Destroyed
        : FactionRelations.SideOf(Actor)?.IsPlayerSide() == true;
}

/// <summary>
/// The game's event channel (an event bus): gameplay reports facts here ("a Forest-Bitten Wolf was killed by the player",
/// "3 Iron Ore were gathered") and anything interested listens, without the two knowing each other. This is what keeps
/// quests, audio and scene reactions independent of the gameplay code that causes them.
/// <para>
/// Who raises what is listed in ARCHITECTURE.md ("Who raises which game event"); keep that table up to date. In short:
/// inventories and loot grants (ItemObtained, ResourceGathered, Consumed), crafting and building (Crafted, Built), the
/// raid and quest systems (RaidFinished, QuestStarted/Completed), and Quest Event fields on triggers, NPCs and data
/// assets (Custom). Kills and destructions are translated here from every Damageable's death.
/// </para>
/// <para>
/// Not to be confused with GameMessages, which carries text for the player to read, or with per-object C# events such
/// as Damageable.Died, which are for components on or near that object.
/// </para>
/// <para>
/// Extending: to report something new, add a GameEventKind (at the end) and call Raise where it happens. To react to
/// something, subscribe to <see cref="Raised"/> and filter by Kind; unsubscribe when your object goes away.
/// </para>
/// </summary>
public static class GameEvents
{
    /// <summary>Raised for every event. Each listener runs in isolation, so one failing can't stop the others.</summary>
    public static event Action<GameEvent> Raised;

    public static void Raise(GameEvent gameEvent)
    {
        Action<GameEvent> handlers = Raised;
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<GameEvent>)handler)(gameEvent);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    /// <summary>Raises a designer-made event, e.g. from a trigger zone.</summary>
    public static void Raise(GameEventDefinition definition, GameObject actor = null)
    {
        if (definition != null)
        {
            Raise(new GameEvent(GameEventKind.Custom, definition, 1, actor));
        }
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialise()
    {
        Raised = null;
        Damageable.AnyDied -= OnAnyDied;
        Damageable.AnyDied += OnAnyDied;
    }

    private static void OnAnyDied(Damageable victim, DamageInfo killingBlow)
    {
        GameObject killer = killingBlow.Source != null ? killingBlow.Source.gameObject : null;
        GameEventKind kind = victim.Category == TargetCategory.Structure ? GameEventKind.Destroyed : GameEventKind.Killed;
        Object subject = victim.TryGetComponent(out IEventSubject known) ? known.EventSubject : null;
        Raise(new GameEvent(kind, subject, 1, killer, origin: victim.gameObject, victimFaction: victim.Faction));
    }
}
