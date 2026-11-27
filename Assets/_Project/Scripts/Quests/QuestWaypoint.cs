using System.Collections.Generic;
using UnityEngine;

public enum QuestWaypointWhen
{
    Active,
    ReadyToTurnIn,
    Available,
}

/// <summary>
/// Marks where a quest is going on (the shelter, the waystones, the stone in the clearing): while its quest is active
/// (and, with an objective chosen, while that objective is still open), the quest tracker points the way to it with an
/// arrow at the edge of the screen and the distance (QuestWaypointHud). Quick setup: GameObject › Safe Haven › Quest
/// Waypoint, then pick the quest. Available and Ready To Turn In waypoints can lead to its giver before and after the task.
/// </summary>
[AddComponentMenu("Safe Haven/Quests/Quest Waypoint")]
public sealed class QuestWaypoint : MonoBehaviour
{
    private static readonly List<QuestWaypoint> active = new List<QuestWaypoint>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    [SerializeField] private QuestDefinition quest;
    [Tooltip("Only while this objective (1 is the first) is open. 0: for the whole quest.")]
    [SerializeField, Min(0)] private int objective;
    [Tooltip("Shown under the arrow, e.g. \"Shelter\". Empty: the quest's name.")]
    [SerializeField] private string label;
    [SerializeField] private QuestWaypointWhen when;
    [Tooltip("Optional: shown only while this gathering spot still stands (one of the rootbound stones to free): once it is " +
             "gathered the mark goes, though the quest goes on.")]
    [SerializeField] private ResourceNode untilGathered;

    public static IReadOnlyList<QuestWaypoint> Active => active;

    public QuestDefinition Quest => quest;
    /// <summary>The objective (1 is the first) it shows for; 0 for the whole quest.</summary>
    public int Objective => objective;
    public string Label => string.IsNullOrWhiteSpace(label) && quest != null ? quest.DisplayName : label;

    /// <summary>Sets the waypoint up (editor tools).</summary>
    public void Configure(QuestDefinition target, int objectiveNumber, string text, QuestWaypointWhen showWhen = QuestWaypointWhen.Active,
        ResourceNode gathered = null)
    {
        quest = target;
        objective = objectiveNumber;
        label = text;
        when = showWhen;
        untilGathered = gathered;
    }

    public ResourceNode UntilGathered => untilGathered;
    /// <summary>When it shows: before the quest, while it is under way, or once it is ready to hand in.</summary>
    public QuestWaypointWhen When => when;

    /// <summary>True when an earlier wanted waypoint with the same label stands within a metre: someone two quests ask for (Tor, for
    /// Past the Ridge and the Woodcutter's Lodge) gets one mark on the map and minimap, not two on one spot.</summary>
    public bool RepeatsAnEarlier(QuestLog log)
    {
        foreach (QuestWaypoint other in active)
        {
            if (other == this)
            {
                return false;
            }

            if (other != null && other.quest != null && other.Label == Label && other.IsWanted(log)
                && (other.transform.position - transform.position).sqrMagnitude < 1f)
            {
                return true;
            }
        }

        return false;
    }

    // Set at runtime by QuestTargetWaypoints: the journal's "Go there" takes the player only to a place they have walked to before (the
    // first walk out is the finding; the next ones needn't be walked again). Empty with Walk First: never by the journal.
    private string foundFirst;
    private bool walkFirst;

    /// <summary>Lets the journal's "Go there" take the player here only once the place <paramref name="placeId"/> is found; with
    /// none, never (the player walks there).</summary>
    public void GoThereOnceFound(string placeId)
    {
        walkFirst = true;
        foundFirst = placeId;
    }

    // On someone who walks about (QuestTargetWaypoints): the journal goes to them only while they are in the Haven's yard, wherever
    // they stood when the mark was made; out at a worked place, the land is walked first.
    private bool onlyInTheYard;

    /// <summary>Lets the journal's "Go there" take the player here only while it stands in the Haven's yard.</summary>
    public void GoThereOnlyInTheYard()
    {
        onlyInTheYard = true;
        walkFirst = false;
    }

    /// <summary>True when the journal may take the player straight here.</summary>
    public bool CanGoThere => onlyInTheYard
        ? HomePortal.Current != null && HomePortal.Current.InBuildLand(transform.position)
        : !walkFirst || !string.IsNullOrEmpty(foundFirst) && GameSession.Progress.Journal.HasDiscovered(foundFirst);

    // A waypoint on an objective to drink at open water ("Strength for the Road") shows the nearest water instead of where
    // it was put: regenerating the Haven moves its rivers and lakes, and one placed by hand went stale (the user, 2026-11-15).
    private bool followsWater;
    private float nextWaterCheck;

    private void OnEnable()
    {
        active.Add(this);
        GameEventDefinition drink = SurvivalSettings.Current != null ? SurvivalSettings.Current.DrankAtWater : null;
        int index = objective - 1;
        followsWater = drink != null && quest != null && index >= 0 && index < quest.Objectives.Count
            && quest.Objectives[index].QuestEvent == drink;
    }

    private void OnDisable() => active.Remove(this);

    private void Update()
    {
        if (!followsWater || Time.time < nextWaterCheck || Player.Current == null)
        {
            return;
        }

        nextWaterCheck = Time.time + 0.5f;
        if (WaterSource.Nearest(Player.Current.transform.position, out Vector3 water))
        {
            transform.position = water;
        }
    }

    /// <summary>True while the player should be shown the way here.</summary>
    public bool IsWanted(QuestLog log)
    {
        if (quest == null || log == null || (untilGathered != null && untilGathered.IsDepleted))
        {
            return false;
        }

        QuestLog.Entry entry = log.Get(quest);
        if (when == QuestWaypointWhen.Available)
            return log.StatusOf(quest) == QuestStatus.NotStarted && log.PrerequisitesMet(quest);
        if (when == QuestWaypointWhen.ReadyToTurnIn)
            return entry != null && entry.Status == QuestStatus.ReadyToTurnIn;
        if (entry == null || entry.Status != QuestStatus.Active)
        {
            return false;
        }

        int index = objective - 1;
        return index < 0 || index >= entry.Counts.Length || !entry.IsObjectiveDone(index) && entry.IsObjectiveOpen(index);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.3f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 3f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 3f, 0.3f);
    }
}
