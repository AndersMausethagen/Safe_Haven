using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The player's deeds by target, as the quest log remembers them: talked to Sigrid, reached the Western Beacon, used the
/// Lake Stone, freed or brought home Ingrid. Each is a key in the log's saved set beside Quest Event ids, so an objective
/// with Count Already Owned, a quest that starts when the player does something, and a once-per-game trigger all know a
/// deed done before (or before a save). One place for the keys, so raising and asking always agree.
/// </summary>
public static class QuestDeeds
{
    /// <summary>The key a game event leaves in the log, or null for events that aren't remembered this way.</summary>
    public static string KeyOf(in GameEvent gameEvent) => Key(gameEvent.Kind, gameEvent.Subject);

    /// <summary>The key of a deed: what was done (Talked, Reached, Used, PersonFreed, PersonRescued) to whom.</summary>
    public static string Key(GameEventKind kind, Object subject)
    {
        string verb = Verb(kind);
        return verb != null && subject is IQuestTarget target && !string.IsNullOrEmpty(target.QuestId) ? verb + target.QuestId : null;
    }

    /// <summary>Whether a deed of this kind counts while visiting a friend's Haven: the rule of the objective type it is for.</summary>
    public static bool CountsInAFriendsHaven(GameEventKind kind)
    {
        switch (kind)
        {
            case GameEventKind.Talked: return QuestObjective.CountsInAFriendsHaven(ObjectiveType.TalkTo);
            case GameEventKind.Reached: return QuestObjective.CountsInAFriendsHaven(ObjectiveType.ReachPlace);
            case GameEventKind.Used: return QuestObjective.CountsInAFriendsHaven(ObjectiveType.UseObject);
            case GameEventKind.PersonFreed:
            case GameEventKind.PersonRescued: return QuestObjective.CountsInAFriendsHaven(ObjectiveType.RescuePerson);
            default: return true;
        }
    }

    private static string Verb(GameEventKind kind)
    {
        switch (kind)
        {
            case GameEventKind.Talked: return "talked:";
            case GameEventKind.Reached: return "reached:";
            case GameEventKind.Used: return "used:";
            case GameEventKind.PersonFreed: return "freed:";
            case GameEventKind.PersonRescued: return "rescued:";
            default: return null;
        }
    }

    // Saves from before deeds remembered these as Quest Events made one per person or place; each still counts as its deed.
    private static readonly System.Collections.Generic.Dictionary<string, string> Legacy = new System.Collections.Generic.Dictionary<string, string>
    {
        ["talkedtohearttree"] = "talked:the-portal",
        ["prologue.portalinspected"] = "used:the-portal",
        ["fetchedwater"] = "used:drinking-spot",
        ["heard_the_wilds_echo"] = "reached:wilds-echo",
        ["reached_western_beacon"] = "reached:western-beacon",
        ["prologue.shelterfound"] = "reached:rock-shelter",
        ["prologue.portalreached"] = "reached:waystones",
        ["prologue.talked_to_bjarne"] = "talked:expedition.bjarne",
        ["prologue.talked_to_eirik"] = "talked:expedition.eirik",
        ["prologue.talked_to_ingrid"] = "talked:ingrid",
        ["prologue.talked_to_liv"] = "talked:expedition.liv",
        ["prologue.talked_to_runa"] = "talked:expedition.runa",
        ["prologue.talked_to_sigrid"] = "talked:expedition.sigrid",
        ["prologue.talked_to_tor"] = "talked:expedition.tor",
    };

    /// <summary>The deed an old save's Quest Event id stands for, or null.</summary>
    public static string FromLegacy(string eventId) => eventId != null && Legacy.TryGetValue(eventId, out string key) ? key : null;

    /// <summary>Reports a deed: <paramref name="kind"/> done to <paramref name="target"/> by <paramref name="actor"/>.</summary>
    public static void Raise(GameEventKind kind, Object target, GameObject actor = null, Object detail = null)
    {
        if (target != null || detail != null)
        {
            GameEvents.Raise(new GameEvent(kind, target, 1, actor, detail: detail));
        }
    }
}
