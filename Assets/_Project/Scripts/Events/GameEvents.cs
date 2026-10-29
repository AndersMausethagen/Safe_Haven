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

    public GameEvent(GameEventKind kind, Object subject, int amount = 1, GameObject actor = null, RaidOutcome outcome = RaidOutcome.InProgress,
        float seconds = 0f, GameObject origin = null, Faction? victimFaction = null)
    {
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

    /// <summary>True when the player caused it, or nobody in particular did (raids, quest events).</summary>
    public bool ByPlayer => Actor == null || Player.IsPlayer(Actor);
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
        GameObject origin = victim.gameObject;
        Faction side = victim.Faction;
        if (victim.TryGetComponent(out MonsterAI monster))
        {
            Raise(new GameEvent(GameEventKind.Killed, monster.Definition, 1, killer, origin: origin, victimFaction: side));
        }
        else if (victim.TryGetComponent(out BuildingObject building))
        {
            Raise(new GameEvent(GameEventKind.Destroyed, building.Definition, 1, killer, origin: origin, victimFaction: side));
        }
        else
        {
            GameEventKind kind = victim.Category == TargetCategory.Structure ? GameEventKind.Destroyed : GameEventKind.Killed;
            Raise(new GameEvent(kind, null, 1, killer, origin: origin, victimFaction: side));
        }
    }
}
